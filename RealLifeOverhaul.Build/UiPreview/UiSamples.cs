using System;
using RealLifeOverhaul.Ui;

// The kit's sample view models: the owner's Contacts wireframe's own people and words (not-in-repo/design/wireframes,
// contacts_source.html), for the offline previews and EconomyCheck's layout checks. Fixtures, never shipped: the mod's
// screens build their models from the game's data and the locale keys. Words a screen labels with go through the given
// text source, so the checks measure every language; the fixtures' names and messages stay as the wireframe has them.
internal static class UiSamples
{
    /// <summary>The masthead's money, day and time in every preview and check: the game's own bar writes them so.</summary>
    public const string Status = "$48,210     Tuesday (Day 12)     14:32";

    /// <summary>A screen of the overhaul drawn from its own view model, with the app it stands in.</summary>
    public sealed class Sample
    {
        public string Name;
        public string App;
        public Action<UiScene, UiFrame, bool, IUiKeys> Lay;
    }

    /// <summary>The overhaul's screens as their own runners draw them from sample view models.</summary>
    public static System.Collections.Generic.List<Sample> Overhaul(UiWords t)
    {
        return new System.Collections.Generic.List<Sample>();
    }

    /// <summary>The wireframe's Contacts with Maya Okafor selected, as the kit's list-and-detail template draws it.</summary>
    public static UiListDetailModel Contacts(UiWords t, bool composer)
    {
        var m = new UiListDetailModel
        {
            AppName = t("reallife_ui_contacts_app", "Contacts"),
            GroupsLabel = t("reallife_ui_contacts_groups", "Groups"),
            PlayerName = "Richy Man",
            PlayerHue = 40f,
            PlayerSummary = "13 contacts · 5 unread",
            ListTitle = t("reallife_ui_contacts_all", "All contacts"),
            ListCount = "13",
            SearchPlaceholder = t("reallife_ui_contacts_search", "Search people, shops, agencies"),
            SearchCount = "13",
            ListEmptyTitle = t("reallife_ui_contacts_none_title", "No contact matches"),
            ListEmptyBody = t("reallife_ui_contacts_none_body", "Try another name, or clear the search."),
            SideNote = t("reallife_ui_contacts_clear_note", "Clearing messages never removes a contact."),
            HasDetail = true,
            Name = "Maya Okafor",
            Hue = 12f,
            Role = "Customer Service · Bloom Café",
            Status = t("reallife_ui_contacts_on_shift", "On shift"),
            Tone = UiTone.Ok,
            ThreadEmptyTitle = t("reallife_ui_contacts_quiet_title", "Quiet line"),
            ThreadEmptyBody = t("reallife_ui_contacts_quiet_body", "No messages yet. They are still saved in your contacts."),
            ThreadStart = t("reallife_ui_contacts_start", "Start of your messages with {name}").Replace("{name}", "Maya Okafor"),
            ClassicLabel = t("reallife_ui_contacts_classic", "Answer in the game's view"),
            Composer = composer,
            ComposerPlaceholder = t("reallife_ui_contacts_message", "Message Maya"),
            SendLabel = t("reallife_ui_contacts_send", "Send"),
            NoReply = t("reallife_ui_contacts_no_reply", "This line takes no messages. Use the actions above."),
            ServicesTitle = t("reallife_ui_contacts_requests", "Requests and actions"),
            StatsTitle = t("reallife_ui_contacts_profile", "Profile"),
            DetailsTitle = t("reallife_ui_contacts_details", "Details"),
            NoDetailTitle = t("reallife_ui_contacts_pick_title", "Pick a contact"),
            NoDetailBody = t("reallife_ui_contacts_pick_body", "Their messages, actions and details show here."),
        };
        m.Groups = new[]
        {
            Nav(t("reallife_ui_contacts_all", "All contacts"), "13", 12f, true),
            Nav(t("contactcategoryname_general", "General"), "3", 250f, false),
            Nav(t("contactcategoryname_business", "Business"), "4", 320f, false),
            Nav(t("contactcategoryname_employees", "Employees"), "3", 155f, false),
            Nav(t("contactcategoryname_importsandgoods", "Imports / Goods"), "1", 75f, false),
            Nav(t("contactcategoryname_furnitureandequipment", "Furniture / Equipment"), "1", 45f, false),
            Nav(t("contactcategoryname_rivals", "Rivals"), "1", 25f, false),
        };
        m.GroupCount = m.Groups.Length;
        m.SideActions = new[]
        {
            new UiActionModel { Label = t("contacts_mark_all_as_read_button", "Mark all as read"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count, Command = 0 },
            new UiActionModel { Label = t("contacts_delete_all_button", "Delete all"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count, Command = 1 },
        };
        m.SideActionCount = m.SideActions.Length;
        // The unread first, as the game lists them, then the rest: the two blocks the real screen draws.
        m.Rows = new[]
        {
            Row("Luis Romero", "Van needs fuel before the afternoon run. Okay to use the company card?", "10:02", 1, 155f, true, false, false, t("reallife_ui_contacts_new", "New messages")),
            Row("Uncle Fred", "Good. Once you’ve got keys, open your first shop. I’ll check in.", "09:02", 1, 40f, true, false, false, null),
            Row("Anderson Recruitment Corp.", "Shall I send the shortlist over?", "08:52", 2, 320f, true, true, false, null),
            Row("City Office", "Fine of $125 deducted from your bank account.", "07:40", 1, 300f, false, true, false, null),
            Row("Maya Okafor", "Happy to cover Tuesday evenings instead.", "09:15", 0, 12f, true, false, true, t("reallife_ui_contacts_earlier", "Earlier")),
            Row("NY AutoTow Service", "Stuck somewhere? One call and we’ll haul you out, any hour.", "Sun", 0, 230f, true, false, false, null),
            Row("City Workforce Inc.", "Your HQ application is approved. Let us know which roles to fill.", "Thu", 0, 275f, true, false, false, null),
            Row("CityAds", "Your billboard on 7th Ave ends Friday. Renew?", "Wed", 0, 350f, false, false, false, null),
            Row("McCain’s eMarketing", "Click-through on your last campaign was up 14%.", "Mon", 0, 200f, true, false, false, null),
            Row("Hana Sato", "Requesting a better desk chair. My back is filing a complaint.", "Mon", 0, 180f, false, false, false, null),
            Row("Harbor Line Wholesale", "Container of coffee beans landed. Your Monday delivery is on track.", "Sat", 0, 75f, true, false, false, null),
            Row("Lumen & Oak Interiors", "The walnut counters you asked about are back in stock.", "Fri", 0, 50f, true, false, false, null),
            Row("Vex Holdings", "Cute little café. Shame if the block got crowded.", "Thu", 0, 25f, false, false, false, null),
        };
        m.RowCount = m.Rows.Length;
        m.Actions = new[]
        {
            new UiActionModel { Label = t("phone_view_on_map_button", "View on map"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option, Command = 0 },
            new UiActionModel { Label = t("phone_call_button", "Call"), Kind = UiButtonKind.Primary, Icon = UiIcon.Phone, Key = UiAction.Alt, Command = 1 },
        };
        m.ActionCount = m.Actions.Length;
        m.Messages = new[]
        {
            new UiMessageModel { From = UiFrom.Them, Text = "Morning! Café’s busy, the new grinder is great.", Time = "07:58" },
            new UiMessageModel { From = UiFrom.Them, Text = "Could I drop the Sunday shift? I’ve picked up a class on weekends.", Time = "09:14" },
            new UiMessageModel { From = UiFrom.Them, Text = "Happy to cover Tuesday evenings instead.", Time = "09:15" },
            new UiMessageModel { From = UiFrom.Me, Text = "Let me look at the rota tonight.", Time = "09:20" },
        };
        m.MessageCount = m.Messages.Length;
        m.Replies = new[] { "Approved", "Let’s talk Friday", "Thanks, Maya" };
        m.ReplyCount = m.Replies.Length;
        m.Services = new[]
        {
            new UiServiceModel { Title = "Approve schedule change", Price = t("reallife_ui_price_free", "Free"), About = "Remove her Sunday shift." },
            new UiServiceModel { Title = "Pay a bonus", Price = "$200", About = "Raises satisfaction for a few weeks." },
            new UiServiceModel { Title = "Negotiate salary", Price = t("reallife_ui_price_quote", "Quote"), About = "Currently $22/h. She’s asking for $24/h." },
        };
        m.ServiceCount = m.Services.Length;
        m.Stats = new[]
        {
            new UiStatModel { Label = "Skill", Figure = "82", Value = 82 },
            new UiStatModel { Label = "Satisfaction", Figure = "64", Value = 64 },
            new UiStatModel { Label = "Energy", Figure = "48", Value = 48 },
        };
        m.StatCount = m.Stats.Length;
        m.Details = new[]
        {
            new UiPairModel { Key = t("common_address", "Address"), Value = "14 Bleecker St" },
            new UiPairModel { Key = t("myemployees_hourlywage", "Hourly wage"), Value = "$22" },
            new UiPairModel { Key = t("contacts_schedule_demands", "Schedule demands"), Value = "No Sundays" },
        };
        m.DetailCount = m.Details.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_group", "Group") },
            new UiPrompt { Action = UiAction.Alt, Label = t("phone_call_button", "Call") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The gallery on one of its pages: the screen's own model, so the previews and the checks see what the game shows.</summary>
    public static UiGalleryModel Gallery(UiWords t, int page) => UiGalleryScreen.Model(t, page, Dollars);

    /// <summary>Money as the game writes it in English: a dollar sign and thousands.</summary>
    public static string Dollars(float amount) => "$" + amount.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A dashboard of the city's people, the shape the Economy app's population view takes.</summary>
    public static UiDashboardModel Dashboard(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = t("reallife_ui_sample_city", "The city"),
            Subtitle = "Day 47 · Autumn",
            Tabs = new[] { t("reallife_ui_gallery_tab_city", "City"), t("reallife_ui_gallery_tab_district", "District"), t("reallife_ui_gallery_tab_people", "People") },
            Figures = SampleFigures(t),
            TableTitle = t("reallife_ui_sample_districts", "Districts"),
            Table = SampleTable(t),
            SideTitle = t("reallife_ui_sample_share", "Your share"),
            Side = new[]
            {
                new UiPairModel { Key = "People employed", Value = "1,240" },
                new UiPairModel { Key = "Share of spending", Value = "6.2%" },
                new UiPairModel { Key = "Taxes paid", Value = "$41,200" },
                new UiPairModel { Key = "Customers a day", Value = "3,860" },
            },
            StatsTitle = t("reallife_ui_sample_mood", "How people feel"),
            Stats = new[]
            {
                new UiStatModel { Label = "Satisfaction", Figure = "71", Value = 71 },
                new UiStatModel { Label = "Rent against pay", Figure = "38", Value = 38 },
                new UiStatModel { Label = "Safety", Figure = "64", Value = 64 },
                new UiStatModel { Label = "Getting about", Figure = "82", Value = 82 },
            },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        m.StatCount = m.Stats.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A desk session's first step: the weekly team meeting's aim.</summary>
    public static UiSessionModel Session(UiWords t)
    {
        var m = new UiSessionModel
        {
            Title = "Weekly team meeting",
            About = "Twenty minutes with the team at Pixel Harbour Games: lift their morale, or push this week's output.",
            StepLabel = t("reallife_ui_gallery_step", "Step 1 of 2"),
            StepCount = 2,
            Step = 0,
            Question = "What is this week's meeting for?",
            Choices = new[]
            {
                new UiChoiceModel { Key = UiAction.Pick1, Name = "Lift morale", Trade = "Burnout eases for the people present. Output this week is a little lower.", Note = "20 min", Selected = true },
                new UiChoiceModel { Key = UiAction.Pick2, Name = "Push output", Trade = "More gets done this week. Burnout builds each day they work.", Note = "20 min" },
                new UiChoiceModel { Key = UiAction.Pick3, Name = "Hear them out", Trade = "No change to output. You learn who is close to leaving.", Note = "30 min" },
            },
            SummaryTitle = t("reallife_ui_sample_today", "Today"),
            Summary = new[]
            {
                new UiPairModel { Key = "Team", Value = "12 people" },
                new UiPairModel { Key = "Morale", Value = "61" },
                new UiPairModel { Key = "Burnout", Value = "Rising" },
            },
            Actions = new[]
            {
                new UiActionModel { Label = t("reallife_session_button_hand", "Hand to a manager"), Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = 0 },
                new UiActionModel { Label = t("reallife_session_button_begin", "Begin"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = 1 },
            },
        };
        m.ChoiceCount = m.Choices.Length;
        m.SummaryCount = m.Summary.Length;
        m.ActionCount = m.Actions.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
            new UiPrompt { Action = UiAction.Alt, Label = t("reallife_session_button_begin", "Begin") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A form: a campaign's settings before it is ordered.</summary>
    public static UiFormModel Form(UiWords t)
    {
        var m = new UiFormModel
        {
            Title = "Billboard campaign",
            About = "CityAds puts your shop on the billboards near it. More people walk past while it runs.",
            Fields = new[]
            {
                new UiFieldModel { Kind = UiFieldKind.Choice, Section = "Where", Label = "Billboards", Value = "Near Bloom Café" },
                new UiFieldModel { Kind = UiFieldKind.Slider, Label = "Weeks", Value = "4", Share = 0.5f },
                new UiFieldModel { Kind = UiFieldKind.Toggle, Section = "Options", Label = "Renew when it ends", On = true },
                new UiFieldModel { Kind = UiFieldKind.Toggle, Label = "Tell me each week", On = false },
                new UiFieldModel { Kind = UiFieldKind.Pair, Section = "Cost", Label = "A week", Value = "$2,000" },
                new UiFieldModel { Kind = UiFieldKind.Pair, Label = "In all", Value = "$8,000" },
            },
            Actions = new[]
            {
                new UiActionModel { Label = t("common_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = 0 },
                new UiActionModel { Label = t("reallife_ui_sample_order", "Order"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = 1 },
            },
        };
        m.FieldCount = m.Fields.Length;
        m.ActionCount = m.Actions.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_change", "Change") },
            new UiPrompt { Action = UiAction.Alt, Label = t("reallife_ui_sample_order", "Order") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static UiFigureModel[] SampleFigures(UiWords t)
    {
        return new[]
        {
            new UiFigureModel { Label = t("reallife_ui_sample_people", "People"), Value = "62,800", Change = "+1.8%", Rising = true, Good = true, Trend = new[] { 60.1f, 60.6f, 61.0f, 61.5f, 61.4f, 62.2f, 62.8f } },
            new UiFigureModel { Label = t("reallife_ui_sample_jobs", "Jobs"), Value = "79,440", Change = "+0.6%", Rising = true, Good = true, Trend = new[] { 78.2f, 78.7f, 78.5f, 79.0f, 79.2f, 79.4f } },
            new UiFigureModel { Label = t("reallife_ui_sample_unemployed", "Out of work"), Value = "6.4%", Change = "+0.3", Rising = true, Good = false, Trend = new[] { 5.6f, 5.9f, 6.1f, 6.0f, 6.2f, 6.4f } },
            new UiFigureModel { Label = t("reallife_ui_sample_rent", "Average rent"), Value = "$2,140", Change = "+2.1%", Rising = true, Good = false, Trend = new[] { 2.0f, 2.02f, 2.05f, 2.09f, 2.12f, 2.14f } },
        };
    }

    private static UiTableModel SampleTable(UiWords t)
    {
        return new UiTableModel
        {
            Columns = new[]
            {
                new UiColumn { Title = t("reallife_ui_sample_district", "District") },
                new UiColumn { Title = t("reallife_ui_sample_people", "People"), Width = 120f, Right = true },
                new UiColumn { Title = t("reallife_ui_sample_jobs", "Jobs"), Width = 120f, Right = true },
                new UiColumn { Title = t("reallife_ui_sample_rent", "Average rent"), Width = 160f, Right = true },
            },
            Rows = new[]
            {
                new[] { t("ba:neighborhood_midtown", "Midtown"), "14,210", "28,400", "$2,620" },
                new[] { t("ba:neighborhood_murrayhill", "Murray Hill"), "9,880", "7,100", "$2,280" },
                new[] { t("ba:neighborhood_garmentdistrict", "Garment District"), "6,040", "11,900", "$1,960" },
                new[] { t("ba:neighborhood_hellskitchen", "Hell's Kitchen"), "8,310", "6,200", "$2,050" },
                new[] { t("ba:neighborhood_lowermanhattan", "Lower Manhattan"), "7,450", "9,800", "$1,880" },
                new[] { t("ba:neighborhood_industrycity", "Industry City"), "5,960", "8,700", "$1,420" },
                new[] { t("ba:neighborhood_thehamptons", "The Hamptons"), "1,180", "640", "$4,900" },
                new[] { t("reallife_neighborhood_uptown", "Uptown"), "5,200", "3,900", "$1,740" },
                new[] { t("reallife_neighborhood_cedarheights", "Cedar Heights"), "4,570", "2,800", "$1,610" },
            },
            RowCount = 9,
            Selected = 0,
            EmptyTitle = t("reallife_ui_sample_none", "Nothing here yet"),
            EmptyBody = t("reallife_ui_sample_none_body", "The city has no figures for this yet."),
        };
    }

    private static UiNavModel Nav(string label, string count, float hue, bool selected) => new UiNavModel { Label = label, Count = count, Hue = hue, Selected = selected };

    private static UiRowModel Row(string name, string preview, string time, int unread, float hue, bool online, bool verified, bool selected, string section)
    {
        return new UiRowModel { Name = name, Preview = preview, Time = time, Unread = unread, Hue = hue, Online = online, Verified = verified, Selected = selected, Section = section };
    }
}
