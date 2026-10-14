using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using BigAmbitions.DayNightCycle;
using UI.Load;
using Localizor;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Texts for transport: a locale key with its {names} filled in. Builds strings: on a change only, never per frame.</summary>
    public static class TransitText
    {
        public static string T(string key, params string[] pairs)
        {
            string text = JobKeys.Fill(key.GetLocalization());
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        public static string Money(float amount)
        {
            return amount.ToCurrencyFormat();
        }
    }

    /// <summary>
    /// Buses. The network is <see cref="TransitMap"/>; this puts it on the street near the player
    /// and nowhere else:
    ///
    /// - the three stops nearest the player (within 135 m) are dressed from three dressings that
    ///   exist (<see cref="BusStopDressing"/>: solid, with seats), out of view;
    /// - the nearest of them within 80 m (or the one the player has walked up to) is the stop that is served: up to two people wait there
    ///   (walking in from out of view; one may take a seat the player has not), and a bus comes
    ///   at intervals that follow the hour, the day of the week and the weather
    ///   (<see cref="TransitBus"/>: one bus on the road for the stop, and one more only to be
    ///   seen pulling away after the player has ridden);
    /// - A BUS COMES. When one is due it is put on the road; if that cannot be done it is tried
    ///   again every two seconds with more allowed each time (a vehicle that is driving out of
    ///   sight, any ordinary vehicle under the bus body, a start in view far off), and the
    ///   panel says DELAYED meanwhile. On the road it is watched: held long out of view it is
    ///   moved up its line; held for minutes it is given up with a notice and the next is sent;
    /// - the panel's figure is <see cref="TransitEta"/>'s: it only goes down, or the panel says
    ///   DELAYED and why;
    /// - when the bus stands, somebody may get out and walk off, the people waiting get up, walk
    ///   to the door one at a time and get in, and it leaves, always;
    /// - WHERE IT STANDS: in the stop's bay beside the lane where the stop has one and it is free
    ///   (<see cref="BusBay"/>, <see cref="TransitBays"/>, <see cref="TransitBus"/>), so that the
    ///   lane is free while it stands; else on the lane's line, and then for as short a time as
    ///   the stop allows: 4 s where nobody waits, nobody gets out and the player is not near,
    ///   9 s otherwise, up to 34 s only while the player is on foot within 35 m;
    /// - on foot at the stop the player is shown, in the game's own panel, the stop, the line and
    ///   the next bus; may sit on the shelter's bench (<see cref="TransitSeat"/>); picks any stop
    ///   of the network from the game's own list (<see cref="TransitTrips"/>), at once or while
    ///   waiting; walks to the bus's door, pays, steps in, sits down (stands on a full bus) and is
    ///   seen pulling away (<see cref="BusRider"/>); the time passes under a fade (<see cref="TransitRide"/>);
    ///   at the far end gets up and off the bus that stands there, which then leaves.
    ///   All of that is in TransitDirector.Player.cs.
    ///
    /// Every step writes one "Transit check (...)" line with what was measured and PASS or FAIL,
    /// so that a run can be judged from Player.log alone.
    ///
    /// The budget: a 0.25 s timer; per frame only one key test while the player sits (the panel's
    /// keys are the game's, read by the game). Four people at most (two JobCast pairs of its own), made only
    /// when first needed. Texts are built when what they say changes. A fault switches buses
    /// off for the session, after putting everything away. After a load: the routes are laid behind the
    /// loading screen (TransitMap.Prewarm, a late piece of Core/SceneFinds' ScenePrewarm) and the road paint's
    /// template found there (RoadPaint.Prewarm); the stops are placed a slice of about a millisecond a frame;
    /// the stops are put up nearest first, as many a tick as fit in about a millisecond (one at the least),
    /// where they were put up six at a time (five ticks of 5 to 20 ms after every load and fast travel).
    /// </summary>
    public sealed partial class TransitDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("transport", "TransitDirector", typeof(TransitDirector));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float TickInterval = 0.25f;
        private const float ArrivedBeyond = 60f;    // metres between two passes that no walk or drive covers: the player was set down here
        private const float SolidWithin = 60f;      // metres: on foot this near, a dressed stop's boxes cut the navigation mesh
        private const float SolidKeep = 90f;        // ...and stay cut until this far off
        private const float ServeRange = 80f;
        private const float ServeKeep = 105f;
        private const float SitRange = 1.5f;         // metres from a seat's place at which sitting is offered
        private const float BusLead = 30f;           // seconds before it is due that the test button's bus is put on the road
        private const float ChangeKeep = 260f;       // metres: on a journey's change the place of the next bus is kept while the player walks to it
        private const int MostCalls = 8;             // lines calling at one place, at the most, that are timetabled there
        private const float BusCruise = 11f;         // m/s on an open road (TransitBus)
        private const float LightsPerMetre = 1f / 150f;   // lit junctions a metre of line, about: Manhattan's blocks
        private const float StartLeast = 60f;        // metres of line before the stop a bus may start from, at the least
        private const float StartMost = 330f;        // ...and at most, at first
        private const float StartFar = 520f;         // ...and once nothing nearer is out of sight
        private const int AwayMost = 4;              // buses driving away after their stop at one time, at the most
        private const float WaitsWithin = 35f;       // metres from the stop within which a bus waits for the player on foot, up to DwellMost
        private const float RetryEvery = 2f;         // seconds between tries when no bus could be put on the road
        private const float MoveUpAfter = 40f;       // seconds without progress, out of view, before a bus is moved up its line
        private const float GiveUpAfter = 150f;      // seconds without progress before a bus is given up
        private const float GiveUpOut = 300f;        // ...or this long on the road without arriving
        private const float DwellLeast = 9f;
        private const float DwellEmpty = 4f;         // ...where it stands in the lane and nobody waits, nobody gets out and the player is not near: doors open and shut
        private const float DwellMost = 34f;
        private const float ChooseFor = 30f;         // seconds the bus waits for the player to pick a stop
        private const int Dressings = 48;          // every stop of the city stands for as long as the city is loaded (18 in the city as it is)
        private const int PutsATick = 6;            // stops put up in one pass at the most, nearest first: all of them within seconds of the network being ready
        private const double PutsSliceMs = 1.0;     // milliseconds of a tick the putting up may take in play (the first stop of a tick always goes up)
        private const int Waiters = 2;

        private enum RiderStep { None, Arriving, Waiting, Rising, Boarding, Entering, Exiting, Leaving }
        private enum PlayerStep { None, AtStop, Choosing, Riding }

        private struct Rider
        {
            public StreetActor Person;
            public RiderStep Step;
            public Vector3 Spot;
            public float Until;
            public int Seat;        // the seat of the shelter's bench they have, or -1
            public bool Sat;        // ...and are sitting on
        }

        public static IModLogger Logger;
        public static TransitDirector Instance { get; private set; }

        /// <summary>True while the player is aboard a bus: from the step up at its door until they are off it at the far end (or the old ride's fade).</summary>
        public static bool Riding => Instance != null && ((Instance._ride != null && Instance._ride.Riding) || (Instance._rider != null && Instance._rider.Aboard));

        /// <summary>True while the stop's panel (whose buttons are on the game's interact keys) is on screen: the mod's own enter-a-door key stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._panel.Showing || Instance._keyFrame == Time.frameCount);

        private readonly BusStopDressing[] _dressings = new BusStopDressing[Dressings];
        private readonly Rider[] _waiting = new Rider[Waiters];
        private readonly Collider[] _touching = new Collider[12];
        private readonly StringBuilder _text = new StringBuilder(320);
        private readonly TransitEta _eta = new TransitEta();
        private readonly TransitSeat _seat = new TransitSeat();
        private BusRider _rider;
        private TransitBus _offBus;      // the bus the player has just stepped down from at the far end: it closes its doors and goes once they are clear
        private float _offAt;
        private TransitBus _bus;         // the front bus: on its way to the place served for its line's call, or standing there
        private TransitBus _next;        // the bus behind it, of whichever line calls next
        private readonly TransitEta _nextEta = new TransitEta();
        private readonly List<TransitBus> _away = new List<TransitBus>(AwayMost);   // buses driving off after their stop, until nobody can see them
        private readonly List<TransitBus> _spare = new List<TransitBus>(AwayMost);  // ...and idle again
        private readonly TransitPlan.Pace _pace = new TransitPlan.Pace();        // how the buses really drive, learnt this session
        private readonly double[] _callAt = new double[MostCalls];   // per line calling at the place served (its Calls): when it next calls there, game minutes
        private readonly bool[] _pinned = new bool[MostCalls];       // ...kept as it is: it is due, or a bus is on its way for it
        private readonly double[] _doneAt = new double[MostCalls];   // ...when a bus of it last stood there
        private readonly double[] _sortAt = new double[MostCalls];
        private readonly int[] _sortWho = new int[MostCalls];
        private double _lastCall = double.MinValue;   // when a bus last stood at the place served
        private int _calls;
        private int _frontCall = -1;     // the call the front bus is for
        private int _nextCall = -1;      // ...and the bus behind it
        private int _nextRefusals;
        private float _nextTry2;
        private int _made;               // buses made this session (each is kept and used again)
        private float _promisedAt;       // Time.time the front bus was promised for when it was put on the road
        private float _shownAt = -1f;    // ...and the first figure the player was shown for its call, as a time
        private float _shownFigure;
        private bool _wasDelayed;
        private int _bookedLine = -1;    // the line of the bus the player waits for, -1 for any
        private TransitTrip _bookedTrip; // ...and the journey picked
        private TransitTrip _plan;       // a journey with a change of bus under way (TransitDirector.Journey)
        private bool _hasPlan;
        private int _planLeg;            // 1 on the first bus, 2 at the change and on the second
        private bool _changing;          // at the change, on foot, for the second bus
        private TransitBus _seenOff;     // the bus a rider sees pull away
        private Rider _alighter;
        private JobCast _waiters;
        private JobCast _others;
        private TransitRide _ride;
        private SubwayJourney _subway;
        private TransitStop _served;
        private TransitStop _rideTo;
        private TransitStop _seeOff;
        private BusStopDressing _sitAt;  // the shelter whose free seat the player stands at
        private int _sitSeat = -1;
        private float _seeOffBy;
        private Transform _root;
        private float _nextTick;
        private int _tick;
        private float _dwellUntil;
        private float _dwellLeast = DwellLeast;
        private float _nextWaiter;
        private float _chooseUntil;
        private float _nextTry;
        private float _routeLogAt;
        private float _moveUpAt;
        private float _promised;         // the figure the panel was showing when the bus was put on the road
        private float _walkerRadius = -1f;
        private int _refusals;
        private int _boarded;
        private int _alighted;
        private int _shownMinutes;
        private int _shownEpoch = -1;
        private Vector3 _door;
        private Vector3 _doorInwards;
        private bool _doorFound;
        private Vector3 _exitDoor;       // where somebody getting off steps down to: the middle door, else the front one
        private bool _boardable;         // the waiting people have a clear way to the door
        private bool _busFull;
        private bool _wasStanding;
        private int _keyFrame = -1;
        private PlayerStep _player;
        private int _panelKey = int.MinValue;
        private bool _failed;
        private bool _saveLoaded;
        private bool _streetClear = true;
        private bool _dressAtOnce = true;           // nothing has been put out for where the player now is: the first pass does not wait for them to look away
        private bool _dressMore;                    // the last pass stopped at its slice with stops still to put up: the next tick goes on
        private Vector3 _dressedFrom;
        private float _minutesPerSecond = 1f;
        private float _rateFrom;
        private float _rateMinutes;

        private void Awake()
        {
            Instance = this;
            TransitMap.Logger = Logger;
            BusZones.Logger = Logger;
            TransitBus.Logger = Logger;
            RoadPaint.Logger = Logger;
            TransitMap.Reset();
            TransitState.Reset();
            ScenePrewarm.AddLate("transit: the bus routes laid between the subway stations", TransitMap.Prewarm);
            ScenePrewarm.Add("transit: the game's road paint found for the bus bays (everything in memory gone through)", RoadPaint.Prewarm);
            _root = new GameObject("RealLifeOverhaul Transit").transform;
            _root.SetParent(transform, false);
            _bus = new TransitBus(_root.gameObject);
            _next = new TransitBus(_root.gameObject);
            _seenOff = new TransitBus(_root.gameObject);
            _ride = new TransitRide(this);
            BusRider.Logger = Logger;
            _rider = new BusRider(this);
            _subway = new SubwayJourney(this, _ride);
            for (int i = 0; i < Waiters; i++)
            {
                _waiting[i].Seat = -1;
            }
            _alighter.Seat = -1;
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                _rider?.Abort("the city is going", put: false);
                RemovePlayerUi();
                _seat.Abort();
                _ride.Abort();
                ClearStreet(true);
                for (int i = 0; i < Dressings; i++)
                {
                    _dressings[i]?.Dispose();
                }
                _waiters?.Dispose();
                _others?.Dispose();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            TransitMap.Reset();
            TransitState.Reset();
            RoadPaint.Dispose();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private System.Action _onSave;   // made once: Core/SaveHook compares it, it is never built again

        private void OnSaveGame()
        {
            try
            {
                if (SaveGameManager.Current != null && SaveGameManager.Current.modData != null)
                {
                    // Part of the way through a bus ride the save has the player on the ground at a stop, not on the bus
                    // (this runs after the game has written where the player is: PlayerHelper.SaveCurrentPosition).
                    float fareBack = 0f;
                    if (_rider != null && _rider.ForSave(out Vector3 place, out fareBack))
                    {
                        SaveGameManager.Current.LastPlayerPosition = place;
                        Logger?.Info("Transit check (save): written part of the way through a bus ride (" + _rider.Now + "): the save has the player at "
                                     + (_rider.Stage == BusInside.Stage.Arriving ? "the stop they get off at" : _rider.Passed != null ? StopLabel(_rider.Passed) + ", the last stop the bus called at"
                                         : "the stop they boarded at") + (fareBack > 0f ? ", the fare to be given back" : "") + ": PASS.");
                    }
                    TransitState.Save(SaveGameManager.Current.modData, fareBack);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the two clocks

        private static readonly int HotUpdate = HotSpots.Slot("Transport: TransitDirector.Update");
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
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (_rider.Active)
                {
                    _rider.Frame(Time.deltaTime, Time.time);
                }
                if (_journey)
                {
                    JourneyFrame();
                }
                if (_seat.Active)
                {
                    if (_seat.Frame(Time.deltaTime))
                    {
                        _keyFrame = Time.frameCount;   // the key that stands them up does nothing else
                        _seat.Stand(Time.time);
                        _panelKey = int.MinValue;
                    }
                }
                float now = Time.time;
                if (now < _nextTick)
                {
                    return;
                }
                _nextTick = now + (TransitMap.PlacingStops && Settings.Buses && TrafficTools.Ready ? 0f : TickInterval);   // (placing stops: a slice of about 1 ms a frame until it is done)
                _tick++;
                long began = System.Diagnostics.Stopwatch.GetTimestamp();
                Tick(now);
                Cost(now, System.Diagnostics.Stopwatch.GetTimestamp() - began);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // A fault, on the timer or in something the game called (a button of the panel, a click, an entry of the list):
        // everything is put away and buses stay off for the session.
        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Transit: buses and the subway journey are switched off for this session after the error above.");
            try
            {
                _rider.Abort("a fault switched the buses off", put: true);
                _seat.Abort();
                _ride.Abort();
                ClearStreet(true);
                HideHud();
                _subway.Hide();
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
        }

        private void Tick(float now)
        {
            var game = InstanceBehavior<GameManager>.Instance;
            if (game == null || game.playerController == null || SaveGameManager.Current == null || !ModState.Loaded || LoadScene.isLoading)
            {
                return;
            }
            if (!_saveLoaded)
            {
                _saveLoaded = true;
                TransitState.Load(SaveGameManager.Current.modData);
                TransitPhone.Setup();
            }
            if (TransitState.FareBack > 0f)
            {
                // The save was written part of the way through a bus ride: the player is at the stop they boarded at, and the fare is theirs again.
                float fare = TransitState.FareBack;
                TransitState.FareBack = 0f;
                RiderFareBack(fare, "the save was written part of the way through a bus ride");
            }
            if (_rider.Now == BusRider.Step.Away && !_ride.Riding)
            {
                _rider.Done();   // (the ride's routine ended without reaching the far end: it gave the player back itself)
            }
            if (_offBus != null)
            {
                SeeOff(now);
            }
            if ((_tick & 7) == 0)
            {
                SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed
                MeasureClock(now);
            }
            Vector3 at = game.playerController.transform.position;
            bool onFoot = !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding;

            _subway.Tick(now, at, onFoot && !_ride.Riding && !_rider.Active && _player != PlayerStep.Choosing && !_seat.Active);

            if (!Settings.Buses || !TransitBus.StreetReady)
            {
                // Switched off, indoors, or traffic shut down: nothing of the buses stays on the street.
                if (!_streetClear)
                {
                    ClearStreet(!Settings.Buses);
                }
                HideHud();
                return;
            }
            if (!TransitMap.Ready)
            {
                TransitMap.Step();
                return;
            }
            if (TransitMap.Stops.Count == 0)
            {
                return;
            }
            BusZones.Step();   // the stops' zones, a slice at a time until they are all made
            _streetClear = false;
            if ((_tick & 3) == 0)
            {
                // Just loaded, back on the street, or set down somewhere else by a taxi or the subway: the stops go up at once.
                bool atOnce = _dressAtOnce || CitySpots.Flat(at - _dressedFrom) > ArrivedBeyond * ArrivedBeyond;
                _dressAtOnce = false;
                _dressedFrom = at;
                _dressMore = DressNear(at, atOnce, now);
                Serve(at, now);
                for (int i = 0; i < Dressings; i++)
                {
                    BusStopDressing dressing = _dressings[i];
                    if (dressing == null || dressing.Stop == null)
                    {
                        continue;
                    }
                    // Solid (the navigation mesh cut round it) only while the player is on foot near it: nearer to
                    // become so than to stay so, so that it does not flicker at the edge. Never while driving past.
                    float reach = dressing.IsSolid ? SolidKeep : SolidWithin;
                    dressing.Solid(onFoot && !_ride.Riding && CitySpots.Flat(dressing.Stop.Sign - at) < reach * reach, now);
                    if (dressing.CheckDue)
                    {
                        // Once after each dressing, and only for the log: with diagnostics off nothing is asked of the mesh and no text is made.
                        dressing.Check(Meta.Dev.Verbose ? Logger : null, now, Meta.Dev.Verbose ? StopLabel(dressing.Stop) : null);
                    }
                }
            }
            else if (_dressMore)
            {
                _dressMore = DressNear(at, false, now);   // the stops the last pass had no time for, a slice a tick
            }
            if (_waiters != null && _waiters.AnyOut)
            {
                _waiters.Cull();
            }
            if (_others != null && _others.AnyOut)
            {
                _others.Cull();
            }
            if (_seat.Active)
            {
                _seat.Tick(now);
                if (!onFoot || _ride.Riding)
                {
                    _seat.Abort();
                }
            }
            if (_journey)
            {
                RunJourney(now);
            }
            ChangeStep();
            RunBus(now);
            RunRiders(now);
            _seenOff.Tick(now);
            if (_seeOff != null)
            {
                if (_seenOff.SendLeaving(_seeOff) || now > _seeOffBy)
                {
                    _seeOff = null;
                }
            }
            RunPlayer(now, at, onFoot);
        }

        // What the buses cost: the director's timer, measured each tick, said once a minute while any bus is out (with none out
        // and no stop near, the timer does next to nothing and nothing is said). The buses' own drivers run in the physics step
        // only while they drive: BusDriver, a box or two of sensing every 0.15 s each.
        private long _costTicks;
        private long _costWorst;
        private int _costCount;
        private int _costBusesMost;
        private float _costFrom;

        private void Cost(float now, long ticks)
        {
            int out_ = (_bus.State != TransitBus.Phase.Idle ? 1 : 0) + (_next.State != TransitBus.Phase.Idle ? 1 : 0) + _away.Count + (_seenOff.State != TransitBus.Phase.Idle ? 1 : 0);
            if (out_ == 0 && _costCount == 0)
            {
                _costFrom = now;
                return;
            }
            _costTicks += ticks;
            _costWorst = Math.Max(_costWorst, ticks);
            _costCount++;
            _costBusesMost = Math.Max(_costBusesMost, out_);
            if (now - _costFrom < 60f)
            {
                return;
            }
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double average = _costTicks * ms / Math.Max(1, _costCount);
            Logger?.Info("Transit check (cost): " + _costCount + " ticks of the director in the last " + (now - _costFrom).ToString("F0") + " s with buses out (" + _costBusesMost + " at the most, "
                         + _made + " made this session besides the first three): " + average.ToString("F3") + " ms a tick on average, " + (_costWorst * ms).ToString("F2") + " ms at worst; "
                         + (average * 4.0).ToString("F2") + " ms a second (wanted: under 1 ms a second on average): " + (average * 4.0 < 1.0 ? "PASS" : "FAIL") + ".");
            _costTicks = 0;
            _costWorst = 0;
            _costCount = 0;
            _costBusesMost = 0;
            _costFrom = now;
        }

        // How fast the game's clock runs, for saying "in about 4 min": measured, since the player can change it.
        private void MeasureClock(float now)
        {
            float minutes = TimeHelper.NowInMinutes();
            if (_rateFrom > 0f && now - _rateFrom > 1f && minutes > _rateMinutes)
            {
                _minutesPerSecond = Mathf.Lerp(_minutesPerSecond, Mathf.Clamp((minutes - _rateMinutes) / (now - _rateFrom), 0.1f, 20f), 0.5f);
            }
            _rateFrom = now;
            _rateMinutes = minutes;
        }

        // ---------------------------------------------------------------- stops

        // The radius of a walker the navigation mesh was made for: what a cut in the mesh is wider than its box by. Asked once.
        private float WalkerRadius()
        {
            if (_walkerRadius < 0f)
            {
                _walkerRadius = 0.5f;
                try
                {
                    var game = InstanceBehavior<GameManager>.Instance;
                    NavMeshAgent agent = game != null && game.playerController != null && game.playerController.Character != null ? game.playerController.Character.navmeshAgent : null;
                    if (agent != null)
                    {
                        _walkerRadius = Mathf.Clamp(NavMesh.GetSettingsByID(agent.agentTypeID).agentRadius, 0.15f, 0.6f);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            return _walkerRadius;
        }

        // True if the pass stopped at its slice with stops still to put up.
        private bool DressNear(Vector3 at, bool evenInView, float now)
        {
            // A bus stop is part of the street: every place is put up once, the nearest first, and stays up while the city
            // is loaded. Nothing waits for the player to look away and nothing is taken in behind them.
            // Put out the nearest that are not out yet: a pass over the places, once a second, and on every tick while the
            // last pass stopped at its slice (about a millisecond in play, the nearest always; all of them behind a black screen).
            WorkSlice slice = SceneFinds.Hidden ? WorkSlice.Whole : WorkSlice.Of(PutsSliceMs, 1);
            for (int pass = 0; pass < PutsATick; pass++)
            {
                if (!slice.More())
                {
                    return true;
                }
                TransitStop nearest = null;
                float best = float.MaxValue;
                List<TransitStop> places = TransitMap.Places;
                for (int i = 0; i < places.Count; i++)
                {
                    float sqr = CitySpots.Flat(places[i].Sign - at);
                    if (sqr < best && !Dressed(places[i]))
                    {
                        best = sqr;
                        nearest = places[i];
                    }
                }
                if (nearest == null)
                {
                    return false;
                }
                int free = -1;
                for (int i = 0; i < Dressings && free < 0; i++)
                {
                    if (_dressings[i] == null || _dressings[i].Stop == null)
                    {
                        free = i;
                    }
                }
                if (free < 0)
                {
                    // All out: the furthest of them gives its place to a nearer stop, which is the one the player can reach.
                    float furthest = best;
                    for (int i = 0; i < Dressings; i++)
                    {
                        float sqr = CitySpots.Flat(_dressings[i].Stop.Sign - at);
                        if (sqr > furthest && _dressings[i].Stop != _served)
                        {
                            furthest = sqr;
                            free = i;
                        }
                    }
                    if (free < 0)
                    {
                        return false;
                    }
                }
                if (_dressings[free] == null)
                {
                    _dressings[free] = new BusStopDressing(_root, free + 1);
                    slice.End();   // a dressing made for the first time (its props, its lettering, its boxes) is a tick's work on its own
                }
                else if (_dressings[free].Stop != null)
                {
                    _dressings[free].Clear();   // the furthest, giving up its place only now that the nearer one is going up
                }
                PutDressing(_dressings[free], nearest);
            }
            return true;   // (as many as a pass puts up: the next pass, on the next tick, sees whether any are left)
        }

        // A place's pole, with every line that stops there on its flag.
        private void PutDressing(BusStopDressing dressing, TransitStop place)
        {
            TransitLine line = TransitMap.Lines[place.Line];
            string numbers = TransitMap.Numbers(place);
            dressing.Put(place.At, numbers, place.At.Calls.Count > 1 ? numbers : line.Number + " " + line.To, WalkerRadius());
        }

        private bool Dressed(TransitStop stop)
        {
            return DressingOf(stop) != null;
        }

        private BusStopDressing DressingOf(TransitStop stop)
        {
            stop = stop != null ? stop.At : null;
            for (int i = 0; stop != null && i < Dressings; i++)
            {
                if (_dressings[i] != null && _dressings[i].Stop == stop)
                {
                    return _dressings[i];
                }
            }
            return null;
        }

        // Which place has people waiting and buses coming: the nearest dressed one, kept until the player is well away.
        private void Serve(Vector3 at, float now)
        {
            if (_journey)
            {
                return;   // (the stop the player's bus drives to is the one served: Journey)
            }
            if (_served != null && _player != PlayerStep.Choosing && !_ride.Riding)
            {
                // Well away from it, or standing on foot at another dressed stop (the other side of the street, say): that one is
                // served instead. On a journey's change the place of the next bus is kept while the player walks to it.
                TransitStop stood = PlayerHelper.IsUsingVehicle || AtStop(_served, at) ? null : StoodAt(at);
                if (_changing && _hasPlan && stood == _plan.ChangeOff.At)
                {
                    stood = null;   // (just off the first bus, at the stop they are walking from)
                }
                float keep = _changing ? ChangeKeep : ServeKeep;
                if (CitySpots.Flat(_served.Sign - at) > keep * keep || !Dressed(_served) || (stood != null && stood != _served))
                {
                    if (_changing)
                    {
                        Logger?.Info("Transit check (journey): the player left the change at " + StopLabel(_served) + " before the " + LineNumber(_plan.ChangeOn) + " came: the journey ends there: PASS.");
                        DropPlan();
                    }
                    EndService();
                }
            }
            if (_served != null)
            {
                return;
            }
            TransitStop nearest = null;
            float best = ServeRange * ServeRange;
            for (int i = 0; i < Dressings; i++)
            {
                TransitStop stop = _dressings[i] != null ? _dressings[i].Stop : null;
                if (stop == null)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(stop.Sign - at);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = stop;
                }
            }
            if (nearest == null)
            {
                return;
            }
            BeginService(nearest, now);
        }

        // A place becomes the one that has people waiting and buses coming: its lines' timetables from now.
        private void BeginService(TransitStop stop, float now)
        {
            _served = stop.At;
            _calls = Mathf.Min(_served.Calls.Count, MostCalls);
            for (int i = 0; i < MostCalls; i++)
            {
                _pinned[i] = false;
                _doneAt[i] = double.MinValue;
                _callAt[i] = double.MaxValue;
            }
            _lastCall = double.MinValue;
            _frontCall = -1;
            _nextCall = -1;
            _refusals = 0;
            _nextRefusals = 0;
            _nextTry = 0f;
            _nextWaiter = now + 2f;
            _panelKey = int.MinValue;
            Schedule(NowMinutes());
        }

        private void EndService()
        {
            ReleaseRiders();
            HandOff(ref _bus);
            HandOff(ref _next);
            _bus.TakeLost();
            _next.TakeLost();
            _served = null;
            _frontCall = -1;
            _nextCall = -1;
            _calls = 0;
            _wasStanding = false;
            _doorFound = false;
            _refusals = 0;
            _player = PlayerStep.None;
            HideHud();
        }

        // A bus the director has done with: let go (it drives on and goes once nobody can see it: TransitBus.Release), and
        // if it is still on the street meanwhile, it is ticked among the buses driving away and another takes its place.
        private void HandOff(ref TransitBus bus)
        {
            if (bus.State == TransitBus.Phase.Idle)
            {
                return;
            }
            bus.Release(remove: false);
            if (bus.State == TransitBus.Phase.Idle)
            {
                return;
            }
            _away.Add(bus);
            bus = Spare();
        }

        // An idle bus of the director's: one that has gone, or a new one (each made only when first needed).
        private TransitBus Spare()
        {
            for (int i = _spare.Count - 1; i >= 0; i--)
            {
                TransitBus bus = _spare[i];
                _spare.RemoveAt(i);
                if (bus.State == TransitBus.Phase.Idle)
                {
                    return bus;
                }
            }
            _made++;
            return new TransitBus(_root.gameObject);
        }

        // The buses driving away after their stop: each goes once nobody can see it, and is idle again for the next.
        private void RunAway(float now)
        {
            for (int i = _away.Count - 1; i >= 0; i--)
            {
                TransitBus bus = _away[i];
                bus.Tick(now);
                if (bus.TakeDeparted(out float gap, out bool pulledOut))
                {
                    Departed(bus, gap, pulledOut);
                }
                bus.TakeLost();
                if (bus.State == TransitBus.Phase.Idle)
                {
                    _away.RemoveAt(i);
                    _spare.Add(bus);
                }
            }
        }

        private void Departed(TransitBus bus, float gap, bool pulledOut)
        {
            // It stood in its bay with its doors closed until the lane was clear: the departure line, held until now.
            Logger?.Info((bus.DepartureNote ?? "Transit check (departure): the bus leaves its bay") + "; in its bay it waited " + gap.ToString("F0") + " s for the lane beside and behind it to be clear"
                         + (pulledOut ? " and pulled out" : " and no gap came: it drives on from where it stands, out of the lane") + ": " + (pulledOut && bus.DepartureSound ? "PASS" : "FAIL") + ".");
            bus.DepartureNote = null;
        }

        // ---------------------------------------------------------------- the timetable at the place served

        /// <summary>The game's clock, in minutes (its total: the day and the time of day).</summary>
        private static double NowMinutes()
        {
            return TimeHelper.NowInMinutes();
        }

        // Real seconds from now to a time on the game's clock, at the rate it is running at.
        private float SecondsTo(double minutes)
        {
            if (minutes >= double.MaxValue)
            {
                return float.MaxValue;
            }
            return (float)((minutes - NowMinutes()) / Mathf.Max(0.1f, _minutesPerSecond));
        }

        // When each line next calls at the place served, by its timetable (TransitPlan.NextCall), no two nearer than
        // SpacingSeconds: what the panel shows. A call that is due or has a bus on its way is kept as it is.
        private void Schedule(double now)
        {
            if (_served == null)
            {
                return;
            }
            List<TransitStop> calls = _served.Calls;
            bool rain = Raining();
            float scale = CivicPolicy.ServiceScale;
            double spacing = TransitPlan.SpacingSeconds * Mathf.Max(0.1f, _minutesPerSecond);
            double anchor = _lastCall;
            int n = 0;
            for (int k = 0; k < _calls; k++)
            {
                if (_pinned[k])
                {
                    if (_callAt[k] < double.MaxValue)
                    {
                        anchor = Math.Max(anchor, _callAt[k]);
                    }
                    continue;
                }
                TransitStop call = calls[k];
                TransitLine line = TransitMap.Lines[call.Line];
                _sortAt[n] = TransitPlan.NextCall(Math.Max(now, _doneAt[k] + 1.0), line.Main, line.OffsetAt(call.Order), rain, scale);
                _sortWho[n] = k;
                n++;
            }
            TransitPlan.Space(_sortAt, _sortWho, n, spacing, anchor);
            for (int i = 0; i < n; i++)
            {
                _callAt[_sortWho[i]] = _sortAt[i];
            }
        }

        // The call due soonest that no bus is on its way for; -1 if none runs.
        private int Soonest()
        {
            int best = -1;
            double at = double.MaxValue;
            for (int k = 0; k < _calls; k++)
            {
                if ((k == _frontCall && _bus.State != TransitBus.Phase.Idle) || (k == _nextCall && _next.State != TransitBus.Phase.Idle))
                {
                    continue;
                }
                if (_callAt[k] < at)
                {
                    at = _callAt[k];
                    best = k;
                }
            }
            return best;
        }

        /// <summary>When a line next calls at a stop of its, by the timetable or (at the place served) as the panel has it: for the journey planner.</summary>
        private double CallAt(int line, int order, double at)
        {
            if (_served != null && line >= 0 && line < TransitMap.Lines.Count)
            {
                for (int k = 0; k < _calls; k++)
                {
                    TransitStop call = _served.Calls[k];
                    if (call.Line == line && call.Order == order && _callAt[k] >= at && _callAt[k] < double.MaxValue)
                    {
                        return _callAt[k];
                    }
                }
            }
            TransitLine l = TransitMap.Lines[line];
            return TransitPlan.NextCall(at, l.Main, l.OffsetAt(order), Raining(), CivicPolicy.ServiceScale);
        }

        private Func<int, int, double, double> _callAtFn;

        /// <summary>The planner's question, made once.</summary>
        internal Func<int, int, double, double> Calls => _callAtFn ?? (_callAtFn = CallAt);

        // Seconds before its call at which a bus is put on the road: what the longest start out of sight takes to drive.
        private float Lead => TransitPlan.LeadSeconds(BusCruise, _pace, LightsPerMetre, StartMost);

        /// <summary>Whether it is raining, if the game's weather can be asked at all.</summary>
        public static bool Raining()
        {
            try
            {
                return RainHelper.AreRainDropsFalling;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- the buses

        private void RunBus(float now)
        {
            _bus.Tick(now);
            _next.Tick(now);
            RunAway(now);
            if (_bus.TakeDeparted(out float gap, out bool pulledOut))
            {
                Departed(_bus, gap, pulledOut);
            }
            if (_served == null)
            {
                _bus.TakeLost();
                _next.TakeLost();
                return;
            }
            if (_bus.TakeLost())
            {
                // The game or a scene took the vehicle: said in the bus's own line; another is sent at once.
                _eta.Lost();
                _wasStanding = false;
                _doorFound = false;
                _refusals = 0;
                _nextTry = now + RetryEvery;
                _panelKey = int.MinValue;
            }
            if (_next.TakeLost())
            {
                Unpin(_nextCall);
                _nextCall = -1;
            }
            if (!_journey)
            {
                Promote();
                Schedule(NowMinutes());
            }
            switch (_bus.State)
            {
                case TransitBus.Phase.Idle:
                    if (_journey)
                    {
                        break;
                    }
                    int soonest = Soonest();
                    if (_eta.State == TransitEta.Mode.Here || _frontCall >= _calls)
                    {
                        _frontCall = -1;   // (the bus that stood here has gone and was kept: the next call is the timetable's)
                    }
                    if (soonest != _frontCall && !(_frontCall >= 0 && _pinned[_frontCall]))
                    {
                        _frontCall = soonest;
                        if (_frontCall >= 0 && _frontCall == _nextCall && _next.State == TransitBus.Phase.Idle)
                        {
                            _nextCall = -1;   // (the bus behind was given up: this call is the front one's now)
                        }
                        _shownAt = -1f;
                        _wasDelayed = false;
                        if (_frontCall >= 0 && _callAt[_frontCall] < double.MaxValue)
                        {
                            _eta.Timetable(SecondsTo(_callAt[_frontCall]));
                        }
                        else
                        {
                            _eta.NoService();
                        }
                        _panelKey = int.MinValue;
                    }
                    if (_frontCall < 0 || _callAt[_frontCall] >= double.MaxValue)
                    {
                        break;
                    }
                    float seconds = SecondsTo(_callAt[_frontCall]);
                    if (!_pinned[_frontCall] && _eta.State == TransitEta.Mode.Timetable && Mathf.Abs(seconds - _eta.Seconds) > 2f)
                    {
                        _eta.Timetable(seconds);   // (the timetable moved under it: the rain, the clock's rate)
                    }
                    if (seconds <= Lead)
                    {
                        _pinned[_frontCall] = true;   // due: the time shown is kept until a bus has stood here for it
                        if (now >= _nextTry && _player != PlayerStep.Choosing && !_ride.Riding)
                        {
                            if (Put(_bus, _eta, _frontCall, ref _refusals, out string refusal))
                            {
                                Assigned(now, false);
                            }
                            else
                            {
                                _refusals++;
                                _nextTry = now + RetryEvery;
                                if (_refusals == 1 || _refusals % 15 == 0)
                                {
                                    Logger?.Info("Transit: no bus could be put on the road out of sight for " + StopLabel(_served.Calls[_frontCall]) + " yet (try " + _refusals + ", again every "
                                                 + RetryEvery.ToString("F0") + " s; " + seconds.ToString("F0") + " s to its call; the panel says DELAYED " + 8 + " s after it is due): " + refusal + ".");
                                }
                            }
                        }
                    }
                    if (_bus.State == TransitBus.Phase.Idle)
                    {
                        _eta.Wait(TickInterval, _refusals > 0);   // (not in the tick a bus has just been put on the road)
                    }
                    break;

                case TransitBus.Phase.Approaching:
                    _eta.Track(TickInterval, _bus.Estimate(_pace), _bus.Waiting != TransitBus.Hold.None);
                    if (!_journey)
                    {
                        Watch(now);   // (with the player aboard it is never moved up its line: Journey gives it up instead)
                    }
                    break;

                case TransitBus.Phase.Standing:
                    if (!_wasStanding)
                    {
                        _wasStanding = true;
                        _eta.Arrived();
                        Arrived(now);
                    }
                    bool busy = _alighter.Step == RiderStep.Exiting;
                    for (int i = 0; i < Waiters; i++)
                    {
                        busy |= _waiting[i].Step == RiderStep.Rising || _waiting[i].Step == RiderStep.Boarding || _waiting[i].Step == RiderStep.Entering;
                    }
                    bool held = (_player == PlayerStep.Choosing && now < _chooseUntil) || _ride.Riding || _rider.HoldsBus || _rider.Holds(_bus);   // (the player on the way to its door, getting on, or getting off again)
                    if (_rider.WantsDeparture && !busy)
                    {
                        Leave(now);   // they sit (or hold on): the doors close and it goes
                        break;
                    }
                    // The driver waits for somebody at the stop or walking up to it: the least stand is for an empty stop. A bus
                    // of another line than the one the player waits for does not wait for them.
                    bool wanted = PlayerAtStop(WaitsWithin) && !_rider.Aboard && (_bookedLine < 0 || _bus.Stop == null || _bus.Stop.Line == _bookedLine);
                    if ((now >= _dwellUntil && !busy && !held && !wanted) || _bus.Standing > DwellMost + (held ? ChooseFor : 0f))
                    {
                        Leave(now);
                    }
                    break;

                case TransitBus.Phase.Leaving:
                    if (_wasStanding)
                    {
                        Leave(now);   // (it is already leaving: the bookkeeping only)
                    }
                    break;
            }
            RunNext(now);
            _wasDelayed |= _eta.State == TransitEta.Mode.Delayed;
        }

        // The bus after the front one, of whichever line calls next: on its way while the front one stands, so that the
        // place has a bus every SpacingSeconds when its lines run often. Not while the player rides or is getting on.
        private void RunNext(float now)
        {
            if (_journey || _served == null)
            {
                return;
            }
            if (_next.State == TransitBus.Phase.Standing && _bus.State != TransitBus.Phase.Standing && !_rider.Active && !_ride.Riding)
            {
                // The bus behind has pulled in while the front one is not standing here (kept driving off, or gone): it is the front one now.
                HandOff(ref _bus);
                Promote();
                return;
            }
            if (_next.State == TransitBus.Phase.Idle)
            {
                if (_nextCall >= 0 && _nextCall == _frontCall)
                {
                    _nextCall = -1;
                }
                if (_rider.Active || _ride.Riding || _bus.State == TransitBus.Phase.Idle || _player == PlayerStep.Choosing)
                {
                    return;
                }
                int soonest = Soonest();
                if (soonest != _nextCall && !(_nextCall >= 0 && _pinned[_nextCall]))
                {
                    _nextCall = soonest;
                    if (_nextCall >= 0 && _callAt[_nextCall] < double.MaxValue)
                    {
                        _nextEta.Timetable(SecondsTo(_callAt[_nextCall]));
                    }
                    else
                    {
                        _nextEta.NoService();
                    }
                }
                if (_nextCall < 0 || _callAt[_nextCall] >= double.MaxValue)
                {
                    return;
                }
                float seconds = SecondsTo(_callAt[_nextCall]);
                if (seconds <= Lead)
                {
                    _pinned[_nextCall] = true;
                    if (now >= _nextTry2 && Put(_next, _nextEta, _nextCall, ref _nextRefusals, out _))
                    {
                        Logger?.Info("Transit check (bus assigned): the next bus, the " + LineNumber(_next.Stop) + " for " + StopLabel(_next.Stop) + ", is on its way behind the "
                                     + LineNumber(_bus.Stop) + ": it starts " + _next.StartedBack.ToString("F0") + " m of line before the stop, "
                                     + Mathf.Sqrt(CitySpots.Flat(_next.Vehicle.transform.position - PlayerHelper.GetPosition())).ToString("F0") + " m from the player, seen: "
                                     + (_next.InView ? "yes" : "no") + ", held at its start " + _next.Held0.ToString("F0") + " s; it reckons " + _nextEta.Seconds.ToString("F0")
                                     + " s, the timetable " + seconds.ToString("F0") + " s: " + (!_next.InView ? "PASS" : "FAIL (it appears where it can be seen)") + ".");
                    }
                    else if (_next.State == TransitBus.Phase.Idle && now >= _nextTry2)
                    {
                        _nextRefusals++;
                        _nextTry2 = now + RetryEvery;
                    }
                }
                if (_next.State == TransitBus.Phase.Idle)
                {
                    _nextEta.Wait(TickInterval, _nextRefusals > 0);
                }
                return;
            }
            if (_next.State == TransitBus.Phase.Approaching)
            {
                _nextEta.Track(TickInterval, _next.Estimate(_pace), _next.Waiting != TransitBus.Hold.None);
                if (_next.NoProgressFor > GiveUpAfter || _next.Out > GiveUpOut)
                {
                    Logger?.Info("Transit check (re-plan): the next bus, the " + LineNumber(_next.Stop) + ", has got nowhere for " + _next.NoProgressFor.ToString("F0") + " s: it is let go, and the next is sent: FAIL.");
                    HandOff(ref _next);
                    _nextEta.Lost();
                    _nextTry2 = now + 15f;
                }
            }
        }

        // The front bus has gone (or was let go): the one behind it is the front one now, with what it was showing.
        private void Promote()
        {
            if (_bus.State != TransitBus.Phase.Idle || _next.State == TransitBus.Phase.Idle)
            {
                return;
            }
            TransitBus idle = _bus;
            _bus = _next;
            _next = idle;
            _eta.CopyFrom(_nextEta);
            _frontCall = _nextCall;
            _nextCall = -1;
            _nextEta.NoService();
            _promised = _eta.Seconds;
            _promisedAt = Time.time + _eta.Seconds;
            _shownAt = -1f;
            _wasStanding = false;
            _doorFound = false;
            _refusals = _nextRefusals;
            _nextRefusals = 0;
            _moveUpAt = Time.time + MoveUpAfter;
            _routeLogAt = Time.time + 60f;
            _busFull = IsRush() && Random.value < 0.25f;
            _panelKey = int.MinValue;
        }

        private void Unpin(int call)
        {
            if (call >= 0 && call < MostCalls)
            {
                _pinned[call] = false;
            }
        }

        // A bus on the road for a call: as far back along its line as the seconds to the call take to drive, out of sight,
        // and held at its start for what is over, so that it stands at the stop when the timetable says. False, with why.
        private bool Put(TransitBus bus, TransitEta eta, int call, ref int refusals, out string refusal)
        {
            TransitStop stop = _served.Calls[call];
            float seconds = Mathf.Max(0f, SecondsTo(_callAt[call]));
            float prefer = TransitPlan.StartBack(seconds, BusCruise, _pace, LightsPerMetre, StartLeast, StartMost);
            // Not further back than the time allows at first; then as far as a start out of sight can be found.
            float most = refusals < 2 ? prefer + 20f : refusals < 6 ? StartMost : StartFar;
            if (!bus.Send(stop, false, out refusal, most, prefer))
            {
                return false;
            }
            float estimate = bus.Estimate(_pace);
            float hold = seconds - estimate;
            if (hold > 0.5f)
            {
                bus.HoldFor(hold);
                estimate = seconds;
            }
            eta.Assign(estimate);
            refusals = 0;
            return true;
        }

        private bool PlayerAtStop(float within)
        {
            return _served != null && !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding
                   && CitySpots.Flat(_served.Sign - PlayerHelper.GetPosition()) < within * within;
        }

        private static bool IsRush()
        {
            return TransitPlan.IsRush(TimeHelper.CurrentHour, TimeHelper.IsWeekend);
        }

        // A bus has just been put on the road for the place served.
        private void Assigned(float now, bool test)
        {
            _busFull = !test && IsRush() && Random.value < 0.25f;
            _promised = _eta.Seconds;
            _promisedAt = now + _eta.Seconds;
            _routeLogAt = now + 60f;
            _moveUpAt = now + MoveUpAfter;
            _refusals = 0;
            _wasStanding = false;
            _panelKey = int.MinValue;
            Vector3 at = _bus.Vehicle.transform.position;
            float away = Mathf.Sqrt(CitySpots.Flat(at - PlayerHelper.GetPosition()));
            bool seen = _bus.InView;
            bool pass = away >= TransitSight.NearKeep && (test || !seen);   // (the test button may start it in view, far off)
            TransitStop stop = _bus.Stop;
            TransitLine line = TransitMap.Lines[stop.Line];
            Logger?.Info("Transit check (bus assigned): the " + line.Number + " to " + line.To + " for " + StopLabel(stop) + (test ? " (test button)" : "") + ": " + _bus.Chassis
                         + "; it starts " + _bus.StartedBack.ToString("F0") + " m of line before the stop, " + away.ToString("F0") + " m from the player, seen (on the screen, no building between): " + (seen ? "yes" : "no")
                         + "; " + _bus.Remaining.ToString("F0") + " m to drive, " + _bus.DriveAtStart.ToString("F0") + " s of open road as it will drive it, " + _bus.LightsAtStart + " lit junction(s); held at its start "
                         + _bus.Held0.ToString("F0") + " s; the estimate (traffic x" + _pace.Traffic.ToString("F2") + ", " + _pace.Light.ToString("F1") + " s a junction) is "
                         + _eta.Seconds.ToString("F0") + " s, the panel " + (_eta.State == TransitEta.Mode.Delayed ? "DELAYED" : "on time") + "; "
                         + (_bus.InBay ? "it will stand in the stop's bay, " + stop.BayOffset.ToString("F2") + " m off the lane's line"
                             : stop.HasBay ? "it will stop in the lane (" + (_bus.BayNote ?? "the bay cannot be used") + ")"
                             : "the stop has no bay (" + (stop.NoBay ?? "none laid out") + "): it will stop in the lane")
                         + (_bus.SentAway > 0 ? ", " + _bus.SentAway + " of the game's parked cars taken in from the bay out of view" : "")
                         + ": " + (pass ? "PASS" : "FAIL (it appears where it can be seen, or too near)") + ".");
        }

        // The bus on its way: a line a minute, and what is done when it gets nowhere.
        private void Watch(float now)
        {
            float still = _bus.NoProgressFor;
            if (now >= _routeLogAt)
            {
                _routeLogAt = now + 60f;
                Logger?.Info("Transit check (en route): " + _bus.Out.ToString("F0") + " s out, " + _bus.Remaining.ToString("F0") + " m still to drive of " + _bus.StartedBack.ToString("F0")
                             + "; " + WaitingWords() + "; no progress for " + still.ToString("F0") + " s; the panel: " + PanelWords() + ": " + (still < 25f ? "PASS" : "FAIL (it is getting nowhere)") + ".");
            }
            if (still > MoveUpAfter && now >= _moveUpAt && !_bus.InView && !_bus.Near && _bus.Remaining > 110f)
            {
                // Held up where nobody can see it and away from the player: put further up its line, past what holds it, still out of sight.
                _moveUpAt = now + 30f;
                float from = _bus.Remaining;
                string why = WaitingWords();
                TransitStop stop = _bus.Stop;
                _bus.Release(remove: true);
                if (_bus.Send(stop, false, out string refusal, from - 45f))
                {
                    _routeLogAt = now + 60f;
                    Logger?.Info("Transit check (re-plan): the bus had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the stop (" + why
                                 + "), out of sight and away from the player: it is moved up its line to " + _bus.Remaining.ToString("F0") + " m from the stop (" + _bus.Chassis + "): PASS.");
                }
                else
                {
                    _eta.Lost();
                    _refusals = 0;
                    _nextTry = now + RetryEvery;
                    Logger?.Info("Transit check (re-plan): the bus had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the stop (" + why
                                 + "), out of sight, and could not be moved up its line (" + refusal + "): taken off the road, another is sent: FAIL.");
                }
                _panelKey = int.MinValue;
                return;
            }
            if (still > GiveUpAfter || _bus.Out > GiveUpOut)
            {
                Logger?.Info("Transit check (re-plan): the bus has got nowhere for " + still.ToString("F0") + " s and has been out " + _bus.Out.ToString("F0") + " s, " + _bus.Remaining.ToString("F0")
                             + " m from the stop (" + WaitingWords() + "): given up (it drives on and goes once nobody can see it), the next bus is sent: FAIL.");
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_skipped", new Dictionary<string, string>(1) { { "route", LineNumber(_bus.Stop) } }, 8f);
                HandOff(ref _bus);
                _eta.Lost();
                _refusals = 0;
                _nextTry = now + 15f;
                _panelKey = int.MinValue;
            }
        }

        private string WaitingWords()
        {
            switch (_bus.Waiting)
            {
                case TransitBus.Hold.Light: return "standing at a red light for " + _bus.WaitingFor.ToString("F0") + " s";
                case TransitBus.Hold.Traffic: return "standing for " + _bus.WaitingFor.ToString("F0") + " s behind " + _bus.WaitsFor();
                case TransitBus.Hold.Closure: return "standing before a closed road for " + _bus.WaitingFor.ToString("F0") + " s";
                default: return _bus.HoldLeft > 0f ? "held at its start for " + _bus.HoldLeft.ToString("F0") + " s more, to keep the timetable" : "driving";
            }
        }

        private string PanelWords()
        {
            return _eta.State == TransitEta.Mode.Delayed ? "DELAYED" + (_eta.Known ? ", " + _eta.Seconds.ToString("F0") + " s" : string.Empty)
                : _eta.State == TransitEta.Mode.Here ? "at the stop" : _eta.State == TransitEta.Mode.None ? "no service" : _eta.Seconds.ToString("F0") + " s (" + _eta.State + ")";
        }

        private static string LineNumber(TransitStop stop)
        {
            return stop != null && stop.Line >= 0 && stop.Line < TransitMap.Lines.Count ? TransitMap.Lines[stop.Line].Number : "bus";
        }

        // The bus has just stopped: where it stands, where its door is, who gets out, whether those waiting can reach it.
        private void Arrived(float now)
        {
            _dwellUntil = now + DwellLeast;
            TransitStop stop = _bus.Stop ?? _served;
            TransitLine line = TransitMap.Lines[stop.Line];
            if (!Riding && PlayerAtStop(WaitsWithin))
            {
                // Said once as it pulls in: the bus is a small one, and it is not obvious that it is theirs.
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_in", new Dictionary<string, string>(1) { { "route", line.Number } }, 8f);
            }
            _doorFound = false;
            _boardable = false;
            _boarded = 0;
            _alighted = 0;
            BusVehicle car = _bus.Vehicle;
            // Where it stands. In its bay the measure is the bay's: nothing of it in the lane, nothing over the kerb, parallel
            // to it. On the lane's line it is the lane's, as it always was.
            _bus.Measure(out float along, out float aside, out float angle, out float toKerb, out float toLane);
            bool inBay = _bus.InBay;
            bool placed = inBay ? Mathf.Abs(along) <= 2.5f && toLane >= -0.15f && toKerb >= 0f && angle <= 3f   // (its rear corner a hand's breadth in the lane at the narrowest bay: NavCheck)
                                : Mathf.Abs(along) <= 2.5f && Mathf.Abs(aside) <= 1.2f && angle <= 12f;
            string held = _bus.StoodShort;   // (held up short of its place, it is at the stop where it is: TransitBus.OnTheWay)
            Logger?.Info("Transit check (arrival): the " + line.Number + " stands at " + StopLabel(stop) + (inBay ? " in its bay" : "") + " with its middle " + Mathf.Abs(along).ToString("F1") + " m "
                         + (along >= 0f ? "past" : "short of") + (Mathf.Abs(stop.Stand) > 0.01f ? " its place " + Mathf.Abs(stop.Stand).ToString("F1") + " m " + (stop.Stand > 0f ? "on from" : "short of") + " the pole" : " the pole") + ", " + Mathf.Abs(aside).ToString(inBay ? "F2" : "F1") + " m " + (aside >= 0f ? "towards the kerb" : "away from the kerb")
                         + " off the lane's line" + (inBay ? " (the bay's line is " + stop.BayOffset.ToString("F2") + ")" : "") + ", " + angle.ToString(inBay ? "F1" : "F0")
                         + " degrees off the lane's direction, door side to the kerb with " + toKerb.ToString(inBay ? "F2" : "F1") + " m of road between"
                         + (inBay ? ", its body " + toLane.ToString("F2") + " m clear of the lane's edge (a lorry on the lane's line passes " + (toLane + BusBay.LaneHalf - BusBay.WidestHalf).ToString("F2")
                                    + " m from it): the lane is " + (toLane >= 0f ? "free" : "NOT free")
                             : stop.HasBay ? ", in the lane (" + (_bus.BayNote ?? "the bay was not used") + ")"
                             : ", in the lane (no bay here: " + (stop.NoBay ?? "none laid out") + ", the kerb " + stop.KerbAside.ToString("F2") + " m from the lane's line)")
                         + "; " + _bus.Took.ToString("F0") + " s after setting out " + _bus.StartedBack.ToString("F0") + " m back"
                         + (held != null ? "; " + held : string.Empty) + ": "
                         + (placed ? "PASS" : held != null ? "PASS (where it was held up: it counts as standing at the stop)"
                             : inBay ? "FAIL (allowed: 2.5 m along, 3 degrees, nothing of it in the lane or over the kerb)" : "FAIL (allowed: 2.5 m along, 1.2 m aside, 12 degrees)") + ".");
            OnTime(now, line, stop);
            if (_frontCall >= 0 && _frontCall < MostCalls)
            {
                _doneAt[_frontCall] = NowMinutes();
                _lastCall = NowMinutes();
                Unpin(_frontCall);
            }

            // The door: where somebody stands to step in. On the road surface, clear of every other vehicle; a little further out
            // from the body if the mesh does not come that close to it.
            _bus.DoorPlace(out Vector3 want, out _doorInwards);
            float out_ = 0f;
            for (int step = 0; step < 4 && !_doorFound; step++)
            {
                out_ = step * 0.3f;
                Vector3 place = want - _doorInwards * out_;
                if (StreetSpots.OnRoad(place, 0.5f, out Vector3 door) && CitySpots.Flat(door - place) < 0.3f * 0.3f && JobSpots.Clear(door, car.transform))
                {
                    _door = door;
                    _doorFound = true;
                }
            }
            bool wayClear = _doorFound && WayClear(_served.Wait, _door, car);
            // Those who get off do so by the middle door, as the player does: where it is on the road and clear, else by the front one.
            _exitDoor = _door;
            _bus.MiddleDoorPlace(out Vector3 exitWant, out _);
            if (_doorFound && StreetSpots.OnRoad(exitWant, 0.5f, out Vector3 exitAt) && CitySpots.Flat(exitAt - exitWant) < 0.3f * 0.3f && JobSpots.Clear(exitAt, car.transform)
                && WayClear(_served.Wait, exitAt, car))
            {
                _exitDoor = exitAt;
            }
            int waiting = 0;
            int seated = 0;
            for (int i = 0; i < Waiters; i++)
            {
                waiting += _waiting[i].Step == RiderStep.Waiting || _waiting[i].Step == RiderStep.Arriving ? 1 : 0;
                seated += _waiting[i].Sat ? 1 : 0;
            }
            bool sat = _seat.Seated && (_bookedLine < 0 || stop.Line == _bookedLine);
            Logger?.Info("Transit check (doors): doors open; a place to step in " + (_doorFound ? "found " + out_.ToString("F1") + " m further out than the door wants" : "NOT found on the road surface beside the door")
                         + "; the way from the kerb to it " + (!_doorFound ? "not looked at" : wayClear ? "clear" : "has vehicles standing in it") + "; " + waiting + " waiting (" + seated + " seated)"
                         + (sat ? ", the player seated and getting up" : "") + "; the bus is " + (_busFull ? "full: no seat free, whoever boards stands" : "not full") + "; it stands "
                         + (_bus.InBay ? "out of the lane" : "in the lane") + ": "
                         + (_doorFound && wayClear ? "PASS" : "FAIL (nobody on foot gets in or out this time; the player still boards from the stop)") + ".");
            if (!wayClear)
            {
                _doorFound = false;
            }
            _boardable = _doorFound;   // (a full bus has no seat free: whoever gets on stands)
            if (sat)
            {
                _seat.Stand(now);   // the bus is here: up off the bench
            }
            if (_doorFound && Random.value < 0.6f && !(_journey && _served == (_jOff != null ? _jOff.At : null)))
            {
                Alight(now);   // (not where the player gets off: the middle door is theirs)
            }
            // How long it stands at the least. In the lane it is in the way of everything behind it, so where nobody at all
            // has business with it (nobody waiting or walking up, nobody getting out, the player not near and not aboard
            // or choosing) the doors open and shut and it goes. Out of the lane, in its bay, it keeps the usual stand.
            bool nobody = waiting == 0 && !sat && _alighter.Step == RiderStep.None && _player != PlayerStep.Choosing && !Riding && !PlayerAtStop(WaitsWithin);
            _dwellLeast = toLane < 0f && nobody ? DwellEmpty : DwellLeast;
            _dwellUntil = now + _dwellLeast;
            _panelKey = int.MinValue;
        }

        // The promise against the arrival, for the record, and what the arrival teaches the estimate (TransitPlan.Pace).
        private void OnTime(float now, TransitLine line, TransitStop stop)
        {
            float took = _bus.Took;
            if (took <= 0f || _promisedAt <= 0f)
            {
                return;   // (put standing there: the arrival of a ride, nothing promised)
            }
            float moving = took - _bus.Held0 - _bus.AtLights;
            float before = _pace.Traffic;
            _pace.Learn(_bus.DriveAtStart, _bus.LightsAtStart, moving, _bus.AtLights);
            float off = now - _promisedAt;
            float slack = TransitPlan.Slack(_promised);
            string shown = _shownAt > 0f ? "; the panel first showed the player " + _shownFigure.ToString("F0") + " s, off by " + (now - _shownAt).ToString("+0;-0") + " s" : string.Empty;
            Logger?.Info("Transit check (on time): the " + line.Number + " at " + StopLabel(stop) + " was promised for " + _promised.ToString("F0") + " s after it set out (" + (_bus.Held0 > 0.5f ? "held "
                         + _bus.Held0.ToString("F0") + " s at its start, " : string.Empty) + _bus.DriveAtStart.ToString("F0") + " s of open road, " + _bus.LightsAtStart + " lit junction(s)) and stood there after "
                         + took.ToString("F0") + " s (" + _bus.AtLights.ToString("F0") + " s of it at red lights): " + off.ToString("+0;-0") + " s against the promise" + shown + "; the estimate learns traffic x"
                         + before.ToString("F2") + " to x" + _pace.Traffic.ToString("F2") + ", " + _pace.Light.ToString("F1") + " s a junction (" + _pace.Samples + " bus(es)); the panel said DELAYED: "
                         + (_eta.State == TransitEta.Mode.Delayed || _wasDelayed ? "yes" : "no") + ": " + (Mathf.Abs(off) <= slack ? "PASS" : "FAIL (more than " + slack.ToString("F0") + " s out)") + ".");
            _promisedAt = 0f;
            _wasDelayed = false;
        }

        // Nothing but the bus stands on the straight way from the kerb to its door.
        private bool WayClear(Vector3 from, Vector3 to, BusVehicle bus)
        {
            int mask = TrafficTools.VehicleMask;
            if (mask == 0)
            {
                return true;
            }
            int count = Physics.OverlapCapsuleNonAlloc(from + Vector3.up * 0.9f, to + Vector3.up * 0.9f, 0.32f, _touching, mask, QueryTriggerInteraction.Ignore);
            bool clear = count < _touching.Length;
            for (int i = 0; i < count; i++)
            {
                if (clear && _touching[i] != null && !_touching[i].transform.IsChildOf(bus.transform))
                {
                    clear = false;
                }
                _touching[i] = null;
            }
            return clear;
        }

        private void Leave(float now)
        {
            _wasStanding = false;
            _doorFound = false;
            if (_rider.Now == BusRider.Step.ToDoor)
            {
                _rider.Cancel("the bus left before they reached its door");
            }
            if (_bus.State == TransitBus.Phase.Standing)
            {
                float stood = _bus.Standing;
                _bus.Depart();
                if (_rider.Aboard && _rider.Stage == BusInside.Stage.Aboard)
                {
                    _rider.Departing();   // (no getting off now: the pull-away is watched)
                }
                bool sound = stood <= DwellMost + ChooseFor + 2f;
                bool waits = _bus.AwaitingGap;   // in its bay: it goes when the lane is clear, and the line is written then (RunBus)
                _bus.DepartureNote = null;
                _bus.DepartureSound = sound;
                if (Logger != null)
                {
                    string said = "Transit check (departure): the " + LineNumber(_bus.Stop) + " " + (waits ? "closes its doors" : "pulls away in the lane") + " after standing " + stood.ToString("F0") + " s ("
                                  + _dwellLeast.ToString("F0") + " to " + DwellMost.ToString("F0") + " allowed); " + _boarded + " got in, " + _alighted + " got out, the player "
                                  + (Riding ? "is aboard" : _player == PlayerStep.Choosing ? "was still choosing a stop" : "did not board");
                    if (waits)
                    {
                        _bus.DepartureNote = said;
                    }
                    else
                    {
                        Logger.Info(said + ": " + (sound ? "PASS" : "FAIL") + ".");
                    }
                }
            }
            _busFull = false;
            // Whoever was still on the way to the door goes back to waiting.
            for (int i = 0; i < Waiters; i++)
            {
                if ((_waiting[i].Step == RiderStep.Boarding || _waiting[i].Step == RiderStep.Rising) && _waiting[i].Person != null && _waiting[i].Person.IsValid)
                {
                    Unseat(ref _waiting[i]);
                    _waiting[i].Person.ClearPose();
                    _waiting[i].Step = _waiting[i].Person.Place(_waiting[i].Person.Position, true) && _waiting[i].Person.MoveTo(_served.Wait, Pace.Walk) ? RiderStep.Arriving : RiderStep.Waiting;
                    _waiting[i].Spot = _served.Wait;
                }
            }
            if (_player == PlayerStep.Choosing)
            {
                _player = PlayerStep.None;
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_missed", null, 6f);
            }
            // The bus drives off on its own (TransitBus: on along its line and the road, taken off once nobody can see it), and
            // the next one is the front one: unless the player is on it, or getting on or off, when it stays the director's.
            if (!_journey && !_rider.Active && !_ride.Riding && _offBus != _bus && _away.Count < AwayMost)
            {
                _away.Add(_bus);
                _bus = Spare();
                _frontCall = -1;
                _eta.NoService();
                Promote();
            }
            _panelKey = int.MinValue;
        }

        // ---------------------------------------------------------------- the people

        private void RunRiders(float now)
        {
            if (_served == null)
            {
                return;
            }
            bool standing = _bus.State == TransitBus.Phase.Standing;

            // Somebody new walks up now and then, while no bus stands.
            if (!standing && now >= _nextWaiter && Settings.TransitPeople)
            {
                _nextWaiter = now + Random.Range(35f, 75f);
                for (int i = 0; i < Waiters; i++)
                {
                    if (_waiting[i].Step == RiderStep.None)
                    {
                        Arrive(i);
                        break;
                    }
                }
            }

            bool doorBusy = _alighter.Step == RiderStep.Exiting || _rider.AtDoor;   // (the player on their way to the front door has it first)
            for (int i = 0; i < Waiters; i++)
            {
                doorBusy |= _waiting[i].Step == RiderStep.Boarding || _waiting[i].Step == RiderStep.Entering;
            }
            for (int i = 0; i < Waiters; i++)
            {
                StreetActor person = _waiting[i].Person;
                if (_waiting[i].Step == RiderStep.None)
                {
                    continue;
                }
                if (person == null || !person.IsValid || person.Limp || !person.InUse)
                {
                    Drop(ref _waiting[i], _waiters);   // knocked down, or gone with the scene: no longer ours to move
                    continue;
                }
                switch (_waiting[i].Step)
                {
                    case RiderStep.Arriving:
                        if (person.Within(0.3f))
                        {
                            Vector3 here = person.Position;
                            BusStopDressing shelter = _waiting[i].Seat >= 0 ? DressingOf(_served) : null;
                            if (shelter != null && shelter.Sheltered && CitySpots.Flat(here - _waiting[i].Spot) < 0.6f * 0.6f)
                            {
                                // Their seat: feet where the clip wants them, back to the bench, down, and then the seated wait.
                                person.StandAt(_waiting[i].Spot, _waiting[i].Spot + shelter.SeatFacing * 3f);
                                _waiting[i].Sat = person.Work("sit_bench", ClipEnd.Hold, "sit_wait");
                                if (!_waiting[i].Sat)
                                {
                                    person.Work("queue_wait");
                                }
                            }
                            else
                            {
                                Unseat(ref _waiting[i]);
                                person.StandAt(here, here - Vector3.Cross(Vector3.up, _served.Along) * 3f + _served.Along * -2f);
                                person.Work("queue_wait");
                            }
                            _waiting[i].Step = RiderStep.Waiting;
                        }
                        break;

                    case RiderStep.Waiting:
                        if (standing && _boardable && !doorBusy && _doorFound && _bus.Standing > 1.5f)
                        {
                            if (_waiting[i].Sat && person.Work("sit_bench_up") && ClipLibrary.TryGet("sit_bench_up", out CityClip up))
                            {
                                _waiting[i].Step = RiderStep.Rising;
                                _waiting[i].Until = now + up.Length;
                                doorBusy = true;   // the door is theirs: nobody sets off for it while they get up
                                break;
                            }
                            ToDoor(ref _waiting[i], now);
                            doorBusy |= _waiting[i].Step == RiderStep.Boarding;
                        }
                        break;

                    case RiderStep.Rising:
                        if (now >= _waiting[i].Until)
                        {
                            if (standing && _boardable && _doorFound)
                            {
                                ToDoor(ref _waiting[i], now);
                            }
                            else
                            {
                                Unseat(ref _waiting[i]);
                                person.ClearPose();
                                person.Work("queue_wait");
                                _waiting[i].Step = RiderStep.Waiting;
                            }
                        }
                        break;

                    case RiderStep.Boarding:
                        if (!standing || now > _waiting[i].Until)
                        {
                            _waiting[i].Spot = _served.Wait;
                            _waiting[i].Step = person.MoveTo(_waiting[i].Spot, Pace.Walk) ? RiderStep.Arriving : RiderStep.Waiting;
                            break;
                        }
                        if (person.Within(0.15f))
                        {
                            Vector3 here = person.Position;
                            if (CitySpots.Flat(here - _door) > 0.4f * 0.4f)
                            {
                                break;   // still closing on it, or held up: the clip is made for that one place
                            }
                            person.StandAt(here, here + _doorInwards * 3f);
                            // A bus has a step and a rail; the plain van a sliding door.
                            string clipName = _bus.Modelled && ClipLibrary.Has("board_step") ? "board_step" : "job_car_enter";
                            bool clip = ClipLibrary.TryGet(clipName, out CityClip enter) && person.Work(clipName, ClipEnd.Hold);
                            _waiting[i].Until = now + (clip ? enter.Length : 0.4f);
                            _waiting[i].Step = RiderStep.Entering;
                        }
                        break;

                    case RiderStep.Entering:
                        if (now >= _waiting[i].Until)
                        {
                            JobCast.DoorSound(person.Position);
                            Drop(ref _waiting[i], _waiters);
                            _boarded++;
                            _dwellUntil = Mathf.Max(_dwellUntil, now + 2f);
                        }
                        break;
                }
            }

            if (_alighter.Step == RiderStep.None)
            {
                return;
            }
            StreetActor out_ = _alighter.Person;
            if (out_ == null || !out_.IsValid || out_.Limp || !out_.InUse)
            {
                Drop(ref _alighter, _others);
                return;
            }
            if (_alighter.Step == RiderStep.Exiting && now >= _alighter.Until)
            {
                out_.ClearPose();
                _alighter.Step = RiderStep.Leaving;
                _alighter.Until = now + 60f;
                if (!out_.Place(out_.Position, true) || !out_.MoveTo(_alighter.Spot, Pace.Walk))
                {
                    _alighter.Until = now + 4f;
                }
            }
            else if (_alighter.Step == RiderStep.Leaving && ((out_.Within(0.4f) && !CitySpots.InView(out_.Position)) || now >= _alighter.Until))
            {
                Drop(ref _alighter, _others);
            }
        }

        // Off to the door: they came along the pavement (or off the bench); the last steps are on the road surface.
        private void ToDoor(ref Rider rider, float now)
        {
            StreetActor person = rider.Person;
            Unseat(ref rider);
            person.ClearPose();
            if (person.Place(person.Position, true) && person.MoveTo(_door, Pace.Walk))
            {
                rider.Step = RiderStep.Boarding;
                rider.Until = now + 16f;
            }
            else
            {
                Vector3 here = person.Position;
                person.StandAt(here, here + _doorInwards * -3f);
                rider.Step = RiderStep.Waiting;
            }
        }

        // Somebody walks up to the stop from out of view and waits: on the bench if a seat is free and the clips are there, else standing by the pole.
        private void Arrive(int slot)
        {
            Vector3 right = Vector3.Cross(Vector3.up, _served.Along);
            Vector3 want = _served.Wait - _served.Along * (slot * 1.15f) + right * (slot * 0.25f);
            if (!CitySpots.Stand(want, 0.35f, out Vector3 spot))
            {
                return;
            }
            int seat = -1;
            BusStopDressing shelter = DressingOf(_served);
            if (shelter != null && shelter.Sheltered && TransitSeat.Possible && Random.value < 0.6f)
            {
                // A seat nobody has (the player's, or the other waiter's, is taken): theirs from now until they board or go.
                for (int i = BusStopDressing.Seats - 1; i >= 0 && seat < 0; i--)
                {
                    if (shelter.SeatFree(i) && NavMesh.SamplePosition(shelter.SeatFeetAt(i), out NavMeshHit hit, 0.12f, NavMesh.AllAreas))
                    {
                        seat = i;
                        spot = hit.position;
                    }
                }
            }
            Vector3 from = spot;
            bool walksIn = CitySpots.InView(spot);
            if (walksIn && (!CitySpots.PavementRing(spot, 26f, 44f, true, out from) || !CitySpots.Reachable(from, spot)))
            {
                return;   // nowhere out of sight to come from: nobody comes just now
            }
            if (_waiters == null)
            {
                _waiters = new JobCast(this, Logger);
            }
            StreetActor person = _waiters.Put(from, false);   // on the pavement only, until the bus is there
            if (person == null)
            {
                return;
            }
            _waiting[slot].Person = person;
            _waiting[slot].Spot = spot;
            _waiting[slot].Seat = seat;
            _waiting[slot].Sat = false;
            if (seat >= 0)
            {
                shelter.TakeSeat(seat, false);
            }
            if (walksIn && person.MoveTo(spot, Pace.Walk))
            {
                _waiting[slot].Step = RiderStep.Arriving;
                return;
            }
            if (seat >= 0)
            {
                person.StandAt(spot, spot + shelter.SeatFacing * 3f);
                _waiting[slot].Sat = person.Work("sit_bench", ClipEnd.Hold, "sit_wait");
                if (!_waiting[slot].Sat)
                {
                    person.Work("queue_wait");
                }
            }
            else
            {
                person.StandAt(spot, spot - right * 3f - _served.Along * 2f);
                person.Work("queue_wait");
            }
            _waiting[slot].Step = RiderStep.Waiting;
        }

        // Somebody gets out at the door and walks away along the pavement.
        private void Alight(float now)
        {
            if (!Settings.TransitPeople || _alighter.Step != RiderStep.None
                || !CitySpots.PavementRing(_served.Wait, 30f, 48f, true, out Vector3 away) || !CitySpots.Reachable(_served.Wait, away))
            {
                return;
            }
            if (_others == null)
            {
                _others = new JobCast(this, Logger);
            }
            StreetActor person = _others.Put(_exitDoor, true);
            if (person == null)
            {
                return;
            }
            person.StandAt(_exitDoor, _exitDoor - _doorInwards * 3f);
            // Out of the van's sliding door there is a clip; down a bus's step there is none: they are simply on the road, and walk.
            bool clip = !_bus.Modelled && ClipLibrary.TryGet("job_car_exit", out CityClip exit) && person.Work("job_car_exit");
            JobCast.DoorSound(_exitDoor);
            _alighter.Person = person;
            _alighter.Spot = away;
            _alighter.Seat = -1;
            _alighter.Sat = false;
            _alighter.Step = RiderStep.Exiting;
            _alighter.Until = now + (clip && ClipLibrary.TryGet("job_car_exit", out CityClip played) ? played.Length : 0.8f);
            _alighted++;
        }

        // Their seat is anybody's again.
        private void Unseat(ref Rider rider)
        {
            if (rider.Seat >= 0)
            {
                BusStopDressing shelter = DressingOf(_served);
                if (shelter != null)
                {
                    shelter.LeaveSeat(rider.Seat);
                }
            }
            rider.Seat = -1;
            rider.Sat = false;
        }

        private void Drop(ref Rider rider, JobCast cast)
        {
            Unseat(ref rider);
            if (cast != null && rider.Person != null)
            {
                cast.Release(rider.Person);
            }
            rider.Person = null;
            rider.Step = RiderStep.None;
        }

        private void ReleaseRiders()
        {
            for (int i = 0; i < Waiters; i++)
            {
                Drop(ref _waiting[i], _waiters);
            }
            Drop(ref _alighter, _others);
            _waiters?.ReleaseAll();
            _others?.ReleaseAll();
        }

        /// <summary>Everything of the buses off the street: people, buses, and (with <paramref name="props"/>) the dressed stops.</summary>
        private void ClearStreet(bool props)
        {
            _rider.Abort("everything of the buses is taken off the street", put: true);   // (not while the ride's time passes: the ride has the player then)
            _offBus = null;
            _seat.Abort();
            _sitAt = null;
            ReleaseRiders();
            _bus.Release(remove: true);
            _bus.TakeLost();
            _next.Release(remove: true);
            _next.TakeLost();
            for (int i = 0; i < _away.Count; i++)
            {
                _away[i].Release(remove: true);
                _spare.Add(_away[i]);
            }
            _away.Clear();
            _seenOff.Release(remove: true);
            if (_changing)
            {
                DropPlan();   // (off the street at the change: the journey ends there; the fare of it still covers a bus for a while)
                _booked = null;
                _bookedLine = -1;
            }
            _served = null;
            _frontCall = -1;
            _nextCall = -1;
            _calls = 0;
            _wasStanding = false;
            _doorFound = false;
            _refusals = 0;
            if (_player != PlayerStep.Riding)
            {
                _player = PlayerStep.None;
            }
            if (props)
            {
                for (int i = 0; i < Dressings; i++)
                {
                    _dressings[i]?.Clear();
                }
                _dressAtOnce = true;
                BusZones.Restore();   // the buses are off: the parking and the furniture as the game had them
            }
            _streetClear = true;
        }

        // (The player's side, from pointing at a stop to the ride, is in TransitDirector.Player.cs.)

        // ---------------------------------------------------------------- the test buttons

        private bool TestReady(string test, out Vector3 at)
        {
            at = default;
            TestRefusal why = TestRefusals.Street();
            if (why == TestRefusal.None && (!Settings.Buses || _failed))
            {
                why = TestRefusal.FeatureOff;
            }
            if (why == TestRefusal.None && Riding)
            {
                why = TestRefusal.SceneActive;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            TransitMap.Complete();   // a press before the layout has finished: finished now
            if (TransitMap.Stops.Count == 0)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NoBusLine);
                return false;
            }
            at = PlayerHelper.PlayerController.transform.position;
            return true;
        }

        /// <summary>Options button: dresses the nearest stop now, in view or not, and says where it is.</summary>
        public void TestDress()
        {
            if (!TestReady("Bus stop", out Vector3 at))
            {
                return;
            }
            TransitStop stop = TransitMap.Nearest(at, 600f);
            if (stop == null)
            {
                TestRefusals.Tell(Logger, "Bus stop", TestRefusal.NoBusStop, 600);
                return;
            }
            DressNow(stop, at);
            int metres = Mathf.RoundToInt(Mathf.Sqrt(CitySpots.Flat(stop.Sign - at)));
            BusStopDressing dressed = DressingOf(stop);
            Logger?.Info("Bus stop test: the stop at " + StopName(stop) + " (" + TransitMap.Numbers(stop, ", ") + ", " + (stop.Shelter ? "with room for a shelter" : "pole only")
                         + (stop.Parking ? ", a parking lane between lane and kerb" : "") + ") is dressed, " + metres + " m away" + (dressed != null ? "; " + dressed.BayFound() : "")
                         + "; its check line follows in a moment.");
            Notifications.Show(NotificationType.Info, "reallife_transit_test_dressed",
                new Dictionary<string, string>(2) { { "name", StopName(stop) }, { "metres", metres.ToString() } }, 8f);
        }

        // Dresses a stop here and now, in view or not, taking the dressing furthest from the player if all are out.
        private void DressNow(TransitStop stop, Vector3 at)
        {
            if (Dressed(stop))
            {
                return;
            }
            int slot = 0;
            float furthest = -1f;
            for (int i = 0; i < Dressings; i++)
            {
                float sqr = _dressings[i] == null || _dressings[i].Stop == null ? float.MaxValue : CitySpots.Flat(_dressings[i].Stop.Sign - at);
                if (sqr > furthest)
                {
                    furthest = sqr;
                    slot = i;
                }
            }
            if (_dressings[slot] == null)
            {
                _dressings[slot] = new BusStopDressing(_root, slot + 1);
            }
            if (_seat.At == _dressings[slot])
            {
                _seat.Abort();
            }
            if (_dressings[slot].Stop == _served && _served != null)
            {
                EndService();
            }
            PutDressing(_dressings[slot], stop.At);
            _streetClear = false;
        }

        /// <summary>Options button: brings a bus to the nearest stop now, or says exactly why it cannot.</summary>
        public void TestBus()
        {
            if (!TestReady("Bus", out Vector3 at))
            {
                return;
            }
            // Only a stop near enough to be served: further off its bus would be let go again before it came.
            TransitStop stop = _served ?? NearestBoardable(at, ServeKeep - 5f);
            if (stop == null)
            {
                TestRefusals.Tell(Logger, "Bus", TestRefusal.NoBusStop, Mathf.RoundToInt(ServeKeep - 5f));
                return;
            }
            if (_bus.State != TransitBus.Phase.Idle)
            {
                TestRefusals.Tell(Logger, "Bus", TestRefusal.SceneActive, 0, "a bus is already " + _bus.State + ", " + _bus.Remaining.ToString("F0") + " m from the stop");
                return;
            }
            if (_served != stop)
            {
                if (_served != null)
                {
                    EndService();
                }
                DressNow(stop, at);
                if (!Dressed(stop))
                {
                    TestRefusals.Tell(Logger, "Bus", TestRefusal.NothingFits, 0, "the stop could not be dressed");
                    return;
                }
                BeginService(stop, Time.time);
                _nextWaiter = Time.time + 1f;
            }
            // The first of its lines: in view if it must be (the button is pressed to see it); out of view is tried first.
            _frontCall = 0;
            _pinned[0] = true;
            _callAt[0] = NowMinutes() + BusLead * _minutesPerSecond;
            _eta.NoService();
            TransitStop call = _served.Calls.Count > 0 ? _served.Calls[0] : stop;
            if (!_bus.Send(call, false, out string refusal) && !_bus.Send(call, true, out refusal))
            {
                TestRefusals.Tell(Logger, "Bus", TestRefusal.NothingFits, 0, refusal);
                return;
            }
            _eta.Assign(_bus.Estimate(_pace));
            Assigned(Time.time, true);
            Notifications.Show(NotificationType.Info, "reallife_transit_test_bus", new Dictionary<string, string>(1) { { "name", StopName(stop) } }, 8f);
        }

        /// <summary>Options button: rides from the nearest stop to the stop two further along its line (or its last; from a line's last stop, to the nearest stop of another line), fare and all.</summary>
        public void TestRide()
        {
            if (!TestReady("Bus ride", out Vector3 at))
            {
                return;
            }
            TransitStop from = NearestBoardable(at, 600f);
            if (from == null)
            {
                TestRefusals.Tell(Logger, "Bus ride", TestRefusal.NoBusStop, 600);
                return;
            }
            if (!TransitRide.CanStart())
            {
                TestRefusals.Tell(Logger, "Bus ride", TestRefusal.SceneActive, 0, "a menu, a dialog or another journey is in the way");
                return;
            }
            // Two stops along the line where it has them; from a line's last stop, the nearest stop of another line.
            int direct = TransitTrips.From(from, _trips, NowMinutes(), Calls);
            if (_trips.Count == 0)
            {
                TestRefusals.Tell(Logger, "Bus ride", TestRefusal.NothingFits, 0, "the network has no other stop to ride to");
                return;
            }
            TransitTrip trip = _trips[direct > 0 ? Mathf.Min(1, direct - 1) : 0];
            if (!TransitState.PayBus(out bool pass))
            {
                return;
            }
            StartRide(trip.Board ?? from, trip, pass, "is put on the bus by the test button");
        }

        private static TransitStop NearestBoardable(Vector3 at, float within)
        {
            TransitStop nearest = null;
            float best = within * within;
            List<TransitStop> stops = TransitMap.Places;
            for (int i = 0; i < stops.Count; i++)
            {
                float sqr = CitySpots.Flat(stops[i].Sign - at);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = stops[i];
                }
            }
            return nearest;
        }

        /// <summary>Options button: the subway journey from the nearest station to the next one in the game's list.</summary>
        public void TestSubway()
        {
            _subway.Test();
        }
    }
}
