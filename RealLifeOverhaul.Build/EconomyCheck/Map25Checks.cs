using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Map25;

// The minimap's 2.5D picture's game-free parts (Scripts/Ui/Map): the camera's arithmetic, the blocks, their budget (vertices, triangles,
// bytes, build time), that a build is the same twice, the flat view having no height, the ground (the coast, the roads), the route's strip,
// and the per frame work (the view, the chunks in view) allocating nothing and costing a few microseconds. The look's rules are in
// Map25LookChecks.cs (heights, tanks, trees, marks) and the city map's logic in Map25LogicChecks.cs (the full map is the game's own city,
// and what the map button's tilt gives back). What only the game can show is in docs/features/navigation.md, "Not verified".
internal static partial class Program
{
    private static void Map25Checks()
    {
        Map25MathChecks();
        Map25Map city = Map25Build();
        Map25BlockChecks(city);
        Map25GroundChecks();
        Map25FrameChecks(city);
        Map25MinimapChecks();
        Map25TextChecks();
        Map25LookChecks(city);
        Map25LogicChecks();
    }

    // The minimap's place, order and avoid rect are the HUD fix's (Navigation/NavPlaceRules, UiHostRules.MinimapOrder, UiMinimapChecks); the
    // 2.5D picture lives inside the card's view and must leave all three alone, and must never take the pointer from the card's backdrop.
    private static void Map25MinimapChecks()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Mods",
                                                                          "RealLifeOverhaul", "Scripts", "Navigation"));
        string[] forbidden = { "_rect.anchoredPosition", "_rect.sizeDelta", "_rect.anchorMin", "_rect.anchorMax", "_canvas", "sortingOrder",
                               "overrideSorting", "HasRoom", "NavPlaceRules", "AvoidX", "SetAsFirstSibling", "SetParent(uis" };
        string touches = "none", pointer = "none";
        foreach (string name in new[] { "NavHud.Scene.cs", "NavHud.Strip.cs" })
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(folder, name));
            foreach (string word in forbidden)
            {
                if (text.Contains(word))
                {
                    touches = name + " names " + word;
                }
            }
            int made = Map25Count(text, "AddComponent<Image>") + Map25Count(text, "AddComponent<RawImage>");
            int clear = Map25Count(text, "raycastTarget = false");
            if (made > clear)
            {
                pointer = name + " makes " + made + " graphics and lets " + clear + " go of the pointer";
            }
        }
        Check(touches == "none", "2.5D minimap: its own files never write the card's place, size or canvas order, or the avoid rect (" + touches + ")");
        Check(pointer == "none", "2.5D minimap: every picture and plate it adds lets the pointer through, so the card's own backdrop is what the pointer hits over it (" + pointer + ")");
        Check(RealLifeOverhaul.Ui.UiHostRules.MinimapOrder(RealLifeOverhaul.Ui.UiHostRules.Above(2)) > RealLifeOverhaul.Ui.UiHostRules.Above(2)
              && RealLifeOverhaul.Ui.UiHostRules.MinimapOrder(RealLifeOverhaul.Ui.UiHostRules.Above(2)) < RealLifeOverhaul.Ui.UiHostRules.Above(21),
            "2.5D minimap: the card's canvas is still over the kit HUD's and under the game's windows");
    }

    private static int Map25Count(string text, string word)
    {
        int count = 0, at = 0;
        while ((at = text.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += word.Length;
        }
        return count;
    }

    private sealed class Map25Map
    {
        public Map25City City;
        public Map25Set Small;
    }

    private static Map25Map Map25Build()
    {
        var map = new Map25Map { City = Map25Fixture.City(7) };
        map.Small = Map25Set.Build(map.City, -0.3f);
        return map;
    }

    private static void Map25MathChecks()
    {
        bool halves = true;
        foreach (float sample in new[] { 0f, 0.0123f, 0.25f, 0.5f, 0.75f, 1f, 0.00008f, 0.9995f })
        {
            float back = FromHalf(Map25Math.Half(sample));
            halves &= Math.Abs(back - sample) <= Math.Max(sample * 0.001f, 0.00001f);
        }
        Check(halves && Map25Math.Half(1f) == 0x3C00 && Map25Math.Half(0.5f) == 0x3800, "2.5D map: half floats hold a colour to a tenth of a per cent (8 bit linear would band the darks)");
        Check(Math.Abs(Map25Math.Linear(0.5f) - 0.2140f) < 0.001f && Math.Abs(Map25Builder.ToLight(0.5f) - Map25Math.Linear(0.5f)) < 0.0001f && Map25Builder.ToLight(1f) == 1f && Map25Builder.ToLight(0f) == 0f,
            "2.5D map: colours go from how they are written to light, once, by a table that agrees with the power to 1 part in 10,000");

        var view = new Map25View();
        view.Look(100f, 5f, -40f, 0f, 40f, 260f, 1f);
        view.Project(100f, 5f, -40f, out float u, out float v);
        bool middle = Math.Abs(u - 0.5f) < 0.001f && Math.Abs(v - 0.5f) < 0.001f;
        view.Project(100f, 5f, -40f + 60f, out _, out float vNorth);
        view.Project(100f + 60f, 5f, -40f, out float uEast, out _);
        Check(middle && vNorth > 0.5f && uEast > 0.5f, "2.5D map: the camera looks at its target, north is up the picture and east to the right at heading 0");
        view.Look(0f, 0f, 0f, 90f, 40f, 260f, 1f);
        view.Project(60f, 0f, 0f, out _, out float vEast);
        Check(vEast > 0.5f, "2.5D map: at heading 90 east is up the picture");
        view.Look(0f, 0f, 0f, 0f, 40f, 260f, 1f);
        view.Project(0f, 0f, 130f, out _, out _);
        bool near = view.Project(130f, 0f, 0f, out float ue, out _);
        Check(near && ue > 0.8f && ue < 1.3f, "2.5D map: the metres across the picture are the metres asked for, at the target");
        var chunk = new Map25Chunk { MinX = -10, MaxX = 10, MinY = 0, MaxY = 30, MinZ = -10, MaxZ = 10 };
        var away = new Map25Chunk { MinX = 5000, MaxX = 5020, MinY = 0, MaxY = 30, MinZ = 5000, MaxZ = 5020 };
        view.Look(0f, 0f, 0f, 0f, 40f, 260f, 1f);
        Check(view.Sees(chunk, true) && !view.Sees(away, true), "2.5D map: a chunk in view is drawn and one far away is not");
        var game0 = new Map25View();
        game0.Look(0f, 0f, 0f, 0f, 47f, 260f, 1f);
        view.Look(0f, 0f, 0f, 0f, 90f, 260f, 1f);
        Check(view.Rise < 0.01f && Map25View.RiseOf(47f) == 1f && Map25View.RiseOf(60f) == 1f && Map25View.RiseOf(70f) > Map25View.RiseOf(75f) && Map25View.RiseOf(85f) < 0.01f,
            "2.5D map: from straight above the blocks have no height (flat), tipped to the game's 47 degrees they stand full, and between they ease");
        bool tiltedEverywhere = true;
        for (float d = 100f; d <= 350f; d += 10f)
        {
            var open = new Map25View();
            open.Set(game0.View, game0.Projection, Map25View.PitchFor(d), 0f);
            tiltedEverywhere &= Map25View.PitchFor(d) == Map25View.TiltPitch && open.Rise == 1f && RealLifeOverhaul.Nav.NavMapTilt.Pitch(RealLifeOverhaul.Nav.NavMapTilt.Auto, d) == Map25View.TiltPitch;
        }
        Check(tiltedEverywhere, "2.5D map: the tilt is the game's 47 degrees with the blocks at full height (rise 1) at every distance the game allows, 100 to 350 m, and the map button's default state asks for no other pitch");
        // the game's view matrices put the scene's ground at the median street: a point of the scene is where the world's point lowered by that street is
        var game = new Map25View();
        game.Look(300f, 40f, -80f, 20f, 47f, 400f, 16f / 9f);
        var lowered = new Map25View();
        lowered.Set(game.View, game.Projection, 47f, 40f);
        bool same = true;
        foreach (float[] p in new[] { new[] { 300f, 40f, -80f }, new[] { 340f, 62f, -20f }, new[] { 250f, 40f, -140f } })
        {
            game.Project(p[0], p[1], p[2], out float gu, out float gv);
            lowered.Project(p[0], p[1] - 40f, p[2], out float lu, out float lv);
            same &= Math.Abs(gu - lu) < 1e-4f && Math.Abs(gv - lv) < 1e-4f;
        }
        Check(same && Math.Abs(lowered.View[13] - game.View[13]) > 1f, "2.5D map: the game's view is lowered by the median street's height, so a scene point lies on the picture where the game's own point 40 m higher does (exact, at any height of the city's ground)");
        lowered.GroundAhead(out float gx, out float gz);
        Check(Math.Abs(gx - 300f) < 0.5f && Math.Abs(gz + 80f) < 0.5f, "2.5D map: the ground the middle of the picture shows is found from the view (" + gx.ToString("F1") + ", " + gz.ToString("F1") + ")");
        view.Look(0f, 0f, 0f, 0f, 90f, 800f, 16f / 9f);
        float flatMetres = view.MetresPerPixel(0f, 0f, 0f, 1920);
        Check(Math.Abs(flatMetres - 800f / 1920f) < 0.01f, "2.5D map: a pixel is the metres across the picture over its width (" + flatMetres.ToString("F3") + " m)");
        var rng = new Map25Rng(5);
        var same2 = new Map25Rng(5);
        bool repeat = true, spread = true;
        for (int i = 0; i < 100; i++)
        {
            repeat &= rng.Next() == same2.Next();
        }
        var counts = new int[5];
        for (int i = 0; i < 5000; i++)
        {
            counts[rng.Int(5)]++;
        }
        foreach (int c in counts)
        {
            spread &= c > 800 && c < 1200;
        }
        Check(repeat && spread, "2.5D map: the seeded generator repeats and spreads");
    }

    private static void Map25BlockChecks(Map25Map map)
    {
        Map25City city = map.City;
        int most = 0;
        bool inside = true, bright = true, solid = true;
        Map25Set set = map.Small;
        for (int k = 0; k < set.Layers.Length; k++)
        {
            bool translucent = k == (int)Map25Kind.Contacts;
            foreach (Map25Chunk chunk in set.Layers[k].Chunks)
            {
                most = Math.Max(most, chunk.Vertices);
                for (int i = 0; i < chunk.Vertices && inside; i++)
                {
                    inside &= !float.IsNaN(chunk.Pos[i * 3]) && !float.IsNaN(chunk.Pos[i * 3 + 1]) && !float.IsNaN(chunk.Pos[i * 3 + 2])
                              && chunk.Pos[i * 3] >= chunk.MinX - 0.001f && chunk.Pos[i * 3] <= chunk.MaxX + 0.001f && chunk.Pos[i * 3 + 1] >= chunk.MinY - 0.001f && chunk.Pos[i * 3 + 1] <= chunk.MaxY + 0.001f;
                    float r = FromHalf(chunk.Col[i * 4]), g = FromHalf(chunk.Col[i * 4 + 1]), b = FromHalf(chunk.Col[i * 4 + 2]), a = FromHalf(chunk.Col[i * 4 + 3]);
                    bright &= r >= 0f && b >= 0f && !(r > 0.9f && g > 0.9f && b > 0.9f);   // no vertex is white or light
                    solid &= translucent ? a > 0f && a < 1f : a == 1f;
                }
                for (int i = 0; i < chunk.Indices && inside; i++)
                {
                    inside &= chunk.Idx[i] < chunk.Vertices;
                }
            }
        }
        Check(inside && most <= Map25Builder.MostVertices, "2.5D map: every vertex of every kind is inside its chunk's box, no index leaves its chunk, and no chunk passes the 16 bit index (" + most + " of " + Map25Builder.MostVertices + " vertices at the most)");
        Check(bright && solid, "2.5D map: no vertex is white or light, the blocks and crowns are solid and the contact patches are translucent");
        // the budget: the fixture has the real city's grain, and its numbers are scaled to the owner's 883 buildings
        double perBuilding = map.Small.Vertices / (double)city.Count, scale = 883.0 / city.Count;
        double smallMb = map.Small.Vertices * scale * 20 / 1048576.0 + map.Small.Triangles * scale * 6 / 1048576.0;
        if (Environment.GetEnvironmentVariable("RLO_MAP25_REPORT") == "1")
        {
            Console.WriteLine("map25: " + city.Count + " buildings: " + map.Small.Blocks.Length + " chunks, " + map.Small.Vertices + " vertices, " + map.Small.Triangles + " triangles, "
                              + map.Small.Milliseconds.ToString("F1") + " ms; scaled to 883 buildings " + smallMb.ToString("F1") + " MB; " + perBuilding.ToString("F0") + " vertices a building");
        }
        Check(smallMb < 3.4 && perBuilding < 260 && map.Small.Triangles * scale < 125000,
            "2.5D map: the budget holds for a city of 883 buildings: at most 3.4 MB and 125,000 triangles (" + smallMb.ToString("F1") + " MB, " + (map.Small.Triangles * scale).ToString("F0") + " triangles)");
        Check(map.Small.Milliseconds < 400, "2.5D map: the set is built in well under a second here (" + map.Small.Milliseconds.ToString("F0") + " ms), on a pool thread in the game");

        Map25Set again = Map25Set.Build(city, -0.3f);
        bool identical = again.Vertices == map.Small.Vertices && again.Triangles == map.Small.Triangles;
        for (int l = 0; l < Map25Builders.Kinds && identical; l++)
        {
            identical &= again.Layers[l].Chunks.Length == map.Small.Layers[l].Chunks.Length;
            for (int k = 0; identical && k < again.Layers[l].Chunks.Length; k++)
            {
                Map25Chunk a = again.Layers[l].Chunks[k], b = map.Small.Layers[l].Chunks[k];
                identical &= a.Vertices == b.Vertices && a.Indices == b.Indices && Same(a.Pos, b.Pos, a.Vertices * 3) && Same(a.Col, b.Col, a.Vertices * 4) && Same(a.Idx, b.Idx, a.Indices);
            }
        }
        Check(identical, "2.5D map: the same city gives the same blocks, crowns and patches, byte for byte");

        // a tower has more in it than a low building
        var one = new Map25City();
        one.Reserve(1, 4);
        one.HullStart[0] = 0;
        one.HullStart[1] = 4;
        float[] hx = { 0f, 40f, 40f, 0f }, hz = { 0f, 0f, 30f, 30f };
        Array.Copy(hx, one.HullX, 4);
        Array.Copy(hz, one.HullZ, 4);
        one.Height[0] = 50f;
        one.Key[0] = 0x1234;
        var gen = new Map25Blocks();
        var b1 = new Map25Builders(4096);
        gen.Generate(b1, one, 0);
        int tall = b1.Vertices;
        one.Height[0] = 12f;
        b1.Clear();
        gen.Generate(b1, one, 0);
        int low = b1.Vertices;
        Check(tall > low, "2.5D map: a tower has more in it than a low building (" + tall + " and " + low + " vertices)");
    }

    // The number a half float's bits stand for (the check's own: the mod only writes them).
    private static float FromHalf(ushort half)
    {
        int sign = (half & 0x8000) != 0 ? -1 : 1;
        int exponent = (half >> 10) & 0x1F;
        int mantissa = half & 0x3FF;
        if (exponent == 0)
        {
            return sign * mantissa * (float)Math.Pow(2.0, -24);
        }
        return exponent == 31 ? (mantissa == 0 ? sign * float.PositiveInfinity : float.NaN) : sign * (1f + mantissa / 1024f) * (float)Math.Pow(2.0, exponent - 15);
    }

    private static bool Same(float[] a, float[] b, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static bool Same(ushort[] a, ushort[] b, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static void Map25GroundChecks()
    {
        Map25Fixture.Roads(out float[] x, out float[] y, out float[] z, out int[] es, out int[] et, out float[] ex, out bool[] us, out float[] sp, out int n);
        Map25Chunk[] roads = Map25Ground.Roads(x, y, z, es, et, ex, us, sp, n, -1500f, -800f, 3500f, 1700f, 0f, out Map25Chunk[] lanes);
        int vertices = 0, edges = 0, fastEdges = 0, dashVertices = 0;
        bool limit = true;
        foreach (Map25Chunk c in roads)
        {
            vertices += c.Vertices;
            limit &= c.Vertices <= Map25Builder.MostVertices;
        }
        foreach (Map25Chunk c in lanes)
        {
            dashVertices += c.Vertices;
            limit &= c.Vertices <= Map25Builder.MostVertices;
        }
        for (int i = 0; i < n; i++)
        {
            for (int e = es[i]; e < es[i + 1]; e++)
            {
                bool lane = !(ex[e] > 0f || !us[et[e]]);
                edges += lane ? 1 : 0;
                fastEdges += lane && sp[i] >= Map25Ground.FastLane ? 1 : 0;
            }
        }
        Check(limit && vertices == edges * 4 && edges > 1000, "2.5D map: every lane that costs nothing extra is one band of the road (" + edges + " lanes, " + vertices + " vertices), none past the chunk limit");
        Check(dashVertices == fastEdges * 4 && fastEdges > 500 && fastEdges < edges, "2.5D map: every lane of an avenue has one dash of lane marking, and a street's lanes have none (" + fastEdges + " dashes of " + edges + " lanes)");
        // the extra edges (changes of lane) are left out
        ex[0] = 30f;
        Map25Chunk[] fewer = Map25Ground.Roads(x, y, z, es, et, ex, us, sp, n, -1500f, -800f, 3500f, 1700f, 0f, out _);
        int fewerVertices = 0;
        foreach (Map25Chunk c in fewer)
        {
            fewerVertices += c.Vertices;
        }
        ex[0] = 0f;
        Check(fewerVertices == vertices - 4 || fewerVertices < vertices, "2.5D map: a change of lane is no road");

        Map25Fixture.Water(out byte[] cells, out int columns, out int rows, out float cell, out float ox, out float oz);
        long began = Stopwatch.GetTimestamp();
        Map25Chunk[] land = Map25Ground.Land(cells, columns, rows, cell, ox, oz, -0.4f);
        double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
        double area = 0;
        int landVertices = 0;
        bool landLimit = true;
        foreach (Map25Chunk c in land)
        {
            landLimit &= c.Vertices <= Map25Builder.MostVertices;
            landVertices += c.Vertices;
            for (int i = 0; i + 2 < c.Indices; i += 3)
            {
                int a = c.Idx[i], b = c.Idx[i + 1], d = c.Idx[i + 2];
                area += Math.Abs((c.Pos[b * 3] - c.Pos[a * 3]) * (c.Pos[d * 3 + 2] - c.Pos[a * 3 + 2]) - (c.Pos[d * 3] - c.Pos[a * 3]) * (c.Pos[b * 3 + 2] - c.Pos[a * 3 + 2])) * 0.5;
            }
        }
        int landCells = 0;
        foreach (byte c in cells)
        {
            landCells += c == 1 ? 0 : 1;
        }
        double cellArea = landCells * (double)cell * cell;
        Check(landLimit && Math.Abs(area - cellArea) / cellArea < 0.04, "2.5D map: the land's outline holds the land's area to 4 per cent (" + (area / 1e6).ToString("F2") + " of " + (cellArea / 1e6).ToString("F2") + " square km), with the coast at an angle and not in steps");
        Check(ms < 300 && landVertices < 80000, "2.5D map: the land is made in well under a third of a second and under 80,000 vertices (" + ms.ToString("F0") + " ms, " + landVertices + ")");

        var b2 = new Map25Builder(256);
        var rx = new float[] { 0f, 50f, 50.3f, 50f, 100f };
        var rz = new float[] { 0f, 0f, 0.1f, 40f, 40f };
        var ry = new float[5];
        int used = Map25Ground.Ribbon(b2, rx, ry, rz, 5, 3f, Map25Palette.Route, 1f, 0.1f, 0f);
        Check(used == 4 && b2.Vertices == used * 2 && b2.Indices == (used - 1) * 6, "2.5D map: a route is one strip of two vertices a point (a point under a metre from the last is left out)");
    }

    // The part of a frame that is the mod's own arithmetic: the view, which chunks are in it, and where the pins stand. The game's
    // part (the draw into the texture) is a command buffer of some fifty draws and is not measurable outside the game.
    private static void Map25FrameChecks(Map25Map map)
    {
        var view = new Map25View();
        var marksX = new float[32];
        var marksZ = new float[32];
        for (int i = 0; i < 32; i++)
        {
            marksX[i] = (i % 8) * 25f;
            marksZ[i] = (i / 8) * 25f;
        }
        Frames(view, map, marksX, marksZ, 200);   // warms the code
        long before = GC.GetAllocatedBytesForCurrentThread();
        long began = Stopwatch.GetTimestamp();
        int drawn = Frames(view, map, marksX, marksZ, 5000);
        double microseconds = (Stopwatch.GetTimestamp() - began) * 1e6 / Stopwatch.Frequency / 5000.0;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        int chunks = 0;
        foreach (Map25Layer layer in map.Small.Layers)
        {
            chunks += layer.Chunks.Length;
        }
        if (Environment.GetEnvironmentVariable("RLO_MAP25_REPORT") == "1")
        {
            Console.WriteLine("map25 frame: " + microseconds.ToString("F2") + " us, " + bytes + " bytes in 5000 frames, " + drawn / 5000.0 + " chunks of " + chunks + " drawn a frame");
        }
        Check(bytes == 0, "2.5D map: a frame's own arithmetic (the view, the chunks in view, 32 pins) allocates nothing (" + bytes + " bytes in 5,000 frames)");
        Check(microseconds < 60.0, "2.5D map: that arithmetic costs under 60 microseconds a frame (" + microseconds.ToString("F2") + ")");
        Check(drawn / 5000 < 40 && drawn > 0, "2.5D map: some chunks of the city are in a view, never all of it (" + drawn / 5000 + " a frame, of " + chunks + ")");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int Frames(Map25View view, Map25Map map, float[] marksX, float[] marksZ, int count)
    {
        int drawn = 0;
        float sum = 0f;
        for (int f = 0; f < count; f++)
        {
            view.Look(f % 400 - 200f, 0f, f % 300 - 150f, f * 0.37f, 40f, 260f, 1f);
            for (int l = 0; l < map.Small.Layers.Length; l++)
            {
                Map25Chunk[] chunks = map.Small.Layers[l].Chunks;
                for (int k = 0; k < chunks.Length; k++)
                {
                    drawn += view.Sees(chunks[k], true) ? 1 : 0;
                }
            }
            for (int m = 0; m < marksX.Length; m++)
            {
                if (view.Project(marksX[m], 0f, marksZ[m], out float u, out float v))
                {
                    sum += u + v;
                }
            }
        }
        return sum == 12345.678f ? -1 : drawn;
    }

    private static void Map25TextChecks()
    {
        var en = LocaleFile("en");
        var pt = LocaleFile("pt");
        var zh = LocaleFile("zh-cn");
        string missing = "none";
        foreach (string key in new[] { "reallife_nav_minimap25", "reallife_opt_sec_citymap", "reallife_opt_tip_nav_minimap25" })
        {
            if (!en.ContainsKey(key) || !pt.ContainsKey(key) || !zh.ContainsKey(key) || string.IsNullOrWhiteSpace(en[key]) || string.IsNullOrWhiteSpace(pt[key]) || string.IsNullOrWhiteSpace(zh[key]))
            {
                missing = key;
            }
        }
        Check(missing == "none", "2.5D map: the minimap option's words and the city map section's title are in English, Portuguese and Chinese (" + missing + ")");
        // the city map's own option is gone from every language: it shows the game's city, so there is nothing to switch
        string left = "none";
        foreach (string language in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            var texts = LocaleFile(language);
            if (texts.ContainsKey("reallife_nav_map25") || texts.ContainsKey("reallife_opt_tip_nav_map25") || texts.ContainsKey("reallife_contract_part_map_work"))
            {
                left = language;
            }
        }
        Check(left == "none", "city map: the city map's 2.5D option and the contract's words for it are gone from every language (" + left + ")");
        // What's new: the sentence is in the same row of every language (the next row, or the version's once numbered), and the minimap alone draws the blocks (the other sentence is the map button's)
        string blocksRow = WhatsNewKey(LocaleFile("en"), "The minimap draws buildings as simple 2.5D blocks");
        string lacking = "none";
        foreach (string language in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            var texts = LocaleFile(language);
            if (WhatsNewText(texts, blocksRow).Split(new[] { "2.5D" }, StringSplitOptions.None).Length != 2)
            {
                lacking = language;
            }
        }
        Check(lacking == "none", "city map: What's new names the 2.5D blocks once, for the minimap, in all eleven languages (" + lacking + ")");
    }
}
