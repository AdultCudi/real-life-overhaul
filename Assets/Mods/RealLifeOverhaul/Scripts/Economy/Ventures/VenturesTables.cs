using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>The Ventures app's tables as data (GameUi.TableSpec); each summary numbers the columns for whoever writes the rows.</summary>
    internal static class VenturesTables
    {
        private static readonly string[] InvestorKinds = { "reallife_ven_kind_angel", "reallife_ven_kind_tycoon", "reallife_ven_kind_firm" };

        // How far an investor's money has got: the Investors tab's second filter.
        public const int IsOffer = 0, IsPledge = 1, IsDeal = 2, IsCalled = 3;
        private static readonly string[] Stages = { "reallife_tab_ven_stage_offer", "reallife_tab_ven_stage_pledge", "reallife_tab_ven_stage_deal", "reallife_tab_ven_stage_called" };

        private static readonly string[] Sectors =
        {
            "reallife_ven_sector_retail", "reallife_ven_sector_food", "reallife_ven_sector_office", "reallife_ven_sector_leisure", "reallife_ven_sector_industry"
        };

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
