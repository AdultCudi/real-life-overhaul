using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Economy, the city's people, Newsstand, the City Ledger and the ending. Its own section: the apps section is full.
        // Labels come from a method. A static field of another part of this class is not set yet when All is gathered.
        static partial void AddNews(List<UiScreenDef> screens)
        {
            string[] shared = NewsShared();
            screens.Add(new UiScreenDef("economy", UiTemplate.AppWindow, "EconomyScreen", NewsKeys(shared, EconomyWords()))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("people", UiTemplate.AppWindow, "PeopleScreen", NewsKeys(shared,
                EconomyWords("reallife_news_people_title", "reallife_news_rent_link", "reallife_news_busy_link")))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("newsstand", UiTemplate.AppWindow, "NewsstandScreen", NewsKeys(shared,
                "reallife_ledger_online_app", "reallife_ledger_online_tab_titles", "reallife_ledger_online_tab_reading"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("ledger", UiTemplate.AppWindow, "LedgerScreen", NewsKeys(shared, "reallife_ledger_title", "reallife_news_nav_markets",
                "reallife_ledger_opened_city", "reallife_ledger_head_opened_city"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("ending", UiTemplate.Decision, "EndingScreen", NewsKeys(shared,
                "reallife_news_end_kicker", "reallife_news_end_load", "reallife_news_end_quit"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("household", UiTemplate.AppWindow, "HouseholdScreen", NewsKeys(shared,
                EconomyWords("reallife_news_home_title", "reallife_news_rent_link")))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("renters", UiTemplate.AppWindow, "RentersScreen", NewsKeys(shared,
                EconomyWords("reallife_news_rent_link", "reallife_news_people_title")))
                .Switch(UiScreenSwitches.News));
        }

        // The Economy window's own words: its name and its tabs. The pages opened from a tab (the people, the renters, a household) stand in the same window.
        private static string[] EconomyWords(params string[] more)
        {
            string[] own =
            {
                "reallife_econapp_app", "reallife_news_nav_overview", "reallife_news_nav_prices", "reallife_news_nav_jobs",
                "reallife_news_nav_districts", "reallife_news_nav_markets", "reallife_news_nav_finances", "reallife_news_nav_insights",
                "reallife_news_nav_people"
            };
            var all = new string[own.Length + more.Length];
            own.CopyTo(all, 0);
            more.CopyTo(all, own.Length);
            return all;
        }

        private static string[] NewsKeys(string[] shared, params string[] own)
        {
            var keys = new string[own.Length + shared.Length];
            for (int i = 0; i < own.Length; i++)
            {
                keys[i] = own[i];
            }
            for (int i = 0; i < shared.Length; i++)
            {
                keys[own.Length + i] = shared[i];
            }
            return keys;
        }

        private static string[] NewsShared()
        {
            return new[]
            {
                "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back",
                "reallife_econapp_verdict_slowing",
                "reallife_news_series_inflation", "reallife_news_series_rate", "reallife_news_series_wages",
                "reallife_news_series_jobless", "reallife_news_series_vacancies", "reallife_news_series_reserve",
                "reallife_news_series_revenue", "reallife_news_series_spending", "reallife_news_series_index",
                "reallife_news_basket", "reallife_news_led", "reallife_news_moved",
                "reallife_news_rise", "reallife_news_no_rise", "reallife_news_advice", "reallife_news_chart",
                "reallife_news_pay_title", "reallife_news_pay_body", "reallife_news_open_staff",
                "reallife_news_fix_title", "reallife_news_fix_body", "reallife_news_fix_do",
                "reallife_news_pull_title", "reallife_news_pull_body", "reallife_news_open_map",
                "reallife_news_def_title", "reallife_news_def_body", "reallife_news_open_stocks", "reallife_news_open_finance",
                "reallife_news_col_good", "reallife_news_col_today", "reallife_news_col_week", "reallife_news_col_month",
                "reallife_news_col_spark", "reallife_news_col_yours", "reallife_news_col_district", "reallife_news_col_pull", "reallife_news_col_rent",
                "reallife_macro_state_boom", "reallife_macro_state_steady", "reallife_macro_state_slowdown",
                "reallife_news_col_name", "reallife_news_empty_city", "reallife_news_empty_city_body",
                "reallife_news_range_30", "reallife_news_range_120", "reallife_news_range_year",
                "reallife_news_people_title", "reallife_news_head_people", "reallife_news_head_homes",
                "reallife_news_head_jobs", "reallife_news_head_rent", "reallife_news_ages",
                "reallife_news_age_young", "reallife_news_age_work", "reallife_news_age_older",
                "reallife_news_income_title", "reallife_news_income_work", "reallife_news_income_middle", "reallife_news_income_upper",
                "reallife_news_legend", "reallife_news_scale", "reallife_news_low", "reallife_news_high",
                "reallife_news_rent_link", "reallife_news_busy_link", "reallife_news_home_title",
                "reallife_news_empty_rent", "reallife_news_empty_rent_body", "reallife_news_not_yet", "reallife_news_where",
                "reallife_news_stand_sum", "reallife_news_stand_save",
                "reallife_news_stand_sub", "reallife_news_stand_stop",
                "reallife_news_stand_off", "reallife_news_stand_off_body", "reallife_news_stand_out", "reallife_news_stand_out_body",
                "reallife_news_read_none", "reallife_news_read_not_held", "reallife_news_read_not_held_body", "reallife_ledger_online_to_titles",
                "reallife_ledger_online_printed", "reallife_ledger_online_nothing", "reallife_ledger_online_box_kept", "reallife_ledger_online_gone",
                "reallife_ledger_online_day_today", "reallife_ledger_online_day", "reallife_news_in_days",
                "reallife_ledger_online_name_ledger", "reallife_ledger_online_name_exchange",
                "reallife_ledger_online_name_street", "reallife_ledger_online_name_review", "reallife_ledger_online_name_pass",
                "reallife_news_paper_quiet", "reallife_news_paper_quiet_body",
                "reallife_news_end_kicker", "reallife_news_end_body", "reallife_news_end_load", "reallife_news_end_quit",
                "reallife_news_end_days", "reallife_news_end_worth", "reallife_news_end_shops", "reallife_news_end_staff", "reallife_news_end_rank",
                "reallife_ledger_title",
                "reallife_news_axis_day", "reallife_news_band_title", "reallife_news_band_note", "reallife_news_basket_flat",
                "reallife_news_basket_label", "reallife_news_basket_less", "reallife_news_chip_led", "reallife_news_col_close",
                "reallife_news_col_code", "reallife_news_col_company", "reallife_news_col_move", "reallife_news_col_price",
                "reallife_news_col_sector", "reallife_news_col_shares", "reallife_news_districts_pull", "reallife_news_empty_board",
                "reallife_news_end_title", "reallife_news_five_note", "reallife_news_goods_index", "reallife_news_hall_detail",
                "reallife_news_hall_mark", "reallife_news_head_growing", "reallife_news_head_overheating", "reallife_news_head_recession",
                "reallife_news_head_slowing", "reallife_news_head_steady", "reallife_news_hold_down", "reallife_news_hold_head",
                "reallife_news_hold_up", "reallife_news_holdings", "reallife_news_in_month", "reallife_news_index_close", "reallife_news_jobs_table",
                "reallife_news_moved_detail", "reallife_news_moved_rival", "reallife_news_movers", "reallife_news_on_day",
                "reallife_news_prices_shops", "reallife_news_prices_today", "reallife_news_sectors", "reallife_news_stand_chip",
                "reallife_news_stand_lead", "reallife_news_stand_to_read", "reallife_news_stand_week", "reallife_news_the_economy",
                "reallife_ledger_tab_front", "reallife_ledger_tab_markets", "reallife_ledger_tab_prices", "reallife_ledger_risers",
                "reallife_ledger_fallers", "reallife_news_traded",
                "reallife_pop_fig_unemployment", "reallife_pop_fig_rent", "reallife_pop_fig_jobs", "reallife_pop_fig_moved_in",
                "reallife_pop_fig_moved_out", "reallife_ledger_online_box_net", "reallife_ledger_online_box_today",
                "reallife_ledger_online_state_renews", "reallife_ledger_online_tagline_ledger",
                "reallife_ledger_online_tagline_exchange", "reallife_ledger_online_tagline_street",
                "reallife_ledger_online_tagline_review", "reallife_ledger_online_about_ledger",
                "reallife_ledger_online_about_exchange", "reallife_ledger_online_about_street",
                "reallife_ledger_online_about_review", "reallife_ledger_online_about_pass", "reallife_ledger_online_today_in",
                "reallife_ledger_dateline_number", "reallife_ledger_dateline_price", "reallife_ledger_sec_economy",
                "reallife_ledger_sec_city", "reallife_ledger_sec_business", "reallife_ledger_sec_around",
                "reallife_ledger_sec_weather", "reallife_ledger_sec_markets", "reallife_ledger_quiet_head",
                "reallife_ledger_quiet_body", "reallife_ledger_prices_legend", "reallife_pd_obit_kicker",
                "reallife_econapp_day", "reallife_econapp_verdict_line_slowing", "reallife_econapp_tile_inflation",
                "reallife_econapp_tile_rate", "reallife_econapp_tile_unemployment", "reallife_ledger_week"
            };
        }
    }
}
