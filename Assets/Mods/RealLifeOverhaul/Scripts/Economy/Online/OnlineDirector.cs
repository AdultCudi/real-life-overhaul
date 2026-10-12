using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One thing the Planner should say about the online shops: built when something it depends on changes, never per frame.</summary>
    public struct OnlineAlert
    {
        public int A, B;        // what it is and which site: the Planner's id for the row
        public int Day;
        public string Title, What, Money, IfMissed;
    }

    /// <summary>
    /// Online shops: a business or a warehouse of the player's sells what it stocks to the whole city. Orders come in
    /// by rule each game hour, are picked from the real stock by the staff on shift, and are delivered by the
    /// player's own vans or by a courier; lateness and gaps cost refunds and a rating that feeds the orders; a share
    /// comes back. See OnlineCatalogue for what it adds and what bounds it.
    ///
    /// THE GAME'S OWN PATTERN, AND HOW THIS FOLLOWS IT. The game has two things called food delivery. One brings food
    /// the player ordered by phone to a building of theirs (FoodDeliveryHelper): a contract with an hour, looked at on
    /// each game hour (RunHourly), paid as one transaction when its hour has come, and the goods simply put in the
    /// building's delivery spot; no vehicle drives anywhere. The other is a job the player does on foot
    /// (FoodDeliveryJobHelper): offers made each hour at shops' tills, a mission with a time limit. The game's own
    /// logistics are of the first kind too (LogisticsManagerPlanDestination.Deliver, at 02:00): stock is taken out of
    /// the warehouse's pallets and put in the shop, a line goes in the warehouse's delivery record, and a van and a
    /// driver are only a count of destinations a day. So here: orders are records with an hour, worked through on the
    /// game's hour; delivery is time and money and no vehicle is put on the road; stock moves by the calls the game's
    /// logistics and its customers use; a van is its driver's hours and its cargo space. What differs: the orders go
    /// out of the player's business instead of into it, and they are paid in once a day as the game pays a shop's
    /// takings in (BusinessHelper.ProcessDailyOrders), not one by one.
    ///
    /// This class keeps it going. It has no Update, no timer and no place of its own in the game's events: once a
    /// second the credit side's timer calls <see cref="EachSecond"/> (the save is waited for, the option is looked
    /// at, and one slice of one road measurement is done until every site's distances are measured: one comparison
    /// a second after that), and the credit side's own handlers of the game's hour and day events call
    /// <see cref="OnHour"/> and <see cref="OnDay"/> when their own work is done (EconomyDirector.AfterHour, AfterDay).
    /// It is added before the company's director (Core/RealLifeOverhaulMod), so that its day comes first and the
    /// company's quarter sees the online sales in the game's statements.
    ///
    ///   the hour   every site: the staff on shift and the vans are read (one pass over today's shifts of the place
    ///              and over the warehouses' slots), then OnlineRules.Hour: a handful of passes over at most 80 open
    ///              orders, with stock taken out for each order picked (one pass over what stands in the building)
    ///   the day    on the hour that opens a day every site's book is closed and paid in as dated transactions; on
    ///              the day event, after the game has made its statements, yesterday's online sales are entered in
    ///              them, hosting is taken when it falls due, and each site's day is worked out from the game's
    ///              demand. The pass says how many milliseconds it took with "Developer diagnostics" on.
    ///   the save   the state is written under its own key. Nothing of the game's is held that a save would keep.
    ///
    /// EVERY EFFECT AND HOW IT IS UNDONE
    ///   stock        leaves the real building when an order is picked and is really gone: it was sold. Nothing is
    ///                held or reserved, so there is nothing to give back when the feature stops
    ///   money        transactions of the mod's own, dated, each with its own key and label. When the feature is
    ///                switched off in the middle of a day, the day's book is paid in at once
    ///   statements   a day's online sales are added to the game's own statement of the business; they are true
    ///                sales and stay
    ///
    /// A fault switches the feature off for the session with one line; the game's hour and day and the rest of the
    /// economy are left alone.
    /// </summary>
    public sealed partial class OnlineDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeOnline";
        public const int TabShops = 0, TabOrders = 1, TabCatalogue = 2;

        private const int CatchUp = 3;      // hours missed that are still worked through one by one; after more, nobody is blamed for the gap

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
        private string _beforeTests;    // the state's line as it was before the first test tool changed it; null with no test change in place
        private bool _running;
        private bool _broken;           // a fault: off for the session
        private bool _gaveUp;           // the game's phone is not built as expected: the app is left off, the rest runs
        private bool _roads;            // some site's distances are still to be measured along the roads
        private bool _dayOpen;          // the hour that opened a day has closed the books; its own orders wait for the day event
        private int _index = CompanyCatalogue.IndexStart;
        private int _alertMark = -1;    // what the Planner's rows were last made from
        private WebPlace _stockAt;      // the place the two stock calls below work on
        private StockTake _take;
        private StockBack _put;
        private OnlineApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public OnlineState State => _running ? _state : null;

        /// <summary>The online shops with what was read from the game for each, in the state's order.</summary>
        public IReadOnlyList<WebPlace> Places => _places;

        public IReadOnlyList<OnlineAlert> Alerts => _alerts;

        /// <summary>The price index as the company counts it: tenths of a point, 1000 at the start.</summary>
        public int Index => _index;

        public int MonthDays => MacroCatalogue.MonthDays(OnlineGame.DaysPerYear);

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;
        private static int Now => Save.Day * 24 + Save.Hour;

        private void Awake()
        {
            Instance = this;
            OnlineGame.Logger = Logger;
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            _take = TakeStock;
            _put = PutStock;
            EconomyDirector.Second += _second;
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
        }

        private void OnDestroy()
        {
            EconomyDirector.Second -= _second;
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;   // another city's director has started since: the state is its
            }
            // The city is unloading (or the mod is being switched off). Nothing is written to the save here: what a save
            // keeps is written when the game saves.
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

        // ---------------------------------------------------------------- once a second (the credit side's timer)

        private void EachSecond()
        {
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

        // ---------------------------------------------------------------- starting and stopping

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
            _index = Prices.Level;
            OnlineGame.Districts(_state, _centres, _hasCentre);
            int gone = ReadPlaces();
            int missed = _state.LastHour < 0 ? 0 : now - _state.LastHour;
            if (missed > CatchUp || missed < 0)
            {
                // Hours went by without the feature (the option was off, or the save was played without the mod).
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
                    CloseBook(_places[i], today);   // (a book left open by a day that ended without the feature)
                }
            }
            PlanAll(today);
            BuildAlerts(today);
            Logger?.Info("Online shops: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + today + ". " + _places.Count + " site(s)"
                         + (gone > 0 ? ", " + gone + " closed because the business is no longer the player's" : string.Empty) + ", " + _state.Districts.Count + " district(s), "
                         + _state.Vans.Count + " van(s) known" + (missed > CatchUp ? "; " + missed + " hour(s) passed without the feature and nobody is blamed for them" : string.Empty) + ".");
        }

        // Each site is tied to its building again; a site whose business is no longer the player's is closed, its day's book paid in first.
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

        // The feature stops. With 'keep' (a stop in the middle of play) each site's day is paid in now, so that no
        // money the player has earned is left waiting, and the state goes into the save's data.
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

        /// <summary>A fault: one line, and nothing more of the feature this session.</summary>
        private void Fault(string where, Exception e)
        {
            _broken = true;
            Logger?.Error(e);
            try
            {
                StandDown("a fault", false, false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
            }
            Logger?.Warn("Online shops: switched off for this session after an error in " + where + ". The game's hour and day and the rest of the economy are not affected; open orders are kept as they were last saved.");
        }

        private void Install()
        {
            var view = new OnlineApp(this);
            GameSprites.Want(AppGlyphs);
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
                    Logger?.Warn("Online shops: the game's phone is not built as this version expects, so the Web shop app is left off. Sites already open go on taking and sending orders; the Planner and the notifications still say what needs attention.");
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

        // ---------------------------------------------------------------- roads

        // One slice of one measurement a second, until every site has the road to every district. The roads are the
        // GPS's own graph, asked through a bridge; without it the straight line's guess stays.
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
                    Logger?.Info("Online check (road, " + place.Shop.Name + " to " + _state.Districts[place.Measuring] + "): " + (metres > 0 ? metres + " m by road" : "no road found, the guess is kept")
                                 + ", " + guess + " m guessed from the straight line: PASS");
                }
                place.Measuring++;
                return;
            }
            _roads = false;
        }

        // ---------------------------------------------------------------- the hour

        // The game's hour event (every game hour, and first thing in its NewDay, before its daily run), after the credit side's work on it.
        private void OnHour()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int now = Now, day = Save.Day;
                if (now <= _state.LastHour)
                {
                    return;
                }
                bool opens = false;
                for (int i = 0; i < _places.Count; i++)
                {
                    if (_places[i].Shop.Today.Day < day)
                    {
                        // The hour that opens a day: yesterday's book is closed and paid in, dated yesterday, before
                        // the game's daily run. The hour's own orders wait for the day event, which enters yesterday's
                        // sales in the game's statement first.
                        CloseBook(_places[i], day);
                        opens = true;
                    }
                }
                if (opens && Save.Hour == 0)
                {
                    _dayOpen = true;
                    return;
                }
                StepTo(now);
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        // Every hour not yet worked through, up to now. After a longer gap nobody is blamed for it.
        private void StepTo(int now)
        {
            _dayOpen = false;
            if (_places.Count == 0)
            {
                _state.LastHour = now;
                return;
            }
            if (now - _state.LastHour > CatchUp)
            {
                for (int i = 0; i < _places.Count; i++)
                {
                    OnlineRules.Resume(_state, _places[i].Shop);
                }
                _state.LastHour = now - 1;
            }
            float started = Time.realtimeSinceStartup;
            int came = 0, picked = 0, cancelled = 0, sent = 0, arrived = 0, late = 0, returned = 0, hours = 0;
            for (int hour = _state.LastHour + 1; hour <= now; hour++, hours++)
            {
                OnlineGame.ReadVans(_state, hour / 24);
                for (int i = 0; i < _places.Count; i++)
                {
                    WebPlace place = _places[i];
                    place.Pickers = OnlineGame.Pickers(place, _state, hour);
                    _stockAt = place;
                    int idle = place.Shop.Idle;
                    HourCount count = OnlineRules.Hour(_state, place.Shop, place.Site, hour, place.Pickers, _take, _put);
                    OnlineGame.Picked(place.Reg);
                    came += count.Came;
                    picked += count.Picked;
                    cancelled += count.Cancelled + count.Missing;
                    sent += count.Sent;
                    arrived += count.Arrived;
                    late += count.Late;
                    returned += count.Returned;
                    if (idle < OnlineCatalogue.IdleAlert && place.Shop.Idle >= OnlineCatalogue.IdleAlert)
                    {
                        Tell(NotificationType.Warning, "reallife_online_note_idle", new Dictionary<string, string>
                        {
                            { "name", place.Shop.Name }, { "count", place.Shop.Count(OrderStage.Waiting).ToString() }
                        }, TabOrders, null);
                    }
                }
                _state.LastHour = hour;
            }
            _stockAt = null;
            int mark = AlertMark();
            if (mark != _alertMark || came + picked + cancelled + sent + arrived + returned > 0)
            {
                Changed();
            }
            if (Verbose && came + picked + cancelled + sent + arrived + returned > 0)
            {
                Logger?.Info("Online check (hour " + (now % 24) + " of day " + (now / 24) + ", " + hours + " hour(s) worked through): " + _places.Count + " site(s), " + came + " order(s) came, " + picked + " picked from real stock, "
                             + cancelled + " cancelled or given up, " + sent + " sent, " + arrived + " at the door of which " + late + " late, " + returned + " returned; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00") + " ms: PASS");
            }
        }

        // The two calls the rules reach the real stock through, at the place the hour is working on.
        private int TakeStock(string item, int wanted, out int cost)
        {
            cost = 0;
            return _stockAt != null ? OnlineGame.Take(_stockAt.Reg, item, wanted, out cost) : 0;
        }

        private int PutStock(string item, int units, int unitCost)
        {
            return _stockAt != null ? OnlineGame.PutBack(_stockAt.Reg, item, units, unitCost) : 0;
        }

        // ---------------------------------------------------------------- the day

        /// <summary>
        /// A site's book is closed and paid in: takings as one transaction, refunds as one, the delivery cost as one,
        /// each dated the book's day and at the business. The cash is read before and after as the check that what
        /// was paid in is takings less refunds and delivery (hosting was taken when it fell due). True if any money moved.
        /// </summary>
        private bool CloseBook(WebPlace place, int day)
        {
            WebShop shop = place.Shop;
            int courier = shop.Way == OnlineCatalogue.Courier ? 1 : 0;
            OnlineRules.CloseDay(shop, day, _closed);
            DayBook b = _closed;
            if (b.Takings == 0 && b.Refunds == 0 && b.Delivery == 0)
            {
                return false;
            }
            int? dated = b.Day > 0 && b.Day < Save.Day ? (int?)Math.Max(b.Day, Save.Day - 1) : null;   // (yesterday at the earliest: the game's own daily entries are dated so)
            float before = Save.Money;
            OnlineGame.Give(b.Takings, OnlineGame.TakingsKey, place, dated);
            OnlineGame.Take(b.Refunds, OnlineGame.RefundsKey, place, dated, false, true);
            OnlineGame.Take(b.Delivery, courier == 1 ? OnlineGame.CourierKey : OnlineGame.VansKey, place, dated, true, true);
            if (Verbose)
            {
                float moved = Save.Money - before, wanted = (b.Takings - b.Refunds - b.Delivery) / 100f;
                Logger?.Info("Online check (the book of day " + b.Day + ", " + shop.Name + "): " + b.Orders + " order(s) came, " + b.Picked + " picked, takings " + Cents(b.Takings) + ", cost of goods " + Cents(b.Goods)
                             + ", refunds " + Cents(b.Refunds) + ", delivery " + Cents(b.Delivery) + ", hosting " + Cents(b.Hosting) + " (taken when due); the game's cash moved by " + moved.ToString("0.00")
                             + " against " + wanted.ToString("0.00") + " wanted: " + (Mathf.Abs(moved - wanted) <= 0.02f + (Mathf.Abs(before) + Mathf.Abs(wanted)) * 0.000004f ? "PASS" : "FAIL, the cash did not move by the book's sum"));   // (the game keeps cash in a float: a few millionths of it is as near as two readings can be)
            }
            return true;
        }

        private static string Cents(int cents)
        {
            return (cents / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        // The game's day event (after its daily run), after the credit side has settled its day.
        private void OnDay()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                float started = Time.realtimeSinceStartup;
                int day = Save.Day;
                _index = Prices.Level;
                int gone = ReadPlaces(), booked = 0, waiting = 0, sales = 0, goods = 0, pruned = 0;
                for (int i = 0; i < _places.Count; i++)
                {
                    WebPlace place = _places[i];
                    if (place.Shop.Today.Day < day)
                    {
                        CloseBook(place, day);   // (a day event that no hour event came before)
                    }
                    int s, g;
                    if (OnlineGame.Book(place, day - 1, out s, out g))
                    {
                        booked += s != 0 || g != 0 ? 1 : 0;
                        sales += s;
                        goods += g;
                    }
                    else
                    {
                        waiting++;
                    }
                    Host(place, day);
                    pruned += OnlineRules.Prune(place.Shop);
                }
                float entered = Time.realtimeSinceStartup;
                PlanAll(day);
                if (_dayOpen || Now > _state.LastHour)
                {
                    StepTo(Now);   // (the orders of the hour that opened the day)
                }
                Changed();
                if (Verbose)
                {
                    float done = Time.realtimeSinceStartup;
                    Logger?.Info("Online check (day " + day + "): " + _places.Count + " site(s)" + (gone > 0 ? ", " + gone + " closed with its business" : string.Empty) + "; " + booked + " site(s) had yesterday's sales of "
                                 + Cents(sales) + " and goods of " + Cents(goods) + " entered in the game's statement for day " + (day - 1)
                                 + (waiting > 0 ? ", " + waiting + " wait for a statement the game has not made" : string.Empty) + (pruned > 0 ? ", " + pruned + " product(s) no longer offered taken out" : string.Empty)
                                 + "; prices " + (_index / 10f).ToString("0.0") + ", the economy " + CityEconomy.State + "; the books " + ((entered - started) * 1000f).ToString("0.0") + " ms, demand and the first hour "
                                 + ((done - entered) * 1000f).ToString("0.0") + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // A month's hosting, when it falls due: taken if the cash covers it. If not, the site is down until it is paid, and is asked again each day.
        private void Host(WebPlace place, int day)
        {
            WebShop shop = place.Shop;
            if (!OnlineRules.HostingDue(shop, day))
            {
                return;
            }
            int cost = OnlineRules.HostingCost(shop.Quality, _index);
            if (OnlineGame.Take(cost * 100, OnlineGame.HostingKey, place, null, true, false))
            {
                bool was = shop.Offline;
                OnlineRules.HostingPaid(shop, day, cost * 100, MonthDays);
                if (was)
                {
                    Tell(NotificationType.Success, "reallife_online_note_back", new Dictionary<string, string> { { "name", shop.Name } }, TabShops, "shop:" + shop.Place);
                }
                if (Verbose)
                {
                    Logger?.Info("Online check (hosting, " + shop.Name + "): " + cost + " taken for a month at today's prices (" + _index + "), deductible; due again on day " + shop.HostDue + ": PASS");
                }
                return;
            }
            if (!shop.Offline)
            {
                OnlineRules.HostingMissed(shop);
                Tell(NotificationType.Warning, "reallife_online_note_unpaid", new Dictionary<string, string> { { "name", shop.Name }, { "amount", OnlineText.Money(cost * 100) } }, TabShops, "shop:" + shop.Place);
                if (Verbose)
                {
                    Logger?.Info("Online check (hosting, " + shop.Name + "): " + cost + " could not be taken: the cash does not cover it. The site takes no orders until it is paid: PASS");
                }
            }
        }

        // Each site's day worked out again: prices, the going prices and the game's demand are read, and the rule gives the orders a day.
        // (Said in the log one site at a time only when the player has changed something: Replan.)
        private void PlanAll(int day)
        {
            int cycle = (int)CityEconomy.State, season = OnlineBridges.Season != null ? OnlineBridges.Season(day) : 1;
            for (int i = 0; i < _places.Count; i++)
            {
                Plan(_places[i], day, cycle, season, false);
            }
        }

        private void Plan(WebPlace place, int day, int cycle, int season, bool say)
        {
            OnlineGame.ReadSite(place, _state);
            OnlineGame.GuessRoads(place, _centres, _hasCentre);
            int campaign = OnlineBridges.Campaign != null && place.Reg != null ? OnlineBridges.Campaign(place.Reg) : 0;
            OnlineRules.Plan(place.Shop, place.Site, day, campaign, cycle, season, _state.Shops.Count);
            if (Verbose && say)
            {
                Site s = place.Site;
                Logger?.Info("Online check (demand, " + place.Shop.Name + "): " + (s.PerDay / 1000f).ToString("0.0") + " order(s) a day of at most "
                             + OnlineCatalogue.OrdersMost[OnlineCatalogue.Quality(place.Shop.Quality)] + ": quality " + place.Shop.Quality + (place.Shop.InHouse ? " built in-house" : string.Empty) + ", " + s.Lines
                             + " product(s), the game's demand " + s.Need + " of 100, prices " + s.Price + ", promise " + place.Shop.Promise + ", stars " + s.Stars + ", advertising " + s.Ads + "%, a campaign at the business "
                             + s.Campaign + "%, the economy " + s.Cycle + ", season " + s.Season + ", " + s.Crowd + " site(s) sharing the city (factors in thousandths)" + (place.Shop.Offline ? "; offline, hosting unpaid" : string.Empty) + ": PASS");
            }
        }

        /// <summary>One site's day worked out again now: the player has just changed something of it.</summary>
        internal void Replan(WebPlace place)
        {
            if (place != null && Save != null)
            {
                Plan(place, Save.Day, (int)CityEconomy.State, OnlineBridges.Season != null ? OnlineBridges.Season(Save.Day) : 1, true);
            }
        }

        // ---------------------------------------------------------------- the save

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

        // ---------------------------------------------------------------- what the player is told

        /// <summary>A notification of the game's that opens the Web shop app on a tab, with something picked.</summary>
        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 11f, null, () => Open(tab, pick), true, false);
        }

        public void Open(int tab, string pick)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>The app has been opened: stock is counted, so that what the catalogue shows is what stands in the building now.</summary>
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

        /// <summary>Something has changed: the Planner's rows and the app's badge are made again, and the app if it is open.</summary>
        internal void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            BuildAlerts(Save.Day);
            Badge();
            if (_view != null)
            {
                _view.Changed();
            }
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : _alerts.Count);
            }
        }

        // ---------------------------------------------------------------- the Planner's rows

        // What the rows depend on, as one number: they are made again only when it changes.
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
                        OnlineText.Fill("reallife_online_plan_stars_what", "stars", OnlineText.Stars(shop.Stars), "was", OnlineText.Stars(before > 0 ? before : shop.Stars), "late", week.Late.ToString(),
                            "gaps", (week.Short + week.Cancelled + week.Missing).ToString()), null, null);
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

        // ---------------------------------------------------------------- for the app

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

        // ---------------------------------------------------------------- for the save tools

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
