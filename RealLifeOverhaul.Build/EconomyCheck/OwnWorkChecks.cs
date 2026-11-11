using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;
using Contract = RealLifeOverhaul.Careers.Contract;

// Own work (Scripts/Careers: Work/OwnRules, CareersDirector.OwnStint, ContractStint). A player who took a management degree after a developer's training found that
// Own work kept up only Leadership and Delivery while Coding and System design showed Fading: the highest rank they could come in at was the engineering manager's,
// whose role trains the managing pair alone, and every path of own work (a desk of their firm, a freelance contract) read that pair and nothing else. Now the
// profession's own two skills are kept up beside it, by one rule for every path and every kind of firm. The same thread asked for the hours of a contract's stint to be
// the player's to pick (it was always three) and for the work to be started at a desk, as a job's is, and not from the Career app.
internal static partial class Program
{
    private static void OwnWork()
    {
        OwnWorkSkills();
        OwnWorkPlayer();
        OwnWorkHours();
        OwnWorkOrder();
        OwnWorkSources();
        OwnWorkOptions();
        OwnWorkTexts();
    }

    // The rule for every profession and every rank: the rank's two skills are taught, the profession's own two are kept up, and no managing skill comes with an engineer's.
    private static void OwnWorkSkills()
    {
        bool covers = true, apart = true, roles = true, quiet = true;
        string first = null;
        int ranks = 0;
        for (int i = 0; i < ProCatalogue.All.Length; i++)
        {
            Profession p = ProCatalogue.All[i];
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                OwnRules.WorkSkills(p, r, out int a, out int b, out int keepA, out int keepB);
                p.SkillsOf(r, out int roleA, out int roleB);
                bool hasA = a == p.SkillA || b == p.SkillA || keepA == p.SkillA, hasB = a == p.SkillB || b == p.SkillB || keepB == p.SkillB;
                covers &= hasA && hasB;
                apart &= keepA != a && keepA != b && keepB != a && keepB != b && (keepA < 0 || keepA == p.SkillA) && (keepB < 0 || keepB == p.SkillB);
                roles &= a == roleA && b == roleB;
                bool ownPair = (roleA == p.SkillA || roleA == p.SkillB) && (roleB == p.SkillA || roleB == p.SkillB);
                quiet &= !ownPair || (keepA < 0 && keepB < 0);
                if (first == null && !(hasA && hasB))
                {
                    first = p.Key + " rank " + r;
                }
                ranks++;
            }
        }
        Check(covers, "own work skills: at every rank of every profession the profession's own two skills are taught or kept up (" + (first ?? ranks + " ranks") + ")");
        Check(roles, "own work skills: the two taught are the rank's role's, as a job's day teaches them");
        Check(apart, "own work skills: a skill kept up is one of the profession's own that the rank does not teach, never one taught twice");
        Check(quiet, "own work skills: where the rank's role has the profession's own two skills nothing more is kept up, so no rank but a managing one changes");

        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        const int engineeringManager = 5;
        Check(dev.Ranks[engineeringManager].Role == TalentCatalogue.EngineeringManager && dev.Ranks[engineeringManager - 1].Role == TalentCatalogue.SoftwareEngineer,
            "own work skills: the developer's ladder has the engineering manager's rank straight after the engineers' five (the report's rank)");
        OwnRules.WorkSkills(dev, engineeringManager, out int ma, out int mb, out int mka, out int mkb);
        Check(ma == TalentCatalogue.Leadership && mb == TalentCatalogue.Delivery && mka == TalentCatalogue.Coding && mkb == TalentCatalogue.SystemDesign,
            "own work skills: an engineering manager's own work teaches Leadership and Delivery and keeps Coding and System design up (" + ma + ", " + mb + ", " + mka + ", " + mkb + ")");
        bool engineers = true;
        for (int r = 0; r < engineeringManager; r++)
        {
            OwnRules.WorkSkills(dev, r, out int a, out int b, out int keepA, out int keepB);
            engineers &= a == TalentCatalogue.Coding && b == TalentCatalogue.SystemDesign && keepA < 0 && keepB < 0;
        }
        Check(engineers, "own work skills: an engineer's own work, at any of the five ranks, teaches Coding and System design, keeps up nothing else and brings no management with it");

