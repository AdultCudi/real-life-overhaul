using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The talk past the game's own answers. The bank's loan: its terms, its figures by the game's own formulas and a point off
// the rate for a good rating once in a while (Economy/Banking/Terms/LoanTalkRules.cs). The court: a lost trial's fine twice
// a guilty plea's, a chance said in tenths (Justice/JusticeLaw.cs). A tax case settled with the city: the inspector at half
// the chance for a year, on the player's own businesses (Economy/Company/TaxRules.cs). And the saves that keep them, a
// save from before each loading as it did.
internal static partial class Program
{
    private static void DialogTalk()
    {
        DialogLoanTerms();
        DialogLowerRate();
        DialogTrialFine();
        DialogQuietAudits();
        DialogTalkSaves();
    }

    // The terms offered: a shorter, the bank's own and a longer; the figures as the game's own loan would run them.
    private static void DialogLoanTerms()
    {
        int[] terms = new int[LoanTalkRules.TermsMost];
        int two = LoanTalkRules.Terms(2, terms);
        bool twoOk = two == 3 && terms[0] == 1 && terms[1] == 2 && terms[2] == 3 && LoanTalkRules.OwnTerm(2, terms, two) == 1;
        int one = LoanTalkRules.Terms(1, terms);
        bool oneOk = one == 2 && terms[0] == 1 && terms[1] == 2 && LoanTalkRules.OwnTerm(1, terms, one) == 0;
        int five = LoanTalkRules.Terms(5, terms);
        bool fiveOk = five == 3 && terms[0] == 3 && terms[1] == 5 && terms[2] == 6;
        Check(twoOk && oneOk && fiveOk && LoanTalkRules.Terms(0, terms) == 2 && terms[0] == 1,
            "loan talk: the terms offered are two years under the bank's own, its own and a year over, never under a year, the talk starting on the bank's own");

        // $25,000 over the bank's two years of sixty days at 9% a year: the game's own daily interest and payment.
        LoanFigures f = LoanTalkRules.Figures(25000f, 2, 9f, 1f, 60);
        Check(f.Interest == GameLoanMath.DailyInterest(25000f, 9f, 1f, 2, 60) && f.Interest == 37 && f.Principal == 208 && f.PerDay == 245 && f.Days == 121 && f.Total == 25000L + 37L * 121L,
            "loan talk: a loan's figures are the game's own, its interest a day on the whole amount, its payment the amount over the term's days, the sum to repay both");
        LoanFigures longer = LoanTalkRules.Figures(25000f, 3, 9f, 1f, 60), shorter = LoanTalkRules.Figures(25000f, 1, 9f, 1f, 60);
        Check(longer.PerDay < f.PerDay && f.PerDay < shorter.PerDay && longer.Days > f.Days && f.Days > shorter.Days,
            "loan talk: a longer term pays less a day for more days, a shorter one more for fewer");
        Check(LoanTalkRules.DailyPayment(100f, 2, 60) == LoanTalkRules.LeastDailyPayment && LoanTalkRules.DailyPayment(0f, 2, 60) == 0 && LoanTalkRules.Figures(0f, 2, 9f, 1f, 60).Total == 0L
              && LoanTalkRules.DailyPayment(float.NaN, 2, 60) == 0,
            "loan talk: a small loan pays at least the game's least a day, and nothing borrowed costs nothing");
    }

