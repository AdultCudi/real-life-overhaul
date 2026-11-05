using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Web shop pages from the shops' state and the game and carries every button to the director that already owns it: the same methods, the same question boxes, the game's
    /// own text box for a name. A page is written when the director's revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed partial class ShopLive : IVentSource, IShopActions
    {
        public static readonly ShopLive Instance = new ShopLive();
        public const string ScreenId = "webshop";

        private static int _wantTab = -1;
        private static string _wantPick;
        private int _local;
        private readonly VentWords _words = new VentWords((key, english) => OnlineText.T(key), x => OnlineText.Money((int)(x * 100f))).WithPrice(c => OnlineText.Price((int)c));
        private VentView _view;

        private ShopLive()
        {
        }

        /// <summary>A notice asks for a tab and a row: taken when the screen next opens or is next written.</summary>
        public static void Want(int tab, string pick)
        {
            _wantTab = tab;
            _wantPick = pick;
            Instance._local++;
        }

        public int Generation => (OnlineDirector.Instance != null ? OnlineDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
            OnlineDirector.Instance?.AppOpened();
            TakeWant(view);
        }

        /// <summary>A calendar entry or a notice names a place in the app as "tab:pick"; the pick is "shop:place" for a site, "ord:id" for an order, "line:item" for a product.</summary>
        public static void WantFrom(string want)
        {
            int colon = want != null ? want.IndexOf(':') : -1;
            if (colon > 0 && int.TryParse(want.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int tab))
            {
                Want(tab, colon + 1 < want.Length ? want.Substring(colon + 1) : null);
            }
        }

        private static void TakeWant(VentView view)
        {
            if (_wantTab < 0)
            {
                return;
            }
            view.Tab = Math.Max(0, Math.Min(3, _wantTab));
            view.Pick = null;
            view.From = null;
            string pick = _wantPick;
            if (pick != null && pick.StartsWith("shop:", StringComparison.Ordinal))
            {
                view.S1 = pick.Substring(5);   // a notice about one site opens it
            }
            else if (pick != null && (pick.StartsWith(ShopPages.OrderKey, StringComparison.Ordinal) || pick.StartsWith(ShopPages.LineKey, StringComparison.Ordinal)))
            {
                view.Pick = pick;
            }
            _wantTab = -1;
            _wantPick = null;
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            _view = v;
            TakeWant(v);
            OnlineDirector d = OnlineDirector.Instance;
            ShopFacts f = d != null && d.Running && d.State != null && SaveGameManager.Current != null ? Facts(d) : new ShopFacts { Ready = false };
            ShopPages.Fill(m, v, f, this, _words);
        }

        private ShopFacts Facts(OnlineDirector d)
        {
            OnlineState state = d.State;
            int day = SaveGameManager.Current.Day;
            int now = day * 24 + SaveGameManager.Current.Hour;
            IReadOnlyList<WebPlace> places = d.Places;
            var sites = new ShopSiteFact[places.Count];
            var week = new DayBook();
            for (int i = 0; i < sites.Length; i++)
            {
                WebPlace p = places[i];
                WebShop shop = p.Shop;
                ShopExtra x = d.Extra.Of(shop.Place, true);
                shop.WeekInto(week, day);
                var ring = new int[ShopExtra.SalesDays];
                OnlineExtraRules.Ring(x, day, ring);
                int first = 0;
                while (first < ring.Length && ring[first] <= 0)
                {
                    first++;
                }
                var sales = new long[ring.Length - first];
                var names = new string[sales.Length];
                for (int k = 0; k < sales.Length; k++)
                {
                    sales[k] = ring[first + k];
                    names[k] = (day - (ring.Length - 1 - (first + k))).ToString();
                }
                var areas = new ShopAreaFact[Math.Min(8, state.Districts.Count)];
                for (int k = 0; k < areas.Length; k++)
                {
                    areas[k] = new ShopAreaFact { Name = OnlineText.District(state.Districts[k]), Served = x.Serves(k) };
                }
                var lines = new ShopLineFact[shop.Lines.Count];
                for (int l = 0; l < lines.Length; l++)
                {
                    WebLine w = shop.Lines[l];
                    int going = l < p.Site.Going.Length ? p.Site.Going[l] : 0;
                    lines[l] = new ShopLineFact
                    {
                        Item = w.Item, Name = d.ItemName(w.Item), OwnCents = w.Price, AskCents = l < p.Site.Ask.Length ? p.Site.Ask[l] : d.ShopPrice(p, w.Item), GoingCents = going,
                        RivalCents = going > 0 ? OnlineExtraRules.LowestRival(state.Seed, w.Item, going) : 0, Stock = l < p.Site.Stock.Length ? p.Site.Stock[l] : 0, SoldWeek = w.SoldWeek,
                        MissedWeek = w.MissedWeek, On = w.On
                    };
                }
                var open = new List<ShopOrderFact>(shop.Orders.Count);
                for (int o = 0; o < shop.Orders.Count; o++)
                {
                    WebOrder order = shop.Orders[o];
                    open.Add(new ShopOrderFact
                    {
                        Customer = CustomerOf(state.Seed, order.Id), Id = order.Id, StageCode = (int)order.Stage, CanRefund = true, CanSend = order.Stage == OrderStage.Picked,
                            Items = ItemsOf(d, shop, order),
                            District = order.District < state.Districts.Count ? OnlineText.District(state.Districts[order.District]) : string.Empty,
                        Total = order.Total > 0 ? order.Total : order.Asked, Day = order.Placed / 24, Open = true,
                        Stage = order.Stage == OrderStage.Waiting ? _words.T("reallife_ui_shop_stage_waiting",
                            "Packing") : order.Stage == OrderStage.Picked ? _words.T("reallife_ui_shop_stage_picked", "Waiting for a van")
                            : _words.T("reallife_ui_shop_stage_out", "On its way")
                    });
                }
                var done = new ShopOrderFact[x.Done.Count];
                for (int o = 0; o < done.Length; o++)
                {
                    FinishedOrder fo = x.Done[done.Length - 1 - o];
                    done[o] = new ShopOrderFact
                    {
                        Customer = CustomerOf(state.Seed, fo.Person, fo.Person), Items = _words.F("reallife_ui_shop_n_items", "{count} products", "count", fo.Items.ToString()),
                        District = fo.District < state.Districts.Count ? OnlineText.District(state.Districts[fo.District]) : string.Empty, Total = fo.Total, Day = fo.Day,
                            Late = fo.Late, Returned = fo.Returned
                    };
                }
                sites[i] = new ShopSiteFact
                {
                    Key = shop.Place, Name = shop.Name, Business = p.Business, Quality = OnlineText.Quality(shop.Quality), Orders = shop.Today.Orders,
                        Queue = shop.Orders.Count, Overdue = shop.Overdue(now),
                    Halves = Math.Max(0, Math.Min(10, shop.Stars / 100)), Reviews = x.Reviews, Takings = shop.Today.Takings, Profit = shop.Today.Profit, Week = week.Takings,
                        Offline = shop.Offline,
                    Express = OnlineExtraRules.Express(x, state.Districts.Count), FeeCents = x.FeeCents, FreeOverCents = x.FreeOverCents, Packing = x.Packing, Night = x.Night,
                        Match = x.Match,
                    ShowStock = x.ShowStock, StoreName = shop.Name, Areas = areas, Sales30 = sales, SalesDays = names, Lines = lines, Open = open.ToArray(), Done = done,
                        CanPay = shop.Offline,
                        CanAdvertise = shop.AdEnd <= 0,
                    HostingLine = shop.Offline ? OnlineText.T("reallife_online_row_offline") : null
                };
                StoreFacts(d, state, p, x, sites[i], now);
            }
            return new ShopFacts { Sites = sites };
        }

        // A customer is a person of the shared people pool, the one the order chose (OnlineDirector.CustomerOf); a saved person number from before the pool is taken modulo its size,
        // and with no pool running the order is named by its number.
        private string CustomerOf(int seed, int order, int saved = 0)
        {
            int people = RealLifeOverhaul.Population.CityPopulation.People;
            int id = saved > 0 && people > 0 ? 1 + (int)((uint)saved % (uint)people) : OnlineDirector.CustomerOf(seed, order);
            EstatePerson p = id > 0 ? EstateLive.ReadPerson(id) : default(EstatePerson);
            return p.Found && !string.IsNullOrEmpty(p.Name) ? p.Name : _words.F("reallife_ui_shop_customer_n", "Customer {n}", "n", order.ToString(CultureInfo.InvariantCulture));
        }

        private static string ItemsOf(OnlineDirector d, WebShop shop, WebOrder order)
        {
            var sb = new System.Text.StringBuilder(48);
            for (int j = 0; j < order.Count; j++)
            {
                string item = order.Line[j] >= 0 && order.Line[j] < shop.Lines.Count ? shop.Lines[order.Line[j]].Item : null;
                sb.Append(j > 0 ? ", " : string.Empty).Append(order.Qty[j]).Append(" x ").Append(d.ItemName(item));
            }
            return sb.ToString();
        }

        // The shop as customers see it, from the same state the seller pages read: the products with their prices, reviews and promise, the rivals and the latest basket.
        private void StoreFacts(OnlineDirector d, OnlineState state, WebPlace p, ShopExtra x, ShopSiteFact site, int now)
        {
            WebShop shop = p.Shop;
            var names = new List<string>(4);
            RealLifeOverhaul.Economy.Ventures.VenturesGame.RivalNames(names);
            var rivals = new ShopRivalFact[OnlineExtraRules.RivalsMost];
            for (int r = 0; r < rivals.Length; r++)
            {
                rivals[r] = new ShopRivalFact { Index = r, Name = names.Count > 0 ? names[r % names.Count] : _words.F("reallife_ui_shop_rival_n", "Seller {n}", "n",
                    (r + 1).ToString(CultureInfo.InvariantCulture)) };
            }
            int due = OnlineRules.Due(shop.Promise, now);
            OnlineExtraRules.ArrivesBy(due, now, out int days, out int hour);
            string arrives = days <= 0 ? _words.F("reallife_ui_shop_arrives_today", "Today by {hour}", "hour", hour.ToString("00", CultureInfo.InvariantCulture) + ":00")
                : _words.F("reallife_ui_shop_arrives_tomorrow", "Tomorrow by {hour}", "hour", hour.ToString("00", CultureInfo.InvariantCulture) + ":00");
            var products = new List<ShopProductFact>(site.Lines.Length);
            var seen = new int[5];
            var rival = new int[5];
            var rivalLines = new List<ShopRivalLine>[rivals.Length];
            for (int r = 0; r < rivals.Length; r++)
            {
                rivalLines[r] = new List<ShopRivalLine>(site.Lines.Length);
            }
            var rivalStars = new long[rivals.Length];
            var rivalCount = new long[rivals.Length];
            for (int l = 0; l < site.Lines.Length; l++)
            {
                ShopLineFact line = site.Lines[l];
                LineExtra le = x.LineOf(line.Item, false);
                int count = OnlineExtraRules.Breakdown(x, line.Item, seen);
                var prod = new ShopProductFact
                {
                    Item = line.Item, Name = line.Name, Title = le != null ? le.Title : null, Desc = le != null ? le.Desc : null, Cents = line.AskCents,
                        WasCents = le != null ? le.WasCents : 0,
                    Stock = line.Stock, OnlyLeft = OnlineExtraRules.OnlyLeft(line.Stock), Express = site.Express, On = line.On, Arrives = arrives,
                        Halves = OnlineExtraRules.Halves(seen), Count = count,
                    Breakdown = (int[])seen.Clone()
                };
                prod.PercentOff = prod.WasCents > prod.Cents && prod.WasCents > 0 ? (int)((prod.WasCents - prod.Cents) * 100 / prod.WasCents) : 0;
                var reviews = new List<ShopReviewFact>(6);
                for (int n = x.Notes.Count - 1; n >= 0 && reviews.Count < 6; n--)
                {
                    ReviewNote note = x.Notes[n];
                    if (string.Equals(note.Item, line.Item, StringComparison.Ordinal))
                    {
                        reviews.Add(new ShopReviewFact
                        {
                            Index = n, Name = CustomerOf(state.Seed, n, note.Person), Stars = note.Stars, Day = note.Day, Reply = note.Reply,
                            District = note.District >= 0 && note.District < state.Districts.Count ? OnlineText.District(state.Districts[note.District]) : string.Empty
                        });
                    }
                }
                prod.Reviews = reviews.ToArray();
                var sellers = new List<ShopSellerFact>(rivals.Length + 1)
                {
                    new ShopSellerFact { Name = site.Name, Cents = line.AskCents, Halves = prod.Halves, Reviews = count, Express = site.Express, Mine = true, Rival = -1 }
                };
                for (int r = 0; r < rivals.Length; r++)
                {
                    int halves;
                    int rc = OnlineExtraRules.RivalReviews(state.Seed, r, line.Item, rival, out halves);
                    long cents = line.GoingCents > 0 ? OnlineExtraRules.RivalPrice(state.Seed, r, line.Item, (int)line.GoingCents) : 0;
                    if (cents <= 0)
                    {
                        continue;
                    }
                    sellers.Add(new ShopSellerFact { Name = rivals[r].Name, Cents = cents, Halves = halves, Reviews = rc, Express = r % 2 == 0, Rival = r });
                    rivalLines[r].Add(new ShopRivalLine { Name = line.Name, Cents = cents, Halves = halves, Count = rc });
                    rivalStars[r] += halves * rc;
                    rivalCount[r] += rc;
                }
                sellers.Sort((a, b) => a.Cents.CompareTo(b.Cents));
                prod.Sellers = sellers.ToArray();
                products.Add(prod);
            }
            for (int r = 0; r < rivals.Length; r++)
            {
                rivals[r].Lines = rivalLines[r].ToArray();
                rivals[r].Reviews = (int)Math.Min(int.MaxValue, rivalCount[r]);
                rivals[r].Halves = rivalCount[r] > 0 ? (int)(rivalStars[r] / rivalCount[r]) : -1;
                rivals[r].Express = r % 2 == 0;
            }
            site.Products = products.ToArray();
            site.Rivals = rivals;
            site.Basket = BasketOf(d, state, shop, x, site, arrives);
        }

        // The latest order, open or finished, as its customer saw the basket: its lines at the prices of the day, the fee the rule asks and the promise.
        private ShopBasketFact BasketOf(OnlineDirector d, OnlineState state, WebShop shop, ShopExtra x, ShopSiteFact site, string arrives)
        {
            WebOrder last = shop.Orders.Count > 0 ? shop.Orders[shop.Orders.Count - 1] : null;
            if (last == null)
            {
                return null;
            }
            var lines = new List<ShopBasketLine>(last.Count);
            long subtotal = 0;
            for (int j = 0; j < last.Count; j++)
            {
                string item = last.Line[j] >= 0 && last.Line[j] < shop.Lines.Count ? shop.Lines[last.Line[j]].Item : null;
                lines.Add(new ShopBasketLine { Name = d.ItemName(item), Qty = last.Qty[j], Cents = last.Unit[j] });
                subtotal += (long)last.Qty[j] * last.Unit[j];
            }
            long fee = OnlineExtraRules.FeeFor(x, (int)Math.Min(int.MaxValue, subtotal));
            return new ShopBasketFact
            {
                Customer = CustomerOf(state.Seed, last.Id), Arrives = arrives, Lines = lines.ToArray(), Subtotal = subtotal, Fee = fee, FreeOver = x.FreeOverCents,
                ToFree = x.FreeOverCents > 0 && subtotal < x.FreeOverCents && x.FeeCents > 0 ? x.FreeOverCents - subtotal : 0, Total = subtotal + fee
            };
        }

        // ------------------------------------------------------------------------------------------------- the buttons

        private void Done()
        {
            _local++;
        }

        public void Fee(string place, int cents)
        {
            OnlineDirector.Instance?.SetFee(place, cents);
            Done();
        }

        public void FreeOver(string place, int cents)
        {
            OnlineDirector.Instance?.SetFreeOver(place, cents);
            Done();
        }

        public void Packing(string place, int packing)
        {
            OnlineDirector.Instance?.SetPacking(place, packing);
            Done();
        }

        public void Toggle(string place, int which)
        {
            OnlineDirector.Instance?.Toggle(place, which);
            Done();
        }

        public void Area(string place, int district)
        {
            OnlineDirector.Instance?.ToggleDistrict(place, district);
            Done();
        }

        public void Price(string place, string item, int cents)
        {
            OnlineDirector.Instance?.SetPrice(place, item, cents);
            Done();
        }

        public void Offer(string place, string item, bool on)
        {
            OnlineDirector.Instance?.Offer(place, item, on);
            Done();
        }

        public void Pay(string place)
        {
            OnlineDirector.Instance?.PayHosting(place);
            Done();
        }

        public void Advertise(string place)
        {
            OnlineDirector.Instance?.Advertise(place);
            Done();
        }

        public void Rename(string place)
        {
            OnlineDirector d = OnlineDirector.Instance;
            WebPlace p = d != null ? d.PlaceAt(place) : null;
            if (p == null)
            {
                return;
            }
            RealLifeOverhaul.GameUi.TextBox.Ask("reallife_online_rename_placeholder", "common_confirm", text =>
            {
                d.Rename(place, text);
                Done();
            }, null, p.Shop.Name, false, OnlineCatalogue.NameMost);
        }

        private void Ask(string placeholder, string initial, int most, Action<string> done)
        {
            RealLifeOverhaul.GameUi.TextBox.Ask(placeholder, "common_confirm", text =>
            {
                done(text);
                Done();
            }, null, initial ?? string.Empty, true, most);
        }

        public void Title(string place, string item)
        {
            OnlineDirector d = OnlineDirector.Instance;
            LineExtra l = d != null && d.Extra != null ? d.Extra.Of(place, true).LineOf(item, false) : null;
            Ask("reallife_ui_shop_title_placeholder", l != null ? l.Title : string.Empty, OnlineCatalogue.NameMost, text => d.SetTitle(place, item, text));
        }

        public void Describe(string place, string item)
        {
            OnlineDirector d = OnlineDirector.Instance;
            LineExtra l = d != null && d.Extra != null ? d.Extra.Of(place, true).LineOf(item, false) : null;
            Ask("reallife_ui_shop_desc_placeholder", l != null ? l.Desc : string.Empty, 200, text => d.SetDescription(place, item, text));
        }

        public void Reply(string place, int note)
        {
            OnlineDirector d = OnlineDirector.Instance;
            ShopExtra x = d != null && d.Extra != null ? d.Extra.Of(place, false) : null;
            if (x == null || note < 0 || note >= x.Notes.Count)
            {
                return;
            }
            Ask("reallife_ui_shop_reply_placeholder", x.Notes[note].Reply, 160, text => d.Reply(place, note, text));
        }

        public void Refund(string place, int order)
        {
            OnlineDirector d = OnlineDirector.Instance;
            if (d == null)
            {
                return;
            }
            GameConfirm.Show("reallife_ui_shop_refund_title".Localize(), "reallife_ui_shop_refund_body".Localize(new { id = order.ToString(CultureInfo.InvariantCulture) }), Guard.Wrap(() =>
            {
                d.RefundOrder(place, order);
                Done();
            }), null, null, null, true, true);
        }

        public void Courier(string place, int order)
        {
            OnlineDirector d = OnlineDirector.Instance;
            if (d == null)
            {
                return;
            }
            GameConfirm.Show("reallife_ui_shop_send_title".Localize(), "reallife_ui_shop_send_body".Localize(new { id = order.ToString(CultureInfo.InvariantCulture) }), Guard.Wrap(() =>
            {
                d.SendCourier(place, order);
                Done();
            }), null, null, null, true, false);
        }

        public void Close(string place)
        {
            OnlineDirector d = OnlineDirector.Instance;
            WebPlace p = d != null ? d.PlaceAt(place) : null;
            if (p == null)
            {
                return;
            }
            GameConfirm.Show("reallife_online_confirm_shut_title".Localize(new { name = p.Shop.Name }),
                "reallife_online_confirm_shut_body".Localize(new { orders = p.Shop.Orders.Count.ToString() }),
                Guard.Wrap(() =>
                {
                    _view.S1 = null;
                    d.CloseShop(place);
                    Done();
                }), null, null, null, true, true);
        }
    }
}
