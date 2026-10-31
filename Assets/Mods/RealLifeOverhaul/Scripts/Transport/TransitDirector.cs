using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using UI.Load;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Buses: puts the network (<see cref="TransitMap"/>) on the street. Every place is dressed once, nearest first; the nearest
    /// within 80 m is served, with people waiting and buses on the timetable (<see cref="TransitBus"/>), put on the road out of
    /// sight. The places' dressings are built behind the loading screen, so putting one up in play makes nothing. The player's
    /// side is in the Player and Journey parts. Every step writes one "Transit check (...)" line with PASS or FAIL. Cost: one
    /// comparison a frame and a 0.25 s timer; per frame only the rider, the journey and a key while seated. A fault switches
    /// buses off for the session, after putting everything away.
    /// </summary>
    public sealed partial class TransitDirector : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("transport", "TransitDirector", typeof(TransitDirector));

        private const float TickInterval = 0.25f;   // seconds
        private const float SolidWithin = 60f;      // metres: on foot this near, a dressed stop's boxes cut the navigation mesh
        private const float SolidKeep = 90f;        // and stay cut until this far off
        private const float ServeRange = 80f;       // metres within which the nearest dressed place is served
        private const float ServeKeep = 105f;       // and beyond which it is served no longer
        private const float SitRange = 1.5f;         // metres from a seat's place at which sitting is offered
        private const float BusLead = 30f;           // seconds before it is due that the test button's bus is put on the road
        private const float ChangeKeep = 260f;       // metres: on a journey's change the place of the next bus is kept while the player walks to it
        private const int MostCalls = 8;             // lines calling at one place, at the most, that are timetabled there
        private const float BusCruise = 11f;         // m/s on an open road (TransitBus)
        private const float LightsPerMetre = 1f / 150f;   // lit junctions a metre of line, about: Manhattan's blocks
        private const float StartLeast = 60f;        // metres of line before the stop a bus may start from, at the least
        private const float StartMost = 330f;        // and at most, at first
        private const float StartFar = 520f;         // and once nothing nearer is out of sight
        private const int AwayMost = 4;              // buses driving away after their stop at one time, at the most
        private const float WaitsWithin = 35f;       // metres from the stop within which a bus waits for the player on foot, up to DwellMost
        private const float RetryEvery = 2f;         // seconds between tries when no bus could be put on the road
        private const float MoveUpAfter = 40f;       // seconds without progress, out of view, before a bus is moved up its line
        private const float GiveUpAfter = 150f;      // seconds without progress before a bus is given up
        private const float GiveUpOut = 300f;        // or this long on the road without arriving
        private const float DwellLeast = 9f;         // seconds a bus stands at a stop, at the least
        private const float DwellEmpty = 4f;         // where it stands in the lane and nobody waits, gets out, or is near: doors open and shut
        private const float DwellMost = 34f;         // seconds at the most
        private const float ChooseFor = 30f;         // seconds the bus waits for the player to pick a stop
        private const int Dressings = 48;            // every place of the city stands while the city is loaded (26 in the city as it is)
        private const int PutsATick = 6;             // places put up in one pass at the most, nearest first
        private const double PutsSliceMs = 1.0;      // milliseconds of a tick the putting up may take in play; the first place of a tick always goes up
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
            public bool Sat;        // and are sitting on
        }

        public static IModLogger Logger;
        public static TransitDirector Instance { get; private set; }

        /// <summary>True while the player is aboard a bus, from the step up at its door until they are off at the far end.</summary>
        public static bool Riding => Instance != null && ((Instance._ride != null && Instance._ride.Riding) || (Instance._rider != null && Instance._rider.Aboard));

        /// <summary>True while the stop's panel, on the game's interact keys, is up: the mod's enter-a-door key stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._panel.Showing || Instance._keyFrame == Time.frameCount);

        private readonly BusStopDressing[] _dressings = new BusStopDressing[Dressings];
        private readonly Rider[] _waiting = new Rider[Waiters];
        private readonly Collider[] _touching = new Collider[12];
        private readonly StringBuilder _text = new StringBuilder(320);
        private readonly TransitEta _eta = new TransitEta();
        private readonly TransitSeat _seat = new TransitSeat();
        private BusRider _rider;
        private TransitBus _offBus;      // the bus the player has just stepped down from at the far end: it goes once they are clear
        private float _offAt;
        private TransitBus _bus;         // the front bus: on its way to the place served for its line's call, or standing there
        private TransitBus _next;        // the bus behind it, of whichever line calls next
        private readonly TransitEta _nextEta = new TransitEta();
        private readonly List<TransitBus> _away = new List<TransitBus>(AwayMost);   // buses driving off after their stop, until nobody can see them
        private readonly List<TransitBus> _spare = new List<TransitBus>(AwayMost);  // and idle again
        private readonly TransitPlan.Pace _pace = new TransitPlan.Pace();        // how the buses really drive, learnt this session
        private readonly double[] _callAt = new double[MostCalls];   // per line calling at the place served: when it next calls there, game minutes
        private readonly bool[] _pinned = new bool[MostCalls];       // kept as it is: it is due, or a bus is on its way for it
        private readonly double[] _doneAt = new double[MostCalls];   // when a bus of it last stood there
        private readonly double[] _sortAt = new double[MostCalls];
        private readonly int[] _sortWho = new int[MostCalls];
        private double _lastCall = double.MinValue;   // when a bus last stood at the place served
        private int _calls;
        private int _frontCall = -1;     // the call the front bus is for
        private int _nextCall = -1;      // and the bus behind it
        private int _nextRefusals;
        private float _nextTry2;
        private int _made;               // buses made this session (each is kept and used again)
        private float _promisedAt;       // Time.time the front bus was promised for when it was put on the road
        private float _shownAt = -1f;    // and when the first figure for its call was shown to the player
        private float _shownFigure;
        private bool _wasDelayed;
        private int _bookedLine = -1;    // the line of the bus the player waits for, -1 for any
        private TransitTrip _bookedTrip; // and the journey picked
        private TransitTrip _plan;       // a journey with a change of bus under way
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
        private bool _dressMore;                    // the last pass stopped at its slice with places still to put up: the next tick goes on
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
            ScenePrewarm.AddLate("transit: the bus stops' dressings built for the routes (props, lettering, boxes and paint)", PrewarmDressings);
            ScenePrewarm.Add("transit: the game's road paint found for the bus bays (everything in memory gone through)", RoadPaint.Prewarm);
            ScenePrewarm.Add("transit: the people who wait at the stops and get off the bus being made", PrewarmPeople);
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

        private Action _onSave;   // made once, as SaveHook compares it

        private void OnSaveGame()
        {
            try
            {
                if (SaveGameManager.Current != null && SaveGameManager.Current.modData != null)
                {
                    // Part of the way through a ride the save has the player at a stop (this runs after the game wrote their place).
                    float fareBack = 0f;
                    if (_rider != null && _rider.ForSave(out Vector3 place, out fareBack))
                    {
                        SaveGameManager.Current.LastPlayerPosition = place;
                        Logger?.Info("Transit check (save): written part of the way through a bus ride (" + _rider.Now + "): the save has the player at "
                                     + (_rider.Stage == BusInside.Stage.Arriving ? "the stop they get off at"
                                         : _rider.Passed != null ? StopLabel(_rider.Passed) + ", the last stop the bus called at" : "the stop they boarded at")
                                     + (fareBack > 0f ? ", the fare to be given back" : "") + ": PASS.");
                    }
                    TransitState.Save(SaveGameManager.Current.modData, fareBack);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Transport: TransitDirector.Update");
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
                _nextTick = now + (TransitMap.PlacingStops && Settings.Buses && TrafficTools.Ready ? 0f : TickInterval);   // laying out: a slice a frame until done
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

        // A fault, on the timer or in something the game called: everything is put away and buses stay off for the session.
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
                // A save written during a ride has the player at the stop they boarded at: the fare is theirs again.
                float fare = TransitState.FareBack;
                TransitState.FareBack = 0f;
                RiderFareBack(fare, "the save was written part of the way through a bus ride");
            }
            if (_rider.Now == BusRider.Step.Away && !_ride.Riding)
            {
                _rider.Done();   // the ride's routine ended without reaching the far end and gave the player back itself
            }
            if (_offBus != null)
            {
                SeeOff(now);
            }
            if ((_tick & 7) == 0)
            {
                SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // nothing is allocated while the game's save event has not changed
                MeasureClock(now);
            }
            Vector3 at = game.playerController.transform.position;
            bool onFoot = !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding;

            _subway.Tick(now, at, onFoot && !_ride.Riding && !_rider.Active && _player != PlayerStep.Choosing && !_seat.Active);

            if (!Settings.Buses || !TransitBus.StreetReady)
            {
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
                _dressMore = DressNear(at);
                Serve(at, now);
                for (int i = 0; i < Dressings; i++)
                {
                    BusStopDressing dressing = _dressings[i];
                    if (dressing == null || dressing.Stop == null)
                    {
                        continue;
                    }
                    // Nearer to become solid than to stay so, so it does not flicker.
                    float reach = dressing.IsSolid ? SolidKeep : SolidWithin;
                    dressing.Solid(onFoot && !_ride.Riding && CitySpots.Flat(dressing.Stop.Sign - at) < reach * reach, now);
                    if (dressing.CheckDue)
                    {
                        // For the log only: with diagnostics off nothing is asked of the mesh.
                        dressing.Check(Meta.Dev.Verbose ? Logger : null, now, Meta.Dev.Verbose ? StopLabel(dressing.Stop) : null);
                    }
                }
            }
            else if (_dressMore)
            {
                _dressMore = DressNear(at);   // the places the last pass had no time for, a slice a tick
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

        // What the director's timer costs, said once a minute while any bus is out.
        private long _costTicks;
        private long _costWorst;
        private int _costCount;
        private int _costBusesMost;
        private float _costFrom;

        private void Cost(float now, long ticks)
        {
            int buses = (_bus.State != TransitBus.Phase.Idle ? 1 : 0) + (_next.State != TransitBus.Phase.Idle ? 1 : 0) + _away.Count + (_seenOff.State != TransitBus.Phase.Idle ? 1 : 0);
            if (buses == 0 && _costCount == 0)
            {
                _costFrom = now;
                return;
            }
            _costTicks += ticks;
            _costWorst = Math.Max(_costWorst, ticks);
            _costCount++;
            _costBusesMost = Math.Max(_costBusesMost, buses);
            if (now - _costFrom < 60f)
            {
                return;
            }
            double ms = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            double average = _costTicks * ms / Math.Max(1, _costCount);
            Logger?.Info("Transit check (cost): " + _costCount + " ticks of the director in the last " + (now - _costFrom).ToString("F0") + " s with buses out ("
                         + _costBusesMost + " at the most, " + _made + " made this session besides the first three): "
                         + average.ToString("F3", CultureInfo.InvariantCulture) + " ms a tick on average, " + (_costWorst * ms).ToString("F2", CultureInfo.InvariantCulture)
                         + " ms at worst; " + (average * 4.0).ToString("F2", CultureInfo.InvariantCulture) + " ms a second (wanted: under 1 ms a second on average): "
                         + (average * 4.0 < 1.0 ? "PASS" : "FAIL") + ".");
            _costTicks = 0;
            _costWorst = 0;
            _costCount = 0;
            _costBusesMost = 0;
            _costFrom = now;
        }

        // How fast the game's clock runs, measured as the player can change it: for saying "in about 4 min".
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

        // The radius of the walker the navigation mesh was made for, by which a cut is wider than its box. Asked once.
        private float WalkerRadius()
        {
            if (_walkerRadius < 0f)
            {
                _walkerRadius = 0.5f;
                try
                {
                    var game = InstanceBehavior<GameManager>.Instance;
                    NavMeshAgent agent = game != null && game.playerController != null && game.playerController.Character != null
                        ? game.playerController.Character.navmeshAgent : null;
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

        // Behind the loading screen, once the routes are laid: a dressing for about every place they will stop at, with its
        // paint and lettering, so putting one up in play makes nothing. False while the routes or the props are not there
        // yet: then at the next black screen out of doors.
        private bool PrewarmDressings()
        {
            if (!Settings.Buses)
            {
                return true;   // switched on later, from the options in a pause: the dressings are built then
            }
            if (!TransitMap.RoutesLaid || !PropLibrary.Settled)
            {
                return false;
            }
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            int want = Math.Min(Dressings, TransitMap.PlacesLikely());
            string glyphs = TransitMap.LineNumbers();
            int built = 0;
            for (int i = 0; i < want; i++)
            {
                if (_dressings[i] == null)
                {
                    _dressings[i] = new BusStopDressing(_root, i + 1);
                    _dressings[i].Prepare(glyphs);
                    built++;
                }
            }
            // The buses' bodies: the three the director keeps, and a spare for each bus that may be driving off at once (AwayMost), so that no
            // bus is built in play (one built in a tick was the 9.3 ms worst tick of a player's log, 28 Oct 2026, with one spare kept).
            _bus.Prepare();
            _next.Prepare();
            _seenOff.Prepare();
            while (_spare.Count < AwayMost && _made == 0)
            {
                var spare = new TransitBus(_root.gameObject);
                spare.Prepare();
                _spare.Add(spare);
            }
            if (Meta.Dev.Verbose)
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                Logger?.Info("Transit check (dressings): " + built + " built behind the loading screen for about " + want + " places of " + TransitMap.Lines.Count
                             + " lines, and the buses' bodies, in " + ms.ToString("F1", CultureInfo.InvariantCulture) + " ms (wanted: none built in play): PASS.");
            }
            return true;
        }

        // Puts up the nearest places not yet up, once a second and on every tick while the last pass ran out of its slice
        // (about a millisecond in play; all of them behind a black screen). True if places were left for the next tick.
        private static readonly int HotDressNear = HotSpots.Slot("Transport: TransitDirector.DressNear");

        private bool DressNear(Vector3 at)
        {
            using var hotSpot = HotSpots.Time(HotDressNear);
            bool hidden = SceneFinds.Hidden;
            WorkSlice slice = hidden ? WorkSlice.Whole : WorkSlice.Of(PutsSliceMs, 1);
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
                int free = FreeDressing(at, best, hidden);
                if (free < 0)
                {
                    return false;
                }
                if (_dressings[free] == null)
                {
                    _dressings[free] = new BusStopDressing(_root, free + 1);
                    if (!hidden)
                    {
                        return true;   // built in play, the city having none: a tick of its own, put up on the next
                    }
                }
                else if (_dressings[free].Stop != null)
                {
                    _dressings[free].Clear();   // the furthest gives up its place only now that the nearer one is going up
                }
                PutDressing(_dressings[free], nearest);
            }
            return true;   // as many as a pass puts up: the next pass sees whether any are left
        }

        // The dressing for the nearest place not up (at nearestSqr): an idle one; a new one behind a screen, or in play only
        // while none was built for the city; else the furthest up, if further than that place and not the one served. -1 for none.
        private int FreeDressing(Vector3 at, float nearestSqr, bool hidden)
        {
            int empty = -1, built = 0;
            for (int i = 0; i < Dressings; i++)
            {
                if (_dressings[i] == null)
                {
                    empty = empty < 0 ? i : empty;
                    continue;
                }
                built++;
                if (_dressings[i].Stop == null)
                {
                    return i;
                }
            }
            if (empty >= 0 && (hidden || built == 0))
            {
                return empty;
            }
            int free = -1;
            float furthest = nearestSqr;
            for (int i = 0; i < Dressings; i++)
            {
                if (_dressings[i] == null)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(_dressings[i].Stop.Sign - at);
                if (sqr > furthest && _dressings[i].Stop != _served)
                {
                    furthest = sqr;
                    free = i;
                }
            }
            return free;
        }

        private void PutDressing(BusStopDressing dressing, TransitStop place)
        {
            dressing.Put(place.At, TransitMap.Numbers(place), WalkerRadius());
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

        // Everything of the buses off the street: people, buses, and with props the dressed stops.
        private void ClearStreet(bool props)
        {
            _rider.Abort("everything of the buses is taken off the street", put: true);   // not while the ride's time passes: the ride has the player then
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
                DropPlan();   // off the street at the change: the journey ends there; its fare still covers a bus for a while
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
                BusZones.Restore();   // the buses are off: the parking and the furniture as the game had them
            }
            _streetClear = true;
        }
    }
}
