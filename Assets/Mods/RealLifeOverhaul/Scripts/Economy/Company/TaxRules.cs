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

        public const int SettledKept = 4;                                   // past returns kept for the page
        public const int StateOnTime = 0, StateLate = 1, StateMissed = 2;   // how a return ended

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
        /// The quarter closes, the next begins; a return still waiting is dropped and counted late. The closed quarter's business tax at the game's rate
        /// (<paramref name="percent"/>) is worked out for the return and added to the year's account, which the tax reserve is for.
        /// </summary>
        public static void Close(TaxFile t, int takings, int day, int percent = 0)
        {
            if (t.ReturnWaiting && t.ReturnPenalties == 0)
            {
                t.LateRun++;   // one that drew a penalty was counted then
            }
            if (t.ReturnWaiting)
            {
                Settle(t, StateMissed, day);   // dropped unfiled
            }
            t.ReturnQuarter = t.Quarter;
            t.ReturnTakings = Math.Max(0, takings);
            t.ReturnCosts = CostsSum(t);
            t.ReturnTax = QuarterTax(t.ReturnTakings, t.ReturnCosts, percent);
            t.YearTax = (int)Math.Min(int.MaxValue / 4, (long)t.YearTax + t.ReturnTax);
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
            Settle(t, late ? StateLate : StateOnTime, day);
            t.ReturnDue = 0;
            return late;
        }

        /// <summary>The waiting return is done with: it goes on the ring of past returns, the oldest out past <see cref="SettledKept"/>.</summary>
        public static void Settle(TaxFile t, int state, int day)
        {
            if (t.ReturnQuarter <= 0)
            {
                return;
            }
            t.Settled.Add(new SettledReturn
            {
                Quarter = t.ReturnQuarter, Day = day, Takings = t.ReturnTakings, Costs = t.ReturnCosts, Tax = t.ReturnTax,
                State = CompanyCatalogue.Clamp(state, StateOnTime, StateMissed)
            });
            while (t.Settled.Count > SettledKept)
            {
                t.Settled.RemoveAt(0);
            }
        }

        // ---------------------------------------------------------------- the quarter's tax and the reserve for it

        /// <summary>What is left of a quarter's takings after the costs that count, never below nothing.</summary>
        public static int Taxable(int takings, int costs)
        {
            return (int)Math.Max(0L, Math.Min(int.MaxValue / 2, (long)takings - Math.Max(0, costs)));
        }

        /// <summary>The business tax at the game's rate on a quarter's taxable profit: an estimate, the game's own bill also takes the costs it counts itself.</summary>
        public static int QuarterTax(int takings, int costs, int percent)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)Taxable(takings, costs) * Math.Max(0, percent) / 100);
        }

        /// <summary>The quarter that is running, as the page shows it: what it took, the costs that count, the profit taxed, the tax, and what is set aside against the year's.</summary>
        public struct QuarterFigures
        {
            public int Takings, Costs, Taxable, Tax, Percent;
            public int Year;        // the tax of the quarters already closed this tax year
            public int Reserve;     // set aside
            public int Still;       // still to set aside: the year's tax so far less the reserve
        }

        public static QuarterFigures Quarter(TaxFile t, int takings, int percent)
        {
            var q = new QuarterFigures { Takings = Math.Max(0, takings), Costs = CostsSum(t), Percent = Math.Max(0, percent), Year = t.YearTax, Reserve = t.Reserve };
            q.Taxable = Taxable(q.Takings, q.Costs);
            q.Tax = QuarterTax(q.Takings, q.Costs, q.Percent);
            q.Still = (int)Math.Max(0L, Math.Min(int.MaxValue / 2, (long)q.Year + q.Tax - q.Reserve));
            return q;
        }

        /// <summary>The game's tax year a day is in: its first day (the game's yearly bill is for days of one).</summary>
        public static int YearStart(int day, int daysPerYear)
        {
            int length = Math.Max(1, daysPerYear);
            return (Math.Max(1, day) - 1) / length * length + 1;
        }

        /// <summary>
        /// The year the reserve is for turns over: what was set aside goes back to the cash and the year's account starts afresh. False the first time a year is
        /// known (nothing to turn over). <paramref name="released"/> is what goes back.
        /// </summary>
        public static bool Year(TaxFile t, int yearFrom, out int released)
        {
            released = 0;
            if (t.YearFrom == yearFrom)
            {
                return false;
            }
            bool first = t.YearFrom == 0;
            t.YearFrom = yearFrom;
            if (first)
            {
                return false;
            }
            released = t.Reserve;
            t.Reserve = 0;
            t.YearTax = 0;
            return true;
        }

        /// <summary>Money set aside out of the cash: what is asked for, as far as the cash and the most the reserve holds go. Returns what moved.</summary>
        public static int SetAside(TaxFile t, int amount, int cash)
        {
            int moved = (int)Math.Max(0L, Math.Min(Math.Min(amount, cash), CompanyCatalogue.ReserveHeld - (long)t.Reserve));
            t.Reserve += moved;
            return moved;
        }

        /// <summary>The finance chief's top-up when a quarter closes: the shortfall against the year's tax so far, never more than a share of the cash. Returns what moved.</summary>
        public static int TopUp(TaxFile t, int cash)
        {
            int short_ = Math.Max(0, t.YearTax - t.Reserve);
            return short_ > 0 ? SetAside(t, Math.Min(short_, CompanyCatalogue.Share(Math.Max(0, cash), CompanyCatalogue.ReserveCashShare)), cash) : 0;
        }

        /// <summary>The reserve goes back to the cash, all of it. Returns what goes back.</summary>
        public static int Release(TaxFile t)
        {
            int back = t.Reserve;
            t.Reserve = 0;
            return back;
        }

        /// <summary>The day a quarter that is running closes, the day the finance chief tops the reserve up.</summary>
        public static int TopUpDay(TaxFile t, int quarterDays)
        {
            return t.QuarterFrom + Math.Max(1, quarterDays);
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

        /// <summary>Quarters a tax case settled with the city keeps the inspector away: audits at half the chance.</summary>
        public const int QuietQuarters = 4;

        /// <summary>The chance of an audit on this day, ten-thousandths: halved while a settlement keeps the inspector away.</summary>
        public static int AuditChance(TaxFile t, int day)
        {
            int chance = AuditChance(t.Risk, t.Accountant);
            return day < t.QuietUntil ? chance / 2 : chance;
        }

        /// <summary>Whether the tax office audits on this day, a settlement's quiet time counted; the same answer for a seed and a day.</summary>
        public static bool Audited(TaxFile t, int seed, int age, int day)
        {
            int chance = AuditChance(t, day);
            return chance > 0 && CityModel.Draw(seed, PurposeAudit, age, 10000) < chance;
        }

        /// <summary>A tax case settled on this day: the inspector comes at half the chance for <see cref="QuietQuarters"/> quarters.</summary>
        public static void Settled(TaxFile t, int day, int quarterDays)
        {
            t.QuietUntil = Math.Max(t.QuietUntil, day + QuietQuarters * Math.Max(1, quarterDays));
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
