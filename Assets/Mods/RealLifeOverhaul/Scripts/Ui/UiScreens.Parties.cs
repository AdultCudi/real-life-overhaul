using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The Parties app on the kit (Scripts/Ui/Parties): Host a party, Staff events and the Diary on the window and runner the Ventures apps share (VentScreen).
        // The key list is every label its pages write with (the pages' w.T and w.F calls, and the names PartyKeys gives a place, a tier, a kind and an answer).
        static partial void AddParties(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("parties", UiTemplate.AppWindow, "VentScreen", new[]
            {
                // KEYS BEGIN
                "reallife_party_answer_asleep", "reallife_party_answer_busy", "reallife_party_answer_far", "reallife_party_answer_notclose",
                "reallife_party_answer_quarrel", "reallife_party_answer_rough", "reallife_party_answer_working", "reallife_party_answer_yes", "reallife_party_app",
                "reallife_party_d_accept", "reallife_party_d_back", "reallife_party_d_cancel", "reallife_party_d_city_none", "reallife_party_d_decline",
                "reallife_party_d_locate", "reallife_party_d_none_body", "reallife_party_d_none_title", "reallife_party_d_state", "reallife_party_d_when",
                "reallife_party_diary_next", "reallife_party_e_book", "reallife_party_e_business", "reallife_party_e_cost", "reallife_party_e_every",
                "reallife_party_e_every_days", "reallife_party_e_every_off", "reallife_party_e_kind", "reallife_party_e_last", "reallife_party_e_low",
                "reallife_party_e_manager_is", "reallife_party_e_manager_none", "reallife_party_e_managers", "reallife_party_e_never", "reallife_party_e_note",
                "reallife_party_e_plan_kind", "reallife_party_e_staff", "reallife_party_e_underpaid", "reallife_party_f_cost", "reallife_party_f_day",
                "reallife_party_f_guests", "reallife_party_f_head", "reallife_party_f_hour", "reallife_party_f_hours", "reallife_party_f_lasts", "reallife_party_f_note",
                "reallife_party_f_other", "reallife_party_f_place", "reallife_party_f_place_none", "reallife_party_f_room", "reallife_party_f_send",
                "reallife_party_f_tier", "reallife_party_f_where", "reallife_party_g_answer", "reallife_party_g_ask", "reallife_party_g_list",
                "reallife_party_g_none_body", "reallife_party_g_none_title", "reallife_party_g_not_asked", "reallife_party_g_take_off", "reallife_party_g_who",
                "reallife_party_host_next", "reallife_party_kind_evening", "reallife_party_kind_lunch", "reallife_party_kind_teamday", "reallife_party_off_body",
                "reallife_party_off_title", "reallife_party_place_bar", "reallife_party_place_home", "reallife_party_place_restaurant", "reallife_party_sec_city",
                "reallife_party_sec_coming", "reallife_party_sec_event", "reallife_party_sec_guests", "reallife_party_sec_manager", "reallife_party_sec_party",
                "reallife_party_sec_past", "reallife_party_sec_staff", "reallife_party_staff_next", "reallife_party_staff_none", "reallife_party_tab_diary",
                "reallife_party_tab_host", "reallife_party_tab_staff", "reallife_party_tier_good", "reallife_party_tier_lavish", "reallife_party_tier_simple",
                "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select"
                // KEYS END
            }).Switch(UiScreenSwitches.Life));
        }
    }
}
