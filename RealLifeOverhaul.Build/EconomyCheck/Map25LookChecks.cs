using System;
using System.Collections.Generic;
using System.Reflection;
using RealLifeOverhaul.Map25;

// The 2.5D map's look, as rules with numbers (round 2 of the job: the critic's thirteen defects, one check each where the game-free code can say):
// the heights (a knee and a cap, so a thin real lot is a block and not a needle), the windows (a pane each, 4.4 by 3.1 m, a few lit), the foot
// shading in metres, the setback only where it reads, the awnings (a strip that stands out, on the north and south walls), the water tank (a drum
// on four legs), the lane marking, the trees and the parks they make, the sizes of the route and the view cone in pixels, the ring and the roof
// line of a tinted building, the game's zones, and which kinds of drawing a view of a given height has. The previews prove the look by eye (real
// footprints, see docs/features/navigation.md); these keep it from going back.
internal static partial class Program
{
    private static void Map25LookChecks(Map25Map map)
    {
        Map25HeightChecks(map);
        Map25PaneChecks(map);
        Map25ShapeChecks();
        Map25UnderChecks();
        Map25MarkChecks(map);
        Map25RuleChecks();
        Map25RealChecks();
    }

    // The owner's own footprints (883 buildings, the file their minimap keeps), when it is on this machine: the budget and the height policy on the real grain,
    // with the ordinary model and the deliberately tall one. Nothing is checked, and nothing said, where the file is not.
    private static void Map25RealChecks()
    {
        string file = Environment.GetEnvironmentVariable("RLO_MAP25_CITY");
        if (string.IsNullOrEmpty(file))
        {
            file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Hovgaard Games", "Big Ambitions", "RealLifeOverhaul", "minimap_base_3682.bin");
        }
        if (!System.IO.File.Exists(file))
        {
            return;
        }
        foreach (bool tall in new[] { false, true })
        {
            Map25Real real = Map25Real.Load(file, tall);
            Check(real != null && real.City.Count > 800, "2.5D map: the owner's footprints are read from their minimap file (" + (real != null ? real.City.Count : 0) + " buildings)");
            if (real == null)
            {
                return;
            }
            Map25Set full = Map25Set.Build(real.City, Map25Detail.Full, -0.3f), small = Map25Set.Build(real.City, Map25Detail.Small, -0.3f);
            double fullMb = (full.Vertices * 20.0 + full.Triangles * 6.0) / 1048576.0, smallMb = (small.Vertices * 20.0 + small.Triangles * 6.0) / 1048576.0;
            var sx = new float[32];
            var sz = new float[32];
            int needles = 0, high = 0;
            float tallest = 0f;
            for (int i = 0; i < real.City.Count; i++)
            {
                Map25Height.Measure(real.City, i, sx, sz, out float shorter, out float longer);
                float h = real.City.Height[i];
                needles += h / shorter > 4.7f || h > 2f * longer + 10.01f ? 1 : 0;
                high += h > 50f ? 1 : 0;
                tallest = Math.Max(tallest, h);
            }
            Check(fullMb < 11.5 && smallMb < 3.4 && full.Vertices / (double)real.City.Count < 560 && full.Layers[(int)Map25Kind.Panes].Vertices < 340000 && needles == 0 && tallest < 100f && high < real.City.Count / 12,
                "2.5D map: on the owner's own 883 footprints (" + (tall ? "deliberately tall" : "ordinary") + " heights) the full detail is " + fullMb.ToString("F1") + " MB, the minimap's " + smallMb.ToString("F1")
                + " MB, " + (full.Vertices / (double)real.City.Count).ToString("F0") + " vertices a building, no block is a needle, the tallest is " + tallest.ToString("F0") + " m and " + high + " are over 50 m");
        }
    }

