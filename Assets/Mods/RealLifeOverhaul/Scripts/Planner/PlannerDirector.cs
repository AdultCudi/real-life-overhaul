using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Planner
{
    /// <summary>
    /// The Planner: a phone app gathering what the player has to do into one agenda, with reminders and notes
    /// (docs/features/planner.md). It installs the app, keeps the save lines, asks the sources and sends reminders.
    /// Cost a frame: a timer comparison; once a second the clock against the soonest reminder; the rest on the hour.
    /// </summary>
    public sealed class PlannerDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("planner", "PlannerDirector", typeof(PlannerDirector));

        public const string AppId = "RealLifePlanner";
        internal const int BadgeMinutes = 120;      // game minutes ahead the badge counts

        public static IModLogger Logger;
        public static PlannerDirector Instance { get; private set; }

        // in the order of PlannerCategory; a feature that is off or absent costs nothing
        private IPlannerSource[] _sources =
        {
            new NoteSource(), new SampleSource(), new TaxSource(), new CareerSource(), new JusticeSource(), new CustodySource(),
            new LicenceSource(), new FamilySource(), new SocialSource(), new TravelSource(), new EventsSource(), new CivicSource(),
            new BusinessSource(), new HealthSource()
        };

        private int[] _counts;
        private bool[] _failed;
        private readonly List<PlannerReminder> _due = new List<PlannerReminder>(4);
        public readonly PlannerList List = new PlannerList();

        private PlannerApp _view;
        private PhoneApp _app;
        private float _next;
        private bool _gaveUp;          // the game's phone is not built as expected: logged once, not tried again
        private bool _broken;
        private Action _onSave, _onHour, _hourSeen;
        private int _lastHour = -1;
        private int _morningSeen = -1;   // the game minute the morning line was last looked at

        /// <summary>The test's sample entries are showing, for this session; its sample notes are saved and marked.</summary>
        public static bool Samples;

        public PlannerDirector()
        {
            _counts = new int[_sources.Length];
            _failed = new bool[_sources.Length];
        }

        /// <summary>One more source, from Scripts/Wiring; a source with an id already there is ignored.</summary>
        public void AddSource(IPlannerSource source)
        {
            if (source == null)
            {
                return;
            }
            for (int s = 0; s < _sources.Length; s++)
            {
                if (_sources[s] == source || _sources[s].Id == source.Id)
                {
                    return;
                }
            }
            int count = _sources.Length;
            Array.Resize(ref _sources, count + 1);
            Array.Resize(ref _counts, count + 1);
            Array.Resize(ref _failed, count + 1);
            _sources[count] = source;
        }

        public static int Now()
        {
            return PlannerTime.At(TimeHelper.CurrentDay, TimeHelper.CurrentHour, Mathf.FloorToInt(TimeHelper.CurrentMinute));
        }

        private void Awake()
        {
            Instance = this;
            PlannerText.WantIcons();   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            PlannerStore.Reset();
            Samples = false;
            _onSave = OnSave;
            _onHour = Guard.Wrap(OnHour);
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GlobalEvents.onNewHour -= _onHour;
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
            PlannerStore.Reset();
            GameSprites.Forget();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Planner: PlannerDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            _next = now + 1f;
            if (_broken)
            {
                return;
            }
            try
            {
                Slow();
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Planner: switched off for this session after an error.");
                if (_app != null)
                {
                    _app.Remove();
                    _app = null;
                }
            }
        }

        private void Slow()
        {
            if (SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;   // LifeEvents makes the save's modData ready first
            }
            if (!PlannerStore.Loaded)
            {
                PlannerStore.Load(SaveGameManager.Current.modData);
                PlannerStore.ArmAll(Now());
                SaveHook.Keep(_onSave);
                Logger?.Info("Planner: ready. " + PlannerStore.Notes.Count + " note(s), " + PlannerStore.Reminders.Count + " reminder(s) waiting.");
            }
            if (Now() >= PlannerStore.NextFire)
            {
                Due();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
            if (!Settings.Planner)
            {
                if (_app != null)
                {
                    _app.Remove();
                    _app = null;
                    _view = null;
                }
                return;
            }
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            Morning();
            if (_lastHour < 0 && (_app != null || _gaveUp))
            {
                // a save just loaded: whatever fell due goes out now, once, as late
                _lastHour = TimeHelper.CurrentHour;
                OnHour();
            }
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new PlannerApp(this);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_planner_app",
                Icon = like => AppIconPainter.Paint(like, PlannerText.First(PlannerText.AppGlyphs), new Color32(255, 122, 138, 255), new Color32(226, 61, 108, 255), 0.42f, "AppIcon-Planner"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Planner: the game's phone is not built as this version expects, so the Planner is left off. Reminders already set still go out.");
                }
                return;
            }
            _view = view;
            if (!PlannerStore.Welcomed)
            {
                PlannerStore.Welcomed = true;
                Notifications.Show(NotificationType.Info, "reallife_planner_welcome", null, 10f, "reallife_planner_welcome", Guard.Wrap(Open), true, false);
            }
        }

        public void Open()
        {
            if (_app != null && Settings.Planner)
            {
                _app.Open();
            }
        }

        /// <summary>Fills List from every source for the days fromDay..toDay; a source that throws is left out for the session.</summary>
        public void Look(int fromDay, int toDay, bool wantText)
        {
            List.Begin(fromDay, toDay, wantText);
            for (int s = 0; s < _sources.Length; s++)
            {
                _counts[s] = 0;
                if (_failed[s])
                {
                    continue;
                }
                _counts[s] = CollectFrom(s, List, fromDay, toDay);
            }
            // a kind switched off is not shown, counted or reminded of
            if (PlannerStore.HiddenCategories != 0)
            {
                for (int i = List.Items.Count - 1; i >= 0; i--)
                {
                    if (!PlannerStore.Shows(List.Items[i].Category))
                    {
                        List.Items.RemoveAt(i);
                    }
                }
            }
            // only with the texts are places found and a way too long a clash, at the pace by car or taxi
            if (wantText)
            {
                Places();
            }
            PlannerLogic.Arrange(List, Now(), wantText ? PlannerRules.DrivePerMinute : 0);
        }

        /// <summary>
        /// For another app (the Calendar): the sources <paramref name="wanted"/> accepts by id, for the days fromDay..toDay,
        /// into the caller's own list with their texts; the Planner's own list, hidden kinds and reminders are untouched.
        /// </summary>
        public void LookFor(PlannerList into, int fromDay, int toDay, Func<string, bool> wanted)
        {
            into.Begin(fromDay, toDay, true);
            for (int s = 0; s < _sources.Length; s++)
            {
                if (_failed[s] || (wanted != null && !wanted(_sources[s].Id)))
                {
                    continue;
                }
                CollectFrom(s, into, fromDay, toDay);
            }
        }

        // one source's entries into a list, how many; a source that throws is left out for the session
        private int CollectFrom(int s, PlannerList into, int fromDay, int toDay)
        {
            int before = into.Items.Count;
            try
            {
                if (_sources[s].Available)
                {
                    _sources[s].Collect(fromDay, toDay, into);
                }
            }
            catch (Exception e)
            {
                _failed[s] = true;
                Logger?.Error(e);
                Logger?.Warn("Planner: the source '" + _sources[s].Id + "' failed and is left out for this session.");
                into.Items.RemoveRange(before, into.Items.Count - before);
            }
            return into.Items.Count - before;
        }

        private void Places()
        {
            List<PlannerEntry> items = List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.HasPlace)
                {
                    e.Placed = true;
                    e.PlaceX = e.X;
                    e.PlaceZ = e.Z;
                }
                else if (!string.IsNullOrEmpty(e.Street))
                {
                    e.Placed = PlannerGame.Door(e.Street, e.Number, out e.PlaceX, out e.PlaceZ);
                }
            }
        }

        public string LookSummary()
        {
            var text = new StringBuilder(160);
            for (int s = 0; s < _sources.Length; s++)
            {
                text.Append(s > 0 ? ", " : string.Empty).Append(_sources[s].Id).Append(' ')
                    .Append(_failed[s] ? "failed" : _sources[s].Available ? _counts[s].ToString() : "off");
            }
            return text.ToString();
        }

        /// <summary>The player has looked: the badge goes, and stays away until something new falls due.</summary>
        public void Seen()
        {
            int now = Now();
            PlannerStore.SeenStamp = PlannerLogic.DueStamp(List, now, BadgeMinutes);
            PlannerStore.SeenOldHash = false;
            if (_app != null)
            {
                _app.SetBadge(0);
            }
        }

        private static readonly int HotOnHour = HotSpots.Slot("Planner: PlannerDirector.OnHour");

        // GlobalEvents.onNewHour, raised on a new day too; no text is built unless a reminder is due
        private void OnHour()
        {
            using var hotSpot = HotSpots.Time(HotOnHour);
            if (_broken || !PlannerStore.Loaded || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                int now = Now();
                int today = TimeHelper.CurrentDay;
                _lastHour = TimeHelper.CurrentHour;
                Due();
                if (_app != null && Settings.Planner && !_app.Showing)
                {
                    Look(today, today + 1, false);
                    int count = PlannerLogic.DueSoon(List, now, BadgeMinutes);
                    int stamp = PlannerLogic.DueStamp(List, now, BadgeMinutes);
                    if (PlannerStore.SeenOldHash)
                    {
                        PlannerStore.CarrySeen(PlannerLogic.OldDueStamp(List, now, BadgeMinutes), stamp);
                    }
                    _app.SetBadge(count > 0 && stamp != PlannerStore.SeenStamp ? count : 0);
                }
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Planner: switched off for this session after an error on the hour.");
            }
        }

        private void Due()
        {
            int now = Now();
            PlannerStore.TakeDue(now, _due);
            if (_due.Count > 0)
            {
                Remind(now, TimeHelper.CurrentDay);
            }
        }

        // each reminder is checked against its source now: only what is still there and to do is said
        private void Remind(int now, int today)
        {
            if (!Settings.Planner || !Settings.PlannerReminders)
            {
                Rearm(now);
                _due.Clear();
                return;
            }
            int first = today, last = today;
            for (int i = 0; i < _due.Count; i++)
            {
                int day = PlannerTime.DayOf(_due[i].EntryBegins);
                first = Mathf.Min(first, day - 1);
                last = Mathf.Max(last, day + 1);
            }
            Look(Mathf.Max(1, first), Mathf.Min(last, today + 8), true);
            for (int i = 0; i < _due.Count; i++)
            {
                PlannerReminder reminder = _due[i];
                PlannerEntry entry = List.Find(reminder.Entry);
                bool late = PlannerStore.IsLate(reminder, now);
                string why = null;
                if (entry == null)
                {
                    why = "its entry is gone";
                }
                else if (entry.Done || entry.State == PlannerState.Missed || entry.State == PlannerState.Over)
                {
                    why = "its entry is " + entry.State;
                }
                else
                {
                    // an entry moved since (a hearing put back) takes its reminder with it
                    int start = entry.Deadline && entry.End >= 0 ? entry.End : entry.Start;
                    int fireAt = PlannerStore.FireAt(entry.Day, start, reminder.Lead, reminder.Travel);
                    if (fireAt > now && PlannerStore.SetReminder(entry.Id, entry.Day, start, reminder.Lead, now, reminder.Travel))
                    {
                        why = "its entry has moved, the reminder with it";
                    }
                }
                if (why != null)
                {
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Planner: reminder for " + reminder.Entry.Source + " " + reminder.Entry.A + " not sent: " + why + ".");
                    }
                    continue;
                }
                var data = new Dictionary<string, string>
                {
                    { "title", entry.Title ?? string.Empty },
                    { "when", PlannerText.WhenLong(entry, today) }
                };
                string key = late ? "reallife_planner_reminder_late" : "reallife_planner_reminder";
                if (reminder.Lead == ReminderLead.LeaveBy && PlannerGame.MetresTo(entry, out float metres))
                {
                    // the way from where the player is now
                    key = late ? "reallife_planner_reminder_leave_late" : "reallife_planner_reminder_leave";
                    data["way"] = PlannerText.Span(PlannerRules.TravelMinutes(metres, PlannerGame.ByCar() ? PlannerRules.DrivePerMinute : PlannerRules.WalkPerMinute));
                }
                EntryId id = entry.Id;
                Notifications.Show(late ? NotificationType.Warning : NotificationType.Info, key, data, 12f, null, Guard.Wrap(() => OpenAt(id)), true, false);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Planner: reminder sent for " + id.Source + " " + id.A + " ('" + entry.Title + "', " + data["when"] + ")"
                                 + (late ? ", late by " + (now - reminder.FireAt) + " min." : "."));
                }
            }
            Rearm(now);
            _due.Clear();
        }

        // a repeating note's reminder is followed by the one for its next time
        private void Rearm(int now)
        {
            for (int i = 0; i < _due.Count; i++)
            {
                if (string.Equals(_due[i].Entry.Source, NoteSource.SourceId, StringComparison.Ordinal))
                {
                    PlannerStore.Arm(PlannerStore.Note(_due[i].Entry.A), now);
                }
            }
        }

        private void OpenAt(EntryId id)
        {
            if (_app == null || _view == null || !Settings.Planner)
            {
                return;
            }
            _view.Select(id);
            _app.Open();
        }

        private void OnSave()
        {
            try
            {
                if (PlannerStore.Loaded && SaveGameManager.Current != null)
                {
                    PlannerStore.Save(SaveGameManager.Current.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // the day's line on waking or from 05:00, once a day; looked at once a game minute at most
        private void Morning()
        {
            int now = Now();
            if (now == _morningSeen || !Settings.Planner || !Settings.PlannerMorning)
            {
                return;
            }
            _morningSeen = now;
            int today = TimeHelper.CurrentDay;
            if (!PlannerRules.MorningDue(today, TimeHelper.CurrentHour, false, PlannerStore.MorningDay) || PlannerGame.Asleep())
            {
                return;
            }
            if (_app != null && _app.Showing)
            {
                return;   // the line waits until the app is closed
            }
            PlannerStore.MorningDay = today;
            Look(today, today, true);
            int count = PlannerRules.MorningCount(List.Items, today);
            PlannerEntry first = PlannerRules.FirstOf(List.Items, today);
            int leave = -1;
            if (first != null && first.State == PlannerState.Upcoming && PlannerGame.MetresTo(first, out float metres) && metres > PlannerRules.HereMetres)
            {
                leave = PlannerRules.LeaveBy(first, PlannerRules.TravelMinutes(metres, PlannerGame.ByCar() ? PlannerRules.DrivePerMinute : PlannerRules.WalkPerMinute));
            }
            bool leaveBy = leave > now && PlannerTime.DayOf(leave) == today;
            string key = PlannerRules.MorningKey(count, first != null, leaveBy);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Planner check (morning line, day " + today + " at " + PlannerText.Clock(PlannerTime.OfDay(now)) + "): " + count + " entries to do, "
                             + (first != null ? "the first at " + PlannerText.Clock(PlannerTime.OfDay(first.Begins)) : "none at a set time")
                             + (leaveBy ? ", leave by " + PlannerText.Clock(PlannerTime.OfDay(leave)) : string.Empty)
                             + "; " + (key == null ? "an empty day, nothing sent" : "sent once") + ": PASS");
            }
            if (key == null)
            {
                return;
            }
            var data = new Dictionary<string, string> { { "count", count.ToString() } };
            if (first != null)
            {
                data["time"] = PlannerText.Clock(PlannerTime.OfDay(first.Begins));
                data["title"] = first.Title ?? string.Empty;
            }
            if (leaveBy)
            {
                data["leave"] = PlannerText.Clock(PlannerTime.OfDay(leave));
            }
            Notifications.Show(NotificationType.Info, key, data, 12f, null, Guard.Wrap(Open), true, false);
        }

        /// <summary>A test: a marked sample entry of every kind today, and two marked notes.</summary>
        public void TestFill()
        {
            if (!PlannerStore.Loaded || SaveGameManager.Current == null)
            {
                TestRefusals.Tell(Logger, "Planner fill", TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.Planner || _broken)
            {
                TestRefusals.Tell(Logger, "Planner fill", TestRefusal.FeatureOff);
                return;
            }
            TestClear(false);
            Samples = true;
            int today = TimeHelper.CurrentDay;
            string mark = PlannerText.T("reallife_planner_sample_mark");
            PlannerNote soon = PlannerStore.AddNote(mark + " " + PlannerText.T("reallife_planner_preset_call"), today, Mathf.Min(23, TimeHelper.CurrentHour + 1) * 60);
            PlannerNote later = PlannerStore.AddNote(mark + " " + PlannerText.T("reallife_planner_preset_shopping"), today + 2, -1);
            if (soon != null)
            {
                soon.Sample = true;
                soon.Repeat = PlannerRepeat.Daily;   // in every column of the Week tab
            }
            if (later != null)
            {
                later.Sample = true;
            }
            _lastHour = TimeHelper.CurrentHour;
            OnHour();
            Notifications.Show(NotificationType.Info, "reallife_planner_test_filled", null, 8f, null, Guard.Wrap(Open), true, false);
        }

        /// <summary>A test: the samples taken away.</summary>
        public void TestClear(bool tell = true)
        {
            Samples = false;
            for (int i = PlannerStore.Notes.Count - 1; i >= 0; i--)
            {
                if (PlannerStore.Notes[i].Sample)
                {
                    PlannerStore.RemoveNote(PlannerStore.Notes[i].Id);
                }
            }
            if (tell && PlannerStore.Loaded)
            {
                OnHour();
                Notifications.Show(NotificationType.Info, "reallife_planner_test_cleared", null, 6f, null, null, true, false);
            }
        }

        /// <summary>For the save tools: the app off the phone, back as the options say.</summary>
        public void StopAll()
        {
            Samples = false;
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
        }
    }
}
