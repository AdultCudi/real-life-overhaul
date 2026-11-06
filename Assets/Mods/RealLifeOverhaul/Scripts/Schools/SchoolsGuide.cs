using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.TestRun;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The schools' entry in the feature list, its keys written out whole as the options checks read them: its option, its
    /// lines in the save, its Guide page's figures, its test entries and its clean-up. It registers itself
    /// (<see cref="DescribesFeatureAttribute"/>). Each test works when pressed or says why not in a notice, and is one of the
    /// city expansion's test run's checks (its .Check line), pressed where the player stands; the put back at the run's end.
    /// </summary>
    [DescribesFeature]
    public static class SchoolsGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static void DescribeFeature()
        {
            Features.Add("schools", "reallife_meta_title_schools")
                .About("reallife_guide_schools_about").Help("reallife_guide_schools")
                .Row(new ToggleOption(Settings.SchoolsId, "reallife_schools_enabled", true, on => Settings.Schools = on))
                .Owns(Settings.SchoolsId)
                .Data(SchoolStore.Key, SchoolStore.TestKey)
                .ResetData(SchoolsDirector.Forget)
                .Objects("RealLifeOverhaul Schools campus")
                .Fact("school_places", () => SchoolRules.Def(SchoolRules.CampusId).Capacity.ToString(Inv))
                .Fact("school_class", () => SchoolRules.ClassSize.ToString(Inv))
                .Fact("school_pool", () => SchoolRules.PrivatePool.ToString(Inv))
                .Fact("school_ratio", () => SchoolRules.RatioTarget.ToString(Inv))
                .Fact("school_inspect_days", () => SchoolRules.InspectEvery.ToString(Inv))
                .Fact("school_sale_days", () => SchoolRules.SaleDays.ToString(Inv))
                .Fact("school_asking", () => SchoolRules.AskingPremium.ToString(Inv))
                .Fact("school_sale_part", () => SchoolRules.SalePart.ToString(Inv))
                .Fact("school_poach", () => (SchoolRules.PoachPremium / 100 - 100).ToString(Inv))
                .Fact("school_age_from", () => ((int)SchoolRules.SchoolFrom).ToString(Inv))
                .Fact("school_age_to", () => ((int)SchoolRules.SchoolTo - 1).ToString(Inv))
                .Fact("school_count", () => SchoolRules.Count.ToString(Inv))
                .Fact("school_trusts", () => SchoolRules.Trusts.ToString(Inv))
                .Fact("school_hire_most", () => SchoolRules.HireMost.ToString(Inv))
                .Fact("school_head_days", () => SchoolRules.HeadHireDays.ToString(Inv))
                .Fact("school_pay_least", () => (SchoolRules.PayLeast / 100).ToString(Inv) + "%")
                .Fact("school_pay_most", () => (SchoolRules.PayMost / 100).ToString(Inv) + "%")
                .Fact("school_lift", () => SchoolRules.LiftReach.ToString(Inv))
                .Check("reallife_schools_test_census", SchoolsDirector.TestCensus, "Schools check (census)")
                .Check("reallife_schools_test_take", SchoolsDirector.TestTake, "Schools check (take me)")
                .Check("reallife_schools_test_run", SchoolsDirector.TestRun, "Schools check (school run)")
                .Check("reallife_schools_test_results", SchoolsDirector.TestResults, "Schools check (results day)")
                .Check("reallife_schools_test_inspect", SchoolsDirector.TestInspect, "Schools check (inspection)")
                .Check("reallife_schools_test_term", SchoolsDirector.TestTerm, "Schools check (term)")
                .Check("reallife_schools_test_mine", SchoolsDirector.TestMine, "Schools check (make it mine)")
                .Check("reallife_schools_test_enrol", SchoolsDirector.TestEnrol, "Schools check (enrol)")
                .Check("reallife_schools_test_look", SchoolsDirector.TestLook, "Schools check (look)")
                .Check("reallife_schools_test_compare", SchoolsDirector.TestCompare, "Schools check (compare)")
                .Check("reallife_schools_test_back", SchoolsDirector.TestBack, "Schools check (back)", RunPlace.Here, RunPhase.End)
                .Cleanup(() => SchoolsDirector.Instance?.StandDown("its things were put away"));
        }
    }
}
