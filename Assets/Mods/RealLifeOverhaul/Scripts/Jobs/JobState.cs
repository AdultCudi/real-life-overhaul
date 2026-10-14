using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a savegame remembers about the player's jobs: experience, strikes and sackings per
    /// job, what was earned today and this week. Kept as one line of text in the save's modData
    /// (a plain string dictionary the base game carries along without reading), so a save made
    /// with jobs loads without the mod, and one from before jobs simply starts at nothing.
    /// A job in progress is not saved: loading a game always starts with no job on.
    /// Game-free: the director tells it the day.
    /// </summary>
    public static class JobState
    {
        private const string Key = "reallife:jobs";
        private const string Version = "1";

        public sealed class Record
        {
            public int Units;           // units of work ever done: the experience
            public int Shifts;          // shifts finished
            public int Strikes;         // failed shifts since the last good one
            public int SackedUntil;     // the day it takes the player back; 0 when not sacked
            public int Today;           // units done today
        }

        private static readonly Record[] Records = NewRecords();

        public static bool Loaded { get; private set; }
        public static int Day { get; private set; }
        public static int EarnedToday { get; private set; }
        public static int Week { get; private set; }
        public static int EarnedThisWeek { get; private set; }
        public static int ShiftsThisWeek { get; private set; }
        /// <summary>The last day the police pulled the player over while on a job; 0 if never.</summary>
        public static int LastPoliceStopDay { get; private set; }

        private static Record[] NewRecords()
        {
            var records = new Record[JobCatalogue.All.Length];
            for (int i = 0; i < records.Length; i++)
            {
                records[i] = new Record();
            }
            return records;
        }

        public static Record Of(JobDef def)
        {
            int index = JobCatalogue.IndexOf(def.Id);
            return index >= 0 ? Records[index] : new Record();
        }

        public static int Level(JobDef def)
        {
            return JobCatalogue.LevelFor(def, Of(def).Units);
        }

        /// <summary>A reference letter: level two in any job.</summary>
        public static bool HasReference
        {
            get
            {
                for (int i = 0; i < Records.Length; i++)
                {
                    if (JobCatalogue.LevelFor(JobCatalogue.All[i], Records[i].Units) >= JobCatalogue.ReferenceLevel)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public static bool CleanRecord(int day)
        {
            return LastPoliceStopDay <= 0 || day - LastPoliceStopDay >= JobCatalogue.CleanDays;
        }

        public static bool Sacked(JobDef def, int day)
        {
            return Of(def).SackedUntil > day;
        }

        /// <summary>How many units of this job are still to be had today.</summary>
        public static int LeftToday(JobDef def)
        {
            int left = def.DailyUnits - Of(def).Today;
            return left > 0 ? left : 0;
        }

        public static void Reset()
        {
            Loaded = false;
            Clear();
        }

        private static void Clear()
        {
            for (int i = 0; i < Records.Length; i++)
            {
                Record record = Records[i];
                record.Units = record.Shifts = record.Strikes = record.SackedUntil = record.Today = 0;
            }
            Day = EarnedToday = Week = EarnedThisWeek = ShiftsThisWeek = LastPoliceStopDay = 0;
        }

        // ---------------------------------------------------------------- what happens

        /// <summary>
        /// Moves on to a new day (today's counts start again). Returns true when a week has ended
        /// in which something was earned: the totals are then in the out values, and reset.
        /// </summary>
        public static bool NewDay(int day, out int weekEarned, out int weekShifts)
        {
            weekEarned = 0;
            weekShifts = 0;
            if (day == Day)
            {
                return false;
            }
            Day = day;
            EarnedToday = 0;
            for (int i = 0; i < Records.Length; i++)
            {
                Records[i].Today = 0;
            }
            int week = (day - 1) / 7;
            if (week == Week)
            {
                return false;
            }
            Week = week;
            weekEarned = EarnedThisWeek;
            weekShifts = ShiftsThisWeek;
            EarnedThisWeek = 0;
            ShiftsThisWeek = 0;
            return weekEarned > 0;
        }

        /// <summary>Units of work done and paid. Returns true if that reached a new level.</summary>
        public static bool Worked(JobDef def, int units, int paid)
        {
            Record record = Of(def);
            int before = JobCatalogue.LevelFor(def, record.Units);
            record.Units += units;
            record.Today += units;
            EarnedToday += paid;
            EarnedThisWeek += paid;
            return JobCatalogue.LevelFor(def, record.Units) > before;
        }

        /// <summary>
        /// A shift that was cut short by a save and a load (<see cref="JobSave"/>) and had work done on it: it counts as a
        /// shift worked, and the strikes stay as they were. It is neither a failed shift nor a good one.
        /// </summary>
        public static void ShiftCut(JobDef def)
        {
            Of(def).Shifts++;
            ShiftsThisWeek++;
        }

        /// <summary>A shift came to its end. A good one takes a strike off; a failed one adds one. Returns true if that got the player sacked.</summary>
        public static bool ShiftEnded(JobDef def, bool failed, int day)
        {
            Record record = Of(def);
            if (!failed)
            {
                record.Shifts++;
                ShiftsThisWeek++;
                if (record.Strikes > 0)
                {
                    record.Strikes--;
                }
                return false;
            }
            record.Strikes++;
            if (record.Strikes < JobCatalogue.StrikesToSack)
            {
                return false;
            }
            record.Strikes = 0;
            record.SackedUntil = day + JobCatalogue.SackedDays;
            return true;
        }

        public static void PoliceStop(int day)
        {
            LastPoliceStopDay = day;
        }

        // ---------------------------------------------------------------- the save

        public static void Load(Dictionary<string, string> data, int day)
        {
            Clear();
            Loaded = true;
            if (data != null && data.TryGetValue(Key, out string text) && !string.IsNullOrEmpty(text))
            {
                try
                {
                    Parse(text);
                }
                catch (System.Exception)
                {
                    Clear();   // a damaged line costs this save's jobs, never the load
                    SaveLines.Unreadable(data, Key, "it could not be read");   // (and the line is kept as it is: Core/SaveLines)
                }
            }
            if (Day == 0)
            {
                // A save from before jobs: the week starts counting from now.
                Day = day;
                Week = (day - 1) / 7;
            }
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            var text = new StringBuilder(96);
            text.Append(Version).Append('|');
            Join(text, Day, EarnedToday, Week, EarnedThisWeek, ShiftsThisWeek, LastPoliceStopDay);
            for (int i = 0; i < Records.Length; i++)
            {
                Record record = Records[i];
                text.Append('|').Append(JobCatalogue.All[i].Id).Append(':');
                Join(text, record.Units, record.Shifts, record.Strikes, record.SackedUntil, record.Today, 0);
            }
            data[Key] = text.ToString();
        }

        private static void Join(StringBuilder text, int a, int b, int c, int d, int e, int f)
        {
            text.Append(a.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(b.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(c.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(d.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(e.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(f.ToString(CultureInfo.InvariantCulture));
        }

        // Anything that does not read as expected is left at nothing: a damaged line costs the history, never the load.
        private static void Parse(string text)
        {
            string[] parts = text.Split('|');
            if (parts.Length < 2 || parts[0] != Version)
            {
                return;
            }
            int[] head = Numbers(parts[1]);
            if (head == null)
            {
                return;
            }
            Day = head[0];
            EarnedToday = head[1];
            Week = head[2];
            EarnedThisWeek = head[3];
            ShiftsThisWeek = head[4];
            LastPoliceStopDay = head[5];
            for (int i = 2; i < parts.Length; i++)
            {
                int colon = parts[i].IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                int index = JobCatalogue.IndexOf(parts[i].Substring(0, colon));   // a job this build does not have is dropped
                int[] values = Numbers(parts[i].Substring(colon + 1));
                if (index < 0 || values == null)
                {
                    continue;
                }
                Record record = Records[index];
                record.Units = values[0];
                record.Shifts = values[1];
                record.Strikes = values[2];
                record.SackedUntil = values[3];
                record.Today = values[4];
            }
        }

        private static int[] Numbers(string text)
        {
            string[] fields = text.Split(',');
            if (fields.Length < 6)
            {
                return null;
            }
            var values = new int[6];
            for (int i = 0; i < 6; i++)
            {
                if (!int.TryParse(fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]) || values[i] < 0)
                {
                    return null;
                }
            }
            return values;
        }
    }
}
