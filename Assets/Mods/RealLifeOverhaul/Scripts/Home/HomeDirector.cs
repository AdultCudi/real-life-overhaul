using System;
using BAModAPI;
using BigAmbitions.Items;
using Extensions;
using Helpers;
using Parking.UndergroundParking;
using PlayerActivity;
using PlayerActivity.Activities.Rest;
using UI;
using UI.Load;
using UI.MiniMenu;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A routine the player asked for: from the phone, the furniture, the game's panel or a test button.</summary>
    public enum HomeWish
    {
        None,
        Cook,          // opens the cooking list
        CookFirst,     // a test: the first dish the fridges allow, without the list
        Coffee,
        Tidy,
        Stretch,
        PushUps,
        SitUps,
        Run,
        StopRun,
        Doctor
    }

    /// <summary>
    /// Home life and health: once a second it reads what the player is doing off the game's activity panel and moves the
    /// needs; on the hour and at midnight it settles mood, illness and diet; it offers the routines. Cost: idle, one time
    /// compare a frame; a run is ticked four times a second. A fault switches the feature off for the session.
    /// </summary>
    public sealed partial class HomeDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("home", "HomeDirector", typeof(HomeDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const string ReadingParameter = "ReadingABook";

        public static IModLogger Logger;
        public static HomeDirector Instance { get; private set; }

        /// <summary>The player's body for the mod's routines (the jobs' class: held on the spot, a clip or the game's own animation).</summary>
        public JobBody Body { get; private set; }

        /// <summary>Set by a routine just before it feeds the player: what the meal the game is about to report was. Skipped means "not ours".</summary>
        public Meal OwnMeal = Meal.Skipped;

        public bool Jogging => _run != null && _run.Active;

        private JobProps _props;
        private JogRun _run;
        private HomeActivity _activity;
        private HomeActivity _starting;      // chosen on the cooking list: started for the player once the game's panel has shown it
        private CookPlan _plan;              // chosen on the cooking list and waiting for the player to be free
        private float _planUntil;
        private Action _later;
        private int _laterFrame;
        private HomeWish _wish;
        private float _wishUntil;
        private ItemController _clicked;     // the coffee machine a click was on: the cup is made at that one
        private readonly GameUi.WorldPanel _doctor = new GameUi.WorldPanel();
        private Action _onDoctor;
        private int _keyFrame = -1;
        private float _nextSlow;
        private float _nextRunTick;
        private bool _broken;

        private Doing _doing;
        private float _lastMinutes = -1f;
        private int _lastHour = -1;
        private float _lastHunger;
        private float _slept;
        private int _bedHour;
        private int _bedQuality;
        private bool _bedCar;
        private bool _bedHome;
        private int _readingKnown;       // 0 not asked, 1 the player's animator has the parameter, 2 it has not
        private bool _wasOn;

        private void Awake()
        {
            Instance = this;
            HomeState.Reset();
            HomeTidy.Reset();
            HomePlace.Reset();
            JogRun.ForgetCity();
            ScenePrewarm.Add("home: the city's parks being listed for the run", JogRun.Prewarm);
            _props = new JobProps(transform);
            Body = new JobBody(_props, Logger);
            _run = new JogRun(Logger);
            _run.KeptBy(this);
            HomeHealth.Logger = Logger;
            HomeOffers.Logger = Logger;
            HomeFridgeButton.Logger = Logger;
            HomeCookList.Logger = Logger;
            HomeNeedsBar.Logger = Logger;
            GameUi.WorldPanel.Logger = Logger;
            GameUi.TravelList.Logger = Logger;
            GameUi.GuideMark.Logger = Logger;
            _onDoctor = DoctorClicked;
        }

        /// <summary>True while the doctor's panel holds the game's interact key, and in the frame its button ran: the mod's own key then stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._doctor.Showing || Instance._keyFrame == Time.frameCount);

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GameEvent.onGameEventTriggered -= _onGameEvent;   // the wrapped handler that was put on
            TakeBack();
            HomeCookList.Remove();
            HomeNeedsBar.Remove();
            _run.Dispose();
            _props.Dispose();
            HomeHealth.Reset();
            HomeState.Reset();
            HomeTidy.Reset();
            HomePlace.Reset();
            JogRun.ForgetCity();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Runs something a few frames from now (one thing at a time: the routines' own next step).</summary>
        public void Later(Action what, int frames)
        {
            _later = what;
            _laterFrame = Time.frameCount + frames;
        }

        /// <summary>The game ran a click of ours: on a cooker, a coffee machine, or "Cook a meal" in a fridge's panel.</summary>
        public void OfferClicked(HomeWish wish, ItemController item)
        {
            if (_broken)
            {
                return;
            }
            try
            {
                bool busy = ThirdPersonCamera.IsUiOpen();
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (choice): the player clicks "
                                 + (wish == HomeWish.Coffee ? "a coffee machine" : item is FridgeController ? "'Cook a meal' in a fridge's panel" : "a cooker")
                                 + (item != null ? " (" + item.itemName + ")" : string.Empty) + ": " + (busy ? "nothing is started, another screen is open" : "it is started") + ".");
                }
                if (busy)
                {
                    return;
                }
                _clicked = wish == HomeWish.Coffee ? item : null;
                Act(wish, false);
                _clicked = null;
            }
            catch (Exception e)
            {
                _clicked = null;
                Logger?.Error(e);
            }
        }

        /// <summary>Asks for a routine as soon as nothing is in the way (the phone put down, the conversation over).</summary>
        public void Wish(HomeWish wish)
        {
            _wish = wish;
            _wishUntil = Time.unscaledTime + 20f;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Home: HomeDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
                float now = Time.unscaledTime;
                if (_later != null && Time.frameCount >= _laterFrame)
                {
                    Action what = _later;
                    _later = null;
                    what();
                }
                if (_plan != null)
                {
                    TryCook(now);
                }
                if (_run.Active)
                {
                    PlayerController player = PlayerHelper.PlayerController;
                    if (player == null)
                    {
                        _run.End("reallife_home_run_ended_left");
                    }
                    else
                    {
                        if (Time.time >= _nextRunTick)
                        {
                            _nextRunTick = Time.time + 0.25f;
                            _run.Tick(player);
                            _run.Mark();
                        }
                    }
                }
                if (_activity != null && _activity.SkipAt > 0f && now >= _activity.SkipAt)
                {
                    _activity.SkipAt = -1f;
                    var uis = InstanceBehavior<UIs>.Instance;
                    if (_activity.Running && uis != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, _activity) && !uis.timeMachine.isRunning)
                    {
                        uis.playerActivityUI.ForceTimeMachine();   // the game's own time skip, as its bed starts one
                    }
                }
                if (now >= _nextSlow)
                {
                    _nextSlow = now + 1f;
                    Slow(now);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Home: switched off for this session after the error above; nothing of its own is left on the player.");
                _broken = true;
                TakeBack();
                HomeHealth.Reset();
            }
        }

        // Everything of ours off the player and the screen. Never throws.
        private void TakeBack()
        {
            try
            {
                HomeOffers.Remove();
                HomeFridgeButton.Remove();
                Body?.Release();
                _run?.End("reallife_home_run_ended_left");
                HideDoctor();
                HomeNeedsBar.Hide();
                _activity = null;
                _later = null;
                _wish = HomeWish.None;
                _starting = null;
                _plan = null;
                _offersTold = false;
                HomeCookList.Close("home life was switched off or the city is unloading");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Slow(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (gm == null || gm.playerController == null || save == null || LoadScene.isLoading)
            {
                return;
            }
            PlayerController player = gm.playerController;
            if (!HomeState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                HomeState.Load(save.modData);
                HomeState.NewDay(TimeHelper.CurrentDay);
                HomePhone.Setup();
                _lastMinutes = TimeHelper.NowInMinutes();
                _lastHour = TimeHelper.CurrentHour;
                _lastHunger = save.Hunger;
                _doing = Doing.Nothing;
                Logger?.Info("Home: ready. Hygiene " + (int)HomeState.Hygiene + ", stress " + (int)HomeState.Stress + ", fitness " + (int)HomeState.Fitness
                             + ", illness " + HomeState.Ill + ", home " + (HomeTidy.HasHome ? "yes" : "none") + ".");
            }

            // The game clears its event hubs on every load, so the hooks are kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // Core/SaveHook: nothing is allocated while the game's event is unchanged
            _onGameEvent = _onGameEvent ?? Guard.Wrap<string>(OnGameEvent);   // made once
            EventHold.Keep(ref GameEvent.onGameEventTriggered, _onGameEvent, ref _gameEventSeen);

            float minutesNow = TimeHelper.NowInMinutes();
            float minutes = minutesNow - _lastMinutes;
            _lastMinutes = minutesNow;
            if (minutes < 0f || minutes > 3f * 24f * 60f)
            {
                minutes = 0f;
            }
            if (!Settings.HomeLife)
            {
                if (_wasOn)
                {
                    _wasOn = false;   // switched off on the options page: nothing of ours stays on the player, the screen or their energy
                    TakeBack();
                    HomeHealth.Reset();
                }
                _doing = Doing.Nothing;
                _lastHunger = save.Hunger;
                return;
            }

            _wasOn = true;
            Body.Bind(player);
            if (_activity != null)
            {
                var ui = InstanceBehavior<UIs>.Instance;
                if (_activity.Over || ui == null || !ReferenceEquals(ui.playerActivityUI.GetCurrentActivity, _activity))
                {
                    _activity = null;   // finished, or called off on the way there
                }
            }

            bool atHome = HomeTidy.AtHome();
            bool inside = BuildingManager.IsInsideBuilding;
            bool onFootOutside = !inside && !PlayerHelper.IsUsingVehicle && !UndergroundParkingManager.IsInsideParking;
            Doing doing = ReadDoing(player);
            Sleep(doing, minutes, atHome);
            _doing = doing;
            HomeHealth.Advance(minutes, doing, atHome, onFootOutside, _run.Active ? _run.Boost : 1f);
            _lastHunger = save.Hunger;

            int hour = TimeHelper.CurrentHour;
            if (hour != _lastHour)
            {
                _lastHour = hour;
                int passed = HomeState.NewDay(TimeHelper.CurrentDay);
                if (passed > 0)
                {
                    HomeHealth.NewDay(passed);
                }
                HomeTidy.Read();
                HomeHealth.Hourly(atHome, inside && !atHome && !GoingPublic.Mine(InstanceBehavior<BuildingManager>.Instance.buildingRegistration));
            }

            if (HomeCookList.IsOpen && (!atHome || !Settings.HomeRoutines || PlayerHelper.IsUsingVehicle || FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen
                                        || PlayerActivityUI.IsPanelOpen))
            {
                HomeCookList.Close(!atHome ? "the player left home" : "another screen came up");
            }
            bool free = Free(player);
            if (_wish != HomeWish.None)
            {
                if (free || _wish == HomeWish.StopRun)
                {
                    HomeWish wanted = _wish;
                    _wish = HomeWish.None;
                    Act(wanted, false);
                    free = false;
                }
                else if (now > _wishUntil)
                {
                    Refuse("reallife_home_refused_busy", _wish.ToString(), "waited 20 s");
                    _wish = HomeWish.None;
                }
            }
            Offer(player, free && !_run.Active, atHome);
            Strip(atHome);
        }

        private Doing ReadDoing(PlayerController player)
        {
            if (_run.Active)
            {
                return _run.Moving ? Doing.Jogging : Doing.Nothing;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            IPlayerActivity activity = uis != null && uis.playerActivityUI != null ? uis.playerActivityUI.GetCurrentActivity : null;
            if (activity == null)
            {
                return Doing.Nothing;
            }
            PlayerActivityState state = activity.GetState();
            if (state != PlayerActivityState.Running && state != PlayerActivityState.Started)
            {
                return Doing.Nothing;
            }
            switch (activity)
            {
                case HomeActivity own: return own.Kind;
                case SleepActivity _: return Doing.Sleeping;
                case HygieneActivity _:
                {
                    EntityController used = player.Character != null ? player.Character.CurrentEntityController : null;
                    return used is ToiletController ? Doing.Toilet : used is SinkController ? Doing.Washing : Doing.Showering;
                }
                case EntertainActivity _:
                {
                    Animator animator = player.Character != null ? player.Character.animator : null;
                    if (_readingKnown == 0 && animator != null)
                    {
                        _readingKnown = animator.HasParameter(ReadingParameter) ? 1 : 2;
                    }
                    return _readingKnown == 1 && animator != null && animator.GetBool(ReadingParameter) ? Doing.Reading : Doing.Screen;
                }
                case RestActivity _: return Doing.Resting;
                case WorkoutActivity _: return Doing.Workout;
                case SwimmingActivity _:
                case TennisActivity _:
                case GolfActivity _: return Doing.Sport;
                case WorkActivity _: return Doing.Working;
                case StudyActivity _: return Doing.Studying;
                default: return Doing.Nothing;
            }
        }

        private void Sleep(Doing doing, float minutes, bool atHome)
        {
            if (doing == Doing.Sleeping)
            {
                if (_doing != Doing.Sleeping)
                {
                    _slept = 0f;
                    _bedHour = TimeHelper.CurrentHour;
                    _bedCar = PlayerHelper.IsUsingVehicle;
                    _bedHome = atHome;
                    _bedQuality = 50;
                    try
                    {
                        PlayerController player = PlayerHelper.PlayerController;
                        var bed = (MouseController.currentTargetEntity as BedController)
                                  ?? (player != null && player.Character != null ? player.Character.CurrentEntityController as BedController : null);
                        if (bed != null && !string.IsNullOrEmpty(bed.itemName))
                        {
                            _bedQuality = ItemsGetter.GetByName(bed.itemName).quality;
                        }
                    }
                    catch (Exception)
                    {
                        _bedQuality = 50;   // a bed the game has no data for is an ordinary bed
                    }
                }
                _slept += minutes;
                return;
            }
            if (_doing != Doing.Sleeping || !Settings.HomeSleep)
            {
                return;
            }
            float hours = _slept / 60f;
            _slept = 0f;
            HomeHealth.Slept(hours, _bedQuality, _bedCar, _bedHour, _bedHome);
            int day = TimeHelper.CurrentDay;
            if (hours >= 3f && Settings.HomeMorning && HomeState.MorningDay != day)
            {
                HomeState.MorningDay = day;
                HomeReport.Morning(hours);
            }
        }

        private static bool Free(PlayerController player)
        {
            return LifeEvents.IsSafeForEvent() && !ThirdPersonCamera.IsUiOpen() && !PlayerHelper.IsUsingVehicle && !JobDirector.OnShift
                   && !player.NavigationDisabled && !GameManager.IsAnyMiniGameActive();
        }

        private Action _onSave;   // made once: Core/SaveHook compares it

        private void OnSaveGame()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                if (save == null || !HomeState.Loaded)
                {
                    return;
                }
                if (save.modData == null)
                {
                    save.modData = new System.Collections.Generic.Dictionary<string, string>();
                }
                HomeState.Save(save.modData);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The game says "food eaten" for anything that fills: what it was is told from where it was eaten.
        private Action<string> _onGameEvent;
        private Action<string> _gameEventSeen;

        private static readonly int HotOnGameEvent = HotSpots.Slot("Home: HomeDirector.OnGameEvent");
        private void OnGameEvent(string gameEvent)
        {
            using var hotSpot = HotSpots.Time(HotOnGameEvent);
            if (gameEvent != "ba:gameevent_foodeaten" || !HomeState.Loaded || _broken)
            {
                return;
            }
            try
            {
                GameInstance save = SaveGameManager.Current;
                float rise = save.Hunger - _lastHunger;
                _lastHunger = save.Hunger;
                Meal own = OwnMeal;
                OwnMeal = Meal.Skipped;
                if (own != Meal.Skipped)
                {
                    HomeState.Ate(own);
                }
                else if (rise >= HomeBalance.MealAtLeast)
                {
                    HomeState.Ate(HomeTidy.AtHome() ? Meal.Plain : Meal.Junk);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
