using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Banks and Your bank screens' samples: each state is the facts of a save (BankFacts, OwnBankFacts) and what the player chose, written
// through the same build the live screens use. Realistic data: long names, a day-5 save with nothing, a bank at its limits.
internal static partial class UiSamples
{
    public static readonly string[] MoneyBankStates =
    {
        "accounts", "accounts_new", "accounts_long", "loans", "loans_pick", "loans_call", "overdraft", "overdraft_offer", "card", "card_offer", "apply", "apply_loan", "apply_business",
            "apply_finance",
        "apply_refused", "rating"
    };

    public static readonly string[] MoneyOwnStates =
    {
        "bank", "bank_level", "bank_pay", "bank_pay_fixed", "bank_div", "bank_new", "bank_nohq", "bank_closed", "savers", "savers_pick", "lending", "lending_pick", "fund", "fund_new", "branches",
        "branches_pick", "branches_none", "bank_top"
    };

    public static MoneyModel MoneyBanks(UiWords t, string state)
    {
        MoneyModel m = BanksModelOf(state);
        BanksBuild.Fill(m, BanksFactsOf(state), UtilitiesWordsOf(t));
        return m;
    }

    public static MoneyModel MoneyOwn(UiWords t, string state)
    {
        MoneyModel m = OwnModelOf(state);
        OwnBankBuild.Fill(m, OwnBankFactsOf(state), UtilitiesWordsOf(t));
        return m;
    }

    /// <summary>The model a state starts from: the tab and the row picked, as the host would hold them.</summary>
    public static MoneyModel BanksModelOf(string state)
    {
        var m = new MoneyModel { App = MoneyApp.Banks, Icon = UiIcon.Bank };
        m.Tab = state.StartsWith("loans", StringComparison.Ordinal) ? BanksBuild.Loans : state.StartsWith("overdraft", StringComparison.Ordinal) ? BanksBuild.Overdraft
            : state.StartsWith("card", StringComparison.Ordinal) ? BanksBuild.Card : state.StartsWith("apply", StringComparison.Ordinal) ? BanksBuild.Apply
            : state.StartsWith("rating", StringComparison.Ordinal) ? BanksBuild.Rating : BanksBuild.Accounts;
        m.View.Row = state == "loans_pick" ? "acct:7" : state == "loans_call" ? "acct:9" : state == "apply_loan" ? "p:0" : state == "apply_business" ? "p:3" : state == "apply_finance" ? "p:5"
            : state == "apply_refused" ? "p:0" : null;
        return m;
    }

    public static MoneyModel OwnModelOf(string state)
    {
        var m = new MoneyModel { App = MoneyApp.OwnBank, Icon = UiIcon.PiggyBank };
        m.Tab = state.StartsWith("savers", StringComparison.Ordinal) ? OwnBankBuild.SaversTab : state.StartsWith("lending", StringComparison.Ordinal) ? OwnBankBuild.LendingTab
            : state.StartsWith("fund", StringComparison.Ordinal) ? OwnBankBuild.FundTab : state.StartsWith("branches", StringComparison.Ordinal) ? OwnBankBuild.BranchesTab : OwnBankBuild.BankTab;
        m.View.Row = state == "bank_level" ? OwnBankBuild.LevelRow : state == "bank_pay" || state == "bank_pay_fixed" ? OwnBankBuild.PayRow : state == "bank_div" ? OwnBankBuild.DivRow
            : state == "savers_pick" ? "s|0" : state == "lending_pick" ? "l|4" : state == "branches_pick" ? "r|3 High Street|1" : null;
        return m;
    }

    // ------------------------------------------------------------------------------------------------- Banks

    private static BankMove Move(string day, string what, float amount, float balance) => new BankMove { Day = day, What = what, Amount = amount, Balance = balance };

