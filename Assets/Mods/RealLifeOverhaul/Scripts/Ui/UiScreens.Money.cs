using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // Finance, Banks, Your bank and Utilities. The money section is its own, because the apps section is already full.
        // The shared labels are a method, not a field: a static field of another part of this class is not set yet
        // when All is gathered (the parts run their initialisers in no fixed order).
        static partial void AddMoney(List<UiScreenDef> screens)
        {
            string[] shared = MoneyShared();
            screens.Add(new UiScreenDef("finance", UiTemplate.AppWindow, "FinanceScreen", MoneyKeys(shared,
                "reallife_econ_app", "reallife_econ_tab_overview", "reallife_econ_tab_credit", "reallife_econ_tab_borrow",
                "reallife_econ_tab_cash", "reallife_econ_tab_accounts", "reallife_rival_title", "reallife_money_nav_rivals",
                "reallife_fame_title"))
                .Switch(UiScreenSwitches.Money));
            screens.Add(new UiScreenDef("banks", UiTemplate.AppWindow, "BanksScreen", MoneyKeys(shared,
                "reallife_banks_app", "reallife_bank_tab_accounts", "reallife_bank_tab_loans", "reallife_bank_tab_card",
                "reallife_bank_tab_apply", "reallife_bank_tab_rating"))
                .Switch(UiScreenSwitches.Money));
            screens.Add(new UiScreenDef("own_bank", UiTemplate.AppWindow, "OwnBankScreen", MoneyKeys(shared,
                "reallife_ob_app", "reallife_ob_tab_bank", "reallife_ob_tab_savers", "reallife_ob_tab_lending",
                "reallife_ob_tab_fund", "reallife_branch_tab"))
                .Switch(UiScreenSwitches.Money));
            screens.Add(new UiScreenDef("utilities", UiTemplate.AppWindow, "UtilitiesScreen", MoneyKeys(shared,
                "reallife_util_app", "reallife_util_tab_accounts", "reallife_util_tab_internet", "reallife_util_tab_network"))
                .Switch(UiScreenSwitches.Money));
        }

        // Every label any of the four samples asks for, so a shared line is named on each screen that can show it.
        private static string[] MoneyKeys(string[] shared, params string[] own)
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

        private static string[] MoneyShared()
        {
            return new[]
            {
                "reallife_ui_prompt_move", "reallife_ui_prompt_select", "reallife_ui_prompt_back", "reallife_ui_prompt_choose",
                "reallife_money_take", "reallife_money_apply", "reallife_money_export", "reallife_money_follow",
                "reallife_money_economy", "reallife_money_leaves", "reallife_money_all", "reallife_money_adds",
                "reallife_money_feed", "reallife_money_feed_empty", "reallife_money_headroom", "reallife_money_cover",
                "reallife_money_cut", "reallife_money_campaign", "reallife_money_hold", "reallife_money_forecast",
                "reallife_money_expanding", "reallife_money_worth", "reallife_money_cash_empty", "reallife_money_brought",
                "reallife_money_coming", "reallife_money_where", "reallife_money_threshold", "reallife_money_since",
                "reallife_money_other_banks", "reallife_money_month", "reallife_money_fund_days", "reallife_money_biggest",
                "reallife_money_customers", "reallife_money_dig", "reallife_money_live", "reallife_money_plan",
                "reallife_money_empty_debts", "reallife_money_empty_loans", "reallife_money_col_when",
                "reallife_money_col_what", "reallife_money_col_in", "reallife_money_col_out", "reallife_money_col_balance",
                "reallife_money_you", "reallife_money_owed", "reallife_money_cash", "reallife_money_locate"
            };
        }
    }
}
