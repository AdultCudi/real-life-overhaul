using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>How far the player's building has come on the way to a builder, as the front desk sees it. Never saved.</summary>
    public enum DeskStep
    {
        Land = 0,        // nothing to build on and no project: a lot or a building of theirs first
        Plans = 1,       // ground of their own, but no plans drawn and priced
        Permission = 2,  // plans drawn: planning permission at City Hall next
        Deciding = 3,    // an application in, the council still deciding
        Ready = 4,       // a project a builder can be hired for now
        Building = 5     // a builder of the city's already at work, and nothing else waiting
    }

    /// <summary>
    /// The front desk at a builder's office and at the player's own (docs/features/construction.md, "The front desk"):
    /// what the person there talks about first, which desk is the front one, and the counts the office manager gives.
    /// The quotes and the contract are CommissionRules' and the director's own, never priced here. Game-free (EconomyCheck's
    /// BuilderDeskChecks); nothing allocated. COST: nothing per frame; a pass over the projects on a press.
    /// </summary>
    public static class FrontDeskRules
    {
        /// <summary>
        /// The step of the player's building furthest along of those a builder can be hired for (a plot or a rebuild of theirs,
        /// no builder yet), the first of equal ones, and its project's id; with none, a building a builder of the city's is
        /// putting up for them, else the plans when they have ground (<paramref name="hasGround"/>: a lot or a building of
        /// theirs) and the land when they have none. <paramref name="project"/> is 0 when no project is meant.
        /// </summary>
        public static DeskStep Step(IList<ProjectFile> projects, bool hasGround, out int project)
        {
            project = 0;
            DeskStep best = hasGround ? DeskStep.Plans : DeskStep.Land;
            int rank = 0, building = 0;
            for (int i = 0; projects != null && i < projects.Count; i++)
            {
                ProjectFile p = projects[i];
                if (p == null || (p.Kind != ProjectKind.Plot && p.Kind != ProjectKind.Rebuild))
                {
                    continue;
                }
                if (p.Contractor >= 0)
                {
                    building = building == 0 && p.Stage == ProjectStage.Building ? p.Id : building;
                    continue;
                }
                DeskStep s = Of(p);
                int r = s == DeskStep.Ready ? 4 : s == DeskStep.Deciding ? 3 : s == DeskStep.Permission ? 2 : s == DeskStep.Plans ? 1 : 0;
                if (r > rank)
                {
                    rank = r;
                    best = s;
                    project = p.Id;
                }
            }
            if (rank == 0 && building > 0)
            {
                project = building;
                return DeskStep.Building;
            }
            return best;
        }

        // One project's step; Land for one past being built for (the company's own works, finished or given up).
        private static DeskStep Of(ProjectFile p)
        {
            switch (p.Stage)
            {
                case ProjectStage.Ready:
                    return p.Estimate > 0 ? DeskStep.Ready : DeskStep.Plans;
                case ProjectStage.Planning:
                    return DeskStep.Deciding;
                case ProjectStage.Design:
                    return p.PlansDone ? DeskStep.Permission : DeskStep.Plans;
                default:
                    return DeskStep.Land;
            }
        }

        /// <summary>The text key of what the desk says a step needs first; null for a project ready to quote.</summary>
        public static string NeedKey(DeskStep step)
        {
            switch (step)
            {
                case DeskStep.Land: return "reallife_bld_desk_need_land";
                case DeskStep.Plans: return "reallife_bld_desk_need_plans";
                case DeskStep.Permission: return "reallife_bld_desk_need_permission";
                case DeskStep.Deciding: return "reallife_bld_desk_need_decision";
                case DeskStep.Building: return "reallife_bld_desk_need_building";
                default: return null;
            }
        }

        /// <summary>
        /// The front desk of an office: of the desks with somebody at them (their places on the ground, m), the one nearest
        /// the door the game lets the player in by, the first of equal ones; -1 for none.
        /// </summary>
        public static int Front(float[] x, float[] z, int count, float doorX, float doorZ)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            int n = x != null && z != null ? System.Math.Min(count, System.Math.Min(x.Length, z.Length)) : 0;
            for (int i = 0; i < n; i++)
            {
                float dx = x[i] - doorX, dz = z[i] - doorZ, distance = dx * dx + dz * dz;
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>The office manager's counts: projects under way (not finished or given up) and of them those being built.</summary>
        public static int UnderWay(IList<ProjectFile> projects, out int building)
        {
            int n = 0;
            building = 0;
            for (int i = 0; projects != null && i < projects.Count; i++)
            {
                ProjectFile p = projects[i];
                if (p == null || p.Stage == ProjectStage.Finished || p.Stage == ProjectStage.Abandoned)
                {
                    continue;
                }
                n++;
                building += p.Stage == ProjectStage.Building ? 1 : 0;
            }
            return n;
        }
    }
}
