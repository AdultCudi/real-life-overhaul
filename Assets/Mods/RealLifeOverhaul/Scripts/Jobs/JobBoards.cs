using System;
using System.Collections.Generic;
using BAModAPI;
using Buildings;
using Helpers;
using Localizor;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Help-wanted boards beside the door of the first building of each kind of special service (the kinds being each employer's own, see
    /// <see cref="BoardHomeRules.Kinds"/>), the same places in every game; at one, the interact key opens the Gig Line's list. Looked for once
    /// a city load, a few buildings a second, the boards put up when the whole list has been seen. A kind whose buildings all failed their
    /// asks (a car parked at the door, the pavement full of people, the street not streamed in yet) is asked again every
    /// <see cref="RetryEvery"/> seconds while the player is within <see cref="RetryReach"/> of one of its buildings, a board at a time.
    /// A board that was put up at a spare building because the first of its kind failed its asks is only there for a while: it is asked at the
    /// better buildings again every <see cref="RetryEvery"/> seconds and taken down and put up at the first that takes it, out of the player's sight.
    /// </summary>
    public sealed class JobBoards
    {
        private const int Most = 6;              // boards at most
        private const int Spares = 6;            // buildings kept per kind, to fall back on when the first can never take a board
        private const int Tries = 3;             // asks of one building (a second apart) before the next of its kind is used
        private const int PerTick = 150;         // buildings looked at a second while searching (a null check for most)
        private const float RetryEvery = 10f;    // seconds between two asks for a board no building of its kind could take
        private const float RetryReach = 120f;   // metres from the player a building is asked again: its street is in play, its parked cars and people real
        private const float FromWall = 0.30f;    // metres from the wall the board's middle stands
        private const float Reach = 1.5f;        // metres from its front to read it (the door beside it keeps its own key)
        private const float Aside = 2.4f;        // metres along the wall from the door
        private const string PropId = "job_board";
        private const float CullRadius = 1.5f;   // metres round a board's foot: all of it (Performance/ModCulling)

        private readonly JobProps _props;
        private readonly IModLogger _logger;
        private readonly List<Transform> _boards = new List<Transform>(Most);
        private readonly List<CullEntry> _culls = new List<CullEntry>(Most);   // the boards, not drawn beyond the game's far band
        private readonly List<Vector3> _fronts = new List<Vector3>(Most);       // where the reader stands
        private readonly HashSet<string> _kinds = new HashSet<string>();
        private readonly List<string> _kindOf = new List<string>(Most);        // the kind of service building each board stands at
        private readonly List<Vector3> _outward = new List<Vector3>(Most);     // the way each board faces: away from its wall
        private readonly List<string> _names = new List<string>(Most);
        private readonly List<string> _places = new List<string>(Most);        // the name of the shop or service each board stands outside
        private readonly List<CityBuildingController> _buildings = new List<CityBuildingController>(Most);   // the building each board stands at
        private readonly Dictionary<string, List<CityBuildingController>> _candidates = new Dictionary<string, List<CityBuildingController>>();   // a kind's buildings, in the city's list order
        private readonly List<string> _seen = new List<string>();    // the kinds in the order the city's list first shows them
        private readonly List<string> _order = new List<string>();   // the kinds a board is put up for, each employer's own first (BoardHomeRules.Kinds): the order the boards are put up in
        private readonly List<int> _rank = new List<int>(Most);       // which of its kind's buildings each board stands at: 0 is the first, its home
        private readonly List<int> _farFails = new List<int>(Most);   // asks of a better building the player was far from that failed, for each board
        private readonly List<string> _chosen = new List<string>(Most);
        private float _mendAt;               // when the next spare board is asked at a better building (unscaled seconds)
        private int _mendCursor;             // which board is looked at next
        private bool _ordered;
        private readonly List<string> _late = new List<string>(Most);   // kinds every building of which failed its asks: no board yet, asked again near the player
        private float _retryAt;              // when the next of those is asked (unscaled seconds)
        private int _lateCursor;             // which building of them is asked next, so the first one near is not asked every time
        private int _head;                   // the kind being put up
        private int _spare;                  // which of its buildings is being tried
        private int _tries;                  // failed tries at that building
        private int _next;                   // the next building to look at
        private bool _searched;
        private bool _failed;

        public JobBoards(JobProps props, IModLogger logger)
        {
            _props = props;
            _logger = logger;
        }

        public bool InReach { get; private set; }

        public int Count => _boards.Count;

        /// <summary>How many times a board was taken down and put up at its home again.</summary>
        public int Moves { get; private set; }

        /// <summary>Changes whenever a board is put up or moves: what the city map and the offices list wait on to be drawn again.</summary>
        public int Stamp => 1 + _boards.Count + 64 * Moves;

        /// <summary>True once every building has been looked at: no more boards will be put up.</summary>
        public bool Searched => _searched || _failed;

        /// <summary>Where the reader of a board stands.</summary>
        public Vector3 Front(int board) => _fronts[board];

        /// <summary>The board itself, on the ground against its wall.</summary>
        public Vector3 At(int board) => _boards[board] != null ? _boards[board].position : _fronts[board];

        public Vector3 Outward(int board) => _outward[board];

        /// <summary>The game's name for the kind of service building a board stands at.</summary>
        public string Kind(int board) => _kindOf[board];

        /// <summary>The address of the building a board stands at, as the game writes it.</summary>
        public string Address(int board) => _names[board];

        /// <summary>The name of the shop or service a board stands outside, as the player reads it over the door (never empty).</summary>
        public string Place(int board) => _places[board];

        /// <summary>The building a board stands at (it lights up on the city map while its row is ticked).</summary>
        public CityBuildingController Building(int board) => _buildings[board];

        /// <summary>
        /// True while a board can be met: it is placed, its building is still in the city and the pavement in front of it was found
        /// clear when it was put up. A review or a shift is only ever sent to a board for which this holds (CareerDirector.WhereOf).
        /// </summary>
        public bool Stands(int board) => board >= 0 && board < _boards.Count && board < _buildings.Count && _buildings[board] != null;

        /// <summary>The board the player stands in front of, or -1.</summary>
        public int Near { get; private set; } = -1;

        /// <summary>The player is not on foot in the street: no board is in reach.</summary>
        public void Away()
        {
            InReach = false;
            Near = -1;
        }

        /// <summary>Once a second while no job is on and the player is on foot out of doors.</summary>
        public void Tick(Vector3 player)
        {
            if (_failed)
            {
                InReach = false;
                Near = -1;
                return;
            }
            try
            {
                if (!_searched)
                {
                    Search();
                }
                else
                {
                    if (_late.Count > 0 && _boards.Count < Most)
                    {
                        Retry(player);
                    }
                    Mend(player);
                }
                bool near = false;
                Near = -1;
                for (int i = 0; i < _fronts.Count && !near; i++)
                {
                    Vector3 offset = _fronts[i] - player;
                    near = offset.y > -2f && offset.y < 2f && offset.x * offset.x + offset.z * offset.z < Reach * Reach;
                    if (near)
                    {
                        Near = i;
                    }
                }
                InReach = near;
            }
            catch (Exception e)
            {
                _failed = true;
                InReach = false;
                Near = -1;
                _logger?.Error(e);
                _logger?.Warn("Jobs: the help-wanted boards are off for this session after the error above; the Gig Line still has every job.");
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _culls.Count; i++)
            {
                ModCulling.Remove(_culls[i]);   // drawn again before it goes back on the shelf
            }
            _culls.Clear();
            for (int i = 0; i < _boards.Count; i++)
            {
                _props.Return(_boards[i]);
            }
            _boards.Clear();
            _fronts.Clear();
            _kindOf.Clear();
            _names.Clear();
            _places.Clear();
            _buildings.Clear();
            _outward.Clear();
            _candidates.Clear();
            _seen.Clear();
            _order.Clear();
            _rank.Clear();
            _farFails.Clear();
            _late.Clear();
            InReach = false;
            Near = -1;
        }

        private void Search()
        {
            if (!PropLibrary.Ready)
            {
                return;   // still loading, or never will be: then there are no boards
            }
            if (!PropLibrary.TryGet(PropId, out _, out _))
            {
                _searched = true;
                _logger?.Info("Jobs: the prop bundle has no help-wanted board; work is found on the phone only.");
                return;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = GameDlc.Open(city);
            if (buildings == null)
            {
                return;
            }
            // Which building stands for a kind is settled by the city's own list alone (the first of its kind, then the next ones as
            // spares), never by whether the pavement happened to be clear on the second it was asked: a board that fell through to a
            // building across the map whenever a car stood at the first one was moved by the weather of the load.
            for (int looked = 0; looked < PerTick && _next < buildings.Length; looked++, _next++)
            {
                CityBuildingController building = buildings[_next];
                if (building == null || building.building == null || building.building.SpecialService == null
                    || GameMembers.Fenced(building.building) || !JobSpots.DoorOf(building, out _))
                {
                    continue;
                }
                string kind = building.building.SpecialService.businessTypeName;
                if (string.IsNullOrEmpty(kind))
                {
                    continue;
                }
                if (!_candidates.TryGetValue(kind, out List<CityBuildingController> list))
                {
                    list = new List<CityBuildingController>(Spares);
                    _candidates[kind] = list;
                    _seen.Add(kind);
                }
                if (list.Count < Spares)
                {
                    list.Add(building);
                }
            }
            if (_next < buildings.Length)
            {
                return;   // the kinds an employer wants are chosen from the whole list, so nothing is put up before it has been seen
            }
            if (!_ordered)
            {
                _ordered = true;
                Employer[] all = CareerCatalogue.All;
                var premises = new string[all.Length][];
                for (int e = 0; e < all.Length; e++)
                {
                    premises[e] = all[e].Premises;
                }
                BoardHomeRules.Kinds(premises, _seen, Most, _chosen);
                _order.AddRange(_chosen);
            }
            while (_head < _order.Count && _boards.Count < Most)
            {
                string kind = _order[_head];
                List<CityBuildingController> list = _candidates[kind];
                if (_spare >= list.Count)
                {
                    _late.Add(kind);   // every building of this kind failed its asks: it has no board for now, and is asked again near the player (Retry)
                    _head++;
                    _spare = 0;
                    _tries = 0;
                    continue;
                }
                if (Place(list[_spare], kind, _spare))
                {
                    _kinds.Add(kind);
                    _head++;
                    _spare = 0;
                    _tries = 0;
                    continue;
                }
                // The first building of its kind is asked again on the next tries, since a passer-by or a parked car is gone in seconds;
                // only a building that fails every time gives the board to the next of its kind.
                if (++_tries >= Tries)
                {
                    _spare++;
                    _tries = 0;
                }
                break;
            }
            if (_boards.Count >= Most || _head >= _order.Count)
            {
                _searched = true;
                _logger?.Info("Jobs: " + _boards.Count + " help-wanted board(s) stand outside service buildings (" + string.Join(", ", _kinds) + ")"
                              + (_late.Count > 0 ? "; none yet at " + string.Join(", ", _late) + ", asked again near the player." : "."));
            }
        }

        // Once a RetryEvery while a kind has no board: the next building of those within reach, a board at a time. The building is the same
        // one the search settled on (the city's list order), so a board put up late stands where it would have stood at the first.
        private void Retry(Vector3 player)
        {
            float now = Time.unscaledTime;
            if (now < _retryAt)
            {
                return;
            }
            _retryAt = now + RetryEvery;
            int total = 0;
            for (int k = 0; k < _late.Count; k++)
            {
                total += _candidates[_late[k]].Count;
            }
            for (int n = 0; n < total; n++)
            {
                int at = (_lateCursor + n) % total;
                int kind = 0;
                while (at >= _candidates[_late[kind]].Count)
                {
                    at -= _candidates[_late[kind]].Count;
                    kind++;
                }
                CityBuildingController building = _candidates[_late[kind]][at];
                if (!JobSpots.DoorOf(building, out Vector3 door) || JobSpots.Flat(door, player) > RetryReach)
                {
                    continue;
                }
                _lateCursor = (_lateCursor + n + 1) % total;
                string name = _late[kind];
                if (Place(building, name, at))
                {
                    _kinds.Add(name);
                    _late.RemoveAt(kind);
                    _logger?.Info("Jobs: the help-wanted board at " + building.building.Address.ToFormattedString() + " (" + name + ") is up, put there on a later ask.");
                }
                return;
            }
        }

        // What is over the door: the service's own name (a furniture store's brand), else the game's name for its kind of business.
        private static string PlaceName(SpecialService service, string kind)
        {
            string name = service != null ? service.businessName : null;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = kind;
            }
            return LocalizorManager.IsLocalizedKey(name) ? name.GetLocalization() : name;
        }

        // Against the wall a step to one side of the door, facing the pavement, where nothing else stands. False where no side has room now.
        private bool Find(CityBuildingController building, out Vector3 at, out Vector3 stand, out Vector3 outward)
        {
            at = default;
            stand = default;
            outward = default;
            if (!JobSpots.Front(building, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return false;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                // Measured 0.75 m out from the wall with 0.6 m of room, so the board's strip is inside what was found clear.
                if (!CitySpots.Shopfront(front + along * (side * Aside), FromWall + 0.45f, 0.6f, out stand, out outward))
                {
                    continue;
                }
                at = stand - outward * 0.45f;
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || JobSpots.Flat(at, front) < 1.2f)
                {
                    continue;
                }
                at.y = ground;
                return true;
            }
            return false;
        }

        // <paramref name="rank"/> is the building's place in its kind's list: above 0 the board is a spare one, to go back to a better building (Mend).
        private bool Place(CityBuildingController building, string kind, int rank)
        {
            if (!Find(building, out Vector3 at, out Vector3 stand, out Vector3 outward))
            {
                return false;
            }
            Transform board = _props.Stand(PropId, at, outward);
            if (board == null)
            {
                return false;
            }
            _boards.Add(board);
            _culls.Add(ModCulling.Add(CullRules.Kind.JobBoard, board, at, CullRadius));
            _fronts.Add(stand + outward * 0.5f);
            _kindOf.Add(kind);
            _names.Add(building.building.Address.ToFormattedString());
            _places.Add(PlaceName(building.building.SpecialService, kind));
            _buildings.Add(building);
            _outward.Add(outward);
            _rank.Add(rank);
            _farFails.Add(0);
            return true;
        }

        // Once a RetryEvery, one board that stands at a spare building: the better buildings of its kind, in the city's list order, are asked
        // again, since what blocked the first (a parked car, people, a street not yet in play) was never a reason for it to be anywhere else.
        // A better building is asked from any distance a few times and then only with the player near; the board moves only out of his sight.
        private void Mend(Vector3 player)
        {
            float now = Time.unscaledTime;
            if (now < _mendAt || _boards.Count == 0)
            {
                return;
            }
            _mendAt = now + RetryEvery;
            for (int step = 0; step < _boards.Count; step++)
            {
                int board = (_mendCursor + step) % _boards.Count;
                if (_rank[board] <= 0)
                {
                    continue;
                }
                _mendCursor = (board + 1) % _boards.Count;
                List<CityBuildingController> list = _candidates[_kindOf[board]];
                for (int c = 0; c < _rank[board] && c < list.Count; c++)
                {
                    CityBuildingController home = list[c];
                    if (home == null || !JobSpots.DoorOf(home, out Vector3 door))
                    {
                        continue;
                    }
                    bool near = JobSpots.Flat(door, player) <= RetryReach;
                    if (!BoardHomeRules.Mends(_rank[board], _farFails[board], near))
                    {
                        continue;
                    }
                    if (!Find(home, out Vector3 at, out Vector3 stand, out Vector3 outward))
                    {
                        _farFails[board] += near ? 0 : 1;
                        continue;
                    }
                    if (BoardHomeRules.MayMove(JobSpots.Flat(_fronts[board], player), JobSpots.Flat(stand, player)))
                    {
                        Move(board, home, c, at, stand, outward);
                    }
                    return;   // found a better home: moved now, or when the player is away from both
                }
                return;
            }
        }

        private void Move(int board, CityBuildingController home, int rank, Vector3 at, Vector3 stand, Vector3 outward)
        {
            Transform made = _props.Stand(PropId, at, outward);
            if (made == null)
            {
                return;
            }
            string was = _names[board];
            ModCulling.Remove(_culls[board]);
            _props.Return(_boards[board]);
            _boards[board] = made;
            _culls[board] = ModCulling.Add(CullRules.Kind.JobBoard, made, at, CullRadius);
            _fronts[board] = stand + outward * 0.5f;
            _names[board] = home.building.Address.ToFormattedString();
            _places[board] = PlaceName(home.building.SpecialService, _kindOf[board]);
            _buildings[board] = home;
            _outward[board] = outward;
            _rank[board] = rank;
            _farFails[board] = 0;
            Moves++;
            _logger?.Info("Jobs: the help-wanted board for " + _kindOf[board] + " went back from " + was + " to " + _names[board] + ", the building the city's list gives it first.");
        }
    }
}
