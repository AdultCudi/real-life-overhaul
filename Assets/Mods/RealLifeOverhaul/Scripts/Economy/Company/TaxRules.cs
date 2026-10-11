using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the mod adds to the game's own tax, by rule: a return to file each quarter and what filing late costs, an
    /// accountant, and cash sales kept off the books with the risk that grows with them. Game-free.
    ///
    /// The game's own tax is left exactly as it is: once a year it taxes the businesses' sales, less the expenses it
    /// has marked deductible, if the year's income passes its threshold. The mod's own business costs are marked the
    /// same way when they are paid, so the game's tax deducts them itself; nothing here works that out again.
    /// </summary>
    public static class TaxRules
    {
        private const int PurposeAudit = 71, PurposeError = 72;

        // ---------------------------------------------------------------- the quarter and its return

        /// <summary>A quarter is three of the city's months.</summary>
        public static int QuarterDays(int monthDays)
        {
            return CompanyCatalogue.QuarterMonths * Math.Max(1, monthDays);
        }

        public static bool QuarterOver(TaxFile t, int day, int quarterDays)
        {
            return day - t.QuarterFrom >= quarterDays;
        }

        /// <summary>A business cost of the mod's own, counted in the quarter that is running.</summary>
        public static void Note(TaxFile t, CostKind kind, int amount)
        {
            if (amount > 0)
            {
                t.Costs[(int)kind] = (int)Math.Min(int.MaxValue / 2, (long)t.Costs[(int)kind] + amount);
            }
        }

        public static int CostsSum(TaxFile t)
        {
            long sum = 0;
            for (int k = 0; k < t.Costs.Length; k++)
            {
                sum += t.Costs[k];
            }
            return (int)Math.Min(int.MaxValue / 2, sum);
        }

        /// <summary>
        /// The quarter closes: its return waits to be filed, and the next quarter begins today. A return of the
        /// quarter before that was never filed is dropped and counted as one more late return.
        /// </summary>
        public static void Close(TaxFile t, int takings, int day)
        {
            if (t.ReturnWaiting && t.ReturnPenalties == 0)
            {
                t.LateRun++;   // (one that already drew a penalty has been counted)
            }
            t.ReturnQuarter = t.Quarter;
            t.ReturnTakings = Math.Max(0, takings);
            t.ReturnCosts = CostsSum(t);
            t.ReturnDue = day + CompanyCatalogue.ReturnDays;
            t.ReturnPenalties = 0;
            Array.Clear(t.Costs, 0, t.Costs.Length);
            t.Quarter++;
            t.QuarterFrom = day;
        }

        /// <summary>The penalty for a late return: a share of the quarter's takings, between a least and a most.</summary>
        public static int Penalty(int takings)
        {
            return CompanyCatalogue.Clamp(CompanyCatalogue.Share(Math.Max(0, takings), CompanyCatalogue.PenaltyShare), CompanyCatalogue.PenaltyLeast, CompanyCatalogue.PenaltyMost);
        }

        /// <summary>
        /// A day with a return waiting: the penalty that falls due today, or nothing. One on the first day late and
        /// one more after each further five days, three at most for one return.
        /// </summary>
        public static int LateDay(TaxFile t, int day)
        {
            if (!t.ReturnWaiting || day <= t.ReturnDue)
            {
                return 0;
            }
            int due = Math.Min(CompanyCatalogue.PenaltiesMost, 1 + (day - t.ReturnDue - 1) / CompanyCatalogue.PenaltyRepeatDays);
            if (t.ReturnPenalties >= due)
            {
                return 0;
            }
            if (t.ReturnPenalties == 0)
            {
                t.LateRun++;
            }
            t.ReturnPenalties++;
            return Penalty(t.ReturnTakings);
        }

        /// <summary>The return is filed. True if that was late. A return filed on time ends a run of late ones.</summary>
        public static bool File(TaxFile t, int day)
        {
            if (!t.ReturnWaiting)
            {
                return false;
            }
            bool late = day > t.ReturnDue;
            if (!late)
            {
                t.LateRun = 0;
            }
            t.ReturnDue = 0;
            return late;
        }

        /// <summary>Late returns running have brought an audit: the run starts again.</summary>
        public static bool LateAudit(TaxFile t)
        {
            if (t.LateRun < CompanyCatalogue.LateForAudit)
            {
                return false;
            }
            t.LateRun = 0;
            return true;
        }

        // ---------------------------------------------------------------- the accountant

        /// <summary>A month's fee at today's prices. The cheapest charges half as much again while cash is kept off the books.</summary>
        public static int Fee(int tier, int index, bool hiding)
        {
            int fee = CompanyCatalogue.AccountantFee[tier];
            return CompanyCatalogue.AtPrices(tier == 0 && hiding ? CompanyCatalogue.Share(fee, CompanyCatalogue.AccountantHidingFee) : fee, index);
        }

        /// <summary>The dearest will not keep books with cash left out of them.</summary>
        public static bool Resigns(int tier, bool hiding)
        {
            return hiding && tier == CompanyCatalogue.Accountants - 1;
        }

        /// <summary>Whether the accountant files the return: always, unless cash is kept off the books; then only the cheapest does.</summary>
        public static bool Files(int tier, bool hiding)
        {
            return tier >= 0 && (!hiding || tier == 0);
        }

        /// <summary>The savings an accountant finds in a quarter's deductible costs, paid as a rebate when the return is filed.</summary>
        public static int Rebate(int tier, int costs)
        {
            return tier < 0 ? 0 : CompanyCatalogue.Share(Math.Max(0, costs), CompanyCatalogue.AccountantSaving[tier]);
        }

        /// <summary>Whether the cheapest gets this quarter's return wrong; the same answer for a seed and a quarter.</summary>
        public static bool Errs(int tier, int seed, int quarter)
        {
            return tier == 0 && CityModel.Draw(seed, PurposeError, quarter, 1000) < CompanyCatalogue.AccountantErrorChance;
        }

        public static int ErrorPenalty(int takings)
        {
            return CompanyCatalogue.Share(Penalty(takings), CompanyCatalogue.AccountantErrorShare);
        }

        // ---------------------------------------------------------------- cash kept off the books

        /// <summary>The most that may be hidden, per cent of takings: ten on Relaxed.</summary>
        public static int HideMost(int level)
        {
            return CompanyCatalogue.Level(level) == CompanyCatalogue.Relaxed ? CompanyCatalogue.HideMostRelaxed : CompanyCatalogue.HideSteps[CompanyCatalogue.HideSteps.Length - 1];
        }

        /// <summary>A share brought to the nearest step at or under it that the strictness allows.</summary>
        public static int HideStep(int percent, int level)
        {
            int most = HideMost(level), step = 0;
            for (int i = 0; i < CompanyCatalogue.HideSteps.Length; i++)
            {
                if (CompanyCatalogue.HideSteps[i] <= percent && CompanyCatalogue.HideSteps[i] <= most)
                {
                    step = CompanyCatalogue.HideSteps[i];
                }
            }
            return step;
        }

        /// <summary>The tax a day's hidden takings would have borne at the game's own rate: what is paid back as a rebate, and owed again if it is found.</summary>
        public static int Saved(int takings, int hide, int taxPercent)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, takings) * Math.Max(0, hide) * Math.Max(0, taxPercent) / 10000);
        }

        /// <summary>The risk after a day: up with every per cent hidden at every business, slowly down while nothing is.</summary>
        public static int RiskDay(int risk, int hidden)
        {
            return hidden > 0 ? Math.Min(CompanyCatalogue.RiskMost, risk + hidden * CompanyCatalogue.RiskPerPercent) : Math.Max(0, risk - CompanyCatalogue.RiskDecay);
        }

        /// <summary>The chance of an audit today, ten-thousandths: the risk, less with an accountant on the books.</summary>
        public static int AuditChance(int risk, int tier)
        {
            return tier >= 0 && tier < CompanyCatalogue.Accountants ? CompanyCatalogue.Share(risk, CompanyCatalogue.AccountantRisk[tier]) : risk;
        }

        /// <summary>Whether the tax office audits today; the same answer for a seed and a day.</summary>
        public static bool Audited(TaxFile t, int seed, int age)
        {
            int chance = AuditChance(t.Risk, t.Accountant);
            return chance > 0 && CityModel.Draw(seed, PurposeAudit, age, 10000) < chance;
        }

        public static int Fine(int backTax, int level)
        {
            return CompanyCatalogue.Share(Math.Max(0, backTax), CompanyCatalogue.FineShare[CompanyCatalogue.Level(level)]);
        }

        /// <summary>Whether what was found goes to court: from a stated sum, and never on Relaxed.</summary>
        public static bool Court(int backTax, int level)
        {
            return CompanyCatalogue.Level(level) != CompanyCatalogue.Relaxed && backTax >= CompanyCatalogue.CourtSum;
        }

        /// <summary>
        /// An audit. Everything saved since the last one is found: it is owed again with a fine on top, the count
        /// starts from nothing and so does the risk. Returns what is owed for it; nothing if nothing was hidden.
        /// </summary>
        public static int Audit(TaxFile t, int level, int day, out int backTax, out int fine)
        {
            backTax = Math.Max(0, t.Saved);
            fine = Fine(backTax, level);
            t.Saved = 0;
            t.Risk = 0;
            t.LastAudit = day;
            t.Owed = (int)Math.Min(int.MaxValue / 2, (long)t.Owed + backTax + fine);
            return backTax + fine;
        }

        /// <summary>What is taken today of what is owed: as far as the cash in hand goes.</summary>
        public static int TakeOwed(TaxFile t, int cash)
        {
            int take = Math.Max(0, Math.Min(t.Owed, cash));
            t.Owed -= take;
            return take;
        }
    }
}
