using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The kit's BizMan and staff screens against the game's own (the purchasing agents' tab says why no plan is added, an employee's card on
// hover and focus where a person is shown for placing) and the eight screens whose pictures showed cut text and empty columns: a table
// leaves out a column nobody has a value in, a blank cell is a dash, a name wider than its column takes a second line, a head's word is
// never split, a lead's sentence has its room, a count has its thousands separator. Read from the sources where the screen is game UI.
internal static partial class Program
{
    private static void UiBizman20Checks()
    {
        // The purchasing tab: the game has no Add plan there (plans start in the importer's phone call, one to a seated agent, with a rented
        // warehouse in use), so the kit says what is missing and what to do, from the same facts the game's own refusal reads.
        Check(BizManRules.PurchasingNoteKey(false, 3) == "reallife_ui_bizops_buy_note_warehouse" && BizManRules.PurchasingNoteKey(true, 0) == "reallife_ui_bizops_buy_note_agent"
              && BizManRules.PurchasingNoteKey(true, 2) == "reallife_ui_bizops_buy_note_free", "bizman20: the purchasing note names the warehouse, the agent or the free agents as the case is");
        string managers = ModSource(@"Ui\BizMan\Ops\BizManScreen.Managers.cs");
        string buying = MemberText(managers, "private void Purchasing(BizManBusiness b, BizOpsModel o)");
        Check(buying.Contains("PurchasingNote(b)") && buying.Contains("o.PlansNote = why;") && buying.Contains("Plans(Get<ReorderableList>(GameContract.BuyPlanOrder, list), null,"),
            "bizman20: the purchasing tab has no Add plan of its own (the game has none) and says why under its plans");
        string note = MemberText(managers, "private static string PurchasingNote(BizManBusiness b)");
        Check(note.Contains("ba:buildingtype_warehouse") && note.Contains("ba:businesstype_empty") && note.Contains("ba:skill_purchasingagent") && note.Contains("IsAssignedToAnyWorkShift()")
              && note.Contains("GetAssignedPlanForPurchasingAgent"), "bizman20: the note counts the game's own conditions: a warehouse in use, a seated purchasing agent, no plan yet");
        string pages = ModSource(@"Ui\BizManPages.Ops.cs");
        Check(pages.Contains("o.PlansNote") && ModSource(@"Ui\BizMan\Ops\BizManScreen.Ops.cs").Contains("o.PlansNote = o.PlansTitle"), "bizman20: the note is drawn under the plans and emptied with the page");
        foreach (string key in new[] { "reallife_ui_bizops_buy_note_warehouse", "reallife_ui_bizops_buy_note_agent", "reallife_ui_bizops_buy_note_free", "reallife_ui_cons_lead_found_title",
                     "reallife_ui_cons_lead_office_title" })
        {
            Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
            Check(ModSource(@"Ui\UiScreens.BizOps.cs").Contains(key) || ModSource(@"Ui\UiScreens.Construction.cs").Contains(key), "bizman20: " + key + " is named in its screen's keys");
            Check(en != null && (!en.TryGetValue(key, out string words) || (!words.Contains('!') && !words.Contains((char)0x2014) && !words.Contains((char)0x2013))),
                "bizman20: " + key + " has no dash and no exclamation mark");
        }

        // The employee's card: built from the reads of the game's own EmployeeTooltip, and asked for where a person is shown to be placed.
        string card = MemberText(ModSource(@"Ui\Tooltips\TooltipEmployee.cs"), "internal static bool Make(UiTooltipModel into, EmployeeInstance e, bool schedule)");
        Check(card.Contains("common_years_amount") && card.Contains("common_hours_per_week") && card.Contains("skill.GetRoundedValue()") && card.Contains("JobDemandHelper.GetByName(e.demands[i])")
              && card.Contains("d.PriorityLocalizeKey") && card.Contains("d.Fulfilled(e, null)") && card.Contains("ScheduleHelper.GetOverworkedDays(e)")
              && card.Contains("bizman_schedule_employee_overworked_days") && card.Contains("awaiting_replacement_from_headhunter") && card.Contains("Extend(into, e);"),
            "bizman20: a person's card reads what the game's employee tooltip reads: years, hours, mood, skills, asks with their priority and whether met, overworked days");
        string host = ModSource(@"Ui\UiHost.cs");
        Check(InOrder(MemberText(host, "private void PointerEntered(int node)"), "PersonCard(node);") && host.Contains("PersonCard(_view.Focus);"),
            "bizman20: a person's card shows on the mouse resting on a target and on the pad's focus coming to it");
        string schedule = ModSource(@"Ui\BizMan\Ops\BizManScreen.Schedule.cs");
        string at = MemberText(schedule, "public string PersonAt(int command, int arg, out bool schedule)");
        Check(at.Contains("UiBoard.ShiftCommand") && at.Contains("BizManPages.Crew + UiDataTable.Row") && at.Contains("BizManPages.People + UiDataTable.Row") && at.Contains("_dragged != null"),
            "bizman20: the schedule gives the card on a shift, on a name beside the board and on a name in the picker, never while a shift is held");
        Check(ModSource(@"Ui\BizMan\BizManScreen.cs").Contains("IUiPersonTip") && ModSource(@"Ui\Company\CompanyScreen.cs").Contains("IUiPersonTip")
              && ModSource(@"Ui\Company\CompanyLive.Rota.cs").Contains("Who = sheet.Workers[wk].id"), "bizman20: the schedule, and the Company app's Staff and Rota, ask for the card");
        UiTooltipRules.BesideRow(100f, 200f, 300f, 40f, 240f, 200f, 1920f, 1080f, 8f, out float bx, out float by);
        UiTooltipRules.BesideRow(1800f, 1000f, 300f, 40f, 240f, 200f, 1920f, 1080f, 8f, out float cx, out float cy);
        Check(bx == 100f && by > 240f && cx + 240f <= 1920f && cy + 200f <= 1000f, "bizman20: a row's card stands under the row, or over it where the screen ends, inside the screen");

        // Columns: a blank one is left out with its sort and totals kept in step; a column with some values dashes the rest.
        var t = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Good", Kind = UiCellKind.Text }, new UiDataColumn { Title = "Month", Kind = UiCellKind.Trend, Width = 104f },
                new UiDataColumn { Title = "Today", Kind = UiCellKind.Money, Width = 96f }
            },
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { "Bread", "", "$2.00" }, Values = new[] { 0, 0, 0 } },
                new UiDataRow { Cells = new[] { "Milk", "", "" }, Values = new[] { 0, 0, 0 } }
            },
            RowCount = 2, SortColumn = 2, Totals = new[] { "", "", "$2.00" }
        };
        int gone = UiDataTableRules.LeaveOutBlank(t);
        Check(gone == 1 && t.Columns.Length == 2 && t.Columns[1].Title == "Today" && t.Rows[0].Cells[1] == "$2.00" && t.SortColumn == 1 && t.Totals[1] == "$2.00" && t.Rows[0].Values.Length == 2,
            "bizman20: a column no row has a value in is left out, the sort, the totals and the rows' arrays stay in step");
        int dashed = UiDataTableRules.DashBlank(t);
        Check(dashed == 1 && t.Rows[1].Cells[1] == "-", "bizman20: a cell a column does not apply to in a row is a dash where other rows have a value");
        var none = new UiDataTableModel
        {
            Columns = new[] { new UiDataColumn { Title = "A", Kind = UiCellKind.Text }, new UiDataColumn { Title = "Needs a day", Kind = UiCellKind.Figure } },
            Rows = new[] { new UiDataRow { Cells = new[] { "x", "" } } }, RowCount = 1
        };
        Check(UiDataTableRules.DashBlank(none) == 0 && UiDataTableRules.DashBlank(none, 1, true) == 1 && none.Rows[0].Cells[1] == "-", "bizman20: a table that keeps its columns dashes a column of blanks when asked");
        string live = ModSource(@"Ui\News\NewsLive.cs");
        Check(Occurs(live, "UiDataTableRules.LeaveOutBlank(") == 2, "bizman20: the Economy's prices and districts tables leave out a week or a month the book does not hold yet");
        Check(ModSource(@"Ui\Work\RolesLive.cs").Contains("Cells = new[] { null, w.T(\"common_unknown\", \"Unknown\"), w.T(\"common_unknown\", \"Unknown\"),")
              && ModSource(@"Ui\Services\ServicesLive.Round.cs").Contains("DashBlank(t, 1, true, ServicesText.T(\"reallife_ui_cell_none\"))")
              && ModSource(@"Ui\Money\UtilitiesBuild.cs").Contains("w.Money(a.Owed),") && ModSource(@"Ui\Money\UtilitiesBuild.cs").Contains("w.T(\"reallife_ui_cell_none\", \"None\")"),
            "bizman20: a rival's ad says Unknown for its posts and applicants, services and utilities say None, and an amount owed is an amount, never a bare dash");

        // A narrow table: its tile column's head word is not split, and a name wider than its room takes a second line, whole.
        var narrow = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "District", Kind = UiCellKind.Tile }, new UiDataColumn { Title = "Pull", Kind = UiCellKind.Meter, Width = 104f },
                new UiDataColumn { Title = "Rent a day", Kind = UiCellKind.Text, Width = 110f }
            },
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { "Garment District", "53", "$41" }, Values = new[] { (int)UiIcon.MapPin, 53, 0 } },
                new UiDataRow { Cells = new[] { "Hell's Kitchen", "189", "$85" }, Values = new[] { (int)UiIcon.MapPin, 100, 0 } }
            },
            RowCount = 2
        };
        var s = new UiScene();
        s.Begin("narrow", 1920f, 1080f, true);
        UiDataTable.Lay(s, UiAt.Screen, narrow, 100f, 100f, 430f, 300f, UiDefaultKeys.Keyboard);
        int cut = 0, whole = 0, split = 0;
        string seen = string.Empty;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            cut += it.Kind == UiKind.Text && it.Text != null && it.Text.Contains("...") ? 1 : 0;
            if (it.Kind == UiKind.Text && it.Text != null && (it.Text.StartsWith("Garm") || it.Text.StartsWith("Hell")))
            {
                seen += it.Text + "@" + (int)it.W + "x" + (int)it.H + ";";
            }
            whole += it.Kind == UiKind.Text && (it.Text == "Garment District" || it.Text == "Hell's Kitchen") ? 1 : 0;
            split += it.Kind == UiKind.Text && it.Text == "District" && UiTextRules.Lines(it.Text, it.Style, it.W, 0, out float _) > 1 ? 1 : 0;
        }
        var found = new List<string>();
        int unfit = UiCheckRules.Unfit(s, line => found.Add(line));
        Check(cut == 0 && whole == 2 && split == 0 && unfit == 0, "bizman20: in a narrow table a district's name is whole on one line or two, and the head's word is not split (cut " + cut + ", whole " + whole
            + ", split " + split + ", seen " + seen + ", unfit " + string.Join(" | ", found) + ")");

        // The screens' own rooms.
        string face = ModSource(@"Ui\Construction\ConstructionFace.cs");
        Check(face.Contains("LeadLines = 3") && face.Contains("lead.Title, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle") && face.Contains("lead.Body, UiFlags.Wrap | UiFlags.Label, LeadLines"),
            "bizman20: the construction lead's sentence wraps to its lines and its title is read by the fit check");
        string overview = ModSource(@"Construction\Live\ConstructionLive.Overview.cs");
        Check(overview.Contains("Body = office ? ConstructionText.T(\"reallife_bld_glance_found\"") && overview.Contains("\"reallife_ui_cons_lead_office_title\""), "bizman20: the construction start's long sentence is its body, a short line its title");
        string vent = ModSource(@"Ui\Ventures\VentFace.cs");
        Check(vent.Contains("UiTextRules.Width(b.Value ?? string.Empty, UiStyle.BodyStrong)") && vent.Contains("b.Run, b.Disabled, b.Hint, true);") && ModSource(@"Ui\UiDraw.Fields.cs").Contains("(whole ? UiFlags.Label"),
            "bizman20: a stepper's field is as wide as its figure needs and the fit check reads the figure");
        string day = ModSource(@"Ui\UiPlanDay.cs");
        Check(day.Contains("bool two = !UiTextRules.Fits(e.Title, UiStyle.Body, tw)") && day.Contains("UiFlags.Label | UiFlags.Wrap | UiFlags.Right, 2"), "bizman20: a later day's words take a second line before they are cut");
        string mine = ModSource(@"Ui\Estate\EstateLive.Mine.cs");
        Check(!mine.Contains("\"units\", units.ToString(Inv)") && !mine.Contains("\"units\", l.Units.ToString(Inv)") && mine.Contains("\"units\", l.Units.ToString(\"N0\", Inv)"),
            "bizman20: the estate's counts of units carry their thousands separator");
        Check("1911".Length == 4 && 1911.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) == "1,911", "bizman20: a count of 1911 is written 1,911");
    }
}
