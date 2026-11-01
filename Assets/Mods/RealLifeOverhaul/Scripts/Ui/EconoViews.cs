using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A row of the game's income statement, as its scroller or its business page holds it: a heading, a line under one, or the total.</summary>
    public struct EconoIncomeRow
    {
        public string Key;           // the row's key as the game names it (a business or a property by its name), for its glyph
        public string Name;          // its words
        public int Parent;           // its heading's index, or -1 for a heading or the total
        public bool Total;
        public bool Folded;          // a heading the game holds folded
        public float[] Values;       // the four days' money, yesterday first, as the overview's rows carry it; null where only words are
        public string[] Texts;       // the four days' money in the game's words, as a business's page writes it; null where figures are
        public int Kind;             // 1 money in, -1 money out, 0 a heading or no telling
    }

    /// <summary>A transaction as the game's tables list it: its day and time, its kind's key, its words, its money and the balance after it.</summary>
    public struct EconoTx
    {
        public int Day;
        public string Time;          // "08:30", or null where the game's own record is not found (another player's line)
        public string Type;          // the game's transaction key ("ba:transaction_wage"), for its glyph
        public string What;
        public float Amount;
        public string Balance;       // the balance after it in the game's words, or null
    }

    /// <summary>One loan as the game's loan row holds it, its words as the row wrote them.</summary>
    public sealed class EconoLoanView
    {
        public string Lender;        // the bank's name, or null where it has none
        public float Total, Left;
        public string PaidTotal;     // the row's paid and total, as the game wrote them
        public string Interest;      // the daily interest as the game wrote it
        public string Daily;         // the daily payment in its field
        public long DailyValue, DailyLeast;
        public string TimeLeft;      // "Day 488 (76 days left)"
        public string PayOff;        // the amount in its pay off field
        public long PayOffValue, PayOffMost;
        public bool CanPay;          // its pay off button shows and can be pressed
        public EconoTx[] Payments = new EconoTx[0];   // the game's own records of what was borrowed and paid, the newest first
    }

    /// <summary>One investment fund as the save and the game's fund page hold it.</summary>
    public sealed class EconoFundView
    {
        public string Key;           // the game's fund key ("ba:investmentfund_franklinus"), for its glyph
        public string Name;
        public float Value, Invested, Interest;
        public int Risk = -1;        // 0 low, 1 medium, 2 high, as the game reckons it; -1 unknown
        public string Initial, Added, Withdrawn, Earned;   // the game's money words
        public bool AutoOn;
        public string Auto;          // the daily auto-invest in its field
        public long AutoValue;
        public string Withdraw;      // the amount in the withdraw field
        public long WithdrawValue, WithdrawMost;
        public float[] History = new float[0];   // its value a day, oldest first, for the year
        public int[] HistoryDays = new int[0];
        public EconoTx[] Moves = new EconoTx[0];      // the game's own records of what was put in and taken out, the newest first
    }

    /// <summary>The taxes as the game's tax page wrote them, every figure in its words.</summary>
    public sealed class EconoTaxView
    {
        public string Rate;          // "21%"
        public string Back;          // back taxes due, or null for none
        public string Owed, OwedWhen;
        public int OwedTone;         // -1 to pay (the game's red), 1 settled (its green), 0 neither
        public int OwedProgress = -1;
        public string Projection, ProjectionWhen;
        public int ProjectionTone;
        public int ProjectionProgress = -1;
        public string Deductions;
        public string[] DeductibleKinds = new string[0];
        public bool Irs;             // the IRS is in the player's contacts, as the game shows its link
        public EconoTx[] Paid = new EconoTx[0];       // the game's own records of the taxes paid, the newest first
        public string[] BasisNames = new string[0];   // what this tax year's income tax is on, as the game reckons it: each business's sales
        public float[] BasisAmounts = new float[0];
    }

    /// <summary>What EconoView shows now, from the game's own app and save: plain data for <see cref="EconoViews"/>.</summary>
    public sealed class EconoState
    {
        public int Page;                                   // UiEcono.Overview and the rest
        public Func<float, string> Money;                  // the game's money words
        public string[] DayNames = new string[0];          // the statement's days as its heads name them, yesterday first
        public int[] DayNumbers = new int[0];
        public int Period;                                 // the day of the four the overview's statement shows
        public int BusinessPeriod;                         // and the business's
        public EconoIncomeRow[] Income = new EconoIncomeRow[0];
        public float Cash;
        public float[] CashTrend;
        public EconoTx[] Latest = new EconoTx[0];
        public EconoTx[] All = new EconoTx[0];
        public string[] TypeOptions = new string[0], AmountOptions = new string[0], DayOptions = new string[0];
        public int TypeChosen, AmountChosen, DayChosen;
        public int[] FoldedDays = new int[0];
        public EconoLoanView[] Loans = new EconoLoanView[0];
        public int LoanCount = -1, FundCount = -1;         // the tabs' counts, from the save, or -1 for the lists' lengths
        public int Loan;
        public EconoFundView[] Funds = new EconoFundView[0];
        public int Fund;
        public EconoTaxView Tax = new EconoTaxView();
        public string[] Businesses = new string[0];
        public int BusinessChosen = -1;
        public string BusinessKind;
        public string[] BusinessDays = new string[0];
        public EconoIncomeRow[] BusinessRows = new EconoIncomeRow[0];
        public float[] BusinessSales = new float[0], BusinessProfit = new float[0];   // its four days' sales and profit, yesterday first
        public float[][] BusinessCosts = new float[0][];   // its four days' costs by kind (EconoViews.CostKeys), yesterday first
        public bool CanDestination, CanBizMan;
        public int OpenList = -1;
    }

    /// <summary>
    /// EconoView's view, game-free: what the game's app shows (an <see cref="EconoState"/>) put into the money page's model
    /// (<see cref="UiEconoModel"/>), with the rules it stands on, each checked offline: the glyph of a statement's row and
    /// of a transaction's kind, where the money came from and went as shares (the four largest and the rest), the step of
    /// an amount's minus and plus, and a fund's return as a grade. Words come from the game's keys and the mod's through
    /// UiWords. Cost: a few small arrays a layout.
    /// </summary>
    public static class EconoViews
    {
        public const int MostSlices = 5;          // a donut's slices: the four largest and the rest
        public const int MostLatest = 8;          // the last transactions beside the statement

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Fills the page's model from what the game shows; what scrolls or folds on the page is kept.</summary>
        public static void Page(UiEconoModel m, UiWords t, EconoState v)
        {
            m.NewPage = m.Page != v.Page;
            m.Page = v.Page;
            m.Title = t("smartphone_econoview", "EconoView");
            m.Icon = UiIcon.ChartPie;   // the app's own glyph, as the phone and the app row show it
            Tabs(m, t, v);
            m.EmptyTitle = null;
            m.EmptyBody = null;
            UiPickListModel held = m.NewPage ? null : m.Picks;   // a list's scroll stays while its page does
            m.Picks = null;
            m.Hero = null;
            m.Chart = null;
            m.Figures = new UiFigureModel[0];
            m.RowsTitle = null;
            m.RowLabels = new string[0];
            m.RowValues = new string[0];
            m.ControlsTitle = null;
            m.Controls = new UiEconoControl[0];
            m.Note = null;
            m.NoteTitle = null;
            m.Link = null;
            m.Totals = new UiFigureModel[0];
            m.Split = null;
            m.HistoryTitle = null;
            m.HistoryEmpty = null;
            m.HistoryColumns = new UiColumn[0];
            m.History = new UiAppRow[0];
            m.Cash = new UiFigureModel[0];
            m.In = null;
            m.Out = null;
            m.LatestColumns = new UiColumn[0];
            m.Latest = new UiAppRow[0];
            m.LatestLink = null;
            m.Filters = new string[0];
            m.ToolActions = new UiActionModel[0];
            m.HeadActions = new UiActionModel[0];
            m.GroupRows = new int[0];
            m.GroupLines = new int[0][];
            if (m.NewPage)
            {
                m.Statement = new UiStatementModel();
                m.OpenList = -1;
            }
            switch (v.Page)
            {
                case UiEcono.Transactions: Transactions(m, t, v); break;
                case UiEcono.Loans: Loans(m, t, v, held); break;
                case UiEcono.Investments: Investments(m, t, v, held); break;
                case UiEcono.Taxes: Taxes(m, t, v, held); break;
                case UiEcono.Business: Business(m, t, v); break;
                default: Overview(m, t, v); break;
            }
            Options(m, v);
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_econo_prompt_tab", "Tab") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            };
        }

        /// <summary>The tab a page stands under: the full statement under the overview's.</summary>
        public static int TabOf(int page) => page == UiEcono.Transactions ? 0 : page == UiEcono.Overview ? 0 : page - 1;

        /// <summary>The page a tab opens.</summary>
        public static int PageOf(int tab) => tab <= 0 ? UiEcono.Overview : tab + 1;

        // The tabs across: the overview, loans and funds with their counts, the taxes, and one business while the player runs one.
        private static void Tabs(UiEconoModel m, UiWords t, EconoState v)
        {
            int loans = v.LoanCount >= 0 ? v.LoanCount : v.Loans.Length, funds = v.FundCount >= 0 ? v.FundCount : v.Funds.Length;
            var tabs = new List<UiNavEntry>(5)
            {
                new UiNavEntry { Icon = UiIcon.Wallet, Tint = UiRole.None, Label = t("reallife_ui_econo_overview", "Overview") },
                new UiNavEntry { Icon = UiIcon.Bank, Tint = UiRole.None, Label = t("econoview_loans", "Loans"), Count = loans > 0 ? Whole(loans) : null },
                new UiNavEntry { Icon = UiIcon.ChartLineUp, Tint = UiRole.None, Label = t("common_investments", "Investments"), Count = funds > 0 ? Whole(funds) : null },
                new UiNavEntry { Icon = UiIcon.Receipt, Tint = UiRole.None, Label = t("econoview_taxes", "Taxes") },
            };
            if (v.Businesses.Length > 0)
            {
                tabs.Add(new UiNavEntry { Icon = UiIcon.Store, Tint = UiRole.None, Label = t("reallife_ui_econo_business", "Business") });
            }
            m.Tabs = tabs.ToArray();
            m.Tab = Math.Min(TabOf(v.Page), m.Tabs.Length - 1);
        }

        // ------------------------------------------------------------------------------------------------- the overview

        private static void Overview(UiEconoModel m, UiWords t, EconoState v)
        {
            Statement(m.Statement, t, v, v.Income, v.DayNames, v.Period, UiIcon.Chart, null, true, out m.GroupRows, out m.GroupLines);
            int day = v.DayNumbers.Length > v.Period ? v.DayNumbers[v.Period] : 0;
            string dayWords = Fill(t("common_day_number", "Day {number}"), "number", Whole(day));
            m.Cash = new[] { new UiFigureModel { Label = t("persona_cash", "Cash"), Value = v.Money(v.Cash), Trend = v.CashTrend } };
            m.In = new UiEconoShare { Title = Fill(t("reallife_ui_econo_in", "Money in, {day}"), "day", dayWords), Empty = t("reallife_ui_econo_in_none", "Nothing came in that day.") };
            m.Out = new UiEconoShare { Title = Fill(t("reallife_ui_econo_out", "Money out, {day}"), "day", dayWords), Empty = t("reallife_ui_econo_out_none", "Nothing went out that day.") };
            Shares(v.Income, v.Period, 1, m.In, v.Money, t("reallife_ui_econo_rest", "Everything else"));
            Shares(v.Income, v.Period, -1, m.Out, v.Money, t("reallife_ui_econo_rest", "Everything else"));
            m.LatestTitle = t("econoview_last_transactions", "Last transactions");
            m.LatestColumns = new[]
            {
                new UiColumn { Title = t("reallife_ui_econo_what", "What") },
                new UiColumn { Title = t("common_day", "Day"), Width = 80f, Right = true },
                new UiColumn { Title = t("common_amount", "Amount"), Width = 104f, Right = true },
                new UiColumn { Title = t("common_balance", "Balance"), Width = 104f, Right = true },
            };
            int n = Math.Min(MostLatest, v.Latest.Length);
            m.Latest = new UiAppRow[n];
            for (int i = 0; i < n; i++)
            {
                EconoTx x = v.Latest[i];
                m.Latest[i] = new UiAppRow
                {
                    Icon = TxIcon(x.Type), Tint = UiRole.None, Grade = x.Amount > 0f ? 100 : -1,   // money in in the grade's green, as the statement's In; money out stays quiet
                    Cells = new[] { x.What, Whole(x.Day), Signed(x.Amount, v.Money), x.Balance ?? string.Empty }
                };
            }
            m.LatestLink = t("econoview_last_transactions_full_view_button", "Full transactions view");
        }

        /// <summary>
        /// The income statement as the kit's statement: each heading a band that folds with its day's sum, each line under it
        /// with its glyph, its money in or out, and the day before's beside it; the day's profit as the one large figure; the
        /// money in and out at the foot. A heading with no lines and nothing on it is left out.
        /// </summary>
        public static void Statement(UiStatementModel s, UiWords t, EconoState v, EconoIncomeRow[] rows, string[] days, int period, UiIcon icon, string number, bool foot,
            out int[] groupRows, out int[][] groupLines)
        {
            int p = Math.Max(0, Math.Min(period, Math.Max(0, days.Length - 1)));
            s.Account = t("econoview_income_statement", "Income Statement");
            s.AccountIcon = icon;
            s.Number = number;
            s.BalanceLabel = t("econoview_row_profit", "Profit");
            s.PeriodLabel = days.Length > 0 ? days[p] : null;
            string before = p + 1 < days.Length ? Short(days[p + 1]) : string.Empty;   // the day before's figures stand in the last column
            s.Columns = new[] { string.Empty, string.Empty, t("reallife_ui_econo_money_in", "Money in"), t("reallife_ui_econo_money_out", "Money out"), before };
            s.EmptyTitle = t("reallife_ui_econo_empty_title", "No day counted yet");
            s.EmptyBody = t("reallife_ui_econo_empty_body", "The first income statement comes in at midnight, when the day's books close.");
            var groups = new List<UiStatementGroup>(8);
            var heads = new List<int>(8);
            var under = new List<int[]>(8);
            float moneyIn = 0f, moneyOut = 0f;
            string total = null;
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Total)
                {
                    total = Figure(rows[r], p, v.Money, true);
                    continue;
                }
                if (rows[r].Parent >= 0)
                {
                    continue;
                }
                var lines = new List<UiStatementLine>(8);
                var lineRows = new List<int>(8);
                for (int c = 0; c < rows.Length; c++)
                {
                    if (rows[c].Parent != r)
                    {
                        continue;
                    }
                    lineRows.Add(c);
                    bool money = rows[c].Values != null;
                    float value = money ? At(rows[c].Values, p) : 0f;
                    bool inward = money ? value > 0f : rows[c].Kind > 0;
                    string words = Figure(rows[c], p, v.Money, false);
                    if (money)
                    {
                        moneyIn += value > 0f ? value : 0f;
                        moneyOut += value < 0f ? -value : 0f;
                    }
                    lines.Add(new UiStatementLine
                    {
                        Icon = RowIcon(rows[c].Key, rows[r].Key, rows[c].Kind), What = rows[c].Name,
                        In = inward ? words : null, Out = inward ? null : Unsigned(words),
                        Balance = p + 1 < days.Length ? Figure(rows[c], p + 1, v.Money, true) : null
                    });
                }
                string sum = Figure(rows[r], p, v.Money, true);
                if (lines.Count == 0 && Zero(rows[r]))
                {
                    continue;   // a heading the game made with nothing under it and nothing on it says nothing
                }
                bool good = rows[r].Values != null ? At(rows[r].Values, p) > 0f : UiModels.Sign(sum) >= 0 && !Zero(rows[r].Texts, p);
                groups.Add(new UiStatementGroup { Label = rows[r].Name, Total = sum, TotalGood = good, Folded = rows[r].Folded, Lines = lines.ToArray() });
                heads.Add(r);
                under.Add(lineRows.ToArray());
            }
            s.Groups = groups.ToArray();
            groupRows = heads.ToArray();
            groupLines = under.ToArray();
            s.Balance = total ?? v.Money(0f);
            if (foot && groups.Count > 0)
            {
                s.TotalLabels = new[] { t("reallife_ui_econo_money_in", "Money in"), t("reallife_ui_econo_money_out", "Money out") };
                s.Totals = new[] { v.Money(moneyIn), v.Money(moneyOut) };
            }
            else
            {
                s.TotalLabels = new string[0];
                s.Totals = new string[0];
            }
        }

        /// <summary>
        /// Where the money came from (<paramref name="sign"/> 1) or went (-1) on a day, by the statement's lines (a heading
        /// with no lines stands for itself): the four largest and the rest as one, largest first.
        /// </summary>
        public static void Shares(EconoIncomeRow[] rows, int period, int sign, UiEconoShare into, Func<float, string> money, string rest)
        {
            var parts = new List<KeyValuePair<string, float>>(16);
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Total || rows[r].Values == null)
                {
                    continue;
                }
                bool heading = rows[r].Parent < 0;
                if (heading && HasLines(rows, r))
                {
                    continue;
                }
                float value = At(rows[r].Values, period) * sign;
                if (value > 0.5f)
                {
                    parts.Add(new KeyValuePair<string, float>(rows[r].Name, value));
                }
            }
            parts.Sort((a, b) => b.Value.CompareTo(a.Value));
            int n = Math.Min(parts.Count, MostSlices);
            into.Labels = new string[n];
            into.Values = new string[n];
            into.Shares = new float[n];
            for (int i = 0; i < n; i++)
            {
                bool others = i == MostSlices - 1 && parts.Count > MostSlices;
                float value = parts[i].Value;
                if (others)
                {
                    for (int k = i + 1; k < parts.Count; k++)
                    {
                        value += parts[k].Value;
                    }
                }
                into.Labels[i] = others ? rest : parts[i].Key;
                into.Values[i] = money(value);
                into.Shares[i] = value;
            }
        }

        private static bool HasLines(EconoIncomeRow[] rows, int heading)
        {
            for (int c = 0; c < rows.Length; c++)
            {
                if (rows[c].Parent == heading)
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- every transaction

        private static void Transactions(UiEconoModel m, UiWords t, EconoState v)
        {
            m.Filters = new[] { Pick(v.TypeOptions, v.TypeChosen), Pick(v.AmountOptions, v.AmountChosen) };
            m.ToolActions = new[]
            {
                new UiActionModel { Label = t("econoview_full_transactions_export_to_csv", "Export to CSV"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Receipt,
                    Key = UiAction.Option, Command = UiAppWindow.ActionCommand, Arg = 0 },
                new UiActionModel { Label = t("common_close", "Close"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Close, Key = UiAction.Count, Command = UiAppWindow.ActionCommand, Arg = 1 },
            };
            UiStatementModel s = m.Statement;
            s.Account = t("econoview_full_transactions_title", "Transactions");
            s.AccountIcon = UiIcon.Wallet;
            s.Number = Fill(t("reallife_ui_econo_count", "{count} transactions"), "count", Whole(v.All.Length));
            s.BalanceLabel = t("common_balance", "Balance");
            s.Balance = v.Money(v.Cash);
            s.PeriodLabel = Pick(v.DayOptions, v.DayChosen);
            s.Columns = new[] { t("reallife_ui_econo_time", "Time"), t("reallife_ui_econo_what", "What"), t("reallife_ui_econo_money_in", "Money in"),
                t("reallife_ui_econo_money_out", "Money out"), t("common_balance", "Balance") };
            s.EmptyTitle = t("reallife_ui_econo_none_title", "No transactions match");
            s.EmptyBody = t("reallife_ui_econo_none_body", "No transaction fits these filters. Choose wider ones to see more.");
            string dayKey = t("common_day_number", "Day {number}");
            var days = new List<int>(16);
            for (int i = 0; i < v.All.Length; i++)
            {
                if (!days.Contains(v.All[i].Day))
                {
                    days.Add(v.All[i].Day);
                }
            }
            days.Sort((a, b) => b.CompareTo(a));   // the newest day first, as the game lists them
            var groups = new UiStatementGroup[days.Count];
            float moneyIn = 0f, moneyOut = 0f;
            var lines = new List<UiStatementLine>(32);
            for (int g = 0; g < days.Count; g++)
            {
                lines.Clear();
                float sum = 0f;
                for (int i = 0; i < v.All.Length; i++)
                {
                    ref EconoTx x = ref v.All[i];
                    if (x.Day != days[g])
                    {
                        continue;
                    }
                    sum += x.Amount;
                    moneyIn += x.Amount > 0f ? x.Amount : 0f;
                    moneyOut += x.Amount < 0f ? -x.Amount : 0f;
                    lines.Add(new UiStatementLine
                    {
                        When = x.Time, Icon = TxIcon(x.Type), What = x.What, In = x.Amount > 0f ? v.Money(x.Amount) : null, Out = x.Amount > 0f ? null : v.Money(-x.Amount),
                        Balance = x.Balance
                    });
                }
                groups[g] = new UiStatementGroup
                {
                    Label = Fill(dayKey, "number", Whole(days[g])), Total = Signed(sum, v.Money), TotalGood = sum > 0f, Folded = Array.IndexOf(v.FoldedDays, days[g]) >= 0,
                    Lines = lines.ToArray()
                };
            }
            s.Groups = groups;
            float change = moneyIn - moneyOut;
            s.TotalLabels = groups.Length > 0 ? new[] { t("reallife_ui_econo_money_in", "Money in"), t("reallife_ui_econo_money_out", "Money out"), t("reallife_ui_econo_change", "Change") }
                : new string[0];
            s.Totals = groups.Length > 0 ? new[] { v.Money(moneyIn), v.Money(moneyOut), Signed(change, v.Money) } : new string[0];
            s.ChangeGood = change >= 0f;
        }

        // ------------------------------------------------------------------------------------------------- loans

        private static void Loans(UiEconoModel m, UiWords t, EconoState v, UiPickListModel held)
        {
            if (v.Loans.Length == 0)
            {
                m.EmptyIcon = UiIcon.Bank;
                m.EmptyTitle = t("reallife_ui_econo_no_loans", "No loans");
                m.EmptyBody = t("econoview_loans_noloans", "You don't have any loans. Contact your bank for more information.");
                return;
            }
            int chosen = Math.Max(0, Math.Min(v.Loan, v.Loans.Length - 1));
            string perDay = t("reallife_ui_econo_per_day", "{amount} a day");
            var rows = new UiPickRow[v.Loans.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                EconoLoanView l = v.Loans[i];
                rows[i] = new UiPickRow
                {
                    Icon = UiIcon.Bank, Tint = UiRole.None, Title = Lender(t, l), Sub = l.TimeLeft, Figure = v.Money(l.Left), Note = Fill(perDay, "amount", l.Daily),
                    Meter = Paid(l), MeterTone = UiRole.Accent
                };
            }
            m.Picks = Keep(held, rows, chosen);
            EconoLoanView c = v.Loans[chosen];
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.Bank, Tint = UiRole.None, Kicker = t("reallife_ui_econo_loan", "Loan"), Title = Lender(t, c),
                Line = Fill(t("reallife_ui_econo_paid_of", "Paid {paid} of {total}"), "paid", v.Money(c.Total - c.Left), "total", v.Money(c.Total)), Progress = Paid(c),
                Note = c.TimeLeft, NoteIcon = UiIcon.Calendar
            };
            m.Figures = new[] { new UiFigureModel { Label = t("reallife_ui_econo_left", "Left to pay"), Value = v.Money(c.Left) } };
            float owed = 0f, daily = 0f;
            for (int i = 0; i < v.Loans.Length; i++)
            {
                owed += v.Loans[i].Left;
                daily += v.Loans[i].DailyValue;
            }
            m.Totals = new[]
            {
                new UiFigureModel { Label = t("reallife_ui_econo_owed", "Owed in all"), Value = v.Money(owed) },
                new UiFigureModel { Label = t("reallife_ui_econo_paid_day", "Paid a day"), Value = v.Money(daily) },
            };
            m.RowLabels = new[] { t("reallife_ui_econo_borrowed", "Borrowed"), t("reallife_ui_econo_paid_so_far", "Paid so far"),
                t("econoview_loans_daily_interest_rate", "Daily interest rate"), t("econoview_loans_daily_payment", "Daily payment") };
            m.RowValues = new[] { v.Money(c.Total), v.Money(c.Total - c.Left), c.Interest, c.Daily };
            m.Split = Split(t("reallife_ui_econo_owed_by", "Owed to each bank"), v.Loans.Length, i => Lender(t, v.Loans[i]), i => v.Loans[i].Left, v.Money, t);
            History(m, t, v, c.Payments, t("reallife_ui_econo_payments", "Payments"), t("reallife_ui_econo_no_payments", "No payment has been made on this loan yet."));
            m.Controls = new[]
            {
                new UiEconoControl { Label = t("econoview_loans_daily_payment", "Daily payment"), Figure = c.Daily, CanLess = c.DailyValue > c.DailyLeast, CanMore = c.DailyValue < (long)c.Left },
                new UiEconoControl
                {
                    Label = t("econoview_loan_additional_payment", "Additional payment"), Figure = c.PayOff, CanLess = c.PayOffValue > 1, CanMore = c.PayOffValue < c.PayOffMost,
                    Action = t("econoview_loan_make_payment_button", "Make payment"), ActionKind = UiButtonKind.Primary, ActionIcon = UiIcon.Coin, ActionKey = UiAction.Alt,
                    ActionDisabled = !c.CanPay
                },
            };
        }

        private static string Lender(UiWords t, EconoLoanView l) => string.IsNullOrEmpty(l.Lender) ? t("reallife_ui_econo_a_loan", "A loan") : l.Lender;

        /// <summary>A loan's share paid, 0 to 100.</summary>
        public static int Paid(EconoLoanView l) => l.Total > 0f ? Math.Max(0, Math.Min(100, (int)Math.Round((l.Total - l.Left) / l.Total * 100f))) : 0;

        // ------------------------------------------------------------------------------------------------- investments

        private static void Investments(UiEconoModel m, UiWords t, EconoState v, UiPickListModel held)
        {
            if (v.Funds.Length == 0)
            {
                m.EmptyIcon = UiIcon.ChartLineUp;
                m.EmptyTitle = t("reallife_ui_econo_no_funds", "No investments");
                m.EmptyBody = t("econoview_investments_noinvestments", "You don't have any investments. Contact your bank for more information.");
                return;
            }
            int chosen = Math.Max(0, Math.Min(v.Fund, v.Funds.Length - 1));
            string autoWords = t("reallife_ui_econo_auto_on", "Adds {amount} a day");
            var rows = new UiPickRow[v.Funds.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                EconoFundView f = v.Funds[i];
                int grade = Grade(f);
                rows[i] = new UiPickRow
                {
                    Icon = FundIcon(f.Key), Tint = UiRole.None, Title = f.Name, Sub = f.AutoOn ? Fill(autoWords, "amount", f.Auto) : null, Chip = Risk(t, f.Risk),
                    ChipTone = f.Risk >= 2 ? UiRole.Danger : f.Risk == 1 ? UiRole.Warn : UiRole.Ok, Figure = v.Money(f.Value), Note = Return(f), NoteInk = UiTokens.Grade(grade), Meter = -1
                };
            }
            m.Picks = Keep(held, rows, chosen);
            EconoFundView c = v.Funds[chosen];
            string dayKey = t("common_day_number", "Day {number}");
            var chart = new UiChartModel
            {
                Kind = UiChartKind.Line, Title = c.Name, Format = v.Money, Points = new string[c.History.Length], Reads = new string[c.History.Length],
                Series = new[] { new UiSeries { Label = t("econoview_investments_current_value", "Current value"), Values = c.History, Tint = UiRole.None } },
                EmptyTitle = t("econoview_investments_notenoughdata", "Not enough data to show graph"),
                EmptyBody = t("reallife_ui_econo_chart_wait", "The line starts once the fund has seen a second day.")
            };
            for (int i = 0; i < c.History.Length; i++)
            {
                int day = i < c.HistoryDays.Length ? c.HistoryDays[i] : i;
                chart.Points[i] = Whole(day);
                chart.Reads[i] = Fill(dayKey, "number", Whole(day));
            }
            if (c.History.Length <= 1)
            {
                chart.Points = new string[0];   // a single day is no line: the chart says so in its room
                chart.Reads = null;
            }
            m.Chart = chart;
            float worth = 0f, earned = 0f;
            for (int i = 0; i < v.Funds.Length; i++)
            {
                worth += v.Funds[i].Value;
                earned += v.Funds[i].Interest;
            }
            m.Totals = new[]
            {
                new UiFigureModel { Label = t("reallife_ui_econo_worth", "Worth in all"), Value = v.Money(worth) },
                new UiFigureModel { Label = t("reallife_ui_econo_earned", "Earned in all"), Value = Signed(earned, v.Money) },
            };
            m.Split = Split(t("reallife_ui_econo_worth_by", "Worth in each fund"), v.Funds.Length, i => v.Funds[i].Name, i => v.Funds[i].Value, v.Money, t);
            m.RowLabels = new[] { t("econoview_investments_initial_investment", "Initial investment"), t("reallife_ui_econo_added", "Added since"),
                t("reallife_ui_econo_withdrawn", "Withdrawn"), t("econoview_investments_changetotal", "Total change"), t("econoview_investments_current_value", "Current value") };
            m.RowValues = new[] { c.Initial, c.Added, c.Withdrawn, c.Earned, v.Money(c.Value) };
            History(m, t, v, c.Moves, t("reallife_ui_econo_moves", "Put in and taken out"), t("reallife_ui_econo_no_moves", "Nothing has been put in or taken out in the last days."));
            m.Controls = new[]
            {
                new UiEconoControl
                {
                    Label = t("econoview_investments_withdraw_money", "Withdraw money"), Figure = c.Withdraw, CanLess = c.WithdrawValue > 0, CanMore = c.WithdrawValue < c.WithdrawMost,
                    Action = t("econoview_investments_withdraw", "Withdraw"), ActionKind = UiButtonKind.Primary, ActionIcon = UiIcon.Coin, ActionKey = UiAction.Alt,
                    ActionDisabled = c.WithdrawValue <= 0
                },
                new UiEconoControl { Label = t("econoview_investments_autoinvest", "Daily auto-invest"), Switch = true, On = c.AutoOn,
                    Word = c.AutoOn ? t("reallife_ui_econo_on", "On") : t("reallife_ui_econo_off", "Off") },
                new UiEconoControl { Label = t("reallife_ui_econo_auto_amount", "Invested each day"), Figure = c.Auto, CanLess = c.AutoValue > 0, CanMore = true },
            };
        }

        /// <summary>A fund's return on what was put in, written with its sign to one place ("+4.2%").</summary>
        public static string Return(EconoFundView f)
        {
            float r = f.Invested > 0f ? f.Interest / f.Invested * 100f : 0f;
            string figure = Math.Abs(r).ToString("0.0", Inv) + "%";
            return r > 0.05f ? "+" + figure : r < -0.05f ? "-" + figure : figure;
        }

        /// <summary>A fund's return as a grade, 0 to 100: nothing gained the middle's yellow, two in a hundred gained the top's green, four lost the bottom's red.</summary>
        public static int Grade(EconoFundView f)
        {
            float r = f.Invested > 0f ? f.Interest / f.Invested * 100f : 0f;
            return Math.Max(0, Math.Min(100, (int)Math.Round(60f + r * 10f)));
        }

        private static string Risk(UiWords t, int risk)
        {
            switch (risk)
            {
                case 0: return t("reallife_ui_econo_risk_low", "Low risk");
                case 1: return t("reallife_ui_econo_risk_medium", "Medium risk");
                case 2: return t("reallife_ui_econo_risk_high", "High risk");
                default: return null;
            }
        }

        // ------------------------------------------------------------------------------------------------- taxes

        private static void Taxes(UiEconoModel m, UiWords t, EconoState v, UiPickListModel held)
        {
            EconoTaxView x = v.Tax;
            var rows = new List<UiPickRow>(3);
            if (!string.IsNullOrEmpty(x.Back))
            {
                rows.Add(new UiPickRow { Icon = UiIcon.Warning, Tint = UiRole.None, Title = t("econoview_back_taxes_due", "Back taxes due"),
                    Sub = t("econoview_taxes_due_immediately", "Due Immediately"),
                    Figure = x.Back, FigureInk = UiRole.DangerText, Meter = -1 });
            }
            rows.Add(new UiPickRow { Icon = UiIcon.Receipt, Tint = UiRole.None, Title = t("econoview_taxes_owed", "Taxes owed"), Sub = x.OwedWhen, Figure = x.Owed, FigureInk = Tone(x.OwedTone),
                Meter = x.OwedProgress, MeterTone = UiRole.Accent });
            rows.Add(new UiPickRow { Icon = UiIcon.Calendar, Tint = UiRole.None, Title = t("econoview_taxes_projection", "Upcoming tax projection"), Sub = x.ProjectionWhen,
                Figure = x.Projection, FigureInk = Tone(x.ProjectionTone), Meter = x.ProjectionProgress, MeterTone = UiRole.Accent });
            m.Picks = Keep(held, rows.ToArray(), -1);
            m.Picks.Title = t("reallife_ui_econo_due", "What is due");
            m.RowsTitle = t("reallife_ui_econo_basis", "How it is worked out");
            m.RowLabels = new[] { t("contacts_taxes_tax_rate", "Tax rate"), t("econoview_total_tax_deductions", "Total tax deductions") };
            m.RowValues = new[] { x.Rate, x.Deductions };
            if (x.DeductibleKinds.Length > 0)
            {
                m.NoteTitle = t("reallife_ui_econo_deductible", "Costs that lower the tax");
                m.Note = string.Join("\n", x.DeductibleKinds);
            }
            m.Link = x.Irs ? t("econoview_taxes_irs_contact", "IRS Contact") : null;
            var basis = new EconoIncomeRow[Math.Min(x.BasisNames.Length, x.BasisAmounts.Length)];
            for (int i = 0; i < basis.Length; i++)
            {
                basis[i] = new EconoIncomeRow { Name = x.BasisNames[i], Parent = -1, Values = new[] { x.BasisAmounts[i] } };
            }
            m.Split = new UiEconoShare { Title = t("reallife_ui_econo_taxed_on", "What the tax is on, this year"),
                Empty = t("reallife_ui_econo_taxed_none", "Nothing taxable has been earned yet this tax year.") };
            Shares(basis, 0, 1, m.Split, v.Money, t("reallife_ui_econo_rest", "Everything else"));
            History(m, t, v, x.Paid, t("econoview_taxes_paid", "Taxes paid"), t("reallife_ui_econo_no_tax_paid", "No tax has been paid in the last days."));
        }

        private static UiRole Tone(int tone) => tone < 0 ? UiRole.DangerText : tone > 0 ? UiRole.OkText : UiRole.None;

        // ------------------------------------------------------------------------------------------------- one business

        private static void Business(UiEconoModel m, UiWords t, EconoState v)
        {
            if (v.Businesses.Length == 0 || v.BusinessChosen < 0)
            {
                m.EmptyIcon = UiIcon.Store;
                m.EmptyTitle = t("reallife_ui_econo_no_business", "No business yet");
                m.EmptyBody = t("reallife_ui_econo_no_business_body", "Each business you run shows its own statement here, day by day.");
                return;
            }
            m.Choice = v.Businesses[Math.Min(v.BusinessChosen, v.Businesses.Length - 1)];
            m.ChoiceLine = v.BusinessKind;
            m.Link = v.CanBizMan ? t("open_in_bizman", "Open in BizMan") : null;
            m.HeadActions = v.CanDestination
                ? new[] { new UiActionModel { Label = t("common_setdestination", "Set destination"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option,
                    Command = UiAppWindow.ActionCommand, Arg = 0 } }
                : new UiActionModel[0];
            Statement(m.Statement, t, v, v.BusinessRows, v.BusinessDays, v.BusinessPeriod, UiIcon.Store, null, false, out m.GroupRows, out m.GroupLines);
            int p = Math.Max(0, Math.Min(v.BusinessPeriod, v.BusinessDays.Length - 1));
            int days = Math.Min(v.BusinessDays.Length, Math.Min(v.BusinessSales.Length, v.BusinessProfit.Length));
            if (days == 0)
            {
                return;
            }
            var sales = new float[days];
            var profit = new float[days];
            var points = new string[days];
            var reads = new string[days];
            string dayKey = t("common_day_number", "Day {number}");
            for (int i = 0; i < days; i++)
            {
                int back = days - 1 - i;   // oldest first, as a line reads
                sales[i] = v.BusinessSales[back];
                profit[i] = v.BusinessProfit[back];
                int number = back < v.DayNumbers.Length ? v.DayNumbers[back] : 0;
                points[i] = Fill(dayKey, "number", Whole(number));
                reads[i] = v.BusinessDays[back];
            }
            m.Cash = new[] { new UiFigureModel { Label = t("econoview_row_sales", "Sales"), Value = v.Money(v.BusinessSales[p]), Trend = days > 1 ? sales : null } };
            string dayWords = Fill(dayKey, "number", Whole(p < v.DayNumbers.Length ? v.DayNumbers[p] : 0));
            m.Out = new UiEconoShare { Title = Fill(t("reallife_ui_econo_out", "Money out, {day}"), "day", dayWords), Empty = t("reallife_ui_econo_out_none", "Nothing went out that day.") };
            Costs(m.Out, p < v.BusinessCosts.Length ? v.BusinessCosts[p] : null, t, v.Money);
            m.Chart = new UiChartModel
            {
                Kind = UiChartKind.Bars, Title = t("reallife_ui_econo_profit_days", "Profit, the last days"), Points = points, Reads = reads, Format = v.Money,
                Series = new[] { new UiSeries { Label = t("econoview_row_profit", "Profit"), Values = profit, Tint = UiRole.None } },
                EmptyTitle = t("reallife_ui_econo_empty_title", "No day counted yet"),
                EmptyBody = t("reallife_ui_econo_empty_body", "The first income statement comes in at midnight, when the day's books close.")
            };
        }

        /// <summary>The keys of a business's costs by kind, in the order the game's business page lists them; resources last.</summary>
        public static readonly string[] CostKeys =
        {
            "econoview_row_salaries", "econoview_row_rent", "econoview_row_marketing", "econoview_row_theft", "econoview_row_licensing_fees", "econoview_row_resources"
        };

        private static readonly string[] CostWords = { "Salaries", "Rent", "Marketing", "Theft", "Licensing Fees", "Resources" };

        /// <summary>A business's costs on a day as a share by kind, the largest first, kinds with nothing left out.</summary>
        public static void Costs(UiEconoShare into, float[] costs, UiWords t, Func<float, string> money)
        {
            var parts = new List<KeyValuePair<string, float>>(CostKeys.Length);
            for (int k = 0; costs != null && k < costs.Length && k < CostKeys.Length; k++)
            {
                if (costs[k] > 0.5f)
                {
                    parts.Add(new KeyValuePair<string, float>(t(CostKeys[k], CostWords[k]), costs[k]));
                }
            }
            parts.Sort((a, b) => b.Value.CompareTo(a.Value));
            into.Labels = new string[parts.Count];
            into.Values = new string[parts.Count];
            into.Shares = new float[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                into.Labels[i] = parts[i].Key;
                into.Values[i] = money(parts[i].Value);
                into.Shares[i] = parts[i].Value;
            }
        }

        // How a sum shares out between the things of a list, the largest first, the rest as one past the donut's slices; null
        // for fewer than two, where a donut would say nothing.
        private static UiEconoShare Split(string title, int count, Func<int, string> name, Func<int, float> value, Func<float, string> money, UiWords t)
        {
            if (count < 2)
            {
                return null;
            }
            var rows = new EconoIncomeRow[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = new EconoIncomeRow { Name = name(i), Parent = -1, Values = new[] { value(i) } };
            }
            var share = new UiEconoShare { Title = title };
            Shares(rows, 0, 1, share, money, t("reallife_ui_econo_rest", "Everything else"));
            return share;
        }

        // The game's records under a page's controls: what, the day, the money with its sign and the balance after, or a sentence.
        private static void History(UiEconoModel m, UiWords t, EconoState v, EconoTx[] moves, string title, string empty)
        {
            m.HistoryTitle = title;
            m.HistoryEmpty = moves.Length == 0 ? empty : null;
            m.HistoryColumns = new[]
            {
                new UiColumn { Title = string.Empty },   // the table's title names its lines: a head over them would say it twice
                new UiColumn { Title = t("common_day", "Day"), Width = 80f, Right = true },
                new UiColumn { Title = t("common_amount", "Amount"), Width = 104f, Right = true },
                new UiColumn { Title = t("common_balance", "Balance"), Width = 104f, Right = true },
            };
            m.History = new UiAppRow[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                EconoTx x = moves[i];
                m.History[i] = new UiAppRow { Grade = x.Amount > 0f ? 100 : -1, Cells = new[] { x.What, Whole(x.Day), Signed(x.Amount, v.Money), x.Balance ?? string.Empty } };
            }
        }

        // ------------------------------------------------------------------------------------------------- the open list

        private static void Options(UiEconoModel m, EconoState v)
        {
            m.OpenList = v.OpenList;
            switch (v.OpenList)
            {
                case UiEcono.ListType: m.Options = v.TypeOptions; m.OptionChosen = v.TypeChosen; break;
                case UiEcono.ListAmount: m.Options = v.AmountOptions; m.OptionChosen = v.AmountChosen; break;
                case UiEcono.ListPeriod:
                    m.Options = v.Page == UiEcono.Transactions ? v.DayOptions : v.Page == UiEcono.Business ? v.BusinessDays : v.DayNames;
                    m.OptionChosen = v.Page == UiEcono.Transactions ? v.DayChosen : v.Page == UiEcono.Business ? v.BusinessPeriod : v.Period;
                    break;
                case UiEcono.ListBusiness: m.Options = v.Businesses; m.OptionChosen = v.BusinessChosen; break;
                default: m.Options = new string[0]; m.OptionChosen = -1; break;
            }
            if (m.Options.Length == 0)
            {
                m.OpenList = -1;
            }
        }

        // ------------------------------------------------------------------------------------------------- the rules

        /// <summary>The glyph of a statement's line: by its own key, else by its heading's, else money in or out.</summary>
        public static UiIcon RowIcon(string key, string heading, int kind)
        {
            switch (key)
            {
                case "econoview_row_salary_income": return UiIcon.Briefcase;
                case "econoview_row_private_residences": return UiIcon.Home;
                case "econoview_row_loans": return UiIcon.Bank;
                case "ba:transaction_healthinsurance_label": return UiIcon.Heartbeat;
                case "econoview_row_unassigned_staff_wages":
                case "econoview_row_salaries": return UiIcon.People;
                case "ba:transaction_headhunterreplacement_label": return UiIcon.IdBadge;
                case "econoview_row_parking_fees": return UiIcon.Car;
                case "econoview_row_rent": return UiIcon.Building;
                case "econoview_row_marketing": return UiIcon.Megaphone;
                case "econoview_row_theft": return UiIcon.Eye;
                case "econoview_row_licensing_fees": return UiIcon.Scales;
            }
            switch (heading)
            {
                case "econoview_row_businesses": return UiIcon.Store;
                case "common_real_estate": return UiIcon.Building;
                case "econoview_row_sales": return UiIcon.Coin;
                case "econoview_row_resources": return UiIcon.Package;
            }
            return kind < 0 ? UiIcon.Receipt : UiIcon.Coin;
        }

        /// <summary>The glyph of a transaction by its kind's key: what the money went on or came from, a coin where the key says nothing.</summary>
        public static UiIcon TxIcon(string type)
        {
            string k = type ?? string.Empty;
            if (Has(k, "taxi"))
            {
                return UiIcon.Car;
            }
            if (Has(k, "tax"))
            {
                return UiIcon.Receipt;
            }
            if (Has(k, "loan") || Has(k, "bank"))
            {
                return UiIcon.Bank;
            }
            if (Has(k, "investment"))
            {
                return UiIcon.ChartLineUp;
            }
            if (Has(k, "playerjobsalary") || Has(k, "jobwage") || Has(k, "salary"))
            {
                return UiIcon.Briefcase;
            }
            if (Has(k, "wage") || Has(k, "bonus") || Has(k, "training") || Has(k, "recruit") || Has(k, "headhunter") || Has(k, "replacement"))
            {
                return UiIcon.People;
            }
            if (Has(k, "rent") || Has(k, "deposit") || Has(k, "building") || Has(k, "interior") || Has(k, "moving") || Has(k, "furniture"))
            {
                return UiIcon.Building;
            }
            if (Has(k, "import") || Has(k, "delivery") || Has(k, "cargo"))
            {
                return UiIcon.Truck;
            }
            if (Has(k, "vehicle") || Has(k, "taxi") || Has(k, "subway") || Has(k, "parking") || Has(k, "tow") || Has(k, "scooter") || Has(k, "driver") || Has(k, "boat"))
            {
                return UiIcon.Car;
            }
            if (Has(k, "marketing"))
            {
                return UiIcon.Megaphone;
            }
            if (Has(k, "hospital") || Has(k, "doctor") || Has(k, "health"))
            {
                return UiIcon.Heartbeat;
            }
            if (Has(k, "tuition"))
            {
                return UiIcon.Cap;
            }
            if (Has(k, "itempurchase") || Has(k, "purchase"))
            {
                return UiIcon.ShoppingCart;
            }
            if (Has(k, "casino") || Has(k, "golf") || Has(k, "tennis") || Has(k, "gym") || Has(k, "attraction"))
            {
                return UiIcon.Star;
            }
            if (Has(k, "licens"))
            {
                return UiIcon.Scales;
            }
            return UiIcon.Coin;
        }

        /// <summary>A fund's glyph by what its key says it holds: energy, technology, industry, bonds, the world or one market.</summary>
        public static UiIcon FundIcon(string key)
        {
            string k = key ?? string.Empty;
            if (Has(k, "energy"))
            {
                return UiIcon.Lightning;
            }
            if (Has(k, "technology") || Has(k, "tech"))
            {
                return UiIcon.Cpu;
            }
            if (Has(k, "industries") || Has(k, "industry"))
            {
                return UiIcon.Factory;
            }
            if (Has(k, "bonds"))
            {
                return UiIcon.Scales;
            }
            if (Has(k, "global"))
            {
                return UiIcon.Globe;
            }
            return UiIcon.PiggyBank;
        }

        /// <summary>
        /// The step of an amount's minus and plus: about a tenth of it, rounded to 1, 2 or 5 of a power of ten, never under
        /// one, so a few presses walk any amount the way a person would count it.
        /// </summary>
        public static long Step(long value)
        {
            long v = Math.Max(10L, Math.Abs(value));
            double tenth = v / 10.0;
            double power = Math.Pow(10.0, Math.Floor(Math.Log10(tenth)));
            double unit = tenth / power;
            double nice = unit < 1.5 ? 1.0 : unit < 3.5 ? 2.0 : unit < 7.5 ? 5.0 : 10.0;
            return Math.Max(1L, (long)(nice * power));
        }

        /// <summary>An amount stepped down or up by its step and held between its ends.</summary>
        public static long Stepped(long value, int direction, long least, long most)
        {
            long next = value + direction * Step(value);
            return Math.Max(least, Math.Min(most, next));
        }

        // ------------------------------------------------------------------------------------------------- words

        /// <summary>A sum with its sign: a gain written with a plus, a loss as the game writes it.</summary>
        public static string Signed(float amount, Func<float, string> money) => amount > 0f ? "+" + money(amount) : money(amount);

        /// <summary>Money out without its minus: the column already says which way it went.</summary>
        public static string Unsigned(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return words;
            }
            int i = 0;
            while (i < words.Length && (words[i] == '-' || words[i] == '−' || words[i] == '+'))
            {
                i++;
            }
            return i == 0 ? words : words.Substring(i);
        }

        // A row's money on a day in words: from its figures where it has them, signed when asked; else as the game wrote it.
        private static string Figure(EconoIncomeRow row, int day, Func<float, string> money, bool signed)
        {
            if (row.Values != null)
            {
                float v = At(row.Values, day);
                return signed ? Signed(v, money) : money(v);
            }
            return row.Texts != null && day >= 0 && day < row.Texts.Length ? row.Texts[day] : null;
        }

        private static bool Zero(EconoIncomeRow row)
        {
            if (row.Values != null)
            {
                for (int i = 0; i < row.Values.Length; i++)
                {
                    if (Math.Abs(row.Values[i]) > 0.005f)
                    {
                        return false;
                    }
                }
                return true;
            }
            for (int i = 0; row.Texts != null && i < row.Texts.Length; i++)
            {
                string w = row.Texts[i] ?? string.Empty;
                for (int c = 0; c < w.Length; c++)
                {
                    if (char.IsDigit(w[c]) && w[c] != '0')
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // A day's words that say nothing was made or spent.
        private static bool Zero(string[] texts, int day)
        {
            string w = texts != null && day >= 0 && day < texts.Length ? texts[day] ?? string.Empty : string.Empty;
            for (int c = 0; c < w.Length; c++)
            {
                if (char.IsDigit(w[c]) && w[c] != '0')
                {
                    return false;
                }
            }
            return true;
        }

        private static float At(float[] values, int day) => values != null && day >= 0 && day < values.Length ? values[day] : 0f;

        // A day's head ("Yesterday (Day 411)") cut to its number for a column's head, which has a grid step of room.
        private static string Short(string day)
        {
            if (string.IsNullOrEmpty(day))
            {
                return day;
            }
            int open = day.IndexOf('(');
            int close = day.LastIndexOf(')');
            return open >= 0 && close > open ? day.Substring(open + 1, close - open - 1) : day;
        }

        private static string Pick(string[] options, int chosen) => options.Length == 0 ? string.Empty : options[Math.Max(0, Math.Min(chosen, options.Length - 1))];

        // A list's rows into the model it keeps between layouts, so its scroll stays where the player left it.
        private static UiPickListModel Keep(UiPickListModel held, UiPickRow[] rows, int chosen)
        {
            UiPickListModel m = held ?? new UiPickListModel();
            m.Rows = rows;
            m.Chosen = chosen;
            m.Title = null;
            return m;
        }

        private static bool Has(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Whole(int n) => n.ToString(Inv);

        /// <summary>A text's placeholders filled, names and values in turn.</summary>
        public static string Fill(string text, params string[] pairs)
        {
            string s = text ?? string.Empty;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                s = s.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return s;
        }
    }
}
