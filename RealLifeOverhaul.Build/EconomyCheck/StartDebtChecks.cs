using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Meta;

// A player asked to be able to start a game owing a student loan, "a very hard core play option" (Scripts/Economy/Banking/Terms/StartDebtRules.cs,
// Desk/CreditDesk.StartDebt.cs): "New games start with a student loan" (start_debt, off by default) and "What the student loan paid for"
// (start_debt_degree). The loan is one of the game's own, at the biggest bank, with no money paid out, marked as a student loan so the
// apps name it; the game takes its payments and the credit side reads the ones it cannot take as it reads every loan's. First the rules
// (the amounts held to what the courses cost, the payment the game's loans page leaves alone, which game is new enough), then what a
// missed payment does to the rating, the save, the places in the sources it is joined at, the option and its texts.
internal static partial class Program
{
    private const float StartDebtBanksOwn = 2040000f;   // what the city's two banks would lend on their own terms together (2,000,000 and 40,000)

    private static void StartDebtChecks()
    {
        StartDebtChoices();
        StartDebtTerms();
        StartDebtDecision();
        StartDebtCredit();
        StartDebtStore();
        StartDebtSources();
        StartDebtOption();
        StartDebtWords();
    }

    // Four studies to choose from; each loan is what that study costs, between the cheapest and the dearest course of its kind.
    private static void StartDebtChoices()
    {
        Check(StartDebtRules.Count == 4 && StartDebtRules.DegreeKeys.Length == 4 && StartDebtRules.Amounts.Length == 4
              && StartDebtRules.Diploma == 0 && StartDebtRules.Bachelor == 1 && StartDebtRules.Master == 2 && StartDebtRules.Mba == 3 && StartDebtRules.DegreeDefault == StartDebtRules.Bachelor,
            "start debt: four studies to choose from, saved as 0 to 3, each with its text and its amount, and the bachelor's degree the one the switch starts on");
        bool rising = true;
        for (int i = 1; i < StartDebtRules.Count; i++)
        {
            rising &= StartDebtRules.Amounts[i] > StartDebtRules.Amounts[i - 1];
        }
        Check(rising && StartDebtRules.Amounts[0] >= CreditCatalogue.LoanLeast, "start debt: a dearer study is a bigger loan, and the least of them is a loan a bank would make");
        int vocationalLow = int.MaxValue, vocationalHigh = 0, degreeLow = int.MaxValue, degreeHigh = 0, postLow = int.MaxValue, postHigh = 0;
        foreach (CourseDef c in StudyCatalogue.All)
        {
            if (c.Kind == CourseKind.Vocational)
            {
                vocationalLow = Math.Min(vocationalLow, c.Fee);
                vocationalHigh = Math.Max(vocationalHigh, c.Fee);
            }
            else if (c.Kind == CourseKind.Degree)
            {
                degreeLow = Math.Min(degreeLow, c.Fee);
                degreeHigh = Math.Max(degreeHigh, c.Fee);
            }
            else if (c.Kind == CourseKind.Postgrad)
            {
                postLow = Math.Min(postLow, c.Fee);
                postHigh = Math.Max(postHigh, c.Fee);
            }
        }
        int diploma = StartDebtRules.Amounts[StartDebtRules.Diploma], bachelor = StartDebtRules.Amounts[StartDebtRules.Bachelor];
        int master = StartDebtRules.Amounts[StartDebtRules.Master], mba = StartDebtRules.Amounts[StartDebtRules.Mba];
        Check(diploma >= vocationalLow && diploma <= vocationalHigh,
            "start debt: a diploma's loan is what a diploma at the school costs (" + diploma + " between " + vocationalLow + " and " + vocationalHigh + ")");
        Check(bachelor >= degreeLow && bachelor <= degreeHigh,
            "start debt: a bachelor's loan is what a degree at the university costs (" + bachelor + " between " + degreeLow + " and " + degreeHigh + ")");
        Check(master >= degreeLow + postLow && master <= degreeHigh + postHigh && master > bachelor,
            "start debt: a master's loan is a degree and a master's course (" + master + " between " + (degreeLow + postLow) + " and " + (degreeHigh + postHigh) + ")");
        CourseDef mbaCourse = StudyCatalogue.Find(StudyCatalogue.Mba);
        Check(mbaCourse != null && mbaCourse.Kind == CourseKind.Postgrad && mba == bachelor + mbaCourse.Fee, "start debt: the MBA's loan is a bachelor's degree and the MBA's own fee");
        Check(mba <= StudyCatalogue.LoanMost && StartDebtRules.RateBp == StudyCatalogue.LoanRateBp,
            "start debt: no loan is past the most the university's own student loan lends, and the rate is that loan's (" + StudyCatalogue.LoanRateBp + " basis points)");
        Check(StartDebtRules.Level(-3) == 0 && StartDebtRules.Level(0) == 0 && StartDebtRules.Level(2) == 2 && StartDebtRules.Level(3) == 3 && StartDebtRules.Level(40) == 3,
            "start debt: a saved choice that is not one of the four is the nearest of them");
        Check(StartDebtRules.Figures(-1, PriceRules.Start, 2, 1f, 60).Amount == diploma && StartDebtRules.Figures(9, PriceRules.Start, 2, 1f, 60).Amount == mba,
            "start debt: the figures of a choice nobody offers are the nearest one's, never an error");
    }

