using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The HUD's pieces over the running game, each over the street it would stand on, and the box, a panel and the objectives
    // again over the night street: the bar by day, paused, fast-forwarding, at night with low needs and with the clock held
    // by a scene; an info panel for a
    // business and a car; a box at a door and the hover form indoors; side panels at work and everything folded at night;
    // the objectives; a trade's timing; the subway picker, in custody and with nothing open; the activity before it starts
    // and in each of its other states indoors; the time skip veil, and in custody with no Cancel; the map's side panel over
    // the city map with a place picked and with its filters alone; the designer's tool row indoors, with a tool in hand and
    // with none.
    static partial void AddHud(List<Screen> screens)
    {
        screens.Add(Hud("hud_bar", "13.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, false, false), UiDraw.SettingsPillCommand, 0, "LLRRRRRRRRRRRRRRR"));
        screens.Add(Hud("hud_bar_fps", "13.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, false, false, false, 144), UiDraw.NeedsPillCommand, 0, "LLRRRRRRRRRRRRRRR"));
        screens.Add(Hud("hud_bar_paused", "02.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, true, false, false), UiDraw.TimePillCommand, 1, "LLR"));
        screens.Add(Hud("hud_bar_fast", "02.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, true, false), UiDraw.NeedsPillCommand, 0, "LRRRRRRRRRRR"));
        screens.Add(Hud("hud_bar_night", "11.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, false, true), UiDraw.AlertsPillCommand, 0, "RRRRRRRRRRRR"));
        screens.Add(Hud("hud_bar_locked", "13.jpg", (s, f, d, k) => UiSamples.HudBarScene(s, f, d, k, English, false, false, false, true), UiDraw.MoneyPillCommand, 0, "LLRRRRRRRRRRRRRR"));
        screens.Add(Hud("hud_info_building", "13.jpg", (s, f, d, k) => UiSamples.HudInfo(s, f, d, k, English, false), UiInfoPanel.LinkCommand, 1 * 16 + 0, "RDDDDLLDRR"));
        screens.Add(Hud("hud_info_vehicle", "02.jpg", (s, f, d, k) => UiSamples.HudInfo(s, f, d, k, English, true), UiInfoPanel.FooterCommand, 1, "RDDDLLDRR"));
        screens.Add(Hud("hud_prompt", "02.jpg", (s, f, d, k) => UiSamples.HudPrompt(s, f, d, k, English, false), UiWorldPrompt.ActionCommand, 1, "DDD"));
        screens.Add(Hud("hud_prompt_hover", "05.jpg", (s, f, d, k) => UiSamples.HudPrompt(s, f, d, k, English, true), UiDraw.MoneyPillCommand, 0, "RR"));
        screens.Add(Hud("hud_prompt_night", "11.jpg", (s, f, d, k) => UiSamples.HudPrompt(s, f, d, k, English, false, true), UiWorldPrompt.ActionCommand, 2, "DDD"));
        screens.Add(Hud("hud_info_night", "11.jpg", (s, f, d, k) => UiSamples.HudInfo(s, f, d, k, English, true, true), UiInfoPanel.LinkCommand, 0 * 16 + 15, "RDDDLLDRR"));
        screens.Add(Hud("hud_objectives_night", "11.jpg", (s, f, d, k) => UiSamples.HudObjectives(s, f, d, k, English, false, true), UiObjectives.LineCommand, 1, "DRDDRDDDDDDDD"));
        screens.Add(Hud("hud_side", "13.jpg", (s, f, d, k) => UiSamples.HudSide(s, f, d, k, English, false), UiSidePanel.ActionCommand, 4, "DDDDDRDLD"));
        screens.Add(Hud("hud_side_collapsed", "11.jpg", (s, f, d, k) => UiSamples.HudSide(s, f, d, k, English, true), UiSidePanel.FoldCommand, 1, "RDDDLD"));
        screens.Add(Hud("hud_objectives", "02.jpg", (s, f, d, k) => UiSamples.HudObjectives(s, f, d, k, English, false), UiObjectives.LocateCommand, 0, "DDDDDDDDDDDDDD"));
        screens.Add(Hud("hud_objectives_empty", "13.jpg", (s, f, d, k) => UiSamples.HudObjectives(s, f, d, k, English, true), UiObjectives.FoldCommand, 0, "RR"));
        screens.Add(Hud("hud_meters", "13.jpg", (s, f, d, k) => UiSamples.HudMeters(s, f, d, k, English), UiSidePanel.FoldCommand, 0, "DD"));
        screens.Add(Hud("hud_meters_night", "11.jpg", (s, f, d, k) => UiSamples.HudMeters(s, f, d, k, English, true), UiSidePanel.FoldCommand, 1, "DD"));
        screens.Add(new Screen { Name = "hud_picker", AppName = "Subway", Icon = UiIcon.MapTrifold, Backdrop = "13.jpg",
            Lay = (s, f, d, k) => UiSamples.HudPickerScene(s, f, d, k, English, false, false), HoverCommand = UiPicker.EntryCommand, HoverArg = 2, Walk = Moves("DDDDDDDDR") });
        screens.Add(new Screen { Name = "hud_picker_locked", AppName = "Court", Icon = UiIcon.Scales, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.HudPickerScene(s, f, d, k, English, true, false), HoverCommand = UiPicker.EntryCommand, HoverArg = 1, Walk = Moves("DDD") });
        screens.Add(new Screen { Name = "hud_picker_empty", AppName = "Subway", Icon = UiIcon.MapTrifold, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.HudPickerScene(s, f, d, k, English, false, true), Walk = Moves("D") });
        screens.Add(new Screen { Name = "hud_activity_before", AppName = "Work", Icon = UiIcon.Coffee, Backdrop = "05.jpg",
            Lay = (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.NotStarted), HoverCommand = UiActivity.ButtonCommand, HoverArg = 1, Walk = Moves("RDLURRD") });
        screens.Add(Hud("hud_activity_running", "05.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Running), UiActivity.SkipCommand, 0, "LR"));
        screens.Add(Hud("hud_activity_started", "05.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Started), UiActivity.FastCommand, 0, "LRR"));
        screens.Add(Hud("hud_activity_moving", "13.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Moving), UiActivity.CancelCommand, 0, "RRL"));
        screens.Add(Hud("hud_activity_waiting", "00.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Waiting), UiActivity.SkipCommand, 0, "RRL"));
        screens.Add(Hud("hud_activity_finished", "05.jpg", (s, f, d, k) => UiSamples.HudActivityScene(s, f, d, k, English, UiActivityState.Finished), UiDraw.MoneyPillCommand, 0, "R"));
        screens.Add(new Screen { Name = "hud_time_skip", AppName = "Skip time", Icon = UiIcon.SkipForward, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.HudSkipScene(s, f, d, k, English), HoverCommand = UiTimeSkip.CancelCommand, Walk = Moves("D") });
        screens.Add(new Screen { Name = "hud_time_skip_locked", AppName = "Skip time", Icon = UiIcon.SkipForward, Backdrop = "11.jpg",
            Lay = (s, f, d, k) => UiSamples.HudSkipScene(s, f, d, k, English, true), NoKeys = true, Walk = Moves("") });
        screens.Add(new Screen { Name = "hud_map", AppName = "City map", Icon = UiIcon.MapTrifold, Backdrop = "map_rent.png",
            Lay = (s, f, d, k) => UiSamples.HudMapScene(s, f, d, k, English, true), HoverCommand = UiMapPanel.FilterCommand, HoverArg = 1, Walk = Moves("RDRDRDRDRDRDLDD") });
        screens.Add(new Screen { Name = "hud_map_filters", AppName = "City map", Icon = UiIcon.MapTrifold, Backdrop = "map_rent.png",
            Lay = (s, f, d, k) => UiSamples.HudMapScene(s, f, d, k, English, false), HoverCommand = UiMapPanel.LocateCommand, HoverArg = 2, Walk = Moves("RDRDRDRDRDLDD") });
        screens.Add(Hud("hud_tools", "03.jpg", (s, f, d, k) => UiSamples.HudTools(s, f, d, k, English, true), UiToolRow.ChoiceCommand, 3, "URRRRRLLDR"));
        screens.Add(Hud("hud_minimap", "13.jpg", (s, f, d, k) => UiSamples.HudMinimapScene(s, f, d, k, English, false), UiObjectives.LocateCommand, 0, "DDDD"));
        screens.Add(Hud("hud_minimap_beside", "02.jpg", (s, f, d, k) => UiSamples.HudMinimapScene(s, f, d, k, English, true), UiHudNotice.ActionCommand, 0, "RR"));
        screens.Add(Hud("hud_tools_none", "00.jpg", (s, f, d, k) => UiSamples.HudTools(s, f, d, k, English, false), UiToolRow.ToolCommand, 1, "RRRRLLLL"));
    }

    // A HUD scene over its street: the mouse resting on one of its targets, the pad's walk from its first.
    private static Screen Hud(string name, string backdrop, LayOut lay, int hoverCommand, int hoverArg, string walk) =>
        new Screen { Name = name, AppName = "HUD", Backdrop = backdrop, Lay = lay, HoverCommand = hoverCommand, HoverArg = hoverArg, Walk = Moves(walk) };

    // The bar where the screen and the game's UI zoom give it another room, beside the five shots each screen has: at 2560
    // x 1440 (the same layout as 1080, drawn sharper) and at 1920 x 1080 with the zoom at 80%, 120% and 200% (the HUD's
    // narrowest), by day and at night, with the mouse and with the keys.
    private static readonly string[] ZoomBars = { "hud_bar", "hud_bar_night", "hud_tools" };

    /// <summary>
    /// Renders the HUD's bar at other screens and UI zooms (see <see cref="ZoomBars"/>), as the HUD host scales it
    /// (UiHudRules.Scale), into the folder Render writes to. Run after preview.ps1 has copied the files in:
    ///   Unity.exe -batchmode -quit -projectPath UiAssets/UnityProject -executeMethod UiPreviewRunner.RenderBarZoom -bundle path -out folder -backdrops folder
    /// </summary>
    public static void RenderBarZoom()
    {
        try
        {
            string bundle = Argument("-bundle", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles/reallife_ui"));
            string outFolder = Argument("-out", Path.GetFullPath("../../../not-in-repo/previews/ui"));
            _backdrops = Argument("-backdrops", "");
            Directory.CreateDirectory(outFolder);
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }
            UiFonts.LoadNow(bundle);
            if (!UiFonts.Own)
            {
                throw new InvalidOperationException("the font bundle did not load from " + bundle);
            }
            TMP_FontAsset cjk = CjkFallback();
            if (cjk != null)
            {
                UiFonts.SetFallback(cjk);
            }
            UiChrome.Status = () => UiSamples.Status;
            var screens = new List<Screen>();
            AddHud(screens);
            int count = 0;
            foreach (Screen screen in screens)
            {
                if (Array.IndexOf(ZoomBars, screen.Name) < 0)
                {
                    continue;
                }
                foreach (bool keys in new[] { false, true })
                {
                    string input = keys ? "_keys" : "";
                    ZoomShot(screen, screen.Name + "_1440" + input, 1f, 2560, 1440, keys, outFolder);
                    ZoomShot(screen, screen.Name + "_zoom80" + input, 0.8f, 1920, 1080, keys, outFolder);
                    ZoomShot(screen, screen.Name + "_zoom120" + input, 1.2f, 1920, 1080, keys, outFolder);
                    ZoomShot(screen, screen.Name + "_zoom200" + input, 2f, 1920, 1080, keys, outFolder);
                    count += 4;
                }
            }
            Debug.Log(Tag + "rendered " + count + " zoomed shots into " + outFolder);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e);
            EditorApplication.Exit(1);
        }
    }

    // One shot of a HUD screen on the game's canvas under a UI zoom (3840 x 2160 divided by it, wider on an ultrawide), laid
    // out at the HUD's own scale, drawn into a picture of the screen's size.
    private static void ZoomShot(Screen screen, string name, float zoom, int width, int height, bool keys, string outFolder)
    {
        float canvasH = CanvasH / zoom, canvasW = Mathf.Max(3840f, CanvasH * width / height) / zoom;
        float scale = UiHudRules.Scale(canvasW);
        var root = new GameObject("Preview");
        try
        {
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = canvasH / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(canvasW / 2f, -canvasH / 2f, -10f);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var cr = (RectTransform)canvasGo.transform;
            cr.pivot = new Vector2(0f, 1f);
            cr.sizeDelta = new Vector2(canvasW, canvasH);
            cr.position = Vector3.zero;
            RectTransform world = UiView.NewRect(cr, "World");
            UiView.Stretch(world);
            RectTransform host = UiView.NewRect(cr, "Host");
            UiView.Stretch(host);
            Backdrop(cr, screen.Backdrop);
            UiChrome.Bar = screen.Bar ?? (() => UiSamples.Bar(screen.App));
            var view = new UiView(host, "Screen") { Motion = UiMotionSetting.Off, Scale = scale };
            var scene = new UiScene();
            var frame = new UiFrame { Width = canvasW / scale, Height = canvasH / scale, Band = UiFrame.BandHeight, Title = screen.AppName, Icon = screen.Icon, Pointer = !keys };
            screen.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
            view.Ring = keys;
            view.Write(scene, false);
            int focus = screen.FocusCommand >= 0 ? scene.FindNode(screen.FocusCommand, screen.FocusArg) : UiFocusRules.First(scene);
            view.SetFocus(focus >= 0 ? focus : UiFocusRules.First(scene), false, 0f);
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                t.ForceMeshUpdate(true, true);
            }
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(outFolder, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log(Tag + name + ": " + frame.Width.ToString("0") + " x " + frame.Height.ToString("0") + " px at " + scale.ToString("0.##") + ", " + scene.ItemCount + " items");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
