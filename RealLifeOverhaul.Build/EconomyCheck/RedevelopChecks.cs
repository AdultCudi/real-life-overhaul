using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;

// Redevelopment (Scripts/Construction with Uptown, the district save and Civic's sites), in the construction game's BuildersChecks
// style, a part at a time: refurnishing (the game's designs for any unit of the player's, the game's own installation contract
// and its price, an AI business's new layout after a tender); then over the sources (one path for the player and the city's
// side, the game touched only in the construction game's game file, nothing in a Going Public session that would differ between
// machines, the tests' lines and buttons); then the texts in every language.
internal static partial class Program
{
    private static void Redevelop()
    {
        RefurnishRuleChecks();
        RefurnishStoreChecks();
        RefurnishSourceChecks();
        RedevelopTextChecks();
    }

    // ------------------------------------------------------------------ refurnishing

    private static void RefurnishRuleChecks()
    {
        // The game's own price: its fee and the design's pieces, less what the furniture there sells for; nothing below nothing.
        Check(RefitRules.FurnishPrice(5860L, 12000L, 3000L) == 14860L && RefitRules.FurnishPrice(5860L, 1000L, 9000L) == 0L && RefitRules.FurnishPrice(0L, 0L, 0L) == 0L
              && RefitRules.FurnishPrice(-5L, 100L, -7L) == 100L, "refurnishing: the installers' price is their fee and the pieces less what the old furniture sells for, never below nothing");
        // The selector: its first place keeps the furniture, the rest are the firm's designs in order; a place past them keeps it too.
        Check(RefitRules.DesignAt(0, 3) == -1 && RefitRules.DesignAt(1, 3) == 0 && RefitRules.DesignAt(3, 3) == 2 && RefitRules.DesignAt(4, 3) == -1 && RefitRules.DesignAt(-2, 3) == -1
              && RefitRules.DesignAt(1, 0) == -1, "refurnishing: the selector's first place keeps the furniture and the others are the designs in the firm's order");
        // An AI business's new layout: never its own, always one of the list, the same for the same seed and tender, none with no other.
        bool never = true, inside = true, same = true;
        var seen = new HashSet<int>();
        for (int tender = 1; tender <= 400; tender++)
        {
            int pick = RefitRules.AiLayout(12345, tender, 5, 2);
            never &= pick != 2;
            inside &= pick >= 0 && pick < 5;
            same &= pick == RefitRules.AiLayout(12345, tender, 5, 2);
            seen.Add(pick);
        }
        Check(never && inside && same, "refurnishing: a business of the city's always wears another layout of the list than its own, the same for the same save and tender");
        Check(seen.Count == 4, "refurnishing: over many tenders every other layout of its kind is worn (" + seen.Count + " of 4)");
        Check(RefitRules.AiLayout(7, 9, 1, 0) == -1 && RefitRules.AiLayout(7, 9, 0, -1) == -1 && RefitRules.AiLayout(7, 9, 1, -1) == 0 && RefitRules.AiLayout(7, 9, 2, 0) == 1
              && RefitRules.AiLayout(7, 9, 2, 1) == 0, "refurnishing: with no other layout the business keeps its own; one not of the list may take any");
    }

