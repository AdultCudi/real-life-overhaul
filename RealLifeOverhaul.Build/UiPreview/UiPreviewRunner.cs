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
public static partial class UiPreviewRunner
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
        public bool Tour;         // the pad path is every target the d-pad reaches from the first, right then down then left and up (UiPreviewRunner.TourOf)
        public string Backdrop;   // the game's world behind it, a picture's file in the backdrops folder, or null for black
        public int App = -1;      // the app the status bar marks as open
        public bool Extra;        // also a hover shot and a keys shot (the prototype's two that show input)
        public UiIcon Icon;       // the window's icon, as the host is given it in the game
        public Func<UiStatusModel> Bar;   // the status bar it stands over, when not the sample's with App open (the frame's app row)
        public bool NoKeys;       // takes no key (the notice cards): its keys and pad shots show it as the keys leave it, no ring, no path
        public bool Types;        // a field of it takes the keys (Typing below): it also gets a shot with the caret hidden, the blink's other half
    }

    // The screen laid out as the host lays it while the field of that command and argument takes the keys (UiScene.TypingCommand).
    private static Screen Typing(Screen screen, int command, int arg)
    {
        LayOut lay = screen.Lay;
        screen.Lay = (s, f, d, k) =>
        {
            s.TypingCommand = command;
            s.TypingArg = arg;
            lay(s, f, d, k);
        };
        screen.Types = true;
        return screen;
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
        public bool CaretOff;   // the blink's hidden half: the caret of the typing field not drawn
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
            string set = Argument("-set", "");
            _backdrops = Argument("-backdrops", "");
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
            TMP_FontAsset cjk = CjkFallback();
            if (cjk != null)
            {
                UiFonts.SetFallback(cjk);
            }
            UiChrome.Status = () => UiSamples.Status;
            var shots = new List<Shot>();
            bool prototype = set == "cs2";
            foreach (Screen screen in prototype ? Prototype() : Screens())
            {
                if (only.Length > 0 && Array.FindIndex(only, o => screen.Name.StartsWith(o, StringComparison.Ordinal)) < 0)
                {
                    continue;
                }
                // One look now: each screen at 1080 and 720, with the mouse resting on a target and with the keys leading.
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_1080", Width = 1920, Height = 1080, Dark = true });
                shots.Add(new Shot { Screen = screen, Name = screen.Name + "_720", Width = 1280, Height = 720, Dark = true });
                if (!prototype || screen.Extra)
                {
                    shots.Add(new Shot { Screen = screen, Name = screen.Name + "_1080_mouse_hover", Width = 1920, Height = 1080, Dark = true, Hover = true });
                    shots.Add(new Shot { Screen = screen, Name = screen.Name + "_1080_keys", Width = 1920, Height = 1080, Dark = true, Keys = true });
                }
                if (screen.Types)
                {
                    shots.Add(new Shot { Screen = screen, Name = screen.Name + "_1080_caret_off", Width = 1920, Height = 1080, Dark = true, CaretOff = true });
                }
                if (!prototype)
                {
                    shots.Add(new Shot { Screen = screen, Name = screen.Name + "_1080_pad_focus_path", Width = 1920, Height = 1080, Dark = true, Path = true, Keys = true, Pad = UiPad.Xbox });
                }
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

    // The prototype's six screens over the game's own streets (design/DIRECTION_CS2.md, section 7).
    private static List<Screen> Prototype()
    {
        return new List<Screen>
        {
            new Screen { Name = "1_person_info_panel", AppName = "Rosa Stanley", Lay = (s, f, d, k) => UiInfoPanel.Lay(s, UiSamples.Person(English), f, d, k), Backdrop = "13.jpg",
                HoverCommand = UiInfoPanel.LinkCommand, HoverArg = 1 * 16 + 1, Extra = true },
            // The city map with its blocks coloured by rent, as the info view would leave it: the game's own map, painted for the preview.
            new Screen { Name = "2_people_info_view", AppName = "The city's people", Lay = (s, f, d, k) => UiInfoView.Lay(s, UiSamples.People(English), f, d, k),
                Backdrop = "map_rent.png" },
            new Screen { Name = "3_studio_app_window", AppName = "Tech", Lay = (s, f, d, k) => UiAppWindow.Lay(s, UiSamples.Studio(English), f, d, k), Backdrop = "02.jpg",
                App = UiSamples.AppTech, HoverCommand = UiAppWindow.RowCommand, HoverArg = 1, Extra = true },
            new Screen { Name = "4_desk_session_decision", AppName = "Desk", Lay = (s, f, d, k) => UiDecision.Lay(s, UiSamples.Meeting(English), f, d, k), Backdrop = "11.jpg" },
            new Screen { Name = "5_contacts_app_window", AppName = "Contacts", Lay = (s, f, d, k) => UiListDetail.Lay(s, UiSamples.ContactsWindow(English), f, d, k), Backdrop = "13.jpg",
                Icon = UiIcon.AddressBook,
                App = UiSamples.AppContacts, FocusCommand = UiListDetail.Row, FocusArg = 4 },
            new Screen { Name = "6_phone_home", AppName = "Phone", Lay = (s, f, d, k) => UiPhone.Lay(s, UiSamples.PhoneHome(English), f, d, k), Backdrop = "02.jpg" },
            new Screen { Name = "8_employee_profile_app_window", AppName = "Business", Lay = (s, f, d, k) => UiAppWindow.Lay(s, UiSamples.Employee(English), f, d, k),
                Backdrop = "13.jpg", App = UiSamples.AppBusiness },
            new Screen { Name = "7_icon_style_sheet", AppName = "Interface gallery", Lay = (s, f, d, k) => UiGallery.Lay(s, UiSamples.Gallery(English, UiGallery.IconsPage), f, d, k),
                Icon = UiIcon.Grid,
                Backdrop = "13.jpg" },
        };
    }

    // Every screen the kit draws, gathered from each area's part (UiPreviewRunner.<Area>.cs) with its app's name, the target
    // focus rests on, a walk of the d-pad for the path shot and the game's picture it stands over. Each area has a file of its
    // own, so jobs on different areas never edit one list; an area with no file yet is a partial method nobody wrote, and
    // the compiler drops its call. A new area adds its line to both lists below.
    private static List<Screen> Screens()
    {
        var screens = new List<Screen>();
        AddContacts(screens);
        AddKit(screens);
        AddPages(screens);
        AddShell(screens);
        AddPhone(screens);
        AddGrids(screens);
        AddHud(screens);
        AddBizMan(screens);
        AddEconoView(screens);
        AddEmployees(screens);
        AddPersona(screens);
        AddActivity(screens);
        AddBizOps(screens);
        AddBuy(screens);
        AddDialog(screens);
        AddHudScreens(screens);
        AddWorld(screens);
        AddFlare(screens);
        AddLife(screens);
        AddMoney(screens);
        AddNews(screens);
        AddSettings(screens);
        AddStocks(screens);
        AddCompany(screens);
        AddConstruction(screens);
        AddEstate(screens);
        AddTech(screens);
        AddVentures(screens);
        AddWork(screens);
        AddDesigner(screens);
        AddMap(screens);
        AddOverlays(screens);
        AddPause(screens);
        AddSweep(screens);
        foreach (Screen screen in screens)
        {
            if (string.IsNullOrEmpty(screen.Backdrop))
            {
                // A screen judged on black hides its glare and how the street's signs read behind it.
                throw new InvalidOperationException(screen.Name + " has no backdrop: every screen stands over a picture of the game's world");
            }
        }
        return screens;
    }

    static partial void AddContacts(List<Screen> screens);
    static partial void AddKit(List<Screen> screens);
    static partial void AddPages(List<Screen> screens);
    static partial void AddShell(List<Screen> screens);
    static partial void AddPhone(List<Screen> screens);
    static partial void AddGrids(List<Screen> screens);
    static partial void AddHud(List<Screen> screens);
    static partial void AddBizMan(List<Screen> screens);
    static partial void AddEconoView(List<Screen> screens);
    static partial void AddEmployees(List<Screen> screens);
    static partial void AddPersona(List<Screen> screens);
    static partial void AddActivity(List<Screen> screens);
    static partial void AddBizOps(List<Screen> screens);
    static partial void AddBuy(List<Screen> screens);
    static partial void AddDialog(List<Screen> screens);
    static partial void AddHudScreens(List<Screen> screens);
    static partial void AddWorld(List<Screen> screens);
    static partial void AddFlare(List<Screen> screens);
    static partial void AddLife(List<Screen> screens);
    static partial void AddMoney(List<Screen> screens);
    static partial void AddNews(List<Screen> screens);
    static partial void AddSettings(List<Screen> screens);
    static partial void AddStocks(List<Screen> screens);
    static partial void AddCompany(List<Screen> screens);
    static partial void AddConstruction(List<Screen> screens);
    static partial void AddEstate(List<Screen> screens);
    static partial void AddTech(List<Screen> screens);
    static partial void AddVentures(List<Screen> screens);
    static partial void AddWork(List<Screen> screens);
    static partial void AddDesigner(List<Screen> screens);
    static partial void AddMap(List<Screen> screens);
    static partial void AddOverlays(List<Screen> screens);
    static partial void AddPause(List<Screen> screens);
    static partial void AddSweep(List<Screen> screens);

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
            RectTransform world = UiView.NewRect(cr, "World");
            UiView.Stretch(world);
            RectTransform host = UiView.NewRect(cr, "Host");
            UiView.Stretch(host);

            Backdrop(cr, shot.Screen.Backdrop);
            UiChrome.Bar = shot.Screen.Bar ?? (() => UiSamples.Bar(shot.Screen.App));
            var view = new UiView(host, "Screen") { Motion = UiMotionSetting.Off };
            var scene = new UiScene();
            UiFrame frame = UiFrame.FullMenu(canvasW / CanvasH, shot.Screen.AppName);
            frame.Icon = shot.Screen.Icon;
            frame.Pointer = !shot.Keys || shot.Screen.NoKeys;   // as UiHost lays a screen out while the mouse leads (always, for one that takes no key)
            IUiKeys keys = shot.Pad == UiPad.None ? (IUiKeys)UiDefaultKeys.Keyboard : UiDefaultKeys.XboxPad;
            shot.Screen.Lay(scene, frame, shot.Dark, keys);
            view.Ring = shot.Keys && !shot.Screen.NoKeys;
            view.Write(scene, false);
            if (shot.CaretOff)
            {
                view.SetCaret(false);   // as the host's blink hides it
            }
            int focus = shot.Screen.FocusCommand >= 0 ? scene.FindNode(shot.Screen.FocusCommand, shot.Screen.FocusArg) : UiFocusRules.First(scene);
            focus = focus >= 0 ? focus : UiFocusRules.First(scene);
            view.SetFocus(focus, false, 0f);
            if (shot.Hover)
            {
                // As the host takes a hover: only on the top layer, so an open question or call leaves the page under it still.
                int top = UiFocusRules.TopLayer(scene);
                int hover = shot.Screen.HoverCommand >= 0 ? scene.FindNode(shot.Screen.HoverCommand, shot.Screen.HoverArg) : -1;
                if (hover < 0 || scene.Nodes[hover].Layer != top)
                {
                    hover = -1;
                    for (int n = 0; n < scene.NodeCount && hover < 0; n++)
                    {
                        hover = scene.Nodes[n].Layer == top && n != focus ? n : -1;
                    }
                }
                view.SetHover(hover);
            }
            if (shot.Path && focus >= 0 && !shot.Screen.NoKeys)
            {
                if (shot.Screen.Tour)
                {
                    TourOf(host, scene, focus);
                }
                else
                {
                    FocusPath(host, scene, focus, shot.Screen.Walk);
                }
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

    // The game's world behind the screen: a street picture fitted to cover the canvas, cropped at its edges where the
    // aspects differ, so a panel's translucency is judged on a real scene and not on black.
    private static void Backdrop(RectTransform canvas, string file)
    {
        string path = string.IsNullOrEmpty(file) || string.IsNullOrEmpty(_backdrops) ? null : Path.Combine(_backdrops, file);
        if (path == null || !File.Exists(path))
        {
            return;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.LoadImage(File.ReadAllBytes(path));
        RectTransform world = (RectTransform)canvas.Find("World");
        var img = world.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        float canvasAspect = canvas.sizeDelta.x / canvas.sizeDelta.y, picture = tex.width / (float)tex.height;
        img.uvRect = canvasAspect > picture ? new Rect(0f, (1f - picture / canvasAspect) / 2f, 1f, picture / canvasAspect)
            : new Rect((1f - canvasAspect / picture) / 2f, 0f, canvasAspect / picture, 1f);
    }

    private static string _backdrops = "";

    // The pad's path from the first target, a screen's own walk of the d-pad: each stop numbered and joined by a line, as
    // the d-pad moves the one focus (UiFocusRules); a press that leads nowhere is left out.
    private static void FocusPath(RectTransform parent, UiScene scene, int start, Vector2Int[] moves)
    {
        var stops = new List<Vector2> { Centre(scene, start) };
        var came = new List<int> { -1 };
        int at = start;
        foreach (Vector2Int m in moves)
        {
            int next = UiFocusRules.Next(scene, at, m.x, m.y);
            if (next < 0)
            {
                continue;
            }
            at = next;
            came.Add(stops.Count - 1);
            stops.Add(Centre(scene, at));
        }
        DrawStops(parent, stops, came);
    }

    // The same drawing for a tour: stop i is joined to the stop it was pressed from.
    private static void DrawStops(RectTransform parent, List<Vector2> stops, List<int> came)
    {
        RectTransform layer = UiView.NewRect(parent, "FocusPath");
        UiView.Stretch(layer);
        var ink = new Color(1f, 0.85f, 0.2f, 1f);
        for (int i = 1; i < stops.Count; i++)
        {
            Vector2 a = stops[came[i]], b = stops[i];
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

    // The pad's walk of a Flare screen drawn as a player takes it: from the first target, right first, then down, then left and up, on to the first target not been to, every stop joined to the
    // one it was pressed from by one real press of the game's own links (FlareFace.Reach). The log says how many of the screen's targets the walk met.
    private static void TourOf(RectTransform parent, UiScene scene, int start)
    {
        var order = new int[scene.NodeCount + 1];
        var from = new int[scene.NodeCount + 1];
        int met = FlareFace.Reach(scene, start, order, from);
        var stops = new List<Vector2>(met);
        var came = new List<int>(met);
        var place = new int[scene.NodeCount];
        for (int i = 0; i < met; i++)
        {
            place[order[i]] = i;
            stops.Add(Centre(scene, order[i]));
            came.Add(from[i] < 0 ? -1 : place[from[i]]);
        }
        DrawStops(parent, stops, came);
        int top = 0;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            top += scene.Nodes[i].Layer == scene.Nodes[start].Layer && !scene.Nodes[i].Disabled ? 1 : 0;
        }
        Debug.Log(Tag + "pad tour: " + met + " stops by right, down, left and up from the first target, of " + top + " targets");
    }

    private static Vector2 Centre(UiScene scene, int node)
    {
        scene.NodeBox(node, out float x, out float y, out float w, out float h);
        return new Vector2((x + w / 2f) * UiTokens.CanvasPerPx, (y + h / 2f) * UiTokens.CanvasPerPx);
    }

    private static string English(string key, string english) => english;

    // A Windows font with Chinese and Korean, copied into the project by preview.ps1 (Assets/Preview/Fonts) and made a
    // dynamic font asset whose one atlas holds every glyph the shots draw (a second atlas page does not render offline); the system font by name where the copy is not there.
    private static TMP_FontAsset CjkFallback()
    {
        foreach (string path in new[] { "Assets/Preview/Fonts/msyh.ttc", "Assets/Preview/Fonts/malgun.ttf" })
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
            TMP_FontAsset asset = font != null
                ? TMP_FontAsset.CreateFontAsset(font, 36, 4, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 4096, 4096, AtlasPopulationMode.Dynamic, true)
                : null;
            if (asset != null)
            {
                // Its line box cut to the interface face's: YaHei's tall ascent and descent overflow a one-line box at the
                // kit's leading, and a truncated line then drew as nothing.
                TMP_FontAsset own = UiFonts.For(UiStyle.Small);
                if (own != null && own.faceInfo.pointSize > 0f)
                {
                    var face = asset.faceInfo;
                    float scale = (float)face.pointSize / own.faceInfo.pointSize;
                    face.ascentLine = own.faceInfo.ascentLine * scale;
                    face.descentLine = own.faceInfo.descentLine * scale;
                    face.lineHeight = own.faceInfo.lineHeight * scale;
                    asset.faceInfo = face;
                }
                Debug.Log("[UiPreview] Chinese and Korean fall back to " + path);
                return asset;
            }
        }
        Debug.Log("[UiPreview] no Windows CJK font copied: falling back to the system font by name");
        return TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular") ?? TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular");
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
