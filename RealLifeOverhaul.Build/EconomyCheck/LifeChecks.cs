using System;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// Checks for working life (Scripts/Careers/Work/LifeRules): a review's score and rating, rises, bonuses and promotion,
// asking for a rise, burnout, office matters and the boss's manner, a search firm's approach, redundancy, severance and
// benefit, share options, the pension, the top seats of a rival's company and a conflict of interest.
internal static partial class Program
{
    private static void Life()
    {
        LifeReviews();
        LifePromotion();
        LifeBurnout();
        LifeMatters();
        LifeMovingOn();
        LifeMoney();
    }

    private static Job LifeJob(int prof, int rank, EmployerKind kind = EmployerKind.City)
    {
        var j = new Job { Hired = 1, RankDay = 1 };
        new Opening { Prof = prof, Rank = rank, Kind = (int)kind, Employer = "Codeharbour Software" }.CopyTo(j.Post);
        j.Pay = ProCatalogue.Find(prof).Ranks[rank].Pay;
        return j;
    }

    private static void LifeReviews()
    {
        Job j = LifeJob(ProCatalogue.Developer, 1);
        j.Due = 20;
        j.Worked = 20;
        int steady = LifeRules.ReviewScore(j, 38, 38, BossKind.HandsOff);
        Check(LifeRules.Rating(steady) == 3, "life: a steady worker just up to the post is rated 3 (" + steady + ")");
        Check(LifeRules.Rating(LifeRules.ReviewScore(j, 56, 38, BossKind.HandsOff)) == 5, "life: well over the post and always there is a 5");
        j.Worked = 10;
        j.Absent = 10;
        Check(LifeRules.Rating(LifeRules.ReviewScore(j, 38, 38, BossKind.HandsOff)) <= 2, "life: missing half the days is a poor review");
        j.Worked = 20;
        j.Absent = 0;
        j.Late = 10;
        Check(LifeRules.ReviewScore(j, 38, 38, BossKind.Micromanager) < LifeRules.ReviewScore(j, 38, 38, BossKind.HandsOff) - 3, "life: a micromanager minds lateness twice");
        j.Late = 0;
        j.Overtime = 8 * 60;
        Check(LifeRules.ReviewScore(j, 38, 38, BossKind.Driver) > LifeRules.ReviewScore(j, 38, 38, BossKind.HandsOff), "life: a driver counts overtime twice");
        j.Overtime = 0;
        j.Favour = 10;
        Check(LifeRules.ReviewScore(j, 38, 38, BossKind.HandsOff) == steady + 10, "life: standing with colleagues counts");
        j.Favour = 0;
        j.Burnout = 800;
        Check(LifeRules.ReviewScore(j, 38, 38, BossKind.HandsOff) == steady - 8, "life: burnout costs eight");
        Check(LifeRules.ReviewScore(j, 38, 38, BossKind.Mentor) > LifeRules.ReviewScore(j, 38, 38, BossKind.Micromanager), "life: a mentor is kinder than a micromanager");
        Check(LifeRules.Rating(0) == 1 && LifeRules.Rating(35) == 2 && LifeRules.Rating(50) == 3 && LifeRules.Rating(65) == 4 && LifeRules.Rating(80) == 5 && LifeRules.Rating(100) == 5,
            "life: the rating's bands");
        Check(LifeRules.RiseFor(5, 0) == LifeRules.RiseBp * 2 && LifeRules.RiseFor(4, 0) == LifeRules.RiseBp && LifeRules.RiseFor(3, 0) == LifeRules.RiseBp / 2 && LifeRules.RiseFor(2, 0) == 0
              && LifeRules.RiseFor(5, LifeRules.RaisesMost) == 0, "life: rises by rating, none after a rank's last");
        Check(LifeRules.BonusDays(5) == LifeRules.BonusDays5 && LifeRules.BonusDays(4) == LifeRules.BonusDays4 && LifeRules.BonusDays(3) == 0 && LifeRules.BonusDays5 > LifeRules.BonusDays4,
            "life: bonuses of days' pay for a 4 and a 5");
        Check(LifeRules.DayPay(j) == (long)j.Pay * 8, "life: a day's pay is the rate by the working day");
        Check(LifeRules.ReviewEvery(60) == 14 && LifeRules.ReviewEvery(90) == 21 && LifeRules.ReviewEvery(120) == 28 && LifeRules.ReviewEvery(365) == 28,
            "life: a review every two weeks in the game's own year of 60 days, every four at most in a longer one");
        Check(LifeRules.WeekPay(j) == (long)j.Pay * 8 * 5, "life: a week's pay is the rate by the working week");
        Check(LifeRules.AskRiseChance(5) > LifeRules.AskRiseChance(4) && LifeRules.AskRiseChance(4) > LifeRules.AskRiseChance(3) && LifeRules.AskRiseChance(1) > 0, "life: asking works better after a good review");
    }

