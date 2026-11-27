using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// How fast the studio's teams build a game, the option "Game development pace" (a player, 25 Nov 2026: "the game development
    /// duration feels a bit too long"). A game's work is what its budget buys (StudioRules.WorkFor) and stays so: only the work a
    /// team puts in each day is multiplied, by <see cref="SpeedOf"/>, so a game already half built is half built at any pace and
    /// moves at the new one from the next day. Normal is the figures as they were, to the last point.
    ///
    /// What a faster pace must not do is make a game cheaper or better for nothing. The same work in fewer days would cost the
    /// team's pay for fewer days, so each building day carries the difference as <see cref="Overhead"/>: tools, contractors and
    /// overtime, in the same proportion as the days saved, which leaves a game's cost where it was. The work, its parts' plan and
    /// the bugs a point of work hides do not change, so a game's quality and its bugs per point are as before. A crunch and a
    /// contractors' month are measured in days, so they shorten by the same share (and a crunch's burnout a day rises to keep its
    /// whole): each keeps the share of the game it always had. Game-free: EconomyCheck's TechPlanChecks hold it.
    /// </summary>
    public static class StudioPace
    {
        public const int Normal = 0, Brisk = 1, Fast = 2, Count = 3;

        /// <summary>The work points a team puts in a day against Normal's, thousandths: Brisk builds a game in two thirds of the days, Fast in half.</summary>
        public static readonly int[] Speed = { 1000, 1500, 2000 };

        public static int Clamp(int pace) => pace < 0 ? Normal : pace >= Count ? Count - 1 : pace;

        public static int SpeedOf(int pace) => Speed[Clamp(pace)];

        /// <summary>A day's work points at a pace: the team's points a day with the pace's speed on them.</summary>
        public static long Points(long pointsADay, int pace) => pointsADay * SpeedOf(pace) / 1000;

        /// <summary>Days the work of a game takes at a pace for a team of so many points a day (rounded up); 0 with no points.</summary>
        public static int Days(long work, long pointsADay, int pace) => StudioRules.DaysLeft(work, Points(pointsADay, pace));

        /// <summary>Days a Normal duration becomes at a pace, rounded to the nearest, never under one.</summary>
        public static int Shorten(int normalDays, int pace)
        {
            int speed = SpeedOf(pace);
            return Math.Max(1, (int)(((long)normalDays * 1000 + speed / 2) / speed));
        }

        /// <summary>The days a crunch lasts at a pace.</summary>
        public static int CrunchDays(int pace) => Shorten(StudioCatalogue.CrunchDays, pace);

        /// <summary>The days a month of contractors lasts at a pace.</summary>
        public static int BoostDays(int pace) => Shorten(StudioCatalogue.BoostDays, pace);

        /// <summary>Burnout a day worked in a crunch at a pace: the whole a crunch always cost, spread over its shorter days.</summary>
        public static int CrunchBurnout(int pace)
        {
            int days = CrunchDays(pace);
            return (StudioCatalogue.CrunchBurnout * StudioCatalogue.CrunchDays + days / 2) / days;
        }

        /// <summary>
        /// What a building day costs beyond the team's pay at a pace, cents at the start's prices: the pay for the days the pace saves,
        /// spread over the days it takes, so a game costs the same at every pace. None at Normal.
        /// </summary>
        public static long Overhead(long wagesShare, int pace)
        {
            int speed = SpeedOf(pace);
            return Math.Max(0L, wagesShare) * (speed - 1000) / 1000;
        }

        /// <summary>The option's name for a pace, the key of its drop-down entry.</summary>
        public static string NameKey(int pace) => NameKeys[Clamp(pace)];

        private static readonly string[] NameKeys = { "reallife_game_pace_0", "reallife_game_pace_1", "reallife_game_pace_2" };

        /// <summary>Days as game years of <paramref name="yearDays"/> days with one decimal place, "1.9": what a player counts a long build in.</summary>
        public static string Years(int days, int yearDays)
        {
            long tenths = ((long)Math.Max(0, days) * 10 + Math.Max(1, yearDays) / 2) / Math.Max(1, yearDays);
            return (tenths / 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Thousandths a building day costs more than at Normal (the pay and its overhead over the pay).</summary>
        public static int DearerBy(int pace) => SpeedOf(pace) - 1000;

        /// <summary>Thousandths of the days a game takes at Normal that it takes at a pace: the share a player sees saved is the rest.</summary>
        public static int TakesShare(int pace) => (int)(1000000L / SpeedOf(pace));
    }
}
