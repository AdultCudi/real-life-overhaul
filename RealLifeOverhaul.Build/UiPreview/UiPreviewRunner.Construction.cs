using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The Construction app: every state over the street, and the longest languages' pages beside the English.
    static partial void AddConstruction(List<Screen> screens)
    {
        AddBuildModeScreens(screens);
        for (int i = 0; i < UiSamples.ConstructionStates.Length; i++)
        {
            string st = UiSamples.ConstructionStates[i];
            ConstructionModel sample = UiSamples.Construction(ModEnglish, st);
            screens.Add(new Screen
            {
                Name = "construction_" + st,
                AppName = "Construction",
                Icon = UiIcon.Building,
                Backdrop = i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = sample.Shape == ConstructionShape.Off ? RealLifeOverhaul.Ui.UiDraw.CloseCommand : UiAppWindow.NavCommand,
                HoverArg = sample.Shape == ConstructionShape.Off ? 0 : 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => ConstructionFace.Lay(s, UiSamples.Construction(ModEnglish, st), f, d, k)
            });
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = ModWords(lang);
            string suffix = lang == "pt" ? "pt" : "zh";
            foreach (string st in new[] { "overview", "land", "project_ready", "tender_bid" })
            {
                string name = st;
                screens.Add(new Screen
                {
                    Name = "construction_" + name + "_" + suffix, AppName = "Construction", Icon = UiIcon.Building, Backdrop = "13.jpg", HoverCommand = UiAppWindow.NavCommand, HoverArg = 1,
                    Walk = Moves("DDR"), Lay = (sc, f, d, k) => ConstructionFace.Lay(sc, UiSamples.Construction(words, name), f, d, k)
                });
            }
        }
    }
}
