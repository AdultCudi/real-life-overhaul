using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's Contacts app, drawn on the kit over the game's own.
        static partial void AddContacts(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("contacts", UiTemplate.ListDetail, "ContactsScreen", new[]
                {
                    "reallife_ui_contacts_app", "reallife_ui_contacts_groups", "reallife_ui_contacts_all", "reallife_ui_contacts_search",
                    "reallife_ui_contacts_none_title", "reallife_ui_contacts_none_body", "reallife_ui_contacts_quiet_title", "reallife_ui_contacts_quiet_body",
                    "reallife_ui_contacts_start", "reallife_ui_contacts_classic", "reallife_ui_contacts_message", "reallife_ui_contacts_send",
                    "reallife_ui_contacts_requests", "reallife_ui_contacts_profile", "reallife_ui_contacts_details", "reallife_ui_contacts_pick_title",
                    "reallife_ui_contacts_pick_body", "reallife_ui_contacts_new", "reallife_ui_contacts_earlier", "reallife_ui_contacts_summary",
                    "reallife_ui_contacts_day", "reallife_ui_price_free", "reallife_ui_price_quote", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                    "reallife_ui_prompt_group", "reallife_ui_prompt_back", "contacts_mark_all_as_read_button", "contacts_delete_all_button", "phone_call_button",
                    "phone_view_on_map_button", "contacts_manage_employee_button", "contacts_manage_delivery_button", "myemployees_manage_schedule",
                    "contacts_candidates_button", "contacts_rival_button", "contacts_tax_button", "contacts_header_clear_chat_button",
                    "contacts_today_opening_hours", "common_address", "contacts_schedule_demands", "myemployees_hourlywage",
                    "bizman_headhunters_skill_level_title", "contactcategoryname_general", "contactcategoryname_business", "contactcategoryname_finance",
                    "contactcategoryname_furnitureandequipment", "contactcategoryname_importsandgoods", "contactcategoryname_employees",
                    "contactcategoryname_rivals"
                }).Switch(UiScreenSwitches.Apps),
            });
        }
    }
}
