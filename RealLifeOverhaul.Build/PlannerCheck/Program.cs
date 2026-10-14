using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Planner;

// Checks for the Planner's game-free parts. A check that fails prints one line; the last line is
// ALL CHECKS PASSED or the count that failed.
internal static partial class Program
{
    private static int _failed, _run;

    private static void Check(bool ok, string what)
    {
        _run++;
        if (!ok)
        {
            _failed++;
            Console.WriteLine("FAIL  " + what);
        }
    }

    private static PlannerEntry Add(PlannerList list, string source, int a, int day, int start, int end, bool attend = true)
    {
        PlannerEntry e = list.Add(source, a, 0, PlannerCategory.Work, day);
        if (e != null)
        {
            e.Start = start;
            e.End = end;
            e.Attend = attend;
        }
        return e;
    }

    private static int Main()
    {
        Calendar();
        Order();
        Clashes();
        States();
        Badge();
        Reminders();
        SaveAndLoad();
        Damaged();
        Limits();
        Reach();
        Travel();
        Leads();
        Repeats();
        Deleting();
        Groups();
        NewFields();
        OldSaves();
        Week();
        Free();
        Owed();
        Texts();
        Console.WriteLine(_failed == 0 ? "ALL CHECKS PASSED (" + _run + ")" : _failed + " of " + _run + " CHECKS FAILED");
        return _failed == 0 ? 0 : 1;
    }

    private static void Calendar()
    {
        Check(PlannerTime.At(1, 0) == 1440 && PlannerTime.At(3, 9, 30) == (3 * 24 + 9) * 60 + 30, "minutes as the game counts them");
        Check(PlannerTime.DayOf(PlannerTime.At(12, 23, 59)) == 12 && PlannerTime.OfDay(PlannerTime.At(12, 23, 59)) == 1439, "day and minute of a moment");
        Check(PlannerTime.DayOf(PlannerTime.At(13, 0)) == 13 && PlannerTime.OfDay(PlannerTime.At(13, 0)) == 0, "midnight belongs to the new day");
        Check(PlannerTime.Weekday(1) == 0 && PlannerTime.Weekday(7) == 6 && PlannerTime.Weekday(8) == 0 && PlannerTime.Weekday(6) == 5, "day 1 is a Monday, day 6 a Saturday");
        Check(PlannerTime.Weekday(0) == 6 && PlannerTime.Weekday(-6) == 0, "weekday of a day before the first does not throw");
        Check(PlannerTime.Year(59, 60) == 0 && PlannerTime.Year(60, 60) == 1 && PlannerTime.Year(125, 60) == 2, "the game's year");
        Check(PlannerTime.NextYearStart(1, 60) == 60 && PlannerTime.NextYearStart(60, 60) == 120 && PlannerTime.NextYearStart(61, 120) == 120, "the next year's first day");
        Check(PlannerTime.Year(10, 0) == 0 && PlannerTime.NextYearStart(10, 0) == 0, "a year of no days is no year");
    }

    private static void Order()
    {
        var list = new PlannerList();
        list.Begin(1, 10, false);
        Add(list, "b", 1, 3, 600, 660);
        Add(list, "a", 2, 2, 900, 960);
        PlannerEntry allDay = Add(list, "c", 3, 2, -1, -1, false);
        PlannerEntry deadline = Add(list, "d", 4, 2, -1, 1020, false);
        deadline.Deadline = true;
        Add(list, "a", 5, 2, 540, 600);
        Check(Add(list, "x", 9, 11, 0, 60) == null && Add(list, "x", 9, 0, 0, 60) == null, "a day outside the range is not taken");
        PlannerLogic.Arrange(list, PlannerTime.At(1, 0));
        List<PlannerEntry> i = list.Items;
        Check(i.Count == 5 && i[0] == allDay && i[1].Id.A == 5 && i[2].Id.A == 2 && i[3] == deadline && i[4].Id.A == 1, "all day first, then by the clock, a deadline at its hour, then the next day");
        Check(deadline.Begins == PlannerTime.At(2, 17) && !deadline.AllDay && allDay.AllDay, "a deadline stands at its last moment");
        // the same again must give the same order (ties are broken by who it is, never by chance)
        list.Begin(1, 10, false);
        Add(list, "z", 2, 4, 600, 660);
        Add(list, "z", 1, 4, 600, 660);
        Add(list, "y", 7, 4, 600, 660);
        PlannerLogic.Arrange(list, 0);
        Check(list.Items[0].Id.Source == "y" && list.Items[1].Id.A == 1 && list.Items[2].Id.A == 2, "two things at one time keep one order");
        Check(list.Find(new EntryId("z", 2)) == list.Items[2] && list.Find(new EntryId("z", 3)) == null, "an entry is found by who it is");
    }

