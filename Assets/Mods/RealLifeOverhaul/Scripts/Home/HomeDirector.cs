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
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Something the player asked for from the phone, a click on the furniture, a button of the game's panel or a test button.</summary>
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
    /// Home life and health: the one component that runs them. Once a second it reads what the
    /// player is doing off the game's own activity panel (sleeping, showering, watching
    /// television, on a workout machine...) and moves the needs, fitness and sleep debt by the
    /// game minutes that went by; on the hour and at midnight it settles mood, illness and
    /// diet; and it offers the mod's own routines (cooking, coffee, tidying, floor exercises,
    /// the doctor) at the furniture they need, on the phone and from the options page.
    ///
    /// Everything it shows is the game's own interface: a click on a cooker or a coffee machine
    /// (<see cref="HomeOffers"/>), a button in the fridge's panel (<see cref="HomeFridgeButton"/>),
    /// the game's list for the dishes (<see cref="HomeCookList"/>), three more bars under the
    /// game's needs (<see cref="HomeNeedsBar"/>), the game's panel over a place at the doctor's
    /// desk (GameUi.WorldPanel), and the game's activity panel for every routine. It reads no key.
    ///
    /// Per frame while nothing is on: one time compare. A run is ticked four times a second,
    /// and its marker is the game's own. A fault anywhere switches the
    /// whole feature off for the session with everything of its own taken back.
    /// </summary>
    public class HomeDirector : MonoBehaviour
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
        private readonly System.Collections.Generic.List<PantryItem> _pantry = new System.Collections.Generic.List<PantryItem>(16);
        private Action _later;
        private int _laterFrame;
        private HomeWish _wish;
        private float _wishUntil;
        private ItemController _clicked;     // the coffee machine a click was on: the cup is made at that one
        private readonly GameUi.WorldPanel _doctor = new GameUi.WorldPanel();
        private Action _onDoctor;
        private Buildings.Building _doctorIn;   // the hospital the desk was looked for in
        private Vector3 _doctorAt;
        private bool _doctorDesk;
        private int _doctorKey = int.MinValue;
        private int _doctorState;            // 0 not up, 1 up, 2 refused by the game's own panel
        private int _keyFrame = -1;
        private bool _offersTold;
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
        private int _noteCode = -1;
        private string _note;

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

        /// <summary>
        /// True while the game's panel at the doctor's desk is up with its button on the game's interact key, and in
        /// the frame that button ran: the mod's own interact key does nothing else then (World/NearbyInteraction).
        /// </summary>
        public static bool TakesInteractKey => Instance != null && (Instance._doctor.Showing || Instance._keyFrame == Time.frameCount);

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            GameEvent.onGameEventTriggered -= _onGameEvent;   // (the handler that was put on: Guard.Wrap's, not the method itself)
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

        /// <summary>
        /// The game ran a click of the mod's: "Click to cook" on a cooker, "Click to make a coffee" on a coffee
        /// machine, or "Cook a meal" in a fridge's panel.
        /// </summary>
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
                    Logger?.Info("Home check (choice): the player clicks " + (wish == HomeWish.Coffee ? "a coffee machine" : item is FridgeController ? "'Cook a meal' in a fridge's panel" : "a cooker")
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

        private void Update()
        {
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
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

        // ---------------------------------------------------------------- once a second

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
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed
            _onGameEvent = _onGameEvent ?? Guard.Wrap<string>(OnGameEvent);   // made once, and put back only when the game's event is not the one last seen
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

        // What the player is doing, from the game's own activity panel.
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

        // A sleep from its first second to its last: how long, in what, from what hour.
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

        // ---------------------------------------------------------------- what is offered, and the bars

        private const float DoctorReach = 7f;        // metres from the doctor's desk at which the game's panel is offered
        private const string ButtonDoctor = "reallife_home_button_doctor";

        // At home the game offers a click on the cookers and coffee machines and a button in a fridge's panel; in a
        // hospital its panel over a place stands at the doctor's desk. Once a second: a few compares unless something changed.
        private void Offer(PlayerController player, bool free, bool atHome)
        {
            if (Settings.HomeRoutines && atHome)
            {
                HomePlace.Scan();
                bool clicks = HomeOffers.Mark(HomePlace.Cookers, HomePlace.CoffeeMachines);
                bool button = HomeFridgeButton.Install();
                if (!_offersTold && Meta.Dev.Verbose)
                {
                    _offersTold = true;
                    Logger?.Info("Home check (offer): at home with " + HomePlace.Cookers.Count + " cooker(s), " + HomePlace.CoffeeMachines.Count + " coffee machine(s) and "
                                 + HomePlace.Fridges.Count + " fridge(s): click actions " + (clicks ? "on" : "NOT on") + ", the fridge panel's button " + (button ? "in" : "NOT in") + ": "
                                 + (clicks && button ? "PASS" : "FAIL") + ".");
                }
            }
            else if (!atHome)
            {
                _offersTold = false;
            }
            if (free && Settings.HomeIllness && HomePlace.InHospital())
            {
                Doctor(player);
                return;
            }
            HideDoctor();
            if (!BuildingManager.IsInsideBuilding)
            {
                _doctorIn = null;
            }
        }

        // The game's panel over a place, at the doctor's desk (or, in a clinic without one, where the player came in).
        private void Doctor(PlayerController player)
        {
            Vector3 at = player.transform.position;
            Buildings.Building building = InstanceBehavior<BuildingManager>.Instance.building;
            if (!ReferenceEquals(building, _doctorIn))
            {
                _doctorIn = building;
                _doctorDesk = HomePlace.DoctorDesk(out _doctorAt);
                if (!_doctorDesk)
                {
                    _doctorAt = at;
                }
                _doctorKey = int.MinValue;
            }
            if (CitySpots.Flat(_doctorAt - at) > DoctorReach * DoctorReach || Mathf.Abs(_doctorAt.y - at.y) > 3f)
            {
                HideDoctor();
                return;
            }
            bool ill = HomeHealth.Ill || ModState.IsSick;
            int key = (ill ? 1 : 0) | ((int)HomeState.Ill << 1);
            if (key != _doctorKey)
            {
                _doctorKey = key;
                _doctor.Lines(JobText.T("reallife_home_doctor_title"),
                    JobText.T(HomeHealth.Ill ? HomeHealth.NameKey(HomeState.Ill) : ill ? "reallife_home_doctor_unwell" : "reallife_home_doctor_checkup"),
                    JobText.Money(HomeBalance.ConsultationFee), GameUi.WorldPanel.White());
                _doctor.Buttons(ButtonDoctor, _onDoctor, true, null, null, false);
            }
            // Over the desk; over the player's own head while the desk is behind the camera.
            Vector3 over = _doctorAt + Vector3.up * (_doctorDesk ? 2.2f : 2.1f);
            Camera camera = GameManager.GetMainCamera();
            if (camera != null && Vector3.Dot(over - camera.transform.position, camera.transform.forward) < 0.5f)
            {
                over = at + Vector3.up * 2.1f;
            }
            _doctor.Position = over;
            int state = _doctor.Show() ? 1 : 2;
            if (state != _doctorState)
            {
                _doctorState = state;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (doctor panel): " + Mathf.Sqrt(CitySpots.Flat(_doctorAt - at)).ToString("F1") + " m from " + (_doctorDesk ? "the doctor's desk" : "where the player came in (no desk found)")
                                 + ", offered within " + DoctorReach.ToString("F0") + " m: " + (state == 1 ? "the game's own panel is up (" + (ill ? "ill" : "well") + ", fee " + HomeBalance.ConsultationFee + "): PASS"
                                     : "FAIL (the game's panel is showing another place, a lift; it is tried again while the player stands here)") + ".");
                }
            }
        }

        private void HideDoctor()
        {
            if (_doctorState != 0)
            {
                bool was = _doctorState == 1;
                _doctorState = 0;
                _doctor.Hide();
                if (was && Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (doctor panel): closed (its button was used, the player walked away or left, or another screen is open).");
                }
            }
        }

        // The panel's button, on the game's interact key or a click.
        private void DoctorClicked()
        {
            try
            {
                _keyFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (choice): 'See a doctor' at the doctor's panel.");
                }
                HideDoctor();
                Act(HomeWish.Doctor, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Strip(bool atHome)
        {
            bool show = Settings.HomeStrip && Settings.HomeNeeds && (atHome || HomeState.Hygiene < 35f || HomeState.Stress > 65f || HomeHealth.Ill);
            if (!show)
            {
                HomeNeedsBar.Hide();
                return;
            }
            string note = null;
            if (HomeHealth.Ill)
            {
                int hoursLeft = Mathf.Max(0, Mathf.CeilToInt((HomeState.IllUntil - TimeHelper.NowInMinutes()) / 60f));
                int code = (int)HomeState.Ill * 100000 + hoursLeft * 2 + (HomeState.IllTreated ? 1 : 0);
                if (code != _noteCode)
                {
                    _noteCode = code;
                    _note = JobText.T("reallife_home_strip_ill", "illness", JobText.T(HomeHealth.NameKey(HomeState.Ill)), "days", HomeHealth.Days(hoursLeft * 60f));
                }
                note = _note;
            }
            HomeNeedsBar.Show(HomeState.Hygiene, HomeState.Stress, HomeState.Tidy, HomeTidy.HasHome, note);
        }

        // ---------------------------------------------------------------- the game's events

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

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
        private System.Action<string> _onGameEvent;
        private System.Action<string> _gameEventSeen;

        private void OnGameEvent(string gameEvent)
        {
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

        // ---------------------------------------------------------------- starting things

        /// <summary>A toast and a log line for something that could not be started: one sentence per cause.</summary>
        public static void Refuse(string key, string what, string detail = null)
        {
            Logger?.Info("Home: '" + what + "' not started (" + key + (detail != null ? "; " + detail : string.Empty) + ").");
            Notifications.Show(NotificationType.Warning, key, null, 8f);
        }

        private void Act(HomeWish wish, bool test)
        {
            string what = wish.ToString();
            var gm = InstanceBehavior<GameManager>.Instance;
            PlayerController player = gm != null ? gm.playerController : null;
            if (!HomeState.Loaded || player == null || SaveGameManager.Current == null)
            {
                TestRefusals.Tell(Logger, "Home " + what, TestRefusal.NotLoaded);
                return;
            }
            if (!Settings.HomeLife || _broken)
            {
                TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff, 0, _broken ? "switched off after an error" : null);
                return;
            }
            if (wish == HomeWish.StopRun)
            {
                if (_run.Active)
                {
                    _run.End("reallife_home_run_ended_stopped");
                }
                return;
            }
            if (JobDirector.OnShift)
            {
                Refuse("reallife_home_refused_on_job", what);
                return;
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                Refuse("reallife_home_refused_vehicle", what);
                return;
            }
            if (_run.Active)
            {
                Refuse("reallife_home_refused_running", what);
                return;
            }
            if (!Free(player))
            {
                Refuse("reallife_home_refused_busy", what);
                return;
            }
            Vector3 at = player.transform.position;
            float energy = SaveGameManager.Current.Energy;
            switch (wish)
            {
                case HomeWish.Cook:
                case HomeWish.CookFirst:
                {
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                        return;
                    }
                    if (!HomeTidy.AtHome())
                    {
                        Refuse("reallife_home_refused_not_home", what);
                        return;
                    }
                    HomePlace.Scan(true);
                    if (wish == HomeWish.Cook)
                    {
                        // The list says for itself what is missing: no fridge, nothing in it, no cooker.
                        string found = HomeCookList.Open(this);
                        if (found != null)
                        {
                            Logger?.Info("Home: cooking list opened: " + found);
                            return;
                        }
                        // (The game's window could not be copied, said once in Player.log: the first dish the fridges allow is cooked.)
                    }
                    HomePlace.ReadPantry(_pantry);
                    bool cooker = HomePlace.Cookers.Count > 0;
                    CookPlan first = cooker ? Cooking.Reheat(HomeState.Leftovers, HomeState.LeftoverDish, HomeState.LeftoverFill) : null;
                    for (int d = 0; first == null && d < HomeBalance.Dishes.Length; d++)
                    {
                        if (cooker || !HomeBalance.Dishes[d].Cooker)
                        {
                            first = Cooking.Plan(HomeBalance.Dishes[d], 0, _pantry);
                        }
                    }
                    if (first == null)
                    {
                        Refuse("reallife_home_refused_no_food", what, HomePlace.Fridges.Count + " fridge(s), " + HomePlace.Cookers.Count + " cooker(s)");
                        return;
                    }
                    Cook(first);
                    return;
                }
                case HomeWish.Coffee:
                {
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                        return;
                    }
                    if (!HomeTidy.AtHome())
                    {
                        Refuse("reallife_home_refused_not_home", what);
                        return;
                    }
                    HomePlace.Scan(true);
                    ItemController item = _clicked != null && HomePlace.CoffeeMachines.Contains(_clicked) ? _clicked : HomePlace.Nearest(HomePlace.CoffeeMachines, at, 200f);
                    if (item == null)
                    {
                        Refuse("reallife_home_refused_no_coffee_machine", what);
                        return;
                    }
                    Show(new CoffeeActivity(this, item));
                    return;
                }
                case HomeWish.Tidy:
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (!HomeTidy.AtHome())
                    {
                        Refuse("reallife_home_refused_not_home", what);
                    }
                    else if (HomeTidy.Read() >= 97f)
                    {
                        Refuse("reallife_home_refused_spotless", what);
                    }
                    else
                    {
                        Show(new TidyActivity(this));
                    }
                    return;
                case HomeWish.Stretch:
                case HomeWish.PushUps:
                case HomeWish.SitUps:
                    if (!Settings.HomeRoutines)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (BuildingManager.IsInsideBuilding && !GoingPublic.Mine(InstanceBehavior<BuildingManager>.Instance.buildingRegistration))
                    {
                        Refuse("reallife_home_refused_not_here", what);
                    }
                    else if (wish != HomeWish.Stretch && HomeHealth.TooIllToTrain)
                    {
                        Refuse("reallife_home_refused_ill", what, HomeState.Ill.ToString());
                    }
                    else if (energy < 15f)
                    {
                        Refuse("reallife_home_refused_tired", what, "energy " + (int)energy);
                    }
                    else if (!CanPerform(player, wish))
                    {
                        Refuse("reallife_home_refused_no_animation", what);   // never a routine with nothing to see
                    }
                    else
                    {
                        Show(new ExerciseActivity(this, wish == HomeWish.PushUps ? FloorExercise.PushUps : wish == HomeWish.SitUps ? FloorExercise.SitUps : FloorExercise.Stretch));
                    }
                    return;
                case HomeWish.Run:
                    if (!Settings.HomeJog)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.Indoors);
                    }
                    else if (HomeHealth.TooIllToTrain)
                    {
                        Refuse("reallife_home_refused_ill", what, HomeState.Ill.ToString());
                    }
                    else if (energy < 25f)
                    {
                        Refuse("reallife_home_refused_tired", what, "energy " + (int)energy);
                    }
                    else if (!_run.Begin(player, transform, out string refusal))
                    {
                        Refuse(refusal, what);
                    }
                    else
                    {
                        _nextRunTick = Time.time + 0.25f;
                    }
                    return;
                case HomeWish.Doctor:
                    if (!Settings.HomeIllness)
                    {
                        TestRefusals.Tell(Logger, "Home " + what, TestRefusal.FeatureOff);
                    }
                    else if (test)
                    {
                        DoctorVisit.Free = true;
                        DoctorVisit.Open();
                    }
                    else if (!HomePlace.InHospital())
                    {
                        Refuse("reallife_home_refused_not_hospital", what);
                    }
                    else if (SaveGameManager.Current.Money < HomeBalance.ConsultationFee)
                    {
                        Refuse("reallife_doctor_no_money_toast", what);
                    }
                    else
                    {
                        DoctorVisit.Free = false;
                        Show(new WaitingRoomActivity(this));
                    }
                    return;
            }
        }

        // Whether the player can be seen to do a floor exercise: one of the mod's clips for it, or the game's own animation.
        private bool CanPerform(PlayerController player, HomeWish wish)
        {
            Body.Bind(player);
            Animator animator = player.Character != null ? player.Character.animator : null;
            switch (wish)
            {
                case HomeWish.PushUps: return ClipLibrary.Has("pushup") || Body.Knows(BigAmbitions.Characters.PermanentAnimationType.PushUpIdle);
                case HomeWish.SitUps: return ClipLibrary.Has("situp") || Body.Knows(BigAmbitions.Characters.PermanentAnimationType.Crunches);
                default: return ClipLibrary.Has("stretch") || ClipLibrary.Has("tai_chi") || (animator != null && animator.HasParameter("StretchArms"));
            }
        }

        // Puts a routine on the game's own activity panel. The game may still say no (no energy for an exercise): then nothing is on.
        private void Show(HomeActivity activity, bool startForThePlayer = false)
        {
            bool panelBusy = PlayerActivityUI.IsPanelOpen;   // the game's Show then does nothing and says nothing
            PlayerActivityUI.Show(activity);
            var uis = InstanceBehavior<UIs>.Instance;
            _activity = uis != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity) ? activity : null;
            Logger?.Info("Home: " + activity.GetType().Name + (_activity == null ? " was turned down by the game."
                : startForThePlayer ? " on the game's panel; it starts by itself." : " offered."));
            if (_activity == null)
            {
                if (panelBusy)
                {
                    Refuse("reallife_home_refused_busy", activity.GetType().Name, "the game's activity panel was open");
                }
                return;   // otherwise the game has said why itself (no energy, in a vehicle)
            }
            if (startForThePlayer)
            {
                // Two frames on: the game's panel must have seen the routine waiting once before it sees it under
                // way, or it neither hides itself nor lets the clock run (it compares with the state it saw last).
                _starting = activity;
                Later(StartChosen, 2);
            }
        }

        private void StartChosen()
        {
            HomeActivity activity = _starting;
            _starting = null;
            var uis = InstanceBehavior<UIs>.Instance;
            if (activity != null && uis != null && ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity))
            {
                activity.StartNow();
                activity.CallOff();   // it could not start after all and has said why: its panel is not left standing
            }
        }

        private HomeActivity _dropped;

        /// <summary>
        /// A routine the game's panel dropped as it set off (see HomeActivity.SetOff) is put on the panel again
        /// and started for the player, who has already said yes once.
        /// </summary>
        public void Again(HomeActivity activity)
        {
            _dropped = activity;
            Later(ShowAgain, 1);
        }

        private void ShowAgain()
        {
            HomeActivity activity = _dropped;
            _dropped = null;
            if (activity != null)
            {
                activity.ChangeState(PlayerActivityState.NotStarted);
                Show(activity, true);
            }
        }

        // ---------------------------------------------------------------- cooking

        /// <summary>
        /// A dish was chosen on the cooking list. It starts as soon as the player is free (the list has just
        /// closed; from the options page, when the menus are closed): within 20 seconds, or they are told why not.
        /// </summary>
        public void Cook(CookPlan plan)
        {
            if (plan == null || _broken)
            {
                return;
            }
            _plan = plan;
            _planUntil = Time.unscaledTime + 20f;
        }

        // Per frame only while a dish is waiting to be started.
        private void TryCook(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            PlayerController player = gm != null ? gm.playerController : null;
            string what = _plan.NameKey;
            if (player == null || !Settings.HomeLife || !Settings.HomeRoutines)
            {
                _plan = null;
                return;
            }
            if (!Free(player) || _run.Active || _later != null)
            {
                if (now > _planUntil)
                {
                    _plan = null;
                    Refuse(_run.Active ? "reallife_home_refused_running" : "reallife_home_refused_busy", what, "waited 20 s");
                }
                return;
            }
            CookPlan plan = _plan;
            _plan = null;
            if (!HomeTidy.AtHome())
            {
                Refuse("reallife_home_refused_not_home", what);
                return;
            }
            HomePlace.Scan(true);
            Vector3 at = player.transform.position;
            ItemController where = HomePlace.Nearest(HomePlace.Cookers, at, 200f);
            if (!plan.Cooker)
            {
                where = HomePlace.NearestFridge(at, 200f) ?? where;   // put together at the fridge
            }
            if (where == null)
            {
                Refuse(plan.Cooker ? "reallife_home_refused_no_cooker" : "reallife_home_refused_no_fridge", what);
                return;
            }
            Show(new CookActivity(this, where, plan), true);
        }

        // ---------------------------------------------------------------- the options page

        public void TestRoutine(HomeWish wish)
        {
            try
            {
                // The cooking tests are pressed on the options page, where nothing can start: they wait for the
                // menus to be closed, as what is asked for on the phone does, and say so.
                PlayerController player = PlayerHelper.PlayerController;
                if ((wish == HomeWish.Cook || wish == HomeWish.CookFirst) && HomeState.Loaded && Settings.HomeLife && !_broken
                    && player != null && !Free(player) && !JobDirector.OnShift && !PlayerHelper.IsUsingVehicle && !_run.Active)
                {
                    Wish(wish);
                    Notifications.Show(NotificationType.Info, "reallife_home_test_waits", null, 8f);
                    return;
                }
                Act(wish, true);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public void TestSick(Illness ill)
        {
            if (!Ready("sick"))
            {
                return;
            }
            HomeHealth.Catch(ill, "reallife_health_cause_test", true);
        }

        public void TestCure()
        {
            if (!Ready("cure"))
            {
                return;
            }
            bool was = HomeHealth.Ill || ModState.IsSick;
            HomeHealth.Over(false);
            LifeEvents.Instance?.Cure(null);
            ModState.RemoveModifier(ModState.Injured);
            HomeState.ImmuneUntilDay = 0;
            HomeState.SickNoteUntil = 0f;
            Notifications.Show(NotificationType.Success, was ? "reallife_health_test_cured" : "reallife_health_test_nothing_to_cure", null, 6f);
        }

        public void TestFitness(float change)
        {
            if (!Ready("fitness"))
            {
                return;
            }
            HomeState.Fitness = HomeState.Clamp(HomeState.Fitness + change, 0f, 100f);
            HomeState.ShapeDue += change * 0.002f;   // shown on the body the next time the player wakes
            HomeState.FatDue -= change * 0.002f;
            HomeHealth.Hourly(HomeTidy.AtHome(), false);
            JobText.Toast(NotificationType.Info, "reallife_health_test_fitness", "fitness", Mathf.RoundToInt(HomeState.Fitness).ToString());
        }

        public void TestNeeds(bool low)
        {
            if (!Ready("needs"))
            {
                return;
            }
            HomeState.Hygiene = low ? 8f : 90f;
            HomeState.Stress = low ? 88f : 10f;
            HomeState.SleepDebt = low ? 12f : 0f;
            HomeTidy.Change(low ? -(HomeTidy.Read() - 12f) : 100f);
            HomeTidy.Read();
            HomeHealth.Hourly(HomeTidy.AtHome(), false);
            Notifications.Show(NotificationType.Info, low ? "reallife_health_test_needs_low" : "reallife_health_test_needs_full", null, 6f);
        }

        public void TestPage()
        {
            if (!Ready("page"))
            {
                return;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !Free(player))
            {
                Refuse("reallife_home_refused_busy", "page");
                return;
            }
            HomePhone.OpenInPerson();
        }

        private bool Ready(string test)
        {
            if (!HomeState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, "Home " + test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.HomeLife || _broken)
            {
                TestRefusals.Tell(Logger, "Home " + test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }
    }
}
