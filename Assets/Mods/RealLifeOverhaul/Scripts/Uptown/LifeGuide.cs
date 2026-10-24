using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.TestRun;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's district tests in the feature list, under the one feature of the district (its ground's entry owns the
    /// option, the title, the section and the clean-up: a feature's clean-up is one action, so the district's own is a hook
    /// of the ground's, UptownLots.Closing). Features.Add hands back the feature already registered under the id, so the
    /// tests join the ground's entry whichever registers first; should this one come first, the feature is named by the
    /// district's own name, the same words, until the ground's entry adds the rest. It registers itself
    /// (<see cref="DescribesFeatureAttribute"/>); its keys are written out whole as the options checks read them. Each test
    /// works when pressed or says why not in a notice; the offices, the map row and the frame cost join the city expansion's
    /// test run (the census, the business, the rent and the save are in its own table).
    /// </summary>
    [DescribesFeature]
    public static class LifeGuide
    {
        private static void DescribeFeature()
        {
            Features.Add("uptown", LifeText.District)
                .Test("reallife_uptown_test_census", LifeDirector.TestCensus)
                .Test("reallife_uptown_test_business", LifeDirector.TestBusiness)
                .Test("reallife_uptown_test_rent", LifeDirector.TestRent)
                .Test("reallife_uptown_test_save", LifeDirector.TestSave)
                .Check("reallife_uptown_test_offices", LifeDirector.TestOffices, "Uptown check (builders' offices)")
                .Check("reallife_uptown_test_row", LifeDirector.TestRow, "Uptown check (map row)")
                .Check("reallife_uptown_test_cost", LifeDirector.TestCost, "Uptown check (frame cost)", RunPlace.UptownLot)
                .Check("reallife_uptown_test_store", LifeDirector.TestStore, "Uptown check (wholesale store)", RunPlace.Outdoors)
                .Test("reallife_uptown_test_store_order", LifeDirector.TestOrder)
                .Check("reallife_uptown_test_station", LifeDirector.TestStation, "Uptown check (gas station)", RunPlace.Outdoors)
                .Test("reallife_uptown_test_station_fill", LifeDirector.TestFill);
        }
    }
}
