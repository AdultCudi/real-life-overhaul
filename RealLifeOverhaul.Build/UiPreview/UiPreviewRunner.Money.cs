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
        AddMoneyApp(screens, "utilities", "Utilities", UiSamples.MoneyUtilStates, UiIcon.Globe, (t, st) => UiSamples.MoneyUtilities(t, st));
    }

    private static void AddMoneyApp(List<Screen> screens, string name, string title, string[] states, UiIcon icon, System.Func<UiWords, string, MoneyModel> make)
    {
        for (int i = 0; i < states.Length; i++)
        {
            string st = states[i];
            bool night = name == "utilities" && st == "network";
            screens.Add(new Screen
            {
                Name = name + "_" + st,
                AppName = title,
                Icon = icon,
                Backdrop = night ? "11.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => MoneyPages.Lay(s, make(English, st), f, d, k)
            });
        }
    }
}
