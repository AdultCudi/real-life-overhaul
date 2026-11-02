using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // BizMan's operations (the schedule as a board, the managers' plans and forms, the factory, the warehouse), drawn
        // by BizMan's own screen while their switch is on, and the uniform presets' editor beside the game's 3D preview.
        static partial void AddBizOps(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("bizops", UiTemplate.AppWindow, "BizManScreen", new[]
                {
                    "ba:businesstype_coffeeshop", "ba:businesstype_factory", "ba:businesstype_warehouse", "ba:itemname_screencinema", "ba:skill_factoryworker",
                    "bizman_add_plan", "bizman_delete_plan", "bizman_deliveries_urgent_order", "bizman_drivers_driver", "bizman_drivers_slot_number",
                    "bizman_drivers_vehicle", "bizman_drivers_vehicle_info", "bizman_factory_group_table_produced_per_hour",
                    "bizman_factory_inactive_reason_invalid_workstation", "bizman_factory_material_consumption_title", "bizman_factory_material_in_stock",
                    "bizman_factory_material_runs_out_in", "bizman_factory_no_employee_assigned", "bizman_factory_not_running",
                    "bizman_factory_produce_continuously", "bizman_factory_produce_up_to", "bizman_factory_production_flow_title",
                    "bizman_factory_production_machines", "bizman_factory_running", "bizman_factory_total_production_per_hour_title",
                    "bizman_factory_workstation_name_placeholder", "bizman_headhunter_not_assigned", "bizman_headhunter_not_assigned_description",
                    "bizman_headhunters_automatic_replacement_not_enough_skill_description", "bizman_headhunters_automatic_replacement_not_enough_skill_title",
                    "bizman_headhunters_currently_recruiting_title", "bizman_headhunters_deal_breakers_clear_all_button",
                    "bizman_headhunters_deal_breakers_title", "bizman_headhunters_employee_type_title", "bizman_headhunters_replacement_reason",
                    "bizman_headhunters_salary_range_title", "bizman_headhunters_skill_level_title", "bizman_headhunters_start_recruiting_button",
                    "bizman_headhunters_stop_recruiting_button", "bizman_headhunters_type_of_employee_title",
                    "bizman_headhunters_view_current_recruits_button", "bizman_headhunters_wage_and_skill_title", "bizman_hrmanager_assign_employees",
                    "bizman_hrmanager_average_salary", "bizman_hrmanager_cancel_insurance_button", "bizman_hrmanager_clear", "bizman_hrmanager_fill",
                    "bizman_hrmanager_insurance_demand", "bizman_hrmanager_not_assigned", "bizman_hrmanager_not_assigned_description",
                    "bizman_hrmanager_salary", "bizman_hrmanager_upgrade_insurance_button", "bizman_inventory_balance", "bizman_inventory_days_until_empty",
                    "bizman_inventory_import", "bizman_inventory_in_stock", "bizman_inventory_never_runs_out", "bizman_inventory_product_days_until_empty",
                    "bizman_inventory_run_out", "bizman_inventory_sell_all", "bizman_inventory_shipped", "bizman_logisticsmanagers_adddestination",
                    "bizman_logisticsmanagers_min_stock_amount", "bizman_logisticsmanagers_no_products", "bizman_logisticsmanagers_not_assigned",
                    "bizman_logisticsmanagers_not_assigned_description", "bizman_logisticsmanagers_runs_out_in", "bizman_logisticsmanagers_warehouse_stock",
                    "bizman_menu_deliveries", "bizman_menu_drivers", "bizman_menu_factory", "bizman_menu_headhunters",
                    "bizman_menu_headhunters_automatic_replacement_header", "bizman_menu_headhunters_recruiting_header", "bizman_menu_hr_managers",
                    "bizman_menu_insight", "bizman_menu_inventory", "bizman_menu_logistics_managers", "bizman_menu_marketing", "bizman_menu_pricing_managers",
                    "bizman_menu_purchasing_agents", "bizman_menu_schedule", "bizman_menu_settings", "bizman_pricingmanagers_apply_suggested_prices",
                    "bizman_pricingmanagers_header_current_price", "bizman_pricingmanagers_header_new_price", "bizman_pricingmanagers_header_product",
                    "bizman_pricingmanagers_header_rivals_prices", "bizman_pricingmanagers_header_suggested_price", "bizman_pricingmanagers_no_products",
                    "bizman_pricingmanagers_not_assigned", "bizman_pricingmanagers_not_assigned_description", "bizman_purchasingagents_cancel_order",
                    "bizman_purchasingagents_designated_warehouse", "bizman_purchasingagents_end_partnership", "bizman_purchasingagents_lock_time_header",
                    "bizman_purchasingagents_next_delivery", "bizman_purchasingagents_not_assigned", "bizman_purchasingagents_not_assigned_description",
                    "bizman_purchasingagents_order", "bizman_purchasingagents_price", "bizman_purchasingagents_product",
                    "bizman_purchasingagents_smart_delivery", "bizman_purchasingagents_total", "bizman_purchasingagents_warehouse_stock",
                    "bizman_schedule_auto_fill", "bizman_schedule_auto_fill_day", "bizman_schedule_auto_fill_loading",
                    "bizman_schedule_cannot_change_working_hours", "bizman_schedule_clear_filter", "bizman_schedule_customers_per_hour",
                    "bizman_schedule_employee_doesnt_have_required_skill", "bizman_schedule_employee_header_no_workstation",
                    "bizman_schedule_employee_no_employees", "bizman_schedule_employee_not_available", "bizman_schedule_employee_search_placeholder",
                    "bizman_schedule_fast_autofill", "bizman_schedule_hourly_wage", "bizman_schedule_manage_employee_tooltip",
                    "bizman_schedule_notification_cant_assign_on_closed_days", "bizman_schedule_one_customer_per_hour", "bizman_schedule_opening_hours",
                    "bizman_schedule_rename_workstation", "bizman_schedule_search_workstation", "bizman_security", "bizman_security_level_good",
                    "bizman_vehicle_slots", "bizman_warehouse_driverstation_no_vehicle_assigned", "bizman_warehouses", "common_business", "common_cancel",
                    "common_neighborhood", "common_open", "common_open_in_econoview", "common_per_hour", "common_product", "common_requirements",
                    "common_show_warehouse", "common_unassigned", "reallife_interior_tab", "reallife_ui_bizman_needs", "reallife_ui_bizman_prompt_section",
                    "reallife_ui_bizman_tab_building", "reallife_ui_bizman_tab_hr", "reallife_ui_bizman_tab_logistics", "reallife_ui_bizman_tab_prices",
                    "reallife_ui_bizman_tab_pricing", "reallife_ui_bizman_tab_purchasing", "reallife_ui_bizops_amount", "reallife_ui_bizops_and_more",
                    "reallife_ui_bizops_assigned", "reallife_ui_bizops_auto_title", "reallife_ui_bizops_clear", "reallife_ui_bizops_clear_week",
                    "reallife_ui_bizops_closed_day", "reallife_ui_bizops_col_assigned", "reallife_ui_bizops_col_boxes", "reallife_ui_bizops_col_fit",
                    "reallife_ui_bizops_col_machine", "reallife_ui_bizops_col_makes", "reallife_ui_bizops_col_person", "reallife_ui_bizops_col_skill",
                    "reallife_ui_bizops_col_wage", "reallife_ui_bizops_col_week", "reallife_ui_bizops_copy", "reallife_ui_bizops_day_1",
                    "reallife_ui_bizops_day_2", "reallife_ui_bizops_day_3", "reallife_ui_bizops_day_4", "reallife_ui_bizops_day_5", "reallife_ui_bizops_day_6",
                    "reallife_ui_bizops_day_7", "reallife_ui_bizops_designate", "reallife_ui_bizops_done", "reallife_ui_bizops_factories",
                    "reallife_ui_bizops_fee", "reallife_ui_bizops_fit_best", "reallife_ui_bizops_fit_busy", "reallife_ui_bizops_fit_demand",
                    "reallife_ui_bizops_fit_free", "reallife_ui_bizops_fit_tired", "reallife_ui_bizops_goods_count", "reallife_ui_bizops_hours",
                    "reallife_ui_bizops_hours_line", "reallife_ui_bizops_hours_week", "reallife_ui_bizops_hr_assign_none", "reallife_ui_bizops_hr_cover",
                    "reallife_ui_bizops_hr_line", "reallife_ui_bizops_hr_mood", "reallife_ui_bizops_hr_none", "reallife_ui_bizops_hr_none_body",
                    "reallife_ui_bizops_hr_replace", "reallife_ui_bizops_hr_skill", "reallife_ui_bizops_hr_staff", "reallife_ui_bizops_hr_staff_none",
                    "reallife_ui_bizops_hr_staff_none_body", "reallife_ui_bizops_hr_train", "reallife_ui_bizops_hunt_always", "reallife_ui_bizops_hunt_amount",
                    "reallife_ui_bizops_hunt_breaker", "reallife_ui_bizops_hunt_count", "reallife_ui_bizops_hunt_line", "reallife_ui_bizops_hunt_none",
                    "reallife_ui_bizops_hunt_none_body", "reallife_ui_bizops_hunt_resign", "reallife_ui_bizops_hunt_retire", "reallife_ui_bizops_hunt_staff",
                    "reallife_ui_bizops_hunt_staff_none", "reallife_ui_bizops_hunt_target", "reallife_ui_bizops_imports_goods_none",
                    "reallife_ui_bizops_imports_none", "reallife_ui_bizops_imports_none_body", "reallife_ui_bizops_in_stock", "reallife_ui_bizops_lanes_none",
                    "reallife_ui_bizops_lanes_none_body", "reallife_ui_bizops_machine_choose", "reallife_ui_bizops_machine_choose_body",
                    "reallife_ui_bizops_machine_missing", "reallife_ui_bizops_machines_none", "reallife_ui_bizops_machines_none_body",
                    "reallife_ui_bizops_manager", "reallife_ui_bizops_no_manager", "reallife_ui_bizops_not_assigned", "reallife_ui_bizops_off",
                    "reallife_ui_bizops_on", "reallife_ui_bizops_open_on", "reallife_ui_bizops_output", "reallife_ui_bizops_paste",
                    "reallife_ui_bizops_paste_all", "reallife_ui_bizops_pick", "reallife_ui_bizops_pick_all", "reallife_ui_bizops_pick_line",
                    "reallife_ui_bizops_pick_title", "reallife_ui_bizops_plan_choose", "reallife_ui_bizops_plan_choose_body",
                    "reallife_ui_bizops_pricing_none", "reallife_ui_bizops_pricing_none_body", "reallife_ui_bizops_production_none",
                    "reallife_ui_bizops_range", "reallife_ui_bizops_recipe", "reallife_ui_bizops_remove_shift", "reallife_ui_bizops_routes_none",
                    "reallife_ui_bizops_routes_none_body", "reallife_ui_bizops_skill_line", "reallife_ui_bizops_slots_none", "reallife_ui_bizops_span",
                    "reallife_ui_bizops_stock_none", "reallife_ui_bizops_stock_none_body", "reallife_ui_bizops_stop_goods", "reallife_ui_bizops_stops",
                    "reallife_ui_bizops_stops_none", "reallife_ui_bizops_target", "reallife_ui_bizops_worker", "reallife_ui_prompt_back",
                    "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_staff_all_demands", "reallife_ui_staff_chosen", "repeating_order",
                    "smartphone_bizman", "stage_with_licensing_fee_disabled", "stage_with_licensing_fee_enabled"
                }).Switch(UiScreenSwitches.Business),
                new UiScreenDef("uniforms", UiTemplate.InfoPanel, "UniformsScreen", new[]
                {
                    "myemployees_configure_uniform_presets", "myemployees_uniform", "reallife_ui_bizops_done", "reallife_ui_bizops_uniform_colour",
                    "reallife_ui_bizops_uniform_copy", "reallife_ui_bizops_uniform_delete", "reallife_ui_bizops_uniform_female",
                    "reallife_ui_bizops_uniform_male", "reallife_ui_bizops_uniform_name", "reallife_ui_bizops_uniform_none",
                    "reallife_ui_bizops_uniform_none_body", "reallife_ui_bizops_uniform_part", "reallife_ui_bizops_uniform_pick",
                    "reallife_ui_bizops_uniform_pick_body", "reallife_ui_bizops_uniform_pieces_none", "reallife_ui_bizops_uniform_presets",
                    "reallife_ui_bizops_uniform_turn", "reallife_ui_bizops_uniform_unsaved", "reallife_ui_bizops_wear_feet",
                    "reallife_ui_bizops_wear_feetaccessory", "reallife_ui_bizops_wear_headaccessory", "reallife_ui_bizops_wear_legs",
                    "reallife_ui_bizops_wear_legsaccessory", "reallife_ui_bizops_wear_other", "reallife_ui_bizops_wear_torso",
                    "reallife_ui_bizops_wear_torsoaccessory", "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select",
                    "uniform_customizer_create_new_preset", "uniform_customizer_save_preset_changes"
                }).Switch(UiScreenSwitches.Business),
            });
        }
    }
}
