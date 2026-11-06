using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Buying out a notice (Tech/Hiring): the player pays an accepted hire's employer so they start tomorrow, and a rival that has poached
// one of the player's senior people may buy out theirs by the same rule and pay the player. The price, who can be bought out, the
// rival's seeded once-only choice, the money's keys, the app's question, the day's place, the tests and the texts.
internal static partial class Program
{
    private static void Buyout()
    {
        BuyoutPrice();
        BuyoutApplicant();
        BuyoutRival();
        BuyoutSources();
        BuyoutTexts();
    }

    private static void BuyoutPrice()
    {
        Check(HiringCatalogue.BuyoutBp == 15000, "notice buy out: the price is one and a half times the pay for the days saved");
        Check(HiringRules.NoticePay(7, 3000) == 7L * 3000 * TalentCatalogue.WeekHours / 7, "notice buy out: a week of notice is a week's hours of pay");
        Check(HiringRules.BuyoutPrice(7, 3000) == 7 * 3000 * TalentCatalogue.WeekHours / 7 * 3 / 2, "notice buy out: the price of a week at 30.00 an hour is a week's pay and half as much again");
        Check(HiringRules.BuyoutPrice(0, 3000) == 0 && HiringRules.BuyoutPrice(-4, 3000) == 0 && HiringRules.BuyoutPrice(10, 0) == 0 && HiringRules.BuyoutPrice(10, -50) == 0,
            "notice buy out: no days or no pay cost nothing, never less");
        Check(HiringRules.BuyoutPrice(20, 6000) > HiringRules.BuyoutPrice(10, 6000) && HiringRules.BuyoutPrice(10, 9000) > HiringRules.BuyoutPrice(10, 6000),
            "notice buy out: more days or more pay cost more");
        Check(HiringRules.BuyoutPrice(1000000, int.MaxValue) == int.MaxValue, "notice buy out: the largest price is held at the most a sum can be");
        Check(HiringRules.BuyoutDays(23, 7) == 16 && HiringRules.BuyoutDays(7, 7) == 0 && HiringRules.BuyoutDays(5, 9) == 0, "notice buy out: the days saved, none when it is not sooner");
        Check(HiringRules.BuyoutStart(40) == 40 + HiringCatalogue.StartLeast, "notice buy out: a bought out hire starts tomorrow");
        Check(HiringRules.NoticeFor(TalentCatalogue.Junior) == 7 && HiringRules.NoticeFor(TalentCatalogue.Senior) == 17 && HiringRules.NoticeFor(TalentCatalogue.Principal) == 27,
            "notice buy out: a week of notice, five days more a level");
        Seeker s = HiringRules.NewSeeker(4, 3, 1, false, TalentCatalogue.SoftwareEngineer, TalentCatalogue.Staff, FlatRate, 1000, "d");
        Check(s.Notice == (s.Employed ? HiringRules.NoticeFor(TalentCatalogue.Staff) : 0), "notice buy out: a seeker's notice is the level's, none out of work");
    }

