using System;
using System.Runtime.CompilerServices;
using BigAmbitions.DayNightCycle;
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
    /// as the game's HUD names it (PlayerHUD's nearest building and district), the figure the game's top bar writes under
    /// its money with its arrow (Topbar.moneyChange: yesterday's result), and the notices the game's list keeps. It stands
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
                Season(m);
                Sky(m);
                Place(m, uis);
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
        private static void Season(UiStatusModel m)
        {
            if (!Settings.Seasons || SaveGameManager.Current == null)
            {
                return;
            }
            int season = SeasonRules.SeasonOf(SeasonGame.Today, GameCalendar.DaysPerYear);
            m.Season = SeasonText.Title(season);
        }

        // The weather in a glyph and a word or two: the mod's snow, its reading of the game's rain (mist before and after a
        // shower, a storm in the heaviest; the game's rain alone while the weather's effects are off), then the day's spell
        // of heat or cold, else the sun by day and a clear night. The game keeps no temperature, so the season and these
        // words say it (m.Temperature stays null).
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
                key = "snow";
            }
            else if (Weather.Sky == Events.Sky.Storm)
            {
                m.Weather = UiIcon.CloudRain;
                key = "storm";
            }
            else if (Weather.Sky == Events.Sky.Mist)
            {
                m.Weather = UiIcon.Cloud;
                key = "mist";
            }
            else if (Weather.Sky == Events.Sky.Rain || RainHelper.isRaining)
            {
                m.Weather = UiIcon.CloudRain;
                key = "rain";
            }
            else
            {
                m.Weather = fair;
                key = Weather.Spell == Events.Spell.HeatWave ? "heat" : Weather.Spell == Events.Spell.ColdSnap ? "cold" : Weather.Spell == Events.Spell.Ice ? "ice" : day ? "sunny" : "clear";
            }
            m.WeatherWords = UiText.T("reallife_ui_hud_weather_" + key);
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

        // The figure the game's top bar writes under its money (yesterday's result, or the mod's day result in its place,
        // Economy/DayResult) and which of its arrows shows; left out while the bar shows none.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MoneyDay(UiStatusModel m, UIs uis)
        {
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
                h = h * 31 + (speed != null ? (speed.Paused ? 1 : 0) + (speed.isFastForwarding ? 2 : 0) + (speed.isTimeControlDisabled ? 4 : 0) : 7);
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
