using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Map25;
using RealLifeOverhaul.Ui;
using UnityEditor;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The 2.5D map is drawn by Map25Preview below, by the mod's own renderer (Scripts/Ui/Map) into pictures: the picture of the game's own map is a
    // 3D scene the preview cannot draw. What is real: the renderer, the buildings' outlines and the water of the owner's own city (their
    // minimap_base file), the camera maths. What is made up is said in Map25Real: the heights, the roads (the gaps through the buildings), the trees.
    static partial void AddMap(List<Screen> screens)
    {
        Func<string, Sprite> before = UiPictures.Find;
        UiPictures.Find = key => key != null && key.StartsWith("map5:", StringComparison.Ordinal) ? MapPicture(key) : before?.Invoke(key);
        foreach (string state in UiSamples.MapFilterStates)
        {
            screens.Add(MapFiltersScreenOf(state, "en"));
        }
        foreach (string state in UiSamples.MapPlaceStates)
        {
            screens.Add(MapPlaceScreenOf(state, "en"));
        }
        foreach (string state in UiSamples.MapDistrictStates)
        {
            screens.Add(MapDistrictScreenOf(state, "en"));
        }
        foreach (string state in UiSamples.MapMinimapStates)
        {
            screens.Add(MapMinimapScreenOf(state, "en", false));
        }
        screens.Add(MapMinimapScreenOf("route_tilt", "en", true));
        foreach (string lang in new[] { "pt", "zh-cn" })
        {
            screens.Add(MapFiltersScreenOf("filters", lang));
            screens.Add(MapFiltersScreenOf("picker_long", lang));
            screens.Add(MapFiltersScreenOf("subway", lang));
            screens.Add(MapPlaceScreenOf("overview", lang));
            screens.Add(MapPlaceScreenOf("reviews", lang));
            screens.Add(MapDistrictScreenOf("rent", lang));
            screens.Add(MapDistrictScreenOf("wealth", lang));
            screens.Add(MapMinimapScreenOf("route_long", lang, false));
        }
    }

    // The real 2.5D shots the minimap card shows as its view: a picture of the owner's own footprints, from the backdrops folder.
    private static readonly Dictionary<string, Sprite> MapPictures = new Dictionary<string, Sprite>();

    private static Sprite MapPicture(string key)
    {
        if (string.IsNullOrEmpty(_backdrops))
        {
            return null;
        }
        if (MapPictures.TryGetValue(key, out Sprite known))
        {
            return known;
        }
        string path = Path.Combine(_backdrops, key.Substring("map5:".Length) + ".png");
        Sprite sprite = null;
        if (File.Exists(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(File.ReadAllBytes(path));
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        MapPictures[key] = sprite;
        return sprite;
    }

    // The place card over the real tilted map with the route strip, the layers and the buttons.
    private static Screen MapPlaceScreenOf(string state, string lang)
    {
        UiWords t = DialogWords(lang);
        bool reviews = state == "reviews", business = state == "business", hover = state == "hover";
        return new Screen
        {
            Name = "map_place_" + state + Suffix(lang), AppName = "City map", Icon = UiIcon.MapTrifold, Backdrop = state == "unrated" ? "real_full_junction.png" : "real_full_tilt.png",
            Lay = (s, f, d, k) => UiSamples.MapPlaceScene(s, f, d, k, t, state, true),
            HoverCommand = hover ? UiMapPlace.RentCommand : reviews ? UiMapPlace.ReviewCommand : business ? UiMapPlace.TabCommand : UiMapPlace.SaveCommand, HoverArg = reviews ? 1 : business ? 1 : 0,
            Walk = Moves("DDDRRDDDD")
        };
    }

    // The district view over the tinted map (the game's rent shot) or the real picture.
    private static Screen MapDistrictScreenOf(string state, string lang)
    {
        UiWords t = DialogWords(lang);
        return new Screen
        {
            Name = "map_district_" + state + Suffix(lang), AppName = "City map", Icon = UiIcon.MapTrifold, Backdrop = state == "crime" ? "real_full_flat.png" : "map_rent.png",
            Lay = (s, f, d, k) => UiSamples.MapDistrictScene(s, f, d, k, t, state, true),
            HoverCommand = UiMapDistrict.RowCommand, HoverArg = 2, Walk = Moves("DDDRRRDDDD")
        };
    }

    // The minimap with a toast, an objective and a prompt all up, by day (13) and by night (11).
    private static Screen MapMinimapScreenOf(string state, string lang, bool night)
    {
        UiWords t = DialogWords(lang);
        return new Screen
        {
            Name = "map_minimap_" + state + (night ? "_night" : string.Empty) + Suffix(lang), AppName = "HUD", Icon = UiIcon.MapTrifold, Backdrop = night ? "11.jpg" : "13.jpg",
            Lay = (s, f, d, k) => UiSamples.MapMinimapScene(s, f, d, k, t, state == "route_long", state),
            HoverCommand = UiObjectives.LocateCommand, HoverArg = 0, Walk = Moves("DDDD")
        };
    }

    // The map's left dock over the real 2.5D picture (or the game's tinted map for the Rent view), the mouse resting on a line and the pad's walk down it.
    private static Screen MapFiltersScreenOf(string state, string lang)
    {
        UiWords t = DialogWords(lang);
        bool picker = state.StartsWith("picker", StringComparison.Ordinal), subway = state == "subway";
        string backdrop = state == "taxi" ? "real_full_flat.png" : subway ? "real_full_flat.png" : picker ? "real_full_junction.png" : state == "filters" ? "real_full_tilt.png" : "map_rent.png";
        return new Screen
        {
            Name = "map_" + state + Suffix(lang), AppName = "City map", Icon = UiIcon.MapTrifold, Backdrop = backdrop,
            Lay = (s, f, d, k) => UiSamples.MapFiltersScene(s, f, d, k, t, state),
            HoverCommand = picker ? UiMapFilters.ChoiceCommand : subway ? UiMapFilters.StationCommand : state == "search" ? UiMapFilters.ResultCommand : UiMapFilters.LineCommand,
            HoverArg = picker || subway ? 2 : state == "search" ? 1 : 7,
            Walk = Moves(picker ? "DDDDD" : subway ? "DDDDDD" : "RRDDDDDDDDDDRRD")
        };
    }
}

/// <summary>
/// Draws the 2.5D map's views into PNGs: <c>-executeMethod Map25Preview.Render -out folder [-city path to a minimap_base file]</c>. With the file, the city
/// is the owner's own (883 footprints); without it, the made-up city of the checks. A second set is drawn with a deliberately tall height model, to
/// show that a block's height has a limit. The two discs in the full tilt shot (the player, the destination) are drawn on the picture here to show the
/// framing: in the game they are the interface's own pins and are not part of the picture.
/// </summary>
public static class Map25Preview
{
    private const string DefaultCity = @"C:\Users\sarvesh\AppData\LocalLow\Hovgaard Games\Big Ambitions\RealLifeOverhaul\minimap_base_3682.bin";

    private enum At { Route, Start, Building, Park, Coast }

    private sealed class Look
    {
        public string Name;
        public int Width, Height;
        public float Heading, Pitch, Across;
        public At Where = At.Route;
        public bool Mini, Fog, Pins, Marks = true;
        public bool Bare;            // the same picture without the player's view cone and halo, for measuring their share of the colour in rendered pixels
        public int Aa;               // multisampling of the picture: 0 for what the game's pictures have (2 for the full map, 4 for the minimap)
        public int Detail;           // for At.Building: which kind of building (see Pick); 0 the one the pointer is on
        public bool AtJunction;      // the player stands on the first junction of the route, looking north: the cone shows on the open ground
    }

    private sealed class World
    {
        public string Prefix;
        public Map25City City;
        public Map25Set Full, Small;
        public Map25Scene Scene;
        public float[] RouteX, RouteZ, RouteY;
        public float StartX, StartZ, EndX, EndZ;
        public int Dest, Yours, Hover;
        public int[] Tinted = new int[0];
        public float ParkX, ParkZ, CoastX, CoastZ, JunctionX, JunctionZ;
        public Map25Real Real;
        public float TargetX, TargetZ;
        public string Notes;
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

    public static void Render()
    {
        try
        {
            string folder = Argument("-out", Path.GetFullPath("map25"));
            string file = Argument("-city", DefaultCity);
            Directory.CreateDirectory(folder);
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }
            if (!Map25Scene.Available(out string why))
            {
                throw new InvalidOperationException(why);
            }
            if (!Map25Scene.Calibrate(out string calibration))
            {
                throw new InvalidOperationException("the 2.5D map cannot draw here: " + calibration);
            }
            Debug.Log("[UiPreview] map25: colour space " + QualitySettings.activeColorSpace + "; " + calibration);
            bool real = File.Exists(file);
            int shots = 0;
            foreach (bool tall in new[] { false, true })
            {
                World world = Build(real ? file : null, tall);
                foreach (Look look in Looks(world, tall))
                {
                    Draw(world, look, folder);
                    shots++;
                }
                Debug.Log("[UiPreview] map25 " + world.Prefix + ": " + world.City.Count + " buildings, " + world.Full.Vertices + " + " + world.Small.Vertices + " vertices; " + world.Notes);
                world.Scene.Dispose();
            }
            Debug.Log("[UiPreview] rendered " + shots + " map views into " + folder);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[UiPreview] " + e);
            EditorApplication.Exit(1);
        }
    }

    private static World Build(string file, bool tall)
    {
        var w = new World { Prefix = (file != null ? "real" : "fixture") + (tall ? "_tall" : "") };
        float[] x, y, z, speed, treeX, treeZ;
        int[] edgeStart, edgeTo;
        float[] extra;
        bool[] usable;
        int count, trees;
        Map25Under under;
        if (file != null)
        {
            Map25Real real = Map25Real.Load(file, tall);
            w.Real = real;
            w.City = real.City;
            real.FindGrid(-420f, -480f, 440f, 520f);
            real.Roads(out x, out y, out z, out edgeStart, out edgeTo, out extra, out usable, out speed, out count);
            real.Trees(out treeX, out treeZ, out trees, out w.ParkX, out w.ParkZ);
            under = Map25Under.Make(real.Cells, real.Columns, real.Rows, real.Cell, real.OriginX, real.OriginZ, -0.15f, x, y, z, edgeStart, edgeTo, extra, usable, speed, count,
                                    -480f, -540f, 960f, 1100f, 0f, treeX, treeZ, trees, -480f, -540f, 480f, 560f);
            // the route: north along an avenue from before the first street, east along it, north along the next avenue, west along the next street
            float a0 = real.Avenues[Math.Min(1, real.Avenues.Count - 1)], a1 = real.Avenues[Math.Min(2, real.Avenues.Count - 1)];
            float s0 = real.Streets[Math.Min(1, real.Streets.Count - 1)], s1 = real.Streets[Math.Min(2, real.Streets.Count - 1)];
            BuildRoute(w, a0, s0 - 150f, a0, s0, a1, s0, a1, s1, a1 - 50f, s1);
            w.JunctionX = a0;
            w.JunctionZ = s0;
            w.Yours = Map25Fixture.Nearest(w.City, a0 + 38f, s0 + 44f);
            w.Hover = Map25Fixture.Nearest(w.City, a0 + 44f, s0 - 70f);
            w.Dest = Map25Fixture.Nearest(w.City, a1 - 50f, s1 + 34f);
            int coral = Map25Fixture.Nearest(w.City, a1 - 36f, s0 + 70f), violet = Map25Fixture.Nearest(w.City, a0 - 44f, s1 - 60f), rent = Map25Fixture.Nearest(w.City, a1 + 54f, s1 + 40f);
            w.Tinted = new[] { w.Yours, coral, violet, rent };
            // the coast: from the middle of the district west along its middle until the water
            w.CoastX = -300f;
            w.CoastZ = 40f;
            for (float cx = -300f; cx > -1500f; cx -= 8f)
            {
                int c = (int)((cx - real.OriginX) / real.Cell), r = (int)((40f - real.OriginZ) / real.Cell);
                if (c >= 0 && c < real.Columns && r >= 0 && r < real.Rows && real.Cells[r * real.Columns + c] == 1)
                {
                    w.CoastX = cx + 80f;
                    break;
                }
            }
            w.Notes = "the owner's footprints and water; heights a model" + (tall ? " (deliberately tall)" : "") + "; roads from the gaps (" + real.Avenues.Count + " avenues, " + real.Streets.Count + " streets); "
                      + trees + " made-up trees (a street tree every 28 m, none in the water, and a park at " + w.ParkX.ToString("F0") + ", " + w.ParkZ.ToString("F0") + ")";
        }
        else
        {
            w.City = Map25Fixture.City(7, tall);
            Map25Fixture.Roads(out x, out y, out z, out edgeStart, out edgeTo, out extra, out usable, out speed, out count);
            Map25Fixture.Water(out byte[] cells, out int columns, out int rows, out float cell, out float ox, out float oz);
            Map25Fixture.Trees(out treeX, out treeZ, out trees);
            under = Map25Under.Make(cells, columns, rows, cell, ox, oz, -0.15f, x, y, z, edgeStart, edgeTo, extra, usable, speed, count, -1500f, -1000f, 3000f, 2000f, 0f,
                                    treeX, treeZ, trees, -900f, -800f, 900f, 800f);
            BuildRoute(w, Map25Fixture.RouteX[0], Map25Fixture.RouteZ[0], Map25Fixture.AvenueX(2), Map25Fixture.StreetZ(1), Map25Fixture.AvenueX(3), Map25Fixture.StreetZ(1),
                       Map25Fixture.AvenueX(3), Map25Fixture.StreetZ(2), Map25Fixture.AvenueX(3) - 36f, Map25Fixture.StreetZ(2));
            w.JunctionX = Map25Fixture.AvenueX(2);
            w.JunctionZ = Map25Fixture.StreetZ(1);
            w.Yours = Map25Fixture.Nearest(w.City, 30f, -60f);
            w.Hover = Map25Fixture.Nearest(w.City, 40f, -140f);
            w.Dest = Map25Fixture.Nearest(w.City, w.EndX, w.EndZ + 30f);
            w.Tinted = new[] { w.Yours, Map25Fixture.Nearest(w.City, 100f, 88f), Map25Fixture.Nearest(w.City, -100f, 30f) };
            w.ParkX = (Map25Fixture.AvenueX(2) + Map25Fixture.AvenueX(3)) * 0.5f;
            w.ParkZ = (Map25Fixture.StreetZ(1) + Map25Fixture.StreetZ(2)) * 0.5f;
            w.CoastX = Map25Fixture.AvenueX(Map25Fixture.Columns) + 60f;
            w.CoastZ = 0f;
            w.Notes = "the made-up city of the checks" + (tall ? " (deliberately tall)" : "");
        }
        // As in the game: the sets are built from a city nothing is highlighted in (NavScene.Job), and the highlights come later, one building at a time, by the scene's retint of
        // the full map's set (NavScene.Tint). The minimap's set is never tinted, so its pictures show no highlight and no ring.
        w.Full = Map25Set.Build(w.City, Map25Detail.Full, -0.3f);
        w.Small = Map25Set.Build(w.City, Map25Detail.Small, -0.3f);
        w.Scene = new Map25Scene(new[] { w.Full, w.Small }, under, w.City.MinX, w.City.MinZ, w.City.MaxX, w.City.MaxZ);
        while (w.Scene.Upload(200.0))
        {
        }
        Color[] colours = { new Color(0.247f, 0.812f, 0.557f), new Color(0.937f, 0.478f, 0.353f), new Color(0.553f, 0.486f, 0.941f), new Color(0.941f, 0.698f, 0.227f) };
        var generator = new Map25Blocks();
        var scratch = new Map25Builders(4096);
        for (int k = 0; k < w.Tinted.Length; k++)
        {
            Color c = colours[k % colours.Length];
            w.City.Tint[w.Tinted[k]] = new Map25Colour(c.r, c.g, c.b);
            w.City.TintShare[w.Tinted[k]] = Map25Palette.TintShare;
            w.Scene.Retint(w.City, w.Tinted[k], generator, scratch);
        }
        w.City.Tint[w.Hover] = new Map25Colour(1f, 1f, 1f);
        w.City.TintShare[w.Hover] = Map25Palette.HoverShare;
        w.Scene.Retint(w.City, w.Hover, generator, scratch);
        w.Scene.Refresh();
        return w;
    }

    // The route through the points given (x, z pairs), a point every 12 m.
    private static void BuildRoute(World w, params float[] p)
    {
        var xs = new List<float>();
        var zs = new List<float>();
        int n = p.Length / 2;
        for (int k = 0; k + 1 < n; k++)
        {
            float x0 = p[k * 2], z0 = p[k * 2 + 1], x1 = p[k * 2 + 2], z1 = p[k * 2 + 3];
            int steps = Math.Max(1, (int)(Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)) / 12f));
            for (int s = k == 0 ? 0 : 1; s <= steps; s++)
            {
                xs.Add(x0 + (x1 - x0) * s / steps);
                zs.Add(z0 + (z1 - z0) * s / steps);
            }
        }
        w.RouteX = xs.ToArray();
        w.RouteZ = zs.ToArray();
        w.RouteY = new float[xs.Count];
        w.StartX = w.RouteX[0];
        w.StartZ = w.RouteZ[0];
        w.EndX = w.RouteX[w.RouteX.Length - 1];
        w.EndZ = w.RouteZ[w.RouteZ.Length - 1];
        w.TargetX = (w.StartX + w.EndX) * 0.5f;
        w.TargetZ = (w.StartZ + w.EndZ) * 0.5f;
    }

    private static List<Look> Looks(World w, bool tall)
    {
        string p = w.Prefix + "_";
        var looks = new List<Look>
        {
            new Look { Name = p + "full_flat", Width = 1920, Height = 1080, Pitch = 90f, Across = 800f, Pins = true },
            new Look { Name = p + "full_tilt", Width = 1920, Height = 1080, Heading = -16f, Pitch = 47f, Across = 760f, Fog = true, Pins = true },
            new Look { Name = p + "mini_flat", Width = 600, Height = 600, Pitch = 90f, Across = 260f, Mini = true, Where = At.Start },
            new Look { Name = p + "mini_tilt", Width = 600, Height = 600, Pitch = 40f, Across = 260f, Mini = true, Where = At.Start, Fog = true },
        };
        if (!tall)
        {
            looks.Add(new Look { Name = p + "full_flat_bare", Width = 1920, Height = 1080, Pitch = 90f, Across = 800f, Bare = true });
            looks.Add(new Look { Name = p + "mini_flat_bare", Width = 600, Height = 600, Pitch = 90f, Across = 260f, Mini = true, Where = At.Start, Bare = true });
            looks.Add(new Look { Name = p + "full_tilt_aa1", Width = 1920, Height = 1080, Heading = -16f, Pitch = 47f, Across = 760f, Fog = true, Pins = true, Aa = 1 });
            looks.Add(new Look { Name = p + "full_p65", Width = 1920, Height = 1080, Pitch = 65f, Across = 780f });
            looks.Add(new Look { Name = p + "full_p75", Width = 1920, Height = 1080, Pitch = 75f, Across = 780f });
            looks.Add(new Look { Name = p + "full_junction", Width = 1920, Height = 1080, Pitch = 47f, Across = 220f, Where = At.Start, AtJunction = true, Fog = true, Pins = true });
            looks.Add(new Look { Name = p + "full_hover", Width = 1920, Height = 1080, Heading = 20f, Pitch = 47f, Across = 240f, Where = At.Building, Fog = true });
            looks.Add(new Look { Name = p + "full_coast", Width = 1920, Height = 1080, Pitch = 55f, Across = 900f, Where = At.Coast, Fog = true });
            looks.Add(new Look { Name = p + "full_park", Width = 1920, Height = 1080, Heading = 10f, Pitch = 47f, Across = 280f, Where = At.Park, Fog = true });
            looks.Add(new Look { Name = p + "detail_tower", Width = 1920, Height = 1080, Heading = 25f, Pitch = 26f, Across = 70f, Where = At.Building, Detail = 1 });
            looks.Add(new Look { Name = p + "detail_roofs", Width = 1920, Height = 1080, Heading = 200f, Pitch = 52f, Across = 80f, Where = At.Building, Detail = 2 });
            looks.Add(new Look { Name = p + "detail_awnings", Width = 1920, Height = 1080, Heading = 0f, Pitch = 38f, Across = 40f, Where = At.Building, Detail = 3 });
        }
        return looks;
    }

    // The building a close look is of: 1 the tallest drawn (on a lot that is not a sliver), 2 one with the most on its roof, 3 a shop front of 15 to 30 m with awnings; the hover one otherwise.
    private static int Pick(World w, int kind)
    {
        Map25City c = w.City;
        int best = w.Hover;
        float most = -1f;
        Map25Layer blocks = w.Full.Layers[(int)Map25Kind.Blocks], fronts = w.Full.Layers[(int)Map25Kind.Fronts];
        var sx = new float[32];
        var sz = new float[32];
        for (int i = 0; i < c.Count; i++)
        {
            float near = Math.Abs(c.HullX[c.HullStart[i]] - w.TargetX) + Math.Abs(c.HullZ[c.HullStart[i]] - w.TargetZ);
            if (near > 420f)
            {
                continue;
            }
            float score = -1f;
            if (kind == 1)
            {
                Map25Height.Measure(c, i, sx, sz, out float shorter, out _);
                score = c.Height[i] * (shorter > 18f ? 1f : 0.2f);
            }
            else if (kind == 2)
            {
                score = c.Height[i] > 18f ? blocks.Count[i] + c.Height[i] * 0.1f : -1f;
            }
            else if (kind == 3)
            {
                Map25Height.Measure(c, i, sx, sz, out float shorter, out float longer);
                bool plain = c.TintShare[i] == 0f;
                score = plain && fronts.Count[i] >= 24 && c.Height[i] >= 10f && c.Height[i] <= 40f && shorter >= 8f && longer <= 70f ? fronts.Count[i] - near * 0.01f : -1f;
                score += score > 0f && SouthIsOpen(c, i, 38f) ? 1000f : 0f;
                score = score > 0f && !LargeWindows(c, i) ? score : -1f;
            }
            if (score > most)
            {
                most = score;
                best = i;
            }
        }
        return best;
    }

    // True when the camera looking north at the building's south wall sees its foot: no building south of it stands higher than the line of sight
    // up to the camera's pitch, over 70 m (the awnings are at the foot of the wall).
    private static bool SouthIsOpen(Map25City c, int i, float pitchDegrees)
    {
        float cx = 0f, minZ = float.MaxValue;
        int n = c.HullStart[i + 1] - c.HullStart[i];
        for (int k = c.HullStart[i]; k < c.HullStart[i + 1]; k++)
        {
            cx += c.HullX[k] / n;
            minZ = Math.Min(minZ, c.HullZ[k]);
        }
        float slope = Mathf.Tan(pitchDegrees * Mathf.Deg2Rad);
        for (float t = 3f; t <= 70f; t += 2f)
        {
            for (int j = 0; j < c.Count; j++)
            {
                if (j != i && c.Height[j] > 3f + t * slope - 1f && Holds(c, j, cx, minZ - t))
                {
                    return false;
                }
            }
        }
        return true;
    }

    // True when the building has more windows than one is drawn with at their own size (its panes are drawn larger): not a building to show a shop front on.
    private static bool LargeWindows(Map25City c, int i)
    {
        int windows = 0;
        int s = c.HullStart[i], e = c.HullStart[i + 1];
        for (int k = s; k < e; k++)
        {
            int f = k + 1 < e ? k + 1 : s;
            windows += Map25Blocks.PaneColumns(Mathf.Sqrt((c.HullX[f] - c.HullX[k]) * (c.HullX[f] - c.HullX[k]) + (c.HullZ[f] - c.HullZ[k]) * (c.HullZ[f] - c.HullZ[k])));
        }
        return windows * Map25Blocks.PaneFloors(c.Height[i], true) > Map25Blocks.PaneCap - 20;
    }

    // Whether the outline of building j (convex, anticlockwise) holds the place.
    private static bool Holds(Map25City c, int j, float x, float z)
    {
        int s = c.HullStart[j], e = c.HullStart[j + 1];
        for (int k = s; k < e; k++)
        {
            int f = k + 1 < e ? k + 1 : s;
            if ((c.HullX[f] - c.HullX[k]) * (z - c.HullZ[k]) - (c.HullZ[f] - c.HullZ[k]) * (x - c.HullX[k]) < 0f)
            {
                return false;
            }
        }
        return true;
    }

    private static void Draw(World w, Look look, string folder)
    {
        var view = new Map25View();
        float aspect = look.Width / (float)look.Height;
        float tx = w.TargetX, tz = w.TargetZ, ty = 0f;
        switch (look.Where)
        {
            case At.Start:
                tx = w.StartX;
                tz = w.StartZ;
                break;
            case At.Park:
                tx = w.ParkX;
                tz = w.ParkZ;
                break;
            case At.Coast:
                tx = w.CoastX;
                tz = w.CoastZ;
                break;
            case At.Building:
                {
                    int i = look.Detail == 0 ? w.Hover : Pick(w, look.Detail);
                    int s = w.City.HullStart[i], e = w.City.HullStart[i + 1];
                    tx = tz = 0f;
                    for (int k = s; k < e; k++)
                    {
                        tx += w.City.HullX[k];
                        tz += w.City.HullZ[k];
                    }
                    tx /= e - s;
                    tz /= e - s;
                    ty = look.Detail == 0 ? 0f : look.Detail == 3 ? 3f : w.City.Height[i] * 0.5f;
                    if (look.Detail == 3)
                    {
                        float south = float.MaxValue;
                        for (int k = s; k < e; k++)
                        {
                            south = Math.Min(south, w.City.HullZ[k]);
                        }
                        tz = south;   // the camera looks at the foot of the south wall, where the awnings are
                    }
                    break;
                }
        }
        float playerX = w.StartX, playerZ = w.StartZ;
        if (look.AtJunction)
        {
            playerX = w.JunctionX;
            playerZ = w.JunctionZ - 5f;
            tx = playerX;
            tz = playerZ + 34f;
        }
        float lead = look.Mini && look.Pitch < 80f ? look.Across * 0.16f : 0f;
        float radians = look.Heading * Mathf.Deg2Rad;
        view.Look(tx + Mathf.Sin(radians) * lead, ty, tz + Mathf.Cos(radians) * lead, look.Heading, look.Pitch, look.Across, aspect);
        float pixels = look.Mini ? look.Width : look.Height;
        float mpp = view.MetresPerPixel(tx, 0f, tz, look.Width);
        w.Scene.SetRoute(w.RouteX, w.RouteY, w.RouteZ, w.RouteX.Length, 0f);
        w.Scene.SetRouteHalf(Map25Marks.Step(Map25Marks.RouteHalf(look.Mini, pixels, mpp)));
        w.Scene.SetMarks(w.City, look.Marks ? w.Tinted : null, look.Marks ? w.Tinted.Length : 0, 0.08f);
        var target = new RenderTexture(look.Width, look.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = look.Aa > 0 ? look.Aa : look.Mini ? 4 : 2 };
        target.Create();
        var frame = new Map25Frame
        {
            Player = !look.Bare, PlayerX = playerX, PlayerY = 0f, PlayerZ = playerZ, Heading = 0f, Fog = look.Fog,
            ConeRadius = Map25Marks.ConeMetres(look.Mini, pixels, mpp), HaloRadius = Map25Marks.HaloMetres(look.Mini, pixels, mpp)
        };
        w.Scene.Draw(target, view, frame, look.Mini ? 1 : 0);
        Measure(w, look, view, frame, playerX, playerZ);
        RenderTexture before = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(look.Width, look.Height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, look.Width, look.Height), 0, 0);
        if (look.Pins)
        {
            Pin(texture, view, playerX, 0f, playerZ, look.Width, look.Height, 11f, Color.white, new Color(0.31f, 0.72f, 0.92f));
            int d = w.Dest, s = w.City.HullStart[d];
            Pin(texture, view, (w.City.HullX[s] + w.City.HullX[s + 2]) * 0.5f, w.City.Height[d] * view.Rise, (w.City.HullZ[s] + w.City.HullZ[s + 2]) * 0.5f, look.Width, look.Height, 9f,
                new Color(0.937f, 0.365f, 0.365f), Color.white);
        }
        texture.Apply();
        File.WriteAllBytes(Path.Combine(folder, look.Name + ".png"), texture.EncodeToPNG());
        RenderTexture.active = before;
        UnityEngine.Object.DestroyImmediate(texture);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }

    // What the picture draws, in its own pixels, from the same view the picture was drawn with: the route's line and its casing, the view cone's length, the halo's radius, and where
    // the player and the destination stand (a place outside the picture is said so). One line a picture, in the preview's log.
    private static void Measure(World w, Look look, Map25View view, Map25Frame frame, float playerX, float playerZ)
    {
        float half = w.Scene.RouteHalf;
        int s0 = w.City.HullStart[w.Dest];
        float destX = (w.City.HullX[s0] + w.City.HullX[s0 + 2]) * 0.5f, destZ = (w.City.HullZ[s0] + w.City.HullZ[s0 + 2]) * 0.5f;
        string where(float x, float y, float z)
        {
            return view.Project(x, y, z, out float u, out float v) ? (u >= 0f && u <= 1f && v >= 0f && v <= 1f ? Mathf.RoundToInt(u * look.Width) + "," + Mathf.RoundToInt((1f - v) * look.Height) : "outside") : "behind";
        }
        Debug.Log("[UiPreview] map25 measure " + look.Name + ": route line " + Pixels(view, w.RouteX[2], w.RouteZ[2], half * 2f, 0f, look).ToString("F1") + " px, casing "
                  + Pixels(view, w.RouteX[2], w.RouteZ[2], half * 2f * Map25Marks.CasingShare, 0f, look).ToString("F1") + " px, cone " + Pixels(view, playerX, playerZ, 0f, frame.ConeRadius, look).ToString("F1")
                  + " px, halo radius " + Pixels(view, playerX, playerZ, frame.HaloRadius, 0f, look).ToString("F1") + " px, player at " + where(playerX, 0f, playerZ) + ", destination at "
                  + where(destX, w.City.Height[w.Dest] * view.Rise, destZ));
    }

    // The distance on the picture, in pixels, of a segment on the ground from a place: along x by dx and along z by dz.
    private static float Pixels(Map25View view, float x, float z, float dx, float dz, Look look)
    {
        if (!view.Project(x, 0f, z, out float u0, out float v0) || !view.Project(x + dx, 0f, z + dz, out float u1, out float v1))
        {
            return 0f;
        }
        float px = (u1 - u0) * look.Width, py = (v1 - v0) * look.Height;
        return Mathf.Sqrt(px * px + py * py);
    }

    // A disc of a colour with a ring round it at a place of the picture (the preview's stand-in for the interface's pin).
    private static void Pin(Texture2D t, Map25View view, float x, float y, float z, int width, int height, float radius, Color disc, Color ring)
    {
        if (!view.Project(x, y, z, out float u, out float v))
        {
            return;
        }
        int cx = Mathf.RoundToInt(u * width), cy = Mathf.RoundToInt(v * height);
        int r = Mathf.CeilToInt(radius * 1.8f);
        for (int py = Math.Max(0, cy - r); py <= Math.Min(height - 1, cy + r); py++)
        {
            for (int px = Math.Max(0, cx - r); px <= Math.Min(width - 1, cx + r); px++)
            {
                float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                if (d <= radius)
                {
                    t.SetPixel(px, py, disc);
                }
                else if (d <= radius * 1.6f)
                {
                    t.SetPixel(px, py, Color.Lerp(t.GetPixel(px, py), ring, 0.45f));
                }
            }
        }
    }
}
