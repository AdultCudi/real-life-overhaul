using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A raise for several employees picked in the game's My Employees app (its mass actions, <see cref="PickedRaiseGame"/>):
    /// each one goes the way of one raise in the Staff tab (StaffRules.GivePicked, as Answer gives one), so the standing, the
    /// rest before the next, the pay on the hours the game counts and the hierarchy's log are as for a raise given alone.
    /// Nothing of the game's employee is written. Runs on the player's click only, never per frame.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private readonly List<StaffFile> _pickedFiles = new List<StaffFile>(16);
        private readonly List<string> _pickedPlaces = new List<string>(4);
        private readonly List<int> _placeCount = new List<int>(4), _placeSum = new List<int>(4);   // by place: raised, and their raises an hour

        /// <summary>True while a raise for picked employees can be given: the staff part runs.</summary>
        public bool PickedRaisesOn => On(CompanyPart.Staff);

        /// <summary>What a choice would come to for the picked employees (the game's ids), read afresh from the game first.</summary>
        public PickedPlan PlanPicked(IList<string> ids, int choice)
        {
            if (!PickedRaisesOn || Save == null)
            {
                return new PickedPlan();
            }
            ReadPeople(out _);   // a wage the game changed since the morning is the one the raise is worked from
            Files(ids);
            return StaffRules.Plan(_pickedFiles, choice, Save.Day);
        }

        /// <summary>
        /// The raise given to each picked employee it may reach, one at a time by the rule of one raise. Returns how many were
        /// raised; <paramref name="sum"/> is what it adds an hour in all, hundredths.
        /// </summary>
        public int GivePicked(IList<string> ids, int choice, out int sum)
        {
            sum = 0;
            if (!PickedRaisesOn || Save == null)
            {
                return 0;
            }
            int day = Save.Day, count = 0;
            ReadPeople(out _);
            Files(ids);
            _pickedPlaces.Clear();
            _placeCount.Clear();
            _placeSum.Clear();
            for (int i = 0; i < _pickedFiles.Count; i++)
            {
                StaffFile f = _pickedFiles[i];
                int raise = f != null && StaffRules.CanPick(f, day) ? StaffRules.PickedRaise(f, choice) : 0;
                if (raise <= 0)
                {
                    continue;
                }
                int standing = f.Standing;
                StaffRules.GivePicked(f, raise, day);
                count++;
                sum += raise;
                int at = _pickedPlaces.IndexOf(f.Place);
                if (at < 0)
                {
                    at = _pickedPlaces.Count;
                    _pickedPlaces.Add(f.Place);
                    _placeCount.Add(0);
                    _placeSum.Add(0);
                }
                _placeCount[at]++;
                _placeSum[at] += raise;
                Logger?.Info("Company: " + f.Name + " given a raise of " + raise + " an hour (hundredths) with others picked in My Employees; agreed raise now " + f.Raise
                             + ", standing " + standing + " to " + f.Standing + "; the game's wage of " + f.Wage + " is not changed.");
            }
            if (count == 0)
            {
                return 0;
            }
            LogPicked(day);
            Recount();
            Changed();
            return count;
        }

        // The hierarchy's log has a line for each place, as for the owner's batch of raises from a dashboard.
        private void LogPicked(int day)
        {
            if (!HierarchyOn)
            {
                return;
            }
            for (int p = 0; p < _pickedPlaces.Count; p++)
            {
                HierarchyEntry entry = _hier.Log(HierarchyLog.Raises, LevelOfHolder(EscalationRules.LevelOwner), CompanyText.T("reallife_hier_you"), _pickedPlaces[p], day, Save.Hour);
                entry.A = _placeCount[p];
                entry.B = _placeSum[p];
            }
        }

        // The company's file for each picked id, in the same order; null for one it does not know.
        private void Files(IList<string> ids)
        {
            _pickedFiles.Clear();
            for (int i = 0; ids != null && i < ids.Count; i++)
            {
                _pickedFiles.Add(_state.Employee(ids[i]));
            }
        }

        /// <summary>The words of a choice, as the game's drop-down lists it.</summary>
        public static string PickedChoiceText(int choice)
        {
            int percent = StaffRules.PickedPercent(choice), amount = StaffRules.PickedAmount(choice);
            return choice == 0 ? CompanyText.T("reallife_co_mass_raise_going")
                : percent > 0 ? CompanyText.T("reallife_co_mass_raise_percent", "percent", percent.ToString())
                : CompanyText.T("reallife_co_mass_raise_amount", "amount", CompanyText.Wage(amount));
        }

        /// <summary>Test: a raise for the first two employees with nothing waiting, given through the same two calls as the game's list makes.</summary>
        public void TestPickedRaise()
        {
            const string test = "reallife_co_test_picked_raise";
            if (!TestReady(test, CompanyPart.Staff))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ReadPeople(out _);
                var ids = new List<string>(2);
                for (int i = 0; i < _state.Staff.Count && ids.Count < 2; i++)
                {
                    StaffFile f = _state.Staff[i];
                    if (f.AskDay == 0 && f.PoachDay == 0 && f.LeaveDay == 0)
                    {
                        ids.Add(f.Id);
                    }
                }
                if (ids.Count == 0)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_co_test_no_staff");
                    return;
                }
                BeforeTest();
                for (int i = 0; i < ids.Count; i++)
                {
                    _state.Employee(ids[i]).AskRest = 0;   // a raise given lately would leave them out of the test
                }
                const int choice = 2;   // the second share: five per cent
                PickedPlan plan = PlanPicked(ids, choice);
                long before = 0;
                for (int i = 0; i < ids.Count; i++)
                {
                    before += _state.Employee(ids[i]).Raise;
                }
                int count = GivePicked(ids, choice, out int sum);
                long after = 0;
                bool rested = true;
                for (int i = 0; i < ids.Count; i++)
                {
                    StaffFile f = _state.Employee(ids[i]);
                    after += f.Raise;
                    rested &= f.AskRest == Save.Day + CompanyCatalogue.RaiseRestDays;
                }
                bool ok = count == ids.Count && plan.Raised == count && plan.Raise == sum && after - before == sum && sum > 0 && rested && plan.After - plan.Before == sum;
                Logger?.Info("Company check (a raise for " + ids.Count + " picked employees, five per cent): planned " + plan.Raised + " for " + plan.Raise + ", given " + count + " for " + sum
                             + " an hour (hundredths), the agreed raises " + before + " to " + after + ", the rest begun for each " + rested + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_co_test_picked_raise_pass" : "reallife_co_test_picked_raise_fail", null, 10f);
                TestDone("a raise for " + ids.Count + " picked employees", TabStaff, "emp:" + ids[0]);
            });
        }
    }
}