    // The payment, the interest and the pace: the game's own loan formulas, and nothing the game's loans page would move.
    private static void StartDebtTerms()
    {
        StartDebtFigures two = StartDebtRules.Figures(StartDebtRules.Bachelor, PriceRules.Start, 2, 1f, 60);
        Check(two.Amount == 9000 && two.Payment == 37 && two.Interest == 6 && two.Days == 244,
            "start debt: a bachelor's degree at a bank that pays loans over two years is 9,000, with 37 a day and 6 interest, paid off in 244 days (" + two.Amount + ", " + two.Payment + ", "
            + two.Interest + ", " + two.Days + ")");
        StartDebtFigures three = StartDebtRules.Figures(StartDebtRules.Bachelor, PriceRules.Start, 3, 1f, 60), five = StartDebtRules.Figures(StartDebtRules.Bachelor, PriceRules.Start, 5, 1f, 60);
        Check(three.Payment == 25 && three.Days == 360 && five.Payment == 25 && five.Days == 360,
            "start debt: at a bank that lets a loan run longer the loan runs its own 360 days, 25 a day on 9,000 (" + three.Payment + " and " + five.Payment + ")");
        StartDebtFigures one = StartDebtRules.Figures(StartDebtRules.Bachelor, PriceRules.Start, 1, 1f, 60);
        Check(one.Payment == 75 && one.Days == 120,
            "start debt: at a bank that pays loans over one year the game will not let it run slower than half the bank's own pace (" + one.Payment + " a day)");
        Check(StartDebtRules.Figures(StartDebtRules.Bachelor, 2000, 2, 1f, 60).Amount == 18000 && StartDebtRules.Figures(StartDebtRules.Bachelor, 500, 2, 1f, 60).Amount == 4500,
            "start debt: the amount is at the city's prices, as every sum of the mod's is");
        Check(StartDebtRules.Figures(StartDebtRules.Bachelor, PriceRules.Start, 2, 2f, 60).Interest == 13
              && StartDebtRules.Figures(StartDebtRules.Bachelor, PriceRules.Start, 2, 0.5f, 60).Interest == 3
              && StartDebtRules.Figures(StartDebtRules.Bachelor, PriceRules.Start, 2, 1f, 365).Interest == 1,
            "start debt: the game's interest multiplier and the length of its year move the daily interest as they move every loan's");

        bool untouched = true, interest = true, paid = true;
        string worst = string.Empty;
        foreach (int years in new[] { 1, 2, 3, 4, 5, 10 })
        {
            foreach (int year in new[] { 60, 120, 365 })
            {
                foreach (int level in new[] { 1000, 1500, 2500 })
                {
                    for (int d = 0; d < StartDebtRules.Count; d++)
                    {
                        StartDebtFigures f = StartDebtRules.Figures(d, level, years, 1f, year);
                        // LoanHelper.CalculateDailyPayment and CalculateMinimumDailyPayment, as the decompiled game has them: the loans page clamps a payment up to the least
                        int standard = Math.Max(5, (int)Math.Floor(f.Amount / (float)(year * years)));
                        int least = Math.Max(5, (int)Math.Floor(standard * 0.5f));
                        bool here = f.Payment >= least && f.Payment >= 5 && f.Payment >= f.Amount / StartDebtRules.TermDays && f.Payment <= f.Amount && f.Days >= 1;
                        untouched &= here;
                        // the daily interest of 4.5 per cent a year, floored, as the game works a loan's out
                        interest &= f.Interest == (int)Math.Floor((decimal)f.Amount * 4.5m / 100m / year);
                        paid &= f.Amount == PriceRules.At(StartDebtRules.Amounts[d], level);
                        worst = here || worst.Length > 0 ? worst : "choice " + d + " at " + years + " years, " + year + " days, level " + level + ": " + f.Payment + " against " + least;
                    }
                }
            }
        }
        Check(untouched, "start debt: the payment is never under the least the game lets that loan carry, so opening the loans page moves nothing (" + worst + ")");
        Check(interest, "start debt: the daily interest is the student loan's rate on the amount over the game's year, in whole money");
        Check(paid, "start debt: the amount is the choice's at the city's prices whatever the bank");
        bool same = true;
        for (int d = 0; d < StartDebtRules.Count; d++)
        {
            StartDebtFigures a = StartDebtRules.Figures(d, 1300, 3, 1.5f, 120), b = StartDebtRules.Figures(d, 1300, 3, 1.5f, 120);
            same &= a.Amount == b.Amount && a.Payment == b.Payment && a.Interest == b.Interest && a.Days == b.Days;
        }
        Check(same, "start debt: the same inputs give the same loan, on every machine");
        Check(StartDebtRules.MinimumPayment(0, 2, 60) == 0 && StartDebtRules.Payment(0, 40) == 0 && StartDebtRules.MinimumPayment(100, 2, 60) == 5 && StartDebtRules.Payment(100, 3) == 5,
            "start debt: no loan means no payment, and the game's least payment of 5 is the floor");
    }

