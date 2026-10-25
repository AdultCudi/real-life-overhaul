using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Economy
{
    // What the company's hierarchy keeps of a save, and no rules. Game-free: EconomyCheck compiles this file.

    /// <summary>
    /// One place as the escalations follow it from day to day: when its staff fell short, when they began to stand low,
    /// how many days it has been off its targets, and the days its staff left. Kept only while something is being counted.
    /// </summary>
    public sealed class PlaceWatch
    {
        public string Place = string.Empty;
        public int GapFrom;             // the day the staff fell under the number kept; 0 while they are not
        public int LowFrom;             // the day the staff's standing fell under the floor; 0 while it is not
        public int KpiRun;              // days off target in a row
        public int KpiDay;              // the last day counted
        public int Staff = -1;          // the staff as counted yesterday; -1 before the first count
        public int TopUp;               // what the finance chief added to the manager's budget this month, at prices
        public int TopUpFrom;           // the first day of the month that was (the managers' MonthFrom)
        public readonly List<int> Left = new List<int>(4);   // the days staff left, the last week's

        public bool Idle => GapFrom == 0 && LowFrom == 0 && KpiRun == 0 && Left.Count == 0 && TopUp == 0;

        /// <summary>The top-up of the month that began on a day; nothing for another month's.</summary>
        public int TopUpOf(int monthFrom) => TopUpFrom == monthFrom ? TopUp : 0;

        /// <summary>Leavers within a week of a day: those counted on the days since.</summary>
        public int LeftSince(int day)
        {
            int n = 0;
            for (int i = 0; i < Left.Count; i++)
            {
                n += day - Left[i] < PolicyCatalogue.WaveWindow ? 1 : 0;
            }
            return n;
        }
    }

    /// <summary>
    /// One escalation: what it is about, where, who holds it now and since when, and the figures that raised it. The
    /// figures mean what the kind says (CompanyDirector.Escalations, the Raise of each kind).
    /// </summary>
    public sealed class Escalation
    {
        public int Id;
        public int Kind;                        // EscalationKind
        public string Place = string.Empty;     // a place; a region's key for a region's own; empty for the company's
        public int Level;                       // who holds it: EscalationRules.LevelRegion, LevelExecutive or LevelOwner
        public int Day, Moved;                  // raised, and handed to its holder
        public int State;                       // EscalationState
        public int A, B, C;
        public string Subject = string.Empty;   // the employee an exception is about; a rule's number for a proposal
        public string Detail = string.Empty;    // a name, as it was when raised
        public string By = string.Empty;        // who settled it or passed it on last

        public bool Open => State == (int)EscalationState.Open;
    }

    /// <summary>
    /// One entry of the hierarchy's log: something a regional manager, an executive or the hierarchy itself did or was paid,
    /// for the owner to look back on. The figures mean what the kind says (HierarchyLog).
    /// </summary>
    public sealed class HierarchyEntry
    {
        public int Id;
        public int Day, Hour;
        public int Kind;                        // HierarchyLog
        public int Level;                       // the rung of who did it (Careers/Ladder/LadderRanks: Rung)
        public string Who = string.Empty;
        public string Place = string.Empty;
        public int A, B, C;
        public string Detail = string.Empty;
    }

    /// <summary>What a log entry records. Saved as a number: never renumber, add at the end.</summary>
    public enum HierarchyLog
    {
        Raised = 0,         // an escalation raised: A its id, B its kind
        Passed = 1,         // passed up: A its id, B the level it went to
        Settled = 2,        // settled by its holder: A its id, B its kind
        Moved = 3,          // a member of staff moved between stores: A 1 undone later; detail the name
        Approved = 4,       // an exception agreed: A the raise, hundredths an hour
        TopUp = 5,          // a budget added to: A the amount
        Appointed = 6,      // a store manager appointed by a regional manager: detail the name
        Replaced = 7,       // a store manager stood down by a regional manager: detail the name
        Proposed = 8,       // a proposal made: A the rule, B the step, C the step before
        Report = 9,         // a report made: A escalations open, B settled this week, C the score
        Paid = 10,          // an executive's allowance: A the amount
        Raises = 11,        // a batch of raises: A how many, B the raise a day in all
        Hired = 12,         // a gap filled from another place's board: detail the name
        Policy = 13         // a rule changed by the owner's approval: A the rule, B the step
    }

    /// <summary>One executive: their area, who they are (a professional of the player's), and their month.</summary>
    public sealed class ExecutiveFile
    {
        public int Area;                        // the seat (Careers/Ladder/LadderRanks: ExecSeat), Coo to Cfo
        public string Id = string.Empty;        // the professional's id (the game's employee id, or the mod's "pro:n")
        public string Name = string.Empty;
        public int Role, Level;                 // TalentCatalogue's role and level as last read
        public string Place = string.Empty;     // the place they work at
        public int From;                        // the day appointed
        public int Decided, Paid;               // this month
        public int LastDecided, LastPaid;       // the month before
        public int ProposedDay;                 // the last day they made a proposal

        // Read from the game; never saved.
        public int Going;                       // the going rate of their role and level, cents an hour
        public bool Seen;
    }

    /// <summary>A store put into a region the player made, instead of its district's.</summary>
    public sealed class AreaPick
    {
        public string Place = string.Empty;
        public string Area = string.Empty;      // HierarchyRules.CustomArea(n)
    }

    /// <summary>What the hierarchy keeps for a save.</summary>
    public sealed class HierarchyState
    {
        public const int EscalationsKept = 120, EntriesKept = 160, WatchesMost = 96, AreaPicksMost = 64;

        public readonly PolicySet Company = new PolicySet();
        public readonly List<PolicySet> Regions = new List<PolicySet>(4);
        public readonly List<PolicySet> Stores = new List<PolicySet>(8);
        public readonly List<PolicyTemplate> Templates = new List<PolicyTemplate>(2);
        public readonly List<PlaceWatch> Watches = new List<PlaceWatch>(16);
        public readonly List<Escalation> Escalations = new List<Escalation>(16);
        public readonly List<HierarchyEntry> Entries = new List<HierarchyEntry>(32);
        public readonly List<ExecutiveFile> Executives = new List<ExecutiveFile>(3);
        public readonly List<AreaPick> AreaPicks = new List<AreaPick>(4);
        public int NextId = 1;
        public int NextTemplate = 1;
        public int WeekFrom;                    // the first day of the week the reports count
        public int Settled;                     // escalations settled since the week began
        public bool Deep;                       // the owner's Deep Mode: every level's pages, not only what needs them

        public HierarchyState()
        {
            Company.Start();
        }

        public PolicySet Region(string key) => Find(Regions, key);

        public PolicySet Store(string place) => Find(Stores, place);

        private static PolicySet Find(List<PolicySet> sets, string scope)
        {
            for (int i = 0; scope != null && i < sets.Count; i++)
            {
                if (string.Equals(sets[i].Scope, scope, StringComparison.Ordinal))
                {
                    return sets[i];
                }
            }
            return null;
        }

        /// <summary>A region's or a store's set, made when first wanted; null past the most kept.</summary>
        public PolicySet Want(bool region, string scope)
        {
            List<PolicySet> sets = region ? Regions : Stores;
            PolicySet set = Find(sets, scope);
            if (set == null && sets.Count < (region ? PolicyCatalogue.RegionsMost : PolicyCatalogue.StoresMost) && !string.IsNullOrEmpty(scope))
            {
                set = new PolicySet { Scope = scope };
                sets.Add(set);
            }
            return set;
        }

        public PlaceWatch Watch(string place, bool make)
        {
            for (int i = 0; i < Watches.Count; i++)
            {
                if (string.Equals(Watches[i].Place, place, StringComparison.Ordinal))
                {
                    return Watches[i];
                }
            }
            if (!make || Watches.Count >= WatchesMost || string.IsNullOrEmpty(place))
            {
                return null;
            }
            var w = new PlaceWatch { Place = place };
            Watches.Add(w);
            return w;
        }

        /// <summary>The open escalation of a kind about a place; null with none.</summary>
        public Escalation OpenOf(EscalationKind kind, string place)
        {
            for (int i = 0; i < Escalations.Count; i++)
            {
                Escalation e = Escalations[i];
                if (e.Open && e.Kind == (int)kind && string.Equals(e.Place, place, StringComparison.Ordinal))
                {
                    return e;
                }
            }
            return null;
        }

        public Escalation Find(int id)
        {
            for (int i = Escalations.Count - 1; i >= 0; i--)
            {
                if (Escalations[i].Id == id)
                {
                    return Escalations[i];
                }
            }
            return null;
        }

        /// <summary>A new escalation at the end of the list; the oldest settled one let go past the number kept (an open one never is).</summary>
        public Escalation Add(EscalationKind kind, string place, int level, int day)
        {
            if (Escalations.Count >= EscalationsKept)
            {
                int drop = -1;
                for (int i = 0; i < Escalations.Count && drop < 0; i++)
                {
                    drop = Escalations[i].Open ? -1 : i;
                }
                Escalations.RemoveAt(drop >= 0 ? drop : 0);
            }
            var e = new Escalation { Id = NextId++, Kind = (int)kind, Place = place ?? string.Empty, Level = level, Day = day, Moved = day };
            Escalations.Add(e);
            return e;
        }

        /// <summary>A log entry at the end, the oldest let go past the number kept.</summary>
        public HierarchyEntry Log(HierarchyLog kind, int level, string who, string place, int day, int hour)
        {
            if (Entries.Count >= EntriesKept)
            {
                Entries.RemoveAt(0);
            }
            var e = new HierarchyEntry { Id = NextId++, Kind = (int)kind, Level = level, Who = who ?? string.Empty, Place = place ?? string.Empty, Day = day, Hour = hour };
            Entries.Add(e);
            return e;
        }

        public ExecutiveFile Executive(ExecSeat seat)
        {
            for (int i = 0; i < Executives.Count; i++)
            {
                if (Executives[i].Area == (int)seat)
                {
                    return Executives[i];
                }
            }
            return null;
        }

        public ExecutiveFile ExecutiveOf(string id)
        {
            for (int i = 0; id != null && i < Executives.Count; i++)
            {
                if (string.Equals(Executives[i].Id, id, StringComparison.Ordinal))
                {
                    return Executives[i];
                }
            }
            return null;
        }

        /// <summary>The region a store was put into; null for its district's.</summary>
        public string AreaPicked(string place)
        {
            for (int i = 0; place != null && i < AreaPicks.Count; i++)
            {
                if (string.Equals(AreaPicks[i].Place, place, StringComparison.Ordinal))
                {
                    return AreaPicks[i].Area;
                }
            }
            return null;
        }

        /// <summary>A store put into a region, or back into its district's with null.</summary>
        public void PickArea(string place, string area)
        {
            for (int i = AreaPicks.Count - 1; i >= 0; i--)
            {
                if (string.Equals(AreaPicks[i].Place, place, StringComparison.Ordinal))
                {
                    AreaPicks.RemoveAt(i);
                }
            }
            if (!string.IsNullOrEmpty(area) && AreaPicks.Count < AreaPicksMost)
            {
                AreaPicks.Add(new AreaPick { Place = place, Area = area });
            }
        }

        /// <summary>The open escalations held at a level.</summary>
        public int OpenAt(int level)
        {
            int n = 0;
            for (int i = 0; i < Escalations.Count; i++)
            {
                n += Escalations[i].Open && Escalations[i].Level == level ? 1 : 0;
            }
            return n;
        }
    }
}
