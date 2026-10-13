using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The Tech app's tables, declared as data (GameUi.TableSpec). The numbers beside each are the columns.</summary>
    public static class TechColumns
    {
        public static readonly TableSpec Off = new TableSpec("Tech", "Off").Column("reallife_tech_col_firm", 600f, TableSort.None, flexible: true);

        /// <summary>Firms: 0 firm, 1 kind, 2 people, 3 engineering points a day, 4 products, 5 contracts, 6 takings over 30 days, 7 recurring a month, 8 reputation.</summary>
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

        // ---- where a product stands
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

        // ---- where a contract stands
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
    }
}
