using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The Ventures app's tables, declared as data (GameUi.TableSpec): for each tab and each page of a pitch its
    /// columns, how each sorts, which are summed, what its rows can be filtered by, and the sentence shown when a
    /// filter leaves nothing. The numbers beside each are the columns, for whoever writes the rows.
    /// </summary>
    internal static class VenturesTables
    {
        private static readonly string[] InvestorKinds = { "reallife_ven_kind_angel", "reallife_ven_kind_tycoon", "reallife_ven_kind_firm" };

        // ---- how far an investor's money has got (the second filter of the Investors tab)
        public const int IsOffer = 0, IsPledge = 1, IsDeal = 2, IsCalled = 3;
        private static readonly string[] Stages = { "reallife_tab_ven_stage_offer", "reallife_tab_ven_stage_pledge", "reallife_tab_ven_stage_deal", "reallife_tab_ven_stage_called" };

        private static readonly string[] Sectors =
        {
            "reallife_ven_sector_retail", "reallife_ven_sector_food", "reallife_ven_sector_office", "reallife_ven_sector_leisure", "reallife_ven_sector_industry"
        };

        // ---- whether shares of a company are held (the second filter of the Market tab)
        public const int Held = 0, NotHeld = 1;
        private static readonly string[] Holding = { "reallife_tab_ven_held", "reallife_tab_ven_not_held" };

        /// <summary>While the file is not open: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Ventures", "Off").Column("reallife_ven_col_investor", 600f, TableSort.None, flexible: true);

        /// <summary>Investors: 0 investor, 1 type, 2 business, 3 put in, 4 share of profit, 5 paid to them, 6 state.</summary>
        public static readonly TableSpec Investors = new TableSpec("Ventures", "Investors")
            .Column("reallife_ven_col_investor", 420f, TableSort.Text, flexible: true)
            .Column("reallife_tab_col_type", 260f, TableSort.Text)
            .Column("reallife_ven_col_business", 380f, TableSort.Text)
            .Column("reallife_tab_col_invested", 260f, TableSort.Money, sums: true)
            .Column("reallife_tab_col_share", 220f, TableSort.Number)
            .Column("reallife_tab_col_paid_out", 260f, TableSort.Money, sums: true)
            .Column("reallife_ven_col_state", 340f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_types", InvestorKinds)
            .Filter("reallife_tab_all_stages", Stages)
            .Empty("reallife_tab_empty_investors");

        /// <summary>Raise money, the business: 0 business, 1 recent profit, 2 open for, 3 promised.</summary>
        public static readonly TableSpec RaiseBusiness = new TableSpec("Ventures", "Raise money: business")
            .Column("reallife_ven_col_business", 620f, TableSort.Text, flexible: true)
            .Column("reallife_ven_col_profit", 400f, TableSort.Money)
            .Column("reallife_ven_col_age", 300f, TableSort.Number)
            .Column("reallife_ven_col_promised", 300f, TableSort.Number, right: true);

        /// <summary>Raise money, the kind of business: 0 kind, 1 sector, 2 liked by, 3 you run.</summary>
        public static readonly TableSpec RaiseType = new TableSpec("Ventures", "Raise money: kind")
            .Column("reallife_ven_col_kind", 620f, TableSort.Text, flexible: true)
            .Column("reallife_ven_col_sector", 500f, TableSort.Text)
            .Column("reallife_ven_col_liked", 300f, TableSort.Number)
            .Column("reallife_ven_col_run", 300f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_sectors", Sectors)
            .Empty("reallife_tab_empty_kinds");

        /// <summary>Raise money, the investor: 0 investor, 1 type, 2 looks for, 3 puts in, 4 sees you.</summary>
        public static readonly TableSpec RaiseWho = new TableSpec("Ventures", "Raise money: investor")
            .Column("reallife_ven_col_investor", 480f, TableSort.Text, flexible: true)
            .Column("reallife_tab_col_type", 280f, TableSort.Text)
            .Column("reallife_ven_col_looks", 600f, TableSort.Text)
            .Column("reallife_ven_col_sums", 440f, TableSort.Money)
            .Column("reallife_ven_col_open", 320f, TableSort.Custom, right: true)
            .Filter("reallife_tab_all_types", InvestorKinds)
            .Empty("reallife_tab_empty_investors");

        /// <summary>Market: 0 company, 1 sector, 2 price, 3 last move, 4 shares held, 5 worth, 6 gain or loss, 7 dividend.</summary>
        public static readonly TableSpec Market = new TableSpec("Ventures", "Market")
            .Column("reallife_ven_col_company", 400f, TableSort.Text, flexible: true)
            .Column("reallife_ven_col_sector", 300f, TableSort.Text)
            .Column("reallife_ven_col_price", 200f, TableSort.Money)
            .Column("reallife_ven_col_today", 200f, TableSort.Number)
            .Column("reallife_ven_col_shares", 180f, TableSort.Number)
            .Column("reallife_ven_col_worth", 240f, TableSort.Money, sums: true)
            .Column("reallife_ven_col_gain", 260f, TableSort.Money, sums: true)
            .Column("reallife_tab_col_dividend", 200f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_sectors", VenturesCatalogue.MarketSectorKeys)
            .Filter("reallife_tab_ven_all_companies", Holding)
            .Empty("reallife_tab_empty_companies");

        /// <summary>Portfolio: 0 company, 1 sector, 2 shares, 3 worth, 4 paid, 5 gain or loss, 6 dividends received.</summary>
        public static readonly TableSpec Portfolio = new TableSpec("Ventures", "Portfolio")
            .Column("reallife_ven_col_company", 440f, TableSort.Text, flexible: true)
            .Column("reallife_ven_col_sector", 320f, TableSort.Text)
            .Column("reallife_ven_col_shares", 200f, TableSort.Number)
            .Column("reallife_ven_col_worth", 260f, TableSort.Money, sums: true)
            .Column("reallife_ven_col_cost", 260f, TableSort.Money, sums: true)
            .Column("reallife_ven_col_gain", 280f, TableSort.Money, sums: true)
            .Column("reallife_tab_col_dividends", 260f, TableSort.Money, sums: true, right: true)
            .Filter("reallife_tab_all_sectors", VenturesCatalogue.MarketSectorKeys)
            .Empty("reallife_tab_empty_companies");

        /// <summary>Property: 0 building, 1 district, 2 let, 3 paid, 4 worth, 5 rent over 7 days, 6 yield a year.</summary>
        public static readonly TableSpec Property = new TableSpec("Ventures", "Property")
            .Column("reallife_ven_col_building", 500f, TableSort.Text, flexible: true)
            .Column("reallife_co_col_district", 340f, TableSort.Text)
            .Column("reallife_tab_col_let", 160f, TableSort.Number)
            .Column("reallife_ven_col_cost", 260f, TableSort.Money, sums: true)
            .Column("reallife_ven_col_worth", 260f, TableSort.Money, sums: true)
            .Column("reallife_ven_col_rent", 260f, TableSort.Money, sums: true)
            .Column("reallife_ven_col_yield", 240f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_districts")
            .Empty("reallife_tab_empty_buildings");
    }
}
