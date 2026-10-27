using System;
using System.Collections.Generic;
using System.Reflection;
using Buildings.Outdoors;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One building wrapped in the game's own scaffolding to its roof (WrapRules lays it out), its pieces set out and taken
    /// down a few a frame, only while the wrap is out of view. Each piece's renderers join the building's own lists for the
    /// camera's fade and for the city map, and the pieces hang under a holder whose game extension names the building, so
    /// the camera's ray meeting the scaffold fades the building and the scaffold with it, exactly as the game's own scaffold
    /// does; every renderer leaves those lists before its piece is destroyed. On a plot there is no building: the plot hides
    /// the wrap with its frame (PlotFade). Cost: nothing per frame while nothing is to change; a few pieces a frame while it does.
    /// </summary>
    internal sealed class SiteWrap
    {
        private static readonly FieldInfo OwnerField = GameContract.WrapFadeOwner.Field;
        private static readonly FieldInfo MapField = GameContract.WrapMapRenderers.Field;
        private const float Height = 6.3f;            // metres: the game's module, posts and its top rails
        private const float Reach = 2.8f;             // metres either side of a piece's middle its footprint can reach, the 6 m module turned
        private const float ProbeTop = 14f;           // metres: trees, lamps and signs are looked for up to here
        private const float ProbeStep = 1.5f;         // metres along a side between looks for what stands in the way

        private static readonly List<Renderer> Buffer = new List<Renderer>(8);
        private static int _obstacles = -1, _buildings = -1;

        public readonly WrapPlan Plan = new WrapPlan();
        private readonly WrapShell _shell = new WrapShell();
        private readonly List<WrapKeepOut> _keepOuts = new List<WrapKeepOut>(16);
        private readonly bool[] _street = new bool[4];
        private GameObject[] _placed = new GameObject[WrapRules.MostPieces];
        private readonly Transform _root;
        private Transform _holder;
        private CityBuildingController _building;
        private HashSet<Renderer> _mapSet;
        private Vector3 _origin;
        private Quaternion _turn;
        private int _lifts, _addAt, _takeAt, _placedCount, _modulesShown;
        private bool _fabric, _checked, _hidden;
        private string _name;

        /// <summary>Pieces standing now.</summary>
        public int Placed => _placedCount;

        /// <summary>The building whose fade it follows; null on a plot.</summary>
        public CityBuildingController Building => _building;

        /// <summary>The world box round every piece of the plan, for the out-of-view test.</summary>
        public Bounds Bounds { get; private set; }

        /// <summary>True while some piece is to be set out or taken down.</summary>
        public bool Busy => _addAt < Plan.Pieces.Count || _takeAt >= 0;

        /// <summary>True while a piece is still to be taken down: that comes before any is set out.</summary>
        public bool Taking => _takeAt >= 0;

        /// <summary>Called once a wrap's wanted pieces all stand: the self-check line is written then.</summary>
        public Action<SiteWrap> Settled;

        public SiteWrap(Transform root)
        {
            _root = root;
        }

        /// <summary>
        /// Lays out the wrap of a building: its shell from its own colliders, its street sides by the pavement in front, its
        /// doors and what stands in the way. Null if it can be wrapped (Plan says how), else why not.
        /// </summary>
        public string Prepare(CityBuildingController building, string name)
        {
            Strip();
            _name = name;
            if (!BuildingShell.Read(building, _shell, out _origin, out _turn))
            {
                return "the building has no solid box a storey high";
            }
            _building = building;
            _mapSet = MapSet(building);
            Survey();
            WrapRules.Plan(_shell, _street, _keepOuts, WrapRules.MostLifts, WrapRules.MostPieces, Plan);
            Measure();
            return Plan.Pieces.Count > 0 ? null : Plan.Why ?? "no street side has room for the game's scaffold";
        }

        /// <summary>Lays out the wrap of a plot's frame: one box, every side wrapped, nobody's fade to follow.</summary>
        public string PrepareFrame(Vector3 origin, Quaternion turn, ShellBox frame, string name)
        {
            Strip();
            _name = name;
            _origin = origin;
            _turn = turn;
            _building = null;
            _mapSet = null;
            WrapRules.Measure(new[] { frame }, 1, _shell);
            for (int side = 0; side < 4; side++)
            {
                _street[side] = true;
            }
            _keepOuts.Clear();
            WrapRules.Plan(_shell, _street, _keepOuts, WrapRules.MostLifts, WrapRules.MostPieces, Plan);
            Measure();
            return Plan.Pieces.Count > 0 ? null : Plan.Why;
        }

        /// <summary>What should stand: so many lifts, with or without the fabric. Nothing changes until <see cref="Step"/>.</summary>
        public void Want(int lifts, bool fabric)
        {
            _lifts = lifts;
            _fabric = fabric;
            _addAt = 0;
            _takeAt = Plan.Pieces.Count - 1;
            _checked = false;
            Skip();
        }

        /// <summary>Sets out or takes down up to <paramref name="budget"/> pieces: down first, from the top; up after, from the bottom.</summary>
        public int Step(int budget)
        {
            int done = 0;
            while (done < budget && _takeAt >= 0)
            {
                Take(_takeAt);
                _takeAt--;
                done++;
                Skip();
            }
            while (done < budget && _addAt < Plan.Pieces.Count)
            {
                Put(_addAt);
                _addAt++;
                done++;
                Skip();
            }
            if (!Busy && !_checked)
            {
                _checked = true;
                Settled?.Invoke(this);
            }
            return done;
        }

        // Moves both cursors past every piece already as it should be.
        private void Skip()
        {
            while (_takeAt >= 0 && (_placed[_takeAt] == null || Wanted(_takeAt)))
            {
                _takeAt--;
            }
            while (_addAt < Plan.Pieces.Count && (_placed[_addAt] != null || !Wanted(_addAt)))
            {
                _addAt++;
            }
        }

        private bool Wanted(int index) => WrapRules.Shown(Plan.Pieces[index], _lifts, _fabric);

        /// <summary>Everything down at once (indoors, behind the black screen, the feature off): each renderer leaves the building's lists first.</summary>
        public void Strip()
        {
            for (int i = 0; i < _placed.Length; i++)
            {
                if (_placed[i] != null)
                {
                    Take(i);
                }
            }
            if (_holder != null)
            {
                SceneryTrees.Destroy(_holder.gameObject);
                _holder = null;
            }
            _placedCount = 0;
            _modulesShown = 0;
            _hidden = false;
            _addAt = Plan.Pieces.Count;
            _takeAt = -1;
        }

        /// <summary>Whether any of the wrap's box is in the camera's view: planes made once a frame by whoever asks.</summary>
        public bool InView(Plane[] planes) => GeometryUtility.TestPlanesAABB(planes, Bounds);

        // The street sides (pavement under the whole depth of the scaffold, no other building in it), the doors, and what
        // stands where the scaffold would.
        private void Survey()
        {
            if (_obstacles < 0)
            {
                _obstacles = LayerMask.GetMask("StreetProps", "Default");
                _buildings = LayerMask.GetMask("Buildings", "BuildingWalls", "BuildingsOutlined");
            }
            _keepOuts.Clear();
            for (int side = 0; side < 4; side++)
            {
                _street[side] = Street(side);
            }
            for (int d = 0; _building.entranceDoors != null && d < _building.entranceDoors.Length; d++)
            {
                BuildingEntranceDoor door = _building.entranceDoors[d];
                if (door == null || door.doorTransform == null)
                {
                    continue;
                }
                Vector3 local = BuildingShell.Local(_origin, _turn, door.doorTransform.position);
                int side = WrapRules.SideOf(_shell, local.x, local.z);
                _keepOuts.Add(WrapRules.Door(side, WrapRules.Along(side, local.x, local.z)));
            }
            for (int side = 0; side < 4; side++)
            {
                if (_street[side])
                {
                    Obstacles(side);
                }
            }
        }

        private bool Street(int side)
        {
            ShellSide s = _shell.Sides[side];
            float length = s.RunMax - s.RunMin;
            if (length < 2f * WrapRules.Unit)
            {
                return false;
            }
            int good = 0;
            for (int k = 1; k <= 3; k++)
            {
                float along = s.RunMin + length * k / 4f;
                if (Pavement(side, s.Outer + WrapRules.Gap + 0.6f, along) && Pavement(side, s.Outer + WrapRules.Gap + WrapRules.Depth - 0.25f, along))
                {
                    good++;
                }
            }
            if (good < 2)
            {
                return false;
            }
            // Another building standing where the scaffold would (a side against the next house): not a street side.
            float middle = (s.RunMin + s.RunMax) * 0.5f;
            Vector3 centre = At(side, s.Outer + WrapRules.Gap + WrapRules.Depth * 0.5f, middle, 2.5f);
            var half = new Vector3(Mathf.Max(0.5f, length * 0.5f - 0.6f), 2f, WrapRules.Depth * 0.5f - 0.15f);
            return CivicSpots.BoxFree(centre, half, Turn(side), _buildings);
        }

        private bool Pavement(int side, float outward, float along) => CitySpots.OnPavement(At(side, outward, along, 0f), out _);

        // Every 1.5 m of a street side: a tree's crown, a sign or a lamp over the scaffold keeps that stretch clear; a bin or
        // a hydrant where a post would stand keeps the posts off it.
        private void Obstacles(int side)
        {
            ShellSide s = _shell.Sides[side];
            float top = Mathf.Min(_shell.Top, ProbeTop);
            for (float from = s.RunMin; from < s.RunMax; from += ProbeStep)
            {
                float to = Mathf.Min(from + ProbeStep, s.RunMax), middle = (from + to) * 0.5f, half = (to - from) * 0.5f;
                if (top > 3f)
                {
                    Vector3 high = At(side, s.Outer + WrapRules.Gap + WrapRules.Depth * 0.5f, middle, (2.9f + top) * 0.5f);
                    if (!CivicSpots.BoxFree(high, new Vector3(half, (top - 2.9f) * 0.5f, WrapRules.Depth * 0.5f), Turn(side), _obstacles))
                    {
                        _keepOuts.Add(new WrapKeepOut { Side = side, From = from, To = to, PostsOnly = false });
                        continue;
                    }
                }
                for (int row = 0; row < 2; row++)
                {
                    float outward = s.Outer + WrapRules.Gap + (row == 0 ? 0.19f : WrapRules.Depth - 0.19f);
                    if (!CivicSpots.BoxFree(At(side, outward, middle, 0.85f), new Vector3(half, 0.7f, 0.15f), Turn(side), _obstacles))
                    {
                        _keepOuts.Add(new WrapKeepOut { Side = side, From = from, To = to, PostsOnly = true });
                        break;
                    }
                }
            }
        }

        private Vector3 At(int side, float outward, float along, float up)
        {
            WrapRules.Point(side, outward, along, out float x, out float z);
            return BuildingShell.World(_origin, _turn, x, up, z);
        }

        private Quaternion Turn(int side) => _turn * Quaternion.Euler(0f, WrapRules.YawOf(side), 0f);

        // The world box round every piece of the plan.
        private void Measure()
        {
            if (Plan.Pieces.Count > _placed.Length)
            {
                _placed = new GameObject[Plan.Pieces.Count];
            }
            var bounds = new Bounds(_origin, Vector3.zero);
            for (int i = 0; i < Plan.Pieces.Count; i++)
            {
                WrapPiece piece = Plan.Pieces[i];
                Vector3 foot = BuildingShell.World(_origin, _turn, piece.X, piece.Y, piece.Z);
                bounds.Encapsulate(foot - new Vector3(Reach, 0f, Reach));
                bounds.Encapsulate(foot + new Vector3(Reach, Height, Reach));
            }
            Bounds = bounds;
            _addAt = Plan.Pieces.Count;
            _takeAt = -1;
        }

        private void Put(int index)
        {
            Transform holder = Holder();
            WrapPiece piece = Plan.Pieces[index];
            GameObject made = SiteKit.Make(piece.Kind, holder);
            if (made == null)
            {
                return;
            }
            made.transform.SetPositionAndRotation(BuildingShell.World(_origin, _turn, piece.X, piece.Y, piece.Z), _turn * Quaternion.Euler(0f, piece.Yaw, 0f));
            _placed[index] = made;
            _placedCount++;
            _modulesShown += piece.Fabric ? 0 : 1;
            if (_building == null)
            {
                if (_hidden)
                {
                    Show(made, false);
                }
                return;
            }
            Buffer.Clear();
            made.GetComponentsInChildren(true, Buffer);
            bool hidden = _building.IsInCameraBlockMode;
            for (int r = 0; r < Buffer.Count; r++)
            {
                Renderer renderer = Buffer[r];
                _building.renderersToFade.Add(renderer);
                _building.renderersToHide.Add(renderer);
                _mapSet?.Add(renderer);
                if (hidden)
                {
                    ViewBlockingEntity.SetRendererToHideVisibility(renderer, true);   // as its building is: the camera looks through both
                }
            }
            Buffer.Clear();
        }

        private void Take(int index)
        {
            GameObject made = _placed[index];
            _placed[index] = null;
            if (made == null)
            {
                return;
            }
            _placedCount--;
            _modulesShown -= index < Plan.Pieces.Count && Plan.Pieces[index].Fabric ? 0 : 1;
            if (_building != null)
            {
                Buffer.Clear();
                made.GetComponentsInChildren(true, Buffer);
                for (int r = 0; r < Buffer.Count; r++)
                {
                    _building.renderersToFade.Remove(Buffer[r]);
                    _building.renderersToHide.Remove(Buffer[r]);
                    _mapSet?.Remove(Buffer[r]);
                }
                Buffer.Clear();
            }
            SceneryTrees.Destroy(made);
        }

        // The holder of the pieces: its game extension names the building, set before the holder wakes, so that the camera's
        // ray finding a piece fades its building (the game looks at a hit's own and its parent's components).
        private Transform Holder()
        {
            if (_holder != null)
            {
                return _holder;
            }
            var holder = new GameObject("RealLifeOverhaul Wrap " + _name);
            holder.SetActive(false);
            holder.transform.SetParent(_root, false);
            if (_building != null && OwnerField != null)
            {
                ViewBlockingEntityExtension extension = holder.AddComponent<ViewBlockingEntityExtension>();
                OwnerField.SetValue(extension, _building);
            }
            holder.SetActive(true);
            _holder = holder.transform;
            return _holder;
        }

        /// <summary>A plot's wrap hidden from the camera with its frame, or shown again: every piece standing, and those set out meanwhile.</summary>
        public void Hide(bool hide)
        {
            if (_hidden == hide)
            {
                return;
            }
            _hidden = hide;
            for (int i = 0; i < _placed.Length; i++)
            {
                if (_placed[i] != null)
                {
                    Show(_placed[i], !hide);
                }
            }
        }

        // As the game hides what blocks its camera: a renderer that casts a shadow keeps only its shadow, any other goes off.
        private static void Show(GameObject piece, bool shown)
        {
            Buffer.Clear();
            piece.GetComponentsInChildren(true, Buffer);
            for (int r = 0; r < Buffer.Count; r++)
            {
                ViewBlockingEntity.SetRendererToHideVisibility(Buffer[r], !shown);
            }
            Buffer.Clear();
        }

        private static HashSet<Renderer> MapSet(CityBuildingController building) => MapField != null ? MapField.GetValue(building) as HashSet<Renderer> : null;

        /// <summary>
        /// The self-check: every renderer standing is in the building's fade and hide lists (and the map's set where it can be
        /// read), and the holder's extension names the building. Null when all is linked, else what is not.
        /// </summary>
        public string Unlinked()
        {
            if (_building == null)
            {
                return "it wraps a plot, which has no building to fade with";
            }
            if (OwnerField == null || _holder == null || !_holder.TryGetComponent(out ViewBlockingEntityExtension extension)
                || !extension.TryGetViewBlockingEntity(out ViewBlockingEntity owner) || owner != _building)
            {
                return "the holder's extension does not name the building";
            }
            int missing = 0, missingMap = 0, renderers = 0;
            for (int i = 0; i < _placed.Length; i++)
            {
                if (_placed[i] == null)
                {
                    continue;
                }
                Buffer.Clear();
                _placed[i].GetComponentsInChildren(true, Buffer);
                for (int r = 0; r < Buffer.Count; r++)
                {
                    renderers++;
                    missing += _building.renderersToFade.Contains(Buffer[r]) && _building.renderersToHide.Contains(Buffer[r]) ? 0 : 1;
                    missingMap += _mapSet == null || _mapSet.Contains(Buffer[r]) ? 0 : 1;
                }
            }
            Buffer.Clear();
            if (renderers == 0)
            {
                return "no piece stands";
            }
            if (missing > 0)
            {
                return missing + " of " + renderers + " renderers are not in the building's fade";
            }
            return missingMap > 0 ? missingMap + " of " + renderers + " renderers are not in the city map's set" : null;
        }

        /// <summary>
        /// Where somebody stands on the first deck nearest a point: the middle of a street-lift module (the game's deck is at its
        /// top, <see cref="Deck"/> m up), facing the wall. False if no module of the plan is near enough.
        /// </summary>
        public bool DeckSpot(Vector3 near, float within, out Vector3 spot, out Vector3 face)
        {
            spot = face = default;
            float best = within * within;
            int found = -1;
            for (int i = 0; i < Plan.Pieces.Count; i++)
            {
                WrapPiece piece = Plan.Pieces[i];
                if (piece.Lift != 0 || piece.Fabric || piece.Kind == WrapPieceKind.Corner)
                {
                    continue;
                }
                Vector3 at = BuildingShell.World(_origin, _turn, piece.X, 0f, piece.Z);
                float sqr = CivicCity.Flat(at - near);
                if (sqr < best)
                {
                    best = sqr;
                    found = i;
                    spot = at;
                }
            }
            if (found < 0)
            {
                return false;
            }
            WrapRules.Axes(Plan.Pieces[found].Side, out float outX, out float outZ, out _, out _);
            face = spot - BuildingShell.Direction(_turn, outX, outZ);
            return true;
        }

        /// <summary>Metres up a street-lift module's boards are, where its people stand: the game's deck.</summary>
        public const float Deck = 5.12f;

        /// <summary>Modules standing (fabric not counted) and the lifts wanted, for the self-check line.</summary>
        public int ModulesShown => _modulesShown;
        public int LiftsShown => Math.Min(_lifts, Plan.Lifts);
        public bool MapLinked => _mapSet != null;
    }
}
