using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The game's help as a player meets it: the Gift Shop page of its business types open with its links (a list of pages and a
/// sentence that names two more), the same page after a search has narrowed the list, a search nothing matches, a page with a picture or a
/// video (the game's own view shows it), and one of the mod's own Staff and roles pages that the game's list holds beside its own.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] HelpStates = { "page", "search", "none", "media", "staff" };

    private const string GiftShop =
        "**Gift Shop** businesses operate out of retail buildings.\n\n"
        + "Some of this store's items can be ordered from [Wholesalers](wholesalers-locations) and the rest can be ordered from [Importers](importers-contract) in your [Headquarters](businesstypes-headquarters).\n\n"
        + "Customers are self-serving.\n\nThe business requires the following furniture to function:\n\n"
        + "* [Stack of Shopping Baskets](furniture-stackofshoppingbaskets)\n* [Point of Sales](furniture-itemgrouppointofsale)\n* At least one product to sell (see below)\n\n"
        + "Businesses of this type primarily sell:\n\n* [Gift (Cheap)](products-cheapgift)\n* [Gift (Expensive)](products-expensivegift)\n* [Umbrella](products-umbrella)\n\n"
        + "And can additionally sell:\n\n* [Energy Drink](products-energydrink)\n* [Flower (Cheap)](products-cheapflower)\n* [Picture Book](products-picturebook)\n\n"
        + "Employees with the following skills can be assigned:\n\n* [Customer Service](skill-customerservice)\n* [Cleaning](skill-cleaning)";

    private const string HelpStaffPage =
        "**Barista** is a role of a cafe, a bakery and a coffee stand.\n\nA barista makes the drinks at the counter and takes the orders when no one else is on the till.\n\n"
        + "* Works shifts of up to eight hours\n* Is paid by the hour\n* Counts toward the cafe's service rating\n\nThe roles that work beside them:\n\n"
        + "* [Cashier](staff-cashier)\n* [Cleaner](staff-cleaner)";

    private static readonly string[] HelpCategories = { "General", "Finance", "Buildings", "Business types", "Employee types", "Furniture", "Staff and roles, places" };

    private static readonly string[][] HelpPagesOf =
    {
        new[] { "Movement", "Energy", "Hunger", "Happiness", "Personal goals", "BizPhone" },
        new[] { "Loans", "Taxes", "Credit score" },
        new[] { "Building sizes", "Renting", "Buying", "The interior designer" },
        new[] { "Bakery", "Cafe", "Clothing store", "Gift shop", "Grocery", "Restaurant" },
        new[] { "Cashier", "Cleaner", "Cook", "Manager" },
        new[] { "Counters", "Shelves", "Tables and chairs", "Kitchen machines" },
        new[] { "Cafe", "Bakery", "Gift shop", "Grocery" }
    };

    private static readonly UiIcon[] HelpIcons = { UiIcon.Info, UiIcon.Coin, UiIcon.Building, UiIcon.Store, UiIcon.People, UiIcon.Sofa, UiIcon.Briefcase };

    /// <summary>The help window in a state, in the words given.</summary>
    public static UiHelpModel Help(UiWords t, string state)
    {
        bool searching = state == "search" || state == "none";
        var nav = new List<UiNavEntry>(32);
        int selected = -1;
        for (int c = 0; c < HelpCategories.Length; c++)
        {
            for (int p = 0; p < HelpPagesOf[c].Length; p++)
            {
                string name = HelpPagesOf[c][p];
                if (searching && state == "search" && name.IndexOf("gift", System.StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("shop", System.StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("cafe", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (state == "none")
                {
                    continue;
                }
                if (c == 3 && name == "Gift shop" && state != "staff")
                {
                    selected = nav.Count;
                }
                if (c == 6 && name == "Cafe" && state == "staff")
                {
                    selected = nav.Count;
                }
                nav.Add(new UiNavEntry { Icon = HelpIcons[c], Label = name, Group = HelpCategories[c] });
            }
        }
        var m = new UiHelpModel
        {
            Title = t("reallife_ui_help_title", "Help"),
            Nav = nav.ToArray(),
            Selected = selected,
            SearchPlaceholder = t("common_search", "Search"),
            Search = state == "search" ? "shop" : state == "none" ? "zeppelin" : null,
            SearchCount = state == "search" ? t("reallife_ui_help_found", "{n} pages").Replace("{n}", nav.Count.ToString()) : state == "none" ? t("reallife_ui_help_found", "{n} pages").Replace("{n}", "0") : null,
            BackLabel = t("reallife_ui_help_back", "Back"),
            ForwardLabel = t("reallife_ui_help_forward", "Forward"),
            CanBack = true,
            CanForward = state == "search",
            HasPage = state != "none",
            EmptyTitle = t("reallife_ui_help_none", "No page matches"),
            EmptyBody = t("reallife_ui_help_none_body", "Nothing in the help mentions \"{search}\". Try a shorter word, or clear the search to see every page.").Replace("{search}", "zeppelin"),
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") }
            }
        };
        m.PromptCount = m.Prompts.Length;
        var slugs = new List<string>(16);
        UiReaderBlock[] blocks = HelpRules.Parse(state == "staff" ? HelpStaffPage : GiftShop, slugs, out bool media);
        if (state == "media")
        {
            var more = new UiReaderBlock[blocks.Length + 2];
            more[0] = new UiReaderBlock { Kind = UiReaderKind.Note, Text = t("reallife_ui_help_media", "This page has a picture or a video. The game shows them in its own view.") };
            more[1] = new UiReaderBlock { Kind = UiReaderKind.Link, Text = t("reallife_ui_help_media_open", "Open this page in the game's own view"), Target = slugs.Count };
            System.Array.Copy(blocks, 0, more, 2, blocks.Length);
            blocks = more;
        }
        m.Page.Title = state == "staff" ? "Barista" : "Gift shop";
        m.Page.Blocks = blocks;
        m.Page.Picture = state == "staff" ? UiIcon.Briefcase : UiIcon.Store;
        m.Page.PictureCaption = state == "staff" ? HelpCategories[6] : HelpCategories[3];
        m.Page.RelatedTitle = t("reallife_ui_help_next", "Next in this category");
        m.Page.Related = state == "staff" ? new[] { "Bakery", "Gift shop" } : new[] { "Grocery", "Restaurant" };
        return m;
    }
}
