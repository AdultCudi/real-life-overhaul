using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger in a Going Public session (Core/SharedCity, part L): ONE paper for the city. The host prints the
    /// morning's edition as alone; every guest reads that same edition, and prints none of its own while it follows.
    /// Game-free (EconomyCheck, FlareLiveChecks.cs).
    ///
    ///   the whole part   "LS1|day", then the edition as the store writes it (E, P, S, Q and C lines: LedgerStore.Edition)
    ///   a guest's asks   N|kind|x|z|detail|amount     something the paper would print happened on the guest's street (a
    ///                                                crash, an arrest, a crime): the host reports it to its own paper
    ///                    P|section|weight|key|head|name|value...   a piece about the guest themselves for today's paper
    ///                                                (their arrest in court, a graduation, their business, a post that
    ///                                                went viral): the host prints it in today's edition, a few a day
    ///
    /// So the paper has stories about any player in that city. A piece is taken only with a key of the mod's own pieces
    /// about a player (<see cref="PersonalPrefixes"/>), a few names and short values; a report only of a kind the paper
    /// prints. A guest's own edition, if it had one, is kept to be read again (LedgerDirector's earlier editions).
    /// </summary>
    public static class LedgerShare
    {
        public const char Part = 'L';
        public const string Version = "LS1";
        public const int PiecesPerGuestDay = 4;      // a guest's own pieces in one edition at most
        public const int ValueMost = 120;            // characters of a name's value in a guest's piece
        public const int PairsMost = 8;
        public const int AsksMost = 24;              // lines of one request the host reads
        public const int CityKinds = 7;              // Social/News.NewsKind: Accident, PedestrianHit, HitAndRun, Crime, Arrest, BreakIn, CarBreakIn (the city's); after them the player's own and Story
        public const int GuestPiecesMost = 32;       // the guests' pieces the host keeps for a day, all guests together
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The keys of pieces about a player, the only ones a guest may send for the paper.</summary>
        public static readonly string[] PersonalPrefixes =
        {
            P + "_news_player", P + "_study_news", P + "_life_news", P + "_own_news", P + "_flare_news", P + "_co_news", P + "_ob_news", P + "_news_shop", P + "_news_story"
        };

        private const string P = "reallife";   // (the beginnings of keys, not keys: written so that the locale check does not read them as keys)

        /// <summary>The host's edition as the part's text; null for none.</summary>
        public static string Write(LedgerEdition e)
        {
            if (e == null)
            {
                return null;
            }
            var b = new StringBuilder(8192);
            b.Append(Version).Append('|').Append(e.Day.ToString(Inv));
            LedgerStore.Edition(b, e);
            return b.ToString();
        }

        /// <summary>The host's edition read back; null for a text that does not read.</summary>
        public static LedgerEdition Read(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            string[] lines = text.Split('\n');
            string[] head = lines[0].TrimEnd('\r').Split('|');
            if (head.Length < 2 || head[0] != Version)
            {
                return null;
            }
            LedgerEdition e = null;
            for (int i = 1; i < lines.Length; i++)
            {
                try
                {
                    LedgerStore.EditionLine(lines[i].TrimEnd('\r').Split('|'), ref e);
                }
                catch (Exception)
                {
                    // one line this version cannot read: left out
                }
            }
            return LedgerStore.Finish(e);
        }

        /// <summary>
        /// Whether a report of the paper's is the city's (a crash, a knockdown, a hit and run, a crime, an arrest, a break-in),
        /// to go to the host's paper from a guest. The player's own crash, arrest and knockdown and a finished story are not:
        /// in the host's paper they would be the host player's.
        /// </summary>
        public static bool CityKind(int kind)
        {
            return kind >= 0 && kind < CityKinds;
        }

        /// <summary>A guest's report for the host's paper, as a line of its request; null for one that is not the city's.</summary>
        public static string AskReport(int kind, float x, float z, string detail, int amount)
        {
            if (!CityKind(kind))
            {
                return null;
            }
            return "N|" + kind.ToString(Inv) + "|" + x.ToString("0.#", Inv) + "|" + z.ToString("0.#", Inv) + "|" + LedgerStore.Clean(detail) + "|" + amount.ToString(Inv);
        }

        /// <summary>A guest's piece about themselves, as a line; null for one that is not about a player.</summary>
        public static string AskPiece(LedgerPiece p)
        {
            if (p == null || !Personal(p.Key))
            {
                return null;
            }
            var b = new StringBuilder(128);
            b.Append("P|").Append((int)p.Section).Append('|').Append(p.Weight).Append('|').Append(LedgerStore.Clean(p.Key)).Append('|').Append(LedgerStore.Clean(p.Head));
            for (int i = 0; i + 1 < p.Data.Length && i < PairsMost * 2; i += 2)
            {
                b.Append('|').Append(LedgerStore.Clean(p.Data[i])).Append('|').Append(LedgerStore.Clean(Cut(p.Data[i + 1])));
            }
            return b.ToString();
        }

        public static bool Personal(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 80)
            {
                return false;
            }
            for (int i = 0; i < PersonalPrefixes.Length; i++)
            {
                if (key.StartsWith(PersonalPrefixes[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A guest's report read by the host: false for a kind out of range or a line that does not read.</summary>
        public static bool ReadReport(string[] f, int kinds, out int kind, out float x, out float z, out string detail, out int amount)
        {
            kind = amount = 0;
            x = z = 0f;
            detail = null;
            if (f.Length < 6 || f[0] != "N" || !int.TryParse(f[1], NumberStyles.Integer, Inv, out kind) || kind < 0 || kind >= kinds || !CityKind(kind))
            {
                return false;
            }
            if (!float.TryParse(f[2], NumberStyles.Float, Inv, out x) || !float.TryParse(f[3], NumberStyles.Float, Inv, out z) || float.IsNaN(x) || float.IsNaN(z)
                || float.IsInfinity(x) || float.IsInfinity(z))
            {
                return false;
            }
            detail = f[4].Length > 0 && f[4].Length <= 64 ? f[4] : null;
            int.TryParse(f[5], NumberStyles.Integer, Inv, out amount);
            return true;
        }

        /// <summary>A guest's piece read by the host: null for one that is not about a player, or does not read.</summary>
        public static LedgerPiece ReadPiece(string[] f)
        {
            if (f.Length < 5 || f[0] != "P" || !Personal(f[3]))
            {
                return null;
            }
            int section, weight;
            if (!int.TryParse(f[1], NumberStyles.Integer, Inv, out section) || section < 0 || section >= (int)LedgerSection.Count || !int.TryParse(f[2], NumberStyles.Integer, Inv, out weight))
            {
                return null;
            }
            int pairs = Math.Min(PairsMost, (f.Length - 5) / 2);
            var data = pairs > 0 ? new string[pairs * 2] : LedgerPiece.NoData;
            for (int i = 0; i < pairs * 2; i++)
            {
                data[i] = Cut(f[5 + i]);
            }
            return new LedgerPiece((LedgerSection)section, Math.Max(0, Math.Min(100, weight)), f[3], f[4].Length > 0 && f[4].Length <= 80 ? f[4] : null, data);
        }

        /// <summary>
        /// The host takes a guest's piece for today's edition: each guest's first few (<see cref="PiecesPerGuestDay"/>), each
        /// once, and a few in all; its values (names, words) through the filter given (what another player's game wrote, as the
        /// host's players will read it). True when it was taken.
        /// </summary>
        public static bool Admit(List<KeyValuePair<string, LedgerPiece>> pieces, string who, LedgerPiece p, Func<string, string> filter)
        {
            if (p == null || pieces.Count >= GuestPiecesMost)
            {
                return false;
            }
            who = who ?? string.Empty;
            int n = 0;
            string id = p.Key + "|" + string.Join("|", p.Data);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Key == who)
                {
                    n++;
                    if (pieces[i].Value.Key + "|" + string.Join("|", pieces[i].Value.Data) == id)
                    {
                        return false;
                    }
                }
            }
            if (n >= PiecesPerGuestDay)
            {
                return false;
            }
            if (filter != null)
            {
                for (int i = 1; i < p.Data.Length; i += 2)
                {
                    p.Data[i] = filter(p.Data[i]) ?? string.Empty;
                }
            }
            pieces.Add(new KeyValuePair<string, LedgerPiece>(who, p));   // (its head is a locale key of the paper's own, not words)
            return true;
        }

        /// <summary>When today's edition is printed again with the guests' pieces: a while after the first of them came, not put off by each that follows.</summary>
        public static float ReprintAt(float set, float now, float wait)
        {
            return set >= 0f ? set : now + wait;
        }

        private static string Cut(string value)
        {
            return value != null && value.Length > ValueMost ? value.Substring(0, ValueMost) : value ?? string.Empty;
        }

        /// <summary>
        /// The guests' pieces for an edition: a few of each guest's, the heaviest first, each once (the same key and names
        /// twice is one piece). Into the list given (the edition's own pieces follow them in the page's order by weight).
        /// </summary>
        public static void Take(List<KeyValuePair<string, LedgerPiece>> asked, List<LedgerPiece> into)
        {
            var count = new Dictionary<string, int>(4);
            var seen = new HashSet<string>();
            for (int i = 0; i < asked.Count; i++)
            {
                string who = asked[i].Key ?? string.Empty;
                LedgerPiece p = asked[i].Value;
                int n;
                count.TryGetValue(who, out n);
                string id = p.Key + "|" + string.Join("|", p.Data);
                if (n >= PiecesPerGuestDay || !seen.Add(id))
                {
                    continue;
                }
                count[who] = n + 1;
                into.Add(p);
            }
        }
    }
}
