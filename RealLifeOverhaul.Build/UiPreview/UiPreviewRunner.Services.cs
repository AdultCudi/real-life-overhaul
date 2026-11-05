using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The Services app: every state of its four tabs over the street, and the sale in pt and zh. The window's section buttons are what the pointer rests on.
    private static void ServicesTargets(ServicesModel m, out int command, out int arg)
    {
        command = UiAppWindow.NavCommand;
        arg = m.Tab == 3 ? 2 : 3;
        if (m.Shape == ServicesShape.Off)
        {
            command = UiDraw.CloseCommand;   // a part that is off has the window's close and the one button under the pointer
            arg = 0;
        }
    }

    static partial void AddServices(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.ServicesStates.Length; i++)
        {
            string st = UiSamples.ServicesStates[i];
            ServicesModel sample = UiSamples.Services(ModEnglish, st);
            ServicesTargets(sample, out int hoverCommand, out int hoverArg);
            screens.Add(new Screen
            {
                Name = "services_" + st,
                AppName = "Services",
                Icon = UiIcon.Handshake,
                Backdrop = i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = hoverCommand,
                HoverArg = hoverArg,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => ServicesFace.Lay(s, UiSamples.Services(ModEnglish, st), f, d, k)
            });
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = ModWords(lang);
            string suffix = lang == "pt" ? "pt" : "zh";
            foreach (string st in new[] { "cleaning", "contracts_offer", "sell", "sell_rivals" })
            {
                string name = st;
                screens.Add(new Screen { Name = "services_" + name + "_" + suffix, AppName = "Services", Icon = UiIcon.Handshake, Backdrop = "13.jpg", HoverCommand = UiAppWindow.NavCommand,
                    HoverArg = 1, Walk = Moves("DDR"), Lay = (sc, f, d, k) => ServicesFace.Lay(sc, UiSamples.Services(words, name), f, d, k) });
            }
        }
    }
}
