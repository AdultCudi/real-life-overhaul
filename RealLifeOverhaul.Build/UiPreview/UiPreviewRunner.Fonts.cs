using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The fonts' check against the text engine itself, the game's TextMeshPro in the game's Unity: no picture is made, every
/// answer is read from what the engine laid out. A report said all Korean text was gone (Ui/UiFallbackRules). First the
/// fault is made again as the kit laid every label before, then the same line through the kit as it is now, then the
/// kit's room with no copy of a font at all, then whole pages in Korean and in Chinese: no text may draw nothing, be cut
/// though the kit measured it to fit, be drawn wider than measured, or stand on a line taller than the face's. Before
/// the pages, a text as the mod's older screens make one, outside the view, with the room that keeps it
/// (Core/GameUi/TextRoom): dropped without it, whole with it, no word moved, and followed when its maker's callers
/// change its size, its box or its margins, or copy it. The fonts
/// are the game's own where they were extracted beside the project (Census/game_fonts.py --extract), else two of
/// Windows' that stand in for them. Run (preview.ps1 -Method UiPreviewRunner.FontsCheck):
///   Unity.exe -batchmode -quit -projectPath UiAssets/UnityProject -executeMethod UiPreviewRunner.FontsCheck -bundle path
/// Its lines begin "[UiPreview] fonts check:" and its exit code is 0 when every one passed.
/// </summary>
public static partial class UiPreviewRunner
{
    private const string FontsTag = Tag + "fonts check: ";
    private const string Korean = "새 게임을 시작합니다", Chinese = "开始新的游戏吧", Latin = "Start a new game";

    private static int _fontsRun, _fontsFailed;
    private static bool _gameFonts;      // the fallbacks are the game's own font files, so a width is the game's width
    private static float _faceLine;      // the face's line, em

    // What the engine made of one text.
    private struct Drew
    {
        public int Characters;   // visible characters drawn
        public int Fallback;     // of them, from another font than the text's own
        public int Plain;        // of those, from a font that is not one of the mod's copies
        public bool Cut;         // the engine ended the text early
        public float LineEm;     // its first line, ascender to descender
        public float Baseline;   // px from the box's top to the first line's baseline
        public float Width;      // px the first line advances
    }

    private sealed class Pages
    {
        public int Screens, NotLaid, Texts, WithFallback, Blank, CutShort, Wider, Taller, Plain;
        public string First;
    }

    private static void FontsSay(bool ok, string what)
    {
        _fontsRun++;
        _fontsFailed += ok ? 0 : 1;
        Debug.Log(FontsTag + what + ": " + (ok ? "PASS" : "FAIL"));
    }

