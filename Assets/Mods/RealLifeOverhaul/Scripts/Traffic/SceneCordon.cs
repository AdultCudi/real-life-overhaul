using System;
using System.Collections.Generic;
using BAModAPI;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>One thing lying in the road that a cordon goes round: a wreck, or a person.</summary>
    public struct Footprint
    {
        public Vector3 Centre;
        public Vector3 Forward;
        public float HalfLength;
        public float HalfWidth;
        public bool Vehicle;      // something solid the crews must walk round (a held car), not room kept for someone lying down
    }

    /// <summary>
    /// Scene control at an accident, in two steps.
    /// Close shuts the road at once, through the traffic system's own switch on its road
    /// points: the lanes the wrecks lie in are closed from well before them (the apron, kept
    /// clear so that the response vehicles have somewhere to stand in sight of the scene); the
    /// way into that stretch is closed at the junction behind it, so new traffic turns off
    /// instead of queuing; and a lane that is physically clear stays usable but gets a gate at
    /// the scene. It also says where in those lanes a response vehicle should stand (Stand).
    /// Raise, when the police arrive, puts up what the player sees: a compact rectangle of
    /// posts and tape round the wrecks and a working margin, kept on the carriageway (never
    /// over a kerb, steps or anything else solid), with barriers across both ends. The tape
    /// is also a wall for anyone on foot (the game moves people on its navigation mesh, so the
    /// wall is cut out of that; a collider would stop nobody): one wall along every run of tape
    /// that actually went up, the barriers with it, and an entry left open at each end for the
    /// crews, and for the player to get out by. Whether each wall really cut the mesh is said in
    /// the log. The cones are a taper: a short diagonal upstream of each end that traffic
    /// arrives at, beyond the response vehicles standing there, laid once those have parked
    /// and taken in when the first of them leaves. From then on the gate lets the cars already
    /// waiting through a few at a time, on a timer.
    /// Open undoes all of it: every road point this closed is opened again (and no other), the
    /// props and walls are switched off. They are built once from shared shapes and reused;
    /// nothing here runs per frame, and a fault in any of it opens the road and leaves the
    /// feature off.
    /// </summary>
    public sealed class SceneCordon
    {
        private const float Margin = 3f;          // metres of working room round the wrecks, along the road
        private const float Side = 0.6f;          // ...and across it, from the outermost wreck to the tape
        private const float CrewLane = 2f;        // across the road, from a held vehicle to the tape on the side the crews walk: a metre clear of it, and room for them
        private const float Apron = 30f;          // metres of each blocked lane kept empty before the wrecks, for the response vehicles
        private const float LaneClear = 2f;       // a lane whose middle is nearer the wrecks than this is blocked by them
        private const float RoadWidth = 9f;       // lanes further across than this are another carriageway
        private const float EntryWalk = 320f;     // metres back along a lane the junction behind is looked for
        private const float KerbReach = 3f;       // a pavement this close beyond the tape makes that the kerb side
        private const float KerbInset = 0.5f;     // the tape stays this far off the kerb, on the carriageway
        private const float EntryGap = 2.4f;      // the opening left in each end: facing the ambulance at the near end, at the kerb corner of the far one
        private const float WorkRoom = 2.6f;      // at a junction, no road point this near the wrecks (across the road) stays open
        private const float BarrierWidth = 2.5f;  // metres of an end one barrier takes up
        private const float ParkedGap = 0.45f;    // the tape stays this far off a parked vehicle
        private const float PostSpacing = 3.5f;
        private const float PostHeight = 1f;
        private const float TapeHeight = 0.9f;
        private const float ConeSpacing = 1.9f;   // metres between the cones of a taper
        private const int TaperCones = 6;         // cones a taper may have (each end has its own)
        private const float WallThickness = 0.3f;
        private const float BarrierOut = 0.7f;    // metres outside an end's tape that its barriers stand
        private const float BarrierWall = 1.22f;  // an end's wall, tape and barriers together: from just inside the tape to the barriers' far face
        private const float CarveWait = 0.8f;     // seconds after the walls go up before the navigation mesh is asked about them
        private const float GateOpenFor = 0.6f;   // seconds: long enough for the car at the front to set off
        private const int MaxPosts = 24;
        private const int MaxCones = TaperCones * 2;
        private const int MaxBarriers = 8;
        private const int Walls = 8;
        private const int Markers = 4;

        private static readonly List<Waypoint> Found = new List<Waypoint>(96);
        private static readonly HashSet<int> Lane = new HashSet<int>();
        private static readonly HashSet<int> Visited = new HashSet<int>();
        private static readonly Stack<Waypoint> Walk = new Stack<Waypoint>();
        private static readonly List<Waypoint> Entries = new List<Waypoint>(8);
        private static readonly List<Waypoint> EntryParents = new List<Waypoint>(8);
        private static readonly Predicate<Waypoint> Collect = Keep;

        public static IModLogger Logger;

        private readonly Transform _host;
        private readonly List<Waypoint> _closed = new List<Waypoint>(64);   // switched off by us, and still off
        private readonly List<Waypoint> _gates = new List<Waypoint>(4);
        private readonly List<Waypoint> _lanePoints = new List<Waypoint>(32);   // the blocked lanes, apron included
        private readonly List<bool> _laneWith = new List<bool>(32);             // ...and whether each runs with the road's direction
        private readonly Vector3[] _corners = new Vector3[4];

        private GameObject _props;
        private Transform[] _posts;
        private Transform[] _tapes;
        private Transform[] _cones;
        private Transform[] _barriers;
        private NavMeshObstacle[] _walls;
        private readonly Vector3[] _wallFrom = new Vector3[Walls];   // each wall runs along tape that went up: from this post...
        private readonly Vector3[] _wallTo = new Vector3[Walls];     // ...to this one
        private readonly float[] _wallOut = new float[Walls];        // -1 or 1 for an end (its barriers are part of it), 0 for a side
        private int _wallCount;
        private int _segment = -1;   // the wall the tape now being run belongs to; -1 to start another
        private float _lastAlong, _lastAcross;   // where on the rectangle the last post was asked for (before any moving in)
        private float _outward;
        private float _carveAt;      // when the walls are looked for in the navigation mesh; 0 when there is nothing to look at
        private int _carveTries;
        private int _tapeBlocked;    // stretches of tape left out because something solid stood between their posts
        private string _blockedBy;
        private int _movedPosts;     // posts that could not stand on their line and were moved in
        private static int _solidMask;
        private static readonly System.Text.StringBuilder Report = new System.Text.StringBuilder(160);

        // The gate held for a response vehicle that is leaving (see HoldGateShut).
        private int _gateHold;       // -1 shut, 0 worked on its timer
        private int _sideFrom, _sideTo;   // the lengths of tape along the side the open lane runs past: these of _tapes (none if equal)

        // Glass and plastic at the point of impact and the numbered markers an officer sets out: the mod's own, built once.
        private GameObject _scatter;
        private Transform[] _litter;
        private Transform[] _markers;
        private bool _scatterModelled;

        private bool _failed;
        private bool _modelled;      // the props were built when the prop library was ready: if not, they are built again once it is
        private float _tapeHeight = TapeHeight;   // where the tape ties on to the posts that were built
        private float _tapeUnit;     // the length of the modelled tape piece; 0 if the tape is the plain strip
        private static int _parkedMask;
        private float _doorAcross;   // where across the near end the opening is: in line with the ambulance
        private float _doorWidth;
        private int _crossClosed;    // road points closed only because they cross the crew's way (a junction beside the scene)

        // The ambulance crew's way out and back (CrewWay), once it is known: nothing of the cordon stands on it.
        private const float CrewClear = 0.3f;     // metres every post, cone, barrier and length of tape keeps from anything of the crew on its way
        private const float CrewGap = 2.6f;       // the least an opening for the crew is wide
        private const float HoldBack = 24f;       // metres before the scene that traffic let past it is held: behind where the ambulance stands and its crew turns the stretcher
        private CrewPath _wayOut, _wayHome;
        private int _wayRefused;                  // places for a post, a cone or a barrier refused because they are on it
        private int _wayOpenings;                 // openings made for it in the sides and the far end
        private bool _crewHold;                   // the gate is held shut while the crew is in the lane it works
        private readonly Vector3[] _tapeFrom = new Vector3[MaxPosts];
        private readonly Vector3[] _tapeTo = new Vector3[MaxPosts];
        private readonly Vector3[] _oldFrom = new Vector3[Walls];   // the walls of the last dressing, while the tape is run again:
        private readonly Vector3[] _oldTo = new Vector3[Walls];     // the navigation mesh is still cut where they stood
        private int _oldCount;
        private readonly List<float> _openAcross = new List<float>(4);   // how far across the road each lane traffic is let past in runs

        // The road's own frame at the scene, and the wrecks in it (metres from the scene).
        private Vector3 _scene;
        private Vector3 _forward;
        private Vector3 _right;
        private float _alongMin, _alongMax, _acrossMin, _acrossMax;
        private float _bandMin, _bandMax;   // how far across the blocked lanes reach
        private bool _bothWays;      // a lane running the other way is blocked too
        private float _gateSide;     // which side of the wrecks the open lane is on; 0 if there is none
        private bool _gateAgainst;   // a lane left open beside the scene runs the other way: its traffic arrives at the far end
        private float _solidMin, _solidMax;   // how far across the vehicles alone reach (the wrecks without the room kept for whoever lies there)
        private bool _anySolid;
        private readonly List<Waypoint> _touched = new List<Waypoint>(72);   // every road point this scene switched off, kept until Recheck has looked at them

        // The taped rectangle, in the same frame.
        private float _back, _front, _left, _rightEdge;
        private bool _kerbLeft, _kerbRight;
        private bool _crossing;      // people on foot have a way through here (a crossing): no walls
        private float _entry;        // the side the entries are on: -1 left, 1 right

        // Counters for the props being laid out.
        private int _placed, _taped;
        private Vector3 _last;
        private bool _hasLast;

        private bool _gateOpen;
        private float _gateAt;

        public SceneCordon(Transform host)
        {
            _host = host;
        }

        /// <summary>True from Close to Open, if the road was shut: road points are switched off.</summary>
        public bool Closed { get; private set; }

        /// <summary>True from Close to Open: the scene has been measured, so Stand has answers.</summary>
        public bool Surveyed { get; private set; }

        /// <summary>True from Raise to Open: the posts and tape are up and the gate is being worked.</summary>
        public bool Up { get; private set; }

        /// <summary>True once the tape has been taken down again (Lower) while the road is still closed.</summary>
        public bool Lowered { get; private set; }

        /// <summary>Outside the opening at the far end, where an officer keeps people from wandering in; and where they look from there.</summary>
        public Vector3 EntryPost { get; private set; }

        /// <summary>See <see cref="EntryPost"/>.</summary>
        public Vector3 EntryLook { get; private set; }

        /// <summary>True while glass and plastic lie at the point of impact (Litter): there is something for an officer to mark.</summary>
        public bool HasImpact { get; private set; }

        /// <summary>True if a lane running the other way is blocked as well, so a vehicle can come up to the far end of the scene.</summary>
        public bool HasFarEnd => Surveyed && _bothWays;

        /// <summary>Where the officer directing traffic stands: just outside the tape, facing what is coming.</summary>
        public Vector3 DirectorPost { get; private set; }

        /// <summary>Where whoever is on the traffic post stands while a response unit leaves by the open lane: beside the post, out of that lane, in front of the near end's barriers.</summary>
        public Vector3 PassPost { get; private set; }

        /// <summary>True while the tape along the open lane is taken in for a unit that is passing (<see cref="TakeInSide"/>).</summary>
        public bool SideIn { get; private set; }

        /// <summary>Outside the tape by the near entry, on the pavement if there is one: where bystanders are sent to wait.</summary>
        public Vector3 Kerbside { get; private set; }

        /// <summary>A point up the road, where the waiting traffic is.</summary>
        public Vector3 Oncoming { get; private set; }

        /// <summary>A point just outside a corner of the taped area, 0 to 3, for someone walking round it.</summary>
        public Vector3 Corner(int index)
        {
            return _corners[index & 3];
        }

        /// <summary>
        /// Closes the road round whatever lies in it. Safe to call when traffic is not running
        /// or the scene is not on a road: nothing is closed then.
        /// </summary>
        /// <param name="shut">False to only measure the scene (for Stand) and leave the road open: a minor bump.</param>
        public void Close(Vector3 scene, Vector3 heading, List<Footprint> footprints, bool shut = true)
        {
            Open();
            Recheck();
            if (_failed || footprints.Count == 0 || !TrafficTools.Ready)
            {
                return;
            }
            try
            {
                Measure(scene, heading, footprints);
                Surveyed = true;   // (Stand is asked while the lanes are being closed)
                CloseLanes(shut);
                Closed = shut;
                Survey();   // the rectangle is fixed now, so that the crews can be kept inside it before the tape is up
                if (shut)
                {
                    ClearStranded();
                    if (_crossClosed > 0)
                    {
                        Logger?.Info("Cordon: a junction lies beside the scene; " + _crossClosed + " more road point(s) across the crew's way between the ambulance and the wrecks are closed.");
                    }
                    Logger?.Info($"Cordon: {_closed.Count} road point(s) closed, {_gates.Count} gated lane(s), wrecks cover {_alongMax - _alongMin:F0} x {_acrossMax - _acrossMin:F0} m{(_bothWays ? ", both directions blocked" : "")}.");
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>
        /// Where a response vehicle should stand: in a blocked lane, a given distance short of
        /// the wrecks in that lane's own direction of travel, so it drives up to the scene on
        /// the scene's own carriageway and stops in sight of it.
        /// </summary>
        /// <param name="farEnd">The far end of the scene, reached by the lane running the other way (see HasFarEnd).</param>
        /// <param name="back">Metres from the nearest wreck to the middle of the vehicle.</param>
        /// <param name="anchor">The road point at or just before the place: a route there ends at it.</param>
        /// <param name="at">The place itself, in that lane, at or a little beyond the anchor.</param>
        /// <returns>False if the scene has no such lane (not on a road, or never measured).</returns>
        public bool Stand(bool farEnd, float back, out Waypoint anchor, out Vector3 at)
        {
            anchor = null;
            at = default;
            if (!Surveyed)
            {
                return false;
            }
            float middle = (_acrossMin + _acrossMax) * 0.5f;
            float best = float.MaxValue;
            float bestGap = 0f;
            for (int i = 0; i < _lanePoints.Count; i++)
            {
                if (_laneWith[i] == farEnd)
                {
                    continue;
                }
                Waypoint point = _lanePoints[i];
                Locate(point, out float along, out float across, out _);
                float gap = farEnd ? along - _alongMax : _alongMin - along;   // metres short of the wrecks
                if (gap < Margin + 2f)
                {
                    continue;
                }
                // The nearest point at or before the place; failing that, the one that falls least short of it.
                // Never in a junction or on its crossing if a point of plain lane will do.
                float cost = (gap >= back ? gap - back : 100f + back - gap) + Mathf.Abs(across - middle) * 2f + (IsForked(point) ? 60f : 0f);
                if (cost < best)
                {
                    best = cost;
                    bestGap = gap;
                    anchor = point;
                }
            }
            if (anchor == null)
            {
                return false;
            }
            // On from the anchor to the exact place, unless that would take it into a junction: then the anchor is the place.
            Waypoint next = anchor.neighbors != null && anchor.neighbors.Count == 1 ? TrafficManager.Instance.GetWaypoint(anchor.neighbors[0]) : null;
            bool plainAhead = next != null && !IsForked(next) && !IsForked(anchor);
            at = plainAhead ? anchor.position + LaneDirection(anchor) * Mathf.Max(0f, bestGap - back) : anchor.position;
            return true;
        }

        private static bool IsForked(Waypoint point)
        {
            return point.neighbors.Count > 1 || point.prev.Count > 1 || point.IsInIntersection();
        }

        /// <summary>Puts up the posts, tape, barriers and walls, and starts working the gate. Needs Close first.</summary>
        public void Raise(float now)
        {
            if (!Closed || Up || _failed)
            {
                return;
            }
            try
            {
                Up = true;
                SetGates(false);
                _gateAt = now + Random.Range(4f, 7f);
                ClearStranded();
                if (Shapes.Available)
                {
                    Dress(now);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>
        /// Takes the tape, barriers, cones and walls down again but leaves the road closed: the
        /// officers are packing up, and the wrecks are still where they were. Open does the rest.
        /// </summary>
        public void Lower()
        {
            if (!Up || Lowered)
            {
                return;
            }
            Lowered = true;
            _carveAt = 0f;
            if (_props != null && _props.activeSelf)
            {
                _props.SetActive(false);   // the walls with them: people on foot can cross again
                Logger?.Info("Cordon: tape, barriers and cones taken down; the road stays closed until the scene is cleared.");
            }
        }

        /// <summary>
        /// Works the gate. Returns true at the moment waiting traffic is waved through, so that
        /// whoever is directing it can be seen to. Two comparisons when nothing is due.
        /// </summary>
        public bool Tick(float now)
        {
            if (_carveAt > 0f && now >= _carveAt)
            {
                CheckWalls(now);
            }
            if (!Up || _gates.Count == 0 || _gateHold != 0 || _crewHold || now < _gateAt)
            {
                return false;
            }
            if (_gateOpen)
            {
                SetGates(false);
                _gateAt = now + Random.Range(5f, 9f);
                return false;
            }
            SetGates(true);
            _gateAt = now + GateOpenFor;
            return true;
        }

        /// <summary>Opens the road and takes the props and walls down. Safe at any time, any number of times.</summary>
        public void Open()
        {
            for (int i = 0; i < _closed.Count; i++)
            {
                _closed[i].temporaryDisabled = false;
            }
            _closed.Clear();
            for (int i = 0; i < _gates.Count; i++)
            {
                _gates[i].temporaryDisabled = false;
            }
            _gates.Clear();
            _gateHold = 0;
            _crewHold = false;
            _wayOut = null;
            _wayHome = null;
            _openAcross.Clear();
            CorridorClear = -1f;
            _lanePoints.Clear();
            _laneWith.Clear();
            _gateOpen = false;
            Closed = false;
            SideIn = false;
            Surveyed = false;
            Up = false;
            Lowered = false;
            HasImpact = false;
            _carveAt = 0f;
            if (_props != null)
            {
                _props.SetActive(false);   // the walls with them: the navigation mesh is whole again
            }
            if (_scatter != null)
            {
                _scatter.SetActive(false);
            }
        }

        /// <summary>
        /// Looks again at every road point the last scene switched off, after Open, and opens any
        /// that is still closed: the proof that the road graph was left as it was found. Returns how
        /// many had to be opened (none, unless Open was cut short). Not for use while a scene is on.
        /// </summary>
        public int Recheck()
        {
            int reopened = 0;
            for (int i = 0; i < _touched.Count; i++)
            {
                Waypoint point = _touched[i];
                if (point != null && point.temporaryDisabled)
                {
                    point.temporaryDisabled = false;
                    reopened++;
                }
            }
            _touched.Clear();
            return reopened;
        }

        /// <summary>
        /// Holds traffic for a response vehicle that is leaving round the scene by a lane beside the wrecks:
        /// nothing more is let past the scene into the lanes the gate works (the gate stays shut), so that lane
        /// is empty when the vehicle pulls out into it. ReleaseExit (or Open) undoes it.
        /// </summary>
        public void HoldGateShut()
        {
            ReleaseExit();
            if (!Surveyed || _failed || !TrafficTools.Ready)
            {
                return;
            }
            _gateHold = -1;
            SetGates(false);
        }

        /// <summary>The lane held for a leaving vehicle is given back and the gate goes back on its timer. Safe at any time.</summary>
        public void ReleaseExit()
        {
            if (_gateHold != 0)
            {
                _gateHold = 0;
                SetGates(!Up && !_crewHold);   // shut while the tape is up; before that the gated lane runs freely
                _gateAt = Time.time + 3f;
            }
        }

        /// <summary>
        /// Holds traffic back while the ambulance crew wheels its stretcher along a lane the gate works (true), and
        /// gives the lane back when it is off it (false): the gate stays shut meanwhile, at the hold line well
        /// before the scene, so nothing new comes along that lane. Open undoes it too. Safe at any time.
        /// </summary>
        public void HoldForCrew(bool hold)
        {
            if (_crewHold == hold)
            {
                return;
            }
            _crewHold = hold;
            if (!Surveyed || _failed || _gates.Count == 0 || !TrafficTools.Ready)
            {
                return;
            }
            if (hold)
            {
                SetGates(false);
                Logger?.Info("Cordon: the lane traffic is let past in is held shut while the ambulance crew is on it.");
            }
            else if (_gateHold == 0)
            {
                SetGates(!Up);
                _gateAt = Time.time + 3f;
            }
        }

        /// <summary>
        /// True if a point lies in a lane beside the scene that traffic is let past in (a lane the gate works), give
        /// or take a margin: the ambulance crew keeps out of it where it can.
        /// </summary>
        public bool InOpenLane(Vector3 point, float margin)
        {
            if (!Surveyed || _openAcross.Count == 0)
            {
                return false;
            }
            Vector3 offset = point - _scene;
            float along = Vector3.Dot(offset, _forward);
            if (along < _alongMin - Apron || along > _alongMax + Apron)
            {
                return false;
            }
            float across = Vector3.Dot(offset, _right);
            for (int i = 0; i < _openAcross.Count; i++)
            {
                if (Mathf.Abs(across - _openAcross[i]) < 1.75f + margin)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if a point is inside the rectangle the tape runs round (or will), by a margin. False if never measured.</summary>
        public bool Inside(Vector3 point, float margin)
        {
            if (!Surveyed)
            {
                return false;
            }
            Vector3 offset = point - _scene;
            float along = Vector3.Dot(offset, _forward);
            float across = Vector3.Dot(offset, _right);
            return along > _back + margin && along < _front - margin && across > _left + margin && across < _rightEdge - margin;
        }

        /// <summary>
        /// The nearest place to a point that is outside that rectangle by a margin, straight out through its nearest
        /// side: for whoever has no business inside the tape. The point itself if it already is, or if never measured.
        /// </summary>
        public Vector3 KeepOutside(Vector3 point, float margin)
        {
            if (!Inside(point, -margin))
            {
                return point;
            }
            Vector3 offset = point - _scene;
            float along = Vector3.Dot(offset, _forward);
            float across = Vector3.Dot(offset, _right);
            float toLeft = across - (_left - margin);
            float toRight = _rightEdge + margin - across;
            float toBack = along - (_back - margin);
            float toFront = _front + margin - along;
            float least = Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toBack, toFront));
            if (least == toLeft)
            {
                return point - _right * toLeft;
            }
            if (least == toRight)
            {
                return point + _right * toRight;
            }
            return least == toBack ? point - _forward * toBack : point + _forward * toFront;
        }

        /// <summary>
        /// The tape along the side the open lane runs past is taken in (true) while a response unit drives by a
        /// hand's breadth from it, and run out again (false) once it is past. Only the tape: its posts stay
        /// where they are, and so does the wall that keeps people on foot out. Does nothing if that side has
        /// no tape, or the tape is not up. Safe at any time.
        /// </summary>
        public void TakeInSide(bool taken)
        {
            if (SideIn == taken || _tapes == null || (taken && (!Up || Lowered || _sideTo <= _sideFrom)))
            {
                return;
            }
            SideIn = taken;
            for (int i = _sideFrom; i < _sideTo && i < _tapes.Length; i++)
            {
                if (_tapes[i] != null)
                {
                    _tapes[i].gameObject.SetActive(!taken);
                }
            }
            Logger?.Info(taken
                ? $"Cordon: {_sideTo - _sideFrom} length(s) of tape along the open lane taken in while the ambulance passes."
                : "Cordon: the tape along the open lane is run out again.");
        }

        /// <summary>
        /// Metres from a point to the far side of the taped area, measured the way a vehicle there faces: what it
        /// still has in front of it to be through the scene. Negative once it is beyond. 0 if never measured.
        /// </summary>
        public float Through(Vector3 from, Vector3 facing)
        {
            if (!Surveyed)
            {
                return 0f;
            }
            float along = Vector3.Dot(from - _scene, _forward);
            return Vector3.Dot(facing, _forward) >= 0f ? _front - along : along - _back;
        }

        /// <summary>
        /// The rectangle the tape runs round (or will), for whoever plans a way into it: the road's own frame at
        /// the scene and the rectangle's four edges in it, in metres from the scene. False if never measured.
        /// </summary>
        public bool Frame(out Vector3 scene, out Vector3 forward, out Vector3 right, out float back, out float front, out float left, out float rightEdge)
        {
            scene = _scene;
            forward = _forward;
            right = _right;
            back = _back;
            front = _front;
            left = _left;
            rightEdge = _rightEdge;
            return Surveyed;
        }

        /// <summary>Where across the near end its opening is, in metres from the scene.</summary>
        public float DoorAcross => _doorAcross;

        /// <summary>
        /// Metres from anything of the ambulance crew on its way to the nearest post, cone, barrier or length of
        /// tape, as last measured (after the tape was last run, or the cones last laid); -1 with no way set or no tape up.
        /// </summary>
        public float CorridorClear { get; private set; } = -1f;

        /// <summary>
        /// The ambulance crew's way out and back, once it has been planned (CrewWay): from now on nothing of the
        /// cordon stands on it. Every place a post, a cone or a barrier would have is tested against all that the
        /// crew sweeps over on the way (the bed, whoever pushes it, whoever walks beside it, and every swing), and
        /// refused within 0.3 m of it; no length of tape is run across it; where it crosses a side of the tape
        /// that side has an opening at least 2.6 m wide, and the near end's own opening is put where it crosses
        /// that end. If the tape is already up and stands clear of the way as it is, nothing moves; if not, it is
        /// run again round the way (the cones stay as they are, but any on it is taken in). What was measured is
        /// said in the log. Null for both forgets it.
        /// </summary>
        public void SetCorridor(CrewPath there, CrewPath back)
        {
            _wayOut = there != null && there.Built ? there : null;
            _wayHome = back != null && back.Built ? back : null;
            CorridorClear = -1f;
            if (!Surveyed || _failed || !HasWay)
            {
                return;
            }
            if (!Up || Lowered || _props == null || !Shapes.Available)
            {
                PlaceDoor();   // the tape goes up round it when the police arrive
                return;
            }
            try
            {
                // Cones first: they were laid before the way was known, and one on it is simply taken in (the tape need not be run again for that).
                int cones = 0;
                for (int i = 0; i < _cones.Length; i++)
                {
                    if (_cones[i].gameObject.activeSelf && WayClearance(_cones[i].position) - 0.15f < CrewClear - 0.02f)
                    {
                        _cones[i].gameObject.SetActive(false);
                        cones++;
                    }
                }
                if (cones > 0)
                {
                    Logger?.Info($"Cordon: {cones} cone(s) stood on the ambulance crew's way and are taken in.");
                }
                float clear = MeasureWay(false, out _, out string nearest);
                if (clear >= CrewClear - 0.02f)
                {
                    CorridorClear = clear;
                    Logger?.Info($"Cordon: the tape stands clear of the crew's way as it is: the nearest thing ({nearest}) is {clear:F2} m from anything of the crew ({CrewClear:F2} m wanted): PASS.");
                    return;
                }
                Logger?.Info($"Cordon: the crew's way comes within {clear:F2} m of the cordon as it stands ({nearest}); the tape is run again round it.");
                _oldCount = Mathf.Min(_wallCount, Walls);
                for (int i = 0; i < _oldCount; i++)
                {
                    _oldFrom[i] = _wallFrom[i];
                    _oldTo[i] = _wallTo[i];
                }
                PlaceDoor();
                _redress = true;
                Dress(Time.time);
            }
            catch (Exception e)
            {
                Fail(e);
            }
            finally
            {
                _redress = false;
                _oldCount = 0;
            }
        }

        private bool HasWay => _wayOut != null || _wayHome != null;

        private bool _redress;   // the tape is being run again round the crew's way: the cones are left as they stand

        // Metres from a point to anything of the crew on its way, out or back.
        private float WayClearance(Vector3 point)
        {
            float clear = float.MaxValue;
            if (_wayOut != null)
            {
                clear = Mathf.Min(clear, _wayOut.Clearance(point.x, point.z));
            }
            if (_wayHome != null)
            {
                clear = Mathf.Min(clear, _wayHome.Clearance(point.x, point.z));
            }
            return clear;
        }

        // The part of a straight line that the crew comes within a distance of, out or back, as fractions of it. False if none.
        private bool WayCrossing(Vector3 from, Vector3 to, float within, out float low, out float high)
        {
            low = 1f;
            high = 0f;
            bool any = false;
            for (int i = 0; i < 2; i++)
            {
                CrewPath way = i == 0 ? _wayOut : _wayHome;
                if (way != null && way.Crossing(from.x, from.z, to.x, to.z, within, out float first, out float last))
                {
                    low = Mathf.Min(low, first);
                    high = Mathf.Max(high, last);
                    any = true;
                }
            }
            return any;
        }

        // The near end's opening is where the crew's way crosses that end, and at least wide enough for it. If it does
        // not come in by the near end, the opening stays where it was.
        private void PlaceDoor()
        {
            if (!WayCrossing(Point(_back, _left), Point(_back, _rightEdge), CrewClear, out float low, out float high))
            {
                return;
            }
            float span = _rightEdge - _left;
            float wide = Mathf.Min(Mathf.Max((high - low) * span, CrewGap), span);
            _doorWidth = wide;
            _doorAcross = Mathf.Clamp(_left + span * (low + high) * 0.5f, _left + wide * 0.5f, _rightEdge - wide * 0.5f);
        }

        // How near the crew's way comes to what stands of the cordon: every post, length of tape, cone and barrier. With
        // 'takeIn', whatever is nearer than it may be is switched off (and counted). Says which thing was the nearest.
        private float MeasureWay(bool takeIn, out int taken, out string nearest)
        {
            float least = float.MaxValue;
            float limit = CrewClear - 0.12f;
            taken = 0;
            nearest = "nothing";
            for (int i = 0; i < _placed && i < _posts.Length; i++)
            {
                Tally(_posts[i], WayClearance(_posts[i].position), "a post", takeIn, limit, ref least, ref taken, ref nearest);
            }
            for (int i = 0; i < _taped && i < _tapes.Length; i++)
            {
                if (!_tapes[i].gameObject.activeSelf)
                {
                    continue;
                }
                float clear = float.MaxValue;
                int pieces = Mathf.Max(1, Mathf.CeilToInt((_tapeTo[i] - _tapeFrom[i]).magnitude / 0.25f));
                for (int j = 0; j <= pieces; j++)
                {
                    clear = Mathf.Min(clear, WayClearance(Vector3.Lerp(_tapeFrom[i], _tapeTo[i], (float)j / pieces)));
                }
                Tally(_tapes[i], clear, "a length of tape", takeIn, limit, ref least, ref taken, ref nearest);
            }
            for (int i = 0; i < _cones.Length; i++)
            {
                Tally(_cones[i], WayClearance(_cones[i].position) - 0.15f, "a cone", takeIn, limit, ref least, ref taken, ref nearest);
            }
            for (int i = 0; i < _barriers.Length; i++)
            {
                Transform barrier = _barriers[i];
                Vector3 reach = barrier.right * (BarrierWidth * 0.5f - 0.2f);
                float clear = Mathf.Min(WayClearance(barrier.position), Mathf.Min(WayClearance(barrier.position + reach), WayClearance(barrier.position - reach))) - 0.2f;
                Tally(barrier, clear, "a barrier", takeIn, limit, ref least, ref taken, ref nearest);
            }
            return least;
        }

        private static void Tally(Transform prop, float clear, string what, bool takeIn, float limit, ref float least, ref int taken, ref string nearest)
        {
            if (!prop.gameObject.activeSelf)
            {
                return;
            }
            if (takeIn && clear < limit)
            {
                prop.gameObject.SetActive(false);
                taken++;
                return;
            }
            if (clear < least)
            {
                least = clear;
                nearest = what;
            }
        }

        /// <summary>True if a point is beyond the far end of the taped area by so many metres, the way the road runs: a unit there has passed the scene.</summary>
        public bool Beyond(Vector3 point, float by)
        {
            return Surveyed && Vector3.Dot(point - _scene, _forward) > _front + by;
        }

        /// <summary>
        /// How much of the road outside an end of the taped area a standing vehicle takes up: for
        /// LayTaper, which puts the cones beyond it. Adds to whichever of the two it lies at.
        /// </summary>
        public void Reach(Vector3 vehicle, float halfLength, ref float near, ref float far)
        {
            if (!Surveyed)
            {
                return;
            }
            float along = Vector3.Dot(vehicle - _scene, _forward);
            if (along < _back)
            {
                near = Mathf.Max(near, _back - along + halfLength + 1.5f);
            }
            else if (along > _front)
            {
                far = Mathf.Max(far, along - _front + halfLength + 1.5f);
            }
        }

        // ---------------------------------------------------------------- the road

        // The road's direction at the scene, and how far the wrecks reach along and across it.
        private void Measure(Vector3 scene, Vector3 heading, List<Footprint> footprints)
        {
            _scene = scene;
            Waypoint nearest = TrafficManager.Instance.GetClosestWaypoint(scene, 40f);
            Vector3 lane = nearest != null ? LaneDirection(nearest) : Vector3.zero;
            if (lane == Vector3.zero)
            {
                lane = heading;
                lane.y = 0f;
            }
            _forward = lane.sqrMagnitude < 0.01f ? Vector3.forward : lane.normalized;
            _right = Vector3.Cross(Vector3.up, _forward);

            _alongMin = _acrossMin = _solidMin = float.MaxValue;
            _alongMax = _acrossMax = _solidMax = float.MinValue;
            _anySolid = false;
            for (int i = 0; i < footprints.Count; i++)
            {
                Footprint print = footprints[i];
                Vector3 forward = print.Forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude < 0.01f ? _forward : forward.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = print.Centre - scene
                                    + forward * (corner < 2 ? print.HalfLength : -print.HalfLength)
                                    + right * (corner % 2 == 0 ? print.HalfWidth : -print.HalfWidth);
                    float along = Vector3.Dot(point, _forward);
                    float across = Vector3.Dot(point, _right);
                    _alongMin = Mathf.Min(_alongMin, along);
                    _alongMax = Mathf.Max(_alongMax, along);
                    _acrossMin = Mathf.Min(_acrossMin, across);
                    _acrossMax = Mathf.Max(_acrossMax, across);
                    if (print.Vehicle)
                    {
                        _anySolid = true;
                        _solidMin = Mathf.Min(_solidMin, across);
                        _solidMax = Mathf.Max(_solidMax, across);
                    }
                }
            }
            _bandMin = _acrossMin - Side;
            _bandMax = _acrossMax + Side;
        }

        private void CloseLanes(bool shut)
        {
            TrafficManager traffic = TrafficManager.Instance;
            float reach = Mathf.Max(-_alongMin, _alongMax) + Apron + Mathf.Max(-_acrossMin, _acrossMax) + RoadWidth;
            Found.Clear();
            traffic.GetClosestWaypoint(_scene, reach, Collect);

            // First what the wrecks block: each lane from the start of its apron to just past them.
            _bothWays = false;
            _gateSide = 0f;
            _gateAgainst = false;
            bool junction = false;
            bool crossed = false;
            float near = Margin + 1.5f;
            Lane.Clear();
            for (int i = Found.Count - 1; i >= 0; i--)
            {
                Waypoint point = Found[i];
                Locate(point, out float along, out float across, out bool withTraffic);
                bool alongside = along > _alongMin - near && along < _alongMax + near;
                bool forked = point.neighbors.Count > 1 || point.prev.Count > 1 || point.IsInIntersection();
                // A junction within the apron's length behind the scene: the ambulance may have to stand beyond it.
                crossed |= forked && !alongside && across > _acrossMin - RoadWidth && across < _acrossMax + RoadWidth
                           && along > _alongMin - Apron && along < _alongMin;
                // The apron is only ever plain lane: it ends where a junction begins.
                bool onApron = !alongside && !forked
                               && (withTraffic ? along <= _alongMin - near && along > _alongMin - Apron
                                   : along >= _alongMax + near && along < _alongMax + Apron);
                bool inLane = across > _acrossMin - LaneClear && across < _acrossMax + LaneClear;
                bool blocked = inLane && (alongside || onApron);
                bool beside = alongside && !inLane && across > _acrossMin - RoadWidth && across < _acrossMax + RoadWidth;
                if (!blocked && !beside)
                {
                    Found.RemoveAt(i);
                    continue;
                }
                junction |= alongside && forked;
                if (blocked)
                {
                    if (shut)
                    {
                        Shut(point);
                    }
                    _lanePoints.Add(point);
                    _laneWith.Add(withTraffic);
                    _bandMin = Mathf.Min(_bandMin, across - 1.6f);
                    _bandMax = Mathf.Max(_bandMax, across + 1.6f);
                    _bothWays |= !withTraffic;
                    Found.RemoveAt(i);
                }
                else
                {
                    Lane.Add(point.listIndex);   // a lane beside the wrecks that is clear
                }
            }

            if (!shut)
            {
                Found.Clear();
                return;
            }

            // A junction at or just behind the scene: every way across it that passes close to the wrecks, or
            // between them and where the ambulance will stand, is shut as well, so that nothing drives through
            // the crew or across the stretcher's way. Traffic takes the junction's other ways, or waits.
            _crossClosed = 0;
            if (junction || crossed)
            {
                CloseCrossings(traffic, junction);
            }

            // A clear lane gets a gate: the first of its points alongside the scene. At a junction there is no
            // such lane to work: the blocked ways are shut and traffic takes the others.
            for (int i = 0; i < Found.Count && !junction; i++)
            {
                Waypoint point = Found[i];
                if (!HasPrevious(point, Lane))
                {
                    // Held well before the scene, not at the tape: behind where the ambulance stands in the lane beside it,
                    // so that what waits does not stand beside its rear doors, where its crew brings the stretcher out and round.
                    Waypoint hold = HoldLine(traffic, point);
                    _gates.Add(hold);
                    _touched.Add(hold);
                    Locate(point, out _, out float across, out bool withTraffic);
                    _openAcross.Add(across);
                    _gateAgainst |= !withTraffic;
                    _gateSide = across > (_acrossMin + _acrossMax) * 0.5f ? 1f : -1f;
                }
            }

            // And the way into this stretch is shut where traffic last had a choice.
            Entries.Clear();
            EntryParents.Clear();
            Visited.Clear();
            int closedHere = _closed.Count;
            for (int i = 0; i < closedHere; i++)
            {
                FindEntries(traffic, _closed[i]);
            }
            for (int i = 0; i < _gates.Count; i++)
            {
                FindEntries(traffic, _gates[i]);
            }
            for (int i = 0; i < Entries.Count; i++)
            {
                // Never the only way on from where traffic stands: it would wait there for ever.
                if (HasOtherWay(traffic, EntryParents[i]))
                {
                    Shut(Entries[i]);
                }
            }
            Entries.Clear();
            EntryParents.Clear();
            Found.Clear();
        }

        // Shuts every open road point that lies in the crew's way: within working room of the wrecks across the
        // road and, along it, from where the ambulance's rear doors will be to just past the wrecks. In a
        // junction that is every connector crossing that strip; plain lane beside the scene is left to the gate
        // unless the scene is in the junction itself.
        private void CloseCrossings(TrafficManager traffic, bool inJunction)
        {
            float back = Margin + 1.5f;
            if (Stand(false, 9f, out _, out Vector3 stand))
            {
                // To a little behind the ambulance: its middle, half its length, and room at its rear doors.
                back = Mathf.Max(back, _alongMin - Vector3.Dot(stand - _scene, _forward) + 6.5f);
            }
            float reach = Mathf.Max(-_alongMin + back, _alongMax) + Mathf.Max(-_acrossMin, _acrossMax) + RoadWidth;
            Crossing.Clear();
            traffic.GetClosestWaypoint(_scene, reach, CollectCrossing);
            for (int i = 0; i < Crossing.Count; i++)
            {
                Waypoint point = Crossing[i];
                if (point.temporaryDisabled)
                {
                    continue;
                }
                Locate(point, out float along, out float across, out _);
                bool forked = IsForked(point);
                float room = forked ? WorkRoom + 1.2f : inJunction ? WorkRoom : LaneClear;
                if (along > _alongMin - back && along < _alongMax + Margin + 1.5f && across > _acrossMin - room && across < _acrossMax + room)
                {
                    Shut(point);
                    _crossClosed++;
                    _bandMin = Mathf.Min(_bandMin, across - 1.6f);
                    _bandMax = Mathf.Max(_bandMax, across + 1.6f);
                }
            }
            Crossing.Clear();
        }

        private static readonly List<Waypoint> Crossing = new List<Waypoint>(96);
        private static readonly Predicate<Waypoint> CollectCrossing = KeepCrossing;

        private static bool KeepCrossing(Waypoint point)
        {
            Crossing.Add(point);
            return false;
        }

        // Walks back along a lane to the point where traffic turned into it.
        private static void FindEntries(TrafficManager traffic, Waypoint from)
        {
            Walk.Clear();
            Walk.Push(from);
            float budget = EntryWalk;
            while (Walk.Count > 0 && budget > 0f)
            {
                Waypoint point = Walk.Pop();
                for (int i = 0; i < point.prev.Count; i++)
                {
                    Waypoint before = traffic.GetWaypoint(point.prev[i]);
                    if (before == null || !Visited.Add(before.listIndex))
                    {
                        continue;
                    }
                    if (before.neighbors.Count > 1)
                    {
                        Entries.Add(point);
                        EntryParents.Add(before);
                        continue;
                    }
                    budget -= Vector3.Distance(point.position, before.position);
                    Walk.Push(before);
                }
            }
            Walk.Clear();
        }

        private static bool HasOtherWay(TrafficManager traffic, Waypoint parent)
        {
            for (int i = 0; i < parent.neighbors.Count; i++)
            {
                Waypoint way = traffic.GetWaypoint(parent.neighbors[i]);
                if (way != null && !way.temporaryDisabled && !Entries.Contains(way))
                {
                    return true;
                }
            }
            return false;
        }

        // The point of an open lane where its traffic is held: back along the lane from where it comes alongside the
        // scene, as far as the hold line, stopping short of any junction (what waits must not stand in one).
        private static Waypoint HoldLine(TrafficManager traffic, Waypoint from)
        {
            Waypoint hold = from;
            float walked = 0f;
            while (walked < HoldBack && hold.prev != null && hold.prev.Count == 1 && !IsForked(hold))
            {
                Waypoint before = traffic.GetWaypoint(hold.prev[0]);
                if (before == null || before.temporaryDisabled || IsForked(before))
                {
                    break;
                }
                walked += Vector3.Distance(hold.position, before.position);
                hold = before;
            }
            return hold;
        }

        private static bool HasPrevious(Waypoint point, HashSet<int> among)
        {
            for (int i = 0; i < point.prev.Count; i++)
            {
                if (among.Contains(point.prev[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private void Locate(Waypoint point, out float along, out float across, out bool withTraffic)
        {
            Vector3 offset = point.position - _scene;
            along = Vector3.Dot(offset, _forward);
            across = Vector3.Dot(offset, _right);
            withTraffic = Vector3.Dot(LaneDirection(point), _forward) >= 0f;
        }

        // Which way a lane runs at one of its points; zero if it cannot be told.
        private static Vector3 LaneDirection(Waypoint point)
        {
            TrafficManager traffic = TrafficManager.Instance;
            Vector3 direction = Vector3.zero;
            if (point.neighbors != null && point.neighbors.Count > 0)
            {
                Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
                direction = next != null ? next.position - point.position : Vector3.zero;
            }
            else if (point.prev != null && point.prev.Count > 0)
            {
                Waypoint before = traffic.GetWaypoint(point.prev[0]);
                direction = before != null ? point.position - before.position : Vector3.zero;
            }
            direction.y = 0f;
            return direction.sqrMagnitude < 0.01f ? Vector3.zero : direction.normalized;
        }

        // A road point is only ever closed by us if it was open, so that Open gives back exactly what was taken.
        private void Shut(Waypoint point)
        {
            if (!point.temporaryDisabled)
            {
                point.temporaryDisabled = true;
                _closed.Add(point);
                _touched.Add(point);
            }
        }

        private void SetGates(bool open)
        {
            _gateOpen = open;
            for (int i = 0; i < _gates.Count; i++)
            {
                _gates[i].temporaryDisabled = !open;
            }
        }

        // Kept as a field so that asking the traffic system for the points near the scene allocates nothing.
        private static bool Keep(Waypoint point)
        {
            Found.Add(point);
            return false;
        }

        // Traffic that has stopped in the blocked lanes, on the apron or among the wrecks: taken off the
        // road if nobody is looking (the game does the same to any car that stands still for long).
        private void ClearStranded()
        {
            List<VehicleComponent> cars = Manager.GetVehicleList();
            TrafficManager traffic = TrafficManager.Instance;
            for (int i = 0; i < cars.Count; i++)
            {
                VehicleComponent car = cars[i];
                if (car == null || !car.gameObject.activeInHierarchy || car.presetPath != null || car.GetComponent<TaxiController>() != null
                    || traffic.IsVehicleIgnored(car.GetIndex()) || car.GetCurrentSpeed() > 1f)
                {
                    continue;
                }
                Vector3 offset = car.transform.position - _scene;
                float along = Vector3.Dot(offset, _forward);
                float across = Vector3.Dot(offset, _right);
                if (along > _alongMin - Apron && along < _alongMax + (_bothWays ? Apron : Margin) && across > _bandMin && across < _bandMax
                    && !StreetSpots.IsInView(car.transform.position))
                {
                    TrafficTools.Remove(car, "the cordon: standing in a closed lane, out of view");
                }
            }
        }

        // ---------------------------------------------------------------- what the player sees

        // The taped rectangle: the wrecks and a working margin, pulled in to the kerb where a pavement
        // is close, so that it stays on the carriageway and the pavement stays open. Then its corners,
        // which side the entries are on, and where the officer on traffic duty stands.
        private void Survey()
        {
            _back = _alongMin - Margin;
            _front = _alongMax + Margin;
            _left = _acrossMin - Side;
            _rightEdge = _acrossMax + Side;

            float middleAlong = (_alongMin + _alongMax) * 0.5f;
            float middleAcross = (_acrossMin + _acrossMax) * 0.5f;
            _crossing = IsPavement(middleAlong, middleAcross);
            _kerbLeft = !_crossing && FindKerb(-1f, middleAlong, middleAcross, ref _left);
            _kerbRight = !_crossing && FindKerb(1f, middleAlong, middleAcross, ref _rightEdge);
            // Anywhere people on foot have a way across the road inside the tape, it is not walled.
            for (int i = 0; i < 3 && !_crossing; i++)
            {
                float along = Mathf.Lerp(_back, _front, i * 0.5f);
                _crossing = IsPavement(along, Mathf.Lerp(_left, _rightEdge, 0.25f)) || IsPavement(along, Mathf.Lerp(_left, _rightEdge, 0.75f));
            }

            // A parking lane with cars in it is not carriageway: the tape stops short of whatever is parked there
            // (and of street furniture) instead of running on to the kerb through it. That side then counts
            // as a kerb: nothing passes there, so it gets no cones.
            bool pavementLeft = _kerbLeft;
            bool pavementRight = _kerbRight;
            bool parkedLeft = ClampToParked(-1f, ref _left);
            bool parkedRight = ClampToParked(1f, ref _rightEdge);
            _kerbLeft |= parkedLeft;
            _kerbRight |= parkedRight;
            // An edge pulled in for parked cars is pushed out again to clear the wrecks: the tape never runs
            // through what it is there to go round. Not at a kerb: a wreck lying against (or over) the kerb
            // would take the tape onto the pavement and up whatever stands there. There the line stays on
            // the carriageway and the wreck itself closes that stretch (no post stands in a vehicle).
            float left = _left;
            float right = _rightEdge;
            if (!pavementLeft)
            {
                _left = Mathf.Min(_left, _acrossMin - 0.3f);
            }
            if (!pavementRight)
            {
                _rightEdge = Mathf.Max(_rightEdge, _acrossMax + 0.3f);
            }
            if (_rightEdge - _left < 2.5f)
            {
                // Wrecks almost wholly off the carriageway: no room left between the kerbs to tape. As it was done before.
                _left = Mathf.Min(left, _acrossMin - 0.3f);
                _rightEdge = Mathf.Max(right, _acrossMax + 0.3f);
            }
            CrewRoom(pavementLeft || parkedLeft, pavementRight || parkedRight);

            _entry = _kerbRight && !_kerbLeft ? 1f : _kerbLeft && !_kerbRight ? -1f : _gateSide != 0f ? -_gateSide : 1f;

            // The near end's opening is in line with where the ambulance stands, so that its crew and the
            // stretcher go straight in and out by it, along closed road.
            float span = _rightEdge - _left;
            _doorWidth = Mathf.Min(EntryGap, span * 0.6f);
            float door = (_left + _rightEdge) * 0.5f;
            if (Stand(false, 9f, out _, out Vector3 ambulance))
            {
                door = Vector3.Dot(ambulance - _scene, _right);
            }
            float reach = Mathf.Max(0f, span * 0.5f - _doorWidth * 0.5f - 0.6f);
            _doorAcross = Mathf.Clamp(door, (_left + _rightEdge) * 0.5f - reach, (_left + _rightEdge) * 0.5f + reach);

            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = Point(i == 0 || i == 3 ? _back - 1f : _front + 1f, i < 2 ? _left - 1f : _rightEdge + 1f);
                _corners[i] = StreetSpots.OnRoad(corner, 1.5f, out Vector3 ground) ? ground : corner;
            }
            // At the end traffic arrives at, on the side it is let past.
            Vector3 duty = Point(_back - 1.6f, _entry > 0f ? _left - 0.8f : _rightEdge + 0.8f);
            DirectorPost = StreetSpots.OnRoad(duty, 2f, out Vector3 stand) ? stand : duty;
            Oncoming = DirectorPost - _forward * 20f;
            // ...and, for when a response unit leaves by that lane: the same end, well in from the lane (no further than the middle).
            float step = Mathf.Min(1.6f, (_rightEdge - _left) * 0.5f);
            Vector3 inward = Point(_back - 1.6f, _entry > 0f ? _left + step : _rightEdge - step);
            PassPost = StreetSpots.OnRoad(inward, 1.5f, out Vector3 beside) ? beside : inward;

            // Where people with no business inside are sent: the pavement by the near entry, or failing
            // that the road just outside it.
            Vector3 aside = Point(_back - 2.5f, (_entry > 0f ? _rightEdge : _left) + _entry * 2.5f);
            Kerbside = StreetSpots.OnPavement(aside, 4f, out Vector3 kerb) ? kerb
                : StreetSpots.OnRoad(Point(_back - 3f, (_entry > 0f ? _rightEdge : _left) - _entry * 1f), 2f, out kerb) ? kerb : DirectorPost;

            // Just outside the far end's opening, looking up the road away from the scene.
            float opening = Mathf.Min(EntryGap, span * 0.5f);
            Vector3 gate = Point(_front + BarrierOut + 0.6f, (_entry > 0f ? _rightEdge : _left) - _entry * opening * 0.5f);
            EntryPost = StreetSpots.OnRoad(gate, 1.5f, out Vector3 post) ? post : gate;
            EntryLook = EntryPost + _forward * 12f;
        }

        // The crews walk past the vehicles held at the scene, the stretcher with them: on one side of those
        // there must be CrewLane between vehicle and tape, a metre clear of the bodywork and room to walk
        // in. If neither side has it, the side that is free to move is taken out to it: never the side
        // traffic is let past on (the tape would stand in an open lane), never over a kerb or into parked
        // cars. Where no side can give it, that is said in the log, once for the scene.
        private void CrewRoom(bool fixedLeft, bool fixedRight)
        {
            if (!_anySolid)
            {
                return;
            }
            float roomLeft = _solidMin - _left;
            float roomRight = _rightEdge - _solidMax;
            if (roomLeft >= CrewLane || roomRight >= CrewLane)
            {
                return;
            }
            bool canLeft = !fixedLeft && _gateSide >= 0f;
            bool canRight = !fixedRight && _gateSide <= 0f;
            if (canLeft && (!canRight || roomLeft >= roomRight))
            {
                _left = _solidMin - CrewLane;
            }
            else if (canRight)
            {
                _rightEdge = _solidMax + CrewLane;
            }
            else
            {
                Logger?.Info($"Cordon: no side of the held vehicle(s) can be given {CrewLane:F1} m for the crews (kerb, parked cars or the open lane): {roomLeft:F1} m on the left, {roomRight:F1} m on the right.");
            }
        }

        // Looks outwards from the wrecks for a parked vehicle or street furniture standing on the carriageway
        // within the rectangle's length. If there is one, the edge on that side stops short of it.
        private bool ClampToParked(float side, ref float edge)
        {
            if (_parkedMask == 0)
            {
                _parkedMask = LayerMask.GetMask("ParkedVehicles", "PlayerVehicles", "StreetProps");
                if (_parkedMask == 0)
                {
                    return false;
                }
            }
            float from = side > 0f ? _acrossMax + 0.35f : _acrossMin - 0.35f;
            if ((edge - from) * side <= 0f)
            {
                return false;   // the kerb has already brought this edge in to the wrecks: nothing between them to look at
            }
            var strip = new Vector3(0.15f, 0.5f, (_front - _back) * 0.5f);
            Quaternion facing = Quaternion.LookRotation(_forward, Vector3.up);
            for (float step = 0f; step <= Mathf.Abs(edge - from) + 0.01f; step += 0.3f)
            {
                float across = from + side * step;
                Vector3 at = Point((_back + _front) * 0.5f, across);
                if (StreetSpots.Ground(at, out float ground))
                {
                    at.y = ground;
                }
                if (Physics.CheckBox(at + Vector3.up * 0.9f, strip, facing, _parkedMask, QueryTriggerInteraction.Ignore))
                {
                    edge = across - side * ParkedGap;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The nearest place to a point that is inside the rectangle the tape runs round (or will,
        /// once it is up), at least a given distance from its line. The point itself if it already
        /// is, or if the scene has not been measured.
        /// </summary>
        public Vector3 KeepInside(Vector3 point, float inset)
        {
            if (!Surveyed)
            {
                return point;
            }
            Vector3 offset = point - _scene;
            float along = Vector3.Dot(offset, _forward);
            float across = Vector3.Dot(offset, _right);
            float insetAlong = Mathf.Min(inset, (_front - _back) * 0.5f);
            float insetAcross = Mathf.Min(inset, (_rightEdge - _left) * 0.5f);
            float keptAlong = Mathf.Clamp(along, _back + insetAlong, _front - insetAlong);
            float keptAcross = Mathf.Clamp(across, _left + insetAcross, _rightEdge - insetAcross);
            return point + _forward * (keptAlong - along) + _right * (keptAcross - across);
        }

        // Looks outwards from the wrecks for the pavement on one side. If it is close, the edge of the
        // rectangle on that side is pulled in to just short of it.
        private bool FindKerb(float side, float along, float middle, ref float edge)
        {
            float reach = Mathf.Abs(edge - middle) + KerbReach;
            for (float step = 0.5f; step <= reach; step += 0.5f)
            {
                float across = middle + side * step;
                if (IsPavement(along, across))
                {
                    float kerb = across - side * KerbInset;
                    edge = side > 0f ? Mathf.Min(edge, kerb) : Mathf.Max(edge, kerb);
                    return true;
                }
            }
            return false;
        }

        // True where the game's own pedestrians can walk: a pavement, or a crossing.
        private bool IsPavement(float along, float across)
        {
            Vector3 point = Point(along, across);
            StreetSpots.Ground(point, out float ground);
            point.y = ground;
            return NavMesh.SamplePosition(point, out _, 0.35f, StreetSpots.Pavement);
        }

        private Vector3 Point(float along, float across)
        {
            return _scene + _forward * along + _right * across;
        }

        private void Dress(float now)
        {
            if (_props != null && PropLibrary.Ready && !_modelled)
            {
                // Built from plain shapes before the modelled props had loaded: once, built again with them.
                UnityEngine.Object.Destroy(_props);
                _props = null;
            }
            if (_props == null)
            {
                Build();
            }
            float entryEdge = _entry > 0f ? _rightEdge : _left;
            float otherEdge = _entry > 0f ? _left : _rightEdge;
            float width = Mathf.Abs(entryEdge - otherEdge);
            float length = _front - _back;
            float gap = Mathf.Min(EntryGap, width * 0.5f);
            float gapEnd = entryEdge - _entry * gap;   // where each end's tape stops, short of the entry-side corner

            // The near end's opening, in line with the ambulance: its side towards the entry edge, and its other side.
            float doorNear = _doorAcross + _entry * _doorWidth * 0.5f;
            float doorFar = _doorAcross - _entry * _doorWidth * 0.5f;

            // Tape on posts, in two runs that leave both openings: from the near opening to the entry-side corner
            // and along that side; and from the opening's other side round the other three sides, stopping
            // short of the far end's entry-side corner. Every stretch that goes up is walled as it goes.
            _placed = 0;
            _taped = 0;
            _wallCount = 0;
            _tapeBlocked = 0;
            _sideFrom = _sideTo = 0;
            SideIn = false;
            _blockedBy = null;
            _movedPosts = 0;
            _wayRefused = 0;
            _wayOpenings = 0;
            // (An opening that reaches a corner leaves no tape on that side of it: the side then starts at the corner by itself.)
            bool nearEntry = Mathf.Abs(doorNear - entryEdge) > 0.3f;
            bool nearOther = Mathf.Abs(doorFar - otherEdge) > 0.3f;
            if (nearEntry)
            {
                Line(_back, doorNear, _back, entryEdge, fresh: true, outward: -1f);
            }
            bool gateAtEntry = _gateSide != 0f && (_gateSide > 0f) == (_entry > 0f);
            int before = _taped;
            Run(_back, entryEdge, _front, entryEdge, fresh: !nearEntry, outward: 0f);
            if (gateAtEntry)
            {
                _sideFrom = before;
                _sideTo = _taped;
            }
            if (nearOther)
            {
                Line(_back, doorFar, _back, otherEdge, fresh: true, outward: -1f);
            }
            before = _taped;
            Run(_back, otherEdge, _front, otherEdge, fresh: !nearOther, outward: 0f);
            if (_gateSide != 0f && !gateAtEntry)
            {
                _sideFrom = before;
                _sideTo = _taped;
            }
            Run(_front, otherEdge, _front, gapEnd, fresh: false, outward: 1f);
            HideFrom(_posts, _placed);
            HideFrom(_tapes, _taped);

            // Barriers across both ends, facing outwards: end to end across the closed part, none in an opening.
            Quaternion nearFacing = Quaternion.LookRotation(-_forward, Vector3.up);
            int barriers = Barriers(0, _back - BarrierOut, otherEdge, doorFar, nearFacing);
            barriers = Barriers(barriers, _back - BarrierOut, doorNear, entryEdge, nearFacing);
            barriers = Barriers(barriers, _front + BarrierOut, otherEdge, gapEnd, Quaternion.LookRotation(_forward, Vector3.up));
            HideFrom(_barriers, barriers);

            // The cones are a taper, laid when the response vehicles are in (LayTaper).
            if (!_redress)
            {
                HideFrom(_cones, 0);
            }

            // The tape that went up, as walls for anyone on foot: the openings are simply where no tape runs.
            bool walled = !_crossing && _wallCount > 0;
            for (int i = 0; i < Walls; i++)
            {
                bool used = walled && i < _wallCount && SetWall(i);
                _walls[i].gameObject.SetActive(used);
            }
            _props.SetActive(true);
            _carveAt = walled ? now + CarveWait : 0f;
            _carveTries = 0;
            Logger?.Info($"Cordon: tape up, {length:F0} x {width:F0} m, {_placed} post(s), {_taped} length(s) of tape, {barriers} barrier(s), "
                         + (_crossing ? "not walled (a crossing runs through it)"
                             : $"{_wallCount} wall(s), open at the near end {_doorAcross - _left:F1} m from the left edge ({_doorWidth:F1} m wide: the crew's corridor) and at the far end's {(_entry > 0f ? "right" : "left")} corner")
                         + $", kerb {(_kerbLeft ? (_kerbRight ? "both sides" : "left") : _kerbRight ? "right" : "on neither side")}"
                         + (_movedPosts > 0 ? $"; {_movedPosts} post(s) moved in off their line" : "")
                         + (_tapeBlocked > 0 ? $"; {_tapeBlocked} stretch(es) of tape left out, in the way: '{_blockedBy}'" : "") + ".");
            CheckWay(barriers);
        }

        // With the crew's way known: what stands of the cordon is measured against it, anything still on it is taken in,
        // and the result is said. (Run round the way, nothing should be: this is the proof, for the log.)
        private void CheckWay(int barriers)
        {
            CorridorClear = -1f;
            if (!HasWay || !Up || Lowered)
            {
                return;
            }
            float clear = MeasureWay(true, out int taken, out string nearest);
            CorridorClear = clear;
            bool crossesNear = WayCrossing(Point(_back, _left), Point(_back, _rightEdge), CrewClear, out _, out _);
            Logger?.Info("Cordon: run round the ambulance crew's way: "
                         + (crossesNear ? $"it comes in by the near end's opening, {_doorWidth:F1} m wide ({CrewGap - 0.2f:F1} m wanted)" : "it does not come in by the near end")
                         + (_wayOpenings > 0 ? $", {_wayOpenings} more opening(s) where it crosses a side or the far end" : "")
                         + $"; {_wayRefused} place(s) for a post, cone or barrier on it left empty, {taken} thing(s) on it taken in; the nearest left ({nearest}) is "
                         + $"{(clear == float.MaxValue ? 99f : clear):F2} m from anything of the crew ({CrewClear - 0.12f:F2} m at least, {CrewClear:F2} m wanted), {barriers} barrier(s) standing: "
                         + (clear >= CrewClear - 0.12f && (!crossesNear || _doorWidth >= CrewGap - 0.2f) ? "PASS." : "FAIL."));
        }

        // A side of the rectangle (or its far end), with an opening where the crew's way crosses it: tape up to the
        // opening and on from it, a post either side. Without a way, or where it does not cross, the side as it is.
        private void Run(float fromAlong, float fromAcross, float toAlong, float toAcross, bool fresh, float outward)
        {
            Vector3 from = Point(fromAlong, fromAcross);
            Vector3 to = Point(toAlong, toAcross);
            float length = (to - from).magnitude;
            if (!HasWay || length < 0.5f || !WayCrossing(from, to, CrewClear, out float low, out float high))
            {
                Line(fromAlong, fromAcross, toAlong, toAcross, fresh, outward);
                return;
            }
            float more = (CrewGap - (high - low) * length) * 0.5f / length;
            if (more > 0f)
            {
                low -= more;
                high += more;
            }
            _wayOpenings++;
            if (low * length > 0.6f)
            {
                Line(fromAlong, fromAcross, Mathf.Lerp(fromAlong, toAlong, low), Mathf.Lerp(fromAcross, toAcross, low), fresh, outward);
            }
            if ((1f - high) * length > 0.6f)
            {
                Line(Mathf.Lerp(fromAlong, toAlong, high), Mathf.Lerp(fromAcross, toAcross, high), toAlong, toAcross, fresh: true, outward);
            }
            else
            {
                _hasLast = false;   // the opening reaches the corner: the next side starts by itself
            }
        }

        // One wall along a run of tape, from its first post to its last. An end's wall is as deep as its tape and
        // barriers together, so that nobody walks through a barrier either. False if the run is too short to matter.
        private bool SetWall(int index)
        {
            Vector3 run = _wallTo[index] - _wallFrom[index];
            float length = run.magnitude;
            if (length < 0.4f)
            {
                return false;
            }
            Vector3 centre = (_wallFrom[index] + _wallTo[index]) * 0.5f;
            float depth = WallThickness;
            if (_wallOut[index] != 0f)
            {
                depth = BarrierWall;
                centre += _forward * (_wallOut[index] * (BarrierWall - WallThickness) * 0.5f);
            }
            NavMeshObstacle wall = _walls[index];
            wall.carveOnlyStationary = true;
            MeshCuts.Note("a cordon's walls");
            wall.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(run / length, Vector3.up));
            // From a metre under the road to two above it: the mesh need not lie exactly where the road's collider does.
            wall.center = new Vector3(0f, 0.5f, 0f);
            wall.size = new Vector3(depth, 3f, length + (_wallOut[index] != 0f ? 0f : WallThickness));
            return true;
        }

        // Asks the navigation mesh, a moment after the walls went up, whether each really is a hole in it: the mesh
        // right under a wall's middle is gone if it was cut. A wall that was not is set to cut at once (not only
        // when it has stood still) and asked about once more. Said in the log either way, once per raising.
        private void CheckWalls(float now)
        {
            _carveAt = 0f;
            try
            {
                int standing = 0;
                int cut = 0;
                Report.Length = 0;
                for (int i = 0; i < _wallCount && i < Walls; i++)
                {
                    NavMeshObstacle wall = _walls[i];
                    if (!wall.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    standing++;
                    Vector3 at = wall.transform.position;
                    bool whole = false;
                    if (NavMesh.SamplePosition(at, out NavMeshHit hit, 0.6f, StreetActor.RoadFilter))
                    {
                        Vector3 off = hit.position - at;
                        off.y = 0f;
                        whole = off.sqrMagnitude < 0.12f * 0.12f;
                    }
                    if (!whole)
                    {
                        cut++;
                    }
                    else if (_carveTries == 0)
                    {
                        wall.carveOnlyStationary = false;
                        wall.gameObject.SetActive(false);
                        wall.gameObject.SetActive(true);
                    }
                    Report.Append(Report.Length > 0 ? ", " : "").Append(_wallOut[i] != 0f ? "end " : "side ")
                        .Append(wall.size.z.ToString("F1")).Append(whole ? " m NOT cut" : " m cut");
                }
                if (standing == 0)
                {
                    return;
                }
                if (cut < standing && _carveTries == 0)
                {
                    _carveTries = 1;
                    _carveAt = now + CarveWait;
                    Logger?.Info($"Cordon: {cut} of {standing} wall(s) cut out of the navigation mesh so far ({Report}); the rest are told to cut at once and are looked at again.");
                }
                else
                {
                    Logger?.Info($"Cordon: {cut} of {standing} wall(s) cut out of the navigation mesh ({Report})"
                                 + (cut < standing ? "; people on foot can cross the tape where one is not." : "; nobody on foot can cross the tape."));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Lays the cones: a taper upstream of each end that traffic arrives at, a short diagonal
        /// that starts at the far side of the closed lanes and steps across to the side traffic is
        /// let past on (or, with the whole road shut, to the middle of it), beyond whatever response
        /// vehicle stands at that end. Laid again whenever it is called; needs the tape up.
        /// </summary>
        /// <param name="near">Metres before the near end taken up by standing vehicles (see <see cref="Reach"/>); negative to leave that end as it is (a vehicle is still due there).</param>
        /// <param name="far">The same beyond the far end, which only has a taper if a lane running the other way is blocked.</param>
        public void LayTaper(float near, float far)
        {
            if (!Up || Lowered || _failed || _props == null || _cones == null)
            {
                return;
            }
            try
            {
                int nearCones = near >= 0f ? Taper(0, -1f, near) : -1;
                // Traffic reaches the far end by a blocked lane running the other way, or by an open one that
                // does (it passes the scene alongside the tape): either way that approach gets its cones.
                int farCones = far >= 0f && (_bothWays || _gateAgainst) ? Taper(TaperCones, 1f, far) : -1;
                if (HasWay)
                {
                    // (No cone is put on the ambulance crew's way: a place on it is left empty. Measured again, with the cones in.)
                    CorridorClear = MeasureWay(true, out _, out _);
                }
                if (nearCones > 0 || farCones > 0)
                {
                    Logger?.Info("Cordon: taper laid, " + (nearCones > 0 ? nearCones + " cone(s) " + (near > 0f ? near.ToString("F0") + " m " : "") + "upstream of the near end" : "")
                                 + (nearCones > 0 && farCones > 0 ? " and " : "")
                                 + (farCones > 0 ? farCones + " cone(s) " + (far > 0f ? far.ToString("F0") + " m " : "") + "upstream of the far end" : "") + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                LiftTaper();
            }
        }

        /// <summary>The cones are taken in: a response vehicle is about to leave through where they stand.</summary>
        public void LiftTaper()
        {
            if (_cones != null)
            {
                HideFrom(_cones, 0);
            }
        }

        // One end's taper. Its cones are this end's own (six from 'first'); returns how many stand.
        private int Taper(int first, float end, float clear)
        {
            for (int i = first; i < first + TaperCones; i++)
            {
                _cones[i].gameObject.SetActive(false);
            }
            // Towards the lane traffic is let past in; with none, towards the middle of the road (away from the kerb the entries are on).
            float open = _gateSide != 0f ? _gateSide : -_entry;
            float fromAcross = open > 0f ? _left : _rightEdge;
            float toAcross = open > 0f ? _rightEdge + 0.3f : _left - 0.3f;
            float width = Mathf.Abs(toAcross - fromAcross);
            float run = Mathf.Clamp(width * 1.2f, 4.5f, 9f);
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(width * width + run * run) / ConeSpacing) + 1, 4, TaperCones);
            float nearest = (end < 0f ? _back : _front) + end * Mathf.Max(clear, BarrierOut + 1.2f);
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);   // 0 at the cone traffic meets first, furthest out
                if (Place(nearest + end * run * (1f - t), Mathf.Lerp(fromAcross, toAcross, t), 0.3f, post: false, out Vector3 ground))
                {
                    Transform cone = _cones[first + placed];
                    cone.SetPositionAndRotation(ground, Quaternion.identity);
                    cone.gameObject.SetActive(true);
                    placed++;
                }
            }
            return placed;
        }

        /// <summary>
        /// Broken glass and bits of bumper on the road at the point of impact, there from the
        /// moment of the crash until Open. Only the modelled pieces: without them nothing is laid
        /// (the wreck's own debris is there in any case).
        /// </summary>
        public void Litter(Vector3 impact, Vector3 along)
        {
            if (_failed || !PropLibrary.Ready || !Shapes.Available)
            {
                return;
            }
            try
            {
                if (_scatter != null && !_scatterModelled)
                {
                    UnityEngine.Object.Destroy(_scatter);
                    _scatter = null;
                }
                if (_scatter == null && !BuildScatter())
                {
                    return;
                }
                along.y = 0f;
                along = along.sqrMagnitude < 0.01f ? Vector3.forward : along.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, along);
                for (int i = 0; i < _litter.Length; i++)
                {
                    // Glass where the cars met, plastic thrown a little further each way.
                    Vector3 at = impact + along * (i == 0 ? 0f : i == 1 ? -1.1f : i == 2 ? 0.5f : 1.5f) + right * (i == 0 ? 0f : i == 1 ? 0.7f : i == 2 ? -1.0f : 0.6f);
                    if (StreetSpots.Ground(at, out float ground))
                    {
                        at.y = ground + 0.01f;
                    }
                    _litter[i].SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                }
                for (int i = 0; i < _markers.Length; i++)
                {
                    _markers[i].gameObject.SetActive(false);
                }
                _scatter.SetActive(true);
                HasImpact = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>Where numbered marker 0, 1, ... belongs: beside one of the pieces lying at the point of impact.</summary>
        public Vector3 Evidence(int index)
        {
            if (!HasImpact || _litter == null)
            {
                return _scene;
            }
            Transform piece = _litter[Mathf.Abs(index) % _litter.Length];
            return piece.position + piece.right * 0.55f;
        }

        /// <summary>An officer sets a numbered marker down beside what lies at the point of impact. False if there is none to set.</summary>
        public bool Mark(int index)
        {
            if (!HasImpact || _markers == null || index < 0 || index >= _markers.Length || _markers[index].childCount == 0)
            {
                return false;   // (a marker the bundle does not have has nothing to show)
            }
            Vector3 at = Evidence(index);
            if (StreetSpots.Ground(at, out float ground))
            {
                at.y = ground;
            }
            _markers[index].SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            _markers[index].gameObject.SetActive(true);
            return true;
        }

        // Once: two patches of glass, two of plastic and the four numbered markers, switched off until wanted.
        private bool BuildScatter()
        {
            if (!PropLibrary.TryGet("debris_glass", out _, out _) || !PropLibrary.TryGet("debris_plastic", out _, out _))
            {
                return false;
            }
            _scatterModelled = true;
            _scatter = new GameObject("RealLifeOverhaul Impact");
            Transform root = _scatter.transform;
            root.SetParent(_host, false);
            _scatter.SetActive(false);
            _litter = new Transform[4];
            for (int i = 0; i < _litter.Length; i++)
            {
                Transform piece = new GameObject("Debris").transform;
                piece.SetParent(root, false);
                Shapes.Prop(i == 0 || i == 3 ? "debris_glass" : "debris_plastic", piece);
                _litter[i] = piece;
            }
            _markers = new Transform[Markers];
            for (int i = 0; i < Markers; i++)
            {
                Transform marker = new GameObject("Marker").transform;
                marker.SetParent(root, false);
                Shapes.Prop("evidence_marker_" + (i + 1), marker);
                marker.gameObject.SetActive(false);
                _markers[i] = marker;
            }
            return true;
        }

        // Barriers along one stretch of an end: as many as fit end to end, centred on it, each only where it
        // stands on the street and in no vehicle of any kind.
        private int Barriers(int used, float along, float from, float to, Quaternion facing)
        {
            float run = Mathf.Abs(to - from);
            int count = Mathf.FloorToInt((run + 0.4f) / BarrierWidth);
            float step = to >= from ? BarrierWidth : -BarrierWidth;
            float first = (from + to) * 0.5f - step * (count - 1) * 0.5f;
            if (_parkedMask == 0)
            {
                _parkedMask = LayerMask.GetMask("ParkedVehicles", "PlayerVehicles", "StreetProps");
            }
            for (int i = 0; i < count && used < MaxBarriers; i++)
            {
                if (Place(along, first + step * i, 0.6f, post: false, out Vector3 ground)
                    && !Physics.CheckBox(ground + Vector3.up * 0.6f, new Vector3(BarrierWidth * 0.5f, 0.4f, 0.3f), facing,
                        TrafficTools.VehicleMask | _parkedMask, QueryTriggerInteraction.Ignore))
                {
                    _barriers[used].SetPositionAndRotation(ground, facing);
                    _barriers[used].gameObject.SetActive(true);
                    used++;
                }
            }
            return used;
        }

        // Posts from one point of the rectangle to another, a tape between each and the one before, and one wall
        // along each unbroken run of that tape. 'outward' is -1 or 1 for the near or far end, 0 for a side.
        private void Line(float fromAlong, float fromAcross, float toAlong, float toAcross, bool fresh, float outward)
        {
            float run = Mathf.Abs(toAlong - fromAlong) + Mathf.Abs(toAcross - fromAcross);   // the lines run along or across, never both
            int steps = Mathf.Max(1, Mathf.CeilToInt(run / PostSpacing));
            if (fresh)
            {
                _hasLast = false;
            }
            _segment = -1;   // a new straight: its tape is another wall
            _outward = outward;
            // The post this line starts from is the corner it shares with the line before, if that post went up.
            // If it did not, the first stretch here runs across the corner from further back: that stretch is
            // walled on its own, so that no wall takes a short cut through the working area.
            bool cornered = fresh || (Mathf.Abs(_lastAlong - fromAlong) < 0.1f && Mathf.Abs(_lastAcross - fromAcross) < 0.1f);
            for (int i = fresh ? 0 : 1; i <= steps && _placed < MaxPosts; i++)
            {
                float t = (float)i / steps;
                float along = Mathf.Lerp(fromAlong, toAlong, t);
                float across = Mathf.Lerp(fromAcross, toAcross, t);
                if (!Place(along, across, 0.3f, post: true, out Vector3 ground))
                {
                    continue;   // in a vehicle, off the carriageway or on a kerb: the tape runs on to the next post, if nothing is in its way
                }
                _posts[_placed].SetPositionAndRotation(ground, Quaternion.identity);
                _posts[_placed].gameObject.SetActive(true);
                _placed++;
                _lastAlong = along;
                _lastAcross = across;
                cornered |= !_hasLast;   // nothing to join up with: this post starts the run
                if (_hasLast && !cornered)
                {
                    cornered = true;
                    if (Stretch(_last, ground) && _wallCount < Walls)
                    {
                        _wallFrom[_wallCount] = _last;
                        _wallTo[_wallCount] = ground;
                        _wallOut[_wallCount] = 0f;
                        _wallCount++;
                    }
                    _segment = -1;
                }
                else if (_hasLast && Stretch(_last, ground))
                {
                    if (_segment < 0 && _wallCount < Walls)
                    {
                        _segment = _wallCount++;
                        _wallFrom[_segment] = _last;
                        _wallOut[_segment] = _outward;
                    }
                    if (_segment >= 0)
                    {
                        _wallTo[_segment] = ground;
                    }
                }
                else
                {
                    _segment = -1;
                }
                _last = ground;
                _hasLast = true;
            }
        }

        // Everything a length of tape must not pass through: vehicles of every kind, street furniture, buildings
        // and their steps and railings, and whatever else stands on the default layer. Not the ground, and not people.
        private static int SolidMask
        {
            get
            {
                if (_solidMask == 0)
                {
                    _solidMask = TrafficTools.VehicleMask
                                 | LayerMask.GetMask("Default", "StreetProps", "Buildings", "BuildingWalls", "BuildingsOutlined", "InteractiveItems", "InteractiveItemsOutlined");
                }
                return _solidMask;
            }
        }

        // A length of tape between two posts, if they are near enough and nothing solid stands between them: looked
        // for at the height the tape hangs and again low down, where steps and kerbs are. False if it was left out.
        private bool Stretch(Vector3 from, Vector3 to)
        {
            Vector3 run = to - from;
            float length = run.magnitude;
            Vector3 lift = Vector3.up * _tapeHeight;
            if (_taped >= _tapes.Length || length < 0.3f || length > PostSpacing * 2.3f)
            {
                return false;
            }
            if (HasWay && WayCrossing(from, to, CrewClear - 0.1f, out _, out _))
            {
                _wayRefused++;
                return false;   // across the ambulance crew's way: that stretch stays open
            }
            Vector3 low = Vector3.up * 0.3f;
            if (Physics.Linecast(from + lift, to + lift, out RaycastHit hit, SolidMask, QueryTriggerInteraction.Ignore)
                || Physics.Linecast(from + low, to + low, out hit, SolidMask, QueryTriggerInteraction.Ignore))
            {
                _tapeBlocked++;
                if (_blockedBy == null && hit.collider != null)
                {
                    _blockedBy = hit.collider.name + " on layer " + LayerMask.LayerToName(hit.collider.gameObject.layer);
                }
                return false;
            }
            _tapeFrom[_taped] = from;
            _tapeTo[_taped] = to;
            Transform tape = _tapes[_taped++];
            tape.SetPositionAndRotation((from + to) * 0.5f + lift, Quaternion.LookRotation(run / length, Vector3.up));
            // The modelled strip is made a set length with its lettering to scale: stretched along its run only.
            tape.localScale = _tapeUnit > 0f ? new Vector3(1f, 1f, length / _tapeUnit) : new Vector3(0.012f, 0.075f, length);
            tape.gameObject.SetActive(true);
            return true;
        }

        // The ground at a point of the rectangle, if a prop can stand there: on the carriageway (the road's own
        // navigation mesh right there, and no pavement), and in no vehicle. A post that cannot is tried a little
        // further in across the road, twice; it never leaves its place along it, so the tape stays a straight run.
        private bool Place(float along, float across, float radius, bool post, out Vector3 ground)
        {
            float middleAcross = (_left + _rightEdge) * 0.5f;
            for (int attempt = 0; attempt < (post ? 3 : 1); attempt++)
            {
                Vector3 wanted = Point(along, across);
                // Never on the ambulance crew's way, nor within reach of anything of the crew on it: that place stays empty.
                if (HasWay && WayClearance(wanted) < CrewClear - 0.1f)
                {
                    _wayRefused++;
                    break;
                }
                bool there = StreetSpots.OnRoad(wanted, 1f, out ground);
                if (there)
                {
                    Vector3 off = ground - wanted;
                    off.y = 0f;
                    there = off.sqrMagnitude < 0.3f * 0.3f;
                }
                // The tape run again: the navigation mesh is still cut where its own walls stood a moment ago, so the
                // mesh says "no road" along every line it ran. There the ground itself is asked (the place was road
                // when the tape first went up); without this every post was moved in off its line, onto the crew's way.
                if (!there && _redress && NearOldWall(wanted) && StreetSpots.Ground(wanted, out float height))
                {
                    ground = new Vector3(wanted.x, height, wanted.z);
                    there = true;
                }
                if (there && !Physics.CheckSphere(ground + Vector3.up * 0.6f, radius, TrafficTools.VehicleMask, QueryTriggerInteraction.Ignore)
                    && (_crossing || !NavMesh.SamplePosition(ground, out _, 0.2f, StreetSpots.Pavement)))
                {
                    if (attempt > 0)
                    {
                        _movedPosts++;
                    }
                    return true;
                }
                across = Mathf.MoveTowards(across, middleAcross, 0.5f);
            }
            ground = default;
            return false;
        }

        // True within reach of where a wall of the last dressing stood (its cut in the navigation mesh, and the mesh's own margin round it).
        private bool NearOldWall(Vector3 point)
        {
            for (int i = 0; i < _oldCount; i++)
            {
                Vector3 run = _oldTo[i] - _oldFrom[i];
                run.y = 0f;
                Vector3 offset = point - _oldFrom[i];
                offset.y = 0f;
                float length = run.magnitude;
                float along = length > 0.01f ? Mathf.Clamp(Vector3.Dot(offset, run) / length, 0f, length) : 0f;
                Vector3 nearest = length > 0.01f ? run * (along / length) : Vector3.zero;
                if ((offset - nearest).sqrMagnitude < OldWallReach * OldWallReach)
                {
                    return true;
                }
            }
            return false;
        }

        private const float OldWallReach = BarrierWall + 1f;   // an end's wall is that deep, and the mesh is cut a little wider than the wall

        private static void HideFrom(Transform[] props, int from)
        {
            for (int i = from; i < props.Length; i++)
            {
                props[i].gameObject.SetActive(false);
            }
        }

        // Once: every post, length of tape, cone, barrier and wall there will ever be, switched off until wanted.
        private void Build()
        {
            _modelled = PropLibrary.Ready;
            _props = new GameObject("RealLifeOverhaul Cordon");
            Transform root = _props.transform;
            root.SetParent(_host, false);
            _props.SetActive(false);

            Material orange = Shapes.Paint(new Color(0.95f, 0.33f, 0.04f), 1.5f);
            Material dark = Shapes.Paint(new Color(0.05f, 0.05f, 0.05f));
            Material yellow = Shapes.Paint(new Color(0.98f, 0.82f, 0.05f), 4f);
            Material white = Shapes.Paint(new Color(0.92f, 0.92f, 0.92f), 1.5f);
            Material blue = Shapes.Paint(new Color(0.05f, 0.15f, 0.55f), 2f);
            Mesh cube = Shapes.Cube;

            _posts = new Transform[MaxPosts];
            for (int i = 0; i < MaxPosts; i++)
            {
                Transform post = new GameObject("Post").transform;
                post.SetParent(root, false);
                if (!Shapes.Prop("tape_post", post))
                {
                    Shapes.Piece("Foot", post, cube, dark, new Vector3(0f, 0.03f, 0f), new Vector3(0.32f, 0.06f, 0.32f));
                    Shapes.Piece("Pole", post, Shapes.Drum, white, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, PostHeight - 0.06f, 0.05f));
                    Shapes.Piece("Cap", post, Shapes.Drum, orange, new Vector3(0f, PostHeight - 0.14f, 0f), new Vector3(0.07f, 0.14f, 0.07f));
                }
                post.gameObject.SetActive(false);
                _posts[i] = post;
            }

            // The modelled tape if it is to be had (the one that hangs a little, else the straight one), tied on
            // where the modelled post takes it; else the plain strip at the plain post's height.
            string tapeId = "tape_4m_sag";
            if (!PropLibrary.TryGet(tapeId, out Mesh tapeMesh, out Material tapeMaterial) || tapeMesh == null || tapeMaterial == null)
            {
                tapeId = "tape_4m";
                if (!PropLibrary.TryGet(tapeId, out tapeMesh, out tapeMaterial) || tapeMesh == null || tapeMaterial == null)
                {
                    tapeId = null;
                }
            }
            _tapeUnit = tapeId != null ? PropLibrary.SizeOf(tapeId).z : 0f;
            if (tapeId != null && _tapeUnit < 0.1f)
            {
                _tapeUnit = 4f;   // its size is not known: what it is modelled to
            }
            _tapeHeight = PropLibrary.TryGet("tape_post", out _, out _) ? 0.95f : TapeHeight;
            _tapes = new Transform[MaxPosts];
            for (int i = 0; i < MaxPosts; i++)
            {
                _tapes[i] = tapeId != null ? Shapes.Piece("Tape", root, tapeMesh, tapeMaterial, Vector3.zero, Vector3.one)
                    : Shapes.Piece("Tape", root, cube, yellow, Vector3.zero, Vector3.one);
                _tapes[i].gameObject.SetActive(false);
            }

            _cones = new Transform[MaxCones];
            for (int i = 0; i < MaxCones; i++)
            {
                Transform cone = new GameObject("Cone").transform;
                cone.SetParent(root, false);
                if (!Shapes.Prop("cone", cone))
                {
                    Shapes.Piece("Foot", cone, cube, dark, new Vector3(0f, 0.015f, 0f), new Vector3(0.34f, 0.03f, 0.34f));
                    Shapes.Piece("Cone", cone, Shapes.Post, orange, new Vector3(0f, 0.03f, 0f), new Vector3(0.26f, 0.55f, 0.26f));
                }
                cone.gameObject.SetActive(false);
                _cones[i] = cone;
            }

            _barriers = new Transform[MaxBarriers];
            for (int i = 0; i < MaxBarriers; i++)
            {
                Transform barrier = new GameObject("Barrier").transform;
                barrier.SetParent(root, false);
                if (!Shapes.Prop("barrier", barrier))
                {
                    Shapes.Piece("Board", barrier, cube, white, new Vector3(0f, 0.9f, 0f), new Vector3(1.9f, 0.22f, 0.04f));
                    Shapes.Piece("Stripe", barrier, cube, blue, new Vector3(0f, 0.9f, 0f), new Vector3(1.9f, 0.08f, 0.05f));
                    Shapes.Piece("Leg", barrier, cube, white, new Vector3(-0.8f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.45f));
                    Shapes.Piece("Leg", barrier, cube, white, new Vector3(0.8f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.45f));
                }
                barrier.gameObject.SetActive(false);
                _barriers[i] = barrier;
            }

            // The walls have nothing to draw: each cuts its own strip out of the navigation mesh while it is switched on.
            _walls = new NavMeshObstacle[Walls];
            for (int i = 0; i < Walls; i++)
            {
                var wall = new GameObject("Wall");
                wall.transform.SetParent(root, false);
                wall.SetActive(false);
                NavMeshObstacle obstacle = wall.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                _walls[i] = obstacle;
            }
        }

        // Scene control is an extra. If it faults, the road is opened and there is none for the rest of the session.
        private void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Cordon: switched off for this session after the error above; accidents run without one.");
            try
            {
                Open();
            }
            catch (Exception inner)
            {
                Logger?.Error(inner);
            }
        }
    }
}
