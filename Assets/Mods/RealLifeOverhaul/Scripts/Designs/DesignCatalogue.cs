using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;

namespace RealLifeOverhaul.Designs
{
    /// <summary>How a business type's ready-made designs are made from the game's own.</summary>
    public enum DesignWay
    {
        Copy,       // the game's own designs of a business that works as this one does, whole
        Showroom,   // the game's own designs with the middle of the floor cleared for the Car Showroom's lot
        Branch      // the game's own shop designs cut to a bank's furniture: a till on a counter, seats to wait on
    }

    /// <summary>Where the game keeps a kind of design it ships.</summary>
    public enum DonorRoot
    {
        Installer,  // StreamingAssets/InteriorDesigns/&lt;building&gt;/&lt;size&gt;/&lt;business&gt;: what the installation firms offer
        Layouts     // StreamingAssets/BusinessLayouts/&lt;business&gt;/&lt;size&gt;: how the game furnishes its own businesses
    }

    /// <summary>One kind of the game's designs a business type's are made from.</summary>
    public sealed class Donor
    {
        public readonly DonorRoot Root;
        public readonly string Business;     // the folder of the game's business, as the game names it on disk

        public Donor(DonorRoot root, string business)
        {
            Root = root;
            Business = business;
        }
    }

    /// <summary>One business type of the mod's and the designs the installation firms offer it.</summary>
    public sealed class DesignKind
    {
        public readonly string Type;         // the business type's saved name (Tech/Firms/TechTypes, Careers/AcademyType, Economy/Dealer)
        public readonly string Building;     // the game's building type it is founded in
        public readonly DesignWay Way;
        public readonly Donor[] Donors;      // in order: on a name two of them share, the first's design is the one offered

        public DesignKind(string type, string building, DesignWay way, params Donor[] donors)
        {
            Type = type;
            Building = building;
            Way = way;
            Donors = donors;
        }
    }

    /// <summary>
    /// The mod's business types' ready-made designs, game-free for the checks: their sources, what a showroom or branch
    /// keeps, and the bounds. The firm lists a type's designs from a folder named after it. Prices stay the game's.
    /// </summary>
    public static class DesignCatalogue
    {
        public const int RulesVersion = 2;          // raise when what a design is made of changes: every design is made again
        public const string Office = "ba:buildingtype_office";
        public const string Retail = "ba:buildingtype_retail";
        public const string Installer = "InteriorDesigns";   // under StreamingAssets: the installation firms' designs
        public const string Layouts = "BusinessLayouts";     // under StreamingAssets: the game's own businesses' layouts
        public const string Manifest = "reallife_designs.txt";   // in the installers' folder: every file the mod put there
        public const string Extension = ".json";

        // The Car Showroom's lot, as DealerRules.Layout lays the cars out.
        public const int Slack = 30;                // centimetres the game's floor may be wider, narrower or further along than its designs reach
        public const int Clear = 40;                // centimetres kept clear round every car's place, for the visitors walking round it
        public const int FloorTop = 200;            // centimetres up: an item higher than this hangs on a wall or the ceiling, above the cars
        public const int Origin = 100;              // centimetres: an item this close to the world's origin is a design's stray, not in the room
        public const int ShowroomMost = 4;          // designs a Car Showroom is offered for a size at most
        public const int ShowroomLeast = 6;         // pieces of furniture, lamps aside, a showroom design keeps at least to be offered

        /// <summary>The styles the game's dealership layouts' names end in; those layouts are offered under them.</summary>
        public static readonly string[] DealerStyles = { "Cheap", "Luxury" };

        public static readonly DesignKind[] Kinds =
        {
            new DesignKind("reallife:businesstype_softwarestudio", Office, DesignWay.Copy, Offered("WebDevelopmentAgency")),
            new DesignKind("reallife:businesstype_gamestudio", Office, DesignWay.Copy, Offered("GraphicDesigner"), Offered("WebDevelopmentAgency")),
            new DesignKind("reallife:businesstype_itconsultancy", Office, DesignWay.Copy, Offered("WebDevelopmentAgency"), Offered("LawFirm")),
            new DesignKind("reallife:businesstype_cybersecurity", Office, DesignWay.Copy, Offered("WebDevelopmentAgency")),
            new DesignKind("reallife:businesstype_ailab", Office, DesignWay.Copy, Offered("WebDevelopmentAgency")),
            new DesignKind("reallife:businesstype_academy", Office, DesignWay.Copy, Offered("travelagency"), Offered("eventplanningagency"), Offered("LawFirm")),
            new DesignKind(BranchCatalogue.TypeName, Retail, DesignWay.Branch,
                Offered("JewelryStore"), Offered("Hairdresser"), Offered("ElectronicsStore"), Offered("Bookstore"), Offered("LiquorStore"), Offered("GiftShop"),
                Offered("Florist"), Offered("ClothingStore"), Offered("CoffeeShop"), Offered("Gym"), Offered("FruitAndVegetableStore"), Offered("Supermarket")),
            new DesignKind("reallife:businesstype_construction", Office, DesignWay.Copy, Offered("GraphicDesigner"), Offered("LawFirm")),
            new DesignKind(DealerCatalogue.TypeName, Retail, DesignWay.Showroom,
                new Donor(DonorRoot.Layouts, "CarDealership"), Offered("JewelryStore"), Offered("ElectronicsStore"), Offered("LiquorStore"), Offered("Bookstore"),
                Offered("ClothingStore"), Offered("GiftShop"), Offered("Florist"), Offered("Hairdresser"), Offered("CoffeeShop"), Offered("FruitAndVegetableStore"),
                Offered("FastFoodRestaurant"), Offered("Nightclub"), Offered("Supermarket"), Offered("Gym"))
        };

