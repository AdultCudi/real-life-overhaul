using System.Collections.Generic;
using GleyUrbanAssets;
using UnityEngine;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// The front door a district of the mod's own lays its streets and ground through, in the city's load (OnLoadAsync,
    /// behind the loading screen, before the traffic starts), at every load: nothing of it is saved, so a save always
    /// loads without the mod. Everything is read from the game as it is at this load (the road grid, the lanes, the
    /// walking meshes and their grids), and each part first looks whether the game has put something of its own where the
    /// plan lays new ground (a free update): a lane of the game's beside a new one that is not a join, more of the game's
    /// colliders on the plan's ground than when the plan was made. Then it stands down with one line and lays nothing.
    /// Never in a Going Public session unless the caller has made sure every machine runs this same version of the mod.
    /// Called once a load; nothing per frame.
    /// </summary>
    public static class GroundWork
    {
        private static readonly Dictionary<CityMesh, float> Proved = new Dictionary<CityMesh, float>(2);   // the dry run's margin, once a load each
        private static readonly List<string> Seen = new List<string>(3);   // the game's colliders found on a plan's ground, for its line
        private static readonly List<GroundBox> Standing = new List<GroundBox>(64);   // what stands on a plan's ground, walked round
        private static readonly HashSet<int> Counted = new HashSet<int>();            // ...each collider once

        /// <summary>How the last ground grown can be walked on, for the caller's log line; null before any.</summary>
        public static string LastWalk { get; private set; }

        /// <summary>
        /// The plan's streets appended to the traffic's data, its joins found again among the game's lanes and its ends
        /// through the game's junctions found again among its junctions at this load. Null when done; else why it stood
        /// down, the road data as it was.
        /// </summary>
        public static string Streets(RoadPlan plan, out RoadBuild build)
        {
            build = null;
            CurrentSceneData data = RoadData.Scene;
            if (data == null || data.allWaypoints == null || !RoadsGame.Grid(data, out RoadGrid grid))
            {
                return "the city's road data was not found";
            }
            RoadsGame.FindJoins(plan, data, grid);
            RoadsGame.FindThroughs(plan, data);
            build = RoadRules.Build(plan, grid, data.allWaypoints.Length, RoadsGame.HighestRoadId(data) + 1);
            if (build.Problem != null)
            {
                return build.Problem;
            }
            RoadsGame.Near(build, data, grid, out int[] indices, out float[] x, out float[] z);
            string clash = RoadRules.Clear(build, indices, x, z);
            if (clash != null)
            {
                return "the game has a road of its own where the plan's streets go (" + clash + ")";
            }
            return RoadsGame.Append(build, data);
        }

        /// <summary>
        /// A district's streets taken back off the traffic's data in the same load, before the traffic starts: the district
        /// stood down after its streets were appended (its ground could not be laid). Null when done; else why not.
        /// </summary>
        public static string TakeBack(RoadBuild build)
        {
            return RoadsGame.TakeBack(build, RoadData.Scene);
        }

        /// <summary>
        /// The plan's ground laid into both city walking meshes now: the game's colliders on it counted against the plan's,
        /// everything solid standing on it gathered to be built as no walking surface, Unity's builder proved once a load on
        /// data of the mod's own, then each mesh grown in rectangles of tiles (GroundBuilder.Grow), the old ground the plan's
        /// seeds stand on joined and the rest let go, the tiles round about compared with how they were. Null when done;
        /// else why it stood down. A mesh that fails is given its tiles back as they were; the player's, grown before the
        /// pedestrians' failed, stays so until the next load.
        /// </summary>
        public static string Ground(GroundPlan plan)
        {
            int found = 0;
            Seen.Clear();
            for (int b = 0; b < plan.Boxes.Count; b++)
            {
                found += WalkGame.Built(plan.Boxes[b], Seen);
            }
            if (found > plan.ExpectColliders)
            {
                return "the game has " + found + " colliders of its own on the plan's ground, " + plan.ExpectColliders + " when it was made (a game update has built there): "
                       + string.Join("; ", Seen);
            }
            Standing.Clear();
            Counted.Clear();
            for (int b = 0; b < plan.Boxes.Count; b++)
            {
                WalkGame.Obstacles(plan.Boxes[b], Counted, Standing);
            }
            string why = Grow(WalkGame.Player, plan, plan.Player);
            return why ?? Grow(WalkGame.Npc, plan, plan.Npc);
        }

        private static string Grow(CityMesh mesh, GroundPlan plan, bool wanted)
        {
            if (!wanted)
            {
                return null;
            }
            if (mesh == null)
            {
                return WalkGame.Missing;
            }
            if (plan.Boxes.Count == 0)
            {
                return "the plan has no ground";
            }
            if (!Proved.TryGetValue(mesh, out float flat))
            {
                string dry = GroundBuilder.DryRun(mesh, new Vector3(plan.Boxes[0].X, plan.Boxes[0].Top, plan.Boxes[0].Z), out _, out flat);
                if (dry != null)
                {
                    return "Unity's builder failed its dry run for the " + mesh.Label + " mesh: " + dry;
                }
                Proved[mesh] = flat;
            }
            string why = GroundBuilder.Grow(mesh, plan.Boxes, plan.Area, Standing, plan.Seeds, flat, out int walkable, out GroundJob job);
            if (why == null && job != null)
            {
                LastWalk = "the " + mesh.Label + " mesh walkable on " + walkable + " of the new ground's " + plan.Boxes.Count + " boxes; " + job.Covered.Count + " tiles ("
                           + job.Fresh.Count + " new in " + job.FreshRects.Count + " update(s), " + job.Joining.Count + " joined to the city's own ground in " + job.JoinRects.Count
                           + "), " + job.OldTriangles + " of the city's own triangles kept, " + job.SeededIslands + " of " + job.Islands + " islands of old ground joined, "
                           + Standing.Count + " things standing on it walked round, built in " + job.Ms.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms";
            }
            return why;
        }

        /// <summary>The city is going: the next load proves the builder again on its own meshes.</summary>
        public static void Forget()
        {
            Proved.Clear();
            WalkGame.ForgetTriangles();
        }
    }
}
