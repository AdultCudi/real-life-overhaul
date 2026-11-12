using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Build mode on the kit over the open city: every state over the street, and the longest languages beside the English (Scripts/Ui/BuildMode).
    private static void AddBuildModeScreens(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.BuildModeStates.Length; i++)
        {
            string st = UiSamples.BuildModeStates[i];
            screens.Add(BuildModeScreenOf(st, "en", i % 2 == 0 ? "01.jpg" : "02.jpg"));
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            foreach (string st in new[] { "land", "zone", "catalogue_long", "rents" })
            {
                screens.Add(BuildModeScreenOf(st, lang, "02.jpg"));
            }
        }
    }

    private static Screen BuildModeScreenOf(string state, string lang, string backdrop)
    {
        UiWords t = lang == "en" ? ModEnglish : ModWords(lang);
        bool catalogue = state.StartsWith("catalogue", System.StringComparison.Ordinal);
        return new Screen
        {
            Name = "build_mode_" + state + (lang == "en" ? string.Empty : lang == "pt" ? "_pt" : "_zh"), AppName = "Build mode", Icon = UiIcon.Building, Backdrop = backdrop, NoKeys = true,
            HoverCommand = catalogue ? UiBuildPanels.CardCommand : UiBuildPanels.ViewCommand, HoverArg = catalogue ? 1 : 2,
            Lay = (s, f, d, k) => UiSamples.BuildModeScene(s, f, d, k, t, state)
        };
    }
}
