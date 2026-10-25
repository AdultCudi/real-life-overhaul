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
    /// the snow's two figures worked out and handed to the weather, the walkers and the look; the goods written once a season.
    /// </summary>
    public sealed partial class SeasonDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("seasons", "SeasonDirector", typeof(SeasonDirector));
        private static readonly int HotUpdate = HotSpots.Slot("Seasons: SeasonDirector.Update");

        private const float WalkPatchy = 0.93f;   // of the game's paces, on snow in patches
        private const float WalkCovered = 0.85f;  // on covered streets
        private const int MorningFrom = 7, MorningTo = 10, EveningAt = 18;   // hours the season's and the snow's news is given

        public static IModLogger Logger;
        public static SeasonDirector Instance { get; private set; }

        private SnowLook _look;
        private float _next;
        private bool _broken;
        private bool _on;                // something of the seasons may be on the city: StopAll has work to do
        private int _applied = -1;       // the season whose figures are on the goods; -1 for none
        private int _toldSeason = -1, _toldForecast = -1, _toldTomorrow = -1, _toldFalling = -1;   // days each piece of news was given on
        private float _holdUntil;        // the test's snow, while the real time is before this
        private bool _markLater;         // the ground was left unmarked (an option off): marked at the next door once wanted

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
            // The snow on the ground copies the game's road paint, which can only be looked for behind a screen.
            ScenePrewarm.Add("seasons: the game's road paint being found for the snow on the ground", RoadPaint.Prewarm);
            ScenePrewarm.Add(MarkWhat, MarkGround);
        }

        private const string MarkWhat = "seasons: the ground, roads, pavements and roofs given the snow's own decal layer";

        // Behind a screen: the only place the snow may lie is marked, while the snow's look is wanted and can be drawn.
        private bool MarkGround()
        {
            if (Settings.Seasons && Settings.SnowLook && SnowGround.Drawable)
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

        // The ground's marks follow the options: let go with the look switched off, asked for at the next door once on.
        private void FollowMarks(bool wanted)
        {
            if (!wanted && _look.Ground.Marked > 0)
            {
                _look.Ground.Unmark();
                _markLater = true;
            }
            else if (wanted && _markLater)
            {
                _markLater = false;
                ScenePrewarm.Again(MarkWhat, MarkGround);
            }
        }

        private void OnDestroy()
        {
            try
            {
                StopAll();
                _look.Dispose();
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
                Logger?.Warn("Seasons: switched off for this session after an error; the goods' sales and the city's people are as the game has them.");
                StopAll();
            }
        }

        private void Slow()
        {
            if (SeasonGame.Loading())
            {
                return;
            }
            if (!Settings.Seasons)
            {
                if (_on)
                {
                    StopAll();
                }
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
            FollowMarks(Settings.SnowLook);
            if (Settings.SnowLook)
            {
                _look.Show(Falling, Cover, SeasonGame.Player, SeasonGame.Outdoors);
            }
            else
            {
                _look.Hide();
            }
            if (salt != 0)
            {
                News(day, hour, year, salt);
            }
        }

        // The season's figures on the goods: once when it turns, and once after a load.
        private void Demand(int season, int day)
        {
            if (season == _applied)
            {
                return;
            }
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
            if (_look != null && _look.Ground.Marked > 0)
            {
                _look.Ground.Unmark();
                _markLater = true;
            }
        }

        /// <summary>The seasons' line for the Calendar's day page: the season and what sells more and less in it; null while off.</summary>
        public static string CalendarWords(int day)
        {
            if (!Settings.Seasons || Instance == null || SeasonGame.Loading())
            {
                return null;
            }
            return SeasonText.Line(SeasonRules.SeasonOf(day, GameCalendar.DaysPerYear));
        }
    }
}
