using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.InputSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// People react to the player's horn. On the frame a blast begins (the game's own horn input goes
    /// down, or has been held another two seconds) the people in earshot are picked without a physics
    /// query: the game's walking pedestrians (its own list) and its standing figures (StreetPeople),
    /// compared with a cone ahead of the car and a strip either side; the six nearest react
    /// (StreetRules says how). Somebody in the car's way who is there by right (the car's light is red,
    /// or they are on a crossing whose light is theirs) stops, turns to the car and glares; somebody in
    /// the wrong breaks into a jog onwards, or back if they had only just stepped in; some of the people
    /// on the pavement look round. A reckless car's near miss makes people jump the same way (Scare).
    /// Any other car's horn is answered exactly as the player's is (Sound, called by Traffic/Solid for a
    /// traffic car that has braked hard for somebody or stands behind them), with that car's own light;
    /// only what the police make of it stays the player's. The same director has three more things done to
    /// one person through here, so that there is one place that changes a pedestrian and puts them back:
    /// waiting at the kerb for a car that could not stop (Wait), being moved on out of a car's way (MoveOn),
    /// and the shout of somebody a car has bumped at walking pace (Shout).
    /// The mod's own people never react: every one of them is somebody's cast (ActorPool, the living
    /// city, a job, the family), so they are in neither list and are never touched.
    /// Traffic is not hurried by the horn. The traffic system sets a car off the moment its light goes
    /// green by itself (WaypointManager.TrafficLightChanged clears the car's StopInPoint), so a car still
    /// standing at a green light is standing for something in its sensor, and the one call that would
    /// move it (TrafficManager.ForceSetVehicleActiveAction) wipes exactly that knowledge. There is no
    /// call that says "go if it is safe", so none is made, and no car is ever sent through a red light.
    ///
    /// WHAT IS CHANGED ON A PERSON, AND HOW IT IS PUT BACK. Nothing is added to anybody. At most these
    /// are changed on one of the game's pooled pedestrians, each remembered in its slot:
    ///   the agent's pace (raised to a jog)      put back if it is still the pace this set;
    ///   the agent's stop flag (set)             cleared again, and only if this set it (somebody the game
    ///                                           itself holds at a kerb for the lights is never stopped or started here);
    ///   the agent's path (one step back)        not put back by hand: the game's own walker asks for the way to where it
    ///                                           was going as soon as it finds itself at the end of a path that is not its
    ///                                           destination (PedestrianMovementHandler.HasReachedDestination), and they are
    ///                                           held at the step's end by the stop flag above until the car has gone by;
    ///   an upper-body or arm clip, played once  returns by itself; handed back at once on any early exit;
    ///   a standing figure's facing              turned back.
    /// Every way out runs the same End: the time is up; the pedestrian was released to the pool or the
    /// player went indoors (the game releases every pedestrian: seen within a tenth of a second, and the
    /// pool gives it a pace of its own at its next use in any case); the city was unloaded (the object is
    /// gone: nothing to put back); the option was switched off or traffic stopped (menu, fast travel,
    /// sleep: ReleaseAll); and being hit by a car meanwhile (PedestrianIncidents calls LetGo before the
    /// casualty takes the pedestrian over, so it is taken over exactly as the game had it).
    ///
    /// COST. Nobody reacting and on foot: two flag tests and one time comparison a frame. Driving:
    /// one more, the horn itself. A blast: one pass over the pedestrians out and the standing figures
    /// (a few dozen subtractions each), a grid lookup or two for the six picked, no physics query and
    /// nothing allocated. While people react: eight slots looked at a frame, a turn for those still
    /// turning, and ten times a second a check that each is still the game's.
    /// </summary>
    public class HornReactions : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("people", "HornReactions", typeof(HornReactions));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float LookupInterval = 0.5f;   // how often it asks which car the player is driving
        private const float CareInterval = 0.1f;     // how often it checks that the people reacting are still there to react
        private const float TurnRate = 420f;         // degrees a second somebody turns to face the car
        private const float FacedWithin = 4f;        // degrees: near enough
        private const float TurnBackFor = 0.6f;      // seconds a standing figure is given to turn back
        private const float StepBack = 2.2f;         // metres back the way they came
        private const float BackWithin = 0.7f;       // metres from that place at which they have got there (a little before the game's own walker would notice and send them on)
        private const float BackMost = 1.5f;         // seconds the step back may take
        private const float BackWait = 2.5f;         // seconds they wait there for the car to go by
        private const float LinePassed = 2f;         // metres past its stop line from which the car's own light no longer counts
        private const float OnRoadWithin = 1.9f;     // metres from the middle of a lane inside which somebody is in the road
        private const float EmojiFor = 2.5f;
        private const int Slots = 8;                 // people reacting at once: six to a blast, and room for a near miss beside it

        private const string WaveClip = "goodbye_wave_walk";   // right arm only, made to be played over the game's walk
        private const string AngryClip = "job_annoyed";        // upper body: both hands thrown up, a shake of the head
        private const string PointClip = "point_show";         // upper body: an open hand swept out ahead

        private sealed class Slot
        {
            public bool InUse;
            public StreetRules.Reaction What;
            public Pedestrian Walker;        // one of the game's pooled pedestrians, or
            public BaseHuman Figure;         // ...somebody who stands (a figure of the game's, or a pedestrian in its standing state)
            public Transform Body;
            public NavMeshAgent Agent;       // the walker's, while it is the walking kind
            public float OwnPace;
            public float RaisedPace;
            public bool Raised;
            public bool Stopped;             // this stopped the agent
            public Quaternion OwnFacing;
            public bool Turned;              // a standing figure was turned: it is turned back
            public bool Turning;
            public bool TurningBack;
            public Vector3 Face;
            public float Until;
            public bool Backing;             // on the step back: stopped when they get there
            public Vector3 Back;
            public float BackBy;
            public Animator Gestured;        // a clip of ours was started on this animator
        }

        private struct Heard
        {
            public float Sqr;
            public Pedestrian Walker;
            public BaseHuman Figure;
            public Vector3 At;
            public float Ahead;
            public float Side;
            public StreetRules.Place Place;
            public bool InPath;
            public bool MayWalk;
        }

        public static IModLogger Logger;
        public static HornReactions Instance { get; private set; }

        private readonly Slot[] _slots = new Slot[Slots];
        private readonly Heard[] _heard = new Heard[StreetRules.HornMost];
        private readonly float[] _rageAt = new float[8];   // when the last few blasts at people in the right were sounded
        private int _rageNext;
        private NavMeshPath _path;       // one, reused: a step back is planned into it

        private CarController _car;
        private Rigidbody _body;
        private PedestrianSpawner _spawner;
        private bool _driving;
        private bool _pressed;
        private float _lastBlast = -100f;
        private float _nextLookup;
        private float _nextCare;
        private int _active;
        private float _warnedUntil;
        private float _nextPenalty;
        private int _faults;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new Slot();
            }
            for (int i = 0; i < _rageAt.Length; i++)
            {
                _rageAt[i] = -1000f;
            }
            _path = new NavMeshPath();
        }

        private void OnDestroy()
        {
            ReleaseAll("the mod is closing");
            Crossings.Reset();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("People: HornReactions.Update");
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
            float now = Time.time;
            if (_active > 0)
            {
                try
                {
                    TickReactions(now);
                }
                catch (Exception e)
                {
                    Fault(e);
                }
            }
            if (now >= _nextLookup)
            {
                _nextLookup = now + LookupInterval;
                Lookup();
            }
            if (!_driving)
            {
                return;
            }
            bool pressed = HornDown();
            bool blast = StreetRules.Blast(pressed, _pressed, now - _lastBlast);
            _pressed = pressed;
            if (!blast)
            {
                return;
            }
            _lastBlast = now;
            try
            {
                OnBlast(now);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // Everybody is let go at once, so a fault cannot repeat on the same person; after three there are no reactions this session.
        private void Fault(Exception e)
        {
            Logger?.Error(e);
            ReleaseAll("an error");
            if (++_faults >= 3)
            {
                Logger?.Warn("Horn reactions: switched off for this session after repeated errors (see above).");
                _driving = false;
                enabled = false;
            }
        }

        // Twice a second: is the feature wanted, which car is the player driving, where does the game keep its pedestrians.
        private void Lookup()
        {
            if (!Settings.HornReactions || !ModState.Loaded || !TrafficTools.Ready)
            {
                // Switched off, indoors, a menu, fast travel or sleep: whoever was reacting is the game's again, as it had them.
                if (_active > 0)
                {
                    ReleaseAll("the option is off or the street is paused");
                }
                _driving = false;
                _pressed = false;
                return;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            CarController car = gm != null ? gm.selectedVehicle as CarController : null;
            _driving = car != null && car.vehicleController != null && PlayerHelper.IsUsingVehicle;
            if (car != _car)
            {
                _car = car;
                _body = car != null ? car.GetComponent<Rigidbody>() : null;
                _pressed = false;
            }
            if (_spawner == null)
            {
                var city = InstanceBehavior<CityManager>.Instance;
                _spawner = city != null ? city.pedestrianSpawner : null;
            }
        }

        // The horn as the game itself has just read it (CarController sets it from the same input action every frame it
        // takes input at all), and that action once more, so that a value left standing while a menu is open is not a blast.
        private bool HornDown()
        {
            return _car != null && _car.vehicleController != null && _car.vehicleController.input.Horn && PlayerAction.VehicleHorn.Pressing();
        }

        // ---------------------------------------------------------------- a blast

        private void OnBlast(float now)
        {
            if (_body == null)
            {
                return;
            }
            Vector3 origin = _body.position;
            Vector3 forward = _body.rotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return;
            }
            forward.Normalize();
            float halfWidth = 0.95f;
            CarStrikes strikes = CarStrikes.Instance;
            if (strikes != null && strikes.PlayerCar(out _, out _, out Vector3 half))
            {
                halfWidth = half.x;
            }
            Blasted(origin, forward, halfWidth, LightOf(origin, forward), true, now);
        }

        /// <summary>
        /// The light of the car that sounds a horn: the stop line ahead of it in its lane, if it has not passed it
        /// yet (a grid lookup, no physics). None if there is no light within reach.
        /// </summary>
        internal static StreetRules.Light LightOf(Vector3 origin, Vector3 forward)
        {
            if (RoadMap.Ensure() && RoadMap.LineAt(origin, forward, out int line, out float along) && along < LinePassed)
            {
                return RoadMap.Line(line).Point.stop ? StreetRules.Light.Red : StreetRules.Light.Green;
            }
            return StreetRules.Light.None;
        }

        /// <summary>
        /// A car that is not the player's has sounded its horn at somebody in its way: the people in earshot
        /// answer it exactly as they answer the player's (in the wrong they hurry or step back, in the right they
        /// stop and glare, others look round), by that car's own light. Nothing of the police follows from it.
        /// Does nothing with the option off.
        /// </summary>
        /// <param name="forward">The way the car faces.</param>
        public void Sound(Vector3 origin, Vector3 forward, float halfWidth)
        {
            try
            {
                forward.y = 0f;
                if (!enabled || !Settings.HornReactions || forward.sqrMagnitude < 0.01f)
                {
                    return;
                }
                forward.Normalize();
                if (_spawner == null)
                {
                    Lookup();
                }
                Blasted(origin, forward, halfWidth, LightOf(origin, forward), false, Time.time);
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // One blast from one car: who hears it, where each is and whose light it is, and what each does about it.
        // player: the player sounded it (the police may have something to say about a blast at people in the right).
        private void Blasted(Vector3 origin, Vector3 forward, float halfWidth, StreetRules.Light light, bool player, float now)
        {
            Crossings.Ensure();

            int heard = Gather(origin, forward);
            bool rude = false;
            for (int i = 0; i < heard; i++)
            {
                ref Heard person = ref _heard[i];
                person.InPath = StreetRules.InPath(person.Ahead, person.Side, halfWidth);
                person.Place = PlaceOf(person.At, out person.MayWalk);
                if (person.InPath && StreetRules.InTheRight(person.Place, light, person.MayWalk))
                {
                    rude = true;
                }
            }
            int rages = rude && player ? NoteRage(now) : 0;

            int hurry = 0, back = 0, glare = 0, startle = 0;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < heard; i++)
            {
                ref Heard person = ref _heard[i];
                float sideSpeed = 0f;
                if (person.Walker != null && person.Walker.tpc != null && person.Walker.tpc.navmeshAgent != null && person.Walker.tpc.navmeshAgent.isActiveAndEnabled)
                {
                    Vector3 walking = person.Walker.tpc.navmeshAgent.velocity;
                    sideSpeed = walking.x * right.x + walking.z * right.z;
                }
                StreetRules.Reaction reaction = StreetRules.React(person.Place, person.InPath, light, person.MayWalk, person.Side, sideSpeed, halfWidth, Random.Range(0, 100));
                bool bystander = person.Place == StreetRules.Place.Pavement || !person.InPath;
                if (bystander && rude && StreetRules.Joins(rages, Random.Range(0, 100)))
                {
                    reaction = StreetRules.Reaction.Glare;   // a second and a third blast at people in the right: others take their side
                }
                if (reaction == StreetRules.Reaction.None || !Begin(ref person, reaction, origin, light, rages, now))
                {
                    continue;
                }
                switch (reaction)
                {
                    case StreetRules.Reaction.Hurry: hurry++; break;
                    case StreetRules.Reaction.StepBack: back++; break;
                    case StreetRules.Reaction.Glare: glare++; break;
                    default: startle++; break;
                }
            }
            Forget(heard);
            Logger?.Info($"Horn{(player ? string.Empty : " (a traffic car's)")}: the car's light {light}, {heard} in earshot: {hurry} hurry on, {back} step back, {glare} stop and glare, {startle} look round"
                         + (!rude ? "." : player ? $"; sounded at people in the right ({rages} in the last minute)." : "; sounded at people in the right."));
            if (rude && player)
            {
                Police(origin, rages, now);
            }
        }

        // ---------------------------------------------------------------- one person, asked for by Traffic/Solid

        /// <summary>
        /// Somebody about to step out in front of a car that could no longer stop for them waits instead: they
        /// stop where they are (or take a step back if they are a step into its way already), look at the car,
        /// and walk on when it has gone by. False (and nothing changed) if they cannot: they are reacting to
        /// something already, are not walking, or the game itself is holding them.
        /// </summary>
        /// <param name="back">They are a step into the car's way already: back to the kerb.</param>
        public bool Wait(Pedestrian walker, Vector3 car, bool back, float seconds)
        {
            try
            {
                if (!Usable(walker) || Reacting(walker, null))
                {
                    return false;
                }
                NavMeshAgent agent = walker.tpc.navmeshAgent;
                if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || agent.isStopped)
                {
                    return false;
                }
                var person = new Heard { Walker = walker, At = walker.transform.position };
                float now = Time.time;
                return (back && Begin(ref person, StreetRules.Reaction.StepBack, car, StreetRules.Light.Green, 0, now))
                       || Begin(ref person, StreetRules.Reaction.Startle, car, StreetRules.Light.Green, 0, now, seconds);
            }
            catch (Exception e)
            {
                Fault(e);
                return false;
            }
        }

        /// <summary>
        /// Somebody who has held a car up for too long is moved on: whatever they were doing about its horn ends,
        /// and they jog on out of its way. False if they cannot be (they are not walking, or the game holds them).
        /// </summary>
        public bool MoveOn(Pedestrian walker, Vector3 car)
        {
            try
            {
                if (!Usable(walker))
                {
                    return false;
                }
                LetGo(walker);
                var person = new Heard { Walker = walker, At = walker.transform.position };
                return Begin(ref person, StreetRules.Reaction.Hurry, car, StreetRules.Light.Green, 0, Time.time);
            }
            catch (Exception e)
            {
                Fault(e);
                return false;
            }
        }

        /// <summary>
        /// Somebody a car has bumped at walking pace: they stop, turn on it and shout (the glare of somebody in the
        /// right, which is what they are by now). One of the game's walkers or one of its standing figures.
        /// </summary>
        public bool Shout(Pedestrian walker, BaseHuman figure, Vector3 car)
        {
            try
            {
                if (walker != null ? !Usable(walker) || Reacting(walker, null) : figure == null || !figure.gameObject.activeInHierarchy || Reacting(null, figure))
                {
                    return false;
                }
                var person = new Heard { Walker = walker, Figure = figure, At = walker != null ? walker.transform.position : figure.transform.position };
                return Begin(ref person, StreetRules.Reaction.Glare, car, StreetRules.Light.Red, 1, Time.time);
            }
            catch (Exception e)
            {
                Fault(e);
                return false;
            }
        }

        // True if one of the game's walkers is there to react at all: out, its own, not sitting, not thrown down.
        private bool Usable(Pedestrian walker)
        {
            return enabled && Settings.HornReactions && walker != null && walker.isActiveAndEnabled && walker.tpc != null
                   && walker.tpc.isSittingOn == null && !RagdollSystem.Holds(walker.tpc);
        }

        /// <summary>
        /// A car that is not the player's has come at people fast (a reckless driver's near miss, a car braking for
        /// somebody who stepped out): whoever is in its way gets out of it, people beside it look round. Nobody
        /// stops to glare at a car that is not stopping. Does nothing with the option off.
        /// </summary>
        public void Scare(Vector3 origin, Vector3 forward, float halfWidth)
        {
            try
            {
                forward.y = 0f;
                if (!enabled || !Settings.HornReactions || forward.sqrMagnitude < 0.01f)
                {
                    return;
                }
                forward.Normalize();
                if (_spawner == null)
                {
                    Lookup();
                }
                float now = Time.time;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                int heard = Gather(origin, forward);
                int moved = 0;
                for (int i = 0; i < heard; i++)
                {
                    ref Heard person = ref _heard[i];
                    person.InPath = StreetRules.InPath(person.Ahead, person.Side, halfWidth);
                    float sideSpeed = 0f;
                    if (person.Walker != null && person.Walker.tpc != null && person.Walker.tpc.navmeshAgent != null && person.Walker.tpc.navmeshAgent.isActiveAndEnabled)
                    {
                        Vector3 walking = person.Walker.tpc.navmeshAgent.velocity;
                        sideSpeed = walking.x * right.x + walking.z * right.z;
                    }
                    // (As for a horn from a car with a green light at somebody with none: out of the way, or a look.)
                    StreetRules.Reaction reaction = StreetRules.React(person.InPath ? StreetRules.Place.Road : StreetRules.Place.Pavement, person.InPath,
                        StreetRules.Light.Green, false, person.Side, sideSpeed, halfWidth, Random.Range(0, 100));
                    if (reaction != StreetRules.Reaction.None && Begin(ref person, reaction, origin, StreetRules.Light.Green, 0, now))
                    {
                        moved++;
                    }
                }
                Forget(heard);
                if (moved > 0)
                {
                    Logger?.Info($"Near miss: {heard} people beside or ahead of the car, {moved} jump out of its way or look round.");
                }
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        // The six nearest people in earshot, nearest first, into _heard. Positions only: no physics, nothing allocated.
        private int Gather(Vector3 origin, Vector3 forward)
        {
            int count = 0;
            float rx = forward.z;    // the car's right, flat
            float rz = -forward.x;
            List<Pedestrian> walkers = _spawner != null ? _spawner.activePedestrians : null;
            for (int i = walkers != null ? walkers.Count - 1 : -1; i >= 0; i--)
            {
                Pedestrian walker = walkers[i];
                // Switched off: already somebody's (a casualty, somebody the mod sent across the road). Sitting, lying: left alone.
                if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null || walker.tpc.isSittingOn != null
                    || RagdollSystem.Holds(walker.tpc) || Reacting(walker, null))
                {
                    continue;
                }
                Vector3 at = walker.transform.position;
                float dx = at.x - origin.x;
                float dz = at.z - origin.z;
                float ahead = dx * forward.x + dz * forward.z;
                float side = dx * rx + dz * rz;
                if (at.y - origin.y < 3f && at.y - origin.y > -3f && StreetRules.Hears(ahead, side))
                {
                    count = Keep(count, ahead * ahead + side * side, walker, null, at, ahead, side);
                }
            }
            List<BaseHuman> standing = StreetPeople.Standing(Logger);
            for (int i = standing.Count - 1; i >= 0; i--)
            {
                BaseHuman figure = standing[i];
                if (figure == null || !figure.gameObject.activeInHierarchy || figure.isSittingOn != null || RagdollSystem.Holds(figure) || Reacting(null, figure))
                {
                    continue;
                }
                Vector3 at = figure.transform.position;
                float dx = at.x - origin.x;
                float dz = at.z - origin.z;
                float ahead = dx * forward.x + dz * forward.z;
                float side = dx * rx + dz * rz;
                if (at.y - origin.y < 3f && at.y - origin.y > -3f && StreetRules.Hears(ahead, side))
                {
                    count = Keep(count, ahead * ahead + side * side, null, figure, at, ahead, side);
                }
            }
            return count;
        }

        // Keeps the nearest few, in order of distance: an insertion into a fixed array.
        private int Keep(int count, float sqr, Pedestrian walker, BaseHuman figure, Vector3 at, float ahead, float side)
        {
            int place = count;
            while (place > 0 && _heard[place - 1].Sqr > sqr)
            {
                place--;
            }
            if (place >= _heard.Length)
            {
                return count;
            }
            int last = Mathf.Min(count, _heard.Length - 1);
            for (int i = last; i > place; i--)
            {
                _heard[i] = _heard[i - 1];
            }
            _heard[place] = new Heard { Sqr = sqr, Walker = walker, Figure = figure, At = at, Ahead = ahead, Side = side };
            return Mathf.Min(count + 1, _heard.Length);
        }

        // (The list is a field: nobody the game has since put away is kept alive by it.)
        private void Forget(int heard)
        {
            for (int i = 0; i < heard; i++)
            {
                _heard[i] = default;
            }
        }

        // On a marked crossing (and whose light it is), in the carriageway away from one, or on the pavement. A grid
        // lookup each; no physics.
        internal static StreetRules.Place PlaceOf(Vector3 at, out bool mayWalk)
        {
            if (Crossings.At(at, out mayWalk))
            {
                return StreetRules.Place.Crossing;
            }
            mayWalk = false;
            return RoadMap.Usable && RoadMap.NearestLane(at, out float off, out _, out _) && off < OnRoadWithin
                ? StreetRules.Place.Road
                : StreetRules.Place.Pavement;
        }

        private bool Reacting(Pedestrian walker, BaseHuman figure)
        {
            if (_active == 0)
            {
                return false;
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot.InUse && ((walker != null && slot.Walker == walker) || (figure != null && slot.Figure == figure)))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- one person's reaction

        // Starts it. False (and nothing changed) if there is no room or the person cannot do it as they are.
        // seconds: how long a look round lasts, if not the usual moment (somebody waiting for a car to go by).
        private bool Begin(ref Heard person, StreetRules.Reaction reaction, Vector3 car, StreetRules.Light light, int rages, float now, float seconds = 0f)
        {
            Slot slot = null;
            for (int i = 0; i < _slots.Length && slot == null; i++)
            {
                if (!_slots[i].InUse)
                {
                    slot = _slots[i];
                }
            }
            if (slot == null)
            {
                return false;
            }

            ThirdPersonCharacter character = person.Walker != null ? person.Walker.tpc : null;
            NavMeshAgent agent = character != null ? character.navmeshAgent : null;
            bool walking = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
            if (!walking && (reaction == StreetRules.Reaction.Hurry || reaction == StreetRules.Reaction.StepBack))
            {
                return false;   // standing about: nobody jogs who is not walking anywhere
            }

            slot.What = reaction;
            slot.Walker = walking ? person.Walker : null;
            slot.Figure = walking ? null : person.Walker != null ? (BaseHuman)person.Walker.tpc : person.Figure;
            slot.Body = walking ? character.transform : slot.Figure != null ? slot.Figure.transform : null;
            slot.Agent = walking ? agent : null;
            slot.Raised = false;
            slot.Stopped = false;
            slot.Turned = false;
            slot.Turning = false;
            slot.TurningBack = false;
            slot.Backing = false;
            slot.Face = car;
            slot.Gestured = null;
            if (slot.Body == null)
            {
                Wipe(slot);
                return false;
            }
            Animator animator = walking ? character.animator : slot.Figure.animator;

            switch (reaction)
            {
                case StreetRules.Reaction.Hurry:
                case StreetRules.Reaction.StepBack:
                    if (agent.isStopped)
                    {
                        Wipe(slot);
                        return false;   // held at a kerb by the game, or turning at their door: not ours to send anywhere
                    }
                    if (reaction == StreetRules.Reaction.StepBack)
                    {
                        if (TurnBack(agent, out slot.Back))
                        {
                            slot.Backing = true;
                            slot.BackBy = now + BackMost;
                        }
                        else
                        {
                            slot.What = StreetRules.Reaction.Hurry;   // no way back to be had: onwards it is
                        }
                    }
                    slot.OwnPace = agent.speed;
                    slot.RaisedPace = StreetRules.HurryPace(slot.OwnPace);
                    agent.speed = slot.RaisedPace;
                    slot.Raised = true;
                    slot.Until = now + StreetRules.HurrySeconds;
                    if (StreetRules.Waves(Random.Range(0, 100)))
                    {
                        Gesture(slot, animator, WaveClip, BodyMask.RightArm);
                    }
                    break;

                case StreetRules.Reaction.Glare:
                case StreetRules.Reaction.Startle:
                    bool glare = reaction == StreetRules.Reaction.Glare;
                    if (walking)
                    {
                        // Stopped for the moment, unless the game is holding them already (then they only turn to look).
                        if (!agent.isStopped)
                        {
                            agent.isStopped = true;
                            slot.Stopped = true;
                        }
                    }
                    else
                    {
                        slot.OwnFacing = slot.Body.rotation;
                        slot.Turned = true;
                    }
                    slot.Turning = true;
                    slot.Until = now + (glare ? StreetRules.GlareFor(rages) : seconds > 0f ? seconds : StreetRules.StartleSeconds);
                    if (glare)
                    {
                        Gesture(slot, animator, StreetRules.Points(light, Random.Range(0, 100)) ? PointClip : AngryClip, BodyMask.Upper);
                        ActorPool pool = ActorPool.Instance;
                        if (pool != null && walking)
                        {
                            pool.ShowEmoji(character, CharacterEmojiName.CommonRedAngry, EmojiFor);
                        }
                        else if (pool != null && slot.Figure.head != null)
                        {
                            pool.ShowEmojiAt(slot.Figure.head, CharacterEmojiName.CommonRedAngry, EmojiFor);
                        }
                    }
                    break;
            }
            slot.InUse = true;
            _active++;
            _nextCare = Mathf.Min(_nextCare, now + CareInterval);
            return true;
        }

        // One step back the way they came, planned at once into the one path this keeps (the game plans its own
        // walkers' paths the same way). They are stopped when they get there (Backed) and wait for the car to pass;
        // the game's walker asks for the way to where it was going by itself, then or when they are let go.
        private bool TurnBack(NavMeshAgent agent, out Vector3 back)
        {
            Vector3 going = agent.velocity;
            going.y = 0f;
            back = agent.transform.position;
            if (going.sqrMagnitude < 0.04f)
            {
                return false;
            }
            back -= going.normalized * StepBack;
            return agent.CalculatePath(back, _path) && _path.status != NavMeshPathStatus.PathInvalid && agent.SetPath(_path);
        }

        // Somebody on their step back: once they are there (or it has taken too long) they stop, turn to the car and wait.
        private static void Backed(Slot slot, float now)
        {
            NavMeshAgent agent = slot.Agent;
            Vector3 off = slot.Body.position - slot.Back;
            if (off.x * off.x + off.z * off.z > BackWithin * BackWithin && now < slot.BackBy)
            {
                return;
            }
            slot.Backing = false;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped)
            {
                agent.isStopped = true;
                slot.Stopped = true;
            }
            slot.Turning = true;
            slot.Until = now + BackWait;
        }

        // An upper-body or one-arm clip of the mod's, once, over whatever the game is animating; it returns by itself.
        // If it cannot be had (the clips are still loading, the clip player is off) the turn and the pace say it alone.
        private static void Gesture(Slot slot, Animator animator, string clipName, BodyMask mask)
        {
            if (animator == null || !ActivityAnimators.Working || !ClipLibrary.Has(clipName) || !ClipLibrary.TryGet(clipName, out CityClip clip))
            {
                return;
            }
            ActivityAnimator player = ActivityAnimators.For(animator);
            if (player != null && player.Usable && !player.Active && player.PlayOver(clip.Clip, mask))
            {
                slot.Gestured = animator;
            }
        }

        // Every frame while somebody reacts: the turn of those still turning, and whose time is up.
        private void TickReactions(float now)
        {
            bool care = now >= _nextCare;
            if (care)
            {
                _nextCare = now + CareInterval;
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (!slot.InUse)
                {
                    continue;
                }
                if (care && Gone(slot))
                {
                    End(slot, early: true);
                    continue;
                }
                if (slot.Backing && slot.Body != null)
                {
                    Backed(slot, now);
                }
                if (slot.Turning)
                {
                    Turn(slot);
                }
                if (now < slot.Until)
                {
                    continue;
                }
                if (slot.Turned && !slot.TurningBack)
                {
                    slot.TurningBack = true;   // a figure that was standing a certain way stands that way again
                    slot.Turning = true;
                    slot.Until = now + TurnBackFor;
                }
                else
                {
                    End(slot, early: false);
                }
            }
        }

        // True if the person is no longer the game's to react with: destroyed with the city, put back in the pool,
        // taken over (a casualty), thrown down, or handed out again by the pool with a pace of its own.
        private static bool Gone(Slot slot)
        {
            if ((object)slot.Walker != null)
            {
                Pedestrian walker = slot.Walker;
                return walker == null || !walker.isActiveAndEnabled || slot.Agent == null || !slot.Agent.isActiveAndEnabled
                       || (slot.Raised && !Mathf.Approximately(slot.Agent.speed, slot.RaisedPace));
            }
            BaseHuman figure = slot.Figure;
            if (figure == null || !figure.gameObject.activeInHierarchy || RagdollSystem.Holds(figure))
            {
                return true;
            }
            // One of the game's pedestrians that was standing about and has set off again: it faces where it walks
            // now, and the way it stood is nothing to put back.
            if (figure is ThirdPersonCharacter character && character.navmeshAgent != null && character.navmeshAgent.isActiveAndEnabled)
            {
                slot.Turned = false;
                return true;
            }
            return false;
        }

        private static void Turn(Slot slot)
        {
            Transform body = slot.Body;
            if (body == null)
            {
                slot.Turning = false;
                return;
            }
            Quaternion want = slot.OwnFacing;
            if (!slot.TurningBack)
            {
                Vector3 to = slot.Face - body.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f)
                {
                    slot.Turning = false;
                    return;
                }
                want = Quaternion.LookRotation(to, Vector3.up);
            }
            body.rotation = Quaternion.RotateTowards(body.rotation, want, TurnRate * Time.deltaTime);
            if (Quaternion.Angle(body.rotation, want) < FacedWithin)
            {
                slot.Turning = false;
            }
        }

        // The one way out of a reaction. Whatever this changed is put back if the person is still there to have it
        // put back, and only what this changed. early: not at the end of its time (taken, pooled, switched off).
        private void End(Slot slot, bool early)
        {
            try
            {
                NavMeshAgent agent = slot.Agent;
                if (agent != null)
                {
                    // (A pace somebody else has set since is theirs: the pool gives a pedestrian a new one each time it is handed out.)
                    if (slot.Raised && Mathf.Approximately(agent.speed, slot.RaisedPace))
                    {
                        agent.speed = slot.OwnPace;
                    }
                    Pedestrian walker = slot.Walker;
                    if (slot.Stopped && walker != null && walker.isActiveAndEnabled && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.isStopped)
                    {
                        agent.isStopped = false;
                    }
                }
                if (slot.Turned && slot.Body != null && (early || slot.TurningBack))
                {
                    slot.Body.rotation = slot.OwnFacing;
                }
                if (early && slot.Gestured != null)
                {
                    ActivityAnimators.Forget(slot.Gestured);   // the body is handed back at once: a ragdoll or the pool is about to have it
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Wipe(slot);
            _active = Mathf.Max(0, _active - 1);
        }

        private static void Wipe(Slot slot)
        {
            slot.InUse = false;
            slot.Walker = null;
            slot.Figure = null;
            slot.Body = null;
            slot.Agent = null;
            slot.Gestured = null;
            slot.Raised = false;
            slot.Stopped = false;
            slot.Turned = false;
            slot.Turning = false;
            slot.TurningBack = false;
            slot.Backing = false;
        }

        /// <summary>Everybody who is reacting is the game's again, as it had them, now. Safe at any time.</summary>
        public void ReleaseAll(string why)
        {
            int let = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].InUse)
                {
                    End(_slots[i], early: true);
                    let++;
                }
            }
            _active = 0;
            if (let > 0)
            {
                Logger?.Info("Horn reactions: " + let + " people let go at once (" + why + "); pace, stop and facing put back.");
            }
        }

        /// <summary>
        /// One of the game's pedestrians is about to be taken over by somebody else of the mod (a car has hit
        /// them, or they are to be sent across the road): whatever a reaction changed on them is put back first.
        /// </summary>
        public static void LetGo(Pedestrian pedestrian)
        {
            HornReactions reactions = Instance;
            if (reactions == null || reactions._active == 0 || pedestrian == null)
            {
                return;
            }
            for (int i = 0; i < reactions._slots.Length; i++)
            {
                Slot slot = reactions._slots[i];
                if (slot.InUse && (slot.Walker == pedestrian || (slot.Figure != null && slot.Figure == pedestrian.tpc)))
                {
                    reactions.End(slot, early: true);
                }
            }
        }

        /// <summary>The same for one of the game's standing figures.</summary>
        public static void LetGo(BaseHuman figure)
        {
            HornReactions reactions = Instance;
            if (reactions == null || reactions._active == 0 || figure == null)
            {
                return;
            }
            for (int i = 0; i < reactions._slots.Length; i++)
            {
                Slot slot = reactions._slots[i];
                if (slot.InUse && slot.Figure == figure)
                {
                    reactions.End(slot, early: true);
                }
            }
        }

        // ---------------------------------------------------------------- what the police make of it

        // Notes a blast at people in the right and says how many there have been in the last minute, this one included.
        private int NoteRage(float now)
        {
            _rageAt[_rageNext] = now;
            _rageNext = (_rageNext + 1) % _rageAt.Length;
            int count = 0;
            for (int i = 0; i < _rageAt.Length; i++)
            {
                if (now - _rageAt[i] <= StreetRules.RageWindow)
                {
                    count++;
                }
            }
            return count;
        }

        // Only with an officer on foot or a parked patrol car near: three in a minute are warned, and after a warning
        // the next one is fined, through the fine every other traffic offence is charged by (Crime.Fine).
        private void Police(Vector3 at, int rages, float now)
        {
            if (!Settings.Police || !Settings.PlayerOffences || now < _nextPenalty)
            {
                return;
            }
            StreetActor officer = PlayerOffences.OfficerNear(at, StreetRules.OfficerWithin);
            PolicePresence presence = PolicePresence.Instance;
            bool near = officer != null || (presence != null && presence.CarWithin(at, StreetRules.OfficerWithin));
            StreetRules.Penalty penalty = StreetRules.For(rages, near, now < _warnedUntil);
            if (penalty == StreetRules.Penalty.None)
            {
                return;
            }
            _nextPenalty = now + StreetRules.PenaltyPause;
            _warnedUntil = now + StreetRules.WarnedFor;
            ActorPool pool = ActorPool.Instance;
            if (officer != null && pool != null)
            {
                pool.ShowEmoji(officer, CharacterEmojiName.CommonRedAngry, 3f);
            }
            if (penalty == StreetRules.Penalty.Fine)
            {
                Crime.Fine(CrimeLaw.HornFine, "reallife_horn_fine");
            }
            else
            {
                Crime.Show(NotificationType.Warning, "reallife_horn_warning", 8f);
            }
            Logger?.Info($"Horn: {rages} blasts at people in the right within a minute, {(officer != null ? "an officer on foot" : "a parked patrol car")} within {StreetRules.OfficerWithin:F0} m: {penalty}.");
        }
    }
}
