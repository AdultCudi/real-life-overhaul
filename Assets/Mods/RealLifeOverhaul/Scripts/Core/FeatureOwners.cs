using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The feature of Meta/FeatureList a class of the mod belongs to, by its name and namespace, for a class that has no
    /// guard of its own naming one (a component's FrameGuard does, Core/Guard). The running mod asks it for a handler's
    /// class (Guard.OfHandler) and, as it starts, for each class of the mod that names a piece of the game this game
    /// lacks (Core/ModScan); ContractCheck asks it the same, so that what it prints is what the running mod does.
    ///
    /// Most of the mod's older folders share the root namespace, so their classes are named here one by one where it
    /// matters (the directors whose feature is not their namespace's); any other class is found through the classes that
    /// use it (Core/ModScan). Null: the mod's own machinery. No Unity or game types.
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
            { "RealLifeOverhaul.Rivals", "rivals" }, { "RealLifeOverhaul.Photos", "photos" }, { "RealLifeOverhaul.Walking", "walking" }, { "RealLifeOverhaul.Designs", "designs" },
        };

        // Namespaces that are the mod's own machinery, whatever a class of them is called.
        private static readonly HashSet<string> Machinery = new HashSet<string>(StringComparer.Ordinal)
        {
            "RealLifeOverhaul.GameUi", "RealLifeOverhaul.Meta", "RealLifeOverhaul.FitOut",
        };

        /// <summary>
        /// The feature of a class of the mod (its simple name and namespace; a nested class is its outermost class's), or
        /// null when it is not known from these alone. <paramref name="known"/> is true when the answer is final: a
        /// feature, or the mod's machinery (null); false when the class must be placed by the classes that use it.
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
