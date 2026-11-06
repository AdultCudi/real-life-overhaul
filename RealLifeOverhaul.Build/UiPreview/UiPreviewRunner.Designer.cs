using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The interior designer on the kit over the game's own rooms: every state the samples hold on the indoor pictures (00, 03 and 05), the longest
    // languages beside the English for the panels that hold the most words, and the game's own designer (07) as the picture to compare with.
    // Pictures: the mockups' thumbnails of the game's furniture and floors (local only, like the backdrops), and a swatch's colour flat.
    static partial void AddDesigner(List<Screen> screens)
    {
        Func<string, Sprite> before = UiPictures.Find;
        UiPictures.Find = key => DesignerPicture(key) ?? before?.Invoke(key);
        string[] backdrops = { "00.jpg", "03.jpg", "05.jpg" };
        for (int i = 0; i < UiSamples.DesignerStates.Length; i++)
        {
            screens.Add(DesignerShot(UiSamples.DesignerStates[i], "en", backdrops[i % backdrops.Length]));
        }
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            foreach (string st in new[] { "furniture", "floors", "blueprint", "time", "security" })
            {
                screens.Add(DesignerShot(st, lang, "00.jpg"));
            }
        }
        screens.Add(new Screen
        {
            Name = "designer_the_games_own", AppName = "Interior designer", Icon = UiIcon.Sofa, Backdrop = "07.jpg", NoKeys = true, Single = true,
            Lay = (s, f, d, k) =>
            {
                s.Begin("designer before", f.Width, f.Height, d);
                s.Floats = true;
            }
        });
    }

    private static Screen DesignerShot(string state, string lang, string backdrop)
    {
        UiWords t = lang == "en" ? ModEnglish : ModWords(lang);
        bool materials = state == "floors" || state == "walls", catalogue = state.StartsWith("furniture", StringComparison.Ordinal) || state == "folded" || state == "second_floor";
        return new Screen
        {
            Name = "designer_" + state + (lang == "en" ? string.Empty : lang == "pt" ? "_pt" : "_zh"), AppName = "Interior designer", Icon = UiIcon.Sofa, Backdrop = backdrop, NoKeys = true,
            HoverCommand = catalogue ? UiDesignerPanels.CardCommand : materials ? UiDesignerPanels.MaterialCardCommand : state == "time" ? UiDesignerPanels.TimeMoreCommand
                : state.StartsWith("blueprint", StringComparison.Ordinal) ? UiDesignerPanels.SaveCommand : UiDesignerPanels.ToolCommand,
            HoverArg = catalogue || materials ? 1 : 0,
            Lay = (s, f, d, k) => UiSamples.DesignerScene(s, f, d, k, t, state)
        };
    }

    private static readonly Dictionary<string, Sprite> DesignerPictures = new Dictionary<string, Sprite>(64);
    private static string _designerThumbs;
    private static readonly string[] DesignerThumbNames =
    {
        "flower_stand", "flower_shelf", "bakery_shelf", "counter", "freezer", "fridge", "palm", "pendant", "armchair", "coffee_table", "espresso", "tv_wall", "chair_wood", "sofa", "dining_set", "grill"
    };

    // "designer:sample:<n>": a furniture thumbnail of the mockups; "designer:sample:material:<n>": a floor's; "swatch:RRGGBB": a square of that colour. Null for any other key.
    private static Sprite DesignerPicture(string key)
    {
        if (key == null)
        {
            return null;
        }
        bool swatch = key.StartsWith(UiDraw.SwatchPicture, StringComparison.Ordinal);
        if (!swatch && !key.StartsWith("designer:sample:", StringComparison.Ordinal))
        {
            return null;
        }
        if (DesignerPictures.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        Sprite sprite = null;
        if (swatch)
        {
            int rgb = UiDraw.SwatchColour(key);
            if (rgb >= 0)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
                var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = c;
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            }
        }
        else
        {
            string rest = key.Substring("designer:sample:".Length);
            bool material = rest.StartsWith("material:", StringComparison.Ordinal);
            int n = int.TryParse(material ? rest.Substring(9) : rest, out int parsed) ? parsed : 0;
            string file = material ? "floor_" + (n % 14).ToString("00") : DesignerThumbNames[n % DesignerThumbNames.Length];
            string path = Path.Combine(DesignerThumbs(), file + ".png");
            if (File.Exists(path))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(path));
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        DesignerPictures[key] = sprite;
        return sprite;
    }

    // The mockups' thumbnails: not-in-repo beside the repository (or above a worktree of it), never committed.
    private static string DesignerThumbs()
    {
        if (_designerThumbs != null)
        {
            return _designerThumbs;
        }
        _designerThumbs = string.Empty;
        for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
        {
            string thumbs = Path.Combine(dir.FullName, "not-in-repo", "previews", "ui", "mockups", "designer", "thumbs");
            if (Directory.Exists(thumbs))
            {
                _designerThumbs = thumbs;
                break;
            }
        }
        return _designerThumbs;
    }
}
