using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    // The station furnished with the game's own furniture (Core/GameFurniture), after the counter, the waiting bench, the
    // office desks and the cell have been planned. Everything is placed in the room's own frame (depth along the way in,
    // "aside" to the right of somebody walking in), only where the measured floor and wall have room for it, and never on
    // floor another piece, a person's place or a way through has already taken:
    //
    //   the counter      a monitor, a phone and a pile of papers on its top, on the sergeant's side
    //   the desks        a monitor, a phone and a plant on each officer's desk
    //   along the walls  in the public part: a water cooler, a plant, a bin; behind the counter: lockers, a printer, a
    //                    coffee machine, a uniform locker, shelving, a plant; each with its back to a straight stretch of wall
    //   on the walls     an exit sign over the door, a clock in the public part, framed certificates behind the counter
    //   the ceiling      flush lamps along the middle of the room, where a ray finds the ceiling
    //   the floor        a carpet in the waiting part
    //   the cell         a toilet and a sink, where the cell is big enough for them beside its bench
    //   the rooms        waiting chairs, a queue, a briefing row and a break table (StationInterior.Rooms.cs), and the game's
    //                    own design for the unit round all of it, its walls and floors too (StationInterior.Design.cs);
    //                    then the walkway check, and the people who use it all (StationInterior.Cast.cs)
    //
    // Nothing runs while the player is in the room: this is one pass on the way in, under the game's own fade. Its cost is
    // a few thousand walkable-floor samples and the first copy of each piece (kept for every later visit).
    internal sealed partial class StationInterior
    {
        private const float Clearance = 0.55f;      // metres kept free in front of a piece that stands against a wall
        private const float Aisle = 0.75f;          // half the width of the way from the door to the counter, round it, and to the cell
        private const int LampsMost = 3;

        // A piece of floor that is taken: in the room's frame, aside x, depth y.
        private struct Taken
        {
            public float A, D, HalfA, HalfD;
        }

        // One kind of piece along a wall: the game's prefab, how many a room may have, and whether it goes in front of or behind the counter.
        private struct WallKind
        {
            public string Prefab;
            public int Most;
            public bool Public;
        }

        private static readonly WallKind[] WallKinds =
        {
            new WallKind { Prefab = "OfficeLockers", Most = 2 },
            new WallKind { Prefab = "UniformLocker", Most = 2 },
            new WallKind { Prefab = "CheapCoffeeMachine", Most = 1 },
            new WallKind { Prefab = "StandardFridge", Most = 1 },
            new WallKind { Prefab = "Printer", Most = 1 },
            new WallKind { Prefab = "BigCabinet", Most = 1 },
            new WallKind { Prefab = "StorageShelf", Most = 1 },
            new WallKind { Prefab = "GroundPlant03", Most = 1 },
            new WallKind { Prefab = "WaterCooler", Most = 1, Public = true },
            new WallKind { Prefab = "GroundPlant03", Most = 1, Public = true },
            new WallKind { Prefab = "TrashBin", Most = 1, Public = true }
        };

        private readonly List<Taken> _taken = new List<Taken>(48);
        private readonly Dictionary<string, List<Transform>> _furniture = new Dictionary<string, List<Transform>>(24);
        private readonly Dictionary<string, int> _used = new Dictionary<string, int>(24);
        private readonly Dictionary<Transform, Bounds> _sizes = new Dictionary<Transform, Bounds>(48);
        private readonly int[] _kindCount = new int[WallKinds.Length];
        private int _pieces;

        // What PlanCell found, for the toilet and the sink.
        private float _cellBack, _cellDeep, _cellAside, _cellPanel;
        private int _cellSide;

        // ---------------------------------------------------------------- the pass

        private void Furnish(bool desk, bool bench, bool cell)
        {
            HideFurniture();
            StandIns(true);
            ClearRooms();
            if (!Settings.LawStationFurniture)
            {
                return;   // the old bare station: the counter, the bench, the desks and the cell, with the people as they were
            }
            SessionWatch.Mark("law: the station furnished with the game's own furniture");   // the first visit copies the pieces
            _taken.Clear();
            _pieces = 0;
            for (int i = 0; i < _kindCount.Length; i++)
            {
                _kindCount[i] = 0;
            }
            Reserve(desk, bench, cell);
            MeasureGrid();
            if (desk)
            {
                DressCounter();
            }
            for (int i = 0; i < _desks.Length; i++)
            {
                if (_staffed[i])
                {
                    DressDesk(_desks[i]);
                }
            }
            float counterFront = desk ? _deskDepth - _deskSize.z * 0.5f - 0.2f : _length * 0.4f;
            float counterBack = desk ? _deskDepth + _deskSize.z * 0.5f + 1.2f : _length * 0.4f;
            float back = cell ? _cellBack - _cellDeep - 0.9f : _length - 0.4f;
            // The rooms first (StationInterior.Rooms.cs): seats to wait on and the queue in the public part; a briefing
            // row facing a screen and a break table behind the counter. Then what stands along the walls.
            PlanWaitingChairs(bench ? _benchSide : 0, counterFront);
            PlanQueue(desk);
            PlanBriefing(counterBack, back);
            for (int side = -1; side <= 1; side += 2)
            {
                AlongWall(side, 1.5f, counterFront, true);
                AlongWall(side, counterBack, back, false);
            }
            PlanBreak(counterBack, back);   // (after the walls: the table goes near the coffee machine if there is one)
            if (cell)
            {
                DressCell();
            }
            // The game's own design for this very unit, round all of that (StationInterior.Design.cs), then every way
            // that has to stay open is checked and whatever closes one is left out.
            PlaceDesign();
            KeepWays(cell);
            Activate();
            if (!_designCarpet)
            {
                Carpet(desk, bench);
            }
            Hang(desk);
            if (!_designLamps)
            {
                Lamps();
            }
            ApplyLook();
            _cast = true;
            _fitted.Append(_pieces).Append(" piece(s) of the game's furniture; ");
        }

        // The ways through and the places already planned are taken before anything else is placed.
        private void Reserve(bool desk, bool bench, bool cell)
        {
            Take(0f, 0.8f, 1.6f, 0.8f);                                                         // the door
            float service = Vector3.Dot(ServiceSpot - _origin, _in);
            float serviceAside = Vector3.Dot(ServiceSpot - _origin, _right);
            Take(serviceAside * 0.5f, service * 0.5f, Aisle + Mathf.Abs(serviceAside) * 0.5f, service * 0.5f + 0.4f);   // the way to the counter
            if (desk)
            {
                float half = _deskSize.x * 0.5f, deep = _deskSize.z * 0.5f;
                Take(_deskAside, _deskDepth, half + 0.1f, deep + 0.1f);
                Fixed(_deskAside, _deskDepth, half, deep);
                Take(_deskAside, _deskDepth + deep + 0.65f, half, 0.65f);                        // the sergeant's side
                Take(_deskAside - half - 0.55f, _deskDepth, 0.6f, deep + 1.0f);                  // round either end
                Take(_deskAside + half + 0.55f, _deskDepth, 0.6f, deep + 1.0f);
                Take(_deskAside, _deskDepth + deep + 1.6f, Aisle, 0.4f);
            }
            if (bench && _bench != null)
            {
                Vector3 b = _bench.position - _origin;
                Take(Vector3.Dot(b, _right), Vector3.Dot(b, _in), 0.9f + 0.6f, _benchSize.x * 0.5f + 0.1f);   // the bench, its side of the room, and the visitor in front
                Fixed(Vector3.Dot(b, _right), Vector3.Dot(b, _in), _benchSize.z * 0.5f, _benchSize.x * 0.5f);
                Vector3 v = _visitorAt - _origin;
                Take(Vector3.Dot(v, _right), Vector3.Dot(v, _in), 0.4f, 0.4f);
            }
            for (int i = 0; i < _desks.Length; i++)
            {
                if (!_staffed[i])
                {
                    continue;
                }
                Vector3 d = _desks[i].position - _origin, s = _staffAt[i] - _origin;
                Take(Vector3.Dot(d, _right), Vector3.Dot(d, _in), 0.75f, 0.9f);
                Fixed(Vector3.Dot(d, _right), Vector3.Dot(d, _in), 0.4f, 0.75f);
                Take(Vector3.Dot(s, _right), Vector3.Dot(s, _in), 0.45f, 0.45f);
            }
            if (cell)
            {
                // The cell, the bars, and the floor in front of them where an officer stands and a prisoner is walked in.
                Take(_cellAside, _cellBack - _cellDeep * 0.5f, _cellPanel * 0.5f + 0.15f, _cellDeep * 0.5f + 0.15f);
                Fixed(_cellAside, _cellBack - _cellDeep * 0.5f, _cellPanel * 0.5f, _cellDeep * 0.5f);   // (the cell itself: nobody walks into it)
                Take(_cellAside, _cellBack - _cellDeep - 0.8f, _cellPanel * 0.5f, 0.8f);
                float from = desk ? _deskDepth + _deskSize.z * 0.5f : 2f, to = _cellBack - _cellDeep;
                Take(_cellAside, (from + to) * 0.5f, Aisle, Mathf.Max(0.1f, (to - from) * 0.5f));   // the way to the cell
            }
            if (_evidence != null && _evidence.gameObject.activeSelf)
            {
                Vector3 e = _evidence.position - _origin;
                Take(Vector3.Dot(e, _right), Vector3.Dot(e, _in) - 0.6f, 0.8f, 0.7f);
            }
        }

        private void Take(float aside, float depth, float halfA, float halfD)
        {
            _taken.Add(new Taken { A = aside, D = depth, HalfA = halfA, HalfD = halfD });
        }

        private bool IsTaken(float aside, float depth, float halfA, float halfD)
        {
            for (int i = 0; i < _taken.Count; i++)
            {
                Taken t = _taken[i];
                if (Mathf.Abs(t.A - aside) < t.HalfA + halfA && Mathf.Abs(t.D - depth) < t.HalfD + halfD)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the pieces, copied once and kept

        // The next copy of a piece for this visit; null if the game has no such piece.
        private Transform Piece(string prefab, out Bounds size)
        {
            size = default;
            if (!_furniture.TryGetValue(prefab, out List<Transform> copies))
            {
                copies = new List<Transform>(2);
                _furniture.Add(prefab, copies);
            }
            _used.TryGetValue(prefab, out int used);
            if (used >= copies.Count)
            {
                Transform made = GameFurniture.Make(prefab, FurnitureHolder());
                if (made == null)
                {
                    return null;
                }
                copies.Add(made);
                _sizes[made] = GameFurniture.LocalBounds(made);
            }
            Transform piece = copies[used];
            _used[prefab] = used + 1;
            size = _sizes[piece];
            return piece;
        }

        private Transform _furnitureHolder;

        private Transform FurnitureHolder()
        {
            if (_furnitureHolder == null)
            {
                _furnitureHolder = new GameObject("RealLifeOverhaul Station Furniture").transform;
                _furnitureHolder.SetParent(_root, false);
            }
            return _furnitureHolder;
        }

        // A piece goes in: placed, cut out of the walkable floor if it stands on it, switched on.
        private void Put(Transform piece, Bounds size, Vector3 at, Quaternion facing, bool solid, Transform parent = null)
        {
            piece.SetParent(parent != null ? parent : FurnitureHolder(), true);
            piece.SetPositionAndRotation(at, facing);
            if (solid && piece.GetComponent<NavMeshObstacle>() == null)
            {
                StationProps.Solid(piece, new Vector3(size.center.x, size.size.y * 0.5f, size.center.z), new Vector3(size.size.x, Mathf.Max(0.5f, size.size.y), size.size.z));
            }
            piece.gameObject.SetActive(true);
            _pieces++;
        }

        private void HideFurniture()
        {
            foreach (KeyValuePair<string, List<Transform>> pair in _furniture)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Transform piece = pair.Value[i];
                    if (piece != null)
                    {
                        piece.gameObject.SetActive(false);
                        piece.SetParent(FurnitureHolder(), false);   // (off a counter or a desk it may have been put on)
                    }
                }
            }
            _used.Clear();
        }

        private void DestroyFurniture()
        {
            if (_furnitureHolder != null)
            {
                Object.Destroy(_furnitureHolder.gameObject);
            }
            _furnitureHolder = null;
            _furniture.Clear();
            _sizes.Clear();
            _used.Clear();
        }

        // ---------------------------------------------------------------- on the counter and the desks

        // On the counter's top, on the sergeant's side (its -Z), facing them: a monitor, a phone, a pile of papers.
        private void DressCounter()
        {
            float top = _deskSize.y, half = _deskSize.x * 0.5f;
            Quaternion toSergeant = _desk.rotation * Quaternion.Euler(0f, 180f, 0f);
            OnTop(_desk, "ComputerMonitor", new Vector3(-half * 0.45f, top, -0.12f), toSergeant);
            OnTop(_desk, "OfficePhone", new Vector3(half * 0.15f, top, -0.12f), toSergeant);
            OnTop(_desk, "StackOfBooks", new Vector3(half * 0.6f, top, -0.1f), toSergeant * Quaternion.Euler(0f, 18f, 0f));
        }

        // On an officer's desk, facing the officer in front of it: a monitor, a phone and a small plant.
        private void DressDesk(Transform desk)
        {
            // The game's own desk and chair in place of the stand-in (StationInterior.Rooms.cs); its top is what is dressed.
            if (!GameDesk(desk, out Bounds size) && desk.childCount > 0 && !_sizes.TryGetValue(desk, out size))
            {
                size = GameFurniture.LocalBounds(desk);
                _sizes[desk] = size;
            }
            float top = size.size.y > 0.3f ? size.max.y : 0.75f;
            float half = size.size.x > 0.4f ? size.extents.x : 0.7f;
            OnTop(desk, "ComputerMonitor", new Vector3(-half * 0.35f, top, 0f), desk.rotation);
            OnTop(desk, "OfficePhone", new Vector3(half * 0.4f, top, 0.05f), desk.rotation);
            OnTop(desk, "DeskPlant01", new Vector3(half * 0.8f, top, -0.12f), desk.rotation);
        }

        // A small piece on a surface, at a place in the surface's own frame.
        private void OnTop(Transform surface, string prefab, Vector3 local, Quaternion facing)
        {
            Transform piece = Piece(prefab, out Bounds size);
            if (piece == null)
            {
                return;
            }
            Put(piece, size, surface.TransformPoint(local), facing, false, surface);
        }

        // ---------------------------------------------------------------- along the walls

        // Along one side wall between two depths: the kinds of piece for that part of the room, each where a straight
        // stretch of wall, the floor in front of it and nothing taken leave room for it, one after another.
        private void AlongWall(int side, float from, float to, bool publicPart)
        {
            float depth = from;
            int guard = 0;
            while (depth < to && guard++ < 64)
            {
                bool placed = false;
                for (int k = 0; k < WallKinds.Length && !placed; k++)
                {
                    WallKind kind = WallKinds[k];
                    if (kind.Public != publicPart || _kindCount[k] >= kind.Most)
                    {
                        continue;
                    }
                    if (TryWall(kind.Prefab, side, depth, to, out float width))
                    {
                        _kindCount[k]++;
                        if (kind.Prefab == "CheapCoffeeMachine" && _lastWall != null)
                        {
                            _hasCoffee = true;
                            _coffeeFront = _lastWall.position + _lastWall.forward * 0.9f;   // where somebody stands to use it
                            _lastCoffeeFace = -_lastWall.forward;
                        }
                        depth += width + 0.25f;
                        placed = true;
                    }
                }
                if (!placed)
                {
                    depth += 0.35f;
                }
            }
        }

        private bool TryWall(string prefab, int side, float start, float limit, out float width)
        {
            width = 0f;
            Transform piece = Piece(prefab, out Bounds size);
            if (piece == null)
            {
                return false;
            }
            width = size.size.x;
            float deep = size.size.z;
            float depth = start + width * 0.5f;
            if (depth + width * 0.5f > limit || !SideWall(depth, side, width * 0.5f, out float edge) || edge < deep + Aisle + 0.4f)
            {
                Unuse(prefab);
                return false;
            }
            // The floor's edge is a body's width short of the wall: the piece's back goes to the wall itself.
            float wall = edge + _radius - FromWall;
            float aside = side * (wall - deep * 0.5f);
            float halfIn = Mathf.Max(0.05f, deep * 0.5f - _radius * 0.5f);
            if (IsTaken(aside, depth, deep * 0.5f, width * 0.5f + 0.05f) || IsTaken(side * (wall - deep - Clearance * 0.5f), depth, Clearance * 0.5f, width * 0.5f)
                || !Free(At(depth, side * (edge - halfIn)), _in, width * 0.5f, _right, halfIn) || !Free(At(depth, side * (edge - deep - Clearance * 0.5f)), _in, width * 0.5f, _right, Clearance * 0.5f))
            {
                Unuse(prefab);
                return false;
            }
            Vector3 at = At(depth, aside) - Rotate(side, new Vector3(size.center.x, 0f, size.center.z));
            at.y = _floor;
            Quaternion facing = Quaternion.LookRotation(_right * -side, Vector3.up);
            if (_stageWall != SeatUse.None)
            {
                // A seat along the wall (the waiting chairs): staged, so that the walkway check may still leave it out.
                _lastStaged = Stage(piece, size, at, facing, true, -1, _stageWall, false);
            }
            else
            {
                Put(piece, size, at, facing, true);
                Fixed(aside, depth, deep * 0.5f, width * 0.5f);
            }
            Take(aside, depth, deep * 0.5f, width * 0.5f);
            _lastWall = piece;
            Take(side * (wall - deep - Clearance * 0.5f), depth, Clearance * 0.5f, width * 0.5f);
            return true;
        }

        // A piece's own offset turned into the room's frame, for one that faces away from the wall on this side.
        private Vector3 Rotate(int side, Vector3 local)
        {
            return Quaternion.LookRotation(_right * -side, Vector3.up) * local;
        }

        private void Unuse(string prefab)
        {
            if (_used.TryGetValue(prefab, out int used) && used > 0)
            {
                _used[prefab] = used - 1;
            }
        }

        // ---------------------------------------------------------------- in the cell

        // A toilet against the back wall beside the bench, and a sink on the cell's side wall: only where both fit with room to stand.
        private void DressCell()
        {
            if (_cellSide == 0)
            {
                return;
            }
            Transform toilet = Piece("ToiletClassic", out Bounds t);
            if (toilet == null)
            {
                return;
            }
            float tHalf = t.extents.x, tDeep = t.size.z;
            float aside = _cellAside + _cellSide * (_cellPanel * 0.5f - tHalf - 0.2f);
            float benchHalf = _cellBenchFits ? _benchSize.x * 0.5f : 0f;
            if (Mathf.Abs(aside - _cellAside) - tHalf < benchHalf + 0.1f || _cellDeep - tDeep < 1.2f)
            {
                Unuse("ToiletClassic");
                return;
            }
            Vector3 at = At(_cellBack - FromWall, aside) - Quaternion.LookRotation(-_in, Vector3.up) * new Vector3(t.center.x, 0f, t.min.z);
            at.y = _floor;
            Put(toilet, t, at, Quaternion.LookRotation(-_in, Vector3.up), true);
            Transform sink = Piece("SinkWallModern", out Bounds s);
            if (sink == null)
            {
                return;
            }
            // On the cell's own side wall, half way along it, at the height the game hangs it.
            float depth = _cellBack - _cellDeep * 0.5f + 0.2f;
            float wallAside = _cellAside + _cellSide * _cellPanel * 0.5f;
            if (Surface(At(depth, wallAside - _cellSide * 0.3f), _right * _cellSide, out Vector3 surface))
            {
                Put(sink, s, surface + Vector3.up * 0.85f - _right * _cellSide * 0.004f, Quaternion.LookRotation(_right * -_cellSide, Vector3.up), false);
            }
            else
            {
                Unuse("SinkWallModern");
            }
        }

        // ---------------------------------------------------------------- the floor, the walls, the ceiling

        // A carpet in the waiting part, lying from the door towards the counter, where the whole of it is floor.
        private void Carpet(bool desk, bool bench)
        {
            Transform carpet = Piece("CarpetBigBasic", out Bounds c);
            if (carpet == null)
            {
                return;
            }
            float long_ = Mathf.Max(c.size.x, c.size.z), wide = Mathf.Min(c.size.x, c.size.z);
            float depth = 1.4f + long_ * 0.5f;
            float limit = desk ? _deskDepth - _deskSize.z * 0.5f - 0.3f : _length - 1f;
            if (depth + long_ * 0.5f > limit || !Free(At(depth, 0f), _in, long_ * 0.5f, _right, wide * 0.5f))
            {
                Unuse("CarpetBigBasic");
                return;
            }
            // The carpet's long side along the way in: turned a quarter if the game's runs across.
            Quaternion facing = Quaternion.LookRotation(_in, Vector3.up) * (c.size.x > c.size.z ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            Vector3 at = At(depth, 0f) - facing * new Vector3(c.center.x, 0f, c.center.z);
            at.y = _floor + 0.003f;
            Put(carpet, c, at, facing, false);
        }

        // On the walls, where a ray finds the wall's real surface: the exit sign over the door, a clock in the public
        // part, and framed certificates on the side walls behind the counter.
        private void Hang(bool desk)
        {
            if (Surface(At(0.6f, 0f), -_in, out Vector3 door))
            {
                Transform exit = Piece("ExitSign", out Bounds e);
                if (exit != null)
                {
                    Put(exit, e, door + Vector3.up * 2.35f + _in * 0.004f, Quaternion.LookRotation(_in, Vector3.up), false);
                }
            }
            float clockDepth = Mathf.Min(3.2f, _length * 0.35f);
            for (int side = -1; side <= 1; side += 2)
            {
                if (OnWall("LargeWallClock", side, clockDepth, 2.25f))
                {
                    break;
                }
            }
            if (!desk)
            {
                return;
            }
            float certificates = _deskDepth + _deskSize.z * 0.5f + 1.4f;
            int hung = 0;
            for (int side = -1; side <= 1 && hung < 2; side += 2)
            {
                hung += OnWall("FramedCertificate", side, certificates, 1.75f) ? 1 : 0;
            }
        }

        // A piece hung on a side wall at a depth and a height: the wall straight there and its surface found; nothing else hung within a metre.
        private bool OnWall(string prefab, int side, float depth, float height)
        {
            if (depth > _length - 0.6f || !SideWall(depth, side, 0.5f, out float edge) || !Surface(At(depth, side * edge), _right * side, out Vector3 surface))
            {
                return false;
            }
            if (_board != null && _board.gameObject.activeSelf && CitySpots.Flat(_board.position - surface) < 1.6f * 1.6f)
            {
                return false;   // the notice board hangs there
            }
            if (NearHung(surface, 1.0f))
            {
                return false;   // something of the game's design hangs there
            }
            Transform piece = Piece(prefab, out Bounds size);
            if (piece == null)
            {
                return false;
            }
            Put(piece, size, surface + Vector3.up * height - _right * side * 0.004f, Quaternion.LookRotation(_right * -side, Vector3.up), false);
            return true;
        }

        // Flush lamps on the ceiling down the middle of the room: where a ray up from the floor finds a flat ceiling over the room.
        private void Lamps()
        {
            int lit = 0;
            for (int i = 0; i < LampsMost; i++)
            {
                float depth = _length * (i + 1) / (LampsMost + 1);
                Vector3 from = At(depth, 0f) + Vector3.up * 1.2f;
                if (!Physics.Raycast(from, Vector3.up, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.normal.y > -0.9f)
                {
                    continue;
                }
                Transform lamp = Piece("CeilingLampSquare", out Bounds size);
                if (lamp == null)
                {
                    return;
                }
                Put(lamp, size, hit.point - Vector3.up * 0.002f, Quaternion.LookRotation(_in, Vector3.up), false);
                lit++;
            }
            if (lit > 0)
            {
                _fitted.Append(lit).Append(" ceiling lamp(s); ");
            }
        }
    }
}
