using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The executives (L3): a chief operating officer, a chief people officer and a chief financial officer, each one of the
    /// player's professionals (Tech: the talent catalogue's roles and pay, through CompanyBridges.Professionals), appointed
    /// to a seat. They hold the escalations of their area (EscalationRules.AreaOf), settle what the policy lets them, and
    /// once a week report to the owner and may propose a change to one company rule of their area, which only the owner
    /// approves. Read on the hierarchy's day and when the app opens; nothing per frame.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly List<ProfessionalHand> _pros = new List<ProfessionalHand>(16);
        private bool _prosKnown;   // the professions answered on the last read: with them off, the seats wait and nobody is stood down

        /// <summary>The player's professionals as last read, for the executives' pages; empty while the professions are off.</summary>
        internal IReadOnlyList<ProfessionalHand> Professionals => _pros;

        /// <summary>
        /// The executives tied to the professionals they are: their name, role, level, place and going rate read again. One
        /// who has gone, is leaving, or no longer fits the seat is stood down and said so. With the professions off nothing
        /// is read and every seat waits, kept as it is.
        /// </summary>
        internal void ExecutivesRead(bool tell)
        {
            if (_hier == null)
            {
                return;
            }
            _pros.Clear();
            _prosKnown = false;
            try
            {
                _prosKnown = CompanyBridges.Professionals != null && CompanyBridges.Professionals(_pros);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            for (int i = _hier.Executives.Count - 1; i >= 0; i--)
            {
                ExecutiveFile x = _hier.Executives[i];
                x.Seen = false;
                if (!_prosKnown)
                {
                    continue;
                }
                int at = HandOf(x.Id);
                ProfessionalHand h = at >= 0 ? _pros[at] : default(ProfessionalHand);
                if (at >= 0 && !h.Leaving && ExecutiveHiring.Fits((ExecSeat)x.Area, h.Role, h.Level))
                {
                    x.Seen = true;
                    x.Name = HierarchyStore.Clean(h.Name);
                    x.Role = h.Role;
                    x.Level = h.Level;
                    x.Place = HierarchyStore.Clean(h.Place);
                    x.Going = h.Going;
                    continue;
                }
                if (!tell || !HierarchyOn)
                {
                    continue;   // off: the seat is kept as it is and looked at again when it is on
                }
                _hier.Executives.RemoveAt(i);
                Logger?.Info("Company: " + x.Name + " is no longer one of your professionals fit for the " + (ExecSeat)x.Area + " seat, so it is empty.");
                Notify(NotificationType.Warning, "reallife_hier_note_exec_gone", new Dictionary<string, string> { { "name", x.Name }, { "seat", SeatName((ExecSeat)x.Area) } }, 8f);
            }
        }

        private int HandOf(string id)
        {
            for (int i = 0; i < _pros.Count; i++)
            {
                if (string.Equals(_pros[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A seat's name, as the ladders call it (LadderRanks).</summary>
        internal static string SeatName(ExecSeat seat)
        {
            return CompanyText.T(LadderRanks.TitleKey((int)Rung.Executive, seat));
        }

        /// <summary>A professional of the player's made an executive: one seat each, and never somebody who manages a place or a region.</summary>
        public void HierarchyAppointExecutive(int seat, string id)
        {
            if (!HierarchyOn || !ExecutiveHiring.Real(seat))
            {
                return;
            }
            ExecutivesRead(true);
            int at = HandOf(id);
            ProfessionalHand h = at >= 0 ? _pros[at] : default(ProfessionalHand);
            if (at < 0 || h.Leaving || !ExecutiveHiring.Fits((ExecSeat)seat, h.Role, h.Level) || (_mgr != null && _mgr.Of(id) != null))
            {
                Notify(NotificationType.Warning, "reallife_hier_note_exec_cannot", null, 8f);
                return;
            }
            ExecutiveFile other = _hier.ExecutiveOf(id);
            if (other != null && other.Area != seat)
            {
                _hier.Executives.Remove(other);   // one seat at a time: moved here, the other is empty
            }
            ExecutiveFile x = _hier.Executive((ExecSeat)seat);
            if (x == null)
            {
                x = new ExecutiveFile { Area = seat };
                _hier.Executives.Add(x);
            }
            x.Id = HierarchyStore.Clean(id);
            x.Name = HierarchyStore.Clean(h.Name);
            x.Role = h.Role;
            x.Level = h.Level;
            x.Place = HierarchyStore.Clean(h.Place);
            x.Going = h.Going;
            x.From = Save.Day;
            x.Decided = x.Paid = 0;
            x.ProposedDay = Save.Day;   // their first proposal comes after a week in the seat
            x.Seen = true;
            HierarchyEntry entry = _hier.Log(HierarchyLog.Appointed, (int)Rung.Owner, CompanyText.T("reallife_hier_you"), string.Empty, Save.Day, Save.Hour);
            entry.A = seat;
            entry.Detail = x.Name;
            Logger?.Info("Company: " + x.Name + " appointed to the " + (ExecSeat)seat + " seat, from day " + x.From + ", their going rate " + x.Going + " cents an hour.");
            Notify(NotificationType.Success, "reallife_hier_note_exec_appointed", new Dictionary<string, string> { { "name", x.Name }, { "seat", SeatName((ExecSeat)seat) } }, 8f);
            Changed();
        }

        /// <summary>A seat emptied: what its executive held passes to the owner on the next round.</summary>
        public void HierarchyStandDownExecutive(int seat)
        {
            ExecutiveFile x = HierarchyOn ? _hier.Executive((ExecSeat)seat) : null;
            if (x == null)
            {
                return;
            }
            _hier.Executives.Remove(x);
            int moved = 0;
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                if (e.Open && e.Level == EscalationRules.LevelExecutive && (int)EscalationRules.AreaOf((EscalationKind)e.Kind) == seat)
                {
                    e.Level = EscalationRules.LevelOwner;   // nobody else in the seat's area may settle it now
                    e.Moved = Save.Day;
                    moved++;
                }
            }
            Logger?.Info("Company: " + x.Name + " stood down from the " + (ExecSeat)seat + " seat; " + moved + " escalation(s) they held are yours now.");
            Notify(NotificationType.Info, "reallife_hier_note_exec_stood_down", new Dictionary<string, string> { { "name", x.Name }, { "seat", SeatName((ExecSeat)seat) } }, 8f);
            Changed();
        }

        // The executives' week: each in post reports, and proposes one change to a rule of their area if the week asks for one.
        private void ExecutivesWeek(int day)
        {
            WeekFigures week = WeekOf(day);
            for (int i = 0; i < _hier.Executives.Count; i++)
            {
                ExecutiveFile x = _hier.Executives[i];
                if (!x.Seen)
                {
                    continue;
                }
                HierarchyEntry report = _hier.Log(HierarchyLog.Report, (int)Rung.Executive, x.Name, string.Empty, day, Save.Hour);
                report.A = OpenOfSeat((ExecSeat)x.Area);
                report.B = SettledSince(null, x.Name, day - 7);
                report.C = x.Area;
                if (day - x.ProposedDay >= HierarchyRules.ProposalDays)
                {
                    Propose(x, week, day);
                }
            }
        }

        // One executive's proposal from a week's figures, to the owner; null when the week asks for none or one is waiting.
        private Escalation Propose(ExecutiveFile x, WeekFigures week, int day)
        {
            PolicyField field;
            int step;
            if (OpenProposalOf(x.Area) || !HierarchyRules.Propose((ExecSeat)x.Area, week, _hier.Company, out field, out step))
            {
                return null;
            }
            Escalation e = _hier.Add(EscalationKind.Proposal, string.Empty, EscalationRules.LevelOwner, day);
            e.A = (int)field;
            e.B = step;
            e.C = _hier.Company.Of(field);
            e.Subject = x.Area.ToString(System.Globalization.CultureInfo.InvariantCulture);
            e.Detail = x.Name;
            e.By = x.Name;
            x.ProposedDay = day;
            x.Decided++;
            _ownerNew++;
            HierarchyEntry entry = _hier.Log(HierarchyLog.Proposed, (int)Rung.Executive, x.Name, string.Empty, day, Save.Hour);
            entry.A = e.A;
            entry.B = e.B;
            entry.C = e.C;
            Logger?.Info("Company: " + x.Name + " (" + (ExecSeat)x.Area + ") proposes the company's " + PolicyCatalogue.Def(field).Key + " from step " + e.C + " to " + e.B
                         + " (the week: " + week.Gaps + " gap(s), " + week.Budgets + " budget(s), " + week.People + " about people, " + week.Kpis + " off target).");
            return e;
        }

        // The escalations raised in the week to a day, by area, from the log.
        private WeekFigures WeekOf(int day)
        {
            var week = new WeekFigures();
            for (int i = 0; i < _hier.Entries.Count; i++)
            {
                HierarchyEntry e = _hier.Entries[i];
                if (e.Kind != (int)HierarchyLog.Raised || day - e.Day >= 7)
                {
                    continue;
                }
                switch ((EscalationKind)e.B)
                {
                    case EscalationKind.Gap: week.Gaps++; break;
                    case EscalationKind.Budget: week.Budgets++; break;
                    case EscalationKind.Kpi: week.Kpis++; break;
                    case EscalationKind.Proposal: break;
                    default: week.People++; break;
                }
            }
            return week;
        }

        /// <summary>The open escalations of a seat's area, wherever they are held.</summary>
        internal int OpenOfSeat(ExecSeat seat)
        {
            int n = 0;
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                n += e.Open && e.Kind != (int)EscalationKind.Proposal && EscalationRules.AreaOf((EscalationKind)e.Kind) == seat ? 1 : 0;
            }
            return n;
        }

        private bool OpenProposalOf(int seat)
        {
            string s = seat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < _hier.Escalations.Count; i++)
            {
                Escalation e = _hier.Escalations[i];
                if (e.Open && e.Kind == (int)EscalationKind.Proposal && e.Subject == s)
                {
                    return true;
                }
            }
            return false;
        }

        // A proposal the owner approves: the company's rule set to it at once, the managers' files with it.
        partial void ProposalDecided(Escalation e, bool approve, int day, int hour, ref bool done)
        {
            if (!approve)
            {
                Close(e, EscalationState.Dismissed, CompanyText.T("reallife_hier_you"), day);
                return;
            }
            var field = (PolicyField)CompanyCatalogue.Clamp(e.A, 0, (int)PolicyField.Count - 1);
            PolicyDef d = PolicyCatalogue.Def(field);
            _hier.Company.Steps[(int)field] = CompanyCatalogue.Clamp(e.B, 0, d.Steps.Length - 1);
            PolicyMirror();
            HierarchyEntry entry = _hier.Log(HierarchyLog.Policy, (int)Rung.Owner, CompanyText.T("reallife_hier_you"), string.Empty, day, hour);
            entry.A = e.A;
            entry.B = _hier.Company.Of(field);
            Logger?.Info("Company: you approved " + e.Detail + "'s proposal: the company's " + d.Key + " is now step " + entry.B + ".");
            done = true;
        }

        partial void ProposalRejected(Escalation e)
        {
            var field = (PolicyField)CompanyCatalogue.Clamp(e.A, 0, (int)PolicyField.Count - 1);
            Logger?.Info("Company: you rejected " + e.Detail + "'s proposal for the company's " + PolicyCatalogue.Def(field).Key + ".");
        }

        // A new month of the managers': the executives' month turns over with it, each one's pay for it in the log.
        private void ExecutivesMonth()
        {
            if (_mgr == null || _hier.MonthFrom == _mgr.MonthFrom)
            {
                return;
            }
            for (int i = 0; i < _hier.Executives.Count; i++)
            {
                ExecutiveFile x = _hier.Executives[i];
                if (x.Paid > 0)
                {
                    HierarchyEntry entry = _hier.Log(HierarchyLog.Paid, (int)Rung.Executive, x.Name, string.Empty, Save.Day, Save.Hour);
                    entry.A = x.Paid;
                    entry.B = x.Decided;
                    entry.C = x.Area;
                }
                x.LastDecided = x.Decided;
                x.LastPaid = x.Paid;
                x.Decided = x.Paid = 0;
            }
            _hier.MonthFrom = _mgr.MonthFrom;
        }

        /// <summary>An executive's allowance a day at their going rate now (HierarchyRules.ExecutiveAllowance).</summary>
        internal static int AllowanceOf(ExecutiveFile x)
        {
            return x != null ? HierarchyRules.ExecutiveAllowance(x.Going) : 0;
        }

        // Yesterday's allowance of each executive in the seat by then, a business cost through the game's money, as the managers' are.
        private void ExecutivesPay(int day)
        {
            int paid = 0;
            for (int i = 0; i < _hier.Executives.Count; i++)
            {
                ExecutiveFile x = _hier.Executives[i];
                int amount = AllowanceOf(x);
                if (!x.Seen || x.From >= day || amount <= 0)
                {
                    continue;   // a seat that waits, or one taken today, is paid nothing for yesterday
                }
                Cost(amount, CompanyGame.ExecutiveKey, null, x.Name, CostKind.Wages, day - 1,
                    "the " + (ExecSeat)x.Area + " executive's allowance at a going rate of " + x.Going + " cents an hour, for day " + (day - 1), true);
                x.Paid += amount;
                paid += amount;
            }
            if (Verbose && paid > 0)
            {
                Logger?.Info("Company check (executives, day " + day + "): " + paid + " paid in allowances for day " + (day - 1) + " through the game's money: PASS");
            }
        }

        /// <summary>The executives' allowances over a month of the game's days, at their going rates now.</summary>
        internal int ExecutivesMonthCost()
        {
            int sum = 0;
            for (int i = 0; _hier != null && i < _hier.Executives.Count; i++)
            {
                sum += _hier.Executives[i].Seen ? ManagerDirectoryRules.MonthCost(AllowanceOf(_hier.Executives[i]), MonthDays) : 0;
            }
            return sum;
        }
    }
}
