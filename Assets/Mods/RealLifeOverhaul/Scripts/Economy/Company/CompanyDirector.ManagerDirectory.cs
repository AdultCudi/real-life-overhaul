using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One place as the Managers tab's directory lists it, read for each page written; the app keeps a pool of them.</summary>
    internal sealed class ManagerLine
    {
        public ManagedPlace Mp;
        public PostLine Line;
        public PostState State;
        public int Needs;           // ManagerDirectoryRules.NeedMistake and the others, a bit each
        public int Allowance;       // a day, at prices; 0 with no manager
    }

    /// <summary>
    /// What the Managers tab's directory reads of the managers (ManagerDirectoryRules): each place's post as it stands
    /// today, the counts of every group, who stands in for whom, a manager's pay, cost and chance of a mistake today, and
    /// who a decision is counted to. Only the player's own places are read (Managed, from RotaGame.ReadPlaces, which takes
    /// only those Going Public calls theirs). Nothing per frame: it runs when a page of the tab is written.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly Dictionary<string, int> _openAt = new Dictionary<string, int>(16, StringComparer.Ordinal);
        private readonly Dictionary<string, int> _mistakesAt = new Dictionary<string, int>(16, StringComparer.Ordinal);

        /// <summary>The days of the game's month, as the managers' report counts them.</summary>
        internal int ManagerMonthDays => MonthDays;

        /// <summary>
        /// Every place of the player's into <paramref name="into"/> (from <paramref name="pool"/>), and every group's
        /// counts. One pass over the places, one over the log and one over the districts.
        /// </summary>
        internal void ManagerLook(List<ManagerLine> into, List<ManagerLine> pool, DirectoryCounts counts)
        {
            for (int i = 0; i < into.Count; i++)
            {
                into[i].Mp = null;
                pool.Add(into[i]);
            }
            into.Clear();
            counts.Clear();
            if (_mgr == null)
            {
                return;
            }
            _openAt.Clear();
            _mistakesAt.Clear();
            for (int i = 0; i < _mgr.Decisions.Count; i++)
            {
                Decision d = _mgr.Decisions[i];
                if (d.Open)
                {
                    _openAt[d.Place] = (_openAt.TryGetValue(d.Place, out int n) ? n : 0) + 1;
                    _mistakesAt[d.Place] = (_mistakesAt.TryGetValue(d.Place, out int k) ? k : 0) + (d.Mistake ? 1 : 0);
                }
            }
            int monthDays = MonthDays;
            for (int i = 0; i < _managed.Count; i++)
            {
                ManagedPlace mp = _managed[i];
                ManagerLine line = pool.Count > 0 ? pool[pool.Count - 1] : new ManagerLine();
                if (pool.Count > 0)
                {
                    pool.RemoveAt(pool.Count - 1);
                }
                ManagerFile own = Working(mp.Own), region = Working(mp.Region);
                int open, wrong;
                _openAt.TryGetValue(mp.Place.Place, out open);
                _mistakesAt.TryGetValue(mp.Place.Place, out wrong);
                line.Mp = mp;
                line.Allowance = own != null ? AllowanceOf(own) : 0;
                line.Line = new PostLine
                {
                    Kind = mp.Kind,
                    Has = own != null,
                    Sick = own != null && Sick(own),
                    RegionThere = region != null,
                    RegionSick = region != null && Sick(region),
                    Open = open,
                    OpenMistakes = wrong,
                    Spent = own != null ? own.Spent : 0,
                    Budget = own != null ? BudgetOf(own) : 0,
                    Cost = ManagerDirectoryRules.MonthCost(line.Allowance, monthDays),
                    Decided = own != null ? own.Decided : 0
                };
                line.State = ManagerDirectoryRules.State(line.Line.Has, line.Line.Sick, line.Line.RegionThere, line.Line.RegionSick);
                line.Needs = ManagerDirectoryRules.NeedsOf(line.Line);
                counts.Add(line.Line);
                into.Add(line);
            }
            for (int i = 0; i < _districts.Count; i++)
            {
                ManagerFile r = Working(RegionOf(_districts[i]));
                counts.AddRegion(r != null, r != null && Sick(r), r != null ? MonthCostOf(r) : 0);
            }
        }

        // A manager who decides at all: found at work on the last read (one who is not is stood down on the next one).
        private static ManagerFile Working(ManagerFile m)
        {
            return m != null && m.Seen ? m : null;
        }

        /// <summary>True while the game has a manager off sick today.</summary>
        internal static bool Sick(ManagerFile m)
        {
            return m != null && ManagerGame.Absent(ManagerGame.Employee(m.Id));
        }

        /// <summary>How a place's post stands today, as its decisions are made (ManagerRules.Decider asks the same).</summary>
        internal PostState PostOf(ManagedPlace mp)
        {
            ManagerFile own = mp != null ? Working(mp.Own) : null, region = mp != null ? Working(mp.Region) : null;
            return ManagerDirectoryRules.State(own != null, Sick(own), region != null, Sick(region));
        }

        /// <summary>A decision's chance of a mistake at a place today, thousandths, as its decisions are made; -1 while nobody decides there.</summary>
        internal int ChanceToday(ManagedPlace mp)
        {
            int skill, review;
            string who;
            return mp != null && Decider(mp, out skill, out review, out who) ? ManagerRules.MistakeChance(skill, review) : -1;
        }

        /// <summary>A manager's allowance over a month of the game's days, at today's prices.</summary>
        internal int MonthCostOf(ManagerFile m)
        {
            return m != null ? ManagerDirectoryRules.MonthCost(AllowanceOf(m), MonthDays) : 0;
        }

        /// <summary>A manager's own hourly wage from the game, at today's prices, hundredths; 0 if the game no longer has them.</summary>
        internal int PayOf(ManagerFile m)
        {
            Entities.EmployeeInstance e = m != null ? ManagerGame.Employee(m.Id) : null;
            return e != null ? ManagerGame.WageNow(e, _index) : 0;
        }

        /// <summary>The places of a district whose manager is off sick today with its regional manager standing in.</summary>
        internal int StandingIn(string district)
        {
            int count = 0;
            for (int i = 0; i < _managed.Count; i++)
            {
                count += string.Equals(_managed[i].District, district, StringComparison.Ordinal) && PostOf(_managed[i]) == PostState.Covered ? 1 : 0;
            }
            return count;
        }

        /// <summary>The district a regional manager covers now, if this employee is one; null if not.</summary>
        internal string RegionHeldBy(string id)
        {
            ManagerFile m = _mgr != null && !string.IsNullOrEmpty(id) ? _mgr.Of(id) : null;
            return m != null && m.Region ? m.Place : null;
        }

        /// <summary>
        /// Who a decision at a place is counted to: the regional manager when it was made under their name, standing in
        /// for the place's manager off sick (ManagerDirectoryRules.ByRegion); otherwise the place's manager.
        /// </summary>
        private ManagerFile MadeBy(ManagedPlace mp, string who)
        {
            if (mp == null)
            {
                return null;
            }
            return mp.Region != null && ManagerDirectoryRules.ByRegion(who, mp.Own != null ? mp.Own.Name : null, mp.Region.Name) ? mp.Region : mp.Own;
        }
    }
}
