using System;
using System.Collections.Generic;
using GleyUrbanAssets;
using RealLifeOverhaul.Meta;
using UnityEngine;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// The roads' side of the tests: the census of the game's road data as it is at this load, and the test road (a
    /// developer option read at each load): a copy of a short stretch of one of the game's lanes near where the save was
    /// made, appended as a road of its own with a fork and a merge before the traffic starts, checked once it has, and
    /// the cars that drive it counted for two game minutes. Never in a Going Public session: the other machines would not
    /// have it.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const float DroveMinutes = 2f;    // game minutes the cars on the test road are counted for

        private RoadBuild _road;
        private CurrentSceneData _roadData;
        private Action _trafficStarted;
        private float _droveUntil;                // game minutes; 0 while not counting
        private bool _tellAdded;
        private int _driving;
        private readonly HashSet<int> _drove = new HashSet<int>();

        /// <summary>"Roads: add a test road at the next load": the developer option switched, and said so.</summary>
        public static void TestRoad()
        {
            bool on = !Settings.NewGroundTestRoad;
            if (on && GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, "Roads: add a test road at the next load", NewGroundText.Session);
                return;
            }
            OptionInfo info = SettingsTable.Get(Settings.NewGroundTestRoadId);
            if (info != null)
            {
                SettingsTable.Write(info, on ? 1 : 0);
                UnityEngine.PlayerPrefs.Save();
            }
            Settings.NewGroundTestRoad = on;
            OptionsLayout.Refresh();
            Logger?.Info("New ground: the test road is " + (on ? "added at each load from the next" : "no longer added") + ".");
            Notice(on ? NewGroundText.RoadOn : NewGroundText.RoadOff, null);
        }

        // In the city's load, before the traffic starts: the test road appended, if the developer option asks for it.
        private void ArmRoad()
        {
            if (!Settings.NewGroundTestRoad)
            {
                return;
            }
            if (GoingPublic.InSession)
            {
                Logger?.Info("Roads: no test road in a Going Public session (the other players' traffic would not have it).");
                return;
            }
            CurrentSceneData data = RoadData.Scene;
            if (data == null || !RoadsGame.Grid(data, out RoadGrid grid))
            {
                Logger?.Info("Roads: no test road: the city's road data was not found.");
                return;
            }
            Vector3 near = SaveGameManager.Current != null ? SaveGameManager.Current.LastPlayerPosition : Vector3.zero;
            if (!RoadsGame.TestChain(data, near, out int[] chain, out float[] x, out float[] y, out float[] z, out int speed, out bool cars, out bool trucks, out string why))
            {
                Logger?.Info("Roads: no test road: " + why + ".");
                return;
            }
            RoadBuild build = RoadRules.Copy(chain, x, y, z, speed, cars, trucks, grid, data.allWaypoints.Length, RoadsGame.HighestRoadId(data) + 1);
            string refused = RoadsGame.Append(build, data);
            if (refused != null)
            {
                Logger?.Info("Roads: no test road: " + refused + ".");
                return;
            }
            _road = build;
            _roadData = data;
            GlobalEvents.RegisterOnGameLoadedLateCallback(_trafficStarted);
            Logger?.Info("Roads: a test road appended before the traffic starts: " + RoadRules.Words(build) + ", a copy of the game's lane points "
                         + chain[1] + " to " + chain[chain.Length - 2] + " between its fork at " + chain[0] + " and its merge at " + chain[chain.Length - 1]
                         + ", near (" + x[1].ToString("0", Inv) + ", " + z[1].ToString("0", Inv) + ").");
        }

        // The game's late "game loaded" call: its traffic has started with the appended road, or has not.
        private void TrafficStarted()
        {
            if (_road == null)
            {
                return;
            }
            bool taken = RoadsGame.Taken(_road, _roadData, out string line);
            if (Meta.Dev.Verbose)
            {
                Logger?.Info(line);
            }
            if (!taken)
            {
                return;
            }
            _drove.Clear();
            _droveUntil = TimeHelper.NowInMinutes() + DroveMinutes;
            _tellAdded = true;
            Wake();
        }

        private bool TickRoad()
        {
            if (_road == null || _droveUntil <= 0f)
            {
                return false;
            }
            if (UI.Load.LoadScene.isLoading)
            {
                return true;   // the traffic starts behind the loading screen; the count begins once it is gone
            }
            if (_tellAdded)
            {
                _tellAdded = false;
                Notice(NewGroundText.RoadAdded, new Dictionary<string, string>(1) { { "points", _road.Count.ToString(Inv) } });
            }
            _driving = RoadsGame.Drove(_road, _drove);
            if (TimeHelper.NowInMinutes() < _droveUntil)
            {
                return true;
            }
            _droveUntil = 0f;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Roads check (test road): cars that drove it: " + _drove.Count + " in " + DroveMinutes.ToString("0", Inv) + " game minutes ("
                             + _driving + " cars on the road at the last look)");
            }
            Notice(NewGroundText.RoadDrove, new Dictionary<string, string>(1) { { "cars", _drove.Count.ToString(Inv) } });
            return false;
        }

        // "Roads: census": the road data as the game has it now, the player's cell, the walking meshes and the map's bounds.
        private void Census()
        {
            CurrentSceneData data = RoadData.Scene;
            if (data == null || data.allWaypoints == null || !RoadsGame.Grid(data, out RoadGrid grid))
            {
                TestRefusals.TellKey(Logger, "Roads: census", NewGroundText.NoRoads);
                return;
            }
            int points = data.allWaypoints.Length;
            int ids = data.roadIds != null ? data.roadIds.Length : 0;
            var roads = new HashSet<int>();
            for (int i = 0; i < ids; i++)
            {
                roads.Add(data.roadIds[i]);
            }
            int junctions = data.allIntersections != null ? data.allIntersections.Length : 0;
            int lights = data.allLightsIntersections != null ? data.allLightsIntersections.Length : 0;
            int priority = data.allPriorityIntersections != null ? data.allPriorityIntersections.Length : 0;
            Vector3 at = WalkGame.At;
            int row = grid.Row(at.z), column = grid.Column(at.x);
            bool inside = grid.Inside(at.x, at.z, 0f);
            bool pass = ids == points && inside && junctions == lights + priority;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Roads check (census): corner (" + grid.CornerX.ToString("0.0", Inv) + ", " + grid.CornerZ.ToString("0.0", Inv) + "), "
                             + grid.Cell.ToString("0", Inv) + " m, " + grid.Rows + " x " + grid.Columns + ", " + points + " lane points, " + ids + " road ids ("
                             + roads.Count + " roads), " + junctions + " junctions (" + lights + " with lights, " + priority + " priority), player in row " + row
                             + " column " + column + ": " + (pass ? "PASS" : "FAIL" + (inside ? string.Empty : " (the player is off the road grid)")
                             + (ids == points ? string.Empty : " (the lane points and their road ids differ)")));
                Logger?.Info(Places());
            }
            Notice(NewGroundText.Census, new Dictionary<string, string>(7)
            {
                { "points", points.ToString(Inv) },
                { "roads", roads.Count.ToString(Inv) },
                { "junctions", junctions.ToString(Inv) },
                { "row", row.ToString(Inv) },
                { "column", column.ToString(Inv) },
                { "rows", grid.Rows.ToString(Inv) },
                { "columns", grid.Columns.ToString(Inv) }
            });
        }

        // The walking meshes and the map's bounds as the game has them at this load: what new ground is laid against.
        private static string Places()
        {
            string meshes = WalkGame.Player != null
                ? Mesh(WalkGame.Player) + "; " + Mesh(WalkGame.Npc)
                : "the walking meshes were not found (" + WalkGame.Missing + ")";
            string map = RoadsGame.MapBounds(out Bounds bounds)
                ? "the city map pans over x " + bounds.min.x.ToString("0", Inv) + " to " + bounds.max.x.ToString("0", Inv) + ", z "
                  + bounds.min.z.ToString("0", Inv) + " to " + bounds.max.z.ToString("0", Inv)
                : "the city map's bounds were not found";
            return "Walk check (census): " + meshes + "; " + map;
        }

        private static string Mesh(CityMesh mesh)
        {
            Vector3 size = mesh.Data.sourceBounds.size;
            return mesh.Label + " mesh " + mesh.Name + " (agent " + mesh.Agent + "), tiles of " + mesh.Grid.Side.ToString("0.00", Inv) + " m from ("
                   + mesh.Grid.OriginX.ToString("0.0", Inv) + ", " + mesh.Grid.OriginZ.ToString("0.0", Inv) + "), over " + size.x.ToString("0", Inv) + " x "
                   + size.z.ToString("0", Inv) + " m, voxel " + mesh.Settings.voxelSize.ToString("0.000", Inv) + " m, radius " + mesh.Settings.agentRadius.ToString("0.00", Inv)
                   + " m, slope " + mesh.Settings.agentSlope.ToString("0", Inv) + " degrees";
        }
    }
}
