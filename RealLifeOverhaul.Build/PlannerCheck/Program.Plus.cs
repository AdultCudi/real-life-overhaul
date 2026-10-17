using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul;
using RealLifeOverhaul.Planner;

// The checks of the Planner's day planner: clashes by the way between two places, when to leave, the reminders of
// 15, 30 minutes and when to leave, the player's own entries that repeat (each day, each week, across the week's
// end), deleting, the filter's groups, the new fields of the save and old saves, the Week tab's columns, the day's
// free time and money, and the texts in the three languages.
internal static partial class Program
{
    private static PlannerEntry At(PlannerList list, string source, int a, int day, int start, int end, float x, float z, bool placed = true)
    {
        PlannerEntry e = Add(list, source, a, day, start, end);
        if (e != null)
        {
            e.Placed = placed;
            e.PlaceX = x;
            e.PlaceZ = z;
        }
        return e;
    }

    private static void Reach()
    {
        int drive = PlannerRules.DrivePerMinute;
        var list = new PlannerList();
        list.Begin(1, 10, false);
        PlannerEntry lesson = At(list, "study", 1, 5, 540, 600, 0f, 0f);
        PlannerEntry shift = At(list, "work", 2, 5, 660, 720, 1000f, 0f);      // an hour later, a kilometre away: 168 minutes by road
        PlannerEntry next = At(list, "family", 3, 5, 900, 960, 1010f, 0f);     // three hours after the shift, ten metres from it
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6), drive);
        Check(lesson.Clash && shift.Clash && lesson.ClashReach && shift.ClashReach && lesson.ClashWith == shift && shift.ClashWith == lesson,
            "reach: a kilometre in an hour cannot be made by road: both clash, each with the other, by the way");
        Check(!next.Clash, "reach: the same building three hours later is no clash");
        Check(PlannerLogic.ClashPairs(list.Items, 5, drive) == 1 && PlannerLogic.ClashPairs(list.Items, 6, drive) == 0, "reach: the day's box counts one pair of clashes, the next day none");
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6), 0);
        Check(!lesson.Clash && !shift.Clash && lesson.ClashWith == null, "reach: with no pace (the hourly look) only overlaps count, and the last look's clash is forgotten");
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6));
        Check(!lesson.Clash, "reach: the old call is overlaps only");
        shift.Placed = false;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6), drive);
        Check(!lesson.Clash && !shift.Clash, "reach: a place that is not known is no clash");
        shift.Placed = true;
        shift.PlaceX = 30f;   // inside the same block: no way to go
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6), drive);
        Check(!lesson.Clash, "reach: thirty metres away is the same place");
        shift.PlaceX = 1000f;
        shift.Attend = false;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6), drive);
        Check(!lesson.Clash && !shift.Clash, "reach: something the player need not attend clashes with nothing");

        // An overlap is said before a way too long.
        list.Begin(1, 10, false);
        PlannerEntry x = At(list, "a", 1, 5, 540, 600, 0f, 0f);
        PlannerEntry y = At(list, "a", 2, 5, 570, 630, 0f, 0f);
        PlannerEntry z = At(list, "a", 3, 5, 620, 700, 5000f, 0f);
        PlannerLogic.Arrange(list, PlannerTime.At(5, 6), drive);
        Check(x.ClashWith == y && !x.ClashReach && z.ClashWith == y && !z.ClashReach && y.Clash,
            "reach: an entry that overlaps one and is too far from another names the overlap");

        // The class at 09:00 while a shift runs 06:00 to 14:00 (a common case).
        list.Begin(1, 10, false);
        PlannerEntry early = At(list, "work", 1, 5, 360, 840, 0f, 0f);
        PlannerEntry nine = At(list, "study", 2, 5, 540, 600, 0f, 0f);
        PlannerLogic.Arrange(list, PlannerTime.At(5, 5), drive);
        Check(early.Clash && nine.Clash && !nine.ClashReach && nine.ClashWith == early, "reach: a class at 09:00 inside a shift from 06:00 to 14:00 clashes with it");

        // A night shift and a class across town early the next morning.
        list.Begin(1, 10, false);
        PlannerEntry night = At(list, "work", 1, 5, 1320, 360, 0f, 0f);
        PlannerEntry dawn = At(list, "study", 2, 6, 420, 480, 3000f, 0f);
        PlannerLogic.Arrange(list, PlannerTime.At(5, 12), drive);
        Check(night.Clash && dawn.Clash && dawn.ClashReach, "reach: a night shift ending at 06:00 and a class at 07:00 three kilometres away clash across midnight");

        // A shift is missed once clocking on is refused (three hours after its hour), as a class is.
        list.Begin(1, 10, false);
        PlannerEntry rota = Add(list, "work", 1, 5, 540, 1020);
        rota.JoinBy = (9 + 3) * 60 - 1;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 11, 59));
        Check(rota.State == PlannerState.Now, "missed: a shift can still be clocked on to at 11:59");
        PlannerLogic.Arrange(list, PlannerTime.At(5, 12));
        Check(rota.State == PlannerState.Missed && !rota.Next, "missed: at 12:00 it is missed, though its hours run on");
    }

    private static void Travel()
    {
        Check(PlannerRules.TravelMinutes(0f, PlannerRules.WalkPerMinute) == 0 && PlannerRules.TravelMinutes(PlannerRules.HereMetres, PlannerRules.DrivePerMinute) == 0,
            "travel: no way at all, or within forty metres, is no way");
        Check(PlannerRules.TravelMinutes(float.NaN, PlannerRules.WalkPerMinute) == 0 && PlannerRules.TravelMinutes(1000f, 0) == 0, "travel: no distance, or no pace, is nothing");
        Check(PlannerRules.TravelMinutes(1000f, PlannerRules.WalkPerMinute) == 377, "travel: a kilometre on foot is 1,300 m of street at 3.5 m a minute and 5 to set off: 377 minutes");
        Check(PlannerRules.TravelMinutes(1000f, PlannerRules.DrivePerMinute) == 168, "travel: a kilometre by car is 163 minutes and 5 to set off: 168");
        Check(PlannerRules.TravelMinutes(800f, PlannerRules.DrivePerMinute) == 135, "travel: a way of exact minutes is not rounded up again");
        Check(PlannerRules.TravelMinutes(1e9f, PlannerRules.WalkPerMinute) == 100000 + PlannerRules.SetOffMinutes, "travel: a way off the map is held, not overflowed");
        Check(Math.Abs(PlannerRules.Metres(0f, 0f, 300f, 400f) - 500f) < 0.001f, "travel: the straight line between two doors");
        var list = new PlannerList();
        list.Begin(1, 10, false);
        PlannerEntry nine = Add(list, "a", 1, 5, 540, 600);
        PlannerEntry one = Add(list, "a", 2, 5, 60, 120);
        PlannerEntry due = Add(list, "a", 3, 5, -1, 1020, false);
        due.Deadline = true;
        Check(PlannerRules.LeaveBy(nine, 168) == PlannerTime.At(5, 6, 12), "leave by: 09:00 less 168 minutes is 06:12");
        Check(PlannerRules.LeaveBy(one, 168) == PlannerTime.At(4, 22, 12) && PlannerTime.DayOf(PlannerRules.LeaveBy(one, 168)) == 4, "leave by: 01:00 less 168 minutes is 22:12 the day before");
        Check(PlannerRules.LeaveBy(due, 30) == PlannerTime.At(5, 16, 30), "leave by: a deadline at 17:00 is left for by 16:30");
        Check(PlannerRules.LeaveBy(nine, -50) == nine.Begins && PlannerRules.LeaveBy(nine, 0) == nine.Begins, "leave by: no way, leave at its hour");
        Check(PlannerRules.TravelMinutes(1234.5f, PlannerRules.DrivePerMinute) == PlannerRules.TravelMinutes(1234.5f, PlannerRules.DrivePerMinute), "travel: the same way is the same minutes");
    }

    private static void Leads()
    {
        PlannerStore.Reset();
        PlannerStore.Load(null);
        Check(PlannerStore.FireAt(11, 570, ReminderLead.QuarterBefore) == PlannerTime.At(11, 9, 15) && PlannerStore.FireAt(11, 570, ReminderLead.HalfHourBefore) == PlannerTime.At(11, 9),
            "leads: 15 and 30 minutes before 09:30");
        Check(PlannerStore.FireAt(11, 570, ReminderLead.LeaveBy, 168) == PlannerTime.At(11, 6, 42) && PlannerStore.FireAt(11, 570, ReminderLead.LeaveBy, -5) == PlannerTime.At(11, 9, 30),
            "leads: when to leave is the hour less the way");
        var shift = new EntryId("work", 7);
        Check(PlannerStore.SetReminder(shift, 11, 570, ReminderLead.LeaveBy, PlannerTime.At(10, 12), 168) && PlannerStore.Reminders[0].Travel == 168 && PlannerStore.Reminders[0].FireAt == PlannerTime.At(11, 6, 42),
            "leads: a reminder of when to leave keeps its way");
        Check(PlannerStore.SetReminder(shift, 11, 570, ReminderLead.QuarterBefore, PlannerTime.At(10, 12), 168) && PlannerStore.Reminders.Count == 1 && PlannerStore.Reminders[0].Travel == 0,
            "leads: any other reminder keeps no way, and replaces the one before");
        Check(!PlannerStore.SetReminder(shift, 11, 570, ReminderLead.LeaveBy, PlannerTime.At(11, 8), 168) && PlannerStore.ReminderIndex(shift) < 0,
            "leads: when to leave, asked for after the moment to leave, is refused");
        // The player's own reminder list offers no "when to leave" for a whole series; every lead is read back from a save.
        for (int lead = 0; lead <= (int)ReminderLead.LeaveBy; lead++)
        {
            PlannerStore.Reset();
            PlannerStore.Load(new Dictionary<string, string> { { PlannerStore.Key, "v=1;lead=" + lead } });
            Check(PlannerStore.DefaultLead == (lead == 0 ? ReminderLead.HourBefore : (ReminderLead)lead), "leads: the default lead " + lead + " comes back from a save");
        }
    }

    private static PlannerNote Series(PlannerRepeat repeat, int day, int start, ReminderLead lead)
    {
        PlannerNote note = PlannerStore.AddNote("Gym", day, start);
        note.Repeat = repeat;
        note.Lead = lead;
        return note;
    }

    // The days a note falls on in a look, as NoteSource walks them.
    private static List<int> Times(PlannerNote note, int from, int to)
    {
        var days = new List<int>();
        for (int day = PlannerStore.NextTime(note, from); day >= 0 && day <= to; day = PlannerStore.NextTime(note, day + 1))
        {
            bool done;
            if (PlannerStore.Falls(note, day, out done))
            {
                days.Add(day);
            }
        }
        return days;
    }

    private static void Repeats()
    {
        Check(PlannerRules.Occurs(PlannerRepeat.Once, 5, 5) && !PlannerRules.Occurs(PlannerRepeat.Once, 5, 6) && !PlannerRules.Occurs(PlannerRepeat.Daily, 5, 4)
              && PlannerRules.Occurs(PlannerRepeat.Daily, 5, 100) && PlannerRules.Occurs(PlannerRepeat.Weekly, 5, 12) && !PlannerRules.Occurs(PlannerRepeat.Weekly, 5, 11)
              && PlannerRules.Occurs(PlannerRepeat.Weekly, 5, 5),
            "repeats: once on its day, every day from its first, every week on its weekday");
        Check(PlannerRules.NextOccurrence(PlannerRepeat.Weekly, 5, 6) == 12 && PlannerRules.NextOccurrence(PlannerRepeat.Weekly, 5, 12) == 12 && PlannerRules.NextOccurrence(PlannerRepeat.Weekly, 5, 13) == 19
              && PlannerRules.NextOccurrence(PlannerRepeat.Daily, 5, 9) == 9 && PlannerRules.NextOccurrence(PlannerRepeat.Daily, 5, 2) == 5
              && PlannerRules.NextOccurrence(PlannerRepeat.Once, 5, 6) == -1 && PlannerRules.NextOccurrence(PlannerRepeat.Once, 5, 3) == 5,
            "repeats: the next time on or after a day");

        PlannerStore.Reset();
        PlannerStore.Load(null);
        PlannerNote once = PlannerStore.AddNote("Dentist", 9, 600);
        PlannerNote daily = Series(PlannerRepeat.Daily, 6, 540, ReminderLead.QuarterBefore);     // from a Saturday
        PlannerNote weekly = Series(PlannerRepeat.Weekly, 6, 1080, ReminderLead.None);           // every Saturday
        List<int> d = Times(daily, 5, 12), w = Times(weekly, 5, 20), o = Times(once, 5, 12);
        Check(d.Count == 7 && d[0] == 6 && d[1] == 7 && d[2] == 8 && d[6] == 12, "repeats: every day runs over the week's end, Sunday to Monday");
        Check(w.Count == 3 && w[0] == 6 && w[1] == 13 && w[2] == 20 && PlannerTime.Weekday(13) == PlannerTime.Weekday(6), "repeats: every week stays on its weekday over the week's end");
        Check(o.Count == 1 && o[0] == 9 && Times(once, 10, 12).Count == 0, "repeats: once falls on its day only");
        Check(PlannerStore.NoteEntry(once, 9).Equals(new EntryId("note", once.Id, 0)) && PlannerStore.NoteEntry(daily, 8).Equals(new EntryId("note", daily.Id, 8)),
            "repeats: one that is once keeps the id of a note (its old reminders still find it); each time of a series is its own");

        bool done;
        PlannerStore.SetDone(daily, 8, true);
        PlannerStore.Falls(daily, 8, out done);
        bool doneEight = done;
        PlannerStore.Falls(daily, 9, out done);
        Check(doneEight && !done, "repeats: one time is marked done, the others are not");
        PlannerStore.SetDone(daily, 8, false);
        PlannerStore.Falls(daily, 8, out done);
        Check(!done, "repeats: and marked not done again");

        // Its reminder is for the next time whose moment is still to come, and follows on after each.
        PlannerStore.Arm(daily, PlannerTime.At(6, 8));
        Check(PlannerStore.Reminders.Count == 1 && PlannerStore.Reminders[0].Entry.Equals(new EntryId("note", daily.Id, 6)) && PlannerStore.Reminders[0].FireAt == PlannerTime.At(6, 8, 45),
            "repeats: the series' reminder is for today's time, 15 minutes before");
        var due = new List<PlannerReminder>();
        PlannerStore.TakeDue(PlannerTime.At(6, 8, 45), due);
        PlannerStore.Arm(daily, PlannerTime.At(6, 8, 45));
        Check(due.Count == 1 && PlannerStore.Reminders.Count == 1 && PlannerStore.Reminders[0].Entry.B == 7, "repeats: once it has gone out, the next is for tomorrow's time");
        PlannerStore.SetDone(daily, 7, true);
        Check(PlannerStore.Reminders.Count == 0, "repeats: a time marked done loses its reminder");
        PlannerStore.Arm(daily, PlannerTime.At(6, 9));
        Check(PlannerStore.Reminders.Count == 1 && PlannerStore.Reminders[0].Entry.B == 8, "repeats: and the series goes on from the time after it");
        weekly.Lead = ReminderLead.EveningBefore;
        PlannerStore.Arm(weekly, PlannerTime.At(12, 19));
        int sat = PlannerStore.ReminderIndex(new EntryId("note", weekly.Id, 20));
        Check(PlannerStore.ReminderIndex(new EntryId("note", weekly.Id, 13)) < 0 && sat >= 0 && PlannerStore.Reminders[sat].FireAt == PlannerTime.At(19, 18),
            "repeats: a weekly series asked after Friday evening is reminded the Friday after, over the week's end");
        PlannerStore.Arm(once, PlannerTime.At(1, 0));
        Check(PlannerStore.ReminderIndex(new EntryId("note", once.Id, 0)) < 0, "repeats: one that is once is not armed as a series");
        weekly.Lead = ReminderLead.None;
        PlannerStore.Arm(weekly, PlannerTime.At(12, 19));
        Check(PlannerStore.ReminderIndex(new EntryId("note", weekly.Id, 20)) < 0, "repeats: a series with no reminder has none");
        int before = PlannerStore.Reminders.Count;
        PlannerStore.ArmAll(PlannerTime.At(6, 9));
        Check(PlannerStore.Reminders.Count == before, "repeats: arming them all again changes nothing (one reminder a series)");
    }

    private static void Deleting()
    {
        PlannerStore.Reset();
        PlannerStore.Load(null);
        PlannerNote daily = Series(PlannerRepeat.Daily, 6, 540, ReminderLead.HourBefore);
        PlannerNote other = PlannerStore.AddNote("Call", 8, 600);
        PlannerStore.Arm(daily, PlannerTime.At(6, 0));
        PlannerStore.SetReminder(new EntryId("note", daily.Id, 9), 9, 540, ReminderLead.LeaveBy, PlannerTime.At(6, 0), 60);
        PlannerStore.SetReminder(new EntryId("note", other.Id, 0), 8, 600, ReminderLead.AtTheTime, PlannerTime.At(6, 0));
        PlannerStore.SetReminder(new EntryId("work", daily.Id, 9), 9, 540, ReminderLead.AtTheTime, PlannerTime.At(6, 0));
        Check(PlannerStore.Reminders.Count == 4, "delete: a series, a time of it to leave for, another note and a shift of the same number have a reminder each");
        PlannerStore.DropReminders("note", daily.Id, true);
        Check(PlannerStore.Reminders.Count == 3 && PlannerStore.LeadOf(new EntryId("note", daily.Id, 9)) == ReminderLead.LeaveBy, "delete: arming again keeps the time to leave");
        Check(PlannerStore.RemoveNote(daily.Id) && PlannerStore.Note(daily.Id) == null, "delete: the series is taken away");
        Check(PlannerStore.Reminders.Count == 2 && PlannerStore.ReminderIndex(new EntryId("note", other.Id, 0)) >= 0 && PlannerStore.ReminderIndex(new EntryId("work", daily.Id, 9)) >= 0,
            "delete: with every reminder of it, and none of anybody else's");
        Check(PlannerStore.NextFire == Math.Min(PlannerTime.At(8, 10), PlannerTime.At(9, 9)), "delete: the soonest is the other note's");
        Check(!PlannerStore.RemoveNote(daily.Id), "delete: once only");
        var data = new Dictionary<string, string>();
        PlannerStore.Save(data);
        PlannerStore.Load(data);
        Check(PlannerStore.Notes.Count == 1 && PlannerStore.Note(other.Id) != null && PlannerStore.Reminders.Count == 2, "delete: and it stays gone after a save");
    }

    private static void Groups()
    {
        var seen = new bool[(int)PlannerGroup.Count];
        bool inRange = true;
        for (int c = 0; c < (int)PlannerCategory.Count; c++)
        {
            int g = (int)PlannerRules.GroupOf((PlannerCategory)c);
            inRange &= g >= 0 && g < (int)PlannerGroup.Count;
            if (g >= 0 && g < seen.Length)
            {
                seen[g] = true;
            }
        }
        Check(inRange && seen[0] && seen[1] && seen[2] && seen[3], "filter: every kind is in one of the four groups, and every group has a kind");
        Check(PlannerRules.GroupOf(PlannerCategory.Work) == PlannerGroup.Work && PlannerRules.GroupOf(PlannerCategory.Money) == PlannerGroup.Money
              && PlannerRules.GroupOf(PlannerCategory.Business) == PlannerGroup.Business && PlannerRules.GroupOf(PlannerCategory.Family) == PlannerGroup.Life
              && PlannerRules.GroupOf(PlannerCategory.Social) == PlannerGroup.Life && PlannerRules.GroupOf(PlannerCategory.Note) == PlannerGroup.Life
              && PlannerRules.GroupOf(PlannerCategory.Court) == PlannerGroup.Life,
            "filter: work and study, money, family and life, business");
    }

    private static void NewFields()
    {
        PlannerStore.Reset();
        PlannerStore.Load(null);
        PlannerNote weekly = Series(PlannerRepeat.Weekly, 6, 1080, ReminderLead.HalfHourBefore);
        weekly.DoneDay = 13;
        weekly.Street = "ba:street_a";
        weekly.Number = 12;
        weekly.Place = "12 Street A";
        PlannerNote once = PlannerStore.AddNote("Once", 9, -1);
        once.Lead = ReminderLead.AtTheTime;
        once.DoneDay = 4;   // means nothing for one that is once
        PlannerStore.SetReminder(new EntryId("note", once.Id, 0), 9, 600, ReminderLead.LeaveBy, 0, 168);
        var data = new Dictionary<string, string>();
        PlannerStore.Save(data);
        PlannerStore.Load(data);
        PlannerNote w = PlannerStore.Note(weekly.Id), o = PlannerStore.Note(once.Id);
        Check(w != null && w.Repeat == PlannerRepeat.Weekly && w.DoneDay == 13 && w.Lead == ReminderLead.HalfHourBefore && w.Street == "ba:street_a" && w.Number == 12,
            "save: a weekly entry comes back with its time marked done and its reminder");
        Check(o != null && o.Repeat == PlannerRepeat.Once && o.DoneDay == -1 && o.Lead == ReminderLead.AtTheTime, "save: one that is once keeps no time marked done");
        Check(PlannerStore.Reminders.Count == 1 && PlannerStore.Reminders[0].Lead == ReminderLead.LeaveBy && PlannerStore.Reminders[0].Travel == 168,
            "save: a reminder of when to leave comes back with its way");
        var again = new Dictionary<string, string>();
        PlannerStore.Save(again);
        Check(again[PlannerStore.NotesKey] == data[PlannerStore.NotesKey] && again[PlannerStore.RemindersKey] == data[PlannerStore.RemindersKey], "save: saving what was loaded writes the same lines");
    }

    private static void OldSaves()
    {
        // The note records and reminder records of saves from before repeats: eleven fields, five fields, six fields.
        var data = new Dictionary<string, string>
        {
            { PlannerStore.Key, "v=1;next=9;hide=0;lead=2;hello=1;seen=5" },
            { PlannerStore.NotesKey, "3|5|600|4|Old|Home|1|2|3|Main St|7;4|6|-1|1|Oldest" },
            { PlannerStore.RemindersKey, "note|3|0|100|200|2;note|4|0|100|300|6" }
        };
        PlannerStore.Load(data);
        PlannerNote a = PlannerStore.Note(3), b = PlannerStore.Note(4);
        Check(a != null && a.Repeat == PlannerRepeat.Once && a.DoneDay == -1 && a.Lead == ReminderLead.None && a.HasPlace && a.Street == "Main St" && a.Number == 7,
            "old save: a note of eleven fields loads as once, with no reminder of its own");
        Check(b != null && b.Repeat == PlannerRepeat.Once && b.Done && b.Title == "Oldest", "old save: a note of five fields loads as before");
        Check(PlannerStore.Reminders.Count == 2 && PlannerStore.Reminders[0].Travel == 0 && PlannerStore.Reminders[1].Lead == ReminderLead.LeaveBy && PlannerStore.Reminders[1].Travel == 0,
            "old save: reminders of six fields load with no way");
        Check(PlannerStore.NextFire == 200, "old save: the soonest reminder is known after a load");
        var odd = new Dictionary<string, string> { { PlannerStore.NotesKey, "5|5|600|0|Odd|||0|0||0|9|x|99;6|5|600|0|Odd2|||0|0||0|7|3|99" } };
        PlannerStore.Load(odd);
        PlannerNote c = PlannerStore.Note(5), e = PlannerStore.Note(6);
        Check(c != null && c.Repeat == PlannerRepeat.Once && c.Lead == ReminderLead.None && e != null && e.Repeat == PlannerRepeat.Once && e.Lead == ReminderLead.None,
            "old save: fields of repeats that cannot be read leave a note once, with no reminder");
        PlannerStore.Load(new Dictionary<string, string>());
        Check(PlannerStore.Loaded && PlannerStore.Notes.Count == 0 && PlannerStore.Reminders.Count == 0 && PlannerStore.NextFire == int.MaxValue, "old save: a save with no Planner at all loads with no entries");
    }

    private static void Week()
    {
        var list = new PlannerList();
        list.Begin(1, 20, false);
        PlannerEntry late = Add(list, "a", 1, 8, 1200, 1260);
        PlannerEntry before = Add(list, "a", 2, 5, 600, 660);   // the day before the week
        PlannerEntry early = Add(list, "a", 3, 8, 420, 480);
        PlannerEntry allDay = Add(list, "a", 4, 8, -1, -1, false);
        PlannerEntry sunday = Add(list, "a", 5, 7, 600, 660);
        PlannerEntry last = Add(list, "a", 6, 12, 60, 120);
        PlannerEntry after = Add(list, "a", 7, 13, 60, 120);   // the day after
        PlannerEntry noon = Add(list, "b", 8, 8, 720, 780);
        var grid = new WeekGrid();
        grid.Begin(6);
        bool rejected = !grid.Add(before) & !grid.Add(after) & !grid.Add(null);
        // given in no order at all (the filter's order, or any other): each column is put in time order
        grid.Add(late);
        grid.Add(noon);
        grid.Add(last);
        grid.Add(early);
        grid.Add(sunday);
        grid.Add(allDay);
        grid.Finish();
        Check(rejected && grid.Count == 6, "week: only the seven days from the first are taken");
        Check(grid.Columns[0].Count == 0 && grid.Columns[1].Count == 1 && grid.Columns[1][0] == sunday && grid.Columns[6].Count == 1 && grid.Columns[6][0] == last,
            "week: Saturday is empty, Sunday has its entry, the seventh day its own");
        List<PlannerEntry> monday = grid.Columns[2];
        Check(monday.Count == 4 && monday[0] == allDay && monday[1] == early && monday[2] == noon && monday[3] == late,
            "week: Monday, over the week's end, holds its entries all day first, then by the clock");
        Check(PlannerTime.Weekday(grid.FirstDay + 1) == 6 && PlannerTime.Weekday(grid.FirstDay + 2) == 0, "week: the columns run Saturday, Sunday, Monday from day 6");
        grid.Begin(6);
        Check(grid.Count == 0 && grid.Columns[2].Count == 0, "week: begun again, it is empty");
    }

    private static void Free()
    {
        var list = new PlannerList();
        list.Begin(1, 10, false);
        Add(list, "a", 1, 5, 540, 660);          // 09:00 to 11:00
        Add(list, "a", 2, 5, 840, 900);          // 14:00 to 15:00
        Add(list, "a", 3, 5, 720, 740);          // 12:00 to 12:20
        Add(list, "a", 4, 5, 1000, 1010);        // 16:40 to 16:50
        Add(list, "a", 5, 5, 600, 1200, false);  // a market all afternoon: the player need not be there
        PlannerEntry done = Add(list, "a", 6, 5, 480, 530);
        done.Done = true;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 0));
        int[] from = new int[PlannerRules.MostGaps], to = new int[PlannerRules.MostGaps];
        int total;
        int gaps = PlannerRules.FreeTime(list.Items, 5, 0, from, to, out total);
        Check(gaps == 3 && from[0] == 420 && to[0] == 540 && from[1] == 660 && to[1] == 720 && from[2] == 740 && to[2] == 840,
            "free time: from 07:00, between the entries the player has to be at: 07:00 to 09:00, 11:00 to 12:00, 12:20 to 14:00");
        Check(total == 120 + 60 + 100 + 100 + 370, "free time: all of it, the stretches not named as well (15:00 to 16:40, 16:50 to 23:00)");
        gaps = PlannerRules.FreeTime(list.Items, 5, 600, from, to, out total);
        Check(gaps == 3 && from[0] == 660 && total == 60 + 100 + 100 + 370, "free time: from now (10:00), inside an entry, starts when it ends");
        list.Begin(1, 10, false);
        Add(list, "a", 1, 4, 1320, 480);          // last night's shift ends at 08:00
        Add(list, "a", 2, 5, 500, 520);           // 08:20: twenty minutes after, no free time between
        PlannerEntry note = list.Add("note", 1, 0, PlannerCategory.Note, 5);
        note.Own = true;
        note.Start = 1260;                        // 21:00, one of the player's own: it is busy too
        PlannerLogic.Arrange(list, PlannerTime.At(4, 12));
        gaps = PlannerRules.FreeTime(list.Items, 5, 0, from, to, out total);
        Check(gaps == 2 && from[0] == 520 && to[0] == 1260 && from[1] == 1320 && to[1] == 1380, "free time: a night shift fills the morning, a gap under half an hour is none, the player's own entry counts");
        list.Begin(1, 10, false);
        Add(list, "a", 1, 5, 420, 600);           // 07:00 to 10:00
        Add(list, "a", 2, 5, 630, 1380);          // 10:30 to 23:00: exactly half an hour between
        PlannerLogic.Arrange(list, PlannerTime.At(5, 0));
        gaps = PlannerRules.FreeTime(list.Items, 5, 0, from, to, out total);
        Check(gaps == 1 && from[0] == 600 && to[0] == 630 && total == PlannerRules.FreeLeast, "free time: exactly half an hour between two entries is free time");
        list.Begin(1, 10, false);
        PlannerLogic.Arrange(list, PlannerTime.At(5, 0));
        gaps = PlannerRules.FreeTime(list.Items, 5, 1400, from, to, out total);
        Check(gaps == 0 && total == 0, "free time: after 23:00 there is none left to count");
        Check(PlannerRules.FreeTime(list.Items, 5, 0, from, to, out total) == 1 && total == 960, "free time: a day with nothing is free from 07:00 to 23:00");
    }

    private static void Owed()
    {
        var list = new PlannerList();
        list.Begin(1, 20, false);
        Add(list, "a", 1, 5, -1, -1, false).Owes = 100f;
        PlannerEntry paid = Add(list, "a", 2, 6, -1, -1, false);
        paid.Owes = 50f;
        paid.Done = true;
        Add(list, "a", 3, 8, -1, -1, false).Owes = 25f;
        Add(list, "a", 4, 12, -1, -1, false).Owes = 5.5f;
        Add(list, "a", 5, 13, -1, -1, false).Owes = 10f;
        Add(list, "a", 6, 9, -1, -1, false).Owes = 0f;
        PlannerLogic.Arrange(list, PlannerTime.At(5, 9));
        Check(Math.Abs(PlannerRules.OwedOn(list.Items, 5, 5) - 100f) < 0.001f, "money: what is due today");
        Check(Math.Abs(PlannerRules.OwedOn(list.Items, 6, 12) - 30.5f) < 0.001f, "money: what is due in the next seven days, less what is paid, not after them");
        Check(PlannerRules.OwedOn(list.Items, 14, 20) == 0f, "money: nothing due is nothing");
    }

    // ---------------------------------------------------------------- the texts

    // A language's words as the game shows them: a key German, French, Spanish or Russian lacks reads in English (ModLanguages).
    private static Dictionary<string, string> Words(string folder, string lang)
    {
        string path = Path.Combine(folder, lang + ".json");
        if (!File.Exists(path))
        {
            return null;
        }
        bool later = Array.IndexOf(ModLanguages.All, lang) >= ModLanguages.Required;
        Dictionary<string, string> english = later ? Words(folder, "en") : null;
        var words = english != null ? new Dictionary<string, string>(english) : new Dictionary<string, string>();
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
            {
                words[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : string.Empty;
            }
        }
        return words;
    }

    private static List<string> Slots(string words)
    {
        var slots = new List<string>();
        for (int at = words.IndexOf('{'); at >= 0; at = words.IndexOf('{', at + 1))
        {
            int end = words.IndexOf('}', at);
            if (end > at)
            {
                slots.Add(words.Substring(at, end - at + 1));
            }
        }
        slots.Sort(StringComparer.Ordinal);
        return slots;
    }

    private static readonly string[] NewKeys =
    {
        "reallife_planner_tab_week", "reallife_planner_filter_all", "reallife_planner_group_work", "reallife_planner_group_money", "reallife_planner_group_life",
        "reallife_planner_group_business", "reallife_planner_tag_court", "reallife_planner_tag_licence", "reallife_planner_span_minutes", "reallife_planner_span_hours",
        "reallife_planner_span_both", "reallife_planner_lead_quarter", "reallife_planner_lead_half", "reallife_planner_lead_leave", "reallife_planner_reminder_leave",
        "reallife_planner_reminder_leave_late", "reallife_planner_week_day", "reallife_planner_week_more", "reallife_planner_week_none", "reallife_planner_glance_first",
        "reallife_planner_glance_first_none", "reallife_planner_way_leave", "reallife_planner_way_now", "reallife_planner_glance_free", "reallife_planner_glance_free_none",
        "reallife_planner_glance_free_span", "reallife_planner_glance_clashes", "reallife_planner_glance_due_today", "reallife_planner_glance_due_day",
        "reallife_planner_glance_due_week", "reallife_planner_glance_planned", "reallife_planner_add_entry", "reallife_planner_detail_clash_reach", "reallife_planner_way_here",
        "reallife_planner_way_late", "reallife_planner_way_about_car", "reallife_planner_way_about_foot", "reallife_planner_repeat_once", "reallife_planner_repeat_daily",
        "reallife_planner_repeat_weekly", "reallife_planner_glance_hint_week", "reallife_guide_planner"
    };

    private static void Texts()
    {
        string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        string[] langs = ModLanguages.Present(folder);
        var words = new Dictionary<string, string>[langs.Length];
        var lost = new List<string>();
        for (int l = 0; l < langs.Length; l++)
        {
            words[l] = Words(folder, langs[l]);
            if (words[l] == null)
            {
                lost.Add(langs[l] + ".json");
            }
        }
        if (lost.Count > 0)
        {
            Check(false, "texts: every language's file is where the checks look for it (" + folder + "; not read: " + string.Join(", ", lost) + ")");
            return;
        }
        var wrong = new List<string>();
        foreach (string key in NewKeys)
        {
            string en;
            if (!words[0].TryGetValue(key, out en) || en.Length == 0)
            {
                wrong.Add("en " + key + " missing");
                continue;
            }
            string slots = string.Join(",", Slots(en));
            for (int l = 1; l < langs.Length; l++)
            {
                string w;
                if (!words[l].TryGetValue(key, out w) || w.Length == 0)
                {
                    wrong.Add(langs[l] + " " + key + " missing");
                }
                else if (string.Join(",", Slots(w)) != slots)
                {
                    wrong.Add(langs[l] + " " + key + " has other slots");
                }
            }
        }
        Check(wrong.Count == 0, "texts: every new text is in every language with the same slots (" + string.Join("; ", wrong) + ")");
        Check(words[0]["reallife_planner_reminder_leave_late"].Length + 60 + 40 <= 240 && words[0]["reallife_planner_reminder_leave"].Length + 60 + 40 <= 240,
            "texts: a reminder to leave stays within a notification's 240 characters with a long title and day");
        Check(words[0]["reallife_planner_glance_free_span"].Contains("{from}") && words[0]["reallife_planner_way_about_car"].Contains("{drive}") && words[0]["reallife_planner_way_about_car"].Contains("{walk}"),
            "texts: the free time and the way have their figures");
        // What's new has the Week tab in some row of en, pt and zh-cn (the release moves the next row to a numbered one); the
        // languages after them were translated later, with the rows as they were.
        string[] marks = { "Week tab", "aba Semana", "本周标签页" };
        bool told = true;
        for (int l = 0; l < marks.Length; l++)
        {
            bool found = false;
            foreach (KeyValuePair<string, string> pair in words[l])
            {
                found |= pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && pair.Value.Contains(marks[l]);
            }
            told &= found;
        }
        Check(told, "texts: What's new tells of the Week tab in en, pt and zh-cn");
        bool guide = true;
        for (int l = 0; l < langs.Length; l++)
        {
            string g = words[l]["reallife_guide_planner"];
            guide &= g.Contains("{planner_walk}") && g.Contains("{planner_drive}") && g.Contains("{planner_days}") && g.Contains("{planner_notes}") && g.Contains("{planner_evening}");
        }
        Check(guide, "texts: the Guide's Planner page fills the paces, the days, the most entries and the evening from the code in every language");
    }
}
