using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.NewGround;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's ground's tests under Options > Tests: each works when pressed, wherever the player is, or says why not in a
    /// notice, and writes one "Uptown check (...)" line per event with Developer diagnostics on. They read and count (the
    /// traffic test first puts the player where the cars come out); none changes the city, so each runs in a Going Public
    /// session too.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const float CountMinutes = 2f;     // game minutes the cars on the district's streets are counted for
        private const float StartReach = 3f;       // m round a path's ends the walking meshes are looked for
        private const float StartAside = 2f;       // m east of a street's pavement a walk from 1st Avenue begins, on its own pavement
        private const string CensusTest = "Uptown: census";
        private const string PathTest = "Uptown: a path from Manhattan into Uptown";
        private const string TrafficTest = "Uptown: traffic into Uptown";

        private readonly HashSet<int> _drove = new HashSet<int>();
        private float _countUntil;                 // game minutes; 0 while not counting
        private int _driving;

        public static void TestCensus()
        {
            if (Instance == null)
            {
                TestRefusals.Tell(Logger, CensusTest, TestRefusal.NotLoaded);
                return;
            }
            Instance.Census();
        }

        public static void TestPath()
        {
            if (Instance == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, PathTest, TestRefusal.NotLoaded);
                return;
            }
            Instance.Path();
        }

        public static void TestTraffic()
        {
            TestRefusal refusal = TestRefusals.Street();
            if (Instance == null || refusal != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, TrafficTest, Instance == null ? TestRefusal.NotLoaded : refusal);
                return;
            }
            Instance.Count();
        }

        // "Uptown: census": whether the district stands at this load and how it was laid, piece by piece.
        private void Census()
        {
            int junctions = _build != null ? _build.Junctions.Count : 0;
            int lights = 0, crossings = 0;
            for (int j = 0; j < junctions; j++)
            {
                int sides = (j > 0 ? 1 : 0) + (j < junctions - 1 ? 1 : 0);   // the avenue comes in from each side it goes on
                lights += 1 + sides;
                crossings += 1 + sides;
            }
            int open = 0;
            for (int i = 0; i < UptownLots.FirstBlock.Count; i++)
            {
                open += UptownLots.Open(UptownLots.FirstBlock[i]) ? 1 : 0;
            }
            bool map = GroundGame.MapBounds(out Bounds bounds);
            Vector3 at = WalkGame.At;
            bool onIt = LayoutRules.OnFirstGround(at.x, at.z);
            int zones = _people ? LayoutPlans.ZoneCount : 0;
            bool pass = UptownLots.Ready && _copied == LayoutPlans.ModuleCount && GroundGame.Lights == lights && GroundGame.Crossings == crossings
                        && _opened == LayoutPlans.OldWallCount && _hidden > 0 && GroundGame.PeopleZones == zones && GroundGame.ParkingLanes == LayoutPlans.ParkingCount
                        && map && bounds.max.z >= LayoutPlans.MapNorth - 1f;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Uptown check (census): " + (UptownLots.Ready ? "the first block stands" : "the district does not stand (" + UptownLots.Why + ")")
                             + (_build != null
                                 ? "; streets: " + RoadRules.Words(_build) + "; walking ground: the player's " + (_walkable ? "in " + _playerMs.ToString("F0", Inv) + " ms" : "NOT laid")
                                   + ", the pedestrians' " + (_people ? "in " + _peopleMs.ToString("F0", Inv) + " ms" : "not (" + _peopleWhy + ")") + "; the backdrop: " + _hidden
                                   + " pieces put away, " + _opened + " of " + LayoutPlans.OldWallCount + " walls opened, " + GroundGame.Built + " walls and colliders built; the art: "
                                   + _copied + " of " + LayoutPlans.ModuleCount + " modules, " + GroundGame.Lights + " of " + lights + " light poles hung, " + GroundGame.Crossings
                                   + " of " + crossings + " crosswalks linked, " + GroundGame.SignsDown + " signs down; " + GroundGame.ParkingLanes + " of " + LayoutPlans.ParkingCount
                                   + " parking lanes with " + GroundGame.Parked + " cars parked now, " + GroundGame.PeopleZones + " of " + zones + " pedestrians' zones; laid in "
                                   + _layMs.ToString("F0", Inv) + " ms"
                                 : string.Empty)
                             + "; the city map pans " + (map ? "to z " + Metres(bounds.max.z) : "nowhere (no map camera)") + "; lots: " + UptownLots.All.Count + " in all, "
                             + UptownLots.FirstBlock.Count + " in the first block, " + open + " open; the player is " + (onIt ? "on" : "off") + " the district's ground at ("
                             + Metres(at.x) + ", " + Metres(at.z) + "): " + (pass ? "PASS" : "FAIL"));
            }
            if (!UptownLots.Ready)
            {
                TestRefusals.TellKey(Logger, CensusTest, GroundText.NotLaid, UptownLots.Why);
                return;
            }
            Notice(GroundText.Laid, new Dictionary<string, string>(4)
            {
                { "streets", LayoutPlans.StreetCount.ToString(Inv) },
                { "junctions", junctions.ToString(Inv) },
                { "lots", open.ToString(Inv) },
                { "hidden", _hidden.ToString(Inv) }
            });
        }

        // "Uptown: a path from Manhattan into Uptown": on both walking meshes, from 1st Avenue's pavement at the foot of the
        // continued street nearest the player to the first block's middle on Uptown Avenue.
        private void Path()
        {
            if (!UptownLots.Ready)
            {
                TestRefusals.TellKey(Logger, PathTest, GroundText.NotLaid, UptownLots.Why);
                return;
            }
            int street = LayoutPlans.NearestLanes(WalkGame.At.x);
            Vector3 from = Foot(street);
            LayoutPlans.Terminus(out float tx, out float tz);
            var to = new Vector3(tx, UptownLots.GroundY, tz);
            string player = Walk(WalkGame.Player, "Player", street, from, to);
            string people = _people ? Walk(WalkGame.Npc, "NPC", street, from, to) : "NOT LAID";
            Notice(player == "COMPLETE" && people == "COMPLETE" ? GroundText.PathWhole : GroundText.PathShort, null);
        }

        private static string Walk(CityMesh mesh, string label, int street, Vector3 from, Vector3 to)
        {
            string result = mesh == null ? "INVALID (" + WalkGame.Missing + ")"
                : !WalkGame.Sample(mesh, from, StartReach, out Vector3 start) ? "INVALID (no walking mesh on 1st Avenue's pavement there)"
                : !WalkGame.Sample(mesh, to, StartReach, out Vector3 end) ? "INVALID (no walking mesh in the first block's middle)"
                : WalkGame.Path(mesh, start, end);
            if (Meta.Dev.Verbose)
            {
                LayoutStreet name = LayoutStreets.Street(street);
                Logger?.Info("Uptown check (path, " + label + "): from 1st Avenue at " + (name != null ? name.Id : "street " + street) + " (" + Metres(from.x) + ", "
                             + Metres(from.z) + ") to the first block's middle on Uptown Avenue (" + Metres(to.x) + ", " + Metres(to.z) + "): " + result + ": "
                             + (result == "COMPLETE" ? "PASS" : "FAIL"));
            }
            return result;
        }

        // 1st Avenue's north pavement at the foot of a continued street, just east of its mouth: Manhattan's side of the old wall.
        private static Vector3 Foot(int street)
        {
            return new Vector3(LayoutRules.StreetX(street) + LayoutRules.StreetHalf + StartAside, UptownLots.GroundY,
                LayoutRules.FirstAvenueZ + (LayoutRules.AvenueKerb + LayoutRules.AvenueHalf) * 0.5f);
        }

        // "Uptown: traffic into Uptown": the player put on 1st Avenue at the foot of the nearest continued street, where the
        // game's cars come out round them, and the cars whose next lane point is one of the district's counted for two game
        // minutes. From there the player walks north into the district.
        private void Count()
        {
            if (_build == null)
            {
                TestRefusals.TellKey(Logger, TrafficTest, GroundText.NotLaid, UptownLots.Why);
                return;
            }
            if (_countUntil > 0f)
            {
                TestRefusals.TellKey(Logger, TrafficTest, GroundText.CountBusy);
                return;
            }
            if (!WalkGame.OnFoot())
            {
                TestRefusals.Tell(Logger, TrafficTest, TestRefusal.OnFootOnly);
                return;
            }
            int street = LayoutPlans.NearestLanes(WalkGame.At.x);
            WalkGame.Warp(Foot(street));
            LayoutStreet name = LayoutStreets.Street(street);
            _drove.Clear();
            _driving = 0;
            _countUntil = TimeHelper.NowInMinutes() + CountMinutes;
            Notice(GroundText.Counting, new Dictionary<string, string>(1) { { "street", name != null ? name.NameKey.GetLocalization() : string.Empty } });
            _nextLook = 0f;
            enabled = true;
        }

        // A look while the count runs: true while it goes on.
        private bool TickCount()
        {
            if (_countUntil <= 0f || _build == null)
            {
                return false;
            }
            if (UI.Load.LoadScene.isLoading)
            {
                return true;
            }
            _driving = RoadsGame.Drove(_build, _drove);
            if (TimeHelper.NowInMinutes() < _countUntil)
            {
                return true;
            }
            _countUntil = 0f;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Uptown check (traffic): cars that drove its streets: " + _drove.Count + " in " + CountMinutes.ToString("0", Inv) + " game minutes ("
                             + _driving + " cars on the road at the last look): " + (_drove.Count > 0 ? "PASS" : "FAIL"));
            }
            Notice(GroundText.Counted, new Dictionary<string, string>(1) { { "cars", _drove.Count.ToString(Inv) } });
            return false;
        }

        // The count ends with the feature's clean-up and the city.
        private void StopCount()
        {
            _countUntil = 0f;
            _drove.Clear();
        }
    }
}
