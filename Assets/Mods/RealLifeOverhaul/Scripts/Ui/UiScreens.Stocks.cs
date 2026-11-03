using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The Stocks app on the kit (Scripts/Ui/Stocks). The market's own page is the way back.
        static partial void AddStocks(List<UiScreenDef> screens)
        {
            screens.Add(new UiScreenDef("stocks", UiTemplate.AppWindow, "StocksScreen", new[]
            {
                "reallife_mkt_app", "reallife_mkt_tab_market", "reallife_mkt_tab_companies", "reallife_mkt_tab_watch", "reallife_mkt_tab_portfolio",
                "reallife_mkt_tab_orders", "reallife_mkt_tab_history", "reallife_mkt_tab_bots", "reallife_stk_nav_listing", "reallife_mkt_tab_bonds",
                "reallife_mkt_tab_futures", "reallife_stk_group_markets", "reallife_stk_group_yours", "reallife_stk_group_other", "reallife_ui_prompt_back",
                "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_stk_watch_empty", "reallife_stk_watch_empty_body", "reallife_stk_port_empty",
                "reallife_stk_port_empty_body", "reallife_stk_bonds_empty", "reallife_stk_bonds_empty_body", "reallife_stk_fut_empty", "reallife_stk_fut_empty_body",
                "reallife_mkt_col_index", "reallife_stk_small", "reallife_stk_all", "reallife_stk_today", "reallife_stk_tape", "reallife_stk_fund", "reallife_stk_you",
                "reallife_mkt_buy", "reallife_mkt_sell", "reallife_mkt_col_company", "reallife_mkt_col_price", "reallife_mkt_col_today", "reallife_mkt_col_volume",
                "reallife_stk_filter_all", "reallife_stk_sort_movers", "reallife_stk_search", "reallife_mkt_sector_groceries", "reallife_mkt_col_pe",
                "reallife_mkt_col_yield", "reallife_stk_open", "reallife_stk_high", "reallife_stk_low", "reallife_stk_read", "reallife_mkt_overlay_sma20",
                "reallife_stk_rsi", "reallife_stk_add_indicator", "reallife_stk_clear", "reallife_stk_sig_sma_up", "reallife_stk_sig_rsi_mid",
                "reallife_stk_market_order", "reallife_stk_day", "reallife_stk_max", "reallife_stk_cash_after", "reallife_stk_position_after",
                "reallife_stk_place", "reallife_stk_sell_instead", "reallife_stk_alert", "reallife_stk_worth", "reallife_mkt_sector_restaurants",
                "reallife_mkt_sector_leisure", "reallife_stk_cash", "reallife_mkt_col_day", "reallife_mkt_col_what", "reallife_stk_in", "reallife_stk_out",
                "reallife_stk_after", "reallife_stk_budget", "reallife_stk_day_result", "reallife_mkt_col_state", "reallife_stk_running", "reallife_stk_waiting",
                "reallife_mkt_bot_strategy_trend", "reallife_stk_rule", "reallife_stk_now", "reallife_stk_policy", "reallife_stk_stages", "reallife_lst_stage_advised",
                "reallife_lst_stage_auditing", "reallife_lst_stage_building", "reallife_lst_stage_priced", "reallife_lst_stage_listed", "reallife_stk_done",
                "reallife_stk_next", "reallife_stk_later", "reallife_stk_bidder", "reallife_stk_rival", "reallife_mkt_col_shares"
            }).Switch(UiScreenSwitches.Market));
        }
    }
}
