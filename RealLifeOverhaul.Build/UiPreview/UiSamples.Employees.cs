using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Ui;

/// <summary>
/// My Employees and a rival's staff on the kit, each state as the game would hand it over (StaffView, through the same
/// StaffViews the game's screen uses): a full squad sorted by satisfaction, one employee, none, the candidates with the job
/// board's hidden, five picked for a mass action, a mass action asking which business, a filter's list open, an employee's
/// page, a candidate's page, a training under way, a long name with a sick day, the squad in French (the longest of the
/// mod's languages, the game's words in it and the mod's new words still in English, as a French player sees them until the
/// next batch) and a rival's staff. Plain English, as the owner reads the previews.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>Every state of My Employees, in the previews' order.</summary>
    public static readonly string[] StaffStates =
    {
        "employees_40", "employees_1", "employees_0", "candidates_hidden", "picking_5", "mass_ask", "filter_open", "person_employee", "person_candidate",
        "training_card", "person_long", "employees_fr", "person_fr"
    };

    private static readonly string[] Firsts =
    {
        "Maya", "Nadia", "Hana", "Dana", "Theo", "Ines", "Sam", "Priya", "Luca", "Amara", "Jonas", "Elif", "Kofi", "Marta", "Ravi", "Sofia", "Owen", "Leila",
        "Tomas", "Yara", "Felix", "Noor", "Carmen", "Isaac"
    };

    private static readonly string[] Lasts =
    {
        "Okafor", "Haddad", "Sato", "Whitfield", "Brandt", "Duarte", "Okoro", "Raman", "Moretti", "Diallo", "Lindqvist", "Kaya", "Mensah", "Novak", "Iyer",
        "Castillo", "Pryce", "Farah", "Silva", "Haddock", "Weber", "Aziz", "Ortega", "Bellamy"
    };

    private static readonly string[] Shops = { "Bloom Café", "Harbour Hardware", "Uptown Bakery", "Kessler Logistics", "Midtown Office" };
    private const string LongShop = "Harbour Lights Bakery and Coffee House";

    private static readonly string[] Skills = { "ba:skill_customerservice", "ba:skill_cleaning", "ba:skill_securityguard", "ba:skill_deliverydriver", "ba:skill_programmer" };
    private static readonly string[] SkillWords = { "Customer Service", "Cleaning", "Security Guard", "Delivery Driver", "Programmer" };
    private static readonly string[] Tasks = { "Cash Register", "Mop and Bucket", "Security Post", "Box Truck", "Computer Workstation" };

    /// <summary>My Employees in one of its states.</summary>
    public static UiStaffModel Staff(UiWords t, string state)
    {
        UiWords w = state.EndsWith("_fr") ? French(t) : t;
        StaffView v = StaffSample(w, state);
        var m = new UiStaffModel();
        StaffViews.Staff(m, w, v);
        return m;
    }

    private static StaffView StaffSample(UiWords t, string state)
    {
        var v = new StaffView { EmployeeCount = 40, CandidateCount = 6, Filters = new[] { t("reallife_ui_staff_all_businesses", "All businesses"), t("reallife_ui_staff_all_skills", "All skills"),
            t("reallife_ui_staff_all_demands", "All demands"), t("reallife_ui_staff_any_status", "Any status") } };
        v.Mass = new[]
        {
            new StaffMass { Type = "myemployees_mass_action_assignbusiness", Label = t("myemployees_mass_action_assignbusiness", "Assign business") },
            new StaffMass { Type = "myemployees_mass_action_paybonuses", Label = t("myemployees_mass_action_paybonuses", "Pay Bonuses") },
            new StaffMass { Type = "myemployees_mass_action_trainprimaryskill", Label = t("myemployees_mass_action_trainprimaryskill", "Train primary skill") },
            new StaffMass { Type = "myemployees_mass_action_fire", Label = t("myemployees_mass_action_fire", "Fire") },
            new StaffMass { Type = "reallife_co_mass_raise", Label = t("reallife_co_mass_raise", "Give a raise") },
        };
        switch (state)
        {
            case "employees_1":
                v.EmployeeCount = 1;
                v.Employees = new[] { Employee(t, 1) };
                v.Selected = 0;
                v.Preview = new StaffPreviewView
                {
                    Name = v.Employees[0].Name, Role = v.Employees[0].Role, RoleIcon = StaffViews.SkillIcon(Skills[0]), Business = v.Employees[0].Business,
                    Status = t("reallife_ui_staff_at_work", "At work"), StatusTone = UiRole.Ok, Skill = v.Employees[0].Skill, Satisfaction = v.Employees[0].Satisfaction, Age = 29,
                    Hours = 36, Wage = v.Employees[0].Wage, Demands = 7, DemandsMet = 4, DemandRows = PersonSample(t, v.Employees[0].Name, false, false, false).Demands
                };
                break;
            case "employees_0":
                v.EmployeeCount = 0;
                v.CandidateCount = 3;
                break;
            case "candidates_hidden":
                Candidates(t, v, 6);
                v.Filters = new[] { t("reallife_ui_staff_all_skills", "All skills"), t("reallife_ui_staff_all_demands", "All demands") };
                v.HideJobBoard = true;
                v.SortColumn = 3;
                v.SortDown = true;
                break;
            case "picking_5":
            case "mass_ask":
                Squad(t, v, 40);
                v.Picking = true;
                for (int i = 0; i < 5; i++)
                {
                    v.Employees[i * 2 + 1].Picked = true;
                }
                v.PickedCount = 5;
                if (state == "mass_ask")
                {
                    v.AskTitle = t("myemployees_mass_action_assign_business_title", "Select business to assign to employees");
                    v.AskValue = Shops[2];
                    v.OpenList = UiStaff.ListMass;
                    v.Options = new[] { t("common_unassign", "Unassign"), Shops[0], Shops[1], Shops[2], Shops[3], Shops[4] };
                    v.OptionChosen = 3;
                }
                break;
            case "filter_open":
                Squad(t, v, 40);
                v.OpenList = 1;
                v.Options = new[] { t("reallife_ui_staff_all_skills", "All skills"), t(Skills[0], SkillWords[0]), t(Skills[1], SkillWords[1]), t(Skills[2], SkillWords[2]),
                    t(Skills[3], SkillWords[3]), t(Skills[4], SkillWords[4]) };
                v.OptionChosen = 0;
                break;
            case "person_employee":
            case "person_fr":
                v.Person = PersonSample(t, "Maya Okafor", false, false, false);
                break;
            case "person_candidate":
                v.Candidates = true;
                v.Person = PersonSample(t, "Kofi Mensah", true, false, false);
                break;
            case "training_card":
                v.Person = PersonSample(t, "Theo Brandt", false, true, false);
                break;
            case "person_long":
                v.Person = PersonSample(t, "Alexandria Montgomery-Okonkwo Vasquez", false, false, true);
                break;
            default:
                Squad(t, v, 40);
                v.SortColumn = 4;
                v.SortDown = true;
                break;
        }
        return v;
    }

    private static void Squad(UiWords t, StaffView v, int count)
    {
        var rows = new StaffRow[count];
        float wages = 0f;
        for (int i = 0; i < count; i++)
        {
            rows[i] = Employee(t, i);
            wages += Wage(i);
        }
        // Sorted by satisfaction, the highest first, as the game's header sorts its table.
        System.Array.Sort(rows, (a, b) => b.Satisfaction.CompareTo(a.Satisfaction));
        v.Employees = rows;
        v.Listed = count;
        v.TotalWage = Money(wages);
        StaffRow top = rows[1];
        v.Selected = 1;   // the cursor on the second row, its person in the preview beside the table
        v.Preview = new StaffPreviewView
        {
            Name = top.Name, Role = top.Role, RoleIcon = StaffViews.SkillIcon(Skills[2]), Business = top.Business,
            Status = t("reallife_ui_staff_at_work", "At work"), StatusTone = UiRole.Ok, Skill = top.Skill,
            Satisfaction = top.Satisfaction, Age = 31, Hours = 28, Wage = top.Wage, Demands = 7, DemandsMet = 4,
            DemandRows = PersonSample(t, top.Name, false, false, false).Demands
        };
    }

    private static StaffRow Employee(UiWords t, int i)
    {
        int kind = i % 5;
        int satisfaction = (i * 37 + 18) % 97;
        bool unassigned = i % 13 == 7, sick = i % 11 == 4, training = i % 17 == 9;
        string task = unassigned ? null : training ? "myemployees_task_training" : sick ? "myemployees_task_absent" : null;
        return new StaffRow
        {
            Name = Firsts[i % Firsts.Length] + " " + Lasts[(i * 7 + i / Firsts.Length * 5) % Lasts.Length], Role = t(Skills[kind], SkillWords[kind]),
            Business = unassigned ? null : Shops[(i / 2) % Shops.Length], Skill = 34 + (i * 23) % 62, Wage = Money(Wage(i)), Satisfaction = satisfaction,
            Task = unassigned ? t("myemployees_no_task", "No task") : training ? t("myemployees_task_training", "Training") : sick ? t("myemployees_task_absent", "Sick") : Tasks[kind],
            TaskTone = unassigned ? UiRole.Danger : sick ? UiRole.Warn : training ? UiRole.Accent : UiRole.Muted
        };
    }

    // What each is paid an hour, at the city's prices: the game's wage for the skill, a little over the start's.
    private static float Wage(int i) => 14.5f + (i * 13 % 40) * 0.35f;

    private static string Money(float amount) => "$" + amount.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static void Candidates(UiWords t, StaffView v, int count)
    {
        v.Candidates = true;
        v.CandidateCount = count;
        var rows = new CandidateRow[count];
        string[] sources = { t("ba:itemname_jobboard", "Job Board"), "Riverside Recruitment", "Headhunter: Dana Whitfield" };
        string[] schedules = { t("ba:jobdemand_fulltime", "Full-time"), t("ba:jobdemand_parttime", "Part-time") };
        for (int i = 0; i < count; i++)
        {
            int kind = (i + 2) % 5, hoursLeft = i == 0 ? 6 : 48 + i * 22;
            rows[i] = new CandidateRow
            {
                Name = Firsts[(i + 9) % Firsts.Length] + " " + Lasts[(i * 5 + 3) % Lasts.Length], Role = t(Skills[kind], SkillWords[kind]), Skill = 88 - i * 9,
                Wage = Money(16f + (5 - i) * 1.6f), Schedule = schedules[i % 2], Source = sources[1 + i % 2],
                Expires = hoursLeft < 24 ? t("common_hours", "{hours} hours").Replace("{hours}", hoursLeft.ToString(CultureInfo.InvariantCulture))
                    : t("common_days", "{value} days").Replace("{value}", (hoursLeft / 24).ToString(CultureInfo.InvariantCulture)),
                Soon = hoursLeft < 24
            };
        }
        v.CandidateRows = rows;
        v.Listed = count + 4;   // the job board's four hidden by the game's toggle
        v.Selected = 1;   // the cursor on the second row, as the keys' shot rings it
        v.Preview = new StaffPreviewView
        {
            Candidate = true, Name = rows[1].Name, Role = rows[1].Role, RoleIcon = StaffViews.SkillIcon(Skills[3]), Skill = rows[1].Skill, Age = 27, Wage = rows[1].Wage,
            Schedule = rows[1].Schedule, Source = rows[1].Source, Expires = rows[1].Expires,
            Status = StaffViews.Fill(t("reallife_ui_staff_expires", "Expires in {time}"), "time", rows[1].Expires), StatusTone = UiRole.Muted,
            DemandRows = PersonSample(t, rows[1].Name, true, false, false).Demands
        };
    }

    // A person's page as the game's box writes it: the facts, the box's buttons, two or three skills and the demands.
    private static StaffPersonView PersonSample(UiWords t, string name, bool candidate, bool training, bool sick)
    {
        var p = new StaffPersonView
        {
            Candidate = candidate, Name = name, Role = t(Skills[candidate ? 4 : 0], SkillWords[candidate ? 4 : 0]), RoleIcon = StaffViews.SkillIcon(Skills[candidate ? 4 : 0]),
            Business = candidate ? null : sick ? LongShop : Shops[0], Satisfaction = candidate ? 50 : sick ? 41 : 64, Level = candidate ? 77 : 82,
            Age = candidate ? 34 : 29, Wage = candidate ? "$31.40" : "$22.40", DaysHired = 214, WeeklyHours = 36, WorkedHours = 22,
            Assign = candidate ? t("common_unassigned", "Unassigned") : sick ? LongShop : Shops[0],
            AssignOn = !training
        };
        if (candidate)
        {
            p.Schedule = t("ba:jobdemand_fulltime", "Full-time");
            p.Source = "Riverside Recruitment";
            p.Expires = t("common_days", "{value} days").Replace("{value}", "3");
            p.Status = StaffViews.Fill(t("reallife_ui_staff_expires", "Expires in {time}"), "time", p.Expires);
            p.StatusTone = UiRole.Muted;
            p.Positive = t("myemployees_hirecandidate_button", "Hire");
            p.Negative = t("myemployees_discardcandidate_button", "Discard");
            p.Additional = t("dialog_negotiate_button", "Negotiate");
        }
        else
        {
            p.Task = training ? t("myemployees_task_training", "Training") : sick ? t("myemployees_task_absent", "Sick") : Tasks[0];
            p.Status = training ? p.Task : sick ? p.Task : t("reallife_ui_staff_at_work", "At work");
            p.StatusTone = training ? UiRole.Accent : sick ? UiRole.Warn : UiRole.Ok;
            p.Positive = t("myemployees_manage_schedule", "Manage schedule");
            p.Negative = t("myemployees_fire", "Fire employee");
            p.Bonus = t("myemployees_configure_pay_bonus", "Pay Bonus ({bonusAmount})").Replace("{bonusAmount}", "$1.2K");
            p.BonusOn = !sick;
            p.Cooldown = sick ? t("myemployees_pay_bonus_cooldown", "Cooldown: {days} days").Replace("{days}", "12") : null;
            p.HrManager = sick ? null : "Dana Whitfield";
            if (training)
            {
                p.Training = t(Skills[0], SkillWords[0]);
                p.TrainingLeft = t("common_hours_left", "{hours} hours left").Replace("{hours}", "14");
                p.TrainingCard = true;
            }
        }
        string train = t("myemployees_train_skill_button", "Train skill");
        p.Skills = candidate
            ? new[] { new UiStaffSkill { Label = t(Skills[4], SkillWords[4]), Value = 77 }, new UiStaffSkill { Label = t(Skills[0], SkillWords[0]), Value = 41 } }
            : new[]
            {
                new UiStaffSkill { Label = t(Skills[0], SkillWords[0]), Value = 82, Train = training ? null : train },
                new UiStaffSkill { Label = t(Skills[1], SkillWords[1]), Value = 58, Train = training ? null : train },
                new UiStaffSkill { Label = t(Skills[2], SkillWords[2]), Value = 34, Train = training ? null : train },
            };
        string met = t("reallife_ui_staff_met", "Met"), notMet = t("reallife_ui_staff_not_met", "Not met");
        string hours = null;   // the hours worked stand once, in the work's rows
        p.Demands = new[]
        {
            Demand(t, UiIcon.Clock, "ba:jobdemand_fulltime", "Full-time", hours, 2, candidate ? -1 : 1, met, notMet),
            Demand(t, UiIcon.Calendar, "ba:jobdemand_freeweekends", "No weekends", null, 1, candidate ? -1 : 0, met, notMet),
            Demand(t, UiIcon.Calendar, "ba:jobdemand_nomornings", "No morning shifts", null, 0, candidate ? -1 : 1, met, notMet),
            Demand(t, UiIcon.Package, "ba:jobdemand_coffeemachine", "Coffee Machine", null, 0, candidate ? -1 : 0, met, notMet),
            Demand(t, UiIcon.Heart, "ba:jobdemand_silverhealthinsurance", "Silver Health Insurance", null, 1, candidate ? -1 : 1, met, notMet),
            Demand(t, UiIcon.Sofa, "ba:jobdemand_cleanworkplace", "Clean work environment", null, 2, candidate ? -1 : 1, met, notMet),
            Demand(t, UiIcon.Package, "ba:jobdemand_seatedatofficechair", "Office Chair", null, 0, candidate ? -1 : 0, met, notMet),
        };
        p.MatesAt = candidate ? p.Role : p.Business;
        // A candidate's are the staff with their skill; an employee's work beside them at the same business.
        int[] kinds = candidate ? new[] { 4, 4, 4 } : new[] { 0, 2, 1, 3, 0, 1 };
        string[] names = { "Nadia Haddad", "Dana Whitfield", "Ines Duarte", "Sam Okoro", "Priya Raman", "Luca Moretti" };
        int[] skills = { 77, 85, 69, 47, 72, 58 }, moods = { 68, 74, 80, 63, 55, 71 };
        p.Mates = new StaffMate[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            p.Mates[i] = new StaffMate
            {
                Name = names[i], Role = t(Skills[kinds[i]], SkillWords[kinds[i]]), RoleIcon = StaffViews.SkillIcon(Skills[kinds[i]]), Skill = skills[i], Satisfaction = moods[i]
            };
        }
        p.MatesTotal = kinds.Length;
        return p;
    }

    private static UiStaffDemand Demand(UiWords t, UiIcon icon, string key, string english, string note, int priority, int met, string metWords, string notMetWords)
    {
        string[] keys = { "job_demand_priority_low", "job_demand_priority_medium", "job_demand_priority_high" };
        string[] words = { "Nice to have", "Important", "Critical" };
        return new UiStaffDemand
        {
            Icon = icon, Name = t(key, english), Note = note, Priority = t(keys[priority], words[priority]),
            PriorityTone = priority == 2 ? UiRole.Danger : priority == 1 ? UiRole.Warn : UiRole.Muted, Met = met, MetWords = met > 0 ? metWords : notMetWords
        };
    }

    /// <summary>A rival's staff as the game lists them: their skill, the hours they look for, one in talks and one who once worked for the player.</summary>
    public static UiStaffModel RivalStaff(UiWords t)
    {
        var rows = new RivalRow[9];
        for (int i = 0; i < rows.Length; i++)
        {
            int kind = i % 3;
            rows[i] = new RivalRow
            {
                Name = Firsts[(i + 4) % Firsts.Length] + " " + Lasts[(i * 3 + 11) % Lasts.Length], Skill = t(Skills[kind], SkillWords[kind]), Level = 92 - i * 7,
                Hours = 24 + (i * 6) % 20, Talking = i == 4, Poached = i == 5, SkillKey = Skills[kind], Age = 23 + (i * 7) % 30,
                Wage = Money(15.5f + (92 - i * 7) * 0.12f)
            };
        }
        var m = new UiStaffModel();
        // The cursor on the third, as the keys' shot rings it; the game's offer for them beside the table.
        var offer = new RivalChosenView
        {
            Demands = new[]
            {
                Demand(t, UiIcon.Clock, "ba:jobdemand_fulltime", "Full-time", null, 2, -1, null, null),
                Demand(t, UiIcon.Calendar, "ba:jobdemand_freeweekends", "No weekends", null, 1, -1, null, null),
                Demand(t, UiIcon.Package, "ba:jobdemand_coffeemachine", "Coffee Machine", null, 0, -1, null, null),
                Demand(t, UiIcon.Heart, "ba:jobdemand_silverhealthinsurance", "Silver Health Insurance", null, 1, -1, null, null),
            }
        };
        StaffViews.Rival(m, t, t("bizman_presentation_rival_employees_list", "Employees working at {businessName}").Replace("{businessName}", "Golden Crust Bakery"), rows, 2,
            offer);
        return m;
    }

    // The game's words in French over the words the checks and previews were given, as a French player reads the screen:
    // the game's own keys in French, the mod's new keys in English until the next translation batch.
    private static UiWords French(UiWords t)
    {
        var fr = new Dictionary<string, string>
        {
            { "myemployees_employees_tab", "Employés" }, { "myemployees_candidates_tab", "Candidats" }, { "common_cancel", "Annuler" }, { "common_confirm", "Confirmer" },
            { "common_unassigned", "Non affecté" }, { "myemployees_employee_name", "Nom de l’employé" }, { "myemployees_current_business", "Entreprise actuelle" },
            { "myemployees_age", "Âge" }, { "myemployees_jobtype", "Compétence" }, { "myemployees_hourly_wage", "Salaire horaire" }, { "myemployees_satisfaction", "Satisfaction" },
            { "myemployees_task", "Tâche" }, { "myemployees_skills", "Compétences" }, { "myemployees_wants_and_demands", "Envies et exigences" },
            { "myemployees_assignbusiness", "Affecter à une entreprise" }, { "myemployees_configure_pay_bonus_tooltip_title", "Prime" }, { "myemployees_hrmanager", "Responsable RH" },
            { "myemployees_no_task", "Aucune tâche" }, { "myemployees_task_absent", "Malade" }, { "myemployees_task_training", "Formation" },
            { "myemployees_train_skill_button", "Former la compétence" }, { "myemployees_manage_schedule", "Gérer le planning" }, { "myemployees_fire", "Licencier l’employé" },
            { "myemployees_configure_pay_bonus", "Verser une prime ({bonusAmount})" }, { "myemployees_mass_action_assignbusiness", "Affecter à une entreprise" },
            { "myemployees_mass_action_paybonuses", "Verser des primes" }, { "myemployees_mass_action_trainprimaryskill", "Former la compétence principale" },
            { "myemployees_mass_action_fire", "Licencier" }, { "job_demand_priority_high", "Critique" }, { "job_demand_priority_medium", "Important" },
            { "job_demand_priority_low", "Souhaitable" }, { "ba:skill_customerservice", "Service client" }, { "ba:skill_cleaning", "Nettoyage" },
            { "ba:skill_securityguard", "Agent de sécurité" }, { "ba:skill_deliverydriver", "Chauffeur-livreur" }, { "ba:skill_programmer", "Programmeur" },
            { "ba:jobdemand_fulltime", "Temps plein" }, { "ba:jobdemand_freeweekends", "Pas de week-ends" }, { "ba:jobdemand_nomornings", "Pas de services le matin" },
            { "ba:jobdemand_coffeemachine", "Machine à café" }, { "ba:jobdemand_silverhealthinsurance", "Assurance santé Argent" },
            { "ba:jobdemand_cleanworkplace", "Environnement de travail propre" }, { "ba:jobdemand_seatedatofficechair", "Chaise de bureau" },
            { "myemployees_days_hired", "Jours depuis l’embauche : <b>{daysHired}</b>" }, { "common_days", "{value} jours" }, { "smartphone_myemployees", "MyEmployees" },
        };
        return (key, english) => fr.TryGetValue(key, out string words) ? words : t(key, english);
    }
}