    public static BanksFacts BanksFactsOf(string state)
    {
        bool fresh = state == "accounts_new", refused = state == "apply_refused", longName = state == "accounts_long";
        var f = new BanksFacts
        {
            Ready = true, Day = fresh ? 5 : 289, Score = fresh ? 640 : refused ? 540 : 742, Threshold = 580, ScoreMax = 850, Grade = fresh ? "Fair" : refused ? "Poor" : "Good",
            GradeTone = fresh ? UiRole.Warn : refused ? UiRole.Danger : UiRole.Ok, Lends = !refused, Halves = fresh ? 4 : refused ? 2 : 8, Cash = fresh ? 12400f : 1204300f, IsHouse = true,
            HouseName = "Hudson Trust", ProductsOn = true, Headroom = refused ? 0 : fresh ? 22 : 62, Note = "The city's oldest bank, slow to lend and quick to forgive a late payment.",
            Owed = fresh ? 0f : 1980000f, Tomorrow = fresh ? 0f : 1240f, Debts = fresh ? 0 : 3
        };
        string a = longName ? "Hudson Trust Savings and Commercial Bank" : "Hudson Trust";
        f.Banks = new[]
        {
            new BankFact { Name = a, Rank = 0, Rate = 4.8f, Most = refused ? 0 : fresh ? 60000 : 400000, Lends = !refused, Since = fresh ? 0 : 36, Owed = f.Owed, Debts = f.Debts, Answer = !refused },
            new BankFact { Name = "Empire Federal", Rank = 1, Rate = 5.6f, Most = refused ? 0 : fresh ? 40000 : 300000, Lends = !refused, Since = fresh ? 0 : 52, Owed = fresh ? 0f : 420000f,
                Debts = fresh ? 0 : 1, Answer = !refused },
            new BankFact { Name = "Metropolitan Savings", Rank = 2, Rate = 5.9f, Most = refused ? 0 : fresh ? 25000 : 250000, Lends = !refused, Since = 0, Owed = 0f, Debts = 0,
                Answer = !refused && !fresh }
        };
        f.Periods = new[] { "Yesterday", "7 days", "30 days" };
        f.Period = 1;
        if (!fresh)
        {
            f.Statement = new[]
            {
                Move("289", "Loan payments, four daily", -1772f, 1199758f), Move("289", "Power and water, 7 5th Avenue", -318f, 1199440f), Move("289", "Takings so far, Speedy Bites", 4860f, 1204300f),
                Move("288", "Wages, all businesses", -7880f, 1201530f), Move("288", "Takings, Bloom & Co", 6310f, 1209410f), Move("288", "HSF dividend, 1,200 shares", 465f, 1184680f),
                Move("287", "Rent, Bloom & Co unit", -2840f, 1184215f), Move("287", "Loan payments, four daily", -1772f, 1190995f)
            };
            f.StatementIn = 11635f;
            f.StatementOut = 14582f;
        }
        switch (state)
        {
            case "loans":
            case "loans_pick":
            case "loans_call":
                f.Debt = new[]
                {
                    new BankDebt
                    {
                        Id = "loan:1", GameLoan = true, Kind = 1, Name = "Business loan, variable", Owed = 1200000f, Total = 1500000f, Rate = "6.4%", NextAmount = 877, NextDay = 290,
                            NextWord = "tomorrow", EndDay = 1660,
                        State = "On time", Sound = true, Payment = 740, Left = 1371, Interest = 137f, Opened = 118, Thing = "Secured on Bloom & Co, Garment District",
                            About = "A business loan is repaid from the shop's takings.",
                        PayMost = 1200000, PaySuggested = 740
                    },
                    new BankDebt
                    {
                        Id = "acct:7", Number = 7, Kind = 5, Name = "Mortgage, 7 5th Avenue", Owed = 780000f, Total = 800000f, Rate = "4.8%", NextAmount = 363, NextDay = 290,
                            NextWord = "tomorrow", EndDay = 2430, State = "On time", Sound = true,
                        Payment = 363, Left = 2141, Opened = 201, Thing = "Secured on 7 5th Avenue", About = "A mortgage is repaid every day; the building is the bank's if it is not.",
                            PayMost = 780000, PaySuggested = 363
                    },
                    new BankDebt
                    {
                        Id = "acct:9", Number = 9, Kind = 4, Name = "Car finance, Onyx GT", Owed = 38400f, Total = 40000f, Rate = "5.9%", NextAmount = 112, NextDay = state == "loans_call" ? 287 : 290,
                        NextWord = state == "loans_call" ? "2 days ago" : "tomorrow", EndDay = 642, State = state == "loans_call" ? "2 days late" : "On time", Sound = state != "loans_call",
                            CallDue = state == "loans_call",
                        Overdue = state == "loans_call" ? 224f : 0f, Payment = 112, Left = 343, Opened = 266, Thing = "Secured on the Onyx GT", About = "Car finance is repaid every day.",
                            PayMost = 38400, PaySuggested = state == "loans_call" ? 224 : 112
                    }
                };
                break;
            case "overdraft":
                f.Overdraft = new BankLimit
                {
                    Held = true,
                    Account = new BankDebt
                    {
                        Id = "acct:4", Number = 4, Kind = 2, Name = "Overdraft, Hudson Trust", Owed = 4400f, Total = 10000f, Free = 5600, Rate = "9.8%", State = "On time", Sound = true,
                            Opened = 140, PayMost = 4400, PaySuggested = 4400,
                        Thing = "You may owe up to your limit; the first $500 of it is free for a day.", About = "An overdraft takes what you owe at the end of the day, up to its limit."
                    }
                };
                break;
            case "overdraft_offer":
                f.Overdraft = new BankLimit { OfferLimit = 25000, OfferRate = 980, Free = "An overdraft takes what you owe at the end of the day, up to its limit." };
                break;
            case "card":
                f.Card = new BankLimit
                {
                    Held = true,
                    Account = new BankDebt
                    {
                        Id = "acct:5", Number = 5, Kind = 3, Name = "Card, Hudson Trust", Owed = 4400f, Total = 20000f, Free = 15600, DrawMost = 15600, Rate = "14.2%", State = "On time",
                            Sound = true, Minimum = 220, StatementLeft = 1800,
                        DueDay = 297, NextStatement = 311, Opened = 160, PayMost = 4400, PaySuggested = 1800, About = "A card's statement comes every 14 days; its minimum is taken on the due day."
                    }
                };
                f.CardMoves = new[]
                {
                    Move("day 288", "Stock for Speedy Bites", 2310f, 0f), Move("day 285", "Paid into the card", -1500f, 0f), Move("day 281", "Cash drawn", 3500f, 0f), Move("day 276",
                        "Paid into the card", -800f, 0f),
                    Move("day 272", "Internet, Midtown office", 96f, 0f)
                };
                break;
            case "card_offer":
                f.Card = new BankLimit { Elsewhere = "Empire Federal" };
                break;
            case "apply":
            case "apply_loan":
            case "apply_business":
            case "apply_finance":
            case "apply_refused":
                ApplyFacts(f, state, fresh, refused);
                break;
            case "rating":
                RatingFacts(f);
                break;
        }
        return f;
    }

