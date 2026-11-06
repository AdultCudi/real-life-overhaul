using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Landmarks
{
    /// <summary>Who builds a landmark: the player's company, one of the city's rival builders (0 to 7) or the city's own works.</summary>
    public static class LandmarkBuilder
    {
        public const int Nobody = -3;   // not given out yet
        public const int City = -2;     // the city's own works, when nobody won a tender
        public const int Player = -1;   // the player's construction company
    }

    /// <summary>One landmark in one save. Game-free.</summary>
    public sealed class LandmarkFile
    {
        public LandmarkKind Kind;
        public LandmarkStage Stage;
        public int Block;                          // its site's block, settled at its announcement; 0 before
        public int Announced;                      // the day it was announced; 0 before
        public int Tender;                         // the construction game's tender for it; 0 for none
        public int Builder = LandmarkBuilder.Nobody;
        public long Price;                         // what the works cost the city (the winning bid, or the city's own estimate)
        public int Started;                        // the day the works began
        public int Due;                            // the day a rival's or the city's works hand it over; 0 for the player's
        public int SiteId;                         // the programme's site showing a rival's or the city's works; 0 for none
        public int Finished;                       // the day it was handed over
        public int Opening;                        // its opening day
        public int Phase = -1;                     // the last phase of the works the City Ledger told of
        public int PhaseDay;                       // the day it did
        public bool Joined;                        // the player joined its opening
        public bool InDistrict;                    // its buildings are the district's (from the next load)
        public string Design = string.Empty;       // the main building's design (the tower, the mall's): a DesignEntry's name
        public string Size = string.Empty;         // its structure: the game's size, version and type
        public int Version;
        public string Type = string.Empty;
        public float Width, Depth, Height;         // m: the design's footprint and height, for the plaza round it
        public int Area;                           // m2: the floor area of the city's own building the design is measured on

        public bool Built => Stage >= LandmarkStage.Finished;
    }

    /// <summary>The landmarks in one save: when the programme first ran here, and each landmark. Game-free.</summary>
    public sealed class LandmarkState
    {
        public int Anchor;    // the day the programme first ran in this save, with the district living
        public int LastDay;   // the last day whose pass has run
        public readonly LandmarkFile[] Files = new LandmarkFile[(int)LandmarkKind.Count];

        public LandmarkState()
        {
            for (int k = 0; k < Files.Length; k++)
            {
                Files[k] = new LandmarkFile { Kind = (LandmarkKind)k };
            }
        }

        public LandmarkFile Of(LandmarkKind kind) => Files[Math.Max(0, Math.Min(Files.Length - 1, (int)kind))];

        /// <summary>The landmark whose works the programme's site shows, or null.</summary>
        public LandmarkFile OnSite(int siteId)
        {
            for (int k = 0; siteId > 0 && k < Files.Length; k++)
            {
                if (Files[k].SiteId == siteId)
                {
                    return Files[k];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The landmarks' line in the save, game-free: one line under <see cref="Key"/>, "v=1^a:anchor|last^l:records", records
    /// divided by ';' and fields by '|' in the order <see cref="Write"/> joins them. A newer version adds fields at the end of
    /// a record or a part with a new letter: this reader skips what it does not know and bounds every figure. It names
    /// Uptown's blocks and the design's name, never an address of the game's; the district save keeps what names the
    /// landmarks' buildings in the game's part of the save. COST: a parse at a load, a write at a save.
    /// </summary>
    public static class LandmarkStore
    {
        public const string Key = "reallife:landmarks";
        public const string TestKey = "reallife:landmarks_before_tests";
        public const int Version = 1;

        private const int Fields = 23;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The state of this save; a new one when it has none; null when its line cannot be read (it is then kept as it is).</summary>
        public static LandmarkState Load(Dictionary<string, string> data, string key, out int version)
        {
            version = 0;
            if (data == null || !data.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
            {
                return new LandmarkState();
            }
            return SaveLines.Checked(data, key, Read(text, out version), version, Version);
        }

        /// <summary>The line read; null when it is not a line of this store.</summary>
        public static LandmarkState Read(string text, out int version)
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
            var state = new LandmarkState();
            for (int p = 1; p < parts.Length; p++)
            {
                string part = parts[p];
                if (part.StartsWith("a:", StringComparison.Ordinal))
                {
                    string[] a = part.Substring(2).Split('|');
                    state.Anchor = Whole(a, 0, 0, 0, int.MaxValue);
                    state.LastDay = Whole(a, 1, 0, 0, int.MaxValue);
                }
                else if (part.StartsWith("l:", StringComparison.Ordinal) && part.Length > 2)
                {
                    foreach (string record in part.Substring(2).Split(';'))
                    {
                        ReadOne(record, state);
                    }
                }
            }
            return state;
        }

        private static void ReadOne(string record, LandmarkState state)
        {
            string[] f = record.Split('|');
            if (f.Length < Fields || !int.TryParse(f[0], NumberStyles.Integer, Inv, out int kind) || kind < 0 || kind >= (int)LandmarkKind.Count)
            {
                return;   // a landmark this version does not know, or a damaged record: the rest still read
            }
            LandmarkFile l = state.Files[kind];
            l.Stage = (LandmarkStage)Whole(f, 1, 0, 0, (int)LandmarkStage.Opened);
            l.Block = Whole(f, 2, 0, 0, 399);
            l.Announced = Whole(f, 3, 0, 0, int.MaxValue);
            l.Tender = Whole(f, 4, 0, 0, int.MaxValue);
            l.Builder = Whole(f, 5, LandmarkBuilder.Nobody, LandmarkBuilder.Nobody, 31);
            l.Price = long.TryParse(f[6], NumberStyles.Integer, Inv, out long price) ? Math.Max(0L, price) : 0L;
            l.Started = Whole(f, 7, 0, 0, int.MaxValue);
            l.Due = Whole(f, 8, 0, 0, int.MaxValue);
            l.SiteId = Whole(f, 9, 0, 0, int.MaxValue);
            l.Finished = Whole(f, 10, 0, 0, int.MaxValue);
            l.Opening = Whole(f, 11, 0, 0, int.MaxValue);
            l.Phase = Whole(f, 12, -1, -1, 3);
            l.PhaseDay = Whole(f, 13, 0, 0, int.MaxValue);
            l.Joined = f[14] == "1";
            l.InDistrict = f[15] == "1";
            l.Design = f[16];
            l.Size = f[17];
            l.Version = Whole(f, 18, 0, 0, 99);
            l.Type = f[19];
            l.Width = Metres(f[20]);
            l.Depth = Metres(f[21]);
            l.Height = Metres(f[22]);
            l.Area = f.Length > Fields ? Whole(f, Fields, 0, 0, 1000000) : 0;
        }

        private static int Whole(string[] f, int at, int otherwise, int least, int most)
        {
            if (at >= f.Length || !int.TryParse(f[at], NumberStyles.Integer, Inv, out int v))
            {
                return otherwise;
            }
            return Math.Max(least, Math.Min(most, v));
        }

        private static float Metres(string text)
        {
            return float.TryParse(text, NumberStyles.Float, Inv, out float m) && m >= 0f && m < 1000f ? m : 0f;
        }

        // A text without the line's separators, so a record always reads back whole.
        private static string Clean(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('|', ' ').Replace(';', ' ').Replace('^', ' ');
        }

        /// <summary>The line for this state.</summary>
        public static string Write(LandmarkState s)
        {
            var t = new StringBuilder(64 + s.Files.Length * 128);
            t.Append("v=").Append(Version.ToString(Inv)).Append("^a:").Append(s.Anchor.ToString(Inv)).Append('|').Append(s.LastDay.ToString(Inv)).Append("^l:");
            for (int k = 0; k < s.Files.Length; k++)
            {
                LandmarkFile l = s.Files[k];
                t.Append(k > 0 ? ";" : string.Empty)
                 .Append(((int)l.Kind).ToString(Inv)).Append('|')
                 .Append(((int)l.Stage).ToString(Inv)).Append('|')
                 .Append(l.Block.ToString(Inv)).Append('|')
                 .Append(l.Announced.ToString(Inv)).Append('|')
                 .Append(l.Tender.ToString(Inv)).Append('|')
                 .Append(l.Builder.ToString(Inv)).Append('|')
                 .Append(l.Price.ToString(Inv)).Append('|')
                 .Append(l.Started.ToString(Inv)).Append('|')
                 .Append(l.Due.ToString(Inv)).Append('|')
                 .Append(l.SiteId.ToString(Inv)).Append('|')
                 .Append(l.Finished.ToString(Inv)).Append('|')
                 .Append(l.Opening.ToString(Inv)).Append('|')
                 .Append(l.Phase.ToString(Inv)).Append('|')
                 .Append(l.PhaseDay.ToString(Inv)).Append('|')
                 .Append(l.Joined ? '1' : '0').Append('|')
                 .Append(l.InDistrict ? '1' : '0').Append('|')
                 .Append(Clean(l.Design)).Append('|')
                 .Append(Clean(l.Size)).Append('|')
                 .Append(l.Version.ToString(Inv)).Append('|')
                 .Append(Clean(l.Type)).Append('|')
                 .Append(l.Width.ToString("0.##", Inv)).Append('|')
                 .Append(l.Depth.ToString("0.##", Inv)).Append('|')
                 .Append(l.Height.ToString("0.##", Inv)).Append('|')
                 .Append(l.Area.ToString(Inv));
            }
            return t.ToString();
        }
    }
}
