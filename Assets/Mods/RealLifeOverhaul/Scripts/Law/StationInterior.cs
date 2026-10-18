using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The inside of a police station: the empty unit's bare interior, which the game loads through its own door, furnished
    /// while the player is in it. The room is measured from the door on the game's navigation mesh, and a piece goes in only
    /// where its whole footprint can be walked on. Cost: one pass on the way in, under the fade; then a tick a second.
    /// </summary>
    internal sealed partial class StationInterior
    {
        private const float Reach = 45f;             // metres the floor is measured out to
        private const float Step = 0.35f;            // metres between the floor samples of a footprint
        private const float Straight = 0.14f;        // metres: a wall is straight if the room's width agrees to within this
        private const float FromWall = 0.04f;        // metres between a piece's back and the wall

        private readonly IModLogger _logger;
        private readonly Transform _root;
        private readonly ActorRole _sergeant = new ActorRole();
        private readonly ActorRole[] _staff = { new ActorRole(), new ActorRole() };
        private readonly ActorRole _visitor = new ActorRole();
        private readonly Transform[] _desks = new Transform[2];
        private readonly Vector3[] _staffAt = new Vector3[2];
        private readonly Vector3[] _staffFace = new Vector3[2];
        private readonly bool[] _staffed = new bool[2];
        private readonly StringBuilder _fitted = new StringBuilder(256);

        private Transform _holder;
        private Transform _desk;
        private TextMeshPro _deskName;
        private Vector3 _deskSize;
        private Transform _bench;
        private Vector3 _benchSize;
        private Transform _cellBench;
        private Transform _board;
        private Transform _barsFront;
        private Transform _barsSide;
        private Vector3 _barsSize;
        private Transform _evidence;
        private bool _built;
        private bool _builtModelled;

        private NavMeshQueryFilter _filter;
        private float _radius = 0.3f;
        private Vector3 _origin;         // where the player came in, on the floor
        private Vector3 _in;             // into the room
        private Vector3 _right;          // to the right of somebody walking in
        private float _floor;
        private float _length;           // how far the floor runs straight in from the door
        private float _deskDepth = -1f;  // how far in the counter's middle is; -1 if there is none
        private float _deskAside;
        private Vector3 _sergeantAt;
        private Vector3 _visitorAt;
        private Vector3 _visitorFace;
        private bool _hasVisitor;
        private float _nextPeople;
        private BuildingManager _building;
        private bool _cellBenchFits;
        private Precinct _triedFor;
        private float _triedAt;
        private int _tries;

        public StationInterior(Transform root, IModLogger logger)
        {
            _root = root;
            _logger = logger;
        }

        /// <summary>True while the room the player is in is furnished (or at least measured) as a station.</summary>
        public bool Dressed { get; private set; }

        public Precinct Precinct { get; private set; }

        public bool HasDesk => _deskDepth >= 0f;

        /// <summary>True if a holding cell went in: custody is then served in it, in view.</summary>
        public bool HasCell { get; private set; }

        /// <summary>Where the player stands to be served: before the counter, or before the sergeant where there is none.</summary>
        public Vector3 ServiceSpot { get; private set; }

        /// <summary>What they face from there.</summary>
        public Vector3 ServiceFace { get; private set; }

        /// <summary>The middle of the cell, and what somebody held in it looks at (the bars).</summary>
        public Vector3 CellSpot { get; private set; }

        public Vector3 CellFace { get; private set; }

        /// <summary>The place by the unit's own door the game takes the player out from; zero if it was not found.</summary>
        public Vector3 WayOut { get; private set; }

        public ActorRole Sergeant => _sergeant;

        /// <summary>The player has just been put inside: the room is measured and furnished now, under the dark screen. Safe again.</summary>
        public void Begin(Precinct precinct)
        {
            End();
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            _building = InstanceBehavior<BuildingManager>.Instance;
            if (precinct == null || player == null || _building == null || !Shapes.Available)
            {
                return;
            }
            Precinct = precinct;
            Dressed = true;
            _fitted.Length = 0;
            _filter = NavMeshHelper.NpcNavMeshFilter;
            try
            {
                _radius = Mathf.Clamp(NavMesh.GetSettingsByID(_filter.agentTypeID).agentRadius, 0.15f, 0.6f);
            }
            catch (Exception)
            {
                _radius = 0.3f;
            }
            // Stand-ins built before the prop bundle loaded give way to the models.
            if (_built && !_builtModelled && PropLibrary.Ready)
            {
                DestroyPieces();
            }

            Vector3 at = player.transform.position;
            Vector3 inward = player.transform.forward;
            ExitZone entry = null;
            float best = 36f;
            List<ExitZone> exits = _building.exitZones;
            for (int i = 0; exits != null && i < exits.Count; i++)
            {
                ExitZone exit = exits[i];
                if (exit == null || exit.playerSpawnPoint == null || exit.isDriveInBay)
                {
                    continue;
                }
                float sqr = (exit.playerSpawnPoint.position - at).sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    entry = exit;
                }
            }
            if (entry != null)
            {
                at = entry.playerSpawnPoint.position;
                inward = entry.playerSpawnPoint.forward;
                WayOut = entry.despawner != null ? entry.despawner.transform.position : Vector3.zero;
            }
            inward.y = 0f;
            if (!NavMesh.SamplePosition(at, out NavMeshHit start, 1.5f, _filter) || inward.sqrMagnitude < 0.01f)
            {
                if (TryLater(precinct))
                {
                    return;
                }
                Fallback(player.transform.position, player.transform.forward);
                _logger?.Info("Law: " + precinct.Name + ": the room could not be measured from the door (no floor to stand on there); it is left bare, the front desk answers where you stand.");
                return;
            }
            _origin = start.position;
            _floor = _origin.y;
            _in = inward.normalized;
            // The way in is the direction the floor runs furthest in: the door's own or one of the three square to it.
            _length = Run(_origin, _in, Reach);
            if (_length < 3f)
            {
                Vector3 turned = _in;
                for (int quarter = 1; quarter < 4; quarter++)
                {
                    turned = Vector3.Cross(Vector3.up, turned);
                    float run = Run(_origin, turned, Reach);
                    if (run > _length)
                    {
                        _length = run;
                        _in = turned;
                    }
                }
            }
            _right = Vector3.Cross(Vector3.up, _in);
            Fallback(_origin, _in);
            if (_length < 3.4f)
            {
                if (TryLater(precinct))
                {
                    return;
                }
                _logger?.Info(FormattableString.Invariant($"Law: {precinct.Name}: the room is only {_length:F1} m deep from the door; it is left bare, the front desk answers where you stand."));
                return;
            }

            Build();
            // Everything is measured before anything is switched on: what is switched on is cut out of the mesh being measured.
            bool desk = PlanDesk();
            bool bench = PlanWaiting(out int benchSide);
            _benchSide = bench ? benchSide : 0;
            bool cell = PlanCell(out int cellSide);
            PlanOffice(cell);
            PlanBoard(bench ? -benchSide : 1);
            _fitted.Append(desk ? "front desk" : "no front desk (the sergeant stands)").Append(bench ? ", waiting bench" : ", no waiting bench")
                .Append(cell ? ", holding cell" : ", no holding cell (custody is the panel)");
            if (_desk != null && desk)
            {
                _desk.gameObject.SetActive(true);
            }
            Show(_bench, bench);
            Show(_barsFront, cell);
            Show(_barsSide, cell && cellSide != 0);
            Show(_cellBench, cell && _cellBenchFits);
            HasCell = cell;
            for (int i = 0; i < _desks.Length; i++)
            {
                Show(_desks[i], _staffed[i]);
            }
            Furnish(desk, bench, cell);
            _nextPeople = 0f;
            BeginCast();
            BeginFill();
            _logger?.Info(FormattableString.Invariant($"Law: {precinct.Name}: inside: room {_length:F1} m deep from the door: {_fitted}."));
        }

        // The floor was not there to be measured (the game may still be building the room): the room is left bare for now
        // and the caller may ask again, up to five times in the first seconds of a visit.
        private bool TryLater(Precinct precinct)
        {
            if (_triedFor != precinct || Time.unscaledTime - _triedAt > 20f)
            {
                _triedFor = precinct;
                _tries = 0;
            }
            _triedAt = Time.unscaledTime;
            if (++_tries > 5)
            {
                return false;
            }
            Dressed = false;
            Precinct = null;
            return true;
        }

        // With nothing measured the front desk still answers, a step in from the door.
        private void Fallback(Vector3 from, Vector3 inward)
        {
            inward.y = 0f;
            inward = inward.sqrMagnitude < 0.01f ? Vector3.forward : inward.normalized;
            ServiceSpot = from;
            ServiceFace = from + inward * 3f;
            _sergeantAt = from + inward * 2.2f;
        }

        /// <summary>Once a second while the player is inside: the station's people kept at their places.</summary>
        public void Tick(float now)
        {
            if (!Dressed)
            {
                return;
            }
            TickFill(now);
            _sergeant.Tick(now);
            _visitor.Tick(now);
            for (int i = 0; i < _staff.Length; i++)
            {
                _staff[i].Tick(now);
            }
            if (_cast)
            {
                CastTick(now);
                return;
            }
            if (now < _nextPeople || !Settings.People)
            {
                return;
            }
            _nextPeople = now + 6f;
            // The sergeant is talked to: a person the pool gives up for nobody. The rest are there to be seen.
            if (!_sergeant.Present && Standable(_sergeantAt, out Vector3 post))
            {
                if (_sergeant.Enter(post, ActorLook.Officer, 0.5f, onRoad: false))
                {
                    _sergeant.Stand(post - _in * 6f);
                    if (HasDesk && ClipLibrary.Has(SceneClips.Clipboard))
                    {
                        _sergeant.Work(SceneClips.Clipboard);
                    }
                }
                return;   // one new character per look
            }
            for (int i = 0; i < _staff.Length; i++)
            {
                if (_staffed[i] && !_staff[i].Present && Standable(_staffAt[i], out Vector3 place))
                {
                    if (_staff[i].EnterPresence(place, ActorLook.Officer, 0.4f, onRoad: false))
                    {
                        _staff[i].Stand(_staffFace[i]);
                        if (ClipLibrary.Has(SceneClips.TakeNotes))
                        {
                            _staff[i].Work(SceneClips.TakeNotes);
                        }
                    }
                    return;
                }
            }
            if (_hasVisitor && !_visitor.Present && Standable(_visitorAt, out Vector3 waiting))
            {
                if (_visitor.EnterPresence(waiting, ActorLook.Civilian, 0.4f, onRoad: false))
                {
                    _visitor.Stand(_visitorFace);
                    if (ClipLibrary.Has("queue_wait"))
                    {
                        _visitor.Work("queue_wait");
                    }
                }
            }
        }

        /// <summary>The player has left or the feature is going: everybody and everything out of the room. Safe at any time.</summary>
        public void End()
        {
            try
            {
                _sergeant.Exit();
                _visitor.Exit();
                for (int i = 0; i < _staff.Length; i++)
                {
                    _staff[i].Exit();
                }
                Show(_desk, false);
                Show(_bench, false);
                Show(_cellBench, false);
                Show(_board, false);
                Show(_barsFront, false);
                Show(_barsSide, false);
                Show(_evidence, false);
                for (int i = 0; i < _desks.Length; i++)
                {
                    Show(_desks[i], false);
                }
                EndCast();
                EndFill();
                HideFurniture();
                RestoreLook();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Dressed = false;
            HasCell = false;
            Precinct = null;
            _cast = false;
            _deskDepth = -1f;
            _hasVisitor = false;
            _cellBenchFits = false;
            _staffed[0] = false;
            _staffed[1] = false;
            WayOut = Vector3.zero;
        }

        /// <summary>The cell's bars taken away at once: somebody has to be got out and nothing else worked.</summary>
        public void OpenCell()
        {
            Show(_barsFront, false);
            Show(_barsSide, false);
            HasCell = false;
        }

        public void Dispose()
        {
            End();
            DestroyPieces();
            DestroyFurniture();
        }

        public string Report()
        {
            return Dressed && Precinct != null ? Precinct.Name + ", inside: " + _fitted : "not inside a station";
        }

        private static void Show(Transform piece, bool on)
        {
            if (piece != null && piece.gameObject.activeSelf != on)
            {
                piece.gameObject.SetActive(on);
            }
        }

        private Vector3 At(float depth, float aside)
        {
            return _origin + _in * depth + _right * aside;
        }

        // How far the floor runs from a point in a direction before it ends at anything. -1 if the point is not on the floor.
        private float Run(Vector3 from, Vector3 direction, float most)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 0.45f, _filter) || Mathf.Abs(start.position.y - _floor) > 0.4f)
            {
                return -1f;
            }
            return NavMesh.Raycast(start.position, start.position + direction * most, out NavMeshHit hit, _filter) ? hit.distance : most;
        }

        private bool Free(Vector3 centre, Vector3 alongA, float halfA, Vector3 alongB, float halfB)
        {
            int stepsA = Mathf.Max(1, Mathf.CeilToInt(halfA * 2f / Step));
            int stepsB = Mathf.Max(1, Mathf.CeilToInt(halfB * 2f / Step));
            for (int a = 0; a <= stepsA; a++)
            {
                for (int b = 0; b <= stepsB; b++)
                {
                    Vector3 point = centre + alongA * (-halfA + 2f * halfA * a / stepsA) + alongB * (-halfB + 2f * halfB * b / stepsB);
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.3f, _filter) || Mathf.Abs(hit.position.y - _floor) > 0.25f
                        || CitySpots.Flat(hit.position - point) > 0.09f * 0.09f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool Standable(Vector3 near, out Vector3 spot)
        {
            bool found = NavMesh.SamplePosition(near, out NavMeshHit hit, 0.8f, _filter) && Mathf.Abs(hit.position.y - _floor) < 0.4f;
            spot = hit.position;
            return found;
        }

        // The room's edge to one side at a depth, and whether the wall runs straight there for a stretch either way.
        private bool SideWall(float depth, int side, float stretch, out float edge)
        {
            edge = Run(At(depth, 0f), _right * side, 30f);
            if (edge < 0f || edge >= 29f)
            {
                return false;
            }
            float before = Run(At(depth - stretch, 0f), _right * side, 30f);
            float after = Run(At(depth + stretch, 0f), _right * side, 30f);
            return before >= 0f && after >= 0f && Mathf.Abs(before - edge) < Straight && Mathf.Abs(after - edge) < Straight;
        }

        // The wall's real surface beyond the floor's edge, by a ray at chest height. False if nothing upright is found.
        private bool Surface(Vector3 atEdge, Vector3 towardWall, out Vector3 surface)
        {
            Vector3 from = atEdge - towardWall * 0.3f + Vector3.up * 1.3f;
            if (Physics.Raycast(from, towardWall, out RaycastHit hit, _radius + 1.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(hit.normal, towardWall) < -0.88f)
            {
                surface = new Vector3(hit.point.x, _floor, hit.point.z);
                return true;
            }
            surface = default;
            return false;
        }

        private void Build()
        {
            if (_built)
            {
                return;
            }
            _built = true;
            _builtModelled = PropLibrary.Ready;
            var holder = new GameObject("RealLifeOverhaul Station");
            _holder = holder.transform;
            _holder.SetParent(_root, false);

            _desk = StationProps.Holder("Front Desk", _holder, StationProps.Desk, Pivot.Floor, new Vector3(2.4f, 1.1f, 0.8f), out _deskSize, out bool modelled);
            if (!modelled)
            {
                StationProps.DeskStandIn(_desk);
            }
            StationProps.Solid(_desk, new Vector3(0f, 0.55f, 0f), new Vector3(_deskSize.x, 1.1f, _deskSize.z));
            // The precinct's name across the counter's face: in the plain strip under the modelled desk's POLICE (0.60 m up,
            // its face 0.05 m behind the shelf's edge), or across the stand-in's middle.
            _deskName = modelled
                ? StationProps.Letter(_desk, string.Empty, new Vector3(0f, 0.34f, _deskSize.z * 0.5f - 0.05f + 0.004f), 1.50f, 0.09f)
                : StationProps.Letter(_desk, string.Empty, new Vector3(0f, Mathf.Min(0.55f, _deskSize.y * 0.5f), _deskSize.z * 0.5f + 0.012f), _deskSize.x * 0.8f, 0.14f);

            _bench = MakeBench("Waiting Bench");
            _cellBench = MakeBench("Cell Bench");

            _board = StationProps.Holder("Notice Board", _holder, StationProps.NoticeBoard, Pivot.Wall, new Vector3(1.2f, 0.9f, 0.05f), out _, out modelled);
            if (!modelled)
            {
                StationProps.NoticeBoardStandIn(_board);
            }

            _barsFront = MakeBars("Cell Front");
            _barsSide = MakeBars("Cell Side");

            for (int i = 0; i < _desks.Length; i++)
            {
                _desks[i] = StationProps.Holder("Office Desk " + (i + 1), _holder, StationProps.OfficeDesk, Pivot.Floor, new Vector3(1.4f, 0.75f, 0.7f), out Vector3 size, out modelled);
                if (!modelled)
                {
                    // In a holder of its own: the game's own desk takes its place in a furnished station.
                    var standIn = new GameObject(StandInName).transform;
                    standIn.SetParent(_desks[i], false);
                    StationProps.OfficeDeskStandIn(standIn);
                }
                StationProps.Solid(_desks[i], new Vector3(0f, 0.4f, 0f), new Vector3(Mathf.Min(size.x, 1.6f), 0.8f, Mathf.Min(size.z, 0.8f)));
            }

            _evidence = StationProps.Holder("Evidence Room", _holder, StationProps.EvidenceDoor, Pivot.Wall, new Vector3(1.04f, 2.12f, 0.05f), out Vector3 doorSize, out modelled);
            if (!modelled)
            {
                StationProps.EvidenceDoorStandIn(_evidence);
                StationProps.Letter(_evidence, "reallife_law_station_evidence".Tr().ToUpperInvariant(), new Vector3(0f, 1.62f, 0.052f), 0.56f, 0.09f);
            }
            else
            {
                // A fitted model hangs from the middle of its back face: stood on the floor instead.
                Transform model = _evidence.childCount > 0 ? _evidence.GetChild(0) : null;
                if (model != null)
                {
                    model.localPosition += Vector3.up * (doorSize.y * 0.5f);
                }
            }
        }

        private Transform MakeBench(string name)
        {
            Transform bench = StationProps.Holder(name, _holder, StationProps.Bench, Pivot.Floor, new Vector3(1.8f, 0.85f, 0.5f), out _benchSize, out bool modelled);
            if (!modelled)
            {
                StationProps.BenchStandIn(bench);
            }
            StationProps.Solid(bench, new Vector3(0f, 0.45f, 0f), new Vector3(_benchSize.x, 0.9f, _benchSize.z));
            return bench;
        }

        private Transform MakeBars(string name)
        {
            Transform bars = StationProps.Holder(name, _holder, StationProps.Bars, Pivot.Floor, new Vector3(3.0f, 2.4f, 0.06f), out _barsSize, out bool modelled);
            if (!modelled)
            {
                StationProps.BarsStandIn(bars);
            }
            StationProps.Solid(bars, new Vector3(0f, 1.2f, 0f), new Vector3(_barsSize.x, 2.4f, 0.16f));
            return bars;
        }

        private void DestroyPieces()
        {
            if (_holder != null)
            {
                UnityEngine.Object.Destroy(_holder.gameObject);
            }
            _holder = null;
            _desk = null;
            _deskName = null;
            _bench = null;
            _cellBench = null;
            _board = null;
            _barsFront = null;
            _barsSide = null;
            _evidence = null;
            _desks[0] = null;
            _desks[1] = null;
            _built = false;
            _builtModelled = false;
        }
    }
}
