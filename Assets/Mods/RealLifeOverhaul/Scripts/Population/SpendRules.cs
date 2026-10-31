using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>What a household took in on a day, cents.</summary>
    public struct DayIncome
    {
        public int Wages, Benefits, Pension, Returns;
        public int IncomeTax;

        public long Gross => (long)Wages + Benefits + Pension + Returns;
        public long Disposable => Gross - IncomeTax;
    }

    /// <summary>
    /// A household's money for a day (POPULATION.md section 6): its pay from its people's jobs, benefits for those out of work, pensions,
    /// returns on its savings; income tax by each earner's pay; rent; then each kind of spending as its share of what is left by its income
    /// against the city's middle (the Consumer Expenditure Survey's shape, PopulationCatalogue.SpendShares) and its tastes, and saving what
    /// is left. The day goes into its month's ledger (Ledger), its savings and debt, and the council's books; what it spends at businesses goes
    /// into the day's pools (PoolRules). Game-free. COST: nothing per frame; some forty operations a household.
    /// </summary>
    public static class SpendRules
    {
        public const int Kinds = PopulationCatalogue.Categories;
        public const int DebtPayShare = 10;      // thousandths of its debt a day a household with savings to spare pays off
        public const int DebtPayBuffer = 30;     // days of its pay it keeps in savings before paying debt off

        // A spending kind's three anchor levels: half the middle, the middle, three times it (thousandths of the city's middle).
        private const int LevelLow = 500, LevelMid = 1000, LevelHigh = 3000;

        /// <summary>The household's pay, benefits and pensions a day, cents (no returns): what its savings are measured in.</summary>
        public static long GrossDaily(PopulationState s, int h)
        {
            Income(s, h, s.Day, 0, out DayIncome inc);
            return inc.Gross;
        }

        /// <summary>A household's income on a day: pay, benefits, pensions and child benefit, returns on its savings at the bank rate's part (hundredths of a per cent), and its income tax.</summary>
        public static void Income(PopulationState s, int h, int day, int baseBp, out DayIncome inc)
        {
            inc = default;
            PersonTable pt = s.People;
            int children = 0;
            int benefitDays = PopulationCatalogue.BenefitDays * Math.Max(1, s.YearDays) / 365;
            for (int p = s.Households.First[h]; p >= 0; p = pt.Next[p])
            {
                switch ((Status)pt.Status[p])
                {
                    case Status.Worker:
                        int pay = LabourRules.DailyPay(s, p);
                        inc.Wages += pay;
                        inc.IncomeTax += Tax(pay);
                        break;
                    case Status.Unemployed:
                        if (day - pt.Since[p] < benefitDays && (pt.Flags[p] & PersonFlags.Frozen) == 0)
                        {
                            inc.Benefits += Math.Min(PopulationCatalogue.BenefitMost, (int)((long)LabourRules.DailyPay(s, p) * PopulationCatalogue.BenefitShare / 1000));
                        }
                        break;
                    case Status.Retired:
                        int pension = Math.Max(PopulationCatalogue.PensionLeast, (int)((long)LabourRules.DailyPay(s, p) * PopulationCatalogue.PensionShare / 1000));
                        inc.Pension += pension;
                        inc.IncomeTax += Tax(pension);
                        break;
                    case Status.Child:
                    case Status.Pupil:
                        children++;
                        break;
                }
            }
            if (children > 0 && inc.Wages < PopulationCatalogue.ChildBenefitBelow)
            {
                inc.Benefits += children * PopulationCatalogue.ChildBenefit;
            }
            int savings = s.Households.Savings[h];
            if (savings > 0)
            {
                long bp = PopulationCatalogue.ReturnPoints + Math.Max(-PopulationCatalogue.ReturnPoints, baseBp);
                inc.Returns = (int)Math.Min(int.MaxValue / 8, (long)savings * 100 * bp / 10000 / Math.Max(1, s.YearDays));
            }
        }

        /// <summary>Income tax on a day's pay or pension, cents: each step's rate on the part above its start.</summary>
        public static int Tax(int daily)
        {
            if (daily <= 0)
            {
                return 0;
            }
            long tax = 0;
            var steps = PopulationCatalogue.IncomeTax;
            for (int i = 0; i < steps.Length; i++)
            {
                long from = steps[i].From, to = i + 1 < steps.Length ? steps[i + 1].From : long.MaxValue;
                if (daily <= from)
                {
                    break;
                }
                tax += (Math.Min(daily, to) - from) * steps[i].Rate / 1000;
            }
            return (int)tax;
        }

        /// <summary>A household's income against the city's middle, thousandths.</summary>
        public static int Level(long disposable, long median)
        {
            if (median <= 0)
            {
                return LevelMid;
            }
            return (int)Math.Min(100000, Math.Max(0, disposable * 1000 / median));
        }

        /// <summary>
        /// Each kind's share of what a household has after tax and rent, thousandths, by its income's level: drawn between the catalogue's three rows
        /// on a log scale, then tilted towards what it likes (the total kept), and its getting about by whether it has a car.
        /// </summary>
        public static void Shares(int level, uint tastes, bool car, int[] into)
        {
            int[] low, high;
            double t;
            if (level <= LevelLow)
            {
                low = high = PopulationCatalogue.SpendShares[0];
                t = 0;
            }
            else if (level < LevelMid)
            {
                low = PopulationCatalogue.SpendShares[0];
                high = PopulationCatalogue.SpendShares[1];
                t = Math.Log(level / (double)LevelLow) / Math.Log(LevelMid / (double)LevelLow);
            }
            else if (level < LevelHigh)
            {
                low = PopulationCatalogue.SpendShares[1];
                high = PopulationCatalogue.SpendShares[2];
                t = Math.Log(level / (double)LevelMid) / Math.Log(LevelHigh / (double)LevelMid);
            }
            else
            {
                low = high = PopulationCatalogue.SpendShares[2];
                t = 0;
            }
            int total = 0, tilted = 0;
            for (int k = 0; k < Kinds; k++)
            {
                into[k] = (int)Math.Round(low[k] + (high[k] - low[k]) * t);
                total += into[k];
            }
            for (int bit = 0; bit < GenerateRules.TasteBits; bit++)
            {
                if ((tastes & (1u << bit)) != 0)
                {
                    int k = (int)GenerateRules.TasteKinds[bit];
                    into[k] += into[k] * PopulationCatalogue.TasteTilt / 1000;
                }
            }
            for (int k = 0; k < Kinds; k++)
            {
                tilted += into[k];
            }
            if (tilted > 0 && tilted != total)
            {
                for (int k = 0; k < Kinds; k++)
                {
                    into[k] = (int)((long)into[k] * total / tilted);   // tastes move money between kinds, not the total
                }
            }
            into[(int)Spend.Transport] = into[(int)Spend.Transport] * (car ? 1300 : 600) / 1000;   // a car costs fuel; without one, fares
            into[(int)Spend.Rent] = 0;
            into[(int)Spend.Saving] = 0;
        }

        /// <summary>
        /// The day's spending by kind, cents (<paramref name="budget"/>, Rent and Saving none), and what goes into or out of savings: the shares of
        /// what is left after tax and rent; when that is too little for the essentials, they are paid from savings and nothing else is bought.
        /// </summary>
        public static long Budget(PopulationState s, int h, in DayIncome inc, long median, int[] shares, int[] budget)
        {
            HouseholdTable hh = s.Households;
            long disposable = inc.Disposable;
            long rent = hh.Rent[h];
            long after = disposable - rent;
            Shares(Level(disposable, median), hh.Tastes[h], hh.Cars[h] > 0, shares);
            long essentialsShare = shares[(int)Spend.Utilities] + shares[(int)Spend.Groceries] + shares[(int)Spend.Transport];
            long floor = Math.Max(0, disposable) * essentialsShare / 1000 * 600 / 1000;   // the essentials of a lean month, whatever the rent
            long spent = 0;
            if (after * essentialsShare / 1000 < floor)
            {
                for (int k = 0; k < Kinds; k++)
                {
                    bool essential = k == (int)Spend.Utilities || k == (int)Spend.Groceries || k == (int)Spend.Transport;
                    budget[k] = essential && essentialsShare > 0 ? (int)(floor * shares[k] / essentialsShare) : 0;
                    spent += budget[k];
                }
            }
            else
            {
                long base0 = Math.Max(0, after);
                for (int k = 0; k < Kinds; k++)
                {
                    budget[k] = (int)Math.Min(int.MaxValue / 4, base0 * shares[k] / 1000);
                    spent += budget[k];
                }
            }
            return disposable - rent - spent;
        }

        /// <summary>
        /// The day booked: the ledger's lines (Ledger), savings and debt (whole money, what was drawn past the savings becoming debt, debt paid off
        /// from savings to spare), and the council's books. The sales tax is booked by the businesses that take it (PoolRules); the household's
        /// line holds its share of it, from what it spent at them.
        /// </summary>
        public static void Book(PopulationState s, int h, in DayIncome inc, int[] budget, long saving)
        {
            HouseholdTable hh = s.Households;
            int row = h * Ledger.Lines;
            int[] m = hh.Month;
            m[row + Ledger.Wages] += inc.Wages;
            m[row + Ledger.Benefits] += inc.Benefits;
            m[row + Ledger.Pension] += inc.Pension;
            m[row + Ledger.Returns] += inc.Returns;
            m[row + Ledger.IncomeTax] += inc.IncomeTax;
            int rent = hh.Rent[h];
            m[row + Ledger.Rent] += rent;
            int property = (int)((long)rent * PopulationCatalogue.PropertyTaxShare / 1000);
            m[row + Ledger.PropertyTax] += property;
            long shop = 0;
            for (int k = (int)Spend.Utilities; k <= (int)Spend.Digital; k++)
            {
                m[row + Ledger.SpendFirst + k - (int)Spend.Utilities] += budget[k];
                if (k >= PopulationCatalogue.ShopCategoryFirst && k <= PopulationCatalogue.ShopCategoryLast)
                {
                    shop += budget[k];
                }
            }
            m[row + Ledger.SalesTax] += (int)(shop * PopulationCatalogue.SalesTax / 1000);
            m[row + Ledger.Saving] += (int)Math.Max(int.MinValue / 4, Math.Min(int.MaxValue / 4, saving));

            long savings = hh.Savings[h] + saving / 100;
            long debt = hh.Debt[h];
            long debtBefore = debt;
            if (savings < 0)
            {
                debt -= savings;   // what could not be drawn from savings is borrowed
                savings = 0;
            }
            long buffer = inc.Gross / 100 * DebtPayBuffer;
            if (debt > 0 && savings > buffer)
            {
                long pay = Math.Min(debt, Math.Max(1, debt * DebtPayShare / 1000));
                pay = Math.Min(pay, savings - buffer);
                debt -= pay;
                savings -= pay;
            }
            hh.Savings[h] = (int)Math.Min(int.MaxValue / 2, savings);
            hh.Debt[h] = (int)Math.Min(int.MaxValue / 2, debt);
            m[row + Ledger.DebtChange] += (int)Math.Max(int.MinValue / 4, Math.Min(int.MaxValue / 4, (debt - debtBefore) * 100));

            CityBooks books = s.Books;
            books.IncomeTax += inc.IncomeTax;
            books.PropertyTax += property;
            books.Benefits += inc.Benefits;
            books.Pensions += inc.Pension;
            books.Wages += inc.Wages;
            books.Rents += rent;
            books.Utilities += budget[(int)Spend.Utilities];
            books.Outside += budget[(int)Spend.Digital];
        }

        /// <summary>The month closes for a household: the running ledger becomes the last, and starts again.</summary>
        public static void Roll(PopulationState s, int h)
        {
            int row = h * Ledger.Lines;
            Array.Copy(s.Households.Month, row, s.Households.Last, row, Ledger.Lines);
            Array.Clear(s.Households.Month, row, Ledger.Lines);
        }
    }
}
