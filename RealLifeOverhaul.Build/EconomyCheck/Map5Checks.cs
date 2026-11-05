using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Nav;
using RealLifeOverhaul.Ui;

// The city map's panels of the interface kit (stage 5 of the 2.5D map job): the minimap's destination block and its 2D and 3D button, the map's tilt
// button, the filters' dock and its counts, the picker, the place card and its route strip, the district view and its monthly snapshots. Each part has its
// game-free rules checked here; the screens' layouts are laid out by UiChecks (UiChecks.Map.cs) at every frame and language, and the sources are scanned.
// `dotnet EconomyCheck.dll --map5` runs this file alone.
internal static partial class Program
{
    private static string Map5Root() => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Map5Source(string relative) => File.ReadAllText(System.IO.Path.Combine(Map5Root(), "Assets", "Mods", "RealLifeOverhaul", "Scripts", relative.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    private static void Map5Checks()
    {
        Map5MinimapChecks();
        Map5RuleChecks();
        WorkQueueChecks();   // the wraps on the game's queue of work on the city map's buildings, against replicas of its class
    }

    // ------------------------------------------------------------------------------------------------- the minimap's destination block

    private static void Map5MinimapChecks()
    {
        // The minutes left are the game's clock minutes, rounded up, at least one while any is left, never past three figures.
        Check(NavDestRules.Minutes(0f, 1f) == 1 && NavDestRules.Minutes(-5f, 1f) == 1 && NavDestRules.Minutes(30f, 0f) == 1,
            "minimap: a route with nothing known left reads one minute, never zero or less");
        Check(NavDestRules.Minutes(61f, 1f / 60f) == 2 && NavDestRules.Minutes(60f, 1f / 60f) == 1 && NavDestRules.Minutes(59f, 1f / 60f) == 1,
            "minimap: a minute and a second left is two minutes, exactly one minute is one (rounded up, never promising more than it takes)");
        Check(NavDestRules.Minutes(300f, 0.5f) == 150 && NavDestRules.Minutes(30f, 2f) == 60 && NavDestRules.Minutes(1e9f, 1f) == NavDestRules.MostMinutes,
            "minimap: the clock's rate scales the seconds, and the figure stops at " + NavDestRules.MostMinutes);
        NavDestRules.Split(45, out int h0, out int m0);
        NavDestRules.Split(99, out int h1, out int m1);
        NavDestRules.Split(100, out int h2, out int m2);
        NavDestRules.Split(343, out int h3, out int m3);
        NavDestRules.Split(0, out int h4, out int m4);
        Check(h0 == 0 && m0 == 45 && h1 == 0 && m1 == 99 && h2 == 1 && m2 == 40 && h3 == 5 && m3 == 43 && h4 == 0 && m4 == 1,
            "minimap: under 100 minutes the block says minutes, from 100 hours and minutes (343 is 5 h 43), and never less than one minute");
        Check(NavDestRules.Switch(true) == "3D" && NavDestRules.Switch(false) == "2D", "minimap: the picture's switch says what the picture is now: 3D tilted, 2D flat");
        Check(NavDestRules.TiltedFrom(1) && !NavDestRules.TiltedFrom(0) && NavDestRules.TiltedFrom(-1) && NavDestRules.TiltedFrom(7),
            "minimap: the saved tilt is flat only for 0; a missing or odd value is the default, tilted");

        // The city map's tilt button: a ring of three, each with its own pitch.
        Check(NavMapTilt.Next(NavMapTilt.Auto) == NavMapTilt.Flat && NavMapTilt.Next(NavMapTilt.Flat) == NavMapTilt.Tilted && NavMapTilt.Next(NavMapTilt.Tilted) == NavMapTilt.Auto
              && NavMapTilt.Next(9) == NavMapTilt.Flat, "city map: the tilt button goes by the camera's distance, then flat, then tilted, then by distance again");
        Check(NavMapTilt.Pitch(NavMapTilt.Flat, 120f) == RealLifeOverhaul.Map25.Map25View.FlatPitch && NavMapTilt.Pitch(NavMapTilt.Tilted, 340f) == RealLifeOverhaul.Map25.Map25View.TiltPitch
              && NavMapTilt.Pitch(NavMapTilt.Auto, 340f) == RealLifeOverhaul.Map25.Map25View.PitchFor(340f) && NavMapTilt.Pitch(NavMapTilt.Auto, 100f) == RealLifeOverhaul.Map25.Map25View.PitchFor(100f),
            "city map: flat and tilted ignore the distance, by distance is the pitch the map had before the button");
        Check(NavMapTilt.Words(NavMapTilt.Flat, 100f) == "2D" && NavMapTilt.Words(NavMapTilt.Tilted, 340f) == "3D" && NavMapTilt.Words(NavMapTilt.Auto, 340f) == "2D"
              && NavMapTilt.Words(NavMapTilt.Auto, 100f) == "3D", "city map: the tile says 2D while the picture stands flat, 3D while it stands tilted, whichever way it got there");

        // Sources: the destination block and the button are made once with the panel, write only a changed text, and the button is the one part of the view that takes the pointer.
        string dest = Map5Source("Navigation/NavHud.Destination.cs");
        Check(dest.Contains("private void DestinationMake()") && Map5Source("Navigation/NavHud.Make.cs").Contains("DestinationMake();"), "minimap: the destination block is made with the panel, once");
        Check(dest.Contains("address != _subShown") && dest.Contains("right != _rightShown") && dest.Contains("if (_switchText.text != words)"),
            "minimap: the address, the minutes and the switch's words are written only when they change");
        int takes = Map5Count(dest, "raycastTarget = true");
        string strip = Map5Source("Navigation/NavHud.Strip.cs"), scene = Map5Source("Navigation/NavHud.Scene.cs");
        Check(takes == 1 && !strip.Contains("raycastTarget = true") && !scene.Contains("raycastTarget = true"),
            "minimap: the 2D and 3D button is the one thing in the view that takes the pointer (" + takes + " in its file, none in the strip or the picture)");
        Check(Map5Source("Navigation/NavDirector.cs").Contains("NavTilt.Minimap") && Map5Source("Navigation/NavMap25.cs").Contains("NavMapTilt.Pitch(NavTilt.Map"),
            "minimap and city map: the director and the picture read the player's tilt from NavTilt, and nothing else decides it");
    }

    private static int Map5Count(string text, string word)
    {
        int count = 0, at = 0;
        while ((at = text.IndexOf(word, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += word.Length;
        }
        return count;
    }
}
