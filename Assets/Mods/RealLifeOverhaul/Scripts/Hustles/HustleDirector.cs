using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>
    /// Legal hustles (docs/features/hustles.md): the Marketplace app, where the city's people sell used goods and cars that
    /// the player buys, collects at the seller's door or has brought home, fixes, cleans and sells on to buyers who haggle;
    /// and freelance visits clients ask for (detailing, odd jobs, junk runs and moves), done at the client's door on the
    /// game's own work panel or left to a hired helper. This owns the state and the hour. Cost: a timer comparison a frame;
    /// once a second the day and hour are checked and the few doors in play measured against the player.
    /// </summary>
    public sealed partial class HustleDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("hustles", "HustleDirector", typeof(HustleDirector));

        public const string AppId = "RealLifeMarketplace";
        public const int TabListings = 0, TabYours = 1, TabOffers = 2, TabFreelance = 3;

        private const int CatchUp = 24;     // hours missed that are still worked through one by one; more is a gap
        private static readonly string[] AppGlyphs = { "Icon-Coin", "Icon-Shop", "Icon-Money" };

        public static IModLogger Logger;
        public static HustleDirector Instance { get; private set; }

        /// <summary>True while the panel at a door has the interaction key: the mod's own door key stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && Instance._door != null && Instance._door.TakesKey;

        private readonly List<HustlePlace> _homes = new List<HustlePlace>(256);
        private readonly List<MarketModel> _cars = new List<MarketModel>(32);
        private readonly List<string> _colours = new List<string>(16);
        private readonly List<int> _worths = new List<int>(HustleCatalogue.OwnedMost);
        private readonly List<HustleEvent> _events = new List<HustleEvent>(8);

        private HustleState _state;
        private bool _running, _broken, _gaveUp, _cityRead;
        private float _next;
        private HustleApp _view;
        private PhoneApp _app;
        private Action _onSave;

        public bool Running => _running;

        /// <summary>The state in use; null while the feature is not running.</summary>
        public HustleState State => _running ? _state : null;

        private static bool Verbose => Meta.Dev.Verbose;

        private void Awake()
        {
            Instance = this;
            GameSprites.Want(AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            _onSave = OnSave;
            MakePanel();
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                StandDown("the city is unloading", false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Hustles: HustleDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.unscaledTime;
            if (now < _next)
            {
                return;
            }
            _next = now + 1f;
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
            if (!HustleGame.Ready)
            {
                return;
            }
            if (!Settings.Hustles)
            {
                if (_running)
                {
                    StandDown("the option is off", true);
                }
                return;
            }
            if (!_running)
            {
                StartUp();
            }
            SaveHook.Keep(_onSave);   // the game clears its event hub on every load, so the hook is kept attached
            if (_app == null && !_gaveUp)
            {
                Install();
            }
            Clock();
            Places();
        }

        private void StartUp()
        {
            GameInstance save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            HustleState state = HustleStore.Load(save.modData, out int version);
            bool fresh = state == null;
            _state = state ?? new HustleState { Seed = HustleRules.SeedOf(HustleGame.SeedText) };
            _running = true;
            ReadCity();
            int lost = Reconcile();
            Logger?.Info("Hustles: " + (fresh ? "opened for this save" : "state loaded (version " + version + ")") + " on day " + save.Day + ". " + _state.Listings.Count
                         + " listing(s), " + _state.Items.Count + " thing(s) of the player's, " + _state.Offers.Count + " offer(s), " + _state.Gigs.Count + " visit(s); " + _homes.Count
                         + " homes in the city, " + _cars.Count + " kind(s) of car" + (lost > 0 ? "; " + lost + " car(s) the save no longer has taken off" : string.Empty) + ".");
        }

        // The city's homes and cars, once a city: the same list on every machine.
        private void ReadCity()
        {
            if (_cityRead)
            {
                return;
            }
            HustleGame.Homes(_homes);
            HustleGame.Cars(_cars, _colours);
            _cityRead = _homes.Count > 0;
        }

        private void StandDown(string why, bool tell)
        {
            bool was = _running;
            if (was && SaveGameManager.Current != null && _state != null)
            {
                Write(SaveGameManager.Current);
            }
            _running = false;
            CloseWork();
            HidePanel("the Marketplace stood down");
            RemoveApp();
            if (was && (tell || Verbose))
            {
                Logger?.Info("Hustles check (stopped, " + why + "): the things bought stay in the save, the cars stay the game's own vehicles, and nothing of the game's is held: PASS");
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
                StandDown("a fault", false);
            }
            catch (Exception second)
            {
                Logger?.Error(second);
                _running = false;
            }
            Logger?.Warn("Hustles: switched off for this session after an error in " + where + ". The things bought and the cars stay the player's.");
        }

        private void Install()
        {
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            var view = new HustleApp(this);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_hustle_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(255, 178, 74, 255), new Color32(214, 104, 28, 255), 0.42f, "AppIcon-Marketplace"),
                Build = view.Build,
                Opened = view.Opened,
                Closed = view.Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Hustles: the game's phone is not built as this version expects, so the Marketplace app is left off. Things already bought are still collected at their doors.");
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

        private void Badge()
        {
            if (_app == null || _state == null)
            {
                return;
            }
            int booked = 0;
            for (int i = 0; i < _state.Gigs.Count; i++)
            {
                booked += _state.Gigs[i].Stage == GigStage.Booked || _state.Gigs[i].Stage == GigStage.Loaded ? 1 : 0;
            }
            _app.SetBadge(_state.Waiting + booked);
        }

        /// <summary>Opens the app on a tab, with a row picked.</summary>
        public void Open(int tab, string pick)
        {
            if (_app != null && _view != null)
            {
                _view.Want(tab, pick);
                _app.Open();
            }
        }

        /// <summary>Something changed that the app shows: written again if it is open, the badge put right.</summary>
        internal void Changed()
        {
            Badge();
            _view?.Changed();
        }

        /// <summary>The app is opened: true while there is a state to show.</summary>
        internal bool AppOpened()
        {
            if (_running)
            {
                Clock();
            }
            return _running;
        }

        // ---------------------------------------------------------------- the clock

        private void Clock()
        {
            int stamp = HustleGame.Stamp;
            bool gap = _state.LastHour < 0 || stamp - _state.LastHour > CatchUp || stamp < _state.LastHour;
            if (gap)
            {
                _state.LastHour = stamp - 1;   // hours that went by without the feature are not worked through, this one is
            }
            if (_state.LastHour >= stamp && _state.Day == stamp / 24)
            {
                return;
            }
            ReadCity();
            _events.Clear();
            while (_state.LastHour < stamp)
            {
                _state.LastHour++;
                int day = _state.LastHour / 24;
                if (_state.Day != day)
                {
                    HustleRules.Morning(_state, day, _homes, _cars, _colours, HustleGame.Level);
                    if (Verbose)
                    {
                        Logger?.Info("Hustles: the listings of day " + day + ": " + _state.Listings.Count + " thing(s), " + _state.Gigs.Count + " visit(s) asked for or booked (seed "
                                     + _state.Seed + ").");
                    }
                }
                Reconcile();
                Worths();
                HustleRules.Hour(_state, _state.LastHour, _worths, _events);
            }
            if (_state.Day != stamp / 24)
            {
                HustleRules.Morning(_state, stamp / 24, _homes, _cars, _colours, HustleGame.Level);
            }
            Tell(_events);
            Changed();
        }

        /// <summary>Each thing's worth now, in the order of the state's things: a car's from the game's vehicle as it is.</summary>
        private void Worths()
        {
            _worths.Clear();
            int level = HustleGame.Level;
            for (int i = 0; i < _state.Items.Count; i++)
            {
                Owned o = _state.Items[i];
                int dirt = 0;
                if (o.Car && o.VehicleId != null && HustleGame.ReadCar(o.VehicleId, out int condition, out dirt))
                {
                    o.Condition = condition;
                }
                _worths.Add(HustleRules.Worth(o, level, dirt));
            }
        }

        /// <summary>The worth of one thing now, as the hour reads it.</summary>
        internal int WorthOf(Owned o)
        {
            int dirt = 0;
            if (o.Car && o.VehicleId != null && HustleGame.ReadCar(o.VehicleId, out int condition, out dirt))
            {
                o.Condition = condition;
            }
            return HustleRules.Worth(o, HustleGame.Level, dirt);
        }

        /// <summary>Cars of the player's the save no longer has (sold through the game, written off) leave the list. Returns how many.</summary>
        private int Reconcile()
        {
            int lost = 0;
            for (int i = _state.Items.Count - 1; i >= 0; i--)
            {
                Owned o = _state.Items[i];
                if (!o.Car || o.VehicleId == null || HustleGame.Vehicle(o.VehicleId) != null)
                {
                    continue;
                }
                _state.Items.RemoveAt(i);
                for (int k = _state.Offers.Count - 1; k >= 0; k--)
                {
                    if (_state.Offers[k].Item == o.Id)
                    {
                        _state.Offers.RemoveAt(k);
                    }
                }
                lost++;
                Logger?.Info("Hustles: the " + o.Thing + " bought on day " + o.Bought + " is no longer in the save (sold or written off in the game); taken off the player's things.");
                HustleText.Note(NotificationType.Info, "reallife_hustle_note_car_gone", "thing", HustleText.Thing(o.Thing, true));
            }
            return lost;
        }

        private void Tell(List<HustleEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                HustleEvent e = events[i];
                Owned o = _state.Item(e.Id);
                string thing = o != null ? HustleText.Thing(o.Thing, o.Car) : string.Empty;
                switch (e.Kind)
                {
                    case HustleEventKind.Delivered:
                        HustleText.Note(NotificationType.Success, "reallife_hustle_note_delivered", "thing", thing);
                        break;
                    case HustleEventKind.Offer:
                        HustleText.Note(NotificationType.Info, "reallife_hustle_note_offer", "who", e.Who, "amount", HustleText.Money(e.Amount), "thing", thing, 10f);
                        break;
                    case HustleEventKind.Lapsed:
                        HustleText.Note(NotificationType.Info, "reallife_hustle_note_lapsed", "who", e.Who, "thing", thing);
                        break;
                    case HustleEventKind.Missed:
                        HustleText.Note(NotificationType.Warning, "reallife_hustle_note_missed", "who", e.Who, 10f);
                        break;
                    case HustleEventKind.HelperDone:
                        HustleGame.Earned(e.Amount, "reallife_transaction_hustle_helper");
                        HustleText.Note(NotificationType.Success, "reallife_hustle_note_helper_done", "who", e.Who, "amount", HustleText.Money(e.Amount), "stars", e.Stars.ToString(), 10f);
                        Logger?.Info("Hustles check (helper): the helper's visit for " + e.Who + " is done, " + e.Stars + " star(s), " + e.Amount + " kept after their share: PASS");
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- the save

        private void OnSave()
        {
            try
            {
                if (_running && SaveGameManager.Current != null && _state != null)
                {
                    Write(SaveGameManager.Current);
                    if (Verbose)
                    {
                        Logger?.Info("Hustles check (before a save): " + _state.Items.Count + " thing(s), " + _state.Gigs.Count + " visit(s) written under " + HustleStore.Key
                                     + "; the cars are the game's own vehicles: PASS");
                    }
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
            save.modData[HustleStore.Key] = HustleStore.Write(_state);
        }

        /// <summary>Everything put away (the save tools, the feature list's clean-up): the panel, the work on the clock and the app.</summary>
        public void StopAll()
        {
            CloseWork();
            HidePanel("put away");
            RemoveApp();
        }

        /// <summary>This save's Marketplace forgotten (its line was just removed): opened afresh on the next second.</summary>
        public void Forget()
        {
            StopAll();
            _running = false;
            _state = null;
            _broken = false;
            _gaveUp = false;
        }
    }
}
