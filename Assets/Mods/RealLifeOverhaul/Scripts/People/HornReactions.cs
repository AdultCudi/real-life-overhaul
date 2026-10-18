using System;
using System.Collections.Generic;
using BAModAPI;
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
    /// People react to a horn: the six nearest in earshot, from the game's pedestrians and standing figures, stop and glare
    /// when in the right, hurry or step back when in the wrong, or look round (StreetRules). The same slots make one person
    /// wait, move on or shout for Traffic, so one place changes a pedestrian and puts back exactly what it changed (pace,
    /// stop flag, facing) on every way out. Traffic is never hurried: the one call that would move a waiting car
    /// (ForceSetVehicleActiveAction) forgets why it waits. Cost: idle, two flag tests and a time comparison a frame; while
    /// people react, eight slots a frame; a blast is one pass over the people out, with no physics and no allocation.
    /// </summary>
    public partial class HornReactions : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("people", "HornReactions", typeof(HornReactions));

        private const float LookupInterval = 0.5f;   // seconds between looks at which car the player drives
        private const float CareInterval = 0.1f;     // seconds between checks that the people reacting are still there
        private const float TurnRate = 420f;         // degrees a second somebody turns to face the car
        private const float FacedWithin = 4f;        // degrees: near enough
        private const float TurnBackFor = 0.6f;      // seconds a standing figure is given to turn back
        private const float StepBack = 2.2f;         // metres back the way they came
        private const float BackWithin = 0.7f;       // metres from that place at which they are there, before the game's walker would send them on
        private const float BackMost = 1.5f;         // seconds the step back may take
        private const float BackWait = 2.5f;         // seconds they wait there for the car to go by
        private const float LinePassed = 2f;         // metres past its stop line from which the car's own light no longer counts
        private const float OnRoadWithin = 1.9f;     // metres from the middle of a lane inside which somebody is in the road
        private const float EmojiFor = 2.5f;         // seconds
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

        // Everybody is let go, so a fault cannot repeat on one person; after three, no reactions this session.
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

        // Twice a second: is the feature wanted, which car does the player drive, where are the game's pedestrians.
        private void Lookup()
        {
            if (!Settings.HornReactions || !ModState.Loaded || !TrafficTools.Ready)
            {
                // Switched off, indoors, a menu, fast travel or sleep: whoever was reacting is the game's again.
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

        // The horn as the game itself read it this frame, and the input action once more, so a value left standing while a
        // menu is open is not a blast.
        private bool HornDown()
        {
            return _car != null && _car.vehicleController != null && _car.vehicleController.input.Horn && PlayerAction.VehicleHorn.Pressing();
        }

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

        /// <summary>The light of a car sounding its horn: the stop line ahead in its lane, if not yet passed; None if out of reach.</summary>
        internal static StreetRules.Light LightOf(Vector3 origin, Vector3 forward)
        {
            if (RoadMap.Ensure() && RoadMap.LineAt(origin, forward, out int line, out float along) && along < LinePassed)
            {
                return RoadMap.Line(line).Point.stop ? StreetRules.Light.Red : StreetRules.Light.Green;
            }
            return StreetRules.Light.None;
        }

        /// <summary>A traffic car's horn, answered as the player's is but by that car's light; the police take no part.</summary>
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

        // One blast from one car; player: the police may have something to say about it.
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
                float sideSpeed = SideSpeed(person.Walker, right);
                StreetRules.Reaction reaction = StreetRules.React(person.Place, person.InPath, light, person.MayWalk, person.Side, sideSpeed, halfWidth,
                    Random.Range(0, 100));
                bool bystander = person.Place == StreetRules.Place.Pavement || !person.InPath;
                if (bystander && rude && StreetRules.Joins(rages, Random.Range(0, 100)))
                {
                    reaction = StreetRules.Reaction.Glare;   // after the second or third blast at people in the right, others take their side
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
            Logger?.Info($"Horn{(player ? string.Empty : " (a traffic car's)")}: the car's light {light}, {heard} in earshot: "
                         + $"{hurry} hurry on, {back} step back, {glare} stop and glare, {startle} look round"
                         + (!rude ? "." : player ? $"; sounded at people in the right ({rages} in the last minute)." : "; sounded at people in the right."));
            if (rude && player)
            {
                Police(origin, rages, now);
            }
        }

        /// <summary>
        /// Somebody about to step out in front of a car that can no longer stop waits instead, or steps back to the kerb, and
        /// walks on once it has gone by. False, with nothing changed, if they cannot.
        /// </summary>
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

        /// <summary>Somebody who has held a car up too long jogs on out of its way; false if not walking or held by the game.</summary>
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

        /// <summary>Somebody a car bumped at walking pace stops, turns on it and shouts: one of the game's walkers or standing figures.</summary>
        public bool Shout(Pedestrian walker, BaseHuman figure, Vector3 car)
        {
            try
            {
                if (walker != null
                        ? !Usable(walker) || Reacting(walker, null)
                        : figure == null || !figure.gameObject.activeInHierarchy || Reacting(null, figure) || Street.StreetDirector.Holds(figure))
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

        private bool Usable(Pedestrian walker)
        {
            return enabled && Settings.HornReactions && walker != null && walker.isActiveAndEnabled && walker.tpc != null
                   && walker.tpc.isSittingOn == null && !RagdollSystem.Holds(walker.tpc);
        }

        /// <summary>A traffic car came at people fast: whoever is in its way gets out of it; others look round.</summary>
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
                    float sideSpeed = SideSpeed(person.Walker, right);
                    // As for a horn from a car with a green light at somebody with none: out of the way, or a look.
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

        // The six nearest people in earshot, nearest first, into _heard: positions only.
        private int Gather(Vector3 origin, Vector3 forward)
        {
            int count = 0;
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
                count = Hear(count, origin, forward, walker.transform.position, walker, null);
            }
            List<BaseHuman> standing = StreetPeople.Standing(Logger);
            for (int i = standing.Count - 1; i >= 0; i--)
            {
                BaseHuman figure = standing[i];
                if (figure == null || !figure.gameObject.activeInHierarchy || figure.isSittingOn != null || RagdollSystem.Holds(figure) || Reacting(null, figure)
                    || Street.StreetDirector.Holds(figure))
                {
                    continue;
                }
                count = Hear(count, origin, forward, figure.transform.position, null, figure);
            }
            return count;
        }

        private int Hear(int count, Vector3 origin, Vector3 forward, Vector3 at, Pedestrian walker, BaseHuman figure)
        {
            float rx = forward.z;    // the car's right, flat
            float rz = -forward.x;
            float dx = at.x - origin.x;
            float dz = at.z - origin.z;
            float ahead = dx * forward.x + dz * forward.z;
            float side = dx * rx + dz * rz;
            if (at.y - origin.y < 3f && at.y - origin.y > -3f && StreetRules.Hears(ahead, side))
            {
                return Keep(count, ahead * ahead + side * side, walker, figure, at, ahead, side);
            }
            return count;
        }

        // How fast a walker crosses the car's way, towards its right; 0 for anybody not walking.
        private static float SideSpeed(Pedestrian walker, Vector3 right)
        {
            if (walker != null && walker.tpc != null && walker.tpc.navmeshAgent != null && walker.tpc.navmeshAgent.isActiveAndEnabled)
            {
                Vector3 walking = walker.tpc.navmeshAgent.velocity;
                return walking.x * right.x + walking.z * right.z;
            }
            return 0f;
        }

        // Keeps the nearest few in order of distance: an insertion into a fixed array.
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

        // The list is a field: nobody the game has since put away is kept alive by it.
        private void Forget(int heard)
        {
            for (int i = 0; i < heard; i++)
            {
                _heard[i] = default;
            }
        }

        // On a marked crossing (and whose light it is), in the carriageway, or on the pavement: a grid lookup each.
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

        /// <summary>True while one of the game's standing figures is turned by a reaction (street talk never offers them meanwhile).</summary>
        public static bool Reacts(BaseHuman figure)
        {
            return Instance != null && figure != null && Instance.Reacting(null, figure);
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

        // Notes a blast at people in the right; how many there have been in the last minute, this one included.
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

        // Only with an officer on foot or a parked patrol car near: three in a minute are warned, and after a warning the
        // next is fined (Crime.Fine).
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
            Logger?.Info($"Horn: {rages} blasts at people in the right within a minute, {(officer != null ? "an officer on foot" : "a parked patrol car")} "
                         + $"within {StreetRules.OfficerWithin:F0} m: {penalty}.");
        }
    }
}