    // Which game is new, and in what order the reasons not to are told.
    private static void StartDebtDecision()
    {
        Check(StartDebtRules.Decide(true, 1, 0, false, false, true) == StartDebtAnswer.Yes, "start debt: a game on its first day that owes nothing, with a bank and no tutorial, is given its loan");
        Check(StartDebtRules.Decide(false, 1, 0, false, false, true) == StartDebtAnswer.Off && StartDebtRules.Decide(false, 400, 5, true, true, false) == StartDebtAnswer.Off,
            "start debt: with the option off nothing is made, whatever else is so");
        Check(StartDebtRules.Decide(true, StartDebtRules.NewDays, 0, false, false, true) == StartDebtAnswer.Yes
              && StartDebtRules.Decide(true, StartDebtRules.NewDays + 1, 0, false, false, true) == StartDebtAnswer.NotNew,
            "start debt: the first day is new and the second is not");
        Check(StartDebtRules.Decide(true, 1, 1, false, false, true) == StartDebtAnswer.NotNew && StartDebtRules.Decide(true, 1, 0, true, true, false) == StartDebtAnswer.Session,
            "start debt: a game that owes something already is not new, so a second loan is never made on a reload of the first day");
        Check(StartDebtRules.Decide(true, 90, 3, true, true, false) == StartDebtAnswer.NotNew, "start debt: an old save is never given a loan, in a session or not");
        Check(StartDebtRules.Decide(true, 1, 0, false, true, true) == StartDebtAnswer.Tutorial && StartDebtRules.Decide(true, 1, 0, false, true, false) == StartDebtAnswer.Tutorial,
            "start debt: with the tutorial on there is no loan (its first loan and its objectives read every loan), told to the player");
        Check(StartDebtRules.Decide(true, 1, 0, false, false, false) == StartDebtAnswer.NoBank, "start debt: a city with no bank has nobody to lend it, and the desk asks again for a while");
        Check(StartDebtRules.Decide(true, 1, 0, true, false, true) == StartDebtAnswer.Session, "start debt: in a Going Public session a loan is never made on a shared world's behalf");
    }

