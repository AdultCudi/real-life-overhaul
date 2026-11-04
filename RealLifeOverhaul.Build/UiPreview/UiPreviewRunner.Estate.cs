using System;
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

    // What the pointer rests on in a state's hover shot and how the pad walks in its path shot, read from the state's own sample: a choice of a decision, the first
    // button of a card, the info view's second icon, or the next section of a window. A decision's walk starts back on its first choice and goes down through
    // every choice to the way out, so the shot shows the pad reach each answer.
    private static void EstateTargets(EstateModel m, out int command, out int arg, out string walk)
    {
        command = UiAppWindow.NavCommand;
        arg = m.Tab == 1 ? 2 : 1;
        walk = "DDR";
        if (m.Shape == EstateShape.Off)
        {
            command = UiDraw.CloseCommand;   // a page that says the app is off has the window's close and nothing else under the pointer
            arg = 0;
        }
        else if (m.Shape == EstateShape.Rents)
        {
            command = UiInfoView.ViewCommand;
            arg = 1;
        }
        else if (m.Shape == EstateShape.Decide && m.Decision != null && m.Decision.Choices.Length > 0)
        {
            command = EstateFace.ActionCommand;
            arg = m.Decision.Choices[0].Run;
            walk = new string('U', Math.Max(0, m.Decision.Start)) + new string('D', m.Decision.Choices.Length);
        }
        else if (m.Card)
        {
            command = EstateFace.ActionCommand;
            UiActionModel[] acts = m.Auction != null && m.Auction.Right != null ? m.Auction.Right.Actions : m.Left.Length > 0 ? m.Left[0].Actions : new UiActionModel[0];
            arg = acts.Length > 0 ? acts[0].Arg : -1;
        }
    }

    // The pad's walk of a card: every target of it in reading order (EstateTour), where a page keeps the short walk down its sidebar and across.
    private static Vector2Int[] EstateWalk(EstateModel m, string walk)
    {
        if (!m.Card && m.Shape != EstateShape.Decide && m.Shape != EstateShape.Auction)
        {
            return Moves(walk);
        }
        var scene = new UiScene();
        EstateFace.Lay(scene, m, UiFrame.FullMenu(16f / 9f, "Property"), true, UiDefaultKeys.XboxPad);
        return Moves(EstateTour.Walk(scene, UiFocusRules.First(scene)));
    }

    static partial void AddEstate(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.EstateStates.Length; i++)
        {
            string st = UiSamples.EstateStates[i];
            bool rents = st.StartsWith("rents", System.StringComparison.Ordinal);
            EstateModel sample = UiSamples.Estate(ModEnglish, st);
            EstateTargets(sample, out int hoverCommand, out int hoverArg, out string walk);
            screens.Add(new Screen
            {
                Name = "estate_" + st,
                AppName = "Property",
                Icon = UiIcon.Home,
                Backdrop = rents ? "tint_city.jpg" : st.StartsWith("unit", System.StringComparison.Ordinal) || st == "building" ? "12.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = hoverCommand,
                HoverArg = hoverArg,
                Walk = EstateWalk(sample, walk),
                Lay = (s, f, d, k) => EstateFace.Lay(s, UiSamples.Estate(ModEnglish, st), f, d, k)
            });
        }
        for (int i = 0; i < UiSamples.DealerStates.Length; i++)
        {
            string st = UiSamples.DealerStates[i];
            EstateModel sample = UiSamples.Dealer(ModEnglish, st);
            EstateTargets(sample, out int hoverCommand, out int hoverArg, out string walk);
            screens.Add(new Screen
            {
                Name = "dealer_" + st,
                AppName = "Car Showroom",
                Icon = UiIcon.Car,
                Backdrop = st.StartsWith("auction", System.StringComparison.Ordinal) ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "08.jpg",
                HoverCommand = hoverCommand,
                HoverArg = hoverArg,
                Walk = EstateWalk(sample, walk),
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
