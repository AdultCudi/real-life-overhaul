using System;
using BAModAPI;
using Helpers;
using PlayerActivity;
using UI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Leisure in one place: the activity being played, what is on offer where the player stands, the chess player, the
    /// clothes rack and rule, night life, the films and drink at the wheel. Idle it costs one timer test a frame and a
    /// look round once a second. A fault puts everything borrowed back and switches leisure off for the session.
    /// </summary>
    public sealed partial class LeisureDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("leisure", "LeisureDirector", typeof(LeisureDirector));

        private const float TickInterval = 0.2f;   // seconds
        private const float CardSeconds = 9f;

        public static IModLogger Logger;
        public static LeisureDirector Instance { get; private set; }

        /// <summary>True while leisure has the interaction key: the mod's own door and car keys stand aside.</summary>
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

        /// <summary>Drinks in the player's blood now, 0 when sober.</summary>
        public static float Drinks => LeisureState.Loaded ? LeisureState.DrinksAt(TimeHelper.NowInMinutes()) : 0f;

        public static bool Tipsy => Drinks >= BarDrinks.TipsyFrom;

        internal LeisureScreen Screen => _screen;

        public LeisureKind CurrentKind => _activity != null ? _activity.Kind : LeisureKind.None;

        /// <summary>Whether the chess table is out, and where: for the city map.</summary>
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
        private LeisureScreen _screen;
        private LeisureActivity _activity;
        private string _title;
        private LeisureKind _offer = LeisureKind.None;
        private float _nextSlow;
        private float _nextTick;
        private float _nextDraw;
        private float _noOfferUntil;
        private int _interactFrame = -1;
        private Vector3 _lastPosition;
        private bool _stoodStill;
        private bool _broken;
        private int _loads = -1;
        private bool _tipsyTold;
        private Rigidbody _car;
        private VehicleController _carOf;

        private void Awake()
        {
            Instance = this;
            _screen = new LeisureScreen(Logger, PressInteract, PressChoose, PressAccept);
            _screen.KeptBy(this);
            LeisureSound.Logger = Logger;
            PhotoMode.Logger = Logger;
            ClothesScreen.Logger = Logger;
            _props = new LeisureProps(transform);
            _body = new LeisureBody(_props, Logger);
            _tipsy = gameObject.AddComponent<TipsyDriving>();
            gameObject.AddComponent<LeisureSound>();
            gameObject.AddComponent<PhotoMode>();
            _context.Body = _body;
            _context.Props = _props;
            LeisureVenues.Reset();
            ScenePrewarm.Add("leisure: the city's landmarks being found (six searches of the whole scene)", LeisureVenues.PrewarmLandmarks);
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
            ClothesScreen.Shutdown();
            ClothesShop.Trying = false;
            ClothesShop.RestoreLabels();
            LeisureVenues.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Leisure: LeisureDirector.Update");
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
                else if (_corner.Pending)
                {
                    _corner.Step();   // the chess table's opponent and onlooker, a frame each after the table
                }
                if (_activity == null || Time.timeScale <= 0f || PhotoMode.Active)
                {
                    return;
                }
                float now = Time.time;
                if (_body.NeedsFrame)
                {
                    _body.Frame(Time.deltaTime);
                }
                _activity.Frame(now, Time.deltaTime);
                if (_activity.Meter)
                {
                    _screen.Gauge(_activity);
                }
                if (now >= _nextTick)
                {
                    _nextTick = now + TickInterval;
                    Tick(now);
                }
                if (_activity != null && now >= _nextDraw)
                {
                    _nextDraw = now + TickInterval;
                    _screen.Draw(_activity, _title, _context.Position);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

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
        }

        // The panel's buttons, on the player's key or a click.
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

        private void Slow(float unscaled)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading || !ModState.Loaded
                || !LeisureState.Loaded)
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
            if (Settings.ClothesShops)
            {
                ClothesScreen.Install();
            }

            bool indoors = BuildingManager.IsInsideBuilding;
            BuildingManager manager = indoors ? InstanceBehavior<BuildingManager>.Instance : null;
            LeisureVenues.ScanIndoors(manager);
            string business = LeisureVenues.BusinessIn(manager);
            bool on = Settings.Leisure;

            if (indoors && on && Settings.ClothesShops && business == LeisureVenues.ClothingStore)
            {
                _rack.Tick(manager, _props, position);
            }
            else
            {
                _rack.TakeIn(_props);
            }

            if (!indoors && TrafficTools.Street)
            {
                LeisureVenues.FindLandmarks();
                _corner.Tick(position, unscaled, _activity != null && _activity.Kind == LeisureKind.Chess);
                _night.Tick(position, unscaled);
            }
            else if (indoors && (_corner.Out || _night.Out))
            {
                _corner.TakeIn();
                _night.Clear();
            }

            Cinema(manager, business, on);
            Wander(gm);

            _stoodStill = (position - _lastPosition).sqrMagnitude < 0.02f;
            _lastPosition = position;
            if (_activity != null || !on || unscaled < _noOfferUntil || JobDirector.OnShift || PlayerHelper.IsUsingVehicle || PhotoMode.Active
                || UiOpen())
            {
                SetOffer(LeisureKind.None, default, null);
                return;
            }
            Look(position, player.transform.forward, indoors, manager, business);
        }

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
            PoliceAttention.Report(Offence.DrunkDriving);   // the police look at most once in 25 s
            if (!_tipsyTold)
            {
                _tipsyTold = true;
                Notifications.Show(NotificationType.Warning, "reallife_leisure_drinks_driving", null, 8f);
            }
        }

        public bool StartFromApi(LeisureKind kind, Vector3? place, ThirdPersonCharacter companion, out string refusalKey)
        {
            if (place.HasValue && PlayerHelper.PlayerController != null
                && LeisureReach.Beyond(CitySpots.Flat(place.Value - PlayerHelper.PlayerController.transform.position), LeisureReach.ApiPlace))
            {
                refusalKey = "reallife_leisure_too_far";
                return false;
            }
            return StartActivity(kind, companion, false, out refusalKey);
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
            else if (GameMembers.PlayerDead || EnergyHelper.goingToHospital || uis.timeMachine.isRunning || PlayerActivityUI.IsPanelOpen
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
            else if (SaveGameManager.Current.Energy <= 13f && kind != LeisureKind.Rack)
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
                case LeisureKind.Rack: return Settings.ClothesShops;
                default: return false;
            }
        }

        private bool StartActivity(LeisureKind kind, ThirdPersonCharacter companion, bool test, out string refusalKey)
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
            if (!indoors && !TrafficTools.Street)
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
                default:
                    refusalKey = "reallife_test_refused_nothing_fits";
                    return false;
            }
            _context.Companion = companion;
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
            Logger?.Info("Leisure: '" + LeisureKinds.Id(kind) + "' started" + (test ? " (test)" : string.Empty)
                         + (companion != null ? " with company." : "."));
            return true;
        }

        public void Quit()
        {
            if (_activity != null)
            {
                EndActivity(null, false);
            }
        }

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
            if (GameMembers.PlayerDead || EnergyHelper.goingToHospital || uis.timeMachine.isRunning || PlayerActivityUI.IsPanelOpen
                || PoliceAttention.IsBusy || JobDirector.OnShift || PlayerHelper.IsUsingVehicle || !Enabled(_activity.Kind)
                || RagdollSystem.Holds(gm.playerController.Character))
            {
                reasonKey = "reallife_leisure_end_interrupted";
                return true;
            }
            bool indoors = BuildingManager.IsInsideBuilding;
            if (indoors != _activity.Indoors
                || (indoors && !ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.building, _context.Building)))
            {
                reasonKey = "reallife_leisure_end_left";
                return true;
            }
            if (_activity.Leash > 0f && LeisureReach.Beyond(CitySpots.Flat(gm.playerController.transform.position - _activity.Anchor), _activity.Leash))
            {
                reasonKey = "reallife_leisure_end_left";
                return true;
            }
            return false;
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
                    ModState.AddModifier(ModState.Social);
                }
                if (activity.CardTitle != null)
                {
                    string body = activity.CardBody ?? string.Empty;
                    if (best && before > 0 && activity.Kind != LeisureKind.Sightseeing)
                    {
                        body += "\n" + JobText.T("reallife_leisure_new_best");
                    }
                    LeisureScreen.Card(activity.CardTitle, body, CardSeconds);
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
    }
}
