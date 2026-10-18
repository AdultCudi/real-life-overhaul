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
    /// Scene control at an accident, a fire or a street event. Close shuts the road at once through the traffic system's own
    /// switch on its road points (the blocked lanes with an apron for the units, the way in, a gate on a clear lane); Stand
    /// says where a unit stands. Raise, when the police arrive, puts up tape, barriers and walls cut out of the navigation
    /// mesh, and puts whoever is inside outside (People/CordonClear); the cones are a taper. Open undoes it all. Props are
    /// built once; nothing runs per frame but the gate's timer, and a fault opens the road and ends it for the session.
    /// </summary>
    public sealed partial class SceneCordon
    {
        private const float Margin = 3f;          // metres of working room round the wrecks, along the road
        private const float Side = 0.6f;          // ...and across it, from the outermost wreck to the tape
        private const float CrewLane = 2f;        // metres across from a held vehicle to the tape on the crews' side: a metre clear, and room
        private const float Apron = 30f;          // metres of each blocked lane kept empty before the wrecks, for the response vehicles
        private const float LaneClear = 2f;       // metres: a lane whose middle is nearer the wrecks than this is blocked by them
        private const float RoadWidth = 9f;       // metres: lanes further across than this are another carriageway
        private const float KerbReach = 3f;       // metres: a pavement this close beyond the tape makes that the kerb side
        private const float KerbInset = 0.5f;     // metres the tape stays off the kerb, on the carriageway
        private const float EntryGap = 2.4f;      // metres: the opening in each end, facing the ambulance at the near end, at the far end's kerb corner
        private const float WorkRoom = 2.6f;      // metres across: at a junction, no road point this near the wrecks stays open
        private const float BarrierWidth = 2.5f;  // metres of an end one barrier takes up
        private const float ParkedGap = 0.45f;    // metres the tape stays off a parked vehicle
        private const float PostSpacing = 3.5f;   // metres
        private const float PostHeight = 1f;      // metres
        private const float TapeHeight = 0.9f;    // metres
        private const int TaperCones = 6;         // cones a taper may have (each end has its own)
        private const float WallThickness = 0.3f; // metres
        private const float BarrierOut = 0.7f;    // metres outside an end's tape that its barriers stand
        private const float BarrierWall = 1.22f;  // metres: an end's wall covers tape and barriers, from inside the tape to the barriers' far face
        private const float GateOpenFor = 0.6f;   // seconds: long enough for the car at the front to set off
        private const int MaxPosts = 24;
        private const int MaxCones = TaperCones * 2;
        private const int MaxBarriers = 8;
        private const int Walls = 8;
        private const int Bars = 4;               // bars across openings a walkway runs through: cuts in the mesh, nothing to see
        private const int BarPoints = 96;         // points of a line looked at, one every half metre: 47 m (a parade's line is 38 m)
        private const int Markers = 4;            // numbered evidence markers

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
        private NavMeshObstacle[] _bars;
        private int _barCount;       // bars standing across openings a walkway runs through
        private readonly bool[] _walkway = new bool[BarPoints * 4];   // the pedestrians' mesh under each point of each line, as found when the tape first went up
        private int _segment = -1;   // the wall the tape now being run belongs to; -1 to start another
        private float _lastAlong, _lastAcross;   // where on the rectangle the last post was asked for (before any moving in)
        private float _outward;
        private float _carveAt;      // when the walls are looked for in the navigation mesh; 0 when there is nothing to look at
        private int _carveTries;
        private int _wallsChanged;   // walls moved, put up or taken away by the last dressing: what the navigation mesh is cut again for
        private int _tapeBlocked;    // stretches of tape left out because something solid stood between their posts
        private string _blockedBy;
        private int _movedPosts;     // posts that could not stand on their line and were moved in
        private static int _solidMask;
        // The gate held for a response vehicle that is leaving (see HoldGateShut).
        private int _gateHold;       // -1 shut, 0 worked on its timer

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
        private bool _crossing;      // people on foot have a way across the road here (a crossing)
        private float _entry;        // the side the entries are on: -1 left, 1 right

        private int _placed, _taped;
        private Vector3 _last;
        private bool _hasLast;

        private bool _gateOpen;
        private float _gateAt;

        public SceneCordon(Transform host)
        {
            _host = host;
            ScenePrewarm.Add("cordon: its posts, tape, cones, barriers and walls being made", Prewarm);
        }

        private int _prewarmTries;

        // The seventy-odd props are made out of sight (behind the loading screen, or the black screen at a door once the
        // modelled props have loaded), not in the frame the first tape goes up; after a few looks without them, on first need.
        private bool Prewarm()
        {
            if (_failed || (_props != null && _modelled) || !Settings.TrafficAccidents && !Settings.CityFires && !Settings.StreetEvents)
            {
                return true;
            }
            if (Closed || Up)
            {
                return false;   // a scene is using what there is: looked at again the next time
            }
            if (!Shapes.Available || !PropLibrary.Ready)
            {
                return ++_prewarmTries >= 5;
            }
            try
            {
                if (_props != null)
                {
                    UnityEngine.Object.Destroy(_props);   // built from plain shapes before the modelled props had loaded
                    _props = null;
                }
                Build();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                if (_props != null)
                {
                    UnityEngine.Object.Destroy(_props);   // half built: not kept, and not built over next time
                }
                _props = null;
            }
            return true;
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

        /// <summary>Outside the tape by the near entry, on the pavement if there is one: where bystanders are sent to wait.</summary>
        public Vector3 Kerbside { get; private set; }

        /// <summary>A point up the road, where the waiting traffic is.</summary>
        public Vector3 Oncoming { get; private set; }

        /// <summary>A point just outside a corner of the taped area, 0 to 3, for someone walking round it.</summary>
        public Vector3 Corner(int index)
        {
            return _corners[index & 3];
        }

        /// <summary>Closes the road round whatever lies in it; nothing is closed when traffic is not running or there is no road.</summary>
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
                    Logger?.Info($"Cordon: {_closed.Count} road point(s) closed, {_gates.Count} gated lane(s), "
                                 + $"wrecks cover {_alongMax - _alongMin:F0} x {_acrossMax - _acrossMin:F0} m{(_bothWays ? ", both directions blocked" : "")}.");
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
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
                    int dressing = SessionWatch.Mark("cordon: the tape, posts, barriers and walls being put up");
                    Dress(now);
                    SessionWatch.Done(dressing);
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Takes the tape, barriers, cones and walls down but leaves the road closed while the officers pack up.</summary>
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
                MeshCuts.Note("a cordon's walls taken down", _wallCount + _barCount);
                Logger?.Info("Cordon: tape, barriers and cones taken down; the road stays closed until the scene is cleared.");
            }
        }

        private static readonly int HotTick = HotSpots.Slot("Traffic: SceneCordon.Tick");
        /// <summary>Works the gate: true at the moment waiting traffic is waved through. Two comparisons when nothing is due.</summary>
        public bool Tick(float now)
        {
            using var hotSpot = HotSpots.Time(HotTick);
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

        /// <summary>After Open, opens any road point the last scene left closed and says how many (none unless Open was cut short).</summary>
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

        /// <summary>Keeps the gate shut for a unit leaving round the scene by a lane beside the wrecks; ReleaseExit undoes it.</summary>
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

        /// <summary>Holds the gated lane shut while the ambulance crew wheels its stretcher along it (true), and gives it back (false).</summary>
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

        /// <summary>True if a point lies in a lane beside the scene that traffic is let past in, give or take a margin.</summary>
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

        /// <summary>The nearest place outside the taped rectangle by a margin, straight out through its nearest side; the point itself if never measured.</summary>
        public Vector3 KeepOutside(Vector3 point, float margin)
        {
            if (!Surveyed)
            {
                return point;
            }
            Vector3 offset = point - _scene;
            float along = Vector3.Dot(offset, _forward);
            float across = Vector3.Dot(offset, _right);
            CordonRules.Outside(Box, along, across, margin, out float outAlong, out float outAcross);
            return point + _forward * (outAlong - along) + _right * (outAcross - across);
        }

        /// <summary>The rectangle the tape runs round (or will), in the road's frame at the scene (see <see cref="Frame"/>).</summary>
        public CordonBox Box => new CordonBox(_back, _front, _left, _rightEdge);

        private const float WaitOut = 1.2f;   // metres outside the tape a place for somebody who only waits is kept: clear of the wall's cut

        /// <summary>True while the tape is measured and a place is inside or against it: none but the scene's own people go there.</summary>
        public bool Shuts(Vector3 point)
        {
            if (!Surveyed)
            {
                return false;
            }
            Vector3 offset = point - _scene;
            return CordonRules.Inside(Box, Vector3.Dot(offset, _forward), Vector3.Dot(offset, _right));
        }

        /// <summary>Metres a vehicle facing this way still has to go to be through the taped area: negative beyond it, 0 if never measured.</summary>
        public float Through(Vector3 from, Vector3 facing)
        {
            if (!Surveyed)
            {
                return 0f;
            }
            float along = Vector3.Dot(from - _scene, _forward);
            return Vector3.Dot(facing, _forward) >= 0f ? _front - along : along - _back;
        }

        /// <summary>The road's frame at the scene and the taped rectangle's edges in it, in metres from the scene. False if never measured.</summary>
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

        /// <summary>Metres from the ambulance crew's way to the nearest post, cone, barrier or tape, as last measured; -1 with no way or tape.</summary>
        public float CorridorClear { get; private set; } = -1f;

        /// <summary>True if a point is beyond the far end of the taped area by so many metres, the way the road runs: a unit there has passed the scene.</summary>
        public bool Beyond(Vector3 point, float by)
        {
            return Surveyed && Vector3.Dot(point - _scene, _forward) > _front + by;
        }

        /// <summary>Adds how far a standing vehicle reaches beyond an end of the taped area to that end's figure, for LayTaper's cones.</summary>
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

        /// <summary>The nearest place inside the taped rectangle at least the inset from its line; the point itself if never measured.</summary>
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
