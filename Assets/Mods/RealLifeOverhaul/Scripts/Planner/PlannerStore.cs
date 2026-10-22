using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Planner
{
    // Game-free: PlannerCheck compiles this file as it is.

    /// <summary>When a reminder goes out, counted from the entry it is for. Saved as its number.</summary>
    public enum ReminderLead
    {
        None = 0,
        AtTheTime = 1,
        HourBefore = 2,
        EveningBefore = 3,
        QuarterBefore = 4,
        HalfHourBefore = 5,
        LeaveBy = 6          // its hour less the way there (PlannerReminder.Travel)
    }

    /// <summary>One of the player's own entries.</summary>
    public sealed class PlannerNote
    {
        public int Id;
        public string Title = string.Empty;
        public int Day;              // the first day it falls on
        public int Start = -1;       // minutes since midnight; -1: all day
        public bool Done;            // for one that is once
        public bool Sample;          // made by a test button, removed by the other
        public string Place;         // what the place is called, or null
        public bool HasPlace;
        public float X, Y, Z;
        public string Street;
        public int Number;
        public PlannerRepeat Repeat;
        public int DoneDay = -1;     // for one that repeats: the day whose time is done
        public ReminderLead Lead;    // for one that repeats: the reminder each time; one that is once keeps it in Reminders
    }

    /// <summary>A reminder that has been asked for and has not gone out yet.</summary>
    public struct PlannerReminder
    {
        public EntryId Entry;
        public int EntryBegins;      // game minutes: an entry that has moved since is reminded of at its new time
        public int FireAt;           // game minutes
        public ReminderLead Lead;
        public int Travel;           // game minutes of way for LeaveBy, as it was when set
    }

    /// <summary>The Planner's three save lines: a damaged record is skipped, a line nothing can be read of is kept (SaveLines).</summary>
    public static class PlannerStore
    {
        public const string Key = "reallife:planner";
        public const string NotesKey = "reallife:planner_notes";
        public const string RemindersKey = "reallife:planner_reminders";

        public const int MostNotes = 200;
        public const int MostReminders = 100;
        public const int TitleLength = 60;       // characters
        public const int EveningHour = 18;       // "the evening before" is 18:00 on the day before
        public const int AllDayHour = 8;         // an entry with no hour begins at 08:00 for its reminders
        public const int LateMinutes = 30;       // game minutes after its moment (a time skip, a loaded save): a reminder says it is late
        public const int ArmDays = 15;           // days looked ahead for a repeated entry's next time

        public static readonly List<PlannerNote> Notes = new List<PlannerNote>(16);
        public static readonly List<PlannerReminder> Reminders = new List<PlannerReminder>(16);

        public static bool Loaded { get; private set; }
        public static int NextNoteId = 1;
        public static int HiddenCategories;                       // one bit per PlannerCategory
        public static ReminderLead DefaultLead = ReminderLead.HourBefore;
        public static bool Welcomed;                              // the first-run notification has been shown
        public static int SeenStamp;                              // PlannerLogic.DueStamp when the app was last opened
        public static int MorningDay;                             // the day the morning line was last sent; 0 none
        /// <summary>The soonest reminder's moment in game minutes, int.MaxValue with none: the director's one comparison a second.</summary>
        public static int NextFire { get; private set; } = int.MaxValue;

        public static void Reset()
        {
            Loaded = false;
            Notes.Clear();
            Reminders.Clear();
            NextFire = int.MaxValue;
            NextNoteId = 1;
            HiddenCategories = 0;
            DefaultLead = ReminderLead.HourBefore;
            Welcomed = false;
            SeenStamp = 0;
            MorningDay = 0;
        }

        public static bool Shows(PlannerCategory category)
        {
            return (HiddenCategories & (1 << (int)category)) == 0;
        }

        public static void Show(PlannerCategory category, bool show)
        {
            int bit = 1 << (int)category;
            HiddenCategories = show ? HiddenCategories & ~bit : HiddenCategories | bit;
        }

        public static PlannerNote AddNote(string title, int day, int start)
        {
            if (Notes.Count >= MostNotes)
            {
                return null;
            }
            var note = new PlannerNote { Id = NextNoteId++, Title = Clean(title), Day = day, Start = start };
            Notes.Add(note);
            return note;
        }

        public static PlannerNote Note(int id)
        {
            for (int i = 0; i < Notes.Count; i++)
            {
                if (Notes[i].Id == id)
                {
                    return Notes[i];
                }
            }
            return null;
        }

        public static bool RemoveNote(int id)
        {
            for (int i = 0; i < Notes.Count; i++)
            {
                if (Notes[i].Id == id)
                {
                    Notes.RemoveAt(i);
                    DropReminders(NoteSourceId, id);
                    return true;
                }
            }
            return false;
        }

        public const string NoteSourceId = "note";

        /// <summary>One time of a note: (note, number, 0) for one that is once, (note, number, day) for one that repeats.</summary>
        public static EntryId NoteEntry(PlannerNote note, int day)
        {
            return new EntryId(NoteSourceId, note.Id, note.Repeat == PlannerRepeat.Once ? 0 : day);
        }

        /// <summary>The first day on or after 'fromDay' a note falls on; -1 if none.</summary>
        public static int NextTime(PlannerNote note, int fromDay)
        {
            return PlannerRules.NextOccurrence(note.Repeat, note.Day, fromDay);
        }

        /// <summary>Whether a note falls on a day, and whether that time of it is done.</summary>
        public static bool Falls(PlannerNote note, int day, out bool done)
        {
            done = note.Repeat == PlannerRepeat.Once ? note.Done : note.DoneDay == day;
            return PlannerRules.Occurs(note.Repeat, note.Day, day);
        }

        /// <summary>Marks one time of a note done or not; done takes its reminder away.</summary>
        public static void SetDone(PlannerNote note, int day, bool done)
        {
            if (note.Repeat == PlannerRepeat.Once)
            {
                note.Done = done;
            }
            else
            {
                note.DoneDay = done ? day : (note.DoneDay == day ? -1 : note.DoneDay);
            }
            if (done)
            {
                SetReminder(NoteEntry(note, day), 0, 0, ReminderLead.None, 0);
            }
        }

        /// <summary>Sets a repeating note's one reminder for its next time after now; a note that is once is left alone.</summary>
        public static void Arm(PlannerNote note, int nowMinutes)
        {
            if (note == null || note.Repeat == PlannerRepeat.Once)
            {
                return;
            }
            DropReminders(NoteSourceId, note.Id, true);
            if (note.Lead == ReminderLead.None || note.Lead == ReminderLead.LeaveBy)
            {
                return;   // when to leave depends on where the player is: set for one time in its box
            }
            int day = PlannerRules.NextOccurrence(note.Repeat, note.Day, PlannerTime.DayOf(nowMinutes) - 1);
            for (int i = 0; day >= 0 && i < ArmDays; i++)
            {
                if (Falls(note, day, out bool done) && !done && FireAt(day, note.Start, note.Lead) > nowMinutes)
                {
                    SetReminder(NoteEntry(note, day), day, note.Start, note.Lead, nowMinutes);
                    return;
                }
                day = PlannerRules.NextOccurrence(note.Repeat, note.Day, day + 1);
            }
        }

        public static void ArmAll(int nowMinutes)
        {
            for (int i = 0; i < Notes.Count; i++)
            {
                Arm(Notes[i], nowMinutes);
            }
        }

        /// <summary>A title as it is kept: one line of at most TitleLength characters, never null.</summary>
        public static string Clean(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }
            var text = new StringBuilder(title.Length);
            for (int i = 0; i < title.Length && text.Length < TitleLength; i++)
            {
                char c = title[i];
                // no line breaks, and no angle brackets a text field would read as mark-up
                text.Append(c < ' ' || c == '<' || c == '>' ? ' ' : c);
            }
            return text.ToString().Trim();
        }

        /// <summary>When a reminder goes out, in game minutes; 'travel' is the way there for LeaveBy.</summary>
        public static int FireAt(int entryDay, int entryStart, ReminderLead lead, int travel = 0)
        {
            int begins = PlannerTime.At(entryDay, 0) + (entryStart >= 0 ? entryStart : AllDayHour * 60);
            switch (lead)
            {
                case ReminderLead.HourBefore:
                    return begins - 60;
                case ReminderLead.HalfHourBefore:
                    return begins - 30;
                case ReminderLead.QuarterBefore:
                    return begins - 15;
                case ReminderLead.LeaveBy:
                    return begins - Math.Max(0, travel);
                case ReminderLead.EveningBefore:
                    return PlannerTime.At(entryDay - 1, EveningHour);
                default:
                    return begins;
            }
        }

        public static int ReminderIndex(EntryId entry)
        {
            for (int i = 0; i < Reminders.Count; i++)
            {
                if (Reminders[i].Entry.Equals(entry))
                {
                    return i;
                }
            }
            return -1;
        }

        public static ReminderLead LeadOf(EntryId entry)
        {
            int index = ReminderIndex(entry);
            return index >= 0 ? Reminders[index].Lead : ReminderLead.None;
        }

        /// <summary>
        /// Sets, changes or (with None) takes away an entry's one reminder; 'travel' is the way there for LeaveBy. False,
        /// and nothing kept, if the moment has passed or there is no room.
        /// </summary>
        public static bool SetReminder(EntryId entry, int entryDay, int entryStart, ReminderLead lead, int nowMinutes, int travel = 0)
        {
            int index = ReminderIndex(entry);
            if (index >= 0)
            {
                Reminders.RemoveAt(index);
                Soonest();
            }
            if (lead == ReminderLead.None)
            {
                return true;
            }
            int fireAt = FireAt(entryDay, entryStart, lead, travel);
            if (fireAt < nowMinutes || Reminders.Count >= MostReminders || string.IsNullOrEmpty(entry.Source))
            {
                return false;
            }
            int begins = PlannerTime.At(entryDay, 0) + (entryStart >= 0 ? entryStart : 0);
            Reminders.Add(new PlannerReminder { Entry = entry, EntryBegins = begins, FireAt = fireAt, Lead = lead, Travel = lead == ReminderLead.LeaveBy ? Math.Max(0, travel) : 0 });
            NextFire = Math.Min(NextFire, fireAt);
            return true;
        }

        /// <summary>Takes away every reminder of (source, a), whatever its third number; with 'keepLeaveBy' a LeaveBy stays.</summary>
        public static void DropReminders(string source, int a, bool keepLeaveBy = false)
        {
            for (int i = Reminders.Count - 1; i >= 0; i--)
            {
                EntryId id = Reminders[i].Entry;
                if (id.A == a && string.Equals(id.Source, source, StringComparison.Ordinal) && !(keepLeaveBy && Reminders[i].Lead == ReminderLead.LeaveBy))
                {
                    Reminders.RemoveAt(i);
                }
            }
            Soonest();
        }

        /// <summary>Takes out every reminder whose moment has come, oldest first, so each goes out once.</summary>
        public static void TakeDue(int nowMinutes, List<PlannerReminder> due)
        {
            due.Clear();
            for (int i = Reminders.Count - 1; i >= 0; i--)
            {
                if (Reminders[i].FireAt <= nowMinutes)
                {
                    due.Add(Reminders[i]);
                    Reminders.RemoveAt(i);   // before anything is shown
                }
            }
            if (due.Count > 1)
            {
                due.Sort(ByFireAt);
            }
            Soonest();
        }

        private static readonly Comparison<PlannerReminder> ByFireAt = (a, b) => a.FireAt != b.FireAt ? a.FireAt.CompareTo(b.FireAt) : a.Entry.A.CompareTo(b.Entry.A);

        private static void Soonest()
        {
            int soonest = int.MaxValue;
            for (int i = 0; i < Reminders.Count; i++)
            {
                soonest = Math.Min(soonest, Reminders[i].FireAt);
            }
            NextFire = soonest;
        }

        public static bool IsLate(PlannerReminder reminder, int nowMinutes)
        {
            return nowMinutes - reminder.FireAt > LateMinutes;
        }

        public static void Load(Dictionary<string, string> data)
        {
            Reset();
            Loaded = true;
            if (data == null)
            {
                return;
            }
            if (data.TryGetValue(Key, out string text) && !string.IsNullOrEmpty(text))
            {
                int read = 0;
                foreach (string part in text.Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    if (!Int(part.Substring(eq + 1), out int value))
                    {
                        continue;
                    }
                    read++;
                    switch (part.Substring(0, eq))
                    {
                        case "next": NextNoteId = Math.Max(1, value); break;
                        case "hide": HiddenCategories = value & ((1 << (int)PlannerCategory.Count) - 1); break;
                        case "lead": DefaultLead = Lead(value, ReminderLead.HourBefore); break;
                        case "hello": Welcomed = value != 0; break;
                        case "seen": SeenStamp = value; break;
                        case "morning": MorningDay = value; break;
                        default: read--; break;
                    }
                }
                if (read == 0)
                {
                    SaveLines.Unreadable(data, Key, "none of its settings could be read");
                }
            }
            if (data.TryGetValue(NotesKey, out text) && !string.IsNullOrEmpty(text))
            {
                int read = 0;
                foreach (string record in text.Split(';'))
                {
                    PlannerNote note = ReadNote(record);
                    if (note == null)
                    {
                        continue;
                    }
                    read++;
                    if (Note(note.Id) == null && Notes.Count < MostNotes)
                    {
                        Notes.Add(note);
                        NextNoteId = Math.Max(NextNoteId, note.Id + 1);
                    }
                }
                if (read == 0)
                {
                    SaveLines.Unreadable(data, NotesKey, "none of its notes could be read");
                }
            }
            if (data.TryGetValue(RemindersKey, out text) && !string.IsNullOrEmpty(text))
            {
                int read = 0;
                foreach (string record in text.Split(';'))
                {
                    if (!ReadReminder(record, out PlannerReminder reminder))
                    {
                        continue;
                    }
                    read++;
                    if (ReminderIndex(reminder.Entry) < 0 && Reminders.Count < MostReminders)
                    {
                        Reminders.Add(reminder);
                    }
                }
                if (read == 0)
                {
                    SaveLines.Unreadable(data, RemindersKey, "none of its reminders could be read");
                }
            }
            Soonest();
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (data == null || !Loaded)
            {
                return;
            }
            data[Key] = "v=1;next=" + Text(NextNoteId) + ";hide=" + Text(HiddenCategories) + ";lead=" + Text((int)DefaultLead)
                        + ";hello=" + (Welcomed ? "1" : "0") + ";seen=" + Text(SeenStamp) + ";morning=" + Text(MorningDay);
            var text = new StringBuilder(Notes.Count * 48);
            for (int i = 0; i < Notes.Count; i++)
            {
                PlannerNote n = Notes[i];
                if (i > 0)
                {
                    text.Append(';');
                }
                text.Append(Text(n.Id)).Append('|').Append(Text(n.Day)).Append('|').Append(Text(n.Start)).Append('|')
                    .Append((n.Done ? 1 : 0) | (n.Sample ? 2 : 0) | (n.HasPlace ? 4 : 0)).Append('|')
                    .Append(Escape(n.Title)).Append('|').Append(Escape(n.Place)).Append('|')
                    .Append(Text(n.X)).Append('|').Append(Text(n.Y)).Append('|').Append(Text(n.Z)).Append('|')
                    .Append(Escape(n.Street)).Append('|').Append(Text(n.Number)).Append('|')
                    .Append(Text((int)n.Repeat)).Append('|').Append(Text(n.Repeat == PlannerRepeat.Once ? -1 : n.DoneDay)).Append('|').Append(Text((int)n.Lead));   // fields 12 to 14 may be missing
            }
            data[NotesKey] = text.ToString();
            text.Length = 0;
            for (int i = 0; i < Reminders.Count; i++)
            {
                PlannerReminder r = Reminders[i];
                if (i > 0)
                {
                    text.Append(';');
                }
                text.Append(Escape(r.Entry.Source)).Append('|').Append(Text(r.Entry.A)).Append('|').Append(Text(r.Entry.B)).Append('|')
                    .Append(Text(r.EntryBegins)).Append('|').Append(Text(r.FireAt)).Append('|').Append(Text((int)r.Lead)).Append('|').Append(Text(r.Travel));
            }
            data[RemindersKey] = text.ToString();
        }

        private static PlannerNote ReadNote(string record)
        {
            string[] f = record.Split('|');
            if (f.Length < 5 || !Int(f[0], out int id) || !Int(f[1], out int day) || !Int(f[2], out int start) || !Int(f[3], out int flags) || id < 1 || day < 0)
            {
                return null;
            }
            var note = new PlannerNote
            {
                Id = id,
                Day = day,
                Start = start >= 0 && start < PlannerTime.MinutesPerDay ? start : -1,
                Done = (flags & 1) != 0,
                Sample = (flags & 2) != 0,
                Title = Clean(Unescape(f[4]))
            };
            if (f.Length >= 11)
            {
                note.Place = NullIfEmpty(Unescape(f[5]));
                note.Street = NullIfEmpty(Unescape(f[9]));
                if (Float(f[6], out float x) && Float(f[7], out float y) && Float(f[8], out float z) && Int(f[10], out int number))
                {
                    note.HasPlace = (flags & 4) != 0;
                    note.X = x;
                    note.Y = y;
                    note.Z = z;
                    note.Number = number;
                }
            }
            if (f.Length >= 14 && Int(f[11], out int repeat) && Int(f[12], out int doneDay) && Int(f[13], out int lead))
            {
                note.Repeat = repeat >= (int)PlannerRepeat.Once && repeat <= (int)PlannerRepeat.Weekly ? (PlannerRepeat)repeat : PlannerRepeat.Once;
                note.DoneDay = note.Repeat == PlannerRepeat.Once ? -1 : doneDay;
                note.Lead = Lead(lead, ReminderLead.None);
            }
            return note;
        }

        private static bool ReadReminder(string record, out PlannerReminder reminder)
        {
            reminder = default(PlannerReminder);
            string[] f = record.Split('|');
            if (f.Length < 6 || !Int(f[1], out int a) || !Int(f[2], out int b) || !Int(f[3], out int begins) || !Int(f[4], out int fireAt) || !Int(f[5], out int lead))
            {
                return false;
            }
            string source = Unescape(f[0]);
            ReminderLead kind = Lead(lead, ReminderLead.None);
            if (string.IsNullOrEmpty(source) || kind == ReminderLead.None || fireAt < 0)
            {
                return false;
            }
            reminder = new PlannerReminder
            {
                Entry = new EntryId(string.Intern(source), a, b), EntryBegins = begins, FireAt = fireAt, Lead = kind,
                Travel = kind == ReminderLead.LeaveBy && f.Length >= 7 && Int(f[6], out int travel) && travel > 0 ? travel : 0
            };
            return true;
        }

        private static ReminderLead Lead(int value, ReminderLead otherwise)
        {
            return value >= (int)ReminderLead.AtTheTime && value <= (int)ReminderLead.LeaveBy ? (ReminderLead)value : otherwise;
        }

        // '|' parts the fields and ';' the records: both, and '~', are written as two characters
        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.IndexOf('|') < 0 && text.IndexOf(';') < 0 && text.IndexOf('~') < 0)
            {
                return text;
            }
            return text.Replace("~", "~t").Replace("|", "~p").Replace(";", "~s");
        }

        private static string Unescape(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('~') < 0)
            {
                return text ?? string.Empty;
            }
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '~' || i + 1 >= text.Length)
                {
                    result.Append(c);
                    continue;
                }
                char next = text[++i];
                result.Append(next == 'p' ? '|' : next == 's' ? ';' : next == 't' ? '~' : next);
            }
            return result.ToString();
        }

        private static string NullIfEmpty(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Text(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool Int(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool Float(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
