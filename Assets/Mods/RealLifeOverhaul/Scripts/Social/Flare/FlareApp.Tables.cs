using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The Flare app's tables, declared as data (GameUi.TableSpec), all in this one place: for each tab its columns,
    /// how each sorts, what its rows can be filtered by, the order it opens in, and the sentence shown when a filter
    /// leaves nothing. The numbers beside each are the columns, for whoever writes the rows.
    /// </summary>
    internal static class FlareTables
    {
        // ---- whose a post of the Feed is (its filter)
        public const int IsYours = 0, IsFriends = 1, IsAccounts = 2, IsFollowed = 3;
        private static readonly string[] Whose = { "reallife_flare_kind_yours", "reallife_flare_kind_friends", "reallife_flare_kind_accounts", "reallife_flare_kind_followed" };

        // ---- what an account of the City tab is (its filter)
        public const int IsInfluencer = 0, IsBrand = 1, IsCandidate = 2;
        private static readonly string[] Accounts = { "reallife_flare_kind_influencers", "reallife_flare_kind_brands", "reallife_flare_kind_candidates" };

        /// <summary>While Flare is not running: one empty column.</summary>
        public static readonly TableSpec Off = new TableSpec("Flare", "Off").Column("reallife_flare_col_post", 600f, TableSort.None, flexible: true);

        /// <summary>Feed: 0 who, 1 the post, 2 likes, 3 replies, 4 when (by the hour). Newest first.</summary>
        public static readonly TableSpec Feed = new TableSpec("Flare", "Feed")
            .Column("reallife_flare_col_who", 300f, TableSort.Text)
            .Column("reallife_flare_col_post", 900f, TableSort.Text, flexible: true)
            .Column("reallife_flare_col_likes", 180f, TableSort.Number)
            .Column("reallife_flare_col_replies", 180f, TableSort.Number)
            .Column("reallife_flare_col_when", 280f, TableSort.Date, right: true)
            .Filter("reallife_tab_all_kinds", Whose)
            .Order(4, false)
            .Empty("reallife_flare_empty_feed");

        /// <summary>Post: 0 what happened, 1 when, 2 what it is worth (quality, per cent). Newest first.</summary>
        public static readonly TableSpec Drafts = new TableSpec("Flare", "Post")
            .Column("reallife_flare_col_what", 900f, TableSort.Text, flexible: true)
            .Column("reallife_flare_col_when", 280f, TableSort.Date)
            .Column("reallife_flare_col_worth", 220f, TableSort.Number, right: true)
            .Order(1, false)
            .Empty("reallife_flare_empty_drafts");

        /// <summary>Me: 0 the post, 1 when, 2 seen by, 3 likes, 4 followers it brought. Newest first.</summary>
        public static readonly TableSpec Mine = new TableSpec("Flare", "Me")
            .Column("reallife_flare_col_post", 800f, TableSort.Text, flexible: true)
            .Column("reallife_flare_col_when", 260f, TableSort.Date)
            .Column("reallife_flare_col_reach", 200f, TableSort.Number)
            .Column("reallife_flare_col_likes", 160f, TableSort.Number)
            .Column("reallife_flare_col_gained", 200f, TableSort.Number, right: true)
            .Order(1, false)
            .Empty("reallife_flare_empty_mine");

        /// <summary>Business: 0 business, 1 its stars (the game's own satisfaction), 2 the talk on Flare now, 3 posts about it, 4 the last one.</summary>
        public static readonly TableSpec Business = new TableSpec("Flare", "Business")
            .Column("reallife_flare_col_business", 620f, TableSort.Text, flexible: true)
            .Column("reallife_flare_col_stars", 220f, TableSort.Number)
            .Column("reallife_flare_col_talk", 220f, TableSort.Number)
            .Column("reallife_flare_col_posts", 200f, TableSort.Number)
            .Column("reallife_flare_col_last", 260f, TableSort.Date, right: true)
            .Empty("reallife_flare_empty_business");

        /// <summary>City: 0 account, 1 niche or kind, 2 followers, 3 home district, 4 followed by the player.</summary>
        public static readonly TableSpec City = new TableSpec("Flare", "City")
            .Column("reallife_flare_col_account", 520f, TableSort.Text, flexible: true)
            .Column("reallife_flare_col_niche", 300f, TableSort.Text)
            .Column("reallife_flare_col_followers", 240f, TableSort.Number)
            .Column("reallife_flare_col_district", 320f, TableSort.Text)
            .Column("reallife_flare_col_following", 220f, TableSort.Text, right: true)
            .Filter("reallife_tab_all_kinds", Accounts)
            .Order(2, false)
            .Empty("reallife_flare_empty_city");
    }
}
