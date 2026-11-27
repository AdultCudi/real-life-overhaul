using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What Flare's batch rule did at one business when a manager answered its waiting reviews (the bridge's answer). Game-free.</summary>
    public struct ReviewRun
    {
        public bool Ok;                    // Flare could be asked: it is running and the place is one of the player's
        public int Thanks, Sorry, Mixed;   // answers made
        public int Left;                   // poor reviews out of line with the rating: left for the player to judge
        public int Skipped;                // answers due that a mistake of the manager's did not get to today
        public int Before, After;          // the business's rating, thousandths, before and after

        public int Answers => Thanks + Sorry + Mixed;
    }

    /// <summary>A business a manager answered reviews at this week, with its rating at the first answer.</summary>
    public struct ReviewPlace
    {
        public string Place;
        public int Before;
    }

    /// <summary>
    /// The week's reviews a manager answered, kept for the weekly notice (Sunday evening): how many, how each was answered and the rating at each business
    /// when the first answer of the week went up. Saved with the managers as the part "w" (ManagerStore), never more than <see cref="ManagerReviews.PlacesMost"/>
    /// businesses. Nothing here is money: the manager's allowance is the cost of the duty.
    /// </summary>
    public sealed class ReviewWeek
    {
        public int From;                   // the day of the week's first answer; 0 for none yet
        public int Thanks, Sorry, Mixed;
        public readonly List<ReviewPlace> Places = new List<ReviewPlace>(4);

        public int Answered => Thanks + Sorry + Mixed;
    }

    /// <summary>The weekly record of a manager's answers to reviews, game-free (EconomyCheck compiles it).</summary>
    public static class ManagerReviews
    {
        public const int PlacesMost = 32;     // businesses a week's record follows: the saved line stays bounded
        public const int CountMost = 99999;   // answers a week's record counts at most

        /// <summary>Whether the duty does anything at all: its option is on and Flare can answer. With either missing nothing is answered, said or counted, and the duty waits.</summary>
        public static bool May(bool option, bool flareReady)
        {
            return option && flareReady;
        }

        /// <summary>Whether the week's notice is told at this hour of this day: Sunday, at the hour of the day's digest.</summary>
        public static bool Due(int day, int hour)
        {
            return ManagerRules.Weekday(day) == ManagerCatalogue.Sunday && hour == ManagerCatalogue.DigestHour;
        }

        /// <summary>One round's answers into the week: the counts, and the business's rating when it is the first time this week it is answered at.</summary>
        public static void Record(ReviewWeek w, int day, string place, int before, int thanks, int sorry, int mixed)
        {
            if (w == null || thanks + sorry + mixed <= 0)
            {
                return;
            }
            if (w.From <= 0)
            {
                w.From = Math.Max(1, day);
            }
            w.Thanks = Math.Min(CountMost, w.Thanks + Math.Max(0, thanks));
            w.Sorry = Math.Min(CountMost, w.Sorry + Math.Max(0, sorry));
            w.Mixed = Math.Min(CountMost, w.Mixed + Math.Max(0, mixed));
            for (int i = 0; i < w.Places.Count; i++)
            {
                if (string.Equals(w.Places[i].Place, place, StringComparison.Ordinal))
                {
                    return;
                }
            }
            if (w.Places.Count < PlacesMost)
            {
                w.Places.Add(new ReviewPlace { Place = place ?? string.Empty, Before = Math.Max(0, before) });
            }
        }

        /// <summary>The mean rating of the week's businesses when their first answer went up, thousandths; 0 when none had one.</summary>
        public static int Before(ReviewWeek w)
        {
            long sum = 0;
            int n = 0;
            for (int i = 0; w != null && i < w.Places.Count; i++)
            {
                if (w.Places[i].Before > 0)
                {
                    sum += w.Places[i].Before;
                    n++;
                }
            }
            return n == 0 ? 0 : (int)(sum / n);
        }

        /// <summary>The week is over: nothing is kept of it.</summary>
        public static void Clear(ReviewWeek w)
        {
            w.From = w.Thanks = w.Sorry = w.Mixed = 0;
            w.Places.Clear();
        }
    }
}
