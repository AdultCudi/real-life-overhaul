using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Suppliers
{
    /// <summary>
    /// Options, Tests: each situation the player would otherwise wait for, on real contracts at real prices. Each says what happened in a notice and writes one
    /// "Suppliers check" line.
    /// </summary>
    public sealed partial class SupplierDirector
    {
        private readonly Quote _testQuote = new Quote();

        private bool TestReady()
        {
            if (!Running)
            {
                SupplierGame.Tell(NotificationType.Warning, "reallife_supply_test_off", null);
                return false;
            }
            if (_beforeTests == null)
            {
                _beforeTests = SupplierStore.Write(_state);   // what "put suppliers back as before" restores
            }
            return true;
        }

        private Contract FirstRunning()
        {
            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                if (_state.Contracts[i].Running)
                {
                    return _state.Contracts[i];
                }
            }
            return null;
        }

        /// <summary>Options, Tests: how every supplier stands, in the log.</summary>
        public void TestShow()
        {
            if (!TestReady())
            {
                return;
            }
            bool sane = true;
            for (int i = 0; i < _state.Firms.Length; i++)
            {
                SupplierDef def = SupplierCatalogue.Firms[i];
                FirmState f = _state.Firms[i];
                bool ok = f.Health >= 0 && f.Health <= SupplierCatalogue.HealthMost && f.Size >= 1 && f.Size <= SupplierCatalogue.SizeMost && (f.Open || f.ReopenDay > 0);
                sane &= ok;
                Logger?.Info("Suppliers check (test, supplier " + def.Id + " " + SupplierText.Firm(def.Id) + "): health " + f.Health + ", size " + f.Size
                             + (f.Open ? ", trading" : ", shut until day " + f.ReopenDay) + (f.InTrouble(SupplierGame.Today) ? ", in trouble until day " + f.TroubleUntil : string.Empty)
                             + ", " + f.Customers + " shop(s) served, depot at " + (f.Depot.Length > 0 ? f.Depot : "none") + ": " + (ok ? "PASS" : "FAIL"));
            }
            SupplierGame.Tell(NotificationType.Info, "reallife_supply_test_show", SupplierGame.Data("n", _state.Firms.Length.ToString(), "contracts", _state.RunningCount().ToString()));
            Logger?.Info("Suppliers check (test, the suppliers): " + _state.Firms.Length + " suppliers, " + _state.RunningCount() + " contract(s) running, "
                         + SupplierGame.Money(_state.OwedAll()) + " owed: " + (sane ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the cheapest two week contract any supplier offers the first business that can have one.</summary>
        public void TestSign()
        {
            if (!TestReady())
            {
                return;
            }
            var places = new List<SupplyPlaceFact>(8);
            SupplierGame.Places(places, _scratch);
            for (int i = 0; i < places.Count; i++)
            {
                if (!SupplierEngine.Best(_state, _world, places[i].Key, SupplierCatalogue.TermWeeks[0], SupplierCatalogue.ShareDefault, 0, 0, _testQuote))
                {
                    continue;
                }
                Contract c = Sign(_testQuote.Supplier, places[i].Key, SupplierCatalogue.TermWeeks[0], SupplierCatalogue.ShareDefault, out Refusal why);
                Logger?.Info("Suppliers check (test, a contract for " + places[i].Name + "): " + (c != null ? "signed with supplier " + c.Supplier + ", " + c.Lines.Count
                             + " product(s), first delivery day " + c.Due + ": PASS" : "refused (" + why + "): FAIL"));
                return;
            }
            SupplierGame.Tell(NotificationType.Warning, "reallife_supply_test_no_offer", null);
            Logger?.Info("Suppliers check (test, a contract): no business of yours has an offer from any supplier (" + places.Count + " place(s) read): PASS");
        }

        /// <summary>Options, Tests: every contract's next delivery comes now, as it was judged or will be.</summary>
        public void TestDeliver()
        {
            if (!TestReady())
            {
                return;
            }
            int moved = 0, today = SupplierGame.Today;
            for (int i = 0; i < _state.Contracts.Count; i++)
            {
                Contract c = _state.Contracts[i];
                if (c.Running)
                {
                    c.Due = today - c.Slip;
                    c.JudgedFor = c.Out == Arrival.None ? 0 : c.Due;   // one not judged yet is judged for today by the same draw
                    moved++;
                }
            }
            int running = _state.RunningCount();
            _hour = -1;   // the next look brings them
            SupplierEngine.Hour(_state, new TestClock(_world));
            Flush();
            SupplierGame.Tell(NotificationType.Info, "reallife_supply_test_deliver", SupplierGame.Data("n", moved.ToString()));
            Logger?.Info("Suppliers check (test, deliveries now): " + moved + " contract(s) brought their next delivery, " + running + " were running and "
                         + _state.RunningCount() + " still are: PASS");
        }

        /// <summary>Options, Tests: the first contract's next delivery is made late or short by the supplier's own doing, and told.</summary>
        public void TestFail(bool late)
        {
            if (!TestReady())
            {
                return;
            }
            Contract c = FirstRunning();
            if (c == null)
            {
                SupplierGame.Tell(NotificationType.Warning, "reallife_supply_test_no_contract", null);
                return;
            }
            c.Out = late ? Arrival.Late : Arrival.Short;
            c.Why = Cause.Own;
            c.Slip = late ? 2 : 0;
            c.OutShare = late ? 1000 : 600;
            c.OutKind = -1;
            c.JudgedFor = c.Due;
            _state.Say(SupplyEventKind.Tomorrow, c, c.Supplier, late ? c.Slip : 1000 - c.OutShare, c.Due, (int)c.Why);
            Flush();
            Logger?.Info("Suppliers check (test, a " + (late ? "late" : "short") + " delivery): contract " + c.Id + " at " + c.PlaceName + ", due day " + c.Due
                         + (late ? ", " + c.Slip + " day(s) late" : ", " + c.OutShare + " thousandths brought") + ", the supplier's own doing so a credit follows: PASS");
        }

        /// <summary>Options, Tests: the supplier of the first contract (or the first supplier) goes under now.</summary>
        public void TestUnder()
        {
            if (!TestReady())
            {
                return;
            }
            Contract c = FirstRunning();
            int id = c != null ? c.Supplier : SupplierCatalogue.Firms[0].Id;
            FirmState f = _state.Firm(id);
            int held = _state.RunningCount();
            f.Health = 0;
            f.TroubleKind = 1;
            f.TroubleUntil = SupplierGame.Today + 1;   // its trouble takes the last of it today
            SupplierEngine.Day(_state, _world);
            Flush();
            SupplierGame.Tell(NotificationType.Info, "reallife_supply_test_under", SupplierGame.Data("supplier", SupplierText.Firm(id), "day", f.ReopenDay.ToString()));
            Logger?.Info("Suppliers check (test, a supplier goes under): supplier " + id + " is " + (f.Open ? "still open" : "shut until day " + f.ReopenDay) + ", "
                         + held + " contract(s) ran before and " + _state.RunningCount() + " after: " + (!f.Open ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the city's rival shops' supply day now, whatever the hour.</summary>
        public void TestRivals()
        {
            if (!TestReady())
            {
                return;
            }
            if (GoingPublic.IsGuest)
            {
                SupplierGame.Tell(NotificationType.Warning, "reallife_supply_test_guest", null);
                return;
            }
            RivalsFinish();
            SupplierGame.RivalShops(_rivals);
            System.Array.Clear(_customers, 0, _customers.Length);
            _rivalAt = 0;
            _rivalToday = SupplierGame.Today;
            _rivalCents = 0;
            _rivalMoved = _rivalFailed = 0;
            int shops = _rivals.Count;
            RivalsRun(int.MaxValue);
            SupplierGame.Tell(NotificationType.Info, "reallife_supply_test_rivals", SupplierGame.Data("n", shops.ToString(), "moved", _rivalMoved.ToString(), "failed", _rivalFailed.ToString()));
            Logger?.Info("Suppliers check (test, the rival shops' supply day): " + shops + " shop(s) read, " + _rivalMoved + " moved, " + _rivalFailed + " failed: PASS");
        }

        /// <summary>Options, Tests: suppliers and contracts as they were before the tests (money paid stays paid, goods delivered stay delivered).</summary>
        public void TestBack()
        {
            if (!Running || _beforeTests == null)
            {
                SupplierGame.Tell(NotificationType.Info, "reallife_supply_test_back_none", null);
                return;
            }
            SupplierState before = SupplierStore.Read(_beforeTests, out _);
            if (before != null)
            {
                RivalsStop();
                before.RivalDay = _state.RivalDay;   // the rival shops' day is not run again
                _state = before;
                _world = new SupplierGame(_state);
                SupplierEngine.Resume(_state, SupplierGame.Today);
            }
            _beforeTests = null;
            _revision++;
            SupplierGame.Tell(NotificationType.Info, "reallife_supply_test_back", null);
            Logger?.Info("Suppliers check (test, put back as before): the line is as it was: PASS");
        }

        // The world with the clock at the delivery hour: the test's deliveries come now, whatever the hour is.
        private sealed class TestClock : ISupplierWorld
        {
            private readonly SupplierGame _w;

            public TestClock(SupplierGame w)
            {
                _w = w;
            }

            public int Day => _w.Day;
            public int Hour => SupplierCatalogue.DeliveryHour;
            public int Month => _w.Month;
            public void Play(int day) => _w.Play(day);
            public long Cash => _w.Cash;
            public int Level => _w.Level;
            public int Credit => _w.Credit;
            public int Cycle => _w.Cycle;
            public int Factor(int kind) => _w.Factor(kind);
            public int ShortageOn(int kind, int day) => _w.ShortageOn(kind, day);
            public void ShortAhead(int kind, out int from, out int days, out int size) => _w.ShortAhead(kind, out from, out days, out size);
            public Economy.CityDay Conditions(int day) => _w.Conditions(day);
            public long Market(string item) => _w.Market(item);
            public long Base(string item) => _w.Base(item);
            public long Export(string item) => _w.Export(item);
            public bool Backorder(string item) => _w.Backorder(item);
            public int Most(string item) => _w.Most(item);
            public int Box(string item) => _w.Box(item);
            public bool PlaceOpen(string place) => _w.PlaceOpen(place);
            public void Wants(string place, List<ContractLine> into) => _w.Wants(place, into);
            public int Deliver(string place, string item, int units, long mills) => _w.Deliver(place, item, units, mills);
            public bool Pay(long cents, string key, string place, int supplier) => _w.Pay(cents, key, place, supplier);
            public void Receive(long cents, string key, string place, int supplier) => _w.Receive(cents, key, place, supplier);
            public bool Buyer(string place) => _w.Buyer(place);
            public bool Spend(string place, long cents) => _w.Spend(place, cents);
            public int FarDays(int supplier, string place) => _w.FarDays(supplier, place);
            public void OwnerGain(int tycoon, long cents) => _w.OwnerGain(tycoon, cents);
            public void RivalMoved(int tycoon, long cents) => _w.RivalMoved(tycoon, cents);
        }
    }
}
