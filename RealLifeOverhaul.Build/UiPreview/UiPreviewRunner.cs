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
/// out each sample screen and drawing it with the real fonts from the built bundle, inside a stand-in for the game's full
/// menu (its blurred backdrop, its blue top bar and its bottom bar of prompts), at 1920 x 1080 and 1280 x 720, in the
/// dark and the light look, one with the pad's focus path drawn over it. The canvas is the game's 3840 x 2160 in canvas
/// units, so 720p is the same layout at two thirds, as the game's own scaler shows it. Run (see preview.ps1):
///   Unity.exe -batchmode -quit -projectPath UiAssets/UnityProject -executeMethod UiPreviewRunner.Render
///             -bundle path -out folder [-backdrop png]
/// </summary>
public static class UiPreviewRunner
{
    private const string Tag = "[UiPreview] ";
    private const float CanvasH = 2160f;
    private static readonly Color TopBar = new Color(0.271f, 0.477f, 0.66f, 0.463f);   // the game's full menu bar (its tree)

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

    private static readonly Vector2Int Up = new Vector2Int(0, -1), Down = new Vector2Int(0, 1), Left = new Vector2Int(-1, 0), Right = new Vector2Int(1, 0);

    public static void Render()
    {
        try
        {
            string bundle = Argument("-bundle", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles/reallife_ui"));
            string outFolder = Argument("-out", Path.GetFullPath("../../../not-in-repo/previews/ui"));
            string backdrop = Argument("-backdrop", "");
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
            Texture2D back = File.Exists(backdrop) ? Png(backdrop) : null;
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
                RenderShot(shot, back, outFolder);
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

    private static void RenderShot(Shot shot, Texture2D back, string outFolder)
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

            FullMenu(cr, back, canvasW, shot);
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
            foreach (UiSpark spark in root.GetComponentsInChildren<UiSpark>(false))
            {
                Rect r = spark.rectTransform.rect;
                var points = typeof(UiSpark).GetField("_points", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(spark) as float[];
                Debug.Log(Tag + shot.Name + " spark: " + r.width.ToString("0") + " x " + r.height.ToString("0") + ", colour " + spark.color + ", active " + spark.isActiveAndEnabled
                          + ", points " + (points != null ? points.Length : -1) + ", alpha " + spark.canvasRenderer.GetAlpha() + ", material " + (spark.materialForRendering != null ? spark.materialForRendering.shader.name : "none"));
            }
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

    // The game's full menu as its tree has it: the blurred backdrop, the translucent blue bar (173 canvas units) with the
    // app's name, the date and the money, and the bottom bar's own prompts at its two ends.
    private static void FullMenu(RectTransform canvas, Texture2D back, float canvasW, Shot shot)
    {
        RectTransform bg = UiView.NewRect(canvas, "Backdrop");
        UiView.Stretch(bg);
        if (back != null)
        {
            var raw = bg.gameObject.AddComponent<RawImage>();
            raw.texture = back;
            raw.uvRect = Cover(back, canvasW / CanvasH);
        }
        else
        {
            bg.gameObject.AddComponent<Image>().color = new Color(0.33f, 0.36f, 0.4f, 1f);
        }
        RectTransform bar = UiView.NewRect(canvas, "TopBar");
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.sizeDelta = new Vector2(0f, 173f);
        bar.gameObject.AddComponent<Image>().color = TopBar;
        Label(bar, shot.Screen.AppName, 100f, -40f, 68f, TextAlignmentOptions.TopLeft, 1f, false, 900f);
        Label(bar, "$48,210     Tuesday (Day 12)     14:32", canvasW - 1500f, -55f, 44f, TextAlignmentOptions.TopRight, 1f, false, 1400f);
        Label(canvas, "Help [F1]", 50f, -(CanvasH - 98f), 32f, TextAlignmentOptions.TopLeft, 1f, true, 400f);
        Label(canvas, "Submit bug report [F2]", 400f, -(CanvasH - 98f), 32f, TextAlignmentOptions.TopLeft, 0.55f, true, 700f);
        Label(canvas, "Close [Escape]", canvasW - 650f, -(CanvasH - 98f), 32f, TextAlignmentOptions.TopRight, 1f, true, 600f);
    }

    private static void Label(Transform parent, string words, float x, float y, float size, TextAlignmentOptions align, float alpha, bool caps, float w)
    {
        RectTransform r = UiView.NewRect(parent, words);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, size * 1.4f);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = UiFonts.For(UiStyle.Title);
        t.fontSize = size;
        t.alignment = align;
        t.color = new Color(1f, 1f, 1f, alpha);
        t.fontStyle = caps ? FontStyles.UpperCase : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.text = words;
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

    private static Rect Cover(Texture2D tex, float aspect)
    {
        float texAspect = tex.width / (float)tex.height;
        return aspect > texAspect ? new Rect(0f, (1f - texAspect / aspect) / 2f, 1f, texAspect / aspect) : new Rect((1f - aspect / texAspect) / 2f, 0f, aspect / texAspect, 1f);
    }

    private static string English(string key, string english) => english;

    private static Texture2D Png(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        return tex;
    }

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
