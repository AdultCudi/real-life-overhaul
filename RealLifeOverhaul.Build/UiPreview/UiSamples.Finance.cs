using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;

// The Finance screen's samples: a save's facts for each state, written through the real tables (FinanceTables) and laid by the real face
// (FinanceFace), so a shot shows what ships. Day 5 with little history, a player with a loan and a card, and a rival who just made a move.
internal static partial class UiSamples
{
    public static readonly string[] FinanceStates =
    {
        "overview", "overview_new", "overview_long", "credit", "credit_new", "borrow", "borrow_card", "borrow_finance", "borrow_refused", "cash", "cash_new", "accounts", "accounts_card",
        "accounts_new", "rivals", "rivals_move", "rivals_cut", "rivals_blocked", "rivals_new", "rivals_tycoons", "standing", "standing_new"
    };

    public static UtilitiesWords FinanceWordsOf(UiWords t) => UtilitiesWordsOf(t);

    private static string FM(float money) => UtilitiesMoney(money);

    public static FinanceView FinanceViewOf(string state)
    {
        var v = new FinanceView();
        string tab = state.Split('_')[0];
        v.Tab = tab == "credit" ? FinanceFace.Credit : tab == "borrow" ? FinanceFace.Borrow : tab == "cash" ? FinanceFace.CashFlow : tab == "accounts" ? FinanceFace.Accounts
            : tab == "rivals" ? FinanceFace.Rivals : tab == "standing" ? FinanceFace.Standing : FinanceFace.Overview;
        v.Offer = state == "borrow_card" ? 4 : state == "borrow_finance" ? 6 : 0;
        v.Period = state == "cash_new" ? 1 : 2;
        v.Rival = state == "rivals_move" || state == "rivals_cut" || state == "rivals_blocked" || state == "rivals" ? 0 : state == "rivals_new" ? 1 : 0;
        v.Account = state == "accounts_card" ? "acct:2" : "acct:1";
        v.Standing = "wealth";
        return v;
    }

    public static FinanceFacts FinanceFactsOf(UiWords t, string state)
    {
        UtilitiesWords w = FinanceWordsOf(t);
        bool fresh = state.EndsWith("_new", StringComparison.Ordinal);
        var f = new FinanceFacts { Ready = true, Day = fresh ? "Day 5" : "Day 289", RivalsCount = fresh ? 0 : 8 };
        f.Score = fresh
            ? new FinanceScore { Value = 640, Min = 300, Max = 850, Threshold = 580, Grade = "Fair", Tone = UiRole.Warn, Lends = true, Halves = 6, HasChange = false,
                Sentence = "Banks lend from 580. A short file keeps a rating in the middle: pay what is due on time and it climbs." }
            : state == "borrow_refused" ? new FinanceScore { Value = 540, Min = 300, Max = 850, Threshold = 580, Grade = "Poor", Tone = UiRole.Danger, Lends = false, Halves = 3, HasChange = true, Change = -22, ChangeDays = 30,
                Sentence = "Lending stops under 580. Paying what is overdue brings it back." }
            : new FinanceScore { Value = 742, Min = 300, Max = 850, Threshold = 580, Grade = "Very good", Tone = UiRole.Ok, Lends = true, Halves = 8, HasChange = true, Change = 12, ChangeDays = 30,
                Sentence = "Three banks lend at their best rates from 700. Paying the card down moves you towards Excellent. No bank lends under 580." };
        string tab = state.Split('_')[0];
        if (tab == "overview") { Overview(f, w, fresh, state == "overview_long"); }
        else if (tab == "credit") { Credit(f, w, fresh); }
        else if (tab == "borrow") { Borrow(f, w, state); }
        else if (tab == "cash") { Cash(f, w, fresh); }
        else if (tab == "accounts") { Accounts(f, w, state, fresh); }
        else if (tab == "rivals") { Rivals(f, w, state); }
        else { Standing(f, w, fresh); }
        return f;
    }

    private static UiChartModel FinanceLine(string title, string[] days, float[] values, UiRole tint = UiRole.None)
    {
        return FinanceRules.Chart(title, new[] { title }, new[] { values }, Array.ConvertAll(days, d => int.Parse(d, CultureInfo.InvariantCulture)), values.Length, FM,
            "Not enough days yet", "The chart fills in as days pass.");
    }

    private static float[] Walk(int n, float start, float step, int seed)
    {
        var v = new float[n];
        float x = start;
        for (int i = 0; i < n; i++)
        {
            x += step * (1f + 0.6f * (float)Math.Sin(i * 1.7 + seed));
            v[i] = (float)Math.Round(x);
        }
        return v;
    }

    private static string[] Days(int last, int n)
    {
        var d = new string[n];
        for (int i = 0; i < n; i++)
        {
            d[i] = (last - n + 1 + i).ToString(CultureInfo.InvariantCulture);
        }
        return d;
    }

