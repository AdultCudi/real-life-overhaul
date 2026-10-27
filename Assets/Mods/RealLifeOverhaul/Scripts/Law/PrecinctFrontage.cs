using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// What makes a designated building look like a police station from the street: one set of pieces, built once and moved
    /// to the precinct the player is near, each measured against the building first and left off, named in the log, where
    /// it does not fit. Nothing is put out or taken in where the camera looks, except under a dark screen. Called once a
    /// second by <see cref="Precincts"/>; nothing here runs by itself. Dressed within 260 m and taken in beyond 330 m: in
    /// between, beyond the game's far band, it is not drawn (Performance/ModCulling).
    /// </summary>
    internal sealed partial class PrecinctFrontage
    {
        private const float SignFromWall = 0.12f;    // metres: nobody walking along the wall is ever that near it
        private const int CarSearches = 8;           // kerb searches a visit, six seconds apart
        private const int Cars = 3;
        private const int Bollards = 6;
        private const float RayOut = 1.4f;           // metres out from the door's plane the wall rays start
        private const float FlatWithin = 0.06f;      // metres the rays of one flat patch agree to
        private const float CullRadius = 50f;        // metres round the door: the pieces and the patrol cars parked within 48 m of it

        // Metres along the wall from the door, and up it, tried in turn.
        private static readonly float[] SignAside = { 2.3f, 3.3f, 1.7f, 4.2f };
        private static readonly float[] LampAside = { 1.15f, 1.4f, 1.7f };
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
        private readonly MeshRenderer[] _lampGlass = new MeshRenderer[3];   // the two on the wall and the sign's (stand-ins only)
        private readonly List<Vector4> _patches = new List<Vector4>(12);    // wall taken: from, to along it; from, to up it
        private readonly List<BuildingSignController> _signsOff = new List<BuildingSignController>(4);
        private readonly List<BuildingLogoSignController> _logosOff = new List<BuildingLogoSignController>(4);
        private readonly string[] _why = new string[(int)Piece.Count];
        private readonly bool[] _fits = new bool[(int)Piece.Count];
        private readonly float[] _depths = new float[12];

        private Transform _sign;
        private CullEntry _cull;                     // the pieces and cars, not drawn beyond the far band (Performance/ModCulling)
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
        private Vector3 _front;          // on the pavement in front of the door
        private Vector3 _out;            // from the door towards the street
        private Vector3 _across;         // along the wall
        private Vector3 _doorGround;     // the door at the pavement's height
        private Vector3 _doorSpot;       // where somebody steps out of or into the door
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

        public PrecinctFrontage(Transform root, IModLogger logger)
        {
            _root = new GameObject("RealLifeOverhaul Precinct Front").transform;   // its own holder: only the front is ever culled
            _root.SetParent(root, false);
            _logger = logger;
        }

        public Precinct Dressed => _dressed;

        /// <summary>Once a second while the player is out of doors near this precinct.</summary>
        public void Tick(float now, Vector3 player, Precinct near)
        {
            if (_dressed != near)
            {
                Undress();
                Begin(near);
            }
            Dress(now, player, force: false);
        }

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
            // Stand-ins built before the prop bundle loaded give way to the models.
            if (_built && !_builtModelled && PropLibrary.Ready)
            {
                DestroyPieces();
            }
        }

        private void Dress(float now, Vector3 player, bool force)
        {
            if (!_frontFound)
            {
                // A parked car may have cut the pavement away: looked for again now and then.
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
                // The prop bundle is given a few seconds to load before stand-ins are built.
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

        /// <summary>Everything off the street, and the building's own signs as the game has them. Safe at any time.</summary>
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
                ModCulling.Remove(_cull);
                _cull = null;
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
                    SceneryTrees.Destroy(_cars[i]);
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
                SceneryTrees.Destroy(_doorText.gameObject);
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
                SceneryTrees.Destroy(piece.gameObject);
                piece = null;
            }
        }

        /// <summary>One line for the log: what went up at the dressed precinct, and why each of the rest did not.</summary>
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
    }
}
