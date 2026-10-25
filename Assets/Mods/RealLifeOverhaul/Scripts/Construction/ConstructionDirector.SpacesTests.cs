using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The test of the land on Uptown's spaces (Options > Tests): sites of one, two and three free spaces side by side bought
    /// for nothing in the free runs nearest the player, each joined into one lot, a plot planned on each with a design that
    /// fills it. The other tests take them on (plans, planning, stages, handover); "put the construction game back" lets
    /// them go. COST: nothing per frame; a pass over the district's lots when pressed.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string SitesCheck = "Construction check (sites of 1, 2 and 3 spaces bought, joined and planned)";

        /// <summary>Test: three sites of one, two and three spaces bought for nothing and planned, each a building that fills it. True when every one holds.</summary>
        public static bool TestSitesNow()
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, SitesCheck, TestRefusal.NotLoaded);
                Logger?.Warn(SitesCheck + ": the construction game has not read this save yet: FAIL.");
                return false;
            }
            if (!d.Running || GoingPublic.InSession || !DistrictLives())
            {
                TestRefusals.Tell(Logger, SitesCheck, TestRefusal.FeatureOff);
                Logger?.Warn(SitesCheck + ": the construction game is off, a Going Public session is on, or Uptown is not a district at this load: FAIL.");
                return false;
            }
            try
            {
                return d.Sites();
            }
            catch (Exception e)
            {
                d.Fault("a test (sites of spaces)", e);
                return false;
            }
        }

        private bool Sites()
        {
            var runs = new List<LandRun>(32);
            Runs(runs);
            UnityEngine.Vector3 near = ConstructionGame.Player;
            runs.Sort((a, b) => Far(a.First, near).CompareTo(Far(b.First, near)));
            // One space and two from one run's first spaces, three from another's: no site spills past its run.
            var plan = new List<KeyValuePair<string, int>>(3);
            for (int i = 0; i < runs.Count && plan.Count < 3; i++)
            {
                if (plan.Count == 0 && runs[i].Spaces >= 3 && SiteSpaces(runs[i].First.Id, 3, _strip))
                {
                    plan.Add(new KeyValuePair<string, int>(runs[i].First.Id, 1));
                    plan.Add(new KeyValuePair<string, int>(_strip[1].Id, 2));
                }
                else if (plan.Count == 2 && runs[i].Spaces >= 3)
                {
                    plan.Add(new KeyValuePair<string, int>(runs[i].First.Id, 3));
                }
            }
            if (plan.Count < 3 || _state.Lots.Count + 3 > BuildCatalogue.LotsMost)
            {
                TestRefusals.TellKey(Logger, SitesCheck, "reallife_bld_test_nothing");
                Logger?.Warn(SitesCheck + ": no two runs of three free spaces or more, or no room for three more lots: FAIL.");
                return false;
            }
            BeforeTest();
            bool all = true;
            var line = new System.Text.StringBuilder(256);
            var choices = new List<DesignChoice>(DesignsShown);
            foreach (KeyValuePair<string, int> site in plan)
            {
                SiteSpaces(site.Key, site.Value, _strip);
                int width = 0;
                for (int k = 0; k < _strip.Count; k++)
                {
                    width += _strip[k].Width;
                }
                int day = ConstructionGame.Day;
                _state.Lots.Add(new LotFile { Id = site.Key, Bought = day, Zones = (1 << (int)BuildUse.Count) - 1, TaxedTo = day, EmptySince = day, Spaces = site.Value });
                bool joined = Lot(site.Key, out LotView lot) && lot.Spaces == site.Value && lot.Width == width;
                int project = joined ? NewPlot(site.Key, out string why) : 0;
                ProjectFile p = _state.Project(project);
                bool ready = p != null && DesignChoices(p, choices);
                bool fills = ready && choices.Count > 0;
                for (int k = 0; fills && k < choices.Count; k++)
                {
                    fills &= BuildCatalogue.Offered(site.Value, SpacesOf(choices[k].Size, choices[k].Version), choices[k].Width >= lot.Width - BuildCatalogue.SiteSliver);
                }
                bool ok = joined && p != null && p.Width == width && (fills || !ready);
                all &= ok;
                line.Append(line.Length > 0 ? "; " : string.Empty).Append(site.Value.ToString(CultureInfo.InvariantCulture)).Append(" space(s) from ").Append(site.Key)
                    .Append(", ").Append(width.ToString(CultureInfo.InvariantCulture)).Append(" m, project ").Append(project.ToString(CultureInfo.InvariantCulture))
                    .Append(ready ? ", " + choices.Count + " design(s) that fill it" : ", its designs still being looked through").Append(ok ? "" : " (WRONG)");
            }
            Logger?.Info(SitesCheck + ": " + line + ": " + (all ? "PASS" : "FAIL") + ".");
            TestDone("three sites of spaces the player's, for nothing, a plot planned on each", TabProjects, 0);
            return all;
        }

        private static float Far(LotView lot, UnityEngine.Vector3 near)
        {
            float dx = lot.X - near.x, dz = lot.Z - near.z;
            return dx * dx + dz * dz;
        }
    }
}
