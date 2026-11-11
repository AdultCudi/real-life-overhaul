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
// the player's to pick (it was always three) and for the work to be started at a desk, as a job's is, and not from the Career app. Where it is started is checked on the
// rules (StartOf, ClickFor, ChoiceAt: every state of the place, so there is never a room in which a contract can be worked and nothing starts it, and at home it is the
// computer's panel), and the slider on the rules too (StintSlider: what the panel's own set-up does to the hours kept); the sources only pin that the game's side uses them.
internal static partial class Program
{
    private static void OwnWork()
    {
        OwnWorkSkills();
        OwnWorkPlayer();
        OwnWorkHours();
        OwnWorkSlider();
        OwnWorkOrder();
        OwnWorkPlaces();
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

    // The slider keeps what the player picks and nothing the panel does while it sets itself up. PlayerActivityUI.SetUpSlider holds the slider's old stand to the new ends, puts it
    // where the activity says it stands and tells the activity each value on the way, with a slider shown or not; the hours kept for the next stint were once overwritten by the
    // contract's own ends every time the panel opened. Run here as the game runs it: the set-up inside the offer, the offer done, then the player's hands.
    private static void OwnWorkSlider()
    {
        // The panel's set-up before the offer is done: the old stand held to the new ends, then where the activity says it stands, then the panel's own call after it.
        void SetUp(StintSlider s, int old)
        {
            s.Slide(Math.Max(s.Least, Math.Min(s.Most, old)));
            s.Slide(s.Now);
            s.Slide(s.Now);
        }

        // The case the first version got wrong: a pick of 6 hours on a 16 hour contract, two stints worked, the last with 4 hours left and then 2: the next contract starts at 6 and not at the 2.
        int kept = OwnRules.SittingHoursDefault, picks = 0;
        Action<int> keep = hours =>
        {
            kept = hours;
            picks++;
        };
        var first = new StintSlider(kept, 16 * 60, true, keep);
        SetUp(first, 12);
        first.Offered();
        first.Slide(6);
        Check(kept == 6 && picks == 1 && first.Now == 6 && first.Minutes == 360 && first.Most == 12 && first.Slides,
            "own work slider: a move of the player's to 6 hours is kept, once, and the stint runs the 6 hours of a 16 hour contract");
        var third = new StintSlider(kept, 4 * 60, true, keep);
        SetUp(third, 12);
        third.Offered();
        third.Slide(third.Now);
        Check(third.Now == 4 && third.Minutes == 240 && kept == 6 && picks == 1,
            "own work slider: with 4 hours left the slider stands at 4 and the 6 the player picked stay the hours kept (the panel's set-up call used to write the 4 over them)");
        var last = new StintSlider(kept, 2 * 60, true, keep);
        SetUp(last, 6);
        last.Offered();
        Check(last.Now == 2 && kept == 6 && picks == 1, "own work slider: the last 2 hours of a contract leave the hours kept at 6");
        var next = new StintSlider(kept, 24 * 60, true, keep);
        Check(next.Now == 6 && next.Minutes == 360, "own work slider: the next contract's slider starts where the player left it, at 6 and not at the last stint's 2");

        // A last stint with 150 minutes left (the slider runs 1 to 3) or an hour or less (one end only, no slider shown): the panel sets it up all the same.
        kept = 8;
        picks = 0;
        var short3 = new StintSlider(kept, 150, true, keep);
        SetUp(short3, 12);
        short3.Offered();
        var short1 = new StintSlider(kept, 60, true, keep);
        SetUp(short1, 12);
        short1.Offered();
        var under = new StintSlider(kept, 45, true, keep);
        SetUp(under, 1);
        under.Offered();
        Check(short3.Now == 3 && short3.Most == 3 && short3.Slides && short1.Now == 1 && short1.Most == 1 && !short1.Slides && under.Now == 1 && !under.Slides && kept == 8 && picks == 0,
            "own work slider: the last stint (150 minutes, an hour, 45 minutes) is held to its own ends and the 8 hours kept stay (the set-up used to make them 3 and 1)");

        // With the choice of hours switched off the slider is not there to move: three hours, nothing picked, and switching it on later starts where the player left it.
        kept = 6;
        picks = 0;
        var off = new StintSlider(kept, 16 * 60, false, keep);
        SetUp(off, 12);
        off.Offered();
        off.Slide(9);
        off.Slide(1);
        Check(off.Minutes == OwnRules.SittingMinutes && off.Most == 1 && off.Now == 1 && !off.Slides && kept == 6 && picks == 0 && new StintSlider(kept, 16 * 60, true, keep).Now == 6,
            "own work slider: with the choice of hours off a stint is three hours, nothing is picked, and the hours kept (6) are what the slider starts at when the choice is on again");
        var shortOff = new StintSlider(kept, 100, false, keep);
        Check(shortOff.Minutes == 100 && shortOff.Minutes == OwnRules.FixedMinutes(100), "own work slider: with the choice of hours off the last stint is what the contract has left, if under three hours");

        // The player's moves after the offer: kept, held to the ends, and a move to where the slider stands is no new pick.
        kept = 3;
        picks = 0;
        var moves = new StintSlider(kept, 32 * 60, true, keep);
        SetUp(moves, 20);
        moves.Offered();
        moves.Slide(moves.Now);
        moves.Slide(99);
        bool top = kept == OwnRules.SittingHoursMost && moves.Now == OwnRules.SittingHoursMost && picks == 1;
        moves.Slide(-4);
        bool bottom = kept == OwnRules.SittingHoursLeast && moves.Now == OwnRules.SittingHoursLeast && moves.Minutes == 60 && picks == 2;
        moves.Slide(5);
        Check(top && bottom && kept == 5 && moves.Minutes == 300 && picks == 3,
            "own work slider: the player's moves are kept, held to 1 and 12, and the panel telling the slider where it stands again picks nothing");

        // Every contract size and every hour left, every hours kept: whatever the panel's set-up tells the slider, the hours kept do not change and the slider stands inside its ends.
        bool unchanged = true, inside = true, matches = true;
        foreach (int left in new[] { 0, 1, 30, 59, 60, 61, 90, 150, 480, 720, 721, 960, 1440, 1920 })
        {
            for (int remembered = -2; remembered <= 40; remembered++)
            {
                for (int old = -1; old <= 14; old += 3)
                {
                    int now = remembered;
                    int calls = 0;
                    var s = new StintSlider(remembered, left, true, h =>
                    {
                        now = h;
                        calls++;
                    });
                    SetUp(s, old);
                    s.Offered();
                    s.Slide(s.Now);
                    unchanged &= now == remembered && calls == 0;
                    inside &= s.Now >= s.Least && s.Now <= s.Most && s.Minutes <= left && s.Minutes >= 0;
                    matches &= s.Now == OwnRules.StintHours(remembered, left);
                }
            }
        }
        Check(unchanged, "own work slider: for every contract size and hours kept, the panel setting the slider up never changes the hours kept");
        Check(inside && matches, "own work slider: the slider stands where the hours kept say, held to the ends of this contract, and never runs more than the contract has left");
    }

    // Where a contract is started from, for every place the player can stand in: never from the Career app while something at the place starts it, and never with nothing that does.
    private static void OwnWorkPlaces()
    {
        // The table, for all 64 states of the six facts: the way off or a trade's is the app; at home the computer's button or else the app; in an office the click or else the app; elsewhere the page says where.
        int states = 0, dead = 0, doubled = 0, wrong = 0;
        for (int bits = 0; bits < 64; bits++)
        {
            bool way = (bits & 1) != 0, trade = (bits & 2) != 0, atHome = (bits & 4) != 0, computer = (bits & 8) != 0, inOffice = (bits & 16) != 0, click = (bits & 32) != 0;
            ContractStart start = OwnPlaceRules.StartOf(way, trade, atHome, computer, inOffice, click);
            states++;
            bool placed = way && !trade && (atHome || inOffice);
            dead += placed && start == ContractStart.Elsewhere ? 1 : 0;
            doubled += start == ContractStart.App && way && !trade && ((atHome && computer) || (!atHome && inOffice && click)) ? 1 : 0;
            wrong += (start == ContractStart.Computer && !(way && !trade && atHome && computer)) || (start == ContractStart.Desk && !(way && !trade && !atHome && inOffice && click))
                     || (start == ContractStart.Elsewhere && (!way || trade || atHome || inOffice)) ? 1 : 0;
        }
        Check(states == 64 && dead == 0, "own work places: wherever a contract can be worked (at home, or inside an office business of the player's) something starts it, never nothing (" + dead + " of 64 states)");
        Check(doubled == 0 && wrong == 0, "own work places: the Career app starts a contract only where the computer's button and the desk's click are not there to do it, and each start is offered only where it stands");

        // The freelance developer with no software firm, at home: the computer's panel, not the app.
        Check(OwnPlaceRules.StartOf(true, false, true, true, false, false) == ContractStart.Computer && OwnPlaceRules.StartOf(true, false, true, false, false, false) == ContractStart.App,
            "own work places: a freelance developer with no office at home starts the contract at the computer's panel, and from the app only where the home has no computer with a chair");
        Check(OwnPlaceRules.StartOf(true, true, true, true, false, false) == ContractStart.App && OwnPlaceRules.StartOf(true, true, false, false, true, true) == ContractStart.App
              && OwnPlaceRules.StartOf(false, false, true, true, true, true) == ContractStart.App && OwnPlaceRules.StartOf(true, false, false, false, false, false) == ContractStart.Elsewhere,
            "own work places: a trade's work (at the client's) and the desk way switched off are the app's; away from home and from an office of theirs the page says where to go");

        // The computer at home is a way in only while the game opens its panel for it (CustomizableButtonsOverlay.ShouldShow: the home is the player's and nothing the computer needs is
        // missing), whatever chair stands by it. A home whose every computer is shut out of that has nothing to click for a contract, so the app starts it there: a computer with its chair
        // that the game opens no panel for used to count (DeskGame.HomeComputer asked for the chair alone), and the contract could then be started nowhere in that room.
        bool HomeHas(params bool[] pairs)   // each pair: a computer's chair by it, and whether the game opens its panel
        {
            for (int k = 0; k + 1 < pairs.Length; k += 2)
            {
                if (OwnPlaceRules.ComputerPanel(pairs[k], pairs[k + 1]))
                {
                    return true;
                }
            }
            return false;
        }
        Check(OwnPlaceRules.ComputerPanel(true, true) && !OwnPlaceRules.ComputerPanel(true, false) && !OwnPlaceRules.ComputerPanel(false, true) && !OwnPlaceRules.ComputerPanel(false, false),
            "own work places: a computer at home carries the contract's button only with its chair by it and its panel opened by the game");
        Check(!HomeHas() && !HomeHas(true, false) && !HomeHas(false, true) && !HomeHas(true, false, false, true) && HomeHas(true, false, true, true) && HomeHas(false, false, false, true, true, true),
            "own work places: a home is a way in when one of its computers has both its chair and an opened panel, and not when each lacks one");
        Check(OwnPlaceRules.StartOf(true, false, true, HomeHas(true, false), false, false) == ContractStart.App && OwnPlaceRules.StartOf(true, false, true, HomeHas(), false, false) == ContractStart.App
              && OwnPlaceRules.StartOf(true, false, true, HomeHas(true, false, true, true), false, false) == ContractStart.Computer,
            "own work places: at a home whose computers the game opens no panel for the app starts the contract, and the computer's panel does once one does");

        // Which click leads to the contract in an office business: all 32 states of the five facts.
        int clicks = 0, bad = 0, silent = 0;
        for (int bits = 0; bits < 32; bits++)
        {
            bool contract = (bits & 1) != 0, tech = (bits & 2) != 0, techLive = (bits & 4) != 0, free = (bits & 8) != 0, held = (bits & 16) != 0;
            DeskClick click = OwnPlaceRules.ClickFor(contract, tech, techLive, free, held);
            clicks++;
            bad += (click == DeskClick.Tech && !(contract && tech && techLive)) || (click == DeskClick.Own && !(contract && free && !held && !(tech && techLive))) || (click != DeskClick.None && !contract) ? 1 : 0;
            bool reachable = contract && ((tech && techLive) || (free && !held));
            silent += reachable != (click != DeskClick.None) ? 1 : 0;
        }
        Check(clicks == 32 && bad == 0 && silent == 0, "own work places: a click leads to a contract exactly when the tech firm's is up or a desk is free and no other part's click holds the place");

        // The two dead ends of the first version: with the tech sector off (or its firms stood down) the web agency's contract is clicked like any office's, and the bank branch's counter holds the place.
        bool webAgencyOff = OwnPlaceRules.ClickFor(true, true, false, true, false) == DeskClick.Own;
        bool techUp = OwnPlaceRules.ClickFor(true, true, true, true, false) == DeskClick.Tech && OwnPlaceRules.ClickFor(true, true, true, false, false) == DeskClick.Tech
                      && OwnPlaceRules.ClickFor(true, true, true, true, true) == DeskClick.Tech;
        bool bank = OwnPlaceRules.ClickFor(true, false, false, true, true) == DeskClick.None
                    && OwnPlaceRules.StartOf(true, false, false, false, true, OwnPlaceRules.ClickFor(true, false, false, true, true) != DeskClick.None) == ContractStart.App;
        bool noDesk = OwnPlaceRules.ClickFor(true, false, false, false, false) == DeskClick.None && OwnPlaceRules.ClickFor(true, true, false, false, false) == DeskClick.None;
        Check(webAgencyOff && techUp && bank && noDesk,
            "own work places: tech sector off, a web agency's free desk is clicked by Careers; the tech firm's click, while up, carries the contract; the bank branch's counter or no free desk leaves the app to start it");

        // A click on a free desk of the player's own tech firm.
        Check(OwnPlaceRules.ChoiceAt(false, true) == DeskChoice.Firm && OwnPlaceRules.ChoiceAt(false, false) == DeskChoice.Firm && OwnPlaceRules.ChoiceAt(true, false) == DeskChoice.Contract
              && OwnPlaceRules.ChoiceAt(true, true) == DeskChoice.Ask,
            "own work places: a tech firm's desk gives the firm's own work when no contract is taken, the contract when only it can be worked, and asks the player which when both can");

        // Which contract a desk or a computer takes.
        var taken = new Contract { Id = 1, Prof = ProCatalogue.Developer, Hours = 8, Minutes = 0, Stage = (int)ContractStep.Taken };
        var offered = new Contract { Id = 2, Prof = ProCatalogue.Developer, Hours = 8, Stage = (int)ContractStep.Offered };
        var done = new Contract { Id = 3, Prof = ProCatalogue.Developer, Hours = 8, Minutes = 8 * 60, Stage = (int)ContractStep.Taken };
        var late = new Contract { Id = 4, Prof = ProCatalogue.Developer, Hours = 8, Stage = (int)ContractStep.Late };
        var wire = new Contract { Id = 5, Prof = ProCatalogue.Electrician, Hours = 8, Stage = (int)ContractStep.Taken };
        var unknown = new Contract { Id = 6, Prof = 9999, Hours = 8, Stage = (int)ContractStep.Taken };
        Check(OwnPlaceRules.AtDesk(taken) && !OwnPlaceRules.AtDesk(offered) && !OwnPlaceRules.AtDesk(done) && !OwnPlaceRules.AtDesk(late) && !OwnPlaceRules.AtDesk(wire) && !OwnPlaceRules.AtDesk(unknown)
              && !OwnPlaceRules.AtDesk(null),
            "own work places: a desk or a computer takes a contract that is taken, has work left and is desk work, not an offer, a finished or late one, a trade's or a profession the game lost");

        // Show on map: a trade is led to the client, other desk work to the first office business of theirs it fits, and a freelancer with no office to their home, not to a refusal.
        Check(OwnPlaceRules.PlaceFor(true, false, false) == ContractPlace.Client && OwnPlaceRules.PlaceFor(true, true, false) == ContractPlace.Client
              && OwnPlaceRules.PlaceFor(true, false, true) == ContractPlace.Client && OwnPlaceRules.PlaceFor(true, true, true) == ContractPlace.Client
              && OwnPlaceRules.PlaceFor(false, true, false) == ContractPlace.Office && OwnPlaceRules.PlaceFor(false, true, true) == ContractPlace.Office
              && OwnPlaceRules.PlaceFor(false, false, true) == ContractPlace.Home && OwnPlaceRules.PlaceFor(false, false, false) == ContractPlace.None,
            "own work places: Show on map leads a trade's contract to the client, desk work to the first office business of the player's it fits, and a freelancer with no office to their home");
    }

    // The sources: one rule for every path, the click on the second place, the tech firm's click first sharing the desk, the home computer, the panel's slider, the app only as a fallback, the cost.
    private static void OwnWorkSources()
    {
        string own = ModSource(@"Careers\CareersDirector.Own.cs"), desk = ModSource(@"Careers\CareersDirector.OwnDesk.cs"), stint = ModSource(@"Careers\CareersDirector.OwnStint.cs");
        string contract = ModSource(@"Careers\ContractStint.cs"), rules = ModSource(@"Careers\Work\OwnRules.cs"), learning = ModSource(@"Careers\Study\StudyRules.Learning.cs");
        string work = ModSource(@"Careers\CareersDirector.Work.cs"), director = ModSource(@"Careers\CareersDirector.cs"), techDesk = ModSource(@"Tech\TechDirector.OwnDesk.cs");
        string bridges = ModSource(@"Tech\TechBridges.cs"), links = ModSource(@"Wiring\CareersLinks.cs"), app = ModSource(@"Careers\CareerApp.Own.cs");
        string kit = ModSource(@"Ui\Work\CareerLive.Manage.cs"), live = ModSource(@"Ui\Work\CareerLive.cs"), pages = ModSource(@"Ui\Work\CareerPages.cs");
        string home = ModSource(@"Careers\CareersDirector.OwnHome.cs"), buttons = ModSource(@"Careers\ComputerButtons.cs"), sitting = ModSource(@"Careers\ComputerSitting.cs");
        string play = ModSource(@"Careers\CareersDirector.ClassPlay.cs"), careersBridges = ModSource(@"Careers\CareersBridges.cs"), slider = ModSource(@"Careers\Work\StintSlider.cs");
        Check(own.Length > 0 && desk.Length > 0 && stint.Length > 0 && contract.Length > 0 && rules.Length > 0 && learning.Length > 0 && work.Length > 0 && director.Length > 0 && techDesk.Length > 0
              && bridges.Length > 0 && links.Length > 0 && app.Length > 0 && kit.Length > 0 && live.Length > 0 && pages.Length > 0 && home.Length > 0 && buttons.Length > 0 && sitting.Length > 0
              && play.Length > 0 && careersBridges.Length > 0 && slider.Length > 0, "own work sources: the sources are read");

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

        // The click, on the second place, by the one rule (OwnPlaceRules.ClickFor), never taking another part's and never leaving a room with a contract and nothing that starts it.
        string offer = MethodText(stint, "private void OfferOwnDesk(BuildingRegistration reg)");
        Check(offer.Contains("OwnDeskWay") && offer.Contains("NextContractHere(reg) != null") && offer.Contains("FirmsGame.KindOf(reg.businessTypeName) >= 0") && offer.Contains("CareersBridges.FirmDeskNow()")
              && offer.Contains("OwnPlaceRules.ClickFor(contract, tech, techLive, free != null, held)") && offer.Contains("_ownDeskNone = contract && click == DeskClick.None;")
              && offer.Contains("DeskGame.OtherClicked = _onOwnDesk;") && !offer.Contains("if (free == null || FirmsGame.KindOf(")
              && ComesBefore(offer, "bool held = DeskGame.OtherClicked != null && !ReferenceEquals(DeskGame.OtherClicked, _onOwnDesk);", "OwnPlaceRules.ClickFor(")
              && ComesBefore(offer, "OwnPlaceRules.ClickFor(", "DeskGame.OtherTarget = free;"),
            "own work sources: a free desk of an office business is clicked on the second place by the one rule, a click another part holds is left alone, and a tech firm is left to its own click only while that is up");
        string freeNow = MethodText(stint, "private EmployeeStationController FreeOwnDeskNow()");
        Check(freeNow.Contains("TechBridges.SessionDeskNow()") && freeNow.Contains("FreeOwnDesk(session)") && stint.Contains("!ReferenceEquals(_ownDesks[i], session)"),
            "own work sources: the desk sessions' own desk is never the one Careers clicks, their panel answers it");
        Check(stint.Contains("internal bool OwnWay => Settings.CareersOwnDesk && OwnOn && _workOn && !_ownDeskOff;") && stint.Contains("internal bool OwnDeskWay => OwnWay && DeskGame.Clickable;")
              && MethodText(stint, "private void ReleaseOwnClick()").Contains("ReferenceEquals(DeskGame.OtherClicked, _onOwnDesk)"),
            "own work sources: the way needs its switch, working for oneself, the working life and no fault; the click needs the game's list too, and is taken back only where it is this part's own");
        string at = MethodText(stint, "internal bool ContractAtDesk(EmployeeStationController desk)");
        string forDesk = MethodText(stint, "private Contract ContractFor(EmployeeStationController desk, out BuildingRegistration reg)");
        Check(forDesk.Contains("!OwnDeskWay") && forDesk.Contains("NextContractHere(reg)") && forDesk.Contains("!FirmsGame.Free(desk)") && at.Contains("ContractFor(desk, out BuildingRegistration reg)")
              && at.Contains("FirmsGame.Shut(reg)") && at.Contains("\"reallife_tech_own_why_shut\"") && at.Contains("Notifications.ShowInsufficientEnergy();") && at.Contains("DeskGame.WalkTo(desk,")
              && at.Contains("OpenOwnStint(id, desk);") && ComesBefore(at, "FirmsGame.Shut(reg)", "Notifications.ShowInsufficientEnergy();"),
            "own work sources: a click walks the player to a free desk and opens the panel, or says the business is shut or that it is tired, and takes none where no contract can be worked");
        string first = MethodText(stint, "private Contract FirstContract(BuildingRegistration office)");
        Check(first.Contains("OwnPlaceRules.AtDesk(c)") && first.Contains("OfficeFits(c.Def, office)") && first.Contains("OwnRules.WorkedBefore(c, best)") && home.Contains("FirstContract(null)"),
            "own work sources: the contract offered at a desk or a computer is taken, has work left, is desk work, fits this office (none at home) and is the one due first");
        Check(MethodText(own, "private BuildingRegistration OwnOffice(Profession p, bool here)").Contains("OfficeFits(p, reg)") && first.Contains("OfficeFits(c.Def, office)"),
            "own work sources: where a profession's desk work can be done is one test (OfficeFits) for the app's place check and for the desk");

        // The tech firm's click carries the contract, and asks which work is meant when the firm's own can be done too.
        string click = MethodText(techDesk, "private void DeskClicked(EntityController e)");
        string firmWork = MethodText(techDesk, "private void FirmDeskWork(EmployeeStationController desk)");
        string ask = MethodText(techDesk, "private void AskDeskWork(EmployeeStationController desk, string client, string firm)");
        Check(click.Contains("TechBridges.ContractClientAt(desk)") && click.Contains("OwnPlaceRules.ChoiceAt(client != null, DeskRefusal(firm, out _, out _) == null)") && click.Contains("case DeskChoice.Contract:")
              && click.Contains("case DeskChoice.Ask:") && click.Contains("FirmDeskWork(desk);") && !click.Contains("if (TechBridges.ContractAt(desk))")
              && ask.Contains("GameConfirm.TryShow(") && ask.Contains("\"reallife_own_pick_contract\", \"reallife_own_pick_firm\"") && ask.Contains("() => ContractDeskWork(desk), () => FirmDeskWork(desk)")
              && firmWork.Contains("DeskRefusal(firm, out OwnerStanding standing, out string professions)") && firmWork.Contains("DeskGame.WalkTo(desk,") && firmWork.Contains("Notifications.ShowInsufficientEnergy();"),
            "own work sources: in a tech firm of the player's a click works the contract or the firm's own work, whichever can be, and asks the player which when both can; the firm's own work is never hidden by a contract");
        Check(techDesk.Contains("public bool OwnDeskOffered => _firmsOn && _stint == null && (_sharedDesk != null || (_onDesk != null && ReferenceEquals(DeskGame.OtherClicked, _onDesk)));")
              && careersBridges.Contains("public static Func<bool> FirmDesk;") && careersBridges.Contains("FirmDesk = null;") && careersBridges.Contains("public static bool FirmDeskNow()")
              && links.Contains("CareersBridges.FirmDesk = FirmDesk;") && links.Contains("t.OwnDeskOffered"),
            "own work sources: Careers asks the tech firm whether its click is up, through a bridge, and clicks a desk of its own where it is not");
        Check(bridges.Contains("public static Func<EmployeeStationController, bool> ContractDesk;") && bridges.Contains("public static Func<EmployeeStationController, string> ContractClient;")
              && bridges.Contains("ContractDesk = null;") && bridges.Contains("ContractClient = null;") && links.Contains("TechBridges.ContractDesk = ContractDesk;") && links.Contains("TechBridges.ContractClient = ContractClient;")
              && links.Contains("TechBridges.ContractDesk = null;") && links.Contains("TechBridges.ContractClient = null;") && links.Contains("d.ContractAtDesk(desk)") && links.Contains("d.ContractClientAt(desk)"),
            "own work sources: the contract at a tech firm's desk is asked of Careers through bridges it fills, wired and taken down in Wiring");

        // The home computer: its own button, then the game's panel with the hours slider, the walk and the seat on Start.
        string atComputer = MethodText(home, "internal string ContractAtComputer(ComputerController computer)");
        Check(home.Contains("internal bool OwnComputerWay => OwnWay && ComputerButtons.Installed && !ComputerButtons.Failed;") && atComputer.Contains("SeatBusy()") && atComputer.Contains("computer.FindChair() == null")
              && atComputer.Contains("StartAtComputer(computer, ContractSitting(c, left), c.Client, slider)") && atComputer.Contains("new StintSlider(_ownHours, left, Settings.CareersOwnHours, OwnHoursChosen)")
              && buttons.Contains("director.ComputerContract()") && buttons.Contains("director.ContractAtComputer(computer)") && buttons.Contains("\"RealLifeOverhaul Own Contract\"") && buttons.Contains("Show(_contract, contract != null);"),
            "own work sources: at home a contract has its own button on the computer's panel, which offers the game's work panel with the hours slider, as a day from home is begun at the computer");
        string pressed = MethodText(sitting, "private void Pressed()");
        string startAt = MethodText(play, "private string StartAtComputer(ComputerController computer, Sitting sitting, string what, StintSlider slide = null)");
        Check(sitting.Contains("private bool Offered => _slide != null && _state == PlayerActivityState.NotStarted && !_pressed;") && ComesBefore(pressed, "_tally = new ClassTally(_slide.Minutes);", "Go();")
              && sitting.Contains("\"StartRealLifeContract\"") && sitting.Contains("\"CancelRealLifeContract\"") && MethodText(sitting, "public void OnSliderValueChanged(int value)").Contains("_slide.Slide(value);")
              && startAt.Contains("slide.Offered();") && startAt.Contains("activity.Go();") && ComesBefore(startAt, "slide.Offered();", "activity.Go();")
              && sitting.Contains("|| sitting.Kind == SittingKind.Contract;") && sitting.Contains("public bool RequiresEnergy() => !_work;"),
            "own work sources: a contract at the computer waits on the panel for Start with its slider, walks and sits on Start, and is not ended by the player's energy running out, as at the desk");

        // The panel with its slider, and the start where the player stands as it always was.
        string offerStint = MethodText(stint, "private string OfferStint(Contract c, EmployeeStationController desk, string place)");
        Check(offerStint.Contains("ShiftSkip.Offer(this, stint)") && offerStint.Contains("Hold(sitting, skip);") && offerStint.Contains("_ownStint = stint;") && offerStint.Contains("Settings.CareersOwnHours")
              && offerStint.Contains("if (desk == null && !Settings.CareersOwnHours)") && offerStint.Contains("return Offer(ContractSitting(c, OwnRules.FixedMinutes(left)));") && offerStint.Contains("SeatBusy()")
              && ComesBefore(offerStint, "ShiftSkip.Offer(this, stint)", "stint.Offered();"),
            "own work sources: the stint is the game's work panel (ShiftSkip), held in the book with its activity, its slider told when the offer is done; with no desk and the hours switch off it is the three hour stint where the player stands, as before");
        Check(contract.Contains("internal sealed class ContractStint : ISlidingShift") && contract.Contains("public int SkipMinutes => _slider.Minutes;") && contract.Contains("public int SlideLeast => _slider.Least;")
              && contract.Contains("public int SlideMost => _slider.Most;") && contract.Contains("public int SlideNow => _slider.Now;") && contract.Contains("public void Slide(int value) => _slider.Slide(value);")
              && contract.Contains("(\"common_hours\", new { hours = SlideNow })") && !contract.Contains("OwnHoursChosen"),
            "own work sources: the stint's length is the slider's, one hour to twelve or what the contract has left, in the game's own words for hours, and the slider alone decides which moves are kept");
        Check(MethodText(stint, "internal void OwnHoursChosen(int hours)").Contains("_ownHours = hours;") && stint.Contains("private int _ownHours = OwnRules.SittingHoursDefault;")
              && offerStint.Contains("new StintSlider(_ownHours, left, Settings.CareersOwnHours, OwnHoursChosen)") && slider.Contains("_picked?.Invoke(hours);")
              && MethodText(slider, "public void Slide(int value)").Contains("if (!_slides || !_offered)"),
            "own work sources: the slider starts where the player left it, at three hours the first time, and the hours are kept only by the player's moves once the panel's offer is done");

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

        // The Career app: it names the place and shows it on the map; it starts the stint only where nothing at the place does (OwnPlaceRules.StartOf).
        Check(app.Contains("bool fromApp = _director.ContractFromApp(c);") && app.Contains("if (fromApp)") && app.Contains("MapButton(buttons, () => _director.ShowContractPlace(id));")
              && app.Contains("\"reallife_own_where_desk\"") && ComesBefore(app, "if (fromApp)", "\"reallife_own_button_work\""),
            "own work sources: the Career app starts a contract itself only where nothing at the place does, and says where to go and shows it on the map otherwise");
        Check(kit.Contains("if (d.ContractFromApp(c))") && kit.Contains("CareerAct.WorkContract") && kit.Contains("\"reallife_own_where_desk\"") && live.Contains("case CareerAct.WorkContract: return Said(d.WorkContract(arg), d);")
              && pages.Contains("WorkContract = 139"), "own work sources: the RLO interface says where a taken contract is worked, and its Work button is there only where the app starts it");
        string fromApp = MethodText(stint, "internal bool ContractFromApp(Contract c)");
        Check(fromApp.Contains("OwnPlaceRules.StartOf(OwnWay, !deskWork, home, computer, office, click) == ContractStart.App") && fromApp.Contains("OwnPlaceRules.AtDesk(c)") && fromApp.Contains("CareersGame.AtHome()")
              && fromApp.Contains("OwnComputerWay && HomeComputerHere()") && fromApp.Contains("OwnOffice(c.Def, true)") && fromApp.Contains("OwnDeskWay && !_ownDeskNone") && !fromApp.Contains("|| CareersGame.AtHome()"),
            "own work sources: the app starts a stint by the one rule: the way off, a trade at the client's, a home with no computer button, an office no click leads to; never at home with the computer to click");
        Check(home.Contains("_homeComputer = CareersGame.AtHome() ? DeskGame.HomeComputer() : null;") && home.Contains("Time.realtimeSinceStartup - _homeComputerAt >= 1f"),
            "own work sources: the home's items are looked through at most once a second for a page of the Career app");
        string gameDesk = ModSource(@"Careers\DeskGame.cs");
        Check(MethodText(gameDesk, "public static ComputerController HomeComputer()").Contains("OwnPlaceRules.ComputerPanel(computer.FindChair() != null, Usable(computer))")
              && gameDesk.Contains("public static bool Usable(ComputerController computer)"),
            "own work sources: the home's computer that counts is one with its chair that the game opens a panel for (DeskGame.HomeComputer), for the working day's fallback and the contract's alike");
        string mapPlace = MethodText(stint, "internal string ShowContractPlace(int id)");
        Check(mapPlace.Contains("OwnPlaceRules.PlaceFor(trade, office != null, home != null)") && mapPlace.Contains("CareersGame.Home()") && mapPlace.Contains("case ContractPlace.Client:")
              && mapPlace.Contains("case ContractPlace.Office:") && mapPlace.Contains("case ContractPlace.Home:") && mapPlace.Contains("CareersText.KeyName(\"reallife_own_place_home\")")
              && mapPlace.Contains("return \"reallife_own_why_no_office\";"),
            "own work sources: Show on map leads to the client, an office of the player's it fits, else their home, and says there is no office only when they have none of the three");

        // Cost: one look a second, nothing per frame, nothing searched.
        Check(!Regex.IsMatch(stint + contract + home + slider, @"\b(Update|LateUpdate|FixedUpdate|OnGUI)\s*\(") && !stint.Contains("FindObject") && !stint.Contains("GetComponent") && !stint.Contains("Physics.")
              && !home.Contains("FindObject") && stint.Contains("Time.realtimeSinceStartup >= _ownDeskLookAt"),
            "own work sources: nothing per frame and no search of the scene: a look once a second, the desks read once a visit and every ten seconds while none is free");
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
        string[] keys = { "reallife_own_opt_desk", "reallife_opt_tip_careers_own_desk", "reallife_own_opt_hours", "reallife_opt_tip_careers_own_hours", "reallife_own_where_desk", "reallife_own_why_no_office", "reallife_own_place_home" };
        bool all = true;
        foreach (string key in keys)
        {
            all &= en.TryGetValue(key, out string text) && text.Length > 8 && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ");
        }
        Check(all, "own work texts: every new key is in en.json, with no dash and no exclamation mark");
        Check(Fits(en, "reallife_own_why_no_office") && Fits(en, "reallife_own_where_desk") && en["reallife_own_where_desk"].Contains("desk") && en["reallife_own_where_desk"].Contains("home")
              && en["reallife_own_where_desk"].Contains("Start"),
            "own work texts: the reason for a contract with no office to work it in and the line that says where to work it are within 240 characters, and say desk, home and Start");
        string source = ModSource(@"Careers\CareersDirector.OwnStint.cs") + ModSource(@"Careers\CareersDirector.Own.cs") + ModSource(@"Careers\CareerApp.Own.cs") + ModSource(@"Ui\Work\CareerLive.Manage.cs");
        foreach (string key in new[] { "reallife_own_where_desk", "reallife_own_why_no_office", "reallife_own_place_home" })
        {
            Check(source.Contains("\"" + key + "\""), "own work texts: " + key + " is read by the code");
        }
        Check(WhatsNewHas(en, "choose how long a stint of Own work on a freelance contract lasts") && WhatsNewHas(en, "Own work on a freelance contract can now be started at a desk")
              && WhatsNewHas(en, "Fixed: Own work kept up only Leadership and Delivery after a management degree"),
            "own work texts: What's new tells of the slider, the desk and the fix, the fix behind its fix mark, in some row (the next row, or the version row it is numbered into)");

        // What's new is true of every contract: its slider runs from one hour to twelve hours or what the contract has left, if that is less, whatever its size (8, 12, 16, 24 or 32 hours)
        // and however much of it is left (a part hour counted), which is the rule's range and not what the contract has left alone.
        bool claimed = WhatsNewHas(en, "from one hour up to twelve hours, or what is left of the contract if that is less") && OwnRules.SittingHoursLeast == 1 && OwnRules.SittingHoursMost == 12;
        foreach (int size in OwnRules.HoursOf)
        {
            claimed &= OwnRules.StintMostHours(size * 60) == Math.Min(12, size) && OwnRules.StintHours(1, size * 60) == 1;
        }
        for (int left = 1; left <= 32 * 60; left++)
        {
            claimed &= OwnRules.StintMostHours(left) == Math.Min(12, (left + 59) / 60);
        }
        Check(claimed, "own work texts: What's new says a stint runs from one hour up to twelve hours or what is left of the contract if that is less, which is the slider's range for every size and every amount left");

        // ...and of where it is started: a desk, the computer at home, and the app only at a client's building and where nothing can be clicked.
        bool placed = WhatsNewHas(en, "click a free desk inside an office business of yours, a software firm or a law firm included, or your computer at home, and press Start in the game's work panel")
                      && WhatsNewHas(en, "the Career app still starts it at a client's building and where nothing can be clicked")
                      && OwnPlaceRules.StartOf(true, false, false, false, true, true) == ContractStart.Desk && OwnPlaceRules.StartOf(true, false, true, true, false, false) == ContractStart.Computer
                      && OwnPlaceRules.StartOf(true, true, false, false, false, false) == ContractStart.App && OwnPlaceRules.StartOf(true, false, false, false, true, false) == ContractStart.App;
        Check(placed, "own work texts: What's new says a contract is started at a free desk of an office business or the computer at home, and by the app at a client's building and where nothing can be clicked, as the rule has it");
    }
}
