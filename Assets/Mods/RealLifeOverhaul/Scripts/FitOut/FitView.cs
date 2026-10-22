using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using BAModAPI;
using RealLifeOverhaul.Tech;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The floor of one of the mod's buildings fitted out while the player is in it, from the room the caller measured and
    /// what it stands there: the game's design read on a worker thread (<see cref="FitDesigns"/>), a template of each piece
    /// (Core/GameFurniture), the plan on a worker thread (<see cref="FitPlanner"/>), copies stood nearest the door first and
    /// cut out of the walkable floor, then the people (<see cref="FitCast"/>) and the player's desk (<see cref="FitDesks"/>).
    /// The cost: a frame takes at most <see cref="BudgetMs"/> of the work in play, <see cref="HiddenBudgetMs"/> behind the
    /// black screen; once it stands only the cast's look once a second. Plain local objects, destroyed when the player
    /// leaves: never items, never saved, nothing sent over Going Public's connection.
    /// </summary>
    internal sealed partial class FitView
    {
        public const float BudgetMs = 2.5f;           // milliseconds of a played frame the building may take
        public const float HiddenBudgetMs = 28f;      // milliseconds of a frame behind the black screen
        private const int CutsPerFrame = 24;          // pieces cut out of the walkable floor a frame
        private const float WaitForDesign = 8f;       // seconds the reading of a design is waited for, at most
        private const float CarveWait = 3f;           // real seconds the navigation system is waited for to cut the floor, at most
        private const float CarveProbeLeast = 1.0f;   // metres a cut piece is across at least to tell by its middle that it is cut
        private const string ModProp = "rlo:";

        /// <summary>What a caller asks for: the room, the building's kind, what is there already and what to furnish from.</summary>
        public sealed class Request
        {
            public string Name;
            public FitKind Kind;
            public int Seed;
            public BuildingManager Building;
            public string Place;                                              // the place key for the player's desk; null: the building's
            public RoomGrid Grid;
            public float Floor;
            public Vector3 Door, Inward;
            public readonly List<string> DesignKinds = new List<string>(8);   // the kinds of business whose designs may furnish it, in order
            public readonly List<string> LookKinds = new List<string>(4);     // and whose walls and floors alone may be put on it
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

        /// <summary>Fits out the request's room a little each frame on <paramref name="host"/>, reading the request until End; false with no room measured.</summary>
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
            _running = host.StartCoroutine(Guard.Routine(Build()));
            return true;
        }

        private IEnumerator Build()
        {
            var watch = new Stopwatch();
            Request r = _request;
            FitSite site = r.Site;
            Buildings.Building building = r.Building.building;

            // the game's design for this very unit, read on a worker thread
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

            // the pieces wanted, each made ready once a session: the design's and the building kind's
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

            // the sizes, the design as pieces of a plan, and the plan on a worker thread
            site.Kind = r.Kind;
            site.Seed = r.Seed;
            site.DoorX = r.Door.x;
            site.DoorZ = r.Door.z;
            site.InX = r.Inward.x;
            site.InZ = r.Inward.z;
            site.Erosion = Erosion();
            Exits(r, site);
            // each visit its own design and plan: a worker left over from the last writes only into its own
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

            // the design's walls and floors, by the game's own call
            if (r.Look && _set != null && _set.interiorDesigns != null && _set.interiorDesigns.Count > 0)
            {
                ApplyLook(r);
            }

            // the pieces, nearest the door first
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
                // what stands on a piece goes on once it stands: its parent is earlier in the plan, not always in the order
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
            // cut out of the walkable floor a few a frame; the navigation system cuts at its next update
            bool probing = CarveProbe(r, out Vector3 probe);
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
            // the seats are measured on the floor as cut: a place to stand under a desk is no place to walk to
            float carvedBy = Time.realtimeSinceStartup + CarveWait;
            for (int after = 0; _cuts.Count > 0 && after < 2; after++)
            {
                _frames++;
                yield return null;   // the navigation system's next updates, for the last few switched on
            }
            while (probing && Time.realtimeSinceStartup < carvedBy && NavMesh.SamplePosition(probe, out _, 0.3f, NavMesh.AllAreas))
            {
                _frames++;
                yield return null;
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
                FitDesks.Down(_deskPlace);   // no desk with a seat stands: the careers look elsewhere
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

        // A step that may meet something of the game's gone: the fault said once and the floor left as it is.
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

        // A frame's share of the work, counted before the next frame is waited for.
        private void Lap(Stopwatch watch)
        {
            _workMs += watch.Elapsed.TotalMilliseconds;
            _frames++;
        }

        // The seats the plan gave a use, with where somebody stands to sit down.
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

        // The player's seat among those at a desk: the seed's pick if it can be walked to from the door, else the next
        // round that can; -1 for none, or none wanted.
        private int PlayerSeat()
        {
            _desk = null;
            _deskOf = _deskShift = 0;
            if (_deskSeed < 0)
            {
                return -1;
            }
            _deskSeats.Clear();
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i].Use == FitUse.Desk && _seats[i].Desk != null)
                {
                    _deskSeats.Add(i);
                }
            }
            _deskOf = _deskSeats.Count;
            int pick = FitDesks.Pick(_deskSeats.Count, _deskSeed);
            Request r = _request;
            Vector3 from = r.Door + r.Inward * 0.6f;
            Vector3 stand = Vector3.zero;
            int chosen = DeskReach.First(pick, _deskSeats.Count, k =>
            {
                Seat s = _seats[_deskSeats[k]];
                return FitReach.StandBy(s.At, s.Desk, from, r.Floor, out stand);
            });
            if (chosen < 0)
            {
                _deskShift = -1;
                return -1;
            }
            _deskShift = (chosen - pick + _deskSeats.Count) % _deskSeats.Count;
            int at = _deskSeats[chosen];
            Seat mine = _seats[at];
            mine.Stand = stand;
            _seats[at] = mine;
            _desk = new FitDesk { Seat = mine.At, Desk = mine.Desk, Chair = mine.Chair, Stand = stand, Place = _deskPlace, Kind = _request.Kind };
            return at;
        }

        private readonly List<int> _deskSeats = new List<int>(32);
        private int _deskOf, _deskShift;   // the floor's desks, and how far round from the seed's pick the player's is (-1: none)

        // The middle of the largest piece to be cut, walkable now: once it is not, the floor is cut.
        private bool CarveProbe(Request r, out Vector3 probe)
        {
            probe = Vector3.zero;
            float best = 0f;
            for (int k = 0; k < _cuts.Count; k++)
            {
                NavMeshObstacle cut = _cuts[k];
                if (cut == null || Mathf.Min(cut.size.x, cut.size.z) < CarveProbeLeast || cut.size.x * cut.size.z <= best)
                {
                    continue;
                }
                Vector3 middle = cut.transform.TransformPoint(cut.center);
                middle.y = r.Floor;
                if (NavMesh.SamplePosition(middle, out NavMeshHit hit, 0.3f, NavMesh.AllAreas))
                {
                    best = cut.size.x * cut.size.z;
                    probe = hit.position;
                }
            }
            return best > 0f;
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

        // By the game's own call, only while the player is in the unit: the game saves a unit's look only for one the
        // player rents, and puts each unit's own look back every time it loads one.
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

        // If the player has left, the game puts the look back when it next loads the unit.
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

        /// <summary>Once a second while the player is in the room: the people.</summary>
        public void Tick(float now)
        {
            if (_root != null && Ready && !_hidden)
            {
                _cast.Tick(now);
            }
        }

        /// <summary>Hidden for the game's placement mode and shown again; the people go meanwhile.</summary>
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
                .Append(_plan.Unreached == 0 && _plan.Pockets == 0 ? "every way open" : _plan.Unreached + " places not reached, " + _plan.Pockets + " closed off");
            if (_deskSeed >= 0)
            {
                _said.Append("; the player's desk: ").Append(_desk == null
                    ? "none of the " + _deskOf + " desks can be walked to from the door"
                    : _deskShift == 0 ? "walked to from the door" : "the seed's own cannot be walked to from the door, so the " + _deskShift + " next round of " + _deskOf);
            }
            _said.Append("; built in ").Append(_frames).Append(" frames, ").Append(_workMs.ToString("F0")).Append(" ms of work");
        }

        /// <summary>The people sent away, the pieces destroyed, the room's own look back. Safe at any time.</summary>
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
