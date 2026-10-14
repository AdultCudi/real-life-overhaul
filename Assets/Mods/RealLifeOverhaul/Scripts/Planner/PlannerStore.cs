using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Planner
{
    // Free of the game and of the engine, like PlannerModel.cs: checked outside the game by PlannerCheck.

    /// <summary>When a reminder goes out, counted from the entry it is for.</summary>
    public enum ReminderLead
    {
        None = 0,
        AtTheTime = 1,
        HourBefore = 2,
        EveningBefore = 3
    }

    /// <summary>One of the player's own entries.</summary>
    public sealed class PlannerNote
    {
        public int Id;
        public string Title = string.Empty;
        public int Day;
        public int Start = -1;       // minutes since midnight; -1: all day
        public bool Done;
        public bool Sample;          // made by the test button: the other test button removes it
        public string Place;         // what the place is called, or null
        public bool HasPlace;
        public float X, Y, Z;
        public string Street;
        public int Number;
    }

    /// <summary>A reminder that has been asked for and has not gone out yet.</summary>
    public struct PlannerReminder
    {
        public EntryId Entry;
        public int EntryBegins;      // game minutes: an entry that has moved since is reminded of at its new time
        public int FireAt;           // game minutes
        public ReminderLead Lead;
    }

    /// <summary>
    /// The Planner's part of a save: three lines in the save's modData, read and written here and nowhere else.
    /// A line that is damaged loses only the records that cannot be read; nothing here ever throws on what it is given.
    /// A line of which nothing at all can be read (of a later version's shape, or damaged through) is kept in the save
    /// exactly as it was read (Core/SaveLines): it is never written over.
    /// </summary>
    public static class PlannerStore
    {
        public const string Key = "reallife:planner";
        public const string NotesKey = "reallife:planner_notes";
        public const string RemindersKey = "reallife:planner_reminders";

        public const int MostNotes = 200;
        public const int MostReminders = 100;
        public const int TitleLength = 60;
        public const int EveningHour = 18;       // "the evening before" is 18:00 on the day before
        public const int AllDayHour = 8;         // an entry with no hour counts as beginning at 08:00 for its reminders

        public static readonly List<PlannerNote> Notes = new List<PlannerNote>(16);
        public static readonly List<PlannerReminder> Reminders = new List<PlannerReminder>(16);

        public static bool Loaded { get; private set; }
        public static int NextNoteId = 1;
        public static int HiddenCategories;                       // one bit per PlannerCategory
        public static ReminderLead DefaultLead = ReminderLead.HourBefore;
        public static bool Welcomed;                              // the first-run notification has been shown on this save
        public static int SeenStamp;                              // PlannerLogic.DueStamp when the app was last opened

        public static void Reset()
        {
            Loaded = false;
            Notes.Clear();
            Reminders.Clear();
            NextNoteId = 1;
            HiddenCategories = 0;
            DefaultLead = ReminderLead.HourBefore;
            Welcomed = false;
            SeenStamp = 0;
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

        // ---------------------------------------------------------------- notes

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
                    return true;
                }
            }
            return false;
        }

        /// <summary>A title as it is kept: one line, no more than sixty characters, never null.</summary>
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
                // No line breaks, and none of the angle brackets a text field would read as mark-up.
                text.Append(c < ' ' || c == '<' || c == '>' ? ' ' : c);
            }
            return text.ToString().Trim();
        }

        // ---------------------------------------------------------------- reminders

        /// <summary>When a reminder for an entry goes out, in game minutes.</summary>
        public static int FireAt(int entryDay, int entryStart, ReminderLead lead)
        {
            int begins = PlannerTime.At(entryDay, 0) + (entryStart >= 0 ? entryStart : AllDayHour * 60);
            switch (lead)
            {
                case ReminderLead.HourBefore:
                    return begins - 60;
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
        /// Sets, changes or (with None) takes away the one reminder an entry can have. False if the moment has already
        /// passed or there is no room: nothing is kept then.
        /// </summary>
        public static bool SetReminder(EntryId entry, int entryDay, int entryStart, ReminderLead lead, int nowMinutes)
        {
            int index = ReminderIndex(entry);
            if (index >= 0)
            {
                Reminders.RemoveAt(index);
            }
            if (lead == ReminderLead.None)
            {
                return true;
            }
            int fireAt = FireAt(entryDay, entryStart, lead);
            if (fireAt < nowMinutes || Reminders.Count >= MostReminders || string.IsNullOrEmpty(entry.Source))
            {
                return false;
            }
            int begins = PlannerTime.At(entryDay, 0) + (entryStart >= 0 ? entryStart : 0);
            Reminders.Add(new PlannerReminder { Entry = entry, EntryBegins = begins, FireAt = fireAt, Lead = lead });
            return true;
        }

        /// <summary>
        /// Takes out of the list every reminder that is due and hands it over, each once. The clock is looked at once
        /// a game hour, so a reminder is due when it falls within the hour that has just begun: it is never more than
        /// 59 minutes early, and late only if the hour it fell in was jumped over (a time skip, a loaded save).
        /// </summary>
        public static void TakeDue(int nowMinutes, List<PlannerReminder> due)
        {
            due.Clear();
            for (int i = Reminders.Count - 1; i >= 0; i--)
            {
                if (Reminders[i].FireAt < nowMinutes + 60)
                {
                    due.Add(Reminders[i]);
                    Reminders.RemoveAt(i);   // out of the list before anything is shown: it cannot go out twice
                }
            }
            if (due.Count > 1)
            {
                due.Sort(ByFireAt);
            }
        }

        private static readonly Comparison<PlannerReminder> ByFireAt = (a, b) => a.FireAt.CompareTo(b.FireAt);

        /// <summary>Whether a reminder taken by TakeDue missed its moment.</summary>
        public static bool IsLate(PlannerReminder reminder, int nowMinutes)
        {
            return reminder.FireAt < nowMinutes;
        }

        // ---------------------------------------------------------------- the save

        public static void Load(Dictionary<string, string> data)
        {
            Reset();
            Loaded = true;
            if (data == null)
            {
                return;
            }
            string text;
            if (data.TryGetValue(Key, out text) && !string.IsNullOrEmpty(text))
            {
                int read = 0;
                foreach (string part in text.Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    int value;
                    if (!Int(part.Substring(eq + 1), out value))
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
                    PlannerReminder reminder;
                    if (!ReadReminder(record, out reminder))
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
        }

        public static void Save(Dictionary<string, string> data)
        {
            if (data == null || !Loaded)
            {
                return;
            }
            data[Key] = "v=1;next=" + Text(NextNoteId) + ";hide=" + Text(HiddenCategories) + ";lead=" + Text((int)DefaultLead)
                        + ";hello=" + (Welcomed ? "1" : "0") + ";seen=" + Text(SeenStamp);
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
                    .Append(Escape(n.Street)).Append('|').Append(Text(n.Number));
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
                    .Append(Text(r.EntryBegins)).Append('|').Append(Text(r.FireAt)).Append('|').Append(Text((int)r.Lead));
            }
            data[RemindersKey] = text.ToString();
        }

        private static PlannerNote ReadNote(string record)
        {
            string[] f = record.Split('|');
            int id, day, start, flags;
            if (f.Length < 5 || !Int(f[0], out id) || !Int(f[1], out day) || !Int(f[2], out start) || !Int(f[3], out flags) || id < 1 || day < 0)
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
                float x, y, z;
                int number;
                note.Place = NullIfEmpty(Unescape(f[5]));
                note.Street = NullIfEmpty(Unescape(f[9]));
                if (Float(f[6], out x) && Float(f[7], out y) && Float(f[8], out z) && Int(f[10], out number))
                {
                    note.HasPlace = (flags & 4) != 0;
                    note.X = x;
                    note.Y = y;
                    note.Z = z;
                    note.Number = number;
                }
            }
            return note;
        }

        private static bool ReadReminder(string record, out PlannerReminder reminder)
        {
            reminder = default(PlannerReminder);
            string[] f = record.Split('|');
            int a, b, begins, fireAt, lead;
            if (f.Length < 6 || !Int(f[1], out a) || !Int(f[2], out b) || !Int(f[3], out begins) || !Int(f[4], out fireAt) || !Int(f[5], out lead))
            {
                return false;
            }
            string source = Unescape(f[0]);
            ReminderLead kind = Lead(lead, ReminderLead.None);
            if (string.IsNullOrEmpty(source) || kind == ReminderLead.None || fireAt < 0)
            {
                return false;
            }
            reminder = new PlannerReminder { Entry = new EntryId(string.Intern(source), a, b), EntryBegins = begins, FireAt = fireAt, Lead = kind };
            return true;
        }

        private static ReminderLead Lead(int value, ReminderLead otherwise)
        {
            return value >= (int)ReminderLead.AtTheTime && value <= (int)ReminderLead.EveningBefore ? (ReminderLead)value : otherwise;
        }

        // '|' parts a record's fields and ';' parts the records, so both (and the escape itself) are written as two characters.
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
