using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>A design the player finished: whose project it was for, when, and how it scored.</summary>
    public sealed class DesignEntry
    {
        public string Id;          // the construction company's project ("build:12"), or the office's own ("own:3")
        public int Day;
        public DesignScore Score;
    }

    /// <summary>A brief a construction company has put at the architects' office for the player (Scripts/Construction).</summary>
    public sealed class Commission
    {
        public string Id;
        public int Storeys, Units, Budget;
    }

    /// <summary>
    /// The building trades' one line in the save (<see cref="Key"/>), game-free: the player's last designs (the construction
    /// company reads them by its project's id), the briefs it has put at the office, the office's own count, and a bribe taken
    /// that may yet come out. A line that does not read is kept as it was read (Core/SaveLines, by the director).
    /// </summary>
    public static class DesignBook
    {
        public const string Key = "reallife:trades";
        public const int Version = 1;
        public const int DesignsMost = 8;
        public const int CommissionsMost = 6;
        public const int IdMost = 40;           // characters of a project's id kept
        public const int BribeComesOutDays = 3; // days after a bribe at which it comes out, or is forgotten

        private static readonly List<DesignEntry> Designs = new List<DesignEntry>(DesignsMost);
        private static readonly List<Commission> Briefs = new List<Commission>(CommissionsMost);

        public static bool Loaded { get; private set; }
        public static int OwnCount { get; private set; }        // the office's own briefs drawn so far: each a seed of its own
        public static int BribeDay { get; private set; } = -1;  // the day a bribe was taken; -1 none
        public static int BribeSite { get; private set; }
        public static int BribeAmount { get; private set; }

        public static int Count => Designs.Count;
        public static int Waiting => Briefs.Count;

        public static void Reset()
        {
            Loaded = false;
            Clear();
        }

        private static void Clear()
        {
            Designs.Clear();
            Briefs.Clear();
            OwnCount = 0;
            BribeDay = -1;
            BribeSite = 0;
            BribeAmount = 0;
        }

        /// <summary>The newest design; false with none.</summary>
        public static bool Latest(out DesignScore score)
        {
            score = default;
            if (Designs.Count == 0)
            {
                return false;
            }
            score = Designs[Designs.Count - 1].Score;
            return true;
        }

        /// <summary>The design finished for a project; false until the player has finished it.</summary>
        public static bool For(string projectId, out DesignScore score)
        {
            score = default;
            for (int i = Designs.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Designs[i].Id, projectId, StringComparison.Ordinal))
                {
                    score = Designs[i].Score;
                    return true;
                }
            }
            return false;
        }

        /// <summary>A construction company puts a brief at the office for one of its projects; asked again for the same project, the brief is replaced.</summary>
        public static void Commission(string projectId, int storeys, int units, int budget)
        {
            string id = Clean(projectId);
            if (id.Length == 0)
            {
                return;
            }
            for (int i = 0; i < Briefs.Count; i++)
            {
                if (Briefs[i].Id == id)
                {
                    Briefs[i].Storeys = storeys;
                    Briefs[i].Units = units;
                    Briefs[i].Budget = budget;
                    return;
                }
            }
            if (Briefs.Count >= CommissionsMost)
            {
                Briefs.RemoveAt(0);
            }
            Briefs.Add(new Commission { Id = id, Storeys = storeys, Units = units, Budget = budget });
        }

        /// <summary>The oldest brief waiting, or null.</summary>
        public static Commission Next => Briefs.Count > 0 ? Briefs[0] : null;

        /// <summary>The office's next brief of its own: its number, for the seed.</summary>
        public static int TakeOwn()
        {
            return ++OwnCount;
        }

        /// <summary>A design is finished: kept (the oldest goes past <see cref="DesignsMost"/>), and its project's brief taken off.</summary>
        public static void Finished(string id, int day, in DesignScore score)
        {
            id = Clean(id);
            for (int i = Briefs.Count - 1; i >= 0; i--)
            {
                if (Briefs[i].Id == id)
                {
                    Briefs.RemoveAt(i);
                }
            }
            for (int i = Designs.Count - 1; i >= 0; i--)
            {
                if (Designs[i].Id == id)
                {
                    Designs.RemoveAt(i);   // a project designed again keeps only its latest
                }
            }
            if (Designs.Count >= DesignsMost)
            {
                Designs.RemoveAt(0);
            }
            Designs.Add(new DesignEntry { Id = id, Day = day, Score = score });
        }

        /// <summary>A bribe was taken at an inspection: it may come out in the next days.</summary>
        public static void Bribed(int day, int site, int amount)
        {
            BribeDay = day;
            BribeSite = site;
            BribeAmount = amount;
        }

        /// <summary>The bribe is settled: come out, or forgotten.</summary>
        public static void BribeDone()
        {
            BribeDay = -1;
            BribeSite = 0;
            BribeAmount = 0;
        }

        /// <summary>True on the day a bribe taken is settled.</summary>
        public static bool BribeDue(int day) => BribeDay >= 0 && day >= BribeDay + BribeComesOutDays;

        private static string Clean(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }
            var text = new StringBuilder(Math.Min(id.Length, IdMost));
            for (int i = 0; i < id.Length && text.Length < IdMost; i++)
            {
                char c = id[i];
                text.Append(c == ',' || c == '~' || c == '|' || c < ' ' ? '_' : c);
            }
            return text.ToString();
        }

        // ---- the line ----

        public static string Write()
        {
            var text = new StringBuilder(256);
            text.Append(Version.ToString(CultureInfo.InvariantCulture)).Append('~')
                .Append("h,").Append(OwnCount.ToString(CultureInfo.InvariantCulture)).Append(',').Append(BribeDay.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(BribeSite.ToString(CultureInfo.InvariantCulture)).Append(',').Append(BribeAmount.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Designs.Count; i++)
            {
                text.Append("~d,").Append(Designs[i].Id).Append(',').Append(Designs[i].Day.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Designs[i].Score.Write());
            }
            for (int i = 0; i < Briefs.Count; i++)
            {
                text.Append("~c,").Append(Briefs[i].Id).Append(',').Append(Briefs[i].Storeys.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Briefs[i].Units.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Briefs[i].Budget.ToString(CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        /// <summary>Reads a line written by <see cref="Write"/>; nothing for none. False (and the book empty) for one that does not read.</summary>
        public static bool Read(string line)
        {
            Clear();
            Loaded = true;
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }
            string[] records = line.Split('~');
            if (records.Length < 2 || records[0] != Version.ToString(CultureInfo.InvariantCulture))
            {
                return false;
            }
            for (int r = 1; r < records.Length; r++)
            {
                string[] f = records[r].Split(',');
                if (f.Length == 5 && f[0] == "h" && Int(f[1], out int own) && Int(f[2], out int day) && Int(f[3], out int site) && Int(f[4], out int amount))
                {
                    OwnCount = Math.Max(0, own);
                    BribeDay = day < 0 ? -1 : day;
                    BribeSite = site;
                    BribeAmount = Math.Max(0, amount);
                }
                else if (f.Length == 4 && f[0] == "d" && Int(f[2], out int when) && DesignScore.Read(f[3], out DesignScore score) && Designs.Count < DesignsMost)
                {
                    Designs.Add(new DesignEntry { Id = Clean(f[1]), Day = when, Score = score });
                }
                else if (f.Length == 5 && f[0] == "c" && Int(f[2], out int storeys) && Int(f[3], out int units) && Int(f[4], out int budget) && Briefs.Count < CommissionsMost)
                {
                    Briefs.Add(new Commission { Id = Clean(f[1]), Storeys = storeys, Units = units, Budget = budget });
                }
                else
                {
                    Clear();
                    Loaded = true;
                    return false;
                }
            }
            return true;
        }

        private static bool Int(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
