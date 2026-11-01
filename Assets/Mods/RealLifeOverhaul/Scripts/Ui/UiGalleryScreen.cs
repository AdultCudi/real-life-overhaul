using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The gallery as a screen: every component of the kit on six pages, the tab keys turning the pages and Option turning
    /// the look between dark and light for the gallery alone, so the owner judges the kit in both themes from Options,
    /// Tests. Its samples are words of the locale (UiText) and figures in the game's own money; nothing it shows does
    /// anything but show. Game-free (the words and the money's form come in from the director). Cost: a layout a press.
    /// </summary>
    public sealed class UiGalleryScreen : IUiScreen
    {
        private readonly UiWords _words;
        private readonly Func<float, string> _money;
        private int _page;
        private bool? _look;   // the look the gallery shows, once Option has turned it; the setting's until then
        private bool _dirty = true;

        /// <summary>Back closes the gallery (it stands over whatever showed when the test opened it).</summary>
        public Action Close;

        public UiGalleryScreen(UiWords words, Func<float, string> money)
        {
            _words = words;
            _money = money;
        }

        public string Id => "gallery";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            UiGallery.Lay(s, Model(_words, _page, _money), frame, _look ?? dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiGallery.PageCommand && arg != _page)
            {
                _page = arg;
                _dirty = true;
            }
            return true;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            Close?.Invoke();
            return Close != null;
        }

        public void Tab(int step)
        {
            _page = (_page + step + UiGallery.PageCount) % UiGallery.PageCount;
            _dirty = true;
        }

        public bool Act(UiAction action)
        {
            if (action != UiAction.Option)
            {
                return false;
            }
            _look = !(_look ?? true);
            _dirty = true;
            return true;
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        /// <summary>The gallery's view model on a page: its samples from the locale, its sums in the game's form of money.</summary>
        public static UiGalleryModel Model(UiWords t, int page, Func<float, string> money)
        {
            string bonus = money(200f);
            var m = new UiGalleryModel
            {
                Title = t("reallife_ui_gallery_title", "Interface gallery"),
                Pages = new[]
                {
                    t("reallife_ui_gallery_page_controls", "Controls"), t("reallife_ui_gallery_page_lists", "Lists"), t("reallife_ui_gallery_page_figures", "Figures"),
                    t("reallife_ui_gallery_page_messages", "Messages"), t("reallife_ui_gallery_page_question", "Question"), t("reallife_ui_gallery_page_call", "Call"),
                },
                Page = Math.Max(0, Math.Min(UiGallery.PageCount - 1, page)),
                Captions = new[]
                {
                    t("reallife_ui_gallery_cap_buttons", "Buttons"), t("reallife_ui_gallery_cap_nav", "Groups and tabs"), t("reallife_ui_gallery_cap_inputs", "Settings"),
                    t("reallife_ui_gallery_cap_rows", "Rows"), t("reallife_ui_gallery_cap_cards", "Services and choices"), t("reallife_ui_gallery_cap_data", "Details and table"),
                    t("reallife_ui_gallery_cap_figures", "Figure"), t("reallife_ui_gallery_cap_stats", "Bars and steps"), t("reallife_ui_gallery_cap_status", "Status, badges, faces, keys"),
                    t("reallife_ui_gallery_cap_thread", "Messages"), t("reallife_ui_gallery_cap_states", "Empty and loading"),
                },
                Buttons = new[]
                {
                    t("phone_call_button", "Call"), t("phone_view_on_map_button", "View on map"), t("contacts_delete_all_button", "Delete all"),
                    t("reallife_ui_gallery_armed", "Press again to delete"), t("reallife_ui_gallery_chip", "Thanks"), t("reallife_ui_contacts_send", "Send"),
                    t("reallife_ui_gallery_disabled", "Not now"),
                },
                Nav = new[]
                {
                    new UiNavModel { Label = t("reallife_ui_contacts_all", "All contacts"), Count = "14", Hue = 12f, Selected = true },
                    new UiNavModel { Label = t("reallife_ui_gallery_group_work", "Work"), Count = "4", Hue = 320f },
                    new UiNavModel { Label = t("reallife_ui_gallery_group_family", "Family"), Count = "3", Hue = 155f, Loud = true },
                    new UiNavModel { Label = t("reallife_ui_gallery_group_shops", "Shops"), Count = "5", Hue = 75f },
                    new UiNavModel { Label = t("reallife_ui_gallery_group_services", "Services"), Count = "2", Hue = 230f },
                    new UiNavModel { Label = t("contactcategoryname_rivals", "Rivals"), Count = "1", Hue = 25f },
                },
                Tabs = new[] { t("reallife_ui_gallery_tab_city", "City"), t("reallife_ui_gallery_tab_district", "District"), t("reallife_ui_gallery_tab_people", "People") },
                SearchPlaceholder = t("reallife_ui_contacts_search", "Search people, shops, agencies"),
                Fields = new[]
                {
                    t("reallife_ui_gallery_toggle_on", "Ambient sounds"), t("reallife_ui_gallery_toggle_off", "Show hints"), t("reallife_ui_gallery_slider", "Budget"),
                    t("reallife_ui_gallery_choice", "Look"), t("reallife_ui_gallery_choice_motion", "Motion"),
                },
                Query = t("reallife_ui_gallery_query", "Maya"),
                SliderFigure = money(62000f),
                ChoiceValue = t("reallife_flare_theme_2", "Dark"),
                MotionValue = t("reallife_ui_motion_1", "Calm"),
                Rows = new[]
                {
                    new UiRowModel { Name = t("reallife_ui_gallery_person_2", "Anderson Recruitment"), Preview = t("reallife_ui_gallery_line_2",
                        "Shall I send the shortlist over?"), Time = "08:52", Unread = 2, Hue = 320f,
                        Online = true, Verified = true, Section = t("reallife_ui_contacts_new", "New messages") },
                    new UiRowModel { Name = t("reallife_ui_gallery_person_5", "Luis Romero"), Preview = t("reallife_ui_gallery_line_7",
                        "The van needs fuel before the afternoon run."), Time = "08:30", Unread = 1, Hue = 155f, Online = true },
                    new UiRowModel { Name = t("reallife_ui_gallery_person_1", "Maya Okafor"), Preview = t("reallife_ui_gallery_line_1",
                        "Happy to cover Tuesday evenings instead."), Time = "09:15", Hue = 12f, Online = true, Selected = true,
                        Section = t("reallife_ui_contacts_earlier", "Earlier") },
                    new UiRowModel { Name = t("reallife_ui_gallery_person_3", "Vex Holdings"), Preview = t("reallife_ui_gallery_line_3",
                        "Cute little café. Shame if the block got crowded."), Time = "Thu", Hue = 25f },
                },
                Services = new[]
                {
                    new UiServiceModel { Title = t("reallife_ui_gallery_service_1", "Pay a bonus"), Price = bonus, About = t("reallife_ui_gallery_service_1_about",
                        "Raises satisfaction for a few weeks.") },
                    new UiServiceModel { Title = t("reallife_ui_gallery_service_2", "Negotiate salary"), Price = t("reallife_ui_price_quote", "Quote"),
                        About = t("reallife_ui_gallery_service_2_about", "She asks for a little more an hour.") },
                },
                Choices = new[]
                {
                    new UiChoiceModel { Key = UiAction.Pick1, Name = t("reallife_session_meeting_morale", "Lift morale"),
                        Trade = t("reallife_session_meeting_morale_trade", "Burnout eases. Output this week is lower."), Selected = true },
                    new UiChoiceModel { Key = UiAction.Pick2, Name = t("reallife_session_meeting_push", "Push output"), Trade = t("reallife_session_meeting_push_trade",
                        "More done this week. Burnout builds each day.") },
                },
                Pairs = new[]
                {
                    new UiPairModel { Key = t("common_address", "Address"), Value = t("reallife_ui_gallery_address", "Bleecker St") },
                    new UiPairModel { Key = t("myemployees_hourlywage", "Hourly wage"), Value = money(24f) },
                    new UiPairModel { Key = t("contacts_today_opening_hours", "Today's opening hours"), Value = "08:00 - 18:00" },
                },
                StepLabel = t("reallife_ui_gallery_step", "Step 2 of 3"),
                Statuses = new[] { t("reallife_ui_gallery_status_quiet", "Closed"), t("reallife_ui_contacts_on_shift", "On shift"), t("reallife_ui_gallery_status_warn",
                    "Late"), t("reallife_ui_gallery_status_danger", "Rival") },
                AvatarNames = new[] { t("reallife_ui_gallery_person_1", "Maya Okafor"), t("reallife_ui_gallery_person_2", "Anderson Recruitment"),
                    t("reallife_ui_gallery_person_3", "Vex Holdings"), t("reallife_ui_gallery_person_4", "Hana Sato") },
                Messages = new[]
                {
                    new UiMessageModel { From = UiFrom.Them, Text = t("reallife_ui_gallery_line_4", "Could I drop the Sunday shift?"), Time = "09:14" },
                    new UiMessageModel { From = UiFrom.Me, Text = t("reallife_ui_gallery_line_5", "Let me look at the rota tonight."), Time = "09:20" },
                    new UiMessageModel { From = UiFrom.System, Text = t("reallife_ui_gallery_line_6", "A bonus was paid."), Time = "09:22" },
                },
                Replies = new[] { t("reallife_ui_gallery_reply_1", "Approved"), t("reallife_ui_gallery_reply_2", "Talk on Friday") },
                ComposerPlaceholder = t("reallife_ui_gallery_message", "Write a message"),
                Draft = t("reallife_ui_gallery_draft", "See you at nine."),
                SendLabel = t("reallife_ui_contacts_send", "Send"),
                EmptyTitle = t("reallife_ui_contacts_quiet_title", "Quiet line"),
                EmptyBody = t("reallife_ui_contacts_quiet_body", "No messages yet. They are still saved in your contacts."),
                BusyLabel = t("reallife_ui_gallery_busy", "Loading the city"),
                QuestionTitle = t("reallife_ui_gallery_question_title", "Delete every message?"),
                QuestionBody = t("reallife_ui_gallery_question_body", "Your contacts stay. Only their messages go, and they cannot be brought back."),
                Answers = new[]
                {
                    new UiActionModel { Label = t("reallife_ui_gallery_keep", "Keep them"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
                    new UiActionModel { Label = t("contacts_delete_all_button", "Delete all"), Kind = UiButtonKind.Armed, Key = UiAction.Count, Command = 1 },
                },
                CallName = t("reallife_ui_gallery_person_1", "Maya Okafor"),
                CallState = t("reallife_ui_gallery_connected", "Connected"),
                CallOptions = new[]
                {
                    new UiServiceModel { Title = t("reallife_ui_gallery_service_1", "Pay a bonus"), Price = bonus },
                    new UiServiceModel { Title = t("reallife_ui_gallery_service_2", "Negotiate salary"), Price = t("reallife_ui_price_quote", "Quote") },
                },
                CallEnd = t("reallife_ui_gallery_end", "End"),
                ToastTitle = t("reallife_ui_gallery_toast_title", "Bonus paid"),
                ToastSub = t("reallife_ui_gallery_person_1", "Maya Okafor"),
            };
            m.Table = new UiTableModel
            {
                Columns = new[]
                {
                    new UiColumn { Title = t("reallife_ui_gallery_col_name", "Name") },
                    new UiColumn { Title = t("reallife_ui_gallery_col_skill", "Skill"), Width = UiTokens.S16 + UiTokens.S6, Right = true },
                },
                Rows = new[]
                {
                    new[] { t("reallife_ui_gallery_person_1", "Maya Okafor"), "82" },
                    new[] { t("reallife_ui_gallery_person_4", "Hana Sato"), "93" },
                    new[] { t("reallife_ui_gallery_person_5", "Luis Romero"), "71" },
                    new[] { t("reallife_ui_gallery_person_6", "Ana Lima"), "64" },
                    new[] { t("reallife_ui_gallery_person_7", "Tom Weber"), "58" },
                },
                RowCount = 5,
                Selected = 0,
            };
            // The three ways a change reads: up and good, down and good, up and bad. The arrow follows the sign, the colour the player.
            m.Figures = new[]
            {
                new UiFigureModel { Label = t("reallife_ui_gallery_tab_people", "People"), Value = "62,800", Change = "+1.8%", Rising = true, Good = true,
                    Trend = new[] { 60.1f, 60.6f, 61.0f, 61.5f, 61.4f, 62.2f, 62.8f } },
                new UiFigureModel { Label = t("reallife_ui_gallery_out_of_work", "Out of work"), Value = "6.4%", Change = "−0.4", Good = true,
                    Trend = new[] { 7.1f, 7.0f, 6.8f, 6.9f, 6.6f, 6.4f } },
                new UiFigureModel { Label = t("reallife_ui_gallery_rent", "Average rent"), Value = money(2140f), Change = "+2.1%", Rising = true,
                    Trend = new[] { 2.0f, 2.02f, 2.05f, 2.09f, 2.12f, 2.14f } },
            };
            m.Stats = new[]
            {
                new UiStatModel { Label = t("bizman_headhunters_skill_level_title", "Skill"), Figure = "82", Value = 82 },
                new UiStatModel { Label = t("reallife_ui_gallery_satisfaction", "Satisfaction"), Figure = "64", Value = 64 },
                new UiStatModel { Label = t("reallife_ui_gallery_energy", "Energy"), Figure = "48", Value = 48 },
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
    }
}
