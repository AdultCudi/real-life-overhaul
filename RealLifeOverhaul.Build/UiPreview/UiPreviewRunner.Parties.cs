using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Parties: every state over the street. The mouse rests on the second tab in the hover shot, the pad walks down the tabs and in.
    static partial void AddParties(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.PartiesStates.Length; i++)
        {
            string st = UiSamples.PartiesStates[i];
            screens.Add(new Screen
            {
                Name = "parties_" + st, AppName = "Parties", Icon = UiIcon.ForkKnife, Backdrop = i % 2 == 0 ? "13.jpg" : "02.jpg", HoverCommand = UiAppWindow.NavCommand, HoverArg = 1,
                Walk = Moves("DDR"), Lay = (s, f, d, k) => VentFace.Lay(s, UiSamples.Parties(ModEnglish, st), f, d, k)
            });
        }
    }
}
