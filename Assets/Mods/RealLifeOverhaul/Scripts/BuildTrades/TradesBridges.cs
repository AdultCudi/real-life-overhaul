using System;
using UnityEngine;

namespace RealLifeOverhaul.BuildTrades
{
    /// <summary>A works site as the trades see it: filled in by civic life's construction programme (Wiring/TradesLinks), one object reused.</summary>
    public sealed class TradeSite
    {
        public int Id;
        public int Kind;           // Civic's SiteKind: 0 roadworks .. 4 a rebuild, 5 a plot
        public int Phase;          // 0 to 4
        public bool Working;       // within its working hours now
        public bool Unsafe;        // left unsafe by its contractor now (an inspector finds it)
        public Vector3 At;         // in front of the building's door; a plot's middle
        public bool Plot;          // on given ground: its size and turn
        public int Width, Depth, Yaw;
        public string Project;     // its name, the contractor's and the address, in the player's language
        public string Contractor;
        public string Address;
    }

    /// <summary>
    /// What the building trades ask of other features, each a delegate with a default that works without them, set by
    /// Wiring/TradesLinks: the player's courses and skills (Careers), the works sites (civic life's construction programme),
    /// and an unsafe site reported. Every use is wrapped; nothing here is called per frame.
    /// </summary>
    public static class TradesBridges
    {
        /// <summary>True if the player has passed a course (StudyCatalogue id). Default: none passed.</summary>
        public static Func<int, bool> Passed;

        /// <summary>The player's whole points of a skill (Careers' skills). Default: none.</summary>
        public static Func<int, int> Points;

        /// <summary>A skill learnt at work: the skill and thousandths of a point. Default: nothing is learnt.</summary>
        public static Action<int, int> Learn;

        /// <summary>The works site nearest a point within so many metres, written into the object given; false for none. Default: none.</summary>
        public static Func<Vector3, float, TradeSite, bool> Nearest;

        /// <summary>A site by its id, written into the object given; false once it is gone. Default: none.</summary>
        public static Func<int, TradeSite, bool> Site;

        /// <summary>The inspector fails a site: reported unsafe to the city as the notice's own report does; true if it was. Default: nothing.</summary>
        public static Func<int, bool> ReportUnsafe;

        internal static bool HasPassed(int course)
        {
            try
            {
                return Passed != null && Passed(course);
            }
            catch (Exception e)
            {
                TradesDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static int PointsOf(int skill)
        {
            try
            {
                return skill >= 0 && Points != null ? Points(skill) : 0;
            }
            catch (Exception e)
            {
                TradesDirector.Logger?.Error(e);
                return 0;
            }
        }

        internal static void Learnt(int skill, int thousandths)
        {
            try
            {
                if (skill >= 0 && thousandths > 0)
                {
                    Learn?.Invoke(skill, thousandths);
                }
            }
            catch (Exception e)
            {
                TradesDirector.Logger?.Error(e);
            }
        }

        internal static bool NearestSite(Vector3 at, float within, TradeSite into)
        {
            try
            {
                return Nearest != null && Nearest(at, within, into);
            }
            catch (Exception e)
            {
                TradesDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static bool SiteById(int id, TradeSite into)
        {
            try
            {
                return Site != null && Site(id, into);
            }
            catch (Exception e)
            {
                TradesDirector.Logger?.Error(e);
                return false;
            }
        }

        internal static bool Report(int id)
        {
            try
            {
                return ReportUnsafe != null && ReportUnsafe(id);
            }
            catch (Exception e)
            {
                TradesDirector.Logger?.Error(e);
                return false;
            }
        }

        private static readonly Func<int, bool> HoldsCourse = HasPassed;
        private static readonly Func<int, int> SkillPoints = PointsOf;

        /// <summary>True if the player holds what a trade, a rank or a role asks (<see cref="TradeRules.Holds"/> with the player's own file).</summary>
        public static bool Holds(TradeNeed need) => TradeRules.Holds(need, HoldsCourse, SkillPoints);

        /// <summary>True if the player qualifies for a role of a construction company's (TradeCatalogue.Roles' id); an unknown role, false.</summary>
        public static bool Qualified(string roleId)
        {
            TradeRole role = TradeCatalogue.Role(roleId);
            return role != null && Holds(role.Needs);
        }

        /// <summary>A construction company's project wants the player's design: its brief goes to the architects' office (replaced if asked again).</summary>
        public static void Commission(string projectId, int storeys, int units, int budget)
        {
            if (DesignBook.Loaded)
            {
                DesignBook.Commission(projectId, storeys, units, budget);
                TradesDirector.Instance?.CommissionAdded(projectId);
            }
        }

        /// <summary>The design the player finished for a project; false until they have.</summary>
        public static bool DesignOf(string projectId, out DesignScore score)
        {
            score = default;
            return DesignBook.Loaded && DesignBook.For(projectId, out score);
        }
    }
}
