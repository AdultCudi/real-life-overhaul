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
        }
    }

    private static readonly int[] StaffCommands =
    {
        UiAppWindow.NavCommand, UiAppWindow.ActionCommand, UiStaff.SwitchCommand, UiStaff.AssignCommand, UiStaff.TrainCommand, UiStaff.OptionCommand,
        UiDataTable.First + UiDataTable.Head, UiDataTable.First + UiDataTable.Filter, UiDataTable.First + UiDataTable.Search, UiDataTable.First + UiDataTable.Row,
        UiDataTable.First + UiDataTable.Pick, UiDataTable.First + UiDataTable.Mass, UiDataTable.First + UiDataTable.Done, UiDataTable.First + UiDataTable.Clear,
        UiDataTable.First + UiDataTable.MassChoice, UiDataTable.First + UiDataTable.MassAnswer, UiCard.ButtonCommand
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
        Check(m.Table.Rows[0].Ring == UiRole.Ok && m.Table.Rows[1].Ring == UiRole.Danger && StaffViews.Mood(30) == UiRole.Danger && StaffViews.Mood(60) == UiRole.Warn
              && StaffViews.Mood(61) == UiRole.Ok, "staff: a face's ring is the mood by the game's own steps (red to 30, yellow to 60)");
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