    private static void Clashes()
    {
        var list = new PlannerList();
        list.Begin(1, 10, false);
        PlannerEntry shift = Add(list, "work", 1, 5, 540, 1020);
        PlannerEntry court = Add(list, "court", 2, 5, 600, 660);
        PlannerEntry market = Add(list, "city", 3, 5, 480, 840, false);
        PlannerEntry dinner = Add(list, "family", 4, 5, 1020, 1140);
        PlannerEntry other = Add(list, "work", 5, 6, 540, 1020);
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6));
        Check(shift.Clash && court.Clash, "a hearing inside a shift clashes with it");
        Check(!market.Clash, "something the player need not attend clashes with nothing");
        Check(!dinner.Clash, "one ending as the next begins is no clash");
        Check(!other.Clash, "the same hours on another day are no clash");
        court.Done = true;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6));
        Check(!shift.Clash && !court.Clash, "what is done no longer clashes");
        court.Done = false;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 12));
        Check(!shift.Clash && court.State == PlannerState.Missed, "what was missed no longer clashes");
        // a night shift runs past midnight
        list.Begin(1, 10, false);
        PlannerEntry night = Add(list, "work", 1, 5, 1320, 360);
        PlannerEntry late = Add(list, "social", 2, 5, 1380, 1410);
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6));
        Check(night.Ends == PlannerTime.At(6, 6) && night.Clash && late.Clash, "a shift that ends after midnight ends the next day");
    }

    private static void States()
    {
        var list = new PlannerList();
        list.Begin(1, 10, false);
        PlannerEntry past = Add(list, "a", 1, 5, 480, 540);
        PlannerEntry market = Add(list, "a", 2, 5, 480, 540, false);
        PlannerEntry now = Add(list, "a", 3, 5, 600, 720);
        PlannerEntry soon = Add(list, "a", 4, 5, 780, 840);
        PlannerEntry later = Add(list, "a", 5, 6, 480, 540);
        PlannerEntry allDay = Add(list, "a", 6, 5, -1, -1, false);
        PlannerEntry due = Add(list, "a", 7, 5, -1, 1020, false);
        due.Deadline = true;
        PlannerEntry open = Add(list, "a", 8, 5, 630, -1);
        int at = PlannerTime.At(5, 10, 30);
        PlannerLogic.Arrange(list, at);
        Check(past.State == PlannerState.Missed && market.State == PlannerState.Over, "past: missed if the player had to be there, over if not");
        Check(now.State == PlannerState.Now && soon.State == PlannerState.Upcoming && later.State == PlannerState.Upcoming, "now and upcoming");
        Check(allDay.State == PlannerState.Now && !allDay.Next, "an all-day entry is on all day and is never 'next'");
        Check(due.State == PlannerState.Upcoming, "a deadline is open until its hour");
        Check(open.State == PlannerState.Now && open.Ends == PlannerTime.At(5, 11, 30), "an entry with no end lasts an hour");
        Check(now.Next && !soon.Next && !open.Next, "what is going on now is what is next");
        now.Done = true;
        open.Done = true;
        PlannerLogic.Arrange(list, at);
        Check(now.State == PlannerState.Done && soon.Next, "done: the next one is the nearest still to come");
        PlannerLogic.Arrange(list, PlannerTime.At(5, 17));
        Check(due.State == PlannerState.Missed, "a deadline is missed at its hour");
        PlannerLogic.Arrange(list, PlannerTime.At(6, 0));
        Check(allDay.State == PlannerState.Over && later.Next, "the day changes: yesterday's all-day entry is over, tomorrow's first is next");
        // A class can be joined only until a while after it starts (StudyRules.Joinable): after that it is missed, though its
        // hours run on, and it is not what is next.
        list.Begin(1, 10, false);
        PlannerEntry lesson = Add(list, "a", 9, 5, 540, 720);
        lesson.JoinBy = 600;
        PlannerEntry after = Add(list, "a", 10, 5, 780, 840);
        PlannerLogic.Arrange(list, PlannerTime.At(5, 9, 30));
        Check(lesson.State == PlannerState.Now && lesson.Next, "a class is on and next while it can still be joined");
        PlannerLogic.Arrange(list, PlannerTime.At(5, 10, 0));
        Check(lesson.State == PlannerState.Now, "a class can be joined until its last minute");
        PlannerLogic.Arrange(list, PlannerTime.At(5, 10, 12));
        Check(lesson.State == PlannerState.Missed && !lesson.Next && after.Next, "past the moment it can be joined, a class is missed and the next entry is next");
        lesson.JoinBy = -1;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 10, 12));
        Check(lesson.State == PlannerState.Now, "an entry with no last moment to begin is on until it ends");
        list.Begin(1, 10, false);
        PlannerLogic.Arrange(list, at);
        Check(list.Items.Count == 0, "an empty agenda arranges to nothing");
    }

    private static void Badge()
    {
        var list = new PlannerList();
        list.Begin(1, 10, false);
        Add(list, "a", 1, 5, 600, 660);
        Add(list, "a", 2, 5, 720, 780);
        PlannerEntry done = Add(list, "a", 3, 5, 630, 690);
        done.Done = true;
        Add(list, "a", 4, 5, -1, -1, false);
        Add(list, "a", 5, 6, 60, 120);
        int at = PlannerTime.At(5, 9);
        Check(PlannerLogic.DueSoon(list, at, 120) == 1, "due within two hours: one");
        Check(PlannerLogic.DueSoon(list, at, 180) == 2, "due within three hours: two (the done one and the all-day one do not count)");
        Check(PlannerLogic.DueSoon(list, PlannerTime.At(5, 23, 30), 120) == 1, "across midnight");
        int stamp = PlannerLogic.DueStamp(list, at, 180);
        Check(stamp == PlannerLogic.DueStamp(list, at + 5, 180), "nothing new: the stamp stays");
        Add(list, "a", 6, 5, 700, 760);
        Check(stamp != PlannerLogic.DueStamp(list, at, 180), "something new is due: the stamp changes");
    }

    private static void Reminders()
    {
        PlannerStore.Reset();
        PlannerStore.Load(null);
        var due = new List<PlannerReminder>();
        var shift = new EntryId("work", 12);
        var court = new EntryId("court", 3);
        var note = new EntryId("note", 1);
        int now = PlannerTime.At(10, 12);
        Check(PlannerStore.FireAt(11, 570, ReminderLead.AtTheTime) == PlannerTime.At(11, 9, 30), "at the time");
        Check(PlannerStore.FireAt(11, 570, ReminderLead.HourBefore) == PlannerTime.At(11, 8, 30), "an hour before");
        Check(PlannerStore.FireAt(11, 570, ReminderLead.EveningBefore) == PlannerTime.At(10, 18), "the evening before");
        Check(PlannerStore.FireAt(11, -1, ReminderLead.AtTheTime) == PlannerTime.At(11, 8) && PlannerStore.FireAt(11, -1, ReminderLead.HourBefore) == PlannerTime.At(11, 7), "an all-day entry counts from 08:00");
        Check(PlannerStore.FireAt(11, 30, ReminderLead.HourBefore) == PlannerTime.At(10, 23, 30), "an hour before 00:30 is the day before");

        Check(PlannerStore.SetReminder(shift, 11, 540, ReminderLead.HourBefore, now), "set");
        Check(PlannerStore.SetReminder(court, 11, 600, ReminderLead.EveningBefore, now), "set a second");
        Check(!PlannerStore.SetReminder(note, 10, 600, ReminderLead.AtTheTime, now) && PlannerStore.ReminderIndex(note) < 0, "a moment that has passed is refused and nothing is kept");
        Check(!PlannerStore.SetReminder(note, 11, 600, ReminderLead.EveningBefore, PlannerTime.At(10, 19)), "the evening before, asked for after that evening, is refused");
        Check(PlannerStore.SetReminder(shift, 11, 540, ReminderLead.AtTheTime, now) && PlannerStore.Reminders.Count == 2 && PlannerStore.LeadOf(shift) == ReminderLead.AtTheTime, "setting again changes it: one reminder per entry");
        Check(PlannerStore.LeadOf(note) == ReminderLead.None, "none set");

        PlannerStore.TakeDue(PlannerTime.At(10, 17), due);
        Check(due.Count == 0, "nothing due yet");
        PlannerStore.TakeDue(PlannerTime.At(10, 18), due);
        Check(due.Count == 1 && due[0].Entry.Equals(court) && !PlannerStore.IsLate(due[0], PlannerTime.At(10, 18)), "the evening before goes out at 18:00, on time");
        PlannerStore.TakeDue(PlannerTime.At(10, 18), due);
        Check(due.Count == 0, "and never twice");
        // the save is written and read back between the two
        var data = new Dictionary<string, string>();
        PlannerStore.Save(data);
        PlannerStore.Load(data);
        Check(PlannerStore.Reminders.Count == 1 && PlannerStore.LeadOf(shift) == ReminderLead.AtTheTime && PlannerStore.ReminderIndex(court) < 0, "a save keeps what is still to come and not what went out");
        PlannerStore.TakeDue(PlannerTime.At(11, 8), due);
        Check(due.Count == 0, "08:00: the 09:00 reminder is not within the hour");
        PlannerStore.TakeDue(PlannerTime.At(11, 9), due);
        Check(due.Count == 1 && !PlannerStore.IsLate(due[0], PlannerTime.At(11, 9)) && PlannerStore.Reminders.Count == 0, "09:00: out, on time");

        // a reminder goes out at its minute (the director asks once a second), never before it
        PlannerStore.SetReminder(note, 12, 570, ReminderLead.AtTheTime, PlannerTime.At(11, 10));
        Check(PlannerStore.NextFire == PlannerTime.At(12, 9, 30), "the soonest reminder is known: one comparison a second");
        PlannerStore.TakeDue(PlannerTime.At(12, 9), due);
        Check(due.Count == 0, "09:00 is too early for 09:30");
        PlannerStore.TakeDue(PlannerTime.At(12, 9, 29), due);
        Check(due.Count == 0, "09:29 is too early for 09:30");
        PlannerStore.TakeDue(PlannerTime.At(12, 9, 30), due);
        Check(due.Count == 1 && !PlannerStore.IsLate(due[0], PlannerTime.At(12, 9, 30)) && PlannerStore.NextFire == int.MaxValue, "09:30: out, on time; nothing left to wait for");
        Check(!PlannerStore.IsLate(due[0], PlannerTime.At(12, 9, 30) + PlannerStore.LateMinutes) && PlannerStore.IsLate(due[0], PlannerTime.At(12, 9, 31) + PlannerStore.LateMinutes),
            "a reminder is late only when it goes out more than half an hour after its moment");

        // a time skip: three reminders fall inside it, each goes out once when it ends, marked late, in their order
        PlannerStore.SetReminder(new EntryId("a", 1), 14, 600, ReminderLead.AtTheTime, PlannerTime.At(13, 0));
        PlannerStore.SetReminder(new EntryId("a", 2), 14, 480, ReminderLead.AtTheTime, PlannerTime.At(13, 0));
        PlannerStore.SetReminder(new EntryId("a", 3), 15, 480, ReminderLead.EveningBefore, PlannerTime.At(13, 0));
        PlannerStore.SetReminder(new EntryId("a", 4), 16, 480, ReminderLead.AtTheTime, PlannerTime.At(13, 0));
        int after = PlannerTime.At(15, 2);
        PlannerStore.TakeDue(after, due);
        Check(due.Count == 3 && due[0].Entry.A == 2 && due[1].Entry.A == 1 && due[2].Entry.A == 3, "after a skip: everything that fell inside it, oldest first");
        Check(due.Count == 3 && PlannerStore.IsLate(due[0], after) && PlannerStore.IsLate(due[2], after), "and marked late");
        PlannerStore.TakeDue(after, due);
        Check(due.Count == 0 && PlannerStore.Reminders.Count == 1, "once only; what is still to come stays");
        Check(PlannerStore.SetReminder(new EntryId("a", 4), 0, 0, ReminderLead.None, after) && PlannerStore.Reminders.Count == 0, "None takes a reminder away");
        Check(!PlannerStore.SetReminder(new EntryId(null, 1), 20, 0, ReminderLead.AtTheTime, after), "an entry that is nobody's cannot be reminded of");
    }

    private static void SaveAndLoad()
    {
        PlannerStore.Reset();
        var untouched = new Dictionary<string, string>();
        PlannerStore.Save(untouched);
        Check(untouched.Count == 0, "nothing is written before a save was read: a load that failed cannot wipe the notes");
        PlannerStore.Load(null);
        Check(PlannerStore.Loaded && PlannerStore.Notes.Count == 0 && PlannerStore.DefaultLead == ReminderLead.HourBefore, "a save without the Planner loads empty");
        PlannerNote plain = PlannerStore.AddNote("Buy a present", 12, 600);
        PlannerNote nasty = PlannerStore.AddNote("  a|b;c~d <b>e</b>\nf  ", 13, -1);
        nasty.Done = true;
        nasty.Sample = true;
        nasty.Place = "5th Ave; no. 3|x";
        nasty.HasPlace = true;
        nasty.X = 12.5f;
        nasty.Y = -3f;
        nasty.Z = 1000.25f;
        nasty.Street = "ba:street~name";
        nasty.Number = 44;
        PlannerStore.Show(PlannerCategory.City, false);
        PlannerStore.Show(PlannerCategory.Note, false);
        PlannerStore.Show(PlannerCategory.Note, true);
        PlannerStore.DefaultLead = ReminderLead.EveningBefore;
        PlannerStore.Welcomed = true;
        PlannerStore.SeenStamp = -123456;
        PlannerStore.SetReminder(new EntryId("note", nasty.Id), 13, -1, ReminderLead.EveningBefore, 0);
        var data = new Dictionary<string, string>();
        PlannerStore.Save(data);
        Check(data.Count == 3 && data.ContainsKey(PlannerStore.Key) && data.ContainsKey(PlannerStore.NotesKey) && data.ContainsKey(PlannerStore.RemindersKey), "three lines, all under reallife:planner");
        string cleaned = nasty.Title;
        PlannerStore.Load(data);
        PlannerNote a = PlannerStore.Note(plain.Id), b = PlannerStore.Note(nasty.Id);
        Check(PlannerStore.Notes.Count == 2 && a != null && a.Title == "Buy a present" && a.Day == 12 && a.Start == 600 && !a.Done && !a.HasPlace, "a plain note comes back");
        Check(b != null && b.Title == cleaned && cleaned.IndexOf('\n') < 0 && cleaned.IndexOf('<') < 0 && cleaned == cleaned.Trim() && b.Start == -1 && b.Done && b.Sample, "a title with every awkward character comes back as it was kept: one line, no mark-up");
        Check(b != null && b.Place == "5th Ave; no. 3|x" && b.HasPlace && b.X == 12.5f && b.Y == -3f && b.Z == 1000.25f && b.Street == "ba:street~name" && b.Number == 44, "a place comes back");
        Check(!PlannerStore.Shows(PlannerCategory.City) && PlannerStore.Shows(PlannerCategory.Note) && PlannerStore.DefaultLead == ReminderLead.EveningBefore && PlannerStore.Welcomed && PlannerStore.SeenStamp == -123456, "the settings come back");
        Check(PlannerStore.Reminders.Count == 1 && PlannerStore.Reminders[0].Entry.Equals(new EntryId("note", nasty.Id)) && PlannerStore.Reminders[0].FireAt == PlannerTime.At(12, 18), "the reminder comes back");
        Check(PlannerStore.AddNote("next", 1, 0).Id == nasty.Id + 1, "the next note gets a new number after a load");
        var again = new Dictionary<string, string>();
        PlannerStore.RemoveNote(nasty.Id + 1);
        PlannerStore.Save(again);
        Check(again[PlannerStore.NotesKey] == data[PlannerStore.NotesKey] && again[PlannerStore.RemindersKey] == data[PlannerStore.RemindersKey], "saving what was loaded writes the same lines");
        Check(PlannerStore.RemoveNote(plain.Id) && !PlannerStore.RemoveNote(plain.Id) && PlannerStore.Note(plain.Id) == null, "a note is removed once");
        Check(PlannerStore.Clean(null) == string.Empty && PlannerStore.Clean(new string('x', 500)).Length == PlannerStore.TitleLength, "a title is never null and never longer than sixty characters");
    }

    private static void Damaged()
    {
        string[] bad =
        {
            "", ";", "|||", "abc", "1|2", "x|1|2|0|title", "1|x|2|0|title", "-5|1|2|0|title", "1|-1|2|0|title",
            "1|5|99999|0|kept", "2|5|600|0|ok|place|nan|0|0|street|x", "3|5|600|7|ok|place|1e999|0|0||3", "4|5|600|0|t~", "5|5|600|0|~~~|", "1|6|0|0|twice"
        };
        var data = new Dictionary<string, string>
        {
            { PlannerStore.Key, "v=1;next=abc;hide=999999;lead=42;hello;=;seen=7;;what=1" },
            { PlannerStore.NotesKey, string.Join(";", bad) },
            { PlannerStore.RemindersKey, "|1|2|3|4|1;note|x|0|0|5|1;note|1|0|0|5|9;note|1|0|0|-5|1;note|1|0|100|200|2;note|1|0|100|300|1;work|2|3|100|400|3;work" }
        };
        bool threw = false;
        try
        {
            PlannerStore.Load(data);
        }
        catch (Exception)
        {
            threw = true;
        }
        Check(!threw, "a damaged save does not throw");
        Check(PlannerStore.Loaded && PlannerStore.SeenStamp == 7 && PlannerStore.DefaultLead == ReminderLead.HourBefore && PlannerStore.NextNoteId >= 6, "what can be read of the settings is read, the rest keeps its default");
        Check((PlannerStore.HiddenCategories >> (int)PlannerCategory.Count) == 0, "no category bit beyond the last");
        Check(PlannerStore.Notes.Count == 5, "five notes can be read out of the damaged records (" + PlannerStore.Notes.Count + ")");
        PlannerNote one = PlannerStore.Note(1);
        Check(one != null && one.Title == "kept" && one.Start == -1, "an hour that is no hour becomes 'all day'; a number used twice keeps the first");
        PlannerNote two = PlannerStore.Note(2), three = PlannerStore.Note(3);
        Check(two != null && !two.HasPlace && three != null && !three.HasPlace, "a place that cannot be read is no place");
        Check(PlannerStore.Reminders.Count == 2 && PlannerStore.LeadOf(new EntryId("note", 1)) == ReminderLead.HourBefore && PlannerStore.LeadOf(new EntryId("work", 2, 3)) == ReminderLead.EveningBefore, "two reminders can be read; the second for one entry is dropped");
        var huge = new Dictionary<string, string> { { PlannerStore.NotesKey, new string('|', 20000) + new string(';', 20000) } };
        PlannerStore.Load(huge);
        Check(PlannerStore.Notes.Count == 0, "noise is nothing");
        Check(SaveLines.IsKept(huge, PlannerStore.NotesKey) && !SaveLines.IsKept(data, PlannerStore.NotesKey), "a line of which nothing can be read is kept as it was read; one partly read is not");
        var later = new Dictionary<string, string> { { PlannerStore.Key, "v=2;next=4" }, { PlannerStore.RemindersKey, "v2#note#1#0" } };
        PlannerStore.Load(later);
        Check(!SaveLines.IsKept(later, PlannerStore.Key) && SaveLines.IsKept(later, PlannerStore.RemindersKey) && PlannerStore.NextNoteId == 4,
            "settings of a later version are read as far as they can be; reminders of another shape are kept");
        later[PlannerStore.RemindersKey] = string.Empty;
        Check(SaveLines.PutBack(later) == 1 && later[PlannerStore.RemindersKey] == "v2#note#1#0", "a kept line goes back as it was read when the Planner writes its own");
        SaveLines.Forget();
    }

    private static void Limits()
    {
        PlannerStore.Reset();
        PlannerStore.Load(null);
        for (int i = 0; i < PlannerStore.MostNotes + 20; i++)
        {
            PlannerStore.AddNote("n" + i, 3, i);
        }
        Check(PlannerStore.Notes.Count == PlannerStore.MostNotes && PlannerStore.AddNote("one more", 3, 0) == null, "no more notes than the most");
        for (int i = 0; i < PlannerStore.MostReminders + 20; i++)
        {
            PlannerStore.SetReminder(new EntryId("note", i + 1), 30, 600, ReminderLead.AtTheTime, 0);
        }
        Check(PlannerStore.Reminders.Count == PlannerStore.MostReminders, "no more reminders than the most");
        var data = new Dictionary<string, string>();
        PlannerStore.Save(data);
        PlannerStore.Load(data);
        Check(PlannerStore.Notes.Count == PlannerStore.MostNotes && PlannerStore.Reminders.Count == PlannerStore.MostReminders, "a full save comes back whole");
        var list = new PlannerList();
        list.Begin(1, 5, false);
        int taken = 0;
        for (int i = 0; i < PlannerLogic.MostEntries + 50; i++)
        {
            if (Add(list, "a", i, 1 + i % 5, i % 1440, -1) != null)
            {
                taken++;
            }
        }
        PlannerLogic.Arrange(list, PlannerTime.At(3, 12));
        Check(taken == PlannerLogic.MostEntries && list.Items.Count == taken, "a look never holds more than the most entries");
        int next = 0;
        bool ordered = true;
        for (int i = 0; i < list.Items.Count; i++)
        {
            next += list.Items[i].Next ? 1 : 0;
            ordered &= i == 0 || list.Items[i - 1].Day <= list.Items[i].Day;
        }
        Check(ordered, "four hundred entries are in day order");
        Check(next == 1, "one entry is next, however many there are");
        // the pool gives the same objects back: a second look makes nothing new
        PlannerEntry first = list.Items[0];
        first.Title = "x";
        first.Done = true;
        list.Begin(1, 5, false);
        bool reused = false;
        for (int i = 0; i < PlannerLogic.MostEntries; i++)
        {
            PlannerEntry e = Add(list, "a", i, 1, 0, -1);
            reused |= e == first && e.Title == null && !e.Done;
        }
        Check(reused, "entries are reused, and come back blank");
    }
}
