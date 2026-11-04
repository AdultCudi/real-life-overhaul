using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Fills a money page from the save when the day series or the tab changes, and carries a press to the feature that
    /// already owns the action. Nothing a frame.
    /// </summary>
    public sealed class MoneyLive : IMoneySource
    {
        public static readonly MoneyLive BanksLive = new MoneyLive(MoneyApp.Banks);
        public static readonly MoneyLive OwnLive = new MoneyLive(MoneyApp.OwnBank);

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly MoneyApp _app;
        private readonly float[] _chart = new float[12];
        private int _amount = 250000;
        private int _local;

        private MoneyLive(MoneyApp app) { _app = app; }

        public int Generation => DaySeriesDirector.Generation + _local;

        public void Fill(MoneyModel model)
        {
            model.App = _app;
            model.Icon = _app == MoneyApp.Banks ? UiIcon.Bank : _app == MoneyApp.OwnBank ? UiIcon.PiggyBank : UiIcon.Wallet;
            bool act = _app == MoneyApp.Banks && model.Tab == 3;
            model.Prompts = Prompts(act);
            if (_app == MoneyApp.Banks)
            {
                FillBanks(model);
            }
            else
            {
                FillOwn(model);
            }
            if (string.IsNullOrEmpty(model.EmptyTitle))
            {
                model.EmptyTitle = T("reallife_money_empty_debts");
                model.EmptyBody = T("reallife_money_empty_loans");
                model.EmptyIcon = model.Icon;
            }
        }

        public bool Press(MoneyModel model, int command, int arg)
        {
            if (command == MoneyCommands.StepLess || command == MoneyCommands.StepMore)
            {
                _amount = Step(_amount, command == MoneyCommands.StepMore);
                _local++;
                return true;
            }
            if (command == UiAppWindow.ActionCommand)
            {
                return Act(model);
            }
            return false;
        }

        private void FillBanks(MoneyModel model)
        {
            model.Title = T("reallife_banks_app");
            model.Across = true;
            model.Nav = new[]
            {
                Nav("reallife_bank_tab_accounts", UiIcon.Wallet), Nav("reallife_bank_tab_loans", UiIcon.Bank), Nav("reallife_bank_tab_card", UiIcon.IdCard),
                Nav("reallife_bank_tab_apply", UiIcon.Check), Nav("reallife_bank_tab_rating", UiIcon.Star)
            };
            model.Tab = Clamp(model.Tab, 4);
            model.Profile = new UiProfileModel
            {
                Name = BankName(0), Hue = 200f, RoleIcon = UiIcon.Bank, Status = T("reallife_money_since"), StatusTone = UiRole.Ok,
                FactLabels = new[] { "Rate", T("reallife_money_since") }, FactValues = new[] { "4.8%", DayText() },
                RatingLabel = T("reallife_money_since"), Rating = Stars(CreditLedger.Value)
            };
            if (model.Tab == 3)
            {
                ApplyPage(model);
            }
            else if (model.Tab == 2 || model.Tab == 4)
            {
                model.Shape = MoneyShape.Factors;
                Factors(model);
                model.Note = Threshold();
            }
            else
            {
                model.Shape = MoneyShape.Flow;
                model.Statement = CashStatement(false);
            }
        }

        private void FillOwn(MoneyModel model)
        {
            model.Title = T("reallife_ob_app");
            model.Nav = new[]
            {
                Nav("reallife_ob_tab_bank", UiIcon.Bank), Nav("reallife_ob_tab_savers", UiIcon.PiggyBank), Nav("reallife_ob_tab_lending", UiIcon.Coin),
                Nav("reallife_ob_tab_fund", UiIcon.ChartPie), Nav("reallife_branch_tab", UiIcon.Building)
            };
            model.Tab = Clamp(model.Tab, 4);
            OwnBank bank = OwnBankDirector.Instance != null ? OwnBankDirector.Instance.Bank : null;
            if (model.Tab == 3)
            {
                FundPage(model, bank);
            }
            else if (model.Tab == 0)
            {
                Home(model, bank);
            }
            else
            {
                model.Shape = MoneyShape.Table;
                model.Table = model.Tab == 4 ? BranchRows(bank) : Biggest(bank, model.Tab == 2);
            }
        }

        private void ApplyPage(MoneyModel model)
        {
            model.Shape = MoneyShape.Apply;
            model.Amount = Money(_amount); model.CanLess = _amount > 1000; model.CanMore = _amount < 5000000;
            model.Meter = DaySeriesRules.Headroom(CreditLedger.Value, CreditCatalogue.LendingThreshold, CreditCatalogue.ScoreMax);
            model.MeterLabel = T("reallife_money_headroom");
            model.Lines = new[] { Threshold(), T("reallife_money_cover") };
            model.LineOn = new[] { CreditLedger.Value >= CreditCatalogue.LendingThreshold, true };
            model.Note2 = T("reallife_money_other_banks");
            model.Actions = new[]
            {
                new UiActionModel
                {
                    Label = T("reallife_money_apply"),
                    Kind = UiButtonKind.Primary,
                    Icon = UiIcon.Check,
                    Key = UiAction.Alt,
                    Command = UiAppWindow.ActionCommand
                }
            };
            model.Table = BanksTable();
        }

        private static void Home(MoneyModel model, OwnBank bank)
        {
            model.Shape = MoneyShape.Home;
            model.Hero = new UiHeroModel
            {
                Icon = UiIcon.PiggyBank,
                Kicker = T("reallife_ob_tab_bank"),
                Title = T("reallife_ob_tab_bank"),
                Progress = bank != null ? 40 : 0,
                Note = T("reallife_money_month"),
                NoteIcon = UiIcon.Flag
            };
            model.Note = T("reallife_money_month");
            model.Lines = new[] { T("reallife_money_month") };
            model.LineOn = new[] { bank != null };
        }

        private void FundPage(MoneyModel model, OwnBank bank)
        {
            model.Shape = MoneyShape.Fund;
            model.Chart = Chart(DaySeriesCatalogue.Fund, T("reallife_money_fund_days"));
            long fund = bank != null ? bank.FundValue / 100L : 0L;
            long reserve = bank != null ? bank.Reserves / 100L : 0L;
            model.DonutTitle = T("reallife_ob_tab_fund");
            model.Donut = fund <= 0 ? new float[0] : new[] { (float)reserve, (float)Math.Max(0L, fund - reserve) };
            model.DonutLabels = new[] { "Cash", "Shares" };
            model.DonutValues = new[] { Money(reserve), Money(fund - reserve) };
            model.Table = Biggest(bank, true);
        }

        private bool Act(MoneyModel model)
        {
            if (_app == MoneyApp.Banks && model.Tab == 3 && BanksDirector.Instance != null)
            {
                BanksDirector.Instance.KitApply(_amount);
                return true;
            }
            return false;
        }

        private UiChartModel Chart(string key, string title)
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            int day = RivalGame.Day > 0 ? RivalGame.Day : book != null ? book.LastDay : 0;
            int found = book != null && day > 0 ? DaySeriesRules.Chart(book, key, day, _chart.Length, _chart) : 0;
            if (found == 0)
            {
                return new UiChartModel { Kind = UiChartKind.Line, Title = title, EmptyTitle = T("reallife_money_cash_empty"), EmptyBody = T("reallife_money_coming") };
            }
            var values = new float[_chart.Length];
            var points = new string[_chart.Length];
            for (int i = 0; i < _chart.Length; i++)
            {
                values[i] = _chart[i];
                points[i] = (i + 1).ToString(Inv);
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line,
                Title = title,
                Points = points,
                Series = new[] { new UiSeries { Label = title,
                Values = values } },
                Format = v => ((int)v).ToString(Inv)
            };
        }

        private static void Factors(MoneyModel model)
        {
            CreditScore score = CreditLedger.Score;
            int n = (int)CreditFactor.Count;
            model.Lines = new string[n];
            model.Marks = new int[n];
            for (int i = 0; i < n; i++)
            {
                model.Lines[i] = T(CreditCatalogue.FactorKeys[i]);
                model.Marks[i] = score != null ? (int)Math.Round(score.Factor[i] * 100f) : 0;
            }
        }

        private static UiDataTableModel BanksTable()
        {
            var rows = new List<UiDataRow>(3);
            int n = BankTerms.Banks != null ? Math.Min(BankRules.AppsMost, BankTerms.Banks.Count) : 0;
            for (int i = 0; i < n; i++)
            {
                bool yes = CreditLedger.Value >= CreditCatalogue.LendingThreshold;
                rows.Add(new UiDataRow { Cells = new[] { BankTerms.Banks[i].Name, "4.8%", yes ? "Yes" : "No" }, Tones = new[] { UiRole.None, UiRole.None, yes ? UiRole.Ok : UiRole.Danger } });
            }
            UiDataTableModel table = Make(rows, new[] { "Bank", "Rate", "Answer" }, T("reallife_money_other_banks"), Threshold(), UiIcon.Bank);
            table.Columns[2].Kind = UiCellKind.Chip;
            return table;
        }

        private static UiDataTableModel Biggest(OwnBank bank, bool lending)
        {
            var rows = new List<UiDataRow>(3);
            if (bank != null)
            {
                long pool = lending ? bank.Lent : Sum(bank.Deposits);
                var names = new string[RivalCatalogue.TycoonCount];
                var parts = new int[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = RivalCatalogue.Tycoons[i].Holding;
                }
                DaySeriesRules.Split(DaySeriesRules.DefaultSeed, pool > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, pool), names, parts);
                for (int i = 0; i < names.Length; i++)
                {
                    if (parts[i] > 0)
                    {
                        rows.Add(new UiDataRow { Cells = new[] { names[i], Money(parts[i]), "5%", "1" } });
                    }
                }
            }
            return Make(rows, new[] { lending ? "Borrower" : "Saver", "Balance", "Rate", T("reallife_money_since") }, T("reallife_money_biggest"), T("reallife_money_biggest"), UiIcon.People);
        }

        private static UiDataTableModel BranchRows(OwnBank bank)
        {
            var rows = new List<UiDataRow>(4);
            if (bank != null)
            {
                for (int i = 0; i < bank.Branches.Count; i++)
                {
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { T("reallife_branch_tab"), (i + 1).ToString(Inv), T("reallife_money_live") },
                        Tones = new[] { UiRole.None, UiRole.None, UiRole.Ok }
                    });
                }
            }
            UiDataTableModel table = Make(rows, new[] { "Branch", "Customers", "State" }, T("reallife_branch_tab"), T("reallife_money_locate"), UiIcon.Building);
            table.Columns[2].Kind = UiCellKind.Chip;
            return table;
        }

        private static UiStatementModel CashStatement(bool accounts)
        {
            var model = new UiStatementModel
            {
                Account = accounts ? T("reallife_econ_tab_accounts") : T("reallife_econ_tab_cash"),
                AccountIcon = UiIcon.Wallet, BalanceLabel = T("reallife_money_cash"), Balance = Money(RivalGame.Cash), PeriodLabel = "30",
                Columns = new[] { T("reallife_money_col_when"), T("reallife_money_col_what"), T("reallife_money_col_in"), T("reallife_money_col_out"), T("reallife_money_col_balance") },
                EmptyTitle = T("reallife_money_cash_empty"), EmptyBody = T("reallife_money_coming")
            };
            var flow = new CashFlow();
            if (RivalGame.Day > 0)
            {
                flow.Read(CashFlow.Week, RivalGame.Day, null);
            }
            if (flow.Entries.Count == 0)
            {
                return model;
            }
            var lines = new List<UiStatementLine>(8);
            float balance = RivalGame.Cash;
            for (int i = flow.Entries.Count - 1; i >= 0 && lines.Count < 8; i--)
            {
                CashEntry entry = flow.Entries[i];
                lines.Add(new UiStatementLine
                {
                    When = entry.Day.ToString(Inv), Icon = UiIcon.Coin, What = T(CashCategory.Keys[entry.Category]),
                    In = entry.Amount > 0f ? Money(entry.Amount) : null, Out = entry.Amount < 0f ? Money(-entry.Amount) : null, Balance = Money(balance)
                });
                balance -= entry.Amount;
            }
            lines.Reverse();
            model.Groups = new[] { new UiStatementGroup { Label = T("reallife_money_brought"), Total = Money(RivalGame.Cash), TotalGood = true, Lines = lines.ToArray() } };
            model.TotalLabels = new[] { T("reallife_money_col_in"), T("reallife_money_col_out") };
            model.Totals = new[] { Money(flow.In), Money(-flow.Out) };
            model.ChangeGood = flow.In + flow.Out >= 0f;
            return model;
        }

        private static string BankName(int rank)
        {
            if (BankTerms.Banks != null && rank >= 0 && rank < BankTerms.Banks.Count && !string.IsNullOrEmpty(BankTerms.Banks[rank].Name))
            {
                return BankTerms.Banks[rank].Name;
            }
            return T("reallife_banks_app");
        }

        private static string Threshold() => T("reallife_money_threshold", "threshold", CreditCatalogue.LendingThreshold.ToString(Inv));

        private static int Stars(int score)
        {
            int halves = (score - 300) * 10 / 550;
            return halves < 0 ? 0 : halves > 10 ? 10 : halves;
        }

        private static int Step(int amount, bool up)
        {
            int[] steps = { 1000, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000 };
            int at = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] <= amount)
                {
                    at = i;
                }
            }
            return steps[up ? Math.Min(steps.Length - 1, at + 1) : Math.Max(0, at - 1)];
        }

        private static long Sum(long[] values)
        {
            long sum = 0;
            for (int i = 0; values != null && i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum;
        }

        private static string DayText()
        {
            int day = RivalGame.Day;
            return day > 0 ? day.ToString(Inv) : "0";
        }

        private static int Clamp(int tab, int most) => tab < 0 ? 0 : tab > most ? most : tab;

        private static UiDataTableModel Make(List<UiDataRow> rows, string[] titles, string emptyTitle, string emptyBody, UiIcon icon)
        {
            var columns = new UiDataColumn[titles.Length];
            for (int i = 0; i < titles.Length; i++)
            {
                columns[i] = new UiDataColumn
                {
                    Title = titles[i],
                    Kind = i == 0 ? UiCellKind.Text : UiCellKind.Figure,
                    Width = i == 0 ? 0f : UiTokens.S16 * 2f
                };
            }
            return new UiDataTableModel { Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon };
        }

        private static UiNavEntry Nav(string key, UiIcon icon) => new UiNavEntry { Icon = icon, Label = T(key) };

        private static UiPrompt[] Prompts(bool act)
        {
            var list = new UiPrompt[act ? 4 : 3];
            list[0] = new UiPrompt { Action = UiAction.Move, Label = T("reallife_ui_prompt_move") };
            list[1] = new UiPrompt { Action = UiAction.Confirm, Label = T("reallife_ui_prompt_select") };
            list[2] = new UiPrompt { Action = UiAction.Back, Label = T("reallife_ui_prompt_back") };
            if (act)
            {
                list[3] = new UiPrompt { Action = UiAction.Alt, Label = T("reallife_ui_prompt_choose") };
            }
            return list;
        }

        private static string Money(float amount) => FinanceText.Money(amount);
        private static string Money(long amount) => FinanceText.Money((float)amount);
        private static string T(string key) => FinanceText.T(key);
        private static string T(string key, string name, string value) => FinanceText.T(key, name, value);
        private static string T(string key, string a, string av, string b, string bv) => FinanceText.T(key, a, av, b, bv);
    }
}
