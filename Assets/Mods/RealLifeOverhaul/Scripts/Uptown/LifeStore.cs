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
    }

    /// <summary>What the district keeps in the save: the buildings the construction game finished.</summary>
    public sealed class LifeState
    {
        public readonly List<FinishedBuilding> Finished = new List<FinishedBuilding>(8);

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
    }

    /// <summary>
    /// The district's line in the save, game-free: one line under <see cref="Key"/>, "v=1^f:finished", records divided by ';'
    /// and fields by '|' (lot, size, version, type, design, storeys, owner, price, day, settled), in the order
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
            var state = new LifeState();
            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
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
                    if (f != null && state.On(f.Lot) == null)
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
                Settled = f[9] == "1"
            };
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
            }
            return t.ToString();
        }
    }
}
