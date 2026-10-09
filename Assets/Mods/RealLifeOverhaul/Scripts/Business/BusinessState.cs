using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>An issue that is open: which, where, how far along, and when its deadline is.</summary>
    public sealed class BusinessIssue
    {
        public string Kind;
        public string Place;      // the business: BusinessState.PlaceKey of its address
        public int Stage;
        public int StartDay;
        public int DueDay;
        public int DueHour;
        public int A;             // what the kind needs to remember: an amount, a percent...
        public int B;
        public int C;
        public string Ref;        // ...or somebody's id

        public IssueDef Def => BusinessCatalogue.Find(Kind);
    }

    /// <summary>A change to a business's footfall that is running: percent of customers, until a day.</summary>
    public struct FootfallEffect
    {
        public string Place;
        public int Percent;
        public int UntilDay;      // the first day without it
    }

    /// <summary>A business the mod closed and will reopen.</summary>
    public struct Closure
    {
        public string Place;
        public int Day;
        public int Hour;
    }

    /// <summary>Where the protection racket stands.</summary>
    public enum RacketStage
    {
        None = 0,
        Demand = 1,        // the collector has asked: an open issue
        Paying = 2,        // paid by the week
        Refused = 3,       // refused: what they do about it comes on RacketDay
        Reported = 4,      // the police are looking into it until RacketDay
        Sting = 5,         // the police will be waiting on RacketDay at RacketHour
        Ended = 6,         // arrested: over for good
        Securing = 7       // the player said they would see to security: looked at on RacketDay
    }

    /// <summary>
    /// What a savegame remembers of business pressure: the open issues, the footfall changes and
    /// closures that are running (so each can be undone on its day), the racket, and when each
    /// kind and each business last had something. One line of text in the save's modData; the
    /// base game carries it without reading it. Everything it describes is either the game's own
    /// field with the game's own meaning (a promotion figure the game recomputes daily, the
    /// "temporarily closed" switch) or nothing at all, so a save loads and plays without the mod.
    /// Game-free.
    /// </summary>
    public static class BusinessState
    {
        private const string Key = "reallife:business";
        private const string Version = "1";

        public static bool Loaded { get; private set; }
        public static int FirstDay { get; private set; }       // the day the mod first saw this save
        public static int LastRollDay { get; set; }

        public static RacketStage Racket { get; set; }
        public static string RacketPlace { get; set; }
        public static int RacketWeekly { get; set; }           // what is paid a week now
        public static int RacketFirst { get; set; }            // what was first asked: the creep is measured against it
        public static int RacketDay { get; set; }              // the day of whatever comes next
        public static int RacketHour { get; set; }
        public static int RacketRestUntil { get; set; }        // no new demand before this day

        public static readonly List<BusinessIssue> Issues = new List<BusinessIssue>(BusinessCatalogue.MostOpen + 1);
        public static readonly List<FootfallEffect> Effects = new List<FootfallEffect>(4);
        public static readonly List<Closure> Closures = new List<Closure>(2);
        private static readonly int[] LastOfKind = new int[BusinessCatalogue.All.Length];
        private static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();        // place -> the day first seen
        private static readonly Dictionary<string, int> Rested = new Dictionary<string, int>();      // place -> the day its last issue ended

        /// <summary>A business by its address, as one string that survives the save.</summary>
        public static string PlaceKey(string street, int number)
        {
            return Uri.EscapeDataString(street ?? string.Empty) + "~" + number.ToString(CultureInfo.InvariantCulture);
        }

        public static bool SplitPlace(string place, out string street, out int number)
        {
            street = null;
            number = 0;
            int at = place != null ? place.LastIndexOf('~') : -1;
            if (at <= 0 || !int.TryParse(place.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return false;
            }
            street = Uri.UnescapeDataString(place.Substring(0, at));
            return true;
        }

        public static void Reset()
        {
            Loaded = false;
            Clear();
        }

        private static void Clear()
        {
            FirstDay = LastRollDay = 0;
            Racket = RacketStage.None;
            RacketPlace = null;
            RacketWeekly = RacketFirst = RacketDay = RacketHour = RacketRestUntil = 0;
            Issues.Clear();
            Effects.Clear();
            Closures.Clear();
            Seen.Clear();
            Rested.Clear();
            for (int i = 0; i < LastOfKind.Length; i++)
            {
                LastOfKind[i] = 0;
            }
        }

        // ---------------------------------------------------------------- questions

        public static BusinessIssue IssueAt(string place)
        {
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Place == place)
                {
                    return Issues[i];
                }
            }
            return null;
        }

        public static BusinessIssue IssueOf(string kind)
        {
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Kind == kind)
                {
                    return Issues[i];
                }
            }
            return null;
        }

        /// <summary>Notes a business the first time it is seen. Returns the day it was first seen.</summary>
        public static int See(string place, int day)
        {
            if (!Seen.TryGetValue(place, out int first))
            {
                first = day;
                Seen[place] = day;
            }
            return first;
        }

        /// <summary>True if a business is old enough, has nothing open and has been left alone long enough.</summary>
        public static bool Eligible(string place, int day)
        {
            if (IssueAt(place) != null || !Seen.TryGetValue(place, out int first) || day - first < BusinessCatalogue.NewBusinessDays)
            {
                return false;
            }
            return !Rested.TryGetValue(place, out int ended) || day - ended >= BusinessCatalogue.BusinessRestDays;
        }

        /// <summary>True if a kind has not come up for its rest days (and is not already open).</summary>
        public static bool KindRested(IssueDef def, int day)
        {
            int index = BusinessCatalogue.IndexOf(def.Id);
            return index >= 0 && IssueOf(def.Id) == null && (LastOfKind[index] <= 0 || day - LastOfKind[index] >= def.RestDays);
        }

        public static bool PastGrace(int day)
        {
            return FirstDay > 0 && day - FirstDay >= BusinessCatalogue.NewSaveDays;
        }

        public static int EffectAt(string place)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].Place == place)
                {
                    return i;
                }
            }
            return -1;
        }

        // ---------------------------------------------------------------- changes

        public static BusinessIssue Open(string kind, string place, int day, int dueDay, int dueHour)
        {
            var issue = new BusinessIssue { Kind = kind, Place = place, StartDay = day, DueDay = dueDay, DueHour = dueHour };
            Issues.Add(issue);
            int index = BusinessCatalogue.IndexOf(kind);
            if (index >= 0)
            {
                LastOfKind[index] = day;
            }
            return issue;
        }

        public static void Close(BusinessIssue issue, int day)
        {
            if (Issues.Remove(issue) && issue.Place != null)
            {
                Rested[issue.Place] = day;
            }
        }

        /// <summary>Sets the one footfall change a business has (replacing any other). Zero percent removes it.</summary>
        public static void SetEffect(string place, int percent, int untilDay)
        {
            int at = EffectAt(place);
            if (at >= 0)
            {
                Effects.RemoveAt(at);
            }
            percent = BusinessCatalogue.ClampFootfall(percent);
            if (percent != 0)
            {
                Effects.Add(new FootfallEffect { Place = place, Percent = percent, UntilDay = untilDay });
            }
        }

        public static void SetClosure(string place, int day, int hour)
        {
            for (int i = Closures.Count - 1; i >= 0; i--)
            {
                if (Closures[i].Place == place)
                {
                    Closures.RemoveAt(i);
                }
            }
            Closures.Add(new Closure { Place = place, Day = day, Hour = hour });
        }

        /// <summary>Forgets businesses that are no longer the player's. <paramref name="owned"/> says which still are.</summary>
        public static void Prune(Predicate<string> owned)
        {
            var gone = new List<string>();
            foreach (string place in Seen.Keys)
            {
                if (!owned(place))
                {
                    gone.Add(place);
                }
            }
            for (int i = 0; i < gone.Count; i++)
            {
                Seen.Remove(gone[i]);
                Rested.Remove(gone[i]);
            }
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
                catch (Exception)
                {
                    Clear();   // a damaged line costs the open issues, never the load
                }
            }
            if (Racket == RacketStage.Demand && IssueOf(BusinessCatalogue.Racket) == null)
            {
                Racket = RacketStage.None;   // a demand with no issue behind it could never be answered
            }
            if (FirstDay <= 0)
            {
                FirstDay = day > 0 ? day : 1;   // a save from before business pressure: the grace period starts now
            }
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (!Loaded || data == null)
            {
                return;
            }
            var text = new StringBuilder(256);
            text.Append(Version).Append('|');
            Join(text, FirstDay, LastRollDay, (int)Racket, RacketWeekly, RacketFirst, RacketDay, RacketHour, RacketRestUntil);
            text.Append('|').Append(RacketPlace ?? string.Empty).Append('|');
            for (int i = 0; i < Issues.Count; i++)
            {
                BusinessIssue issue = Issues[i];
                text.Append(issue.Kind).Append(',').Append(issue.Place).Append(',');
                Join(text, issue.Stage, issue.StartDay, issue.DueDay, issue.DueHour, issue.A, issue.B, issue.C);
                text.Append(',').Append(Uri.EscapeDataString(issue.Ref ?? string.Empty)).Append(';');
            }
            text.Append('|');
            for (int i = 0; i < Effects.Count; i++)
            {
                text.Append(Effects[i].Place).Append(',').Append(Effects[i].Percent.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(Effects[i].UntilDay.ToString(CultureInfo.InvariantCulture)).Append(';');
            }
            text.Append('|');
            for (int i = 0; i < Closures.Count; i++)
            {
                text.Append(Closures[i].Place).Append(',');
                Join(text, Closures[i].Day, Closures[i].Hour);
                text.Append(';');
            }
            text.Append('|');
            for (int i = 0; i < LastOfKind.Length; i++)
            {
                if (LastOfKind[i] > 0)
                {
                    text.Append(BusinessCatalogue.All[i].Id).Append('=').Append(LastOfKind[i].ToString(CultureInfo.InvariantCulture)).Append(';');
                }
            }
            text.Append('|');
            foreach (KeyValuePair<string, int> seen in Seen)
            {
                Rested.TryGetValue(seen.Key, out int rested);
                text.Append(seen.Key).Append(',');
                Join(text, seen.Value, rested);
                text.Append(';');
            }
            data[Key] = text.ToString();
        }

        private static void Join(StringBuilder text, params int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(',');
                }
                text.Append(values[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void Parse(string text)
        {
            string[] parts = text.Split('|');
            if (parts.Length < 8 || parts[0] != Version)
            {
                return;
            }
            string[] head = parts[1].Split(',');
            if (head.Length < 8)
            {
                return;
            }
            FirstDay = Int(head[0]);
            LastRollDay = Int(head[1]);
            int stage = Int(head[2]);
            Racket = stage >= 0 && stage <= (int)RacketStage.Securing ? (RacketStage)stage : RacketStage.None;
            RacketWeekly = Math.Max(0, Int(head[3]));
            RacketFirst = Math.Max(0, Int(head[4]));
            RacketDay = Int(head[5]);
            RacketHour = Int(head[6]);
            RacketRestUntil = Int(head[7]);
            RacketPlace = parts[2].Length > 0 ? parts[2] : null;

            foreach (string entry in parts[3].Split(';'))
            {
                string[] f = entry.Split(',');
                if (f.Length < 10 || BusinessCatalogue.Find(f[0]) == null || Issues.Count >= BusinessCatalogue.MostOpen + 1)
                {
                    continue;   // a kind this build does not have is dropped
                }
                Issues.Add(new BusinessIssue
                {
                    Kind = f[0], Place = f[1], Stage = Int(f[2]), StartDay = Int(f[3]), DueDay = Int(f[4]), DueHour = Int(f[5]),
                    A = Int(f[6]), B = Int(f[7]), C = Int(f[8]), Ref = f[9].Length > 0 ? Uri.UnescapeDataString(f[9]) : null
                });
            }
            foreach (string entry in parts[4].Split(';'))
            {
                string[] f = entry.Split(',');
                if (f.Length >= 3 && Effects.Count < 8)
                {
                    Effects.Add(new FootfallEffect { Place = f[0], Percent = BusinessCatalogue.ClampFootfall(Int(f[1])), UntilDay = Int(f[2]) });
                }
            }
            foreach (string entry in parts[5].Split(';'))
            {
                string[] f = entry.Split(',');
                if (f.Length >= 3 && Closures.Count < 8)
                {
                    Closures.Add(new Closure { Place = f[0], Day = Int(f[1]), Hour = Int(f[2]) });
                }
            }
            foreach (string entry in parts[6].Split(';'))
            {
                int equals = entry.IndexOf('=');
                int index = equals > 0 ? BusinessCatalogue.IndexOf(entry.Substring(0, equals)) : -1;
                if (index >= 0)
                {
                    LastOfKind[index] = Int(entry.Substring(equals + 1));
                }
            }
            foreach (string entry in parts[7].Split(';'))
            {
                string[] f = entry.Split(',');
                if (f.Length >= 3 && f[0].Length > 0)
                {
                    Seen[f[0]] = Int(f[1]);
                    int rested = Int(f[2]);
                    if (rested > 0)
                    {
                        Rested[f[0]] = rested;
                    }
                }
            }
        }

        private static int Int(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
