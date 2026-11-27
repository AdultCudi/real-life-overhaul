using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Tech
{
    /// <summary>Who puts a piece of work out. Saved on the contract: never renumber, add at the end. 0 is every contract made before the kinds.</summary>
    public enum ClientKind
    {
        Classic = 0, Government = 1, Individual = 2, Company = 3, Count = 4
    }

    /// <summary>The line of business of a company client, which decides who the brief is written for. None for any other kind of client.</summary>
    public enum Trade
    {
        None = -1, Retail = 0, Grocer = 1, Logistics = 2, Bank = 3, Insurer = 4, Restaurant = 5, Hotel = 6, Law = 7, Health = 8, Property = 9, Industry = 10, Utility = 11, Builder = 12, Count = 13
    }

    /// <summary>
    /// One client's brief: a piece of work in the client's own voice, with what it asks of the firm. Its number is saved on the contract (never reused), its text is the
    /// key. A government brief names its department, an individual brief the person, a company brief takes a company of its trade from the city.
    /// </summary>
    public sealed class ClientBrief
    {
        public readonly int Id;
        public readonly ClientKind Kind;
        public readonly WorkKind Work;
        public readonly Trade Trade;
        public readonly string Key;        // the brief's text, written whole as the client would write it
        public readonly int NeedSecurity;  // thousandths of security posture the firm must hold to take it; 0 none
        public readonly int NeedReliability;
        public readonly bool Tender;       // a public tender that rival firms bid on (a government brief only)
        public readonly string[] Names;    // the departments or the people who write it; null for a company, which is named from the city

        public ClientBrief(int id, ClientKind kind, WorkKind work, Trade trade, string key, int needSecurity, int needReliability, bool tender, params string[] names)
        {
            Id = id;
            Kind = kind;
            Work = work;
            Trade = trade;
            Key = key;
            NeedSecurity = needSecurity;
            NeedReliability = needReliability;
            Tender = tender;
            Names = names != null && names.Length > 0 ? names : null;
        }
    }

    /// <summary>
    /// The figures of the three new kinds of client (city departments, individuals, the city's companies) beside the old clients. Game-free. A kind's points and pay are a
    /// share of what the day's offer was drawn at, so the mix changes who asks and how, never how much work the city puts out: the checks hold the income of a small, a
    /// medium and a large firm to the old mix's within a band.
    /// </summary>
    public static class ClientCatalogue
    {
        // Who a day's offer comes from, thousandths: the old clients keep a share whatever the firm's name, individuals come to a young firm and the departments to a known one.
        public const int ClassicShare = 150;
        public const int IndividualLeast = 150, IndividualMost = 600, IndividualBase = 600;   // 600 less half the firm's name, kept between 150 and 600
        public const int GovernmentBase = 40, GovernmentMost = 220;                            // 40 plus a sixth of the name, at most 220

        // By ClientKind: Classic, Government, Individual, Company.
        public static readonly int[] PointsScale = { 1000, 2200, 450, 1000 };      // thousandths of the points the offer was drawn at
        public static readonly int[] PointsSpread = { 0, 150, 250, 200 };          // thousandths either way, by a roll of the contract's own
        public static readonly int[] RateScale = { 1000, 1150, 950, 1000 };        // thousandths of the pay for a point (a company's trade moves it, see TradeRate)
        public static readonly int[] Upfront = { 300, 100, 400, 300 };            // thousandths of the price paid when it is signed
        public static readonly int[] DueScale = { 1000, 2000, 700, 1000 };         // thousandths of the days a kind of work is given
        public static readonly int[] LatePenalty = { 20, 30, 10, 20 };             // thousandths of the price off for each day late, up to half
        public static readonly int[] ReputationOnTime = { 25, 45, 8, 30 };         // what the firm's name gains for work done on time
        public static readonly int[] ReputationLate = { 5, 8, 4, 5 };              // ...and loses for each day late
        public static readonly int[] ReputationFailed = { 80, 120, 40, 80 };       // ...and loses when the work fails

        // Payment after delivery. A department pays every time, weeks after. An individual or a company pays late now and then, which the client does not say beforehand.
        public const int GovernmentLagLeast = 21, GovernmentLagMost = 35;
        public const int IndividualLateChance = 280, IndividualLagLeast = 8, IndividualLagMost = 24;   // per mille of individual contracts, and the days
        public const int CompanyTermsChance = 300, CompanyTermsDays = 14;

        // Scope creep: a client who asks for one more change after the work is part done. Unpaid, with the date moved by the same share.
        public const int IndividualCreepChance = 350, CompanyCreepChance = 150;   // per mille of contracts that carry it
        public const int CreepShare = 200;                                         // thousandths of the points added by a change
        public const int CreepFirstAt = 400, CreepSecondAt = 750;                  // thousandths of the work done when a change may come
        public const int CreepMost = 2;

        // A department's tender: its fair price per point (thousandths of the council's old tender price, FirmsCatalogue.CouncilShare), and the weeks of audit a late public job brings.
        public const int TenderRate = 1250;
        public const int ScrutinyLateDays = 3, ScrutinyDays = 30;

        /// <summary>A company's rate for a point by its trade: the trades that ask for a safe, steady system pay a little more, a restaurant's thin margins a little less. Thousandths.</summary>
        public static int TradeRate(Trade t)
        {
            switch (t)
            {
                case Trade.Bank: return 1100;
                case Trade.Utility: return 1100;
                case Trade.Law: return 1080;
                case Trade.Health: return 1060;
                case Trade.Insurer: return 1050;
                case Trade.Hotel: return 980;
                case Trade.Grocer: return 970;
                case Trade.Property: return 960;
                case Trade.Builder: return 960;
                case Trade.Restaurant: return 940;
                default: return 1000;
            }
        }

        /// <summary>The trade a company of one of the game's own business types is a client in; None for a type no brief is written for.</summary>
        public static Trade TradeOf(string businessType)
        {
            switch (businessType)
            {
                case "ba:businesstype_supermarket":
                case "ba:businesstype_fruitandvegetablestore":
                    return Trade.Grocer;
                case "ba:businesstype_clothingstore":
                case "ba:businesstype_electronicsstore":
                case "ba:businesstype_appliancestore":
                case "ba:businesstype_bookstore":
                case "ba:businesstype_giftshop":
                case "ba:businesstype_jewelrystore":
                case "ba:businesstype_furniturestore":
                case "ba:businesstype_liquorstore":
                case "ba:businesstype_officesupplystore":
                case "ba:businesstype_wholesalestore":
                case "ba:businesstype_florist":
                    return Trade.Retail;
                case "ba:businesstype_fastfoodrestaurant":
                case "ba:businesstype_coffeeshop":
                    return Trade.Restaurant;
                case "ba:businesstype_movingservice":
                case "ba:businesstype_truckgarage":
                case "ba:businesstype_warehouse":
                case "ba:businesstype_importexport":
                case "ba:businesstype_privatedriverservice":
                    return Trade.Logistics;
                case "ba:businesstype_bank":
                    return Trade.Bank;
                case "ba:businesstype_lawfirm":
                    return Trade.Law;
                case "ba:businesstype_clinic":
                case "ba:businesstype_hospital":
                    return Trade.Health;
                case "ba:businesstype_gym":
                case "ba:businesstype_cinema":
                case "ba:businesstype_theater":
                case "ba:businesstype_nightclub":
                case "ba:businesstype_casino":
                case "ba:businesstype_hairdresser":
                    return Trade.Hotel;
                case "ba:businesstype_factory":
                    return Trade.Industry;
                case "ba:businesstype_interiorinstallationfirm":
                case "reallife:businesstype_construction":
                    return Trade.Builder;
                default:
                    return Trade.None;
            }
        }

        /// <summary>Whether a listed company of the city is a client for a trade: by its sector and, for a law firm, the business type it competes with. Never by name.</summary>
        public static bool InTrade(CompanyDef c, Trade t)
        {
            switch (t)
            {
                case Trade.Retail: return c.Sector == MarketSector.Stores || c.Sector == MarketSector.Specialists;
                case Trade.Grocer: return c.Sector == MarketSector.Groceries;
                case Trade.Logistics: return c.Sector == MarketSector.Logistics || c.Sector == MarketSector.Transport;
                case Trade.Bank: return c.Sector == MarketSector.Banks || c.Sector == MarketSector.Finance;
                case Trade.Insurer: return c.Sector == MarketSector.Insurance;
                case Trade.Restaurant: return c.Sector == MarketSector.Restaurants;
                case Trade.Hotel: return c.Sector == MarketSector.Leisure;
                case Trade.Law: return c.Trade == "lawfirm";
                case Trade.Health: return c.Sector == MarketSector.Healthcare;
                case Trade.Property: return c.Sector == MarketSector.Property || c.Sector == MarketSector.Trusts;
                case Trade.Industry: return c.Sector == MarketSector.Industry || c.Sector == MarketSector.Goods;
                case Trade.Utility: return c.Sector == MarketSector.Power || c.Sector == MarketSector.Water || c.Sector == MarketSector.Telecoms || c.Sector == MarketSector.Energy;
                case Trade.Builder: return c.Sector == MarketSector.Construction;
                default: return false;
            }
        }

        /// <summary>Every brief. Numbers are saved on contracts: add at the end, never reuse one.</summary>
        public static readonly ClientBrief[] Briefs =
        {
            new ClientBrief(101, ClientKind.Government, WorkKind.Integration, Trade.None, "reallife_deal_brief_101", 250, 400, true, "Department of Buildings"),
            new ClientBrief(102, ClientKind.Government, WorkKind.Integration, Trade.None, "reallife_deal_brief_102", 200, 600, true, "Metropolitan Transit Authority"),
            new ClientBrief(103, ClientKind.Government, WorkKind.Integration, Trade.None, "reallife_deal_brief_103", 600, 400, true, "Municipal Health Service"),
            new ClientBrief(104, ClientKind.Government, WorkKind.Integration, Trade.None, "reallife_deal_brief_104", 450, 300, true, "Board of Education"),
            new ClientBrief(105, ClientKind.Government, WorkKind.Website, Trade.None, "reallife_deal_brief_105", 0, 500, false, "City Council"),
            new ClientBrief(106, ClientKind.Government, WorkKind.Integration, Trade.None, "reallife_deal_brief_106", 350, 300, false, "Parking Authority"),
            new ClientBrief(107, ClientKind.Government, WorkKind.Website, Trade.None, "reallife_deal_brief_107", 0, 300, false, "Public Library Service"),
            new ClientBrief(108, ClientKind.Government, WorkKind.Managed, Trade.None, "reallife_deal_brief_108", 300, 600, true, "Housing Office"),
            new ClientBrief(109, ClientKind.Government, WorkKind.Audit, Trade.None, "reallife_deal_brief_109", 550, 0, false, "City Treasury"),
            new ClientBrief(110, ClientKind.Government, WorkKind.PenTest, Trade.None, "reallife_deal_brief_110", 650, 0, false, "Elections Office"),
            new ClientBrief(111, ClientKind.Government, WorkKind.Branding, Trade.None, "reallife_deal_brief_111", 0, 0, false, "Office of Tourism"),
            new ClientBrief(112, ClientKind.Government, WorkKind.Website, Trade.None, "reallife_deal_brief_112", 0, 700, false, "Emergency Management Office"),
            new ClientBrief(113, ClientKind.Government, WorkKind.Integration, Trade.None, "reallife_deal_brief_113", 0, 400, false, "Sanitation Department"),
            new ClientBrief(114, ClientKind.Government, WorkKind.Integration, Trade.None, "reallife_deal_brief_114", 300, 350, true, "Department of Consumer Affairs"),
            new ClientBrief(115, ClientKind.Government, WorkKind.PenTest, Trade.None, "reallife_deal_brief_115", 600, 0, false, "Municipal Health Service"),
            new ClientBrief(116, ClientKind.Government, WorkKind.Managed, Trade.None, "reallife_deal_brief_116", 300, 700, false, "Metropolitan Transit Authority"),
            new ClientBrief(117, ClientKind.Government, WorkKind.Managed, Trade.None, "reallife_deal_brief_117", 250, 650, true, "Department of Parks"),
            new ClientBrief(201, ClientKind.Individual, WorkKind.Website, Trade.None, "reallife_deal_brief_201", 0, 0, false, "Marta Kowal, baker"),
            new ClientBrief(202, ClientKind.Individual, WorkKind.Website, Trade.None, "reallife_deal_brief_202", 0, 0, false, "Dev Rao, photographer"),
            new ClientBrief(203, ClientKind.Individual, WorkKind.Website, Trade.None, "reallife_deal_brief_203", 0, 0, false, "Elena Rossi, piano teacher"),
            new ClientBrief(204, ClientKind.Individual, WorkKind.Website, Trade.None, "reallife_deal_brief_204", 0, 0, false, "Sam O'Hara, plumber"),
            new ClientBrief(205, ClientKind.Individual, WorkKind.Website, Trade.None, "reallife_deal_brief_205", 0, 0, false, "Joy Adeyemi, wedding celebrant"),
            new ClientBrief(206, ClientKind.Individual, WorkKind.Website, Trade.None, "reallife_deal_brief_206", 0, 0, false, "Gus Moretti, cheese stall"),
            new ClientBrief(207, ClientKind.Individual, WorkKind.Integration, Trade.None, "reallife_deal_brief_207", 0, 0, false, "Kofi Mensah, gym owner"),
            new ClientBrief(208, ClientKind.Individual, WorkKind.Integration, Trade.None, "reallife_deal_brief_208", 0, 0, false, "Ines Duarte, florist"),
            new ClientBrief(209, ClientKind.Individual, WorkKind.Integration, Trade.None, "reallife_deal_brief_209", 0, 0, false, "Dr Hana Sato, dentist"),
            new ClientBrief(210, ClientKind.Individual, WorkKind.Integration, Trade.None, "reallife_deal_brief_210", 0, 0, false, "Luca Bianchi, cafe owner"),
            new ClientBrief(211, ClientKind.Individual, WorkKind.Integration, Trade.None, "reallife_deal_brief_211", 0, 0, false, "Amara Okoye, private tutor"),
            new ClientBrief(212, ClientKind.Individual, WorkKind.Integration, Trade.None, "reallife_deal_brief_212", 0, 0, false, "Tess Harper, dog walker"),
            new ClientBrief(213, ClientKind.Individual, WorkKind.Audit, Trade.None, "reallife_deal_brief_213", 0, 0, false, "Nora Lindqvist, online shop owner"),
            new ClientBrief(214, ClientKind.Individual, WorkKind.Audit, Trade.None, "reallife_deal_brief_214", 0, 0, false, "Vikram Shah, freelance accountant"),
            new ClientBrief(215, ClientKind.Individual, WorkKind.PenTest, Trade.None, "reallife_deal_brief_215", 0, 0, false, "Bea Thompson, ceramicist"),
            new ClientBrief(216, ClientKind.Individual, WorkKind.Branding, Trade.None, "reallife_deal_brief_216", 0, 0, false, "Rosa Delgado, food truck owner"),
            new ClientBrief(217, ClientKind.Individual, WorkKind.Branding, Trade.None, "reallife_deal_brief_217", 0, 0, false, "Mira Patel, yoga teacher"),
            new ClientBrief(218, ClientKind.Individual, WorkKind.Branding, Trade.None, "reallife_deal_brief_218", 0, 0, false, "Jonas Weber, drummer"),
            new ClientBrief(219, ClientKind.Individual, WorkKind.Branding, Trade.None, "reallife_deal_brief_219", 0, 0, false, "Hugh Aldridge, bookbinder"),
            new ClientBrief(301, ClientKind.Company, WorkKind.Integration, Trade.Retail, "reallife_deal_brief_301", 0, 450, false),
            new ClientBrief(302, ClientKind.Company, WorkKind.Website, Trade.Retail, "reallife_deal_brief_302", 0, 500, false),
            new ClientBrief(303, ClientKind.Company, WorkKind.Managed, Trade.Retail, "reallife_deal_brief_303", 0, 600, false),
            new ClientBrief(304, ClientKind.Company, WorkKind.Branding, Trade.Retail, "reallife_deal_brief_304", 0, 0, false),
            new ClientBrief(337, ClientKind.Company, WorkKind.Audit, Trade.Retail, "reallife_deal_brief_337", 600, 0, false),
            new ClientBrief(305, ClientKind.Company, WorkKind.Integration, Trade.Grocer, "reallife_deal_brief_305", 0, 700, false),
            new ClientBrief(306, ClientKind.Company, WorkKind.Integration, Trade.Grocer, "reallife_deal_brief_306", 0, 500, false),
            new ClientBrief(307, ClientKind.Company, WorkKind.Managed, Trade.Grocer, "reallife_deal_brief_307", 0, 650, false),
            new ClientBrief(308, ClientKind.Company, WorkKind.Integration, Trade.Logistics, "reallife_deal_brief_308", 0, 500, false),
            new ClientBrief(309, ClientKind.Company, WorkKind.Integration, Trade.Logistics, "reallife_deal_brief_309", 0, 550, false),
            new ClientBrief(310, ClientKind.Company, WorkKind.Managed, Trade.Logistics, "reallife_deal_brief_310", 0, 700, false),
            new ClientBrief(311, ClientKind.Company, WorkKind.Integration, Trade.Bank, "reallife_deal_brief_311", 700, 500, false),
            new ClientBrief(312, ClientKind.Company, WorkKind.Audit, Trade.Bank, "reallife_deal_brief_312", 750, 0, false),
            new ClientBrief(313, ClientKind.Company, WorkKind.PenTest, Trade.Bank, "reallife_deal_brief_313", 750, 0, false),
            new ClientBrief(335, ClientKind.Company, WorkKind.Managed, Trade.Bank, "reallife_deal_brief_335", 500, 750, false),
            new ClientBrief(314, ClientKind.Company, WorkKind.Integration, Trade.Insurer, "reallife_deal_brief_314", 450, 400, false),
            new ClientBrief(315, ClientKind.Company, WorkKind.Website, Trade.Insurer, "reallife_deal_brief_315", 0, 450, false),
            new ClientBrief(336, ClientKind.Company, WorkKind.Audit, Trade.Insurer, "reallife_deal_brief_336", 600, 0, false),
            new ClientBrief(316, ClientKind.Company, WorkKind.Integration, Trade.Restaurant, "reallife_deal_brief_316", 0, 600, false),
            new ClientBrief(317, ClientKind.Company, WorkKind.Website, Trade.Restaurant, "reallife_deal_brief_317", 0, 400, false),
            new ClientBrief(318, ClientKind.Company, WorkKind.Branding, Trade.Restaurant, "reallife_deal_brief_318", 0, 0, false),
            new ClientBrief(338, ClientKind.Company, WorkKind.Managed, Trade.Restaurant, "reallife_deal_brief_338", 0, 550, false),
            new ClientBrief(319, ClientKind.Company, WorkKind.Integration, Trade.Hotel, "reallife_deal_brief_319", 0, 600, false),
            new ClientBrief(320, ClientKind.Company, WorkKind.Website, Trade.Hotel, "reallife_deal_brief_320", 0, 450, false),
            new ClientBrief(321, ClientKind.Company, WorkKind.Branding, Trade.Hotel, "reallife_deal_brief_321", 0, 0, false),
            new ClientBrief(322, ClientKind.Company, WorkKind.Managed, Trade.Hotel, "reallife_deal_brief_322", 0, 650, false),
            new ClientBrief(323, ClientKind.Company, WorkKind.Integration, Trade.Law, "reallife_deal_brief_323", 600, 350, false),
            new ClientBrief(324, ClientKind.Company, WorkKind.Audit, Trade.Law, "reallife_deal_brief_324", 650, 0, false),
            new ClientBrief(325, ClientKind.Company, WorkKind.Integration, Trade.Health, "reallife_deal_brief_325", 550, 500, false),
            new ClientBrief(326, ClientKind.Company, WorkKind.Audit, Trade.Health, "reallife_deal_brief_326", 700, 0, false),
            new ClientBrief(327, ClientKind.Company, WorkKind.Integration, Trade.Property, "reallife_deal_brief_327", 0, 450, false),
            new ClientBrief(328, ClientKind.Company, WorkKind.Website, Trade.Property, "reallife_deal_brief_328", 0, 400, false),
            new ClientBrief(329, ClientKind.Company, WorkKind.Integration, Trade.Industry, "reallife_deal_brief_329", 0, 600, false),
            new ClientBrief(330, ClientKind.Company, WorkKind.Managed, Trade.Industry, "reallife_deal_brief_330", 300, 700, false),
            new ClientBrief(331, ClientKind.Company, WorkKind.Integration, Trade.Utility, "reallife_deal_brief_331", 0, 750, false),
            new ClientBrief(332, ClientKind.Company, WorkKind.Managed, Trade.Utility, "reallife_deal_brief_332", 400, 800, false),
            new ClientBrief(339, ClientKind.Company, WorkKind.PenTest, Trade.Utility, "reallife_deal_brief_339", 700, 400, false),
            new ClientBrief(333, ClientKind.Company, WorkKind.Integration, Trade.Builder, "reallife_deal_brief_333", 0, 450, false),
            new ClientBrief(334, ClientKind.Company, WorkKind.Website, Trade.Builder, "reallife_deal_brief_334", 0, 350, false),
        };

        /// <summary>A brief by its saved number, or null for a number this version does not know.</summary>
        public static ClientBrief BriefOf(int id)
        {
            for (int i = 0; i < Briefs.Length; i++)
            {
                if (Briefs[i].Id == id)
                {
                    return Briefs[i];
                }
            }
            return null;
        }
    }
}