    private static void LifePromotion()
    {
        var f = new StudyFile();
        f.Results.Add(new Enrolment { Course = StudyCatalogue.ComputerScience, Stage = (int)EnrolStage.Passed, Grade = 3 });
        StudyRules.Learn(f, TalentCatalogue.Coding, 45000, 1);
        StudyRules.Learn(f, TalentCatalogue.SystemDesign, 35000, 1);
        Job j = LifeJob(ProCatalogue.Developer, 0);
        int days = LifeRules.Paced(ProCatalogue.Find(ProCatalogue.Developer).Ranks[1].Days, 60);
        Check(!LifeRules.CanPromote(f, j, 4, 1 + days - 1, 60), "life: not before the time at the rank");
        Check(LifeRules.CanPromote(f, j, 4, 1 + days, 60) && !LifeRules.CanPromote(f, j, 3, 1 + days, 60), "life: a 4 and the time brings a promotion; a 3 does not");
        Job top = LifeJob(ProCatalogue.Developer, 6);
        Check(!LifeRules.CanPromote(f, top, 5, 9999, 60), "life: nothing above the top seat");
        Job em = LifeJob(ProCatalogue.Developer, 4);
        StudyRules.Learn(f, TalentCatalogue.Coding, 50000, 1);
        StudyRules.Learn(f, TalentCatalogue.SystemDesign, 50000, 1);
        StudyRules.Learn(f, TalentCatalogue.Leadership, 70000, 1);
        StudyRules.Learn(f, TalentCatalogue.Delivery, 70000, 1);
        Job cto = LifeJob(ProCatalogue.Developer, 5);
        cto.Declared = true;
        Check(!LifeRules.CanPromote(f, cto, 5, 9999, 60), "life: a declared conflict closes the top seats");
        cto.Declared = false;
        Check(LifeRules.CanPromote(f, cto, 5, 9999, 60), "life: an engineering manager with the leadership can become chief technology officer");
        Check(LifeRules.CanPromote(f, em, 5, 9999, 60), "life: a principal engineer with the leadership can become an engineering manager");
        int pay = LifeRules.PromotedPay(j, 1);
        Check(pay >= j.Pay * (10000 + LifeRules.PromoteBp) / 10000 && pay >= ProRules.PostPay(j.Def, 1, EmployerKind.City, 1000), "life: a promotion pays at least its rise and the new rank's rate");
    }

    private static void LifeBurnout()
    {
        int mark = LifeRules.LongWeekHours * 60;
        Check(LifeRules.BurnoutFor(0, 480) == 0 && LifeRules.BurnoutFor(mark - 60, 60) == 0, "life: no burnout in a normal week");
        Check(LifeRules.BurnoutFor(mark - 60, 120) == LifeRules.BurnoutPerHour, "life: an hour over the long week is a step of burnout");
        Check(LifeRules.BurnoutFor(mark + 600, 60) == LifeRules.BurnoutPerHour, "life: every hour past it adds the same");
        Check(LifeRules.BurnoutMood < LifeRules.BurnoutSignedOff && LifeRules.BurnoutAfterSignedOff < LifeRules.BurnoutSignedOff, "life: the doctor signs off past the mood's mark, and the rest takes it down");
    }

    private static void LifeMatters()
    {
        bool keys = true, bounded = true;
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (Matter m in LifeRules.Matters)
        {
            keys &= seen.Add(m.Key);
            bounded &= Math.Abs(m.FavourA) <= 6 && Math.Abs(m.FavourB) <= 6 && m.BurnoutA >= 0 && m.BurnoutA < 300;
        }
        Check(keys && bounded && LifeRules.Matters.Length >= 6, "life: every office matter is its own, with bounded effects");
        Matter credit = LifeRules.Matters[0];
        Check(LifeRules.FavourOf(credit, true, BossKind.Micromanager) < 0 && LifeRules.FavourOf(credit, true, BossKind.Mentor) > LifeRules.FavourOf(credit, true, BossKind.Driver),
            "life: speaking up suits a mentor, not a micromanager");
        Check(LifeRules.FavourOf(credit, false, BossKind.Mentor) == credit.FavourB, "life: the second answer is the same whoever the boss");
    }