        /// <summary>What a Car Showroom keeps, by item name without "ba:itemname_": no shelves, cases or cars on show, as it sells its lot's.</summary>
        public static readonly string[] ShowroomKeeps =
        {
            "cashregister", "counter1", "counter2", "countercorner", "countercorneroutermodern", "moderncounter", "countermarble01",
            "counterendleftmarble", "counterendrightmarble",
            "armchair1", "armchair2", "cornersofa01", "cornersofa02left", "cornersofa02right", "modularsofa1l", "modularsofa1m",
            "modularsofa1r", "multipurposechair", "officechair", "officechair2", "regularchair", "sofawithblanket", "valenciachair",
            "valenciaottoman", "waitingroomchairs",
            "coffeetable1", "coffeetable2", "coffeetable3", "coffeetableluxurycircle", "coffeetableluxurywooden", "roundtable", "table1",
            "officedesk1", "officedesk2left", "officedesk2right",
            "computer", "computermonitor", "desktopcomputer", "laptop", "officephone", "classicphone", "calculator", "deskcalendar",
            "deskglobe", "printer", "printertable", "paperbin", "framedcertificate", "decorativepapers01", "decorativepapers02",
            "stackofbooks", "bookswithbookends", "mousepad",
            "deskplant01", "deskplant02", "deskplant03", "deskplant04", "groundplant01", "groundplant02", "groundplant03",
            "groundplant04", "groundplant05", "groundplant06", "groundplant07", "pottedplant1", "cheapflower", "expensiveflower",
            "painting50x70", "pictureframe", "pictureframe2", "pictureframe3", "pictureframesmall", "wallmountedpictureframegroup",
            "mirrorcircle", "wallmountedmirrorirregular", "wallclock", "largewallclock", "wallmountedtv", "wallmountedtvmodern",
            "wallmountedtvxl", "loudspeaker2", "classicloudspeakermedium", "decorativebookshelf",
            "largedecorativebookshelf", "umbrellastand",
            "colorfulcarpet", "roundcarpet", "patterncarpet", "rug04", "carpetlongbigclassic01", "carpetlongbigclassic02",
            "carpetroundbigclassic01", "carpetroundbigclassic02", "carpetroundbigclassic03", "carpetroundbigmodern", "carpetbigmodern",
            "carpetlongbigmodern",
            "ceilinglampround", "ceilinglampsquare", "ceilinglamptube", "floorlamp",
            "cheapcoffeemachine", "industrialcoffeemachine", "drinksfridge", "watercooler", "waterfountain",
            "toiletstall", "sink", "sinkmodular", "sinksoap", "sinksoapwallmounted", "handdryer", "mirrorwallmounted",
            "bathroomsignwallmounted", "towelwall",
            "cleaningstation", "uniformlocker", "securityguardlocker", "securitycamera", "securitycameraroof", "securitypanel",
            "securitysign", "jobboard", "exitsign", "wallradiator", "trashbin", "smallcabinet"
        };

        /// <summary>What a bank branch keeps besides what a showroom keeps: queue barriers, glass counters, seats, the paper holder.</summary>
        public static readonly string[] BranchExtras =
        {
            "glassbarrier", "beltbarrier", "ropebarrier", "counterwithglass", "counterwithglasscornerleft", "eameschair",
            "bench01", "paperholder"
        };

        /// <summary>Seats to wait on: a branch design with one is offered first.</summary>
        public static readonly string[] BranchSeats =
        {
            "waitingroomchairs", "bench01", "armchair1", "armchair2", "cornersofa01", "cornersofa02left", "cornersofa02right", "modularsofa1l", "modularsofa1m",
            "modularsofa1r", "sofawithblanket", "valenciachair", "eameschair", "multipurposechair"
        };

        public const int BranchMost = 4;            // designs a bank branch is offered for a size at most

        public const string Till = "cashregister";       // the showroom's salesperson waits by it
        public const string Toilet = "toiletstall";
        public const string LampMark = "ceilinglamp";    // not counted as furniture

        public const string ItemPrefix = "ba:itemname_";

        private static Donor Offered(string business)
        {
            return new Donor(DonorRoot.Installer, business);
        }
    }
}
