using System;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The test tools of the property business (the options page). Each makes one thing that would otherwise take
    /// days happen at once, at the first building, hotel or car it fits, and opens the Property app on it. The state
    /// as it was before the first test is kept, once, and "Test: put property back" restores it.
    ///
    /// What a test cannot take back is said where it happens: the money a test moves is real (a night's takings and
    /// costs, a hire's rent, a fee), and so is an answer the player then gives. Putting property back restores the
    /// books, not the cash.
    /// </summary>
    public sealed partial class EstateDirector
    {
        private bool TestReady(string test, EstatePart part)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || _broken || !_running || !On(part))
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        // A test tool is about to change the state: the state as it is now is kept, once, to be put back.
        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = EstateStore.Write(_state);
            }
        }

        private void TestDone(string what, int tab, string pick)
        {
            Logger?.Warn("Estate test: " + what + ".");   // asked for by a button: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_estate_test_done", null, 7f, null, () => Open(tab, pick), true, false);
            Open(tab, pick);
        }

        // The first building the player lets himself that fits; with none, the first he owns and does not use is taken on for the test.
        private EstateHolding TestHolding(string test, Predicate<EstateHolding> fits)
        {
            int added, dropped;
            EstateGame.ReadHoldings(_state, out added, out dropped);
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                if (_state.Holdings[i].Managed && fits(_state.Holdings[i]))
                {
                    return _state.Holdings[i];
                }
            }
            for (int i = 0; i < _state.Holdings.Count; i++)
            {
                EstateHolding h = _state.Holdings[i];
                if (!h.Managed && !h.Self)
                {
                    h.Managed = true;
                    h.ToLet = true;
                    if (fits(h))
                    {
                        return h;
                    }
                    h.Managed = false;
                }
            }
            TestRefusals.TellKey(Logger, test, _state.Holdings.Count == 0 ? "reallife_estate_test_no_building" : "reallife_estate_test_no_fit");
            return null;
        }

        /// <summary>Test: a tenant applies.</summary>
        public void TestApplicant()
        {
            const string test = "reallife_estate_test_applicant";
            if (!TestReady(test, EstatePart.Property))
            {
                return;
            }
            BeforeTest();   // (the test may take a building on for the player: that is put back with the rest)
            EstateHolding h = TestHolding(test, x => !x.Let && !x.Busy && x.Applicants.Count < EstateCatalogue.ApplicantsMost && EstateRules.MarketRent(x, _climate) > 0);
            if (h == null)
            {
                return;
            }
            Applicant a = EstateRules.Apply(h, _state.Seed, Save.Day + _state.Holdings.Count + h.NextApplicant, _climate, true);
            if (a != null)
            {
                a.Lapse = Save.Day + EstateCatalogue.ApplicantDays;
            }
            Touched(h);
            TestDone("an applicant at " + h.Name + (a != null ? ": " + a.Offer + " a day, record " + a.Record + ", " + a.Term + " days" : ": none could be made"), TabTenants, "own:" + h.Place);
        }

        /// <summary>Test: a rent day with a tenant in arrears. With no tenant anywhere, one is put in first.</summary>
        public void TestArrears()
        {
            const string test = "reallife_estate_test_arrears";
            if (!TestReady(test, EstatePart.Property))
            {
                return;
            }
            BeforeTest();
            EstateHolding h = TestHolding(test, x => x.Let || (!x.Busy && EstateRules.MarketRent(x, _climate) > 0));
            if (h == null)
            {
                return;
            }
            int day = Save.Day;
            if (!h.Let)
            {
                h.Applicants.Clear();
                Applicant a = EstateRules.Apply(h, _state.Seed, day, _climate, true);
                if (a == null || !EstateRules.Accept(h, a.Id, day))
                {
                    TestRefusals.TellKey(Logger, test, "reallife_estate_test_no_fit");
                    return;
                }
            }
            h.Arrears = h.Rent * EstateCatalogue.NoticeArrearsDays;
            h.ArrearsDays = EstateCatalogue.NoticeArrearsDays;
            HoldingDay(h, day);
            TestDone("a rent day at " + h.Name + " with " + EstateCatalogue.NoticeArrearsDays + " days owed: arrears now " + h.Arrears + " over " + h.ArrearsDays + " day(s)" + (h.Let ? string.Empty : ", and the tenant has gone"),
                TabTenants, "own:" + h.Place);
        }

        /// <summary>Test: the works under way finish.</summary>
        public void TestWorks()
        {
            const string test = "reallife_estate_test_works";
            if (!TestReady(test, EstatePart.Property))
            {
                return;
            }
            EstateHolding busy = null;
            for (int i = 0; i < _state.Holdings.Count && busy == null; i++)
            {
                busy = _state.Holdings[i].Busy ? _state.Holdings[i] : null;
            }
            if (busy == null)
            {
                TestRefusals.TellKey(Logger, test, _state.Holdings.Count == 0 ? "reallife_estate_test_no_building" : "reallife_estate_test_no_works");
                return;
            }
            BeforeTest();
            busy.WorkEnd = Save.Day;
            bool granted;
            EstateWork done = EstateRules.Finish(busy, _state.Seed, Save.Day, _climate, out granted);
            WorkDone(busy, done, granted);
            Touched(busy);
            TestDone(done + " finished at " + busy.Name, TabWorks, "own:" + busy.Place);
        }

        /// <summary>Test: an offer for a building that is for sale.</summary>
        public void TestOffer()
        {
            const string test = "reallife_estate_test_offer";
            if (!TestReady(test, EstatePart.Property))
            {
                return;
            }
            EstateHolding listed = null;
            for (int i = 0; i < _state.Holdings.Count && listed == null; i++)
            {
                EstateHolding h = _state.Holdings[i];
                listed = h.List > 0 && h.Offer == 0 && h.SaleDay == 0 ? h : null;
            }
            if (listed == null)
            {
                TestRefusals.TellKey(Logger, test, _state.Holdings.Count == 0 ? "reallife_estate_test_no_building" : "reallife_estate_test_no_listing");
                return;
            }
            BeforeTest();
            int offer = EstateRules.SaleOffer(listed, _state.Seed, Save.Day, EstateRules.Value(listed, _state.Real, _climate.Index), _climate.Cycle, true);
            TestDone("an offer of " + offer + " for " + listed.Name + ", asked " + listed.List, TabMine, "own:" + listed.Place);
        }

        /// <summary>Test: an owner comes to the agency.</summary>
        public void TestInstruction()
        {
            const string test = "reallife_estate_test_instruction";
            if (!TestReady(test, EstatePart.Property))
            {
                return;
            }
            EstateGame.ReadAgency(_state.Agency);
            if (!_state.Agency.Running || _state.Agency.Open.Count >= EstateCatalogue.InstructionsMost)
            {
                TestRefusals.TellKey(Logger, test, _state.Agency.Running ? "reallife_estate_test_agency_full" : "reallife_estate_test_no_agency");
                return;
            }
            BeforeTest();
            Instruction made = null;
            for (int n = 0; n < 12 && made == null; n++)
            {
                made = AgencyRules.Arrive(_state.Agency, _state.Seed, Save.Day + n * 101 + _state.Agency.NextId, _climate, _state.Real, true);
            }
            if (made == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_estate_test_no_fit");
                return;
            }
            made.Day = Save.Day + EstateCatalogue.InstructionAnswerDays;
            TestDone("an instruction in " + made.District + ": " + (made.Sale ? "a sale" : "a letting") + " worth " + made.Value + ", " + made.Hours / 10 + " agent's hours", TabAgency, "ins:" + made.Id);
        }

        private HotelFile TestHotel(string test)
        {
            if (!TestReady(test, EstatePart.Hotel))
            {
                return null;
            }
            for (int i = 0; i < _state.Hotels.Count; i++)
            {
                if (EstateGame.ReadHotel(_state.Hotels[i], Save.Day))
                {
                    return _state.Hotels[i];
                }
            }
            TestRefusals.TellKey(Logger, test, "reallife_estate_test_no_hotel");
            return null;
        }

        /// <summary>Test: a night passes at the hotel. Its takings and costs are real money.</summary>
        public void TestHotelNight()
        {
            HotelFile f = TestHotel("reallife_estate_test_night");
            if (f == null)
            {
                return;
            }
            BeforeTest();
            Night(f, Save.Day, EstateGame.Climate(Save.Day));
            TestDone("a night at " + f.Name + ": " + (f.Sold[0] + f.Sold[1]) + " room(s) sold, takings " + f.Takings + ", costs " + f.Costs, TabHotel, "hot:" + f.Slot);
        }

        /// <summary>Test: a group asks for rooms at a discount.</summary>
        public void TestGroup()
        {
            const string test = "reallife_estate_test_group";
            HotelFile f = TestHotel(test);
            if (f == null)
            {
                return;
            }
            BeforeTest();
            HotelRules.RefuseGroup(f);
            f.GroupRooms = f.GroupNights = f.GroupRate = 0;
            HotelRules.AskGroup(f, _state.Seed, Save.Day);
            TestDone("a group asks at " + f.Name + ": " + f.AskRooms + " room(s) for " + f.AskNights + " night(s) at " + f.AskRate, TabHotel, "hot:" + f.Slot);
        }

        /// <summary>Test: a hire car goes out; pressed again, it comes back damaged. The hire's money is real.</summary>
        public void TestHire()
        {
            const string test = "reallife_estate_test_hire";
            if (!TestReady(test, EstatePart.Hire))
            {
                return;
            }
            HireFile hire = _state.Hire;
            HireCar outNow = null, idle = null;
            for (int i = 0; i < hire.Cars.Count; i++)
            {
                HireCar car = hire.Cars[i];
                outNow = outNow == null && car.State == (int)HireState.Out && car.HireDays > 0 ? car : outNow;
                idle = idle == null && (car.State == (int)HireState.Idle || car.State == (int)HireState.Valet) ? car : idle;
            }
            if (outNow != null)
            {
                BeforeTest();
                string name = EstateGame.CarName(outNow.Type), id = outNow.Id;
                outNow.Day = Save.Day;
                _damageNext = id;
                Due(outNow, Save.Day, true);
                _damageNext = null;
                HireCar after = hire.Car(id);
                TestDone(name + (after != null && after.State == (int)HireState.Waiting ? " is due back damaged and waits for its space to be clear" : " came back damaged"), TabHire, "car:" + id);
                return;
            }
            if (!hire.Running || idle == null)
            {
                TestRefusals.TellKey(Logger, test, hire.Running ? "reallife_estate_test_no_car" : "reallife_estate_test_no_lot");
                return;
            }
            BeforeTest();
            idle.State = (int)HireState.Idle;
            if (!HireRules.TryHire(idle, _state.Seed, Save.Day, _climate, hire.Rating, true) || !GoOut(idle, Save.Day, true))
            {
                TestRefusals.TellKey(Logger, test, "reallife_estate_test_car_stays");
                return;
            }
            TestDone(EstateGame.CarName(idle.Type) + " went out for " + idle.HireDays + " day(s) at " + idle.HireRate + " a day; press the test again to have it come back damaged", TabHire, "car:" + idle.Id);
        }

        /// <summary>Test: a month passes for the property market.</summary>
        public void TestMonth()
        {
            if (!TestReady("reallife_estate_test_month", EstatePart.Property))
            {
                return;
            }
            BeforeTest();
            int was = _state.Real;
            _state.Months++;
            _state.Real = EstateRules.Month(_state.Real, _state.Seed, _state.Months, _climate.Cycle, _climate.Quarters);
            _state.FirstDay -= MacroCatalogue.MonthDays(_climate.DaysPerYear);   // (so that the day's own count does not take the month back)
            int written = EstateGame.WriteValues(_state, _climate.Index);
            TestDone("a month of the market: " + was + " to " + _state.Real + ", heading for " + EstateRules.RealTarget(_climate.Cycle, _climate.Quarters) + "; " + written + " building value(s) written", TabMarket, null);
        }

        /// <summary>Test: put property back as it was before the first test. The books are restored; money that moved stays moved, and a car that is out is called back.</summary>
        public void TestBack()
        {
            const string test = "reallife_estate_test_back";
            if (Save == null || !ModState.Loaded || !_running || _broken)
            {
                TestRefusals.Tell(Logger, test, _running ? TestRefusal.NotLoaded : TestRefusal.FeatureOff);
                return;
            }
            if (_beforeTests == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_estate_test_nothing");
                return;
            }
            int version;
            EstateState before = EstateStore.Read(_beforeTests, out version);
            if (before == null)
            {
                _beforeTests = null;
                TestRefusals.TellKey(Logger, test, "reallife_estate_test_nothing");
                return;
            }
            RecallCars("the tests are put back");
            if (On(EstatePart.Property))
            {
                WriteRents(true);
            }
            // A car that still waits for its space keeps waiting in the books that come back.
            for (int i = 0; i < _state.Hire.Cars.Count; i++)
            {
                HireCar now = _state.Hire.Cars[i], then = before.Hire.Car(now.Id);
                if (then != null)
                {
                    then.State = now.State == (int)HireState.Waiting ? now.State : (int)HireState.Idle;
                    then.HireDays = then.HireRate = then.Deposit = then.HireSeed = 0;
                    then.X = now.X;
                    then.Y = now.Y;
                    then.Z = now.Z;
                    then.Yaw = now.Yaw;
                }
            }
            before.LastDay = Save.Day;
            _state = before;
            _beforeTests = null;
            _climate = EstateGame.Climate(Save.Day);
            if (On(EstatePart.Property))
            {
                ApplyProperty(Save.Day);
                EstateGame.ReadAgency(_state.Agency);
            }
            ReadHotels(Save.Day);
            if (On(EstatePart.Hire))
            {
                HireReconcile();
            }
            Logger?.Warn("Estate test: the books are as they were before the first test (" + _state.Holdings.Count + " building(s), " + _state.Hotels.Count + " hotel(s), " + _state.Hire.Cars.Count
                         + " car(s)); money that the tests moved stays moved.");
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_estate_test_back_done", null, 7f, null, null, true, false);
        }
    }
}