    private static void BuyoutApplicant()
    {
        var s = new Seeker { Id = 1, Employed = true, Notice = 17 };
        var a = new Applicant { Id = 2, Seeker = 1, Stage = (int)ApplyStage.Accepted, Offer = 6000, Start = 10 + 17 };
        Check(HiringRules.CanBuyOut(a, s, 10) == HiringRules.Buyable, "notice buy out: an accepted hire in work with days to go can be bought out");
        int quote = HiringRules.BuyoutQuote(a, s, 10);
        Check(quote == HiringRules.BuyoutPrice(16, 6000), "notice buy out: the quote is the rule's price for the days saved at the pay agreed");
        Check(HiringRules.BuyoutQuote(a, s, 11) < quote, "notice buy out: a day later saves a day less and costs less, never more");
        Check(HiringRules.BuyOut(a, s, 10) == 16 && a.Start == 11, "notice buy out: bought out, they start tomorrow, sixteen days sooner");
        Check(HiringRules.CanBuyOut(a, s, 10) == HiringRules.BuyoutTooSoon && HiringRules.BuyoutQuote(a, s, 10) == 0 && HiringRules.BuyOut(a, s, 10) == 0 && a.Start == 11,
            "notice buy out: never twice, and nothing changes the second time");
        var idle = new Seeker { Id = 3, Employed = false };
        var b = new Applicant { Id = 4, Seeker = 3, Stage = (int)ApplyStage.Accepted, Offer = 5000, Start = 13 };
        Check(HiringRules.CanBuyOut(b, idle, 10) == HiringRules.BuyoutNotEmployed && HiringRules.BuyOut(b, idle, 10) == 0 && b.Start == 13,
            "notice buy out: someone out of work has no notice to buy");
        foreach (ApplyStage stage in new[] { ApplyStage.New, ApplyStage.Offered, ApplyStage.Countered, ApplyStage.Started, ApplyStage.Withdrawn })
        {
            var c = new Applicant { Stage = (int)stage, Offer = 5000, Start = 40 };
            if (HiringRules.CanBuyOut(c, s, 10) != HiringRules.BuyoutNotAccepted)
            {
                Check(false, "notice buy out: only an accepted hire can be bought out (" + stage + ")");
            }
        }
        Check(HiringRules.CanBuyOut(null, s, 10) == HiringRules.BuyoutNotAccepted && HiringRules.CanBuyOut(a, null, 10) == HiringRules.BuyoutNotAccepted,
            "notice buy out: nobody to buy out is refused");

        // A crew search's hire waits out a notice the same way, and is bought out by the same rule.
        HiringFile f = NewLabour(21, 5);
        var crew = new JobAd { Id = f.Next(), Place = "Dock%20St~4", Role = TalentCatalogue.Technician, Level = TalentCatalogue.Mid, Search = true, Crew = 8, From = 5, To = 30, Due = 7 };
        f.Ads.Add(crew);
        HiringRules.DeliverCrew(f, crew, 7, FlatRate, 1000, null);
        Applicant waiting = null;
        for (int i = 0; i < f.Applicants.Count && waiting == null; i++)
        {
            Applicant x = f.Applicants[i];
            Seeker y = f.SeekerOf(x.Seeker);
            if (x.Ad == crew.Id && y.Employed)
            {
                HiringRules.Offer(x, y.Ask * 2, 7);
                HiringRules.Answer(f, x, y, 8, 0, 1000);
                waiting = x;
            }
        }
        Seeker ws = waiting != null ? f.SeekerOf(waiting.Seeker) : null;
        Check(waiting != null && waiting.At == ApplyStage.Accepted && waiting.Start == 8 + ws.Notice && HiringRules.CanBuyOut(waiting, ws, 8) == HiringRules.Buyable,
            "notice buy out: a data centre crew's hire in work waits out their notice and can be bought out");
    }

    private static void BuyoutRival()
    {
        Profile p = Poached("pro:7", TalentCatalogue.Principal, 30, 30 + 21);
        Check(HiringRules.NoticeServed(p) == 30, "rival buy out: the day the notice was handed in comes from the answer, with no field of its own");
        int seed = 0;
        for (int s = 1; s < 400 && seed == 0; s++)
        {
            seed = HiringRules.RivalBuysOut(s, p, 31) ? s : 0;
        }
        Check(seed > 0, "rival buy out: a rival can buy out a principal's notice the day after it is handed in");
        Check(!HiringRules.RivalBuysOut(seed, p, 30) && !HiringRules.RivalBuysOut(seed, p, 32) && !HiringRules.RivalBuysOut(seed, p, 40),
            "rival buy out: only the day after the notice was handed in, so once");
        Check(HiringRules.RivalBuysOut(seed, p, 31) == HiringRules.RivalBuysOut(seed, Poached("pro:7", TalentCatalogue.Principal, 30, 51), 31),
            "rival buy out: the same save gives the same answer on every machine");
        Profile resigned = Poached("pro:7", TalentCatalogue.Principal, 30, 51);
        resigned.Leaving = (int)LeaveKind.Resigned;
        Profile laidOff = Poached("pro:7", TalentCatalogue.Principal, 30, 51);
        laidOff.Leaving = (int)LeaveKind.LaidOff;
        Profile ending = Poached("pro:7", TalentCatalogue.Principal, 30, 31);
        Check(!HiringRules.RivalBuysOut(seed, resigned, 31) && !HiringRules.RivalBuysOut(seed, laidOff, 31) && !HiringRules.RivalBuysOut(seed, ending, 31)
              && !HiringRules.RivalBuysOut(seed, null, 31), "rival buy out: only a notice for a rival, with days still to save");
        int[] bought = new int[TalentCatalogue.Levels];
        for (int n = 0; n < 2000; n++)
        {
            for (int level = 0; level < TalentCatalogue.Levels; level++)
            {
                bought[level] += HiringRules.RivalBuysOut(17, Poached("pro:" + n, level, 30, 51), 31) ? 1 : 0;
            }
        }
        Check(bought[TalentCatalogue.Junior] == 0 && bought[TalentCatalogue.Mid] == 0, "rival buy out: never a junior or a mid, whom rivals do not poach");
        Check(bought[TalentCatalogue.Senior] > 400 && bought[TalentCatalogue.Senior] < 600 && bought[TalentCatalogue.Staff] > bought[TalentCatalogue.Senior]
              && bought[TalentCatalogue.Principal] > bought[TalentCatalogue.Staff] && bought[TalentCatalogue.Principal] < 1250,
            "rival buy out: about a quarter of seniors, more often the more senior (" + string.Join(", ", bought) + " of 2000)");
        Check(HiringCatalogue.RivalBuyoutChance.Length == TalentCatalogue.Levels, "rival buy out: a chance for every level");
    }

