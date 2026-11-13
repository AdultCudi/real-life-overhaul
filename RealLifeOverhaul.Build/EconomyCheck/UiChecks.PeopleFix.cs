using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // What the people and dialog screens carry over from the game's own: two people of one name each keep their face, My
    // Employees' filters combine, its table has the game's age and hours columns, its person page the days, the sick line and
    // the HR manager, a candidate's row its actions, a text of a thread can be deleted, a study session shows its diploma
    // before Start, and the kit's question box takes a long body whole. Run with --ui-only my_employees.
    private static void PeopleFixChecks()
    {
        UiWords en = UiWords("en");
        string staff = ModSource(@"Ui\Employees\EmployeesScreen.View.cs"), press = ModSource(@"Ui\Employees\EmployeesScreen.cs"), sources = ModSource(@"Ui\Faces\FaceSources.cs");

        // Faces: the key of a person carries their id, and every screen that draws the face draws it by that key.
        Check(sources.Contains("public static string Key(string name, EmployeeInstance employee)") && sources.Contains("employee.id") && sources.Contains("Character(key, data);"),
            "people: a hired person's or a candidate's face is kept under their name and their own id, so two of one name never share a face");
        var same = new StaffView
        {
            Employees = new[]
            {
                new StaffRow { Name = "Ines Duarte", Face = "Ines Duarte|a", Satisfaction = 50 }, new StaffRow { Name = "Ines Duarte", Face = "Ines Duarte|b", Satisfaction = 50 }
            }
        };
        var model = new UiStaffModel();
        StaffViews.Staff(model, en, same);
        var scene = new UiScene();
        UiStaff.Lay(scene, model, UiFrame.FullMenu(16f / 9f, "My Employees"), true, UiDefaultKeys.Keyboard);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < scene.ItemCount; i++)
        {
            if (scene.Items[i].Kind == UiKind.Portrait)
            {
                keys.Add(scene.Items[i].Text);
            }
        }
        Check(keys.Contains("Ines Duarte|a") && keys.Contains("Ines Duarte|b") && !keys.Contains("Ines Duarte"),
            "people: the table draws each row's face by its own key, not by the name the two rows share");
        string tableCell = ModSource(@"Ui\UiDataTable.cs"), person = ModSource(@"Ui\UiStaff.cs"), window = ModSource(@"Ui\UiAppWindow.cs");
        Check(tableCell.Contains("row.Face ?? words") && person.Contains("p.Face ?? p.Name") && person.Contains("person.Face ?? person.Name") && window.Contains("p.Face ?? p.Name"),
            "people: the preview, the page's head, the colleagues and the batch sheet draw by the person's face key too");
        var page = StaffViews.Person(en, new StaffPersonView { Name = "Ines Duarte", Face = "Ines Duarte|a" });
        var preview = StaffViews.Preview(en, new StaffPreviewView { Name = "Ines Duarte", Face = "Ines Duarte|a" });
        Check(page.Profile.Face == "Ines Duarte|a" && preview.Face == "Ines Duarte|a", "people: a person's page and preview carry the face key to the view");

        // Filters: each toggle on its own, and the list ticks every one that stands on.
        string setFilter = MemberText(staff, "private bool SetFilter(int list, int option)");
        Check(setFilter.Contains("SetOn(_toggles[chosen], !IsOn(_toggles[chosen]))"),
            "people: a filter option switches its own toggle on or off and leaves the others, as the game's panel combines skills, states and every group of demands");
        Check(MemberText(press, "private bool Option(int option)").Contains("several") && staff.Contains("v.OptionTicks[o] = IsOn("),
            "people: a filter's list stays open for the next pick and ticks each option that stands on");
        var ticked = new StaffView { Employees = new StaffRow[0], OpenList = 2, Options = new[] { "All", "A", "B" }, OptionTicks = new[] { false, true, true }, Filters = new[] { "x", "y", "z", "w" } };
        var open = new UiStaffModel();
        StaffViews.Staff(open, en, ticked);
        Check(open.OptionTicks != null && open.OptionTicks[1] && open.OptionTicks[2] && !open.OptionTicks[0], "people: the ticks of a list reach the list's layout");

        // The table: the age and the hours beside the game's other columns, each row as wide as the columns.
        UiDataColumn[] columns = StaffViews.EmployeeColumns(en);
        var one = new StaffView { Employees = new[] { new StaffRow { Name = "Ines Duarte", Age = 31, Hours = 24, Skill = 40, Satisfaction = 70, Wage = "$20" } } };
        StaffViews.Staff(model, en, one);
        Check(columns.Length == 8 && columns[1].Title == "Age" && columns[7].Title == "Hours Per Week" && model.Table.Rows[0].Cells.Length == 8 && model.Table.Rows[0].Cells[1] == "31"
              && model.Table.Rows[0].Cells[7] == "24" && model.Table.Rows[0].Values.Length == 8 && model.Table.Rows[0].Tones.Length == 8 && StaffViews.ColumnSkill == 3 && StaffViews.ColumnWage == 4,
            "people: the employees' table has the game's age and hours a week columns, every row as many cells as columns (" + columns.Length + ", " + columns[1].Title + ", " + columns[columns.Length - 1].Title + ", "
            + model.Table.Rows[0].Cells.Length + ", " + model.Table.Rows[0].Cells[1] + ", " + model.Table.Rows[0].Values.Length + ", " + model.Table.Rows[0].Tones.Length + ")");
        string headers = Between(staff, "EmployeeHeaders = {", "};"), alt = Between(staff, "EmployeeHeadersAlt = {", "};");
        Check(headers.Split(',').Length == columns.Length && alt.Split(',').Length == columns.Length && staff.Contains("HeaderOf(root, candidates, c)") && staff.Contains("SortsBy(candidates, head.name)"),
            "people: each table column has the game's header to sort by, and a column the game's table cannot sort is shown unsorted");
        one.Locked = new[] { false, true, false, false, false, false, false, false };
        StaffViews.Staff(model, en, one);
        Check(!model.Table.Columns[1].Sortable && model.Table.Columns[0].Sortable, "people: a column with no sortable header in the game's table is not offered as a sort");

        // The person's page: the days, the sick line, the HR manager.
        Check(MemberText(staff, "private static string DaysNote(").Contains("myemployees_employee_days") && staff.Contains("Note = DaysNote(e, d, candidate)"),
            "people: the demand for days a week shows the days scheduled and worked, in the game's words");
        UiStaffPerson sick = StaffViews.Person(en, new StaffPersonView { Name = "Ines Duarte", Sick = "Ines Duarte called in sick today", Task = "x", Status = "y" });
        Check(sick.Note == "Ines Duarte called in sick today" && sick.NoteTone == UiRole.Warn, "people: an employee who called in sick has the game's warning line on the page");
        UiStaffPerson hr = StaffViews.Person(en, new StaffPersonView { Name = "Ines Duarte", HrManager = "Sam Park", Positive = "Manage schedule" });
        bool hrButton = false;
        for (int i = 0; i < hr.Actions.Length; i++)
        {
            hrButton |= hr.Actions[i].Arg == 3 && hr.Actions[i].Command == UiAppWindow.ActionCommand;
        }
        Check(hrButton && MemberText(press, "private bool Action(int arg)").Contains("case 3:") && MemberText(press, "private bool HrManager()").Contains("BizManRules.HrManagers")
              && MemberText(press, "private bool HrManager()").Contains("SelectPlan(plan)"),
            "people: an employee under an HR manager has the manager's button, which opens BizMan's HR managers tab on that plan as the game's row does");
        Check(staff.Contains("myemployees_hrmanager_info_with_health_insurance") && staff.Contains("p.Sick = e.isAbsent"), "people: the HR row names the manager and the health plan as the game's tooltip does");

        // A candidate's row: discard, negotiate and hire.
        UiStaffPreview row = StaffViews.Preview(en, new StaffPreviewView { Name = "Kofi Mensah", Candidate = true });
        UiStaffPreview mine = StaffViews.Preview(en, new StaffPreviewView { Name = "Ines Duarte" });
        Check(row.Actions.Length == 3 && row.Actions[0].Arg == 0 && row.Actions[0].Kind == UiButtonKind.Destructive && row.Actions[2].Kind == UiButtonKind.Primary && mine.Actions.Length == 0
              && MemberText(press, "public bool Press(int command, int arg)").Contains("case UiStaff.PreviewCommand:") && press.Contains("OpenPerson(_previewRow) && Action(arg)"),
            "people: a candidate's row beside the table offers discard, negotiate and hire, each through the game's own button on the person's page");

        // Age and the clock.
        Check(MemberText(staff, "private long Signature()").Contains("TimeHelper.NowInMinutes() / 15"), "people: an open person's page is laid again as the shift's clock moves");

        // Contacts: a text of a thread can be deleted on its own.
        var talk = new UiConversationModel
        {
            Name = "Mom", Messages = new[] { new UiMessageModel { From = UiFrom.Them, Text = "Hello", Time = "10:00", Remove = 1 }, new UiMessageModel { From = UiFrom.Me, Text = "Hi", Time = "10:01", Remove = 2 },
                new UiMessageModel { From = UiFrom.Them, Text = "Said in a call", Time = "10:02" } },
            MessageCount = 3
        };
        var thread = new UiScene();
        UiConversation.LayDocked(thread, talk, UiFrame.FullMenu(16f / 9f, "Dialogue"), true, UiDefaultKeys.Keyboard);
        int deletes = 0;
        var args = new HashSet<int>();
        for (int i = 0; i < thread.NodeCount; i++)
        {
            if (thread.Nodes[i].Command == UiConversation.DeleteCommand)
            {
                deletes++;
                args.Add(thread.Nodes[i].Arg);
            }
        }
        Check(deletes == 2 && args.Contains(0) && args.Contains(1), "people: each text of the game's own in a thread has a delete button of its own, a line said in a call has none (" + deletes + ")");
        Check(ContactsRoutes.Route(UiConversation.DeleteCommand, 0, null) == ContactsRoute.Remove, "people: a delete press is a route of its own");
        int[] others = { UiConversation.ChoiceCommand, UiConversation.FormCommand, UiConversation.FormLess, UiConversation.FormMore, UiConversation.ComposeCommand, UiConversation.SendCommand,
            UiFormRows.OptionCommand, UiFormRows.RowCommand, UiFormRows.SwatchCommand, UiFormRows.RemoveCommand, UiMessageList.RowCommand, UiMessageList.SearchCommand, UiMessageList.ClearCommand };
        bool clear = true;
        foreach (int other in others)
        {
            clear &= other != UiConversation.DeleteCommand;
        }
        Check(clear, "people: the delete command is no other command of a thread's, its form's or its list's");
        string contacts = ModSource(@"Ui\Contacts\ContactsScreen.cs");
        string delete = MemberText(contacts, "private bool DeleteText(Contact open, int place)");
        Check(delete.Contains("entries != texts") && delete.Contains("EntryHolds(entry, target)") && delete.Contains("Handed(") && ModSource(@"Ui\Contacts\ContactsScreen.Read.cs").Contains("m.Remove = removable ? place : 0;"),
            "people: the delete is pressed on the game's own entry only when the entries and the texts agree, so a wrong text never goes, and the game asks first");

        // A study session before Start shows its way to the diploma.
        string activity = ModSource(@"Ui\Activity\ActivityPanelScreen.cs");
        Check(activity.Contains("state == UiActivityState.NotStarted && a.HasProgressBar()") && activity.Contains("Meter = true, Fill = share"),
            "people: the card of a study session shows the time left to the diploma before Start, as the game's panel does");

        // The question box: the kit's card takes the whole body.
        Check(MemberText(ModSource(@"Core\GameConfirm.cs"), "private static string CutOf(string body)").Contains("KitCard") && ModSource(@"Ui\Shell\ShellBoxes.cs").Contains("_m.BodyLines = LongBody;"),
            "people: where the kit draws the question box the body is not cut for the game's box, and the card wraps a long one");
    }
}
