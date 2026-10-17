using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The Car Showroom, a business type of the mod's own (<see cref="DealerType"/>) run with the game's staff, hours and money,
    /// its stock real cars on the lot inside. This owns its state. Nothing per frame: the credit side's timer calls
    /// <see cref="EachSecond"/> and its hour and day handlers call OnHour and OnDay. A fault switches it off for the session.
    /// </summary>
    public sealed partial class DealerDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeDealer";
        public const int TabStock = 0, TabMarket = 1, TabOffers = 2, TabFinance = 3, TabLots = 4, TabGrowth = 5, TabReports = 6;

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
        private string _laidInside;         // the showroom whose lot was last seen to while the player stood in it

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
            _second = EachSecond;
            _onSave = OnSave;
            _onHour = OnHour;
            _onDay = OnDay;
            _onEnter = Guard.Wrap<Address>(OnEnter);
            EconomyDirector.Follow(StartSeats.Dealer, _second);   // its first second after a load has a frame of its own (Core/StartLine)
            EconomyDirector.AfterHour += _onHour;
            EconomyDirector.AfterDay += _onDay;
            DealerType.Dress();
        }

        private void OnDestroy()
        {
            EconomyDirector.Unfollow(_second);
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

        private static readonly int HotSecond = HotSpots.Slot("Economy: DealerDirector.Second");
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

        // While the player stands in one of the showrooms: its lot laid out once. One comparison a second otherwise.
        private void Inside()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (!BuildingManager.IsInsideBuilding || manager == null || manager.building == null)
            {
                _laidInside = null;
                TellFloor(null);
                return;
            }
            string place = DealerGame.Place(manager.building.Address);
            if (place == _laidInside)
            {
                TellFloor(_state.Yard(place));
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
            TellFloor(y);
        }

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
            ReadBays(save);
            _heard.Clear();
            _running = true;
            _waiting = -1;
            _laidInside = null;
            DealerType.Dress();
            DealerGame.Models(_models, _colours);
            int gone = ReadYards(), lost = Reconcile();
            int now = Now;
            bool gap = _state.LastHour < 0 || now - _state.LastHour > CatchUp || now < _state.LastHour;
            if (gap)
            {
                _state.LastHour = now;   // hours that went by without the feature are not worked through
            }
            if (_state.MarketDay != save.Day)
            {
                DealerRules.Morning(_state, save.Day, _models, _colours, Prices.Level);
                for (int y = 0; gap && y < _state.Yards.Count; y++)
                {
                    DealerRules.CloseDay(_state, _state.Yards[y], save.Day);   // the last day worked goes into the reports (hours worked through close their own)
                }
            }
            RebuildIds();
            Logger?.Info("Dealership: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + save.Day + ". "
                         + _state.Yards.Count + " dealership(s), " + CarCount() + " car(s) in stock, " + _state.Deliveries.Count + " on their way, "
                         + _models.Count + " kind(s) of car on the market, " + _state.Plans.Count + " finance plan(s), " + _state.Covers.Count + " warranty(ies)"
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
            TellFloor(null);
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
            Logger?.Warn("Dealership: switched off for this session after an error in " + where
                         + ". The cars stay on their lots; the game's hour and day and the rest of the economy are not affected.");
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
                    Logger?.Warn("Dealership: the game's phone is not built as this version expects, so the Dealership app is left off. "
                                 + "The dealerships go on trading; their staff close what reaches their floor.");
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

        /// <summary>
        /// The showrooms brought in line with the player's dealerships: new ones added, ones no longer theirs dropped (their cars stay).
        /// Returns how many were dropped.
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
                if (DealerGame.Place(_regs[r]) == y.Place)
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
                TellFloor(y);
            }
            catch (Exception e)
            {
                Fault("entering a dealership", e);
            }
        }

        // The showroom the player stands in: its lot laid out if it has none, and its cars on their places, held still.
        private void SeeTo(DealerYard y)
        {
            bool laid = false;
            if (!y.Laid)
            {
                laid = DealerGame.Lay(y);
                if (laid)
                {
                    Logger?.Info("Dealership: the lot of '" + y.Name + "' is laid out with " + y.Spots.Count + " place(s).");
                    Tell(NotificationType.Info, "reallife_dealer_note_laid", new Dictionary<string, string>(2) { { "name", y.Name }, { "places", y.Spots.Count.ToString() } },
                        TabLots, LotRow(y));
                }
            }
            int placed = DealerGame.Park(y, _lost, _bays);
            DropLost(y);
            if (Verbose)
            {
                Logger?.Info("Dealership check (entered '" + y.Name + "'): " + (laid ? "the lot laid out, " : string.Empty) + y.Spots.Count + " place(s), "
                             + placed + " of " + y.Cars.Count + " car(s) held on their places"
                             + (_lost.Count > 0 ? ", " + _lost.Count + " the save no longer has taken off" : string.Empty) + ": " + (placed == y.Cars.Count ? "PASS" : "FAIL") + ".");
            }
            Changed();
        }

        // The cars Park found the save no longer has, taken off the stock with their deals.
        private void DropLost(DealerYard y)
        {
            for (int i = 0; i < _lost.Count; i++)
            {
                RemoveDeals(y, _lost[i].Id);
                y.Cars.Remove(_lost[i]);
            }
            if (_lost.Count > 0)
            {
                RebuildIds();
            }
        }

        internal static string LotRow(DealerYard y) => "lot:" + y.Place;

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
            WriteBays(save);
            if (_beforeTests != null)
            {
                save.modData[DealerStore.TestKey] = _beforeTests;
            }
            else
            {
                save.modData.Remove(DealerStore.TestKey);
            }
        }

        internal void Tell(NotificationType type, string key, Dictionary<string, string> data, int tab, string pick)
        {
            Notifications.Show(type, key, data, 11f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        public void Open(int tab, string pick)
        {
            if (_app != null && _view != null && _running)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>The app has been opened: the showrooms are read again, so what it shows is the game's as it is now.</summary>
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
            if (_waiting >= 0 && waiting > _waiting && DealerRules.TellDue(Now, _toldHour))
            {
                _toldHour = Now;
                Tell(NotificationType.Info, "reallife_dealer_note_offers", new Dictionary<string, string>(1) { { "count", waiting.ToString() } }, TabOffers, null);
            }
            _waiting = waiting;
            Badge();
            _view?.Changed();
        }

        /// <summary>The offers waiting for the player's answer, at every showroom.</summary>
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

        /// <summary>For the save tools: what is under way settled, then the state written and the feature stood down.</summary>
        public void StopAll()
        {
            try
            {
                Settle();
                StandDown("the save tools asked", true, true, false);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line was removed, so the state is forgotten.</summary>
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
            _bays.Clear();
            _heard.Clear();
        }
    }
}
