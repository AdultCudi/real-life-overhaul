using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// One bank's app: that bank's slice of the player's money in the game's frame, five tabs (Accounts, Loans, Card, Apply,
    /// Rating). It keeps nothing: figures come from the credit file and the game's loans, actions go to the credit desk.
    /// Cost: nothing while closed; a page is written when it opens, a tab is picked or something is done.
    /// </summary>
    public sealed partial class BankApp
    {
        public const int Accounts = 0, Loans = 1, Card = 2, Apply = 3, Rating = 4;
        private const int TypedAmountLength = 9;
        private const string GuideTitle = "reallife_meta_title_economy_banks";   // the Guide's page every page of the app points to

        private static readonly string[] TabKeys = { "reallife_bank_tab_accounts", "reallife_bank_tab_loans", "reallife_bank_tab_card", "reallife_bank_tab_apply", "reallife_bank_tab_rating" };
        private static readonly int[] AmountSteps = { 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000, 2500000, 5000000 };
        private static readonly string[] ProfileKeys = { "reallife_bank_profile_0", "reallife_bank_profile_1", "reallife_bank_profile_2" };

        public static IModLogger Logger;

        private readonly EconomyDirector _director;
        private readonly BankTables _tables;
        private readonly string _id;
        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);
        private readonly List<Debt> _debts = new List<Debt>(16);
        private readonly List<string> _names = new List<string>(8);
        private readonly List<int> _amounts = new List<int>(16);
        private readonly CreditFactor[] _tips = new CreditFactor[2];
        private readonly CashFlow _flow = new CashFlow();
        private readonly CashTable _cash;                                   // the statement, written as it scrolls
        private readonly Func<Dictionary<string, string>, bool> _mine, _ofCard;   // made once: what a read of the history keeps
        private string _cardName;   // the card whose movements are read
        private bool _cashRows;     // the table showing is the statement

        private AppShell _shell;
        private DataTable _table;
        private TableSpec _spec;
        private FullMenu _menu;
        private int _tab, _wanted = -1;
        private int _period = CashFlow.Week;
        private bool _ready;

        /// <summary>The bank this app is for (one branch of it), set by the director.</summary>
        public Lender Bank;

        public BankApp(EconomyDirector director, string id)
        {
            _director = director;
            _id = id;
            _tables = new BankTables(id);
            _spec = _tables.Off;
            _cash = new CashTable(_flow.Entries, CashWords.Game);
            _mine = Mine;
            _ofCard = data =>
            {
                string value;
                return data != null && data.TryGetValue("name", out value) && string.Equals(value, _cardName, StringComparison.Ordinal);
            };
        }

        private CreditDesk Desk => _director.Desk;
        private bool IsHouse => Bank != null && BankTerms.House != null && Bank.Rank == BankTerms.House.Rank;
        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;
        private static float Multiplier => SaveGameManager.Current != null && SaveGameManager.Current.gameVariables != null ? SaveGameManager.Current.gameVariables.bankInterestMultiplier : 1f;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            _menu = menu;
            using var piece = SessionWatch.Piece("banks: an app being built from the game's MyEmployees");   // once per bank and city: a few hundred objects in one frame
            _shell = AppShell.Make(menu, dormant, _id);
            _table = DataTable.On(_shell, _id);
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(_tables.Off))
            {
                return null;
            }
            _table.Money = amount => FinanceText.Money((float)amount);
            _table.SelectionLost = ClosePick;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        public void Want(int tab)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            if (Showing)
            {
                Opened();
            }
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _wanted = -1;
            }
            _ready = Bank != null && _director.BringUpToDate("a bank app opened") && Desk != null && Desk.Running;
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab)
            {
                _tab = tab;
                _picked = null;
                _product = -1;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            for (int i = 0; i < _rows.Count; i++)
            {
                _pool.Add(_rows[i]);
            }
            _rows.Clear();
            _cashRows = false;
            _shell.ShowTab(_tab);
            InfoBox box = _shell.Box;
            box.Clear();
            string selected = null;
            _spec = _tables.Off;
            if (!_ready || !CreditLedger.Ready || Bank == null)
            {
                UiClone.Words(_shell.Header, Bank != null ? Bank.Name : FinanceText.T("reallife_econ_lender_bank"));
                box.Title(FinanceText.T("reallife_bank_off_title"));
                box.Note(FinanceText.T("reallife_bank_off_body"));
            }
            else
            {
                CreditScore score = CreditLedger.Score;
                UiClone.Words(_shell.Header, FinanceText.T("reallife_bank_headline", "bank", Bank.Name, "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)));
                switch (_tab)
                {
                    case Loans: selected = WriteLoans(box); break;
                    case Card: WriteCard(box); break;
                    case Apply: selected = WriteApply(box, score); break;
                    case Rating: WriteRating(box, score); break;
                    default: WriteAccounts(box); break;
                }
            }
            box.Done();
            if (_cashRows)
            {
                _table.Show(_spec, _cash, selected);   // written as it scrolls: some fifteen rows of words, not one for every movement
            }
            else
            {
                _table.Show(_spec, _rows, selected);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Banks: " + (Bank != null ? Bank.Name : "no bank") + ", tab " + _tab + " written, " + (_cashRows ? _cash.Count + " row(s) as they scroll" : _rows.Count + " row(s)")
                             + "; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms; " + box.Fit() + ".");
            }
        }

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            _rows.Add(row.Reset(id));
            return row;
        }

        private void Say(string id, string words)
        {
            TableRow row = Row(id);
            row.Info = true;
            row.A = words;
        }

        private void RowClicked(TableRow row)
        {
            if (row != null && row.Pick && _tab == Loans)
            {
                _picked = row.Id;
                Refresh();
                return;
            }
            if (row != null && row.Pick && _tab == Apply && row.Index >= 0 && row.Index < BankTables.OfferCount)
            {
                if (_product != row.Index)
                {
                    _product = row.Index;
                    _amount = 0;
                    _deposit = 0;
                    _term = 0;
                }
                Refresh();
                return;
            }
            _table.ClearSelection();
        }

        private void ClosePick()
        {
            _picked = null;
            _product = -1;
            _table.ClearSelection();
            Refresh();
        }

        private void After()
        {
            if (Showing)
            {
                _ready = Bank != null && _director.Running && CreditLedger.Ready;
                Refresh();
            }
        }

        private static void Tell(NotificationType type, string key, Dictionary<string, string> data = null)
        {
            Notifications.Show(type, key, data, 9f, null, null, true, false);
        }

        // Why something is not on offer, in one sentence; a refusal for the rating in the bank's own words.
        private string Why(ProductAnswer answer)
        {
            return answer == ProductAnswer.BelowThreshold
                ? FinanceText.T("reallife_bank_why_threshold", "bank", Bank.Name, "threshold", CreditCatalogue.LendingThreshold.ToString(), "score", CreditLedger.Value.ToString())
                : FinanceApp.Why(answer);
        }

        private void Said(ProductAnswer answer)
        {
            if (answer != ProductAnswer.Yes)
            {
                Tell(NotificationType.Error, "reallife_econ_note_not_done", new Dictionary<string, string> { { "why", Why(answer) } });
            }
            After();
        }

        // The game's own text box for an amount; what is typed is checked by the action it is for.
        private void AskAmount(Transform row, string buttonKey, string placeholderKey, int initial, Action<int> then)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            InfoBox.Button(row, InfoBox.Additional, buttonKey, () => uis.textInputUi.Show(placeholderKey, "common_confirm", text => UiClone.Safe(() =>
            {
                int amount;
                if (!FinanceApp.Whole(text, out amount) || amount <= 0)
                {
                    Notifications.ShowError("common_notification_invalid_amount", null, false);
                    return;
                }
                then(amount);
            }), null, Mathf.Max(0, initial).ToString(), false, TypedAmountLength));
        }

        // The amounts a selector steps through between a least and a most, the one chosen among them.
        private void Steps(int least, int most)
        {
            _amount = Mathf.Clamp(_amount <= 0 ? most : _amount, least, Mathf.Max(least, most));
            _amounts.Clear();
            for (int i = 0; i < AmountSteps.Length && AmountSteps[i] < most; i++)
            {
                if (AmountSteps[i] >= least)
                {
                    _amounts.Add(AmountSteps[i]);
                }
            }
            _amounts.Add(most);
            if (!_amounts.Contains(_amount))
            {
                int at = 0;
                while (at < _amounts.Count && _amounts[at] < _amount)
                {
                    at++;
                }
                _amounts.Insert(at, _amount);
            }
        }

        private void AmountSelector(InfoBox box)
        {
            box.Headline("reallife_econ_offer_amount");
            if (!box.HasSelector || !box.Selector(_amounts.Count, _amounts.IndexOf(_amount), i => FinanceText.Money(_amounts[i]), i =>
                {
                    _amount = _amounts[i];
                    Refresh();
                }))
            {
                box.Line(FinanceText.Money(_amount));
            }
        }

        private CreditAccount HeldHere(ProductKind kind)
        {
            CreditAccount a = CreditLedger.File != null ? CreditLedger.File.Account(kind) : null;
            return a != null && BankTerms.IsBank(Bank, a.Street, a.Number) ? a : null;
        }

        private static string HeldWith(ProductKind kind)
        {
            CreditAccount a = CreditLedger.File != null ? CreditLedger.File.Account(kind) : null;
            return a != null ? CreditDesk.LenderName(a) : null;
        }

        private void WriteAccounts(InfoBox box)
        {
            GameInstance save = SaveGameManager.Current;
            box.Title(FinanceText.T("reallife_bank_current"));
            box.Subtitle(Bank.Name);
            if (IsHouse)
            {
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_bank_balance", FinanceText.Money(save.Money), save.Money < 0f ? CellTone.Red : CellTone.Plain);
                box.Note(FinanceText.T("reallife_bank_current_here"));
            }
            else
            {
                box.Note(FinanceText.T("reallife_bank_current_elsewhere", "house", BankTerms.House != null ? BankTerms.House.Name : string.Empty, "bank", Bank.Name));
            }
            OverdraftLines(box);
            Statement(box, save.Day);
            Meta.Guide.Point(box, GuideTitle);
        }

        private void OverdraftLines(InfoBox box)
        {
            box.Headline("reallife_econ_kind_overdraft");
            CreditAccount a = HeldHere(ProductKind.Overdraft);
            if (a != null)
            {
                box.Row("reallife_econ_offer_drawn", FinanceText.Money(a.Owed), a.Owed > 0 ? CellTone.Yellow : CellTone.Plain);
                box.Row("reallife_econ_offer_limit", FinanceText.Money(a.Agreed));
                if (a.Agreed > 0)
                {
                    float used = a.Owed / (float)a.Agreed;
                    box.Meter("reallife_ibox_used", used, Mathf.RoundToInt(used * 100f) + "%", used >= 0.9f ? CellTone.Red : used >= 0.5f ? CellTone.Yellow : CellTone.Green);
                }
                box.Row("reallife_econ_offer_rate", FinanceApp.Rate(a.RateNow));
                box.Note(FinanceText.T("reallife_econ_account_overdraft", "free", FinanceText.Money(a.Free), "float", FinanceText.Money(ProductCatalogue.OverdraftFloat)));
                if (a.Frozen)
                {
                    FinanceApp.InArrangement(box);
                }
                Transform buttons = box.ButtonRow();
                CreditDesk desk = Desk;
                if (a.Owed > 0)
                {
                    // in a debt arrangement too: paid sooner by hand, it counts under the arrangement
                    AskAmount(buttons, "reallife_econ_account_pay", "reallife_econ_account_pay_how_much", a.Owed, amount => Paid(desk, a, amount));
                    InfoBox.Button(buttons, InfoBox.Positive, "reallife_econ_account_pay_off", () => Paid(desk, a, a.Owed));
                }
                else if (!a.Frozen)
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_econ_account_close", () => Said(desk.CloseAccount(a)));
                }
                return;
            }
            string other = HeldWith(ProductKind.Overdraft);
            if (other != null)
            {
                box.Note(FinanceText.T("reallife_bank_overdraft_elsewhere", "other", other));
                return;
            }
            ProductOffer offer = Desk.Offer(ProductKind.Overdraft, Bank);
            if (offer.Answer != ProductAnswer.Yes)
            {
                box.Line(Why(offer.Answer), CellTone.Red);
                return;
            }
            box.Line(FinanceText.T("reallife_bank_overdraft_offer", "bank", Bank.Name, "limit", FinanceText.Money(offer.Most), "rate", (offer.RateBp / 100f).ToString("0.#")));
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_bank_apply_go", () => GoApply(BankTables.Overdraft));
        }

        private void GoApply(int product)
        {
            _tab = Apply;
            _product = product;
            _amount = 0;
            Refresh();
        }

        // A movement of money is this bank's if the house bank holds the cash, or it is under the bank's name or an account it holds.
        private bool Mine(Dictionary<string, string> data)
        {
            if (IsHouse)
            {
                return true;
            }
            if (data == null)
            {
                return false;
            }
            string value;
            if (data.TryGetValue("businessName", out value) && string.Equals(value, Bank.Name, StringComparison.Ordinal))
            {
                return true;
            }
            if (!data.TryGetValue("name", out value) || string.IsNullOrEmpty(value))
            {
                return false;
            }
            for (int i = 0; i < _names.Count; i++)
            {
                if (string.Equals(value, _names[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private void Names()
        {
            _names.Clear();
            _names.Add(Bank.Name);
            Desk.Held(Bank, _debts);
            for (int i = 0; i < _debts.Count; i++)
            {
                if (_debts[i].Account != null)
                {
                    _names.Add(CreditDesk.Name(_debts[i].Account));
                }
            }
        }

        private void Statement(InfoBox box, int today)
        {
            Names();
            _flow.Read(_period, today, IsHouse ? null : _mine);   // the house bank's is every movement: nothing to leave out
            _spec = _tables.Statement;
            box.Headline("reallife_bank_statement");
            if (!box.HasSelector || !box.Selector(CashFlow.PeriodDays.Length, _period, CashFlow.PeriodName, i =>
                {
                    _period = i;
                    Refresh();
                }))
            {
                box.Line(CashFlow.PeriodName(_period));
            }
            // the totals summed in the order the rows were once added up in, so that they come to the same figures
            float came = 0f, went = 0f;
            for (int i = _flow.Entries.Count - 1; i >= 0; i--)
            {
                float amount = _flow.Entries[i].Amount;
                if (amount > 0f)
                {
                    came += amount;
                }
                else
                {
                    went -= amount;
                }
            }
            box.Row("reallife_econ_cash_net", FinanceText.Money(came - went), came >= went ? CellTone.Green : CellTone.Red);
            box.RowPair("reallife_econ_cash_in", FinanceText.Money(came), "reallife_econ_cash_out", FinanceText.Money(went));
            _cashRows = _flow.Entries.Count > 0;
            if (!_cashRows)
            {
                Say("none", FinanceText.T("reallife_bank_row_no_movements", "bank", Bank.Name));
            }
        }

        // The rating as this bank sees it: its terms at each band, the player's own row among them.
        private void WriteRating(InfoBox box, CreditScore score)
        {
            _spec = _tables.Rating;
            int threshold = CreditCatalogue.LendingThreshold, best = CreditCatalogue.BestTermsFrom, third = (best - threshold) / 3;
            RatingRow("band:0", threshold - 1, BankTables.Band);
            RatingRow("band:1", threshold, BankTables.Band);
            RatingRow("band:2", threshold + third / 10 * 10, BankTables.Band);
            RatingRow("band:3", threshold + 2 * third / 10 * 10, BankTables.Band);
            RatingRow("band:4", best, BankTables.Band);
            RatingRow("yours", score.Value, BankTables.Yours);

            box.Title(FinanceText.T("reallife_econ_rating_value", "score", score.Value.ToString(), "grade", FinanceText.Grade(score.Grade)));
            box.Subtitle(Bank.Name);
            box.Chip(FinanceText.Grade(score.Grade), FinanceApp.Tone(score.Grade));
            box.Line(FinanceText.T(score.Lends ? "reallife_bank_rating_lends" : "reallife_bank_rating_refuses", "bank", Bank.Name, "threshold", threshold.ToString()),
                score.Lends ? CellTone.Green : CellTone.Red);
            int next = Mathf.Min(CreditCatalogue.ScoreMax, (score.Value / 50 + 1) * 50);
            if (next > score.Value && score.Value < best)
            {
                LenderTerms then = LendingTerms.For(next, BankTerms.OwnCap(Bank.Settings), BankTerms.OwnRate(Bank.Settings));
                box.Line(FinanceText.Fill("reallife_bank_rating_next", "score", next.ToString(), "bank", Bank.Name, "loan", FinanceText.Money(then.Cap), "rate",
                         (then.AnnualRate * Multiplier).ToString("0.#"),
                    "card", FinanceText.Money(CreditDesk.LimitAt(ProductKind.CreditCard, next, Bank))));
            }
            int tips = CreditScoring.Tips(score, _tips);
            if (tips > 0)
            {
                box.Headline("reallife_econ_credit_raise");
                for (int i = 0; i < tips; i++)
                {
                    box.Line(FinanceText.Tip(score, _tips[i]));
                }
            }
            box.Note(FinanceText.T(ProfileKeys[Mathf.Clamp(Bank.Rank, 0, ProfileKeys.Length - 1)]));
            box.Note(FinanceText.T("reallife_bank_rating_note"));
            Meta.Guide.Point(box, GuideTitle);
        }

        // One band: the bank's loan cap and rate at that rating before the borrower's means count, and its card, overdraft and car finance.
        private void RatingRow(string id, int at, int kind)
        {
            TableRow row = Row(id);
            row.Kind = kind;
            bool lends = BankRules.Lends(at);
            row.Set(0, at.ToString(), at, kind == BankTables.Yours ? TableRow.Green : TableRow.Plain);
            row.Set(1, FinanceText.T(BankTables.RatingKinds[kind]));
            if (!lends)
            {
                row.Set(2, FinanceText.T("reallife_bank_row_refused"), 0, TableRow.Red);
                return;
            }
            LenderTerms terms = LendingTerms.For(at, BankTerms.OwnCap(Bank.Settings), BankTerms.OwnRate(Bank.Settings));
            float rate = terms.AnnualRate * Multiplier;
            row.Set(2, FinanceText.Money(terms.Cap), terms.Cap);
            row.Set(3, FinanceText.T("reallife_econ_row_rate", "rate", rate.ToString("0.#")), rate);
            if (Settings.EconomyProducts)
            {
                int card = CreditDesk.LimitAt(ProductKind.CreditCard, at, Bank), overdraft = CreditDesk.LimitAt(ProductKind.Overdraft, at, Bank), car = CreditDesk.LimitAt(ProductKind.CarFinance,
                                              at, Bank);
                row.Set(4, FinanceText.Money(card), card);
                row.Set(5, FinanceText.Money(overdraft), overdraft);
                row.Set(6, FinanceText.Money(car), car);
            }
        }
    }
}