    private static void ApplyFacts(BanksFacts f, string state, bool fresh, bool refused)
    {
        string no = "Not offered", on = "On offer";
        f.Products = new[]
        {
            new BankProduct { Kind = 0, Name = "Loan by phone", Most = refused ? 0 : 400000, Rate = "4.8%", Daily = refused ? null : "$874", Fee = refused ? null : "$12,000",
                State = refused ? no : on, On = !refused },
            new BankProduct { Kind = 1, Name = "Overdraft", Most = refused ? 0 : 25000, Rate = "9.8%", State = refused ? no : on, On = !refused },
            new BankProduct { Kind = 2, Name = "Credit card", Most = refused ? 0 : 20000, Rate = "14.2%", State = refused ? "Open" : "Open", Open = true },
            new BankProduct { Kind = 3, Name = "Business loan", Most = 0, State = refused ? no : on, On = !refused },
            new BankProduct { Kind = 4, Name = "Car finance", Most = refused ? 0 : 150000, Rate = "5.9%", State = refused ? no : on, On = !refused },
            new BankProduct { Kind = 5, Name = "Mortgage", Most = refused ? 0 : 1200000, Rate = "4.8%", State = refused ? no : on, On = !refused },
            new BankProduct { Kind = 6, Name = "Consolidation", Most = 0, State = refused ? no : on, On = !refused }
        };
        var checks = new List<BankCheck>
        {
            new BankCheck { Text = refused ? "Your rating is 540; Hudson Trust lends from 580" : "Your rating is 742; Hudson Trust lends from 580", Met = !refused },
            new BankCheck { Text = "Nothing you owe here is overdue", Met = true },
            new BankCheck { Text = "$180,000 is under the $400,000 Hudson Trust lends you", Met = !refused },
            new BankCheck { Text = "2 applications in the last 90 days count against you", Met = false }
        };
        f.Checks = checks.ToArray();
        f.Tips = refused ? new[] { "Pay what is overdue: a late payment costs the most points.", "Keep your cards under half their limit." } : new string[0];
        if (state == "apply_loan")
        {
            f.Quote = new BankQuote
            {
                Rate = "4.8% a year", HasAmount = true, Amount = 180000, Least = 5000, Most = 400000, AmountPrev = 100000, AmountNext = 250000, AmountHint = "$5,000 to $400,000",
                Labels = new[] { "Fee", "Payment a day", "All you repay" }, Values = new[] { "$5,400", "$336", "$216,600" },
                Note = "It adds $336 to the $1,240 that already leave each day.", Note2 = null
            };
        }
        else if (state == "apply_business")
        {
            f.Quote = new BankQuote
            {
                Rate = "5.2% a year", Record = "Bloom & Co has 34 days of takings on record, $19,400 a day; it needs 30.", HasAmount = true, Amount = 180000, Least = 5000, Most = 400000,
                    AmountPrev = 100000,
                AmountNext = 250000, AmountHint = "$5,000 to $400,000", StepLabel3 = "Business", StepText3 = "Bloom & Co, Garment District", Step3Prev = -1, Step3Next = 1,
                Labels = new[] { "Fee", "Payment a day" }, Values = new[] { "$5,400", "$338" },
                Note = "A business loan lends up to 20 times the shop's daily profit and takes a share of its takings."
            };
        }
        else if (state == "apply_finance")
        {
            f.Quote = new BankQuote
            {
                Rate = "4.8% a year", HasAmount = true, Amount = 600000, Least = 20000, Most = 1200000, AmountPrev = 500000, AmountNext = 1000000, AmountHint = "$20,000 to $1,200,000",
                StepLabel1 = "Deposit", StepText1 = "20%", Step1Prev = 0, Step1Next = 2, StepLabel2 = "Term", StepText2 = "600 days", Step2Prev = 1, Step2Next = 3,
                Labels = new[] { "Price", "Your deposit", "First payment", "Interest over the term" }, Values = new[] { "$750,000", "$150,000", "$1,450", "$212,000" },
                Note = "A mortgage is secured on the building: the bank takes it if you stop paying."
            };
        }
        else if (state == "apply_refused")
        {
            f.Quote = new BankQuote { Refused = "Hudson Trust will not lend: your rating is 540 and it lends from 580." };
        }
        f.Headroom = refused ? 0 : fresh ? 22 : 62;
    }

