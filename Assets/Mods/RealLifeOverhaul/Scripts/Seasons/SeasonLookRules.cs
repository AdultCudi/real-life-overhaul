using System;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>A colour as three factors, 0 to 1 each, that the trees' leaves are multiplied by.</summary>
    public struct Tint
    {
        public float R, G, B;

        public Tint(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public bool Plain => R >= 0.999f && G >= 0.999f && B >= 0.999f;

        public bool Same(Tint other)
        {
            return Math.Abs(R - other.R) < 0.0005f && Math.Abs(G - other.G) < 0.0005f && Math.Abs(B - other.B) < 0.0005f;
        }
    }

    /// <summary>One day's rain of the season's own: none, spring showers, autumn rain or a summer thunderstorm.</summary>
    public struct RainDay
    {
        public bool Falls;
        public int From;     // the hour it starts
        public int Hours;    // how long it lasts
        public bool Storm;   // a summer thunderstorm: heavier, with lightning

        public int To => From + Hours;

        public bool At(float hour) => Falls && hour >= From && hour < To;
    }

    /// <summary>
    /// How the seasons look, game-free (checked by EconomyCheck): the day of the season and how far through it a moment
    /// is, the colour the trees' leaves turn to through the year, the season's own rain (spring showers, autumn rain,
    /// summer thunderstorms) and its lightning, the falling leaves of autumn, and the softer light of a snow day and the
    /// warmer light of summer. Everything comes of the day, the hour and the save's number (<see cref="SeasonRules.Roll"/>),
    /// so every machine of a session sees the same, and nothing is saved.
    /// </summary>
    public static class SeasonLookRules
    {
        public const int Steps = 20;                  // a tint or a light changes in twentieths, so it is written a few times a day at most

        public const int ShowerChance = 300;          // thousandths of a spring day with a shower
        public const int AutumnRainChance = 400;      // thousandths of an autumn day with rain
        public const int StormChance = 180;           // thousandths of a summer day with a thunderstorm
        public const int ShowerHours = 2;             // a shower lasts 1 to this many hours
        public const int AutumnRainHours = 6;         // autumn rain lasts 2 to this many hours
        public const int StormHours = 3;              // a storm lasts 1 to this many hours
        public const int RainFirstHour = 6;           // the season's rain begins between this hour
        public const int RainLastHour = 20;           //   and this one, and is over by midnight
        public const int FlashLeast = 9;              // seconds between flashes of lightning in a storm, at least
        public const int FlashMost = 40;              //   and at most
        public const float LeavesMost = 1f;           // the falling leaves' strength at the height of autumn

        public const int PurposeRain = 61, PurposeRainFrom = 67, PurposeRainHours = 71, PurposeFlash = 73;

        // The leaves' colour through the year, at parts of it (the year begins in midwinter, CalendarRules.Season):
        // bare and dull in winter, a fresh light green in spring, the game's own in summer, amber, red and brown in autumn.
        private static readonly float[] Parts = { 0f, 0.17f, 0.30f, 0.42f, 0.67f, 0.73f, 0.80f, 0.87f, 0.92f, 1f };
        private static readonly Tint[] Colours =
        {
            new Tint(0.78f, 0.72f, 0.62f),   // midwinter: dull and brown
            new Tint(0.80f, 0.76f, 0.64f),   // the end of winter
            new Tint(0.88f, 1f, 0.78f),      // spring: fresh light green
            new Tint(1f, 1f, 1f),            // the game's own, from early summer
            new Tint(1f, 1f, 1f),            //   to its end
            new Tint(1f, 0.80f, 0.45f),      // early autumn: amber
            new Tint(1f, 0.58f, 0.32f),      // mid autumn: red and orange
            new Tint(0.82f, 0.55f, 0.34f),   // late autumn: brown
            new Tint(0.78f, 0.72f, 0.62f),   // winter again
            new Tint(0.78f, 0.72f, 0.62f),
        };

        /// <summary>The day of its season a day is, from 1.</summary>
        public static int SeasonDay(int day, int year)
        {
            return Math.Max(1, day) - SeasonRules.SeasonStart(day, year) + 1;
        }

        /// <summary>How many days the season a day is in lasts (the year's quarters are not all alike).</summary>
        public static int SeasonLength(int day, int year)
        {
            year = Math.Max(1, year);
            int season = SeasonRules.SeasonOf(day, year), start = SeasonRules.SeasonStart(day, year), end = Math.Max(1, day);
            while (end + 1 < start + year && SeasonRules.SeasonOf(end + 1, year) == season)
            {
                end++;
            }
            return end - start + 1;
        }

        /// <summary>How far through the year a moment is, 0 to just under 1, with the hour as a fraction of the day.</summary>
        public static float YearPart(int day, float hour, int year)
        {
            year = Math.Max(1, year);
            float part = (CalendarRules.DayOfYear(day, year) - 1 + Math.Max(0f, Math.Min(23.99f, hour)) / 24f) / year;
            return part - (float)Math.Floor(part);
        }

        /// <summary>The leaves' colour at a moment of the year, in twentieths; the game's own (plain) in summer.</summary>
        public static Tint Foliage(int day, float hour, int year)
        {
            float part = YearPart(day, hour, year);
            int k = 0;
            while (k < Parts.Length - 2 && part >= Parts[k + 1])
            {
                k++;
            }
            float t = (part - Parts[k]) / Math.Max(0.0001f, Parts[k + 1] - Parts[k]);
            t = Math.Max(0f, Math.Min(1f, t));
            return new Tint(Step(Lerp(Colours[k].R, Colours[k + 1].R, t)), Step(Lerp(Colours[k].G, Colours[k + 1].G, t)), Step(Lerp(Colours[k].B, Colours[k + 1].B, t)));
        }

        /// <summary>The season's own rain of a day: spring showers, autumn rain, summer thunderstorms; none in winter, which snows.</summary>
        public static RainDay RainOn(int day, int year, int salt)
        {
            var rain = new RainDay();
            if (day < 1)
            {
                return rain;
            }
            int season = SeasonRules.SeasonOf(day, year);
            int chance = season == CalendarRules.Spring ? ShowerChance : season == CalendarRules.Autumn ? AutumnRainChance : season == CalendarRules.Summer ? StormChance : 0;
            if (chance == 0 || SeasonRules.Roll(salt, day, PurposeRain, SeasonRules.Thousand) >= chance)
            {
                return rain;
            }
            int least = season == CalendarRules.Autumn ? 2 : 1;
            int most = season == CalendarRules.Spring ? ShowerHours : season == CalendarRules.Autumn ? AutumnRainHours : StormHours;
            rain.Falls = true;
            rain.Storm = season == CalendarRules.Summer;
            rain.From = RainFirstHour + SeasonRules.Roll(salt, day, PurposeRainFrom, RainLastHour - RainFirstHour + 1);
            rain.Hours = least + SeasonRules.Roll(salt, day, PurposeRainHours, Math.Max(least, Math.Min(most, 24 - rain.From)) - least + 1);
            return rain;
        }

        /// <summary>The seconds from one flash of lightning to the next, the <paramref name="index"/>th of a storm's day.</summary>
        public static int FlashGap(int day, int salt, int index)
        {
            return FlashLeast + SeasonRules.Roll(salt, day * 977 + index, PurposeFlash, FlashMost - FlashLeast + 1);
        }

        /// <summary>The falling leaves' strength, 0 to 1: autumn only, rising to its middle and dying away by its end.</summary>
        public static float Leaves(int day, float hour, int year)
        {
            if (SeasonRules.SeasonOf(day, year) != CalendarRules.Autumn)
            {
                return 0f;
            }
            int length = SeasonLength(day, year);
            float through = (SeasonDay(day, year) - 1 + Math.Max(0f, Math.Min(23.99f, hour)) / 24f) / Math.Max(1, length);
            float rise = through < 0.5f ? through * 2f : (1f - through) * 2f;
            return Step(Math.Max(0.15f, rise) * LeavesMost);
        }

        /// <summary>
        /// The sun's light as a share of the game's (1 its own): softer while snow falls or lies thick, and a touch more in
        /// summer's warmth. Its colour's warmth is <see cref="Warmth"/>.
        /// </summary>
        public static float Sun(int season, float snowFalling, float cover)
        {
            float snow = Math.Max(Math.Min(1f, snowFalling), Math.Min(1f, cover) * 0.5f);
            return Step(1f - 0.35f * snow);
        }

        /// <summary>How much warmer than the game's the sun's colour is, 0 to 1: summer, not on a stormy hour.</summary>
        public static float Warmth(int season, bool storm)
        {
            return season == CalendarRules.Summer && !storm ? 1f : 0f;
        }

        /// <summary>A sky's greyness for the snow's own overcast, 0 to 1, from how hard it snows and how much lies.</summary>
        public static float Overcast(float snowFalling, float cover)
        {
            return Step(Math.Max(Math.Min(1f, snowFalling), Math.Min(1f, cover) * 0.4f));
        }

        /// <summary>
        /// A day of the first year that shows a season at its most itself, for the test buttons: <paramref name="through"/>
        /// (0 to 1) of the way into it. Day 1 if the year has no such season (it always has all four from 8 days).
        /// </summary>
        public static int ShowDay(int season, int year, float through)
        {
            year = Math.Max(CalendarRules.YearLeast, year);
            for (int day = 1; day <= year; day++)
            {
                // The first day of the season's span in the year (winter's is the one that comes back at the year's end).
                if (SeasonRules.SeasonOf(day, year) == season && (day == 1 ? season != CalendarRules.Winter : SeasonRules.SeasonOf(day - 1, year) != season))
                {
                    int length = SeasonLength(day, year);
                    int shown = day + Math.Min(length - 1, (int)(Math.Max(0f, Math.Min(1f, through)) * length));
                    return CalendarRules.DayOfYear(shown, year);   // winter runs on into the next year's first days
                }
            }
            return 1;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float Step(float value)
        {
            return (float)Math.Round(Math.Max(0f, Math.Min(1f, value)) * Steps) / Steps;
        }
    }
}
