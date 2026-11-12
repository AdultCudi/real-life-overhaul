using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// Round 5 of the Flare audit fix: the order the d-pad walks a Flare screen in. From the first target, presses of right alone go on through the screen left to right and top
// to bottom, group after group, and the commit button (Post, Run, Hire) comes after the controls that feed it. This is the focus graph the game uses (UiFocusRules.Next over
// the links FlareFace.ReadingLinks sets), the same the preview's pad path draws.
internal static partial class Program
{
    private static void FlareOrderChecks()
    {
        string bad = "none", sheets = "none", hire = "none", rail = "none", pages = "none";
        int walked = 0, pagesWalked = 0, pageStates = 0;
        var walk = new int[256];
        var scene = new UiScene();
        UiWords en = UiWords("en");
        foreach (string state in UiSamples.FlareStates)
        {
            UiSamples.Flare(scene, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, state);
            int start = scene.FocusFirst >= 0 ? scene.FocusFirst : UiFocusRules.First(scene);
            if (start < 0)
            {
                continue;
            }
            byte layer = scene.Nodes[start].Layer;
            if (layer == 0)
            {
                pageStates++;
                pagesWalked += FlareOrderPage(scene, start, walk, state, ref pages);
            }
            var chain = new List<int> { start };
            for (int at = start; ; )
            {
                int next = UiFocusRules.Next(scene, at, 1, 0);
                if (next < 0 || next >= scene.NodeCount || chain.Contains(next) || scene.Nodes[next].Layer != layer || scene.Nodes[next].Region != scene.Nodes[start].Region)
                {
                    break;
                }
                chain.Add(next);
                at = next;
            }
            walked += chain.Count;
            // never back up a row: each step is on the same row or a later one
            for (int i = 1; i < chain.Count && layer == 1 && state != "photo_picker"; i++)
            {
                if (layer == 1 && i == chain.Count - 1)
                {
                    continue;   // a sheet's close, read last, stands at its top
                }
                scene.NodeBox(chain[i - 1], out _, out float top0, out _, out float high0);
                scene.NodeBox(chain[i], out _, out float top1, out _, out float high1);
                float before = top0 + high0 / 2f, now = top1 + high1 / 2f;
                if (now < before - 14f)
                {
                    bad = state + " goes back up at step " + i;
                }
            }
            if (layer == 1 && state != "photo_picker")
            {
                // every target once, with Cancel, then the commit button, then the close last
                int want = 0;
                for (int n = 0; n < scene.NodeCount; n++)
                {
                    ref UiNode node = ref scene.Nodes[n];
                    want += node.Layer == 1 && !node.Disabled && !UiDraw.IsChrome(node.Command) ? 1 : 0;
                }
                int confirm = -1, feed = -1;
                for (int i = 0; i < chain.Count; i++)
                {
                    int command = scene.Nodes[chain[i]].Command;
                    confirm = command == FlareFace.SheetConfirmCommand ? i : confirm;
                    feed = command == FlareFace.SheetChipCommand || command == FlareFace.OptionCommand || command == FlareFace.FieldCommand ? i : feed;
                }
                bool cancel = confirm > 0 && scene.Nodes[chain[confirm - 1]].Command == FlareFace.SheetCancelCommand;
                bool closeLast = scene.Nodes[chain[chain.Count - 1]].Command == FlareFace.SheetCancelCommand && scene.Nodes[chain[chain.Count - 1]].Arg == 0;
                bool offered = false;   // a commit button that cannot be pressed yet (nothing written) is no target: the chips and Cancel then end the run
                for (int n = 0; n < scene.NodeCount; n++)
                {
                    offered |= scene.Nodes[n].Layer == 1 && scene.Nodes[n].Command == FlareFace.SheetConfirmCommand && !scene.Nodes[n].Disabled;
                }
                bool tail = offered ? confirm == chain.Count - 2 && cancel : scene.Nodes[chain[chain.Count - 2]].Command == FlareFace.SheetCancelCommand;
                if (chain.Count != want || !closeLast || !tail || feed > (offered ? confirm : chain.Count))
                {
                    sheets = state + " (" + chain.Count + " of " + want + " walked, commit at " + confirm + ": " + string.Join(" ", chain.ConvertAll(c => scene.Nodes[c].Command + "/" + scene.Nodes[c].Arg)) + ")";
                }
            }
        }
        // The hire page: the filters, then each row's Hire from the top down, then the chosen influencer's card.
        UiSamples.Flare(scene, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, "hire");
        int first = scene.FocusFirst >= 0 ? scene.FocusFirst : UiFocusRules.First(scene);
        int lastFilter = -1, firstHire = -1, count = 0;
        float lastHireY = -1f;
        bool buttonsDown = true;
        var seen = new HashSet<int> { first };
        for (int at = first; at >= 0; )
        {
            ref UiNode node = ref scene.Nodes[at];
            int command = node.Command;
            scene.NodeBox(at, out _, out float nodeY, out _, out _);
            if (command == FlareFace.PickCommand || command == FlareFace.NicheCommand || command == FlareFace.SortCommand)
            {
                lastFilter = count;
            }
            if (command == FlareFace.ButtonCommand)
            {
                firstHire = firstHire < 0 ? count : firstHire;
                buttonsDown &= nodeY > lastHireY;
                lastHireY = nodeY;
            }
            count++;
            int next = UiFocusRules.Next(scene, at, 1, 0);
            at = next >= 0 && next < scene.NodeCount && scene.Nodes[next].Region == node.Region && seen.Add(next) ? next : -1;
        }
        if (!(firstHire > lastFilter && lastFilter >= 0 && buttonsDown && lastHireY > 0f))
        {
            hire = "filters last at " + lastFilter + ", first Hire at " + firstHire + ", buttons down " + buttonsDown;
        }
        // The rail beside a page holds the same rows in every language (the week's tiles are the same height).
        int rows = -1;
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            UiSamples.Flare(scene, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, UiWords(lang), "home");
            int n = 0;
            for (int i = 0; i < scene.NodeCount; i++)
            {
                n += scene.Nodes[i].Command == FlareFace.WhoFollowCommand ? 1 : 0;
            }
            rows = rows < 0 ? n : rows;
            if (n != rows)
            {
                rail = lang + " shows " + n + " rows of Who to follow where en shows " + rows;
            }
        }
        Check(bad == "none" && sheets == "none" && walked > 100, "flare order: from the first target, right alone walks every screen and sheet left to right and top to bottom, never back up a row on a sheet ("
            + walked + " steps; " + bad + "); a sheet's chips, Cancel and then its commit button come last (" + sheets + ")");
        Check(pages == "none" && pagesWalked > 40, "flare order: on every page right on a nav entry goes into the page, right alone walks its column and its side without a target twice, back to an earlier group or into the nav, and the pad reaches every target from the nav ("
            + pagesWalked + " steps over " + pageStates + " pages; " + pages + ")");
        // A group of chips never leaves one chip alone in its last row. Laid as the sheet draws it, with two to twelve chips of three lengths, the chip targets' own rows are
        // counted: no row holds one chip while another holds more (a column of options, every row one chip, is the answer when two columns cannot spare one).
        string orphan = "none";
        int grids = 0;
        foreach (string[] names in new[] { new[] { "Midtown", "Garment District", "Murray Hill" }, new[] { "Garment District" }, new[] { "Now", "A moment", "Report" }, new[] { "Power or water", "Street light out" } })
        {
            for (int n = 2; n <= 12; n++)
            {
                FlareFaceModel sheet = UiSamples.FlareSample(en, "compose_report");
                var chips = new string[n];
                for (int i = 0; i < n; i++)
                {
                    chips[i] = names[i % names.Length];
                }
                sheet.Sheet0.Groups = new[] { "About" };
                sheet.Sheet0.Chips = new[] { chips };
                sheet.Sheet0.Chosen = new[] { 0 };
                FlareFace.Lay(scene, UiFrame.FullMenu(16f / 9f, "Flare"), true, UiDefaultKeys.Keyboard, en, sheet);
                var rowsAt = new List<float>();
                var inRow = new List<int>();
                for (int k = 0; k < scene.NodeCount; k++)
                {
                    if (scene.Nodes[k].Command != FlareFace.SheetChipCommand || scene.Nodes[k].Arg / FlareFace.GroupStride != 0)
                    {
                        continue;
                    }
                    scene.NodeBox(k, out _, out float cy, out _, out _);
                    int at = rowsAt.FindIndex(y => Math.Abs(y - cy) < 4f);
                    if (at < 0)
                    {
                        rowsAt.Add(cy);
                        inRow.Add(1);
                    }
                    else
                    {
                        inRow[at]++;
                    }
                }
                grids++;
                if (inRow.Count > 1 && inRow.Contains(1) && inRow.Exists(c => c > 1))
                {
                    orphan = n + " chips of " + names[0] + " and more drawn in rows of " + string.Join(", ", inRow);
                }
            }
        }
        Check(orphan == "none" && grids > 40, "flare order: a group of chips never leaves one chip alone in its last row, counted from the chips as drawn (" + grids + " sheets laid; " + orphan + ")");
        Check(hire == "none", "flare order: on the hire page the filter chips come before the Hire buttons, which go down the rows (" + hire + ")");
        Check(rail == "none" && rows >= 2, "flare order: the rail's Who to follow holds the same " + rows + " rows in en, pt and zh-cn (" + rail + ")");
    }

    // A page as a player walks it. Right on a nav entry goes into the page, to its first target at the top left, and only the account's right goes to the window's close, so the door
    // opens both ways. From that first target right alone reads the column, the detail beside it and the side, each group after the one before and each inside down the rows, never
    // to a target it has been to and never into the nav. And the pad reaches every target of the page from the nav entry the page stands on, by right, down, left and up. On a page
    // with no side and a single column right alone reaches every target of the column that is read after its start; a page with a side hands its rows on to the side at their ends
    // (that is where right goes), so the rows below the first are reached by down.
    private static int FlareOrderPage(UiScene scene, int start, int[] walk, string state, ref string bad)
    {
        int first = -1;
        for (int i = 0; i < scene.NodeCount; i++)
        {
            ref UiNode node = ref scene.Nodes[i];
            if (node.Layer != 0 || node.Disabled || UiDraw.IsChrome(node.Command) || node.Region == 11 || node.Command == UiDraw.CloseCommand)
            {
                continue;
            }
            if (first < 0 || FlareOrderRank(scene, i) < FlareOrderRank(scene, first)
                || (FlareOrderRank(scene, i) == FlareOrderRank(scene, first) && FlareReadsBefore(scene, i, first)))
            {
                first = i;
            }
        }
        start = scene.Nodes[start].Region == 11 && first >= 0 ? first : start;   // a page with nothing to press starts on its place in the nav: its reading starts where the nav's right goes
        bool nothing = first < 0 && scene.Nodes[start].Region == 11;   // a page with nothing to press: there is no reading to walk, only the nav's door to check
        bool revisit = false;
        int n = nothing ? 0 : FlareFace.WalkRight(scene, start, walk, out revisit);
        string path = string.Empty;
        if (revisit)
        {
            var steps = new List<string>();
            for (int i = 0; i < n; i++)
            {
                steps.Add(scene.Nodes[walk[i]].Command + "/" + scene.Nodes[walk[i]].Arg);
            }
            path = state + " visits " + walk[n - 1] + " twice (" + string.Join(" ", steps) + " then " + (n > 0 ? scene.Nodes[UiFocusRules.Next(scene, walk[n - 1], 1, 0)].Command.ToString() : string.Empty) + ")";
        }
        for (int i = 1; i < n && path.Length == 0; i++)
        {
            int rankBefore = FlareOrderRank(scene, walk[i - 1]), rank = FlareOrderRank(scene, walk[i]);
            scene.NodeBox(walk[i - 1], out _, out float y0, out _, out float h0);
            scene.NodeBox(walk[i], out _, out float y1, out _, out float h1);
            if (rank < rankBefore || (rank == rankBefore && y1 + h1 / 2f < y0 + h0 / 2f - 14f))
            {
                path = state + " goes back at step " + (i + 1) + " (" + scene.Nodes[walk[i - 1]].Command + "/" + scene.Nodes[walk[i - 1]].Arg + " to " + scene.Nodes[walk[i]].Command + "/" + scene.Nodes[walk[i]].Arg + ")";
            }
        }
        for (int i = 0; i < n && path.Length == 0; i++)
        {
            if (scene.Nodes[walk[i]].Region == 11)
            {
                path = state + " walks into the nav at step " + (i + 1) + " (" + scene.Nodes[walk[i]].Command + "/" + scene.Nodes[walk[i]].Arg + ")";
            }
        }
        // the nav's way in: right on every entry lands on the page's first target, the account's on the close
        int door = -1, close = scene.FindNode(UiDraw.CloseCommand, 0);
        for (int i = 0; i < scene.NodeCount && path.Length == 0; i++)
        {
            ref UiNode node = ref scene.Nodes[i];
            if (node.Layer != 0 || node.Disabled || node.Region != 11 || UiDraw.IsChrome(node.Command))
            {
                continue;
            }
            int to = UiFocusRules.Next(scene, i, 1, 0);
            int want = node.Command == FlareFace.AccountCommand ? close : first;
            if (want >= 0 && to != want)
            {
                path = state + " right on the nav's " + node.Command + "/" + node.Arg + " goes to " + (to >= 0 && to < scene.NodeCount ? scene.Nodes[to].Command + "/" + scene.Nodes[to].Arg : "nothing")
                    + " and not " + (node.Command == FlareFace.AccountCommand ? "to the close" : "into the page");
            }
            if (node.Command == FlareFace.NavCommand && node.Face >= 0 && (scene.Items[node.Face].Flags & UiFlags.Selected) != 0)
            {
                door = i;   // the entry the page stands on; Thread, which has none, is entered by the Post button
            }
        }
        door = door >= 0 ? door : scene.FindNode(FlareFace.PostCommand, 0);
        if (path.Length == 0 && door >= 0)
        {
            var order = new int[scene.NodeCount + 1];
            var from = new int[scene.NodeCount + 1];
            int met = FlareFace.Reach(scene, door, order, from);
            var seen = new bool[scene.NodeCount];
            for (int i = 0; i < met; i++)
            {
                seen[order[i]] = true;
                for (int k = 0; scene.Nodes[order[i]].List >= 0 && k < scene.NodeCount; k++)
                {
                    seen[k] |= scene.Nodes[k].List == scene.Nodes[order[i]].List;   // a list's rows are walked down it
                }
            }
            int missed = 0, firstMissed = -1;
            for (int i = 0; i < scene.NodeCount; i++)
            {
                if (!seen[i] && scene.Nodes[i].Layer == 0 && !scene.Nodes[i].Disabled && !UiDraw.IsChrome(scene.Nodes[i].Command))
                {
                    missed++;
                    firstMissed = firstMissed < 0 ? i : firstMissed;
                }
            }
            if (missed > 0)
            {
                path = state + " leaves " + missed + " targets out of the walk from the nav (first " + scene.Nodes[firstMissed].Command + "/" + scene.Nodes[firstMissed].Arg + ")";
            }
        }
        bool side = false, detail = false;
        int column = 0, reached = 0;
        scene.NodeBox(start, out float sx, out float sy, out float sw, out float sh);
        for (int i = 0; i < scene.NodeCount; i++)
        {
            ref UiNode node = ref scene.Nodes[i];
            side |= node.Region == 13;
            detail |= node.Region == 14;   // a list with the conversation it opens beside it: right alone reads one of the two, left and down the other
            scene.NodeBox(i, out float nx, out float ny, out _, out float nh);
            bool after = ny + nh / 2f > sy + sh / 2f + 14f || (Math.Abs(ny + nh / 2f - (sy + sh / 2f)) <= 14f && nx >= sx);   // read after the start: below it, or beside it to the right
            if (node.Region == 12 && !node.Disabled && node.Layer == 0 && after)
            {
                column++;
            }
        }
        for (int i = 0; i < n; i++)
        {
            ref UiNode node = ref scene.Nodes[walk[i]];
            scene.NodeBox(walk[i], out float nx, out float ny, out _, out float nh);
            bool after = ny + nh / 2f > sy + sh / 2f + 14f || (Math.Abs(ny + nh / 2f - (sy + sh / 2f)) <= 14f && nx >= sx);
            reached += node.Region == 12 && after ? 1 : 0;
        }
        if (path.Length == 0 && !side && !detail && scene.Nodes[start].Region == 12 && reached < column)
        {
            path = state + " reaches " + reached + " of " + column + " targets of its column that come after its start";
        }
        if (path.Length > 0)
        {
            bad = bad == "none" ? path : bad + "; " + path;
        }
        return n;
    }

    // Whether a target is read before another: on an earlier row, or on the same row further left.
    private static bool FlareReadsBefore(UiScene scene, int p, int q)
    {
        scene.NodeBox(p, out float px, out float py, out _, out float ph);
        scene.NodeBox(q, out float qx, out float qy, out _, out float qh);
        float dy = py + ph / 2f - (qy + qh / 2f);
        return Math.Abs(dy) > 14f ? dy < 0f : px < qx;
    }

    // Where a node stands in a page's reading: the column, the detail beside it, the side, the nav, then the window's close.
    private static int FlareOrderRank(UiScene scene, int node)
    {
        if (scene.Nodes[node].Command == UiDraw.CloseCommand)
        {
            return 4;
        }
        switch (scene.Nodes[node].Region)
        {
            case 12: return 0;
            case 14: return 1;
            case 13: return 2;
            default: return 3;
        }
    }
}