    // A point off the rate: from a rating of 700, once in thirty days at any bank, never under the least a bank lends at.
    private static void DialogLowerRate()
    {
        int floor = CreditCatalogue.RateFloor;
        bool yes = LoanTalkRules.CanLower(true, 712, 400, 0, 9, false, out int w0) == LowerRateAnswer.Yes && w0 == 0;
        bool rating = LoanTalkRules.CanLower(true, 684, 400, 0, 9, false, out _) == LowerRateAnswer.Rating;
        bool wait = LoanTalkRules.CanLower(true, 760, 400, 390, 9, false, out int w1) == LowerRateAnswer.Wait && w1 == 20;
        bool again = LoanTalkRules.CanLower(true, 760, 420, 390, 9, false, out _) == LowerRateAnswer.Yes;
        bool atFloor = LoanTalkRules.CanLower(true, 800, 400, 0, floor, false, out _) == LowerRateAnswer.Floor;
        bool given = LoanTalkRules.CanLower(true, 800, 400, 0, 9, true, out _) == LowerRateAnswer.Given;
        bool off = LoanTalkRules.CanLower(false, 800, 400, 0, 9, false, out _) == LowerRateAnswer.Off;
        Check(yes && rating && wait && again && atFloor && given && off,
            "loan talk: a point off is had from a rating of 700, thirty days after the last ask, once a talk, never under the least rate, and not at all with the economy off");
        Check(LoanTalkRules.Rate(9, 1) == 8 && LoanTalkRules.Rate(9, 0) == 9 && LoanTalkRules.Rate(floor, 1) == floor && LoanTalkRules.Rate(9, -3) == 9,
            "loan talk: the rate agreed is the bank's own less the point, never under the least a bank lends at");
    }

    // A conviction after a trial: the whole sentence and twice the fine a guilty plea brings; a chance in tenths.
    private static void DialogTrialFine()
    {
        ChargeDef tax = JusticeLaw.Find(JusticeLaw.TaxEvasion), reckless = JusticeLaw.Find(JusticeLaw.Reckless);
        var one = new List<ChargeDef> { tax };
        var two = new List<ChargeDef> { reckless };
        JusticeSentence plea = JusticeLaw.Sentence(one, JusticeLaw.GuiltyPleaFactor, 0f, 0, false, JusticeLaw.PrisonCapDefault, false, 1e9f);
        JusticeSentence trial = JusticeLaw.Sentence(one, 1f, 0f, 0, false, JusticeLaw.PrisonCapDefault, false, 1e9f, JusticeLaw.TrialFineFactor);
        JusticeSentence same = JusticeLaw.Sentence(two, 1f, 0f, 0, false, JusticeLaw.PrisonCapDefault, false, 1e9f);
        JusticeSentence told = JusticeLaw.Sentence(two, 1f, 0f, 0, false, JusticeLaw.PrisonCapDefault, false, 1e9f, -1f);
        Check(tax != null && plea.Fine > 0 && Math.Abs(trial.Fine - 2 * plea.Fine) <= 10 && trial.CommunityHours >= plea.CommunityHours && same.Fine == told.Fine
              && Math.Abs(JusticeLaw.TrialFineFactor - 2f * JusticeLaw.GuiltyPleaFactor) < 0.0001f,
            "court: a conviction after a trial carries the whole sentence and twice the fine a guilty plea brings (" + plea.Fine + " and " + trial.Fine + "), every other sentence as it was");
        Check(JusticeLaw.InTen(0.04f) == 0 && JusticeLaw.InTen(0.36f) == 4 && JusticeLaw.InTen(0.34f) == 3 && JusticeLaw.InTen(0.96f) == 10 && JusticeLaw.InTen(1.2f) == 10
              && JusticeLaw.InTen(float.NaN) == 0 && JusticeLaw.InTen(-1f) == 0, "court: a chance is said in the nearest tenth, held between none and all");
    }

