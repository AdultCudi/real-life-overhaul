using System;
using BAModAPI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The seasons (docs/features/seasons.md): what customers buy follows the season of the calendar, and some winter days
    /// it snows. Everything comes of the day and the save's number (SeasonRules), so nothing is saved and every machine of
    /// a session has the same seasons and the same snow. Cost: a timer comparison a frame; once a second a few comparisons,
    /// the snow's two figures worked out and handed to the weather, the walkers and the look, and the seasons' other looks
    /// every few seconds; while snow falls, the flakes moved after the camera four times a second; the goods written once
    /// a season. Nothing of it allocates in its steady run (StandardCheck, RLO-PERF-014).
    /// </summary>
    public sealed partial class SeasonDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("seasons", "SeasonDirector", typeof(SeasonDirector));
        private static readonly int HotUpdate = HotSpots.Slot("Seasons: SeasonDirector.Update");

        private const float WalkPatchy = 0.93f;   // of the game's paces, on snow in patches
        private const float WalkCovered = 0.85f;  // on covered streets
        private const int MorningFrom = 7, MorningTo = 10, EveningAt = 18;   // hours the season's and the snow's news is given
        private const float SlowEvery = 1f;       // seconds between looks at the snow and the news
        private const float FollowEvery = 0.25f;  // seconds between moves of the flakes after the camera, while they fall
        private const float LookEvery = 3f;       // seconds between looks at the trees, sky, rain, leaves, coats and the line under the date

        public static IModLogger Logger;
        public static SeasonDirector Instance { get; private set; }

        private SnowLook _look;
        private SnowHush _hush;
        private Func<bool> _markGround;  // made once: handed over behind the loading screen and again at a door
        private float _next, _nextSlow, _nextLook;
        private bool _lookShowing;       // a test's season look was held at the last look: a change is looked at at once
        private bool _broken;
        private bool _on;                // something of the seasons may be on the city: StopAll has work to do
        private int _applied = -1;       // the season whose figures are on the goods; -1 for none
        private int _toldSeason = -1, _toldForecast = -1, _toldTomorrow = -1, _toldFalling = -1;   // days each piece of news was given on
        private float _holdUntil;        // the test's snow, while the real time is before this
        private bool _markLater;         // the ground was left unmarked (the seasons off): marked at the next door once they are on

        /// <summary>The season of today (CalendarRules.Winter to Autumn).</summary>
        public int Season { get; private set; } = CalendarRules.Spring;
        public SnowDay Today { get; private set; }
        public float Falling { get; private set; }
        public float Cover { get; private set; }

        /// <summary>The snow on the roads, 0 to 2, while the seasons and the weather are on; 0 otherwise.</summary>
        public int Step { get; private set; }

        public bool Holding => _holdUntil > Time.unscaledTime;

        private void Awake()
        {
            Instance = this;
            _look = new SnowLook(transform);
            _seasonLook = new SeasonLook(transform, this);
            _hush = gameObject.AddComponent<SnowHush>();
            // The snow on the ground copies the game's road paint, and the flakes are the game's soft round particle:
            // both can only be looked for behind a screen.
            ScenePrewarm.Add("seasons: the game's road paint being found for the snow on the ground", RoadPaint.Prewarm);
            ScenePrewarm.Add("seasons: the game's soft round particle being found for the snowflakes", SeasonGame.FindFlakePaint);
            ScenePrewarm.Add("seasons: the trees' leaf materials found (every tree of the city)", SeasonGame.WalkAllTrees);
            _markGround = MarkGround;
            ScenePrewarm.Add(MarkWhat, _markGround);
        }

        private const string MarkWhat = "seasons: the ground, roads, pavements and roofs given the snow's own decal layer";

        // Behind every load while the seasons are on, whatever the snow's look option says: the mark is one rendering
        // layer bit no decal of the game's uses, harmless while no snow lies, so switching the look on in play needs no door.
        private bool MarkGround()
        {
            if (Settings.Seasons && SnowGround.Drawable)
            {
                _markLater = false;
                _look.Ground.Mark();
            }
            else
            {
                _markLater = SnowGround.Drawable;
            }
            return true;
        }

        // The seasons switched back on in play after a load passed with them off: the ground is marked at the next door.
        private void FollowMarks()
        {
            if (_markLater)
            {
                _markLater = false;
                ScenePrewarm.Again(MarkWhat, _markGround);
            }
        }

        private void OnDestroy()
        {
            try
            {
                StopAll();
                _look.Dispose();
                _seasonLook.Dispose();
                SeasonBar.Remove();
                SeasonGame.ForgetFailures();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

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
            _next = now + (_look.Following ? FollowEvery : SlowEvery);
            if (_broken)
            {
                return;
            }
            try
            {
                if (now >= _nextSlow)
                {
                    _nextSlow = now + SlowEvery;
                    Slow(now);
                }
                else if (_look.Following)
                {
                    _look.Follow(SeasonGame.Player);
                }
            }
            catch (Exception e)
            {
                _broken = true;
                Logger?.Error(e);
                Logger?.Warn("Seasons: switched off for this session after an error; the goods' sales and the city's people are as the game has them.");
                StopAll();
            }
        }

        private void Slow(float now)
        {
            if (SeasonGame.Loading())
            {
                return;
            }
            if (!Settings.Seasons || StandDown())
            {
                if (_on)
                {
                    StopAll();
                }
                BarAlone();
                return;
            }
            _on = true;
            int day = SeasonGame.Today, hour = SeasonGame.WholeHour, year = GameCalendar.DaysPerYear;
            float time = SeasonGame.Hour;
            Season = SeasonRules.SeasonOf(day, year);
            Demand(SeasonRules.SeasonOf(SeasonRules.DrawDay(day, hour), year), day);
            int salt = SeasonBridges.Salt != null ? SeasonBridges.Salt() : 0;
            // Until the save's number is read (the events' save line, or the host's in Going Public) no day's snow is known.
            Today = salt != 0 ? SeasonRules.SnowOn(day, year, salt) : default(SnowDay);
            if (Holding)
            {
                Falling = 1f;
                Cover = Mathf.Min(1f, Cover + 0.05f);   // the test's ground whitens over twenty seconds
            }
            else
            {
                Falling = SeasonRules.Falling(Today, time);
                Cover = salt != 0 ? SeasonRules.Cover(day, time, year, salt) : 0f;
            }
            Step = Settings.WeatherMatters ? SeasonRules.Step(Cover) : 0;
            SeasonGame.Walkers(Step == 2 ? WalkCovered : Step == 1 ? WalkPatchy : 1f);
            FollowMarks();
            bool outdoors = SeasonGame.Outdoors;
            if (Settings.SnowLook)
            {
                _look.Show(Falling, Cover, SeasonGame.Player, outdoors);
            }
            else
            {
                _look.Hide();
            }
            Hush(Snowing);
            if (now >= _nextLook || Showing != _lookShowing)
            {
                _nextLook = now + LookEvery;
                _lookShowing = Showing;
                Looks(day, time, year, salt, outdoors);
            }
            if (salt != 0)
            {
                News(day, hour, year, salt);
            }
        }

        /// <summary>Snow falls or lies on the roads here now, or the test's snow is held: it takes the place of rain.</summary>
        public bool Snowing => Showing ? Holding : Falling > 0f || Step > 0 || Holding;   // a test showing another season shows its own rain

        // While snow takes the place of rain, the game's rain is not seen or heard on this machine; its weather goes on.
        private void Hush(bool snowing)
        {
            SeasonGame.HushRain(snowing);
            _hush.Hold(snowing && SeasonGame.GameRaining);
        }

        // The season's figures on the goods: once when it turns, and once after a load.
        private void Demand(int season, int day)
        {
            if (season != _applied)
            {
                Turned(season, day);
            }
        }

        private void Turned(int season, int day)
        {
            float started = Time.realtimeSinceStartup;
            int written = SeasonGame.ApplyDemand(season);
            bool first = _applied < 0;
            _applied = season;
            Logger?.Info("Seasons: " + SeasonText.Name(season) + (first ? " on load" : " from tonight's customers") + " (day " + day + "): " + written + " of "
                         + SeasonRules.GoodCount + " seasonal goods follow it.");
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Seasons check (demand, day " + day + "): " + DemandCheck(season, out bool ok) + ", "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms: " + (ok ? "PASS" : "FAIL"));
            }
        }

        // Every good written is its own times the season's figure: the words and whether all were.
        private static string DemandCheck(int season, out bool ok)
        {
            int held = 0, wrong = 0;
            string first = null;
            for (int g = 0; g < SeasonRules.GoodCount; g++)
            {
                if (!SeasonGame.Ratios(g, out float own, out float now))
                {
                    continue;
                }
                held++;
                if (Mathf.Abs(now - SeasonRules.Ratio(own, g, season)) > 0.0001f)
                {
                    wrong++;
                    first = first ?? SeasonRules.Goods[g];
                }
            }
            ok = held > 0 && wrong == 0;
            return held + " good(s) held for " + SeasonText.Name(season) + ", " + wrong + " not as wanted" + (first != null ? " (first " + first + ")" : string.Empty);
        }

        // The season's first morning, the snow forecast in the morning and the evening before, and the first flakes.
        private void News(int day, int hour, int year, int salt)
        {
            if (hour >= MorningFrom && hour < MorningTo && _toldSeason != day && SeasonRules.BeginsOn(day, year) && day > 1)
            {
                _toldSeason = day;
                MorningNews.Show(NotificationType.Info, "reallife_season_turn", SeasonText.TurnData(Season), 12f, "rlo-season");
            }
            if (!Settings.WeatherMatters)
            {
                return;   // the forecast is the weather's, as its own is
            }
            if (hour >= MorningFrom && hour < MorningTo && _toldForecast != day && Today.Falls && Today.From > hour)
            {
                _toldForecast = day;
                MorningNews.Show(NotificationType.Info, "reallife_season_snow_today", SeasonText.Hours(Today), 10f, "rlo-snow");
            }
            if (hour == EveningAt && _toldTomorrow != day)
            {
                _toldTomorrow = day;
                SnowDay tomorrow = SeasonRules.SnowOn(day + 1, year, salt);
                if (tomorrow.Falls)
                {
                    MorningNews.Show(NotificationType.Info, "reallife_season_snow_tomorrow", SeasonText.Hours(tomorrow), 10f, "rlo-snow");
                }
            }
            if (Falling > 0f && _toldFalling != day && !Holding)
            {
                _toldFalling = day;
                MorningNews.Show(NotificationType.Warning, Today.Heavy ? "reallife_season_snow_heavy" : "reallife_season_snow_light", null, 10f, "rlo-snow");
            }
        }

        /// <summary>Everything as the game has it: the goods' sales, the people's paces, no snow to be seen or felt. Safe at any time.</summary>
        public void StopAll()
        {
            SeasonGame.Reset();
            _on = false;
            _applied = -1;
            _holdUntil = 0f;
            Falling = Cover = 0f;
            Step = 0;
            _look?.Hide();
            if (_hush != null)
            {
                _hush.Hold(false);
            }
            StopLooks();
            if (_look != null && _look.Ground.Marked > 0)
            {
                _look.Ground.Unmark();
                _markLater = true;
            }
        }

        /// <summary>The seasons' line for the Calendar's day page: the season and what sells more and less in it; null while off.</summary>
        public static string CalendarWords(int day)
        {
            if (!Settings.Seasons || StandingDown || Instance == null || SeasonGame.Loading())
            {
                return null;
            }
            return SeasonText.Line(SeasonRules.SeasonOf(day, GameCalendar.DaysPerYear));
        }
    }
}
