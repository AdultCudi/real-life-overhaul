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
        string bad = "none", sheets = "none", hire = "none", rail = "none";
        int walked = 0;
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
        Check(hire == "none", "flare order: on the hire page the filter chips come before the Hire buttons, which go down the rows (" + hire + ")");
        Check(rail == "none" && rows >= 2, "flare order: the rail's Who to follow holds the same " + rows + " rows in en, pt and zh-cn (" + rail + ")");
    }
}
