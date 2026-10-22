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
    /// Offline checks of the Settings app (Meta/SettingsRules.cs, as it ships): the tree as the app's groups (every
    /// option, button and key once, in its group under its section's heading, in the page's order; the test tools only
    /// under Troubleshooting, by group and section; rows nothing claims under "Other"), the pages of the list on the left
    /// (every option on exactly one page, with a control of its kind: a switch, a slider with the option's own ends in
    /// whole numbers, the game's drop-down, a button, a key), the values saved under the keys they always were, the presets
    /// compared with the choices made, the search over every page (in English and in Chinese), and the app's texts.
    /// </summary>
    internal static class SettingsAppCheck
    {
        public static void Run(Action<string> fail, Dictionary<string, string> locale, string src, List<PlanRow> built, ICollection<string> testKeys, Dictionary<string, string> labelOf,
            Dictionary<string, (int Min, int Max)> sliders)
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
            // The notices' own switches, each a switch of the app on its feature's tab and under its feature's heading.
            var noticeTabs = new Dictionary<string, (string Group, string Section)>
            {
                { "home_morning_news", ("you", "needs") }, { "standing_request_notes", ("you", "standing") }, { "street_protest_notes", ("city", "events") },
                { "careers_own_offer_notes", ("work", "ownwork") }, { "tech_offer_notes", ("business", "techsector") }, { "economy_grade_up_notes", ("money", "credit") }
            };
            foreach (KeyValuePair<string, (string Group, string Section)> notice in noticeTabs)
            {
                int row = built.FindIndex(r => r.Key == notice.Key);
                SettingsLine line = row >= 0 ? lines.FirstOrDefault(l => l.Kind == LineKind.Option && l.Row == row) : null;
                Check(line != null && built[row].Kind == PlanKind.Toggle && groups[line.Tab].Id == notice.Value.Group && line.Section != null && line.Section.Id == notice.Value.Section,
                    "the switch " + notice.Key + " is not a switch on the " + notice.Value.Group + " tab under " + notice.Value.Section);
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

            // ---- the pages of the list on the left, and the control of each row
            Console.WriteLine("\n== The Settings app: the pages and their controls ==");
            List<SettingsPage> pages = SettingsRules.Pages(tabs, built);
            int testsCategory = SettingsRules.TestsCategory(tabs);
            Console.WriteLine($"  {pages.Count} pages: {pages.Count(p => !p.IsTests)} sections and {pages.Count(p => p.IsTests)} pages of test tools");
            Check(pages.Count == lines.Count(l => l.IsHeading), "the app has " + pages.Count + " pages for " + lines.Count(l => l.IsHeading) + " headings");
            Check(pages.Select((p, i) => p.Index == i).All(x => x), "a page's number is not its place in the list");
            Check(pages.All(p => p.IsTests ? p.Category == testsCategory : p.Category == p.Heading.Tab), "a page is listed under a category not its group's (or the test tools' last one)");
            Check(pages.All(p => p.Lines.All(l => !l.IsHeading)), "a heading is a row of a page");
            Check(pages.All(p => p.IsTests == p.Lines.All(l => l.Kind == LineKind.Test) || p.Lines.Count == 0), "a page of test tools has another row, or a section's page a test tool");
            List<int> categoryOrder = pages.Select(p => p.Category).ToList();
            Check(categoryOrder.SequenceEqual(categoryOrder.OrderBy(c => c)), "the pages are not in the order of their categories: the groups in the tree's order, then the test tools");
            var onPages = pages.SelectMany(p => p.Lines).ToList();
            int settingsButton = built.FindIndex(r => r.LabelKey == SettingsRules.SettingsButtonKey);
            Check(settingsButton >= 0 && !onPages.Any(l => l.Kind != LineKind.Test && l.Row == settingsButton), "the button that opens the app is on a page of the app itself");
            var wantedControl = new Dictionary<PlanKind, ControlKind>
            {
                { PlanKind.Toggle, ControlKind.Switch }, { PlanKind.Slider, ControlKind.Slider }, { PlanKind.Choice, ControlKind.Choice }, { PlanKind.Button, ControlKind.Action }, { PlanKind.Key, ControlKind.Key }
            };
            int[] byControl = new int[6];
            for (int i = 0; i < built.Count; i++)
            {
                if (i == settingsButton) continue;
                List<SettingsLine> places = onPages.Where(l => l.Kind != LineKind.Test && l.Row == i).ToList();
                Check(places.Count == 1, "the row " + built[i].Key + " is on " + places.Count + " pages of the app (exactly one is wanted)");
                if (places.Count != 1) continue;
                ControlKind control = SettingsRules.ControlOf(places[0].Kind, built[i].Kind);
                byControl[(int)control]++;
                Check(control == wantedControl[built[i].Kind], "the row " + built[i].Key + " (a " + built[i].Kind + ") is drawn with a " + control + ", not a " + wantedControl[built[i].Kind]);
                SettingsPage page = pages.First(p => p.Lines.Contains(places[0]));
                Check(!page.IsTests && page.Heading.Section == OptionsTree.SectionOf(built[i].Key), "the row " + built[i].Key + " is not on its own section's page");
            }
            Check(Enumerable.Range(0, tests.Count).All(t => onPages.Count(l => l.Kind == LineKind.Test && l.Row == t) == 1), "a test tool is on no page, or on two");
            Check(onPages.Where(l => l.Kind == LineKind.Test).All(l => SettingsRules.ControlOf(l.Kind, PlanKind.Button) == ControlKind.Action), "a test tool is not a button");
            foreach (TreeRow row in OptionsTree.Groups.SelectMany(g => g.Sections).SelectMany(s => s.Rows).Where(r => !r.IsButton))
            {
                int at = built.FindIndex(r => r.Key == row.Id);
                Check(at >= 0 && onPages.Count(l => l.Kind == LineKind.Option && l.Row == at) == 1, "the tree's option " + row.Id + " is not on exactly one page of the app as an option");
            }
            Check(SettingsRules.ControlOf(LineKind.Heading, PlanKind.Toggle) == ControlKind.None && SettingsRules.ControlOf(LineKind.TestHeading, PlanKind.Button) == ControlKind.None,
                "a heading is drawn with a control");
            Check(SettingsRules.ControlOf(LineKind.Option, PlanKind.Button) == ControlKind.None && SettingsRules.ControlOf(LineKind.Option, PlanKind.Key) == ControlKind.None,
                "an option that is a button or a key is drawn with a control of a value");
            Console.WriteLine($"  {byControl[(int)ControlKind.Switch]} switches, {byControl[(int)ControlKind.Slider]} sliders, {byControl[(int)ControlKind.Choice]} choices, "
                              + $"{byControl[(int)ControlKind.Action]} buttons and {byControl[(int)ControlKind.Key]} keys, each on one page");
            Check(byControl[(int)ControlKind.Switch] >= 150 && byControl[(int)ControlKind.Slider] >= 40 && byControl[(int)ControlKind.Choice] >= 25 && byControl[(int)ControlKind.Key] >= 8,
                "fewer switches, sliders, choices or keys on the pages than the mod has");
            SettingsPage[] emptyPages = SettingsRules.Pages(empty, new List<PlanRow>()).ToArray();
            Check(emptyPages.Length == 0, "an empty page gives pages");

            // ---- a number's slider: the option's own ends and whole numbers
            Console.WriteLine("\n== The Settings app: the sliders ==");
            int sliderRows = 0;
            foreach (KeyValuePair<string, (int Min, int Max)> slider in sliders)
            {
                int row = built.FindIndex(r => r.Key == slider.Key);
                Check(row >= 0 && built[row].Kind == PlanKind.Slider, "the slider " + slider.Key + " is not a slider row of the page");
                if (row < 0) continue;
                sliderRows++;
                (int min, int max) = slider.Value;
                Check(min < max, "the slider " + slider.Key + " has the ends " + min + " and " + max);
                int middle = SettingsRules.SliderStart(min, max, (min + max) / 2, out int low, out int high);
                Check(low == min && high == max && middle == (min + max) / 2, "the slider " + slider.Key + " is not drawn between its own ends " + min + " and " + max);
                Check(SettingsRules.SliderStart(min, max, max + 7, out _, out _) == max && SettingsRules.SliderStart(min, max, min - 7, out _, out _) == min, "the slider " + slider.Key + " starts outside its ends");
                bool whole = true;
                for (float dragged = min; dragged <= max; dragged += 0.25f)
                {
                    int snapped = SettingsRules.Snap(dragged, low, high);
                    whole &= snapped >= min && snapped <= max && Math.Abs(snapped - dragged) <= 0.5f;
                }
                Check(whole, "the slider " + slider.Key + " does not snap to its whole numbers between " + min + " and " + max);
                Check(SettingsRules.Snap(max + 3.6f, low, high) == max && SettingsRules.Snap(min - 3.6f, low, high) == min, "the slider " + slider.Key + " snaps past its ends");
            }
            Check(sliderRows == built.Count(r => r.Kind == PlanKind.Slider), "the page has " + built.Count(r => r.Kind == PlanKind.Slider) + " sliders and " + sliderRows + " of them have their ends read");
            Check(SettingsRules.SliderStart(10, 1, 50, out int lo, out int hi) == 10 && lo == 1 && hi == 10, "a slider given its ends the wrong way round is not drawn from the lower one");
            Check(SettingsRules.Snap(4.5f, 0, 10) == 5 && SettingsRules.Snap(4.49f, 0, 10) == 4 && SettingsRules.Snap(-0.4f, -50, 50) == 0, "a slider does not snap to the nearest whole number");
            string view = File.ReadAllText(Path.Combine(src, "Scripts", "Meta", "SettingsView.Rows.cs"));
            Check(view.Contains("row.Slider.wholeNumbers = true;") && view.Contains("row.Slider.minValue = slot.Low;") && view.Contains("row.Slider.maxValue = slot.High;")
                  && view.Contains("SettingsRules.SliderStart(min, max, OptionsLayout.AppValue(line), out slot.Low, out slot.High)") && view.Contains("OptionsLayout.AppRange(line, out int min, out int max)"),
                "the app's slider is not set to the option's own ends (OptionsLayout.AppRange) in whole numbers");
            Check(view.Contains("SettingsRules.Snap(dragged, slot.Low, slot.High)"), "a value dragged to is not snapped to the slider's whole numbers before it is saved");
            Check(view.Contains("case ControlKind.Switch: return OptionRowKind.Switch;") && view.Contains("case ControlKind.Slider: return OptionRowKind.Slider;")
                  && view.Contains("case ControlKind.Choice: return OptionRowKind.Choice;") && view.Contains("ControlKind kind = OptionsLayout.AppControl(line);"),
                "the app does not draw each line with the game's control of its kind (SettingsRules.ControlOf through OptionsLayout.AppControl)");
            Console.WriteLine($"  {sliderRows} sliders, each between the option's own ends in whole numbers");

            // ---- the values saved where they always were
            Console.WriteLine("\n== The Settings app: the values it saves ==");
            string scripts = Path.Combine(src, "Scripts");
            string book = File.ReadAllText(Path.Combine(scripts, "Meta", "OptionsLayout.Book.cs"));
            string settingsCode = File.ReadAllText(Path.Combine(scripts, "Core", "Settings.cs"));
            Check(book.Contains("string key = row != null && row.Id != null ? Settings.PrefsKey(row.Id) : null;") && book.Contains("UnityEngine.PlayerPrefs.SetInt(key, value);")
                  && book.Contains("string key = Settings.PrefsKey(row.Id);"),
                "the app no longer reads and writes an option under the options page's own key (Settings.PrefsKey of its id)");
            Check(settingsCode.Contains("public static string PrefsKey(string id) => _modId == null ? null : Key(_modId, id);")
                  && System.Text.RegularExpressions.Regex.IsMatch(settingsCode, @"static string Key\(string modId, string id\)\s*=>\s*""m:"" \+ modId \+ "":"" \+ id;"),
                "the key an option is saved under is no longer the game's own m:<mod>:<id> (ModOptionPrefs), so the values saved before would be lost");
            foreach (string file in Directory.GetFiles(Path.Combine(scripts, "Meta"), "SettingsView*.cs"))
            {
                string code = File.ReadAllText(file);
                Check(!code.Contains("PlayerPrefs.Set") && !code.Contains("PlayerPrefs.Delete") && !code.Contains("SettingsTable.Write"),
                    Path.GetFileName(file) + " writes a value by a way of its own: every value goes through OptionsLayout.AppSet, as the Mods tab saves it");
            }
            Check(view.Contains("OptionsLayout.AppSet(line, value)"), "the app's rows do not save through OptionsLayout.AppSet");
            Check(built.Where(r => r.Kind != PlanKind.Button && r.Kind != PlanKind.Key).All(r => onPages.Any(l => l.Kind == LineKind.Option && built[l.Row].Id == r.Id)),
                "an option's line in the app stands for another id than the one it is saved under");

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
                string title = Text(group.TitleKey);
                Check(!string.IsNullOrEmpty(title) && OptionsWording.FirstBanned(title) == null, "the group " + group.Id + " has no name in the list, or one with the mod's own words: " + title);
            }
            Check(!string.IsNullOrEmpty(Text("reallife_meta_title_tests")), "the test tools' category has no name");
            foreach (string key in new[] { "reallife_set_app", "reallife_set_link", "reallife_set_search", "reallife_set_list_differ", "reallife_opt_about" })
            {
                string text = Text(key);
                Check(!string.IsNullOrEmpty(text) && text.Length <= 24 && OptionsWording.FirstBanned(text) == null, "the label " + key + " is missing, longer than 24 characters or uses one of the mod's own words: " + text);
            }
            var named = new Dictionary<string, string[]>
            {
                { "reallife_set_count", new[] { "{on}", "{total}" } }, { "reallife_set_tools", new[] { "{count}" } }, { "reallife_set_headline_differ", new[] { "{preset}", "{count}" } },
                { "reallife_set_headline_in_use", new[] { "{preset}" } }, { "reallife_set_box_needs_line", new[] { "{option}" } }, { "reallife_set_headline_found", new[] { "{count}", "{query}" } },
                { "reallife_set_default_is", new[] { "{value}" } }, { "reallife_set_preset_sets", new[] { "{preset}", "{value}" } }, { "reallife_set_differ_elsewhere", new[] { "{option}", "{now}" } },
                { "reallife_set_differ_note", new[] { "{preset}" } }, { "reallife_set_found_more", new[] { "{count}" } }, { "reallife_set_apply_preset", new[] { "{preset}" } },
                { "reallife_set_box_same", new[] { "{preset}" } }, { "reallife_set_box_differ", new[] { "{preset}", "{count}" } }, { "reallife_set_in_use", new string[0] },
                { "reallife_set_differ_count", new[] { "{count}" } }, { "reallife_set_reload_note", new string[0] }, { "reallife_set_keys_note", new string[0] },
                { "reallife_set_box_tests", new string[0] }, { "reallife_set_box_same_page", new string[0] }, { "reallife_set_search_hint", new string[0] },
                { "reallife_set_empty_found", new string[0] }, { "reallife_set_box_found_none", new string[0] }, { "reallife_set_app_title", new string[0] }
            };
            foreach (KeyValuePair<string, string[]> text in named)
            {
                string said = Text(text.Key);
                Check(!string.IsNullOrEmpty(said) && text.Value.All(n => said.Contains(n)), "the app's text " + text.Key + " is missing or has lost " + string.Join(" ", text.Value));
            }
            string start = Text("reallife_guide_start") ?? string.Empty;
            Check(start.Contains("# The " + (Text("reallife_set_app") ?? "?") + " app"), "the Guide's Getting started page has no section on the app by its name");
            Check(start.Contains("'" + (Text("reallife_meta_settings_button") ?? "?") + "'"), "the Guide's section on the app does not name the Mods tab's button as it is");
            Check(start.Contains("'" + (Text("reallife_meta_show_tests") ?? "?") + "'") && start.Contains("'" + (Text("reallife_set_search") ?? "?") + "'"), "the Guide's section on the app does not name 'Show test tools' and 'Search' as they are");
            Check(start.Contains("'" + (Text("reallife_opt_sec_presets") ?? "?") + "'") && start.Contains("(F1)"), "the Guide's section on the app does not name 'Presets', or the game's help it looks like");

            // ---- how long it takes
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) SettingsRules.Pages(SettingsRules.Tabs(plan, built, tests), built);
            double tabsMs = watch.Elapsed.TotalMilliseconds / 200.0;
            watch.Restart();
            for (int i = 0; i < 200; i++) Find("police chase");
            double findMs = watch.Elapsed.TotalMilliseconds / 200.0;
            Console.WriteLine($"  the groups and pages from {built.Count} rows and {tests.Count} test tools {tabsMs:0.000} ms; one search over {lines.Count} lines {findMs:0.000} ms");
            Check(tabsMs < 2.0, "making the groups and pages takes " + tabsMs.ToString("0.00") + " ms");
            Check(findMs < 2.0, "a search over every line takes " + findMs.ToString("0.00") + " ms");
            Console.WriteLine($"  {checks} checks of the Settings app");
            if (checks < 200) fail("settings app: only " + checks + " checks ran");
        }
    }
}
