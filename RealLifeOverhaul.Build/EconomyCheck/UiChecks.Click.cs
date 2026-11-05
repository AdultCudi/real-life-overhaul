using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// A single click on a kit screen (the owner, 5 Nov: "Single click almost does not work in any app", and a click that picks the item
// clicked before). The press is kept by what the target is, not by the pooled object that showed it or its number: down on A, a
// layout that renumbers the targets or moves A in between, then up over A must press A once and nothing else; up over another target
// presses nothing. Run: dotnet run -c Release from RealLifeOverhaul.Build/EconomyCheck; the lines read "ui click: ...".
internal static partial class Program
{
    private const int ClickRows = 7;   // the rows' command

    private static void UiClickChecks()
    {
        var press = new UiPress();
        var scene = new UiScene();
        var presses = new List<long>();   // command * 100 + argument of every press delivered

        // Down on A, then a layout puts a new target before the rows: every number moves and A stands a row lower.
        UiClickLay(scene, 0);
        int a = scene.FindNode(ClickRows, 0);
        press.Down(scene, a);
        UiClickLay(scene, 1);
        int node = press.Up(scene, 50f, 45f);   // over A where it stands now
        if (node >= 0)
        {
            presses.Add(scene.Nodes[node].Command * 100L + scene.Nodes[node].Arg);
        }
        Check(a == 0 && scene.FindNode(ClickRows, 0) != a && presses.Count == 1 && presses[0] == ClickRows * 100L,
            "ui click: down on A, a layout renumbers the targets, up over A presses exactly A and no other target");
        Check(press.Up(scene, 50f, 45f) == -1 && !press.Armed, "ui click: a press is spent once, a second up presses nothing");

        UiClickLay(scene, 0);
        press.Down(scene, scene.FindNode(ClickRows, 0));
        UiClickLay(scene, 1);
        Check(press.Up(scene, 50f, 15f) == -1, "ui click: down on A, a layout moves A, up over the target now where A was presses nothing");

        // B and A swap places: the object that showed B shows A now.
        UiClickLay(scene, 0);
        press.Down(scene, scene.FindNode(ClickRows, 1));
        UiClickLay(scene, 2);
        node = press.Up(scene, 50f, 15f);   // B stands first now
        Check(node >= 0 && scene.Nodes[node].Arg == 1, "ui click: down on B, a layout swaps B and A, up over B presses B and not A");
        UiClickLay(scene, 0);
        press.Down(scene, scene.FindNode(ClickRows, 1));
        UiClickLay(scene, 2);
        Check(press.Up(scene, 50f, 45f) == -1, "ui click: down on B, the swap, up over A presses nothing");

        // Up elsewhere, a drag, the target gone, an up with no down, and a target cut off by its scroll region.
        UiClickLay(scene, 0);
        press.Down(scene, scene.FindNode(ClickRows, 0));
        Check(press.Up(scene, 50f, 75f) == -1, "ui click: down on A, up over C presses nothing");
        press.Down(scene, scene.FindNode(ClickRows, 0));
        press.Dragged();
        Check(press.Up(scene, 50f, 15f) == -1, "ui click: a press that became a drag on a screen that takes drags is no click");
        press.Down(scene, scene.FindNode(ClickRows, 0));
        scene.Begin("click", 400f, 300f, true);
        Check(press.Up(scene, 50f, 15f) == -1, "ui click: a target gone from the layout in between is not replaced by another");
        UiClickLay(scene, 0);
        Check(press.Up(scene, 50f, 15f) == -1, "ui click: an up that never had a down presses nothing");
        UiClickLay(scene, 3);
        press.Down(scene, scene.FindNode(ClickRows, 2));
        Check(press.Up(scene, 50f, 75f) == -1, "ui click: the pointer below the scroll region's edge presses nothing, though the cut-off row's box is there");

        // The engine's side: the click is not left to a pooled object, and a click's own focus does not renumber what it presses.
        string hit = ModSource(@"Ui\UiHit.cs"), pointer = ModSource(@"Ui\UiPointer.cs"), view = ModSource(@"Ui\UiView.cs"), taken = ModSource(@"Ui\UiView.Pointer.cs"), host = ModSource(@"Ui\UiHost.cs");
        Check(!hit.Contains("IPointerClickHandler") && !hit.Contains("OnPointerClick") && !hit.Contains("PointerPressed"),
            "ui click: a face does not carry the click (the engine clicks only the object the pointer went down on, and a layout hands that object to another target)");
        Check(pointer.Contains("IPointerDownHandler") && pointer.Contains("IPointerUpHandler") && view.Contains("AddComponent<UiPointer>()")
              && taken.Contains("_pressed.Up(") && taken.Contains("_pressed.Down("),
            "ui click: the view's root takes the pointer down and up, and the press is told by what the target is");
        Check(host.Contains("UiPress.Find(") && host.Contains("_laid++"),
            "ui click: a layout made by a click's own focus does not renumber the target the click then presses");
    }

    // Layout 0: three rows A, B, C of command ClickRows, 100 x 30, stacked. 1: a new target (command 9) stands before them.
    // 2: A and B swap. 3: the rows in a scroll region 60 high, so C is cut off.
    private static void UiClickLay(UiScene s, int layout)
    {
        s.Begin("click", 400f, 300f, true);
        short clip = layout == 3 ? s.AddClip(0f, 0f, 100f, 60f, 0f) : (short)-1;
        int rows = layout == 1 ? 4 : 3;
        for (int i = 0; i < rows; i++)
        {
            bool extra = layout == 1 && i == 0;
            int arg = layout == 1 ? i - 1 : layout == 2 && i < 2 ? 1 - i : i;
            var n = new UiNode
            {
                X = 0f, Y = i * 30f, W = 100f, H = 30f, Clip = clip, Layer = 0, Command = extra ? 9 : ClickRows, Arg = extra ? 0 : arg, Face = -1, List = -1,
                Key = UiAction.Count, Up = -1, Down = -1, Left = -1, Right = -1
            };
            s.AddNode(ref n);
        }
    }
}
