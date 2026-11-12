using System;
using RealLifeOverhaul.Ui;

// The owner, 6 Nov: "When clicking a taxi it is not coming in the center of the page to travel instead it is going to bottom right corner which
// is making it completely hidden". A taxi opens the city map in taxi mode, whose dock (the place card with Travel here) is the window the player
// moved by its strip. Its kept place was the owner's, dragged to the right edge: the card then stood under the layers and the buttons. A kept place
// is now kept inside the room the screen leaves the window, a place that is no number is no place, and the lists of the world stand centred whole.
// Run: --taxi-pick; the lines read "taxi pick: ...".
internal static partial class Program
{
    private static readonly float[][] TaxiPickSizes =
    {
        new[] { 1280f, 720f }, new[] { 1920f, 1080f }, new[] { 2520f, 1080f }, new[] { 3840f, 1080f },   // a UI scale of 1.5 down to an ultrawide of 21:9 and 32:9
        new[] { 2560f, 1440f }, new[] { 1536f, 864f },
    };

    private static void UiTaxiPickChecks()
    {
        UiWords en = UiWords("en");
        string saved = null;
        UiWindowPlaces.Load = () => saved;
        UiWindowPlaces.Save = line => saved = line;
        string[] places =
        {
            string.Empty, "CityMapFilters|1349|-8", "CityMapFilters|9000|9000", "CityMapFilters|-9000|-9000", "CityMapFilters|1E+30|-1E+30", "CityMapFilters|Infinity|NaN",
            "CityMapFilters|0|0", "CityMapFilters|1920|962",
        };
        foreach (float[] size in TaxiPickSizes)
        {
            var frame = new UiFrame { Width = size[0], Height = size[1], Band = UiFrame.BandHeight };
            string at = (int)size[0] + "x" + (int)size[1];
            foreach (string place in places)
            {
                foreach (string state in new[] { "taxi", "overview", "hover", "unrated" })
                {
                    UiWindowPlaces.Forget();
                    saved = place;
                    var scene = new UiScene { Draggable = true };
                    var grip = new UiWindowGrip { Id = "CityMapFilters", Saved = true };
                    string what = "taxi pick: " + at + ", " + (place.Length == 0 ? "no kept place" : place.Substring(15)) + ", " + state;
                    if (state == "taxi")
                    {
                        var map = new MapScreenModel
                        {
                            Filters = UiSamples.MapFilters(en, "taxi"), Buttons = UiSamples.MapButtons(en, false),
                        };
                        UiMap.Lay(scene, map, frame, true, UiDefaultKeys.Keyboard);
                    }
                    else
                    {
                        UiSamples.MapPlaceScene(scene, frame, true, UiDefaultKeys.Keyboard, en, state);
                    }
                    grip.Place(scene, frame.Width, frame.BandY);
                    float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                    for (int i = 0; i < scene.ItemCount; i++)
                    {
                        ref UiItem it = ref scene.Items[i];
                        if (!it.Window || it.Clip >= 0 || it.Layer != 0 || (it.Flags & (UiFlags.Prompt | UiFlags.Overflow)) != 0)
                        {
                            continue;
                        }
                        x0 = Math.Min(x0, it.X);
                        x1 = Math.Max(x1, it.X + it.W);
                        y0 = Math.Min(y0, it.Y);
                        y1 = Math.Max(y1, it.Y + it.H);
                    }
                    ref UiItem card = ref scene.Items[scene.HandlePanel];
                    float room = frame.Width - UiTokens.S4 * 2f - UiMapChrome.LayersWidth, cardRight = card.X + card.W;
                    bool whole = x0 >= -0.01f && y0 >= -0.01f && y1 <= frame.BandY + 0.01f && x1 <= frame.Width + 0.01f;
                    bool clear = state == "taxi" ? cardRight <= frame.Width - UiTokens.S4 * 2f - UiMapChrome.Button + 0.01f : cardRight <= room + 0.01f;
                    Check(whole && clear, what + ": the window stands whole on the screen and clear of the layers and buttons (" + x0 + ".." + x1 + ", " + y0 + ".." + y1 + ")");
                    Check(WindowTargetsInside(scene, frame.Width, frame.BandY), what + ": every target of the window is on the screen");
                }
            }
        }

        // The kept place that is no place: nothing but a number inside the reach of a screen is read back.
        UiWindowPlaces.Forget();
        saved = "A|Infinity|1;B|1E+30|1;C|NaN|1;D|12|-30;E|-70000|0";
        bool a = UiWindowPlaces.Get("A", out _, out _), b = UiWindowPlaces.Get("B", out _, out _), c = UiWindowPlaces.Get("C", out _, out _);
        bool d = UiWindowPlaces.Get("D", out float dx, out float dy), e = UiWindowPlaces.Get("E", out _, out _);
        Check(!a && !b && !c && !e && d && dx == 12f && dy == -30f, "taxi pick: a kept place that is endless, not a number or beyond any screen is dropped, a real one is read back");
        Check(UiWindowDrag.Contain(10f, 50f, 0f, 100f, float.PositiveInfinity) == 0f && UiWindowDrag.Contain(10f, 50f, 0f, 100f, float.NaN) == 0f
            && UiWindowDrag.Contain(10f, 50f, 0f, 100f, 1e30f) == 0f, "taxi pick: the clamp reads a move that is no number as none");

        // The world's lists (the station desk, the bus stops, the hotel desk) stand centred and whole at every size.
        foreach (float[] size in TaxiPickSizes)
        {
            var frame = new UiFrame { Width = size[0], Height = size[1], Band = UiFrame.BandHeight };
            string at = (int)size[0] + "x" + (int)size[1];
            foreach (bool pages in new[] { false, true })
            {
                var scene = new UiScene();
                UiPickerModel m = UiSamples.WorldDesk(en, pages);
                m.PerPage = UiPicker.PerPageFor(m, 8, frame.BandY - UiTokens.S8 * 2f - UiPicker.Height(new UiPickerModel
                {
                    Title = m.Title, Icon = m.Icon, Said = m.Said, Entries = new[] { new UiPickEntry { Name = string.Empty } }, PerPage = 1,
                }) + UiTokens.Row - (UiTokens.Control + UiTokens.S2));
                UiWorld.PickerLay(scene, m, frame, true, UiDefaultKeys.Keyboard);
                ref UiItem panel = ref scene.Items[PickPanel(scene)];
                bool centred = Math.Abs(panel.X + panel.W / 2f - frame.Width / 2f) <= 1f;
                Check(centred && panel.X >= 0f && panel.X + panel.W <= frame.Width && panel.Y >= 0f && panel.Y + panel.H <= frame.BandY + 0.01f,
                    "taxi pick: " + at + ": the world's list stands centred and whole above the bar (" + panel.X + ", " + panel.Y + ", " + panel.W + " x " + panel.H + ")");
            }
        }
    }

    private static bool WindowTargetsInside(UiScene scene, float width, float bottom)
    {
        for (int i = 0; i < scene.NodeCount; i++)
        {
            if (!scene.Nodes[i].Window || scene.Nodes[i].Clip >= 0)
            {
                continue;
            }
            scene.NodeBox(i, out float x, out float y, out float w, out float h);
            if (x < -0.01f || y < -0.01f || x + w > width + 0.01f || y + h > bottom + 0.01f)
            {
                return false;
            }
        }
        return true;
    }

    private static int PickPanel(UiScene scene)
    {
        for (int i = 0; i < scene.ItemCount; i++)
        {
            if ((scene.Items[i].Flags & UiFlags.Panel) != 0)
            {
                return i;
            }
        }
        return 0;
    }
}
