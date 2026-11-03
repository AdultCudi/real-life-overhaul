using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Finance, Banks, Your bank and Utilities, each tab of a rich save and of a new one, over the street.
    static partial void AddMoney(List<Screen> screens)
    {
        AddMoneyApp(screens, "finance", "Finance", UiSamples.MoneyFinanceStates, UiIcon.Wallet, (t, st) => UiSamples.MoneyFinance(t, st));
        AddMoneyApp(screens, "banks", "Banks", UiSamples.MoneyBankStates, UiIcon.Bank, (t, st) => UiSamples.MoneyBanks(t, st));
        AddMoneyApp(screens, "own_bank", "Your bank", UiSamples.MoneyOwnStates, UiIcon.PiggyBank, (t, st) => UiSamples.MoneyOwn(t, st));
        AddUtilities(screens);
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
