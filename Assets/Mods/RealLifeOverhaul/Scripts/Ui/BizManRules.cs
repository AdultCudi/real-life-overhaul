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

        /// <summary>The mod's own Loyalty tab (Loyalty/LoyaltyTab.Id), a child of the tab bar the mod made; drawn as the Interior tab is, a page written as lines.</summary>
        public const string Loyalty = "RealLifeLoyalty";

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
                case Interiors: case Loyalty: return BizManPart.Interior;
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
                case Loyalty: return UiIcon.IdCard;
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

        /// <summary>
        /// How the open switch reaches the game's own "temporarily closed" toggle. The game keeps the toggle in its header and
        /// switches the header off on the building's and the real estate's tabs (BizManBusiness.SetTab), where the kit's head
        /// still shows the switch: the toggle and its row are on there, so it is set all the same and its own listener runs the
        /// game's opening or closing. A toggle or a row the game itself switched off is no switch, one it holds is never set, and
        /// one hidden by anything but the game's own tab (a Going Public session's lock may hide a control) is no switch either.
        /// </summary>
        public static BizManSwitchWay SwitchWay(bool there, bool rowOn, bool selfOn, bool shownNow, bool interactable, bool headerOffByTab)
        {
            if (!there || !rowOn || !selfOn || (!shownNow && !headerOffByTab))
            {
                return BizManSwitchWay.None;
            }
            if (!interactable)
            {
                return BizManSwitchWay.Held;
            }
            return shownNow ? BizManSwitchWay.Click : BizManSwitchWay.Under;
        }

        /// <summary>
        /// Why a business open by its switch will not trade yet, for Player.log (in English, as every line of it): requirements of
        /// the game's still missing, no opening hours on any day or none today, a station the game wants someone at. Null when
        /// nothing stands in its way. The game lets a business be opened in any of these states and says none of them at the
        /// switch, so a report of "it will not open" is answered by this line.
        /// </summary>
        public static string NotTrading(int needs, int missing, string firstMissing, bool anyDay, bool today, bool staffMissing)
        {
            var why = new System.Text.StringBuilder(128);
            if (missing > 0)
            {
                why.Append(missing).Append(" of the game's ").Append(needs).Append(" requirement(s) missing (the first: ").Append(string.IsNullOrEmpty(firstMissing) ? "unnamed" : firstMissing)
                    .Append(')');
            }
            if (!anyDay || !today)
            {
                why.Append(why.Length > 0 ? "; " : string.Empty).Append(!anyDay ? "no day of its week has opening hours" : "it keeps no opening hours today");
            }
            if (staffMissing)
            {
                why.Append(why.Length > 0 ? "; " : string.Empty).Append("the game's own alert says a station it needs has nobody at it");
            }
            return why.Length > 0 ? why.ToString() : null;
        }

        /// <summary>The sentence the page says when a press came to nothing, by why: each names what was pressed as {action}.</summary>
        public static string RefusalKey(BizManRefusal why)
        {
            switch (why)
            {
                case BizManRefusal.Gone: return "reallife_ui_bizman_refused_gone";
                case BizManRefusal.Held: return "reallife_ui_bizman_refused_held";
                default: return "reallife_ui_bizman_refused_fault";
            }
        }

        /// <summary>The same, for Player.log.</summary>
        public static string RefusalSaid(BizManRefusal why)
        {
            switch (why)
            {
                case BizManRefusal.Gone: return "the game does not show its own control for it now";
                case BizManRefusal.Held: return "the game holds its own control for it now";
                default: return "no part of the page answered it, a fault of the RLO interface";
            }
        }

        /// <summary>
        /// True for a target that says in words what it does: a page's action, a button of a page written as lines, the head's
        /// action, the open switch. A press of one that nothing answered is the page's own fault, and the page says so. Any other
        /// target may have nothing to do where it stands (a chart's point, the tab of a page that has one page), so a press of
        /// one that comes to nothing with no reason known keeps to the error cue and is never called a fault.
        /// </summary>
        public static bool Promises(int command) =>
            command == BizManPages.Action || command == BizManPages.LineAction || command == UiFirmHead.First + UiFirmHead.Action || command == UiFirmHead.First + UiFirmHead.Switch;
    }

    /// <summary>How the open switch's press reaches the game's toggle (BizManRules.SwitchWay).</summary>
    public enum BizManSwitchWay : byte
    {
        None = 0,   // the game gives this place no switch
        Held,       // the game holds its toggle: refused, and said
        Click,      // the toggle shows: set as a click sets it
        Under       // only the header it stands in is switched off for the tab shown: set all the same
    }

    /// <summary>Why a press on a BizMan page came to nothing: the page says each in words, and Player.log once.</summary>
    public enum BizManRefusal : byte
    {
        Fault = 0,  // nothing answered the press: the kit's own fault
        Gone,       // the game does not show its control for it now
        Held        // the game holds its control for it now
    }
}
