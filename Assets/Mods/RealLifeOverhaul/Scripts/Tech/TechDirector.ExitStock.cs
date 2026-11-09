using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A firm of the player's that listed, on the stock market (Tech/Venture/ExitStockRules, Markets/Listing/ListingFirm): when its listing is priced a stock
    /// is made of it with the cap table's shares, the offer's price and the 180 day lock-up, and from then on the stock is the one that moves. A sale of the
    /// player's shares is the Stocks app's, never a second path here; the cap table is brought back into agreement with the stock each day and before every
    /// sale of the firm, wind down or removal of the founder; the stock ends with the firm. A startup of the city that lists is made a company of the market's
    /// too, on the host's or a lone player's market. Under the options: with Startups or the stock market off none of it runs. Nothing per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private StockFile _stock;
        private readonly HashSet<string> _stockRefused = new HashSet<string>();
        private readonly List<string> _stockPlaces = new List<string>(8);

        /// <summary>What the tech sector remembers of its firms on the market; null while the venture part is off.</summary>
        public StockFile StockRecord => _ventureOn ? _stock : null;

        private void StartExitStock()
        {
            StockFile file = ExitStockStore.Load(Save.modData, out int version);
            bool fresh = file == null;
            _stock = file ?? new StockFile();
            _stockRefused.Clear();
            Logger?.Info("Tech: firms on the market " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + Save.Day + ": " + _stock.Firms.Count + " record(s).");
        }

        private void StopExitStock(bool keep)
        {
            if (_stock == null)
            {
                return;
            }
            if (keep && Save != null)
            {
                WriteExitStock(Save.modData);
            }
            _stock = null;
        }

        private void WriteExitStock(Dictionary<string, string> data)
        {
            if (_stock != null && data != null)
            {
                if (_stock.Firms.Count > 0 || data.ContainsKey(ExitStockStore.Key))
                {
                    data[ExitStockStore.Key] = ExitStockStore.Write(_stock);
                }
            }
        }

        // ---------------------------------------------------------------- what the market says of a firm

        /// <summary>A firm's stock as the market has it now; not live for a firm with none, or while the market cannot be asked.</summary>
        internal StockInfo StockOf(string place)
        {
            return _ventureOn && _stock != null ? StockBridges.LookNow(place) : default(StockInfo);
        }

        /// <summary>True while a firm's shares are the stock market's, also while the market cannot be asked (see <see cref="ExitStockRules.Held"/>).</summary>
        internal bool HasStock(string place)
        {
            return _ventureOn && _stock != null && ExitStockRules.Held(StockBridges.LookNow(place), _stock.Of(place));
        }

        // The ticker to name a firm's stock by: the market's, or the record's while the market cannot be asked.
        private string StockTicker(string place, in StockInfo now)
        {
            if (now.Live)
            {
                return now.Ticker;
            }
            StockFirm rec = _stock != null ? _stock.Of(place) : null;
            return rec != null && rec.Ticker.Length > 0 ? rec.Ticker : place;
        }

        /// <summary>The days of the lock-up left for the player's shares of a firm with a stock: 0 when it has run out; -1 for a firm with no stock.</summary>
        internal int StockLockLeft(string place)
        {
            StockInfo i = StockOf(place);
            return i.Live ? ExitStockRules.LockLeft(i, Save != null ? Save.Day : 0) : -1;
        }

        /// <summary>The Stocks app opened on a firm's stock.</summary>
        internal void StockShow(string place)
        {
            StockInfo i = StockOf(place);
            if (i.Live)
            {
                StockBridges.Show?.Invoke(i.Id);
            }
        }

        /// <summary>What a firm that trades owes on its bridge loan, whole units of money at today's prices; the market follows it as the stock's debt.</summary>
        internal long StockDebt(string place)
        {
            Cap c = _ventureOn ? _venture.CapOf(place) : null;
            return c != null ? Level(Math.Max(0L, c.Bridge)) / 100 : 0L;
        }

        /// <summary>A firm's worth for an offer, cents at the start's prices: its shares at the market's last price once it trades, else by its books.</summary>
        private long StockWorth(string place, long byBooks)
        {
            StockInfo i = StockOf(place);
            return i.Live ? Math.Max(1L, ExitStockRules.MarketValue(i.Shares, i.Last, Prices.Level)) : byBooks;
        }

        // ---------------------------------------------------------------- the day

        // Each firm that has listed has a stock on the market, and the cap table agrees with it. Once a game day, after the exits'.
        private void ExitStockDay(int day)
        {
            if (_stock == null || _exits == null || _stock.LastDay >= day)
            {
                return;
            }
            _stock.LastDay = day;
            bool open = StockBridges.Ready && StockBridges.OpenNow();
            _stockPlaces.Clear();
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                Cap c = _venture.Caps[i];
                string place = c.Place;
                FirmExit e = _exits.Exit(place, false);
                if (e == null || e.Stage != ListStage.Listed || !StartupMoney.Has(c))
                {
                    continue;
                }
                _stockPlaces.Add(place);   // still listed in the tech sector's books, its business found or not: only the wind down or a sale ends its stock
                FirmPlace firm = FirmOf(place);
                if (firm == null || firm.Reg == null)
                {
                    continue;
                }
                StockInfo info = StockBridges.LookNow(place);
                StockFirm rec = _stock.Of(place);
                if (info.Live)
                {
                    StockReconcile(c, place, info, day);
                    if (rec == null || rec.ListDay != e.ListDay)
                    {
                        rec = rec != null && rec.OpenDay == 0 ? rec : _stock.Make(place);
                        rec.ListDay = e.ListDay;
                        rec.OpenDay = day;
                        rec.EndDay = 0;
                        rec.Ended = StockEnd.None;
                    }
                    rec.Ticker = info.Ticker;
                    continue;
                }
                if (rec != null && rec.ListDay == e.ListDay && rec.OpenDay > 0)
                {
                    if (rec.EndDay == 0 && open)
                    {
                        rec.EndDay = day;   // it left the market by the market's own hand: not made again for this listing
                        rec.Ended = StockEnd.Gone;
                        Logger?.Info("Tech: the stock of " + firm.File.Name + " is not on the market any more; it is not made again for this listing.");
                    }
                    continue;
                }
                if (open)
                {
                    OpenStock(firm, e, c, day, e.ListDay < day);
                }
            }
            if (open && StockBridges.Sweep != null)
            {
                int swept = StockBridges.Sweep(_stockPlaces);
                if (swept > 0)
                {
                    Logger?.Info("Tech: " + swept + " stock(s) of firms that are not listed any more left the market.");
                }
            }
        }

        // The cap table and the stock hold the same shares: what the stock has grown by that is neither the player's nor the public's (staff options, a co-founder's line)
        // is made in the stock, and the player's and the public's lines are what the stock says.
        private void StockReconcile(Cap c, string place, StockInfo info, int day)
        {
            long grown = ExitStockRules.Grown(c, info);
            if (grown != 0 && StockBridges.Grow != null && StockBridges.Grow(info.Id, grown))
            {
                info = StockBridges.LookNow(place);
            }
            if (info.Live && ExitStockRules.Align(c, info))
            {
                ChangedTech();
            }
        }

        // The cap table made to agree with the stock now, for a sale, a wind down or a removal that is about to work out who holds what.
        private void StockSyncFor(string place, Cap c, int day)
        {
            StockInfo info = StockOf(place);
            if (info.Live && c != null)
            {
                StockReconcile(c, place, info, day);
            }
        }

        // ---------------------------------------------------------------- making the stock

        /// <summary>
        /// The firm's stock made. A listing just priced gives the offer's price (the firm's value before the money over the shares before it); one from an older
        /// save gives the firm's worth by its books now over all its shares, with no fee and no money moved. The stock is made once for a listing.
        /// </summary>
        private bool OpenStock(FirmPlace firm, FirmExit e, Cap c, int day, bool old)
        {
            string place = firm.File.Place;
            if (StockBridges.Make == null)
            {
                return false;
            }
            long value, over;
            if (old)
            {
                FirmValue(firm, out long book, out _);
                value = Level(Math.Max(1L, book));
                over = Math.Max(1L, c.Total);
            }
            else
            {
                value = Level(Math.Max(1L, e.Price));
                over = Math.Max(1L, c.Total - ExitStockRules.PublicShares(c));
            }
            StockAsk ask = ExitStockRules.Ask(c, e, place, firm.File.Name, value, over, Year);
            ask.Debt = Level(ask.Debt);
            StockInfo made = StockBridges.Make(ask);
            if (!made.Live)
            {
                if (_stockRefused.Add(place + "|" + made.Why))
                {
                    Logger?.Info("Tech: the stock market could not take " + firm.File.Name + " (" + made.Why + "); it is asked again each day.");
                }
                return false;
            }
            _stockRefused.Remove(place + "|" + made.Why);
            StockFirm rec = _stock.Of(place);
            if (rec == null || (rec.OpenDay > 0 && rec.ListDay != e.ListDay))
            {
                rec = _stock.Make(place);
            }
            rec.ListDay = e.ListDay;
            rec.OpenDay = day;
            rec.EndDay = 0;
            rec.Ended = StockEnd.None;
            rec.Old = old;
            rec.Ticker = made.Ticker;
            c.RaiseUntil = 0;   // a private round under way ends: the shares in issue are the stock's from now
            c.Sheets.Clear();
            long left = Math.Max(0, made.LockDay - day);
            TellTech(NotificationType.Success, "reallife_exit_note_stock",
                Data("firm", firm.File.Name, "ticker", made.Ticker, "price", TechText.Cents(made.Last), "days", left.ToString(CultureInfo.InvariantCulture)),
                TabFunding, ExitRow + place);
            string fresh = old ? "; its listing was from before there was a stock, so nothing was paid and no price jumped" : string.Empty;
            Logger?.Info("Tech: " + firm.File.Name + " is on the market as " + made.Ticker + " at " + TechText.Cents(made.Last) + " a share with " + made.Shares
                         + " shares (" + made.Founder + " the player's, " + made.Float + " the public's), the player locked up to day " + made.LockDay + fresh + ".");
            ChangedTech();
            return true;
        }

        // The listing just priced: the money of the offer went into the firm's account; its stock is made at the offer's price. Called by ListFirm.
        private void StockAfterListing(FirmPlace firm, FirmExit e, Cap c, int day)
        {
            if (_stock != null && StockBridges.Ready && StockBridges.OpenNow())
            {
                OpenStock(firm, e, c, day, false);
            }
        }

        // ---------------------------------------------------------------- the player's shares

        /// <summary>
        /// A sale of the player's shares of a firm with a stock refused here: the stock market is the one place they are sold (its lock-up, its closed windows, its
        /// limits and its price). The player is told where, and a click opens the stock.
        /// </summary>
        internal bool StockSellRefused(string place, string name)
        {
            StockInfo i = StockOf(place);
            if (!HasStock(place))
            {
                return false;
            }
            Notifications.Show(NotificationType.Info, "reallife_exit_note_sell_market", Data("firm", name, "ticker", StockTicker(place, i)), 8f, null,
                i.Live ? Guard.Wrap(() => StockShow(place)) : null, true, false);
            return true;
        }

        /// <summary>A private round refused for a firm whose shares trade: more shares in issue are the stock's to make, and the investors already hold their lines.</summary>
        internal bool StockRaiseRefused(string place, string name)
        {
            StockInfo i = StockOf(place);
            if (!HasStock(place))
            {
                return false;
            }
            Notifications.Show(NotificationType.Info, "reallife_exit_note_raise_market", Data("firm", name, "ticker", StockTicker(place, i)), 8f, null,
                i.Live ? Guard.Wrap(() => StockShow(place)) : null, true, false);
            return true;
        }

        /// <summary>
        /// The player dealt in a firm's shares on the market (MarketBridges.FirmDealt): the cap table follows the stock. Called from the market's own moment on this
        /// machine, so it changes nothing there. This is where a gain on the shares sold is noted for the tax, as ExitSell does for the older sale.
        /// </summary>
        internal void FirmStockDealt(int stockId, long shares, int cents, long money)
        {
            if (!_ventureOn || _stock == null || Save == null)
            {
                return;
            }
            for (int i = 0; i < _firms.Count; i++)
            {
                string place = _firms[i].File.Place;
                StockInfo info = StockOf(place);
                Cap c = info.Live && info.Id == stockId ? _venture.CapOf(place) : null;
                if (c == null)
                {
                    continue;
                }
                ExitStockRules.Align(c, info);
                if (shares < 0)
                {
                    ExitBridges.Practised?.Invoke(TalentCatalogue.Selling, 1);
                }
                Logger?.Info("Tech: the player " + (shares < 0 ? "sold " + (-shares) : "bought " + shares) + " share(s) of " + _firms[i].File.Name + " on the market at " + TechText.Cents(cents) + ", "
                             + TechText.Cents(money) + " before the commission; the cap table has them " + info.Founder + " the player's and " + info.Float + " the public's.");
                ChangedTech();
                return;
            }
        }

        /// <summary>The player's unvested shares of a firm with a stock taken back, as a bad leaver's are, and the cap table's line made what the stock holds. Returns the shares.</summary>
        private long StockCancelUnvested(Cap c, int day)
        {
            StockInfo info = StockOf(c.Place);
            if (!info.Live || StockBridges.Cancel == null)
            {
                return 0L;
            }
            long back = StockBridges.Cancel(info.Id, day);
            StockInfo after = StockBridges.LookNow(c.Place);
            if (after.Live)
            {
                ExitStockRules.Align(c, after);
            }
            return back;
        }

        // ---------------------------------------------------------------- the stock ends with the firm

        // The firm is sold: its stock leaves the market, each share paid what the cap table's waterfall pays the public's, so a holder there is made whole by what
        // the investors were owed. The pot is the one the sale shared.
        private void StockSold(string place, Cap c, long pot, int day)
        {
            EndStock(place, c, pot, StockEnd.Sold, false, day);
        }

        private void StockWound(string place, Cap c, long pot, bool dry, int day)
        {
            EndStock(place, c, pot, StockEnd.WoundDown, dry, day);
        }

        private void EndStock(string place, Cap c, long pot, int how, bool dry, int day)
        {
            StockInfo info = StockOf(place);
            StockFirm rec = _stock != null ? _stock.Of(place) : null;
            if (rec != null && rec.Live)
            {
                rec.EndDay = day;   // the firm is gone whether or not the market could be asked: a stock left over there is swept off it later
                rec.Ended = how;
            }
            if (!info.Live)
            {
                return;
            }
            long each = Level(ExitStockRules.PublicEach(c, pot));
            bool done = StockBridges.End != null && StockBridges.End(info.Id, ExitStockRules.Reason(how, dry), (int)Math.Min(int.MaxValue, Math.Max(0L, each)));
            Logger?.Info("Tech: the stock of " + place + " (" + info.Ticker + ") " + (done ? "left the market" : "could not be ended") + " on day " + day + " as the firm was "
                         + (how == StockEnd.Sold ? "sold" : "wound down") + ", each share paid out at " + TechText.Cents(each) + ".");
        }

        // ---------------------------------------------------------------- a startup of the city that listed

        // The city's startup is a company of the market's too: seeded by the day it listed on, on the host's or a lone player's market only, and never more than the
        // market holds of them. Its listing price and shares are its cap table's; its business is the market's own model of one in its sector.
        private void StockCity(Startup s)
        {
            if (!_ventureOn || StockBridges.Startup == null || !StockBridges.OpenNow())
            {
                return;
            }
            var ask = new StartupAsk
            {
                Name = s.Name, Venture = s.Sector, ArrCents = Math.Max(0L, s.Mrr) * 12, Shares = Math.Max(1L, s.Total),
                PriceCents = ExitStockRules.PriceCents(Level(Math.Max(1L, s.Post)), Math.Max(1L, s.Total))
            };
            StockWhy why = StockBridges.Startup(ask);
            if (why != StockWhy.None)
            {
                Logger?.Info("Tech: " + s.Name + " listed, and the stock market did not take it (" + why + ").");
            }
        }

        // ---------------------------------------------------------------- the test tool

        /// <summary>
        /// The first firm of the player's that has not listed lists at once, for real: its offer is priced at its worth by its books, the money lands as money
        /// raised, and its stock is made.
        /// </summary>
        public void TestExitStock()
        {
            if (!TestVentures() || _exits == null || _stock == null)
            {
                return;
            }
            if (!StockBridges.Ready || !StockBridges.OpenNow())
            {
                Notifications.ShowError("reallife_exit_test_stock_off");
                return;
            }
            int day = Save.Day;
            for (int i = 0; i < _firms.Count; i++)
            {
                FirmPlace firm = _firms[i];
                string place = firm.File.Place;
                FirmExit e = _exits.Exit(place, true);
                if (firm.Reg == null || e.OnTheWay)
                {
                    continue;
                }
                if (e.Listed)
                {
                    Cap listed = _venture.CapOf(place);
                    if (listed != null && StartupMoney.Has(listed) && !HasStock(place))
                    {
                        OpenStock(firm, e, listed, day, true);   // a listing from before there was a stock: given its stock now
                        return;
                    }
                    continue;
                }
                FirmValue(firm, out long value, out _);
                if (value <= 0)
                {
                    continue;
                }
                Cap c = CapFor(place, true);
                if (c == null)
                {
                    continue;
                }
                int owed = ExitStockRules.ListBlocked(c);
                if (owed != ExitStockRules.BlockedNone)
                {
                    Notifications.ShowError(owed == ExitStockRules.BlockedTaken ? "reallife_exit_refuse_taken" : "reallife_exit_refuse_ladder");
                    return;
                }
                IReadOnlyList<string> banks = ExitBanks();
                e.Stage = ListStage.Roadshow;
                e.Bank = 0;
                e.BankName = banks[0];
                e.Reach = ExitRules.BankReach(0, banks[0]);
                e.Spread = ExitRules.BankSpread(e.Reach);
                e.Fee = 0;
                e.Fair = value;
                e.Demand = e.Choice = 0;
                e.Low = e.High = e.Price = value;
                e.FloatBp = ExitCatalogue.FloatBp;
                e.PriceDay = day;
                ListFirm(firm, e, day);
                return;
            }
            Notifications.ShowError("reallife_exit_test_none");
        }
    }
}
