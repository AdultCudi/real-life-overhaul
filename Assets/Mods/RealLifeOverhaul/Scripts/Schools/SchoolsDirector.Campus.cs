using System.Collections.Generic;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The director's part for the campus as it stands: the hall and the junior block asked of the copy engine while the city
    /// loads, the main school's classrooms held and its pieces stood behind the loading screen, the copies cut out of the
    /// walking meshes once they stand, the families at the gates in the school run's hours, its row on the city map and the
    /// district's traffic round it. On a load, a second's look or a day's pass; never a frame.
    /// </summary>
    public sealed partial class SchoolsDirector
    {
        private const float CrowdNear = 300f;   // m from the campus within which the families gather at its gates: the game's pedestrians are wanted elsewhere further off
        private static readonly Color MapColour = new Color(0.30f, 0.52f, 0.30f);   // the playing field's green

        private readonly List<CopyHandle> _copies = new List<CopyHandle>(2);
        private readonly List<CampusBlockPlot> _plots = new List<CampusBlockPlot>(2);
        private readonly List<CampusPiece> _pieces = new List<CampusPiece>(320);
        private readonly List<CampusFence> _runs = new List<CampusFence>(8);
        private readonly List<CampusCrowd> _crowdPlaces = new List<CampusCrowd>(2);
        private readonly StationaryAiSpawner[] _crowds = new StationaryAiSpawner[2];
        private readonly List<MapPlace> _places = new List<MapPlace>(1);
        private GameObject _pieceRoot;
        private SchoolHost _host;
        private SchoolFloors _floors;
        private bool _held;
        private bool _campusStood;          // the main school stood at this load and its pieces are out
        private int _cutCopies;
        private bool _crowding;
        private MapPlaces.Category _map;
        private int _liftLevel = -1;

        /// <summary>Pieces stood at this load, and the game's kinds that were not found (null for none).</summary>
        public static int PiecesStood { get; private set; }
        public static string PiecesMissing { get; private set; }

        /// <summary>True when the main school stood at this load and its campus is out.</summary>
        public static bool CampusStands => Instance != null && Instance._campusStood;

        // While the city loads: the campus's two other buildings asked of the copy engine, so they stand behind the loading screen.
        private void Ask()
        {
            _copies.Clear();
            if (!Wanted())
            {
                return;
            }
            SchoolGrounds.Blocks(_plots);
            for (int i = 0; i < _plots.Count; i++)
            {
                CopyHandle handle = SchoolsGame.Plot(_plots[i]);
                if (handle != null)
                {
                    _copies.Add(handle);
                }
            }
        }

        // Behind the loading screen, once the made buildings are made: the main school's classrooms held and the campus's pieces
        // stood, or everything given back with one line when the main school stood down.
        private bool AtLoad()
        {
            _campusStood = false;
            if (!Wanted())
            {
                DropCopies();
                return true;
            }
            MadeBuilding main = SchoolsGame.Main;
            if (main == null || main.Controller == null)
            {
                DropCopies();
                Logger?.Info("Schools: Birchwood School does not stand at this load: its main school stood down (the made buildings' line says why); the market runs on.");
                return true;
            }
            bool cut = SchoolsGame.Cut(main);
            PiecesMissing = SchoolsGame.FindTemplates();
            SchoolGrounds.Pieces(_pieces, _runs);
            _pieceRoot = SchoolsGame.Stand(_pieces, _runs, out int stood, out int missed);
            PiecesStood = stood;
            _campusStood = true;
            Hold(main);
            Logger?.Info("Schools: Birchwood School's campus stood at 1 Birchwood Green (the main school wears " + main.Design + ", " + (cut ? "cut out of the walking meshes" : "not cut") + "); "
                         + stood + " of " + _pieces.Count + " pieces of the game's own" + (missed > 0 ? " (" + missed + " not stood; not found: " + (PiecesMissing ?? "the pitch") + ")" : string.Empty)
                         + ", " + _runs.Count + " runs of fence; the hall and the junior block asked of the copy engine (" + _copies.Count + ").");
            return true;
        }

        private void DropCopies()
        {
            for (int i = 0; i < _copies.Count; i++)
            {
                SchoolsGame.Remove(_copies[i]);
            }
            _copies.Clear();
            _cutCopies = 0;
        }

        // Once a second while running: the copies cut once they stand, the classrooms held while the main school is shown, the
        // families at the gates in the school run's hours, the map's row.
        private void Campus()
        {
            if (!_campusStood)
            {
                return;
            }
            if (_cutCopies < _copies.Count)
            {
                int cut = 0;
                for (int i = 0; i < _copies.Count; i++)
                {
                    cut += SchoolsGame.CutCopy(_copies[i]) ? 1 : 0;
                }
                _cutCopies = cut;
            }
            MadeBuilding main = SchoolsGame.Main;
            if (main != null && main.Shown != _held)
            {
                if (main.Shown)
                {
                    Hold(main);
                }
                else
                {
                    Let("the main school is hidden");
                }
            }
            Crowds();
            Map();
        }

        // The main school's classrooms handed to the floors core: the game's school on its three floors.
        private void Hold(MadeBuilding main)
        {
            if (_held || main == null || main.Controller == null || !main.Shown)
            {
                return;
            }
            _host = _host ?? new SchoolHost();
            _floors = _floors ?? new SchoolFloors();
            _host.Building = main.Controller;
            float turn = main.DoorFacing * Mathf.Deg2Rad;
            _host.Door = new Vector3(main.DoorX + Mathf.Sin(turn) * 1.2f, main.DoorY, main.DoorZ + Mathf.Cos(turn) * 1.2f);
            _host.Name = SchoolsText.T(SchoolsText.Campus);
            _host.Own.Clear();
            _host.Own.Add(_floors);
            FloorsDirector.Hold(_host);
            _held = true;
        }

        private void Let(string why)
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            if (_host != null)
            {
                FloorsDirector.Let(_host, why);
            }
        }

        // The families at the gates: the game's pedestrians on the pavements before the main and side gates in the school run's
        // hours and on an open day, while the player is near.
        private void Crowds()
        {
            int day = SchoolsGame.Day, minute = SchoolsGame.Minute;
            bool openDay = SchoolRules.OpenDay(day, SchoolsGame.Year) && minute >= SchoolRules.OpenDayFrom * 60 && minute < SchoolRules.OpenDayTo * 60;
            bool want = (SchoolRules.SchoolRun(day, minute) || openDay || _testRun) && Near(CrowdNear);
            if (want == _crowding)
            {
                return;
            }
            _crowding = want;
            if (!want)
            {
                NoCrowds();
                return;
            }
            SchoolGrounds.Crowds(_crowdPlaces);
            int stood = 0;
            string why = null;
            for (int i = 0; i < _crowdPlaces.Count && i < _crowds.Length; i++)
            {
                _crowds[i] = SchoolsGame.Crowd(_crowdPlaces[i], out string whyNot);
                stood += _crowds[i] != null ? 1 : 0;
                why = why ?? whyNot;
            }
            if (Verbose)
            {
                Logger?.Info("Schools check (school run): " + stood + " of " + _crowdPlaces.Count + " squares of families at the gates" + (stood == _crowdPlaces.Count ? ": PASS" : ": " + why) + ".");
            }
        }

        private void NoCrowds()
        {
            for (int i = 0; i < _crowds.Length; i++)
            {
                SchoolsGame.NoCrowd(ref _crowds[i]);
            }
        }

        private static bool Near(float within)
        {
            if (!SchoolsGame.Outdoors)
            {
                return false;
            }
            Vector3 p = SchoolsGame.Player;
            SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
            float dx = p.x < x0 ? x0 - p.x : p.x > x1 ? p.x - x1 : 0f, dz = p.z < z0 ? z0 - p.z : p.z > z1 ? p.z - z1 : 0f;
            return dx * dx + dz * dz < within * within;
        }

        // The schools' row in the map's filters, a marker at the main gate with the school's name.
        private void Map()
        {
            if (_map != null && _places.Count > 0)
            {
                return;
            }
            SchoolGrounds.Gate(out float x, out float z, out _);
            _places.Clear();
            _places.Add(new MapPlace(new Vector3(x, UptownLots.GroundY, z), SchoolsText.T(SchoolsText.Campus)));
            if (_map == null)
            {
                _map = MapPlaces.Register("schools", SchoolsText.MapRow, MapColour, MapIcon.Mortarboard, true);
                _map.Available = () => Settings.Schools && UptownLots.Ready;
            }
            _map.Set(_places);
        }

        // The district's buildings near the campus given their traffic again when its name moves the lift a step.
        private void Traffic(bool force)
        {
            int level = SchoolRules.Lift(0f, 0f, _state.Campus.Reputation, _state.Opened > 0, 0f, 0f, 0f, 0f);
            if (!force && level == _liftLevel)
            {
                return;
            }
            _liftLevel = level;
            if (!DistrictBuildings.Lives || SchoolsGame.InSession)
            {
                return;
            }
            Standing.Clear();
            DistrictBuildings.Standing(Standing);
            SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
            int moved = 0;
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                if (made == null || made.Spec == null)
                {
                    continue;
                }
                float dx = made.X < x0 ? x0 - made.X : made.X > x1 ? made.X - x1 : 0f, dz = made.Z < z0 ? z0 - made.Z : made.Z > z1 ? made.Z - z1 : 0f;
                int own = dx * dx + dz * dz <= SchoolRules.LiftReach * SchoolRules.LiftReach ? DistrictBuildings.TrafficOf(made.Spec.Id) : -1;
                if (own >= 0 && SchoolsGame.Traffic(made, own))
                {
                    moved++;
                }
            }
            Standing.Clear();
            if (Verbose)
            {
                Logger?.Info("Schools check (traffic): the campus's name lifts the lots near it by up to " + level + " point(s); " + moved + " of the district's building(s) within "
                             + SchoolRules.LiftReach + " m read again: PASS.");
            }
        }

        private static readonly List<MadeBuilding> Standing = new List<MadeBuilding>(64);

        // Everything the campus stands in the street put away: its pieces, the families, the classrooms, the map's marker.
        private void LeaveCampus(string why)
        {
            SchoolsGame.Take(ref _pieceRoot);
            NoCrowds();
            _crowding = false;
            Let(why);
            DropCopies();
            _places.Clear();
            _map?.Set(_places);
            _campusStood = false;
        }
    }
}
