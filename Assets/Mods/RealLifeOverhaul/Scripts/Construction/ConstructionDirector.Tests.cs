using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The construction game's test entries (Options > Tests): each brings forward one thing the player would wait for, or
    /// says in a notice why it cannot, and writes its "Construction check (...)" line. The state before the first test is
    /// kept for "put the construction game back"; money moved after a test is real.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const int TestLotWidth = 24, TestLotDepth = 30;
        internal const string TestLotPrefix = "test:";

        private double _testPrice = double.NaN;   // the district's price where a test lot was made
        private bool _testOpening;                // a test opening: the ribbon can be cut at any hour of its day

        internal static bool IsTestLot(string lot) => lot != null && lot.StartsWith(TestLotPrefix, StringComparison.Ordinal);

        private bool TestReady(string test, bool outdoors)
        {
            if (!ModState.Loaded || SaveGameManager.Current == null || !_loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (!Running)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (outdoors && !ConstructionGame.Outdoors)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.Indoors);
                return false;
            }
            return true;
        }

        // A site of the city's programme is the host's in a Going Public session: a guest's test leaves it be.
        private bool HostOnly(string test)
        {
            if (GoingPublic.IsGuest)
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_test_host");
                return true;
            }
            return false;
        }

        // The state as it is before a test first changes it, kept once to be put back.
        private void BeforeTest()
        {
            if (_beforeTests == null)
            {
                _beforeTests = BuildStore.Write(_state);
            }
        }

        private void TestDone(string what, int tab, int pick)
        {
            Logger?.Warn("Construction test: " + what + ".");   // asked for by a button: written whether or not diagnostics are on
            Changed();
            Notifications.Show(NotificationType.Info, "reallife_bld_test_done", null, 7f, null, Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        private void Nothing(string test)
        {
            TestRefusals.TellKey(Logger, test, "reallife_bld_test_nothing");
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

        // ------------------------------------------------------------------ the company and its land

        /// <summary>Test: a lot of the player's ahead of them, 24 by 30 m and zoned for every use, for the loop without the district's lots.</summary>
        public void TestLot()
        {
            const string test = "Construction lot";
            if (!TestReady(test, true))
            {
                return;
            }
            if (GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_answer_session_land");
                return;
            }
            TestGuard(test, () =>
            {
                Vector3 ahead = ConstructionGame.Ahead;
                int yaw = Mathf.RoundToInt(Quaternion.LookRotation(ahead).eulerAngles.y);
                Vector3 middle = ConstructionGame.Player + ahead * (TestLotDepth * 0.5f + 2f);
                if (!GroundClear(middle, yaw, TestLotWidth, TestLotDepth, out float ground))
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_test_no_ground");
                    return;
                }
                BeforeTest();
                int day = ConstructionGame.Day;
                string id = TestLotPrefix + _state.NewId().ToString(System.Globalization.CultureInfo.InvariantCulture);
                _testLots.Add(new LotView
                {
                    Id = id, Street = string.Empty, X = middle.x, Y = ground, Z = middle.z, Yaw = yaw, Width = TestLotWidth, Depth = TestLotDepth, Class = 0, Open = true, Traffic = 50
                });
                _testPrice = ConstructionGame.PriceNear(middle);
                _state.Lots.Add(new LotFile { Id = id, Bought = day, Zones = (1 << (int)BuildUse.Count) - 1, TaxedTo = day, EmptySince = day });
                Logger?.Info("Construction check (test lot): lot " + id + " of " + TestLotWidth + " by " + TestLotDepth
                    + " m ahead of the player, turned " + yaw + ", its ground clear; the price a square metre there "
                             + (double.IsNaN(_testPrice) ? "not known" : _testPrice.ToString("0", System.Globalization.CultureInfo.InvariantCulture)) + ": PASS.");
                TestDone("a lot of the player's ahead of them (" + id + "), for nothing", TabLand, 0);
            });
        }

        /// <summary>Test: the company founded at the player's office of its type, its licence granted at once.</summary>
        public void TestCompany()
        {
            const string test = "Construction company";
            if (!TestReady(test, false))
            {
                return;
            }
            if (!Office(out PlaceView office))
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_test_no_office");
                return;
            }
            TestGuard(test, () =>
            {
                BeforeTest();
                int day = ConstructionGame.Day;
                FirmFile f = _state.Firm;
                if (f.Founded <= 0)
                {
                    f.Founded = day;
                    f.Office = office.Place;
                }
                f.Licence = LicenceState.Granted;
                f.LicenceDay = day;
                f.LicenceUntil = PlanningRules.LicenceUntil(day, ConstructionGame.DaysPerYear);
                Logger?.Info("Construction check (company): founded at " + office.Place + " on day " + f.Founded + ", licensed to day " + f.LicenceUntil + ": PASS.");
                TestDone("the company founded at " + office.Name + " and licensed", TabOverview, 0);
            });
        }

        /// <summary>Test: the licence applied for is decided now, as City Hall would decide it.</summary>
        public void TestLicence()
        {
            const string test = "Construction licence";
            if (!TestReady(test, false))
            {
                return;
            }
            if (_state.Firm.Founded <= 0)
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_answer_no_company");
                return;
            }
            TestGuard(test, () =>
            {
                BeforeTest();
                FirmFile f = _state.Firm;
                int day = ConstructionGame.Day;
                f.Licence = LicenceState.Applied;
                f.LicenceDay = day - BuildCatalogue.LicenceDays;
                LicenceDay(day);
                Logger?.Info("Construction check (licence): decided now with a record " + Record() + ": " + f.Licence + (f.Licence == LicenceState.Granted ? " to day " + f.LicenceUntil : string.Empty)
                             + ": PASS.");
                TestDone("the licence decided now: " + f.Licence, TabCompany, 0);
            });
        }

        // ------------------------------------------------------------------ the papers

        /// <summary>Test: every set of plans being drawn is drawn now, and every engineer's sign off given now.</summary>
        public void TestPlans()
        {
            const string test = "Construction plans";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int drawn = 0, signed = 0, last = 0;
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.Stage == ProjectStage.Design && !p.PlansDone && p.PlansDay > 0)
                    {
                        BeforeTest();
                        if (p.Architect == ArchitectKind.Player)
                        {
                            bool done = PlayerDesign(CommissionId(p), out int total, out int cost, out int storeys, out int units);
                            OwnDesign(p, done ? total : 70, done ? cost : 1000, done ? storeys : p.Storeys, done ? units : 0);
                        }
                        else
                        {
                            PlansDrawn(p, p.Quality);
                        }
                        drawn++;
                        last = p.Id;
                    }
                    if (p.PlansDone && !p.Engineered && p.EngineerDay > 0)
                    {
                        BeforeTest();
                        p.Engineered = true;
                        signed++;
                        last = p.Id;
                    }
                }
                if (drawn + signed == 0)
                {
                    Nothing(test);
                    return;
                }
                Logger?.Info("Construction check (plans): " + drawn + " set(s) of plans drawn now, " + signed + " engineer's sign off given now: PASS.");
                TestDone(drawn + " set(s) of plans drawn and " + signed + " signed off now", TabProjects, last);
            });
        }

        /// <summary>Test: every planning application is decided now, and every appeal heard now.</summary>
        public void TestPlanning()
        {
            const string test = "Construction planning";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int decided = 0, heard = 0, last = 0;
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.Planning == PlanningState.Submitted)
                    {
                        BeforeTest();
                        Decide(p);
                        decided++;
                        last = p.Id;
                    }
                    else if (p.Planning == PlanningState.Appealed)
                    {
                        BeforeTest();
                        Hear(p);
                        heard++;
                        last = p.Id;
                    }
                }
                if (decided + heard == 0)
                {
                    Nothing(test);
                    return;
                }
                Logger?.Info("Construction check (planning test): " + decided + " application(s) decided now, " + heard + " appeal(s) heard now: PASS.");
                TestDone(decided + " application(s) decided and " + heard + " appeal(s) heard now", TabProjects, last);
            });
        }

        /// <summary>Test: the council votes now on every change of zoning asked of it.</summary>
        public void TestCouncil()
        {
            const string test = "Construction council";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int votes = 0;
                for (int i = 0; i < _state.Lots.Count; i++)
                {
                    if (_state.Lots[i].ZoneAsk != 0)
                    {
                        BeforeTest();
                        ZoneVote(_state.Lots[i]);
                        votes++;
                    }
                }
                if (votes == 0)
                {
                    Nothing(test);
                    return;
                }
                Logger?.Info("Construction check (council): " + votes + " change(s) of zoning voted on now: PASS.");
                TestDone(votes + " zoning vote(s) now", TabLand, 0);
            });
        }

        // ------------------------------------------------------------------ the sites

        /// <summary>Test: every site of the company moves on to its next stage now (the last hands it over).</summary>
        public void TestStage()
        {
            const string test = "Construction stage";
            if (!TestReady(test, false) || HostOnly(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int moved = 0, last = 0;
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.Stage == ProjectStage.Building && p.SiteId > 0)
                    {
                        BeforeTest();
                        int site = p.SiteId;
                        bool still = AdvanceSiteNow(site);
                        Logger?.Info("Construction check (stage): project " + p.Id + "'s site " + site + (still ? " moved on to its next stage" : " was handed over from its last stage") + ": PASS.");
                        moved++;
                        last = p.Id;
                    }
                }
                if (moved == 0)
                {
                    Nothing(test);
                    return;
                }
                TestDone(moved + " site(s) moved on a stage", TabProjects, last);
            });
        }

        /// <summary>Test: every inspection due or booked is held now.</summary>
        public void TestInspection()
        {
            const string test = "Construction inspection";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int day = ConstructionGame.Day, held = 0, last = 0;
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.Stage != ProjectStage.Building)
                    {
                        continue;
                    }
                    for (int k = 0; k < (int)Inspection.Count; k++)
                    {
                        if (p.Insp[k] == InspectionState.Due || p.Insp[k] == InspectionState.Booked)
                        {
                            BeforeTest();
                            p.Insp[k] = InspectionState.Booked;
                            p.InspDay[k] = day;
                            Inspect(p, k, day);
                            held++;
                            last = p.Id;
                        }
                    }
                }
                if (held == 0)
                {
                    Nothing(test);
                    return;
                }
                Logger?.Info("Construction check (inspection test): " + held + " inspection(s) held now: PASS.");
                TestDone(held + " inspection(s) held now", TabProjects, last);
            });
        }

        /// <summary>Test: every order from the builders' merchants arrives now.</summary>
        public void TestDeliveries()
        {
            const string test = "Construction deliveries";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int day = ConstructionGame.Day, came = 0, last = 0;
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.Stage == ProjectStage.Building && p.OrderDay > 0)
                    {
                        BeforeTest();
                        Deliver(p, day);
                        came++;
                        last = p.Id;
                    }
                }
                if (came == 0)
                {
                    Nothing(test);
                    return;
                }
                Logger?.Info("Construction check (deliveries): " + came + " order(s) delivered now: PASS.");
                TestDone(came + " delivery(ies) now", TabProjects, last);
            });
        }

        /// <summary>Test: every site of the company is handed over now, its works done.</summary>
        public void TestHandover()
        {
            const string test = "Construction handover";
            if (!TestReady(test, false) || HostOnly(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int handed = 0, last = 0;
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.Stage == ProjectStage.Building && p.SiteId > 0)
                    {
                        BeforeTest();
                        int id = p.Id;
                        bool done = FinishSite(p.SiteId);
                        if (!done && p.Stage == ProjectStage.Building)
                        {
                            Handover(p, ConstructionGame.Day);   // a site no longer in the programme: handed over all the same
                        }
                        Logger?.Info("Construction check (handover test): project " + id + " handed over now " + (done ? "by its site" : "without a site") + ": PASS.");
                        handed++;
                        last = id;
                    }
                }
                if (handed == 0)
                {
                    Nothing(test);
                    return;
                }
                TestDone(handed + " project(s) handed over now", TabProjects, last);
            });
        }

        /// <summary>Test: the latest building finished has its opening day today, the ribbon at its front whatever the hour.</summary>
        public void TestOpening()
        {
            const string test = "Construction opening";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ProjectFile p = Latest(ProjectStage.Finished);
                if (p == null)
                {
                    Nothing(test);
                    return;
                }
                BeforeTest();
                p.Opening = ConstructionGame.Day;
                p.Opened = false;
                p.Ribbon = false;
                _testOpening = true;
                bool place = OpeningPlace(p, out Vector3 at);
                Logger?.Info("Construction check (opening test): project " + p.Id + " opens today; its ribbon " + (place ? "at " + at.x.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                    + ", " + at.z.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) : "has no place") + ": "
                             + (place ? "PASS" : "FAIL") + ".");
                Tell(NotificationType.Info, "reallife_bld_notice_opening", ProjectName(p), null, TabProjects, p.Id);
                TestDone("project " + p.Id + "'s opening is today", TabProjects, p.Id);
            });
        }

        private ProjectFile Latest(ProjectStage stage)
        {
            for (int i = _state.Projects.Count - 1; i >= 0; i--)
            {
                if (_state.Projects[i].Stage == stage)
                {
                    return _state.Projects[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ the market

        /// <summary>Test: a business's tender opens now, its bids closing in two hours.</summary>
        public void TestTender()
        {
            const string test = "Construction tender";
            if (!TestReady(test, false) || HostOnly(test))
            {
                return;
            }
            if (!Settings.ConstructionDevelopers)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            TestGuard(test, () =>
            {
                BeforeTest();
                int before = _state.Tenders.Count, day = ConstructionGame.Day;
                _state.NextClientDay = day;
                ClientTenders(day);
                if (_state.Tenders.Count == before)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_test_no_client");
                    return;
                }
                TenderFile t = _state.Tenders[_state.Tenders.Count - 1];
                t.Closes = day * 24 + ConstructionGame.Hour + 2;
                Logger?.Info("Construction check (tender test): tender " + t.Id + " for " + t.Value + " opened now, closing at hour " + t.Closes + ": PASS.");
                TestDone("tender " + t.Id + " opened now", TabTenders, t.Id);
            });
        }

        /// <summary>Test: every open tender is awarded now.</summary>
        public void TestAward()
        {
            const string test = "Construction award";
            if (!TestReady(test, false) || HostOnly(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int awarded = 0, last = 0, day = ConstructionGame.Day;
                for (int i = 0; i < _state.Tenders.Count; i++)
                {
                    TenderFile t = _state.Tenders[i];
                    if (t.State == TenderState.Open)
                    {
                        BeforeTest();
                        Award(t, day);
                        awarded++;
                        last = t.Id;
                    }
                }
                if (awarded == 0)
                {
                    Nothing(test);
                    return;
                }
                Logger?.Info("Construction check (award test): " + awarded + " tender(s) awarded now: PASS.");
                TestDone(awarded + " tender(s) awarded now", TabTenders, last);
            });
        }

        /// <summary>Test: the city's developers finish the development going up longest now, or begin their next now.</summary>
        public void TestDevelopers()
        {
            const string test = "Construction developers";
            if (!TestReady(test, false))
            {
                return;
            }
            if (GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_test_session");   // the developers' days are every machine's
                return;
            }
            if (!Settings.ConstructionDevelopers)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return;
            }
            if (!DistrictLives() || Lots.Count == 0)
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_land_no_district");
                return;
            }
            TestGuard(test, () =>
            {
                IReadOnlyList<LotView> lots = Lots;
                for (int i = 0; i < _state.Developments.Count; i++)
                {
                    DevFile dev = _state.Developments[i];
                    if (dev.SiteId > 0 && !dev.Handed && !dev.Public && Lot(dev.Lot, out LotView going))
                    {
                        BeforeTest();
                        FinishDevelopment(dev, going, DevelopmentRules.Use(going.Id, going.Class));
                        Logger?.Info("Construction check (developers test): lot " + going.Id + "'s development finished now: " + (dev.Handed ? "PASS"
                            : "FAIL (" + "the district did not take it" + ")") + ".");
                        TestDone("the developers finished lot " + going.Id, TabRivals, 0);
                        return;
                    }
                }
                if (_devOrderStale)
                {
                    DevelopmentRules.Order(_lots, _devOrder);
                    _devOrderStale = false;
                }
                int day = ConstructionGame.Day;
                for (int k = 0; k < _devOrder.Count; k++)
                {
                    LotView lot = lots[_devOrder[k]];
                    DevFile dev = _state.Development(lot.Id);
                    if (!lot.Open || (dev != null && (dev.Handed || dev.SiteId > 0 || dev.Design == NoDesign)) || _state.Lot(lot.Id) != null || LotTaken(lot.Id))
                    {
                        continue;
                    }
                    BuildUse use = DevelopmentRules.Use(lot.Id, lot.Class);
                    if (dev == null)
                    {
                        dev = new DevFile { Lot = lot.Id };
                    }
                    if (string.IsNullOrEmpty(dev.Design) && !DevDesign(lot, use, dev))
                    {
                        TestRefusals.TellKey(Logger, test, "reallife_bld_designs_wait");
                        return;
                    }
                    if (dev.Design == NoDesign)
                    {
                        continue;
                    }
                    BeforeTest();
                    if (!_state.Developments.Contains(dev))
                    {
                        _state.Developments.Add(dev);
                    }
                    StartDevelopment(dev, lot, use, k, day);
                    Logger?.Info("Construction check (developers test): lot " + lot.Id + "'s development begun now: " + (dev.SiteId > 0 ? "PASS" : "FAIL (no site)") + ".");
                    TestDone("the developers began on lot " + lot.Id, TabRivals, 0);
                    return;
                }
                Nothing(test);
            });
        }

        // ------------------------------------------------------------------ crews and troubles

        /// <summary>Test: a rival offers the company's best crew more pay now.</summary>
        public void TestPoach()
        {
            const string test = "Construction poach";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                CrewFile best = null;
                for (int i = 0; i < _state.Crews.Count; i++)
                {
                    best = _state.Crews[i].PoachRival < 0 && (best == null || _state.Crews[i].Skill > best.Skill) ? _state.Crews[i] : best;
                }
                if (best == null)
                {
                    Nothing(test);
                    return;
                }
                BeforeTest();
                best.PoachRival = BuildCatalogue.Clamp(best.Id % BuildCatalogue.RivalCount, 0, BuildCatalogue.RivalCount - 1);
                best.PoachPay = (BuildCatalogue.PoachPayLeast + BuildCatalogue.PoachPayMost) / 2;
                best.PoachUntil = ConstructionGame.Day + BuildCatalogue.PoachDays;
                Tell(NotificationType.Info, "reallife_bld_notice_poach", ForemanWords(best), BuildCatalogue.RivalKeys[best.PoachRival], TabCrews, 0, best.PoachPay / 10);
                Logger?.Info("Construction check (poach test): crew " + best.Id + " offered " + best.PoachPay + " in 1000 more by rival "
                    + best.PoachRival + " until day " + best.PoachUntil + ": PASS.");
                TestDone("a rival's offer to crew " + best.Id, TabCrews, 0);
            });
        }

        /// <summary>Test: the first crew at work on a site goes on strike now.</summary>
        public void TestStrike()
        {
            const string test = "Construction strike";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                for (int i = 0; i < _state.Crews.Count; i++)
                {
                    CrewFile c = _state.Crews[i];
                    ProjectFile p = _state.Project(c.Project);
                    if (p == null || p.Stage != ProjectStage.Building || c.StrikeUntil >= ConstructionGame.Day)
                    {
                        continue;
                    }
                    BeforeTest();
                    int day = ConstructionGame.Day, days = BuildCatalogue.StrikeLeast;
                    c.StrikeUntil = PlanningRules.AfterWorkingDays(day, days) - 1;
                    c.Morale = BuildCatalogue.MoraleAfterStrike;
                    p.StrikeDays += days;
                    Tell(NotificationType.Warning, "reallife_bld_notice_strike", ForemanWords(c), null, TabCrews, 0, days);
                    Logger?.Info("Construction check (strike test): crew " + c.Id + " on strike until day " + c.StrikeUntil + "; project " + p.Id + "'s site goes without them: PASS.");
                    TestDone("crew " + c.Id + " on strike", TabCrews, 0);
                    return;
                }
                Nothing(test);
            });
        }

        /// <summary>Test: a fault comes to light now in the latest building finished: its claim filed, the insurer's share paid.</summary>
        public void TestFault()
        {
            const string test = "Construction fault";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ProjectFile p = Latest(ProjectStage.Finished);
                if (p == null)
                {
                    Nothing(test);
                    return;
                }
                BeforeTest();
                p.DefectClaim = Math.Max(1000L, Math.Max(p.Spent, p.ClientValue) * 3 / 100);
                Defect(p);
                Logger?.Info("Construction check (fault test): project " + p.Id + "'s fault came to light now, its claim filed: PASS.");
                TestDone("a fault at project " + p.Id, TabProjects, p.Id);
            });
        }

        /// <summary>Test: a bribe paid to the inspector comes out now.</summary>
        public void TestBribe()
        {
            const string test = "Construction bribe";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.BribeDay > 0)
                    {
                        BeforeTest();
                        BribeOut(p);
                        Logger?.Info("Construction check (bribe test): project " + p.Id + "'s bribe of " + p.BribeAmount + " came out now: PASS.");
                        TestDone("project " + p.Id + "'s bribe came out", TabProjects, p.Id);
                        return;
                    }
                }
                TestRefusals.TellKey(Logger, test, "reallife_bld_test_no_bribe");
            });
        }

        /// <summary>Test: a buyer takes one of a finished block's flats now, at its asking price.</summary>
        public void TestBuyer()
        {
            const string test = "Construction buyer";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.After == AfterUse.SellFlats && p.Stage == ProjectStage.Finished && p.FlatsSold < p.Flats)
                    {
                        BeforeTest();
                        bool sold = FlatSale(p);
                        Logger?.Info("Construction check (buyer test): project " + p.Id + ": " + (sold ? "a flat sold now, " + (p.Flats - p.FlatsSold)
                            + " left: PASS" : "no price for its flats: FAIL") + ".");
                        TestDone("a buyer for a flat at project " + p.Id, TabProjects, p.Id);
                        return;
                    }
                }
                Nothing(test);
            });
        }

        // ------------------------------------------------------------------ back as it was

        /// <summary>Puts back the construction game as it was before the first test: sites the tests began are taken away.</summary>
        public void TestBack()
        {
            const string test = "Construction back";
            if (!ModState.Loaded || SaveGameManager.Current == null || !_loaded)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return;
            }
            TestGuard(test, () =>
            {
                BuildState back = _beforeTests != null ? BuildStore.Read(_beforeTests, out _) : null;
                if (back == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_test_nothing_back");
                    return;
                }
                int dropped = 0;
                for (int i = 0; i < _state.Projects.Count; i++)
                {
                    ProjectFile p = _state.Projects[i];
                    if (p.SiteId > 0 && p.Stage == ProjectStage.Building && back.OnSite(p.SiteId) == null && DropSite(p.SiteId))
                    {
                        dropped++;
                    }
                }
                for (int i = 0; i < _state.Developments.Count; i++)
                {
                    DevFile dev = _state.Developments[i];
                    if (dev.SiteId > 0 && !dev.Handed && back.Development(dev.Lot) == null && DropSite(dev.SiteId))
                    {
                        dropped++;
                    }
                }
                back.LastDay = Math.Max(back.LastDay, _state.LastDay);
                _state = back;
                _beforeTests = null;
                _testLots.Clear();
                _testOpening = false;
                _devOrderStale = true;
                HidePanels();
                Logger?.Info("Construction check (back): the state from before the tests is back (" + _state.Projects.Count + " project(s), " + _state.Crews.Count + " crew(s)); " + dropped
                             + " site(s) the tests began taken away; money moved since stays moved: PASS.");
                TestDone("the construction game is as it was before the tests", TabOverview, 0);
            });
        }
    }
}
