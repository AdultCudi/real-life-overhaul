using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Street;

// A player asked a woman to make things official, was told it was too early, and the option left the conversation for good.
// A step that is asked too early is never taken off the menu now: she says how many days to wait, and asked again after them
// the answer can be yes. The same holds for asking somebody out again (five days) and asking a partner to move in. A second
// report: a stranger in the street was told to be up to 80 though the game draws no face older than 60 (StreetRules.SeenAge).
internal static partial class Program
{
    private static void SocialSteps()
    {
        SocialAskOutWait();
        SocialStepWaits();
        SocialSeenAge();
        SocialStepTexts();
    }

    private static SocialPerson SocialDating(int stageDay)
    {
        return new SocialPerson { Stage = SocialStage.Dating, StageDay = stageDay, Affection = 90f, Trust = 80f, Dates = 6, Compatible = true };
    }

    private static void SocialAskOutWait()
    {
        var p = new SocialPerson { Stage = SocialStage.Friend, Compatible = true, Affection = 70f, Trust = 60f };
        bool free = SocialRules.AskOutWait(p, 10) == 0;
        p.Remember(MemoryCode.AskedOut, 10);
        p.Remember(MemoryCode.Official, 11);
        Check(free && SocialRules.AskOutWait(p, 10) == SocialRules.AskOutGapDays && SocialRules.AskOutWait(p, 12) == SocialRules.AskOutGapDays - 2,
            "social steps: asked out again at once, they say how many days to wait (" + SocialRules.AskOutWait(p, 10) + " the same day, " + SocialRules.AskOutWait(p, 12) + " two days on)");
        Check(SocialRules.AskOutWait(p, 10 + SocialRules.AskOutGapDays) == 0 && SocialRules.AskOutWait(p, 400) == 0 && SocialRules.AskOutWait(p, 3) == SocialRules.AskOutGapDays,
            "social steps: after the days nobody waits, and in a save whose day went back nobody is made to wait longer than the gap");
        p.Remember(MemoryCode.AskedOut, 40);
        Check(SocialRules.AskOutWait(p, 41) == SocialRules.AskOutGapDays - 1, "social steps: the latest time asked counts, not the first");
    }

    private static void SocialStepWaits()
    {
        int day0 = 100;
        SocialPerson p = SocialDating(day0);
        bool early = true;
        for (int day = day0; day < day0 + SocialRules.PartnerDays; day++)
        {
            early &= !SocialRules.WouldCommit(p, day) && SocialRules.CommitWait(p, day) == SocialRules.PartnerDays - (day - day0);
        }
        Check(early && p.Stage == SocialStage.Dating && p.Affection == 90f && p.Trust == 80f,
            "social steps: asked to make it official early, the days still to wait are counted down each day and nothing about them changes");
        Check(SocialRules.WouldCommit(p, day0 + SocialRules.PartnerDays) && SocialRules.CommitWait(p, day0 + SocialRules.PartnerDays) == 0,
            "social steps: asked again after " + SocialRules.PartnerDays + " days of dating, with the dates and the figures, the answer can be yes");
        SocialPerson few = SocialDating(day0);
        few.Dates = SocialRules.PartnerDates - 1;
        Check(!SocialRules.WouldCommit(few, day0 + 40) && SocialRules.CommitWait(few, day0 + 40) == 0,
            "social steps: with the days passed but too few dates the answer stays no, and no wait is promised that would not help");
        var partner = new SocialPerson { Stage = SocialStage.Partner, StageDay = day0, Trust = 90f };
        int last = day0 + SocialRules.MoveInDays - 1;
        bool moveIn = SocialRules.MoveInWait(partner, day0) == SocialRules.MoveInDays && !SocialRules.WouldMoveIn(partner, last) && SocialRules.MoveInWait(partner, last) == 1
                      && SocialRules.WouldMoveIn(partner, last + 1) && SocialRules.MoveInWait(partner, last + 1) == 0;
        Check(moveIn, "social steps: asked to move in early, the days to wait are counted down to the day it can be yes");
    }