    private static void RatingFacts(BanksFacts f)
    {
        f.Factors = new[]
        {
            new BankFactor { Name = "Payments", Part = 82, Stands = "Paid on time in 94 of the last 100 payments." },
            new BankFactor { Name = "What you owe", Part = 58, Stands = "You owe a fair share of what you could borrow." },
            new BankFactor { Name = "History", Part = 46, Stands = "Your oldest account is 80 days old." },
            new BankFactor { Name = "Legal record", Part = 94, Stands = "No case against you." },
            new BankFactor { Name = "Applications", Part = 64, Stands = "Two applications in the last 90 days." }
        };
        f.Bands = new[]
        {
            new BankBand { Score = "579", Kind = "Under the line", Refused = true },
            new BankBand { Score = "580", Kind = "At the line", Loan = "$40,000", Rate = "9.9%", Card = "$4,000", Overdraft = "$2,000", Car = "$20,000" },
            new BankBand { Score = "630", Kind = "Fair", Loan = "$120,000", Rate = "7.8%", Card = "$9,000", Overdraft = "$6,000", Car = "$60,000" },
            new BankBand { Score = "690", Kind = "Good", Loan = "$260,000", Rate = "6.0%", Card = "$16,000", Overdraft = "$11,000", Car = "$110,000" },
            new BankBand { Score = "750", Kind = "Best terms", Loan = "$400,000", Rate = "4.8%", Card = "$20,000", Overdraft = "$25,000", Car = "$150,000" },
            new BankBand { Score = "742", Kind = "Yours", Loan = "$400,000", Rate = "4.8%", Card = "$20,000", Overdraft = "$25,000", Car = "$150,000", Yours = true }
        };
        f.Tips = new[] { "Keep your cards under half their limit." };
        f.Next = "At a rating of 750 Hudson Trust would lend you $400,000 at 4.8% and give you a card of $20,000.";
    }

    // ------------------------------------------------------------------------------------------------- Your bank

    private static long D(long dollars) => dollars * 100L;

