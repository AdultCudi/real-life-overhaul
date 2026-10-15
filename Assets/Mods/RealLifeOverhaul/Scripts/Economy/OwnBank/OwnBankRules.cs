using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The rules of the player's bank (OwnBankCatalogue), game-free and checked by EconomyCheck: what it costs to found,
    /// what the rival banks pay and charge, how the city's savers and borrowers choose between them, how loans default,
    /// are put right or written off and partly come back, what the bank earns and spends each day, how far the city
    /// trusts it, when it is run on, what the regulator does about its capital ratio, and what a buyer pays.
    ///
    /// THE BALANCE SHEET. Reserves, loans, arrears and the fund at cost are, always, the deposits and the capital: every
    /// flow of a day moves two of them by the same sum (a saver's money comes into the deposits and the reserves, a loan
    /// is paid out of the reserves into the loans, a loan written off leaves the arrears and the capital). What a written
    /// off loan may still bring back is off the books until it comes. <see cref="Balanced"/> is that identity.
    ///
    /// THE MARKET IS CALIBRATED ON THE CITY AS IT STANDS. Before the bank opens the rival banks hold every saving and make
    /// every loan; the bank's weight grows with the city's trust in it, which a new name earns over TrustMonths, and its
    /// rates against the rivals'. Nothing moves by more than a share of a month's way in a day, so savers and borrowers
    /// are won and lost over months, and a run empties a bank over weeks, never in a day.
    ///
    /// AT THE END GAME'S SCALE (Core/EndScale, the city's Scale) the city's savings and borrowing are at its real size, the
    /// licence and the least capital are a real bank's, and the bank keeps crews by kind (hired towards what it holds; short
    /// of them it takes savings and lends more slowly) and pays its systems and deposit insurance. At a share of 0 every
    /// figure is as before.
    ///
    /// Cost: a day is a few dozen sums over five segments, two kinds of saver and the fund's dozen holdings; nothing is
    /// allocated but in the fund's weekly rebalance (one small list).
    /// </summary>
    public static class OwnBankRules
    {
        public const int RatioMost = 9999;   // a bank with nothing at risk

        private static readonly long[] Want = new long[OwnBankCatalogue.Segments];   // the day's new lending by kind, worked in here (one day at a time)

        // ---------------------------------------------------------------- money at today's prices

        /// <summary>An amount at the start's prices at today's, cents (in longs: a bank's figures run to billions of cents).</summary>
        public static long At(long cents, int level)
        {
            return (cents * Level(level) + PriceRules.Start / 2) / PriceRules.Start;
        }

        /// <summary>The banking licence at today's prices and the end game's share (Core/EndScale), cents.</summary>
        public static long Licence(int level, int share = 0) => At(EndScaleRules.Mix(OwnBankCatalogue.LicenceCents, OwnBankCatalogue.LicenceCentsScale, share), level);

        public static long CapitalLeast(int level) => At(LeastAt(0), level);

        public static long CapitalLeast(int level, int closures) => CapitalLeast(level, closures, 0);

        /// <summary>The capital a new bank must pay in when the regulator has closed this many of the player's banks before: the least, and AgainCapital thousandths more for each.</summary>
        public static long CapitalLeast(int level, int closures, int share)
        {
            return At(LeastAt(share) * (1000 + Math.Max(0, closures) * OwnBankCatalogue.AgainCapital) / 1000, level);
        }

        private static long LeastAt(int share) => EndScaleRules.Mix(OwnBankCatalogue.CapitalLeastCents, OwnBankCatalogue.CapitalLeastCentsScale, share);

        public const string AgainWaitKey = "reallife_ob_why_closed_before", AgainIntegrityKey = "reallife_ob_why_closed_integrity";

        /// <summary>The first day a new licence may be applied for after the regulator closed the bank: a game year (of the save's length) after the closure. 0 if the bank was not closed.</summary>
        public static int AgainDay(OwnBank b, int daysPerYear)
        {
            return b.State == OwnBankState.Closed ? b.EndDay + Math.Max(1, daysPerYear) : 0;
        }

        /// <summary>
        /// Why the regulator will not license the player again yet after closing their bank: the year not yet passed
        /// (AgainWaitKey), or, with Standing on, their integrity as an owner not yet back to AgainIntegrity
        /// (AgainIntegrityKey). Null if a licence may be applied for (or the bank was not closed).
        /// </summary>
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

        /// <summary>What is put in or taken out, to the step below.</summary>
        public static long ToStep(long cents)
        {
            long step = OwnBankCatalogue.CapitalStepCents;
            return cents <= 0 ? 0 : cents / step * step;
        }

        // ---------------------------------------------------------------- rates

        /// <summary>What the rival banks pay savers of a kind, hundredths of a per cent a year: the bank rate less their margin, raised where the bank takes their savers.</summary>
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

        /// <summary>
        /// What the rival banks charge a kind of borrower, hundredths of a per cent a year: their own rate for its typical
        /// rating with the central bank's points (LendingTerms, as they price the player), a business loan a point under
        /// it, the game's interest multiplier on it; lowered where the bank has taken their borrowers. Under the lending
        /// threshold no bank of the city lends: the yardstick is the fair price and a premium (the private lender's is
        /// three per cent a day).
        /// </summary>
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

        // The rivals' answer: up to AnswerMost for the share of the city the bank holds.
        private static int Answer(long own, long pool)
        {
            if (own <= 0 || pool <= 0)
            {
                return 0;
            }
            long share = own * 1000 / pool;
            return (int)Math.Min(OwnBankCatalogue.AnswerMost, share * OwnBankCatalogue.AnswerPer / 1000);
        }

        // ---------------------------------------------------------------- the city's savings and borrowing

        /// <summary>The city's savings of a kind at today's prices, cents.</summary>
        public static long SavingsPool(BankCity c, int kind)
        {
            return At(OwnBankCatalogue.SavingsCents[kind] / 1000 * EndScaleRules.Mix(1000, OwnBankCatalogue.PoolScale, c.Scale), c.Level);
        }

        /// <summary>What a kind of borrower would owe the city's banks at today's prices in this cycle, cents.</summary>
        public static long DemandPool(BankCity c, int segment)
        {
            return At(OwnBankCatalogue.DemandCents[segment] / 1000 * EndScaleRules.Mix(1000, OwnBankCatalogue.PoolScale, c.Scale), c.Level) * OwnBankCatalogue.DemandCycle[Cycle(c)] / 1000;
        }

        /// <summary>The savings of a kind the bank would hold, as the city's savers would share them out now, cents.</summary>
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

        /// <summary>The loans of a kind the bank would have, as the city's borrowers would share them out now, cents; none where it does not lend.</summary>
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

        // ---------------------------------------------------------------- defaults

        /// <summary>
        /// How many of a kind of loan default in a year, thousandths: the steady city's figure, moved by the cycle (people)
        /// or business pressure in this economy (firms), inflation against the target (people), the council's policies,
        /// how much dearer than the rivals the bank lends (the borrowers a dear lender gets are the riskier ones) and the
        /// month's luck. Never under FactorLeast or over FactorMost thousandths of the steady figure.
        /// </summary>
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

        /// <summary>What the council's policies do to defaults, thousandths (1000: nothing).</summary>
        public static int Policies(BankCity c, bool firm)
        {
            int sum = 1000;
            if (firm)
            {
                sum += Lever(OwnBankCatalogue.TaxOnFirms, c.Tax) + Lever(OwnBankCatalogue.GrantsOnFirms, c.Grants) + Lever(OwnBankCatalogue.WagesOnFirms, c.Wages) + Lever(OwnBankCatalogue.RentOnFirms, c.Rent);
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

        /// <summary>A month's luck for a kind of loan, thousandths, the same for the same bank, month and kind.</summary>
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

        // ---------------------------------------------------------------- the regulator's figures

        /// <summary>The capital as the regulator counts it: what was paid in and kept, and what the fund is worth over its cost.</summary>
        public static long CapitalNow(OwnBank b)
        {
            return b.Capital + b.FundValue - b.FundCost;
        }

        /// <summary>The bank's assets weighted by their risk, cents.</summary>
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

        /// <summary>The reserves the bank keeps against its deposits, cents.</summary>
        public static long ReserveNeed(OwnBank b)
        {
            return b.Savings * OwnBankCatalogue.ReserveShare / 1000;
        }

        /// <summary>True while reserves, loans, arrears and the fund at cost are the deposits and the capital.</summary>
        public static bool Balanced(OwnBank b)
        {
            return b.Reserves + b.Lent + b.InArrears + b.FundCost == b.Savings + b.Capital;
        }

        // ---------------------------------------------------------------- trust

        /// <summary>
        /// The city's confidence in the bank, thousandths: the player's integrity (Standing), the capital ratio against the
        /// regulator's lines, the scare of what the City Ledger has printed about it, and the cycle. A run begins under
        /// RunStart.
        /// </summary>
        public static int Confidence(BankCity c, OwnBank b, int ratio)
        {
            long standing = Clamp(OwnBankCatalogue.StandingNeutral + (c.Integrity - 50) * OwnBankCatalogue.StandingPerPoint, 100, 1000);
            long cushion = ratio >= OwnBankCatalogue.WatchRatio ? 1000 : ratio >= OwnBankCatalogue.MinRatio ? OwnBankCatalogue.CushionWatch : OwnBankCatalogue.CushionBreach;
            long scare = 1000 - Clamp(b.Scare, 0, OwnBankCatalogue.ScareMost) / 2;
            long cycle = OwnBankCatalogue.CycleTrust[Cycle(c)];
            return (int)(standing * cushion / 1000 * scare / 1000 * cycle / 1000);
        }

        /// <summary>How far the city's savers trust the bank, thousandths: its confidence, and its name, which a new bank earns over TrustMonths.</summary>
        public static int Trust(BankCity c, OwnBank b, int day, int confidence)
        {
            int m = Math.Max(1, c.MonthDays);
            long age = 1000L * (day - b.FoundedDay + m) / ((long)OwnBankCatalogue.TrustMonths * m);
            return (int)(Clamp(confidence, 0, 1000) * Math.Max(0L, Math.Min(1000L, age)) / 1000);
        }

        // ---------------------------------------------------------------- founding, capital, selling

        /// <summary>The bank founded on a day with its capital paid in: every saving and loan still the rivals', its rates the rivals', lending to all but the poor.</summary>
        public static void Found(OwnBank b, int day, int seed, long capital, string name)
        {
            b.State = OwnBankState.Running;
            b.Name = name ?? string.Empty;
            b.Seed = seed;
            b.FoundedDay = day;
            b.EndDay = 0;
            b.EndCents = 0;
            b.Why = ClosedWhy.None;
            b.LastDay = day - 1;   // (the day it was founded on is its first)
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

        /// <summary>Capital put in: into the reserves.</summary>
        public static void PutIn(OwnBank b, long cents)
        {
            if (b.Running && cents > 0)
            {
                b.Capital += cents;
                b.Reserves += cents;
            }
        }

        /// <summary>
        /// The most capital that can be taken out as a dividend now, to the step: what leaves the reserves over what the
        /// deposits need, the capital over the least the regulator wants and over what keeps the ratio at the lending
        /// line. Nothing while the regulator is after the bank or its savers are running.
        /// </summary>
        public static long MostOut(OwnBank b, int level, int share = 0)
        {
            if (!b.Running || b.Stage == Regulator.Breach || b.Run || b.Test)
            {
                return 0;
            }
            long cash = b.Reserves - ReserveNeed(b);
            long keep = Math.Max(CapitalLeast(level, 0, share), RiskWeighted(b) * OwnBankCatalogue.LendRatio / 1000);
            long capital = CapitalNow(b) - keep;
            return ToStep(Math.Min(cash, capital));
        }

        /// <summary>A dividend taken out of the capital. False if it is more than <see cref="MostOut"/>.</summary>
        public static bool TakeOut(OwnBank b, long cents, int level, int share = 0)
        {
            if (cents <= 0 || cents > MostOut(b, level, share))
            {
                return false;
            }
            b.Capital -= cents;
            b.Reserves -= cents;
            return true;
        }

        /// <summary>The last year's profit (the months recorded, at most HistoryMonths), cents.</summary>
        public static long YearProfit(OwnBank b)
        {
            long sum = 0;
            for (int i = 0; i < b.HistoryCount && i < b.History.Length; i++)
            {
                sum += b.History[i];
            }
            return sum;
        }

        /// <summary>What a buyer pays: the capital and SaleYears of the last year's profit, if there was one; cents.</summary>
        public static long SaleValue(OwnBank b)
        {
            return Math.Max(0, CapitalNow(b)) + OwnBankCatalogue.SaleYears * Math.Max(0, YearProfit(b));
        }

        /// <summary>Whether a buyer can be found: not for a bank the regulator is after, nor one its savers are running from, nor one a test founded.</summary>
        public static bool Sellable(OwnBank b)
        {
            return b.Running && b.Stage != Regulator.Breach && !b.Run && !b.Test;
        }

        /// <summary>The bank sold for what a buyer pays; its books are the buyer's from now. Returns the price.</summary>
        public static long Sell(OwnBank b, int day)
        {
            long price = SaleValue(b);
            End(b, OwnBankState.Sold, day, price, ClosedWhy.None);
            return price;
        }

        /// <summary>
        /// The bank closed by the regulator and wound up: its loans and fund sold for what they fetch, its savers paid
        /// first (any shortfall is the city's deposit guarantee's), the regulator's charge on what is left, and the rest
        /// the player's. Returns what is left for the player.
        /// </summary>
        public static long WindUp(OwnBank b, int day, ClosedWhy why)
        {
            long assets = b.Reserves + b.Lent * OwnBankCatalogue.WindPerforming / 1000 + b.InArrears * OwnBankCatalogue.WindArrears / 1000 + b.FundValue * OwnBankCatalogue.WindFund / 1000;
            long left = b.Test ? 0 : Math.Max(0, assets - b.Savings) * (1000 - OwnBankCatalogue.WindFee) / 1000;   // (a bank a test founded pays its owner nothing)
            End(b, OwnBankState.Closed, day, left, why);
            if (!b.Test)
            {
                b.Closures++;
            }
            return left;
        }

        // The bank is no longer the player's: every balance goes with it.
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

        // ---------------------------------------------------------------- the day

        /// <summary>
        /// One day of the bank, in this order: the city's confidence (a run begins or ends); the savers' interest, and
        /// their savings move towards what they would hold here (or leave, in a run); the loans' interest, defaults, the
        /// arrears put right or written off, what was written off coming back, repayments, and new loans as far as the
        /// reserves and the capital allow; the reserves' interest; the fund (OwnBankFund.Day, with the market's quotes:
        /// none while the market is not running); the staff; the day's profit into the capital; the savers paid out of the
        /// fund when the reserves are not enough; then the regulator. A bank that is closed is wound up at once
        /// (<see cref="WindUp"/>: into.WoundUp). Nothing for a bank that is not running.
        /// </summary>
        public static void Day(OwnBank b, BankCity c, int day, IList<FundQuote> quotes, BankDay into)
        {
            into.Clear();
            if (!b.Running)
            {
                return;
            }
            int y = Math.Max(1, c.YearDays), m = Math.Max(1, c.MonthDays);
            long level = Level(c.Level);

            // The city's confidence, from the books as yesterday left them.
            int confidence = Confidence(c, b, Ratio(b));
            if (!b.Run && confidence < OwnBankCatalogue.RunStart)
            {
                b.Run = true;
                b.RunFrom = day;
                b.Scare = Math.Min(OwnBankCatalogue.ScareMost, b.Scare + OwnBankCatalogue.ScareRun);
                into.RunStarted = true;
            }
            else if (b.Run && confidence >= OwnBankCatalogue.RunEnd && b.Stage != Regulator.Breach)
            {
                b.Run = false;
                into.RunEnded = true;
            }
            int trust = Trust(c, b, day, confidence);

            // The crews at the end game's scale, hired towards what the bank holds today: short of them it takes savings and lends more slowly.
            int working = EndScaleRules.Working(Hire(b, c.Scale));

            // The savers.
            for (int k = 0; k < OwnBankCatalogue.Savers; k++)
            {
                long interest = b.Deposits[k] * SaverBp(c, b, k) / (10000L * y);
                b.Deposits[k] += interest;
                into.Savers += interest;
                long move;
                if (b.Run)
                {
                    move = -(b.Deposits[k] * OwnBankCatalogue.RunShare[k] / 1000);
                }
                else
                {
                    long gap = SaversWanted(c, b, k, trust) - b.Deposits[k];
                    move = gap * OwnBankCatalogue.SaverFollow[k] / (1000L * m) * working / 1000;
                }
                move = Math.Max(-b.Deposits[k], move);
                b.Deposits[k] += move;
                b.Reserves += move;
                if (move < 0)
                {
                    into.Withdrawn -= move;
                }
                else
                {
                    into.Deposited += move;
                }
            }

            // The loans: interest, defaults, the arrears, recoveries, repayments.
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                long interest = b.Loans[j] * LoanBp(c, b, j) / (10000L * y);
                b.Reserves += interest;
                into.Interest += interest;
                long defaulted = (long)Math.Round(b.Loans[j] * (double)DefaultPerMille(c, b, j) / (1000.0 * y));
                defaulted = Math.Min(b.Loans[j], defaulted);
                b.Loans[j] -= defaulted;
                b.Arrears[j] += defaulted;
                into.Defaulted += defaulted;
                long settled = b.Arrears[j] < 100 * m ? b.Arrears[j] : b.Arrears[j] / m;   // (a few dollars left in arrears are settled at once)
                long cured = settled * OwnBankCatalogue.CureShare[j] / 1000, written = settled - cured;
                b.Arrears[j] -= settled;
                b.Loans[j] += cured;
                into.Cured += cured;
                into.WrittenOff += written;
                into.Lost += written;
                if (written > 0)
                {
                    int share = OwnBankCatalogue.RecoverShare[j] + (c.Courts ? OwnBankCatalogue.CourtExtra : 0);
                    b.Recovering[j] += written * share / 1000;
                    b.Recover[j] = (OwnBankCatalogue.RecoverMonths + (c.Courts ? OwnBankCatalogue.CourtMonths : 0)) * m;
                    long court = c.Courts ? written * OwnBankCatalogue.CourtCost / 1000 : 0;
                    b.Reserves -= court;
                    into.Court += court;
                }
                if (b.Recovering[j] > 0)
                {
                    long back = b.Recovering[j] < 100 * m ? b.Recovering[j] : b.Recovering[j] / Math.Max(1, b.Recover[j]);
                    b.Recovering[j] -= back;
                    b.Reserves += back;
                    into.Recovered += back;
                }
                long repaid = b.Loans[j] / ((long)OwnBankCatalogue.TermYears[j] * y);
                b.Loans[j] -= repaid;
                b.Reserves += repaid;
                into.Repaid += repaid;
            }
            Lend(b, c, m, into, working);

            // The reserves at the central bank.
            if (b.Reserves > 0)
            {
                long reserve = b.Reserves * Math.Max(0, c.CityRateBp - OwnBankCatalogue.ReserveSpread) / (10000L * y);
                b.Reserves += reserve;
                into.Reserve += reserve;
            }

            OwnBankFund.Day(b, c, day, quotes, into);

            // The staff: before the scale a hall and a share of what it holds; at it, its crews' wages, and its systems and
            // deposit insurance. What the scale adds never takes the capital below nothing in one day (Core/EndScale).
            int s = c.Scale;
            long before = OwnBankCatalogue.StaffDayCents * level / PriceRules.Start
                          + (b.Savings * OwnBankCatalogue.StaffOnSavings + (b.Lent + b.InArrears) * OwnBankCatalogue.StaffOnLoans) / (1000L * y);
            long staff = s <= 0 ? before : before * (EndScaleRules.Full - s) / EndScaleRules.Full + Crews(b) * level / PriceRules.Start;
            long systems = s <= 0 ? 0 : EndScaleRules.Mix(0L, OwnBankCatalogue.SystemsDayCents * level / PriceRules.Start + b.Savings * OwnBankCatalogue.InsuranceBp / (10000L * y), s);
            long room = b.Capital + into.Interest + into.Reserve + into.Recovered - into.Savers - into.Lost - into.Court - Math.Min(before, staff);
            staff = Math.Min(before, staff) + EndScaleRules.Take(ref room, Math.Max(0, staff - before));
            systems = EndScaleRules.Take(ref room, systems);
            b.Reserves -= staff + systems;
            into.Staff += staff;
            into.Systems += systems;

            // The day's profit is kept in the capital (what the fund made, lost and paid is in it already: OwnBankFund).
            b.Capital += into.Interest + into.Reserve + into.Recovered - into.Savers - into.Lost - into.Court - into.Staff - into.Systems;

            // Savers who cannot be paid out of the reserves are paid out of the fund; a bank that still cannot pay them fails.
            if (b.Reserves < 0)
            {
                OwnBankFund.FireSale(b, -b.Reserves, into);
            }
            if (b.Reserves < 0)
            {
                Close(b, day, ClosedWhy.Run, into);
                return;
            }

            Regulate(b, c, day, m, into);
            if (!b.Running)
            {
                return;
            }
            if (b.Scare > 0)
            {
                b.Scare = Math.Max(0, b.Scare - Math.Max(1, b.Scare * OwnBankCatalogue.ScareFade / (1000 * m)));
            }
            b.MonthIncome += into.Income;
            b.MonthCosts += into.Costs;
            b.MonthLost += into.WrittenOff;
            b.LostAll += into.WrittenOff;
            b.RecoveredAll += into.Recovered;
            b.LastDay = day;
            if (day + 1 - b.MonthFrom >= m)
            {
                CloseMonth(b, day + 1);
            }
        }

        // New loans: each kind a month's LendFollow of the way to what its borrowers would have, as far as the reserves over
        // what the deposits need, and the capital over the lending line, allow; none in a run or a breach.
        private static void Lend(OwnBank b, BankCity c, int m, BankDay into, int working)
        {
            if (b.Run || b.Stage == Regulator.Breach)
            {
                return;
            }
            long need = 0, weighted = 0;
            long[] want = Want;
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                long gap = LoansWanted(c, b, j) - b.Loans[j];
                want[j] = gap > 0 ? gap * OwnBankCatalogue.LendFollow / (1000L * m) * working / 1000 : 0;
                need += want[j];
                weighted += want[j] * OwnBankCatalogue.LoanWeight[j] / 1000;
            }
            if (need <= 0)
            {
                return;
            }
            long cash = b.Reserves - ReserveNeed(b);
            long room = CapitalNow(b) * 1000 / OwnBankCatalogue.LendRatio - RiskWeighted(b);
            if (cash <= 0 || room <= 0)
            {
                return;
            }
            double scale = Math.Min(1.0, Math.Min(cash / (double)need, weighted > 0 ? room / (double)weighted : 1.0));
            for (int j = 0; j < OwnBankCatalogue.Segments; j++)
            {
                long lent = (long)(want[j] * scale);
                b.Loans[j] += lent;
                b.Reserves -= lent;
                into.Lent += lent;
            }
        }

        // The regulator's day: the bank closed if its capital is gone or its ratio is under half the law's; a breach given a
        // deadline, fined for each deadline missed and closed at the second; a breach put right; the watch line.
        private static void Regulate(OwnBank b, BankCity c, int day, int m, BankDay into)
        {
            long capital = CapitalNow(b);
            int ratio = Ratio(b);
            if (capital <= 0)
            {
                Close(b, day, ClosedWhy.Insolvent, into);
                return;
            }
            if (ratio < OwnBankCatalogue.MinRatio / 2)
            {
                Close(b, day, ClosedWhy.Collapse, into);
                return;
            }
            if (ratio < OwnBankCatalogue.MinRatio)
            {
                if (b.Stage != Regulator.Breach)
                {
                    b.Stage = Regulator.Breach;
                    b.Deadline = day + OwnBankCatalogue.CureMonths * m;
                    b.Strikes = 0;
                    b.Scare = Math.Min(OwnBankCatalogue.ScareMost, b.Scare + OwnBankCatalogue.ScareBreach);
                    into.Breached = true;
                }
                else if (day >= b.Deadline)
                {
                    b.Strikes++;
                    if (b.Strikes >= OwnBankCatalogue.StrikesClose)
                    {
                        Close(b, day, ClosedWhy.Strikes, into);
                        return;
                    }
                    long fine = Fine(b, c.Level);
                    b.Capital -= fine;
                    b.Reserves -= fine;
                    b.FinedCents += fine;
                    into.Fine += fine;
                    into.Fined = true;
                    b.Deadline = day + m;
                    b.Scare = Math.Min(OwnBankCatalogue.ScareMost, b.Scare + OwnBankCatalogue.ScareFine);
                }
                return;
            }
            Regulator was = b.Stage;
            b.Stage = ratio < OwnBankCatalogue.WatchRatio ? Regulator.Watch : Regulator.Clear;
            b.Strikes = 0;
            into.Cured2 = was == Regulator.Breach;
            into.Watched = was == Regulator.Clear && b.Stage == Regulator.Watch;
        }

        // ---------------------------------------------------------------- the crews at scale

        /// <summary>The people of a crew the bank needs at the full scale, by what it holds today.</summary>
        public static int CrewAtScale(OwnBank b, int kind)
        {
            if (!b.Running)
            {
                return 0;
            }
            switch (kind)
            {
                case OwnBankCatalogue.Tellers: return EndScaleRules.Per(b.Deposits[OwnBankCatalogue.People], OwnBankCatalogue.TellerCents);
                case OwnBankCatalogue.Managers: return EndScaleRules.Per(b.Deposits[OwnBankCatalogue.Firms], OwnBankCatalogue.ManagerCents);
                case OwnBankCatalogue.Officers: return EndScaleRules.Per(b.Lent + b.InArrears, OwnBankCatalogue.OfficerCents);
                default: return OwnBankCatalogue.ComplianceLeast + (int)Math.Min(int.MaxValue / 4, Math.Max(0L, b.Savings + b.Capital) / OwnBankCatalogue.ComplianceCents);
            }
        }

        public static int CrewNeed(OwnBank b, int kind, int share) => EndScaleRules.Need(CrewAtScale(b, kind), share);

        /// <summary>A day's hiring: each crew a share of its need nearer it (or down to it). Returns how far they cover it, thousandths: the shortest of them.</summary>
        public static int Hire(OwnBank b, int share)
        {
            int cover = EndScaleRules.Full;
            for (int k = 0; k < OwnBankCatalogue.CrewKinds; k++)
            {
                int need = CrewNeed(b, k, share);
                b.Crew[k] = EndScaleRules.Hire(b.Crew[k], need, OwnBankCatalogue.CrewPace);
                cover = Math.Min(cover, EndScaleRules.Cover(b.Crew[k], need));
            }
            return cover;
        }

        /// <summary>The crews' wages a day, cents at the start's prices.</summary>
        public static long Crews(OwnBank b)
        {
            long sum = 0;
            for (int k = 0; k < OwnBankCatalogue.CrewKinds; k++)
            {
                sum += (long)Math.Max(0, b.Crew[k]) * OwnBankCatalogue.CrewWage[k];
            }
            return sum;
        }

        /// <summary>The regulator's fine for a deadline missed: a share of the capital the bank is short of, never under the least; cents.</summary>
        public static long Fine(OwnBank b, int level)
        {
            long short_ = RiskWeighted(b) * OwnBankCatalogue.MinRatio / 1000 - CapitalNow(b);
            return Math.Max(At(OwnBankCatalogue.FineLeastCents, level), Math.Max(0, short_) * OwnBankCatalogue.FineShare / 1000);
        }

        private static void Close(OwnBank b, int day, ClosedWhy why, BankDay into)
        {
            into.Closed = true;
            into.Why = why;
            into.WoundUp = WindUp(b, day, why);
        }

        /// <summary>A month's books closed on the first day of the next: its profit recorded (the last HistoryMonths kept), the figures kept for the page, and a new month begun with its own luck.</summary>
        public static void CloseMonth(OwnBank b, int nextDay)
        {
            long profit = b.MonthIncome - b.MonthCosts;
            if (b.HistoryCount < b.History.Length)
            {
                b.History[b.HistoryCount++] = profit;
            }
            else
            {
                Array.Copy(b.History, 1, b.History, 0, b.History.Length - 1);
                b.History[b.History.Length - 1] = profit;
            }
            b.LastIncome = b.MonthIncome;
            b.LastCosts = b.MonthCosts;
            b.LastLost = b.MonthLost;
            b.MonthIncome = b.MonthCosts = b.MonthLost = 0;
            b.MonthFrom = nextDay;
            b.Month++;
        }

        /// <summary>
        /// The bank's clock set back by some days (a test ran them at once): each day it counts from or waits for is that
        /// much earlier, so that its next day is the game's next day and what had waited (a deadline, a month) has moved on.
        /// </summary>
        public static void Rewind(OwnBank b, int days)
        {
            if (days <= 0)
            {
                return;
            }
            b.FoundedDay -= days;
            b.MonthFrom -= days;
            b.LastDay -= days;
            b.Deadline = b.Deadline > 0 ? b.Deadline - days : 0;
            b.RunFrom = b.RunFrom > 0 ? b.RunFrom - days : 0;
        }

        // ---------------------------------------------------------------- small helpers

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
