using System;
using System.Collections.Generic;

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
        public int RentScale;       // the options' rental income, thousandths of the market's rent: 1000 as it was (0 is read as 1000)

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
        private const int UnitSalt = 7919;   // a prime: each unit's draws are its own, the first unit's the building's

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
            r = r * RentShare(c.RentScale) / 1000;
            r /= 1000L * Math.Max(8, c.DaysPerYear);
            return (int)Math.Max(1, Math.Min(int.MaxValue / 4, r));
        }

        /// <summary>
        /// The options' rental income as a share of the market's rent, thousandths, within its bounds; 0 (a climate that
        /// never had it set) is the market's own. Everything a tenant is asked, offers, renews at or is measured against
        /// comes from <see cref="MarketRent(int, int, int, int, int, EstateClimate)"/>, so all of it moves together, and the
        /// chance that somebody applies, which reads only the asking rent's share of the market, does not change: a higher
        /// setting is a richer city of tenants, not emptier buildings. A lease keeps the rent it was agreed at.
        /// </summary>
        public static int RentShare(int scale)
        {
            return scale <= 0 ? 1000 : EstateCatalogue.Clamp(scale, EstateCatalogue.RentScaleLeast, EstateCatalogue.RentScaleMost);
        }

        public static int MarketRent(EstateHolding h, EstateClimate c)
        {
            return MarketRent(h.Base, h.Use, h.Income, h.Cond, h.Level, c);
        }

        // ---------------------------------------------------------------- units

        /// <summary>
        /// How many units the game counts in a building: its floor (Building.totalSqm) over the size of the unit the
        /// game lets on its own and lets the player take (its BuildingSizeData), one at least and no more than
        /// <see cref="EstateCatalogue.UnitsMost"/>. A building the game gives no unit size is one unit.
        /// </summary>
        public static int UnitCount(int totalSqm, int unitSqm)
        {
            return totalSqm <= 0 || unitSqm <= 0 ? 1 : EstateCatalogue.Clamp(totalSqm / unitSqm, 1, EstateCatalogue.UnitsMost);
        }

        /// <summary>
        /// A building's units made to the game's count: empty units are added, and units over it are taken off from
        /// the last while nobody is in them (a let unit stays until its lease ends). Returns how many it has now.
        /// </summary>
        public static int FitUnits(EstateHolding h, int count)
        {
            count = EstateCatalogue.Clamp(count, 1, EstateCatalogue.UnitsMost);
            while (h.Units.Count < count)
            {
                h.Units.Add(new EstateUnit());
            }
            while (h.Units.Count > count && !h.Units[h.Units.Count - 1].Let)
            {
                h.Units.RemoveAt(h.Units.Count - 1);
            }
            return h.Units.Count;
        }

        /// <summary>A number for a unit's draws: the building's own for the first unit, so a building of one unit draws as it did before units.</summary>
        public static int UnitKey(EstateHolding h, int unit)
        {
            return Hash(h.Place) + unit * UnitSalt;
        }

        /// <summary>The market's rent of one unit: the building's, shared over its units. Nothing for a building unfit to let.</summary>
        public static int UnitRent(EstateHolding h, EstateClimate c)
        {
            int market = MarketRent(h, c);
            return market <= 0 ? 0 : Math.Max(1, market / Math.Max(1, h.Units.Count));
        }

        /// <summary>The rent the game's nightly run is to pay for a building: every let unit whose tenant owes nothing.</summary>
        public static int PayingRent(EstateHolding h)
        {
            long rent = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                rent += h.Units[i].Paying ? h.Units[i].Rent : 0;
            }
            return (int)Math.Min(int.MaxValue / 4, rent);
        }

        /// <summary>How many applications may wait for a whole building: a unit's most, and one more for each unit after the first.</summary>
        public static int ApplicantCap(int units)
        {
            return Math.Min(EstateCatalogue.ApplicantsBuildingMost, EstateCatalogue.ApplicantsMost + Math.Max(0, units - 1));
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
        /// Somebody applies for a unit today, or nobody: null. An applicant with a poorer record offers more, against
        /// the unit's share of the market's rent. Nobody applies while a tenant is in the unit, works are under way,
        /// the unit is not to let, three applications wait for it or the building has as many as it takes
        /// (<see cref="ApplicantCap"/>).
        /// </summary>
        public static RentApplicant Apply(EstateHolding h, int unit, int seed, int day, EstateClimate c, bool whatever = false)
        {
            EstateUnit u = h.Unit(unit);
            int place = Hash(h.Place);
            int market = UnitRent(h, c);
            if (u == null || u.Let || h.Self || !h.Managed || market <= 0 || u.Applicants.Count >= EstateCatalogue.ApplicantsMost || h.ApplicantCount >= ApplicantCap(h.Units.Count)
                || (!whatever && (!u.ToLet || h.Busy || h.SaleDay > 0)))
            {
                return null;
            }
            if (!whatever && Roll(seed, day, ForApplicant, UnitKey(h, unit)) >= ApplicantChance(u.Ask, h.Cond, c.Cycle))
            {
                return null;
            }
            int n = h.NextApplicant;   // (numbered across the building, so every draw below is the applicant's own)
            int record = Between(Roll(seed, day, ForRecord, place + n), EstateCatalogue.RecordLeast, EstateCatalogue.RecordMost);
            int offer = u.Ask + Between(Roll(seed, day, ForOffer, place + n), -EstateCatalogue.OfferUnder, EstateCatalogue.OfferOver) + Math.Max(0, 60 - record) * EstateCatalogue.RecordPremium;
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
            u.Applicants.Add(a);
            return a;
        }

        /// <summary>Applications whose day has passed are dropped, at every unit. Returns how many.</summary>
        public static int Lapse(EstateHolding h, int day)
        {
            int gone = 0;
            for (int u = 0; u < h.Units.Count; u++)
            {
                List<RentApplicant> list = h.Units[u].Applicants;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Lapse < day)
                    {
                        list.RemoveAt(i);
                        gone++;
                    }
                }
            }
            return gone;
        }

        /// <summary>
        /// An applicant becomes the tenant of the unit applied for: the rent offered, a deposit held, a lease to its
        /// last day. The unit's other applicants are turned away; the other units' wait on.
        /// </summary>
        public static bool Accept(EstateHolding h, int applicantId, int day)
        {
            int unit;
            RentApplicant a = h.ApplicantOf(applicantId, out unit);
            EstateUnit u = h.Unit(unit);
            if (a == null || u == null || u.Let || h.Self || !h.Managed || h.Busy || h.Cond < EstateCatalogue.ConditionUnfit)
            {
                return false;
            }
            u.Tenant = a.Kind;
            u.Record = a.Record;
            u.Rent = a.Offer;
            u.Deposit = a.Offer * EstateCatalogue.DepositDays;
            u.Until = day + a.Term;
            u.Arrears = u.ArrearsDays = u.NoticeEnd = u.CourtDay = u.VoidDays = 0;
            u.Applicants.Clear();
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

        /// <summary>Notice may be served on a unit's tenant when the rent is days behind or the lease has run out, and none is running.</summary>
        public static bool MayServeNotice(EstateHolding h, int unit, int day)
        {
            EstateUnit u = h.Unit(unit);
            return u != null && u.Let && u.NoticeEnd == 0 && u.CourtDay == 0 && (u.ArrearsDays >= EstateCatalogue.NoticeArrearsDays || day > u.Until);
        }

        /// <summary>How many of a building's units wait for the landlord: notice may be served, or the court asked.</summary>
        public static int UnitsToAnswer(EstateHolding h, int day)
        {
            int count = 0;
            for (int u = 0; u < h.Units.Count; u++)
            {
                count += MayServeNotice(h, u, day) || MayGoToCourt(h, u, day) ? 1 : 0;
            }
            return count;
        }

        public static bool ServeNotice(EstateHolding h, int unit, int day, int rentLever)
        {
            if (!MayServeNotice(h, unit, day))
            {
                return false;
            }
            h.Units[unit].NoticeEnd = day + NoticeLength(rentLever);
            return true;
        }

        /// <summary>The court may be asked for a unit once notice has run out and the tenant is still in.</summary>
        public static bool MayGoToCourt(EstateHolding h, int unit, int day)
        {
            EstateUnit u = h.Unit(unit);
            return u != null && u.Let && u.NoticeEnd > 0 && day > u.NoticeEnd && u.CourtDay == 0;
        }

        public static bool GoToCourt(EstateHolding h, int unit, int day)
        {
            if (!MayGoToCourt(h, unit, day))
            {
                return false;
            }
            h.Units[unit].CourtDay = day + EstateCatalogue.CourtDays;
            return true;
        }

        public static int CourtFee(int index)
        {
            return EstateCatalogue.Share(EstateCatalogue.CourtFee, EstateCatalogue.Clamp(index, 500, 4000));
        }

        // The tenant goes: the deposit meets what it can of the arrears, the rest is written off, and the unit is empty.
        // Damage left behind is the building's.
        private static void Vacate(EstateHolding h, int unit, ref TenancyDay d, bool evicted, int seed, int day)
        {
            EstateUnit u = h.Units[unit];
            int met = Math.Min(u.Deposit, u.Arrears);
            d.Lump += met;
            d.WrittenOff += u.Arrears - met;
            d.Left = !evicted;
            d.Evicted = evicted;
            if (evicted && u.Record < 60)
            {
                d.Damage = Between(Roll(seed, day, ForDamage, UnitKey(h, unit)), 0, EstateCatalogue.EvictionDamageMost);
                h.Cond = Math.Max(0, h.Cond - d.Damage);
            }
            u.Vacant();
            d.Pays = false;
        }

        /// <summary>
        /// A morning's wear of a building the player lets himself: each unit wears it as a building of its own would
        /// (a let unit by its tenant's record, an empty one a little), and the building loses the average of its
        /// units, rounded, so that a building of many units wears as fast as one of one. Before <see cref="Day"/>.
        /// Returns the condition lost.
        /// </summary>
        public static int Wear(EstateHolding h)
        {
            int count = Math.Max(1, h.Units.Count), sum = 0;
            for (int i = 0; i < h.Units.Count; i++)
            {
                EstateUnit u = h.Units[i];
                sum += u.Let ? EstateCatalogue.WearLet + Math.Max(0, 100 - u.Record) / EstateCatalogue.WearPerRecord : EstateCatalogue.WearVoid;
            }
            int before = h.Cond;
            h.Cond = Math.Max(0, h.Cond - (sum + count / 2) / count);
            return before - h.Cond;
        }

        /// <summary>
        /// One morning of a unit: the court's order, notice, the rent, the end of a lease. Whether the rent of the
        /// day that begins will be paid is the answer the game is then told (<see cref="Encode"/>, with every unit's
        /// in <see cref="PayingRent"/>). An empty unit only counts its day. The building's wear is <see cref="Wear"/>.
        /// </summary>
        public static TenancyDay Day(EstateHolding h, int unit, int seed, int day, EstateClimate c)
        {
            var d = new TenancyDay();
            EstateUnit u = h.Unit(unit);
            if (u == null)
            {
                return d;
            }
            int key = UnitKey(h, unit);
            if (!u.Let)
            {
                u.VoidDays++;
                return d;
            }
            if (u.CourtDay > 0 && day >= u.CourtDay)
            {
                Vacate(h, unit, ref d, true, seed, day);
                return d;
            }
            if (u.Arrears > 0)
            {
                bool paysUp = Roll(seed, day, ForCatchUp, key) < (u.NoticeEnd > 0 ? Math.Max(EstateCatalogue.NoticePaysUp, CatchUpChance(u.Record)) : CatchUpChance(u.Record));
                if (paysUp)
                {
                    d.Lump = u.Arrears;
                    u.Arrears = u.ArrearsDays = 0;
                    if (day <= u.Until)
                    {
                        u.NoticeEnd = u.CourtDay = 0;   // (notice for arrears falls away once they are paid)
                    }
                }
                else
                {
                    u.Arrears += u.Rent;
                    u.ArrearsDays++;
                    d.Missed = true;
                    // Under notice that has run out, somebody who owes may simply go, leaving the deposit behind.
                    if (u.NoticeEnd > 0 && day > u.NoticeEnd && u.CourtDay == 0 && Roll(seed, day, ForLeave, key) < 250)
                    {
                        Vacate(h, unit, ref d, false, seed, day);
                    }
                    return d;
                }
            }
            else if (Roll(seed, day, ForMiss, key) < MissChance(u.Record, c.Cycle))
            {
                u.Arrears = u.Rent;
                u.ArrearsDays = 1;
                d.Missed = true;
                return d;
            }
            if (u.NoticeEnd > 0 && day > u.NoticeEnd && day > u.Until)
            {
                Vacate(h, unit, ref d, false, seed, day);   // notice at a lease's end, with nothing owed: they go
                return d;
            }
            if (day > u.Until && u.NoticeEnd == 0)
            {
                int market = UnitRent(h, c);
                bool content = h.Cond >= EstateCatalogue.ConditionPoor && (market <= 0 || (long)u.Rent * 1000 <= (long)market * EstateCatalogue.RenewRentMost);
                if (Roll(seed, day, ForRenew, key) < (content ? EstateCatalogue.RenewChance : EstateCatalogue.RenewChancePoor))
                {
                    u.Until = day + EstateCatalogue.TermLeast;
                    d.Renewed = true;
                }
                else
                {
                    Vacate(h, unit, ref d, false, seed, day);
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
        /// building the player lets himself, with nobody in any of its units. A change of use needs the council's permission
        /// first (Planning), then the building (Convert).
        /// </summary>
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
            h.ClearApplicants();
            return true;
        }

        public static int AgentFee(int price, int index)
        {
            int least = EstateCatalogue.Share(EstateCatalogue.AgentFeeLeast, EstateCatalogue.Clamp(index, 500, 4000));
            return price <= 0 ? 0 : Math.Min(price, Math.Max(least, EstateCatalogue.Share(price, EstateCatalogue.AgentFee)));
        }

        /// <summary>The last day of the game's tax year that a day is in: the game's year turns at the end of each day that is a multiple of its length.</summary>
        public static int TaxYearLast(int day, int daysPerYear)
        {
            int year = Math.Max(1, daysPerYear);
            return (Math.Max(1, day) + year - 1) / year * year;
        }

        /// <summary>The first day of the game's tax year that a day is in.</summary>
        public static int TaxYearFirst(int day, int daysPerYear)
        {
            return TaxYearLast(day, daysPerYear) - Math.Max(1, daysPerYear) + 1;
        }

        /// <summary>
        /// Tax on a year of the property business's trading (its hotels and car hire), which is in no business's daily
        /// statement, as the game taxes a business's: its own rate on the takings less the running costs, nothing on a
        /// loss, and nothing while the year's business income in all (the statements' sales and this trading) is under
        /// the game's threshold.
        /// </summary>
        public static int TradingTax(int takings, int costs, long yearSales, int taxPercent)
        {
            long profit = (long)takings - costs;
            if (profit <= 0 || yearSales + Math.Max(0, takings) < EstateCatalogue.TaxThreshold)
            {
                return 0;
            }
            return (int)Math.Min(int.MaxValue / 4, profit * EstateCatalogue.Clamp(taxPercent, 0, 100) / 100);
        }

        /// <summary>Tax on a gain: half the game's own rate on what the sale brought over the price paid, the works and the fee. Nothing on a loss.</summary>
        public static int GainsTax(int price, int fee, int paid, int spent, int taxPercent)
        {
            long gain = (long)price - fee - paid - spent;
            return gain <= 0 ? 0 : (int)Math.Min(int.MaxValue / 4, gain * EstateCatalogue.Clamp(taxPercent, 0, 100) * EstateCatalogue.GainsShare / 100000);
        }
    }
}
