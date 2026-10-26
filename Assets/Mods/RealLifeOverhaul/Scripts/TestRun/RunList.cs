using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Floors;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.NewBuildings;
using UnityEngine;

namespace RealLifeOverhaul.TestRun
{
    /// <summary>
    /// Every check of the run with its work: the city expansion's own table (<see cref="RunPlan"/>), each id given the
    /// test it presses and what puts it back, then each feature's own <c>.Check(...)</c> from its guide. The run's own
    /// steps are here too: Uptown stood down in play, what is asked for at the load, the next hour, and the put backs at
    /// the end, each writing its own line. COST: the list is made once when a run begins; each step on its call.
    /// </summary>
    internal static class RunList
    {
        private const float UptownSettle = 3f;   // s after Uptown's clean-up before its census reads the stand-down
        private const string Sites = "sites";    // the run's duties (RunDirector.Duties): the first and the next site id of the civic tests
        private const string Road = "road";      // the run switched the test road on
        private const string Building = "building";   // the run asked for the test building

        /// <summary>The run's checks, the table first, then the features' own; ids registered twice say so once and are left out.</summary>
        public static List<RunCheck> All(out string twice)
        {
            List<RunCheck> all = RunPlan.All();
            for (int i = 0; i < all.Count; i++)
            {
                Bind(all[i]);
            }
            foreach (Feature feature in Features.All)
            {
                for (int k = 0; k < feature.Checks.Count; k++)
                {
                    feature.Checks[k].Order = all.Count;
                    all.Add(feature.Checks[k]);
                }
            }
            List<string> doubled = RunRules.Twice(all);
            twice = doubled.Count > 0 ? string.Join(", ", doubled) : null;
            for (int i = all.Count - 1; i >= 0 && doubled.Count > 0; i--)
            {
                if (doubled.Contains(all[i].Id) && IndexOf(all, all[i].Id) < i)
                {
                    all.RemoveAt(i);
                }
            }
            return all;
        }

