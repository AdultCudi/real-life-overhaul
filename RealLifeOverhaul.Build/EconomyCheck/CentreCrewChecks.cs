using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// A warehouse taken as a data centre (Tech/Infra: the question when a warehouse is rented or bought, the one fit-out both ways
// reach) and a data centre's crew found by a search firm briefed from headquarters (Tech/Hiring: the crew search, its fee by
// level, its candidates, the hires at that centre, the save's line).
internal static partial class Program
{
    private static void CentreOffer()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        var taken = new List<string>();
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "9 Pier Rd" }, true, taken) == 0 && known.Count == 2,
            "data centre question: the first look of a session learns the warehouses the save holds and asks about none");
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "9 Pier Rd" }, false, taken) == 0, "data centre question: nothing new, nothing asked");
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "4 Quay Ln", "9 Pier Rd" }, false, taken) == 1 && taken[0] == "4 Quay Ln",
            "data centre question: a warehouse rented or bought since is asked about");
        Check(InfraRules.Taken(known, new[] { "1 Dock St", "4 Quay Ln", "9 Pier Rd" }, false, taken) == 0, "data centre question: each warehouse is asked about once");
        Check(InfraRules.Taken(known, new[] { "4 Quay Ln" }, false, taken) == 0 && InfraRules.Taken(known, new[] { "4 Quay Ln", "1 Dock St" }, false, taken) == 0,
            "data centre question: one given up and taken back in the same session is not asked about again");
        var a = new HashSet<string>(StringComparer.Ordinal);
        var b = new HashSet<string>(StringComparer.Ordinal);
        var ta = new List<string>();
        var tb = new List<string>();
        InfraRules.Taken(a, new string[0], true, ta);
        InfraRules.Taken(b, new string[0], true, tb);
        InfraRules.Taken(a, new[] { "7 Mill St", "2 Yard Rd" }, false, ta);
        InfraRules.Taken(b, new[] { "7 Mill St", "2 Yard Rd" }, false, tb);
        Check(ta.Count == 2 && ta[0] == tb[0] && ta[1] == tb[1] && ta[0] == "7 Mill St", "data centre question: in the game's order, the same on every machine");
        Check(InfraRules.Taken(new HashSet<string>(), null, false, taken) == 0, "data centre question: no list, nothing asked");

        Check(InfraRules.FitBlock(0, false) == InfraRules.FitFree && InfraRules.FitBlock(InfraCatalogue.CentresMost - 1, false) == InfraRules.FitFree,
            "data centre fit-out: free below the most there may be");
        Check(InfraRules.FitBlock(InfraCatalogue.CentresMost, false) == InfraRules.FitMost && InfraRules.FitBlock(1, true) == InfraRules.FitAlready,
            "data centre fit-out: refused at the most, and for one that is already a data centre");
        Check(InfraRules.SupplyMissing(true, true, true) == 0 && InfraRules.SupplyMissing(false, true, true) == 1 && InfraRules.SupplyMissing(true, false, true) == 2
              && InfraRules.SupplyMissing(true, true, false) == 4 && InfraRules.SupplyMissing(false, false, false) == 7,
            "data centre fit-out: power, water and the connection named a bit each");

        string infra = ModSource(@"Tech\TechDirector.Infra.cs");
        string page = ModSource(@"Tech\Infra\TechApp.Infra.cs");
        string director = ModSource(@"Tech\TechDirector.cs");
        Check(infra.Contains("() => OpenTech(TabInfra, TechApp.FitRow + place)") && page.Contains("_director.FitOut(place, tier)") && Occurs(infra, "internal bool FitOut(") == 1,
            "data centre question sources: yes opens the Infra tab's own fit-out of that warehouse, and one FitOut does the work");
        Check(infra.Contains("InfraRules.FitBlock(_infra.Centres.Count, _infra.CentreAt(place) != null)") && page.Contains("InfraRules.FitBlock(f.Centres.Count, f.CentreAt(place) != null)"),
            "data centre question sources: the page, the question and the fit-out refuse by the same rule");
        Check(infra.Contains("!Settings.TechInfraOffer") && infra.Contains("GameConfirm.TryShow(") && !infra.Contains("HudConfirm.Show(") && infra.Contains("!GameConfirm.IsOpen"),
            "data centre question sources: its option, the game's question box through GameConfirm, never over another question");
        Check(infra.Contains("EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onRegChange, ref _regChangeSeen)")
              && director.Contains("_onRegChange = Guard.Wrap<Address>(OnRegistrationChange)") && director.Contains("GlobalEvents.onBuildingRegistrationChange -= _onRegChange"),
            "data centre question sources: the game's own event for a building's record, wrapped once by the guard and let go");
        Check(infra.Contains("_warehousesFirst = true;   // what the save already holds is learnt, not asked about"),
            "data centre question sources: loading a save asks about nothing it already holds");
        Check(page.Contains("\"reallife_infra_fit_supply\"") && infra.Contains("TechBridges.SuppliedAt(reg, 1)"), "data centre question sources: power, water and the connection are shown before the money goes");

        string[] keys =
        {
            "reallife_infra_opt_offer", "reallife_opt_tip_tech_infra_offer", "reallife_infra_offer_title", "reallife_infra_offer_body", "reallife_infra_offer_yes",
            "reallife_infra_offer_no", "reallife_infra_supply_power", "reallife_infra_supply_water", "reallife_infra_supply_net", "reallife_infra_supply_missing",
            "reallife_infra_fit_supply", "reallife_infra_fit_already", "reallife_infra_fit_most", "reallife_infra_test_offer", "reallife_infra_test_offer_pass",
            "reallife_infra_test_offer_fail", "reallife_infra_test_offer_off", "reallife_infra_test_offer_option", "reallife_infra_test_offer_none"
        };
        Check(AllIn(keys, out string missing), "data centre question texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        Check(en.TryGetValue("reallife_guide_tech_sector", out string guide) && guide.Contains("When you rent or buy a warehouse, a question asks whether to set it up as a data centre"),
            "data centre question texts: the Guide's page on the tech sector tells of the question");
    }
}
