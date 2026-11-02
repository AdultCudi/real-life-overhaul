using System;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// The air's temperature the HUD shows beside the weather, worked out from what the city already has: neither the game
    /// nor the mod keeps a temperature, so it is a figure for the eye, never a rule for anything else. The year's curve
    /// follows the calendar (it begins in midwinter, CalendarRules), each day is rolled a little warmer or colder from the
    /// save's own number (SeasonRules.Roll, the same every time), the afternoon is warmer than the small hours, and the
    /// weather moves it: rain and mist cool it, snow and ice keep it at freezing or under, a heat wave or a cold snap lift
    /// or drop the whole day. Nothing is saved. Game-free; checked offline (EconomyCheck).
    /// </summary>
    public static class TemperatureRules
    {
        public const int Mean = 13;              // °C: the year's middle, a city on the sea in a temperate climate
        public const int Swing = 12;             // °C: midsummer's afternoon above it, midwinter's under it
        public const int DayRange = 3;           // °C: a day rolled up to this much warmer or colder
        public const int DaySwing = 4;           // °C: the warmest hour this much over the day's middle, the coldest this much under
        public const int WarmestHour = 15;       // the afternoon's peak; the coldest is twelve hours on
        public const int PurposeDay = 61;        // the day's roll, apart from the snow's own purposes (SeasonRules)
        public const int Rain = -2, Storm = -3, Mist = -1;
        public const int HeatWave = 6, ColdSnap = -7;
        public const int RainLeast = 1;          // °C: rain falls as rain, so never under this while it does
        public const int SnowMost = 0;           // °C: snow lies at freezing or under
        public const int IceMost = -2;
        public const int Least = -25, Most = 45; // °C: the figure never leaves these, whatever is set

        // The sky and the day's spell as the weather keeps them (Events.Sky and Events.Spell, in their order), passed as
        // numbers so these rules stay free of the game.
        public const int SkyDry = 0, SkyMist = 1, SkyRain = 2, SkyStorm = 3;
        public const int SpellNone = 0, SpellHeatWave = 1, SpellColdSnap = 2, SpellIce = 3;

        /// <summary>Whole °C at an hour of a day: the year's curve, the day's roll, the hour and the weather.</summary>
        /// <param name="day">The game's day, 1 on.</param>
        /// <param name="year">The save's year in days (CalendarRules.Year).</param>
        /// <param name="hour">The hour with its minutes as a fraction, 0 to 24.</param>
        /// <param name="salt">The save's own number (SeasonBridges.Salt), 0 when it has none.</param>
        /// <param name="sky">What falls: <see cref="SkyDry"/>, <see cref="SkyMist"/>, <see cref="SkyRain"/> or <see cref="SkyStorm"/>.</param>
        /// <param name="spell">The day's spell: <see cref="SpellNone"/>, <see cref="SpellHeatWave"/>, <see cref="SpellColdSnap"/> or <see cref="SpellIce"/>.</param>
        public static int Celsius(int day, int year, float hour, int salt, int sky, int spell, bool snowing)
        {
            double part = CalendarRules.YearPart(day, year);
            double t = Mean - Swing * Math.Cos(part * 2.0 * Math.PI);   // the year begins in midwinter: its coldest at the start
            t += SeasonRules.Roll(salt, Math.Max(1, day), PurposeDay, DayRange * 2 + 1) - DayRange;
            t += DaySwing * Math.Cos((Hours(hour) - WarmestHour) / 24.0 * 2.0 * Math.PI);
            switch (spell)
            {
                case SpellHeatWave:
                    t += HeatWave;
                    break;
                case SpellColdSnap:
                    t += ColdSnap;
                    break;
            }
            switch (sky)
            {
                case SkyMist:
                    t += Mist;
                    break;
                case SkyRain:
                    t += Rain;
                    break;
                case SkyStorm:
                    t += Storm;
                    break;
            }
            int c = (int)Math.Round(t, MidpointRounding.AwayFromZero);
            if (snowing)
            {
                c = Math.Min(c, SnowMost);
            }
            else if (sky == SkyRain || sky == SkyStorm)
            {
                c = Math.Max(c, RainLeast);
            }
            if (spell == SpellIce)
            {
                c = Math.Min(c, IceMost);
            }
            return Math.Max(Least, Math.Min(Most, c));
        }

        /// <summary>Whole °F from whole °C, rounded as a thermometer reads.</summary>
        public static int Fahrenheit(int celsius) => (int)Math.Round(celsius * 9.0 / 5.0 + 32.0, MidpointRounding.AwayFromZero);

        /// <summary>The figure as the HUD writes it: "18°C", or "64°F" where the game counts in imperial units.</summary>
        public static string Text(int celsius, bool imperial)
        {
            int n = imperial ? Fahrenheit(celsius) : celsius;
            return n.ToString(System.Globalization.CultureInfo.InvariantCulture) + (imperial ? "°F" : "°C");
        }

        private static double Hours(float hour) => float.IsNaN(hour) ? WarmestHour : Math.Max(0f, Math.Min(24f, hour));
    }
}
