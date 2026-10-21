using System.Globalization;
using BAModAPI;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What stands at one bus stop: the pole and, where there is room, a shelter with a bench; modelled where the prop bundle
    /// has them, else plain shapes. One per place, built once. Carving NavMeshObstacles cut exactly the solid things out of
    /// the mesh, so the seats stay walkable; a stop with a bay has its zone painted on the road (RoadPaint) or strips.
    /// Every place of the city stands while the city is loaded: beyond the game's far band it is not drawn (ModCulling).
    /// </summary>
    public sealed class BusStopDressing
    {
        public const float ShelterLength = 3.6f;    // metres
        public const float ShelterDepth = 1.4f;     // metres
        public const float BenchSeat = 0.46f;       // metres up: the model's bench, and the seat the clips are made for
        public const int Seats = 2;

        private const float BackX = 0.66f;          // the back posts' line
        private const float EndZ = 1.76f;           // the end posts' line
        private const float BenchFront = 0.20f;     // the bench: x 0.20 to 0.60, z -1.30 to +0.60
        private const float BenchBack = 0.60f;
        private const float BenchFrom = -1.30f;
        private const float BenchTo = 0.60f;
        private const float SeatMiddle = 0.40f;     // the middle line of the seat
        private const float SeatFeet = 0.10f;       // where the sitter's feet are: 0.30 m in front of it
        private const float KerbFromMiddle = 2.35f; // the kerb's edge is this far from the shelter's middle (TransitMap.Try)
        private const float RoutesAt = 2.53f;       // the middle of the flag's plain blue field (2.33 to 2.73 m up on bus_stop_sign)
        private const float RoutesWide = 0.44f;     // the lettering's box: 3 cm inside the flag's edges, 2 cm clear of the band below
        private const float RoutesHigh = 0.34f;
        private static readonly float[] SeatZ = { -0.85f, 0.15f };
        private const float BoxEnd = 0.9f;          // the bay's box reaches this far beyond each end of the bus
        private const float BoxLaneSide = 0.12f;    // and this far beyond its side, towards the lane
        private const float BoxKerbSide = 0.06f;    // its kerb-side line lies this far out from the kerb
        private const float LineWide = 0.12f;       // metres, a strip's width
        private const float LineTall = 0.03f;
        private static readonly Color BayYellow = new Color(0.98f, 0.82f, 0.10f);
        private const float PaintWide = 0.17f;      // the broken line's decal, of which 0.15 m is paint (decals.py)
        private const float PaintOut = 0.25f;       // the line lies this far beyond the bus's side, towards the lane
        private const float PaintKerb = 0.15f;      // and its tapers end this far short of the kerb, clear of its face
        private const float PaintTaper = 4f;        // metres along which a taper comes in from the line to the kerb
        private const float PaintBehind = 3f;       // the box reaches this far behind the standing bus's tail
        private const float PaintAhead = 2f;        // and this far ahead of its nose (it stands with its middle at the pole)
        private const float PaintPiece = 8f;        // the longest piece of line one decal: the road's fall along it stays within its depth
        private const float WordsWide = 1.9f;       // metres, the words BUS STOP on the road
        private const float WordsLong = 4.6f;
        private const float WordsAhead = 1f;        // their middle this far ahead of the pole
        private const int PaintPieces = 8;          // seven pieces of line at the most, and the words
        private const float CullRadius = 15f;       // metres round the pole: the shelter and the bay's strips (Performance/ModCulling)

        private readonly GameObject _root;
        private readonly Transform _sign;
        private readonly Transform _shelter;
        private readonly TextMeshPro[] _labels = new TextMeshPro[2];
        private readonly NavMeshObstacle[] _blocks = new NavMeshObstacle[5];   // pole, back, end panel, screen, bench
        private CullEntry _cull;                    // not drawn beyond the far band while it stands (Performance/ModCulling)
        private readonly byte[] _taken = new byte[Seats];                      // 0 free, 1 the player, 2 somebody waiting
        private bool _modelled;
        private bool _solid;       // the boxes cut the navigation mesh, only while the player is on foot near the stop
        private bool _checkDue;
        private float _checkAt;
        private float _radius = 0.5f;
        private float _walkRoom;
        private string _walkNote;
        private Transform _bay;        // the bay's strips where there is no road paint
        private Transform _paintRoot;  // the bay's paint, where there is
        private readonly DecalProjector[] _paint = new DecalProjector[PaintPieces];
        private string _marking;
        private bool _bayFree;
        private string _bayTaken;
        private int _baySent;

        public TransitStop Stop { get; private set; }
        public Transform Sign => _sign;

        /// <summary>The shelter's own object, off where the stop is a pole only.</summary>
        public Transform Shelter => _shelter;

        public bool IsSolid => _solid;   // the stop's boxes cut the navigation mesh

        /// <summary>Makes the stop solid to walkers, or not: a cut is work for the engine, so all its boxes in one frame.</summary>
        public void Solid(bool on, float now)
        {
            if (on == _solid || Stop == null)
            {
                return;
            }
            _solid = on;
            MeshCuts.Note(on ? "a bus stop made solid" : "a bus stop no longer solid");
            using var piece = SessionWatch.Piece(on ? "transit: a bus stop made solid (the navigation mesh is cut round it)" : "transit: a bus stop no longer solid (the navigation mesh closes)");
            for (int i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].enabled = on;
            }
            _checkDue = on;
            _checkAt = now + 1.6f;   // the cut is made once the boxes have stood still for half a second
        }

        public bool Sheltered => Stop != null && _shelter.gameObject.activeSelf;

        public BusStopDressing(Transform parent, int number)
        {
            _root = new GameObject("RealLifeOverhaul Bus Stop " + number);
            _root.transform.SetParent(parent, false);
            _sign = new GameObject("Sign").transform;
            _sign.SetParent(_root.transform, false);
            _shelter = new GameObject("Shelter").transform;
            _shelter.SetParent(_root.transform, false);
            BuildSign();
            BuildShelter();
            _root.SetActive(false);   // before anything solid is added, so nothing is cut until a stop is dressed
            _blocks[0] = Block("Pole Block", _sign);
            _blocks[1] = Block("Back Block", _shelter);
            _blocks[2] = Block("End Block", _shelter);
            _blocks[3] = Block("Screen Block", _shelter);
            _blocks[4] = Block("Bench Block", _shelter);
        }

        /// <summary>
        /// Behind the loading screen: the bay's paint (or, with none to copy, its strips) made and the lettering's glyphs drawn
        /// once, so a dressing put up in play makes nothing new. <paramref name="glyphs"/> holds every line's number.
        /// </summary>
        public void Prepare(string glyphs)
        {
            if (!PaintReady() && _bay == null)
            {
                BuildStrips();
            }
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] != null)
                {
                    _labels[i].text = glyphs;
                    _labels[i].ForceMeshUpdate(true);   // drawn although switched off: the font has every glyph before play
                }
            }
        }

        /// <summary>Stands the dressing at a stop, for a mesh made for <paramref name="walkerRadius"/>.</summary>
        public void Put(TransitStop stop, string numbers, float walkerRadius)
        {
            using var piece = SessionWatch.Piece("transit: a bus stop being put out");
            _root.SetActive(false);   // moved while off, so the mesh is cut once, in the right place
            Stop = stop;
            _radius = Mathf.Clamp(walkerRadius, 0.15f, 0.6f);
            for (int i = 0; i < Seats; i++)
            {
                _taken[i] = 0;
            }
            Quaternion facing = Quaternion.LookRotation(stop.Along, Vector3.up);
            _sign.SetPositionAndRotation(stop.Sign, facing);
            _walkRoom = 0f;
            _walkNote = stop.Shelter ? null : "the pavement has no room for a shelter";
            bool shelter = stop.Shelter && WayPast(stop, facing);
            _shelter.gameObject.SetActive(shelter);
            if (shelter)
            {
                _shelter.SetPositionAndRotation(stop.ShelterAt, facing);
            }
            Lay();
            LayBay(stop);
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] != null)
                {
                    _labels[i].text = numbers;
                }
            }
            // Nothing is cut for a stop only driven past.
            _solid = false;
            for (int i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].enabled = false;
            }
            _checkDue = false;
            _root.SetActive(true);
            if (_cull == null)
            {
                _cull = ModCulling.Add(CullRules.Kind.BusStop, _root.transform, stop.Sign, CullRadius);
            }
            else
            {
                ModCulling.Move(_cull, stop.Sign, CullRadius);
            }
        }

        public void Clear()
        {
            Stop = null;
            _solid = false;
            _checkDue = false;
            for (int i = 0; i < Seats; i++)
            {
                _taken[i] = 0;
            }
            ModCulling.Remove(_cull);
            _cull = null;
            _root.SetActive(false);
        }

        public void Dispose()
        {
            ModCulling.Remove(_cull);
            _cull = null;
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        /// <summary>What was found of the stop's bay, for a log line.</summary>
        public string BayFound()
        {
            if (Stop == null)
            {
                return "no stop";
            }
            if (!Stop.HasBay)
            {
                return "no bay (" + (Stop.NoBay ?? "none laid out") + "; the kerb is " + Stop.KerbAside.ToString("F2", CultureInfo.InvariantCulture)
                       + " m from the lane's line, " + BusBay.KerbNeeded(TransitBus.BodyHalfWidth).ToString("F2", CultureInfo.InvariantCulture)
                       + " needed): the bus stops in the lane";
            }
            return "a bay " + Stop.BayLength.ToString("F0") + " m long (" + Stop.BayBehind.ToString("F0") + " m to come in, " + Stop.BayAhead.ToString("F0")
                   + " to pull out), the bus " + Stop.BayOffset.ToString("F2", CultureInfo.InvariantCulture) + " m off the lane's line in it with the kerb at "
                   + Stop.KerbAside.ToString("F2", CultureInfo.InvariantCulture) + ", "
                   + (_bayFree ? "free" : "taken by " + _bayTaken + " (the bus stops in the lane until it is free)")
                   + (_baySent > 0 ? ", " + _baySent + " of the game's parked cars taken in from it out of view" : "")
                   + ", " + (_marking ?? "NOT marked") + "; " + BusZones.Describe(Stop.Zone);
        }

        private void LayBay(TransitStop stop)
        {
            _bayFree = false;
            _bayTaken = null;
            _baySent = 0;
            _marking = null;
            bool paint = stop.HasBay && PaintReady();
            if (_paintRoot != null)
            {
                _paintRoot.gameObject.SetActive(paint);
            }
            if (_bay != null)
            {
                _bay.gameObject.SetActive(stop.HasBay && !paint);
            }
            if (!stop.HasBay)
            {
                return;
            }
            if (paint)
            {
                Paint(stop);
                _marking = "painted";
            }
            else
            {
                Strips(stop);
                _marking = _bay == null ? "NOT marked" : "marked with strips (" + (RoadPaint.Missing ?? "no road paint") + ")";
            }
            _bayFree = TransitBays.Free(stop, stop.BayBehind, stop.BayAhead, null, true, out _bayTaken, out _baySent);
        }

        // The projectors, made once both paints can be had (the prop bundle loads in the background).
        private bool PaintReady()
        {
            if (_paintRoot != null)
            {
                return true;
            }
            Material dash = RoadPaint.Paint(RoadPaint.Dash);
            Material words = dash != null ? RoadPaint.Paint(RoadPaint.Words) : null;
            if (words == null)
            {
                return false;
            }
            _paintRoot = new GameObject("Paint").transform;
            _paintRoot.SetParent(_root.transform, false);
            for (int i = 0; i < PaintPieces - 1; i++)
            {
                _paint[i] = RoadPaint.Make(_paintRoot, "Line", dash);
            }
            _paint[PaintPieces - 1] = RoadPaint.Make(_paintRoot, "Words", words);
            return true;
        }

        // Painted round where the bus stands (some 24 m): the broken line, tapers in to the kerb, and the words.
        private void Paint(TransitStop stop)
        {
            Vector3 right = Vector3.Cross(Vector3.up, stop.BayAlong);
            float half = BusBay.BusLength * 0.5f;
            float from = Mathf.Max(stop.Zone != null ? stop.Zone.From : -stop.BayBehind, -(half + PaintBehind + PaintTaper));
            float to = Mathf.Min(stop.Zone != null ? stop.Zone.To : stop.BayAhead, half + PaintAhead + PaintTaper);
            float kerb = stop.KerbAside - PaintKerb;
            float line = Mathf.Max(BusBay.LaneHalf + 0.12f, stop.BayOffset - TransitBus.BodyHalfWidth - PaintOut);
            float taper = Mathf.Min(PaintTaper, (to - from) * 0.25f);
            int used = Line(stop, right, kerb, from, line, from + taper, 0);
            used = Line(stop, right, line, from + taper, line, to - taper, used);
            used = Line(stop, right, line, to - taper, kerb, to, used);
            for (int i = used; i < PaintPieces - 1; i++)
            {
                _paint[i].gameObject.SetActive(false);
            }
            float wide = Mathf.Clamp(kerb - line - 0.3f, 0.8f, WordsWide);
            Lie(_paint[PaintPieces - 1], At(stop, right, (line + kerb) * 0.5f, WordsAhead), stop.BayAlong, wide, wide * WordsLong / WordsWide, 1f, 0f);
        }

        // One broken line from (aside a, along s) to (aside b, along t), in pieces of at most PaintPiece metres.
        private int Line(TransitStop stop, Vector3 right, float a, float s, float b, float t, int used)
        {
            Vector3 start = At(stop, right, a, s);
            Vector3 run = At(stop, right, b, t) - start;
            run.y = 0f;
            float length = run.magnitude;
            if (length < 0.3f)
            {
                return used;
            }
            Vector3 direction = run / length;
            float pattern = length / BusBay.Dashes(length, RoadPaint.DashRepeat, RoadPaint.DashPaint);
            int pieces = Mathf.Max(1, Mathf.CeilToInt(length / PaintPiece));
            for (int p = 0; p < pieces && used < PaintPieces - 1; p++)
            {
                float near = length * p / pieces;
                float far = length * (p + 1) / pieces;
                Lie(_paint[used++], start + direction * ((near + far) * 0.5f), direction, PaintWide, far - near, (far - near) / pattern, 1f - RoadPaint.DashPaint + near / pattern);
            }
            return used;
        }

        private static Vector3 At(TransitStop stop, Vector3 right, float aside, float along)
        {
            return stop.Lane + stop.BayAlong * along + right * aside;
        }

        // One piece of paint, tipped to the road's fall.
        private static void Lie(DecalProjector decal, Vector3 middle, Vector3 along, float width, float length, float repeats, float shift)
        {
            Vector3 across = Vector3.Cross(Vector3.up, along);
            float reach = Mathf.Max(0.5f, length * 0.5f);
            float y = middle.y;
            Vector3 back = OnGround(middle - along * reach, y);
            Vector3 front = OnGround(middle + along * reach, y);
            Vector3 laneSide = OnGround(middle - across * 0.5f, y);
            Vector3 kerbSide = OnGround(middle + across * 0.5f, y);
            Vector3 up = Vector3.Cross(front - back, kerbSide - laneSide);
            up = up.y > 0.9f * up.magnitude && up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;   // a reading that makes no sense: flat
            middle.y = (back.y + front.y + laneSide.y + kerbSide.y) * 0.25f;
            RoadPaint.Pose(decal, middle, along, up, width, length, repeats, shift);
        }

        // No road paint to copy: a yellow box of four strips round where the bus stands.
        private void Strips(TransitStop stop)
        {
            if (_bay == null)
            {
                BuildStrips();
            }
            if (_bay == null)
            {
                return;
            }
            Vector3 right = Vector3.Cross(Vector3.up, stop.BayAlong);
            float halfLength = TransitBus.BodyLength * 0.5f + BoxEnd;
            float inner = stop.BayOffset - TransitBus.BodyHalfWidth - BoxLaneSide;
            float outer = stop.KerbAside - BoxKerbSide;
            float halfWidth = (outer - inner) * 0.5f;
            Vector3 middle = stop.Lane + right * ((inner + outer) * 0.5f);
            Vector3 back = OnGround(middle - stop.BayAlong * halfLength, stop.Bay.y);
            Vector3 front = OnGround(middle + stop.BayAlong * halfLength, stop.Bay.y);
            Vector3 laneSide = OnGround(middle - right * halfWidth, stop.Bay.y);
            Vector3 kerbSide = OnGround(middle + right * halfWidth, stop.Bay.y);
            Vector3 forward = front - back;
            Vector3 up = Vector3.Cross(forward, kerbSide - laneSide);
            if (up.y < 0.9f * up.magnitude)
            {
                forward = stop.BayAlong;   // a reading that makes no sense: flat, at the bay's own height
                up = Vector3.up;
            }
            _bay.SetPositionAndRotation(new Vector3(middle.x, (back.y + front.y + laneSide.y + kerbSide.y) * 0.25f, middle.z), Quaternion.LookRotation(forward, up));
            Strip(_bay.GetChild(0), new Vector3(-halfWidth, LineTall, 0f), new Vector3(LineWide, LineTall, halfLength * 2f));
            Strip(_bay.GetChild(1), new Vector3(halfWidth, LineTall, 0f), new Vector3(LineWide, LineTall, halfLength * 2f));
            Strip(_bay.GetChild(2), new Vector3(0f, LineTall, halfLength), new Vector3(halfWidth * 2f + LineWide, LineTall, LineWide));
            Strip(_bay.GetChild(3), new Vector3(0f, LineTall, -halfLength), new Vector3(halfWidth * 2f + LineWide, LineTall, LineWide));
            _bay.gameObject.SetActive(true);
        }

        private static Vector3 OnGround(Vector3 place, float otherwise)
        {
            place.y = CitySpots.Ground(new Vector3(place.x, otherwise, place.z), out float ground) && Mathf.Abs(ground - otherwise) < 1f ? ground : otherwise;
            return place;
        }

        private static void Strip(Transform piece, Vector3 position, Vector3 size)
        {
            piece.localPosition = position;
            piece.localScale = size;
        }

        // Four strips, as the driving test's bay is marked. Z along the bay, X towards the kerb.
        private void BuildStrips()
        {
            if (!Shapes.Available)
            {
                return;
            }
            _bay = new GameObject("Bay").transform;
            _bay.SetParent(_root.transform, false);
            Material line = Shapes.Paint(BayYellow, 0.6f);
            for (int i = 0; i < 4; i++)
            {
                Shapes.Piece("Line", _bay, Shapes.Cube, line, Vector3.zero, Vector3.one);
            }
        }

        private static NavMeshObstacle Block(string name, Transform parent)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            NavMeshObstacle block = box.AddComponent<NavMeshObstacle>();
            block.shape = NavMeshObstacleShape.Box;
            block.carving = true;
            block.carveOnlyStationary = true;
            return block;
        }

        // The boxes, for the walker's radius: the cut is that much wider.
        private void Lay()
        {
            Set(_blocks[0], new Vector3(0.12f, 1f, 0f), new Vector3(0.40f, 2f, 0.08f), true);   // the pole and its timetable plate
            Set(_blocks[1], new Vector3(BackX, 1f, 0f), new Vector3(0.04f, 2f, ShelterLength), true);
            Set(_blocks[2], new Vector3(0f, 1f, EndZ), new Vector3(BackX * 2f + 0.04f, 2f, 0.04f), true);
            Set(_blocks[3], new Vector3(BackX - 0.24f, 1f, -EndZ), new Vector3(0.48f, 2f, 0.04f), true);
            // The bench's cut ends 4 cm in front of it, so the sitter's feet (10 cm further out) stay on the mesh.
            float front = BenchFront - 0.04f + _radius;
            float length = BenchTo - BenchFrom - 2f * _radius;
            bool bench = front < BenchBack - 0.02f && BackX - 0.02f - _radius > BenchFront - 0.04f;
            Set(_blocks[4], new Vector3((front + BenchBack + 0.02f) * 0.5f, 0.5f, (BenchFrom + BenchTo) * 0.5f),
                new Vector3(BenchBack + 0.02f - front, 1f, Mathf.Max(0.1f, length)), bench);
        }

        private static void Set(NavMeshObstacle block, Vector3 centre, Vector3 size, bool on)
        {
            block.center = centre;
            block.size = size;
            block.gameObject.SetActive(on);
        }

        // The pavement between the kerb and the shelter's open side must be one walkable strip, asked before any cut.
        private bool WayPast(TransitStop stop, Quaternion facing)
        {
            _walkRoom = KerbFromMiddle - BackX - _radius;   // metres between the kerb's edge and the end panel's cut
            if (_walkRoom < 0.5f)
            {
                _walkNote = "only " + _walkRoom.ToString("F2", CultureInfo.InvariantCulture) + " m would be left between the shelter and the kerb";
                return false;
            }
            float x = -(BackX + _radius + 0.12f);
            Vector3 from = stop.ShelterAt + facing * new Vector3(x, 0f, -(EndZ + 1f));
            Vector3 to = stop.ShelterAt + facing * new Vector3(x, 0f, EndZ + 1f);
            if (!NavMesh.SamplePosition(from, out NavMeshHit a, 0.4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out NavMeshHit b, 0.4f, NavMesh.AllAreas)
                || NavMesh.Raycast(a.position, b.position, out _, NavMesh.AllAreas))
            {
                _walkNote = "the pavement between where the shelter would stand and the kerb is not one walkable strip";
                return false;
            }
            _walkNote = null;
            return true;
        }

        public bool CheckDue => _checkDue;   // from a dressing until its self-check has been made

        /// <summary>A second after dressing: what the mesh says was cut, in one line. From the director's timer.</summary>
        public void Check(IModLogger logger, float now, string name)
        {
            if (!_checkDue || Stop == null || now < _checkAt)
            {
                return;
            }
            _checkDue = false;
            if (logger == null)
            {
                return;   // the check only writes a line
            }
            bool pole = !OnMesh(_sign.position);
            if (!_shelter.gameObject.activeSelf)
            {
                logger.Info("Transit check (stop dressed): " + name + ": the pole" + (_modelled ? " (modelled)" : " (plain shapes)") + ", no shelter ("
                            + (_walkNote ?? "none wanted") + "); " + BayFound() + "; walker radius " + _radius.ToString("F2", CultureInfo.InvariantCulture)
                            + " m; cut out of the mesh: pole " + YesNo(pole) + ": " + (pole ? "PASS" : "FAIL (the pole can be walked through)") + ".");
                return;
            }
            bool back = !OnMesh(_shelter.TransformPoint(new Vector3(BackX, 0f, 0.9f)));
            bool end = !OnMesh(_shelter.TransformPoint(new Vector3(0f, 0f, EndZ)));
            bool screen = !OnMesh(_shelter.TransformPoint(new Vector3(BackX - 0.24f, 0f, -EndZ)));
            bool bench = !OnMesh(_shelter.TransformPoint(new Vector3(SeatMiddle, 0f, -0.35f)));
            int seats = 0;
            for (int i = 0; i < Seats; i++)
            {
                seats += OnMesh(SeatFeetAt(i)) ? 1 : 0;
            }
            bool inside = OnMesh(_shelter.TransformPoint(new Vector3(-0.35f, 0f, 0f)));
            float x = -(BackX + _radius + 0.12f);
            Vector3 from = _shelter.TransformPoint(new Vector3(x, 0f, -(EndZ + 1f)));
            Vector3 to = _shelter.TransformPoint(new Vector3(x, 0f, EndZ + 1f));
            bool past = NavMesh.SamplePosition(from, out NavMeshHit a, 0.4f, NavMesh.AllAreas) && NavMesh.SamplePosition(to, out NavMeshHit b, 0.4f, NavMesh.AllAreas)
                        && !NavMesh.Raycast(a.position, b.position, out _, NavMesh.AllAreas);
            bool pass = pole && back && end && screen && bench && seats == Seats && inside && past;
            logger.Info("Transit check (stop dressed): " + name + ": the pole and the shelter" + (_modelled ? " (modelled)" : " (plain shapes)") + ", bench seat "
                         + BenchSeat.ToString("F2", CultureInfo.InvariantCulture)
                         + " m up; " + BayFound() + "; walker radius " + _radius.ToString("F2", CultureInfo.InvariantCulture) + " m; cut out of the mesh: pole " + YesNo(pole) + ", back "
                         + YesNo(back) + ", end panel " + YesNo(end)
                         + ", screen " + YesNo(screen) + ", bench " + YesNo(bench) + "; still walkable: " + seats + " of " + Seats + " seat places, the open side " + YesNo(inside)
                         + ", the way past between shelter and kerb " + YesNo(past) + " (" + _walkRoom.ToString("F2", CultureInfo.InvariantCulture) + " m to the kerb): " + (pass ? "PASS" : "FAIL")
                         + ".");
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        private static bool OnMesh(Vector3 place)
        {
            return NavMesh.SamplePosition(place, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) && CitySpots.Flat(hit.position - place) < 0.06f * 0.06f;
        }

        public Vector3 SeatFeetAt(int seat)
        {
            return _shelter.TransformPoint(new Vector3(SeatFeet, 0f, SeatZ[seat]));
        }

        /// <summary>The way a sitter faces, out of the shelter to the road.</summary>
        public Vector3 SeatFacing => -_shelter.right;

        public bool SeatFree(int seat)
        {
            return Sheltered && seat >= 0 && seat < Seats && _taken[seat] == 0;
        }

        /// <summary>The free seat with its feet place nearest, within a distance; -1 if none.</summary>
        public int FreeSeat(Vector3 near, float within)
        {
            if (!Sheltered)
            {
                return -1;
            }
            int found = -1;
            float best = within * within;
            for (int i = 0; i < Seats; i++)
            {
                float sqr = CitySpots.Flat(SeatFeetAt(i) - near);
                if (_taken[i] == 0 && sqr < best)
                {
                    best = sqr;
                    found = i;
                }
            }
            return found;
        }

        public void TakeSeat(int seat, bool player)
        {
            if (seat >= 0 && seat < Seats)
            {
                _taken[seat] = (byte)(player ? 1 : 2);
            }
        }

        public void LeaveSeat(int seat)
        {
            if (seat >= 0 && seat < Seats)
            {
                _taken[seat] = 0;
            }
        }

        private void BuildSign()
        {
            if (!Shapes.Available)
            {
                return;
            }
            _modelled = Shapes.Prop("bus_stop_sign", _sign);
            if (!_modelled)
            {
                Material steel = Shapes.Paint(new Color(0.42f, 0.44f, 0.46f));
                Material blue = Shapes.Paint(new Color(0.08f, 0.27f, 0.58f), 0.35f);
                Material white = Shapes.Paint(new Color(0.93f, 0.93f, 0.90f), 0.35f);
                Shapes.Piece("Pole", _sign, Shapes.Drum, steel, Vector3.zero, new Vector3(0.07f, 2.85f, 0.07f));
                Shapes.Piece("Foot", _sign, Shapes.Drum, steel, Vector3.zero, new Vector3(0.16f, 0.03f, 0.16f));
                // The flag hangs towards the pavement, across the kerb line, so it is read walking up from either way.
                Shapes.Piece("Flag", _sign, Shapes.Cube, blue, new Vector3(0.30f, 2.42f, 0f), new Vector3(0.50f, 0.62f, 0.03f));
                Shapes.Piece("Band", _sign, Shapes.Cube, white, new Vector3(0.30f, 2.20f, 0f), new Vector3(0.50f, 0.16f, 0.034f));
                Shapes.Piece("Times", _sign, Shapes.Cube, white, new Vector3(0.20f, 1.45f, 0f), new Vector3(0.26f, 0.36f, 0.025f));
            }
            // The route numbers in the flag's plain blue field, 2.33 to 2.73 m up on the model.
            _labels[0] = Label(_sign, new Vector3(0.30f, RoutesAt, -0.019f), false);
            _labels[1] = Label(_sign, new Vector3(0.30f, RoutesAt, 0.019f), true);
        }

        // Lettering on one face of the flag, read from behind its own forward.
        private static TextMeshPro Label(Transform parent, Vector3 at, bool back)
        {
            TMP_FontAsset font = GameUi.GameFont.Asset;
            if (font == null)
            {
                return null;
            }
            var label = new GameObject("Routes");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = at;
            label.transform.localRotation = back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(RoutesWide, RoutesHigh);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.3f;
            text.fontSizeMax = 2.4f;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return text;
        }

        // Local axes: Z along the kerb, X away from the road. Open to the road; the back is at +X.
        private void BuildShelter()
        {
            if (!Shapes.Available || Shapes.Prop("bus_shelter", _shelter))
            {
                return;
            }
            Material frame = Shapes.Paint(new Color(0.20f, 0.22f, 0.24f));
            Material roof = Shapes.Paint(new Color(0.30f, 0.33f, 0.36f));
            Material pane = Shapes.Paint(new Color(0.55f, 0.66f, 0.70f));
            Material panel = Shapes.Paint(new Color(0.90f, 0.88f, 0.80f), 0.5f);
            Material wood = Shapes.Paint(new Color(0.45f, 0.30f, 0.17f));
            float halfLength = ShelterLength * 0.5f;
            float halfDepth = ShelterDepth * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -halfDepth + 0.04f : halfDepth - 0.04f;
                float z = (i & 2) == 0 ? -halfLength + 0.04f : halfLength - 0.04f;
                Shapes.Piece("Post", _shelter, Shapes.Cube, frame, new Vector3(x, 1.2f, z), new Vector3(0.07f, 2.4f, 0.07f));
            }
            Shapes.Piece("Roof", _shelter, Shapes.Cube, roof, new Vector3(-0.08f, 2.44f, 0f), new Vector3(ShelterDepth + 0.30f, 0.08f, ShelterLength + 0.16f));
            Shapes.Piece("Back", _shelter, Shapes.Cube, pane, new Vector3(halfDepth - 0.04f, 1.30f, 0f), new Vector3(0.02f, 1.90f, ShelterLength - 0.16f));
            Shapes.Piece("Back Rail", _shelter, Shapes.Cube, frame, new Vector3(halfDepth - 0.04f, 0.32f, 0f), new Vector3(0.05f, 0.06f, ShelterLength - 0.10f));
            Shapes.Piece("Back Top", _shelter, Shapes.Cube, frame, new Vector3(halfDepth - 0.04f, 2.28f, 0f), new Vector3(0.05f, 0.06f, ShelterLength - 0.10f));
            // The end panel: the route map and an advert, lit a little.
            Shapes.Piece("End Frame", _shelter, Shapes.Cube, frame, new Vector3(0f, 1.30f, halfLength - 0.04f), new Vector3(ShelterDepth - 0.16f, 1.96f, 0.05f));
            Shapes.Piece("Map", _shelter, Shapes.Cube, panel, new Vector3(0f, 1.32f, halfLength - 0.075f), new Vector3(ShelterDepth - 0.36f, 1.70f, 0.02f));
            Shapes.Piece("Screen", _shelter, Shapes.Cube, pane, new Vector3(halfDepth - 0.28f, 1.30f, -halfLength + 0.04f), new Vector3(0.48f, 1.90f, 0.02f));
            Shapes.Piece("Seat", _shelter, Shapes.Cube, wood, new Vector3(SeatMiddle, BenchSeat - 0.025f, -0.35f), new Vector3(0.40f, 0.05f, 1.90f));
            Shapes.Piece("Leg", _shelter, Shapes.Cube, frame, new Vector3(SeatMiddle, (BenchSeat - 0.05f) * 0.5f, -1.15f), new Vector3(0.34f, BenchSeat - 0.05f, 0.05f));
            Shapes.Piece("Leg", _shelter, Shapes.Cube, frame, new Vector3(SeatMiddle, (BenchSeat - 0.05f) * 0.5f, 0.45f), new Vector3(0.34f, BenchSeat - 0.05f, 0.05f));
        }
    }
}