    public static void FontsCheck()
    {
        try
        {
            string bundle = Argument("-bundle", Path.GetFullPath("../../Assets/Mods/RealLifeOverhaul/Bundles/reallife_ui"));
            int standIns = GameFallbacks();
            _gameFonts = AssetDatabase.LoadAssetAtPath<Font>("Assets/Preview/Fonts/NotoSansKR.otf") != null;
            UiFonts.LoadNow(bundle);
            if (!UiFonts.Own)
            {
                throw new InvalidOperationException("the font bundle did not load from " + bundle);
            }
            UiChrome.Status = () => UiSamples.Status;
            TMP_FontAsset face = UiFonts.For(UiStyle.Small);
            _faceLine = (face.faceInfo.ascentLine - face.faceInfo.descentLine) / face.faceInfo.pointSize;
            float tallest = 0f;
            foreach (TMP_FontAsset font in TMP_Settings.fallbackFontAssets)
            {
                tallest = Mathf.Max(tallest, (font.faceInfo.ascentLine - font.faceInfo.descentLine) / font.faceInfo.pointSize);
            }
            FontsSay(standIns >= 2 && tallest > _faceLine + 0.01f, standIns + " font files stand in for the game's fallbacks (" + (_gameFonts ? "the game's own" : "two of Windows'")
                                                                    + "), the tallest line " + Em(tallest) + " em against the face's " + Em(_faceLine));

            // The fault, by the engine itself: the faces as the bundle gives them, the fallbacks found by TextMeshPro's own general list.
            Drew korean = Bare(Korean, UiStyle.Small, true, false), chinese = Bare(Chinese, UiStyle.Small, true, false), latin = Bare(Latin, UiStyle.Small, true, false);
            Drew uncut = Bare(Korean, UiStyle.Small, false, false), body = Bare(Korean, UiStyle.Body, true, false);
            FontsSay(korean.Characters == 0 && chinese.Characters == 0, "as every label was laid (one line that may be cut, a box one line of the face tall), a Korean line draws "
                                                                        + korean.Characters + " of " + Visible(Korean) + " characters and a Chinese one " + chinese.Characters + " of " + Visible(Chinese)
                                                                        + ": the report's fault");
            FontsSay(latin.Characters == Visible(Latin) && uncut.Characters == Visible(Korean) && uncut.LineEm > _faceLine + 0.01f,
                "the same box draws a Latin line whole (" + latin.Characters + "), and the Korean line whole when it may not be cut (" + uncut.Characters + ", its line " + Em(uncut.LineEm)
                + " em): no glyph was missing, the line was dropped for its height");
            FontsSay(body.Characters == Visible(Korean) || tallest > UiTextRules.LineHeight(UiStyle.Body) / UiTokens.Type(UiStyle.Body).Size,
                "in a body style, whose leading leaves a line " + Em(UiTextRules.LineHeight(UiStyle.Body) / UiTokens.Type(UiStyle.Body).Size) + " em, the Korean line was drawn ("
                + body.Characters + "): why some text was still there");

            // The kit as it is now: the mod's copies of the fallbacks, their line the face's, in the language's order.
            foreach (string code in new[] { "ko", "zh-cn" })
            {
                string words = code == "ko" ? Korean : Chinese;
                Chain(code);
                string sample = UiFonts.FontsSample(UiFontProbe.Most);
                int drawn = UiFontProbe.Drawn(sample);
                string line = UiFonts.FontsLine(sample.Length, drawn, 0);
                Debug.Log(Tag + line);
                FontsSay(line.Contains(", 0 left: PASS.") && drawn == sample.Length && sample.Length == UiFontProbe.Most, "the line the game writes for " + code + " passes, its probe drawing "
                                                                                                                      + drawn + " of " + sample.Length + " characters");
                Drew copies = Bare(words, UiStyle.Small, true, false);
                FontsSay(copies.Characters == Visible(words) && copies.Fallback == Visible(words) && copies.Plain == 0 && Mathf.Abs(copies.LineEm - _faceLine) < 0.002f,
                    code + ": with the mod's copies even the bare box draws the line whole (" + copies.Characters + " characters, " + copies.Plain + " by a font not the mod's), on a line "
                    + Em(copies.LineEm) + " em tall, the face's");
                Drew top = ViewLine(words, UiStyle.Small, UiFlags.Truncate | UiFlags.Label), latinTop = ViewLine(Latin, UiStyle.Small, UiFlags.Truncate | UiFlags.Label);
                Drew middle = ViewLine(words, UiStyle.Button, UiFlags.Truncate | UiFlags.Label | UiFlags.Middle), latinMiddle = ViewLine(Latin, UiStyle.Button, UiFlags.Truncate | UiFlags.Label | UiFlags.Middle);
                FontsSay(top.Characters == Visible(words) && middle.Characters == Visible(words) && Mathf.Abs(top.Baseline - latinTop.Baseline) < 0.05f && Mathf.Abs(middle.Baseline - latinMiddle.Baseline) < 0.05f,
                    code + ": through the view the line is drawn whole and sits on the baseline a Latin line sits on, laid from the top (" + top.Baseline.ToString("0.00", CultureInfo.InvariantCulture)
                    + " against " + latinTop.Baseline.ToString("0.00", CultureInfo.InvariantCulture) + " px) and centred (" + middle.Baseline.ToString("0.00", CultureInfo.InvariantCulture) + " against "
                    + latinMiddle.Baseline.ToString("0.00", CultureInfo.InvariantCulture) + " px)");
                float measured = UiTextRules.Width(words, UiStyle.Small);
                FontsSay(!_gameFonts || (top.Width <= measured + 0.05f && top.Width >= measured - 1.5f),
                    code + ": the line is drawn " + top.Width.ToString("0.00", CultureInfo.InvariantCulture) + " px wide, measured " + measured.ToString("0.00", CultureInfo.InvariantCulture)
                    + (_gameFonts ? string.Empty : " (not held: a Windows font stands in, whose letters are not the game's width)"));
            }

            // The room moves no word: a Latin line through the view stands where the bare box had it, from the top and centred.
            Drew bareTop = Bare(Latin, UiStyle.Small, true, false), viewTop = ViewLine(Latin, UiStyle.Small, UiFlags.Truncate | UiFlags.Label);
            Drew bareMiddle = Bare(Latin, UiStyle.Button, true, true), viewMiddle = ViewLine(Latin, UiStyle.Button, UiFlags.Truncate | UiFlags.Label | UiFlags.Middle);
            FontsSay(Mathf.Abs(bareTop.Baseline - viewTop.Baseline) < 0.01f && Mathf.Abs(bareMiddle.Baseline - viewMiddle.Baseline) < 0.01f && viewTop.Characters == Visible(Latin),
                "the room moves no word: a Latin line's baseline is " + viewTop.Baseline.ToString("0.000", CultureInfo.InvariantCulture) + " px under its box's top with it and "
                + bareTop.Baseline.ToString("0.000", CultureInfo.InvariantCulture) + " without, and centred " + viewMiddle.Baseline.ToString("0.000", CultureInfo.InvariantCulture) + " against "
                + bareMiddle.Baseline.ToString("0.000", CultureInfo.InvariantCulture));

            // The room alone: the faces left with no fallback of their own, as if no copy could be made, so TextMeshPro's general list answers.
            foreach (UiStyle style in new[] { UiStyle.Small, UiStyle.Title, UiStyle.Display })
            {
                TMP_FontAsset weight = UiFonts.For(style);
                weight.fallbackFontAssetTable.Clear();
                weight.ReadFontAssetDefinition();
            }
            Drew bareAgain = Bare(Korean, UiStyle.Small, true, false), viewAlone = ViewLine(Korean, UiStyle.Small, UiFlags.Truncate | UiFlags.Label);
            Drew wrapped = ViewLine(Korean + " " + Korean + " " + Korean, UiStyle.Small, UiFlags.Truncate | UiFlags.Wrap | UiFlags.Label, 120f, 2);
            FontsSay(bareAgain.Characters == 0 && viewAlone.Characters == Visible(Korean) && viewAlone.LineEm > _faceLine + 0.01f && wrapped.Characters > 0,
                "with no copy at all the bare box drops the Korean line again (" + bareAgain.Characters + ") and the kit's own text still draws it whole (" + viewAlone.Characters + " of " + Visible(Korean)
                + ", on a line " + Em(viewAlone.LineEm) + " em tall), and a wrapped block its first line (" + wrapped.Characters + " characters): the room holds whatever font answers");

            // The mod's older screens, outside the view, while the faces still have no fallback of their own.
            FontsRoom(tallest);

            // Whole pages, in Korean and in Chinese, with the language's characters given their fonts first as the game does.
            FontsPages("ko", KoreanPages());
            var chinesePages = new List<Screen>();
            foreach (Screen screen in Screens())
            {
                if (screen.Name.EndsWith("_zh", StringComparison.Ordinal) || screen.Name.Contains("_zh_"))
                {
                    chinesePages.Add(screen);
                }
            }
            FontsPages("zh-cn", chinesePages);

            Debug.Log(FontsTag + (_fontsFailed == 0 ? "ALL " + _fontsRun + " PASSED" : _fontsFailed + " of " + _fontsRun + " FAILED"));
            EditorApplication.Exit(_fontsFailed == 0 ? 0 : 1);
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + e);
            EditorApplication.Exit(1);
        }
    }

    private static string Em(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static int Visible(string words)
    {
        int n = 0;
        foreach (char c in words)
        {
            n += char.IsWhiteSpace(c) ? 0 : 1;
        }
        return n;
    }

    // The chain put on the faces for a language, and the characters of the mod's texts in it given their fonts, as the game's director does.
    private static void Chain(string code)
    {
        var seen = new bool[65536];
        string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Locales", code + ".json"));
        UiFallbackRules.Mark(File.ReadAllText(file), seen);
        char[] outside = UiFallbackRules.Outside(seen, out int marked);
        UiFonts.Language("en");                       // another language first, so the one asked for is ordered and checked afresh
        UiFonts.FallbacksStep(int.MaxValue);
        UiFonts.Language(code);
        UiFonts.Characters(code, marked, outside, 0, new char[0]);
        for (int i = 0; i < 64 && !UiFonts.FallbacksStep(int.MaxValue); i++)
        {
        }
    }

    // A text bound as the view binds a label, in a box one line of the face tall and nothing more: how every label stood before.
    private static Drew Bare(string words, UiStyle style, bool mayCut, bool middle)
    {
        var root = new GameObject("Bare", typeof(RectTransform));
        try
        {
            root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform r = UiView.NewRect(root.transform, "Text");
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            UiType type = UiTokens.Type(style);
            float scale = UiTokens.CanvasPerPx;
            t.raycastTarget = false;
            t.richText = false;
            t.enableAutoSizing = false;
            t.margin = Vector4.zero;
            t.isOrthographic = true;
            t.extraPadding = false;
            t.font = UiFonts.For(style);
            t.fontSize = type.Size * scale;
            t.characterSpacing = type.Tracking * 100f;
            t.fontStyle = type.Case == UiCase.Upper ? FontStyles.UpperCase : FontStyles.Normal;
            t.alignment = middle ? TextAlignmentOptions.CaplineLeft : TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = mayCut ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            r.sizeDelta = new Vector2(600f * scale, UiTextRules.LineHeight(style) * scale);
            t.text = words;
            t.ComputeMarginSize();
            t.ForceMeshUpdate(true, true);
            return Read(t, scale, 0f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // One text through the kit's own view, as a screen draws it: laid by UiDraw, placed with its room and bound by UiView.
    private static Drew ViewLine(string words, UiStyle style, UiFlags flags, float width = 600f, int lines = 1)
    {
        const float Top = 80f;   // px: the item's box well inside the scene, so room over it stays in it
        var root = new GameObject("ViewLine", typeof(RectTransform));
        try
        {
            root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var view = new UiView(root.transform, "Line") { Motion = UiMotionSetting.Off };
            var scene = new UiScene();
            scene.Begin("fonts check", 800f, 400f, true);
            int item = UiDraw.Text(scene, UiAt.Screen, 0f, Top, width, style, UiRole.Text, words, flags, lines);
            view.Write(scene, false);
            TMP_Text t = view.TextOf(item);
            t.ComputeMarginSize();
            t.ForceMeshUpdate(true, true);
            return Read(t, view.Scale, -t.rectTransform.anchoredPosition.y / view.Scale - Top);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // A text as the mod's older screens make one (Flare's own kit, the City Ledger): the engine may cut it, and its font has
    // no fallback of its own, so TextMeshPro's general list answers with its tall line.
    private static TextMeshProUGUI Older(Transform parent, string words, float size, float box, TextAlignmentOptions align)
    {
        RectTransform r = UiView.NewRect(parent, "Text");
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = UiFonts.For(UiStyle.Small);
        t.fontSize = size;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.margin = Vector4.zero;
        r.sizeDelta = new Vector2(600f, box);
        t.text = words;
        return t;
    }

    private static Drew Laid(TMP_Text t)
    {
        t.ComputeMarginSize();
        t.ForceMeshUpdate(true, true);
        return Read(t, 1f, 0f);
    }

    // The room of a text outside the view, by the engine itself: the box holds a line of the text's own font and not one of
    // the tallest fallback, as the older screens' boxes do.
    private static void FontsRoom(float tallest)
    {
        const float Size = 24f;
        float box = (_faceLine + tallest) / 2f * Size;
        var root = new GameObject("Older", typeof(RectTransform));
        try
        {
            root.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            TextMeshProUGUI korean = Older(root.transform, Korean, Size, box, TextAlignmentOptions.TopLeft);
            Drew bare = Laid(korean);
            TextRoom.On(korean);
            Drew roomed = Laid(korean);
            FontsSay(bare.Characters == 0 && roomed.Characters == Visible(Korean) && korean.margin.w < 0f && korean.margin.y == 0f,
                "outside the view, a 24 px text that may be cut, in a box of " + box.ToString("0.0", CultureInfo.InvariantCulture) + " px, draws " + bare.Characters + " of " + Visible(Korean)
                + " Korean characters as the mod's older screens made it, and " + roomed.Characters + " with its room (" + (-korean.margin.w).ToString("0.0", CultureInfo.InvariantCulture) + " px under it)");

            float moved = 0f;
            int given = 0, whole = 0;
            var ways = new[] { TextAlignmentOptions.TopLeft, TextAlignmentOptions.Left, TextAlignmentOptions.BottomLeft, TextAlignmentOptions.MidlineLeft, TextAlignmentOptions.BaselineLeft };
            foreach (TextAlignmentOptions way in ways)
            {
                TextMeshProUGUI t = Older(root.transform, Latin, Size, box, way);
                Drew before = Laid(t);
                TextRoom.On(t);
                Drew after = Laid(t);
                moved = Mathf.Max(moved, Mathf.Abs(after.Baseline - before.Baseline) + (before.Characters == Visible(Latin) && after.Characters == Visible(Latin) ? 0f : 100f));
                given += t.margin != Vector4.zero ? 1 : 0;
                t.text = Korean;
                whole += Laid(t).Characters == Visible(Korean) ? 1 : 0;
            }
            FontsSay(moved < 0.01f && given == ways.Length && whole == ways.Length,
                "the room moves no word there: a Latin line laid from the top, the middle, the bottom, its own middle and its baseline stands within "
                + moved.ToString("0.000", CultureInfo.InvariantCulture) + " px of where it stood, and a Korean line is drawn whole each way (" + whole + " of " + ways.Length + ")");

            // What a maker's callers do after the text is made: a size, a taller box, margins of their own, the short box again.
            TextMeshProUGUI late = Older(root.transform, Korean, Size / 2f, box, TextAlignmentOptions.TopLeft);
            TextRoom.On(late);
            bool none = late.margin == Vector4.zero && Laid(late).Characters == Visible(Korean);
            late.fontSize = Size;
            bool grown = late.margin.w < 0f && Laid(late).Characters == Visible(Korean);
            late.rectTransform.sizeDelta = new Vector2(600f, 3f * Size);
            bool tall = late.margin == Vector4.zero;
            var own = new Vector4(3f, 2f, 3f, 2f);
            late.margin = own;
            bool kept = late.margin == own;
            late.rectTransform.sizeDelta = new Vector2(600f, box);
            bool under = late.margin.x == own.x && late.margin.y == own.y && late.margin.z == own.z && late.margin.w < 0f && Laid(late).Characters == Visible(Korean);
            FontsSay(none && grown && tall && kept && under,
                "the room follows what is changed after it is put on: none while the box holds the line (" + none + "), room when the size grows (" + grown + "), none again in a taller box (" + tall
                + "), the maker's own margins kept (" + kept + ") and the room taken off them in the short box (" + under + ")");

            // A copy of the text's object, as a row made from a pattern: drawn whole, and in a taller box it has the maker's
            // own margins again, so it could tell them from its room.
            GameObject twin = UnityEngine.Object.Instantiate(late.gameObject, root.transform, false);
            var copy = twin.GetComponent<TextMeshProUGUI>();
            bool drawn = Laid(copy).Characters == Visible(Korean) && copy.margin == late.margin;
            copy.rectTransform.sizeDelta = new Vector2(600f, 3f * Size);
            bool knew = copy.margin == own && late.margin.w < 0f;
            FontsSay(drawn && knew, "a copy of such a text is drawn whole (" + drawn + ") and knows its own margins from its room (" + knew + ")");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // What the engine laid out: its visible characters and their fonts, its first line, and its baseline under the layout's box
    // (rectAbove: px the text's rect begins over that box, negative when it begins above it).
    private static Drew Read(TMP_Text t, float scale, float rectAbove)
    {
        var d = new Drew { Cut = t.isTextTruncated };
        TMP_TextInfo info = t.textInfo;
        bool first = true;
        for (int i = 0; i < info.characterCount && i < info.characterInfo.Length; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible)
            {
                continue;
            }
            d.Characters++;
            if (first)
            {
                first = false;
                d.Baseline = rectAbove - c.baseLine / scale;
            }
            if (c.fontAsset != t.font)
            {
                d.Fallback++;
                d.Plain += c.fontAsset != null && c.fontAsset.name.StartsWith("RLO ", StringComparison.Ordinal) ? 0 : 1;
            }
        }
        if (d.Characters > 0 && info.lineCount > 0 && t.fontSize > 0f)
        {
            d.LineEm = (info.lineInfo[0].ascender - info.lineInfo[0].descender) / t.fontSize;
            d.Width = info.lineInfo[0].maxAdvance / scale;
        }
        return d;
    }

    // The pages the preview has in Chinese, laid in Korean: the conversations, the scenes, the calls and the Company app.
    private static List<Screen> KoreanPages()
    {
        var pages = new List<Screen>();
        foreach (string state in new[] { "bank_loan", "wholesale", "furniture", "desk_full", "street_said" })
        {
            pages.Add(DialogScreenOf(state, "ko"));
        }
        foreach (string state in new[] { "court_plea", "court_tax", "interview_round", "pitch_question", "venture_pitch", "venture_player" })
        {
            pages.Add(SceneScreenOf(state, "ko"));
        }
        pages.Add(SpokenScreenOf("fred_more", "ko"));
        pages.Add(CallScreenOf("loan", "ko"));
        pages.Add(CallScreenOf("mood", "ko"));
        for (int i = 0; i < UiSamples.CompanyStates.Length; i++)
        {
            pages.Add(CompanyScreenOf(i, "ko"));
        }
        return pages;
    }

    private static void FontsPages(string code, List<Screen> screens)
    {
        Chain(code);
        var count = new Pages();
        foreach (Screen screen in screens)
        {
            try
            {
                FontsPage(screen, count);
            }
            catch (Exception e)
            {
                count.NotLaid++;
                Debug.Log(Tag + "fonts check: " + screen.Name + " was not laid here (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }
        bool widths = !_gameFonts || count.Wider == 0;
        FontsSay(count.Screens >= 10 && count.Texts > 300 && count.WithFallback > 200 && count.Blank == 0 && count.CutShort == 0 && count.Taller == 0 && count.Plain == 0 && widths,
            code + " pages: " + count.Screens + " screens (" + count.NotLaid + " not laid here), " + count.Texts + " texts, " + count.WithFallback + " with characters from a fallback: "
            + count.Blank + " drew nothing, " + count.CutShort + " cut though the kit measured them to fit, " + count.Wider + " drawn wider than measured"
            + (_gameFonts ? string.Empty : " (not held: a Windows font stands in)") + ", " + count.Taller + " on a line taller than the face's, " + count.Plain
            + " by a font that is not the mod's copy" + (count.First != null ? "; the first: " + count.First : string.Empty));
    }

    // One page laid and written as RenderShot does, every text of it asked what the engine made of it. No camera, no picture.
    private static void FontsPage(Screen screen, Pages count)
    {
        const float CanvasW = 3840f;
        var root = new GameObject("FontsPage");
        try
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform, false);
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var cr = (RectTransform)canvasGo.transform;
            cr.pivot = new Vector2(0f, 1f);
            cr.sizeDelta = new Vector2(CanvasW, CanvasH);
            RectTransform host = UiView.NewRect(cr, "Host");
            UiView.Stretch(host);
            UiSamples.SampleWords = BarLanguageOf(screen.Name);
            UiChrome.Bar = screen.Bar ?? (() => UiSamples.Bar(screen.App));
            var view = new UiView(host, "Screen") { Motion = UiMotionSetting.Off };
            var scene = new UiScene();
            UiFrame frame = UiFrame.FullMenu(CanvasW / CanvasH, screen.AppName);
            frame.Icon = screen.Icon;
            frame.Pointer = true;
            screen.Lay(scene, frame, true, UiDefaultKeys.Keyboard);
            view.Write(scene, false);
            Canvas.ForceUpdateCanvases();
            count.Screens++;
            for (int i = 0; i < scene.ItemCount; i++)
            {
                UiItem it = scene.Items[i];
                TMP_Text t = it.Kind == UiKind.Text && !string.IsNullOrWhiteSpace(it.Text) ? view.TextOf(i) : null;
                if (t == null)
                {
                    continue;
                }
                t.ComputeMarginSize();
                t.ForceMeshUpdate(true, true);
                Drew d = Read(t, view.Scale, 0f);
                bool wrap = (it.Flags & UiFlags.Wrap) != 0, mayCut = (it.Flags & UiFlags.Truncate) != 0;
                count.Texts++;
                count.WithFallback += d.Fallback > 0 ? 1 : 0;
                count.Plain += d.Plain > 0 ? 1 : 0;
                string fault = null;
                if (d.Characters == 0)
                {
                    count.Blank++;
                    fault = "drew nothing";
                }
                else if (!wrap && mayCut && d.Cut && UiTextRules.Fits(it.Text, it.Style, it.W))
                {
                    count.CutShort++;
                    fault = "was cut though the kit measured it to fit";
                }
                else if (!wrap && !d.Cut && d.Width > UiTextRules.Width(it.Text, it.Style) + 0.6f)
                {
                    count.Wider++;
                    fault = "is drawn " + d.Width.ToString("0.0", CultureInfo.InvariantCulture) + " px wide, measured " + UiTextRules.Width(it.Text, it.Style).ToString("0.0", CultureInfo.InvariantCulture);
                }
                else if (d.LineEm > _faceLine + 0.01f)
                {
                    count.Taller++;
                    fault = "stands on a line " + Em(d.LineEm) + " em tall";
                }
                if (fault != null && count.First == null)
                {
                    count.First = screen.Name + ", \"" + it.Text + "\" " + fault;
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
