using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Hustles
{
    /// <summary>The Marketplace app's tables as data (GameUi.TableSpec); each table's summary numbers its columns for whoever writes the rows.</summary>
    public static class HustleColumns
    {
        private const string App = "Marketplace";

        /// <summary>The filter's kinds: each ThingKind in its order.</summary>
        public static readonly string[] ThingKinds =
        {
            "reallife_hustle_kind_electronics", "reallife_hustle_kind_furniture", "reallife_hustle_kind_clothes", "reallife_hustle_kind_shoes", "reallife_hustle_kind_car"
        };

        public const int Waiting = 0, Sold = 1;
        public static readonly string[] OfferKinds = { "reallife_hustle_filter_waiting", "reallife_hustle_filter_sold" };

        public static readonly string[] GigKinds = { "reallife_hustle_gig_detailing", "reallife_hustle_gig_handyman", "reallife_hustle_gig_junk", "reallife_hustle_gig_moving" };

        public static readonly TableSpec Off = new TableSpec(App, "Off").Column("reallife_hustle_col_thing", 600f, TableSort.None, flexible: true);

        /// <summary>Listings: 0 thing, 1 kind, 2 condition said, 3 asking, 4 worth as said, 5 seller, 6 address, 7 state.</summary>
        public static readonly TableSpec Listings = new TableSpec(App, "Listings")
            .Column("reallife_hustle_col_thing", 360f, TableSort.Text, flexible: true)
            .Column("reallife_hustle_col_kind", 220f, TableSort.Text)
            .Column("reallife_hustle_col_condition", 220f, TableSort.Number)
            .Column("reallife_hustle_col_asking", 200f, TableSort.Money)
            .Column("reallife_hustle_col_worth", 200f, TableSort.Money)
            .Column("reallife_hustle_col_seller", 260f, TableSort.Text)
            .Column("reallife_hustle_col_where", 320f, TableSort.Text)
            .Column("reallife_hustle_col_state", 220f, TableSort.Text, right: true)
            .Filter("reallife_hustle_filter_all", ThingKinds)
            .Empty("reallife_hustle_empty_listings");

        /// <summary>Yours: 0 thing, 1 kind, 2 condition, 3 cost, 4 worth, 5 asking, 6 where, 7 state.</summary>
        public static readonly TableSpec Yours = new TableSpec(App, "Yours")
            .Column("reallife_hustle_col_thing", 360f, TableSort.Text, flexible: true)
            .Column("reallife_hustle_col_kind", 220f, TableSort.Text)
            .Column("reallife_hustle_col_condition", 220f, TableSort.Number)
            .Column("reallife_hustle_col_cost", 200f, TableSort.Money, sums: true)
            .Column("reallife_hustle_col_worth", 200f, TableSort.Money, sums: true)
            .Column("reallife_hustle_col_asking", 200f, TableSort.Money)
            .Column("reallife_hustle_col_where", 320f, TableSort.Text)
            .Column("reallife_hustle_col_state", 240f, TableSort.Text, right: true)
            .Filter("reallife_hustle_filter_all", ThingKinds)
            .Empty("reallife_hustle_empty_yours");

        /// <summary>Offers: 0 buyer, 1 thing, 2 offer or price, 3 asking or cost, 4 worth or margin, 5 lapses or day, 6 state.</summary>
        public static readonly TableSpec Offers = new TableSpec(App, "Offers")
            .Column("reallife_hustle_col_buyer", 280f, TableSort.Text)
            .Column("reallife_hustle_col_thing", 360f, TableSort.Text, flexible: true)
            .Column("reallife_hustle_col_offer", 200f, TableSort.Money, sums: true)
            .Column("reallife_hustle_col_asking", 200f, TableSort.Money)
            .Column("reallife_hustle_col_margin", 200f, TableSort.Money, sums: true)
            .Column("reallife_hustle_col_when", 220f, TableSort.Number)
            .Column("reallife_hustle_col_state", 240f, TableSort.Text, right: true)
            .Filter("reallife_hustle_filter_offers", OfferKinds)
            .Empty("reallife_hustle_empty_offers");

        /// <summary>Freelance: 0 client, 1 work, 2 address, 3 pay, 4 when, 5 needs, 6 state.</summary>
        public static readonly TableSpec Freelance = new TableSpec(App, "Freelance")
            .Column("reallife_hustle_col_client", 280f, TableSort.Text)
            .Column("reallife_hustle_col_work", 260f, TableSort.Text)
            .Column("reallife_hustle_col_where", 360f, TableSort.Text, flexible: true)
            .Column("reallife_hustle_col_pay", 180f, TableSort.Money, sums: true)
            .Column("reallife_hustle_col_when", 240f, TableSort.Number)
            .Column("reallife_hustle_col_needs", 220f, TableSort.Text)
            .Column("reallife_hustle_col_state", 260f, TableSort.Text, right: true)
            .Filter("reallife_hustle_filter_work", GigKinds)
            .Empty("reallife_hustle_empty_freelance");
    }
}
