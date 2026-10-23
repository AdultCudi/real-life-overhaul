using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A contractor's quote for a refit: who, at what price and in how many working days.</summary>
    public struct RefitQuote
    {
        public int Rival;      // the city's contractor (BuildCatalogue.RivalKeys)
        public long Price;
        public int Days;
        public int Hours;
    }

    /// <summary>
    /// A refit of a business's interior: the working hours by floor area, the grade of the new fittings from the work's quality
    /// and the company's speciality, and a contractor's quote when the player's company does not do it: the city's eight
    /// builders bid on it as on a tender of the programme's refits, the best score wins, drawn from the save's seed and the day,
    /// so the same day gives the same quote. Game-free.
    /// </summary>
    public static class RefitRules
    {
        public const int Works = 2;   // the programme's refit works (Civic's SiteKind.Refit): their rate, loads, crews and plant

        /// <summary>The working hours a refit of so many square metres takes at the usual pace.</summary>
        public static int Hours(int sqm)
        {
            return BuildCatalogue.Clamp(BuildCatalogue.RefitHoursBase + Math.Max(0, sqm) / BuildCatalogue.RefitSqmPerHour, BuildCatalogue.RefitHoursLeast, BuildCatalogue.RefitHoursMost);
        }

        /// <summary>The new fittings' grade (0 plain, 1 good, 2 fine) from the work's quality; a renovation company's a grade better.</summary>
        public static int Grade(int quality, bool special)
        {
            int grade = 0;
            for (int g = BuildCatalogue.RefitGradeFrom.Length - 1; g >= 0; g--)
            {
                if (quality >= BuildCatalogue.RefitGradeFrom[g])
                {
                    grade = g;
                    break;
                }
            }
            return BuildCatalogue.Clamp(grade + (special ? 1 : 0), 0, BuildCatalogue.RefitGradeFrom.Length - 1);
        }

        /// <summary>What the city pays for a refit's hours, and a contractor's works are valued at: the programme's refit rate.</summary>
        public static long Value(int hours, int prices)
        {
            return TenderRules.WorksValue(Works, hours, prices);
        }

        /// <summary>
        /// The contractor's quote: every one of the city's builders bids on the refit (their margin, speciality and luck), the
        /// best score wins. The same seed and day give the same quote.
        /// </summary>
        public static RefitQuote Quote(int seed, int day, int sqm, int prices)
        {
            int hours = Hours(sqm);
            long value = Value(hours, prices);
            int days = TenderRules.StandardDays(hours);
            var bids = new TenderBid[BuildCatalogue.RivalCount];
            for (int r = 0; r < bids.Length; r++)
            {
                bids[r] = TenderRules.RivalBid(seed, day * 31 + 7, r, value, days, Speciality.Renovation, 0);
            }
            TenderBid won = bids[Math.Max(0, TenderRules.Winner(bids, bids.Length))];
            return new RefitQuote { Rival = won.Bidder, Price = won.Price / 10L * 10L, Days = Math.Max(1, won.Days), Hours = hours };
        }
    }
}
