using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    /// <summary>The furnished station's rooms, staged with the game's design until the walkway check has kept every way open.</summary>
    internal sealed partial class StationInterior
    {
        private const string StandInName = "Stand-in";
        private const float GridCell = 0.3f;

        private enum SeatUse { None, Desk, Break, Waiting, Briefing }

        // A piece placed but not yet switched on.
        private struct Staged
        {
            public Transform Piece;
            public Bounds Size;
            public Vector3 At;
            public Quaternion Facing;
            public bool Solid;
            public bool Floor;        // it stands on the floor, in the way of anybody walking
            public FloorRect Rect;
            public int Anchor;        // the piece it stands on or at (a computer on a desk, a chair at a table), or -1
            public SeatUse Use;
            public bool Dropped;
        }

        // A seat of the furnished station, where somebody stands to sit on it, and what it is for.
        private struct SeatPlace
        {
            public Transform Seat;
            public SeatUse Use;
            public Vector3 Stand;
            public bool Held;         // somebody of the cast has it
        }

        private readonly List<Staged> _staged = new List<Staged>(128);
        private readonly List<FloorRect> _fixedRects = new List<FloorRect>(32);
        private readonly List<Vector3> _hung = new List<Vector3>(32);
        private readonly List<SeatPlace> _seats = new List<SeatPlace>(24);
        private readonly List<Transform> _seatScratch = new List<Transform>(8);
        private readonly List<FloorRect> _blocks = new List<FloorRect>(160);
        private readonly List<bool> _blockKeep = new List<bool>(160);
        private readonly List<bool> _blockFixed = new List<bool>(160);
        private readonly List<int> _blockStaged = new List<int>(160);
        private readonly List<float> _targetA = new List<float>(32);
        private readonly List<float> _targetD = new List<float>(32);

        private StationGrid _grid;
        private int _benchSide;
        private bool _cast;               // the furnished station's people are in charge (Cast)
        private bool _designLamps;
        private bool _designCarpet;
        private Transform _lastWall;
        private SeatUse _stageWall;
        private bool _hasCoffee;
        private Vector3 _coffeeFront;
        private Vector3 _lastCoffeeFace;  // from where somebody stands at the coffee machine towards it
        private bool _hasBriefing;
        private Vector3 _briefingFront;   // where somebody stands to speak to the briefing row
        private Vector3 _briefingScreen;

        private void ClearRooms()
        {
            _staged.Clear();
            _fixedRects.Clear();
            _hung.Clear();
            _seats.Clear();
            _grid = null;
            _cast = false;
            _designLamps = false;
            _designCarpet = false;
            _lastWall = null;
            _stageWall = SeatUse.None;
            _hasCoffee = false;
            _hasBriefing = false;
            _designName = null;
        }

        // The office desks' plain stand-ins shown (the bare station) or hidden (the game's desk stands there instead).
        private void StandIns(bool on)
        {
            for (int i = 0; i < _desks.Length; i++)
            {
                Transform standIn = _desks[i] != null ? _desks[i].Find(StandInName) : null;
                if (standIn != null && standIn.gameObject.activeSelf != on)
                {
                    standIn.gameObject.SetActive(on);
                }
            }
        }

        // A piece that stands whatever the walkway check finds: the counter, the cell, the bench, what stands along the walls.
        private void Fixed(float aside, float depth, float halfA, float halfD)
        {
            _fixedRects.Add(new FloorRect(aside, depth, halfA, halfD));
        }

        private bool NearHung(Vector3 surface, float within)
        {
            for (int i = 0; i < _hung.Count; i++)
            {
                if (CitySpots.Flat(_hung[i] - surface) < within * within && Mathf.Abs(_hung[i].y - surface.y) < 1.4f)
                {
                    return true;
                }
            }
            return false;
        }

        private float AsideOf(Vector3 world) => Vector3.Dot(world - _origin, _right);

        private float DepthOf(Vector3 world) => Vector3.Dot(world - _origin, _in);

        // The room's floor as a grid of cells, walkable or not, sampled before anything is cut out of the mesh.
        private void MeasureGrid()
        {
            float left = 0.5f, right = 0.5f;
            for (float depth = 0.4f; depth < _length; depth += 0.8f)
            {
                left = Mathf.Max(left, Run(At(depth, 0f), -_right, 30f));
                right = Mathf.Max(right, Run(At(depth, 0f), _right, 30f));
            }
            _grid = new StationGrid(-left - 0.6f, -0.9f, right + 0.6f, _length + 0.9f, GridCell);
            for (int row = 0; row < _grid.Rows; row++)
            {
                for (int col = 0; col < _grid.Cols; col++)
                {
                    _grid.Centre(col, row, out float a, out float d);
                    Vector3 point = At(d, a);
                    point.y = _floor;
                    bool walkable = NavMesh.SamplePosition(point, out NavMeshHit hit, 0.22f, _filter) && Mathf.Abs(hit.position.y - _floor) < 0.25f
                                    && CitySpots.Flat(hit.position - point) < 0.15f * 0.15f;
                    _grid.SetWalkable(col, row, walkable);
                }
            }
        }

        private FloorRect RectOf(Vector3 at, Quaternion facing, Bounds size)
        {
            float minA = float.MaxValue, maxA = float.MinValue, minD = float.MaxValue, maxD = float.MinValue;
            for (int corner = 0; corner < 4; corner++)
            {
                var local = new Vector3((corner & 1) != 0 ? size.max.x : size.min.x, 0f, (corner & 2) != 0 ? size.max.z : size.min.z);
                Vector3 world = at + facing * local;
                float a = AsideOf(world), d = DepthOf(world);
                minA = Mathf.Min(minA, a);
                maxA = Mathf.Max(maxA, a);
                minD = Mathf.Min(minD, d);
                maxD = Mathf.Max(maxD, d);
            }
            return new FloorRect((minA + maxA) * 0.5f, (minD + maxD) * 0.5f, Mathf.Max(0.05f, (maxA - minA) * 0.5f), Mathf.Max(0.05f, (maxD - minD) * 0.5f));
        }

        // A piece placed, switched off, for the walkway check to keep or leave out. Its index.
        private int Stage(Transform piece, Bounds size, Vector3 at, Quaternion facing, bool solid, int anchor, SeatUse use)
        {
            FloorRect rect = RectOf(at, facing, size);
            bool floor = solid && at.y - _floor < 0.15f && rect.HalfA * rect.HalfD * 4f > 0.04f;
            _staged.Add(new Staged
            {
                Piece = piece,
                Size = size,
                At = at,
                Facing = facing,
                Solid = solid,
                Floor = floor,
                Rect = rect,
                Anchor = anchor,
                Use = use
            });
            return _staged.Count - 1;
        }

        private int Stage(string prefab, Vector3 at, Quaternion facing, bool solid, int anchor, SeatUse use)
        {
            Transform piece = Piece(prefab, out Bounds size);
            return piece == null ? -1 : Stage(piece, size, at, facing, solid, anchor, use);
        }

        private bool OverlapsStaged(FloorRect rect, float margin)
        {
            for (int i = 0; i < _staged.Count; i++)
            {
                if (_staged[i].Floor && !_staged[i].Dropped && _staged[i].Rect.Overlaps(rect, margin))
                {
                    return true;
                }
            }
            return false;
        }

        private bool OverlapsFixed(FloorRect rect, float margin)
        {
            for (int i = 0; i < _fixedRects.Count; i++)
            {
                if (_fixedRects[i].Overlaps(rect, margin))
                {
                    return true;
                }
            }
            return false;
        }

        // The game's own office desk in place of the stand-in, and a chair at it. True, with its bounds, if the desk went in.
        private bool GameDesk(Transform desk, out Bounds size)
        {
            size = default;
            bool placed = false;
            Transform standIn = desk.Find(StandInName);
            if (standIn != null)
            {
                Transform made = Piece("OfficeDesk1", out Bounds b);
                if (made != null)
                {
                    standIn.gameObject.SetActive(false);
                    Put(made, b, desk.position, desk.rotation, false, desk);   // the holder is already cut out of the mesh
                    size = b;
                    placed = true;
                }
            }
            float front = placed ? size.max.z + 0.22f : 0.6f;
            Stage("OfficeChair", desk.TransformPoint(new Vector3(0f, 0f, front)), desk.rotation * Quaternion.Euler(0f, 180f, 0f), true, -1, SeatUse.Desk);
            return placed;
        }

        private void PlanWaitingChairs(int benchSide, float limit)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? (benchSide != 0 ? -benchSide : 1) : (benchSide != 0 ? benchSide : -1);
                _stageWall = SeatUse.Waiting;
                bool placed = false;
                for (float depth = 1.4f; depth < limit && !placed; depth += 0.35f)
                {
                    placed = TryWall("WaitingRoomChairs", side, depth, limit, out _);
                }
                _stageWall = SeatUse.None;
                if (placed)
                {
                    _fitted.Append("waiting chairs; ");
                    return;
                }
            }
        }

        private void PlanQueue(bool desk)
        {
            if (!desk)
            {
                return;
            }
            Transform probe = Piece("BeltBarrier", out Bounds size);
            if (probe == null)
            {
                return;
            }
            Unuse("BeltBarrier");
            float run = Mathf.Max(size.size.x, size.size.z);
            if (run < 0.8f || run > 3.5f)
            {
                return;   // not a length of belt between posts: no queue
            }
            Quaternion facing = size.size.x >= size.size.z ? Quaternion.LookRotation(_right, Vector3.up) : Quaternion.LookRotation(_in, Vector3.up);
            float service = DepthOf(ServiceSpot), aside = AsideOf(ServiceSpot);
            float depth = service - 0.3f - run * 0.5f;
            if (depth - run * 0.5f < 1.2f)
            {
                return;   // too near the door
            }
            int placed = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                float a = aside + side * 1.0f;
                Vector3 at = At(depth, a) - facing * new Vector3(size.center.x, 0f, size.center.z);
                at.y = _floor;
                FloorRect rect = RectOf(at, facing, size);
                if (OverlapsFixed(rect, 0.3f) || OverlapsStaged(rect, 0.3f) || !Free(At(depth, a), _in, run * 0.5f, _right, 0.1f))
                {
                    continue;
                }
                if (Stage("BeltBarrier", at, facing, true, -1, SeatUse.None) >= 0)
                {
                    placed++;
                }
            }
            if (placed > 0)
            {
                _fitted.Append("queue barriers; ");
            }
        }

        private void PlanBriefing(float from, float to)
        {
            const int Chairs = 3;
            const float Spacing = 0.75f, Out = 1.9f;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int side = attempt == 0 ? 1 : -1;
                for (float depth = from + 1.2f; depth <= to - 1.2f; depth += 0.5f)
                {
                    if (!SideWall(depth, side, 1.3f, out float edge) || edge < Out + 1.4f || !Surface(At(depth, side * edge), _right * side, out Vector3 surface))
                    {
                        continue;
                    }
                    float rowAside = side * (edge - Out);
                    var row = new FloorRect(rowAside, depth, 0.4f, Spacing * Chairs * 0.5f);
                    var between = new FloorRect(side * (edge - Out * 0.5f), depth, Out * 0.5f - 0.3f, 0.8f);   // the floor between the row and the screen
                    if (IsTaken(row.A, row.D, row.HalfA, row.HalfD) || IsTaken(between.A, between.D, between.HalfA, between.HalfD)
                        || OverlapsFixed(row, 0.1f) || OverlapsStaged(row, 0.1f) || NearHung(surface, 1.6f)
                        || (_board != null && _board.gameObject.activeSelf && CitySpots.Flat(_board.position - surface) < 2f * 2f)
                        || !Free(At(depth, rowAside), _in, row.HalfD, _right, 0.3f))
                    {
                        continue;
                    }
                    Transform screen = Piece("WallMountedTvXl", out Bounds tv);
                    if (screen == null)
                    {
                        return;
                    }
                    int screenAt = Stage(screen, tv, surface + Vector3.up * 1.45f - _right * side * 0.004f,
                        Quaternion.LookRotation(_right * -side, Vector3.up), false, -1, SeatUse.None);
                    _hung.Add(surface + Vector3.up * 1.45f);
                    Quaternion facing = Quaternion.LookRotation(_right * side, Vector3.up);   // the chairs face the screen
                    for (int i = 0; i < Chairs; i++)
                    {
                        Vector3 at = At(depth + (i - (Chairs - 1) * 0.5f) * Spacing, rowAside);
                        at.y = _floor;
                        Stage("FoldingChair", at, facing, true, screenAt, SeatUse.Briefing);
                    }
                    Take(row.A, row.D, row.HalfA, row.HalfD);
                    Take(between.A, between.D, between.HalfA, between.HalfD);
                    _hasBriefing = true;
                    _briefingScreen = surface;
                    _briefingFront = At(depth, side * (edge - 0.6f));
                    _fitted.Append("briefing row; ");
                    return;
                }
            }
        }

        private void PlanBreak(float from, float to)
        {
            Transform table = Piece("Table1", out Bounds t);
            if (table == null)
            {
                return;
            }
            Unuse("Table1");
            float tableLong = t.size.x, tableWide = t.size.z;
            float halfD = tableLong * 0.5f + 0.75f;   // the table and a chair at each end, along the way in
            float halfA = Mathf.Max(tableWide * 0.5f, 0.35f) + 0.1f;
            bool found = false;
            float bestSqr = float.MaxValue, bestA = 0f, bestD = 0f;
            for (float depth = from + halfD; depth <= to - halfD; depth += 0.4f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float edge = Run(At(depth, 0f), _right * side, 30f);
                    if (edge < halfA * 2f + Aisle * 2f)
                    {
                        continue;
                    }
                    float aside = side * (edge - halfA - 0.3f);
                    if (IsTaken(aside, depth, halfA, halfD) || OverlapsFixed(new FloorRect(aside, depth, halfA, halfD), 0.2f)
                        || OverlapsStaged(new FloorRect(aside, depth, halfA, halfD), 0.2f) || !Free(At(depth, aside), _in, halfD, _right, halfA))
                    {
                        continue;
                    }
                    float sqr = _hasCoffee ? CitySpots.Flat(At(depth, aside) - _coffeeFront) : -depth;   // near the coffee, or as far in as it goes
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        bestA = aside;
                        bestD = depth;
                        found = true;
                    }
                }
            }
            if (!found)
            {
                return;
            }
            // The table's length along the way in: its own x turned onto the room's depth.
            Quaternion along = Quaternion.LookRotation(_right, Vector3.up);
            Vector3 middle = At(bestD, bestA);
            middle.y = _floor;
            int at = Stage("Table1", middle - along * new Vector3(t.center.x, 0f, t.center.z), along, true, -1, SeatUse.None);
            if (at < 0)
            {
                return;
            }
            for (int end = -1; end <= 1; end += 2)
            {
                Vector3 chair = middle + _in * (end * (tableLong * 0.5f + 0.3f));
                Stage("RegularChair", chair, Quaternion.LookRotation(_in * -end, Vector3.up), true, at, SeatUse.Break);
            }
            Stage("DeskPlant02", middle + Vector3.up * t.max.y, along, false, at, SeatUse.None);
            Take(bestA, bestD, halfA, halfD);
            _fitted.Append("break table; ");
        }

        // Every way that has to stay open, checked on the measured floor with the furniture standing; what closes one is left out.
        private void KeepWays(bool cell)
        {
            if (_grid == null)
            {
                return;
            }
            Blocks();
            float margin = _radius * 0.8f;
            _targetA.Clear();
            _targetD.Clear();
            AddTarget(ServiceSpot);
            if (HasDesk)
            {
                AddTarget(_sergeantAt);
            }
            if (cell)
            {
                AddTarget(At(_cellBack - _cellDeep - 0.6f, _cellAside));
            }
            if (_hasCoffee)
            {
                AddTarget(_coffeeFront);
            }
            int removed = _grid.KeepWaysOpen(0f, 0.4f, _targetA, _targetD, _blocks, _blockKeep, _blockFixed, margin, 0.9f);
            // Then to every seat somebody may be sent to, up to a step and a half from it.
            _targetA.Clear();
            _targetD.Clear();
            for (int i = 0; i < _staged.Count; i++)
            {
                if (_staged[i].Use != SeatUse.None && !_staged[i].Dropped)
                {
                    AddTarget(_staged[i].At);
                }
            }
            removed += _grid.KeepWaysOpen(0f, 0.4f, _targetA, _targetD, _blocks, _blockKeep, _blockFixed, margin, 1.4f);
            for (int b = 0; b < _blocks.Count; b++)
            {
                int s = _blockStaged[b];
                if (s >= 0 && !_blockKeep[b])
                {
                    Drop(s);
                }
            }
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < _staged.Count; i++)
                {
                    int anchor = _staged[i].Anchor;
                    if (anchor >= 0 && _staged[anchor].Dropped && !_staged[i].Dropped)
                    {
                        Drop(i);
                    }
                }
            }
            if (removed > 0)
            {
                _fitted.Append(removed).Append(" piece(s) left out to keep the ways clear; ");
            }
        }

        private void AddTarget(Vector3 world)
        {
            _targetA.Add(AsideOf(world));
            _targetD.Add(DepthOf(world));
        }

        private void Drop(int index)
        {
            Staged s = _staged[index];
            s.Dropped = true;
            _staged[index] = s;
        }

        private void Blocks()
        {
            _blocks.Clear();
            _blockKeep.Clear();
            _blockFixed.Clear();
            _blockStaged.Clear();
            for (int i = 0; i < _fixedRects.Count; i++)
            {
                _blocks.Add(_fixedRects[i]);
                _blockKeep.Add(true);
                _blockFixed.Add(true);
                _blockStaged.Add(-1);
            }
            for (int i = 0; i < _staged.Count; i++)
            {
                if (_staged[i].Floor && !_staged[i].Dropped)
                {
                    _blocks.Add(_staged[i].Rect);
                    _blockKeep.Add(true);
                    _blockFixed.Add(false);
                    _blockStaged.Add(i);
                }
            }
        }

        // Every staged piece not left out switched on; then the seats among them, each with where somebody stands to sit.
        private void Activate()
        {
            for (int i = 0; i < _staged.Count; i++)
            {
                Staged s = _staged[i];
                if (!s.Dropped && s.Piece != null)
                {
                    Put(s.Piece, s.Size, s.At, s.Facing, s.Floor);   // one left out stays off, in the pool, for this visit
                }
            }
            if (_grid == null)
            {
                return;
            }
            Blocks();
            float margin = _radius * 0.8f;
            for (int i = 0; i < _staged.Count; i++)
            {
                Staged s = _staged[i];
                if (s.Dropped || s.Use == SeatUse.None || s.Piece == null)
                {
                    continue;
                }
                GameFurniture.Seats(s.Piece, _seatScratch);
                for (int k = 0; k < _seatScratch.Count; k++)
                {
                    Transform seat = _seatScratch[k];
                    // Where they stand to sit down: a step in front of the seat if the floor is open there, or the nearest open floor.
                    Vector3 front = seat.position + seat.forward * 0.55f;
                    if (!_grid.NearestOpen(0f, 0.4f, AsideOf(front), DepthOf(front), 1.0f, _blocks, _blockKeep, margin, out float a, out float d)
                        && !_grid.NearestOpen(0f, 0.4f, AsideOf(seat.position), DepthOf(seat.position), 1.6f, _blocks, _blockKeep, margin, out a, out d))
                    {
                        continue;
                    }
                    if (!Standable(At(d, a), out Vector3 stand) || !Seating.InReach(stand, seat))
                    {
                        continue;
                    }
                    _seats.Add(new SeatPlace { Seat = seat, Use = s.Use, Stand = stand });
                }
            }
            if (_seats.Count > 0)
            {
                _fitted.Append(_seats.Count).Append(" seat(s) for the people; ");
            }
        }
    }
}
