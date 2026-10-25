using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Careers;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>A place's staff as the escalations count them each day: one pass over the company's staff fills every place's.</summary>
    internal sealed class StaffTally
    {
        public int Count, Standing;     // staff with a file, and their standing summed
        public int Unmet, Oldest;       // requests and offers waiting past the policy's days, and the oldest day among them
        public int Leaving;             // working their notice
    }

    /// <summary>
    /// The escalations (EscalationRules): each day every place with a manager is looked at once, and only a real trigger
    /// raises one: a gap in the staff past the policy's days, a budget nearly spent, a request or an offer left unanswered,
    /// standing under the floor, a wave of leavers, figures off target. Each goes to the first holder in post who may
    /// settle it under the policy (the regional manager, the area's executive, the owner), who tries once a round and
    /// passes it up if they cannot. Exceptions to the pay band are raised and settled at the managers' staff hour, as the
    /// requests are answered. Nothing per frame: one pass over the places and one over the staff a day.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly Dictionary<string, StaffTally> _tallies = new Dictionary<string, StaffTally>(16, StringComparer.Ordinal);
        private readonly List<StaffTally> _tallyPool = new List<StaffTally>(16);
        private readonly List<EmployeeInstance> _movable = new List<EmployeeInstance>(8), _boards = new List<EmployeeInstance>(8);
        private readonly List<RotaPlace> _scope = new List<RotaPlace>(16);
        private int _ownerNew;   // escalations handed to the owner in this round, told in one notice

        // The game's day, after the managers' own: what binds them written into their files, every place looked at, the
        // escalations handled, the week's report.
        partial void HierarchyDay(int day)
        {
            if (!HierarchyOn)
            {
                return;
            }
            try
            {
                float started = UnityEngine.Time.realtimeSinceStartup;
                PolicyMirror();
                _ownerNew = 0;
                ExecutivesRead(true);
                ExecutivesMonth();
                Tally(day);
                int raised = _hier.NextId;
                for (int i = 0; i < _managed.Count; i++)
                {
                    WatchPlace(_managed[i], day);
                }
                Forgotten();
                RegionRound(day, Save.Hour);   // the regional managers appoint and replace before the escalations are handled
                int settled = Handle(day, Save.Hour, false);
                Week(day);
                TellOwner();
                if (Verbose)
                {
                    Logger?.Info("Company check (hierarchy, day " + day + "): " + _managed.Count + " place(s) looked at, " + (_hier.NextId - raised) + " new escalation(s) or entries, "
                                 + settled + " settled by a regional manager or an executive, " + _hier.OpenAt(EscalationRules.LevelOwner) + " waiting for you; "
                                 + ((UnityEngine.Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms: PASS");
                }
            }
            catch (Exception e)
            {
                HierarchyFault("its day", e);
            }
        }

        // The managers' hour: an exception raised as the requests were answered goes to its approver at once, who has the
        // request's few days to agree it.
        partial void HierarchyHour(int day, int hour)
        {
            if (!HierarchyOn || hour != ManagerCatalogue.StaffHour)
            {
                return;
            }
            try
            {
                _ownerNew = 0;
                Handle(day, hour, true);
                TellOwner();
            }
            catch (Exception e)
            {
                HierarchyFault("its hour", e);
            }
        }

        // Every place's staff counted in one pass: standing, what waits past the policy's days, who is leaving.
        private void Tally(int day)
        {
            foreach (KeyValuePair<string, StaffTally> pair in _tallies)
            {
                _tallyPool.Add(pair.Value);
            }
            _tallies.Clear();
            List<StaffFile> staff = _state.Staff;
            for (int i = 0; i < staff.Count; i++)
            {
                StaffFile f = staff[i];
                if (string.IsNullOrEmpty(f.Place))
                {
                    continue;
                }
                StaffTally t = TallyOf(f.Place, true);
                t.Count++;
                t.Standing += f.Standing;
                t.Leaving += f.LeaveDay > 0 ? 1 : 0;
                if ((f.AskDay == 0 && f.PoachDay == 0) || (_hier.OpenOf(EscalationKind.Exception, f.Place) != null && OpenExceptionFor(f.Id)))
                {
                    continue;   // nothing waits, or it waits for its approver: an exception of its own, not a demand left unmet
                }
                int step = Rule(ManagedAt(f.Place), PolicyField.UnmetDays);
                int asked = f.AskDay > 0 && EscalationRules.UnmetDue(f.AskDay, day, step) ? f.AskDay : 0;
                int offered = f.PoachDay > 0 && EscalationRules.UnmetDue(f.PoachDay, day, step) ? f.PoachDay : 0;
                t.Unmet += (asked > 0 ? 1 : 0) + (offered > 0 ? 1 : 0);
                int first = asked > 0 && offered > 0 ? Math.Min(asked, offered) : Math.Max(asked, offered);
                t.Oldest = first > 0 && (t.Oldest == 0 || first < t.Oldest) ? first : t.Oldest;
            }
        }

        private StaffTally TallyOf(string place, bool make)
        {
            StaffTally t;
            if (_tallies.TryGetValue(place, out t) || !make)
            {
                return t;
            }
            t = _tallyPool.Count > 0 ? _tallyPool[_tallyPool.Count - 1] : new StaffTally();
            if (_tallyPool.Count > 0)
            {
                _tallyPool.RemoveAt(_tallyPool.Count - 1);
            }
            t.Count = t.Standing = t.Unmet = t.Oldest = t.Leaving = 0;
            _tallies.Add(place, t);
            return t;
        }

        private bool OpenExceptionFor(string id)
        {
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                if (e.Open && e.Kind == (int)EscalationKind.Exception && string.Equals(e.Subject, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A place's staff's standing, an average, and how many have a file; 0 with none.</summary>
        internal int StandingAt(string place, out int count)
        {
            StaffTally t = place != null ? TallyOf(place, false) : null;
            count = t != null ? t.Count : 0;
            return count > 0 ? t.Standing / count : 0;
        }

        // One place looked at for the day: each trigger measured against the policy in force there, an escalation raised
        // where one is due and cleared where its cause has gone.
        private void WatchPlace(ManagedPlace mp, int day)
        {
            ManagerFile m = Working(mp.Own);
            string place = mp.Place.Place;
            if (m == null || mp.Place.Reg == null || !GoingPublic.Mine(mp.Place.Reg))
            {
                ClearAll(place, day, true);   // nobody at the place to escalate from: the place is the owner's own
                return;
            }
            PlaceWatch w = _hier.Watch(place, true);
            if (w == null)
            {
                return;
            }
            int staff = mp.Place.Staff;
            bool gap = m.Keep > 0 && staff < m.Keep;
            w.GapFrom = gap ? (w.GapFrom > 0 ? w.GapFrom : day) : 0;
            Hold(EscalationKind.Gap, mp, gap && EscalationRules.GapDue(w.GapFrom, day, Rule(mp, PolicyField.GapDays)), staff, m.Keep, w.GapFrom, day);

            int budget = BudgetOf(m);
            Hold(EscalationKind.Budget, mp, EscalationRules.BudgetDue(m.Spent, budget, Rule(mp, PolicyField.BudgetShare)), m.Spent, budget, 0, day);

            StaffTally t = TallyOf(place, false);
            bool strike = mp.Shop != null && mp.Shop.File.StrikeFrom > 0 && On(CompanyPart.Staff);
            int unmet = (t != null ? t.Unmet : 0) + (strike ? 1 : 0);
            Hold(EscalationKind.Demand, mp, unmet > 0, unmet, t != null ? t.Oldest : day, strike ? 1 : 0, day);

            int count, standing = StandingAt(place, out count);
            bool low = count > 0 && EscalationRules.Low(standing, Rule(mp, PolicyField.MoodFloor));
            w.LowFrom = low ? (w.LowFrom > 0 ? w.LowFrom : day) : 0;
            Hold(EscalationKind.Mood, mp, low && EscalationRules.MoodDue(w.LowFrom, day), standing, PolicyCatalogue.Value(PolicyField.MoodFloor, Rule(mp, PolicyField.MoodFloor)), count, day);

            if (w.Staff >= 0 && staff < w.Staff)
            {
                for (int k = staff; k < w.Staff; k++)
                {
                    if (w.Left.Count >= 8)
                    {
                        w.Left.RemoveAt(0);
                    }
                    w.Left.Add(day);
                }
            }
            for (int k = w.Left.Count - 1; k >= 0; k--)
            {
                if (day - w.Left[k] >= PolicyCatalogue.WaveWindow)
                {
                    w.Left.RemoveAt(k);
                }
            }
            w.Staff = staff;
            int gone = w.LeftSince(day), leaving = t != null ? t.Leaving : 0;
            Hold(EscalationKind.Wave, mp, EscalationRules.WaveDue(gone + leaving, Rule(mp, PolicyField.WaveCount)), gone + leaving, gone, leaving, day);

            if (w.KpiDay != day)
            {
                int target = PolicyCatalogue.Value(PolicyField.ProfitTarget, Rule(mp, PolicyField.ProfitTarget));
                int mood = PolicyCatalogue.Value(PolicyField.MoodTarget, Rule(mp, PolicyField.MoodTarget));
                bool none = target == PolicyCatalogue.ProfitNone;
                int atPrices = none ? 0 : CompanyCatalogue.AtPrices(target, _index);
                int tolerance = Rule(mp, PolicyField.Tolerance);
                bool books = mp.Shop != null && mp.Shop.HasBooks;
                bool onProfit = !books || EscalationRules.OnTarget(mp.Shop.Profit, atPrices, tolerance, none);
                bool onMood = count == 0 || EscalationRules.OnTarget(standing, mood, tolerance, mood <= PolicyCatalogue.MoodNone);
                w.KpiRun = onProfit && onMood ? 0 : Math.Min(999, w.KpiRun + 1);
                w.KpiDay = day;
                Hold(EscalationKind.Kpi, mp, EscalationRules.KpiDue(w.KpiRun, Rule(mp, PolicyField.KpiDays)), books ? mp.Shop.Profit : 0, atPrices, w.KpiRun, day);
            }
        }

        // A trigger's answer for the day: due, an escalation raised or its figures brought up to date; not due, one open cleared.
        private void Hold(EscalationKind kind, ManagedPlace mp, bool due, int a, int b, int c, int day)
        {
            Escalation open = _hier.OpenOf(kind, mp.Place.Place);
            if (!due)
            {
                if (open != null)
                {
                    Close(open, EscalationState.Cleared, string.Empty, day);
                }
                return;
            }
            if (open != null)
            {
                open.A = a;
                open.B = b;
                open.C = c;
                return;
            }
            Raise(kind, mp, a, b, c, null, mp.Place.Name, day);
        }

        // Every open escalation about a place cleared: it has no manager now, or is no longer the player's.
        private void ClearAll(string place, int day, bool keepOwner)
        {
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                if (e.Open && e.Kind != (int)EscalationKind.Proposal && string.Equals(e.Place, place, StringComparison.Ordinal) && !(keepOwner && e.Level == EscalationRules.LevelOwner))
                {
                    Close(e, EscalationState.Cleared, string.Empty, day);
                }
            }
        }

        // Watches of places no longer the player's let go; an escalation about one of them is cleared.
        private void Forgotten()
        {
            for (int i = _hier.Watches.Count - 1; i >= 0; i--)
            {
                if (ManagedAt(_hier.Watches[i].Place) == null)
                {
                    _hier.Watches.RemoveAt(i);
                }
            }
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                if (e.Open && e.Kind != (int)EscalationKind.Proposal && e.Place.Length > 0 && ManagedAt(e.Place) == null)
                {
                    Close(e, EscalationState.Cleared, string.Empty, Save.Day);
                }
            }
        }

        /// <summary>A new escalation, handed to the first holder in post who may settle it under the policy; null if one is open already.</summary>
        private Escalation Raise(EscalationKind kind, ManagedPlace mp, int a, int b, int c, string subject, string detail, int day)
        {
            int level = Holder(kind, mp, (int)Rung.StoreManager);
            Escalation e = _hier.Add(kind, mp.Place.Place, level, day);
            e.A = a;
            e.B = b;
            e.C = c;
            e.Subject = HierarchyStore.Clean(subject);
            e.Detail = HierarchyStore.Clean(detail);
            e.By = mp.Own != null ? HierarchyStore.Clean(mp.Own.Name) : string.Empty;
            HierarchyEntry entry = _hier.Log(HierarchyLog.Raised, (int)Rung.StoreManager, e.By, e.Place, day, Save.Hour);
            entry.A = e.Id;
            entry.B = e.Kind;
            entry.C = level;
            _ownerNew += level == EscalationRules.LevelOwner ? 1 : 0;
            Logger?.Info("Company: " + e.By + " (manager of " + mp.Place.Name + ") escalated " + kind + " (" + a + ", " + b + ", " + c + ") to " + HolderWords(level, kind, mp) + ".");
            return e;
        }

        // The first holder above a level who is in post and may settle this kind under the policy; the owner if nobody is.
        private int Holder(EscalationKind kind, ManagedPlace mp, int from)
        {
            int approver = Rule(mp, PolicyField.Approver), sources = Rule(mp, PolicyField.Sources);
            bool region = RegionAtWork(AreaOf(mp)) != null, executive = ExecutiveAtWork(EscalationRules.AreaOf(kind)) != null;
            int level = EscalationRules.Next(from, region, executive);
            while (level < EscalationRules.LevelOwner && !EscalationRules.Tries(kind, level, approver, sources))
            {
                level = EscalationRules.Next(level, region, executive);
            }
            return level;
        }

        /// <summary>The regional manager of a region who is at work today; null for none.</summary>
        internal ManagerFile RegionAtWork(string area)
        {
            ManagerFile r = Working(RegionOf(area));
            return r != null && !Sick(r) ? r : null;
        }

        /// <summary>The executive of a seat in post; null for none.</summary>
        internal ExecutiveFile ExecutiveAtWork(ExecSeat seat)
        {
            ExecutiveFile x = _hier != null ? _hier.Executive(seat) : null;
            return x != null && x.Seen ? x : null;
        }

        private string HolderWords(int level, EscalationKind kind, ManagedPlace mp)
        {
            if (level == EscalationRules.LevelRegion)
            {
                ManagerFile r = RegionAtWork(AreaOf(mp));
                return "the regional manager" + (r != null ? " (" + r.Name + ")" : string.Empty);
            }
            if (level == EscalationRules.LevelExecutive)
            {
                ExecutiveFile x = ExecutiveAtWork(EscalationRules.AreaOf(kind));
                return "the " + EscalationRules.AreaOf(kind) + " executive" + (x != null ? " (" + x.Name + ")" : string.Empty);
            }
            return "you";
        }

        /// <summary>
        /// Every open escalation held below the owner tried once by its holder; settled, or passed to the next holder who may
        /// settle it. With <paramref name="exceptionsOnly"/> only the exceptions to the pay band, at the staff hour. Returns
        /// how many were settled.
        /// </summary>
        private int Handle(int day, int hour, bool exceptionsOnly)
        {
            int settled = 0;
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                if (!e.Open || e.Level >= EscalationRules.LevelOwner || (exceptionsOnly && e.Kind != (int)EscalationKind.Exception))
                {
                    continue;
                }
                settled += TryOne(e, day, hour) ? 1 : 0;
            }
            return settled;
        }

        // One escalation tried by its holder: settled (or found lapsed), or passed up. True when settled.
        private bool TryOne(Escalation e, int day, int hour)
        {
            ManagedPlace mp = ManagedAt(e.Place);
            if (mp == null)
            {
                Close(e, EscalationState.Cleared, string.Empty, day);
                return false;
            }
            var kind = (EscalationKind)e.Kind;
            string who;
            bool can = HolderName(e, mp, out who) && EscalationRules.Tries(kind, e.Level, Rule(mp, PolicyField.Approver), Rule(mp, PolicyField.Sources));
            bool done = false;
            if (can)
            {
                done = Settle(e, mp, e.Level, who, day, hour);
            }
            if (!e.Open)
            {
                return done;   // settled, or what it asked about lapsed meanwhile
            }
            if (done)
            {
                Settled(e, mp, who, day, hour);
                return true;
            }
            PassUp(e, mp, who, day, hour);
            return false;
        }

        private bool HolderName(Escalation e, ManagedPlace mp, out string who)
        {
            who = string.Empty;
            if (e.Level == EscalationRules.LevelRegion)
            {
                ManagerFile r = RegionAtWork(AreaOf(mp));
                who = r != null ? r.Name : string.Empty;
                return r != null;
            }
            if (e.Level == EscalationRules.LevelExecutive)
            {
                ExecutiveFile x = ExecutiveAtWork(EscalationRules.AreaOf((EscalationKind)e.Kind));
                who = x != null ? x.Name : string.Empty;
                return x != null;
            }
            return false;
        }

        // A holder's own try at an escalation, by kind; true when it is settled. Each does only what the policy allows.
        private bool Settle(Escalation e, ManagedPlace mp, int level, string who, int day, int hour)
        {
            switch ((EscalationKind)e.Kind)
            {
                case EscalationKind.Gap:
                    return FillGap(mp, level, Rule(mp, PolicyField.Sources), who, day, hour, false) > 0 && GapFilled(mp);
                case EscalationKind.Exception:
                    return Approve(e, mp, level, who, day, hour, false);
                case EscalationKind.Demand:
                    return AnswerWaiting(mp, level, who, day, hour);
                case EscalationKind.Mood:
                case EscalationKind.Wave:
                    return BatchRaise(mp, level, who, day, hour, false) > 0;
                case EscalationKind.Budget:
                    return TopUp(mp, level, who, day, hour);
                default:
                    return false;
            }
        }

        private bool GapFilled(ManagedPlace mp)
        {
            return mp.Own == null || mp.Own.Keep <= 0 || mp.Place.Staff >= mp.Own.Keep;
        }

        private void Settled(Escalation e, ManagedPlace mp, string who, int day, int hour)
        {
            int level = e.Level;
            Close(e, EscalationState.Resolved, who, day);
            HierarchyEntry entry = _hier.Log(HierarchyLog.Settled, level, who, e.Place, day, hour);
            entry.A = e.Id;
            entry.B = e.Kind;
            ExecutiveFile x = level == EscalationRules.LevelExecutive ? ExecutiveAtWork(EscalationRules.AreaOf((EscalationKind)e.Kind)) : null;
            if (x != null)
            {
                x.Decided++;
            }
            Logger?.Info("Company: " + who + " settled the " + (EscalationKind)e.Kind + " escalation of " + mp.Place.Name + ".");
        }

        private void PassUp(Escalation e, ManagedPlace mp, string who, int day, int hour)
        {
            int next = Holder((EscalationKind)e.Kind, mp, e.Level);
            e.Level = next;
            e.Moved = day;
            e.By = HierarchyStore.Clean(who);
            HierarchyEntry entry = _hier.Log(HierarchyLog.Passed, LevelOfHolder(e.Level), who, e.Place, day, hour);
            entry.A = e.Id;
            entry.B = next;
            entry.C = e.Kind;
            _ownerNew += next == EscalationRules.LevelOwner ? 1 : 0;
            Logger?.Info("Company: the " + (EscalationKind)e.Kind + " escalation of " + mp.Place.Name + " passed up to " + HolderWords(next, (EscalationKind)e.Kind, mp)
                         + (who.Length > 0 ? " by " + who + ", who could not settle it under the policy" : string.Empty) + ".");
        }

        private static int LevelOfHolder(int level)
        {
            return CompanyCatalogue.Clamp(level, 0, (int)Rung.Owner);
        }

        private void Close(Escalation e, EscalationState state, string who, int day)
        {
            e.State = (int)state;
            e.Moved = day;
            if (!string.IsNullOrEmpty(who))
            {
                e.By = HierarchyStore.Clean(who);
            }
            _hier.Settled += state == EscalationState.Resolved ? 1 : 0;
        }

        // The escalations newly handed to the owner this round, in one notice that opens them.
        private void TellOwner()
        {
            if (_ownerNew > 0)
            {
                Tell(NotificationType.Warning, "reallife_hier_note_owner", new Dictionary<string, string> { { "count", _ownerNew.ToString() } }, TabManagers, "hx:");
                _ownerNew = 0;
            }
        }

        // The week's report: logged always, told by notice unless the policy reports only what needs the owner.
        private void Week(int day)
        {
            if (!HierarchyRules.WeekOver(_hier.WeekFrom, day))
            {
                return;
            }
            int open = 0, scores = 0, scored = 0;
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                open += _hier.Escalations[i].Open ? 1 : 0;
            }
            for (int i = 0; i < _managed.Count; i++)
            {
                if (_managed[i].Own != null)
                {
                    scores += ScoreOf(_managed[i]);
                    scored++;
                }
            }
            HierarchyEntry entry = _hier.Log(HierarchyLog.Report, (int)Rung.Owner, string.Empty, string.Empty, day, Save.Hour);
            entry.A = open;
            entry.B = _hier.Settled;
            entry.C = scored > 0 ? scores / scored : 0;
            WeekReports(day, entry);
            Logger?.Info("Company: the hierarchy's week from day " + _hier.WeekFrom + " to day " + day + ": " + _hier.Settled + " escalation(s) settled, " + open
                         + " open, the places' score " + entry.C + " on average.");
            if (PolicyCatalogue.Value(PolicyField.Reports, Rule(null, PolicyField.Reports)) != PolicyCatalogue.ReportExceptions && (open > 0 || _hier.Settled > 0))
            {
                Tell(NotificationType.Info, "reallife_hier_note_week", new Dictionary<string, string>
                {
                    { "settled", _hier.Settled.ToString() }, { "open", open.ToString() }, { "score", entry.C.ToString() }
                }, TabManagers, null);
            }
            _hier.WeekFrom = day;
            _hier.Settled = 0;
        }

        partial void WeekReports(int day, HierarchyEntry report);

        /// <summary>A place's score on its targets today, 0 to 100 (EscalationRules.Score).</summary>
        internal int ScoreOf(ManagedPlace mp)
        {
            int count, standing = StandingAt(mp.Place.Place, out count);
            int target = PolicyCatalogue.Value(PolicyField.ProfitTarget, Rule(mp, PolicyField.ProfitTarget));
            bool none = target == PolicyCatalogue.ProfitNone || mp.Shop == null || !mp.Shop.HasBooks;
            int mood = count > 0 ? PolicyCatalogue.Value(PolicyField.MoodTarget, Rule(mp, PolicyField.MoodTarget)) : PolicyCatalogue.MoodNone;
            return EscalationRules.Score(mp.Shop != null ? mp.Shop.Profit : 0, none ? 0 : CompanyCatalogue.AtPrices(target, _index), none, standing, mood, Rule(mp, PolicyField.ProfitWeight));
        }

        // --- What a holder does ---

        // The places a holder's scope covers: a regional manager's region, an executive's or the owner's whole company.
        private void ScopeOf(ManagedPlace mp, int level)
        {
            _scope.Clear();
            string area = AreaOf(mp);
            for (int i = 0; i < _managed.Count; i++)
            {
                ManagedPlace other = _managed[i];
                bool inScope = level != EscalationRules.LevelRegion || string.Equals(AreaOf(other), area, StringComparison.Ordinal);
                if (other != mp && other.Place.Reg != null && GoingPublic.Mine(other.Place.Reg) && inScope)
                {
                    _scope.Add(other.Place);
                }
            }
        }

        /// <summary>
        /// A gap at a place filled by a holder: first from the job boards of the other places in their scope, inside the
        /// place's own policy and budget; then, where the policy allows moves (or the owner asks), by moving somebody from a
        /// store with more staff than it keeps. Returns how many came.
        /// </summary>
        internal int FillGap(ManagedPlace mp, int level, int sourcesStep, string who, int day, int hour, bool owner)
        {
            ManagerFile m = mp.Own;
            if (m == null || m.Keep <= 0 || mp.Place.Reg == null)
            {
                return 0;
            }
            int sources = PolicyCatalogue.Value(PolicyField.Sources, sourcesStep), came = 0;
            ScopeOf(mp, owner ? EscalationRules.LevelOwner : level);
            if (owner)
            {
                came += HireOwnBoard(mp, true);   // the owner's own press: the place's board first, as its manager would
            }
            if (owner || sources >= PolicyCatalogue.SourceRegion)
            {
                came += HireFromBoards(mp, level, who, day, hour, owner);
            }
            if (owner || sources >= PolicyCatalogue.SourceMove)
            {
                for (int guard = 0; guard < 8 && mp.Place.Staff < m.Keep; guard++)
                {
                    if (!MoveOne(mp, level, who, day, hour))
                    {
                        break;
                    }
                    came++;
                }
            }
            return came;
        }

        /// <summary>
        /// A manager's next source when their own board has nobody inside the policy: where the policy allows it, the job
        /// boards of the other places in their region, inside this place's own rules and budget.
        /// </summary>
        private void HireFromRegion(ManagedPlace mp, string who, int day, int hour)
        {
            if (!HierarchyOn || mp.Own == null || mp.Own.Keep <= mp.Place.Staff
                || PolicyCatalogue.Value(PolicyField.Sources, Rule(mp, PolicyField.Sources)) < PolicyCatalogue.SourceRegion)
            {
                return;
            }
            ScopeOf(mp, EscalationRules.LevelRegion);
            HireFromBoards(mp, (int)Rung.StoreManager, who, day, hour, false);
        }

        // Candidates the other places' boards found, hired for this place inside its policy while its gap lasts; the
        // owner's own press is not held to the manager's budget.
        private int HireFromBoards(ManagedPlace mp, int level, string who, int day, int hour, bool owner)
        {
            ManagerFile m = mp.Own;
            int hired = 0;
            for (int guard = 0; guard < 8 && mp.Place.Staff < m.Keep; guard++)
            {
                HierarchyGame.CandidatesFor(mp.Place.Reg, _scope, _boards);
                int count = _boards.Count;
                if (count == 0)
                {
                    break;
                }
                if (_hireWage.Length < count)
                {
                    _hireWage = new int[count * 2];
                    _hireGoing = new int[count * 2];
                    _hireSkill = new int[count * 2];
                }
                int income = mp.Shop != null ? mp.Shop.Income : UnityEngine.Mathf.RoundToInt(CityEconomy.DistrictIncome(mp.District));
                for (int i = 0; i < count; i++)
                {
                    _hireWage[i] = ManagerGame.WageNow(_boards[i], _index);
                    _hireGoing[i] = ManagerGame.Going(_boards[i], _index, _cycle, income);
                    _hireSkill[i] = ManagerGame.SkillOf(_boards[i]);
                }
                int left = owner ? int.MaxValue / 4 : ManagerRules.Left(BudgetOf(m), m.Spent);
                int pick = PolicyRules.Pick(_hireWage, _hireGoing, _hireSkill, count, Rule(mp, PolicyField.PayBand), Rule(mp, PolicyField.HirePayCap), Rule(mp, PolicyField.SkillFloor),
                    left, false);
                EmployeeInstance e = pick >= 0 ? _boards[pick] : null;
                string name = e != null ? ManagerGame.NameOf(e) : null;
                if (e == null || !HierarchyGame.HireFor(e, mp.Place.Reg))
                {
                    break;
                }
                int cost = ManagerRules.HireCost(_hireWage[pick]);
                m.Spent += cost;
                mp.Place.Staff++;
                hired++;
                HierarchyEntry entry = _hier.Log(HierarchyLog.Hired, LevelOfHolder(level), who, mp.Place.Place, day, hour);
                entry.A = _hireWage[pick];
                entry.B = _hireSkill[pick];
                entry.C = cost;
                entry.Detail = HierarchyStore.Clean(name);
                Logger?.Info("Company: " + who + " hired " + name + " (skill " + _hireSkill[pick] + ") for " + mp.Place.Name + " from another place's job board, " + cost
                             + " of its manager's budget.");
            }
            _boards.Clear();
            return hired;
        }

        // Somebody moved to this place from a store in scope with more staff than it keeps, the most skilled who fits.
        private bool MoveOne(ManagedPlace mp, int level, string who, int day, int hour)
        {
            ManagedPlace best = null;
            EmployeeInstance pick = null;
            int bestSkill = -1;
            for (int i = 0; i < _scope.Count; i++)
            {
                ManagedPlace from = ManagedAt(_scope[i].Place);
                if (from == null || from.Own == null || from.Own.Keep <= 0 || from.Place.Staff <= from.Own.Keep)
                {
                    continue;   // only a store with staff to spare over what its manager keeps gives anybody up
                }
                HierarchyGame.Movable(from.Place, mp.Place.Reg, from.Own.Id, _movable);
                for (int k = 0; k < _movable.Count; k++)
                {
                    int skill = ManagerGame.SkillOf(_movable[k]);
                    if (skill > bestSkill)
                    {
                        bestSkill = skill;
                        pick = _movable[k];
                        best = from;
                    }
                }
            }
            _movable.Clear();
            if (pick == null)
            {
                return false;
            }
            string name = ManagerGame.NameOf(pick);
            if (!HierarchyGame.Move(pick, best.Place.Reg, mp.Place.Reg))
            {
                return false;
            }
            best.Place.Staff--;
            mp.Place.Staff++;
            PlaceWatch was = _hier.Watch(best.Place.Place, false), now = _hier.Watch(mp.Place.Place, false);
            if (was != null && was.Staff > 0)
            {
                was.Staff--;   // a move is not somebody leaving: the wave's count is kept straight
            }
            if (now != null && now.Staff >= 0)
            {
                now.Staff++;
            }
            HierarchyEntry entry = _hier.Log(HierarchyLog.Moved, LevelOfHolder(level), who, mp.Place.Place, day, hour);
            entry.B = bestSkill;
            entry.Detail = HierarchyStore.Clean(name).Replace('/', ' ') + "/" + HierarchyStore.Clean(best.Place.Name).Replace('/', ' ');   // who, and from where
            Logger?.Info("Company: " + who + " moved " + name + " (skill " + bestSkill + ") from " + best.Place.Name + ", which keeps " + best.Own.Keep + ", to " + mp.Place.Name
                         + ", which keeps " + mp.Own.Keep + ".");
            return true;
        }

        /// <summary>
        /// An exception to the pay band agreed by its approver, if the pay asked for is inside the band and the policy's
        /// margin over it (the owner agrees whatever is asked). Found lapsed when the request or the offer has gone.
        /// </summary>
        private bool Approve(Escalation e, ManagedPlace mp, int level, string who, int day, int hour, bool owner)
        {
            StaffFile f = _state.Employee(e.Subject);
            bool offer = e.C == 1;
            if (f == null || (offer ? f.PoachDay == 0 : f.AskDay == 0) || !On(CompanyPart.Staff))
            {
                Close(e, EscalationState.Lapsed, string.Empty, day);
                return false;
            }
            int ceiling = PolicyRules.ExceptionCeiling(f.Going, Rule(mp, PolicyField.PayBand), Rule(mp, PolicyField.Approver), Rule(mp, PolicyField.ExceptionMargin));
            int asked = offer ? f.PoachOffer : f.AskTo;
            if (!owner && asked > ceiling)
            {
                return false;
            }
            int raise = f.Raise;
            if (offer)
            {
                StaffRules.OfferAnswer(f, true, day);
            }
            else
            {
                StaffRules.Answer(f, RaiseAnswer.Agree, day);
            }
            Recount();
            HierarchyEntry entry = _hier.Log(HierarchyLog.Approved, LevelOfHolder(level), who, mp.Place.Place, day, hour);
            entry.A = f.Raise - raise;
            entry.B = asked;
            entry.C = ceiling;
            entry.Detail = HierarchyStore.Clean(f.Name);
            Logger?.Info("Company: " + who + " agreed " + f.Name + "'s " + (offer ? "match of a rival's offer" : "raise") + " to " + asked + " an hour (hundredths), past the band"
                         + (owner ? "" : ", inside the approver's ceiling of " + ceiling) + ".");
            return true;
        }

        // What waits at a place answered by the people chief inside the band, as a manager would; a strike stays the owner's.
        private bool AnswerWaiting(ManagedPlace mp, int level, string who, int day, int hour)
        {
            if (!On(CompanyPart.Staff))
            {
                return false;
            }
            int band = Rule(mp, PolicyField.PayBand), most = Rule(mp, PolicyField.RaiseMost), answered = 0;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (f.Place != mp.Place.Place || OpenExceptionFor(f.Id))
                {
                    continue;
                }
                if (f.AskDay > 0)
                {
                    int ceiling = PolicyRules.RaiseCeiling(f.Paid, f.Going, band, most);
                    StaffRules.Answer(f, ManagerRules.RaiseAnswerFor(f.Paid, f.AskTo, ceiling, false), day);
                    answered++;
                }
                if (f.PoachDay > 0 && !f.GameOffer && !f.Held)
                {
                    StaffRules.OfferAnswer(f, ManagerRules.Match(f.PoachOffer, ManagerRules.Ceiling(f.Going, band), false), day);
                    answered++;
                }
            }
            if (answered > 0)
            {
                Recount();
                HierarchyEntry entry = _hier.Log(HierarchyLog.Settled, LevelOfHolder(level), who, mp.Place.Place, day, hour);
                entry.A = 0;
                entry.B = (int)EscalationKind.Demand;
                entry.C = answered;
            }
            return !(mp.Shop != null && mp.Shop.File.StrikeFrom > 0);
        }

        /// <summary>
        /// Everybody underpaid at a place given a raise nobody asked for, up to the going rate inside the band (the owner's
        /// own press: up to the going rate), as often as the policy allows. Returns how many.
        /// </summary>
        internal int BatchRaise(ManagedPlace mp, int level, string who, int day, int hour, bool owner)
        {
            if (!On(CompanyPart.Staff) || mp == null)
            {
                return 0;
            }
            int band = Rule(mp, PolicyField.PayBand), most = Rule(mp, PolicyField.RaiseMost), every = Rule(mp, PolicyField.RaiseEvery), count = 0, sum = 0;
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (f.Place != mp.Place.Place || !StaffRules.CanGive(f, day) || (!owner && !PolicyRules.RaiseDue(LastRaise(mp, f.Id), day, every)))
                {
                    continue;
                }
                int to = owner ? f.Going : Math.Min(f.Going, PolicyRules.RaiseCeiling(f.Paid, f.Going, band, most));
                if (to <= f.Paid)
                {
                    continue;
                }
                int before = f.Raise;
                f.AskDay = day;
                f.AskTo = to;
                StaffRules.Answer(f, RaiseAnswer.Agree, day);
                count++;
                sum += f.Raise - before;
            }
            if (count > 0)
            {
                Recount();
                HierarchyEntry entry = _hier.Log(HierarchyLog.Raises, LevelOfHolder(level), who, mp.Place.Place, day, hour);
                entry.A = count;
                entry.B = sum;
                Logger?.Info("Company: " + who + " gave " + count + " of the staff at " + mp.Place.Name + " a raise, " + sum + " an hour (hundredths) in all, up to the going rate"
                             + (owner ? "." : " inside the band."));
            }
            return count;
        }

        // The day a manager last agreed somebody more pay at a place: a raise or an offer matched; 0 for never in the log.
        private int LastRaise(ManagedPlace mp, string id)
        {
            for (int i = _mgr.Decisions.Count - 1; i >= 0; i--)
            {
                Decision d = _mgr.Decisions[i];
                if ((d.Task == (int)ManagerTask.Raises || d.Task == (int)ManagerTask.Offers) && d.A > 0 && d.State != DecisionState.Undone && d.Subject == id && d.Place == mp.Place.Place)
                {
                    return d.Day;
                }
            }
            return 0;
        }

        // A month's budget running out topped up by the finance chief, once a month, by the most the policy allows.
        private bool TopUp(ManagedPlace mp, int level, string who, int day, int hour)
        {
            PlaceWatch w = _hier.Watch(mp.Place.Place, true);
            int amount = CompanyCatalogue.AtPrices(PolicyCatalogue.Value(PolicyField.FinanceMost, Rule(mp, PolicyField.FinanceMost)), _index);
            if (w == null || mp.Own == null || amount <= 0 || w.TopUpOf(_mgr.MonthFrom) > 0)
            {
                return false;
            }
            w.TopUp = amount;
            w.TopUpFrom = _mgr.MonthFrom;
            HierarchyEntry entry = _hier.Log(HierarchyLog.TopUp, LevelOfHolder(level), who, mp.Place.Place, day, hour);
            entry.A = amount;
            Logger?.Info("Company: " + who + " added " + amount + " to the month's budget of the manager of " + mp.Place.Name + ".");
            return true;
        }

        /// <summary>What the finance chief or the owner added to a manager's budget this month, at prices.</summary>
        internal int TopUpOf(ManagerFile m)
        {
            PlaceWatch w = m != null && !m.Region && _hier != null && HierarchyOn ? _hier.Watch(m.Place, false) : null;
            return w != null ? w.TopUpOf(_mgr.MonthFrom) : 0;
        }

        // --- The managers' decisions that go to an approver ---

        /// <summary>
        /// A request for a raise, or a rival's offer, that the band and the policy keep the manager from agreeing: with
        /// somebody named to approve exceptions it is left open for them, an exception escalated at once, and the manager
        /// does not answer it. False where nobody is named, and the manager answers as before.
        /// </summary>
        private bool ToApprover(ManagedPlace mp, StaffFile f, bool offer, int ceiling, int day)
        {
            if (!HierarchyOn || PolicyCatalogue.Value(PolicyField.Approver, Rule(mp, PolicyField.Approver)) == PolicyCatalogue.ApproveNobody)
            {
                return false;
            }
            if (OpenExceptionFor(f.Id))
            {
                return true;   // still with its approver
            }
            Escalation e = Raise(EscalationKind.Exception, mp, offer ? f.PoachOffer : f.AskTo, ceiling, offer ? 1 : 0, f.Id, f.Name, day);
            return e != null;
        }

        // --- The owner's answers, from the exceptions page and the dashboards ---

        /// <summary>
        /// An escalation settled by the owner: 0 the kind's own answer (fill the gap, add a month's budget, agree the
        /// exception, raise the staff to the going rate, approve the proposal), 1 the other (refuse the exception, reject the
        /// proposal). A gap filled only in part, or nobody there to raise, stays open and says so.
        /// </summary>
        public void HierarchySettle(int id, int how)
        {
            Escalation e = HierarchyOn ? _hier.Find(id) : null;
            ManagedPlace mp = e != null && e.Place.Length > 0 ? ManagedAt(e.Place) : null;
            if (e == null || !e.Open || (mp == null && e.Kind != (int)EscalationKind.Proposal))
            {
                return;
            }
            string who = CompanyText.T("reallife_hier_you");
            int day = Save.Day, hour = Save.Hour;
            bool done = false;
            try
            {
                switch ((EscalationKind)e.Kind)
                {
                    case EscalationKind.Gap:
                        FillGap(mp, EscalationRules.LevelOwner, Rule(mp, PolicyField.Sources), who, day, hour, true);
                        done = GapFilled(mp);
                        if (!done)
                        {
                            Notify(NotificationType.Warning, "reallife_hier_note_gap_left", new Dictionary<string, string> { { "place", mp.Place.Name } }, 8f);
                        }
                        break;
                    case EscalationKind.Budget:
                    {
                        PlaceWatch w = _hier.Watch(mp.Place.Place, true);
                        if (w != null && mp.Own != null)
                        {
                            int amount = CompanyCatalogue.AtPrices(ManagerCatalogue.Step(ManagerCatalogue.Budgets, mp.Own.Budget), _index);
                            w.TopUp = w.TopUpOf(_mgr.MonthFrom) + amount;
                            w.TopUpFrom = _mgr.MonthFrom;
                            HierarchyEntry entry = _hier.Log(HierarchyLog.TopUp, (int)Rung.Owner, who, mp.Place.Place, day, hour);
                            entry.A = amount;
                            done = true;
                        }
                        break;
                    }
                    case EscalationKind.Exception:
                        if (how == 0)
                        {
                            done = Approve(e, mp, EscalationRules.LevelOwner, who, day, hour, true);
                        }
                        else
                        {
                            done = Refuse(e);
                        }
                        break;
                    case EscalationKind.Mood:
                    case EscalationKind.Wave:
                        done = BatchRaise(mp, EscalationRules.LevelOwner, who, day, hour, true) > 0;
                        if (!done)
                        {
                            Notify(NotificationType.Warning, "reallife_hier_note_none_underpaid", new Dictionary<string, string> { { "place", mp.Place.Name } }, 8f);
                        }
                        break;
                    case EscalationKind.Proposal:
                        done = Decide(e, how == 0, day, hour);
                        break;
                }
            }
            catch (Exception ex)
            {
                HierarchyFault("an answer of yours", ex);
                return;
            }
            if (done && e.Open)
            {
                Close(e, EscalationState.Resolved, who, day);
                HierarchyEntry entry = _hier.Log(HierarchyLog.Settled, (int)Rung.Owner, who, e.Place, day, hour);
                entry.A = e.Id;
                entry.B = e.Kind;
            }
            Changed();
        }

        // An exception refused by the owner: the request answered no, or the offer let go, as the Staff tab would.
        private bool Refuse(Escalation e)
        {
            StaffFile f = _state.Employee(e.Subject);
            if (f == null)
            {
                Close(e, EscalationState.Lapsed, string.Empty, Save.Day);
                return false;
            }
            if (e.C == 1)
            {
                AnswerOffer(f.Id, false);
            }
            else
            {
                AnswerRaise(f.Id, RaiseAnswer.Refuse);
            }
            return true;
        }

        partial void ProposalDecided(Escalation e, bool approve, int day, int hour, ref bool done);

        private bool Decide(Escalation e, bool approve, int day, int hour)
        {
            bool done = false;
            ProposalDecided(e, approve, day, hour, ref done);
            return done;
        }

        /// <summary>An escalation the owner lets be: it closes, and the trigger raises a new one only if it is still due tomorrow.</summary>
        public void HierarchyDismiss(int id)
        {
            Escalation e = HierarchyOn ? _hier.Find(id) : null;
            if (e == null || !e.Open)
            {
                return;
            }
            Close(e, EscalationState.Dismissed, CompanyText.T("reallife_hier_you"), Save.Day);
            if (e.Kind == (int)EscalationKind.Proposal)
            {
                ProposalRejected(e);
            }
            Changed();
        }

        partial void ProposalRejected(Escalation e);

        /// <summary>An escalation passed up a level from a dashboard, as its holder would when they cannot settle it.</summary>
        public void HierarchyPass(int id)
        {
            Escalation e = HierarchyOn ? _hier.Find(id) : null;
            ManagedPlace mp = e != null ? ManagedAt(e.Place) : null;
            if (e == null || !e.Open || mp == null || e.Level >= EscalationRules.LevelOwner)
            {
                return;
            }
            string who;
            HolderName(e, mp, out who);
            _ownerNew = 0;
            PassUp(e, mp, who.Length > 0 ? who : CompanyText.T("reallife_hier_you"), Save.Day, Save.Hour);
            TellOwner();
            Changed();
        }

        /// <summary>An escalation tried now by its holder, from a dashboard, as on their round.</summary>
        public void HierarchyTryNow(int id)
        {
            Escalation e = HierarchyOn ? _hier.Find(id) : null;
            if (e == null || !e.Open || e.Level >= EscalationRules.LevelOwner)
            {
                return;
            }
            _ownerNew = 0;
            TryOne(e, Save.Day, Save.Hour);
            TellOwner();
            Changed();
        }
    }
}
