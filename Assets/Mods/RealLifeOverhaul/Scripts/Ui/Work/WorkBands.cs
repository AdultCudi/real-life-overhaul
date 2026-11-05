using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The plain bands that fill the foot of a work page, made from arrays by the live source and the previews alike: tiles (a glyph, a name, a line under it and a figure),
    /// people (a face, a role and a figure) and pairs (a label and its value). Each is what the page's own list does not already say. Game-free.
    /// </summary>
    public static class WorkBands
    {
        /// <summary>Tiles: a glyph, a name, a line under it and a figure at the right; at most <paramref name="most"/> of them.</summary>
        public static WorkFormBand Tiles(string title, UiIcon[] icons, string[] labels, string[] hints, string[] values, int most = 3)
        {
            var band = new WorkFormBand { Title = title };
            for (int i = 0; i < labels.Length && i < most; i++)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Icon = icons[i], Label = labels[i], Hint = hints[i], Value = values != null ? values[i] : null });
            }
            band.Count = band.Rows.Count > 0 ? labels.Length.ToString() : null;
            return band;
        }

        /// <summary>People: a face, their role and a figure at the right.</summary>
        public static WorkFormBand People(string title, string[] names, string[] roles, string[] rights, int most = 3)
        {
            var band = new WorkFormBand { Title = title };
            for (int i = 0; i < names.Length && i < most; i++)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = names[i], Role = roles[i], Right = rights != null ? rights[i] : null, Ring = UiRole.Ok } });
            }
            band.Count = band.Rows.Count > 0 ? names.Length.ToString() : null;
            return band;
        }

        /// <summary>Pairs: a label and its value.</summary>
        public static WorkFormBand Pairs(string title, string[] labels, string[] values)
        {
            var band = new WorkFormBand { Title = title };
            for (int i = 0; i < labels.Length; i++)
            {
                band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = labels[i], Value = values[i] });
            }
            return band;
        }

        /// <summary>
        /// Who joined last and who has been with the firm longest, two bands of at most two people each from one list: the team's own record of the day each joined.
        /// A team of one is not split in two, and a band is left out when it would say what the other says.
        /// </summary>
        public static void Tenure(System.Collections.Generic.List<WorkFormBand> into, UtilitiesWords w, string[] names, string[] roles, int[] joined, string[] joinedText)
        {
            int n = names.Length;
            if (n < 3)
            {
                return;
            }
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) => joined[b] != joined[a] ? joined[b].CompareTo(joined[a]) : a.CompareTo(b));   // the newest first, the team's own order between two on one day
            var newest = new WorkFormBand { Title = w.T("reallife_work_band_newest", "Newest on the team") };
            var longest = new WorkFormBand { Title = w.T("reallife_work_band_longest", "With you the longest") };
            for (int k = 0; k < 2; k++)
            {
                int a = order[k], b = order[n - 1 - k];
                newest.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = names[a], Role = roles[a], Right = joinedText[a], Ring = UiRole.Ok } });
                longest.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = names[b], Role = roles[b], Right = joinedText[b], Ring = UiRole.Ok } });
            }
            newest.Count = n.ToString();
            into.Add(newest);
            into.Add(longest);
        }

        /// <summary>
        /// Of the applicants a list shows: the two with the strongest craft on the CV and the two that ask the least, as two bands of people (a face, where each comes from
        /// and the figure). Made from one list, so what is said of the list is what the list holds.
        /// </summary>
        public static void Candidates(System.Collections.Generic.List<WorkFormBand> into, UtilitiesWords w, string[] names, string[] from, int[] craft, int[] asks, string[] askText)
        {
            int n = names.Length;
            if (n < 4)
            {
                return;
            }
            var byCraft = new int[n];
            var byAsk = new int[n];
            for (int i = 0; i < n; i++)
            {
                byCraft[i] = i;
                byAsk[i] = i;
            }
            Array.Sort(byCraft, (a, b) => craft[b] != craft[a] ? craft[b].CompareTo(craft[a]) : a.CompareTo(b));
            Array.Sort(byAsk, (a, b) => asks[a] != asks[b] ? asks[a].CompareTo(asks[b]) : a.CompareTo(b));
            var strongest = new WorkFormBand { Title = w.T("reallife_work_band_strongest_cv", "Strongest on the CV") };
            var cheapest = new WorkFormBand { Title = w.T("reallife_work_band_cheapest_hire", "Cheapest to hire") };
            for (int k = 0; k < 2; k++)   // two each: the band stands under a list of ten rows, in the room that is left
            {
                strongest.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = names[byCraft[k]], Role = from[byCraft[k]],
                    Right = w.F("reallife_work_craft_n", "craft {n}", "n", craft[byCraft[k]].ToString()), Ring = UiRole.Ok } });
                cheapest.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = names[byAsk[k]], Role = from[byAsk[k]], Right = askText[byAsk[k]], Ring = UiRole.Ok } });
            }
            into.Add(strongest);
            into.Add(cheapest);
        }

        /// <summary>A band added to a list when it has rows, so a page never draws a title over nothing.</summary>
        public static void AddIfAny(System.Collections.Generic.List<WorkFormBand> into, WorkFormBand band)
        {
            if (band.Rows.Count > 0)
            {
                into.Add(band);
            }
        }
    }
}
