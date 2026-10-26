using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using GleyUrbanAssets;
using UnityEngine;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// Where new streets touch the game's traffic (GleyTrafficSystem): the only file that writes its road data. The
    /// traffic takes its lane points and road ids by reference when it starts (WaypointManagerBase.Initialize, in
    /// CityManager.OnScenesLoaded, a "game loaded" call that comes after the mods' city load), copies its junctions once
    /// (GridManager.GetAllIntersections) and reads its grid live. So a build is appended in the mod's city load, before
    /// the traffic starts: new arrays with the game's own points first (their indices never change), the joins added to
    /// the game's points' neighbours or previous points, every new point filed in its cell (and its spawn list, through the
    /// game's own GridCell.AddWaypoint), every new junction appended after the game's (their light indices never change,
    /// which Going Public sends) and filed in its cells. A street continued through one of the game's junctions with
    /// lights gives that junction its new stop points (in the way of its own street, with the lamps the game hangs for that
    /// approach) and its new exits; the junction keeps its index and its number of ways. The traffic is never started
    /// again. Called on a load or a test, never per frame.
    /// </summary>
    public static class RoadsGame
    {
        public const float TestReach = 400f;     // m from the saved position the test road's lane is looked for
        public const int TestPoints = 10;        // the game's lane points the test road's chain takes at most (about 45 m)
        public const int TestLeast = 6;          // ...and at least: four copied between the fork and the merge
        public const float TestBend = 20f;       // degrees the lane may turn over the chain: the copy is of a straight stretch
        public const string LampsName = "RealLifeOverhaul new ground lamps";
        private const float LampReach = 4f;      // m: a light of the game's junction this near where a new approach's light belongs is that approach's

        private static readonly List<int> CellPoints = new List<int>(256);

        // What an append changed, kept until the city goes, so that a district standing down at its load takes it all back.
        private sealed class Applied
        {
            public Waypoint[] OldPoints;
            public int[] OldIds;
            public IntersectionData[] OldJunctions;
            public TrafficLightsIntersection[] OldLights;
            public PriorityIntersection[] OldPriority;
            public readonly List<LinkEdit> Edited = new List<LinkEdit>(8);
            public readonly List<int> Filed = new List<int>(64);
            public readonly List<ThroughEdit> Through = new List<ThroughEdit>(4);
            public readonly List<GameObject> Lamps = new List<GameObject>(12);   // the game's lamps given to a way for a new approach, three a stop
            public readonly List<int> LampWays = new List<int>(12);              // ... the junction and way each went to, as junction * 64 + way
            public readonly List<long> Cells = new List<long>(8);                // cells given one of the game's junctions: row, column and junction packed
        }

        private static readonly Dictionary<RoadBuild, Applied> Appends = new Dictionary<RoadBuild, Applied>(2);

        /// <summary>The road grid as the game has it now; false where its scene data has none.</summary>
        public static bool Grid(CurrentSceneData data, out RoadGrid grid)
        {
            grid = default;
            if (data == null || data.grid == null || data.grid.Length == 0 || data.grid[0] == null || data.grid[0].row == null)
            {
                return false;
            }
            grid = new RoadGrid(data.gridCorner.x, data.gridCorner.z, data.gridCellSize, data.grid.Length, data.grid[0].row.Length);
            return grid.Valid;
        }

        /// <summary>The highest road id the game's lanes use now: every new road's is above it.</summary>
        public static int HighestRoadId(CurrentSceneData data)
        {
            int highest = 0;
            if (data != null && data.roadIds != null)
            {
                for (int i = 0; i < data.roadIds.Length; i++)
                {
                    highest = Math.Max(highest, data.roadIds[i]);
                }
            }
            return highest;
        }

        /// <summary>The city map camera's bounds as the game has them now, where the map can be panned; false outside a city.</summary>
        public static bool MapBounds(out Bounds bounds)
        {
            bounds = default;
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            if (city == null || city.cityMap == null || city.cityMap.cityMapCam == null)
            {
                return false;
            }
            bounds = city.cityMap.cityMapCam.bounds;
            return true;
        }

        /// <summary>
        /// Appends a checked build to the scene's road data before the traffic starts. Null when done; otherwise why not,
        /// and the road data is as it was.
        /// </summary>
        public static string Append(RoadBuild build, CurrentSceneData data)
        {
            if (data == null || data.allWaypoints == null)
            {
                return "the city has no road data";
            }
            if (TrafficManager.IsInitialized)
            {
                return "the traffic has already started: it is never started again";
            }
            if (!Grid(data, out RoadGrid grid))
            {
                return "the road data has no grid";
            }
            Waypoint[] old = data.allWaypoints;
            int count = old.Length;
            if (data.roadIds == null || data.roadIds.Length != count)
            {
                return "the game's lane points have no road ids of their own (" + (data.roadIds != null ? data.roadIds.Length : 0) + " for " + count + ")";
            }
            string problem = RoadRules.Check(build, grid, count, HighestRoadId(data));
            if (problem != null)
            {
                return problem;
            }
            for (int t = 0; t < build.Through.Count; t++)
            {
                ThroughEdit edit = build.Through[t];
                TrafficLightsIntersection[] gameLights = data.allLightsIntersections;
                if (gameLights == null || edit.Junction >= gameLights.Length || gameLights[edit.Junction] == null || gameLights[edit.Junction].stopWaypoints == null
                    || edit.Way >= gameLights[edit.Junction].stopWaypoints.Count || gameLights[edit.Junction].exitWaypoints == null)
                {
                    return "the game's junction " + edit.Junction + " a street goes through is not as it was read";
                }
            }
            // Everything is made first; the game's data is changed only once all of it is there.
            var points = new Waypoint[count + build.Count];
            var ids = new int[count + build.Count];
            Array.Copy(old, points, count);
            Array.Copy(data.roadIds, ids, count);
            for (int i = 0; i < build.Count; i++)
            {
                LanePoint point = build.Points[i];
                points[count + i] = new Waypoint(point.Name, count + i, new Vector3(point.X, point.Y, point.Z), Vehicles(point), new List<int>(point.Next),
                    new List<int>(point.Prev), new List<int>(point.Beside), point.Speed, false);
                ids[count + i] = point.RoadId;
            }
            IntersectionData[] oldJunctions = data.allIntersections ?? new IntersectionData[0];
            var lights = new List<TrafficLightsIntersection>(data.allLightsIntersections ?? new TrafficLightsIntersection[0]);
            var priority = new List<PriorityIntersection>(data.allPriorityIntersections ?? new PriorityIntersection[0]);
            var junctions = new IntersectionData[oldJunctions.Length + build.Junctions.Count];
            Array.Copy(oldJunctions, junctions, oldJunctions.Length);
            for (int j = 0; j < build.Junctions.Count; j++)
            {
                BuiltJunction built = build.Junctions[j];
                var ways = new List<IntersectionStopWaypointsIndex>(built.Ways.Count);
                for (int w = 0; w < built.Ways.Count; w++)
                {
                    ways.Add(built.Lights ? new IntersectionStopWaypointsIndex(new List<int>(built.Ways[w]), Lamp(built, w, "red"), Lamp(built, w, "yellow"), Lamp(built, w, "green"))
                        : new IntersectionStopWaypointsIndex(new List<int>(built.Ways[w]), new List<GameObject>(0), new List<GameObject>(0), new List<GameObject>(0)));
                }
                if (built.Lights)
                {
                    lights.Add(new TrafficLightsIntersection(built.Name, ways, built.Green, built.Yellow, new List<int>(built.Exits)));
                    junctions[oldJunctions.Length + j] = new IntersectionData(IntersectionType.TrafficLights, lights.Count - 1);
                }
                else
                {
                    priority.Add(new PriorityIntersection(built.Name, ways, new List<int>(built.Exits)));
                    junctions[oldJunctions.Length + j] = new IntersectionData(IntersectionType.Priority, priority.Count - 1);
                }
            }
            var applied = new Applied
            {
                OldPoints = old,
                OldIds = data.roadIds,
                OldJunctions = oldJunctions,
                OldLights = data.allLightsIntersections,
                OldPriority = data.allPriorityIntersections
            };
            try
            {
                for (int e = 0; e < build.Edits.Count; e++)
                {
                    LinkEdit edit = build.Edits[e];
                    (edit.AsNext ? old[edit.Existing].neighbors : old[edit.Existing].prev).Add(edit.Added);
                    applied.Edited.Add(edit);
                }
                for (int i = 0; i < build.Count; i++)
                {
                    LanePoint point = build.Points[i];
                    data.grid[point.Row].row[point.Column].AddWaypoint(count + i, point.Name, points[count + i].allowedAgents, point.Spawn);
                    applied.Filed.Add(i);
                }
                for (int j = 0; j < build.Junctions.Count; j++)
                {
                    List<int> cells = build.Junctions[j].Cells;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        data.grid[cells[c] / grid.Columns].row[cells[c] % grid.Columns].AddIntersection(oldJunctions.Length + j);
                    }
                }
                GiveThrough(build, data, applied);
                data.allWaypoints = points;
                data.roadIds = ids;
                data.allIntersections = junctions;
                data.allLightsIntersections = lights.ToArray();
                data.allPriorityIntersections = priority.ToArray();
                Appends[build] = applied;
                return null;
            }
            catch (Exception e)
            {
                Undo(build, data, applied, grid);
                return "the road data could not take the build (" + e.GetType().Name + ": " + e.Message + "); it is as it was";
            }
        }

        /// <summary>
        /// Takes an appended build back off the road data while the traffic has not started: a district that stands down
        /// later in its own load leaves the roads as the game made them. Null when done; otherwise why not, and the build
        /// stays until the next load.
        /// </summary>
        public static string TakeBack(RoadBuild build, CurrentSceneData data)
        {
            if (build == null || data == null || !Appends.TryGetValue(build, out Applied applied))
            {
                return "it was not appended at this load";
            }
            if (TrafficManager.IsInitialized)
            {
                return "the traffic has already started: it stays until the next load";
            }
            if (!Grid(data, out RoadGrid grid))
            {
                return "the road data has no grid";
            }
            Undo(build, data, applied, grid);
            data.allWaypoints = applied.OldPoints;
            data.roadIds = applied.OldIds;
            data.allIntersections = applied.OldJunctions;
            data.allLightsIntersections = applied.OldLights;
            data.allPriorityIntersections = applied.OldPriority;
            for (int j = 0; j < build.Junctions.Count; j++)
            {
                DropLamps(build.Junctions[j]);
            }
            Appends.Remove(build);
            return null;
        }

        // What an append had done is taken off again, before the traffic ever saw it: a failed append, or one taken back.
        private static void Undo(RoadBuild build, CurrentSceneData data, Applied applied, RoadGrid grid)
        {
            Waypoint[] old = applied.OldPoints;
            for (int e = 0; e < applied.Edited.Count; e++)
            {
                LinkEdit edit = applied.Edited[e];
                (edit.AsNext ? old[edit.Existing].neighbors : old[edit.Existing].prev).Remove(edit.Added);
            }
            for (int f = 0; f < applied.Filed.Count; f++)
            {
                LanePoint point = build.Points[applied.Filed[f]];
                GridCell cell = data.grid[point.Row].row[point.Column];
                int index = build.First + applied.Filed[f];
                cell.waypointsInCell.Remove(index);
                for (int s = cell.spawnWaypoints.Count - 1; s >= 0; s--)
                {
                    if (cell.spawnWaypoints[s].waypointIndex == index)
                    {
                        cell.spawnWaypoints.RemoveAt(s);
                    }
                }
            }
            int oldJunctions = applied.OldJunctions.Length;
            for (int j = 0; j < build.Junctions.Count; j++)
            {
                List<int> cells = build.Junctions[j].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    data.grid[cells[c] / grid.Columns].row[cells[c] % grid.Columns].intersectionsInCell.Remove(oldJunctions + j);
                }
            }
            TrafficLightsIntersection[] gameLights = applied.OldLights;
            for (int t = 0; t < applied.Through.Count; t++)
            {
                ThroughEdit edit = applied.Through[t];
                TrafficLightsIntersection junction = gameLights[edit.Junction];
                if (edit.Way >= 0)
                {
                    junction.stopWaypoints[edit.Way].roadWaypoints.Remove(edit.Point);
                }
                else
                {
                    junction.exitWaypoints.Remove(edit.Point);
                }
            }
            for (int l = 0; l < applied.Lamps.Count; l += 3)
            {
                IntersectionStopWaypointsIndex way = gameLights[applied.LampWays[l] / 64].stopWaypoints[applied.LampWays[l] % 64];
                way.redLightObjects.Remove(applied.Lamps[l]);
                way.yellowLightObjects.Remove(applied.Lamps[l + 1]);
                way.greenLightObjects.Remove(applied.Lamps[l + 2]);
            }
            for (int c = 0; c < applied.Cells.Count; c++)
            {
                Unpack(applied.Cells[c], out int row, out int column, out int junction);
                data.grid[row].row[column].intersectionsInCell.Remove(junction);
            }
            applied.Edited.Clear();
            applied.Filed.Clear();
            applied.Through.Clear();
            applied.Lamps.Clear();
            applied.LampWays.Clear();
            applied.Cells.Clear();
        }

        // The new stop points and exits a build gives the game's junctions: a stop point in the way of the street it
        // continues, the lamps the game hangs for its approach given to that way (the module of the junction has them,
        // unlit, for a fourth arm), an exit among its exits, and each point's cell told of the junction.
        private static void GiveThrough(RoadBuild build, CurrentSceneData data, Applied applied)
        {
            for (int t = 0; t < build.Through.Count; t++)
            {
                ThroughEdit edit = build.Through[t];
                TrafficLightsIntersection junction = data.allLightsIntersections[edit.Junction];
                if (edit.Way >= 0)
                {
                    junction.stopWaypoints[edit.Way].roadWaypoints.Add(edit.Point);
                    applied.Through.Add(edit);
                    GiveLamps(junction, edit, applied);
                }
                else
                {
                    junction.exitWaypoints.Add(edit.Point);
                    applied.Through.Add(edit);
                }
                LanePoint point = build.At(edit.Point);
                int dataIndex = JunctionData(data, edit.Junction);
                if (point == null || dataIndex < 0)
                {
                    continue;
                }
                GridCell cell = data.grid[point.Row].row[point.Column];
                if (!cell.intersectionsInCell.Contains(dataIndex))
                {
                    cell.intersectionsInCell.Add(dataIndex);
                    applied.Cells.Add(Pack(point.Row, point.Column, dataIndex));
                }
            }
        }

        // The lamps of the game's junction's own module for a new approach: the red, yellow and green of the light nearest
        // where that approach's light hangs that no way uses yet; none found, the approach goes on the way's green unlit.
        private static void GiveLamps(TrafficLightsIntersection junction, ThroughEdit edit, Applied applied)
        {
            Transform traffic = null;
            for (int w = 0; w < junction.stopWaypoints.Count && traffic == null; w++)
            {
                List<GameObject> reds = junction.stopWaypoints[w].redLightObjects;
                for (int r = 0; reds != null && r < reds.Count && traffic == null; r++)
                {
                    traffic = Above(reds[r] != null ? reds[r].transform : null, "Traffic");
                }
            }
            if (traffic == null)
            {
                return;
            }
            GameObject red = null;
            float nearest = LampReach * LampReach;
            foreach (Transform light in traffic.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(light.name, "RedLight", StringComparison.OrdinalIgnoreCase) || Used(junction, light.gameObject))
                {
                    continue;
                }
                float dx = light.position.x - edit.LampX, dz = light.position.z - edit.LampZ;
                if (dx * dx + dz * dz < nearest)
                {
                    nearest = dx * dx + dz * dz;
                    red = light.gameObject;
                }
            }
            GameObject yellow = red != null ? Sibling(red.transform, "YellowLight") : null;
            GameObject green = red != null ? Sibling(red.transform, "Greenlight") : null;
            if (red == null || yellow == null || green == null)
            {
                return;
            }
            IntersectionStopWaypointsIndex way = junction.stopWaypoints[edit.Way];
            way.redLightObjects.Add(red);
            way.yellowLightObjects.Add(yellow);
            way.greenLightObjects.Add(green);
            applied.Lamps.Add(red);
            applied.Lamps.Add(yellow);
            applied.Lamps.Add(green);
            int at = edit.Junction * 64 + edit.Way;
            applied.LampWays.Add(at);
            applied.LampWays.Add(at);
            applied.LampWays.Add(at);
        }

        // The nearest ancestor of that name, or null.
        private static Transform Above(Transform from, string name)
        {
            for (Transform t = from; t != null; t = t.parent)
            {
                if (t.name == name)
                {
                    return t;
                }
            }
            return null;
        }

        private static GameObject Sibling(Transform of, string name)
        {
            Transform parent = of.parent;
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                if (string.Equals(parent.GetChild(i).name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return parent.GetChild(i).gameObject;
                }
            }
            return null;
        }

        // A red lamp some way of the junction switches already.
        private static bool Used(TrafficLightsIntersection junction, GameObject red)
        {
            for (int w = 0; w < junction.stopWaypoints.Count; w++)
            {
                if (junction.stopWaypoints[w].redLightObjects != null && junction.stopWaypoints[w].redLightObjects.Contains(red))
                {
                    return true;
                }
            }
            return false;
        }

        // The index in the road data's list of all junctions of one with lights (by its index among them), or -1.
        private static int JunctionData(CurrentSceneData data, int lights)
        {
            IntersectionData[] all = data.allIntersections;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                if (all[i].type == IntersectionType.TrafficLights && all[i].index == lights)
                {
                    return i;
                }
            }
            return -1;
        }

        private static long Pack(int row, int column, int junction)
        {
            return ((long)row << 40) | ((long)column << 20) | (uint)junction;
        }

        private static void Unpack(long packed, out int row, out int column, out int junction)
        {
            row = (int)(packed >> 40);
            column = (int)((packed >> 20) & 0xFFFFF);
            junction = (int)(packed & 0xFFFFF);
        }

        /// <summary>
        /// Finds the game's junction with lights each through end of a plan meets at this load (never a kept index): the one
        /// whose stop points' middle lies nearest the end's place, and reads its stop points and exits for the build.
        /// </summary>
        public static void FindThroughs(RoadPlan plan, CurrentSceneData data)
        {
            TrafficLightsIntersection[] lights = data != null ? data.allLightsIntersections : null;
            Waypoint[] all = data != null ? data.allWaypoints : null;
            if (lights == null || all == null || data.roadIds == null || data.roadIds.Length != all.Length)
            {
                for (int t = 0; t < plan.Throughs.Count; t++)
                {
                    plan.Throughs[t].Existing = -1;
                }
                return;
            }
            var cx = new float[lights.Length];
            var cz = new float[lights.Length];
            for (int j = 0; j < lights.Length; j++)
            {
                cx[j] = float.MaxValue;
                cz[j] = float.MaxValue;
                if (lights[j] == null || lights[j].stopWaypoints == null)
                {
                    continue;
                }
                float sx = 0f, sz = 0f;
                int n = 0;
                for (int w = 0; w < lights[j].stopWaypoints.Count; w++)
                {
                    List<int> stops = lights[j].stopWaypoints[w].roadWaypoints;
                    for (int s = 0; stops != null && s < stops.Count; s++)
                    {
                        if (stops[s] >= 0 && stops[s] < all.Length)
                        {
                            sx += all[stops[s]].position.x;
                            sz += all[stops[s]].position.z;
                            n++;
                        }
                    }
                }
                if (n > 0)
                {
                    cx[j] = sx / n;
                    cz[j] = sz / n;
                }
            }
            for (int t = 0; t < plan.Throughs.Count; t++)
            {
                PlanThrough through = plan.Throughs[t];
                through.Stops.Clear();
                through.Exits.Clear();
                through.Existing = RoadRules.FindThrough(through, cx, cz);
                if (through.Existing < 0)
                {
                    continue;
                }
                TrafficLightsIntersection junction = lights[through.Existing];
                for (int w = 0; w < junction.stopWaypoints.Count; w++)
                {
                    List<int> stops = junction.stopWaypoints[w].roadWaypoints;
                    for (int s = 0; stops != null && s < stops.Count; s++)
                    {
                        if (stops[s] >= 0 && stops[s] < all.Length && all[stops[s]].prev != null && all[stops[s]].prev.Count > 0)
                        {
                            Waypoint stop = all[stops[s]];
                            through.Stops.Add(Read(data, stops[s], stop.position - all[stop.prev[0]].position, w));
                        }
                    }
                }
                List<int> exits = junction.exitWaypoints;
                for (int e = 0; exits != null && e < exits.Count; e++)
                {
                    if (exits[e] >= 0 && exits[e] < all.Length && all[exits[e]].neighbors != null && all[exits[e]].neighbors.Count > 0)
                    {
                        Waypoint exit = all[exits[e]];
                        through.Exits.Add(Read(data, exits[e], all[exit.neighbors[0]].position - exit.position, -1));
                    }
                }
            }
        }

        // One of the game's lane points as the road rules read it.
        private static GamePoint Read(CurrentSceneData data, int index, Vector3 heading, int way)
        {
            Waypoint point = data.allWaypoints[index];
            string name = point.name ?? string.Empty;
            int cut = name.IndexOf('-', name.IndexOf("-Lane_", StringComparison.Ordinal) + 1);
            return new GamePoint
            {
                Index = index,
                X = point.position.x,
                Y = point.position.y,
                Z = point.position.z,
                Dx = heading.x,
                Dz = heading.z,
                Way = way,
                RoadId = data.roadIds[index],
                Lane = cut > 0 ? name.Substring(0, cut) : name,
                Speed = point.maxSpeed,
                Cars = point.allowedCars != null && point.allowedCars.Contains(VehicleTypes.Car),
                Trucks = point.allowedCars != null && point.allowedCars.Contains(VehicleTypes.Truck)
            };
        }

        private static List<VehicleTypes> Vehicles(LanePoint point)
        {
            var allowed = new List<VehicleTypes>(2);
            if (point.Cars)
            {
                allowed.Add(VehicleTypes.Car);
            }
            if (point.Trucks)
            {
                allowed.Add(VehicleTypes.Truck);
            }
            return allowed;
        }

        // A lamp of a new junction: the game's lights switch it on and off (TrafficLightsBehaviours.DefaultBehaviour), and a
        // crossing's people wait while the red one for its road is off. Plain objects under the mod's own holder; a
        // street's art may put the game's own lamps under them.
        private static List<GameObject> Lamp(BuiltJunction junction, int way, string colour)
        {
            if (_lamps == null)
            {
                _lamps = new GameObject(LampsName);
            }
            var lamp = new GameObject(LampName(junction, way, colour));
            lamp.transform.SetParent(_lamps.transform, false);
            lamp.SetActive(false);
            return new List<GameObject>(1) { lamp };
        }

        private static string LampName(BuiltJunction junction, int way, string colour)
        {
            return junction.Name + " road " + way + " " + colour;
        }

        private static GameObject _lamps;

        /// <summary>
        /// The red lamp of one road into a new junction with lights (<paramref name="way"/>, as the junction's ways run),
        /// for a crossing over that road (<see cref="PeopleGame.Crossing"/>); null for a priority junction, or before the
        /// build is appended. Looked up by name among the holder's lamps, once a crossing at a load.
        /// </summary>
        public static GameObject RedLamp(BuiltJunction junction, int way)
        {
            return LampOf(junction, way, "red");
        }

        /// <summary>
        /// One lamp ("red", "yellow" or "green") of one road into a new junction with lights: the object its lights switch on
        /// and off, under which a street's art puts the game's own light of that colour. Null as for <see cref="RedLamp"/>.
        /// </summary>
        public static GameObject LampOf(BuiltJunction junction, int way, string colour)
        {
            if (_lamps == null || junction == null || !junction.Lights)
            {
                return null;
            }
            string name = LampName(junction, way, colour);
            foreach (Transform lamp in _lamps.transform)
            {
                if (lamp.name == name)
                {
                    return lamp.gameObject;
                }
            }
            return null;
        }

        // A junction taken back: its lamps go, and whatever a street's art hung under them.
        private static void DropLamps(BuiltJunction junction)
        {
            if (_lamps == null)
            {
                return;
            }
            string prefix = junction.Name + " road ";
            for (int i = _lamps.transform.childCount - 1; i >= 0; i--)
            {
                Transform lamp = _lamps.transform.GetChild(i);
                if (lamp.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    UnityEngine.Object.Destroy(lamp.gameObject);
                }
            }
        }

        /// <summary>The city is going: the lamps of its new junctions go with it, and what its appends changed is let go.</summary>
        public static void Forget()
        {
            if (_lamps != null)
            {
                UnityEngine.Object.Destroy(_lamps);
            }
            _lamps = null;
            Appends.Clear();
        }

        /// <summary>
        /// Once the traffic has started: true when it took the appended points and junctions as its own (the very objects
        /// at the very indices, a road id for each, the new junctions' stop points marked by the junctions), with the line.
        /// </summary>
        public static bool Taken(RoadBuild build, CurrentSceneData data, out string line)
        {
            int first = build.First, count = build.Count;
            string head = "Roads check (append): traffic started with " + first + " + " + count + " lane points";
            if (!TrafficManager.HasInstance || !TrafficManager.IsInitialized)
            {
                line = head + ": FAIL, the traffic did not start";
                return false;
            }
            try
            {
                TrafficManager traffic = TrafficManager.Instance;
                for (int i = 0; i < count; i++)
                {
                    if (!ReferenceEquals(traffic.GetWaypoint(first + i), data.allWaypoints[first + i]))
                    {
                        line = head + ": FAIL, the traffic's lane point " + (first + i) + " is not the one appended";
                        return false;
                    }
                }
                int marked = 0, stops = 0;
                for (int j = 0; j < build.Junctions.Count; j++)
                {
                    List<List<int>> ways = build.Junctions[j].Ways;
                    for (int w = 0; w < ways.Count; w++)
                    {
                        for (int k = 0; k < ways[w].Count; k++)
                        {
                            stops++;
                            marked += data.allWaypoints[ways[w][k]].IsInIntersection() ? 1 : 0;
                        }
                    }
                }
                for (int t = 0; t < build.Through.Count; t++)
                {
                    stops++;
                    marked += data.allWaypoints[build.Through[t].Point].IsInIntersection() ? 1 : 0;   // the game's junctions mark their new stop points and exits too
                }
                bool ids = data.roadIds != null && data.roadIds.Length == data.allWaypoints.Length;
                bool pass = ids && marked == stops;
                line = head + ", " + data.allWaypoints.Length + " road ids " + (ids ? "for all" : "MISSING") + ", " + marked + " of " + stops
                       + " new stop points (and exits given to the game's junctions) marked by their junctions: " + (pass ? "PASS" : "FAIL");
                return pass;
            }
            catch (Exception e)
            {
                line = head + ": FAIL, the traffic does not hold the appended points (" + e.GetType().Name + ")";
                return false;
            }
        }

        /// <summary>
        /// The test road's chain: the nearest straight stretch of one of the game's own lanes to <paramref name="near"/>,
        /// plain points of one road each with one way on, a car lane; its points' indices, places and figures. False
        /// with why where there is none within <see cref="TestReach"/>.
        /// </summary>
        public static bool TestChain(CurrentSceneData data, Vector3 near, out int[] chain, out float[] x, out float[] y, out float[] z, out int speed,
            out bool cars, out bool trucks, out string why)
        {
            chain = null;
            x = y = z = null;
            speed = 0;
            cars = trucks = false;
            why = null;
            Waypoint[] all = data != null ? data.allWaypoints : null;
            if (all == null || data.roadIds == null || data.roadIds.Length != all.Length)
            {
                why = "the city has no road data with road ids";
                return false;
            }
            var found = new List<int>(TestPoints);
            var best = new List<int>(TestPoints);
            float nearest = TestReach * TestReach;
            for (int i = 0; i < all.Length; i++)
            {
                Waypoint start = all[i];
                if (!Single(all, i) || start.prev == null || start.prev.Count != 1)
                {
                    continue;
                }
                float dx = start.position.x - near.x, dz = start.position.z - near.z;
                float distance = dx * dx + dz * dz;
                if (distance >= nearest || !Chain(all, data.roadIds, i, found))
                {
                    continue;
                }
                nearest = distance;
                best.Clear();
                best.AddRange(found);
            }
            if (best.Count < TestLeast)
            {
                why = "no straight stretch of a car lane of the game's within " + TestReach.ToString("0") + " m of where the save was made";
                return false;
            }
            chain = best.ToArray();
            x = new float[chain.Length];
            y = new float[chain.Length];
            z = new float[chain.Length];
            for (int k = 0; k < chain.Length; k++)
            {
                Vector3 at = all[chain[k]].position;
                x[k] = at.x;
                y[k] = at.y;
                z[k] = at.z;
            }
            Waypoint inner = all[chain[1]];
            speed = inner.maxSpeed;
            cars = inner.allowedCars != null && inner.allowedCars.Contains(VehicleTypes.Car);
            trucks = inner.allowedCars != null && inner.allowedCars.Contains(VehicleTypes.Truck);
            return true;
        }

        // A plain point of a road proper with one way on, cars allowed.
        private static bool Single(Waypoint[] all, int index)
        {
            Waypoint point = all[index];
            return point != null && point.name != null && TrafficManager.IsNotIntersection(point) && point.neighbors != null && point.neighbors.Count == 1
                   && point.allowedCars != null && point.allowedCars.Contains(VehicleTypes.Car);
        }

        // From a point on along its lane: plain points of the same road, one way on and one way in, turning little.
        private static bool Chain(Waypoint[] all, int[] roadIds, int start, List<int> into)
        {
            into.Clear();
            into.Add(start);
            int at = start;
            Vector3 first = Vector3.zero;
            while (into.Count < TestPoints)
            {
                int next = all[at].neighbors[0];
                if (next < 0 || next >= all.Length || next == start || roadIds[next] != roadIds[start] || !Single(all, next) || all[next].prev.Count != 1)
                {
                    break;
                }
                Vector3 step = all[next].position - all[at].position;
                step.y = 0f;
                if (step.sqrMagnitude < 0.25f || step.sqrMagnitude > RoadRules.LongestLink * RoadRules.LongestLink)
                {
                    break;
                }
                if (into.Count == 1)
                {
                    first = step;
                }
                else if (Vector3.Angle(first, step) > TestBend)
                {
                    break;
                }
                into.Add(next);
                at = next;
            }
            return into.Count >= TestLeast;
        }

        /// <summary>
        /// The game's lane points near any of a build's new ones, read from the road grid's cells round each: where the
        /// "has the game put a road here since" check looks.
        /// </summary>
        public static void Near(RoadBuild build, CurrentSceneData data, RoadGrid grid, out int[] indices, out float[] x, out float[] z)
        {
            CellPoints.Clear();
            var seen = new HashSet<int>();
            for (int i = 0; i < build.Count; i++)
            {
                LanePoint point = build.Points[i];
                for (int r = point.Row - 1; r <= point.Row + 1; r++)
                {
                    for (int c = point.Column - 1; c <= point.Column + 1; c++)
                    {
                        if (r < 0 || c < 0 || r >= grid.Rows || c >= grid.Columns)
                        {
                            continue;
                        }
                        List<int> inCell = data.grid[r].row[c].waypointsInCell;
                        for (int k = 0; inCell != null && k < inCell.Count; k++)
                        {
                            if (inCell[k] < build.First && seen.Add(inCell[k]))
                            {
                                CellPoints.Add(inCell[k]);
                            }
                        }
                    }
                }
            }
            indices = CellPoints.ToArray();
            x = new float[indices.Length];
            z = new float[indices.Length];
            for (int k = 0; k < indices.Length; k++)
            {
                Vector3 at = data.allWaypoints[indices[k]].position;
                x[k] = at.x;
                z[k] = at.z;
            }
            CellPoints.Clear();
        }

        /// <summary>
        /// Finds each join's lane point of the game's at this load (never a kept index): the nearest that runs the new
        /// lane's way within <see cref="RoadRules.JoinReach"/> of its place, from the grid's cell there and those round it.
        /// </summary>
        public static void FindJoins(RoadPlan plan, CurrentSceneData data, RoadGrid grid)
        {
            for (int j = 0; j < plan.Joins.Count; j++)
            {
                PlanJoin join = plan.Joins[j];
                join.Existing = -1;
                if (join.Road < 0 || join.Road >= plan.Roads.Count || !grid.Inside(join.X, join.Z, 0f))
                {
                    continue;
                }
                RoadRules.JoinHeading(plan.Roads[join.Road], join, out float laneDx, out float laneDz);
                int row = grid.Row(join.Z), column = grid.Column(join.X);
                var indices = new List<int>(64);
                for (int r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
                {
                    for (int c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
                    {
                        List<int> inCell = data.grid[r].row[c].waypointsInCell;
                        if (inCell != null)
                        {
                            indices.AddRange(inCell);
                        }
                    }
                }
                var ix = new int[indices.Count];
                var px = new float[indices.Count];
                var pz = new float[indices.Count];
                var dx = new float[indices.Count];
                var dz = new float[indices.Count];
                for (int k = 0; k < indices.Count; k++)
                {
                    Waypoint point = data.allWaypoints[indices[k]];
                    ix[k] = indices[k];
                    px[k] = point.position.x;
                    pz[k] = point.position.z;
                    if (point.neighbors != null && point.neighbors.Count > 0)
                    {
                        Vector3 ahead = data.allWaypoints[point.neighbors[0]].position - point.position;
                        dx[k] = ahead.x;
                        dz[k] = ahead.z;
                    }
                }
                join.Existing = RoadRules.FindJoin(join, laneDx, laneDz, ix, px, pz, dx, dz);
            }
        }

        /// <summary>
        /// Counts the traffic's cars whose next lane point is one of a build's, each car once into <paramref name="seen"/>;
        /// the cars on the road now (a look of the pool's 30, on a timer, never per frame).
        /// </summary>
        public static int Drove(RoadBuild build, HashSet<int> seen)
        {
            if (!TrafficManager.HasInstance || !TrafficManager.IsInitialized)
            {
                return 0;
            }
            List<VehicleComponent> cars = Manager.GetVehicleList();
            TrafficManager traffic = TrafficManager.Instance;
            int driving = 0;
            for (int i = 0; cars != null && i < cars.Count; i++)
            {
                VehicleComponent car = cars[i];
                if (car == null || !car.gameObject.activeInHierarchy)
                {
                    continue;
                }
                driving++;
                Waypoint target = traffic.GetTargetWaypoint(car.GetIndex());
                if (target != null && target.listIndex >= build.First && target.listIndex < build.First + build.Count)
                {
                    seen.Add(car.GetIndex());
                }
            }
            return driving;
        }
    }
}