    private static void Overview(FinanceFacts f, UtilitiesWords w, bool fresh, bool longName)
    {
        FinanceOverviewFacts o = f.Overview;
        o.Player = longName ? "Alexandria Montgomery-Featherstonehaugh" : fresh ? "Maya Okafor" : "Maya Okafor";
        o.Cash = FM(fresh ? 24850f : 1204300f);
        o.Owed = FM(fresh ? 0f : 2343500f);
        o.Worth = FM(fresh ? 61200f : 1966000f);
        o.EconomyApp = true;
        o.LeavesNote = fresh ? null : "Leaves tomorrow " + FM(1772) + ", four daily payments.";
        o.RiseNote = fresh ? null : "Your variable debts cost " + FM(3600) + " more a month after the last rise, on day 281.";
        o.CityLabels = new[] { "Bank rate", "Prices", "The economy" };
        o.CityValues = new[] { "4.00%", "6.0% above last year", "Slowdown, month 3" };
        o.BestOn = true;
        o.BestName = "Hudson Trust";
        o.BestLine = "up to $400,000 at 5.2%";
        o.Cash30 = fresh
            ? FinanceLine("Cash", Days(5, 5), new[] { 25000f, 24100f, 26300f, 24850f, 24850f })
            : FinanceLine("Cash, last 30 days", Days(289, 30), Walk(30, 1100000f, 4200f, 2));
        if (fresh)
        {
            o.Debts = FinanceKitTables.Make(FinanceKitTables.DebtColumns(w), new List<FinanceRow>(), 0, "No debts", string.Empty, UiIcon.Bank);
            o.NextLabels = new[] { "Rent", "Wages", "Rate review" };
            o.NextValues = new[] { FM(350), FM(1200), "Day 30" };
            return;
        }
        var rows = new List<FinanceRow>
        {
            DebtRow("Business loan, variable", "Hudson Trust", 1200000f, "6.4%", 877f, "1,800 days"),
            DebtRow("Mortgage, 7 5th Avenue", "Hudson Trust", 780000f, "4.8%", 363f, "3,000 days"),
            DebtRow("Bloom & Co business loan", "Empire Federal", 286500f, "5.4%", 361f, "900 days"),
            DebtRow("Car finance, blue coupe", "Empire Federal", 64200f, "6.1%", 171f, "400 days"),
            DebtRow("Credit card", "Metropolitan Savings", 12800f, "17.9%", 384f, "Statement day 297")
        };
        if (longName)
        {
            rows[0].Name = "Business loan against Speedy Bites, Garment District flagship";
        }
        o.Debts = FinanceKitTables.Make(FinanceKitTables.DebtColumns(w), rows, -1, "No debts", string.Empty, UiIcon.Bank);
        o.Donut = new[] { 63.4f, 33.3f, 2.7f, 0.5f };
        o.DonutLabels = new[] { "Business loans", "Mortgage", "Car finance", "Credit card" };
        o.DonutValues = new[] { "63.4%", "33.3%", "2.7%", "0.5%" };
        o.NextLabels = new[] { "Day 290, four daily payments", "Day 297, card minimum", "Rent", "Wages", "Rate review" };
        o.NextValues = new[] { FM(1772), FM(384), FM(2840), FM(7880), "Day 291, +0.25" };
    }

    private static FinanceRow DebtRow(string name, string lender, float owed, string rate, float perDay, string left)
    {
        return new FinanceRow { Name = name, Sub = lender, Icon = UiIcon.Bank, Cells = new[] { FM(owed), rate, FM(perDay), left } };
    }

    private static void Credit(FinanceFacts f, UtilitiesWords w, bool fresh)
    {
        FinanceCreditFacts c = f.Credit;
        c.Factors = new[]
        {
            Factor("Payment history", "40%", fresh ? 40 : 94, fresh ? "No payments yet" : "Excellent", fresh ? "You have made no payments yet, so this waits for your first." : "9 of 9 daily payments taken in full this month.", "Pay every loan and card on its day."),
            Factor("What you owe", "25%", fresh ? 100 : 61, fresh ? "Excellent" : "Fair", fresh ? "You owe nothing against what banks would lend." : "Your debts use 61% of what the three banks would lend you.", "Pay the card down."),
            Factor("Length of history", "15%", fresh ? 8 : 71, fresh ? "Poor" : "Good", fresh ? "Your file is 5 days old; it counts in full at 300." : "Your first loan is 214 days old; it counts in full at 300.", "It grows by itself with each day."),
            Factor("Legal record", "10%", 100, "Clean", "No conviction, fine or court debt on your file.", null),
            Factor("Recent applications", "10%", fresh ? 100 : 54, fresh ? "None" : "2 in 90 days", fresh ? "No applications in the last 90 days." : "The car finance and the card count a little.", "Wait before you apply again.")
        };
        c.Tips = fresh ? new[] { "Take a small loan and pay it on time: payment history is the heaviest part of the score." }
            : new[] { "Paying the card down moves you towards Excellent (780).", "Wait 30 days before the next application." };
        c.Worst = fresh ? null : "The card is 75% used. Bring it under half.";
        c.OffersTitle = "Offers at your score";
        c.Offers = FinanceKitTables.Make(FinanceKitTables.OfferColumns(w, true), OfferRows(fresh), 0, "No offers", "A bank lends from 580.", UiIcon.Bank);
        var ev = new List<FinanceRow>();
        if (!fresh)
        {
            ev.Add(new FinanceRow { Name = "Payment taken on time", Cells = new[] { "Day 288", "+2" }, Values = new[] { 0, 1 } });
            ev.Add(new FinanceRow { Name = "Card statement paid late", Cells = new[] { "Day 281", "-12" }, Values = new[] { 0, -1 } });
            ev.Add(new FinanceRow { Name = "New credit: car finance", Cells = new[] { "Day 262", "-4" }, Values = new[] { 0, -1 } });
        }
        c.Events = FinanceKitTables.Make(FinanceKitTables.EventColumns(w), ev, -1, "Nothing yet", "Events appear as you borrow and pay.", UiIcon.Star);
    }