    // A professional who answered a rival on a day and works their notice for it until the day given.
    private static Profile Poached(string id, int level, int answered, int end)
    {
        return new Profile { Id = id, Level = level, Leaving = (int)LeaveKind.Poached, NoticeEnd = end, PoachDay = answered + TalentCatalogue.PoachRestDays, PoachBy = "Skyvault Systems" };
    }

    private static void BuyoutSources()
    {
        string hiring = ModSource(@"Tech\TechDirector.Hiring.cs");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        string page = ModSource(@"Tech\People\RolesApp.Hiring.cs");
        string tests = ModSource(@"Tech\TechDirector.Tests.cs");
        string game = ModSource(@"Tech\Hiring\HiringGame.cs");
        string guide = ModSource(@"Tech\TechGuide.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string list = ModSource(@"Meta\FeatureList.Economy.cs");
        string lines = ModSource(@"Economy\BizLines.cs");
        string buy = MethodText(hiring, "internal bool BuyOutNotice(int id)");
        Check(buy.Contains("string refusal = BuyoutRefusal(a, s);") && buy.Contains("HiringRules.BuyoutQuote(a, s, day)") && buy.Contains("HiringGame.Pay(price, HiringGame.BuyoutKey, at, s.P.Name)")
              && buy.IndexOf("HiringGame.Pay(", StringComparison.Ordinal) < buy.IndexOf("HiringRules.BuyOut(a, s, day);", StringComparison.Ordinal)
              && buy.Contains("\"reallife_hire_buyout_short\"") && buy.Contains("\"reallife_hire_note_buyout\"") && buy.Contains("BuyoutCheck("),
            "notice buy out sources: refused with the reason, priced afresh, paid whole by the business before the start moves, told and checked");
        string refuse = MethodText(hiring, "internal string BuyoutRefusal(Applicant a, Seeker s)");
        Check(refuse.Contains("!_hiringOn") && refuse.Contains("\"reallife_hire_buyout_off\"") && refuse.Contains("\"reallife_hire_buyout_not_employed\"") && refuse.Contains("\"reallife_hire_buyout_soon\""),
            "notice buy out sources: the hiring option off, out of work, or no notice left, each refused with its own words");
        string check = MethodText(hiring, "private void BuyoutCheck(string who, int day, int end, int now, int wanted, int price, int centsHour)");
        Check(check.Contains("if (!Verbose)") && check.Contains("Tech check (notice buy out, ") && check.Contains("(ok ? \"PASS\" : \"FAIL\")") && check.Contains("HiringRules.BuyoutPrice(saved, centsHour)"),
            "notice buy out sources: one self-check line a buy out under Developer diagnostics, the price against the rule's");
        Check(page.Contains("_director.BuyoutRefusal(a, s)") && page.Contains("GameConfirm.Show(") && page.Contains("\"reallife_hire_buyout_body\"") && page.Contains("_director.BuyOutNotice(id)")
              && page.Contains("else if (a.At == ApplyStage.Accepted)") && !page.Contains("HudConfirm"),
            "notice buy out sources: an accepted hire's page asks through the game's question box, as a lay off does");
        string day = MethodText(people, "private void PeopleDay(int day)");
        Check(day.Contains("if (_hiringOn && HiringRules.RivalBuysOut(Seed, p, day))") && day.IndexOf("HiringRules.RivalBuysOut(", StringComparison.Ordinal)
              < day.IndexOf("if (p.NoticeEnd > 0 && day >= p.NoticeEnd)", StringComparison.Ordinal) && Occurs(people + hiring, "HiringRules.RivalBuysOut(") == 1,
            "rival buy out sources: decided in one place, the professionals' day, with the hiring option on, before the day lets them go");
        string rival = MethodText(people, "private bool RivalBuyout(Person person, int day)");
        Check(rival.Contains("HiringRules.BuyoutPrice(HiringRules.BuyoutDays(end, day), p.GameWage)") && rival.Contains("HiringGame.Receive(price, HiringGame.BuyoutPaidKey, person.At, person.Name)")
              && rival.Contains("p.NoticeEnd = day;") && rival.Contains("\"reallife_talent_note_buyout\"") && rival.Contains("BuyoutCheck("),
            "rival buy out sources: the same price at their pay, paid to the player, they go today, told with the amount and checked");
        Check(game.Contains("public const string BuyoutKey = \"reallife_transaction_tech_recruitment_buyout\"") && game.Contains("public const string BuyoutPaidKey = \"reallife_transaction_tech_recruitment_buyout_paid\"")
              && lines.Contains("P + \"tech_recruitment_buyout\", P + \"tech_recruitment_buyout_paid\""),
            "notice buy out sources: the money has keys of its own, among the businesses' recruiting costs");
        Check(RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_tech_recruitment_buyout") == RealLifeOverhaul.Economy.BizLine.Cost
              && RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_tech_recruitment_buyout_paid") == RealLifeOverhaul.Economy.BizLine.Cost,
            "notice buy out sources: BizMan counts both as recruiting costs, the rival's money against them");
        Check(tests.Contains("public void TestBuyout()") && tests.Contains("public void TestRivalBuyout()") && tests.Contains("\"reallife_hire_test_no_accepted\"") && tests.Contains("\"reallife_talent_test_buyout_none\"")
              && mod.Contains("\"reallife_hire_test_buyout\", () => Tech.TechDirector.Instance?.TestBuyout()") && mod.Contains("\"reallife_talent_test_buyout\", () => Tech.TechDirector.Instance?.TestRivalBuyout()")
              && list.Contains("\"reallife_hire_test_buyout\", \"reallife_talent_test_buyout\""),
            "notice buy out sources: the two tests are in Options, on the hiring feature's list, and say why when they cannot run");
        Check(guide.Contains(".Fact(\"hire_buyout\", () => Factor(HiringCatalogue.BuyoutBp))"), "notice buy out sources: the Guide's factor comes from the catalogue");
    }

    private static void BuyoutTexts()
    {
        string[] keys =
        {
            "reallife_hire_button_buyout", "reallife_hire_buyout_line", "reallife_hire_buyout_title", "reallife_hire_buyout_body", "reallife_hire_note_buyout",
            "reallife_hire_buyout_short", "reallife_hire_buyout_not_employed", "reallife_hire_buyout_soon", "reallife_hire_buyout_off", "reallife_hire_buyout_gone",
            "reallife_talent_note_buyout", "reallife_transaction_tech_recruitment_buyout", "reallife_transaction_tech_recruitment_buyout_label",
            "reallife_transaction_tech_recruitment_buyout_paid", "reallife_transaction_tech_recruitment_buyout_paid_label", "reallife_hire_test_buyout",
            "reallife_hire_test_buyout_ready", "reallife_hire_test_no_accepted", "reallife_talent_test_buyout", "reallife_talent_test_buyout_none", "reallife_talent_test_buyout_unpaid"
        };
        Check(AllIn(keys, out string missing), "notice buy out texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool fits = true;
        foreach (string key in new[] { "reallife_hire_note_buyout", "reallife_hire_buyout_short", "reallife_hire_buyout_not_employed", "reallife_hire_buyout_soon", "reallife_hire_buyout_off",
                     "reallife_hire_buyout_gone", "reallife_talent_note_buyout", "reallife_hire_test_buyout_ready" })
        {
            fits &= Fits(en, key);
        }
        Check(fits, "notice buy out texts: the notices are 240 characters or fewer");
        Check(en.TryGetValue("reallife_hire_buyout_body", out string body) && body.Contains("{amount}") && body.Contains("{days}") && body.Contains("{day}") && body.Contains("{factor}"),
            "notice buy out texts: the question gives the price, the days saved, the new start day and the factor");
        Check(en.TryGetValue("reallife_guide_tech_jobs", out string page) && page.Contains("you can buy out the notice so they start tomorrow") && page.Contains("{hire_buyout} times their pay")
              && page.Contains("A rival that poaches one of your senior people may buy out their notice"), "notice buy out texts: the Guide's page on hiring tells of both sides");
        Check(WhatsNewHas(en, "Buy out an accepted hire's notice in Open Roles so they start tomorrow"), "notice buy out texts: What's new tells of it");
    }
}
