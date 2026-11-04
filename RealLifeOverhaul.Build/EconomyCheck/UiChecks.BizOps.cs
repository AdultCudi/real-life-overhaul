using System;
using System.Collections.Generic;
using System.IO;
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
        // Pieces a day from the game's week of seven, in tenths, and how they read: one decimal under ten, whole from there.
        Check(BizOpsRules.TenthsADay(0) == 0 && BizOpsRules.TenthsADay(-3) == 0 && BizOpsRules.TenthsADay(7) == 10 && BizOpsRules.TenthsADay(10) == 14
              && BizOpsRules.Tenths(0) == "0" && BizOpsRules.Tenths(24) == "2.4" && BizOpsRules.Tenths(30) == "3" && BizOpsRules.Tenths(99) == "9.9"
              && BizOpsRules.Tenths(100) == "10" && BizOpsRules.Tenths(124) == "12" && BizOpsRules.Tenths(125) == "13" && BizOpsRules.PerDay(10) == "1.4"
              && BizOpsRules.PerDay(1000) == "143",
            "bizops: a week's count reads as pieces a day, one decimal under ten and whole from there, in figures that never follow the machine's culture");
        // How long a stock lasts in tints, by the game's days until empty; a machine's pace from its rate an hour.
        Check(BizOpsRules.LastsTone(-1) == UiRole.DangerText && BizOpsRules.LastsTone(2) == UiRole.DangerText && BizOpsRules.LastsTone(3) == UiRole.WarnText
              && BizOpsRules.LastsTone(4) == UiRole.WarnText && BizOpsRules.LastsTone(5) == UiRole.None && BizOpsRules.LastsTone(int.MaxValue) == UiRole.None
              && BizOpsRules.TenthsEach(36) == 1000 && BizOpsRules.TenthsEach(3600) == 10 && BizOpsRules.TenthsEach(0) == 0,
            "bizops: a stock gone or lasting under three days reads in the danger's colour, under five the warning's; a pace is the hour over the rate");
        // A person's free hours around the hour being filled, by their shifts that day; the hours their demand leaves them.
        int[] own = { 6, 16 }, ownEnd = { 10, 20 };
        bool noon = BizOpsRules.FreeWindow(own, ownEnd, 2, 12, out int f1, out int t1), held = BizOpsRules.FreeWindow(own, ownEnd, 2, 8, out int _, out int _);
        bool late = BizOpsRules.FreeWindow(own, ownEnd, 2, 21, out int f2, out int t2), clear = BizOpsRules.FreeWindow(own, ownEnd, 0, 3, out int f3, out int t3);
        Check(noon && f1 == 10 && t1 == 16 && !held && late && f2 == 20 && t2 == BizOpsRules.Hours && clear && f3 == 0 && t3 == BizOpsRules.Hours
              && BizOpsRules.HoursLeft(40, 32) == 8 && BizOpsRules.HoursLeft(40, 46) == 0 && BizOpsRules.HoursLeft(0, 10) == -1,
            "bizops: a person is free from their last shift's end to their next one's start, held where a shift covers the hour; the hours left never under none");
    }

    // An item of the scene whose words are these (the kit lays words whole; the text cuts a long one when it is drawn).
    private static bool OpsSays(UiScene s, string words)
    {
        for (int i = 0; words != null && i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && s.Items[i].Text == words)
            {
                return true;
            }
        }
        return false;
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
        // The day's facts over the board with Fill the gaps at their right, its problems, and the planner's strip under the lanes.
        Check(OpsSays(s, t("reallife_ui_bizops_on_shift_now", "On shift now")) && OpsSays(s, t("reallife_ui_bizops_week_planned", "Week planned"))
              && full.Ops.Problems.Length == 2 && OpsSays(s, full.Ops.Problems[0]) && OpsSays(s, full.Ops.Problems[1]) && OpsSays(s, full.Ops.Board.CoverTitle)
              && OpsSays(s, full.Ops.Board.Cover[17]) && full.Ops.Board.Short[17] && !full.Ops.Board.Short[12] && OpsSays(s, t("reallife_ui_bizops_fill_gaps", "Fill the gaps")),
            "bizops: the full day shows its facts and both its problems over the board, each hour's staff against the planner's need under it, and Fill the gaps");
        Check(s.FindNode(BizManPages.OpsSearch, 0) < 0 && full.Ops.FactActions.Length == 1 && Array.FindIndex(full.Actions, a => a.Label == full.Ops.FactActions[0].Label) < 0,
            "bizops: a place of five stations shows no lanes' search, and Fill the gaps stands with the day's facts, not among the game's own day buttons");
        // The staff placed over needed: one cell for each hour that wants staff, standing in its hour's column on the ruler, apart from the
        // next, the placed figure over the needed one (no string of figures run together), short hours in the danger's colours.
        foreach (string state in new[] { "schedule_full", "schedule_tens" })
        {
            var cs = new UiScene();
            BizManModel cm = UiSamples.BizOps(t, state);
            BizManPages.Lay(cs, cm, frame, true, UiDefaultKeys.Keyboard);
            UiBoardModel bd = cm.Ops.Board;
            float perHour = bd.RulerW / Math.Max(1, bd.LastHour - bd.FirstHour);
            int cells = 0, drawn = 0, outside = 0, touching = 0, tooWide = 0, wrongTone = 0, unsaid = 0;
            float prevRight = float.MinValue;
            for (int h = bd.FirstHour; h < bd.LastHour; h++)
            {
                if (string.IsNullOrEmpty(bd.Cover[h]))
                {
                    continue;
                }
                cells++;
                int i = h - bd.FirstHour;
                UiBoard.CoverCell(bd.RulerX, perHour, i, out float cx, out float cw);
                outside += cx < bd.RulerX + i * perHour - 0.01f || cx + cw > bd.RulerX + (i + 1) * perHour + 0.01f ? 1 : 0;
                touching += cx < prevRight + 0.01f ? 1 : 0;
                prevRight = cx + cw;
                tooWide += UiTextRules.Width(bd.Cover[h], UiStyle.SmallStrong) > cw || UiTextRules.Width(bd.Needed[h], UiStyle.Small) > cw ? 1 : 0;
                UiRole wantEdge = bd.Short[h] ? UiRole.Danger : UiRole.None;
                bool box = false, placed = false, needed = false;
                for (int k = 0; k < cs.ItemCount; k++)
                {
                    UiItem it = cs.Items[k];
                    bool here = Math.Abs(it.X - cx) < 0.01f && Math.Abs(it.W - cw) < 0.01f;
                    box |= here && it.Kind == UiKind.Box && it.Fill == UiRole.Band && it.Edge == wantEdge;
                    placed |= here && it.Kind == UiKind.Text && it.Text == bd.Cover[h] && it.Style == UiStyle.SmallStrong;
                    needed |= here && it.Kind == UiKind.Text && it.Text == bd.Needed[h] && it.Style == UiStyle.Small;
                }
                drawn += box ? 1 : 0;
                wrongTone += box ? 0 : 1;
                unsaid += placed && needed ? 0 : 1;
            }
            Check(cells >= 8 && drawn == cells && outside == 0 && touching == 0 && tooWide == 0 && unsaid == 0 && wrongTone == 0,
                "bizops: the staff row (" + state + ") has a cell for each of its " + cells + " hours in that hour's column, none touching the next, the placed figure over the needed one inside it and"
                + " short hours in the danger's colours (" + outside + " outside, " + touching + " touching, " + tooWide + " too wide, " + unsaid + " unsaid, " + wrongTone + " wrong)");
        }
        string pageSource = File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\BizMan\BizManScreen.Page.cs")));
        string en1 = File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales\en.json")));
        Check(pageSource.Contains("alerts.Count == 1 ? UiText.T(\"reallife_ui_bizman_sum_alert_one\")") && en1.Contains("\"reallife_ui_bizman_sum_alert_one\": \"1 alert\""),
            "bizman: one alert reads \"1 alert\", not \"1 alerts\"; the singular key is used where the count is one");
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            UiWords w = UiWords(lang);
            string one = w("reallife_ui_bizman_sum_alert_one", "MISSING"), many = w("reallife_ui_bizman_sum_alerts", "MISSING"), goods = w("reallife_ui_bizops_goods_one", "MISSING"), piece = w("reallife_ui_bizops_output_one", "MISSING");
            Check(one != "MISSING" && one.Contains("1") && !one.Contains("{count}") && many.Contains("{count}") && goods != "MISSING" && goods.Contains("1") && piece != "MISSING" && piece.Contains("1"),
                "bizman: the singular count labels (one alert, one good, one piece an hour) exist in " + lang);
        }
        s = new UiScene();
        BizManModel empty = UiSamples.BizOps(t, "schedule_empty");
        BizManPages.Lay(s, empty, UiFrame.FullMenu(21f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);   // at 16:9 the game's five day buttons leave it no room
        Check(empty.Ops.Board.Lanes.Length >= BizOpsRules.LaneSearchFrom && s.FindNode(BizManPages.OpsSearch, 0) >= 0 && s.FindNode(BizManPages.OpsHours, 0) >= 0,
            "bizops: on a wide screen a place of more stations than the board holds shows the lanes' search beside the day's hours");
        s = new UiScene();
        BizManModel none = UiSamples.BizOps(t, "schedule_none");
        BizManPages.Lay(s, none, frame, true, UiDefaultKeys.Keyboard);
        Check(none.Ops.Board.CoverTitle == null && none.Ops.Problems.Length == 0 && !OpsSays(s, t("reallife_ui_bizops_week_planned", "Week planned")),
            "bizops: a place with no stations has no planner's strip and no week planned, as the planner keeps no rota for it");
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
        // Its facts, its strategy's own rows in the form's right column (the plan's half), its filter over the table and Reset
        // to yesterday (the prices' half, with the products' search and pictures).
        Check(s.FindNode(BizManPages.OpsFilter, 2) >= 0 && s.FindNode(BizManPages.Table + UiDataTable.Search, 0) >= 0 && pricing.Ops.Table.Columns.Length == 7
              && pricing.Ops.Table.Columns[0].Kind == UiCellKind.Tile && pricing.Ops.Table.Columns[4].Kind == UiCellKind.Stepper && s.FindNode(BizManPages.OpsFormTab, 1) >= 0
              && OpsSays(s, t("reallife_ui_bizops_reset_prices", "Reset to yesterday")),
            "bizops: a pricing plan's prices show its three filters, a search and seven columns with the good's picture and the price stepped in its own, and Reset to yesterday");
        s = new UiScene();
        pricing = UiSamples.BizOps(t, "pricing_plan");
        BizManPages.Lay(s, pricing, frame, true, UiDefaultKeys.Keyboard);
        int stanceRow = Array.FindIndex(pricing.Ops.Rows, r => r.Kind == BizOpsRowKind.Choice && r.Right);
        int marginRow = stanceRow < 0 ? -1 : Array.FindIndex(pricing.Ops.Rows, stanceRow + 1, r => r.Kind == BizOpsRowKind.Choice && r.Right);
        int stepRow = Array.FindLastIndex(pricing.Ops.Rows, r => r.Kind == BizOpsRowKind.Choice && r.Right);
        int followRow = Array.FindIndex(pricing.Ops.Rows, r => r.Kind == BizOpsRowKind.Switch && r.Right);
        Check(stanceRow > 0 && marginRow > stanceRow && stepRow > marginRow && followRow > 0 && s.FindNode(BizManPages.OpsChoice, stanceRow) >= 0
              && s.FindNode(BizManPages.OpsChoice, marginRow) >= 0 && s.FindNode(BizManPages.OpsChoice, stepRow) >= 0 && s.FindNode(BizManPages.OpsRow, followRow) >= 0
              && pricing.Ops.Table == null && OpsSays(s, t("reallife_ui_bizops_average_margin", "Average margin")),
            "bizops: a pricing plan's other half shows its facts and its strategy as switches and choices");
        s = new UiScene();
        BizManPages.Lay(s, UiSamples.BizOps(t, "logistics"), frame, true, UiDefaultKeys.Keyboard);
        int good = s.FindNode(BizManPages.Table + UiDataTable.Row, 1), less = s.FindNode(BizManPages.Table + UiDataTable.Less, 1);
        int typed = s.FindNode(BizManPages.Table + UiDataTable.Type, 1), more = s.FindNode(BizManPages.Table + UiDataTable.More, 1);
        Check(good >= 0 && less >= 0 && typed >= 0 && more >= 0 && s.Nodes[good].Right == less && s.Nodes[less].Right == typed && s.Nodes[typed].Right == more
              && s.Nodes[more].Left == typed && s.Nodes[typed].Left == less && s.Nodes[less].Left == good,
            "bizops: the keys go right from the chosen good along its minus, its figure and its plus, and back the same way");
        Check(s.FindNode(BizManPages.OpsStop, 2) >= 0 && s.FindNode(BizManPages.OpsStopChoice, 0) >= 0 && s.FindNode(BizManPages.OpsStopChoice, 2) < 0
              && s.FindNode(BizManPages.Table + UiDataTable.Row, 2) >= 0 && s.FindNode(BizManPages.Table + UiDataTable.Type, 1) >= 0
              && OpsSays(s, t("reallife_ui_bizops_send_now", "Send now")) && OpsSays(s, t("reallife_ui_bizops_last_run", "Last delivery")),
            "bizops: a route shows its three stops and the chosen stop's goods under them, the chosen good's amount stepped on its row, its runs and Send now");
        s = new UiScene();
        BizManModel sent = UiSamples.BizOps(t, "logistics_sent");
        BizManPages.Lay(s, sent, frame, true, UiDefaultKeys.Keyboard);
        Check(sent.Ops.Status != null && OpsSays(s, sent.Ops.Status) && !OpsSays(s, sent.Ops.TableLead),
            "bizops: just after Send now the route says what it sent where the stop's goods are named, until the page changes");
        s = new UiScene();
        BizManModel factory = UiSamples.BizOps(t, "factory");
        BizManPages.Lay(s, factory, frame, true, UiDefaultKeys.Keyboard);
        Check(s.FindNode(BizManPages.OpsFilter, 0) >= 0 && s.FindNode(BizManPages.OpsFilter, 2) >= 0 && OpsSays(s, factory.Ops.TableLead)
              && OpsSays(s, t("reallife_ui_bizops_held_now", "Held now")) && OpsSays(s, t("reallife_ui_bizops_pace", "Pace")),
            "bizops: a factory's machines filter by their states under their total output, the chosen one's held stock and pace beside its recipe");
        s = new UiScene();
        BizManModel drivers = UiSamples.BizOps(t, "warehouse_drivers");
        BizManPages.Lay(s, drivers, frame, true, UiDefaultKeys.Keyboard);
        int lastChoice = Array.FindLastIndex(drivers.Ops.Rows, r => r.Kind == BizOpsRowKind.Choice);
        Check(lastChoice > 0 && s.FindNode(BizManPages.OpsChoice, lastChoice) >= 0 && OpsSays(s, t("reallife_ui_bizops_away", "Away"))
              && OpsSays(s, drivers.Ops.Slots[0].Line) && drivers.Ops.Slots.Length == 6 && drivers.Ops.Slots[5].DriverArg == lastChoice,
            "bizops: every vehicle slot is a card with where it is, its load and its driver's choice, the last slot's too");
        s = new UiScene();
        BizManModel stock = UiSamples.BizOps(t, "warehouse_stock");
        BizManPages.Lay(s, stock, frame, true, UiDefaultKeys.Keyboard);
        Check(stock.Ops.Table.Columns.Length == 8 && OpsSays(s, stock.Ops.FootLabel) && OpsSays(s, stock.Ops.FootRight) && OpsSays(s, stock.Ops.FootWords)
              && s.FindNode(BizManPages.Table + UiDataTable.Row, 0) >= 0,
            "bizops: the stock lists a day's in and out, how long each lasts and what it cost, the shelf space and the stock's worth under it");
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
