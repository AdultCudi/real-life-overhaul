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
        Map6Checks();
        Map5TextChecks();   // the game's words on the place card: no tag drawn as text, and the chips whole
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

        // The city map's 2D and 3D button: two states, the default is tilted.
        Check(NavMapTilt.Next(NavMapTilt.Auto) == NavMapTilt.Flat && NavMapTilt.Next(NavMapTilt.Flat) == NavMapTilt.Tilted && NavMapTilt.Next(NavMapTilt.Tilted) == NavMapTilt.Flat
              && NavMapTilt.Next(9) == NavMapTilt.Flat, "city map: the 2D and 3D button goes flat from the default or tilted, and tilted from flat");
        Check(NavMapTilt.Pitch(NavMapTilt.Flat, 120f) == RealLifeOverhaul.Map25.Map25View.FlatPitch && NavMapTilt.Pitch(NavMapTilt.Tilted, 340f) == RealLifeOverhaul.Map25.Map25View.TiltPitch
              && NavMapTilt.Pitch(NavMapTilt.Auto, 340f) == RealLifeOverhaul.Map25.Map25View.TiltPitch && NavMapTilt.Pitch(NavMapTilt.Auto, 100f) == RealLifeOverhaul.Map25.Map25View.TiltPitch,
            "city map: flat and tilted ignore the distance, and the default is tilted at every distance");
        Check(NavMapTilt.Words(NavMapTilt.Flat, 100f) == "2D" && NavMapTilt.Words(NavMapTilt.Tilted, 340f) == "3D" && NavMapTilt.Words(NavMapTilt.Auto, 340f) == "3D"
              && NavMapTilt.Words(NavMapTilt.Auto, 100f) == "3D", "city map: the tile says 2D while the map stands flat, 3D while it stands tilted");
        // The button turns the game's own map camera, in every frame the map is open (not only when the camera stands still), and never the game's main camera or its culling mask.
        string camera = Map5Source("Navigation/NavMapCamera.cs");
        Check(camera.Contains("NavMapTilt.Pitch(NavTilt.Map, map.cityMapCam.distance)") && !camera.Contains("Resting") && !camera.Contains("IsBlending") && !camera.Contains("cullingMask")
              && camera.Contains("_virtual.position = pivot - direction * distance;"),
            "city map: the 2D and 3D button moves the game's own map camera along its arc in every frame the map is open, panning or not, and never touches the culling mask");
        Check(Map5Source("Navigation/NavDirector.Map.cs").Contains("OpenCamera(open && !_voogle);"), "city map: the button's camera work goes on and off with the map, and is left alone when another mod owns the map");

        // Sources: the destination block and the button are made once with the panel, write only a changed text, and the button is the one part of the view that takes the pointer.
        string dest = Map5Source("Navigation/NavHud.Destination.cs");
        Check(dest.Contains("private void DestinationMake()") && Map5Source("Navigation/NavHud.Make.cs").Contains("DestinationMake();"), "minimap: the destination block is made with the panel, once");
        Check(dest.Contains("address != _subShown") && dest.Contains("right != _rightShown") && dest.Contains("if (_switchText.text != words)"),
            "minimap: the address, the minutes and the switch's words are written only when they change");
        int takes = Map5Count(dest, "raycastTarget = true");
        string strip = Map5Source("Navigation/NavHud.Strip.cs"), scene = Map5Source("Navigation/NavHud.Scene.cs");
        Check(takes == 1 && !strip.Contains("raycastTarget = true") && !scene.Contains("raycastTarget = true"),
            "minimap: the 2D and 3D button is the one thing in the view that takes the pointer (" + takes + " in its file, none in the strip or the picture)");
        Check(Map5Source("Navigation/NavDirector.cs").Contains("NavTilt.Minimap") && Map5Source("Navigation/NavMapCamera.cs").Contains("NavMapTilt.Pitch(NavTilt.Map"),
            "minimap and city map: the director and the camera read the player's tilt from NavTilt, and nothing else decides it");
    }

    // The owner's test of 5 Nov: the picked place card said "Open<br>Closing in 7 hours (17:00)" with the tag drawn as text, and "Business owned..." was cut short. The game writes
    // its labels for TextMeshPro; the kit draws plain text.
    private static void Map5TextChecks()
    {
        Check(MapText.Plain("Open<br>Closing in 7 hours (17:00)") == "Open\nClosing in 7 hours (17:00)" && MapText.OneLine("Open<br>Closing in 7 hours (17:00)") == "Open" + MapText.Join + "Closing in 7 hours (17:00)",
            "city map text: the game's break tag between \"Open\" and \"Closing in 7 hours (17:00)\" is a line break, or a middle dot where the kit has one line");
        Check(MapText.OneLine("A<BR/>B<br />C") == "A" + MapText.Join + "B" + MapText.Join + "C" && MapText.OneLine("<b>Open</b> <color=#ff0000>now</color>") == "Open now"
              && MapText.OneLine("<sprite=3> Rent") == "Rent" && MapText.OneLine("A<br><br>B") == "A" + MapText.Join + "B" && MapText.Plain("a\r\nb") == "a\nb",
            "city map text: break tags in any case, bold and colour tags and a sprite tag are taken out, empty lines are dropped, and a carriage return goes");
        Check(MapText.OneLine("Rent < $5 and > $2") == "Rent < $5 and > $2" && MapText.OneLine("Fish <3 chips") == "Fish <3 chips" && MapText.OneLine("  plain  ") == "plain" && MapText.OneLine(null) == null && MapText.OneLine(string.Empty) == string.Empty,
            "city map text: a less-than sign in a price or a comparison is not a tag, and plain words come back as they were");
        string picked = Map5Source("Ui/MapDock/MapLive.Picked.cs"), lines = Map5Source("Ui/MapDock/MapLive.Lines.cs"), place = Map5Source("Ui/MapDock/MapLive.Place.cs");
        Check(picked.Contains("MapText.OneLine(LabelLines(label))") && picked.Contains("MapText.Plain(text.text)") && lines.Contains("MapText.OneLine(text.text)") && !picked.Contains("return text != null ? text.text : null;")
              && !place.Contains("TextContainer.text") && !lines.Contains("? text.text : null"),
            "city map text: every place the dock reads a game label (the card's labels, the groups' and lines' names) takes its tags out first");

        // The picked block and the place card at every language the dock has, with the longest words the game gives: both chips whole, none past the column, and the block tall enough.
        string[][] states =
        {
            new[] { "en", "Open" + MapText.Join + "Closing in 7 hours (17:00)", "Business owned by Pioneer Game Studio" },
            new[] { "pt", "Aberto" + MapText.Join + "Fecha em 7 horas (17:00)", "Negócio pertencente a Pioneer Game Studio" },
            new[] { "zh-cn", "营业中" + MapText.Join + "7 小时后关门（17:00）", "企业所有者：Pioneer Game Studio 工作室" }
        };
        int cases = 0, cut = 0, wide = 0, short_ = 0;
        string fault = null;
        foreach (string[] state in states)
        {
            UiWords t = UiWords(state[0]);
            foreach (float[] size in new[] { new[] { 1920f, 1080f }, new[] { 1280f, 720f } })
            {
                var frame = new UiFrame { Width = size[0], Height = size[1], Band = UiFrame.BandHeight };
                MapPanelModel m = UiSamples.MapFilters(t, "filters");
                m.Place.Chips = new[] { new MapStateChip { Label = state[1], Tone = UiRole.Ok }, new MapStateChip { Label = state[2], Tone = UiRole.Warn } };
                var s = new UiScene();
                UiMapFilters.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
                cases++;
                float column = UiMapFilters.Width - UiTokens.S4 * 2f;
                foreach (string label in new[] { state[1], state[2] })
                {
                    int at = -1;
                    for (int i = 0; i < s.ItemCount; i++)
                    {
                        if (s.Items[i].Kind == UiKind.Text && s.Items[i].Text == label)
                        {
                            at = i;
                        }
                    }
                    if (at < 0 || s.Items[at].W + 0.5f < UiTextRules.Width(label, UiStyle.SmallStrong))
                    {
                        cut++;
                        fault ??= "the chip \"" + label + "\" is cut short or missing (" + state[0] + " " + size[0] + "x" + size[1] + ")";
                    }
                }
                float buttons = 0f, panelFoot = 0f;
                for (int i = 0; i < s.NodeCount; i++)
                {
                    if (s.Nodes[i].Command == UiMapFilters.PickedDestinationCommand || s.Nodes[i].Command == UiMapFilters.PickedLocateCommand)
                    {
                        buttons = Math.Max(buttons, s.Nodes[i].Y + s.Nodes[i].H);
                    }
                }
                for (int i = 0; i < s.ItemCount; i++)
                {
                    if ((s.Items[i].Flags & UiFlags.Panel) != 0 && s.Items[i].Layer == 0 && s.Items[i].W == UiMapFilters.Width)
                    {
                        panelFoot = Math.Max(panelFoot, s.Items[i].Y + s.Items[i].H);
                    }
                }
                short_ += buttons > 0f && buttons <= panelFoot + 0.01f ? 0 : 1;
                wide += UiMapPlace.ChipsHeight(m.Place.Chips, column) > 0f ? 0 : 1;
                fault ??= buttons > panelFoot + 0.01f ? "the picked block's buttons stand past the panel (" + buttons + " of " + panelFoot + ", " + state[0] + " " + size[0] + "x" + size[1] + ")" : null;
            }
        }
        Check(cut == 0 && short_ == 0 && wide == 0,
            "city map text: on the picked place the open state with its hours and the business owner's chip are both drawn whole in English, Portuguese and Chinese, in rows, and the block grows to hold them (" + cases + " layouts)"
            + (fault == null ? string.Empty : ": " + fault));
        float two = UiMapPlace.ChipsHeight(new[] { new MapStateChip { Label = "A" }, new MapStateChip { Label = "B" } }, 300f);
        float one = UiMapPlace.ChipsHeight(new[] { new MapStateChip { Label = "A" } }, 300f);
        float rows = UiMapPlace.ChipsHeight(new[] { new MapStateChip { Label = new string('W', 28) }, new MapStateChip { Label = new string('W', 28) } }, 300f);
        Check(one == UiTokens.Badge + UiTokens.S1 && two == one && rows > one && UiMapPlace.ChipsHeight(new MapStateChip[0], 300f) == 0f,
            "city map text: short chips share a row, chips that do not fit together take a row each, and no chips take no room");
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
