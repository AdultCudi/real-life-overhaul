using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using Helpers;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Leisure in one place: the one activity that is being played (started from a prompt where
    /// the player stands, a test button or <see cref="Activities.Start"/>), what is on offer
    /// here, the chess player on the pavement, the rack in a clothing store, the people outside
    /// a venue at night, the rule that clothes are bought, the film programme, and what drink
    /// does to driving.
    ///
    /// Idle it costs one timer test a frame and a look round once a second (a handful of
    /// distance tests; the passes over a building's items or the city's landmarks are made once
    /// each). Per frame it works only while an activity is on: the keys, the meter's cursor,
    /// the HUD's fields compared by reference. An error anywhere ends whatever was on, puts the
    /// player and everything borrowed back, and switches leisure off for the session.
    /// </summary>
    public sealed class LeisureDirector : MonoBehaviour
    {
        private const float TickInterval = 0.2f;
        private const float CardSeconds = 9f;

        private static readonly string[] Films =
        {
            "reallife_leisure_film_1", "reallife_leisure_film_2", "reallife_leisure_film_3", "reallife_leisure_film_4",
            "reallife_leisure_film_5", "reallife_leisure_film_6", "reallife_leisure_film_7", "reallife_leisure_film_8",
            "reallife_leisure_film_9", "reallife_leisure_film_10", "reallife_leisure_film_11", "reallife_leisure_film_12"
        };
        private static readonly int[] FilmStars = { 2, 1, 0, 2, 1, 1, 0, 2, 1, 2, 0, 1 };   // 2 a great film, 1 a good one, 0 a dud

        public static IModLogger Logger;
        public static LeisureDirector Instance { get; private set; }

        /// <summary>True while leisure has the interaction key: the mod's own E/F (enter a door, get in a car) stands aside.</summary>
        public static bool TakesInteractKey
        {
            get
            {
                LeisureDirector director = Instance;
                if (director == null || director._broken)
                {
                    return false;
                }
                return director._interactFrame == Time.frameCount || (director._screen != null && director._screen.TakesKey)
                       || (director._activity != null && (director._activity.Prompt != null || director._activity.OwnsInteractKey))
                       || (director._activity == null && director._offer != LeisureKind.None);
            }
        }

        /// <summary>Drinks in the player's blood right now (0 when sober). Safe to read at any time: for crime, life events, friends.</summary>
        public static float Drinks => LeisureState.Loaded ? LeisureState.DrinksAt(TimeHelper.NowInMinutes()) : 0f;

        /// <summary>True from two drinks up: the car wanders.</summary>
        public static bool Tipsy => Drinks >= BarDrinks.TipsyFrom;

        /// <summary>What leisure shows on screen: the game's panels, lists and notifications (see <see cref="LeisureScreen"/>).</summary>
        internal LeisureScreen Screen => _screen;

        public LeisureKind CurrentKind => _activity != null ? _activity.Kind : LeisureKind.None;

        /// <summary>True while the chess player's table is out on a pavement, and where: for the city map (World/MapLinks).</summary>
        public bool ChessTable(out Vector3 at)
        {
            at = _corner.Table;
            return _corner.Out;
        }

        private readonly LeisureContext _context = new LeisureContext();
        private readonly ChessCorner _corner = new ChessCorner();
        private readonly ClothesRack _rack = new ClothesRack();
        private readonly NightLife _night = new NightLife();
        private LeisureProps _props;
        private LeisureBody _body;
        private TipsyDriving _tipsy;
        private LeisureActivity _activity;
        private string _title;
        private LeisureKind _offer = LeisureKind.None;
        private Vector3 _offerPlace;
        private string _offerText;
        private string _offerFor;          // what the text was built for (a kind, a landmark)
        private int _offerKeys = -1;
        private float _nextSlow;
        private float _nextTick;
        private float _nextDraw;
        private LeisureScreen _screen;
        private float _noOfferUntil;
        private int _interactFrame = -1;
        private Vector3 _lastPosition;
        private bool _stoodStill;
        private bool _broken;
        private int _loads = -1;
        private bool _tipsyTold;
        private Rigidbody _car;
        private VehicleController _carOf;

        // the cinema
        private Buildings.Building _cinema;
        private int _film = -1;
        private float _watchedFrom = -1f;

        private void Awake()
        {
            Instance = this;
            _screen = new LeisureScreen(Logger, PressInteract, PressChoose, PressAccept);
            LeisureSound.Logger = Logger;
            PhotoMode.Logger = Logger;
            _props = new LeisureProps(transform);
            _body = new LeisureBody(_props, Logger);
            _tipsy = gameObject.AddComponent<TipsyDriving>();
            gameObject.AddComponent<LeisureSound>();
            gameObject.AddComponent<PhotoMode>();
            _context.Director = this;
            _context.Body = _body;
            _context.Props = _props;
            _context.Logger = Logger;
            LeisureVenues.Reset();
        }

        private void OnDestroy()
        {
            try
            {
                if (_activity != null)
                {
                    EndActivity(null, true);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _screen.Dispose();
            _body.Release();
            _corner.TakeIn();
            _night.Clear();
            _rack.TakeIn(_props);
            _props.Dispose();
            ClothesShop.Trying = false;
            ClothesShop.RestoreLabels();
            LeisureVenues.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_broken)
            {
                return;
            }
            try
            {
                float unscaled = Time.unscaledTime;
                if (unscaled >= _nextSlow)
                {
                    _nextSlow = unscaled + 1f;
                    Slow(unscaled);
                }
                if (_activity == null)
                {
                    return;
                }
                if (Time.timeScale <= 0f || PhotoMode.Active)
                {
                    return;   // the game is paused: nothing is played
                }
                float now = Time.time;
                if (_body.NeedsFrame)
                {
                    _body.Frame(Time.deltaTime);
                }
                _activity.Frame(now, Time.deltaTime);
                if (_activity.Meter)
                {
                    _screen.Gauge(_activity);   // writes a line only when the cursor is in another cell
                }
                if (now >= _nextTick)
                {
                    _nextTick = now + TickInterval;
                    Tick(now);
                }
                if (_activity != null && now >= _nextDraw)
                {
                    _nextDraw = now + TickInterval;
                    Draw();
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // A fault: logged once, the player and everything borrowed put back, leisure off for the session.
        private void Fail(Exception e)
        {
            Logger?.Error(e);
            Logger?.Warn("Leisure: switched off for this session after the error above; the player and everything borrowed were put back.");
            _broken = true;
            try
            {
                EndActivity(null, true);
            }
            catch (Exception inner)
            {
                Logger?.Error(inner);
            }
            _body.Release();
            _corner.TakeIn();
            _night.Clear();
            _rack.TakeIn(_props);
            _tipsy.Stop();
            ClothesShop.Trying = false;
            _screen.HideActivity();
            _screen.HideOffer();
            _screen.CloseList();
        }

        // The buttons of the game's panel (it runs them on the player's key or a click).
        private void PressInteract()
        {
            if (_broken || _activity == null || Time.timeScale <= 0f || PhotoMode.Active || UiOpen())
            {
                return;
            }
            try
            {
                _interactFrame = Time.frameCount;
                _activity.Interact(Time.time);
                _nextDraw = 0f;
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void PressChoose(int choice)
        {
            if (_broken || _activity == null || _activity.Choices == null || Time.timeScale <= 0f || PhotoMode.Active || UiOpen())
            {
                return;
            }
            try
            {
                _activity.Choose(choice, Time.time);
                _nextDraw = 0f;
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void PressAccept()
        {
            if (_broken || _activity != null || _offer == LeisureKind.None || UiOpen())
            {
                return;
            }
            try
            {
                _interactFrame = Time.frameCount;
                Accept();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // ---------------------------------------------------------------- once a second

        private void Slow(float unscaled)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded || !LeisureState.Loaded)
            {
                SetOffer(LeisureKind.None, default, null);
                return;
            }
            PlayerController player = gm.playerController;
            _context.Player = player;
            _body.Bind(player);
            Vector3 position = player.transform.position;

            if (_loads != LeisureState.Loads)
            {
                _loads = LeisureState.Loads;
                if (LeisureState.Fresh)
                {
                    ClothesShop.FirstLoad(TimeHelper.CurrentDay <= 1);
                    Logger?.Info("Leisure: first load of this save; clothes must be bought: " + LeisureState.MustBuy + ".");
                }
                else
                {
                    Settings.SetClothesMustBeBought(LeisureState.MustBuy);   // the options page speaks for this save
                }
            }
            ClothesShop.Enforce();

            bool indoors = BuildingManager.IsInsideBuilding;
            BuildingManager manager = indoors ? InstanceBehavior<BuildingManager>.Instance : null;
            LeisureVenues.ScanIndoors(manager);
            string business = LeisureVenues.BusinessIn(manager);
            bool on = Settings.Leisure;

            if (indoors && on && Settings.ClothesShops && business == LeisureVenues.ClothingStore)
            {
                _rack.Tick(manager, _props, position);
            }
            else if (_activity == null || _activity.Kind != LeisureKind.Rack)
            {
                _rack.TakeIn(_props);
            }

            bool street = !indoors && TrafficTools.Ready;
            if (street)
            {
                LeisureVenues.FindLandmarks();
                _corner.Tick(position, unscaled, _activity != null && _activity.Kind == LeisureKind.Chess);
                _night.Tick(position, unscaled);
            }
            else if (indoors && (_corner.Out || _night.Out))
            {
                // Indoors nothing on the street is in view: both go back to the pool.
                _corner.TakeIn();
                _night.Clear();
            }

            Cinema(manager, business, on);
            Wander(gm);

            _stoodStill = (position - _lastPosition).sqrMagnitude < 0.02f;
            _lastPosition = position;
            if (_activity != null || !on || unscaled < _noOfferUntil || JobDirector.OnShift || PlayerHelper.IsUsingVehicle || PhotoMode.Active || UiOpen())
            {
                SetOffer(LeisureKind.None, default, null);
                return;
            }
            Look(position, player.transform.forward, indoors, manager, business);
        }

        // What can be played where the player stands. A handful of distance tests; the water test only while they stand still by the water.
        private void Look(Vector3 position, Vector3 forward, bool indoors, BuildingManager manager, string business)
        {
            if (indoors)
            {
                if (Settings.ClothesShops && business == LeisureVenues.ClothingStore && _rack.Find(position, out Vector3 rack) && CitySpots.Flat(rack - position) < 2.6f)
                {
                    SetOffer(LeisureKind.Rack, rack, "rack");
                }
                else if (Settings.ClothesShops && LeisureVenues.IsHome(manager) && LeisureVenues.Nearest(LeisureVenues.Wardrobes, position, 2.4f) is ItemController wardrobe)
                {
                    SetOffer(LeisureKind.Wardrobe, wardrobe.transform.position, "wardrobe");
                }
                else if (Settings.LeisureNightOut && business == LeisureVenues.Nightclub && LeisureVenues.Nearest(LeisureVenues.Bars, position, 2.8f) is ItemController bar)
                {
                    SetOffer(LeisureKind.Drinks, bar.transform.position, "drinks");
                }
                else if (Settings.LeisureNightOut && business == LeisureVenues.Nightclub && LeisureVenues.Nearest(LeisureVenues.DanceFloors, position, 3.5f) is ItemController floor)
                {
                    SetOffer(LeisureKind.Dance, floor.transform.position, "dance");
                }
                else
                {
                    SetOffer(LeisureKind.None, default, null);
                }
                return;
            }
            if (Settings.LeisureChess && _corner.Out && _corner.OpponentThere && CitySpots.Flat(_corner.Stool - position) < 3f)
            {
                SetOffer(LeisureKind.Chess, _corner.Stool, "chess");
                return;
            }
            if (Settings.LeisureFishing && _stoodStill && LeisureVenues.WatersideDistance(position) < 320f
                && LeisureVenues.WaterAhead(position, forward, out Vector3 water, out _))
            {
                SetOffer(LeisureKind.Fishing, water, "fishing");
                return;
            }
            if (Settings.LeisureSights)
            {
                Landmark mark = LeisureVenues.LandmarkAt(position);
                if (mark != null && !LeisureState.HasSeen(mark.Id))
                {
                    SetOffer(LeisureKind.Sightseeing, position, mark.Id);
                    return;
                }
            }
            SetOffer(LeisureKind.None, default, null);
        }

        // The prompt for what is on offer. Its text is built when the offer (or the key's binding) changes, not each second.
        private void SetOffer(LeisureKind kind, Vector3 place, string what)
        {
            _offer = kind;
            _offerPlace = place;
            if (kind == LeisureKind.None)
            {
                if (_offerFor != null)
                {
                    _offerFor = null;
                    if (_activity == null)
                    {
                        _screen.HideOffer();
                    }
                }
                return;
            }
            if (what != _offerFor || _offerKeys != Keys.Version)
            {
                _offerFor = what;
                _offerKeys = Keys.Version;
                switch (kind)
                {
                    case LeisureKind.Chess: _offerText = JobText.T("reallife_leisure_offer_chess", "amount", JobText.Money(ChessGame.Stake)); break;
                    case LeisureKind.Fishing: _offerText = JobText.T("reallife_leisure_offer_fishing", "amount", JobText.Money(Fishing.Bait)); break;
                    case LeisureKind.Dance: _offerText = JobText.T("reallife_leisure_offer_dance"); break;
                    case LeisureKind.Drinks: _offerText = JobText.T("reallife_leisure_offer_drinks"); break;
                    case LeisureKind.Rack: _offerText = JobText.T("reallife_leisure_offer_rack"); break;
                    case LeisureKind.Wardrobe: _offerText = JobText.T("reallife_leisure_offer_wardrobe"); break;
                    default:
                        Landmark mark = LeisureVenues.LandmarkAt(_lastPosition);
                        _offerText = JobText.T("reallife_leisure_offer_sights", "place", mark != null ? JobText.T(mark.NameKey) : string.Empty);
                        break;
                }
            }
            if (_activity == null)
            {
                _screen.Offer(_offerText, place, _lastPosition);
            }
        }

        private void Accept()
        {
            LeisureKind kind = _offer;
            Vector3 place = _offerPlace;
            if (!StartActivity(kind, place, null, false, out string refusal) && refusal != null)
            {
                Notifications.Show(NotificationType.Warning, refusal, null, 6f);
            }
        }

        // ---------------------------------------------------------------- the film programme

        // The game seats the player in a cinema or theatre and passes the time itself ("watch show"). What it does not
        // have is what was on: each house shows one film a day from a list, named on the way in, and a show watched for
        // three quarters of an hour or more is remembered with what the player thought of it.
        private void Cinema(BuildingManager manager, string business, bool on)
        {
            bool inside = on && Settings.LeisureCinema && manager != null && (business == LeisureVenues.Cinema || business == LeisureVenues.Theater);
            if (!inside)
            {
                _cinema = null;
                _film = -1;
                _watchedFrom = -1f;
                return;
            }
            if (!ReferenceEquals(manager.building, _cinema))
            {
                _cinema = manager.building;
                string name = manager.building.name ?? string.Empty;
                int hash = TimeHelper.CurrentDay * 31;
                for (int i = 0; i < name.Length; i++)
                {
                    hash = hash * 17 + name[i];
                }
                _film = Mathf.Abs(hash % Films.Length);
                _watchedFrom = -1f;
                JobText.Toast(NotificationType.Info, business == LeisureVenues.Cinema ? "reallife_leisure_cinema_showing" : "reallife_leisure_theatre_showing",
                    "film", JobText.T(Films[_film]), 8f);
            }
            bool watching = false;
            List<HappinessModifierData> modifiers = SaveGameManager.Current.happinessModifiers;
            for (int i = 0; modifiers != null && i < modifiers.Count; i++)
            {
                string type = modifiers[i].type;
                if (type != null && type.IndexOf("watching_show", StringComparison.Ordinal) >= 0)
                {
                    watching = true;
                    break;
                }
            }
            float minutes = TimeHelper.NowInMinutes();
            if (watching && _watchedFrom < 0f)
            {
                _watchedFrom = minutes;
            }
            else if (!watching && _watchedFrom >= 0f)
            {
                float watched = minutes - _watchedFrom;
                _watchedFrom = -1f;
                if (watched >= 45f && _film >= 0)
                {
                    int stars = FilmStars[_film];
                    LeisureState.Record("cinema", LeisureState.PlaysOf("cinema") + 1);
                    if (stars > 0)
                    {
                        ModState.AddModifier(stars == 2 ? ModState.MoodUpBig : ModState.MoodUp);
                    }
                    JobText.Toast(NotificationType.Success,
                        stars == 2 ? "reallife_leisure_cinema_great" : stars == 1 ? "reallife_leisure_cinema_good" : "reallife_leisure_cinema_dud",
                        "film", JobText.T(Films[_film]), 9f);
                }
            }
        }

        // ---------------------------------------------------------------- drink at the wheel

        private void Wander(GameManager gm)
        {
            VehicleController vehicle = PlayerHelper.IsUsingVehicle ? gm.selectedVehicle : null;
            float drinks = vehicle != null && Settings.Leisure && Settings.LeisureNightOut ? Drinks : 0f;
            if (drinks < BarDrinks.TipsyFrom)
            {
                _tipsy.Stop();
                _tipsyTold = false;
                _carOf = null;
                _car = null;
                return;
            }
            if (!ReferenceEquals(vehicle, _carOf))
            {
                _carOf = vehicle;
                _car = vehicle.GetComponent<Rigidbody>();
            }
            if (_car == null)
            {
                _tipsy.Stop();
                return;
            }
            _tipsy.Drive(_car, drinks);
            PoliceAttention.Report(Offence.DrunkDriving);   // somebody may notice the car wandering (one look every 25 s at most)
            if (!_tipsyTold)
            {
                _tipsyTold = true;
                Notifications.Show(NotificationType.Warning, "reallife_leisure_drinks_driving", null, 8f);
            }
        }

        // ---------------------------------------------------------------- starting

        /// <summary>From <see cref="Activities.Start"/>: where the player stands (or at the place given, if it is within reach).</summary>
        public bool StartFromApi(LeisureKind kind, Vector3? place, ThirdPersonCharacter companion, out string refusalKey)
        {
            if (place.HasValue && PlayerHelper.PlayerController != null && CitySpots.Flat(place.Value - PlayerHelper.PlayerController.transform.position) > 6f)
            {
                refusalKey = "reallife_leisure_too_far";
                return false;
            }
            return StartActivity(kind, place ?? default, companion, false, out refusalKey);
        }

        private static bool Free(LeisureKind kind, out string refusalKey)
        {
            refusalKey = null;
            var uis = InstanceBehavior<UIs>.Instance;
            var gm = InstanceBehavior<GameManager>.Instance;
            if (uis == null || gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading
                || !ModState.Loaded || !LeisureState.Loaded)
            {
                refusalKey = "reallife_test_refused_not_loaded";
            }
            else if (FuneralHelper.PlayerDead || EnergyHelper.goingToHospital || uis.timeMachine.isRunning || PlayerActivityUI.IsPanelOpen
                     || GameManager.IsAnyMiniGameActive() || PhotoMode.Active || PoliceAttention.IsBusy || gm.playerController.NavigationDisabled)
            {
                refusalKey = "reallife_leisure_no_now";
            }
            else if (JobDirector.OnShift)
            {
                refusalKey = "reallife_leisure_no_job";
            }
            else if (PlayerHelper.IsUsingVehicle)
            {
                refusalKey = "reallife_leisure_no_vehicle";
            }
            else if (SaveGameManager.Current.Energy <= 13f && kind != LeisureKind.Rack && kind != LeisureKind.Wardrobe)
            {
                refusalKey = "reallife_leisure_no_energy";
            }
            return refusalKey == null;
        }

        private static bool Enabled(LeisureKind kind)
        {
            if (!Settings.Leisure)
            {
                return false;
            }
            switch (kind)
            {
                case LeisureKind.Chess: return Settings.LeisureChess;
                case LeisureKind.Fishing: return Settings.LeisureFishing;
                case LeisureKind.Dance:
                case LeisureKind.Drinks: return Settings.LeisureNightOut;
                case LeisureKind.Sightseeing: return Settings.LeisureSights;
                case LeisureKind.Rack:
                case LeisureKind.Wardrobe: return Settings.ClothesShops;
                default: return false;
            }
        }

        private bool StartActivity(LeisureKind kind, Vector3 place, ThirdPersonCharacter companion, bool test, out string refusalKey)
        {
            if (_broken || !Enabled(kind))
            {
                refusalKey = "reallife_test_refused_feature_off";
                return false;
            }
            if (_activity != null)
            {
                refusalKey = "reallife_test_refused_scene_active";
                return false;
            }
            if (!Free(kind, out refusalKey))
            {
                return false;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            _context.Player = gm.playerController;
            _body.Bind(gm.playerController);
            bool indoors = BuildingManager.IsInsideBuilding;
            if (!indoors && !TrafficTools.Ready && kind != LeisureKind.Rack && kind != LeisureKind.Wardrobe)
            {
                refusalKey = "reallife_test_refused_traffic_off";
                return false;
            }
            LeisureActivity activity;
            switch (kind)
            {
                case LeisureKind.Chess: activity = new ChessGame(_corner); break;
                case LeisureKind.Fishing: activity = new Fishing(); break;
                case LeisureKind.Dance: activity = new DanceFloor(); break;
                case LeisureKind.Drinks: activity = new BarDrinks(); break;
                case LeisureKind.Sightseeing: activity = new Sightseeing(); break;
                case LeisureKind.Rack: activity = new ClothesBrowser(false, place == default ? _context.Position : place); break;
                case LeisureKind.Wardrobe: activity = new ClothesBrowser(true, place == default ? _context.Position : place); break;
                default:
                    refusalKey = "reallife_test_refused_nothing_fits";
                    return false;
            }
            _context.Companion = companion;
            _context.Place = place == default ? (Vector3?)null : place;
            _context.Test = test;
            _context.Building = indoors ? InstanceBehavior<BuildingManager>.Instance.building : null;
            activity.Ctx = _context;
            activity.Indoors = indoors;
            bool begun;
            try
            {
                begun = activity.Begin(Time.time, out refusalKey);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                refusalKey = "reallife_leisure_no_now";
                begun = false;
            }
            if (!begun)
            {
                _body.Release();
                try
                {
                    activity.End();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                Logger?.Info("Leisure: '" + LeisureKinds.Id(kind) + "' not started (" + refusalKey + ").");
                return false;
            }
            _activity = activity;
            _title = JobText.T(activity.TitleKey);
            _nextTick = Time.time + TickInterval;
            SetOffer(LeisureKind.None, default, null);
            Logger?.Info("Leisure: '" + LeisureKinds.Id(kind) + "' started" + (test ? " (test)" : string.Empty) + (companion != null ? " with company." : "."));
            return true;
        }

        /// <summary>Ends whatever is on as if the player had walked away.</summary>
        public void Quit()
        {
            if (_activity != null)
            {
                EndActivity(null, false);
            }
        }

        // ---------------------------------------------------------------- five times a second

        private void Tick(float now)
        {
            if (Interrupted(out string reason))
            {
                EndActivity(reason, false);
                return;
            }
            _activity.Tick(now);
            if (_activity.Finished)
            {
                EndActivity(null, false);
            }
        }

        // Anything that takes the player out of their own hands, or away from where it is played, ends it.
        private bool Interrupted(out string reasonKey)
        {
            reasonKey = null;
            var uis = InstanceBehavior<UIs>.Instance;
            var gm = InstanceBehavior<GameManager>.Instance;
            if (uis == null || gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading)
            {
                return true;
            }
            bool browsing = _activity.Kind == LeisureKind.Rack || _activity.Kind == LeisureKind.Wardrobe;
            if (FuneralHelper.PlayerDead || EnergyHelper.goingToHospital || uis.timeMachine.isRunning || PlayerActivityUI.IsPanelOpen
                || PoliceAttention.IsBusy || JobDirector.OnShift || PlayerHelper.IsUsingVehicle || !Enabled(_activity.Kind)
                || RagdollSystem.Holds(gm.playerController.Character))
            {
                reasonKey = browsing ? null : "reallife_leisure_end_interrupted";
                return true;
            }
            bool indoors = BuildingManager.IsInsideBuilding;
            if (indoors != _activity.Indoors
                || (indoors && !ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.building, _context.Building)))
            {
                reasonKey = browsing ? null : "reallife_leisure_end_left";
                return true;
            }
            if (_activity.Leash > 0f && CitySpots.Flat(gm.playerController.transform.position - _activity.Anchor) > _activity.Leash)
            {
                reasonKey = browsing ? null : "reallife_leisure_end_left";
                return true;
            }
            return false;
        }

        // Five times a second: the card, the marker and the panel are told what changed (the gauge has its own frame).
        private void Draw()
        {
            _screen.Draw(_activity, _title, _context.Position);
        }

        private void EndActivity(string reasonKey, bool silent)
        {
            LeisureActivity activity = _activity;
            if (activity == null)
            {
                return;
            }
            _activity = null;
            _body.Release();
            try
            {
                activity.End();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _screen.HideActivity();
            _noOfferUntil = Time.unscaledTime + 2.5f;
            string id = LeisureKinds.Id(activity.Kind);
            Logger?.Info("Leisure: '" + id + "' ended (" + (activity.Played ? "played, score " + activity.Score : reasonKey ?? "left") + ").");
            if (silent)
            {
                return;
            }
            if (activity.Played)
            {
                bool scored = activity.Kind == LeisureKind.Chess || activity.Kind == LeisureKind.Fishing || activity.Kind == LeisureKind.Dance
                              || activity.Kind == LeisureKind.Sightseeing;
                int before = LeisureState.BestOf(id);
                bool best = scored && LeisureState.Record(id, activity.Score);
                if (activity.Mood != null)
                {
                    ModState.AddModifier(activity.Mood);
                }
                if (_context.Companion != null)
                {
                    ModState.AddModifier(ModState.Social);   // good company
                }
                if (activity.CardTitle != null)
                {
                    string body = activity.CardBody ?? string.Empty;
                    if (best && before > 0 && activity.Kind != LeisureKind.Sightseeing)
                    {
                        body += "\n" + JobText.T("reallife_leisure_new_best");
                    }
                    _screen.Card(activity.CardTitle, body, CardSeconds);
                }
            }
            else if (reasonKey != null)
            {
                Notifications.Show(NotificationType.Info, reasonKey, null, 6f);
            }
            Activities.Raise(activity.Kind, activity.Played ? activity.Score : 0, activity.Played);
        }

        private static bool UiOpen()
        {
            if (ThirdPersonCamera.IsUiOpen())
            {
                return true;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null && uis.playerHUD.dialogUI != null && uis.playerHUD.dialogUI.isPanelOpen;
        }

        // ---------------------------------------------------------------- the options page

        /// <summary>Test button: the activity where the player stands, or one clear sentence why not.</summary>
        public void TestStart(LeisureKind kind)
        {
            if (!ModState.Loaded || !LeisureState.Loaded || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, "Leisure " + kind, TestRefusal.NotLoaded);
                return;
            }
            if (_broken || !Enabled(kind))
            {
                TestRefusals.Tell(Logger, "Leisure " + kind, TestRefusal.FeatureOff);
                return;
            }
            Vector3 position = PlayerHelper.PlayerController.transform.position;
            if (kind == LeisureKind.Chess && _activity == null && (!_corner.Out || CitySpots.Flat(_corner.Stool - position) > 4f))
            {
                // The chess player is not here: one is set up just ahead (a test may do that in view), and the player walks up.
                TestRefusal street = TestRefusals.Street();
                if (street != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Leisure Chess", street);
                    return;
                }
                _corner.TakeIn();
                Vector3 ahead = position + PlayerHelper.PlayerController.transform.forward * 3.5f;
                if (CitySpots.Pavement(ahead, 5f, out Vector3 anchor) && !_corner.PutOut(anchor, position, out TestRefusal why))
                {
                    // No room for a table and two stools (nothing fits), nobody free to sit at it (pool busy), or the prop is not in yet (not loaded).
                    TestRefusals.Tell(Logger, "Leisure Chess", why, 0, "table at " + anchor.ToString("F0"));
                    return;
                }
                if (!_corner.Out)
                {
                    TestRefusals.Tell(Logger, "Leisure Chess", TestRefusal.NothingFits, 0, "no pavement within 5 m ahead");
                    return;
                }
                Notifications.Show(NotificationType.Info, "reallife_leisure_chess_set_up", null, 8f);
                return;
            }
            if (!StartActivity(kind, default, null, true, out string refusal))
            {
                TestRefusals.TellKey(Logger, "Leisure " + kind, refusal);
            }
        }

        public void TestNight()
        {
            if (PlayerHelper.PlayerController == null || TestRefusals.Street() != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Night life", PlayerHelper.PlayerController == null ? TestRefusal.NotLoaded : TestRefusals.Street());
                return;
            }
            _night.Test(PlayerHelper.PlayerController.transform.position, Time.unscaledTime);
        }

        /// <summary>Test button: this save's records, landmarks and catch on a card.</summary>
        public void ShowRecords()
        {
            if (!LeisureState.Loaded)
            {
                TestRefusals.Tell(Logger, "Leisure records", TestRefusal.NotLoaded);
                return;
            }
            LeisureVenues.FindLandmarks();
            string body = JobText.T("reallife_leisure_records_chess", "best", LeisureState.BestOf("chess").ToString(), "plays", LeisureState.PlaysOf("chess").ToString())
                          + "\n" + JobText.T("reallife_leisure_records_fishing", "best", (LeisureState.BestOf("fishing") / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "kinds", LeisureState.FishKinds.ToString())
                          + "\n" + JobText.T("reallife_leisure_records_dance", "best", LeisureState.BestOf("dance").ToString(), "plays", LeisureState.PlaysOf("dance").ToString())
                          + "\n" + JobText.T("reallife_leisure_records_sights", "seen", LeisureState.SeenCount.ToString(), "all", LeisureVenues.Landmarks.Count.ToString())
                          + "\n" + JobText.T("reallife_leisure_records_films", "films", LeisureState.PlaysOf("cinema").ToString(), "clothes", LeisureState.OwnedCount.ToString())
                          + "\n" + JobText.T("reallife_leisure_records_spent", "amount", JobText.Money(LeisureState.Spent));
            string missing = null;
            for (int i = 0; i < LeisureVenues.Landmarks.Count; i++)
            {
                if (!LeisureState.HasSeen(LeisureVenues.Landmarks[i].Id))
                {
                    missing = JobText.T(LeisureVenues.Landmarks[i].NameKey);
                    break;
                }
            }
            if (missing != null)
            {
                body += "\n" + JobText.T("reallife_leisure_records_next", "place", missing);
            }
            _screen.Card(JobText.T("reallife_leisure_records_title"), body, 16f);
        }
    }
}
