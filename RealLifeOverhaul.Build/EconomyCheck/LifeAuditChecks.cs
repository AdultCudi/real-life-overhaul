using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Planner;
using RealLifeOverhaul.Ui;

// The Planner and the Calendar on the kit, pressed through their own flows (PlannerFlow, CalendarFlow: what the screens run): the
// board holds every hour an entry stands in (the staff pay day at 23:00 was drawn nowhere), Jump to now chooses the entry, a note can
// be deleted (through the question), ticked off and edited in its title, day, hour, repeat and place, the settings show and hide
// kinds, and the Calendar opens on today with a header counted from the rows' own list, its weather in the game's unit, tomorrow
// from the Planner, the way back to today and each entry's app. Every target of every state is pressed and the state it should
// change is read back (a recording host stands for the game: an action that reaches the wrong handler is a failed line).
internal static partial class Program
{
    private sealed class PlanSpec
    {
        public string Source = "work";
        public int A;
        public PlannerCategory Category = PlannerCategory.Work;
        public int Day, Start = -1, End = -1;
        public bool Attend, Deadline;
        public string Title, Where, Street;
        public string What, IfMissed;
        public float Metres = -1f;
    }

    // The game, recorded: what was asked of it, and what the player would have answered.
    private sealed class PlanHost : IPlannerHost, ICalendarHost
    {
        public int Today = 40, MinuteNow = 8 * 60 + 45, Year = 360;
        public bool RemindersOn = true, Car = true, Imperial;
        public readonly List<PlanSpec> Specs = new List<PlanSpec>();
        public readonly List<CalSpec> CalSpecs = new List<CalSpec>();
        public readonly List<PlanSpec> Tomorrow = new List<PlanSpec>();
        public Action<string> AskedTitle;
        public Action DeleteYes;
        public PlannerEntry Located;
        public readonly List<string> Notices = new List<string>();
        public string OpenedApp;
        public bool PlannerOpened;
        public bool AppThere = true;
        public string HereStreet = "Fifth Avenue";
        public int Looks;
        private readonly PlannerList _list = new PlannerList();
        private readonly CalendarList _cal = new CalendarList();

        public int NowMinutes => PlannerTime.At(Today, MinuteNow / 60, MinuteNow % 60);
        int IPlannerHost.Today => Today;
        int IPlannerHost.Minute => MinuteNow;
        int ICalendarHost.Today => Today;
        public int Hour => MinuteNow / 60;
        public int YearDays => Year;
        public bool Reminders => RemindersOn;
        public bool ByCar => Car;

        public void Look(int fromDay, int toDay, List<PlannerEntry> into)
        {
            Looks++;
            Fill(Specs, fromDay, toDay, into);
        }

        private void Fill(List<PlanSpec> specs, int fromDay, int toDay, List<PlannerEntry> into)
        {
            _list.Begin(fromDay, toDay, true);
            foreach (PlanSpec s in specs)
            {
                PlannerEntry e = _list.Add(s.Source, s.A, 0, s.Category, s.Day);
                if (e == null)
                {
                    continue;
                }
                e.Start = s.Start;
                e.End = s.End;
                e.Attend = s.Attend;
                e.Deadline = s.Deadline;
                e.Title = s.Title;
                e.Where = s.Where;
                e.What = s.What;
                e.IfMissed = s.IfMissed;
                e.Street = s.Street;
                e.Number = s.Street != null ? 7 : 0;
                e.Placed = s.Metres >= 0f;
                e.PlaceX = s.Metres;
            }
            if (specs == Specs)
            {
                for (int i = 0; i < PlannerStore.Notes.Count; i++)
                {
                    PlannerNote note = PlannerStore.Notes[i];
                    for (int day = PlannerStore.NextTime(note, fromDay); day >= 0 && day <= toDay; day = PlannerStore.NextTime(note, day + 1))
                    {
                        if (!PlannerStore.Falls(note, day, out bool done))
                        {
                            continue;
                        }
                        EntryId id = PlannerStore.NoteEntry(note, day);
                        PlannerEntry e = _list.Add("note", id.A, id.B, PlannerCategory.Note, day);
                        if (e == null)
                        {
                            continue;
                        }
                        e.Own = true;
                        e.Done = done;
                        e.Start = note.Start;
                        e.Street = note.Street;
                        e.Number = note.Number;
                        e.Title = note.Title;
                        e.Where = note.Place;
                    }
                }
            }
            for (int i = _list.Items.Count - 1; i >= 0; i--)
            {
                if (!PlannerStore.Shows(_list.Items[i].Category))
                {
                    _list.Items.RemoveAt(i);
                }
            }
            PlannerLogic.Arrange(_list, NowMinutes, 0);
            into.AddRange(_list.Items);
        }

        public string Clock(int minuteOfDay)
        {
            int m = ((minuteOfDay % 1440) + 1440) % 1440;
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static readonly string[] Days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

        public string Weekday(int day) => Days[PlannerTime.Weekday(day)];
        public string DayWord(int day, int today) => day == today ? "Today" : day == today + 1 ? "Tomorrow" : Weekday(day) + " (Day " + day + ")";
        public string Span(int minutes) => minutes / 60 + " h " + minutes % 60 + " min";
        public string Money(float amount) => "$" + amount.ToString("0", CultureInfo.InvariantCulture);
        public void AskTitle(string initial, Action<string> done) => AskedTitle = done;
        public void ConfirmDelete(Action yes) => DeleteYes = yes;
        public void Locate(PlannerEntry entry) => Located = entry;

        public bool MetresTo(PlannerEntry entry, out float metres)
        {
            metres = entry.PlaceX;
            return entry.Placed;
        }

        public bool HerePlace(out string street, out int number, out string name)
        {
            street = HereStreet;
            number = 12;
            name = street == null ? null : "12 " + street;
            return street != null;
        }

        public void Notice(string key) => Notices.Add(key);
        public void Looked() { }

        // ---- the Calendar's side
        public void Look(int month, List<CalendarEntry> into)
        {
            int from = CalendarRules.MonthStart(month, Year), to = CalendarRules.MonthEnd(month, Year);
            _cal.Begin(from, to);
            foreach (CalSpec c in CalSpecs)
            {
                if (c.Day < from || c.Day > to)
                {
                    continue;
                }
                CalendarEntry e = _cal.Add(c.Source, c.A, 0, c.Group, c.Day);
                e.Title = c.Title;
                e.App = c.App;
                e.Done = c.Done;
                e.Value = c.Value;
            }
            into.AddRange(_cal.Items);
        }

        public void Plan(int day, List<PlannerEntry> into)
        {
            var one = new List<PlanSpec>();
            foreach (PlanSpec s in Tomorrow)
            {
                if (s.Day == day)
                {
                    one.Add(s);
                }
            }
            Fill(one, day, day, into);
        }

        public bool Weather(out string skyKey, out string skyEnglish, out int celsius, out bool imperial)
        {
            skyKey = "reallife_ui_hud_weather_sunny";
            skyEnglish = "Sunny";
            celsius = 18;
            imperial = Imperial;
            return true;
        }

        public string AppName(string appId) => AppThere ? "App " + appId : null;
        public bool OpenApp(string appId)
        {
            OpenedApp = AppThere ? appId : null;
            return AppThere;
        }

        public void OpenPlanner() => PlannerOpened = true;
    }

