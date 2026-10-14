using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The Stocks app, in the game's frame (GameUi.AppShell, copied from MyEmployees) as the mod's other apps are. Six
    /// tabs over the table on the right, and on the left the white box:
    ///
    ///   Market      the city's index and every sector's, with their moves over a day, a week, a month and longer; the
    ///               box has the market's state, the bank rate and the news (pick the index for its chart, a sector for
    ///               its companies)
    ///   Companies   every listed company: price, the day's move, volume, worth, P/E, yield and what you hold, by sector
    ///               and size (the screener: every column sorts)
    ///   Watchlist   the companies you watch
    ///   Orders      orders open and lately done, worked orders with what is done and left; pick one to see it or cancel it
    ///   Portfolio   everything held, each with what it is (a stock, a property trust, a bond, a deposit), what it cost and
    ///               is worth, its income and next payment; the box has the spread across kinds, what it pays over a year
    ///               and the account (and a margin account) (MarketApp.Portfolio.cs)
    ///   History     the account's history: trades, dividends, payouts, interest and fees
    ///   Bots        the trading bots for hire and those hired: their record, their settings (MarketApp.Bots.cs)
    ///   Bonds       the bonds and bank deposits on offer and those held, with their next payment (MarketApp.Bonds.cs)
    ///
    /// Picking a company opens its page in place of the table (MarketApp.Page.cs): its candle chart with the analysis
    /// tools, or its figures and results; the box then has its quote and the order ticket (MarketApp.Box.cs). Nothing
    /// runs while the app is closed: rows and figures are made when it opens and when the player does something in it
    /// (the game stands still while the phone is open).
    /// </summary>
    public sealed partial class MarketApp
    {
        public const int TabMarket = 0, TabCompanies = 1, TabWatch = 2, TabOrders = 3, TabPortfolio = 4, TabHistory = 5;
        public const int IndexPage = -1;   // the page of the city's index (a company's page is its id)
        private const string GuideTitle = "reallife_meta_title_markets";   // the Guide's page every page of the app points to at its foot

        private static readonly string[] TabKeys =
        {
            "reallife_mkt_tab_market", "reallife_mkt_tab_companies", "reallife_mkt_tab_watch", "reallife_mkt_tab_orders", "reallife_mkt_tab_portfolio", "reallife_mkt_tab_history",
            "reallife_mkt_tab_bots", "reallife_mkt_tab_bonds"
        };

        public static IModLogger Logger;

        private readonly MarketDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(160);
        private readonly List<TableRow> _pool = new List<TableRow>(160);
        private readonly Action _changed;

        private AppShell _shell;
        private DataTable _table;
        private MarketPage _page;
        private int _tab, _wanted = -1, _wantCompany;
        private string _picked;     // the row picked on the tab that shows
        private int _company;       // the page shown in place of the table: a company's id, IndexPage, or 0 for the table

        public MarketApp(MarketDirector director)
        {
            _director = director;
            _changed = () =>
            {
                if (Showing)
                {
                    UiClone.Safe(Refresh);
                }
            };
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        // ---------------------------------------------------------------- building it from the game's parts

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected (each has said so in Player.log).</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("stocks: the app being built from the game's MyEmployees");   // once per city
            _shell = AppShell.Make(menu, dormant, MarketDirector.AppId);
            _table = DataTable.On(_shell, "Stocks");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(MarketTables.Off))
            {
                return null;
            }
            _table.Money = amount => MarketText.Money((long)Math.Round(amount));
            _table.SelectionLost = ClosePick;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => UiClone.Safe(() => ShowTab(tab)));
            }
            _page = MarketPage.Make(_shell, this);
            if (_page == null)
            {
                return null;
            }
            return _shell.Root;
        }

        // ---------------------------------------------------------------- opening and the tabs

        /// <summary>The company whose page to show the next time the app opens (a notification was clicked).</summary>
        public void WantCompany(int stockId)
        {
            _wantCompany = stockId;
            if (Showing)
            {
                Opened();
            }
        }

        /// <summary>The tab to show the next time the app opens.</summary>
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
            _director.Changed -= _changed;
            _director.Changed += _changed;
            if (_wantCompany != 0)
            {
                OpenPage(_wantCompany);
                _wantCompany = 0;
            }
            else if (_wanted >= 0)
            {
                _tab = _wanted;
                _wanted = -1;
                _picked = null;
                _company = 0;
            }
            Refresh();
        }

        public void Closed()
        {
            _director.Changed -= _changed;
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            _picked = null;
            _company = 0;
            ResetTicket();
            Refresh();
        }

        /// <summary>A company's page (or the index's, with IndexPage) in place of the table.</summary>
        internal void OpenPage(int company)
        {
            if (company != _company)
            {
                ResetTicket();
            }
            _company = company;
            _picked = null;
        }

        internal void ClosePage()
        {
            _company = 0;
            Refresh();
        }

        // Everything the app shows, made again: asked for when it opens, a tab is picked or something has changed.
        internal void Refresh()
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
            _shell.ShowTab(_tab);
            InfoBox box = _shell.Box;
            box.Clear();
            MarketState s = _director.State;
            TableSpec table = MarketTables.Off;
            if (s == null || SaveGameManager.Current == null)
            {
                UiClone.Words(_shell.Header, MarketText.T("reallife_mkt_app"));
                box.Title(MarketText.T("reallife_mkt_off_title"));
                box.Note(MarketText.T(Settings.Markets ? "reallife_mkt_off_body" : "reallife_mkt_off_option"));
                _company = 0;
                _picked = null;
            }
            else if (_company == OwnPage)
            {
                _page.Hide();
                UiClone.Words(_shell.Header, MarketText.T("reallife_mkt_app"));
                table = WriteListing(box, s);   // the player's own company (MarketApp.Own.cs)
            }
            else if (_company != 0 && (_company == IndexPage || s.Stock(_company) != null))
            {
                UiClone.Words(_shell.Header, MarketText.T("reallife_mkt_app"));
                if (_company == IndexPage)
                {
                    WriteIndexBox(box, s);
                    _page.ShowIndex(s);
                }
                else
                {
                    Stock k = s.Stock(_company);
                    WriteTicket(box, s, k);
                    _page.ShowCompany(s, k);
                }
            }
            else
            {
                _company = 0;
                _page.Hide();
                UiClone.Words(_shell.Header, MarketText.T("reallife_mkt_app"));
                switch (_tab)
                {
                    case TabCompanies: table = WriteCompanies(box, s, false); break;
                    case TabWatch: table = WriteCompanies(box, s, true); break;
                    case TabOrders: table = WriteOrders(box, s); break;
                    case TabPortfolio: table = WritePortfolio(box, s); break;
                    case TabHistory: table = WriteHistory(box, s); break;
                    case TabBots: table = WriteBots(box, s); break;
                    case TabBonds: table = WriteBonds(box, s); break;
                    default: table = WriteMarket(box, s); break;
                }
            }
            box.Done();
            if (_company == 0 || _company == OwnPage)
            {
                _table.Show(table, _rows, _picked);
            }
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Stocks: " + (_company != 0 ? "page " + _company : "tab " + _tab + ", " + _rows.Count + " row(s)") + "; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
            }
        }

        private TableRow Row(string kind, int index)
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
            row.Reset(kind + index);
            row.Pick = true;
            row.Index = index;
            _rows.Add(row);
            return row;
        }

        private void Say(string words)
        {
            TableRow row = Row("say:", _rows.Count);
            row.Info = true;
            row.Pick = false;
            row.A = words;
        }

        // The number at the end of a row's id ("co:12"), if the id is of that kind; int.MinValue otherwise.
        private static int Picked(string id, string kind)
        {
            int number;
            return id != null && id.StartsWith(kind, StringComparison.Ordinal) && int.TryParse(id.Substring(kind.Length), out number) ? number : int.MinValue;
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            int company = Picked(row.Id, CompanyRow);
            if (company == int.MinValue)
            {
                company = Picked(row.Id, HoldingRow);
            }
            if (company == int.MinValue)
            {
                company = BotHoldingCompany(row.Id);   // a bot's holding (MarketApp.Bots.cs)
            }
            if (company != int.MinValue)
            {
                OpenPage(company);
                Refresh();
                return;
            }
            int bond = Picked(row.Id, PortfolioBondRow);
            if (bond != int.MinValue)
            {
                // A bond of the Portfolio tab: it, on the Bonds tab (where bonds are bought, sold and managed).
                _tab = TabBonds;
                _picked = BondHeldRow + bond;
                Refresh();
                return;
            }
            int sector = Picked(row.Id, SectorRow);
            if (sector != int.MinValue)
            {
                if (sector < 0)
                {
                    OpenPage(IndexPage);
                }
                else
                {
                    // A sector's row leads to its companies.
                    _tab = TabCompanies;
                    _picked = null;
                    MarketTables.Companies.Filters[0].Chosen = sector;
                }
                Refresh();
                return;
            }
            _picked = row.Id;
            Refresh();
        }

        private void ClosePick()
        {
            _picked = null;
            _table.ClearSelection();
            Refresh();
        }

        // ---------------------------------------------------------------- figures every page uses

        /// <summary>A move from one price to another in basis points; 0 if there is no price before.</summary>
        internal static int Bp(long now, long before)
        {
            return MarketHistory.MoveBp(now, before);
        }

        internal static GameUi.CellTone Tone(int bp)
        {
            return bp > 0 ? TableRow.Green : bp < 0 ? TableRow.Red : TableRow.Plain;
        }

        /// <summary>Earnings a share over the last four quarters given out, in money; NaN with fewer than four.</summary>
        internal static double TrailingEps(Stock s)
        {
            if (s.Results.Count < 4)
            {
                return double.NaN;
            }
            double eps = 0.0;
            for (int i = s.Results.Count - 4; i < s.Results.Count; i++)
            {
                eps += s.Results[i].Eps / 10000.0;
            }
            return eps;
        }

        /// <summary>The price on earnings over the last four quarters; NaN for a company that lost money or has fewer.</summary>
        internal static double Pe(Stock s)
        {
            double eps = TrailingEps(s);
            return !double.IsNaN(eps) && eps > 0.0 ? s.Last / 100.0 / eps : double.NaN;
        }

        /// <summary>What the last quarter's dividend comes to over a year, on today's price, in basis points.</summary>
        internal static int YieldBp(Stock s)
        {
            return s.Last > 0 ? (int)Math.Round(s.DpsCents * 4.0 * 10000.0 / s.Last) : 0;
        }

        internal static string Count(long n)
        {
            return n.ToString("N0");
        }
    }
}
