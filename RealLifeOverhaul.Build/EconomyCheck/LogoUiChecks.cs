using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The logo editor of BizMan's settings on the kit (--logo-ui): "Shape and colours" opens the kit's own page, every shape and colour
// pick presses the game's own entry button so the customizer's handlers and its Save logo button run, Save logo still presses the
// game's button, and no path of the logo shows the game's own BizMan. The laid page is checked in code; the game side is read from
// the sources, as it is game UI the checker cannot run. It fails on the code before this page, which sent the button to
// BizManKit.Classic.
internal static partial class Program
{
    private static void LogoUiAll()
    {
        string press = ModSource(@"Ui\BizMan\BizManScreen.Press.cs");
        string logo = ModSource(@"Ui\BizMan\BizManScreen.Logo.cs");
        string parts = ModSource(@"Ui\BizMan\BizManScreen.Parts.cs");

        // The button opens the kit's page and never the game's view.
        string edit = Lines(press, "case BizManPages.DoLogoEdit:", 1);
        Check(edit.Contains("return OpenLogoEditor(b);") && !press.Contains("DoLogoClassic") && !parts.Contains("DoLogoClassic"),
            "logo ui: Shape and colours opens the kit's editor (OpenLogoEditor), not the game's own view");
        Check(MemberText(logo, "private bool OpenLogoEditor(").Contains("_logoEdit = true;") && !logo.Contains("Classic("),
            "logo ui: the editor is a state of the kit's settings page and calls nothing of the classic hand over");

        // The only hand over to the game's view is for pages the kit does not draw; the settings page is drawn in both switch states.
        int classics = 0;
        for (int at = press.IndexOf("BizManKit.Classic()", StringComparison.Ordinal); at >= 0; at = press.IndexOf("BizManKit.Classic()", at + 1, StringComparison.Ordinal))
        {
            classics++;
        }
        Check(classics == 1 && MemberText(press, "private bool ToClassic(").Contains("BizManKit.Classic()") && BizManRules.Drawn("Settings", false) && BizManRules.Drawn("Settings", true)
              && BizManRules.PartOf("Settings", false) == BizManPart.Settings && BizManRules.PartOf("Settings", true) == BizManPart.Settings,
            "logo ui: no path from the settings or the logo reaches the game's page (the one hand over is for pages the kit does not draw)");

        // Every pick reaches the game's own call: the entry's button, so the list's handler, the customizer's and its save button run.
        Check(MemberText(logo, "private bool PressShape(").Contains("Click(_shapeButtons[index]") && MemberText(logo, "private bool PressColour(").Contains("Click(_colourButtons[row][index]")
              && MemberText(logo, "private bool PressAddColour(").Contains("Click(button") && MemberText(logo, "private bool PressAddColour(").Contains("list.AddNewColor()"),
            "logo ui: a shape, a colour and the add button each press the game's own button (or its AddNewColor), never set a value of their own");
        Check(logo.Contains("GameContract.LogoShapesPlayer") && logo.Contains("GameContract.LogoShapesBuiltIn") && logo.Contains("GameContract.LogoShapeColours")
              && logo.Contains("GameContract.LogoFontColours") && logo.Contains("GameContract.LogoBackColours"),
            "logo ui: both lists of shapes and the three lists of colours are the game's own");
        Check(press.Contains("case BizManPages.LogoShape: return PressShape(arg);") && press.Contains("case BizManPages.LogoColourAdd: return PressAddColour(arg);")
              && press.Contains("return PressColour(command - BizManPages.LogoColour, arg);") && press.Contains("case BizManPages.LogoOwn: return PressOwnShapes(b);"),
            "logo ui: every command of the editor is answered");

        // Save logo is still the game's save button.
        Check(press.Contains("case BizManPages.DoSaveLogo: return Click(Get<Button>(GameContract.LogoSave, Get<LogoCustomizer>(GameContract.SetLogo, b.bizManSettings)), \"save the logo\");")
              && logo.Contains("Do(BizManPages.DoSaveLogo"),
            "logo ui: Save logo presses the game's own save button, from the settings page and from the editor");

        // The page laid, tab by tab: the shapes in a scrolling grid window with the link to the player's own, then three colour rows each with
        // its add button; Save logo and Done on both; every target a known command and no more than the screen's 48.
        var known = new HashSet<int>(BizManPages.Commands);
        int shapes = 0, adds = 0, rows = 0, own = 0, tabs = 0, unknown = 0, most = 0;
        foreach (string state in new[] { "store_logo", "store_logo_colours" })
        {
            BizManModel m = UiSamples.BizMan(UiWords("en"), state);
            var s = new UiScene();
            BizManPages.Lay(s, m, UiFrame.FullMenu(16f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);
            int save = 0, done = 0;
            for (int i = 0; i < s.NodeCount; i++)
            {
                int c = s.Nodes[i].Command;
                shapes += c == BizManPages.LogoShape ? 1 : 0;
                adds += c == BizManPages.LogoColourAdd ? 1 : 0;
                rows += c >= BizManPages.LogoColour && c <= BizManPages.LogoColour + 2 ? 1 : 0;
                own += c == BizManPages.LogoOwn ? 1 : 0;
                tabs += c == BizManPages.LogoTab ? 1 : 0;
                save += c == BizManPages.Action && s.Nodes[i].Arg == BizManPages.DoSaveLogo ? 1 : 0;
                done += c == BizManPages.Action && s.Nodes[i].Arg == BizManPages.DoLogoDone ? 1 : 0;
                unknown += known.Contains(c) || c == UiAppWindow.NavCommand || c == UiDraw.CloseCommand ? 0 : 1;
            }
            most = Math.Max(most, s.NodeCount);
            Check(save == 1 && done == 1, "logo ui: " + state + " has Save logo and Done once each");
        }
        Check(shapes >= 8 && shapes <= 30 && own == 1 && adds == 3 && rows == 24 && tabs == 4 && unknown == 0,
            "logo ui: the editor lays its shapes in a scrolling grid window and three colour rows with their add buttons, every target a known command (" + shapes + " shapes, " + rows + " swatches, " + unknown + " unknown)");
        BizManModel colours = UiSamples.BizMan(UiWords("en"), "store_logo_colours");
        var cs = new UiScene();
        BizManPages.Lay(cs, colours, UiFrame.FullMenu(16f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);
        Check(cs.FindNode(BizManPages.LogoColour, 0) >= 0 && cs.FindNode(BizManPages.LogoColour + 1, 7) >= 0 && cs.FindNode(BizManPages.LogoColour + 2, 0) >= 0 && cs.FindNode(BizManPages.LogoColourAdd, 2) >= 0,
            "logo ui: each colour row and its add button can be pressed");

        // The settings page offers the button and stays the settings page until it is pressed.
        BizManModel plain = UiSamples.BizMan(UiWords("en"), "store_settings");
        Check(!plain.LogoEdit && Array.Exists(plain.Actions, a => a.Arg == BizManPages.DoLogoEdit), "logo ui: the settings page offers Shape and colours and stays the settings page");

        // Back from the editor lands on the settings, and a change of section closes it.
        Check(MemberText(press, "private bool CloseLists()").Contains("_logoEdit = false"), "logo ui: Back and a change of section close the editor");

        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            bool ok = words != null;
            foreach (string key in new[] { "shapes", "shapes_none", "note", "own", "done", "colour_shape", "colour_text", "colour_back" })
            {
                string v = null;
                ok = ok && words.TryGetValue("reallife_ui_bizman_logo_" + key, out v) && v.Length > 0 && v.Length <= 240 && !v.Contains('!') && !v.Contains((char)0x2014) && !v.Contains((char)0x2013);
            }
            Check(ok, "logo ui: the editor's texts in " + code);
        }
        foreach (string code in new[] { "en", "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            string next = WhatsNewText(words, WhatsNewKey(LoadWords(System.IO.Path.Combine(DesignLocales, "en.json")), "BizMan's logo editor now has its own shape and colour page"));   // the row that holds it
            Check(words != null && next.Contains("BizMan"), "logo ui: What's new in " + code);
        }
    }
}
