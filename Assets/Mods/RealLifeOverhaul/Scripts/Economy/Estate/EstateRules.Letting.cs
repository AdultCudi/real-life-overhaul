using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The rules' letting part: units, applicants and a tenancy's day.</summary>
    public static partial class EstateRules
    {
        private const int UnitSalt = 7919;   // a prime: each unit draws its own; the first unit draws the building's

        /// <summary>How many units the game counts in a building: its floor over the size of the unit the game lets on its own.</summary>
        public static int UnitCount(int totalSqm, int unitSqm)
        {
            return totalSqm <= 0 || unitSqm <= 0 ? 1 : EstateCatalogue.Clamp(totalSqm / unitSqm, 1, EstateCatalogue.UnitsMost);
        }

        /// <summary>A building's units made to the game's count, a let unit staying until its lease ends. Returns how many it has now.</summary>
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

        /// <summary>A number for a unit's draws: the building's own for the first unit, so a building of one unit draws as before units.</summary>
        public static int UnitKey(EstateHolding h, int unit)
        {
            return Hash(h.Place) + unit * UnitSalt;
        }

        /// <summary>The market's rent of one unit: the building's, shared over its units.</summary>
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

        /// <summary>What the game is told so that its nightly run pays exactly a day's rent (RealEstate.DailyIncome).</summary>
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

        /// <summary>Somebody applies for a unit today, or null; a test tool's <paramref name="whatever"/> skips chance and choice.</summary>
        public static RentApplicant Apply(EstateHolding h, int unit, int seed, int day, EstateClimate c, bool whatever = false, int lift = 1000)
        {
            EstateUnit u = h.Unit(unit);
            int place = Hash(h.Place);
            int market = UnitRent(h, c);
            if (u == null || u.Let || h.Self || !h.Managed || market <= 0 || u.Applicants.Count >= EstateCatalogue.ApplicantsMost || h.ApplicantCount >= ApplicantCap(h.Units.Count)
                || (!whatever && (!u.ToLet || h.Busy || h.SaleDay > 0)))
            {
                return null;
            }
            if (!whatever && Roll(seed, day, ForApplicant, UnitKey(h, unit)) >= (int)Math.Min(1000L, (long)ApplicantChance(u.Ask, h.Cond, c.Cycle) * lift / 1000))
            {
                return null;
            }
            int n = h.NextApplicant;   // numbered across the building, so every draw below is the applicant's own
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

        /// <summary>An applicant takes the unit applied for at the rent offered, with a deposit held. The unit's other applicants are turned away.</summary>
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
        public static bool MayServeNotice(EstateHolding h, int unit, int day)
        {
            EstateUnit u = h.Unit(unit);
            return u != null && u.Let && u.NoticeEnd == 0 && u.CourtDay == 0 && !u.OnPlan && (u.ArrearsDays >= EstateCatalogue.NoticeArrearsDays || day > u.Until);
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

        /// <summary>The court may be asked once notice has run out and the tenant is still in.</summary>
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

        // The tenant goes: the deposit meets what it can of the arrears, the rest is written off.
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

        /// <summary>A morning's wear before <see cref="Day"/>: the average of its units', so many units wear as fast as one. Returns the loss.</summary>
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

        /// <summary>One morning of a unit: the court's order, notice, the rent and the end of a lease.</summary>
        public static TenancyDay Day(EstateHolding h, int unit, int seed, int day, EstateClimate c)
        {
            return Day(h, unit, seed, day, c, false);
        }

        /// <summary>
        /// The same, with a landlord's duties (<paramref name="duties"/>: the option): a tenant who said they go goes, a
        /// payment plan's instalment, a reminder's pull, and satisfaction in missing and renewing. Without them, as before.
        /// </summary>
        public static TenancyDay Day(EstateHolding h, int unit, int seed, int day, EstateClimate c, bool duties)
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
            if (duties && u.QuitDay > 0 && day >= u.QuitDay)
            {
                Vacate(h, unit, ref d, false, seed, day);
                d.Quit = true;
                return d;
            }
            if (duties && u.OnPlan && u.Arrears > 0)
            {
                if (!PlanDay(h, unit, seed, day, c, ref d))
                {
                    return d;
                }
            }
            else if (u.Arrears > 0)
            {
                bool paysUp = Roll(seed, day, ForCatchUp, key) < PayUpChance(u, duties);
                if (paysUp)
                {
                    d.Lump = u.Arrears;
                    u.Cleared();
                    if (day <= u.Until)
                    {
                        u.NoticeEnd = u.CourtDay = 0;   // notice for arrears falls away once they are paid
                    }
                }
                else
                {
                    u.Arrears += u.Rent;
                    u.ArrearsDays++;
                    d.Missed = true;
                    // Once notice has run out, somebody who owes may simply go, leaving the deposit behind.
                    if (u.NoticeEnd > 0 && day > u.NoticeEnd && u.CourtDay == 0 && Roll(seed, day, ForLeave, key) < 250)
                    {
                        Vacate(h, unit, ref d, false, seed, day);
                    }
                    return d;
                }
            }
            else if (Roll(seed, day, ForMiss, key) < (duties ? UnhappyMiss(MissChance(u.Record, c.Cycle), u.Happy) : MissChance(u.Record, c.Cycle)))
            {
                u.Arrears = u.Rent;
                u.ArrearsDays = 1;
                d.Missed = true;
                return d;
            }
            if (u.NoticeEnd > 0 && day > u.NoticeEnd && day > u.Until)
            {
                Vacate(h, unit, ref d, false, seed, day);   // notice at a lease's end with nothing owed: they go
                return d;
            }
            if (day > u.Until && u.NoticeEnd == 0)
            {
                int market = UnitRent(h, c);
                bool content = h.Cond >= EstateCatalogue.ConditionPoor && (market <= 0 || (long)u.Rent * 1000 <= (long)market * EstateCatalogue.RenewRentMost);
                int renew = duties && u.Happy >= 0 ? RenewChance(u.Happy) : content ? EstateCatalogue.RenewChance : EstateCatalogue.RenewChancePoor;
                if (Roll(seed, day, ForRenew, key) < renew)
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
    }
}
