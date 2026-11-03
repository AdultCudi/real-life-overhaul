using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Utilities;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Fills a money page from the save when the day series or the tab changes, and carries a press to the feature that
    /// already owns the action. Nothing a frame.
    /// </summary>
    public sealed class MoneyLive : IMoneySource
    {
        public static readonly MoneyLive FinanceLive = new MoneyLive(MoneyApp.Finance);
        public static readonly MoneyLive BanksLive = new MoneyLive(MoneyApp.Banks);
        public static readonly MoneyLive OwnLive = new MoneyLive(MoneyApp.OwnBank);
        public static readonly MoneyLive UtilitiesLive = new MoneyLive(MoneyApp.Utilities);

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly MoneyApp _app;
        private readonly float[] _chart = new float[12];
        private int _amount = 250000;
        private int _rival;
        private int _local;

        private MoneyLive(MoneyApp app) { _app = app; }

        public int Generation => DaySeriesDirector.Generation + _local;

        public void Fill(MoneyModel model)
        {
            model.App = _app;
            model.Icon = _app == MoneyApp.Banks ? UiIcon.Bank : _app == MoneyApp.OwnBank ? UiIcon.PiggyBank : _app == MoneyApp.Utilities ? UiIcon.Globe : UiIcon.Wallet;
            bool act = (_app == MoneyApp.Finance && (model.Tab == 1 || model.Tab == 2)) || (_app == MoneyApp.Banks && model.Tab == 3) || (_app == MoneyApp.Utilities && model.Tab == 2);
            model.Prompts = Prompts(act);
            if (_app == MoneyApp.Finance) FillFinance(model);
            else if (_app == MoneyApp.Banks) FillBanks(model);
            else if (_app == MoneyApp.OwnBank) FillOwn(model);
            else FillUtilities(model);
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
            if (command == MoneyCommands.Choice) return Answer(arg);
            if (command == MoneyCommands.Export) return Export(model);
            if (command == MoneyCommands.Follow) return FollowTycoon();
            if (command == MoneyCommands.Link) return EconomyBridges.OpenEconomyApp != null && EconomyBridges.OpenEconomyApp(0);
            if (command == UiDataTable.First + UiDataTable.Row && _app == MoneyApp.Finance && model.Tab == 5)
            {
                _rival = arg;
                _local++;
                return true;
            }
            if (command == UiAppWindow.ActionCommand) return Act(model);
            return false;
        }

        private void FillFinance(MoneyModel model)
        {
            model.Title = T("reallife_econ_app");
            model.Across = false;
            model.Nav = new[]
            {
                Nav("reallife_econ_tab_overview", UiIcon.IdCard), Nav("reallife_econ_tab_credit", UiIcon.Star), Nav("reallife_econ_tab_borrow", UiIcon.Bank),
                Nav("reallife_econ_tab_cash", UiIcon.ChartLineUp), Nav("reallife_econ_tab_accounts", UiIcon.Wallet), Nav("reallife_rival_title", UiIcon.Trophy), Nav("reallife_fame_title", UiIcon.Star)
            };
            model.Tab = Clamp(model.Tab, 6);
            if (model.Tab == 0) Overview(model);
            else if (model.Tab == 1 || model.Tab == 2) Offer(model);
            else if (model.Tab == 5) LeaguePage(model);
            else if (model.Tab == 6) { model.Shape = MoneyShape.Factors; Factors(model); model.Note = Threshold(); }
            else Flow(model, model.Tab == 4);
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
            if (model.Tab == 3) ApplyPage(model);
            else if (model.Tab == 2 || model.Tab == 4) { model.Shape = MoneyShape.Factors; Factors(model); model.Note = Threshold(); }
            else { model.Shape = MoneyShape.Flow; model.Statement = CashStatement(false); }
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
            if (model.Tab == 3) FundPage(model, bank);
            else if (model.Tab == 0) Home(model, bank);
            else { model.Shape = MoneyShape.Table; model.Table = model.Tab == 4 ? BranchRows(bank) : Biggest(bank, model.Tab == 2); }
        }

        private void FillUtilities(MoneyModel model)
        {
            model.Title = T("reallife_util_app");
            model.Across = true;
            model.Nav = new[] { Nav("reallife_util_tab_accounts", UiIcon.Wallet), Nav("reallife_util_tab_internet", UiIcon.Globe), Nav("reallife_util_tab_network", UiIcon.HardDrives) };
            model.Tab = Clamp(model.Tab, 2);
            Network network = UtilitiesDirector.Instance != null && UtilitiesDirector.Instance.Company != null ? UtilitiesDirector.Instance.Company.Network : null;
            if (model.Tab == 1) { model.Shape = MoneyShape.Plans; model.Catalogue = Plans(); }
            else if (model.Tab == 2) NetworkPage(model, network);
            else { model.Shape = MoneyShape.Flow; model.Statement = CashStatement(false); }
        }

        private void Overview(MoneyModel model)
        {
            int score = CreditLedger.Value;
            model.Shape = MoneyShape.Overview;
            model.Profile = new UiProfileModel
            {
                Name = T("reallife_money_you"), Hue = 28f, Role = T(CreditCatalogue.GradeKeys[(int)CreditCatalogue.GradeOf(score)]), RoleIcon = UiIcon.IdCard,
                Status = score.ToString(Inv), StatusTone = score >= CreditCatalogue.LendingThreshold ? UiRole.Ok : UiRole.Warn,
                FactLabels = new[] { T("reallife_money_owed"), T("reallife_money_cash") },
                FactValues = new[] { Money(OwedTotal()), Money(RivalGame.Cash) },
                RatingLabel = "Score", Rating = Stars(score)
            };
            model.Note = Leaves();
            model.DonutTitle = T("reallife_money_owed");
            OwedSplit(out float[] shares, out string[] labels, out string[] values);
            model.Donut = shares; model.DonutLabels = labels; model.DonutValues = values;
            model.Actions = new[] { new UiActionModel { Label = T("reallife_money_economy"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Locate, Key = UiAction.Option, Command = MoneyCommands.Link } };
            model.Table = Debts();
        }

        private void Offer(MoneyModel model)
        {
            int pay = DaySeriesRules.DailyPayment(_amount, 480, 365 * 5);
            model.Shape = MoneyShape.Offer;
            model.Hero = new UiHeroModel { Icon = UiIcon.Bank, Kicker = T("reallife_econ_tab_borrow"), Title = BankName(0), Line = "4.8%", Note = Money(pay), NoteIcon = UiIcon.Coin, Progress = -1 };
            model.Amount = Money(_amount); model.CanLess = _amount > 1000; model.CanMore = _amount < 5000000;
            model.Note = T("reallife_money_all", "all", Money((long)pay * 365 * 5), "interest", Money(DaySeriesRules.Interest(_amount, pay, 365 * 5)));
            model.Note2 = T("reallife_money_adds", "add", Money(pay), "already", Money(DailyOut()));
            model.Actions = new[] { new UiActionModel { Label = T("reallife_money_take"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand } };
            model.Table = LoanFeed();
        }

        private void Flow(MoneyModel model, bool accounts)
        {
            model.Shape = MoneyShape.Flow;
            model.Chart = Chart(DaySeriesCatalogue.Cash, T("reallife_money_where"));
            model.Note = T("reallife_money_coming");
            model.Statement = CashStatement(accounts);
            model.Actions = new[] { new UiActionModel { Label = T("reallife_money_export"), Kind = UiButtonKind.Secondary, Icon = UiIcon.ClipboardText, Key = UiAction.Option, Command = MoneyCommands.Export } };
        }

        private void LeaguePage(MoneyModel model)
        {
            model.Shape = MoneyShape.League;
            model.Table = Rich();
            model.Profile = new UiProfileModel
            {
                Name = RivalCatalogue.Tycoons[0].Name, Hue = 320f, Role = RivalCatalogue.Tycoons[0].Holding, RoleIcon = UiIcon.Rocket,
                Status = T("reallife_money_expanding"), StatusTone = UiRole.Warn, Rating = 6, RatingLabel = "Place",
                FactLabels = new[] { T("reallife_money_worth") }, FactValues = new[] { Money(0L) }
            };
            model.Chart = Chart(DaySeriesCatalogue.WorthYou, T("reallife_money_worth"));
            model.Choices = new[] { T("reallife_money_cut"), T("reallife_money_campaign"), T("reallife_money_hold") };
            string line = T("reallife_money_forecast", "count", DaySeriesRules.CampaignForecast(0, 40).ToString(Inv));
            model.ChoiceBody = new[] { line, line, T("reallife_money_hold") };
            model.Actions = new[] { new UiActionModel { Label = T("reallife_money_follow"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Megaphone, Key = UiAction.Option, Command = MoneyCommands.Follow } };
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
            model.Actions = new[] { new UiActionModel { Label = T("reallife_money_apply"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand } };
            model.Table = BanksTable();
        }

        private static void Home(MoneyModel model, OwnBank bank)
        {
            model.Shape = MoneyShape.Home;
            model.Hero = new UiHeroModel { Icon = UiIcon.PiggyBank, Kicker = T("reallife_ob_tab_bank"), Title = T("reallife_ob_tab_bank"), Progress = bank != null ? 40 : 0, Note = T("reallife_money_month"), NoteIcon = UiIcon.Flag };
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

        private void NetworkPage(MoneyModel model, Network network)
        {
            int customers = network != null && network.Running ? BroadbandRules.Connections(network) : 0;
            model.Shape = MoneyShape.Network;
            model.Hero = new UiHeroModel { Icon = UiIcon.HardDrives, Kicker = T("reallife_util_tab_network"), Title = T("reallife_util_tab_network"), Line = customers.ToString(Inv), Progress = network != null && network.Running ? 40 : 0, Note = T("reallife_money_dig"), NoteIcon = UiIcon.Wrench };
            model.Figures = new[] { new UiFigureModel { Label = T("reallife_money_customers"), Value = customers.ToString(Inv) } };
            model.Chart = Chart(DaySeriesCatalogue.Customers, T("reallife_money_customers"));
            model.Table = DistrictRows(network);
            model.Actions = new[] { new UiActionModel { Label = T("reallife_money_dig"), Kind = UiButtonKind.Primary, Icon = UiIcon.Wrench, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand } };
        }

        private bool Act(MoneyModel model)
        {
            if (_app == MoneyApp.Finance && (model.Tab == 1 || model.Tab == 2) && EconomyDirector.Instance != null)
            {
                EconomyDirector.Instance.KitBorrow(_amount);
                return true;
            }
            if (_app == MoneyApp.Banks && model.Tab == 3 && BanksDirector.Instance != null)
            {
                BanksDirector.Instance.KitApply(_amount);
                return true;
            }
            return _app == MoneyApp.Utilities && model.Tab == 2 && UtilitiesDirector.Instance != null && UtilitiesDirector.Instance.KitDig();
        }

        private bool Answer(int which)
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            int day = RivalGame.Day;
            if (book == null || day <= 0) return false;
            if (which == 0)
            {
                book.CutFrom = day;
                book.CutUntil = day + DaySeriesRules.CutDays;
                if (CompanyDirector.Instance != null) CompanyDirector.Instance.CutPlans(DaySeriesRules.CutPercent);
            }
            else if (which == 2 && book.CutFrom > 0)
            {
                if (CompanyDirector.Instance != null) CompanyDirector.Instance.ReleaseCut(book.HeldAlready);
                book.CutFrom = 0;
                book.CutUntil = 0;
            }
            _local++;
            return true;
        }

        private bool FollowTycoon()
        {
            if (FlareDirector.Instance == null || _rival < 0 || _rival >= RivalCatalogue.TycoonCount) return false;
            FlareDirector.Instance.Follow("t:" + RivalCatalogue.Tycoons[_rival].Name, true);
            return true;
        }

        private static bool Export(MoneyModel model)
        {
            if (model.Statement == null || model.Statement.Groups == null) return false;
            var text = new StringBuilder();
            text.Append("when,what,in,out,balance\n");
            for (int g = 0; g < model.Statement.Groups.Length; g++)
            {
                UiStatementLine[] lines = model.Statement.Groups[g].Lines;
                for (int i = 0; lines != null && i < lines.Length; i++)
                {
                    UiStatementLine line = lines[i];
                    text.Append(Csv(line.When)).Append(',').Append(Csv(line.What)).Append(',').Append(Csv(line.In)).Append(',').Append(Csv(line.Out)).Append(',').Append(Csv(line.Balance)).Append('\n');
                }
            }
            File.WriteAllText(Path.Combine(RealLifeOverhaul.Meta.Presets.Folder, "statement.csv"), text.ToString(), new UTF8Encoding(false));
            return true;
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
            return new UiChartModel { Kind = UiChartKind.Line, Title = title, Points = points, Series = new[] { new UiSeries { Label = title, Values = values } }, Format = v => ((int)v).ToString(Inv) };
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

        private static UiDataTableModel Debts()
        {
            var rows = new List<UiDataRow>(8);
            CreditFile file = CreditLedger.File;
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
            {
                CreditAccount account = file.Accounts[i];
                if (account == null || account.Closed || account.Owed <= 0) continue;
                rows.Add(new UiDataRow { Cells = new[] { T(account.NameKey), Money(account.Owed), (account.RateNow / 100f).ToString("0.#", Inv), account.NextDay.ToString(Inv) } });
            }
            return Make(rows, new[] { "Debt", T("reallife_money_owed"), "Rate", "Day" }, T("reallife_money_empty_debts"), T("reallife_money_empty_loans"), UiIcon.Bank);
        }

        private static UiDataTableModel LoanFeed()
        {
            DaySeriesBook book = DaySeriesDirector.Book;
            var rows = new List<UiDataRow>(8);
            int n = book != null ? book.LoanCount : 0;
            for (int i = n - 1; i >= 0 && rows.Count < 6; i--)
            {
                DayLoan loan = book.LoanAt(i);
                rows.Add(new UiDataRow { Cells = new[] { loan.Who, BankName(loan.Rank), loan.Day.ToString(Inv), Money(loan.Amount) } });
            }
            return Make(rows, new[] { T("reallife_money_feed"), "Bank", "Day", "Amount" }, T("reallife_money_feed_empty"), T("reallife_money_feed"), UiIcon.People);
        }

        private UiDataTableModel Rich()
        {
            var into = new List<RichRow>(12);
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            RivalRules.RichList(file, RivalGame.Worth(), Prices.Level, T("reallife_money_you"), into);
            var rows = new List<UiDataRow>(into.Count);
            for (int i = 0; i < into.Count; i++)
            {
                RichRow row = into[i];
                rows.Add(new UiDataRow
                {
                    Cells = new[] { string.IsNullOrEmpty(row.Name) ? T("reallife_money_you") : row.Name, Money(row.Worth), row.Worth >= row.WeekAgo ? "Up" : "Down" },
                    Values = new[] { 0, 0, row.Worth >= row.WeekAgo ? 1 : -1 },
                    Hue = 40f + i * 12f, Ring = UiRole.Ok
                });
            }
            UiDataTableModel table = Make(rows, new[] { "Name", T("reallife_money_worth"), "30" }, T("reallife_money_worth"), T("reallife_rival_title"), UiIcon.Trophy);
            table.Columns[0].Kind = UiCellKind.Person;
            table.Columns[2].Kind = UiCellKind.Trend;
            table.Selected = _rival >= 0 && _rival < rows.Count ? _rival : 0;
            return table;
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
                for (int i = 0; i < names.Length; i++) names[i] = RivalCatalogue.Tycoons[i].Holding;
                DaySeriesRules.Split(DaySeriesRules.DefaultSeed, pool > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, pool), names, parts);
                for (int i = 0; i < names.Length; i++)
                {
                    if (parts[i] > 0) rows.Add(new UiDataRow { Cells = new[] { names[i], Money(parts[i]), "5%", "1" } });
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
                    rows.Add(new UiDataRow { Cells = new[] { T("reallife_branch_tab"), (i + 1).ToString(Inv), T("reallife_money_live") }, Tones = new[] { UiRole.None, UiRole.None, UiRole.Ok } });
            }
            UiDataTableModel table = Make(rows, new[] { "Branch", "Customers", "State" }, T("reallife_branch_tab"), T("reallife_money_locate"), UiIcon.Building);
            table.Columns[2].Kind = UiCellKind.Chip;
            return table;
        }

        private static UiDataTableModel DistrictRows(Network network)
        {
            var rows = new List<UiDataRow>(4);
            if (network != null)
            {
                int day = RivalGame.Day;
                for (int i = 0; i < Network.Districts; i++)
                {
                    if (!network.Open(i, day) && !network.Digging(i)) continue;
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { BroadbandCompany.District(i), BroadbandRules.Connections(network, i).ToString(Inv), network.Digging(i) ? network.ReadyDay(i).ToString(Inv) : T("reallife_money_live") },
                        Tones = new[] { UiRole.None, UiRole.None, network.Digging(i) ? UiRole.Warn : UiRole.Ok }
                    });
                }
            }
            UiDataTableModel table = Make(rows, new[] { "District", T("reallife_money_customers"), "State" }, T("reallife_util_tab_network"), T("reallife_money_dig"), UiIcon.HardDrives);
            table.Columns[2].Kind = UiCellKind.Chip;
            return table;
        }

        private static UiCatalogueModel Plans()
        {
            var model = new UiCatalogueModel { EmptyTitle = T("reallife_util_tab_internet"), EmptyBody = T("reallife_money_plan"), EmptyIcon = UiIcon.Globe };
            Provider provider = InternetCatalogue.Providers[0];
            int n = provider.Mbps != null ? provider.Mbps.Length : 0;
            if (n == 0) return model;
            model.Cards = new UiCatalogueCard[n];
            for (int i = 0; i < n; i++)
            {
                int cents = provider.MonthCents != null && i < provider.MonthCents.Length ? provider.MonthCents[i] : 0;
                model.Cards[i] = new UiCatalogueCard { Icon = UiIcon.Globe, Name = provider.Mbps[i].ToString(Inv), Figure = Money(cents / 100) };
            }
            model.CardCount = n;
            model.Chosen = 0;
            model.DetailKicker = T(provider.NameKey);
            model.DetailLabels = new[] { "Mb" };
            model.DetailValues = new[] { provider.Mbps[0].ToString(Inv) };
            return model;
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
            if (RivalGame.Day > 0) flow.Read(CashFlow.Week, RivalGame.Day, null);
            if (flow.Entries.Count == 0) return model;
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

        private static void OwedSplit(out float[] shares, out string[] labels, out string[] values)
        {
            var sums = new float[(int)ProductKind.Count];
            CreditFile file = CreditLedger.File;
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
            {
                CreditAccount account = file.Accounts[i];
                if (account != null && !account.Closed && account.Owed > 0 && (int)account.Kind < sums.Length) sums[(int)account.Kind] += account.Owed;
            }
            int n = 0;
            for (int i = 0; i < sums.Length; i++) n += sums[i] > 0f ? 1 : 0;
            shares = new float[n]; labels = new string[n]; values = new string[n];
            int at = 0;
            for (int i = 0; i < sums.Length; i++)
            {
                if (sums[i] <= 0f) continue;
                shares[at] = sums[i];
                labels[at] = T(ProductCatalogue.KindKeys[i]);
                values[at] = Money(sums[i]);
                at++;
            }
        }

        private static float OwedTotal()
        {
            float sum = 0f;
            CreditFile file = CreditLedger.File;
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
                if (file.Accounts[i] != null && !file.Accounts[i].Closed) sum += file.Accounts[i].Owed;
            return sum;
        }

        private static float DailyOut()
        {
            float sum = 0f;
            CreditFile file = CreditLedger.File;
            for (int i = 0; file != null && i < file.Accounts.Count; i++)
                if (file.Accounts[i] != null) sum += file.Accounts[i].PaymentDue;
            return sum;
        }

        private static string Leaves()
        {
            float due = DailyOut();
            return due > 0f ? T("reallife_money_leaves", "money", Money(due)) : null;
        }

        private static string BankName(int rank)
        {
            if (BankTerms.Banks != null && rank >= 0 && rank < BankTerms.Banks.Count && !string.IsNullOrEmpty(BankTerms.Banks[rank].Name)) return BankTerms.Banks[rank].Name;
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
            for (int i = 0; i < steps.Length; i++) if (steps[i] <= amount) at = i;
            return steps[up ? Math.Min(steps.Length - 1, at + 1) : Math.Max(0, at - 1)];
        }

        private static long Sum(long[] values)
        {
            long sum = 0;
            for (int i = 0; values != null && i < values.Length; i++) sum += values[i];
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
                columns[i] = new UiDataColumn { Title = titles[i], Kind = i == 0 ? UiCellKind.Text : UiCellKind.Figure, Width = i == 0 ? 0f : UiTokens.S16 * 2f };
            return new UiDataTableModel { Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = icon };
        }

        private static UiNavEntry Nav(string key, UiIcon icon) => new UiNavEntry { Icon = icon, Label = T(key) };

        private static UiPrompt[] Prompts(bool act)
        {
            var list = new UiPrompt[act ? 4 : 3];
            list[0] = new UiPrompt { Action = UiAction.Move, Label = T("reallife_ui_prompt_move") };
            list[1] = new UiPrompt { Action = UiAction.Confirm, Label = T("reallife_ui_prompt_select") };
            list[2] = new UiPrompt { Action = UiAction.Back, Label = T("reallife_ui_prompt_back") };
            if (act) list[3] = new UiPrompt { Action = UiAction.Alt, Label = T("reallife_ui_prompt_choose") };
            return list;
        }

        private static string Money(float amount) => FinanceText.Money(amount);
        private static string Money(long amount) => FinanceText.Money((float)amount);
        private static string T(string key) => FinanceText.T(key);
        private static string T(string key, string name, string value) => FinanceText.T(key, name, value);
        private static string T(string key, string a, string av, string b, string bv) => FinanceText.T(key, a, av, b, bv);
        private static string Csv(string text) => string.IsNullOrEmpty(text) ? string.Empty : text.Replace(',', ' ');
    }
}
