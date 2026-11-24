using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Meta;

// The loan means test and the game's year (Scripts/Economy/Banking/Terms/LoanMeansRules.cs, Loans/GameLoans.MeansLimit, Loans/BankTerms.MeansCap): a player on 365 day years, with about 2,000 in the bank,
// no job and no business, only gig work, was offered a loan of 48,000, where a 60 day year lent a sum that fitted their means. The game's own test (BankDialog.GetEconomyMaxLoanAmount) lends the larger
// of the wealth and a quarter of the daily income over the days the loan is paid back in, and those days are the bank's years times the game's days a year, so the same income is lent six times as much
// at 365 days. A bank's cap does not scale with the year either, so nothing held it back. The mod's copy of the test now counts a 60 day year's days at most, and writes the bank's cap so the counter
// follows it; a shorter year keeps the game's smaller limit; the option brings the game's own test back.
internal static partial class Program
{
    private static void LoanMeans()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        LoanMeansTerms();
        LoanMeansSources();
        Console.WriteLine("  the loan means test and the year: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // The game's test as its code reads (BankDialog.GetEconomyMaxLoanAmount, LoanHelper.CalculatePayBackDays), written out again here from the decompiled game for the comparison.
    private static float GameMeans(float wealth, float income, int daysPerYear, int years, float remaining, bool tutorialLoanOpen)
    {
        float a = 0f;
        if (tutorialLoanOpen)
        {
            a = 15500f;
        }
        a = Math.Max(a, wealth);
        float b = income * (float)(daysPerYear * years) * 0.25f;
        a = Math.Max(a, b);
        return (float)Math.Floor(Math.Max(0f, a - remaining));
    }

    private static void LoanMeansTerms()
    {
        bool same = true;
        foreach (int year in new[] { 30, 60, 120, 365 })
        {
            foreach (int years in new[] { 1, 2, 3, 5 })
            {
                foreach (float income in new[] { 0f, 40f, 105f, 400f })
                {
                    foreach (float wealth in new[] { 0f, 2000f, 90000f })
                    {
                        foreach (float owed in new[] { 0f, 3000f })
                        {
                            foreach (bool tutorial in new[] { false, true })
                            {
                                float mine = LoanMeansRules.Limit(wealth, income, LoanMeansRules.PayBackDays(year, years, false), owed, tutorial);
                                same &= mine == GameMeans(wealth, income, year, years, owed, tutorial);
                            }
                        }
                    }
                }
            }
        }
        Check(same, "loan means: with the year not scaled the test is the game's, term for term, over 384 cases of year, term, income, wealth, debt and tutorial");
        // the player's case: gig work paying about 105 a day, 2,000 in the bank, a bank that pays loans over three years
        float game365 = GameMeans(2000f, 105f, 365, 3, 0f, false), game60 = GameMeans(2000f, 105f, 60, 3, 0f, false);
        float scaled365 = LoanMeansRules.Limit(2000f, 105f, LoanMeansRules.PayBackDays(365, 3, true), 0f, false);
        Check(game365 > 20000f && game60 < 6000f && game365 > game60 * 6f, "loan means: the game's own test at 365 days lends " + game365 + " for the income a 60 day year lends " + game60 + " for (the report's six times)");
        Check(scaled365 == game60 && scaled365 < game365, "loan means: with the year scaled, 365 days lends what 60 days does for the same income (" + scaled365 + ")");
        bool never = true, shortKept = true, flat = true;
        foreach (int year in new[] { 8, 30, 45, 60, 61, 90, 180, 365, 1000 })
        {
            foreach (int years in new[] { 1, 3, 5 })
            {
                int days = LoanMeansRules.PayBackDays(year, years, true);
                never &= days <= LoanMeansRules.PayBackDays(year, years, false) && days <= LoanMeansRules.ReferenceYear * years;
                shortKept &= year > LoanMeansRules.ReferenceYear || days == LoanMeansRules.PayBackDays(year, years, false);
                flat &= year < LoanMeansRules.ReferenceYear || days == LoanMeansRules.ReferenceYear * years;
            }
        }
        Check(never, "loan means: the scaled days are never more than the game's, nor more than a 60 day year's");
        Check(shortKept, "loan means: a year shorter than 60 days keeps the game's smaller limit, the year is not stretched up");
        Check(flat, "loan means: from 60 days a year up the scaled days are the same, so the limit does not grow with the year");
        // what does not change
        Check(LoanMeansRules.Limit(90000f, 105f, LoanMeansRules.PayBackDays(365, 3, true), 0f, false) == 90000f, "loan means: wealth still counts in full, scaled or not");
        Check(LoanMeansRules.Limit(0f, 0f, LoanMeansRules.PayBackDays(365, 3, true), 0f, true) == 15500f && LoanMeansRules.Limit(0f, 0f, 1095, 20000f, true) == 0f,
            "loan means: the game's tutorial loan is 15,500 less what is owed, scaled or not");
        Check(LoanMeansRules.Limit(2000f, 105f, LoanMeansRules.PayBackDays(365, 3, true), 1000f, false) == scaled365 - 1000f && LoanMeansRules.Limit(0f, -50f, 180, 0f, false) == 0f,
            "loan means: what is owed comes off the means, and a week of losses lends nothing");
        // a bank's cap: the means and what is owed there already, never more than the rating's
        Check(LendingTerms.MostNow(Math.Min(48409f, scaled365 + 500f), 500f, 1e9f) == scaled365, "loan means: a cap held to the means and the debt at that bank leaves exactly the means to borrow");
    }

    private static void LoanMeansSources()
    {
        string rules = ModSource(@"Economy\Banking\Terms\LoanMeansRules.cs"), loans = ModSource(@"Economy\Banking\Loans\GameLoans.cs"), terms = ModSource(@"Economy\Banking\Loans\BankTerms.cs");
        string app = ModSource(@"Economy\Banking\EconomyDirector.App.cs"), director = ModSource(@"Economy\Banking\EconomyDirector.cs"), settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), list = ModSource(@"Meta\FeatureList.Economy.cs");
        string means = MemberText(loans, "public static float MeansLimit(");
        Check(means.Contains("LoanMeansRules.PayBackDays(save.gameVariables.daysPerYear, settings.yearsToPayLoan, Settings.EconomyLoanYear)") && means.Contains("LoanMeansRules.Limit(") && !means.Contains("CalculatePayBackDays"),
            "loan means: the phone's limit counts the paying-back days as the rules do and no longer the game's year");
        string wanted = MemberText(terms, "public static LenderTerms Wanted("), cap = MemberText(terms, "public static float MeansCap(");
        Check(wanted.Contains("Mathf.Min(terms.Cap, MeansCap(settings))") && wanted.IndexOf("QuestHold(settings)", StringComparison.Ordinal) < wanted.IndexOf("MeansCap", StringComparison.Ordinal),
            "loan means: the bank's cap is held to the means after the game's quest loans are left alone (the tutorial's loan is the game's)");
        Check(cap.Contains("!Settings.EconomyLoanYear") && cap.Contains("daysPerYear <= LoanMeansRules.ReferenceYear") && cap.Contains("float.MaxValue") && cap.Contains("GameLoans.MeansLimit(settings) + GameLoans.OwedAt(settings)"),
            "loan means: the cap is the means and the debt at that bank, and is not held with the option off or in a year of 60 days or fewer");
        Check(app.Contains("private void KeepAtTheBank()") && app.Contains("open.dialog is BankDialog") && app.Contains("KeepTerms(\"a bank's dialogue\")") && director.Contains("KeepAtTheBank();"),
            "loan means: a bank's dialogue brings the cap up to date once, before an amount is typed");
        Check(!rules.Contains("UnityEngine") && !rules.Contains("SaveGameManager"), "loan means: the rules are game-free");
        Check(settings.Contains("EconomyLoanYearId = \"economy_loan_year\"") && settings.Contains("public static bool EconomyLoanYear = true;") && settings.Contains("EconomyLoanYear = Bool(modId, EconomyLoanYearId, EconomyLoanYear);")
              && mod.Contains("AddToggle(Settings.EconomyLoanYearId, \"reallife_econ_opt_loan_year\", Settings.EconomyLoanYear,") && list.Contains("Owns(Settings.EconomyLoanYearId)")
              && tree.Contains("\"economy_loan_year\")"), "loan means: the option is saved as economy_loan_year, on, read at every load, in the page and in the feature list");
        TreeRow row = OptionsTree.Row("economy_loan_year");
        Check(row != null && row.TipKey == "reallife_opt_tip_economy_loan_year" && OptionsTree.SectionOf("economy_loan_year") != null && OptionsTree.SectionOf("economy_loan_year") == OptionsTree.SectionOf("start_debt"),
            "loan means: the option is in the Debt section with its tip");
        Dictionary<string, string> en = LocaleFile("en");
        string label = en.TryGetValue("reallife_econ_opt_loan_year", out string l) ? l : string.Empty, tip = en.TryGetValue("reallife_opt_tip_economy_loan_year", out string t) ? t : string.Empty;
        Check(label.Length > 0 && label.Length <= 60 && tip.Length > 0 && tip.Length <= 190 && tip.Split(' ').Length <= 32 && tip.IndexOf('—') < 0 && tip.IndexOf(';') < 0 && label.IndexOf(';') < 0,
            "loan means: the option's label and tip are short (" + label.Length + " and " + tip.Length + " characters)");
        Check(WhatsNewHas(en, "Fixed: on a game with 365 day years the banks no longer lend six times what a 60 day year lends for the same income"), "loan means: What's new tells of the fix");
    }
}
