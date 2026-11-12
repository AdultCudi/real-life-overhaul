using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's Contacts app, drawn on the kit over the game's own: an app window with its groups down the sidebar, and a
        // call in progress as the call card (UiContacts, Scripts/Ui/Contacts).
        static partial void AddContacts(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("contacts", UiTemplate.AppWindow, "ContactsScreen", new[]
                {
                    "reallife_ui_contacts_app", "reallife_ui_contacts_all", "reallife_ui_contacts_search", "reallife_ui_contacts_none_title", "reallife_ui_contacts_none_body",
                    "reallife_ui_contacts_quiet_title", "reallife_ui_contacts_quiet_body", "reallife_ui_contacts_start", "reallife_ui_contacts_requests", "reallife_ui_contacts_profile",
                    "reallife_ui_contacts_details", "reallife_ui_contacts_pick_title", "reallife_ui_contacts_pick_body", "reallife_ui_contacts_new", "reallife_ui_contacts_earlier",
                    "reallife_ui_contacts_summary", "reallife_ui_contacts_day", "reallife_ui_contacts_you", "reallife_ui_contacts_other_player", "reallife_ui_contacts_game_view",
                    "reallife_ui_contacts_delete", "reallife_ui_contacts_waiting", "reallife_ui_contacts_waiting_on", "reallife_ui_contacts_waiting_from",
                    "reallife_ui_contacts_from_two", "reallife_ui_contacts_from_more",
                    "reallife_ui_test_contacts_tax_note", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_group", "reallife_ui_prompt_back",
                    "reallife_ui_prompt_choose",
                    "reallife_ui_parts_clear_search", "myemployees_jobtype", "dialog_health_insurance_negotiation_start",
                    "contacts_mark_all_as_read_button", "contacts_delete_all_button", "phone_call_button", "phone_view_on_map_button",
                    "contacts_manage_employee_button", "contacts_manage_delivery_button", "myemployees_manage_schedule", "contacts_candidates_button", "contacts_rival_button",
                    "contacts_tax_button", "contacts_header_clear_chat_button", "contacts_today_opening_hours", "common_address", "contacts_schedule_demands",
                    "myemployees_hourly_wage", "bizman_headhunters_skill_level_title", "myemployees_satisfaction", "common_unassigned", "ba:skill_customerservice",
                    "contactcategoryname_general", "contactcategoryname_business", "contactcategoryname_finance", "contactcategoryname_furnitureandequipment",
                    "contactcategoryname_importsandgoods", "contactcategoryname_employees", "contactcategoryname_rivals", "contactcategoryname_101", "contactcategoryname_102",
                    "contactcategoryname_103", "contactcategoryname_104", "contactcategoryname_105", "contactcategoryname_106", "contactcategoryname_107",
                    "dialog_accept_button", "dialog_decline_button", "dialog_negotiate_button", "dialog_send_offer_button", "dialog_hang_up_button",
                    "dialog_candidate_salary_offer_hourly_wage", "dialog_candidate_salary_offer_input_hourly_wage", "dialog_candidate_salary_offer_input_signing_bonus",
                    "dialog_player_offer_input", "dialog_health_insurance_plan_offer_price", "dialog_negotiation_negotiate_counter_offer", "contacts_salary_negotiation_start",
                    "common_accepted", "common_declined", "common_subtotal", "contacts_taxes_message_heading", "contacts_taxes_message_ending", "contacts_taxes_income",
                    "contacts_taxes_gambling", "contacts_taxes_tax_deductions", "contacts_taxes_real_estate", "contacts_taxes_grand_total", "contacts_taxes_taxable_income",
                    "contacts_taxes_tax_rate", "contacts_taxes_income_tax", "contacts_taxes_real_estate_tax", "contacts_taxes_total_tax_due",
                    "contacts_taxes_repossession_message_heading", "contacts_taxes_repossession_message_ending", "contacts_taxes_repossessed_valuables",
                    "contacts_taxes_back_taxes_still_owed"
                }).Switch(UiScreenSwitches.Apps),
            });
        }
    }
}
