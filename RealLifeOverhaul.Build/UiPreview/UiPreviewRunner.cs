using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RealLifeOverhaul.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the kit offline, as the game will draw it: the mod's own Scripts/Ui files (copied in by preview.ps1) laying
/// out each sample screen and drawing it with the real fonts from the built bundle, inside the full menu's frame painted
/// flat as the kit paints it (a solid top bar with the app's name, the date and the money, and the bottom bar of
/// prompts; no blurred backdrop), at 1920 x 1080 and 1280 x 720, in the dark and the light look, one with the pad's
/// focus path drawn over it. The canvas is the game's 3840 x 2160 in canvas units, so 720p is the same layout at two
/// thirds, as the game's own scaler shows it. Themes renders the theme study: Contacts and the weekly meeting in each
/// direction, dark, at 1080p, into themes/<name>. Run (see preview.ps1):
///   Unity.exe -batchmode -quit -projectPath UiAssets/UnityProject -executeMethod UiPreviewRunner.Render -bundle path -out folder
///   Unity.exe ... -executeMethod UiPreviewRunner.Themes -bundle path -study path -out folder
/// </summary>
public static class UiPreviewRunner
{
    private const string Tag = "[UiPreview] ";
    private const float CanvasH = 2160f;

    private delegate void LayOut(UiScene s, UiFrame frame, bool dark, IUiKeys keys);

    private sealed class Screen
    {
        public string Name;
        public string AppName;    // the game's bar names the app
        public LayOut Lay;
        public int FocusCommand = -1, FocusArg;
        public Vector2Int[] Walk;
    }

    private sealed class Shot
    {
        public Screen Screen;
        public string Name;
        public int Width, Height;
        public bool Dark;
        public bool Path;
        public UiPad Pad;
    }

    /// <summary>The theme study: Contacts and the weekly meeting in each direction, dark, 1080p, into themes/name.</summary>
    public static void Themes()
    {
        try
        {
            string bundle = Argument("-bundle", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles/reallife_ui"));
            string study = Argument("-study", Path.GetFullPath("../build/bundles/reallife_ui_themes"));
            string outFolder = Argument("-out", Path.GetFullPath("../../../not-in-repo/previews/ui"));
            PlayerSettings.colorSpace = ColorSpace.Linear;
            UiFonts.LoadNow(bundle);
            UiFonts.LoadStudy(study);
            UiFontMetrics.Register(RealLifeOverhaul.Preview.UiThemeMetrics.Faces);
            TMP_FontAsset cjk = TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular") ?? TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            if (cjk != null)
            {
                UiFonts.SetFallback(cjk);
            }
            var contacts = new Screen { Name = "contacts", AppName = "Contacts", Lay = (s, f, d, k) => UiListDetail.Lay(s, UiSamples.Contacts(English, false), f, d, k),
                FocusCommand = UiListDetail.Row, FocusArg = 4 };
            var session = new Screen { Name = "session", AppName = "Desk", Lay = (s, f, d, k) => UiSessionSteps.Lay(s, UiSamples.Session(English), f, d, k) };
            int count = 0;
            foreach (UiTheme theme in new[] { UiTheme.Wireframe, UiTheme.Ledger, UiTheme.Terminal, UiTheme.Phone })
            {
                UiTokens.Theme = theme;
                string folder = Path.Combine(outFolder, "themes", theme.ToString().ToLowerInvariant());
                Directory.CreateDirectory(folder);
                foreach (Screen screen in new[] { contacts, session })
                {
                    RenderShot(new Shot { Screen = screen, Name = screen.Name + "_dark_1080", Width = 1920, Height = 1080, Dark = true }, folder);
                    count++;
                }
            }
            UiTokens.Theme = UiTheme.Wireframe;
            Debug.Log(Tag + "rendered " + count + " theme shots into " + Path.Combine(outFolder, "themes"));
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e);
            EditorApplication.Exit(1);
        }
    }

    private static readonly Vector2Int Up = new Vector2Int(0, -1), Down = new Vector2Int(0, 1), Left = new Vector2Int(-1, 0), Right = new Vector2Int(1, 0);

