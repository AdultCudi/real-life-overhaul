using System;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>Viewings of the cars on a lot: how many buyers looked at each car today and yesterday, and how often a buyer looked before an offer. Game-free.</summary>
    public static class ViewRules
    {
        public const int LooksMost = 99;

        /// <summary>A buyer looks at a car today. Returns how many looks the car has had today.</summary>
        public static int Look(StockCar car)
        {
            car.ViewsToday = Math.Min(LooksMost, car.ViewsToday + 1);
            return car.ViewsToday;
        }

        /// <summary>A showroom's day is over: today's looks are yesterday's, and every car starts the new day with none.</summary>
        public static void NewDay(DealerYard y)
        {
            for (int i = 0; i < y.Cars.Count; i++)
            {
                y.Cars[i].ViewsLast = y.Cars[i].ViewsToday;
                y.Cars[i].ViewsToday = 0;
            }
        }

        /// <summary>
        /// The words' number for a buyer's offer: how many times they looked before it. At least one; a buyer who came back to a car already
        /// looked at today looked again, so a car looked at often draws offers from people who looked more than once.
        /// </summary>
        public static int LooksBeforeOffer(StockCar car, int seed, int day, int customerKey)
        {
            int seen = Math.Max(1, car.ViewsToday);
            var dice = new DealerDice(seed, day, customerKey, 97);
            return Math.Min(seen, 1 + dice.Range(0, 2));
        }
    }
}
