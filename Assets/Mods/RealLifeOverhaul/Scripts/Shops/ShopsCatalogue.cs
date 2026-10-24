namespace RealLifeOverhaul.Shops
{
    /// <summary>One good a kind of shop sells: the game's item and how much it draws next to the others (the game's impact).</summary>
    public sealed class ShopGood
    {
        public readonly string Item;         // the game's item name, with its "ba:itemname_"
        public readonly int Impact;          // thousandths: 1000 is a primary good, less a good bought on the side

        public ShopGood(string item, int impact)
        {
            Item = item;
            Impact = impact;
        }
    }

    /// <summary>An hour band of a kind's customers: from its hour up to (not including) the next band's, at this share of its best hour.</summary>
    public sealed class ShopHours
    {
        public readonly int From;            // the hour the band starts, 0 to 23
        public readonly int To;              // the hour it ends, 1 to 24
        public readonly int Share;           // thousandths of the busiest hour

        public ShopHours(int from, int to, int share)
        {
            From = from;
            To = to;
            Share = share;
        }
    }

    /// <summary>
    /// One of the new kinds of shop: a business type of the game's that sells the game's own goods from the game's own
    /// furniture, run by the game's own simulation of a shop of the kind it is made like (<see cref="Donor"/>).
    /// </summary>
    public sealed class ShopKind
    {
        public string Type;                  // its saved name and the text key of its name ("reallife_" and its folder)
        public string Donor;                 // the game's type it works as: simulation, customers, requirements, staff, uniforms, logos
        public string Template;              // the game's type it becomes when the save is prepared for removing the mod
        public int Glyph;                    // its icon, ShopsRules.Glyph
        public float Red, Green, Blue;       // the city map filter's colour
        public ShopGood[] Goods;             // in the order the game lists them; the first is the first the tutorial points to
        public string[] Sources;             // where its goods are bought: the game's product sources
        public int MostEach;                 // tenths: the most of one primary good a customer buys (the game's maxAmountPerProduct)
        public int[] Days;                   // thousandths of the busiest day, Monday first
        public ShopHours[] Hours;            // the day's bands, from midnight to midnight
        public string[] Aliases;             // the words the game's search finds it by
        public string[] Shelves;             // the game's furniture its goods are shown on, without "ba:itemname_": its designs keep these
        public string[] Leads;               // of those, the ones that show a primary good: a design is offered with one of them
        public string[] Restock;             // pairs of goods, without "ba:itemname_": a design's shelf set to stock the first stocks the second
    }

    /// <summary>
    /// The new kinds of shop, game-free so the offline checks read the same table: an ice cream parlour, a tea bar and a
    /// tobacconist, each made of goods the game already sells (at its wholesalers and importers, priced by its market and
    /// the city's economy) and furniture it already has. A shop's customers come as the game's own do: its building's
    /// size, its marketing and the street's traffic, its district's demand for each good, the day and hour bands below.
    /// The other kinds players asked for need goods, machines or service stations the game has no model of (see the
    /// feature's page).
    /// </summary>
    public static class ShopsCatalogue
    {
        public const string Prefix = "reallife_";
        public const string IceCream = Prefix + "icecreamparlour";
        public const string TeaBar = Prefix + "teabar";
        public const string Tobacconist = Prefix + "tobacconist";
        public const string HelpTextPrefix = "reallife" + "_shops_help_";   // the locale key of a kind's help page, and its folder (split, so the locale check does not take a prefix for a key)
        public const string BuildingType = "ba:buildingtype_retail";
        public const string Wholesaler = "ba:businessproductsource_wholesaler";
        public const string Importer = "ba:businessproductsource_importer";
        public const string ItemPrefix = "ba:itemname_";
        public const string HelpPrefix = "businesstypes-";   // the game's help page of a business type: its slug, and its title key
        public const int Primary = 1000;                     // the impact of a primary good

        private const string CoffeeShop = "ba:businesstype_coffeeshop";
        private const string FastFood = "ba:businesstype_fastfoodrestaurant";
        private const string LiquorStore = "ba:businesstype_liquorstore";

        public static readonly ShopKind[] Kinds =
        {
            new ShopKind
            {
                Type = IceCream, Donor = CoffeeShop, Template = FastFood, Glyph = 0, Red = 0.93f, Green = 0.45f, Blue = 0.66f,
                Goods = new[]
                {
                    Good("icecream", Primary), Good("slushi", Primary), Good("cottoncandy", 600), Good("cupcake", 500), Good("donut", 500),
                    Good("sodacan", 700), Good("popcorn", 300), Good("cupofcoffee", 300)
                },
                Sources = new[] { Wholesaler }, MostEach = 20,
                Days = new[] { 700, 650, 700, 750, 900, 1000, 1000 },
                Hours = new[]
                {
                    new ShopHours(0, 10, 50), new ShopHours(10, 12, 400), new ShopHours(12, 15, 900), new ShopHours(15, 18, 1000),
                    new ShopHours(18, 21, 850), new ShopHours(21, 23, 400), new ShopHours(23, 24, 50)
                },
                Aliases = new[] { "Ice cream", "Ice cream shop", "Gelato", "Dessert", "Sweets" },
                Shelves = new[] { "icecreamcounter", "slushimachine", "concessionsdisplaycase", "bakeryshowcase", "drinksfridge", "largedrinksfridge", "industrialcoffeemachine" },
                Leads = new[] { "icecreamcounter", "slushimachine" },
                Restock = new string[0]
            },
            new ShopKind
            {
                Type = TeaBar, Donor = CoffeeShop, Template = CoffeeShop, Glyph = 1, Red = 0.20f, Green = 0.62f, Blue = 0.55f,
                Goods = new[]
                {
                    Good("cupoftea", Primary), Good("slushi", Primary), Good("cupcake", 600), Good("donut", 600), Good("icecream", 500),
                    Good("croissant", 400), Good("sodacan", 600), Good("cupofcoffee", 400), Good("cottoncandy", 300)
                },
                Sources = new[] { Wholesaler }, MostEach = 30,
                Days = new[] { 800, 800, 800, 850, 950, 1000, 900 },
                Hours = new[]
                {
                    new ShopHours(0, 8, 50), new ShopHours(8, 11, 350), new ShopHours(11, 14, 700), new ShopHours(14, 18, 1000),
                    new ShopHours(18, 21, 800), new ShopHours(21, 23, 350), new ShopHours(23, 24, 50)
                },
                Aliases = new[] { "Tea", "Bubble tea", "Tea shop", "Boba", "Slush" },
                Shelves = new[] { "industrialcoffeemachine", "slushimachine", "bakeryshowcase", "icecreamcounter", "drinksfridge", "largedrinksfridge", "concessionsdisplaycase" },
                Leads = new[] { "industrialcoffeemachine", "slushimachine" },
                Restock = new[] { "cupofcoffee", "cupoftea" }   // the coffee shop's machines pour tea at a tea bar
            },
            new ShopKind
            {
                Type = Tobacconist, Donor = LiquorStore, Template = LiquorStore, Glyph = 2, Red = 0.55f, Green = 0.38f, Blue = 0.24f,
                Goods = new[]
                {
                    Good("cigarette", Primary), Good("cigar", Primary), Good("energydrink", 800), Good("sodacan", 800), Good("cheapgift", 300)
                },
                Sources = new[] { Importer, Wholesaler }, MostEach = 20,
                Days = new[] { 850, 800, 800, 850, 1000, 950, 700 },
                Hours = new[]
                {
                    new ShopHours(0, 7, 50), new ShopHours(7, 9, 700), new ShopHours(9, 12, 550), new ShopHours(12, 14, 700),
                    new ShopHours(14, 17, 600), new ShopHours(17, 20, 1000), new ShopHours(20, 22, 600), new ShopHours(22, 24, 150)
                },
                Aliases = new[] { "Tobacco", "Vape shop", "Smoke shop", "Cigars", "Cigarettes" },
                Shelves = new[] { "tobaccoshelf1", "tobaccoshelf2", "tobaccoshelf3", "drinksfridge", "largedrinksfridge", "productpanel", "roundedshelf" },
                Leads = new[] { "tobaccoshelf1", "tobaccoshelf2", "tobaccoshelf3" },
                Restock = new string[0]
            }
        };

        /// <summary>The kind of a business type, or null for a type that is not one of these.</summary>
        public static ShopKind Of(string type)
        {
            for (int i = 0; i < Kinds.Length; i++)
            {
                if (Kinds[i].Type == type)
                {
                    return Kinds[i];
                }
            }
            return null;
        }

        /// <summary>True for a business of one of the new kinds.</summary>
        public static bool Is(string type)
        {
            return Of(type) != null;
        }

        private static ShopGood Good(string item, int impact)
        {
            return new ShopGood(ItemPrefix + item, impact);
        }
    }
}
