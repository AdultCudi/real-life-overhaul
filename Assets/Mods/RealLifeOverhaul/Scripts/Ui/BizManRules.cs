using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// BizMan's rules on the kit, game-free and checked offline: which of the game's tabs the kit draws and which open the
    /// game's own view (another mod's, the operations while their switch is off), each tab's and each kind of business's glyph (one glyph a role, its
    /// colour its own), which children of the game's tab bar are the game's (any other is another mod's), and the step an
    /// amount takes on a stepper. Cost: a few comparisons, at a layout or a press only.
    /// </summary>
    public static class BizManRules
    {
        // The game's tabs, by the names of their buttons and pages (BizMan's constants).
        public const string Presentation = "Presentation", Insight = "Insight", Pricing = "InventoryPricing", Schedule = "Schedule", Deliveries = "Deliveries",
            Marketing = "Marketing", Settings = "Settings", RealEstate = "RealEstate", Drivers = "Drivers", Inventory = "Inventory", Factory = "Factory",
            PricingManagers = "PricingManagers", LogisticsManagers = "LogisticsManagers", PurchasingAgents = "PurchasingAgents", HrManagers = "HRManagers",
            Headhunters = "Headhunters";

        /// <summary>
        /// The game's sortable heads of the business list, one a column of the kit's table (null where the game has none to
        /// press: the district and the set destination column at the end).
        /// </summary>
        public static readonly string[] ShopHeads = { "BusinessName", null, "AvgDailyIncome", "Satisfaction", "Alerts", "Status", null };

        /// <summary>The game's sortable heads of a store's prices, one a column of the kit's table.</summary>
        public static readonly string[] PriceHeads = { "ProductName", "SoldLastWeek", "StockCount", "MarketPrice", "RetailPriceSortField" };

        /// <summary>
        /// The game's head over a column of the kit's table, or null for none: a column past the game's heads (the kit's own,
        /// such as set destination) has none, never an index past the names.
        /// </summary>
        public static string HeadOf(string[] names, int column) => names != null && column >= 0 && column < names.Length ? names[column] : null;

        /// <summary>The mod's own Interior tab (Interiors/InteriorTab.Id), a child of the tab bar the mod made.</summary>
        public const string Interiors = "RealLifeInteriors";

        /// <summary>The names the game gives the children of BizMan's tab bar: anything else there is another mod's.</summary>
        public static readonly string[] GameTabs =
        {
            Presentation, Insight, Pricing, Schedule, Deliveries, Marketing, Settings, RealEstate, Drivers, Inventory, Factory, PricingManagers, LogisticsManagers,
            PurchasingAgents, HrManagers, Headhunters
        };

        /// <summary>
        /// The part the kit draws for a game's tab, or Classic for a tab only the game's own view shows. The operations (the
        /// schedule, the managers, the factory, the warehouse's tabs) are drawn while their own switch in Mod settings is on
        /// (ops), so a player who meets a broken one hands those tabs alone back to the game while the rest stays on the kit.
        /// </summary>
        public static BizManPart PartOf(string tab, bool ops)
        {
            switch (tab)
            {
                case Schedule: return ops ? BizManPart.Schedule : BizManPart.Classic;
                case PricingManagers: case LogisticsManagers: case PurchasingAgents: case HrManagers: case Headhunters:
                    return ops ? BizManPart.Managers : BizManPart.Classic;
                case Factory: return ops ? BizManPart.Factory : BizManPart.Classic;
                case Drivers: return ops ? BizManPart.Drivers : BizManPart.Classic;
                case Inventory: return ops ? BizManPart.Stock : BizManPart.Classic;
                case Presentation: return BizManPart.Building;
                case Insight: return BizManPart.Insight;
                case Pricing: return BizManPart.Pricing;
                case Deliveries: return BizManPart.Deliveries;
                case Marketing: return BizManPart.Marketing;
                case Settings: return BizManPart.Settings;
                case RealEstate: return BizManPart.Estate;
                case Interiors: return BizManPart.Interior;
                default: return BizManPart.Classic;
            }
        }

        /// <summary>True for a tab the kit draws; false for one whose page the game's own view shows (another mod's, or the operations with their switch off).</summary>
        public static bool Drawn(string tab, bool ops) => PartOf(tab, ops) != BizManPart.Classic;

        /// <summary>A tab's glyph, one a role; another mod's tab wears the glyph of the phone's other mods.</summary>
        public static UiIcon TabIcon(string tab)
        {
            switch (tab)
            {
                case Presentation: return UiIcon.Image;
                case Insight: return UiIcon.ChartLineUp;
                case Pricing: return UiIcon.Tag;
                case Schedule: return UiIcon.Calendar;
                case Deliveries: return UiIcon.Truck;
                case Marketing: return UiIcon.Megaphone;
                case Settings: return UiIcon.Gear;
                case RealEstate: return UiIcon.Key;
                case Drivers: return UiIcon.Car;
                case Inventory: return UiIcon.Database;
                case Factory: return UiIcon.Factory;
                case PricingManagers: return UiIcon.Percent;
                case LogisticsManagers: return UiIcon.Rocket;
                case PurchasingAgents: return UiIcon.ShoppingCart;
                case HrManagers: return UiIcon.People;
                case Headhunters: return UiIcon.IdBadge;
                case Interiors: return UiIcon.Sofa;
                default: return UiIcon.Code;
            }
        }

        /// <summary>The glyph of a kind of business, by the game's name for it: its family's (food, retail, office, industry, tech, services).</summary>
        public static UiIcon KindIcon(string businessType, string buildingType)
        {
            switch (Plain(businessType))
            {
                case "coffeeshop": case "bakery": case "juicebar":
                    return UiIcon.Coffee;
                case "fastfoodrestaurant": case "restaurant": case "bar":
                    return UiIcon.ForkKnife;
                case "nightclub":
                    return UiIcon.MusicNotes;
                case "supermarket": case "fruitandvegetablestore": case "liquorstore":
                    return UiIcon.ShoppingCart;
                case "clothingstore": case "officesupplystore":
                    return UiIcon.ShoppingBag;
                case "giftshop":
                    return UiIcon.Tag;
                case "jewelrystore":
                    return UiIcon.Star;
                case "bookstore":
                    return UiIcon.Newspaper;
                case "florist":
                    return UiIcon.Sun;
                case "furniturestore":
                    return UiIcon.Sofa;
                case "appliancestore":
                    return UiIcon.Lightning;
                case "cardealership":
                    return UiIcon.Car;
                case "electronicsstore": case "webdevelopmentagency":
                    return UiIcon.Cpu;
                case "lawfirm": case "recruitmentagency": case "graphicdesigner": case "travelagency": case "eventplanningagency": case "marketingagency":
                    return UiIcon.Briefcase;
                case "headquarters":
                    return UiIcon.Presentation;
                case "warehouse": case "importexport": case "wholesalestore":
                    return UiIcon.Package;
                case "factory":
                    return UiIcon.Factory;
                case "gym":
                    return UiIcon.Heartbeat;
                case "hairdresser": case "cinema": case "theater": case "casino":
                    return UiIcon.Trophy;
                case "movingservice": case "truckgarage": case "interiorinstallationfirm": case "gasstation":
                    return UiIcon.Truck;
                case "empty": case "":
                    return Plain(buildingType) == "residential" ? UiIcon.Home : UiIcon.MapPin;   // an empty building is a place, not yet a kind
                default:
                    return UiIcon.Store;
            }
        }

        // The game's name without its "ba:businesstype_" or "ba:buildingtype_" prefix.
        private static string Plain(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            int cut = name.LastIndexOf('_');
            return cut >= 0 ? name.Substring(cut + 1) : name;
        }

        /// <summary>
        /// The step an amount of money takes on a stepper: about a hundredth of it, rounded to one significant figure (a
        /// $245,000 offer steps by $2,000, a $3.40 price by 5 cents), never less than <paramref name="least"/>.
        /// </summary>
        public static double Step(double amount, double least)
        {
            double basis = Math.Abs(amount) / 100.0;
            if (basis <= least)
            {
                return least;
            }
            double unit = Math.Pow(10.0, Math.Floor(Math.Log10(basis)));
            double lead = basis / unit;
            double nice = lead < 2.0 ? 1.0 : lead < 5.0 ? 2.0 : 5.0;
            return Math.Max(least, nice * unit);
        }

        /// <summary>An amount stepped once up or down onto the step's next mark that way, never below nothing.</summary>
        public static double Stepped(double amount, int direction, double least)
        {
            double step = Step(amount, least);
            double marks = direction > 0 ? Math.Floor(amount / step + 1e-9) + 1.0 : Math.Ceiling(amount / step - 1e-9) - 1.0;
            return Math.Max(0.0, Math.Round(marks * step, 2));
        }

        /// <summary>
        /// Which offer for the building the game offers: its for-sale box with the button to make an offer, else its not-for-sale
        /// box with its own. The game keeps the box that holds the amount and Send shut until that button is pressed, so the
        /// offer is read from the two boxes and their buttons, never from Send. 0 for none, 1 for the for-sale box, 2 for the other.
        /// </summary>
        public static int BuildingOffer(bool forSale, bool forSaleButton, bool notForSale, bool notForSaleButton) =>
            forSale && forSaleButton ? 1 : notForSale && notForSaleButton ? 2 : 0;

        /// <summary>
        /// True when the game holds a Send: it shows now and cannot be pressed (Going Public's locks, a missing diploma). A Send the
        /// game keeps shut behind its make-an-offer button is not held: the method it calls is what runs, and it answers with its own notice.
        /// </summary>
        public static bool SendHeld(bool shown, bool interactable) => shown && !interactable;
    }
}
