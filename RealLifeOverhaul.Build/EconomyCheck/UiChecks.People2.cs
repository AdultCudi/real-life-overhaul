using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // The second pass over people and dialogs: a face keyed by the person on Contacts, a rival's staff and a company question,
    // the candidate's business choice beside the table, the game's words under a demand, the gender and the sick row, the
    // keys by the activity's slider and the plan and manager in a health offer. Run with --ui-only my_employees.
    private static void People2Checks()
    {
        UiWords en = UiWords("en");
        string faces = ModSource(@"Ui\Contacts\ContactsScreen.Faces.cs"), read = ModSource(@"Ui\Contacts\ContactsScreen.Read.cs"), rival = ModSource(@"Ui\Employees\RivalStaffScreen.cs");
        string company = ModSource(@"Ui\Company\CompanyLive.Question.cs"), companyFace = ModSource(@"Ui\Company\CompanyFace.Question.cs");

        // Faces: Contacts' list, head, thread and call, the rival's staff and a company question draw by the person's own key.
        Check(faces.Contains("FaceSources.Key(name, who)") && faces.Contains("FaceSources.Employee(name, key, who)"), "people2: an employee's or a candidate's contact is told to the faces under their own key");
        Check(read.Contains("Face = faceKey") && read.Contains("h.Face = Face(c)") && read.Contains("t.FaceKey = c != null ? Face(c) : null") && read.Contains("Face = _m.Head.Face"),
            "people2: the contact's row, head, thread and call card carry the face key");
        Check(rival.Contains("FaceSources.Key(row.Name, e)") && rival.Contains("row.Face = ask.Face"), "people2: a rival's staff member is told and drawn under their own key");
        Check(company.Contains("FaceKeyed(manager.Name, manager.Id)") && company.Contains("FaceKeyed(f.Name, f.Id)") && companyFace.Contains("PortraitFace ="), "people2: a company question tells its asker and its subject under their own keys");
        var list = new UiMessageListModel { Rows = new[] { new UiMessageRow { Name = "Ines Duarte", Face = "Ines Duarte|a" }, new UiMessageRow { Name = "Ines Duarte", Face = "Ines Duarte|b" } }, RowCount = 2 };
        var s = new UiScene();
        UiMessageList.Lay(s, UiAt.Screen, list, 0f, 0f, 420f, 400f);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Portrait)
            {
                keys.Add(s.Items[i].Text);
            }
        }
        Check(keys.Contains("Ines Duarte|a") && keys.Contains("Ines Duarte|b") && !keys.Contains("Ines Duarte"), "people2: two contacts of one name draw their own faces in the message list");

        // The candidate's business, a choice beside the table that opens the game's own drop-down of businesses.
        var candidate = StaffViews.Preview(en, new StaffPreviewView { Candidate = true, Name = "Ines Duarte" });
        Check(!string.IsNullOrEmpty(candidate.AssignLabel) && !string.IsNullOrEmpty(candidate.AssignValue), "people2: a candidate's preview has the business choice, unassigned until one is picked");
        var hired = StaffViews.Preview(en, new StaffPreviewView { Name = "Ines Duarte", Business = "Bakery" });
        Check(string.IsNullOrEmpty(hired.AssignLabel), "people2: a hired person's preview has no choice of business (their page holds it)");
        Check(MemberText(ModSource(@"Ui\Employees\EmployeesScreen.cs"), "public bool Press(int command, int arg)").Contains("UiStaff.PreviewAssign") &&
              ModSource(@"Ui\Employees\EmployeesScreen.cs").Contains("OpenPerson(_previewRow) && Open(UiStaff.ListAssign)"), "people2: the choice opens the person and the game's list of businesses");
        var sc = new UiScene();
        candidate.Actions = new UiActionModel[0];
        UiStaff.Preview(sc, UiAt.Screen, candidate, 0f, 0f, 360f, 900f, UiDefaultKeys.Keyboard);
        bool target = false;
        for (int i = 0; i < sc.NodeCount; i++)
        {
            target |= sc.Nodes[i].Command == UiStaff.PreviewCommand && sc.Nodes[i].Arg == UiStaff.PreviewAssign;
        }
        Check(target, "people2: the business choice is a target of the preview");

        // The game's words under a demand, the gender, the sick row, the keys of the slider, the plan and manager of an offer.
        string view = ModSource(@"Ui\Employees\EmployeesScreen.View.cs"), staffViews = ModSource(@"Ui\StaffViews.cs");
        Check(view.Contains("About = About(key)") && view.Contains("_description"), "people2: a demand carries the words the game's tooltip gives it");
        var demandPage = new UiStaffPerson { Demands = new[] { new UiStaffDemand { Name = "Full-time", About = "Works forty hours a week" } } };
        var ds = new UiScene();
        UiStaff.Person(ds, UiAt.Screen, demandPage, 0f, 0f, 1200f, 800f, UiDefaultKeys.Keyboard);
        bool about = false;
        for (int i = 0; i < ds.ItemCount; i++)
        {
            about |= ds.Items[i].Text == "Works forty hours a week";
        }
        Check(about, "people2: a demand's words are drawn under its name");
        Check(view.Contains("p.Gender = ") && staffViews.Contains("common_gender"), "people2: the person page names their gender in the game's words");
        Check(view.Contains("Task = e.isAbsent ? UiText.T(AbsentTask)"), "people2: a sick employee's row says so in its task, as the game's warning icon does");
        string activity = ModSource(@"Ui\Activity\ActivityPanelScreen.cs");
        Check(activity.Contains("PlayerAction.SliderLeft") && activity.Contains("PlayerAction.SliderRight") && MemberText(ModSource(@"Ui\Activity\ActivityRead.cs"), "public static string Capped(string key, string words, bool after)").Contains("after ?"),
            "people2: the activity slider's ends carry the game's keys for left and right");
        Check(read.Contains("HrManagerHelper.GetPlanFromId(o.hrManagerPlanId)") && read.Contains("o.planType.GetLocalizeKey()"), "people2: a health plan offer names the plan and its HR manager as the game's card does");
    }
}
