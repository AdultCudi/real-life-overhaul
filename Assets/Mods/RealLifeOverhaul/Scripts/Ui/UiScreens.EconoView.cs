using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    public static partial class UiScreens
    {
        // The game's EconoView, drawn on the kit over the game's own (Scripts/Ui/EconoView).
        static partial void AddEconoView(List<UiScreenDef> screens)
        {
            screens.AddRange(new[]
            {
                new UiScreenDef("econoview", UiTemplate.AppWindow, "EconoViewScreen", new[]
                {
                    "reallife_ui_econo_a_loan", "reallife_ui_econo_added", "reallife_ui_econo_auto_amount", "reallife_ui_econo_auto_on", "reallife_ui_econo_basis",
                    "reallife_ui_econo_borrowed", "reallife_ui_econo_business", "reallife_ui_econo_change", "reallife_ui_econo_chart_wait", "reallife_ui_econo_count",
                    "reallife_ui_econo_deductible", "reallife_ui_econo_due", "reallife_ui_econo_earned", "reallife_ui_econo_empty_body", "reallife_ui_econo_empty_title",
                    "reallife_ui_econo_in", "reallife_ui_econo_in_none", "reallife_ui_econo_left", "reallife_ui_econo_loan", "reallife_ui_econo_money_in",
                    "reallife_ui_econo_money_out", "reallife_ui_econo_moves", "reallife_ui_econo_no_business", "reallife_ui_econo_no_business_body",
                    "reallife_ui_econo_no_funds", "reallife_ui_econo_no_loans", "reallife_ui_econo_no_moves", "reallife_ui_econo_no_payments",
                    "reallife_ui_econo_no_tax_paid", "reallife_ui_econo_none_body", "reallife_ui_econo_none_title", "reallife_ui_econo_off", "reallife_ui_econo_on",
                    "reallife_ui_econo_out", "reallife_ui_econo_out_none", "reallife_ui_econo_overview", "reallife_ui_econo_owed", "reallife_ui_econo_owed_by",
                    "reallife_ui_econo_paid_day", "reallife_ui_econo_paid_of", "reallife_ui_econo_paid_so_far", "reallife_ui_econo_payments",
                    "reallife_ui_econo_per_day", "reallife_ui_econo_profit_days", "reallife_ui_econo_prompt_tab", "reallife_ui_econo_rest",
                    "reallife_ui_econo_risk_high", "reallife_ui_econo_risk_low", "reallife_ui_econo_risk_medium", "reallife_ui_econo_taxed_none",
                    "reallife_ui_econo_taxed_on", "reallife_ui_econo_time", "reallife_ui_econo_what", "reallife_ui_econo_withdrawn", "reallife_ui_econo_worth",
                    "reallife_ui_econo_worth_by", "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select", "amountoption_all",
                    "amountoption_negative", "amountoption_positive", "ba:investmentfund_alliancestechnologya", "ba:investmentfund_franklinus",
                    "ba:investmentfund_hgchinabonds", "ba:transaction_headhunterreplacement_label", "ba:transaction_healthinsurance_label",
                    "ba:transaction_importdelivery_label", "ba:transaction_loanpayment_label", "ba:transaction_marketing_label", "ba:transaction_rent_label",
                    "ba:transaction_revenue_label", "ba:transaction_taxiride_label", "ba:transaction_wage_label", "common_amount", "common_balance", "common_close",
                    "common_day", "common_day_number", "common_investments", "common_real_estate", "common_setdestination", "contacts_taxes_tax_rate",
                    "econoview_back_taxes_due", "econoview_full_transactions_all_days", "econoview_full_transactions_all_types",
                    "econoview_full_transactions_export_to_csv", "econoview_full_transactions_title", "econoview_income_statement", "econoview_investments_autoinvest",
                    "econoview_investments_changetotal", "econoview_investments_current_value", "econoview_investments_initial_investment",
                    "econoview_investments_noinvestments", "econoview_investments_notenoughdata", "econoview_investments_withdraw",
                    "econoview_investments_withdraw_money", "econoview_last_transactions", "econoview_last_transactions_full_view_button",
                    "econoview_loan_additional_payment", "econoview_loan_make_payment_button", "econoview_loans", "econoview_loans_daily_interest_rate",
                    "econoview_loans_daily_payment", "econoview_loans_noloans", "econoview_row_businesses", "econoview_row_fees", "econoview_row_licensing_fees",
                    "econoview_row_loans", "econoview_row_marketing", "econoview_row_ongoing_expenses", "econoview_row_other_income", "econoview_row_parking_fees",
                    "econoview_row_private_residences", "econoview_row_profit", "econoview_row_rent", "econoview_row_resources", "econoview_row_salaries",
                    "econoview_row_salary_income", "econoview_row_sales", "econoview_row_theft", "econoview_row_total", "econoview_row_unassigned_staff_wages",
                    "econoview_taxes", "econoview_taxes_due_immediately", "econoview_taxes_irs_contact", "econoview_taxes_owed", "econoview_taxes_paid",
                    "econoview_taxes_projection", "econoview_total_tax_deductions", "econoview_yesterday_date", "open_in_bizman", "persona_cash", "smartphone_econoview"
                }).Switch(UiScreenSwitches.Apps),
            });
        }
    }
}
