using System;
using System.Text;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A few tech fittings in the player's tech office while they are in it, placed by <see cref="OfficeFitPlan"/>. Only to be seen.</summary>
    internal sealed class OfficeView
    {
        public const int Most = 3;

        private readonly IModLogger _logger;
        private readonly OfficeFit _plan = new OfficeFit();
        private readonly StringBuilder _said = new StringBuilder(96);
        private Transform _root;
        private int _items;
        private bool _hidden;

        public OfficeView(IModLogger logger)
        {
            _logger = logger;
        }

        public bool Active => _root != null;

        public string Said => _said.ToString();

        public bool Begin(BuildingManager building, string name)
        {
            End();
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (building == null || player == null || !Shapes.Available)
            {
                return false;
            }
            if (!RoomMeasure.Door(building, player.transform.position, player.transform.forward, out Vector3 door, out Vector3 _))
            {
                return false;
            }
            RoomGrid grid = RoomMeasure.Measure(building, door, true, out float floor);
            if (grid == null)
            {
                return false;
            }
            OfficeFitPlan.Plan(grid, door.x, door.z, Most, _plan);
            _items = RoomMeasure.Items(building);
            _root = new GameObject("RealLifeOverhaul Tech Office").transform;
            int placed = 0;
            for (int i = 0; i < _plan.Pieces.Count; i++)
            {
                SitePiece p = _plan.Pieces[i];
                if (!SiteProps.Of(p.Kind, out SiteProps.Spec spec))
                {
                    continue;
                }
                if (spec.Wall)
                {
                    // hung by its back on the wall's real surface, its middle 0.6 m over the plan's height
                    Vector3 face = RoomMeasure.Facing(p);
                    var at = new Vector3(p.X, floor + p.Y + spec.Size.y * 0.5f, p.Z);
                    if (!RoomMeasure.Wall(at, -face, 1.0f, out Vector3 surface, out Vector3 normal))
                    {
                        continue;
                    }
                    Transform board = SiteProps.Make(spec, _root, out _);
                    board.SetPositionAndRotation(new Vector3(surface.x, at.y, surface.z) + normal * 0.004f, Quaternion.LookRotation(normal, Vector3.up));
                    placed++;
                    continue;
                }
                Transform piece = SiteProps.Make(spec, _root, out _);
                RoomMeasure.Stand(piece, p, floor);
                placed++;
            }
            _said.Length = 0;
            _said.Append(name).Append(": ").Append(placed).Append(" tech fittings").Append(_plan.Why != null ? " (" + _plan.Why + ")" : string.Empty);
            return true;
        }

        /// <summary>Once a second: true if the fittings must be placed again, the furniture having changed or placement mode ended.</summary>
        public bool Tick(BuildingManager building)
        {
            if (_root == null)
            {
                return false;
            }
            bool placing = BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode;
            if (placing != _hidden)
            {
                _hidden = placing;
                _root.gameObject.SetActive(!placing);
                return !placing;
            }
            return !_hidden && RoomMeasure.Items(building) != _items;
        }

        public void End()
        {
            try
            {
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root.gameObject);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _root = null;
            _hidden = false;
        }
    }
}
