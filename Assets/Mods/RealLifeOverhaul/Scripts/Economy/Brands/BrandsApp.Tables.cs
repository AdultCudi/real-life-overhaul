using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The Brands app's tables, declared as data (GameUi.TableSpec), all in this one place: for each tab its columns,
    /// how each sorts, which are summed, what its rows can be filtered by, the order it opens in, and the sentence
    /// shown when a filter leaves nothing. The numbers beside each are the columns, for whoever writes the rows.
    /// Each specification also keeps what the player last chose there until the game is closed (the kit's).
    /// </summary>
    internal static class BrandsTables
    {
        // ---- what a row of the Brands tab is (its filter)
        public const int IsBrand = 0, IsDistrict = 1;
        private static readonly string[] BrandKinds = { "reallife_brand_kind_brand", "reallife_brand_kind_district" };

        // ---- whose shop a row of the Shops tab is (its first filter)
        public const int IsMember = 0, IsChainShop = 1, IsPlain = 2, IsFranchised = 3;
        private static readonly string[] ShopKinds = { "reallife_brand_kind_member", "reallife_brand_kind_chain", "reallife_brand_kind_plain", "reallife_brand_kind_franchised" };

        // ---- an applicant's temperament (the Applicants tab's first filter), in the order of Temper
        private static readonly string[] Tempers = { "reallife_brand_kind_careful", "reallife_brand_kind_ambitious", "reallife_brand_kind_cutting" };

        // ---- how an agreement stands (the Agreements tab's filter)
        public const int InOrder = 0, Waiting = 1, Trouble = 2, OwnChain = 3;
        private static readonly string[] States = { "reallife_brand_state_order", "reallife_brand_state_waiting", "reallife_brand_state_trouble", "reallife_brand_state_chain" };

        /// <summary>While the feature is not running: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Brands", "Off").Column("reallife_brand_col_brand", 600f, TableSort.None, flexible: true);

        /// <summary>
        /// Brands: 0 brand (a district's row: the brand, then the district), 1 type, 2 shops, 3 recognition (a brand's
        /// row: its best district), 4 city-wide, 5 standards, 6 what the name is worth to a shop there, per cent of
        /// customers. Opens by name, so that each brand is followed by its districts. No money, so no totals.
        /// </summary>
        public static readonly TableSpec Brands = new TableSpec("Brands", "Brands")
            .Column("reallife_brand_col_brand", 520f, TableSort.Text, flexible: true)
            .Column("reallife_brand_col_type", 320f, TableSort.Text)
            .Column("reallife_brand_col_shops", 160f, TableSort.Number)
            .Column("reallife_brand_col_recognition", 240f, TableSort.Number)
            .Column("reallife_brand_col_city", 220f, TableSort.Number)
            .Column("reallife_brand_col_standards", 220f, TableSort.Number)
            .Column("reallife_brand_col_worth", 240f, TableSort.Number, right: true)
            .Filter("reallife_tab_all_kinds", BrandKinds)
            .Order(0, true)
            .Empty("reallife_brand_empty_brands");

        /// <summary>
        /// Shops: 0 shop, 1 run by, 2 brand or chain, 3 district, 4 takings yesterday (summed), 5 royalty yesterday
        /// (summed: what franchised shops paid, less what the player's own shops pay a city chain), 6 standards (a
        /// franchised shop: as last inspected), 7 status. Filters: whose shop it is, and the district. Opens with the
        /// shop that took most first.
        /// </summary>
        public static readonly TableSpec Shops = new TableSpec("Brands", "Shops")
            .Column("reallife_brand_col_shop", 440f, TableSort.Text, flexible: true)
            .Column("reallife_brand_col_run_by", 300f, TableSort.Text)
            .Column("reallife_brand_col_brand", 280f, TableSort.Text)
            .Column("reallife_brand_col_district", 280f, TableSort.Text)
            .Column("reallife_brand_col_takings", 220f, TableSort.Money, sums: true)
            .Column("reallife_brand_col_royalty", 200f, TableSort.Money, sums: true)
            .Column("reallife_brand_col_standards", 180f, TableSort.Number)
            .Column("reallife_brand_col_status", 280f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", ShopKinds)
            .Filter("reallife_tab_all_districts")
            .Order(4, false)
            .Empty("reallife_brand_empty_shops");

        /// <summary>
        /// Applicants: 0 applicant, 1 brand, 2 district wanted, 3 temperament, 4 capital, 5 years in the trade, 6 the
        /// last day they wait. Filters: temperament, and the district. Opens with whoever goes soonest first. Their
        /// capital is not the player's money: no totals.
        /// </summary>
        public static readonly TableSpec Applicants = new TableSpec("Brands", "Applicants")
            .Column("reallife_brand_col_applicant", 460f, TableSort.Text, flexible: true)
            .Column("reallife_brand_col_brand", 320f, TableSort.Text)
            .Column("reallife_brand_col_district", 320f, TableSort.Text)
            .Column("reallife_brand_col_temper", 260f, TableSort.Text)
            .Column("reallife_brand_col_capital", 260f, TableSort.Money)
            .Column("reallife_brand_col_years", 200f, TableSort.Number)
            .Column("reallife_brand_col_until", 240f, TableSort.Date, right: true)
            .Filter("reallife_tab_all_kinds", Tempers)
            .Filter("reallife_tab_all_districts")
            .Order(6, true)
            .Empty("reallife_brand_empty_applicants");

        /// <summary>
        /// Agreements: 0 franchisee (the player's own with a city chain: the shop), 1 brand or chain, 2 district,
        /// 3 royalty rate, 4 royalty and levy paid yesterday (summed; what the player pays a chain counts against it),
        /// 5 paid in all (summed the same way), 6 the day it runs until, 7 its state (ordered by how pressing it is:
        /// something waiting, then short of cash, then in order). Filter: the state. Opens with what presses most first.
        /// </summary>
        public static readonly TableSpec Agreements = new TableSpec("Brands", "Agreements")
            .Column("reallife_brand_col_franchisee", 420f, TableSort.Text, flexible: true)
            .Column("reallife_brand_col_brand", 280f, TableSort.Text)
            .Column("reallife_brand_col_district", 260f, TableSort.Text)
            .Column("reallife_brand_col_rate", 200f, TableSort.Number)
            .Column("reallife_brand_col_paid", 240f, TableSort.Money, sums: true)
            .Column("reallife_brand_col_paid_all", 240f, TableSort.Money, sums: true)
            .Column("reallife_brand_col_ends", 200f, TableSort.Date)
            .Column("reallife_brand_col_state", 300f, TableSort.Custom, right: true)
            .Filter("reallife_tab_all_kinds", States)
            .Order(7, false)
            .Empty("reallife_brand_empty_agreements");
    }
}
