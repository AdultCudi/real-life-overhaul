using System;
using System.Collections.Generic;
using RealLifeOverhaul.Rivals;

// A player asked to meet the first rival tycoon later than at 5 million, to grow rich without the pressure first. The option
// "When the first rival comes forward" (rivals_first) moves only the first tier's threshold, in steps up to 50 million, and
// every place that reads a tier reads it: the day's round and a new move through the world (RivalWorld.FirstFrom, filled by
// RivalGame.Read), the page's chip and its note, the test button and the Guide's figure. First the steps and the rule, then
// a season of days at each step, then the sources that read it, then the texts.
internal static partial class Program
{
    private static void RivalFirst()
    {
        RivalFirstSteps();
        RivalFirstTiers();
        RivalFirstDays();
        RivalFirstSources();
        RivalFirstWords();
    }

    private static void RivalFirstSteps()
    {
        long[] s = RivalCatalogue.FirstFromSteps;
        bool rising = s.Length == 5 && s[0] == RivalCatalogue.TierFrom[1] && s[s.Length - 1] == 50000000L;
        for (int i = 1; i < s.Length; i++)
        {
            rising &= s[i] > s[i - 1] && s[i] < RivalCatalogue.TierFrom[2];
        }
        Check(rising, "rivals first: the option's steps start at today's threshold, rise to 50 million and stay under the second tier's");
        bool read = true;
        for (int i = 0; i < s.Length; i++)
        {
            read &= RivalRules.FirstFrom(i) == s[i];
        }
        read &= RivalRules.FirstFrom(-1) == s[0] && RivalRules.FirstFrom(s.Length) == s[0] && RivalRules.FirstFrom(99) == s[0];
        Check(read, "rivals first: each step gives its amount, and a step the option does not have is the default");
        Check(RivalRules.From(1, 0L) == RivalCatalogue.TierFrom[1] && RivalRules.From(1, long.MaxValue) == RivalCatalogue.TierFrom[2] - 1L
              && RivalRules.From(2, 50000000L) == RivalCatalogue.TierFrom[2] && RivalRules.From(0, 50000000L) == 0L
              && RivalRules.From(4, 50000000L) == RivalCatalogue.TierFrom[4],
            "rivals first: the first tier's amount is kept between today's threshold and the second's, and no other tier moves");
        var w = new RivalWorld { FirstFrom = 50000000L };
        w.Clear();
        Check(w.FirstFrom == RivalCatalogue.TierFrom[1] && new RivalWorld().FirstFrom == RivalCatalogue.TierFrom[1],
            "rivals first: a new or cleared world reads the default until the game is read");
    }

    private static void RivalFirstTiers()
    {
        bool at = true, same = true;
        foreach (int level in new[] { 1000, 1370, 2000 })
        {
            for (int i = 0; i < RivalCatalogue.FirstFromSteps.Length; i++)
            {
                long first = RivalCatalogue.FirstFromSteps[i];
                long on = RivalRules.At(first, level);
                at &= RivalRules.Tier(on - 1L, level, first) == 0 && RivalRules.Tier(on, level, first) == 1
                      && RivalRules.Tier(RivalRules.At(RivalCatalogue.TierFrom[2], level), level, first) == 2
                      && RivalRules.Tier(RivalRules.At(RivalCatalogue.TierFrom[3], level), level, first) == 3
                      && RivalRules.Tier(RivalRules.At(RivalCatalogue.TierFrom[4], level), level, first) == 4;
            }
            foreach (long worth in new[] { 0L, 4999999L, 5000000L, 7000000L, 99999999L, 100000000L, 3000000000L, 20000000000L })
            {
                same &= RivalRules.Tier(worth, level) == RivalRules.Tier(worth, level, RivalCatalogue.FirstFromSteps[0]);
            }
        }
        Check(at, "rivals first: at every step and price level, a dollar under the amount is quiet, the amount is the first tier, and the later tiers are where they were");
        Check(same, "rivals first: the default step gives the same tier as before for every worth");
    }

    // A season at each step for a player worth 30 million: with the first rival at 5, 10 or 20 million one comes forward and
    // moves; at 50 million nobody does; the same seed gives the same days.
    private static void RivalFirstDays()
    {
        bool right = true, again = true;
        for (int i = 0; i < RivalCatalogue.FirstFromSteps.Length; i++)
        {
            long first = RivalCatalogue.FirstFromSteps[i];
            RivalFirstSeason(first, out int forward, out int moves, out string trace);
            RivalFirstSeason(first, out int forward2, out int moves2, out string trace2);
            bool want = 30000000L >= first;
            right &= want ? forward >= 1 && moves >= 1 : forward == 0 && moves == 0;
            again &= forward == forward2 && moves == moves2 && trace == trace2;
        }
        Check(right, "rivals first: a player worth 30 million meets a rival over a season at 5, 10, 20 and 30 million, and nobody at 50 million");
        Check(again, "rivals first: the same seed and step give the same season");
    }

    private static void RivalFirstSeason(long first, out int forward, out int moves, out string trace)
    {
        RivalFile f = RivalRules.Open(41, 1, 1000);
        RivalWorld w = RivalWorldOf(30000000L);
        w.FirstFrom = first;
        moves = 0;
        var seen = new List<string>();
        for (int d = 1; d <= 90; d++)
        {
            RivalDayReport r = RivalRules.Day(f, w, d, RivalCatalogue.Normal);
            if (r.Announced != 0)
            {
                moves++;
                f.Moves.Clear();   // (the director lands and ends a move; here only the next announcement is wanted)
            }
            seen.Add(r.Activated + "/" + r.Announced + "/" + f.Tier);
        }
        forward = 0;
        for (int i = 0; i < f.Tycoons.Length; i++)
        {
            forward += f.Tycoons[i].Active ? 1 : 0;
        }
        trace = string.Join(",", seen);
    }

