using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>Why a claim paid what it did.</summary>
    public enum ClaimWhy
    {
        Paid = 0,
        NoCover,       // no policy in force
        Waiting,       // the policy is in its first days
        NotCovered,    // the tier does not cover this kind of loss, or the loss was there before the cover
        UnderExcess,   // the loss is no more than the holder's own part
        YearLimit      // the year's claims or the year's sum are used up
    }

    /// <summary>What became of a missed premium.</summary>
    public enum GraceStep
    {
        Began = 0,     // the first morning: the holder is told, the cover holds
        Holds,         // asked for again, the cover still holds
        Lapsed         // the days are over: the policy ends
    }

    /// <summary>
    /// The rules of insurance, game-free: what a car's tier and its driver's record do to the Garage's premium, a home's premium, what a claim pays (never more than the
    /// loss, never in a policy's first days, never for what was there before the cover, never over the year's limits), the no-claims discount, the days of grace and the
    /// rival tycoons' cover. Money is whole money; nothing here is drawn at random. COST: a few operations a call, nothing allocated.
    /// </summary>
    public static class InsuranceRules
    {
        /// <summary>An amount written at the start's prices, at today's.</summary>
        public static int At(int amount, int level) => PriceRules.At(amount, level);

        /// <summary>The year of the calendar a day is in.</summary>
        public static int YearOf(int day, int yearDays) => Math.Max(0, day) / Math.Max(1, yearDays);

        // ---- what a claim pays

        /// <summary>
        /// What a policy pays of a loss: its share of the loss over the excess, held to what is left of the year's cap, nothing once the year's claims are used up, never
        /// under nothing and never over the loss. <paramref name="borne"/> is the holder's own first part.
        /// </summary>
        public static long Pays(long loss, int excess, int share, long paidYear, long yearCap, int claimsYear, int claimsMost, out long borne, out ClaimWhy why)
        {
            borne = 0;
            if (loss <= 0 || share <= 0)
            {
                why = ClaimWhy.NotCovered;
                return 0;
            }
            if (claimsYear >= claimsMost || paidYear >= yearCap)
            {
                why = ClaimWhy.YearLimit;
                return 0;
            }
            borne = Math.Min(loss, Math.Max(0, excess));
            long pays = InsuranceCatalogue.Share(loss - borne, Math.Min(1000, share));
            pays = InsuranceCatalogue.Clamp(Math.Min(pays, yearCap - Math.Max(0L, paidYear)), 0L, loss);
            why = pays > 0 ? ClaimWhy.Paid : ClaimWhy.UnderExcess;
            return pays;
        }

        /// <summary>True while a policy that began on a day pays nothing yet. A policy from before the feature (0) never waits.</summary>
        public static bool Waiting(int since, int day, int waitDays) => since > 0 && day < since + waitDays;

        // ---- the car

        /// <summary>The no-claims discount for so many months renewed without a claim, thousandths off.</summary>
        public static int NoClaim(int months)
        {
            return InsuranceCatalogue.Clamp(months, 0, InsuranceCatalogue.NoClaimMost / InsuranceCatalogue.NoClaimStep) * InsuranceCatalogue.NoClaimStep;
        }

        /// <summary>The crashes that still count against the premium on a day.</summary>
        public static int Crashes(List<int> days, int today)
        {
            int n = 0;
            for (int i = 0; days != null && i < days.Count; i++)
            {
                n += today >= days[i] && today - days[i] < InsuranceCatalogue.CrashDays ? 1 : 0;
            }
            return Math.Min(n, InsuranceCatalogue.CrashesMost);
        }

        /// <summary>A crash with damages to another car is put on the driver's record; those too old to count go.</summary>
        public static void NoteCrash(List<int> days, int today)
        {
            for (int i = days.Count - 1; i >= 0; i--)
            {
                if (today - days[i] >= InsuranceCatalogue.CrashDays || days[i] > today)
                {
                    days.RemoveAt(i);
                }
            }
            while (days.Count >= InsuranceCatalogue.CrashesMost)
            {
                days.RemoveAt(0);
            }
            days.Add(today);
        }

        /// <summary>
        /// What a car's tier and its driver's record make of the Garage's premium, in thousandths: the tier, the credit grade (0 to 5), the points on the licence, the
        /// crashes that still count and the no-claims discount, multiplied together and held inside the catalogue's bounds. The standard tier with good credit, a clean
        /// licence, no crash and no discount is exactly 1000: the Garage's own premium.
        /// </summary>
        public static int CarFactor(int tier, int grade, int points, int crashes, int noClaimMonths)
        {
            long f = InsuranceCatalogue.CarPremiumShare[InsuranceCatalogue.Tier(tier)];
            f = f * InsuranceCatalogue.CreditShare[InsuranceCatalogue.Clamp(grade, 0, InsuranceCatalogue.CreditShare.Length - 1)] / 1000;
            f = f * (1000 + InsuranceCatalogue.PointShare * InsuranceCatalogue.Clamp(points, 0, InsuranceCatalogue.PointsMost)) / 1000;
            f = f * (1000 + InsuranceCatalogue.CrashShare * InsuranceCatalogue.Clamp(crashes, 0, InsuranceCatalogue.CrashesMost)) / 1000;
            f = f * (1000 - NoClaim(noClaimMonths)) / 1000;
            return InsuranceCatalogue.Clamp((int)f, InsuranceCatalogue.FactorLeast, InsuranceCatalogue.FactorMost);
        }

        /// <summary>The Garage's premium at a factor, to the whole as the Garage charges it; one at least, and a premium that is no number is left as it came.</summary>
        public static float CarPremium(float garage, int factor)
        {
            if (!(garage > 0f) || float.IsInfinity(garage))
            {
                return garage;
            }
            return (float)Math.Max(1.0, Math.Round((double)garage * InsuranceCatalogue.Clamp(factor, InsuranceCatalogue.FactorLeast, InsuranceCatalogue.FactorMost) / 1000.0));
        }

        /// <summary>Damage (0 to 1) as the thousandths the policy keeps; nothing for a figure that is no number.</summary>
        public static int Damage(float damage)
        {
            return !(damage > 0f) ? 0 : damage >= 1f ? 1000 : (int)Math.Round(damage * 1000.0);
        }

        /// <summary>
        /// The part of a repair that is the insurer's, in thousandths of damage: what was mended above the damage the car had when its cover began. A car bought damaged
        /// and insured afterwards is mended at its owner's cost down to that damage.
        /// </summary>
        public static int CoveredDrop(int before, int after, int pre)
        {
            return Math.Max(0, before - Math.Max(Math.Max(0, after), Math.Max(0, pre)));
        }

        /// <summary>The damage from before the cover that is left after a repair: what was mended of it is gone for good.</summary>
        public static int PreAfter(int pre, int after) => Math.Max(0, Math.Min(pre, after));

        /// <summary>The year turns for a car's policy: its sums start again. The year only moves forward.</summary>
        public static void TurnYear(CarPolicy p, int day, int yearDays)
        {
            int year = YearOf(day, yearDays);
            if (year > p.Year)
            {
                p.Year = year;
                p.PaidYear = 0;
                p.ClaimsYear = 0;
            }
        }

        public static void TurnYear(HomePolicy p, int day, int yearDays)
        {
            int year = YearOf(day, yearDays);
            if (year > p.Year)
            {
                p.Year = year;
                p.PaidYear = 0;
                p.ClaimsYear = 0;
            }
        }

        /// <summary>The tier's excess and share for a kind of loss at a car, at today's prices. A police report takes the excess off a repair, as the Garage has it.</summary>
        public static void CarTerms(int tier, ClaimKind kind, bool reported, int level, out int excess, out int share)
        {
            tier = InsuranceCatalogue.Tier(tier);
            switch (kind)
            {
                case ClaimKind.Repair:
                    share = InsuranceCatalogue.CarRepairShare[tier];
                    excess = reported ? 0 : At(InsuranceCatalogue.CarRepairExcess[tier], level);
                    break;
                case ClaimKind.Liability:
                    share = InsuranceCatalogue.CarLiabilityShare[tier];
                    excess = At(InsuranceCatalogue.CarLiabilityExcess[tier], level);
                    break;
                case ClaimKind.Tow:
                    share = InsuranceCatalogue.CarTowShare[tier];
                    excess = 0;
                    break;
                default:
                    share = 0;
                    excess = 0;
                    break;
            }
        }

        /// <summary>
        /// A claim on a car's policy: paid by its tier's terms where the car is insured today and past its first days, counted in the policy's year, and the no-claims
        /// discount lost. Nothing is booked here: the caller takes the payout off the bill, or pays it out.
        /// </summary>
        public static long CarClaim(CarPolicy p, ClaimKind kind, long loss, bool insured, bool reported, int day, int yearDays, int level, out long borne, out ClaimWhy why)
        {
            borne = 0;
            if (p == null || !insured)
            {
                why = ClaimWhy.NoCover;
                return 0;
            }
            if (Waiting(p.Since, day, InsuranceCatalogue.CarWaitDays))
            {
                why = ClaimWhy.Waiting;
                return 0;
            }
            TurnYear(p, day, yearDays);
            int tier = InsuranceCatalogue.Tier(p.Tier);
            CarTerms(tier, kind, reported, level, out int excess, out int share);
            long pays = Pays(loss, excess, share, p.PaidYear, At(InsuranceCatalogue.CarYearCap[tier], level), p.ClaimsYear, InsuranceCatalogue.CarYearClaims[tier], out borne, out why);
            if (pays > 0)
            {
                p.PaidYear += pays;
                p.ClaimsYear++;
                p.NoClaimMonths = 0;
            }
            return pays;
        }

        /// <summary>True for a car whose tier may be moved up: no damage to speak of and not broken down, so nothing that has happened already is bought cover for.</summary>
        public static bool Sound(int damage, bool broken) => !broken && damage < InsuranceCatalogue.SoundBelow;

        /// <summary>What moving a tier up costs now: the difference of the two premiums for the days left of the paid month. Moving down costs and returns nothing.</summary>
        public static int UpgradeCharge(int oldPremium, int newPremium, int daysLeft, int policyDays)
        {
            long more = Math.Max(0, newPremium - Math.Max(0, oldPremium));
            return (int)(more * InsuranceCatalogue.Clamp(daysLeft, 0, Math.Max(1, policyDays)) / Math.Max(1, policyDays));
        }

        // ---- a missed premium

        /// <summary>
        /// A premium could not be taken this morning: the first time the days of grace begin, on the mornings after the cover still holds, and once they are over the
        /// policy lapses. <paramref name="graceFrom"/> is the policy's own record of the first morning.
        /// </summary>
        public static GraceStep Missed(ref int graceFrom, int today)
        {
            if (graceFrom <= 0 || graceFrom > today)
            {
                graceFrom = today;
                return GraceStep.Began;
            }
            if (today - graceFrom < InsuranceCatalogue.GraceDays)
            {
                return GraceStep.Holds;
            }
            graceFrom = 0;
            return GraceStep.Lapsed;
        }

        /// <summary>The last morning a missed premium is asked for again.</summary>
        public static int GraceEnds(int graceFrom) => graceFrom + InsuranceCatalogue.GraceDays - 1;

        // ---- the home

        /// <summary>What the district does to a home's premium, thousandths: dearer where incomes are low.</summary>
        public static int HomeDistrict(int income)
        {
            return InsuranceCatalogue.Clamp(1000 + (income - InsuranceCatalogue.HomeIncomeMiddle) * InsuranceCatalogue.HomeIncomePerPoint,
                InsuranceCatalogue.HomeDistrictLeast, InsuranceCatalogue.HomeDistrictMost);
        }

        /// <summary>The claims at a home that still count against its premium on a day.</summary>
        public static int HomeClaims(List<int> days, int today)
        {
            int n = 0;
            for (int i = 0; days != null && i < days.Count; i++)
            {
                n += today >= days[i] && today - days[i] < InsuranceCatalogue.HomeClaimDays ? 1 : 0;
            }
            return n;
        }

        /// <summary>
        /// A month's premium of a home: the tier's base and its floor area, by the district, the claims that still count and the no-claims discount, for a month of so
        /// many days at today's prices. One at least.
        /// </summary>
        public static int HomePremium(int tier, int sqm, int income, int claims, int noClaimMonths, int level, int monthDays)
        {
            tier = InsuranceCatalogue.Tier(tier);
            // Thousandths of whole money, at the start's prices, for the catalogue's month.
            long list = (long)InsuranceCatalogue.HomeBase[tier] * 1000 + (long)InsuranceCatalogue.Clamp(sqm, 0, InsuranceCatalogue.HomeSqmMost) * InsuranceCatalogue.HomePerSqm[tier];
            list = list * HomeDistrict(income) / 1000;
            list = list * (1000 + Math.Min(InsuranceCatalogue.HomeLoadingMost, Math.Max(0, claims) * InsuranceCatalogue.HomeClaimLoading)) / 1000;
            list = list * (1000 - NoClaim(noClaimMonths)) / 1000;
            list = list * Math.Max(1, monthDays) / InsuranceCatalogue.PremiumDays;
            list = list * InsuranceCatalogue.Clamp(level, PriceRules.Least, PriceRules.Most) / PriceRules.Start;
            return (int)Math.Max(1, Math.Min(int.MaxValue / 4, (list + 500) / 1000));
        }

        /// <summary>
        /// A claim on a home's policy: a repair at home by its tier's terms, or a repair to the holder's own things where the tier covers belongings; never in the
        /// policy's first days. Counted in the policy's year and against its next premiums, and the no-claims discount lost. Nothing is booked here.
        /// </summary>
        public static long HomeClaim(HomePolicy p, bool belongings, long loss, int day, int yearDays, int level, out long borne, out ClaimWhy why)
        {
            borne = 0;
            if (p == null || !p.Active)
            {
                why = ClaimWhy.NoCover;
                return 0;
            }
            if (Waiting(p.Since, day, InsuranceCatalogue.HomeWaitDays))
            {
                why = ClaimWhy.Waiting;
                return 0;
            }
            TurnYear(p, day, yearDays);
            int tier = InsuranceCatalogue.Tier(p.Tier);
            int share = belongings ? InsuranceCatalogue.BelongingsShare[tier] : InsuranceCatalogue.HomeRepairShare[tier];
            long pays = Pays(loss, At(InsuranceCatalogue.HomeExcess[tier], level), share, p.PaidYear, At(InsuranceCatalogue.HomeYearCap[tier], level), p.ClaimsYear,
                InsuranceCatalogue.HomeYearClaims[tier], out borne, out why);
            if (pays > 0)
            {
                p.PaidYear += pays;
                p.ClaimsYear++;
                p.NoClaimMonths = 0;
                while (p.ClaimDays.Count >= 8)
                {
                    p.ClaimDays.RemoveAt(0);
                }
                p.ClaimDays.Add(day);
            }
            return pays;
        }

        /// <summary>
        /// Which of so many homes a repair at home happened at on a day: the same home on every machine for the same day, a different one from day to day. Each home
        /// has its own policy, so only that home's pays.
        /// </summary>
        public static int HomeAt(int day, int homes)
        {
            if (homes <= 1)
            {
                return 0;
            }
            unchecked
            {
                uint h = (uint)Math.Max(0, day) * 2654435761u;
                h ^= h >> 15;
                return (int)(h % (uint)homes);
            }
        }

        /// <summary>A home's policy begins: its first month is paid by the caller. The year's sums and the claims that count stay as they were.</summary>
        public static void Take(HomePolicy p, int tier, int premium, int day, int monthDays)
        {
            p.Tier = InsuranceCatalogue.Tier(tier);
            p.Active = true;
            p.Since = day;
            p.NextDue = day + Math.Max(1, monthDays);
            p.GraceFrom = 0;
            p.Premium = Math.Max(1, premium);
        }

        /// <summary>A home's policy ends. Its record stays: the year's sums, the claims that count and the months without a claim.</summary>
        public static void End(HomePolicy p)
        {
            p.Active = false;
            p.Since = p.NextDue = p.GraceFrom = 0;
        }

        /// <summary>True when a home's premium is due. Days past the due day are still one premium: time away is not billed twice.</summary>
        public static bool Due(HomePolicy p, int day) => p.Active && p.NextDue > 0 && day >= p.NextDue;

        /// <summary>The due day after a premium was taken on a day: the cycle's own day, the first after today.</summary>
        public static int NextDueAfter(int due, int day, int monthDays)
        {
            int month = Math.Max(1, monthDays);
            return due <= 0 ? day + month : due + (day >= due ? (day - due) / month + 1 : 1) * month;
        }

        /// <summary>A home's premium was taken: the next is due on the cycle's day, the days of grace are over, and a month without a claim is counted.</summary>
        public static void Paid(HomePolicy p, int premium, int day, int monthDays)
        {
            p.NextDue = NextDueAfter(p.NextDue, day, monthDays);
            p.GraceFrom = 0;
            p.Premium = Math.Max(1, premium);
            p.NoClaimMonths = Math.Min(p.NoClaimMonths + 1, InsuranceCatalogue.NoClaimMost / InsuranceCatalogue.NoClaimStep);
        }

        // ---- the rival tycoons

        /// <summary>The health plan a tycoon holds by their fortune against their floor: basic, standard from twice the floor, full from four times (1 to 3).</summary>
        public static int RivalPlan(long worth, long floor)
        {
            floor = Math.Max(1L, floor);
            return worth >= floor * InsuranceCatalogue.RivalFullFrom ? 3 : worth >= floor * InsuranceCatalogue.RivalStandardFrom ? 2 : 1;
        }

        /// <summary>
        /// A tycoon's cover on a day: with none they take the plan their fortune goes with, and a month's premium falls due on its day. <paramref name="quote"/> answers
        /// a plan's premium today and <paramref name="pay"/> what really left their worth (only the part over their floor can). A premium they cannot pay in full is
        /// missed, and after so many running the plan lapses until they can pay again. Returns what they paid today.
        /// </summary>
        public static long RivalDay(RivalCover c, long worth, long floor, int day, int yearDays, int monthDays, Func<int, long> quote, Func<long, long> pay)
        {
            if (worth < 0 || quote == null || pay == null)
            {
                return 0;   // not forward, out, or resting in a Going Public session
            }
            int year = YearOf(day, yearDays);
            if (year > c.Year)
            {
                c.Year = year;
                c.PaidYear = 0;
                c.PremiumsYear = 0;
            }
            if (c.Plan > 0 && day < c.NextDue)
            {
                return 0;
            }
            int plan = c.Plan > 0 ? c.Plan : RivalPlan(worth, floor);
            long premium = Math.Max(1L, quote(plan));
            if (worth - Math.Max(0L, floor) < premium)
            {
                RivalMissed(c, day, monthDays);
                return 0;
            }
            long paid = pay(premium);
            c.PremiumsYear += Math.Max(0L, paid);
            if (paid < premium)
            {
                RivalMissed(c, day, monthDays);   // (the floor moved under them between the look and the payment)
                return Math.Max(0L, paid);
            }
            c.Plan = plan;
            c.Missed = 0;
            c.NextDue = NextDueAfter(c.NextDue, day, monthDays);
            return paid;
        }

        private static void RivalMissed(RivalCover c, int day, int monthDays)
        {
            if (c.Plan == 0)
            {
                return;   // they hold none and cannot buy one today: asked again tomorrow
            }
            c.Missed++;
            if (c.Missed >= InsuranceCatalogue.RivalLapseAfter)
            {
                c.Plan = 0;
                c.Missed = 0;
                c.NextDue = 0;
            }
            else
            {
                c.NextDue = NextDueAfter(c.NextDue, day, monthDays);
            }
        }

        /// <summary>
        /// A tycoon's hospital bill: the part their plan pays (<paramref name="claim"/> answers it by Health's own rule for a plan, a bill and the year's payments so far)
        /// is counted in their year, and what is left is theirs to pay. Never more than the bill, never under nothing; the whole bill with no plan.
        /// </summary>
        public static long RivalBill(RivalCover c, long bill, int day, int yearDays, Func<int, long, long, long> claim)
        {
            bill = Math.Max(0L, bill);
            if (c == null || c.Plan <= 0 || claim == null || bill == 0)
            {
                return bill;
            }
            int year = YearOf(day, yearDays);
            if (year > c.Year)
            {
                c.Year = year;
                c.PaidYear = 0;
                c.PremiumsYear = 0;
            }
            long pays = InsuranceCatalogue.Clamp(claim(c.Plan, bill, c.PaidYear), 0L, bill);
            c.PaidYear += pays;
            return bill - pays;
        }

        // ---- the history

        /// <summary>A claim paid goes into the history, the oldest making room.</summary>
        public static ClaimNote Note(InsuranceState s, int day, CoverLine line, ClaimKind kind, string what, long loss, long excess, long payout)
        {
            while (s.Claims.Count >= InsuranceCatalogue.ClaimsKept)
            {
                s.Claims.RemoveAt(0);
            }
            var note = new ClaimNote { Day = day, Line = (int)line, Kind = (int)kind, What = what ?? string.Empty, Loss = loss, Excess = excess, Payout = payout };
            s.Claims.Add(note);
            return note;
        }

        /// <summary>What the policies have paid out in the year a day is in, from the history.</summary>
        public static long PaidInYear(InsuranceState s, int day, int yearDays)
        {
            long sum = 0;
            int year = YearOf(day, yearDays);
            for (int i = 0; i < s.Claims.Count; i++)
            {
                sum += YearOf(s.Claims[i].Day, yearDays) == year ? s.Claims[i].Payout : 0;
            }
            return sum;
        }
    }
}
