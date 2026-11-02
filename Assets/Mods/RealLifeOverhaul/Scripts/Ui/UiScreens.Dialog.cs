using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's conversations on the kit (Scripts/Ui/Dialog): the dialogue panel as the conversation docked at the right
        // over the live scene, with every form the game asks in a line; the spoken lines (Uncle Fred's, a rival's) as the
        // notice card at the top centre; and the mod's scenes (a hearing, a fame or office scene, an interview, a pitch) as
        // the decision card or the docked conversation. Each has its own switch among the boxes.
        static partial void AddDialog(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("dialog", UiTemplate.AppWindow, "DialogScreen", new[]
                {
                    "reallife_ui_dialog_title", "itempanelui_buttons_leave", "dialog_bank_larry", "dialog_bank_selector", "dialog_bank_new_loan", "dialog_bank_new_investment",
                    "common_confirm", "dialog_marketing_agency_npc_name", "dialog_marketing_agency_start", "dialog_marketing_agency_start_new_campaign",
                    "dialog_marketing_agency_campaign_settings_header", "common_business", "marketingtypename_friendbookads", "marketingtypename_jitterads",
                    "marketingtypename_latergramads", "marketingtypename_voogleads", "common_price_per_day", "reallife-business", "reallife_biz_undercut_name",
                    "reallife_biz_desk_options", "reallife_biz_desk_next", "reallife_biz_health_name", "reallife_biz_health_start", "reallife_biz_supplierlate_name",
                    "reallife_biz_supplierlate_start", "reallife_biz_desk_option", "reallife_biz_undercut_marketing", "reallife_biz_undercut_do_marketing",
                    "reallife_biz_desk_another", "reallife_biz_desk_later", "reallife_street_stranger", "reallife_ui_dialog_in_street", "reallife_street_open_warm_1",
                    "reallife_street_ask_day", "reallife_social_btn_else", "reallife_street_btn_goodbye", "reallife_street_day_good_2", "reallife_street_ask_area",
                    "reallife_street_ask_say", "dialog_recruitment_agency_npc_name", "dialog_recruitment_agency_already_has_campaign_new",
                    "dialog_recruitment_agency_manage_campaigns", "reallife_ui_contacts_game_view", "reallife_ui_prompt_move", "reallife_ui_prompt_choose",
                    "dialog_bank_loan_amount_header", "dialog_bank_amount_to_loan", "reallife_biz_desk_issue", "reallife_biz_undercut_start",
                    "reallife_ui_dialog_loan_term", "reallife_ui_dialog_loan_term_years", "reallife_ui_dialog_loan_rate", "reallife_ui_dialog_loan_rate_value",
                    "reallife_ui_dialog_loan_payment", "reallife_ui_dialog_loan_total", "reallife_ui_dialog_loan_cost_in", "reallife_ui_dialog_loan_cost_out",
                    "reallife_ui_dialog_loan_lower", "reallife_ui_dialog_loan_lower_yes", "reallife_ui_dialog_loan_lower_rating", "reallife_ui_dialog_loan_line",
                    "reallife_ui_dialog_loan_line_cost", "reallife_econ_kind_overdraft", "reallife_ui_dialog_mood_unsure", "dialog_health_insurance_manager_npc_name",
                    "dialog_health_insurance_negotiation_start", "dialog_health_insurance_player_offer", "dialog_negotiation_acceptable_1",
                    "dialog_negotiation_counter_offer", "dialog_accept_button", "dialog_negotiate_button", "dialog_decline_button"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("monologue", UiTemplate.Decision, "MonologueScreen", new[]
                {
                    "uncle_fred", "reallife_ui_monologue_count", "reallife_ui_monologue_next", "reallife_ui_monologue_skip", "common_close",
                    "ba:messagetype_tutorial_quest_find_a_job_uncle_fred", "ba:messagetype_tutorial_quest_casino_uncle_fred", "ba:messagetype_tutorial_quest_get_some_food_uncle_fred"
                }).Switch(UiScreenSwitches.Boxes),
                new UiScreenDef("scenes", UiTemplate.Decision, "SceneScreen", new[]
                {
                    "reallife_ui_prompt_move", "reallife_ui_prompt_choose", "common_cancel", "input_key_menu", "reallife_justice_judge_1", "reallife_justice_prosecutor_1",
                    "reallife_justice_court_subtitle", "reallife_justice_court_title", "reallife_justice_court_aside", "reallife_justice_charge_reckless",
                    "reallife_justice_court_plea", "reallife_justice_plea_guilty", "reallife_justice_plea_guilty_hint", "reallife_justice_plea_not_guilty",
                    "reallife_justice_plea_not_guilty_hint", "reallife_justice_plea_deal", "reallife_justice_deal_reduced", "reallife_justice_charge_evading",
                    "reallife_justice_deal_tail", "reallife_justice_court_charges", "reallife_justice_charge_hit_run_injury", "reallife_justice_charge_injury_driving",
                    "reallife_justice_court_withdrawn", "reallife_justice_charge_hit_run", "reallife_justice_court_on_probation", "reallife_justice_court_call_warrant",
                    "reallife_justice_court_appointed", "reallife_justice_continue", "reallife_justice_file_subtitle", "reallife_justice_file_title",
                    "reallife_justice_file_case", "reallife_justice_file_case_date", "reallife_justice_file_counsel", "reallife_justice_file_owed",
                    "reallife_justice_file_debt_fine", "reallife_justice_file_probation_none", "reallife_justice_file_licence_ok", "reallife_justice_file_actions",
                    "reallife_justice_file_actions_hint", "reallife_justice_file_record", "reallife_justice_file_record_hint", "reallife_justice_file_close",
                    "reallife_fame_show_host", "reallife_fame_scene_progress", "reallife_fame_show_place", "reallife_fame_invite_0", "reallife_fame_show_open",
                    "reallife_fame_q_rise", "reallife_fame_a_rise_0", "reallife_fame_say_rise_0", "reallife_fame_a_rise_1", "reallife_fame_say_rise_1",
                    "reallife_civic_office_door_speaker", "reallife_civic_office_door_title", "reallife_civic_issue_safety", "reallife_civic_issue_business",
                    "reallife_civic_office_door_open", "reallife_civic_office_door_civil", "reallife_civic_office_door_talk_theirs", "reallife_civic_office_hint_plank",
                    "reallife_civic_policy_policing_up", "reallife_civic_office_door_talk_mine", "reallife_civic_office_hint_no_plank", "reallife_civic_office_door_ask_vote",
                    "reallife_civic_office_hint_ask", "ba:skill_customerservice", "reallife_hire_meet_title", "reallife_hire_meet_opening", "reallife_hire_topic_project",
                    "reallife_hire_meet_heard_years", "reallife_hire_meet_progress", "reallife_hire_topic_technical", "reallife_hire_topic_technical_detail",
                    "reallife_hire_topic_motive", "reallife_hire_topic_motive_detail", "reallife_hire_topic_pay", "reallife_hire_topic_pay_detail", "reallife_hire_topic_team",
                    "reallife_hire_topic_team_detail", "reallife_hire_meet_leave", "reallife_hire_meet_leave_detail", "reallife_hire_meet_over", "reallife_hire_meet_summary",
                    "reallife_hire_meet_close", "reallife_hire_meet_close_detail", "reallife_ven_meet_title", "reallife_ven_q_f04", "reallife_ven_meet_progress",
                    "reallife_ven_meet_answer", "reallife_ven_meet_leave", "reallife_ven_meet_leave_detail", "reallife_ven_meet_result_yes", "reallife_ven_verdict_invest",
                    "reallife_ven_verdict_helped", "reallife_ven_meet_take_title", "reallife_ven_meet_take", "reallife_ven_meet_think", "reallife_ven_meet_think_detail",
                    "reallife_ven_meet_decline", "reallife_vc_meet_founders_open", "reallife_vc_topic_revenue", "reallife_vc_meet_title_founders", "reallife_vc_topic_team",
                    "reallife_vc_topic_market", "reallife_vc_topic_burn", "reallife_vc_topic_terms", "reallife_vc_meet_leave", "reallife_ven_q_j01",
                    "reallife_ui_scene_present", "reallife_justice_court_you", "reallife_justice_role_prosecutor", "reallife_justice_role_inspector",
                    "reallife_justice_role_judge", "reallife_justice_role_counsel", "reallife_justice_role_defendant", "reallife_justice_cost_hours",
                    "reallife_justice_cost_fine", "reallife_justice_cost_community", "reallife_justice_odds", "reallife_justice_cost_lost_fine",
                    "reallife_justice_cost_lost_community", "reallife_justice_charge_tax_evasion", "reallife_justice_plea_settle",
                    "reallife_justice_plea_settle_hint", "reallife_justice_settle_cost", "reallife_justice_settle_gain"
                }).Switch(UiScreenSwitches.Boxes),
            });
        }
    }
}