    private sealed class CalSpec
    {
        public string Source = "bills";
        public int A, Day;
        public CalendarGroup Group = CalendarGroup.Money;
        public string Title, App, Value;
        public bool Done;
    }

    private static int PlanNodes(UiScene s, out int[] commands, out int[] args, out bool[] off)
    {
        commands = new int[s.NodeCount];
        args = new int[s.NodeCount];
        off = new bool[s.NodeCount];
        for (int i = 0; i < s.NodeCount; i++)
        {
            commands[i] = s.Nodes[i].Command;
            args[i] = s.Nodes[i].Arg;
            off[i] = s.Nodes[i].Disabled;
        }
        return s.NodeCount;
    }

    private static UiScene PlanScene(PlannerFlow f, int width = 1920, int height = 1080)
    {
        if (f.Dirty)
        {
            f.Build();
        }
        var s = new UiScene();
        LifePages.Planner(s, f.Model, UiFrame.FullMenu(width / (float)height, "Planner"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static UiScene CalScene(CalendarFlow f)
    {
        if (f.Dirty)
        {
            f.Build();
        }
        var s = new UiScene();
        LifePages.Calendar(s, f.Model, UiFrame.FullMenu(16f / 9f, "Calendar"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static PlanSpec Plan(string title, int day, int start, int end, PlannerCategory cat = PlannerCategory.Work, bool attend = true, string source = "work", int a = 0)
    {
        return new PlanSpec { Title = title, Day = day, Start = start, End = end, Category = cat, Attend = attend, Source = source, A = a };
    }

    // A rig: the host with a Planner day and its flow, opened on a tab.
    private static PlannerFlow PlanRig(out PlanHost host, int tab = 0, Action<PlanHost> setup = null)
    {
        PlannerStore.Reset();
        host = new PlanHost();
        host.Specs.Add(Plan("Open the shop", host.Today, 7 * 60, 8 * 60, PlannerCategory.Work));
        host.Specs.Add(Plan("Coffee delivery", host.Today, 9 * 60 + 30, 10 * 60 + 30, PlannerCategory.Business, false, "delivery"));
        host.Specs.Add(Plan("Weekly meeting", host.Today, 10 * 60, 11 * 60, PlannerCategory.Work, true, "work", 1));
        host.Specs[1].Street = "Fifth Avenue";
        host.Specs[1].Metres = 2000f;
        host.Specs[1].Where = "Bloom and Co";
        host.Specs[1].What = "A delivery on one of your routes.";
        host.Specs[1].IfMissed = "It is sent back.";
        host.Specs.Add(Plan("Marketing class", host.Today + 1, 18 * 60, 20 * 60, PlannerCategory.Work, true, "study"));
        host.Specs.Add(Plan("Rent due", host.Today + 2, -1, -1, PlannerCategory.Money, false, "tax"));
        setup?.Invoke(host);
        var f = new PlannerFlow(host, UiWords("en"));
        for (int i = 0; i < tab; i++)
        {
            f.Tab(1);
        }
        return f;
    }

    private static string PlanModelLabel(UiActionModel[] actions, int i) => actions != null && i >= 0 && i < actions.Length ? actions[i].Label : null;

    private static void LifeAuditChecks()
    {
        PlannerEntryHours();
        PlannerJump();
        PlannerNotesPressed();
        PlannerDayBox();
        PlannerEveryTarget();
        CalendarOpensToday();
        CalendarEveryTarget();
    }

    // ---------------------------------------------------------------- the board's hours

    private static void PlannerEntryHours()
    {
        // The rule: 07:00 to 23:00 is the least; an entry outside it widens the board, never past the day.
        var rows = new List<PlannerEntry>
        {
            new PlannerEntry { Day = 40, Start = 5 * 60 + 30, End = 6 * 60 + 30, Title = "Early" },
            new PlannerEntry { Day = 40, Start = 23 * 60, End = 23 * 60 + 30, Title = "Staff pay day" },
        };
        PlannerFlow.Hours(rows, 40, false, -1, out int first, out int last);
        Check(first == 5 && last == 24, "life audit: the board's hours run from the earliest entry's hour to the end of the latest (05 to 24, got " + first + " to " + last + ")");
        PlannerFlow.Hours(new List<PlannerEntry>(), 40, false, -1, out first, out last);
        Check(first == 7 && last == 23, "life audit: a day with nothing early or late keeps 07:00 to 23:00");
        PlannerFlow.Hours(new List<PlannerEntry> { new PlannerEntry { Day = 40, Start = 0, End = 1440 } }, 40, false, -1, out first, out last);
        Check(first == 0 && last == 24, "life audit: a board never reaches past the day (0 to 24)");
        Check(PlannerFlow.Span(new PlannerEntry { Start = 600, End = 600 }, out int s0, out int e0) && e0 > s0, "life audit: an entry with no length is a bar of half an hour, not nothing");
        Check(PlannerFlow.Span(new PlannerEntry { Start = -1, End = 17 * 60, Deadline = true }, out int s1, out int e1) && e1 == 17 * 60 && s1 < e1, "life audit: a deadline is a bar ending at its last moment");
        Check(!PlannerFlow.Span(new PlannerEntry { Start = -1, End = -1 }, out _, out _), "life audit: an all day entry has no bar");

        // Through the flow and the layout: every bar of the day is drawn.
        PlannerFlow f = PlanRig(out PlanHost host, 0, h =>
        {
            h.Specs.Add(Plan("Open early", h.Today, 5 * 60 + 30, 6 * 60 + 30, PlannerCategory.Work, true, "work", 5));
            h.Specs.Add(Plan("Staff pay, 26 people", h.Today, 23 * 60, 23 * 60 + 30, PlannerCategory.Business, false, "business", 6));
            h.Specs.Add(Plan("Instant", h.Today, 12 * 60, 12 * 60, PlannerCategory.Family, false, "social", 7));
        });
        UiScene scene = PlanScene(f);
        UiBoardModel b = f.Model.Board;
        int bars = 0;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            bars += scene.Nodes[i].Command == UiBoard.ShiftCommand ? 1 : 0;
        }
        Check(b != null && b.Shifts.Length == 6 && bars == 6, "life audit: every entry with an hour has a bar on Today, the early one, the 23:00 pay day and an instant too (" + bars + " drawn of 6)");
        Check(b != null && b.FirstHour <= 5 && b.LastHour == 24 && b.Hours.Length == b.LastHour - b.FirstHour, "life audit: the board's ruler names each hour it spans");
        // Week: the same pay day on its lane.
        f = PlanRig(out host, 2, h => h.Specs.Add(Plan("Staff pay, week", h.Today + 3, 23 * 60, 23 * 60 + 30, PlannerCategory.Business, false, "business", 8)));
        scene = PlanScene(f);
        bars = 0;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            bars += scene.Nodes[i].Command == UiBoard.ShiftCommand ? 1 : 0;
        }
        Check(f.Model.Board != null && f.Model.Board.LastHour == 24 && bars == f.Model.Board.Shifts.Length && bars >= 4, "life audit: the week's board draws the 23:00 entry on its day (" + bars + " bars)");
    }

    // ---------------------------------------------------------------- Jump to now

    private static int ActionArg(UiScene s, string label, PlannerFlow f)
    {
        UiActionModel[] a = f.Model.Board != null ? f.Model.Board.Actions : new UiActionModel[0];
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Label == label)
            {
                return i;
            }
        }
        return -1;
    }

    private static void PlannerJump()
    {
        PlannerFlow f = PlanRig(out PlanHost host, 0);   // 08:45: "Open the shop" is over, the delivery and the meeting come
        UiScene s = PlanScene(f);
        int jump = ActionArg(s, "Jump to now", f);
        Check(jump >= 0 && !f.Model.Board.Actions[jump].Disabled && s.FindNode(UiBoard.ActionCommand, jump) >= 0, "life audit: Jump to now is drawn on Today and can be pressed");
        bool acted = f.Press(UiBoard.ActionCommand, jump);
        s = PlanScene(f);
        UiBoardModel b = f.Model.Board;
        Check(acted && b.Chosen >= 0 && b.Shifts[b.Chosen].Name == "Coffee delivery", "life audit: Jump to now chooses the next entry at a set hour (the delivery at 09:30, the earliest still to come; got "
            + (b.Chosen >= 0 ? b.Shifts[b.Chosen].Name : "none") + ")");
        Check(b.Chosen >= 0 && b.Offset >= 0f && b.Now == host.MinuteNow, "life audit: Jump to now keeps the now line and brings the chosen lane into view");
        // Away from the board's hours: now at 03:00, an entry at 05:00; the board reaches back to the hour now stands in.
        f = PlanRig(out host, 0, h =>
        {
            h.MinuteNow = 3 * 60;
            h.Specs.Add(Plan("Early shift", h.Today, 5 * 60, 6 * 60, PlannerCategory.Work, true, "work", 9));
        });
        s = PlanScene(f);
        Check(f.Model.Board.FirstHour > 3 && f.Model.Board.Now == 180, "life audit: before Jump the board starts after an early hour, so the now line stands out of it");
        jump = ActionArg(s, "Jump to now", f);
        Check(f.Press(UiBoard.ActionCommand, jump), "life audit: Jump to now at 03:00 finds the early shift");
        s = PlanScene(f);
        Check(f.Model.Board.FirstHour <= 3 && f.Model.Board.LastHour > 3, "life audit: Jump to now widens the board to the hour now stands in (from " + f.Model.Board.FirstHour + ")");
        // The key does the same.
        f = PlanRig(out host, 0);
        PlanScene(f);
        Check(f.Act(UiAction.Option) && f.Model.Board != null, "life audit: the Option key jumps to now too");
        // Nothing left today: the button is drawn disabled and the key does nothing.
        f = PlanRig(out host, 0, h => h.MinuteNow = 23 * 60 + 30);
        s = PlanScene(f);
        jump = ActionArg(s, "Jump to now", f);
        Check(jump >= 0 && f.Model.Board.Actions[jump].Disabled && !f.Act(UiAction.Option), "life audit: with nothing left today Jump to now is drawn disabled and the key does nothing");
    }

    // ---------------------------------------------------------------- notes: delete, done, edit, settings

    private static int FindAction(PlannerFlow f, string label)
    {
        UiActionModel[] a = f.Model.Entry != null ? f.Model.Entry.Actions : new UiActionModel[0];
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].Label == label)
            {
                return i;
            }
        }
        return -1;
    }

