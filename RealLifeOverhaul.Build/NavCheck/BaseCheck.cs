using System;
using System.Diagnostics;
using RealLifeOverhaul.Nav;

// Checks for the game-free part of the minimap's base picture (Navigation/NavBaseRules.cs): where a place is on the
// picture and back, the window on it at the city's edges, the turned view, outlines, the water grid, the drawing, and
// the file the measured city is kept in.
internal static partial class Program
{
    // A made-up island: 400 m by 240 m of picture, land in the middle with water all round, a courtyard nobody walks
    // in, a pond the game names, and three buildings.
    private static NavBaseData Island()
    {
        var data = new NavBaseData
        {
            Build = 4321,
            OriginX = -100f,
            OriginZ = 50f,
            WorldWidth = 400f,
            WorldHeight = 240f,
            Cell = NavBaseWater.CellMetres,
            Columns = 50,
            Rows = 30
        };
        // an upright box, a box turned by 30 degrees, and a small one at the picture's very corner
        var x = new float[32];
        var z = new float[32];
        var sx = new float[8];
        var sz = new float[8];
        int corners = 0;
        var start = new int[4];
        float[][] boxes =
        {
            new[] { 0f, 150f, 40f, 150f, 40f, 180f, 0f, 180f },
            new[] { 150f, 150f, 184.64f, 170f, 169.64f, 195.98f, 135f, 175.98f },
            new[] { -100f, 50f, -94f, 50f, -94f, 56f, -100f, 56f }
        };
        for (int b = 0; b < boxes.Length; b++)
        {
            for (int i = 0; i < 4; i++)
            {
                sx[i] = boxes[b][i * 2];
                sz[i] = boxes[b][i * 2 + 1];
            }
            start[b] = corners;
            corners += NavBaseMath.Hull(sx, sz, 4, x, z, corners);
        }
        start[3] = corners;
        data.Hulls = 3;
        data.HullStart = start;
        data.HullKey = new[] { NavBaseMath.BuildingKey("ba:street_a", 1), NavBaseMath.BuildingKey("ba:street_a", 2), NavBaseMath.BuildingKey("ba:street_b", 1) };
        data.HullX = x;
        data.HullZ = z;
        data.Cells = new byte[data.Columns * data.Rows];
        return data;
    }

    // The island's squares as the city would answer: open sea for five squares round the edge, ground inside.
    private static void Ask(NavBaseData data)
    {
        for (int row = 0; row < data.Rows; row++)
        {
            for (int column = 0; column < data.Columns; column++)
            {
                int cell = row * data.Columns + column;
                if (data.Cells[cell] != NavBaseWater.Unasked)
                {
                    continue;
                }
                bool sea = row < 5 || column < 5 || row >= data.Rows - 5 || column >= data.Columns - 5;
                data.Cells[cell] = sea ? NavBaseWater.Open : NavBaseWater.Land;
            }
        }
        // a courtyard of open squares shut in by ground, and a pond the game names
        for (int row = 12; row < 15; row++)
        {
            for (int column = 30; column < 34; column++)
            {
                data.Cells[row * data.Columns + column] = NavBaseWater.Open;
            }
        }
        data.Cells[20 * data.Columns + 40] = NavBaseWater.Named;
    }

