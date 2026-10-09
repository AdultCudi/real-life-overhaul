using System;
using Streets;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Help-wanted boards: the modelled <c>job_board</c> stood against the wall beside the door
    /// of a few of the city's own service buildings (the first building of each kind of special
    /// service, so the same places in every game). Standing in front of one and pressing the
    /// interaction key opens the list the Gig Line reads out on the phone.
    ///
    /// The places are looked for once per city load, a few buildings a second while no job is
    /// on, and only once the prop bundle has the board: with no board to stand there, there are
    /// none and the phone is the way to find work. A place is a stretch of real wall with
    /// pavement in front of it and nothing standing in it (CitySpots.Shopfront), a step aside
    /// from the door. A board is one object with a shared mesh and material, no collider and no
    /// script; nothing here runs unless the director calls it, once a second.
    /// </summary>
    public sealed class JobBoards
    {
        private const int Most = 6;
        private const int PerTick = 30;          // buildings looked at per second while searching
        private const float FromWall = 0.30f;    // metres from the wall the board's middle stands
        private const float Reach = 1.5f;        // how near its front the player must be to read it (the door beside it keeps its own key)
        private const float Aside = 2.4f;        // metres along the wall from the door
        private const string PropId = "job_board";

        private readonly JobProps _props;
        private readonly IModLogger _logger;
        private readonly List<Transform> _boards = new List<Transform>(Most);
        private readonly List<Vector3> _fronts = new List<Vector3>(Most);       // where the reader stands
        private readonly HashSet<string> _kinds = new HashSet<string>();
        private readonly List<string> _kindOf = new List<string>(Most);        // the kind of service building each board stands at
        private readonly List<Vector3> _outward = new List<Vector3>(Most);     // the way each board faces: away from its wall
        private readonly List<string> _names = new List<string>(Most);
        private int _next;                   // the next building to look at
        private bool _searched;
        private bool _failed;
        private string _prompt;

        public JobBoards(JobProps props, IModLogger logger)
        {
            _props = props;
            _logger = logger;
        }

        /// <summary>True while the player stands in front of a board.</summary>
        public bool InReach { get; private set; }

        public string Prompt => _prompt ?? (_prompt = JobText.T("reallife_job_board_prompt"));

        public int Count => _boards.Count;

        /// <summary>True once every building has been looked at: the boards there are, are all there will be.</summary>
        public bool Searched => _searched || _failed;

        /// <summary>Where the reader of a board stands.</summary>
        public Vector3 Front(int board) => _fronts[board];

        /// <summary>The board itself, on the ground against its wall.</summary>
        public Vector3 At(int board) => _boards[board] != null ? _boards[board].position : _fronts[board];

        /// <summary>The way a board faces: out from its wall.</summary>
        public Vector3 Outward(int board) => _outward[board];

        /// <summary>The game's name for the kind of service building a board stands at.</summary>
        public string Kind(int board) => _kindOf[board];

        /// <summary>The address of the building a board stands at, as the game writes it.</summary>
        public string Address(int board) => _names[board];

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
            for (int i = 0; i < _boards.Count; i++)
            {
                _props.Return(_boards[i]);
            }
            _boards.Clear();
            _fronts.Clear();
            _kindOf.Clear();
            _names.Clear();
            _outward.Clear();
            InReach = false;
            Near = -1;
        }

        private void Search()
        {
            if (!PropLibrary.Ready)
            {
                return;   // still loading (or never will be: then there are no boards)
            }
            if (!PropLibrary.TryGet(PropId, out _, out _))
            {
                _searched = true;
                _logger?.Info("Jobs: the prop bundle has no help-wanted board; work is found on the phone only.");
                return;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] buildings = city != null ? city.cityBuildingControllers : null;
            if (buildings == null)
            {
                return;
            }
            for (int looked = 0; looked < PerTick && _next < buildings.Length && _boards.Count < Most; looked++, _next++)
            {
                CityBuildingController building = buildings[_next];
                if (building == null || building.building == null || building.building.SpecialService == null
                    || building.building.locatedInsidePrivateFenceIndex >= 0)
                {
                    continue;
                }
                string kind = building.building.SpecialService.businessTypeName;
                if (string.IsNullOrEmpty(kind) || _kinds.Contains(kind) || !Place(building, kind))
                {
                    continue;
                }
                _kinds.Add(kind);
            }
            if (_next >= buildings.Length || _boards.Count >= Most)
            {
                _searched = true;
                _logger?.Info("Jobs: " + _boards.Count + " help-wanted board(s) stand outside service buildings (" + string.Join(", ", _kinds) + ").");
            }
        }

        // Against the wall a step to one side of the door, facing the pavement, where nothing else stands.
        private bool Place(CityBuildingController building, string kind)
        {
            if (!JobSpots.Front(building, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return false;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                // Measured 0.75 m out from the wall with 0.6 m of room: the strip the board stands in is inside what was found clear.
                if (!CitySpots.Shopfront(front + along * (side * Aside), FromWall + 0.45f, 0.6f, out Vector3 stand, out Vector3 outward))
                {
                    continue;
                }
                Vector3 at = stand - outward * 0.45f;
                if (!CitySpots.Ground(at, out float ground) || Mathf.Abs(ground - stand.y) > 0.25f || JobSpots.Flat(at, front) < 1.2f)
                {
                    continue;
                }
                at.y = ground;
                Transform board = _props.Stand(PropId, at, outward);
                if (board == null)
                {
                    return false;
                }
                _boards.Add(board);
                _fronts.Add(stand + outward * 0.5f);
                _kindOf.Add(kind);
                _names.Add(building.building.Address.ToFormattedString());
                _outward.Add(outward);
                return true;
            }
            return false;
        }
    }
}
