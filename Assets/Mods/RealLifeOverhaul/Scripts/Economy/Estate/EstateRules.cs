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
        public int Event;           // what is on today, the event calendar's own number
        public int Season;          // 0 spring to 3 winter
        public int Weekday;         // 0 Monday to 6 Sunday

        public static EstateClimate Steady => new EstateClimate { Cycle = 1, Index = EstateCatalogue.IndexStart, DaysPerYear = EstateCatalogue.DaysPerYearDefault, TaxPercent = 20 };
    }

    /// <summary>What a day did to a tenancy. The landlord's money moves only through <see cref="Lump"/>: the day's rent is paid by the game's own nightly run.</summary>
    public struct TenancyDay
    {
        public bool Pays;           // the rent of the day that begins will be paid
        public bool Missed;         // a day's rent was not paid and was added to the arrears
        public int Lump;            // arrears paid in full today, or recovered from the deposit
        public int WrittenOff;      // arrears that are gone for good
        public bool Left;           // the tenant has gone of their own accord
        public bool Evicted;        // ...or by the court's order
        public bool Renewed;
        public int Damage;          // condition lost when they went
    }

    /// <summary>What may go wrong at a building on a day.</summary>
    public struct EstateHazard
    {
        public bool Breakdown, Fire;
        public int Loss;            // condition lost
        public int Bill;            // what putting the fire's damage right costs
        public int Payout;          // what landlord's cover pays of it
    }

    /// <summary>
    /// The property business by rule: what a building is worth and lets for, who applies, what a tenancy does each
    /// day, wear and repairs, works, and a sale. Game-free and without a random number of its own: every draw is
    /// <see cref="Roll"/> of the save's seed, the day and what it is for, so the same save gives the same history
    /// whenever and however often it is asked. Nothing here keeps anything: a rule reads the state it is given and
    /// writes only the holding it is given.
    /// </summary>
    public static class EstateRules
    {
        // What a draw is for (the third number of a roll).
        private const int ForCondition = 1, ForMonth = 2, ForApplicant = 3, ForOffer = 4, ForRecord = 5, ForTerm = 6, ForKind = 7, ForMiss = 8, ForCatchUp = 9, ForNotice = 10,
            ForRenew = 11, ForDamage = 12, ForBreakdown = 13, ForFire = 14, ForPlanning = 15, ForSale = 16, ForSaleOffer = 17, ForFall = 18, ForLeave = 19;

        // ---------------------------------------------------------------- draws

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

        /// <summary>A number for a text that is always the same for it (an address, a vehicle's id).</summary>
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

        // ---------------------------------------------------------------- the market

        /// <summary>The condition of a building nobody of the mod's has looked at yet: a quarter of the city is run down.</summary>
        public static int StreetCondition(int seed, int place)
        {
            bool runDown = Roll(seed, place, ForCondition, 0) < EstateCatalogue.RunDownShare;
            int roll = Roll(seed, place, ForCondition, 1);
            return runDown ? Between(roll, EstateCatalogue.RunDownLeast, EstateCatalogue.RunDownMost) : Between(roll, EstateCatalogue.KeptLeast, EstateCatalogue.KeptMost);
        }

        /// <summary>Where the market heads: the cycle's level, less what the central bank's rate takes off (or a cut puts on).</summary>
        public static int RealTarget(int cycle, int quarters)
        {
            return EstateCatalogue.Clamp(EstateCatalogue.RealTarget[EstateCatalogue.Clamp(cycle, 0, 3)] - quarters * EstateCatalogue.RealPerQuarterPoint, EstateCatalogue.RealLeast, EstateCatalogue.RealMost);
        }

        /// <summary>A month of the market: part of the way to its target and a small move of its own, never more than the month's most.</summary>
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

        /// <summary>
        /// What a building is worth: the game's own value of it, by the market, prices, the district as it stands
        /// against the middle, its condition and what has been done to it.
        /// </summary>
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

        // ---------------------------------------------------------------- rent

        /// <summary>The gross yield a year of a use in a district, thousandths of the value: dear districts yield less.</summary>
        public static int Yield(int use, int income)
        {
            int middle = EstateCatalogue.YieldMiddle[EstateCatalogue.Clamp(use, 0, EstateCatalogue.Uses - 1)];
            int off = (EstateCatalogue.Clamp(income, EstateCatalogue.IncomeLeast, EstateCatalogue.IncomeMost) - EstateCatalogue.IncomeMiddle) * EstateCatalogue.YieldPerIncomePoint / 100;
            return EstateCatalogue.Clamp(middle - off, EstateCatalogue.YieldLeast, EstateCatalogue.YieldMost);
        }

        /// <summary>
        /// The market's rent for a day: the yield on the game's own value, spread over the game's year, by prices,
        /// the building's condition and level, and for homes the council's rent policy. Nothing for a building that
        /// is unfit to let. Rents follow prices, not the property market: a boom in values is a fall in yields.
        /// </summary>
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
            r /= 1000L * Math.Max(8, c.DaysPerYear);
            return (int)Math.Max(1, Math.Min(int.MaxValue / 4, r));
        }

        public static int MarketRent(EstateHolding h, EstateClimate c)
        {
            return MarketRent(h.Base, h.Use, h.Income, h.Cond, h.Level, c);
        }

        /// <summary>What the game is told so that its own nightly run pays exactly a day's rent: the share let, and a rent for each square metre.</summary>
        public static void Encode(int rent, bool pays, int totalSqm, int maxOccupancy, out float occupancy, out float pricePerSqm)
        {
            if (!pays || rent <= 0 || totalSqm <= 0 || maxOccupancy <= 0)
            {
                occupancy = 0f;
                pricePerSqm = 0f;
                return;
            }
            occupancy = maxOccupancy;
            pricePerSqm = rent / (totalSqm * (maxOccupancy / 100f));
        }

        // ---------------------------------------------------------------- applicants

        /// <summary>The chance, in thousandths, that somebody applies on a day.</summary>
        public static int ApplicantChance(int ask, int cond, int cycle)
        {
            if (cond < EstateCatalogue.ConditionUnfit)
            {
                return 0;
            }
            int asked = EstateCatalogue.Clamp(1000 - (ask - 1000) * EstateCatalogue.ApplicantAskSlope, EstateCatalogue.ApplicantChanceLeast, EstateCatalogue.ApplicantChanceMost);
            long chance = (long)EstateCatalogue.ApplicantChance * asked / 1000 * EstateCatalogue.ApplicantCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000;
            if (cond < EstateCatalogue.ConditionPoor)
            {
                chance = chance * EstateCatalogue.ApplicantPoorShare / 1000;
            }
            return (int)Math.Min(1000, chance);
        }

        /// <summary>
        /// Somebody applies today, or nobody: null. An applicant with a poorer record offers more. Nobody applies
        /// while a tenant is in, works are under way, the building is not to let or three applications wait.
        /// </summary>
        public static RentApplicant Apply(EstateHolding h, int seed, int day, EstateClimate c, bool whatever = false)
        {
            int place = Hash(h.Place);
            int market = MarketRent(h, c);
            if (h.Let || h.Self || !h.Managed || market <= 0 || h.Applicants.Count >= EstateCatalogue.ApplicantsMost || (!whatever && (!h.ToLet || h.Busy || h.SaleDay > 0)))
            {
                return null;
            }
            if (!whatever && Roll(seed, day, ForApplicant, place) >= ApplicantChance(h.Ask, h.Cond, c.Cycle))
            {
                return null;
            }
            int n = h.NextApplicant;
            int record = Between(Roll(seed, day, ForRecord, place + n), EstateCatalogue.RecordLeast, EstateCatalogue.RecordMost);
            int offer = h.Ask + Between(Roll(seed, day, ForOffer, place + n), -EstateCatalogue.OfferUnder, EstateCatalogue.OfferOver) + Math.Max(0, 60 - record) * EstateCatalogue.RecordPremium;
            offer = EstateCatalogue.Clamp(offer, EstateCatalogue.OfferLeast, EstateCatalogue.OfferMost);
            var a = new RentApplicant
            {
                Id = n,
                Kind = Between(Roll(seed, day, ForKind, place + n), 0, EstateCatalogue.TenantKinds - 1),
                Offer = Math.Max(1, EstateCatalogue.Share(market, offer)),
                Record = record,
                Term = EstateCatalogue.TermLeast + EstateCatalogue.TermStep * Between(Roll(seed, day, ForTerm, place + n), 0, EstateCatalogue.TermSteps - 1),
                Lapse = day + EstateCatalogue.ApplicantDays
            };
            h.NextApplicant = n + 1;
            h.Applicants.Add(a);
            return a;
        }

        /// <summary>Applications whose day has passed are dropped. Returns how many.</summary>
        public static int Lapse(EstateHolding h, int day)
        {
            int gone = 0;
            for (int i = h.Applicants.Count - 1; i >= 0; i--)
            {
                if (h.Applicants[i].Lapse < day)
                {
                    h.Applicants.RemoveAt(i);
                    gone++;
                }
            }
            return gone;
        }

        /// <summary>An applicant becomes the tenant: the rent offered, a deposit held, a lease to its last day. The others are turned away.</summary>
        public static bool Accept(EstateHolding h, int applicantId, int day)
        {
            RentApplicant a = h.ApplicantOf(applicantId);
            if (a == null || h.Let || h.Self || !h.Managed || h.Busy || h.Cond < EstateCatalogue.ConditionUnfit)
            {
                return false;
            }
            h.Tenant = a.Kind;
            h.Record = a.Record;
            h.Rent = a.Offer;
            h.Deposit = a.Offer * EstateCatalogue.DepositDays;
            h.Until = day + a.Term;
            h.Arrears = h.ArrearsDays = h.NoticeEnd = h.CourtDay = h.VoidDays = 0;
            h.Applicants.Clear();
            return true;
        }

        // ---------------------------------------------------------------- a tenancy's day

        /// <summary>The chance, in thousandths, that a tenant who is up to date misses a day's rent.</summary>
        public static int MissChance(int record, int cycle)
        {
            long chance = (long)Math.Max(0, 100 - record) * EstateCatalogue.MissPerPoint / 10;
            return (int)Math.Min(1000, chance * EstateCatalogue.MissCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000);
        }

        public static int CatchUpChance(int record)
        {
            return EstateCatalogue.Clamp(EstateCatalogue.CatchUpBase + record * EstateCatalogue.CatchUpPerPoint, 0, 1000);
        }

        public static int NoticeLength(int rentLever)
        {
            return EstateCatalogue.NoticeDays[EstateCatalogue.Lever(rentLever)];
        }

        /// <summary>Notice may be served when the rent is days behind or the lease has run out, and none is running.</summary>
        public static bool MayServeNotice(EstateHolding h, int day)
        {
            return h.Let && h.NoticeEnd == 0 && h.CourtDay == 0 && (h.ArrearsDays >= EstateCatalogue.NoticeArrearsDays || day > h.Until);
        }

        public static bool ServeNotice(EstateHolding h, int day, int rentLever)
        {
            if (!MayServeNotice(h, day))
            {
                return false;
            }
            h.NoticeEnd = day + NoticeLength(rentLever);
            return true;
        }

        /// <summary>The court may be asked once notice has run out and the tenant is still in.</summary>
        public static bool MayGoToCourt(EstateHolding h, int day)
        {
            return h.Let && h.NoticeEnd > 0 && day > h.NoticeEnd && h.CourtDay == 0;
        }

        public static bool GoToCourt(EstateHolding h, int day)
        {
            if (!MayGoToCourt(h, day))
            {
                return false;
            }
            h.CourtDay = day + EstateCatalogue.CourtDays;
            return true;
        }

        public static int CourtFee(int index)
        {
            return EstateCatalogue.Share(EstateCatalogue.CourtFee, EstateCatalogue.Clamp(index, 500, 4000));
        }

        // The tenant goes: the deposit meets what it can of the arrears, the rest is written off, and the building is empty.
        private static void Vacate(EstateHolding h, ref TenancyDay d, bool evicted, int seed, int day)
        {
            int met = Math.Min(h.Deposit, h.Arrears);
            d.Lump += met;
            d.WrittenOff += h.Arrears - met;
            d.Left = !evicted;
            d.Evicted = evicted;
            if (evicted && h.Record < 60)
            {
                d.Damage = Between(Roll(seed, day, ForDamage, Hash(h.Place)), 0, EstateCatalogue.EvictionDamageMost);
                h.Cond = Math.Max(0, h.Cond - d.Damage);
            }
            h.Tenant = -1;
            h.Record = h.Rent = h.Deposit = h.Until = h.Arrears = h.ArrearsDays = h.NoticeEnd = h.CourtDay = 0;
            d.Pays = false;
        }

        /// <summary>
        /// One morning of a building: the court's order, notice, the rent, the end of a lease, and wear. Whether the
        /// rent of the day that begins will be paid is the answer the game is then told (<see cref="Encode"/>). An
        /// empty building only counts its day and wears a little.
        /// </summary>
        public static TenancyDay Day(EstateHolding h, int seed, int day, EstateClimate c)
        {
            var d = new TenancyDay();
            int place = Hash(h.Place);
            if (!h.Let)
            {
                h.VoidDays++;
                h.Cond = Math.Max(0, h.Cond - EstateCatalogue.WearVoid);
                return d;
            }
            h.Cond = Math.Max(0, h.Cond - EstateCatalogue.WearLet - Math.Max(0, 100 - h.Record) / EstateCatalogue.WearPerRecord);
            if (h.CourtDay > 0 && day >= h.CourtDay)
            {
                Vacate(h, ref d, true, seed, day);
                return d;
            }
            if (h.Arrears > 0)
            {
                bool paysUp = Roll(seed, day, ForCatchUp, place) < (h.NoticeEnd > 0 ? Math.Max(EstateCatalogue.NoticePaysUp, CatchUpChance(h.Record)) : CatchUpChance(h.Record));
                if (paysUp)
                {
                    d.Lump = h.Arrears;
                    h.Arrears = h.ArrearsDays = 0;
                    if (day <= h.Until)
                    {
                        h.NoticeEnd = h.CourtDay = 0;   // (notice for arrears falls away once they are paid)
                    }
                }
                else
                {
                    h.Arrears += h.Rent;
                    h.ArrearsDays++;
                    d.Missed = true;
                    // Under notice that has run out, somebody who owes may simply go, leaving the deposit behind.
                    if (h.NoticeEnd > 0 && day > h.NoticeEnd && h.CourtDay == 0 && Roll(seed, day, ForLeave, place) < 250)
                    {
                        Vacate(h, ref d, false, seed, day);
                    }
                    return d;
                }
            }
            else if (Roll(seed, day, ForMiss, place) < MissChance(h.Record, c.Cycle))
            {
                h.Arrears = h.Rent;
                h.ArrearsDays = 1;
                d.Missed = true;
                return d;
            }
            if (h.NoticeEnd > 0 && day > h.NoticeEnd && day > h.Until)
            {
                Vacate(h, ref d, false, seed, day);   // notice at a lease's end, with nothing owed: they go
                return d;
            }
            if (day > h.Until && h.NoticeEnd == 0)
            {
                int market = MarketRent(h, c);
                bool content = h.Cond >= EstateCatalogue.ConditionPoor && (market <= 0 || (long)h.Rent * 1000 <= (long)market * EstateCatalogue.RenewRentMost);
                if (Roll(seed, day, ForRenew, place) < (content ? EstateCatalogue.RenewChance : EstateCatalogue.RenewChancePoor))
                {
                    h.Until = day + EstateCatalogue.TermLeast;
                    d.Renewed = true;
                }
                else
                {
                    Vacate(h, ref d, false, seed, day);
                    return d;
                }
            }
            d.Pays = true;
            return d;
        }

        // ---------------------------------------------------------------- wear, repairs, cover

        /// <summary>What it costs to bring a building back to new.</summary>
        public static int RepairCost(int gameValue, int cond, int index)
        {
            long cost = (long)Math.Max(0, gameValue) * EstateCatalogue.RepairShare / 1000 * (1000 - EstateCatalogue.Clamp(cond, 0, 1000)) / 1000;
            return (int)Math.Min(int.MaxValue / 4, cost * EstateCatalogue.Clamp(index, 500, 4000) / 1000);
        }

        /// <summary>Days a job takes at the council's site hours.</summary>
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

        /// <summary>
        /// What goes wrong today, if anything: something fails (the worse the condition, the likelier), or a fire or a
        /// flood, whose damage landlord's cover pays for less its excess. The condition is lowered here.
        /// </summary>
        public static EstateHazard Hazard(EstateHolding h, int seed, int day, EstateClimate c)
        {
            var z = new EstateHazard();
            int place = Hash(h.Place);
            if (h.Busy)
            {
                return z;   // (a site is the builder's risk)
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

        // ---------------------------------------------------------------- works

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

        /// <summary>
        /// What a job costs, to be paid before <see cref="Start"/>; 0 if it cannot start. One job at a time, and none
        /// once a sale is agreed. A repair can be done at any building; a refurbishment and a change of use need a
        /// building the player lets himself, with nobody in it. A change of use needs the council's permission
        /// first (Planning), then the building (Convert).
        /// </summary>
        public static int Quote(EstateHolding h, EstateWork work, int toUse, EstateClimate c)
        {
            if (h.Busy || h.SaleDay > 0 || h.Base <= 0 || (work != EstateWork.Repair && (h.Let || h.Self || !h.Managed)))
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
            h.Applicants.Clear();
            return true;
        }

        /// <summary>
        /// A job whose day has come is finished. Returns what it was (None while one is still under way or there is
        /// none); for the council's decision <paramref name="granted"/> says how it went.
        /// </summary>
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

        // ---------------------------------------------------------------- a sale

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

        /// <summary>
        /// A buyer's offer today, or none: 0. A buyer offers a little under to a little over the value, less in a
        /// slump, and never more than is asked. One offer at a time; it stands three days.
        /// </summary>
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
            int share = Between(Roll(seed, day, ForSaleOffer, place), EstateCatalogue.SaleOfferLeast, EstateCatalogue.SaleOfferMost) + EstateCatalogue.SaleOfferCycle[EstateCatalogue.Clamp(cycle, 0, 3)];
            int offer = Math.Min(h.List, EstateCatalogue.Share(value, share));
            if (offer <= 0)
            {
                return 0;
            }
            h.Offer = offer;
            h.OfferLapse = day + EstateCatalogue.SaleOfferDays;
            return offer;
        }

        /// <summary>The offer is taken: the sale completes in five days, unless the buyer's own sale fails (settled now, found out then).</summary>
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
            h.Applicants.Clear();
            return true;
        }

        public static int AgentFee(int price)
        {
            return price <= 0 ? 0 : Math.Min(price, Math.Max(EstateCatalogue.AgentFeeLeast, EstateCatalogue.Share(price, EstateCatalogue.AgentFee)));
        }

        /// <summary>Tax on a gain: half the game's own rate on what the sale brought over the price paid, the works and the fee. Nothing on a loss.</summary>
        public static int GainsTax(int price, int fee, int paid, int spent, int taxPercent)
        {
            long gain = (long)price - fee - paid - spent;
            return gain <= 0 ? 0 : (int)Math.Min(int.MaxValue / 4, gain * EstateCatalogue.Clamp(taxPercent, 0, 100) * EstateCatalogue.GainsShare / 100000);
        }
    }
}
