using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The blueprints window as a player meets it: a library of fourteen on its first page with one of their own chosen (what is inside it counted),
/// one they have shared on the Workshop (an update waiting, an older build's warning), the Workshop with a blueprint they could add, the
/// Workshop while it loads and with no connection, the filters and the sort open, and the Workshop's terms before an upload. The thumbnails
/// are the game's own pictures (the preview's backdrops), as the game loads a blueprint's own.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] BlueprintStates = { "library", "library_shared", "workshop", "workshop_loading", "workshop_offline", "filters", "sort", "terms" };

    private static readonly string[] BlueprintPictures = { "05", "00", "07", "06", "03", "02", "09", "10", "12", "08", "01", "04", "11", "13", "00", "05" };

    private static UiBlueprintCard Card(string name, string author, int halves, string rating, string size, string state, UiRole tone, int picture, bool old = false) => new UiBlueprintCard
    {
        Name = name, Author = author, Halves = halves, Rating = rating, Size = size, State = state, StateTone = tone, Old = old,
        Picture = "bpthumb:" + BlueprintPictures[picture % BlueprintPictures.Length]
    };

    /// <summary>The blueprints window in a state, in the words given.</summary>
    public static UiBlueprintsModel Blueprints(UiWords t, string state)
    {
        bool workshop = state.StartsWith("workshop", System.StringComparison.Ordinal);
        bool shared = state == "library_shared", loading = state == "workshop_loading", offline = state == "workshop_offline";
        string by = t("reallife_ui_bp_by", "by {author}");
        string yours = t("reallife_ui_bp_yours", "Yours"), sharedChip = t("reallife_ui_bp_shared", "Shared"), subscribed = t("reallife_ui_bp_subscribed", "Subscribed");
        string inLibrary = t("reallife_ui_bp_in_library", "In your library");
        var m = new UiBlueprintsModel
        {
            Title = t("common_blueprints", "Blueprints"),
            Tabs = new[]
            {
                new UiNavEntry { Icon = UiIcon.Books, Label = t("reallife_ui_bp_library", "Library"), Count = "14" },
                new UiNavEntry { Icon = UiIcon.People, Label = t("reallife_ui_bp_workshop", "Workshop"), Count = "31" }
            },
            Tab = workshop ? 1 : 0,
            SearchPlaceholder = t("common_search", "Search"),
            SortLabel = t("reallife_ui_bp_sort", "Sort: {sort}").Replace("{sort}", workshop ? "Top rated" : "Newest"),
            SortOptions = new[] { "Most subscribed", "Top rated", "Newest" },
            SortChosen = workshop ? 1 : 2,
            SortOpen = state == "sort",
            FiltersLabel = state == "filters" ? t("reallife_ui_bp_filters_on", "Filters {n}").Replace("{n}", "1") : t("reallife_ui_bp_filters", "Filters"),
            FiltersOn = state == "filters" ? 1 : 0,
            FiltersOpen = state == "filters",
            FilterRows = FilterRows(t, state == "filters"),
            PageLine = BlueprintsRules.PageLine(t, 1, workshop ? -1 : 1),
            PrevLabel = t("reallife_ui_bp_prev", "Previous"),
            NextLabel = t("reallife_ui_bp_next", "Next"),
            CanPrev = false,
            CanNext = workshop,
            Loading = loading,
            LoadingText = t("reallife_ui_bp_loading", "Loading blueprints"),
            EmptyTitle = t("reallife_ui_bp_none", "Nothing to show here"),
            EmptyBody = offline ? t("blueprint_list_no_results_connection", "The Steam Workshop is not reachable. Check your connection and open this window again.")
                : t("reallife_ui_bp_none_body", "No blueprints match what you asked for."),
            EmptyIcon = offline ? UiIcon.Globe : UiIcon.Layout,
            DetailEmpty = t("reallife_ui_bp_pick", "Choose a blueprint to see what it holds and what you can do with it.")
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") }
        };
        m.PromptCount = m.Prompts.Length;
        if (loading || offline)
        {
            return m;
        }
        if (!workshop)
        {
            m.Cards = new[]
            {
                Card("Corner cafe, two floors", null, -1, null, "Medium2", yours, UiRole.Accent, 0),
                Card("Bakery with seating", by.Replace("{author}", "ravi_builds"), 9, "4.6", "Medium1", subscribed, UiRole.Notice, 1),
                Card("Compact grocery", null, 8, "4.4", "Small1", sharedChip, UiRole.Ok, 2, shared),
                Card("Garment studio", by.Replace("{author}", "stitchpoint"), 9, "4.7", "Large1", subscribed, UiRole.Notice, 3),
                Card("Night club with a bar", null, -1, null, "Large2", yours, UiRole.Accent, 4),
                Card("Dockside office", by.Replace("{author}", "harbour_hal"), 9, "4.5", "Small2", subscribed, UiRole.Notice, 5),
                Card("Tiny gift shop", null, -1, null, "Small1", yours, UiRole.Accent, 6),
                Card("Factory line, six stations", by.Replace("{author}", "gearhead"), 10, "4.9", "Large2", subscribed, UiRole.Notice, 7),
                Card("Warehouse and loading bay", null, -1, null, "Large1", yours, UiRole.Accent, 8),
                Card("Restaurant with a terrace", by.Replace("{author}", "ravi_builds"), 8, "4.3", "Medium2", subscribed, UiRole.Notice, 9),
                Card("Family home and pool", null, -1, null, "Medium1", yours, UiRole.Accent, 10),
                Card("Pop-up flower stand", by.Replace("{author}", "stitchpoint"), 8, "3.9", "Small1", subscribed, UiRole.Notice, 11)
            };
            m.CardCount = m.Cards.Length;
            m.Chosen = shared ? 2 : 0;
        }
        else
        {
            m.Cards = new[]
            {
                Card("Bayside diner", by.Replace("{author}", "harbour_hal"), 10, "4.9", "Medium1", null, UiRole.None, 3),
                Card("Corner cafe, two floors", by.Replace("{author}", "kettlecraft"), 10, "4.8", "Medium2", inLibrary, UiRole.Ok, 0),
                Card("Bakery with seating", by.Replace("{author}", "ravi_builds"), 9, "4.6", "Medium1", inLibrary, UiRole.Ok, 1),
                Card("Open plan studio floor", by.Replace("{author}", "marlab"), 9, "4.4", "Large1", null, UiRole.None, 4),
                Card("Compact grocery", by.Replace("{author}", "marlab"), 9, "4.4", "Small1", null, UiRole.None, 2),
                Card("Pocket cinema", by.Replace("{author}", "oaktree"), 8, "4.1", "Large2", null, UiRole.None, 5),
                Card("Tiny gift shop", by.Replace("{author}", "marlab"), 8, "4.1", "Small1", null, UiRole.None, 6),
                Card("Greenhouse market", by.Replace("{author}", "oaktree"), 9, "4.5", "Medium2", null, UiRole.None, 8),
                Card("Print shop and copy desk", by.Replace("{author}", "gearhead"), 7, "3.8", "Small2", null, UiRole.None, 9, true),
                Card("Lounge and cocktail bar", by.Replace("{author}", "dockhand"), 8, "4.2", "Large1", null, UiRole.None, 10),
                Card("Family home and pool", by.Replace("{author}", "oaktree"), 10, "4.8", "Medium1", null, UiRole.None, 11),
                Card("Bike repair and rental", by.Replace("{author}", "stitchpoint"), 7, "3.9", "Small1", null, UiRole.None, 12)
            };
            m.CardCount = m.Cards.Length;
            m.Chosen = 0;
            m.CardCount = m.Cards.Length;
        }
        if (state == "workshop_search")
        {
            m.Search = "cafe";
        }
        Detail(t, m, workshop, shared);
        if (state == "terms")
        {
            m.Confirm = true;
            m.ConfirmTitle = t("reallife_ui_bp_terms_title", "Upload to the Steam Workshop");
            m.ConfirmBody = t("reallife_ui_bp_terms_body", "Anyone can then add this blueprint to their library. By uploading it you agree to Steam's Workshop terms of service.");
            m.ConfirmYes = t("reallife_ui_bp_terms_yes", "Upload");
            m.ConfirmNo = t("reallife_ui_bp_terms_no", "Cancel");
            m.ConfirmTerms = t("reallife_ui_bp_terms_read", "Read the terms");
        }
        return m;
    }

    private static void Detail(UiWords t, UiBlueprintsModel m, bool workshop, bool shared)
    {
        UiBlueprintCard c = m.Cards[m.Chosen];
        m.HasDetail = true;
        m.DetailPicture = c.Picture;
        m.DetailKicker = "Retail";
        m.DetailName = c.Name;
        m.DetailHalves = c.Halves;
        m.DetailRating = c.Rating;
        m.DetailAuthor = workshop ? t("reallife_ui_bp_by", "by {author}").Replace("{author}", "kettlecraft") : shared ? t("reallife_ui_bp_by", "by {author}").Replace("{author}", "you") : null;
        m.DetailState = c.State;
        m.DetailStateTone = c.StateTone;
        m.FactsTitle = t("reallife_ui_bp_facts", "Facts");
        var labels = new List<string> { t("common_building_size", "Building size"), t("reallife_ui_bp_cost", "Cost to place") };
        var values = new List<string> { BlueprintsRules.SizeLine(t, c.Size, 2), workshop ? "$48,200" : "$31,950" };
        if (workshop || shared)
        {
            labels.Add(t("reallife_ui_bp_subscribers", "Subscribers"));
            values.Add("3,412");
            labels.Add(t("reallife_ui_bp_released", "Released"));
            values.Add("12 Oct 2026");
        }
        labels.Add("Business type");
        values.Add("Cafe");
        if (!workshop)
        {
            labels.Add(t("reallife_ui_bp_items", "Items"));
            values.Add("64");
        }
        m.FactLabels = labels.ToArray();
        m.FactValues = values.ToArray();
        m.InsideTitle = t("reallife_ui_bp_inside", "Inside");
        if (workshop)
        {
            m.InsideNote = t("reallife_ui_bp_not_here", "Add it to your library to see what it holds.");
        }
        else
        {
            m.InsideLabels = new[]
            {
                t("reallife_ui_bp_in_counters", "Counters and tills"), t("reallife_ui_bp_in_shelves", "Shelves and displays"), t("reallife_ui_bp_in_tables", "Tables"),
                t("reallife_ui_bp_in_seats", "Seats"), t("reallife_ui_bp_in_machines", "Machines")
            };
            m.InsideValues = new[] { "6", "4", "8", "24", "3" };
        }
        m.Warnings = c.Old || shared ? new[] { t("reallife_ui_bp_older", "Made in an older version of the game ({version})").Replace("{version}", "0.62") } : new string[0];
        if (workshop)
        {
            m.Actions = new[]
            {
                new UiActionModel { Label = "Add to library", Icon = UiIcon.Download, Key = UiAction.Alt, Kind = UiButtonKind.Primary, Command = UiBlueprints.ActionCommand }
            };
            if (m.Cards[m.Chosen].State != null)
            {
                m.Actions = new UiActionModel[0];
            }
        }
        else
        {
            m.Actions = new[]
            {
                new UiActionModel { Label = shared ? "Update on the Workshop" : "Upload to the Workshop", Icon = UiIcon.Upload,
                    Key = UiAction.Alt, Kind = UiButtonKind.Primary, Command = UiBlueprints.ActionCommand, Arg = 0 },
                new UiActionModel { Label = "Remove from library", Icon = UiIcon.Trash, Key = UiAction.Count, Kind = UiButtonKind.Secondary,
                    Command = UiBlueprints.ActionCommand, Arg = 1 }
            };
        }
    }

    private static UiBlueprintFilterRow[] FilterRows(UiWords t, bool open)
    {
        var rows = new List<UiBlueprintFilterRow>(40);
        void Group(string name, params string[] options)
        {
            rows.Add(new UiBlueprintFilterRow { Label = name, Group = true });
            rows.Add(new UiBlueprintFilterRow { Label = t("common_all", "All"), On = name != t("common_building_type", "Building type") });
            for (int i = 0; i < options.Length; i++)
            {
                rows.Add(new UiBlueprintFilterRow { Label = options[i], On = name == t("common_building_type", "Building type") && i == 1 });
            }
        }
        Group(t("common_building_type", "Building type"), "Residential", "Retail", "Office", "Industrial");
        Group(t("common_building_size", "Building size"), "Small1", "Small2", "Medium1", "Medium2", "Large1", "Large2");
        Group(t("blueprintdata_businesstypename", "Business type"), "Bakery", "Cafe", "Clothing store", "Gift shop", "Grocery", "Night club", "Restaurant", "Warehouse");
        return rows.ToArray();
    }
}
