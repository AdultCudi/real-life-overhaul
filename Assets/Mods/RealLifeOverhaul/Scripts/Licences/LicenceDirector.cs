using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Driving licences: the one component that runs them. It loads and saves the record
    /// (LicenceState), grants the licences an existing save is owed the first time it runs,
    /// finds the driving school's desks, runs the one written or practical test that may be on,
    /// and ticks enforcement (points, the licence asked for at a stop, the routine roadside
    /// check).
    /// Cost: a handful of comparisons per frame; once a second the desks and the school's car;
    /// four times a second enforcement while out of doors. Per-frame work only while a test is
    /// on. Anything unexpected is logged once, whatever was out is taken back, and licences stay
    /// as they are for the rest of the session with nothing further staged.
    /// </summary>
    public class LicenceDirector : MonoBehaviour
    {
        private const string FeeTransaction = "reallife_transaction_licence_fee";

        public static IModLogger Logger;
        public static LicenceDirector Instance { get; private set; }

        /// <summary>True while the interaction key is ours: at a desk, or while a test shows a prompt. The mod's own enter-a-door key stands aside.</summary>
        public static bool TakesInteractKey =>
            Instance != null && (Instance._interactFrame == Time.frameCount || Instance._prompting || Instance._theory.Active
                                 || Instance._practical.Prompting || (LicenceHud.Exists && LicenceHud.Instance.SheetShown));

        /// <summary>True while a written or practical test (or a lesson) is on. Other large scenes may want to wait.</summary>
        public static bool IsActive => Instance != null && (Instance._theory.Active || Instance._practical.Active);

        private readonly TheoryTest _theory = new TheoryTest();
        private PracticalTest _practical;
        private TestCar _car;
        private LicenceDesks _desks;
        internal LicenceDesks Desks => _desks;   // read only, for the city map (World/MapLinks)
        private DrivingEnforcement _enforcement;
        private JobCast _cast;

        private float _nextSlow;
        private float _nextFast;
        private bool _broken;
        private bool _prompting;
        private string _promptText;
        private int _promptKeys = -1;
        private int _interactFrame = -1;
        private int _lastDay = -1;
        private bool _pending;
        private SchoolService _pendingService;
        private int _pendingFee;
        private Vector3 _pendingDesk;
        private float _pendingSince;
        private float _cardUntil;
        private VehicleController _lastDriven;
        private float _nextWarning;

        // Firearms were taken out of the mod in 0.37: what a save still carries of them (one modData line, one phone contact) is removed once.
        private void ForgetFirearms(GameInstance save)
        {
            const string contactId = "reallife-firearms-unit";
            bool data = save.modData != null && save.modData.Remove("reallife:firearms");
            bool contact = false;
            if (save.Contacts != null)
            {
                for (int i = save.Contacts.Count - 1; i >= 0; i--)
                {
                    if (save.Contacts[i] != null && (save.Contacts[i].id == contactId || save.Contacts[i].id == "reallife-gunshop"))
                    {
                        save.Contacts.RemoveAt(i);
                        contact = true;
                    }
                }
            }
            if (contact && save.PlayerDefaults != null && (save.PlayerDefaults.contactsLastName == contactId || save.PlayerDefaults.contactsLastName == "reallife-gunshop"))
            {
                save.PlayerDefaults.contactsLastName = null;   // the game's Contacts app shows an empty list for a contact that is gone
            }
            if (data || contact)
            {
                Logger?.Info("Licences: this save's firearms data was removed (the feature is no longer part of the mod).");
            }
        }

        private void Awake()
        {
            Instance = this;
            LicenceState.Reset();
            _car = new TestCar(this, Logger);
            _practical = new PracticalTest(_car, Cast, transform, Logger);
            _desks = new LicenceDesks(transform, Logger);
            _enforcement = new DrivingEnforcement(this, Logger, IsSchoolCar);
            _enforcement.Attach();
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                _enforcement.Detach();
                if (_theory.Abort())
                {
                    Refund();
                }
                _practical.Abort(null, refund: false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            LicenceHold.Free();
            _car.Dispose();
            _desks.Dispose();
            if (_cast != null)
            {
                _cast.Dispose();
            }
            LicenceState.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private JobCast Cast()
        {
            return _cast ?? (_cast = new JobCast(this, Logger));
        }

        private bool IsSchoolCar(VehicleController vehicle)
        {
            return vehicle != null && _car != null && _car.Out && ReferenceEquals(_practical.Car, vehicle);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (_broken)
            {
                if (now >= _nextSlow)
                {
                    _nextSlow = now + 1f;
                    PlayerController alone = PlayerHelper.PlayerController;
                    if (alone != null)
                    {
                        _car.Tick(alone.transform.position);   // a car that was out is still seen back
                    }
                }
                return;
            }
            try
            {
                if (now >= _nextSlow)
                {
                    _nextSlow = now + 1f;
                    Slow(now);
                }
                if (!LicenceState.Loaded)
                {
                    return;
                }
                if (_theory.Active)
                {
                    _theory.Frame();
                    return;
                }
                if (_practical.Active)
                {
                    if ((_practical.Prompting || (LicenceHud.Exists && LicenceHud.Instance.SheetShown)) && JobKeys.InteractDown() && !ThirdPersonCamera.IsUiOpen())
                    {
                        _interactFrame = Time.frameCount;
                        _practical.Interact(Time.time);
                    }
                    _practical.Frame(Time.time);
                }
                else if (_prompting && JobKeys.InteractDown() && !ThirdPersonCamera.IsUiOpen())
                {
                    _interactFrame = Time.frameCount;
                    OpenDesk();
                }
                else if (_cardUntil > 0f && (now >= _cardUntil || JobKeys.InteractDown()))
                {
                    _cardUntil = 0f;
                    _interactFrame = Time.frameCount;
                    LicenceHud.Instance.HideSheet();
                }
                if (_pending && now - _pendingSince > 0.3f)
                {
                    StartPending(now);
                }
                if (now >= _nextFast)
                {
                    _nextFast = now + 0.25f;
                    if (TrafficTools.Ready)
                    {
                        _enforcement.Tick(Time.time);
                    }
                    else if (_enforcement.Checking)
                    {
                        _enforcement.ClearCheck();
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Licences: the driving school is off for this session after the error above; whatever was out was taken back. Licences already held still count.");
                _broken = true;
                Shutdown();
            }
        }

        private void Shutdown()
        {
            try
            {
                if (_theory.Abort() || _pending)
                {
                    Refund();
                }
                _pending = false;
                _practical.Abort("reallife_lic_test_interrupted", refund: true);
                _enforcement.Detach();
                _desks.Dispose();
                LicenceHold.Free();
                if (LicenceHud.Exists)
                {
                    LicenceHud.Instance.HideActivity();
                    LicenceHud.Instance.HidePrompt();
                }
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
            if (gm == null || gm.playerController == null || SaveGameManager.Current == null || LoadScene.isLoading)
            {
                return;
            }
            if (!LicenceState.Loaded)
            {
                if (!ModState.Loaded)
                {
                    return;   // LifeEvents makes the save's modData ready first
                }
                LicenceState.Load(SaveGameManager.Current.modData);
                _car.Load(SaveGameManager.Current.modData);
                FirstRun();
                LicencePhone.Setup();
                ForgetFirearms(SaveGameManager.Current);
                _lastDay = TimeHelper.CurrentDay;
                Logger?.Info("Licences: ready. Car " + LicenceState.Standing(LicenceClass.Car, _lastDay) + ", commercial " + LicenceState.Standing(LicenceClass.Commercial, _lastDay)
                             + ", taxi " + LicenceState.Standing(LicenceClass.Taxi, _lastDay) + ", " + LicenceState.PointsOn(_lastDay) + " points.");
            }

            // The game clears its event hub on every load, so the save hook is kept attached.
            SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed

            if (!LicenceState.MotorcycleDecided)
            {
                GrandfatherMotorbike();
            }

            int day = TimeHelper.CurrentDay;
            if (day != _lastDay)
            {
                _lastDay = day;
                NewDay(day);
            }

            Vector3 player = gm.playerController.transform.position;
            _car.Tick(player);

            // Getting into something they hold no licence for: said once as they sit down, never policed by magic.
            VehicleController driven = PlayerHelper.IsUsingVehicle ? gm.selectedVehicle : null;
            if (!ReferenceEquals(driven, _lastDriven))
            {
                _lastDriven = driven;
                if (driven != null && Settings.DrivingLicence && now >= _nextWarning && !IsSchoolCar(driven)
                    && Licences.Driving.ClassOf(driven, out LicenceClass needs) && !LicenceState.Valid(needs, day))
                {
                    _nextWarning = now + 90f;
                    LicenceStanding standing = LicenceState.Standing(needs, day);
                    Licences.Toast(NotificationType.Warning,
                        standing == LicenceStanding.Suspended ? "reallife_lic_warn_suspended"
                        : standing == LicenceStanding.Expired ? "reallife_lic_warn_expired" : "reallife_lic_warn_none",
                        "class", Licences.ClassName(needs), 10f);
                }
            }

            bool onFoot = !BuildingManager.IsInsideBuilding && gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle;
            if (!Settings.DrivingLicence || !Settings.DrivingSchool || !onFoot || IsActive || _pending)
            {
                _desks.Away();
            }
            else
            {
                _desks.Tick(player);
            }
            bool prompting = _desks.At >= 0 && !ThirdPersonCamera.IsUiOpen() && _cardUntil <= 0f;
            if (prompting != _prompting || (prompting && _promptKeys != Keys.Version))
            {
                _prompting = prompting;
                if (prompting)
                {
                    if (_promptText == null || _promptKeys != Keys.Version)
                    {
                        _promptKeys = Keys.Version;
                        _promptText = Licences.T("reallife_school_prompt", "key", Keys.Label(KeyAction.Interact));
                    }
                    LicenceHud.Instance.ShowPrompt(_promptText);
                }
                else if (LicenceHud.Exists && !_practical.Active)
                {
                    LicenceHud.Instance.HidePrompt();
                }
            }
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                if (LicenceState.Loaded && SaveGameManager.Current != null)
                {
                    if (SaveGameManager.Current.modData == null)
                    {
                        SaveGameManager.Current.modData = new Dictionary<string, string>();
                    }
                    LicenceState.Save(SaveGameManager.Current.modData);
                    _car.BeforeSave(SaveGameManager.Current.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The first time the rules run on a save: whoever already owns a vehicle already drives, and keeps doing so lawfully.
        private void FirstRun()
        {
            if (LicenceState.FirstDay > 0)
            {
                return;
            }
            int day = Mathf.Max(1, TimeHelper.CurrentDay);
            LicenceState.FirstDay = day;
            bool ownsCar = false;
            bool ownsCommercial = false;
            List<VehicleInstance> all = SaveGameManager.Current.VehicleInstances;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                VehicleInstance instance = all[i];
                if (instance == null || instance is DeliveryVehicleInstance)
                {
                    continue;   // the game's job van is not theirs
                }
                VehicleType type = SafeType(instance.vehicleTypeName);
                if (type == null || !type.IsMotorVehicle || type.spawnInPlayerObject)
                {
                    continue;
                }
                CarKind kind = CarCatalogue.For(type.vehicleTypeName, new CarTraits(true, false, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, 1400f)).Kind;
                if (kind == CarKind.Van || kind == CarKind.Truck)
                {
                    ownsCommercial = true;
                }
                else if (kind != CarKind.Untouched)
                {
                    ownsCar = true;
                }
            }
            if (ownsCar || ownsCommercial)
            {
                LicenceState.Grant(LicenceClass.Car, day);
                if (ownsCommercial)
                {
                    LicenceState.Grant(LicenceClass.Commercial, day);
                }
                if (Settings.DrivingLicence)
                {
                    Licences.Toast(NotificationType.Success, ownsCommercial ? "reallife_lic_grandfather_both" : "reallife_lic_grandfather_car",
                        "days", LicenceLaw.ValidDays.ToString(), 14f);
                }
                Logger?.Info("Licences: first run on a save that owns a " + (ownsCommercial ? "commercial vehicle" : "car") + ": licence(s) granted.");
            }
            else if (Settings.StartWithLicence)
            {
                LicenceState.Grant(LicenceClass.Car, day);
                if (Settings.DrivingLicence)
                {
                    Licences.Toast(NotificationType.Success, "reallife_lic_start_with", "days", LicenceLaw.ValidDays.ToString(), 12f);
                }
                Logger?.Info("Licences: first run with 'Start with a driving licence' on: car licence granted.");
            }
            else
            {
                if (Settings.DrivingLicence)
                {
                    Licences.Toast(NotificationType.Info, "reallife_lic_first_run", 14f);
                }
                Logger?.Info("Licences: first run on a save with no vehicle: the player starts without a licence.");
            }
        }

        // A save from before the motorcycle class (or a new one): whoever already owns a motorbike already rides, and keeps doing so
        // lawfully, once. The Garage reads its list a little after this feature is ready, so this is asked once a second until it answers.
        private void GrandfatherMotorbike()
        {
            int owns;
            try
            {
                owns = Licences.Driving.OwnsMotorbike != null ? Licences.Driving.OwnsMotorbike() : -1;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                owns = 0;
            }
            if (owns < 0)
            {
                return;
            }
            LicenceState.MotorcycleDecided = true;
            if (owns == 0)
            {
                return;
            }
            int day = TimeHelper.CurrentDay;
            if (!LicenceState.Of(LicenceClass.Motorcycle).Held)
            {
                LicenceState.Grant(LicenceClass.Motorcycle, day);
                if (Settings.DrivingLicence)
                {
                    Licences.Toast(NotificationType.Success, "reallife_lic_grandfather_motorcycle", "days", LicenceLaw.ValidDays.ToString(), 14f);
                }
                Logger?.Info("Licences: the save owns a motorbike: motorcycle licence granted.");
            }
        }

        // A vehicle type the game no longer knows (a removed mod's car) must not stop the first run.
        private static VehicleType SafeType(string name)
        {
            try
            {
                return string.IsNullOrEmpty(name) ? null : VehicleTypeHelper.GetVehicleType(name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void NewDay(int day)
        {
            if (LicenceState.NewDay(day))
            {
                Licences.Toast(NotificationType.Success, "reallife_lic_suspension_over", 10f);
            }
            if (!Settings.DrivingLicence)
            {
                return;
            }
            for (int i = 0; i < DrivingSchool.Classes; i++)
            {
                var kind = (LicenceClass)i;
                LicenceState.Card card = LicenceState.Of(kind);
                if (!card.Held)
                {
                    continue;
                }
                int left = card.Expires - day;
                if (left == LicenceLaw.RenewFromDays || left == 3)
                {
                    Licences.Toast(NotificationType.Warning, "reallife_lic_renew_soon", "class", Licences.ClassName(kind), "days", left.ToString(), 10f);
                }
                else if (left == -1)
                {
                    Licences.Toast(NotificationType.Error, "reallife_lic_expired", "class", Licences.ClassName(kind), "days", LicenceLaw.LapsedAfterDays.ToString(), 12f);
                }
            }
        }

        // ---------------------------------------------------------------- the desk

        /// <summary>The address of the desk nearest the player, for the phone to give as directions.</summary>
        internal bool NearestDesk(out string address)
        {
            address = null;
            PlayerController player = PlayerHelper.PlayerController;
            return player != null && _desks.Nearest(player.transform.position, out _, out address);
        }

        /// <summary>The front of the nearest driving-school desk (for anything else that keeps a counter there).</summary>
        internal bool NearestDesk(Vector3 from, out Vector3 front)
        {
            return _desks.Nearest(from, out front, out _);
        }

        private void OpenDesk()
        {
            if (!LifeEvents.IsSafeForEvent() || JobDirector.OnShift)
            {
                Licences.Toast(NotificationType.Warning, "reallife_lic_no_busy", 5f);
                return;
            }
            _pendingDesk = _desks.At >= 0 ? _desks.Front(_desks.At) : PlayerHelper.GetPosition();
            _prompting = false;
            _desks.Away();
            LicenceHud.Instance.HidePrompt();
            LicencePhone.OpenInPerson(null);
        }

        /// <summary>
        /// The player took a service in the conversation. Renewals and the endorsement are done on
        /// the spot; a test or a lesson is paid for now and starts when the conversation is over.
        /// Returns the line the school closes with.
        /// </summary>
        internal string Take(SchoolService service)
        {
            int day = TimeHelper.CurrentDay;
            int fee = DrivingSchool.Fee(service);
            if (!Licences.Pay(fee, FeeTransaction))
            {
                return "reallife_school_no_money";
            }
            switch (service)
            {
                case SchoolService.Renew:
                    for (int i = 0; i < DrivingSchool.Classes; i++)
                    {
                        if (LicenceState.Renewable((LicenceClass)i, day))
                        {
                            LicenceState.Renew((LicenceClass)i, day);
                        }
                    }
                    Logger?.Info("Licences: renewed for " + fee + ".");
                    return "reallife_school_done_renew";

                case SchoolService.Taxi:
                    LicenceState.Grant(LicenceClass.Taxi, day);
                    Logger?.Info("Licences: taxi endorsement issued.");
                    return "reallife_school_done_taxi";

                case SchoolService.Motorcycle:
                    LicenceState.Grant(LicenceClass.Motorcycle, day);   // the ride assessment is recorded at the desk: the school has no motorbike to lend
                    ModState.AddModifier(ModState.MoodUp);
                    Logger?.Info("Licences: motorcycle licence issued after the desk assessment.");
                    return "reallife_school_done_motorcycle";

                default:
                    _pending = true;
                    _pendingService = service;
                    _pendingFee = fee;
                    _pendingSince = Time.unscaledTime;
                    if (_desks.At >= 0)
                    {
                        _pendingDesk = _desks.Front(_desks.At);
                    }
                    return service == SchoolService.Theory ? "reallife_school_go_theory" : "reallife_school_go_drive";
            }
        }

        // The conversation is over (or nearly): the test that was paid for starts, or the money goes back.
        private void StartPending(float now)
        {
            if (!LifeEvents.IsSafeForEvent())
            {
                if (now - _pendingSince > 40f)
                {
                    _pending = false;
                    Refund();
                    Licences.Toast(NotificationType.Warning, "reallife_lic_test_interrupted", 8f);
                }
                return;
            }
            _pending = false;
            string reason = null;
            bool started;
            if (_pendingService == SchoolService.Theory)
            {
                started = _theory.Begin(DrivingQuestions.Pool, LicenceLaw.TheoryQuestions, LicenceLaw.TheoryPass, "reallife_lic_theory_title", OnTheoryMarked);
            }
            else
            {
                bool van = _pendingService == SchoolService.LessonVan || _pendingService == SchoolService.TestVan;
                bool lesson = _pendingService == SchoolService.LessonCar || _pendingService == SchoolService.LessonVan;
                started = CanDrive(out reason)
                          && _practical.Begin(van ? LicenceClass.Commercial : LicenceClass.Car, lesson, false, _pendingFee, _pendingDesk, out reason);
                if (started)
                {
                    _pendingFee = 0;   // the test has it now, and gives it back itself if it cannot go on
                }
            }
            if (!started)
            {
                Refund();
                Licences.Toast(NotificationType.Warning, reason ?? "reallife_lic_no_busy", 9f);
                Logger?.Info("Licences: '" + _pendingService + "' could not start (" + (reason ?? "busy") + "); the fee was returned.");
            }
        }

        private static bool CanDrive(out string reasonKey)
        {
            reasonKey = BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle ? "reallife_lic_no_on_foot"
                : JobDirector.OnShift || PoliceAttention.IsBusy ? "reallife_lic_no_busy"
                : null;
            return reasonKey == null;
        }

        private void Refund()
        {
            if (_pendingFee > 0)
            {
                Licences.Give(_pendingFee, "reallife_transaction_licence_refund");
                Licences.Toast(NotificationType.Info, "reallife_lic_fee_returned", "amount", Licences.Money(_pendingFee), 8f);
                _pendingFee = 0;
            }
        }

        private void OnTheoryMarked(bool passed, int score, int of)
        {
            _pendingFee = 0;
            int day = TimeHelper.CurrentDay;
            if (passed)
            {
                LicenceState.TheoryPassedDay[0] = day;
                LicenceState.TheoryRetryDay = 0;
                ModState.AddModifier(ModState.MoodUp);
            }
            else
            {
                LicenceState.TheoryRetryDay = day + LicenceLaw.RetestWaitDays;
            }
        }

        // ---------------------------------------------------------------- the options page

        private bool Ready(string test)
        {
            if (!LicenceState.Loaded || PlayerHelper.PlayerController == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (_broken)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff, 0, "switched off after an error this session");
                return false;
            }
            return true;
        }

        /// <summary>Test button: gives the licence if it is not in order, takes it away if it is.</summary>
        public void TestToggle(LicenceClass kind)
        {
            if (!Ready("Licence"))
            {
                return;
            }
            int day = TimeHelper.CurrentDay;
            if (LicenceState.Valid(kind, day))
            {
                LicenceState.Revoke(kind);
                Licences.Toast(NotificationType.Warning, "reallife_lic_test_revoked", "class", Licences.ClassName(kind), 6f);
            }
            else
            {
                if ((kind == LicenceClass.Commercial || kind == LicenceClass.Taxi) && !LicenceState.Valid(LicenceClass.Car, day))
                {
                    LicenceState.Grant(LicenceClass.Car, day);   // the van and the taxi stand on a car licence
                }
                LicenceState.SuspendedUntil = 0;
                LicenceState.Grant(kind, day);
                Licences.Toast(NotificationType.Success, "reallife_lic_test_granted", "class", Licences.ClassName(kind), 6f);
            }
        }

        /// <summary>Test button: the written test, here and now, with nothing charged. A pass counts.</summary>
        public void TestTheory()
        {
            if (!Ready("Theory test"))
            {
                return;
            }
            TestRefusal why = IsActive || _pending ? TestRefusal.SceneActive
                : !Settings.DrivingLicence || !Settings.DrivingSchool ? TestRefusal.FeatureOff
                : PlayerHelper.IsUsingVehicle ? TestRefusal.NotOnFoot
                : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Theory test", why);
                return;
            }
            _pendingFee = 0;
            _pending = true;
            _pendingService = SchoolService.Theory;
            _pendingSince = Time.unscaledTime;   // starts once the options page is closed
        }

        /// <summary>Test button: a practical test from where the player stands, with nothing charged. A pass counts.</summary>
        public void TestPractical(LicenceClass kind)
        {
            if (!Ready("Practical test"))
            {
                return;
            }
            TestRefusal why = TestRefusals.Street();
            if (why == TestRefusal.None)
            {
                why = IsActive || _pending ? TestRefusal.SceneActive
                    : !Settings.DrivingLicence || !Settings.DrivingSchool ? TestRefusal.FeatureOff
                    : PlayerHelper.IsUsingVehicle ? TestRefusal.NotOnFoot
                    : JobDirector.OnShift ? TestRefusal.OnJob
                    : PoliceAttention.IsBusy ? TestRefusal.PoliceBusy
                    : TestRefusal.None;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, "Practical test", why);
                return;
            }
            if (!_practical.Begin(kind, false, true, 0, PlayerHelper.GetPosition(), out string reason))
            {
                TestRefusals.Tell(Logger, "Practical test", TestRefusal.NothingFits, 0, reason);
            }
        }

        /// <summary>Test button: a routine roadside check now.</summary>
        public void TestCheck()
        {
            if (Ready("Roadside check"))
            {
                _enforcement.TestCheck();
            }
        }

        /// <summary>Test button: four penalty points, as for driving on the pavement.</summary>
        public void TestPoints()
        {
            if (Ready("Penalty points"))
            {
                _enforcement.Points(PointCode.Reckless);
            }
        }

        /// <summary>Test button (and the plain way to look): the licence card on a sheet for a few seconds.</summary>
        public void ShowCard()
        {
            if (!Ready("Licence card") || IsActive)
            {
                return;
            }
            LicenceHud.Instance.ShowSheet(Licences.T("reallife_lic_card_title"), DrivingSchool.CardText(), null,
                Licences.T("reallife_lic_sheet_close", "key", Keys.Label(KeyAction.Interact)));
            _cardUntil = Time.unscaledTime + 20f;
        }
    }
}