    private static void Base()
    {
        // ---- a place on the picture, and back
        const float origin = -1820.5f, perMetre = 0.565f;
        bool round = true;
        for (float world = origin; world < origin + 3625f; world += 137.3f)
        {
            round &= Near(NavBaseMath.ToWorld(NavBaseMath.ToPixel(world, origin, perMetre), origin, perMetre), world, 0.002f);
        }
        Check(round, "a place to its pixel and back is the same place, across the whole city, to 2 mm");
        Check(Near(NavBaseMath.ToPixel(origin, origin, perMetre), 0f) && Near(NavBaseMath.ToPixel(origin + 100f, origin, perMetre), 56.5f, 0.001f), "the picture's corner is pixel 0, and 100 m on is 100 x pixels a metre");
        bool back = true;
        for (int pixel = 0; pixel < 2048; pixel += 97)
        {
            float middle = NavBaseMath.ToWorld(pixel + 0.5f, origin, perMetre);
            back &= NavBaseMath.Texel(NavBaseMath.ToPixel(middle, origin, perMetre), 2048) == pixel;
        }
        Check(back, "the middle of a pixel, taken to the world and back, is that pixel");
        Check(NavBaseMath.Texel(-3.2f, 2048) == 0 && NavBaseMath.Texel(2047.9f, 2048) == 2047 && NavBaseMath.Texel(9000f, 2048) == 2047 && NavBaseMath.Texel(0f, 2048) == 0,
              "a place outside the picture reads the pixel at its edge, never one from elsewhere");
        float halfPixel = 0.5f / perMetre;
        float off = Math.Abs(NavBaseMath.ToWorld(NavBaseMath.Texel(NavBaseMath.ToPixel(123.4f, origin, perMetre), 2048) + 0.5f, origin, perMetre) - 123.4f);
        Check(off <= halfPixel + 0.001f, "a place is never more than half a pixel from the middle of the pixel it is drawn in");

        // ---- the size
        NavRaster.Size(3625f, 2042f, NavBaseMath.LongestSide, NavBaseMath.MostPerMetre, out int width, out int height, out float pm);
        Check(width == 2048 && height <= 2048 && height >= 1150 && height <= 1156 && width * height * 4 <= 10 * 1048576, "the game's city is 2048 pixels long and under 10 MB");
        NavRaster.Size(600f, 400f, NavBaseMath.LongestSide, NavBaseMath.MostPerMetre, out width, out height, out pm);
        Check(Near(pm, 1.25f) && width == 750 && height == 500, "a small city gets the road picture's pixels a metre and no more");

        // ---- the window, in the middle and at the city's edges
        NavView.Window(0f, 170f, -100f, 50f, 400f, 240f, 80f, out float u, out float v, out float w, out float h);
        Check(Near(NavBaseMath.Inside(u, v, w, h), 1f, 0.0001f), "in the middle of the city the whole window is on the picture");
        NavView.Window(-100f, 50f, -100f, 50f, 400f, 240f, 80f, out u, out v, out w, out h);
        Check(Near(NavBaseMath.Inside(u, v, w, h), 0.25f, 0.0001f) && u < 0f && v < 0f, "at the picture's corner a quarter of the window is on it");
        NavView.Window(300f, 170f, -100f, 50f, 400f, 240f, 80f, out u, out v, out w, out h);
        Check(Near(NavBaseMath.Inside(u, v, w, h), 0.5f, 0.0001f) && u + w > 1f, "at its east edge, half");
        NavView.Window(5000f, 5000f, -100f, 50f, 400f, 240f, 80f, out u, out v, out w, out h);
        Check(NavBaseMath.Inside(u, v, w, h) == 0f && NavBaseMath.Inside(0f, 0f, 0f, 1f) == 0f, "far outside the city none of it is, and a window of no size divides nothing");
        // what the window's far corner reads at the edge: the picture's last pixel, drawn out
        Check(NavBaseMath.Texel((u + w) * 2048f, 2048) == 2047 && NavBaseMath.Texel(-0.2f * 2048f, 2048) == 0, "past the edge the window reads the edge's own pixels");

        // ---- the picture and the markers agree
        float worst = 0f;
        for (int i = 0; i < 200; i++)
        {
            float px = origin + 18f * i, pz = 400f - 7f * i;
            worst = Math.Max(worst, NavBaseMath.Misfit(px + 90f, pz - 60f, px, pz, origin, -900f, 3625f, 2042f, 390f));
        }
        Check(worst < 0.01f, "a place is drawn by the picture where the markers' layer puts it, across the city, to a centimetre");
        Check(NavBaseMath.Misfit(-2500f, 4000f, -2400f, 3900f, origin, -900f, 3625f, 2042f, 390f) < 0.01f, "outside the picture too");
        Check(NavBaseMath.Misfit(10f, 10f, 10f, 10f, origin, -900f, 3625f, 2042f, 65f) < 0.001f, "the player is the middle of the window");

        // ---- the turned view
        bool turned = true;
        for (float turn = -360f; turn <= 720f; turn += 37f)
        {
            NavView.ToView(140f, -25f, 100f, 30f, turn, 2.6f, out float vx, out float vy);
            NavBaseMath.FromView(vx, vy, 100f, 30f, turn, 2.6f, out float wx, out float wz);
            turned &= Near(wx, 140f, 0.005f) && Near(wz, -25f, 0.005f);
        }
        Check(turned, "a place taken to the turned view and back is the same place, at every heading");
        NavBaseMath.FromView(0f, 100f, 0f, 0f, 90f, 1f, out float ex, out float ez);
        Check(Near(ex, 100f, 0.01f) && Near(ez, 0f, 0.01f), "facing east, the top of the view is to the east");
        NavBaseMath.FromView(50f, 0f, 0f, 0f, 0f, 2f, out ex, out ez);
        Check(Near(ex, 25f) && Near(ez, 0f), "north up, the right of the view is to the east, at the view's scale");
        NavView.ToView(140f, -25f, 100f, 30f, 0f, 2.6f, out float nx, out float ny);
        NavView.ToView(140f, -25f, 100f, 30f, 73f, 2.6f, out float tx, out float ty);
        Check(Near(nx * nx + ny * ny, tx * tx + ty * ty, 0.05f), "turning the view moves nothing nearer or further");

        // ---- outlines
        var hx = new float[20];
        var hz = new float[20];
        var cx = new[] { 0f, 10f, 10f, 0f, 5f, 5f, 2f, 8f };
        var cz = new[] { 0f, 0f, 6f, 6f, 3f, 1f, 5f, 2f };
        int n = NavBaseMath.Hull(cx, cz, 8, hx, hz, 0);
        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += hx[i] * hz[j] - hx[j] * hz[i];
        }
        Check(n == 4 && Near(area / 2f, 60f, 0.001f), "the outline round a box's corners and what lies inside is the box, anticlockwise");
        var one = new[] { 3f, 3f, 3f };
        var oneZ = new[] { 7f, 7f, 7f };
        Check(NavBaseMath.Hull(one, oneZ, 3, hx, hz, 0) < 3 && NavBaseMath.Hull(new[] { 0f, 1f, 2f }, new[] { 0f, 1f, 2f }, 3, hx, hz, 0) < 3 && NavBaseMath.Hull(one, oneZ, 0, hx, hz, 0) < 3,
              "one place, one line and nothing have no outline");
        n = NavBaseMath.Hull(new[] { 0f, 10f, 10f, 0f }, new[] { 0f, 0f, 6f, 6f }, 4, hx, hz, 0);
        Check(NavBaseMath.Outside(5f, 3f, hx, hz, 0, n) == 0f && NavBaseMath.Outside(10f, 6f, hx, hz, 0, n) == 0f && Near(NavBaseMath.Outside(13f, 3f, hx, hz, 0, n), 3f)
              && Near(NavBaseMath.Outside(13f, 10f, hx, hz, 0, n), 5f), "a place inside an outline or on it is 0 m outside; beside it, the distance to its edge or corner");

