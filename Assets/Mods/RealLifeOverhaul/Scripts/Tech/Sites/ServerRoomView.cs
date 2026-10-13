using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A data centre's server room while the player is inside it (the warehouse it is fitted into): the room is measured
    /// on the way in (Tech/Sites/SiteProps.cs, RoomMeasure), laid out by its tier and its racks
    /// (<see cref="ServerRoomPlan"/>) and built: the racks, cooling, battery cabinets, trays, raised floor and the
    /// operations desk, each a model of the prop bundle (or a box of its size until the bundle has loaded).
    ///
    /// The status lights: every rack carries the two sets of lights of its door (dc_leds_a, dc_leds_b), drawn in two
    /// materials of this room's own. Twice a second, and only while the player is in the room, the two materials change
    /// colour (the one set lit while the other is dark): two property changes, whatever the number of racks. In an outage
    /// they go amber and red and the room's own lights are dimmed (put back on the way out and when it is over); with no
    /// power or no connection the lights go red.
    ///
    /// The technicians: one or two of the mod's people (People/ActorPool) when the data centre has technicians, walking
    /// the cold aisles end to end and stopping at each end; looked after once a second.
    ///
    /// The rows are cut out of the floor people walk on only where the plan found that nothing of the player's is cut
    /// off by them; otherwise they are only to be seen. Everything is local: nothing is saved, nothing is an item of the
    /// player's, nothing is sent anywhere. All of it goes when the player leaves; it is built again on the next visit
    /// (the same room: the plan is the same for the same warehouse and racks). In the game's placement mode it is hidden,
    /// and built again from the new furniture afterwards.
    /// </summary>
    internal sealed class ServerRoomView
    {
        private const float Glow = 3.2f, Faint = 0.25f, RoomDimmed = 0.35f;

        private static readonly Color Green = new Color(0.25f, 1.0f, 0.40f), Blue = new Color(0.35f, 0.70f, 1.0f);
        private static readonly Color Amber = new Color(1.0f, 0.55f, 0.05f), Red = new Color(1.0f, 0.08f, 0.05f);

        private enum State { Up, Down, Supply }

        private sealed class Technician
        {
            public readonly ActorRole Role = new ActorRole();
            public int Stop;
            public float Until;
            public bool Waiting;
        }

        private readonly IModLogger _logger;
        private readonly ServerRoom _plan = new ServerRoom();
        private readonly List<Light> _lights = new List<Light>(16);
        private readonly List<float> _intensity = new List<float>(16);
        private readonly Technician[] _technicians = { new Technician(), new Technician() };
        private readonly StringBuilder _said = new StringBuilder(160);
        private Transform _root;
        private Material _setA, _setB;
        private State _state = State.Up;
        private bool _phase;
        private bool _dimmed;
        private int _items;
        private int _staff;
        private bool _hidden;

        public ServerRoomView(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>True while a room stands.</summary>
        public bool Active => _root != null;

        /// <summary>The data centre's place key the room is of.</summary>
        public string Place { get; private set; }

        /// <summary>What the last plan came to, for the log and the test button.</summary>
        public string Said => _said.ToString();

        /// <summary>
        /// The player is in a data centre's warehouse: the room measured, planned and built. False if the floor could not be
        /// measured yet (asked again a second later).
        /// </summary>
        public bool Begin(BuildingManager building, DataCentre centre, int day)
        {
            End();
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (building == null || centre == null || player == null || !Shapes.Available)
            {
                return false;
            }
            if (!RoomMeasure.Door(building, player.transform.position, player.transform.forward, out Vector3 door, out Vector3 inward))
            {
                return false;
            }
            RoomGrid grid = RoomMeasure.Measure(building, door, true, out float floor);
            if (grid == null)
            {
                return false;
            }
            ServerRoomPlan.Plan(grid, door.x, door.z, inward.x, inward.z, centre.Tier, centre.Racks, centre.GpuRacks, _plan);
            Place = centre.Place;
            _items = RoomMeasure.Items(building);
            _staff = Math.Min(_technicians.Length, Math.Max(0, centre.Technicians));
            _said.Length = 0;
            _said.Append(centre.Name).Append(": ");
            if (_plan.Why != null && _plan.Servers + _plan.Accelerators == 0)
            {
                _said.Append("no server room (").Append(_plan.Why).Append(')');
            }
            _root = new GameObject("RealLifeOverhaul Server Room").transform;
            _setA = SiteProps.Own(Green, Glow);
            _setB = SiteProps.Own(Blue, Faint);
            Build(floor);
            ReadLights(building);
            _state = (State)(-1);
            Apply(centre, day);
            if (_plan.Servers + _plan.Accelerators > 0)
            {
                _said.Append("hall ").Append((_plan.X1 - _plan.X0).ToString("F1")).Append(" x ").Append((_plan.Z1 - _plan.Z0).ToString("F1")).Append(" m, ")
                    .Append(_plan.Servers).Append(" server and ").Append(_plan.Accelerators).Append(" accelerator racks of ").Append(centre.Racks + centre.GpuRacks)
                    .Append(" (room for ").Append(_plan.Slots).Append("), ").Append(_plan.Rows).Append(" rows, ").Append(_plan.Cooling).Append(" cooling, ")
                    .Append(_plan.Ups).Append(" battery cabinets").Append(_plan.HasDesk ? ", the desk" : ", no desk")
                    .Append(_plan.Carve ? "; the rows are solid" : "; the rows are only seen (they would cut off the furniture)")
                    .Append(_plan.Unreachable > 0 ? "; " + _plan.Unreachable + " aisle ends not reached" : string.Empty);
            }
            return true;
        }

        private void Build(float floor)
        {
            var rows = new Dictionary<int, Bounds>(16);
            for (int i = 0; i < _plan.Pieces.Count; i++)
            {
                SitePiece p = _plan.Pieces[i];
                if (!SiteProps.Of(p.Kind, out SiteProps.Spec spec))
                {
                    continue;
                }
                Transform piece = SiteProps.Make(spec, _root, out bool modelled);
                RoomMeasure.Stand(piece, p, floor);
                if (p.Kind == SitePieceKind.Tray)
                {
                    piece.localScale = new Vector3(1f, 1f, Mathf.Max(0.1f, p.Length / ServerRoomPlan.TrayMost));
                }
                else if (p.Kind == SitePieceKind.Floor || p.Kind == SitePieceKind.Vent)
                {
                    piece.localScale = new Vector3(Mathf.Max(0.1f, p.Width / spec.Size.x), 1f, Mathf.Max(0.1f, p.Length / spec.Size.z));
                }
                if (modelled && (p.Kind == SitePieceKind.Rack || p.Kind == SitePieceKind.AiRack || p.Kind == SitePieceKind.NetworkRack))
                {
                    Vector3 forward = p.Kind == SitePieceKind.AiRack ? new Vector3(0f, 0f, SiteProps.AiForward) : Vector3.zero;
                    Lights(piece, "dc_leds_a", _setA, forward);
                    Lights(piece, "dc_leds_b", _setB, forward);
                }
                if (!_plan.Carve || !p.Stands)
                {
                    continue;
                }
                if (p.Row >= 0)
                {
                    var b = new Bounds(new Vector3(p.X, floor + 1f, p.Z), new Vector3(p.HalfX * 2f, 2f, p.HalfZ * 2f));
                    if (rows.TryGetValue(p.Row, out Bounds all))
                    {
                        all.Encapsulate(b);
                        rows[p.Row] = all;
                    }
                    else
                    {
                        rows[p.Row] = b;
                    }
                }
                else
                {
                    SiteProps.Solid(piece, new Vector3(0f, spec.Size.y * 0.5f, 0f), new Vector3(spec.Size.x, Mathf.Max(0.5f, spec.Size.y), spec.Size.z));
                }
            }
            // One cut for each row of racks, not one a rack.
            foreach (KeyValuePair<int, Bounds> row in rows)
            {
                var cut = new GameObject("RealLifeOverhaul Row").transform;
                cut.SetParent(_root, false);
                cut.position = row.Value.center;
                SiteProps.Solid(cut, Vector3.zero, row.Value.size);
            }
        }

        private static void Lights(Transform rack, string id, Material material, Vector3 at)
        {
            if (material != null && PropLibrary.TryGet(id, out Mesh mesh, out Material _) && mesh != null)
            {
                Shapes.Piece(id, rack, mesh, material, at, Vector3.one);
            }
        }

        // The room's own lights, to be dimmed in an outage and put back.
        private void ReadLights(BuildingManager building)
        {
            _lights.Clear();
            _intensity.Clear();
            Transform room = building.currentBuildingVersion;
            if (room == null)
            {
                return;
            }
            Light[] all = room.GetComponentsInChildren<Light>(false);
            for (int i = 0; i < all.Length && _lights.Count < 64; i++)
            {
                if (all[i] != null && all[i].enabled && all[i].type != LightType.Directional)
                {
                    _lights.Add(all[i]);
                    _intensity.Add(all[i].intensity);
                }
            }
        }

        private void Dim(bool dimmed)
        {
            if (_dimmed == dimmed)
            {
                return;
            }
            _dimmed = dimmed;
            for (int i = 0; i < _lights.Count; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].intensity = _intensity[i] * (dimmed ? RoomDimmed : 1f);
                }
            }
        }

        // What the data centre is doing now: up, in an outage, or without power or a connection.
        private void Apply(DataCentre centre, int day)
        {
            State state = centre.Down(day) ? State.Down : !centre.Power || !centre.Net ? State.Supply : State.Up;
            if (state == _state)
            {
                return;
            }
            _state = state;
            Dim(state != State.Up);
            Blink();
        }

        /// <summary>Twice a second while the player is in the room: the two sets of lights change places.</summary>
        public void Blink()
        {
            if (_root == null || _hidden)
            {
                return;
            }
            _phase = !_phase;
            switch (_state)
            {
                case State.Down:
                    SiteProps.Tint(_setA, Amber, Glow);
                    SiteProps.Tint(_setB, Red, _phase ? Glow : Faint);
                    break;
                case State.Supply:
                    SiteProps.Tint(_setA, Red, _phase ? Glow * 0.6f : Faint);
                    SiteProps.Tint(_setB, Red, Faint);
                    break;
                default:
                    SiteProps.Tint(_setA, Green, _phase ? Glow : Faint);
                    SiteProps.Tint(_setB, Blue, _phase ? Faint : Glow);
                    break;
            }
        }

        /// <summary>
        /// Once a second while the player is inside: the data centre's state, the technicians. True if the room has to be
        /// built again (the player's furniture has changed, or the game's placement mode is over).
        /// </summary>
        public bool Tick(BuildingManager building, DataCentre centre, int day, float now)
        {
            if (_root == null || centre == null)
            {
                return false;
            }
            bool placing = BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode;
            if (placing != _hidden)
            {
                _hidden = placing;
                _root.gameObject.SetActive(!placing);
                if (placing)
                {
                    EndCast();
                    Dim(false);
                    return false;
                }
                return true;   // (out of placement mode: built again round whatever was placed)
            }
            if (_hidden)
            {
                return false;
            }
            if (RoomMeasure.Items(building) != _items)
            {
                return true;
            }
            Apply(centre, day);
            _staff = Math.Min(_technicians.Length, Math.Max(0, centre.Technicians));
            Cast(now);
            return false;
        }

        // The technicians walk the cold aisles end to end, a pause at each end. Put out one a second at most.
        private void Cast(float now)
        {
            int points = _plan.Patrol.Count / 2;
            for (int i = 0; i < _technicians.Length; i++)
            {
                Technician t = _technicians[i];
                t.Role.Tick(now);
                if (i >= _staff || points < 2 || !Settings.People)
                {
                    t.Role.Exit();
                    continue;
                }
                if (!t.Role.Present)
                {
                    int stop = (i * points / _technicians.Length) % points;
                    if (RoomMeasure.Spot(PatrolPoint(stop), out Vector3 at) && t.Role.EnterPresence(at, ActorLook.Civilian, 0.5f, onRoad: false))
                    {
                        t.Stop = stop;
                        t.Waiting = false;
                        t.Role.Stand(PatrolPoint((stop + 1) % points));
                        t.Until = now + Random.Range(1f, 4f);
                    }
                    return;   // (one new person a second)
                }
                if (t.Role.Settled && !t.Waiting)
                {
                    t.Waiting = true;
                    t.Until = now + Random.Range(3f, 8f);
                }
                if (t.Waiting && now >= t.Until)
                {
                    t.Waiting = false;
                    t.Stop = (t.Stop + 1) % points;
                    t.Role.Go(PatrolPoint(t.Stop), Pace.Walk, PatrolPoint((t.Stop + 1) % points));
                }
            }
        }

        private Vector3 PatrolPoint(int stop)
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            float y = player != null ? player.transform.position.y : 0f;
            return new Vector3(_plan.Patrol[2 * stop], y, _plan.Patrol[2 * stop + 1]);
        }

        private void EndCast()
        {
            for (int i = 0; i < _technicians.Length; i++)
            {
                _technicians[i].Role.Exit();
            }
        }

        /// <summary>The player has left (or the part is going): everything taken away, the room's lights put back. Safe at any time.</summary>
        public void End()
        {
            try
            {
                EndCast();
                Dim(false);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _lights.Clear();
            _intensity.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            _root = null;
            if (_setA != null)
            {
                UnityEngine.Object.Destroy(_setA);
            }
            if (_setB != null)
            {
                UnityEngine.Object.Destroy(_setB);
            }
            _setA = _setB = null;
            _hidden = false;
            _dimmed = false;
            Place = null;
        }
    }
}
