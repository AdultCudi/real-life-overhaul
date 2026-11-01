using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The kit's icons (Scripts/Ui/UiIcon.cs, UiIconRaster.cs, UiIconPaths.Data.cs, UiAssets/scripts/icons.py): every icon but
// the keys' and the pad's glyphs has Phosphor outlines, and a duotone its fill weight too. Each variant fills something
// to see inside its box. A duotone is its faint back under its outline, the rest one solid layer, a fill only solid
// layers. Shapes built for it show the nonzero rule and exact edge coverage. Every fill matches each texel centre's own
// winding number, found apart from the rasterizer, so no hole is inverted, no part lost and the back lies at its tuned
// opacity, and its layers laid over each other one by one give it to the last bit. The pinned files are the ones on
// disk, the committed outlines are what icons.py makes of them, Phosphor's licence ships, and a fill is quick and
// allocates nothing.
internal static partial class Program
{
    private const int UiIconTexels = 96;   // UiIcons' texture
    private const int UiIconProbe = 64;    // texels a side the winding reference is taken at: four units of the box to a texel

    private static void UiIconChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        var rows = UiIconTable(out var fills, out string root);
        UiIconFamily(rows, fills);
        UiIconRules();
        UiIconFills();
        UiIconSources(rows, fills, root);
        UiIconSpeed();
        Console.WriteLine("  Icons: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A glyph drawn in code: the keyboard's arrows and the pad's buttons, which no icon set draws.
    private static bool UiIconInCode(UiIcon icon) => icon >= UiIcon.Arrows && icon <= UiIcon.PadR1;

    // icons.py's rows (UiIcon, Phosphor's name, weight, pin) and its fill pins, read from the script itself.
    private static List<(string Icon, string Name, string Weight, string Pin)> UiIconTable(out Dictionary<string, string> fills, out string root)
    {
        root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
        string script = System.IO.Path.Combine(root, "UiAssets", "scripts", "icons.py");
        string text = File.Exists(script) ? File.ReadAllText(script) : "";
        var rows = new List<(string, string, string, string)>();
        foreach (Match m in Regex.Matches(text, "\\(\"(\\w+)\", \"([a-z0-9-]+)\", \"(regular|thin|light|bold|fill|duotone)\", \"([0-9a-f]{64})\"\\)"))
        {
            rows.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value));
        }
        fills = new Dictionary<string, string>(StringComparer.Ordinal);
        int open = text.IndexOf("FILLS = {", StringComparison.Ordinal);
        int close = open < 0 ? -1 : text.IndexOf("\n}", open, StringComparison.Ordinal);
        if (close > open)
        {
            foreach (Match m in Regex.Matches(text.Substring(open, close - open), "\"(\\w+)\": \"([0-9a-f]{64})\""))
            {
                fills[m.Groups[1].Value] = m.Groups[2].Value;
            }
        }
        Check(rows.Count > 0 && fills.Count > 0, "ui icons: icons.py's table and fill pins are read (" + rows.Count + " rows, " + fills.Count + " fills)");
        return rows;
    }

    // ---------------------------------------------------------------- the family

    private static void UiIconFamily(List<(string Icon, string Name, string Weight, string Pin)> rows, Dictionary<string, string> fills)
    {
        Check(!UiIconRaster.Has(UiIcon.None) && !UiIconRaster.Fill(UiIcon.None, new float[16], 4) && !UiIconRaster.Has(UiIcon.Count),
            "ui icons: None and Count have no outlines");
        var order = new List<string>();
        var weight = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string icon, _, string w, _) in rows)
        {
            weight[icon] = w;
        }
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            var icon = (UiIcon)i;
            if (UiIconInCode(icon))
            {
                Check(!UiIconRaster.Has(icon) && !UiIconRaster.HasFilled(icon), "ui icons: " + icon + " stays drawn in code (UiIcons), with no outlines");
                continue;
            }
            order.Add(icon.ToString());
            bool duotone = weight.TryGetValue(icon.ToString(), out string w) && w == "duotone";
            Check(UiIconRaster.Has(icon), "ui icons: " + icon + " has Phosphor outlines");
            Check(UiIconRaster.HasFilled(icon) == duotone && fills.ContainsKey(icon.ToString()) == duotone,
                "ui icons: " + icon + (duotone ? " (duotone) has its fill weight as a second variant" : " (" + w + ") has no second variant"));
            UiIconLayers(icon, false, duotone ? "duotone" : w);
            if (duotone)
            {
                UiIconLayers(icon, true, "fill");
            }
            else
            {
                Check(UiIconPaths.Of(icon, true) == UiIconPaths.Of(icon, false) && UiIconRaster.Layers(icon, true) == UiIconRaster.Layers(icon, false),
                    "ui icons: " + icon + " asked for filled gives its main weight");
            }
        }
        var listed = new List<string>();
        foreach ((string icon, _, _, _) in rows)
        {
            listed.Add(icon);
        }
        Check(string.Join(",", listed) == string.Join(",", order), "ui icons: icons.py names every icon with outlines once, in UiIcon's order");
    }

    // One variant's layers as the check reads them on its own: a duotone is Phosphor's faint back (0.2) under its outline,
    // a bold or fill weight one solid layer, a fill variant only solid layers. Each draws inside the 256-unit box.
    private static void UiIconLayers(UiIcon icon, bool filled, string kind)
    {
        string name = icon + (filled ? " filled" : "");
        List<UiIconLayer> layers = UiIconRead(UiIconPaths.Of(icon, filled), out bool whole);
        Check(whole && layers.Count == UiIconRaster.Layers(icon, filled), "ui icons: " + name + "'s outlines read whole, layer by layer, to the array's end");
        bool shape = kind == "duotone" ? layers.Count == 2 && layers[0].Opacity == 20 && layers[1].Opacity == 100
                                       : layers.Count >= 1 && layers.TrueForAll(l => l.Opacity == 100) && (kind == "fill" || layers.Count == 1);
        Check(shape, "ui icons: " + name + " is " + (kind == "duotone" ? "two layers, its back at Phosphor's 0.2 under its outline" : kind == "fill" ? "solid layers only" : "one solid layer")
                     + " (" + layers.Count + " layers)");
        bool inside = layers.Count > 0;
        foreach (UiIconLayer l in layers)
        {
            inside &= l.Edges.Count >= 3 && l.MinX >= 0 && l.MinY >= 0 && l.MaxX <= UiIconPaths.Box && l.MaxY <= UiIconPaths.Box;
        }
        Check(inside, "ui icons: " + name + "'s every point lies inside the 256-unit box");
    }

    // ---------------------------------------------------------------- the rules

    // Shapes built to try what Phosphor's own outlines never test: two squares wound the same way stay filled where they
    // overlap (the nonzero rule, where even-odd would punch a hole), a square wound against its frame is a hole, and a
    // square off the texel grid covers its edge texels by exactly their share.
    private static void UiIconRules()
    {
        const int n = 64;   // 16 quarter units to a texel
        var a = new float[n * n];
        int[] Square(int x0, int y0, int x1, int y1, bool clockwise) => clockwise ? new[] { x0, y0, x1, y0, x1, y1, x0, y1 } : new[] { x0, y0, x0, y1, x1, y1, x1, y0 };
        UiIconRaster.Fill(UiIconShape(Square(128, 128, 640, 640, true), Square(384, 384, 896, 896, true)), a, n);
        Check(a[32 * n + 32] == 1f && a[12 * n + 12] == 1f && a[50 * n + 50] == 1f && a[12 * n + 50] == 0f,
            "ui icons: two squares wound the same way fill where they overlap, the nonzero rule SVG fills by (" + a[32 * n + 32] + ")");
        UiIconRaster.Fill(UiIconShape(Square(128, 128, 896, 896, true), Square(384, 384, 640, 640, false)), a, n);
        Check(a[32 * n + 32] == 0f && a[12 * n + 12] == 1f, "ui icons: a square wound against its frame is a hole in it");
        UiIconRaster.Fill(UiIconShape(Square(128, 128, 896, 896, true), Square(384, 384, 640, 640, true)), a, n);
        Check(a[32 * n + 32] == 1f, "ui icons: a square inside a frame wound the same way fills, no hole by even-odd");
        UiIconRaster.Fill(UiIconShape(Square(100, 100, 612, 612, true)), a, n);
        Check(Math.Abs(a[20 * n + 20] - 1f) < 1e-5f && Math.Abs(a[20 * n + 6] - 0.75f) < 1e-5f && Math.Abs(a[6 * n + 6] - 0.5625f) < 1e-5f
              && Math.Abs(a[6 * n + 38] - 0.1875f) < 1e-5f && a[20 * n + 39] == 0f,
            "ui icons: a square off the texel grid covers each edge texel by its exact share (a side " + a[20 * n + 6] + ", a corner " + a[6 * n + 6] + ")");
        UiIconRaster.Fill(UiIconShape(new[] { 0, 0, 1024, 0, 0, 1024 }), a, n);
        float sum = 0f;
        foreach (float v in a)
        {
            sum += v;
        }
        Check(Math.Abs(sum / (n * n) - 0.5f) < 0.002f, "ui icons: a triangle on half the box fills half of it (" + (sum / (n * n)).ToString("0.0000") + ")");
    }

    // One solid layer of the given contours, each a list of x, y in quarter units, in UiIconPaths' form.
    private static short[] UiIconShape(params int[][] contours)
    {
        var p = new List<short> { 1, 100, (short)contours.Length };
        foreach (int[] c in contours)
        {
            p.Add((short)(c.Length / 2));
            for (int i = 0; i < c.Length; i += 2)
            {
                p.Add((short)(c[i] - (i == 0 ? 0 : c[i - 2])));
                p.Add((short)(c[i + 1] - (i == 0 ? 0 : c[i - 1])));
            }
        }
        return p.ToArray();
    }

    // Each layer filled alone and laid over the ones below here (alpha = a + below times one less a) must give the icon's
    // own fill to the last bit, edge texels and all.
    private static void UiIconOver(UiIcon icon, bool filled, string name)
    {
        short[] p = UiIconPaths.Of(icon, filled);
        int n = UiIconProbe, layers = p[0], at = 1;
        if (layers < 2)
        {
            return;
        }
        var want = new float[n * n];
        var one = new float[n * n];
        for (int l = 0; l < layers; l++)
        {
            int start = at, contours = p[at + 1];
            at += 2;
            for (int c = 0; c < contours; c++)
            {
                at += 1 + 2 * p[at];
            }
            var alone = new short[1 + at - start];
            alone[0] = 1;
            Array.Copy(p, start, alone, 1, at - start);
            UiIconRaster.Fill(alone, one, n);
            for (int i = 0; i < want.Length; i++)
            {
                want[i] = one[i] + want[i] * (1f - one[i]);
            }
        }
        UiIconRaster.Fill(icon, one, n, filled);
        float worst = 0f;
        for (int i = 0; i < want.Length; i++)
        {
            worst = Math.Max(worst, Math.Abs(want[i] - one[i]));
        }
        Check(worst < 1e-6f, "ui icons: " + name + "'s layers each laid over the ones below give its fill (worst " + worst + ")");
    }

    // ---------------------------------------------------------------- the fills

    private static void UiIconFills()
    {
        var a = new float[UiIconTexels * UiIconTexels];
        var probe = new float[UiIconProbe * UiIconProbe];
        float thinnest = 1f;
        string thinnestAt = "";
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            var icon = (UiIcon)i;
            if (!UiIconRaster.Has(icon))
            {
                continue;
            }
            foreach (bool filled in UiIconRaster.HasFilled(icon) ? new[] { false, true } : new[] { false })
            {
                string name = icon + (filled ? " filled" : "");
                Check(UiIconRaster.Fill(icon, a, UiIconTexels, filled), "ui icons: " + name + " fills at " + UiIconTexels + " texels");
                float sum = 0f, most = 0f;
                foreach (float v in a)
                {
                    sum += v;
                    most = Math.Max(most, v);
                }
                float share = sum / (UiIconTexels * UiIconTexels);
                if (share < thinnest)
                {
                    thinnest = share;
                    thinnestAt = name;
                }
                Check(most >= 0.999f && share >= 0.02f, "ui icons: " + name + " shows something solid (" + (share * 100f).ToString("0.0") + "% of its box, its most " + most.ToString("0.00") + ")");
                UiIconWinding(icon, filled, name, probe);
                UiIconOver(icon, filled, name);
            }
        }
        Console.WriteLine("    icons: the thinnest " + thinnestAt + " at " + (thinnest * 100f).ToString("0.0") + "% of its box");
    }

    // The fill against each texel centre's winding number, counted apart from the rasterizer. A texel no edge of any layer
    // passes through lies wholly inside or outside each layer, so its value must be its centre's exactly: an inverted hole,
    // a lost part or a back at the wrong opacity shows there. A duotone's back must show somewhere beside its outline.
    private static void UiIconWinding(UiIcon icon, bool filled, string name, float[] probe)
    {
        List<UiIconLayer> layers = UiIconRead(UiIconPaths.Of(icon, filled), out _);
        int n = UiIconProbe, wrong = 0, back = 0, clear = 0;
        float[] want = UiIconReference(layers, n);
        bool[] crossed = UiIconCrossed(layers, n);
        UiIconRaster.Fill(icon, probe, n, filled);
        float worst = 0f;
        for (int i = 0; i < n * n; i++)
        {
            if (crossed[i])
            {
                continue;
            }
            clear++;
            float off = Math.Abs(probe[i] - want[i]);
            worst = Math.Max(worst, off);
            wrong += off > 0.001f ? 1 : 0;
            back += Math.Abs(want[i] - UiIconRaster.DuotoneBack) < 1e-6f ? 1 : 0;
        }
        Check(wrong == 0 && clear > n * n / 2, "ui icons: " + name + " fills every texel no edge crosses as its centre's winding number says (" + wrong + " of " + clear
                                                + " off, worst " + worst.ToString("0.000") + ")");
        if (layers.Count == 2 && layers[0].Opacity == 20)
        {
            Check(back > 0, "ui icons: " + name + "'s back shows at " + UiIconRaster.DuotoneBack + " beside its outline (" + back + " texels)");
        }
    }

    // Every texel an edge of any layer passes through or touches: each edge walked in steps of half a texel, each step
    // marking the texels its own small box spans.
    private static bool[] UiIconCrossed(List<UiIconLayer> layers, int n)
    {
        var crossed = new bool[n * n];
        double scale = n / (double)UiIconPaths.Box;
        foreach (UiIconLayer layer in layers)
        {
            foreach ((int x0, int y0, int x1, int y1) in layer.Edges)
            {
                double ax = x0 * scale, ay = y0 * scale, bx = x1 * scale, by = y1 * scale;
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)) / 0.5));
                for (int s = 0; s < steps; s++)
                {
                    double px = ax + (bx - ax) * s / steps, py = ay + (by - ay) * s / steps;
                    double qx = ax + (bx - ax) * (s + 1) / steps, qy = ay + (by - ay) * (s + 1) / steps;
                    int left = Math.Max(0, (int)Math.Floor(Math.Min(px, qx) - 1e-4)), right = Math.Min(n - 1, (int)Math.Floor(Math.Max(px, qx) + 1e-4));
                    int top = Math.Max(0, (int)Math.Floor(Math.Min(py, qy) - 1e-4)), bottom = Math.Min(n - 1, (int)Math.Floor(Math.Max(py, qy) + 1e-4));
                    for (int y = top; y <= bottom; y++)
                    {
                        for (int x = left; x <= right; x++)
                        {
                            crossed[y * n + x] = true;
                        }
                    }
                }
            }
        }
        return crossed;
    }

    // The layers at each texel centre: inside where a ray to the right crosses edges that do not cancel (the nonzero rule),
    // each layer laid over the ones below at its opacity, Phosphor's 0.2 back at the kit's tuned one.
    private static float[] UiIconReference(List<UiIconLayer> layers, int n)
    {
        var a = new float[n * n];
        var hits = new List<(double X, int Dir)>();
        foreach (UiIconLayer layer in layers)
        {
            float o = layer.Opacity == 100 ? 1f : layer.Opacity == 20 ? UiIconRaster.DuotoneBack : layer.Opacity / 100f;
            for (int row = 0; row < n; row++)
            {
                double y = (row + 0.5) * UiIconPaths.Box / n;
                hits.Clear();
                foreach ((int x0, int y0, int x1, int y1) in layer.Edges)
                {
                    if (y0 != y1 && y >= Math.Min(y0, y1) && y < Math.Max(y0, y1))
                    {
                        hits.Add((x0 + (y - y0) * (x1 - x0) / (y1 - y0), y1 > y0 ? 1 : -1));
                    }
                }
                for (int col = 0; col < n; col++)
                {
                    double x = (col + 0.5) * UiIconPaths.Box / n;
                    int winding = 0;
                    foreach ((double hx, int dir) in hits)
                    {
                        winding += hx > x ? dir : 0;
                    }
                    if (winding != 0)
                    {
                        int i = row * n + col;
                        a[i] = o + a[i] * (1f - o);
                    }
                }
            }
        }
        return a;
    }

    /// <summary>One layer of an icon read by the check on its own: its opacity in hundredths, its edges and its bounds, in quarter units.</summary>
    private sealed class UiIconLayer
    {
        public int Opacity;
        public readonly List<(int X0, int Y0, int X1, int Y1)> Edges = new List<(int, int, int, int)>();
        public int MinX = int.MaxValue, MinY = int.MaxValue, MaxX = int.MinValue, MaxY = int.MinValue;
    }

    // UiIconPaths' array: its layer count, then each layer's opacity and contour count, and each contour's point count, its
    // first point and every next point as its step from the one before.
    private static List<UiIconLayer> UiIconRead(short[] p, out bool whole)
    {
        var layers = new List<UiIconLayer>();
        whole = false;
        if (p == null || p.Length == 0)
        {
            return layers;
        }
        try
        {
            int at = 0, count = p[at++];
            for (int l = 0; l < count; l++)
            {
                var layer = new UiIconLayer { Opacity = p[at++] };
                int contours = p[at++];
                for (int c = 0; c < contours; c++)
                {
                    int points = p[at++], x = 0, y = 0, firstX = 0, firstY = 0;
                    for (int i = 0; i < points; i++)
                    {
                        int nextX = x + p[at++], nextY = y + p[at++];
                        if (i == 0)
                        {
                            firstX = nextX;
                            firstY = nextY;
                        }
                        else
                        {
                            layer.Edges.Add((x, y, nextX, nextY));
                        }
                        x = nextX;
                        y = nextY;
                        layer.MinX = Math.Min(layer.MinX, x);
                        layer.MinY = Math.Min(layer.MinY, y);
                        layer.MaxX = Math.Max(layer.MaxX, x);
                        layer.MaxY = Math.Max(layer.MaxY, y);
                    }
                    layer.Edges.Add((x, y, firstX, firstY));
                }
                layers.Add(layer);
            }
            whole = at == p.Length;
        }
        catch (IndexOutOfRangeException)
        {
            whole = false;   // an array cut short: the check says so rather than stopping the run
        }
        return layers;
    }

    // ---------------------------------------------------------------- the sources

    private static void UiIconSources(List<(string Icon, string Name, string Weight, string Pin)> rows, Dictionary<string, string> fills, string root)
    {
        string icons = System.IO.Path.Combine(root, "UiAssets", "icons");
        bool pinned = rows.Count > 0;
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string icon, string name, string weight, string pin) in rows)
        {
            string file = System.IO.Path.Combine(icons, weight, name + (weight == "regular" ? "" : "-" + weight) + ".svg");
            pinned &= UiIconHash(file) == pin;
            named.Add(System.IO.Path.GetFullPath(file));
            if (fills.TryGetValue(icon, out string fillPin))
            {
                string fill = System.IO.Path.Combine(icons, "fill", name + "-fill.svg");
                pinned &= UiIconHash(fill) == fillPin;
                named.Add(System.IO.Path.GetFullPath(fill));
            }
        }
        Check(pinned, "ui icons: every SVG in UiAssets/icons is the file icons.py pins by its SHA-256 (" + named.Count + " files)");
        int strays = 0;
        if (Directory.Exists(icons))
        {
            foreach (string file in Directory.GetFiles(icons, "*", SearchOption.AllDirectories))
            {
                strays += named.Contains(System.IO.Path.GetFullPath(file)) ? 0 : 1;
            }
        }
        Check(strays == 0, "ui icons: UiAssets/icons holds only the pinned SVGs (" + strays + " others)");
        string licence = System.IO.Path.Combine(root, "UiAssets", "fonts", "LICENSE-Phosphor.txt");
        string words = File.Exists(licence) ? File.ReadAllText(licence) : "";
        Check(words.StartsWith("MIT License", StringComparison.Ordinal) && words.Contains("Copyright (c) 2023 Phosphor Icons", StringComparison.Ordinal),
            "ui icons: Phosphor's MIT licence is in UiAssets/fonts, where the bundle takes the licences it ships");
        string data = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui", "UiIconPaths.Data.cs");
        Check(File.Exists(data) && File.ReadAllText(data).StartsWith("// Generated by UiAssets/scripts/icons.py", StringComparison.Ordinal),
            "ui icons: the outlines are the file icons.py writes");
        (int code, string said) = UiIconGenerator(root);
        Check(code == 0 && said.Contains("ICONS: outlines as the icons give them", StringComparison.Ordinal),
            "ui icons: icons.py --check finds the committed outlines what the pinned SVGs give (exit " + code + ": " + said.Trim() + ")");
    }

    private static string UiIconHash(string file) => File.Exists(file) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant() : "missing";

    // The generator's own check (py -3 -I UiAssets/scripts/icons.py --check), run from the repository's root.
    private static (int Code, string Said) UiIconGenerator(string root)
    {
        var start = new ProcessStartInfo("py", "-3 -I UiAssets/scripts/icons.py --check")
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        try
        {
            using (Process p = Process.Start(start))
            {
                var output = p.StandardOutput.ReadToEndAsync();
                var errors = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(120000))
                {
                    p.Kill();
                    return (-1, "still running after two minutes");
                }
                return (p.ExitCode, output.Result + errors.Result);
            }
        }
        catch (Win32Exception e)
        {
            return (-1, "Python 3 with fontTools is needed to run it (" + e.Message + ")");
        }
    }

    // ---------------------------------------------------------------- the cost

    private static void UiIconSpeed()
    {
        var a = new float[UiIconTexels * UiIconTexels];
        UiIconPass(a);   // the scratch grows to the largest icon once
        double best = double.MaxValue;
        for (int pass = 0; pass < 5; pass++)
        {
            best = Math.Min(best, UiIconPass(a));
        }
        long bytes = UiIconBytes(a);
        int variants = 0;
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            variants += UiIconRaster.Has((UiIcon)i) ? (UiIconRaster.HasFilled((UiIcon)i) ? 2 : 1) : 0;
        }
        double each = best / variants;
        Check(each < 0.5, "ui icons: a fill at " + UiIconTexels + " texels takes " + each.ToString("0.000") + " ms, under 0.5 ms here, room to spare under 2 ms on the game's slower Mono");
        Check(bytes == 0, "ui icons: filling every icon again allocates nothing (" + bytes + " bytes), the scratch is kept from icon to icon");
        Console.WriteLine("    icons: " + variants + " variants, " + each.ToString("0.000") + " ms a fill at " + UiIconTexels + " texels");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double UiIconPass(float[] a)
    {
        var watch = Stopwatch.StartNew();
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            UiIconRaster.Fill((UiIcon)i, a, UiIconTexels, false);
            if (UiIconRaster.HasFilled((UiIcon)i))
            {
                UiIconRaster.Fill((UiIcon)i, a, UiIconTexels, true);
            }
        }
        return watch.Elapsed.TotalMilliseconds;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long UiIconBytes(float[] a)
    {
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i < (int)UiIcon.Count; i++)
        {
            UiIconRaster.Fill((UiIcon)i, a, UiIconTexels, false);
            if (UiIconRaster.HasFilled((UiIcon)i))
            {
                UiIconRaster.Fill((UiIcon)i, a, UiIconTexels, true);
            }
        }
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }
}
