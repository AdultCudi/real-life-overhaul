using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Offline checks of the options page's structure (Meta/OptionsTree.cs, as it ships) and of its arithmetic
    /// (Meta/OptionsPlan.cs): every saved option the sources declare stands in exactly one section, every
    /// feature has a section, no section is too long or empty, what a row needs exists and leads in no circle,
    /// the search finds each row by each word of its label, its section and its group, an unplaced row is
    /// caught by "Other", and every label the page shows keeps to the length limits and uses none of the mod's
    /// own words. The option ids and label keys are read from the sources as text, and the texts from en.json.
    /// </summary>
    internal static class OptionsCheck
    {
        // Ids of Settings.cs that are not rows of the options page. A name here needs its reason beside it.
        private static readonly string[] NotOnThePage =
        {
            "car_speed",   // the old single "car speed %" slider: its saved value is dropped at load, it has no row
        };

        public static void Run(Action<string> fail, Dictionary<string, string> locale, string src)
        {
            Console.WriteLine("\n== The options page: the tree ==");
            int checks = 0;
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) fail("options: " + message);
            }
            string Text(string key) => key != null && locale.TryGetValue(key, out string t) ? t : null;

            string scripts = Path.Combine(src, "Scripts");
            var sources = new Dictionary<string, string>();
            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories)) sources[file] = File.ReadAllText(file);
            string Source(params string[] parts) => sources[Path.Combine(new[] { scripts }.Concat(parts).ToArray())];

            // ---- what the sources declare: option ids, the label of each, the test buttons, the features
            var consts = new Dictionary<string, string>(StringComparer.Ordinal);   // "Settings.XId" and "XId" -> id
            var ids = new List<string>();
            foreach (Match m in Regex.Matches(Source("Core", "Settings.cs"), @"const string (\w+Id) = ""([^""]+)"""))
            {
                consts["Settings." + m.Groups[1].Value] = m.Groups[2].Value;
                if (!NotOnThePage.Contains(m.Groups[2].Value)) ids.Add(m.Groups[2].Value);
            }
            int fromSettings = ids.Count;
            foreach (Match m in Regex.Matches(Source("Justice", "JusticeSettings.cs"), @"const string (\w+Id) = ""([^""]+)"""))
            {
                consts[m.Groups[1].Value] = m.Groups[2].Value;
                consts["JusticeSettings." + m.Groups[1].Value] = m.Groups[2].Value;
                ids.Add(m.Groups[2].Value);
            }
            foreach (Match m in Regex.Matches(Source("Home", "Roommates", "RoommateSettings.cs"), @"const string EnabledId = ""([^""]+)"""))
            {
                consts["Roommates.RoommateSettings.EnabledId"] = m.Groups[1].Value;
                consts["RoommateSettings.EnabledId"] = m.Groups[1].Value;
                ids.Add(m.Groups[1].Value);
            }
            var labelOf = new Dictionary<string, string>(StringComparer.Ordinal);   // option id -> label key
            foreach (Match m in Regex.Matches(Source("Character", "CharacterCatalogue.cs"),
                         @"new Slot\(""([^""]+)"", ""[^""]+"", ""([^""]+)""[^\n]*?(?:, ""([^""]+)"", ""([^""]+)""\))?\)?,?\s*$", RegexOptions.Multiline))
            {
                ids.Add(m.Groups[1].Value);
                labelOf[m.Groups[1].Value] = m.Groups[2].Value;
                if (m.Groups[3].Success)
                {
                    ids.Add(m.Groups[3].Value);
                    labelOf[m.Groups[3].Value] = m.Groups[4].Value;
                }
            }
            foreach (string file in sources.Keys.Where(f => f.Contains(Path.Combine("Performance", "Tweaks"))))
            {
                foreach (Match m in Regex.Matches(sources[file], @"public override string Id => ""([^""]+)"";\s*public override string LabelKey => ""([^""]+)"";"))
                {
                    ids.Add("perf_" + m.Groups[1].Value);
                    labelOf["perf_" + m.Groups[1].Value] = m.Groups[2].Value;
                }
            }
            foreach (Match m in Regex.Matches(Source("Performance", "PerfTweaks.cs"), @"new LayerCull\(""([^""]+)"", ""([^""]+)"""))
            {
                ids.Add("perf_" + m.Groups[1].Value);
                labelOf["perf_" + m.Groups[1].Value] = m.Groups[2].Value;
            }
            ids.Add("perf_far_districts");
            labelOf["perf_far_districts"] = "reallife_perf_far_districts2";
            ids.Add("perf_step_timing");
            var switches = new HashSet<string>(ids.Where(i => i.StartsWith("perf_")), StringComparer.Ordinal);   // the options that are on or off
            var buttons = new HashSet<string>(StringComparer.Ordinal);    // label keys of buttons and keys the sources put on the page
            var tests = new HashSet<string>(StringComparer.Ordinal);
            foreach (string code in sources.Values)
            {
                foreach (Match m in Regex.Matches(code, @"(Add(?:Toggle|Slider|Dropdown)|new ToggleOption)\(\s*([\w.]+)\s*,\s*""([^""]+)"""))
                {
                    if (!consts.TryGetValue(m.Groups[2].Value, out string id)) continue;
                    labelOf[id] = m.Groups[3].Value;
                    if (m.Groups[1].Value.EndsWith("Toggle") || m.Groups[1].Value.EndsWith("ToggleOption")) switches.Add(id);
                }
                foreach (Match m in Regex.Matches(code, @"(?:AddButton|new ButtonOption|new Binding)\(\s*""([^""]+)""(?:,\s*""([^""]+)"")?"))
                {
                    string key = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value;
                    buttons.Add(key);
                    if (key.Contains("_test_")) tests.Add(key);
                }
                foreach (Match m in Regex.Matches(code, @"\.Test\(((?:\s*""[^""]+"",?)+)"))
                {
                    foreach (Match k in Regex.Matches(m.Groups[1].Value, @"""([^""]+)""")) tests.Add(k.Groups[1].Value);
                }
            }
            var features = new List<string>();
            foreach (string code in sources.Values)
            {
                foreach (Match m in Regex.Matches(code, @"Features\.Add\(""([a-z_]+)"", ""reallife_")) if (!features.Contains(m.Groups[1].Value)) features.Add(m.Groups[1].Value);
            }
            var playerButtons = new HashSet<string>(StringComparer.Ordinal);   // "_test_" in the key, but named by a feature as the player's
            foreach (string code in sources.Values)
            {
                foreach (Match m in Regex.Matches(code, @"\.Rows\(""([^""]+)""\)")) playerButtons.Add(m.Groups[1].Value);
            }
            tests.ExceptWith(playerButtons);
            Console.WriteLine($"  the sources declare {ids.Count} saved options ({fromSettings} of them in Settings.cs), {buttons.Count} buttons and keys, {tests.Count} test tools, {features.Count} features");
            Check(fromSettings >= 150, "Settings.cs could not be read: only " + fromSettings + " option ids found");
            Check(ids.Distinct().Count() == ids.Count, "an option id is declared twice in the sources: " + string.Join(", ", ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key)));
            Check(tests.Count >= 250, "the test buttons could not be read: only " + tests.Count);
            Check(features.Count >= 40, "the feature list could not be read: only " + features.Count);

            // ---- the tree itself
            var sections = OptionsTree.Groups.SelectMany(g => g.Sections).ToList();
            var treeRows = sections.SelectMany(s => s.Rows).ToList();
            var optionRows = treeRows.Where(r => !r.IsButton).ToList();
            Console.WriteLine($"  the tree: {OptionsTree.Groups.Length} groups, {sections.Count} sections, {optionRows.Count} options and {treeRows.Count - optionRows.Count} buttons placed");
            Check(OptionsTree.Groups.Length >= 8 && OptionsTree.Groups.Length <= 12, "the page has " + OptionsTree.Groups.Length + " groups: a small number is wanted");
            Check(OptionsTree.Groups.Select(g => g.Id).Distinct().Count() == OptionsTree.Groups.Length, "two groups share an id");
            Check(sections.Select(s => s.Id).Distinct().Count() == sections.Count, "two sections share an id");
            Check(OptionsTree.Groups[0].Id == OptionsTree.StartId, "the first group is not 'Start here'");
            Check(OptionsTree.Groups[OptionsTree.Groups.Length - 1].Id == OptionsTree.TroubleId, "the last group is not Troubleshooting");
            Check(OptionsTree.Group(OptionsTree.StartId).IsFlat, "'Start here' is not shown without section buttons");
            foreach (string id in ids)
            {
                int places = treeRows.Count(r => !r.IsButton && r.Id == id);
                Check(places == 1, places == 0 ? "the option " + id + " is in no section of Meta/OptionsTree.cs" : "the option " + id + " is in " + places + " sections");
                Check(labelOf.ContainsKey(id) || id == "perf_step_timing", "no label found in the sources for the option " + id);
            }
            labelOf["perf_step_timing"] = "reallife_perf_step_timing";
            var known = new HashSet<string>(ids, StringComparer.Ordinal);
            foreach (TreeRow row in optionRows) Check(known.Contains(row.Id), "the tree names the option " + row.Id + ", which no source declares");
            foreach (TreeRow row in treeRows.Where(r => r.IsButton))
            {
                Check(buttons.Contains(row.Id), "the tree names the button " + row.Id + ", which no source puts on the page");
                Check(treeRows.Count(r => r.Id == row.Id) == 1, "the button " + row.Id + " is placed twice");
                Check(!tests.Contains(row.Id), "the button " + row.Id + " is a test: tests stand only under Troubleshooting");
            }
            foreach (string button in playerButtons) Check(treeRows.Any(r => r.IsButton && r.Id == button), "the player's button " + button + " is in no section");
            foreach (TreeGroup group in OptionsTree.Groups)
            {
                Check(group.Sections.Count > 0, "the group " + group.Id + " is empty");
                Check(Text(group.TitleKey) != null, "the group " + group.Id + " has no title " + group.TitleKey + " in en.json");
            }
            foreach (TreeSection section in sections)
            {
                Check(section.Rows.Count > 0, "the section " + section.Id + " is empty");
                Check(section.Rows.Count <= OptionsTree.MostRows, "the section " + section.Id + " has " + section.Rows.Count + " rows: at most " + OptionsTree.MostRows);
                Check(Text(section.TitleKey) != null, "the section " + section.Id + " has no title " + section.TitleKey + " in en.json");
                Check(section.Master == null || section.Rows[0].Id == section.Master, "the main switch of " + section.Id + " is not its first row");
                Check(section.Master == null || section.Rows.Count(r => r.Id == section.Master) == 1, "the main switch of " + section.Id + " is also listed among its rows");
                Check(section.GuideFeature == null || features.Contains(section.GuideFeature), "the section " + section.Id + " names the Guide page of " + section.GuideFeature + ", which is no feature");
                foreach (string feature in section.Features) Check(features.Contains(feature), "the section " + section.Id + " stands for " + feature + ", which is no feature");
                foreach (string need in section.Needs) Check(known.Contains(need), "the section " + section.Id + " needs " + need + ", which is no option");
                foreach (TreeRow row in section.Rows)
                {
                    foreach (string need in row.Needs)
                    {
                        Check(known.Contains(need), "the row " + row.Id + " needs " + need + ", which is no option");
                        Check(need != row.Id, "the row " + row.Id + " needs itself");
                    }
                }
            }
            foreach (string feature in features)
            {
                if (feature == OptionsTree.TestsId) continue;
                Check(sections.Count(s => s.Features.Contains(feature)) == 1, "the feature " + feature + " has " + sections.Count(s => s.Features.Contains(feature)) + " sections: exactly one must stand for it");
                Check(OptionsTree.SectionOfFeature(feature) != null, "no section is found for the feature " + feature);
            }

            // ---- what a row needs
            Console.WriteLine("\n== The options page: what a row needs ==");
            string cycle = OptionsPlan.FindCycle();
            Check(cycle == null, "options need each other in a circle: " + cycle);
            int needy = 0;
            foreach (string id in ids)
            {
                string[] needs = OptionsPlan.NeedsOf(id);
                needy += needs.Length > 0 ? 1 : 0;
                Check(!needs.Contains(id), id + " needs itself");
                foreach (string need in needs)
                {
                    Check(labelOf.TryGetValue(need, out string label) && Text(label) != null, id + " needs " + need + ", which has no label to name it by");
                    Check(switches.Contains(need), id + " needs " + need + ", which is not a switch");
                }
                Check(OptionsPlan.Blocker(id, _ => true) == null, id + " is kept from working although everything is on");
                Check(needs.Length == 0 || OptionsPlan.Blocker(id, _ => false) == needs[0], id + ": with everything off the first need is not what is named");
            }
            Console.WriteLine($"  {needy} of {ids.Count} options need another one on");
            Check(needy >= 60, "only " + needy + " options need anything: the table has lost its needs");
            Func<string, Func<string, bool>> allBut = off => id => id != off;
            Check(OptionsPlan.Blocker("economy_company", allBut("economy")) == "economy", "running a business is not kept back by the credit rating being off");
            Check(OptionsPlan.Blocker("economy_company_staff", allBut("economy_company")) == "economy_company", "staff is not kept back by its main switch");
            Check(OptionsPlan.Blocker("economy_company_staff", allBut("economy")) == "economy", "staff is not kept back by the credit rating being off");
            Check(OptionsPlan.Blocker("economy_rota", allBut("economy")) == "economy", "the rota planner is not kept back by the credit rating, two steps away");
            Check(OptionsPlan.Blocker("economy_rota", allBut("economy_company")) == "economy_company", "the rota planner is not kept back by running a business");
            Check(OptionsPlan.Blocker("economy", _ => false) == null, "the credit rating itself needs something");
            Check(OptionsPlan.Blocker("home_jog", allBut("home_life")) == "home_life", "going for a run is not kept back by home life");
            Check(OptionsPlan.Blocker("law_arrests", allBut("law_precincts")) == null, "arrests are kept back by the police stations, which they do not need");
            Check(OptionsPlan.Blocker("nav_gps", allBut("nav_minimap")) == null, "the GPS is kept back by the minimap, which it does not need");
            Check(OptionsPlan.Blocker("nav_other_route", allBut("nav_gps")) == "nav_gps", "the other mod's route is not kept back by the GPS");
            Check(OptionsPlan.Blocker("leisure_chess", allBut("city_life")) == "city_life", "street chess is not kept back by street life");
            Check(OptionsPlan.Blocker("camera_sensitivity", allBut("camera_enabled")) == "camera_enabled", "mouse look sensitivity is not kept back by the camera, two steps away");
            Check(OptionsPlan.Blocker("no_such_option", _ => false) == null, "an unknown option is kept back");
            Check(OptionsPlan.NeedsOf(null).Length == 0 && OptionsPlan.NeedsOf("no_such_option").Length == 0, "an unknown option needs something");
            Check(OptionsPlan.NeededBy("economy").Count >= 10, "few rows need the credit rating: " + OptionsPlan.NeededBy("economy").Count);
            Check(OptionsPlan.NeededBy("economy").All(d => OptionsPlan.NeedsOf(d).Contains("economy")), "NeededBy names a row that does not need it");

            // ---- placing the built rows
            Console.WriteLine("\n== The options page: placing the rows ==");
            PlanKind KindOf(string id) => switches.Contains(id) ? PlanKind.Toggle : PlanKind.Slider;
            var built = new List<PlanRow>();
            foreach (string id in ids) built.Add(new PlanRow(KindOf(id), id, labelOf[id]));
            foreach (TreeRow row in treeRows.Where(r => r.IsButton)) built.Add(new PlanRow(PlanKind.Button, null, row.Id));
            Plan plan = OptionsPlan.Place(built);
            Console.WriteLine($"  {built.Count} rows: {plan.Placed} placed in {plan.Sections.Count} sections of {plan.Groups} groups, {plan.Other.Rows.Count} in Other, {plan.Missing.Count} missing");
            Check(plan.Placed == built.Count, "not every row was placed: " + plan.Placed + " of " + built.Count);
            Check(plan.Other.Rows.Count == 0, "rows fell to Other: " + string.Join(", ", plan.Other.Rows.Select(i => built[i].Key)));
            Check(plan.Missing.Count == 0, "the tree names rows that are not there: " + string.Join(", ", plan.Missing));
            Check(plan.Sections.Count == sections.Count && plan.Groups == OptionsTree.Groups.Length, "a section or a group got no row");
            foreach (PlanSection placed in plan.Sections)
            {
                Check(placed.Rows.Select(i => built[i].Key).SequenceEqual(placed.Tree.Rows.Select(r => r.Id)), "the rows of " + placed.Tree.Id + " are not in the table's order");
            }
            var reversed = Enumerable.Reverse(built).ToList();
            Plan again = OptionsPlan.Place(reversed);
            Check(again.Placed == built.Count && again.Sections.Zip(plan.Sections, (a, b) => a.Tree == b.Tree && a.Rows.Select(i => reversed[i].Key).SequenceEqual(b.Rows.Select(i => built[i].Key))).All(x => x),
                "the order of the page depends on the order the rows were built in");
            Plan twice = OptionsPlan.Place(built);
            Check(twice.Sections.Zip(plan.Sections, (a, b) => a.Rows.SequenceEqual(b.Rows)).All(x => x), "placing the same rows twice gives two answers");
            var extra = new List<PlanRow>(built) { new PlanRow(PlanKind.Toggle, "zz_new_option", "reallife_zz_new"), new PlanRow(PlanKind.Button, null, "reallife_zz_button") };
            Plan withNew = OptionsPlan.Place(extra);
            Check(withNew.Other.Rows.Count == 2 && withNew.Placed == built.Count, "a row nothing claims was not caught by Other");
            Check(withNew.Other.Rows.Select(i => extra[i].Key).SequenceEqual(new[] { "zz_new_option", "reallife_zz_button" }), "Other does not hold the unclaimed rows in their order");
            var doubled = new List<PlanRow>(built) { built[0] };
            Check(OptionsPlan.Place(doubled).Other.Rows.Count == 1, "a row built twice was not caught by Other");
            Plan none = OptionsPlan.Place(new List<PlanRow>());
            Check(none.Placed == 0 && none.Sections.Count == 0 && none.Groups == 0 && none.Other.Rows.Count == 0 && none.Missing.Count == treeRows.Count, "an empty page is not placed as empty");
            Plan one = OptionsPlan.Place(new List<PlanRow> { new PlanRow(PlanKind.Toggle, "economy", "reallife_econ_enabled") });
            Check(one.Placed == 1 && one.Sections.Count == 1 && one.Sections[0].Tree.Id == "credit" && one.Groups == 1 && one.Find("credit") != null && one.Find("debt") == null, "a page of one row is not placed in its section");
            Plan wrongKind = OptionsPlan.Place(new List<PlanRow> { new PlanRow(PlanKind.Button, null, "economy") });
            Check(wrongKind.Placed == 0 && wrongKind.Other.Rows.Count == 1, "a button with an option's id as its label was taken for the option");

            // ---- counting switches
            var isSwitch = new List<bool> { true, true, false, true };
            var on = new List<bool> { true, false, true, true };
            Check(OptionsPlan.CountOn(isSwitch, on, new List<int> { 0, 1, 2, 3 }, out int total) == 2 && total == 3, "2 of 3 switches on is not counted so");
            Check(OptionsPlan.CountOn(isSwitch, on, new List<int>(), out total) == 0 && total == 0, "no rows do not count as none");
            Check(OptionsPlan.CountOn(isSwitch, on, new List<int> { 2 }, out total) == 0 && total == 0, "a slider was counted as a switch");
            Check(OptionsPlan.CountOn(isSwitch, on, new List<int> { 1 }, out total) == 0 && total == 1, "one switch that is off is not 0 of 1");
            Check(OptionsPlan.CountOn(isSwitch, on, new List<int> { 0, 3 }, out total) == 2 && total == 2, "all on is not counted so");
            Check(OptionsPlan.CountOn(isSwitch, on, new List<int> { -1, 9, 0 }, out total) == 1 && total == 1, "a row that is not there was counted");

            // ---- the search
            Console.WriteLine("\n== The options page: the search ==");
            var words = new List<string>();
            Check(OptionsFind.Split(null, words) == string.Empty && words.Count == 0, "nothing typed is searched for");
            Check(OptionsFind.Split("a", words) == string.Empty && words.Count == 0, "one letter is searched for");
            Check(OptionsFind.Split("  po ", words) == "po" && words.SequenceEqual(new[] { "po" }), "'  po ' is not the word po");
            Check(OptionsFind.Split("Police  Chase", words) == "police  chase" && words.SequenceEqual(new[] { "police", "chase" }), "two words with two spaces are not two words");
            Check(OptionsFind.Has("the rent is due", "rent") && OptionsFind.Has("rental cars", "rent") && OptionsFind.Has("a red-light runner", "light"), "a word is not found at the start of a word");
            Check(!OptionsFind.Has("the current rate", "rent") && !OptionsFind.Has("parent", "rent") && !OptionsFind.Has("different", "rent"), "'rent' is found inside another word");
            Check(!OptionsFind.Has(null, "rent") && !OptionsFind.Has(string.Empty, "rent") && !OptionsFind.Has("rent", string.Empty) && !OptionsFind.Has("re", "rent"), "an empty text or word finds something");
            Check(OptionsFind.Has("current rent", "rent"), "a word is not found after a false start inside another word");
            words.Clear();
            Check(!OptionsFind.Matches(words, "anything", "at", "all"), "no words match everything");

            string Lower(string key) => (Text(key) ?? string.Empty).ToLowerInvariant();
            var rowText = new Dictionary<TreeRow, string>();
            var sectionText = new Dictionary<TreeSection, string>();
            foreach (TreeSection section in sections)
            {
                sectionText[section] = Lower(section.TitleKey) + " " + section.Words;
                foreach (TreeRow row in section.Rows)
                {
                    rowText[row] = Lower(row.IsButton ? row.Id : labelOf[row.Id]) + " " + Lower(row.TipKey) + " " + row.Words;
                }
            }
            List<TreeRow> Find(string typed)
            {
                var found = new List<TreeRow>();
                OptionsFind.Split(typed, words);
                foreach (TreeSection section in sections)
                    foreach (TreeRow row in section.Rows)
                        if (OptionsFind.Matches(words, rowText[row], sectionText[section], Lower(section.Group.TitleKey))) found.Add(row);
                return found;
            }
            IEnumerable<string> WordsOf(string text) => Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9']+").Select(w => w.Trim('\'')).Where(w => w.Length >= OptionsFind.ShortestQuery);
            int searches = 0;
            foreach (TreeSection section in sections)
            {
                foreach (TreeRow row in section.Rows)
                {
                    string label = Text(row.IsButton ? row.Id : labelOf[row.Id]) ?? string.Empty;
                    foreach (string word in WordsOf(label).Concat(WordsOf(Text(section.TitleKey) ?? string.Empty)).Concat(WordsOf(Text(section.Group.TitleKey) ?? string.Empty)).Distinct())
                    {
                        searches++;
                        if (!Find(word).Contains(row)) Check(false, "'" + word + "' does not find the row " + row.Id + " (" + label + ")");
                    }
                    Check(Find(label).Contains(row), "the whole label does not find the row " + row.Id);
                }
            }
            Console.WriteLine($"  {searches} searches, one for each word of each row's label, section and group");
            List<TreeRow> rent = Find("rent");
            Console.WriteLine("  'rent' finds: " + string.Join("; ", rent.Select(r => r.Id + " in " + Text(OptionsTree.SectionOf(r.Id).Group.TitleKey))));
            Check(rent.Any(r => r.Id == "home_roommates"), "'rent' does not find the roommate");
            Check(rent.Any(r => r.Id == "economy_products"), "'rent' does not find the mortgage");
            Check(rent.Any(r => r.Id == "economy_estate"), "'rent' does not find property");
            Check(rent.Count <= 8, "'rent' finds " + rent.Count + " rows: it is found inside other words");
            Check(rent.Select(r => OptionsTree.SectionOf(r.Id).Group.Id).Distinct().Count() >= 3, "'rent' is not found in three groups");
            Check(Find("fps").Count > 0 && Find("fps").All(r => OptionsTree.SectionOf(r.Id).Group.Id == "performance" || OptionsTree.SectionOf(r.Id).Group.Id == OptionsTree.TroubleId), "'fps' does not find the performance rows");
            Check(Find("police chase").Any(r => r.Id == "police_pursuit"), "'police chase' does not find the pursuit");
            Check(Find("taxi").Any(r => r.Id == "transit_taxi_call") && Find("cab").Any(r => r.Id == "transit_taxi_call"), "'taxi' and 'cab' do not both find the taxi");
            Check(Find("getting around").Count == OptionsTree.Group("around").Sections.Sum(s => s.Rows.Count), "a group's name does not find every row of the group, and only those");
            Check(Find("qqzzx").Count == 0, "nonsense finds something");
            Check(Find("x").Count == 0, "one letter finds something");

            // ---- the words on the page
            Console.WriteLine("\n== The options page: the words ==");
            void Wording(string key, string what, int mostWords, int mostChars)
            {
                string text = Text(key);
                Check(!string.IsNullOrEmpty(text), what + " " + key + " is not in en.json");
                if (string.IsNullOrEmpty(text)) return;
                Check(OptionsWording.WordCount(text) <= mostWords, what + " " + key + " has " + OptionsWording.WordCount(text) + " words (at most " + mostWords + "): " + text);
                Check(text.Length <= mostChars, what + " " + key + " has " + text.Length + " characters (at most " + mostChars + "): " + text);
                Check(OptionsWording.FirstBanned(text) == null, what + " " + key + " uses '" + OptionsWording.FirstBanned(text) + "': " + text);
            }
            foreach (TreeGroup group in OptionsTree.Groups) Wording(group.TitleKey, "the group title", OptionsWording.TitleWords, OptionsWording.TitleChars);
            foreach (TreeSection section in sections) Wording(section.TitleKey, "the section title", OptionsWording.TitleWords, OptionsWording.TitleChars);
            foreach (TreeRow row in treeRows)
            {
                string key = row.IsButton ? row.Id : labelOf[row.Id];
                Wording(key, "the label", OptionsWording.LabelWords, OptionsWording.LabelChars);
                Check(!(Text(key) ?? string.Empty).EndsWith("."), "the label " + key + " ends in a full stop");
                if (!row.IsButton)
                {
                    Wording(row.TipKey, "the sentence", OptionsWording.TipWords, OptionsWording.TipChars);
                    Check((Text(row.TipKey) ?? ".").EndsWith("."), "the sentence " + row.TipKey + " does not end in a full stop");
                }
            }
            foreach (string test in tests.Concat(new[] { "reallife_opt_test_layout" }))
            {
                Wording(test, "the test button", OptionsWording.TestWords, OptionsWording.TestChars);
                Check(!(Text(test) ?? string.Empty).StartsWith("Test", StringComparison.OrdinalIgnoreCase), "the test button " + test + " still begins with 'Test'");
                Check(char.IsUpper((Text(test) ?? "X")[0]) || char.IsDigit((Text(test) ?? "X")[0]), "the test button " + test + " does not begin with a capital");
            }
            foreach (string key in new[] { "reallife_opt_about", "reallife_meta_title_tests", "reallife_meta_title_other" }) Wording(key, "the label", OptionsWording.LabelWords, OptionsWording.LabelChars);
            foreach (string key in new[] { "reallife_opt_count", "reallife_opt_needs", "reallife_opt_text", "reallife_opt_ok", "reallife_opt_ask_preset", "reallife_opt_test_layout_done" })
            {
                Check(Text(key) != null, "the text " + key + " is not in en.json");
            }
            Check((Text("reallife_opt_count") ?? string.Empty).Contains("{on}") && (Text("reallife_opt_count") ?? string.Empty).Contains("{total}"), "the count has lost its numbers");
            Check((Text("reallife_opt_needs") ?? string.Empty).Contains("{option}"), "the reason has lost its option");
            Check((Text("reallife_opt_ask_preset") ?? string.Empty).Contains("{count}") && (Text("reallife_opt_ask_preset") ?? string.Empty).Contains("{about}"), "the preset question has lost its count");
            Check(OptionsWording.WordCount(null) == 0 && OptionsWording.WordCount("  ") == 0 && OptionsWording.WordCount("one") == 1 && OptionsWording.WordCount(" two  words ") == 2, "words are miscounted");
            Check(OptionsWording.FirstBanned("The traffic director") == "director" && OptionsWording.FirstBanned("A fast finish (visible: yes)") != null, "a banned word is not seen");
            Check(OptionsWording.FirstBanned("Carpool lanes and tiered seats") == null && OptionsWording.FirstBanned(null) == null && OptionsWording.FirstBanned("Cambridge") == null, "a banned word is seen inside another word");
            Check(OptionsWording.FirstBanned("Far props (visible: they appear at 200 m)") == "(visible", "the '(visible' tail is not seen");

            // ---- every options header a source adds is registered by a feature's Group (else the set-up check calls its group Auto: no guide page, presets or clean-up)
            var headers = new SortedSet<string>(StringComparer.Ordinal);
            var groups = new HashSet<string>(StringComparer.Ordinal);
            foreach (string code in sources.Values)
            {
                foreach (Match m in Regex.Matches(code, @"AddHeader\(""([a-z_0-9]+)""\)")) headers.Add(m.Groups[1].Value);
                foreach (Match m in Regex.Matches(code, @"\.Group\(""([a-z_0-9]+)""\)")) groups.Add(m.Groups[1].Value);
            }
            headers.Remove("reallife_meta_header");   // (the mod's own page title, written by OptionsLayout.Emit: not a group of rows)
            Check(headers.Count >= 40 && groups.Count >= 40, "the options headers could not be read: " + headers.Count + " headers, " + groups.Count + " groups");
            foreach (string header in headers) Check(groups.Contains(header), "the options header " + header + " is registered by no feature's .Group(...) in Meta/FeatureList.cs");

            // ---- how long it takes
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++) OptionsPlan.Place(built);
            double placeMs = watch.Elapsed.TotalMilliseconds / 200.0;
            watch.Restart();
            for (int i = 0; i < 200; i++) Find("police chase");
            double findMs = watch.Elapsed.TotalMilliseconds / 200.0;
            watch.Restart();
            for (int i = 0; i < 200; i++) foreach (string id in ids) OptionsPlan.Blocker(id, _ => true);
            double blockMs = watch.Elapsed.TotalMilliseconds / 200.0;
            Console.WriteLine($"  placing {built.Count} rows {placeMs:0.000} ms; one search over every row {findMs:0.000} ms; every row's needs {blockMs:0.000} ms");
            Check(placeMs < 2.0, "placing the rows takes " + placeMs.ToString("0.00") + " ms");
            Check(findMs < 2.0, "a search takes " + findMs.ToString("0.00") + " ms");
            Check(blockMs < 2.0, "looking at every row's needs takes " + blockMs.ToString("0.00") + " ms");
            Console.WriteLine($"  {checks} checks of the options page");
            if (checks < 60) fail("options: only " + checks + " checks ran");

            SettingsAppCheck.Run(fail, locale, src, built, tests, labelOf);
        }
    }
}