    private static void LifeMovingOn()
    {
        Check(LifeRules.PoachChanceOf(1, 5, 10) == 0 && LifeRules.PoachChanceOf(2, 3, 10) == 0 && LifeRules.PoachChanceOf(2, 4, 0) == LifeRules.PoachChance
              && LifeRules.PoachChanceOf(3, 5, 10) > LifeRules.PoachChanceOf(3, 4, 0), "life: search firms come for good seniors, more for the best and the known");
        Check(LifeRules.RedundancyChance(0, EmployerKind.City, 3) == 0 && LifeRules.RedundancyChance(1, EmployerKind.Rival, 3) == 0 && LifeRules.RedundancyChance(3, EmployerKind.Public, 1) == 0,
            "life: no cuts in good times, and never in the public sector");
        Check(LifeRules.RedundancyChance(3, EmployerKind.City, 3) > LifeRules.RedundancyChance(2, EmployerKind.City, 3) && LifeRules.RedundancyChance(3, EmployerKind.City, 5) < LifeRules.RedundancyChance(3, EmployerKind.City, 3),
            "life: more cuts in a recession, fewer for the best");
        Job j = LifeJob(ProCatalogue.Accountant, 1);
        Check(LifeRules.Severance(j, 10, 60) == LifeRules.WeekPay(j) * LifeRules.SeveranceLeastWeeks && LifeRules.Severance(j, 1 + 60 * 5, 60) == LifeRules.WeekPay(j) * 5,
            "life: severance is a week a year of service, two at least");
        Check(LifeRules.BenefitWeek(100000) == 400 && LifeRules.BenefitWeek(100000000) == LifeRules.BenefitMost, "life: benefit is a share of the last week, to its most");
    }

    private static void LifeMoney()
    {
        Check(LifeRules.OptionsFor(EmployerKind.TechFirm, 0) == 0 && LifeRules.OptionsFor(EmployerKind.TechFirm, 3) == 3 * LifeRules.OptionsPerRank && LifeRules.OptionsFor(EmployerKind.City, 3) == 0,
            "life: options at a tech company, above the first rung");
        int vested;
        long boom = LifeRules.Vest(8000, 0, out vested);
        Check(vested == 2000 && boom == 2000L * LifeRules.OptionCents[0] && LifeRules.Vest(8000, 3, out vested) < boom, "life: a quarter vests, worth more in a boom");
        long own, employer;
        LifeRules.Pension(100000, EmployerKind.City, out own, out employer);
        Check(own == 5000 && employer == 3000, "life: the pension takes five per cent and the employer adds three");
        LifeRules.Pension(100000, EmployerKind.Public, out own, out employer);
        Check(employer == 8000, "life: the public sector adds eight");
        Check(LifeRules.PensionGrowth(3650000, 365) == 400 && LifeRules.PensionGrowth(0, 60) == 0, "life: the pot grows by its yearly rate a day");
        long pot = 60L * 20 * 1000;
        Check(LifeRules.PensionDay(pot, 60, false) == 1000 && LifeRules.PensionDay(pot, 60, true) == 750, "life: the pot pays over twenty years, less for an early start");
        Job chief = LifeJob(ProCatalogue.Manager, 6, EmployerKind.Rival), coo = LifeJob(ProCatalogue.Manager, 5, EmployerKind.Rival), city = LifeJob(ProCatalogue.Manager, 6, EmployerKind.City);
        Check(LifeRules.ProfitShareBp(chief) == LifeRules.ProfitShareChiefBp && LifeRules.ProfitShareBp(coo) == LifeRules.ProfitShareExecBp && LifeRules.ProfitShareBp(city) == 0
              && LifeRules.ProfitShareBp(LifeJob(ProCatalogue.Manager, 2, EmployerKind.Rival)) == 0, "life: a share of a rival's takings at its top seats only");
        Check(LifeRules.ConflictChanceOf(0) < LifeRules.ConflictChanceOf(5), "life: a conflict comes out more easily the higher the seat");
    }
}
