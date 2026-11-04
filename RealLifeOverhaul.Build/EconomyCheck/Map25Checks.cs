using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Map25;

// The 2.5D map's game-free parts (Scripts/Ui/Map): the camera's arithmetic, the blocks every detail of the approved mockups is
// drawn with, their budget (vertices, triangles, bytes, build time), that a build is the same twice, that a tint changes colours of one
// building and nothing else, the flat view having no height, the ground (the coast, the roads), the route's strip, and the per frame
// work (the view, the chunks in view, the pins' places) allocating nothing and costing a few microseconds. What only the game can
// show (the shader, the picture's place on the screen, the pins on the roofs) is in docs/features/navigation.md, "Not verified".
internal static partial class Program
{
    private static void Map25Checks()
    {
        Map25MathChecks();
        Map25Map city = Map25Build();
        Map25BlockChecks(city);
        Map25TintChecks(city);
        Map25GroundChecks();
        Map25FrameChecks(city);
        Map25TextChecks();
    }

    private sealed class Map25Map
    {
        public Map25City City;
        public Map25Set Full, Small;
    }

    private static Map25Map Map25Build()
    {
        var map = new Map25Map { City = Map25Fixture.City(7) };
        map.Full = Map25Set.Build(map.City, Map25Detail.Full, -0.3f);
        map.Small = Map25Set.Build(map.City, Map25Detail.Small, -0.3f);
        return map;
    }

