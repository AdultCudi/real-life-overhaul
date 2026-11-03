using System;
using System.Runtime.CompilerServices;
using BigAmbitions.DayNightCycle;
using Extensions;
using Helpers;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Seasons;
using TMPro;
using UI;
using UI.PlayerHUD;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The status bar's pills read from the game, mirrored and never worked out again: the game's speed, pause and time
    /// lock (GameSpeedController), the player's energy, hunger and happiness as the game's top bar reads them, the mod's
    /// hygiene, stress and home (HomeState, while the needs are on), the season (the mod's calendar, while the seasons are
    /// on), the weather in a glyph and a word (the mod's snow and its reading of the game's rain, Events.Weather), the place
    /// as the game's HUD names it (PlayerHUD's nearest building and district), today's money so far under the money (less
    /// the last midnight's balance; before one, Topbar.moneyChange: yesterday's result), and the notices the game keeps. It stands
    /// behind UiChrome.Bar, after the time, the day, the money and the app row (UiGame.Bar). A member that fails leaves its
    /// pill out with one line in Player.log. Cost: a few reads each time the bar is laid out; <see cref="Signature"/> a few
    /// loads a frame while the HUD shows.
    /// </summary>
    public static class HudStatusGame
    {
        /// <summary>Set while the HUD's own bar is laid out: only there are the pills targets (UiDraw.HudBar).</summary>
        public static bool ForHud;

        /// <summary>True while the HUD shows: every bar drawn then carries the app row (UiGame.Bar), as the owner wants it while walking.</summary>
        public static bool Shows;

        /// <summary>True while the HUD's All apps stands over it: its tile filled cyan.</summary>
        public static bool AllAppsOpen;

        private static bool _said;
        private static int _widestDay = -1;   // the day the widest day's words were worked out for
        private static string _widest;

        /// <summary>The bar's model: the game's time, day, money and apps, then the HUD's pills.</summary>
        public static UiStatusModel Bar()
        {
            UiStatusModel m = UiGame.Bar();
            if (m == null)
            {
                return null;
            }
            try
            {
                // Each pill from where the game or the mod keeps it, each in its own small method so a member a build lacks fails alone.
                UIs uis = InstanceBehavior<UIs>.Instance;
                Speed(m, uis);
                m.DayWidest = Widest();
                Needs(m);
                Season(m, uis);
                Sky(m);
                Temperature(m);
                Place(m, uis);
                if (ForHud)
                {
                    HudTopGame.Fill(m);   // the top bar's small buttons, on the HUD's own bar alone (appended with the HUD's screens)
                }
                MoneyDay(m, uis);
                m.Alerts = SaveGameManager.Current != null && SaveGameManager.Current.notifications != null ? SaveGameManager.Current.notifications.Count : 0;   // the notices the game's list keeps
            }
            catch (Exception e)
            {
                Say("the pills beyond the time and the money were left out (" + e.GetType().Name + ": " + e.Message + ")");
            }
            m.PillsPress = ForHud;
            m.AllAppsOpen |= AllAppsOpen;
            return m;
        }

        // The widest of the coming week's days in the game's own words, as its top bar writes them (UiGame.Bar writes the day
        // so), worked out once a day, so the time pill keeps one width all week.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Widest()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return null;
            }
            if (save.Day != _widestDay)
            {
                _widestDay = save.Day;
                _widest = null;
                float most = -1f;
                for (int d = save.Day; d < save.Day + 7; d++)
                {
                    string words = RealLifeOverhaul.Planner.PlannerText.Date(d);
                    float w = UiTextRules.Width(words, UiStyle.Small);   // as the time pill writes the day, under the time
                    if (w > most)
                    {
                        most = w;
                        _widest = words;
                    }
                }
            }
            return _widest;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Speed(UiStatusModel m, UIs uis)
        {
            GameSpeedController speed = uis != null ? uis.gameSpeed : null;
            if (speed == null)
            {
                return;
            }
            m.Paused = speed.Paused;
            m.Speed = speed.isFastForwarding ? 3 : 1;   // the game has its normal speed and an activity's fast forward: the play square or the fast forward's
            m.TimeLocked = speed.isTimeControlDisabled;
            m.FastOffered = FastOffered(uis);
        }

        /// <summary>
        /// The game's fast forward is its activity panel's switch, there only while an activity that has it runs inside a building
        /// (PlayerActivityUI, HasFastForward and BuildingManager.IsInsideBuilding); the game's top bar has none.
        /// </summary>
        public static bool FastOffered(UIs uis)
        {
            PlayerActivity.PlayerActivityUI ui = uis != null ? uis.playerActivityUI : null;
            PlayerActivity.IPlayerActivity a = ui != null && PlayerActivity.PlayerActivityUI.IsPanelOpen ? ui.GetCurrentActivity : null;
            return a != null && a.HasFastForward() && BuildingManager.IsInsideBuilding;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Needs(UiStatusModel m)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            m.Energy = Whole(save.Energy);
            m.Hunger = Whole(save.Hunger);
            m.Happiness = Whole(HappinessHelper.Happiness);
            if (Settings.HomeNeeds && HomeState.Loaded)
            {
                m.Hygiene = Whole(HomeState.Hygiene);
                m.Stress = Whole(HomeState.Stress);
                m.HomeNeed = HomeTidy.HasHome ? Whole(HomeState.Tidy) : -1;   // with no home of their own there is no home to keep
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Season(UiStatusModel m, UIs uis)
        {
            if (SaveGameManager.Current == null)
            {
                return;
            }
            if (!Settings.Seasons)
            {
                m.Season = OtherSeason(uis);
                return;
            }
            int season = SeasonRules.SeasonOf(SeasonGame.Today, GameCalendar.DaysPerYear);
            m.Season = SeasonText.Title(season);
        }

        // The weather in a glyph and a word or two: the mod's snow, its reading of the game's rain (mist before and after a
        // shower, a storm in the heaviest; the game's rain alone while the weather's effects are off), then the day's spell
        // of heat or cold, else the sun by day and a clear night. The game keeps no temperature: the HUD's own figure
        // stands over these words (Temperature).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Sky(UiStatusModel m)
        {
            if (SaveGameManager.Current == null)
            {
                return;
            }
            int hour = SaveGameManager.Current.Hour;
            bool day = hour >= 6 && hour < 20;
            SeasonDirector seasons = SeasonDirector.Instance;
            UiIcon fair = day ? UiIcon.Sun : UiIcon.Moon;
            string key;
            if (Settings.Seasons && seasons != null && seasons.Snowing)
            {
                m.Weather = UiIcon.CloudSnow;
                key = "reallife_ui_hud_weather_snow";
            }
            else if (Weather.Sky == Events.Sky.Storm)
            {
                m.Weather = UiIcon.CloudRain;
                key = "reallife_ui_hud_weather_storm";
            }
            else if (Weather.Sky == Events.Sky.Mist)
            {
                m.Weather = UiIcon.Cloud;
                key = "reallife_ui_hud_weather_mist";
            }
            else if (Weather.Sky == Events.Sky.Rain || RainHelper.isRaining)
            {
                m.Weather = UiIcon.CloudRain;
                key = "reallife_ui_hud_weather_rain";
            }
            else
            {
                m.Weather = fair;
                key = Weather.Spell == Events.Spell.HeatWave ? "reallife_ui_hud_weather_heat"
                    : Weather.Spell == Events.Spell.ColdSnap ? "reallife_ui_hud_weather_cold"
                    : Weather.Spell == Events.Spell.Ice ? "reallife_ui_hud_weather_ice"
                    : day ? "reallife_ui_hud_weather_sunny" : "reallife_ui_hud_weather_clear";
            }
            m.WeatherWords = UiText.T(key);
        }

        // Another mod's season (AlcwareSeasons writes its own under the game's date) while the mod's seasons are off: its
        // words as it wrote them, mirrored, never worked out.
        private static string OtherSeason(UIs uis)
        {
            Transform date = uis != null && uis.topBar != null && uis.topBar.dateLabel != null ? uis.topBar.dateLabel.transform : null;
            Transform other = date != null ? date.Find(OtherSeasonName) : null;
            TMP_Text words = other != null && other.gameObject.activeSelf ? other.GetComponentInChildren<TMP_Text>(false) : null;
            return words != null ? UiWidgets.Text(words) : null;
        }

        private const string OtherSeasonName = "Alcware season indicator";

        // The air's temperature over the season and the weather's words (Seasons/TemperatureRules: a figure for the eye from
        // the calendar, the day's roll, the hour and the weather; neither the game nor the mod keeps one), in the game's units.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Temperature(UiStatusModel m)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            SeasonDirector seasons = SeasonDirector.Instance;
            bool snowing = Settings.Seasons && seasons != null && seasons.Snowing;
            int sky = (int)Weather.Sky;
            if (sky == TemperatureRules.SkyDry && RainHelper.isRaining)
            {
                sky = TemperatureRules.SkyRain;   // the game's own rain while the mod's weather is off
            }
            int salt = SeasonBridges.Salt != null ? SeasonBridges.Salt() : 0;
            int celsius = TemperatureRules.Celsius(SeasonGame.Today, GameCalendar.DaysPerYear, save.Hour + save.Minute / 60f, salt, sky, (int)Weather.Spell, snowing);
            m.Temperature = TemperatureRules.Text(celsius, UnitHelper.useImperial);
            if (!string.IsNullOrEmpty(m.Season) && !string.IsNullOrEmpty(m.WeatherWords))
            {
                m.Season = UiText.T("reallife_ui_hud_season_weather").Replace("{season}", m.Season).Replace("{weather}", m.WeatherWords);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Place(UiStatusModel m, UIs uis)
        {
            PlayerHUD hud = uis != null ? uis.playerHUD : null;
            if (hud == null)
            {
                return;
            }
            TMP_Text building = hud.nearestBuildingText != null ? hud.nearestBuildingText.GetComponent<TMP_Text>() : null;
            string near = building != null ? building.text : null;
            string district = hud.currentNeighborhoodText != null ? hud.currentNeighborhoodText.text : null;
            if (!string.IsNullOrEmpty(near))
            {
                m.Place = near;
                m.District = district;
            }
            else if (!string.IsNullOrEmpty(district))
            {
                m.Place = district;
            }
        }

        // Today's money so far under the money ("+$1,240 today"): the money now less the game's balance at the last midnight
        // (GameInstance.midnightBankBalances, saved). Before a save's first midnight, the figure the game's top bar writes
        // under its money (yesterday's result, or the mod's day result, Economy/DayResult) with its arrow; else none.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MoneyDay(UiStatusModel m, UIs uis)
        {
            GameInstance save = SaveGameManager.Current;
            if (save != null && HudNoteRules.Today(save.Money, save.midnightBankBalances, out double net))
            {
                m.MoneyTrend = HudNoteRules.Trend(net);
                string sign = m.MoneyTrend > 0 ? "+" : m.MoneyTrend < 0 ? "-" : string.Empty;
                m.MoneyDay = UiText.T("reallife_ui_hud_money_today").Replace("{amount}", sign + Math.Abs(net).ToShortCurrencyFormat(abbreviated: true));
                return;
            }
            TextMeshProUGUI day = uis != null && uis.topBar != null ? uis.topBar.moneyChange : null;
            if (day == null || !day.gameObject.activeSelf || string.IsNullOrEmpty(day.text))
            {
                return;
            }
            m.MoneyDay = day.text;
            Transform down = day.transform.Find("NegativeArrow");
            m.MoneyTrend = down != null && down.gameObject.activeSelf ? -1 : 1;
        }

        /// <summary>
        /// What the pills show, as one number: the game's minute, the money to the cent, the needs' whole numbers, the pause,
        /// the speed, the time lock, the weather, the figure under the money and the notices kept. The HUD lays its bar out
        /// again only when it changes. Nothing is allocated.
        /// </summary>
        public static int Signature()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return 0;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameSpeedController speed = uis != null ? uis.gameSpeed : null;
            unchecked
            {
                int h = save.Day * 1440 + save.Hour * 60 + (int)save.Minute;
                h = h * 31 + (int)Math.Round(save.Money * 100.0);
                h = h * 31 + Whole(save.Energy) * 10201 + Whole(save.Hunger) * 101 + Whole(HappinessHelper.Happiness);
                h = h * 31 + Whole(HomeState.Hygiene) * 10201 + Whole(HomeState.Stress) * 101 + Whole(HomeState.Tidy);
                h = h * 31 + (speed != null ? (speed.Paused ? 1 : 0) + (speed.isFastForwarding ? 2 : 0) + (speed.isTimeControlDisabled ? 4 : 0) + (FastOffered(uis) ? 8 : 0) : 7);
                h = h * 31 + (save.notifications != null ? save.notifications.Count : 0);
                h = h * 31 + (int)Weather.Sky * 16 + (int)Weather.Spell * 4 + (RainHelper.isRaining ? 2 : 0) + (SeasonDirector.Instance != null && SeasonDirector.Instance.Snowing ? 1 : 0);
                TextMeshProUGUI day = uis != null && uis.topBar != null ? uis.topBar.moneyChange : null;
                h = h * 31 + (day != null && day.gameObject.activeSelf && day.text != null ? day.text.GetHashCode() : 0);
                return h;
            }
        }

        private static int Whole(float value) => Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);

        private static void Say(string what)
        {
            if (!_said)
            {
                _said = true;
                UiDirector.Logger?.Warn("UI: the status bar: " + what + "; it says so once a session.");
            }
        }
    }
}
