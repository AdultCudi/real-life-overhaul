using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What an escalation is about. Saved as a number: never renumber, add at the end.</summary>
    public enum EscalationKind
    {
        Gap = 0,            // the staff under the number kept for longer than the policy's days
        Budget = 1,         // a manager's month nearly spent
        Demand = 2,         // a request for a raise or a rival's offer left unanswered too long
        Mood = 3,           // the staff's standing under the policy's floor for days
        Wave = 4,           // several of the staff leaving one place within a week
        Kpi = 5,            // a place's figures off its targets past the tolerance for days
        Exception = 6,      // a raise or an offer past the pay band, waiting for whoever may agree it
        Proposal = 7,       // an executive's change to a company rule, for the owner to approve
        Count = 8
    }

    /// <summary>Where an escalation stands. Saved as a number.</summary>
    public enum EscalationState
    {
        Open = 0,           // with somebody, waiting
        Resolved = 1,       // settled by whoever held it
        Cleared = 2,        // what caused it went away by itself
        Dismissed = 3,      // the owner let it be
        Lapsed = 4          // what it asked about ran out of time (a request or an offer the staff gave up on)
    }

    /// <summary>
    /// The escalation rules, game-free (EconomyCheck compiles them): only real triggers raise one, each measured against
    /// the policy in force at the place; who holds it, from the place's manager up to the owner, with nobody's level
    /// skipped who is in post; which holder may settle which kind under the policy; and a place's score on its targets.
    /// </summary>
    public static class EscalationRules
    {
        public const int LevelRegion = 2, LevelExecutive = 3, LevelOwner = 4;   // HierarchyLevel as a number: who holds an escalation

        /// <summary>The executive whose area an escalation is in.</summary>
        public static ExecutiveArea AreaOf(EscalationKind kind)
        {
            switch (kind)
            {
                case EscalationKind.Gap:
                case EscalationKind.Kpi: return ExecutiveArea.Operations;
                case EscalationKind.Budget: return ExecutiveArea.Money;
                default: return ExecutiveArea.People;
            }
        }

        /// <summary>
        /// The next holder above a level: the regional manager if one is in post, then the area's executive if one is, then
        /// the owner. Nobody's level is skipped who is in post, and the owner holds what nobody else can.
        /// </summary>
        public static int Next(int level, bool region, bool executive)
        {
            if (level < LevelRegion && region)
            {
                return LevelRegion;
            }
            if (level < LevelExecutive && executive)
            {
                return LevelExecutive;
            }
            return LevelOwner;
        }

        // A regional manager fills a gap from the region (where the policy lets gaps be filled past the place's own board),
        // agrees an exception when the policy names them, and brings low or leaving staff up to the band. The operations
        // chief fills a gap from the whole company; the people chief agrees exceptions they or a regional manager are named
        // for (a level above may do what the one below may), answers what waits and brings staff up to the band; the
        // finance chief adds to a budget running out. The owner may settle anything; a proposal is only ever the owner's.
        /// <summary>Whether a holder may try to settle a kind under the policy, by their level and area.</summary>
        public static bool Tries(EscalationKind kind, int level, int approverStep, int sourcesStep)
        {
            int approver = PolicyCatalogue.Value(PolicyField.Approver, approverStep), sources = PolicyCatalogue.Value(PolicyField.Sources, sourcesStep);
            if (level >= LevelOwner)
            {
                return true;
            }
            if (kind == EscalationKind.Proposal)
            {
                return false;
            }
            if (level == LevelRegion)
            {
                switch (kind)
                {
                    case EscalationKind.Gap: return sources >= PolicyCatalogue.SourceRegion;
                    case EscalationKind.Exception: return approver == PolicyCatalogue.ApproveRegion;
                    case EscalationKind.Mood:
                    case EscalationKind.Wave: return true;
                    default: return false;
                }
            }
            if (level != LevelExecutive)
            {
                return false;
            }
            switch (kind)
            {
                case EscalationKind.Gap: return sources >= PolicyCatalogue.SourceRegion;
                case EscalationKind.Exception: return approver == PolicyCatalogue.ApprovePeople || approver == PolicyCatalogue.ApproveRegion;
                case EscalationKind.Demand:
                case EscalationKind.Mood:
                case EscalationKind.Wave:
                case EscalationKind.Budget: return true;
                default: return false;   // a place off its targets is reported to the owner, not settled
            }
        }

        /// <summary>Whether a gap that began on a day has lasted past the policy's days (it began the day the staff fell short).</summary>
        public static bool GapDue(int gapFrom, int day, int gapDaysStep)
        {
            return gapFrom > 0 && day - gapFrom >= PolicyCatalogue.Value(PolicyField.GapDays, gapDaysStep);
        }

        /// <summary>Whether a month's budget is about to be spent: the policy's share of it or more. A budget of nothing never is.</summary>
        public static bool BudgetDue(int spent, int budget, int shareStep)
        {
            return budget > 0 && (long)Math.Max(0, spent) * 1000 >= (long)budget * PolicyCatalogue.Value(PolicyField.BudgetShare, shareStep);
        }

        /// <summary>Whether a request or an offer asked on a day has waited past the policy's days unanswered.</summary>
        public static bool UnmetDue(int askedDay, int day, int unmetStep)
        {
            return askedDay > 0 && day - askedDay >= PolicyCatalogue.Value(PolicyField.UnmetDays, unmetStep);
        }

        /// <summary>Whether a place's staff stand under the floor (standing 0 to 100, an average); a floor of nothing is none.</summary>
        public static bool Low(int standing, int floorStep)
        {
            int floor = PolicyCatalogue.Value(PolicyField.MoodFloor, floorStep);
            return floor > PolicyCatalogue.MoodNone && standing < floor;
        }

        /// <summary>Whether a run of days under the floor, from the day it began, is long enough to escalate.</summary>
        public static bool MoodDue(int lowFrom, int day)
        {
            return lowFrom > 0 && day - lowFrom + 1 >= PolicyCatalogue.MoodDays;
        }

        /// <summary>Whether so many leaving one place within a week make a wave.</summary>
        public static bool WaveDue(int leavers, int waveStep)
        {
            return leavers >= PolicyCatalogue.Value(PolicyField.WaveCount, waveStep);
        }

        /// <summary>
        /// Whether a figure is on target: no target, or at least the target less the tolerance's share of it (of one unit at
        /// a target of nothing, so a break-even target lets a loss of a share of one through and no more).
        /// </summary>
        public static bool OnTarget(int value, int target, int toleranceStep, bool none)
        {
            if (none)
            {
                return true;
            }
            int tolerance = PolicyCatalogue.Value(PolicyField.Tolerance, toleranceStep);
            long room = (long)Math.Max(Math.Abs(target), 1) * tolerance / 1000;
            return value >= target - room;
        }

        /// <summary>Whether days off target in a row are enough to escalate.</summary>
        public static bool KpiDue(int run, int kpiDaysStep)
        {
            return run >= PolicyCatalogue.Value(PolicyField.KpiDays, kpiDaysStep);
        }

        /// <summary>
        /// A place's score on its targets, 0 to 100: each figure's share of its target (100 at or over it, a target of none
        /// counts 100), weighed by the policy's weight of profit against standing.
        /// </summary>
        public static int Score(int profit, int profitTarget, bool noProfitTarget, int standing, int moodTarget, int weightStep)
        {
            int weight = PolicyCatalogue.Value(PolicyField.ProfitWeight, weightStep);
            int p = noProfitTarget ? 100 : profitTarget <= 0 ? (profit >= profitTarget ? 100 : 0) : CompanyCatalogue.Clamp((int)((long)Math.Max(0, profit) * 100 / profitTarget), 0, 100);
            int m = moodTarget <= PolicyCatalogue.MoodNone ? 100 : CompanyCatalogue.Clamp(Math.Max(0, standing) * 100 / moodTarget, 0, 100);
            return (p * weight + m * (100 - weight)) / 100;
        }

        /// <summary>Whether an escalation of a kind about a place may be raised while another is open: one of each kind a place at a time.</summary>
        public static bool Raises(bool openOfKind)
        {
            return !openOfKind;
        }
    }
}
