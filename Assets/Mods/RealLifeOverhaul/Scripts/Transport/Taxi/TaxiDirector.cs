using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Parking.UndergroundParking;
using RealLifeOverhaul.GameUi;
using UI.Load;
using UI.Notification;
using UnityEngine;
using Vehicles.Taxis;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A taxi called from the phone (<see cref="TaxiPhone"/>): a taxi of the game's traffic is put on the road out of view and
    /// driven to the caller (<see cref="TaxiRoads"/>, <see cref="TaxiCab"/>); once it stands, the game's own click, map, fare and
    /// ride take over and this only watches how the call ends. Cost: one comparison a frame, a 0.25 s timer while a taxi is out,
    /// switched off otherwise. Nothing is saved; the rules and numbers are <see cref="TaxiRules"/>'s.
    /// </summary>
    public sealed partial class TaxiDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("taxi", "TaxiDirector", typeof(TaxiDirector));

        private const float TickInterval = 0.25f;    // seconds
        private const float QuoteGoodFor = 45f;      // seconds a quote's plan may be used for the taxi that is sent
        private const float QuoteMoved = 12f;        // metres: unless the caller has walked this far since
        private const int StartTries = 4;            // lane points tried when traffic stands on the one a taxi was to start from
        private const float ArrivedWithin = 14f;     // metres from its lane point within which a standing taxi has arrived where it was sent
        private const float MapExtends = 15f;        // seconds a taxi stays at least, while a destination is being picked from it

        private const string CallOutKey = "reallife_transaction_taxi_callout";
        private const string CancelKey = "reallife_transaction_taxi_cancel";
        private const string NoShowKey = "reallife_transaction_taxi_noshow";

        private enum Step { Idle, Coming, Waiting }

        public static IModLogger Logger;
        public static TaxiDirector Instance { get; private set; }

        /// <summary>True while a called taxi is on its way or standing for the caller.</summary>
        public static bool Out => Instance != null && Instance._step != Step.Idle;

        private readonly TaxiPlan _quote = new TaxiPlan();
        private readonly TaxiPlan _plan = new TaxiPlan();
        private readonly TaxiRoads _roads = new TaxiRoads();
        private readonly GuideMark _mark = new GuideMark("the taxi");
        private TaxiCab _cab;
        private Action<bool> _onMap, _mapSeen;
        private Step _step;
        private float _nextTick;
        private float _fee;              // the call-out as it was quoted for the taxi that is out
        private float _quoteFee;
        private int _quoteMinutes;
        private float _waitUntil;
        private float _routeLogAt;
        private Vector3 _caller;         // where the caller stood when the taxi was sent: it comes there
        private string _markName;
        private bool _replanned;
        private bool _grace;
        private bool _mapOurs;           // the last taxi the game's map had was this one
        private bool _contactDone;
        private bool _failed;

        private void Awake()
        {
            Instance = this;
            var host = new GameObject("RealLifeOverhaul Taxi");
            host.transform.SetParent(transform, false);
            _cab = new TaxiCab(host);
            _mark.Keep(this);   // the marker goes with this component, or when the guard stops it
            _onMap = Guard.Wrap<bool>(OnMap);
        }

        private void OnDestroy()
        {
            try
            {
                Stand(TaxiCab.Leave.Remove, "the city is closing");
                _roads.Dispose();
                TaxiPhone.Forget();
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

        private static readonly int HotUpdate = HotSpots.Slot("Transport: TaxiDirector.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
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
            float now = Time.time;
            if (now < _nextTick)
            {
                return;
            }
            _nextTick = now + TickInterval;
            try
            {
                if (!_contactDone)
                {
                    if (PlayerHelper.PlayerController == null || SaveGameManager.Current == null || !ModState.Loaded || LoadScene.isLoading)
                    {
                        return;
                    }
                    _contactDone = true;
                    TaxiPhone.Setup();
                }
                if (_step == Step.Idle || _failed)
                {
                    enabled = false;   // nothing is out: nothing runs until the next call
                    return;
                }
                Tick(now);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // A fault, on the timer or in something the game called: everything is let go and no call is taken this session.
        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Warn("Taxi: calling a taxi is switched off for this session after an error. " + e);
            try
            {
                Stand(TaxiCab.Leave.Unseen, "a called taxi let go after an error");
            }
            catch (Exception)
            {
                _step = Step.Idle;
            }
        }

        /// <summary>The option was switched: the contact is added at once; a taxi that is out goes on the next tick.</summary>
        public static void OptionChanged()
        {
            TaxiDirector director = Instance;
            if (director != null && director._contactDone && Settings.TaxiCall)
            {
                TaxiPhone.Setup();
            }
        }

        private void Tick(float now)
        {
            if (!Settings.TaxiCall)
            {
                Released("its option was switched off", TaxiCab.Leave.Unseen);
                return;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || SaveGameManager.Current == null)
            {
                Released("the game is closing", TaxiCab.Leave.Remove);
                return;
            }
            EventHold.Keep(ref GlobalEvents.onCityMapToggle, _onMap, ref _mapSeen);   // the game clears its events on every load
            if (CityMap.IsOpen)
            {
                NoteMapTaxi();
            }
            // The game's ride has begun, with this taxi or another (hailed in the street, the private driver).
            if (InstanceBehavior<TaxiSystem>.IsInitialized && TaxiSystem.IsTraveling)
            {
                if (_mapOurs)
                {
                    Ride();
                }
                else
                {
                    WentAway("the player took another taxi");
                }
                return;
            }
            if (TaxiBridges.Held != null && TaxiBridges.Held())
            {
                Released("the player is held by a scene", TaxiCab.Leave.Unseen);
                return;
            }
            string away = Away();
            if (away != null)
            {
                WentAway(away);
                return;
            }
            _cab.Tick(now);
            if (_cab.TakeLost())
            {
                CannotCome("the game took its vehicle from under it");
                return;
            }
            if (_step == Step.Coming)
            {
                if (_cab.State == TaxiCab.Phase.Standing)
                {
                    Arrived(now, player.transform.position);
                }
                else
                {
                    Watch(now);
                }
            }
            else if (now >= _waitUntil)
            {
                if (CityMap.IsOpen && _mapOurs)
                {
                    _waitUntil = now + MapExtends;   // a destination is being picked from it
                }
                else if (TaxiRules.Grace(Mathf.Sqrt(CitySpots.Flat(player.transform.position - _cab.Car.transform.position)), _grace))
                {
                    _grace = true;
                    _waitUntil = now + TaxiRules.GraceSeconds;
                }
                else
                {
                    NoShow();
                }
            }
        }

        // Why the caller is no longer waiting in the street, or null.
        private static string Away()
        {
            if (PlayerHelper.IsUsingVehicle && VehicleHelper.IsInsideVehicle())
            {
                return "the player got into a vehicle";
            }
            if (BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking)
            {
                return "the player went indoors";
            }
            if (SubwaySystem.IsRiding || CasinoBoatManager.IsOnCasinoBoat || (TaxiBridges.Riding != null && TaxiBridges.Riding()))
            {
                return "the player left by other transport";
            }
            return null;
        }

        private static readonly int HotOnMap = HotSpots.Slot("Transport: TaxiDirector.OnMap");
        private void OnMap(bool open)
        {
            using var hotSpot = HotSpots.Time(HotOnMap);
            if (_step != Step.Idle)
            {
                NoteMapTaxi();
            }
        }

        // The map opened plainly has no taxi and leaves the note as it is.
        private void NoteMapTaxi()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            ITaxi taxi = city != null && city.cityMap != null ? city.cityMap.Taxi : null;
            if (taxi != null)
            {
                _mapOurs = !ReferenceEquals(_cab.Controller, null) && ReferenceEquals(taxi, _cab.Controller);
            }
        }

        /// <summary>Whether a call would be taken now, from what costs nothing to ask.</summary>
        public TaxiRules.Refusal Check(out bool failed)
        {
            failed = _failed;
            bool riding = (InstanceBehavior<TaxiSystem>.IsInitialized && TaxiSystem.IsTraveling) || SubwaySystem.IsRiding || CasinoBoatManager.IsOnCasinoBoat
                          || (TaxiBridges.Riding != null && TaxiBridges.Riding());
            bool indoors = BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking || !TrafficTools.Street;
            // A Going Public guest has the host's traffic, so no cab can be sent.
            return TaxiRules.Refuse(Settings.TaxiCall && !_failed && !GoingPublic.TrafficMirrored, riding, TaxiBridges.Held != null && TaxiBridges.Held(), _step != Step.Idle,
                PlayerHelper.IsUsingVehicle && VehicleHelper.IsInsideVehicle(), indoors);
        }

        /// <summary>What the dispatcher says: the wait and the call-out. Plans a taxi's way but puts nothing on the road. False with why.</summary>
        public bool Quote(out int minutes, out float fee, out TaxiRules.Refusal refusal, out bool failed)
        {
            minutes = 0;
            fee = 0f;
            refusal = TaxiRules.Refusal.Off;
            failed = _failed;
            try
            {
                refusal = Check(out failed);
                if (refusal != TaxiRules.Refusal.None)
                {
                    if (Loud)
                    {
                        Say("call", "refused: " + refusal + (failed ? " (switched off for the session after an error)" : string.Empty));
                    }
                    return false;
                }
                bool wet = Wet();
                TaxiRules.StartBand(wet, false, out float least, out float most);
                Vector3 caller = PlayerHelper.GetPosition();
                VehicleComponent pick = TaxiCab.Pick(out bool idle);
                if (pick == null)
                {
                    refusal = TaxiRules.Refusal.NoTaxi;
                    if (Loud)
                    {
                        Say("call", "refused: no taxi of the traffic pool is idle or driving out of sight");
                    }
                    return false;
                }
                if (!_roads.Find(caller, pick.vehicleType, least, most, false, _quote, out refusal))
                {
                    if (Loud)
                    {
                        Say("call", "refused: " + refusal + " (" + _quote.Goals + " lane points within " + TaxiRules.RoadWithin.ToString("F0") + " m to end at, " + _quote.Starts
                                    + " out of view between " + least.ToString("F0") + " and " + most.ToString("F0") + " m to start from, " + _quote.Steps + " search steps)");
                    }
                    return false;
                }
                _quoteFee = fee = TaxiRules.Fee(TimeHelper.CurrentHour, wet);
                _quoteMinutes = minutes = TaxiRules.Minutes(_quote.Metres, PlayerPrefSettings.GameSpeed);
                if (Loud)
                {
                    Say("call", "quoted about " + minutes + " min and a call-out of " + fee.ToString("F0") + " (" + TimeHelper.CurrentHour + ":00, "
                                + (wet ? "wet" : "dry") + "; " + (idle ? "an idle taxi of the pool" : "a taxi driving out of sight") + " would come " + _quote.Metres.ToString("F0") + " m)");
                }
                return true;
            }
            catch (Exception e)
            {
                Fail(e);
                failed = true;
                refusal = TaxiRules.Refusal.Off;
                return false;
            }
        }

        /// <summary>"Send one": the taxi quoted is put on the road. False if it cannot be done after all.</summary>
        public bool Dispatch(out int minutes)
        {
            minutes = _quoteMinutes;
            try
            {
                if (Check(out _) != TaxiRules.Refusal.None)
                {
                    return false;
                }
                bool wet = Wet();
                TaxiRules.StartBand(wet, false, out float least, out float most);
                Vector3 caller = PlayerHelper.GetPosition();
                bool fresh = _quote.Valid && Time.time - _quote.At <= QuoteGoodFor && CitySpots.Flat(caller - _quote.Caller) <= QuoteMoved * QuoteMoved;
                _roads.NewCall();
                if (!Bring(caller, least, most, false, false, fresh ? _quote : null, out string why))
                {
                    if (Loud)
                    {
                        Say("call", "no taxi could be sent: " + why);
                    }
                    return false;
                }
                _fee = fresh ? _quoteFee : TaxiRules.Fee(TimeHelper.CurrentHour, wet);
                minutes = TaxiRules.Minutes(_plan.Metres, PlayerPrefSettings.GameSpeed);
                Notifications.Show(NotificationType.Info, "reallife_taxi_note_coming", new Dictionary<string, string>(1) { { "minutes", minutes.ToString() } }, 8f);
                return true;
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }
        }

        // Finds a taxi and its way and puts it on the road, leaving out a lane point traffic stands on; again: a taxi's one new plan.
        private bool Bring(Vector3 caller, float least, float most, bool mayBeSeen, bool again, TaxiPlan ready, out string why)
        {
            why = null;
            for (int attempt = 0; attempt < StartTries; attempt++)
            {
                VehicleComponent pick = TaxiCab.Pick(out bool idle);
                if (pick == null)
                {
                    why = "no taxi of the traffic pool is idle or driving out of sight";
                    return false;
                }
                TaxiPlan plan = ready;
                ready = null;
                if (plan == null)
                {
                    plan = _plan;
                    if (!_roads.Find(caller, pick.vehicleType, least, most, mayBeSeen, plan, out TaxiRules.Refusal refusal))
                    {
                        why = refusal + " (" + plan.Goals + " lane points to end at, " + plan.Starts + " to start from, " + plan.Steps + " search steps)";
                        return false;
                    }
                }
                if (Loud)
                {
                    Say("route", plan.Route.Count + " lane points, " + plan.Metres.ToString("F0") + " m of road from a start " + plan.StartAway.ToString("F0") + " m off (band "
                                 + least.ToString("F0") + " to " + most.ToString("F0") + "), to a lane point " + plan.FromCaller.ToString("F1", CultureInfo.InvariantCulture) + " m from the caller; "
                                 + plan.Starts + " places to start from, " + plan.Goals + " to end at, " + plan.Steps + " search steps, "
                                 + plan.Milliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms: PASS");
                }
                bool seen = CitySpots.InView(plan.StartAt) || StreetSpots.IsInView(plan.StartAt);
                if (_cab.Send(plan, _roads, pick, idle, out why, out bool startTaken))
                {
                    if (!ReferenceEquals(plan, _plan))
                    {
                        Copy(plan, _plan);
                    }
                    _caller = caller;
                    _step = Step.Coming;
                    _replanned = again;
                    _grace = false;
                    _mapOurs = false;
                    _routeLogAt = Time.time + 60f;
                    _nextTick = 0f;
                    enabled = true;
                    if (Loud)
                    {
                        Say("taxi found", _cab.Chassis + " put on the road " + plan.StartAway.ToString("F0") + " m from the caller, " + plan.Metres.ToString("F0")
                                          + " m of road away; in view: " + (seen ? "yes" : "no") + ": " + (seen && !mayBeSeen ? "FAIL" : "PASS"));
                    }
                    return true;
                }
                if (!startTaken)
                {
                    return false;
                }
                _roads.Refuse(plan.Start);
            }
            why = "traffic stood on each of the " + StartTries + " lane points tried";
            return false;
        }

        private static void Copy(TaxiPlan from, TaxiPlan to)
        {
            to.Clear();
            to.Route.AddRange(from.Route);
            to.Points.AddRange(from.Points);
            to.Start = from.Start;
            to.StartAt = from.StartAt;
            to.Stop = from.Stop;
            to.Along = from.Along;
            to.Caller = from.Caller;
            to.Metres = from.Metres;
            to.FromCaller = from.FromCaller;
            to.StartAway = from.StartAway;
            to.Starts = from.Starts;
            to.Goals = from.Goals;
            to.Steps = from.Steps;
            to.Milliseconds = from.Milliseconds;
            to.At = from.At;
        }

        private static bool Wet()
        {
            return TaxiBridges.Wet != null && TaxiBridges.Wet();
        }

        // A taxi that gets nowhere is planned again once, out of view, then given up (TaxiRules.Judge).
        private void Watch(float now)
        {
            float still = _cab.NoProgressFor;
            if (now >= _routeLogAt)
            {
                _routeLogAt = now + 60f;
                if (Loud)
                {
                    Say("en route", _cab.Out.ToString("F0") + " s out, " + _cab.Remaining.ToString("F0") + " m still to drive of " + _cab.StartedBack.ToString("F0") + "; " + WaitingWords()
                                    + "; no progress for " + still.ToString("F0") + " s: " + (still < 25f ? "PASS" : "FAIL (it is getting nowhere)"));
                }
            }
            switch (TaxiRules.Judge(still, _cab.Out, _cab.Remaining, _cab.InView, _replanned))
            {
                case TaxiRules.Watch.Replan:
                    Replan(still);
                    break;
                case TaxiRules.Watch.GiveUp:
                    if (Loud)
                    {
                        Say("re-plan", "the taxi has got nowhere for " + still.ToString("F0") + " s and has been out " + _cab.Out.ToString("F0") + " s, " + _cab.Remaining.ToString("F0")
                                       + " m from the caller (" + WaitingWords() + "): given up, nothing charged: FAIL");
                    }
                    CannotCome("it was given up");
                    break;
            }
        }

        private void Replan(float still)
        {
            float from = _cab.Remaining;
            string held = WaitingWords();
            _cab.Release(TaxiCab.Leave.Remove, "a called taxi that got nowhere, out of view");
            TaxiRules.ReplanBand(from, out float least, out float most);
            if (Bring(_caller, least, most, false, true, null, out string why))
            {
                if (Loud)
                {
                    Say("re-plan", "the taxi had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the caller (" + held
                                   + "), out of view: another is on its way from " + _cab.Remaining.ToString("F0") + " m: PASS");
                }
            }
            else
            {
                if (Loud)
                {
                    Say("re-plan", "the taxi had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the caller (" + held
                                   + "), out of view, and no other could be sent (" + why + "): given up, nothing charged: FAIL");
                }
                CannotCome("no second taxi could be sent");
            }
        }

        private string WaitingWords()
        {
            switch (_cab.Waiting)
            {
                case RoadHold.Light: return "standing at a red light for " + _cab.WaitingFor.ToString("F0") + " s";
                case RoadHold.Traffic: return "standing behind traffic for " + _cab.WaitingFor.ToString("F0") + " s";
                case RoadHold.Closure: return "standing before a closed road for " + _cab.WaitingFor.ToString("F0") + " s";
                default: return "driving";
            }
        }

        // Asked before a log line is put together, so no text is made with diagnostics off.
        private static bool Loud => Meta.Dev.Verbose;

        private static void Say(string step, string what)
        {
            Logger?.Info("Taxi check (" + step + "): " + what + ".");
        }

        /// <summary>Where the called taxi is, while one is out and the game's own marker is not on it.</summary>
        public static bool Where(out Vector3 at)
        {
            TaxiDirector director = Instance;
            if (director != null && director._step != Step.Idle && director._cab.Car != null && !director._mark.Showing)
            {
                at = director._cab.Car.transform.position;
                return true;
            }
            at = default;
            return false;
        }
    }
}
