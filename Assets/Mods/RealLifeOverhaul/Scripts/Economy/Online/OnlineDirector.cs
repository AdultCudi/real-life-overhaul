using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One row the Planner shows for the online shops.</summary>
    public struct OnlineAlert
    {
        public int A, B;        // the Planner's id for the row: what it is and which site
        public int Day;
        public string Title, What, Money, IfMissed;
    }

    /// <summary>
    /// Online shops: a business or a warehouse of the player's sells its stock to the whole city. Orders come by rule
    /// each game hour, are picked from the real stock and delivered by vans or a courier as time and money, as the
    /// game's own logistics do: no vehicle is put on the road. No Update, so no cost a frame: it follows the credit
    /// side's second, hour and day, made before the company's director so the company's quarter sees the online sales.
    /// </summary>
    public sealed partial class OnlineDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeOnline";
        public const int TabShops = 0, TabOrders = 1, TabCatalogue = 2;

        private static readonly string[] AppGlyphs = { "Icon-Cargo", "Icon-Shop", "Icon-Building" };

        public static IModLogger Logger;
        public static OnlineDirector Instance { get; private set; }

        private readonly List<WebPlace> _places = new List<WebPlace>(OnlineCatalogue.ShopsMost);
        private readonly List<WebPlace> _pool = new List<WebPlace>(OnlineCatalogue.ShopsMost);
        private readonly List<Vector3> _centres = new List<Vector3>(16);
        private readonly List<bool> _hasCentre = new List<bool>(16);
        private readonly List<OnlineAlert> _alerts = new List<OnlineAlert>(8);
        private readonly DayBook _closed = new DayBook();

        private OnlineState _state;
        private string _beforeTests;    // the state's line before the first test tool changed it; null without one
        private bool _running;
        private bool _broken;           // off for the session
        private bool _gaveUp;           // the game's phone is not as expected: no app, the rest runs
        private bool _roads;            // some site's roads are still to be measured
        private bool _dayOpen;          // the hour that opened a day has closed the books; its orders wait for the day event
        private int _index = CompanyCatalogue.IndexStart;
        private int _alertMark = -1;    // what the Planner's rows were last made from
        private WebPlace _stockAt;      // the place the stock delegates work on
        private StockTake _take;
        private StockBack _put;
        private OnlineApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public OnlineState State => _running ? _state : null;

        /// <summary>The sites with what was read from the game for each, in the state's order.</summary>
        public IReadOnlyList<WebPlace> Places => _places;

        public IReadOnlyList<OnlineAlert> Alerts => _alerts;

        public int MonthDays => MacroCatalogue.MonthDays(OnlineGame.DaysPerYear);

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;
        private static int Now => Save.Day * 24 + Save.Hour;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            _take = TakeStock;
            _put = PutStock;
            EconomyDirector.Follow(StartSeats.Online, _second);   // its first second after a load has a frame of its own (Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;   // another city's director has started since: the state is its
            }
            // Nothing is written to the save here: what a save keeps is written when the game saves.
            try
            {
                StandDown("the city is unloading", false, false, true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _running = false;
            }
            Instance = null;
        }

        private static readonly int HotSecond = HotSpots.Slot("Economy: OnlineDirector.Second");
        private void EachSecond()
        {
            using var hotSpot = HotSpots.Time(HotSecond);
            if (_broken)
            {
                return;
            }
            try
            {
                Slow();
            }
            catch (Exception e)
            {
                Fault("the timer", e);
            }
        }

        private void Slow()
        {
            if (Save == null || LoadScene.isLoading || !ModState.Loaded)
            {
                return;
            }
            EconomyDirector credit = EconomyDirector.Instance;
            if (!Settings.Economy || !Settings.EconomyOnline || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!Settings.EconomyOnline ? "the option is off" : "the credit side is not running", true, true, false);
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
            if (_roads)
            {
                MeasureRoad();
            }
        }

        private void StartUp()
        {
            GameInstance save = Save;
            int today = save.Day, now = Now;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            int version;
            OnlineState state = OnlineStore.Load(save.modData, OnlineStore.Key, out version);
            bool fresh = state == null;
            if (fresh)
            {
                state = new OnlineState { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = today };
            }
            string before;
            _beforeTests = save.modData.TryGetValue(OnlineStore.TestKey, out before) && !string.IsNullOrEmpty(before) ? before : null;
            _state = state;
            _running = true;
            _dayOpen = false;
            _netNone.Clear();
            _index = Prices.Level;
            OnlineGame.Districts(_state, _centres, _hasCentre);
            int gone = ReadPlaces();
            int missed = _state.LastHour < 0 ? 0 : now - _state.LastHour;
            if (missed > CatchUp || missed < 0)
            {
                // Hours went by without the feature: the option was off, or the save was played without the mod.
                for (int i = 0; i < _places.Count; i++)
                {
                    OnlineRules.Resume(_state, _places[i].Shop);
                }
                _state.LastHour = now;
            }
            else if (_state.LastHour < 0)
            {
                _state.LastHour = now;
            }
            for (int i = 0; i < _places.Count; i++)
            {
                if (_places[i].Shop.Today.Day < today)
                {
                    CloseBook(_places[i], today);   // a book left open by a day that ended without the feature
                }
            }
            PlanAll(today);
            BuildAlerts(today);
            Logger?.Info("Online shops: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". " + _places.Count + " site(s)"
                         + (gone > 0 ? ", " + gone + " closed because the business is no longer the player's" : string.Empty) + ", " + _state.Districts.Count + " district(s), "
                         + _state.Vans.Count + " van(s) known" + (missed > CatchUp ? "; " + missed + " hour(s) passed without the feature and nobody is blamed for them" : string.Empty) + ".");
        }

        // Ties each site to its building again. A site whose business is no longer the player's is closed, its day's book paid in first.
        private int ReadPlaces()
        {
            OnlineGame.ReadPlaces(_state, _places, _pool);
            int gone = 0;
            for (int i = _places.Count - 1; i >= 0; i--)
            {
                WebPlace place = _places[i];
                if (place.Shop.Seen)
                {
                    continue;
                }
                CloseBook(place, Save.Day);
                OnlineGame.Book(place, Save.Day - 1, out _, out _, true);   // its sales not yet in a statement go into the latest
                Notifications.Show(NotificationType.Warning, "reallife_online_note_gone", new Dictionary<string, string> { { "name", place.Shop.Name } }, 9f, null, null, true, false);
                Logger?.Info("Online shops: " + place.Shop.Name + " closed: its business is no longer the player's. Its day's book was paid in; its open orders are dropped.");
                _state.Shops.Remove(place.Shop);
                place.Shop = null;
                place.Reg = null;
                _places.RemoveAt(i);
                _pool.Add(place);
                gone++;
            }
            for (int i = 0; i < _places.Count; i++)
            {
                OnlineGame.GuessRoads(_places[i], _centres, _hasCentre);
            }
            _roads = _places.Count > 0;
            return gone;
        }

        // With keep (a stop in the middle of play), each site's day is paid in now and the state goes into the save's data.
        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            int paid = 0;
            if (keep && _running && Save != null && _state != null)
            {
                for (int i = 0; i < _places.Count; i++)
                {
                    paid += CloseBook(_places[i], Save.Day) ? 1 : 0;
                }
                Write(Save);
            }
            _running = false;
            _dayOpen = false;
            _roads = false;
            for (int i = 0; i < _places.Count; i++)
            {
                _places[i].Reg = null;
                _places[i].Shop = null;
                _pool.Add(_places[i]);
            }
            _places.Clear();
            _alerts.Clear();
            _alertMark = -1;
            _stockAt = null;
            if (!unloading)
            {
                RemoveApp();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Online check (stopped, " + why + "): " + paid + " site(s) had the day's book paid in; no stock and no figure of the game's is held, so nothing had to be put back: PASS");
            }
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
            PayInAfterFault();
            try
            {
                StandDown("a fault", false, false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
            }
            Logger?.Warn("Online shops: switched off for this session after an error in " + where
                         + ". The game's hour and day and the rest of the economy are not affected; open orders are kept as they were last saved.");
        }

        // The goods of orders already picked have left the building: their takings are paid in, each site on its own, before everything stops.
        private void PayInAfterFault()
        {
            for (int i = 0; _running && Save != null && _state != null && i < _places.Count; i++)
            {
                try
                {
                    CloseBook(_places[i], Save.Day);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new OnlineApp(this);
            bool tryAgain;
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_online_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(236, 150, 84, 255), new Color32(192, 84, 48, 255), 0.42f, "AppIcon-Online"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Online shops: the game's phone is not built as this version expects, so the Web shop app is left off. "
                                 + "Sites already open go on taking and sending orders; the Planner and the notifications still say what needs attention.");
                }
                return;
            }
            _view = view;
            Badge();
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

        // One slice of one measurement a second along the GPS's own graph, through a bridge; without it the guess stays.
        private void MeasureRoad()
        {
            for (int i = 0; i < _places.Count; i++)
            {
                WebPlace place = _places[i];
                Site site = place.Site;
                while (place.Measuring < _centres.Count && place.Measuring < site.Measured.Length && (site.Measured[place.Measuring] || !place.HasAt || !_hasCentre[place.Measuring]))
                {
                    place.Measuring++;
                }
                if (place.Measuring >= _centres.Count || place.Measuring >= site.Metres.Length)
                {
                    continue;
                }
                int metres = OnlineBridges.Road != null ? OnlineBridges.Road(place.At, _centres[place.Measuring]) : 0;
                if (metres < 0)
                {
                    return;   // still looking: the same one again next second
                }
                int guess = site.Metres[place.Measuring];
                if (metres > 0)
                {
                    site.Metres[place.Measuring] = metres;
                }
                site.Measured[place.Measuring] = true;
                if (Verbose)
                {
                    Logger?.Info("Online check (road, " + place.Shop.Name + " to " + _state.Districts[place.Measuring] + "): "
                                 + (metres > 0 ? metres + " m by road" : "no road found, the guess is kept") + ", " + guess + " m guessed from the straight line: PASS");
                }
                place.Measuring++;
                return;
            }
            _roads = false;
        }

        private void OnSave()
        {
            try
            {
                if (!_running || Save == null || _state == null)
                {
                    return;
                }
                Write(Save);
                if (Verbose)
                {
                    int orders = 0;
                    for (int i = 0; i < _state.Shops.Count; i++)
                    {
                        orders += _state.Shops[i].Orders.Count;
                    }
                    Logger?.Info("Online check (before a save): " + _state.Shops.Count + " site(s) and " + orders + " open order(s) written under their own key; nothing of the game's is held: PASS");
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
            save.modData[OnlineStore.Key] = OnlineStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[OnlineStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(OnlineStore.TestKey);
            }
        }

        /// <summary>A notification of the game's that opens the Web shop app on a tab, with something picked.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 11f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        public void Open(int tab, string pick)
        {
            if (_app != null && _running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>The app has opened: stock is counted, so the catalogue shows what stands in the building now. False while not running.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                for (int i = 0; i < _places.Count; i++)
                {
                    OnlineGame.CountStock(_places[i]);
                }
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>Something has changed: the Planner's rows and the badge are made again, and the app if it is open.</summary>
        internal void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            BuildAlerts(Save.Day);
            Badge();
            _view?.Changed();
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : _alerts.Count);
            }
        }

        // What the Planner's rows depend on, as one number: they are made again only when it changes.
        private int AlertMark()
        {
            int mark = 17;
            for (int i = 0; i < _places.Count; i++)
            {
                WebShop shop = _places[i].Shop;
                mark = mark * 31 + (shop.Idle >= OnlineCatalogue.IdleAlert ? 1 : 0) + (Falling(shop, Save.Day) ? 2 : 0) + (shop.Offline ? 4 : 0) + (shop.HostDue == Save.Day + 1 ? 8 : 0);
            }
            return mark;
        }

        private static bool Falling(WebShop shop, int day)
        {
            int before = shop.StarsBefore(day);
            return shop.Stars < OnlineCatalogue.PoorStars || (before > 0 && before - shop.Stars >= OnlineCatalogue.FallingBy);
        }

        private void BuildAlerts(int day)
        {
            _alerts.Clear();
            _alertMark = AlertMark();
            for (int i = 0; i < _places.Count; i++)
            {
                WebShop shop = _places[i].Shop;
                int id = OnlineRules.Key(shop.Place);
                if (shop.Idle >= OnlineCatalogue.IdleAlert)
                {
                    Alert(1, id, day, OnlineText.T("reallife_online_plan_idle_title", "name", shop.Name),
                        OnlineText.T("reallife_online_plan_idle_what", "count", shop.Count(OrderStage.Waiting).ToString()), null, OnlineText.T("reallife_online_plan_idle_missed"));
                }
                if (Falling(shop, day))
                {
                    int before = shop.StarsBefore(day);
                    DayBook week = _closed;
                    shop.WeekInto(week, day);
                    Alert(2, id, day, OnlineText.T("reallife_online_plan_stars_title", "name", shop.Name),
                        OnlineText.Fill("reallife_online_plan_stars_what", "stars", OnlineText.Stars(shop.Stars), "was", OnlineText.Stars(before > 0 ? before : shop.Stars),
                            "late", week.Late.ToString(), "gaps", (week.Short + week.Cancelled + week.Missing).ToString()), null, null);
                }
                if (shop.Offline)
                {
                    Alert(3, id, day, OnlineText.T("reallife_online_plan_offline_title", "name", shop.Name), OnlineText.T("reallife_online_plan_offline_what"),
                        OnlineText.Money(OnlineRules.HostingCost(shop.Quality, _index) * 100), null);
                }
                else if (shop.HostDue == day + 1)
                {
                    Alert(4, id, day + 1, OnlineText.T("reallife_online_plan_hosting_title", "name", shop.Name), OnlineText.T("reallife_online_plan_hosting_what"),
                        OnlineText.Money(OnlineRules.HostingCost(shop.Quality, _index) * 100), OnlineText.T("reallife_online_plan_hosting_missed"));
                }
            }
        }

        private void Alert(int a, int b, int day, string title, string what, string money, string ifMissed)
        {
            if (_alerts.Count < CompanyCatalogue.AlertsMost)
            {
                _alerts.Add(new OnlineAlert { A = a, B = b, Day = day, Title = title, What = what, Money = money, IfMissed = ifMissed });
            }
        }

        internal WebPlace PlaceAt(string place)
        {
            for (int i = 0; i < _places.Count; i++)
            {
                if (string.Equals(_places[i].Shop.Place, place, StringComparison.Ordinal))
                {
                    return _places[i];
                }
            }
            return null;
        }

        /// <summary>For the save tools: each site's day is paid in and the feature stops; the timer starts it again if the option is on.</summary>
        public void StopAll()
        {
            try
            {
                StandDown("the save tools asked", true, true, false);
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
                StandDown("the save's line was removed", false, false, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
            _state = null;
            _beforeTests = null;
        }
    }
}
