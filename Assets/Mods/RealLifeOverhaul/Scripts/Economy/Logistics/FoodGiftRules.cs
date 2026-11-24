using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A week of days kept as a ring of seven (the day each slot holds, and its count), the way a place's spoilage keeps its seven days.</summary>
    public sealed class WeekRing
    {
        public const int Days = 7;

        public readonly int[] Stamp = new int[Days];
        public readonly int[] Count = new int[Days];

        public WeekRing()
        {
            for (int i = 0; i < Days; i++)
            {
                Stamp[i] = -1;
            }
        }

        public bool Empty
        {
            get
            {
                for (int i = 0; i < Days; i++)
                {
                    if (Stamp[i] >= 0 && Count[i] != 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>Counts into a day's slot, the slot cleared first when it held an older day.</summary>
        public void Add(int day, int n)
        {
            int i = ((day % Days) + Days) % Days;
            if (Stamp[i] != day)
            {
                Stamp[i] = day;
                Count[i] = 0;
            }
            Count[i] += Math.Max(0, n);
        }

        /// <summary>The count over the seven days to <paramref name="today"/>, today included.</summary>
        public int Sum(int today)
        {
            long n = 0;
            for (int i = 0; i < Days; i++)
            {
                if (Stamp[i] >= 0 && Stamp[i] <= today && Stamp[i] > today - Days)
                {
                    n += Count[i];
                }
            }
            return (int)Math.Min(int.MaxValue, n);
        }

        /// <summary>The count on one day alone.</summary>
        public int On(int day)
        {
            int i = ((day % Days) + Days) % Days;
            return Stamp[i] == day ? Count[i] : 0;
        }
    }

    /// <summary>
    /// Giving food that would spoil to charity, the rules: what is given, what earns anything, and the ceilings that keep it from being worked.
    /// Whole numbers and days only, so every machine reads the same. Game-free; checked offline (EconomyCheck, LogisticsChecks.cs).
    /// </summary>
    public static class FoodGiftRules
    {
        public const int EveningHour = 19;       // from this hour of the day a place gives what would be thrown out at the next turn of the day
        public const int SalesShare = 3;         // per cent of a product's last seven days of sales at the place that earns, a day: an honest shop wastes about a tenth of one day's sales
                                                 // (the week's sales are seven days, so 3 per cent of them is about a fifth of an average day, twice an honest shop's leftovers)
        public const int PlaceStandingWeek = 2;  // points of neighbourhood standing one place earns in seven days
        public const int PlayerStandingWeek = 2; // and all the player's places together, the same as one place can reach: many small places do not multiply it
        public const int StandingDay = 1;        // points a place earns on a day it gave something that earned
        public const int StaffWeek = 2;          // days in seven a place's staff are lifted
        public const int StaffLift = 2;          // points of satisfaction the lift gives each of the place's staff
        public const int MoodWeek = 3;           // days in seven the player's good mood comes

        /// <summary>The pieces of a product that earn: a share of what the place sold of it over the last seven days, never more than was given. 0 with no sales.</summary>
        public static int Earning(int given, int soldWeek)
        {
            if (given <= 0 || soldWeek <= 0)
            {
                return 0;
            }
            return (int)Math.Min(given, (long)soldWeek * SalesShare / 100);
        }

        /// <summary>What a share of the pieces given was worth at what they cost, in cents: whole cents, rounded down, so the shares never add to more than the whole.</summary>
        public static long ValueOf(int pieces, int total, long cents)
        {
            return total <= 0 || pieces <= 0 || cents <= 0 ? 0L : (long)Math.Min(pieces, total) * cents / total;
        }

        /// <summary>
        /// What a place that gave something that earned gets today, within the ceilings: the points of neighbourhood standing, whether its staff are lifted and
        /// whether the player's good mood comes. A place earns each at most once a day; the place's own week, and the player's week over all places, hold the rest.
        /// The rings are written to (a ceiling counts what was really given).
        /// </summary>
        public static void Grant(int today, WeekRing placeStanding, WeekRing placeStaff, WeekRing playerStanding, WeekRing playerMood, out int standing, out bool staff, out bool mood)
        {
            standing = 0;
            staff = false;
            mood = false;
            if (placeStanding.On(today) == 0)
            {
                standing = Math.Max(0, Math.Min(StandingDay, Math.Min(PlaceStandingWeek - placeStanding.Sum(today), PlayerStandingWeek - playerStanding.Sum(today))));
                if (standing > 0)
                {
                    placeStanding.Add(today, standing);
                    playerStanding.Add(today, standing);
                }
            }
            if (placeStaff.On(today) == 0 && placeStaff.Sum(today) < StaffWeek)
            {
                staff = true;
                placeStaff.Add(today, 1);
            }
            if (playerMood.On(today) == 0 && playerMood.Sum(today) < MoodWeek)
            {
                mood = true;
                playerMood.Add(today, 1);
            }
        }

        /// <summary>
        /// The pieces that would be thrown out at the next turn of the day: those due by then and not already thrown out at this one (<paramref name="batches"/> hold only
        /// what is left after the turn), by the same lives and cold share the turn uses, so what is given is exactly what the turn would have thrown out.
        /// </summary>
        public static int DueTomorrow(List<Cohort> batches, int life, int coldLife, int chilled, int today) => SpoilRules.Expired(batches, life, coldLife, chilled, today + 1);
    }
}