    private static FinanceFactor Factor(string name, string weight, int part, string word, string stands, string tip)
    {
        return new FinanceFactor { Name = name, Weight = weight, Part = part, Word = word, Stands = stands, Tip = tip, Tone = part >= 85 ? UiRole.Ok : part >= 40 ? UiRole.Warn : UiRole.Danger };
    }

    private static List<FinanceRow> OfferRows(bool fresh)
    {
        var rows = new List<FinanceRow>
        {
            new FinanceRow { Name = "Hudson Trust", Sub = "Up to $400,000, fee 1%", Icon = UiIcon.Bank, Cells = new[] { "5.2%" } },
            new FinanceRow { Name = "Empire Federal", Sub = "Up to $320,000, fee 0.8%", Icon = UiIcon.Bank, Cells = new[] { "5.6%" } },
            new FinanceRow { Name = "Metropolitan Savings", Sub = "Up to $180,000, fee 0.6%", Icon = UiIcon.Bank, Cells = new[] { "5.9%" } },
            new FinanceRow { Name = "Overdraft", Sub = "Limit $120,000", Icon = UiIcon.Wallet, Cells = new[] { "7.4%" } },
            new FinanceRow { Name = "Credit card", Sub = "Limit $60,000", Icon = UiIcon.Coin, Cells = new[] { "17.9%" } }
        };
        if (fresh)
        {
            rows.RemoveRange(1, 2);
            rows[0].Sub = "Up to $20,000, fee 1%";
        }
        return rows;
    }

