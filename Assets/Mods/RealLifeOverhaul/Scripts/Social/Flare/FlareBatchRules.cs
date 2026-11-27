using System.Collections.Generic;

namespace RealLifeOverhaul.Flare
{
    /// <summary>How a bulk answer treats one review. Never saved: what the answer was is its quality, as for any answer.</summary>
    public enum FlareBatchWay
    {
        Left = 0,       // a poor review out of line with the business's rating: a judgement call, so it waits for the player
        Thanks = 1,     // four or five stars
        Sorry = 2,      // two stars or fewer, and fair
        Mixed = 3       // three stars: thanked for the honesty (an answer that fits no better and costs nothing)
    }

    /// <summary>What answering a business's waiting reviews in bulk would do, worked out on a copy of its stars: each review's way, the counts and the rating before and after.</summary>
    public sealed class FlareBatchPlan
    {
        public readonly List<FlareBatchWay> Ways = new List<FlareBatchWay>(32);
        public int Thanks, Sorry, Mixed, Left;
        public int Beyond;            // waiting past the most one press answers
        public int Before, After;     // the business's rating, thousandths, as it stands and as the answers would leave it

        public int Answers => Thanks + Sorry + Mixed;

        public void Clear()
        {
            Ways.Clear();
            Thanks = Sorry = Mixed = Left = Beyond = Before = After = 0;
        }
    }

    /// <summary>What a bulk answer came to over one business or many: the figures the preview and the notice say.</summary>
    public sealed class FlareBatchSummary
    {
        public int Thanks, Sorry, Mixed, Left, Beyond;
        public int Skipped;           // a manager's mistake: answers that were due and were not made
        public int Businesses;        // businesses that had reviews to answer
        public long BeforeSum, AfterSum;   // the sum of those businesses' ratings, thousandths
        public string Name;           // the business, when there is one

        public int Answers => Thanks + Sorry + Mixed;
        public int Before => Businesses > 0 ? (int)(BeforeSum / Businesses) : 0;
        public int After => Businesses > 0 ? (int)(AfterSum / Businesses) : 0;

        public void Clear()
        {
            Thanks = Sorry = Mixed = Left = Beyond = Skipped = Businesses = 0;
            BeforeSum = AfterSum = 0;
            Name = null;
        }

        public void Add(FlareBatchPlan p, string name)
        {
            if (p.Ways.Count == 0)
            {
                return;
            }
            Thanks += p.Thanks;
            Sorry += p.Sorry;
            Mixed += p.Mixed;
            Left += p.Left;
            Beyond += p.Beyond;
            Businesses++;
            BeforeSum += p.Before;
            AfterSum += p.After;
            Name = Businesses == 1 ? name : null;
        }
    }

    /// <summary>
    /// Answering reviews in bulk and by a manager, by the same rule as by hand (<see cref="FlareReplyRules"/>): thanks for four and five stars,
    /// sorry for two and one, a word of thanks for the honesty of a three (it stands, it never costs), and a poor review that is out of line with the
    /// business's rating left alone, because standing your ground is a judgement call. Each answer is worked out in order on a copy of the business's
    /// stars, so a rating that an answer lifts can make a later poor review out of line exactly as it would by hand, the preview says what the press will
    /// do, and the caps (<see cref="FlareReplyRules.MostLift"/>) hold however many reviews there are. Game-free; a press costs one walk over the waiting reviews.
    /// </summary>
    public static class FlareBatchRules
    {
        public const int Most = 60;        // answers one press, or one day's round at a business, makes at the most: the rest wait for the next
        public const int AutoDays = 10;    // a manager answers reviews up to this many days old: older ones are for "Answer all"

        /// <summary>The way a review is answered in bulk, by its stars and whether a poor one is out of line with the business's rating.</summary>
        public static FlareBatchWay Way(int stars, bool unfair)
        {
            stars = stars < 1 ? 1 : stars > 5 ? 5 : stars;
            if (stars >= 4)
            {
                return FlareBatchWay.Thanks;
            }
            if (stars == 3)
            {
                return FlareBatchWay.Mixed;
            }
            return unfair ? FlareBatchWay.Left : FlareBatchWay.Sorry;
        }

        /// <summary>The option of the reply sheet a way answers with; never <see cref="FlareReplyOption.Defend"/>.</summary>
        public static FlareReplyOption Option(FlareBatchWay way)
        {
            return way == FlareBatchWay.Sorry ? FlareReplyOption.Sorry : FlareReplyOption.Thanks;
        }

        /// <summary>
        /// Works out the answers to reviews with these stars, in this order, on a copy of <paramref name="row"/> (null for a business with no stars): at most
        /// <paramref name="most"/> are answered or left, the rest counted in <see cref="FlareBatchPlan.Beyond"/>.
        /// </summary>
        public static void Plan(FlareStarRow row, IList<int> stars, int most, FlareBatchPlan into)
        {
            into.Clear();
            FlareStarRow sim = CopyOf(row);
            into.Before = FlareReplyRules.Average(sim);
            for (int i = 0; stars != null && i < stars.Count; i++)
            {
                if (into.Ways.Count >= most)
                {
                    into.Beyond++;
                    continue;
                }
                int s = stars[i] < 1 ? 1 : stars[i] > 5 ? 5 : stars[i];
                FlareBatchWay way = Way(s, FlareReplyRules.Unfair(s, sim));
                into.Ways.Add(way);
                switch (way)
                {
                    case FlareBatchWay.Thanks: into.Thanks++; break;
                    case FlareBatchWay.Sorry: into.Sorry++; break;
                    case FlareBatchWay.Mixed: into.Mixed++; break;
                    default: into.Left++; continue;
                }
                FlareReplyOption option = Option(way);
                FlareReplyRules.Answered(sim, s, FlareReplyRules.Quality(option, s, false));
            }
            into.After = FlareReplyRules.Average(sim);
        }

        /// <summary>A business's stars copied, so an answer worked out is never counted into the real row.</summary>
        public static FlareStarRow CopyOf(FlareStarRow from)
        {
            return from == null ? null : new FlareStarRow
            {
                Key = from.Key, One = from.One, Two = from.Two, Three = from.Three, Four = from.Four, Five = from.Five, Recent = from.Recent, Earlier = from.Earlier,
                Eased = from.Eased, EasedStars = from.EasedStars, Raised = from.Raised, RaisedStars = from.RaisedStars
            };
        }

        /// <summary>The first word of a name, for a reply that says "Thank you Dana": a handle's "@" is dropped and "there" stands for no name.</summary>
        public static string FirstName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "there";
            }
            string trimmed = name.Trim().TrimStart('@');
            int space = trimmed.IndexOf(' ');
            string first = space > 0 ? trimmed.Substring(0, space) : trimmed;
            return first.Length == 0 || first == "?" ? "there" : first;
        }

        /// <summary>Whether a manager answers a review written on <paramref name="reviewDay"/>, today being <paramref name="today"/>: not one older than <see cref="AutoDays"/>.</summary>
        public static bool Fresh(int reviewDay, int today) => today - reviewDay <= AutoDays;
    }
}
