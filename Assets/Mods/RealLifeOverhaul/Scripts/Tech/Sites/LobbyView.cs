using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The lobby of a tech company's headquarters while the player is in it: the bare interior of the empty office unit
    /// the company was given (Tech/Sites/Headquarters), measured on the way in and laid out by <see cref="LobbyPlan"/>:
    ///
    ///   the reception desk   the mod's model, the company's colour along its front
    ///   the logo wall        the mod's model, a panel in the company's colour on its plate and the company's name on it
    ///                        in the game's font, in the company's second colour
    ///   seating              the game's own sofa, armchairs and low table (Core/GameFurniture: copies to look at, nothing more)
    ///   plants               the game's own
    ///   screens              the mod's wall screen, hung where a ray finds the wall
    ///   the ceiling          the game's own flush lamps along the way in, where a ray finds the ceiling
    ///
    /// What stands on the floor is cut out of the navigation mesh (this is the city's own empty building, never a player's).
    /// The people: a receptionist behind the desk, and a visitor or two waiting on the seats now and then; the mod's own
    /// people, put out under the fade and looked after once a second. All of it goes when the player leaves.
    /// </summary>
    internal sealed class LobbyView
    {
        private static readonly string[] Sofas = { "sofaluxury", "sofa1" };
        private static readonly string[] Armchairs = { "armchairluxury", "armchair1" };
        private static readonly string[] Tables = { "coffeetableluxury", "coffeetable1" };
        private static readonly string[] Plants = { "groundplant03", "groundplant01", "groundplant05", "groundplant02", "groundplant06" };
        private const string Lamp = "ceilinglampsquare";

        private readonly IModLogger _logger;
        private readonly Lobby _plan = new Lobby();
        private readonly List<Transform> _seats = new List<Transform>(6);
        private readonly List<Transform> _scratch = new List<Transform>(4);
        private readonly ActorRole _receptionist = new ActorRole();
        private readonly ActorRole[] _visitors = { new ActorRole(), new ActorRole() };
        private readonly float[] _visitorUntil = new float[2];
        private readonly bool[] _leaving = new bool[2];
        private readonly StringBuilder _said = new StringBuilder(128);
        private Transform _root;
        private Vector3 _door, _staff, _front;
        private float _nextPeople;

        public LobbyView(IModLogger logger)
        {
            _logger = logger;
        }

        public bool Active => _root != null;

        /// <summary>The company whose lobby stands, or -1.</summary>
        public int Company { get; private set; } = -1;

        public string Said => _said.ToString();

        /// <summary>The player is in a headquarters' unit: the lobby measured, planned and furnished. False to be asked again a second later.</summary>
        public bool Begin(BuildingManager building, Headquarters.Hq hq)
        {
            End();
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (building == null || hq == null || player == null || !Shapes.Available)
            {
                return false;
            }
            if (!RoomMeasure.Door(building, player.transform.position, player.transform.forward, out Vector3 door, out Vector3 inward))
            {
                return false;
            }
            RoomGrid grid = RoomMeasure.Measure(building, door, false, out float floor);
            if (grid == null)
            {
                return false;
            }
            float ix = inward.x, iz = inward.z;
            ServerRoomPlan.Normalise(ref ix, ref iz);
            LobbyPlan.Plan(grid, door.x, door.z, ix, iz, _plan);
            Company = hq.Company;
            _door = door;
            _root = new GameObject("RealLifeOverhaul Lobby").transform;
            HqColours.Of(hq.Company, false, out float r, out float g, out float b);
            var main = new Color(r, g, b);
            HqColours.Of(hq.Company, true, out r, out g, out b);
            var second = new Color(r, g, b);
            int plants = 0;
            for (int i = 0; i < _plan.Pieces.Count; i++)
            {
                SitePiece p = _plan.Pieces[i];
                switch (p.Kind)
                {
                    case SitePieceKind.Reception:
                    {
                        Transform desk = Model(p, floor, true);
                        SiteProps.Panel(desk, main, SiteProps.ReceptionBand + new Vector3(0f, 0f, 0.004f), new Vector3(2.48f, 0.10f, 0.008f));
                        break;
                    }
                    case SitePieceKind.LogoWall:
                    {
                        Transform wall = Model(p, floor, true);
                        SiteProps.Panel(wall, main, SiteProps.LogoPlate + new Vector3(0f, 0f, 0.005f), new Vector3(SiteProps.LogoPlateSize.x, SiteProps.LogoPlateSize.y, 0.01f));
                        SiteProps.Letter(wall, hq.Name, SiteProps.LogoPlate + new Vector3(0f, 0f, 0.016f), SiteProps.LogoPlateSize * 0.86f, second, 6f);
                        break;
                    }
                    case SitePieceKind.Screen:
                        Hang(p, floor);
                        break;
                    case SitePieceKind.Sofa:
                        Furniture(Sofas, p, floor, true, true);
                        break;
                    case SitePieceKind.Armchair:
                        Furniture(Armchairs, p, floor, true, true);
                        break;
                    case SitePieceKind.LowTable:
                        Furniture(Tables, p, floor, true, false);
                        break;
                    case SitePieceKind.Plant:
                        Furniture(new[] { Plants[plants++ % Plants.Length] }, p, floor, false, false);
                        break;
                }
            }
            Lamps(door, inward, floor);
            if (_plan.HasDesk)
            {
                _staff = new Vector3(_plan.StaffX, floor, _plan.StaffZ);
                _front = new Vector3(_plan.FrontX, floor, _plan.FrontZ);
            }
            _nextPeople = 0f;
            _said.Length = 0;
            _said.Append(hq.Name).Append(": lobby ").Append(_plan.Pieces.Count).Append(" pieces")
                .Append(_plan.HasDesk ? ", the desk " + _plan.Depth.ToString("F1") + " m in" : ", no desk").Append(_plan.HasSeating ? ", seating" : string.Empty)
                .Append(_plan.Why != null ? " (" + _plan.Why + ")" : string.Empty).Append(", ").Append(_seats.Count).Append(" seats");
            return true;
        }

        private Transform Model(SitePiece p, float floor, bool solid)
        {
            if (!SiteProps.Of(p.Kind, out SiteProps.Spec spec))
            {
                return null;
            }
            Transform piece = SiteProps.Make(spec, _root, out _);
            RoomMeasure.Stand(piece, p, floor);
            if (solid)
            {
                SiteProps.Solid(piece, new Vector3(0f, spec.Size.y * 0.5f, 0f), new Vector3(spec.Size.x, spec.Size.y, spec.Size.z));
            }
            return piece;
        }

        // A screen on the wall behind where the plan put it, its back on the wall's real surface.
        private void Hang(SitePiece p, float floor)
        {
            Vector3 face = RoomMeasure.Facing(p);
            var at = new Vector3(p.X, floor + p.Y, p.Z);
            if (!RoomMeasure.Wall(at, -face, 1.2f, out Vector3 surface, out Vector3 normal))
            {
                return;
            }
            Transform screen = SiteProps.Make(SiteProps.Screen, _root, out _);
            screen.SetPositionAndRotation(new Vector3(surface.x, floor + p.Y, surface.z) + normal * 0.004f, Quaternion.LookRotation(normal, Vector3.up));
        }

        // One of the game's own pieces where the plan put one of its size, its drawn middle on the plan's.
        private void Furniture(string[] prefabs, SitePiece p, float floor, bool solid, bool seats)
        {
            Transform piece = null;
            for (int i = 0; i < prefabs.Length && piece == null; i++)
            {
                piece = GameFurniture.Make(prefabs[i], _root);
            }
            if (piece == null)
            {
                return;
            }
            Bounds size = GameFurniture.LocalBounds(piece);
            Quaternion facing = Quaternion.Euler(0f, p.Yaw, 0f);
            Vector3 middle = facing * new Vector3(size.center.x, 0f, size.center.z);
            piece.SetPositionAndRotation(new Vector3(p.X, floor, p.Z) - middle, facing);
            if (solid)
            {
                SiteProps.Solid(piece, new Vector3(size.center.x, size.size.y * 0.5f, size.center.z), new Vector3(size.size.x, Mathf.Max(0.5f, size.size.y), size.size.z));
            }
            piece.gameObject.SetActive(true);
            if (seats)
            {
                _scratch.Clear();
                GameFurniture.Seats(piece, _scratch);
                _seats.AddRange(_scratch);
            }
        }

        // The game's flush lamps down the way in, where a ray finds the ceiling.
        private void Lamps(Vector3 door, Vector3 inward, float floor)
        {
            float depth = Mathf.Max(3f, _plan.Depth);
            float[] at = { depth * 0.5f, depth + 1.2f, depth + 3.4f };
            for (int i = 0; i < at.Length; i++)
            {
                Vector3 from = door + inward * at[i];
                from.y = floor + 1.2f;
                if (!RoomMeasure.Ceiling(from, out Vector3 ceiling))
                {
                    continue;
                }
                Transform lamp = GameFurniture.Make(Lamp, _root);
                if (lamp == null)
                {
                    return;
                }
                lamp.SetPositionAndRotation(ceiling - Vector3.up * 0.002f, Quaternion.LookRotation(inward, Vector3.up));
                lamp.gameObject.SetActive(true);
            }
        }

        /// <summary>Once a second while the player is in the lobby: the receptionist at the desk, now and then a visitor.</summary>
        public void Tick(float now)
        {
            if (_root == null)
            {
                return;
            }
            _receptionist.Tick(now);
            for (int i = 0; i < _visitors.Length; i++)
            {
                _visitors[i].Tick(now);
            }
            if (!Settings.People || now < _nextPeople)
            {
                return;
            }
            _nextPeople = now + 3f;
            if (_plan.HasDesk && !_receptionist.Present)
            {
                if (RoomMeasure.Spot(_staff, out Vector3 at) && _receptionist.Enter(at, ActorLook.Civilian, 0.3f, onRoad: false))
                {
                    _receptionist.Stand(_front);
                }
                return;
            }
            for (int i = 0; i < _visitors.Length && i < _seats.Count; i++)
            {
                ActorRole visitor = _visitors[i];
                if (visitor.Present)
                {
                    if (now >= _visitorUntil[i] && !_leaving[i])
                    {
                        visitor.WalkOff(_door, Pace.Walk);
                        _leaving[i] = true;
                    }
                    continue;
                }
                if (_leaving[i])
                {
                    _leaving[i] = false;
                    _visitorUntil[i] = now + Random.Range(30f, 90f);   // (gone: the next one some time later)
                }
                if (now < _visitorUntil[i])
                {
                    continue;
                }
                if (Random.value < 0.5f)
                {
                    _visitorUntil[i] = now + Random.Range(20f, 60f);   // (nobody this time)
                    continue;
                }
                Transform seat = _seats[(i * 2) % _seats.Count];
                if (seat != null && RoomMeasure.Spot(seat.position, out Vector3 near) && visitor.EnterPresence(near, ActorLook.Civilian, 0.5f, onRoad: false))
                {
                    visitor.Sit(seat);
                    _visitorUntil[i] = now + Random.Range(60f, 150f);
                }
                return;   // (one new person at a look)
            }
        }

        public void End()
        {
            try
            {
                _receptionist.Exit();
                for (int i = 0; i < _visitors.Length; i++)
                {
                    _visitors[i].Exit();
                    _visitorUntil[i] = 0f;
                    _leaving[i] = false;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _seats.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            _root = null;
            Company = -1;
        }
    }
}