    // What the loan does to the rating: a game that starts with it can still borrow, it counts against the rating, a missed payment lowers it and a payment on time raises it.
    private static void StartDebtCredit()
    {
        var clean = new CreditScore();
        CreditScoring.Compute(StartDebtFile(), new CreditInputs { Limit = StartDebtBanksOwn }, 1, clean);
        Check(clean.Value == CreditCatalogue.CleanStart, "start debt: a game with no debt opens at the rating a clean file has (" + clean.Value + ")");
        bool lends = true, counts = true, hurts = true, closes = true, heals = true;
        string seen = string.Empty;
        for (int d = 0; d < StartDebtRules.Count; d++)
        {
            StartDebtFigures f = StartDebtRules.Figures(d, PriceRules.Start, 2, 1f, 60);
            var owing = new CreditInputs { Owed = f.Amount, Limit = StartDebtBanksOwn, DailyService = f.Payment + f.Interest };
            CreditFile file = StartDebtFile();
            var start = new CreditScore();
            CreditScoring.Compute(file, owing, 1, start);
            lends &= start.Lends;
            counts &= start.Value < clean.Value;
            file.CountPayments(1, 0, 1, 0);
            var missed = new CreditScore();
            CreditScoring.Compute(file, owing, 1, missed);
            hurts &= missed.Value < start.Value;
            closes &= missed.Value < CreditCatalogue.LendingThreshold;
            CreditFile kept = StartDebtFile();
            for (int day = 1; day <= 5; day++)
            {
                kept.CountPayments(day, 1, 0, 0);
            }
            var later = new CreditScore();
            CreditScoring.Compute(kept, owing, 6, later);
            var idle = new CreditScore();
            CreditScoring.Compute(StartDebtFile(), owing, 6, idle);   // the same file five days on with no payment on it: only its length has grown
            heals &= later.Value > idle.Value;
            seen += (seen.Length > 0 ? ", " : string.Empty) + start.Value + " to " + missed.Value + " to " + later.Value;
        }
        Check(lends, "start debt: whichever study it paid for, a game that starts with the loan can still borrow (" + seen + ")");
        Check(counts, "start debt: the debt counts against the rating from the first day");
        Check(hurts, "start debt: a payment the game cannot take lowers the rating");
        Check(closes, "start debt: one payment missed on a thin file takes the rating under the lending threshold of " + CreditCatalogue.LendingThreshold + " (" + seen + ")");
        Check(heals, "start debt: five payments on time raise the rating past the same file five days on with none");

        // The loan's mark is read as any loan's: the same finding for the same day, whatever the loan was taken for.
        var results = new List<string>();
        foreach (int purpose in new[] { LoanPurpose.Plain, LoanPurpose.Student })
        {
            var reference = new object();
            var marks = new List<LoanMark> { StartDebtMark(purpose, reference) };
            var findings = new List<LoanFinding>();
            LoanBook.Settle(marks, new List<LoanView> { StartDebtView(reference, 9000f) }, findings);
            string missedDay = findings.Count == 1 ? findings[0].Note + " " + findings[0].Amount : "none";
            reference = new object();
            marks = new List<LoanMark> { StartDebtMark(purpose, reference) };
            findings = new List<LoanFinding>();
            LoanBook.Settle(marks, new List<LoanView> { StartDebtView(reference, 8963f) }, findings);
            string paidDay = findings.Count == 1 ? findings[0].Note + " " + findings[0].Amount : "none";
            results.Add(missedDay + "; " + paidDay);
        }
        Check(results[0] == "Missed 37; Paid 43" && results[1] == results[0],
            "start debt: a student loan's day with no payment taken is a missed payment and one with it taken is a paid one, exactly as a plain loan's (" + results[0] + " / " + results[1] + ")");
        var carried = new LoanMark();
        Check(carried.Purpose == LoanPurpose.Plain && LoanPurpose.Plain == 0 && LoanPurpose.Business == 1 && LoanPurpose.Consolidation == 2 && LoanPurpose.Student == 3,
            "start debt: a loan's purpose is a saved number, the old ones as they were and the student loan the next");
    }

    private static CreditFile StartDebtFile()
    {
        var file = new CreditFile();
        CreditScoring.Open(file, new List<LoanView>(), 1);
        file.FirstCreditDay = 1;
        return file;
    }

    private static LoanMark StartDebtMark(int purpose, object reference)
    {
        return new LoanMark { Street = "W 34th St", Number = 120, Total = 9000f, FirstDay = 1, Remaining = 9000f, Payment = 37, Interest = 6, Ref = reference, Purpose = purpose };
    }

