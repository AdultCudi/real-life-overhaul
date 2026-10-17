using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>How a hire ended, worked out from its own seed when the car is due back.</summary>
    public struct HireReturn
    {
        public bool Stolen;         // it does not come back
        public bool Late;           // a day late, charged at HireLateRate
        public int Days;            // days it was out, the late one included
        public int Rent;            // what the customer paid for the hire in all
        public int Km;              // driven
        public int Fuel;            // thousandths of a tank left
        public int Damage;          // thousandths of the car damaged; 0 for none
        public int Repair;          // what putting that right is reckoned at
        public int Kept;            // of the deposit, against the repair
        public int Payout;          // fleet cover: for a repair over the deposit, or for a car that is gone
        public int Cleaning;
    }

    /// <summary>Car hire by rule: a car's rate, whether it is hired today, and how it comes back. Game-free.</summary>
    public static class HireRules
    {
        private const int ForHire = 71, ForDays = 72, ForKm = 73, ForFuel = 74, ForDamage = 75, ForDamageSize = 76, ForLate = 77, ForStolen = 78;

        /// <summary>A car's class by its list price: 0 economy, 1 standard, 2 premium, 3 luxury.</summary>
        public static int Class(int price)
        {
            int[] under = EstateCatalogue.ClassUnder;
            for (int k = 0; k < under.Length; k++)
            {
                if (price < under[k])
                {
                    return k;
                }
            }
            return under.Length;
        }

        /// <summary>The going rate for a day, at today's prices.</summary>
        public static int Going(int price, int index)
        {
            long rate = (long)Math.Max(0, price) * EstateCatalogue.HireRate[Class(price)] / 10000 * EstateCatalogue.Clamp(index, 500, 4000) / 1000;
            return (int)Math.Max(EstateCatalogue.HireRateLeast, Math.Min(int.MaxValue / 4, rate));
        }

        public static int Rate(int price, int rate, int index)
        {
            return Math.Max(1, EstateCatalogue.Share(Going(price, index), EstateCatalogue.Clamp(rate, EstateCatalogue.RateLeast, EstateCatalogue.RateMost)));
        }

        /// <summary>The chance, in thousandths, that an idle car is hired on a day. Each input moves it one way only.</summary>
        public static int Demand(int carClass, int season, int evt, int cycle, int rate, int rating)
        {
            int k = EstateCatalogue.Clamp(carClass, 0, EstateCatalogue.HireClasses - 1);
            long d = EstateCatalogue.HireDemand[k];
            d = d * (k == EstateCatalogue.HireClasses - 1 && season == 1 ? EstateCatalogue.HireLuxurySummer : EstateCatalogue.HireSeason[EstateCatalogue.Clamp(season, 0, 3)]) / 1000;
            d = d * EstateCatalogue.HireEvent[EstateCatalogue.Clamp(evt, 0, EstateCatalogue.HireEvent.Length - 1)] / 1000;
            d = d * EstateCatalogue.HireCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000;
            d = d * EstateCatalogue.Clamp(1000 - (rate - 1000) * EstateCatalogue.HireSlope / 10, EstateCatalogue.RateDemandLeast, EstateCatalogue.RateDemandMost) / 1000;
            d = d * (EstateCatalogue.HireRatingLeast + EstateCatalogue.Clamp(rating, 0, 1000) * EstateCatalogue.HireRatingSpan / 1000) / 1000;
            return (int)Math.Min(EstateCatalogue.HireDemandMost, Math.Max(0, d));
        }

        /// <summary>A day's premium for one car on fleet cover.</summary>
        public static int Premium(int price, EstateClimate c)
        {
            long year = (long)Math.Max(0, price) * EstateCatalogue.HireCoverShare / 1000 * EstateCatalogue.Clamp(c.Index, 500, 4000) / 1000;
            return (int)Math.Max(1, year / Math.Max(8, c.DaysPerYear));
        }

        public static int Cleaning(int index)
        {
            return EstateCatalogue.Share(EstateCatalogue.HireCleaning, EstateCatalogue.Clamp(index, 500, 4000));
        }

        /// <summary>Whether somebody hires an idle car today; if so it is out until its day, with its rate, deposit and seed fixed.</summary>
        public static bool TryHire(HireCar car, int seed, int day, EstateClimate c, int rating, bool whatever = false)
        {
            if (car.State != (int)HireState.Idle || car.Price <= 0)
            {
                return false;
            }
            int key = EstateRules.Hash(car.Id);
            if (!whatever && EstateRules.Roll(seed, day, ForHire, key) >= Demand(Class(car.Price), c.Season, c.Event, c.Cycle, car.Rate, rating))
            {
                return false;
            }
            car.HireDays = EstateRules.Between(EstateRules.Roll(seed, day, ForDays, key), 1, EstateCatalogue.HireDaysMost);
            car.HireRate = Rate(car.Price, car.Rate, c.Index);
            car.Deposit = car.HireRate * EstateCatalogue.HireDepositDays;
            car.HireSeed = EstateRules.Roll(seed, day, ForHire, key + 1) * 1000 + EstateRules.Roll(seed, day, ForHire, key + 2);
            car.Day = day + car.HireDays;
            car.State = (int)HireState.Out;
            car.HireRating = EstateCatalogue.Clamp(rating, 0, 1000);
            return true;
        }

        /// <summary>True for a car not on the lot: with a customer, or back and waiting for its space.</summary>
        public static bool Away(int state) => state == (int)HireState.Out || state == (int)HireState.Waiting;

        /// <summary>The day a car that is out comes back: its day, or the day after when the hire runs a day late.</summary>
        public static int Back(HireCar car, int rating)
        {
            return car.Day + (Late(car, rating) ? 1 : 0);
        }

        /// <summary>The customer's deposit still held on a car: while it is out or waits for its space, until the hire is settled.</summary>
        public static int DepositHeld(HireCar car) => Away(car.State) ? Math.Max(0, car.Deposit) : 0;

        /// <summary>How the hire that is ending went: the same answer however often it is asked. Changes nothing; <see cref="Settle"/> follows.</summary>
        public static HireReturn End(HireCar car, int rating, bool cover, EstateClimate c, bool damageNow = false)
        {
            var r = new HireReturn();
            int s = car.HireSeed;
            r.Stolen = !damageNow && Stolen(car, rating);
            r.Late = !r.Stolen && EstateRules.Roll(s, 0, ForLate, 0) < EstateCatalogue.HireLateChance;
            r.Days = car.HireDays + (r.Late ? 1 : 0);
            r.Rent = car.HireRate * car.HireDays + (r.Late ? EstateCatalogue.Share(car.HireRate, EstateCatalogue.HireLateRate) : 0);
            if (r.Stolen)
            {
                int worth = EstateCatalogue.Share(car.Price, EstateCatalogue.HireResidual);
                r.Kept = car.Deposit;
                r.Payout = cover ? Math.Max(0, EstateCatalogue.Share(worth, EstateCatalogue.HireCoverPays) - EstateCatalogue.HireCoverExcess) : 0;
                return r;
            }
            r.Km = r.Days * EstateRules.Between(EstateRules.Roll(s, 0, ForKm, 0), EstateCatalogue.HireKmLeast, EstateCatalogue.HireKmMost);
            r.Fuel = EstateRules.Between(EstateRules.Roll(s, 0, ForFuel, 0), EstateCatalogue.HireFuelLeast, EstateCatalogue.HireFuelMost);
            if (damageNow || EstateRules.Roll(s, 0, ForDamage, 0) < EstateCatalogue.HireDamageChance)
            {
                r.Damage = EstateRules.Between(EstateRules.Roll(s, 0, ForDamageSize, 0), EstateCatalogue.HireDamageLeast, EstateCatalogue.HireDamageMost);
                r.Repair = EstateCatalogue.Share((long)EstateCatalogue.Share(car.Price, EstateCatalogue.HireRepairShare), r.Damage);
                r.Kept = Math.Min(car.Deposit, r.Repair);
                int over = r.Repair - r.Kept;
                r.Payout = cover && over > EstateCatalogue.HireCoverExcess ? EstateCatalogue.Share(over - EstateCatalogue.HireCoverExcess, EstateCatalogue.HireCoverPays) : 0;
            }
            r.Cleaning = Cleaning(c.Index);
            return r;
        }

        // Whether the car is not brought back: judged by the rating the hire went out under (an older save's by the one given).
        private static bool Stolen(HireCar car, int rating)
        {
            int judged = car.HireRating >= 0 ? car.HireRating : rating;
            return EstateRules.Roll(car.HireSeed, 0, ForStolen, 0) < (judged >= EstateCatalogue.HireGoodRating ? EstateCatalogue.HireStolenChanceGood : EstateCatalogue.HireStolenChance);
        }

        private static bool Late(HireCar car, int rating)
        {
            return !Stolen(car, rating) && EstateRules.Roll(car.HireSeed, 0, ForLate, 0) < EstateCatalogue.HireLateChance;
        }

        /// <summary>The hire is over and paid for: the car is cleaned today and can be hired from tomorrow.</summary>
        public static void Settle(HireCar car, HireReturn r)
        {
            car.Takings += r.Rent;
            car.Hires++;
            car.HireDays = car.HireRate = car.Deposit = car.HireSeed = car.Day = 0;
            car.HireRating = -1;
            car.State = (int)HireState.Valet;
        }

        /// <summary>What a hire does to the rating: a car handed over in good order adds a little, one that was not takes more away.</summary>
        public static int Rated(int rating, bool goodOrder)
        {
            return EstateCatalogue.Clamp(rating + (goodOrder ? EstateCatalogue.HireRatingGood : EstateCatalogue.HireRatingPoor), 0, 1000);
        }
    }
}
