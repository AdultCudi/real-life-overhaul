using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's streets' tests in the feature list, under the district's one feature (its ground's entry owns the option,
    /// the title and the clean-up; Features.Add hands back the entry already registered under the id, whichever registers
    /// first): the buses' reach over every block, the subway's answer and the furniture of every street. It registers
    /// itself (<see cref="DescribesFeatureAttribute"/>); its keys are written out whole as the options checks read them.
    /// </summary>
    [DescribesFeature]
    public static class StreetsGuide
    {
        private static void DescribeFeature()
        {
            Features.Add("uptown", LifeText.District)
                .Check("reallife_uptown_streets_test_reach", GroundDirector.TestReach, "Uptown check (bus reach)")
                .Check("reallife_uptown_streets_test_subway", GroundDirector.TestSubway, "Uptown check (subway)")
                .Check("reallife_uptown_streets_test_furniture", GroundDirector.TestFurniture, "Uptown check (street furniture)");
        }
    }
}
