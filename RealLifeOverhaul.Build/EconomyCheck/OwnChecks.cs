using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// Checks for working for oneself (Scripts/Careers/Work/OwnRules): who may found a company or open a practice, a
// founder's record, a former employer's claim, contracts' fees and lateness, a practice's name, and the save's line.
internal static partial class Program
{
    private static void Own()
    {
        var f = new StudyFile();
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer), law = ProCatalogue.Find(ProCatalogue.Lawyer), nurse = ProCatalogue.Find(ProCatalogue.Nurse);
        Check(OwnRules.CanFound(dev) && OwnRules.CanFound(ProCatalogue.Find(ProCatalogue.DataPro)) && !OwnRules.CanFound(law) && !OwnRules.CanFound(nurse),
            "own: tech people found companies; lawyers and nurses do not");
        Check(!OwnRules.CanPractise(law, f) && !OwnRules.CanPractise(nurse, f), "own: no practice of law without the bar, none of nursing at all");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.BarExam, Stage = (int)EnrolStage.Passed, Grade = 2 });
        Check(OwnRules.CanPractise(law, f), "own: the bar opens a practice of law");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.Electrician, Stage = (int)EnrolStage.Passed, Grade = 2 });
        Check(OwnRules.CanPractise(ProCatalogue.Find(ProCatalogue.Electrician), f), "own: a qualified electrician may work for themselves");
        Check(OwnRules.RegisterFee(law) > OwnRules.RegisterFee(ProCatalogue.Find(ProCatalogue.Designer)), "own: registering a practice of law costs most");
        Check(OwnRules.FounderScore(0, 0, 0) == 0 && OwnRules.FounderScore(3, 365 * 4, 5) == 3 * 20 + 40 + 15 && OwnRules.FounderScore(9, 365 * 30, 10) == OwnRules.FounderMost
              && OwnRules.FounderScore(0, 0, -10) == 0, "own: a founder's record counts rungs, years and name, bounded");
        Check(OwnRules.ClaimFor(200000, 0) == 2000L * OwnRules.ClaimWeeks && OwnRules.ClaimFor(200000, 2) == 2000L * (OwnRules.ClaimWeeks + 2 * OwnRules.ClaimWeeksPerColleague),
            "own: a claim is weeks of pay, more for each colleague taken");
        Check(OwnRules.ClaimChance(true) > OwnRules.ClaimChance(false), "own: taking colleagues is likelier to bring a claim than working in the field");
        long free = OwnRules.Fee(5000, 10, false, false), practice = OwnRules.Fee(5000, 10, true, false), office = OwnRules.Fee(5000, 10, true, true);
        Check(free == 650 && practice > free && office > practice, "own: a contract pays the rate with a premium, more at a practice and more from an office");
        var c = new Contract { Fee = 1000, Due = 10, Hours = 8, Minutes = 300 };
        Check(OwnRules.Paid(c, 10) == 1000 && OwnRules.Paid(c, 11) == 700, "own: late work is paid less");
        Check(OwnRules.Left(c) == 180, "own: what is left of a contract");
        var p = new Practice { Prof = ProCatalogue.Lawyer };
        OwnRules.Delivered(p, false);
        OwnRules.Delivered(p, true);
        Check(p.Reputation == OwnRules.PracticeStart + OwnRules.PracticeOnTime + OwnRules.PracticeLate && p.Served == 2 && p.Late == 1, "own: a practice's name follows its deliveries");
        Check(OwnRules.OfferChanceOf(p) > OwnRules.OfferChanceOf(new Practice()), "own: a practice is offered more work than a freelancer");
        var list = new List<Contract> { new Contract { Stage = (int)ContractStep.Offered }, new Contract { Stage = (int)ContractStep.Taken }, new Contract { Stage = (int)ContractStep.Done } };
        Check(OwnRules.Live(list) == 2, "own: offers and contracts under way are counted, finished ones not");

        var s = new StudyFile();
        WorkFile w = s.Work;
        w.FounderDay = 30;
        w.FounderProf = ProCatalogue.Developer;
        w.FounderTop = 3;
        w.FounderFrom = "Skyvault Systems";
        w.RestraintUntil = 70;
        w.RestraintProf = 0;
        w.RestraintEmployer = "Skyvault Systems";
        w.RestraintWeek = 320000;
        w.ColleaguesCount = 2;
        w.ColleaguesUntil = 60;
        w.ColleaguesProf = 0;
        w.ColleaguesLevel = 1;
        w.PracticeAfter = 5;
        w.Practice.Prof = ProCatalogue.Accountant;
        w.Practice.Opened = 12;
        w.Practice.Reputation = 44;
        w.Contracts.Add(new Contract { Id = 9, Prof = 4, Rank = 1, Client = "Fairway Markets", Street = "Main", Number = 2, Hours = 16, Minutes = 60, Fee = 1234, Due = 40, Lapse = 33, Stage = 1, Practice = true });
        int version;
        StudyFile back = CareersStore.Read(CareersStore.Write(s), out version);
        WorkFile b = back.Work;
        Check(b.FounderTop == 3 && b.FounderFrom == "Skyvault Systems" && b.RestraintWeek == 320000 && b.ColleaguesCount == 2 && b.PracticeAfter == 5, "own: the founder, the clause and the colleagues come back");
        Check(b.Practice.Prof == ProCatalogue.Accountant && b.Practice.Reputation == 44 && b.Contracts.Count == 1 && b.Contracts[0].Fee == 1234 && b.Contracts[0].Practice, "own: the practice and a contract come back");
        Check(CareersStore.Write(back) == CareersStore.Write(s), "own: the line reads back the same");
    }
}