    private static LoanView StartDebtView(object reference, float remaining)
    {
        return new LoanView { Street = "W 34th St", Number = 120, Total = 9000f, Remaining = remaining, Payment = 37, Interest = 6, Ref = reference };
    }

    // The loan's purpose is in the save and comes back; a purpose past the known ones is the last known.
    private static void StartDebtStore()
    {
        var file = new CreditFile();
        file.Loans.Add(StartDebtMark(LoanPurpose.Student, null));
        string line = CreditStore.Write(file);
        CreditFile back = CreditStore.Read(line, out int version);
        Check(back != null && back.Loans.Count == 1 && back.Loans[0].Purpose == LoanPurpose.Student && back.Loans[0].Total == 9000f && back.Loans[0].Payment == 37,
            "start debt: a student loan's mark comes back from the save as a student loan, with its figures (version " + version + ")");
        file.Loans[0].Purpose = 9;
        CreditFile beyond = CreditStore.Read(CreditStore.Write(file), out int _);
        Check(beyond != null && beyond.Loans.Count == 1 && beyond.Loans[0].Purpose == LoanPurpose.Student, "start debt: a purpose a later version added is read as the nearest this one knows");
        file.Loans[0].Purpose = LoanPurpose.Business;
        CreditFile business = CreditStore.Read(CreditStore.Write(file), out int _);
        Check(business != null && business.Loans[0].Purpose == LoanPurpose.Business, "start debt: a business loan is still read as one");
    }

    // The places it is joined at, read from the sources as they ship.
    private static void StartDebtSources()
    {
        string director = ModSource(@"Economy\Banking\EconomyDirector.cs"), desk = ModSource(@"Economy\Banking\Desk\CreditDesk.cs"), made = ModSource(@"Economy\Banking\Desk\CreditDesk.StartDebt.cs");
        string start = MemberText(director, "private void StartUp(");
        int synced = start.IndexOf("LoanBook.Sync(file.Loans, _live, today, null, true);", StringComparison.Ordinal);
        int told = start.IndexOf("_desk.Start(file, today, fresh)", StringComparison.Ordinal);
        int worked = start.IndexOf("CreditLedger.Refresh();", StringComparison.Ordinal);
        Check(synced > 0 && told > synced && worked > told,
            "start debt: the credit side tells its desk the file is new as it starts, after the save's loans are found and before the rating is first worked out");
        string begin = MemberText(desk, "public void Start(CreditFile file, int today, bool opened = false)");
        Check(begin.Contains("StartDebtBegin(opened);") && begin.IndexOf("StartDebtBegin(opened);", StringComparison.Ordinal) < begin.IndexOf("SelfCheck(today);", StringComparison.Ordinal),
            "start debt: the desk looks at the new game's loan as it starts, before the loans are looked over");
        Check(MemberText(desk, "public void Slow()").Contains("if (_startDebtWaiting)") && MemberText(desk, "public void Slow()").Contains("StartDebtSecond();")
              && MemberText(desk, "public void Stop(string why)").Contains("_startDebtWaiting = false;"),
            "start debt: a second asks again only while the loan waits for a bank, and the desk stopping forgets the wait");
        string first = MemberText(made, "private void StartDebtBegin(bool opened)"), second = MemberText(made, "private void StartDebtSecond()");
        Check(first.Contains("_startDebtWaiting = opened && Settings.StartDebt;"),
            "start debt: only a file opened just now, with the option on, is given a loan: a save the credit side has seen never is");
        Check(second.Contains("StartDebtRules.Decide(Settings.StartDebt, Today, Save.Loans.Count, GoingPublic.InSession,") && second.Contains("TutorialHelper.IsTutorialEnabled()")
              && second.Contains("BankTerms.House") && second.Contains("++_startDebtWaited < StartDebtSeconds") && second.Contains("\"reallife_econ_note_start_debt_tutorial\""),
            "start debt: the day, the loans owed, a session, the tutorial and a bank are asked of the rules; the banks are waited for a while; a tutorial game is told why");
        string make = MemberText(made, "private void MakeStartDebt(Lender bank)");
        Check(make.Contains("Save.Loans.Add(loan);") && make.Contains("Purpose = LoanPurpose.Student") && make.Contains("_file.Loans.Add(mark);") && make.Contains("Ref = loan")
              && make.Contains("_file.FirstCreditDay <= 0") && make.Contains("\"reallife_econ_note_start_debt\"") && make.Contains("new Loan(f.Amount, f.Interest, f.Payment, bank.Address)"),
            "start debt: the loan goes in the game's list and on the file at once as a student loan, the history of credit begins with it and the player is told");
        Check(!made.Contains("PhoneLending") && !made.Contains("BizBooks") && !made.Contains("ChangeMoney") && !made.Contains("_wallet")
              && !made.Contains("GameEvent.Invoke") && !made.Contains("CreditKind.Application")
              && !made.Contains("CreditKind.NewCredit") && !made.Contains("modData"),
            "start debt: no money is paid out, no event of the game's is raised, no application or new credit is written and nothing new is kept in the save");
        string test = MemberText(made, "public void TestStartDebt()");
        Check(test.Contains("TestReady(\"Start debt\")") && test.Contains("\"reallife_econ_note_start_debt_test\"") && !test.Contains("Save.Loans.Add") && !test.Contains("_file.Loans.Add"),
            "start debt: the test button shows the figures and makes nothing");
        string live = ModSource(@"Ui\Money\FinanceLive.Overview.cs");
        Check(FinanceTables.Student == FinanceTables.Approval + 1 && FinanceTables.DebtKinds.Length == FinanceTables.Student + 1
              && FinanceTables.DebtKinds[FinanceTables.Student] == "reallife_study_loan_account" && FinanceTables.OfLoan(LoanPurpose.Student) == FinanceTables.Student
              && FinanceTables.OfLoan(LoanPurpose.Plain) == FinanceTables.Loan && FinanceTables.OfLoan(LoanPurpose.Business) == FinanceTables.Business
              && FinanceTables.OfLoan(LoanPurpose.Consolidation) == FinanceTables.Consolidated
              && FinanceTables.DebtKinds[FinanceTables.Approval] == "reallife_tab_kind_approval" && FinanceTables.DebtKinds[FinanceTables.Loan] == "reallife_econ_kind_loan",
            "start debt: the apps' debt kinds name a student loan, last, so no other kind's number moves");
        Check(live.Contains("new float[FinanceTables.DebtKinds.Length]") && !live.Contains("FinanceTables.Approval + 1") && live.Contains("case FinanceTables.Student: return UiIcon.Cap;"),
            "start debt: the Finance overview's sums have a place for every kind, and a student loan has a cap for its picture");
        Check(ModSource(@"Economy\Banking\Rating\CreditStore.cs").Contains("Math.Min(Count(c, 22), LoanPurpose.Student)"), "start debt: the save reads the student loan's purpose");
    }

