using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// How each season looks, once a second with the rest of the seasons: the trees' leaves, the season's own rain and a
    /// summer storm's lightning, the sky's overcast on snow and summer's warmth, autumn's falling leaves, people dressed
    /// for the cold (off by default) and the season under the top bar's date; and standing down for Alcware - Seasons.
    /// Everything is the day, the hour and the save's number through SeasonLookRules, so every machine sees the same; each
    /// part is written only when what it shows changes. Looked at every few seconds, not every frame. Cost: a few
    /// comparisons each look, a tree walk spread over the first minute of a city, and a flash's few frames in a storm.
    /// </summary>
    public sealed partial class SeasonDirector
    {
        private const float SnowSky = 0.8f;          // of the way to the game's rain sky a heavy snow day's sky goes
        private const float ShowSeconds = 180f;      // a season's test look, held
        private const float ShowerStrength = 0.5f;   // the game's rain from 0.4 to 1
        private const float AutumnStrength = 0.7f;
        private const float StormStrength = 1f;
        private const float TestRainHours = 1f;      // game hours a test's shower or storm lasts
        private const float RainLeast = 0.75f;       // game hours a day's rain must have left to be begun (a load near its end begins none)

        private SeasonLook _seasonLook;
        private float _nextFlash;
        private int _flashDay = -1, _flashIndex;
        private int _showSeason = -1;
        private float _showUntil;
        private string _barLine;
        private int _barDay = -1, _barSeason = -1;
        private bool _alcwareTold;

        /// <summary>Today's rain of the season's own (none in winter).</summary>
        public RainDay Rain { get; private set; }

        /// <summary>A test button holds a season's look where the player stands.</summary>
        public bool Showing => _showSeason >= 0 && _showUntil > Time.unscaledTime;

        /// <summary>
        /// Whether these seasons stand down for Alcware - Seasons: what the calendar lists and says of them stays off with the rest. Asks nothing of
        /// the game and tells nobody; <see cref="StandDown"/> is the one that says so, once.
        /// </summary>
        public static bool StandingDown => SeasonRules.StandsDown(OtherMods.AlcwareSeasons.Loaded, Settings.SeasonBesideAlcware);

        /// <summary>
        /// True while Alcware - Seasons is switched on and these seasons are not kept beside it: everything of theirs stays
        /// off, said once in a notice and the log.
        /// </summary>
        private bool StandDown()
        {
            if (!StandingDown)
            {
                return false;
            }
            if (!_alcwareTold)
            {
                _alcwareTold = true;
                Logger?.Warn("Seasons: Alcware - Seasons is switched on, so these seasons stand down for it (what sells, snow, sky, rain, trees, leaves, coats and the top bar's season); "
                             + "'Keep these seasons with Alcware - Seasons' keeps them on.");
                Notifications.Show(NotificationType.Info, "reallife_season_alcware", null, 12f);
            }
            return true;
        }

        private void Looks(int day, float hour, int year, int salt, bool outdoors)
        {
            bool showing = Showing;
            if (!showing && _showSeason >= 0)
            {
                _showSeason = -1;   // the test's look over: the day's own again
            }
            int season = showing ? _showSeason : Season;
            int lookDay = showing ? SeasonLookRules.ShowDay(_showSeason, year, 0.5f) : day;
            float lookHour = showing ? 12f : hour;

            if (Settings.SeasonTrees)
            {
                SeasonGame.Trees(SeasonLookRules.Foliage(lookDay, lookHour, year));
            }
            else
            {
                SeasonGame.RestoreTrees();
            }

            Rain = salt != 0 ? SeasonLookRules.RainOn(day, year, salt) : default(RainDay);
            bool snowDay = Snowing || (!showing && Today.Falls);   // snow takes the place of rain: the seasons start no shower or storm on a snow day
            bool storm = !snowDay && (showing ? _showSeason == CalendarRules.Summer : Rain.Storm && Rain.At(hour));
            if (!Settings.SeasonRain || snowDay)
            {
                SeasonGame.EndRain();
            }
            else if (!showing && Rain.At(hour) && Rain.To - hour >= RainLeast && !GoingPublic.IsGuest)   // a guest's rain is the host's, which Going Public brings
            {
                SeasonGame.StartRain(Rain.Storm ? StormStrength : Season == CalendarRules.Autumn ? AutumnStrength : ShowerStrength, Rain.To);
            }
            if (Settings.SeasonRain && storm && outdoors && SeasonGame.RainFalling)
            {
                Lightning(day, salt);
            }

            float overcast = Settings.SeasonSky ? SeasonLookRules.Overcast(Falling, Cover) * SnowSky : 0f;
            float warmth = Settings.SeasonSky ? SeasonLookRules.Warmth(season, storm) : 0f;
            SeasonGame.Sky(Mathf.Round(overcast * SeasonLookRules.Steps) / SeasonLookRules.Steps, warmth);

            _seasonLook.Leaf(Settings.SeasonLeaves ? SeasonLookRules.Leaves(lookDay, lookHour, year) : 0f, outdoors && !SeasonGame.RainFalling);
            SeasonGame.Coats(season, Settings.SeasonCoats);
            Bar(showing ? lookDay : day, year, season);
        }

        // Flashes at the storm day's own gaps while drops fall on the player outdoors.
        private void Lightning(int day, int salt)
        {
            float now = Time.unscaledTime;
            if (_flashDay != day)
            {
                _flashDay = day;
                _flashIndex = 0;
                _nextFlash = now + SeasonLookRules.FlashGap(day, salt, _flashIndex++) * 0.3f;
            }
            if (now < _nextFlash)
            {
                return;
            }
            _seasonLook.Flash();
            _nextFlash = now + SeasonLookRules.FlashGap(day, salt, _flashIndex++);
        }

        // The season under the date does not stand down with the rest for Alcware Seasons (it is the calendar's, and shows unless another mod's
        // season is really under the date: SeasonBar). Not while the seasons are off altogether. Called from the once-a-second look, so the
        // season and its day are worked out from the calendar here, the way a look does.
        private void BarAlone()
        {
            if (!Settings.Seasons)
            {
                return;
            }
            int day = SeasonGame.Today, year = GameCalendar.DaysPerYear;
            Bar(day, year, SeasonRules.SeasonOf(day, year));
        }

        // The season and its day under the date, written when the day or the season changes.
        private void Bar(int day, int year, int season)
        {
            if (!Settings.SeasonBar)
            {
                SeasonBar.Hide();
                return;
            }
            if (day != _barDay || season != _barSeason || _barLine == null)
            {
                _barDay = day;
                _barSeason = season;
                _barLine = SeasonText.Bar(season, SeasonLookRules.SeasonDay(day, year), SeasonLookRules.SeasonLength(day, year));
            }
            SeasonBar.Show(_barLine, season);
        }

        /// <summary>Every look of the seasons' put back: the game's trees, sky, rain and clothes, no leaves or flashes, no line under the date.</summary>
        private void StopLooks()
        {
            _seasonLook?.Hide();
            SeasonBar.Hide();
            _showSeason = -1;
            _barLine = null;
            _flashDay = -1;
        }
    }
}
