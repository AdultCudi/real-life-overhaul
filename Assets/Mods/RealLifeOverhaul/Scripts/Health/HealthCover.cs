using System;

namespace RealLifeOverhaul.Health
{
    /// <summary>How a bill was met: what a plan paid, what the player paid on the day, and what became a debt.</summary>
    public struct BillResult
    {
        public long Bill, Claim, Paid, Owed;
        public Plan By;        // the plan that paid the claim
        public bool Employer;  // ...which was the employer's
    }

    /// <summary>
    /// Health insurance and what care costs, game-free: a plan's premium by age and record, what a plan pays of a bill (never more than the bill, never for what began
    /// before the cover or in its first days, never over its year's cap), the month's premium and the lapse, and the medical debt with its instalments. COST: a few
    /// operations a call, nothing allocated.
    /// </summary>
    public static class HealthCover
    {
        private static int P(Plan p) => HealthCatalogue.Clamp((int)p, 0, (int)Plan.Count - 1);

        // ---- the premium

        /// <summary>The age band's share of the premium, thousandths.</summary>
        public static int AgeShare(int years)
        {
            int share = HealthCatalogue.AgeShare[0];
            for (int i = 0; i < HealthCatalogue.AgeFrom.Length; i++)
            {
                share = years >= HealthCatalogue.AgeFrom[i] ? HealthCatalogue.AgeShare[i] : share;
            }
            return share;
        }

        /// <summary>The claims of the last year, as the next premium counts them.</summary>
        public static int ClaimsLastYear(HealthPolicy p, int day, int yearDays)
        {
            int n = 0;
            for (int i = 0; i < p.ClaimDays.Count; i++)
            {
                n += day - p.ClaimDays[i] < yearDays ? 1 : 0;
            }
            return n;
        }

        /// <summary>A month's premium of a plan for somebody of an age with so many claims in the last year, at today's prices.</summary>
        public static int Premium(Plan plan, int years, int claims, int level)
        {
            if (plan == Plan.None)
            {
                return 0;
            }
            long loading = 1000 + Math.Min(HealthCatalogue.ClaimLoadingMost, (long)Math.Max(0, claims) * HealthCatalogue.ClaimLoading);
            long start = (long)HealthCatalogue.PremiumMonth[P(plan)] * AgeShare(years) / 1000 * loading / 1000;
            return Math.Max(1, HealthRules.At((int)start, level));
        }

        // ---- what a plan pays

        /// <summary>True if a policy taken on a day covers something that began on another: only what begins after the policy's first days.</summary>
        public static bool Covers(Plan plan, int since, int began) => plan != Plan.None && since > 0 && began >= since + HealthCatalogue.WaitDays;

        /// <summary>True if an employer's plan covers it: the job with the plan began before it did.</summary>
        public static bool EmployerCovers(Plan plan, int since, int began) => plan != Plan.None && since > 0 && began >= since + HealthCatalogue.EmployerWaitDays;

        /// <summary>
        /// What a plan pays of a bill: its share of a consultation or a prescription, or its share of a stay over the excess, held to what is left of the year's cap, never
        /// under nothing and never over the bill.
        /// </summary>
        public static long Claim(Plan plan, Care care, long bill, long paidThisYear, int level)
        {
            if (plan == Plan.None || bill <= 0)
            {
                return 0;
            }
            int p = P(plan);
            long pays = care == Care.Hospital
                ? Math.Max(0L, bill - HealthRules.At(HealthCatalogue.Excess[p], level)) * HealthCatalogue.HospitalShare[p] / 1000
                : bill * HealthCatalogue.DoctorShare[p] / 1000;
            long left = Math.Max(0L, (long)HealthRules.At(HealthCatalogue.YearCap[p], level) - Math.Max(0L, paidThisYear));
            return HealthCatalogue.Clamp(Math.Min(pays, left), 0L, bill);
        }

        /// <summary>The bill of a stay of so many days, at today's prices; the game's own first night, where there was one, is the game's bill and not in it.</summary>
        public static long StayBill(int days, bool gameNight, int level)
        {
            days = HealthCatalogue.Clamp(days, 0, HealthCatalogue.StayMost);
            int own = gameNight ? Math.Max(0, days - 1) : days;
            return (gameNight ? 0L : HealthRules.At(HealthCatalogue.Admission, level)) + (long)own * HealthRules.At(HealthCatalogue.StayDay, level);
        }

