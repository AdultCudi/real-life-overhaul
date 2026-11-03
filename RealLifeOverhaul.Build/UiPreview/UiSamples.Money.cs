using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static readonly string[] MoneyFinanceStates =
    {
        "overview", "overview_new", "overview_long", "credit", "borrow", "cash", "cash_new", "accounts", "rivals", "standing"
    };

    public static readonly string[] MoneyBankStates = { "accounts", "accounts_new", "loans", "card", "apply", "rating", "long" };

    public static readonly string[] MoneyOwnStates = { "bank", "bank_new", "savers", "lending", "fund", "branches", "long" };

    public static readonly string[] MoneyUtilStates = { "accounts", "accounts_new", "internet", "network", "long" };

    public static MoneyModel MoneyFinance(UiWords t, string state) => FinancePage(t, state);

    public static MoneyModel MoneyBanks(UiWords t, string state) => BanksPage(t, state);

    public static MoneyModel MoneyOwn(UiWords t, string state) => OwnPage(t, state);

    public static MoneyModel MoneyUtilities(UiWords t, string state) => UtilPage(t, state);

    private static string W(UiWords t, string key, string english) => t(key, english);

    private static UiPrompt[] Prompts(UiWords t, bool act)
    {
        var list = new UiPrompt[act ? 4 : 3];
        list[0] = new UiPrompt { Action = UiAction.Move, Label = W(t, "reallife_ui_prompt_move", "Move") };
        list[1] = new UiPrompt { Action = UiAction.Confirm, Label = W(t, "reallife_ui_prompt_select", "Select") };
        list[2] = new UiPrompt { Action = UiAction.Back, Label = W(t, "reallife_ui_prompt_back", "Back") };
        if (act)
        {
            list[3] = new UiPrompt { Action = UiAction.Alt, Label = W(t, "reallife_ui_prompt_choose", "Choose") };
        }
        return list;
    }

    private static UiNavEntry Nav(UiWords t, string key, string english, UiIcon icon)
    {
        return new UiNavEntry { Icon = icon, Tint = UiRole.None, Label = W(t, key, english) };
    }

    private static MoneyModel FinancePage(UiWords t, string state)
    {
        bool fresh = state.EndsWith("_new", StringComparison.Ordinal);
        bool longName = state == "overview_long";
        string tab = state.Replace("_new", string.Empty).Replace("_long", string.Empty);
        int index = tab == "credit" ? 1 : tab == "borrow" ? 2 : tab == "cash" ? 3 : tab == "accounts" ? 4 : tab == "rivals" ? 5 : tab == "standing" ? 6 : 0;
        var m = new MoneyModel
        {
            App = MoneyApp.Finance,
            Icon = UiIcon.Wallet,
            Title = W(t, "reallife_econ_app", "Finance"),
            Tab = index,
            Nav = new[]
            {
                Nav(t, "reallife_econ_tab_overview", "Overview", UiIcon.IdCard),
                Nav(t, "reallife_econ_tab_credit", "Credit", UiIcon.Star),
                Nav(t, "reallife_econ_tab_borrow", "Borrow", UiIcon.Bank),
                Nav(t, "reallife_econ_tab_cash", "Cash flow", UiIcon.ChartLineUp),
                Nav(t, "reallife_econ_tab_accounts", "Accounts", UiIcon.Wallet),
                Nav(t, "reallife_money_nav_rivals", "Rivals", UiIcon.Trophy),
                Nav(t, "reallife_fame_title", "Standing", UiIcon.Star)
            },
            Prompts = Prompts(t, index == 1 || index == 2),
            EmptyTitle = W(t, "reallife_money_empty_debts", "No debts yet"),
            EmptyBody = W(t, "reallife_money_empty_loans", "A loan or a card will be listed here."),
            EmptyIcon = UiIcon.Wallet
        };
        if (index == 0)
        {
            m.Shape = MoneyShape.Overview;
            m.Profile = Borrower(t, fresh, longName);
            m.Note = fresh ? null : W(t, "reallife_money_leaves", "Leaves tomorrow $1,772, four daily payments.");
            m.DonutTitle = W(t, "reallife_money_owed", "What you owe");
            m.Donut = fresh ? new float[0] : new[] { 62f, 18f, 12f, 8f };
            m.DonutLabels = new[] { "Loans", "Card", "Overdraft", "Other" };
            m.DonutValues = new[] { "$820,000", "$240,000", "$160,000", "$110,000" };
            m.Actions = new[] { Link(t) };
            m.Table = DebtTable(t, fresh);
        }
        else if (index == 1 || index == 2)
        {
            m.Shape = MoneyShape.Offer;
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.Bank, Tint = UiRole.None,
                Kicker = W(t, "reallife_econ_tab_borrow", "Borrow"),
                Title = longName ? "The city's largest lender, a long counter name" : "Largest lender",
                Line = "4.8% a year, 5 years",
                Note = "$186 a day",
                NoteIcon = UiIcon.Coin,
                Progress = -1
            };
            m.Amount = "$250,000";
            m.CanLess = true;
            m.CanMore = true;
            m.Note = W(t, "reallife_money_all", "All you repay is $339,000. Interest over the term is $89,000.");
            m.Note2 = W(t, "reallife_money_adds", "This adds $186 to the $1,772 that already leave each day.");
            m.Actions = new[]
            {
                new UiActionModel { Label = W(t, "reallife_money_take", "Take it"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand }
            };
            m.Table = MoneyFeedTable(t, fresh);
        }
        else if (index == 3 || index == 4)
        {
            m.Shape = MoneyShape.Flow;
            m.Chart = fresh ? null : LineChart(W(t, "reallife_money_where", "Cash over 30 days"), 12, 400000f, 900000f);
            m.Note = fresh ? null : W(t, "reallife_money_coming", "Coming up: rent on day 12, wages on day 14.");
            m.Statement = Statement(t, fresh, index == 4 ? "Everyday" : "Cash");
            m.Actions = new[]
            {
                new UiActionModel { Label = W(t, "reallife_money_export", "Export"), Kind = UiButtonKind.Secondary, Icon = UiIcon.ClipboardText, Key = UiAction.Option, Command = MoneyCommands.Export }
            };
        }
        else if (index == 5)
        {
            m.Shape = MoneyShape.League;
            m.Profile = TycoonHead(t, fresh);
            m.Choices = new[]
            {
                W(t, "reallife_money_cut", "Cut prices 5%"),
                W(t, "reallife_money_campaign", "Flare campaign"),
                W(t, "reallife_money_hold", "Hold prices")
            };
            m.ChoiceBody = new[]
            {
                W(t, "reallife_money_forecast", "About 40 more customers over the fortnight."),
                W(t, "reallife_money_forecast", "About 40 more customers over the fortnight."),
                W(t, "reallife_money_hold", "Hold your prices")
            };
            m.Chart = fresh ? null : LineChart(W(t, "reallife_money_worth", "Who is catching whom"), 12, 40f, 90f);
            m.Table = LeagueTable(t, fresh);
            m.Actions = new[]
            {
                new UiActionModel { Label = W(t, "reallife_money_follow", "Follow"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Megaphone, Key = UiAction.Option, Command = MoneyCommands.Follow }
            };
        }
        else
        {
            m.Shape = MoneyShape.Factors;
            m.Lines = new[] { "Payments", "What you owe", "History", "Legal", "Applications" };
            m.LineBody = new[]
            {
                "Paid on time lately.",
                "What you owe is a fair share of what you could borrow.",
                "The file is still young.",
                "No case open.",
                "Few applications in 90 days."
            };
            m.Marks = fresh ? new[] { 40, 40, 20, 80, 70 } : new[] { 82, 64, 48, 90, 76 };
            m.Note = W(t, "reallife_money_threshold", "Lending stops under 580.");
        }
        return m;
    }

    private static MoneyModel BanksPage(UiWords t, string state)
    {
        bool fresh = state == "accounts_new";
        bool longName = state == "long";
        int index = state.StartsWith("loans", StringComparison.Ordinal) ? 1 : state.StartsWith("card", StringComparison.Ordinal) ? 2
            : state.StartsWith("apply", StringComparison.Ordinal) ? 3 : state.StartsWith("rating", StringComparison.Ordinal) ? 4 : 0;
        var m = new MoneyModel
        {
            App = MoneyApp.Banks,
            Icon = UiIcon.Bank,
            Across = true,
            Title = W(t, "reallife_banks_app", "Banks"),
            Tab = index,
            Nav = new[]
            {
                Nav(t, "reallife_bank_tab_accounts", "Accounts", UiIcon.Wallet),
                Nav(t, "reallife_bank_tab_loans", "Loans", UiIcon.Bank),
                Nav(t, "reallife_bank_tab_card", "Card", UiIcon.IdCard),
                Nav(t, "reallife_bank_tab_apply", "Apply", UiIcon.Check),
                Nav(t, "reallife_bank_tab_rating", "Rating", UiIcon.Star)
            },
            Prompts = Prompts(t, index == 3),
            Profile = new UiProfileModel
            {
                Name = longName ? "The city's largest lender with a very long counter name" : "Largest lender",
                Hue = 200f,
                Role = "Rank 1",
                RoleIcon = UiIcon.Bank,
                Status = fresh ? "New" : "Account open",
                StatusTone = UiRole.Ok,
                FactLabels = new[] { "Rate", "Limit", "Since" },
                FactValues = new[] { "4.8%", "$500,000", "Day 40" },
                RatingLabel = W(t, "reallife_money_since", "Standing"),
                Rating = fresh ? 2 : 8
            },
            EmptyTitle = W(t, "reallife_money_empty_debts", "No debts yet"),
            EmptyBody = W(t, "reallife_money_empty_loans", "A loan or a card will be listed here."),
            EmptyIcon = UiIcon.Bank
        };
        if (index == 3)
        {
            m.Shape = MoneyShape.Apply;
            m.Amount = "$80,000";
            m.CanLess = true;
            m.CanMore = true;
            m.Meter = fresh ? 0 : 46;
            m.MeterLabel = W(t, "reallife_money_headroom", "Headroom");
            m.Lines = new[] { "Rating over 580", "Amount under the limit", "Nothing overdue", "Applications in 90 days", "34 days of takings, needs 30" };
            m.LineOn = new[] { !fresh, true, true, true, !fresh };
            m.LineBody = new[] { null, null, null, null, W(t, "reallife_money_cover", "Takings cover the payment 20 times.") };
            m.Note2 = W(t, "reallife_money_other_banks", "The same application at the other banks.");
            m.Actions = new[]
            {
                new UiActionModel { Label = W(t, "reallife_money_apply", "Apply"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand }
            };
            m.Table = OtherBanks(t);
        }
        else if (index == 2)
        {
            m.Shape = MoneyShape.Factors;
            m.Lines = new[] { "Limit", "Drawn", "Free", "Statement", "Minimum" };
            m.LineBody = new[] { "$20,000", "$4,400", "$15,600", "Day 18", "$220" };
            m.Note = fresh ? W(t, "reallife_money_empty_debts", "No debts yet") : null;
        }
        else if (index == 4)
        {
            m.Shape = MoneyShape.Factors;
            m.Lines = new[] { "Payments", "What you owe", "History", "Legal", "Applications" };
            m.LineBody = new[] { "Paid on time.", "A fair share.", "Still young.", "No case.", "Few applications." };
            m.Marks = new[] { 80, 60, 45, 90, 70 };
            m.Note = W(t, "reallife_money_threshold", "Lending stops under 580.");
        }
        else
        {
            m.Shape = MoneyShape.Flow;
            m.Statement = Statement(t, fresh, index == 1 ? "Loans" : "Accounts");
        }
        return m;
    }

    private static MoneyModel OwnPage(UiWords t, string state)
    {
        bool fresh = state == "bank_new";
        bool longName = state == "long";
        int index = state.StartsWith("savers", StringComparison.Ordinal) ? 1 : state.StartsWith("lending", StringComparison.Ordinal) ? 2
            : state.StartsWith("fund", StringComparison.Ordinal) ? 3 : state.StartsWith("branches", StringComparison.Ordinal) ? 4 : 0;
        var m = new MoneyModel
        {
            App = MoneyApp.OwnBank,
            Icon = UiIcon.PiggyBank,
            Title = W(t, "reallife_ob_app", "Your bank"),
            Tab = index,
            Nav = new[]
            {
                Nav(t, "reallife_ob_tab_bank", "Bank", UiIcon.Bank),
                Nav(t, "reallife_ob_tab_savers", "Savers", UiIcon.PiggyBank),
                Nav(t, "reallife_ob_tab_lending", "Lending", UiIcon.Coin),
                Nav(t, "reallife_ob_tab_fund", "Fund", UiIcon.ChartPie),
                Nav(t, "reallife_branch_tab", "Branches", UiIcon.Building)
            },
            Prompts = Prompts(t, false),
            EmptyTitle = W(t, "reallife_money_empty_debts", "No debts yet"),
            EmptyBody = W(t, "reallife_money_biggest", "Named savers and borrowers will be listed here."),
            EmptyIcon = UiIcon.PiggyBank
        };
        if (index == 3)
        {
            m.Shape = MoneyShape.Fund;
            m.Chart = fresh ? null : LineChart(W(t, "reallife_money_fund_days", "Fund over 30 days"), 12, 200000f, 480000f);
            m.DonutTitle = W(t, "reallife_ob_tab_fund", "Fund");
            m.Donut = fresh ? new float[0] : new[] { 35f, 65f };
            m.DonutLabels = new[] { "Cash reserve", "Shares" };
            m.DonutValues = new[] { "$168,000", "$312,000" };
            m.Table = NamedAccounts(t, fresh, false);
        }
        else if (index == 0)
        {
            m.Shape = MoneyShape.Home;
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.PiggyBank, Tint = UiRole.None,
                Kicker = longName ? "Your bank, a long trading name" : W(t, "reallife_ob_tab_bank", "Bank"),
                Title = fresh ? "Level 1" : "Level 2",
                Line = fresh ? "The next level is still ahead" : "Level 3 needs more deposits",
                Progress = fresh ? 20 : 64,
                Note = "Next level",
                NoteIcon = UiIcon.Flag
            };
            m.Figures = fresh ? new UiFigureModel[0] : new[]
            {
                new UiFigureModel { Label = "From borrowers", Value = "$42,000", Change = "+$4,200", Rising = true, Good = true },
                new UiFigureModel { Label = "To savers", Value = "$18,000", Change = "+$900", Rising = true, Good = false },
                new UiFigureModel { Label = "Branches", Value = "$6,400", Good = true },
                new UiFigureModel { Label = "Paid to you", Value = "$12,000", Change = "+$1,100", Rising = true, Good = true }
            };
            m.Lines = new[] { "Deposits in range", "Loans in range", "Branches in range" };
            m.LineOn = new[] { true, !fresh, true };
            m.Note = W(t, "reallife_money_month", "This month: interest in, interest out, branch costs, profit paid to you.");
        }
        else if (index == 4)
        {
            m.Shape = MoneyShape.Table;
            m.Table = BranchTable(t, fresh);
        }
        else
        {
            m.Shape = MoneyShape.Table;
            m.Table = NamedAccounts(t, fresh, index == 2);
        }
        return m;
    }

    private static MoneyModel UtilPage(UiWords t, string state)
    {
        bool fresh = state == "accounts_new";
        bool longName = state == "long";
        int index = state.StartsWith("internet", StringComparison.Ordinal) ? 1 : state.StartsWith("network", StringComparison.Ordinal) || state == "long" ? 2 : 0;
        var m = new MoneyModel
        {
            App = MoneyApp.Utilities,
            Icon = UiIcon.Globe,
            Across = true,
            Title = W(t, "reallife_util_app", "Utilities"),
            Tab = index,
            Nav = new[]
            {
                Nav(t, "reallife_util_tab_accounts", "Accounts", UiIcon.Wallet),
                Nav(t, "reallife_util_tab_internet", "Internet", UiIcon.Globe),
                Nav(t, "reallife_util_tab_network", "Your network", UiIcon.HardDrives)
            },
            Prompts = Prompts(t, index == 2),
            EmptyTitle = W(t, "reallife_money_empty_debts", "No debts yet"),
            EmptyBody = W(t, "reallife_money_coming", "Coming up: rent on day 12, wages on day 14."),
            EmptyIcon = UiIcon.Globe
        };
        if (index == 1)
        {
            m.Shape = MoneyShape.Plans;
            m.Catalogue = Plans(t, fresh);
        }
        else if (index == 2)
        {
            m.Shape = MoneyShape.Network;
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.HardDrives, Tint = UiRole.None,
                Kicker = W(t, "reallife_util_tab_network", "Your network"),
                Title = longName ? "Your broadband company with a long name" : "Your network",
                Line = fresh ? "Not founded yet" : "Two districts live",
                Progress = fresh ? 0 : 40,
                Note = fresh ? "Found it to begin" : "Dig the next district",
                NoteIcon = UiIcon.Wrench
            };
            m.Figures = fresh ? new UiFigureModel[0] : new[]
            {
                new UiFigureModel { Label = W(t, "reallife_money_customers", "Customers"), Value = "1,240" }
            };
            m.Chart = fresh ? null : LineChart(W(t, "reallife_money_customers", "Customers over 60 days"), 12, 800f, 1240f);
            m.Table = DistrictTable(t, fresh);
            m.Actions = new[]
            {
                new UiActionModel { Label = W(t, "reallife_money_dig", "Dig"), Kind = UiButtonKind.Primary, Icon = UiIcon.Wrench, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand }
            };
        }
        else
        {
            m.Shape = MoneyShape.Flow;
            m.Statement = Statement(t, fresh, "Bills");
        }
        return m;
    }

    private static UiActionModel Link(UiWords t)
    {
        return new UiActionModel { Label = W(t, "reallife_money_economy", "Economy"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Locate, Key = UiAction.Option, Command = MoneyCommands.Link };
    }

    private static UiProfileModel Borrower(UiWords t, bool fresh, bool longName)
    {
        return new UiProfileModel
        {
            Name = longName ? "A borrower with a very long given name" : W(t, "reallife_money_you", "You"),
            Hue = 28f,
            Role = fresh ? "No file yet" : "Good",
            RoleIcon = UiIcon.IdCard,
            Status = fresh ? "New" : "642",
            StatusTone = fresh ? UiRole.Warn : UiRole.Ok,
            FactLabels = new[] { W(t, "reallife_money_owed", "What you owe"), W(t, "reallife_money_cash", "Cash"), "Next" },
            FactValues = fresh ? new[] { "$0", "$12,400", "None" } : new[] { "$1,330,000", "$842,000", "Tomorrow" },
            RatingLabel = "Score",
            Rating = fresh ? 4 : 7
        };
    }

    private static UiProfileModel TycoonHead(UiWords t, bool fresh)
    {
        return new UiProfileModel
        {
            Name = "Space Karen",
            Hue = 320f,
            Role = "Karen Aerospace & Motors",
            RoleIcon = UiIcon.Rocket,
            Status = fresh ? "Quiet" : W(t, "reallife_money_expanding", "Expanding"),
            StatusTone = UiRole.Warn,
            FactLabels = new[] { "Worth", "30 days", "90 days" },
            FactValues = fresh ? new[] { "Not forward", "0", "0" } : new[] { "$48.2m", "Up $1.1m", "Up $4.0m" },
            RatingLabel = "Place",
            Rating = 8
        };
    }

    private static UiStatementModel Statement(UiWords t, bool fresh, string account)
    {
        var m = new UiStatementModel
        {
            Account = account,
            AccountIcon = UiIcon.Wallet,
            BalanceLabel = W(t, "reallife_money_cash", "Cash"),
            Balance = fresh ? "$12,400" : "$842,110",
            PeriodLabel = "30 days",
            Columns = new[]
            {
                W(t, "reallife_money_col_when", "When"), W(t, "reallife_money_col_what", "What"), W(t, "reallife_money_col_in", "In"),
                W(t, "reallife_money_col_out", "Out"), W(t, "reallife_money_col_balance", "Balance")
            },
            EmptyTitle = W(t, "reallife_money_cash_empty", "No movements yet"),
            EmptyBody = W(t, "reallife_money_coming", "Coming up: rent on day 12, wages on day 14.")
        };
        if (fresh)
        {
            return m;
        }
        m.Groups = new[]
        {
            new UiStatementGroup
            {
                Label = W(t, "reallife_money_brought", "Brought forward"),
                Total = "$860,000",
                TotalGood = true,
                Lines = new[]
                {
                    new UiStatementLine { When = "09:10", Icon = UiIcon.Store, What = "Takings", Where = "A shop of yours", In = "$18,400", Balance = "$878,400" },
                    new UiStatementLine { When = "18:00", Icon = UiIcon.People, What = "Wages", Where = "Staff", Out = "$6,200", Balance = "$872,200" }
                }
            }
        };
        m.TotalLabels = new[] { "In", "Out", "Change" };
        m.Totals = new[] { "$18,400", "$6,200", "$12,200" };
        m.ChangeGood = true;
        return m;
    }

    private static UiChartModel LineChart(string title, int points, float from, float to)
    {
        var values = new float[points];
        var names = new string[points];
        for (int i = 0; i < points; i++)
        {
            float u = points == 1 ? 1f : i / (float)(points - 1);
            values[i] = from + (to - from) * u;
            names[i] = (i + 1).ToString();
        }
        return new UiChartModel
        {
            Kind = UiChartKind.Line,
            Title = title,
            Points = names,
            Series = new[] { new UiSeries { Label = title, Values = values, Tint = UiRole.None } },
            Format = v => ((int)v).ToString(),
            EmptyTitle = "No history yet",
            EmptyBody = "A day of play will draw the line."
        };
    }

    private static UiDataTableModel DebtTable(UiWords t, bool fresh)
    {
        var m = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Debt", Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Owed", Kind = UiCellKind.Money, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Rate", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Left", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f }
            },
            EmptyTitle = W(t, "reallife_money_empty_debts", "No debts yet"),
            EmptyBody = W(t, "reallife_money_empty_loans", "A loan or a card will be listed here."),
            EmptyIcon = UiIcon.Bank
        };
        if (fresh)
        {
            return m;
        }
        m.Rows = new[]
        {
            Row("Largest lender", "$820,000", "4.8%", "1,240 days"),
            Row("Card", "$240,000", "17%", "Statement"),
            Row("Overdraft", "$160,000", "9%", "Open")
        };
        m.RowCount = m.Rows.Length;
        return m;
    }

    private static UiDataTableModel MoneyFeedTable(UiWords t, bool fresh)
    {
        var m = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = W(t, "reallife_money_feed", "Others borrowing"), Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Bank", Kind = UiCellKind.Text, Width = UiTokens.S16 * 3f },
                new UiDataColumn { Title = "Day", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Amount", Kind = UiCellKind.Money, Width = UiTokens.S16 * 2f }
            },
            EmptyTitle = W(t, "reallife_money_feed_empty", "Nobody else has borrowed yet"),
            EmptyBody = W(t, "reallife_money_feed", "Others borrowing"),
            EmptyIcon = UiIcon.People
        };
        if (fresh)
        {
            return m;
        }
        m.Rows = new[]
        {
            Row("Karen Aerospace & Motors", "Largest lender", "412", "$2,400,000"),
            Row("Luthor Everything Co.", "Second lender", "408", "$1,100,000"),
            Row("The Oracle of Omaha", "Third lender", "401", "$800,000")
        };
        m.RowCount = m.Rows.Length;
        return m;
    }

    private static UiDataTableModel LeagueTable(UiWords t, bool fresh)
    {
        var m = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Name", Kind = UiCellKind.Person, Width = 0f },
                new UiDataColumn { Title = "Worth", Kind = UiCellKind.Money, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "30 days", Kind = UiCellKind.Trend, Width = UiTokens.S16 * 2f }
            },
            EmptyTitle = W(t, "reallife_money_worth", "Who is catching whom"),
            EmptyBody = W(t, "reallife_rival_title", "Rival tycoons"),
            EmptyIcon = UiIcon.Trophy,
            Selected = 0
        };
        m.Rows = new[]
        {
            Person("The Oracle of Omaha", "Old money", 40f, "$2.20bn", 1),
            Person("The Wolf in Cashmere", "Old money", 18f, "$1.40bn", 0),
            Person("Space Karen", "Tycoon", 320f, fresh ? "$0" : "$48.2m", 1),
            Person("You", "You", 28f, "$12.4m", 1)
        };
        m.RowCount = m.Rows.Length;
        return m;
    }

    private static UiDataTableModel NamedAccounts(UiWords t, bool fresh, bool lending)
    {
        var m = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = lending ? "Borrower" : "Saver", Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Balance", Kind = UiCellKind.Money, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Rate", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Since", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f }
            },
            EmptyTitle = W(t, "reallife_money_biggest", "Named savers and borrowers will be listed here."),
            EmptyBody = W(t, "reallife_money_biggest", "Named savers and borrowers will be listed here."),
            EmptyIcon = UiIcon.People
        };
        if (fresh)
        {
            return m;
        }
        m.Rows = new[]
        {
            Row("Karen Aerospace & Motors", lending ? "$420,000" : "$180,000", "6.2%", "Day 80"),
            Row("Luthor Everything Co.", lending ? "$260,000" : "$90,000", "5.4%", "Day 60"),
            Row("Zucc Social Holdings", lending ? "$140,000" : "$70,000", "5.1%", "Day 40")
        };
        m.RowCount = m.Rows.Length;
        return m;
    }

    private static UiDataTableModel OtherBanks(UiWords t)
    {
        var m = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Bank", Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Rate", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Answer", Kind = UiCellKind.Chip, Width = UiTokens.S16 * 3f }
            }
        };
        m.Rows = new[]
        {
            ChipRow("Largest lender", "4.8%", "Yes", UiRole.Ok),
            ChipRow("Second lender", "5.8%", "Yes", UiRole.Ok),
            ChipRow("Third lender", "3.8%", "No", UiRole.Danger)
        };
        m.RowCount = m.Rows.Length;
        return m;
    }

    private static UiDataTableModel BranchTable(UiWords t, bool fresh)
    {
        var m = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Branch", Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Customers", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "State", Kind = UiCellKind.Chip, Width = UiTokens.S16 * 2f }
            },
            EmptyTitle = W(t, "reallife_branch_tab", "Branches"),
            EmptyBody = W(t, "reallife_money_locate", "Locate"),
            EmptyIcon = UiIcon.Building
        };
        if (fresh)
        {
            return m;
        }
        m.Rows = new[]
        {
            ChipRow("Midtown branch", "420", W(t, "reallife_money_live", "Live"), UiRole.Ok),
            ChipRow("Harbour branch", "180", "Digging", UiRole.Warn)
        };
        m.RowCount = m.Rows.Length;
        return m;
    }

    private static UiDataTableModel DistrictTable(UiWords t, bool fresh)
    {
        var m = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "District", Kind = UiCellKind.Text, Width = 0f },
                new UiDataColumn { Title = "Homes", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Customers", Kind = UiCellKind.Figure, Width = UiTokens.S16 * 2f },
                new UiDataColumn { Title = "Take up", Kind = UiCellKind.Meter, Width = UiTokens.S16 * 3f },
                new UiDataColumn { Title = "State", Kind = UiCellKind.Chip, Width = UiTokens.S16 * 2f }
            },
            EmptyTitle = W(t, "reallife_util_tab_network", "Your network"),
            EmptyBody = W(t, "reallife_money_dig", "Dig"),
            EmptyIcon = UiIcon.HardDrives
        };
        if (fresh)
        {
            return m;
        }
        var live = ChipRow("Midtown", "12,400", "640", W(t, "reallife_money_live", "Live"), UiRole.Ok);
        live.Values = new[] { 0, 12400, 640, 42, 0 };
        var dig = ChipRow("Harbour", "9,200", "0", "12 days", UiRole.Warn);
        dig.Values = new[] { 0, 9200, 0, 0, 0 };
        m.Rows = new[] { live, dig };
        m.RowCount = m.Rows.Length;
        return m;
    }

    private static UiCatalogueModel Plans(UiWords t, bool fresh)
    {
        var m = new UiCatalogueModel
        {
            EmptyTitle = W(t, "reallife_util_tab_internet", "Internet"),
            EmptyBody = W(t, "reallife_money_plan", "The plan you are on is marked."),
            EmptyIcon = UiIcon.Globe
        };
        if (fresh)
        {
            return m;
        }
        m.Cards = new[]
        {
            new UiCatalogueCard { Icon = UiIcon.Globe, Name = "Basic", Figure = "$1 a day", State = null },
            new UiCatalogueCard { Icon = UiIcon.Globe, Name = "Home", Figure = "$2 a day", State = "On" , StateTone = UiRole.Ok },
            new UiCatalogueCard { Icon = UiIcon.Globe, Name = "Fast", Figure = "$4 a day" }
        };
        m.CardCount = 3;
        m.Chosen = 1;
        m.DetailKicker = W(t, "reallife_money_plan", "The plan you are on is marked.");
        m.DetailLabels = new[] { "Speed", "Price" };
        m.DetailValues = new[] { "Home", "$2 a day" };
        return m;
    }

    private static UiDataRow Row(string a, string b, string c, string d)
    {
        return new UiDataRow { Cells = new[] { a, b, c, d } };
    }

    private static UiDataRow ChipRow(string a, string b, string chip, UiRole tone)
    {
        return new UiDataRow { Cells = new[] { a, b, chip }, Tones = new[] { UiRole.None, UiRole.None, tone } };
    }

    private static UiDataRow ChipRow(string a, string b, string c, string chip, UiRole tone)
    {
        return new UiDataRow { Cells = new[] { a, b, c, "0", chip }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone } };
    }

    private static UiDataRow Person(string name, string role, float hue, string worth, int trend)
    {
        return new UiDataRow
        {
            Cells = new[] { name, worth, trend > 0 ? "Up" : "Flat" },
            Values = new[] { 0, 0, trend },
            Sub = role,
            Hue = hue,
            Ring = UiRole.Ok
        };
    }
}
