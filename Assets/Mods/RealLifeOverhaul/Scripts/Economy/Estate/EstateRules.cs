using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The city as the property business sees it on a day: read once each morning, handed to every rule.</summary>
    public struct EstateClimate
    {
        public int Cycle;           // 0 boom, 1 steady, 2 slowdown, 3 recession
        public int Index;           // prices, tenths of a point: 1000 at the start
        public int Quarters;        // the central bank's rate in quarter points on top of the banks' own
        public int RentLever;       // the council's rent policy: -1 freed, 0 as it was, +1 controlled
        public int PermitLever;     // its permits policy: -1 tightened, +1 eased
        public int DaysPerYear;
        public int TaxPercent;      // the game's own rate
        public int Event;           // the event calendar's number for today
        public int Season;          // 0 spring to 3 winter
        public int Weekday;         // 0 Monday to 6 Sunday
        public int RentScale;       // the option's rental income, thousandths of the market's rent; 0 is read as 1000

        public static EstateClimate Steady => new EstateClimate { Cycle = 1, Index = EstateCatalogue.IndexStart, DaysPerYear = EstateCatalogue.DaysPerYearDefault, TaxPercent = 20 };
    }

    /// <summary>What a day did to a tenancy. Only <see cref="Lump"/> moves money: the day's rent is paid by the game's nightly run.</summary>
    public struct TenancyDay
    {
        public bool Pays;           // the rent of the day that begins will be paid
        public bool Missed;         // a day's rent was not paid and was added to the arrears
        public int Lump;            // arrears paid in full today, or met from the deposit
        public int WrittenOff;      // arrears gone for good
        public bool Left;           // the tenant has gone of their own accord
        public bool Evicted;        // ...or by the court's order
        public bool Renewed;
        public int Damage;          // condition lost when they went
    }

    /// <summary>What went wrong at a building on a day.</summary>
    public struct EstateHazard
    {
        public bool Breakdown, Fire;
        public int Loss;            // condition lost
        public int Bill;            // what putting the fire's damage right costs
        public int Payout;          // what landlord's cover pays of it
    }

    /// <summary>
    /// The property business by rule: the market, value, rent, wear, works, a sale and tax. Game-free; every draw is
    /// <see cref="Roll"/> of the seed, the day and what it is for, so a save always gives the same history.
    /// </summary>
    public static partial class EstateRules
    {
        // What a draw is for (the third number of a roll).
        private const int ForCondition = 1, ForMonth = 2, ForApplicant = 3, ForOffer = 4, ForRecord = 5, ForTerm = 6, ForKind = 7, ForMiss = 8, ForCatchUp = 9,
            ForRenew = 11, ForDamage = 12, ForBreakdown = 13, ForFire = 14, ForPlanning = 15, ForSale = 16, ForSaleOffer = 17, ForFall = 18, ForLeave = 19;
        /// <summary>A number from 0 to 999 that is always the same for the same four numbers.</summary>
        public static int Roll(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 73856093u ^ (uint)b * 19349663u ^ (uint)c * 83492791u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h % 1000u);
            }
        }

        /// <summary>A whole number from least to most, both included, by a roll.</summary>
        public static int Between(int roll, int least, int most)
        {
            return most <= least ? least : least + (int)((long)roll * (most - least + 1) / 1000);
        }

        /// <summary>A number that is always the same for a text (an address, a vehicle's id).</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)(h & 0x3fffffff);
            }
        }

        /// <summary>The condition of a building nobody of the mod's has looked at yet: a quarter of the city is run down.</summary>
        public static int StreetCondition(int seed, int place)
        {
            bool runDown = Roll(seed, place, ForCondition, 0) < EstateCatalogue.RunDownShare;
            int roll = Roll(seed, place, ForCondition, 1);
            return runDown ? Between(roll, EstateCatalogue.RunDownLeast, EstateCatalogue.RunDownMost) : Between(roll, EstateCatalogue.KeptLeast, EstateCatalogue.KeptMost);
        }

        /// <summary>Where the market heads: the cycle's level, less what the central bank's rate takes off.</summary>
        public static int RealTarget(int cycle, int quarters)
        {
            return EstateCatalogue.Clamp(EstateCatalogue.RealTarget[EstateCatalogue.Clamp(cycle, 0, 3)] - quarters * EstateCatalogue.RealPerQuarterPoint,
                EstateCatalogue.RealLeast, EstateCatalogue.RealMost);
        }

        /// <summary>A month of the market: part of the way to its target and a small move of its own, within the month's most.</summary>
        public static int Month(int real, int seed, int month, int cycle, int quarters)
        {
            int gap = RealTarget(cycle, quarters) - real;
            int step = EstateCatalogue.Share(gap, EstateCatalogue.RealPull) + Between(Roll(seed, month, ForMonth, 0), -EstateCatalogue.RealNoise, EstateCatalogue.RealNoise);
            step = EstateCatalogue.Clamp(step, -EstateCatalogue.RealStepMost, EstateCatalogue.RealStepMost);
            return EstateCatalogue.Clamp(real + step, EstateCatalogue.RealLeast, EstateCatalogue.RealMost);
        }

        public static int ConditionValue(int cond)
        {
            return EstateCatalogue.ValueAtWorst + EstateCatalogue.Clamp(cond, 0, 1000) * (1000 - EstateCatalogue.ValueAtWorst) / 1000;
        }

        public static int ConditionRent(int cond)
        {
            return EstateCatalogue.RentAtWorst + EstateCatalogue.Clamp(cond, 0, 1000) * (1000 - EstateCatalogue.RentAtWorst) / 1000;
        }

        /// <summary>What a district's income does to a value, thousandths: 840 in the poorest, 1160 in the richest.</summary>
        public static int DistrictValue(int income)
        {
            return 1000 + (EstateCatalogue.Clamp(income, EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost) - EstateCatalogue.IncomeMiddle) * EstateCatalogue.DistrictValuePerPoint / 10;
        }

        /// <summary>What a building is worth: the game's own value by the market, prices, the district, its condition and level.</summary>
        public static int Value(int gameValue, int real, int index, int income, int cond, int level)
        {
            long v = Math.Max(0, gameValue);
            v = v * EstateCatalogue.Clamp(real, EstateCatalogue.RealLeast, EstateCatalogue.RealMost) / 1000;
            v = v * EstateCatalogue.Clamp(index, 500, 4000) / 1000;
            v = v * DistrictValue(income) / 1000;
            v = v * ConditionValue(cond) / 1000;
            v = v * (1000 + EstateCatalogue.LevelValue[EstateCatalogue.Clamp(level, 0, EstateCatalogue.LevelMost)]) / 1000;
            return (int)Math.Min(int.MaxValue / 4, v);
        }

        public static int Value(EstateHolding h, int real, int index)
        {
            return Value(h.Base, real, index, h.Income, h.Cond, h.Level);
        }

        /// <summary>The gross yield a year of a use in a district, thousandths of the value: dear districts yield less.</summary>
        public static int Yield(int use, int income)
        {
            int middle = EstateCatalogue.YieldMiddle[EstateCatalogue.Clamp(use, 0, EstateCatalogue.Uses - 1)];
            int off = (EstateCatalogue.Clamp(income, EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost) - EstateCatalogue.IncomeMiddle) * EstateCatalogue.YieldPerIncomePoint / 100;
            return EstateCatalogue.Clamp(middle - off, EstateCatalogue.YieldLeast, EstateCatalogue.YieldMost);
        }

        /// <summary>The market's rent for a day; 0 for a building unfit to let. It follows prices, not the property market.</summary>
        public static int MarketRent(int gameValue, int use, int income, int cond, int level, EstateClimate c)
        {
            if (cond < EstateCatalogue.ConditionUnfit || gameValue <= 0)
            {
                return 0;
            }
            long r = (long)gameValue * Yield(use, income);
            r = r * EstateCatalogue.Clamp(c.Index, 500, 4000) / 1000;
            r = r * ConditionRent(cond) / 1000;
            r = r * (1000 + EstateCatalogue.LevelRent[EstateCatalogue.Clamp(level, 0, EstateCatalogue.LevelMost)]) / 1000;
            if (use == (int)EstateUse.Homes)
            {
                r = r * (1000 - Math.Sign(c.RentLever) * EstateCatalogue.RentLeverShare) / 1000;
            }
            r = r * RentShare(c.RentScale) / 1000;
            r /= 1000L * Math.Max(8, c.DaysPerYear);
            return (int)Math.Max(1, Math.Min(int.MaxValue / 4, r));
        }

        /// <summary>The option's rental income, thousandths of the market's rent; a setting of 0 is the market's own.</summary>
        public static int RentShare(int scale)
        {
            return scale <= 0 ? 1000 : EstateCatalogue.Clamp(scale, EstateCatalogue.RentScaleLeast, EstateCatalogue.RentScaleMost);
        }

        /// <summary>What is added to (or, under 100%, taken from) a night's rent the game paid, so the night comes to that rent times the setting.</summary>
        public static int GameRentTopUp(int gameRent, int scale)
        {
            if (gameRent <= 0)
            {
                return 0;
            }
            long t = (long)gameRent * (RentShare(scale) - 1000) / 1000;
            return (int)Math.Max(-gameRent, Math.Min(int.MaxValue / 4, t));
        }

        /// <summary>A night's top-up for a building the game lets, once a night at most; none where the leases are at the setting already.</summary>
        public static int TopUp(EstateHolding h, int day, int gameRent, int scale)
        {
            if (h == null || h.Managed || day <= h.TopUpDay)
            {
                return 0;
            }
            h.TopUpDay = day;
            return GameRentTopUp(gameRent, scale);
        }

        public static int MarketRent(EstateHolding h, EstateClimate c)
        {
            return MarketRent(h.Base, h.Use, h.Income, h.Cond, h.Level, c);
        }

        public static int RepairCost(int gameValue, int cond, int index)
        {
            long cost = (long)Math.Max(0, gameValue) * EstateCatalogue.RepairShare / 1000 * (1000 - EstateCatalogue.Clamp(cond, 0, 1000)) / 1000;
            return (int)Math.Min(int.MaxValue / 4, cost * EstateCatalogue.Clamp(index, 500, 4000) / 1000);
        }

        /// <summary>Days a job takes at the speed the council's permits policy allows.</summary>
        public static int SiteDays(int days, int permitLever)
        {
            return Math.Max(1, (days * 1000 + EstateCatalogue.SiteSpeed[EstateCatalogue.Lever(permitLever)] - 1) / EstateCatalogue.SiteSpeed[EstateCatalogue.Lever(permitLever)]);
        }

        public static int RepairDays(int cond, int permitLever)
        {
            return SiteDays(EstateCatalogue.RepairDaysLeast + (1000 - EstateCatalogue.Clamp(cond, 0, 1000)) / EstateCatalogue.RepairPerDay, permitLever);
        }

        public static int BreakdownChance(int cond)
        {
            return EstateCatalogue.BreakdownBase + (1000 - EstateCatalogue.Clamp(cond, 0, 1000)) / EstateCatalogue.BreakdownPerPoint;
        }

        /// <summary>A day's premium for landlord's cover.</summary>
        public static int Premium(int gameValue, EstateClimate c)
        {
            long year = (long)Math.Max(0, gameValue) * EstateCatalogue.CoverShare / 1000 * EstateCatalogue.Clamp(c.Index, 500, 4000) / 1000;
            return (int)Math.Max(1, year / Math.Max(8, c.DaysPerYear));
        }

        /// <summary>What goes wrong today, if anything: a breakdown, likelier the worse the condition, or a fire. The condition is lowered here.</summary>
        public static EstateHazard Hazard(EstateHolding h, int seed, int day, EstateClimate c)
        {
            var z = new EstateHazard();
            int place = Hash(h.Place);
            if (h.Busy)
            {
                return z;   // a site is the builder's risk
            }
            if (Roll(seed, day, ForFire, place) < EstateCatalogue.FireChance)
            {
                int before = h.Cond;
                h.Cond = Math.Max(0, h.Cond - EstateCatalogue.FireLoss);
                z.Fire = true;
                z.Loss = before - h.Cond;
                z.Bill = RepairCost(h.Base, 1000 - z.Loss, c.Index);
                z.Payout = h.Cover ? z.Bill - EstateCatalogue.Share(z.Bill, EstateCatalogue.CoverExcess) : 0;
            }
            else if (Roll(seed, day, ForBreakdown, place) < BreakdownChance(h.Cond))
            {
                int before = h.Cond;
                h.Cond = Math.Max(0, h.Cond - EstateCatalogue.BreakdownLoss);
                z.Breakdown = true;
                z.Loss = before - h.Cond;
            }
            return z;
        }

        public static int LevelCost(int gameValue, int toLevel, int index)
        {
            long cost = (long)Math.Max(0, gameValue) * EstateCatalogue.LevelCost[EstateCatalogue.Clamp(toLevel, 0, EstateCatalogue.LevelMost)] / 1000;
            return (int)Math.Min(int.MaxValue / 4, cost * EstateCatalogue.Clamp(index, 500, 4000) / 1000);
        }

        /// <summary>What a step up adds to the value as the market stands: more than it costs in a boom, less in a slump.</summary>
        public static int LevelGain(EstateHolding h, int toLevel, int real, int index)
        {
            return Value(h.Base, real, index, h.Income, h.Cond, toLevel) - Value(h.Base, real, index, h.Income, h.Cond, toLevel - 1);
        }

        public static int PlanningFee(int gameValue, int index)
        {
            return Math.Max(1, EstateCatalogue.Share((long)EstateCatalogue.Share(gameValue, EstateCatalogue.PlanningFee), EstateCatalogue.Clamp(index, 500, 4000)));
        }

        public static int ConvertCost(int gameValue, int index)
        {
            return EstateCatalogue.Share((long)EstateCatalogue.Share(gameValue, EstateCatalogue.ConvertCost), EstateCatalogue.Clamp(index, 500, 4000));
        }

        public static bool PlanningGranted(int seed, string place, int day, int permitLever)
        {
            return Roll(seed, day, ForPlanning, Hash(place)) < EstateCatalogue.PlanningChance[EstateCatalogue.Lever(permitLever)];
        }

        /// <summary>What a job costs, paid before <see cref="Start"/>; 0 if it cannot start.</summary>
        public static int Quote(EstateHolding h, EstateWork work, int toUse, EstateClimate c)
        {
            if (h.Busy || h.SaleDay > 0 || h.Base <= 0 || (work != EstateWork.Repair && (h.AnyLet || h.Self || !h.Managed)))
            {
                return 0;
            }
            switch (work)
            {
                case EstateWork.Repair: return h.Cond < EstateCatalogue.ConditionNew ? Math.Max(1, RepairCost(h.Base, h.Cond, c.Index)) : 0;
                case EstateWork.Refurbish: return h.Level < EstateCatalogue.LevelMost ? Math.Max(1, LevelCost(h.Base, h.Level + 1, c.Index)) : 0;
                case EstateWork.Planning: return toUse >= 0 && toUse < EstateCatalogue.Uses && toUse != h.Use && h.PlanUse < 0 ? PlanningFee(h.Base, c.Index) : 0;
                case EstateWork.Convert: return h.PlanUse >= 0 && h.PlanUse != h.Use ? Math.Max(1, ConvertCost(h.Base, c.Index)) : 0;
                default: return 0;
            }
        }

        public static bool Start(EstateHolding h, EstateWork work, int toUse, int day, EstateClimate c)
        {
            int cost = Quote(h, work, toUse, c);
            if (cost <= 0)
            {
                return false;
            }
            h.Work = (int)work;
            h.WorkCost = cost;
            switch (work)
            {
                case EstateWork.Repair: h.WorkEnd = day + RepairDays(h.Cond, c.PermitLever); h.WorkTo = EstateCatalogue.ConditionNew; break;
                case EstateWork.Refurbish: h.WorkEnd = day + SiteDays(EstateCatalogue.LevelDays[h.Level + 1], c.PermitLever); h.WorkTo = h.Level + 1; break;
                case EstateWork.Planning: h.WorkEnd = day + EstateCatalogue.PlanningDays; h.WorkTo = toUse; break;
                default: h.WorkEnd = day + SiteDays(EstateCatalogue.ConvertDays, c.PermitLever); h.WorkTo = h.PlanUse; break;
            }
            h.ClearApplicants();
            return true;
        }

        /// <summary>A job whose day has come is finished: what it was, or None; <paramref name="granted"/> is the council's answer.</summary>
        public static EstateWork Finish(EstateHolding h, int seed, int day, EstateClimate c, out bool granted)
        {
            granted = true;
            if (!h.Busy || day < h.WorkEnd)
            {
                return EstateWork.None;
            }
            var work = (EstateWork)h.Work;
            switch (work)
            {
                case EstateWork.Repair: h.Cond = EstateCatalogue.ConditionNew; break;
                case EstateWork.Refurbish:
                    h.Level = EstateCatalogue.Clamp(h.WorkTo, 0, EstateCatalogue.LevelMost);
                    h.Spent += h.WorkCost;
                    break;
                case EstateWork.Planning:
                    granted = PlanningGranted(seed, h.Place, h.WorkEnd, c.PermitLever);
                    h.PlanUse = granted ? h.WorkTo : -1;
                    break;
                case EstateWork.Convert:
                    h.Use = EstateCatalogue.Clamp(h.WorkTo, 0, EstateCatalogue.Uses - 1);
                    h.PlanUse = -1;
                    h.Spent += h.WorkCost;
                    break;
            }
            h.Work = (int)EstateWork.None;
            h.WorkEnd = h.WorkCost = h.WorkTo = 0;
            return work;
        }

        /// <summary>The chance, in thousandths, of an offer on a day at an asking price against the value.</summary>
        public static int SaleChance(int list, int value, int cycle)
        {
            if (list <= 0 || value <= 0)
            {
                return 0;
            }
            long ratio = (long)list * 1000 / value;
            int[] table = EstateCatalogue.SaleChance;
            long chance;
            if (ratio <= 900)
            {
                chance = table[0];
            }
            else if (ratio >= 900 + 100 * (table.Length - 1))
            {
                chance = table[table.Length - 1];
            }
            else
            {
                int at = (int)((ratio - 900) / 100);
                long part = (ratio - 900) % 100;
                chance = table[at] + (table[at + 1] - table[at]) * part / 100;
            }
            return (int)(chance * EstateCatalogue.SaleCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000);
        }

        /// <summary>A buyer's offer today, or 0: around the value, less in a slump, never more than is asked.</summary>
        public static int SaleOffer(EstateHolding h, int seed, int day, int value, int cycle, bool whatever = false)
        {
            if (h.List <= 0 || h.Offer > 0 || h.SaleDay > 0 || value <= 0)
            {
                return 0;
            }
            int place = Hash(h.Place);
            if (!whatever && Roll(seed, day, ForSale, place) >= SaleChance(h.List, value, cycle))
            {
                return 0;
            }
            int share = Between(Roll(seed, day, ForSaleOffer, place), EstateCatalogue.SaleOfferLeast, EstateCatalogue.SaleOfferMost)
                        + EstateCatalogue.SaleOfferCycle[EstateCatalogue.Clamp(cycle, 0, 3)];
            int offer = Math.Min(h.List, EstateCatalogue.Share(value, share));
            if (offer <= 0)
            {
                return 0;
            }
            h.Offer = offer;
            h.OfferLapse = day + EstateCatalogue.SaleOfferDays;
            return offer;
        }

        /// <summary>The offer is taken: the sale completes on its day unless the buyer's own sale fails, which is settled now.</summary>
        public static bool AcceptOffer(EstateHolding h, int seed, int day, int cycle)
        {
            if (h.Offer <= 0 || h.SaleDay > 0 || day > h.OfferLapse)
            {
                return false;
            }
            h.SalePrice = h.Offer;
            h.SaleDay = day + EstateCatalogue.CompletionDays;
            h.SaleFails = Roll(seed, day, ForFall, Hash(h.Place)) < EstateCatalogue.FallThrough[EstateCatalogue.Clamp(cycle, 0, 3)];
            h.Offer = h.OfferLapse = 0;
            h.ClearApplicants();
            return true;
        }

        public static int AgentFee(int price, int index)
        {
            int least = EstateCatalogue.Share(EstateCatalogue.AgentFeeLeast, EstateCatalogue.Clamp(index, 500, 4000));
            return price <= 0 ? 0 : Math.Min(price, Math.Max(least, EstateCatalogue.Share(price, EstateCatalogue.AgentFee)));
        }

        /// <summary>The last day of the game's tax year a day is in: the game taxes on each day that is a multiple of the year (TaxHelper).</summary>
        public static int TaxYearLast(int day, int daysPerYear)
        {
            int year = Math.Max(1, daysPerYear);
            return (Math.Max(1, day) + year - 1) / year * year;
        }

        public static int TaxYearFirst(int day, int daysPerYear)
        {
            return TaxYearLast(day, daysPerYear) - Math.Max(1, daysPerYear) + 1;
        }

        /// <summary>Tax on the hotels' and car hire's year as the game taxes a business: its rate on a profit, over the threshold.</summary>
        public static int TradingTax(int takings, int costs, long yearSales, int taxPercent)
        {
            long profit = (long)takings - costs;
            if (profit <= 0 || yearSales + Math.Max(0, takings) < EstateCatalogue.TaxThreshold)
            {
                return 0;
            }
            return (int)Math.Min(int.MaxValue / 4, profit * EstateCatalogue.Clamp(taxPercent, 0, 100) / 100);
        }

        /// <summary>Tax on a gain: half the game's rate on what the sale brought over the price paid, the works and the fee.</summary>
        public static int GainsTax(int price, int fee, int paid, int spent, int taxPercent)
        {
            long gain = (long)price - fee - paid - spent;
            return gain <= 0 ? 0 : (int)Math.Min(int.MaxValue / 4, gain * EstateCatalogue.Clamp(taxPercent, 0, 100) * EstateCatalogue.GainsShare / 100000);
        }
    }
}
