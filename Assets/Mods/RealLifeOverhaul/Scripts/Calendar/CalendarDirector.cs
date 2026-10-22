using System;
using System.Globalization;
using System.Text;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>
    /// The Calendar app on the phone: the month as the chosen year divides it (Core/CalendarRules), with the days money
    /// moves, the player's own time and the city's events read from each feature's schedule (docs/features/calendar.md).
    /// This keeps the app on the phone and asks the sources when the app wants a month; nothing is saved or sent.
    /// Cost: a timer comparison a frame; once a second the app is put on or off and an open page checked for a new day.
    /// </summary>
    public sealed partial class CalendarDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("calendar", "CalendarDirector", typeof(CalendarDirector));

        public const string AppId = "RealLifeCalendar";
        private static readonly string[] AppGlyphs = { "Icon-Calendar", "Icon-Clock", "Icon-OpeningHours" };

        public static IModLogger Logger;
        public static CalendarDirector Instance { get; private set; }

        private ICalendarSource[] _sources = { CalendarGame.Source };   // the game's own, then each feature's from Scripts/Wiring
        private int[] _counts = new int[1];
        private bool[] _failed = new bool[1];

        /// <summary>The last look's entries, in the calendar's order, and nightly lines.</summary>
        public readonly CalendarList List = new CalendarList();

        private CalendarApp _view;
        private PhoneApp _app;
        private float _next;
        private bool _gaveUp;     // the game's phone is not built as expected: logged once, not tried again
        private bool _broken;

        /// <summary>One more source, from Scripts/Wiring; a source or an id already there is not added again.</summary>
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
                Logger?.Warn("Calendar: switched off for this session after an error.");
                StopAll();
            }
        }

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
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new CalendarApp(this);
            GameSprites.Want(AppGlyphs);   // asked for before the one look through the game's pictures
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

        /// <summary>Opens the app on a month (CalendarRules.MonthOf).</summary>
        public void OpenAt(int month)
        {
            if (_app != null && Settings.Calendar)
            {
                _view?.Want(month);
                _app.Open();
            }
        }

        /// <summary>
        /// Fills <see cref="List"/> for a month from every source; one that throws is logged and left out for the session, and
        /// nothing it added stays.
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
                int before = List.Items.Count, nightsBefore = List.Nights.Count;
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
                    List.Cut(before, nightsBefore);   // its entries and nightly lines alike, back to the pools
                }
                _counts[s] = List.Items.Count - before;
            }
            List.Items.Sort(MonthViewRules.ByTime);
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

        /// <summary>How many entries each source gave at the last look, for the log.</summary>
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

        /// <summary>Takes the app off the phone; the next second puts it back while the option is on and no fault stopped it.</summary>
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
