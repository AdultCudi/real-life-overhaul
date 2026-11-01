using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;

/// <summary>
/// EconoView on the kit, each state as the game would hand it over (an EconoState, through the same EconoViews the game's
/// screen uses): a rich save's overview, a new save's, the overview with its day's list open, every transaction, the
/// transactions filtered and with the kinds' list open, no loans and three, the investments with a year's line and none,
/// the taxes with and without back taxes, one business with its choice closed and open, and the overview and loans in
/// German (the longest of the languages the game and the mod ship). Plain English for the rest, as the owner reads the previews.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>Every state of EconoView, in the previews' order.</summary>
    public static readonly string[] EconoStates =
    {
        "overview_rich", "overview_new", "overview_period_open", "transactions_all", "transactions_filtered", "transactions_type_open", "loans_none", "loans_three",
        "investments_year", "investments_none", "taxes", "taxes_back", "business", "business_choice_open", "overview_de", "loans_de"
    };

    private static readonly CultureInfo EconoInv = CultureInfo.InvariantCulture;

    /// <summary>EconoView's place in the frame's app row, or -1 where the phone keeps it on a later page.</summary>
    public static int RowEconoView => Array.IndexOf(RowIds, "EconoView");

    private const int EconoToday = 412;

    // The game's money words as its short currency format writes them.
    private static string EconoMoney(float v) => (v < 0f ? "-" : string.Empty) + "$" + Math.Abs(Math.Round(v)).ToString("#,0", EconoInv);

    /// <summary>EconoView in one of its states.</summary>
    public static UiEconoModel Econo(UiWords t, string state)
    {
        EconoState v = EconoSample(t, state);
        var m = new UiEconoModel();
        EconoViews.Page(m, t, v);
        return m;
    }

    private static EconoState EconoSample(UiWords t, string state)
    {
        var v = new EconoState { Money = EconoMoney, Cash = 48210f };
        string day = t("common_day_number", "Day {number}");
        v.DayNames = new[]
        {
            EconoViews.Fill(t("econoview_yesterday_date", "Yesterday (Day {dayNumber})"), "dayNumber", "411"), EconoViews.Fill(day, "number", "410"),
            EconoViews.Fill(day, "number", "409"), EconoViews.Fill(day, "number", "408")
        };
        v.DayNumbers = new[] { 411, 410, 409, 408 };
        v.Income = RichIncome(t);
        v.CashTrend = new[] { 39120f, 39880f, 41020f, 40210f, 41790f, 42630f, 43310f, 42980f, 44520f, 45210f, 46040f, 45700f, 47130f, 48210f };
        v.Latest = Latest(t, 8);
        v.Businesses = new[] { "Bloom Café", "Harbour Hardware", "Uptown Bakery" };
        v.Loans = ThreeLoans(t);
        v.Funds = Funds(t);
        v.Tax = Taxes(t, false);
        switch (state)
        {
            case "overview_new":
                v.Cash = 1250f;
                v.CashTrend = new[] { 1000f, 1250f };
                v.DayNames = new[] { v.DayNames[0].Replace("411", "1") };
                v.DayNumbers = new[] { 1 };
                v.Income = NewIncome(t);
                v.Latest = NewLatest(t);
                v.Businesses = new string[0];
                v.Loans = new EconoLoanView[0];
                v.Funds = new EconoFundView[0];
                break;
            case "overview_period_open":
                v.OpenList = UiEcono.ListPeriod;
                v.Period = 1;
                break;
            case "transactions_all":
            case "transactions_filtered":
            case "transactions_type_open":
                v.Page = UiEcono.Transactions;
                Full(t, v, state);
                break;
            case "loans_none":
                v.Page = UiEcono.Loans;
                v.Loans = new EconoLoanView[0];
                break;
            case "loans_three":
            case "loans_de":
                v.Page = UiEcono.Loans;
                v.Loan = 0;
                break;
            case "investments_year":
                v.Page = UiEcono.Investments;
                v.Fund = 0;
                break;
            case "investments_none":
                v.Page = UiEcono.Investments;
                v.Funds = new EconoFundView[0];
                break;
            case "taxes":
                v.Page = UiEcono.Taxes;
                break;
            case "taxes_back":
                v.Page = UiEcono.Taxes;
                v.Tax = Taxes(t, true);
                break;
            case "business":
            case "business_choice_open":
                v.Page = UiEcono.Business;
                v.BusinessChosen = 0;
                v.BusinessKind = "Coffee Shop";   // the business type's words, as the game's page writes them
                v.BusinessDays = v.DayNames;
                v.BusinessRows = BusinessRows(t);
                v.BusinessSales = new[] { 2180f, 1960f, 2040f, 1870f };
                v.BusinessProfit = new[] { 840f, 650f, 750f, 600f };
                v.BusinessItems = new[]
                {
                    new[] { "Coffee", "Croissant", "Cheese Sandwich" }, new[] { "Coffee", "Croissant", "Cheese Sandwich" }, new[] { "Coffee", "Croissant", "Cheese Sandwich" },
                    new[] { "Coffee", "Croissant", "Cheese Sandwich" }
                };
                v.BusinessItemSales = new[] { new[] { 1120f, 640f, 420f }, new[] { 1010f, 590f, 360f }, new[] { 1060f, 600f, 380f }, new[] { 960f, 540f, 370f } };
                v.BusinessCosts = new[]
                {
                    new[] { 680f, 240f, 100f, 0f, 0f, 320f }, new[] { 680f, 240f, 100f, 0f, 0f, 290f }, new[] { 650f, 240f, 100f, 0f, 0f, 300f }, new[] { 650f, 240f, 100f, 0f, 0f, 280f }
                };
                v.CanDestination = true;
                v.CanBizMan = true;
                v.OpenList = state == "business_choice_open" ? UiEcono.ListBusiness : -1;
                break;
        }
        return v;
    }

    private static EconoIncomeRow Head(string key, string name, params float[] values) =>
        new EconoIncomeRow { Key = key, Name = name, Parent = -1, Values = values };

    private static EconoIncomeRow Line(int parent, string key, string name, params float[] values) =>
        new EconoIncomeRow { Key = key, Name = name, Parent = parent, Values = values, Kind = values[0] < 0f ? -1 : 1 };

    // A player with three shops, a job, a let flat, loans, insurance and a few fees, yesterday first.
    private static EconoIncomeRow[] RichIncome(UiWords t)
    {
        var rows = new List<EconoIncomeRow>
        {
            Head("econoview_row_businesses", t("econoview_row_businesses", "Businesses"), 2560f, 2920f, 2910f, 2410f),
            Line(0, "Bloom Café", "Bloom Café", 1840f, 1620f, 1710f, 1490f),
            Line(0, "Harbour Hardware", "Harbour Hardware", 960f, 1120f, 880f, 1010f),
            Line(0, "Uptown Bakery", "Uptown Bakery", -240f, 180f, 320f, -90f),
            Head("econoview_row_other_income", t("econoview_row_other_income", "Other income"), 420f, 420f, 420f, 420f),
            Line(4, "econoview_row_salary_income", t("econoview_row_salary_income", "Salary"), 420f, 420f, 420f, 420f),
            Head("common_real_estate", t("common_real_estate", "Real estate"), 650f, 650f, 650f, 650f),
            Line(6, "70 Fifth Avenue", "70 Fifth Avenue", 650f, 650f, 650f, 650f),
            Head("econoview_row_ongoing_expenses", t("econoview_row_ongoing_expenses", "Ongoing Expenses"), -1066f, -1066f, -1042f, -1042f),
            Line(8, "econoview_row_private_residences", t("econoview_row_private_residences", "Private Residences"), -310f, -310f, -310f, -310f),
            Line(8, "econoview_row_loans", t("econoview_row_loans", "Loans"), -540f, -540f, -540f, -540f),
            Line(8, "ba:transaction_healthinsurance_label", t("ba:transaction_healthinsurance_label", "Health Insurance"), -120f, -120f, -96f, -96f),
            Line(8, "econoview_row_unassigned_staff_wages", t("econoview_row_unassigned_staff_wages", "Unassigned Staff Wages"), -96f, -96f, -96f, -96f),
            Head("econoview_row_fees", t("econoview_row_fees", "Fees"), -85f, -25f, -25f, -110f),
            Line(13, "ba:transaction_headhunterreplacement_label", t("ba:transaction_headhunterreplacement_label", "Headhunter Replacements"), -60f, 0f, 0f, -85f),
            Line(13, "econoview_row_parking_fees", t("econoview_row_parking_fees", "Parking fees"), -25f, -25f, -25f, -25f),
        };
        rows.Add(new EconoIncomeRow { Key = "econoview_row_total", Name = t("econoview_row_total", "Total"), Parent = -1, Total = true, Values = new[] { 2479f, 2899f, 2913f, 2328f } });
        return rows.ToArray();
    }

    // A first day: a job's pay and the flat's rent, the game's empty headings left as it makes them.
    private static EconoIncomeRow[] NewIncome(UiWords t)
    {
        return new[]
        {
            Head("econoview_row_businesses", t("econoview_row_businesses", "Businesses"), 0f, 0f, 0f, 0f),
            Head("econoview_row_other_income", t("econoview_row_other_income", "Other income"), 180f, 0f, 0f, 0f),
            Line(1, "econoview_row_salary_income", t("econoview_row_salary_income", "Salary"), 180f, 0f, 0f, 0f),
            Head("econoview_row_ongoing_expenses", t("econoview_row_ongoing_expenses", "Ongoing Expenses"), -55f, 0f, 0f, 0f),
            Line(3, "econoview_row_private_residences", t("econoview_row_private_residences", "Private Residences"), -55f, 0f, 0f, 0f),
            new EconoIncomeRow { Key = "econoview_row_total", Name = t("econoview_row_total", "Total"), Parent = -1, Total = true, Values = new[] { 125f, 0f, 0f, 0f } },
        };
    }

    private static readonly string[] TxTypes =
    {
        "ba:transaction_revenue", "ba:transaction_wage", "ba:transaction_importdelivery", "ba:transaction_revenue", "ba:transaction_rent", "ba:transaction_revenue",
        "ba:transaction_marketing", "ba:transaction_loanpayment", "ba:transaction_revenue", "ba:transaction_taxiride"
    };

    private static readonly string[] TxWords =
    {
        "Sales at Bloom Café", "Wages at Harbour Hardware", "Import delivery for Uptown Bakery", "Sales at Harbour Hardware", "Rent for 70 Fifth Avenue",
        "Sales at Uptown Bakery", "Billboard campaign for Bloom Café", "Loan payment to Midtown Savings Bank", "Sales at Bloom Café", "Taxi ride to Midtown"
    };

    private static readonly float[] TxAmounts = { 1580f, -680f, -425f, 960f, -240f, 610f, -150f, -540f, 1410f, -32f };

    private static EconoTx[] Latest(UiWords t, int n)
    {
        var rows = new EconoTx[n];
        float balance = 48210f;
        for (int i = 0; i < n; i++)
        {
            int k = i % TxTypes.Length;
            rows[i] = new EconoTx { Day = EconoToday - i / 3, Type = TxTypes[k], What = TxWords[k], Amount = TxAmounts[k], Balance = EconoMoney(balance) };
            balance -= TxAmounts[k];
        }
        return rows;
    }

    private static EconoTx[] NewLatest(UiWords t)
    {
        return new[]
        {
            new EconoTx { Day = 2, Type = "ba:transaction_playerjobsalary", What = "Salary from Bluebird Diner", Amount = 180f, Balance = "$1,250" },
            new EconoTx { Day = 1, Type = "ba:transaction_taxiride", What = "Taxi ride to Garment District", Amount = -25f, Balance = "$1,070" },
            new EconoTx { Day = 1, Type = "ba:transaction_rent", What = "Rent for 12 Orchard Street", Amount = -55f, Balance = "$1,095" },
        };
    }

    // Every transaction of four days, with the game's filters: all of them, wages paid out, or the kinds' list open.
    private static void Full(UiWords t, EconoState v, string state)
    {
        v.TypeOptions = new[] { t("econoview_full_transactions_all_types", "All types"), t("ba:transaction_revenue_label", "Revenue"), t("ba:transaction_wage_label", "Wage"),
            t("ba:transaction_importdelivery_label", "Import Delivery"), t("ba:transaction_rent_label", "Rent"), t("ba:transaction_marketing_label", "Marketing"),
            t("ba:transaction_loanpayment_label", "Loan Payment"), t("ba:transaction_taxiride_label", "Taxi Ride") };
        v.AmountOptions = new[] { t("amountoption_all", "All amounts"), t("amountoption_positive", "Positive amounts"), t("amountoption_negative", "Negative amounts") };
        v.DayOptions = new[] { t("econoview_full_transactions_all_days", "All days"), EconoViews.Fill(t("common_day_number", "Day {number}"), "number", "412"),
            EconoViews.Fill(t("common_day_number", "Day {number}"), "number", "411") };
        var all = new List<EconoTx>(48);
        float balance = 48210f;
        string[] times = { "18:30", "17:00", "15:45", "13:20", "12:30", "11:10", "10:15", "09:40", "08:30", "08:00" };
        for (int i = 0; i < 40; i++)
        {
            int k = i % TxTypes.Length;
            float amount = (float)Math.Round(TxAmounts[k] * (1f + 0.07f * (i / 10) * (TxAmounts[k] > 0f ? -1f : 1f)));   // each day's takings and costs a little apart
            all.Add(new EconoTx { Day = EconoToday - i / 10, Time = times[i % times.Length], Type = TxTypes[k], What = TxWords[k], Amount = amount, Balance = EconoMoney(balance) });
            balance -= amount;
        }
        // Another player's line, merged in by the multiplayer mod: no record of the player's own, so no time.
        all[13] = new EconoTx { Day = all[13].Day, Type = all[13].Type, What = all[13].What, Amount = all[13].Amount, Balance = all[13].Balance };
        if (state == "transactions_filtered")
        {
            all.RemoveAll(x => x.Type != "ba:transaction_wage");
            v.TypeChosen = 2;
            v.AmountChosen = 2;
        }
        else if (state == "transactions_type_open")
        {
            v.OpenList = UiEcono.ListType;
        }
        v.All = all.ToArray();
        v.FoldedDays = state == "transactions_all" ? new[] { EconoToday - 1 } : new int[0];
    }

    private static EconoLoanView[] ThreeLoans(UiWords t)
    {
        return new[]
        {
            Loan("Midtown Savings Bank", 50000f, 38000f, 45, 500, 120, 488, 76, 38000, 9),
            Loan("Harbour Credit Union", 20000f, 12400f, 18, 300, 60, 454, 42, 12400, 0),
            Loan("Empire State Bank", 100000f, 95000f, 90, 1200, 600, 492, 80, 95000, 3),
        };
    }

    private static EconoLoanView Loan(string bank, float total, float left, int interest, long daily, long least, int finalDay, int daysLeft, long payOff, int payments)
    {
        var lines = new EconoTx[payments];
        float balance = 48210f;
        for (int i = 0; i < payments; i++)
        {
            bool taken = i == payments - 1 && payments > 4;
            float amount = taken ? total : -daily;
            lines[i] = new EconoTx { Day = EconoToday - i, Type = taken ? "ba:transaction_loanpayout" : "ba:transaction_loanpayment",
                What = taken ? "Loan Payout" : "Loan Payment", Amount = amount, Balance = EconoMoney(balance) };
            balance += daily;
        }
        return new EconoLoanView
        {
            Lender = bank, Total = total, Left = left, PaidTotal = EconoMoney(total - left) + "/" + EconoMoney(total), Interest = EconoMoney(interest), Daily = EconoMoney(daily),
            DailyValue = daily, DailyLeast = least, TimeLeft = "Day " + finalDay.ToString(EconoInv) + " (" + daysLeft.ToString(EconoInv) + " days left)", PayOff = EconoMoney(payOff),
            PayOffValue = payOff, PayOffMost = (long)left, CanPay = true, Payments = lines
        };
    }

    private static EconoFundView[] Funds(UiWords t)
    {
        return new[]
        {
            Fund("ba:investmentfund_franklinus", t("ba:investmentfund_franklinus", "Franklin US"), 40000f, 5000f, 3200f, 2930f, 1, true, 500, 0.0011f),
            Fund("ba:investmentfund_alliancestechnologya", t("ba:investmentfund_alliancestechnologya", "Alliances Technology A"), 12000f, 1270f, 0f, -320f, 2, false, 0, -0.0004f),
            Fund("ba:investmentfund_hgchinabonds", t("ba:investmentfund_hgchinabonds", "HG China Bonds"), 30000f, 0f, 0f, 540f, 0, false, 0, 0.0003f),
        };
    }

    private static EconoFundView Fund(string key, string name, float initial, float added, float withdrawn, float earned, int risk, bool auto, long autoValue, float drift)
    {
        float value = initial + added - withdrawn + earned;
        var history = new float[60];
        var days = new int[60];
        for (int i = 0; i < 60; i++)
        {
            float back = 59 - i;   // a steady drift with a gentle swell, ending on today's value
            history[i] = (float)Math.Round(value * (1f - drift * back) + (float)Math.Sin(back * 0.6f) * value * 0.004f);
            days[i] = EconoToday - 60 + i;
        }
        var moves = new List<EconoTx>(6);
        float balance = 48210f;
        for (int i = 0; auto && i < 5; i++)
        {
            moves.Add(new EconoTx { Day = EconoToday - i, Type = "ba:transaction_investment", What = "Investment", Amount = -autoValue, Balance = EconoMoney(balance) });
            balance += autoValue;
        }
        if (withdrawn > 0f)
        {
            moves.Add(new EconoTx { Day = EconoToday - 6, Type = "ba:transaction_investmentpayout", What = "Investment Payout", Amount = withdrawn, Balance = EconoMoney(balance - withdrawn) });
        }
        return new EconoFundView
        {
            Key = key, Name = name, Value = value, Invested = initial + added, Interest = earned, Risk = risk, Initial = EconoMoney(initial), Added = EconoMoney(added),
            Withdrawn = EconoMoney(withdrawn), AutoOn = auto, Auto = EconoMoney(autoValue), AutoValue = autoValue, Withdraw = EconoMoney(5000f),
            WithdrawValue = 5000, WithdrawMost = (long)value, History = history, HistoryDays = days, Moves = moves.ToArray()
        };
    }

    private static EconoTaxView Taxes(UiWords t, bool back)
    {
        return new EconoTaxView
        {
            Rate = "21%", Back = back ? "$1,960" : null, Owed = "$3,420", OwedWhen = "Day 420 (8 days left)", OwedTone = -1, OwedProgress = 61,
            Projection = "$12,800", ProjectionWhen = "Day 480 (68 days left)", ProjectionTone = -1, ProjectionProgress = 13, Deductions = "$8,950",
            DeductibleKinds = new[] { "Retail Appliance Stores & Office Supply Stores", "Interior Installation & Movers Fees", "Wages, Insurance, Rent, Marketing, & Recruitment",
                "Cost of Inventory", "Business Classes", "Delivery Vehicles & Transportation Costs", "Factory Supply Depot" },
            Irs = true,
            BasisNames = new[] { "Bloom Café", "Harbour Hardware", "Uptown Bakery" },
            BasisAmounts = new[] { 58400f, 41200f, 17900f },
            Paid = new[]
            {
                new EconoTx { Day = 361, Type = "ba:transaction_taxpayment", What = "Tax Payment", Amount = -9840f, Balance = "$31,620" },
                new EconoTx { Day = 301, Type = "ba:transaction_taxpayment", What = "Tax Payment", Amount = -6210f, Balance = "$22,940" },
            }
        };
    }

    private static EconoIncomeRow Words(int parent, string key, string name, int kind, params string[] texts) =>
        new EconoIncomeRow { Key = key, Name = name, Parent = parent, Texts = texts, Kind = kind };

    // A coffee shop's four days as the game's business page writes them.
    private static EconoIncomeRow[] BusinessRows(UiWords t)
    {
        return new[]
        {
            Words(-1, "econoview_row_sales", t("econoview_row_sales", "Sales"), 0, "$2,180", "$1,960", "$2,040", "$1,870"),
            Words(0, "ba:itemname_coffee", "Coffee", 1, "$1,120", "$1,010", "$1,060", "$960"),
            Words(0, "ba:itemname_croissant", "Croissant", 1, "$640", "$590", "$600", "$540"),
            Words(0, "ba:itemname_cheesesandwich", "Cheese Sandwich", 1, "$420", "$360", "$380", "$370"),
            Words(-1, "econoview_row_ongoing_expenses", t("econoview_row_ongoing_expenses", "Ongoing Expenses"), 0, "-$1,020", "-$1,020", "-$990", "-$990"),
            Words(4, "econoview_row_salaries", t("econoview_row_salaries", "Salaries"), -1, "-$680", "-$680", "-$650", "-$650"),
            Words(4, "econoview_row_rent", t("econoview_row_rent", "Rent"), -1, "-$240", "-$240", "-$240", "-$240"),
            Words(4, "econoview_row_marketing", t("econoview_row_marketing", "Marketing"), -1, "-$100", "-$100", "-$100", "-$100"),
            Words(-1, "econoview_row_resources", t("econoview_row_resources", "Resources"), 0, "-$320", "-$290", "-$300", "-$280"),
            Words(8, "ba:itemname_coffeebeans", "Coffee Beans", -1, "-$180", "-$160", "-$170", "-$150"),
            Words(8, "ba:itemname_bread", "Bread", -1, "-$140", "-$130", "-$130", "-$130"),
            new EconoIncomeRow { Key = "econoview_row_profit", Name = t("econoview_row_profit", "Profit"), Parent = -1, Total = true, Texts = new[] { "$840", "$650", "$750", "$600" } },
        };
    }
}
