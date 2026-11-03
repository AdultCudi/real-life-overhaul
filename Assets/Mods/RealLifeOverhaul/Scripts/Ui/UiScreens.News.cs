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
            screens.Add(new UiScreenDef("economy", UiTemplate.AppWindow, "EconomyScreen", NewsKeys(shared,
                "reallife_econapp_app", "reallife_news_nav_overview", "reallife_news_nav_prices", "reallife_news_nav_jobs",
                "reallife_news_nav_districts", "reallife_news_nav_markets", "reallife_news_nav_finances", "reallife_news_nav_insights",
                "reallife_news_nav_people"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("people", UiTemplate.InfoView, "PeopleScreen", NewsKeys(shared,
                "reallife_news_people_title", "reallife_news_rent_link", "reallife_news_busy_link"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("newsstand", UiTemplate.AppWindow, "NewsstandScreen", NewsKeys(shared,
                "reallife_ledger_online_app", "reallife_ledger_online_tab_titles", "reallife_ledger_online_tab_reading"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("ledger", UiTemplate.AppWindow, "LedgerScreen", NewsKeys(shared, "reallife_ledger_title"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("ending", UiTemplate.Decision, "EndingScreen", NewsKeys(shared,
                "reallife_news_end_kicker", "reallife_news_end_load", "reallife_news_end_quit"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("household", UiTemplate.InfoPanel, "HouseholdScreen", NewsKeys(shared, "reallife_news_home_title"))
                .Switch(UiScreenSwitches.News));
            screens.Add(new UiScreenDef("renters", UiTemplate.AppWindow, "RentersScreen", NewsKeys(shared, "reallife_news_rent_link"))
                .Switch(UiScreenSwitches.News));
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
                "reallife_econapp_verdict_slowing", "reallife_news_lead", "reallife_news_lead_body",
                "reallife_news_series_inflation", "reallife_news_series_rate", "reallife_news_series_wages",
                "reallife_news_rise", "reallife_news_no_rise", "reallife_news_advice", "reallife_news_chart",
                "reallife_news_pay_title", "reallife_news_pay_body", "reallife_news_open_staff",
                "reallife_news_fix_title", "reallife_news_fix_body", "reallife_news_fix_do",
                "reallife_news_pull_title", "reallife_news_pull_body", "reallife_news_open_map",
                "reallife_news_def_title", "reallife_news_def_body", "reallife_news_open_stocks", "reallife_news_open_finance",
                "reallife_news_col_good", "reallife_news_col_today", "reallife_news_col_week", "reallife_news_col_month",
                "reallife_news_col_yours", "reallife_news_col_district", "reallife_news_col_pull", "reallife_news_col_rent",
                "reallife_news_col_name", "reallife_news_empty_city", "reallife_news_empty_city_body",
                "reallife_news_range_30", "reallife_news_range_120", "reallife_news_range_year",
                "reallife_news_people_title", "reallife_news_head_people", "reallife_news_head_homes",
                "reallife_news_head_jobs", "reallife_news_head_rent", "reallife_news_ages",
                "reallife_news_age_young", "reallife_news_age_work", "reallife_news_age_older",
                "reallife_news_legend", "reallife_news_scale", "reallife_news_low", "reallife_news_high",
                "reallife_news_rent_link", "reallife_news_busy_link", "reallife_news_home_title",
                "reallife_news_empty_rent", "reallife_news_empty_rent_body",
                "reallife_news_stand_sum", "reallife_news_stand_save", "reallife_news_stand_renew",
                "reallife_news_stand_sub", "reallife_news_stand_stop", "reallife_news_stand_on",
                "reallife_news_stand_off", "reallife_news_stand_off_body", "reallife_news_stand_out", "reallife_news_stand_out_body",
                "reallife_ledger_online_name_ledger", "reallife_ledger_online_name_exchange",
                "reallife_ledger_online_name_street", "reallife_ledger_online_name_review", "reallife_ledger_online_name_pass",
                "reallife_news_paper_quiet", "reallife_news_paper_quiet_body", "reallife_news_sec_city", "reallife_news_sec_business",
                "reallife_news_open_title", "reallife_news_open_body", "reallife_news_hold_body",
                "reallife_news_end_kicker", "reallife_news_end_body", "reallife_news_end_load", "reallife_news_end_quit",
                "reallife_news_end_days", "reallife_news_end_worth", "reallife_news_end_shops", "reallife_news_end_staff", "reallife_news_end_rank",
                "reallife_ledger_title"
            };
        }
    }
}