    public static void Render()
    {
        try
        {
            string bundle = Argument("-bundle", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles/reallife_ui"));
            string outFolder = Argument("-out", Path.GetFullPath("../../../not-in-repo/previews/ui"));
            Directory.CreateDirectory(outFolder);
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;   // the game's own: its interface colours read as they do there
            }
            UiFonts.LoadNow(bundle);
            if (!UiFonts.Own)
            {
                throw new InvalidOperationException("the font bundle did not load from " + bundle);
            }
            // The game's own font (with its Noto fallbacks) is the faces' last fallback in play; here a system font with
            // Chinese and Korean stands in for it, so a name in those scripts draws as it will in the game.
            TMP_FontAsset cjk = TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular") ?? TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
            if (cjk != null)
            {
                UiFonts.SetFallback(cjk);
            }
            var screens = new List<Screen>
            {
                new Screen { Name = "contacts", AppName = "Contacts", Lay = (s, f, d, k) => UiListDetail.Lay(s, UiSamples.Contacts(English, true), f, d, k), FocusCommand = UiListDetail.Row, FocusArg = 2,
                    Walk = new[] { Up, Down, Down, Right, Up, Right, Down, Down, Left, Left, Left } },
                new Screen { Name = "dashboard", AppName = "Economy", Lay = (s, f, d, k) => UiDashboard.Lay(s, UiSamples.Dashboard(English), f, d, k),
                    Walk = new[] { Down, Down, Down, Right, Down, Left, Up, Up } },
                new Screen { Name = "session", AppName = "Desk", Lay = (s, f, d, k) => UiSessionSteps.Lay(s, UiSamples.Session(English), f, d, k),
                    Walk = new[] { Down, Down, Down, Right, Left, Up } },
                new Screen { Name = "form", AppName = "CityAds", Lay = (s, f, d, k) => UiForm.Lay(s, UiSamples.Form(English), f, d, k),
                    Walk = new[] { Down, Down, Down, Down, Right, Left } },
            };
            for (int page = 0; page < UiGallery.PageCount; page++)
            {
                int p = page;
                screens.Add(new Screen { Name = "gallery_" + page, AppName = "Interface gallery", Lay = (s, f, d, k) => UiGallery.Lay(s, UiSamples.Gallery(English, p), f, d, k),
                    Walk = new[] { Down, Down, Right, Down, Right, Down, Left } });
            }
            var shots = new List<Shot>();
            foreach (Screen screen in screens)
            {
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_1080", Width = 1920, Height = 1080, Dark = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_light_1080", Width = 1920, Height = 1080, Dark = false });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_720", Width = 1280, Height = 720, Dark = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_light_720", Width = 1280, Height = 720, Dark = false });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_1080_pad_focus_path", Width = 1920, Height = 1080, Dark = true, Path = true, Pad = UiPad.Xbox });
            }
            foreach (Shot shot in shots)
            {
                RenderShot(shot, outFolder);
            }
            Debug.Log(Tag + "rendered " + shots.Count + " shots into " + outFolder);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e);
            EditorApplication.Exit(1);
        }
    }

    private static void RenderShot(Shot shot, string outFolder)
    {
        float canvasW = Mathf.Max(3840f, CanvasH * shot.Width / shot.Height);
        var root = new GameObject("Preview");
        try
        {
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CanvasH / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(canvasW / 2f, -CanvasH / 2f, -10f);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var cr = (RectTransform)canvasGo.transform;
            cr.pivot = new Vector2(0f, 1f);
            cr.sizeDelta = new Vector2(canvasW, CanvasH);
            cr.position = Vector3.zero;

            FullMenu(cr, canvasW, shot);
            RectTransform apps = UiView.NewRect(cr, "Apps");
            apps.anchorMin = new Vector2(0f, 0f);
            apps.anchorMax = new Vector2(1f, 1f);
            apps.pivot = new Vector2(0f, 1f);
            apps.offsetMin = new Vector2(0f, UiFrame.FullMenuBand * UiTokens.CanvasPerPx);
            apps.offsetMax = new Vector2(0f, -UiFrame.FullMenuTop * UiTokens.CanvasPerPx);

            var view = new UiView(apps, "Screen") { Motion = UiMotionSetting.Off };
            var scene = new UiScene();
            var frame = new UiFrame
            {
                Width = canvasW / UiTokens.CanvasPerPx,
                Height = CanvasH / UiTokens.CanvasPerPx - UiFrame.FullMenuTop - UiFrame.FullMenuBand,
                Band = UiFrame.FullMenuBand
            };
            IUiKeys keys = shot.Pad == UiPad.None ? (IUiKeys)UiDefaultKeys.Keyboard : UiDefaultKeys.XboxPad;
            shot.Screen.Lay(scene, frame, shot.Dark, keys);
            view.Write(scene, false);
            int focus = shot.Screen.FocusCommand >= 0 ? scene.FindNode(shot.Screen.FocusCommand, shot.Screen.FocusArg) : UiFocusRules.First(scene);
            view.SetFocus(focus >= 0 ? focus : UiFocusRules.First(scene), false, 0f);
            if (shot.Path)
            {
                FocusPath(apps, scene, focus, shot.Screen.Walk);
            }
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                t.ForceMeshUpdate(true, true);
            }
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(shot.Width, shot.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(shot.Width, shot.Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, shot.Width, shot.Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(outFolder, shot.Name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log(Tag + shot.Name + ": " + scene.ItemCount + " items, " + scene.NodeCount + " targets");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // The full menu's frame as the kit paints it, flat: the background, a solid top bar (173 canvas units, the game's)
    // with a hairline under it, the app's name, the money, the day and the time, and the bottom bar's own prompts at its
    // two ends. No blurred backdrop and no translucent glass (RLO-UI-015).
    private static void FullMenu(RectTransform canvas, float canvasW, Shot shot)
    {
        RectTransform bg = UiView.NewRect(canvas, "Backdrop");
        UiView.Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = Role(UiRole.Bg, shot.Dark);
        RectTransform bar = UiView.NewRect(canvas, "TopBar");
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, 173f);
        bar.gameObject.AddComponent<Image>().color = Role(UiRole.Header, shot.Dark);
        RectTransform rule = UiView.NewRect(bar, "Rule");
        rule.anchorMin = new Vector2(0f, 0f);
        rule.anchorMax = new Vector2(1f, 0f);
        rule.pivot = new Vector2(0.5f, 0f);
        rule.sizeDelta = new Vector2(0f, UiTokens.FrameRule > 0f ? UiTokens.FrameRule * UiTokens.CanvasPerPx : UiTokens.CanvasPerPx);
        rule.gameObject.AddComponent<Image>().color = Role(UiTokens.FrameRule > 0f ? UiRole.Text : UiRole.PanelEdge, shot.Dark);
        Color ink = Role(UiRole.Text, shot.Dark), quiet = Role(UiRole.Text2, shot.Dark);
        Label(bar, shot.Screen.AppName, 100f, -44f, 64f, TextAlignmentOptions.TopLeft, ink, UiStyle.AppTitle, false, 900f);
        Label(bar, "$48,210     Tuesday (Day 12)     14:32", canvasW - 1500f, -58f, 40f, TextAlignmentOptions.TopRight, quiet, UiStyle.Title, false, 1400f);
        Label(canvas, "Help [F1]", 50f, -(CanvasH - 98f), 32f, TextAlignmentOptions.TopLeft, quiet, UiStyle.Prompt, true, 400f);
        Label(canvas, "Submit bug report [F2]", 400f, -(CanvasH - 98f), 32f, TextAlignmentOptions.TopLeft, quiet * new Color(1f, 1f, 1f, 0.55f), UiStyle.Prompt, true, 700f);
        Label(canvas, "Close [Escape]", canvasW - 650f, -(CanvasH - 98f), 32f, TextAlignmentOptions.TopRight, quiet, UiStyle.Prompt, true, 600f);
    }

    private static Color Role(UiRole role, bool dark)
    {
        uint c = UiTokens.Colour(role, dark);
        return new Color(((c >> 24) & 0xFF) / 255f, ((c >> 16) & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, (c & 0xFF) / 255f);
    }

    private static void Label(Transform parent, string words, float x, float y, float size, TextAlignmentOptions align, Color ink, UiStyle style, bool caps, float w)
    {
        RectTransform r = UiView.NewRect(parent, words);
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, size * 1.4f);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = UiFonts.For(style);
        t.fontSize = size;
        t.alignment = align;
        t.color = ink;
        t.fontStyle = caps ? FontStyles.UpperCase : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        UiCase letters = UiTokens.Type(style).Case;
        t.text = letters == UiCase.Upper ? words.ToUpperInvariant() : letters == UiCase.Lower ? words.ToLowerInvariant() : words;
    }

    // The pad's path from the first target, a screen's own walk of the d-pad: each stop numbered and joined by a line, as
    // the d-pad moves the one focus (UiFocusRules); a press that leads nowhere is left out.
    private static void FocusPath(RectTransform parent, UiScene scene, int start, Vector2Int[] moves)
    {
        var stops = new List<Vector2> { Centre(scene, start) };
        int at = start;
        foreach (Vector2Int m in moves)
        {
            int next = UiFocusRules.Next(scene, at, m.x, m.y);
            if (next < 0)
            {
                continue;
            }
            at = next;
            stops.Add(Centre(scene, at));
        }
        RectTransform layer = UiView.NewRect(parent, "FocusPath");
        UiView.Stretch(layer);
        var ink = new Color(1f, 0.85f, 0.2f, 1f);
        for (int i = 1; i < stops.Count; i++)
        {
            Vector2 a = stops[i - 1], b = stops[i];
            RectTransform seg = UiView.NewRect(layer, "Step" + i);
            seg.pivot = new Vector2(0f, 0.5f);
            seg.anchoredPosition = new Vector2(a.x, -a.y);
            seg.sizeDelta = new Vector2(Vector2.Distance(a, b), 6f);
            seg.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            seg.gameObject.AddComponent<Image>().color = ink;
        }
        for (int i = 0; i < stops.Count; i++)
        {
            RectTransform dot = UiView.NewRect(layer, "Stop" + i);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = new Vector2(stops[i].x, -stops[i].y);
            dot.sizeDelta = new Vector2(52f, 52f);
            var img = dot.gameObject.AddComponent<Image>();
            img.sprite = UiSprites.Circle;
            img.type = Image.Type.Sliced;
            img.color = ink;
            RectTransform tr = UiView.NewRect(dot, "N");
            UiView.Stretch(tr);
            var t = tr.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = UiFonts.For(UiStyle.Number);
            t.fontSize = 30f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.08f, 0.06f, 0.02f, 1f);
            t.text = (i + 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static Vector2 Centre(UiScene scene, int node)
    {
        scene.NodeBox(node, out float x, out float y, out float w, out float h);
        return new Vector2((x + w / 2f) * UiTokens.CanvasPerPx, (y + h / 2f) * UiTokens.CanvasPerPx);
    }

    private static string English(string key, string english) => english;

    private static string Argument(string name, string fallback)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == name)
            {
                return arguments[i + 1];
            }
        }
        return fallback;
    }
}
