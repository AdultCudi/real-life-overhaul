using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The car dealership: a business type of the mod's own (DealerType) that the player founds in a retail building and
    /// runs with the game's own staff, hours and money. Its stock is real cars of the game's on the lot inside; it buys
    /// them new from the wholesaler, from private owners (an inspection first, if the player wants one, and a haggle) and
    /// at the trade auction against the other dealers of the room, and takes customers' cars in part exchange; it sells
    /// them to customers whose number comes from the game's own customer count for the building, looked after by the
    /// salespeople on shift, who make offers the staff close or the player answers; and it sells on its own finance and
    /// with warranties. See DealerCatalogue for what it makes and what bounds it, and the Dealership app (DealerApp).
    ///
    /// This class keeps it going. It has no Update and no timer of its own: once a second the credit side's timer calls
    /// <see cref="EachSecond"/> (the save is waited for, the option looked at, the app installed, the game's event for
    /// entering a building kept, and, while the player stands in one of the dealerships, whether its lot is laid out: one
    /// comparison), and the credit side's own handlers of the game's hour and day call <see cref="OnHour"/> and
    /// <see cref="OnDay"/> (EconomyDirector.AfterHour, AfterDay).
    ///
    ///   the hour   the market's morning on a new day, an hour of the trade auction's room, and for each dealership one
    ///              reading of the game (its hours, its building's customers and promotion, its staff on shift: one pass
    ///              over the staff) and DealerRules.Hour; then the staff take the customers who asked out on their test
    ///              drives and close what reaches the dealership's floor
    ///   the day    each finance plan whose week has come, each warranty's day, and the dealerships' books
    ///   entering   the dealership the player walks into has its lot laid out the first time and its cars held still on
    ///              their places (DealerGame.Park)
    ///   the save   the state is written under its own key; the cars themselves are the game's vehicles
    ///
    /// EVERY EFFECT AND HOW IT IS UNDONE
    ///   cars         the game's vehicles at the dealership's address. Switched off, they stay where they are, still the
    ///                dealership's (Dealerships.Stock), and trading resumes when it is switched on again
    ///   money        the game's own transactions at the dealership's address; true sales and purchases, and they stay
    ///
    /// A fault switches the feature off for the session with one line; the game's hour and day are left alone.
    /// </summary>
    public sealed partial class DealerDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeDealer";
        public const int TabStock = 0, TabMarket = 1, TabOffers = 2, TabFinance = 3, TabLots = 4;

        private const int CatchUp = 3;      // hours missed that are still worked through one by one
        private static readonly string[] AppGlyphs = { "Icon-Car", "Icon-Vehicle", "Icon-Shop" };

        public static IModLogger Logger;
        public static DealerDirector Instance { get; private set; }

        private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(DealerCatalogue.YardsMost);
        private readonly List<MarketModel> _models = new List<MarketModel>(32);
        private readonly List<string> _colours = new List<string>(16);
        private readonly List<int> _worths = new List<int>(DealerCatalogue.CarsMost);
        private readonly List<DealerListing> _down = new List<DealerListing>(DealerCatalogue.AuctionLots);
        private readonly List<StockCar> _lost = new List<StockCar>(4);
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);

        private DealerState _state;
        private string _beforeTests;
        private bool _running, _broken, _gaveUp;
        private int _waiting = -1;          // offers waiting for the player when last counted
        private int _toldHour = int.MinValue;   // the hour the player was last told of offers waiting
        private DealerApp _view;
        private PhoneApp _app;
        private Action _second, _onSave, _onHour, _onDay;
        private Action<Address> _onEnter, _enterSeen;
        private string _laidInside;         // the dealership whose lot was last seen to while the player stood in it

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public DealerState State => _running ? _state : null;

        public IReadOnlyList<MarketModel> Models => _models;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;
        private static int Now => Save.Day * 24 + Save.Hour;

        /// <summary>True for the id of a game vehicle that is a car of the stock. A set lookup; the set is kept while the feature stands down.</summary>
        public bool Holds(string vehicleId)
        {
            return vehicleId != null && _ids.Contains(vehicleId);
        }

        private void Awake()
        {
            Instance = this;
            DealerGame.Logger = Logger;
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            _onEnter = Guard.Wrap<Address>(OnEnter);
            EconomyDirector.Second += _second;
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
            DealerType.Dress();
        }

        private void OnDestroy()
        {
            EconomyDirector.Second -= _second;
            EconomyDirector.AfterHour -= _onHour;
            EconomyDirector.AfterDay -= _onDay;
            GlobalEvents.onEnterBuildingDelayed -= _onEnter;
            SaveHook.Drop(_onSave);
            if (Instance != this)
            {
                RemoveApp();
                return;
            }
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
            if (!Settings.Economy || !Settings.EconomyDealer || credit == null || !credit.Running)
            {
                if (_running)
                {
                    StandDown(!Settings.EconomyDealer ? "the option is off" : "the credit side is not running", true, true, false);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);
            EventHold.Keep(ref GlobalEvents.onEnterBuildingDelayed, _onEnter, ref _enterSeen);
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            Inside();
        }

        // While the player stands in one of the dealerships: its lot laid out once. One comparison a second otherwise.
        private void Inside()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || manager == null || manager.building == null)
            {
                _laidInside = null;
                return;
            }
            string place = DealerGame.Place(manager.building.Address);
            if (place == _laidInside)
            {
                return;
            }
            _laidInside = place;
            DealerYard y = _state.Yard(place);
            if (y == null)
            {
                ReadYards();
                y = _state.Yard(place);
            }
            if (y != null)
            {
                SeeTo(y);
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
            DealerState state = DealerStore.Load(save.modData, DealerStore.Key, out int version);
            bool fresh = state == null;
            if (fresh)
            {
                state = new DealerState { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day };
            }
            _beforeTests = save.modData.TryGetValue(DealerStore.TestKey, out string before) && !string.IsNullOrEmpty(before) ? before : null;
            _state = state;
            _running = true;
            _waiting = -1;
            _laidInside = null;
            DealerType.Dress();
            DealerGame.Models(_models, _colours);
            int gone = ReadYards(), lost = Reconcile();
            int now = Now;
            if (_state.LastHour < 0 || now - _state.LastHour > CatchUp || now < _state.LastHour)
            {
                _state.LastHour = now;   // hours that went by without the feature are not worked through
            }
            if (_state.MarketDay != save.Day)
            {
                DealerRules.Morning(_state, save.Day, _models, _colours, Prices.Level);
            }
            RebuildIds();
            Logger?.Info("Dealership: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + save.Day + ". " + _state.Yards.Count + " dealership(s), "
                         + CarCount() + " car(s) in stock, " + _models.Count + " kind(s) of car on the market, " + _state.Plans.Count + " finance plan(s), " + _state.Covers.Count + " warranty(ies)"
                         + (gone > 0 ? "; " + gone + " dealership(s) no longer the player's, whose cars stay where they stand" : string.Empty)
                         + (lost > 0 ? "; " + lost + " car(s) the save no longer has taken off the stock" : string.Empty) + ".");
            if (_models.Count == 0)
            {
                Logger?.Warn("Dealership: the game has no car this mod can sell; the market stays empty.");
            }
        }

        private void StandDown(string why, bool tell, bool keep, bool unloading)
        {
            bool was = _running;
            if (keep && _running && Save != null && _state != null)
            {
                Write(Save);
            }
            _running = false;
            _regs.Clear();
            _laidInside = null;
            if (!unloading)
            {
                RemoveApp();
            }
            if (was && (tell || Verbose))
            {
                Logger?.Info("Dealership check (stopped, " + why + "): the cars stay on their lots as the dealerships' stock, and nothing of the game's is held: PASS");
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
            try
            {
                StandDown("a fault", false, false, false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
            }
            Logger?.Warn("Dealership: switched off for this session after an error in " + where + ". The cars stay on their lots; the game's hour and day and the rest of the economy are not affected.");
        }

        private void Install()
        {
            var view = new DealerApp(this);
            GameSprites.Want(AppGlyphs);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_dealer_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(92, 160, 230, 255), new Color32(36, 84, 164, 255), 0.42f, "AppIcon-Dealer"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Dealership: the game's phone is not built as this version expects, so the Dealership app is left off. The dealerships go on trading; their staff close what reaches their floor.");
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

        // ---------------------------------------------------------------- the dealerships

        /// <summary>
        /// The state's dealerships made the player's own as they are now: one for each of their dealerships, a new one with
        /// the dealership's name, one whose business is no longer theirs dropped (its cars, if any are left, stay where
        /// they stand). Returns how many were dropped.
        /// </summary>
        private int ReadYards()
        {
            DealerGame.Dealerships(_regs);
            int gone = 0;
            for (int i = _state.Yards.Count - 1; i >= 0; i--)
            {
                DealerYard y = _state.Yards[i];
                bool there = false;
                for (int r = 0; r < _regs.Count && !there; r++)
                {
                    there = DealerGame.Place(_regs[r]) == y.Place;
                }
                if (!there)
                {
                    Logger?.Warn("Dealership: '" + y.Name + "' is no longer a dealership of the player's; its " + y.Cars.Count + " car(s) stay where they stand.");
                    _state.Yards.RemoveAt(i);
                    gone++;
                }
            }
            for (int r = 0; r < _regs.Count && _state.Yards.Count < DealerCatalogue.YardsMost; r++)
            {
                string place = DealerGame.Place(_regs[r]);
                DealerYard y = _state.Yard(place);
                if (y == null)
                {
                    y = new DealerYard { Place = place };
                    y.Today.Clear(Save.Day);
                    _state.Yards.Add(y);
                }
                y.Name = DealerRules.Clean(DealerGame.NameOf(_regs[r]));
            }
            if (gone > 0)
            {
                RebuildIds();
            }
            return gone;
        }

        // A car of the stock whose vehicle the save no longer has (deleted by something else) is taken off the stock.
        private int Reconcile()
        {
            int lost = 0;
            for (int y = 0; y < _state.Yards.Count; y++)
            {
                List<StockCar> cars = _state.Yards[y].Cars;
                for (int c = cars.Count - 1; c >= 0; c--)
                {
                    if (DealerGame.Vehicle(cars[c].VehicleId) == null)
                    {
                        RemoveDeals(_state.Yards[y], cars[c].Id);
                        cars.RemoveAt(c);
                        lost++;
                    }
                }
            }
            return lost;
        }

        internal void RebuildIds()
        {
            _ids.Clear();
            for (int y = 0; _state != null && y < _state.Yards.Count; y++)
            {
                List<StockCar> cars = _state.Yards[y].Cars;
                for (int c = 0; c < cars.Count; c++)
                {
                    if (!string.IsNullOrEmpty(cars[c].VehicleId))
                    {
                        _ids.Add(cars[c].VehicleId);
                    }
                }
            }
        }

        internal int CarCount()
        {
            int n = 0;
            for (int y = 0; _state != null && y < _state.Yards.Count; y++)
            {
                n += _state.Yards[y].Cars.Count;
            }
            return n;
        }

        internal BuildingRegistration RegOf(DealerYard y)
        {
            for (int r = 0; r < _regs.Count; r++)
            {
                if (_regs[r] != null && DealerGame.Place(_regs[r]) == y.Place)
                {
                    return _regs[r];
                }
            }
            return DealerGame.Registration(y.Place);
        }

        private static void RemoveDeals(DealerYard y, int car)
        {
            for (int d = y.Deals.Count - 1; d >= 0; d--)
            {
                if (y.Deals[d].Car == car)
                {
                    y.Deals.RemoveAt(d);
                }
            }
        }

        // ---------------------------------------------------------------- entering a dealership

        private static readonly int HotOnEnter = HotSpots.Slot("Economy: DealerDirector.OnEnter");
        private void OnEnter(Address address)
        {
            using var hotSpot = HotSpots.Time(HotOnEnter);
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                string place = DealerGame.Place(address);
                DealerYard y = _state.Yard(place);
                if (y == null)
                {
                    ReadYards();
                    y = _state.Yard(place);
                }
                if (y != null)
                {
                    _laidInside = place;
                    SeeTo(y);
                }
            }
            catch (Exception e)
            {
                Fault("entering a dealership", e);
            }
        }

        // The dealership the player stands in: its lot laid out if it has none, and its cars on their places, held still.
        private void SeeTo(DealerYard y)
        {
            bool laid = false;
            if (!y.Laid)
            {
                laid = DealerGame.Lay(y);
                if (laid)
                {
                    Logger?.Info("Dealership: the lot of '" + y.Name + "' is laid out with " + y.Spots.Count + " place(s).");
                    Tell(NotificationType.Info, "reallife_dealer_note_laid", new Dictionary<string, string>(2) { { "name", y.Name }, { "places", y.Spots.Count.ToString() } }, TabLots, LotRow(y));
                }
            }
            int placed = DealerGame.Park(y, _lost);
            for (int i = 0; i < _lost.Count; i++)
            {
                RemoveDeals(y, _lost[i].Id);
                y.Cars.Remove(_lost[i]);
            }
            if (_lost.Count > 0)
            {
                RebuildIds();
            }
            if (Verbose)
            {
                Logger?.Info("Dealership check (entered '" + y.Name + "'): " + (laid ? "the lot laid out, " : string.Empty) + y.Spots.Count + " place(s), " + placed + " of " + y.Cars.Count + " car(s) held on their places"
                             + (_lost.Count > 0 ? ", " + _lost.Count + " the save no longer has taken off" : string.Empty) + ": " + (placed == y.Cars.Count ? "PASS" : "FAIL") + ".");
            }
            Changed();
        }

        internal static string LotRow(DealerYard y) => "lot:" + y.Place;

        // ---------------------------------------------------------------- the hour

        private void OnHour()
        {
            if (!_running || _broken || Save == null)
            {
                return;
            }
            try
            {
                int now = Now;
                if (now <= _state.LastHour)
                {
                    return;
                }
                float started = Time.realtimeSinceStartup;
                if (now - _state.LastHour > CatchUp)
                {
                    _state.LastHour = now - 1;   // (a gap longer than the catch-up: only this hour is worked)
                }
                ReadYards();
                int offers = 0, closed = 0, drives = 0;
                for (int hour = _state.LastHour + 1; hour <= now; hour++)
                {
                    StepHour(hour, ref offers, ref closed, ref drives);
                }
                _state.LastHour = now;
                Changed();
                if (Verbose && (offers > 0 || closed > 0 || drives > 0))
                {
                    Logger?.Info("Dealership check (hour " + Save.Hour + " of day " + Save.Day + "): " + _state.Yards.Count + " dealership(s), " + offers + " offer(s) made, " + drives + " test drive(s), "
                                 + closed + " deal(s) closed by the staff; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00") + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the hour", e);
            }
        }

        private void StepHour(int now, ref int offers, ref int closed, ref int drives)
        {
            int day = now / 24, level = Prices.Level;
            if (_state.MarketDay != day)
            {
                DealerRules.Morning(_state, day, _models, _colours, level);
                for (int y = 0; y < _state.Yards.Count; y++)
                {
                    DealerRules.CloseDay(_state.Yards[y], day);
                }
            }
            Auction(now, level);
            int financeLeast = Prices.At(DealerCatalogue.FinanceLeast);
            for (int i = 0; i < _state.Yards.Count; i++)
            {
                DealerYard y = _state.Yards[i];
                BuildingRegistration reg = RegOf(y);
                if (reg == null)
                {
                    continue;
                }
                YardHour h = DealerGame.Hour(reg);
                Worths(y, level);
                offers += DealerRules.Hour(_state, y, h, now, day, _worths, financeLeast, _models, _colours, level).Offers;
                if (h.Staff <= 0 || !h.Open)
                {
                    continue;
                }
                // The staff on shift: the customers who asked go out on their test drives, and what reaches the floor is closed.
                for (int d = y.Deals.Count - 1; d >= 0 && d < y.Deals.Count; d--)
                {
                    DealerOffer deal = y.Deals[d];
                    StockCar car = y.Car(deal.Car);
                    if (car == null || deal.Stage != OfferStage.Open)
                    {
                        continue;
                    }
                    if (deal.WantsDrive && !deal.Drove)
                    {
                        DealerRules.TestDrive(deal, car, now, _state.Seed);
                        DealerGame.Wear(car);
                        drives++;
                        continue;
                    }
                    if (DealerRules.StaffClose(deal, car, y.StaffFloor) && Close(y, reg, deal, deal.Offer, true))
                    {
                        closed++;
                    }
                }
            }
        }

        // The worths of a lot's cars, in the order of its cars.
        internal void Worths(DealerYard y, int level)
        {
            _worths.Clear();
            for (int c = 0; c < y.Cars.Count; c++)
            {
                _worths.Add(Worth(y.Cars[c], level));
            }
        }

        internal static int Worth(StockCar car, int level)
        {
            return DealerRules.Worth(DealerGame.ListPrice(car.Type), level, car.Km, car.Condition);
        }

        internal int Worth(DealerListing l)
        {
            return DealerRules.WorthOf(l, _models, Prices.Level);
        }

        // ---------------------------------------------------------------- the day

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
                ReadYards();
                int paid = 0, missed = 0, defaulted = 0, finished = 0, claims = 0, claimed = 0;
                int economy = EconomyFactor();
                for (int i = _state.Plans.Count - 1; i >= 0; i--)
                {
                    DealerPlan p = _state.Plans[i];
                    while (p.Due <= day && i < _state.Plans.Count && _state.Plans[i] == p)
                    {
                        PlanWeek w = DealerRules.Week(p, _state.Seed, economy, out int got);
                        DealerYard y = _state.Yard(p.Place);
                        BuildingRegistration reg = y != null ? RegOf(y) : null;
                        if (got > 0)
                        {
                            DealerGame.Give(got, DealerLines.FinanceKey, DealerGame.Named(reg), reg);
                            paid += got;
                            Book(y, got, 0, 0, 0);
                        }
                        if (w == PlanWeek.Missed)
                        {
                            missed++;
                        }
                        else if (w == PlanWeek.Defaulted)
                        {
                            defaulted++;
                            int back = DealerRules.Recovery(p.Worth);
                            DealerGame.Give(back, DealerLines.RecoveryKey, DealerGame.Named(reg), reg);
                            Book(y, back, 0, 0, 0);
                            Tell(NotificationType.Warning, "reallife_dealer_note_default", new Dictionary<string, string>(3)
                                { { "customer", p.Customer }, { "car", DealerGame.CarName(p.Type) }, { "amount", DealerText.Money(back) } }, TabFinance, null);
                            Logger?.Info("Dealership: " + p.Customer + " stopped paying for a " + p.Type + " with " + p.Owed + " still owed; the car was taken back and sold at the trade auction for " + back + ".");
                            _state.Plans.RemoveAt(i);
                        }
                        else if (w == PlanWeek.PaidOff)
                        {
                            finished++;
                            _state.Plans.RemoveAt(i);
                        }
                    }
                }
                int floor = Prices.At(DealerCatalogue.ClaimFloor), ceiling = Prices.At(DealerCatalogue.ClaimCeiling);
                for (int i = _state.Covers.Count - 1; i >= 0; i--)
                {
                    DealerCover c = _state.Covers[i];
                    if (day > c.Until || c.Claims >= DealerCatalogue.ClaimsMost)
                    {
                        _state.Covers.RemoveAt(i);
                        continue;
                    }
                    int cost = DealerRules.Claim(c, day, _state.Seed, floor, ceiling);
                    if (cost <= 0)
                    {
                        continue;
                    }
                    DealerYard y = _state.Yard(c.Place);
                    BuildingRegistration reg = y != null ? RegOf(y) : null;
                    DealerGame.Take(cost, DealerLines.ClaimKey, DealerGame.Named(reg), reg, true);
                    Book(y, 0, 0, cost, 0);
                    claims++;
                    claimed += cost;
                }
                Changed();
                if (Verbose)
                {
                    Logger?.Info("Dealership check (day " + day + "): " + _state.Plans.Count + " finance plan(s): " + paid + " paid in, " + missed + " week(s) missed, " + defaulted + " car(s) taken back, " + finished
                                 + " paid off; " + _state.Covers.Count + " warranty(ies): " + claims + " claim(s) of " + claimed + "; the economy at " + economy + " thousandths of the usual misses; "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.00") + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                Fault("the day", e);
            }
        }

        // How the city's economy moves the instalments missed (thousandths of the usual): more in a slowdown or a recession.
        private static int EconomyFactor()
        {
            return DealerCatalogue.CycleMisses[DealerCatalogue.Clamp((int)CityEconomy.State, 0, DealerCatalogue.CycleMisses.Length - 1)];
        }

        /// <summary>A dealership's day book: money in, money spent on stock, claims paid, margin made.</summary>
        internal void Book(DealerYard y, int takings, int bought, int claims, int margin)
        {
            if (y == null || Save == null)
            {
                return;
            }
            if (y.Today.Day != Save.Day)
            {
                DealerRules.CloseDay(y, Save.Day);
            }
            y.Today.Takings += takings;
            y.Today.Bought += bought;
            y.Today.Claims += claims;
            y.Today.Margin += margin;
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
                    Logger?.Info("Dealership check (before a save): " + _state.Yards.Count + " dealership(s), " + CarCount() + " car(s), " + _state.Plans.Count + " plan(s) and " + _state.Covers.Count
                                 + " warranty(ies) written under their own key; the cars are the game's own vehicles: PASS");
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
            save.modData[DealerStore.Key] = DealerStore.Write(_state);
            if (_beforeTests != null)
            {
                save.modData[DealerStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(DealerStore.TestKey);
            }
        }

        // ---------------------------------------------------------------- what the player is told

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

        /// <summary>The app has been opened: the dealerships are read again, so what it shows is the game's as it is now.</summary>
        public bool AppOpened()
        {
            if (!_running || _broken || Save == null)
            {
                return false;
            }
            try
            {
                ReadYards();
                Badge();
                return true;
            }
            catch (Exception e)
            {
                Fault("opening the app", e);
                return false;
            }
        }

        /// <summary>Something has changed: the badge and the app if it is open; and a word when more offers wait for the player.</summary>
        internal void Changed()
        {
            if (!_running || Save == null)
            {
                return;
            }
            int waiting = Waiting();
            if (_waiting >= 0 && waiting > _waiting && Now - _toldHour >= DealerCatalogue.TellEvery)
            {
                _toldHour = Now;
                Tell(NotificationType.Info, "reallife_dealer_note_offers", new Dictionary<string, string>(1) { { "count", waiting.ToString() } }, TabOffers, null);
            }
            _waiting = waiting;
            Badge();
            _view?.Changed();
        }

        /// <summary>Offers that wait for the player: under the floor the staff close at, or with nobody on shift to close them.</summary>
        internal int Waiting()
        {
            int n = 0;
            for (int y = 0; _state != null && y < _state.Yards.Count; y++)
            {
                n += _state.Yards[y].Deals.Count;
            }
            return n;
        }

        private void Badge()
        {
            if (_app != null)
            {
                _app.SetBadge(_app.Showing ? 0 : Waiting());
            }
        }

        // ---------------------------------------------------------------- for the save tools

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
            _ids.Clear();
        }
    }
}
