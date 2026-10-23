using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Meta
{
    /// <summary>Where a feature runs: in any city, or only in the New York the mod knows.</summary>
    public enum CityScope
    {
        AnyCity,    // stands on nothing of New York's own: the phone, money, the character's life, the game's own buildings picked again at each load
        NewYork     // stands on New York's data: buildings it claims, tables by district, the subway's stations, a map measured from it
    }

    /// <summary>
    /// THE CITY TABLE: every feature of Meta/FeatureList, and whether it stands on New York's data. In another city (a
    /// DLC's, Core/CityCheck) every New York feature stands down with the rest of its feature's entry points (its guards
    /// are away, Core/FeatureGuard) and comes back in New York; the others run. A feature is New York's when it claims or
    /// keeps a building of New York's (the precincts, the hotels, the towers, the university, the courthouse, City
    /// Utilities, the offices it converts), keeps a table by New York's districts, runs between the game's subway stations,
    /// or keeps something measured from New York. A feature that only picks from the game's buildings again at every load,
    /// and keeps no place in the save, runs anywhere. A new feature adds its line here: ContractCheck fails while a feature
    /// registered in Meta has none. Game-free.
    /// </summary>
    public static class FeatureCities
    {
        private struct Line
        {
            public readonly string Feature;
            public readonly CityScope Scope;
            public readonly string Why;

            public Line(string feature, CityScope scope, string why)
            {
                Feature = feature;
                Scope = scope;
                Why = why;
            }
        }

        private static readonly Line[] Table =
        {
            new Line("camera", CityScope.AnyCity, "the camera behind the player"),
            new Line("character", CityScope.AnyCity, "the player's looks and clothes"),
            new Line("citylife", CityScope.AnyCity, "the game's own pedestrians and benches"),
            new Line("crime", CityScope.NewYork, "arrests and stops go to New York's precincts"),
            new Line("driving", CityScope.AnyCity, "the cars' handling"),
            new Line("jobs", CityScope.AnyCity, "places picked from the game's buildings at each load"),
            new Line("life", CityScope.AnyCity, "the character's life"),
            new Line("people", CityScope.NewYork, "officers and patrols of New York's precincts"),
            new Line("phone", CityScope.AnyCity, "the phone and its contacts"),
            new Line("phonefolders", CityScope.AnyCity, "the phone's home screen"),
            new Line("ragdoll", CityScope.AnyCity, "people hit by cars"),
            new Line("traffic", CityScope.AnyCity, "the game's own traffic"),
            new Line("streets", CityScope.AnyCity, "the game's own drivers"),
            new Line("visuals", CityScope.AnyCity, "the picture"),
            new Line("keys", CityScope.AnyCity, "the mod's keys"),
            new Line("performance", CityScope.AnyCity, "how the game draws any city"),
            new Line("savetools", CityScope.AnyCity, "the save's own tools"),
            new Line("diagnostics", CityScope.AnyCity, "Player.log and the tests"),
            new Line("multiplayer", CityScope.AnyCity, "Going Public"),
            new Line("tests", CityScope.AnyCity, "the test buttons"),
            new Line("economy", CityScope.AnyCity, "credit, loans and the Finance app"),
            new Line("economy_city", CityScope.NewYork, "the city economy's districts are New York's"),
            new Line("ventures", CityScope.AnyCity, "investors in the player's businesses"),
            new Line("markets", CityScope.AnyCity, "the mod's own stock market"),
            new Line("economy_banks", CityScope.AnyCity, "the Banks app"),
            new Line("economy_company", CityScope.AnyCity, "the player's businesses"),
            new Line("economy_rota", CityScope.AnyCity, "the player's staff"),
            new Line("economy_managers", CityScope.AnyCity, "the player's managers, by the game's own districts"),
            new Line("economy_estate", CityScope.AnyCity, "the player's buildings"),
            new Line("economy_online", CityScope.AnyCity, "the player's web shops, by the game's own districts"),
            new Line("tech_people", CityScope.AnyCity, "the player's professionals"),
            new Line("tech_jobs", CityScope.AnyCity, "the jobs board"),
            new Line("tech_sector", CityScope.AnyCity, "the player's tech firms"),
            new Line("tech_startups", CityScope.AnyCity, "startups and funds"),
            new Line("tech_sites", CityScope.NewYork, "the shared towers it claims in New York"),
            new Line("economy_dealer", CityScope.AnyCity, "the player's showroom"),
            new Line("economy_cleaning", CityScope.AnyCity, "the player's cleaning service"),
            new Line("economy_sale", CityScope.AnyCity, "selling the player's business"),
            new Line("office_space", CityScope.NewYork, "New York's empty shops it converts and holds"),
            new Line("economy_brands", CityScope.AnyCity, "the player's brands, by the game's own districts"),
            new Line("home", CityScope.AnyCity, "the player's home life"),
            new Line("law", CityScope.NewYork, "a precinct claimed in each of New York's districts"),
            new Line("transport", CityScope.NewYork, "buses between the game's subway stations of New York"),
            new Line("taxi", CityScope.AnyCity, "the game's own taxis"),
            new Line("leisure", CityScope.AnyCity, "the building the player is in, landmarks only where the game's stations name them"),
            new Line("events", CityScope.AnyCity, "places picked from the game's buildings at each load"),
            new Line("social", CityScope.AnyCity, "people and outings at the game's businesses"),
            new Line("ledger", CityScope.AnyCity, "the paper and kiosks picked at each load"),
            new Line("flare", CityScope.AnyCity, "the phone app; its district posts only in New York (Social/Flare)"),
            new Line("standing", CityScope.AnyCity, "the player's name"),
            new Line("travel", CityScope.NewYork, "a hotel claimed in each of New York's districts"),
            new Line("careers", CityScope.NewYork, "New York's employers and the university it claims (one director)"),
            new Line("careers_study", CityScope.NewYork, "the school and Hudson City University it claims in New York"),
            new Line("careers_degrees", CityScope.NewYork, "Hudson City University"),
            new Line("careers_family", CityScope.NewYork, "the careers director"),
            new Line("careers_jobs", CityScope.NewYork, "the careers director"),
            new Line("careers_life", CityScope.NewYork, "the careers director"),
            new Line("careers_own", CityScope.NewYork, "the careers director"),
            new Line("business", CityScope.AnyCity, "the player's businesses"),
            new Line("garage", CityScope.AnyCity, "the game's repair bays and shops picked at each load"),
            new Line("justice", CityScope.NewYork, "the courthouse it claims in New York"),
            new Line("licences", CityScope.AnyCity, "the driving school picked at each load"),
            new Line("planner", CityScope.AnyCity, "the Planner app"),
            new Line("navigation", CityScope.NewYork, "the minimap's base measured from New York and kept on file"),
            new Line("civic", CityScope.NewYork, "wards of New York's districts and City Utilities' building"),
            new Line("utilities", CityScope.NewYork, "broadband and internet by New York's districts, the Utilities office"),
            new Line("family", CityScope.AnyCity, "the player's family"),
            new Line("permadeath", CityScope.AnyCity, "the player's ending"),
            new Line("photos", CityScope.AnyCity, "the Camera and Gallery apps"),
            new Line("rivals", CityScope.AnyCity, "the rival tycoons' figures"),
            new Line("street", CityScope.AnyCity, "the game's own pedestrians"),
            new Line("texts", CityScope.AnyCity, "RLO Texts"),
            new Line("textsai", CityScope.AnyCity, "Your own AI"),
            new Line("walking", CityScope.AnyCity, "the player's walk"),
            new Line("calendar", CityScope.AnyCity, "the Calendar app"),
            new Line("designs", CityScope.AnyCity, "the installation firms' designs"),
            new Line("districtview", CityScope.NewYork, "the district view's layers of New York's districts"),
            new Line("economy_bank_branch", CityScope.AnyCity, "the player's bank's branches"),
            new Line("economy_ownbank", CityScope.AnyCity, "the player's bank"),
            new Line("economy_endscale", CityScope.AnyCity, "the end game's figures"),
            new Line("faults", CityScope.NewYork, "faults kept in New York's streets and districts"),
            new Line("home_roommates", CityScope.AnyCity, "the player's home"),
            new Line("interiors", CityScope.AnyCity, "the player's interiors, by the game's own district data"),
            new Line("interiors_standard", CityScope.AnyCity, "the game's own district data"),
            new Line("interiors_decor", CityScope.AnyCity, "the player's decor"),
            new Line("markets_derivatives", CityScope.AnyCity, "the raw materials exchange"),
            new Line("markets_listing", CityScope.AnyCity, "the player's company on the market"),
            new Line("markets_bots", CityScope.AnyCity, "the market's orders"),
            new Line("meals", CityScope.AnyCity, "meals and music"),
            new Line("newground", CityScope.NewYork, "new ground laid on New York's map, against its walking meshes, road grid and lanes"),
            new Line("newbuildings", CityScope.NewYork, "buildings of the mod's own on lots measured from New York's map"),
            new Line("uptown", CityScope.NewYork, "a district north of New York's Manhattan, laid against its streets, walking meshes and backdrop"),
        };

        private static Dictionary<string, Line> _byFeature;

        /// <summary>The features the table names, in its order.</summary>
        public static IEnumerable<string> Named
        {
            get
            {
                for (int i = 0; i < Table.Length; i++)
                {
                    yield return Table[i].Feature;
                }
            }
        }

        /// <summary>True when the table names the feature.</summary>
        public static bool Has(string feature)
        {
            return feature != null && Index().ContainsKey(feature);
        }

        /// <summary>Where the feature runs; a feature the table does not name runs anywhere (ContractCheck makes sure there is none).</summary>
        public static CityScope Of(string feature)
        {
            return feature != null && Index().TryGetValue(feature, out Line line) ? line.Scope : CityScope.AnyCity;
        }

        /// <summary>What the feature stands on, in English for the check and Player.log; null for a feature not named.</summary>
        public static string Why(string feature)
        {
            return feature != null && Index().TryGetValue(feature, out Line line) ? line.Why : null;
        }

        /// <summary>The New York features, in the table's order, into the caller's list (cleared first).</summary>
        public static List<string> NewYork(List<string> into)
        {
            into.Clear();
            for (int i = 0; i < Table.Length; i++)
            {
                if (Table[i].Scope == CityScope.NewYork)
                {
                    into.Add(Table[i].Feature);
                }
            }
            return into;
        }

        /// <summary>Features named twice, for the check; empty when each has one line.</summary>
        public static List<string> Twice()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var twice = new List<string>();
            for (int i = 0; i < Table.Length; i++)
            {
                if (!seen.Add(Table[i].Feature))
                {
                    twice.Add(Table[i].Feature);
                }
            }
            return twice;
        }

        private static Dictionary<string, Line> Index()
        {
            if (_byFeature == null)
            {
                var index = new Dictionary<string, Line>(Table.Length, StringComparer.Ordinal);
                for (int i = 0; i < Table.Length; i++)
                {
                    index[Table[i].Feature] = Table[i];
                }
                _byFeature = index;
            }
            return _byFeature;
        }
    }
}