    // The option: saved, off, on the page in the debt section, owned by the credit feature and set by no preset.
    private static void StartDebtOption()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), list = ModSource(@"Meta\FeatureList.Economy.cs");
        Check(settings.Contains("StartDebtId = \"start_debt\"") && settings.Contains("StartDebtDegreeId = \"start_debt_degree\"") && settings.Contains("public static bool StartDebt = false;")
              && settings.Contains("public static int StartDebtDegree = " + StartDebtRules.DegreeDefault + ";")
              && settings.Contains("StartDebt = Bool(modId, StartDebtId, StartDebt);") && settings.Contains("StartDebtDegree = Int(modId, StartDebtDegreeId, StartDebtDegree);"),
            "start debt: the switch is saved as start_debt and is off, the choice as start_debt_degree and starts on the bachelor's degree, and both are read at every load");
        Check(mod.Contains("AddToggle(Settings.StartDebtId, \"reallife_econ_opt_start_debt\", Settings.StartDebt,") && mod.Contains("v => Settings.StartDebt = v)")
              && mod.Contains("AddDropdown(Settings.StartDebtDegreeId, \"reallife_econ_opt_start_debt_degree\",") && mod.Contains("Economy.StartDebtRules.DegreeKeys, Settings.StartDebtDegree,")
              && mod.Contains("AddButton(\"reallife_econ_test_start_debt\", () => Economy.EconomyDirector.Instance?.DeskTest(d => d.TestStartDebt()))"),
            "start debt: the options page has the switch, the choice by the rules' own texts, and the button that shows the loan");
        Check(list.Contains(".Owns(Settings.StartDebtId, Settings.StartDebtDegreeId)") && !list.Contains("Preset(Settings.StartDebtId") && !list.Contains("Preset(Settings.StartDebtDegreeId"),
            "start debt: the credit feature owns both options, and no preset sets them (the next game begun is the player's own choice)");
        TreeSection section = OptionsTree.SectionOf("start_debt"), sibling = OptionsTree.SectionOf("start_debt_degree");
        TreeRow row = OptionsTree.Row("start_debt"), degree = OptionsTree.Row("start_debt_degree");
        Check(section != null && sibling == section && section.Id == "debt" && row != null && degree != null && !row.IsButton && !degree.IsButton && section.Rows.Count <= OptionsTree.MostRows,
            "start debt: both stand in Money, Debt, which is no more than " + OptionsTree.MostRows + " rows");
        Check(section != null && Array.IndexOf(section.Needs, "economy") >= 0 && degree != null && Array.IndexOf(degree.Needs, "start_debt") >= 0 && row != null && row.Needs.Length == 0,
            "start debt: both wait for the credit rating, and the choice waits for the switch");
        Check(row != null && row.TipKey == "reallife_opt_tip_start_debt" && degree != null && degree.TipKey == "reallife_opt_tip_start_debt_degree"
              && row.Words != null && row.Words.Contains("student")
              && degree.Words != null && degree.Words.Contains("mba") && !row.Words.Contains("rent") && !degree.Words.Contains("rent"),
            "start debt: each has its sentence under the pointer, and the search finds them by student loan and by the study");
    }

    // The texts: in every language, the right size, with the figures the code gives them.
    private static void StartDebtWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("start debt", langs, codes))
        {
            return;
        }
        var keys = new List<string>
        {
            "reallife_econ_opt_start_debt", "reallife_opt_tip_start_debt", "reallife_econ_opt_start_debt_degree", "reallife_opt_tip_start_debt_degree", "reallife_econ_note_start_debt",
            "reallife_econ_note_start_debt_tutorial", "reallife_econ_note_start_debt_test", "reallife_econ_test_start_debt"
        };
        keys.AddRange(StartDebtRules.DegreeKeys);
        string lacking = string.Empty;
        foreach (string key in keys)
        {
            string l = LanguagesLacking(langs, codes, key);
            lacking += l.Length > 0 ? " " + key + " (" + l + ")" : string.Empty;
        }
        Check(lacking.Length == 0, "start debt: every text is in every language:" + lacking);
        Dictionary<string, string> en = langs[0];
        string note = en.TryGetValue("reallife_econ_note_start_debt", out string n) ? n : string.Empty, test = en.TryGetValue("reallife_econ_note_start_debt_test", out string t) ? t : string.Empty;
        string tutorial = en.TryGetValue("reallife_econ_note_start_debt_tutorial", out string u) ? u : string.Empty;
        Check(note.Length > 0 && note.Length <= 240 && test.Length > 0 && test.Length <= 240 && tutorial.Length > 0 && tutorial.Length <= 240,
            "start debt: the three notifications are 240 characters or fewer (" + note.Length + ", " + test.Length + ", " + tutorial.Length + ")");
        Check(note.Contains("{amount}") && note.Contains("{bank}") && note.Contains("{payment}") && note.Contains("{interest}") && test.Contains("{amount}") && test.Contains("{bank}")
              && test.Contains("{payment}") && test.Contains("{interest}"),
            "start debt: the loan's notice gives the sum, the bank, the daily payment and the interest, the same four in the test's");
        Check(tutorial.Contains("tutorial") && !tutorial.Contains("{"), "start debt: a game with the tutorial on is told that the tutorial is why");
        Check(en.TryGetValue("reallife_opt_tip_start_debt", out string tip) && tip.EndsWith(".", StringComparison.Ordinal) && tip.Contains("tutorial")
              && tip.Contains("student loan") && tip.Contains("credit")
              && en.TryGetValue("reallife_econ_opt_start_debt", out string label) && label.Contains("student loan") && !label.EndsWith(".", StringComparison.Ordinal),
            "start debt: the switch is named as a player would look for it and its sentence says it is for the next game, the tutorial and credit");
        Check(WhatsNewHas(en, "A new game can now start with a student loan"), "start debt: What's new tells of it in English (any of its rows)");
    }
}
