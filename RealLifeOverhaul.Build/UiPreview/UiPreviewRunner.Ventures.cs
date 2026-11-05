using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // Ventures: every state over the street, the investors' page at its busiest first. The mouse rests on the second tab in the hover shot, the pad walks down the tabs and in.
    static partial void AddVentures(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.VenturesStates.Length; i++)
        {
            string st = UiSamples.VenturesStates[i];
            screens.Add(new Screen
            {
                Name = "ventures_" + st,
                AppName = "Ventures",
                Icon = UiIcon.Coin,
                Backdrop = st.StartsWith("raise", StringComparison.Ordinal) || st.StartsWith("property", StringComparison.Ordinal) ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => VentFace.Lay(s, UiSamples.Ventures(ModEnglish, st), f, d, k)
            });
        }
        for (int i = 0; i < UiSamples.BrandsStates.Length; i++)
        {
            string st = UiSamples.BrandsStates[i];
            screens.Add(new Screen
            {
                Name = "brands_" + st,
                AppName = "Brands",
                Icon = UiIcon.Tag,
                Backdrop = i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => VentFace.Lay(s, UiSamples.Brands(ModEnglish, st), f, d, k)
            });
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = ModWords(lang);
            string suffix = lang == "pt" ? "pt" : "zh";
            foreach (string st in new[] { "brand", "shops", "applicant", "agreement_rebate" })
            {
                string name = st;
                screens.Add(new Screen
                {
                    Name = "brands_" + name + "_" + suffix, AppName = "Brands", Icon = UiIcon.Tag, Backdrop = "13.jpg", HoverCommand = UiAppWindow.NavCommand, HoverArg = 1,
                    Walk = Moves("DDR"), Lay = (sc, f, d, k) => VentFace.Lay(sc, UiSamples.Brands(words, name), f, d, k)
                });
            }
            foreach (string st in new[] { "investors", "offer", "deal", "raise_who" })
            {
                string name = st;
                screens.Add(new Screen
                {
                    Name = "ventures_" + name + "_" + suffix, AppName = "Ventures", Icon = UiIcon.Coin, Backdrop = "13.jpg", HoverCommand = UiAppWindow.NavCommand, HoverArg = 1,
                    Walk = Moves("DDR"), Lay = (sc, f, d, k) => VentFace.Lay(sc, UiSamples.Ventures(words, name), f, d, k)
                });
            }
        }
    }
}
