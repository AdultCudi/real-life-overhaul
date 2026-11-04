using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // Property and the Car Showroom: every state of both apps over the street, the rents map over the city's own rent picture.
    // The mod's words in another language (Locales/<lang>.json), the English where a text has no translation: the longest texts show on a screen.
    private static UiWords ModWords(string language)
    {
        var words = new Dictionary<string, string>();
        DialogRead(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", language + ".json")), words);
        return (key, english) => key != null && words.TryGetValue(key, out string said) ? said : ModEnglish(key, english);
    }

    static partial void AddEstate(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.EstateStates.Length; i++)
        {
            string st = UiSamples.EstateStates[i];
            bool rents = st.StartsWith("rents", System.StringComparison.Ordinal);
            screens.Add(new Screen
            {
                Name = "estate_" + st,
                AppName = "Property",
                Icon = UiIcon.Home,
                Backdrop = rents ? "tint_city.jpg" : st.StartsWith("unit", System.StringComparison.Ordinal) || st == "building" ? "12.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => EstateFace.Lay(s, UiSamples.Estate(ModEnglish, st), f, d, k)
            });
        }
        for (int i = 0; i < UiSamples.DealerStates.Length; i++)
        {
            string st = UiSamples.DealerStates[i];
            screens.Add(new Screen
            {
                Name = "dealer_" + st,
                AppName = "Car Showroom",
                Icon = UiIcon.Car,
                Backdrop = st.StartsWith("auction", System.StringComparison.Ordinal) ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "08.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => EstateFace.Lay(s, UiSamples.Dealer(ModEnglish, st), f, d, k)
            });
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            UiWords words = ModWords(lang);
            string suffix = lang == "pt" ? "pt" : "zh";
            string[] estate = { "roll", "unit_late", "market" };
            string[] dealer = { "stock", "auction", "offers" };
            foreach (string st in estate)
            {
                string name = st;
                screens.Add(new Screen { Name = "estate_" + name + "_" + suffix, AppName = "Property", Icon = UiIcon.Home, Backdrop = "13.jpg", HoverCommand = UiAppWindow.NavCommand, HoverArg = 1,
                    Walk = Moves("DDR"), Lay = (sc, f, d, k) => EstateFace.Lay(sc, UiSamples.Estate(words, name), f, d, k) });
            }
            foreach (string st in dealer)
            {
                string name = st;
                screens.Add(new Screen { Name = "dealer_" + name + "_" + suffix, AppName = "Car Showroom", Icon = UiIcon.Car, Backdrop = "08.jpg", HoverCommand = UiAppWindow.NavCommand, HoverArg = 1,
                    Walk = Moves("DDR"), Lay = (sc, f, d, k) => EstateFace.Lay(sc, UiSamples.Dealer(words, name), f, d, k) });
            }
        }
    }
}