    // One building of a given ground and height, drawn into fresh builders (its height is taken as the drawn one).
    private static Map25Builders Map25One(float width, float depth, float height, long key, bool full, bool panes = true)
    {
        var city = new Map25City();
        city.Reserve(1, 4);
        city.HullStart[0] = 0;
        city.HullStart[1] = 4;
        float[] hx = { 0f, width, width, 0f }, hz = { 0f, 0f, depth, depth };
        Array.Copy(hx, city.HullX, 4);
        Array.Copy(hz, city.HullZ, 4);
        city.Height[0] = height;
        city.Key[0] = key;
        var builders = new Map25Builders(8192);
        new Map25Blocks().Generate(builders, city, 0, full ? Map25Detail.Full : Map25Detail.Small, panes);
        return builders;
    }

    private static void Map25HeightChecks(Map25Map map)
    {
        bool low = Math.Abs(Map25Height.Drawn(12f, 15f, 40f) - 12f) < 1e-4f && Math.Abs(Map25Height.Drawn(29f, 15f, 40f) - 29f) < 1e-4f && Math.Abs(Map25Height.Knee(15f) - 29f) < 1e-4f;
        float tower = Map25Height.Drawn(100f, 15f, 40f);
        Check(low && Math.Abs(tower - (29f + 71f * Map25Height.Above)) < 1e-3f,
            "2.5D map: a building up to the knee of its lot (8 m and 1.4 m a metre of its shorter side) is drawn exactly as tall as it is, above it 15 per cent of the rest (100 m on a 15 m lot is " + tower.ToString("F1") + " m)");
        float chimney = Map25Height.Drawn(150f, 10f, 10f), slab = Map25Height.Drawn(150f, 8f, 30f);
        Check(chimney <= Map25Height.Cap(10f, 10f) + 1e-4f && chimney <= 10f + 2f * 10f + 1e-3f && slab <= 6f + 3.4f * 8f + 1e-3f && slab / 30f < 1.5f && chimney / 10f <= 3.3f,
            "2.5D map: a real 150 m tower on a 10 by 10 m lot is drawn " + chimney.ToString("F0") + " m, no chimney, and on an 8 by 30 m sliver " + slab.ToString("F0") + " m, a slab no taller than it is long");
        bool steady = true;
        float last = 0f;
        for (float h = 1f; h <= 400f; h += 3f)
        {
            float d = Map25Height.Drawn(h, 14f, 30f);
            steady &= d >= last - 1e-4f && d >= Map25Height.Least - 1e-4f && d <= Map25Height.Cap(14f, 30f) + 1e-4f;
            last = d;
        }
        Check(steady, "2.5D map: a taller building is never drawn lower than a lower one, and none above the cap of its lot");
        // the city the previews and the budget draw: every building, whatever its real height (the tall model's 120 to 260 m towers included)
        foreach (bool tallModel in new[] { false, true })
        {
            Map25City city = Map25Fixture.City(7, tallModel);
            var sx = new float[32];
            var sz = new float[32];
            int highest = 0, needles = 0, over = 0, exact = 0;
            float tallest = 0f;
            for (int i = 0; i < city.Count; i++)
            {
                Map25Height.Measure(city, i, sx, sz, out float shorter, out float longer);
                float h = city.Height[i];
                needles += h / shorter > 4.7f || h > 2f * longer + 10.01f ? 1 : 0;
                over += h > Map25Height.Cap(shorter, longer) + 1e-3f ? 1 : 0;
                exact += city.Real[i] <= Map25Height.Knee(shorter) && Math.Abs(h - Math.Max(city.Real[i], Map25Height.Least)) < 1e-3f ? 1 : 0;
                tallest = Math.Max(tallest, h);
                highest += h > 50f ? 1 : 0;
            }
            Check(needles == 0 && over == 0 && exact > 100 && tallest < 100f,
                "2.5D map: in the " + (tallModel ? "deliberately tall" : "ordinary") + " model city no block is a needle (taller than 4.7 times its shorter side or than 10 m and twice its longer), none is above its cap, " + exact + " low ones keep their real height, the tallest is "
                + tallest.ToString("F0") + " m (" + highest + " over 50 m)");
        }
    }

