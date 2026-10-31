using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Population;

// What the people bring the mod's own businesses, in words and sources: every key its screens, test, notices, Guide page and pointer name in en, pt and
// zh-cn, the notices within 240 characters, the Guide page's facts all filled by its entry, What's new in all eleven languages, and the code read as text
// for the overhaul's rules (nothing per frame, no Harmony, the master option read by every bridge, each business wired to the population's demand).
internal static partial class Program
{
    private static void PopDemandTextChecks()
    {
        var keys = new List<string>
        {
            "reallife_pop_test_demand", "reallife_pop_dem_note_settling", "reallife_pop_dem_note_shown", "reallife_pop_dem_pointer", "reallife_meta_title_population_demand",
            "reallife_guide_population_demand_about", "reallife_guide_population_demand"
        };
        string views = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Population", "Demand", "PopDemandViews.cs"));
        foreach (Match m in Regex.Matches(views, "\"(reallife_pop_[a-z_]+)\""))
        {
            if (!keys.Contains(m.Groups[1].Value))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        Check(AllIn(keys.ToArray(), out string missing) && keys.Count > 70, "population demand texts: every key its screens, test, notices, Guide page and pointer name in en, pt and zh-cn (" + keys.Count + "; " + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        Check(Fits(en, "reallife_pop_dem_note_settling") && Fits(en, "reallife_pop_dem_note_shown"), "population demand texts: every notice is 240 characters or fewer");
        string page = en.TryGetValue("reallife_guide_population_demand", out string p) ? p : string.Empty;
        string guide = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Population", "Demand", "PopDemandGuide.cs"));
        bool facts = true;
        int count = 0;
        foreach (Match m in Regex.Matches(page, @"\{([a-z_]+)\}"))
        {
            facts &= guide.Contains(".Fact(\"" + m.Groups[1].Value + "\"");
            count++;
        }
        Check(facts && count >= 10 && page.Contains("# Going Public") && !page.Contains("<b>"),
            "population demand texts: the Guide page's every figure is filled from the code (" + count + "), and it names no bold text the Guide check would look up");
        Check(WhatsNewHas(en, "take their customers from the city's people") && WhatsNewHas(LocaleFile("pt"), "tiram seus clientes dos moradores da cidade")
              && WhatsNewHas(LocaleFile("zh-cn"), "都从城市居民中获得顾客") && WhatsNewHas(LocaleFile("de"), "ihre Kunden aus den Einwohnern der Stadt")
              && WhatsNewHas(LocaleFile("fr"), "trouvent leurs clients parmi les habitants de la ville") && WhatsNewHas(LocaleFile("es"), "sacan sus clientes de los habitantes de la ciudad")
              && WhatsNewHas(LocaleFile("ru"), "получают клиентов из жителей города") && WhatsNewHas(LocaleFile("nl"), "hun klanten uit de inwoners van de stad")
              && WhatsNewHas(LocaleFile("tr"), "müşterilerini şehrin insanlarından alıyor") && WhatsNewHas(LocaleFile("pl"), "biorą klientów spośród mieszkańców miasta")
              && WhatsNewHas(LocaleFile("ko"), "도시 주민에게서 손님을 얻으며"),
            "population demand texts: What's new tells of the businesses on the city's people in all eleven languages");

        string folder = System.IO.Path.Combine(ModScripts(), "Population", "Demand");
        string all = string.Concat(Directory.GetFiles(folder, "*.cs").Select(File.ReadAllText));
        Check(!Regex.IsMatch(all, @"\bvoid\s+(?:Update|LateUpdate|FixedUpdate|OnGUI)\s*\(|HarmonyLib|\[Harmony|FindObjects|GameObject\.Find\(|\bLinq\b|UnityEngine"),
            "population demand sources: no Update, no Harmony, no search of the scene, no LINQ and nothing of Unity in the demand's code");
        string links = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Wiring", "Wiring.DemandLinks.cs"));
        string[] bridges =
        {
            "OwnBankBridges.People = BankPeople", "OwnBankBridges.Branch = BranchPeople", "EstateBridges.People = EstatePeople", "EstateBridges.HousingIn = HousingIn",
            "DealerBridges.Buyers = CarBuyers", "OnlineBridges.People = Online", "HustleBridges.Sellers = Sellers", "HustleBridges.Buyers = Bargains",
            "CareersBridges.Learners = Learners", "CareersBridges.Openings = Openings", "TechBridges.Seekers = TechSeekers", "Utilities.UtilitiesBridges.Connections = Connections",
            "CivicBridges.Adults = Adults", "CivicBridges.Taxes = Taxes", "CivicBridges.Residents = Residents", "CityWatch.Residents = CrimeHere", "DistrictBridges.Crime = CrimeIn",
            "Meta.Guide.PageExtra = Pointer"
        };
        bool wired = links.Contains("private static bool Live => Settings.LivingPopulation && CityDemand.Running;");
        string unwired = string.Empty;
        foreach (string b in bridges)
        {
            bool both = links.Contains(b) && links.Contains(b.Substring(0, b.IndexOf(" = ", StringComparison.Ordinal)) + " = null") || links.Contains(b) && b.StartsWith("DealerBridges", StringComparison.Ordinal) && links.Contains("DealerBridges.Reset()");
            if (!both && unwired.Length == 0)
            {
                unwired = b;
            }
            wired &= both;
        }
        string building = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Wiring", "Wiring.ConstructionLinks.cs"));
        wired &= building.Contains("ConstructionBridges.People = DemandLinks.Build;") && building.Contains("ConstructionBridges.People = null;") && links.Contains("internal static int Build(BuildDemand d)");
        Check(wired, "population demand sources: every business's bridge is wired to the population's demand and taken off again, each answering only with Living population on" + (unwired.Length > 0 ? " (" + unwired + ")" : string.Empty));
        string director = File.ReadAllText(System.IO.Path.Combine(ModScripts(), "Population", "PopulationDirector.cs"));
        Check(director.Contains("!_sim.Step(ref slice) || !DemandStep(ref slice)") && director.Contains("PopulationShare.Write(_state, _demand)") && director.Contains("SaveDemand(data);")
              && director.Contains("PopulationShare.Apply(_guest, text, GuestDemand())") && director.Contains("DropDemand();"),
            "population demand sources: the director measures the demand in the day's own slices, saves its line, shares it in part P and takes the host's on a guest");
    }
}
