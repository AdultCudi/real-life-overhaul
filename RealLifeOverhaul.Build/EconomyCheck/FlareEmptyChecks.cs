using System;
using System.Collections.Generic;
using RealLifeOverhaul.Flare;

// Flare's empty home feed says what is true of the account: follow some only to a player who follows none (FlareFeedRules.EmptyTitleKey,
// EmptyBodyKey). A words-on-screen change: rivals and NPCs have no side to it.
internal static partial class Program
{
    private static void FlareEmptyChecks()
    {
        const string advice = "reallife_flare_kit_empty_feed_body", adviceTitle = "reallife_flare_empty_following", quiet = "reallife_flare_empty_following_quiet";
        Check(FlareFeedRules.EmptyTitleKey(FlareFeedTab.Following, 0) == adviceTitle && FlareFeedRules.EmptyBodyKey(FlareFeedTab.Following, 0) == advice,
            "flare empty: the Following tab of a player who follows nobody says to follow some");
        Check(FlareFeedRules.EmptyTitleKey(FlareFeedTab.Following, 3) == quiet && FlareFeedRules.EmptyBodyKey(FlareFeedTab.Following, 3) == null,
            "flare empty: the Following tab of a player who follows accounts says none has posted lately, with no advice");
        Check(FlareFeedRules.EmptyTitleKey(FlareFeedTab.ForYou, 0) == "reallife_flare_empty_feed" && FlareFeedRules.EmptyBodyKey(FlareFeedTab.ForYou, 0) == advice,
            "flare empty: the For you tab of a player who follows nobody keeps the advice");
        Check(FlareFeedRules.EmptyTitleKey(FlareFeedTab.ForYou, 2) == "reallife_flare_empty_feed" && FlareFeedRules.EmptyBodyKey(FlareFeedTab.ForYou, 2) == null,
            "flare empty: the For you tab of a player who follows accounts has no advice to follow");
        Check(FlareFeedRules.EmptyTitleKey(FlareFeedTab.Businesses, 0) == "reallife_flare_kit_empty_businesses" && FlareFeedRules.EmptyBodyKey(FlareFeedTab.Businesses, 0) == null,
            "flare empty: the Businesses tab says no business posts and never asks to follow");
        bool never = true;
        for (int follows = 1; follows <= 40; follows++)
        {
            foreach (FlareFeedTab tab in new[] { FlareFeedTab.ForYou, FlareFeedTab.Following, FlareFeedTab.Businesses })
            {
                never &= FlareFeedRules.EmptyTitleKey(tab, follows) != adviceTitle && FlareFeedRules.EmptyBodyKey(tab, follows) != advice;
            }
        }
        Check(never, "flare empty: a player who follows accounts never gets the follow a few line on any tab");

        string fill = BugsSource("Ui/Flare/FlareScreen.Fill.cs"), app = BugsSource("Social/Flare/FlareApp.Screens.cs");
        Check(fill.Contains("FlareFeedRules.EmptyTitleKey(tab, file.Follows.Count)") && fill.Contains("FlareFeedRules.EmptyBodyKey(tab, file.Follows.Count)")
              && !fill.Contains("T(\"reallife_flare_kit_empty_feed_body\")"), "flare empty: the home page reads both words from the rule and from the account's follows");
        Check(app.Contains("FlareFeedRules.EmptyTitleKey(") && !app.Contains("following ? \"reallife_flare_empty_following\""), "flare empty: the older page uses the same rule");

        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (LanguagesRead("flare empty", langs, codes))
        {
            string lacking = LanguagesLacking(new[] { langs[0], langs[1], langs[2] }, new[] { codes[0], codes[1], codes[2] }, quiet);
            Check(lacking.Length == 0, "flare empty: the quiet Following text is in en, pt and zh-cn (" + lacking + ")");
            Check(!langs[0][quiet].ToLowerInvariant().Contains("follow some") && langs[0][quiet].Length <= 240, "flare empty: the quiet text gives no advice to follow");
        }
    }
}
