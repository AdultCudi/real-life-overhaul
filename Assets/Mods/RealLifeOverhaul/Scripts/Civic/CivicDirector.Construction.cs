using System;
using System.Collections.Generic;
using System.Globalization;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The construction test buttons: the game's scaffolding round the nearest building, a rebuild, a stage on, a frame storey, the crane.</summary>
    public sealed partial class CivicDirector
    {
        private const float TestReach = 80f;          // metres: the nearest site a stage test moves on
        private const int TestLandWidth = 24;         // metres of ground a test plot takes along the player's right
        private const int TestLandDepth = 20;         // and ahead
        private const int TestStoreys = 5;
        private const float CraneAhead = 12f;         // metres in front of the player the test crane stands

        private bool ConstructionReady(string test, bool changesSites)
        {
            if (!TestReady(test, true) || (changesSites && HostOwns(test, 'W')))
            {
                return false;
            }
            if (!Settings.CivicConstruction)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            return true;
        }

        private static void Note(string key, Dictionary<string, string> data)
        {
            Notifications.Show(NotificationType.Info, key, data, 10f, null, null, true, false);
        }

        /// <summary>Test: the nearest building wrapped to its roof in the game's own scaffolding, now (it goes up a few pieces a frame).</summary>
        public void TestWrap()
        {
            const string test = "Construction wrap";
            if (!ConstructionReady(test, false))
            {
                return;
            }
            try
            {
                CityBuildingController place = CivicCity.NearestBuilding(PlayerHelper.GetPosition(), 0f, 70f);
                if (place == null)
                {
                    TestRefusals.Tell(Logger, test, TestRefusal.NothingFits, 0, "no building with a street door within 70 m");
                    return;
                }
                string address = CivicCity.AddressText(place.building.StreetName, place.building.StreetNumber);
                string why = _sites.WrapNow(place, address);
                if (why != null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_construction_test_wrap_refused", address + ": " + why);
                    return;
                }
                _worldOut = true;
                WrapPlan plan = _sites.TestPlan;
                Logger?.Info("Construction test: " + address + " is being wrapped: " + plan.Modules + " module(s) a lift in " + plan.Runs + " run(s), " + plan.Lifts + " lift(s), "
                             + plan.Pieces.Count + " piece(s) with the fabric" + (plan.Why != null ? " (" + plan.Why + ")" : string.Empty) + ".");
                Note("reallife_construction_test_wrap_done", new Dictionary<string, string>(3)
                {
                    { "address", address }, { "modules", (plan.Modules * plan.Lifts).ToString() }, { "lifts", plan.Lifts.ToString() }
                });
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        /// <summary>Test: a rebuild site at the nearest building, set out now.</summary>
        public void TestRebuild()
        {
            TestSite(SiteKind.Rebuild);
        }

        /// <summary>Test: the nearest site moves on a stage now (a plot in its frame stage, a storey), and the street follows.</summary>
        public void TestStage()
        {
            const string test = "Construction stage";
            if (!ConstructionReady(test, true))
            {
                return;
            }
            try
            {
                SiteState site = Works.Nearest(PlayerHelper.GetPosition(), TestReach);
                if (site == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_construction_test_no_site");
                    return;
                }
                Advance(site);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Advance(SiteState site)
        {
            int day = TimeHelper.CurrentDay;
            string project = CivicWorks.ProjectName(site);
            if (!Works.AdvanceStage(site, day))
            {
                Logger?.Info("Construction test: " + site.Kind + " at " + CivicWorks.PlaceText(site) + " handed over.");
                Note("reallife_construction_test_handed", new Dictionary<string, string>(1) { { "project", project } });
                return;
            }
            if (!_worldFailed)
            {
                _sites.RefreshNow(day);
            }
            Logger?.Info("Construction test: " + site.Kind + " at " + CivicWorks.PlaceText(site) + " moved on to stage " + (site.Phase + 1)
                         + " (" + Mathf.RoundToInt(site.Progress * 100f) + "% done).");
            Note("reallife_construction_test_advanced", new Dictionary<string, string>(3)
            {
                { "project", project }, { "stage", (site.Phase + 1).ToString() }, { "doing", CivicWorks.PhaseName(site) }
            });
        }

        /// <summary>
        /// Test: a frame storey more on the nearest plot; with none near, a plot on the ground in front of the player, started
        /// at its first frame storey and set out now.
        /// </summary>
        public void TestStorey()
        {
            const string test = "Construction storey";
            if (!ConstructionReady(test, true))
            {
                return;
            }
            try
            {
                int day = TimeHelper.CurrentDay;
                Vector3 player = PlayerHelper.GetPosition();
                SiteState plot = NearestPlot(player);
                if (plot == null)
                {
                    plot = TestPlot(test, player, day);
                    if (plot == null)
                    {
                        return;
                    }
                }
                else if (!Works.AdvanceStage(plot, day))
                {
                    Note("reallife_construction_test_handed", new Dictionary<string, string>(1) { { "project", CivicWorks.ProjectName(plot) } });
                    return;
                }
                if (!_worldFailed && !_sites.DressNow(plot, day))
                {
                    Logger?.Info(test + " test: the plot was started but not set out (" + _sites.LastRefusal + ").");
                }
                _sites.RefreshNow(day);
                _worldOut = true;
                PlotRules.TryRead(plot.Street, out _, out _, out _, out int storeys);
                int standing = PlotRules.Standing(plot.Phase, plot.Progress, storeys);
                Logger?.Info("Construction test: the plot at " + plot.X.ToString("F0", CultureInfo.InvariantCulture) + ", " + plot.Z.ToString("F0", CultureInfo.InvariantCulture)
                             + " is at stage " + (plot.Phase + 1) + " with " + standing + " of " + storeys + " frame storey(s).");
                Note("reallife_construction_test_storey_done", new Dictionary<string, string>(2) { { "standing", standing.ToString() }, { "storeys", storeys.ToString() } });
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private SiteState NearestPlot(Vector3 player)
        {
            SiteState best = null;
            float bestSqr = 60f * 60f;
            for (int i = 0; i < Works.State.Sites.Count; i++)
            {
                SiteState site = Works.State.Sites[i];
                float sqr = CivicCity.Flat(new Vector3(site.X, site.Y, site.Z) - player);
                if (site.Kind == SiteKind.Plot && sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = site;
                }
            }
            return best;
        }

        // The test's ground: ahead of the player, its front towards them, clear of every building.
        private SiteState TestPlot(string test, Vector3 player, int day)
        {
            Vector3 ahead = Ahead();
            int yaw = Mathf.RoundToInt(Quaternion.LookRotation(ahead).eulerAngles.y);
            Vector3 middle = player + ahead * (TestLandDepth * 0.5f + 2f);
            if (CitySpots.Ground(middle, out float ground))
            {
                middle.y = ground;
            }
            var half = new Vector3(TestLandWidth * 0.5f, 5f, TestLandDepth * 0.5f);
            if (!CivicSpots.BoxFree(middle + Vector3.up * 5.5f, half, Quaternion.Euler(0f, yaw, 0f), CivicSpots.Walls))
            {
                TestRefusals.TellKey(Logger, test, "reallife_construction_test_no_ground");
                return null;
            }
            SiteState plot = Works.StartPlot(middle, yaw, TestLandWidth, TestLandDepth, TestStoreys, day, out string why);
            if (plot == null)
            {
                TestRefusals.TellKey(Logger, test, "reallife_construction_test_no_ground", why);
                return null;
            }
            plot.Done = plot.Needed * PlotRules.NextStep(0, 0f, TestStoreys);
            return plot;
        }

        private static Vector3 Ahead()
        {
            Vector3 ahead = PlayerHelper.PlayerController.transform.forward;
            ahead.y = 0f;
            return ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
        }

        /// <summary>Test: the tower crane alone, standing ahead of the player, its jib slewing by the engine's animation.</summary>
        public void TestCrane()
        {
            const string test = "Construction crane";
            if (!ConstructionReady(test, false))
            {
                return;
            }
            try
            {
                Vector3 ahead = Ahead();
                Vector3 at = PlayerHelper.GetPosition() + ahead * CraneAhead;
                if (CitySpots.Ground(at, out float ground))
                {
                    at.y = ground;
                }
                string why = _sites.CraneNow(at, ahead);
                if (why != null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_construction_test_crane_refused", why);
                    return;
                }
                _worldOut = true;
                Note("reallife_construction_test_crane_done", null);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
    }
}
