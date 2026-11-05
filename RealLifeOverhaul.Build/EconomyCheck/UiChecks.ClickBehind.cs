using System;
using RealLifeOverhaul.Ui;

// A click one behind (the owner, 5 Nov: click an item, then another, and the first is what acts; a third moves it to the second).
// The press must begin on the target the scene has under the pointer, never on the object the engine's last raycast named: the two
// differ when the raycast is of an earlier frame. Run: --click-behind; the lines read "click behind: ...".
internal static partial class Program
{
    private static void UiClickBehindChecks()
    {
        var press = new UiPress();
        var scene = new UiScene();
        scene.Begin("behind", 400f, 300f, true);
        for (int i = 0; i < 3; i++)
        {
            var n = new UiNode
            {
                X = 0f, Y = i * 30f, W = 100f, H = 30f, Clip = -1, Layer = 0, Command = ClickRows, Arg = i, Face = -1, List = -1,
                Key = UiAction.Count, Up = -1, Down = -1, Left = -1, Right = -1
            };
            scene.AddNode(ref n);
        }
        // A panel on a higher layer over the middle of the first two rows.
        var top = new UiNode { X = 0f, Y = 0f, W = 100f, H = 40f, Clip = -1, Layer = 1, Command = 5, Arg = 0, Face = -1, List = -1, Key = UiAction.Count, Up = -1, Down = -1, Left = -1, Right = -1 };
        scene.AddNode(ref top);

        int atB = UiPress.At(scene, 50f, 45f);
        Check(atB >= 0 && scene.Nodes[atB].Command == ClickRows && scene.Nodes[atB].Arg == 1, "click behind: the scene names the row under the pointer (B at y 45)");
        Check(UiPress.At(scene, 50f, 15f) == 3, "click behind: a target on a higher layer is the one under the pointer where it overlaps");
        Check(UiPress.At(scene, 250f, 15f) == -1 && UiPress.At(scene, 50f, 200f) == -1, "click behind: a place with no target names none");

        // The raycast named the row clicked before (A), the pointer is on B: the press begins on B and presses B.
        int ray = scene.FindNode(ClickRows, 0);
        int here = UiPress.At(scene, 50f, 45f);
        press.Down(scene, here >= 0 ? here : ray);
        int node = press.Up(scene, 50f, 45f);
        Check(node >= 0 && scene.Nodes[node].Arg == 1 && press.Why == UiPress.Pressed, "click behind: a raycast of the row before does not decide the press, the pointer's place does");
        press.Down(scene, here);
        Check(press.Up(scene, 250f, 45f) == -1 && press.Why == "the pointer left the target", "click behind: an up off the target says why it pressed nothing");

        string pointer = ModSource(@"Ui\UiView.Pointer.cs"), host = ModSource(@"Ui\UiHost.cs");
        Check(pointer.Contains("UiPress.At(_scene") && pointer.Contains("_pressed.Down(_scene, _downNode)") && !pointer.Contains("_pressed.Down(_scene, node)"),
            "click behind: the view begins a press on the target at the pointer's place, not on the raycast's object");
        Check(host.Contains("UI press: ") && host.Contains("_view.PointerReport = PressTrace") && host.Contains("_confirmNote"),
            "click behind: every mouse press writes its one trace line (down, up, Confirm, focus, layouts)");
    }
}
