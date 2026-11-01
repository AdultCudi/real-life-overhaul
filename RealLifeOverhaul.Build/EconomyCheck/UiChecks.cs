using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Ui;

// The interface kit (Scripts/Ui; docs/standards/ui.md; not-in-repo/design/DESIGN_SPEC.md): the tokens (every role in both
// themes, WCAG AA for every text on every surface it is drawn on and for every avatar's initials, the 4 px grid, the type
// scale's smallest size, motion in game timing), the font metrics against the pinned fonts, text measurement and wrapping,
// the motion sums, focus navigation; then every registered screen (UiScreens) laid out at every frame the game's full
// menu can give it, in all eleven languages and both looks, against every layout rule (nothing overlaps, nothing spills,
// the grid, labels fit whole, targets big enough and reached by the d-pad, contrast, one primary action, a prompt bar);
// each rule seen failing once on a broken scene; the kit's sources; its texts; and what a layout costs.
internal static partial class Program
{
    private static readonly float[] UiAspects = { 16f / 9f, 16f / 10f, 4f / 3f, 21f / 9f };   // the screens the game's canvas expands for

    private static void UiKit()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        UiChrome.Status = () => UiSamples.Status;   // the masthead's money, day and time, as the game's bar writes them
        UiTokenChecks();
        UiFontChecks();
        UiTextChecks();
        UiMotionChecks();
        UiFocusChecks();
        UiScreenChecks();
        UiRulesFailOnce();
        UiSourceChecks();
        UiTextKeys();
        UiTimings();
        Console.WriteLine("  Interface kit: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- tokens

    private static void UiTokenChecks()
    {
        Check(UiTokens.ThemeLength(true) == (int)UiRole.Count && UiTokens.ThemeLength(false) == (int)UiRole.Count, "ui tokens: both themes give a colour for every role");
        bool all = true;
        for (int r = 1; r < (int)UiRole.Count; r++)
        {
            all &= UiTokens.Colour((UiRole)r, true) != 0 && UiTokens.Colour((UiRole)r, false) != 0;
        }
        Check(all, "ui tokens: no role is left without a colour in either theme");
        Check(UiTokens.Colour(UiRole.Bg, true) == 0x12110EFF && UiTokens.Colour(UiRole.Text, true) == 0xF1EBDDFF && UiTokens.Colour(UiRole.Accent, true) == 0xF2A58AFF
              && UiTokens.Colour(UiRole.Bg, false) == 0xFFF1E5FF && UiTokens.Colour(UiRole.Text, false) == 0x1A1712FF && UiTokens.Colour(UiRole.Accent, false) == 0x990F3DFF,
            "ui tokens: the City Ledger the owner chose (1 Nov): ink-black with paper-white words and the markets paper's salmon; the salmon paper with its ink and claret");
        Check(UiTokens.RadiusPanel == 0f && UiTokens.RadiusLarge <= 2f && UiTokens.FrameRule >= 2f, "ui tokens: print is square, a framed panel wears its column rule");
        foreach (bool dark in new[] { true, false })
        {
            string theme = dark ? "dark" : "light";
            UiColour panel = UiColour.Of(UiRole.Panel, dark);
            UiRole[] surfaces = { UiRole.Bg, UiRole.Panel, UiRole.Header, UiRole.Row, UiRole.Field, UiRole.Bubble, UiRole.Hover, UiRole.RowSel };
            UiRole[] inks = { UiRole.Text, UiRole.Text2, UiRole.Muted, UiRole.AccentText };
            foreach (UiRole ink in inks)
            {
                foreach (UiRole surface in surfaces)
                {
                    float c = UiContrast(ink, surface, dark);
                    Check(c >= UiCheckRules.NormalContrast, "ui tokens (" + theme + "): " + ink + " on " + surface + " stands " + c.ToString("0.00") + ":1, WCAG AA wants 4.5");
                }
            }
            Check(UiContrast(UiRole.OnAccent, UiRole.Accent, dark) >= UiCheckRules.NormalContrast, "ui tokens (" + theme + "): words on the accent reach 4.5:1 (" + UiContrast(UiRole.OnAccent, UiRole.Accent, dark).ToString("0.00") + ")");
            Check(UiContrast(UiRole.NoticeText, UiRole.Notice, dark) >= UiCheckRules.NormalContrast, "ui tokens (" + theme + "): a quiet badge's words reach 4.5:1");
            foreach (UiRole[] pair in new[] { new[] { UiRole.OkText, UiRole.OkSoft }, new[] { UiRole.WarnText, UiRole.WarnSoft }, new[] { UiRole.DangerText, UiRole.DangerSoft }, new[] { UiRole.AccentText, UiRole.AccentSoft } })
            {
                UiColour soft = UiColour.Of(pair[1], dark).Over(panel);
                float c = UiColour.Contrast(UiColour.Of(pair[0], dark).Over(soft), soft);
                Check(c >= UiCheckRules.NormalContrast, "ui tokens (" + theme + "): " + pair[0] + " on its soft fill reaches 4.5:1 (" + c.ToString("0.00") + ")");
            }
            float worst = 21f;
            int worstHue = 0;
            for (int hue = 0; hue < 360; hue++)
            {
                float c = UiColour.Contrast(UiColour.AvatarInk(hue, dark), UiColour.AvatarFill(hue, dark));
                if (c < worst)
                {
                    worst = c;
                    worstHue = hue;
                }
            }
            Check(worst >= UiCheckRules.NormalContrast, "ui tokens (" + theme + "): every avatar's initials reach 4.5:1 on their circle, at every hue (worst " + worst.ToString("0.00") + " at " + worstHue + ")");
            foreach (UiRole surface in new[] { UiRole.Bg, UiRole.Panel, UiRole.Row })
            {
                float c = UiContrast(UiRole.Focus, surface, dark);
                Check(c >= UiCheckRules.LargeContrast, "ui tokens (" + theme + "): the focus ring stands out 3:1 from " + surface + " (" + c.ToString("0.00") + ")");
            }
            Check(UiColour.Contrast(UiColour.Of(UiRole.PromptInk, dark), UiColour.Of(UiRole.Bg, dark)) >= UiCheckRules.NormalContrast,
                "ui tokens (" + theme + "): the prompts' words stand out on the flat band the kit paints");
        }
        bool grid = true, rising = true;
        for (int i = 0; i < UiTokens.Steps.Length; i++)
        {
            grid &= Math.Abs(UiTokens.Steps[i] % UiTokens.Grid) < 0.001f;
            rising &= i == 0 || UiTokens.Steps[i] > UiTokens.Steps[i - 1];
        }
        Check(grid && rising, "ui tokens: every spacing step is on the 4 px grid, smallest first");
        Check(UiTokens.Row % UiTokens.Grid == 0f && UiTokens.Control % UiTokens.Grid == 0f && UiTokens.ControlSmall % UiTokens.Grid == 0f && UiTokens.Chip % UiTokens.Grid == 0f
              && UiTokens.AvatarRow % UiTokens.Grid == 0f && UiTokens.AvatarHero % UiTokens.Grid == 0f && UiTokens.KeyCap % UiTokens.Grid == 0f && UiTokens.PairRow % UiTokens.Grid == 0f,
            "ui tokens: every control's size is on the grid");
        Check(UiTokens.Control >= UiTokens.MinTarget && UiTokens.ControlSmall >= UiTokens.MinTarget && UiTokens.Chip >= UiTokens.MinTarget && UiTokens.Row >= UiTokens.MinTarget
              && UiTokens.MinTarget >= 24f, "ui tokens: every control is at least the least target, which is over WCAG 2.2 AA's 24 px");
        bool sizes = true, weights = true;
        for (int i = 0; i < (int)UiStyle.Count; i++)
        {
            UiType t = UiTokens.Type((UiStyle)i);
            sizes &= t.Size >= UiTokens.MinText && t.Leading >= 1f;
            UiFace held = UiFontMetrics.Face(t.Family, t.Weight);
            weights &= held != null && held.Weight == t.Weight;   // the very cut the bundle holds, never a neighbour standing in
        }
        Check(sizes && UiTokens.MinText >= 15f, "ui tokens: every style is at least the smallest text (15 px at 1080p), readable at play distance");
        Check(weights, "ui tokens: every style is a weight the bundle holds");
        Check(UiCheckRules.SlowMotion(null) == 0, "ui tokens: feedback, focus, selection and arrivals take 60 to 240 ms, a fill at most 600");
        Check(UiTokens.Stagger <= 0.03f && UiTokens.Slide <= UiTokens.S3 && UiTokens.StaggerMost <= 10f, "ui tokens: a list's stagger and a slide are short, as games move");
        bool calm = true;
        for (int k = 0; k < (int)UiMotionKind.Count; k++)
        {
            var kind = (UiMotionKind)k;
            calm &= UiTokens.Duration(kind, UiMotionSetting.Calm) > UiTokens.Duration(kind, UiMotionSetting.Full) && UiTokens.Duration(kind, UiMotionSetting.Off) == 0f;
        }
        Check(calm && UiTokens.Distance(UiMotionSetting.Calm) < UiTokens.Distance(UiMotionSetting.Full) && UiTokens.Distance(UiMotionSetting.Off) == 0f,
            "ui tokens: Calm is slower and moves half as far, Off moves nothing");
    }

    private static float UiContrast(UiRole ink, UiRole surface, bool dark)
    {
        UiColour under = UiColour.Of(surface, dark).Over(UiColour.Of(UiRole.Bg, dark));
        return UiColour.Contrast(UiColour.Of(ink, dark).Over(under), under);
    }

    // ---------------------------------------------------------------- fonts

    private static void UiFontChecks()
    {
        Check(UiFontMetrics.FaceCount == 7, "ui fonts: seven faces, Source Serif 4's display cut at 600 and 700, its text cut at 500 and 700, Libre Franklin at 600, 700 and 800");
        bool faces = true;
        foreach ((UiFamily family, int weight) in new[] { (UiFamily.SerifDisplay, 600), (UiFamily.SerifDisplay, 700), (UiFamily.Serif, 500), (UiFamily.Serif, 700),
                     (UiFamily.Franklin, 600), (UiFamily.Franklin, 700), (UiFamily.Franklin, 800) })
        {
            UiFace f = UiFontMetrics.Face(family, weight);
            faces &= f != null && f.Family == family && f.Weight == weight && f.UnitsPerEm > 0 && f.Ascender > 0 && f.Descender < 0 && f.LineEm > 1f && f.LineEm < 1.6f;
        }
        Check(faces, "ui fonts: each face has its own metrics and a line of 1 to 1.6 em");
        bool scripts = true;
        foreach (UiFamily family in new[] { UiFamily.SerifDisplay, UiFamily.Serif, UiFamily.Franklin })
        {
            UiFace f = UiFontMetrics.Face(family, 700);
            scripts &= f.Advance('A') > 0 && f.Advance('ł') > 0 && f.Advance('ğ') > 0 && f.Advance('İ') > 0 && f.Advance('ç') > 0 && f.Advance('ж') > 0 && f.Advance('\u2212') > 0;
        }
        Check(scripts, "ui fonts: both families draw Latin, Latin Extended (Polish, Turkish, Portuguese), Cyrillic and the minus sign");
        string fonts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\UiAssets"));
        string script = File.Exists(System.IO.Path.Combine(fonts, "scripts", "fonts.py")) ? File.ReadAllText(System.IO.Path.Combine(fonts, "scripts", "fonts.py")) : "";
        bool pinned = script.Length > 0;
        foreach (string file in new[] { "SourceSerif4[opsz,wght].ttf", "LibreFranklin[wght].ttf" })
        {
            string path = System.IO.Path.Combine(fonts, "fonts", file);
            string hash = File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() : "missing";
            pinned &= script.Contains(hash, StringComparison.Ordinal);
        }
        Check(pinned, "ui fonts: the fonts in UiAssets/fonts are the files fonts.py pins by their SHA-256 (a changed font regenerates the metrics)");
        string data = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\UiFontMetrics.Data.cs"));
        Check(File.Exists(data) && File.ReadAllText(data).StartsWith("// Generated by UiAssets/scripts/fonts.py", StringComparison.Ordinal), "ui fonts: the metrics table is the one fonts.py writes");
        string contents = System.IO.Path.Combine(fonts, "contents", "reallife_ui.json");
        string listed = File.Exists(contents) ? File.ReadAllText(contents) : "";
        string bundle = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Bundles\reallife_ui"));
        Check(File.Exists(bundle) && listed.Contains("\"faces\"") && listed.Contains("SerifDisplay-Bold SDF") && listed.Contains("Serif-Medium SDF") && listed.Contains("Franklin-ExtraBold SDF"),
            "ui fonts: the bundle is built and its contents list holds every face");
    }

    // ---------------------------------------------------------------- text

    private static void UiTextChecks()
    {
        float ab = UiTextRules.Width("Contacts list", UiStyle.Body), a = UiTextRules.Width("Contacts", UiStyle.Body), b = UiTextRules.Width(" list", UiStyle.Body);
        Check(Math.Abs(ab - a - b) < 0.01f && a > 0f, "ui text: widths add up along a line");
        Check(UiTextRules.Width("groups", UiStyle.SectionHeader) > UiTextRules.Width("groups", UiStyle.Small), "ui text: capitals with tracking take more room than the same words in lower case");
        Check(Math.Abs(UiTextRules.Width("联系人", UiStyle.Body) - 3f * UiTokens.Type(UiStyle.Body).Size) < 0.01f, "ui text: Chinese characters are squares of the text's size");
        Check(Math.Abs(UiTextRules.Width("연락처", UiStyle.Body) - 3f * UiTokens.Type(UiStyle.Body).Size) < 0.01f, "ui text: Korean syllables are squares too");
        Check(UiTextRules.Width("Контакты", UiStyle.Title) > 0f, "ui text: Russian in a Sora style is measured in Manrope, as the bundle falls back");
        float one = UiTextRules.LineHeight(UiStyle.Body);
        Check(UiTextRules.Lines("alpha beta gamma", UiStyle.Body, 1000f, 0, out float wide) == 1 && wide > 0f, "ui text: a short line stays on one line");
        Check(UiTextRules.Lines("alpha beta gamma", UiStyle.Body, UiTextRules.Width("alpha beta", UiStyle.Body) + 1f, 0, out float _) == 2, "ui text: words wrap at the space before the one that does not fit");
        Check(UiTextRules.Lines("alpha\nbeta", UiStyle.Body, 1000f, 0, out float _) == 2, "ui text: a line break starts a line");
        Check(UiTextRules.Lines("Supercalifragilistic", UiStyle.Body, UiTextRules.Width("Super", UiStyle.Body), 0, out float _) >= 3, "ui text: a word wider than the line breaks inside itself");
        Check(UiTextRules.Lines("联系人联系人", UiStyle.Body, 3.5f * UiTokens.Type(UiStyle.Body).Size, 0, out float _) == 2, "ui text: Chinese wraps between any two characters");
        Check(UiTextRules.Lines("alpha beta gamma delta", UiStyle.Body, 60f, 2, out float _) == 2, "ui text: a wrapped text stops at its most lines");
        Check(Math.Abs(UiTextRules.WrappedHeight("alpha", UiStyle.Body, 1000f, 3) - one) < 0.01f && UiTextRules.WrappedHeight("", UiStyle.Body, 100f, 3) > 0f, "ui text: a wrapped height is its lines' heights");
        Check(UiDraw.Initials("Maya Okafor") == "MO" && UiDraw.Initials("City Office") == "CO" && UiDraw.Initials("") == "?" && UiDraw.Initials("张伟") == "张"
              && UiDraw.Initials("anderson recruitment corp.") == "AR", "ui text: an avatar's initials, one ideograph alone");
    }

    // ---------------------------------------------------------------- motion

    private static void UiMotionChecks()
    {
        bool rising = true;
        float last = 0f;
        for (int i = 1; i <= 100; i++)
        {
            float e = UiMotionRules.Ease(i / 100f);
            rising &= e >= last - 1e-4f;
            last = e;
        }
        Check(UiMotionRules.Ease(0f) == 0f && UiMotionRules.Ease(1f) == 1f && rising, "ui motion: the curve runs from 0 to 1 and never back");
        Check(UiMotionRules.Ease(0.25f) > 0.6f, "ui motion: the wireframe's curve starts fast and lands soft (" + UiMotionRules.Ease(0.25f).ToString("0.00") + " at a quarter)");
        Check(UiMotionRules.Delay(3, UiMotionSetting.Full) > UiMotionRules.Delay(1, UiMotionSetting.Full) && UiMotionRules.Delay(50, UiMotionSetting.Full) == UiMotionRules.Delay(8, UiMotionSetting.Full)
              && UiMotionRules.Delay(5, UiMotionSetting.Off) == 0f, "ui motion: rows arrive in turn, the later ones with the eighth, none with motion off");
        var buffer = new char[48];
        Check(Counted("$48,210", 0.5f, buffer) == "$24,105" && Counted("$48,210", 1f, buffer) == "$48,210" && Counted("$48,210", 0f, buffer) == "$0",
            "ui motion: a figure counts up in its own form, grouping kept");
        Check(Counted("6.4%", 0.5f, buffer) == "3.2%" && Counted("1.234,5", 1f, buffer) == "1.234,5" && Counted("+1.8%", 1f, buffer) == "+1.8%",
            "ui motion: decimals and another language's grouping are kept as counted");
        Check(UiMotionRules.CountInto("Closed", 0.5f, buffer) == -1 && UiMotionRules.CountInto(null, 0.5f, buffer) == -1, "ui motion: words with no number are not counted");
    }

    private static string Counted(string figure, float share, char[] buffer)
    {
        int n = UiMotionRules.CountInto(figure, share, buffer);
        return n < 0 ? null : new string(buffer, 0, n);
    }

    // ---------------------------------------------------------------- focus

    private static void UiFocusChecks()
    {
        var s = new UiScene();
        s.Begin("focus", 1000f, 600f, true);
        var left = UiAt.Screen.InRegion(1);
        var right = UiAt.Screen.InRegion(2);
        int a = FocusBox(s, left, 0f, 0f, 1), b = FocusBox(s, left, 0f, 100f, 2), c = FocusBox(s, right, 300f, 0f, 3), d = FocusBox(s, right, 300f, 400f, 4);
        Check(UiFocusRules.First(s) == a && UiFocusRules.Next(s, a, 0, 1) == b && UiFocusRules.Next(s, b, 0, -1) == a, "ui focus: up and down find the nearest target that way");
        Check(UiFocusRules.Next(s, a, 1, 0) == c && UiFocusRules.Next(s, c, -1, 0) == a, "ui focus: left and right cross into the next column");
        Check(UiFocusRules.Next(s, c, 0, 1) == d, "ui focus: down stays in its column even when a nearer target stands in another");
        Check(UiFocusRules.Next(s, a, -1, 0) == -1, "ui focus: nothing that way: focus stays");
        s.Nodes[a].Right = d;
        Check(UiFocusRules.Next(s, a, 1, 0) == d, "ui focus: a target's own link wins over the nearest");
        s.Nodes[a].Right = -1;
        var over = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 };
        int e = FocusBox(s, over, 500f, 300f, 5);
        Check(UiFocusRules.TopLayer(s) == 1 && UiFocusRules.First(s) == e && UiFocusRules.Next(s, e, -1, 0) == -1, "ui focus: an open question or call holds the focus");
        Check(UiFocusRules.Unreached(s, out string _) == 0, "ui focus: the top layer's one target is reached");
        // A list of 20 rows with 3 in view: down past the last in view asks the screen to scroll.
        var l = new UiScene();
        l.Begin("list", 1000f, 600f, true);
        short clip = l.AddClip(0f, 0f, 400f, 240f, 0f);
        short list = l.AddList(20, 0, 3, clip, 0);
        var inClip = new UiAt { Parent = -1, Clip = clip, Layer = 0, Order = -1 };
        int r0 = -1, r2 = -1;
        for (int i = 0; i < 3; i++)
        {
            int face = UiDraw.Box(l, inClip, 0f, i * 80f, 400f, 80f, UiRole.Row, UiRole.None, 0f);
            int n = UiDraw.Node(l, inClip, 0f, i * 80f, 400f, 80f, 9, i, face, false, list, i);
            r0 = i == 0 ? n : r0;
            r2 = i == 2 ? n : r2;
        }
        int search = UiDraw.Node(l, UiAt.Screen, 0f, 260f, 400f, 48f, 8, 0, -1);
        Check(UiFocusRules.Next(l, r2, 0, 1) == UiFocusRules.ScrollAsked, "ui focus: down from the last row in view asks the screen to scroll to the next");
        Check(UiFocusRules.Next(l, r0, 0, -1) == -1 || UiFocusRules.Next(l, r0, 0, -1) != r2, "ui focus: up from the first row never lands on another row of its own list");
        Check(UiFocusRules.Next(l, r2, 1, 0) != r0, "ui focus: sideways out of a list never lands back in it");
        Check(search >= 0, "ui focus: a target outside the list is a target of its own");
    }

