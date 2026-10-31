using System;
using RealLifeOverhaul.Ui;

// The kit's sample view models: the owner's Contacts wireframe's own people and words (not-in-repo/design/wireframes,
// contacts_source.html), for the offline previews and EconomyCheck's layout checks. Fixtures, never shipped: the mod's
// screens build their models from the game's data and the locale keys. Words a screen labels with go through the given
// text source, so the checks measure every language; the fixtures' names and messages stay as the wireframe has them.
internal static class UiSamples
{
    internal delegate string Words(string key, string english);

    /// <summary>The wireframe's Contacts with Maya Okafor selected, as the kit's list-and-detail template draws it.</summary>
    public static UiListDetailModel Contacts(Words t, bool composer)
    {
        var m = new UiListDetailModel
        {
            AppName = t("reallife_ui_contacts_app", "contacts"),
            GroupsLabel = t("reallife_ui_contacts_groups", "Groups"),
            PlayerName = "Richy Man",
            PlayerHue = 40f,
            PlayerSummary = "14 contacts · 7 unread",
            ListTitle = t("reallife_ui_contacts_all", "All contacts"),
            ListCount = "14",
            SearchPlaceholder = t("reallife_ui_contacts_search", "Search people, shops, agencies"),
            SearchCount = "14",
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
            Nav(t("reallife_ui_contacts_all", "All contacts"), "14", 12f, true),
            Nav("General", "3", 250f, false),
            Nav("Business", "4", 320f, false),
            Nav("Employees", "3", 155f, false),
            Nav("Imports", "1", 75f, false),
            Nav("Furniture", "1", 45f, false),
            Nav("Rivals", "1", 25f, false),
        };
        m.GroupCount = m.Groups.Length;
        m.SideActions = new[]
        {
            new UiActionModel { Label = t("contacts_mark_all_as_read_button", "Mark all as read"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count, Command = 0 },
            new UiActionModel { Label = t("contacts_delete_all_button", "Delete all"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count, Command = 1 },
        };
        m.SideActionCount = m.SideActions.Length;
        string pinned = t("reallife_ui_contacts_pinned", "Pinned");
        m.Rows = new[]
        {
            Row("Uncle Fred", "Good. Once you’ve got keys, open your first shop. I’ll check in.", "09:02", 1, 40f, true, false, false, pinned),
            Row("Anderson Recruitment Corp.", "Shall I send the shortlist over?", "08:52", 2, 320f, true, true, false, null),
            Row("Maya Okafor", "Happy to cover Tuesday evenings instead.", "09:15", 0, 12f, true, false, true, null),
            Row("City Office", "Fine of $125 deducted from your bank account.", "17:40", 1, 300f, false, true, false, "General"),
            Row("NY AutoTow Service", "Stuck somewhere? One call and we’ll haul you out, any hour.", "Sun", 0, 230f, true, false, false, null),
            Row("City Workforce Inc.", "Your HQ application is approved. Let us know which roles to fill.", "Thu", 0, 275f, true, false, false, "Business"),
            Row("CityAds", "Your billboard on 7th Ave ends Friday. Renew?", "Wed", 0, 350f, false, false, false, null),
            Row("McCain’s eMarketing", "Click-through on your last campaign was up 14%.", "Mon", 0, 200f, true, false, false, null),
            Row("Luis Romero", "Van needs fuel before the afternoon run. Okay to use the company card?", "10:02", 1, 155f, true, false, false, "Employees"),
            Row("Hana Sato", "Requesting a better desk chair. My back is filing a complaint.", "Yesterday", 0, 180f, false, false, false, null),
            Row("Harbor Line Wholesale", "Container of coffee beans landed. Your Monday delivery is on track.", "Sat", 0, 75f, true, false, false, "Imports"),
            Row("Lumen & Oak Interiors", "The walnut counters you asked about are back in stock.", "Fri", 0, 50f, true, false, false, "Furniture"),
            Row("Vex Holdings", "Cute little café. Shame if the block got crowded.", "Thu", 0, 25f, false, false, false, "Rivals"),
        };
        m.RowCount = m.Rows.Length;
        m.Actions = new[]
        {
            new UiActionModel { Label = t("reallife_ui_contacts_pin", "Pin"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Pin, Key = UiAction.Option, Command = 0 },
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
            new UiPairModel { Key = t("reallife_ui_contacts_phone", "Phone"), Value = "(917) 555-0103" },
            new UiPairModel { Key = t("common_address", "Address"), Value = "Bleecker St" },
            new UiPairModel { Key = t("reallife_ui_contacts_group", "Group"), Value = "Employees" },
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

    /// <summary>The gallery on one of its pages, with the wireframe's people as its samples.</summary>
    public static UiGalleryModel Gallery(Words t, int page)
    {
        var m = new UiGalleryModel
        {
            Title = t("reallife_ui_gallery_title", "Interface gallery"),
            Pages = new[]
            {
                t("reallife_ui_gallery_page_controls", "Controls"), t("reallife_ui_gallery_page_lists", "Lists"), t("reallife_ui_gallery_page_figures", "Figures"),
                t("reallife_ui_gallery_page_messages", "Messages"), t("reallife_ui_gallery_page_question", "Question"), t("reallife_ui_gallery_page_call", "Call"),
            },
            Page = page,
            Captions = new[]
            {
                t("reallife_ui_gallery_cap_buttons", "Buttons"), t("reallife_ui_gallery_cap_nav", "Groups and tabs"), t("reallife_ui_gallery_cap_inputs", "Settings"),
                t("reallife_ui_gallery_cap_rows", "Rows"), t("reallife_ui_gallery_cap_cards", "Services and choices"), t("reallife_ui_gallery_cap_data", "Details and table"),
                t("reallife_ui_gallery_cap_figures", "Figure"), t("reallife_ui_gallery_cap_stats", "Bars and steps"), t("reallife_ui_gallery_cap_status", "Status, badges, faces"),
                t("reallife_ui_gallery_cap_thread", "Messages"), t("reallife_ui_gallery_cap_states", "Empty and loading"),
            },
            Buttons = new[]
            {
                t("phone_call_button", "Call"), t("reallife_ui_gallery_secondary", "Pin"), t("contacts_delete_all_button", "Delete all"),
                t("reallife_ui_gallery_armed", "Press again to delete"), t("reallife_ui_gallery_chip", "Thanks"), t("reallife_ui_contacts_send", "Send"),
                t("reallife_ui_gallery_disabled", "Not now"),
            },
            Nav = new[]
            {
                new UiNavModel { Label = t("reallife_ui_contacts_all", "All contacts"), Count = "14", Hue = 12f, Selected = true },
                new UiNavModel { Label = "Business", Count = "4", Hue = 320f },
                new UiNavModel { Label = "Employees", Count = "3", Hue = 155f, Loud = true },
            },
            Tabs = new[] { t("reallife_ui_gallery_tab_city", "City"), t("reallife_ui_gallery_tab_district", "District"), t("reallife_ui_gallery_tab_people", "People") },
            SearchPlaceholder = t("reallife_ui_contacts_search", "Search people, shops, agencies"),
            Fields = new[] { t("reallife_ui_gallery_toggle_on", "Ambient sounds"), t("reallife_ui_gallery_toggle_off", "Show hints"), t("reallife_ui_gallery_slider", "Budget"), t("reallife_ui_gallery_choice", "Look") },
            SliderFigure = "$62,000",
            ChoiceValue = t("reallife_flare_theme_2", "Dark"),
            RowSection = t("reallife_ui_contacts_pinned", "Pinned"),
            Rows = new[]
            {
                Row("Maya Okafor", "Happy to cover Tuesday evenings instead.", "09:15", 0, 12f, true, false, true, null),
                Row("Anderson Recruitment Corp.", "Shall I send the shortlist over?", "08:52", 2, 320f, true, true, false, null),
                Row("Vex Holdings", "Cute little café. Shame if the block got crowded.", "Thu", 0, 25f, false, false, false, null),
            },
            Services = new[]
            {
                new UiServiceModel { Title = "Pay a bonus", Price = "$200", About = "Raises satisfaction for a few weeks." },
                new UiServiceModel { Title = "Negotiate salary", Price = t("reallife_ui_price_quote", "Quote"), About = "Currently $22/h. She’s asking for $24/h." },
            },
            Choices = new[]
            {
                new UiChoiceModel { Key = UiAction.Confirm, Name = "Lift morale", Trade = "Burnout eases. Output this week is lower.", Note = "30 min", Selected = true },
                new UiChoiceModel { Key = UiAction.Alt, Name = "Push output", Trade = "More done this week. Burnout builds each day.", Note = "45 min" },
            },
            Pairs = new[]
            {
                new UiPairModel { Key = t("reallife_ui_contacts_phone", "Phone"), Value = "(917) 555-0103" },
                new UiPairModel { Key = t("common_address", "Address"), Value = "Bleecker St" },
            },
            StepLabel = t("reallife_ui_gallery_step", "Step 2 of 3"),
            Statuses = new[] { t("reallife_ui_gallery_status_quiet", "Closed"), t("reallife_ui_contacts_on_shift", "On shift"), t("reallife_ui_gallery_status_warn", "Late"), t("reallife_ui_gallery_status_danger", "Rival") },
            AvatarNames = new[] { "Maya Okafor", "Luis Romero", "Hana Sato", "Uncle Fred", "City Office", "张伟" },
            Messages = new[]
            {
                new UiMessageModel { From = UiFrom.Them, Text = "Could I drop the Sunday shift?", Time = "09:14" },
                new UiMessageModel { From = UiFrom.Me, Text = "Let me look at the rota tonight.", Time = "09:20" },
                new UiMessageModel { From = UiFrom.System, Text = "Pay a bonus · $200 charged", Time = "09:22" },
            },
            Replies = new[] { "Approved", "Let’s talk Friday" },
            ComposerPlaceholder = t("reallife_ui_contacts_message", "Message Maya"),
            Draft = "See you at nine.",
            SendLabel = t("reallife_ui_contacts_send", "Send"),
            EmptyTitle = t("reallife_ui_contacts_quiet_title", "Quiet line"),
            EmptyBody = t("reallife_ui_contacts_quiet_body", "No messages yet. They are still saved in your contacts."),
            BusyLabel = t("reallife_ui_gallery_busy", "Loading the city"),
            QuestionTitle = t("reallife_ui_gallery_question_title", "Delete every message?"),
            QuestionBody = t("reallife_ui_gallery_question_body", "Your contacts stay. Only their messages go, and they cannot be brought back."),
            Answers = new[]
            {
                new UiActionModel { Label = t("reallife_ui_gallery_keep", "Keep them"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = 0 },
                new UiActionModel { Label = t("contacts_delete_all_button", "Delete all"), Kind = UiButtonKind.Armed, Key = UiAction.Confirm, Command = 1 },
            },
            CallName = "Maya Okafor",
            CallState = t("reallife_ui_gallery_connected", "Connected 00:42"),
            CallOptions = new[]
            {
                new UiServiceModel { Title = "Pay a bonus", Price = "$200" },
                new UiServiceModel { Title = "Approve schedule change", Price = t("reallife_ui_price_free", "Free") },
            },
            CallEnd = t("reallife_ui_gallery_end", "End"),
            ToastTitle = t("reallife_ui_gallery_toast_title", "Bonus paid"),
            ToastSub = "Maya Okafor · $200",
        };
        m.Table = SampleTable(t);
        m.Figures = SampleFigures(t);
        m.Stats = new[]
        {
            new UiStatModel { Label = "Skill", Figure = "82", Value = 82 },
            new UiStatModel { Label = "Satisfaction", Figure = "64", Value = 64 },
            new UiStatModel { Label = "Energy", Figure = "48", Value = 48 },
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") },
            new UiPrompt { Action = UiAction.Option, Label = t("reallife_ui_prompt_look", "Light or dark") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A dashboard of the city's people, the shape the Economy app's population view takes.</summary>
    public static UiDashboardModel Dashboard(Words t)
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
            },
            StatsTitle = t("reallife_ui_sample_mood", "How people feel"),
            Stats = new[]
            {
                new UiStatModel { Label = "Satisfaction", Figure = "71", Value = 71 },
                new UiStatModel { Label = "Rent against pay", Figure = "38", Value = 38 },
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
    public static UiSessionModel Session(Words t)
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
                new UiChoiceModel { Key = UiAction.Confirm, Name = "Lift morale", Trade = "Burnout eases for the people present. Output this week is a little lower.", Note = "20 min", Selected = true },
                new UiChoiceModel { Key = UiAction.Alt, Name = "Push output", Trade = "More gets done this week. Burnout builds each day they work.", Note = "20 min" },
                new UiChoiceModel { Key = UiAction.Option, Name = "Hear them out", Trade = "No change to output. You learn who is close to leaving.", Note = "30 min" },
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
                new UiActionModel { Label = t("reallife_session_button_hand", "Hand to a manager"), Kind = UiButtonKind.Secondary, Key = UiAction.Count, Command = 0 },
                new UiActionModel { Label = t("reallife_session_button_begin", "Begin"), Kind = UiButtonKind.Primary, Key = UiAction.Confirm, Command = 1 },
            },
        };
        m.ChoiceCount = m.Choices.Length;
        m.SummaryCount = m.Summary.Length;
        m.ActionCount = m.Actions.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A form: a campaign's settings before it is ordered.</summary>
    public static UiFormModel Form(Words t)
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
                new UiActionModel { Label = t("reallife_ui_sample_order", "Order"), Kind = UiButtonKind.Primary, Key = UiAction.Confirm, Command = 1 },
            },
        };
        m.FieldCount = m.Fields.Length;
        m.ActionCount = m.Actions.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_change", "Change") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static UiFigureModel[] SampleFigures(Words t)
    {
        return new[]
        {
            new UiFigureModel { Label = t("reallife_ui_sample_people", "People"), Value = "48,210", Change = "+1.8%", Up = true, Trend = new[] { 46.1f, 46.4f, 46.9f, 47.2f, 47.1f, 47.8f, 48.2f } },
            new UiFigureModel { Label = t("reallife_ui_sample_jobs", "Jobs"), Value = "61,400", Change = "+0.6%", Up = true, Trend = new[] { 60f, 60.4f, 60.2f, 60.9f, 61.1f, 61.4f } },
            new UiFigureModel { Label = t("reallife_ui_sample_unemployed", "Out of work"), Value = "6.4%", Change = "+0.3", Up = false, Trend = new[] { 5.6f, 5.9f, 6.1f, 6.0f, 6.2f, 6.4f } },
            new UiFigureModel { Label = t("reallife_ui_sample_rent", "Average rent"), Value = "$2,140", Change = "+2.1%", Up = false, Trend = new[] { 2.0f, 2.02f, 2.05f, 2.09f, 2.12f, 2.14f } },
        };
    }

    private static UiTableModel SampleTable(Words t)
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
                new[] { "Midtown", "14,210", "28,400", "$2,620" },
                new[] { "Murray Hill", "9,880", "7,100", "$2,280" },
                new[] { "Garment District", "6,040", "11,900", "$1,960" },
                new[] { "Hell’s Kitchen", "8,310", "6,200", "$2,050" },
                new[] { "Uptown", "5,200", "3,900", "$1,740" },
                new[] { "Cedar Heights", "4,570", "3,900", "$1,610" },
            },
            RowCount = 6,
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
