using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.Ui;

// The pad's walk of every Work page (WorkWalk): from where the keys land, every target of the page in reading order through the real focus
// links alone. A page fails when a target is left out, a hop takes more than a few presses, the focus starts on a stepper, a destructive button or
// a close, or the walk ends on a destructive button. The previews draw their pad path shots from the same walk, so what the picture shows is
// what these checks held to.
internal static partial class Program
{
    private static bool _workWalkChecked;

    private static UiScene WorkSceneOf(WorkScreen screen)
    {
        var scene = new UiScene();
        screen.Lay(scene, UiFrame.FullMenu(1.78f, "Work"), true, UiDefaultKeys.XboxPad);
        return scene;
    }

    // Why the pad's first target is wrong for the page, or null. The page's own choice (the chosen row, card or day) is a safe start; a page with
    // none (a decision, a form) starts on its main action, so a reflexive press does what the page is for and never steps an amount or leaves.
    private static string WorkStartFault(WorkScreen screen, UiScene s, int start)
    {
        if (start < 0)
        {
            return "no first target";
        }
        UiNode n = s.Nodes[start];
        int topLayer = UiFocusRules.TopLayer(s), contents = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            contents += WorkWalk.IsContent(s, i, topLayer) ? 1 : 0;
        }
        if (contents == 0)
        {
            return n.Command == UiAppWindow.NavCommand ? null : "a page with no target of its own starts on command " + n.Command + ", not its section";   // the focus stays on the section's entry
        }
        if (n.Command == WorkCmd.Less || n.Command == WorkCmd.More || n.Command == WorkCmd.Type || n.Command == WorkCmd.NudgeLess || n.Command == WorkCmd.NudgeMore
            || n.Command == UiDraw.CloseCommand || n.Command == UiAppWindow.NavCommand)
        {
            return "starts on a stepper or the close (command " + n.Command + ")";
        }
        WorkPage page = screen.Page;
        if (WorkDanger(page, n))
        {
            return "starts on a destructive button";
        }
        bool chooses = page.Table != null || page.Cards != null || page.Diary != null || page.Reader != null || page.Asides.Length > 0;
        if (!chooses)
        {
            int main = screen.MainIndex();
            if (main >= 0)
            {
                UiActionModel act = page.Model.Actions[main];
                int at = s.FindNode(act.Command, act.Arg);
                if (at >= 0 && at != start)
                {
                    return "starts on command " + n.Command + ", not on the main action '" + act.Label + "'";
                }
            }
        }
        return null;
    }

    private static bool WorkDanger(WorkPage page, UiNode n)
    {
        if (n.Command != UiAppWindow.ActionCommand || n.Arg < 0 || n.Arg >= page.Model.Actions.Length)
        {
            return false;
        }
        UiButtonKind k = page.Model.Actions[n.Arg].Kind;
        return k == UiButtonKind.Destructive || k == UiButtonKind.Armed;
    }

    // Walks one page and says what is wrong with it, or null. Report fills a line of what the walk was.
    private static string WorkWalkOf(WorkScreen screen, out string report)
    {
        UiScene s = WorkSceneOf(screen);
        int start = UiFocusRules.First(s);
        string fault = WorkStartFault(screen, s, start);
        report = "no walk";
        if (start < 0)
        {
            return fault;
        }
        WorkWalk.Walked w = screen.WalkOf(s);
        int top = UiFocusRules.TopLayer(s), content = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            content += WorkWalk.IsContent(s, i, top) ? 1 : 0;
        }
        bool startIsContent = WorkWalk.IsContent(s, start, top);
        int left = content - (w.Stops.Count - (startIsContent ? 0 : 1));
        int last = w.Stops[w.Stops.Count - 1];
        var sb = new StringBuilder();
        sb.Append(w.Stops.Count).Append(" stops of ").Append(content).Append(", ").Append(w.Presses).Append(" presses, longest ").Append(w.Longest).Append(", across ").Append(w.Across);
        for (int i = 0; i < w.Stops.Count; i++)
        {
            UiNode n = s.Nodes[w.Stops[i]];
            s.NodeBox(w.Stops[i], out float nx, out float ny, out float nw, out float nh);
            sb.Append(i == 0 ? "\n    " : " > ").Append(n.Command).Append('.').Append(n.Arg).Append('@').Append((int)nx).Append(',').Append((int)ny).Append('+').Append((int)nw).Append('x').Append((int)nh).Append(n.Command == UiAppWindow.ActionCommand && n.Arg >= 0 && n.Arg < screen.Page.Model.Actions.Length ? "[" + screen.Page.Model.Actions[n.Arg].Kind + " " + screen.Page.Model.Actions[n.Arg].Label + "]" : string.Empty).Append(i > 0 && w.Hops[i] > 1 ? "(" + w.Hops[i] + ")" : string.Empty);
        }
        for (int i = 0; i < w.Missed.Count; i++)
        {
            UiNode n = s.Nodes[w.Missed[i]];
            s.NodeBox(w.Missed[i], out float bx, out float by, out float _, out float _);
            sb.Append(" MISSED ").Append(n.Command).Append('.').Append(n.Arg).Append('@').Append((int)bx).Append(',').Append((int)by);
        }
        report = sb.ToString();
        if (left > 0 || w.Missed.Count > 0)
        {
            fault = (fault != null ? fault + "; " : string.Empty) + (left + w.Missed.Count) + " targets not reached in reading order";
        }
        if (w.Longest > WorkWalk.HopMost || w.Across > WorkWalk.HopAcross)
        {
            fault = (fault != null ? fault + "; " : string.Empty) + "a hop of " + Math.Max(w.Longest, w.Across) + " presses";
        }
        if (WorkDanger(screen.Page, s.Nodes[last]))
        {
            fault = (fault != null ? fault + "; " : string.Empty) + "ends on a destructive button";
        }
        bool quieter = false;
        for (int i = 1; i < w.Stops.Count - 1; i++)
        {
            quieter |= !screen.Commits(s.Nodes[w.Stops[i]]);
        }
        if (w.Stops.Count > 1 && screen.Commits(s.Nodes[last]) && quieter)
        {
            fault = (fault != null ? fault + "; " : string.Empty) + "ends on a button that does something while a quieter target was left to end on";
        }
        return fault;
    }

    private static void WorkWalkChecks()
    {
        if (_workWalkChecked)
        {
            return;
        }
        _workWalkChecked = true;
        bool report = Environment.GetEnvironmentVariable("RLO_WALK_REPORT") == "1";
        var faults = new StringBuilder();
        int pages = 0, stops = 0;
        foreach (string id in new[] { "career", "campus", "open_roles", "marketplace" })
        {
            foreach (string st in WorkStatesOf(id))
            {
                WorkScreen screen = WorkScreenOf(id, WorkEnglish, st);
                string line;
                string fault = WorkWalkOf(screen, out line);
                pages++;
                stops += 1;
                if (report)
                {
                    Console.WriteLine("  " + id + " " + st + ": " + (fault ?? "ok") + " | " + line);
                }
                if (fault != null)
                {
                    faults.Append(" [" + id + " " + st + ": " + fault + "]");
                }
            }
        }
        // A button runs the action it is numbered for: the words drawn on it are the words of that action (a profile numbers its buttons by their place in its row, and two made
        // in the other order would swap what they do).
        var swapped = new StringBuilder();
        foreach (string id in new[] { "career", "campus", "open_roles", "marketplace" })
        {
            foreach (string st in WorkStatesOf(id))
            {
                WorkScreen screen = WorkScreenOf(id, WorkEnglish, st);
                UiScene scene = WorkSceneOf(screen);
                for (int n = 0; n < scene.NodeCount; n++)
                {
                    UiNode node = scene.Nodes[n];
                    if (node.Command != UiAppWindow.ActionCommand || node.Arg < 0 || node.Arg >= screen.Page.Model.Actions.Length)
                    {
                        continue;
                    }
                    string label = screen.Page.Model.Actions[node.Arg].Label, drawn = null;
                    bool any = false;
                    for (int i = 0; i < scene.ItemCount; i++)
                    {
                        if (scene.Items[i].Node == n && scene.Items[i].Kind == UiKind.Text && !string.IsNullOrEmpty(scene.Items[i].Text))
                        {
                            any = true;
                            drawn = scene.Items[i].Text;
                            if (string.Equals(drawn, label, StringComparison.OrdinalIgnoreCase) || label.StartsWith(drawn.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
                            {
                                any = false;
                                break;
                            }
                        }
                    }
                    if (any)
                    {
                        swapped.Append(" [" + id + " " + st + ": '" + drawn + "' runs '" + label + "']");
                    }
                }
            }
        }
        Check(swapped.Length == 0, "work screens: every button runs the action whose words it shows" + swapped);
        Check(faults.Length == 0 && pages > 30, "work screens: the pad walks every page in reading order through the real focus links, from a safe start, reaching every target and never ending on a destructive button (" + pages + " pages)" + faults);
        // The rule itself: a page whose links cannot walk it is found (a target stands alone, out of every link's reach), and one that starts on a stepper is.
        var lone = new UiScene();
        lone.Begin("walk", 1920f, 1080f, true);
        int a = UiDraw.Node(lone, UiAt.Screen, 100f, 100f, 200f, 48f, WorkCmd.Card, 0, -1);
        int b = UiDraw.Node(lone, UiAt.Screen, 100f, 160f, 200f, 48f, WorkCmd.Card, 1, -1);
        int c = UiDraw.Node(lone, UiAt.Screen, 100f, 220f, 200f, 48f, WorkCmd.Card, 2, -1);
        lone.Nodes[b].Up = a;
        lone.Nodes[b].Down = a;
        lone.Nodes[a].Down = b;
        lone.Nodes[a].Left = a;
        lone.Nodes[a].Right = a;
        lone.Nodes[a].Up = a;
        lone.Nodes[b].Left = b;
        lone.Nodes[b].Right = b;
        lone.Nodes[c].Up = c;
        lone.Nodes[c].Down = c;
        lone.Nodes[c].Left = c;
        lone.Nodes[c].Right = c;
        WorkWalk.Walked lost = WorkWalk.Walk(lone, a);
        Check(lost.Missed.Count >= 1 && lost.Stops.Count == 2, "work screens: a walk with a target the links never reach is found (" + lost.Missed.Count + " missed)");
    }
}
