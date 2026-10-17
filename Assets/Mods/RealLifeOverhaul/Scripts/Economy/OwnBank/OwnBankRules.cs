using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The rules of the player's bank, game-free; its day is in OwnBankRules.Day. Every flow keeps <see cref="Balanced"/> true.</summary>
    public static partial class OwnBankRules
    {
        public const int RatioMost = 9999;   // thousandths: a bank with nothing at risk

        /// <summary>An amount at the start's prices at today's.</summary>
        public static long At(long cents, int level)
        {
            return (cents * Level(level) + PriceRules.Start / 2) / PriceRules.Start;
        }

        /// <summary>The banking licence at today's prices and the end game's share (Core/EndScale).</summary>
        public static long Licence(int level, int share = 0) => At(EndScaleRules.Mix(OwnBankCatalogue.LicenceCents, OwnBankCatalogue.LicenceCentsScale, share), level);

        public static long CapitalLeast(int level) => At(LeastAt(0), level);

        public static long CapitalLeast(int level, int closures) => CapitalLeast(level, closures, 0);

        /// <summary>The capital a new bank must pay in after this many closures: the least, and AgainCapital more for each.</summary>
        public static long CapitalLeast(int level, int closures, int share)
        {
            return At(LeastAt(share) * (1000 + Math.Max(0, closures) * OwnBankCatalogue.AgainCapital) / 1000, level);
        }

        /// <summary>The least capital at the start's prices at a share of the scale: what a bank founded now is held to.</summary>
        public static long LeastAt(int share) => EndScaleRules.Mix(OwnBankCatalogue.CapitalLeastCents, OwnBankCatalogue.CapitalLeastCentsScale, share);

        public const string AgainWaitKey = "reallife_ob_why_closed_before", AgainIntegrityKey = "reallife_ob_why_closed_integrity";

        /// <summary>The first day a new licence may be applied for after a closure: a game year later. 0 if the bank was not closed.</summary>
        public static int AgainDay(OwnBank b, int daysPerYear)
        {
            return b.State == OwnBankState.Closed ? b.EndDay + Math.Max(1, daysPerYear) : 0;
        }

        /// <summary>Why the regulator will not license the player again yet (AgainWaitKey or AgainIntegrityKey); null if it will.</summary>
        public static string AgainWhy(OwnBank b, int today, int daysPerYear, bool standing, int integrity)
        {
            if (b.State != OwnBankState.Closed)
            {
                return null;
            }
            if (today < AgainDay(b, daysPerYear))
            {
                return AgainWaitKey;
            }
            return standing && integrity < OwnBankCatalogue.AgainIntegrity ? AgainIntegrityKey : null;
        }

        public static long ToStep(long cents)
        {
            long step = OwnBankCatalogue.CapitalStepCents;
            return cents <= 0 ? 0 : cents / step * step;
        }

        /// <summary>What the rival banks pay savers of a kind: the bank rate less their margin, raised as the bank takes their savers.</summary>
        public static int RivalSaverBp(BankCity c, OwnBank b, int kind)
        {
            int own = Math.Max(OwnBankCatalogue.RivalSaverLeast, c.CityRateBp - OwnBankCatalogue.RivalSaverMargin);
            return own + Answer(b != null && b.Running ? b.Deposits[kind] : 0, SavingsPool(c, kind));
        }

        /// <summary>What the bank pays savers of a kind: the rivals' rate and the player's points over it, never under nothing.</summary>
        public static int SaverBp(BankCity c, OwnBank b, int kind)
        {
            return Math.Max(0, RivalSaverBp(c, b, kind) + b.SaverOffset[kind]);
        }

        /// <summary>What the rival banks charge a kind of borrower, priced as they price the player; the poorly rated, whom none lends to, at the fair price and a premium.</summary>
        public static int RivalLoanBp(BankCity c, OwnBank b, int segment)
        {
            if (segment == OwnBankCatalogue.Poor)
            {
                return RivalLoanBp(c, null, OwnBankCatalogue.Fair) + OwnBankCatalogue.PoorPremium;
            }
            int house = c.HouseRate > 0 ? c.HouseRate : OwnBankCatalogue.HouseRateDefault;
            int rate = LendingTerms.For(OwnBankCatalogue.TypicalScore[segment], 0f, house, c.BasePointsQuarter / 4f).AnnualRate;
            rate = Math.Max(CreditCatalogue.RateFloor, rate - (OwnBankCatalogue.IsBusiness(segment) ? 1 : 0));
            int bp = (int)Math.Round(rate * 100.0 * Math.Max(0, c.Multiplier) / 1000.0);
            int answer = b != null && b.Running ? Answer(b.Loans[segment], DemandPool(c, segment)) : 0;
            return Math.Max(CreditCatalogue.RateFloor * 100, bp - answer);
        }

        /// <summary>What the bank charges a kind of borrower: the rivals' rate and the player's points over it, never under a point.</summary>
        public static int LoanBp(BankCity c, OwnBank b, int segment)
        {
            return Math.Max(CreditCatalogue.RateFloor * 100, RivalLoanBp(c, b, segment) + b.LoanOffset[segment]);
        }

        // The rivals' answer to the share of the city the bank holds: up to AnswerMost.
        private static int Answer(long own, long pool)
        {
            if (own <= 0 || pool <= 0)
            {
                return 0;
            }
            long share = own * 1000 / pool;
            return (int)Math.Min(OwnBankCatalogue.AnswerMost, share * OwnBankCatalogue.AnswerPer / 1000);
        }

        /// <summary>The city's savings of a kind at today's prices.</summary>
        public static long SavingsPool(BankCity c, int kind)
        {
            return At(OwnBankCatalogue.SavingsCents[kind] / 1000 * EndScaleRules.Mix(1000, OwnBankCatalogue.PoolScale, c.Scale), c.Level);
        }

        /// <summary>What a kind of borrower would owe the city's lenders at today's prices in this cycle.</summary>
        public static long DemandPool(BankCity c, int segment)
        {
            return At(OwnBankCatalogue.DemandCents[segment] / 1000 * EndScaleRules.Mix(1000, OwnBankCatalogue.PoolScale, c.Scale), c.Level)
                   * OwnBankCatalogue.DemandCycle[Cycle(c)] / 1000;
        }

        /// <summary>The savings of a kind the bank would hold, as the city's savers would share them out now.</summary>
        public static long SaversWanted(BankCity c, OwnBank b, int kind, int trust)
        {
            if (!b.Running)
            {
                return 0;
            }
            double points = (SaverBp(c, b, kind) - RivalSaverBp(c, b, kind)) / 100.0;
            double w = OwnBankCatalogue.OwnSaverHold[kind] * Clamp(trust, 0, 1000) / 1000.0 * Math.Exp(OwnBankCatalogue.SaverPull[kind] / 1000.0 * points);
            return (long)(SavingsPool(c, kind) * (w / (w + OwnBankCatalogue.HoldRivals)));
        }

        /// <summary>The loans of a kind the bank would have, as the city's borrowers would share them out now; none where it does not lend.</summary>
        public static long LoansWanted(BankCity c, OwnBank b, int segment)
        {
            if (!b.Running || !b.Lending[segment])
            {
                return 0;
            }
            double points = (LoanBp(c, b, segment) - RivalLoanBp(c, b, segment)) / 100.0;
            double w = OwnBankCatalogue.OwnLoanHold[segment] * Math.Exp(-OwnBankCatalogue.LoanPull[segment] / 1000.0 * points);
            return (long)(DemandPool(c, segment) * (w / (w + OwnBankCatalogue.HoldRivals)));
        }

        /// <summary>Defaults a year of a kind of loan, thousandths: the steady figure moved by the city, a dear rate and the month's luck, within bounds.</summary>
        public static int DefaultPerMille(BankCity c, OwnBank b, int segment)
        {
            double f = 1.0;
            bool firm = OwnBankCatalogue.IsBusiness(segment);
            if (firm)
            {
                f *= Math.Max(0, c.Trouble) / 1000.0;
            }
            else
            {
                f *= OwnBankCatalogue.PeopleCycle[Cycle(c)] / 1000.0;
                int over = c.InflationBp - MacroCatalogue.InflationTarget;
                f *= 1.0 + (over >= 0 ? over * OwnBankCatalogue.InflationPerPoint : -over * OwnBankCatalogue.DeflationPerPoint) / 100.0 / 1000.0;
            }
            f *= Policies(c, firm) / 1000.0;
            f *= 1.0 + Math.Max(0, b.LoanOffset[segment]) / 100.0 * OwnBankCatalogue.AdversePerPoint / 1000.0;
            f *= Noise(b.Seed, b.Month, segment) / 1000.0;
            f = Math.Max(OwnBankCatalogue.FactorLeast / 1000.0, Math.Min(OwnBankCatalogue.FactorMost / 1000.0, f));
            return (int)Math.Round(OwnBankCatalogue.DefaultBase[segment] * f);
        }

        /// <summary>What the council's levers do to defaults, thousandths (1000: nothing).</summary>
        public static int Policies(BankCity c, bool firm)
        {
            int sum = 1000;
            if (firm)
            {
                sum += Lever(OwnBankCatalogue.TaxOnFirms, c.Tax) + Lever(OwnBankCatalogue.GrantsOnFirms, c.Grants) + Lever(OwnBankCatalogue.WagesOnFirms, c.Wages)
                       + Lever(OwnBankCatalogue.RentOnFirms, c.Rent);
            }
            else
            {
                sum += Lever(OwnBankCatalogue.WagesOnPeople, c.Wages) + Lever(OwnBankCatalogue.RentOnPeople, c.Rent);
            }
            return Clamp(sum, 600, 1600);
        }

        private static int Lever(int[] effect, int position)
        {
            return position > 0 ? effect[0] : position < 0 ? effect[1] : 0;
        }

        /// <summary>A month's luck for a kind of loan, thousandths: the same for the same bank, month and kind.</summary>
        public static int Noise(int seed, int month, int segment)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)(month * 40503 + segment * 9973 + 17);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return OwnBankCatalogue.MonthNoiseLeast + (int)(h % (uint)(OwnBankCatalogue.MonthNoiseMost - OwnBankCatalogue.MonthNoiseLeast + 1));
            }
        }

        /// <summary>The capital as the regulator counts it: what was paid in and kept, and the fund's value over its cost.</summary>
        public static long CapitalNow(OwnBank b)
        {
            return b.Capital + b.FundValue - b.FundCost;
        }

        public static long RiskWeighted(OwnBank b)
        {
            long sum = 0;
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                sum += b.Loans[j] * OwnBankCatalogue.LoanWeight[j] / 1000 + b.Arrears[j] * OwnBankCatalogue.ArrearsWeight / 1000;
            }
            return sum + b.FundValue * OwnBankCatalogue.FundWeight / 1000;
        }

        /// <summary>The capital ratio, thousandths; RatioMost with nothing at risk.</summary>
        public static int Ratio(OwnBank b)
        {
            long rwa = RiskWeighted(b);
            if (rwa <= 0)
            {
                return RatioMost;
            }
            return (int)Math.Max(-RatioMost, Math.Min(RatioMost, CapitalNow(b) * 1000 / rwa));
        }

        public static long ReserveNeed(OwnBank b)
        {
            return b.Savings * OwnBankCatalogue.ReserveShare / 1000;
        }

        /// <summary>True while reserves, loans, arrears and the fund at cost equal the deposits and the capital.</summary>
        public static bool Balanced(OwnBank b)
        {
            return b.Reserves + b.Lent + b.InArrears + b.FundCost == b.Savings + b.Capital;
        }

        /// <summary>Whether the regulator's watch may be told again: once a month at most; <paramref name="told"/> is int.MinValue while never told.</summary>
        public static bool WatchDue(int day, int told, int monthDays)
        {
            return (long)day - told >= monthDays;   // as a long: an int would run over from int.MinValue
        }

        /// <summary>The city's confidence in the bank, thousandths: the player's integrity, the ratio against the regulator's lines, the scare and the cycle.</summary>
        public static int Confidence(BankCity c, OwnBank b, int ratio)
        {
            long standing = Clamp(OwnBankCatalogue.StandingNeutral + (c.Integrity - 50) * OwnBankCatalogue.StandingPerPoint, 100, 1000);
            long cushion = ratio >= OwnBankCatalogue.WatchRatio ? 1000 : ratio >= OwnBankCatalogue.MinRatio ? OwnBankCatalogue.CushionWatch : OwnBankCatalogue.CushionBreach;
            long scare = 1000 - Clamp(b.Scare, 0, OwnBankCatalogue.ScareMost) / 2;
            long cycle = OwnBankCatalogue.CycleTrust[Cycle(c)];
            return (int)(standing * cushion / 1000 * scare / 1000 * cycle / 1000);
        }

        /// <summary>How far the city's savers trust the bank, thousandths: its confidence, and its name, earned over TrustMonths.</summary>
        public static int Trust(BankCity c, OwnBank b, int day, int confidence)
        {
            int m = Math.Max(1, c.MonthDays);
            long age = 1000L * (day - b.FoundedDay + m) / ((long)OwnBankCatalogue.TrustMonths * m);
            return (int)(Clamp(confidence, 0, 1000) * Math.Max(0L, Math.Min(1000L, age)) / 1000);
        }

        /// <summary>The bank founded with its capital paid in: no savers or loans yet, the rivals' rates, lending to all but the poorly rated.</summary>
        public static void Found(OwnBank b, int day, int seed, long capital, string name)
        {
            b.State = OwnBankState.Running;
            b.Name = name ?? string.Empty;
            b.Seed = seed;
            b.FoundedDay = day;
            b.EndDay = 0;
            b.EndCents = 0;
            b.Why = ClosedWhy.None;
            b.LastDay = day - 1;   // the founding day is its first
            b.Test = false;
            b.Capital = Math.Max(0, capital);
            b.Reserves = b.Capital;
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                b.Deposits[k] = 0;
                b.SaverOffset[k] = 0;
            }
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                b.Loans[j] = b.Arrears[j] = b.Recovering[j] = 0;
                b.Recover[j] = 0;
                b.LoanOffset[j] = 0;
                b.Lending[j] = j != OwnBankCatalogue.Poor;
            }
            b.Strategy = 0;
            b.FundLimit = 0;
            b.Rebalance = false;
            b.Holdings.Clear();
            Array.Clear(b.Crew, 0, b.Crew.Length);
            b.Scare = 0;
            b.Run = false;
            b.RunFrom = 0;
            b.Stage = Regulator.Clear;
            b.Deadline = b.Strikes = 0;
            b.FinedCents = 0;
            b.MonthFrom = day;
            b.Month = 0;
            b.MonthIncome = b.MonthCosts = b.LastIncome = b.LastCosts = 0;
            b.MonthLost = b.LastLost = b.LostAll = b.RecoveredAll = 0;
            Array.Clear(b.History, 0, b.History.Length);
            b.HistoryCount = 0;
        }

        public static void PutIn(OwnBank b, long cents)
        {
            if (b.Running && cents > 0)
            {
                b.Capital += cents;
                b.Reserves += cents;
            }
        }

        /// <summary>
        /// The most a dividend may take now, to the step: the cash over the reserve, the capital over its least and the lending
        /// line. Nothing in a breach, a run, or for a test's bank.
        /// </summary>
        public static long MostOut(OwnBank b, int level)
        {
            if (!b.Running || b.Stage == Regulator.Breach || b.Run || b.Test)
            {
                return 0;
            }
            long cash = b.Reserves - ReserveNeed(b);
            long keep = Math.Max(At(b.FoundedLeast > 0 ? b.FoundedLeast : OwnBankCatalogue.CapitalLeastCents, level), RiskWeighted(b) * OwnBankCatalogue.LendRatio / 1000);
            long capital = CapitalNow(b) - keep;
            return ToStep(Math.Min(cash, capital));
        }

        /// <summary>A dividend out of the capital. False if it is more than <see cref="MostOut"/>.</summary>
        public static bool TakeOut(OwnBank b, long cents, int level)
        {
            if (cents <= 0 || cents > MostOut(b, level))
            {
                return false;
            }
            b.Capital -= cents;
            b.Reserves -= cents;
            return true;
        }

        /// <summary>The profit of the months recorded, the last HistoryMonths at most.</summary>
        public static long YearProfit(OwnBank b)
        {
            long sum = 0;
            for (int i = 0; i < b.HistoryCount && i < b.History.Length; i++)
            {
                sum += b.History[i];
            }
            return sum;
        }

        /// <summary>What a buyer pays: the capital and SaleYears of the last year's profit, if there was one.</summary>
        public static long SaleValue(OwnBank b)
        {
            return Math.Max(0, CapitalNow(b)) + OwnBankCatalogue.SaleYears * Math.Max(0, YearProfit(b));
        }

        /// <summary>Whether a buyer can be found: not in a breach, a run, or for a test's bank.</summary>
        public static bool Sellable(OwnBank b)
        {
            return b.Running && b.Stage != Regulator.Breach && !b.Run && !b.Test;
        }

        public static long Sell(OwnBank b, int day)
        {
            long price = SaleValue(b);
            End(b, OwnBankState.Sold, day, price, ClosedWhy.None);
            return price;
        }

        /// <summary>
        /// The bank closed and wound up: its assets sold for what they fetch, its savers paid first, the regulator's charge on
        /// what is left. Returns the rest, the player's; nothing for a test's bank.
        /// </summary>
        public static long WindUp(OwnBank b, int day, ClosedWhy why)
        {
            long assets = b.Reserves + b.Lent * OwnBankCatalogue.WindPerforming / 1000 + b.InArrears * OwnBankCatalogue.WindArrears / 1000
                          + b.FundValue * OwnBankCatalogue.WindFund / 1000;
            long left = b.Test ? 0 : Math.Max(0, assets - b.Savings) * (1000 - OwnBankCatalogue.WindFee) / 1000;
            End(b, OwnBankState.Closed, day, left, why);
            if (!b.Test)
            {
                b.Closures++;
            }
            return left;
        }

        // Every balance goes with the bank.
        private static void End(OwnBank b, OwnBankState state, int day, long cents, ClosedWhy why)
        {
            b.State = state;
            b.EndDay = day;
            b.EndCents = cents;
            b.Why = why;
            b.Capital = b.Reserves = 0;
            Array.Clear(b.Deposits, 0, b.Deposits.Length);
            Array.Clear(b.Loans, 0, b.Loans.Length);
            Array.Clear(b.Arrears, 0, b.Arrears.Length);
            Array.Clear(b.Recovering, 0, b.Recovering.Length);
            b.Holdings.Clear();
            b.Run = false;
            b.Stage = Regulator.Clear;
            b.Deadline = b.Strikes = 0;
            b.MonthIncome = b.MonthCosts = 0;
        }

        internal static int Cycle(BankCity c)
        {
            return Clamp((int)c.Cycle, 0, MacroCatalogue.StateCount - 1);
        }

        internal static long Level(int level)
        {
            return Math.Max(PriceRules.Least, Math.Min(PriceRules.Most, level));
        }

        internal static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
