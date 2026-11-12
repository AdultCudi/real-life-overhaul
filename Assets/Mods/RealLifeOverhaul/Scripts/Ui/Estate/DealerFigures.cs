using System;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What the lots add up to on the Dealerships page: the cars, the day's sales and takings, the offers waiting and the free places.</summary>
    public struct LotTotals
    {
        public int Cars, Sold, Revenue, Deals, Bays, Free;
    }

    /// <summary>The offers page's head: the offers waiting, the best of them, and what has been sold today with its margin.</summary>
    public struct OfferTotals
    {
        public int Waiting, Best, Sold, Margin;
    }

    /// <summary>The finance page's head: what is owed, what comes in a week, the plans behind, the warranties paid out and the buyers by rating.</summary>
    public struct FinanceTotals
    {
        public int Owed, Weekly, Behind, PaidOut, Plans, Covers;
        public int[] Grades;
    }

    /// <summary>The growth page's head: the events on, the missions done of all, and the cars sold today.</summary>
    public struct GrowthTotals
    {
        public int EventsOn, MissionsDone, MissionsAll, SoldToday;
    }

    /// <summary>
    /// The totals the Car Showroom's pages put in their heads, worked out from the showroom's state in one place. The live pages and the samples both read them from here,
    /// so a head can only differ from the rows beneath it if the rows are drawn from something else. Game-free. COST: a pass over the lots, the plans and the sales, when a page is written.
    /// </summary>
    public static class DealerFigures
    {
        /// <summary>The cars on the lots, today's sales and their prices (what the Revenue report plots), the offers waiting and the bays.</summary>
        public static LotTotals Lots(DealerState s)
        {
            var t = new LotTotals();
            for (int y = 0; y < s.Yards.Count; y++)
            {
                DealerYard yard = s.Yards[y];
                t.Cars += yard.Cars.Count;
                t.Sold += yard.Today.Sold;
                t.Revenue += yard.Today.Revenue;   // the cars' prices, as the report's chart plots them: the head and the chart end on one figure
                t.Deals += yard.Deals.Count;
                t.Bays += yard.Spots.Count;
                t.Free += s.Room(yard);
            }
            return t;
        }

        /// <summary>The offers waiting (only those for a car that is still on the lot), the best of them and today's sales.</summary>
        public static OfferTotals Offers(DealerState s)
        {
            var t = new OfferTotals();
            for (int y = 0; y < s.Yards.Count; y++)
            {
                DealerYard yard = s.Yards[y];
                t.Sold += yard.Today.Sold;
                t.Margin += yard.Today.Margin;
                for (int d = 0; d < yard.Deals.Count; d++)
                {
                    StockCar car = yard.Car(yard.Deals[d].Car);
                    if (car == null)
                    {
                        continue;
                    }
                    t.Waiting++;
                    t.Best = Math.Max(t.Best, DealerRules.Price(yard, yard.Deals[d], car));
                }
            }
            return t;
        }

        /// <summary>The plans' and the warranties' sums, and the plans by the grade <paramref name="gradeOf"/> gives their buyers (0 to 4).</summary>
        public static FinanceTotals Finance(DealerState s, Func<int, int> gradeOf)
        {
            var t = new FinanceTotals { Grades = new int[5], Plans = s.Plans.Count, Covers = s.Covers.Count };
            for (int i = 0; i < s.Plans.Count; i++)
            {
                DealerPlan p = s.Plans[i];
                t.Owed += p.Owed;
                t.Weekly += p.Weekly;
                t.Behind += p.Missed > 0 ? 1 : 0;
                t.Grades[Math.Max(0, Math.Min(4, gradeOf(p.Score)))]++;
            }
            for (int i = 0; i < s.Covers.Count; i++)
            {
                t.PaidOut += s.Covers[i].Paid;
            }
            return t;
        }

        /// <summary>The events running at the day, the missions done of all the showrooms' missions, and the cars sold today.</summary>
        public static GrowthTotals Growth(DealerState s, int day)
        {
            var t = new GrowthTotals();
            for (int k = 0; k < DealerCatalogue.EventKinds; k++)
            {
                for (int y = 0; y < s.Yards.Count; y++)
                {
                    if (s.Yards[y].Event == k && s.Yards[y].EventOn(day))
                    {
                        t.EventsOn++;
                        break;
                    }
                }
            }
            for (int y = 0; y < s.Yards.Count; y++)
            {
                t.SoldToday += s.Yards[y].Today.Sold;
                for (int m = 0; m < DealerCatalogue.Missions; m++)
                {
                    t.MissionsAll++;
                    t.MissionsDone += DealerRules.MissionDone(s.Yards[y], m) ? 1 : 0;
                }
            }
            return t;
        }

        /// <summary>What every car on the lots is worth together, by the worth given for a car, and the cars counted.</summary>
        public static int StockWorth(DealerState s, Func<StockCar, int> worth, out int cars)
        {
            long sum = 0;
            cars = 0;
            for (int y = 0; y < s.Yards.Count; y++)
            {
                for (int c = 0; c < s.Yards[y].Cars.Count; c++)
                {
                    sum += worth(s.Yards[y].Cars[c]);
                    cars++;
                }
            }
            return (int)Math.Min(int.MaxValue / 2, sum);
        }

        /// <summary>The market's listings by kind, the ones a table shows: a wholesale car is new, and a lot already knocked down still shows as a lot.</summary>
        public static void Market(DealerState s, out int news, out int used, out int lots)
        {
            news = used = lots = 0;
            for (int i = 0; i < s.Market.Count; i++)
            {
                DealerListing l = s.Market[i];
                if (l.Gone && l.Kind != ListingKind.Auction)
                {
                    continue;
                }
                news += l.Kind == ListingKind.Wholesale ? 1 : 0;
                used += l.Kind == ListingKind.Used ? 1 : 0;
                lots += l.Kind == ListingKind.Auction ? 1 : 0;
            }
        }
    }
}
