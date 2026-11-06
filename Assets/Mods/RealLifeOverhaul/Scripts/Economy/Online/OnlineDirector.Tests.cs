using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The online shops' director, part: the test tools. The state before the first test is kept to be put back; stock and money stay moved.</summary>
    public sealed partial class OnlineDirector
    {
        private const int TestOrdersCount = 10;
        private const int TestSalt = 0x5bd1e995;    // test orders come from another seed, so they are not the hour's own orders again

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyOnline || _broken || !_running || _state == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        // Keeps the state once before a test changes it. Each site's day is paid in first, so putting it back can never pay twice.
        private void BeforeTest()
        {
            if (_beforeTests != null)
            {
                return;
            }
            for (int i = 0; i < _places.Count; i++)
            {
                CloseBook(_places[i], Save.Day);
            }
            _beforeTests = OnlineStore.Write(_state);
        }

        private void TestDone(string what, int tab, string pick)
        {
            Logger?.Warn("Online test: " + what + ".");   // asked for by a button: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_online_test_done", null, 7f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
            Open(tab, pick);
        }

        private void TestGuard(string test, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
            }
        }

        // The first site, its day worked out now; null with a refusal if the player has none.
        private WebPlace TestSite(string test)
        {
            if (_places.Count == 0)
            {
                TestRefusals.TellKey(Logger, test, "reallife_online_test_no_site");
                return null;
            }
            Replan(_places[0]);
            return _places[0];
        }

        // One order drawn from the test's seed and picked at once from the real stock; null with a refusal if nothing could be.
        private WebOrder TestOrder(string test, WebPlace place, int number)
        {
            WebShop shop = place.Shop;
            int seed = _state.Seed, now = Now;
            _state.Seed = seed ^ TestSalt;
            WebOrder o = OnlineRules.Draw(_state, shop, place.Site, now, OnlineRules.Key(shop.Place), 200 + number);
            _state.Seed = seed;
            if (o == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_online_test_nothing_on");
                return null;
            }
            shop.Orders.Add(o);
            shop.Today.Orders++;
            var count = new HourCount();
            _stockAt = place;
            bool picked = OnlineRules.PickOrder(shop, place.Site, now, shop.Orders.Count - 1, _take, ref count);
            OnlineGame.Picked(place.Reg);
            _stockAt = null;
            if (!picked)
            {
                TestRefusals.TellKey(Logger, test, "reallife_online_test_no_stock");
                Changed();
                return null;
            }
            return o;
        }

        /// <summary>"Test: open a test web shop on my busiest business": a free Standard site with a courier and a next-day promise.</summary>
        public void TestOpen()
        {
            if (!TestReady("Online open"))
            {
                return;
            }
            TestGuard("open", () =>
            {
                BuildingRegistration reg = OnlineGame.Busiest(_state);
                if (reg == null)
                {
                    TestRefusals.TellKey(Logger, "Online open", "reallife_online_test_no_business");
                    return;
                }
                BeforeTest();
                string key = OnlineGame.Place(reg);
                WebPlace place = OpenShop(key, 1, OnlineCatalogue.Courier, 1, true);
                if (place == null)
                {
                    TestRefusals.TellKey(Logger, "Online open", "reallife_online_test_no_business");
                    return;
                }
                TestDone("a Standard site opened free at " + place.Business + " with " + place.Shop.Lines.Count + " product(s) from its stock; "
                         + (place.Site.PerDay / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " order(s) a day expected", TabShops, "shop:" + key);
            });
        }

        /// <summary>"Test: ten orders": they come in at the first site now, whatever its demand, and wait to be picked.</summary>
        public void TestOrders()
        {
            if (!TestReady("Online orders"))
            {
                return;
            }
            TestGuard("orders", () =>
            {
                WebPlace place = TestSite("Online orders");
                if (place == null)
                {
                    return;
                }
                WebShop shop = place.Shop;
                int seed = _state.Seed, now = Now, key = OnlineRules.Key(shop.Place), came = 0;
                BeforeTest();
                _state.Seed = seed ^ TestSalt;
                for (int k = 0; k < TestOrdersCount && shop.Orders.Count < OnlineCatalogue.OpenMost; k++)
                {
                    WebOrder o = OnlineRules.Draw(_state, shop, place.Site, now, key, 100 + k);
                    if (o == null)
                    {
                        break;
                    }
                    shop.Orders.Add(o);
                    shop.Today.Orders++;
                    came++;
                }
                _state.Seed = seed;
                if (came == 0)
                {
                    TestRefusals.TellKey(Logger, "Online orders", "reallife_online_test_nothing_on");
                    return;
                }
                TestDone(came + " order(s) came in at " + shop.Name + "; " + place.Pickers + " member(s) of staff picked there in the last hour, and each picks "
                         + OnlineCatalogue.PickPerHour + " an hour", TabOrders, null);
            });
        }

        /// <summary>"Test: a late delivery": one order picked from the real stock now reaches its door two hours after its promise.</summary>
        public void TestLate()
        {
            if (!TestReady("Online late"))
            {
                return;
            }
            TestGuard("late", () =>
            {
                WebPlace place = TestSite("Online late");
                if (place == null)
                {
                    return;
                }
                BeforeTest();
                WebOrder o = TestOrder("Online late", place, 1);
                if (o == null)
                {
                    return;
                }
                WebShop shop = place.Shop;
                int now = Now, stars = shop.Stars, refunds = shop.Today.Refunds, total = o.Total, id = o.Id;
                o.Stage = OrderStage.Out;
                o.Due = now - 2;
                o.Arrive = now;
                var count = new HourCount();
                OnlineRules.Deliver(_state, shop, place.Site, now, ref count);
                TestDone("order " + id + " of " + Cents(total) + " at " + shop.Name + " came two hours late: " + Cents(shop.Today.Refunds - refunds) + " refunded (wanted "
                         + Cents(OnlineCatalogue.Share(total, OnlineCatalogue.LateRefund)) + "), the rating " + stars + " to " + shop.Stars
                         + " thousandths; its goods really left the stock", TabShops, "shop:" + shop.Place);
            });
        }

        /// <summary>"Test: a returned order": one order picked from the real stock now comes back at once, refunded, its goods restocked if fit.</summary>
        public void TestReturn()
        {
            if (!TestReady("Online return"))
            {
                return;
            }
            TestGuard("return", () =>
            {
                WebPlace place = TestSite("Online return");
                if (place == null)
                {
                    return;
                }
                BeforeTest();
                WebOrder o = TestOrder("Online return", place, 2);
                if (o == null)
                {
                    return;
                }
                WebShop shop = place.Shop;
                int line = -1;
                for (int j = 0; j < o.Count && line < 0; j++)
                {
                    line = o.Got[j] > 0 && o.Line[j] >= 0 && o.Line[j] < shop.Lines.Count ? j : -1;
                }
                if (line < 0)
                {
                    TestRefusals.TellKey(Logger, "Online return", "reallife_online_test_no_stock");
                    return;
                }
                WebReturn back = OnlineRules.NewReturn();
                back.Day = Save.Day;
                back.Item = shop.Lines[o.Line[line]].Item;
                back.Qty = o.Got[line];
                back.Refund = o.Got[line] * o.Unit[line];
                back.Cost = o.Paid[line] / o.Got[line];
                string item = back.Item;
                int units = back.Qty, refund = back.Refund, id = o.Id, goods = shop.Today.Goods, before = OnlineGame.InStock(place.Reg, item);
                shop.Today.OnTime++;
                shop.Orders.Remove(o);
                OnlineRules.Recycle(o);
                shop.Returns.Add(back);
                var count = new HourCount();
                _stockAt = place;
                OnlineRules.Returns(_state, shop, place.Site, Save.Day * 24 + 23, _put, ref count);
                _stockAt = null;
                int after = OnlineGame.InStock(place.Reg, item);
                TestDone("order " + id + " at " + shop.Name + " came back: " + units + " of " + item + ", " + Cents(refund) + " refunded; " + (after - before)
                         + " unit(s) went back into stock (" + before + " to " + after + ") and " + (units - (after - before)) + " were written off, the cost of goods "
                         + Cents(goods) + " to " + Cents(shop.Today.Goods) + " (goods of kind " + place.Site.Kind + " go back "
                         + OnlineCatalogue.ResaleShare[OnlineCatalogue.Clamp(place.Site.Kind, 0, OnlineCatalogue.ResaleShare.Length - 1)] / 10 + "% of the time)",
                    TabShops, "shop:" + shop.Place);
            });
        }

        /// <summary>"Test: a day of orders passes": the last twenty-four hours again at every site from the test's seed, the books paid in at once.</summary>
        public void TestDay()
        {
            if (!TestReady("Online day"))
            {
                return;
            }
            TestGuard("day", () =>
            {
                if (TestSite("Online day") == null)
                {
                    return;
                }
                BeforeTest();
                PlanAll(Save.Day);
                int seed = _state.Seed, now = Now, came = 0, picked = 0, lost = 0, arrived = 0, late = 0;
                float cash = Save.Money;
                _state.Seed = seed ^ TestSalt;
                for (int hour = now - 23; hour <= now; hour++)
                {
                    OnlineGame.ReadVans(_state, hour / 24);
                    for (int i = 0; i < _places.Count; i++)
                    {
                        WebPlace place = _places[i];
                        place.Pickers = OnlineGame.Pickers(place, _state, hour);
                        _stockAt = place;
                        HourCount count = OnlineRules.Hour(_state, place.Shop, place.Site, hour, place.Pickers, OnlineGame.OpenAt(place, hour), _take, _put);
                        OnlineGame.Picked(place.Reg);
                        came += count.Came;
                        picked += count.Picked;
                        lost += count.Cancelled + count.Missing;
                        arrived += count.Arrived;
                        late += count.Late;
                    }
                }
                _stockAt = null;
                _state.Seed = seed;
                for (int v = 0; v < _state.Vans.Count; v++)
                {
                    _state.Vans[v].Day = now / 24;   // what the vans drove in the test counts against today
                }
                int open = 0;
                for (int i = 0; i < _places.Count; i++)
                {
                    open += _places[i].Shop.Orders.Count;
                    CloseBook(_places[i], Save.Day);
                }
                PlanAll(Save.Day);
                TestDone("twenty-four hours worked through at " + _places.Count + " site(s): " + came + " order(s) came, " + picked + " picked from real stock, " + lost
                         + " cancelled or given up, " + arrived + " at the door of which " + late + " late, " + open + " still open; the books were paid in and the game's cash moved by "
                         + (Save.Money - cash).ToString("0.00", CultureInfo.InvariantCulture), TabShops, "shop:" + _places[0].Shop.Place);
            });
        }

        /// <summary>
        /// "Web shop: connection and orders a day": the first site's day worked out now, its connection read as the hour
        /// reads it, then a day of far more orders than any bound drawn on a copy of the site at each tier of speed: the
        /// day stops at the bound that speed allows, none come with no connection or in an outage. Nothing is kept: the
        /// copy's orders are given back and the seed, the order numbers and the site's day are as they were.
        /// </summary>
        public void TestNet()
        {
            const string test = "Online connection";
            if (!TestReady(test))
            {
                return;
            }
            TestGuard("connection", () =>
            {
                WebPlace place = TestSite(test);
                if (place == null)
                {
                    return;
                }
                WebShop shop = place.Shop;
                Site site = place.Site;
                bool sells = false;
                for (int l = 0; l < Math.Min(shop.Lines.Count, site.Lines); l++)
                {
                    sells |= site.Factor[l] > 0;
                }
                if (!sells || site.Districts <= 0)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_online_test_nothing_on");
                    return;
                }
                int mbps = PlanMbps(place.Reg), seed = _state.Seed, number = _state.NextOrder, perDay = site.PerDay, had = site.Mbps;
                bool down = site.Down, read = site.Mbps == mbps, bounds = true;
                var said = new System.Text.StringBuilder(96);
                _state.Seed = seed ^ TestSalt;
                try
                {
                    for (int t = -1; t <= NetSpeedRules.Floors.Length; t++)
                    {
                        // -1: no connection; the tiers; past them: an outage on the usual plan
                        int speed = t < 0 ? 0 : t < NetSpeedRules.Floors.Length ? NetSpeedRules.Floors[t] : NetSpeedRules.Floors[NetSpeedRules.UsualTier];
                        bool outage = t == NetSpeedRules.Floors.Length;
                        int came = TestDay(shop, site, speed, outage), most = outage ? 0 : Math.Min(OnlineRules.Most(shop.Quality, speed), OnlineCatalogue.OpenMost);
                        bounds &= came == most;
                        said.Append(outage ? "outage" : speed + " Mbps").Append(' ').Append(came).Append('/').Append(most).Append(", ");
                    }
                }
                finally
                {
                    _state.Seed = seed;
                    _state.NextOrder = number;
                    site.PerDay = perDay;
                    site.Mbps = had;
                    site.Down = down;
                }
                bool ok = read && bounds;
                int now = OnlineRules.Most(shop.Quality, site.Mbps);
                Logger?.Warn("Online test (connection, " + shop.Name + "): the building's plan reads " + mbps + " Mbps and the site's day read " + had
                             + "; orders a day drawn at each speed, came/bound: "
                             + said + "now a bound of " + now + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_online_test_net_pass" : "reallife_online_test_net_fail",
                    new Dictionary<string, string> { { "name", shop.Name }, { "most", now.ToString() } }, 10f);
                Changed();
            });
        }

        // A whole day's orders far over any bound, drawn on a copy of the site's shop at a speed; how many came.
        private int TestDay(WebShop shop, Site site, int speed, bool outage)
        {
            var copy = new WebShop { Place = shop.Place, Name = shop.Name, Quality = shop.Quality, Promise = shop.Promise, Stars = shop.Stars };
            copy.Lines.AddRange(shop.Lines);
            copy.Today.Clear(Save.Day);
            site.Mbps = speed;
            site.Down = outage;
            site.PerDay = OnlineCatalogue.OrdersMost[OnlineCatalogue.Qualities - 1] * 1000 * 4;
            int came = 0, first = Save.Day * 24;
            for (int h = 0; h < 24; h++)
            {
                came += OnlineRules.Arrive(_state, copy, site, first + h);
            }
            for (int i = 0; i < copy.Orders.Count; i++)
            {
                OnlineRules.Recycle(copy.Orders[i]);
            }
            copy.Orders.Clear();
            return came;
        }

        /// <summary>"Test: put online shops back": the state from before the first test returns; stock and money that moved stay moved.</summary>
        public void TestBack()
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, "Online back", TestRefusal.NotLoaded);
                return;
            }
            if (!_running || _broken)
            {
                TestRefusals.Tell(Logger, "Online back", TestRefusal.FeatureOff);
                return;
            }
            TestGuard("back", () =>
            {
                int version;
                OnlineState back = _beforeTests != null ? OnlineStore.Read(_beforeTests, out version) : null;
                _beforeTests = null;
                if (back == null)
                {
                    TestRefusals.TellKey(Logger, "Online back", "reallife_online_test_nothing_back");
                    return;
                }
                back.LastHour = Now;
                _state = back;
                OnlineGame.Districts(_state, _centres, _hasCentre);
                ReadPlaces();
                for (int i = 0; i < _places.Count; i++)
                {
                    WebShop shop = _places[i].Shop;
                    if (shop.Today.Day >= Save.Day)
                    {
                        continue;
                    }
                    // A night has passed since it was kept: that day was paid in, and its sales went into the statement.
                    OnlineRules.CloseDay(shop, Save.Day, _closed);
                    shop.BookRefunds = 0;
                    for (int l = 0; l < shop.Lines.Count; l++)
                    {
                        shop.Lines[l].BookTakings = shop.Lines[l].BookCost = 0;
                    }
                }
                PlanAll(Save.Day);
                TestDone("the state from before the tests is back (" + _places.Count + " site(s)); stock and money that moved since stay moved", TabShops, null);
            });
        }
    }
}
