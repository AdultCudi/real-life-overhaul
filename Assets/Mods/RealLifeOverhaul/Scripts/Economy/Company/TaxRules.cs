using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the mod adds to the game's yearly tax: a quarterly return and its penalties, an accountant, and cash kept
    /// off the books with its audit risk. Game-free. The mod's costs are marked deductible as they are paid, so the
    /// game's own tax deducts them and nothing here works that out again.
    /// </summary>
    public static class TaxRules
    {
        private const int PurposeAudit = 71, PurposeError = 72;

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

        /// <summary>The quarter closes, the next begins; a return still waiting is dropped and counted late.</summary>
        public static void Close(TaxFile t, int takings, int day)
        {
            if (t.ReturnWaiting && t.ReturnPenalties == 0)
            {
                t.LateRun++;   // one that drew a penalty was counted then
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

        public static int Penalty(int takings)
        {
            return CompanyCatalogue.Clamp(CompanyCatalogue.Share(Math.Max(0, takings), CompanyCatalogue.PenaltyShare), CompanyCatalogue.PenaltyLeast,
                CompanyCatalogue.PenaltyMost);
        }

        /// <summary>Today's penalty on a waiting return, or 0: the first day late, then every few days.</summary>
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

        /// <summary>The return is filed. True if late; one filed on time ends a run of late ones.</summary>
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

        /// <summary>Enough late returns running bring an audit, and the count starts again.</summary>
        public static bool LateAudit(TaxFile t)
        {
            if (t.LateRun < CompanyCatalogue.LateForAudit)
            {
                return false;
            }
            t.LateRun = 0;
            return true;
        }

        /// <summary>A month's fee at today's prices; the cheapest charges more with cash off the books.</summary>
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

        /// <summary>Whether the accountant files the return: with cash off the books only the cheapest does.</summary>
        public static bool Files(int tier, bool hiding)
        {
            return tier >= 0 && (!hiding || tier == 0);
        }

        /// <summary>The rebate an accountant finds in the quarter's deductible costs, paid on filing.</summary>
        public static int Rebate(int tier, int costs)
        {
            return tier < 0 ? 0 : CompanyCatalogue.Share(Math.Max(0, costs), CompanyCatalogue.AccountantSaving[tier]);
        }

        /// <summary>Whether the cheapest gets this quarter's return wrong; the same for a seed and a quarter.</summary>
        public static bool Errs(int tier, int seed, int quarter)
        {
            return tier == 0 && CityModel.Draw(seed, PurposeError, quarter, 1000) < CompanyCatalogue.AccountantErrorChance;
        }

        public static int ErrorPenalty(int takings)
        {
            return CompanyCatalogue.Share(Penalty(takings), CompanyCatalogue.AccountantErrorShare);
        }

        /// <summary>The most that may be hidden, per cent of takings.</summary>
        public static int HideMost(int level)
        {
            return CompanyCatalogue.Level(level) == CompanyCatalogue.Relaxed ? CompanyCatalogue.HideMostRelaxed : CompanyCatalogue.HideSteps[CompanyCatalogue.HideSteps.Length - 1];
        }

        /// <summary>A share brought down to the nearest step the strictness allows.</summary>
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

        /// <summary>The tax hidden takings would bear at the game's rate: paid back, and owed again if found.</summary>
        public static int Saved(int takings, int hide, int taxPercent)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Math.Max(0, takings) * Math.Max(0, hide) * Math.Max(0, taxPercent) / 10000);
        }

        /// <summary>The risk after a day: up with every per cent hidden, slowly down while nothing is.</summary>
        public static int RiskDay(int risk, int hidden)
        {
            return hidden > 0 ? Math.Min(CompanyCatalogue.RiskMost, risk + hidden * CompanyCatalogue.RiskPerPercent) : Math.Max(0, risk - CompanyCatalogue.RiskDecay);
        }

        /// <summary>The chance of an audit today, ten-thousandths: less with an accountant on the books.</summary>
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

        public static bool Court(int backTax, int level)
        {
            return CompanyCatalogue.Level(level) != CompanyCatalogue.Relaxed && backTax >= CompanyCatalogue.CourtSum;
        }

        /// <summary>An audit: all saved since the last is owed again with a fine. Returns what is owed.</summary>
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