    private static void Map25MathChecks()
    {
        bool halves = true;
        foreach (float sample in new[] { 0f, 0.0123f, 0.25f, 0.5f, 0.75f, 1f, 0.00008f, 0.9995f })
        {
            float back = Map25Math.FromHalf(Map25Math.Half(sample));
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
        bool round = true;
        foreach (float[] spot in new[] { new[] { 0.2f, 0.3f }, new[] { 0.8f, 0.7f }, new[] { 0.5f, 0.5f } })
        {
            if (view.Ground(spot[0], spot[1], 0f, out float gx, out float gz) && view.Project(gx, 0f, gz, out float pu, out float pv))
            {
                round &= Math.Abs(pu - spot[0]) < 0.002f && Math.Abs(pv - spot[1]) < 0.002f;
            }
            else
            {
                round = false;
            }
        }
        Check(round, "2.5D map: a spot of the picture and the ground it shows go back and forth");
        var chunk = new Map25Chunk { MinX = -10, MaxX = 10, MinY = 0, MaxY = 30, MinZ = -10, MaxZ = 10 };
        var away = new Map25Chunk { MinX = 5000, MaxX = 5020, MinY = 0, MaxY = 30, MinZ = 5000, MaxZ = 5020 };
        view.Look(0f, 0f, 0f, 0f, 40f, 260f, 1f);
        Check(view.Sees(chunk, true) && !view.Sees(away, true), "2.5D map: a chunk in view is drawn and one far away is not");
        view.Look(0f, 0f, 0f, 0f, 90f, 260f, 1f);
        Check(view.Rise < 0.01f && Map25View.RiseOf(47f) == 1f && Map25View.RiseOf(60f) == 1f && Map25View.RiseOf(70f) > Map25View.RiseOf(75f) && Map25View.RiseOf(85f) < 0.01f,
            "2.5D map: from straight above the blocks have no height (flat), tipped to the game's 47 degrees they stand full, and between they ease");
        Check(Map25View.PitchFor(350f) == Map25View.FlatPitch && Map25View.PitchFor(200f) == Map25View.TiltPitch && Map25View.PitchFor(100f) == Map25View.TiltPitch
              && Map25View.PitchFor(250f) < Map25View.PitchFor(290f) && Map25View.PitchFor(250f) > Map25View.TiltPitch, "2.5D map: the full map's camera is flat from afar and tipped to the game's own 47 degrees close, eased between");
        var rng = new Map25Rng(5);
        var same = new Map25Rng(5);
        bool repeat = true, spread = true;
        for (int i = 0; i < 100; i++)
        {
            repeat &= rng.Next() == same.Next();
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
        int most = 0, mostChunk = 0;
        bool inside = true, bright = true;
        foreach (Map25Set set in new[] { map.Full, map.Small })
        {
            foreach (Map25Chunk chunk in set.Blocks)
            {
                most = Math.Max(most, chunk.Vertices);
                mostChunk = Math.Max(mostChunk, chunk.Indices);
                for (int i = 0; i < chunk.Vertices && inside; i++)
                {
                    inside &= !float.IsNaN(chunk.Pos[i * 3]) && !float.IsNaN(chunk.Pos[i * 3 + 1]) && !float.IsNaN(chunk.Pos[i * 3 + 2])
                              && chunk.Pos[i * 3] >= chunk.MinX - 0.001f && chunk.Pos[i * 3] <= chunk.MaxX + 0.001f && chunk.Pos[i * 3 + 1] >= chunk.MinY - 0.001f && chunk.Pos[i * 3 + 1] <= chunk.MaxY + 0.001f;
                    float r = Map25Math.FromHalf(chunk.Col[i * 4]), g = Map25Math.FromHalf(chunk.Col[i * 4 + 1]), b = Map25Math.FromHalf(chunk.Col[i * 4 + 2]), a = Map25Math.FromHalf(chunk.Col[i * 4 + 3]);
                    bright &= a == 1f && r >= 0f && b >= 0f && !(r > 0.9f && g > 0.9f && b > 0.9f);   // no vertex is white or light
                }
                for (int i = 0; i < chunk.Indices && inside; i++)
                {
                    inside &= chunk.Idx[i] < chunk.Vertices;
                }
            }
        }
        Check(inside && most <= Map25Builder.MostVertices, "2.5D map: every block's vertex is inside its chunk's box, no index leaves its chunk, and no chunk passes the 16 bit index (" + most + " of " + Map25Builder.MostVertices + " vertices at the most)");
        Check(bright, "2.5D map: no vertex of any block is white or light, and every vertex is solid");
        int built = 0;
        bool whole = true;
        for (int i = 0; i < city.Count; i++)
        {
            built += map.Full.Count[i] > 0 ? 1 : 0;
            whole &= map.Full.Count[i] > 0 && map.Small.Count[i] > 0 && map.Full.First[i] >= 0 && map.Full.First[i] + map.Full.Count[i] <= map.Full.Blocks[map.Full.ChunkOf[i]].Vertices;
        }
        Check(whole && built == city.Count, "2.5D map: every building has its own run of vertices in its chunk, in both details (" + built + " of " + city.Count + ")");
        // the budget: model numbers for the owner's real city (883 buildings) are scaled from the fixture's own
        double perBuilding = map.Full.Vertices / (double)city.Count, scale = 883.0 / city.Count;
        double fullMb = map.Full.Vertices * scale * 20 / 1048576.0 + map.Full.Triangles * scale * 6 / 1048576.0;
        double smallMb = map.Small.Vertices * scale * 20 / 1048576.0 + map.Small.Triangles * scale * 6 / 1048576.0;
        if (Environment.GetEnvironmentVariable("RLO_MAP25_REPORT") == "1")
        {
            Console.WriteLine("map25: " + city.Count + " buildings: full " + map.Full.Blocks.Length + " chunks, " + map.Full.Vertices + " vertices, " + map.Full.Triangles + " triangles, "
                              + map.Full.Milliseconds.ToString("F1") + " ms; small " + map.Small.Vertices + " vertices, " + map.Small.Triangles + " triangles, " + map.Small.Milliseconds.ToString("F1")
                              + " ms; scaled to 883 buildings " + fullMb.ToString("F1") + " MB and " + smallMb.ToString("F1") + " MB; " + perBuilding.ToString("F0") + " vertices a building");
        }
        Check(fullMb < 8.5 && smallMb < 4.5 && perBuilding < 420 && map.Full.Triangles * scale < 220000 && map.Small.Triangles * scale < 110000,
            "2.5D map: the budget holds for a city of 883 buildings: at most 8.5 MB and 220,000 triangles in the full detail, 4.5 MB and 110,000 in the minimap's (" + fullMb.ToString("F1") + " and " + smallMb.ToString("F1") + " MB)");
        Check(map.Full.Milliseconds < 600 && map.Small.Milliseconds < 400, "2.5D map: both sets are built in well under a second here (" + map.Full.Milliseconds.ToString("F0") + " and " + map.Small.Milliseconds.ToString("F0") + " ms), on a pool thread in the game");

        Map25Set again = Map25Set.Build(city, Map25Detail.Full, -0.3f);
        bool identical = again.Blocks.Length == map.Full.Blocks.Length && again.Vertices == map.Full.Vertices;
        for (int k = 0; identical && k < again.Blocks.Length; k++)
        {
            Map25Chunk a = again.Blocks[k], b = map.Full.Blocks[k];
            identical &= a.Vertices == b.Vertices && a.Indices == b.Indices && Same(a.Pos, b.Pos, a.Vertices * 3) && Same(a.Col, b.Col, a.Vertices * 4) && Same(a.Idx, b.Idx, a.Indices);
        }
        Check(identical, "2.5D map: the same city gives the same blocks, byte for byte");

        // the details: a tall building has bands, a cornice and a setback; a shop has an awning only on its north and south walls
        var one = new Map25City();
        one.Reserve(1, 4);
        one.HullStart[0] = 0;
        one.HullStart[1] = 4;
        float[] hx = { 0f, 40f, 40f, 0f }, hz = { 0f, 0f, 30f, 30f };
        Array.Copy(hx, one.HullX, 4);
        Array.Copy(hz, one.HullZ, 4);
        one.Height[0] = 90f;
        one.Key[0] = 0x1234;
        var gen = new Map25Blocks();
        var b1 = new Map25Builder(4096);
        gen.Generate(b1, one, 0, Map25Detail.Full);
        int tall = b1.Vertices;
        one.Height[0] = 12f;
        b1.Clear();
        gen.Generate(b1, one, 0, Map25Detail.Full);
        int low = b1.Vertices;
        one.Height[0] = 90f;
        b1.Clear();
        gen.Generate(b1, one, 0, Map25Detail.Small);
        int small = b1.Vertices;
        Check(tall > low * 2 && small < tall, "2.5D map: a tower has more bands and a setback than a low building, and the minimap's detail has fewer than the full map's (" + tall + ", " + low + " and " + small + " vertices)");
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

    private static void Map25TintChecks(Map25Map map)
    {
        Map25City city = map.City;
        int hull = 5;
        var gen = new Map25Blocks();
        var scratch = new Map25Builder(4096);
        // keep a copy of every colour and every place of the full set
        var colours = new ushort[map.Full.Blocks.Length][];
        var places = new float[map.Full.Blocks.Length][];
        for (int k = 0; k < colours.Length; k++)
        {
            colours[k] = (ushort[])map.Full.Blocks[k].Col.Clone();
            places[k] = (float[])map.Full.Blocks[k].Pos.Clone();
        }
        city.Tint[hull] = Map25Colour.Hex(0x3FCF8E);
        city.TintShare[hull] = Map25Palette.TintShare;
        bool written = map.Full.Retint(city, hull, gen, scratch) && map.Small.Retint(city, hull, gen, scratch);
        int chunkOf = map.Full.ChunkOf[hull], first = map.Full.First[hull], count = map.Full.Count[hull];
        bool onlyHere = true, changed = false, moved = false;
        for (int k = 0; k < colours.Length; k++)
        {
            Map25Chunk chunk = map.Full.Blocks[k];
            for (int i = 0; i < chunk.Vertices; i++)
            {
                bool mine = k == chunkOf && i >= first && i < first + count;
                bool differs = chunk.Col[i * 4] != colours[k][i * 4] || chunk.Col[i * 4 + 1] != colours[k][i * 4 + 1] || chunk.Col[i * 4 + 2] != colours[k][i * 4 + 2];
                onlyHere &= mine || !differs;
                changed |= mine && differs;
            }
            moved |= !Same(chunk.Pos, places[k], chunk.Vertices * 3);
        }
        Map25Chunk tinted = map.Full.Blocks[chunkOf];
        Check(written && onlyHere && changed && !moved && tinted.ColourVersion > 0 && tinted.DirtyFrom == first && tinted.DirtyTo == first + count - 1,
            "2.5D map: tinting a building rewrites the colours of its own vertices in both details and nothing else, moves no vertex, and tells the renderer which range to send");
        city.TintShare[hull] = 0f;
        map.Full.Retint(city, hull, gen, scratch);
        map.Small.Retint(city, hull, gen, scratch);
        bool restored = true;
        for (int k = 0; k < colours.Length && restored; k++)
        {
            restored &= Same(map.Full.Blocks[k].Col, colours[k], map.Full.Blocks[k].Vertices * 4);
        }
        Check(restored, "2.5D map: taking a tint away gives back the colours it had, exactly");
        long before = GC.GetAllocatedBytesForCurrentThread();
        Retints(map, gen, scratch, 200);
        long bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;
        Check(bytes < 4096, "2.5D map: a tint costs a few hundred bytes of garbage at the most and no mesh is built (" + bytes + " bytes a tint)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void Retints(Map25Map map, Map25Blocks gen, Map25Builder scratch, int times)
    {
        for (int t = 0; t < times; t++)
        {
            int i = t % map.City.Count;
            map.City.TintShare[i] = t % 2 == 0 ? Map25Palette.TintShare : 0f;
            map.Full.Retint(map.City, i, gen, scratch);
            map.Small.Retint(map.City, i, gen, scratch);
        }
        for (int i = 0; i < map.City.Count; i++)
        {
            map.City.TintShare[i] = 0f;
        }
    }

    private static void Map25GroundChecks()
    {
        Map25Fixture.Roads(out float[] x, out float[] y, out float[] z, out int[] es, out int[] et, out float[] ex, out bool[] us, out float[] sp, out int n);
        Map25Chunk[] roads = Map25Ground.Roads(x, y, z, es, et, ex, us, sp, n, -1500f, -800f, 3500f, 1700f, 0f);
        int vertices = 0, edges = 0;
        bool limit = true;
        foreach (Map25Chunk c in roads)
        {
            vertices += c.Vertices;
            limit &= c.Vertices <= Map25Builder.MostVertices;
        }
        for (int i = 0; i < n; i++)
        {
            for (int e = es[i]; e < es[i + 1]; e++)
            {
                edges += ex[e] > 0f || !us[et[e]] ? 0 : 1;
            }
        }
        Check(limit && vertices == edges * 4 && edges > 1000, "2.5D map: every lane that costs nothing extra is one band of the road (" + edges + " lanes, " + vertices + " vertices), none past the chunk limit");
        // the extra edges (changes of lane) are left out
        ex[0] = 30f;
        Map25Chunk[] fewer = Map25Ground.Roads(x, y, z, es, et, ex, us, sp, n, -1500f, -800f, 3500f, 1700f, 0f);
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
    // part (the draw into the texture) is a command buffer of at most 50 draws and is not measurable outside the game.
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
        if (Environment.GetEnvironmentVariable("RLO_MAP25_REPORT") == "1")
        {
            Console.WriteLine("map25 frame: " + microseconds.ToString("F2") + " us, " + bytes + " bytes in 5000 frames, " + drawn / 5000.0 + " chunks of " + (map.Full.Blocks.Length + map.Full.Shadows.Length) + " drawn a frame");
        }
        Check(bytes == 0, "2.5D map: a frame's own arithmetic (the view, the chunks in view, 32 pins) allocates nothing (" + bytes + " bytes in 5,000 frames)");
        Check(microseconds < 60.0, "2.5D map: that arithmetic costs under 60 microseconds a frame (" + microseconds.ToString("F2") + ")");
        Check(drawn / 5000 < 25 && drawn > 0, "2.5D map: about a dozen chunks of the city are in a view, never all of it (" + drawn / 5000 + " a frame, of " + map.Full.Blocks.Length + ")");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static int Frames(Map25View view, Map25Map map, float[] marksX, float[] marksZ, int count)
    {
        int drawn = 0;
        float sum = 0f;
        for (int f = 0; f < count; f++)
        {
            view.Look(f % 400 - 200f, 0f, f % 300 - 150f, f * 0.37f, 40f, 260f, 1f);
            for (int k = 0; k < map.Small.Blocks.Length; k++)
            {
                drawn += view.Sees(map.Small.Blocks[k], true) ? 1 : 0;
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
        foreach (string key in new[] { "reallife_nav_minimap25", "reallife_nav_map25" })
        {
            if (!en.ContainsKey(key) || !pt.ContainsKey(key) || !zh.ContainsKey(key) || string.IsNullOrWhiteSpace(en[key]) || string.IsNullOrWhiteSpace(pt[key]) || string.IsNullOrWhiteSpace(zh[key]))
            {
                missing = key;
            }
        }
        Check(missing == "none", "2.5D map: the two options' words are in English, Portuguese and Chinese (" + missing + ")");
    }
}
