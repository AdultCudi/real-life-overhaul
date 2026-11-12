using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Finance, Banks, Your bank and Utilities, each tab of a rich save and of a new one, over the street.
    static partial void AddMoney(List<Screen> screens)
    {
        AddFinance(screens);
        AddMoneyApp(screens, "banks", "Banks", UiSamples.MoneyBankStates, UiIcon.Bank, (t, st) => UiSamples.MoneyBanks(t, st));
        AddMoneyApp(screens, "own_bank", "Your bank", UiSamples.MoneyOwnStates, UiIcon.PiggyBank, (t, st) => UiSamples.MoneyOwn(t, st));
        AddUtilities(screens);
    }

    // The Finance screen's own face: every tab on a rich save and on day 5 with little history, each choice, over the street.
    private static void AddFinance(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.FinanceStates.Length; i++)
        {
            string st = UiSamples.FinanceStates[i];
            screens.Add(new Screen
            {
                Name = "finance_" + st,
                AppName = "Finance",
                Icon = UiIcon.Wallet,
                Backdrop = st.EndsWith("_new", System.StringComparison.Ordinal) ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => FinanceFace.Lay(s, UiSamples.FinanceFactsOf(English, st), UiSamples.FinanceViewOf(st), UiSamples.FinanceWordsOf(English), new FinanceLayout(), f, d, k)
            });
        }
    }

    // The Utilities screen's own face: every tab, the company in full, not founded and digging its first district, and each sheet over it.
    private static void AddUtilities(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.UtilitiesStates.Length; i++)
        {
            string st = UiSamples.UtilitiesStates[i];
            screens.Add(new Screen
            {
                Name = "utilities_" + st,
                AppName = "Utilities",
                Icon = UiIcon.Globe,
                Backdrop = st == "network" ? "13.jpg" : st.StartsWith("network", System.StringComparison.Ordinal) ? "11.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => UtilitiesFace.Lay(s, UiSamples.Utilities(English, st), f, d, k)
            });
        }
    }

    private static void AddMoneyApp(List<Screen> screens, string name, string title, string[] states, UiIcon icon, System.Func<UiWords, string, MoneyModel> make)
    {
        for (int i = 0; i < states.Length; i++)
        {
            string st = states[i];
            screens.Add(new Screen
            {
                Name = name + "_" + st,
                AppName = title,
                Icon = icon,
                Backdrop = i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => MoneyPages.Lay(s, make(English, st), f, d, k)
            });
        }
    }
}
