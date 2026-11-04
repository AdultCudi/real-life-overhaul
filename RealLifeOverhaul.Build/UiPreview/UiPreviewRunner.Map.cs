using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Map25;
using UnityEditor;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // The 2.5D map is drawn first, by the mod's own renderer (Scripts/Ui/Map) from a made-up city of the game's size, into pictures
    // that stand behind the map's panels (the picture of the game's own map is a 3D scene the preview cannot draw). The renderer
    // is the one the game runs; what is made up is the city (Map25Fixture) and the camera, which stands where the game's map camera
    // does (47 degrees down, a few hundred metres up).
    static partial void AddMap(List<Screen> screens)
    {
    }
}

/// <summary>Draws the 2.5D map's views of the fixture city into PNGs: <c>-executeMethod Map25Preview.Render -out folder</c>.</summary>
public static class Map25Preview
{
    public sealed class Look
    {
        public string Name;
        public int Width, Height;
        public float Heading, Pitch, Across;
        public bool Player, Route, Fog;
        public int Building = -1;     // look at this building of the fixture city, from close up
    }

    public static readonly Look[] Looks =
    {
        new Look { Name = "map25_full_flat", Width = 1920, Height = 1080, Heading = 0f, Pitch = 90f, Across = 330f, Player = true, Route = true },
        new Look { Name = "map25_full_tilt", Width = 1920, Height = 1080, Heading = -16f, Pitch = 47f, Across = 300f, Player = true, Route = true, Fog = true },
        new Look { Name = "map25_mini_flat", Width = 600, Height = 600, Heading = 0f, Pitch = 90f, Across = 260f, Player = true, Route = true },
        new Look { Name = "map25_mini_tilt", Width = 600, Height = 600, Heading = 0f, Pitch = 40f, Across = 260f, Player = true, Route = true, Fog = true },
        new Look { Name = "map25_close_a", Width = 1920, Height = 1080, Heading = 20f, Pitch = 38f, Across = 70f, Building = 7 },
        new Look { Name = "map25_close_b", Width = 1920, Height = 1080, Heading = 200f, Pitch = 38f, Across = 70f, Building = 8 },
    };

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
            Directory.CreateDirectory(folder);
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }
            if (!Map25Scene.Available(out string why))
            {
                throw new InvalidOperationException(why);
            }
            Debug.Log("[UiPreview] map25: colour space " + QualitySettings.activeColorSpace);
            Map25City city = Map25Fixture.City(7);
            Tint(city);
            Map25Set set = Map25Set.Build(city, Map25Detail.Full, 0f);
            Map25Fixture.Roads(out float[] x, out float[] y, out float[] z, out int[] es, out int[] et, out float[] ex, out bool[] us, out float[] sp, out int n);
            Map25Chunk[] roads = Map25Ground.Roads(x, y, z, es, et, ex, us, sp, n, -1500f, -800f, 3500f, 1700f, 0f);
            Map25Fixture.Water(out byte[] cells, out int columns, out int rows, out float cell, out float ox, out float oz);
            Map25Chunk[] land = Map25Ground.Land(cells, columns, rows, cell, ox, oz, -0.4f);
            Map25Set small = Map25Set.Build(city, Map25Detail.Small, 0f);
            if (!Map25Scene.Calibrate(out string calibration))
            {
                throw new InvalidOperationException("the 2.5D map cannot draw here: " + calibration);
            }
            Debug.Log("[UiPreview] map25: " + calibration);
            var scene = new Map25Scene(new[] { set, small }, land, roads, city.MinX, city.MinZ, city.MaxX, city.MaxZ);
            while (scene.Upload(50.0))
            {
            }
            var ry = new float[Map25Fixture.RouteX.Length];
            scene.SetRoute(Map25Fixture.RouteX, ry, Map25Fixture.RouteZ, Map25Fixture.RouteX.Length, 3.5f, 0f);
            foreach (Look look in Looks)
            {
                Draw(scene, city, look, folder, "");
            }
            scene.Dispose();
            Debug.Log("[UiPreview] rendered " + Looks.Length + " map views into " + folder + " (" + set.Vertices + " vertices, " + set.Triangles + " triangles, built in " + set.Milliseconds.ToString("F0") + " ms)");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[UiPreview] " + e);
            EditorApplication.Exit(1);
        }
    }

    // A few buildings the way the game's filters tint them: yours green, a rival's coral, one for rent, a job.
    private static void Tint(Map25City city)
    {
        int[] pick =
        {
            Map25Fixture.Nearest(city, 70f, -60f), Map25Fixture.Nearest(city, 100f, 88f), Map25Fixture.Nearest(city, 262f, -72f),
            Map25Fixture.Nearest(city, -100f, 30f), Map25Fixture.Nearest(city, 262f, 176f)
        };
        Map25Colour[] tints =
        {
            Map25Colour.Hex(0x3FCF8E), Map25Colour.Hex(0xEF7A5A), Map25Colour.Hex(0xEF7A5A), Map25Colour.Hex(0x8D7CF0), Map25Colour.Hex(0xF0B23A)
        };
        for (int k = 0; k < pick.Length; k++)
        {
            city.Tint[pick[k]] = tints[k];
            city.TintShare[pick[k]] = Map25Palette.TintShare;
        }
    }

    private static void Draw(Map25Scene scene, Map25City city, Look look, string folder, string suffix)
    {
        var view = new Map25View();
        float aspect = look.Width / (float)look.Height;
        float px = Map25Fixture.RouteX[0], pz = Map25Fixture.RouteZ[0];
        float tx = look.Name.Contains("full") ? 88f : px, tz = look.Name.Contains("full") ? -96f : pz + 60f;
        float ty = 0f;
        if (look.Building >= 0)
        {
            int s = city.HullStart[look.Building], e = city.HullStart[look.Building + 1];
            tx = tz = 0f;
            for (int k = s; k < e; k++)
            {
                tx += city.HullX[k];
                tz += city.HullZ[k];
            }
            tx /= e - s;
            tz /= e - s;
            ty = city.Height[look.Building] * 0.5f;
        }
        view.Look(tx, ty, tz, look.Heading, look.Pitch, look.Across, aspect);
        var target = new RenderTexture(look.Width, look.Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        target.Create();
        var frame = new Map25Frame { Player = look.Player, PlayerX = px, PlayerY = 0f, PlayerZ = pz, Heading = 0f, ConeRadius = look.Across * 0.34f, Fog = look.Fog };
        scene.Draw(target, view, frame, look.Name.Contains("mini") ? 1 : 0);
        RenderTexture before = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(look.Width, look.Height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, look.Width, look.Height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(folder, look.Name + suffix + ".png"), texture.EncodeToPNG());
        RenderTexture.active = before;
        UnityEngine.Object.DestroyImmediate(texture);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }
}
