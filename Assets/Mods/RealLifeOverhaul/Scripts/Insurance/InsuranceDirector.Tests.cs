using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Insurance
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for, with real money. Each says what happened in a notice and writes one "Insurance check" line.
    /// </summary>
    public sealed partial class InsuranceDirector
    {
        private const int TestRepair = 120;      // a repair at home, at the start's prices (the dearest of the life events' is 220)
        private const int TestDamages = 400;     // damages to another car, at the start's prices (they run from 150 to 3,000)
        private const string RepairKey = "reallife_transaction_repair", DamagesKey = "reallife_transaction_accident_liability";

        private bool TestReady()
        {
            if (!Running)
            {
                InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_test_off", null);
                return false;
            }
            if (_beforeTests == null)
            {
                _beforeTests = InsuranceStore.Write(_state);   // what "put insurance back as before" restores
            }
            return true;
        }

        private static void TestSays(string key, Dictionary<string, string> data)
        {
            InsuranceGame.Tell(NotificationType.Info, key, data);
        }

        /// <summary>Options, Tests: the app opens on its first tab.</summary>
        public void TestOpen()
        {
            bool opened = Running && OpenApp(RealLifeOverhaul.Ui.InsurePages.TabCars);
            if (!opened)
            {
                InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_test_off", null);
            }
            Logger?.Info("Insurance check (test, the app opened): " + (opened ? "PASS" : "not opened: insurance is off or the phone has no Insurance app"));
        }

        /// <summary>Options, Tests: a repair at home now, charged as a life event's is: the bill less what the home's policy pays.</summary>
        public void TestHomeClaim()
        {
            if (!TestReady())
            {
                return;
            }
            int bill = InsuranceRules.At(TestRepair, InsuranceGame.Level);
            int paid = HomeRepair(bill, true);
            bool taken = InsuranceGame.Pay(bill - paid, RepairKey);
            TestSays(taken ? "reallife_ins_test_home" : "reallife_ins_note_no_money", InsuranceGame.Data("bill", InsuranceGame.Money(bill), "payout", InsuranceGame.Money(paid),
                "amount", InsuranceGame.Money(bill - paid)));
            Logger?.Info("Insurance check (test, a repair of " + bill + " at home): the cover pays " + paid + ", the player " + (bill - paid) + (taken ? " (taken)" : " (not taken: no cash)") + ": "
                         + (paid >= 0 && paid <= bill ? "PASS" : "FAIL, the payout is not between nothing and the bill"));
        }

        /// <summary>Options, Tests: damages to another car now, for the car being driven or else the first insured car, charged less what its policy pays.</summary>
        public void TestCrashClaim()
        {
            if (!TestReady())
            {
                return;
            }
            int day = InsuranceGame.Day, bill = InsuranceRules.At(TestDamages, InsuranceGame.Level);
            string id = InsuranceBridges.DrivenCar != null ? InsuranceBridges.DrivenCar() : null;
            if (ReadCars() && (string.IsNullOrEmpty(id) || LookOf(id) < 0))
            {
                id = null;
                for (int i = 0; i < _carLooks.Count && id == null; i++)
                {
                    id = _carLooks[i].InsuredUntil >= day ? _carLooks[i].Id : null;
                }
            }
            InsuranceRules.NoteCrash(_state.CrashDays, day);
            int paid = (int)CarLoss(id, ClaimKind.Liability, bill, day);
            bool taken = InsuranceGame.Pay(bill - paid, DamagesKey);
            TestSays(taken ? "reallife_ins_test_crash" : "reallife_ins_note_no_money", InsuranceGame.Data("bill", InsuranceGame.Money(bill), "payout", InsuranceGame.Money(paid),
                "amount", InsuranceGame.Money(bill - paid)));
            _revision++;
            Logger?.Info("Insurance check (test, damages of " + bill + " to another car): " + (id == null ? "no insured car of the player's" : "the car's policy") + " pays " + paid + ", the player "
                         + (bill - paid) + (taken ? " (taken)" : " (not taken: no cash)") + ": " + (paid >= 0 && paid <= bill ? "PASS" : "FAIL, the payout is not between nothing and the bill"));
        }

        /// <summary>Options, Tests: the first home's premium is missed this morning, so its days of grace begin; it is asked for again at the next day's pass.</summary>
        public void TestMissed()
        {
            if (!TestReady())
            {
                return;
            }
            HomePolicy p = null;
            for (int i = 0; i < _state.Homes.Count && p == null; i++)
            {
                p = _state.Homes[i].Active ? _state.Homes[i] : null;
            }
            if (p == null)
            {
                TestSays("reallife_ins_test_no_home", null);
                Logger?.Info("Insurance check (test, a missed premium): no home has a policy: nothing done.");
                return;
            }
            int day = InsuranceGame.Day;
            p.GraceFrom = 0;
            p.NextDue = day;
            GraceStep step = InsuranceRules.Missed(ref p.GraceFrom, day);
            InsuranceGame.Tell(NotificationType.Warning, "reallife_ins_note_home_grace", InsuranceGame.Data("home", p.Name, "amount", InsuranceGame.Money(p.Premium),
                "day", InsuranceRules.GraceEnds(p.GraceFrom).ToString()), OpenHomes);
            _revision++;
            Logger?.Info("Insurance check (test, a missed premium at " + p.Name + "): " + step + ", the cover holds to day " + InsuranceRules.GraceEnds(p.GraceFrom) + ": "
                         + (step == GraceStep.Began ? "PASS" : "FAIL, the days of grace did not begin"));
        }

        /// <summary>Options, Tests: the policies are put back as they were before the first test. Money the tests took stays taken.</summary>
        public void TestBack()
        {
            if (!Running || _beforeTests == null)
            {
                TestSays("reallife_ins_test_back_none", null);
                return;
            }
            InsuranceState before = InsuranceStore.Read(_beforeTests, out _);
            if (before != null)
            {
                _state = before;
                _state.LastDay = 0;
            }
            _beforeTests = null;
            _revision++;
            TestSays("reallife_ins_test_back", null);
            Logger?.Info("Insurance check (test, put back as before): the line is as it was: PASS");
        }
    }
}
