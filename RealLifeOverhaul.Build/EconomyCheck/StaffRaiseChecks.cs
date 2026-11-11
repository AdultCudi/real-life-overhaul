using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Ui;

// The raises staff ask for, answered together and by themselves (two players, 5 and 6 Nov 2026: "Its only agree or refuse no offer less" with 28 requests waiting, and "What settings do I
// need to configure so that employees who, for example, request a 3% pay raise automatically have it approved"). Offer less to all stands beside Agree to all and Refuse all and is the one
// offer-less rule (StaffRules.Answer, half of what was asked) for each request; an option agrees by itself, on each morning's pass, a request that asks a set share of the pay or less, and
// leaves it to a manager or an executive wherever one answers raises. And the review before hiring several candidates in My Employees, which named the game's wage where the candidates'
// table names the wage paid. Run alone by --staff-raises, and in the full list.
internal static partial class Program
{
    private static void StaffRaiseAnswers()
    {
        int before = _run, failedBefore = _failed;
        RaiseAllLess();
        RaiseLimitRule();
        RaiseLimitCases();
        RaiseMorningPass();
        RaiseAnswerScreens();
        RaiseAnswerDirector();
        RaiseAnswerOption();
        RaiseHireReview();
        RaiseAnswerTexts();
        Console.WriteLine("  raises answered together and by themselves: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // Somebody paid so much an hour whose request, for so much more, waits.
    private static StaffFile Asking(int paid, int asked)
    {
        StaffFile f = Worker(paid, paid + asked);
        f.AskDay = 10;
        f.AskTo = paid + asked;
        return f;
    }

    // ---------------------------------------------------------------- Offer less to all

    // 28 requests waiting, as a player's screenshot has them: offering less to all gives each person what offering less gives them, takes the standing it takes and starts the rest.
    private static void RaiseAllLess()
    {
        var crew = new List<StaffFile>();
        long asked = 0, expected = 0;
        for (int i = 0; i < 28; i++)
        {
            StaffFile f = Worker(1500 + i * 130, 1700 + i * 130 + i % 7 * 37);
            f.AskDay = 20;
            f.AskTo = f.Going;
            crew.Add(f);
            asked += StaffRules.RaiseFor(f, RaiseAnswer.Agree);
            expected += (f.AskTo - f.Paid) / 2;
        }
        long given = 0;
        bool each = true;
        foreach (StaffFile f in crew)
        {
            int wanted = (f.AskTo - f.Paid) * CompanyCatalogue.RaiseLessShare / 100, standing = f.Standing;
            StaffRules.Answer(f, RaiseAnswer.Less, 21);
            given += f.Raise;
            each &= f.Raise == wanted && f.Standing == standing + CompanyCatalogue.StandLess && f.AskDay == 0 && f.AskTo == 0 && f.AskRest == 21 + CompanyCatalogue.RaiseRestDays;
        }
        Check(each, "offer less to all: each of " + crew.Count + " people gets half of what they asked, loses the standing offering less costs and rests for " + CompanyCatalogue.RaiseRestDays + " days");
        Check(given == expected && given * 2 <= asked && given * 2 >= asked - crew.Count,
            "offer less to all: the whole comes to half of what agreeing to all would give (" + given + " of " + asked + " hundredths an hour)");
        Check(StaffRules.RaiseFor(Asking(2000, 1), RaiseAnswer.Less) == 0 && StaffRules.RaiseFor(Asking(2000, 301), RaiseAnswer.Less) == 150 && StaffRules.RaiseFor(Asking(2000, 300), RaiseAnswer.Refuse) == 0,
            "offer less to all: the half is whole hundredths rounded down, and refusing gives nothing");
    }

    // ---------------------------------------------------------------- the limit

    // The edge of each limit: the most that is its share of the pay is agreed, one hundredth more is not. And what is no request, or no limit.
    private static void RaiseLimitRule()
    {
        int[] pays = { 400, 1000, 1859, 2000, 3437, 9999, 25000 };
        int[] limits = { 1, 2, 3, 5, 10, 25, CompanyCatalogue.AgreeUpToMost };
        string bad = null;
        int cases = 0;
        foreach (int pay in pays)
        {
            foreach (int limit in limits)
            {
                int most = (int)((long)pay * limit / 100);
                cases++;
                if (!StaffRules.WithinLimit(Asking(pay, most), limit) || StaffRules.WithinLimit(Asking(pay, most + 1), limit))
                {
                    bad = bad ?? pay + " at " + limit + "% (" + most + ")";
                }
            }
        }
        Check(bad == null, "agreed by itself: at the edge of every limit the share of the pay is agreed and a hundredth more is not (" + cases + " cases" + (bad != null ? ", first wrong " + bad : string.Empty) + ")");
        Check(!StaffRules.WithinLimit(Asking(2000, 1), 0) && !StaffRules.WithinLimit(Asking(2000, 1), -5), "agreed by itself: 0 is off, whatever is asked");
        int most50 = 1000 * CompanyCatalogue.AgreeUpToMost / 100;
        Check(StaffRules.WithinLimit(Asking(1000, most50), 999) && !StaffRules.WithinLimit(Asking(1000, most50 + 1), 999), "agreed by itself: a limit past its most is held to the most (" + CompanyCatalogue.AgreeUpToMost + "% of the pay)");
        StaffFile none = Asking(2000, 100);
        none.AskDay = 0;
        StaffFile nothing = Asking(2000, 100);
        nothing.AskTo = 2000;
        StaffFile under = Asking(2000, 100);
        under.AskTo = 1900;
        Check(!StaffRules.WithinLimit(none, CompanyCatalogue.AgreeUpToMost) && !StaffRules.WithinLimit(nothing, CompanyCatalogue.AgreeUpToMost) && !StaffRules.WithinLimit(under, CompanyCatalogue.AgreeUpToMost),
            "agreed by itself: nobody waiting, a request for nothing and one for less than they are paid are no request");
        StaffFile raised = Worker(2000, 2700);
        raised.Raise = 500;   // paid 25.00 with the agreed raise: a request for 26.25 is 5% of that, and 6.25% of the game's wage alone
        raised.AskDay = 10;
        raised.AskTo = 2625;
        Check(StaffRules.WithinLimit(raised, 5) && !StaffRules.WithinLimit(raised, 4), "agreed by itself: the share is of what they are paid now, the agreed raise included");
        StaffFile free = Worker(0, 500);
        free.AskDay = 10;
        free.AskTo = 500;
        StaffFile huge = Worker(int.MaxValue / 4, int.MaxValue / 4 + 1000);
        huge.AskDay = 10;
        huge.AskTo = huge.Going;
        StaffFile vast = Worker(1, int.MaxValue / 2);
        vast.AskDay = 10;
        vast.AskTo = vast.Going;
        Check(!StaffRules.WithinLimit(free, CompanyCatalogue.AgreeUpToMost) && StaffRules.WithinLimit(huge, 1) && !StaffRules.WithinLimit(vast, CompanyCatalogue.AgreeUpToMost),
            "agreed by itself: pay of nothing is never a share to ask by, and the largest figures do not overflow");
    }

    // The players' cases: the thirteen rows of the Staff page in the screenshot (paid, going rate asked) at each limit, and a request for 3%.
    private static void RaiseLimitCases()
    {
        int[][] rows =
        {
            new[] { 1859, 2044 }, new[] { 2212, 2371 }, new[] { 1614, 1778 }, new[] { 1751, 1928 }, new[] { 1598, 1760 }, new[] { 1523, 1632 }, new[] { 2987, 3265 },
            new[] { 3437, 3747 }, new[] { 2793, 3069 }, new[] { 3210, 3511 }, new[] { 3042, 3334 }, new[] { 3267, 3569 }, new[] { 3215, 3479 }
        };
        int[] limits = { 7, 8, 9, 10, 11 }, wanted = { 0, 2, 3, 10, 13 };
        string said = string.Empty;
        bool all = true;
        for (int l = 0; l < limits.Length; l++)
        {
            int agreed = 0;
            foreach (int[] row in rows)
            {
                StaffFile f = Worker(row[0], row[1]);
                f.AskDay = 10;
                f.AskTo = row[1];
                agreed += StaffRules.WithinLimit(f, limits[l]) ? 1 : 0;
            }
            all &= agreed == wanted[l];
            said += " " + limits[l] + "%: " + agreed + (agreed == wanted[l] ? string.Empty : " (wanted " + wanted[l] + ")") + ";";
        }
        Check(all, "agreed by itself: of the thirteen requests on a player's Staff page (paid 15.23 to 34.37, each asking the going rate, 7 to 10 per cent more) 0, 2, 3, 10 and 13 are agreed at 7, 8, 9, 10 and 11 per cent:" + said);
        StaffFile three = Asking(2000, 60), more = Asking(2000, 61);
        Check(StaffRules.WithinLimit(three, 3) && !StaffRules.WithinLimit(three, 2) && !StaffRules.WithinLimit(more, 3),
            "agreed by itself: a request for 3% of the pay is agreed at a limit of 3% and not at 2%, and one for a hundredth more is not agreed at 3%");
    }

    // ---------------------------------------------------------------- the morning's pass

    // What one morning does with a crew as CompanyDirector.StaffDay does it (its source is pinned below): each person's day, which is what asks, then a request inside the limit that nobody else
    // answers is agreed as the player's own Agree is. Returns how many.
    private static int MorningPass(List<StaffFile> crew, int limit, HashSet<string> others, int day, out int raised)
    {
        int agreed = 0;
        raised = 0;
        foreach (StaffFile f in crew)
        {
            StaffRules.Day(f, CompanyCatalogue.Standard, day);
            if (f.AskDay > 0 && limit > 0 && StaffRules.WithinLimit(f, limit) && !others.Contains(f.Id))
            {
                raised += StaffRules.RaiseFor(f, RaiseAnswer.Agree);
                StaffRules.Answer(f, RaiseAnswer.Agree, day);
                agreed++;
            }
        }
        return agreed;
    }

    // Forty people short of the going rate by 11, 15, 20, 25 and 30 per cent of it: on Standard each asks on the seventh day, for 12, 18, 25, 33 and 43 per cent more than they are paid.
    private static List<StaffFile> AskingCrew(out int[] gaps)
    {
        gaps = new[] { 110, 150, 200, 250, 300 };
        var crew = new List<StaffFile>();
        for (int i = 0; i < 40; i++)
        {
            int wage = 1200 + i * 90;
            crew.Add(Worker(wage, wage * 1000 / (1000 - gaps[i % gaps.Length])));
        }
        return crew;
    }

    private static void RaiseMorningPass()
    {
        List<StaffFile> crew = AskingCrew(out int[] gaps);
        var handled = new HashSet<string> { crew[0].Id, crew[5].Id };   // two of the people inside the limit, whose manager answers
        int agreed = 0, raised = 0;
        for (int day = 1; day <= 7; day++)
        {
            agreed += MorningPass(crew, 15, handled, day, out int more);
            raised += more;
        }
        int inside = 0, waiting = 0;
        bool right = true, left = true;
        for (int i = 0; i < crew.Count; i++)
        {
            StaffFile f = crew[i];
            bool within = gaps[i % gaps.Length] == 110;
            inside += within && !handled.Contains(f.Id) ? 1 : 0;
            waiting += f.AskDay > 0 ? 1 : 0;
            if (within && !handled.Contains(f.Id))
            {
                right &= f.AskDay == 0 && f.Paid == f.Going && f.Raise > 0 && f.Standing == CompanyCatalogue.StandingStart - 7 + CompanyCatalogue.StandAgree && f.AskRest == 7 + CompanyCatalogue.RaiseRestDays;
            }
            else
            {
                left &= f.AskDay == 7 && f.AskTo == f.Going && f.Raise == 0;
            }
        }
        Check(agreed == inside && inside == 6 && waiting == crew.Count - inside, "agreed by itself: on the seventh morning the six requests inside 15% that nobody else answers are agreed and the other " + waiting + " wait for the player (" + agreed + " agreed)");
        Check(right, "agreed by itself: each person agreed is paid the going rate with the standing and the rest an agreed request gives");
        Check(left, "agreed by itself: a request past the limit, and one a manager answers, are left as they were for the player");
        long sum = 0;
        foreach (StaffFile f in crew)
        {
            sum += f.Raise;
        }
        Check(raised == sum && raised > 0, "agreed by itself: the raises told in the notice (" + raised + ") are the raises given (" + sum + ")");
        bool quiet = true;
        for (int day = 8; day <= 30; day++)
        {
            MorningPass(crew, 15, handled, day, out _);
            for (int i = 0; i < crew.Count; i++)
            {
                quiet &= !(gaps[i % gaps.Length] == 110 && !handled.Contains(crew[i].Id) && crew[i].AskDay > 0);
            }
        }
        Check(quiet, "agreed by itself: somebody agreed is paid the going rate and does not ask again");

        // Off, the pass changes nothing: a crew run through it at 0 is a crew run through the days alone.
        List<StaffFile> a = AskingCrew(out _), b = AskingCrew(out _);
        int none = 0;
        for (int day = 1; day <= 30; day++)
        {
            none += MorningPass(a, 0, new HashSet<string>(), day, out _);
            foreach (StaffFile f in b)
            {
                StaffRules.Day(f, CompanyCatalogue.Standard, day);
            }
        }
        bool same = true;
        for (int i = 0; i < a.Count; i++)
        {
            same &= a[i].Raise == b[i].Raise && a[i].Standing == b[i].Standing && a[i].AskDay == b[i].AskDay && a[i].AskTo == b[i].AskTo && a[i].AskRest == b[i].AskRest && a[i].UnderDays == b[i].UnderDays;
        }
        Check(none == 0 && same, "agreed by itself: with the option at 0 nothing is agreed and every person is exactly as the days alone leave them");
    }

    // ---------------------------------------------------------------- the screens and the director's source

    private static void RaiseAnswerScreens()
    {
        CompanyFacts facts = UiSamples.CompanyFactsOf(UiWords("en"), "staff_summary");
        var drawn = new List<int>();
        CompanyCard top = facts.Staff != null ? facts.Staff.Top : null;
        for (int i = 0; top != null && top.Blocks != null && i < top.Blocks.Length; i++)
        {
            CompanyBlock block = top.Blocks[i];
            for (int b = 0; block.Kind == CompanyBlockKind.Buttons && block.Buttons != null && b < block.Buttons.Length; b++)
            {
                if (block.Buttons[b].Act.Do == CompanyDo.RaisesAll)
                {
                    drawn.Add(block.Buttons[b].Act.A);
                }
            }
        }
        Check(drawn.Count == 3 && drawn[0] == 0 && drawn[1] == 2 && drawn[2] == 1, "offer less to all: the Staff page draws Agree to all, Offer less to all and Refuse all, in that order (" + string.Join(",", drawn) + ")");
        string live = ModSource(@"Ui\Company\CompanyLive.Staff.cs"), does = ModSource(@"Ui\Company\CompanyLiveDoes.cs"), app = ModSource(@"Economy\Company\CompanyApp.Staff.cs");
        Check(live.Contains("Btn(CompanyText.T(\"reallife_co_staff_less_all\"), UiButtonKind.Secondary, CompanyAct.Of(CompanyDo.RaisesAll, null, 2), UiAction.Option)"),
            "offer less to all: the live Staff page draws the button, as the person's own page draws Offer less");
        Check(does.Contains("d.AnswerAllRaises(a.A == 0 ? RaiseAnswer.Agree : a.A == 2 ? RaiseAnswer.Less : RaiseAnswer.Refuse);"), "offer less to all: the press map answers every request with the offer-less answer");
        Check(app.Contains("InfoBox.Button(buttons, InfoBox.Additional, \"reallife_co_staff_less_all\", () => _director.AnswerAllRaises(RaiseAnswer.Less));"),
            "offer less to all: the game-style Staff page has it too, between Agree to all and Refuse all");
        Check(ModSource(@"Ui\UiScreens.Company.cs").Contains("\"reallife_co_staff_less_all\""), "offer less to all: the Company screen names the button's text, so every language is held to it");
    }

    private static void RaiseAnswerDirector()
    {
        string staff = CompanySource("CompanyDirector.Staff.cs");
        string day = CompanyMethod(staff, "partial void StaffDay(int day)"), all = CompanyMethod(staff, "public void AnswerAllRaises(RaiseAnswer answer)");
        string others = CompanyMethod(staff, "private bool RaiseWithOthers(StaffFile f)"), limit = CompanyMethod(staff, "private static int AgreeLimit()");
        string rule = CompanyMethod(staff, "private bool AgreesByItself(StaffFile f, int limit)"), now = CompanyMethod(staff, "private int AgreeNow(StaffFile f, int limit, int day)");
        Check(day.Length > 0 && InOrder(day, "int limit = AgreeLimit()", "StaffRules.Day(f, _level, day", "AgreesByItself(f, limit)", "AgreeNow(f, limit, day)"),
            "agreed by itself: the day's pass reads the limit once, then after each person's day agrees a request inside it that nobody else answers");
        Check(rule.Contains("f.AskDay > 0") && rule.Contains("StaffRules.WithinLimit(f, limit)") && rule.Contains("!RaiseWithOthers(f)") && now.Contains("StaffRules.RaiseFor(f, RaiseAnswer.Agree)")
              && now.Contains("StaffRules.Answer(f, RaiseAnswer.Agree, day);"),
            "agreed by itself: a request is agreed when it waits, asks the share or less and nobody else answers it, and is answered as the player's own Agree is (StaffRules.Answer)");
        Check(day.Contains("!agree && note == StaffNote.Asked") && InOrder(day, "agreed++", "\"reallife_co_note_raise_auto_one\"", "\"reallife_co_note_raise_auto_many\""),
            "agreed by itself: a request agreed is not also asked about, and one notice tells how many were agreed");
        string tests = CompanySource("CompanyDirector.Tests.cs");
        string test = CompanyMethod(tests, "public void TestAgree()");
        Check(test.Length > 0 && test.Contains("AgreeLimit()") && test.Contains("!RaiseWithOthers(x)") && test.Contains("AgreesByItself(f, limit)") && test.Contains("AgreeNow(f, limit, Save.Day)")
              && test.Contains("\"reallife_co_test_agree_off\"") && test.Contains("\"reallife_co_test_agree_none\"") && test.Contains("BeforeTest();")
              && ModSource(@"Core\RealLifeOverhaulMod.cs").Contains(".AddButton(\"reallife_co_test_agree\", () => Economy.CompanyDirector.Instance?.TestAgree())"),
            "agreed by itself: a test button asks for a request inside the limit and agrees it at once by the day's own steps, says why not while the option is 0, and can be put back");
        Check(others.Contains("Manages(f.Place, ManagerTask.Raises, f.Id)") && others.Contains("OpenExceptionFor(f.Id)") && others.Contains("HierarchyOn")
              && others.Contains("ExecutiveAtWork(EscalationRules.AreaOf(EscalationKind.Demand))") && others.Contains("Working(mp.Own)"),
            "agreed by itself: a request a manager answers, an exception waiting for its approver, and any at a place with a manager at work while the people executive is in post are left to them");
        Check(limit.Contains("CompanyCatalogue.Clamp(Settings.EconomyCompanyAgreeUpTo, 0, CompanyCatalogue.AgreeUpToMost)"), "agreed by itself: the option is read held to its ends");
        Check(all.Length > 0 && all.Contains("answer == RaiseAnswer.Bonus") && all.Contains("answer == RaiseAnswer.Less ? \"reallife_co_note_raise_all_less\"") && all.Contains("StaffRules.RaiseFor(f, answer)")
              && all.Contains("StaffRules.Answer(f, answer, Save.Day)"),
            "offer less to all: every request is answered as its own answer is, a bonus is not an answer for all, and offering less has its own notice");
    }

    private static void RaiseAnswerOption()
    {
        string settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), list = ModSource(@"Meta\FeatureList.Economy.cs");
        Check(settings.Contains("public const string EconomyCompanyAgreeUpToId = \"economy_company_agree_up_to\";") && settings.Contains("public static int EconomyCompanyAgreeUpTo = 0;")
              && settings.Contains("EconomyCompanyAgreeUpTo = Int(modId, EconomyCompanyAgreeUpToId, EconomyCompanyAgreeUpTo);"),
            "agreed by itself: the option is saved with the others and starts at 0, which is off");
        Check(mod.Contains(".AddSlider(Settings.EconomyCompanyAgreeUpToId, \"reallife_co_opt_agree_up_to\", 0, Economy.CompanyCatalogue.AgreeUpToMost, Settings.EconomyCompanyAgreeUpTo,")
              && mod.Contains("v => Settings.EconomyCompanyAgreeUpTo = v, Meta.SettingsTable.SliderPercent)"),
            "agreed by itself: a slider on the options page from 0 to its most, shown as a per cent");
        Check(list.Contains("Settings.EconomyCompanyAgreeUpToId"), "agreed by itself: the option is the Company feature's");
        TreeSection section = OptionsTree.SectionOf("economy_company_agree_up_to");
        TreeRow row = OptionsTree.Row("economy_company_agree_up_to");
        Check(section != null && section.Id == "company" && row != null && Array.IndexOf(row.Needs, "economy_company_staff") >= 0 && !row.Free && section.Rows.Count <= OptionsTree.MostRows
              && row.TipKey == "reallife_opt_tip_economy_company_agree_up_to",
            "agreed by itself: the option stands under Running a business in the options tree, kept back while the staff part is off, with its sentence");
        Check(CompanyCatalogue.AgreeUpToMost > 0 && CompanyCatalogue.AgreeUpToMost <= 100, "agreed by itself: its most is a share of the pay between 1 and 100 per cent");
    }

    // ---------------------------------------------------------------- the wage in My Employees' review before hiring

    private static void RaiseHireReview()
    {
        string batch = ModSource(@"Ui\Employees\StaffBatchHire.cs"), shown = ModSource(@"Core\WageShown.cs");
        Check(batch.Contains("Wages.Add(WageShown.PaidCents(e));") && !batch.Contains("e.hourlyWage * 100f"),
            "one wage: the review before hiring several candidates shows what each costs an hour at the city's prices, the figure of the candidates' table, not the game's");
        Check(shown.Contains("internal static int PaidCents(EmployeeInstance e)") && shown.Contains("Ours(e) ? PaidWage.Now(e.hourlyWage, e.id) : PaidWage.Base(e.hourlyWage)"),
            "one wage: that figure is the paid wage of somebody this machine pays, and the game's own for another player's candidate");
        // At prices 25% up, candidates asking 18.00, 21.00 and 25.50 cost 22.50, 26.25 and 31.88 an hour: the review's total is the sum of those it will hire.
        int level = PriceRules.Level(1250);
        int[] wages = { PaidWage.Cents(1800, level, 0), PaidWage.Cents(2100, level, 0), PaidWage.Cents(2550, level, 0) };
        StaffSheet sheet = StaffBatchRules.HireSheet(new[] { "a", "b", "c" }, new[] { "A", "B", "C" }, new[] { "r", "r", "r" }, wages, new[] { BatchState.Ready, BatchState.Ready, BatchState.NoFit }, "Shop");
        Check(wages[0] == 2250 && wages[1] == 2625 && wages[2] == 3188 && sheet.Now == 2250 + 2625 && sheet.Next == sheet.Now && sheet.Left == 1,
            "one wage: a review of hiring adds up the wages paid of those it will hire (" + sheet.Now + ")");
    }

    // ---------------------------------------------------------------- the texts

    private static void RaiseAnswerTexts()
    {
        string[] keys =
        {
            "reallife_co_staff_less_all", "reallife_co_note_raise_all_less", "reallife_co_opt_agree_up_to", "reallife_opt_tip_economy_company_agree_up_to",
            "reallife_co_note_raise_auto_one", "reallife_co_note_raise_auto_many", "reallife_co_test_agree", "reallife_co_test_agree_off", "reallife_co_test_agree_none"
        };
        Check(AllIn(keys, out string missing), "raises answered together and by themselves: every text in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        string less = en.TryGetValue("reallife_co_note_raise_all_less", out string t1) ? t1 : string.Empty, one = en.TryGetValue("reallife_co_note_raise_auto_one", out string t2) ? t2 : string.Empty;
        string many = en.TryGetValue("reallife_co_note_raise_auto_many", out string t3) ? t3 : string.Empty, label = en.TryGetValue("reallife_co_opt_agree_up_to", out string t4) ? t4 : string.Empty;
        string tip = en.TryGetValue("reallife_opt_tip_economy_company_agree_up_to", out string t5) ? t5 : string.Empty, offer = en.TryGetValue("reallife_co_staff_less_all", out string t6) ? t6 : string.Empty;
        Check(Fits(en, "reallife_co_note_raise_all_less") && Fits(en, "reallife_co_note_raise_auto_one") && Fits(en, "reallife_co_note_raise_auto_many") && Fits(en, "reallife_co_test_agree_off")
              && Fits(en, "reallife_co_test_agree_none"), "raises answered together and by themselves: the notices are 240 characters or fewer");
        string button = en.TryGetValue("reallife_co_test_agree", out string t7) ? t7 : string.Empty;
        Check(button.Length > 0 && button.Length <= 80 && button.Split(' ').Length <= 13 && char.IsUpper(button[0]) && !button.StartsWith("Test", StringComparison.OrdinalIgnoreCase),
            "agreed by itself: the test button's label keeps to the options page's limits for a test");
        Check(less.Contains("{count}") && less.Contains("{raise}") && one.Contains("{name}") && one.Contains("{raise}") && one.Contains("{limit}") && one.Contains("{standing}")
              && many.Contains("{count}") && many.Contains("{raise}") && many.Contains("{limit}"), "raises answered together and by themselves: each notice names what it counts");
        Check(label.Split(' ').Length <= 9 && label.Length <= 60 && label.Length > 0 && !label.EndsWith(".") && tip.Split(' ').Length <= 32 && tip.Length <= 190 && tip.EndsWith("."),
            "agreed by itself: the option's label and sentence keep to the options page's limits (" + label.Split(' ').Length + " words, " + tip.Split(' ').Length + " words)");
        bool plain = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LocaleFile(code);
            foreach (string key in keys)
            {
                string v = words.TryGetValue(key, out string text) ? text : string.Empty;
                plain &= v.Length > 0 && v.IndexOf('!') < 0 && v.IndexOf((char)0x2014) < 0 && v.IndexOf((char)0x2013) < 0 && !v.Contains("  ");
            }
        }
        Check(plain, "raises answered together and by themselves: no text has a dash, an exclamation mark or a double space in any of the three languages");
        // The Guide names the two as they are worded: StandardCheck's bold names must be texts of the mod.
        Check(en.TryGetValue("reallife_guide_economy_company", out string guide) && guide.Contains("<b>" + offer + "</b>") && guide.Contains("<b>" + label + "</b>") && guide.Contains("<b>Give a raise</b>")
              && guide.Contains("{co_raise_rest} days for the next"), "raises answered together and by themselves: the Guide's page on running a business names Offer less to all and the option as they are worded");
        Check(WhatsNewHas(en, "Offer less to all beside Agree to all and Refuse all") && WhatsNewHas(en, "agrees raises automatically") && WhatsNewHas(en, "Fixed: the review before hiring several candidates in My Employees"),
            "raises answered together and by themselves: What's new has the three lines in English, in any of its rows");
    }
}