    // A tax case settled with the city: half the chance of an audit for four quarters, never shortened by a later one.
    private static void DialogQuietAudits()
    {
        var t = new TaxFile { Risk = 2400 };
        int full = TaxRules.AuditChance(t.Risk, t.Accountant);
        bool before = TaxRules.AuditChance(t, 100) == full;
        TaxRules.Settled(t, 100, 30);
        bool quiet = t.QuietUntil == 100 + TaxRules.QuietQuarters * 30 && TaxRules.AuditChance(t, 150) == full / 2 && TaxRules.AuditChance(t, t.QuietUntil - 1) == full / 2
                     && TaxRules.AuditChance(t, t.QuietUntil) == full;
        TaxRules.Settled(t, 50, 30);
        bool kept = t.QuietUntil == 100 + TaxRules.QuietQuarters * 30;
        int loud = 0, soft = 0;
        bool subset = true;
        for (int age = 1; age <= 2000; age++)
        {
            bool a = TaxRules.Audited(t, 31337, age, t.QuietUntil + 10), b = TaxRules.Audited(t, 31337, age, 150);
            loud += a ? 1 : 0;
            soft += b ? 1 : 0;
            subset &= !b || a;
            subset &= a == TaxRules.Audited(t, 31337, age);   // after the quiet time, the audit falls as it always did
        }
        Check(full > 0 && before && quiet && kept && subset && soft < loud,
            "tax: a case settled with the city halves the chance of an audit for four quarters, a later one never shortens it, and after it audits fall as before ("
            + soft + " and " + loud + " in 2000 days)");
    }

    // The saves: the day a bank was last asked, a settlement's quiet time, a case's sum and its settled outcome; older ones load.
    private static void DialogTalkSaves()
    {
        var file = new CreditFile { StartDay = 3, RateAskedDay = 77 };
        string line = CreditStore.Write(file);
        CreditFile back = CreditStore.Read(line, out _);
        string olderLine = line.Replace("|77|0|0~d:", "~d:");   // (the day asked, then the rise's day and sum, are the header's last three fields)
        CreditFile older = CreditStore.Read(olderLine, out _);
        Check(back != null && back.RateAskedDay == 77 && CreditStore.Write(back) == line && olderLine != line && older != null && older.RateAskedDay == 0 && older.StartDay == 3,
            "loan talk: the day a bank was last asked for a point off comes back from the save, and a save from before has none and loads as it did");

        CompanyState s = FullCompany();
        s.Tax.QuietUntil = 99;
        string company = CompanyStore.Write(s);
        CompanyState read = CompanyStore.Read(company, out _);
        string olderCompany = company.Replace("|99^s:", "^s:");
        CompanyState was = CompanyStore.Read(olderCompany, out _);
        Check(read != null && read.Tax.QuietUntil == 99 && CompanyStore.Write(read) == company && olderCompany != company && was != null && was.Tax.QuietUntil == 0
              && was.Tax.CaseNumber == 7 && was.Shops.Count == 2, "tax: a settlement's quiet time comes back from the save, and a save from before has none and loads every business");

        JusticeState.Clear();
        JusticeCase c = JusticeState.Open(4);
        c.Charges.Add(new JusticeCharge { Id = JusticeLaw.TaxEvasion, Evidence = 0.7f });
        c.Amount = 5000;
        JusticeCase settled = JusticeState.Open(5);
        settled.Charges.Add(new JusticeCharge { Id = JusticeLaw.TaxEvasion, Evidence = 0.6f });
        settled.Stage = CaseStage.Closed;
        settled.Outcome = CaseOutcome.Settled;
        string justice = JusticeState.Write();
        JusticeState.Clear();
        JusticeState.Parse(justice);
        bool kept = JusticeState.Cases.Count == 2 && JusticeState.Cases[0].Amount == 5000 && JusticeState.Cases[1].Amount == 0 && JusticeState.Cases[1].Outcome == CaseOutcome.Settled
                    && JusticeState.Cases[0].Charges.Count == 1 && !JusticeState.Cases[1].WasConviction;
        string olderJustice = Regex.Replace(justice, @",5000(?=;|\|)", string.Empty);
        olderJustice = Regex.Replace(olderJustice, @",0(?=\|D:)", string.Empty);
        JusticeState.Clear();
        JusticeState.Parse(olderJustice);
        bool loads = olderJustice != justice && JusticeState.Cases.Count == 2 && JusticeState.Cases[0].Amount == 0 && JusticeState.Cases[0].Charges.Count == 1
                     && JusticeState.Cases[0].Damages == 0;
        JusticeState.Clear();
        Check(kept && loads, "court: a case's sum and a settled outcome come back from the save, a settlement is no conviction, and a save from before loads every case with no sum");
    }
}
