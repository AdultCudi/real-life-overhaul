using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The hierarchy's test tools on the options page: one for each escalation's trigger, each putting the trigger's cause in
    /// place at the first place with a manager (one is appointed if there is none), running the day's own look at it and
    /// writing one check line, PASS or FAIL, with what was measured; and one for each level. The cause is taken away again
    /// where the test made it up (a figure, a target), so tomorrow's look clears the escalation; what a holder settles is real.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private bool HierarchyTestReady(string test)
        {
            if (!ManagerTestReady(test))
            {
                return false;
            }
            if (!HierarchyOn)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        // The first place with a manager in post, a business first; one is appointed (its most skilled) where none has one.
        private ManagedPlace TestManaged(string test, bool shop)
        {
            ManagerRead();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < _managed.Count; i++)
                {
                    ManagedPlace mp = _managed[i];
                    if (Working(mp.Own) != null && (!shop || mp.Kind == ManagedKind.Shop) && mp.Place.Reg != null && GoingPublic.Mine(mp.Place.Reg))
                    {
                        return mp;
                    }
                }
                if (pass == 0)
                {
                    var hands = new List<ManagerHand>(8);
                    for (int i = 0; i < _managed.Count; i++)
                    {
                        ManagedPlace mp = _managed[i];
                        if (mp.Own != null || mp.Place.Staff <= 0 || (shop && mp.Kind != ManagedKind.Shop) || mp.Place.Reg == null || !GoingPublic.Mine(mp.Place.Reg))
                        {
                            continue;
                        }
                        ManagerGame.StaffAt(mp.Place, _index, hands);
                        int best = Best(hands, false);
                        Entities.EmployeeInstance e = best >= 0 ? ManagerGame.Employee(hands[best].Id) : null;
                        if (e != null && Appointed(mp, e, hands[best].Id) != null)
                        {
                            Logger?.Warn("Company test: " + hands[best].Name + " appointed manager of " + mp.Place.Name + " for the hierarchy's test.");
                            break;
                        }
                    }
                    ManagerRead();
                }
            }
            TestRefusals.TellKey(Logger, test, "reallife_co_test_no_staff");
            return null;
        }

        // One check line for a test, PASS or FAIL with what was measured, and its notice.
        private void HierarchyCheck(string test, bool ok, string measured)
        {
            Logger?.Warn("Company check (hierarchy test, " + test + "): " + measured + ": " + (ok ? "PASS" : "FAIL"));
            Notify(ok ? NotificationType.Success : NotificationType.Warning, ok ? "reallife_hier_test_pass" : "reallife_hier_test_fail",
                new Dictionary<string, string> { { "test", test } }, 8f);
        }

        // The trigger's look run now for one place, then its holder's try, as the day's round does.
        private Escalation TestLook(ManagedPlace mp, EscalationKind kind, int day)
        {
            _ownerNew = 0;
            Tally(day);
            WatchPlace(mp, day);
            Escalation e = _hier.OpenOf(kind, mp.Place.Place) ?? LastOf(kind, mp.Place.Place);
            if (e != null && e.Open && e.Level < EscalationRules.LevelOwner)
            {
                TryOne(e, day, Save.Hour);
            }
            TellOwner();
            return e;
        }

        private Escalation LastOf(EscalationKind kind, string place)
        {
            for (int i = _hier.Escalations.Count - 1; i >= 0; i--)
            {
                Escalation e = _hier.Escalations[i];
                if (e.Kind == (int)kind && string.Equals(e.Place, place, StringComparison.Ordinal) && e.Day == Save.Day)
                {
                    return e;
                }
            }
            return null;
        }

        private static string Where(Escalation e)
        {
            return e == null ? "none raised" : "escalation " + e.Id + " " + (EscalationState)e.State + " at level " + e.Level + (e.By.Length > 0 ? " (" + e.By + ")" : string.Empty);
        }

        private void TestOpen(Escalation e, ManagedPlace mp)
        {
            Changed();
            Open(TabManagers, e != null ? "he:" + e.Id : "db:" + mp.Place.Place);
        }

        /// <summary>A gap in the staff that has lasted the policy's days: the staff kept one over what the place has, its gap begun that many days ago.</summary>
        public void TestHierarchyGap()
        {
            const string test = "gap";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace mp = TestManaged(test, false);
                if (mp == null)
                {
                    return;
                }
                int day = Save.Day, keep = mp.Own.Keep;
                PlaceWatch w = _hier.Watch(mp.Place.Place, true);
                mp.Own.Keep = mp.Place.Staff + 1;
                w.GapFrom = day - PolicyCatalogue.Value(PolicyField.GapDays, Rule(mp, PolicyField.GapDays));
                int kept = mp.Own.Keep;
                Escalation e = TestLook(mp, EscalationKind.Gap, day);
                mp.Own.Keep = keep;   // the place keeps what it kept: tomorrow's look clears what the test made up
                HierarchyCheck(test, e != null && e.Day == day, mp.Place.Name + " with " + mp.Place.Staff + " of " + kept + " kept, the gap from day " + w.GapFrom
                                                                  + " against " + PolicyCatalogue.Value(PolicyField.GapDays, Rule(mp, PolicyField.GapDays)) + " day(s): " + Where(e));
                TestOpen(e, mp);
            });
        }

        /// <summary>A manager's month all but spent: the policy's share of the budget, then their try (the finance chief's top-up, or you).</summary>
        public void TestHierarchyBudget()
        {
            const string test = "budget";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace mp = TestManaged(test, false);
                if (mp == null)
                {
                    return;
                }
                int spent = mp.Own.Spent, budget = BudgetOf(mp.Own);
                mp.Own.Spent = Math.Max(1, budget);
                Escalation e = TestLook(mp, EscalationKind.Budget, Save.Day);
                mp.Own.Spent = spent;   // what was really spent this month
                HierarchyCheck(test, budget <= 0 ? e == null : e != null, mp.Place.Name + ", a budget of " + budget + " spent in full against the policy's "
                                                                         + PolicyCatalogue.Value(PolicyField.BudgetShare, Rule(mp, PolicyField.BudgetShare)) + " thousandths: " + Where(e));
                TestOpen(e, mp);
            });
        }

        /// <summary>A request for a raise left unanswered past the policy's days at a business with a manager, the raises switched off there so nobody answers it.</summary>
        public void TestHierarchyDemand()
        {
            const string test = "demand";
            if (!HierarchyTestReady(test) || !On(CompanyPart.Staff))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace mp = TestManaged(test, true);
                StaffFile f = mp != null ? TestStaffAt(mp) : null;
                if (f == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_co_test_no_staff");
                    return;
                }
                BeforeTest();
                int day = Save.Day;
                f.AskDay = Math.Max(1, day - PolicyCatalogue.Value(PolicyField.UnmetDays, Rule(mp, PolicyField.UnmetDays)));
                f.AskTo = Math.Max(f.Going, f.Paid + 100);
                Escalation e = TestLook(mp, EscalationKind.Demand, day);
                HierarchyCheck(test, e != null, f.Name + " at " + mp.Place.Name + " asked on day " + f.AskDay + " and is not answered: " + Where(e));
                TestOpen(e, mp);
            });
        }

        /// <summary>The staff of a business standing under the policy's floor for its days.</summary>
        public void TestHierarchyMood()
        {
            const string test = "standing";
            if (!HierarchyTestReady(test) || !On(CompanyPart.Staff))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace mp = TestManaged(test, true);
                if (mp == null || TestStaffAt(mp) == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_co_test_no_staff");
                    return;
                }
                BeforeTest();
                int day = Save.Day, floor = PolicyCatalogue.Value(PolicyField.MoodFloor, Rule(mp, PolicyField.MoodFloor));
                for (int i = 0; i < _state.Staff.Count; i++)
                {
                    if (_state.Staff[i].Place == mp.Place.Place)
                    {
                        _state.Staff[i].Standing = Math.Max(0, Math.Max(floor, 20) - 10);
                    }
                }
                PlaceWatch w = _hier.Watch(mp.Place.Place, true);
                w.LowFrom = day - PolicyCatalogue.MoodDays + 1;
                Escalation e = TestLook(mp, EscalationKind.Mood, day);
                int count, standing = StandingAt(mp.Place.Place, out count);
                HierarchyCheck(test, floor <= PolicyCatalogue.MoodNone ? e == null : e != null, mp.Place.Name + ", " + count + " of the staff standing " + standing + " on average against a floor of "
                                                                                              + floor + ": " + Where(e) + " (Company back puts their standing back)");
                TestOpen(e, mp);
            });
        }

        /// <summary>A wave of leavers: as many leaving a place this week as the policy calls a wave.</summary>
        public void TestHierarchyWave()
        {
            const string test = "wave";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace mp = TestManaged(test, false);
                if (mp == null)
                {
                    return;
                }
                int day = Save.Day, wave = PolicyCatalogue.Value(PolicyField.WaveCount, Rule(mp, PolicyField.WaveCount));
                PlaceWatch w = _hier.Watch(mp.Place.Place, true);
                var before = new List<int>(w.Left);
                for (int i = 0; i < wave; i++)
                {
                    w.Left.Add(day);
                }
                Escalation e = TestLook(mp, EscalationKind.Wave, day);
                w.Left.Clear();
                w.Left.AddRange(before);   // nobody really left: tomorrow's look clears it
                HierarchyCheck(test, e != null, mp.Place.Name + ", " + wave + " counted as leaving this week against a wave of " + wave + ": " + Where(e));
                TestOpen(e, mp);
            });
        }

        /// <summary>A business off its profit target for the policy's days: the highest target put on it for the look, then taken off.</summary>
        public void TestHierarchyKpi()
        {
            const string test = "targets";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace mp = TestManaged(test, true);
                if (mp == null)
                {
                    return;
                }
                int day = Save.Day;
                PolicySet store = _hier.Want(false, mp.Place.Place);
                int was = store != null ? store.Of(PolicyField.ProfitTarget) : -1, mood = store != null ? store.Of(PolicyField.MoodTarget) : -1;
                if (store != null)
                {
                    store.Steps[(int)PolicyField.ProfitTarget] = PolicyCatalogue.Def(PolicyField.ProfitTarget).Steps.Length - 1;
                    store.Steps[(int)PolicyField.MoodTarget] = PolicyCatalogue.Def(PolicyField.MoodTarget).Steps.Length - 1;
                }
                PlaceWatch w = _hier.Watch(mp.Place.Place, true);
                w.KpiRun = PolicyCatalogue.Value(PolicyField.KpiDays, Rule(mp, PolicyField.KpiDays)) - 1;
                w.KpiDay = day - 1;
                Escalation e = TestLook(mp, EscalationKind.Kpi, day);
                if (store != null)
                {
                    store.Steps[(int)PolicyField.ProfitTarget] = was;   // the store's own targets as they were
                    store.Steps[(int)PolicyField.MoodTarget] = mood;
                }
                bool measurable = mp.Shop != null && mp.Shop.HasBooks && mp.Shop.Profit < CompanyCatalogue.AtPrices(2500, _index) * 3 / 4 || StandingAt(mp.Place.Place, out int _) < 70;
                HierarchyCheck(test, measurable ? e != null : e == null, mp.Place.Name + " held to the highest targets for the look, " + w.KpiRun + " day(s) off them: " + Where(e));
                TestOpen(e, mp);
            });
        }

        /// <summary>
        /// An exception: a request past the pay band at a business whose manager answers raises, with the owner named to
        /// approve if nobody is; the manager's raises round leaves it to its approver.
        /// </summary>
        public void TestHierarchyException()
        {
            const string test = "exception";
            if (!HierarchyTestReady(test) || !On(CompanyPart.Staff))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace mp = TestManaged(test, true);
                StaffFile f = mp != null ? TestStaffAt(mp) : null;
                if (f == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_co_test_no_staff");
                    return;
                }
                BeforeTest();
                int day = Save.Day;
                if (PolicyCatalogue.Value(PolicyField.Approver, _hier.Company.Of(PolicyField.Approver)) == PolicyCatalogue.ApproveNobody)
                {
                    _hier.Company.Steps[(int)PolicyField.Approver] = PolicyCatalogue.ApproveOwner;
                    Logger?.Warn("Company test: the company's policy now names you to approve exceptions (it named nobody); set it back in the policy centre.");
                }
                mp.Own.Tasks |= ManagerRules.Bit(ManagerTask.Raises);
                int ceiling = PolicyRules.RaiseCeiling(f.Paid, f.Going, Rule(mp, PolicyField.PayBand), Rule(mp, PolicyField.RaiseMost));
                f.AskDay = day;
                f.AskTo = ceiling + Math.Max(100, ceiling / 10);
                int skill, review;
                string who;
                if (!Decider(mp, out skill, out review, out who))
                {
                    TestRefusals.TellKey(Logger, test, "reallife_hier_test_nobody");
                    return;
                }
                _ownerNew = 0;
                DecideRaises(mp, skill, review, who, day, ManagerCatalogue.StaffHour);   // this place's raises, as at its hour
                Handle(day, ManagerCatalogue.StaffHour, true);
                TellOwner();
                Escalation e = null;
                for (int i = _hier.Escalations.Count - 1; i >= 0 && e == null; i--)
                {
                    e = _hier.Escalations[i].Kind == (int)EscalationKind.Exception && _hier.Escalations[i].Subject == f.Id ? _hier.Escalations[i] : null;
                }
                HierarchyCheck(test, e != null && (f.AskDay > 0 || e.State == (int)EscalationState.Resolved), f.Name + " asked " + f.AskTo + " an hour (hundredths) against the manager's ceiling of "
                                                                                                           + ceiling + ": " + Where(e) + (f.AskDay > 0 ? ", the request still open" : ", agreed"));
                TestOpen(e, mp);
            });
        }

        /// <summary>L1: every manager's limits against the policy in force at their store, then the first one's dashboard and its gap filled within the policy.</summary>
        public void TestHierarchyStore()
        {
            const string test = "L1 store manager";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace first = TestManaged(test, false);
                if (first == null)
                {
                    return;
                }
                PolicyMirror();
                int checkedPlaces = 0, wrong = 0;
                for (int i = 0; i < _managed.Count; i++)
                {
                    ManagedPlace mp = _managed[i];
                    if (mp.Own == null)
                    {
                        continue;
                    }
                    checkedPlaces++;
                    int band = PolicyRules.Effective(_hier.Company, _hier.Region(AreaOf(mp)), _hier.Store(mp.Place.Place), PolicyField.PayBand);
                    int budget = PolicyRules.Effective(_hier.Company, _hier.Region(AreaOf(mp)), _hier.Store(mp.Place.Place), PolicyField.Budget);
                    wrong += mp.Own.WageBand != band || mp.Own.Budget != budget || Rule(mp, PolicyField.PayBand) != band ? 1 : 0;
                }
                HierarchyCheck(test, checkedPlaces > 0 && wrong == 0, checkedPlaces + " manager(s) deciding by the pay band and the budget in force at their store, " + wrong + " not");
                Open(TabManagers, "db:" + first.Place.Place);
            });
        }

        /// <summary>
        /// L2: the first region with a store a manager runs given a regional manager if it has none (the most skilled at a
        /// headquarters on none of the game's plans), who then appoints a manager at each of its stores with staff and none;
        /// the check line says whether every such store has one, and the region's dashboard opens.
        /// </summary>
        public void TestHierarchyRegion()
        {
            const string test = "L2 regional manager";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace first = TestManaged(test, false);
                if (first == null)
                {
                    return;
                }
                string area = first.Area;
                ManagerFile r = Working(RegionOf(area));
                if (r == null)
                {
                    var hands = new List<ManagerHand>(8);
                    ManagerGame.Offices(_mgrPlaces, _index, hands);
                    int best = Best(hands, true);
                    Entities.EmployeeInstance e = best >= 0 ? ManagerGame.Employee(hands[best].Id) : null;
                    RotaPlace office = best >= 0 ? PlaceOf(hands[best].Place) : null;
                    r = e != null && office != null ? AppointedRegion(area, e, hands[best].Id, office) : null;
                    if (r == null)
                    {
                        TestRefusals.TellKey(Logger, test, "reallife_hier_test_no_office");
                        return;
                    }
                    ManagerRead();
                }
                int made = RegionAppoint(area, r.Name, Save.Day, Save.Hour);
                ManagerRead();
                int stores = 0, without = 0;
                for (int i = 0; i < _managed.Count; i++)
                {
                    ManagedPlace mp = _managed[i];
                    if (mp.Area == area && mp.Place.Staff > 0)
                    {
                        stores++;
                        without += mp.Own == null ? 1 : 0;
                    }
                }
                int places, managed, gaps, standing, profit, score, open;
                RegionFigures(area, out places, out managed, out gaps, out standing, out profit, out score, out open);
                HierarchyCheck(test, Working(RegionOf(area)) != null && (without == 0 || _mgr.Managers.Count >= ManagerCatalogue.ManagersMost),
                    r.Name + " regional manager of " + AreaName(area) + ", " + made + " store manager(s) appointed, " + without + " of " + stores + " store(s) with staff left with none; "
                    + places + " place(s), " + gaps + " gap(s), score " + score);
                Changed();
                Open(TabManagers, "dr:" + area);
            });
        }

        /// <summary>
        /// L3: each empty seat given one of your professionals who fits it, one joining the professions' payroll at your
        /// first place where nobody fits; then each executive's proposal for a week with two of their area's matters, to you.
        /// </summary>
        public void TestHierarchyExecutives()
        {
            const string test = "L3 executives";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagedPlace first = TestManaged(test, false);
                if (first == null)
                {
                    return;
                }
                ExecutivesRead(false);
                if (!_prosKnown)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_hier_test_no_professions");
                    return;
                }
                int seated = 0, proposals = 0;
                foreach (Careers.ExecSeat seat in ExecutiveHiring.Seats)
                {
                    if (_hier.Executive(seat) == null)
                    {
                        string id = FittingProfessional(seat);
                        if (id == null && CompanyBridges.HireProfessional != null
                            && CompanyBridges.HireProfessional(first.Place.Place, ExecutiveHiring.SeatRoles[(int)seat][0], ExecutiveHiring.LeastLevel))
                        {
                            ExecutivesRead(false);
                            id = FittingProfessional(seat);
                        }
                        if (id != null)
                        {
                            HierarchyAppointExecutive((int)seat, id);
                        }
                    }
                    ExecutiveFile x = _hier.Executive(seat);
                    if (x == null || !x.Seen)
                    {
                        continue;
                    }
                    seated++;
                    var week = new WeekFigures { Gaps = 2, Budgets = 2, People = 2, Kpis = 2 };
                    _ownerNew = 0;
                    proposals += Propose(x, week, Save.Day) != null || OpenProposalOf(x.Area) ? 1 : 0;
                    TellOwner();
                }
                HierarchyCheck(test, seated == ExecutiveHiring.Seats.Length && proposals == seated,
                    seated + " of " + ExecutiveHiring.Seats.Length + " seat(s) filled from your professionals, " + proposals + " proposal(s) waiting for you");
                Changed();
                Open(TabManagers, "xs:");
            });
        }

        // The first of your professionals who fits a seat and holds none; null for none.
        private string FittingProfessional(Careers.ExecSeat seat)
        {
            for (int i = 0; i < _pros.Count; i++)
            {
                ProfessionalHand h = _pros[i];
                if (!h.Leaving && ExecutiveHiring.Fits(seat, h.Role, h.Level) && _hier.ExecutiveOf(h.Id) == null && _mgr.Of(h.Id) == null)
                {
                    return h.Id;
                }
            }
            return null;
        }

        /// <summary>L4: what waits for you, counted against the escalations kept, and the exceptions page opened.</summary>
        public void TestHierarchyOwner()
        {
            const string test = "L4 owner";
            if (!HierarchyTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int yours = 0, open = 0;
                for (int i = 0; i < _hier.Escalations.Count; i++)
                {
                    Escalation e = _hier.Escalations[i];
                    open += e.Open ? 1 : 0;
                    yours += e.Open && e.Level == EscalationRules.LevelOwner ? 1 : 0;
                }
                bool levels = true;
                for (int i = 0; i < _hier.Escalations.Count; i++)
                {
                    int l = _hier.Escalations[i].Level;
                    levels &= l >= EscalationRules.LevelRegion && l <= EscalationRules.LevelOwner;
                }
                HierarchyCheck(test, levels && yours == _hier.OpenAt(EscalationRules.LevelOwner), yours + " waiting for you of " + open + " open, every one held at L2 to L4");
                Open(TabManagers, "hx:");
            });
        }

        // One of a place's staff with a file and nothing waiting, not its manager.
        private StaffFile TestStaffAt(ManagedPlace mp)
        {
            int gone;
            ReadPeople(out gone);
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (f.Place == mp.Place.Place && f.AskDay == 0 && f.PoachDay == 0 && f.LeaveDay == 0 && (mp.Own == null || f.Id != mp.Own.Id))
                {
                    return f;
                }
            }
            return null;
        }
    }
}
