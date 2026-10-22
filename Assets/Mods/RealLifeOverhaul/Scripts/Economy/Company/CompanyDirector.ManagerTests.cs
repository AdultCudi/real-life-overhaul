using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The managers' test tools on the options page: a manager appointed at once, every kind decided now whatever the
    /// hour and the day, and every decision undone. What a manager decides by them is real, as it would be at its hour.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private bool ManagerTestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyCompany || !ManagersOn || _mgr == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        /// <summary>The most skilled employee of the first place with staff (a shop first) made its manager, with every kind it has.</summary>
        public void TestManagerAppoint()
        {
            const string test = "appoint a manager";
            if (!ManagerTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagerRead();
                ManagedPlace best = null;
                for (int pass = 0; pass < 2 && best == null; pass++)
                {
                    for (int i = 0; i < _managed.Count && best == null; i++)
                    {
                        best = _managed[i].Place.Staff > 0 && (pass == 1 || _managed[i].Kind == ManagedKind.Shop) ? _managed[i] : null;
                    }
                }
                var hands = new System.Collections.Generic.List<ManagerHand>(8);
                if (best != null)
                {
                    ManagerGame.StaffAt(best.Place, _index, hands);
                }
                if (best == null || hands.Count == 0)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_co_test_no_staff");
                    return;
                }
                ManagerHand pick = hands[0];
                for (int i = 1; i < hands.Count; i++)
                {
                    pick = hands[i].Skill > pick.Skill ? hands[i] : pick;
                }
                ManagerAppoint(best.Place.Place, pick.Id);
                ManagerFile m = OwnOf(best.Place.Place);
                if (m != null)
                {
                    m.Tasks = ManagerRules.Kinds(best.Kind);
                }
                Logger?.Warn("Company test: " + pick.Name + " (skill " + pick.Skill + ") made manager of " + best.Place.Name + " with every kind it has.");
                Open(TabManagers, "mp:" + best.Place.Place);
            });
        }

        /// <summary>
        /// Every manager decides every kind now, as if each kind's hour had come on a Sunday. A first look at the stock
        /// only takes it: orders are worked out from the use seen at the next look, at least half a day later.
        /// </summary>
        public void TestManagerDecide()
        {
            const string test = "the managers decide now";
            if (!ManagerTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ManagerRead();
                int before = _mgr.Decisions.Count > 0 ? _mgr.Decisions[_mgr.Decisions.Count - 1].Id : 0;
                for (int t = 0; t < (int)ManagerTask.Count; t++)
                {
                    Round(Save.Day, Save.Hour, (ManagerTask)t);
                }
                int made = 0;
                for (int i = 0; i < _mgr.Decisions.Count; i++)
                {
                    made += _mgr.Decisions[i].Id > before ? 1 : 0;
                }
                Logger?.Warn("Company test: the managers decided every kind now: " + made + " decision(s) over " + _managed.Count + " place(s).");
                Changed();
                Open(TabManagers, null);
            });
        }

        /// <summary>Every decision that can still be undone, undone, the newest first.</summary>
        public void TestManagerUndoAll()
        {
            const string test = "undo every decision";
            if (!ManagerTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int undone = 0, left = 0;
                for (int i = _mgr.Decisions.Count - 1; i >= 0; i--)
                {
                    Decision d = _mgr.Decisions[i];
                    string why;
                    if (!d.Open)
                    {
                        continue;
                    }
                    if (UndoState(d, Save.Day, out why) != UndoCan)
                    {
                        left++;
                        continue;
                    }
                    UndoOf(d, Save.Day);
                    d.State = DecisionState.Undone;
                    undone++;
                }
                Recount();
                Logger?.Warn("Company test: " + undone + " decision(s) undone, " + left + " that can no longer be undone left as they are.");
                Changed();
                Open(TabManagers, null);
            });
        }
    }
}
