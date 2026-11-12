using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.TestShops;

// The test tool that takes one business of each kind over, for trying the new interface on running shops: the pick (one per kind, the
// nearest, the console's filter, the special rival and diploma skips), the Going Public refusal, the notice's length, the source
// that calls the game's own takeover once and not before the refusals, and the texts.
internal static partial class Program
{
    private static void TestShopsAll()
    {
        TestShopsPick();
        TestShopsNotice();
        TestShopsSource();
        TestShopsWords();
    }

    private static ShopCandidate Shop(string kind, float distance)
    {
        return new ShopCandidate { Kind = kind, Distance = distance, CanBeTakenOver = true };
    }

    private static void TestShopsPick()
    {
        var all = new List<ShopCandidate>
        {
            Shop("bakery", 90f), Shop("bakery", 40f), Shop("bar", 70f), Shop("bar", 70f), Shop("gym", 10f),
            new ShopCandidate { Kind = "cafe", Distance = 1f, CanBeTakenOver = true, Rented = true },
            new ShopCandidate { Kind = "cafe", Distance = 2f, CanBeTakenOver = true, Owned = true },
            new ShopCandidate { Kind = "cafe", Distance = 3f },
            new ShopCandidate { Kind = "clinic", Distance = 4f, CanBeTakenOver = true, SpecialService = true },
            new ShopCandidate { Kind = "ba:businesstype_empty", Distance = 5f, CanBeTakenOver = true, Empty = true },
            new ShopCandidate { Kind = "spa", Distance = 6f, CanBeTakenOver = true, Unavailable = true },
            new ShopCandidate { Kind = "", Distance = 7f, CanBeTakenOver = true }
        };
        var pick = new ShopPick();
        TestShopsRules.Pick(all, true, true, pick);
        Check(pick.Chosen.Count == 3 && pick.Chosen[0] == 4 && pick.Chosen[1] == 1 && pick.Chosen[2] == 2,
            "test shops: one of each kind, the nearest of the kind, nearest kind first, equal distances to the earlier entry");
        Check(pick.OfficesLeftOut == 0 && pick.RivalsLeftOut == 0, "test shops: nothing is counted as left out when nothing is");
        bool none = true;
        foreach (int i in pick.Chosen)
        {
            none &= i < 5;
        }
        Check(none, "test shops: a building behind a DLC or the one the player stands in, and a business without a kind, are never taken");
        TestShopsRules.Pick(all, false, true, pick);
        Check(pick.Chosen.Count == 1 && pick.Chosen[0] == 4, "test shops: the nearest button takes the one nearest business");
        bool filter = !TestShopsRules.Takeable(all[5]) && !TestShopsRules.Takeable(all[6]) && !TestShopsRules.Takeable(all[7])
                      && !TestShopsRules.Takeable(all[8]) && !TestShopsRules.Takeable(all[9]) && TestShopsRules.Takeable(all[0]);
        Check(filter, "test shops: rented, owned, not takeable, a special service and an empty shop are out, as the console's filter has it");

        var mixed = new List<ShopCandidate>
        {
            new ShopCandidate { Kind = "office", Distance = 1f, CanBeTakenOver = true, Office = true },
            new ShopCandidate { Kind = "bank", Distance = 2f, CanBeTakenOver = true, SpecialRivalActive = true },
            Shop("deli", 30f)
        };
        TestShopsRules.Pick(mixed, true, false, pick);
        Check(pick.Chosen.Count == 1 && pick.Chosen[0] == 2 && pick.OfficesLeftOut == 1 && pick.RivalsLeftOut == 1,
            "test shops: without the Office Businesses diploma an office is left out and counted, and an active special rival's business is left out and counted");
        TestShopsRules.Pick(mixed, true, true, pick);
        Check(pick.Chosen.Count == 2 && pick.Chosen[0] == 0 && pick.Chosen[1] == 2 && pick.OfficesLeftOut == 0 && pick.RivalsLeftOut == 1,
            "test shops: with the diploma an office is taken, a special rival's business never is");
        TestShopsRules.Pick(new List<ShopCandidate>(), true, true, pick);
        Check(pick.Chosen.Count == 0, "test shops: a city with nothing to take over gives nothing");
        TestShopsRules.Pick(null, false, true, pick);
        Check(pick.Chosen.Count == 0, "test shops: no list gives nothing");

        var again = new ShopPick();
        TestShopsRules.Pick(all, true, true, pick);
        TestShopsRules.Pick(all, true, true, again);
        bool same = pick.Chosen.Count == again.Chosen.Count;
        for (int i = 0; same && i < pick.Chosen.Count; i++)
        {
            same &= pick.Chosen[i] == again.Chosen[i];
        }
        Check(same, "test shops: the same save gives the same pick every time");
        Check(TestShopsRules.Refused(true) && !TestShopsRules.Refused(false), "test shops: a Going Public session refuses, single player does not");

        var big = new List<ShopCandidate>(6000);
        for (int i = 0; i < 6000; i++)
        {
            big.Add(Shop("kind" + (i % 60), (i * 37) % 997));
        }
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        TestShopsRules.Pick(big, true, true, pick);
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        Check(pick.Chosen.Count == 60 && ms < 50.0, "test shops: a large city (6000 businesses, 60 kinds) is picked in " + ms.ToString("0.0") + " ms, once, on the press");
    }

