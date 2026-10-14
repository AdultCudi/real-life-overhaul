using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using UI;
using UI.Notification;
using Streets;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Penalty points, and what happens to someone found driving without the licence for what
    /// they are in. Nothing is ever discovered by magic: points go on the record only when an
    /// officer has booked the offence (PoliceAttention.Stopped, PolicePursuit.CaughtAfterFleeing,
    /// PolicePursuit.PeopleCharged), and the licence is only looked at when the player is
    /// actually stopped: for an offence, after a pursuit, or at a rare routine roadside check
    /// (a patrol car that comes up behind, lights on: its own small pull-over, below).
    ///   no licence / wrong class   a fine, the vehicle impounded (the game's own tow, to a repair
    ///                              shop bay, and the fee), the third time in thirty days an arrest
    ///   expired, still renewable   a smaller fine; the car stays
    ///   suspended                  the heaviest fine, impounded, the second time an arrest
    /// Driving the school's car on a lesson or test is lawful, and nothing here ever touches it.
    /// Idle this is one roll every ten seconds while the player drives.
    /// </summary>
    internal sealed class DrivingEnforcement
    {
        private const float RollInterval = 10f;
        private const float CheckChance = 0.004f;      // per roll while driving: about once in forty minutes at the wheel...
        private const int CheckGapDays = 2;            // ...and never more often than this
        private const float ComplyWindow = 25f;
        private const float TicketDistance = 28f;
        private const float CrumbSpacing = 6f;
        private const int MaxCrumbs = 600;

        private enum CheckState { Idle, Signalled, Checking }

        private readonly MonoBehaviour _host;
        private readonly IModLogger _logger;
        private readonly Func<VehicleController, bool> _isSchoolCar;
        private readonly RoadsideStop _stop = new RoadsideStop();
        private ResponseUnit _patrol;

        private CheckState _check = CheckState.Idle;
        private Rigidbody _body;
        private VehicleController _vehicle;
        private float _nextRoll;
        private float _nextTick;
        private float _until;
        private float _holdSince;
        private float _askAt = -1f;
        private Vector3 _lastCrumb;
        private bool _attached;
        private bool _settling;
        private int _faults;

        public DrivingEnforcement(MonoBehaviour host, IModLogger logger, Func<VehicleController, bool> isSchoolCar)
        {
            _host = host;
            _logger = logger;
            _isSchoolCar = isSchoolCar;
        }

        /// <summary>True while a routine check has a patrol car out.</summary>
        public bool Checking => _check != CheckState.Idle;

        public void Attach()
        {
            if (_attached)
            {
                return;
            }
            _attached = true;
            PoliceAttention.Stopped += OnStopped;
            PolicePursuit.CaughtAfterFleeing += OnCaughtAfterCrash;
            PolicePursuit.PeopleCharged += OnPeopleCharged;
        }

        public void Detach()
        {
            if (_attached)
            {
                _attached = false;
                PoliceAttention.Stopped -= OnStopped;
                PolicePursuit.CaughtAfterFleeing -= OnCaughtAfterCrash;
                PolicePursuit.PeopleCharged -= OnPeopleCharged;
            }
            ClearCheck();
        }

        // ---------------------------------------------------------------- points

        // An officer has the player stopped for traffic offences: each kind booked goes on the record, and the licence is asked for.
        private void OnStopped(int booked, bool fled)
        {
            try
            {
                if (!LicenceState.Loaded || !Settings.DrivingLicence || OnSchoolCar())
                {
                    return;
                }
                if (Settings.LicencePoints)
                {
                    if ((booked & (1 << (int)Offence.RedLight)) != 0)
                    {
                        Points(PointCode.RedLight);
                    }
                    if ((booked & (1 << (int)Offence.Speeding)) != 0)
                    {
                        Points(PointCode.Speeding);
                    }
                    if ((booked & (1 << (int)Offence.Reckless)) != 0)
                    {
                        Points(PointCode.Reckless);
                    }
                    if ((booked & (1 << (int)Offence.WrongSide)) != 0)
                    {
                        Points(PointCode.WrongSide);
                    }
                    if ((booked & (1 << (int)Offence.DrunkDriving)) != 0)
                    {
                        Points(PointCode.DrunkDriving);
                        Wiring.LicenceLinks.DrunkDrivingBooked(fled);   // the second time inside three weeks it goes to court
                    }
                    if (fled)
                    {
                        Points(PointCode.FleeingStop);
                    }
                }
                AskForLicence(custodyFollows: fled, routine: false);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // Caught after driving off from a crash or from someone they hit.
        private void OnCaughtAfterCrash()
        {
            try
            {
                if (!LicenceState.Loaded || !Settings.DrivingLicence || OnSchoolCar())
                {
                    return;
                }
                if (Settings.LicencePoints)
                {
                    Points(PointCode.HitAndRun);
                }
                Licences.Law.Record("hit_and_run", arrested: true);
                AskForLicence(custodyFollows: true, routine: false);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // The police have charged the player for people hit with the car.
        private void OnPeopleCharged(int hurt, int killed, bool stopped)
        {
            try
            {
                if (!LicenceState.Loaded || !Settings.DrivingLicence || !Settings.LicencePoints)
                {
                    return;
                }
                for (int i = 0; i < Mathf.Min(hurt, 3); i++)
                {
                    Points(PointCode.InjuredPerson);
                }
                if (killed > 0)
                {
                    Points(PointCode.KilledPerson);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Puts an entry on the record, says so, and says so again if it meant a suspension.</summary>
        public void Points(PointCode code)
        {
            int day = TimeHelper.CurrentDay;
            bool suspended = LicenceState.AddPoints(code, day);
            int total = LicenceState.PointsOn(day);
            Licences.Toast(NotificationType.Warning, "reallife_lic_points_added", "points", LicenceLaw.Points(code).ToString(), "total", total.ToString(), 9f);
            _logger?.Info("Licences: " + LicenceLaw.Points(code) + " points for " + code + "; " + total + " on the record.");
            if (suspended)
            {
                Licences.Toast(NotificationType.Error, "reallife_lic_suspended", "days", LicenceLaw.SuspensionDays.ToString(), 12f);
                ModState.AddModifier(ModState.MoodDownBig);
                _logger?.Info("Licences: twelve points: suspended until day " + LicenceState.SuspendedUntil + ".");
            }
        }

        // ---------------------------------------------------------------- the licence, asked for at a stop

        private bool OnSchoolCar()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            return gm != null && gm.selectedVehicle != null && _isSchoolCar(gm.selectedVehicle);
        }

        /// <summary>
        /// The officer asks for the licence. Everything that follows from the answer is said in
        /// one message with its amount, charged, and (a few seconds later, with the officer at
        /// the window) carried out: the vehicle towed, the driver taken in.
        /// </summary>
        /// <param name="custodyFollows">The stop already ends in custody (caught after fleeing): no second arrest is made here.</param>
        /// <returns>True if the licence was in order.</returns>
        public bool AskForLicence(bool custodyFollows, bool routine)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            if (vehicle == null || !Licences.Driving.ClassOf(vehicle, out LicenceClass kind) || _isSchoolCar(vehicle))
            {
                return true;   // on foot by now, or in something that needs no licence
            }
            int day = TimeHelper.CurrentDay;
            int fine = 0;
            if (LicenceState.EvadedChecks > 0)
            {
                // The check they drove on from: answered for now.
                fine += LicenceLaw.EvadedCheckFine * Mathf.Min(LicenceState.EvadedChecks, 3);
                LicenceState.EvadedChecks = 0;
                if (Settings.LicencePoints)
                {
                    Points(PointCode.EvadedCheck);
                }
            }
            LicenceStanding standing = LicenceState.Standing(kind, day);
            if (standing == LicenceStanding.Valid)
            {
                if (fine > 0)
                {
                    Licences.Charge(fine, "reallife_transaction_fine");
                    Licences.Toast(NotificationType.Error, "reallife_lic_stop_evaded_only", "amount", Licences.Money(fine), 10f);
                }
                else if (routine)
                {
                    Licences.Toast(NotificationType.Success, "reallife_lic_check_ok", 7f);
                }
                return true;
            }

            string key;
            bool impound = true;
            bool arrest = false;
            string offence = LicenceOffences.Unlicensed;
            if (standing == LicenceStanding.Expired)
            {
                fine += LicenceLaw.ExpiredFine;
                key = "reallife_lic_stop_expired";
                impound = false;
            }
            else if (standing == LicenceStanding.Suspended)
            {
                fine += LicenceLaw.SuspendedFine;
                key = "reallife_lic_stop_suspended";
                offence = LicenceOffences.Suspended;
                arrest = LicenceState.CountCaught(day) >= 2;
            }
            else if (HoldsAnother(kind, day))
            {
                fine += LicenceLaw.WrongClassFine;
                key = "reallife_lic_stop_wrong_class";
                arrest = LicenceState.CountCaught(day) >= LicenceLaw.ArrestFromCount;
            }
            else
            {
                fine += LicenceLaw.NoLicenceFine;
                key = "reallife_lic_stop_none";
                arrest = LicenceState.CountCaught(day) >= LicenceLaw.ArrestFromCount;
            }
            // A hired van or the game's job van is not the player's to have impounded, nor, in a Going Public session, another
            // player's car they were driving (GoingPublic.MineVehicle): that one is never towed or billed for here.
            bool own = !(vehicle.vehicleInstance is DeliveryVehicleInstance) && GoingPublic.MineVehicle(vehicle);
            Licences.Charge(fine, "reallife_transaction_fine");
            Licences.Toast(NotificationType.Error, key, "amount", Licences.Money(fine), "class", Licences.ClassName(kind), 12f);
            ModState.AddModifier(ModState.MoodDown);
            Licences.Law.Record(offence, arrested: arrest && !custodyFollows);
            if (standing == LicenceStanding.Suspended)
            {
                Wiring.LicenceLinks.SuspendedAtStop(LicenceLaw.SuspendedFine);   // the courts charge this too: what was just paid is credited there, not owed twice
            }
            _logger?.Info("Licences: stopped in a " + kind + " vehicle with the licence " + standing + ": fine " + fine + (impound ? ", impound" : string.Empty)
                          + (arrest ? ", arrest" : string.Empty) + " (caught " + LicenceState.Caught + " time(s) in the window).");
            if ((impound || arrest) && _host != null && _host.isActiveAndEnabled && !_settling)
            {
                _settling = true;
                _host.StartCoroutine(Guard.Routine(Settle(vehicle, impound, own, arrest && !custodyFollows, custodyFollows ? 1.5f : 7f)));
            }
            return false;
        }

        // A licence for something else is in order (a car licence on a motorbike or a van, a motorcycle licence at the wheel of a car):
        // the wrong class, not none at all.
        private static bool HoldsAnother(LicenceClass kind, int day)
        {
            return (kind != LicenceClass.Car && LicenceState.Valid(LicenceClass.Car, day))
                   || (kind != LicenceClass.Commercial && LicenceState.Valid(LicenceClass.Commercial, day))
                   || (kind != LicenceClass.Motorcycle && LicenceState.Valid(LicenceClass.Motorcycle, day));
        }

        // The officer has had time to reach the window: out of the car, the car away, and for a repeat offender the cuffs.
        private IEnumerator Settle(VehicleController vehicle, bool impound, bool own, bool arrest, float after)
        {
            try
            {
                yield return new WaitForSecondsRealtime(after);
                var gm = InstanceBehavior<GameManager>.Instance;
                float waited = 0f;
                while (vehicle != null && gm != null && ReferenceEquals(gm.selectedVehicle, vehicle) && vehicle.CurrentSpeed > 1f && waited < 10f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (vehicle == null || gm == null || SaveGameManager.Current == null)
                {
                    yield break;
                }
                if (impound)
                {
                    if (ReferenceEquals(gm.selectedVehicle, vehicle))
                    {
                        if (vehicle.CurrentSpeed > 1f)
                        {
                            // Still rolling: nobody is pulled out of a moving car. The fine stands; the tow does not happen.
                            Licences.Toast(NotificationType.Warning, "reallife_lic_impound_missed", 8f);
                            yield break;
                        }
                        vehicle.ExitVehicle();
                        yield return new WaitForSecondsRealtime(0.5f);
                        // (As after a crash: the game clears the selected vehicle only when its panel closes.)
                        UIs hud = InstanceBehavior<UIs>.Instance;
                        if (gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                        {
                            hud.playerHUD.itemPanelUI.Toggle(false);
                        }
                        yield return new WaitForSecondsRealtime(0.3f);
                    }
                    if (vehicle != null && !PlayerHelper.IsUsingVehicle)
                    {
                        Impound(vehicle, own);
                    }
                }
                if (arrest)
                {
                    yield return new WaitForSecondsRealtime(1f);
                    if (!Licences.Law.Custody(LicenceLaw.ArrestHours))
                    {
                        _logger?.Info("Licences: the arrest could not be made into custody just now; it stays at the fine.");
                    }
                }
            }
            finally
            {
                _settling = false;
            }
        }

        private void Impound(VehicleController vehicle, bool own)
        {
            try
            {
                if (!own || vehicle.vehicleInstance == null)
                {
                    // A hired van or the game's own job van: it goes back to whoever it belongs to, not to the pound.
                    Licences.Toast(NotificationType.Warning, "reallife_lic_impound_hired", 9f);
                    return;
                }
                Address pound = VehicleHelper.TowVehicle(vehicle.vehicleInstance, "ba:towdestination_autorepairshop");
                if (pound != null && !pound.IsUndefined())
                {
                    Licences.Charge(LicenceLaw.ImpoundFee, "reallife_transaction_impound");
                    Notifications.Show(NotificationType.Error, "reallife_lic_impounded", new Dictionary<string, string>(2)
                    {
                        { "amount", Licences.Money(LicenceLaw.ImpoundFee) }, { "address", pound.ToFormattedString() }
                    }, 12f);
                    _logger?.Info("Licences: the vehicle was towed to " + pound.ToFormattedString() + ".");
                }
                else
                {
                    Licences.Toast(NotificationType.Warning, "reallife_lic_impound_no_room", 9f);
                    _logger?.Info("Licences: no free bay to tow the vehicle to; it stays where it is.");
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the routine roadside check

        /// <summary>Called a few times a second by the director while out of doors.</summary>
        public void Tick(float now)
        {
            if (_check == CheckState.Idle)
            {
                if (_askAt >= 0f)
                {
                    _askAt = -1f;
                }
                if (now >= _nextRoll)
                {
                    _nextRoll = now + RollInterval;
                    if (Random.value < CheckChance && CanCheck(out _))
                    {
                        BeginCheck(now);
                    }
                }
                return;
            }
            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + 0.25f;
            try
            {
                TickCheck(now);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                ClearCheck();
                if (++_faults >= 3)
                {
                    _nextRoll = float.MaxValue;
                    _logger?.Warn("Licences: roadside checks are off for this session after repeated errors (see above).");
                }
            }
        }

        private bool CanCheck(out TestRefusal why)
        {
            why = TestRefusals.Street();
            if (why != TestRefusal.None)
            {
                return false;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            VehicleController vehicle = gm != null ? gm.selectedVehicle : null;
            if (vehicle == null || !PlayerHelper.IsUsingVehicle || !Licences.Driving.ClassOf(vehicle, out _) || _isSchoolCar(vehicle))
            {
                why = TestRefusal.NotDriving;
                return false;
            }
            if (!Settings.DrivingLicence || !Settings.RoadsideChecks || !Settings.Police)
            {
                why = TestRefusal.FeatureOff;
                return false;
            }
            if (PoliceAttention.IsBusy || _settling)
            {
                why = TestRefusal.PoliceBusy;
                return false;
            }
            _vehicle = vehicle;
            _body = vehicle.GetComponent<Rigidbody>();
            return _body != null;
        }

        private bool Wanted(int day)
        {
            return day - LicenceState.LastCheckDay >= CheckGapDays && !JobDirector.OnShift && LifeEvents.IsSafeForEvent()
                   && !Wiring.Scenes.AnyLargeSceneActive && _body != null && _body.velocity.sqrMagnitude > 36f;   // never a check beside another scene (Wiring/Scenes)
        }

        private void BeginCheck(float now, bool test = false)
        {
            if (!test && !Wanted(TimeHelper.CurrentDay))
            {
                return;
            }
            if (_patrol == null)
            {
                _patrol = new ResponseUnit(_host.gameObject);
            }
            Vector3 position = _body.position;
            Waypoint anchor = TrafficTools.ClosestWaypointAlong(position, _body.velocity, 40f);
            if (!_patrol.Spawn(UnitKind.Police, anchor, Random.Range(45f, 80f), 25f))
            {
                if (test)
                {
                    TestRefusals.Tell(_logger, "Roadside check", TestRefusal.NothingFits, 0, _patrol.Refusal);
                }
                return;   // no patrol car could get onto the road here: no check
            }
            _patrol.Path.Add(position);
            EmergencyDriver driver = _patrol.Driver;
            driver.Begin(_patrol.Car, _patrol.Path, position, 9f);
            driver.Follow(_body, 9f);
            driver.CruiseSpeed = PursuitPace.For(_body);
            _lastCrumb = position;
            _check = CheckState.Signalled;
            _until = now + ComplyWindow;
            _holdSince = 0f;
            _nextTick = now;
            Licences.Toast(NotificationType.Warning, "reallife_lic_check_pull_over", 9f);
            _logger?.Info("Licences: a routine roadside check: patrol car out behind the player.");
        }

        private void TickCheck(float now)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !TrafficTools.Ready || _patrol == null || _patrol.IsLost)
            {
                ClearCheck();
                return;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            bool driving = _body != null && gm != null && ReferenceEquals(gm.selectedVehicle, _vehicle) && PlayerHelper.IsUsingVehicle;
            Vector3 position = driving ? _body.position : player.transform.position;

            if (_check == CheckState.Signalled)
            {
                if (PoliceAttention.IsBusy)
                {
                    ClearCheck();   // they did something worse meanwhile: that stop takes over, and asks for the licence too
                    return;
                }
                if ((position - _lastCrumb).sqrMagnitude > CrumbSpacing * CrumbSpacing && _patrol.Path.Count < MaxCrumbs)
                {
                    _lastCrumb = position;
                    _patrol.Path.Add(position);
                }
                bool stopped = !driving || _body.velocity.sqrMagnitude < 1f;
                if (stopped)
                {
                    _until = Mathf.Max(_until, now + 5f);
                    if ((_patrol.Car.transform.position - position).sqrMagnitude > TicketDistance * TicketDistance)
                    {
                        _holdSince = 0f;
                    }
                    else if (_holdSince <= 0f)
                    {
                        _holdSince = now;
                    }
                    else if (now - _holdSince >= 1.5f)
                    {
                        StartChecking(now, driving);
                    }
                }
                else
                {
                    _holdSince = 0f;
                    if (now >= _until)
                    {
                        // Drove on. Nobody gives chase over a routine check, but the plate is written down.
                        LicenceState.EvadedChecks++;
                        LicenceState.LastCheckDay = TimeHelper.CurrentDay;
                        Licences.Toast(NotificationType.Warning, "reallife_lic_check_evaded", 9f);
                        _logger?.Info("Licences: the player drove on from a roadside check; it is answered for at the next stop.");
                        ClearCheck();
                    }
                }
                return;
            }

            // Checking: the officer walks up, asks, walks back.
            _stop.Tick(now);
            if (_askAt >= 0f && now >= _askAt)
            {
                _askAt = -1f;
                Justice.PoliceContact(driving: true);   // a bench warrant is served at this stop as at any other (Scripts/Justice)
                AskForLicence(custodyFollows: false, routine: true);
            }
            if (now >= _until || (now >= _holdSince && _stop.Finished && _askAt < 0f && !_settling))
            {
                ClearCheck();
            }
        }

        private void StartChecking(float now, bool driving)
        {
            _patrol.Park();
            _check = CheckState.Checking;
            LicenceState.LastCheckDay = TimeHelper.CurrentDay;
            _holdSince = now + Random.Range(9f, 12f);   // (from here: the earliest the patrol car leaves)
            _until = _holdSince;
            bool walking = false;
            try
            {
                walking = driving && StreetSpots.Beside(_body.worldCenterOfMass, _body.rotation * Vector3.forward, 0.95f, StreetSpots.Left, 0.9f, out Vector3 door)
                          && _stop.Begin(_patrol.Car, door, arrest: false, now);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _stop.Clear();
            }
            if (walking)
            {
                _until = _holdSince + 28f;
            }
            _askAt = now + (walking ? 5f : 1.5f);
        }

        /// <summary>The patrol car and its officer go; nothing is after the player. Safe at any time.</summary>
        public void ClearCheck()
        {
            _check = CheckState.Idle;
            _askAt = -1f;
            _holdSince = 0f;
            try
            {
                _stop.Clear();
                if (_patrol != null)
                {
                    _patrol.Release(TrafficTools.Ready, remove: false, quiet: true);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Options-page test button: a routine check now, whatever the dice and the calendar say.</summary>
        public void TestCheck()
        {
            try
            {
                if (_check != CheckState.Idle)
                {
                    TestRefusals.Tell(_logger, "Roadside check", TestRefusal.SceneActive);
                    return;
                }
                if (!CanCheck(out TestRefusal why))
                {
                    TestRefusals.Tell(_logger, "Roadside check", why);
                    return;
                }
                BeginCheck(Time.time, test: true);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                ClearCheck();
            }
        }
    }
}
