using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>Interiors' test buttons on the options page.</summary>
    public sealed partial class InteriorDirector
    {
        // The business the player stands in, or the first of theirs the feature looks at.
        private InteriorShop Here(out BuildingRegistration reg)
        {
            reg = BuildingManager.IsInsideBuilding && InstanceBehavior<BuildingManager>.Instance != null ? InstanceBehavior<BuildingManager>.Instance.buildingRegistration : null;
            InteriorShop s = At(reg);
            if (s == null && _shops.Count > 0)
            {
                s = _shops[0];
                reg = BusinessGame.Held(s.Place);
            }
            return s;
        }

        private bool Refused(string test)
        {
            TestRefusal why = _broken || _store == null ? TestRefusal.NotLoaded : !Settings.Interiors ? TestRefusal.FeatureOff : TestRefusal.None;
            if (why != TestRefusal.None)
            {
                TestRefusals.Tell(Logger, test, why);
                return true;
            }
            return false;
        }

        /// <summary>Test: today's figures again, and those of the business the player is in shown.</summary>
        public void TestReport()
        {
            if (Refused("Interiors report"))
            {
                return;
            }
            Rework(TimeHelper.CurrentDay, false);
            BuildingRegistration reg;
            InteriorShop s = Here(out reg);
            if (s == null)
            {
                TestRefusals.TellKey(Logger, "Interiors report", "reallife_interior_test_none");
                return;
            }
            Logger?.Info("Interiors report: " + s.Name + ": interior " + s.Interior + " (" + s.InteriorNow + " after wear and dirt), decor " + s.DecorRaw + " (" + s.Decor + "), overall " + s.Overall
                         + ", standard " + s.Standard + " (income " + s.Income + ", game minimum " + s.GameMinimum + "), " + s.Sqm + " m2, target " + s.Target / 10 + ", wear " + s.Wear / 10
                         + "%, clean " + s.Clean + "; footfall " + s.Footfall + "% (standard " + s.StandardFootfall + " of " + s.StandardFull
                         + " in full, decor " + s.DecorFootfall + " of " + s.DecorFull
                         + ", bound " + s.Bound + ", business pressure's dip " + s.IssueDip + "), phased in " + s.Ramp / 10 + "% since day " + s.PhaseFrom
                         + ", bears " + s.Bears + " per mille, basket "
                         + s.Basket + " per mille, takings " + InteriorRules.Takings(s.Footfall, s.Basket) + " per mille, complaint " + s.ComplaintChance + " per mille, brand " + s.BrandPoints
                         + ", standing " + s.StandingPoints + ", comfort " + s.Comfort + ", office esteem " + s.OfficeEsteem + ", bids " + s.OfficeBid + " per mille.");
            Notifications.Show(NotificationType.Info, "reallife_interior_test_report", InteriorText.Report(s), 12f);
        }

        /// <summary>Test: the finishes of the business the player is in (or the first) a hundred days older.</summary>
        public void TestWear()
        {
            if (Refused("Interiors wear"))
            {
                return;
            }
            BuildingRegistration reg;
            InteriorShop s = Here(out reg);
            InteriorFile f = s != null ? _store.File(s.Place, true) : null;
            if (f == null)
            {
                TestRefusals.TellKey(Logger, "Interiors wear", "reallife_interior_test_none");
                return;
            }
            string name = s.Name;
            for (int d = 0; d < 100; d++)
            {
                f.Wear = InteriorRules.Age(f.Wear, s.Clean, false);
            }
            Rework(TimeHelper.CurrentDay, false);
            Logger?.Info("Interiors wear test: " + name + "'s finishes a hundred days older: wear " + f.Wear / 10 + "%.");
            Notifications.Show(NotificationType.Info, "reallife_interior_test_worn", new Dictionary<string, string>(2) { { "business", name }, { "wear", (f.Wear / 10).ToString() } }, 9f);
        }

        /// <summary>Test: the business the player is in (or the first) in tomorrow's paper, striking or shabby by its figures.</summary>
        public void TestNews()
        {
            if (Refused("Interiors news"))
            {
                return;
            }
            BuildingRegistration reg;
            InteriorShop s = Here(out reg);
            if (s == null)
            {
                TestRefusals.TellKey(Logger, "Interiors news", "reallife_interior_test_none");
                return;
            }
            bool striking = s.Overall >= s.Standard;
            InteriorBridges.Story?.Invoke(striking ? "reallife_news_story_interior_striking" : "reallife_news_story_interior_shabby");
            if (striking)
            {
                InteriorBridges.Striking?.Invoke(s.Place, s.Name);
            }
            Logger?.Info("Interiors news test: " + s.Name + " in tomorrow's paper for " + (striking ? "a striking" : "a shabby") + " interior.");
            Notifications.Show(NotificationType.Info, "reallife_interior_test_news", new Dictionary<string, string>(1) { { "business", s.Name } }, 9f);
        }
    }
}
