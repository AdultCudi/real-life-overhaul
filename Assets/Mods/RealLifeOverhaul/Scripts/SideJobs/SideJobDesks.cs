using System;
using System.Globalization;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.SideJobs
{
    /// <summary>
    /// The two sign-on desks: one against the wall of the building each firm's help-wanted board stands at (the cab office's and
    /// the parcel depot's, as the employers' ladder placed them), the other side of the door from the board, clear of the board,
    /// its boss and the door. Stood once a city load, when the boards are up; made of plain shapes, drawn no further than the
    /// game's far band (Performance/ModCulling). Each second on foot it says whether the player stands at one. Costs nothing a frame.
    /// </summary>
    internal sealed class SideJobDesks
    {
        public const float Reach = 1.2f;             // metres from a desk's front that count as at it (the board's own reach ends short of it)
        private const float FromWall = 0.35f;        // metres from the wall the desk's middle stands
        private const float DoorClear = 2f;          // metres the desk keeps from the door's front
        private const float BoardClear = 2.6f;       // ...and from the board, so that its reader and its boss are out of the desk's reach
        private const float CullRadius = 1.6f;       // metres round a desk's foot: all of it
        private static readonly float[] Along = { -3f, -5.8f, 5.8f, 7f };   // metres along the wall from the door, the board's side positive

        private static readonly Color Wood = new Color(0.34f, 0.24f, 0.16f);
        private static readonly Color Top = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color CabSign = new Color(0.96f, 0.78f, 0.10f);
        private static readonly Color ParcelSign = new Color(0.20f, 0.46f, 0.74f);

        private readonly Transform _parent;
        private readonly IModLogger _logger;
        private readonly Transform[] _desk = new Transform[2];
        private readonly CullEntry[] _cull = new CullEntry[2];
        private readonly Vector3[] _front = new Vector3[2];
        private readonly Vector3[] _top = new Vector3[2];
        private readonly string[] _address = new string[2];
        private readonly string[] _shop = new string[2];

        public SideJobDesks(Transform parent, IModLogger logger)
        {
            _parent = parent;
            _logger = logger;
        }

        /// <summary>True once the desks were looked for in this city, whether or not either could be stood.</summary>
        public bool Placed { get; private set; }

        /// <summary>The desk the player stands at, as a <see cref="SideKind"/>, or -1.</summary>
        public int At { get; private set; } = -1;

        public bool Stands(SideKind kind) => _desk[(int)kind] != null;

        /// <summary>Where the game's panel hangs: over the desk.</summary>
        public Vector3 Over(SideKind kind) => _top[(int)kind];

        public Vector3 Front(SideKind kind) => _front[(int)kind];

        public string Address(SideKind kind) => _address[(int)kind];

        public string Shop(SideKind kind) => _shop[(int)kind];

        /// <summary>Once a second: the desks are stood when the firms' boards are known, and the one the player is at is noted.</summary>
        public void Tick(Vector3 player, bool onFoot)
        {
            if (!Placed && SideJobBridges.PlacesReady() && Shapes.Available)
            {
                Placed = true;
                Place(SideKind.Cab);
                Place(SideKind.Parcels);
            }
            At = -1;
            if (!onFoot)
            {
                return;
            }
            for (int k = 0; k < _desk.Length; k++)
            {
                if (_desk[k] == null)
                {
                    continue;
                }
                Vector3 offset = _front[k] - player;
                if (offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < Reach * Reach)
                {
                    At = k;
                    return;
                }
            }
        }

        public void Dispose()
        {
            for (int k = 0; k < _desk.Length; k++)
            {
                if (_cull[k] != null)
                {
                    ModCulling.Remove(_cull[k]);
                    _cull[k] = null;
                }
                if (_desk[k] != null)
                {
                    UnityEngine.Object.Destroy(_desk[k].gameObject);
                    _desk[k] = null;
                }
            }
            Placed = false;
            At = -1;
        }

        // Against the wall beside the firm's door, where pavement is clear in front of it: the first of the places that will do.
        private void Place(SideKind kind)
        {
            int k = (int)kind;
            SidePlace place = SideJobBridges.Place(kind);
            if (!place.Known || place.Building == null || !JobSpots.Front(place.Building, out Vector3 door))
            {
                _logger?.Info("Side jobs check (desk, " + kind + "): the firm has no office in this city (no help-wanted board is its own); its shifts cannot be signed on for here: FAIL");
                return;
            }
            Vector3 along = Vector3.Cross(Vector3.up, place.Outward).normalized;
            float side = Vector3.Dot(place.BoardFront - door, along) >= 0f ? 1f : -1f;
            for (int c = 0; c < Along.Length; c++)
            {
                if (!CitySpots.Shopfront(door + along * (side * Along[c]), FromWall + 0.45f, 0.7f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                Vector3 at = stand - outward * 0.45f;
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || JobSpots.Flat(at, door) < DoorClear || JobSpots.Flat(at, place.BoardAt) < BoardClear)
                {
                    continue;
                }
                at.y = ground;
                Transform desk = Make(kind);
                desk.SetPositionAndRotation(at, Quaternion.LookRotation(outward, Vector3.up));
                _desk[k] = desk;
                _cull[k] = ModCulling.Add(CullRules.Kind.JobBoard, desk, at, CullRadius);
                _front[k] = stand + outward * 0.6f;
                _top[k] = at + Vector3.up * 1.9f;
                _address[k] = place.Address;
                _shop[k] = place.Shop;
                CultureInfo inv = CultureInfo.InvariantCulture;
                _logger?.Info("Side jobs check (desk, " + kind + "): stands outside '" + place.Shop + "', " + place.Address + ", " + JobSpots.Flat(at, door).ToString("F1", inv)
                              + " m from the door and " + JobSpots.Flat(at, place.BoardAt).ToString("F1", inv) + " m from the firm's board (wanted: at least "
                              + DoorClear.ToString("F1", inv) + " and " + BoardClear.ToString("F1", inv) + "): PASS");
                return;
            }
            _logger?.Info("Side jobs check (desk, " + kind + "): no clear wall beside the door of " + place.Address + " in " + Along.Length
                          + " places tried; its shifts cannot be signed on for here: FAIL");
        }

        // A lectern-height desk with a sign in the firm's colour, of plain shapes: origin on the ground at its middle, read from +Z.
        private Transform Make(SideKind kind)
        {
            Transform root = new GameObject("RealLifeOverhaul Sign-on Desk").transform;
            root.SetParent(_parent, false);
            Mesh cube = Shapes.Cube;
            Material wood = Shapes.Paint(Wood);
            Shapes.Piece("Front", root, cube, wood, new Vector3(0f, 0.52f, 0.22f), new Vector3(1.2f, 1.04f, 0.04f));
            Shapes.Piece("Left", root, cube, wood, new Vector3(-0.58f, 0.52f, 0f), new Vector3(0.04f, 1.04f, 0.48f));
            Shapes.Piece("Right", root, cube, wood, new Vector3(0.58f, 0.52f, 0f), new Vector3(0.04f, 1.04f, 0.48f));
            Shapes.Piece("Top", root, cube, Shapes.Paint(Top), new Vector3(0f, 1.06f, 0f), new Vector3(1.26f, 0.05f, 0.56f));
            Shapes.Piece("Post", root, cube, Shapes.Paint(Top), new Vector3(0f, 1.3f, -0.22f), new Vector3(0.05f, 0.46f, 0.05f));
            Shapes.Piece("Sign", root, cube, Shapes.Paint(kind == SideKind.Cab ? CabSign : ParcelSign, 0.5f), new Vector3(0f, 1.62f, -0.22f), new Vector3(0.9f, 0.3f, 0.04f));
            return root;
        }
    }
}
