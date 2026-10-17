using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using GleyTrafficSystem;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// What makes a designated building look like a police station from the street. One set of
    /// objects, built once and moved to whichever precinct the player is near; every piece is
    /// measured against that building before it goes up, and a piece that does not fit is left
    /// off and named in the log:
    ///   the fascia     the building's own sign band over the shopfront (the game's wide sign,
    ///                  which would read "for rent") is switched off and a police-blue board of
    ///                  exactly its size put in its place: POLICE and the precinct's name
    ///   the sign       the standing POLICE sign with the precinct's name, against the wall
    ///   the canopy     a blue entrance canopy over the door, where the wall above it is flat and
    ///                  nothing is in the way (and the fascia does not sit there)
    ///   the lamps      twin blue lamps on brackets either side of the door, brighter after dark
    ///   the emblem     a shield plaque on a flat piece of wall
    ///   the board      a notice board on the wall
    ///   the door       POLICE lettered on the door itself
    ///   the kerb       bollards along the kerb either side of the way in, and a flag pole
    ///   the cars       up to three patrol cars at the kerb (the game's police car prop)
    ///   the officers   two at the door; one who comes out to a car and goes back in, one who
    ///                  walks in off the street, and now and then a suspect brought in in handcuffs
    /// Wall pieces go on what was measured: the wall is found with rays square on to it, a piece
    /// needs a flat patch its own size (five rays that agree) and a clear box in front of it, and
    /// no two pieces share a patch. Nothing is put out or taken in where the camera is looking,
    /// except under a dark screen. Called once a second by <see cref="Precincts"/>; nothing here
    /// runs by itself.
    /// </summary>
    internal sealed class PrecinctFrontage
    {
        private const float OfficersWithin = 110f;
        private const float SuspectWithin = 75f;
        private const float CarWithin = 48f;         // metres from the door a patrol car may stand
        private const float LaneGap = 2.0f;
        private const float OtherLane = 2.2f;
        private const float Room = 0.45f;
        private const float SignFromWall = 0.12f;    // close in: nobody walking along the wall is ever that near it
        private const int CarSearches = 8;           // looks for kerb places per visit, six seconds apart; after that the frontage has the cars it has
        private const int Cars = 3;
        private const int Bollards = 6;
        private const float RayOut = 1.4f;           // wall rays start this far out from the door's plane
        private const float FlatWithin = 0.06f;      // the rays of one patch agree to within this
        private const string GuardClip = "officer_guard_idle";   // wished for (docs/features/law.md); guard_stand until then

        private static readonly float[] SignAside = { 2.3f, 3.3f, 1.7f, 4.2f };   // metres along the wall from the door, tried in turn on either side
        private static readonly float[] KerbOffsets = { 3.0f, 3.3f, 3.6f };
        private static readonly float[] LampAside = { 1.15f, 1.4f, 1.7f };
        private static readonly float[] BollardAside = { -1.4f, 1.4f, -2.8f, 2.8f, -4.2f, 4.2f };
        private static readonly float[] PoleAside = { 5.4f, -5.4f, 6.6f, -6.6f };
        private static readonly Vector2[] EmblemPlaces =
        {
            new Vector2(2.1f, 2.0f), new Vector2(-2.1f, 2.0f), new Vector2(2.9f, 2.0f), new Vector2(-2.9f, 2.0f),
            new Vector2(3.7f, 2.0f), new Vector2(-3.7f, 2.0f), new Vector2(0f, 3.2f), new Vector2(0f, 4.2f)
        };
        private static readonly Vector2[] BoardPlaces =
        {
            new Vector2(-2.5f, 1.55f), new Vector2(2.5f, 1.55f), new Vector2(-3.4f, 1.55f), new Vector2(3.4f, 1.55f),
            new Vector2(-4.3f, 1.55f), new Vector2(4.3f, 1.55f)
        };

        private enum Piece { Fascia, Sign, Awning, LampA, LampB, Emblem, Board, Door, Bollards, Pole, Count }

        private enum Act { None, OutToCar, AtCar, BackIn, WalkIn, Suspect }

        private readonly IModLogger _logger;
        private readonly Transform _root;
        private readonly ActorRole[] _officers = { new ActorRole(), new ActorRole() };
        private readonly ActorRole _mover = new ActorRole();
        private readonly ActorRole _suspect = new ActorRole();
        private readonly GameObject[] _cars = new GameObject[Cars];
        private readonly bool[] _carOut = new bool[Cars];
        private readonly Vector3[] _carAt = new Vector3[Cars];
        private readonly Transform[] _bollards = new Transform[Bollards];
        private readonly Transform[] _lamps = new Transform[2];
        private readonly MeshRenderer[] _lampGlass = new MeshRenderer[3];   // the two on the wall and the one on the sign (stand-ins only)
        private readonly List<Vector4> _patches = new List<Vector4>(12);    // wall taken: from, to along it; from, to up it
        private readonly List<BuildingSignController> _signsOff = new List<BuildingSignController>(4);
        private readonly List<BuildingLogoSignController> _logosOff = new List<BuildingLogoSignController>(4);
        private readonly string[] _why = new string[(int)Piece.Count];
        private readonly bool[] _fits = new bool[(int)Piece.Count];
        private readonly float[] _depths = new float[12];

        private Transform _sign;
        private TextMeshPro _signName;
        private Transform _fascia;
        private Transform _fasciaBoard;
        private TextMeshPro _fasciaPolice;
        private TextMeshPro _fasciaName;
        private Transform _awning;
        private Transform _emblem;
        private Transform _board;
        private Transform _pole;
        private TextMeshPro _doorText;
        private bool _built;
        private bool _builtModelled;
        private int _waits;

        private float _halfWidth = 1f;
        private float _halfLength = 2.6f;
        private bool _carsFailed;
        private Precinct _dressed;
        private Vector3 _front;          // on the pavement in front of the dressed precinct's door
        private Vector3 _out;            // from the door towards the street
        private Vector3 _across;         // along the wall
        private Vector3 _doorGround;     // the door, at the height of the pavement
        private Vector3 _doorSpot;       // where somebody steps out of (or into) the door
        private bool _frontFound;
        private bool _measured;
        private bool _wallKnown;
        private float _wall;             // how far out from the door's plane the wall face is
        private bool _statics;           // the wall and kerb pieces are out
        private bool _signOut;
        private Vector3 _signAt;
        private Vector3 _signAcross;
        private int _bollardsOut;
        private float _nextOfficers;
        private float _nextCars;
        private float _nextFront;
        private int _carSearches;
        private bool _night;
        private bool _nightKnown;
        private int _solidMask;
        private int _wallMask;

        private Act _act;
        private float _actUntil;
        private float _nextAct;
        private int _actCar;
        private ThirdPersonCharacter _cuffed;

        public PrecinctFrontage(Transform root, IModLogger logger)
        {
            _root = root;
            _logger = logger;
        }

        public Precinct Dressed => _dressed;

        // ---------------------------------------------------------------- the visit

        /// <summary>Once a second while the player is out of doors and this precinct is the one they are near.</summary>
        public void Tick(float now, Vector3 player, Precinct near)
        {
            if (_dressed != near)
            {
                Undress();
                Begin(near);
            }
            Dress(now, player, force: false);
        }

        /// <summary>Dresses a precinct at once, in view or not: called with the screen dark, just before the player is put outside it.</summary>
        public void DressNow(Precinct precinct)
        {
            if (precinct == null)
            {
                return;
            }
            if (_dressed != precinct)
            {
                Undress();
                Begin(precinct);
            }
            Dress(Time.time, _frontFound ? _front : precinct.Door, force: true);
        }

        private void Begin(Precinct precinct)
        {
            _dressed = precinct;
            _frontFound = Precincts.Front(precinct, out _front, out _out);
            _measured = false;
            _statics = false;
            _waits = 0;
            _nextOfficers = 0f;
            _nextCars = 0f;
            _nextFront = 0f;
            _carSearches = 0;
            _bollardsOut = 0;
            _act = Act.None;
            _nextAct = Time.time + UnityEngine.Random.Range(20f, 45f);
            _patches.Clear();
            for (int i = 0; i < _fits.Length; i++)
            {
                _fits[i] = false;
                _why[i] = "not tried";
            }
            // Stand-ins built before the prop bundle had loaded are thrown away once it has: the models take their place.
            if (_built && !_builtModelled && PropLibrary.Ready)
            {
                DestroyPieces();
            }
        }

        private void Dress(float now, Vector3 player, bool force)
        {
            if (!_frontFound)
            {
                // Pavement may have been cut away by a parked car on the day: looked for again now and then.
                if (now >= _nextFront)
                {
                    _nextFront = now + 10f;
                    _frontFound = Precincts.Front(_dressed, out _front, out _out);
                }
                if (!_frontFound)
                {
                    return;
                }
            }
            if (!_measured)
            {
                Measure();
            }
            if (!_statics)
            {
                // The models come from the prop bundle: it is given a few seconds to load before stand-ins are built.
                if (!PropLibrary.Ready && !force && !_built && ++_waits < 20)
                {
                    return;
                }
                if (force || !StreetSpots.IsInView(_doorGround))
                {
                    PlaceStatics();
                }
            }
            else
            {
                KeepSignsOff();
                Light();
            }
            if (now >= _nextCars && !_carsFailed && _carSearches < CarSearches && TrafficTools.Ready && CarsOut() < Cars)
            {
                _nextCars = now + 6f;
                _carSearches++;
                PlaceCars(force);
            }
            TickPeople(now, player, force);
        }

        /// <summary>Everything off the street, and the building's own signs as the game has them. Safe at any time, any number of times.</summary>
        public void Undress()
        {
            try
            {
                EndAct();
                for (int i = 0; i < _officers.Length; i++)
                {
                    _officers[i].Exit();
                }
                for (int i = 0; i < _cars.Length; i++)
                {
                    if (_cars[i] != null)
                    {
                        _cars[i].SetActive(false);
                    }
                    _carOut[i] = false;
                }
                Off(_sign);
                Off(_fascia);
                Off(_awning);
                Off(_emblem);
                Off(_board);
                Off(_pole);
                Off(_lamps[0]);
                Off(_lamps[1]);
                if (_doorText != null)
                {
                    _doorText.gameObject.SetActive(false);
                }
                for (int i = 0; i < _bollards.Length; i++)
                {
                    Off(_bollards[i]);
                }
                RestoreSigns();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _signOut = false;
            _statics = false;
            _measured = false;
            _dressed = null;
            _frontFound = false;
        }

        public void Dispose()
        {
            Undress();
            DestroyPieces();
            for (int i = 0; i < _cars.Length; i++)
            {
                if (_cars[i] != null)
                {
                    UnityEngine.Object.Destroy(_cars[i]);
                    _cars[i] = null;
                }
            }
        }

        private void DestroyPieces()
        {
            Kill(ref _sign);
            Kill(ref _fascia);
            Kill(ref _awning);
            Kill(ref _emblem);
            Kill(ref _board);
            Kill(ref _pole);
            Kill(ref _lamps[0]);
            Kill(ref _lamps[1]);
            for (int i = 0; i < _bollards.Length; i++)
            {
                Kill(ref _bollards[i]);
            }
            if (_doorText != null)
            {
                UnityEngine.Object.Destroy(_doorText.gameObject);
                _doorText = null;
            }
            _signName = null;
            _fasciaBoard = null;
            _fasciaPolice = null;
            _fasciaName = null;
            for (int i = 0; i < _lampGlass.Length; i++)
            {
                _lampGlass[i] = null;
            }
            _built = false;
            _builtModelled = false;
            _nightKnown = false;
        }

        private static void Off(Transform piece)
        {
            if (piece != null && piece.gameObject.activeSelf)
            {
                piece.gameObject.SetActive(false);
            }
        }

        private static void Kill(ref Transform piece)
        {
            if (piece != null)
            {
                UnityEngine.Object.Destroy(piece.gameObject);
                piece = null;
            }
        }

        /// <summary>What went up at the precinct that is dressed, and why each of the rest did not: one line for the log.</summary>
        public string Report()
        {
            if (_dressed == null)
            {
                return "no precinct is dressed";
            }
            var text = new StringBuilder(320);
            text.Append(_dressed.Name).Append(": ");
            if (!_frontFound)
            {
                return text.Append("no pavement in front of the door; nothing is put out").ToString();
            }
            if (!_statics)
            {
                return text.Append("waiting to be put out (the camera is looking at it, or the props are still loading)").ToString();
            }
            for (int i = 0; i < (int)Piece.Count; i++)
            {
                text.Append(i > 0 ? "; " : string.Empty).Append(Name((Piece)i)).Append(_fits[i] ? " up" : " left off (" + _why[i] + ")");
                if ((Piece)i == Piece.Bollards && _fits[i])
                {
                    text.Append(" x").Append(_bollardsOut);
                }
            }
            text.Append("; patrol cars ").Append(CarsOut()).Append(" of ").Append(Cars);
            text.Append("; wall ").Append(_wallKnown ? _wall.ToString("F2", CultureInfo.InvariantCulture) + " m from the door's plane" : "not found");
            return text.ToString();
        }

        private static string Name(Piece piece)
        {
            switch (piece)
            {
                case Piece.Fascia: return "fascia";
                case Piece.Sign: return "standing sign";
                case Piece.Awning: return "canopy";
                case Piece.LampA: return "lamp one";
                case Piece.LampB: return "lamp two";
                case Piece.Emblem: return "shield";
                case Piece.Board: return "notice board";
                case Piece.Door: return "door lettering";
                case Piece.Bollards: return "bollards";
                default: return "flag pole";
            }
        }

        // ---------------------------------------------------------------- measuring the frontage

        // The door's own frame of reference: which way is out, which way the wall runs, where the pavement is, and how
        // far out from the door's plane the wall face stands (most rays either side of the door agree on it).
        private void Measure()
        {
            _measured = true;
            if (_solidMask == 0)
            {
                _solidMask = ~LayerMask.GetMask("Roads", "Ground", "GroundUnplacable", "UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water");
                _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
                if (_wallMask == 0)
                {
                    _wallMask = _solidMask;
                }
            }
            Transform door = Precincts.DoorOf(_dressed.Building);
            Vector3 toFront = _front - _dressed.Door;
            toFront.y = 0f;
            Vector3 outward = door != null ? door.forward : toFront;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = toFront;
            }
            else if (Vector3.Dot(outward, toFront) < 0f)
            {
                outward = -outward;   // the door's own axis, turned to point at the street
            }
            _out = outward.sqrMagnitude < 0.01f ? Vector3.forward : outward.normalized;
            _across = Vector3.Cross(Vector3.up, _out);
            float ground = CitySpots.Ground(_front, out float found) ? found : _front.y;
            _doorGround = new Vector3(_dressed.Door.x, ground, _dressed.Door.z);
            _doorSpot = StreetSpots.OnPavement(_doorGround + _out * 0.7f, 1.5f, out Vector3 step) ? step : _front;

            int count = 0;
            for (int i = 0; i < 6 && count < _depths.Length - 1; i++)
            {
                float aside = (1.4f + (i / 2) * 0.6f) * ((i & 1) == 0 ? -1f : 1f);
                if (Wall(aside, 1.3f, out float low))
                {
                    _depths[count++] = low;
                }
                if (Wall(aside, 2.2f, out float high))
                {
                    _depths[count++] = high;
                }
            }
            int best = 0;
            _wallKnown = false;
            for (int i = 0; i < count; i++)
            {
                int agree = 0;
                for (int j = 0; j < count; j++)
                {
                    agree += Mathf.Abs(_depths[i] - _depths[j]) < 0.12f ? 1 : 0;
                }
                if (agree > best)
                {
                    best = agree;
                    _wall = _depths[i];
                    _wallKnown = agree >= 2;
                }
            }
        }

        // The wall face at a place along the wall and a height: how far out from the door's plane it stands. False if
        // there is no upright wall facing the street there (a gap, a passage, glass with nothing behind it).
        private bool Wall(float aside, float height, out float depth)
        {
            Vector3 from = _doorGround + _across * aside + _out * RayOut + Vector3.up * height;
            if (Physics.Raycast(from, -_out, out RaycastHit hit, RayOut + 1.6f, _wallMask, QueryTriggerInteraction.Ignore) && Vector3.Dot(hit.normal, _out) > 0.88f)
            {
                depth = RayOut - hit.distance;
                return true;
            }
            depth = 0f;
            return false;
        }

        // A flat patch of wall of a size, with a clear box of a depth in front of it and nothing of ours on it already.
        // Gives the middle of the patch on the wall face.
        private bool Patch(float aside, float height, float halfWide, float halfHigh, float proud, out Vector3 at, out string why)
        {
            at = default;
            if (!_wallKnown)
            {
                why = "no wall could be measured beside the door";
                return false;
            }
            if (Taken(aside - halfWide, aside + halfWide, height - halfHigh, height + halfHigh))
            {
                why = "that wall is taken by another piece";
                return false;
            }
            if (!Wall(aside, height, out float depth) || Mathf.Abs(depth - _wall) > 0.6f)
            {
                why = "no wall there";
                return false;
            }
            float inWide = halfWide * 0.9f;
            float inHigh = halfHigh * 0.9f;
            for (int corner = 0; corner < 4; corner++)
            {
                if (!Wall(aside + ((corner & 1) == 0 ? -inWide : inWide), height + (corner < 2 ? -inHigh : inHigh), out float other) || Mathf.Abs(other - depth) > FlatWithin)
                {
                    why = "the wall is not flat there";
                    return false;
                }
            }
            at = _doorGround + _across * aside + _out * depth + Vector3.up * height;
            if (Physics.CheckBox(at + _out * (proud * 0.5f + 0.03f), new Vector3(halfWide, halfHigh, proud * 0.5f), Quaternion.LookRotation(_out, Vector3.up), _solidMask, QueryTriggerInteraction.Ignore))
            {
                why = "something is in the way";
                return false;
            }
            why = null;
            return true;
        }

        private bool Taken(float from, float to, float low, float high)
        {
            for (int i = 0; i < _patches.Count; i++)
            {
                Vector4 patch = _patches[i];
                if (from < patch.y + 0.08f && to > patch.x - 0.08f && low < patch.w + 0.08f && high > patch.z - 0.08f)
                {
                    return true;
                }
            }
            return false;
        }

        private void Take(float aside, float height, float halfWide, float halfHigh)
        {
            _patches.Add(new Vector4(aside - halfWide, aside + halfWide, height - halfHigh, height + halfHigh));
        }

        // ---------------------------------------------------------------- the wall and the kerb

        private void PlaceStatics()
        {
            using var piece = SessionWatch.Piece("law: a precinct's front being put out");
            if (!Shapes.Available)
            {
                _statics = true;
                for (int i = 0; i < _why.Length; i++)
                {
                    _why[i] = "the game's lit shader was not found";
                }
                return;
            }
            if (!_built)
            {
                _built = true;
                _builtModelled = PropLibrary.Ready;
            }
            _statics = true;
            _patches.Clear();
            Quaternion facing = Quaternion.LookRotation(_out, Vector3.up);
            PlaceFascia(facing);
            PlaceSign();
            PlaceAwning(facing);
            PlaceLamps(facing);
            PlaceWallPiece(Piece.Emblem, ref _emblem, EmblemPlaces, 0.42f, 0.42f, 0.07f, facing);
            PlaceWallPiece(Piece.Board, ref _board, BoardPlaces, 0.64f, 0.50f, 0.08f, facing);
            PlaceDoorText(facing);
            PlaceKerb();
            _nightKnown = false;
            Light();
            _logger?.Info("Law: frontage: " + Report() + ".");
        }

        // The building's own wide sign over the shopfront: switched off, and a police board exactly where it was.
        private void PlaceFascia(Quaternion facing)
        {
            HideSigns(out BuildingSignController nearest);
            if (nearest == null)
            {
                Skip(Piece.Fascia, "the building has no sign band of its own");
                return;
            }
            MeshFilter filter = nearest.signMeshRenderer != null ? nearest.signMeshRenderer.GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                Skip(Piece.Fascia, "its sign band has no shape to measure");
                return;
            }
            // The sign's own box, corner by corner, in the door's frame.
            Bounds box = mesh.bounds;
            Transform frame = filter.transform;
            float fromAside = float.MaxValue, toAside = float.MinValue, low = float.MaxValue, high = float.MinValue, back = float.MaxValue, face = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = frame.TransformPoint(box.center + Vector3.Scale(box.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                Vector3 offset = corner - _doorGround;
                float aside = Vector3.Dot(offset, _across);
                float depth = Vector3.Dot(offset, _out);
                fromAside = Mathf.Min(fromAside, aside);
                toAside = Mathf.Max(toAside, aside);
                low = Mathf.Min(low, offset.y);
                high = Mathf.Max(high, offset.y);
                back = Mathf.Min(back, depth);
                face = Mathf.Max(face, depth);
            }
            float wide = toAside - fromAside;
            float tall = high - low;
            if (wide < 1.2f || wide > 16f || tall < 0.25f || tall > 2.4f || low < 1.9f || face - back > 1.2f || Mathf.Abs(fromAside + toAside) * 0.5f > 12f)
            {
                Skip(Piece.Fascia, FormattableString.Invariant($"its sign band measures {wide:F1} x {tall:F1} m, {low:F1} m up: not a board over a shopfront"));
                return;
            }
            if (_fascia == null)
            {
                BuildFascia();
            }
            float thick = Mathf.Max(0.06f, face - back);
            _fascia.gameObject.SetActive(false);
            _fascia.SetPositionAndRotation(_doorGround + _across * ((fromAside + toAside) * 0.5f) + Vector3.up * ((low + high) * 0.5f) + _out * (face - thick * 0.5f), facing);
            _fasciaBoard.localScale = new Vector3(wide, tall, thick);
            float front = thick * 0.5f + 0.006f;
            bool both = wide >= 3.2f;
            if (_fasciaPolice != null)
            {
                _fasciaPolice.rectTransform.sizeDelta = new Vector2(both ? wide * 0.34f : wide * 0.86f, tall * 0.66f);
                _fasciaPolice.transform.localPosition = new Vector3(both ? wide * 0.27f : 0f, 0f, front);   // (x runs to the reader's left)
            }
            if (_fasciaName != null)
            {
                _fasciaName.gameObject.SetActive(both);
                _fasciaName.rectTransform.sizeDelta = new Vector2(wide * 0.50f, tall * 0.42f);
                _fasciaName.transform.localPosition = new Vector3(-wide * 0.20f, 0f, front);
                _fasciaName.text = _dressed.Name.ToUpperInvariant();
            }
            _fascia.gameObject.SetActive(true);
            Take((fromAside + toAside) * 0.5f, (low + high) * 0.5f, wide * 0.5f, tall * 0.5f);
            Fit(Piece.Fascia);
        }

        private void BuildFascia()
        {
            var holder = new GameObject("RealLifeOverhaul Precinct Fascia");
            _fascia = holder.transform;
            _fascia.SetParent(_root, false);
            _fasciaBoard = Shapes.Piece("Board", _fascia, Shapes.Cube, Shapes.Paint(StationProps.PoliceBlue, 0.5f), Vector3.zero, Vector3.one);
            _fasciaPolice = StationProps.Letter(_fascia, "POLICE", Vector3.zero, 1f, 0.5f);
            _fasciaName = StationProps.Letter(_fascia, string.Empty, Vector3.zero, 1f, 0.3f);
            holder.SetActive(false);
        }

        // Every sign the building carries is switched off for the visit (a station is not "for rent"), and the wide one
        // nearest the door is handed back for the fascia to be measured from.
        private void HideSigns(out BuildingSignController nearest)
        {
            nearest = null;
            CityBuildingController building = _dressed.Building;
            if (building == null)
            {
                return;
            }
            float best = float.MaxValue;
            BuildingSignController[] wide = building.GetComponentsInChildren<BuildingSignController>(true);
            for (int i = 0; i < wide.Length; i++)
            {
                BuildingSignController sign = wide[i];
                if (sign == null)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(sign.transform.position - _dressed.Door);
                if (sqr < best && sign.signMeshRenderer != null)
                {
                    best = sqr;
                    nearest = sign;
                }
                if (!_signsOff.Contains(sign))
                {
                    _signsOff.Add(sign);
                }
                if (sign.gameObject.activeSelf)
                {
                    sign.gameObject.SetActive(false);
                }
            }
            BuildingLogoSignController[] logos = building.GetComponentsInChildren<BuildingLogoSignController>(true);
            for (int i = 0; i < logos.Length; i++)
            {
                BuildingLogoSignController logo = logos[i];
                if (logo == null)
                {
                    continue;
                }
                if (!_logosOff.Contains(logo))
                {
                    _logosOff.Add(logo);
                }
                if (logo.gameObject.activeSelf)
                {
                    logo.gameObject.SetActive(false);
                }
            }
        }

        // The game switches a building's signs back on whenever its registration changes: looked at once a second.
        private void KeepSignsOff()
        {
            for (int i = 0; i < _signsOff.Count; i++)
            {
                if (_signsOff[i] != null && _signsOff[i].gameObject.activeSelf)
                {
                    _signsOff[i].gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < _logosOff.Count; i++)
            {
                if (_logosOff[i] != null && _logosOff[i].gameObject.activeSelf)
                {
                    _logosOff[i].gameObject.SetActive(false);
                }
            }
        }

        // The building's signs as the game would have them now: its own call decides which are shown and what they say,
        // and the mod's front for its buildings (Core/FrontSigns) stands after it.
        private void RestoreSigns()
        {
            if (_signsOff.Count == 0 && _logosOff.Count == 0)
            {
                return;
            }
            CityBuildingController building = null;
            for (int i = 0; i < _signsOff.Count && building == null; i++)
            {
                building = _signsOff[i] != null ? _signsOff[i].GetComponentInParent<CityBuildingController>() : null;
            }
            for (int i = 0; i < _logosOff.Count && building == null; i++)
            {
                building = _logosOff[i] != null ? _logosOff[i].GetComponentInParent<CityBuildingController>() : null;
            }
            _signsOff.Clear();
            _logosOff.Clear();
            try
            {
                if (building != null && building.buildingRegistration != null && !GameManager.isCitySceneBeingUnloaded)
                {
                    FrontSigns.Refresh(building);   // the game's own call, then the station's front as the mod keeps it (Core/FrontSigns: its signs off, never "For rent")
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // The standing sign: against the wall a step or two to one side of the door, facing the pavement, where nothing else stands.
        private void PlaceSign()
        {
            if (!CitySpots.Along(_front, out Vector3 along))
            {
                along = _across;
            }
            string why = "no clear stretch of wall beside the door";
            for (int attempt = 0; attempt < SignAside.Length * 2; attempt++)
            {
                float aside = SignAside[attempt / 2] * ((attempt & 1) == 0 ? -1f : 1f);
                // Measured 0.57 m out from the wall with 0.6 m of room: the strip the sign stands in is inside what was found clear.
                if (!CitySpots.Shopfront(_front + along * aside, SignFromWall + 0.45f, 0.6f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                Vector3 at = stand - outward * 0.45f;
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || CitySpots.Flat(at - _dressed.Door) < 1.3f * 1.3f)
                {
                    continue;
                }
                at.y = ground;
                float where = Vector3.Dot(at - _doorGround, _across);
                if (!SignClear(at, outward) || Taken(where - 0.95f, where + 0.95f, 0f, 3.0f))
                {
                    continue;
                }
                if (_sign == null)
                {
                    BuildSign();
                }
                _sign.gameObject.SetActive(false);
                _sign.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
                if (_signName != null)
                {
                    _signName.text = _dressed.Name.ToUpperInvariant();
                }
                _sign.gameObject.SetActive(true);
                _signOut = true;
                _signAt = at;
                _signAcross = Vector3.Cross(Vector3.up, outward);
                Take(where, 1.5f, 0.95f, 1.5f);
                Fit(Piece.Sign);
                return;
            }
            Skip(Piece.Sign, why);
        }

        // The whole of the sign, not just the middle of it: the board (2.05 m up to 2.85 m, over everybody's head: an awning, a lamp bracket, a shop's
        // own sign), each post from the ground up, and the lamp on top. A flat wall 0.12 m behind it is not touched by any of them.
        private bool SignClear(Vector3 at, Vector3 outward)
        {
            Vector3 across = Vector3.Cross(Vector3.up, outward);
            if (Physics.CheckBox(at + Vector3.up * 2.45f, new Vector3(0.84f, 0.42f, 0.045f), Quaternion.LookRotation(outward, Vector3.up), _solidMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 post = at + across * (side * 0.86f);
                if (Physics.CheckCapsule(post + Vector3.up * 0.2f, post + Vector3.up * 3.1f, 0.05f, _solidMask, QueryTriggerInteraction.Ignore)
                    || !CitySpots.Ground(post, out float ground) || Mathf.Abs(ground - at.y) > 0.12f)
                {
                    return false;   // something in the way of a post, or the ground steps there
                }
            }
            return true;
        }

        // The modelled sign if the prop bundle has one; otherwise a blue board on two posts with the uniform's
        // POLICE panel on it at five times its size, the precinct's name under it, and a lamp on one post.
        private void BuildSign()
        {
            var holder = new GameObject("RealLifeOverhaul Precinct Sign");
            _sign = holder.transform;
            _sign.SetParent(_root, false);
            if (!Shapes.Prop(StationProps.Sign, _sign))
            {
                Material steel = Shapes.Paint(StationProps.Steel);
                Shapes.Piece("Post L", _sign, Shapes.Cube, steel, new Vector3(-0.86f, 1.45f, 0f), new Vector3(0.07f, 2.90f, 0.07f));
                Shapes.Piece("Post R", _sign, Shapes.Cube, steel, new Vector3(0.86f, 1.45f, 0f), new Vector3(0.07f, 2.90f, 0.07f));
                Shapes.Piece("Board", _sign, Shapes.Cube, Shapes.Paint(StationProps.PoliceBlue), new Vector3(0f, 2.45f, 0f), new Vector3(1.65f, 0.80f, 0.05f));
                Shapes.Piece("Band", _sign, Shapes.Cube, Shapes.Paint(StationProps.White), new Vector3(0f, 2.27f, 0.002f), new Vector3(1.65f, 0.025f, 0.05f));
                // The lettering: the panel faces -Z as made (for a back), so it is turned about to face the pavement.
                if (PropLibrary.TryGet("back_panel_police", out Mesh letters, out Material lettering) && letters != null && lettering != null)
                {
                    Transform panel = Shapes.Piece("POLICE", _sign, letters, lettering, new Vector3(0f, 2.56f, 0.028f), new Vector3(5f, 5f, 1f));
                    panel.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
                else
                {
                    StationProps.Letter(_sign, "POLICE", new Vector3(0f, 2.56f, 0.027f), 1.3f, 0.46f);
                }
            }
            var socket = new GameObject("Lamp");
            socket.transform.SetParent(_sign, false);
            socket.transform.localPosition = new Vector3(0.86f, 2.90f, 0f);
            if (!Shapes.Prop(StationProps.Lamp, socket.transform))
            {
                _lampGlass[2] = StationProps.LampStandIn(socket.transform);
            }
            _signName = StationProps.Letter(_sign, string.Empty, new Vector3(0f, 2.15f, 0.027f), 1.5f, 0.17f);
            holder.SetActive(false);
        }

        // A canopy over the door: only on flat wall above it, with its whole volume clear, and never across the fascia.
        private void PlaceAwning(Quaternion facing)
        {
            const float under = 2.5f;
            if (_awning == null)
            {
                _awning = StationProps.Holder("RealLifeOverhaul Precinct Canopy", _root, StationProps.Awning, Pivot.Wall, new Vector3(2.4f, 0.5f, 1.1f), out _, out bool modelled);
                if (!modelled)
                {
                    // The stand-in hangs from its underside's back edge; a fitted model from the middle of its back face.
                    var hung = new GameObject("Hung");
                    hung.transform.SetParent(_awning, false);
                    hung.transform.localPosition = new Vector3(0f, -0.25f, 0f);
                    StationProps.AwningStandIn(hung.transform);
                }
            }
            if (!Patch(0f, under + 0.25f, 1.22f, 0.27f, 1.12f, out Vector3 at, out string why))
            {
                Skip(Piece.Awning, why);
                return;
            }
            // Clear above the heads of people coming out, and not over the road.
            if (!StreetSpots.OnPavement(_doorGround + _out * (_wall + 1.0f), 0.8f, out _))
            {
                Skip(Piece.Awning, "the pavement is not that deep at the door");
                return;
            }
            _awning.gameObject.SetActive(false);
            _awning.SetPositionAndRotation(at, facing);
            _awning.gameObject.SetActive(true);
            Take(0f, under + 0.25f, 1.24f, 0.30f);
            Fit(Piece.Awning);
        }

        // Twin lamps on brackets either side of the door: the same distance out on both sides if the wall allows.
        private void PlaceLamps(Quaternion facing)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_lamps[i] == null)
                {
                    var holder = new GameObject("RealLifeOverhaul Precinct Lamp " + (i + 1));
                    _lamps[i] = holder.transform;
                    _lamps[i].SetParent(_root, false);
                    Transform socket = StationProps.LampBracket(_lamps[i]);
                    if (!Shapes.Prop(StationProps.Lamp, socket))
                    {
                        _lampGlass[i] = StationProps.LampStandIn(socket);
                    }
                    holder.SetActive(false);
                }
            }
            const float height = 2.2f;
            // Together first, then each by itself.
            for (int pass = 0; pass < 2 && !(_fits[(int)Piece.LampA] && _fits[(int)Piece.LampB]); pass++)
            {
                for (int step = 0; step < LampAside.Length; step++)
                {
                    float aside = LampAside[step];
                    bool left = Patch(-aside, height + 0.08f, 0.13f, 0.22f, 0.36f, out Vector3 leftAt, out string leftWhy);
                    bool right = Patch(aside, height + 0.08f, 0.13f, 0.22f, 0.36f, out Vector3 rightAt, out string rightWhy);
                    if (pass == 0 && !(left && right))
                    {
                        _why[(int)Piece.LampA] = leftWhy ?? rightWhy;
                        _why[(int)Piece.LampB] = rightWhy ?? leftWhy;
                        continue;
                    }
                    if (left && !_fits[(int)Piece.LampA])
                    {
                        PutLamp(0, leftAt - Vector3.up * 0.08f, facing, -aside, height);
                        Fit(Piece.LampA);
                    }
                    if (right && !_fits[(int)Piece.LampB])
                    {
                        PutLamp(1, rightAt - Vector3.up * 0.08f, facing, aside, height);
                        Fit(Piece.LampB);
                    }
                    if (_fits[(int)Piece.LampA] && _fits[(int)Piece.LampB])
                    {
                        break;
                    }
                }
            }
        }

        private void PutLamp(int index, Vector3 at, Quaternion facing, float aside, float height)
        {
            _lamps[index].gameObject.SetActive(false);
            _lamps[index].SetPositionAndRotation(at, facing);
            _lamps[index].gameObject.SetActive(true);
            Take(aside, height + 0.08f, 0.13f, 0.22f);
        }

        private void PlaceWallPiece(Piece piece, ref Transform holder, Vector2[] places, float halfWide, float halfHigh, float proud, Quaternion facing)
        {
            if (holder == null)
            {
                if (piece == Piece.Emblem)
                {
                    holder = StationProps.Holder("RealLifeOverhaul Precinct Shield", _root, StationProps.Emblem, Pivot.Wall, new Vector3(0.8f, 0.8f, 0.05f), out _, out bool modelled);
                    if (!modelled)
                    {
                        StationProps.EmblemStandIn(holder);
                    }
                }
                else
                {
                    holder = StationProps.Holder("RealLifeOverhaul Precinct Notices", _root, StationProps.NoticeBoard, Pivot.Wall, new Vector3(1.2f, 0.9f, 0.05f), out _, out bool modelled);
                    if (!modelled)
                    {
                        StationProps.NoticeBoardStandIn(holder);
                    }
                }
            }
            string why = "no flat wall";
            for (int i = 0; i < places.Length; i++)
            {
                if (!Patch(places[i].x, places[i].y, halfWide, halfHigh, proud, out Vector3 at, out string reason))
                {
                    why = reason;
                    continue;
                }
                holder.gameObject.SetActive(false);
                holder.SetPositionAndRotation(at + _out * 0.004f, facing);
                holder.gameObject.SetActive(true);
                Take(places[i].x, places[i].y, halfWide, halfHigh);
                Fit(piece);
                return;
            }
            Skip(piece, why);
        }

        // POLICE on the door: only where the rays find the door itself, flat, a little behind the wall face at most.
        private void PlaceDoorText(Quaternion facing)
        {
            if (!_wallKnown || !Wall(0f, 1.55f, out float depth) || depth > _wall + 0.1f || depth < _wall - 0.8f
                || !Wall(-0.28f, 1.55f, out float left) || !Wall(0.28f, 1.55f, out float right) || Mathf.Abs(left - depth) > 0.04f || Mathf.Abs(right - depth) > 0.04f)
            {
                Skip(Piece.Door, "the door's face could not be found flat");
                return;
            }
            if (_doorText == null)
            {
                _doorText = StationProps.Letter(_root, "POLICE", Vector3.zero, 0.74f, 0.15f);
                if (_doorText == null)
                {
                    Skip(Piece.Door, "no font to letter it in");
                    return;
                }
                _doorText.gameObject.name = "RealLifeOverhaul Precinct Door";
            }
            _doorText.gameObject.SetActive(false);
            // Read from the street: the text's own forward points into the wall.
            _doorText.transform.SetPositionAndRotation(_doorGround + _out * (depth + 0.012f) + Vector3.up * 1.55f, Quaternion.LookRotation(-_out, Vector3.up));
            _doorText.gameObject.SetActive(true);
            Fit(Piece.Door);
        }

        // The kerb: bollards either side of the way in and a flag pole further along, each on the strip between where
        // people walk and the road, on level ground, touching nothing.
        private void PlaceKerb()
        {
            _bollardsOut = 0;
            string why = "no kerb found in front of the door";
            for (int i = 0; i < Bollards; i++)
            {
                if (_bollards[i] == null)
                {
                    _bollards[i] = StationProps.Holder("RealLifeOverhaul Precinct Bollard " + (i + 1), _root, StationProps.Bollard, Pivot.Floor, new Vector3(0.16f, 0.9f, 0.16f), out _, out bool modelled);
                    if (!modelled)
                    {
                        StationProps.BollardStandIn(_bollards[i]);
                    }
                    StationProps.Solid(_bollards[i], new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 1f, 0.22f));
                }
                if (!Kerb(BollardAside[i], 0.9f, 0.12f, out Vector3 at, out string reason))
                {
                    why = reason;
                    continue;
                }
                _bollards[i].gameObject.SetActive(false);
                _bollards[i].SetPositionAndRotation(at, Quaternion.LookRotation(_out, Vector3.up));
                _bollards[i].gameObject.SetActive(true);
                MeshCuts.Note("a precinct's bollards and flag pole");
                _bollardsOut++;
            }
            if (_bollardsOut > 0)
            {
                Fit(Piece.Bollards);
            }
            else
            {
                Skip(Piece.Bollards, why);
            }

            if (_pole == null)
            {
                _pole = StationProps.Holder("RealLifeOverhaul Precinct Flag", _root, StationProps.FlagPole, Pivot.Own, new Vector3(1.3f, 5.1f, 0.3f), out _, out bool modelled);
                if (!modelled)
                {
                    StationProps.FlagPoleStandIn(_pole);
                }
                StationProps.Solid(_pole, new Vector3(0f, 1f, 0f), new Vector3(0.3f, 2f, 0.3f));
            }
            why = "no kerb found for it";
            for (int i = 0; i < PoleAside.Length; i++)
            {
                if (!Kerb(PoleAside[i], 5.3f, 0.16f, out Vector3 at, out string reason))
                {
                    why = reason;
                    continue;
                }
                // The flag flies along the kerb, away from the door; its whole sweep must be clear as well.
                Vector3 fly = _across * (PoleAside[i] > 0f ? 1f : -1f);
                if (Physics.CheckBox(at + fly * 0.65f + Vector3.up * 4.5f, new Vector3(0.7f, 0.5f, 0.12f), Quaternion.LookRotation(_out, Vector3.up), _solidMask, QueryTriggerInteraction.Ignore))
                {
                    why = "something is in the way of the flag";
                    continue;
                }
                _pole.gameObject.SetActive(false);
                _pole.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.Cross(fly, Vector3.up), Vector3.up));   // (the flag flies along the holder's +X)
                _pole.gameObject.SetActive(true);
                MeshCuts.Note("a precinct's bollards and flag pole");
                Fit(Piece.Pole);
                return;
            }
            Skip(Piece.Pole, why);
        }

        // The kerb strip at a place along the frontage: just beyond the edge of where people walk, with the road beyond it.
        private bool Kerb(float aside, float height, float radius, out Vector3 at, out string why)
        {
            at = default;
            if (!StreetSpots.OnPavement(_front + _across * aside, 0.7f, out Vector3 start))
            {
                why = "no pavement there";
                return false;
            }
            if (!NavMesh.Raycast(start, start + _out * 9f, out NavMeshHit edge, StreetSpots.Pavement))
            {
                why = "the pavement runs on (a square, not a kerb)";
                return false;
            }
            Vector3 beyond = edge.position + _out * 1.1f;
            if (NavMesh.SamplePosition(beyond, out _, 0.35f, StreetSpots.Pavement) || !StreetSpots.OnRoad(beyond, 0.6f, out _))
            {
                why = "what the pavement ends at there is not the road";
                return false;
            }
            at = edge.position + _out * 0.06f;
            if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - _doorGround.y) > 0.4f)
            {
                why = "the ground steps there";
                return false;
            }
            at.y = ground;
            if (Physics.CheckCapsule(at + Vector3.up * (radius + 0.12f), at + Vector3.up * height, radius, _solidMask, QueryTriggerInteraction.Ignore))
            {
                why = "something stands there already";
                return false;
            }
            why = null;
            return true;
        }

        private void Fit(Piece piece)
        {
            _fits[(int)piece] = true;
            _why[(int)piece] = null;
        }

        private void Skip(Piece piece, string why)
        {
            _fits[(int)piece] = false;
            _why[(int)piece] = why;
        }

        // Dusk and dawn: the lamps' glass changes paint (two shared paints; nothing is made here). Looked at once a second.
        private void Light()
        {
            bool night;
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                night = gm != null && gm.timeOfDayController != null ? gm.timeOfDayController.ShouldOutdoorLightsBeOn() : Crime.IsNight;
            }
            catch (Exception)
            {
                night = Crime.IsNight;
            }
            if (_nightKnown && night == _night)
            {
                return;
            }
            _nightKnown = true;
            _night = night;
            Material paint = StationProps.LampPaint(night);
            for (int i = 0; i < _lampGlass.Length; i++)
            {
                if (_lampGlass[i] != null && paint != null)
                {
                    _lampGlass[i].sharedMaterial = paint;
                }
            }
        }

        // ---------------------------------------------------------------- the patrol cars

        private int CarsOut()
        {
            int count = 0;
            for (int i = 0; i < Cars; i++)
            {
                count += _carOut[i] ? 1 : 0;
            }
            return count;
        }

        private int FreeCar()
        {
            for (int i = 0; i < Cars; i++)
            {
                if (!_carOut[i])
                {
                    return i;
                }
            }
            return -1;
        }

        // Along the lane that passes the door, both ways: the first kerb places that pass every test.
        private void PlaceCars(bool force)
        {
            TrafficManager traffic = TrafficManager.Instance;
            if (traffic == null)
            {
                return;
            }
            Waypoint start = TrafficTools.ClosestWaypoint(_front, 22f);
            if (start == null)
            {
                return;
            }
            for (int way = 0; way < 2 && FreeCar() >= 0; way++)
            {
                Waypoint at = start;
                for (int step = 0; step < 16 && at != null && FreeCar() >= 0; step++)
                {
                    if (CitySpots.Flat(at.position - _dressed.Door) > CarWithin * CarWithin)
                    {
                        break;
                    }
                    if ((way == 0 || step > 0) && IsPlainLane(traffic, at))
                    {
                        TryCar(traffic, at, force);
                    }
                    List<int> next = way == 0 ? at.neighbors : at.prev;
                    at = next != null && next.Count == 1 ? traffic.GetWaypoint(next[0]) : null;
                }
            }
        }

        private void TryCar(TrafficManager traffic, Waypoint point, bool force)
        {
            int slot = FreeCar();
            if (slot < 0 || (_cars[slot] == null && !BuildCar(slot)))
            {
                return;
            }
            if (!FindKerbPlace(traffic, point, out Vector3 at, out Quaternion facing))
            {
                return;
            }
            // Clear of the others by more than a car's length, and never while somebody is looking at the spot.
            float apart = _halfLength * 2f + 1.4f;
            for (int other = 0; other < Cars; other++)
            {
                if (other != slot && _carOut[other] && CitySpots.Flat(at - _carAt[other]) < apart * apart)
                {
                    return;
                }
            }
            if (!force && StreetSpots.IsInView(at))
            {
                return;
            }
            GameObject car = _cars[slot];
            car.SetActive(false);   // moved while off, so the navigation mesh is cut once, in the right place
            car.transform.SetPositionAndRotation(at, facing);
            car.SetActive(true);
            MeshCuts.Note("a precinct's parked car");
            _carOut[slot] = true;
            _carAt[slot] = at;
            _logger?.Info($"Law: {_dressed.Name}: patrol car {slot + 1} parked at the kerb {Mathf.Sqrt(CitySpots.Flat(at - _dressed.Door)):F0} m from the door.");
        }

        // A stretch of lane with nothing about it: one way in, one way on, open, not in a junction, and the same for its neighbours.
        private static bool IsPlainLane(TrafficManager traffic, Waypoint point)
        {
            for (int way = 0; way < 2; way++)
            {
                Waypoint at = point;
                for (int step = 0; step < 3; step++)
                {
                    if (at == null || at.temporaryDisabled || at.IsInIntersection() || at.neighbors == null || at.neighbors.Count != 1
                        || at.prev == null || at.prev.Count != 1)
                    {
                        return false;
                    }
                    at = traffic.GetWaypoint(way == 0 ? at.neighbors[0] : at.prev[0]);
                }
            }
            return true;
        }

        // Beside a lane, on its kerb side: the nearest place out from the lane's middle that passes every test
        // (the same tests People/PolicePresence parks its patrol car by), and clear of the bollards.
        private bool FindKerbPlace(TrafficManager traffic, Waypoint point, out Vector3 at, out Quaternion facing)
        {
            at = default;
            facing = Quaternion.identity;
            Waypoint next = traffic.GetWaypoint(point.neighbors[0]);
            Vector3 along = next != null ? next.position - point.position : Vector3.zero;
            along.y = 0f;
            if (along.sqrMagnitude < 0.25f)
            {
                return false;
            }
            along.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, along);
            facing = Quaternion.LookRotation(along, Vector3.up);
            for (int i = 0; i < KerbOffsets.Length; i++)
            {
                float outward = Mathf.Max(KerbOffsets[i], LaneGap + _halfWidth);
                Vector3 place = point.position + right * outward;
                if (!StreetSpots.Ground(place, out float ground))
                {
                    continue;
                }
                place.y = ground;
                // On the road, with no corner on a pavement or a crossing, touching nothing, in no other lane,
                // and with a pavement beside it (so it is the kerb and not the middle of the road).
                if (!StreetSpots.OnRoad(place, 0.5f, out _)
                    || OnPavement(place) || OnPavement(place + along * _halfLength + right * _halfWidth) || OnPavement(place + along * _halfLength - right * _halfWidth)
                    || OnPavement(place - along * _halfLength + right * _halfWidth) || OnPavement(place - along * _halfLength - right * _halfWidth)
                    || !SceneParking.IsFree(place, facing, _halfWidth, _halfLength, Room)
                    || TrafficTools.ClosestWaypoint(place, OtherLane) != null
                    || !StreetSpots.OnPavement(place + right * (_halfWidth + 1.3f), 1.6f, out _)
                    || NearKerbPiece(place, along, right))
                {
                    continue;
                }
                at = place;
                return true;
            }
            return false;
        }

        // The bollards and the flag pole have no collider: a car is kept clear of them by measuring.
        private bool NearKerbPiece(Vector3 place, Vector3 along, Vector3 right)
        {
            for (int i = 0; i <= Bollards; i++)
            {
                Transform piece = i < Bollards ? _bollards[i] : _pole;
                if (piece == null || !piece.gameObject.activeSelf)
                {
                    continue;
                }
                Vector3 offset = piece.position - place;
                if (Mathf.Abs(Vector3.Dot(offset, along)) < _halfLength + 0.4f && Mathf.Abs(Vector3.Dot(offset, right)) < _halfWidth + 0.4f)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool OnPavement(Vector3 point)
        {
            return NavMesh.SamplePosition(point, out _, 0.3f, StreetSpots.Pavement);
        }

        // Once each: the game's police car prop with a box round it, kept switched off between visits.
        private bool BuildCar(int slot)
        {
            var car = new GameObject("RealLifeOverhaul Precinct Patrol " + (slot + 1));
            car.transform.SetParent(_root, false);
            car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GameObject body = TrafficEffects.CreateParkedPatrol(car.transform, out Vector3 size);
            if (body == null)
            {
                UnityEngine.Object.Destroy(car);
                _carsFailed = true;
                _logger?.Info("Law: the game's police car prop could not be had; the precincts go without parked patrol cars.");
                return false;
            }
            // The prop may be modelled along either axis: its long side is its length.
            _halfWidth = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.5f, 0.8f, 1.3f);
            _halfLength = Mathf.Clamp(Mathf.Max(size.x, size.z) * 0.5f, 1.8f, 3.2f);
            if (size.x > size.z)
            {
                body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * body.transform.localRotation;
                body.transform.localPosition = Quaternion.Euler(0f, 90f, 0f) * body.transform.localPosition;
            }
            // Solid for the player's car and for anything that looks for parked vehicles, and for people on foot.
            int layer = LayerMask.NameToLayer("ParkedVehicles");
            if (layer >= 0)
            {
                car.layer = layer;
                var box = car.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, Mathf.Max(0.6f, size.y * 0.5f), 0f);
                box.size = new Vector3(_halfWidth * 2f, Mathf.Max(1.2f, size.y), _halfLength * 2f);
            }
            var cut = car.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3(0f, 1f, 0f);
            cut.size = new Vector3(_halfWidth * 2f + 0.3f, 2f, _halfLength * 2f + 0.3f);
            cut.carving = true;
            cut.carveOnlyStationary = true;
            car.SetActive(false);
            _cars[slot] = car;
            return true;
        }

        // ---------------------------------------------------------------- the people

        /// <summary>Everybody of the frontage off the street (the player has gone indoors: the same few characters are wanted inside).</summary>
        public void PeopleIn()
        {
            EndAct();
            for (int i = 0; i < _officers.Length; i++)
            {
                _officers[i].Exit();
            }
        }

        // One either side of the door, a step out from the wall, looking at the street; and the comings and goings. They are
        // people the pool may take back at any moment: whoever is gone is put out again later, out of view.
        private void TickPeople(float now, Vector3 player, bool force)
        {
            for (int i = 0; i < _officers.Length; i++)
            {
                _officers[i].Tick(now);
            }
            _mover.Tick(now);
            _suspect.Tick(now);
            float sqr = CitySpots.Flat(_front - player);
            bool near = sqr < OfficersWithin * OfficersWithin;
            if (!near || !Settings.People || !Settings.Police)
            {
                if (_act != Act.None && (!near || !_mover.Visible))
                {
                    EndAct();
                }
                for (int i = 0; i < _officers.Length; i++)
                {
                    if (_officers[i].Present && !_officers[i].Visible)
                    {
                        _officers[i].Exit();
                    }
                }
                return;
            }
            TickAct(now, sqr);
            if (now < _nextOfficers && !force)
            {
                return;
            }
            _nextOfficers = now + 12f;
            for (int i = 0; i < _officers.Length; i++)
            {
                if (_officers[i].Present)
                {
                    continue;
                }
                // The second one is not always there.
                if (i == 1 && (_dressed.Index + TimeHelper.CurrentDay) % 3 == 0)
                {
                    continue;
                }
                Vector3 place = _front + _across * (i == 0 ? -1.5f : 1.5f) - _out * 0.2f;
                if (!CitySpots.Stand(place, 0.45f, out Vector3 spot) || CitySpots.Flat(spot - player) < 1.2f * 1.2f || AtSign(spot) || (!force && StreetSpots.IsInView(spot)))
                {
                    continue;
                }
                if (!_officers[i].EnterPresence(spot, ActorLook.Officer, 0.6f, onRoad: false))
                {
                    return;   // the pool has nobody to spare just now
                }
                _officers[i].Stand(spot + _out * 8f);
                string clip = ClipLibrary.Has(GuardClip) ? GuardClip : ClipLibrary.Has("guard_stand") ? "guard_stand" : null;
                if (clip != null)
                {
                    _officers[i].Work(clip);   // standing easy by the door; without either clip they simply stand
                }
                return;       // one new character per look
            }
        }

        // The comings and goings, one at a time, on a slow timer: an officer out to a patrol car and back in; an officer
        // in off the street; a suspect walked in in handcuffs. Whoever goes in through the door is gone at the door, as
        // the game's own shoppers are; whoever comes from the street starts out of view.
        private void TickAct(float now, float playerSqr)
        {
            switch (_act)
            {
                case Act.None:
                    if (now >= _nextAct)
                    {
                        _nextAct = now + UnityEngine.Random.Range(55f, 95f);
                        StartAct(now, playerSqr);
                    }
                    break;

                case Act.OutToCar:
                    if (!_mover.Present)
                    {
                        EndAct();
                    }
                    else if (_mover.Settled || now >= _actUntil)
                    {
                        _mover.Stand(_carAt[_actCar]);
                        _act = Act.AtCar;
                        _actUntil = now + UnityEngine.Random.Range(14f, 22f);
                    }
                    break;

                case Act.AtCar:
                    if (!_mover.Present)
                    {
                        EndAct();
                    }
                    else if (now >= _actUntil)
                    {
                        _mover.Go(_doorSpot, Pace.Walk, _dressed.Door, 45f);
                        _act = Act.BackIn;
                        _actUntil = now + 46f;
                    }
                    break;

                case Act.BackIn:
                case Act.WalkIn:
                    if (!_mover.Present || _mover.Settled || now >= _actUntil)
                    {
                        // At the door: gone inside. (Held up on the way and out of time: taken off only if nobody is looking.)
                        if (!_mover.Present || CitySpots.Flat(_mover.Position - _doorSpot) < 2.2f * 2.2f || !_mover.Visible)
                        {
                            EndAct();
                        }
                        else
                        {
                            _mover.WalkAwayFrom(_front, Pace.Walk);
                            _act = Act.None;
                        }
                    }
                    break;

                case Act.Suspect:
                    if (!_suspect.Present || !_mover.Present || _suspect.Settled || now >= _actUntil)
                    {
                        if (!_suspect.Present || !_mover.Present || CitySpots.Flat(_suspect.Position - _doorSpot) < 2.4f * 2.4f || !_suspect.Visible)
                        {
                            EndAct();
                        }
                        else
                        {
                            _actUntil = now + 10f;   // held up in view of the player: a little longer, then whenever nobody is looking
                            _suspect.Go(_doorSpot, Pace.Walk, _dressed.Door, 12f);
                        }
                    }
                    break;
            }
        }

        private void StartAct(float now, float playerSqr)
        {
            float roll = UnityEngine.Random.value;
            int car = -1;
            for (int i = 0; i < Cars; i++)
            {
                if (_carOut[i] && (car < 0 || CitySpots.Flat(_carAt[i] - _doorGround) < CitySpots.Flat(_carAt[car] - _doorGround)))
                {
                    car = i;
                }
            }
            if (roll < 0.42f && car >= 0 && StreetSpots.OnPavement(_carAt[car], 3.6f, out Vector3 byCar) && CitySpots.Reachable(_doorSpot, byCar)
                && CitySpots.ClearQuietly(_doorSpot, 0.4f))
            {
                // Out of the door, over to the car, a word on the radio, back in.
                if (_mover.EnterPresence(_doorSpot, ActorLook.Officer, 0.5f, onRoad: false))
                {
                    _mover.Go(byCar, Pace.Walk, _carAt[car], 45f);
                    _mover.Work(SceneClips.RadioTalk);
                    _act = Act.OutToCar;
                    _actCar = car;
                    _actUntil = now + 46f;
                }
                return;
            }
            if (!StreetSpots.PavementAround(_front, 28f, 46f, true, out Vector3 from) || StreetSpots.IsInView(from) || !CitySpots.Reachable(from, _doorSpot))
            {
                return;
            }
            bool withSuspect = roll > 0.78f && playerSqr < SuspectWithin * SuspectWithin && ClipLibrary.Has(SceneClips.CuffedWalk) && CuffPose.Instance != null;
            if (!withSuspect)
            {
                if (_mover.EnterPresence(from, ActorLook.Officer, 1f, onRoad: false))
                {
                    _mover.Go(_doorSpot, Pace.Walk, _dressed.Door, 70f);
                    _act = Act.WalkIn;
                    _actUntil = now + 72f;
                }
                return;
            }
            // A suspect brought in: somebody in handcuffs walking ahead, the officer a pace behind. The suspect is not one
            // of the people the pool may take back (the cuffs would go with them), so this is a scene of two for a minute.
            Vector3 back = from - _doorSpot;
            back.y = 0f;
            back = back.sqrMagnitude < 0.01f ? -_out : back.normalized;
            if (!_suspect.Enter(from, ActorLook.Civilian, 1f, onRoad: false))
            {
                return;
            }
            ThirdPersonCharacter body = _suspect.Character;
            if (body == null || !CuffPose.Put(body) || !_suspect.Gait(SceneClips.CuffedWalk, SceneClips.WalkPace, SceneClips.CuffedStand)
                || !_mover.EnterPresence(from + back * 1.1f, ActorLook.Officer, 1f, onRoad: false))
            {
                if (body != null)
                {
                    CuffPose.Remove(body);
                }
                _suspect.Gait(null);
                _suspect.Exit();
                return;
            }
            _cuffed = body;
            _suspect.Go(_doorSpot, Pace.Walk, _dressed.Door, 75f);
            _mover.Go(_doorSpot + back * 1.0f, Pace.Walk, _dressed.Door, 75f);
            _act = Act.Suspect;
            _actUntil = now + 78f;
            _logger?.Info("Law: " + _dressed.Name + ": a suspect is being walked in.");
        }

        // Whatever is going on at the door is over: the cuffs come off whoever wore them, and both are off the street.
        private void EndAct()
        {
            try
            {
                if (_cuffed != null)
                {
                    CuffPose.Remove(_cuffed);
                    _cuffed = null;
                    _suspect.Gait(null);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _suspect.Exit();
            _mover.Exit();
            _act = Act.None;
        }

        // True if a place is within arm's reach of the sign's posts and board (they have no collider: this is what keeps people out of them).
        private bool AtSign(Vector3 spot)
        {
            if (!_signOut)
            {
                return false;
            }
            Vector3 offset = spot - _signAt;
            float along = Mathf.Clamp(Vector3.Dot(offset, _signAcross), -0.9f, 0.9f);
            return CitySpots.Flat(offset - _signAcross * along) < 0.75f * 0.75f;
        }
    }
}
