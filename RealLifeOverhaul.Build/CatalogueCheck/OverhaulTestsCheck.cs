using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Meta;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The living economy's test tools reached from the Settings app's Tests category, placed as the game places them
    /// (Meta/OptionsLayout.AddTests): a button with "_test_" in its key is the test of the feature that names it, else of
    /// the header it stands under; a feature's tests go under the first section that stands for it; a test named by a
    /// feature but never put on the page has nothing to run and is left out. Every test of the overhaul's features, and
    /// every overhaul test button, must be named by one of those features, have something to run, and stand on a page of
    /// the Tests category. Read from the sources as they ship.
    /// </summary>
    internal static class OverhaulTestsCheck
    {
        // The overhaul's features (their Features.Add ids) and the key prefixes of its test buttons.
        private static readonly string[] Features = { "ui", "living", "population", "population_demand", "studio", "firms", "flare_city" };
        private static readonly string[] Prefixes =
        {
            "reallife_ui_test_", "reallife_session_test_", "reallife_studio_test_", "reallife_tech_mkt_test_", "reallife_pop_test_", "reallife_flare_city_test_"
        };
        private const string Growth = "reallife_grow_test_";   // the growth's tests, which are the construction game's

        public static int Run(Action<string> fail, string src, List<PlanRow> built)
        {
            Console.WriteLine("\n== The Settings app: the living economy's test tools ==");
            int checks = 0;
            void Check(bool ok, string message)
            {
                checks++;
                if (!ok) fail("settings app: " + message);
            }

            string scripts = Path.Combine(src, "Scripts");
            var sources = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).ToList();
            // Each feature's tests in the order its statement names them, and whether each brings its own run.
            var testsOf = new Dictionary<string, List<(string Key, bool Runs)>>(StringComparer.Ordinal);
            var onPage = new HashSet<string>(StringComparer.Ordinal);
            foreach (string code in sources)
            {
                foreach (Match m in Regex.Matches(code, @"AddButton\(\s*""([^""]+)""")) onPage.Add(m.Groups[1].Value);
                foreach (Match m in Regex.Matches(code, @"Features\.Add\(""([a-z_0-9]+)"""))
                {
                    string chain = Statement(code, m.Index);
                    if (!testsOf.TryGetValue(m.Groups[1].Value, out List<(string, bool)> list))
                    {
                        testsOf[m.Groups[1].Value] = list = new List<(string, bool)>();
                    }
                    foreach (Match t in Regex.Matches(chain, @"\.(Test|TestFirst|Check)\(\s*((?:""[^""]+""\s*,?\s*)+)(\)?)"))
                    {
                        bool namesOnly = t.Groups[1].Value == "Test" && t.Groups[3].Value == ")";
                        foreach (Match k in Regex.Matches(t.Groups[2].Value, @"""([^""]+)"""))
                        {
                            list.Add((k.Groups[1].Value, !namesOnly));
                            if (!namesOnly) break;   // a test with its run names one key; the strings after are its log line
                        }
                    }
                }
            }
            string OwnerOf(string key) => testsOf.FirstOrDefault(f => f.Value.Any(t => t.Key == key)).Key;
            bool Runs(string key) => onPage.Contains(key) || testsOf.Values.Any(l => l.Any(t => t.Key == key && t.Runs));

            // The tests placed as OptionsLayout.AddTests places them: by group, section and feature, each feature once.
            var places = new List<TestPlace>();
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            for (int g = 0; g < OptionsTree.Groups.Length; g++)
            {
                foreach (TreeSection section in OptionsTree.Groups[g].Sections)
                {
                    foreach (string feature in section.Features)
                    {
                        if (!testsOf.TryGetValue(feature, out List<(string Key, bool Runs)> tests) || !claimed.Add(feature)) continue;
                        foreach ((string key, bool _) in tests.Where(t => Runs(t.Key)))
                        {
                            places.Add(new TestPlace(g, section.Id, key));
                        }
                    }
                }
            }
            SettingsTab[] tabs = SettingsRules.Tabs(OptionsPlan.Place(built), built, places);
            List<SettingsPage> pages = SettingsRules.Pages(tabs, built);
            int category = SettingsRules.TestsCategory(tabs);
            var reached = new Dictionary<string, string>(StringComparer.Ordinal);   // test key -> the page that has it
            foreach (SettingsPage page in pages.Where(p => p.Category == category))
            {
                foreach (SettingsLine line in page.Lines.Where(l => l.Kind == LineKind.Test))
                {
                    reached[places[line.Row].LabelKey] = page.Heading.Section != null ? page.Heading.Section.Id : "other";
                }
            }

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string feature in Features)
            {
                Check(testsOf.ContainsKey(feature), "the overhaul's feature " + feature + " is not registered (Features.Add)");
                Check(OptionsTree.SectionOfFeature(feature) != null, "no section of Meta/OptionsTree.cs stands for the overhaul's feature " + feature + ", so its tests are under Other");
                if (testsOf.TryGetValue(feature, out List<(string Key, bool Runs)> tests))
                {
                    foreach ((string key, bool _) in tests) keys.Add(key);
                }
            }
            foreach (string key in onPage.Where(k => Prefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal)))) keys.Add(key);
            foreach (List<(string Key, bool Runs)> tests in testsOf.Values)
            {
                foreach ((string key, bool _) in tests.Where(t => t.Key.StartsWith(Growth, StringComparison.Ordinal))) keys.Add(key);
            }
            Check(keys.Count >= 30, "the overhaul's test tools could not be read: only " + keys.Count);
            foreach (string key in keys)
            {
                string owner = OwnerOf(key);
                Check(owner != null, "the test " + key + " is named by no feature, so it falls to whichever header it stands under");
                Check(key.StartsWith(Growth, StringComparison.Ordinal) ? owner == "construction" : owner != null && Features.Contains(owner),
                    "the test " + key + " is named by " + (owner ?? "nothing") + ", not by its own feature");
                Check(Runs(key), "the test " + key + " is named by " + (owner ?? "nothing") + " but is never put on the page, so it has nothing to run and is left out");
                Check(reached.ContainsKey(key), "the test " + key + " is on no page of the Settings app's Tests category");
            }
            Console.WriteLine("  " + keys.Count + " test tools of the living economy, " + keys.Count(reached.ContainsKey) + " of them reached from the Tests category, on "
                              + keys.Where(reached.ContainsKey).Select(k => reached[k]).Distinct().Count() + " pages");
            return checks;
        }

        // One statement from the given place to its semicolon, the brackets and the strings in it skipped.
        private static string Statement(string code, int from)
        {
            int depth = 0;
            for (int i = from; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '"')
                {
                    for (i++; i < code.Length && code[i] != '"'; i++)
                    {
                        if (code[i] == '\\') i++;
                    }
                    continue;
                }
                if (c == '(' || c == '{' || c == '[') depth++;
                else if (c == ')' || c == '}' || c == ']') depth--;
                else if (c == ';' && depth <= 0) return code.Substring(from, i - from);
            }
            return code.Substring(from);
        }
    }
}
