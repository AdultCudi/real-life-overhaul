using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The words the reader needs of the game: a key's text, a sentence with its names filled in, a day and money in cents.</summary>
    public interface IOwnBankSay
    {
        string T(string key);

        string F(string key, params string[] pairs);

        string Day(int day);

        string Cents(long cents);
    }

    /// <summary>What only the game knows about the bank's world: the day, the price level, the headquarters, the series of the fund, each branch's counter.</summary>
    public sealed class OwnBankWorld
    {
        public bool Ready;
        public int Today, Level, Buying;
        public bool HasHq;
        public string HqName = string.Empty;
        public int Integrity;
        public bool StandingOn;
        public string AgainWhy;         // the key OwnBankRules.AgainWhy names, or null
        public long MoveFee;
        public string MoveWhy;          // the key of why the bank cannot move up now, or null
        public float[] FundSeries = new float[0];
        public int[] FundDays = new int[0];
        public bool BranchOn = true;
        public long[] BranchCostDay = new long[0];
        public bool[] BranchOpen = new bool[0];
        public int[] BranchTellers = new int[0], BranchStaff = new int[0];
    }

    /// <summary>
    /// Reads the Your bank screen's facts from the bank the player owns, as the old app read them for its page: the same rules, the same
    /// figures. Game-free, so the offline checks run it over a bank a seeded city has run for days, and the live screen runs it over the
    /// real one. Cost: a pass over the bank's few rows, when the screen is filled.
    /// </summary>
    public static class OwnBankReader
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Read(OwnBankFacts f, OwnBank b, BankCity c, OwnBankWorld world, IOwnBankSay say)
        {
            f.Ready = world.Ready && b != null;
            if (!f.Ready)
            {
                return;
            }
            f.State = (int)b.State;
            f.Day = world.Today;
            f.Name = b.Name;
            f.Test = b.Test;
            f.Level = say.T(BankTiers.NameKeys[OwnBankRules.Clamp(OwnBankRules.TierOf(b), BankTiers.Community, BankTiers.National)]);
            f.HasHq = world.HasHq;
            f.HqName = world.HqName;
            f.LawRatio = OwnBankCatalogue.MinRatio;
            f.WatchRatio = OwnBankCatalogue.WatchRatio;
            f.Integrity = world.Integrity;
            f.StandingOn = world.StandingOn;
            f.Closures = b.Closures;
            f.Licence = OwnBankRules.TierLicence(BankTiers.Community, world.Level, world.Buying);
            f.FoundCapital = OwnBankRules.TierCapital(BankTiers.Community, world.Level, 0, world.Buying);
            f.NextCapital = OwnBankRules.TierCapital(BankTiers.Community, world.Level, b.Closures, world.Buying);
            f.RivalSaverBp = OwnBankRules.RivalSaverBp(c, b, 0);
            f.RivalLoanBp = OwnBankRules.RivalLoanBp(c, b, OwnBankCatalogue.Good);
            BankCaps community = OwnBankRules.CapsOf(BankTiers.Community, world.Level, c.Scale);
            f.CommBranches = community.Branches;
            f.CommLoans = community.Loans;
            f.CommDeposits = community.Deposits;
            f.CommunityHolds = say.F("reallife_ob_note_level", "branches", community.Branches.ToString("N0", Inv), "loans", say.Cents(community.Loans), "deposits", say.Cents(community.Deposits));
            if (b.State != OwnBankState.Running)
            {
                NotRunning(f, b, world, say);
                return;
            }
            Running(f, b, c, world, say);
        }

        private static void NotRunning(OwnBankFacts f, OwnBank b, OwnBankWorld world, IOwnBankSay say)
        {
            f.EndDay = b.EndDay;
            f.EndCents = b.EndCents;
            f.Why = b.State == OwnBankState.Closed ? say.T(WhyKeyOf(b.Why)) : null;
            f.AgainWait = null;
            if (b.State == OwnBankState.Closed && world.AgainWhy != null)
            {
                int again = OwnBankRules.AgainDay(b, GameYear);
                f.AgainDay = again;
                f.AgainIntegrity = OwnBankCatalogue.AgainIntegrity;
                f.AgainWait = say.F(world.AgainWhy == OwnBankRules.AgainWaitKey ? "reallife_ob_box_closed_after" : "reallife_ob_box_closed_integrity", "day", say.Day(again),
                    "least", OwnBankCatalogue.AgainIntegrity.ToString(Inv), "now", world.Integrity.ToString(Inv));
            }
        }

        /// <summary>The year the days of a closed bank's wait are counted in; set by the game (GameCalendar.DaysPerYear), 60 offline.</summary>
        public static int GameYear = 60;

        private static string WhyKeyOf(ClosedWhy why)
        {
            switch (why)
            {
                case ClosedWhy.Strikes: return "reallife_ob_why_strikes";
                case ClosedWhy.Insolvent: return "reallife_ob_why_insolvent";
                case ClosedWhy.Collapse: return "reallife_ob_why_collapse";
                default: return "reallife_ob_why_run";
            }
        }

        private static void Running(OwnBankFacts f, OwnBank b, BankCity c, OwnBankWorld world, IOwnBankSay say)
        {
            int today = world.Today;
            int tier = OwnBankRules.TierOf(b);
            f.Tier = tier;
            f.Top = tier >= BankTiers.National;
            f.Stage = (int)b.Stage;
            string stageKey = b.Stage == Regulator.Breach ? (b.Strikes > 0 ? "reallife_ob_stage_fined" : "reallife_ob_stage_breach")
                : b.Stage == Regulator.Watch ? "reallife_ob_stage_watch" : "reallife_ob_stage_clear";
            f.StageWords = say.F(stageKey,
                "day", say.Day(b.Deadline));
            f.Run = b.Run;
            f.Reserves = b.Reserves;
            f.Lent = b.Lent;
            f.Arrears = b.InArrears;
            f.Fund = b.FundValue;
            f.FundCost = b.FundCost;
            f.Deposits = b.Savings;
            f.Capital = OwnBankRules.CapitalNow(b);
            f.Risk = OwnBankRules.RiskWeighted(b);
            f.ReserveNeed = OwnBankRules.ReserveNeed(b);
            f.LostAll = b.LostAll;
            f.RecoveredAll = b.RecoveredAll;
            f.CitySavings = OwnBankRules.SavingsPool(c, 0) + OwnBankRules.SavingsPool(c, 1);
            f.Ratio = OwnBankRules.Ratio(b);
            f.Confidence = OwnBankRules.Confidence(c, b, f.Ratio);
            f.Trust = OwnBankRules.Trust(c, b, today, f.Confidence);
            f.MonthIncome = b.MonthIncome;
            f.MonthCosts = b.MonthCosts;
            f.MonthLost = b.MonthLost;
            f.LastIncome = b.LastIncome;
            f.LastCosts = b.LastCosts;
            f.HasLast = b.HistoryCount > 0;
            f.HasHistory = b.HistoryCount > 0;
            f.YearProfit = OwnBankRules.YearProfit(b);
            f.SaleValue = OwnBankRules.SaleValue(b);
            f.MostOut = OwnBankRules.MostOut(b, world.Level);
            f.Sellable = OwnBankRules.Sellable(b);
            f.DaysRun = OwnBankRules.DaysRun(b);
            f.HasDaily = f.DaysRun > 0 || f.HasLast;
            f.PerDayNow = OwnBankRules.ProfitPerDayNow(b);
            f.PerDayLast = OwnBankRules.ProfitPerDayLast(b, c.MonthDays);
            f.Crews = null;
            if (c.Scale > 0)
            {
                f.Crews = say.F("reallife_endscale_ob_crews_value", "tellers", b.Crew[OwnBankCatalogue.Tellers].ToString(Inv), "tellers_need", OwnBankRules.CrewNeed(b,
                    OwnBankCatalogue.Tellers, c.Scale).ToString(Inv),
                    "managers", b.Crew[OwnBankCatalogue.Managers].ToString(Inv), "managers_need", OwnBankRules.CrewNeed(b, OwnBankCatalogue.Managers, c.Scale).ToString(Inv),
                    "officers", b.Crew[OwnBankCatalogue.Officers].ToString(Inv), "officers_need", OwnBankRules.CrewNeed(b, OwnBankCatalogue.Officers, c.Scale).ToString(Inv),
                    "compliance", b.Crew[OwnBankCatalogue.Compliance].ToString(Inv), "compliance_need", OwnBankRules.CrewNeed(b, OwnBankCatalogue.Compliance, c.Scale).ToString(Inv),
                    "wages", say.Cents(OwnBankRules.Crews(b) * OwnBankCatalogue.CostYearDays / Math.Max(1, c.YearDays) * world.Level / PriceRules.Start));
            }
            // Level
            BankCaps caps = OwnBankRules.Caps(b, c.Level, c.Scale);
            f.DepositsMost = caps.Deposits == BankTiers.NoLimit ? -1 : caps.Deposits;
            f.LoansMost = caps.Loans == BankTiers.NoLimit ? -1 : caps.Loans;
            f.BranchesNow = b.Branches.Count;
            f.BranchesMost = caps.Branches;
            long book = b.Lent + b.InArrears;
            f.Over = (caps.Deposits != BankTiers.NoLimit && b.Savings > caps.Deposits) || (caps.Loans != BankTiers.NoLimit && book > caps.Loans) || b.Branches.Count > caps.Branches;
            f.NextLevel = null;
            f.RegulatorOk = b.Stage == Regulator.Clear && !b.Run;
            if (!f.Top)
            {
                int next = OwnBankRules.NextTier(b);
                f.NextLevel = say.T(BankTiers.NameKeys[next]);
                f.NextTop = next >= BankTiers.National;
                f.RecordMonths = BankTiers.RecordMonths[next];
                f.HeldMonths = OwnBankRules.MonthsAtTier(b, today, c.MonthDays);
                f.CapitalLeast = OwnBankRules.TierCapital(next, world.Level, b.Closures, world.Buying);
                f.MoveFee = world.MoveFee;
                f.MoveWhy = world.MoveWhy != null ? say.T(world.MoveWhy) : null;
                BankCaps after = OwnBankRules.CapsOf(next, c.Level, c.Scale);
                f.AfterBranches = after.Branches;
                f.AfterLoans = after.Loans;
                f.AfterDeposits = after.Deposits;
            }
            // Pay and dividends
            f.PayMode = b.PayMode;
            f.PayShare = b.PayShare;
            f.PayFixed = b.PayFixed;
            f.PayWanted = b.PayWanted;
            f.PayGiven = b.PayGiven;
            f.PaidMonth = b.PaidMonth;
            f.PaidLast = b.PaidLast;
            f.PaidAll = b.PaidAll;
            f.PayHeld = null;
            if (b.PayWanted > b.PayGiven)
            {
                string amount = say.Cents(b.PayWanted - b.PayGiven);
                switch (b.PayHeld)
                {
                    case BankTiers.PayHeldBreach: f.PayHeld = say.F("reallife_ob_pay_held_breach", "amount", amount); break;
                    case BankTiers.PayHeldRun: f.PayHeld = say.F("reallife_ob_pay_held_run", "amount", amount); break;
                    case BankTiers.PayHeldReserves: f.PayHeld = say.F("reallife_ob_pay_held_reserves", "amount", amount, "reserve", Ratio(OwnBankCatalogue.ReserveShare)); break;
                    default: f.PayHeld = say.F("reallife_ob_pay_held_capital", "amount", amount); break;
                }
            }
            f.DivEvery = b.DivEvery;
            f.DivEveryNames = new string[BankDividends.Count];
            for (int i = 0; i < BankDividends.Count; i++)
            {
                f.DivEveryNames[i] = say.F(BankDividends.EveryKeys[i], "days", BankDividends.WeekDays.ToString(Inv));
            }
            f.DivShare = b.DivShare;
            f.DivNextDay = b.DivEvery != BankDividends.Off ? OwnBankRules.DividendDay(b, c.MonthDays, c.YearDays) : 0;
            f.DivKept = b.DivKept;
            f.DivDay = b.DivDay;
            f.DivPaid = b.DivPaid;
            f.DivWanted = b.DivWanted;
            f.DivAll = b.DivAll;
            f.DivShort = b.DivDay > 0 && (b.DivPaid < b.DivWanted || b.DivWhy != BankDividends.Paid);
            int why = Math.Max(BankDividends.Paid, Math.Min(BankDividends.WhyMost, b.DivWhy));
            string whyText = why == BankDividends.Paid ? string.Empty : say.T(BankDividends.WhyKeys[why]);
            f.DivWhy = !f.DivShort ? null : b.DivPaid > 0
                ? say.F("reallife_ob_div_short_line", "amount", say.Cents(b.DivPaid), "wanted", say.Cents(b.DivWanted), "why", whyText) : say.F("reallife_ob_div_none_line", "why", whyText);
            // Savers and borrowers
            f.Savers = new OwnSaverFact[OwnBankCatalogue.Savers];
            for (int k = 0; k < f.Savers.Length; k++)
            {
                f.Savers[k] = new OwnSaverFact
                {
                    Name = say.T(OwnBankCatalogue.SaverKeys[k]), Deposits = b.Deposits[k], Pool = OwnBankRules.SavingsPool(c, k), OwnBp = OwnBankRules.SaverBp(c, b, k),
                        RivalBp = OwnBankRules.RivalSaverBp(c, b, k),
                    Offset = b.SaverOffset[k], Wanted = OwnBankRules.SaversWanted(c, b, k, f.Trust)
                };
            }
            f.Segments = new OwnSegmentFact[OwnBankCatalogue.Segments];
            for (int j = 0; j < f.Segments.Length; j++)
            {
                f.Segments[j] = new OwnSegmentFact
                {
                    Name = say.T(OwnBankCatalogue.SegmentKeys[j]), Lent = b.Loans[j], Arrears = b.Arrears[j], Recovering = b.Recovering[j], OwnBp = OwnBankRules.LoanBp(c, b, j),
                        RivalBp = OwnBankRules.RivalLoanBp(c, b, j),
                    Offset = b.LoanOffset[j], DefaultPerMille = OwnBankRules.DefaultPerMille(c, b, j), DefaultUsual = OwnBankCatalogue.DefaultBase[j], Lends = b.Lending[j],
                        Wanted = OwnBankRules.LoansWanted(c, b, j)
                };
            }
            f.Courts = c.Courts;
            // Fund
            f.Strategy = b.Strategy;
            f.FundLimit = b.FundLimit;
            f.FundRoom = OwnBankFund.Room(b);
            f.StrategyNames = new string[OwnBankCatalogue.Strategies];
            for (int i = 0; i < f.StrategyNames.Length; i++)
            {
                f.StrategyNames[i] = say.T(OwnBankCatalogue.StrategyKeys[i]);
            }
            f.StrategyCompanies = OwnBankCatalogue.FundCompanies[Math.Max(1, Math.Min(OwnBankCatalogue.Strategies - 1, b.Strategy))];
            f.Holdings = new OwnHoldingFact[b.Holdings.Count];
            long dividends = 0;
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                FundHolding h = b.Holdings[i];
                long year = h.Qty * h.DpsCents * 4;
                dividends += year;
                f.Holdings[i] = new OwnHoldingFact { Name = string.IsNullOrEmpty(h.Ticker) ? h.Name : h.Name + " (" + h.Ticker + ")", Qty = h.Qty, Cost = h.CostCents,
                    Value = h.Qty * h.LastCents, Year = year };
            }
            f.FundDividends = dividends;
            f.FundSeries = world.FundSeries;
            f.FundDays = world.FundDays;
            // Branches
            f.BranchOn = world.BranchOn;
            f.Branches = new OwnBranchFact[b.Branches.Count];
            int week = 0;
            long fees = 0, costs = 0;
            for (int i = 0; i < b.Branches.Count; i++)
            {
                BankBranch br = b.Branches[i];
                long[] m = br.Month, last = br.Last;
                long cost = i < world.BranchCostDay.Length ? world.BranchCostDay[i] : 0L;
                f.Branches[i] = new OwnBranchFact
                {
                    Id = br.Place, Name = br.Name, Visitors = m[BranchFigure.Visitors], Served = m[BranchFigure.Served], Lost = m[BranchFigure.Lost], Opened = m[BranchFigure.Opened],
                        Loans = m[BranchFigure.Loans],
                    Fees = m[BranchFigure.Fees], CostDay = cost, PaidIn = m[BranchFigure.PaidIn], TakenOut = m[BranchFigure.TakenOut], Lent = m[BranchFigure.Lent], Refused = m[BranchFigure.Refused],
                    LastVisitors = last[BranchFigure.Visitors], LastServed = last[BranchFigure.Served], LastLost = last[BranchFigure.Lost], LastOpened = last[BranchFigure.Opened],
                        LastLoans = last[BranchFigure.Loans],
                    LastLent = last[BranchFigure.Lent], LastRefused = last[BranchFigure.Refused], LastFees = last[BranchFigure.Fees], LastPaidIn = last[BranchFigure.PaidIn],
                        LastTakenOut = last[BranchFigure.TakenOut],
                    Week = br.WeekServed(today), Open = i < world.BranchOpen.Length && world.BranchOpen[i], Tellers = i < world.BranchTellers.Length ? world.BranchTellers[i] : 0,
                    StaffAll = i < world.BranchStaff.Length ? world.BranchStaff[i] : 0, ServePerTeller = BranchCatalogue.ServePerTeller
                };
                week += f.Branches[i].Week;
                fees += m[BranchFigure.Fees];
                costs += cost;
            }
            f.WeekServed = week;
            f.BranchFees = fees;
            f.BranchCosts = costs;
            f.Reach = world.BranchOn ? BranchRules.Reach(week) : 0;
            f.ReachFirms = f.Reach * BranchCatalogue.FirmsReach / 1000;
            f.BranchBrings = f.Reach > 0;
            if (f.BranchBrings)
            {
                BankCity with = c;
                with.Reach = f.Reach;
                BranchWorth worth = BranchRules.Worth(with, b, f.Trust);
                f.BranchDeposits = worth.Deposits;
                f.BranchLoans = worth.Loans;
                f.BranchMargin = worth.DayCents;
            }
        }

        private static string Ratio(int thousandths) => (thousandths / 10.0).ToString("0.#", Inv) + "%";
    }
}
