using System;
using System.Globalization;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// The Guide's page on where the mod's own businesses' customers come from under Living population, each {name} on it filled from the catalogue when the
    /// page opens; and the businesses' own pages, which end with a pointer to it while the option is on (Wiring/DemandLinks hands the pointer to the Guide).
    /// Built when a page opens; no cost while the Guide is shut.
    /// </summary>
    public static class PopDemandGuide
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The Guide pages of the businesses whose customers come from the city's people.</summary>
        public static readonly string[] Pages =
        {
            "economy_ownbank", "economy_bank_branch", "economy_estate", "economy_estate_duties", "construction", "economy_dealer", "economy_online", "hustles",
            "careers_family", "utilities", "civic", "crime", "tech_jobs", "careers_jobs"
        };

        public static void Register()
        {
            Features.Add("population_demand", "reallife_meta_title_population_demand")
                .About("reallife_guide_population_demand_about").Help("reallife_guide_population_demand")
                .Fact("dem_poor_days", () => Whole(PopDemandCatalogue.PoorDebtDays))
                .Fact("dem_default_days", () => Whole(PopDemandCatalogue.DefaultDebtDays))
                .Fact("dem_hotel_business", () => Weight(PopDemand.HotelGuests, PopSource.Wages))
                .Fact("dem_hotel_visitors", () => Weight(PopDemand.HotelGuests, PopSource.Visitors))
                .Fact("dem_hotel_residents", () => Weight(PopDemand.HotelGuests, PopSource.Leisure))
                .Fact("dem_owner_years", () => Whole(PopDemandCatalogue.OwnerYears))
                .Fact("dem_starter_from", () => Whole(PopDemandCatalogue.StarterFrom))
                .Fact("dem_starter_to", () => Whole(PopDemandCatalogue.StarterTo))
                .Fact("dem_car_years", () => Whole(PopDemandCatalogue.CarLifeYears))
                .Fact("dem_online_clothing", () => Share(PopDemandCatalogue.OnlineShare[(int)Spend.Clothing]))
                .Fact("dem_online_electronics", () => Share(PopDemandCatalogue.OnlineShare[(int)Spend.Electronics]))
                .Fact("dem_online_groceries", () => Share(PopDemandCatalogue.OnlineShare[(int)Spend.Groceries]))
                .Fact("dem_learner_from", () => Whole(PopDemandCatalogue.LearnerFrom))
                .Fact("dem_learner_to", () => Whole(PopDemandCatalogue.LearnerTo))
                .Fact("dem_approval_points", () => Whole(PopDemandCatalogue.SatisfactionApproval / 10));
        }

        /// <summary>True for a business's page that ends with the pointer here while Living population is on.</summary>
        public static bool Points(string featureId)
        {
            return featureId != null && Array.IndexOf(Pages, featureId) >= 0;
        }

        // A source's weight in a demand, as a whole per cent.
        private static string Weight(PopDemand d, PopSource src)
        {
            (PopSource Source, int Weight)[] recipe = PopDemandCatalogue.Recipe(d);
            for (int i = 0; i < recipe.Length; i++)
            {
                if (recipe[i].Source == src)
                {
                    return Share(recipe[i].Weight);
                }
            }
            return Share(0);
        }

        private static string Share(int thousandths)
        {
            return Whole(thousandths / 10) + "%";
        }

        private static string Whole(int value)
        {
            return value.ToString(Inv);
        }
    }
}
