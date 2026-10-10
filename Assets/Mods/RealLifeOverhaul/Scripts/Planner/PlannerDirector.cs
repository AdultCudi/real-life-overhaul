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
    /// The Planner: an app on the game's phone that gathers what the player has to do, from this mod's features and
    /// from the game, into one agenda, with reminders and the player's own notes. See the README ("The Planner").
    ///
    /// This class keeps it going and nothing more: it puts the app on the phone once the game's phone is there, reads
    /// and writes the save's three lines, asks the sources when someone looks, and sends reminders. Its Update is a
    /// timer comparison; the work is done once a second until the app is on the phone, and after that on the game's
    /// own hour event, on the save event and when the app is opened.
    /// </summary>
    public sealed class PlannerDirector : MonoBehaviour
    {
        public const string AppId = "RealLifePlanner";
        private const int BadgeMinutes = 120;      // the badge counts what begins within two hours

        public static IModLogger Logger;
        public static PlannerDirector Instance { get; private set; }

        /// <summary>Every source, in the order of PlannerCategory. A feature that is off or absent is skipped at no cost.</summary>
        private readonly IPlannerSource[] _sources =
        {
            new NoteSource(), new SampleSource(), new TaxSource(), new CareerSource(), new JusticeSource(), new CustodySource(),
            new LicenceSource(), new FamilySource(), new SocialSource(), new TravelSource(), new EventsSource(), new CivicSource(),
            new BusinessSource(), new HealthSource()
        };

        private readonly int[] _counts;
        private readonly bool[] _failed;
        private readonly List<PlannerReminder> _due = new List<PlannerReminder>(4);
        public readonly PlannerList List = new PlannerList();

        private PlannerApp _view;
        private PhoneApp _app;
        private float _next;
        private bool _gaveUp;          // the game's phone is not built as expected: one line was logged, nothing more is tried
        private bool _broken;
        private Action _onSave, _onHour, _hourSeen;
        private int _lastHour = -1;

        /// <summary>The test button's sample entries are showing (this session only; the sample notes are in the save and marked).</summary>
        public static bool Samples;

        public PlannerDirector()
        {
            _counts = new int[_sources.Length];
            _failed = new bool[_sources.Length];
        }

        public static int Now()
        {
            return PlannerTime.At(TimeHelper.CurrentDay, TimeHelper.CurrentHour, Mathf.FloorToInt(TimeHelper.CurrentMinute));
        }

        private void Awake()
        {
            Instance = this;
            PlannerStore.Reset();
            Samples = false;
            _onSave = OnSave;
            _onHour = OnHour;
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

        private void Update()
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

        // Once a second: the save's lines (once), the app on or off the phone as the option says, and the game's hour event held.
        private void Slow()
        {
            if (SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;   // LifeEvents makes the save's modData ready first
            }
            if (!PlannerStore.Loaded)
            {
                PlannerStore.Load(SaveGameManager.Current.modData);
                SaveHook.Keep(_onSave);
                Logger?.Info("Planner: ready. " + PlannerStore.Notes.Count + " note(s), " + PlannerStore.Reminders.Count + " reminder(s) waiting.");
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
            if (_lastHour < 0 && (_app != null || _gaveUp))
            {
                // A save loaded in the middle of things: whatever fell due while it was not running goes out now, once, as late.
                _lastHour = TimeHelper.CurrentHour;
                OnHour();
            }
        }

        private void Install()
        {
            var view = new PlannerApp(this);
            PlannerText.WantIcons();   // named before the one look through the game's pictures
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_planner_app",
                Icon = like => AppIconPainter.Paint(like, PlannerText.First(PlannerText.AppGlyphs), new Color32(255, 122, 138, 255), new Color32(226, 61, 108, 255), 0.42f, "AppIcon-Planner"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
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
                Notifications.Show(NotificationType.Info, "reallife_planner_welcome", null, 10f, "reallife_planner_welcome", Open, true, false);
            }
        }

        public void Open()
        {
            if (_app != null && Settings.Planner)
            {
                _app.Open();
            }
        }

        // ---------------------------------------------------------------- looking

        /// <summary>
        /// Asks every source for the days fromDay..toDay and puts the answer in order (List). A source that throws is
        /// logged once and left out for the rest of the session; the others are not affected.
        /// </summary>
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
                int before = List.Items.Count;
                try
                {
                    if (_sources[s].Available)
                    {
                        _sources[s].Collect(fromDay, toDay, List);
                    }
                }
                catch (Exception e)
                {
                    _failed[s] = true;
                    Logger?.Error(e);
                    Logger?.Warn("Planner: the source '" + _sources[s].Id + "' failed and is left out for this session.");
                    List.Items.RemoveRange(before, List.Items.Count - before);
                }
                _counts[s] = List.Items.Count - before;
            }
            // What the player has switched off in the app is not shown, not counted and not reminded of.
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
            PlannerLogic.Arrange(List, Now());
        }

        /// <summary>For the diagnostics line: how many entries each source gave at the last look.</summary>
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
            if (_app != null)
            {
                _app.SetBadge(0);
            }
        }

        // ---------------------------------------------------------------- the hour

        // The game's own hour event (GlobalEvents.onNewHour; a new day raises it too). Nothing here builds a text
        // unless a reminder is due.
        private void OnHour()
        {
            if (_broken || !PlannerStore.Loaded || SaveGameManager.Current == null)
            {
                return;
            }
            try
            {
                int now = Now();
                int today = TimeHelper.CurrentDay;
                _lastHour = TimeHelper.CurrentHour;
                PlannerStore.TakeDue(now, _due);
                if (_due.Count > 0)
                {
                    Remind(now, today);
                }
                if (_app != null && Settings.Planner && !_app.Showing)
                {
                    Look(today, today + 1, false);
                    int count = PlannerLogic.DueSoon(List, now, BadgeMinutes);
                    _app.SetBadge(count > 0 && PlannerLogic.DueStamp(List, now, BadgeMinutes) != PlannerStore.SeenStamp ? count : 0);
                }
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Planner: switched off for this session after an error on the hour.");
            }
        }

        // Each reminder that is due is checked against its source now: only what is still there and still to do is said.
        private void Remind(int now, int today)
        {
            if (!Settings.Planner || !Settings.PlannerReminders)
            {
                _due.Clear();   // reminders are switched off: what fell due is let go
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
                    // The entry has moved since the reminder was set (a hearing put back): the reminder moves with it.
                    int start = entry.Deadline && entry.End >= 0 ? entry.End : entry.Start;
                    int fireAt = PlannerStore.FireAt(entry.Day, start, reminder.Lead);
                    if (fireAt >= now + 60 && PlannerStore.SetReminder(entry.Id, entry.Day, start, reminder.Lead, now))
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
                EntryId id = entry.Id;
                Notifications.Show(late ? NotificationType.Warning : NotificationType.Info, late ? "reallife_planner_reminder_late" : "reallife_planner_reminder",
                    data, 12f, null, () => OpenAt(id), true, false);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Planner: reminder sent for " + id.Source + " " + id.A + " ('" + entry.Title + "', " + data["when"] + ")"
                                 + (late ? ", late by " + (now - reminder.FireAt) + " min." : "."));
                }
            }
            _due.Clear();
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

        // ---------------------------------------------------------------- tests

        /// <summary>"Test: fill the Planner with one of everything": a marked sample entry of every kind today, and two marked notes.</summary>
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
            }
            if (later != null)
            {
                later.Sample = true;
            }
            _lastHour = TimeHelper.CurrentHour;
            OnHour();
            Notifications.Show(NotificationType.Info, "reallife_planner_test_filled", null, 8f, null, Open, true, false);
        }

        /// <summary>"Test: clear the Planner's samples".</summary>
        public void TestClear()
        {
            TestClear(true);
        }

        private void TestClear(bool tell)
        {
            Samples = false;
            for (int i = PlannerStore.Notes.Count - 1; i >= 0; i--)
            {
                if (PlannerStore.Notes[i].Sample)
                {
                    PlannerStore.SetReminder(new EntryId(NoteSource.SourceId, PlannerStore.Notes[i].Id), 0, 0, ReminderLead.None, 0);
                    PlannerStore.Notes.RemoveAt(i);
                }
            }
            if (tell && PlannerStore.Loaded)
            {
                OnHour();
                Notifications.Show(NotificationType.Info, "reallife_planner_test_cleared", null, 6f, null, null, true, false);
            }
        }

        /// <summary>For the save tools: the app goes off the phone and comes back with whatever the options say.</summary>
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
