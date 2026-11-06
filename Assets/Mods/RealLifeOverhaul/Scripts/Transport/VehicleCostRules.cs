using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The guards every money figure of a car passes before it is charged or shown: a service, tyres, a premium, a repair, the
    /// damages for a crash, the kilometres a tick adds. A float that is NaN or infinite, a speed from broken physics, a tick of
    /// no time or of a long sleep, a record from an old save: none may turn into a charge past what the car itself is worth.
    /// Game-free, so the checkers compile it. COST: a few comparisons when a charge is worked out, or a car drives a tick.
    /// </summary>
    public static class VehicleCostRules
    {
        public const float PriceMost = 100000000f;      // money: no car is listed above this; a list price past it is read as this
        public const float SpeedMost = 120f;            // metres a second (430 km/h): a speed past it is the physics breaking, not a car
        public const float TickMost = 5f;               // seconds a drive tick may count: a long hitch or a sleep is not driving
        public const float KmMost = 1000000f;           // kilometres a car record may hold: past it an old save's number is read as this
        public const int MissedDaysMost = 60;           // days of a policy or registration that a late look may count at once: one, in effect (see PoliciesDue)
        public const float DamageMost = 1f;             // the game's damage figure runs 0 to 1
        public const float RepairShare = 0.5f;          // of the list price: the game's own bill for all damage (VehicleInstance.CalculateRepairCost)
        public const float OneChargeFloor = 400f;       // money at the start's prices: the least bound on one charge for a car, however cheap the car

        /// <summary>A list price as the rules may use it: nothing for NaN or less than nothing, <see cref="PriceMost"/> for more.</summary>
        public static float Price(float price)
        {
            return !(price > 0f) ? 0f : price > PriceMost ? PriceMost : price;
        }

        /// <summary>True for a charge that is a real number, not below nothing and no more than <see cref="OneCharge"/> allows for a car of this list price.</summary>
        public static bool Within(float charge, float listPrice, int level)
        {
            if (float.IsNaN(charge) || float.IsInfinity(charge) || charge < 0f)
            {
                return false;
            }
            return charge <= Math.Max(PriceRules.At(OneChargeFloor, level), PriceRules.At(Price(listPrice), level));
        }

        /// <summary>A figure that must be a real number: the fallback for NaN and for both infinities.</summary>
        public static float Finite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        /// <summary>A number with NaN as nothing; infinity is left to the bounds the caller puts on it.</summary>
        private static float NoNaN(float value)
        {
            return float.IsNaN(value) ? 0f : value;
        }

        /// <summary>A bill from a share of the price, held between its least and its most; NaN is the least, infinity the most. (Mathf.Clamp lets NaN through.)</summary>
        public static float Bill(float raw, float least, float most)
        {
            if (float.IsNaN(raw))
            {
                return least;
            }
            return raw < least ? least : raw > most ? most : raw;
        }

        /// <summary>
        /// A single charge for one car, held to what the car is worth, or to <see cref="OneChargeFloor"/> at today's prices if
        /// the car is worth less: nothing a car costs in one go can be more than the car. Zero for a charge that is not a number.
        /// </summary>
        public static float OneCharge(float charge, float listPrice, int level)
        {
            if (float.IsNaN(charge) || charge <= 0f)
            {
                return 0f;
            }
            float most = Math.Max(PriceRules.At(OneChargeFloor, level), PriceRules.At(Price(listPrice), level));
            return charge > most ? most : charge;
        }

        /// <summary>The kilometres one drive tick adds: speed (metres a second) over the seconds it covers, each held to what a car can do. Zero for no time.</summary>
        public static float Km(float speed, float seconds)
        {
            float v = NoNaN(speed), t = NoNaN(seconds);
            v = v < 0f ? 0f : v > SpeedMost ? SpeedMost : v;
            t = t < 0f ? 0f : t > TickMost ? TickMost : t;
            return v * t / 1000f;
        }

        /// <summary>The game's bill for repairing a car's damage: damage (0 to 1) of half the list price; nothing for a damage that is not a number.</summary>
        public static float Repair(float damage, float listPrice)
        {
            float d = NoNaN(damage);
            d = d < 0f ? 0f : d > DamageMost ? DamageMost : d;
            return d * Price(listPrice) * RepairShare;
        }

        /// <summary>
        /// The damages for hitting another car at an impact speed: the speed squared and a rate, between the least and the most
        /// at the start's prices, then at today's. A speed that is NaN is the least; an infinite one is the greatest speed.
        /// </summary>
        public static float Liability(float speed, float rate, float least, float most, int level)
        {
            float v = NoNaN(speed);
            v = v < 0f ? 0f : v > SpeedMost ? SpeedMost : v;
            return PriceRules.At(Bill(v * v * rate, least, most), level);
        }

        /// <summary>
        /// Policies and registrations that a look after many missed days charges: one, however many days have gone by (a policy
        /// is renewed for its length from today, a registration renewed from today), and none when nothing has lapsed.
        /// </summary>
        public static int PoliciesDue(int daysSinceLapse)
        {
            return daysSinceLapse > 0 ? 1 : 0;
        }

        /// <summary>Days counted for a catch-up of any kind: no negative, and no more than <see cref="MissedDaysMost"/>.</summary>
        public static int CatchUpDays(int gone)
        {
            return gone < 0 ? 0 : gone > MissedDaysMost ? MissedDaysMost : gone;
        }

        /// <summary>A kilometre reading from a save: finite, never below nothing, never past <see cref="KmMost"/>.</summary>
        public static float MendKm(float km)
        {
            km = Finite(km, 0f);
            return km < 0f ? 0f : km > KmMost ? KmMost : km;
        }

        /// <summary>A fraction (tyres, brakes) from a save: finite, 0 to 1; a number that is none is a new part.</summary>
        public static float MendFraction(float value)
        {
            value = Finite(value, 1f);
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        /// <summary>
        /// Every guard of the car's costs and of the money of a life event run on its worst input: a huge distance, no time, NaN
        /// and infinity, many missed days, a save's wild numbers, a purse past an int. Returns how many probes failed, and names
        /// the first in <paramref name="first"/> (empty when none did). One rule for the Options test and for the checkers.
        /// </summary>
        public static int Probe(int level, out string first)
        {
            string said = string.Empty;
            int bad = 0;
            void Is(bool ok, string what)
            {
                if (!ok)
                {
                    bad++;
                    said = said.Length == 0 ? what : said;
                }
            }
            float least = PriceRules.At(150f, level), most = PriceRules.At(3000f, level);
            Is(Km(1e9f, 1e9f) <= SpeedMost * TickMost / 1000f + 1e-6f, "a huge speed over a huge time is held to what a car can do in a tick");
            Is(Km(30f, 0f) == 0f && Km(float.NaN, 1f) == 0f && Km(-5f, 1f) == 0f, "no time, a speed that is no number, or backwards, adds no distance");
            Is(Km(float.PositiveInfinity, 1f) <= SpeedMost / 1000f + 1e-6f, "an infinite speed is the greatest speed");
            Is(Bill(float.NaN, 40f, 2500f) == 40f && Bill(float.PositiveInfinity, 40f, 2500f) == 2500f && Bill(1e30f, 40f, 2500f) == 2500f,
                "a bill that is no number is the least, a huge one the most");
            Is(Repair(float.NaN, 50000f) == 0f && Repair(5f, 50000f) == 25000f && Repair(0.5f, float.NaN) == 0f, "a repair is damage 0 to 1 of half the price, and nothing from numbers that are none");
            Is(Liability(float.NaN, 2.5f, 150f, 3000f, level) == least && Liability(float.PositiveInfinity, 2.5f, 150f, 3000f, level) == most,
                "damages for a crash at a speed that is no number are the least, at an infinite one the most");
            Is(OneCharge(1e12f, 20000f, level) <= PriceRules.At(20000f, level) && OneCharge(float.NaN, 20000f, level) == 0f && OneCharge(-5f, 20000f, level) == 0f,
                "one charge is never more than the car is worth");
            Is(CatchUpDays(int.MaxValue) == MissedDaysMost && CatchUpDays(-9) == 0, "a catch-up counts no more than a bounded number of days, and none backwards");
            Is(PoliciesDue(100000) == 1 && PoliciesDue(0) == 0 && PoliciesDue(-3) == 0, "many missed days renew one policy, not one for every day");
            Is(MendKm(float.NaN) == 0f && MendKm(1e30f) == KmMost && MendKm(-4f) == 0f && MendFraction(float.NaN) == 1f && MendFraction(7f) == 1f && MendFraction(-1f) == 0f,
                "a car record from a save is put back within its bounds");
            Is(MendDay(-4, 100, 60) == 0 && MendDay(2000000000, 100, 60) == 0 && MendDay(90, 100, 60) == 90, "a day from a save that is not a day of the game is read as never");
            long bound = LifeMoneyRules.Bound(120, level);
            int rich = LifeMoneyRules.Settle(-60, 5000000000f, 120, level), wrapped = LifeMoneyRules.Settle(int.MinValue, 5000000000f, 120, level);
            Is(rich <= 0 && -(long)rich <= bound && -(long)wrapped <= bound && wrapped != int.MinValue, "a life event's loss from a purse past an int stays within what the event is written with");
            Is(LifeMoneyRules.Settle(-60, float.NaN, 120, level) == 0 && LifeMoneyRules.Settle(-60, 25f, 120, level) >= -25 && LifeMoneyRules.Settle(int.MaxValue, 100f, 120, level) <= bound,
                "no loss from a purse that is no number, none past the purse, no gain past the event's own");
            Is(WholeMoney.Floor(5000000000f) == int.MaxValue && WholeMoney.Floor(float.NaN) == 0 && WholeMoney.Floor(-3f) == 0 && WholeMoney.Round(-1e30) == int.MinValue + 1
               && WholeMoney.Add(int.MaxValue, 5L) == int.MaxValue, "whole money saturates at both ends of an int and reads NaN as nothing");
            first = said;
            return bad;
        }

        /// <summary>A day from a save that is not the future: 0 or less, or past <paramref name="today"/> plus a year, is read as 0 (never).</summary>
        public static int MendDay(int day, int today, int daysPerYear)
        {
            return day < 0 || (long)day > (long)today + Math.Max(1, daysPerYear) * 2L ? 0 : day;
        }
    }
}
