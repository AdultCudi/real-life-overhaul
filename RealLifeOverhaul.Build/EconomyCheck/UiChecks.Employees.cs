using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static bool _staffChecked;

    // My Employees and a rival's staff, every state of each: laid out by the screen checks in every language and frame,
    // with the commands the staff page answers. The views' own rules are checked once, beside them.
    static partial void UiCasesEmployees(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "my_employees")
        {
            foreach (string state in UiSamples.StaffStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "my employees (" + st + ")", Title = "My Employees", Commands = StaffCommands,
                    Lay = (s, f, dark, k, t) => UiStaff.Lay(s, UiSamples.Staff(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == "rival_staff")
        {
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "a rival's staff", Title = "BizMan", Commands = StaffCommands,
                Lay = (s, f, dark, k, t) => UiStaff.Lay(s, UiSamples.RivalStaff(t), f, dark, k)
            });
        }
        else
        {
            return;
        }
        if (!_staffChecked)
        {
            _staffChecked = true;
            StaffChecks();
            PeopleFixChecks();
            GradeOnChosenChecks();
            WindowTitleChecks();
        }
    }

    private static readonly int[] StaffCommands =
    {
        UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiStaff.SwitchCommand, UiStaff.AssignCommand, UiStaff.TrainCommand, UiStaff.OptionCommand, UiStaff.PreviewCommand, UiStaff.EmptyCommand,
        UiStaff.HeldCommand,
        UiAppWindow.PersonCommand, UiAppWindow.AllCommand,
        UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Filter, UiDataTable.First + UiDataTable.Search, UiDataTable.First + UiDataTable.Row,
        UiDataTable.First + UiDataTable.Pick, UiDataTable.First + UiDataTable.Mass, UiDataTable.First + UiDataTable.Done, UiDataTable.First + UiDataTable.Clear,
        UiDataTable.First + UiDataTable.MassChoice, UiDataTable.First + UiDataTable.MassAnswer, UiCard.ButtonCommand,
        UiBatch.ButtonCommand, UiBatch.TableFirst + UiDataTable.Row
    };

    // What the staff views promise beyond the layout: the game's mass actions keep their order with the danger first and
    // the tab's main one last, each pointing back at the game's own; a person's buttons are the box's, danger first and
    // main last on F; the squad's ring is the mood by the game's own steps; the open lists stand under their own fields.
    private static void StaffChecks()
    {
        UiWords en = UiWords("en");
        var v = new StaffView
        {
            Picking = true, PickedCount = 2,
            Mass = new[]
            {
                new StaffMass { Type = "myemployees_mass_action_assignbusiness", Label = "Assign business" }, new StaffMass { Type = "myemployees_mass_action_paybonuses", Label = "Pay Bonuses" },
                new StaffMass { Type = "myemployees_mass_action_fire", Label = "Fire" }, new StaffMass { Type = "some_other_mod_action", Label = "Another mod's" },
            },
            Employees = new[] { new StaffRow { Name = "Ines Duarte", Satisfaction = 80 }, new StaffRow { Name = "Theo Brandt", Satisfaction = 20 } }
        };
        var m = new UiStaffModel();
        StaffViews.Staff(m, en, v);
        UiActionModel[] mass = m.Table.Mass;
        Check(mass.Length == 5 && mass[0].Arg == -1, "staff: picking offers all or none first, then each of the game's mass actions");
        Check(mass[1].Arg == 2 && mass[1].Kind == UiButtonKind.Destructive && mass[4].Arg == 0 && mass[4].Kind == UiButtonKind.Primary,
            "staff: firing stands first and assigning a business (the employees' main) last, each pointing at the game's own action by its place");
        Check(mass[2].Arg == 1 && mass[3].Arg == 3 && mass[3].Icon == UiIcon.None, "staff: the rest keep the game's order, and another mod's action keeps a plain button");
        Check(m.Table.Rows[0].Ring == UiTokens.Grade(80) && m.Table.Rows[1].Ring == UiTokens.Grade(20) && StaffViews.Mood(64) == UiTokens.Grade(64),
            "staff: a face's ring is the mood on the same scale as the satisfaction's meter, so the two never disagree");
        Check(StaffViews.LabelOf("Days Hired: <b>{daysHired}</b>", "daysHired") == "Days Hired" && StaffViews.LabelOf("Jours depuis l\u2019embauche\u00a0: <b>{daysHired}</b>", "daysHired")
              == "Jours depuis l\u2019embauche", "staff: the game's own label of a 'Label: value' text, its tags, colon and placeholder taken off");
        v.PickedCount = 0;
        StaffViews.Staff(m, en, v);
        Check(m.Table.Mass[1].Disabled && !m.Table.Mass[0].Disabled, "staff: with nobody picked the mass actions are held and Pick all is not");
        var c = new StaffView { Candidates = true, Mass = new[] { new StaffMass { Type = "myemployees_mass_action_discardcandidate" }, new StaffMass { Type = "myemployees_mass_action_hirecandidate" },
            new StaffMass { Type = "myemployees_mass_action_assignbusinessandhire" } }, PickedCount = 1, Picking = true };
        StaffViews.Staff(m, en, c);
        Check(m.Table.Mass[1].Arg == 0 && m.Table.Mass[3].Arg == 1 && m.Table.Mass[3].Kind == UiButtonKind.Primary && m.SwitchLabel != null,
            "staff: for candidates discarding stands first and hiring last, and the job board's switch stands on the tab band");
        var person = new StaffPersonView { Name = "Kofi Mensah", Candidate = true, Positive = "Hire", Negative = "Discard", Additional = "Negotiate", NegativeArmed = true };
        UiStaffPerson page = StaffViews.Person(en, person);
        Check(page.Actions.Length == 3 && page.Actions[0].Kind == UiButtonKind.Armed && page.Actions[1].Key == UiAction.Option && page.Actions[2].Key == UiAction.Alt
              && page.Actions[2].Arg == 2, "staff: a person's buttons are the box's, the danger first (armed after one press), the second on P and the main on F");
        person.Negative = null;
        person.Additional = null;
        Check(StaffViews.Person(en, person).Actions.Length == 1, "staff: a button the game hides is left out");
        Check(StaffViews.SkillIcon("ba:skill_programmer") == UiIcon.Code && StaffViews.SkillIcon("ba:skill_new_one") == UiIcon.Briefcase,
            "staff: each skill wears its glyph, one the kit does not know the briefcase");
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "My Employees");
        UiStaffModel open = UiSamples.Staff(en, "filter_open");
        UiStaff.Lay(scene, open, frame, true, UiDefaultKeys.Keyboard);
        Check(ListUnder(scene, UiDataTable.FilterLeft(open.Table, 1, open.TableX), open.TableY + UiTokens.Control),
            "staff: a filter's list opens under its own choice on the tool row");
        UiStaffModel ask = UiSamples.Staff(en, "mass_ask");
        UiStaff.Lay(scene, ask, frame, true, UiDefaultKeys.Keyboard);
        Check(ask.Table.MassFieldW > 0f && ListUnder(scene, ask.Table.MassFieldX, ask.TableY + UiTokens.Control),
            "staff: a mass action's choice stands on the picking bar and its list opens under it");
    }

    // A graded figure reads 4.5:1 wherever a table puts it: every grade on the panel, its bands and a row under the pointer;
    // on the chosen row's cyan tone (where a low grade's red cannot) the figure takes the row's words and the meter its grade.
    private static void GradeOnChosenChecks()
    {
        string weak = null;
        for (int g = 0; g <= 100; g += 10)
        {
            UiRole grade = UiTokens.Grade(g);
            foreach (UiRole surface in new[] { UiRole.Panel, UiRole.Band, UiRole.Hover })
            {
                float c = UiContrast(grade, surface, true);
                if (c < UiCheckRules.NormalContrast && weak == null)
                {
                    weak = grade + " on " + surface + " at " + c.ToString("0.00") + ":1";
                }
            }
        }
        Check(weak == null, "kit: every grade's figure reads 4.5:1 on the panel, a band and a row under the pointer" + (weak == null ? string.Empty : ": " + weak));
        Check(UiContrast(UiRole.Text, UiRole.RowSel, true) >= UiCheckRules.NormalContrast, "kit: the chosen row's words read 4.5:1 on its tone");
        string weakChosen = null;
        for (int g = 0; g <= 100; g += 10)
        {
            UiRole chosen = UiTokens.GradeOnChosen(UiTokens.Grade(g));
            float c = UiContrast(chosen, UiRole.RowSel, true);
            if (c < UiCheckRules.NormalContrast && weakChosen == null)
            {
                weakChosen = chosen + " at " + c.ToString("0.00") + ":1";
            }
        }
        Check(weakChosen == null, "kit: every grade's chosen-row step reads 4.5:1 on the chosen row's tone" + (weakChosen == null ? string.Empty : ": " + weakChosen));
        var table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = "Name", Kind = UiCellKind.Text }, new UiDataColumn { Title = "Skill", Kind = UiCellKind.Meter, Width = 144f },
                new UiDataColumn { Title = "Grade", Kind = UiCellKind.Figure, Width = 64f, Graded = true },
            },
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { "Low", "12", "8" }, Values = new[] { -1, 12, 8 } }, new UiDataRow { Cells = new[] { "Also low", "24", "30" }, Values = new[] { -1, 24, 30 } },
            },
            RowCount = 2, Selected = 0
        };
        var scene = new UiScene();
        scene.Begin("grade on chosen", 1920f, 1080f, true);
        UiDataTable.Lay(scene, UiAt.Screen, table, 100f, 100f, 900f, 400f, UiDefaultKeys.Keyboard);
        int chosenGraded = 0, otherGraded = 0, chosenFigure = 0;
        for (int i = 0; i < scene.ItemCount; i++)
        {
            ref UiItem it = ref scene.Items[i];
            if (it.Kind != UiKind.Text || (it.Text != "12" && it.Text != "8" && it.Text != "24" && it.Text != "30"))
            {
                continue;
            }
            bool graded = it.Ink >= UiRole.Grade1 && it.Ink <= UiRole.Grade5;
            chosenGraded += graded && (it.Text == "12" || it.Text == "8") ? 1 : 0;
            chosenFigure += it.Text == "8" && it.Ink == UiTokens.GradeOnChosen(UiTokens.Grade(8)) ? 1 : 0;
            otherGraded += graded && (it.Text == "24" || it.Text == "30") ? 1 : 0;
        }
        Check(chosenGraded == 0 && chosenFigure == 1 && otherGraded == 2,
            "kit: on the big table's chosen row a meter's figure takes the row's words, a graded figure alone its grade's chosen-row step, elsewhere its grade ("
            + chosenGraded + " in a plain grade on the chosen row, " + chosenFigure + " of 1 in its chosen step, " + otherGraded + " of 2 graded on the other)");
    }

    // The game's own brands written joined (its apps BizMan and EconoView, its billboard firm CityAds); any other joined word
    // in a title is an identifier that reached the screen ("MyEmployees").
    private static readonly string[] JoinedNames = { "BizMan", "EconoView", "CityAds" };

    // No window's title is a code name: every registered screen's every case, laid out in English, its title strip read.
    private static void WindowTitleChecks()
    {
        var s = new UiScene();
        string found = null;
        int titles = 0;
        foreach (UiScreenCase c in UiCases())
        {
            c.Lay(s, UiFrame.FullMenu(16f / 9f, c.Title), true, UiDefaultKeys.Keyboard, UiWords("en"));
            for (int i = 0; i < s.ItemCount; i++)
            {
                ref UiItem it = ref s.Items[i];
                if (it.Kind != UiKind.Text || it.Style != UiStyle.AppTitle || (it.Flags & UiFlags.Chrome) == 0 || string.IsNullOrEmpty(it.Text))
                {
                    continue;
                }
                titles++;
                foreach (string word in it.Text.Split(' '))
                {
                    if (found == null && CamelWord(word))
                    {
                        found = "\"" + it.Text + "\" (" + c.Name + ")";
                    }
                }
            }
        }
        Check(titles > 0 && found == null, "kit: no window's title is a joined code name (" + titles + " titles read)" + (found == null ? string.Empty : ": " + found));
        Check(CamelWord("MyEmployees") && CamelWord("MarketInsider") && !CamelWord("BizMan") && !CamelWord("Employees") && !CamelWord("RLO"),
            "kit: a joined code name is told from a word, an initialism and the game's own joined brand names");
    }

    // A word of a capital, small letters, then another capital inside it, as code names are joined; the game's brands aside.
    private static bool CamelWord(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 3 || !char.IsUpper(word[0]) || System.Array.IndexOf(JoinedNames, word) >= 0)
        {
            return false;
        }
        bool lower = false;
        for (int i = 1; i < word.Length; i++)
        {
            if (char.IsLower(word[i]))
            {
                lower = true;
            }
            else if (char.IsUpper(word[i]) && lower)
            {
                return true;
            }
        }
        return false;
    }

    // True when an open list's panel (on the overlay) starts at x, just under the field whose foot is at y.
    private static bool ListUnder(UiScene s, float x, float y)
    {
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Layer == 1 && s.Items[i].Kind == UiKind.Box && System.Math.Abs(s.Items[i].X - x) < 0.5f && s.Items[i].Y > y && s.Items[i].Y <= y + UiTokens.S2)
            {
                return true;
            }
        }
        return false;
    }
}
