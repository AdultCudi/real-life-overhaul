using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The full menu's frame and the boxes and notices every screen uses (Scripts/Ui/Shell), each with its own switch.
        static partial void AddShell(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("all_apps", UiTemplate.InfoPanel, "AllAppsScreen", new[]
                {
                    "reallife_ui_shell_all_apps", "reallife_ui_shell_other_mods", "reallife_ui_shell_none", "reallife_ui_shell_none_other",
                    "reallife_ui_shell_prompt_folder",
                    "reallife_ui_shell_another_app", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                    "reallife_phone_folder_money",
                    "reallife_phone_folder_work", "reallife_phone_folder_life", "reallife_phone_folder_business", "reallife_phone_folder_setup",
                    "reallife_ui_page_population", "reallife_ui_page_flare", "reallife_ui_page_studio", "reallife_planner_app", "reallife_calendar_app",
                    "reallife_ui_shell_status_unread", "reallife_econ_app", "reallife_banks_app", "reallife_mkt_app", "reallife_econapp_app",
                    "reallife_career_app",
                    "reallife_campus_app", "reallife_roles_app", "reallife_flare_app", "reallife_co_app", "smartphone_persona", "smartphone_contacts",
                    "smartphone_myemployees", "smartphone_bizman", "smartphone_econoview", "smartphone_vooglemaps", "smartphone_marketinsider",
                    "smartphone_rivals"
                }).Switch(UiScreenSwitches.Apps),
                new UiScreenDef("question_box", UiTemplate.Decision, "QuestionScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "hud_confirm_are_you_sure", "common_confirm",
                    "common_cancel",
                    "reallife_ui_test_question_body", "reallife_ui_test_question_negative_body", "reallife_ui_test_ack_body",
                    "reallife_ui_test_mod_box_body", "reallife_vc_confirm_sell_title", "reallife_vc_confirm_sell_body",
                    "change_character_clothes_unsaved_changes_warning", "reallife_pd_ask_header", "reallife_pd_ask_body"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("acknowledge_box", UiTemplate.Decision, "AcknowledgeScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_ui_box_dont_show",
                    "reallife_ui_box_cancel", "common_agree",
                    "reallife_ui_test_ack_title", "reallife_ui_test_ack_body", "main_menu_mods_warning_header", "main_menu_mods_warning_body"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("text_box", UiTemplate.Decision, "TextBoxScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_ui_box_type_here", "reallife_ui_box_cancel",
                    "reallife_ui_test_text_box_ask", "reallife_ui_test_text_box_ok", "reallife_brand_name_placeholder", "reallife_brand_name_confirm"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("mod_box", UiTemplate.Decision, "ModBoxScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_meta_ok", "reallife_meta_cancel",
                    "reallife_ui_test_mod_box_title", "reallife_ui_test_mod_box_body", "reallife_meta_resetdata_title",
                    "reallife_meta_resetdata_explain", "reallife_meta_resetdata_yes"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("notice_cards", UiTemplate.Decision, "NoticeCardsScreen", new[]
                {
                    "reallife_ui_notice_road", "reallife_ui_notice_police", "reallife_ui_notice_health", "reallife_ui_notice_money",
                    "reallife_ui_notice_property",
                    "reallife_ui_notice_work", "reallife_ui_notice_business", "reallife_ui_notice_tech", "reallife_ui_notice_news", "reallife_ui_notice_people",
                    "reallife_ui_notice_other", "reallife_ui_notice_done", "reallife_ui_notice_warning", "reallife_ui_notice_info"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("notice_list", UiTemplate.InfoPanel, "NoticeListScreen", new[]
                {
                    "reallife_ui_notice_list", "reallife_ui_notice_none", "reallife_ui_notice_clear", "reallife_ui_prompt_page", "reallife_ui_prompt_back",
                    "reallife_ui_notice_road", "reallife_ui_notice_police", "reallife_ui_notice_health", "reallife_ui_notice_money",
                    "reallife_ui_notice_property",
                    "reallife_ui_notice_work", "reallife_ui_notice_business", "reallife_ui_notice_tech", "reallife_ui_notice_news", "reallife_ui_notice_people",
                    "reallife_ui_notice_other", "reallife_ui_notice_done", "reallife_ui_notice_warning", "reallife_ui_notice_info"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("shell_frame", UiTemplate.AppWindow, null, new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back"
                }),
            });
        }
    }
}
