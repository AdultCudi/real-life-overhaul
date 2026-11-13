using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One place's spoilage: the batches of each product that spoils, and the last seven days' pieces in and spoiled.</summary>
    public sealed class SpoilPlace
    {
        public const int Days = 7;

        public readonly Dictionary<string, List<Cohort>> Batches = new Dictionary<string, List<Cohort>>(StringComparer.Ordinal);
        public readonly int[] Stamp = new int[Days];      // the day each slot of the two rings below holds
        public readonly int[] Spoiled = new int[Days];    // pieces spoiled that day
        public readonly int[] Came = new int[Days];       // pieces come in that day
        public readonly int[] Cents = new int[Days];      // what the spoiled pieces had cost
        public int Pace = SpoilRules.PaceUsual;           // the pace the batches' days were last read at (a change of the option makes the place's batches read again)
        public int Turned = -1;                           // the last day whose turn was looked at (its goods past their life taken out)
        public readonly Dictionary<string, int> Lives = new Dictionary<string, int>(StringComparer.Ordinal);   // days each product keeps here, as last looked at (not saved)

        public SpoilPlace()
        {
            for (int i = 0; i < Days; i++)
            {
                Stamp[i] = -1;
            }
        }

        public List<Cohort> Of(string item)
        {
            if (!Batches.TryGetValue(item, out List<Cohort> b))
            {
                b = new List<Cohort>(SpoilRules.CohortsMost + 1);
                Batches[item] = b;
            }
            return b;
        }

        /// <summary>Counts pieces into a day of the rings, the slot cleared first when it held an older day.</summary>
        public void Note(int day, int spoiled, int came, int cents)
        {
            int i = ((day % Days) + Days) % Days;
            if (Stamp[i] != day)
            {
                Stamp[i] = day;
                Spoiled[i] = Came[i] = Cents[i] = 0;
            }
            Spoiled[i] += Math.Max(0, spoiled);
            Came[i] += Math.Max(0, came);
            Cents[i] += Math.Max(0, cents);
        }

        /// <summary>Pieces spoiled, come in and the spoiled cost over the seven days to <paramref name="today"/>, or on that day alone.</summary>
        public void Sum(int today, bool dayOnly, out int spoiled, out int came, out int cents)
        {
            spoiled = came = cents = 0;
            for (int i = 0; i < Days; i++)
            {
                if (Stamp[i] >= 0 && Stamp[i] <= today && (dayOnly ? Stamp[i] == today : Stamp[i] > today - Days))
                {
                    spoiled += Spoiled[i];
                    came += Came[i];
                    cents += Cents[i];
                }
            }
        }
    }

    /// <summary>
    /// Food spoilage's state in a save: each of the player's places that holds goods that spoil, by its address key, and the
    /// last day a notice was given. Game-free; written by <see cref="SpoilStore"/>.
    /// </summary>
    public sealed class SpoilState
    {
        public readonly Dictionary<string, SpoilPlace> Places = new Dictionary<string, SpoilPlace>(StringComparer.Ordinal);
        public int Told = -1;

        public SpoilPlace Place(string key)
        {
            if (!Places.TryGetValue(key, out SpoilPlace p))
            {
                p = new SpoilPlace();
                Places[key] = p;
            }
            return p;
        }
    }
}
