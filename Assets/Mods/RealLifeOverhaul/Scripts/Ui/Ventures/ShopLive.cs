using System;
using System.Collections.Generic;
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

        private static void TakeWant(VentView view)
        {
            if (_wantTab < 0)
            {
                return;
            }
            view.Tab = Math.Max(0, Math.Min(2, _wantTab));
            view.Pick = null;
            view.S1 = _wantPick != null && _wantPick.StartsWith("shop:", StringComparison.Ordinal) ? ShopPages.SiteKey.Length > 0 ? _wantPick.Substring(5) : null : null;
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
                        Customer = CustomerOf(state.Seed, order.Id), Items = ItemsOf(d, shop, order),
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
                        Customer = CustomerOf(state.Seed, fo.Person), Items = _words.F("reallife_ui_shop_n_items", "{count} products", "count", fo.Items.ToString()),
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
                    ShowStock = x.ShowStock, Areas = areas, Sales30 = sales, SalesDays = names, Lines = lines, Open = open.ToArray(), Done = done, CanPay = shop.Offline,
                        CanAdvertise = shop.AdEnd <= 0,
                    HostingLine = shop.Offline ? OnlineText.T("reallife_online_row_offline") : null
                };
            }
            return new ShopFacts { Sites = sites };
        }

        // A customer is a name from the catalogue's own first and last names, chosen by the order: the same order always has the same customer.
        private static string CustomerOf(int seed, int order)
        {
            string[] first = RealLifeOverhaul.Economy.Ventures.VenturesCatalogue.FirstNames, last = RealLifeOverhaul.Economy.Ventures.VenturesCatalogue.LastNames;
            uint h = unchecked((uint)(seed * 31 + order) * 2654435761u);
            return first[(int)((h >> 4) % (uint)first.Length)] + " " + last[(int)((h >> 12) % (uint)last.Length)];
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
