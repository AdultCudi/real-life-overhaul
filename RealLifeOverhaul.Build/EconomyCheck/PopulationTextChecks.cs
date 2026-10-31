using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Population;

// The population's words and sources: every key its screens and notices name in en, pt and zh-cn, the notices within 240 characters, the Guide page's
// facts all filled by the feature's entry, What's new in all eleven languages, and the code read as text for the rules of the overhaul (nothing per
// frame, the option checked, the narrow change to the game's calculators standing down to the game's own, the save's one line, the shared part).
internal static partial class Program
{
    private static void PopulationTextChecks()
    {
        var keys = new List<string>
        {
            "reallife_meta_title_population", "reallife_guide_population_about", "reallife_guide_population", "reallife_pop_test_generate", "reallife_pop_test_month",
            "reallife_pop_test_household", "reallife_pop_note_generating", "reallife_pop_note_month", "reallife_pop_note_household", "reallife_pop_note_off",
            "reallife_pop_note_not_ready", "reallife_pop_note_guest"
        };
        string views = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Population", "PopulationViews.cs"));
        foreach (Match m in Regex.Matches(views, "\"(reallife_pop_[a-z_]+)\""))
        {
            if (!m.Groups[1].Value.EndsWith("_", StringComparison.Ordinal) && !keys.Contains(m.Groups[1].Value))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string prefix in new[] { "reallife_pop_status_", "reallife_pop_education_", "reallife_pop_field_", "reallife_pop_employer_" })
        {
            Type kind = prefix.Contains("status") ? typeof(Status) : prefix.Contains("education") ? typeof(Education) : prefix.Contains("field") ? typeof(Field) : typeof(EmployerKind);
            foreach (string name in Enum.GetNames(kind))
            {
                keys.Add(prefix + name.ToLowerInvariant());
            }
        }
        Check(AllIn(keys.ToArray(), out string missing) && keys.Count > 100, "population texts: every key its screens, notices and tests name in en, pt and zh-cn (" + keys.Count + "; " + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool fits = keys.Where(k => k.StartsWith("reallife_pop_note_", StringComparison.Ordinal)).All(k => Fits(en, k));
        Check(fits, "population texts: every notice is 240 characters or fewer");
        string page = en.TryGetValue("reallife_guide_population", out string p) ? p : string.Empty;
        string guide = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Population", "PopulationGuide.cs"));
        bool facts = true;
        foreach (Match m in Regex.Matches(page, @"\{([a-z_]+)\}"))
        {
            facts &= guide.Contains(".Fact(\"" + m.Groups[1].Value + "\"");
        }
        Check(facts && page.Contains("{pop_desk}") && page.Contains("# Going Public") && !page.Contains("<b>"), "population texts: the Guide page's every figure is filled from the code, and it names no bold text the Guide check would look up");
        Check(WhatsNewHas(en, "The city now has real people") && WhatsNewHas(LocaleFile("pt"), "moradores de verdade") && WhatsNewHas(LocaleFile("zh-cn"), "城市现在有了真实的居民")
              && WhatsNewHas(LocaleFile("de"), "echte Einwohner") && WhatsNewHas(LocaleFile("fr"), "vrais habitants") && WhatsNewHas(LocaleFile("es"), "habitantes de verdad")
              && WhatsNewHas(LocaleFile("ru"), "настоящие люди") && WhatsNewHas(LocaleFile("nl"), "echte inwoners") && WhatsNewHas(LocaleFile("tr"), "gerçek insanlar")
              && WhatsNewHas(LocaleFile("pl"), "prawdziwych mieszkańców") && WhatsNewHas(LocaleFile("ko"), "진짜 주민"),
            "population texts: What's new tells of the city's people in all eleven languages");

        string folder = System.IO.Path.Combine(ModScripts(), "Population");
        string all = string.Concat(Directory.GetFiles(folder, "*.cs").Select(File.ReadAllText));
        Check(!Regex.IsMatch(all, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate|OnGUI)\s*\(|HarmonyLib|\[Harmony|FindObjects|GameObject\.Find\(|\bLinq\b"),
            "population sources: no Update, no Harmony, no search of the scene and no LINQ in the population's code");
        string director = File.ReadAllText(System.IO.Path.Combine(folder, "PopulationDirector.cs"));
        Check(director.Contains("Settings.LivingPopulation") && director.Contains("StartSeats.Population") && director.Contains("DaySpread.Shared.Add(") && director.Contains("SaveHook.Keep(")
              && director.Contains("SharedCity.Follows(PopulationShare.Part)") && director.Contains("PopulationCustomers.Stop()"),
            "population sources: the director reads the master option, waits for its seat, spreads the day over frames, saves its line and follows a host");
        string calculators = File.ReadAllText(System.IO.Path.Combine(folder, "PopulationGame.Calculators.cs"));
        Check(calculators.Contains("base.GetCustomersByHour(hour, day, Scaled(") && calculators.Contains("if (!PopulationCustomers.On || reg == null || CountGuard.Off)") && Regex.Matches(calculators, @"return Guard\.Failed\(CountGuard, fault, 0\);").Count == 5 && calculators.Contains("return initial;")
              && Regex.Matches(calculators, @"private sealed class \w+Calculator : CustomerEntriesCalculator\w*").Count == 5,
            "population sources: the game's five calculators each ask the game's own hour with the population's scale behind the guard, and stand down to the game's own");
        string contract = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Core", "GameContract.cs"));
        Check(Regex.Matches(contract, @"StaticField\(PopulationCount, Game, Factory,").Count == 5 && contract.Contains("Factory = \"AI.Customers.CustomerEntries.CustomerEntriesCalculatorFactory\""),
            "population sources: the factory's five fields are lines of the game contract");
        string store = File.ReadAllText(System.IO.Path.Combine(folder, "PopulationStore.cs"));
        Check(store.Contains("public const string Key = \"reallife:population\"") && store.Contains("DistrictSaveZip") && store.Contains("zip.Finish(Head)"),
            "population sources: one line of the save, compressed in pieces on the save's workers as the district save's");
        string links = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Wiring", "Wiring.PopulationLinks.cs"));
        Check(links.Contains("CityBridges.DistrictIncome = DistrictIncome") && links.Contains("DemandBridges.PopulationPull = Pull") && links.Contains("PopulationBridges.UseOf = UseOf") && links.Contains("CustomerScale.Of = Scale")
              && links.Contains("BusinessGame.Levers = Levers") && links.Contains("PopulationAttention.Register("),
            "population sources: the city economy's districts, Uptown's demand, the copies of the customer arithmetic and the business levers are wired to it");
        string outlook = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Wiring", "Wiring.OutlookLinks.cs"));
        Check(outlook.Contains("Population.CityPopulation.Running && cycle == (int)CityEconomy.State"), "population sources: the Economy app's jobs are the measured market's where the population runs");
        foreach (string copy in new[] { "Economy/Company/RotaGame.Read.cs", "Economy/Dealer/DealerGame.cs", "Economy/OwnBank/Branch/BranchGame.cs", "Economy/Brands/BrandsGame.cs",
                                        "Economy/City/CityGame.cs", "Outlook/OutlookGame.cs" })
        {
            Check(File.ReadAllText(System.IO.Path.Combine(ModScripts(), copy)).Contains("CustomerScale."), "population sources: " + copy + " reads the population's scale for its copy of the game's customers");
        }
    }

    private static string ModScripts()
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
    }
}
