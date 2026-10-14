using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The test tools of the dealership (the options page). Each puts one thing in place at once and opens the Dealership
    /// app on it. The state as it was before the first test is kept, once, and "put the dealership back" restores it,
    /// with the cars a test put on a lot taken off again.
    ///
    /// What a test cannot take back is said where it happens: a deal closed during a test pays the sale in for real, and
    /// a car sold leaves the lot. Putting the dealership back restores the stock, offers, plans and warranties, not cash.
    /// </summary>
    public sealed partial class DealerDirector
    {
        private const int TestCars = 3;
        private const int TestHours = 8;

        private bool TestReady(string test)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyDealer || _broken || !_running || _state == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = DealerStore.Write(_state);
            }
        }

        private void TestDone(string what, int tab, string pick)
        {
            Logger?.Warn("Dealership test: " + what + ".");   // asked for by a button: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_dealer_test_done", null, 7f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
            Open(tab, pick);
        }

        private void TestGuard(string test, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Fault("a test (" + test + ")", e);
            }
        }

        // The first dealership whose lot is laid out; a refusal that says so if there is none.
        private DealerYard TestYard(string test)
        {
            ReadYards();
            for (int i = 0; i < _state.Yards.Count; i++)
            {
                if (_state.Yards[i].Laid && RegOf(_state.Yards[i]) != null)
                {
                    return _state.Yards[i];
                }
            }
            TestRefusals.TellKey(Logger, test, _state.Yards.Count == 0 ? "reallife_dealer_test_no_dealership" : "reallife_dealer_test_no_lot");
            return null;
        }

        /// <summary>"Put three new cars on the lot": cars of today's wholesale list onto the first laid-out lot, at no cost.</summary>
        public void TestStock()
        {
            const string test = "Dealership stock";
            if (!TestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                DealerYard y = TestYard(test);
                if (y == null)
                {
                    return;
                }
                BeforeTest();
                int made = 0;
                for (int i = 0; i < _state.Market.Count && made < TestCars && y.FreeSpot() >= 0; i++)
                {
                    DealerListing l = _state.Market[i];
                    if (l.Kind == ListingKind.Wholesale && !l.Gone)
                    {
                        made += Arrive(y, l.Type, l.Colour, l.Km, l.Condition, 0, 0, l.Asking, CarSource.Wholesale) != null ? 1 : 0;
                    }
                }
                TestDone(made + " new car(s) put on the lot of '" + y.Name + "' at no cost; their cost is recorded at the wholesaler's price", TabStock, null);
            });
        }

        /// <summary>"Eight hours of customers": eight hours of the first dealership's customers worked through now, as if two salespeople were on shift.</summary>
        public void TestCustomers()
        {
            const string test = "Dealership customers";
            if (!TestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                DealerYard y = TestYard(test);
                if (y == null)
                {
                    return;
                }
                if (y.Cars.Count == 0)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_dealer_test_no_cars");
                    return;
                }
                BeforeTest();
                BuildingRegistration reg = RegOf(y);
                YardHour h = DealerGame.Hour(reg);
                h.Open = true;
                h.Staff = Math.Max(2, h.Staff);
                h.Skill = Math.Max(50, h.Skill);
                int offers = 0, visitors = 0, level = Prices.Level, now = Now;
                for (int i = 0; i < TestHours; i++)
                {
                    Worths(y, level);
                    DealerHour r = DealerRules.Hour(_state, y, h, Save.Day * 24 + 9 + i, Save.Day, _worths, Prices.At(DealerCatalogue.FinanceLeast), _models, _colours, level);
                    offers += r.Offers;
                    visitors += r.Visitors;
                }
                for (int d = 0; d < y.Deals.Count; d++)
                {
                    y.Deals[d].Made = now;
                    y.Deals[d].Expires = now + DealerCatalogue.DealHours;   // (the hours were drawn as the morning's: the offers stand from now)
                }
                TestDone(TestHours + " hours of customers at '" + y.Name + "': " + visitors + " visitor(s), " + offers + " offer(s) waiting for an answer", TabOffers, null);
            });
        }

        /// <summary>"A trade auction now": a sale opened at once, its lots knocked down over the next hours.</summary>
        public void TestAuction()
        {
            const string test = "Dealership auction";
            if (!TestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                BeforeTest();
                DealerRules.OpenSale(_state, Now, _models, _colours, Prices.Level);
                TestDone("a trade auction opened with " + DealerCatalogue.AuctionLots + " lot(s)", TabMarket, null);
            });
        }

        /// <summary>"A week of finance and warranties": every plan's instalment falls due and each warranty has a day, now.</summary>
        public void TestWeek()
        {
            const string test = "Dealership finance";
            if (!TestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                if (_state.Plans.Count == 0 && _state.Covers.Count == 0)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_dealer_test_no_plans");
                    return;
                }
                BeforeTest();
                for (int i = 0; i < _state.Plans.Count; i++)
                {
                    _state.Plans[i].Due = Math.Min(_state.Plans[i].Due, Save.Day);
                }
                for (int i = 0; i < _state.Covers.Count; i++)
                {
                    _state.Covers[i].RestUntil = -1;
                }
                OnDay();
                TestDone("every finance plan's week and every warranty's day worked through now", TabFinance, null);
            });
        }

        /// <summary>"Put the dealership back": the state as it was before the first test, the cars a test put on a lot taken off.</summary>
        public void TestBack()
        {
            const string test = "Dealership put back";
            if (!TestReady(test))
            {
                return;
            }
            if (_beforeTests == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_dealer_test_nothing_back");
                return;
            }
            TestGuard(test, () =>
            {
                DealerState before = DealerStore.Read(_beforeTests, out int version);
                if (before == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_dealer_test_nothing_back");
                    return;
                }
                var keep = new HashSet<string>(StringComparer.Ordinal);
                for (int y = 0; y < before.Yards.Count; y++)
                {
                    for (int c = 0; c < before.Yards[y].Cars.Count; c++)
                    {
                        keep.Add(before.Yards[y].Cars[c].VehicleId);
                    }
                }
                int removed = 0;
                for (int y = 0; y < _state.Yards.Count; y++)
                {
                    for (int c = 0; c < _state.Yards[y].Cars.Count; c++)
                    {
                        string id = _state.Yards[y].Cars[c].VehicleId;
                        if (!keep.Contains(id))
                        {
                            DealerGame.RemoveCar(id);
                            removed++;
                        }
                    }
                }
                _state = before;
                _beforeTests = null;
                Reconcile();
                RebuildIds();
                TestDone("the dealership put back as it was before the first test; " + removed + " car(s) a test put on a lot taken off", TabStock, null);
            });
        }
    }
}
