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
                "reallife_stk_next", "reallife_stk_later", "reallife_stk_bidder", "reallife_stk_rival", "reallife_mkt_col_shares",
                "reallife_lst_publish", "reallife_mkt_bond_buy", "reallife_mkt_bond_col_bond", "reallife_mkt_bond_filter_held",
                "reallife_mkt_bond_filter_offers", "reallife_mkt_bond_ladder", "reallife_mkt_bond_sell", "reallife_mkt_bot_add",
                "reallife_mkt_bot_col_capital", "reallife_mkt_bot_col_firm", "reallife_mkt_bot_col_profit", "reallife_mkt_bot_col_trades",
                "reallife_mkt_bot_dismiss", "reallife_mkt_bot_hire", "reallife_mkt_bot_sell_up", "reallife_mkt_bot_take", "reallife_mkt_cancel_order",
                "reallife_mkt_col_kind", "reallife_mkt_col_lasts", "reallife_mkt_col_side", "reallife_mkt_col_worth", "reallife_mkt_deposit",
                "reallife_mkt_drv_buy", "reallife_mkt_drv_close", "reallife_mkt_drv_col_kind", "reallife_mkt_drv_col_name", "reallife_mkt_drv_col_next",
                "reallife_mkt_filter_open", "reallife_mkt_open_company", "reallife_stk_about_dip", "reallife_stk_about_div", "reallife_stk_about_earn",
                "reallife_stk_about_gap", "reallife_stk_about_grid", "reallife_stk_about_rebalance", "reallife_stk_about_rider", "reallife_stk_about_stop",
                "reallife_stk_add_rule", "reallife_stk_bonds_offer_empty", "reallife_stk_bonds_offer_empty_body", "reallife_stk_bots_empty",
                "reallife_stk_bots_empty_body", "reallife_stk_fut_offer_empty", "reallife_stk_limit", "reallife_stk_search_clear",
                "reallife_stk_search_count", "reallife_stk_search_none", "reallife_stk_search_none_body", "reallife_stk_sh_account",
                "reallife_stk_sh_add_rule", "reallife_stk_sh_amount", "reallife_stk_sh_bank_rate", "reallife_stk_sh_bid", "reallife_stk_sh_bond",
                "reallife_stk_sh_bonds_contracts", "reallife_stk_sh_budget", "reallife_stk_sh_buy_bond", "reallife_stk_sh_capital_least",
                "reallife_stk_sh_cash_left", "reallife_stk_sh_change", "reallife_stk_sh_company", "reallife_stk_sh_contract", "reallife_stk_sh_cost",
                "reallife_stk_sh_costs", "reallife_stk_sh_days", "reallife_stk_sh_deal", "reallife_stk_sh_dividend", "reallife_stk_sh_each",
                "reallife_stk_sh_each_of", "reallife_stk_sh_end_price", "reallife_stk_sh_ended", "reallife_stk_sh_ends", "reallife_stk_sh_equity",
                "reallife_stk_sh_every", "reallife_stk_sh_face", "reallife_stk_sh_fee", "reallife_stk_sh_fee_least", "reallife_stk_sh_fee_month",
                "reallife_stk_sh_filled", "reallife_stk_sh_firm", "reallife_stk_sh_firms", "reallife_stk_sh_float", "reallife_stk_sh_gain_target",
                "reallife_stk_sh_gap_of", "reallife_stk_sh_keep", "reallife_stk_sh_least", "reallife_stk_sh_level", "reallife_stk_sh_loss",
                "reallife_stk_sh_made", "reallife_stk_sh_maintenance", "reallife_stk_sh_margin", "reallife_stk_sh_mark", "reallife_stk_sh_max_open",
                "reallife_stk_sh_most", "reallife_stk_sh_name", "reallife_stk_sh_new_rule", "reallife_stk_sh_next", "reallife_stk_sh_off",
                "reallife_stk_sh_offered", "reallife_stk_sh_on", "reallife_stk_sh_only_open", "reallife_stk_sh_open", "reallife_stk_sh_open_contract",
                "reallife_stk_sh_open_option", "reallife_stk_sh_open_trades", "reallife_stk_sh_order", "reallife_stk_sh_order_type", "reallife_stk_sh_paid",
                "reallife_stk_sh_paid_out", "reallife_stk_sh_pause", "reallife_stk_sh_pay", "reallife_stk_sh_pay_policy", "reallife_stk_sh_payout",
                "reallife_stk_sh_pays", "reallife_stk_sh_per_trade", "reallife_stk_sh_place", "reallife_stk_sh_placed", "reallife_stk_sh_premium",
                "reallife_stk_sh_profit_share", "reallife_stk_sh_putback", "reallife_stk_sh_qty", "reallife_stk_sh_quote", "reallife_stk_sh_raise",
                "reallife_stk_sh_range", "reallife_stk_sh_received", "reallife_stk_sh_reinvest", "reallife_stk_sh_remove", "reallife_stk_sh_requirements",
                "reallife_stk_sh_reserve", "reallife_stk_sh_resume", "reallife_stk_sh_rival_rule", "reallife_stk_sh_roll", "reallife_stk_sh_rsi_under",
                "reallife_stk_sh_rule", "reallife_stk_sh_rules", "reallife_stk_sh_rules_full", "reallife_stk_sh_save", "reallife_stk_sh_scope",
                "reallife_stk_sh_settles", "reallife_stk_sh_size", "reallife_stk_sh_stake", "reallife_stk_sh_stop", "reallife_stk_sh_stop_loss",
                "reallife_stk_sh_strike", "reallife_stk_sh_strike_price", "reallife_stk_sh_tax", "reallife_stk_sh_tell_me", "reallife_stk_sh_term",
                "reallife_stk_sh_this_week", "reallife_stk_sh_total", "reallife_stk_sh_trade_it", "reallife_stk_sh_units", "reallife_stk_sh_value",
                "reallife_stk_sh_walk", "reallife_stk_sh_week", "reallife_stk_sh_what", "reallife_stk_sh_withdrawable", "reallife_stk_sh_you_hold",
                "reallife_stk_sh_your_contracts"
            }).Switch(UiScreenSwitches.Market));
        }
    }
}
