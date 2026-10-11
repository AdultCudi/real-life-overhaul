using System;
using System.Collections.Generic;
using System.Text;
using Helpers;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The test tool "say what is blocking me here" (Options > Troubleshooting > Test tools). It answers "something
    /// I cannot see stops me on the pavement": the player walks on the game's navigation mesh, so what stops them is
    /// a hole in that mesh (cut by a carving <c>NavMeshObstacle</c>) or its edge, never a collider. One press writes
    /// to Player.log, whether or not diagnostics are on:
    ///
    ///  - where the player stands, how far the walkable mesh is from their feet and how far its nearest edge;
    ///  - every walking obstacle within <see cref="Reach"/> metres: whose it is (its root object: the mod's own
    ///    start with "RealLifeOverhaul"), whether it cuts the mesh, its size, and whether anything of it is drawn;
    ///  - every other walker (a navigation agent) within the same reach: one nobody can see stops the player like a wall;
    ///  - every solid collider within the same reach that is not the ground, the same way.
    ///
    /// An obstacle that cuts and is not drawn is the unseen block. Per frame: nothing. A press goes through the
    /// scene's obstacles once.
    /// </summary>
    internal static class BlockTests
    {
        private const float Reach = 6f;             // metres round the player that are looked at
        private const float NoticeSeconds = 9f;
        private const int Most = 24;                // lines of each kind at most: a press never floods the log

        private static readonly Collider[] Solids = new Collider[64];
        private static readonly StringBuilder Text = new StringBuilder(2048);

        /// <summary>"Test: say what is blocking me here".</summary>
        public static void Say()
        {
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (!ModState.Loaded || SaveGameManager.Current == null || player == null)
                {
                    TestRefusals.Tell(ClickWatch.Logger, "Block", TestRefusal.NotLoaded);
                    return;
                }
                Vector3 feet = player.transform.position;
                Text.Length = 0;
                Text.Append("Block check: the player at (").Append(feet.x.ToString("0.0")).Append(", ").Append(feet.y.ToString("0.0")).Append(", ").Append(feet.z.ToString("0.0")).Append(")");
                Text.Append(NavMesh.SamplePosition(feet, out NavMeshHit on, Reach, NavMesh.AllAreas)
                    ? "; the walkable mesh is " + Vector3.Distance(on.position, feet).ToString("0.00") + " m from their feet"
                    : "; no walkable mesh within " + Reach + " m");
                if (NavMesh.FindClosestEdge(feet, out NavMeshHit edge, NavMesh.AllAreas))
                {
                    Vector3 to = edge.position - feet;
                    Text.Append("; its nearest edge is ").Append(edge.distance.ToString("0.00")).Append(" m away, towards (").Append(to.x.ToString("0.0")).Append(", ").Append(to.z.ToString("0.0")).Append(")");
                }
                Text.Append(". The mod's obstacles changed so far: ").Append(MeshCuts.Total).Append(MeshCuts.Last != null ? ", the last: " + MeshCuts.Last : string.Empty).Append('.');
                ClickWatch.Logger?.Warn(Text.ToString());

                int obstacles = 0, unseen = 0;
                NavMeshObstacle[] all = UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < all.Length; i++)
                {
                    NavMeshObstacle o = all[i];
                    Vector3 centre = o.transform.TransformPoint(o.center);
                    Vector3 size = Vector3.Scale(o.shape == NavMeshObstacleShape.Box ? o.size : new Vector3(o.radius * 2f, o.height, o.radius * 2f), Abs(o.transform.lossyScale));
                    float flat = Flat(centre, feet) - Mathf.Max(size.x, size.z) * 0.5f;
                    if (!o.enabled || flat > Reach)
                    {
                        continue;
                    }
                    bool drawn = Drawn(o.transform, out int renderers, out int seen) && seen > 0;
                    unseen += o.carving && !drawn ? 1 : 0;
                    if (obstacles++ < Most)
                    {
                        Text.Length = 0;
                        Text.Append("Block check (obstacle): '").Append(Path(o.transform)).Append("', ").Append(Mathf.Max(0f, flat).ToString("0.0")).Append(" m away, ")
                            .Append(o.carving ? "CUTS the mesh" : "does not cut the mesh").Append(", ").Append(size.x.ToString("0.0")).Append(" x ").Append(size.z.ToString("0.0")).Append(" m, ")
                            .Append(drawn ? "drawn" : "NOT DRAWN").Append(" (").Append(renderers).Append(" renderer(s) of its own, ").Append(seen).Append(" in view), its middle ")
                            .Append((centre.x - feet.x).ToString("0.0")).Append(" m east and ").Append((centre.z - feet.z).ToString("0.0")).Append(" m north of the player, turned ")
                            .Append(Mathf.RoundToInt(o.transform.eulerAngles.y)).Append(" degrees, layer ").Append(LayerMask.LayerToName(o.gameObject.layer)).Append('.');
                        ClickWatch.Logger?.Warn(Text.ToString());
                    }
                }

                int agents = 0;
                NavMeshAgent[] people = UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < people.Length; i++)
                {
                    NavMeshAgent a = people[i];
                    float flat = Flat(a.transform.position, feet);
                    if (!a.enabled || flat > Reach || a.transform.IsChildOf(player.transform) || player.transform.IsChildOf(a.transform))
                    {
                        continue;
                    }
                    bool drawn = Drawn(a.transform, out int renderers, out int seen) && seen > 0;
                    unseen += drawn ? 0 : 1;
                    if (agents++ < Most)
                    {
                        Text.Length = 0;
                        Text.Append("Block check (walker): '").Append(Path(a.transform)).Append("', ").Append(flat.ToString("0.0")).Append(" m away, radius ").Append(a.radius.ToString("0.00")).Append(" m, ")
                            .Append(drawn ? "drawn" : "NOT DRAWN").Append(" (").Append(renderers).Append(" renderer(s), ").Append(seen).Append(" in view), avoidance ").Append(a.obstacleAvoidanceType).Append('.');
                        ClickWatch.Logger?.Warn(Text.ToString());
                    }
                }

                int solids = 0;
                int found = Physics.OverlapSphereNonAlloc(feet + Vector3.up, Reach, Solids, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < found; i++)
                {
                    Collider c = Solids[i];
                    Solids[i] = null;
                    if (c == null || c.transform.IsChildOf(player.transform) || c.bounds.size.x > 40f || c.bounds.size.z > 40f)
                    {
                        continue;   // (the player's own, and the ground or a building as a whole)
                    }
                    bool drawn = Drawn(c.transform);
                    if (!drawn && solids++ < Most)
                    {
                        Text.Length = 0;
                        Text.Append("Block check (solid, not drawn): '").Append(Path(c.transform)).Append("', ").Append(Flat(c.bounds.center, feet).ToString("0.0")).Append(" m away, ")
                            .Append(c.GetType().Name).Append(' ').Append(c.bounds.size.x.ToString("0.0")).Append(" x ").Append(c.bounds.size.z.ToString("0.0")).Append(" m, layer ")
                            .Append(LayerMask.LayerToName(c.gameObject.layer)).Append('.');
                        ClickWatch.Logger?.Warn(Text.ToString());
                    }
                }
                ClickWatch.Logger?.Warn("Block check: " + obstacles + " walking obstacle(s) and " + agents + " walker(s) within " + Reach + " m, " + unseen + " of them with nothing in view; " + solids + " solid thing(s) with nothing drawn.");
                var data = new Dictionary<string, string>(2) { { "obstacles", obstacles.ToString() }, { "unseen", unseen.ToString() } };
                Notifications.Show(NotificationType.Info, "reallife_test_block_said", data, NoticeSeconds, null, null, true, false);
            }
            catch (Exception e)
            {
                ClickWatch.Logger?.Error(e);
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        // What of the thing itself is drawn: its own renderers and those under it, never a parent's (a parked car's parent is
        // the road, which is always drawn). "IN VIEW" is the engine's own word that a camera drew it this frame.
        private static bool Drawn(Transform t) => Drawn(t, out _, out _);

        private static bool Drawn(Transform t, out int renderers, out int seen)
        {
            renderers = seen = 0;
            int on = 0;
            Renderer[] all = t.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r is ParticleSystemRenderer)
                {
                    continue;
                }
                renderers++;
                bool can = r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff;
                on += can ? 1 : 0;
                seen += can && r.isVisible ? 1 : 0;
            }
            return on > 0;
        }

        // The thing's name under its parents, the scene's root first.
        private static string Path(Transform t)
        {
            string path = t.name;
            int depth = 0;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                path = depth++ < 3 || p.parent == null ? p.name + "/" + path : path;
            }
            return path;
        }
    }
}