    private static void Map25PaneChecks(Map25Map map)
    {
        // a 40 by 30 m building 30 m tall, a shop or not by its address: its windows are the panes on its walls, a pane of 1.75 by 1.3 m every 4.4 by 3.1 m
        int expectedPlain = (Map25Blocks.PaneColumns(40f) * 2 + Map25Blocks.PaneColumns(30f) * 2) * Map25Blocks.PaneFloors(30f, false);
        int expectedShop = (Map25Blocks.PaneColumns(40f) * 2 + Map25Blocks.PaneColumns(30f) * 2) * Map25Blocks.PaneFloors(30f, true);
        Map25Builders t = Map25One(40f, 30f, 30f, 0x1234, true);
        Map25Chunk panes = t.Panes.ToChunk();
        int quads = panes.Vertices / 4, lit = 0;
        bool alphas = true, onWalls = true, inHeight = true;
        for (int v = 0; v < panes.Vertices; v++)
        {
            float x = panes.Pos[v * 3], y = panes.Pos[v * 3 + 1], z = panes.Pos[v * 3 + 2], a = FromHalf(panes.Col[v * 4 + 3]);
            if (Math.Abs(a - 0.75f) < 0.01f)
            {
                lit++;
            }
            else
            {
                alphas &= Math.Abs(a - 0.24f) < 0.003f;
            }
            bool onSouth = Math.Abs(z + 0.08f) < 0.04f && x > 0.5f && x < 39.5f, onNorth = Math.Abs(z - 30.08f) < 0.04f && x > 0.5f && x < 39.5f;
            bool onWest = Math.Abs(x + 0.08f) < 0.04f && z > 0.5f && z < 29.5f, onEast = Math.Abs(x - 40.08f) < 0.04f && z > 0.5f && z < 29.5f;
            onWalls &= onSouth || onNorth || onWest || onEast;
            inHeight &= y >= 1.2f && y <= 29f;
        }
        int windows = quads - lit / 4;
        double perWindow = 4200.0 / Math.Max(1, windows);
        Check((windows == expectedPlain || windows == expectedShop) && lit <= 16 && perWindow > 11.0 && perWindow < 20.0,
            "2.5D map: a 40 by 30 m tower of 30 m has a pane for every window, " + windows + " of them, one to every " + perWindow.ToString("F1") + " square metres of wall (the mockup's dashes: 13.6), and at most four lit");
        Check(alphas && onWalls && inHeight, "2.5D map: every pane lies 8 to 11 cm out of one of the walls, between the first floor and the cornice, as translucent (0.24, lit ones 0.75) as the mockup's dashes");
        // a vast building has larger windows, not fewer: the same look, no more than the cap
        Map25Builders vast = Map25One(120f, 90f, 60f, 0x777, true);
        Map25Chunk vastPanes = vast.Panes.ToChunk();
        float firstWidth = (float)Math.Sqrt(Math.Pow(vastPanes.Pos[3] - vastPanes.Pos[0], 2) + Math.Pow(vastPanes.Pos[5] - vastPanes.Pos[2], 2));
        Check(vastPanes.Vertices / 4 <= Map25Blocks.PaneCap + 4 && firstWidth > 1.75f * 1.15f, "2.5D map: a building that would have more than " + Map25Blocks.PaneCap + " windows has them drawn larger and fewer, not left out (" + vastPanes.Vertices / 4 + " windows of " + firstWidth.ToString("F1") + " m)");
        int most = 0;
        for (int i = 0; i < map.City.Count; i++)
        {
            most = Math.Max(most, map.Full.Layers[(int)Map25Kind.Panes].Count[i]);
        }
        Check(most <= (Map25Blocks.PaneCap + 4) * 4 && map.Small.Layers[(int)Map25Kind.Panes].Vertices == 0 && map.Small.Layers[(int)Map25Kind.Fronts].Vertices == 0,
            "2.5D map: no building has more than " + (Map25Blocks.PaneCap + 4) + " panes (" + most / 4 + " at the most here), and the minimap's detail has no windows or awnings: its pixel is a metre");
        Check(!Map25Rules.Draws(Map25Kind.Panes, Map25View.LeastRise) && Map25Rules.Draws(Map25Kind.Panes, 1f), "2.5D map: windows are drawn when the blocks stand and not on the flat map");
    }

