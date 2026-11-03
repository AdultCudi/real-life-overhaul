using BigAmbitions.DayNightCycle;
using Helpers;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Seasons;
using TMPro;
using UI;
using UI.PlayerHUD;
using UnityEngine;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// What the camera knew at the moment of a shot: the place the HUD names, the hour and the weather. Read once, when
    /// the shutter is pressed, and written beside the JPG. Not read on a frame.
    /// </summary>
    internal static class PhotoShot
    {
        public static PhotoSidecar Capture(int filter, int frame)
        {
            var side = new PhotoSidecar { Filter = filter, Frame = frame, Day = PhotosGame.Day, Hour = PhotosGame.Hour, Weather = Token() };
            Place(ref side);
            side.Celsius = Degrees(side.Weather);
            return side;
        }

        public static string Token()
        {
            if (Settings.Seasons && SeasonDirector.Instance != null && SeasonDirector.Instance.Snowing)
            {
                return "snow";
            }
            if (Weather.Sky == Sky.Storm)
            {
                return "storm";
            }
            if (Weather.Sky == Sky.Rain || RainHelper.isRaining)
            {
                return "rain";
            }
            if (Weather.Sky == Sky.Mist)
            {
                return "mist";
            }
            if (Weather.Spell == Spell.HeatWave)
            {
                return "heat";
            }
            if (Weather.Spell == Spell.ColdSnap)
            {
                return "cold";
            }
            if (Weather.Spell == Spell.Ice)
            {
                return "ice";
            }
            int hour = PhotosGame.Hour;
            return hour >= 7 && hour < 19 ? "sunny" : "clear";
        }

        public static string Words(string token)
        {
            switch (token)
            {
                case "rain": return "reallife_ui_hud_weather_rain";
                case "mist": return "reallife_ui_hud_weather_mist";
                case "storm": return "reallife_ui_hud_weather_storm";
                case "snow": return "reallife_ui_hud_weather_snow";
                case "heat": return "reallife_ui_hud_weather_heat";
                case "cold": return "reallife_ui_hud_weather_cold";
                case "ice": return "reallife_ui_hud_weather_ice";
                case "clear": return "reallife_ui_hud_weather_clear";
                default: return "reallife_ui_hud_weather_sunny";
            }
        }

        public static int Degrees(string token)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return 18;
            }
            int sky = token == "storm" ? TemperatureRules.SkyStorm : token == "rain" ? TemperatureRules.SkyRain : token == "mist" ? TemperatureRules.SkyMist : TemperatureRules.SkyDry;
            int spell = token == "heat" ? (int)Spell.HeatWave : token == "cold" ? (int)Spell.ColdSnap : token == "ice" ? (int)Spell.Ice : (int)Spell.None;
            bool snow = token == "snow";
            int salt = SeasonBridges.Salt != null ? SeasonBridges.Salt() : 0;
            return TemperatureRules.Celsius(SeasonGame.Today, GameCalendar.DaysPerYear, save.Hour + save.Minute / 60f, salt, sky, spell, snow);
        }

        private static void Place(ref PhotoSidecar side)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            PlayerHUD hud = uis != null ? uis.playerHUD : null;
            if (hud == null)
            {
                return;
            }
            TMP_Text building = hud.nearestBuildingText != null ? hud.nearestBuildingText.GetComponent<TMP_Text>() : null;
            string near = building != null ? building.text : null;
            string district = hud.currentNeighborhoodText != null ? hud.currentNeighborhoodText.text : null;
            side.Place = string.IsNullOrEmpty(near) ? district : near;
            side.District = district;
        }
    }
}