    private static void TestShopsNotice()
    {
        const string more = "and {count} more";
        var few = new List<string> { "Bakery", "Bar" };
        Check(TestShopsRules.Kinds(few, 100, more) == "Bakery, Bar", "test shops: a few kinds are all named");
        var many = new List<string>();
        for (int i = 0; i < 80; i++)
        {
            many.Add("Business kind " + i);
        }
        string cut = TestShopsRules.Kinds(many, 150, more);
        Check(cut.Length <= 150 && cut.EndsWith(" more", StringComparison.Ordinal) && cut.StartsWith("Business kind 0, ", StringComparison.Ordinal),
            "test shops: many kinds are cut at a whole name and end in the count left over, within the budget");
        bool fits = true;
        for (int budget = 20; budget <= 300; budget += 7)
        {
            string line = TestShopsRules.Kinds(many, budget, more);
            fits &= line.Length <= budget && line.Contains("more");
        }
        Check(fits, "test shops: the kinds line never passes its budget, at any budget");
        Check(TestShopsRules.Kinds(many, 5, more) == "and 80 more", "test shops: a budget too small for a name still gives the count");
        Check(TestShopsRules.NoticeMax == 240, "test shops: a notice is at most 240 characters");
    }

    private static void TestShopsSource()
    {
        string game = File.ReadAllText(System.IO.Path.Combine(DesignLocales, "..", "Scripts", "TestShops", "TestShopsGame.cs"));
        int refuse = game.IndexOf("TestShopsRules.Refused(GoingPublic.InSession)", StringComparison.Ordinal);
        int take = game.IndexOf("BizManPresentation.OvertakeBusiness(reg);", StringComparison.Ordinal);
        Check(refuse > 0 && take > refuse && game.IndexOf("OvertakeBusiness(", take + 45, StringComparison.Ordinal) < 0,
            "test shops: the Going Public refusal comes first and the game's own takeover is called in one place, never copied");
        Check(!game.Contains("AddToPlayer") && !game.Contains("GenerateEmployees"), "test shops: nothing of the game's takeover body is copied");
        Check(game.Contains("RivalsHelper.GetSpecialRivalState") && game.Contains("isActive") && game.Contains("DiplomaName.OfficeBusinesses") && game.Contains("GameDlc.Gated"),
            "test shops: the game's refusals (an active special rival, the office diploma, a DLC) are read");
        string mod = File.ReadAllText(System.IO.Path.Combine(DesignLocales, "..", "Scripts", "Core", "RealLifeOverhaulMod.cs"));
        Check(mod.Contains("\"reallife_testshops_all\", () => TestShops.TestShopsGame.Run(true)") && mod.Contains("\"reallife_testshops_nearest\", () => TestShops.TestShopsGame.Run(false)"),
            "test shops: both buttons are on the options page, under the rivals");
    }

    private static void TestShopsWords()
    {
        string[] keys = { "reallife_testshops_all", "reallife_testshops_nearest", "reallife_testshops_done", "reallife_testshops_more", "reallife_testshops_none",
                          "reallife_testshops_office", "reallife_testshops_single" };
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            bool all = words != null;
            for (int i = 0; all && i < keys.Length; i++)
            {
                all = words.TryGetValue(keys[i], out string v) && v.Length > 0 && v.Length <= 120 && !v.Contains('!') && !v.Contains((char)0x2014) && !v.Contains((char)0x2013) && !v.Contains("  ");
            }
            Check(all, "test shops texts: the labels and notices in " + code + " are there, short, plain, with no double space");
            Check(words != null && words["reallife_testshops_done"].Contains("{count}") && words["reallife_testshops_done"].Contains("{kinds}")
                  && words["reallife_testshops_more"].Contains("{count}") && words["reallife_testshops_office"].Contains("{name}"),
                "test shops texts: the notices in " + code + " carry their figures");
        }
    }
}