        // Every kind of firm: the sector's five, the web agency and the design studio, a law firm, a headquarters, a bank.
        var types = new List<string>(ProCatalogue.TechSector) { "ba:businesstype_webdevelopmentagency", "ba:businesstype_graphicdesigner", "ba:businesstype_lawfirm",
            "ba:businesstype_headquarters", "ba:businesstype_bank" };
        int stints = 0;
        bool used = true;
        foreach (string type in types)
        {
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                if (!ProCatalogue.EmploysKept(p, type))
                {
                    continue;
                }
                for (int r = 0; r < p.Ranks.Length; r++)
                {
                    var f = new StudyFile();
                    for (int s = 0; s < f.Skill.Length; s++)
                    {
                        f.Skill[s] = 50000;
                        f.Best[s] = 50000;
                    }
                    OwnRules.Practise(f, p, r, 2, 100, 1000);
                    used &= f.Used[p.SkillA] == 100 && f.Used[p.SkillB] == 100;
                    stints++;
                }
            }
        }
        Check(used && stints > 100, "own work skills: a stint at every kind of firm, as every profession it employs and at every rank, uses the profession's own two skills that day (" + stints + " stints)");
    }

    // The report's player: a developer's training and years, then a management degree. The numbers are the screenshot's.
    private static StudyFile OwnWorkDeveloper(int coding, int systemDesign, int leadership, int delivery, int usedDay)
    {
        var f = new StudyFile();
        f.Results.Add(new Enrolment { Course = StudyCatalogue.CodingBootcamp, Stage = (int)EnrolStage.Passed, Grade = 3 });
        f.Work.History.Add(new Spell { Prof = ProCatalogue.Developer, From = 1, To = 400 });
        int[] skills = { TalentCatalogue.Coding, TalentCatalogue.SystemDesign, TalentCatalogue.Leadership, TalentCatalogue.Delivery };
        int[] points = { coding, systemDesign, leadership, delivery };
        for (int i = 0; i < skills.Length; i++)
        {
            f.Skill[skills[i]] = points[i];
            f.Best[skills[i]] = points[i] + 5000;
            f.Used[skills[i]] = usedDay;
        }
        return f;
    }

    private static void OwnWorkPlayer()
    {
        const string studio = "reallife:businesstype_softwarestudio";
        const int today = 439;
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        int coding = TalentCatalogue.Coding, design = TalentCatalogue.SystemDesign, lead = TalentCatalogue.Leadership, delivery = TalentCatalogue.Delivery;

        // After the management degree: Leadership 67 and Delivery 97 make the engineering manager's rank the highest they could come in at.
        StudyFile manager = OwnWorkDeveloper(91000, 90000, 67000, 97000, 293);
        int prof = OwnRules.FirmProfession(manager, studio, today, 60, out int rank);
        Check(prof == ProCatalogue.Developer && rank == 5, "own work player: with the management degree a developer works at a software studio as its engineering manager (" + prof + "/" + rank + ")");

        // What happened: a stint taught the rank's two skills alone, so Coding and System design stayed on day 293 and faded.
        StudyFile old = OwnWorkDeveloper(91000, 90000, 67000, 97000, 293);
        dev.SkillsOf(rank, out int oldA, out int oldB);
        StudyRules.Learn(old, oldA, StudyRules.WorkGain(old, oldA, 3, 1000), today);
        StudyRules.Learn(old, oldB, StudyRules.WorkGain(old, oldB, 3, 1000), today);
        int taughtLead = old.Skill[lead], taughtDelivery = old.Skill[delivery];   // what the rank's two skills alone taught, before the days of fading below touch any skill
        int faded = StudyRules.Fade(old, today + 30);
        Check(old.Used[coding] == 293 && old.Used[design] == 293 && old.Used[lead] == today && old.Used[delivery] == today && faded > 0,
            "own work player: the report, as the rank's two skills alone gave it: Leadership and Delivery on day " + today + ", Coding and System design on day 293, fading (" + faded + " thousandths lost)");

        // Now: the same stint keeps all four up, teaches the managing pair exactly as before, and nothing fades.
        StudyFile now = OwnWorkDeveloper(91000, 90000, 67000, 97000, 293);
        OwnRules.Practise(now, dev, rank, 3, today, 1000);
        int lost = StudyRules.Fade(now, today + StudyCatalogue.FadeAfterDays);
        Check(now.Used[coding] == today && now.Used[design] == today && now.Used[lead] == today && now.Used[delivery] == today && lost == 0,
            "own work player: a stint of own work as the engineering manager uses Coding, System design, Leadership and Delivery that day, and none of them fades for " + StudyCatalogue.FadeAfterDays + " days");
        Check(now.Skill[lead] == taughtLead && now.Skill[delivery] == taughtDelivery && taughtLead > 67000 && taughtDelivery > 97000 && now.Skill[coding] == 91000 && now.Skill[design] == 90000,
            "own work player: the managing pair is taught as before and the engineering pair is kept up, not taught (a stint is the same learning, the trade kept from fading)");
        StudyFile later = OwnWorkDeveloper(91000, 90000, 67000, 97000, 293);
        for (int day = today; day < today + 60; day += 3)
        {
            OwnRules.Practise(later, dev, rank, 3, day, 1000);   // a stint every three days
            StudyRules.Fade(later, day);
        }
        Check(later.Skill[coding] >= 91000 && later.Skill[design] >= 90000, "own work player: two months of a stint every three days leave Coding and System design as they were");

        // Before the degree: the same stint at the engineers' rank is exactly what it always was.
        StudyFile junior = OwnWorkDeveloper(91000, 90000, 30000, 30000, 293);
        int engineerProf = OwnRules.FirmProfession(junior, studio, today, 60, out int engineerRank);
        dev.SkillsOf(engineerRank, out int engA, out int engB);
        StudyFile same = OwnWorkDeveloper(91000, 90000, 30000, 30000, 293);
        StudyRules.Learn(same, engA, StudyRules.WorkGain(same, engA, 3, 1000), today);
        StudyRules.Learn(same, engB, StudyRules.WorkGain(same, engB, 3, 1000), today);
        OwnRules.Practise(junior, dev, engineerRank, 3, today, 1000);
        bool equal = true;
        for (int s = 0; s < junior.Skill.Length; s++)
        {
            equal &= junior.Skill[s] == same.Skill[s] && junior.Best[s] == same.Best[s] && junior.Used[s] == same.Used[s];
        }
        Check(engineerProf == ProCatalogue.Developer && engineerRank == 4 && equal,
            "own work player: without the management skills the stint is the engineer's as it was, skill for skill and day for day (" + engineerProf + "/" + engineerRank + ")");

        // The gain is one sum for a job's day and a stint of own work, with the ladder's equipment factor.
        Check(StudyRules.WorkGain(now, coding, 4, 1000) == StudyCatalogue.WorkLearn * 4 * (100 - 91) / 100
              && StudyRules.WorkGain(now, coding, 4, CareerLadderRules.EquippedLearn) == StudyCatalogue.WorkLearn * 4 * (100 - 91) / 100 * CareerLadderRules.EquippedLearn / 1000
              && StudyRules.WorkGain(now, coding, 0, 1000) == 0,
            "own work player: an hour of work teaches what StudyCatalogue.WorkLearn says, less the more is held, by the ladder's factor");
    }

    // The slider: one hour to what the contract has left, never over twelve, and where it starts it is the stint there always was.
    private static void OwnWorkHours()
    {
        Check(OwnRules.SittingHoursDefault == 3 && OwnRules.SittingHoursDefault * 60 == OwnRules.SittingMinutes && OwnRules.SittingHoursLeast == 1 && OwnRules.SittingHoursMost == 12,
            "own work hours: the stint was three hours; the slider runs from 1 to 12 and starts at 3");
        bool most = true, clamps = true, minutes = true, same = true, grows = true;
        foreach (int hours in OwnRules.HoursOf)
        {
            int left = hours * 60;
            most &= OwnRules.StintMostHours(left) == Math.Min(OwnRules.SittingHoursMost, hours);
            clamps &= OwnRules.StintHours(0, left) == 1 && OwnRules.StintHours(-9, left) == 1 && OwnRules.StintHours(99, left) == OwnRules.StintMostHours(left) && OwnRules.StintHours(3, left) == 3;
        }
        for (int left = 0; left <= 2000; left++)
        {
            int before = 0;
            for (int h = -2; h <= 40; h++)
            {
                int run = OwnRules.StintMinutes(h, left);
                minutes &= run <= left && run >= 0 && (left == 0 || run >= Math.Min(60, left)) && (run == left || run % 60 == 0);
                grows &= run >= before;
                before = run;
            }
            same &= OwnRules.StintMinutes(OwnRules.SittingHoursDefault, left) == OwnRules.FixedMinutes(left);
        }
        Check(most, "own work hours: the slider's right end is the contract's hours, a contract of 8, 12, 16, 24 or 32 hours offering 8, 12, 12, 12 and 12");
        Check(clamps, "own work hours: a value off the slider is held to its ends, and 3 stays 3");
        Check(minutes && grows, "own work hours: a stint of some hours runs those hours, never more than the contract has left, and more hours never run less");
        Check(same, "own work hours: the slider at its start gives the stint there always was, three hours or what the contract has left (0 to 2000 minutes left)");
        Check(OwnRules.StintMostHours(30) == 1 && OwnRules.StintMostHours(90) == 2 && OwnRules.StintMostHours(0) == 1 && OwnRules.StintHours(3, 90) == 2 && OwnRules.StintMinutes(3, 90) == 90
              && OwnRules.StintMinutes(12, 1920) == 720 && OwnRules.StintMinutes(1, 1920) == 60 && OwnRules.FixedMinutes(1920) == 180 && OwnRules.FixedMinutes(40) == 40 && OwnRules.FixedMinutes(-5) == 0,
            "own work hours: a part hour counts at the slider's right end, the last of a contract is what is left, and with the slider off a stint is 180 minutes or less");
        Check(OwnRules.StintMostHours(60) == OwnRules.SittingHoursLeast && OwnRules.StintMostHours(61) > OwnRules.SittingHoursLeast,
            "own work hours: a contract with an hour or less left has no slider (one end only), with more it has one");
    }

    // The contract worked first at a desk, and which are trades.
    private static void OwnWorkOrder()
    {
        var a = new Contract { Id = 4, Due = 30, Stage = (int)ContractStep.Taken };
        var b = new Contract { Id = 7, Due = 20, Stage = (int)ContractStep.Taken };
        var c = new Contract { Id = 2, Due = 30, Stage = (int)ContractStep.Taken };
        Check(OwnRules.WorkedBefore(b, a) && !OwnRules.WorkedBefore(a, b) && OwnRules.WorkedBefore(c, a) && !OwnRules.WorkedBefore(a, c) && !OwnRules.WorkedBefore(a, a),
            "own work order: the contract due first is worked first, then the one taken on first, and none before itself");
        var all = new List<Contract> { a, b, c };
        bool alike = true;
        int[][] orders = { new[] { 0, 1, 2 }, new[] { 2, 1, 0 }, new[] { 1, 0, 2 }, new[] { 0, 2, 1 }, new[] { 2, 0, 1 }, new[] { 1, 2, 0 } };
        foreach (int[] order in orders)
        {
            Contract best = null;
            foreach (int i in order)
            {
                best = best == null || OwnRules.WorkedBefore(all[i], best) ? all[i] : best;
            }
            alike &= ReferenceEquals(best, b);
        }
        Check(alike, "own work order: whichever order the contracts are held in, the same one is worked first");
        bool trades = true;
        foreach (Profession p in ProCatalogue.All)
        {
            bool is3 = p.Id == ProCatalogue.Electrician || p.Id == ProCatalogue.Plumber || p.Id == ProCatalogue.Carpenter;
            trades &= OwnRules.IsTrade(p) == is3;
        }
        Check(trades && !OwnRules.IsTrade(ProCatalogue.Find(ProCatalogue.Developer)) && !OwnRules.IsTrade(ProCatalogue.Find(ProCatalogue.Lawyer)),
            "own work order: the electrician, the plumber and the carpenter work at the client's building, where no desk of the player's stands; every other profession is desk work");
    }

    // The sources: one rule for every path, the click on the second place, the tech firm's click first sharing the desk, the panel's slider, the app only as a fallback, the cost.
    private static void OwnWorkSources()
    {
        string own = ModSource(@"Careers\CareersDirector.Own.cs"), desk = ModSource(@"Careers\CareersDirector.OwnDesk.cs"), stint = ModSource(@"Careers\CareersDirector.OwnStint.cs");
        string contract = ModSource(@"Careers\ContractStint.cs"), rules = ModSource(@"Careers\Work\OwnRules.cs"), learning = ModSource(@"Careers\Study\StudyRules.Learning.cs");
        string work = ModSource(@"Careers\CareersDirector.Work.cs"), director = ModSource(@"Careers\CareersDirector.cs"), techDesk = ModSource(@"Tech\TechDirector.OwnDesk.cs");
        string bridges = ModSource(@"Tech\TechBridges.cs"), links = ModSource(@"Wiring\CareersLinks.cs"), app = ModSource(@"Careers\CareerApp.Own.cs");
        string kit = ModSource(@"Ui\Work\CareerLive.Manage.cs"), live = ModSource(@"Ui\Work\CareerLive.cs"), pages = ModSource(@"Ui\Work\CareerPages.cs");
        Check(own.Length > 0 && desk.Length > 0 && stint.Length > 0 && contract.Length > 0 && rules.Length > 0 && learning.Length > 0 && work.Length > 0 && director.Length > 0 && techDesk.Length > 0
              && bridges.Length > 0 && links.Length > 0 && app.Length > 0 && kit.Length > 0 && live.Length > 0 && pages.Length > 0, "own work sources: the sources are read");

        // One rule for every path of own work.
        string worked = MethodText(desk, "private void OwnWorked(Profession p, int rank, int hours, int day)");
        string firm = MethodText(desk, "internal void FirmWorked(int prof, int rank, int minutes)");
        string ended = MethodText(own, "private void ContractEnded(Sitting s, int minutes)");
        Check(worked.Contains("OwnRules.Practise(_study, p, rank, hours, day, LadderLearn());") && firm.Contains("OwnWorked(p, rank, Math.Max(1, minutes / 60), day);")
              && ended.Contains("OwnWorked(p, c.Rank, Math.Max(1, minutes / 60), day);") && !firm.Contains("SkillsOf(") && !ended.Contains("SkillsOf(")
              && !firm.Contains("LearnAtWork(") && !ended.Contains("LearnAtWork("),
            "own work sources: a stint at a desk of a firm and a stint on a contract both come to the one rule (OwnWorked), and neither reads the rank's two skills alone");
        Check(MethodText(rules, "public static void WorkSkills(Profession p, int rank, out int learnA, out int learnB, out int keepA, out int keepB)").Contains("p.SkillsOf(rank, out learnA, out learnB);")
              && MethodText(rules, "public static void Practise(StudyFile f, Profession p, int rank, int hours, int day, int ladder)").Contains("StudyRules.Use(f, keepA, day);")
              && MethodText(rules, "public static void Practise(StudyFile f, Profession p, int rank, int hours, int day, int ladder)").Contains("StudyRules.Use(f, keepB, day);"),
            "own work sources: the rule teaches the rank's role's two skills and uses the profession's own that the role leaves out");
        Check(learning.Contains("public static int WorkGain(StudyFile f, int skill, int hours, int ladder)")
              && learning.Contains("return StudyCatalogue.WorkLearn * hours * (100 - Points(f, skill)) / 100 * ladder / 1000;")
              && MethodText(work, "private void LearnAtWork(int skill, int hours, int day)").Contains("StudyRules.WorkGain(_study, skill, hours, LadderLearn())")
              && MethodText(rules, "public static void Practise(StudyFile f, Profession p, int rank, int hours, int day, int ladder)").Contains("StudyRules.WorkGain(f, a, hours, ladder)"),
            "own work sources: a job's day and a stint of own work teach by the one sum, StudyRules.WorkGain");

        // The click, on the second place, never taking another part's.
        string offer = MethodText(stint, "private void OfferOwnDesk(BuildingRegistration reg)");
        Check(offer.Contains("OwnDeskWay") && offer.Contains("NextContractHere(reg) != null") && offer.Contains("FirmsGame.KindOf(reg.businessTypeName) >= 0") && offer.Contains("DeskGame.OtherClicked = _onOwnDesk;")
              && ComesBefore(offer, "if (DeskGame.OtherClicked != null && !ReferenceEquals(DeskGame.OtherClicked, _onOwnDesk))", "DeskGame.OtherTarget = free;"),
            "own work sources: a free desk of an office business that is no tech firm is clicked as a job's desk is, on the second place, and a click another part holds there is left alone");
        Check(stint.Contains("internal bool OwnDeskWay => Settings.CareersOwnDesk && OwnOn && !_ownDeskOff && DeskGame.Clickable;")
              && MethodText(stint, "private void ReleaseOwnClick()").Contains("ReferenceEquals(DeskGame.OtherClicked, _onOwnDesk)"),
            "own work sources: the desk way needs its switch, working for oneself, no fault and the click in the game; the click is taken back only where it is this part's own");
        string at = MethodText(stint, "internal bool ContractAtDesk(EmployeeStationController desk)");
        Check(at.Contains("NextContractHere(reg)") && at.Contains("Notifications.ShowInsufficientEnergy();") && at.Contains("DeskGame.WalkTo(desk,") && at.Contains("OpenOwnStint(id, desk);") && at.Contains("FirmsGame.Free(desk)")
              && at.Contains("!OwnDeskWay"), "own work sources: a click walks the player to a free desk and opens the panel, or says it is tired, and takes none where no contract can be worked");
        string next = MethodText(stint, "private Contract NextContractHere(BuildingRegistration reg)");
        Check(next.Contains("c.Stage == (int)ContractStep.Taken") && next.Contains("OwnRules.Left(c) > 0") && next.Contains("!OwnRules.IsTrade(p)") && next.Contains("OfficeFits(p, reg)")
              && next.Contains("OwnRules.WorkedBefore(c, best)"), "own work sources: the contract offered at a desk is taken, has work left, is desk work, fits this office and is the one due first");
        Check(MethodText(own, "private BuildingRegistration OwnOffice(Profession p, bool here)").Contains("OfficeFits(p, reg)")
              && MethodText(stint, "private Contract NextContractHere(BuildingRegistration reg)").Contains("OfficeFits(p, reg)"),
            "own work sources: where a profession's desk work can be done is one test (OfficeFits) for the app's place check and for the desk");

        // The tech firm's click carries the contract first.
        string click = MethodText(techDesk, "private void DeskClicked(EntityController e)");
        Check(click.Contains("if (TechBridges.ContractAt(desk))") && ComesBefore(click, "TechBridges.ContractAt(desk)", "DeskRefusal(firm, out OwnerStanding standing, out string professions)")
              && bridges.Contains("public static Func<EmployeeStationController, bool> ContractDesk;") && bridges.Contains("ContractDesk = null;") && links.Contains("TechBridges.ContractDesk = ContractDesk;")
              && links.Contains("TechBridges.ContractDesk = null;") && links.Contains("d.ContractAtDesk(desk)"),
            "own work sources: in a tech firm of the player's the desk's click offers a contract first, through a bridge Careers fills, and the firm's own work when none can be worked");

        // The panel with its slider, and the start where the player stands as it always was.
        string offerStint = MethodText(stint, "private string OfferStint(Contract c, EmployeeStationController desk, string place)");
        Check(offerStint.Contains("ShiftSkip.Offer(this, stint)") && offerStint.Contains("Hold(sitting, skip);") && offerStint.Contains("_ownStint = stint;") && offerStint.Contains("Settings.CareersOwnHours")
              && offerStint.Contains("if (desk == null && !Settings.CareersOwnHours)") && offerStint.Contains("return Offer(ContractSitting(c, OwnRules.FixedMinutes(left)));") && offerStint.Contains("SeatBusy()"),
            "own work sources: the stint is the game's work panel (ShiftSkip), held in the book with its activity; with no desk and the hours switch off it is the three hour stint where the player stands, as before");
        Check(contract.Contains("internal sealed class ContractStint : ISlidingShift") && contract.Contains("public int SkipMinutes => _slides ? OwnRules.StintMinutes(_hours, _left) : OwnRules.FixedMinutes(_left);")
              && contract.Contains("public int SlideLeast => OwnRules.SittingHoursLeast;") && contract.Contains("public int SlideMost => _slides ? OwnRules.StintMostHours(_left) : OwnRules.SittingHoursLeast;")
              && contract.Contains("_director.OwnHoursChosen(_hours);") && contract.Contains("(\"common_hours\", new { hours = SlideNow })"),
            "own work sources: the stint's length is the slider's, one hour to what the contract has left, in the game's own words for hours, and the choice is kept for the next stint");
        Check(MethodText(stint, "internal void OwnHoursChosen(int hours)").Contains("_ownHours = hours;") && stint.Contains("private int _ownHours = OwnRules.SittingHoursDefault;")
              && offerStint.Contains("new ContractStint(this, sitting, left, desk, place, _ownHours, Settings.CareersOwnHours)"),
            "own work sources: the slider starts where the player left it, at three hours the first time");

        // Ends: the seat's (DeskSeatChecks) and the contract's minutes counted once, by the sitting's own record.
        string finish = MethodText(stint, "private void FinishOwnStint(ContractStint stint, int minutes, bool whole)");
        Check(finish.Contains("if (stint.Over)") && finish.Contains("stint.Over = true;") && finish.Contains("if (stint.Started)") && finish.Contains("stint.Sitting.SkipEnded(minutes, whole);")
              && finish.Contains("stint.Sitting.CalledOff();") && finish.Contains("StartCoroutine(Guard.Routine(OwnRestaffSoon(desk)))"),
            "own work sources: a stint ends once, whichever way; a stint that began is settled by its sitting's record and one that never did counts nothing; the desk's own employee comes back");
        Check(ModSource(@"Careers\Sitting.cs").Contains("if (!Record.Settle(SittingEnd.Ended))") && MethodText(director, "internal void Ended(Sitting sitting, int minutes, bool whole)").Contains("OtherEnded(sitting, minutes, whole);"),
            "own work sources: the minutes of a contract's stint are counted once, as the director settles any sitting");
        Check(MethodText(work, "private void StopWork(bool unloading)").Contains("OwnDeskDown(\"the working life stopped\");")
              && Regex.IsMatch(MethodText(director, "private void Slow()"), @"WorkDesks\(\);\s+OwnDeskSecond\(\);"),
            "own work sources: the timer looks once a second while the working life runs, and the stint and the click are let go when it stops");

        // The Career app: it names the place and shows it on the map; it starts the stint only where there is no desk to click.
        Check(app.Contains("bool fromApp = _director.ContractFromApp(c);") && app.Contains("if (fromApp)") && app.Contains("MapButton(buttons, () => _director.ShowContractPlace(id));")
              && app.Contains("\"reallife_own_where_desk\"") && ComesBefore(app, "if (fromApp)", "\"reallife_own_button_work\""),
            "own work sources: the Career app starts a contract itself only where no desk can be clicked, and says where to go and shows it on the map otherwise");
        Check(kit.Contains("if (d.ContractFromApp(c))") && kit.Contains("CareerAct.WorkContract") && kit.Contains("\"reallife_own_where_desk\"") && live.Contains("case CareerAct.WorkContract: return Said(d.WorkContract(arg), d);")
              && pages.Contains("WorkContract = 139"), "own work sources: the RLO interface says where a taken contract is worked, and its Work button is there only where the app starts it");
        string fromApp = MethodText(stint, "internal bool ContractFromApp(Contract c)");
        Check(fromApp.Contains("!OwnDeskWay") && fromApp.Contains("OwnRules.IsTrade(p)") && fromApp.Contains("CareersGame.AtHome()") && fromApp.Contains("_ownDeskNone"),
            "own work sources: the app starts a stint with the desk way off, for a trade at the client's, at home, and in an office of the player's with no desk free");

        // Cost: one look a second, nothing per frame, nothing searched.
        Check(!Regex.IsMatch(stint + contract, @"\b(Update|LateUpdate|FixedUpdate|OnGUI)\s*\(") && !stint.Contains("FindObject") && !stint.Contains("GetComponent") && !stint.Contains("Physics.")
              && stint.Contains("Time.realtimeSinceStartup >= _ownDeskLookAt"), "own work sources: nothing per frame and no search of the scene: a look once a second, the desks read once a visit and every ten seconds while none is free");
    }

    // The two switches: declared, on by default, read at load, on the options page, in the tree, owned by the feature, and worded within the page's limits.
    private static void OwnWorkOptions()
    {
        string settings = ModSource(@"Core\Settings.cs"), page = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"), list = ModSource(@"Meta\FeatureList.Life.cs");
        string[,] options =
        {
            { "CareersOwnDesk", "careers_own_desk", "reallife_own_opt_desk" }, { "CareersOwnHours", "careers_own_hours", "reallife_own_opt_hours" }
        };
        Dictionary<string, string> en = LocaleFile("en");
        for (int i = 0; i < options.GetLength(0); i++)
        {
            string field = options[i, 0], id = options[i, 1], label = options[i, 2], tip = "reallife_opt_tip_" + id;
            Check(settings.Contains("public const string " + field + "Id = \"" + id + "\";") && settings.Contains("public static bool " + field + " = true;")
                  && settings.Contains(field + " = Bool(modId, " + field + "Id, " + field + ");") && page.Contains(".AddToggle(Settings." + field + "Id, \"" + label + "\", Settings." + field + ",")
                  && tree.Contains(".Opt(\"" + id + "\", \"careers_own\")") && list.Contains("Settings." + field + "Id"),
                "own work options: " + id + " is declared, on by default, read at load, on the options page, in the Working for yourself section and owned by its feature");
            bool worded = en.TryGetValue(label, out string l) && en.TryGetValue(tip, out string t) && l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length <= 9 && l.Length <= 60 && !l.EndsWith(".")
                          && t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length <= 32 && t.Length <= 190 && t.EndsWith(".") && !t.Contains("!") && !t.Contains(" - ");
            Check(worded, "own work options: " + id + " has its label and its one sentence in en.json, within the page's limits (label 9 words and 60 characters, sentence 32 words and 190)");
        }
        Check(en.TryGetValue("reallife_opt_tip_careers_own_desk", out string deskTip) && deskTip.Contains("clicking a free desk") && deskTip.Contains("Off:")
              && en.TryGetValue("reallife_opt_tip_careers_own_hours", out string hoursTip) && hoursTip.Contains("slider") && hoursTip.Contains("three hours") && hoursTip.Contains("Off:"),
            "own work options: each sentence says what its switch does and what off gives back");
    }

    // The words: the keys the code reads, within a notice's length, and What's new (in any row, as the version step moves the next row).
    private static void OwnWorkTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys = { "reallife_own_opt_desk", "reallife_opt_tip_careers_own_desk", "reallife_own_opt_hours", "reallife_opt_tip_careers_own_hours", "reallife_own_where_desk", "reallife_own_why_no_office" };
        bool all = true;
        foreach (string key in keys)
        {
            all &= en.TryGetValue(key, out string text) && text.Length > 8 && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ");
        }
        Check(all, "own work texts: every new key is in en.json, with no dash and no exclamation mark");
        Check(Fits(en, "reallife_own_why_no_office") && Fits(en, "reallife_own_where_desk") && en["reallife_own_where_desk"].Contains("desk") && en["reallife_own_where_desk"].Contains("Start"),
            "own work texts: the reason for a contract with no office to work it in and the line that says where to work it are within 240 characters, and say desk and Start");
        string source = ModSource(@"Careers\CareersDirector.OwnStint.cs") + ModSource(@"Careers\CareersDirector.Own.cs") + ModSource(@"Careers\CareerApp.Own.cs") + ModSource(@"Ui\Work\CareerLive.Manage.cs");
        foreach (string key in new[] { "reallife_own_where_desk", "reallife_own_why_no_office" })
        {
            Check(source.Contains("\"" + key + "\""), "own work texts: " + key + " is read by the code");
        }
        Check(WhatsNewHas(en, "choose how long a stint of Own work on a freelance contract lasts") && WhatsNewHas(en, "Own work on a freelance contract can now be started at a desk")
              && WhatsNewHas(en, "Fixed: Own work kept up only Leadership and Delivery after a management degree"),
            "own work texts: What's new tells of the slider, the desk and the fix, the fix behind its fix mark, in some row (the next row, or the version row it is numbered into)");
    }
}
