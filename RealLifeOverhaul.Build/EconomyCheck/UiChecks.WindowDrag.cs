using System;
using RealLifeOverhaul.Ui;

// A window moved by its title strip (the owner, 5 Nov: "I am unable to drag the modals which was available in native UI"). The game's
// DraggableWindow moves a small window by its handle, keeps it whole inside the screen, remembers it by the window's id in the player's
// settings and has a reset in the options. Run: --window-drag; the lines read "window drag: ...".
internal static partial class Program
{
    private static void UiWindowDragChecks()
    {
        UiWindowPlaces.Forget();
        string saved = null;
        UiWindowPlaces.Load = () => saved;
        UiWindowPlaces.Save = line => saved = line;

        var m = new UiToolPanelModel { Title = "Sign", Icon = UiIcon.PaintBrush, Close = true, Lines = new UiToolLine[2], LineCount = 2 };
        m.Lines[0] = new UiToolLine { Kind = UiToolKind.Text, Label = "Pick the look of the sign.", SwatchChosen = -1 };
        m.Lines[1] = new UiToolLine { Kind = UiToolKind.Buttons, Label = "Apply", SwatchChosen = -1 };

        foreach (float[] size in new[] { new[] { 1920f, 1080f }, new[] { 1280f, 720f } })
        {
            string at = (int)size[0] + "x" + (int)size[1];
            var frame = new UiFrame { Width = size[0], Height = size[1], Band = UiFrame.BandHeight };
            float bottom = frame.BandY;
            UiWindowPlaces.Forget();
            saved = null;
            var scene = new UiScene { Draggable = true };
            var grip = new UiWindowGrip { Id = "SignAppearance", Saved = true };
            Action lay = () =>
            {
                UiToolPanel.Lay(scene, m, frame, true, UiDefaultKeys.Keyboard, "window drag", 200f, 160f);
                grip.Place(scene, frame.Width, bottom);
            };
            lay();

            Check(scene.HandleNode >= 0 && scene.Nodes[scene.HandleNode].Handle && scene.Nodes[scene.HandleNode].Disabled && scene.Nodes[scene.HandleNode].Command == UiDraw.DragCommand,
                "window drag: " + at + ": the panel's title strip is the one grip, a target no key focuses");
            Check(scene.FocusFirst != scene.HandleNode && UiFocusRules.First(scene) != scene.HandleNode, "window drag: " + at + ": the keys never start on the grip");
            var plain = new UiScene();
            UiToolPanel.Lay(plain, m, frame, true, UiDefaultKeys.Keyboard, "window drag", 200f, 160f);
            Check(plain.HandleNode < 0 && plain.NodeCount == scene.NodeCount - 1 && plain.ItemCount == scene.ItemCount,
                "window drag: " + at + ": a screen not given a window id lays out as before, with no grip");

            // The strip's place and where the panel stands now.
            ref UiItem panel = ref scene.Items[scene.HandlePanel];
            float px = panel.X, py = panel.Y, pw = panel.W, ph = panel.H;
            float gx = px + 80f, gy = py + UiFrame.Strip / 2f;
            int under = UiPress.At(scene, gx, gy);
            Check(under == scene.HandleNode, "window drag: " + at + ": the pointer on the strip names the grip");
            var boxes = new float[scene.ItemCount * 2];
            for (int i = 0; i < scene.ItemCount; i++)
            {
                boxes[i * 2] = scene.Items[i].X;
                boxes[i * 2 + 1] = scene.Items[i].Y;
            }
            int apply = scene.FindNode(UiToolPanel.LineCommand, 1);
            Check(apply >= 0, "window drag: " + at + ": the panel has its button");
            scene.NodeBox(apply, out float bx, out float by, out _, out _);

            // A drag by the grip moves the window by the pointer's move.
            var press = new UiPress();
            float dx = 70f, dy = 44f;
            press.Down(scene, under);
            Check(!grip.Drag(new UiDragPoint { Phase = UiDrag.Down, X = gx, Y = gy }) && !grip.Moving, "window drag: " + at + ": taking the strip moves nothing");
            press.Dragged();
            grip.Drag(new UiDragPoint { Phase = UiDrag.Begin, X = gx + 10f, Y = gy });
            for (int step = 1; step <= 4; step++)
            {
                if (grip.Drag(new UiDragPoint { Phase = UiDrag.Move, X = gx + dx * step / 4f, Y = gy + dy * step / 4f }))
                {
                    lay();
                }
            }
            panel = ref scene.Items[scene.HandlePanel];
            Check(Same(panel.X, px + dx) && Same(panel.Y, py + dy) && Same(grip.X, dx) && Same(grip.Y, dy),
                "window drag: " + at + ": the window moved by the pointer's move (" + dx + ", " + dy + ") and stands at " + panel.X + ", " + panel.Y);
            int moved = 0, left = 0;
            for (int i = 0; i < scene.ItemCount && i < boxes.Length / 2; i++)
            {
                if (scene.Items[i].Clip >= 0 || scene.Items[i].Layer != 0)
                {
                    continue;
                }
                bool wasIn = boxes[i * 2] >= px - 0.5f && boxes[i * 2] <= px + pw && boxes[i * 2 + 1] >= py - 0.5f && boxes[i * 2 + 1] <= py + ph;
                if (!wasIn)
                {
                    continue;
                }
                moved++;
                left += Same(scene.Items[i].X, boxes[i * 2] + dx) && Same(scene.Items[i].Y, boxes[i * 2 + 1] + dy) ? 0 : 1;
            }
            Check(moved > 6 && left == 0, "window drag: " + at + ": everything drawn in the window moved with it (" + moved + " items, " + left + " left behind)");
            scene.NodeBox(scene.FindNode(UiToolPanel.LineCommand, 1), out float bx2, out float by2, out _, out _);
            Check(Same(bx2, bx + dx) && Same(by2, by + dy), "window drag: " + at + ": its targets moved with it");
            grip.Drag(new UiDragPoint { Phase = UiDrag.End, X = gx + dx, Y = gy + dy });
            Check(press.Up(scene, gx + dx, gy + dy) == -1 && press.Why == "it became a drag", "window drag: " + at + ": a drag by the strip presses nothing when it is let go");

            // Remembered: by the window's id, in the line the game file keeps, and found again by a window of the same id in a new session.
            Check(saved != null && saved.StartsWith("SignAppearance") && !grip.Moving, "window drag: " + at + ": the place is kept by the window's id when it is let go (" + saved + ")");
            string line = saved;
            UiWindowPlaces.Forget();
            saved = line;
            var again = new UiWindowGrip { Id = "SignAppearance", Saved = true };
            UiToolPanel.Lay(scene, m, frame, true, UiDefaultKeys.Keyboard, "window drag", 200f, 160f);
            again.Place(scene, frame.Width, bottom);
            Check(Same(scene.Items[scene.HandlePanel].X, px + dx) && Same(scene.Items[scene.HandlePanel].Y, py + dy), "window drag: " + at + ": a new session finds the window where it was left");
            var other = new UiWindowGrip { Id = "CityMapFilters", Saved = true };
            UiToolPanel.Lay(scene, m, frame, true, UiDefaultKeys.Keyboard, "window drag", 200f, 160f);
            other.Place(scene, frame.Width, bottom);
            Check(Same(scene.Items[scene.HandlePanel].X, px) && Same(other.X, 0f), "window drag: " + at + ": another window keeps its own place");
            UiWindowPlaces.Set("session only", 5f, 6f, false, true);
            Check(saved == line, "window drag: " + at + ": a window the game does not remember is kept for the session and never written");

            // The screen's edge cuts a drag short: far right and down, then far left and up.
            grip = again;
            lay();
            Drag(grip, gx, gy, 9000f, 9000f, lay);
            panel = ref scene.Items[scene.HandlePanel];
            Check(Same(panel.X + panel.W, frame.Width) && Same(panel.Y + panel.H, bottom), "window drag: " + at + ": dragged past the right and the foot it stops at the edge (" + (panel.X + panel.W) + ", " + (panel.Y + panel.H) + ")");
            Drag(grip, gx, gy, -9000f, -9000f, lay);
            panel = ref scene.Items[scene.HandlePanel];
            Check(Same(panel.X, 0f) && Same(panel.Y, 0f), "window drag: " + at + ": dragged past the left and the top it stops at the edge");
            Check(AllInside(scene, frame.Width, bottom), "window drag: " + at + ": every target stays inside the screen");

            // Buttons: a short press on the button presses it; a drag that starts on it presses nothing.
            UiWindowPlaces.ResetAll();
            Check(saved == string.Empty, "window drag: " + at + ": the reset forgets every kept place and writes that");
            lay();
            Check(Same(scene.Items[scene.HandlePanel].X, px) && Same(scene.Items[scene.HandlePanel].Y, py), "window drag: " + at + ": after the reset the window stands where its screen puts it");
            scene.NodeBox(scene.FindNode(UiToolPanel.LineCommand, 1), out float cx, out float cy, out float cw, out float ch);
            press.Down(scene, UiPress.At(scene, cx + cw / 2f, cy + ch / 2f));
            int hit = press.Up(scene, cx + cw / 2f, cy + ch / 2f);
            Check(hit >= 0 && scene.Nodes[hit].Command == UiToolPanel.LineCommand && press.Why == UiPress.Pressed, "window drag: " + at + ": a short press on a button in the window still presses it");
            press.Down(scene, UiPress.At(scene, cx + cw / 2f, cy + ch / 2f));
            press.Dragged();
            Check(press.Up(scene, cx + cw / 2f + 40f, cy + ch / 2f + 40f) == -1 && press.Why == "it became a drag", "window drag: " + at + ": a drag that starts on a button presses nothing");
            Drag(grip, gx, gy, 30f, 30f, lay);
            scene.NodeBox(scene.FindNode(UiToolPanel.LineCommand, 1), out float cx2, out float cy2, out _, out _);
            press.Down(scene, UiPress.At(scene, cx2 + cw / 2f, cy2 + ch / 2f));
            hit = press.Up(scene, cx2 + cw / 2f, cy2 + ch / 2f);
            Check(hit >= 0 && scene.Nodes[hit].Command == UiToolPanel.LineCommand && Same(cx2, cx + 30f), "window drag: " + at + ": a moved window's button presses where it stands now");
        }

        string host = ModSource(@"Ui\UiHost.cs"), pointer = ModSource(@"Ui\UiView.Pointer.cs"), chrome = ModSource(@"Ui\UiDraw.Chrome.cs");
        Check(host.Contains("_scene.Nodes[node].Handle") && host.Contains("_grip.Drag(at)"),
            "window drag: only a drag that began on the strip's grip moves the window, so a drag that starts on a button moves nothing");
        Check(host.Contains("if (_scene.Nodes[node].Handle)") && host.Contains("a click on a window's title strip does nothing"), "window drag: a click on the strip presses nothing");
        Check(pointer.Contains("_pressed.Dragged()") && chrome.Contains("s.Draggable && s.HandleNode < 0"), "window drag: a press that became a drag presses nothing, and only a draggable screen gets a grip");
        Check(host.Contains("UiWindowPlaces.Version != _placesSeen") && !host.Contains("UiWindowPlaces.Get(") && host.Contains("if (_grip.Moving)"),
            "window drag: a frame costs one integer compare and, while a window is held, one call; places are read on a layout");

        // The game's own windows and the ones the kit has made draggable.
        string game = ModSource(@"Ui\Host\UiGameHost.cs"), map = ModSource(@"Ui\MapDock\MapFiltersGame.cs"), sign = ModSource(@"Ui\World\SignAppearanceKit.cs");
        Check(map.Contains("\"CityMapFilters\"") && sign.Contains("\"SignAppearance\"") && game.Contains("IUiGameDraggable") && game.Contains("isCurrentlyDragging = true"),
            "window drag: the city map's filters and a sign's look take the game's own window ids, and the game's map camera holds still while one is held");
        UiWindowDragMapChecks();
        UiWindowPlaces.Forget();
        UiWindowPlaces.Load = null;
        UiWindowPlaces.Save = null;
    }