    private static void RivalFirstSources()
    {
        string rules = ModSource(@"Rivals\RivalRules.cs");
        string moves = ModSource(@"Rivals\RivalRules.Moves.cs");
        string game = ModSource(@"Rivals\RivalGame.cs");
        string director = ModSource(@"Rivals\RivalDirector.cs");
        string page = ModSource(@"Rivals\RivalPage.cs");
        string guide = ModSource(@"Rivals\RivalGuide.cs");
        string tests = ModSource(@"Rivals\RivalDirector.Tests.cs");
        Check(MemberText(rules, "public static RivalDayReport Day(").Contains("Tier(w.Worth, w.Level, w.FirstFrom)")
              && MemberText(moves, "public static RivalMove Announce(").Contains("Tier(w.Worth, w.Level, w.FirstFrom)"),
            "rivals first: the day's round and a new move read the tier with the world's first threshold");
        Check(MemberText(game, "public static void Read(RivalWorld into)").Contains("into.FirstFrom = RivalDirector.FirstFrom;")
              && director.Contains("public static long FirstFrom => RivalRules.FirstFrom(Settings.RivalsFirst);")
              && director.Contains("RivalRules.At(FirstFrom, Prices.Level)") && !director.Contains("TierFrom[1]"),
            "rivals first: the world is filled from the option each time the game is read, and the director's log says the amount in effect");
        Check(page.Contains("RivalRules.Tier(worth, level, RivalDirector.FirstFrom)") && page.Contains("RivalRules.At(RivalDirector.FirstFrom, level)")
              && !page.Contains("TierFrom[1]") && tests.Contains("_world.FirstFrom") && tests.Contains("public void TestFirst()"),
            "rivals first: the page's chip and note and the tests read the option, and there is a test that prints it");
        Check(guide.Contains(".Fact(\"rival_tens\", () => Money(RivalRules.FirstFrom(Settings.RivalsFirst)))")
              && guide.Contains("Settings.RivalsFirstId") && guide.Contains("\"reallife_rival_test_first\""),
            "rivals first: the Guide's line on when they come follows the option, and the feature owns the option and its test");
        int calls = 0;
        foreach (string code in new[] { rules, moves, page, director, tests })
        {
            calls += System.Text.RegularExpressions.Regex.Matches(code, @"Tier\([^()]*\.Worth, [^(),]*Level\)").Count;
        }
        Check(calls == 0, "rivals first: no tier of the player's in the mod is read without the option (" + calls + " found)");
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        int drop = mod.IndexOf("AddDropdown(Settings.RivalsFirstId", StringComparison.Ordinal);
        string row = drop >= 0 ? mod.Substring(drop, Math.Min(400, mod.Length - drop)) : string.Empty;
        int steps = System.Text.RegularExpressions.Regex.Matches(row.Split(new[] { "Settings.RivalsFirst," }, StringSplitOptions.None)[0], "\"reallife_rival_first_").Count;
        Check(settings.Contains("RivalsFirstId = \"rivals_first\"") && settings.Contains("public static int RivalsFirst = 0;")
              && settings.Contains("RivalsFirst = Int(modId, RivalsFirstId, RivalsFirst);") && steps == RivalCatalogue.FirstFromSteps.Length
              && mod.Contains("AddButton(\"reallife_rival_test_first\"") && tree.Contains("Opts(\"rivals_strength\", \"rivals_first\")"),
            "rivals first: the option is saved, defaults to today's threshold, has a label for each step (" + steps + "), its test button and its place on the page");
    }

    private static void RivalFirstWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("rivals first", langs, codes))
        {
            return;
        }
        string lacking = string.Empty;
        foreach (string key in new[] { "reallife_rival_first", "reallife_rival_first_5", "reallife_rival_first_10", "reallife_rival_first_20",
                                       "reallife_rival_first_30", "reallife_rival_first_50", "reallife_opt_tip_rivals_first", "reallife_rival_test_first",
                                       "reallife_rival_test_first_done" })
        {
            string l = LanguagesLacking(langs, codes, key);
            lacking += l.Length > 0 ? " " + key + " (" + l + ")" : string.Empty;
        }
        Check(lacking.Length == 0, "rivals first: every text is in every language with the English names in curly brackets:" + lacking);
        Check(langs[0].TryGetValue("reallife_rival_test_first_done", out string done) && done.Contains("{amount}") && done.Contains("{worth}") && done.Length <= 240,
            "rivals first: the test's notice names the amount and the player's worth, at most 240 characters");
        Check(LineEverywhere(new[]
              {
                  "a new option sets when the first one comes forward", "uma nova opção define quando o primeiro aparece", "新选项可设定第一位何时登场",
                  "Eine neue Option legt fest, wann der erste hervortritt", "une nouvelle option fixe le moment où le premier se déclare",
                  "una nueva opción fija cuándo se presenta el primero", "новая настройка задаёт, когда выходит вперёд первый",
                  "een nieuwe optie bepaalt wanneer de eerste naar voren komt", "yeni bir seçenek ilk rakibin ne zaman öne çıkacağını belirler"
              }),
            "rivals first: What's new has the option's line in some row of every language");
    }
}
