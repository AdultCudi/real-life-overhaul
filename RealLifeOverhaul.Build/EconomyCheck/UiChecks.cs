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
        UiInputModeChecks();
        UiScreenChecks();
        KitPromptChecks();   // the keys' prompts as chips over the bar: the tray and the pills never move, nothing meets a chip (KitPromptChecks.cs)
        UiPageChecks();
        UiRulesFailOnce();
        UiSourceChecks();
        UiBindChecks();
        UiHostChecks();
        UiClickChecks();
        UiClickBehindAllChecks();
        ToastChecks();
        UiForeignChecks();
        UiCaretChecks();
        UiTextKeys();
        UiTimings();
        Console.WriteLine("  Interface kit: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- tokens

    private static void UiTokenChecks()
    {
        Check(UiTokens.ThemeLength() == (int)UiRole.Count, "ui tokens: the one look gives a colour for every role");
        bool all = true;
        for (int r = 1; r < (int)UiRole.Count; r++)
        {
            all &= (UiRole)r == UiRole.Bg || UiTokens.Colour((UiRole)r) != 0;
        }
        Check(all && UiTokens.Colour(UiRole.Bg) == 0, "ui tokens: no role is left without a colour, and the ground is clear, so the world shows behind every panel");
        Check(UiTokens.Colour(UiRole.Panel) >> 8 == 0x2B3A4A && UiTokens.Colour(UiRole.Header) >> 8 == 0x223040 && UiTokens.Colour(UiRole.Band) >> 8 == 0x1E2A37
              && UiTokens.Colour(UiRole.StatusBar) >> 8 == 0x1A2430 && UiTokens.Colour(UiRole.Pill) == 0x263445FF && UiTokens.Colour(UiRole.Text) == 0xE6EDF3FF
              && UiTokens.Colour(UiRole.Text2) == 0xA9B8C7FF && UiTokens.Colour(UiRole.Accent) == 0x3DBBEBFF && UiTokens.Colour(UiRole.Ok) == 0x74D35AFF
              && UiTokens.Colour(UiRole.Warn) == 0xF2C14EFF && UiTokens.Colour(UiRole.Danger) == 0xEE6352FF,
            "ui tokens: the Cities: Skylines II look (DIRECTION_CS2, section 3): slate panels, a darker strip, band and bar, soft white words, the cyan and the status colours");
        // The owner (1 Nov): nothing that hits the eyes. No pure white anywhere, and every surface a cool dark slate.
        bool white = false;
        for (int r = 1; r < (int)UiRole.Count; r++)
        {
            white |= UiTokens.Colour((UiRole)r) >> 8 == 0xFFFFFF;
        }
        float textL = UiLightness(UiTokens.Colour(UiRole.Text)), avatarInk = UiTokens.AvatarDarkInkL * 100f;
        Check(!white && textL <= 94f && avatarInk <= 94f, "ui tokens: no pure white anywhere: the words at L* " + textL.ToString("0") + ", an avatar's initials at " + avatarInk.ToString("0")
            + " (at most 94)");
        bool cool = true;
        foreach (UiRole r in new[] { UiRole.Panel, UiRole.Header, UiRole.Band, UiRole.StatusBar, UiRole.Pill, UiRole.Field, UiRole.Bubble, UiRole.Notice, UiRole.Tile })
        {
            uint c = UiTokens.Colour(r);
            uint red = (c >> 24) & 0xFF, green = (c >> 16) & 0xFF, blue = (c >> 8) & 0xFF;
            cool &= blue > red && blue >= green && UiLightness(c) < 40f;
        }
        Check(cool, "ui tokens: every surface is a cool dark slate, blue over red: no brown, red or cream panel (DIRECTION_CS2, section 3)");
        UiColour ground = UiColour.Of(UiRole.Panel, true).Over(UiCheckRules.World);
        float rule = UiColour.Contrast(UiColour.Of(UiRole.Rule, true).Over(ground), ground);
        Check(rule > 1.1f && rule < 2.5f, "ui tokens: a hairline is a dim line on the panel, seen but never glaring (" + rule.ToString("0.00") + ":1 on the panel)");
        Check(UiTokens.RadiusPanel == 4f && UiTokens.RadiusLarge == 4f && UiTokens.RadiusMedium == 4f && UiTokens.FrameRule == UiTokens.Hairline,
            "ui tokens: panels, buttons and pills at 4 px corners, a hairline under a title strip (DIRECTION_CS2, section 3)");
        Check(UiFrame.MastheadHeight == 0f && UiFrame.BandHeight == UiFrame.Toolbar + UiFrame.StatusRow && UiFrame.BandHeight % UiTokens.Grid == 0f && UiFrame.Strip % UiTokens.Grid == 0f,
            "ui tokens: no masthead: the status bar along the foot holds the apps and the pills, a title strip heads each panel, all on the grid");
        UiRole[] surfaces = { UiRole.Panel, UiRole.Header, UiRole.Band, UiRole.Field, UiRole.Bubble, UiRole.Hover, UiRole.StatusBar, UiRole.Pill };
        foreach (UiRole ink in new[] { UiRole.Text, UiRole.Text2, UiRole.AccentText })
        {
            foreach (UiRole surface in surfaces)
            {
                float c = UiContrast(ink, surface, true);
                Check(c >= UiCheckRules.NormalContrast, "ui tokens: " + ink + " on " + surface + " stands " + c.ToString("0.00") + ":1, WCAG AA wants 4.5");
            }
        }
        foreach (UiRole surface in new[] { UiRole.Panel, UiRole.Header, UiRole.Band, UiRole.StatusBar, UiRole.Pill })
        {
            float c = UiContrast(UiRole.Muted, surface, true);
            Check(c >= UiCheckRules.NormalContrast, "ui tokens: Muted on " + surface + " stands " + c.ToString("0.00") + ":1, WCAG AA wants 4.5 (a label, never on a bubble or a chosen row)");
        }
        foreach (UiRole ink in new[] { UiRole.Text, UiRole.AccentText })
        {
            float c = UiContrast(ink, UiRole.RowSel, true);
            Check(c >= UiCheckRules.NormalContrast, "ui tokens: " + ink + " on a chosen row stands " + c.ToString("0.00") + ":1, WCAG AA wants 4.5");
        }
        Check(UiContrast(UiRole.OnAccent, UiRole.Accent, true) >= UiCheckRules.NormalContrast, "ui tokens: words on the cyan reach 4.5:1 (" + UiContrast(UiRole.OnAccent, UiRole.Accent, true).ToString("0.00") + ")");
        Check(UiContrast(UiRole.NoticeText, UiRole.Notice, true) >= UiCheckRules.NormalContrast, "ui tokens: a quiet badge's words reach 4.5:1");
        foreach (UiRole[] pair in new[] { new[] { UiRole.OkText, UiRole.OkSoft }, new[] { UiRole.WarnText, UiRole.WarnSoft }, new[] { UiRole.DangerText, UiRole.DangerSoft }, new[] { UiRole.AccentText, UiRole.AccentSoft } })
        {
            UiColour soft = UiColour.Of(pair[1], true).Over(ground);
            float c = UiColour.Contrast(UiColour.Of(pair[0], true).Over(soft), soft);
            Check(c >= UiCheckRules.NormalContrast, "ui tokens: " + pair[0] + " on its soft fill reaches 4.5:1 (" + c.ToString("0.00") + ")");
        }
        float worst = 21f;
        int worstHue = 0;
        for (int hue = 0; hue < 360; hue++)
        {
            float c = UiColour.Contrast(UiColour.AvatarInk(hue, true), UiColour.AvatarFill(hue, true));
            if (c < worst)
            {
                worst = c;
                worstHue = hue;
            }
        }
        Check(worst >= UiCheckRules.NormalContrast, "ui tokens: every avatar's initials reach 4.5:1 on their circle, at every hue (worst " + worst.ToString("0.00") + " at " + worstHue + ")");
        foreach (UiRole surface in new[] { UiRole.Panel, UiRole.Band, UiRole.StatusBar })
        {
            float c = UiContrast(UiRole.Focus, surface, true);
            Check(c >= UiCheckRules.LargeContrast, "ui tokens: the focus ring stands out 3:1 from " + surface + " (" + c.ToString("0.00") + ")");
        }
        Check(UiContrast(UiRole.PromptInk, UiRole.StatusBar, true) >= UiCheckRules.NormalContrast, "ui tokens: the prompts' words stand out on the status bar");
        // Every icon in its own colour reads on its slate tile and on the bare panel, at the 3:1 a graphic needs (WCAG 1.4.11).
        string dim = null;
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            UiRole tint = UiIconTints.Of((UiIcon)i);
            float onTile = UiContrast(tint, UiRole.Tile, true), onPanel = UiContrast(tint, UiRole.Panel, true);
            if (Math.Min(onTile, onPanel) < UiCheckRules.LargeContrast && dim == null)
            {
                dim = (UiIcon)i + " in " + tint + " (" + onTile.ToString("0.00") + ":1 on its tile)";
            }
        }
        Check(dim == null, "ui tokens: every icon's own colour reads 3:1 on its tile and on the panel" + (dim == null ? string.Empty : ": " + dim));
        UiChartChecks();
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
        Check(sizes && UiTokens.MinText >= 13f, "ui tokens: every style is at least the smallest text (13 px at 1080p, a section's label: DIRECTION_CS2, section 3)");
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

    // CIE L* of a token's colour over black, the glare a surface or a word throws.
    private static float UiLightness(uint rgba)
    {
        UiColour c = UiColour.FromRgba(rgba);
        Func<float, double> lin = v => v <= 0.04045f ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        double y = 0.2126 * lin(c.R) + 0.7152 * lin(c.G) + 0.0722 * lin(c.B);
        return (float)(y > 0.008856 ? 116.0 * Math.Pow(y, 1.0 / 3.0) - 16.0 : 903.3 * y);
    }

    // An ink on a surface where the kit draws it: the status bar and its pills over the world, everything else on a panel over
    // the world, taken at its brightest (UiCheckRules.World).
    private static float UiContrast(UiRole ink, UiRole surface, bool dark)
    {
        UiColour world = UiCheckRules.World;
        UiColour bar = UiColour.Of(UiRole.StatusBar, dark).Over(world);
        UiColour panel = UiColour.Of(UiRole.Panel, dark).Over(world);
        UiColour under = surface == UiRole.Panel ? panel : surface == UiRole.StatusBar ? bar : surface == UiRole.Pill ? UiColour.Of(UiRole.Pill, dark).Over(bar)
            : UiColour.Of(surface, dark).Over(panel);
        return UiColour.Contrast(UiColour.Of(ink, dark).Over(under), under);
    }

    // The chart's eight hues stay apart under colour blindness (DIRECTION_CS2, section 3): each pair under protanopia,
    // deuteranopia and tritanopia (Machado, Oliveira and Fernandes 2009, full severity), measured in CIELAB, and each slice
    // from the one beside it in a donut by more.
    private static void UiChartChecks()
    {
        double[][,] kinds =
        {
            new[,] { { 0.152286, 1.052583, -0.204868 }, { 0.114503, 0.786281, 0.099216 }, { -0.003882, -0.048116, 1.051998 } },
            new[,] { { 0.367322, 0.860646, -0.227968 }, { 0.280085, 0.672501, 0.047413 }, { -0.011820, 0.042940, 0.968881 } },
            new[,] { { 1.255528, -0.076749, -0.178779 }, { -0.078411, 0.930809, 0.147602 }, { 0.004733, 0.691367, 0.303900 } },
        };
        string[] names = { "protanopia", "deuteranopia", "tritanopia" };
        for (int k = 0; k < kinds.Length; k++)
        {
            var labs = new double[8][];
            for (int i = 0; i < 8; i++)
            {
                labs[i] = SeenLab(UiTokens.Colour(UiTokens.Chart(i)), kinds[k]);
            }
            double pair = double.MaxValue, beside = double.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                beside = Math.Min(beside, LabDistance(labs[i], labs[(i + 1) % 8]));
                for (int j = i + 1; j < 8; j++)
                {
                    pair = Math.Min(pair, LabDistance(labs[i], labs[j]));
                }
            }
            Check(pair >= 8.0 && beside >= 12.0, "ui tokens: the chart's hues stay apart under " + names[k] + " (closest pair " + pair.ToString("0.0") + ", closest neighbours " + beside.ToString("0.0")
                + "; at least 8 and 12)");
            // A map's ramp is ordered: its lightness runs one way step by step as this eye sees it, and neighbours stay apart.
            // Tritanopia sees the violet's colour only faintly, so there the lightness alone keeps the steps apart.
            double step = double.MaxValue, apart = double.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                double[] a = SeenLab(UiTokens.Colour(UiTokens.Ramp(i)), kinds[k]), b = SeenLab(UiTokens.Colour(UiTokens.Ramp(i + 1)), kinds[k]);
                step = Math.Min(step, a[0] - b[0]);
                apart = Math.Min(apart, LabDistance(a, b));
            }
            double least = k == 2 ? 5.0 : 10.0;
            Check(step >= 4.0 && apart >= least, "ui tokens: the map's ramp runs in order and stays apart under " + names[k] + " (least step " + step.ToString("0.0")
                + " L*, closest neighbours " + apart.ToString("0.0") + "; at least 4 and " + least.ToString("0") + ")");
        }
        // No glare: no step brighter than L* 75, and each reads 3:1 on the panel as a legend's swatch.
        double brightest = 0.0;
        float faintest = float.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            brightest = Math.Max(brightest, SeenLab(UiTokens.Colour(UiTokens.Ramp(i)), new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } })[0]);
            faintest = Math.Min(faintest, UiContrast(UiTokens.Ramp(i), UiRole.Panel, true));
        }
        Check(brightest <= 75.0 && faintest >= UiCheckRules.LargeContrast, "ui tokens: the map's ramp never glares and every step reads on the panel (brightest L* "
            + brightest.ToString("0.0") + ", at most 75; faintest " + faintest.ToString("0.00") + ":1)");
        // A map beside a donut must never say a slice's colour: every step of the ramp stands well away from every chart hue.
        double[,] eye = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        double nearest = double.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            for (int c = 0; c < 8; c++)
            {
                nearest = Math.Min(nearest, LabDistance(SeenLab(UiTokens.Colour(UiTokens.Ramp(i)), eye), SeenLab(UiTokens.Colour(UiTokens.Chart(c)), eye)));
            }
        }
        Check(nearest >= 15.0, "ui tokens: the map's ramp keeps away from the chart's hues (closest " + nearest.ToString("0.0") + "; at least 15)");
    }

    // A colour as an eye with a colour vision deficiency sees it, in CIELAB.
    private static double[] SeenLab(uint rgba, double[,] m)
    {
        double[] c = { Linear((rgba >> 24) & 0xFF), Linear((rgba >> 16) & 0xFF), Linear((rgba >> 8) & 0xFF) };
        var seen = new double[3];
        for (int r = 0; r < 3; r++)
        {
            seen[r] = Math.Max(0.0, Math.Min(1.0, m[r, 0] * c[0] + m[r, 1] * c[1] + m[r, 2] * c[2]));
        }
        double x = 0.4124 * seen[0] + 0.3576 * seen[1] + 0.1805 * seen[2], y = 0.2126 * seen[0] + 0.7152 * seen[1] + 0.0722 * seen[2];
        double z = 0.0193 * seen[0] + 0.1192 * seen[1] + 0.9505 * seen[2];
        double fx = LabF(x / 0.95047), fy = LabF(y), fz = LabF(z / 1.08883);
        return new[] { 116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz) };
    }

    private static double Linear(uint channel)
    {
        double c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double LabF(double t) => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : 7.787 * t + 16.0 / 116.0;

    private static double LabDistance(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]) + (a[2] - b[2]) * (a[2] - b[2]));

    // ---------------------------------------------------------------- fonts

    private static void UiFontChecks()
    {
        Check(UiFontMetrics.FaceCount == 3, "ui fonts: three faces, Overpass at 400, 600 and 700 (DIRECTION_CS2: never serif)");
        bool faces = true;
        foreach ((UiFamily family, int weight) in new[] { (UiFamily.Overpass, 400), (UiFamily.Overpass, 600), (UiFamily.Overpass, 700) })
        {
            UiFace f = UiFontMetrics.Face(family, weight);
            faces &= f != null && f.Family == family && f.Weight == weight && f.UnitsPerEm > 0 && f.Ascender > 0 && f.Descender < 0 && f.LineEm > 1f && f.LineEm < 1.6f;
        }
        Check(faces, "ui fonts: each face has its own metrics and a line of 1 to 1.6 em");
        bool scripts = true;
        bool tabular = true;
        foreach (int weight in new[] { 400, 600, 700 })
        {
            UiFace f = UiFontMetrics.Face(UiFamily.Overpass, weight);
            scripts &= f.Advance('A') > 0 && f.Advance('ł') > 0 && f.Advance('ğ') > 0 && f.Advance('İ') > 0 && f.Advance('ç') > 0 && f.Advance('ж') > 0 && f.Advance('\u2212') > 0;
            for (char d = '1'; d <= '9'; d++)
            {
                tabular &= f.Advance(d) == f.Advance('0');
            }
        }
        Check(scripts, "ui fonts: Overpass draws Latin, Latin Extended (Polish, Turkish, Portuguese), Cyrillic and the minus sign in each weight");
        Check(tabular, "ui fonts: every figure is as wide as every other, so a column of numbers lines up (the tabular cuts swapped in by fonts.py)");
        string fonts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\UiAssets"));
        string script = File.Exists(System.IO.Path.Combine(fonts, "scripts", "fonts.py")) ? File.ReadAllText(System.IO.Path.Combine(fonts, "scripts", "fonts.py")) : "";
        bool pinned = script.Length > 0;
        foreach (string file in new[] { "Overpass-Regular.ttf", "Overpass-SemiBold.ttf", "Overpass-Bold.ttf" })
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
        Check(File.Exists(bundle) && listed.Contains("\"faces\"") && listed.Contains("Overpass-Regular SDF") && listed.Contains("Overpass-SemiBold SDF") && listed.Contains("Overpass-Bold SDF"),
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

    // The input mode (UiFocusMode, UiHost): beta 3's ring followed the mouse onto every row it crossed. With the mouse
    // there is no ring and a hover moves nothing; the first key or pad press only shows where the focus is; moving the
    // mouse hides it again; and the band names Move and Select only for the keys.
    private static void UiInputModeChecks()
    {
        var mode = new UiFocusMode(UiLead.Keys);
        mode.Start(false);
        Check(mode.Lead == UiLead.Pointer && !mode.Ring, "ui input: a screen opened with the mouse shows no focus ring");
        int focus = 2;
        bool hovered = mode.Entered(5);
        Check(hovered && mode.Hover == 5 && !mode.Ring && mode.Lifted(focus) == 5, "ui input: a hover lifts the target under the mouse and shows no ring");
        Check(mode.Exited(5) && mode.Hover == -1 && mode.Lifted(focus) == -1, "ui input: the pointer leaving a target puts its fill back");
        Check(mode.Woke() && mode.Ring && mode.Lifted(focus) == focus && mode.Hover == -1, "ui input: the first key or pad press brings the ring back where the focus is");
        Check(!mode.Woke(), "ui input: the press that woke the ring is spent, the next one moves or presses");
        Check(!mode.Entered(7) && mode.Hover == -1 && mode.Lifted(focus) == focus, "ui input: a list scrolled by the keys under a resting pointer makes no hover");
        Check(mode.PointerMoved() && !mode.Ring && !mode.PointerMoved(), "ui input: moving the mouse hides the ring again, once");
        mode.Start(true);
        Check(mode.Ring, "ui input: a screen opened with a pad has its ring from the start");
        // The host acts on the mode alone: a hover only lifts a face, the keys wake before they move, a click focuses silently.
        string host = File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\UiHost.cs")));
        string entered = Between(host, "private void PointerEntered(int node)", "private void PointerExited(int node)");
        Check(entered.Contains("_view.SetHover(node)") && !entered.Contains("FocusNode") && !entered.Contains("SetFocus") && !entered.Contains("Play("),
            "ui input: the host's hover lifts the target and never moves the focus or plays the move sound");
        string navigate = Between(host, "private void Navigate(float now)", "private void PressKeyed(");
        int wake = navigate.IndexOf("_mode.Woke()", StringComparison.Ordinal), move = navigate.IndexOf("Move(dx", StringComparison.Ordinal);
        Check(wake >= 0 && move > wake, "ui input: the host lets a key wake the ring before any move or press");
        string focusNode = Between(host, "private void FocusNode(int node, float now, bool glide)", "private void PressNode(");
        Check(focusNode.Contains("glide && _mode.Ring") && focusNode.Contains("if (_mode.Ring)"), "ui input: the ring glides and the move sounds only while the keys lead");
        // The band follows the lead.
        var s = new UiScene();
        UiFrame keys = UiFrame.FullMenu(16f / 9f, "The city's people");
        UiFrame pointer = keys;
        pointer.Pointer = true;
        PopulationFace.Lay(s, UiSamples.PopulationPage(UiWords("en"), "contributors"), keys, true, UiDefaultKeys.Keyboard);
        bool keysNamed = Prompted(s, UiAction.Move) && Prompted(s, UiAction.Confirm) && Prompted(s, UiAction.Back) && PromptsDrawn(s) > 0;
        PopulationFace.Lay(s, UiSamples.PopulationPage(UiWords("en"), "contributors"), pointer, true, UiDefaultKeys.Keyboard);
        Check(keysNamed && PromptsDrawn(s) == 0 && Prompted(s, UiAction.Back),
            "ui input: with the keys leading the status bar names every key and the way back; with the mouse leading it shows none, as the mouse does it by pointing");
    }

    // The prompts the status bar draws: none while the mouse leads.
    private static int PromptsDrawn(UiScene s)
    {
        int n = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            n += s.Items[i].Kind == UiKind.Text && (s.Items[i].Flags & UiFlags.Prompt) != 0 ? 1 : 0;
        }
        return n;
    }

    private static bool Prompted(UiScene s, UiAction action)
    {
        for (int i = 0; i < s.PromptCount; i++)
        {
            if (s.Prompts[i].Action == action)
            {
                return true;
            }
        }
        return false;
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
        public int[] Commands;              // every command its template answers: a target with another is a button nothing answers
        public bool Overlay;                // over the world and every screen, taking no key (the notice cards): no prompt bar of its own
    }

    private static readonly int[] ListDetailCommands = { UiListDetail.Group, UiListDetail.SideAction, UiListDetail.Search, UiListDetail.Row, UiListDetail.Action,
        UiListDetail.Service, UiListDetail.Reply, UiListDetail.Compose, UiListDetail.Send, UiListDetail.Answer, UiListDetail.CallOption, UiListDetail.CallEnd, UiListDetail.Classic };
    private static readonly int[] SessionCommands = { UiSessionSteps.ChoiceCommand, UiSessionSteps.ActionCommand };
    private static readonly int[] FormCommands = { UiForm.FieldCommand, UiForm.ActionCommand };
    private static readonly int[] GalleryCommands = { UiGallery.PageCommand, UiGallery.Sample, UiGallery.Answer, UiGallery.CallOption, UiGallery.CallEnd };
    private static readonly int[] InfoPanelCommands = { UiInfoPanel.TabCommand, UiInfoPanel.LinkCommand, UiInfoPanel.FooterCommand };
    private static readonly int[] InfoViewCommands = { UiInfoView.ViewCommand };
    private static readonly int[] AppWindowCommands = { UiAppWindow.NavCommand, UiAppWindow.RowCommand, UiAppWindow.PersonCommand, UiAppWindow.ActionCommand,
        UiAppWindow.MoreCommand, UiAppWindow.AllCommand, UiAppWindow.NoteCommand };
    private static readonly int[] DecisionCommands = { UiDecision.ChoiceCommand, UiDecision.ButtonCommand };
    private static readonly int[] PhoneCommands = { UiPhone.AppCommand, UiPhone.PageCommand, UiPhone.ControlCommand };

    // Every command a template answers: a target with another is a button nothing answers.
    private static int[] Commands(UiTemplate template)
    {
        switch (template)
        {
            case UiTemplate.ListDetail: return ListDetailCommands;
            case UiTemplate.Session: return SessionCommands;
            case UiTemplate.Form: return FormCommands;
            case UiTemplate.InfoPanel: return InfoPanelCommands;
            case UiTemplate.InfoView: return InfoViewCommands;
            case UiTemplate.AppWindow: return AppWindowCommands;
            case UiTemplate.Decision: return DecisionCommands;
            case UiTemplate.Phone: return PhoneCommands;
            default: return GalleryCommands;
        }
    }

    // Each area adds its own screens' cases in a file of its own (UiChecks.<Area>.cs), so jobs on different areas never edit
    // one list; a screen its area gives no case is laid out with its template's own sample. An area with no file yet is a
    // partial method nobody wrote, and the compiler drops its call. A new area adds its line to both lists below.
    private static string UiOnly;   // set by --ui-only: only the screens whose case name begins so
    private static List<UiScreenCase> UiCases()
    {
        var cases = new List<UiScreenCase>();
        foreach (UiScreenDef def in UiScreens.All)
        {
            int before = cases.Count;
            UiCasesContacts(def, cases);
            UiCasesKit(def, cases);
            UiCasesPages(def, cases);
            UiCasesShell(def, cases);
            UiCasesPhone(def, cases);
            UiCasesGrids(def, cases);
            UiCasesHud(def, cases);
            UiCasesBizMan(def, cases);
            UiCasesEconoView(def, cases);
            UiCasesEmployees(def, cases);
            UiCasesPersona(def, cases);
            UiCasesActivity(def, cases);
            UiCasesBizOps(def, cases);
            UiCasesBuy(def, cases);
            UiCasesDialog(def, cases);
            UiCasesHudScreens(def, cases);
            UiCasesWorld(def, cases);
            UiCasesFlare(def, cases);
            UiCasesLife(def, cases);
            UiCasesMoney(def, cases);
            UiCasesNews(def, cases);
            UiCasesSettings(def, cases);
            UiCasesStocks(def, cases);
            UiCasesCompany(def, cases);
            UiCasesConstruction(def, cases);
            UiCasesEstate(def, cases);
            UiCasesServices(def, cases);
            UiCasesTech(def, cases);
            UiCasesVentures(def, cases);
            UiCasesWork(def, cases);
            UiCasesDesigner(def, cases);
            UiCasesMap(def, cases);
            UiCasesOverlays(def, cases);
            UiCasesPause(def, cases);
            UiCasesBlueprints(def, cases);
            UiCasesOptions(def, cases);
            UiCasesSweep(def, cases);
            if (cases.Count == before)
            {
                TemplateCases(def, cases);
            }
        }
        cases.AddRange(PrototypeCases());
        if (UiOnly != null)
        {
            cases.RemoveAll(c => !c.Name.StartsWith(UiOnly, StringComparison.Ordinal));   // a job rechecks its own screen: --ui-only <name>
        }
        return cases;
    }

    static partial void UiCasesContacts(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesKit(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesPages(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesShell(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesPhone(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesGrids(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesHud(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesBizMan(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesEconoView(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesEmployees(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesPersona(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesActivity(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesBizOps(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesBuy(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesDialog(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesHudScreens(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesWorld(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesFlare(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesLife(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesMoney(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesNews(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesSettings(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesStocks(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesCompany(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesConstruction(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesEstate(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesServices(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesTech(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesVentures(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesWork(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesDesigner(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesMap(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesOverlays(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesPause(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesBlueprints(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesOptions(UiScreenDef def, List<UiScreenCase> cases);
    static partial void UiCasesSweep(UiScreenDef def, List<UiScreenCase> cases);

    // The template's own sample: the template_ entries, and every screen whose area adds no case of its own.
    private static void TemplateCases(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Template)
        {
            case UiTemplate.ListDetail:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id, Lay = (s, f, dark, k, t) => UiListDetail.Lay(s, UiSamples.Contacts(t, false), f, dark, k) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a line that takes replies)", Lay = (s, f, dark, k, t) => UiListDetail.Lay(s, UiSamples.Contacts(t, true), f, dark, k) });
                break;
            case UiTemplate.Session:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "Desk", Lay = (s, f, dark, k, t) => UiSessionSteps.Lay(s, UiSamples.Session(t), f, dark, k) });
                break;
            case UiTemplate.Form:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id, Title = "CityAds", Lay = (s, f, dark, k, t) => UiForm.Lay(s, UiSamples.Form(t), f, dark, k) });
                break;
            case UiTemplate.InfoPanel:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a person)", Title = "Rosa Stanley", Lay = (s, f, dark, k, t) => UiInfoPanel.Lay(s, UiSamples.Person(t), f, dark, k) });
                break;
            case UiTemplate.InfoView:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (the city's people)", Title = "The city's people", Lay = (s, f, dark, k, t) => UiInfoView.Lay(s, UiSamples.People(t), f, dark, k) });
                break;
            case UiTemplate.AppWindow:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (the studio, a sidebar)", Title = "Tech", Lay = (s, f, dark, k, t) => UiAppWindow.Lay(s, UiSamples.Studio(t), f, dark, k) });
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (an employee, tabs and a profile)", Title = "Business", Lay = (s, f, dark, k, t) => UiAppWindow.Lay(s, UiSamples.Employee(t), f, dark, k) });
                break;
            case UiTemplate.Decision:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (a desk session)", Title = "Desk", Lay = (s, f, dark, k, t) => UiDecision.Lay(s, UiSamples.Meeting(t), f, dark, k) });
                break;
            case UiTemplate.Phone:
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (home)", Title = "Phone", Lay = (s, f, dark, k, t) => UiPhone.Lay(s, UiSamples.PhoneHome(t), f, dark, k) });
                break;
            case UiTemplate.Gallery:
                for (int page = 0; page < UiGallery.PageCount; page++)
                {
                    int p = page;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " page " + page, Title = "Interface gallery", Lay = (s, f, dark, k, t) => UiGallery.Lay(s, UiSamples.Gallery(t, p), f, dark, k) });
                }
                break;
        }
    }

    // The prototype's Contacts as an app window (DIRECTION_CS2, section 7): the older list and detail drawn in the new window.
    // The prototype's other screens are the new templates' own samples, laid out with their entries above.
    private static IEnumerable<UiScreenCase> PrototypeCases()
    {
        UiScreenDef contacts = Array.Find(UiScreens.All, d => d.Template == UiTemplate.ListDetail) ?? new UiScreenDef("cs2_contacts", UiTemplate.ListDetail, null, new string[0]);   // Contacts stands on the app window now: the prototype's own sample
        yield return new UiScreenCase { Def = contacts, Name = "cs2 contacts app window", Title = "Contacts", Commands = ListDetailCommands,
            Lay = (s, f, dark, k, t) => UiListDetail.Lay(s, UiSamples.ContactsWindow(t), f, dark, k) };
    }

    private static void UiScreenChecks()
    {
        var s = new UiScene();
        var said = new HashSet<string>(StringComparer.Ordinal);
        bool every = Environment.GetEnvironmentVariable("RLO_UI_ALL") != null;   // every finding, in UTF-8, for a pass over the languages
        if (every)
        {
            Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true });
        }
        Action<string> say = line =>
        {
            if ((said.Count < 60 || every) && said.Add(line))
            {
                Console.WriteLine("        " + line);   // each different finding once
            }
        };
        HudBarLanguageChecks();
        string only = Environment.GetEnvironmentVariable("RLO_UI_ONLY");   // a screen's id: the screens' checks of that one alone (with --ui), to iterate on a layout
        foreach (UiScreenCase c in UiCases())
        {
            if (only != null && c.Def.Id != only)
            {
                continue;
            }
            c.Commands = c.Commands ?? Commands(c.Def.Template);
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
                        Check(UiCheckRules.Unfit(s, say) == 0, "ui screens: every label fits whole, and a wrapped text's box holds every line it wraps to: " + where);
                        Check(UiCheckRules.SmallTargets(s, say) == 0, "ui screens: every target at least the least size: " + where);
                        Check(UiCheckRules.TooSmall(s, say) == 0, "ui screens: no text under the smallest size: " + where);
                        Check(UiCheckRules.Crowded(s, say) == 0, "ui screens: one primary action a layer, and no crowd: " + where);
                        Check(c.Overlay || UiCheckRules.NoPrompts(s, say) == 0, "ui screens: a prompt bar that says how to go back: " + where);
                        Check(UiCheckRules.Contradictions(s, say) == 0, "ui screens: no indicator contradicts its value: " + where);
                        Check(UiCheckRules.DeadSpace(s, say) == 0, "ui screens: no frame holds dead space: " + where);
                        Check(UiCheckRules.Unfilled(s, frame, say) == 0, "ui screens: the page uses its screen, anchored at its window's top and across its width: " + where);
                        Check(UiCheckRules.KeyClash(s, say) == 0, "ui screens: one key does one thing: " + where);
                        Check(UiFocusRules.Unreached(s, out string missed) == 0, "ui screens: the d-pad reaches every target: " + where + " (" + missed + ")");
                        Check(UiCheckRules.LowContrast(s, say) == 0, "ui screens: every text and icon stands out (dark): " + where);
                        Check(UiCheckRules.RawKeys(s, say) == 0, "ui screens: no record's key on the screen, only words: " + where);
                        Check(UiCheckRules.Repeats(s, frame, say) == 0, "ui screens: no page repeats its window's title under it: " + where);
                        Check(UiCheckRules.Stacked(s, say) == 0, "ui screens: no label stands over another saying the same thing: " + where);
                        Check(UiCheckRules.EmptyPanels(s, say) == 0, "ui screens: no panel stands empty: " + where);
                        Check(UiCheckRules.Floating(s, say) == 0, "ui screens: an overlay's words stand on a card of their own: " + where);
                        Check(UiCheckRules.Strays(s, say) == 0, "ui screens: no mark lies over a panel it does not belong to: " + where);
                        Check(UiCheckRules.Spines(s, say) == 0, "ui screens: no coloured strip down the edge of a card, row, notice or bar (no spines): " + where);
                        Check(UiCheckRules.TypeLevels(s, say) == 0, "ui screens: three sizes of type at most: " + where);
                        Check(UiCheckRules.UnknownCommands(s, c.Commands, say) == 0, "ui screens: every target runs a command its template answers: " + where);
                        if (lang == "en" && keys == UiDefaultKeys.Keyboard)
                        {
                            // As the mouse sees it: the status bar shows no prompt, still knows the way back, and nothing else moves.
                            UiFrame pointer = frame;
                            pointer.Pointer = true;
                            c.Lay(s, pointer, true, keys, t);
                            Check((c.Overlay || UiCheckRules.NoPrompts(s, say) == 0) && PromptsDrawn(s) == 0 && UiCheckRules.Overlaps(s, say) == 0,
                                "ui screens: with the mouse leading, the status bar shows no prompts and nothing else moves: " + where);
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
        var tall = new UiScene();
        tall.Begin("tall", 400f, 400f, true);
        int wrapped = UiDraw.Text(tall, UiAt.Screen, 8f, 8f, 120f, UiStyle.Body, UiRole.Text, "A wrapped text that takes three lines in a narrow box of words", UiFlags.Wrap, 4);
        Check(UiCheckRules.Unfit(tall, null) == 0, "ui rules: a wrapped text in a box as tall as its lines passes");
        tall.Items[wrapped].H = UiTextRules.LineHeight(UiStyle.Body);
        Check(UiCheckRules.Unfit(tall, null) > 0, "ui rules: a wrapped text in a box lower than the lines it wraps to is found");
        string han = "一二三四五六七八九十";
        float hanWide = UiTextRules.Width(han, UiStyle.Body);
        UiTextRules.EmInHand = true;
        Check(UiTextRules.Lines(han, UiStyle.Body, hanWide + 1f, 0, out float _) == 2 && UiTextRules.Lines("abcdefghij", UiStyle.Body, UiTextRules.Width("abcdefghij", UiStyle.Body) + 1f, 0, out float _) == 1,
            "ui rules: Chinese text keeps an em in hand where TextMeshPro breaks it early, so it is laid in the lines it is drawn in, and Latin text does not");
        UiTextRules.EmInHand = false;
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
        // The owner (2 Nov): "I don't like this spine. Anywhere at all." A cyan strip down a row's edge, 4 px wide, is found; the
        // underline of a chosen tab, a hairline between columns and a meter's fill in its track are not.
        var spine = new UiScene();
        spine.Begin("spine", 800f, 600f, true);
        int shift = UiDraw.Box(spine, UiAt.Screen, 0f, 0f, 200f, 48f, UiRole.Tile, UiRole.None, UiTokens.RadiusMedium);
        UiDraw.Box(spine, UiAt.Screen.In(shift), 0f, 0f, 4f, 48f, UiRole.Chart1, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
        Check(UiCheckRules.Spines(spine, null) == 1, "ui rules: a coloured strip down the edge of a bar is found");
        spine.Items[spine.ItemCount - 1].X = 196f;
        Check(UiCheckRules.Spines(spine, null) == 1, "ui rules: the same strip down the right edge is found");
        spine.Items[spine.ItemCount - 1].Fill = UiRole.Line;
        Check(UiCheckRules.Spines(spine, null) == 0, "ui rules: a neutral hairline down an edge passes");
        spine.Items[spine.ItemCount - 1].Fill = UiRole.Accent;
        spine.Items[spine.ItemCount - 1].X = 0f;
        spine.Items[spine.ItemCount - 1].W = 200f;
        spine.Items[spine.ItemCount - 1].H = 4f;
        spine.Items[spine.ItemCount - 1].Y = 44f;
        Check(UiCheckRules.Spines(spine, null) == 0, "ui rules: the cyan underline of a chosen tab passes");
        spine.Items[spine.ItemCount - 1].Y = 0f;
        Check(UiCheckRules.Spines(spine, null) == 1, "ui rules: a coloured strip along the top of a bar is found");
        spine.Items[spine.ItemCount - 1].Y = 22f;
        spine.Items[spine.ItemCount - 1].H = 4f;
        spine.Items[spine.ItemCount - 1].W = 80f;
        Check(UiCheckRules.Spines(spine, null) == 0, "ui rules: a strip across the middle of a bar is not an edge");
        // The owner's review of the studio's page (1 Nov): a card floating in the middle of the screen, a third of the
        // width empty at either side and gaps over and under it. Anchored under the masthead and across the width it passes.
        UiFrame played = UiFrame.FullMenu(16f / 9f, "Tech");
        played.Room(out float roomX, out float roomY, out float roomW, out float roomH);
        var floating = new UiScene();
        floating.Begin("floating", played.Width, played.Height, true);
        int ground = UiDraw.Box(floating, UiAt.Screen, 0f, 0f, played.Width, played.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);
        int card = UiDraw.Box(floating, UiAt.Screen.In(ground), UiDraw.SnapDown(roomX + (roomW - 880f) / 2f), UiDraw.SnapDown(roomY + (roomH - 588f) / 2f), 880f, 588f,
            UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
        Check(UiCheckRules.Unfilled(floating, played, null) == 3, "ui rules: a card floating in the middle of the page is found at its left, over it and at its right");
        floating.Items[card].X = roomX;
        floating.Items[card].Y = roomY;
        floating.Items[card].W = roomW;
        Check(UiCheckRules.Unfilled(floating, played, null) == 0, "ui rules: the same card anchored under the masthead and across the width passes");
        floating.Items[card].H = UiDraw.Snap(roomH * UiCheckRules.LeastFill * 0.5f);
        Check(UiCheckRules.Unfilled(floating, played, null) == 1, "ui rules: a strip across the top of an empty page is found");
        UiDraw.Text(floating, UiAt.Screen.In(ground), roomX, roomY + roomH - UiTokens.S12, roomW, UiStyle.Title, UiRole.Text, "A line", UiFlags.None);
        Check(UiCheckRules.Unfilled(floating, played, null) == 1, "ui rules: a short line's long box does not count as covering the page");
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
        // Beta 3's faults, each found on a broken scene: a raw key in a table, the masthead's words again under it, a
        // caption over a label that says the same, an empty side box, a call's words straight on the page, five sizes of type.
        var beta = new UiScene();
        beta.Begin("beta 3", played.Width, played.Height, true);
        int sheet = UiDraw.Box(beta, UiAt.Screen, 24f, 100f, 800f, 400f, UiRole.Panel, UiRole.None, 0f, UiFlags.Panel);
        UiDraw.Text(beta, UiAt.Screen.In(sheet), 44f, 120f, 400f, UiStyle.Title, UiRole.Text, "ba:street_fifthavenue|70", UiFlags.Truncate);
        Check(UiCheckRules.RawKeys(beta, null) == 1, "ui rules: a record's key on the screen is found");
        UiDraw.Text(beta, UiAt.Screen.In(sheet), 44f, 160f, 400f, UiStyle.Title, UiRole.Text, "Tech", UiFlags.Label);
        Check(UiCheckRules.Repeats(beta, played, null) == 1, "ui rules: the masthead's words said again under it are found");
        UiDraw.Text(beta, UiAt.Screen.In(sheet), 44f, 200f, 300f, UiStyle.SectionHeader, UiRole.Muted, "Figure", UiFlags.Label);
        UiDraw.Text(beta, UiAt.Screen.In(sheet), 44f, 224f, 300f, UiStyle.SectionHeader, UiRole.Muted, "People", UiFlags.Label);
        Check(UiCheckRules.Stacked(beta, null) == 1, "ui rules: a caption over another label is found");
        UiDraw.Box(beta, UiAt.Screen, 840f, 100f, 380f, 40f, UiRole.Panel, UiRole.None, 0f, UiFlags.Panel);
        Check(UiCheckRules.EmptyPanels(beta, null) == 1, "ui rules: an empty side box is found");
        UiDraw.Text(beta, new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 }, 900f, 300f, 300f, UiStyle.Hero, UiRole.Text, "Maya Okafor", UiFlags.None);
        Check(UiCheckRules.Floating(beta, null) == 1, "ui rules: a call's words straight over the page are found");
        UiDraw.Text(beta, UiAt.Screen.In(sheet), 44f, 260f, 300f, UiStyle.Number, UiRole.Text, "12", UiFlags.None);
        UiDraw.Text(beta, UiAt.Screen.In(sheet), 44f, 300f, 300f, UiStyle.Display, UiRole.Text, "48,210", UiFlags.None);
        Check(UiCheckRules.TypeLevels(beta, null) == 1, "ui rules: more than three sizes of type are found");
        UiDraw.Node(beta, UiAt.Screen, 24f, 600f, 200f, 48f, 0, 0, -1);
        Check(UiCheckRules.UnknownCommands(beta, DecisionCommands, null) == 1, "ui rules: a button whose command its template answers nowhere is found (beta 3's Call read as a group)");
        var games = new UiScene();
        games.Begin("strays", played.Width, played.Height, true);
        int table = UiDraw.Box(games, UiAt.Screen, 24f, 300f, 1000f, 300f, UiRole.Panel, UiRole.None, 0f, UiFlags.Panel);
        int team = UiDraw.Box(games, UiAt.Screen, 1040f, 300f, 380f, 300f, UiRole.Panel, UiRole.None, 0f, UiFlags.Panel);
        UiDraw.Text(games, UiAt.Screen.In(table), 44f, 320f, 400f, UiStyle.Body, UiRole.Text, "No game yet.", UiFlags.None);
        UiDraw.Text(games, UiAt.Screen.In(team), 1060f, 320f, 300f, UiStyle.Small, UiRole.Text2, "Designers (0)", UiFlags.None);
        Check(UiCheckRules.Strays(games, null) == 0, "ui rules: marks inside their own panels pass");
        UiDraw.Text(games, UiAt.Screen, 576f, 380f, 300f, UiStyle.Small, UiRole.Text2, "Engineers (0)", UiFlags.None);
        Check(UiCheckRules.Strays(games, null) == 1, "ui rules: beta 3's team row lying over the games table is found");
        UiDraw.Spark(games, UiAt.Screen.In(table), 900f, 340f, 400f, 40f, new[] { 0f, 1f }, UiRole.Accent);
        Check(UiCheckRules.Spills(games, null) > 0, "ui rules: a sparkline running out of its panel into the next is found");
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

    // ---------------------------------------------------------------- binding over the game's screens

    // Beta 2: the Contacts kit looked for its canvas the plain way while the game's full menu was switched off, found
    // nothing at every load and gave up for the session. Every search of the scene in the kit says whether it takes
    // switched off objects, the canvases are walked as the engine nests them, and a screen not built yet is looked at again.
    private static void UiBindChecks()
    {
        Check(UiBindRules.BlindSearch("Canvas menu = app.GetComponentInParent<Canvas>();") != null, "ui binding: a search for the canvas that skips switched off objects is found (the beta 2 fault)");
        Check(UiBindRules.BlindSearch("rect.SetParent(menu.rootCanvas.transform, false);") != null, "ui binding: the canvas's own root, kept up only while it is on, is found");
        Check(UiBindRules.BlindSearch("var c = go.GetComponentInChildren(typeof(Image));") != null && UiBindRules.BlindSearch("var u = Object.FindObjectOfType<UIs>();") != null,
            "ui binding: a search by type and a search of the whole scene that skip switched off objects are found");
        Check(UiBindRules.BlindSearch("Canvas[] above = app.GetComponentsInParent<Canvas>(true);") == null && UiBindRules.BlindSearch("var t = go.GetComponentsInChildren<TMP_Text>(false);") == null
              && UiBindRules.BlindSearch("// app.GetComponentInParent<Canvas>() skips what is off") == null && UiBindRules.BlindSearch(string.Empty) == null,
            "ui binding: a search that says what it takes, and a comment, are not taken for blind ones");
        // A stand-in for the full menu as the game nests it, nearest canvas first: the app's own canvases inherit their order.
        Check(UiBindRules.Root(3) == 2 && UiBindRules.Sorter(new[] { false, false, false }, 3) == 2, "ui binding: under nested canvases that inherit, the root sets the order and takes the kit");
        Check(UiBindRules.Sorter(new[] { false, true, false }, 3) == 1, "ui binding: the nearest canvas that sorts on its own sets the order");
        Check(UiBindRules.Root(0) == -1 && UiBindRules.Sorter(new bool[0], 0) == -1, "ui binding: no canvas above gives none, not a wrong one");
        Check(UiBindRules.Wait(1) == UiBindRules.FirstWait && UiBindRules.Wait(2) == UiBindRules.FirstWait * 2f && UiBindRules.Wait(30) == UiBindRules.LongestWait
              && !UiBindRules.Stops(UiBindRules.MostTries - 1) && UiBindRules.Stops(UiBindRules.MostTries),
            "ui binding: a screen not built yet is looked at again, later each time, a few times a city");
        float spent = 0f;
        for (int i = 1; i < UiBindRules.MostTries; i++)
        {
            spent += UiBindRules.Wait(i);
        }
        Check(spent >= 120f && spent <= 600f, "ui binding: the looks again span " + spent.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + " s of a city, long enough for a slow phone and short enough to stop");

        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string blind = "none";
        int files = 0;
        foreach (string path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            if (System.IO.Path.GetFileName(path) == "UiBindRules.cs")
            {
                continue;   // the rule's own pattern
            }
            files++;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string found = UiBindRules.BlindSearch(lines[i]);
                if (found != null)
                {
                    blind = System.IO.Path.GetFileName(path) + ":" + (i + 1) + ": " + found;
                }
            }
        }
        Check(files >= 40 && blind == "none", "ui binding: every search of the scene in the kit says whether it takes switched off objects (" + blind + ")");
        string kit = File.ReadAllText(System.IO.Path.Combine(folder, "Host", "UiGameHost.cs"));
        string director = File.ReadAllText(System.IO.Path.Combine(folder, "UiDirector.cs"));
        Check(kit.Contains("GetComponentsInParent<Canvas>(true)") && kit.Contains("UiBindRules.Stops(") && kit.Contains("UiBindRules.Wait(") && director.Contains("ContactsKit.Rearm()"),
            "ui binding: the host binds over the switched off menu, looks again when a screen is not built yet and is looked at afresh each city");
        Check(director.Contains("\"UI: the RLO interface is on; \"") && director.Contains("UiFonts.BundleFile") && director.Contains("ContactsKit.State"),
            "ui binding: the director says once a city that the interface is on, how its fonts came in and which screens it drew over");
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
            "reallife_ui_motion_2", "reallife_opt_sec_ui", "reallife_ui_test_gallery", "reallife_ui_test_contacts", "reallife_ui_test_population", "reallife_ui_page_population", "reallife_ui_test_firm", "reallife_ui_page_studio", "reallife_ui_test_off", "reallife_ui_test_not_ready",
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