    private static void Borrow(FinanceFacts f, UtilitiesWords w, string state)
    {
        FinanceBorrowFacts b = f.Borrow;
        if (state == "borrow_refused")
        {
            b.Refused = "Your rating is 540, Poor. Lending stops under 580.";
            b.RefusedTips = new[] { "Pay what is overdue first: it is the fastest way to move payment history.", "Do not apply again for 30 days." };
        }
        var list = OfferRows(false);
        list.Add(new FinanceRow { Name = "Mortgage", Sub = "Deposit 10 to 30%", Icon = UiIcon.Home, Cells = new[] { "4.8%" } });
        list.Add(new FinanceRow { Name = "Car finance", Sub = "From $5,000", Icon = UiIcon.Car, Cells = new[] { "6.1%" } });
        b.List = FinanceKitTables.Make(FinanceKitTables.OfferColumns(w, false), list, state == "borrow_card" ? 4 : state == "borrow_finance" ? 6 : 0, "No offers", string.Empty, UiIcon.Bank);
        b.FeedTitle = "Others are borrowing too";
        b.Feed = FinanceKitTables.Make(FinanceKitTables.FeedColumns(w), new List<FinanceRow>
        {
            new FinanceRow { Name = "Karen Aerospace & Motors", Sub = "took a loan at Empire Federal", Cells = new[] { FM(1800000), "285" }, Hue = 28f },
            new FinanceRow { Name = "Luthor Everything Co.", Sub = "took a loan at Hudson Trust", Cells = new[] { FM(640000), "281" }, Hue = 260f },
            new FinanceRow { Name = "Pens of Poets", Sub = "took a loan at Hudson Trust", Cells = new[] { FM(210000), "274" }, Hue = 150f }
        }, -1, "Nobody else has borrowed yet", string.Empty, UiIcon.People);
        if (state == "borrow_refused")
        {
            return;
        }
        var q = new FinanceQuoteFacts();
        b.Quote = q;
        if (state == "borrow_card")
        {
            q.Name = "Credit card";
            q.Kind = "Metropolitan Savings";
            q.Rate = "17.9% a year";
            q.Big = "Limit $60,000";
            q.BigNote = "Interest is charged on what you draw, not on the limit.";
            q.Labels = new[] { "Limit", "Rate", "Statement every", "Minimum payment", "Cash fee" };
            q.Values = new[] { FM(60000), "17.9%", "28 days", "3%, at least $25", "3%, at least $10" };
            q.Note = "The card is yours to draw on once it is open. A late minimum counts against your rating.";
            q.TakeLabel = "Open the card";
            q.CanTake = true;
            return;
        }
        if (state == "borrow_finance")
        {
            q.Name = "Car finance";
            q.Kind = "Empire Federal";
            q.Rate = "6.1% a year";
            q.Big = "$171 a day";
            q.HasAmount = true;
            q.Amount = 64200;
            q.Least = 5000;
            q.Most = 120000;
            q.AmountHint = "$5,000 to $120,000";
            q.Steps = new[]
            {
                new UtilitiesStep { Label = "Deposit", Value = "20%", Hint = "10 to 30%", CanLess = true, CanMore = true },
                new UtilitiesStep { Label = "Term", Value = "400 days", Hint = "200 to 800 days", CanLess = true, CanMore = true }
            };
            q.StepMoves = new[] { new FinanceStep { Which = FinanceFace.StepDeposit, Prev = 1, Next = 3 }, new FinanceStep { Which = FinanceFace.StepTerm, Prev = 1, Next = 3 } };
            q.Labels = new[] { "Car price", "Your deposit", "First payment", "Interest over the term" };
            q.Values = new[] { FM(80250), FM(16050), FM(171), FM(6880) };
            q.Note = "Approval stands for 7 days. The car is the security.";
            q.TakeLabel = "Approve";
            q.CanTake = true;
            return;
        }
        q.Name = "Hudson Trust";
        q.Kind = "Loan by phone";
        q.Rate = "5.2% a year";
        q.Big = "$435 a day";
        q.BigNote = "A phone loan has a 1% arrangement fee; at the counter there is none.";
        q.HasAmount = true;
        q.Amount = 250000;
        q.Least = 5000;
        q.Most = 400000;
        q.AmountHint = "$5,000 to $400,000";
        q.Labels = new[] { "You borrow", "Arrangement fee, 1%", "You receive", "Interest over 600 days", "Repayments, $435 a day", "All you repay" };
        q.Values = new[] { FM(250000), FM(2500), FM(247500), FM(11000), FM(261000), FM(261000) };
        q.Note = "Repay early at any time, free. This adds $435 to the $1,772 that already leave each day.";
        q.Note2 = "Your rating takes 0.4 points off this bank's best rate.";
        q.TakeLabel = "Take it";
        q.CanTake = true;
        float[] owing = new float[16];
        for (int i = 0; i < owing.Length; i++)
        {
            owing[i] = 250000f * (1f - i / 15f);
        }
        q.Owing = FinanceLine("What you still owe, day by day", Days(300, 16), owing);
    }

