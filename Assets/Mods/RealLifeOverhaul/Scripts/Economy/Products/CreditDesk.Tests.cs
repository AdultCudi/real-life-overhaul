using System.Text;
using Helpers;
using UI.Notification;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Economy
{
    // Leaving a save (the save tools), and the test tools of the options page. A test works on accounts marked as
    // tests and on a car made for the test, never on the player's own loans, accounts or vehicles; "Test: put
    // everything back" removes all of it.
    public sealed partial class CreditDesk
    {
        private const string TestCarType = "ba:vehicletype_honzamimic";   // the small hatchback the driving school lends
        private const int TestCarShare = 60;                               // per cent of its price a test finances

        /// <summary>
        /// For the save tools ("prepare this save for removing the mod", "reset the mod's data"): the mod's accounts
        /// do not exist in the plain game, so they are settled now as far as the cash goes and the rest is forgiven
        /// and listed in the log. The game's loans are the game's own: each gets its own interest and payment back for
        /// good, and stays. An approval's money goes back to the lender. A test car is removed.
        /// </summary>
        public void Leave()
        {
            if (_file == null)
            {
                return;
            }
            var text = new StringBuilder(400);
            int settled = 0, forgiven = 0, loans = 0;
            if (_file.Relief.HasApproval)
            {
                CancelApproval(false);
            }
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                LoanMark m = _file.Loans[i];
                if (m.Held != LoanHold.None)
                {
                    loans++;
                    EndHold(m, LoanHold.Penalty, "the mod is leaving the save");
                    EndHold(m, LoanHold.Frozen, "the mod is leaving the save");
                }
                m.Ladder.Clear();
            }
            _file.Relief.Arranged = false;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (a.Closed)
                {
                    continue;
                }
                if (a.Test)
                {
                    RemoveTest(a);
                    continue;
                }
                string name = Name(a);
                int paid = a.Owed > 0 ? _wallet.TakeUpTo(a.Owed, Money.Payment, name) : 0;
                settled += paid;
                forgiven += a.Owed - paid;
                text.Append(text.Length > 0 ? "; " : string.Empty).Append(a.Kind).Append(" #").Append(a.Id).Append(": ").Append(a.Owed).Append(" owed, ").Append(paid).Append(" paid, ")
                    .Append(a.Owed - paid).Append(" forgiven").Append(a.Secured ? " ('" + a.SecurityName + "' stays the player's)" : string.Empty);
                a.Owed = 0;
                Closed(a);
            }
            SharkDebt shark = _file.Shark;
            if (shark.Active)
            {
                int paid = shark.Test ? 0 : _wallet.TakeUpTo(shark.Owed, Money.SharkPaid, string.Empty);
                settled += paid;
                forgiven += shark.Test ? 0 : shark.Owed - paid;
                text.Append(text.Length > 0 ? "; " : string.Empty).Append("the private lender: ").Append(shark.Owed).Append(" owed, ").Append(paid).Append(" paid, the rest forgiven");
                shark.Clear();
            }
            // Asked for by the save tools: written whether or not diagnostics are on.
            Logger?.Warn("Economy: leaving this save. " + loans + " of the game's loan(s) had a penalty rate or an arrangement on them and have their own interest and payment again; they stay, as the game's own. "
                         + "The mod's accounts do not exist without the mod: " + settled + " was paid from cash and " + forgiven + " is forgiven. "
                         + (text.Length > 0 ? text.ToString() : "There were no accounts") + ".");
        }

        private bool TestReady(string test)
        {
            if (_file == null || Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, _file == null && Save != null && ModState.Loaded ? TestRefusal.FeatureOff : TestRefusal.NotLoaded);
                return false;
            }
            return true;
        }

        private void TestSaid(string key, string a = null, string av = null, string b = null, string bv = null)
        {
            Notifications.Show(NotificationType.Info, key, a != null ? Data(a, av, b, bv) : null, 10f, null, _openAccounts, true, false);
        }

        private CreditAccount TestAccount(ProductKind kind)
        {
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                if (_file.Accounts[i].Test && !_file.Accounts[i].Closed && _file.Accounts[i].Kind == kind)
                {
                    return _file.Accounts[i];
                }
            }
            return null;
        }

        /// <summary>
        /// "Test: open every product": a test overdraft, a test card with cash drawn on it, and a test car on finance
        /// (a car made for the test, standing at the kerb near the player), each on the terms of a fair rating and
        /// whatever the real rating is. Nothing is paid out and nothing goes on the credit file.
        /// </summary>
        public void TestOpen()
        {
            if (!TestReady("Open every product"))
            {
                return;
            }
            int day = Today, score = Mathf.Max(CreditLedger.Value, CreditCatalogue.LendingThreshold), opened = 0;
            Lender bank = BankTerms.House;
            int ownRate = bank != null ? BankTerms.OwnRate(bank.Settings) : 8;
            for (int k = 0; k <= (int)ProductKind.CreditCard; k++)
            {
                var kind = (ProductKind)k;
                if (TestAccount(kind) != null)
                {
                    continue;
                }
                int limit = kind == ProductKind.Overdraft ? ProductTerms.OverdraftLimit(score, BankTerms.OwnCapTotal) : ProductTerms.CardLimit(score, BankTerms.OwnCapTotal);
                CreditAccount a = AccountRules.Open(kind, limit, ProductTerms.RateBp(score, ownRate, ProductTerms.Points(kind), Multiplier), day);
                a.Test = true;
                a.Owed = limit / 2;   // as if half had been drawn: there is something to pay, to miss and to charge interest on
                if (bank != null)
                {
                    a.Street = bank.Street;
                    a.Number = bank.Number;
                }
                Opened(a);
                opened++;
            }
            if (TestAccount(ProductKind.CarFinance) == null)
            {
                VehicleInstance car = TestCar();
                if (car != null)
                {
                    int price = Mathf.RoundToInt(car.VehicleType.price);
                    CreditAccount a = AccountRules.Finance(ProductKind.CarFinance, price * TestCarShare / 100, ProductCatalogue.CarTermLeast,
                        ProductTerms.RateBp(score, ownRate, ProductCatalogue.CarRatePoints, Multiplier), day, car.id, car.vehicleTypeName, price);
                    a.Test = true;
                    if (bank != null)
                    {
                        a.Street = bank.Street;
                        a.Number = bank.Number;
                    }
                    Opened(a);
                    opened++;
                }
            }
            _director.TermsChanged("a test");
            Logger?.Warn("Economy check (test: open every product): " + opened + " test account(s) opened; test overdraft " + (TestAccount(ProductKind.Overdraft) != null) + ", test card "
                         + (TestAccount(ProductKind.CreditCard) != null) + ", test car on finance " + (TestAccount(ProductKind.CarFinance) != null) + ": "
                         + (TestAccount(ProductKind.Overdraft) != null && TestAccount(ProductKind.CreditCard) != null ? "PASS" : "FAIL"));
            TestSaid(TestAccount(ProductKind.CarFinance) != null ? "reallife_econ_test_opened" : "reallife_econ_test_opened_no_car");
        }

        // A car for the tests: lent, not the player's to sell, at a free stretch of kerb near them. Null if there is none to be had here.
        private VehicleInstance TestCar()
        {
            PlayerController player = PlayerHelper.PlayerController;
            VehicleType type = VehicleTypeHelper.GetVehicleType(TestCarType);
            Vector3 spot;
            Quaternion heading;
            if (player == null || type == null || BuildingManager.IsInsideBuilding || EconomyBridges.KerbSpot == null || !EconomyBridges.KerbSpot(player.transform.position, out spot, out heading))
            {
                Logger?.Info("Economy: no test car (out of doors, beside a road with a free stretch of kerb, is where one can be made).");
                return null;
            }
            var instance = new DeliveryVehicleInstance(TestCarType) { fuel = type.maxFuel, zeroSellingPrice = true, preventWarehouseAssign = true };
            VehicleController made = VehicleHelper.CreateAndSpawnVehicle(instance, spot, heading);
            if (made == null)
            {
                Save.VehicleInstances.Remove(instance);
                return null;
            }
            return instance;
        }

        /// <summary>"Test: miss payments": every test account misses this many due payments, one after the other, with every step that brings.</summary>
        public void TestMiss(int times)
        {
            if (!TestReady("Miss payments"))
            {
                return;
            }
            int day = Today, level = Level, accounts = 0;
            var steps = new StringBuilder(120);
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                CreditAccount a = _file.Accounts[i];
                if (!a.Test || a.Closed)
                {
                    continue;
                }
                accounts++;
                for (int n = 0; n < times; n++)
                {
                    int missed = ProductCatalogue.Instalment(a.Kind) ? Mathf.Min(a.Instalment, a.Owed) : Mathf.Max(AccountRules.Minimum(a.Owed), ProductCatalogue.CardMinimumLeast);
                    LadderStep step = LadderRules.Missed(a.Ladder, level, a.Secured, a.Ladder.Overdue + missed);
                    int fee = step == LadderStep.LateFee ? AccountRules.LateFee(missed, level) : 0;
                    a.Owed += fee;
                    Step(new Debt(a), step, missed, fee, day, level);
                    steps.Append(' ').Append(a.Kind).Append(':').Append(a.Ladder.Misses).Append('=').Append(step);
                }
            }
            Touch();
            Logger?.Warn("Economy check (test: " + times + " missed payment(s) on " + accounts + " test account(s), level " + level + ", consequences " + (Settings.EconomyConsequences ? "on" : "off (the steps are taken all the same: it is a test)")
                         + "):" + steps + ": " + (accounts > 0 ? "PASS" : "FAIL"));
            TestSaid(accounts > 0 ? "reallife_econ_test_missed_accounts" : "reallife_econ_test_none", "count", times.ToString());
        }

        /// <summary>"Test: the collections call": a test account is put where the call comes, and the call is made.</summary>
        public void TestCollections()
        {
            if (!TestReady("Collections call"))
            {
                return;
            }
            CreditAccount a = TestAccount(ProductKind.CarFinance) ?? TestAccount(ProductKind.CreditCard) ?? TestAccount(ProductKind.Overdraft);
            if (a == null)
            {
                TestSaid("reallife_econ_test_none");
                return;
            }
            if (!a.Ladder.CallDue)
            {
                a.Ladder.Clear();
                int count = ProductCatalogue.Counts(Level)[(int)LadderStep.Collections - 1];
                int missed = ProductCatalogue.Instalment(a.Kind) ? Mathf.Min(a.Instalment, a.Owed) : AccountRules.Minimum(a.Owed);
                a.Ladder.Misses = count;
                a.Ladder.Overdue = Mathf.Min(a.Owed, missed * count);
                a.Ladder.Step = (int)LadderStep.Collections;
                a.Ladder.CallDue = true;
            }
            _file.Relief.CollectorKnown = true;
            DebtPhone.Setup(this);
            Logger?.Warn("Economy check (test: the collections call): about " + a.Kind + " #" + a.Id + ", " + a.Ladder.Overdue + " overdue after " + a.Ladder.Misses + " missed; the conversation is opened: PASS");
            DebtPhone.Open(DebtPhone.Collections);
        }

        /// <summary>"Test: repossess the test car": it is taken as soon as the player is away from it and not looking at it.</summary>
        public void TestRepossess()
        {
            if (!TestReady("Repossess the test car"))
            {
                return;
            }
            CreditAccount a = TestAccount(ProductKind.CarFinance);
            if (a == null || !a.Secured)
            {
                TestSaid("reallife_econ_test_no_car");
                return;
            }
            a.Ladder.RepoPending = true;
            _repoWaiting = true;
            Logger?.Warn("Economy check (test: repossess the test car): '" + a.SecurityName + "' (" + a.Security + ") is to be taken once the player is " + RepossessionKeepAway.ToString("0")
                         + " m away and not looking at it; \"Economy check (repossession ...)\" follows when it has been: PASS");
            TestSaid("reallife_econ_test_repo", "metres", RepossessionKeepAway.ToString("0"));
        }

        /// <summary>"Test: the private lender's text": it makes itself known again, as it does the first time a bank refuses.</summary>
        public void TestLenderText()
        {
            if (!TestReady("Private lender's text"))
            {
                return;
            }
            if (!Settings.EconomyShark)
            {
                TestRefusals.Tell(Logger, "Private lender's text", TestRefusal.FeatureOff);
                return;
            }
            _file.Shark.Known = false;
            Refused();
            Logger?.Warn("Economy check (test: the private lender's text): sent; its contact is " + (Settings.PhoneContacts ? "on the phone" : "not added (\"Phone contacts\" is off: a notification was shown instead)") + ": PASS");
        }

        /// <summary>
        /// "Test: work out a debt arrangement and a bankruptcy": both are worked out on the test accounts alone and on
        /// everything the save really owes, and written to the log with what is owed before and after. Nothing is changed.
        /// </summary>
        public void TestWaysOut()
        {
            if (!TestReady("Ways out of debt"))
            {
                return;
            }
            int testDebt = 0, day = Today;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                testDebt += _file.Accounts[i].Test && !_file.Accounts[i].Closed ? _file.Accounts[i].Owed : 0;
            }
            ArrangementPlan onTest = ReliefRules.Arrangement(testDebt, Income);
            BankruptcyPlan bankruptOnTest = ReliefRules.Bankruptcy(testDebt, 0, _wallet.Cash, day);
            ArrangementPlan real;
            BankruptcyPlan bankrupt;
            ProductAnswer arrange = ArrangementOffer(out real), ruin = BankruptcyOffer(out bankrupt);
            Logger?.Warn("Economy check (test: a debt arrangement): on the test accounts: " + onTest.Total + " frozen, " + onTest.PerDay + " a day (20% of an average income of " + Income.ToString("0")
                         + ", at least " + ProductCatalogue.ArrangementLeast + ") for " + onTest.Days + " days, rating held at " + ProductCatalogue.ArrangementScoreCap + ". On the whole save: " + arrange
                         + ", " + real.Total + " at " + real.PerDay + " a day for " + real.Days + " days. Nothing was changed: " + (onTest.PerDay * onTest.Days >= onTest.Total ? "PASS" : "FAIL"));
            Logger?.Warn("Economy check (test: bankruptcy): on the test accounts: before " + bankruptOnTest.WrittenOff + " owed and " + bankruptOnTest.CashNow + " in cash; after 0 owed and " + bankruptOnTest.CashLeft
                         + " in cash (" + bankruptOnTest.CashTaken + " to the creditors), nobody lends until day " + bankruptOnTest.NoCreditUntil + ". On the whole save: " + ruin + ", " + bankrupt.WrittenOff
                         + " written off, " + bankrupt.Kept + " kept" + (GameBankruptcy ? " (the game's own bankruptcy quest is on: not offered)" : string.Empty) + ". Nothing was changed: "
                         + (bankruptOnTest.CashLeft <= Mathf.Max(ProductCatalogue.BankruptKeepCash, 0) ? "PASS" : "FAIL"));
            TestSaid("reallife_econ_test_ways_done", "amount", FinanceText.Money(onTest.Total), "payment", FinanceText.Money(onTest.PerDay));
        }

        /// <summary>"Test: put everything back": every test account and the test car are removed, a test debt to the private lender too, and what is held on the game's loans is checked.</summary>
        public void TestBack()
        {
            if (!TestReady("Put everything back"))
            {
                return;
            }
            int removed = 0;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                if (_file.Accounts[i].Test && !_file.Accounts[i].Closed)
                {
                    RemoveTest(_file.Accounts[i]);
                    removed++;
                }
            }
            _file.Trim();
            if (_file.Shark.Test)
            {
                bool known = _file.Shark.Known;
                _file.Shark.Clear();
                _file.Shark.Known = known;
            }
            bool sound = true;
            int held = 0;
            for (int i = 0; i < _file.Loans.Count; i++)
            {
                held += _file.Loans[i].Held != LoanHold.None ? 1 : 0;
                sound &= LoanHolds.Sound(_file.Loans[i]);
            }
            int written = ApplyHolds("a test");
            _repoWaiting = false;
            for (int i = 0; i < _file.Accounts.Count; i++)
            {
                _repoWaiting |= !_file.Accounts[i].Closed && _file.Accounts[i].Ladder.RepoPending;
            }
            _director.TermsChanged("a test");
            Logger?.Warn("Economy check (test: everything back): " + removed + " test account(s) removed with the test car; " + held + " of the game's loan(s) have something held, each with its own figures remembered ("
                         + written + " written again); no test touched a loan: " + (sound ? "PASS" : "FAIL"));
            TestSaid("reallife_econ_test_back_done", "count", removed.ToString());
        }

        // A test account goes, and the car that was made for it.
        private void RemoveTest(CreditAccount a)
        {
            if (a.Kind == ProductKind.CarFinance && a.Security.Length > 0)
            {
                VehicleInstance car = Books.Vehicle(a.Security);
                if (car != null && car.zeroSellingPrice && Save.ActiveVehicleId != car.id)
                {
                    VehicleController controller;
                    OutOfSight(car, out controller);
                    car.Delete(controller);
                }
            }
            a.Owed = 0;
            a.Ladder.Clear();
            Closed(a);
        }
    }
}
