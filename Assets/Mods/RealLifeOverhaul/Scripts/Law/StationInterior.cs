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
    /// The inside of a police station. A precinct is an empty unit of the city's, and the game
    /// loads such a unit's bare interior through its own door (walls, floor, the way out, its
    /// camera, light and navigation mesh). This furnishes that room while the player is in it:
    ///   the front desk    a counter across the room a few metres in, facing the door, with the
    ///                     desk sergeant behind it (the one who is talked to: StationDesk)
    ///   the waiting area  a bench along a side wall by the entrance, somebody waiting, and a
    ///                     notice board on the wall opposite
    ///   the back office   one or two desks along the side walls behind the counter, an officer
    ///                     at each writing something up
    ///   the holding cell  a back corner (or the whole back of a narrow room) closed off with
    ///                     barred panels, a bench inside; and a closed evidence-room door in the
    ///                     back wall beside it
    /// Every unit is a different size and shape, so nothing is placed by a plan drawn in
    /// advance: the room is measured from where the player comes in, on the navigation mesh
    /// the game itself walks its customers on (how far it runs straight ahead, how far to
    /// either side at each depth, whether a wall runs straight where something is to stand
    /// against it), each piece needs its whole footprint walkable, and what does not fit is left
    /// out and named in the log. Things that hang on a wall go up only where a ray finds the
    /// wall's real surface. The furniture is cut out of the navigation mesh, so nobody walks
    /// through it. One set of objects, built once, switched off when the player leaves.
    /// </summary>
    internal sealed partial class StationInterior
    {
        private const float Reach = 45f;
        private const float Step = 0.35f;
        private const float Straight = 0.14f;        // a wall is straight if the room is the same width here and a little along it, to within this
        private const float FromWall = 0.04f;

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

        public StationInterior(Transform root, IModLogger logger)
        {
            _root = root;
            _logger = logger;
        }

        /// <summary>True while the room the player is in has been furnished (or at least measured) as a station.</summary>
        public bool Dressed { get; private set; }

        /// <summary>The precinct the player is inside, or null.</summary>
        public Precinct Precinct { get; private set; }

        /// <summary>True if the front counter went in.</summary>
        public bool HasDesk => _deskDepth >= 0f;

        /// <summary>True if a holding cell went in: custody is then served in it, in view.</summary>
        public bool HasCell { get; private set; }

        /// <summary>Where the player stands to be served: in front of the counter, or of the sergeant where there is no counter.</summary>
        public Vector3 ServiceSpot { get; private set; }

        /// <summary>What they face from there.</summary>
        public Vector3 ServiceFace { get; private set; }

        /// <summary>The middle of the cell, and what somebody held in it looks at (the bars).</summary>
        public Vector3 CellSpot { get; private set; }

        public Vector3 CellFace { get; private set; }

        /// <summary>The way out: the place by the unit's own door that the game takes the player out from. Zero if it could not be found.</summary>
        public Vector3 WayOut { get; private set; }

        /// <summary>The desk sergeant, for whoever has them do something (hand a form over).</summary>
        public ActorRole Sergeant => _sergeant;

        // ---------------------------------------------------------------- coming in

        /// <summary>
        /// The player has just been put inside this precinct's unit: the room is measured and furnished now (the screen is
        /// still dark). Safe to call again; it starts from an empty room each time.
        /// </summary>
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
            // Stand-ins built before the prop bundle had loaded give way to the models once it has.
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
            // The way in is the direction the floor runs furthest in, of the door's own and the three square to it.
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
                _logger?.Info($"Law: {precinct.Name}: the room is only {_length:F1} m deep from the door; it is left bare, the front desk answers where you stand.");
                return;
            }

            Build();
            // Everything is measured before anything is switched on: what is switched on is cut out of the mesh that is being measured.
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
            Furnish(desk, bench, cell);   // the game's own furniture round all of it (StationInterior.Furnish.cs)
            _nextPeople = 0f;
            BeginCast(desk);              // its people, seated and at work at once, under the fade (StationInterior.Cast.cs)
            BeginFill();                  // what floor is still bare, a city office's back rooms, a little a frame (StationInterior.Fill.cs)
            _logger?.Info($"Law: {precinct.Name}: inside: room {_length:F1} m deep from the door: {_fitted}.");
        }

        private bool _cellBenchFits;
        private Precinct _triedFor;
        private float _triedAt;
        private int _tries;

        // The floor was not there to be measured (the game may still be putting the room together): the room is left
        // unfurnished for now and whoever called is free to ask again, up to five times in the first seconds of a visit.
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

        // With nothing measured the front desk still answers: a step in from the door.
        private void Fallback(Vector3 from, Vector3 inward)
        {
            inward.y = 0f;
            inward = inward.sqrMagnitude < 0.01f ? Vector3.forward : inward.normalized;
            ServiceSpot = from;
            ServiceFace = from + inward * 3f;
            _sergeantAt = from + inward * 2.2f;
        }

        /// <summary>Once a second while the player is inside: the people of the station are kept at their places.</summary>
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
                CastTick(now);   // the furnished station's people (StationInterior.Cast.cs)
                return;
            }
            if (now < _nextPeople || !Settings.People)
            {
                return;
            }
            _nextPeople = now + 6f;
            // The sergeant is the one who is talked to: a person the pool gives up for nobody. The rest are only there to be seen.
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

        /// <summary>The player has left (or the feature is going): everybody and everything out of the room. Safe at any time, any number of times.</summary>
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

        /// <summary>The cell's bars taken away at once (somebody has to be got out of it and nothing else worked).</summary>
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

        /// <summary>What went into the room the player is in, for the log.</summary>
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

        // ---------------------------------------------------------------- measuring

        private Vector3 At(float depth, float aside)
        {
            return _origin + _in * depth + _right * aside;
        }

        // How far the floor runs from a point in a direction before it ends (at a wall, a pillar, anything that stands on it). -1 if the point is not on the floor.
        private float Run(Vector3 from, Vector3 direction, float most)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 0.45f, _filter) || Mathf.Abs(start.position.y - _floor) > 0.4f)
            {
                return -1f;
            }
            return NavMesh.Raycast(start.position, start.position + direction * most, out NavMeshHit hit, _filter) ? hit.distance : most;
        }


        // True if the whole of a rectangle of floor can be walked on: nothing stands anywhere in it.
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

        // The wall's real surface beyond the floor's edge: a ray at chest height. False if nothing solid and upright is found there.
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

        // ---------------------------------------------------------------- the plan, piece by piece

        // The counter: across the room some way in, in the middle of it where the room is wide, with a way round at one end.
        private bool PlanDesk()
        {
            float half = _deskSize.x * 0.5f;
            float deep = _deskSize.z * 0.5f;
            float depth = Mathf.Clamp(_length * 0.42f, 3.2f, 6.5f);
            if (_length < depth + deep + 1.3f)
            {
                depth = _length - deep - 1.3f;
            }
            _sergeantAt = At(Mathf.Min(_length - 0.5f, 2.8f), 0f);
            ServiceSpot = At(Mathf.Min(_length - 0.5f, 2.8f) - 1.3f, 0f);
            ServiceFace = _sergeantAt;
            if (depth < 2.6f)
            {
                return false;
            }
            float left = Run(At(depth, 0f), -_right, 30f);
            float right = Run(At(depth, 0f), _right, 30f);
            if (left < 0f || right < 0f)
            {
                return false;
            }
            float aside = left + right >= half * 2f + 2.2f ? (right - left) * 0.5f : 0f;
            if (Mathf.Max(left + aside - half, right - aside - half) < 0.9f)
            {
                if (left + right < half * 2f + 0.95f)
                {
                    return false;   // the room is no wider than the counter: nobody could get round it
                }
                aside = right - half - 0.05f;
            }
            Vector3 middle = At(depth, aside);
            if (!Free(middle, _right, half + 0.05f, _in, deep + 0.05f) || !Free(At(depth + deep + 0.5f, aside), _right, 0.3f, _in, 0.3f)
                || !Free(At(depth - deep - 0.75f, aside), _right, 0.3f, _in, 0.3f))
            {
                return false;
            }
            _deskDepth = depth;
            _deskAside = aside;
            middle.y = _floor;
            _desk.SetPositionAndRotation(middle, Quaternion.LookRotation(-_in, Vector3.up));
            if (_deskName != null)
            {
                _deskName.text = Precinct.Name.ToUpperInvariant();
            }
            _sergeantAt = At(depth + deep + 0.5f, aside);
            ServiceSpot = At(depth - deep - 0.75f, aside);
            ServiceFace = _sergeantAt;
            return true;
        }

        // A bench along a side wall by the entrance, its back to the wall, with somebody waiting in front of it.
        private bool PlanWaiting(out int benchSide)
        {
            benchSide = 0;
            const float depth = 2.3f;
            float half = _benchSize.x * 0.5f;
            float deep = _benchSize.z;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? 1 : -1;
                if (!SideWall(depth, side, half, out float edge) || edge < 1.5f)
                {
                    continue;
                }
                // Clear of the counter if that runs up to this wall.
                if (HasDesk && depth + half + 0.7f > _deskDepth - _deskSize.z * 0.5f && side * _deskAside + _deskSize.x * 0.5f > edge - deep - 0.6f)
                {
                    continue;
                }
                if (!Free(At(depth, side * (edge - 0.45f)), _in, half, _right, 0.4f))
                {
                    continue;
                }
                Vector3 at = At(depth, side * (edge + _radius - deep * 0.5f - FromWall));
                at.y = _floor;
                _bench.SetPositionAndRotation(at, Quaternion.LookRotation(_right * -side, Vector3.up));
                _visitorAt = At(depth + 0.2f, side * (edge - 0.7f));
                _visitorFace = HasDesk ? At(_deskDepth, _deskAside) : _visitorAt + _right * -side * 4f;
                _hasVisitor = true;
                benchSide = side;
                return true;
            }
            return false;
        }

        // The holding cell at the back: a corner closed with two barred panels, or the whole back of a room no wider than one panel.
        private bool PlanCell(out int cornerSide)
        {
            cornerSide = 0;
            _cellBenchFits = false;
            float panel = Mathf.Clamp(_barsSize.x, 2.2f, 3.8f);
            float back = _length + _radius;                       // the back wall's surface, as near as the floor's edge tells
            float served = HasDesk ? _deskDepth + _deskSize.z * 0.5f : 2.8f;
            float deep = Mathf.Min(panel, back - (served + 2.3f));
            if (deep < 2.3f)
            {
                _fitted.Append("cell: the room is not deep enough behind the desk; ");
                return false;
            }
            float left = Run(At(_length - 0.5f, 0f), -_right, 30f);
            float right = Run(At(_length - 0.5f, 0f), _right, 30f);
            if (left < 0f || right < 0f)
            {
                return false;
            }
            float wide = left + right + _radius * 2f;
            if (Mathf.Abs(wide - panel) <= 0.35f)
            {
                // The whole back of the room: one panel from wall to wall (made a little wider or narrower to meet them).
                float aside = (right - left) * 0.5f;
                if (!SideWall(_length - deep * 0.5f, -1, deep * 0.4f, out float a) || !SideWall(_length - deep * 0.5f, 1, deep * 0.4f, out float b)
                    || Mathf.Abs(a - left) > Straight || Mathf.Abs(b - right) > Straight
                    || !Free(At(back - deep * 0.5f, aside), _right, wide * 0.5f - _radius - 0.08f, _in, deep * 0.5f - _radius - 0.08f))
                {
                    _fitted.Append("cell: the back of the room is not a plain alcove; ");
                    return false;
                }
                PutBars(_barsFront, At(back - deep, aside), -_in, wide / _barsSize.x);
                SetCell(back, deep, aside);
                _cellPanel = wide;
                _cellSide = 0;
                return true;
            }
            if (wide < panel + 0.8f)
            {
                _fitted.Append($"cell: the back of the room is {wide:F1} m wide, neither one panel nor room for a corner; ");
                return false;
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? -1 : 1;
                float edge = side < 0 ? left : right;
                float wall = edge + _radius;                      // the side wall's surface, measured from the way in
                float aside = side * (wall - panel * 0.5f);       // the middle of the cell
                // The side wall straight for the cell's depth, the back wall straight for its width, and the whole of it floor.
                if (!SideWall(_length - deep * 0.5f, side, deep * 0.4f, out float mid) || Mathf.Abs(mid - edge) > Straight)
                {
                    continue;
                }
                bool square = true;
                for (int probe = 0; probe < 3 && square; probe++)
                {
                    float run = Run(At(_length - 1.2f, side * (edge - 0.25f - probe * (panel - 0.7f) * 0.5f)), _in, 6f);
                    square = run >= 0f && Mathf.Abs(run - 1.2f) < Straight;
                }
                if (!square || !Free(At(back - deep * 0.5f, aside), _right, panel * 0.5f - _radius - 0.08f, _in, deep * 0.5f - _radius - 0.08f))
                {
                    continue;
                }
                PutBars(_barsFront, At(back - deep, aside), -_in, panel / _barsSize.x);
                PutBars(_barsSide, At(back - deep * 0.5f, side * (wall - panel)), _right * -side, deep / _barsSize.x);
                SetCell(back, deep, aside);
                _cellPanel = panel;
                _cellSide = side;
                cornerSide = side;
                PlanEvidence(back, side * (wall - panel), -side, side < 0 ? right : left);
                return true;
            }
            _fitted.Append("cell: neither back corner is square and clear; ");
            return false;
        }

        private void PutBars(Transform bars, Vector3 at, Vector3 facing, float stretch)
        {
            at.y = _floor;
            bars.SetPositionAndRotation(at, Quaternion.LookRotation(facing, Vector3.up));
            bars.localScale = new Vector3(stretch, 1f, 1f);
        }

        private void SetCell(float back, float deep, float aside)
        {
            _cellBack = back;
            _cellDeep = deep;
            _cellAside = aside;
            Vector3 middle = At(back - deep * 0.5f, aside);
            CellSpot = NavMesh.SamplePosition(middle, out NavMeshHit hit, 0.6f, _filter) ? hit.position : middle;
            CellFace = CellSpot - _in * 4f;
            // A bench against the back wall, if what is left in front of it is still room to stand.
            if (deep - _benchSize.z >= 1.5f)
            {
                Vector3 at = At(back - _benchSize.z * 0.5f - FromWall, aside);
                at.y = _floor;
                _cellBench.SetPositionAndRotation(at, Quaternion.LookRotation(-_in, Vector3.up));
                _cellBenchFits = true;
                CellSpot = NavMesh.SamplePosition(At(back - _benchSize.z - 0.75f, aside), out NavMeshHit front, 0.5f, _filter) ? front.position : CellSpot;
            }
        }

        // A closed door in the back wall beside the cell, where the wall's real surface is found and there is the width for it.
        private void PlanEvidence(float back, float from, int toward, float edge)
        {
            float to = toward * (edge + _radius);
            if (Mathf.Abs(to - from) < 1.7f)
            {
                return;
            }
            float aside = (from + to) * 0.5f;
            float run = Run(At(_length - 0.6f, aside), _in, 4f);
            if (run < 0f || Mathf.Abs(run - 0.6f) > Straight)
            {
                return;
            }
            if (!Surface(At(_length, aside), _in, out Vector3 surface))
            {
                _fitted.Append("evidence door: the back wall's surface was not found; ");
                return;
            }
            _evidence.SetPositionAndRotation(surface - _in * 0.004f, Quaternion.LookRotation(-_in, Vector3.up));
            Show(_evidence, true);
            _fitted.Append("evidence-room door; ");
        }

        // One or two desks along the side walls between the counter and the cell, an officer standing at each.
        private void PlanOffice(bool cell)
        {
            _staffed[0] = false;
            _staffed[1] = false;
            if (!HasDesk)
            {
                return;
            }
            float from = _deskDepth + _deskSize.z * 0.5f + 1.7f;
            float to = cell ? Vector3.Dot(_barsFront.position - _origin, _in) - 1.4f : _length - 1.0f;
            if (to - from < 1.5f)
            {
                return;
            }
            float depth = (from + to) * 0.5f;
            int placed = 0;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? 1 : -1;
                if (!SideWall(depth, side, 0.7f, out float edge) || edge < 1.6f)
                {
                    continue;
                }
                // Not where the sergeant stands, and room for whoever works at it.
                if (side * _deskAside + 0.6f > edge - 1.6f && depth < _deskDepth + _deskSize.z * 0.5f + 2.2f)
                {
                    continue;
                }
                Vector3 stand = At(depth, side * (edge + _radius - FromWall - 0.70f - 0.50f));
                if (!Free(stand, _in, 0.7f, _right, 0.3f))
                {
                    continue;
                }
                Vector3 at = At(depth, side * (edge + _radius - FromWall - 0.35f));
                at.y = _floor;
                _desks[placed].SetPositionAndRotation(at, Quaternion.LookRotation(_right * -side, Vector3.up));
                _staffAt[placed] = stand;
                _staffFace[placed] = at;
                _staffed[placed] = true;
                placed++;
            }
            if (placed > 0)
            {
                _fitted.Append(placed).Append(placed == 1 ? " office desk; " : " office desks; ");
            }
        }

        // The notice board: on a side wall by the entrance, at eye height, only where the wall's real surface is found.
        private void PlanBoard(int preferred)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? preferred : -preferred;
                for (int place = 0; place < 2; place++)
                {
                    float depth = place == 0 ? 2.3f : 4.4f;
                    if (depth > _length - 1f || !SideWall(depth, side, 0.7f, out float edge) || !Surface(At(depth, side * edge), _right * side, out Vector3 surface))
                    {
                        continue;
                    }
                    // Not behind the bench, and not behind an officer's desk.
                    if (_hasVisitor && Mathf.Abs(Vector3.Dot(_bench.position - surface, _in)) < 1.7f && CitySpots.Flat(_bench.position - surface) < 2.2f * 2.2f)
                    {
                        continue;
                    }
                    bool taken = false;
                    for (int i = 0; i < _desks.Length; i++)
                    {
                        taken |= _staffed[i] && CitySpots.Flat(_desks[i].position - surface) < 1.9f * 1.9f;
                    }
                    if (taken || (HasDesk && CitySpots.Flat(_desk.position - surface) < 1.6f * 1.6f))
                    {
                        continue;
                    }
                    _board.SetPositionAndRotation(surface + Vector3.up * 1.55f - _right * side * 0.004f, Quaternion.LookRotation(_right * -side, Vector3.up));
                    Show(_board, true);
                    _fitted.Append("notice board; ");
                    return;
                }
            }
            _fitted.Append("notice board: no side wall whose surface was found; ");
        }

        // ---------------------------------------------------------------- the pieces

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
            // The precinct's name across the public face of the counter. The modelled desk (precinct_desk) already carries
            // POLICE on its fascia, 1.60 x 0.40 centred 0.60 m up, on a blue panel from 0.28 to 0.92 whose face is 0.05 m
            // behind the shelf's front edge: there the name goes in the plain strip under that word, on the panel itself
            // (it was lettered across the word, standing 6 cm proud of it). The stand-in has no word: across its middle.
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
                    // (in a holder of its own: the game's own desk takes its place in a furnished station)
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