    private static void Cash(FinanceFacts f, UtilitiesWords w, bool fresh)
    {
        FinanceCashFacts c = f.Cash;
        c.Periods = new[] { "Yesterday", "Last 7 days", "Last 30 days" };
        c.ExportLabel = "Export";
        var st = new UiStatementModel
        {
            Account = "Cash flow", AccountIcon = UiIcon.Wallet, BalanceLabel = "Cash", Balance = FM(fresh ? 24850f : 1204300f), PeriodLabel = fresh ? "Last 7 days" : "Last 30 days",
            Columns = new[] { "When", "What", "In", "Out", "Balance" }, EmptyTitle = "No movements yet", EmptyBody = "Money in and out will be listed here day by day."
        };
        if (fresh)
        {
            st.Groups = new[]
            {
                new UiStatementGroup { Label = "Day 5, today", Total = FM(-350), TotalGood = false, Lines = new[]
                {
                    new UiStatementLine { When = "Day 5", Icon = UiIcon.Coin, What = "Rent, Garment District", Out = FM(350), Balance = FM(24850) },
                    new UiStatementLine { When = "Day 5", Icon = UiIcon.Coin, What = "Takings, Bloom Cafe", In = FM(1200), Balance = FM(25200) }
                } },
                new UiStatementGroup { Label = "Day 4", Total = FM(-1100), TotalGood = false, Lines = new[]
                {
                    new UiStatementLine { When = "Day 4", Icon = UiIcon.Coin, What = "Wages", Out = FM(1100), Balance = FM(24000) }
                } }
            };
            st.TotalLabels = new[] { "In", "Out" };
            st.Totals = new[] { FM(1200), FM(1450) };
            c.Chart = FinanceLine("Cash", Days(5, 5), new[] { 25000f, 24100f, 26300f, 24850f, 24850f });
            c.ChartTitle = "Cash, 5 days";
            c.ChartTotal = FM(24850);
            c.ChartChange = "-" + FM(150) + " in 5 days";
            c.ChartRange = "Day 1 to 5";
            c.ChangeGood = false;
            c.Reach = "The game keeps movements from day 1.";
        }
        else
        {
            st.Groups = new[]
            {
                new UiStatementGroup { Label = "Day 289, today", Total = FM(-2090), TotalGood = false, Lines = new[]
                {
                    new UiStatementLine { When = "08:00", Icon = UiIcon.Coin, What = "Takings so far, Speedy Bites", Where = "Breakfast trade", In = FM(4860), Balance = FM(1204300) },
                    new UiStatementLine { When = "06:00", Icon = UiIcon.Lightning, What = "Power and water, 7 5th Avenue", Where = "City Utilities", Out = FM(318), Balance = FM(1199440) },
                    new UiStatementLine { When = "06:00", Icon = UiIcon.Bank, What = "Loan payments, four daily", Where = "Hudson Trust, Empire Federal", Out = FM(1772), Balance = FM(1199758) }
                } },
                new UiStatementGroup { Label = "Day 288", Total = FM(12430), TotalGood = true, Lines = new[]
                {
                    new UiStatementLine { When = "23:00", Icon = UiIcon.Person, What = "Wages, all businesses", Where = "27 people", Out = FM(7880), Balance = FM(1201530) },
                    new UiStatementLine { When = "23:00", Icon = UiIcon.Store, What = "Takings, Bloom & Co", Where = "Garment District", In = FM(6310), Balance = FM(1209410) },
                    new UiStatementLine { When = "23:00", Icon = UiIcon.Store, What = "Takings, Speedy Bites", Where = "7 5th Avenue", In = FM(18420), Balance = FM(1203100) }
                } }
            };
            st.TotalLabels = new[] { "In", "Out" };
            st.Totals = new[] { FM(63055), FM(30580) };
            st.ChangeGood = true;
            c.Chart = FinanceLine("Cash", Days(289, 30), Walk(30, 1100000f, 3500f, 3));
            c.ChartTitle = "Cash, 30 days";
            c.ChartTotal = FM(1204300);
            c.ChartChange = "+" + FM(88420) + " in 30 days";
            c.ChartRange = "Day 260 to 289";
            c.ChangeGood = true;
            c.Reach = "The game keeps movements from day 283.";
        }
        c.Statement = st;
        c.WhereTitle = "Where it went, 30 days";
        c.WhereNote = fresh ? "Not yet before day 2: the ledger holds 4 of 30 days." : null;
        c.CameInLabel = "More came in than went out";
        c.CameIn = fresh ? "+" + FM(1200) : "+" + FM(568480);
        c.InNames = fresh ? new[] { "Takings" } : new[] { "Takings", "Dividends and interest" };
        c.InValues = fresh ? new[] { FM(1200) } : new[] { FM(562300), FM(6180) };
        c.InShare = fresh ? new[] { 100 } : new[] { 99, 1 };
        c.WentOutLabel = "More went out than came in";
        c.WentOut = fresh ? "-" + FM(1450) : "-" + FM(480060);
        c.OutNames = fresh ? new[] { "Wages", "Rent" } : new[] { "Wages", "Stock", "Loan payments", "Rent", "Bills" };
        c.OutValues = fresh ? new[] { FM(1100), FM(350) } : new[] { FM(236400), FM(141200), FM(53160), FM(42660), FM(6640) };
        c.OutShare = fresh ? new[] { 76, 24 } : new[] { 100, 60, 22, 18, 3 };
        c.NetLabel = "Cash changed by";
        c.Net = fresh ? "-" + FM(250) : "+" + FM(88420);
        c.NetGood = !fresh;
        c.ComingTitle = "Coming up";
        c.ComingLabels = fresh ? new[] { "Day 7, rent", "Day 8, wages" } : new[] { "Day 291, rent, Bloom & Co unit", "Day 292, wages, all businesses", "Day 297, card statement, minimum" };
        c.ComingValues = fresh ? new[] { FM(350), FM(1100) } : new[] { FM(2840), FM(7880), FM(384) };
    }

