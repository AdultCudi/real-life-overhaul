using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using Localizor;
using PlayerActivity;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The floors of the tech companies' towers on the game's own panel, at a tower's door and at the lift inside. Nothing is saved.
    /// Cost: a look once a second, the panel's texts made only when they change; nothing per frame.
    /// </summary>
    internal sealed class TowerFloors
    {
        private const float DoorReach = 3.2f;   // metres from the pavement in front of a tower's door
        private const float LiftReach = 2.6f;   // metres from where the player came in

        private readonly IModLogger _logger;
        private readonly DoorPanel _door;
        private readonly DoorPanel _lift;
        private readonly Action[] _doorPress = new Action[WorldPanel.Slots];
        private readonly Action[] _liftPress = new Action[WorldPanel.Slots];
        private readonly int[] _doorFloor = new int[WorldPanel.Slots];
        private readonly int[] _liftFloor = new int[WorldPanel.Slots];
        private readonly Dictionary<string, object> _args = new Dictionary<string, object>(32);
        private Headquarters.Tower _doorOf;      // the tower whose door the panel stands at
        private Headquarters.Tower _pending;     // a floor pressed at its door, until the player is in
        private int _pendingFloor;
        private int _doorPage, _liftPage;

        public TowerFloors(IModLogger logger)
        {
            _logger = logger;
            _door = new DoorPanel(logger, "Tech sites check (tower door)");
            _lift = new DoorPanel(logger, "Tech sites check (tower lift)");
            for (int i = 0; i < WorldPanel.Slots; i++)
            {
                int slot = i;
                _doorPress[i] = _door.Press(() => DoorPressed(slot));
                _liftPress[i] = _lift.Press(() => LiftPressed(slot));
            }
        }

        /// <summary>The tower the player is in, on one of its floors; null outdoors or elsewhere.</summary>
        public Headquarters.Tower In { get; private set; }

        /// <summary>The floor of <see cref="In"/> the player is on, from 1; 0 when not on one.</summary>
        public int Floor { get; private set; }

        /// <summary>Where the player came in: the lift is there.</summary>
        public Vector3 Spawn { get; private set; }

        public Quaternion SpawnTurn { get; private set; }

        /// <summary>The company of the floor the player is on, or null.</summary>
        public Headquarters.Hq Here => In != null ? In.OnFloor(Floor) : null;

        /// <summary>True while a panel of the towers is up, and in the frame its button ran.</summary>
        public bool TakesKey => _door.TakesKey || _lift.TakesKey;

        /// <summary>Asked to change floor from the lift; TechSites lays the floor again under the dark screen.</summary>
        public Action<int> ChangeFloor;

        /// <summary>The player is in a tower, on the floor pressed at the door, else their employer's, else the first. Returns its company.</summary>
        public Headquarters.Hq Arrive(Headquarters.Tower tower)
        {
            if (tower == null || tower.Floors.Count == 0)
            {
                Leave();
                return null;
            }
            int floor = ReferenceEquals(_pending, tower) && _pendingFloor >= 1 && _pendingFloor <= tower.Floors.Count ? _pendingFloor : HqTowers.DoorFloor(tower.Floors.Count, EmployerFloor(tower));
            string how = ReferenceEquals(_pending, tower) ? "the floor pressed at the door" : EmployerFloor(tower) > 0 ? "the floor of the player's employer" : "the first floor";
            _pending = null;
            _pendingFloor = 0;
            In = tower;
            Floor = floor;
            PlayerController player = PlayerHelper.PlayerController;
            if (player != null)
            {
                Spawn = player.transform.position;
                SpawnTurn = player.transform.rotation;
            }
            _liftPage = 0;
            _logger?.Info("Tech sites: the player is in " + tower.Name + " (" + tower.Street + " " + tower.Number + "), on floor " + floor + " of " + tower.Floors.Count
                          + ", " + Here.Name + " (" + how + ").");
            return Here;
        }

        /// <summary>The floor the player is on is now another (the lift): its company.</summary>
        public Headquarters.Hq Moved(int floor)
        {
            if (In == null || floor < 1 || floor > In.Floors.Count)
            {
                return Here;
            }
            int was = Floor;
            Floor = floor;
            _liftPage = 0;
            _logger?.Info("Tech sites: the player takes the lift in " + In.Name + " from floor " + was + " to floor " + floor + ", " + Here.Name + ".");
            return Here;
        }

        /// <summary>The player has left the tower, or the towers changed under them.</summary>
        public void Leave()
        {
            In = null;
            Floor = 0;
            _lift.Hide("left the tower");
        }

        /// <summary>Once a second.</summary>
        public void Tick(Headquarters hq)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (hq == null || player == null || !Settings.TechSiteHq || hq.Towers.Count == 0)
            {
                Hide("no towers");
                return;
            }
            Vector3 at = player.transform.position;
            bool busy = ThirdPersonCamera.IsUiOpen() || PlayerActivityUI.IsPanelOpen || JobDirector.TakesInteractKey;
            if (In != null && Floor > 0 && BuildingManager.IsInsideBuilding)
            {
                _door.Hide("inside");
                Vector3 offset = Spawn - at;
                if (busy || In.Floors.Count < 2 || new Vector2(offset.x, offset.z).magnitude > LiftReach || Mathf.Abs(offset.y) > 2f)
                {
                    _lift.Hide("away from the lift");
                    return;
                }
                WorldPanel panel = _lift.Panel;
                Headquarters.Hq here = Here;
                panel.Lines(In.Name, TechText.Fill("reallife_hq_lift_here", "floor", Floor.ToString(), "company", here != null ? here.Name : string.Empty), null, WorldPanel.White());
                Buttons(panel, In, _liftPress, _liftFloor, _liftPage, Floor);
                _lift.Show(Spawn, at, 2.1f, () => "the lift of " + In.Name + " on floor " + Floor);
                return;
            }
            _lift.Hide("not on a floor");
            if (BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || busy)
            {
                _door.Hide(BuildingManager.IsInsideBuilding ? "inside another building" : "busy");
                return;
            }
            Headquarters.Tower near = null;
            float nearest = DoorReach;
            for (int i = 0; i < hq.Towers.Count; i++)
            {
                Headquarters.Tower t = hq.Towers[i];
                Vector3 offset = t.Front - at;
                float flat = new Vector2(offset.x, offset.z).magnitude;
                if (flat <= nearest && Mathf.Abs(offset.y) < 2.5f && t.Floors.Count > 0 && Headquarters.Open(t))
                {
                    near = t;
                    nearest = flat;
                }
            }
            if (near == null)
            {
                _door.Hide("away from the towers' doors");
                _doorOf = null;
                _pending = null;   // a floor was pressed but the player walked away
                return;
            }
            if (!ReferenceEquals(near, _doorOf))
            {
                _doorOf = near;
                _doorPage = 0;
            }
            WorldPanel door = _door.Panel;
            door.Lines(near.Name, TechText.Fill("reallife_hq_tower_door", "count", near.Floors.Count.ToString()), "common_open".GetLocalization(), WorldPanel.Green());
            Buttons(door, near, _doorPress, _doorFloor, _doorPage, 0);
            _door.Show(near.Front, at, 2.2f, () => "at the door of " + near.Name);
        }

        // up to four buttons: the other floors with their companies, or three and "more floors"
        private void Buttons(WorldPanel panel, Headquarters.Tower tower, Action[] press, int[] floors, int page, int skip)
        {
            HqTowers.Page(tower.Floors.Count, skip, page, floors);
            int yours = EmployerFloor(tower);
            for (int slot = 0; slot < floors.Length; slot++)
            {
                if (floors[slot] < 0)
                {
                    panel.Button(slot, "reallife_campus_floor_more", press[slot], true, false);
                    continue;
                }
                Headquarters.Hq hq = tower.OnFloor(floors[slot]);
                if (hq == null)
                {
                    panel.Button(slot, null, null, false, false);
                    continue;
                }
                bool mine = floors[slot] == yours;
                string key = mine ? "reallife_hq_floor_yours" : "reallife_hq_floor";
                panel.Button(slot, key, Args(key, floors[slot], hq.Name), press[slot], true, mine || slot == 0);
            }
        }

        private object Args(string key, int floor, string company)
        {
            string id = key + "#" + floor + "#" + company;
            if (!_args.TryGetValue(id, out object args))
            {
                args = new { floor = floor.ToString(), company };
                _args[id] = args;
            }
            return args;
        }

        // The floor of the company the player works for, if it is in this tower; 0 otherwise.
        private static int EmployerFloor(Headquarters.Tower tower)
        {
            string employer = TechBridges.EmployerNow();
            for (int i = 0; employer != null && i < tower.Floors.Count; i++)
            {
                if (string.Equals(tower.Floors[i].Name, employer, StringComparison.Ordinal))
                {
                    return tower.Floors[i].Floor;
                }
            }
            return 0;
        }

        private void DoorPressed(int slot)
        {
            Headquarters.Tower tower = _doorOf;
            if (tower == null)
            {
                return;
            }
            if (_doorFloor[slot] < 0)
            {
                _doorPage++;
                return;
            }
            int floor = _doorFloor[slot];
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (floor <= 0 || manager == null || manager.enteringBuilding || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || !Headquarters.Open(tower))
            {
                return;
            }
            _pending = tower;
            _pendingFloor = floor;
            _door.Hide("going in");
            _logger?.Info("Tech sites: going into " + tower.Name + ", floor " + floor + " (" + tower.OnFloor(floor).Name + "), from the panel at its door.");
            try
            {
                manager.EnterBuilding(tower.Building.building, false, false, 0, NearestDoor(tower.Building));
            }
            catch (Exception e)
            {
                _pending = null;
                _logger?.Error(e);
            }
        }

        private void LiftPressed(int slot)
        {
            if (_liftFloor[slot] < 0)
            {
                _liftPage++;
                return;
            }
            if (_liftFloor[slot] > 0 && In != null && _liftFloor[slot] != Floor)
            {
                _lift.Hide("going to another floor");
                ChangeFloor?.Invoke(_liftFloor[slot]);
            }
        }

        private static int NearestDoor(CityBuildingController building)
        {
            Vector3 at = PlayerHelper.GetPosition();
            int best = building.entranceDoors[0].doorId;
            float near = float.MaxValue;
            for (int i = 0; i < building.entranceDoors.Length; i++)
            {
                Transform t = building.entranceDoors[i] != null ? building.entranceDoors[i].doorTransform : null;
                if (t != null && (t.position - at).sqrMagnitude < near)
                {
                    near = (t.position - at).sqrMagnitude;
                    best = building.entranceDoors[i].doorId;
                }
            }
            return best;
        }

        /// <summary>Both panels down.</summary>
        public void Hide(string why)
        {
            _door.Hide(why);
            _lift.Hide(why);
            _doorOf = null;
        }
    }
}
