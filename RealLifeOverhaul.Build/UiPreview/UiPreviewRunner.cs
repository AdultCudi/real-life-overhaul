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
/// out each sample screen and drawing it with the real fonts from the built bundle over the game's whole full menu, its
/// masthead and prompt band painted flat by the kit, at 1920 x 1080 and 1280 x 720, in the dark and the light look, as a
/// mouse player sees it (no focus ring, the band without Move and Select), with the mouse resting on a target (its quiet
/// hover fill), with the keys leading (the ring and the whole band) and with the pad's focus path drawn over it. The
/// canvas is the game's 3840 x 2160 in canvas units, so 720p is the same layout at two thirds, as the game's own scaler
/// shows it. Run (see preview.ps1):
///   Unity.exe -batchmode -quit -projectPath UiAssets/UnityProject -executeMethod UiPreviewRunner.Render -bundle path -out folder [-only prefix,prefix]
/// </summary>
public static class UiPreviewRunner
{
    private const string Tag = "[UiPreview] ";
    private const float CanvasH = 2160f;

    private delegate void LayOut(UiScene s, UiFrame frame, bool dark, IUiKeys keys);

    private sealed class Screen
    {
        public string Name;
        public string AppName;    // the masthead names the app
        public LayOut Lay;
        public int FocusCommand = -1, FocusArg;
        public int HoverCommand = -1, HoverArg;   // the target the mouse rests on in the hover shot, or the second target
        public Vector2Int[] Walk;
    }

    private sealed class Shot
    {
        public Screen Screen;
        public string Name;
        public int Width, Height;
        public bool Dark;
        public bool Path;
        public bool Keys;    // the keys lead: the ring shows and the band names every prompt
        public bool Hover;   // the mouse rests on the screen's hover target
        public UiPad Pad;
    }

    private static readonly Vector2Int Up = new Vector2Int(0, -1), Down = new Vector2Int(0, 1), Left = new Vector2Int(-1, 0), Right = new Vector2Int(1, 0);

    public static void Render()
    {
        try
        {
            string bundle = Argument("-bundle", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles/reallife_ui"));
            string outFolder = Argument("-out", Path.GetFullPath("../../../not-in-repo/previews/ui"));
            string[] only = Argument("-only", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
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
            UiChrome.Status = () => UiSamples.Status;
            var shots = new List<Shot>();
            foreach (Screen screen in Screens())
            {
                if (only.Length > 0 && Array.FindIndex(only, o => screen.Name.StartsWith(o, StringComparison.Ordinal)) < 0)
                {
                    continue;
                }
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_1080", Width = 1920, Height = 1080, Dark = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_light_1080", Width = 1920, Height = 1080, Dark = false });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_720", Width = 1280, Height = 720, Dark = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_light_720", Width = 1280, Height = 720, Dark = false });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_1080_mouse_hover", Width = 1920, Height = 1080, Dark = true, Hover = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_light_1080_mouse_hover", Width = 1920, Height = 1080, Dark = false, Hover = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_1080_keys", Width = 1920, Height = 1080, Dark = true, Keys = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_dark_1080_pad_focus_path", Width = 1920, Height = 1080, Dark = true, Path = true, Keys = true, Pad = UiPad.Xbox });
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

    // Every screen the kit draws, with its app's name, the target focus rests on and a walk of the d-pad for the path shot.
    private static List<Screen> Screens()
    {
        var screens = new List<Screen>
        {
            new Screen { Name = "contacts", AppName = "Contacts", Lay = (s, f, d, k) => UiListDetail.Lay(s, UiSamples.Contacts(English, false), f, d, k), FocusCommand = UiListDetail.Row, FocusArg = 4,
                HoverCommand = UiListDetail.Row, HoverArg = 1, Walk = new[] { Up, Down, Down, Right, Up, Right, Down, Down, Left, Left, Left } },
            new Screen { Name = "dashboard", AppName = "Economy", Lay = (s, f, d, k) => UiDashboard.Lay(s, UiSamples.Dashboard(English), f, d, k),
                HoverCommand = UiDashboard.RowCommand, HoverArg = 2, Walk = new[] { Down, Down, Down, Right, Down, Left, Up, Up } },
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
        foreach (UiSamples.Sample sample in UiSamples.Overhaul(English))
        {
            UiSamples.Sample x = sample;
            screens.Add(new Screen { Name = x.Name, AppName = x.App, Lay = (s, f, d, k) => x.Lay(s, f, d, k), Walk = Moves(x.Walk), HoverCommand = UiDashboard.RowCommand, HoverArg = 1 });
        }
        return screens;
    }

    // A sample's walk of the d-pad, written as letters (U, D, L, R).
    private static Vector2Int[] Moves(string walk)
    {
        var moves = new Vector2Int[walk.Length];
        for (int i = 0; i < walk.Length; i++)
        {
            moves[i] = walk[i] == 'U' ? Up : walk[i] == 'D' ? Down : walk[i] == 'L' ? Left : Right;
        }
        return moves;
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
            RectTransform host = UiView.NewRect(cr, "Host");
            UiView.Stretch(host);

            var view = new UiView(host, "Screen") { Motion = UiMotionSetting.Off };
            var scene = new UiScene();
            UiFrame frame = UiFrame.FullMenu(canvasW / CanvasH, shot.Screen.AppName);
            frame.Pointer = !shot.Keys;   // as UiHost lays a screen out while the mouse leads
            IUiKeys keys = shot.Pad == UiPad.None ? (IUiKeys)UiDefaultKeys.Keyboard : UiDefaultKeys.XboxPad;
            shot.Screen.Lay(scene, frame, shot.Dark, keys);
            view.Ring = shot.Keys;
            view.Write(scene, false);
            int focus = shot.Screen.FocusCommand >= 0 ? scene.FindNode(shot.Screen.FocusCommand, shot.Screen.FocusArg) : UiFocusRules.First(scene);
            focus = focus >= 0 ? focus : UiFocusRules.First(scene);
            view.SetFocus(focus, false, 0f);
            if (shot.Hover)
            {
                int hover = shot.Screen.HoverCommand >= 0 ? scene.FindNode(shot.Screen.HoverCommand, shot.Screen.HoverArg) : -1;
                view.SetHover(hover >= 0 ? hover : scene.NodeCount > 1 ? 1 : 0);
            }
            if (shot.Path && focus >= 0)
            {
                FocusPath(host, scene, focus, shot.Screen.Walk);
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
