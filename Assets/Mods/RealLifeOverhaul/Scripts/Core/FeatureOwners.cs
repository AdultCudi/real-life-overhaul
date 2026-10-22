using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The Meta/FeatureList feature a class of the mod belongs to, by its name and namespace, for a class with no guard
    /// naming one: asked for a handler's class (Guard.OfHandler) and by Core/ModScan, and by ContractCheck the same way.
    /// Older folders share the root namespace, so their directors are named one by one; any other class is placed by
    /// the classes that use it (Core/ModScan). Game-free.
    /// </summary>
    public static class FeatureOwners
    {
        // Directors whose feature is not their namespace's.
        private static readonly Dictionary<string, string> ByName = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "CityEconomyDirector", "economy_city" }, { "CompanyDirector", "economy_company" }, { "EstateDirector", "economy_estate" },
            { "OnlineDirector", "economy_online" }, { "OwnBankDirector", "economy_ownbank" }, { "ServicesDirector", "economy_cleaning" },
            { "BanksDirector", "economy_banks" }, { "BrandsDirector", "economy_brands" }, { "DealerDirector", "economy_dealer" },
            { "EconomyDirector", "economy" }, { "VenturesDirector", "ventures" }, { "ListingDirector", "markets_listing" },
            { "DistrictView", "districtview" }, { "MapPlaces", "navigation" }, { "HomeDirector", "home" }, { "CleanerJob", "jobs" },
            { "JobDirector", "jobs" }, { "BusinessDirector", "business" }, { "CivicDirector", "civic" }, { "EventDirector", "events" },
            { "GarageDirector", "garage" }, { "JusticeDirector", "justice" }, { "LicenceDirector", "licences" }, { "SocialDirector", "social" },
            { "TravelDirector", "travel" }, { "TransitDirector", "transport" }, { "CarOwnership", "transport" }, { "TaxiDirector", "taxi" },
            { "LifeEvents", "life" }, { "MarketDirector", "markets" }, { "LedgerDirector", "ledger" }, { "CareersDirector", "careers_study" },
            { "TechSites", "tech_sites" },
        };

        // The rest by namespace: RealLifeOverhaul.X.
        private static readonly Dictionary<string, string> ByNamespace = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "RealLifeOverhaul.Economy", "economy" }, { "RealLifeOverhaul.Economy.Ventures", "ventures" }, { "RealLifeOverhaul.Economy.Brands", "economy_brands" },
            { "RealLifeOverhaul.Economy.Dealership", "economy_dealer" }, { "RealLifeOverhaul.Tech", "tech_sector" }, { "RealLifeOverhaul.Markets", "markets" },
            { "RealLifeOverhaul.Ledger", "ledger" }, { "RealLifeOverhaul.Family", "family" }, { "RealLifeOverhaul.Leisure", "leisure" },
            { "RealLifeOverhaul.Utilities", "utilities" }, { "RealLifeOverhaul.Flare", "flare" }, { "RealLifeOverhaul.Fame", "standing" },
            { "RealLifeOverhaul.Law", "law" }, { "RealLifeOverhaul.Roommates", "home_roommates" }, { "RealLifeOverhaul.Nav", "navigation" },
            { "RealLifeOverhaul.Events", "events" }, { "RealLifeOverhaul.Permadeath", "permadeath" }, { "RealLifeOverhaul.Planner", "planner" },
            { "RealLifeOverhaul.Districts", "districtview" }, { "RealLifeOverhaul.Careers", "careers_study" }, { "RealLifeOverhaul.Interiors", "interiors" },
            { "RealLifeOverhaul.Rivals", "rivals" }, { "RealLifeOverhaul.Photos", "photos" }, { "RealLifeOverhaul.Walking", "walking" },
            { "RealLifeOverhaul.Calendar", "calendar" }, { "RealLifeOverhaul.Designs", "designs" }, { "RealLifeOverhaul.Meals", "meals" },
        };

        // Namespaces that are the mod's own machinery, whatever a class of them is called.
        private static readonly HashSet<string> Machinery = new HashSet<string>(StringComparer.Ordinal)
        {
            "RealLifeOverhaul.GameUi", "RealLifeOverhaul.Meta", "RealLifeOverhaul.FitOut",
        };

        /// <summary>
        /// The feature of a class (a nested class's outermost name), or null. <paramref name="known"/> is false when the
        /// class must be placed by the classes that use it; true for a feature or the mod's machinery (null).
        /// </summary>
        public static string Of(string name, string ns, out bool known)
        {
            if (name != null && ByName.TryGetValue(name, out string feature))
            {
                known = true;
                return feature;
            }
            if (ns != null && ByNamespace.TryGetValue(ns, out feature))
            {
                known = true;
                return feature;
            }
            known = ns != null && Machinery.Contains(ns);
            return null;
        }

        /// <summary>As <see cref="Of(string, string, out bool)"/>, for a handler's class: null when it is not known.</summary>
        public static string Of(string name, string ns)
        {
            return Of(name, ns, out _);
        }
    }
}