        // ---- three far apart
        var fx = new float[40];
        var fz = new float[40];
        for (int i = 0; i < 40; i++)
        {
            fx[i] = 500f + (i % 8) * 10f;
            fz[i] = 500f + (i / 8) * 10f;
        }
        fx[7] = 3000f;
        fz[7] = 1900f;
        fx[20] = -400f;
        fz[20] = -100f;
        fx[33] = 2900f;
        fz[33] = -150f;
        var three = new int[3];
        int got = NavBaseMath.FarApart(fx, fz, 40, three);
        Array.Sort(three);
        Check(got == 3 && three[0] == 7 && three[1] == 20 && three[2] == 33, "the three far apart are the three corners of the town, not three neighbours");
        var again = new int[3];
        NavBaseMath.FarApart(fx, fz, 40, again);
        Array.Sort(again);
        Check(again[0] == three[0] && again[1] == three[1] && again[2] == three[2] && NavBaseMath.FarApart(fx, fz, 0, again) == 0 && NavBaseMath.FarApart(fx, fz, 2, again) == 2,
              "the same three every time; none of none, both of two");

        // ---- one look down
        Check(NavBaseWater.Classify(false, false, false, false, 0f, 10f) == NavBaseWater.Open, "nothing under the sky is open");
        Check(NavBaseWater.Classify(true, true, false, false, 9f, 10f) == NavBaseWater.Named, "what the game names water is water");
        Check(NavBaseWater.Classify(true, false, true, false, -30f, 10f) == NavBaseWater.Land && NavBaseWater.Classify(true, false, false, true, -30f, 10f) == NavBaseWater.Land,
              "a road, a building and where people walk are ground, however low");
        Check(NavBaseWater.Classify(true, false, false, false, 9.5f, 10f) == NavBaseWater.Land && NavBaseWater.Classify(true, false, false, false, 8.9f, 10f) == NavBaseWater.Open,
              "a lawn at street level is ground; a bed more than a step below the street is open");
        Check(NavBaseWater.Classify(true, false, false, false, 500f, float.NaN) == NavBaseWater.Open && !NavBaseWater.AtStreetLevel(0f, float.NaN), "with no road near, height decides nothing");

