using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>The Brands app's tables as data (GameUi.TableSpec); each keeps the player's last sort and filters until the game closes.</summary>
    internal static class BrandsTables
    {
        // What a row of the Brands tab is (its filter).
        public const int IsBrand = 0, IsDistrict = 1;
        private static readonly string[] BrandKinds = { "reallife_brand_kind_brand", "reallife_brand_kind_district" };

        // Whose shop a row of the Shops tab is (its first filter).
        public const int IsMember = 0, IsChainShop = 1, IsPlain = 2, IsFranchised = 3;
        private static readonly string[] ShopKinds = { "reallife_brand_kind_member", "reallife_brand_kind_chain", "reallife_brand_kind_plain", "reallife_brand_kind_franchised" };

        // An applicant's temperament (the Applicants tab's first filter), in the order of Temper.
        private static readonly string[] Tempers = { "reallife_brand_kind_careful", "reallife_brand_kind_ambitious", "reallife_brand_kind_cutting" };

        // How an agreement stands (the Agreements tab's filter).
        public const int InOrder = 0, Waiting = 1, Trouble = 2, OwnChain = 3;
        private static readonly string[] States = { "reallife_brand_state_order", "reallife_brand_state_waiting", "reallife_brand_state_trouble", "reallife_brand_state_chain" };

        /// <summary>While the feature is not running: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Brands", "Off").Column("reallife_brand_col_brand", 600f, TableSort.None, flexible: true);

        /// <summary>A brand's row shows its best district's recognition; a district's row follows its brand, as the table opens by name.</summary>
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

        /// <summary>Royalty sums what franchised shops paid less what the player's own pay a chain; a franchised shop's standards are as last inspected.</summary>
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

        /// <summary>An applicant's capital is not the player's money: no totals.</summary>
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

        /// <summary>The player's own shops under a chain are rows too, what they pay counted against the sums; the state orders by how pressing it is.</summary>
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
