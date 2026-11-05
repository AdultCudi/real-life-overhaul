using System;
using System.Text;
using RealLifeOverhaul.Ui;

// A click that draws one behind (the owner, 5 Nov: click A and nothing shows, click B and A shows, click C and B shows). The host's
// order for a click is run here on real screens, game-free, as UiHost.PointerPressed runs it: focus the target (the screen hears
// Focused, a layout if it asks), press it, lay out again if the press made the screen dirty. What is drawn after each press must be
// the target pressed. Run: --click-draw; the lines read "click draw: ...".
internal static partial class Program
{
    private static UiFrame ClickDrawFrame() => UiFrame.FullMenu(1.78f, "Work");

    // The chips (filter buttons) of a work screen's page, as scene nodes.
    private static int[] ClickDrawChips(WorkScreen screen, UiScene scene)
    {
        var nodes = new System.Collections.Generic.List<int>();
        WorkPage page = screen.Page;
        for (int a = 0; a < page.Model.Actions.Length; a++)
        {
            int node = scene.FindNode(UiAppWindow.ActionCommand, a);
            if (page.Model.Actions[a].Kind == UiButtonKind.Chip && node >= 0)
            {
                nodes.Add(node);
            }
        }
        return nodes.ToArray();
    }

    // One click as the host does it; returns the scene as drawn after it.
    private static void ClickDrawOnce(WorkScreen screen, UiScene scene, int command, int arg)
    {
        screen.Focused(command, arg);
        if (screen.Dirty)
        {
            screen.Lay(scene, ClickDrawFrame(), true, UiDefaultKeys.Keyboard);
        }
        screen.Press(command, arg);
        if (screen.Dirty)
        {
            screen.Lay(scene, ClickDrawFrame(), true, UiDefaultKeys.Keyboard);
        }
    }

    private static string ClickDrawFace(UiScene scene, int command, int arg)
    {
        int node = scene.FindNode(command, arg);
        if (node < 0 || scene.Nodes[node].Face < 0)
        {
            return "none";
        }
        ref UiItem it = ref scene.Items[scene.Nodes[node].Face];
        return it.Fill + "/" + it.Flags;
    }

    private static void UiClickDrawChecks()
    {
        WorkScreen screen = WorkScreenOf("campus", WorkEnglish, "courses");
        var scene = new UiScene();
        screen.Lay(scene, ClickDrawFrame(), true, UiDefaultKeys.Keyboard);
        int[] chips = ClickDrawChips(screen, scene);
        bool every = chips.Length >= 4;
        for (int k = 1; k <= 3 && k < chips.Length; k++)
        {
            int arg = scene.Nodes[chips[k]].Arg;
            ClickDrawOnce(screen, scene, UiAppWindow.ActionCommand, arg);
            for (int i = 0; i < chips.Length; i++)
            {
                int at = scene.Nodes[scene.FindNode(UiAppWindow.ActionCommand, i)].Arg;
                every &= ClickDrawFace(scene, UiAppWindow.ActionCommand, at).EndsWith("Selected") == (at == arg);
            }
        }
        Check(every, "click draw: three chips of Campus clicked in a row, the chip drawn chosen after each click is the one clicked");

        // The city map's rail: a press on an entry scrolls the list to its group, and the rail names that group in the same layout
        // (it read the offset the list had before the press, so it named the group of the click before).
        Check(UiMapFilters.RailAfterReveal(1, 2, 2) == 2 && UiMapFilters.RailAfterReveal(1, -1, -1) == 1 && UiMapFilters.RailAfterReveal(1, 3, -1) == 1,
            "click draw: the map rail names the group a press scrolls to, and the offset's group when nothing is asked");
        string lines = ModSource(@"Ui\MapDock\MapLive.Lines.cs"), host = ModSource(@"Ui\UiHost.cs");
        Check(lines.Contains("UiMapFilters.RailAfterReveal(m.RailShown, m.Reveal"), "click draw: the map's layout gives the rail the group it reveals, not the group of the offset before");
        Check(host.Contains("drawn after: ") && host.Contains("screen dirty after: "), "click draw: the press trace says what the layout on show draws for the pressed target");
    }
}
