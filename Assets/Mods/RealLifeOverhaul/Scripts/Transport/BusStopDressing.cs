using BAModAPI;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What stands at one bus stop: the pole with its flag (route number both sides), and where
    /// the pavement has room a shelter with a bench and an end panel. Three of these exist (the
    /// stops nearest the player); each is built once, moved from stop to stop and switched off
    /// in between. The modelled props are used when the prop bundle has them
    /// (<c>bus_stop_sign</c>, <c>bus_shelter</c>); otherwise the same things are built from
    /// plain shapes to the same dimensions.
    ///
    /// IT IS SOLID. The game moves everyone on foot on the navigation mesh (the player by click
    /// and by the mod's own controls alike; colliders stop nobody), so what must not be walked
    /// through is cut out of the mesh, the way the cordon's walls and the frontage's cars are:
    /// a carving <c>NavMeshObstacle</c> each for the pole, the shelter's back, its end panel,
    /// the screen at its other end and the bench. They are children of the dressing: moved
    /// with it while it is switched off, cut once when it is switched on, gone with it. A cut
    /// is wider than its box by the walker's radius, so the boxes are the solid things
    /// themselves (and the bench's box is that much smaller than the bench): the open side
    /// stays open, the place in front of each seat stays on the mesh, and the pavement between
    /// the shelter and the kerb stays a way past. That way past is tested on the mesh before
    /// the shelter is shown: where it is not there, the stop is a pole only. A second after a
    /// stop is dressed <see cref="Check"/> asks the mesh what was really cut and says so in
    /// one line.
    ///
    /// THE BAY. Where the stop has one (<see cref="TransitStop.HasBay"/>) the place the bus stands
    /// in it is marked on the road: a yellow box and the words BUS STOP, read the way the traffic
    /// runs. Painted strips and lettering, nothing solid; made the first time this dressing stands
    /// at a stop with a bay, laid to the road's own fall each time it is put out, gone with the
    /// dressing. As the stop is dressed the bay is looked at once (<see cref="TransitBays"/>): the
    /// game's parked cars standing in it out of view are taken in, and what the check line says of
    /// the bay is what was found then.
    ///
    /// THE SEATS are two places on the bench (never one for two): the sitter's feet 0.30 m in
    /// front of the seat's middle line, facing the road, which is what the clips
    /// <c>sit_bench</c>, <c>sit_wait</c> and <c>sit_bench_up</c> are made for (seat 0.46 m up).
    /// </summary>
    public sealed class BusStopDressing
    {
        public const float ShelterLength = 3.6f;
        public const float ShelterDepth = 1.4f;
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
        private const float BoxLaneSide = 0.12f;    // ...and this far beyond its side, towards the lane
        private const float BoxKerbSide = 0.06f;    // its kerb-side line lies this far out from the kerb
        private const float LineWide = 0.12f;
        private const float LineTall = 0.03f;
        private const string BayWords = "reallife_transit_bay_marking";
        private static readonly Color BayYellow = new Color(0.98f, 0.82f, 0.10f);

        private readonly GameObject _root;
        private readonly Transform _sign;
        private readonly Transform _shelter;
        private readonly TextMeshPro[] _labels = new TextMeshPro[2];
        private readonly NavMeshObstacle[] _blocks = new NavMeshObstacle[5];   // pole, back, end panel, screen, bench
        private readonly byte[] _taken = new byte[Seats];                      // 0 free, 1 the player, 2 somebody waiting
        private bool _modelled;
        private bool _solid;       // the boxes are cutting the navigation mesh (only while the player is on foot near the stop)
        private bool _checkDue;
        private float _checkAt;
        private float _radius = 0.5f;
        private float _walkRoom;
        private string _walkNote;
        private Transform _bay;        // the bay's marking: four strips and the lettering
        private TextMeshPro _bayText;
        private bool _bayFree;
        private string _bayTaken;
        private int _baySent;

        public TransitStop Stop { get; private set; }
        public Transform Sign => _sign;

        /// <summary>The shelter's own object (switched off where the stop is a pole only): what points at the bench hangs under it.</summary>
        public Transform Shelter => _shelter;

        /// <summary>True while the stop's boxes cut the navigation mesh.</summary>
        public bool IsSolid => _solid;

        /// <summary>
        /// Makes the stop solid to people on foot, or lets the mesh close again. Cutting the navigation mesh is work
        /// for the engine every time it changes, so a stop is only cut while the player is on foot near it (the
        /// director decides): driving through the city, stops come and go as things to look at and the mesh is
        /// left alone. All of a stop's boxes change in the same frame, on purpose: the engine re-cuts the piece of
        /// mesh they stand on once for the lot, where one box a frame would have it cut five times.
        /// While a stop is not solid the game's pedestrians may walk through its pole and panels.
        /// </summary>
        public void Solid(bool on, float now)
        {
            if (on == _solid || Stop == null)
            {
                return;
            }
            _solid = on;
            SessionWatch.Mark(on ? "transit: a bus stop made solid (the navigation mesh is cut round it)" : "transit: a bus stop no longer solid (the navigation mesh closes)");
            for (int i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].enabled = on;
            }
            _checkDue = on;
            _checkAt = now + 1.6f;   // the cut is made once the boxes have stood still for half a second
        }

        /// <summary>The shelter stands at this stop (the stop has room for one and the way past it is there).</summary>
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
            _root.SetActive(false);   // before anything solid is added: nothing is cut out of the mesh until a stop is dressed
            _blocks[0] = Block("Pole Block", _sign);
            _blocks[1] = Block("Back Block", _shelter);
            _blocks[2] = Block("End Block", _shelter);
            _blocks[3] = Block("Screen Block", _shelter);
            _blocks[4] = Block("Bench Block", _shelter);
        }

        /// <summary>Stands the dressing at a stop. The text is set here, once per stop.</summary>
        /// <param name="walkerRadius">The radius the navigation mesh was made for (the player's kind of walker).</param>
        public void Put(TransitStop stop, string numbers, string mapText, float walkerRadius)
        {
            SessionWatch.Mark("transit: a bus stop being put out");
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
            // Not solid yet: nothing is cut out of the mesh for a stop that is only driven past (see Solid).
            _solid = false;
            for (int i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].enabled = false;
            }
            _checkDue = false;
            _root.SetActive(true);
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
            _root.SetActive(false);
        }

        public void Dispose()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        // ---------------------------------------------------------------- the bay

        /// <summary>What was found of the stop's bay when it was dressed, in words for a log line.</summary>
        public string BayFound()
        {
            if (Stop == null)
            {
                return "no stop";
            }
            if (!Stop.HasBay)
            {
                return "no bay (" + (Stop.NoBay ?? "none laid out") + "; the kerb is " + Stop.KerbAside.ToString("F2") + " m from the lane's line, "
                       + BusBay.KerbNeeded(TransitBus.BodyHalfWidth).ToString("F2") + " needed): the bus stops in the lane";
            }
            return "a bay " + Stop.BayLength.ToString("F0") + " m long (" + Stop.BayBehind.ToString("F0") + " m to come in, " + Stop.BayAhead.ToString("F0") + " to pull out), the bus "
                   + Stop.BayOffset.ToString("F2") + " m off the lane's line in it with the kerb at " + Stop.KerbAside.ToString("F2") + ", "
                   + (_bayFree ? "free" : "taken by " + _bayTaken + " (the bus stops in the lane until it is free)")
                   + (_baySent > 0 ? ", " + _baySent + " of the game's parked cars taken in from it out of view" : "")
                   + ", the marking " + (_bay != null && _bay.gameObject.activeSelf ? "made" : "NOT made");
        }

        // The marking, where the stop has a bay, and one look at what stands in the bay now.
        private void LayBay(TransitStop stop)
        {
            _bayFree = false;
            _bayTaken = null;
            _baySent = 0;
            if (!stop.HasBay)
            {
                if (_bay != null)
                {
                    _bay.gameObject.SetActive(false);
                }
                return;
            }
            if (_bay == null)
            {
                BuildBay();
            }
            if (_bay != null)
            {
                // The box round where the bus stands. It lies on the road as the road lies there: its ends and its two
                // sides are put on the ground under them, so a street that falls away or a cambered strip is followed.
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
                    forward = stop.BayAlong;   // (a reading that makes no sense: flat, at the bay's own height)
                    up = Vector3.up;
                }
                _bay.SetPositionAndRotation(new Vector3(middle.x, (back.y + front.y + laneSide.y + kerbSide.y) * 0.25f, middle.z), Quaternion.LookRotation(forward, up));
                Strip(_bay.GetChild(0), new Vector3(-halfWidth, LineTall, 0f), new Vector3(LineWide, LineTall, halfLength * 2f));
                Strip(_bay.GetChild(1), new Vector3(halfWidth, LineTall, 0f), new Vector3(LineWide, LineTall, halfLength * 2f));
                Strip(_bay.GetChild(2), new Vector3(0f, LineTall, halfLength), new Vector3(halfWidth * 2f + LineWide, LineTall, LineWide));
                Strip(_bay.GetChild(3), new Vector3(0f, LineTall, -halfLength), new Vector3(halfWidth * 2f + LineWide, LineTall, LineWide));
                if (_bayText != null)
                {
                    _bayText.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.8f, halfWidth * 2f - 0.5f), 3.6f);
                }
                _bay.gameObject.SetActive(true);
            }
            _bayFree = TransitBays.Free(stop, stop.BayBehind, stop.BayAhead, null, true, out _bayTaken, out _baySent);
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

        // Four painted strips (the driving test's bay is marked the same way: Licences/LicenceProps.MakeBay) and the words,
        // lying on the road and read the way the traffic runs. Local axes: Z along the bay, X towards the kerb.
        private void BuildBay()
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
            TMP_FontAsset font = Hud.Font;
            if (font == null)
            {
                return;
            }
            var words = new GameObject("Words");
            words.transform.SetParent(_bay, false);
            words.transform.localPosition = new Vector3(0f, LineTall + 0.01f, 0f);
            words.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // flat on the road, the tops of the letters ahead
            _bayText = words.AddComponent<TextMeshPro>();
            _bayText.font = font;
            _bayText.text = TransitText.T(BayWords);
            _bayText.color = new Color(BayYellow.r * 0.85f, BayYellow.g * 0.85f, BayYellow.b * 0.85f);
            _bayText.fontStyle = FontStyles.Bold;
            _bayText.alignment = TextAlignmentOptions.Center;
            _bayText.textWrappingMode = TextWrappingModes.Normal;
            _bayText.rectTransform.sizeDelta = new Vector2(1.8f, 3.6f);
            _bayText.enableAutoSizing = true;
            _bayText.fontSizeMin = 2f;
            _bayText.fontSizeMax = 14f;
            var renderer = words.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // (On the city map every stop of the network is a place of the "Bus stops" category: World/MapLinks, through Core/MapPlaces.
        // A dressed stop no longer adds a marker of its own, which would sit on top of that one.)

        // ---------------------------------------------------------------- what is solid

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

        // The boxes, for the walker's radius: the cut is that much wider than a box all round.
        private void Lay()
        {
            Set(_blocks[0], new Vector3(0f, 1f, 0f), new Vector3(0.08f, 2f, 0.08f), true);
            Set(_blocks[1], new Vector3(BackX, 1f, 0f), new Vector3(0.04f, 2f, ShelterLength), true);
            Set(_blocks[2], new Vector3(0f, 1f, EndZ), new Vector3(BackX * 2f + 0.04f, 2f, 0.04f), true);
            Set(_blocks[3], new Vector3(BackX - 0.24f, 1f, -EndZ), new Vector3(0.48f, 2f, 0.04f), true);
            // The bench: its cut is to end 4 cm in front of the bench's edge, so that the place a sitter's feet are (10 cm
            // further out) is still on the mesh. Where the back's own cut already reaches that far there is no box for it.
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

        // The pavement between the kerb and the shelter's open side, along the whole shelter and a metre beyond each end:
        // a line a walker can be on once the end panel has been cut out. Asked of the mesh before anything is cut.
        private bool WayPast(TransitStop stop, Quaternion facing)
        {
            _walkRoom = KerbFromMiddle - BackX - _radius;   // metres between the kerb's edge and the end panel's cut
            if (_walkRoom < 0.5f)
            {
                _walkNote = "only " + _walkRoom.ToString("F2") + " m would be left between the shelter and the kerb";
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

        /// <summary>
        /// A second after the stop was dressed: what the navigation mesh says was cut, and whether the seats and the way
        /// past are still on it. One line, once per dressing. Call from the director's timer; a no-op otherwise.
        /// </summary>
        /// <summary>True from a dressing until its self-check has been made (or dropped).</summary>
        public bool CheckDue => _checkDue;

        public void Check(IModLogger logger, float now, string name)
        {
            if (!_checkDue || Stop == null || now < _checkAt)
            {
                return;
            }
            _checkDue = false;
            if (logger == null)
            {
                return;   // the check only writes a line: nobody is reading
            }
            bool pole = !OnMesh(_sign.position);
            if (!_shelter.gameObject.activeSelf)
            {
                logger?.Info("Transit check (stop dressed): " + name + ": the pole" + (_modelled ? " (modelled)" : " (plain shapes)") + ", no shelter (" + (_walkNote ?? "none wanted")
                             + "); " + BayFound() + "; walker radius " + _radius.ToString("F2") + " m; cut out of the mesh: pole " + YesNo(pole) + ": " + (pole ? "PASS" : "FAIL (the pole can be walked through)") + ".");
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
            logger?.Info("Transit check (stop dressed): " + name + ": the pole and the shelter" + (_modelled ? " (modelled)" : " (plain shapes)") + ", bench seat " + BenchSeat.ToString("F2")
                         + " m up; " + BayFound() + "; walker radius " + _radius.ToString("F2") + " m; cut out of the mesh: pole " + YesNo(pole) + ", back " + YesNo(back) + ", end panel " + YesNo(end)
                         + ", screen " + YesNo(screen) + ", bench " + YesNo(bench) + "; still walkable: " + seats + " of " + Seats + " seat places, the open side " + YesNo(inside)
                         + ", the way past between shelter and kerb " + YesNo(past) + " (" + _walkRoom.ToString("F2") + " m to the kerb): " + (pass ? "PASS" : "FAIL") + ".");
        }

        private static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        private static bool OnMesh(Vector3 place)
        {
            return NavMesh.SamplePosition(place, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) && CitySpots.Flat(hit.position - place) < 0.06f * 0.06f;
        }

        // ---------------------------------------------------------------- the seats

        /// <summary>Where a sitter's feet are for a seat, on the pavement.</summary>
        public Vector3 SeatFeetAt(int seat)
        {
            return _shelter.TransformPoint(new Vector3(SeatFeet, 0f, SeatZ[seat]));
        }

        /// <summary>The way a sitter faces: out of the shelter, to the road.</summary>
        public Vector3 SeatFacing => -_shelter.right;

        public bool SeatFree(int seat)
        {
            return Sheltered && seat >= 0 && seat < Seats && _taken[seat] == 0;
        }

        /// <summary>The free seat nearest a place, within a distance of its feet place; -1 if none.</summary>
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

        // ---------------------------------------------------------------- the pole

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
                // The flag hangs towards the pavement side, across the kerb line, so it is read walking up from either way.
                Shapes.Piece("Flag", _sign, Shapes.Cube, blue, new Vector3(0.30f, 2.42f, 0f), new Vector3(0.50f, 0.62f, 0.03f));
                Shapes.Piece("Band", _sign, Shapes.Cube, white, new Vector3(0.30f, 2.20f, 0f), new Vector3(0.50f, 0.16f, 0.034f));
                Shapes.Piece("Times", _sign, Shapes.Cube, white, new Vector3(0.20f, 1.45f, 0f), new Vector3(0.26f, 0.36f, 0.025f));
            }
            // The route numbers, in the plain blue field of the flag: on the model that is from 2.33 m up to the white edge line
            // under the flag's top (2.73), inside the same line at both sides; the stand-in's band ends lower still (2.28).
            _labels[0] = Label(_sign, new Vector3(0.30f, RoutesAt, -0.019f), false);
            _labels[1] = Label(_sign, new Vector3(0.30f, RoutesAt, 0.019f), true);
        }

        // Lettering on one face of the flag. Text is read from behind its own forward.
        private static TextMeshPro Label(Transform parent, Vector3 at, bool back)
        {
            TMP_FontAsset font = Hud.Font;
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

        // ---------------------------------------------------------------- the shelter

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
            // The back: a rail at the foot and the top, panes between.
            Shapes.Piece("Back", _shelter, Shapes.Cube, pane, new Vector3(halfDepth - 0.04f, 1.30f, 0f), new Vector3(0.02f, 1.90f, ShelterLength - 0.16f));
            Shapes.Piece("Back Rail", _shelter, Shapes.Cube, frame, new Vector3(halfDepth - 0.04f, 0.32f, 0f), new Vector3(0.05f, 0.06f, ShelterLength - 0.10f));
            Shapes.Piece("Back Top", _shelter, Shapes.Cube, frame, new Vector3(halfDepth - 0.04f, 2.28f, 0f), new Vector3(0.05f, 0.06f, ShelterLength - 0.10f));
            // The end panel downstream: the route map and an advert, lit a little.
            Shapes.Piece("End Frame", _shelter, Shapes.Cube, frame, new Vector3(0f, 1.30f, halfLength - 0.04f), new Vector3(ShelterDepth - 0.16f, 1.96f, 0.05f));
            Shapes.Piece("Map", _shelter, Shapes.Cube, panel, new Vector3(0f, 1.32f, halfLength - 0.075f), new Vector3(ShelterDepth - 0.36f, 1.70f, 0.02f));
            // The screen at the back corner of the other end.
            Shapes.Piece("Screen", _shelter, Shapes.Cube, pane, new Vector3(halfDepth - 0.28f, 1.30f, -halfLength + 0.04f), new Vector3(0.48f, 1.90f, 0.02f));
            // The bench along the back.
            Shapes.Piece("Seat", _shelter, Shapes.Cube, wood, new Vector3(SeatMiddle, BenchSeat - 0.025f, -0.35f), new Vector3(0.40f, 0.05f, 1.90f));
            Shapes.Piece("Leg", _shelter, Shapes.Cube, frame, new Vector3(SeatMiddle, (BenchSeat - 0.05f) * 0.5f, -1.15f), new Vector3(0.34f, BenchSeat - 0.05f, 0.05f));
            Shapes.Piece("Leg", _shelter, Shapes.Cube, frame, new Vector3(SeatMiddle, (BenchSeat - 0.05f) * 0.5f, 0.45f), new Vector3(0.34f, BenchSeat - 0.05f, 0.05f));
        }
    }
}
