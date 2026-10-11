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
    ///   waiting; gets on when the bus stands, pays, and is carried there (<see cref="TransitRide"/>).
    ///   All of that is in TransitDirector.Player.cs.
    ///
    /// Every step writes one "Transit check (...)" line with what was measured and PASS or FAIL,
    /// so that a run can be judged from Player.log alone.
    ///
    /// The budget: a 0.25 s timer; per frame only one key test while the player sits (the panel's
    /// keys are the game's, read by the game). Four people at most (two JobCast pairs of its own), made only
    /// when first needed. Texts are built when what they say changes. A fault switches buses
    /// off for the session, after putting everything away.
    /// </summary>
    public sealed partial class TransitDirector : MonoBehaviour
    {
        private const float TickInterval = 0.25f;
        private const float ArrivedBeyond = 60f;    // metres between two passes that no walk or drive covers: the player was set down here
        private const float SolidWithin = 60f;      // metres: on foot this near, a dressed stop's boxes cut the navigation mesh
        private const float SolidKeep = 90f;        // ...and stay cut until this far off
        private const float ServeRange = 80f;
        private const float ServeKeep = 105f;
        private const float SitRange = 1.5f;         // metres from a seat's place at which sitting is offered
        private const float BusLead = 30f;           // seconds before it is due that a bus is put on the road
        private const float MostWaitMinutes = 20f;   // game minutes: nobody waits longer for a bus than this, whatever the hour
        private const float DriveIn = 14f;           // seconds a bus usually takes from where it is put on the road to the stop
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
        private const int PutsATick = 2;            // stops put up in one pass, nearest first: all of them within seconds of the network being ready
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

        /// <summary>True while the player is aboard a bus (the ride hides the player and darkens the screen).</summary>
        public static bool Riding => Instance != null && Instance._ride != null && Instance._ride.Riding;

        /// <summary>True while the stop's panel (whose buttons are on the game's interact keys) or the subway's key prompt is on screen: the mod's own enter-a-door key stands aside.</summary>
        public static bool TakesInteractKey => Instance != null && (Instance._panel.Showing || Instance._keyFrame == Time.frameCount
                                                                    || (Instance._subway != null && Instance._subway.TakesKey));

        private readonly BusStopDressing[] _dressings = new BusStopDressing[Dressings];
        private readonly Rider[] _waiting = new Rider[Waiters];
        private readonly Collider[] _touching = new Collider[12];
        private readonly StringBuilder _text = new StringBuilder(320);
        private readonly TransitEta _eta = new TransitEta();
        private readonly TransitSeat _seat = new TransitSeat();
        private TransitBus _bus;
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
        private string _departure;       // the departure line of a bus that is waiting in its bay for the lane to be clear: written when it goes
        private bool _departureSound;
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
        private Vector3 _dressedFrom;
        private float _minutesPerSecond = 1f;
        private float _rateFrom;
        private float _rateMinutes;

        private void Awake()
        {
            Instance = this;
            TransitMap.Logger = Logger;
            TransitMap.Reset();
            TransitState.Reset();
            _root = new GameObject("RealLifeOverhaul Transit").transform;
            _root.SetParent(transform, false);
            _bus = new TransitBus(_root.gameObject);
            _seenOff = new TransitBus(_root.gameObject);
            _ride = new TransitRide(this);
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
                    TransitState.Save(SaveGameManager.Current.modData);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- the two clocks

        private void Update()
        {
            if (_failed)
            {
                return;
            }
            try
            {
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
                _nextTick = now + (TransitMap.PlacingStops && Settings.Buses && TrafficTools.Ready ? 0f : TickInterval);   // (placing stops: a 3 ms slice a frame until it is done)
                _tick++;
                Tick(now);
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
            if ((_tick & 7) == 0)
            {
                SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // the mod's one relay in the game's save event (Core/SaveHook): nothing is allocated while that event has not changed
                MeasureClock(now);
            }
            Vector3 at = game.playerController.transform.position;
            bool onFoot = !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding;

            _subway.Tick(now, at, onFoot && !_ride.Riding && _player != PlayerStep.Choosing && !_seat.Active);

            if (!Settings.Buses || !TrafficTools.Ready)
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
            _streetClear = false;
            if ((_tick & 3) == 0)
            {
                // Just loaded, back on the street, or set down somewhere else by a taxi or the subway: the stops go up at once.
                bool atOnce = _dressAtOnce || CitySpots.Flat(at - _dressedFrom) > ArrivedBeyond * ArrivedBeyond;
                _dressAtOnce = false;
                _dressedFrom = at;
                DressNear(at, atOnce, now);
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

        private void DressNear(Vector3 at, bool evenInView, float now)
        {
            // A bus stop is part of the street: every stop is put up once, the nearest first, and stays up while the city
            // is loaded. Nothing waits for the player to look away and nothing is taken in behind them.
            // Put out the nearest that are not out yet: a pass over the stops, once a second.
            for (int pass = 0; pass < PutsATick; pass++)
            {
                TransitStop nearest = null;
                float best = float.MaxValue;
                List<TransitStop> stops = TransitMap.Stops;
                for (int i = 0; i < stops.Count; i++)
                {
                    float sqr = CitySpots.Flat(stops[i].Sign - at);
                    if (sqr < best && !Dressed(stops[i]))
                    {
                        best = sqr;
                        nearest = stops[i];
                    }
                }
                if (nearest == null)
                {
                    return;
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
                        return;
                    }
                }
                if (_dressings[free] == null)
                {
                    _dressings[free] = new BusStopDressing(_root, free + 1);
                }
                else if (_dressings[free].Stop != null)
                {
                    _dressings[free].Clear();   // the furthest, giving up its place only now that the nearer one is going up
                }
                TransitLine line = TransitMap.Lines[nearest.Line];
                _dressings[free].Put(nearest, line.Number, line.Number + " " + line.To, WalkerRadius());
            }
        }

        private bool Dressed(TransitStop stop)
        {
            return DressingOf(stop) != null;
        }

        private BusStopDressing DressingOf(TransitStop stop)
        {
            for (int i = 0; stop != null && i < Dressings; i++)
            {
                if (_dressings[i] != null && _dressings[i].Stop == stop)
                {
                    return _dressings[i];
                }
            }
            return null;
        }

        // Which stop has people waiting and a bus coming: the nearest dressed one, kept until the player is well away.
        private void Serve(Vector3 at, float now)
        {
            if (_served != null && _player != PlayerStep.Choosing && !_ride.Riding)
            {
                // Well away from it, or standing on foot at another dressed stop (the other side of the street, say): that one is served instead.
                TransitStop stood = PlayerHelper.IsUsingVehicle || AtStop(_served, at) ? null : StoodAt(at);
                if (CitySpots.Flat(_served.Sign - at) > ServeKeep * ServeKeep || !Dressed(_served) || (stood != null && stood != _served))
                {
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

        // A stop becomes the one that has people waiting and a bus coming. (A line's last stop too: the bus that ends there takes riders on.)
        private void BeginService(TransitStop stop, float now)
        {
            _served = stop;
            // Arriving at a stop is arriving part of the way through an interval: the same part for the same stop in the same hour.
            float part = Mathf.Abs((stop.Id * 7919 + TimeHelper.CurrentDay * 104729 + TimeHelper.CurrentHour * 1299709) % 1000) / 1000f;
            _eta.Timetable(Wait(Mathf.Max(BusLead + 4f, Headway() * (0.2f + 0.6f * part))));
            _refusals = 0;
            _nextTry = 0f;
            _nextWaiter = now + 2f;
            _panelKey = int.MinValue;
        }

        private void EndService()
        {
            ReleaseRiders();
            if (_bus.State != TransitBus.Phase.Idle)
            {
                _bus.Release(remove: false);
            }
            _bus.TakeLost();
            _served = null;
            _wasStanding = false;
            _doorFound = false;
            _refusals = 0;
            _player = PlayerStep.None;
            HideHud();
        }

        // The wait the timetable asks for, held to MostWaitMinutes of the game's clock at the rate it is running at, the
        // drive in counted: a wait shorter than BusLead puts the bus on the road at once.
        private float Wait(float seconds)
        {
            return Mathf.Clamp(seconds, 2f, Mathf.Max(2f, MostWaitMinutes / Mathf.Max(0.1f, _minutesPerSecond) - DriveIn));
        }

        /// <summary>Seconds between buses at a stop now: by the hour, the day and the weather.</summary>
        private static float Headway()
        {
            int hour = TimeHelper.CurrentHour;
            float seconds = hour < 5 ? 360f : hour < 7 ? 220f : hour < 20 ? 150f : 210f;
            if (IsRush(hour))
            {
                seconds = 100f;
            }
            return (Raining() ? seconds * 1.3f : seconds) / CivicPolicy.ServiceScale;   // the city's transit policy: 1 unless a council has changed it
        }

        private static bool IsRush(int hour)
        {
            return !TimeHelper.IsWeekend && ((hour >= 7 && hour < 9) || (hour >= 16 && hour < 19));
        }

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

        // ---------------------------------------------------------------- the bus

        private void RunBus(float now)
        {
            _bus.Tick(now);
            if (_bus.TakeDeparted(out float gap, out bool pulledOut))
            {
                // It stood in its bay with its doors closed until the lane was clear: the departure line, held until now.
                Logger?.Info((_departure ?? "Transit check (departure): the bus leaves its bay") + "; in its bay it waited " + gap.ToString("F0") + " s for the lane beside and behind it to be clear"
                             + (pulledOut ? " and pulled out" : " and no gap came: it is let go where it stands, out of the lane") + ": " + (pulledOut && _departureSound ? "PASS" : "FAIL") + ".");
                _departure = null;
            }
            if (_served == null)
            {
                _bus.TakeLost();
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
            switch (_bus.State)
            {
                case TransitBus.Phase.Idle:
                    bool due = _eta.Seconds <= BusLead || _eta.State == TransitEta.Mode.Delayed;
                    if (due && now >= _nextTry && _player != PlayerStep.Choosing && !_ride.Riding)
                    {
                        // Out of view first; from the third refusal on it may start in view, far off.
                        if (_bus.Send(_served, _refusals >= 3, out string refusal))
                        {
                            Assigned(now, false);
                        }
                        else
                        {
                            _refusals++;
                            _nextTry = now + RetryEvery;
                            if (_refusals == 1 || _refusals % 15 == 0)
                            {
                                Logger?.Info("Transit: no bus could be put on the road for " + StopLabel(_served) + " yet (try " + _refusals + ", again every " + RetryEvery.ToString("F0")
                                             + " s; the panel says DELAYED after six): " + refusal + ".");
                            }
                        }
                    }
                    if (_bus.State == TransitBus.Phase.Idle)
                    {
                        _eta.Wait(TickInterval, _refusals > 0);   // (not in the tick a bus has just been put on the road)
                    }
                    break;

                case TransitBus.Phase.Approaching:
                    _eta.Track(TickInterval, _bus.Remaining, _bus.Waiting != TransitBus.Hold.None);
                    Watch(now);
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
                    bool held = (_player == PlayerStep.Choosing && now < _chooseUntil) || _ride.Riding;   // (aboard: it stands until the screen is dark)
                    // The driver waits for somebody at the stop or walking up to it: the least stand is for an empty stop.
                    bool wanted = !_busFull && PlayerAtStop(WaitsWithin);
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
                    _eta.Wait(TickInterval, false);   // the next one is on its way while this one drives off
                    break;
            }
        }

        private bool PlayerAtStop(float within)
        {
            return _served != null && !PlayerHelper.IsUsingVehicle && !BuildingManager.IsInsideBuilding
                   && CitySpots.Flat(_served.Sign - PlayerHelper.GetPosition()) < within * within;
        }

        // A bus has just been put on the road for the served stop.
        private void Assigned(float now, bool test)
        {
            _busFull = !test && IsRush(TimeHelper.CurrentHour) && Random.value < 0.25f;
            _promised = _eta.Seconds;
            TransitEta.Mode was = _eta.State;
            _eta.Assign(_bus.Remaining);
            _routeLogAt = now + 60f;
            _moveUpAt = now + MoveUpAfter;
            _refusals = 0;
            _wasStanding = false;
            _panelKey = int.MinValue;
            Vector3 at = _bus.Car.transform.position;
            float away = Mathf.Sqrt(CitySpots.Flat(at - PlayerHelper.GetPosition()));
            bool inView = CitySpots.InView(at);
            bool pass = away >= 60f && _bus.StartedBack >= 60f && (test || !inView);   // (the test button may start it in view, far off)
            TransitLine line = TransitMap.Lines[_served.Line];
            Logger?.Info("Transit check (bus assigned): the " + line.Number + " to " + line.To + " for " + StopLabel(_served) + (test ? " (test button)" : "") + ": " + _bus.Chassis
                         + "; it starts " + _bus.StartedBack.ToString("F0") + " m of line before the stop, " + away.ToString("F0") + " m from the player, in view: " + (inView ? "yes" : "no")
                         + "; " + _bus.Remaining.ToString("F0") + " m to drive, about " + (_bus.Remaining / TransitEta.StartSpeed).ToString("F0") + " s; the panel was showing "
                         + (was == TransitEta.Mode.Delayed ? "DELAYED" : _promised.ToString("F0") + " s") + "; "
                         + (_bus.InBay ? "it will stand in the stop's bay, " + _served.BayOffset.ToString("F2") + " m off the lane's line"
                             : _served.HasBay ? "it will stop in the lane (" + (_bus.BayNote ?? "the bay cannot be used") + ")"
                             : "the stop has no bay (" + (_served.NoBay ?? "none laid out") + "): it will stop in the lane")
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
            if (still > MoveUpAfter && now >= _moveUpAt && !_bus.InView && _bus.Remaining > 110f)
            {
                // Held up where nobody can see it: put further up its line, past what holds it, still out of view.
                _moveUpAt = now + 30f;
                float from = _bus.Remaining;
                string why = WaitingWords();
                TransitStop stop = _served;
                _bus.Release(remove: true);
                if (_bus.Send(stop, false, out string refusal, from - 45f))
                {
                    _routeLogAt = now + 60f;
                    Logger?.Info("Transit check (re-plan): the bus had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the stop (" + why
                                 + "), out of view: it is moved up its line to " + _bus.Remaining.ToString("F0") + " m from the stop (" + _bus.Chassis + "): PASS.");
                }
                else
                {
                    _eta.Lost();
                    _refusals = 0;
                    _nextTry = now + RetryEvery;
                    Logger?.Info("Transit check (re-plan): the bus had got nowhere for " + still.ToString("F0") + " s, " + from.ToString("F0") + " m from the stop (" + why
                                 + "), out of view, and could not be moved up its line (" + refusal + "): taken off the road, another is sent: FAIL.");
                }
                _panelKey = int.MinValue;
                return;
            }
            if (still > GiveUpAfter || _bus.Out > GiveUpOut)
            {
                Logger?.Info("Transit check (re-plan): the bus has got nowhere for " + still.ToString("F0") + " s and has been out " + _bus.Out.ToString("F0") + " s, " + _bus.Remaining.ToString("F0")
                             + " m from the stop (" + WaitingWords() + "): given up, the next bus is sent: FAIL.");
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_skipped", new Dictionary<string, string>(1) { { "route", TransitMap.Lines[_served.Line].Number } }, 8f);
                _bus.Release(remove: false);
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
                case TransitBus.Hold.Traffic: return "standing behind traffic for " + _bus.WaitingFor.ToString("F0") + " s";
                case TransitBus.Hold.Closure: return "standing before a closed road for " + _bus.WaitingFor.ToString("F0") + " s";
                default: return "driving, " + _eta.Speed.ToString("F1") + " m/s while moving";
            }
        }

        private string PanelWords()
        {
            return _eta.State == TransitEta.Mode.Delayed ? "DELAYED" : _eta.State == TransitEta.Mode.Here ? "at the stop" : _eta.Seconds.ToString("F0") + " s (" + _eta.State + ")";
        }

        // The bus has just stopped: where it stands, where its door is, who gets out, whether those waiting can reach it.
        private void Arrived(float now)
        {
            _dwellUntil = now + DwellLeast;
            if (!_busFull && !_ride.Riding && PlayerAtStop(WaitsWithin))
            {
                // Said once as it pulls in: the bus is a small one, and it is not obvious that it is theirs.
                Notifications.Show(NotificationType.Info, "reallife_transit_bus_in",
                    new Dictionary<string, string>(1) { { "route", TransitMap.Lines[_served.Line].Number } }, 8f);
            }
            _doorFound = false;
            _boardable = false;
            _boarded = 0;
            _alighted = 0;
            VehicleComponent car = _bus.Car;
            TransitLine line = TransitMap.Lines[_served.Line];
            // Where it stands. In its bay the measure is the bay's: nothing of it in the lane, nothing over the kerb, parallel
            // to it. On the lane's line it is the lane's, as it always was.
            _bus.Measure(out float along, out float aside, out float angle, out float toKerb, out float toLane);
            bool inBay = _bus.InBay;
            bool placed = inBay ? Mathf.Abs(along) <= 2.5f && toLane >= 0f && toKerb >= 0f && angle <= 3f
                                : Mathf.Abs(along) <= 2.5f && Mathf.Abs(aside) <= 1.2f && angle <= 12f;
            Logger?.Info("Transit check (arrival): the " + line.Number + " stands at " + StopLabel(_served) + (inBay ? " in its bay" : "") + " with its middle " + Mathf.Abs(along).ToString("F1") + " m "
                         + (along >= 0f ? "past" : "short of") + " the pole, " + Mathf.Abs(aside).ToString(inBay ? "F2" : "F1") + " m " + (aside >= 0f ? "towards the kerb" : "away from the kerb")
                         + " off the lane's line" + (inBay ? " (the bay's line is " + _served.BayOffset.ToString("F2") + ")" : "") + ", " + angle.ToString(inBay ? "F1" : "F0")
                         + " degrees off the lane's direction, door side to the kerb with " + toKerb.ToString(inBay ? "F2" : "F1") + " m of road between"
                         + (inBay ? ", its body " + toLane.ToString("F2") + " m clear of the lane's edge (a lorry on the lane's line passes " + (toLane + BusBay.LaneHalf - BusBay.WidestHalf).ToString("F2")
                                    + " m from it): the lane is " + (toLane >= 0f ? "free" : "NOT free")
                             : _served.HasBay ? ", in the lane (" + (_bus.BayNote ?? "the bay was not used") + ")"
                             : ", in the lane (no bay here: " + (_served.NoBay ?? "none laid out") + ", the kerb " + _served.KerbAside.ToString("F2") + " m from the lane's line)")
                         + "; " + _bus.Took.ToString("F0") + " s after setting out " + _bus.StartedBack.ToString("F0") + " m back (the panel had promised " + _promised.ToString("F0") + " s): "
                         + (placed ? "PASS" : inBay ? "FAIL (allowed: 2.5 m along, 3 degrees, nothing of it in the lane or over the kerb)" : "FAIL (allowed: 2.5 m along, 1.2 m aside, 12 degrees)") + ".");

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
            int waiting = 0;
            int seated = 0;
            for (int i = 0; i < Waiters; i++)
            {
                waiting += _waiting[i].Step == RiderStep.Waiting || _waiting[i].Step == RiderStep.Arriving ? 1 : 0;
                seated += _waiting[i].Sat ? 1 : 0;
            }
            bool sat = _seat.Seated;
            Logger?.Info("Transit check (doors): doors open; a place to step in " + (_doorFound ? "found " + out_.ToString("F1") + " m further out than the door wants" : "NOT found on the road surface beside the door")
                         + "; the way from the kerb to it " + (!_doorFound ? "not looked at" : wayClear ? "clear" : "has vehicles standing in it") + "; " + waiting + " waiting (" + seated + " seated)"
                         + (sat ? ", the player seated and getting up" : "") + "; the bus is " + (_busFull ? "full: nobody boards" : "not full") + "; it stands "
                         + (_bus.InBay ? "out of the lane" : "in the lane") + ": "
                         + (_doorFound && wayClear ? "PASS" : "FAIL (nobody on foot gets in or out this time; the player still boards from the stop)") + ".");
            if (!wayClear)
            {
                _doorFound = false;
            }
            _boardable = _doorFound && !_busFull;
            if (sat)
            {
                _seat.Stand(now);   // the bus is here: up off the bench
            }
            if (_doorFound && Random.value < 0.6f)
            {
                Alight(now);
            }
            // How long it stands at the least. In the lane it is in the way of everything behind it, so where nobody at all
            // has business with it (nobody waiting or walking up, nobody getting out, the player not near and not aboard
            // or choosing) the doors open and shut and it goes. Out of the lane, in its bay, it keeps the usual stand.
            bool nobody = waiting == 0 && !sat && _alighter.Step == RiderStep.None && _player != PlayerStep.Choosing && !_ride.Riding && !PlayerAtStop(WaitsWithin);
            _dwellLeast = toLane < 0f && nobody ? DwellEmpty : DwellLeast;
            _dwellUntil = now + _dwellLeast;
            _panelKey = int.MinValue;
        }

        // Nothing but the bus stands on the straight way from the kerb to its door.
        private bool WayClear(Vector3 from, Vector3 to, VehicleComponent bus)
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
            if (_bus.State == TransitBus.Phase.Standing)
            {
                float stood = _bus.Standing;
                _bus.Depart();
                bool sound = stood <= DwellMost + ChooseFor + 2f;
                bool waits = _bus.AwaitingGap;   // in its bay: it goes when the lane is clear, and the line is written then (RunBus)
                _departure = null;
                _departureSound = sound;
                if (Logger != null)
                {
                    string said = "Transit check (departure): the bus " + (waits ? "closes its doors" : "pulls away in the lane") + " after standing " + stood.ToString("F0") + " s ("
                                  + _dwellLeast.ToString("F0") + " to " + DwellMost.ToString("F0") + " allowed); " + _boarded + " got in, " + _alighted + " got out, the player "
                                  + (_ride.Riding ? "is aboard" : _player == PlayerStep.Choosing ? "was still choosing a stop" : "did not board");
                    if (waits)
                    {
                        _departure = said;
                    }
                    else
                    {
                        Logger.Info(said + ": " + (sound ? "PASS" : "FAIL") + ".");
                    }
                }
            }
            _eta.Timetable(Wait(Headway() * (_busFull ? 0.5f : 1f)));
            _refusals = 0;
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

            bool doorBusy = _alighter.Step == RiderStep.Exiting;
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
            StreetActor person = _others.Put(_door, true);
            if (person == null)
            {
                return;
            }
            person.StandAt(_door, _door - _doorInwards * 3f);
            // Out of the van's sliding door there is a clip; down a bus's step there is none: they are simply on the road, and walk.
            bool clip = !_bus.Modelled && ClipLibrary.TryGet("job_car_exit", out CityClip exit) && person.Work("job_car_exit");
            JobCast.DoorSound(_door);
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
            _seat.Abort();
            _sitAt = null;
            ReleaseRiders();
            _bus.Release(remove: true);
            _bus.TakeLost();
            _seenOff.Release(remove: true);
            _served = null;
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
            if (why == TestRefusal.None && _ride.Riding)
            {
                why = TestRefusal.SceneActive;
            }
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return false;
            }
            for (int i = 0; i < 400 && !TransitMap.Ready; i++)
            {
                TransitMap.Step();   // a press before the layout has finished: finished now
            }
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
            Logger?.Info("Bus stop test: the stop at " + StopName(stop) + " (" + TransitMap.Lines[stop.Line].Number + ", " + (stop.Shelter ? "with room for a shelter" : "pole only")
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
            TransitLine line = TransitMap.Lines[stop.Line];
            _dressings[slot].Put(stop, line.Number, line.Number + " " + line.To, WalkerRadius());
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
                _served = stop;
                _eta.Timetable(BusLead);
                _nextWaiter = Time.time + 1f;
            }
            // In view if it must be (the button is pressed to see it): out of view is tried first.
            if (!_bus.Send(stop, false, out string refusal) && !_bus.Send(stop, true, out refusal))
            {
                TestRefusals.Tell(Logger, "Bus", TestRefusal.NothingFits, 0, refusal);
                return;
            }
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
            int direct = TransitTrips.From(from, _trips);
            if (_trips.Count == 0)
            {
                TestRefusals.Tell(Logger, "Bus ride", TestRefusal.NothingFits, 0, "the network has no other stop to ride to");
                return;
            }
            TransitStop to = _trips[direct > 0 ? Mathf.Min(1, direct - 1) : 0].To;
            bool pass = TransitState.HasPass;
            if (!TransitState.Pay(TransitState.BusFare, "reallife_transaction_bus_fare"))
            {
                return;
            }
            StartRide(from, to, pass, "is put on the bus by the test button");
        }

        private static TransitStop NearestBoardable(Vector3 at, float within)
        {
            TransitStop nearest = null;
            float best = within * within;
            List<TransitStop> stops = TransitMap.Stops;
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
