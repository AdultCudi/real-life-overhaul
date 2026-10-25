using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Careers;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The regional managers' own part of the hierarchy (L2): the regions a player draws from their stores, a regional
    /// manager appointing a manager where a store of theirs has none and replacing one whose place stays off its targets
    /// (each where the region's policy allows it), their dashboard's quick actions over the whole region, and their week's
    /// report. On the hierarchy's day and the player's buttons; nothing per frame.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly List<ManagerHand> _regionHands = new List<ManagerHand>(8);

        /// <summary>A store put into a region the player made (1 to HierarchyRules.CustomMost), or back into its district's with 0.</summary>
        public void HierarchyPickArea(string place, int number)
        {
            ManagedPlace mp = HierarchyOn ? ManagedAt(place) : null;
            if (mp == null)
            {
                return;
            }
            _hier.PickArea(place, number > 0 ? HierarchyRules.CustomArea(number) : null);
            ManagerRead();
            Logger?.Info("Company: " + mp.Place.Name + " is now in " + (number > 0 ? "region " + HierarchyRules.Letter(number) : "its district's region") + " for its regional manager.");
            Changed();
        }

        // The regional managers' round: each at work appoints and replaces where the region's policy lets them.
        private void RegionRound(int day, int hour)
        {
            for (int i = 0; i < _districts.Count; i++)
            {
                string area = _districts[i];
                ManagerFile r = RegionAtWork(area);
                if (r == null)
                {
                    continue;
                }
                if (RegionRule(area, PolicyField.RegionAppoints) == 1)
                {
                    RegionAppoint(area, r.Name, day, hour);
                }
                if (RegionRule(area, PolicyField.RegionReplaces) == 1)
                {
                    RegionReplace(area, r.Name, day, hour);
                }
            }
        }

        /// <summary>A manager appointed at every store of a region with staff and none, the most skilled of its staff. Returns how many.</summary>
        internal int RegionAppoint(string area, string who, int day, int hour)
        {
            int made = 0;
            for (int i = 0; i < _managed.Count && _mgr.Managers.Count < ManagerCatalogue.ManagersMost; i++)
            {
                ManagedPlace mp = _managed[i];
                if (mp.Own != null || mp.Place.Staff <= 0 || mp.Place.Reg == null || !GoingPublic.Mine(mp.Place.Reg) || !string.Equals(mp.Area, area, StringComparison.Ordinal))
                {
                    continue;
                }
                ManagerGame.StaffAt(mp.Place, _index, _regionHands);
                int best = Best(_regionHands, false);
                EmployeeInstance e = best >= 0 ? ManagerGame.Employee(_regionHands[best].Id) : null;
                ManagerFile m = e != null ? Appointed(mp, e, _regionHands[best].Id) : null;
                if (m == null)
                {
                    continue;
                }
                made++;
                HierarchyEntry entry = _hier.Log(HierarchyLog.Appointed, (int)Rung.Regional, who, mp.Place.Place, day, hour);
                entry.A = m.Skill;
                entry.Detail = HierarchyStore.Clean(m.Name);
                Logger?.Info("Company: " + who + " (regional manager of " + AreaName(area) + ") appointed " + m.Name + " manager of " + mp.Place.Name + ".");
            }
            _regionHands.Clear();
            return made;
        }

        /// <summary>
        /// A store's manager replaced by its most skilled other member of staff, more skilled than them, where its place has
        /// been off its targets for twice the policy's days and scores under RegionCatalogue.ReplaceScore. Returns how many.
        /// </summary>
        internal int RegionReplace(string area, string who, int day, int hour)
        {
            int made = 0;
            for (int i = 0; i < _managed.Count; i++)
            {
                ManagedPlace mp = _managed[i];
                ManagerFile own = Working(mp.Own);
                PlaceWatch w = own != null ? _hier.Watch(mp.Place.Place, false) : null;
                if (w == null || !string.Equals(mp.Area, area, StringComparison.Ordinal) || mp.Place.Reg == null || !GoingPublic.Mine(mp.Place.Reg)
                    || !HierarchyRules.ReplaceDue(w.KpiRun, PolicyCatalogue.Value(PolicyField.KpiDays, Rule(mp, PolicyField.KpiDays)), ScoreOf(mp)))
                {
                    continue;
                }
                ManagerGame.StaffAt(mp.Place, _index, _regionHands);
                int best = -1;
                for (int k = 0; k < _regionHands.Count; k++)
                {
                    ManagerHand h = _regionHands[k];
                    if (h.Id != own.Id && h.Skill > own.Skill && _mgr.Of(h.Id) == null && (best < 0 || h.Skill > _regionHands[best].Skill))
                    {
                        best = k;
                    }
                }
                EmployeeInstance e = best >= 0 ? ManagerGame.Employee(_regionHands[best].Id) : null;
                string before = own.Name;
                ManagerFile m = e != null ? Appointed(mp, e, _regionHands[best].Id) : null;
                if (m == null)
                {
                    continue;
                }
                w.KpiRun = 0;   // the new manager's own run starts from nothing
                made++;
                HierarchyEntry entry = _hier.Log(HierarchyLog.Replaced, (int)Rung.Regional, who, mp.Place.Place, day, hour);
                entry.A = m.Skill;
                entry.Detail = HierarchyStore.Clean(before).Replace('/', ' ') + "/" + HierarchyStore.Clean(m.Name).Replace('/', ' ');
                Logger?.Info("Company: " + who + " (regional manager of " + AreaName(area) + ") replaced " + before + " with " + m.Name + " at " + mp.Place.Name
                             + ", off its targets and scoring " + ScoreOf(mp) + ".");
            }
            _regionHands.Clear();
            return made;
        }

        // --- The regional dashboard's quick actions, the player acting as the region's level within its policy ---

        /// <summary>Every gap in a region filled: each store's own board, then the region's boards and moves as the policy allows.</summary>
        public void HierarchyFillRegion(string area)
        {
            int came = RegionQuick(area, 0);
            Notify(came > 0 ? NotificationType.Success : NotificationType.Info, came > 0 ? "reallife_hier_note_region_filled" : "reallife_hier_note_region_nothing",
                new Dictionary<string, string> { { "region", AreaName(area) }, { "count", came.ToString() } }, 8f);
        }

        /// <summary>Every store of a region: the underpaid raised inside the band.</summary>
        public void HierarchyRaiseRegion(string area)
        {
            int raised = RegionQuick(area, 1);
            Notify(raised > 0 ? NotificationType.Success : NotificationType.Info, raised > 0 ? "reallife_hier_note_region_raised" : "reallife_hier_note_region_nothing",
                new Dictionary<string, string> { { "region", AreaName(area) }, { "count", raised.ToString() } }, 8f);
        }

        /// <summary>Staff moved from the region's stores with more than they keep to those with gaps, only where the region's policy allows moves.</summary>
        public void HierarchyReallocate(string area)
        {
            if (HierarchyOn && PolicyCatalogue.Value(PolicyField.Sources, RegionRule(area, PolicyField.Sources)) < PolicyCatalogue.SourceMove)
            {
                Notify(NotificationType.Warning, "reallife_hier_note_no_moves", new Dictionary<string, string> { { "region", AreaName(area) } }, 8f);
                return;
            }
            int moved = RegionQuick(area, 2);
            Notify(moved > 0 ? NotificationType.Success : NotificationType.Info, moved > 0 ? "reallife_hier_note_region_moved" : "reallife_hier_note_region_nothing",
                new Dictionary<string, string> { { "region", AreaName(area) }, { "count", moved.ToString() } }, 8f);
        }

        /// <summary>A manager appointed at every store of the region with none, as its regional manager would.</summary>
        public void HierarchyAppointRegion(string area)
        {
            ManagerFile r = HierarchyOn ? Working(RegionOf(area)) : null;
            int made = 0;
            try
            {
                made = HierarchyOn ? RegionAppoint(area, r != null ? r.Name : CompanyText.T("reallife_hier_you"), Save.Day, Save.Hour) : 0;
            }
            catch (Exception e)
            {
                HierarchyFault("appointing for a region", e);
                return;
            }
            ManagerRead();
            Notify(made > 0 ? NotificationType.Success : NotificationType.Info, made > 0 ? "reallife_hier_note_region_appointed" : "reallife_hier_note_region_nothing",
                new Dictionary<string, string> { { "region", AreaName(area) }, { "count", made.ToString() } }, 8f);
            Changed();
        }

        // One quick action over a region's stores: 0 fill gaps, 1 batch raises, 2 moves only. Returns what it did.
        private int RegionQuick(string area, int what)
        {
            if (!HierarchyOn || !HasArea(area))
            {
                return 0;
            }
            ManagerFile r = Working(RegionOf(area));
            string who = r != null ? r.Name : CompanyText.T("reallife_hier_you");
            int done = 0;
            try
            {
                for (int i = 0; i < _managed.Count; i++)
                {
                    ManagedPlace mp = _managed[i];
                    if (!string.Equals(mp.Area, area, StringComparison.Ordinal) || mp.Own == null || mp.Place.Reg == null || !GoingPublic.Mine(mp.Place.Reg))
                    {
                        continue;
                    }
                    if (what == 1)
                    {
                        done += BatchRaise(mp, (int)Rung.Regional, who, Save.Day, Save.Hour, false);
                        continue;
                    }
                    if (mp.Own.Keep <= mp.Place.Staff)
                    {
                        continue;
                    }
                    int before = mp.Place.Staff;
                    if (what == 0)
                    {
                        HireOwnBoard(mp, false);
                        if (mp.Place.Staff < mp.Own.Keep)
                        {
                            FillGap(mp, (int)Rung.Regional, Rule(mp, PolicyField.Sources), who, Save.Day, Save.Hour, false);
                        }
                    }
                    else
                    {
                        ScopeOf(mp, EscalationRules.LevelRegion);
                        for (int guard = 0; guard < 8 && mp.Place.Staff < mp.Own.Keep; guard++)
                        {
                            if (!MoveOne(mp, (int)Rung.Regional, who, Save.Day, Save.Hour))
                            {
                                break;
                            }
                        }
                    }
                    done += mp.Place.Staff - before;
                }
            }
            catch (Exception e)
            {
                HierarchyFault("a region's quick action", e);
                return done;
            }
            Changed();
            return done;
        }

        // --- What a region's page and its report read ---

        /// <summary>A region's figures for its dashboard and its report: places, those with a manager and with a gap, staff, standing, profit, score, open escalations.</summary>
        internal void RegionFigures(string area, out int places, out int managed, out int gaps, out int standing, out int profit, out int score, out int open)
        {
            places = managed = gaps = profit = open = 0;
            int standSum = 0, standCount = 0, scoreSum = 0;
            for (int i = 0; i < _managed.Count; i++)
            {
                ManagedPlace mp = _managed[i];
                if (!string.Equals(mp.Area, area, StringComparison.Ordinal))
                {
                    continue;
                }
                places++;
                managed += mp.Own != null ? 1 : 0;
                gaps += mp.Own != null && mp.Own.Keep > mp.Place.Staff ? 1 : 0;
                profit += mp.Shop != null && mp.Shop.HasBooks ? mp.Shop.Profit : 0;
                int count, stand = StandingAt(mp.Place.Place, out count);
                standSum += stand * count;
                standCount += count;
                scoreSum += mp.Own != null ? ScoreOf(mp) : 0;
            }
            for (int i = 0; _hier != null && i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                ManagedPlace at = e.Open && e.Place.Length > 0 ? ManagedAt(e.Place) : null;
                open += at != null && string.Equals(at.Area, area, StringComparison.Ordinal) ? 1 : 0;
            }
            standing = standCount > 0 ? standSum / standCount : 0;
            score = managed > 0 ? scoreSum / managed : 0;
        }

        // The week's reports beside the company's: one a region with a regional manager in post, one an executive, and each
        // executive's proposal for their area (CompanyDirector.Executives).
        partial void WeekReports(int day, HierarchyEntry report)
        {
            for (int i = 0; i < _districts.Count; i++)
            {
                string area = _districts[i];
                ManagerFile r = Working(RegionOf(area));
                if (r == null)
                {
                    continue;
                }
                int places, managed, gaps, standing, profit, score, open;
                RegionFigures(area, out places, out managed, out gaps, out standing, out profit, out score, out open);
                HierarchyEntry entry = _hier.Log(HierarchyLog.Report, (int)Rung.Regional, r.Name, area, day, Save.Hour);
                entry.A = open;
                entry.B = SettledSince(area, null, day - 7);
                entry.C = score;
            }
            ExecutivesWeek(day);
        }

        /// <summary>Escalations settled since a day in a region (null for any), or by one holder (null for anybody), from the log.</summary>
        internal int SettledSince(string area, string who, int from)
        {
            int n = 0;
            for (int i = 0; _hier != null && i < _hier.Entries.Count; i++)
            {
                HierarchyEntry e = _hier.Entries[i];
                if (e.Kind != (int)HierarchyLog.Settled || e.Day <= from || (who != null && !string.Equals(e.Who, who, StringComparison.Ordinal)))
                {
                    continue;
                }
                ManagedPlace at = area != null && e.Place.Length > 0 ? ManagedAt(e.Place) : null;
                n += area == null || (at != null && string.Equals(at.Area, area, StringComparison.Ordinal)) ? 1 : 0;
            }
            return n;
        }
    }
}