    private static void SocialSeenAge()
    {
        bool most = true, same = true, exact = true, young = true;
        int top = 0, oldest = 0;
        int[] years = { 60, 28, 365 };
        foreach (int perYear in years)
        {
            for (int y = 18; y <= 80; y++)
            {
                for (int seed = 1; seed <= 40; seed++)
                {
                    int days = y * perYear + seed % perYear;
                    int told = StreetRules.SeenAge(days, perYear, seed * 7919);
                    most &= told <= StreetRules.SeenOldest + StreetRules.SeenSpread - 1 && told >= StreetRules.SeenYoungest;
                    same &= told == StreetRules.SeenAge(days, perYear, seed * 7919);
                    exact &= y > StreetRules.SeenOldest || told == y;
                    if (y > StreetRules.SeenOldest)
                    {
                        top |= 1 << (told - StreetRules.SeenOldest);
                        oldest = Math.Max(oldest, told);
                    }
                }
            }
        }
        for (int seed = 1; seed <= 200; seed++)
        {
            int none = StreetRules.SeenAge(0, 60, seed);
            young &= none >= StreetRules.SeenYoungest && none < StreetRules.SeenYoungest + StreetRules.NoAgeSpread;
        }
        Check(most && exact, "street age: nobody is told more than " + (StreetRules.SeenOldest + StreetRules.SeenSpread - 1) + " (the game draws no face older than " + StreetRules.SeenOldest
                             + "), and everybody the game draws up to sixty is told their own age (oldest told " + oldest + ")");
        Check(top == (1 << StreetRules.SeenSpread) - 1, "street age: the oldest are told every age from " + StreetRules.SeenOldest + " to " + (StreetRules.SeenOldest + StreetRules.SeenSpread - 1) + ", not all one number");
        Check(same && young, "street age: the same figure is told the same age every time, and one the game never gave an age is drawn as the youngest and told 18 to 29");
        Check(StreetRules.SeenAge(3 * 60, 60, 5) == StreetRules.SeenYoungest && StreetRules.SeenAge(70 * 60, 0, 5) >= StreetRules.SeenOldest,
            "street age: a child's age in days is told as 18, and a year of no length does not divide by zero");
        StreetStranger s = StreetRules.Identity(11, 5, 77, true, StreetRules.SeenAge(80 * 60, 60, StreetRules.Seed(11, 0, 77)), 8, -1);
        Check(s.Age >= StreetRules.SeenOldest && s.Age <= StreetRules.SeenOldest + StreetRules.SeenSpread - 1 && (s.Age >= StreetRules.RetiredFrom) == (s.Job == SocialCatalogue.Jobs.Length - 1),
            "street age: the oldest stranger (" + s.Age + ") has the job the age gives: retired from " + StreetRules.RetiredFrom);
    }

    private static void SocialStepTexts()
    {
        string[] keys = { "reallife_social_askout_wait", "reallife_social_official_wait", "reallife_social_movein_wait" };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: ", "Naprawiono: ", "수정: " };
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
        bool all = true, next = true;
        string rowKey = SocialRowKey(LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")));
        next = rowKey != null;
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), codes[l] + ".json"));
            if (words == null)
            {
                all = false;
                continue;
            }
            if (l < 3)
            {
                foreach (string key in keys)
                {
                    all &= words.TryGetValue(key, out string many) && many.Contains("{days}") && words.TryGetValue(key + "_one", out string one) && !one.Contains("{days}") && one.Length > 10;
                }
            }
            string row = rowKey != null && words.TryGetValue(rowKey, out string found) ? found : string.Empty;
            next &= row.Split(new[] { marks[l] }, StringSplitOptions.None).Length - 1 >= 2;
        }
        Check(all, "social steps: each wait is said in en, pt and zh-cn, in the plural with the days and in the singular without");
        Check(next, "social steps: What's new says both fixes in all eleven languages");
    }

    // The row that holds the English lines, whichever version took them: every language's row of that key says both.
    private static string SocialRowKey(Dictionary<string, string> en)
    {
        if (en == null)
        {
            return null;
        }
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal)
                && row.Value.Contains("too early to make things official") && row.Value.Contains("draws faces no older than 60"))
            {
                return row.Key;
            }
        }
        return null;
    }
}