    private static void RefurnishStoreChecks()
    {
        var s = new BuildState { Seed = 5, FirstDay = 1, LastDay = 9, NextId = 30 };
        s.Projects.Add(new ProjectFile
        {
            Id = 21, Kind = ProjectKind.Refit, Stage = ProjectStage.Building, Storeys = 1, Hours = 40, Contractor = 3, ContractorPrice = 4567, Street = "ba:street_x", Number = 4,
            FitDesign = "Modern|Blue;1^x", FitType = "ba:businesstype_empty"
        });
        s.Projects.Add(new ProjectFile { Id = 22, Kind = ProjectKind.Refit, Stage = ProjectStage.Ready, Storeys = 1, Hours = 40, Street = "ba:street_y", Number = 6 });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out int version);
        ProjectFile a = back != null ? back.Project(21) : null, b = back != null ? back.Project(22) : null;
        Check(version == BuildStore.Version && a != null && a.FitDesign == "Modern|Blue;1^x" && a.FitType == "ba:businesstype_empty" && b != null && b.FitDesign.Length == 0
              && b.FitType.Length == 0 && BuildStore.Write(back) == line, "refurnishing: a refit's design and its folder come back from the save, any letters in its name, the line as it was");
        // A line from before refurnishing: every refit keeps its furniture.
        string older = line.Replace("|Modern%7CBlue%3B1%5Ex|ba%3Abusinesstype_empty", string.Empty);
        BuildState old = BuildStore.Read(older, out int _);
        Check(older != line && old != null && old.Project(21) != null && old.Project(21).FitDesign.Length == 0 && old.Project(21).FitType.Length == 0 && old.Project(21).ContractorPrice == 4567,
            "refurnishing: a line from before refurnishing reads with every refit keeping its furniture");
        // A folder with no design is no furnishing.
        string lost = line.Replace("|Modern%7CBlue%3B1%5Ex|", "||");
        BuildState none = BuildStore.Read(lost, out int _);
        Check(lost != line && none != null && none.Project(21) != null && none.Project(21).FitType.Length == 0, "refurnishing: a folder with no design reads as no furnishing");
    }

    private static void RefurnishSourceChecks()
    {
        string refits = ModSource(@"Construction\ConstructionDirector.Refits.cs"), units = ModSource(@"Construction\ConstructionGame.Units.cs");
        string market = ModSource(@"Construction\ConstructionDirector.Market.cs"), projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string tests = ModSource(@"Construction\ConstructionDirector.RedevelopTests.cs"), app = ModSource(@"Construction\ConstructionApp.Refit.cs"), rules = ModSource(@"Construction\RefitRules.cs");
        Check(refits.Length > 0 && units.Length > 0 && tests.Length > 0 && app.Length > 0 && rules.Length > 0, "refurnishing sources: the files are where the checks look");
        // The furnishing is the game's own contract and installation, and only the game file names them.
        string furnish = MethodText(units, "internal static bool Furnish(");
        Check(furnish.Contains("new InteriorInstallationFirmContract") && furnish.Contains("interiorInstallationFirmContracts.Add(order)") && furnish.Contains("order.DoInstallation()")
              && furnish.Contains("dayOfInstallation = save.Day"), "refurnishing sources: the design goes in through the game's own installation contract, dated the handover, done by the game");
        Check(MethodText(units, "internal static void Designs(").Contains("InteriorInstallationFirmHelper.GetInteriorDesignsNamesForBuilding(") && MethodText(units, "internal static void Designs(").Contains("into.Sort(StringComparer.Ordinal)"),
            "refurnishing sources: the designs are the game's firm's own list, in the same order on every machine");
        Check(MethodText(units, "internal static bool FurnishFigures(").Contains("InteriorInstallationFirmHelper.GetInstallationFee(") && MethodText(units, "internal static bool FurnishFigures(").Contains("ItemHelper.GetDefaultMarketPrice(")
              && MethodText(units, "internal static bool FurnishFigures(").Contains("GetSellingPrice()"), "refurnishing sources: the price shown is the game's fee, its market prices and its selling prices");
        foreach (string name in new[] { @"Construction\ConstructionDirector.Refits.cs", @"Construction\ConstructionApp.Refit.cs", @"Construction\ConstructionDirector.RedevelopTests.cs" })
        {
            string src = ModSource(name);
            Check(!src.Contains("InteriorInstallationFirmContract") && !src.Contains("InteriorInstallationFirmHelper") && !src.Contains(".Layout ="), "refurnishing sources: " + name + " leaves the game to the construction game's game file");
        }
        foreach (string src in new[] { units, rules, MethodText(refits, "private void AiRefurnish(") })
        {
            Check(!Regex.IsMatch(src, @"using System\.Linq|\.Select\(|\.Where\(|\.Any\(|\.First"), "refurnishing sources: no LINQ in the refurnishing's reads and rules");
        }
        Check(!Regex.IsMatch(rules, @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\."), "refurnishing sources: the refit's rules are game-free and draw only from their seed");
        // The handover lays the design; a business opens; a home is no business for Interiors.
        string refitted = MethodText(refits, "private void Refitted(");
        Check(refitted.Contains("LetOpen(p);") && refitted.Contains("Furnished(p)") && refitted.Contains("u.Trading") && MethodText(refits, "private string Furnished(").Contains("ConstructionGame.Furnish(u, p.FitDesign"),
            "refurnishing sources: at the handover the business opens and the chosen design is laid by the game's installers; a home has no business to score");
        Check(MethodText(refits, "public string RefitBlock(").Contains("ConstructionGame.Unit(street, number") && MethodText(units, "private static bool UnitOf(").Contains("reg.RentedByPlayer")
              && MethodText(units, "private static bool UnitOf(").Contains("GoingPublic.Mine(reg)") && MethodText(units, "private static bool UnitOf(").Contains("IsHamptonsHouse()"),
            "refurnishing sources: any unit the player rents or owns can be refitted, theirs in a session, never a Hamptons house");
        // The city's side: by tender, by whoever wins it, the game's own layouts, never in a session.
        string ai = MethodText(refits, "private void AiRefurnish(");
        Check(ai.Contains("GoingPublic.InSession") && ai.Contains("ConstructionGame.AiLayouts(") && ai.Contains("RefitRules.AiLayout(_state.Seed, tender") && ai.Contains("ConstructionGame.SetAiLayout("),
            "refurnishing sources: a business of the city's wears another of the game's layouts after its refit, from the save's seed, never in a Going Public session");
        Check(MethodText(market, "private void RivalHanded(").Contains("AiRefurnish(t.Street, t.Number, t.Id)") && market.Contains("t.SiteId = StartWorksSite(t.Street, t.Number, RefitRules.Works, false, won.Bidder")
              && MethodText(projects, "private void Handover(").Contains("AiRefurnish(p.Street, p.Number, p.Tender)"), "refurnishing sources: a rival's refit and the player's company's refit of a business of the city's both refurnish it at their handover");
        string layouts = MethodText(units, "internal static bool AiLayouts(");
        Check(layouts.Contains("reg.RentedByPlayer") && layouts.Contains("reg.BuildingOwnedByPlayer") && layouts.Contains("GoingPublic.Foreign(reg)") && layouts.Contains("corporationRivalId")
              && layouts.Contains("GetOrLoadBusinessLayoutSet("), "refurnishing sources: only a business of the city's of no chain is refurnished, with layouts the game has at its size, never the player's or another player's");
        // The tests: public static methods, each once in the guide with its line.
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        foreach (string name in new[] { "TestRefurnishNow", "TestCityRefurnishNow" })
        {
            Check(tests.Contains("public static bool " + name + "()") && Regex.Matches(guide, "ConstructionDirector." + name + "\\(\\)").Count == 1, "refurnishing sources: the test " + name + " is a public static method, once in the guide");
        }
        Check(tests.Contains(": PASS.") && tests.Contains(": FAIL.") && tests.Contains("\"reallife_bld_test_passed\""), "refurnishing sources: the tests' lines end PASS or FAIL and a passed test says so in a notice");
    }

    // ------------------------------------------------------------------ the texts

    private static void RedevelopTextChecks()
    {
        var keys = new SortedSet<string>();
        foreach (string name in new[] { @"Construction\ConstructionApp.Refit.cs", @"Construction\ConstructionDirector.RedevelopTests.cs", @"Construction\ConstructionDirector.Refits.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        Check(keys.Count > 30, "redevelopment texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("redevelopment texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            int missing = 0, styled = 0;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0)
                {
                    missing++;
                    Check(false, "redevelopment texts: " + key + " is in " + codes[l] + ".json");
                }
                else if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    styled++;
                    Check(false, "redevelopment texts: " + key + " has no exclamation mark or dash in " + codes[l]);
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && !SameNames(en, value))
                {
                    styled++;
                    Check(false, "redevelopment texts: " + key + " has the English names in curly brackets in " + codes[l]);
                }
            }
            Check(missing == 0 && styled == 0, "redevelopment texts: every key of redevelopment is in " + codes[l] + ".json, in the style");
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            Check(page.Contains("{bld_refit_hours_least}") && page.Split('\n').Length >= 92, "redevelopment texts: the Guide's construction page in " + codes[l] + " has the refurnishing's lines");
        }
        foreach (string notice in new[] { "reallife_bld_test_passed", "reallife_bld_test_no_unit", "reallife_bld_test_session_city", "reallife_bld_test_no_city_business", "reallife_bld_test_failed_log" })
        {
            Check(words[0].TryGetValue(notice, out string en) && en.Length <= 240, "redevelopment texts: the notice " + notice + " is at most 240 characters");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Refurnish any unit you rent or own" }, { "pt", "Remobilie qualquer unidade que você aluga ou tem" }, { "zh-cn", "可以重新布置你租用或拥有的任何单元" },
            { "de", "Richte jede Einheit neu ein" }, { "fr", "Réaménagez toute unité que vous louez ou possédez" }, { "es", "Vuelve a amueblar cualquier unidad" },
            { "ru", "Обставьте заново любое помещение" }, { "nl", "Richt elke eenheid die je huurt of bezit opnieuw in" }, { "tr", "Kiraladığınız ya da sahip olduğunuz her birimi" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            Check(starts.TryGetValue(codes[l], out string start) && WhatsNewHas(words[l], start), "redevelopment texts: What's new has refurnishing's line in " + codes[l] + " (any of its rows)");
        }
    }
}
