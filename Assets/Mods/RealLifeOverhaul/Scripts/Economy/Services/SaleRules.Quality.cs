using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a "what would raise it" line is about.</summary>
    public enum RaiseKind { Interior = 0, Rating = 1, Skill = 2, Books = 3 }

    /// <summary>One thing that would raise a business's price: where it stands, what it would take and about how much it adds.</summary>
    public struct SaleRaise
    {
        public RaiseKind Kind;
        public int Now;                  // a score, a share or days, as its kind counts it
        public int To;                   // what the line asks for
        public int Want;                 // what the district wants (the interior) or a full record (the books); 0 where nothing is wanted
        public bool Met;                 // it is where a buyer wants it already
        public int Gain;                 // about how much the value rises at To
    }

    /// <summary>
    /// A business's quality in its value, game-free: its interior against what its district's customers want, its customers'
    /// satisfaction and its staff's skill take the goodwill up or down within bounds. With nothing known it counts for nothing.
    /// </summary>
    public static partial class SaleRules
    {
        /// <summary>The goodwill's share for its quality, in per cent, and each part's points.</summary>
        public static int QualityPct(in SaleInputs i, out int interior, out int rating, out int skill)
        {
            interior = rating = skill = 0;
            if (!i.Quality)
            {
                return 100;
            }
            if (i.Interior >= 0)
            {
                int want = ServicesCatalogue.Clamp(i.InteriorWant, 0, 100), score = ServicesCatalogue.Clamp(i.Interior, 0, 100);
                interior = score < want ? -ServicesCatalogue.InteriorShortPct
                    : want >= 100 ? ServicesCatalogue.InteriorTopPct : (score - want) * ServicesCatalogue.InteriorTopPct / (100 - want);
            }
            if (i.Rating >= 0)
            {
                rating = (ServicesCatalogue.Clamp(i.Rating, 0, 100) - 50) * ServicesCatalogue.RatingSwingPct / 100;
            }
            if (i.Staff > 0)
            {
                skill = (ServicesCatalogue.Clamp(i.Skill, 0, 100) - 50) * ServicesCatalogue.SkillSwingPct / 100;
            }
            return ServicesCatalogue.Clamp(100 + interior + rating + skill, ServicesCatalogue.QualityLeastPct, ServicesCatalogue.QualityMostPct);
        }

        /// <summary>
        /// What would raise a business's price before it is sold, each with about how much it adds: the interior to what its
        /// district wants (or to a full score once it is there), customer satisfaction and the staff's skill a step higher, and a
        /// full week of books. A line that adds nothing and is not met is left out (a business with no profit gains nothing).
        /// </summary>
        public static void Raises(in SaleInputs i, List<SaleRaise> into)
        {
            into.Clear();
            int now = Value(i).Value;
            if (i.Quality && i.Interior >= 0)
            {
                int score = ServicesCatalogue.Clamp(i.Interior, 0, 100), want = ServicesCatalogue.Clamp(i.InteriorWant, 0, 100);
                var r = new SaleRaise { Kind = RaiseKind.Interior, Now = score, Want = want, Met = score >= want, To = score >= want ? 100 : want };
                SaleInputs at = i;
                at.Interior = r.To;
                r.Gain = r.To > score ? Math.Max(0, Value(at).Value - now) : 0;
                Keep(into, r);
            }
            if (i.Quality && i.Rating >= 0)
            {
                int rating = ServicesCatalogue.Clamp(i.Rating, 0, 100);
                var r = new SaleRaise { Kind = RaiseKind.Rating, Now = rating, Met = rating >= ServicesCatalogue.RatingGood, To = Math.Min(100, rating + ServicesCatalogue.RaiseStep) };
                SaleInputs at = i;
                at.Rating = r.To;
                r.Gain = Math.Max(0, Value(at).Value - now);
                Keep(into, r);
            }
            if (i.Quality && i.Staff > 0)
            {
                int skill = ServicesCatalogue.Clamp(i.Skill, 0, 100);
                var r = new SaleRaise { Kind = RaiseKind.Skill, Now = skill, Met = skill >= ServicesCatalogue.SkillGood, To = Math.Min(100, skill + ServicesCatalogue.RaiseStep) };
                SaleInputs at = i;
                at.Skill = r.To;
                r.Gain = Math.Max(0, Value(at).Value - now);
                Keep(into, r);
            }
            if (i.DaysOnRecord < ServicesCatalogue.HistoryDays)
            {
                var r = new SaleRaise { Kind = RaiseKind.Books, Now = Math.Max(0, i.DaysOnRecord), Want = ServicesCatalogue.HistoryDays, To = ServicesCatalogue.HistoryDays };
                SaleInputs at = i;
                at.DaysOnRecord = ServicesCatalogue.HistoryDays;
                r.Gain = Math.Max(0, Value(at).Value - now);
                Keep(into, r);
            }
        }

        private static void Keep(List<SaleRaise> into, SaleRaise r)
        {
            if (r.Met || r.Gain > 0)
            {
                into.Add(r);
            }
        }
    }
}
