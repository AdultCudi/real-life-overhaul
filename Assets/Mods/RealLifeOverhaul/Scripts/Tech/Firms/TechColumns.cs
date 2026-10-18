using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Tech app's tables, declared as data (GameUi.TableSpec). The numbers beside each are the columns.</summary>
    public static class TechColumns
    {
        public static readonly TableSpec Off = new TableSpec("Tech", "Off").Column("reallife_tech_col_firm", 600f, TableSort.None, flexible: true);

        /// <summary>Firms: 0 firm, 1 kind, 2 people, 3 points a day, 4 products, 5 contracts, 6 takings over 30 days, 7 recurring a month, 8 reputation.</summary>
        public static readonly TableSpec Firms = new TableSpec("Tech", "Firms")
            .Column("reallife_tech_col_firm", 330f, TableSort.Text, flexible: true)
            .Column("reallife_tech_col_kind", 280f, TableSort.Text)
            .Column("reallife_tech_col_people", 150f, TableSort.Number)
            .Column("reallife_tech_col_points", 180f, TableSort.Number)
            .Column("reallife_tech_col_products", 160f, TableSort.Number)
            .Column("reallife_tech_col_contracts", 170f, TableSort.Number)
            .Column("reallife_tech_col_month", 230f, TableSort.Money, sums: true)
            .Column("reallife_tech_col_recurring", 230f, TableSort.Money, sums: true)
            .Column("reallife_tech_col_reputation", 190f, TableSort.Number, right: true)
            .Empty("reallife_tech_empty_firms");

        // row kinds: where a product stands
        public const int Building = 0, OnSale = 1, Withdrawn = 2;
        private static readonly string[] ProductKinds = { "reallife_tech_kind_building", "reallife_tech_kind_on_sale", "reallife_tech_kind_withdrawn" };

        /// <summary>Products: 0 product, 1 firm, 2 kind, 3 stage, 4 features against the rival's, 5 customers, 6 recurring a month, 7 quality, 8 bugs.</summary>
        public static readonly TableSpec Products = new TableSpec("Tech", "Products")
            .Column("reallife_tech_col_product", 300f, TableSort.Text, flexible: true)
            .Column("reallife_tech_col_firm", 280f, TableSort.Text)
            .Column("reallife_tech_col_kind", 260f, TableSort.Text)
            .Column("reallife_tech_col_stage", 200f, TableSort.Text)
            .Column("reallife_tech_col_features", 200f, TableSort.Number)
            .Column("reallife_tech_col_customers", 200f, TableSort.Number)
            .Column("reallife_tech_col_recurring", 220f, TableSort.Money, sums: true)
            .Column("reallife_tech_col_quality", 160f, TableSort.Number)
            .Column("reallife_tech_col_bugs", 140f, TableSort.Number, right: true)
            .Filter("reallife_tech_all_stages", ProductKinds)
            .Empty("reallife_tech_empty_products");

        // row kinds: where a contract stands
        public const int Waiting = 0, UnderWay = 1, Finished = 2;
        private static readonly string[] WorkKinds = { "reallife_tech_kind_waiting", "reallife_tech_kind_under_way", "reallife_tech_kind_finished" };

        /// <summary>Contracts: 0 client, 1 work, 2 firm, 3 value, 4 progress, 5 due, 6 satisfaction, 7 stage.</summary>
        public static readonly TableSpec Contracts = new TableSpec("Tech", "Contracts")
            .Column("reallife_tech_col_client", 330f, TableSort.Text, flexible: true)
            .Column("reallife_tech_col_work", 300f, TableSort.Text)
            .Column("reallife_tech_col_firm", 260f, TableSort.Text)
            .Column("reallife_tech_col_value", 230f, TableSort.Money, sums: true)
            .Column("reallife_tech_col_progress", 180f, TableSort.Number)
            .Column("reallife_tech_col_due", 170f, TableSort.Number)
            .Column("reallife_tech_col_satisfaction", 190f, TableSort.Number)
            .Column("reallife_tech_col_stage", 230f, TableSort.Text, right: true)
            .Filter("reallife_tech_all_contracts", WorkKinds)
            .Empty("reallife_tech_empty_contracts");

        // row kinds: a startup and the player
        public const int Yours = 0, AsksYou = 1, Others = 2, Closed = 3;
        private static readonly string[] StartupKinds = { "reallife_vc_kind_yours", "reallife_vc_kind_asks", "reallife_vc_kind_others", "reallife_vc_kind_closed" };

        /// <summary>Startups: 0 name, 1 sector, 2 stage, 3 revenue a month (the deck's until looked into), 4 value, 5 your share, 6 your value, 7 state.</summary>
        public static readonly TableSpec Startups = new TableSpec("Tech", "Startups")
            .Column("reallife_vc_col_startup", 280f, TableSort.Text, flexible: true)
            .Column("reallife_vc_col_sector", 240f, TableSort.Text)
            .Column("reallife_vc_col_stage", 180f, TableSort.Number)
            .Column("reallife_vc_col_revenue", 240f, TableSort.Money)
            .Column("reallife_vc_col_value", 240f, TableSort.Money)
            .Column("reallife_vc_col_share", 150f, TableSort.Number)
            .Column("reallife_vc_col_yours", 230f, TableSort.Money, sums: true)
            .Column("reallife_tech_col_stage", 220f, TableSort.Text, right: true)
            .Filter("reallife_vc_all_startups", StartupKinds)
            .Empty("reallife_vc_empty_startups");

        // row kinds: what a competitor is
        public const int RivalCompanies = 0, RivalStartups = 1, RivalFirms = 2;
        private static readonly string[] RivalKinds = { "reallife_tech_kind_companies", "reallife_tech_kind_startups", "reallife_tech_kind_firms" };

        /// <summary>Competitors of one firm: 0 competitor, 1 what it is, 2 where you meet, 3 your products against it, 4 tenders you both bid for, 5 where.</summary>
        public static readonly TableSpec Rivals = new TableSpec("Tech", "Competitors")
            .Column("reallife_tech_col_rival", 320f, TableSort.Text, flexible: true)
            .Column("reallife_tech_col_kind", 260f, TableSort.Text)
            .Column("reallife_tech_col_meet", 460f, TableSort.Text)
            .Column("reallife_tech_col_products", 160f, TableSort.Number)
            .Column("reallife_tech_col_tenders", 160f, TableSort.Number)
            .Column("reallife_tech_col_where", 380f, TableSort.Text, right: true)
            .Filter("reallife_tech_rivals_all", RivalKinds)
            .Empty("reallife_tech_empty_rivals");

        /// <summary>Funding: 0 firm, 1 stage, 2 your share, 3 value, 4 raised, 5 board, 6 target, 7 runway, 8 state.</summary>
        public static readonly TableSpec Funding = new TableSpec("Tech", "Funding")
            .Column("reallife_tech_col_firm", 300f, TableSort.Text, flexible: true)
            .Column("reallife_vc_col_stage", 180f, TableSort.Number)
            .Column("reallife_vc_col_share", 150f, TableSort.Number)
            .Column("reallife_vc_col_value", 230f, TableSort.Money)
            .Column("reallife_vc_col_raised", 230f, TableSort.Money, sums: true)
            .Column("reallife_vc_col_board", 150f, TableSort.Text)
            .Column("reallife_vc_col_target", 230f, TableSort.Money)
            .Column("reallife_vc_col_runway", 170f, TableSort.Number)
            .Column("reallife_tech_col_stage", 220f, TableSort.Text, right: true)
            .Empty("reallife_vc_empty_funding");

        /// <summary>Infra: 0 place, 1 district, 2 tier, 3 racks, 4 AI racks, 5 let, 6 technicians, 7 takings over 30 days, 8 state.</summary>
        public static readonly TableSpec Infra = new TableSpec("Tech", "Infra")
            .Column("reallife_infra_col_place", 300f, TableSort.Text, flexible: true)
            .Column("reallife_infra_col_district", 240f, TableSort.Text)
            .Column("reallife_infra_col_tier", 130f, TableSort.Number)
            .Column("reallife_infra_col_racks", 150f, TableSort.Number)
            .Column("reallife_infra_col_gpu", 150f, TableSort.Number)
            .Column("reallife_infra_col_let", 130f, TableSort.Number)
            .Column("reallife_infra_col_technicians", 190f, TableSort.Text)
            .Column("reallife_tech_col_month", 230f, TableSort.Money, sums: true)
            .Column("reallife_tech_col_stage", 220f, TableSort.Text, right: true)
            .Empty("reallife_infra_empty");
    }
}
