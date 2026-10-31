using System.Globalization;
using BigAmbitions.Mods;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The population's entry in the feature list (under the living economy's master option, Living population, which the "living" feature owns): its
    /// save line, its three tests, its Guide page and the figures the page names, each {name} filled from the catalogue when the page opens.
    /// </summary>
    public static class PopulationGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Register()
        {
            Features.Add("population", "reallife_meta_title_population")
                .About("reallife_guide_population_about").Help("reallife_guide_population")
                .Data(PopulationStore.Key)
                .Test("reallife_pop_test_generate", () => PopulationDirector.Instance?.TestGenerate())
                .Test("reallife_pop_test_month", () => PopulationDirector.Instance?.TestMonth())
                .Test("reallife_pop_test_household", () => PopulationDirector.Instance?.TestHousehold())
                .Fact("pop_flat_net", () => Whole(PopulationCatalogue.HomeNetShare / 10) + "%")
                .Fact("pop_desk", () => Whole(PopulationCatalogue.DeskSqm))
                .Fact("pop_free", () => Whole(PopulationCatalogue.TargetVacancy / 10) + "%")
                .Fact("pop_retire", () => Whole(PopulationCatalogue.RetireAge))
                .Fact("pop_sales_tax", () => (PopulationCatalogue.SalesTax / 10.0).ToString("0.#", Inv) + "%")
                .Fact("pop_property_tax", () => Whole(PopulationCatalogue.PropertyTaxShare / 10) + "%")
                .Fact("pop_rent_burden", () => Whole(PopulationCatalogue.RentBurdenMove / 10) + "%")
                .Cleanup(() => PopulationDirector.Instance?.Forget())
                .ResetData(() => PopulationDirector.Instance?.Forget());
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }

    /// <summary>The population's tests on the options page, after the living economy's master option.</summary>
    public static class PopulationOptions
    {
        public static ModOptions AddPopulation(this ModOptions options)
        {
            return options
                .AddButton("reallife_pop_test_generate", () => PopulationDirector.Instance?.TestGenerate())
                .AddButton("reallife_pop_test_month", () => PopulationDirector.Instance?.TestMonth())
                .AddButton("reallife_pop_test_household", () => PopulationDirector.Instance?.TestHousehold());
        }
    }
}