    private static void PlannerNotesPressed()
    {
        PlannerFlow f = PlanRig(out PlanHost host, 3);
        PlannerNote note = PlannerStore.AddNote("Ask Maria about Sunday shifts", host.Today, 9 * 60);
        PlannerNote weekly = PlannerStore.AddNote("Gym", host.Today + 1, 18 * 60);
        weekly.Repeat = PlannerRepeat.Weekly;
        PlanScene(f);
        Check(f.Model.RowCount == 2, "life audit: the Notes tab lists the notes (2)");
        Check(f.Press(LifePages.Row, 0) && f.Page == UiPlanPageKind.Entry, "life audit: a note's row opens its page");
        PlanScene(f);
        string[] labels = new string[f.Model.Entry.Actions.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i] = f.Model.Entry.Actions[i].Label;
        }
        Check(Array.IndexOf(labels, "Mark as done") >= 0 && Array.IndexOf(labels, "Edit note") >= 0 && Array.IndexOf(labels, "Delete") >= 0, "life audit: a note's page offers Done, Edit and Delete (" + string.Join(", ", labels) + ")");

        // Done
        int done = FindAction(f, "Mark as done");
        Check(f.Press(UiPlanEntry.ActionCommand, done) && note.Done, "life audit: Mark as done ticks the note off in the store");
        PlanScene(f);
        Check(f.Model.Entry.Status == "Done" && FindAction(f, "Mark as not done") >= 0, "life audit: a done note says Done and offers Mark as not done");
        Check(f.Press(UiPlanEntry.ActionCommand, FindAction(f, "Mark as not done")) && !note.Done, "life audit: Mark as not done puts it back");