    public static OwnBankFacts OwnBankFactsOf(string state)
    {
        bool longName = state == "bank_top";
        var f = new OwnBankFacts
        {
            Ready = true, State = 1, Day = 140, Name = longName ? "The Hudson Valley Cooperative and Mercantile Bank of the Five Boroughs" : "Pioneer Bank", Level = "Community bank",
                Tier = 0, StageWords = "The regulator has no complaint",
            Licence = D(150000), FoundCapital = D(300000), HasHq = state != "bank_nohq", HqName = "7 5th Avenue", LawRatio = 100, WatchRatio = 125, RivalSaverBp = 175, RivalLoanBp = 690,
                CommBranches = 2,
            CommLoans = D(2500000), CommDeposits = D(4000000), CommunityHolds = "2 branches, $2,500,000 in loans, $4,000,000 in deposits at most", NextCapital = D(450000)
        };
        if (state == "bank_new" || state == "bank_nohq")
        {
            f.State = 0;
            return f;
        }
        if (state == "bank_closed")
        {
            f.State = 3;
            f.EndDay = 120;
            f.EndCents = D(212000);
            f.Why = "its capital fell under the law's least";
            f.Closures = 1;
            f.AgainWait = "A new licence can be asked for on day 180.";
            return f;
        }
        f.Reserves = D(520000);
        f.Lent = D(2350000);
        f.Arrears = D(80000);
        f.Fund = D(450000);
        f.FundCost = D(420000);
        f.Deposits = D(3100000);
        f.Capital = D(300000);
        f.Risk = D(2900000);
        f.ReserveNeed = D(248000);
        f.LostAll = D(60000);
        f.RecoveredAll = D(22000);
        f.CitySavings = D(650000000);
        f.Ratio = 128;
        f.Confidence = 780;
        f.Trust = 710;
        f.MonthIncome = D(124000);
        f.MonthCosts = D(97000);
        f.MonthLost = D(8000);
        f.LastIncome = D(131000);
        f.LastCosts = D(101000);
        f.HasLast = true;
        f.HasHistory = true;
        f.HasDaily = true;
        f.YearProfit = D(310000);
        f.SaleValue = D(1230000);
        f.MostOut = D(100000);
        f.Sellable = true;
        f.PerDayNow = D(900);
        f.PerDayLast = D(1000);
        f.DepositsMost = D(4000000);
        f.LoansMost = D(2500000);
        f.BranchesNow = 2;
        f.BranchesMost = 2;
        f.Over = false;
        f.NextLevel = "Regional bank";
        f.RecordMonths = 6;
        f.HeldMonths = 4;
        f.CapitalLeast = D(700000);
        f.RegulatorOk = true;
        f.MoveFee = D(650000);
        f.MoveWhy = "It has held its level for 4 of the 6 months it needs without a breach.";
        f.AfterBranches = 6;
        f.AfterLoans = D(12000000);
        f.AfterDeposits = D(20000000);
        f.PayMode = state == "bank_pay_fixed" ? 1 : 0;
        f.PayShare = 300;
        f.PayFixed = D(400);
        f.PayWanted = D(380);
        f.PayGiven = D(310);
        f.PaidMonth = D(8400);
        f.PaidLast = D(9100);
        f.PaidAll = D(41800);
        f.PayHeld = "$70 was held back: the cash over the reserve kept against the deposits was short.";
        f.DivEvery = state == "bank_div" ? 1 : 0;
        f.DivEveryNames = new[] { "Off", "Every 7 days", "Every month", "Every year" };
        f.DivShare = 500;
        f.DivNextDay = 147;
        f.DivKept = D(6200);
        f.DivDay = 133;
        f.DivPaid = D(4100);
        f.DivWanted = D(4800);
        f.DivShort = true;
        f.DivWhy = "$4,100 of $4,800 was paid: its capital over its least, its lending line and your fixed pay was short.";
        f.DivAll = D(18600);
        f.Savers = new[]
        {
            new OwnSaverFact { Name = "People", Deposits = D(1900000), Pool = D(400000000), OwnBp = 215, RivalBp = 175, Offset = 40, Wanted = D(2100000) },
            new OwnSaverFact { Name = "Firms", Deposits = D(1200000), Pool = D(250000000), OwnBp = 180, RivalBp = 190, Offset = -10, Wanted = D(900000) }
        };
        f.Segments = new[]
        {
            new OwnSegmentFact { Name = "Poorly rated people", Lent = D(40000), Arrears = D(12000), Recovering = D(3000), OwnBp = 1900, RivalBp = 1800, Offset = 100, DefaultPerMille = 180,
                DefaultUsual = 160, Lends = false, Wanted = D(90000) },
            new OwnSegmentFact { Name = "Fairly rated people", Lent = D(420000), Arrears = D(21000), Recovering = D(8000), OwnBp = 980, RivalBp = 940, Offset = 40, DefaultPerMille = 52,
                DefaultUsual = 50, Lends = true, Wanted = D(500000) },
            new OwnSegmentFact { Name = "Well rated people", Lent = D(610000), Arrears = D(4000), Recovering = D(1000), OwnBp = 640, RivalBp = 690, Offset = -50, DefaultPerMille = 14,
                DefaultUsual = 15, Lends = true, Wanted = D(700000) },
            new OwnSegmentFact { Name = "Shops", Lent = D(520000), Arrears = D(18000), Recovering = D(6000), OwnBp = 780, RivalBp = 760, Offset = 20, DefaultPerMille = 46, DefaultUsual = 45,
                Lends = true, Wanted = D(430000) },
            new OwnSegmentFact { Name = "Companies", Lent = D(760000), Arrears = D(25000), Recovering = D(4000), OwnBp = 560, RivalBp = 580, Offset = -20, DefaultPerMille = 16,
                DefaultUsual = 15, Lends = true, Wanted = D(900000) }
        };
        f.Courts = true;
        f.Strategy = 2;
        f.FundLimit = 150;
        f.FundRoom = D(15000);
        f.StrategyNames = new[] { "None", "Cautious", "Balanced", "Bold" };
        f.StrategyCompanies = 10;
        f.Holdings = new[]
        {
            new OwnHoldingFact { Name = "Harbourside Freight (HSF)", Qty = 12000, Cost = D(180000), Value = D(196000), Year = D(5600) },
            new OwnHoldingFact { Name = "Speedy Bites (SPB)", Qty = 8000, Cost = D(120000), Value = D(118000), Year = D(2400) },
            new OwnHoldingFact { Name = "Cinder Valley Games (CVG)", Qty = 6500, Cost = D(120000), Value = D(136000), Year = D(0) }
        };
        f.FundDividends = D(8000);
        if (state != "fund_new")
        {
            int n = 30;
            f.FundSeries = new float[n];
            f.FundDays = new int[n];
            for (int i = 0; i < n; i++)
            {
                f.FundDays[i] = 111 + i;
                f.FundSeries[i] = 410000f + i * 1380f + (float)Math.Sin(i * 0.7) * 6500f;
            }
        }
        else
        {
            f.Fund = 0;
            f.FundCost = 0;
            f.Holdings = new OwnHoldingFact[0];
            f.FundSeries = new float[0];
            f.FundDays = new int[0];
        }
        f.Branches = state == "branches_none" ? new OwnBranchFact[0] : new[]
        {
            new OwnBranchFact { Id = "7 5th Avenue|1", Name = "Headquarters, 7 5th Avenue", Visitors = 1480, Served = 1390, Lost = 90, Opened = 118, Loans = 31, Fees = D(5400),
                CostDay = D(420), PaidIn = D(310000), TakenOut = D(120000),
                Lent = D(260000), Refused = 14, LastVisitors = 1420, LastServed = 1300, LastLost = 120, Week = 330, Open = true, Tellers = 3, StaffAll = 4, ServePerTeller = 60 },
            new OwnBranchFact { Id = "3 High Street|1", Name = "3 High Street", Visitors = 610, Served = 340, Lost = 270, Opened = 41, Loans = 9, Fees = D(1700), CostDay = D(310),
                PaidIn = D(88000), TakenOut = D(61000),
                Lent = D(70000), Refused = 6, LastVisitors = 590, LastServed = 360, LastLost = 230, Week = 140, Open = true, Tellers = 1, StaffAll = 2, ServePerTeller = 60 }
        };
        f.BranchOn = true;
        f.WeekServed = state == "branches_none" ? 0 : 470;
        f.BranchFees = D(7100);
        f.BranchCosts = state == "branches_none" ? 0 : D(730);
        f.Reach = state == "branches_none" ? 0 : 54;
        f.ReachFirms = f.Reach * 7 / 10;
        f.BranchBrings = f.Reach > 0;
        f.BranchDeposits = D(380000);
        f.BranchLoans = D(260000);
        f.BranchMargin = D(1200);
        if (state == "bank_top")
        {
            f.Level = "National bank";
            f.Tier = 2;
            f.Top = true;
            f.NextLevel = null;
            f.DepositsMost = -1;
            f.LoansMost = -1;
            f.BranchesMost = 40;
            f.Stage = 1;
            f.StageWords = "The regulator is watching it: its ratio is under 12.5%";
            f.Crews = "Tellers 120 of 140, managers 12 of 12, officers 30 of 31, compliance 14 of 14; wages $18,400 a day.";
        }
        return f;
    }
}
