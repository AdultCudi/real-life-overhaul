using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _bizOpsChecked;

    // BizMan's operations and the uniform presets' editor, every state of each: laid out by the screen checks in every
    // language and frame, with the keys, a pad and the mouse leading; their rules checked once beside them.
    static partial void UiCasesBizOps(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == BizManScreenIds.Ops)
        {
            foreach (string state in UiSamples.BizOpsStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "bizops (" + st + ")", Title = "BizMan", Commands = BizManPages.Commands,
                    Lay = (s, f, dark, k, t) => BizManPages.Lay(s, UiSamples.BizOps(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == BizManScreenIds.Uniforms)
        {
            foreach (string state in UiSamples.UniformStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "uniforms (" + st + ")", Title = "Configure Uniform Presets", Commands = UiUniforms.Commands,
                    Lay = (s, f, dark, k, t) => UiUniforms.Lay(s, UiSamples.Uniforms(t, st), f, dark, k)
                });
            }
        }
        else
        {
            return;
        }
        if (!_bizOpsChecked)
        {
            _bizOpsChecked = true;
            BizOpsRuleChecks();
            BizOpsLayoutChecks();
        }
    }

    private static class BizManScreenIds
    {
        public const string Ops = "bizops", Uniforms = "uniforms";
    }

    private static void BizOpsRuleChecks()
    {
        // A shift dropped into another's hours moves off it as the game's own drag does (WorkShiftDrag.AdjustHoursForOverlap).
        int[] starts = { 8 }, ends = { 12 };
        int a = 10, b = 14;
        BizOpsRules.MoveOff(starts, ends, 1, 13, ref a, ref b);
        int[] inner = { 10 }, innerEnd = { 12 };
        int c = 8, d = 15;
        BizOpsRules.MoveOff(inner, innerEnd, 1, 9, ref c, ref d);
        int e = 8, f = 15;
        BizOpsRules.MoveOff(inner, innerEnd, 1, 14, ref e, ref f);
        Check(a == 12 && b == 14 && c == 8 && d == 10 && e == 12 && f == 15,
            "bizops: a shift dropped on another moves off it as the game's drag does, one covered whole cut on the side let go of");
        // A handle stops at the next shift and keeps a shift at least an hour long (WorkShiftHelper.CalculateHandleRange).
        int[] hs = { 6, 10, 16 }, he = { 9, 14, 20 };
        BizOpsRules.HandleRange(true, 10, 14, hs, he, 3, out int lo, out int hi);
        BizOpsRules.HandleRange(false, 10, 14, hs, he, 3, out int lo2, out int hi2);
        Check(lo == 9 && hi == 13 && lo2 == 11 && hi2 == 16, "bizops: a shift's ends are pulled no further than its neighbours and never under an hour");
        // The day's opening hours by the hour, in words by their runs; a closed day has none.
        bool[] open = BizOpsRules.OpenHours(true, new[] { 6, 14 }, new[] { 12, 22 }, 2);
        bool[] shut = BizOpsRules.OpenHours(false, new[] { 6 }, new[] { 22 }, 1);
        int[] from = new int[4], to = new int[4];
        int runs = BizOpsRules.Runs(open, from, to);
        Check(open[6] && open[11] && !open[12] && !open[13] && open[14] && open[21] && !open[22] && Array.IndexOf(shut, true) < 0
              && runs == 2 && from[0] == 6 && to[0] == 12 && from[1] == 14 && to[1] == 22,
            "bizops: a day open in two runs reads as its two runs, a closed day as none");
        // A lane offers a new shift where the game shows its add mark: after a shift, at an opening, a cleaning station at any free hour.
        var taken = new bool[BizOpsRules.Hours];
        for (int h = 6; h < 14; h++)
        {
            taken[h] = true;
        }
        Check(BizOpsRules.OfferHour(open, taken, false) == 14 && BizOpsRules.OfferHour(open, taken, true) == 0 && !BizOpsRules.CanStart(open, taken, false, 3)
              && BizOpsRules.CanStart(open, taken, true, 3) && !BizOpsRules.CanStart(open, taken, false, 8),
            "bizops: a lane offers its next shift after the last one, a till only in the opening hours, a cleaning station at any free hour");
        // The operations and the editor each have their own switch on the page of the business apps.
        bool ops = false, uniforms = false;
        foreach (UiScreenDef def in UiScreenSwitches.All)
        {
            ops |= def.Id == BizManScreenIds.Ops && def.Section == UiScreenSwitches.Business;
            uniforms |= def.Id == BizManScreenIds.Uniforms && def.Section == UiScreenSwitches.Business;
        }
        Check(ops && uniforms, "bizops: the operations and the uniform presets each have a switch among the business apps");
        // Each part of a uniform the game links its menu to has its words; one the mod does not know reads as its number.
        Dictionary<string, string> en = LocaleFile("en");
        bool worded = true;
        foreach (string kind in UniformWords.Kinds)
        {
            worded &= en.ContainsKey(UniformWords.KeyOf(kind));
        }
        Check(worded && UniformWords.KeyOf("Torso") == "reallife_ui_bizops_wear_torso" && UniformWords.KeyOf("feetAccessory") == "reallife_ui_bizops_wear_feetaccessory"
              && UniformWords.KeyOf("hair") == null && UniformWords.KeyOf(null) == null && en.ContainsKey("reallife_ui_bizops_wear_other"),
            "bizops: every part of a uniform the game's menu links to has its words, and a part the mod does not know its number");
    }

    private static void BizOpsLayoutChecks()
    {
        UiWords t = UiWords("en");
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "BizMan");
        var s = new UiScene();
        BizManModel full = UiSamples.BizOps(t, "schedule_full");
        BizManPages.Lay(s, full, frame, true, UiDefaultKeys.Keyboard);
        int last = full.Ops.Board.Shifts.Length - 1;
        int lane0 = s.FindNode(UiBoard.LaneCommand, 0), shift0 = s.FindNode(UiBoard.ShiftCommand, 0), shift1 = s.FindNode(UiBoard.ShiftCommand, 1);
        Check(s.FindNode(UiBoard.ShiftCommand, last) >= 0 && s.FindNode(UiBoard.LaneCommand, full.Ops.Board.Lanes.Length - 1) >= 0 && s.FindNode(BizManPages.OpsHours, 0) >= 0
              && s.FindNode(BizManPages.People + UiDataTable.Row, 0) < 0 && lane0 >= 0 && shift0 >= 0 && shift1 >= 0 && s.Nodes[lane0].Right == shift0
              && s.Nodes[shift0].Right == shift1 && s.Nodes[shift1].Left == shift0,
            "bizops: the full day shows every shift and lane at the page's width, its hours a button, and the keys walk a lane along its shifts past the page's own rows");
        s = new UiScene();
        BizManPages.Lay(s, UiSamples.BizOps(t, "schedule_hours"), frame, true, UiDefaultKeys.Keyboard);
        int hour0 = s.FindNode(BizManPages.OpsHour, 0), hour23 = s.FindNode(BizManPages.OpsHour, 23), done = s.FindNode(BizManPages.OpsSheetDone, 0);
        Check(hour0 >= 0 && hour23 >= 0 && done >= 0 && s.Nodes[hour0].Layer == 1 && s.Nodes[hour23].Layer == 1 && s.Nodes[done].Layer == 1 && s.FocusFirst == hour0,
            "bizops: the opening hours open as a sheet over the board, every hour of the day a target with Done, the keys on the first hour");
        s = new UiScene();
        BizManPages.Lay(s, UiSamples.BizOps(t, "schedule_picker"), frame, true, UiDefaultKeys.Keyboard);
        int person = s.FindNode(BizManPages.People + UiDataTable.Row, 0), cancel = s.FindNode(BizManPages.OpsSheetDone, 0);
        Check(s.FindNode(BizManPages.People + UiDataTable.Head, 1) >= 0 && s.FindNode(BizManPages.People + UiDataTable.Search, 0) >= 0
              && s.FindNode(BizManPages.People + UiDataTable.Filter, 0) >= 0 && person >= 0 && s.Nodes[person].Layer == 1 && cancel >= 0 && s.Nodes[cancel].Layer == 1
              && s.FindNode(BizManPages.People + UiDataTable.Row, 7) >= 0,
            "bizops: the picker opens as a sheet over the board, sorts by its heads, searches and filters as the game's does, every one it lists a target");
        s = new UiScene();
        BizManModel pricing = UiSamples.BizOps(t, "pricing");
        BizManPages.Lay(s, pricing, frame, true, UiDefaultKeys.Keyboard);
        // The chosen product's price is stepped on its row; every other row is a target that chooses it, its price a figure.
        Check(pricing.Ops.Table.RowCount == 40 && s.FindNode(BizManPages.Table + UiDataTable.Row, 0) >= 0 && s.FindNode(BizManPages.Table + UiDataTable.Row, 1) >= 0
              && s.FindNode(BizManPages.Table + UiDataTable.Type, 1) >= 0 && s.FindNode(BizManPages.Table + UiDataTable.More, 1) >= 0
              && s.FindNode(BizManPages.Table + UiDataTable.Type, 0) < 0 && s.FindNode(BizManPages.Table + UiDataTable.More, 0) < 0 && s.FindNode(BizManPages.OpsPlan, 3) >= 0,
            "bizops: a pricing plan's forty products are listed beside the plans, the chosen one's new price stepped on its row");
        s = new UiScene();
        BizManPages.Lay(s, UiSamples.BizOps(t, "logistics"), frame, true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.OpsStop, 2) >= 0 && s.FindNode(BizManPages.OpsStopChoice, 0) >= 0 && s.FindNode(BizManPages.OpsStopChoice, 2) < 0
              && s.FindNode(BizManPages.Table + UiDataTable.Row, 2) >= 0 && s.FindNode(BizManPages.Table + UiDataTable.Type, 1) >= 0,
            "bizops: a route shows its three stops and the chosen stop's goods under them, the chosen good's amount stepped on its row");
        s = new UiScene();
        BizManPages.Lay(s, UiSamples.BizOps(t, "pricing_none"), frame, true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.OpsFirstPlan, 0) >= 0 && s.FindNode(BizManPages.OpsAction, 0) < 0,
            "bizops: a kind with no plans says what one does across the page, with the way to add the first");
        s = new UiScene();
        UiUniforms.Lay(s, UiSamples.Uniforms(t, "uniforms"), frame, true, UiDefaultKeys.Keyboard);
        int stage = s.FindNode(UiUniforms.Stage, 0);
        Check(stage >= 0 && s.Nodes[stage].Disabled && s.FindNode(UiUniforms.Preset, 4) >= 0 && s.FindNode(UiUniforms.Piece, 0) >= 0 && s.FindNode(UiUniforms.Colours, 0) >= 0
              && s.FindNode(UiUniforms.Colour, 0) < 0 && s.FindNode(UiUniforms.Save, 0) >= 0,
            "bizops: the uniforms' editor lists every preset, the part's pieces, the worn colour's field and Save, the game's preview left to the pointer");
        s = new UiScene();
        UiUniforms.Lay(s, UiSamples.Uniforms(t, "uniforms_colours"), frame, true, UiDefaultKeys.Keyboard);
        int worn = s.FindNode(UiUniforms.Colour, 1), lastColour = s.FindNode(UiUniforms.Colour, 11), paletteDone = s.FindNode(UiUniforms.ColoursDone, 0);
        Check(worn >= 0 && lastColour >= 0 && paletteDone >= 0 && s.Nodes[worn].Layer == 1 && s.Nodes[lastColour].Layer == 1 && s.FocusFirst == worn
              && UiFocusRules.TopLayer(s) == 1,
            "bizops: the worn colour's palette opens over the editor with every colour and Done, the d-pad starting on the worn one");
    }
}
