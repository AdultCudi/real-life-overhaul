using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What fills the feet of the Campus pages, built from plain facts by the live source and the previews alike: the skills' strongest and weakest and the courses
    /// that would help, the family's funds against what a degree costs, and the schools' best results and free places. Game-free.
    /// </summary>
    public static class CampusBands
    {
        /// <summary>The level of a skill in a word: the four steps of its points.</summary>
        public static string Level(UtilitiesWords w, int points)
        {
            return points < 25 ? w.T("reallife_work_level_novice", "Novice") : points < 50 ? w.T("reallife_work_level_competent", "Competent")
                : points < 75 ? w.T("reallife_work_level_skilled", "Skilled") : w.T("reallife_work_level_expert", "Expert");
        }

        /// <summary>A skill's line under its name: its level, and where it was learned.</summary>
        public static string SkillLine(UtilitiesWords w, int points, string place) => w.F("reallife_work_skill_line", "{level}, {place}", "level", Level(w, points), "place", place);

        /// <summary>The skills page's foot: what is strongest and weakest, and the courses that would raise the weakest (each with what it adds and its fee).</summary>
        public static void SkillsBands(UtilitiesWords w, WorkTab t, string strongest, string weakest, int average, string[] courses, string[] adds, string[] fees,
            UiIcon[] rustyIcons, string[] rustyNames, string[] rustyBest, string[] rustyDrop)
        {
            var figures = new WorkFormBand { Title = w.T("reallife_work_band_skill_figures", "Where you stand") };
            figures.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_skill_strongest", "Strongest"), Value = strongest });
            figures.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_skill_weakest", "Weakest"), Value = weakest });
            figures.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_skill_average", "Average level"), Value = Math.Max(0, Math.Min(100, average)) } });
            figures.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_skill_known", "Skills you have"), Value = t.Skills.Count.ToString() });
            t.Below.Add(figures);
            WorkFormBand help = CourseTiles(w.T("reallife_work_band_courses_help", "Courses that would help"), courses, adds, fees);
            if (help.Rows.Count > 0)
            {
                t.Below.Add(help);
            }
            if (rustyNames.Length > 0)
            {
                var rusty = new WorkFormBand { Title = w.T("reallife_work_band_rusty", "Gone rusty") };
                for (int i = 0; i < rustyNames.Length && i < 3; i++)
                {
                    rusty.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Icon = rustyIcons[i], Label = rustyNames[i], Hint = rustyBest[i], Value = rustyDrop[i] });
                }
                t.Below.Add(rusty);
            }
        }

        /// <summary>A band of courses, each a tile with what it adds under its name and its fee at the right: the foot of Skills and of Results.</summary>
        public static WorkFormBand CourseTiles(string title, string[] courses, string[] adds, string[] fees, int most = 3)
        {
            var band = new WorkFormBand { Title = title };
            for (int i = 0; i < courses.Length && i < most; i++)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Icon = UiIcon.Cap, Label = courses[i], Hint = adds[i], Value = fees[i] });
            }
            return band;
        }

        /// <summary>The family page's foot: the funds the children have, against what the degrees on offer cost.</summary>
        public static void FamilyBand(UtilitiesWords w, WorkFormModel form, string funds, int offers, string cost, string toPay)
        {
            var band = new WorkFormBand { Title = w.T("reallife_work_band_family_funds", "Funds and costs") };
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_family_funds", "College funds in all"), Value = funds });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_family_offers", "Degrees on offer"), Value = offers.ToString() });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_family_cost", "What they cost"), Value = cost });
            band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_family_to_pay", "You would pay after the funds"), Value = toPay });
            form.Right.Add(band);
        }

        /// <summary>The schools page's foot, made from its rows: the schools with the best results, and the ones with the most places free.</summary>
        public static void SchoolsBands(UtilitiesWords w, WorkTab t)
        {
            int n = t.Rows.Count;
            if (n == 0)
            {
                return;
            }
            var byResults = new List<int>(n);
            var byFree = new List<int>(n);
            var free = new int[n];
            for (int i = 0; i < n; i++)
            {
                byResults.Add(i);
                byFree.Add(i);
                string[] parts = (t.Rows[i].Cells.Length > 2 ? t.Rows[i].Cells[2] : string.Empty).Split('/');
                free[i] = parts.Length == 2 && int.TryParse(parts[0].Trim(), out int pupils) && int.TryParse(parts[1].Trim(), out int places) ? Math.Max(0, places - pupils) : 0;
            }
            byResults.Sort((a, b) => t.Rows[b].Values[3].CompareTo(t.Rows[a].Values[3]));
            byFree.Sort((a, b) => free[b].CompareTo(free[a]));
            var best = new WorkFormBand { Title = w.T("reallife_work_band_best_results", "Best results") };
            for (int k = 0; k < byResults.Count && k < 3; k++)
            {
                WorkRow r = t.Rows[byResults[k]];
                best.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Icon = UiIcon.Building, Label = r.Name, Hint = r.Cells[1], Value = r.Values[3].ToString() + "%" });
            }
            t.Below.Add(best);
            var freeBand = new WorkFormBand { Title = w.T("reallife_work_band_free_places", "Most places free") };
            for (int k = 0; k < byFree.Count && k < 3; k++)
            {
                WorkRow r = t.Rows[byFree[k]];
                freeBand.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Icon = UiIcon.Building, Label = r.Name, Hint = r.Cells[1],
                    Value = w.F("reallife_work_n_free", "{n} free", "n", free[byFree[k]].ToString()) });
            }
            t.Below.Add(freeBand);
        }
    }
}