        // ---- the grid
        NavBaseData island = Island();
        int settledByBuildings = NavBaseWater.Prepare(island);
        Check(settledByBuildings > 0 && island.Cells[(int)((165f - 50f) / 8f) * 50 + (int)((20f + 100f) / 8f)] == NavBaseWater.Land && island.Cells[0] == NavBaseWater.Land
              && island.Cells[29 * 50 + 49] == NavBaseWater.Unasked, "squares inside a building are ground without asking; the rest are still to be asked");
        Ask(island);
        island.Cells[0] = NavBaseWater.Land;
        int water = NavBaseWater.Settle(island.Cells, island.Columns, island.Rows);
        bool two = true;
        for (int i = 0; i < island.Cells.Length; i++)
        {
            two &= island.Cells[i] == NavBaseWater.Land || island.Cells[i] == NavBaseWater.Water;
        }
        Check(two && island.Cells[2 * 50 + 25] == NavBaseWater.Water && island.Cells[15 * 50 + 20] == NavBaseWater.Land, "after settling every square is ground or water: the sea round the island is water, the island is not");
        Check(island.Cells[13 * 50 + 31] == NavBaseWater.Land, "a courtyard shut in by ground is not water");
        Check(island.Cells[20 * 50 + 40] == NavBaseWater.Water && island.Cells[20 * 50 + 41] == NavBaseWater.Land, "a pond the game names is water where it lies and floods nothing");
        Check(water == 50 * 30 - 40 * 20 + 1 - 1, "and the count is the sea's squares and the pond, less the corner building's");
        var shut = new byte[9] { 0, 0, 0, 0, NavBaseWater.Open, 0, 0, 0, 0 };
        var leak = new byte[9] { 0, NavBaseWater.Open, 0, 0, NavBaseWater.Open, 0, 0, 0, 0 };
        var corner = new byte[16] { NavBaseWater.Open, 0, 0, 0, 0, NavBaseWater.Open, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Check(NavBaseWater.Settle(shut, 3, 3) == 0 && NavBaseWater.Settle(leak, 3, 3) == 2 && NavBaseWater.Settle(corner, 4, 4) == 1,
              "open ground is water only if the edge is reached step by step: not when shut in, and not across a corner");
        Check(NavBaseWater.Settle(new byte[0], 0, 0) == 0 && NavBaseWater.Settle(new byte[1] { NavBaseWater.Open }, 1, 1) == 1, "no grid and a grid of one square settle too");
        Check(NavBaseWater.At(island, -100f + 4f, 50f + 4f) == 0f && Near(NavBaseWater.At(island, 100f, 50f + 12f), 1f) && Near(NavBaseWater.At(island, 100f, 50f + 40f), 0.5f)
              && Near(NavBaseWater.At(island, -5000f, 5000f), 1f), "the water is read smoothly: all at sea, none on land, half on the shore line, and the edge's answer beyond the edge");

        // ---- the drawing
        const int pw = 400, ph = 240;
        var cover = new byte[pw * ph];
        int drawn = NavBaseRaster.Buildings(cover, pw, ph, -100f, 50f, 1f, island);
        Check(drawn == 3 && cover[115 * pw + 120] == 255 && cover[115 * pw + 160] == 0, "a building's inside is covered and the street beside it is not");
        Check(cover[123 * pw + 262] == 255 && cover[105 * pw + 240] == 0 && cover[140 * pw + 280] == 0, "a turned building is drawn turned: its middle is covered, the corners of the box round it are not");
        int sum = 0;
        for (int py = 95; py < 135; py++)
        {
            for (int px = 95; px < 145; px++)
            {
                sum += cover[py * pw + px];
            }
        }
        Check(Math.Abs(sum / 255f - 40f * 30f) < 6f, "the upright building covers its 1200 square metres, to half a per cent");
        bool rim = true;
        for (int i = 0; i < pw; i++)
        {
            rim &= cover[i] == 0 && cover[(ph - 1) * pw + i] == 0;
        }
        for (int i = 0; i < ph; i++)
        {
            rim &= cover[i * pw] == 0 && cover[i * pw + pw - 1] == 0;
        }
        Check(rim && cover[2 * pw + 2] == 255, "no building reaches the picture's outermost pixels (the one at the very corner is drawn from the second pixel in)");
        var half = new byte[16 * 16];
        NavBaseRaster.Polygon(half, 16, 16, 0f, 0f, 1f, new[] { 4f, 8.5f, 8.5f, 4f }, new[] { 4f, 4f, 9f, 9f }, 0, 4);
        Check(half[6 * 16 + 8] > 100 && half[6 * 16 + 8] < 155 && half[6 * 16 + 7] == 255 && half[6 * 16 + 9] == 0, "an edge through the middle of a pixel covers half of it");
        var far = new byte[16 * 16];
        NavBaseRaster.Polygon(far, 16, 16, 9000f, 9000f, 1f, new[] { 4f, 8f, 8f, 4f }, new[] { 4f, 4f, 9f, 9f }, 0, 4);
        bool empty = true;
        for (int i = 0; i < far.Length; i++)
        {
            empty &= far[i] == 0;
        }
        Check(empty, "a building outside the picture writes nothing and throws nothing");

        var rgba = new byte[pw * ph * 4];
        NavBaseRaster.Compose(rgba, cover, pw, ph, -100f, 50f, 1f, island, 200, 201, 202, 84, 10, 40, 90, 215);
        int inBuilding = (115 * pw + 120) * 4, atSea = (10 * pw + 200) * 4, onLand = (100 * pw + 200) * 4, pond = (164 * pw + 324) * 4;
        Check(rgba[inBuilding] == 200 && rgba[inBuilding + 1] == 201 && rgba[inBuilding + 2] == 202 && rgba[inBuilding + 3] == 84, "a building is its colour, as solid as asked");
        Check(rgba[atSea] == 10 && rgba[atSea + 1] == 40 && rgba[atSea + 2] == 90 && rgba[atSea + 3] == 215, "the sea is the water's colour");
        Check(rgba[onLand + 3] == 0, "open ground is clear: the minimap's own ground shows through");
        Check(rgba[pond + 3] > 100 && rgba[pond + 2] == 90, "the pond is water");
        int shore = -1;
        for (int py = 30; py < 50 && shore < 0; py++)
        {
            int o = (py * pw + 200) * 4;
            if (rgba[o + 3] < 215)
            {
                shore = py;
            }
        }
        Check(shore == 40, "the shore lies where the grid says half water: 40 m in, half way between the middle of the last square of sea and the first of ground");
        bool fringe = true;
        for (int py = 1; py < ph - 1; py++)
        {
            for (int px = 1; px < pw - 1; px++)
            {
                int o = (py * pw + px) * 4;
                if (rgba[o + 3] == 0 && rgba[o + 4 + 3] > 0 && rgba[o - 4 + 3] == 0 && rgba[o + pw * 4 + 3] == 0 && rgba[o - pw * 4 + 3] == 0)
                {
                    fringe &= rgba[o] == rgba[o + 4] && rgba[o + 1] == rgba[o + 5] && rgba[o + 2] == rgba[o + 6];
                }
            }
        }
        Check(fringe, "a clear pixel beside a coloured one has that one's colour, so smoothing does not darken the edge");
        Check(rgba[(pw - 1) * 4 + 3] == 215 && rgba[(ph - 1) * pw * 4 + 3] == 215 && rgba[(ph * pw - 1) * 4 + 3] == 215, "the picture's outermost pixels at sea are water: past the edge the sea goes on");
        var dry = Island();
        NavBaseWater.Prepare(dry);
        for (int i = 0; i < dry.Cells.Length; i++)
        {
            dry.Cells[i] = NavBaseWater.Land;
        }
        var dryPicture = new byte[pw * ph * 4];
        NavBaseRaster.Compose(dryPicture, cover, pw, ph, -100f, 50f, 1f, dry, 200, 201, 202, 84, 10, 40, 90, 215);
        Check(dryPicture[atSea + 3] == 0 && dryPicture[inBuilding + 3] == 84, "a city with no water found shows its buildings and no blue");
        var twice = new byte[pw * ph * 4];
        NavBaseRaster.Compose(twice, cover, pw, ph, -100f, 50f, 1f, island, 200, 201, 202, 84, 10, 40, 90, 215);
        bool same = true;
        for (int i = 0; i < twice.Length; i++)
        {
            same &= twice[i] == rgba[i];
        }
        Check(same, "the same city draws the same picture");

        // ---- the file
        byte[] bytes = island.Encode();
        NavBaseData read = NavBaseData.Decode(bytes, 4321, -100f, 50f, 400f, 240f);
        bool whole = read != null && read.Hulls == 3 && read.HullKey[1] == island.HullKey[1] && read.HullKey[1] != 0L && read.Columns == 50 && read.Rows == 30 && read.Cell == island.Cell && read.HullStart[3] == island.HullStart[3];
        for (int i = 0; whole && i < island.HullStart[3]; i++)
        {
            whole &= read.HullX[i] == island.HullX[i] && read.HullZ[i] == island.HullZ[i];
        }
        for (int i = 0; whole && i < island.Cells.Length; i++)
        {
            whole &= read.Cells[i] == island.Cells[i];
        }
        Check(whole, "the measured city written and read back is the same city");
        Check(NavBaseData.Decode(bytes, 4322, -100f, 50f, 400f, 240f) == null, "a file from another build of the game is not used");
        Check(NavBaseData.Decode(bytes, 4321, -98f, 50f, 400f, 240f) == null && NavBaseData.Decode(bytes, 4321, -100f, 50f, 410f, 240f) == null
              && NavBaseData.Decode(bytes, 4321, -100.3f, 50.2f, 400.4f, 240f) != null, "nor one for a city whose roads lie elsewhere; a hand's width does not matter");
        var cut = new byte[bytes.Length - 100];
        Array.Copy(bytes, cut, cut.Length);
        var flipped = (byte[])bytes.Clone();
        flipped[bytes.Length / 2] ^= 0x40;
        Check(NavBaseData.Decode(cut, 4321, -100f, 50f, 400f, 240f) == null && NavBaseData.Decode(flipped, 4321, -100f, 50f, 400f, 240f) == null
              && NavBaseData.Decode(null, 4321, -100f, 50f, 400f, 240f) == null && NavBaseData.Decode(new byte[10], 4321, -100f, 50f, 400f, 240f) == null
              && NavBaseData.Decode(new byte[5000], 4321, -100f, 50f, 400f, 240f) == null, "a file cut short, damaged, empty or not ours is not used, and nothing is thrown");
        var none = new NavBaseData { Build = 1, Cell = 8f, Columns = 1, Rows = 1, Cells = new byte[1] };
        Check(NavBaseData.Decode(none.Encode(), 1, 0f, 0f, 0f, 0f) != null, "a city with no building is written and read too");

        // ---- how long the game's city takes (2048 x 1154, 1,500 buildings, 116,000 squares): printed if slow
        var city = new NavBaseData { Build = 1, OriginX = 0f, OriginZ = 0f, WorldWidth = 3625f, WorldHeight = 2042f, Cell = 8f, Columns = 454, Rows = 256 };
        city.Cells = new byte[city.Columns * city.Rows];
        city.Hulls = 1500;
        city.HullStart = new int[1501];
        city.HullKey = new long[1500];
        city.HullX = new float[6000];
        city.HullZ = new float[6000];
        for (int i = 0; i < 1500; i++)
        {
            float bx = 200f + (i % 50) * 64f, bz = 150f + (i / 50) * 58f;
            city.HullStart[i] = i * 4;
            city.HullX[i * 4] = bx;
            city.HullZ[i * 4] = bz;
            city.HullX[i * 4 + 1] = bx + 40f;
            city.HullZ[i * 4 + 1] = bz;
            city.HullX[i * 4 + 2] = bx + 40f;
            city.HullZ[i * 4 + 2] = bz + 30f;
            city.HullX[i * 4 + 3] = bx;
            city.HullZ[i * 4 + 3] = bz + 30f;
        }
        city.HullStart[1500] = 6000;
        NavBaseWater.Prepare(city);
        for (int i = 0; i < city.Cells.Length; i++)
        {
            int column = i % city.Columns, row = i / city.Columns;
            city.Cells[i] = column < 20 || row < 15 || column > 430 || row > 240 ? NavBaseWater.Open : NavBaseWater.Land;
        }
        Stopwatch watch = Stopwatch.StartNew();
        NavBaseWater.Settle(city.Cells, city.Columns, city.Rows);
        NavRaster.Size(city.WorldWidth, city.WorldHeight, NavBaseMath.LongestSide, NavBaseMath.MostPerMetre, out int cw, out int ch, out float cpm);
        var cityCover = new byte[cw * ch];
        int cityDrawn = NavBaseRaster.Buildings(cityCover, cw, ch, 0f, 0f, cpm, city);
        var cityPicture = new byte[cw * ch * 4];
        NavBaseRaster.Compose(cityPicture, cityCover, cw, ch, 0f, 0f, cpm, city, 200, 201, 202, 84, 10, 40, 90, 215);
        byte[] cityFile = city.Encode();
        double took = watch.Elapsed.TotalMilliseconds;
        Check(cityDrawn == 1500 && took < 3000.0, "a city of the game's size is settled, drawn and written in under 3 s off the main thread (took " + took.ToString("F0") + " ms)");
        Check(cityFile.Length < 200 * 1024 && NavBaseData.Decode(cityFile, 1, 0f, 0f, 3625f, 2042f) != null, "and its file is under 200 KB");

        BaseDoors();   // each outline's building found by its address, and the alignment check naming the door it blames (BaseDoorCheck.cs)
    }
}
