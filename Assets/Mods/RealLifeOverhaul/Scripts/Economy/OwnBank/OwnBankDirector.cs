using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The player's own bank (Economy/OwnBank; its rules OwnBankRules, its money OwnBankGame, its page OwnBankApp). See
    /// the Guide's page "Your own bank".
    ///
    /// This class keeps it going. It has no Update and no place of its own in the game's events: once a second the
    /// credit side's timer calls <see cref="EachSecond"/> (the save is waited for, the option is looked at, the app is
    /// put on the phone), and the credit side's handler of the game's day calls <see cref="OnDay"/> when its own work is
    /// done (EconomyDirector.AfterDay), so the day's books go into the statement the game made for it (GameBooks).
    ///
    ///   the day    each day that has ended since the last (a week at most, after a long jump) is run (OwnBankRules.Day,
    ///              with the city as it is now and the Stocks app's quotes) and booked at the headquarters
    ///              (OwnBankGame.BookDay); what happened is told (a notification that opens the app), printed in the
    ///              City Ledger the next morning (Wiring/OwnBankLinks, the paper's pieces) and, for a fine or a bank
    ///              closed, held against the player's integrity (Standing). At the turn of the bank's month its figures
    ///              are told in one notification.
    ///   the save   the bank is written under its own key (OwnBankStore).
    ///
    /// With the option off, or the credit rating off, the bank stands still: nothing is run, booked or charged, the app
    /// is taken off the phone, and the bank is kept in the save as it was, to go on when the option is on again (the
    /// days between are not run). A fault switches it off for the session with one line.
    ///
    /// Cost: nothing per frame. Once a second: a few comparisons. Once a game day: one run of the rules per day ended
    /// (a few dozen sums, the fund's pass over the market's quotes) and a handful of transactions.
    /// </summary>
    public sealed partial class OwnBankDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeOwnBank";
        public const int TabBank = 0, TabSavers = 1, TabLending = 2, TabFund = 3;
        private const int CatchUpDays = 7;   // a long jump (a stay in hospital, a trip) is run day by day for at most a week
        private const int NewsKept = 8;

        private static readonly string[] AppGlyphs = { "Icon-Security", "Icon-Coin", "Icon-Building" };

        public static IModLogger Logger;
        public static OwnBankDirector Instance { get; private set; }

        private struct Piece
        {
            public int Day;      // the morning's paper it is for
            public string Key;
            public Dictionary<string, string> Data;
        }

        private OwnBank _bank;
        private bool _running, _broken, _gaveUp;
        private PhoneApp _app;
        private OwnBankApp _view;
        private Action _second, _onSave, _onDay;
        private int _warnedDay = -1;
        private int _watchTold = int.MinValue;   // the day the regulator's watch was last told (once a month at most, as a ratio hovers on the line)
        private bool _keepLine;                  // the save has a line of the bank's that could not be read: it is left there, never written over with none
        private string _beforeTests;   // the bank's line before the first test of the session, for "put my bank back"
        private readonly BankDay _day = new BankDay();
        private readonly List<FundQuote> _quotes = new List<FundQuote>(160);
        private readonly List<Piece> _paper = new List<Piece>(NewsKept);

        /// <summary>The bank while the feature runs (it may be none yet: OwnBankState.None); null otherwise.</summary>
        public OwnBank Bank => _running && !_broken ? _bank : null;

        public bool Running => _running && !_broken;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>True while another app may send the player here (the Company app's button).</summary>
        public static bool CanOpen => Instance != null && Instance.Running && Instance._app != null;

        /// <summary>For another app's button: the bank's first page.</summary>
        public static void OpenApp()
        {
            Instance?.Open(TabBank);
        }

        private void Awake()
        {
            Instance = this;
            OwnBankApp.Logger = Logger;
            _second = EachSecond;
            _onSave = OnSave;
            _onDay = OnDay;
            EconomyDirector.Second += _second;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Second -= _second;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;   // another city's director has started since
            }
            _running = false;   // (the city is unloading: what the save keeps was written when the game saved)
            Instance = null;
        }

        // ---------------------------------------------------------------- once a second (the credit side's timer)

        private void EachSecond()
        {
            if (_broken)
            {
                return;
            }
            try
            {
                if (Save == null || LoadScene.isLoading || !ModState.Loaded)
                {
                    return;
                }
                EconomyDirector credit = EconomyDirector.Instance;
                if (!Settings.Economy || !Settings.EconomyBank || credit == null || !credit.Running)
                {
                    if (_running)
                    {
                        StandDown(!Settings.EconomyBank ? "its option is off" : "the credit side is not running", true);
                    }
                    return;
                }
                if (!_running)
                {
                    StartUp();
                }
                SaveHook.Keep(_onSave);
                if (_app == null && !_gaveUp)
                {
                    Install();
                }
            }
            catch (Exception e)
            {
                Fault("the timer", e);
            }
        }

        // ---------------------------------------------------------------- starting and stopping

        private void StartUp()
        {
            GameInstance save = Save;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version = 0;
            OwnBank bank = save.modData.TryGetValue(OwnBankStore.Key, out string line) ? OwnBankStore.Read(line, out version) : null;
            bool fresh = bank == null;
            _keepLine = fresh && line != null;
            if (_keepLine)
            {
                Logger?.Warn("Own bank: the save's line for the bank could not be read; it is left in the save as it is, and the feature starts with no bank.");
            }
            bank = bank ?? new OwnBank();
            bool mended = OwnBankStore.Mend(bank);
            if (bank.Running)
            {
                bank.LastDay = Math.Max(bank.LastDay, save.Day - 1 - CatchUpDays);   // (days while the bank was switched off are not run)
            }
            _bank = bank;
            _running = true;
            _paper.Clear();
            Logger?.Info("Own bank: " + (fresh ? "none in this save yet" : "read from the save (line v" + version + ")") + " on day " + save.Day + ": " + Describe()
                         + (mended ? "; its reserves were mended to balance its books" : string.Empty) + ".");
        }

        private void StandDown(string why, bool keep)
        {
            if (keep && _running && Save != null && _bank != null)
            {
                Write(Save);
            }
            _running = false;
            RemoveApp();
            Logger?.Info("Own bank: stood down (" + why + "); the bank is kept in the save as it is, nothing is run, booked or charged until it is on again.");
        }

        private void RemoveApp()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
                _view = null;
            }
        }

        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                RemoveApp();
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            _running = false;
            Logger?.Warn("Own bank: switched off for this session after an error in " + where + ". Nothing more is run, booked or charged by it; the bank in the save is as it was last written.");
        }

        private void Install()
        {
            var view = new OwnBankApp(this);
            GameSprites.Want(AppGlyphs);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_ob_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(214, 182, 96, 255), new Color32(128, 92, 30, 255), 0.42f, "AppIcon-OwnBank"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Own bank: the game's phone is not built as this version expects, so its app is left off. A bank that is running still runs, and notifications still say what happens.");
                }
                return;
            }
            _view = view;
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        /// <summary>Opens the app on a tab.</summary>
        public void Open(int tab)
        {
            if (_app != null && _view != null && Running)
            {
                _view.Want(tab);
                _app.Open();
            }
        }

        // ---------------------------------------------------------------- the day, the save

        // The game's day event (after its daily run), after the credit side has settled its day: each day that ended since the last.
        private void OnDay()
        {
            if (!Running || Save == null || _bank == null || !_bank.Running)
            {
                return;
            }
            try
            {
                int today = Save.Day;
                for (int d = Math.Max(_bank.LastDay + 1, today - CatchUpDays); d < today && _bank.Running; d++)
                {
                    RunDay(d, OwnBankGame.City(), true);
                }
                _view?.Changed();
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // One day of the bank, run and booked (dated on it, or today for a test), and what happened told.
        private void RunDay(int day, BankCity city, bool dated)
        {
            float started = Time.realtimeSinceStartup;
            Func<List<FundQuote>, bool> quotes = OwnBankBridges.Quotes;
            bool market = quotes != null && quotes(_quotes);
            int month = _bank.Month;
            OwnBankRules.Day(_bank, city, day, market ? _quotes : null, _day);
            BuildingRegistration hq = OwnBankGame.Headquarters();
            OwnBankGame.BookDay(_day, hq, dated ? day : Save.Day);
            if (hq == null && _warnedDay != Save.Day && (_day.Income > 0 || _day.Costs > 0))
            {
                _warnedDay = Save.Day;
                Tell(NotificationType.Warning, "reallife_ob_toast_no_hq", TabBank);
            }
            Happened(day, city, hq);
            if (_bank.Running && _bank.Month != month)
            {
                Tell(NotificationType.Info, "reallife_ob_toast_month", TabBank, "deposits", OwnBankText.Money(_bank.Savings), "loans", OwnBankText.Money(_bank.Lent),
                    "profit", OwnBankText.Signed(_bank.LastIncome - _bank.LastCosts), "ratio", OwnBankText.Ratio(OwnBankRules.Ratio(_bank)));
                Logger?.Info("Own bank: the month turned on day " + day + ": " + Describe() + "; last month's income " + _bank.LastIncome / 100 + ", costs " + _bank.LastCosts / 100
                             + ", written off " + _bank.LastLost / 100 + "; the last year's profit " + OwnBankRules.YearProfit(_bank) / 100 + ".");
            }
            if (Verbose)
            {
                bool balanced = !_bank.Running || OwnBankRules.Balanced(_bank);
                Logger?.Info("Own bank check (day " + day + "): income " + _day.Income + " and costs " + _day.Costs + " cents booked at " + (hq != null ? OwnBankGame.Name(hq) : "no address")
                             + " (the day's profit " + _day.Profit + " kept in the capital, so the player's cash is unchanged); " + (market ? _quotes.Count + " quotes" : "no market")
                             + "; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00") + " ms; the books " + (balanced ? "balance: PASS" : "DO NOT BALANCE: FAIL") + ".");
            }
        }

        // What a day did, told: a notification, a piece for the paper, the player's name; a bank closed is wound up into the player's cash.
        private void Happened(int day, BankCity city, BuildingRegistration hq)
        {
            string name = _bank.Name;
            if (_day.RunStarted)
            {
                Tell(NotificationType.Error, "reallife_ob_toast_run", TabSavers, "bank", name);
                Print("reallife_ob_news_run", "bank", name);
                Logger?.Info("Own bank: a run began on day " + day + " (confidence under " + OwnBankCatalogue.RunStart + ", integrity " + city.Integrity + ", scare " + _bank.Scare + ").");
            }
            if (_day.RunEnded)
            {
                Tell(NotificationType.Success, "reallife_ob_toast_run_over", TabSavers, "bank", name);
                Print("reallife_ob_news_run_over", "bank", name);
            }
            if (_day.Breached)
            {
                Tell(NotificationType.Error, "reallife_ob_toast_breach", TabBank, "ratio", OwnBankText.Ratio(OwnBankRules.Ratio(_bank)), "day", FinanceText.Day(_bank.Deadline));
                Print("reallife_ob_news_breach", "bank", name);
                OwnBankBridges.Moved?.Invoke(-1, false);
                Logger?.Info("Own bank: in breach on day " + day + " (ratio " + OwnBankRules.Ratio(_bank) + " thousandths), to be put right by day " + _bank.Deadline + ".");
            }
            if (_day.Fined)
            {
                Tell(NotificationType.Error, "reallife_ob_toast_fined", TabBank, "fine", OwnBankText.Money(_day.Fine), "day", FinanceText.Day(_bank.Deadline));
                Print("reallife_ob_news_fined", "bank", name, "fine", OwnBankText.Money(_day.Fine));
                OwnBankBridges.Moved?.Invoke(-3, true);
                Logger?.Info("Own bank: fined " + _day.Fine / 100 + " on day " + day + " for a breach left standing; the next deadline is day " + _bank.Deadline + ".");
            }
            if (_day.Cured2)
            {
                Tell(NotificationType.Success, "reallife_ob_toast_cured", TabBank, "bank", name);
                Print("reallife_ob_news_cured", "bank", name);
            }
            if (_day.Watched && day - _watchTold >= city.MonthDays)
            {
                _watchTold = day;
                Tell(NotificationType.Warning, "reallife_ob_toast_watch", TabBank, "ratio", OwnBankText.Ratio(OwnBankRules.Ratio(_bank)));
            }
            if (_day.FireSold > 0)
            {
                Tell(NotificationType.Warning, "reallife_ob_toast_fire_sale", TabFund, "amount", OwnBankText.Money(_day.FireSold));
            }
            if (_day.Closed)
            {
                if (_day.WoundUp > 0)
                {
                    OwnBankGame.Give(_day.WoundUp, OwnBankCatalogue.SaleKey, hq);
                }
                string key = OwnBankText.WhyKey(_day.Why);
                Tell(NotificationType.Error, "reallife_ob_toast_closed", TabBank, "bank", name, "why", OwnBankText.T(key), "left", OwnBankText.Money(_day.WoundUp));
                Print("reallife_ob_news_closed", "bank", name, "why", OwnBankText.T(key));
                OwnBankBridges.Moved?.Invoke(-8, true);
                Logger?.Info("Own bank: closed by the regulator on day " + day + " (" + _day.Why + "); winding it up left " + _day.WoundUp / 100 + " for the player.");
            }
        }

        private void OnSave()
        {
            try
            {
                if (Running && Save != null && _bank != null)
                {
                    Write(Save);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Write(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            if (_bank.Exists)
            {
                save.modData[OwnBankStore.Key] = OwnBankStore.Write(_bank);
                _keepLine = false;
            }
            else if (!_keepLine)
            {
                save.modData.Remove(OwnBankStore.Key);   // (no bank: a save without the feature's line)
            }
        }

        // ---------------------------------------------------------------- what the player does (the app)

        /// <summary>The bank founded with the least capital: the licence and the capital out of the player's cash at the headquarters. False with a reason key if it cannot be.</summary>
        public bool Found(string name, out string why)
        {
            why = Ready();
            if (why != null)
            {
                return false;
            }
            why = _bank.Running ? "reallife_ob_why_already" : _bank.State == OwnBankState.Closed ? "reallife_ob_why_closed_before" : null;
            BuildingRegistration hq = OwnBankGame.Headquarters();
            if (why == null && hq == null)
            {
                why = "reallife_ob_why_no_hq";
            }
            int level = Prices.Level;
            long licence = OwnBankRules.Licence(level), capital = OwnBankRules.CapitalLeast(level);
            if (why == null && licence + capital > OwnBankGame.Cash)
            {
                why = "reallife_ob_why_no_cash";
            }
            if (why != null)
            {
                return false;
            }
            if (!OwnBankGame.Pay(licence, OwnBankCatalogue.LicenceKey, hq, GameBooks.Licensing) || !OwnBankGame.Pay(capital, OwnBankCatalogue.CapitalInKey, hq, null))
            {
                why = "reallife_ob_why_no_cash";
                return false;
            }
            int today = Save.Day;
            OwnBankRules.Found(_bank, today, new System.Random().Next(1, int.MaxValue), capital, Named(name, hq));
            Tell(NotificationType.Success, "reallife_ob_toast_founded", TabBank, "bank", _bank.Name, "capital", OwnBankText.Money(capital));
            Print("reallife_ob_news_founded", "bank", _bank.Name, "place", OwnBankGame.Name(hq));
            Logger?.Info("Own bank: " + _bank.Name + " founded on day " + today + " at " + OwnBankGame.Name(hq) + ", the licence " + licence / 100 + " and the capital " + capital / 100 + " paid.");
            Changed();
            return true;
        }

        /// <summary>The name the bank is founded under: what the player typed, else the headquarters' name and "Bank".</summary>
        public static string Named(string typed, BuildingRegistration hq)
        {
            string name = typed != null ? typed.Trim() : string.Empty;
            if (name.Length == 0)
            {
                name = OwnBankText.T("reallife_ob_default_name", "company", hq != null ? OwnBankGame.Name(hq) : OwnBankText.T("reallife_ob_app"));
            }
            return name.Length > NameMost ? name.Substring(0, NameMost) : name;
        }

        public const int NameMost = 40;

        /// <summary>Capital put in from the player's cash, to the step. False with a reason key if it cannot be.</summary>
        public bool PutIn(long cents, out string why)
        {
            why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : null);
            cents = OwnBankRules.ToStep(cents);
            if (why == null && cents <= 0)
            {
                why = "reallife_ob_why_step";
            }
            if (why == null && cents > OwnBankGame.Cash)
            {
                why = "reallife_ob_why_no_cash";
            }
            if (why != null || !OwnBankGame.Pay(cents, OwnBankCatalogue.CapitalInKey, OwnBankGame.Headquarters(), null))
            {
                why = why ?? "reallife_ob_why_no_cash";
                return false;
            }
            OwnBankRules.PutIn(_bank, cents);
            Tell(NotificationType.Success, "reallife_ob_toast_capital_in", TabBank, "amount", OwnBankText.Money(cents), "ratio", OwnBankText.Ratio(OwnBankRules.Ratio(_bank)));
            Logger?.Info("Own bank: " + cents / 100 + " of capital put in on day " + Save.Day + "; the ratio is " + OwnBankRules.Ratio(_bank) + " thousandths.");
            Changed();
            return true;
        }

        /// <summary>A dividend out of the bank's capital into the player's cash, to the step. False with a reason key if it cannot be.</summary>
        public bool TakeOut(long cents, out string why)
        {
            why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : _bank.Test ? "reallife_ob_why_test" : null);
            cents = OwnBankRules.ToStep(cents);
            if (why == null && (cents <= 0 || !OwnBankRules.TakeOut(_bank, cents, Prices.Level)))
            {
                why = "reallife_ob_why_too_much";
            }
            if (why != null)
            {
                return false;
            }
            OwnBankGame.Give(cents, OwnBankCatalogue.CapitalOutKey, OwnBankGame.Headquarters());
            Tell(NotificationType.Success, "reallife_ob_toast_dividend", TabBank, "amount", OwnBankText.Money(cents));
            Logger?.Info("Own bank: a dividend of " + cents / 100 + " taken on day " + Save.Day + "; the ratio is " + OwnBankRules.Ratio(_bank) + " thousandths.");
            Changed();
            return true;
        }

        /// <summary>The bank sold for what a buyer pays. False with a reason key if no buyer will take it.</summary>
        public bool Sell(out string why)
        {
            why = Ready() ?? (!_bank.Running ? "reallife_ob_why_none" : _bank.Test ? "reallife_ob_why_test" : !OwnBankRules.Sellable(_bank) ? "reallife_ob_why_no_buyer" : null);
            if (why != null)
            {
                return false;
            }
            string name = _bank.Name;
            long price = OwnBankRules.Sell(_bank, Save.Day);
            OwnBankGame.Give(price, OwnBankCatalogue.SaleKey, OwnBankGame.Headquarters());
            Tell(NotificationType.Success, "reallife_ob_toast_sold", TabBank, "bank", name, "price", OwnBankText.Money(price));
            Print("reallife_ob_news_sold", "bank", name, "price", OwnBankText.Money(price));
            Logger?.Info("Own bank: " + name + " sold on day " + Save.Day + " for " + price / 100 + ".");
            Changed();
            return true;
        }

        public void SetSaverRate(int kind, int offset)
        {
            if (Bank != null && _bank.Running && kind >= 0 && kind < OwnBankCatalogue.Savers)
            {
                _bank.SaverOffset[kind] = Math.Max(OwnBankCatalogue.SaverOffsetLeast, Math.Min(OwnBankCatalogue.SaverOffsetMost, offset));
            }
        }

        public void SetLoanRate(int segment, int offset)
        {
            if (Bank != null && _bank.Running && segment >= 0 && segment < OwnBankCatalogue.Segments)
            {
                _bank.LoanOffset[segment] = Math.Max(OwnBankCatalogue.LoanOffsetLeast, Math.Min(OwnBankCatalogue.LoanOffsetMost, offset));
            }
        }

        public void SetLending(int segment, bool on)
        {
            if (Bank != null && _bank.Running && segment >= 0 && segment < OwnBankCatalogue.Segments)
            {
                _bank.Lending[segment] = on;
            }
        }

        public void SetStrategy(int strategy)
        {
            if (Bank != null && _bank.Running)
            {
                _bank.Strategy = Math.Max(0, Math.Min(OwnBankCatalogue.Strategies - 1, strategy));
                _bank.Rebalance = true;
            }
        }

        public void SetFundLimit(int limit)
        {
            if (Bank != null && _bank.Running)
            {
                _bank.FundLimit = Math.Max(0, Math.Min(OwnBankCatalogue.FundLimitMost, limit / OwnBankCatalogue.FundLimitStep * OwnBankCatalogue.FundLimitStep));
                _bank.Rebalance = true;
            }
        }

        // Why nothing can be done now; null if it can.
        private string Ready()
        {
            return !Running || Save == null || _bank == null ? "reallife_ob_why_off" : null;
        }

        // ---------------------------------------------------------------- what the player is told

        /// <summary>The page is written again if it is showing.</summary>
        public void Changed()
        {
            _view?.Changed();
        }

        /// <summary>A notification of the game's that opens the app on a tab; its names filled in (name, value, name, value).</summary>
        private void Tell(NotificationType type, string key, int tab, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length >= 2)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);   // (the game keeps a notification's data for as long as it lists it)
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 11f, null, () => Open(tab), true, false);
        }

        // A piece for the City Ledger: tomorrow's paper if this morning's is out (after six), else this morning's.
        private void Print(string key, params string[] pairs)
        {
            var data = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                data[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            int day = Save.Day + (Save.Hour >= 6 ? 1 : 0);
            if (_paper.Count >= NewsKept)
            {
                _paper.RemoveAt(0);
            }
            _paper.Add(new Piece { Day = day, Key = key, Data = data });
        }

        /// <summary>The bank's pieces for a day's paper (the City Ledger, through Wiring/OwnBankLinks): the same for the same day however often it is asked.</summary>
        public void PaperPieces(int day, List<NewsItem> into)
        {
            for (int i = 0; i < _paper.Count; i++)
            {
                if (_paper[i].Day == day)
                {
                    into.Add(new NewsItem { Key = _paper[i].Key, Data = _paper[i].Data });
                }
            }
        }

        private string Describe()
        {
            OwnBank b = _bank;
            if (b == null || !b.Exists)
            {
                return "no bank";
            }
            if (!b.Running)
            {
                return b.Name + " (" + b.State + " on day " + b.EndDay + (b.Why != ClosedWhy.None ? ", " + b.Why : string.Empty) + ")";
            }
            return b.Name + " founded on day " + b.FoundedDay + ": deposits " + b.Savings / 100 + ", loans " + b.Lent / 100 + ", arrears " + b.InArrears / 100 + ", the fund " + b.FundValue / 100
                   + " in " + b.Holdings.Count + " companies, reserves " + b.Reserves / 100 + ", capital " + b.Capital / 100 + ", ratio " + OwnBankRules.Ratio(b) + " thousandths, " + b.Stage
                   + (b.Run ? ", in a run" : string.Empty) + (b.Test ? ", founded by a test" : string.Empty);
        }

        // ---------------------------------------------------------------- for the save tools

        /// <summary>For the save tools: the bank is written and stands still; the timer starts it again if its option is on.</summary>
        public void StopAll()
        {
            try
            {
                StandDown("the save tools asked", true);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            try
            {
                StandDown("the save's line was removed", false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
            _bank = null;
            _beforeTests = null;
            _paper.Clear();
        }
    }
}