        private static int IndexOf(List<RunCheck> all, string id)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == id)
                {
                    return i;
                }
            }
            return -1;
        }

        // The table's ids and the tests they press (each the button's own action), with what puts each back.
        private static void Bind(RunCheck c)
        {
            switch (c.Id)
            {
                case "reallife_held_test": c.Run = HeldUnits.Test; break;
                case "reallife_uptown_ground_test_census": c.Run = Uptown.GroundDirector.TestCensus; break;
                case "reallife_uptown_ground_test_path": c.Run = Uptown.GroundDirector.TestPath; break;
                case "reallife_uptown_ground_test_traffic": c.Run = Uptown.GroundDirector.TestTraffic; break;
                case "reallife_uptown_test_census": c.Run = Uptown.LifeDirector.TestCensus; break;
                case "reallife_uptown_test_business": c.Run = Uptown.LifeDirector.TestBusiness; break;
                case "reallife_uptown_test_save": c.Run = Uptown.LifeDirector.TestSave; break;
                case "reallife_test_district_save": c.Run = DistrictSave.DistrictSaveDirector.Test; break;
                case "reallife_trades_test_commission": c.Run = Trades(c, t => t.TestCommission()); break;
                case "reallife_trades_test_bribe": c.Run = Trades(c, t => t.TestBribe()); break;
                case "reallife_trades_test_apprentice": c.Run = Trades(c, t => t.TestApprentice()); break;
                case "reallife_trades_test_office": c.Run = Trades(c, t => t.TestOffice()); break;
                case "reallife_bld_test_lot": c.Run = Builder(c, d => d.TestLot()); break;
                case "reallife_bld_test_company": c.Run = Builder(c, d => d.TestCompany()); break;
                case "reallife_bld_test_licence": c.Run = Builder(c, d => d.TestLicence()); break;
                case "reallife_bld_test_plans": c.Run = Builder(c, d => d.TestPlans()); break;
                case "reallife_bld_test_planning": c.Run = Builder(c, d => d.TestPlanning()); break;
                case "reallife_bld_test_council": c.Run = Builder(c, d => d.TestCouncil()); break;
                case "reallife_bld_test_stage": c.Run = Builder(c, d => d.TestStage()); break;
                case "reallife_bld_test_inspection": c.Run = Builder(c, d => d.TestInspection()); break;
                case "reallife_bld_test_deliveries": c.Run = Builder(c, d => d.TestDeliveries()); break;
                case "reallife_bld_test_handover": c.Run = Builder(c, d => d.TestHandover()); break;
                case "reallife_bld_test_opening": c.Run = Builder(c, d => d.TestOpening()); break;
                case "reallife_bld_test_opening_missed": c.Run = Builder(c, d => d.TestOpeningMissed()); break;
                case "reallife_bld_test_tender": c.Run = Builder(c, d => d.TestTender()); break;
                case "reallife_bld_test_award": c.Run = Builder(c, d => d.TestAward()); break;
                case "reallife_bld_test_developers": c.Run = Builder(c, d => d.TestDevelopers()); break;
                case "reallife_bld_test_poach": c.Run = Builder(c, d => d.TestPoach()); break;
                case "reallife_bld_test_strike": c.Run = Builder(c, d => d.TestStrike()); break;
                case "reallife_bld_test_fault": c.Run = Builder(c, d => d.TestFault()); break;
                case "reallife_bld_test_bribe": c.Run = Builder(c, d => d.TestBribe()); break;
                case "reallife_bld_test_buyer": c.Run = Builder(c, d => d.TestBuyer()); break;
                case "reallife_bld_test_back": c.Run = Builder(c, d => d.TestBack()); break;
                case "reallife_floors_test_place":
                    c.Run = FloorsDirector.TestPlace;
                    c.Undo = FloorsDirector.TestPlace;   // pressed again, it takes the test office away and the player out
                    c.UndoOnPass = true;                 // a refused one was given back already
                    break;
                case "reallife_newground_test_census": c.Run = NewGround.GroundDirector.TestCensus; break;
                case "reallife_newground_test_under": c.Run = NewGround.GroundDirector.TestUnder; break;
                case "reallife_newground_test_parking": c.Run = NewGround.GroundDirector.TestParking; break;
                case "reallife_newground_test_patch": c.Run = NewGround.GroundDirector.TestPatch; break;
                case "reallife_newground_test_link": c.Run = NewGround.GroundDirector.TestLink; break;
                case "reallife_newground_test_grow": c.Run = NewGround.GroundDirector.TestGrow; break;
                case "reallife_construction_test_wrap": c.Run = Civic(c, d => d.TestWrap()); break;
                case "reallife_construction_test_rebuild":
                    c.Run = Civic(c, d =>
                    {
                        MarkSites(d);
                        d.TestRebuild();
                    });
                    c.Undo = () => EndMark(CivicDirector.Instance);
                    break;
                case "reallife_construction_test_stage": c.Run = Civic(c, d => d.TestStage()); break;
                case "reallife_construction_test_storey":
                    c.Run = Civic(c, d =>
                    {
                        MarkSites(d);
                        d.TestStorey();
                    });
                    c.Undo = () => EndMark(CivicDirector.Instance);
                    break;
                case "reallife_construction_test_crane": c.Run = Civic(c, d => d.TestCrane()); break;
                case "reallife_newbuildings_test_copy": c.Run = Made(c, d => d.TestCopy()); break;
                case "reallife_newbuildings_test_cost": c.Run = Made(c, d => d.TestCost()); break;
                case "reallife_newbuildings_test_walk": c.Run = Made(c, d => d.TestWalk()); break;
                case "reallife_newbuildings_test_save": c.Run = Made(c, d => d.TestSave()); break;
                case "reallife_newbuildings_test_off": c.Run = Made(c, d => d.TestOff()); break;
                case "reallife_test_city_away": c.Run = Pressed(c, "reallife_test_city_away"); break;
                case "reallife_test_city_back": c.Run = Pressed(c, "reallife_test_city_back"); break;
                case RunPlan.UptownOff: c.Run = UptownDown; break;
                case RunPlan.RoadOn: c.Run = RoadAsked; break;
                case RunPlan.BuildingOn: c.Run = BuildingAsked; break;
                case RunPlan.NextHour:
                    c.Run = () =>
                    {
                        if (!RunGame.ToNextHour())
                        {
                            Missing(c, "the game's time machine is busy");
                        }
                    };
                    c.Undo = () => NewBuildingsDirector.Instance?.TestShowAgain();
                    break;
                case RunPlan.SitesBack: c.Run = SitesPutBack; break;
                case RunPlan.RoadBack: c.Run = RoadPutBack; break;
                case RunPlan.BuildingBack: c.Run = BuildingPutBack; break;
                case RunPlan.PlayerBack: c.Run = () => MetaUi.Instance.Run(Guard.Routine(PlayerPutBack())); break;
                case RunPlan.LookBuilding: c.Run = () => RunDirector.Logger?.Info(RunPlan.LookBuildingLine + ": the run ends with the player at its door."); break;
            }
        }

        // ---------------------------------------------------------------- the directors, asked for when the check runs

        private static Action Trades(RunCheck c, Action<TradesDirector> work) => () => On(c, TradesDirector.Instance, work);

        private static Action Builder(RunCheck c, Action<ConstructionDirector> work) => () => On(c, ConstructionDirector.Instance, work);

        private static Action Civic(RunCheck c, Action<CivicDirector> work) => () => On(c, CivicDirector.Instance, work);

        private static Action Made(RunCheck c, Action<NewBuildingsDirector> work) => () => On(c, NewBuildingsDirector.Instance, work);

        private static void On<T>(RunCheck c, T director, Action<T> work) where T : class
        {
            if (director == null)
            {
                Missing(c, "its part is not running at this load");
                return;
            }
            work(director);
        }

        // A test of a feature's own registered under the Diagnostics tests (Core/CityCheck's), pressed as its button is.
        private static Action Pressed(RunCheck c, string labelKey) => () =>
        {
            Feature owner = Features.OwnerOfTest(labelKey);
            Action run = null;
            for (int i = 0; owner != null && i < owner.Tests.Count && run == null; i++)
            {
                run = owner.Tests[i].LabelKey == labelKey ? owner.Tests[i].Run : null;
            }
            if (run == null)
            {
                Missing(c, "its button is not registered");
                return;
            }
            run();
        };

        /// <summary>A check that could not be pressed says so as a test's refusal does, so the run reads it the same way.</summary>
        private static void Missing(RunCheck c, string why)
        {
            RunDirector.Logger?.Info(RunRules.NameOf(c) + RunRules.RefusedMark + why + ").");
        }

        // ---------------------------------------------------------------- the run's own steps

        // Uptown's clean-up in play, as the options page's clean-up runs it, then its census of the stand-down.
        private static void UptownDown()
        {
            Feature uptown = Features.Find("uptown");
            if (uptown == null || uptown.CleanupAction == null)
            {
                RunDirector.Logger?.Info("Uptown check (census, the test)" + RunRules.RefusedMark + "Uptown is not registered).");
                return;
            }
            RunDirector.Logger?.Info("Test run: Uptown's clean-up in play (it comes back at the next load).");
            uptown.CleanupAction();
            MetaUi.Instance.Run(Guard.Routine(CensusLater()));
        }

        private static IEnumerator CensusLater()
        {
            float until = Time.unscaledTime + UptownSettle;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
            Uptown.LifeDirector.TestCensus();
        }

        // The test road from the next load, unless it is on already (the run then leaves it as it found it).
        private static void RoadAsked()
        {
            if (Settings.NewGroundTestRoad)
            {
                RunDirector.Logger?.Info("New ground: the test road is added at each load already; the test run leaves it on.");
                return;
            }
            NewGround.GroundDirector.TestRoad();
            if (Settings.NewGroundTestRoad)
            {
                RunDirector.Duties[Road] = "1";
            }
        }

        private static void BuildingAsked()
        {
            if (NewBuildingsDirector.TestAskOnOpenGround())
            {
                RunDirector.Duties[Building] = "1";
            }
        }

        // The first site id before the civic tests set any out, kept once.
        private static void MarkSites(CivicDirector civic)
        {
            if (!RunDirector.Duties.ContainsKey(Sites) && civic.TestNextSite > 0)
            {
                RunDirector.Duties[Sites] = civic.TestNextSite.ToString(CultureInfo.InvariantCulture) + ":" + civic.TestNextSite.ToString(CultureInfo.InvariantCulture);
            }
        }

        // The next site id after a civic test set one out: the run takes back what lies between.
        private static void EndMark(CivicDirector civic)
        {
            if (civic == null || !RunDirector.Duties.TryGetValue(Sites, out string marks))
            {
                return;
            }
            int colon = marks.IndexOf(':');
            RunDirector.Duties[Sites] = (colon > 0 ? marks.Substring(0, colon) : marks) + ":" + civic.TestNextSite.ToString(CultureInfo.InvariantCulture);
        }

        private static void SitesPutBack()
        {
            int taken = 0;
            if (RunDirector.Duties.TryGetValue(Sites, out string marks) && CivicDirector.Instance != null)
            {
                int colon = marks.IndexOf(':');
                if (colon > 0 && int.TryParse(marks.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)
                    && int.TryParse(marks.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int to))
                {
                    taken = CivicDirector.Instance.TestTakeAway(from, to);
                }
            }
            RunDirector.Duties.Remove(Sites);
            RunDirector.Logger?.Info(RunPlan.SitesBackLine + ": " + taken + " site(s) the run's construction tests set out taken out of the programme (the street lets go of them out of view).");
        }

        private static void RoadPutBack()
        {
            bool asked = RunDirector.Duties.ContainsKey(Road);
            if (asked && Settings.NewGroundTestRoad)
            {
                NewGround.GroundDirector.TestRoad();   // pressed again, it is no longer added from the next load
            }
            RunDirector.Duties.Remove(Road);
            RunDirector.Logger?.Info(RunPlan.RoadBackLine + ": " + (asked ? "no longer added from the next load (the road there now stays until then)" : "left as the run found it") + ".");
        }

        private static void BuildingPutBack()
        {
            bool asked = RunDirector.Duties.ContainsKey(Building);
            if (asked)
            {
                NewBuildingsDirector.TestNoLongerAsked();
            }
            bool shown = NewBuildingsDirector.Instance != null && NewBuildingsDirector.Instance.TestShowAgain();
            RunDirector.Duties.Remove(Building);
            RunDirector.Logger?.Info(RunPlan.BuildingBackLine + ": " + (asked ? "no longer made from the next load (it stands until then)" : "left as the run found it")
                                     + (shown ? "; the made buildings the switch-off test hid shown again" : string.Empty) + ".");
        }

        private static IEnumerator PlayerPutBack()
        {
            var why = new string[1];
            yield return RunGame.Outdoors(why);
            RunDirector.Logger?.Info(RunPlan.PlayerBackLine + ": " + (why[0] == null ? "out of doors" : why[0] + ": FAIL") + ".");
        }
    }
}
