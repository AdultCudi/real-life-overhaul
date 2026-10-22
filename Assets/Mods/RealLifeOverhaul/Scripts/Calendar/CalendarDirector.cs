using System;
using System.Globalization;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>
    /// The Calendar: an app on the game's phone with the month the player is in, as the year they chose divides it
    /// (Core/CalendarRules: twelve months of 5 days in a 60-day year, of 30 and 31 in a 365-day one), the days money
    /// moves, their own time and what is on in the city, read from each feature's real schedule. See
    /// docs/features/calendar.md.
    ///
    /// This class keeps it going and nothing more: it puts the app on the phone once the game's phone is there, and asks
    /// the sources when the app wants a month. Nothing is saved and nothing is sent. Its Update is a timer comparison;
    /// once a second it puts the app on the phone (or takes it off with its option) and, while the app is open, compares
    /// the game's day with the one the page was written for, so that a page stays right across midnight. It costs nothing
    /// while the app is closed.
    /// </summary>
    public sealed partial class CalendarDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("calendar", "CalendarDirector", typeof(CalendarDirector));   // its Update (Core/Guard)

        public const string AppId = "RealLifeCalendar";
        private static readonly string[] AppGlyphs = { "Icon-Calendar", "Icon-Clock", "Icon-OpeningHours" };

        public static IModLogger Logger;
        public static CalendarDirector Instance { get; private set; }

        /// <summary>Every source: the game's own, then each feature's, added from Scripts/Wiring.</summary>
        private ICalendarSource[] _sources = { CalendarGame.Source };
        private int[] _counts = new int[1];
        private bool[] _failed = new bool[1];

        /// <summary>The last look: the month asked for, its entries in the calendar's order and what moves every night.</summary>
        public readonly CalendarList List = new CalendarList();

        private CalendarApp _view;
        private PhoneApp _app;
        private float _next;
        private bool _gaveUp;     // the game's phone is not built as expected: one line was logged, nothing more is tried
        private bool _broken;

        /// <summary>The month (CalendarRules.MonthOf) the last look was for; -1 before the first.</summary>
        public int LookedMonth { get; private set; } = -1;

        /// <summary>
        /// One more source, from a pairing in Scripts/Wiring (a feature the calendar does not know of). Its id must be one
        /// no other source has; adding it again does nothing.
        /// </summary>
        public void AddSource(ICalendarSource source)
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

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            StopAll();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Calendar: CalendarDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
                Logger?.Warn("Calendar: switched off for this session after an error.");
                StopAll();
            }
        }

        // Once a second: the app on or off the phone as the option says, and an open page written again on a new day.
        private void Slow()
        {
            if (CalendarGame.Loading())
            {
                return;
            }
            if (!Settings.Calendar)
            {
                StopAll();
                return;
            }
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            if (_app != null && _view != null && _app.Showing)
            {
                _view.DayCheck(CalendarGame.Today);
            }
        }

        private void Install()
        {
            var view = new CalendarApp(this);
            GameSprites.Want(AppGlyphs);   // named before the one look through the game's pictures
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_calendar_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(108, 178, 255, 255), new Color32(36, 92, 196, 255), 0.42f, "AppIcon-Calendar"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Calendar: the game's phone is not built as this version expects, so the Calendar is left off.");
                }
                return;
            }
            _view = view;
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        /// <summary>Opens the app on today's month.</summary>
        public void Open()
        {
            if (_app != null && Settings.Calendar)
            {
                _view?.Want(-1);
                _app.Open();
            }
        }

        /// <summary>Opens the app on a month (CalendarRules.MonthOf).</summary>
        public void OpenAt(int month)
        {
            if (_app != null && Settings.Calendar)
            {
                _view?.Want(month);
                _app.Open();
            }
        }

        // ---------------------------------------------------------------- looking

        /// <summary>
        /// Asks every source for the days of one month and puts the answer in the calendar's order (List). A source that
        /// throws is logged once and left out for the rest of the session; the others are not affected.
        /// </summary>
        public void Look(int month)
        {
            float started = Time.realtimeSinceStartup;
            int year = GameCalendar.DaysPerYear;
            int from = CalendarRules.MonthStart(month, year), to = CalendarRules.MonthEnd(month, year);
            List.Begin(from, to);
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
                        _sources[s].Collect(from, to, List);
                    }
                }
                catch (Exception e)
                {
                    _failed[s] = true;
                    Logger?.Error(e);
                    Logger?.Warn("Calendar: the source '" + _sources[s].Id + "' failed and is left out for this session.");
                    List.Items.RemoveRange(before, List.Items.Count - before);
                }
                _counts[s] = List.Items.Count - before;
            }
            List.Items.Sort(MonthViewRules.ByTime);
            LookedMonth = month;
            if (Meta.Dev.Verbose)
            {
                bool whole = CalendarRules.MonthOf(from, year) == month && CalendarRules.MonthOf(to, year) == month && CalendarRules.MonthOf(to + 1, year) == month + 1
                             && CalendarRules.MonthOf(from - 1, year) == month - 1;
                Logger?.Info("Calendar check (month " + (CalendarRules.MonthInYear(month, year) + 1) + " of year " + CalendarRules.YearOfMonth(month, year) + ", a year of " + year
                             + " days): days " + from + " to " + to + " (" + (to - from + 1) + " days), the month ends on day " + to + "; " + List.Items.Count + " entries and "
                             + List.Nights.Count + " nightly line(s): " + LookSummary() + "; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture)
                             + " ms: " + (whole ? "PASS" : "the month's days are not one month"));
            }
        }

        /// <summary>For the diagnostics line: how many entries each source gave at the last look.</summary>
        public string LookSummary()
        {
            var text = new StringBuilder(200);
            for (int s = 0; s < _sources.Length; s++)
            {
                text.Append(s > 0 ? ", " : string.Empty).Append(_sources[s].Id).Append(' ')
                    .Append(_failed[s] ? "failed" : _sources[s].Available ? _counts[s].ToString(CultureInfo.InvariantCulture) : "off");
            }
            return text.ToString();
        }

        /// <summary>The app goes off the phone (its option switched off, a fault, the save tools); it comes back with what the options say.</summary>
        public void StopAll()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
            _view = null;
        }
    }
}