        // Delete: through the question, and only on yes.
        int del = FindAction(f, "Delete");
        Check(f.Press(UiPlanEntry.ActionCommand, del) && host.DeleteYes != null && PlannerStore.Notes.Count == 2, "life audit: Delete asks the question and keeps the note until it is answered");
        host.DeleteYes();
        PlanScene(f);
        Check(PlannerStore.Notes.Count == 1 && PlannerStore.Note(note.Id) == null && f.Page != UiPlanPageKind.Entry, "life audit: Delete on yes removes the note and leaves its page");

        // Edit: title, day, hour, repeat and place all change, in the same note.
        f = PlanRig(out host, 3);
        note = PlannerStore.AddNote("Pick up the keys", host.Today, 9 * 60);
        int count = PlannerStore.Notes.Count;
        PlanScene(f);
        f.Press(LifePages.Row, 0);
        PlanScene(f);
        Check(f.Press(UiPlanEntry.ActionCommand, FindAction(f, "Edit note")) && f.Page == UiPlanPageKind.Form && f.DraftTitle == "Pick up the keys", "life audit: Edit note opens the form with the note's own words");
        PlanScene(f);
        Check(f.Press(UiPlanForm.FieldCommand, 0) && host.AskedTitle != null, "life audit: the title field asks the text box");
        host.AskedTitle("Collect the shop keys");
        Check(f.DraftTitle == "Collect the shop keys", "life audit: the text box's words become the title");
        f.Press(UiPlanForm.MoreCommand, 2);    // the day after
        f.Press(UiPlanForm.MoreCommand, 3);    // an hour on
        f.Press(UiPlanForm.MoreCommand, 3);
        f.Press(UiPlanForm.OptionCommand, UiPlanForm.OptionArg(4, 1));   // every day
        f.Press(UiPlanForm.PlaceCommand, 5 * 2);                          // this building
        f.Press(UiPlanForm.MoreCommand, 6);    // a reminder
        PlanScene(f);
        Check(f.Press(UiPlanForm.ActionCommand, 0), "life audit: Save acts");
        PlannerNote saved = PlannerStore.Note(note.Id);
        Check(PlannerStore.Notes.Count == count && saved != null && saved.Title == "Collect the shop keys" && saved.Day == host.Today + 1 && saved.Start == 11 * 60
              && saved.Repeat == PlannerRepeat.Daily && saved.Street == "Fifth Avenue" && saved.Place == "12 Fifth Avenue",
            "life audit: an edit changes the title, day, hour, repeat and place of the same note, not a new one (" + (saved == null ? "gone" : saved.Title + ", day " + saved.Day + ", " + saved.Start + ", " + saved.Repeat + ", " + saved.Street) + ")");
        Check(saved != null && saved.Lead != ReminderLead.HourBefore && saved.Lead != ReminderLead.None, "life audit: the edit's reminder is the one stepped to (" + (saved != null ? saved.Lead.ToString() : "-") + ")");
        Check(f.Page == UiPlanPageKind.Entry, "life audit: Save leaves the form for the note's page");

        // A new note: Save is disabled with no title, the Add button opens the form, Save adds it.
        f = PlanRig(out host, 3);
        PlanScene(f);
        Check(f.Press(LifePages.Add, 0) && f.Page == UiPlanPageKind.Form && f.DraftTitle.Length == 0, "life audit: Add an entry opens an empty form on Notes");
        UiScene s = PlanScene(f);
        Check(f.Model.Form.Actions[0].Disabled && !f.Press(UiPlanForm.ActionCommand, 0) && PlannerStore.Notes.Count == 0, "life audit: Save is disabled until the note has a title");
        f.Press(UiPlanForm.OptionCommand, UiPlanForm.OptionArg(1, 3));   // a ready title
        PlanScene(f);
        Check(f.DraftTitle == "Pay a bill" && f.Press(UiPlanForm.ActionCommand, 0) && PlannerStore.Notes.Count == 1 && PlannerStore.Notes[0].Title == "Pay a bill", "life audit: a ready title and Save add a note");
        Check(f.Press(UiPlanPage.BackCommand, 0) && f.Page == UiPlanPageKind.List, "life audit: Back from a note's page returns to the list");

