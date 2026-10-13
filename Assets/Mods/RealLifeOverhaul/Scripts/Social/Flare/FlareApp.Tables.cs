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
        public const int IsYours = 0, IsFriends = 1;
        private static readonly string[] Whose = { "reallife_flare_kind_yours", "reallife_flare_kind_friends" };

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

        /// <summary>Business: 0 business, 1 posts about it, 2 the last one.</summary>
        public static readonly TableSpec Business = new TableSpec("Flare", "Business")
            .Column("reallife_flare_col_business", 700f, TableSort.Text, flexible: true)
            .Column("reallife_flare_col_posts", 220f, TableSort.Number)
            .Column("reallife_flare_col_last", 280f, TableSort.Date, right: true)
            .Empty("reallife_flare_empty_business");
    }
}
