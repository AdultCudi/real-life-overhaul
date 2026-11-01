using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's My Employees and a rival's staff, drawn on the kit over the game's own (Scripts/Ui/Employees).
        static partial void AddEmployees(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("my_employees", UiTemplate.AppWindow, "EmployeesScreen", new[]
                {
                    "common_search", "reallife_ui_staff_count", "reallife_ui_staff_pick",
                    "reallife_ui_staff_picked", "reallife_ui_staff_done", "reallife_ui_staff_totals", "reallife_ui_staff_none_match", "reallife_ui_staff_none_match_body",
                    "reallife_ui_staff_clear", "reallife_ui_staff_no_candidates", "reallife_ui_staff_no_candidates_body", "reallife_ui_staff_no_staff",
                    "reallife_ui_staff_no_staff_body", "reallife_ui_staff_open", "reallife_ui_staff_demands_met", "reallife_ui_staff_same_skill", "reallife_ui_staff_mates",
                    "reallife_ui_staff_people", "reallife_ui_staff_see_all", "reallife_ui_staff_pick_none", "reallife_ui_staff_pick_all", "reallife_ui_staff_asking",
                    "reallife_ui_staff_week", "reallife_ui_staff_no_demands", "reallife_ui_staff_work",
                    "reallife_ui_staff_this_week", "reallife_ui_staff_week_hours", "reallife_ui_staff_bonus_ready", "reallife_ui_staff_training",
                    "reallife_ui_staff_training_body", "reallife_ui_staff_prompt_tab", "reallife_ui_staff_all_businesses", "reallife_ui_staff_chosen",
                    "reallife_ui_staff_all_skills", "reallife_ui_staff_all_demands", "reallife_ui_staff_any_status", "reallife_ui_staff_expires", "reallife_ui_staff_met",
                    "reallife_ui_staff_not_met", "reallife_ui_staff_at_work", "reallife_ui_staff_off_shift", "reallife_ui_staff_see_candidates", "myemployees_days_hired",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_co_mass_raise",
                    "smartphone_myemployees", "myemployees_employees_tab", "myemployees_candidates_tab", "myemployees_candidates_hide_job_board", "common_cancel",
                    "common_confirm", "common_ok", "common_unassigned", "common_unassign", "common_select_option", "common_hours", "common_days", "common_hours_left", "myemployees_employee_name",
                    "myemployees_current_business", "myemployees_age", "myemployees_jobtype", "myemployees_hourly_wage", "myemployees_satisfaction", "myemployees_task",
                    "myemployees_schedule", "myemployees_recruitmentsource", "myemployees_expiresin", "myemployees_skills", "myemployees_wants_and_demands",
                    "myemployees_assignbusiness", "ba:myemployees_candidateinfo_header", "myemployees_configure_pay_bonus_tooltip_title", "myemployees_hrmanager",
                    "myemployees_task_training", "myemployees_task_absent", "myemployees_no_task", "myemployees_train_skill_button", "myemployees_called_in_sick",
                    "myemployees_manage_schedule", "myemployees_fire", "myemployees_hirecandidate_button",
                    "myemployees_discardcandidate_button", "dialog_negotiate_button", "myemployees_configure_pay_bonus", "myemployees_pay_bonus_cooldown",
                    "myemployees_mass_action_assignbusiness", "myemployees_mass_action_paybonuses", "myemployees_mass_action_trainprimaryskill",
                    "myemployees_mass_action_fire", "myemployees_mass_action_hirecandidate", "myemployees_mass_action_discardcandidate",
                    "myemployees_mass_action_assignbusinessandhire", "myemployees_mass_action_assign_business_title", "ba:itemname_jobboard",
                    "job_demand_priority_high", "job_demand_priority_medium", "job_demand_priority_low", "ba:skill_customerservice", "ba:skill_cleaning",
                    "ba:skill_securityguard", "ba:skill_deliverydriver", "ba:skill_programmer", "ba:jobdemand_fulltime", "ba:jobdemand_parttime",
                    "ba:jobdemand_freeweekends", "ba:jobdemand_nomornings", "ba:jobdemand_coffeemachine", "ba:jobdemand_silverhealthinsurance",
                    "ba:jobdemand_cleanworkplace", "ba:jobdemand_seatedatofficechair"
                }).Switch(UiScreenSwitches.Apps),
                new UiScreenDef("rival_staff", UiTemplate.AppWindow, "RivalStaffScreen", new[]
                {
                    "reallife_ui_staff_rival_talks", "myemployees_age", "reallife_ui_staff_asking", "myemployees_wants_and_demands", "job_demand_priority_high",
                    "job_demand_priority_medium", "job_demand_priority_low", "ba:jobdemand_fulltime", "ba:jobdemand_freeweekends", "ba:jobdemand_coffeemachine",
                    "ba:jobdemand_silverhealthinsurance", "reallife_ui_staff_rival_former",
                    "reallife_ui_staff_rival_none", "reallife_ui_staff_rival_none_body", "reallife_ui_staff_week", "reallife_ui_prompt_move", "reallife_ui_prompt_back",
                    "smartphone_rivals", "bizman_presentation_rival_employees_list", "myemployees_employee_name", "myemployees_jobtype", "bizman_status",
                    "dialog_negotiate_button", "ba:skill_customerservice", "ba:skill_cleaning", "ba:skill_securityguard"
                }).Switch(UiScreenSwitches.Apps),
            });
        }
    }
}
