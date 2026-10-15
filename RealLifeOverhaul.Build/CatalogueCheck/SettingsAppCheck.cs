using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks of the Settings app (Meta/SettingsRules.cs, as it ships): the tree as the app's tabs (every
    /// option, button and key once, on its group's tab under its section's heading, in the page's order; the test tools
    /// only on Troubleshooting, by group and section; rows nothing claims under "Other"), the presets compared with the
    /// choices made, the values a number's selector steps through, the search over every tab (in English and in
    /// Chinese), and the app's texts: a tab label for each group, short enough for ten side by side.
    /// </summary>
    internal static class SettingsAppCheck
    {
        public static void Run(Action<string> fail, Dictionary<string, string> locale, string src, List<PlanRow> built, ICollection<string> testKeys, Dictionary<string, string> labelOf)
        {
            Console.WriteLine("\n== The Settings app: the tabs ==");
            int checks = 0;
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) fail("settings app: " + message);
            }
            string Text(string key) => key != null && locale.TryGetValue(key, out string t) ? t : null;

            TreeGroup[] groups = OptionsTree.Groups;
            var tests = new List<TestPlace>();
            List<string> testList = testKeys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            // Test tools placed as the page places them: by the group and section of the feature that has them. Here each
            // goes to a section in turn, three of them to a feature no section stands for, so that every path is taken.
            var sections = groups.SelectMany(g => g.Sections).ToList();
            for (int i = 0; i < testList.Count; i++)
            {
                if (i % 97 == 5)
                {
                    tests.Add(new TestPlace(-1, null, testList[i]));
                    continue;
                }
                TreeSection section = sections[(i * 7) % sections.Count];
                tests.Add(new TestPlace(Array.IndexOf(groups, section.Group), section.Id, testList[i]));
            }

            Plan plan = OptionsPlan.Place(built);
            SettingsTab[] tabs = SettingsRules.Tabs(plan, built, tests);
            Console.WriteLine($"  {tabs.Length} tabs, {tabs.Sum(t => t.Lines.Count)} lines: {tabs.Sum(t => t.Lines.Count(l => l.Kind == LineKind.Heading))} headings, "
                              + $"{tabs.Sum(t => t.Lines.Count(l => l.Kind == LineKind.Option))} options, {tabs.Sum(t => t.Lines.Count(l => l.Kind == LineKind.Button))} buttons, "
                              + $"{tabs.Sum(t => t.Lines.Count(l => l.Kind == LineKind.Test))} test tools");
            Check(tabs.Length == groups.Length, "the app has " + tabs.Length + " tabs for " + groups.Length + " groups");
            Check(tabs[0].Group.Id == OptionsTree.StartId, "the first tab is not Start here");
            Check(tabs[tabs.Length - 1].Group.Id == OptionsTree.TroubleId, "the last tab is not Troubleshooting");
            for (int t = 0; t < tabs.Length; t++)
            {
                SettingsTab tab = tabs[t];
                Check(tab.Group == groups[t], "tab " + t + " is not the group " + groups[t].Id);
                Check(tab.Lines.Count > 0 && tab.Lines[0].IsHeading, "the tab " + tab.Group.Id + " does not begin with a heading");
                Check(tab.Lines.All(l => l.Tab == t), "a line of the tab " + tab.Group.Id + " says it is on another tab");
                // Each heading once, in the tree's order, and every row under its own section's heading.
                var headings = tab.Lines.Where(l => l.Kind == LineKind.Heading && l.Section != null).Select(l => l.Section).ToList();
                var treeOrder = tab.Group.Sections.Where(s => plan.Find(s.Id) != null).ToList();
                Check(headings.SequenceEqual(treeOrder), "the headings of " + tab.Group.Id + " are not its sections in the tree's order");
                SettingsLine heading = null;
                foreach (SettingsLine line in tab.Lines)
                {
                    if (line.IsHeading)
                    {
                        heading = line;
                        Check(line.Row == -1, "a heading of " + tab.Group.Id + " stands for a row");
                        continue;
                    }
                    Check(heading != null && line.Section == heading.Section && line.Filter == heading.Filter, "a line of " + tab.Group.Id + " is not under its section's heading");
                    Check(line.Filter >= 0 && line.Filter < tab.Kinds.Count, "a line of " + tab.Group.Id + " has no filter kind");
                    if (line.Kind != LineKind.Test)
                    {
                        Check(line.Row >= 0 && line.Row < built.Count, "a line of " + tab.Group.Id + " stands for no row");
                    }
                }
                for (int k = 0; k < tab.Kinds.Count; k++)
                {
                    Check(tab.Lines.Count(l => l.IsHeading && l.Filter == k) >= 1, "the filter kind " + k + " of " + tab.Group.Id + " has no heading");
                    SettingsKind kind = tab.Kinds[k];
                    Check(kind.IsTests || tab.Lines.Count(l => l.Kind == LineKind.Heading && l.Filter == k) == 1, "the section kind " + k + " of " + tab.Group.Id + " has more than one heading");
                    Check(kind.IsTests || tab.Lines.First(l => l.Kind == LineKind.Heading && l.Filter == k).Section == kind.Section, "the kind " + k + " of " + tab.Group.Id + " is not its heading's section");
                }
            }

            // Every row of the page once, on its own group's tab, in its section, in the page's order.
            var lines = tabs.SelectMany(t => t.Lines).ToList();
            for (int i = 0; i < built.Count; i++)
            {
                List<SettingsLine> places = lines.Where(l => l.Kind != LineKind.Test && l.Row == i).ToList();
                Check(places.Count == 1, "the row " + built[i].Key + " is on " + places.Count + " lines of the app");
                if (places.Count != 1) continue;
                SettingsLine line = places[0];
                TreeSection section = OptionsTree.SectionOf(built[i].Key);
                Check(section != null && line.Section == section, "the row " + built[i].Key + " is not in its section " + (section != null ? section.Id : "(none)"));
                Check(section != null && groups[line.Tab] == section.Group, "the row " + built[i].Key + " is not on its group's tab");
                LineKind wanted = built[i].Kind == PlanKind.Button ? LineKind.Button : built[i].Kind == PlanKind.Key ? LineKind.Key : LineKind.Option;
                Check(line.Kind == wanted, "the row " + built[i].Key + " is shown as a " + line.Kind + ", not a " + wanted);
            }
            foreach (PlanSection placed in plan.Sections)
            {
                List<int> inApp = lines.Where(l => l.Kind != LineKind.Test && !l.IsHeading && l.Section == placed.Tree).Select(l => l.Row).ToList();
                Check(inApp.SequenceEqual(placed.Rows), "the rows of " + placed.Tree.Id + " are not in the Mods tab's order in the app");
            }
            int optionLines = lines.Count(l => l.Kind == LineKind.Option);
            Check(optionLines == built.Count(r => r.Kind != PlanKind.Button && r.Kind != PlanKind.Key), "the app has " + optionLines + " option lines for " + built.Count(r => r.Kind != PlanKind.Button && r.Kind != PlanKind.Key) + " options");

            // The test tools: on Troubleshooting only, each once, a group's under one kind, a heading per section, orphans last.
            int trouble = tabs.Length - 1;
            List<SettingsLine> testLines = lines.Where(l => l.Kind == LineKind.Test).ToList();
            Check(testLines.Count == tests.Count, "the app has " + testLines.Count + " test lines for " + tests.Count + " test tools");
            Check(testLines.All(l => l.Tab == trouble), "a test tool is not on the Troubleshooting tab");
            Check(Enumerable.Range(0, tests.Count).All(i => testLines.Count(l => l.Row == i) == 1), "a test tool is on two lines, or on none");
            Check(testLines.All(l => l.TestGroup == tests[l.Row].Group && (l.Section == null ? tests[l.Row].SectionId == null : l.Section.Id == tests[l.Row].SectionId)), "a test tool is under the wrong group or section");
            List<SettingsLine> troubleLines = tabs[trouble].Lines;
            int firstTest = troubleLines.FindIndex(l => l.Kind == LineKind.TestHeading);
            Check(firstTest > 0 && troubleLines.Skip(firstTest).All(l => l.Kind == LineKind.Test || l.Kind == LineKind.TestHeading), "the test tools are not after Troubleshooting's own sections");
            List<int> order = troubleLines.Where(l => l.Kind == LineKind.TestHeading).Select(l => l.TestGroup < 0 ? int.MaxValue : l.TestGroup * 1000 + groups[l.TestGroup].Sections.IndexOf(l.Section)).ToList();
            Check(order.SequenceEqual(order.OrderBy(x => x)), "the test tools are not in the order of the groups and their sections, with the others last");
            Check(order.Distinct().Count() == order.Count, "a section's test tools have two headings");
            var testKinds = tabs[trouble].Kinds.Where(k => k.IsTests).ToList();
            Check(testKinds.Select(k => k.TestGroup).Distinct().Count() == testKinds.Count, "a group's test tools are under two filter kinds");
            Check(testKinds.Count == tests.Select(p => p.Group).Distinct().Count(), "the test tools have " + testKinds.Count + " filter kinds for " + tests.Select(p => p.Group).Distinct().Count() + " groups");
            Check(tabs.Take(trouble).All(t => t.Lines.All(l => l.Kind != LineKind.Test && l.Kind != LineKind.TestHeading)), "a test tool is on a tab other than Troubleshooting");

            // A row nothing claims is under "Other" on Troubleshooting; no test tools, no test headings.
            var extra = new List<PlanRow>(built) { new PlanRow(PlanKind.Toggle, "zz_new_option", "reallife_zz_new") };
            SettingsTab[] withNew = SettingsRules.Tabs(OptionsPlan.Place(extra), extra, new List<TestPlace>());
            SettingsLine stray = withNew.SelectMany(t => t.Lines).FirstOrDefault(l => l.Row == extra.Count - 1 && l.Kind == LineKind.Option);
            Check(stray != null && stray.Tab == trouble && stray.Section == null, "a row nothing claims is not under Other on the Troubleshooting tab");
            Check(withNew[trouble].Lines.Count(l => l.Kind == LineKind.Heading && l.Section == null) == 1, "Other has no heading of its own");
            Check(withNew.All(t => t.Lines.All(l => l.Kind != LineKind.TestHeading)), "with no test tools there is a heading for them");
            SettingsTab[] empty = SettingsRules.Tabs(OptionsPlan.Place(new List<PlanRow>()), new List<PlanRow>(), new List<TestPlace>());
            Check(empty.Length == groups.Length && empty.All(t => t.Lines.Count == 0 && t.Kinds.Count == 0), "an empty page does not give empty tabs");
            SettingsTab[] again = SettingsRules.Tabs(plan, built, tests);
            Check(again.Zip(tabs, (a, b) => a.Lines.Count == b.Lines.Count && a.Lines.Zip(b.Lines, (x, y) => x.Kind == y.Kind && x.Row == y.Row && x.Section == y.Section && x.Filter == y.Filter).All(z => z)).All(z => z),
                "the tabs made twice from the same page differ");

            // ---- the presets compared
            Console.WriteLine("\n== The Settings app: the presets compared ==");
            var into = new List<int>();
            int[] now = { 1, 0, 5, 3, 1 };
            Check(SettingsRules.Differences(now, new[] { 1, 0, 5, 3, 1 }, new[] { true, true, true, true, true }, into) == 0 && into.Count == 0, "the same values differ");
            Check(SettingsRules.Differences(now, new[] { 0, 0, 7, 3, 1 }, new[] { true, true, true, true, true }, into) == 2 && into.SequenceEqual(new[] { 0, 2 }), "two values that differ are not found, in their order");
            Check(SettingsRules.Differences(now, new[] { 0, 1, 7, 9, 0 }, new[] { false, false, false, false, false }, into) == 0 && into.Count == 0, "an option no preset covers was counted");
            Check(SettingsRules.Differences(now, new[] { 0, 1, 7, 9, 0 }, new[] { true, false, true, false, false }, null) == 2, "only the covered options are compared (and no list is needed)");
            Check(SettingsRules.Differences(now, new[] { 0 }, new[] { true, true }, into) == 1 && into.SequenceEqual(new[] { 0 }), "lists of different lengths are compared past the shortest");
            Check(SettingsRules.Differences(new int[0], new int[0], new bool[0], into) == 0, "no options differ in something");
            int Closest(int[] counts, int[] covers, out bool exact) => SettingsRules.Closest(counts, covers, 1, out exact);   // 1: Standard, as Meta/Features.cs numbers the presets
            Check(Closest(new[] { 3, 0, 5, 9 }, new[] { 40, 40, 40, 40 }, out bool exact) == 1 && exact, "the preset in use is not found");
            Check(Closest(new[] { 3, 4, 5, 9 }, new[] { 40, 40, 40, 40 }, out exact) == 0 && !exact, "the nearest preset is not the one with fewest differences");
            Check(Closest(new[] { 2, 2, 5, 2 }, new[] { 40, 40, 40, 40 }, out exact) == 1 && !exact, "between equals the standard preset is not chosen");
            Check(Closest(new[] { 5, 9, 2, 2 }, new[] { 40, 40, 40, 40 }, out exact) == 2, "between equals that are not the standard the first is not chosen");
            Check(Closest(new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0 }, out exact) == -1 && !exact, "a preset that covers nothing is in use");
            Check(Closest(new[] { 0, 3, 0, 0 }, new[] { 0, 40, 0, 0 }, out exact) == 1 && !exact, "a preset that covers nothing was compared");
            Check(Closest(new int[0], new int[0], out exact) == -1, "no presets give one");

            // ---- a number's selector
            Console.WriteLine("\n== The Settings app: a number's steps ==");
            var steps = new List<int>();
            SettingsRules.Steps(1, 10, 4, 5, steps);
            Check(steps.SequenceEqual(Enumerable.Range(1, 10)), "a small range is not every whole number in it: " + string.Join(",", steps));
            int worst = 0;
            foreach (int[] c in new[] { new[] { 70, 150, 100, 100 }, new[] { -50, 50, 7, 0 }, new[] { 0, 100, 33, 50 }, new[] { 100, 300, 101, 100 }, new[] { 1, 20, 20, 5 }, new[] { 0, 1000, 999, 1 }, new[] { 5, 70, 5, 30 } })
            {
                SettingsRules.Steps(c[0], c[1], c[2], c[3], steps);
                worst = Math.Max(worst, steps.Count);
                Check(steps[0] == c[0] && steps[steps.Count - 1] == c[1], "the steps of " + c[0] + ".." + c[1] + " do not reach both ends");
                Check(steps.Contains(c[2]) && steps.Contains(c[3]), "the steps of " + c[0] + ".." + c[1] + " leave out the value now or the default");
                Check(steps.SequenceEqual(steps.Distinct().OrderBy(x => x)), "the steps of " + c[0] + ".." + c[1] + " are not distinct and in order");
                Check(steps.Count <= SettingsRules.MostSteps + 3, "the range " + c[0] + ".." + c[1] + " has " + steps.Count + " steps");
                Check(steps[SettingsRules.StepOf(steps, c[2])] == c[2], "the value now is not where the selector starts");
            }
            SettingsRules.Steps(10, 1, 50, -3, steps);
            Check(steps[0] == 1 && steps[steps.Count - 1] == 10 && steps.Contains(10) && steps.Contains(1), "a range given the wrong way round, or a value outside it, is not kept inside");
            SettingsRules.Steps(4, 4, 4, 4, steps);
            Check(steps.SequenceEqual(new[] { 4 }), "a range of one value is not that value");
            SettingsRules.Steps(0, 100, 50, 50, steps);
            Check(SettingsRules.StepOf(steps, 51) == steps.IndexOf(50) || Math.Abs(steps[SettingsRules.StepOf(steps, 51)] - 51) <= 1, "a value between steps does not go to the nearest");
            Console.WriteLine($"  at most {worst} steps for the mod's ranges (wanted at most {SettingsRules.MostSteps + 3})");

            // ---- the search
            Console.WriteLine("\n== The Settings app: the search ==");
            string Lower(string key) => (Text(key) ?? string.Empty).ToLowerInvariant();
            string RowText(SettingsLine line)
            {
                if (line.IsHeading) return string.Empty;
                if (line.Kind == LineKind.Test) return Lower(tests[line.Row].LabelKey);
                PlanRow row = built[line.Row];
                TreeRow tree = OptionsTree.Row(row.Key);
                string label = row.Kind == PlanKind.Button ? row.LabelKey : labelOf.TryGetValue(row.Key, out string l) ? l : row.LabelKey;
                return Lower(label) + " " + Lower(tree != null ? tree.TipKey : null) + " " + (tree != null ? tree.Words : null);
            }
            var texts = lines.Select(RowText).ToList();
            var sectionTexts = lines.Select(l => l.Section != null ? Lower(l.Section.TitleKey) + " " + l.Section.Words : string.Empty).ToList();
            var groupTexts = lines.Select(l => Lower(groups[l.Tab].TitleKey)).ToList();
            var words = new List<string>();
            var found = new List<int>();
            List<SettingsLine> Find(string typed)
            {
                OptionsFind.Split(typed, words);
                SettingsRules.Search(words, lines, texts, sectionTexts, groupTexts, found);
                return found.Select(i => lines[i]).ToList();
            }
            string IdOf(SettingsLine l) => l.Kind == LineKind.Test ? tests[l.Row].LabelKey : built[l.Row].Key;
            List<SettingsLine> rent = Find("rent");
            Check(rent.Any(l => IdOf(l) == "home_roommates") && rent.Any(l => IdOf(l) == "economy_estate"), "'rent' does not find the roommate and property in the app");
            Check(rent.All(l => !l.IsHeading), "a heading was found by itself");
            Check(rent.Select(l => l.Tab).Distinct().Count() >= 3, "'rent' is not found on three tabs");
            Check(Find("police chase").Any(l => IdOf(l) == "police_pursuit"), "'police chase' does not find the pursuit in the app");
            Check(Find("interiors").Any(l => IdOf(l) == "interiors_prices_strength"), "'interiors' does not find the interiors' sliders");
            Check(Find("qqzzx").Count == 0 && Find("x").Count == 0 && Find(null).Count == 0, "nonsense, one letter or nothing finds something");
            Check(Find("getting around").Where(l => l.Kind != LineKind.Test).SequenceEqual(lines.Where(l => !l.IsHeading && l.Kind != LineKind.Test && groups[l.Tab].Id == "around")),
                "a group's name does not find every row of its tab, and only those");
            int everyOption = 0;
            foreach (SettingsLine line in lines.Where(l => l.Kind == LineKind.Option))
            {
                string label = Text(labelOf.TryGetValue(built[line.Row].Key, out string k) ? k : null) ?? string.Empty;
                if (label.Length >= OptionsFind.ShortestQuery && Find(label).Contains(line)) everyOption++;
            }
            Check(everyOption == optionLines, "the whole label finds " + everyOption + " of " + optionLines + " options in the app");
            Console.WriteLine($"  every option is found by its whole label ({everyOption} of {optionLines}); 'rent' finds {rent.Count} rows on {rent.Select(l => l.Tab).Distinct().Count()} tabs");

            // Chinese has no spaces between words: a word typed is found inside a phrase.
            Check(OptionsFind.Has("你的角色与住所", "角色") && OptionsFind.Has("写实预设", "预设"), "a Chinese word inside a phrase is not found");
            Check(!OptionsFind.Has("你的角色与住所", "城市") && !OptionsFind.Has("the current rate", "rent"), "the Chinese rule finds what is not there, or 'rent' inside another word");
            // Cyrillic and accented Latin words begin where a word does, as English ones (the texts and what is typed are lowered alike).
            Check(OptionsFind.Has("ваш персонаж и жильё", OptionsFind.Split("Персо", words)) && OptionsFind.Has("mieteinnahmen und ärger", OptionsFind.Split("Ärger", words))
                  && OptionsFind.Has("réglages de l'économie", OptionsFind.Split("Écon", words)), "a Russian, German or French word typed is not found at the start of a word");
            Check(!OptionsFind.Has("городской транспорт", OptionsFind.Split("род", words)) && !OptionsFind.Has("verkehrsunfall", OptionsFind.Split("unfall", words)),
                "a Russian or German word typed is found inside another word");
            var zh = new Dictionary<string, string>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(src, "Locales", "zh-cn.json"))))
            {
                foreach (JsonProperty p in doc.RootElement.EnumerateObject()) zh[p.Name] = p.Value.GetString();
            }
            string Zh(string key) => key != null && zh.TryGetValue(key, out string t) ? t.ToLowerInvariant() : string.Empty;
            var zhTexts = lines.Select(l => l.IsHeading || l.Kind == LineKind.Test ? string.Empty
                : Zh(built[l.Row].Kind == PlanKind.Button ? built[l.Row].LabelKey : labelOf.TryGetValue(built[l.Row].Key, out string lk) ? lk : built[l.Row].LabelKey)).ToList();
            var zhSections = lines.Select(l => l.Section != null ? Zh(l.Section.TitleKey) : string.Empty).ToList();
            var zhGroups = lines.Select(l => Zh(groups[l.Tab].TitleKey)).ToList();
            string familyWord = Zh(OptionsTree.Group("you").Sections.First(s => s.Id == "family").TitleKey);
            OptionsFind.Split(familyWord.Length >= 2 ? familyWord.Substring(0, 2) : familyWord, words);
            SettingsRules.Search(words, lines, zhTexts, zhSections, zhGroups, found);
            Check(found.Count > 0 && found.Select(i => lines[i]).Any(l => l.Section != null && l.Section.Id == "family"), "the Chinese name of a section ('" + familyWord + "') does not find its rows");

            // ---- the app's texts
            Console.WriteLine("\n== The Settings app: the words ==");
            foreach (TreeGroup group in groups)
            {
                string tab = Text("reallife_set_tab_" + group.Id);
                Check(!string.IsNullOrEmpty(tab), "the group " + group.Id + " has no tab label reallife_set_tab_" + group.Id);
                Check(tab == null || tab.Length <= SettingsRules.TabChars, "the tab label of " + group.Id + " has " + (tab ?? string.Empty).Length + " characters (at most " + SettingsRules.TabChars + "): " + tab);
                Check(tab == null || OptionsWording.WordCount(tab) == 1, "the tab label of " + group.Id + " is more than one word: " + tab);
                Check(OptionsWording.FirstBanned(tab) == null, "the tab label of " + group.Id + " uses '" + OptionsWording.FirstBanned(tab) + "'");
            }
            foreach (string key in new[] { "reallife_set_app", "reallife_set_link", "reallife_set_search", "reallife_set_back", "reallife_set_list_differ", "reallife_set_type_number", "reallife_set_to_default", "reallife_set_switch_on", "reallife_set_guide_page", "reallife_set_search_again" })
            {
                string text = Text(key);
                Check(!string.IsNullOrEmpty(text) && text.Length <= 24 && OptionsWording.FirstBanned(text) == null, "the label " + key + " is missing, longer than 24 characters or uses one of the mod's own words: " + text);
            }
            foreach (string key in new[] { "reallife_set_headline", "reallife_set_count" })
            {
                Check((Text(key) ?? string.Empty).Contains("{on}") && (Text(key) ?? string.Empty).Contains("{total}"), "the text " + key + " has lost its numbers");
            }
            Check((Text("reallife_set_headline_differ") ?? string.Empty).Contains("{preset}") && (Text("reallife_set_headline_differ") ?? string.Empty).Contains("{count}"), "the differences headline has lost its preset or count");
            Check((Text("reallife_set_needs") ?? string.Empty).Contains("{option}") && (Text("reallife_set_box_needs_line") ?? string.Empty).Contains("{option}"), "what an option needs has lost its option");
            Check((Text("reallife_set_headline_found") ?? string.Empty).Contains("{query}"), "the search's headline has lost what was looked for");
            string start = Text("reallife_guide_start") ?? string.Empty;
            Check(start.Contains("# The " + (Text("reallife_set_app") ?? "?") + " app"), "the Guide's Getting started page has no section on the app by its name");
            Check(start.Contains("'" + (Text("reallife_meta_settings_button") ?? "?") + "'"), "the Guide's section on the app does not name the Mods tab's button as it is");
            Check(start.Contains("'" + (Text("reallife_meta_show_tests") ?? "?") + "'") && start.Contains("'" + (Text("reallife_set_search") ?? "?") + "'"), "the Guide's section on the app does not name 'Show test tools' and 'Search' as they are");

            // ---- how long it takes
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) SettingsRules.Tabs(plan, built, tests);
            double tabsMs = watch.Elapsed.TotalMilliseconds / 200.0;
            watch.Restart();
            for (int i = 0; i < 200; i++) Find("police chase");
            double findMs = watch.Elapsed.TotalMilliseconds / 200.0;
            Console.WriteLine($"  the tabs from {built.Count} rows and {tests.Count} test tools {tabsMs:0.000} ms; one search over {lines.Count} lines {findMs:0.000} ms");
            Check(tabsMs < 2.0, "making the tabs takes " + tabsMs.ToString("0.00") + " ms");
            Check(findMs < 2.0, "a search over every line takes " + findMs.ToString("0.00") + " ms");
            Console.WriteLine($"  {checks} checks of the Settings app");
            if (checks < 200) fail("settings app: only " + checks + " checks ran");
        }
    }
}
