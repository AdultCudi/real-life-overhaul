using System;
using System.Collections.Generic;
using System.Reflection;
using BigAmbitions.DayNightCycle;
using GleyTrafficSystem;
using Helpers;
using NWH.VehiclePhysics2.Powertrain;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>What falls from the sky. The game itself only has rain; mist is its dry spell before and after a shower, a storm its heaviest showers.</summary>
    public enum Sky { Dry, Mist, Rain, Storm }

    /// <summary>A spell of weather that lasts the day. The game shows none of these; they are felt, forecast and warned of.</summary>
    public enum Spell { None, HeatWave, ColdSnap, Ice }

    public enum Season { Winter, Spring, Summer, Autumn }

    /// <summary>Asked, if anybody has set it, whether the player is dressed for the cold. Null means nobody knows: half the chill is felt.</summary>
    public interface IWardrobe
    {
        bool WearingCoat { get; }
    }

    /// <summary>
    /// Weather that matters. The game already draws its weather (rain, wet roads, puddles, the
    /// murk before a shower); nothing here changes how any of it looks. This turns what the game
    /// has, and a spell of heat or cold rolled for the day from the season, into a handful of
    /// numbers the rest of the mod reads:
    ///
    ///     Events.Weather.AccidentFactor       multiply the chance of a traffic accident by it
    ///     Events.Weather.CrimeFactor          ...of street crime (tempers in the heat)
    ///     Events.Weather.SightFactor          multiply how far the police and witnesses see by it
    ///     Events.Weather.StreetLifeFactor     how much of the pavement's life is out; Events.Weather.TakeCover in a storm
    ///     Events.Weather.GripFactor           the player's tyres (applied by WeatherEffects)
    ///     Events.Weather.TrafficSpeedFactor   how fast traffic chooses to go (applied by WeatherEffects)
    ///
    /// Every one is a plain static float, 1 in fair weather and with the option off, written
    /// only when the weather changes (looked at once a second: two flags and the hour).
    /// </summary>
    public static class Weather
    {
        public static float AccidentFactor = 1f;
        public static float CrimeFactor = 1f;
        public static float SightFactor = 1f;
        public static float StreetLifeFactor = 1f;
        public static float GripFactor = 1f;
        public static float TrafficSpeedFactor = 1f;

        /// <summary>A thunderstorm is on: people run for cover and stalls close.</summary>
        public static bool TakeCover;

        public static Sky Sky { get; private set; }
        public static Spell Spell { get; private set; }
        public static Season Season { get; private set; }

        /// <summary>Set by whoever knows what the player wears (optional).</summary>
        public static IWardrobe Wardrobe;

        /// <summary>True while a test button holds the weather: the game's own is not looked at.</summary>
        public static bool Forced { get; private set; }

        private static FieldInfo _intensityField;
        private static bool _intensitySearched;

        /// <summary>The season a day falls in: the year starts in midwinter, as the calendar's does (GameCalendar, CalendarRules.Season).</summary>
        public static Season SeasonOf(int day) => GameCalendar.SeasonOf(day);

        /// <summary>
        /// The spell rolled for a day: the same answer every time it is asked, so nothing is saved
        /// and tomorrow's can be forecast. Heat only in summer, cold only in winter (ice on a cold
        /// day in one of three); how often is the option's slider.
        /// </summary>
        public static Spell SpellOf(int day)
        {
            if (!Settings.WeatherMatters)
            {
                return Spell.None;
            }
            Season season = SeasonOf(day);
            if (season != Season.Summer && season != Season.Winter)
            {
                return Spell.None;
            }
            float roll = EventCalendar.Roll(day, 17);
            if (roll >= Settings.WeatherFrequency * 0.045f)
            {
                return Spell.None;
            }
            return season == Season.Summer ? Spell.HeatWave : EventCalendar.Roll(day, 29) < 0.34f ? Spell.Ice : Spell.ColdSnap;
        }

        /// <summary>Test buttons: holds the sky and the spell as given until <see cref="Release"/>.</summary>
        internal static void Force(Sky sky, Spell spell)
        {
            Forced = true;
            Set(sky, spell, SeasonOf(TimeHelper.CurrentDay));
        }

        internal static void Release()
        {
            Forced = false;
        }

        /// <summary>Everything back to fair weather: the option is off, or the city is unloading.</summary>
        internal static void Reset()
        {
            Forced = false;
            Set(Sky.Dry, Spell.None, Season.Spring);
        }

        /// <summary>Reads the game's weather and today's spell. True if anything changed.</summary>
        internal static bool Read()
        {
            if (Forced)
            {
                return false;
            }
            if (!Settings.WeatherMatters)
            {
                return Set(Sky.Dry, Spell.None, Season);
            }
            Sky sky = Sky.Dry;
            if (RainHelper.isRaining)
            {
                sky = !RainHelper.AreRainDropsFalling ? Sky.Mist : Intensity() >= 0.8f ? Sky.Storm : Sky.Rain;
            }
            int day = TimeHelper.CurrentDay;
            return Set(sky, SpellOf(day), SeasonOf(day));
        }

        private static bool Set(Sky sky, Spell spell, Season season)
        {
            if (sky == Sky && spell == Spell && season == Season)
            {
                return false;
            }
            Sky = sky;
            Spell = spell;
            Season = season;
            float accident = 1f, crime = 1f, sight = 1f, life = 1f, grip = 1f, speed = 1f;
            switch (sky)
            {
                case Sky.Mist: accident = 1.3f; sight = 0.6f; grip = 0.95f; speed = 0.92f; break;
                case Sky.Rain: accident = 1.5f; sight = 0.85f; life = 0.6f; grip = 0.82f; speed = 0.88f; crime = 0.85f; break;
                case Sky.Storm: accident = 1.9f; sight = 0.7f; life = 0f; grip = 0.72f; speed = 0.8f; crime = 0.7f; break;
            }
            switch (spell)
            {
                case Spell.HeatWave: crime *= 1.3f; life *= 0.85f; break;
                case Spell.ColdSnap: crime *= 0.85f; life *= 0.8f; break;
                case Spell.Ice: accident *= 1.4f; grip *= 0.75f; speed *= 0.9f; life *= 0.7f; break;
            }
            AccidentFactor = accident;
            CrimeFactor = crime;
            SightFactor = sight;
            StreetLifeFactor = life;
            GripFactor = grip;
            TrafficSpeedFactor = speed;
            TakeCover = sky == Sky.Storm;
            return true;
        }

        // How hard the shower that is on will get, 0.4 to 1: the game keeps it to itself. 0.6 if it cannot be read.
        private static float Intensity()
        {
            if (!_intensitySearched)
            {
                _intensitySearched = true;
                _intensityField = GameContract.RainIntensity.Field;   // (resolved once: Core/GameContract)
                if (_intensityField == null || _intensityField.FieldType != typeof(float))
                {
                    _intensityField = null;
                    EventDirector.Logger?.Info("Weather: how heavy a shower is cannot be read from the game; no shower counts as a thunderstorm.");
                }
            }
            try
            {
                return _intensityField != null ? (float)_intensityField.GetValue(null) : 0.6f;
            }
            catch (Exception)
            {
                _intensityField = null;
                return 0.6f;
            }
        }
    }

    /// <summary>
    /// Puts the weather's numbers to work where no other feature does: the player's tyres, how
    /// fast traffic chooses to drive, energy lost in the heat and the cold, the forecast and the
    /// warnings. Stepped by the director once a second; on most of those steps it compares two
    /// flags and returns. Everything it changes on a game object is recorded and put back
    /// exactly (when the weather clears, the player changes car, the option goes off, the city
    /// unloads).
    /// </summary>
    internal sealed class WeatherEffects
    {
        private const float EnergyFloor = 15f;

        private static readonly string[] ForecastFair =
        {
            "reallife_weather_forecast_fair_a", "reallife_weather_forecast_fair_b", "reallife_weather_forecast_fair_c"
        };

        // The driven car's tyres as they were.
        private readonly List<WheelComponent> _wheels = new List<WheelComponent>(6);
        private readonly List<float> _lateral = new List<float>(6);
        private readonly List<float> _longitudinal = new List<float>(6);
        private CarController _car;
        private float _gripApplied = 1f;

        // Traffic's own speed range per pooled vehicle, as it was.
        private readonly List<VehicleComponent> _slowed = new List<VehicleComponent>(64);
        private readonly List<int> _slowedMin = new List<int>(64);
        private readonly List<int> _slowedMax = new List<int>(64);
        private float _speedApplied = 1f;

        private int _lastHour = -1;
        private int _lastForecastDay = -1;
        private bool _gripFailed;
        private bool _speedFailed;

        public int LastForecastDay
        {
            get => _lastForecastDay;
            set => _lastForecastDay = value;
        }

        /// <summary>Once a second while a save is loaded.</summary>
        public void Tick(EventDirector director)
        {
            int hour = TimeHelper.CurrentHour;
            if (Weather.Read())
            {
                Changed();
            }
            ApplySpeed();   // a comparison unless traffic was not running when the weather changed
            Tyres();
            if (hour != _lastHour)
            {
                _lastHour = hour;
                Hourly(director, hour);
            }
        }

        /// <summary>The weather's numbers have just changed: said once, and traffic's pace follows.</summary>
        public void Changed()
        {
            Announce();
            ApplySpeed();
            EventDirector.Logger?.Info(FormattableString.Invariant($"Weather: {Weather.Sky}, {Weather.Spell}, {Weather.Season}: accidents x{Weather.AccidentFactor:F2}, crime x{Weather.CrimeFactor:F2}, sight x{Weather.SightFactor:F2}, ")
                                       + FormattableString.Invariant($"street life x{Weather.StreetLifeFactor:F2}, grip x{Weather.GripFactor:F2}, traffic speed x{Weather.TrafficSpeedFactor:F2}."));
        }

        /// <summary>Everything this changed is put back. Safe at any time.</summary>
        public void Restore()
        {
            RestoreTyres();
            RestoreSpeed();
        }

        // ---------------------------------------------------------------- the player's tyres

        private void Tyres()
        {
            if (_gripFailed)
            {
                return;
            }
            try
            {
                var game = InstanceBehavior<GameManager>.Instance;
                CarController car = game != null && PlayerHelper.IsUsingVehicle ? game.selectedVehicle as CarController : null;
                float wanted = car != null ? Weather.GripFactor : 1f;
                if (car == _car && Mathf.Approximately(wanted, _gripApplied))
                {
                    return;
                }
                RestoreTyres();
                if (car == null || Mathf.Approximately(wanted, 1f) || car.vehicleController == null || car.vehicleController.powertrain == null)
                {
                    return;
                }
                List<WheelComponent> wheels = car.vehicleController.powertrain.wheels;
                for (int i = 0; wheels != null && i < wheels.Count; i++)
                {
                    WheelComponent wheel = wheels[i];
                    if (wheel == null || wheel.wheelUAPI == null)
                    {
                        continue;
                    }
                    _wheels.Add(wheel);
                    _lateral.Add(wheel.wheelUAPI.LateralFrictionGrip);
                    _longitudinal.Add(wheel.wheelUAPI.LongitudinalFrictionGrip);
                    wheel.wheelUAPI.LateralFrictionGrip *= wanted;
                    wheel.wheelUAPI.LongitudinalFrictionGrip *= wanted;   // braking as well as traction: the stop gets longer
                }
                _car = car;
                _gripApplied = wanted;
            }
            catch (Exception e)
            {
                _gripFailed = true;
                EventDirector.Logger?.Error(e);
                EventDirector.Logger?.Warn("Weather: the driven car's tyres could not be read; wet roads no longer change its grip this session.");
                RestoreTyres();
            }
        }

        private void RestoreTyres()
        {
            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelComponent wheel = _wheels[i];
                try
                {
                    if (wheel != null && wheel.wheelUAPI != null)
                    {
                        wheel.wheelUAPI.LateralFrictionGrip = _lateral[i];
                        wheel.wheelUAPI.LongitudinalFrictionGrip = _longitudinal[i];
                    }
                }
                catch (Exception e)
                {
                    EventDirector.Logger?.Error(e);
                }
            }
            _wheels.Clear();
            _lateral.Clear();
            _longitudinal.Clear();
            _car = null;
            _gripApplied = 1f;
        }

        // ---------------------------------------------------------------- traffic's pace

        // The traffic system has no speed factor; each pooled vehicle draws its top speed from its own range when it is
        // put on the road. That range is narrowed here, so the cars that set off in the wet do so more carefully.
        private void ApplySpeed()
        {
            float wanted = Weather.TrafficSpeedFactor;
            if (_speedFailed || Mathf.Approximately(wanted, _speedApplied))
            {
                return;
            }
            try
            {
                if (!Mathf.Approximately(wanted, 1f) && (!TrafficManager.HasInstance || !TrafficManager.IsInitialized))
                {
                    return;   // nothing to slow yet: asked again next second
                }
                RestoreSpeed();
                if (Mathf.Approximately(wanted, 1f))
                {
                    return;
                }
                List<VehicleComponent> pool = Manager.GetVehicleList();
                for (int i = 0; pool != null && i < pool.Count; i++)
                {
                    VehicleComponent car = pool[i];
                    if (car == null)
                    {
                        continue;
                    }
                    _slowed.Add(car);
                    _slowedMin.Add(car.minPossibleSpeed);
                    _slowedMax.Add(car.maxPossibleSpeed);
                    car.minPossibleSpeed = Mathf.Max(20, Mathf.RoundToInt(car.minPossibleSpeed * wanted));
                    car.maxPossibleSpeed = Mathf.Max(car.minPossibleSpeed + 1, Mathf.RoundToInt(car.maxPossibleSpeed * wanted));
                }
                _speedApplied = wanted;
            }
            catch (Exception e)
            {
                _speedFailed = true;
                EventDirector.Logger?.Error(e);
                RestoreSpeed();
            }
        }

        private void RestoreSpeed()
        {
            for (int i = 0; i < _slowed.Count; i++)
            {
                if (_slowed[i] != null)
                {
                    _slowed[i].minPossibleSpeed = _slowedMin[i];
                    _slowed[i].maxPossibleSpeed = _slowedMax[i];
                }
            }
            _slowed.Clear();
            _slowedMin.Clear();
            _slowedMax.Clear();
            _speedApplied = 1f;
        }

        // ---------------------------------------------------------------- once an hour

        private void Hourly(EventDirector director, int hour)
        {
            if (!Settings.WeatherMatters || SaveGameManager.Current == null)
            {
                return;
            }
            int day = TimeHelper.CurrentDay;
            if (hour >= 7 && hour <= 9 && _lastForecastDay != day)
            {
                _lastForecastDay = day;
                Forecast(director, day);
            }
            // Out of doors in the heat of the day, or in the cold at any hour, costs a little energy; never the last of it.
            bool outside = !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle;
            if (outside && SaveGameManager.Current.Energy > EnergyFloor)
            {
                float cost = 0f;
                if (Weather.Spell == Spell.HeatWave && hour >= 11 && hour < 19)
                {
                    cost = 1.5f;
                }
                else if (Weather.Spell == Spell.ColdSnap || Weather.Spell == Spell.Ice)
                {
                    cost = Weather.Wardrobe == null ? 1f : Weather.Wardrobe.WearingCoat ? 0f : 2f;
                }
                if (cost > 0f)
                {
                    EnergyHelper.SpentEnergyOnce(cost);
                }
            }
            if (Weather.Sky == Sky.Storm && Random.value < 0.12f)
            {
                // A power cut is only news: the game has no safe way to switch a district's shops off.
                Notifications.Show(NotificationType.Warning, Random.value < 0.5f ? "reallife_weather_power_cut_a" : "reallife_weather_power_cut_b", null, 9f);
            }
        }

        private static void Forecast(EventDirector director, int day)
        {
            Spell spell = Weather.SpellOf(day);
            string key = spell == Spell.HeatWave ? "reallife_weather_forecast_heat" : spell == Spell.ColdSnap ? "reallife_weather_forecast_cold" : spell == Spell.Ice ? "reallife_weather_forecast_ice"
                : Weather.Sky == Sky.Storm ? "reallife_weather_forecast_storm" : Weather.Sky == Sky.Rain ? "reallife_weather_forecast_rain" : Weather.Sky == Sky.Mist ? "reallife_weather_forecast_mist"
                : SeasonLine(Weather.SeasonOf(day), day);
            Notifications.Show(NotificationType.Info, key, null, 9f);
            director.Bulletin(key, null);
        }

        // A fair day's line: one for the season on the first fair morning of three, a general one otherwise.
        private static string SeasonLine(Season season, int day)
        {
            int pick = day % 3;
            if (pick == 0)
            {
                return season == Season.Winter ? "reallife_weather_forecast_winter" : season == Season.Spring ? "reallife_weather_forecast_spring"
                    : season == Season.Summer ? "reallife_weather_forecast_summer" : "reallife_weather_forecast_autumn";
            }
            return ForecastFair[(day / 3) % ForecastFair.Length];
        }

        // A change for the worse is said once, as it happens.
        private static void Announce()
        {
            if (!Settings.WeatherMatters && !Weather.Forced)
            {
                return;
            }
            string key = Weather.Sky == Sky.Storm ? "reallife_weather_warning_storm" : Weather.Spell == Spell.Ice ? "reallife_weather_warning_ice"
                : Weather.Sky == Sky.Rain ? "reallife_weather_warning_rain" : Weather.Sky == Sky.Mist ? "reallife_weather_warning_mist"
                : Weather.Spell == Spell.HeatWave ? "reallife_weather_warning_heat" : Weather.Spell == Spell.ColdSnap ? "reallife_weather_warning_cold" : null;
            if (key != null)
            {
                Notifications.Show(Weather.Sky == Sky.Storm || Weather.Spell == Spell.Ice ? NotificationType.Warning : NotificationType.Info, key, null, 8f, "rlo-weather");
            }
        }
    }
}