        /// <summary>
        /// A bill is met: the better of the player's own plan and the employer's pays its part where it covers what began on that day (an accident at work is the employer's
        /// insurer's in full), and the rest is the player's. The policy's year is counted and the claim's day kept for the next premium. Nothing is booked here: the caller
        /// takes <see cref="BillResult.Bill"/> less the claim from the player, or makes a debt of it.
        /// </summary>
        public static BillResult Settle(HealthState s, Care care, long bill, int began, bool atWork, Plan employer, int employerSince, int day, int yearDays, int level)
        {
            var r = new BillResult { Bill = Math.Max(0L, bill) };
            if (r.Bill == 0)
            {
                return r;
            }
            TurnYear(s, day, yearDays);
            HealthPolicy p = s.Policy;
            long own = Covers(p.Plan, p.Since, began) ? Claim(p.Plan, care, r.Bill, p.PaidYear, level) : 0L;
            long theirs = atWork ? r.Bill : EmployerCovers(employer, employerSince, began) ? Claim(employer, care, r.Bill, 0L, level) : 0L;
            if (theirs >= own && theirs > 0)
            {
                r.Claim = theirs;
                r.By = atWork ? Plan.Full : employer;
                r.Employer = true;
            }
            else if (own > 0)
            {
                r.Claim = own;
                r.By = p.Plan;
                p.PaidYear += own;
                if (p.ClaimDays.Count >= HealthCatalogue.ClaimsKept)
                {
                    p.ClaimDays.RemoveAt(0);
                }
                p.ClaimDays.Add(day);
            }
            r.Claim = HealthCatalogue.Clamp(r.Claim, 0L, r.Bill);
            s.BillsYear += r.Bill;
            s.ClaimsYear += r.Claim;
            s.LastBill = r.Bill;
            s.LastClaim = r.Claim;
            s.LastBillDay = day;
            return r;
        }

        /// <summary>The year turns: the year's bills and what the plans paid start again, and the policy's cap with them.</summary>
        public static void TurnYear(HealthState s, int day, int yearDays)
        {
            int year = HealthRules.YearOf(day, yearDays);
            if (s.Year != year)
            {
                s.Year = year;
                s.BillsYear = s.ClaimsYear = 0;
            }
            if (s.Policy.Year != year)
            {
                s.Policy.Year = year;
                s.Policy.PaidYear = 0;
            }
        }

        // ---- the policy

        /// <summary>
        /// A plan is taken (or changed): a new policy from today, whose first days cover nothing new and which never covers what is under way. The first month is paid by
        /// the caller.
        /// </summary>
        public static void Take(HealthPolicy p, Plan plan, int day, int monthDays)
        {
            p.Plan = plan;
            p.Since = day;
            p.NextDue = day + Math.Max(1, monthDays);
            p.Missed = 0;
            p.PaidYear = 0;
        }

        public static void Cancel(HealthPolicy p)
        {
            p.Plan = Plan.None;
            p.Since = p.NextDue = p.Missed = 0;
        }

        /// <summary>
        /// The policy's day: 0 nothing due, 1 a premium due now (the caller takes it and says <see cref="Paid"/> or <see cref="Missed"/>). A day past the due day is still one
        /// premium: months away are not billed twice.
        /// </summary>
        public static bool Due(HealthPolicy p, int day) => p.Plan != Plan.None && p.NextDue > 0 && day >= p.NextDue;

        public static void Paid(HealthPolicy p, int day, int monthDays)
        {
            p.Missed = 0;
            p.NextDue = Math.Max(p.NextDue, day - Math.Max(1, monthDays) + 1) + Math.Max(1, monthDays);
        }

        /// <summary>A premium could not be taken; true if the policy lapsed with it.</summary>
        public static bool Missed(HealthPolicy p, int day, int monthDays)
        {
            p.Missed++;
            if (p.Missed >= HealthCatalogue.LapseAfter)
            {
                Cancel(p);
                return true;
            }
            p.NextDue = Math.Max(p.NextDue, day - Math.Max(1, monthDays) + 1) + Math.Max(1, monthDays);
            return false;
        }

        // ---- the medical debt

        /// <summary>What could not be paid on the day is owed: added to the debt, whose instalment pays it off in <see cref="HealthCatalogue.DebtMonths"/> months with no interest.</summary>
        public static void Owe(HealthDebt d, long amount, int monthDays, int day)
        {
            if (amount <= 0)
            {
                return;
            }
            d.Owed += amount;
            long days = Math.Max(1, (long)HealthCatalogue.DebtMonths * Math.Max(1, monthDays));
            d.PerDay = (int)Math.Min(int.MaxValue / 4, Math.Max(HealthCatalogue.DebtLeastADay, (d.Owed + days - 1) / days));
            d.LastDay = Math.Max(d.LastDay, day);   // the first instalment is tomorrow's
        }

        /// <summary>What today's instalment is: the instalment or what is left, nothing for a day already tried.</summary>
        public static long Instalment(HealthDebt d, int day) => d.Owed > 0 && day > d.LastDay ? Math.Min(d.Owed, Math.Max(1, d.PerDay)) : 0L;

        /// <summary>The day's instalment was taken, or could not be. True when the debt is paid off.</summary>
        public static bool Collected(HealthDebt d, int day, long taken)
        {
            d.LastDay = day;
            if (taken <= 0)
            {
                d.Late++;
                return false;
            }
            taken = Math.Min(taken, d.Owed);
            d.Owed -= taken;
            d.Paid += taken;
            d.Late = 0;
            if (d.Owed == 0)
            {
                d.PerDay = 0;
                return true;
            }
            return false;
        }

        /// <summary>True when the lender goes to court: so many days running with nothing taken.</summary>
        public static bool GoesToCourt(HealthDebt d) => d.Owed > 0 && d.Late >= HealthCatalogue.DebtClaimAfter;

        /// <summary>The claim is the court's now: what was owed is collected as a court's debt, and no longer here.</summary>
        public static void Filed(HealthDebt d, int claim)
        {
            if (claim > 0)
            {
                d.Claim = claim;
                d.Owed = 0;
                d.PerDay = 0;
                d.Late = 0;
            }
        }
    }
}