    private static int FocusBox(UiScene s, UiAt at, float x, float y, int command)
    {
        int face = UiDraw.Box(s, at, x, y, 200f, 48f, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
        return UiDraw.Node(s, at, x, y, 200f, 48f, command, 0, face);
    }

    // ---------------------------------------------------------------- every registered screen

    private sealed class UiScreenCase
    {
        public UiScreenDef Def;
        public string Name;
        public Action<UiScene, UiFrame, bool, IUiKeys, UiWords> Lay;
        public string Title = "Contacts";   // the app's name in the masthead
    }

    private static List<UiScreenCase> UiCases()
    {
        var cases = new List<UiScreenCase>();
        foreach (UiScreenDef def in UiScreens.All)
        {
            UiScreenDef d = def;
            if (def.Id == "flare_city" || def.Id == "studio" || def.Id == "session_panel")
            {
                Action<UiScene, UiFrame, bool, IUiKeys, UiWords> lay = def.Id == "flare_city" ? (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.FlareCity(t), f, dark, k)
                    : def.Id == "studio" ? (s, f, dark, k, t) => UiForm.Lay(s, UiSamples.StudioFirm(t), f, dark, k)
                    : (Action<UiScene, UiFrame, bool, IUiKeys, UiWords>)((s, f, dark, k, t) => UiSessionSteps.Lay(s, UiSamples.DeskPanel(t), f, dark, k));
                cases.Add(new UiScreenCase { Def = d, Name = d.Id, Title = def.Id == "flare_city" ? "Flare" : def.Id == "studio" ? "Tech" : "Desk", Lay = lay });
                continue;
            }
            if (def.Id == "population")
            {
                cases.Add(new UiScreenCase { Def = d, Name = d.Id, Title = "Economy", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.PopulationCity(t), f, dark, k) });
                continue;
            }
            switch (def.Template)
            {
                case UiTemplate.ListDetail:
                    cases.Add(new UiScreenCase { Def = d, Name = d.Id, Lay = (s, f, dark, k, t) => UiListDetail.Lay(s, UiSamples.Contacts(t, false), f, dark, k) });
                    cases.Add(new UiScreenCase { Def = d, Name = d.Id + " (a line that takes replies)", Lay = (s, f, dark, k, t) => UiListDetail.Lay(s, UiSamples.Contacts(t, true), f, dark, k) });
                    break;
                case UiTemplate.Dashboard:
                    cases.Add(new UiScreenCase { Def = d, Name = d.Id, Title = "Economy", Lay = (s, f, dark, k, t) => UiDashboard.Lay(s, UiSamples.Dashboard(t), f, dark, k) });
                    break;
                case UiTemplate.Session:
                    cases.Add(new UiScreenCase { Def = d, Name = d.Id, Title = "Desk", Lay = (s, f, dark, k, t) => UiSessionSteps.Lay(s, UiSamples.Session(t), f, dark, k) });
                    break;
                case UiTemplate.Form:
                    cases.Add(new UiScreenCase { Def = d, Name = d.Id, Title = "CityAds", Lay = (s, f, dark, k, t) => UiForm.Lay(s, UiSamples.Form(t), f, dark, k) });
                    break;
                case UiTemplate.Gallery:
                    for (int page = 0; page < UiGallery.PageCount; page++)
                    {
                        int p = page;
                        cases.Add(new UiScreenCase { Def = d, Name = d.Id + " page " + page, Title = "Interface gallery", Lay = (s, f, dark, k, t) => UiGallery.Lay(s, UiSamples.Gallery(t, p), f, dark, k) });
                    }
                    break;
            }
        }
        return cases;
    }

