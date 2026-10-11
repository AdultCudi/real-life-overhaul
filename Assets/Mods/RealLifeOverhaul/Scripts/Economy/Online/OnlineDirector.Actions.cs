using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the player does with a site from the Web shop app: opens one, changes who delivers and what is promised,
    /// has a better site built, advertises, pays hosting that fell behind, puts products on and off the site and
    /// prices them, renames it, closes it. Each is one call from a button; anything that costs is taken as a
    /// transaction of its own and refused, with the game's own notice, if the cash does not cover it.
    /// </summary>
    public sealed partial class OnlineDirector
    {
        private readonly List<string> _stocked = new List<string>(32);

        private bool Ready => _running && !_broken && Save != null && _state != null;

        /// <summary>What a site of a quality would cost to set up today, and a month of its hosting, with whether the player's own agency would build it.</summary>
        public void Quote(int quality, out int setUp, out int hosting, out bool inHouse)
        {
            inHouse = OnlineGame.HasAgency();
            setUp = OnlineRules.SetUpCost(quality, inHouse, _index);
            hosting = OnlineRules.HostingCost(quality, _index);
        }

        /// <summary>
        /// Opens a site for a business or a warehouse. The set-up is charged (unless <paramref name="free"/>, a test)
        /// and has the first month's hosting in it; everything the place stocks is put on the site, up to the
        /// catalogue's size, at the shop's own prices.
        /// </summary>
        public WebPlace OpenShop(string placeKey, int quality, int way, int promise, bool free)
        {
            BuildingRegistration reg = Ready ? OnlineGame.Find(placeKey) : null;
            if (reg == null || _state.Shop(placeKey) != null)
            {
                return null;
            }
            if (_state.Shops.Count >= OnlineCatalogue.ShopsMost)
            {
                Notifications.Show(NotificationType.Warning, "reallife_online_note_most", new Dictionary<string, string> { { "count", OnlineCatalogue.ShopsMost.ToString() } }, 7f, null, null, true, false);
                return null;
            }
            int setUp, hosting, day = Save.Day;
            bool inHouse;
            Quote(quality, out setUp, out hosting, out inHouse);
            string name = OnlineStore.Clean(OnlineText.T("reallife_online_name_default", "business", OnlineGame.NameOf(reg)));
            name = name.Length > OnlineCatalogue.NameMost ? name.Substring(0, OnlineCatalogue.NameMost).Trim() : name;
            WebShop shop = OnlineRules.Open(_state, placeKey, name, quality, way, promise, inHouse, day, MonthDays);
            if (shop == null)
            {
                return null;
            }
            ReadPlaces();
            WebPlace place = PlaceAt(placeKey);
            if (place == null || (!free && !OnlineGame.Take(setUp * 100, OnlineGame.SetUpKey, place, null, true, false)))
            {
                _state.Shops.Remove(shop);
                ReadPlaces();
                if (place != null)
                {
                    Notifications.ShowInsufficientMoney();
                }
                return null;
            }
            OnlineGame.Stocked(reg, _stocked);
            for (int i = 0; i < _stocked.Count && shop.Lines.Count < OnlineCatalogue.LinesMost; i++)
            {
                OnlineRules.Offer(shop, _stocked[i]);
            }
            _stocked.Clear();
            PlanAll(day);   // (every site shares the city with one more)
            OnlineGame.CountStock(place);
            Logger?.Info("Online shops: " + shop.Name + " opened at " + OnlineGame.NameOf(reg) + (place.Warehouse ? " (a warehouse)" : string.Empty) + ": quality " + shop.Quality + (inHouse ? ", built by the player's own web agency" : string.Empty)
                         + ", set-up " + (free ? "free (a test)" : setUp.ToString()) + ", hosting " + hosting + " a month from day " + shop.HostDue + ", " + shop.Lines.Count + " product(s) on it, "
                         + (place.Site.PerDay / 1000f).ToString("0.0") + " order(s) a day expected.");
            Notifications.Show(NotificationType.Success, "reallife_online_note_opened", new Dictionary<string, string> { { "name", shop.Name }, { "count", shop.Lines.Count.ToString() } }, 9f, null,
                () => Open(TabShops, "shop:" + placeKey), true, false);
            Changed();
            return place;
        }

        /// <summary>Closes a site: its day's book is paid in, its open orders are dropped unrated, and it is gone.</summary>
        public void CloseShop(string placeKey)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            if (place == null)
            {
                return;
            }
            string name = place.Shop.Name;
            CloseBook(place, Save.Day);
            _state.Shops.Remove(place.Shop);
            ReadPlaces();
            PlanAll(Save.Day);   // (the sites that are left share the city between fewer)
            Logger?.Info("Online shops: " + name + " closed by the player. Its day's book was paid in; its open orders are dropped.");
            Changed();
        }

        public void SetWay(string placeKey, int way)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            if (place != null && way >= 0 && way < OnlineCatalogue.Ways && way != place.Shop.Way)
            {
                place.Shop.Way = way;
                Logger?.Info("Online shops: " + place.Shop.Name + " now delivers by " + (way == OnlineCatalogue.Courier ? "courier" : "the player's own vans") + ".");
                if (way == OnlineCatalogue.Courier)
                {
                    HandToCourier(place);   // (what was packed for a van is collected by the courier at once, at its fee; what a courier already has stays with it)
                }
                Changed();
            }
        }

        // Packed orders that waited for a van are collected by the courier now.
        private void HandToCourier(WebPlace place)
        {
            WebShop shop = place.Shop;
            int now = Now;
            for (int i = 0; i < shop.Orders.Count; i++)
            {
                WebOrder o = shop.Orders[i];
                if (o.Stage == OrderStage.Picked)
                {
                    int metres = o.District >= 0 && o.District < place.Site.Districts ? place.Site.Metres[o.District] : 0;
                    o.Stage = OrderStage.Out;
                    o.Arrive = now + OnlineRules.CourierTime(metres);
                    shop.Today.Delivery += OnlineRules.CourierFee(metres);
                }
            }
        }

        public void SetPromise(string placeKey, int promise)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            if (place != null && promise >= 0 && promise <= 1 && promise != place.Shop.Promise)
            {
                place.Shop.Promise = promise;   // (orders already taken keep the promise they were given)
                Replan(place);
                Changed();
            }
        }

        /// <summary>Has a better site built: the difference between the two set-up prices today.</summary>
        public void Upgrade(string placeKey, int quality)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            if (place == null || quality <= place.Shop.Quality || quality >= OnlineCatalogue.Qualities)
            {
                return;
            }
            bool inHouse = OnlineGame.HasAgency();
            int cost = Math.Max(0, OnlineRules.SetUpCost(quality, inHouse, _index) - OnlineRules.SetUpCost(place.Shop.Quality, inHouse, _index));
            if (!OnlineGame.Take(cost * 100, OnlineGame.SetUpKey, place, null, true, false))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            Logger?.Info("Online shops: " + place.Shop.Name + " rebuilt from quality " + place.Shop.Quality + " to " + quality + " for " + cost + (inHouse ? ", by the player's own web agency" : string.Empty) + ".");
            place.Shop.Quality = quality;
            place.Shop.InHouse = inHouse;
            Replan(place);
            Changed();
        }

        /// <summary>What advertising a site online would cost today and the orders it would add from tomorrow.</summary>
        public void AdQuote(WebPlace place, out int cost, out int percent)
        {
            OnlineRules.AdQuote(place.Shop, Save.Day, _index, (int)CityEconomy.State, out cost, out percent);
        }

        public void Advertise(string placeKey)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            if (place == null || !OnlineRules.CanAdvertise(place.Shop))
            {
                return;
            }
            int cost, percent;
            AdQuote(place, out cost, out percent);
            if (!OnlineGame.Take(cost * 100, OnlineGame.AdsKey, place, null, true, false))
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            OnlineRules.Advertise(place.Shop, Save.Day, percent);
            Logger?.Info("Online shops: advertising booked for " + place.Shop.Name + ": cost " + cost + ", +" + percent + "% of orders on days " + place.Shop.AdFrom + " to " + (place.Shop.AdEnd - 1) + ".");
            Notifications.Show(NotificationType.Success, "reallife_online_note_ads", new Dictionary<string, string>
            {
                { "name", place.Shop.Name }, { "from", place.Shop.AdFrom.ToString() }, { "to", (place.Shop.AdEnd - 1).ToString() }, { "percent", percent.ToString() }
            }, 9f, null, null, true, false);
            Changed();
        }

        /// <summary>Hosting that fell behind is paid now, if the cash covers it.</summary>
        public void PayHosting(string placeKey)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            if (place == null || !place.Shop.Offline)
            {
                return;
            }
            Host(place, Save.Day);
            if (place.Shop.Offline)
            {
                Notifications.ShowInsufficientMoney();
                return;
            }
            Replan(place);
            Changed();
        }

        public void Rename(string placeKey, string name)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            name = OnlineStore.Clean(name ?? string.Empty).Trim();
            if (place != null && name.Length > 0)
            {
                place.Shop.Name = name.Length > OnlineCatalogue.NameMost ? name.Substring(0, OnlineCatalogue.NameMost).Trim() : name;
                Changed();
            }
        }

        /// <summary>Puts a product on a site or takes it off. A product taken off stays in the list until nothing hangs on it (OnlineRules.Prune).</summary>
        public void Offer(string placeKey, string item, bool on)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            if (place == null || string.IsNullOrEmpty(item))
            {
                return;
            }
            if (on)
            {
                if (OnlineRules.Offer(place.Shop, item) == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_online_note_full", new Dictionary<string, string> { { "count", OnlineCatalogue.LinesMost.ToString() } }, 7f, null, null, true, false);
                    return;
                }
            }
            else
            {
                WebLine line = place.Shop.Line(item);
                if (line == null)
                {
                    return;
                }
                line.On = false;
                OnlineRules.Prune(place.Shop);
            }
            Replan(place);
            OnlineGame.CountStock(place);
            Changed();
        }

        /// <summary>A product's online price in cents; 0 follows the shop's own price.</summary>
        public void SetPrice(string placeKey, string item, int cents)
        {
            WebPlace place = Ready ? PlaceAt(placeKey) : null;
            WebLine line = place != null ? place.Shop.Line(item) : null;
            if (line != null && cents >= 0 && cents != line.Price)
            {
                line.Price = cents;
                Replan(place);
                Changed();
            }
        }

        /// <summary>The retail products a place has in stock, for the app's catalogue page. One pass over what stands in the building.</summary>
        public void Stocked(WebPlace place, List<string> into)
        {
            into.Clear();
            if (Ready && place != null && place.Reg != null)
            {
                OnlineGame.Stocked(place.Reg, into);
            }
        }

        // What the app writes of a business or a product, asked here so that the app itself never reads the game.
        public string KeyOf(BuildingRegistration reg) => OnlineGame.Place(reg);
        public string NameOf(BuildingRegistration reg) => OnlineGame.NameOf(reg);
        public string TypeOf(BuildingRegistration reg) => OnlineGame.TypeOf(reg);
        public string ItemName(string item) => OnlineGame.ItemName(item);
        public int ShopPrice(WebPlace place, string item) => OnlineGame.ShopPrice(place, item);
        public int StockOf(WebPlace place, string item) => place != null ? OnlineGame.InStock(place.Reg, item) : 0;

        /// <summary>The businesses and warehouses of the player's that have no site yet.</summary>
        public void Candidates(List<BuildingRegistration> into)
        {
            into.Clear();
            if (!Ready)
            {
                return;
            }
            OnlineGame.Candidates(into);
            for (int i = into.Count - 1; i >= 0; i--)
            {
                if (_state.Shop(OnlineGame.Place(into[i])) != null)
                {
                    into.RemoveAt(i);
                }
            }
        }
    }
}
