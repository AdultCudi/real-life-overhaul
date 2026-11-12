using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    // The Banks and Your bank screens' states live in UiSamples.Bank.cs; this part holds the small helpers the money and stocks samples share.
    private static string W(UiWords t, string key, string english) => t(key, english);

    private static UiPrompt[] Prompts(UiWords t, bool act)
    {
        var list = new UiPrompt[act ? 4 : 3];
        list[0] = new UiPrompt { Action = UiAction.Move, Label = W(t, "reallife_ui_prompt_move", "Move") };
        list[1] = new UiPrompt { Action = UiAction.Confirm, Label = W(t, "reallife_ui_prompt_select", "Select") };
        list[2] = new UiPrompt { Action = UiAction.Back, Label = W(t, "reallife_ui_prompt_back", "Back") };
        if (act)
        {
            list[3] = new UiPrompt { Action = UiAction.Alt, Label = W(t, "reallife_ui_prompt_choose", "Choose") };
        }
        return list;
    }

    private static UiNavEntry Nav(UiWords t, string key, string english, UiIcon icon)
    {
        return new UiNavEntry { Icon = icon, Tint = UiRole.None, Label = W(t, key, english) };
    }

    private static UiStatementModel Statement(UiWords t, bool fresh, string account)
    {
        var m = new UiStatementModel
        {
            Account = account,
            AccountIcon = UiIcon.Wallet,
            BalanceLabel = W(t, "reallife_money_cash", "Cash"),
            Balance = fresh ? "$12,400" : "$842,110",
            PeriodLabel = "30 days",
            Columns = new[]
            {
                W(t, "reallife_money_col_when", "When"), W(t, "reallife_money_col_what", "What"), W(t, "reallife_money_col_in", "In"),
                W(t, "reallife_money_col_out", "Out"), W(t, "reallife_money_col_balance", "Balance")
            },
            EmptyTitle = W(t, "reallife_money_cash_empty", "No movements yet"),
            EmptyBody = W(t, "reallife_money_coming", "Coming up: rent on day 12, wages on day 14.")
        };
        if (fresh)
        {
            return m;
        }
        m.Groups = new[]
        {
            new UiStatementGroup
            {
                Label = W(t, "reallife_money_brought", "Brought forward"),
                Total = "$860,000",
                TotalGood = true,
                Lines = new[]
                {
                    new UiStatementLine { When = "09:10", Icon = UiIcon.Store, What = "Takings", Where = "A shop of yours", In = "$18,400", Balance = "$878,400" },
                    new UiStatementLine { When = "18:00", Icon = UiIcon.People, What = "Wages", Where = "Staff", Out = "$6,200", Balance = "$872,200" }
                }
            }
        };
        m.TotalLabels = new[] { "In", "Out", "Change" };
        m.Totals = new[] { "$18,400", "$6,200", "$12,200" };
        m.ChangeGood = true;
        return m;
    }
}