    private static void Accounts(FinanceFacts f, UtilitiesWords w, string state, bool fresh)
    {
        FinanceAccountsFacts a = f.Accounts;
        a.Headline = fresh ? "Cash " + FM(24850) + ", you owe nothing" : "Cash " + FM(1204300) + ", you owe " + FM(2343500);
        var rows = new List<FinanceRow>();
        var ids = new List<string>();
        if (!fresh)
        {
            rows.Add(new FinanceRow { Name = "Business loan, Hudson Trust", Sub = "Loan", Icon = UiIcon.Bank, Cells = new[] { FM(1200000), "6.4%", FM(877), "In order" }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Ok } });
            ids.Add("acct:1");
            rows.Add(new FinanceRow { Name = "Credit card, Metropolitan Savings", Sub = "Credit card", Icon = UiIcon.Coin, Cells = new[] { FM(12800), "17.9%", "Minimum " + FM(384), "Statement day 297" }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Warn } });
            ids.Add("acct:2");
            rows.Add(new FinanceRow { Name = "Mortgage, 7 5th Avenue", Sub = "Mortgage", Icon = UiIcon.Home, Cells = new[] { FM(780000), "4.8%", FM(363), "In order" }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.Ok } });
            ids.Add("acct:3");
        }
        rows.Add(new FinanceRow { Name = "Ways out of debt", Sub = "Arrange, or start again", Icon = UiIcon.Scales, Cells = new[] { string.Empty, string.Empty, string.Empty, string.Empty } });
        ids.Add("ways");
        a.Table = FinanceKitTables.Make(FinanceKitTables.AccountColumns(w), fresh ? new List<FinanceRow>() : rows, 0, "No accounts", "A loan or a card will be listed here once you take one.", UiIcon.Wallet);
        a.Ids = fresh ? new string[0] : ids.ToArray();
        if (fresh)
        {
            return;
        }
        bool card = state == "accounts_card";
        a.CardPay = true;
        a.PayAmount = card ? 384 : 877;
        a.PayLeast = 1;
        a.PayMost = card ? 12800 : 1200000;
        a.PayPrev = 1;
        a.PayNext = a.PayMost;
        a.Card = new UtilitiesCard
        {
            Title = card ? "Credit card, Metropolitan Savings" : "Business loan, Hudson Trust",
            Chip = "In order",
            ChipTone = UiRole.Ok,
            Sub = card ? "Opened day 281" : "Opened day 214",
            Lines = card
                ? new[]
                {
                    new UtilitiesLine { Label = "You owe", Value = FM(12800) + " of " + FM(60000) },
                    new UtilitiesLine { Label = "Rate", Value = "17.9%" },
                    new UtilitiesLine { Label = "Next payment", Value = "Minimum " + FM(384) + " by day 297" },
                    new UtilitiesLine { Value = "Pay the statement in full and the card costs nothing.", Ink = UiRole.Text2 }
                }
                : new[]
                {
                    new UtilitiesLine { Label = "You owe", Value = FM(1200000) },
                    new UtilitiesLine { Label = "Rate", Value = "6.4%" },
                    new UtilitiesLine { Label = "A day", Value = FM(877) },
                    new UtilitiesLine { Value = "Secured on Speedy Bites. Pay it early at any time, free.", Ink = UiRole.Text2 }
                },
            Actions = new[]
            {
                new UiActionModel { Label = "Draw cash", Kind = UiButtonKind.Secondary, Icon = UiIcon.Coin, Key = UiAction.Count, Command = FinanceFace.CardAct, Arg = (int)FinanceAct.Draw },
                new UiActionModel { Label = "Pay off", Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count, Command = FinanceFace.CardAct, Arg = (int)FinanceAct.PayOff },
                new UiActionModel { Label = "Pay", Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = FinanceFace.CardAct, Arg = (int)FinanceAct.Pay }
            }
        };
        if (!card)
        {
            a.Card.Actions = new[] { a.Card.Actions[1], a.Card.Actions[2] };
        }
    }

    private static void Rivals(FinanceFacts f, UtilitiesWords w, string state)
    {
        FinanceRivalsFacts r = f.Rivals;
        bool fresh = state == "rivals_new";
        bool move = state == "rivals_move";
        bool live0 = move || state == "rivals_cut" || state == "rivals_blocked";
        r.Headline = fresh ? "You are 4th of 9. The next move comes by day 14." : "You are 4th of 9, with " + FM(19640000) + ". Huang Guo is ahead.";
        var rows = new List<FinanceRow>
        {
            RichRow("Huang Guo", "Burger Barn", 31420000f, "+2.1%", 1, new[] { 24f, 25f, 25.5f, 27f, 28f, 30f, 31.4f }, 28f),
            RichRow("Imani Brooks", "Brooks Fresh Markets", 27880000f, "+1.4%", 1, new[] { 26f, 25f, 27f, 26f, 27f, 27.5f, 27.9f }, 260f),
            RichRow("Viktor Sorensen", "Sorensen Cold Storage", 22510000f, "-0.8%", -1, new[] { 23f, 24f, 23.5f, 23f, 22.8f, 22.6f, 22.5f }, 190f),
            RichRow("Maya Okafor (you)", "Speedy Bites, Bloom & Co", 19640000f, "+3.2%", 1, new[] { 15f, 16f, 17f, 17.5f, 18f, 19f, 19.6f }, 330f),
            RichRow("Kwame Mensah", "Mensah Auto Group", 17960000f, "+0.6%", 1, new[] { 17f, 17.5f, 17.2f, 17.4f, 17.6f, 17.9f, 17.96f }, 45f)
        };
        bool tycoons = state == "rivals_tycoons";
        if (tycoons)
        {
            // The rich list's own people, so their painted faces (UiPortraits' named looks) are in the picture.
            rows = new List<FinanceRow>
            {
                RichRow("Space Karen", "Karen Aerospace & Motors", 40000000f, "+2.4%", 1, new[] { 30f, 32f, 33f, 35f, 36f, 38f, 40f }, 200f),
                RichRow("Bald Luthor", "Luthor Everything Co.", 38200000f, "+0.9%", 1, new[] { 36f, 37f, 36.5f, 37f, 37.5f, 38f, 38.2f }, 100f),
                RichRow("The Zucc", "Zucc Social Holdings", 36900000f, "-1.2%", -1, new[] { 39f, 38.5f, 38f, 37.5f, 37.2f, 37f, 36.9f }, 130f),
                RichRow("The Oracle of Omaha", "Berkshire-like holding", 29400000f, "+0.4%", 1, new[] { 28f, 28.5f, 28.8f, 29f, 29.1f, 29.3f, 29.4f }, 40f),
                RichRow("Chair-Leap Bill", "Soft Wares Ltd", 23100000f, "+1.1%", 1, new[] { 21f, 21.5f, 22f, 22.4f, 22.7f, 23f, 23.1f }, 300f),
                RichRow("Yacht Larry", "Sunset Marine", 21000000f, "-0.3%", -1, new[] { 21.4f, 21.3f, 21.2f, 21.1f, 21.1f, 21f, 21f }, 180f),
                RichRow("Maya Okafor (you)", "Speedy Bites, Bloom & Co", 19640000f, "+3.2%", 1, new[] { 15f, 16f, 17f, 17.5f, 18f, 19f, 19.6f }, 330f),
                RichRow("Rocket Richard", "Launchpad Capital", 17800000f, "+0.6%", 1, new[] { 17f, 17.2f, 17.4f, 17.5f, 17.6f, 17.7f, 17.8f }, 20f)
            };
        }
        if (fresh)
        {
            rows = new List<FinanceRow>
            {
                RichRow("Huang Guo", "Burger Barn", 31420000f, string.Empty, 0, null, 28f),
                RichRow("Imani Brooks", "Brooks Fresh Markets", 27880000f, string.Empty, 0, null, 260f),
                RichRow("Maya Okafor (you)", "Bloom Cafe", 61200f, string.Empty, 0, null, 330f)
            };
        }
        r.Table = FinanceKitTables.Make(FinanceKitTables.RichColumns(w), rows, fresh ? 1 : 0, "Nobody yet", string.Empty, UiIcon.Trophy);
        r.Who = fresh ? new[] { 0, 1, -1 } : tycoons ? new[] { 0, 1, 2, 3, 4, 5, -1, 6 } : new[] { 0, 1, 2, -1, 3 };
        string name = tycoons ? "Space Karen" : fresh ? "Imani Brooks" : "Huang Guo";
        r.Profile = new UiProfileModel
        {
            Name = name, Hue = tycoons ? 200f : fresh ? 260f : 28f, Role = tycoons ? "Karen Aerospace & Motors" : fresh ? "Brooks Fresh Markets" : "Burger Barn", RoleIcon = UiIcon.Store, Status = live0 ? "Making a move" : "Quiet",
            StatusTone = live0 ? UiRole.Warn : UiRole.Muted, FactLabels = new[] { "Worth", "In a week" }, FactValues = new[] { FM(tycoons ? 40000000 : fresh ? 27880000 : 31420000), fresh ? string.Empty : "+" + FM(640000) },
            Rating = -1
        };
        r.Labels = new[] { "Their holding", "Your shares in it" };
        r.Values = new[] { fresh ? "Brooks Fresh Markets" : "Burger Barn (HSF)", "0 of 120,000" };
        r.MovesTitle = "Their latest moves";
        r.MovesNone = "No moves yet.";
        r.Moves = fresh ? new FinanceRivalMove[0] : new[]
        {
            new FinanceRivalMove { Day = "Day 287", What = live0 ? "Raising prices 4% in all shops" : "Bought up buildings in Midtown", Live = live0 },
            new FinanceRivalMove { Day = "Day 281", What = "Borrowed $1,800,000" },
            new FinanceRivalMove { Day = "Day 277", What = "Bought 8,000 HSF shares" }
        };
        bool cutOn = state == "rivals_cut", blocked = state == "rivals_blocked";
        bool live = move || cutOn || blocked;
        r.Reading = live ? "Huang Guo is raising prices 4% in all shops. About 4% fewer customers at your shops while their prices are up. It runs until day 301." : null;
        r.AnswersTitle = "How do you answer?";
        r.Answers = !live ? new FinanceAnswer[0] : new[]
        {
            blocked
                ? new FinanceAnswer { Title = "Run a Flare campaign at Speedy Bites", Hint = "Not now: a campaign is booked there already.", Act = FinanceAct.Campaign, On = false }
                : new FinanceAnswer { Title = "Run a Flare campaign at Speedy Bites", Hint = "Influencers post for 7 days. Costs $4,200. Forecast: 12% more customers, about $15,400 more takings.", Act = FinanceAct.Campaign, On = true },
            cutOn
                ? new FinanceAnswer { Title = "End the price cut and hold your prices", Hint = "The cut runs until day 301. Ending it takes off only the holds it put on.", Act = FinanceAct.HoldPrices, On = true }
                : blocked
                    ? new FinanceAnswer { Title = "Cut your prices 5% for 14 days", Hint = "Not now: none of your pricing plans has a price the plan sets.", Act = FinanceAct.CutPrices, On = false }
                    : new FinanceAnswer { Title = "Cut your prices 5% for 14 days", Hint = "Your own shops' prices go down and the plans it moved are held. They go back by themselves at the end. Forecast: about 3 more customers a day while it lasts.", Act = FinanceAct.CutPrices, On = true },
            new FinanceAnswer { Title = "Outbid them for the building", Hint = "Costs $210,000 and stops the move. They may try again in 20 days.", Act = FinanceAct.Counter, Arg = 0, On = true },
            new FinanceAnswer { Title = "Lobby the council back", Hint = "Not now: they are resting.", Act = FinanceAct.Counter, Arg = 3, On = false }
        };
        r.CanFollow = true;
        r.FollowLabel = "Follow " + name;
        r.CanHolding = !fresh;
        r.HoldingLabel = "Open HSF in Stocks";
        r.ListLabel = "The rich list in the paper";
        var days = Days(289, 30);
        var rival = Walk(30, 24000000f, 250000f, 5);
        var you = Walk(30, 15000000f, 150000f, 1);
        int[] chartDays = Array.ConvertAll(fresh ? Days(5, 5) : days, d => int.Parse(d, CultureInfo.InvariantCulture));
        r.Chart = fresh
            ? FinanceRules.Chart("Who is catching whom", new[] { name, "You" }, new[] { new[] { 27400000f, 27500000f, 27650000f, 27800000f, 27880000f }, new[] { 60000f, 60500f, 61000f, 61200f, 61200f } }, chartDays, 5, FM, "Not enough days yet", string.Empty)
            : FinanceRules.Chart("Who is catching whom", new[] { name, "You" }, new[] { rival, you }, chartDays, rival.Length, FM, "Not enough days yet", string.Empty);
    }

    private static FinanceRow RichRow(string name, string holding, float worth, string change, int dir, float[] line, float hue)
    {
        return new FinanceRow
        {
            Name = name, Sub = holding, Hue = hue, Cells = new[] { FM(worth), string.Empty, change }, Values = new[] { 0, 0, dir }, Line = line,
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None }
        };
    }

    private static void Standing(FinanceFacts f, UtilitiesWords w, bool fresh)
    {
        FinanceStandingFacts s = f.Standing;
        s.Profile = fresh ? "Unknown" : "Well known";
        s.OverallValue = fresh ? 50 : 68;
        s.Overall = fresh ? "Nobody knows you yet. What you do in the first weeks sets how the city sees you." : "People know your name, and most of what they have heard is good.";
        s.PartNames = new[] { "Integrity", "Wages", "Quality", "Giving back" };
        s.PartWords = fresh ? new[] { "Neutral", "Neutral", "Neutral", "Neutral" } : new[] { "Good", "Fair", "Very good", "Fair" };
        s.PartMarks = fresh ? new[] { 50, 50, 50, 50 } : new[] { 74, 52, 81, 49 };
        s.PartNotes = new[]
        {
            "How honest you are with the city: promises kept, fair dealing, no scandal.",
            "How well you pay and treat the people who work for you.",
            "How good your shops are, as the people who use them say.",
            "What you give back to the districts you do business in."
        };
        s.Table = FinanceKitTables.Make(FinanceKitTables.StandingColumns(w), new List<FinanceRow>
        {
            new FinanceRow { Name = "Wealth", Sub = "How rich you look", Icon = UiIcon.Coin, Cells = new[] { fresh ? "12" : "71", fresh ? "rising" : "steady" }, Tones = new[] { UiRole.Warn, UiRole.None } },
            new FinanceRow { Name = "Recognition", Sub = "Followers and press", Icon = UiIcon.Megaphone, Cells = new[] { fresh ? "4" : "58", fresh ? "rising" : "rising" }, Tones = new[] { UiRole.Warn, UiRole.None } },
            new FinanceRow { Name = "Midtown", Sub = "District opinion", Icon = UiIcon.MapPin, Cells = new[] { fresh ? "50" : "66", fresh ? "known by 2" : "known by 41" }, Tones = new[] { UiRole.None, UiRole.None } }
        }, 0, "Nothing yet", string.Empty, UiIcon.Flag);
        s.Ids = new[] { "wealth", "recognition", "district:0" };
        s.Card = new UtilitiesCard
        {
            Title = "Wealth",
            Sub = "How rich you look",
            Chip = fresh ? "Unknown" : "Well known",
            ChipTone = UiRole.Accent,
            Lines = new[]
            {
                new UtilitiesLine { Label = "Wealth", Value = fresh ? "12" : "71" },
                new UtilitiesLine { Value = fresh ? "Net worth $61,200, $1,200 a week, 1 business, no property." : "Net worth $1,966,000, $62,000 a week, 3 businesses, 2 properties.", Ink = UiRole.Text2 },
                new UtilitiesLine { Value = "Wealth is how rich you look to the city. It rises with your net worth and your weekly takings.", Ink = UiRole.Text2 }
            }
        };
    }
}
