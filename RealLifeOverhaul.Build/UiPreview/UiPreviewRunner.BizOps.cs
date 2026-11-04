using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // BizMan's operations and the uniform presets' editor in every state their samples hold, BizMan open in the status
    // bar's app row: a store's schedule and a headquarters' managers over the day streets the full menu leaves (13 and 02),
    // the factory over the factory, the warehouse over the warehouse. The last three are in Portuguese, the longest of the
    // languages with every key of these pages, the game's words from its own file.
    static partial void AddBizOps(List<Screen> screens)
    {
        Func<string, Sprite> before = UiPictures.Find;
        UiPictures.Find = key => key != null && key.StartsWith(OpsSwatch, StringComparison.Ordinal) ? OpsColour(key) : before?.Invoke(key);
        int n = 0;
        foreach (string state in UiSamples.BizOpsStates)
        {
            string st = state;
            screens.Add(OpsScreen(++n, st, st, English));
        }
        foreach (string state in UiSamples.UniformStates)
        {
            string st = state;
            int at = ++n;
            screens.Add(st == "uniforms_colours"
                ? UniformsScreen(at, st, st, English, UiUniforms.Colour, 1, UiUniforms.Colour, 5, "RRDLLU")
                : UniformsScreen(at, st, st, English));
        }
        screens.Add(OpsScreen(++n, "schedule_long", "schedule_long_portuguese", OpsPortuguese));
        screens.Add(OpsScreen(++n, "pricing", "pricing_portuguese", OpsPortuguese));
        screens.Add(UniformsScreen(++n, "uniforms_long", "uniforms_long_portuguese", OpsPortuguese));
    }

    private static Screen OpsScreen(int n, string state, string name, UiWords words)
    {
        var screen = new Screen
        {
            Name = "bizops_" + n.ToString("00") + "_" + name, AppName = "BizMan", Icon = UiIcon.Building, Backdrop = OpsBackdrop(state, n),
            Bar = () => UiSamples.ShellBar(UiSamples.RowBizMan, false),
            Lay = (s, f, d, k) => BizManPages.Lay(s, UiSamples.BizOps(words, state), f, d, k)
        };
        OpsInput(screen, state);
        return screen;
    }

    private static Screen UniformsScreen(int n, string state, string name, UiWords words, int focus = UiUniforms.Preset, int focusArg = 0, int hover = UiUniforms.Piece,
        int hoverArg = 2, string walk = "RRRDDLLD")
    {
        return new Screen
        {
            Name = "bizops_" + n.ToString("00") + "_" + name, AppName = words("myemployees_configure_uniform_presets", "Configure Uniform Presets"), Icon = UiIcon.PaintBrush,
            Backdrop = n % 2 == 0 ? "13.jpg" : "02.jpg", Bar = () => UiSamples.ShellBar(UiSamples.RowBizMan, false),
            Lay = (s, f, d, k) => UiUniforms.Lay(s, UiSamples.Uniforms(words, state), f, d, k),
            FocusCommand = focus, FocusArg = focusArg, HoverCommand = hover, HoverArg = hoverArg, Walk = Moves(walk)
        };
    }

    private static string OpsBackdrop(string state, int n)
    {
        if (state.StartsWith("factory", StringComparison.Ordinal))
        {
            return "09.jpg";
        }
        if (state.StartsWith("warehouse", StringComparison.Ordinal))
        {
            return "10.jpg";
        }
        return n % 2 == 0 ? "02.jpg" : "13.jpg";
    }

    // Where the mouse rests and the keys stand in each state, and the pad's walk from there; a state not named here starts
    // on the page's own first target, the mouse on the next.
    private static void OpsInput(Screen screen, string state)
    {
        screen.Walk = Moves("DDDRRU");
        switch (state)
        {
            case "schedule_empty":
                screen.FocusCommand = UiBoard.LaneCommand;
                screen.FocusArg = 0;
                screen.HoverCommand = BizManPages.OpsHours;
                screen.HoverArg = 0;
                screen.Walk = Moves("DDDDUUR");
                break;
            case "schedule_hours":
                screen.FocusCommand = BizManPages.OpsHour;
                screen.FocusArg = 9;
                screen.HoverCommand = BizManPages.OpsHour;
                screen.HoverArg = 14;
                screen.Walk = Moves("RRDDLLU");
                break;
            case "schedule_full":
            case "schedule_tens":
            case "schedule_long":
                screen.FocusCommand = UiBoard.ShiftCommand;
                screen.FocusArg = 0;
                screen.HoverCommand = UiBoard.ShiftCommand;
                screen.HoverArg = 4;
                screen.Walk = Moves("RRDDDLLU");
                break;
            case "schedule_drag":
                screen.FocusCommand = UiBoard.ShiftCommand;
                screen.FocusArg = 2;
                screen.HoverCommand = UiBoard.ShiftCommand;
                screen.HoverArg = 2;
                screen.Walk = Moves("RRDU");
                break;
            case "schedule_picker":
                screen.FocusCommand = BizManPages.People + UiDataTable.Row;
                screen.FocusArg = 0;
                screen.HoverCommand = BizManPages.People + UiDataTable.Row;
                screen.HoverArg = 2;
                screen.Walk = Moves("DDDUUL");
                break;
            case "pricing":
                screen.FocusCommand = BizManPages.Table + UiDataTable.Type;
                screen.FocusArg = 1;
                screen.HoverCommand = BizManPages.Table + UiDataTable.Row;
                screen.HoverArg = 3;
                screen.Walk = Moves("LLDDUU");
                break;
            case "logistics":
                screen.FocusCommand = BizManPages.OpsStopChoice;
                screen.FocusArg = 0;
                screen.HoverCommand = BizManPages.OpsStop;
                screen.HoverArg = 1;
                screen.Walk = Moves("DDDDR");
                break;
            case "hr":
            case "headhunters":
                screen.FocusCommand = BizManPages.OpsPlan;
                screen.FocusArg = 0;
                screen.HoverCommand = BizManPages.OpsPlan;
                screen.HoverArg = 1;
                screen.Walk = Moves("RDDDL");
                break;
            case "factory":
                screen.FocusCommand = BizManPages.Table + UiDataTable.Row;
                screen.FocusArg = 0;
                screen.HoverCommand = BizManPages.Table + UiDataTable.Row;
                screen.HoverArg = 2;
                screen.Walk = Moves("DDRRDD");
                break;
            case "warehouse_drivers":
                screen.FocusCommand = BizManPages.OpsChoice;
                screen.FocusArg = 0;
                screen.HoverCommand = BizManPages.OpsChoice;
                screen.HoverArg = 1;
                screen.Walk = Moves("DDDD");
                break;
            case "warehouse_stock":
                screen.FocusCommand = BizManPages.Table + UiDataTable.Row;
                screen.FocusArg = 0;
                screen.HoverCommand = BizManPages.Table + UiDataTable.Row;
                screen.HoverArg = 1;
                screen.Walk = Moves("DDDUUR");
                break;
        }
    }

    private const string OpsSwatch = "uniform:rgb:";
    private static readonly Dictionary<string, Sprite> OpsColours = new Dictionary<string, Sprite>();

    // A colour of the uniforms' palette as the game tints its swatch: the colour itself.
    private static Sprite OpsColour(string key)
    {
        if (OpsColours.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        Sprite sprite = null;
        if (uint.TryParse(key.Substring(OpsSwatch.Length), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out uint rgb))
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        }
        OpsColours[key] = sprite;
        return sprite;
    }

    private const string OpsGameLocales = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\locale";
    private static Dictionary<string, string> _opsPortuguese;

    // A text in Portuguese: the mod's own from its locale, the game's from the game's, English where neither has it.
    private static string OpsPortuguese(string key, string english)
    {
        if (_opsPortuguese == null)
        {
            _opsPortuguese = new Dictionary<string, string>();
            ReadOpsWords(Path.Combine(OpsGameLocales, "pt.json"));
            ReadOpsWords(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", "pt.json")));
        }
        return key != null && _opsPortuguese.TryGetValue(key, out string words) ? words : english;
    }

    private static void ReadOpsWords(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        foreach (Match pair in Regex.Matches(File.ReadAllText(path, Encoding.UTF8), "\"([^\"\\\\]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
        {
            _opsPortuguese[pair.Groups[1].Value] = Regex.Unescape(pair.Groups[2].Value);
        }
    }
}
