using System;
using RealLifeOverhaul.Ui;

// The city map stopped answering WASD and panning once a kit window was dragged on it (the owner, 5 Nov). The game's map camera holds
// still while DraggableWindows.isCurrentlyDragging is set, and the kit sets it every frame its grip is held. The engine tells a pointer
// up before the end of a drag, so a press spent by the up named no target at the end and the grip was never let go. Run: --map-drag;
// the lines read "map drag: ...".
internal static partial class Program
{
    private static void UiMapDragChecks()
    {
        UiWindowPlaces.Forget();
        UiWindowPlaces.Load = () => null;
        UiWindowPlaces.Save = line => { };

        var m = new UiToolPanelModel { Title = "Map filters", Icon = UiIcon.PaintBrush, Close = true, Lines = new UiToolLine[1], LineCount = 1 };
        m.Lines[0] = new UiToolLine { Kind = UiToolKind.Text, Label = "Pick what the map shows.", SwatchChosen = -1 };
        var frame = new UiFrame { Width = 1920f, Height = 1080f, Band = UiFrame.BandHeight };
        var scene = new UiScene { Draggable = true };
        var grip = new UiWindowGrip { Id = "CityMapFilters", Saved = true };
        UiToolPanel.Lay(scene, m, frame, true, UiDefaultKeys.Keyboard, "map drag", 200f, 160f);
        grip.Place(scene, frame.Width, frame.BandY);

        ref UiItem panel = ref scene.Items[scene.HandlePanel];
        float gx = panel.X + 80f, gy = panel.Y + UiFrame.Strip / 2f;
        var press = new UiPress();
        press.Down(scene, UiPress.At(scene, gx, gy));
        grip.Drag(new UiDragPoint { Phase = UiDrag.Down, X = gx, Y = gy });
        press.Dragged();
        grip.Drag(new UiDragPoint { Phase = UiDrag.Begin, X = gx + 10f, Y = gy });
        grip.Drag(new UiDragPoint { Phase = UiDrag.Move, X = gx + 60f, Y = gy + 30f });
        Check(grip.Moving, "map drag: the strip is held while the pointer drags it");

        // The engine's release: the pointer up first, then the end of the drag, which the view gives to the target the press began on.
        press.Up(scene, gx + 60f, gy + 30f);
        int target = press.Current(scene);
        Check(target >= 0 && scene.Nodes[target].Handle, "map drag: after the pointer came up the end of the drag still names the strip it began on");
        if (target >= 0)
        {
            grip.Drag(new UiDragPoint { Phase = UiDrag.End, X = gx + 60f, Y = gy + 30f });
        }
        Check(!grip.Moving, "map drag: the strip is let go when the drag ends, so the map camera is no longer held");

        // A new press is its own: a click that is not on a target names none at its end.
        press.Down(scene, -1);
        Check(press.Current(scene) < 0, "map drag: a press on no target names no target");

        string pointer = ModSource(@"Ui\UiView.Pointer.cs");
        string host = ModSource(@"Ui\UiHost.cs");
        Check(pointer.Contains("_pressed.Current(_scene)") && host.Contains("if (_grip.Moving)") && host.Contains("_grip.End();"),
            "map drag: the end of a drag reads the press's target, the hold on the game's windows runs only while the grip is held, and hiding lets go");
    }
}
