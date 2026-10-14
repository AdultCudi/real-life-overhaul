using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Meta
{
    /// <summary>What a line of the Settings app is.</summary>
    public enum LineKind
    {
        Heading,      // a section's name: picking it shows the section, its main switch and its Guide page
        Option,       // a saved option: a switch, a choice or a number
        Button,       // a button of the player's (a preset, "Save my choices to a file", a page to open)
        Key,          // one of the mod's keys: shown, changed on the Mods tab
        TestHeading,  // a section's test tools, under Troubleshooting
        Test          // one test tool
    }

    /// <summary>One line of a tab of the Settings app: what it is, the section it stands in, and the row of the page it stands for.</summary>
    public sealed class SettingsLine
    {
        public LineKind Kind;
        public int Tab;                // the group of OptionsTree.Groups it is on
        public TreeSection Section;    // the section it stands in; null for "Other"
        public int Row = -1;           // the index into the page's rows (an option, a button, a key) or into the test tools (a test); -1 for a heading
        public int Filter;             // which of its tab's filter kinds it is of
        public int TestGroup = -1;     // for test tools: the group whose tests they are (-1: a feature no section stands for)

        public bool IsHeading => Kind == LineKind.Heading || Kind == LineKind.TestHeading;
    }

    /// <summary>One filter kind of a tab: a section, or a group's test tools.</summary>
    public sealed class SettingsKind
    {
        public TreeSection Section;    // null with TestGroup -1 and IsTests false: "Other"
        public int TestGroup = -1;
        public bool IsTests;
    }

    /// <summary>One tab of the Settings app: one group of the options page, its lines in the page's order.</summary>
    public sealed class SettingsTab
    {
        public readonly TreeGroup Group;
        public readonly List<SettingsLine> Lines = new List<SettingsLine>();
        public readonly List<SettingsKind> Kinds = new List<SettingsKind>();

        internal SettingsTab(TreeGroup group)
        {
            Group = group;
        }
    }

    /// <summary>Where a test tool stands: the group and the section of the page its feature is under, and its label.</summary>
    public struct TestPlace
    {
        public int Group;              // index into OptionsTree.Groups; -1 for a feature no section stands for
        public string SectionId;       // null with Group -1
        public string LabelKey;

        public TestPlace(int group, string sectionId, string labelKey)
        {
            Group = group;
            SectionId = sectionId;
            LabelKey = labelKey;
        }
    }

    /// <summary>
    /// The arithmetic of the Settings app, with no Unity or game types so that CatalogueCheck runs it as it ships:
    /// which line stands on which tab and under which heading (from the very plan the Mods tab is put in order by,
    /// <see cref="OptionsPlan.Place"/>), how the presets compare with the choices made, the values a number's
    /// selector steps through, and which lines a search finds. Cost: <see cref="Tabs"/> is one pass over the rows and
    /// the test tools, once when the page is built; the rest runs on a click, over a few hundred ints. Nothing per frame.
    /// </summary>
    public static class SettingsRules
    {
        public const int MostSteps = 21;   // values a number's selector offers between its ends: a click or two from any of them to any other
        public const int TabChars = 11;    // a tab's label: ten tabs stand side by side over the table

        // ---------------------------------------------------------------- the tabs

        /// <summary>
        /// The app's tabs, one per group of the options page in its order. Each section that has rows is a heading
        /// followed by its rows, as the plan placed them; rows the plan placed nowhere ("Other") and the test tools go
        /// on the last tab, Troubleshooting, the test tools by group and section as the Mods tab has them.
        /// </summary>
        public static SettingsTab[] Tabs(Plan plan, IList<PlanRow> rows, IList<TestPlace> tests)
        {
            TreeGroup[] groups = OptionsTree.Groups;
            var tabs = new SettingsTab[groups.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                tabs[g] = new SettingsTab(groups[g]);
            }
            foreach (PlanSection placed in plan.Sections)
            {
                int g = Array.IndexOf(groups, placed.Tree.Group);
                if (g < 0)
                {
                    continue;
                }
                SettingsTab tab = tabs[g];
                int filter = tab.Kinds.Count;
                tab.Kinds.Add(new SettingsKind { Section = placed.Tree });
                tab.Lines.Add(new SettingsLine { Kind = LineKind.Heading, Tab = g, Section = placed.Tree, Filter = filter });
                foreach (int index in placed.Rows)
                {
                    tab.Lines.Add(new SettingsLine { Kind = KindOf(rows[index].Kind), Tab = g, Section = placed.Tree, Row = index, Filter = filter });
                }
            }

            int trouble = groups.Length - 1;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g].Id == OptionsTree.TroubleId)
                {
                    trouble = g;
                }
            }
            SettingsTab last = tabs[trouble];
            if (plan.Other.Rows.Count > 0)
            {
                int filter = last.Kinds.Count;
                last.Kinds.Add(new SettingsKind());
                last.Lines.Add(new SettingsLine { Kind = LineKind.Heading, Tab = trouble, Filter = filter });
                foreach (int index in plan.Other.Rows)
                {
                    last.Lines.Add(new SettingsLine { Kind = KindOf(rows[index].Kind), Tab = trouble, Row = index, Filter = filter });
                }
            }

            // The test tools: a group's in one filter kind, a heading per section, in the order of the tree; those of a
            // feature no section stands for last.
            for (int g = 0; g < groups.Length; g++)
            {
                int filter = -1;
                foreach (TreeSection section in groups[g].Sections)
                {
                    AddTests(last, trouble, tests, g, section, ref filter);
                }
            }
            int rest = -1;
            AddTests(last, trouble, tests, -1, null, ref rest);
            return tabs;
        }

        private static void AddTests(SettingsTab tab, int trouble, IList<TestPlace> tests, int group, TreeSection section, ref int filter)
        {
            SettingsLine heading = null;
            for (int t = 0; t < tests.Count; t++)
            {
                TestPlace place = tests[t];
                bool here = group < 0 ? place.Group < 0 : place.Group == group && place.SectionId == section.Id;
                if (!here)
                {
                    continue;
                }
                if (filter < 0)
                {
                    filter = tab.Kinds.Count;
                    tab.Kinds.Add(new SettingsKind { IsTests = true, TestGroup = group });
                }
                if (heading == null)
                {
                    heading = new SettingsLine { Kind = LineKind.TestHeading, Tab = trouble, Section = section, Filter = filter, TestGroup = group };
                    tab.Lines.Add(heading);
                }
                tab.Lines.Add(new SettingsLine { Kind = LineKind.Test, Tab = trouble, Section = section, Row = t, Filter = filter, TestGroup = group });
            }
        }

        private static LineKind KindOf(PlanKind kind)
        {
            switch (kind)
            {
                case PlanKind.Button: return LineKind.Button;
                case PlanKind.Key: return LineKind.Key;
                default: return LineKind.Option;
            }
        }

        // ---------------------------------------------------------------- the presets

        /// <summary>
        /// How many options differ from what a preset sets, of those it covers; their indices go into
        /// <paramref name="into"/> (when it is given) in their order. The three lists are one entry per option.
        /// </summary>
        public static int Differences(IList<int> now, IList<int> preset, IList<bool> covered, List<int> into)
        {
            into?.Clear();
            int count = 0;
            int n = Math.Min(now.Count, Math.Min(preset.Count, covered.Count));
            for (int i = 0; i < n; i++)
            {
                if (covered[i] && now[i] != preset[i])
                {
                    count++;
                    into?.Add(i);
                }
            }
            return count;
        }

        /// <summary>
        /// The preset to compare the choices with: one they match exactly (it is in use), else the one they differ
        /// from in fewest options; between equals the standard one, then the first. -1 when no preset covers anything.
        /// <paramref name="counts"/> are the differences, <paramref name="covers"/> how many options each preset covers.
        /// </summary>
        public static int Closest(IList<int> counts, IList<int> covers, int standard, out bool exact)
        {
            exact = false;
            int best = -1;
            for (int p = 0; p < counts.Count && p < covers.Count; p++)
            {
                if (covers[p] <= 0)
                {
                    continue;
                }
                if (best < 0 || counts[p] < counts[best] || (counts[p] == counts[best] && p == standard && best != standard))
                {
                    best = p;
                }
            }
            exact = best >= 0 && counts[best] == 0;
            return best;
        }

        // ---------------------------------------------------------------- a number's selector

        /// <summary>
        /// The values a number's selector steps through: both ends, the value it has now and the default, and evenly
        /// spaced values between, no more than <see cref="MostSteps"/> in all where the range allows, smallest first.
        /// A small range is every whole number in it.
        /// </summary>
        public static void Steps(int min, int max, int now, int standard, List<int> into)
        {
            into.Clear();
            if (max < min)
            {
                int swap = min;
                min = max;
                max = swap;
            }
            now = Math.Max(min, Math.Min(max, now));
            standard = Math.Max(min, Math.Min(max, standard));
            long range = (long)max - min;
            int step = range < MostSteps ? 1 : (int)((range + MostSteps - 2) / (MostSteps - 1));
            for (long v = min; v <= max; v += step)
            {
                into.Add((int)v);
            }
            if (into[into.Count - 1] != max)
            {
                into.Add(max);
            }
            Insert(into, now);
            Insert(into, standard);
        }

        private static void Insert(List<int> values, int value)
        {
            int at = values.BinarySearch(value);
            if (at < 0)
            {
                values.Insert(~at, value);
            }
        }

        /// <summary>Where a value stands among the steps: its own place, else the nearest step.</summary>
        public static int StepOf(List<int> steps, int value)
        {
            int best = 0;
            for (int i = 1; i < steps.Count; i++)
            {
                if (Math.Abs((long)steps[i] - value) < Math.Abs((long)steps[best] - value))
                {
                    best = i;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- the search

        /// <summary>
        /// The lines a search finds: those whose own text, section or group has every word typed at the start of one
        /// of its words (<see cref="OptionsFind"/>, as the Mods tab's search box). Headings are never found by
        /// themselves (their rows are). The four lists are one entry per line; indices go into <paramref name="into"/>.
        /// </summary>
        public static int Search(List<string> words, IList<SettingsLine> lines, IList<string> texts, IList<string> sections, IList<string> groups, List<int> into)
        {
            into.Clear();
            if (words.Count == 0)
            {
                return 0;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].IsHeading && OptionsFind.Matches(words, texts[i], sections[i], groups[i]))
                {
                    into.Add(i);
                }
            }
            return into.Count;
        }
    }
}
