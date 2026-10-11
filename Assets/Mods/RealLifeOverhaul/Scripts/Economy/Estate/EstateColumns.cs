namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The columns of the Property app's table, tab by tab, in one place: the names of the four header cells (none
    /// is a field of the row, so none sorts), their widths, and each tab's four titles. A change to how the kit's
    /// tables declare a column is made here and nowhere else in the app.
    /// </summary>
    internal static class EstateColumns
    {
        public const float WidthA = 620f, WidthB = 500f, WidthC = 300f, WidthD = 300f;
        public const int Flexible = 1;   // the column that takes what width is left

        public static readonly string[] Names = { "ColumnA", "ColumnB", "ColumnC", "ColumnD" };
        public static readonly float[] Widths = { WidthA, WidthB, WidthC, WidthD };

        /// <summary>The tabs, in the order of EstateDirector.TabMarket and its neighbours.</summary>
        public static readonly string[] Tabs =
        {
            "reallife_estate_tab_market", "reallife_estate_tab_mine", "reallife_estate_tab_tenants", "reallife_estate_tab_works", "reallife_estate_tab_agency", "reallife_estate_tab_hotel", "reallife_estate_tab_hire"
        };

        public static readonly string[][] Titles =
        {
            new[] { "reallife_estate_col_building", "reallife_estate_col_district", "reallife_estate_col_price", "reallife_estate_col_condition" },
            new[] { "reallife_estate_col_building", "reallife_estate_col_state", "reallife_estate_col_worth", "reallife_estate_col_rent" },
            new[] { "reallife_estate_col_building", "reallife_estate_col_who", "reallife_estate_col_rent", "reallife_estate_col_state" },
            new[] { "reallife_estate_col_building", "reallife_estate_col_condition", "reallife_estate_col_level", "reallife_estate_col_works" },
            new[] { "reallife_estate_col_instruction", "reallife_estate_col_worth", "reallife_estate_col_state", "reallife_estate_col_day" },
            new[] { "reallife_estate_col_hotel", "reallife_estate_col_sold", "reallife_estate_col_takings", "reallife_estate_col_rating" },
            new[] { "reallife_estate_col_car", "reallife_estate_col_state", "reallife_estate_col_rate", "reallife_estate_col_takings" }
        };
    }
}
