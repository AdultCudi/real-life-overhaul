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
