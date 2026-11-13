using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the blueprints window works out for itself, game-free: a blueprint's rating as stars and as a figure, what a layout
    /// holds counted by the game's item types, the search over the page that is loaded, the page's words and the sort the
    /// window offers each tab. The game's own lists, paging and Steam fetches stay the game's. Cost: a pass over a layout's items once
    /// a blueprint is chosen; the rest are a few string checks.
    /// </summary>
    public static class BlueprintsRules
    {
        /// <summary>The game's page of blueprints (BlueprintsListUI.MaxNumberOnPage).</summary>
        public const int PageSize = 16;

        /// <summary>The kinds of thing the Inside list counts, in the order it lists them.</summary>
        public const int KindCount = 6;

        public const int Counters = 0, Shelves = 1, Tables = 2, Seats = 3, Workstations = 4, Machines = 5;

        // The game's ItemType flags (BigAmbitions.Items.ItemType), by value so the rule stays game-free; ContractCheck holds them to the game.
        private const long PointOfSale = 0x8, StorageShelf = 0x20, ShowcaseShelf = 0x40, FactoryMachine = 0x80, Seat = 0x1000, Desk = 0x20000;
        private const long WorkoutMachine = 0x100000, Table = 0x200000, Computer = 0x800000, EmployeeWorkstation = 0x10;

        /// <summary>The locale keys of the Inside lines, by kind.</summary>
        public static readonly string[] KindKeys =
        {
            "reallife_ui_bp_in_counters", "reallife_ui_bp_in_shelves", "reallife_ui_bp_in_tables", "reallife_ui_bp_in_seats", "reallife_ui_bp_in_workstations",
            "reallife_ui_bp_in_machines"
        };

        /// <summary>The icon of each kind, for the Inside lines.</summary>
        public static readonly UiIcon[] KindIcons = { UiIcon.Receipt, UiIcon.Package, UiIcon.Cube, UiIcon.Sofa, UiIcon.Briefcase, UiIcon.Factory };

        /// <summary>
        /// Which kind an item of these flags counts under, or -1 for a thing that is none of them (a lamp, a plant). One kind a
        /// thing, the first that fits: a till is a counter even when it also stands on a table.
        /// </summary>
        public static int KindOf(long itemType)
        {
            if ((itemType & PointOfSale) != 0)
            {
                return Counters;
            }
            if ((itemType & (StorageShelf | ShowcaseShelf)) != 0)
            {
                return Shelves;
            }
            if ((itemType & Table) != 0)
            {
                return Tables;
            }
            if ((itemType & Seat) != 0)
            {
                return Seats;
            }
            if ((itemType & (EmployeeWorkstation | Desk | Computer)) != 0)
            {
                return Workstations;
            }
            return (itemType & (FactoryMachine | WorkoutMachine)) != 0 ? Machines : -1;
        }

        /// <summary>The game keeps a rating from 0 to 1; the window shows it out of five, by halves (0 to 10).</summary>
        public static int Halves(float rating)
        {
            if (float.IsNaN(rating) || rating <= 0f)
            {
                return 0;
            }
            return Math.Min(10, (int)Math.Round(rating * 10f, MidpointRounding.AwayFromZero));
        }

        /// <summary>The rating as the figure beside the stars: out of five, one decimal ("4.8").</summary>
        public static string RatingFigure(float rating) => (Math.Max(0f, Math.Min(1f, rating)) * 5f).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>A blueprint carries a rating only once Steam has told the game one: the local ones never uploaded have none to show.</summary>
        public static bool Rated(bool fromSteam, float rating) => fromSteam && rating > 0f;

        /// <summary>
        /// True when a name holds every word of a query, whatever the case and the accents (the game's own help search compares
        /// the same way): the search over the page of blueprints that is loaded.
        /// </summary>
        public static bool Matches(string name, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            CompareInfo compare = CultureInfo.InvariantCulture.CompareInfo;
            string[] words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (compare.IndexOf(name, words[i], CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>"Page 2 of 3", or "Page 2" while the Workshop has not said how many there are (the game's own total is -1 then).</summary>
        public static string PageLine(UiWords t, int page, int total)
        {
            string p = Math.Max(1, page).ToString(CultureInfo.InvariantCulture);
            return total > 0
                ? t("reallife_ui_bp_page_of", "Page {page} of {total}").Replace("{page}", p).Replace("{total}", total.ToString(CultureInfo.InvariantCulture))
                : t("reallife_ui_bp_page", "Page {page}").Replace("{page}", p);
        }

        /// <summary>"3 of 16": how many cards the search leaves of those loaded.</summary>
        public static string CountLine(UiWords t, int shown, int all) =>
            t("reallife_ui_bp_count", "{shown} of {all}").Replace("{shown}", shown.ToString(CultureInfo.InvariantCulture)).Replace("{all}", all.ToString(CultureInfo.InvariantCulture));

        /// <summary>"Library 14": a tab's label has its count in its own slot; the count reads as a figure, never zero padded.</summary>
        public static string Count(int n) => n.ToString("N0", CultureInfo.CurrentCulture);

        /// <summary>The size line of a detail: the building's size as the game names it, then its floors when it has more than one.</summary>
        public static string SizeLine(UiWords t, string size, int floors)
        {
            if (floors <= 1)
            {
                return size;
            }
            return t("reallife_ui_bp_size_floors", "{size}, {floors} floors").Replace("{size}", size).Replace("{floors}", floors.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The game's key for a count of downloads (Blueprint.GetDownloadsLabel), with the number its text takes in <paramref name="number"/>: none and one have a key of their own
        /// and no number; below a thousand the count, below a million the thousands, above that the millions.
        /// </summary>
        public static string DownloadsKey(float downloads, out string number)
        {
            int n = (int)Math.Round(downloads);   // as Mathf.RoundToInt rounds, which the game's label uses
            number = null;
            if (n <= 0)
            {
                return "blueprints_no_downloads";
            }
            if (n == 1)
            {
                return "blueprints_one_download";
            }
            if (n < 1000)
            {
                number = n.ToString(CultureInfo.InvariantCulture);
                return "blueprints_downloads_amount";
            }
            if (n < 1000000)
            {
                number = (n / 1000).ToString(CultureInfo.InvariantCulture);
                return "blueprints_downloads_amount_thousands";
            }
            number = (n / 1000000).ToString(CultureInfo.InvariantCulture);
            return "blueprints_downloads_amount_millions";
        }

        /// <summary>Subscribers as the game counts them: whole numbers, thousands grouped.</summary>
        public static string Subscribers(float downloads) => Math.Max(0, (int)Math.Round(downloads)).ToString("N0", CultureInfo.CurrentCulture);
    }
}
