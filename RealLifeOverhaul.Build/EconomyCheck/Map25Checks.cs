using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Map25;

// The 2.5D map's game-free parts (Scripts/Ui/Map): the camera's arithmetic, the blocks every detail of the approved mockups is
// drawn with, their budget (vertices, triangles, bytes, build time), that a build is the same twice, that a tint changes colours of one
// building and nothing else, the flat view having no height, the ground (the coast, the roads), the route's strip, and the per frame
// work (the view, the chunks in view, the pins' places) allocating nothing and costing a few microseconds. The look's rules are in
// Map25LookChecks.cs (heights, windows, awnings, tanks, trees, marks) and the logic's in Map25LogicChecks.cs (what the picture reads from
// the game and when, what it gives back). What only the game can show (the shader, the picture's place on the screen, the pins on the
// roofs) is in docs/features/navigation.md, "Not verified".
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
        Map25NameChecks();
        Map25MinimapChecks();
        Map25TextChecks();
        Map25LookChecks(city);
        Map25LogicChecks();
    }

    // The street names on the full map: a label every 260 m or so along each named road, on its middle line, running the way the road runs.
    private static void Map25NameChecks()
    {
        var x = new List<float>();
        var z = new List<float>();
        var road = new List<int>();
        for (int lane = -1; lane <= 1; lane++)
        {
            for (int k = 0; k <= 100; k++)
            {
                x.Add(lane * 3f);
                z.Add(k * 10f);
                road.Add(0);
            }
        }
        for (int lane = 0; lane < 2; lane++)
        {
            for (int k = 0; k <= 30; k++)
            {
                x.Add(k * 10f);
                z.Add(500f + (lane == 0 ? -2f : 2f));
                road.Add(1);
            }
        }
        for (int k = 0; k <= 30; k++)
        {
            x.Add(400f + k * 10f);
            z.Add(100f);
            road.Add(2);
        }
        for (int k = 0; k <= 4; k++)
        {
            x.Add(900f + k * 5f);
            z.Add(900f);
            road.Add(3);
        }
        int n = x.Count;
        var usable = new bool[n];
        var junction = new bool[n];
        for (int i = 0; i < n; i++)
        {
            usable[i] = true;
        }
        string[] names = { "Fifth Avenue", "Fortieth Street", null, "Alley" };
        Map25Names found = Map25Names.Anchors(road.ToArray(), x.ToArray(), z.ToArray(), usable, junction, n, names);
        int avenue = 0, street = 0, other = 0;
        bool onTheMiddle = true, ways = true;
        for (int i = 0; i < found.Count; i++)
        {
            if (found.Name[i] == "Fifth Avenue")
            {
                avenue++;
                onTheMiddle &= Math.Abs(found.X[i]) < 0.5f;
                ways &= Math.Abs(found.DirZ[i]) > 0.99f;
            }
            else if (found.Name[i] == "Fortieth Street")
            {
                street++;
                onTheMiddle &= Math.Abs(found.Z[i] - 500f) < 0.5f;
                ways &= Math.Abs(found.DirX[i]) > 0.99f;
            }
            else
            {
                other++;
            }
        }
        Check(avenue == 3 && street == 1 && other == 0, "2.5D map: a named road has a label every 260 m or so, an unnamed one and one under 36 m none (" + avenue + " on the avenue, " + street + " on the street, " + other + " elsewhere)");
        Check(onTheMiddle && ways, "2.5D map: a label stands on its road's middle line, between its lanes, and runs the way the road runs");
        bool spaced = found.Count == 4;
        for (int i = 0; i < found.Count && spaced; i++)
        {
            for (int j = i + 1; j < found.Count; j++)
            {
                if (found.Name[i] == found.Name[j] && found.Name[i] == "Fifth Avenue")
                {
                    spaced &= Math.Abs(found.Z[i] - found.Z[j]) > 250f;
                }
            }
        }
        Check(spaced, "2.5D map: two labels of one avenue are more than 250 m apart");
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
        Check(tiltedEverywhere, "2.5D map: the opened map has the game's 47 degrees and the blocks at full height (rise 1) at every distance the game allows, 100 to 350 m");
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
        foreach (Map25Set set in new[] { map.Full, map.Small })
        {
            for (int k = 0; k < set.Layers.Length; k++)
            {
                bool translucent = k == (int)Map25Kind.Panes || k == (int)Map25Kind.Contacts;
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
        }
        Check(inside && most <= Map25Builder.MostVertices, "2.5D map: every vertex of every kind is inside its chunk's box, no index leaves its chunk, and no chunk passes the 16 bit index (" + most + " of " + Map25Builder.MostVertices + " vertices at the most)");
        Check(bright && solid, "2.5D map: no vertex is white or light, the blocks, crowns and awnings are solid and the panes and contact patches are translucent");
        int built = 0;
        bool whole = true;
        for (int i = 0; i < city.Count; i++)
        {
            built += map.Full.Count[i] > 0 ? 1 : 0;
            whole &= map.Full.Count[i] > 0 && map.Small.Count[i] > 0 && map.Full.First[i] >= 0 && map.Full.First[i] + map.Full.Count[i] <= map.Full.Blocks[map.Full.ChunkOf[i]].Vertices;
            for (int k = 0; k < Map25Builders.Kinds; k++)
            {
                Map25Layer layer = map.Full.Layers[k];
                whole &= layer.Count[i] == 0 || (layer.First[i] >= 0 && layer.First[i] + layer.Count[i] <= layer.Chunks[layer.ChunkOf[i]].Vertices);
            }
        }
        Check(whole && built == city.Count, "2.5D map: every building has its own run of vertices in its chunk, in every kind and both details (" + built + " of " + city.Count + ")");
        // the budget: the fixture has the real city's grain, and its numbers are scaled to the owner's 883 buildings
        double perBuilding = map.Full.Vertices / (double)city.Count, scale = 883.0 / city.Count;
        double fullMb = map.Full.Vertices * scale * 20 / 1048576.0 + map.Full.Triangles * scale * 6 / 1048576.0;
        double smallMb = map.Small.Vertices * scale * 20 / 1048576.0 + map.Small.Triangles * scale * 6 / 1048576.0;
        double panes = map.Full.Layers[(int)Map25Kind.Panes].Vertices * scale;
        if (Environment.GetEnvironmentVariable("RLO_MAP25_REPORT") == "1")
        {
            Console.WriteLine("map25: " + city.Count + " buildings: full " + map.Full.Blocks.Length + " chunks, " + map.Full.Vertices + " vertices (" + panes.ToString("F0") + " of windows scaled), " + map.Full.Triangles + " triangles, "
                              + map.Full.Milliseconds.ToString("F1") + " ms; small " + map.Small.Vertices + " vertices, " + map.Small.Triangles + " triangles, " + map.Small.Milliseconds.ToString("F1")
                              + " ms; scaled to 883 buildings " + fullMb.ToString("F1") + " MB and " + smallMb.ToString("F1") + " MB; " + perBuilding.ToString("F0") + " vertices a building");
        }
        Check(fullMb < 11.5 && smallMb < 3.4 && perBuilding < 560 && map.Full.Triangles * scale < 240000 && map.Small.Triangles * scale < 125000 && panes < 340000,
            "2.5D map: the budget holds for a city of 883 buildings: at most 11.5 MB, 240,000 triangles and 340,000 vertices of windows in the full detail, 3.4 MB and 125,000 triangles in the minimap's (" + fullMb.ToString("F1") + " and " + smallMb.ToString("F1") + " MB)");
        Check(map.Full.Milliseconds < 700 && map.Small.Milliseconds < 400, "2.5D map: both sets are built in well under a second here (" + map.Full.Milliseconds.ToString("F0") + " and " + map.Small.Milliseconds.ToString("F0") + " ms), on a pool thread in the game");

        Map25Set again = Map25Set.Build(city, Map25Detail.Full, -0.3f);
        bool identical = again.Vertices == map.Full.Vertices && again.Triangles == map.Full.Triangles;
        for (int l = 0; l < Map25Builders.Kinds && identical; l++)
        {
            identical &= again.Layers[l].Chunks.Length == map.Full.Layers[l].Chunks.Length;
            for (int k = 0; identical && k < again.Layers[l].Chunks.Length; k++)
            {
                Map25Chunk a = again.Layers[l].Chunks[k], b = map.Full.Layers[l].Chunks[k];
                identical &= a.Vertices == b.Vertices && a.Indices == b.Indices && Same(a.Pos, b.Pos, a.Vertices * 3) && Same(a.Col, b.Col, a.Vertices * 4) && Same(a.Idx, b.Idx, a.Indices);
            }
        }
        Check(identical, "2.5D map: the same city gives the same blocks, crowns, awnings, windows and patches, byte for byte");

        // the details: a tower has more than a low building, and the minimap's detail has fewer than the full map's
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
        gen.Generate(b1, one, 0, Map25Detail.Full, true);
        int tall = b1.Vertices;
        one.Height[0] = 12f;
        b1.Clear();
        gen.Generate(b1, one, 0, Map25Detail.Full, true);
        int low = b1.Vertices;
        one.Height[0] = 50f;
        b1.Clear();
        gen.Generate(b1, one, 0, Map25Detail.Small, true);
        int small = b1.Vertices;
        Check(tall > low && small < tall, "2.5D map: a tower has more in it than a low building, and the minimap's detail has fewer than the full map's (" + tall + ", " + low + " and " + small + " vertices)");
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

    private static void Map25TintChecks(Map25Map map)
    {
        Map25City city = map.City;
        int hull = 5;
        var gen = new Map25Blocks();
        var scratch = new Map25Builders(4096);
        // keep a copy of every colour and every place of every kind of the full set
        var colours = new ushort[Map25Builders.Kinds][][];
        var places = new float[Map25Builders.Kinds][][];
        for (int l = 0; l < Map25Builders.Kinds; l++)
        {
            Map25Chunk[] chunks = map.Full.Layers[l].Chunks;
            colours[l] = new ushort[chunks.Length][];
            places[l] = new float[chunks.Length][];
            for (int k = 0; k < chunks.Length; k++)
            {
                colours[l][k] = (ushort[])chunks[k].Col.Clone();
                places[l][k] = (float[])chunks[k].Pos.Clone();
            }
        }
        city.Tint[hull] = Map25Colour.Hex(0x3FCF8E);
        city.TintShare[hull] = Map25Palette.TintShare;
        bool written = map.Full.Retint(city, hull, gen, scratch);
        bool onlyHere = true, changed = false, moved = false, panesKept = true;
        for (int l = 0; l < Map25Builders.Kinds; l++)
        {
            Map25Layer layer = map.Full.Layers[l];
            for (int k = 0; k < layer.Chunks.Length; k++)
            {
                Map25Chunk chunk = layer.Chunks[k];
                for (int i = 0; i < chunk.Vertices; i++)
                {
                    bool mine = k == layer.ChunkOf[hull] && i >= layer.First[hull] && i < layer.First[hull] + layer.Count[hull];
                    bool differs = chunk.Col[i * 4] != colours[l][k][i * 4] || chunk.Col[i * 4 + 1] != colours[l][k][i * 4 + 1] || chunk.Col[i * 4 + 2] != colours[l][k][i * 4 + 2];
                    onlyHere &= (mine && layer.Tints) || !differs;
                    changed |= mine && differs;
                    panesKept &= layer.Tints || !differs;
                }
                moved |= !Same(chunk.Pos, places[l][k], chunk.Vertices * 3);
            }
        }
        Map25Layer blocks = map.Full.Layers[(int)Map25Kind.Blocks];
        Map25Chunk tinted = blocks.Chunks[blocks.ChunkOf[hull]];
        Check(written && onlyHere && changed && !moved && panesKept && tinted.ColourVersion > 0 && tinted.DirtyFrom == blocks.First[hull] && tinted.DirtyTo == blocks.First[hull] + blocks.Count[hull] - 1,
            "2.5D map: tinting a building rewrites the colours of its own vertices in the blocks, crowns and awnings and nothing else (not a window, not a patch, no other building), moves no vertex, and tells the renderer which range to send");
        city.TintShare[hull] = 0f;
        map.Full.Retint(city, hull, gen, scratch);
        bool restored = true;
        for (int l = 0; l < Map25Builders.Kinds && restored; l++)
        {
            for (int k = 0; k < map.Full.Layers[l].Chunks.Length && restored; k++)
            {
                restored &= Same(map.Full.Layers[l].Chunks[k].Col, colours[l][k], map.Full.Layers[l].Chunks[k].Vertices * 4);
            }
        }
        Check(restored, "2.5D map: taking a tint away gives back the colours it had, exactly");
        // the minimap's set is never written by a tint: it has no kind that tints
        bool smallPlain = true;
        foreach (Map25Layer layer in map.Small.Layers)
        {
            smallPlain &= !layer.Tints;
        }
        Check(smallPlain && map.Full.Layers[(int)Map25Kind.Blocks].Tints && map.Full.Layers[(int)Map25Kind.Crowns].Tints && map.Full.Layers[(int)Map25Kind.Fronts].Tints
              && !map.Full.Layers[(int)Map25Kind.Panes].Tints && !map.Full.Layers[(int)Map25Kind.Contacts].Tints,
            "2.5D map: a tint is written into the full map's blocks, crowns and awnings only: the minimap's set, the windows and the patches are never rewritten");
        long before = GC.GetAllocatedBytesForCurrentThread();
        Retints(map, gen, scratch, 200);
        long bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;
        Check(bytes < 4096, "2.5D map: a tint costs a few hundred bytes of garbage at the most and no mesh is built (" + bytes + " bytes a tint)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void Retints(Map25Map map, Map25Blocks gen, Map25Builders scratch, int times)
    {
        for (int t = 0; t < times; t++)
        {
            int i = t % map.City.Count;
            map.City.TintShare[i] = t % 2 == 0 ? Map25Palette.TintShare : 0f;
            map.Full.Retint(map.City, i, gen, scratch);
        }
        for (int i = 0; i < map.City.Count; i++)
        {
            map.City.TintShare[i] = 0f;
        }
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
        foreach (string key in new[] { "reallife_nav_minimap25", "reallife_nav_map25", "reallife_opt_sec_citymap", "reallife_opt_tip_nav_minimap25", "reallife_opt_tip_nav_map25" })
        {
            if (!en.ContainsKey(key) || !pt.ContainsKey(key) || !zh.ContainsKey(key) || string.IsNullOrWhiteSpace(en[key]) || string.IsNullOrWhiteSpace(pt[key]) || string.IsNullOrWhiteSpace(zh[key]))
            {
                missing = key;
            }
        }
        Check(missing == "none", "2.5D map: the two options' words, the new section's title and the two sentences are in English, Portuguese and Chinese (" + missing + ")");
        // What's new: the sentence is in `reallife_changes_next` of every language, and says both maps
        string lacking = "none";
        foreach (string language in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            var texts = LocaleFile(language);
            if (!texts.TryGetValue("reallife_changes_next", out string next) || !next.Contains("2.5D"))
            {
                lacking = language;
            }
        }
        Check(lacking == "none", "2.5D map: What's new names the 2.5D city map and minimap in all eleven languages (" + lacking + ")");
    }
}