    // The city map's dock dragged (the owner's test of 5 Nov: the frame moved and its lists, the rail and the rows stayed behind over the map): the real dock with its rail, its
    // scrolling rows, its picked place and its buttons, laid in the one scene that also holds the route strip, the layers and the map's buttons. After a drag every node and every
    // scrolling region of the dock is offset by exactly the drag, what the dock draws moved with it, and the strip, the layers and the buttons stood where they were.
    private static void UiWindowDragMapChecks()
    {
        UiWindowPlaces.Forget();
        UiWords t = UiWords("en");
        foreach (float[] size in new[] { new[] { 1920f, 1080f }, new[] { 1280f, 720f } })
        {
            string at = (int)size[0] + "x" + (int)size[1];
            var frame = new UiFrame { Width = size[0], Height = size[1], Band = UiFrame.BandHeight };
            foreach (string page in new[] { "filters", "place", "district" })
            {
                var scene = new UiScene { Draggable = true };
                var grip = new UiWindowGrip { Id = "CityMapFilters", Saved = false };
                Action lay = () =>
                {
                    var model = new MapScreenModel { Route = UiSamples.MapRoute(t), Buttons = UiSamples.MapButtons(t, true), HasLabel = true, Label = UiSamples.MapNextLabel(t) };
                    if (page == "filters")
                    {
                        model.Filters = UiSamples.MapFilters(t, "filters");
                    }
                    else if (page == "place")
                    {
                        model.Place = UiSamples.MapPlace(t, "overview");
                        model.Layers = UiSamples.MapLayers(t);
                    }
                    else
                    {
                        model.District = UiSamples.MapDistrict(t, "rent");
                        model.Layers = UiSamples.MapLayers(t);
                    }
                    UiMap.Lay(scene, model, frame, true, UiDefaultKeys.Keyboard);
                    grip.Place(scene, frame.Width, frame.BandY);
                };
                UiWindowPlaces.Forget();
                lay();
                var nodes = new float[scene.NodeCount * 2];
                var items = new float[scene.ItemCount * 2];
                var clips = new float[scene.ClipCount * 2];
                for (int i = 0; i < scene.NodeCount; i++)
                {
                    nodes[i * 2] = scene.Nodes[i].X;
                    nodes[i * 2 + 1] = scene.Nodes[i].Y;
                }
                for (int i = 0; i < scene.ItemCount; i++)
                {
                    items[i * 2] = scene.Items[i].X;
                    items[i * 2 + 1] = scene.Items[i].Y;
                }
                for (int i = 0; i < scene.ClipCount; i++)
                {
                    clips[i * 2] = scene.Clips[i].X;
                    clips[i * 2 + 1] = scene.Clips[i].Y;
                }
                int nodeCount = scene.NodeCount, itemCount = scene.ItemCount, clipCount = scene.ClipCount;
                bool hadList = page != "place" || clipCount > 0;
                float gx = scene.Nodes[scene.HandleNode].X + 60f, gy = scene.Nodes[scene.HandleNode].Y + UiFrame.Strip / 2f;
                float dx = 120f, dy = 8f;   // the dock stands nearly the screen's height, so it has only a few px to move down
                Drag(grip, gx, gy, dx, dy, lay);
                bool sameShape = scene.NodeCount == nodeCount && scene.ItemCount == itemCount && scene.ClipCount == clipCount;
                int dockNodes = 0, dockMoved = 0, stayNodes = 0, stayMoved = 0, dockItems = 0, itemsMoved = 0;
                for (int i = 0; i < nodeCount && sameShape; i++)
                {
                    int command = scene.Nodes[i].Command;
                    bool stays = command >= UiMapChrome.OptionCommand && command <= UiMapChrome.HideAllCommand;
                    bool moved = Same(scene.Nodes[i].X, nodes[i * 2] + dx) && Same(scene.Nodes[i].Y, nodes[i * 2 + 1] + dy) || (scene.Nodes[i].Clip >= 0 && Same(scene.Nodes[i].X, nodes[i * 2]) && Same(scene.Nodes[i].Y, nodes[i * 2 + 1]));
                    bool still = Same(scene.Nodes[i].X, nodes[i * 2]) && Same(scene.Nodes[i].Y, nodes[i * 2 + 1]);
                    if (stays)
                    {
                        stayNodes++;
                        stayMoved += still ? 0 : 1;
                    }
                    else
                    {
                        dockNodes++;
                        dockMoved += moved ? 0 : 1;
                    }
                }
                int clipsMoved = 0, clipsLeft = 0;
                for (int i = 0; i < clipCount && sameShape; i++)
                {
                    bool moved = Same(scene.Clips[i].X, clips[i * 2] + dx) && Same(scene.Clips[i].Y, clips[i * 2 + 1] + dy);
                    clipsMoved += moved ? 1 : 0;
                    clipsLeft += moved ? 0 : 1;
                }
                for (int i = 0; i < itemCount && sameShape; i++)
                {
                    if (scene.Items[i].Clip >= 0 || (scene.Items[i].Flags & UiFlags.Prompt) != 0 || !scene.Items[i].Window)
                    {
                        continue;
                    }
                    dockItems++;
                    itemsMoved += Same(scene.Items[i].X, items[i * 2] + dx) && Same(scene.Items[i].Y, items[i * 2 + 1] + dy) ? 0 : 1;
                }
                Check(sameShape && dockNodes > 5 && dockMoved == 0 && clipsLeft == 0 && (!hadList || clipsMoved > 0),
                    "window drag: the city map's " + page + " dock at " + at + ": every target (" + dockNodes + ") and every scrolling region (" + clipsMoved + ") of the dock is offset by exactly the drag (" + dx + ", " + dy + "), "
                    + dockMoved + " targets and " + clipsLeft + " regions left behind");
                Check(dockItems > 20 && itemsMoved == 0 && stayNodes > 0 && stayMoved == 0,
                    "window drag: the city map's " + page + " dock at " + at + ": everything the dock draws (" + dockItems + " items, its rail, rows and picked place with them) moved with it, and the route strip, the map's buttons and the layers (" + stayNodes + " targets) stood where they were");
                // The rows of a scrolling list are drawn in their region's own coordinates: the region moved, so where each row lands on the screen moved by the drag.
                if (page == "filters")
                {
                    int rows = 0, rowsOff = 0;
                    for (int i = 0; i < scene.NodeCount; i++)
                    {
                        if (scene.Nodes[i].Clip < 0 || scene.Nodes[i].Layer != 0)
                        {
                            continue;
                        }
                        scene.NodeBox(i, out float rx, out float ry, out _, out _);
                        UiClip c = scene.Clips[scene.Nodes[i].Clip];
                        rows++;
                        rowsOff += rx >= c.X - 0.01f && rx <= c.X + c.W + 0.01f ? 0 : 1;
                    }
                    Check(rows > 5 && rowsOff == 0, "window drag: the dock's rows stand inside their moved region, not over the map where the dock was (" + rows + " rows, " + rowsOff + " outside)");
                }
                UiWindowPlaces.Moved("CityMapFilters", out float shownX, out float shownY);
                Check(Same(shownX, dx) && Same(shownY, dy) && Same(grip.X, dx) && Same(grip.Y, dy),
                    "window drag: the move the dock was laid with is noted for the HUD's avoid box (" + shownX + ", " + shownY + ")");
            }
        }
        UiWindowPlaces.Forget();
    }

    private static bool Same(float a, float b) => Math.Abs(a - b) < 0.01f;

    // A whole drag by the grip: down on the strip, past the threshold, to a place (the pointer's move in px), let go. The screen is laid out again at each move.
    private static void Drag(UiWindowGrip grip, float x, float y, float dx, float dy, Action lay)
    {
        grip.Drag(new UiDragPoint { Phase = UiDrag.Down, X = x, Y = y });
        grip.Drag(new UiDragPoint { Phase = UiDrag.Begin, X = x + 10f, Y = y });
        if (grip.Drag(new UiDragPoint { Phase = UiDrag.Move, X = x + dx, Y = y + dy }))
        {
            lay();
        }
        grip.Drag(new UiDragPoint { Phase = UiDrag.End, X = x + dx, Y = y + dy });
    }

    private static bool AllInside(UiScene scene, float width, float bottom)
    {
        for (int i = 0; i < scene.NodeCount; i++)
        {
            scene.NodeBox(i, out float x, out float y, out float w, out float h);
            if (x < -0.01f || y < -0.01f || x + w > width + 0.01f || y + h > bottom + 0.01f)
            {
                return false;
            }
        }
        return true;
    }
}