    private static void UiScreenChecks()
    {
        var s = new UiScene();
        var said = new HashSet<string>(StringComparer.Ordinal);
        Action<string> say = line =>
        {
            if (said.Count < 60 && said.Add(line))
            {
                Console.WriteLine("        " + line);   // each different finding once
            }
        };
        foreach (UiScreenCase c in UiCases())
        {
            if (c.Def.Runner != null)
            {
                // A screen the game runs labels only with keys its entry names, so the texts check sees each one translated.
                var used = new List<string>();
                UiWords en = UiWords("en");
                c.Lay(s, UiFrame.FullMenu(16f / 9f, c.Title), true, UiDefaultKeys.Keyboard, (key, english) =>
                {
                    used.Add(key);
                    return en(key, english);
                });
                var unnamed = new List<string>();
                foreach (string key in used)
                {
                    if (Array.IndexOf(c.Def.Keys, key) < 0 && !unnamed.Contains(key))
                    {
                        unnamed.Add(key);
                    }
                }
                Check(unnamed.Count == 0, "ui screens: every key " + c.Name + " labels with is named in its entry (" + (unnamed.Count == 0 ? "all" : string.Join(", ", unnamed)) + ")");
            }
            foreach (string lang in ModLanguages.All)
            {
                UiWords t = UiWords(lang);
                foreach (float aspect in UiAspects)
                {
                    UiFrame frame = UiFrame.FullMenu(aspect, c.Title);
                    foreach (IUiKeys keys in new IUiKeys[] { UiDefaultKeys.Keyboard, UiDefaultKeys.XboxPad })
                    {
                        if (keys != UiDefaultKeys.Keyboard && lang != "en")
                        {
                            continue;   // a pad's glyphs are the same in every language: checked in one
                        }
                        c.Lay(s, frame, true, keys, t);
                        string where = c.Name + " in " + lang + " at " + aspect.ToString("0.00") + (keys.Pad != UiPad.None ? " with a pad" : "");
                        Check(UiCheckRules.Overlaps(s, say) == 0, "ui screens: nothing overlaps its siblings: " + where);
                        Check(UiCheckRules.Spills(s, say) == 0, "ui screens: nothing spills out of its parent: " + where);
                        Check(UiCheckRules.OffGrid(s, say) == 0, "ui screens: every box on the 4 px grid: " + where);
                        Check(UiCheckRules.Unfit(s, say) == 0, "ui screens: every label fits whole: " + where);
                        Check(UiCheckRules.SmallTargets(s, say) == 0, "ui screens: every target at least the least size: " + where);
                        Check(UiCheckRules.TooSmall(s, say) == 0, "ui screens: no text under the smallest size: " + where);
                        Check(UiCheckRules.Crowded(s, say) == 0, "ui screens: one primary action a layer, and no crowd: " + where);
                        Check(UiCheckRules.NoPrompts(s, say) == 0, "ui screens: a prompt bar that says how to go back: " + where);
                        Check(UiCheckRules.Contradictions(s, say) == 0, "ui screens: no indicator contradicts its value: " + where);
                        Check(UiCheckRules.DeadSpace(s, say) == 0, "ui screens: no frame holds dead space: " + where);
                        Check(UiCheckRules.KeyClash(s, say) == 0, "ui screens: one key does one thing: " + where);
                        Check(UiFocusRules.Unreached(s, out string missed) == 0, "ui screens: the d-pad reaches every target: " + where + " (" + missed + ")");
                        Check(UiCheckRules.LowContrast(s, say) == 0, "ui screens: every text and icon stands out (dark): " + where);
                        if (lang == "en")
                        {
                            c.Lay(s, frame, false, keys, t);
                            Check(UiCheckRules.LowContrast(s, say) == 0, "ui screens: every text and icon stands out (light): " + where);
                        }
                    }
                }
            }
        }
    }

