using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // BizMan in every state, its app row's tile open in the status bar: the stores over the day streets the full menu
    // leaves (13 and 02), the warehouse over the warehouse, the factory over the factory, the villa over the villa, another's
    // shop by night. The German states are in German, the longest of the mod's languages, the game's words from its own file.
    static partial void AddBizMan(List<Screen> screens)
    {
        Func<string, Sprite> before = UiPictures.Find;
        UiPictures.Find = key => key != null && key.StartsWith("bizman:", StringComparison.Ordinal) ? BizManPicture(key) : before?.Invoke(key);
        for (int i = 0; i < UiSamples.BizManStates.Length; i++)
        {
            string state = UiSamples.BizManStates[i];
            var screen = new Screen
            {
                Name = "bizman_" + (i + 1).ToString("00") + "_" + state, AppName = "BizMan", Icon = UiIcon.Building, Backdrop = BizManBackdrop(state, i),
                Bar = () => UiSamples.ShellBar(UiSamples.RowBizMan, false),
                Lay = (s, f, d, k) => BizManPages.Lay(s, UiSamples.BizMan(state.EndsWith("_german", StringComparison.Ordinal) ? German : (UiWords)English, state), f, d, k)
            };
            BizManInput(screen, state);
            screens.Add(screen);
        }
    }

    private static string BizManBackdrop(string state, int i)
    {
        switch (state)
        {
            case "warehouse": case "portfolio_warehouses": return "10.jpg";
            case "factory": return "09.jpg";
            case "villa": return "12.jpg";
            case "rival": return "11.jpg";
            default: return i % 2 == 0 ? "13.jpg" : "02.jpg";
        }
    }

    // Where the mouse rests and the keys stand in each state, and the pad's walk from the page's first target.
    private static void BizManInput(Screen screen, string state)
    {
        switch (state)
        {
            case "portfolio": case "portfolio_long": case "portfolio_german":
                screen.HoverCommand = UiDataTable.First + UiDataTable.Row;
                screen.HoverArg = 2;
                screen.FocusCommand = UiDataTable.First + UiDataTable.Row;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDDDRRRDDDLL");
                break;
            case "portfolio_filter_open":
                screen.HoverCommand = BizManPages.FilterOption;
                screen.HoverArg = 2;
                screen.FocusCommand = BizManPages.FilterOption;
                screen.FocusArg = 0;
                screen.Walk = Moves("DDDUU");
                break;
            case "portfolio_warehouses":
                screen.HoverCommand = UiDataTable.First + UiDataTable.Act;
                screen.HoverArg = 1;
                screen.FocusCommand = UiDataTable.First + UiDataTable.Row;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDDRLL");
                break;
            case "portfolio_empty":
                screen.HoverCommand = BizManPages.Section;
                screen.HoverArg = 0;
                screen.Walk = Moves("DDDRDD");
                break;
            case "store_insight":
                screen.HoverCommand = BizManPages.Period;
                screen.HoverArg = 0;
                screen.FocusCommand = BizManPages.Period;
                screen.FocusArg = 1;
                screen.Walk = Moves("DLLLLLLL");
                break;
            case "store_pricing":
                screen.HoverCommand = BizManPages.Goods + UiDataTable.More;
                screen.HoverArg = 1;
                screen.FocusCommand = BizManPages.Goods + UiDataTable.Type;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDDDRRLL");
                break;
            case "store_deliveries":
                screen.HoverCommand = BizManPages.Item;
                screen.HoverArg = 1;
                screen.FocusCommand = BizManPages.Repeat;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDRDDDD");
                break;
            case "store_marketing":
                screen.HoverCommand = BizManPages.Toggle;
                screen.HoverArg = 2;
                screen.FocusCommand = BizManPages.Toggle;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDDDDRDD");
                break;
            case "store_settings": case "store_settings_unsaved": case "store_german":
                screen.HoverCommand = BizManPages.Kind;
                screen.HoverArg = 0;
                screen.FocusCommand = BizManPages.Type;
                screen.FocusArg = BizManPages.FieldName;
                screen.Walk = Moves("RDDDRDD");
                break;
            case "store_estate":
                screen.HoverCommand = BizManPages.Action;
                screen.HoverArg = BizManPages.DoApplyRent;
                screen.FocusCommand = BizManPages.Slider;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDDRDD");
                break;
            case "store_switch_open":
                screen.HoverCommand = UiFirmHead.First + UiFirmHead.Option;
                screen.HoverArg = 3;
                screen.FocusCommand = UiFirmHead.First + UiFirmHead.Option;
                screen.FocusArg = 0;
                screen.Walk = Moves("DDDU");
                break;
            case "store_needs_open":
                screen.HoverCommand = UiFirmHead.First + UiFirmHead.Todo;
                screen.HoverArg = 1;
                screen.FocusCommand = UiFirmHead.First + UiFirmHead.Todo;
                screen.FocusArg = 1;   // the open list, the layer's one stop
                screen.Walk = Moves("RDD");
                break;
            case "store_interior":
                screen.HoverCommand = BizManPages.Page;
                screen.HoverArg = 2;
                screen.FocusCommand = BizManPages.Page;
                screen.FocusArg = 0;
                screen.Walk = Moves("DDLUL");
                break;
            case "store_classic": case "store_foreign":
                screen.HoverCommand = BizManPages.Classic;
                screen.HoverArg = 0;
                screen.Walk = Moves("RDDLDD");
                break;
            case "start":
                screen.HoverCommand = UiCatalogue.First + UiCatalogue.Card;
                screen.HoverArg = 2;
                screen.FocusCommand = UiCatalogue.First + UiCatalogue.Card;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDRRDR");
                break;
            case "rival":
                screen.HoverCommand = BizManPages.Action;
                screen.HoverArg = BizManPages.DoTakeover;
                screen.FocusCommand = UiFirmHead.First + UiFirmHead.Place;
                screen.FocusArg = 0;
                screen.Walk = Moves("DRDDDRDD");
                break;
            case "store_building": case "store_long": case "warehouse": case "factory": case "headquarters":
                screen.HoverCommand = UiFirmHead.First + UiFirmHead.Action;
                screen.HoverArg = 0;
                screen.FocusCommand = UiFirmHead.First + UiFirmHead.Place;
                screen.FocusArg = 0;
                screen.Walk = Moves(state == "factory" ? "URLLDDD" : state == "warehouse" || state == "headquarters" ? "ULDDD" : "URRDLLLL");
                break;
            default:
                screen.HoverCommand = BizManPages.Action;
                screen.HoverArg = BizManPages.DoPreview;
                screen.FocusCommand = UiFirmHead.First + UiFirmHead.Place;
                screen.FocusArg = 0;
                screen.Walk = Moves("RDDDRDD");
                break;
        }
    }

    private static readonly Dictionary<string, Sprite> BizPictures = new Dictionary<string, Sprite>();

    // A place's photo offline: a frontage of the game's own street picture of that kind of place, in the shape the game
    // frames its photo.
    private static Sprite BizManPicture(string key)
    {
        if (string.IsNullOrEmpty(_backdrops))
        {
            return null;
        }
        if (BizPictures.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        if (key == "bizman:logo")
        {
            return BizPictures[key] = Logo();
        }
        string file = key == "bizman:villa" ? "12.jpg" : key == "bizman:warehouse" ? "10.jpg" : key == "bizman:factory" ? "09.jpg" : "02.jpg";
        string path = Path.Combine(_backdrops, file);
        Sprite sprite = null;
        if (File.Exists(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(File.ReadAllBytes(path));
            float h = tex.height * 0.5f, w = h * 1.6f;
            sprite = Sprite.Create(tex, new Rect(tex.width * 0.55f - w / 2f, tex.height * 0.3f, w, h), new Vector2(0.5f, 0.5f));
        }
        BizPictures[key] = sprite;
        return sprite;
    }

    private const string GameShapes = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\LogoShapes";

    // The café's logo as the game draws its own: one of the game's logo shapes for a coffee shop, from the game's files.
    private static Sprite Logo()
    {
        string path = Path.Combine(GameShapes, "coffee1.png");
        if (!File.Exists(path))
        {
            return null;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    private const string GameLocales = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\locale";
    private static Dictionary<string, string> _german;

    // A text in German: the mod's own from its locale, the game's from the game's, English where neither has it.
    private static string German(string key, string english)
    {
        if (_german == null)
        {
            _german = new Dictionary<string, string>();
            ReadGerman(Path.Combine(GameLocales, "de.json"));
            ReadGerman(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", "de.json")));
        }
        return key != null && _german.TryGetValue(key, out string words) ? words : english;
    }

    private static void ReadGerman(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        foreach (Match pair in Regex.Matches(File.ReadAllText(path, Encoding.UTF8), "\"([^\"\\\\]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
        {
            _german[pair.Groups[1].Value] = Regex.Unescape(pair.Groups[2].Value);
        }
    }
}
