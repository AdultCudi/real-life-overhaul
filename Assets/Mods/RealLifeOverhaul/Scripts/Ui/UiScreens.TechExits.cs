using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The Tech app's pages of exits (Tech/Venture/ExitRules): the offers for a firm, its shares sold later, the way to the market, and a city startup's founder and office.
        private static string[] TechExitKeys() => new[]
        {
            "reallife_ui_tech_exit_offers", "reallife_ui_tech_exit_shares", "reallife_ui_tech_exit_market", "reallife_ui_tech_exit_place", "reallife_ui_tech_exit_counter",
            "reallife_ui_tech_exit_bank", "reallife_ui_tech_exit_road", "reallife_exit_button_page", "reallife_exit_title", "reallife_exit_off", "reallife_exit_offer_none",
            "reallife_exit_offer_line", "reallife_exit_hire_line", "reallife_exit_offer_after", "reallife_exit_offer_free", "reallife_exit_offer_gap", "reallife_exit_sold_line",
            "reallife_exit_button_accept", "reallife_exit_button_decline", "reallife_exit_button_counter", "reallife_exit_counter_0", "reallife_exit_counter_1", "reallife_exit_counter_2",
            "reallife_exit_shares_room", "reallife_exit_shares_none", "reallife_exit_shares_locked", "reallife_exit_button_sell", "reallife_exit_market_not_ready",
            "reallife_exit_market_start", "reallife_exit_bank_line", "reallife_exit_bank_default", "reallife_exit_button_bankers", "reallife_exit_market_bankers", "reallife_exit_road_0",
            "reallife_exit_road_1", "reallife_exit_road_2", "reallife_exit_button_road", "reallife_exit_button_withdraw", "reallife_exit_market_wait", "reallife_exit_market_listed",
            "reallife_exit_market_free", "reallife_exit_market_called", "reallife_exit_button_whole", "reallife_exit_holder_public", "reallife_exit_founder_city",
            "reallife_exit_founder_invented", "reallife_exit_team", "reallife_exit_office", "reallife_exit_office_none", "common_setdestination"
        };

        private static string[] Plus(string[] keys, string[] more)
        {
            var all = new List<string>(keys.Length + more.Length);
            all.AddRange(keys);
            all.AddRange(more);
            return all.ToArray();
        }
    }
}
