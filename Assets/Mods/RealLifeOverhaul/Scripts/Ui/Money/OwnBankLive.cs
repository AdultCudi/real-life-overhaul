using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using UI;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Your bank screen's live source: reads the facts of the bank the player owns (OwnBankReader, the same rules the old page read) when
    /// the day series, the hour or a press moves the generation, and carries each button to the director that already owns it, as the old app
    /// did: the same calls, the same questions in the game's own box, the same refusals as notices. Nothing a frame.
    /// </summary>
    public sealed class OwnBankLive : IMoneySource, IMoneyActs, IUiTabWant
    {
        private int _wanted = -1;

        public static readonly OwnBankLive Instance = new OwnBankLive();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private readonly OwnBankFacts _facts = new OwnBankFacts();
        private readonly OwnBankWorld _world = new OwnBankWorld();
        private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(4);
        private readonly float[] _values = new float[30];
        private readonly int[] _days = new int[30];
        private int _local;

        private OwnBankLive() { }

        private sealed class Say : IOwnBankSay
        {
            public string T(string key) => FinanceText.T(key);

            public string F(string key, params string[] pairs) => FinanceText.Fill(key, pairs);

            public string Day(int day) => FinanceText.Day(day);

            public string Cents(long cents) => OwnBankText.Money(cents);
        }

        private static readonly Say Words = new Say();

        public static UtilitiesWords W => new UtilitiesWords(UiText.Words, FinanceText.Money, () => Inv);

        public int Generation
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                return DaySeriesDirector.Generation + _local + MoneyStamp.Now + (save != null ? save.Day * 24 + save.Hour : 0);
            }
        }

        private void Moved() => _local++;

        /// <summary>A notice asked for a tab of Your bank: the page shows it the next time it lays out.</summary>
        public static void Want(int tab)
        {
            Instance._wanted = tab;
            Instance._local++;
        }

        public int TakeWanted()
        {
            int tab = _wanted;
            _wanted = -1;
            return tab;
        }

        public bool Press(MoneyModel model, int command, int arg) => false;

        /// <summary>The facts of the bank as the kit's page reads them, for the old app's newer pages, so both interfaces show one figure. Read now; nothing kept but the facts.</summary>
        public OwnBankFacts Facts(int tab)
        {
            try
            {
                Read(tab);
            }
            catch (Exception e)
            {
                _facts.Ready = false;
                OwnBankDirector.Logger?.Warn("Your bank: the facts could not be read: " + e.Message);
            }
            return _facts;
        }

        public void Fill(MoneyModel model)
        {
            try
            {
                Read(model.Tab);
            }
            catch (Exception e)
            {
                _facts.Ready = false;
                OwnBankDirector.Logger?.Warn("Your bank (kit): the page could not be read: " + e.Message);
            }
            OwnBankBuild.Fill(model, _facts, W);
        }

        // ------------------------------------------------------------------------------------------------- reading

        private void Read(int tab)
        {
            OwnBankDirector director = OwnBankDirector.Instance;
            OwnBank bank = director != null ? director.Bank : null;
            GameInstance save = SaveGameManager.Current;
            OwnBankWorld w = _world;
            w.Ready = bank != null && save != null;
            if (!w.Ready)
            {
                _facts.Ready = false;
                return;
            }
            OwnBankReader.GameYear = GameCalendar.DaysPerYear;
            BuildingRegistration hq = OwnBankGame.Headquarters();
            w.Today = OwnBankGame.Today;
            w.Level = Prices.Level;
            w.Buying = EndScaleRules.Buying(Settings.EndScale);
            w.HasHq = hq != null;
            w.HqName = hq != null ? OwnBankGame.Name(hq) : string.Empty;
            w.Integrity = OwnBankGame.Integrity;
            w.StandingOn = OwnBankGame.StandingOn;
            w.AgainWhy = bank.State == OwnBankState.Closed ? OwnBankGame.AgainWhy(bank) : null;
            w.MoveFee = director.MoveUpFee();
            w.MoveWhy = bank.Running ? director.MoveUpWhy() : null;
            w.BranchOn = Settings.EconomyBranch;
            w.Tax = OwnBankGame.TaxOn;
            if (tab == OwnBankBuild.FundTab && bank.Running)
            {
                DaySeriesBook book = DaySeriesDirector.Book;
                int n = book != null ? FinanceRules.ExistingDays(book, DaySeriesCatalogue.Fund, RealLifeOverhaul.Rivals.RivalGame.Day, _values.Length, false, _values, _days) : 0;
                w.FundSeries = new float[n];
                w.FundDays = new int[n];
                Array.Copy(_values, w.FundSeries, n);
                Array.Copy(_days, w.FundDays, n);
            }
            if (bank.Running)
            {
                BranchGame.Branches(_regs);
                int count = bank.Branches.Count;
                w.BranchCostDay = new long[count];
                w.BranchOpen = new bool[count];
                w.BranchTellers = new int[count];
                w.BranchStaff = new int[count];
                for (int i = 0; i < count; i++)
                {
                    BuildingRegistration reg = Reg(bank.Branches[i].Place);
                    w.BranchCostDay[i] = BranchGame.CostDay(reg);
                    BranchHourIn h = BranchGame.Hour(reg, false);
                    w.BranchOpen[i] = h.Open;
                    w.BranchTellers[i] = h.Tellers;
                    w.BranchStaff[i] = BranchGame.Staff(reg);
                }
            }
            OwnBankReader.Read(_facts, bank, OwnBankGame.City(), w, Words);
        }

        private BuildingRegistration Reg(string place)
        {
            for (int i = 0; i < _regs.Count; i++)
            {
                if (BranchGame.Place(_regs[i]) == place)
                {
                    return _regs[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------------------------------------------- doing

        public bool Do(MoneyAct act, MoneyView view, int id, int value)
        {
            try
            {
                bool done = DoAct(act, view, id, value);
                if (done)
                {
                    Moved();
                }
                return done;
            }
            catch (Exception e)
            {
                OwnBankDirector.Logger?.Warn("Your bank (kit): " + act + " failed: " + e.Message);
                return false;
            }
        }

        private static void Warn(string why)
        {
            if (why != null)
            {
                Notifications.Show(NotificationType.Warning, why, null, 8f, null, null, true, false);
            }
        }

        private bool DoAct(MoneyAct act, MoneyView view, int id, int value)
        {
            OwnBankDirector director = OwnBankDirector.Instance;
            OwnBank bank = director != null ? director.Bank : null;
            if (bank == null)
            {
                return false;
            }
            switch (act)
            {
                case MoneyAct.Found: return Found(director);
                case MoneyAct.PutIn:
                    {
                        bool ok = director.PutIn(OwnBankBuild.PutDollars(view, _facts) * 100L, out string why);
                        Warn(ok ? null : why);
                        return ok;
                    }
                case MoneyAct.TakeOut:
                    {
                        bool ok = director.TakeOut(OwnBankBuild.TakeDollars(view, _facts) * 100L, out string why);
                        Warn(ok ? null : why);
                        return ok;
                    }
                case MoneyAct.Sell: return Sell(director, bank);
                case MoneyAct.SaverRate: director.SetSaverRate(id, value); return true;
                case MoneyAct.LoanRate: director.SetLoanRate(id, value); return true;
                case MoneyAct.Lending:
                    {
                        int segment = SegmentOf(view.Row);
                        if (segment < 0)
                        {
                            return false;
                        }
                        director.SetLending(segment, value == 1);
                        return true;
                    }
                case MoneyAct.Strategy: director.SetStrategy(value); return true;
                case MoneyAct.FundLimit: director.SetFundLimit(value); return true;
                case MoneyAct.PayMode: director.SetPayMode(value == 1 ? BankTiers.PayFixedMode : BankTiers.PayShareMode); return true;
                case MoneyAct.PayShare: director.SetPayShare(value); return true;
                case MoneyAct.PayFixed:
                    {
                        bool ok = director.SetPayFixed(OwnBankBuild.FixedPayCents(view, _facts), out string why);
                        Warn(ok ? null : why);
                        return ok;
                    }
                case MoneyAct.DividendEvery: director.SetDividends(value, bank.DivShare); return true;
                case MoneyAct.DividendShare: director.SetDividendShare(value); return true;
                case MoneyAct.MoveUp: return MoveUp(director, bank);
                case MoneyAct.CardsOn: director.SetCards(value == 1); return true;
                case MoneyAct.CardIssue:
                    {
                        int kind = RowKind(view.Row, "c|");
                        if (kind < 0)
                        {
                            return false;
                        }
                        director.SetCardIssue(kind, value == 1);
                        return true;
                    }
                case MoneyAct.CardLimit: director.SetCardLimit(id, value); return true;
                case MoneyAct.CardRate: director.SetCardRate(value); return true;
                case MoneyAct.CardFee: director.SetCardFee(value); return true;
                case MoneyAct.BlackOn: director.SetBlack(value == 1); return true;
                case MoneyAct.BlackWealth: director.SetBlackLines(value, bank.BlackScore, bank.BlackFee, bank.BlackReward); return true;
                case MoneyAct.BlackScore: director.SetBlackLines(bank.BlackWealth, value, bank.BlackFee, bank.BlackReward); return true;
                case MoneyAct.BlackFee: director.SetBlackLines(bank.BlackWealth, bank.BlackScore, value, bank.BlackReward); return true;
                case MoneyAct.BlackReward: director.SetBlackLines(bank.BlackWealth, bank.BlackScore, bank.BlackFee, value); return true;
                case MoneyAct.CardAuto: director.SetCardLines(value, bank.CardDecline); return true;
                case MoneyAct.CardBelow: director.SetCardLines(bank.CardAuto, value); return true;
                case MoneyAct.CardApprove:
                case MoneyAct.CardRefuse:
                    {
                        bool ok = director.DecideCards(id, act == MoneyAct.CardApprove, out string why);
                        Warn(ok ? null : why);
                        return ok;
                    }
                case MoneyAct.OwnCardOpen:
                    {
                        bool ok = director.OpenOwnCard(OwnBankBuild.OwnLimitDollars(view, _facts), out string why);
                        Warn(ok ? null : why);
                        return ok;
                    }
                case MoneyAct.OwnCardClose:
                    {
                        bool ok = director.CloseOwnCard(out string why);
                        Warn(ok ? null : why);
                        return ok;
                    }
                case MoneyAct.DeskOn: director.SetDesk(value == 1); return true;
                case MoneyAct.DeskMandate: director.SetDeskMandate(value); return true;
                case MoneyAct.DeskShare: director.SetDeskShare(value); return true;
                case MoneyAct.DeskLimit: director.SetDeskLimit(value); return true;
                case MoneyAct.SellLoans: return SellLoans(director, id, OwnBankBuild.SaleDollars(view, _facts, id) * 100L);
                case MoneyAct.Borrow: return Borrow(director, OwnBankBuild.BorrowDollars(view, _facts) * 100L);
                case MoneyAct.Repay:
                    {
                        bool ok = director.Repay(OwnBankBuild.BorrowDollars(view, _facts) * 100L, out string why);
                        Warn(ok ? null : why);
                        return ok;
                    }
                default: return false;
            }
        }


        private static int RowKind(string row, string prefix)
        {
            return row != null && row.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(row.Substring(prefix.Length), NumberStyles.None, Inv, out int kind) ? kind : -1;
        }

        // The game's own question before loans are sold to other banks: what goes, what comes, what is lost at once.
        private bool SellLoans(OwnBankDirector director, int segment, long face)
        {
            if (face <= 0 || segment < 0 || segment >= OwnBankCatalogue.Segments)
            {
                return false;
            }
            BankCity city = OwnBankGame.City();
            long loss = face * OwnBankRules.SaleDiscountOf(city, director.Bank) / 1000;
            Ask("reallife_ob_confirm_sale_title".Localize(new { bank = director.Bank.Name }), "reallife_ob_confirm_sale_body".Localize(new { face = OwnBankText.Money(face),
                kind = OwnBankText.Segment(segment), cash = OwnBankText.Money(face - loss), loss = OwnBankText.Money(loss) }), "reallife_ob_sale_button", true,
                () => director.SellLoans(segment, face, out string why) ? null : why);
            return true;
        }

        // The game's own question before money is borrowed from other banks: the price and the day it falls due.
        private bool Borrow(OwnBankDirector director, long cents)
        {
            OwnBank bank = director.Bank;
            BankCity city = OwnBankGame.City();
            cents = OwnBankRules.ToStep(cents);
            if (cents <= 0)
            {
                return false;
            }
            Ask("reallife_ob_confirm_borrow_title".Localize(new { bank = bank.Name }), "reallife_ob_confirm_borrow_body".Localize(new { amount = OwnBankText.Money(cents),
                rate = OwnBankText.Rate(OwnBankRules.BorrowBp(city, bank)), fee = OwnBankText.Money(OwnBankRules.BorrowFee(cents)) }), "reallife_ob_stress_borrow_button", false,
                () => director.Borrow(cents, out string why) ? null : why);
            return true;
        }

        private static int SegmentOf(string row)
        {
            return row != null && row.StartsWith("l|", StringComparison.Ordinal) && int.TryParse(row.Substring(2), NumberStyles.None, Inv, out int segment) ? segment : -1;
        }

        // The bank's name typed into the game's text box (empty: the headquarters' name and "Bank"), then the game's question.
        private bool Found(OwnBankDirector director)
        {
            BuildingRegistration hq = OwnBankGame.Headquarters();
            if (hq == null)
            {
                return false;
            }
            int level = Prices.Level, buy = EndScaleRules.Buying(Settings.EndScale);
            long licence = OwnBankDirector.FoundLicence(level, buy), capital = OwnBankDirector.FoundCapital(level, director.Bank.Closures, buy);
            UIs uis = InstanceBehavior<UIs>.Instance;
            string suggested = OwnBankDirector.Named(null, hq);
            if (uis == null || uis.textInputUi == null)
            {
                AskFound(director, suggested, licence, capital);
                return true;
            }
            GameUi.TextBox.Ask("reallife_ob_ask_name", "common_confirm", text => Safe(() => AskFound(director, OwnBankDirector.Named(text, hq), licence, capital)), null, suggested, true,
                OwnBankDirector.NameMost);
            return true;
        }

        private void AskFound(OwnBankDirector director, string name, long licence, long capital)
        {
            Ask("reallife_ob_confirm_found_title".Localize(new { bank = name }),
                "reallife_ob_confirm_found_body".Localize(new { licence = OwnBankText.Money(licence), capital = OwnBankText.Money(capital) }),
                "reallife_ob_box_found", false, () => director.Found(name, licence, capital, out string why) ? null : why);
        }

        private bool Sell(OwnBankDirector director, OwnBank bank)
        {
            if (!OwnBankRules.Sellable(bank))
            {
                return false;
            }
            long value = OwnBankRules.SaleValue(bank);
            Ask("reallife_ob_confirm_sell_title".Localize(new { bank = bank.Name }), "reallife_ob_confirm_sell_body".Localize(new { price = OwnBankText.Money(value),
                years = OwnBankCatalogue.SaleYears.ToString() }),
                "reallife_ob_box_sell", true, () => director.Sell(value, out string why) ? null : why);
            return true;
        }

        // The game's own question before the licence is paid; on yes, the bank moves up and any refusal is said.
        private bool MoveUp(OwnBankDirector director, OwnBank bank)
        {
            if (director.MoveUpWhy() != null)
            {
                Warn(director.MoveUpWhy());
                return false;
            }
            int next = OwnBankRules.NextTier(bank);
            long fee = director.MoveUpFee();
            BankCity city = OwnBankGame.City();
            BankCaps after = OwnBankRules.CapsOf(next, city.Level, city.Scale);
            string level = OwnBankDirector.TierName(next), money = OwnBankText.Money(fee), branches = after.Branches.ToString("N0", Inv);
            LanguageChangeEventDataHolder body = next >= BankTiers.National
                ? "reallife_ob_confirm_up_body_top".Localize(new { fee = money, level, branches })
                : "reallife_ob_confirm_up_body".Localize(new { fee = money, level, branches, loans = OwnBankText.Money(after.Loans), deposits = OwnBankText.Money(after.Deposits) });
            Ask("reallife_ob_confirm_up_title".Localize(new { bank = bank.Name, level }), body, "reallife_ob_box_up_button", false, () => director.MoveUp(fee, out string why) ? null : why);
            return true;
        }

        // The game's own question before a large sum is spent or the bank sold; on yes, done, and any refusal said.
        private void Ask(LanguageChangeEventDataHolder title, LanguageChangeEventDataHolder body, string confirmKey, bool negative, Func<string> act)
        {
            Safe(() => GameConfirm.Show(title, body, () => Safe(() =>
            {
                Warn(act());
                Moved();
            }), null, confirmKey, "common_cancel", false, negative));
        }

        private static void Safe(Action act)
        {
            try
            {
                act();
            }
            catch (Exception e)
            {
                OwnBankDirector.Logger?.Warn("Your bank (kit): an answer failed: " + e.Message);
            }
        }
    }
}