        // Settings
        f = PlanRig(out host, 0);
        PlanScene(f);
        Check(f.Press(UiPlanPage.SettingsCommand, 0) && f.Page == UiPlanPageKind.Form && f.Settings, "life audit: the gear opens the settings");
        PlanScene(f);
        Check(PlannerStore.Shows(PlannerCategory.Note) && f.Press(UiPlanForm.FieldCommand, 8) && !PlannerStore.Shows(PlannerCategory.Note), "life audit: a switch hides a kind in the store (my notes)");
        Check(f.Press(UiPlanForm.FieldCommand, 8) && PlannerStore.Shows(PlannerCategory.Note), "life audit: and shows it again");
        ReminderLead was = PlannerStore.DefaultLead;
        Check(f.Press(UiPlanForm.LessCommand, 10) && PlannerStore.DefaultLead != was, "life audit: the default reminder steps");
        Check(f.Back() && f.Page == UiPlanPageKind.List, "life audit: Back closes the settings");
        Check(!f.Back(), "life audit: Back at the list is left to the game (closes the app)");
    }

    // ---------------------------------------------------------------- the day box and the week

    private static void PlannerDayBox()
    {
        PlannerFlow f = PlanRig(out PlanHost host, 0);
        PlanScene(f);
        UiPlanDayModel side = f.Model.Side;
        Check(side.Next.Length == 2 && side.Next[0].Title == "Coffee delivery" && side.Next[0].Locate && side.Next[1].Title == "Weekly meeting", "life audit: up next is the entries still to come, in time order, a place one with its link");
        Check(side.Free.Length > 0 && side.Free[0].Span.Contains("to"), "life audit: free time is listed as stretches");
        Check(side.Later.Length >= 1 && side.Later[0].Title.Contains("Marketing class"), "life audit: later this week names the days after with their first things");
        Check(side.Totals.Length >= 1 && side.Totals[0].Value == "2", "life audit: the day's count of what is to do (2: the shop's opening is missed)");
        // A clash shows its line.
        f = PlanRig(out host, 0, h =>
        {
            h.Specs.Add(Plan("Gym class", h.Today, 9 * 60 + 45, 10 * 60 + 30, PlannerCategory.Family, true, "social", 11));
            h.Specs.Add(Plan("Interview", h.Today, 10 * 60 + 15, 11 * 60, PlannerCategory.Work, true, "work", 12));
        });
        PlanScene(f);
        Check(f.Model.Side.ClashLine != null, "life audit: a clash is said in the day box");
        // Week lanes open a day; the day has its own box and rows.
        f = PlanRig(out host, 2);
        PlanScene(f);
        Check(f.Press(UiBoard.LaneCommand, 1) && f.Page == UiPlanPageKind.Day && f.DayShown == host.Today + 1, "life audit: a lane of the week opens its day");
        PlanScene(f);
        Check(f.Model.RowCount == 1 && f.Model.Side != null && f.Model.DayTitle == "Tomorrow", "life audit: the day's page has its entries and its box");
        Check(f.Press(LifePages.Row, 0) && f.Page == UiPlanPageKind.Entry && f.Press(UiPlanPage.BackCommand, 0) && f.Page == UiPlanPageKind.Day, "life audit: an entry of the day opens and Back returns to the day");
        Check(f.Back() && f.Page == UiPlanPageKind.List, "life audit: Back from the day returns to the week");
        // Locate through a row.
        f = PlanRig(out host, 1);
        PlanScene(f);
        int loc = -1;
        for (int i = 0; i < f.Model.RowCount; i++)
        {
            loc = f.Model.Rows[i].Locate ? i : loc;
        }
        Check(loc < 0 || (f.Press(LifePages.Locate, loc) && host.Located != null), "life audit: Locate on a row sets the destination");
    }

    // ---------------------------------------------------------------- every target pressed, its effect read back

    private static string PlanState(PlannerFlow f, PlanHost h)
    {
        var text = new System.Text.StringBuilder();
        text.Append(f.CurrentTab).Append('|').Append(f.Chip).Append('|').Append(f.Page).Append('|').Append(f.DayShown).Append('|').Append(f.Selected.Source).Append(f.Selected.A).Append('|')
            .Append(f.Settings).Append('|').Append(PlannerStore.HiddenCategories).Append('|').Append(PlannerStore.DefaultLead).Append('|').Append(PlannerStore.Notes.Count).Append('|')
            .Append(f.DraftTitle).Append(f.DraftPlace).Append(f.DraftDay).Append(f.DraftStart).Append(f.DraftRepeat).Append(f.DraftLead).Append('|')
            .Append(h.AskedTitle != null).Append(h.DeleteYes != null).Append(h.Located != null).Append(h.Notices.Count);
        foreach (PlannerNote n in PlannerStore.Notes)
        {
            text.Append('|').Append(n.Title).Append(n.Day).Append(n.Start).Append(n.Done).Append(n.DoneDay).Append(n.Repeat).Append(n.Lead);
        }
        if (f.Model.Board != null)
        {
            text.Append("|b").Append(f.Model.Board.Chosen).Append(f.Model.Board.FirstHour);
        }
        if (f.Model.Entry != null)
        {
            text.Append("|e").Append(f.Model.Entry.ReminderValue).Append(f.Model.Entry.Status);
        }
        return text.ToString();
    }

    private static void PlannerEveryTarget()
    {
        var states = new List<KeyValuePair<string, Func<PlannerFlow>>>();
        PlanHost[] hosts = new PlanHost[1];
        Func<int, Action<PlanHost>, Func<PlannerFlow>> tab = (t, extra) => () =>
        {
            PlannerFlow x = PlanRig(out hosts[0], t, h => extra?.Invoke(h));
            return x;
        };
        Action<PlanHost> notes = h =>
        {
            PlannerNote a = PlannerStore.AddNote("Pick up the keys", h.Today, 9 * 60);
            a.Street = "Fifth Avenue";
            a.Place = "7 Fifth Avenue";
            PlannerNote b = PlannerStore.AddNote("Gym", h.Today + 1, -1);
            b.Repeat = PlannerRepeat.Weekly;
        };
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("today", tab(0, notes)));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("upcoming", tab(1, notes)));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("week", tab(2, notes)));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("notes", tab(3, notes)));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("a day", () =>
        {
            PlannerFlow x = tab(2, notes)();
            PlanScene(x);
            x.Press(UiBoard.LaneCommand, 0);
            return x;
        }));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("a note's page", () =>
        {
            PlannerFlow x = tab(3, notes)();
            PlanScene(x);
            x.Press(LifePages.Row, 0);
            return x;
        }));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("an entry's page", () =>
        {
            PlannerFlow x = tab(1, notes)();
            PlanScene(x);
            x.Press(LifePages.Row, 0);
            return x;
        }));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("the note form", () =>
        {
            PlannerFlow x = tab(3, notes)();
            PlanScene(x);
            x.Press(LifePages.Add, 0);
            return x;
        }));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("the edit form", () =>
        {
            PlannerFlow x = tab(3, notes)();
            PlanScene(x);
            x.Press(LifePages.Row, 0);
            PlanScene(x);
            x.Press(UiPlanEntry.ActionCommand, FindAction(x, "Edit note"));
            return x;
        }));
        states.Add(new KeyValuePair<string, Func<PlannerFlow>>("the settings", () =>
        {
            PlannerFlow x = tab(0, notes)();
            PlanScene(x);
            x.Press(UiPlanPage.SettingsCommand, 0);
            return x;
        }));
        foreach (var state in states)
        {
            PlannerFlow probe = state.Value();
            UiScene scene = PlanScene(probe);
            PlanNodes(scene, out int[] cmd, out int[] arg, out bool[] off);
            int pressed = 0, dead = 0, wrong = 0;
            string deadWhere = null;
            for (int n = 0; n < cmd.Length; n++)
            {
                if (off[n] || cmd[n] == UiDraw.CloseCommand)
                {
                    continue;
                }
                PlannerFlow f = state.Value();
                PlanHost h = hosts[0];
                UiScene before = PlanScene(f);
                string label = NodeLabel(f, cmd[n], arg[n]);
                int tabNow = f.CurrentTab, chipNow = f.Chip, pageNow = f.Page;
                string was = PlanState(f, h);
                int repeatWas = (int)f.DraftRepeat;
                bool acted = f.Press(cmd[n], arg[n]);
                if (f.Dirty)
                {
                    f.Build();   // the next layout reads the state the press left
                }
                bool same = cmd[n] == UiPlanForm.OptionCommand && UiPlanForm.OptionField(arg[n]) == 4 && UiPlanForm.OptionIndex(arg[n]) == repeatWas && pageNow == UiPlanPageKind.Form || cmd[n] == UiAppWindow.NavCommand && arg[n] == tabNow && pageNow == UiPlanPageKind.List
                            || cmd[n] == LifePages.Chip && arg[n] == chipNow;
                if (same)
                {
                    continue;   // the tab or the filter already shown
                }
                pressed++;
                if (!acted || PlanState(f, h) == was && !PlanReadOnlyEffect(cmd[n], label))
                {
                    dead++;
                    deadWhere = deadWhere ?? state.Key + ": command " + cmd[n] + " arg " + arg[n] + (label != null ? " (" + label + ")" : string.Empty);
                    continue;
                }
                if (!PlanEffectIsRight(f, h, cmd[n], arg[n], label, tabNow, chipNow, pageNow))
                {
                    wrong++;
                    deadWhere = deadWhere ?? state.Key + ": command " + cmd[n] + " arg " + arg[n] + (label != null ? " (" + label + ")" : string.Empty) + " reached the wrong handler";
                }
            }
            Check(dead == 0 && wrong == 0 && pressed > 0, "life audit: the Planner's " + state.Key + ": every target acts, as the kind it was drawn (" + pressed + " pressed" + (deadWhere != null ? "; " + deadWhere : string.Empty) + ")");
        }
        // The recording itself: an action that reached the wrong handler is found (Delete drawn, but Done read back; Back drawn, but the page unmoved).
        PlannerFlow mis = PlanRig(out PlanHost rec, 3);
        PlannerStore.AddNote("Pick up the keys", rec.Today, 9 * 60);
        PlanScene(mis);
        mis.Press(LifePages.Row, 0);
        PlanScene(mis);
        mis.Press(UiPlanEntry.ActionCommand, FindAction(mis, "Delete"));
        Check(rec.DeleteYes != null && !PlanEffectIsRight(mis, rec, UiPlanEntry.ActionCommand, 0, "Mark as done", 3, 0, UiPlanPageKind.Entry)
              && !PlanEffectIsRight(mis, rec, UiPlanPage.BackCommand, 0, null, 3, 0, UiPlanPageKind.Entry),
            "life audit: the recording host fails an action that reached another kind (Delete is not Done, and Back that did not move the page is no Back)");
        // A command of another page reaches nothing.
        PlannerFlow g = PlanRig(out PlanHost host, 0);
        PlanScene(g);
        string idle = PlanState(g, host);
        bool none = !g.Press(UiPlanForm.ActionCommand, 0) && !g.Press(UiPlanEntry.ActionCommand, 0) && !g.Press(UiPlanForm.FieldCommand, 0) && !g.Press(UiPlanForm.MoreCommand, 3)
                    && !g.Press(UiDiary.DayCommand, 3) && !g.Press(UiDiary.MonthCommand, 1) && !g.Press(UiPlanEntry.MoreCommand, 0);
        Check(none && PlanState(g, host) == idle, "life audit: the Planner's list ignores the form's, the entry's and the diary's commands");
        PlannerFlow form = PlanRig(out host, 3);
        PlanScene(form);
        form.Press(LifePages.Add, 0);
        PlanScene(form);
        idle = PlanState(form, host);
        none = !form.Press(UiBoard.ActionCommand, 0) && !form.Press(LifePages.Row, 0) && !form.Press(UiBoard.ShiftCommand, 0) && !form.Press(LifePages.Chip, 2) && !form.Press(UiPlanEntry.ActionCommand, 0);
        Check(none && PlanState(form, host) == idle, "life audit: the note form ignores the board's, the list's and the entry page's commands");
    }

    // The words of the button a target was drawn as, where it is one of an entry's actions.
    private static string NodeLabel(PlannerFlow f, int command, int arg)
    {
        if (command == UiPlanEntry.ActionCommand)
        {
            return PlanModelLabel(f.Model.Entry != null ? f.Model.Entry.Actions : null, arg);
        }
        if (command == UiPlanForm.ActionCommand)
        {
            return PlanModelLabel(f.Model.Form != null ? f.Model.Form.Actions : null, arg);
        }
        if (command == UiBoard.ActionCommand)
        {
            return PlanModelLabel(f.Model.Board != null ? f.Model.Board.Actions : null, arg);
        }
        return null;
    }

    // Presses that read as an effect on the host alone (the text box asked, the question asked, the destination set).
    private static bool PlanReadOnlyEffect(int command, string label) => false;

    // What each kind of target must have done, read back from the flow, the store and the host.
    private static bool PlanEffectIsRight(PlannerFlow f, PlanHost h, int command, int arg, string label, int tabWas, int chipWas, int pageWas)
    {
        switch (command)
        {
            case UiAppWindow.NavCommand:
                return f.CurrentTab == arg && f.Page == UiPlanPageKind.List;
            case LifePages.Chip:
                return f.Chip == arg;
            case LifePages.Row:
            case UiBoard.ShiftCommand:
            case UiPlanDay.RowCommand:
                return f.Page == UiPlanPageKind.Entry && f.Selected.Source != null;
            case LifePages.Locate:
            case UiPlanDay.LocateCommand:
                return h.Located != null;
            case UiPlanDay.LaterCommand:
                return f.Page == UiPlanPageKind.Day;
            case UiBoard.LaneCommand:
            case UiBoard.DayCommand:
                return tabWas == PlannerFlow.TabWeek ? f.Page == UiPlanPageKind.Day : f.Chip != chipWas;
            case LifePages.Add:
                return f.Page == UiPlanPageKind.Form && !f.Settings && f.DraftTitle.Length == 0;
            case UiPlanPage.SettingsCommand:
                return f.Page == UiPlanPageKind.Form && f.Settings;
            case UiPlanPage.BackCommand:
                return f.Page != pageWas;
            case UiBoard.ActionCommand:
                return label == "Jump to now" ? f.Model.Board.Chosen >= 0 : label == "Add an entry" && f.Page == UiPlanPageKind.Form;
            case UiPlanEntry.ActionCommand:
                switch (label)
                {
                    case "Set destination": return h.Located != null;
                    case "Mark as done": return PlannerStore.Notes.Exists(n => n.Done || n.DoneDay >= 0);
                    case "Mark as not done": return PlannerStore.Notes.TrueForAll(n => !n.Done && n.DoneDay < 0);
                    case "Edit note": return f.Page == UiPlanPageKind.Form && f.DraftTitle.Length > 0;
                    case "Delete": return h.DeleteYes != null;
                    default: return false;
                }
            case UiPlanEntry.LessCommand:
            case UiPlanEntry.MoreCommand:
                return true;
            case UiPlanForm.FieldCommand:
                return f.Settings ? true : h.AskedTitle != null;
            case UiPlanForm.OptionCommand:
                return UiPlanForm.OptionField(arg) == 1 ? f.DraftTitle.Length > 0 : f.DraftRepeat == (PlannerRepeat)UiPlanForm.OptionIndex(arg);
            case UiPlanForm.LessCommand:
            case UiPlanForm.MoreCommand:
                return true;
            case UiPlanForm.PlaceCommand:
                return true;
            case UiPlanForm.ActionCommand:
                if (f.Settings || label == "Close")
                {
                    return f.Page != UiPlanPageKind.Form;
                }
                return label == "Save" ? f.Page == UiPlanPageKind.Entry : f.Page != UiPlanPageKind.Form;
            default:
                return false;
        }
    }

    // ---------------------------------------------------------------- the Calendar

    private static CalendarFlow CalRig(out PlanHost host, int today = 40, Action<PlanHost> setup = null)
    {
        host = new PlanHost { Today = today };
        setup?.Invoke(host);
        return new CalendarFlow(host, UiWords("en"));
    }

    private static void CalendarOpensToday()
    {
        foreach (int today in new[] { 1, 40, 75, 360, 361 })
        {
            CalendarFlow f = CalRig(out PlanHost host, today);
            f.Build();
            int from = CalendarRules.MonthStart(CalendarRules.MonthOf(today, 360), 360);
            Check(f.Model.Diary.Chosen == today - from + 1 && f.Model.Diary.Today == today - from + 1 && f.ChosenDay == today && f.Month == CalendarRules.MonthOf(today, 360),
                "life audit: the Calendar opens on today, day " + today + " (chosen " + f.Model.Diary.Chosen + ", today's cell " + f.Model.Diary.Today + ")");
            Check(f.Model.Weather != null && f.Model.Today == null, "life audit: opened on today it has today's weather and no way back to offer (day " + today + ")");
        }
        // Today's weather, tomorrow and the header from the rows' own list.
        CalendarFlow c = CalRig(out PlanHost h, 40, x =>
        {
            x.CalSpecs.Add(new CalSpec { Day = 40, Title = "Rent", A = 1, App = "RealLifeFinance", Value = "$1,200" });
            x.CalSpecs.Add(new CalSpec { Day = 40, Title = "Tax", A = 2, Done = true });
            x.CalSpecs.Add(new CalSpec { Day = 40, Title = "Call", A = 3, Source = "planner:note", Group = CalendarGroup.Time, Done = true });
            x.CalSpecs.Add(new CalSpec { Day = 41, Title = "Not today", A = 4 });
            x.CalSpecs.Add(new CalSpec { Day = 45, Title = "Later in the month", A = 5 });
            x.Tomorrow.Add(Plan("Bloom delivery", 41, 9 * 60, 10 * 60, PlannerCategory.Business, false, "delivery"));
            x.Tomorrow.Add(Plan("Marketing class", 41, 18 * 60, 20 * 60, PlannerCategory.Work, true, "study", 2));
        });
        c.Build();
        Check(c.DayCount == 3 && c.DayDone == 2 && c.Model.Diary.ChosenTitle == "3 entries, 2 done", "life audit: the day's header counts the rows' own list: 3 entries, 2 done (" + c.Model.Diary.ChosenTitle + ")");
        bool never = true;
        for (int d = 1; d <= 30; d++)
        {
            CalendarFlow k = CalRig(out PlanHost hh, 40, x =>
            {
                for (int i = 0; i < d % 7 + 1; i++)
                {
                    x.CalSpecs.Add(new CalSpec { Day = 40, Title = "E" + i, A = i, Done = (i + d) % 3 == 0 });
                }
            });
            k.Build();
            never &= k.DayDone <= k.DayCount;
        }
        Check(never, "life audit: the header never counts more done than entries");
        Check(c.Model.Diary.Entries.Length == 5, "life audit: the month's grid holds every day's entries, not the chosen day's alone (" + c.Model.Diary.Entries.Length + " of 5)");
        Check(c.Model.Diary.Ahead != null && c.Model.Diary.Ahead.Length == 2 && c.Model.Diary.Ahead[0].Title == "Bloom delivery" && c.Model.Diary.AheadTitle == "Tomorrow",
            "life audit: tomorrow comes from the Planner (2 entries)");
        // The last day of a month: tomorrow is the next month's first day, pressing it goes there.
        int last = CalendarRules.MonthEnd(CalendarRules.MonthOf(40, 360), 360);
        c = CalRig(out h, last, x => x.Tomorrow.Add(Plan("First of the month", last + 1, 9 * 60, 10 * 60, PlannerCategory.Work, true, "work", 3)));
        c.Build();
        Check(c.Model.Diary.Ahead.Length == 1 && c.Model.Diary.Ahead[0].Day == c.Model.Diary.Length + 1, "life audit: on a month's last day tomorrow is the next month's first day, not empty");
        int month = c.Month;
        Check(c.Press(UiDiary.DayCommand, c.Model.Diary.Ahead[0].Day) && c.Month == month + 1 && c.Chosen == 1, "life audit: pressing tomorrow of a month's last day opens the next month on its first day");
        // The unit.
        c = CalRig(out h, 40);
        c.Build();
        Check(c.Model.Weather.Contains("°C") && !c.Model.Weather.Contains("°F"), "life audit: the weather says °C where the game counts in metric (" + c.Model.Weather + ")");
        c = CalRig(out h, 40, x => x.Imperial = true);
        c.Build();
        Check(c.Model.Weather.Contains("64°F"), "life audit: the weather says °F where the game counts in imperial units (" + c.Model.Weather + ")");
        // Only today carries it, and the way back to today appears with the other day.
        c = CalRig(out h, 40);
        c.Build();
        Check(c.Press(UiDiary.DayCommand, c.Chosen + 2), "life audit: a day is chosen");
        c.Build();
        Check(c.Model.Weather == null && c.Model.Today != null && c.Model.Diary.AheadTitle != "Tomorrow", "life audit: another day has no weather, offers Today and calls the next day by its own name");
        Check(c.Press(LifePages.Today, 0) && c.ChosenDay == 40 && c.Model.Weather == null, "life audit: Today brings the chosen day back");
        c.Build();
        Check(c.Model.Weather != null && c.Model.Today == null && !c.Press(LifePages.Today, 0), "life audit: back on today the button is gone and a press does nothing");
        Check(c.Press(UiDiary.MonthCommand, 1) && c.Month == CalendarRules.MonthOf(40, 360) + 1 && c.Press(LifePages.Today, 0) && c.Month == CalendarRules.MonthOf(40, 360), "life audit: Today returns from another month");
        Check(!CalRig(out h, 1).Press(UiDiary.MonthCommand, -1), "life audit: the first month has nothing before it");
        // Each entry's app.
        c = CalRig(out h, 40, x =>
        {
            x.CalSpecs.Add(new CalSpec { Day = 40, Title = "Rent", A = 1, App = "RealLifeFinance" });
            x.CalSpecs.Add(new CalSpec { Day = 40, Title = "Your birthday", A = 2, Group = CalendarGroup.Time });
        });
        c.Build();
        UiDiaryEntry[] en = c.Model.Diary.Entries;
        Check(en.Length == 2 && en[0].Action == "Open App RealLifeFinance" && en[1].Action == null, "life audit: an entry with an app says which to open, one without says nothing");
        Check(c.Press(UiDiary.EntryCommand, 0) && h.OpenedApp == "RealLifeFinance", "life audit: Open the entry's app opens it");
        Check(!c.Press(UiDiary.EntryCommand, 1) && !c.Press(UiDiary.EntryCommand, 9), "life audit: an entry with no app has nothing to open");
        h.AppThere = false;
        c.Build();
        Check(c.Model.Diary.Entries[0].Action == null, "life audit: an app not on the phone offers no link");
        // Add and Open Planner.
        Check(c.Press(LifePages.Add, 0) && h.AskedTitle != null, "life audit: Add an entry asks the text box");
        PlannerStore.Reset();
        h.AskedTitle("Pay the rent");
        Check(PlannerStore.Notes.Count == 1 && PlannerStore.Notes[0].Day == 40 && PlannerStore.Notes[0].Start == 9 * 60, "life audit: the words become a note on the chosen day at 09:00");
        Check(c.Press(LifePages.Second, 0) && h.PlannerOpened, "life audit: Open Planner opens the Planner");
    }

    private static void CalendarEveryTarget()
    {
        Action<PlanHost> setup = x =>
        {
            x.CalSpecs.Add(new CalSpec { Day = 40, Title = "Rent", A = 1, App = "RealLifeFinance" });
            x.CalSpecs.Add(new CalSpec { Day = 41, Title = "Class", A = 2, App = "RealLifeCampus" });
            x.Tomorrow.Add(Plan("Delivery", 41, 9 * 60, 10 * 60, PlannerCategory.Business, false, "delivery"));
        };
        foreach (int offset in new[] { 0, 3 })
        {
            CalendarFlow probe = CalRig(out PlanHost ph, 40, setup);
            if (offset > 0)
            {
                CalScene(probe);
                probe.Press(UiDiary.DayCommand, probe.Chosen + offset);
            }
            UiScene scene = CalScene(probe);
            int pressed = 0, dead = 0;
            string where = null;
            for (int n = 0; n < scene.NodeCount; n++)
            {
                if (scene.Nodes[n].Disabled || scene.Nodes[n].Command == UiDraw.CloseCommand)
                {
                    continue;
                }
                int cmd = scene.Nodes[n].Command, arg = scene.Nodes[n].Arg;
                CalendarFlow f = CalRig(out PlanHost h, 40, setup);
                if (offset > 0)
                {
                    CalScene(f);
                    f.Press(UiDiary.DayCommand, f.Chosen + offset);
                }
                CalScene(f);
                int month = f.Month, chosen = f.Chosen;
                bool acted = f.Press(cmd, arg);
                bool right;
                switch (cmd)
                {
                    case UiDiary.DayCommand: right = acted && f.Chosen == arg; break;
                    case UiDiary.MonthCommand: right = acted && f.Month == month + (arg < 0 ? -1 : 1); break;
                    case UiDiary.EntryCommand: right = acted && h.OpenedApp != null; break;
                    case LifePages.Add: right = acted && h.AskedTitle != null; break;
                    case LifePages.Second: right = acted && h.PlannerOpened; break;
                    case LifePages.Today: right = acted && f.ChosenDay == 40; break;
                    default: right = false; break;
                }
                pressed++;
                if (!right)
                {
                    dead++;
                    where = where ?? "command " + cmd + " arg " + arg;
                }
            }
            Check(dead == 0 && pressed > 0, "life audit: the Calendar's targets each act as the kind drawn, " + (offset == 0 ? "on today" : "on another day") + " (" + pressed + " pressed" + (where != null ? "; " + where : string.Empty) + ")");
        }
    }
}
