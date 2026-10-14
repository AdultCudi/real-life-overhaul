using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using BAModAPI;
using BigAmbitions.InteriorDesigner.InteriorElements;
using BigAmbitions.SaveSystem;
using BusinessLayoutSets;
using Extensions;
using RealLifeOverhaul.Tech;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The floor of one of the mod's buildings fitted out while the player is in it (Scripts/FitOut): the room the caller
    /// has measured (Tech/Sites/SiteProps.cs, RoomMeasure) and what it already stands there (a lobby, a station's counter,
    /// a server hall) are handed in, and this does the rest, a little each frame:
    ///
    ///   the design     the game's own design for the unit, of the kinds of business asked for (FitDesigns), read on a
    ///                  worker thread; its walls and floors put on the room by the game's own call while the player is in
    ///                  it, where the caller asks for them
    ///   the pieces     a template of every piece wanted, the game's own (Core/GameFurniture) or a model of the mod's
    ///                  where the game has none (the server closet's racks, a workbench), made the first time it is
    ///                  wanted in a session; their sizes measured from them
    ///   the plan       <see cref="FitPlanner"/>, on a worker thread: the design round what is there, rooms of the
    ///                  building's kind on the bare floor, the walkway check
    ///   the building   copies of the templates stood where the plan says, the nearest the door first; hung where a ray
    ///                  finds the wall, lamps where one finds the ceiling; then what stands is cut out of the walkable
    ///                  floor, a few pieces a frame (the game's own carving)
    ///   the people     <see cref="FitCast"/>: staff at the desks, a few walking, looked after once a second
    ///   your desk      where a job of the player's is worked on this floor (<see cref="FitDesks"/>), one of its desks
    ///                  is the player's: picked by the seed the careers give, the same every visit, and nobody of the
    ///                  cast sits there
    ///
    /// THE COST. Behind the game's black screen at the door a frame may take up to <see cref="HiddenBudgetMs"/> of the
    /// work, in play <see cref="BudgetMs"/>: no frame of the building is a long one the player sees (a template's first
    /// load from the game's files is the one step that cannot be cut, and comes once a session, mostly behind the black
    /// screen). Nothing runs once it stands but the cast's look once a second; nothing runs at all while the player is
    /// elsewhere. Everything is destroyed when they leave; the templates are kept for the next visit.
    ///
    /// Everything is local and only to be seen: plain objects, not items, never saved, never counted in what furniture is
    /// worth, nothing sent over Going Public's connection. The callers hand in only the mod's own buildings and the
    /// player's own places.
    /// </summary>
    internal sealed class FitView
    {
        public const float BudgetMs = 2.5f;           // milliseconds of a played frame the building may take
        public const float HiddenBudgetMs = 28f;      // ...and of a frame behind the black screen
        private const int CutsPerFrame = 24;          // pieces cut out of the walkable floor a frame
        private const float WaitForDesign = 8f;       // seconds the reading of a design is waited for, at most
        private const string ModProp = "rlo:";

        /// <summary>What a caller asks for: the room, the building's kind, what is there already and what to furnish from.</summary>
        public sealed class Request
        {
            public string Name;
            public FitKind Kind;
            public int Seed;
            public BuildingManager Building;
            public string Place;                                              // the floor's place key for the player's desk (a tower's floor: the company's); null: the building's
            public RoomGrid Grid;
            public float Floor;
            public Vector3 Door, Inward;
            public readonly List<string> DesignKinds = new List<string>(8);   // the kinds of business whose designs may furnish it, in order
            public readonly List<string> LookKinds = new List<string>(4);     // ...and whose walls and floors alone may be put on it
            public bool Look = true;                                          // the design's walls and floors on the room while the player is in it
            public bool Carve = true;                                         // what stands is cut out of the walkable floor
            public bool People = true;
            public readonly FitSite Site = new FitSite();                     // what is there already: the caller's Fixed, KeepFree and Keep

            public void Reset()
            {
                Name = null;
                Building = null;
                Place = null;
                Grid = null;
                DesignKinds.Clear();
                LookKinds.Clear();
                Look = Carve = People = true;
                Site.Reset();
            }
        }

        private struct Seat
        {
            public Transform At;
            public FitUse Use;
            public Vector3 Stand;
            public Transform Desk, Chair;   // what the seat is drawn up to, and the chair it is on
        }

        private readonly IModLogger _logger;
        private FitPlan _plan = new FitPlan();
        private readonly List<string> _candidates = new List<string>(8);
        private readonly List<string> _wanted = new List<string>(64);
        private readonly List<int> _order = new List<int>(400);
        private readonly List<NavMeshObstacle> _cuts = new List<NavMeshObstacle>(256);
        private readonly List<Transform> _seatScratch = new List<Transform>(8);
        private readonly List<Seat> _seats = new List<Seat>(64);
        private readonly StringBuilder _said = new StringBuilder(256);
        private readonly FitCast _cast;
        private Request _request;
        private MonoBehaviour _host;
        private Coroutine _running;
        private Transform _root;
        private BusinessLayoutSet _set;
        private string _setName;
        private InteriorElement[] _elements;
        private Buildings.Building _lookOn;
        private int _frames;
        private double _workMs;
        private bool _hidden;
        private string _deskPlace;   // the building, while one of its desks is the player's
        private int _deskSeed = -1;
        private FitDesk _desk;

        public FitView(IModLogger logger)
        {
            _logger = logger;
            _cast = new FitCast(logger);
        }

        /// <summary>True from Begin until End, built or still building.</summary>
        public bool Active => _root != null;

        /// <summary>True once the building stands and its people are about.</summary>
        public bool Ready { get; private set; }

        /// <summary>True if lamps were hung (the design's or the plan's): the caller then hangs none of its own.</summary>
        public bool HasLamps => Ready && _plan.HasLamps;

        /// <summary>What the fit-out came to, for the log line.</summary>
        public string Said => _said.ToString();

        /// <summary>Said once the building stands (the callers log it).</summary>
        public event Action<FitView> Built;

        /// <summary>
        /// The floor of the room in the request fitted out, a little each frame on <paramref name="host"/>. The request is
        /// the caller's and is read until End. False if there is nothing to fit out (no room was measured).
        /// </summary>
        public bool Begin(Request request, MonoBehaviour host)
        {
            End();
            if (request == null || request.Grid == null || request.Building == null || host == null || !host.isActiveAndEnabled)
            {
                return false;
            }
            _request = request;
            _host = host;
            BuildingRegistration reg = request.Building.buildingRegistration;
            _deskPlace = !string.IsNullOrEmpty(request.Place) ? request.Place : reg != null ? TalentGame.Place(reg) : null;
            _deskSeed = FitDesks.Ask(_deskPlace);
            if (_deskSeed >= 0)
            {
                FitDesks.Going(_deskPlace);
            }
            _root = new GameObject("RealLifeOverhaul Fit-out").transform;
            _said.Length = 0;
            _frames = 0;
            _workMs = 0;
            Ready = false;
            _running = host.StartCoroutine(Build());
            return true;
        }

        // ---------------------------------------------------------------- the building, a little a frame

        private IEnumerator Build()
        {
            var watch = new Stopwatch();
            Request r = _request;
            FitSite site = r.Site;
            Buildings.Building building = r.Building.building;

            // The game's design for this very unit, read on a worker thread.
            _set = null;
            _setName = null;
            string size = FitDesigns.SizeOf(building);
            FitDesigns.Candidates(size, r.DesignKinds.Count > 0 ? r.DesignKinds : r.LookKinds, r.Seed, _candidates);
            float waited = 0f;
            for (int i = 0; i < _candidates.Count && _set == null; i++)
            {
                Task<BusinessLayoutSet> read = FitDesigns.Read(_candidates[i]);
                while (!read.IsCompleted && waited < WaitForDesign)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                BusinessLayoutSet set = read.IsCompleted && !read.IsFaulted ? read.Result : null;
                if (FitDesigns.Fits(set, building) && (r.DesignKinds.Count == 0 || StandsHere(set, r)))
                {
                    _set = set;
                    _setName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(_candidates[i])) + "/" + System.IO.Path.GetFileNameWithoutExtension(_candidates[i]) + " (" + size + ")";
                }
                else if (i + 2 < _candidates.Count)
                {
                    FitDesigns.Read(_candidates[i + 2]);
                }
            }
            bool furnish = _set != null && r.DesignKinds.Count > 0;

            // The pieces wanted, each made ready once a session: the design's and those of the building's kind.
            _wanted.Clear();
            if (furnish)
            {
                for (int i = 0; i < _set.Items.Count; i++)
                {
                    string id = Id(_set.Items[i]);
                    if (id != null && FitRoles.Fitted(id) && !_wanted.Contains(id))
                    {
                        _wanted.Add(id);
                    }
                }
            }
            IReadOnlyList<string> kind = FitPlanner.Pieces(r.Kind);
            for (int i = 0; i < kind.Count; i++)
            {
                if (!_wanted.Contains(kind[i]))
                {
                    _wanted.Add(kind[i]);
                }
            }
            watch.Restart();
            for (int i = 0; i < _wanted.Count; i++)
            {
                if (_wanted[i].StartsWith(ModProp, StringComparison.Ordinal) || GameFurniture.HasTemplate(_wanted[i]))
                {
                    continue;
                }
                if (!Safely(() => GameFurniture.Template(_wanted[i], out _)))
                {
                    yield break;
                }
                if (OverBudget(watch))
                {
                    Lap(watch);
                    yield return null;
                    watch.Restart();
                }
            }
            Lap(watch);

            // The sizes, the design as pieces of a plan, and the plan on a worker thread.
            site.Kind = r.Kind;
            site.Seed = r.Seed;
            site.DoorX = r.Door.x;
            site.DoorZ = r.Door.z;
            site.InX = r.Inward.x;
            site.InZ = r.Inward.z;
            site.Erosion = Erosion();
            Exits(r, site);
            // (each visit its own design and plan: a worker left over from the visit before writes only into its own)
            var design = new List<FitPiece>(furnish ? _set.Items.Count : 0);
            var plan = new FitPlan();
            watch.Restart();
            bool ready = Safely(() =>
            {
                site.Sizes.Clear();
                for (int i = 0; i < _wanted.Count; i++)
                {
                    if (Size(_wanted[i], out FitSize size))
                    {
                        site.Sizes[_wanted[i]] = size;
                    }
                }
                if (furnish)
                {
                    DesignPieces(r, design);
                }
            });
            Lap(watch);
            if (!ready)
            {
                yield break;
            }
            string failed = null;
            Task planning = Task.Run(() =>
            {
                try
                {
                    FitPlanner.Plan(r.Grid, site, furnish ? design : null, plan);
                }
                catch (Exception e)
                {
                    failed = e.GetType().Name + ": " + e.Message;
                }
            });
            while (!planning.IsCompleted)
            {
                yield return null;
            }
            if (failed != null)
            {
                _logger?.Warn("Fit-out: " + r.Name + ": the plan could not be made (" + failed + "); the floor is left as it is.");
                yield break;
            }
            _plan = plan;

            // The walls and floors of the design, by the game's own call (behind the black screen if it is still up).
            if (r.Look && _set != null && _set.interiorDesigns != null && _set.interiorDesigns.Count > 0)
            {
                ApplyLook(r);
            }

            // The pieces, the nearest the door first.
            _order.Clear();
            for (int i = 0; i < _plan.Pieces.Count; i++)
            {
                if (_plan.Pieces[i].Kept)
                {
                    _order.Add(i);
                }
            }
            Vector3 door = r.Door;
            _order.Sort((a, b) =>
            {
                FitPiece p = _plan.Pieces[a], q = _plan.Pieces[b];
                float da = (p.CX - door.x) * (p.CX - door.x) + (p.CZ - door.z) * (p.CZ - door.z), db = (q.CX - door.x) * (q.CX - door.x) + (q.CZ - door.z) * (q.CZ - door.z);
                int byDistance = da.CompareTo(db);
                return byDistance != 0 ? byDistance : a.CompareTo(b);
            });
            var made = new Transform[_plan.Pieces.Count];
            int placed = 0;
            watch.Restart();
            for (int k = 0; k < _order.Count; k++)
            {
                int i = _order[k];
                FitPiece p = _plan.Pieces[i];
                // (what stands on a piece goes on once that piece stands: its parent's index is lower in the plan, not always in the order)
                Transform under = p.Anchor >= 0 ? made[p.Anchor] : null;
                if (!Safely(() => made[i] = Place(p, under)))
                {
                    yield break;
                }
                placed += made[i] != null ? 1 : 0;
                if (OverBudget(watch))
                {
                    Lap(watch);
                    yield return null;
                    watch.Restart();
                }
            }
            Lap(watch);
            // What stands cut out of the walkable floor, a few a frame.
            for (int k = 0; k < _cuts.Count; k++)
            {
                if (_cuts[k] != null)
                {
                    _cuts[k].enabled = true;
                }
                if ((k + 1) % CutsPerFrame == 0)
                {
                    _frames++;
                    yield return null;
                }
            }
            bool seated = Safely(() =>
            {
                Seats(made);
                if (r.People && Settings.People)
                {
                    _cast.Begin(r.Kind, _seatsView, StopsView(r), r.Door);
                }
                Say(r, placed);
            });
            if (!seated)
            {
                yield break;
            }
            Ready = true;
            _running = null;
            if (_desk != null)
            {
                FitDesks.Stands(_desk);
            }
            else if (_deskSeed >= 0)
            {
                FitDesks.Down(_deskPlace);   // (no desk with a seat stands: the careers look elsewhere)
            }
            try
            {
                Built?.Invoke(this);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // A step of the building that may meet something of the game's gone: the fault said once and the floor left as it is.
        private bool Safely(Action step)
        {
            try
            {
                step();
                return true;
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Fit-out: " + (_request != null ? _request.Name : "a floor") + " is left as it is after the error above.");
                _running = null;
                return false;
            }
        }

        private bool OverBudget(Stopwatch watch)
        {
            return watch.Elapsed.TotalMilliseconds >= (SceneFinds.Hidden ? HiddenBudgetMs : BudgetMs);
        }

        // A frame's share of the work counted, before the next frame is waited for.
        private void Lap(Stopwatch watch)
        {
            _workMs += watch.Elapsed.TotalMilliseconds;
            _frames++;
        }

        private static string Id(Item item) => item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName.GetIdWithoutType() : null;

        // Most of a design's floor pieces on this room's floor: it is a design for this room.
        private static bool StandsHere(BusinessLayoutSet set, Request r)
        {
            int floorItems = 0, onFloor = 0;
            for (int i = 0; i < set.Items.Count; i++)
            {
                Item item = set.Items[i];
                Vector3 at = item.position;
                if (item.itemName == null || Mathf.Abs(at.y - r.Floor) > 0.15f)
                {
                    continue;
                }
                floorItems++;
                onFloor += r.Grid.NearestOpen(at.x, at.z, null) >= 0 ? 1 : 0;
            }
            return floorItems >= 4 && onFloor >= floorItems * 0.7f;
        }

        // How far the walkable floor stops short of a wall: the game's walkers' own width.
        private static float Erosion()
        {
            try
            {
                return Mathf.Clamp(NavMesh.GetSettingsByID(NavMeshHelper.NpcNavMeshFilter.agentTypeID).agentRadius, 0.15f, 0.6f);
            }
            catch (Exception)
            {
                return 0.3f;
            }
        }

        // The unit's other ways out kept clear, and its drive-in bays, with room for a car.
        private static void Exits(Request r, FitSite site)
        {
            site.Exits.Clear();
            List<ExitZone> exits = r.Building.exitZones;
            for (int i = 0; exits != null && i < exits.Count; i++)
            {
                ExitZone exit = exits[i];
                if (exit == null || exit.playerSpawnPoint == null)
                {
                    continue;
                }
                Vector3 at = exit.playerSpawnPoint.position;
                if (!exit.isDriveInBay && CitySpots.Flat(at - r.Door) < 0.5f * 0.5f)
                {
                    continue;   // (the door the player came in by: the plan keeps its own way clear of it)
                }
                site.Exits.Add(new FitSpot(at.x, at.z, exit.isDriveInBay ? 4.5f : FitPlanner.ExitClear));
            }
        }

        // A piece's drawn size: its template's, or the size the mod's model stands by.
        private static bool Size(string prefab, out FitSize size)
        {
            if (prefab.StartsWith(ModProp, StringComparison.Ordinal))
            {
                if (ModSpec(prefab, out SiteProps.Spec spec))
                {
                    size = new FitSize(spec.Size.x, spec.Size.z, spec.Size.y);
                    return true;
                }
                size = default;
                return false;
            }
            if (GameFurniture.Template(prefab, out Bounds b) == null || b.size.x < 0.02f || b.size.z < 0.02f)
            {
                size = default;
                return false;
            }
            size = new FitSize(b.size.x, b.size.z, b.size.y, b.center.x, b.center.z, b.max.y);
            return true;
        }

        private static bool ModSpec(string prefab, out SiteProps.Spec spec)
        {
            switch (prefab)
            {
                case "rlo:closetrack": spec = SiteProps.ClosetRack; return true;
                case "rlo:bench": spec = SiteProps.Bench; return true;
                default: spec = default; return false;
            }
        }

        // The design's items as pieces of the plan, each with the floor its drawn bounds take, turned as it stands.
        private void DesignPieces(Request r, List<FitPiece> into)
        {
            for (int i = 0; i < _set.Items.Count; i++)
            {
                Item item = _set.Items[i];
                string id = Id(item);
                if (id == null || !FitRoles.Fitted(id) || !r.Site.Sizes.TryGetValue(id, out FitSize s))
                {
                    continue;
                }
                Vector3 at = item.position;
                Quaternion facing = item.rotation;
                float height = at.y - r.Floor;
                FitRole role = FitRoles.IsLamp(id) ? FitRole.Lamp : FitRoles.IsFlat(id) ? FitRole.Flat : height >= 0.15f ? FitRole.OnTop : FitRoles.IsChair(id) ? FitRole.Seat : FitRole.Floor;
                float yaw = facing.eulerAngles.y;
                float sin = Mathf.Sin(yaw * Mathf.Deg2Rad), cos = Mathf.Cos(yaw * Mathf.Deg2Rad);
                float cx = at.x + s.CX * cos + s.CZ * sin, cz = at.z - s.CX * sin + s.CZ * cos;
                into.Add(new FitPiece
                {
                    Prefab = id, Role = role, X = at.x, Z = at.z, Y = height, Yaw = yaw, CX = cx, CZ = cz,
                    HalfX = Mathf.Abs(cos) * s.W * 0.5f + Mathf.Abs(sin) * s.D * 0.5f, HalfZ = Mathf.Abs(sin) * s.W * 0.5f + Mathf.Abs(cos) * s.D * 0.5f,
                    Height = s.H, Anchor = -1, Tall = s.H > 1.3f
                });
            }
        }

        // One piece of the plan stood in the room; null if it could not be (no such prefab, no wall or ceiling where it hangs).
        private Transform Place(FitPiece p, Transform under)
        {
            Request r = _request;
            float floor = r.Floor;
            Quaternion facing = Quaternion.Euler(0f, p.Yaw, 0f);
            Vector3 at = new Vector3(p.X, floor + p.Y, p.Z);
            if (p.Role == FitRole.Hung && !p.Design)
            {
                // On the wall behind where the plan put it, its back on the wall's real surface.
                Vector3 front = facing * Vector3.forward;
                if (!RoomMeasure.Wall(new Vector3(p.X, floor + p.Y + 0.4f, p.Z), -front, 1.4f, out Vector3 surface, out Vector3 normal))
                {
                    return null;
                }
                at = new Vector3(surface.x, floor + p.Y, surface.z) + normal * 0.004f;
                facing = Quaternion.LookRotation(normal, Vector3.up);
            }
            else if (p.Role == FitRole.Lamp && !p.Design)
            {
                if (!RoomMeasure.Ceiling(new Vector3(p.X, floor + 1.2f, p.Z), out Vector3 ceiling))
                {
                    return null;
                }
                at = ceiling - Vector3.up * 0.002f;
            }
            else if (p.Role == FitRole.Flat)
            {
                at.y += 0.003f;
            }
            Transform piece;
            Vector3 boxCentre, boxSize;
            if (p.Prefab.StartsWith(ModProp, StringComparison.Ordinal))
            {
                if (!ModSpec(p.Prefab, out SiteProps.Spec spec))
                {
                    return null;
                }
                piece = SiteProps.Make(spec, _root, out _);
                boxCentre = new Vector3(0f, spec.Size.y * 0.5f, 0f);
                boxSize = spec.Size;
            }
            else
            {
                piece = GameFurniture.Copy(p.Prefab, _root);
                if (piece == null)
                {
                    return null;
                }
                GameFurniture.Template(p.Prefab, out Bounds b);
                boxCentre = new Vector3(b.center.x, b.size.y * 0.5f, b.center.z);
                boxSize = new Vector3(b.size.x, Mathf.Max(0.5f, b.size.y), b.size.z);
            }
            piece.SetPositionAndRotation(at, facing);
            if (under != null && p.Role == FitRole.OnTop)
            {
                piece.SetParent(under, true);
            }
            if (r.Carve && p.Role == FitRole.Floor && p.HalfX * p.HalfZ * 4f >= 0.06f)   // (thin partitions too: they make the rooms)
            {
                var cut = piece.gameObject.AddComponent<NavMeshObstacle>();
                cut.shape = NavMeshObstacleShape.Box;
                cut.center = boxCentre;
                cut.size = boxSize;
                cut.carving = true;
                cut.carveOnlyStationary = true;
                cut.enabled = false;   // (all of them switched on together once the room stands, a few a frame)
                _cuts.Add(cut);
            }
            piece.gameObject.SetActive(true);
            return piece;
        }

        // The places to sit, on the seats the plan gave a use, with where somebody stands to sit down.
        private void Seats(Transform[] made)
        {
            _seats.Clear();
            for (int i = 0; i < _plan.Pieces.Count; i++)
            {
                FitPiece p = _plan.Pieces[i];
                Transform piece = made[i];
                if (piece == null || !p.Kept || (p.Use != FitUse.Desk && p.Use != FitUse.Meeting && p.Use != FitUse.Lounge && p.Use != FitUse.Waiting))
                {
                    continue;
                }
                GameFurniture.Seats(piece, _seatScratch);
                for (int k = 0; k < _seatScratch.Count && _seats.Count < 96; k++)
                {
                    Transform seat = _seatScratch[k];
                    if (RoomMeasure.Spot(seat.position + seat.forward * 0.55f, out Vector3 stand) || RoomMeasure.Spot(seat.position, out stand))
                    {
                        _seats.Add(new Seat { At = seat, Use = p.Use, Stand = stand, Chair = piece, Desk = p.Anchor >= 0 && p.Anchor < made.Length ? made[p.Anchor] : null });
                    }
                }
            }
            int mine = PlayerSeat();
            _seatsView.Clear();
            for (int i = 0; i < _seats.Count; i++)
            {
                if (i != mine)
                {
                    _seatsView.Add(new FitCast.SeatPlace { At = _seats[i].At, Use = _seats[i].Use, Stand = _seats[i].Stand });
                }
            }
        }

        // The player's desk among the seats at a desk with something on it, in the plan's order: the seed's pick (-1 for none, or none wanted).
        private int PlayerSeat()
        {
            _desk = null;
            if (_deskSeed < 0)
            {
                return -1;
            }
            int count = 0;
            for (int i = 0; i < _seats.Count; i++)
            {
                count += _seats[i].Use == FitUse.Desk && _seats[i].Desk != null ? 1 : 0;
            }
            int pick = FitDesks.Pick(count, _deskSeed);
            for (int i = 0; i < _seats.Count && pick >= 0; i++)
            {
                if (_seats[i].Use != FitUse.Desk || _seats[i].Desk == null || pick-- > 0)
                {
                    continue;
                }
                Seat s = _seats[i];
                _desk = new FitDesk { Seat = s.At, Desk = s.Desk, Chair = s.Chair, Stand = s.Stand, Place = _deskPlace, Kind = _request.Kind };
                return i;
            }
            return -1;
        }

        private readonly List<FitCast.SeatPlace> _seatsView = new List<FitCast.SeatPlace>(64);
        private readonly List<Vector3> _stopsView = new List<Vector3>(24);

        private List<Vector3> StopsView(Request r)
        {
            _stopsView.Clear();
            for (int i = 0; i < _plan.Stops.Count; i++)
            {
                if (RoomMeasure.Spot(new Vector3(_plan.Stops[i].X, r.Floor, _plan.Stops[i].Z), out Vector3 at))
                {
                    _stopsView.Add(at);
                }
            }
            return _stopsView;
        }

        // ---------------------------------------------------------------- the look

        // The design's walls and floors on the room, by the game's own call. Only while the player is in this unit: the
        // game writes a unit's look into the save only for one the player rents, and puts each unit's own look back on
        // the room every time it loads one.
        private void ApplyLook(Request r)
        {
            try
            {
                Transform room = r.Building.currentBuildingVersion;
                if (room == null)
                {
                    return;
                }
                _elements = room.GetComponentsInChildren<InteriorElement>(true);
                if (_elements.Length == 0)
                {
                    return;
                }
                BuildingManager.ApplyInteriorDesign(_set.interiorDesigns, _elements);
                _lookOn = r.Building.building;
            }
            catch (Exception e)
            {
                _logger?.Warn("Fit-out: " + r.Name + ": the game's walls and floors could not be put on (" + e.Message + "); it keeps the unit's own.");
                _lookOn = null;
            }
        }

        // The unit's own look back, if the player is still in it; out of it, the game puts it back when it next loads the unit.
        private void RestoreLook()
        {
            Buildings.Building on = _lookOn;
            InteriorElement[] elements = _elements;
            _lookOn = null;
            _elements = null;
            if (on == null || elements == null)
            {
                return;
            }
            try
            {
                BuildingManager buildings = InstanceBehavior<BuildingManager>.Instance;
                if (BuildingManager.IsInsideBuilding && buildings != null && buildings.building == on && !buildings.exitingBuilding)
                {
                    BuildingManager.ApplyInteriorDesign(on, elements);
                }
            }
            catch (Exception e)
            {
                _logger?.Warn("Fit-out: the unit's own walls and floors could not be put back (" + e.Message + "); the game puts them back when it next loads the unit.");
            }
        }

        // ---------------------------------------------------------------- once a second, and away

        /// <summary>Once a second while the player is in the room: the people.</summary>
        public void Tick(float now)
        {
            if (_root != null && Ready && !_hidden)
            {
                _cast.Tick(now);
            }
        }

        /// <summary>Hidden (the game's placement mode in a place of the player's) and shown again; the people go meanwhile.</summary>
        public void Hide(bool hidden)
        {
            if (_root == null || hidden == _hidden)
            {
                return;
            }
            _hidden = hidden;
            _root.gameObject.SetActive(!hidden);
            if (hidden)
            {
                _cast.End();
            }
            else if (Ready && _request != null && _request.People && Settings.People)
            {
                _cast.Begin(_request.Kind, _seatsView, _stopsView, _request.Door);
            }
        }

        private void Say(Request r, int placed)
        {
            _said.Length = 0;
            _said.Append(r.Name).Append(": ");
            if (_setName != null && r.DesignKinds.Count > 0)
            {
                _said.Append("the game's design ").Append(_setName).Append(": ").Append(_plan.KeptDesign).Append(" of ").Append(_plan.DesignOffered)
                    .Append(" pieces (").Append(_plan.DesignClashed).Append(" where something stands, ").Append(_plan.DesignCut).Append(" for the walkways, ")
                    .Append(_plan.DesignCapped).Append(" over the most").Append(_plan.DesignGivenUp ? "; given up" : string.Empty).Append("); ");
            }
            else
            {
                _said.Append(r.DesignKinds.Count > 0 ? "no design of the game's for this unit; " : string.Empty);
                if (_setName != null && _lookOn != null)
                {
                    _said.Append("the walls and floors of ").Append(_setName).Append("; ");
                }
            }
            _said.Append(_plan.Zones).Append(" rooms of the mod's (");
            bool first = true;
            for (int t = 0; t < FitPlanner.ZoneTypeCount; t++)
            {
                if (_plan.ZoneCounts[t] > 0)
                {
                    _said.Append(first ? string.Empty : ", ").Append(_plan.ZoneCounts[t]).Append(' ').Append(((FitPlanner.ZoneType)t).ToString().ToLowerInvariant());
                    first = false;
                }
            }
            _said.Append("); ").Append(placed).Append(" pieces stand, ").Append(_cuts.Count).Append(" cut out of the floor; ")
                .Append(_plan.BareAfter.ToString("F0")).Append(" of ").Append(_plan.FloorArea.ToString("F0")).Append(" square metres bare (")
                .Append(_plan.BareBefore.ToString("F0")).Append(" before); ").Append(_seats.Count).Append(" seats, ").Append(_stopsView.Count).Append(" places to walk to; ")
                .Append(_plan.Unreached == 0 && _plan.Pockets == 0 ? "every way open" : _plan.Unreached + " places not reached, " + _plan.Pockets + " closed off")
                .Append("; built in ").Append(_frames).Append(" frames, ").Append(_workMs.ToString("F0")).Append(" ms of work");
        }

        /// <summary>The player has left (or the part is going): the people sent away, the pieces destroyed, the room's own look back. Safe at any time.</summary>
        public void End()
        {
            try
            {
                if (_running != null && _host != null)
                {
                    _host.StopCoroutine(_running);
                }
                _cast.End();
                RestoreLook();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _running = null;
            if (_deskPlace != null)
            {
                FitDesks.Down(_deskPlace);
            }
            _deskPlace = null;
            _deskSeed = -1;
            _desk = null;
            _cuts.Clear();
            _seats.Clear();
            _seatsView.Clear();
            _stopsView.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            _root = null;
            _request = null;
            _set = null;
            _hidden = false;
            Ready = false;
        }
    }
}