    // A text source for one language: the mod's locale, then the game's own (its keys are the game's), then English, then the sample's words.
    private static UiWords UiWords(string lang)
    {
        Dictionary<string, string> mod = LocaleFile(lang), modEn = LocaleFile("en"), game = GameLocale(lang), gameEn = GameLocale("en");
        return (key, english) =>
        {
            if (mod.TryGetValue(key, out string v) || game.TryGetValue(key, out v) || modEn.TryGetValue(key, out v) || gameEn.TryGetValue(key, out v))
            {
                return v;
            }
            return english;
        };
    }

    private static readonly Dictionary<string, Dictionary<string, string>> GameLocales = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

    // The game's own translations (StreamingAssets/locale), where the game's keys a screen labels with are translated.
    private static Dictionary<string, string> GameLocale(string code)
    {
        if (GameLocales.TryGetValue(code, out Dictionary<string, string> held))
        {
            return held;
        }
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(GameManagedFolder(), "..", "StreamingAssets", "locale", code + ".json"));
        if (File.Exists(path))
        {
            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
                {
                    Flatten(doc.RootElement, texts);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // a file the game changed: its keys then read in the mod's English sample words
            }
        }
        GameLocales[code] = texts;
        return texts;
    }

    private static void Flatten(System.Text.Json.JsonElement e, Dictionary<string, string> into)
    {
        if (e.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return;
        }
        foreach (System.Text.Json.JsonProperty p in e.EnumerateObject())
        {
            if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                into[p.Name] = p.Value.GetString();
            }
            else
            {
                Flatten(p.Value, into);
            }
        }
    }

    // ---------------------------------------------------------------- each rule seen failing

    private static void UiRulesFailOnce()
    {
        var s = new UiScene();
        s.Begin("broken", 800f, 600f, true);
        int panel = UiDraw.Box(s, UiAt.Screen, 0f, 0f, 400f, 400f, UiRole.Panel, UiRole.None, 0f);
        UiDraw.Box(s, UiAt.Screen.In(panel), 8f, 8f, 100f, 100f, UiRole.Row, UiRole.None, 0f);
        UiDraw.Box(s, UiAt.Screen.In(panel), 60f, 60f, 100f, 100f, UiRole.Row, UiRole.None, 0f);
        Check(UiCheckRules.Overlaps(s, null) > 0, "ui rules: two siblings that cross are found");
        UiDraw.Box(s, UiAt.Screen.In(panel), 360f, 8f, 100f, 100f, UiRole.Row, UiRole.None, 0f);
        Check(UiCheckRules.Spills(s, null) > 0, "ui rules: a box over its parent's edge is found");
        UiDraw.Box(s, UiAt.Screen.In(panel), 8f, 250f, 30f, 30f, UiRole.Row, UiRole.None, 0f);
        UiDraw.Box(s, UiAt.Screen.In(panel), 13f, 290f, 10f, 10f, UiRole.Row, UiRole.None, 0f);
        Check(UiCheckRules.OffGrid(s, null) > 0, "ui rules: a box off the grid is found");
        UiDraw.Text(s, UiAt.Screen.In(panel), 8f, 320f, 40f, UiStyle.Body, UiRole.Text, "A label far too long for its box", UiFlags.Label);
        Check(UiCheckRules.Unfit(s, null) > 0, "ui rules: a label that does not fit is found");
        UiDraw.Text(s, UiAt.Screen.In(panel), 8f, 360f, 300f, UiStyle.Body, UiRole.Panel, "Words in the panel's own colour", UiFlags.None);
        Check(UiCheckRules.LowContrast(s, null) > 0, "ui rules: words that do not stand out are found");
        UiDraw.Node(s, UiAt.Screen, 500f, 500f, 20f, 20f, 1, 0, -1);
        Check(UiCheckRules.SmallTargets(s, null) > 0, "ui rules: a target smaller than the least is found");
        UiDraw.Button(s, UiAt.Screen, 500f, 0f, 0f, UiTokens.Control, UiButtonKind.Primary, "One", UiIcon.None, UiAction.Count, null, 2, 0);
        UiDraw.Button(s, UiAt.Screen, 500f, 100f, 0f, UiTokens.Control, UiButtonKind.Primary, "Two", UiIcon.None, UiAction.Count, null, 3, 0);
        Check(UiCheckRules.Crowded(s, null) > 0, "ui rules: two primary actions in a layer are found");
        Check(UiCheckRules.NoPrompts(s, null) > 0, "ui rules: a screen without its prompt bar is found");
        var lone = new UiScene();
        lone.Begin("lone", 800f, 600f, true);
        int f0 = UiDraw.Box(lone, UiAt.Screen.InRegion(1), 0f, 0f, 100f, 48f, UiRole.Field, UiRole.None, 0f);
        UiDraw.Node(lone, UiAt.Screen.InRegion(1), 0f, 0f, 100f, 48f, 1, 0, f0);
        int f1 = UiDraw.Box(lone, UiAt.Screen.InRegion(2), 600f, 500f, 100f, 48f, UiRole.Field, UiRole.None, 0f);
        int n1 = UiDraw.Node(lone, UiAt.Screen.InRegion(2), 600f, 500f, 100f, 48f, 2, 0, f1);
        lone.Nodes[0].Right = 0;
        lone.Nodes[0].Down = 0;
        Check(UiFocusRules.Unreached(lone, out string first) == 1 && first != null && n1 == 1, "ui rules: a target the d-pad never reaches is found");
        UiType[] types = (UiType[])typeof(UiTokens).GetField("Types", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
        float size = types[(int)UiStyle.Small].Size;
        types[(int)UiStyle.Small].Size = 11f;
        var small = new UiScene();
        small.Begin("small", 800f, 600f, true);
        UiDraw.Text(small, UiAt.Screen, 0f, 0f, 400f, UiStyle.Small, UiRole.Text, "Too small", UiFlags.None);
        Check(UiCheckRules.TooSmall(small, null) > 0, "ui rules: a text under the smallest size is found");
        types[(int)UiStyle.Small].Size = size;
        // The review's own two faults (1 Nov): "+2.1%" by a falling arrow, and a frame with its bottom half empty.
        var figure = new UiFigureModel { Label = "Average rent", Value = "$2,140", Change = "+2.1%", Rising = false, Good = false, Trend = new[] { 2f, 2.1f } };
        var told = new UiScene();
        told.Begin("told", 800f, 600f, true);
        UiDraw.Figure(told, UiAt.Screen, 0f, 0f, 300f, ref figure, true);
        Check(UiCheckRules.Contradictions(told, null) == 0, "ui rules: a figure's arrow follows the sign of its change, whatever the model says");
        int arrow = -1;
        for (int i = 0; i < told.ItemCount; i++)
        {
            arrow = told.Items[i].Kind == UiKind.Icon ? i : arrow;
        }
        Check(arrow >= 0 && told.Items[arrow].Icon == UiIcon.TrendUp && told.Items[arrow].Fill == UiRole.DangerText,
            "ui rules: rent up reads as a red arrow up (the arrow by the sign, the colour by the player)");
        told.Items[arrow].Icon = UiIcon.TrendDown;
        Check(UiCheckRules.Contradictions(told, null) > 0, "ui rules: a falling arrow by \"+2.1%\" is found");
        told.Items[arrow].Icon = UiIcon.TrendUp;
        told.Items[arrow].Fill = UiRole.OkText;
        Check(UiCheckRules.Contradictions(told, null) > 0, "ui rules: a green arrow by red words is found");
        var hole = new UiScene();
        hole.Begin("hole", 800f, 600f, true);
        int frame = UiDraw.Box(hole, UiAt.Screen, 0f, 0f, 400f, 400f, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
        UiDraw.Text(hole, UiAt.Screen.In(frame), 20f, 20f, 300f, UiStyle.Body, UiRole.Text, "Half a panel of words", UiFlags.None);
        Check(UiCheckRules.DeadSpace(hole, null) > 0, "ui rules: a frame whose bottom half is empty is found");
        hole.Items[frame].H = UiDraw.Snap(20f + UiTextRules.LineHeight(UiStyle.Body) + UiTokens.S5);
        Check(UiCheckRules.DeadSpace(hole, null) == 0, "ui rules: the frame fitted to its words passes");
        // The session's first draft: its choices far over its buttons, a hole between two lines rather than under the last.
        hole.Items[frame].H = 400f;
        UiDraw.Button(hole, UiAt.Screen.In(frame), 20f, 320f, 200f, UiTokens.Control, UiButtonKind.Secondary, "Begin", UiIcon.None, UiAction.Count, null, 1, 0);
        Check(UiCheckRules.DeadSpace(hole, null) > 0, "ui rules: a frame with a hole between its lines is found");
        // The session's keys (1 Nov): Enter on a choice and on Begin, and one key on two buttons.
        var keyed = new UiScene();
        keyed.Begin("keyed", 800f, 600f, true);
        var choice = new UiChoiceModel { Key = UiAction.Confirm, Name = "Lift morale", Trade = "Burnout eases." };
        UiDraw.ChoiceCard(keyed, UiAt.Screen, 0f, 0f, 400f, ref choice, UiDefaultKeys.Keyboard, 1, 0, out float _);
        Check(UiCheckRules.KeyClash(keyed, null) > 0, "ui rules: a target showing Enter, which presses whatever has focus, is found");
        var twice = new UiScene();
        twice.Begin("twice", 800f, 600f, true);
        UiDraw.Button(twice, UiAt.Screen, 0f, 0f, 0f, UiTokens.Control, UiButtonKind.Primary, "Call", UiIcon.None, UiAction.Alt, UiDefaultKeys.Keyboard, 1, 0);
        UiDraw.Button(twice, UiAt.Screen, 0f, 100f, 0f, UiTokens.Control, UiButtonKind.Secondary, "Begin", UiIcon.None, UiAction.Alt, UiDefaultKeys.Keyboard, 2, 0);
        Check(UiCheckRules.KeyClash(twice, null) > 0, "ui rules: one key on two targets is found");
        var picks = new UiScene();
        picks.Begin("picks", 800f, 600f, true);
        for (int i = 0; i < 3; i++)
        {
            var c = new UiChoiceModel { Key = (UiAction)((int)UiAction.Pick1 + i), Name = "Choice", Trade = "What it does." };
            UiDraw.ChoiceCard(picks, UiAt.Screen, 0f, i * 120f, 400f, ref c, UiDefaultKeys.Keyboard, 1, i, out float _);
        }
        Check(UiCheckRules.KeyClash(picks, null) == 0 && UiFocusRules.Keyed(picks, UiAction.Pick2) == 1, "ui rules: choices on 1, 2 and 3 pass, and 2 presses the second");
    }

    // ---------------------------------------------------------------- sources

    private static void UiSourceChecks()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        var gameFree = new List<string>();
        bool clean = true;
        string dirty = "none";
        foreach (string path in Directory.GetFiles(folder, "*.cs"))
        {
            string name = System.IO.Path.GetFileName(path);
            bool meetsGame = name.StartsWith("UiGame", StringComparison.Ordinal) || name.StartsWith("UiDirector", StringComparison.Ordinal) || name == "UiText.cs" || name == "UiGuide.cs";
            if (meetsGame)
            {
                continue;
            }
            gameFree.Add(name);
            foreach (string line in File.ReadAllLines(path))
            {
                string l = line.Trim();
                if (l.StartsWith("using ", StringComparison.Ordinal) && l.EndsWith(";", StringComparison.Ordinal) && !l.StartsWith("using System", StringComparison.Ordinal)
                    && !l.StartsWith("using UnityEngine", StringComparison.Ordinal) && !l.StartsWith("using TMPro", StringComparison.Ordinal) && !l.StartsWith("using RealLifeOverhaul", StringComparison.Ordinal)
                    && !l.StartsWith("using var ", StringComparison.Ordinal))
                {
                    clean = false;
                    dirty = name + ": " + l;
                }
            }
        }
        Check(clean && gameFree.Count >= 30, "ui sources: the kit's files name only the engine, its interface and text, so the offline preview draws exactly what ships (" + dirty + ")");
        // RLO-UI-015 (the owner, 1 Nov): flat surfaces, crisp edges; no gradient, glow, blur or soft shadow anywhere in the kit.
        var soft = new System.Text.RegularExpressions.Regex(@"\b\w*(Glow|Wash|Gradient|Blur|Shadow)\w*\b");
        string soften = "none";
        foreach (string path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            foreach (string line in File.ReadAllLines(path))
            {
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                string code = comment >= 0 ? line.Substring(0, comment) : line;
                if (soft.IsMatch(code))
                {
                    soften = System.IO.Path.GetFileName(path) + ": " + code.Trim();
                }
            }
        }
        Check(soften == "none", "ui sources: no gradient, glow, blur or soft shadow in the kit (" + soften + ")");
        string host = File.ReadAllText(System.IO.Path.Combine(folder, "UiHost.cs"));
        Check(host.Contains("[DefaultExecutionOrder(-31000)]") && host.Contains("enabled = false;") && host.Contains("HotSpots.Time(HotUpdate)") && host.Contains("Guard.Fail(FrameGuard, e, this)"),
            "ui sources: the host runs before the game's Escape, sleeps while hidden, is timed and guarded");
        string tweens = File.ReadAllText(System.IO.Path.Combine(folder, "UiTweens.cs"));
        Check(tweens.Contains("if (_count == 0)") && tweens.Contains("enabled = false;") && tweens.Contains("new Entry[Most]"), "ui sources: the tweens run from a fixed pool and switch off when still");
        // Every class that runs a kit screen is registered (UiScreens), and every registered runner exists.
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, ".."));
        var runners = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            int at = text.IndexOf("IUiScreen", StringComparison.Ordinal);
            while (at >= 0)
            {
                int cls = text.LastIndexOf("class ", at, StringComparison.Ordinal);
                int brace = text.LastIndexOf('{', at);
                if (cls >= 0 && cls > brace && text.IndexOf(':', cls) < at)
                {
                    string rest = text.Substring(cls + 6).TrimStart();
                    int end = 0;
                    while (end < rest.Length && (char.IsLetterOrDigit(rest[end]) || rest[end] == '_'))
                    {
                        end++;
                    }
                    runners.Add(rest.Substring(0, end));
                }
                at = text.IndexOf("IUiScreen", at + 9, StringComparison.Ordinal);
            }
        }
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (UiScreenDef def in UiScreens.All)
        {
            if (def.Runner != null)
            {
                named.Add(def.Runner);
            }
        }
        Check(named.SetEquals(runners), "ui sources: every class running a kit screen is in UiScreens, and every runner UiScreens names exists (found " + string.Join(", ", runners) + ")");
    }

    // ---------------------------------------------------------------- texts

    private static void UiTextKeys()
    {
        var mine = new List<string>();
        foreach (UiScreenDef def in UiScreens.All)
        {
            foreach (string key in def.Keys)
            {
                if (key.StartsWith("reallife_", StringComparison.Ordinal) && !mine.Contains(key))
                {
                    mine.Add(key);
                }
            }
        }
        mine.AddRange(new[]
        {
            "reallife_ui_enabled", "reallife_opt_tip_ui_interface", "reallife_ui_motion", "reallife_opt_tip_ui_motion", "reallife_ui_motion_0", "reallife_ui_motion_1",
            "reallife_ui_motion_2", "reallife_opt_sec_ui", "reallife_ui_test_gallery", "reallife_ui_test_contacts", "reallife_ui_test_population", "reallife_ui_page_population", "reallife_ui_test_flare", "reallife_ui_test_firm", "reallife_ui_page_flare", "reallife_ui_page_studio", "reallife_ui_test_off", "reallife_ui_test_not_ready",
            "reallife_ui_note_fallback", "reallife_guide_ui", "reallife_guide_ui_about", "reallife_meta_title_ui"
        });
        Check(AllIn(mine.ToArray(), out string missing), "ui texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        var game = GameLocale("en");
        bool games = true;
        string gone = "none";
        foreach (UiScreenDef def in UiScreens.All)
        {
            foreach (string key in def.Keys)
            {
                if (!key.StartsWith("reallife_", StringComparison.Ordinal) && game.Count > 0 && !game.ContainsKey(key) && !en.ContainsKey(key))
                {
                    games = false;
                    gone = key;
                }
            }
        }
        Check(games, "ui texts: every game key a screen labels with is the game's own (" + gone + ")");
        Check(WhatsNewHas(en, "RLO interface"), "ui texts: What's new tells of the RLO interface");
    }

    // ---------------------------------------------------------------- timings

    private static void UiTimings()
    {
        UiWords t = UiWords("en");
        UiListDetailModel model = UiSamples.Contacts(t, true);
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        var s = new UiScene();
        UiListDetail.Lay(s, model, frame, true, UiDefaultKeys.Keyboard);
        double ms = UiLayoutTime(s, model, frame, 200);
        Check(ms < 2.0, "ui timings: laying the Contacts screen out takes " + ms.ToString("0.000") + " ms, under the 2 ms slice");
        long bytes = UiLayoutBytes(s, model, frame);
        Check(bytes < 16384, "ui timings: laying it out again allocates " + bytes + " bytes (an avatar's initials, a badge's count), under 16 KB");
        Console.WriteLine("    ui: a Contacts layout " + ms.ToString("0.000") + " ms, " + bytes + " bytes, " + s.ItemCount + " items, " + s.NodeCount + " targets");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double UiLayoutTime(UiScene s, UiListDetailModel model, UiFrame frame, int times)
    {
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < times; i++)
        {
            UiListDetail.Lay(s, model, frame, (i & 1) == 0, UiDefaultKeys.Keyboard);
        }
        return watch.Elapsed.TotalMilliseconds / times;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long UiLayoutBytes(UiScene s, UiListDetailModel model, UiFrame frame)
    {
        long start = GC.GetAllocatedBytesForCurrentThread();
        UiListDetail.Lay(s, model, frame, true, UiDefaultKeys.Keyboard);
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }
}
