using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Meta
{
    /// <summary>What a line of the Settings app is.</summary>
    public enum LineKind
    {
        Heading,      // a section's name: a page of the list on the left
        Option,       // a saved option: a switch, a choice or a number
        Button,       // a button of the player's (a preset, "Save my choices to a file", a page to open)
        Key,          // one of the mod's keys: press it, then the new key
        TestHeading,  // a section's test tools: a page of the list's last category, while the test tools are shown
        Test          // one test tool
    }

    /// <summary>What a line is drawn with on its page.</summary>
    public enum ControlKind
    {
        None,         // a heading
        Switch,       // an option that is on or off
        Slider,       // a number, between the option's own ends, in whole numbers
        Choice,       // one of a few values: the game's drop-down
        Action,       // a button of the player's or a test tool: it does something, it holds no value
        Key           // one of the mod's keys: press it, then the new key
    }

    /// <summary>One page of the Settings app: a section, or a section's test tools, with its lines; one entry of the list on the left.</summary>
    public sealed class SettingsPage
    {
        public readonly int Index;           // its place in the list of every page
        public readonly int Category;        // the group it is listed under: a tab, or the test tools after the last one
        public readonly SettingsLine Heading;
        public readonly List<SettingsLine> Lines = new List<SettingsLine>();

        internal SettingsPage(int index, int category, SettingsLine heading)
        {
            Index = index;
            Category = category;
            Heading = heading;
        }

        public bool IsTests => Heading.Kind == LineKind.TestHeading;
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
    /// The arithmetic of the Settings app: each tab's lines from the Mods tab's own plan, the pages of the list on its left,
    /// the control each line is drawn with, a number's slider, the presets against the choices and the search. No Unity
    /// types, so CatalogueCheck runs it as it ships; nothing per frame.
    /// </summary>
    public static class SettingsRules
    {
        public const string SettingsButtonKey = "reallife_meta_settings_button";   // the Mods tab's button that opens the app: on none of the app's own pages
        public const int MostFound = 60;   // rows a search shows at once: a word in most rows would be a wall, and more letters narrow it

        /// <summary>One tab per group, each section a heading over its rows as placed; rows placed nowhere ("Other") and the test tools go on Troubleshooting.</summary>
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

            // The test tools: a group's in one filter kind, a heading per section; those of a feature no section stands for last.
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

        /// <summary>How many of the options a preset covers differ from it; their indices go into <paramref name="into"/> when given. One entry per option in each list.</summary>
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

        /// <summary>The preset the choices match, else the one they differ from least (a tie to <paramref name="standard"/>, then the first); -1 when none covers anything.</summary>
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

        /// <summary>
        /// The pages of the app, as the list on its left names them: one per section heading and one per heading of a
        /// section's test tools, each with the lines under it in the page's order. The button that opens this very app is
        /// on no page. A tab's own pages come first and in its order; the test tools' pages are of the category after the
        /// last group (<see cref="TestsCategory"/>).
        /// </summary>
        public static List<SettingsPage> Pages(SettingsTab[] tabs, IList<PlanRow> rows)
        {
            var pages = new List<SettingsPage>();
            int testsCategory = TestsCategory(tabs);
            for (int t = 0; t < tabs.Length; t++)
            {
                SettingsPage page = null;
                foreach (SettingsLine line in tabs[t].Lines)
                {
                    if (line.IsHeading)
                    {
                        page = new SettingsPage(pages.Count, line.Kind == LineKind.TestHeading ? testsCategory : t, line);
                        pages.Add(page);
                        continue;
                    }
                    if (page == null || !Shows(line, rows))
                    {
                        continue;
                    }
                    page.Lines.Add(line);
                }
            }
            return pages;
        }

        /// <summary>The category of the list that holds the test tools' pages: the one after the last group.</summary>
        public static int TestsCategory(SettingsTab[] tabs)
        {
            return tabs.Length;
        }

        /// <summary>Whether a line is on a page of the app: every line but the button that opens the app itself.</summary>
        public static bool Shows(SettingsLine line, IList<PlanRow> rows)
        {
            if (line.Kind == LineKind.Test || line.IsHeading)
            {
                return true;
            }
            return line.Row < 0 || line.Row >= rows.Count || rows[line.Row].LabelKey != SettingsButtonKey;
        }

        /// <summary>The control a line is drawn with: a switch for on and off, a slider for a number, the game's drop-down for a choice, a button for an action, the key itself for a key.</summary>
        public static ControlKind ControlOf(LineKind line, PlanKind kind)
        {
            switch (line)
            {
                case LineKind.Option:
                    switch (kind)
                    {
                        case PlanKind.Toggle: return ControlKind.Switch;
                        case PlanKind.Slider: return ControlKind.Slider;
                        case PlanKind.Choice: return ControlKind.Choice;
                        default: return ControlKind.None;
                    }
                case LineKind.Button:
                case LineKind.Test:
                    return ControlKind.Action;
                case LineKind.Key:
                    return ControlKind.Key;
                default:
                    return ControlKind.None;
            }
        }

        /// <summary>
        /// A number's slider: the option's own ends (the lower first), whole numbers only, as the game's slider on the Mods
        /// tab has it (ModOptionsSliderControl: wholeNumbers, Min, Max), starting at the value now kept inside them.
        /// </summary>
        public static int SliderStart(int min, int max, int value, out int low, out int high)
        {
            low = Math.Min(min, max);
            high = Math.Max(min, max);
            return Math.Max(low, Math.Min(high, value));
        }

        /// <summary>Where a value dragged to stands: the nearest whole number inside the ends, as the slider snaps to it.</summary>
        public static int Snap(float dragged, int low, int high)
        {
            double near = Math.Round(dragged, MidpointRounding.AwayFromZero);
            return (int)Math.Max(low, Math.Min(high, near));
        }

        /// <summary>The lines whose text, section or group has every word typed (<see cref="OptionsFind"/>); a heading is found through its rows. One entry per line in each list.</summary>
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
