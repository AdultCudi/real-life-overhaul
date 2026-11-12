using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A niche of a tech market: the businesses of one kind that buy the same software. It exists only if the city can have a business of one
    /// of its types, and it is sized from the businesses the city really has. Saved as a number: never renumber.
    /// </summary>
    public sealed class NicheDef
    {
        public readonly int Id;
        public readonly string[] Types;     // the game's business types its buyers run
        public readonly int Buyers;         // SectorBuyers: businesses, or large organisations
        public readonly int Near;           // one bit per niche: the neighbours a product reuses most of itself in
        public readonly string Good;        // a good whose sales follow the season (SeasonRules.Goods), or null: no season data for this niche
        public readonly bool Rented;        // sized from the buildings that let flats, not from a business type

        public NicheDef(int id, string[] types, int buyers, int near, string good, bool rented = false)
        {
            Id = id;
            Types = types;
            Buyers = buyers;
            Near = near;
            Good = good;
            Rented = rented;
        }
    }

    /// <summary>The figures of niches. Game-free: Phase 5 (rivals, bake offs) reads the same list and rules.</summary>
    public static class NicheCatalogue
    {
        public const int Restaurants = 0, Cafes = 1, Shops = 2, Grocers = 3, Gyms = 4, Clinics = 5, Salons = 6, Offices = 7, Venues = 8, Motoring = 9, Landlords = 10,
            Hospitals = 11, Banks = 12, Industry = 13, Headquarters = 14, Count = 15;

        private static int Bit(int niche) => 1 << niche;

        /// <summary>Every niche, by its id. The types are the game's own (the population's table names the same ones).</summary>
        public static readonly NicheDef[] Defs =
        {
            new NicheDef(Restaurants, new[] { "ba:businesstype_fastfoodrestaurant", "reallife_icecreamparlour" }, (int)SectorBuyers.Businesses,
                Bit(Cafes) | Bit(Grocers) | Bit(Venues), "ba:itemname_pizza"),
            new NicheDef(Cafes, new[] { "ba:businesstype_coffeeshop", "reallife_teabar" }, (int)SectorBuyers.Businesses, Bit(Restaurants) | Bit(Shops), "ba:itemname_cupofcoffee"),
            new NicheDef(Shops, new[]
            {
                "ba:businesstype_clothingstore", "ba:businesstype_electronicsstore", "ba:businesstype_appliancestore", "ba:businesstype_furniturestore", "ba:businesstype_bookstore",
                "ba:businesstype_giftshop", "ba:businesstype_officesupplystore", "ba:businesstype_florist", "ba:businesstype_jewelrystore"
            }, (int)SectorBuyers.Businesses, Bit(Cafes) | Bit(Grocers) | Bit(Salons), "ba:itemname_moderncheapmaleclothing"),
            new NicheDef(Grocers, new[] { "ba:businesstype_supermarket", "ba:businesstype_fruitandvegetablestore", "ba:businesstype_liquorstore", "reallife_tobacconist" },
                (int)SectorBuyers.Businesses, Bit(Restaurants) | Bit(Shops), "ba:itemname_tomato"),
            new NicheDef(Gyms, new[] { "ba:businesstype_gym" }, (int)SectorBuyers.Businesses, Bit(Clinics) | Bit(Salons), null),
            new NicheDef(Clinics, new[] { "ba:businesstype_clinic" }, (int)SectorBuyers.Businesses, Bit(Gyms) | Bit(Salons) | Bit(Hospitals), null),
            new NicheDef(Salons, new[] { "ba:businesstype_hairdresser" }, (int)SectorBuyers.Businesses, Bit(Gyms) | Bit(Clinics) | Bit(Shops), null),
            new NicheDef(Offices, new[]
            {
                "ba:businesstype_lawfirm", "ba:businesstype_travelagency", "ba:businesstype_eventplanningagency", "ba:businesstype_graphicdesigner",
                "ba:businesstype_webdevelopmentagency", "ba:businesstype_marketingagency", "ba:businesstype_recruitmentagency"
            }, (int)SectorBuyers.Businesses, Bit(Landlords) | Bit(Motoring), null),
            new NicheDef(Venues, new[] { "ba:businesstype_cinema", "ba:businesstype_theater", "ba:businesstype_nightclub", "ba:businesstype_casino" },
                (int)SectorBuyers.Businesses, Bit(Restaurants) | Bit(Cafes), "ba:itemname_beer"),
            new NicheDef(Motoring, new[]
            {
                "ba:businesstype_cardealership", "reallife_businesstype_cardealership", "ba:businesstype_truckgarage", "ba:businesstype_gasstation", "ba:businesstype_privatedriverservice",
                "ba:businesstype_movingservice"
            }, (int)SectorBuyers.Businesses, Bit(Offices) | Bit(Industry), null),
            new NicheDef(Landlords, new string[0], (int)SectorBuyers.Businesses, Bit(Offices), null, true),
            new NicheDef(Hospitals, new[] { "ba:businesstype_hospital", "ba:businesstype_school" }, (int)SectorBuyers.Enterprises, Bit(Clinics) | Bit(Banks), null),
            new NicheDef(Banks, new[] { "ba:businesstype_bank", "reallife_bankbranch" }, (int)SectorBuyers.Enterprises, Bit(Hospitals) | Bit(Headquarters), null),
            new NicheDef(Industry, new[]
            {
                "ba:businesstype_factory", "ba:businesstype_warehouse", "ba:businesstype_importexport", "ba:businesstype_wholesalestore"
            }, (int)SectorBuyers.Enterprises, Bit(Headquarters) | Bit(Motoring), null),
            new NicheDef(Headquarters, new[] { "ba:businesstype_headquarters" }, (int)SectorBuyers.Enterprises, Bit(Banks) | Bit(Industry), null)
        };

        /// <summary>Whole text keys, one for each niche: the name.</summary>
        public static readonly string[] NameKeys =
        {
            "reallife_niche_restaurants", "reallife_niche_cafes", "reallife_niche_shops", "reallife_niche_grocers", "reallife_niche_gyms", "reallife_niche_clinics",
            "reallife_niche_salons", "reallife_niche_offices", "reallife_niche_venues", "reallife_niche_motoring", "reallife_niche_landlords", "reallife_niche_hospitals",
            "reallife_niche_banks", "reallife_niche_industry", "reallife_niche_headquarters"
        };

        public static string NameKey(int niche) => NameKeys[niche < 0 ? 0 : niche >= Count ? Count - 1 : niche];

        public static NicheDef Def(int niche) => Defs[niche < 0 ? 0 : niche >= Count ? Count - 1 : niche];

        // ---------------------------------------------------------------- how a niche is sized and how it moves

        public const int PriceSpread = 150;          // thousandths of a niche's going price each doubling of its customers' basket moves it (richer businesses pay more)
        public const int PriceLeast = 800, PriceMost = 1400;   // thousandths of the market's going price a niche's can reach
        // thousandths of a good's seasonal swing that reaches the software its seller buys: software is bought on a slower rhythm than a shop's goods sell
        public const int SeasonShare = 250;
        public const int RentShareLeast = 700, RentShareMost = 1500;   // thousandths: the landlords' demand follows the rents, within these bounds
        public const int HypeShare = 500;            // thousandths of the AI wave's swing that reaches an AI product's niche
        public const int NeutralBasket = 40;         // whole money: a customer's typical basket at an average shop; a niche's price is measured against it
        public const int RivalFloor = 120;           // thousandths of a market's rivals a very small niche still has
        public const int ExpandDays = 15;            // days of the firm's burn an expansion into a neighbouring niche costs
        public const int ExpandRamp = 60;            // days the new niche's buyers take to count in full
        public const int ExpandStart = 500;          // thousandths of the new niche's buyers that count on the day of the expansion
        public const int Active = 3;                 // niches a firm can sell into at once

        /// <summary>The kind of spending a niche's businesses sell, from the population's table; -1 when none of its types is in that table (no data).</summary>
        public static int SpendOf(NicheDef d)
        {
            for (int i = 0; i < d.Types.Length; i++)
            {
                int c = PopulationCatalogue.CategoryOfType(d.Types[i]);
                if (c >= 0)
                {
                    return c;
                }
            }
            return -1;
        }
    }
}