    private static void Map25ShapeChecks()
    {
        // the foot of a wall is shaded in metres from the ground: 0.55 there, 0.14 at 12 m, nothing from 28 m, so a tall wall's foot is no taller than a low one's
        bool foot = Math.Abs(Map25Palette.Foot(0f) - 0.55f) < 1e-4f && Math.Abs(Map25Palette.Foot(12f) - 0.14f) < 1e-4f && Map25Palette.Foot(28f) == 0f && Map25Palette.Foot(120f) == 0f
                    && Map25Palette.Foot(6f) > Map25Palette.Foot(12f) && Map25Palette.Foot(20f) < Map25Palette.Foot(12f) && Map25Palette.Foot(20f) > 0f;
        Map25Builders tower = Map25One(30f, 24f, 60f, 0x4321, true);
        Map25Chunk wall = tower.Blocks.ToChunk();
        bool knees = false, kneesEnd = false, podium = false, crown = false;
        for (int v = 0; v < wall.Vertices; v++)
        {
            float y = wall.Pos[v * 3 + 1];
            knees |= Math.Abs(y - 12f) < 1e-3f;
            kneesEnd |= Math.Abs(y - 28f) < 1e-3f;
            podium |= Math.Abs(y - 18f) < 1e-3f;
            crown |= Math.Abs(y - 44.4f) < 1e-3f;
        }
        Check(foot && knees && kneesEnd && podium && crown, "2.5D map: the foot's shade is in metres (0.55 at the ground, 0.14 at 12 m, none from 28 m) and a wall's pieces break at those metres and at the podium (30 per cent) and the crown (74 per cent), as hard steps");
        // the setback: a wide block gets a crown of its own kind, a thin one or a low one none
        Map25Builders wide = Map25One(40f, 40f, 40f, 0x55, true), thin = Map25One(12f, 40f, 40f, 0x55, true), lowWide = Map25One(40f, 40f, 15f, 0x55, true);
        Map25Chunk crownChunk = wide.Crowns.ToChunk();
        float top = 0f, inset = float.MaxValue, base_ = float.MaxValue;
        for (int v = 0; v < crownChunk.Vertices; v++)
        {
            top = Math.Max(top, crownChunk.Pos[v * 3 + 1]);
            base_ = Math.Min(base_, crownChunk.Pos[v * 3 + 1]);
            inset = Math.Min(inset, crownChunk.Pos[v * 3]);
        }
        Check(crownChunk.Vertices > 0 && Math.Abs(base_ - 40f) < 0.01f && Math.Abs(top - (40f + 16.8f)) < 0.01f && Math.Abs(inset - 8.8f) < 0.05f && thin.Crowns.Vertices == 0 && lowWide.Crowns.Vertices == 0,
            "2.5D map: a block 40 m wide and 40 m tall has a crown 22 per cent of its width in from the walls and 42 per cent of its height up (at most 45 per cent of its width), standing on its roof; a thin block or a low one has none");
        Check(!Map25Rules.Draws(Map25Kind.Crowns, Map25View.LeastRise) && Map25Rules.Draws(Map25Kind.Crowns, 1f), "2.5D map: the crown is its own kind, drawn only when the blocks stand: the flat map has no frame inside a frame");
        // the awnings: on the north and south walls of a shop only, standing out 1.25 m with a valance, in their own kind
        Map25Builders shop = null;
        for (long key = 1; key < 400 && (shop == null || shop.Fronts.Vertices == 0); key++)
        {
            shop = Map25One(40f, 30f, 20f, key, true);
        }
        Map25Chunk awnings = shop.Fronts.ToChunk();
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int v = 0; v < awnings.Vertices; v++)
        {
            minX = Math.Min(minX, awnings.Pos[v * 3]);
            maxX = Math.Max(maxX, awnings.Pos[v * 3]);
            minZ = Math.Min(minZ, awnings.Pos[v * 3 + 2]);
            maxZ = Math.Max(maxZ, awnings.Pos[v * 3 + 2]);
        }
        Check(awnings.Vertices == 48 && minX >= 0f && maxX <= 40f && Math.Abs(minZ + 1.25f) < 0.01f && Math.Abs(maxZ - 31.25f) < 0.01f,
            "2.5D map: a shop's awnings are on its two 40 m walls (three to a wall, a strip and a valance each) and none on the 30 m ones, each standing out 1.25 m (" + awnings.Vertices + " vertices)");
        Check(!Map25Rules.Draws(Map25Kind.Fronts, 0.4f) && Map25Rules.Draws(Map25Kind.Fronts, 0.6f), "2.5D map: awnings are drawn from a half height of the blocks, so a flat map has no bars beside the shop fronts");
        // the water tank: four thin legs, a drum on them with a gap under it, a lid and a cone
        MethodInfo tank = typeof(Map25Blocks).GetMethod("Tank", BindingFlags.NonPublic | BindingFlags.Static);
        var b = new Map25Builder(256);
        tank.Invoke(null, new object[] { b, 0f, 0f, 0f, 1.7f });
        Map25Chunk drum = b.ToChunk();
        float legs = 1.45f * 1.7f;
        bool thinLegs = true, gap = true, body = false;
        for (int v = 0; v < drum.Vertices; v++)
        {
            float x = drum.Pos[v * 3], y = drum.Pos[v * 3 + 1], z = drum.Pos[v * 3 + 2];
            if (y < legs - 0.01f)
            {
                thinLegs &= Math.Abs(Math.Abs(x) - 0.68f * 1.7f) <= 0.12f && Math.Abs(Math.Abs(z) - 0.68f * 1.7f) <= 0.12f;
                gap &= y < 0.3f || Math.Abs(x) > 0.6f;
            }
            body |= y > legs + 1f && Math.Sqrt(x * x + z * z) > 1.6f;
        }
        Check(drum.Vertices > 60 && drum.Vertices < 140 && thinLegs && gap && body, "2.5D map: a water tank is a drum on four thin legs with a gap under it, a lid and a cone, not a post under a cap (" + drum.Vertices + " vertices)");
    }

    private static void Map25UnderChecks()
    {
        // one tree is a six sided dome: 13 vertices, 18 triangles, in two greens, the same tree at the same place
        Map25Chunk[] one = Map25Ground.Trees(new[] { 100f }, new[] { 100f }, 1, 0f, 0f, 200f, 200f, 0f);
        Map25Chunk[] again = Map25Ground.Trees(new[] { 100f }, new[] { 100f }, 1, 0f, 0f, 200f, 200f, 0f);
        bool dome = one.Length == 1 && one[0].Vertices == 13 && one[0].Indices == 54 && Same(one[0].Pos, again[0].Pos, 39) && Same(one[0].Col, again[0].Col, 52);
        float highest = 0f, widest = 0f;
        for (int v = 0; dome && v < 13; v++)
        {
            highest = Math.Max(highest, one[0].Pos[v * 3 + 1]);
            widest = Math.Max(widest, (float)Math.Sqrt(Math.Pow(one[0].Pos[v * 3] - 100f, 2) + Math.Pow(one[0].Pos[v * 3 + 2] - 100f, 2)));
        }
        Check(dome && highest > 4f && highest < 9f && widest >= 2.5f && widest <= 4.5f, "2.5D map: a tree is 13 vertices and 18 triangles, 2.6 to 4.4 m across its base and up to 8 m high, and the same tree stands at the same place (" + highest.ToString("F1") + " m high)");
        // a row of street trees is no park; a thicket is
        var rowX = new float[100];
        var rowZ = new float[100];
        for (int i = 0; i < 100; i++)
        {
            rowX[i] = 500f;
            rowZ[i] = i * 14f;
        }
        Map25Chunk[] none = Map25Ground.Greens(rowX, rowZ, 100, 400f, -50f, 600f, 1450f, 0f);
        var avenueX = new float[200];
        var avenueZ = new float[200];
        var jitter = new Map25Rng(9);
        for (int i = 0; i < 100; i++)
        {
            avenueX[i * 2] = 492f;
            avenueX[i * 2 + 1] = 508f;
            avenueZ[i * 2] = i * 14f + jitter.Range(-1.5f, 1.5f);
            avenueZ[i * 2 + 1] = i * 14f + jitter.Range(-1.5f, 1.5f);
        }
        Map25Chunk[] verges = Map25Ground.Greens(avenueX, avenueZ, 200, 400f, -50f, 600f, 1450f, 0f);
        var rng = new Map25Rng(3);
        var thicketX = new float[130];
        var thicketZ = new float[130];
        for (int i = 0; i < 130; i++)
        {
            thicketX[i] = 100f + rng.Range(0f, 56f);
            thicketZ[i] = 300f + rng.Range(0f, 56f);
        }
        Map25Chunk[] green = Map25Ground.Greens(thicketX, thicketZ, 130, 0f, 200f, 300f, 500f, 0f);
        double area = 0;
        bool near = true;
        foreach (Map25Chunk c in green)
        {
            for (int i = 0; i + 2 < c.Indices; i += 3)
            {
                int a = c.Idx[i], bb = c.Idx[i + 1], d = c.Idx[i + 2];
                area += Math.Abs((c.Pos[bb * 3] - c.Pos[a * 3]) * (c.Pos[d * 3 + 2] - c.Pos[a * 3 + 2]) - (c.Pos[d * 3] - c.Pos[a * 3]) * (c.Pos[bb * 3 + 2] - c.Pos[a * 3 + 2])) * 0.5;
            }
            for (int v = 0; v < c.Vertices; v += 4)
            {
                float cx = c.Pos[v * 3] + 5f, cz = c.Pos[v * 3 + 2] + 5f;
                bool any = false;
                for (int k = 0; k < 130; k++)
                {
                    any |= Math.Abs(thicketX[k] - cx) < 45f && Math.Abs(thicketZ[k] - cz) < 45f;
                }
                near &= any;
            }
        }
        Check(none.Length == 0 && verges.Length == 0 && green.Length > 0 && area > 2800 && area < 7000 && near,
            "2.5D map: a row of a hundred street trees, or one on each side of an avenue, makes no green ground and 130 trees in 56 by 56 m make a park of " + area.ToString("F0") + " square metres, none far from a tree");
    }

    private static void Map25MarkChecks(Map25Map map)
    {
        // pixels, not metres: the route's line is 7 of a full map's 1080 pixels and 4.2 of a minimap's 300, the view cone 142 and 55 pixels long
        float m = 0.4f;
        float fullLine = Map25Marks.RouteHalf(false, 1080f, m) * 2f / m, miniLine = Map25Marks.RouteHalf(true, 300f, m) * 2f / m;
        float fullCone = Map25Marks.ConeMetres(false, 1080f, m) / m, miniCone = Map25Marks.ConeMetres(true, 300f, m) / m;
        float fullHalo = Map25Marks.HaloMetres(false, 1080f, m) / m, miniHalo = Map25Marks.HaloMetres(true, 300f, m) / m;
        Check(Math.Abs(fullLine - 7f) < 0.01f && Math.Abs(miniLine - 4.2f) < 0.01f && Math.Abs(fullCone - 142f) < 0.1f && Math.Abs(miniCone - 55f) < 0.1f && Math.Abs(fullHalo - 30f) < 0.1f && Math.Abs(miniHalo - 11.6f) < 0.1f,
            "2.5D map: the route's line is 7 pixels on a full map 1080 high and 4.2 on a minimap 300 wide, the view cone 142 and 55 pixels long, the halo 30 and 11.6 across, at any zoom");
        float onAvenue = fullLine * Map25Marks.CasingShare / 46f;
        Check(onAvenue < 0.28f && Math.Abs(Map25Marks.RouteHalf(false, 2160f, m) / Map25Marks.RouteHalf(false, 1080f, m) - 2f) < 1e-3f && Map25Marks.ConeAlpha == 0.17f && Map25Marks.HaloAlpha == 0.30f,
            "2.5D map: the route with its casing takes " + (100 * onAvenue).ToString("F0") + " per cent of a 46 pixel avenue (it took 68), it grows with the screen, the cone's fill is 0.17 and the halo's 0.30");
        bool steps = true;
        for (float h = 0.2f; h < 40f; h *= 1.07f)
        {
            float s = Map25Marks.Step(h);
            steps &= Math.Abs(s / h - 1f) <= 0.0626f && Math.Abs(Map25Marks.Step(s) / s - 1f) < 1e-3f;
        }
        Check(steps, "2.5D map: the route's width is kept to steps of an eighth, so a zoom that moves it by a hair writes nothing again");
        // a ring on the ground round a tinted building, and a line round its roof at its roof's height
        Map25City city = map.City;
        int hull = 7;
        var ring = new Map25Builder(256);
        city.Tint[hull] = Map25Colour.Hex(0x3FCF8E);
        Map25Ground.Ring(ring, city, hull, city.Tint[hull], 5f, 1.6f, 0.08f);
        Map25Chunk rc = ring.ToChunk();
        int s0 = city.HullStart[hull], n = city.HullStart[hull + 1] - s0;
        float minx = float.MaxValue, maxx = float.MinValue, fx0 = float.MaxValue, fx1 = float.MinValue;
        for (int k = 0; k < n; k++)
        {
            fx0 = Math.Min(fx0, city.HullX[s0 + k]);
            fx1 = Math.Max(fx1, city.HullX[s0 + k]);
        }
        int fills = 0, lines = 0;
        for (int v = 0; v < rc.Vertices; v++)
        {
            minx = Math.Min(minx, rc.Pos[v * 3]);
            maxx = Math.Max(maxx, rc.Pos[v * 3]);
            float a = FromHalf(rc.Col[v * 4 + 3]);
            fills += Math.Abs(a - 0.20f) < 0.002f ? 1 : 0;
            lines += Math.Abs(a - 0.62f) < 0.003f ? 1 : 0;
        }
        var roof = new Map25Builder(256);
        Map25Ground.RoofLine(roof, city, hull, city.Tint[hull], 0.9f, 0.62f);
        Map25Chunk lc = roof.ToChunk();
        bool atTop = lc.Vertices == n * 4;
        for (int v = 0; v < lc.Vertices; v++)
        {
            atTop &= Math.Abs(lc.Pos[v * 3 + 1] - (city.BaseY[hull] + city.Height[hull])) < 1e-3f;
        }
        city.Tint[hull] = default;
        Check(Math.Abs(minx - (fx0 - 5f)) < 0.2f && Math.Abs(maxx - (fx1 + 5f)) < 0.2f && fills == n && lines == n * 4 && atTop,
            "2.5D map: a tinted building has a ring on the ground 5 m round it (a faint fill, a stronger line) and a line round its roof at its roof's height, so a taller block in front cannot hide where it is");
        // the game's zones: the rectangle of a mesh's box through its matrix, and four corners going round
        float[] identity = { 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f };
        Map25Zones.Rectangle(identity, 10f, 0f, 20f, 5f, 0.1f, 8f, out float ax0, out float az0, out float ax1, out float az1);
        float[] quarter = { 0f, 0f, -1f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 0f, 100f, 0f, 50f, 1f };   // turned a quarter, moved
        Map25Zones.Rectangle(quarter, 10f, 0f, 20f, 5f, 0.1f, 8f, out float bx0, out float bz0, out float bx1, out float bz1);
        var corners = new float[16];
        Map25Zones.Corners(ax0, az0, ax1, az1, corners, 1);
        double turn = (corners[10] - corners[8]) * (corners[13] - corners[9]) - (corners[11] - corners[9]) * (corners[12] - corners[8]);
        Check(Math.Abs(ax0 - 5f) < 1e-4f && Math.Abs(ax1 - 15f) < 1e-4f && Math.Abs(az0 - 12f) < 1e-4f && Math.Abs(az1 - 28f) < 1e-4f
              && Math.Abs((bx1 - bx0) - 16f) < 1e-3f && Math.Abs((bz1 - bz0) - 10f) < 1e-3f && Math.Abs(bx0 - (100f + 12f)) < 1e-3f && turn > 0,
            "2.5D map: a zone's ground rectangle is its mesh's box through its matrix (turned and moved, as the district view reads it), and its four corners go round");
    }

    private static void Map25RuleChecks()
    {
        bool flat = Map25Rules.Draws(Map25Kind.Blocks, Map25View.LeastRise) && !Map25Rules.Draws(Map25Kind.Crowns, Map25View.LeastRise) && !Map25Rules.Draws(Map25Kind.Fronts, Map25View.LeastRise)
                    && !Map25Rules.Draws(Map25Kind.Panes, Map25View.LeastRise) && !Map25Rules.Draws(Map25Kind.Contacts, Map25View.LeastRise);
        bool full = Map25Rules.Draws(Map25Kind.Crowns, 1f) && Map25Rules.Draws(Map25Kind.Fronts, 1f) && Map25Rules.Draws(Map25Kind.Panes, 1f) && Map25Rules.Draws(Map25Kind.Contacts, 1f);
        bool between = Map25Rules.Draws(Map25Kind.Crowns, 0.2f) && Map25Rules.Draws(Map25Kind.Panes, 0.3f) && !Map25Rules.Draws(Map25Kind.Fronts, 0.3f) && Map25Rules.Draws(Map25Kind.Fronts, 0.6f);
        Check(flat && full && between, "2.5D map: seen from above only the blocks are drawn; every kind from half height up; crowns from a tenth, windows from a quarter, awnings from a half");
        string scene = ModSource(@"Ui\Map\Map25Scene.cs");
        int from = scene.IndexOf("public void Draw(", StringComparison.Ordinal);
        string[] order =
        {
            "DrawGroup(cmd, _under[0]", "DrawGroup(cmd, _under[1]", "DrawGroup(cmd, _under[2]", "DrawGroup(cmd, _under[3]", "cmd.DrawMesh(_zones", "cmd.DrawMesh(_rings", "cmd.DrawMesh(_cone",
            "Map25Kind.Contacts]", "DrawGroup(cmd, _under[4]", "Map25Kind.Blocks]", "Map25Kind.Crowns]", "Map25Kind.Fronts]", "Map25Kind.Panes]", "cmd.DrawMesh(_route", "cmd.DrawMesh(_marks", "cmd.DrawMesh(_fog"
        };
        int last = -1;
        string wrong = "none";
        for (int i = 0; i < order.Length; i++)
        {
            int at = from < 0 ? -1 : scene.IndexOf(order[i], from, StringComparison.Ordinal);
            if (at <= last)
            {
                wrong = order[i];
            }
            last = Math.Max(last, at);
        }
        Check(wrong == "none", "2.5D map: a picture is drawn water, land, green ground, roads, lane marking, the game's zones, rings, the cone and halo, contact patches, trees, blocks, crowns, awnings, windows, the route, the roof lines, the fade (" + wrong + ")");
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\Map"));
        Check(!System.IO.File.Exists(System.IO.Path.Combine(folder, "Map25Fixture.cs")) && !System.IO.File.Exists(System.IO.Path.Combine(folder, "Map25Real.cs"))
              && System.IO.File.Exists(System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, @"..\..\..\..\..\..\RealLifeOverhaul.Build\Shared\Map25Fixture.cs"))),
            "2.5D map: the made-up city and the owner's real footprints are preview and check code in the build's shared folder, never in the mod's own sources");
    }
}
