using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The pause menu's blueprints window and the game's help window, every state their samples hold, over the day street (13) and the
    // street indoors (02); the longest languages beside the English. A blueprint's thumbnail is one of the game's own pictures (the
    // backdrops), as the game shows one it has loaded.
    static partial void AddBlueprints(List<Screen> screens)
    {
        UiPictures.Answer(BlueprintThumb);
        string[] blueprintBack = { "13.jpg", "02.jpg", "13.jpg", "02.jpg", "13.jpg", "02.jpg", "13.jpg", "02.jpg" };
        for (int i = 0; i < UiSamples.BlueprintStates.Length; i++)
        {
            string st = UiSamples.BlueprintStates[i];
            screens.Add(BlueprintShot("blueprints_" + st, blueprintBack[i], English, st));
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            foreach (string st in new[] { "library", "workshop", "filters" })
            {
                screens.Add(BlueprintShot("blueprints_" + st + (lang == "pt" ? "_pt" : "_zh"), "13.jpg", ModWords(lang), st));
            }
        }
        string[] helpBack = { "13.jpg", "02.jpg", "13.jpg", "02.jpg", "13.jpg" };
        for (int i = 0; i < UiSamples.HelpStates.Length; i++)
        {
            string st = UiSamples.HelpStates[i];
            screens.Add(HelpShot("help_" + st, helpBack[i], English, st));
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            screens.Add(HelpShot("help_page" + (lang == "pt" ? "_pt" : "_zh"), "13.jpg", ModWords(lang), "page"));
        }
    }

    private static Screen BlueprintShot(string name, string backdrop, UiWords t, string state)
    {
        Screen screen = new Screen
        {
            Name = name, AppName = "Blueprints", Icon = UiIcon.Layout, Backdrop = backdrop, Bar = () => UiSamples.ShellBar(0, false),
            Lay = (s, f, d, k) => UiBlueprints.Lay(s, UiSamples.Blueprints(t, state), f, d, k),
            HoverCommand = UiBlueprints.CardCommand, HoverArg = 2, FocusCommand = UiBlueprints.CardCommand, FocusArg = 1, Walk = Moves("RDR")
        };
        if (state == "sort")
        {
            screen.HoverCommand = UiBlueprints.SortOptionCommand;
            screen.HoverArg = 0;
            screen.FocusCommand = UiBlueprints.SortOptionCommand;
            screen.FocusArg = 2;
            screen.Walk = Moves("D");
        }
        else if (state == "filters")
        {
            screen.HoverCommand = UiBlueprints.FilterRowCommand;
            screen.HoverArg = 3;
            screen.FocusCommand = UiBlueprints.FilterRowCommand;
            screen.FocusArg = 2;
            screen.Walk = Moves("DD");
        }
        else if (state == "terms")
        {
            screen.HoverCommand = UiBlueprints.ConfirmCommand;
            screen.HoverArg = 1;
            screen.FocusCommand = UiBlueprints.ConfirmCommand;
            screen.FocusArg = 0;
            screen.Walk = Moves("L");
        }
        else if (state == "workshop_loading" || state == "workshop_offline")
        {
            screen.HoverCommand = UiAppWindow.NavCommand;
            screen.HoverArg = 0;
            screen.FocusCommand = UiAppWindow.NavCommand;
            screen.FocusArg = 1;
            screen.Walk = new Vector2Int[0];
        }
        return screen;
    }

    private static Screen HelpShot(string name, string backdrop, UiWords t, string state)
    {
        Screen screen = new Screen
        {
            Name = name, AppName = "Help", Icon = UiIcon.Info, Backdrop = backdrop, Bar = () => UiSamples.ShellBar(0, false),
            Lay = (s, f, d, k) => UiHelp.Lay(s, UiSamples.Help(t, state), f, d, k),
            HoverCommand = UiAppWindow.NavCommand, HoverArg = 3, FocusCommand = UiReader.AnchorCommand, FocusArg = 0, Walk = Moves("D")
        };
        return state == "search" || state == "none" ? Typing(screen, UiHelp.SearchCommand, 0) : screen;
    }

    private static readonly Dictionary<string, Sprite> BlueprintThumbs = new Dictionary<string, Sprite>(16);

    // "bpthumb:NN": the game's own picture NN of the backdrops' folder, as a blueprint's thumbnail. Null for any other key.
    private static Sprite BlueprintThumb(string key)
    {
        if (key == null || !key.StartsWith("bpthumb:", StringComparison.Ordinal))
        {
            return null;
        }
        if (BlueprintThumbs.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        Sprite sprite = null;
        string path = string.IsNullOrEmpty(_backdrops) ? null : Path.Combine(_backdrops, key.Substring(8) + ".jpg");
        if (path != null && File.Exists(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(File.ReadAllBytes(path));
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        BlueprintThumbs[key] = sprite;
        return sprite;
    }
}
