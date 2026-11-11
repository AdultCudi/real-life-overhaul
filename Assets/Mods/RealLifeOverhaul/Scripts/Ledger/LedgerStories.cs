using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>One listed company at the market's last close, as the milestones read it.</summary>
    public struct LedgerCompany
    {
        public int Id;                   // its id on the market
        public string Name;
        public double Value;             // all its shares at the last close, in money
        public bool Player;              // a player's own listed company (any player's, in a Going Public session)
        public bool Rival;               // a rival tycoon's holding company: one of the mod's tycoons', or one of the game's own rivals'
        public int Sector;               // its part of the market, as a number (LedgerLeads)
    }

    /// <summary>A rival tycoon taken over by the player: the facts of the next morning's story.</summary>
    public sealed class LedgerBeat
    {
        public int Tycoon;               // the rival's index (Rivals' catalogue)
        public int Day;                  // the day of the takeover
        public int Seed;                 // the rivals' own seed: which version of the story is printed
        public string Rival = string.Empty;
        public string Holding = string.Empty;
        public string Buyer = string.Empty;   // the player's listed company, else the player
        public string Owner = string.Empty;   // the player, by name
        public string Quip = string.Empty;    // the rival's own line, as the rivals' pieces end
        public double Cost;              // what the shares bought cost, in money; 0 not known
        public double Back;              // what the holding's parts bring the player...
        public int Days;                 // ...this many days after the takeover
    }

    /// <summary>What one morning's stories came to, for the self-check line.</summary>
    public struct LedgerStoryCount
    {
        public int Beaten, Milestones, Top, Leads, Seeded;
    }

    /// <summary>
    /// The City Ledger's stories of the market's big moments (a player's suggestion, 29 Oct 2026): a rival tycoon taken
    /// over, a listed company's market value passing a round figure for the first time, a new most valuable company in the
    /// city; a player's company and a rival tycoon's holding are told alike, the city's other companies at their first
    /// billion. All of it is read from what the save already has (the rivals' file, the market's last close) and from the
    /// highest figure printed per company (LedgerState.Marks), so a reload never prints a story twice. A sector's lead
    /// changing hands is <see cref="LedgerLeads"/>'s. Game-free; the cost is one pass over the listed companies once a
    /// morning, nothing per frame.
    /// </summary>
    public static class LedgerStories
    {
        private const string P = "reallife";   // in parts: keys' beginnings, not keys

        public static readonly string BeatenPrefix = P + "_ledger_beaten";
        public static readonly string[] BeatenKeys = { P + "_ledger_beaten_1", P + "_ledger_beaten_2", P + "_ledger_beaten_3" };
        public static readonly string BeatenPlain = P + "_ledger_beaten_plain";   // the cost is not known
        public static readonly string MileOwn = P + "_ledger_mile_own";           // a player's company under a billion
        public static readonly string MileBig = P + "_ledger_mile_big";           // ...at a billion and over
        public static readonly string MileCity = P + "_ledger_mile_city";         // a city company's first billion
        public static readonly string MileRival = P + "_ledger_mile_rival";       // a rival tycoon's holding passing any figure of the ladder
        public static readonly string TopFirst = P + "_ledger_top_first";         // a player's company the most valuable, its first time
        public static readonly string TopAgain = P + "_ledger_top_again";
        public static readonly string TopLost = P + "_ledger_top_lost";           // a player's company overtaken
        public static readonly string TopRival = P + "_ledger_top_rival";         // a rival tycoon's holding the most valuable
        public static readonly string TopCity = P + "_ledger_top_city";
        public static readonly string BoughtOwn = P + "_ledger_bought_own";       // a listed company took a player's company over
        public static readonly string BoughtCity = P + "_ledger_bought_city";     // ...or a city company
        public static readonly string OpenedCity = P + "_ledger_opened_city";     // a business of the city opened, the edition after the day it did
        public static readonly string OpenedRival = P + "_ledger_opened_rival";   // ...or a rival's

        /// <summary>The rivals' own one line for a takeover (RivalDirector.Paper), which the full story replaces.</summary>
        public static readonly string RivalTakeoverKey = P + "_rival_paper_done_takeover";

        private static readonly string[] KeptPrefixes = { P + "_ledger_beaten", P + "_ledger_mile", P + "_ledger_top", P + "_ledger_lead" };
        private static readonly string BoughtPrefix = P + "_ledger_bought";

        /// <summary>
        /// The round figures of a company's market value, in money. The city's own companies are worth a hundred million to
        /// a few tens of billions (Markets/MarketCatalogue: turnover of 120 million to 22 billion), so every step can be
        /// reached and none is passed every week; a player's company climbs it from its listing.
        /// </summary>
        public static readonly long[] Ladder =
        {
            100000000L, 250000000L, 500000000L, 1000000000L, 2000000000L, 3000000000L, 4000000000L, 5000000000L, 6000000000L, 7000000000L,
            8000000000L, 9000000000L, 10000000000L, 25000000000L, 50000000000L, 100000000000L
        };

        public const int BigRung = 4;            // Ladder[BigRung - 1] is a billion: a player's milestone from here is front page news
        public const int CityRung = 4;           // the city's companies are news at their first billion only
        public const int TopLeadBp = 200;        // a company takes the top place only this far ahead of the holder, so two close rivals do not swap it every day
        public const int MarksMost = 400;        // companies kept: the market lists about 160

        /// <summary>How many of the ladder's figures a value has passed (reached counts).</summary>
        public static int Rung(double value)
        {
            int rung = 0;
            while (rung < Ladder.Length && value >= Ladder[rung])
            {
                rung++;
            }
            return rung;
        }

        /// <summary>True for a story the paper keeps when today's edition is written again: it was read from the state once, on the edition's first print.</summary>
        public static bool Kept(string key)
        {
            for (int i = 0; key != null && i < KeptPrefixes.Length; i++)
            {
                if (key.StartsWith(KeptPrefixes[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True for any of these stories, a takeover by a listed company included.</summary>
        public static bool Story(string key)
        {
            return Kept(key) || (key != null && key.StartsWith(BoughtPrefix, StringComparison.Ordinal));
        }

        /// <summary>True for a story of a business that opened: read from the city's record on the edition's first print and kept when it is written again.</summary>
        public static bool OpenedStory(string key)
        {
            return key != null && (key == OpenedCity || key == OpenedRival);
        }

        /// <summary>The story of a business that opened, naming it; null for one without a name.</summary>
        public static LedgerPiece Opened(LedgerOpening o)
        {
            if (string.IsNullOrEmpty(o.Firm))
            {
                return null;
            }
            return LedgerRules.Piece(o.Rival ? OpenedRival : OpenedCity, new[] { "firm", o.Firm });
        }

        /// <summary>A number from 0 below <paramref name="count"/>, the same on every machine for the same seed, day and salt.</summary>
        public static int Draw(int seed, int day, int salt, int count)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)day) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return count > 0 ? (int)(h % (uint)count) : 0;
            }
        }

        /// <summary>The version of a rival's story: one of three, drawn from the rivals' seed, the day and the rival; without the cost, the plain one.</summary>
        public static string BeatenKey(LedgerBeat b)
        {
            return b.Cost > 0.0 ? BeatenKeys[Draw(b.Seed, b.Day, b.Tycoon, BeatenKeys.Length)] : BeatenPlain;
        }

        /// <summary>The front page story of a rival taken over; null for one without a rival.</summary>
        public static LedgerPiece Beaten(LedgerBeat b, Func<double, string> money)
        {
            if (b == null || string.IsNullOrEmpty(b.Rival))
            {
                return null;
            }
            return LedgerRules.Piece(BeatenKey(b), new[]
            {
                "rival", b.Rival, "holding", b.Holding, "buyer", b.Buyer, "owner", b.Owner, "cost", money(b.Cost), "back", money(b.Back),
                "days", b.Days.ToString(System.Globalization.CultureInfo.InvariantCulture), "quip", b.Quip ?? string.Empty
            });
        }

        /// <summary>A takeover completed by a listed company: of a player's company, or of one of the city's.</summary>
        public static LedgerPiece Bought(string bidder, string name, string price, bool own)
        {
            return LedgerRules.Piece(own ? BoughtOwn : BoughtCity, new[] { "bidder", bidder ?? string.Empty, "name", name ?? string.Empty, "price", price ?? string.Empty });
        }

        /// <summary>
        /// The morning's milestones from the companies at the last close, the state brought up to date. A company the paper
        /// has not seen yet (every one, for a save without the record) is set where it stands without a story; afterwards a
        /// player's company and a rival tycoon's holding have a story for the highest figure they passed, a city company for its
        /// first billion, and a new most valuable company one of its own. Quiet: the state follows, nothing is printed (a guest
        /// reading the host's paper).
        /// </summary>
        public static void Marks(LedgerState s, List<LedgerCompany> companies, bool quiet, Func<double, string> money, List<LedgerPiece> into, ref LedgerStoryCount count)
        {
            bool seed = !s.MarksKept;
            bool tell = !seed && !quiet;
            int leader = -1;
            for (int i = 0; i < companies.Count; i++)
            {
                LedgerCompany c = companies[i];
                if (leader < 0 || c.Value > companies[leader].Value || (c.Value == companies[leader].Value && c.Id < companies[leader].Id))
                {
                    leader = i;
                }
                int rung = Rung(c.Value);
                int at = Find(s.Marks, c.Id);
                if (at < 0)
                {
                    if (s.Marks.Count < MarksMost)
                    {
                        s.Marks.Add(new LedgerMark { Id = c.Id, Rung = rung });
                    }
                    if (seed)
                    {
                        count.Seeded++;
                    }
                    continue;
                }
                LedgerMark m = s.Marks[at];
                if (rung <= m.Rung)
                {
                    continue;
                }
                if (tell && c.Player)
                {
                    Add(into, LedgerRules.Piece(rung >= BigRung ? MileBig : MileOwn, new[] { "name", c.Name, "mark", money(Ladder[rung - 1]), "value", money(c.Value) }));
                    count.Milestones++;
                }
                else if (tell && c.Rival)
                {
                    Add(into, LedgerRules.Piece(MileRival, new[] { "name", c.Name, "mark", money(Ladder[rung - 1]), "value", money(c.Value) }));
                    count.Milestones++;
                }
                else if (tell && m.Rung < CityRung && rung >= CityRung)
                {
                    Add(into, LedgerRules.Piece(MileCity, new[] { "name", c.Name, "mark", money(Ladder[CityRung - 1]), "value", money(c.Value) }));
                    count.Milestones++;
                }
                m.Rung = rung;
                s.Marks[at] = m;
            }
            for (int i = s.Marks.Count - 1; i >= 0; i--)
            {
                if (IndexOf(companies, s.Marks[i].Id) < 0)
                {
                    s.Marks.RemoveAt(i);   // gone from the market: should it come back years on, it is a new listing
                }
            }
            Top(s, companies, leader, tell, money, into, ref count);
            s.MarksKept = true;
        }

        // The most valuable company: the leader takes the place from a holder still listed only TopLeadBp ahead of it.
        private static void Top(LedgerState s, List<LedgerCompany> companies, int leader, bool tell, Func<double, string> money, List<LedgerPiece> into, ref LedgerStoryCount count)
        {
            if (leader < 0)
            {
                return;
            }
            LedgerCompany top = companies[leader];
            int held = s.TopId != 0 ? IndexOf(companies, s.TopId) : -1;
            if (top.Id == s.TopId || (held >= 0 && top.Value * 10000.0 < companies[held].Value * (10000.0 + TopLeadBp)))
            {
                return;
            }
            int second = -1;
            for (int i = 0; i < companies.Count; i++)
            {
                if (i != leader && (second < 0 || companies[i].Value > companies[second].Value))
                {
                    second = i;
                }
            }
            int at = Find(s.Marks, top.Id);
            bool before = at >= 0 && s.Marks[at].Topped;
            if (tell && s.TopId != 0 && second >= 0)
            {
                LedgerCompany other = companies[second];
                if (top.Player)
                {
                    Add(into, LedgerRules.Piece(before ? TopAgain : TopFirst, new[] { "name", top.Name, "value", money(top.Value), "other", other.Name, "other_value", money(other.Value) }));
                }
                else if (held >= 0 && companies[held].Player)
                {
                    LedgerCompany lost = companies[held];
                    Add(into, LedgerRules.Piece(TopLost, new[] { "name", lost.Name, "value", money(lost.Value), "other", top.Name, "other_value", money(top.Value) }));
                }
                else if (top.Rival)
                {
                    Add(into, LedgerRules.Piece(TopRival, new[] { "name", top.Name, "value", money(top.Value), "other", other.Name, "other_value", money(other.Value) }));
                }
                else
                {
                    Add(into, LedgerRules.Piece(TopCity, new[] { "name", top.Name, "value", money(top.Value), "other", other.Name, "other_value", money(other.Value) }));
                }
                count.Top++;
            }
            s.TopId = top.Id;
            if (at >= 0)
            {
                LedgerMark m = s.Marks[at];
                m.Topped = true;
                s.Marks[at] = m;
            }
        }

        /// <summary>The stories switched off: what the paper remembered goes, so switched on again it starts where the market stands.</summary>
        public static void Forget(LedgerState s)
        {
            s.MarksKept = false;
            s.TopId = 0;
            s.Marks.Clear();
            LedgerLeads.Forget(s);
        }

        /// <summary>The rivals' one line of a takeover is taken out where the full story of the same rival is in; returns how many went.</summary>
        public static int Covered(List<LedgerPiece> pieces)
        {
            int gone = 0;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                LedgerPiece p = pieces[i];
                if (p == null || p.Key != RivalTakeoverKey)
                {
                    continue;
                }
                string rival = p.Value("rival");
                for (int j = 0; j < pieces.Count; j++)
                {
                    LedgerPiece q = pieces[j];
                    if (q != null && q.Key.StartsWith(BeatenPrefix, StringComparison.Ordinal) && q.Value("rival") == rival)
                    {
                        pieces.RemoveAt(i);
                        gone++;
                        break;
                    }
                }
            }
            return gone;
        }

        /// <summary>
        /// The test's milestone story: the player's company at the highest figure it has passed, else the city's most valuable
        /// company at its first billion; null when neither has a figure to print.
        /// </summary>
        public static LedgerPiece TestMark(List<LedgerCompany> companies, Func<double, string> money, out bool own, out string name)
        {
            own = false;
            name = null;
            int player = -1, city = -1;
            for (int i = 0; i < companies.Count; i++)
            {
                LedgerCompany c = companies[i];
                if (c.Player && Rung(c.Value) > 0 && (player < 0 || c.Value > companies[player].Value))
                {
                    player = i;
                }
                else if (!c.Player && (city < 0 || c.Value > companies[city].Value))
                {
                    city = i;
                }
            }
            if (player >= 0)
            {
                LedgerCompany c = companies[player];
                int rung = Rung(c.Value);
                own = true;
                name = c.Name;
                return LedgerRules.Piece(rung >= BigRung ? MileBig : MileOwn, new[] { "name", c.Name, "mark", money(Ladder[rung - 1]), "value", money(c.Value) });
            }
            if (city < 0 || Rung(companies[city].Value) < CityRung)
            {
                return null;
            }
            LedgerCompany k = companies[city];
            name = k.Name;
            return LedgerRules.Piece(MileCity, new[] { "name", k.Name, "mark", money(Ladder[CityRung - 1]), "value", money(k.Value) });
        }

        /// <summary>How many of these stories a list holds.</summary>
        public static int Count(List<LedgerPiece> pieces)
        {
            int n = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] != null && Story(pieces[i].Key))
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>Adds a piece unless one of the same key and names is in already (a guest's piece taken again, a test pressed twice).</summary>
        public static bool AddOnce(List<LedgerPiece> into, LedgerPiece piece)
        {
            if (piece == null)
            {
                return false;
            }
            for (int i = 0; i < into.Count; i++)
            {
                if (Same(into[i], piece))
                {
                    return false;
                }
            }
            into.Add(piece);
            return true;
        }

        private static bool Same(LedgerPiece a, LedgerPiece b)
        {
            if (a == null || a.Key != b.Key || a.Data.Length != b.Data.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Data.Length; i++)
            {
                if (a.Data[i] != b.Data[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static void Add(List<LedgerPiece> into, LedgerPiece piece)
        {
            if (piece != null)
            {
                into.Add(piece);
            }
        }

        private static int Find(List<LedgerMark> marks, int id)
        {
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int IndexOf(List<LedgerCompany> companies, int id)
        {
            for (int i = 0; i < companies.Count; i++)
            {
                if (companies[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
