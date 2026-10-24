using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>A building the construction game finished on one of Uptown's lots: a district building from the next load on.</summary>
    public sealed class FinishedBuilding
    {
        public string Lot;           // the plan's lot id
        public string Size;          // the game's size id
        public int Version;          // the structure's version
        public string Type;          // the game's building type id
        public string Design;        // a design's name, or empty for one the copy engine picks
        public int Storeys;          // storeys of floor area its record has; 0: the floor area of the game's building its design copies
        public FinishOwner Owner;    // who it is given to at its first load
        public double Price;         // dollars the player paid in all, the deed's purchase price (the player's own only)
        public int Day;              // the game day it was finished
        public bool Settled;         // its first load's setting up is done (the deed, or let to the city): never done twice
        public string[] Over;        // the other lots of its block it stands over too (a landmark's), or null: its own lot alone
    }

    /// <summary>What the district keeps in the save: the buildings the construction game finished.</summary>
    public sealed class LifeState
    {
        public readonly List<FinishedBuilding> Finished = new List<FinishedBuilding>(8);

        /// <summary>The opening pass has run in this save (LifeGame.OpenUnits): once, so the units the game's own days empty later stay its own.</summary>
        public bool Opened;

        /// <summary>Lots new to the save after the first pass that a later pass has been over: each is filled once.</summary>
        public readonly List<string> OpenedLots = new List<string>(8);

        /// <summary>The generation of Uptown's plan its lots are of (LayoutRules.PlanGeneration); a line without one is of the first plan.</summary>
        public int Plan = LayoutRules.PlanGeneration;

        /// <summary>True when a later opening pass has been over the lot.</summary>
        public bool OpenedLot(string lot)
        {
            return !string.IsNullOrEmpty(lot) && OpenedLots.Contains(lot);
        }

        /// <summary>The lot marked as gone over by a later pass (at most <see cref="LifeStore.OpenedMost"/> kept).</summary>
        public void MarkOpened(string lot)
        {
            if (!string.IsNullOrEmpty(lot) && !OpenedLots.Contains(lot) && OpenedLots.Count < LifeStore.OpenedMost)
            {
                OpenedLots.Add(lot);
            }
        }

        /// <summary>The finished building on a lot, or null.</summary>
        public FinishedBuilding On(string lot)
        {
            for (int i = 0; i < Finished.Count; i++)
            {
                if (string.Equals(Finished[i].Lot, lot, StringComparison.Ordinal))
                {
                    return Finished[i];
                }
            }
            return null;
        }

        /// <summary>The finished building on a lot or standing over it as well as its own (a landmark's), or null.</summary>
        public FinishedBuilding Covering(string lot)
        {
            for (int i = 0; i < Finished.Count; i++)
            {
                FinishedBuilding f = Finished[i];
                if (string.Equals(f.Lot, lot, StringComparison.Ordinal))
                {
                    return f;
                }
                for (int k = 0; f.Over != null && k < f.Over.Length; k++)
                {
                    if (string.Equals(f.Over[k], lot, StringComparison.Ordinal))
                    {
                        return f;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The district's line in the save, game-free: one line under <see cref="Key"/>, "v=1^f:finished" and "^o:1" once the
    /// opening pass has run, "^n:lot,lot" for the lots a later pass has been over and "^g:2" the plan's generation its
    /// lots are of (none: the first plan, LifeStore.PlanOf), records divided by ';'
    /// and fields by '|' (lot, size, version, type, design, storeys, owner, price, day, settled, the other lots it stands over joined by ','), in the order
    /// <see cref="Write"/> joins them. A newer version adds fields at the end of a record, or a part with a new letter: this
    /// reader skips what it does not know, bounds every figure and caps the list. The line names Uptown's lots, never an
    /// address of the game's part of the save, which the district save keeps clear (it is in the mod's own section). The
    /// save tools leave it with the district's records (DistrictSaveDirector.KeepsAlso): without it the buildings and what
    /// is in them would never come back. COST: nothing per frame; a parse at a load and a write when a building is finished.
    /// </summary>
    public static class LifeStore
    {
        public const string Key = "reallife:uptown";
        public const int Version = 1;
        public const int FinishedMost = 512;   // records read at most: more lots than the whole band north of Manhattan holds
        public const int OpenedMost = 512;     // lots a later opening pass is marked for at most: more than the band holds

        private const int Fields = 10;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; an empty one when the save has none, null when its line cannot be read (it is then kept as it is).</summary>
        public static LifeState Load(Dictionary<string, string> data, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return new LifeState();
            }
            return SaveLines.Checked(data, Key, Read(text, out version), version, Version);
        }

        /// <summary>
        /// The generation of Uptown's plan a save's lots are of: its line's, or the first plan's when its line has none or the
        /// save has no line (the first plan wrote one only once a building was finished or its units opened).
        /// </summary>
        public static int PlanOf(Dictionary<string, string> data)
        {
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return 1;
            }
            LifeState read = Read(text, out int _);
            return read != null ? read.Plan : 1;
        }

        /// <summary>The line read; null when it is not a line of this store.</summary>
        public static LifeState Read(string text, out int version)
        {
            version = 0;
            if (string.IsNullOrEmpty(text) || !text.StartsWith("v=", StringComparison.Ordinal))
            {
                return null;
            }
            string[] parts = text.Split('^');
            if (!int.TryParse(parts[0].Substring(2), NumberStyles.Integer, Inv, out version) || version < 1)
            {
                return null;
            }
            var state = new LifeState { Plan = 1 };
            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
                if (part.StartsWith("g:", StringComparison.Ordinal))
                {
                    state.Plan = Whole(part.Substring(2), 1, 1, 99);
                    continue;
                }
                if (part == "o:1")
                {
                    state.Opened = true;
                    continue;
                }
                if (part.StartsWith("n:", StringComparison.Ordinal))
                {
                    foreach (string lot in part.Substring(2).Split(','))
                    {
                        if (IsId(lot))
                        {
                            state.MarkOpened(lot);
                        }
                    }
                    continue;
                }
                if (!part.StartsWith("f:", StringComparison.Ordinal))
                {
                    continue;   // a part a later version added
                }
                string body = part.Substring(2);
                if (body.Length == 0)
                {
                    continue;
                }
                foreach (string record in body.Split(';'))
                {
                    if (state.Finished.Count >= FinishedMost)
                    {
                        break;
                    }
                    FinishedBuilding f = ReadOne(record);
                    if (f != null && state.Covering(f.Lot) == null)
                    {
                        state.Finished.Add(f);
                    }
                }
            }
            return state;
        }

        private static FinishedBuilding ReadOne(string record)
        {
            string[] f = record.Split('|');
            if (f.Length < Fields || string.IsNullOrEmpty(f[0]) || string.IsNullOrEmpty(f[1]) || string.IsNullOrEmpty(f[3]))
            {
                return null;
            }
            return new FinishedBuilding
            {
                Lot = f[0],
                Size = f[1],
                Version = Whole(f[2], 1, -1, 99),
                Type = f[3],
                Design = f[4],
                Storeys = Whole(f[5], 0, 0, BuildingsRules.StoreysMost),
                Owner = f[6] == "1" ? FinishOwner.Player : FinishOwner.City,   // only the player's own mark makes a deed: anything else goes to the city
                Price = double.TryParse(f[7], NumberStyles.Float, Inv, out double price) && price >= 0 && !double.IsInfinity(price) ? price : 0,
                Day = Whole(f[8], 0, 0, int.MaxValue),
                Settled = f[9] == "1",
                Over = f.Length > Fields ? Lots(f[Fields]) : null
            };
        }

        // The other lots a building stands over: ids of letters, digits and underscores, at most a span's worth; null for none.
        private static string[] Lots(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            string[] parts = text.Split(',');
            var kept = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length && kept.Count < BuildingsRules.SpanMost - 1; i++)
            {
                if (IsId(parts[i]) && !kept.Contains(parts[i]))
                {
                    kept.Add(parts[i]);
                }
            }
            return kept.Count > 0 ? kept.ToArray() : null;
        }

        private static bool IsId(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }

        // A text without the line's separators, so a record always reads back whole.
        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('^', ' ');
        }

        private static int Whole(string text, int otherwise, int least, int most)
        {
            if (!int.TryParse(text, NumberStyles.Integer, Inv, out int v))
            {
                return otherwise;
            }
            return Math.Max(least, Math.Min(most, v));
        }

        /// <summary>The line for this state.</summary>
        public static string Write(LifeState s)
        {
            var t = new StringBuilder(16 + s.Finished.Count * 96);
            t.Append("v=").Append(Version.ToString(Inv)).Append("^f:");
            for (int i = 0; i < s.Finished.Count && i < FinishedMost; i++)
            {
                FinishedBuilding f = s.Finished[i];
                t.Append(i > 0 ? ";" : string.Empty)
                 .Append(Clean(f.Lot)).Append('|')
                 .Append(Clean(f.Size)).Append('|')
                 .Append(f.Version.ToString(Inv)).Append('|')
                 .Append(Clean(f.Type)).Append('|')
                 .Append(Clean(f.Design)).Append('|')
                 .Append(f.Storeys.ToString(Inv)).Append('|')
                 .Append(f.Owner == FinishOwner.Player ? '1' : '0').Append('|')
                 .Append(f.Price.ToString("R", Inv)).Append('|')
                 .Append(f.Day.ToString(Inv)).Append('|')
                 .Append(f.Settled ? '1' : '0');
                if (f.Over != null && f.Over.Length > 0)
                {
                    t.Append('|');
                    for (int k = 0; k < f.Over.Length; k++)
                    {
                        t.Append(k > 0 ? "," : string.Empty).Append(Clean(f.Over[k]).Replace(',', ' '));
                    }
                }
            }
            t.Append("^g:").Append(s.Plan.ToString(Inv));
            if (s.Opened)
            {
                t.Append("^o:1");
            }
            if (s.OpenedLots.Count > 0)
            {
                t.Append("^n:");
                for (int i = 0; i < s.OpenedLots.Count; i++)
                {
                    t.Append(i > 0 ? "," : string.Empty).Append(Clean(s.OpenedLots[i]).Replace(',', ' '));
                }
            }
            return t.ToString();
        }
    }
}
