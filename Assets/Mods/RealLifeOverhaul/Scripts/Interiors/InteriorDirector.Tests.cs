using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>Interiors' test buttons on the options page.</summary>
    public sealed partial class InteriorDirector
    {
        // Another feature's test changed these businesses' records (a refit done now): what they were, to be put back.
        private readonly Dictionary<string, InteriorFile> _kept = new Dictionary<string, InteriorFile>(4, System.StringComparer.Ordinal);

        /// <summary>Test: a business's record kept as it is now (the first time only), so a test's refit can be put back.</summary>
        public void TestKeep(BuildingRegistration reg)
        {
            InteriorShop s = At(reg);
            InteriorFile f = s != null ? _store.File(s.Place, false) : null;
            if (f == null || _kept.ContainsKey(f.Place))
            {
                return;
            }
            _kept[f.Place] = new InteriorFile
            {
                Place = f.Place, Wear = f.Wear, LastInterior = f.LastInterior, LastDay = f.LastDay, StoryDay = f.StoryDay, RefreshDay = f.RefreshDay, FirstDay = f.FirstDay,
                Refit = f.Refit, RefitDay = f.RefitDay
            };
        }

        /// <summary>Test: every record kept put back as it was, and today's figures again; how many.</summary>
        public int TestPutBack()
        {
            int back = 0;
            foreach (KeyValuePair<string, InteriorFile> kept in _kept)
            {
                InteriorFile f = _store != null ? _store.File(kept.Key, false) : null;
                if (f == null)
                {
                    continue;
                }
                InteriorFile k = kept.Value;
                f.Wear = k.Wear;
                f.LastInterior = k.LastInterior;
                f.RefreshDay = k.RefreshDay;
                f.Refit = k.Refit;
                f.RefitDay = k.RefitDay;
                back++;
            }
            _kept.Clear();
            if (back > 0)
            {
                Rework(TimeHelper.CurrentDay, false);
            }
            Logger?.Info("Interiors: " + back + " business record(s) a test had changed put back as they were.");
            return back;
        }

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

        /// <summary>Test: the finishes of the business the player is in (or the first) a hundred days older, aged as play ages them.</summary>
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
            if (Parts.WearScale <= 0)
            {
                TestRefusals.TellKey(Logger, "Interiors wear", "reallife_interior_wear_off");   // play ages nothing with wear off: nor does the test
                return;
            }
            string name = s.Name;
            AgeFinishes(f, s, 100, Parts.WearScale);   // as play ages them: the wear option's strength, the cleaning service
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
