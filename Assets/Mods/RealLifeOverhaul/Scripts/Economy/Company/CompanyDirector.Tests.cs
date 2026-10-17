using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The test tools on the options page: each puts one thing in place at the first business or employee it fits and
    /// opens the app on it. The state before the first test is kept for "put the company back". Only the audit test
    /// moves money by itself; the player's answers afterwards are real.
    /// </summary>
    public sealed partial class CompanyDirector
    {
        private const int TestLoss = 1000;      // the fire's repair bill of the claim test
        private const int TestKeptBack = 6000;  // the tax kept back the audit test finds when there is none

        private bool TestReady(string test, CompanyPart part)
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Settings.Economy || !Settings.EconomyCompany || _broken || !_running || !On(part))
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
                _beforeTests = CompanyStore.Write(_state);
            }
        }

        private void TestDone(string what, int tab, string pick)
        {
            Logger?.Warn("Company test: " + what + ".");   // a button's answer: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_co_test_done", null, 7f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
            Open(tab, pick);
        }

        private CompanyShop TestShop(string test, bool withStaff)
        {
            Recount();
            for (int i = 0; i < _shops.Count; i++)
            {
                if (!withStaff || _shops[i].Staff > 0)
                {
                    return _shops[i];
                }
            }
            TestRefusals.TellKey(Logger, test, withStaff ? "reallife_co_test_no_staff" : "reallife_co_test_no_business");
            return null;
        }

        private StaffFile TestEmployee(string test)
        {
            int gone;
            ReadPeople(out gone);
            for (int i = 0; i < _state.Staff.Count; i++)
            {
                StaffFile f = _state.Staff[i];
                if (f.AskDay == 0 && f.PoachDay == 0 && f.LeaveDay == 0)
                {
                    return f;
                }
            }
            TestRefusals.TellKey(Logger, test, "reallife_co_test_no_staff");
            return null;
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

        /// <summary>The first employee with nothing waiting asks for the going rate, or a little more than they are paid.</summary>
        public void TestRaise()
        {
            if (!TestReady("Company raise", CompanyPart.Staff))
            {
                return;
            }
            TestGuard("raise", () =>
            {
                StaffFile f = TestEmployee("Company raise");
                if (f == null)
                {
                    return;
                }
                BeforeTest();
                f.AskDay = Save.Day;
                f.AskTo = Math.Max(f.Going, f.Paid + 100);
                TestDone(f.Name + " asks for " + f.AskTo + " an hour (hundredths); paid " + f.Paid + ", going rate " + f.Going, TabStaff, "emp:" + f.Id);
            });
        }

        /// <summary>A rival's offer to the first employee with nothing waiting, at least a little over their pay.</summary>
        public void TestOffer()
        {
            if (!TestReady("Company offer", CompanyPart.Staff))
            {
                return;
            }
            TestGuard("offer", () =>
            {
                StaffFile f = TestEmployee("Company offer");
                if (f == null)
                {
                    return;
                }
                BeforeTest();
                CompanyShop shop = ShopAt(f.Place);
                string rival = shop != null && CompanyBridges.Rival != null ? CompanyBridges.Rival(shop.Reg) : null;
                StaffRules.Offer(f, CompanyStore.Clean(string.IsNullOrEmpty(rival) ? CompanyText.T("reallife_co_rival_default") : rival), Save.Day);
                f.PoachOffer = Math.Max(f.PoachOffer, f.Paid + 100);
                TestDone(f.Rival + " offers " + f.Name + " " + f.PoachOffer + " an hour (hundredths); paid " + f.Paid, TabStaff, "emp:" + f.Id);
            });
        }

        /// <summary>A strike called at the first business with staff, as long as the strictness gives (Standard's on Relaxed).</summary>
        public void TestStrike()
        {
            if (!TestReady("Company strike", CompanyPart.Staff))
            {
                return;
            }
            TestGuard("strike", () =>
            {
                CompanyShop shop = TestShop("Company strike", true);
                if (shop == null)
                {
                    return;
                }
                BeforeTest();
                shop.File.StrikeRest = 0;
                shop.File.StrikeFrom = shop.File.StrikeEnd = 0;
                StaffRules.Call(shop.File, Math.Max(_level, CompanyCatalogue.Standard), Save.Day);
                TestDone("a strike at " + shop.Name + " from day " + shop.File.StrikeFrom + " to day " + (shop.File.StrikeEnd - 1), TabStaff, "strike:" + shop.File.Place);
            });
        }

        /// <summary>A fire claim at the first business, with fire cover from the middle insurer if it has none; no fire, no bill.</summary>
        public void TestFireClaim()
        {
            if (!TestReady("Company claim", CompanyPart.Cover))
            {
                return;
            }
            TestGuard("claim", () =>
            {
                CompanyShop shop = TestShop("Company claim", false);
                if (shop == null)
                {
                    return;
                }
                BeforeTest();
                Policy p = shop.File.Cover[(int)CoverKind.Fire];
                if (!CoverRules.Covers(p, Save.Day))
                {
                    CoverRules.Buy(p, 1, Quote(shop, (int)CoverKind.Fire, 1), Save.Day, CompanyCatalogue.TermDays(CompanyGame.DaysPerYear));
                }
                Claim claim = CoverRules.Open(_state, p, shop.File.Place, shop.Name, (int)CoverKind.Fire, TestLoss, Save.Day);
                TestDone("a fire loss of " + TestLoss + " at " + shop.Name + " (no fire, no bill): insurer " + p.Insurer + " would pay " + (claim != null ? claim.Payout : 0),
                    TabCover, claim != null ? "claim:" + claim.Id : null);
            });
        }

        /// <summary>A report on flyers at the first business as if run the last three days, from the game's real figures; nothing charged.</summary>
        public void TestCampaign()
        {
            if (!TestReady("Company campaign", CompanyPart.Marketing))
            {
                return;
            }
            TestGuard("campaign", () =>
            {
                CompanyShop shop = TestShop("Company campaign", false);
                if (shop == null)
                {
                    return;
                }
                BeforeTest();
                int kind = (int)CampaignKind.Flyers, days = CompanyCatalogue.CampaignDays[kind];
                CampaignReport report = CampaignRules.Measure(shop.Name, kind, Save.Day - days, days, CampaignRules.Cost(kind, _index),
                    (int d, out int customers, out int takings, out int profit) => CompanyGame.DayBook(shop, d, out customers, out takings, out profit));
                CampaignRules.Keep(_state.Reports, report);
                TestDone("a report for flyers at " + shop.Name + " over days " + report.From + " to " + (Save.Day - 1) + " (not run, not charged): figures for "
                         + report.DaysBefore + " day(s) before and " + report.DaysDuring + " during, extra " + report.Extra, TabMarketing, "rep:" + (_state.Reports.Count - 1));
            });
        }

        /// <summary>The running quarter closes today and its return waits.</summary>
        public void TestReturn()
        {
            if (!TestReady("Company return", CompanyPart.Tax))
            {
                return;
            }
            TestGuard("return", () =>
            {
                BeforeTest();
                TaxFile t = _state.Tax;
                TaxRules.Close(t, CompanyGame.Sales(t.QuarterFrom, Save.Day - 1), Save.Day);
                TestDone("quarter " + t.ReturnQuarter + " closed today: takings " + t.ReturnTakings + ", deductible costs " + t.ReturnCosts + ", due by day " + t.ReturnDue, TabTax, "ret");
            });
        }

        /// <summary>An audit now, with <see cref="TestKeptBack"/> to find if need be; its charge and any court case are real.</summary>
        public void TestAudit()
        {
            if (!TestReady("Company audit", CompanyPart.Tax))
            {
                return;
            }
            TestGuard("audit", () =>
            {
                BeforeTest();
                if (_state.Tax.Saved <= 0)
                {
                    _state.Tax.Saved = TestKeptBack;
                }
                int found = _state.Tax.Saved;
                Audit(Save.Day);
                TestDone("an audit that found " + found + " kept back; still owed " + _state.Tax.Owed + ", court case " + _state.Tax.CaseNumber + " (the charge and any case are real)", TabTax, null);
            });
        }

        /// <summary>The first business's signage permit ran out yesterday.</summary>
        public void TestLicence()
        {
            if (!TestReady("Company licence", CompanyPart.Licences))
            {
                return;
            }
            TestGuard("licence", () =>
            {
                CompanyShop shop = TestShop("Company licence", false);
                if (shop == null)
                {
                    return;
                }
                BeforeTest();
                int kind = (int)LicenceKind.Signage;
                shop.File.Permits[kind].Until = Save.Day - 1;
                shop.File.Permits[kind].Auto = false;
                TestDone("the signage permit of " + shop.Name + " lapsed: it trades without one until it is bought", TabTax, "lic:" + shop.File.Place + ":" + kind);
            });
        }

        /// <summary>The state as before the first test tool changed it; money that moved since stays moved.</summary>
        public void TestBack()
        {
            if (Save == null || !ModState.Loaded)
            {
                TestRefusals.Tell(Logger, "Company back", TestRefusal.NotLoaded);
                return;
            }
            if (!_running || _broken)
            {
                TestRefusals.Tell(Logger, "Company back", TestRefusal.FeatureOff);
                return;
            }
            TestGuard("back", () =>
            {
                int version;
                CompanyState back = _beforeTests != null ? CompanyStore.Read(_beforeTests, out version) : null;
                _beforeTests = null;
                if (back == null)
                {
                    TestRefusals.TellKey(Logger, "Company back", "reallife_co_test_nothing_back");
                    return;
                }
                back.LastDay = Save.Day;
                back.Age = _state.Age;
                _state = back;
                int added, dropped, gone;
                Read(Save.Day, false, false, out added, out dropped, out gone);
                for (int i = 0; i < _shops.Count; i++)
                {
                    Refoot(_shops[i]);   // every figure, whether or not a test changed it
                }
                TestDone("the state from before the tests is back (" + _shops.Count + " business(es), " + _state.Staff.Count + " employee(s)); money that moved since stays moved",
                    TabOverview, null);
            });
        }
    }
}
