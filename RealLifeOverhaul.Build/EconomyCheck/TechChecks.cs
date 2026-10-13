using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Checks for the professions and the tech sector (Scripts/Tech): the roles and levels, a newcomer's file, output,
// learning, morale, reviews and promotion, notice and severance, courses and exams, the going rate, rivals' approaches,
// the save line and the paper's stories.
internal static partial class Program
{
    private static void Tech()
    {
        TechRoles();
        TechNewcomers();
        TechOutput();
        TechLearning();
        TechMood();
        TechReviews();
        TechNotice();
        TechCourses();
        TechGoing();
        TechRivals();
        TechPeopleSave();
        TechStories();
    }

    private static void TechRoles()
    {
        Check(TalentCatalogue.Roles.Length == TalentCatalogue.RoleCount, "tech: the roles' count is the table's");
        bool ids = true, levels = true, skills = true, bands = true;
        for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
        {
            RoleDef r = TalentCatalogue.Roles[i];
            ids &= r.Id == i;
            levels &= r.LeastLevel <= r.MostLevel && r.MostLevel < TalentCatalogue.Levels;
            skills &= r.SkillA >= 0 && r.SkillA < TalentCatalogue.SkillCount && r.SkillB >= 0 && r.SkillB < TalentCatalogue.SkillCount && r.SkillA != r.SkillB;
        }
        for (int l = 0; l < TalentCatalogue.Levels; l++)
        {
            bands &= TalentCatalogue.LevelSkillLeast[l] < TalentCatalogue.LevelSkillMost[l] && (l == 0 || TalentCatalogue.LevelSkillLeast[l] > TalentCatalogue.LevelSkillLeast[l - 1]);
        }
        Check(ids, "tech: every role's id is its place in the table");
        Check(levels, "tech: every role's levels are in order and inside the five");
        Check(skills, "tech: every role has two different specialisms of the catalogue");
        Check(bands, "tech: the levels' skill bands rise from junior to principal");
        Check(TalentCatalogue.Skills.Length == TalentCatalogue.SkillCount && TalentCatalogue.Certs.Length == TalentCatalogue.CertCount, "tech: the skills and certificates match their counts");
        Check(TalentCatalogue.Professional(TalentCatalogue.Programmer) && TalentCatalogue.Professional(TalentCatalogue.Lawyer) && !TalentCatalogue.Professional("ba:skill_cleaning")
              && !TalentCatalogue.Professional("ba:skill_deliverydriver"), "tech: programmers and lawyers are professionals; cleaners and drivers keep the game's hiring");
        var into = new int[TalentCatalogue.RoleCount];
        int count = TalentCatalogue.RolesOf(TalentCatalogue.GraphicDesigner, into);
        Check(count == 2, "tech: a graphic designer can be a UX and UI designer or a graphic designer (" + count + ")");
        int[] agency = { TalentCatalogue.WebDesigner };
        Check(TalentRules.Pick(TalentCatalogue.Programmer, agency, 1, 7, 1) == TalentCatalogue.WebDesigner, "tech: a programmer at a web agency is first of all its web designer");
        Check(TalentRules.Pick("ba:skill_cleaning", null, 0, 7, 1) == -1, "tech: a skill with no professional role gets none");
        RoleDef cto = TalentCatalogue.Role(TalentCatalogue.Cto);
        Check(TalentRules.LevelFor(20, cto) == TalentCatalogue.Staff && TalentRules.LevelFor(95, cto) == TalentCatalogue.Principal, "tech: a CTO is staff level at the least");
        RoleDef engineer = TalentCatalogue.Role(TalentCatalogue.SoftwareEngineer);
        Check(TalentRules.LevelFor(20, engineer) == TalentCatalogue.Junior && TalentRules.LevelFor(60, engineer) == TalentCatalogue.Senior && TalentRules.LevelFor(85, engineer) == TalentCatalogue.Principal,
            "tech: an engineer's level follows the main skill");
    }

    private static void TechNewcomers()
    {
        bool inBand = true, sameAgain = true, ages = true;
        int records = 0, made = 0;
        for (int n = 0; n < 400; n++)
        {
            int role = n % TalentCatalogue.RoleCount;
            RoleDef r = TalentCatalogue.Role(role);
            int level = r.LeastLevel + n % (r.MostLevel - r.LeastLevel + 1);
            Profile p = TalentRules.Make(11, n, role, level, 30);
            Profile q = TalentRules.Make(11, n, role, level, 30);
            made++;
            inBand &= p.Main / 1000 >= TalentCatalogue.LevelSkillLeast[p.Level] && p.Main / 1000 <= TalentCatalogue.LevelSkillMost[p.Level] && p.A <= 100000 && p.B >= 5000;
            sameAgain &= p.Main == q.Main && p.A == q.A && p.B == q.B && p.School == q.School && p.Certs == q.Certs && p.Record == q.Record;
            ages &= p.Age >= 21 && p.Age <= 60 && p.YearsDays >= TalentCatalogue.LevelYearsLeast[p.Level] * 365;
            records += p.Record ? 1 : 0;
        }
        Check(inBand, "tech: a newcomer's main skill is within their level's band");
        Check(sameAgain, "tech: the same seed and number give the same person");
        Check(ages, "tech: a newcomer's age and experience fit their level");
        Check(records > 0 && records < made / 10, "tech: a few newcomers have a past conviction (" + records + " of " + made + ")");
        Profile doctor = TalentRules.Make(3, 5, TalentCatalogue.ResearchScientist, TalentCatalogue.Senior, 1);
        Check(doctor.School >= TalentCatalogue.Master, "tech: a research scientist has a master's degree or a doctorate");
    }

    private static Profile Person(int role, int level, int main, int a, int b)
    {
        Profile p = TalentRules.Make(1, 1, role, level, 0);
        p.Id = "e" + role + "_" + level;
        p.Level = level;
        p.Main = main * 1000;
        p.A = a * 1000;
        p.B = b * 1000;
        p.Morale = 650;
        p.Burnout = 0;
        return p;
    }

    private static void TechOutput()
    {
        Profile low = Person(0, TalentCatalogue.Mid, 40, 40, 40), high = Person(0, TalentCatalogue.Mid, 80, 80, 80);
        Check(TalentRules.Output(high, 80, 1) > TalentRules.Output(low, 40, 1), "tech: more skill gets more done");
        Profile senior = Person(0, TalentCatalogue.Senior, 60, 60, 60), mid = Person(0, TalentCatalogue.Mid, 60, 60, 60);
        Check(TalentRules.Output(senior, 60, 1) > TalentRules.Output(mid, 60, 1), "tech: a senior gets more done than a mid at the same skill");
        Profile tired = Person(0, TalentCatalogue.Mid, 60, 60, 60);
        tired.Burnout = 800;
        Check(TalentRules.Output(tired, 60, 1) < TalentRules.Output(mid, 60, 1), "tech: burnout costs output");
        Profile sick = Person(0, TalentCatalogue.Mid, 60, 60, 60);
        sick.Sick = true;
        Check(TalentRules.Output(sick, 60, 1) == 0, "tech: nobody works off sick");
        Check(TalentRules.Faults(high, 80) < TalentRules.Faults(low, 40), "tech: more skill, fewer faults");
        Profile mid50 = Person(0, TalentCatalogue.Mid, 50, 50, 50);
        mid50.Morale = 750;
        int o = TalentRules.Output(mid50, 50, 1);
        Check(o > 900 && o < 1100, "tech: a mid-level at 50 in good heart does about a mid-level's work (" + o + ")");
    }

    private static void TechLearning()
    {
        Profile fresh = Person(0, TalentCatalogue.Junior, 20, 20, 20), expert = Person(0, TalentCatalogue.Senior, 90, 90, 90);
        int f0 = fresh.A, e0 = expert.A;
        TalentRules.Learn(fresh, 8, false);
        TalentRules.Learn(expert, 8, false);
        Check(fresh.A - f0 > expert.A - e0 && expert.A > e0, "tech: learning slows as a skill nears 100");
        Profile alone = Person(0, TalentCatalogue.Junior, 20, 20, 20), taught = Person(0, TalentCatalogue.Junior, 20, 20, 20);
        int main = TalentRules.Learn(taught, 8, true);
        TalentRules.Learn(alone, 8, false);
        Check(taught.A > alone.A && main > 0, "tech: a mentor speeds learning and adds to the game's skill");
        Check(TalentRules.Learn(Person(0, 0, 20, 20, 20), 0, true) == 0, "tech: nothing is learnt on a day off");
        Profile year = Person(0, TalentCatalogue.Junior, 30, 30, 30);
        for (int d = 0; d < 250; d++)
        {
            TalentRules.Learn(year, 8, false);
        }
        int gain = year.A / 1000 - 30;
        Check(gain >= 15 && gain <= 30, "tech: a year of working days takes a skill of 30 up by 15 to 30 points (" + gain + ")");
        Profile full = Person(0, TalentCatalogue.Principal, 100, 100, 100);
        TalentRules.Learn(full, 12, true);
        Check(full.A <= 100000 && full.B <= 100000, "tech: a skill never passes 100");
    }

    private static WorkDay Day(int hours, int paid, int going, int esteem = 0)
    {
        return new WorkDay { Day = 100, Year = 120, Hours = hours, WeekHours = hours * 5, MainPoints = 50, Paid = paid, Going = going, Esteem = esteem, Wanted = true };
    }

    private static void TechMood()
    {
        Profile fair = Person(0, 1, 50, 50, 50), under = Person(0, 1, 50, 50, 50), over = Person(0, 1, 50, 50, 50);
        Check(TalentRules.MoraleTarget(under, Day(8, 2500, 3000)) < TalentRules.MoraleTarget(fair, Day(8, 3000, 3000)), "tech: pay under the going rate lowers morale");
        Check(TalentRules.MoraleTarget(over, Day(8, 3600, 3000)) > TalentRules.MoraleTarget(fair, Day(8, 3000, 3000)), "tech: pay over the going rate raises it");
        Check(TalentRules.MoraleTarget(fair, Day(8, 3000, 3000, 10)) > TalentRules.MoraleTarget(fair, Day(8, 3000, 3000, -10)), "tech: a good employer is better to work for");
        Profile hard = Person(0, 1, 50, 50, 50);
        for (int d = 0; d < 20; d++)
        {
            TalentRules.Mood(hard, Day(12, 3000, 3000));
        }
        Check(hard.Burnout > 500, "tech: twelve-hour days burn people out (" + hard.Burnout + ")");
        int was = hard.Burnout;
        for (int d = 0; d < 10; d++)
        {
            TalentRules.Mood(hard, Day(0, 3000, 3000));
        }
        Check(hard.Burnout < was, "tech: days off ease burnout");
        Profile steady = Person(0, 1, 50, 50, 50);
        for (int d = 0; d < 60; d++)
        {
            TalentRules.Mood(steady, Day(8, 3000, 3000));
        }
        Check(steady.Burnout == 0 && steady.Morale > 500, "tech: an ordinary week burns nobody out (" + steady.Burnout + ", morale " + steady.Morale + ")");
        Profile worn = Person(0, 1, 50, 50, 50);
        worn.Burnout = 950;
        Check(TalentRules.SickChance(worn) > 0 && TalentRules.SickChance(steady) == 0, "tech: only the burnt out go off sick for it");
        Check(Math.Abs(TalentRules.GameMood(steady, 20f)) <= TalentCatalogue.MoodToGame, "tech: morale moves the game's satisfaction a few points at most");
    }

    private static void TechReviews()
    {
        int[] seen = new int[6];
        for (int n = 0; n < 300; n++)
        {
            Profile p = Person(0, TalentCatalogue.Mid, 35 + n % 30, 35 + n % 30, 30 + n % 30);
            p.Id = "r" + n;
            p.Morale = 300 + n % 7 * 100;
            int rating = TalentRules.Review(p, 9, 100, 120, 35 + n % 30);
            seen[rating]++;
            if (rating == 4 && p.RaiseAsk != TalentCatalogue.ReviewRiseBp || rating == 5 && p.RaiseAsk != 2 * TalentCatalogue.ReviewRiseBp || rating < 4 && p.RaiseAsk != 0)
            {
                Check(false, "tech: a review's request for a rise follows its rating");
            }
        }
        Check(seen[1] > 0 && seen[3] > 0 && seen[5] > 0, "tech: reviews give the whole range of ratings (" + seen[1] + "/" + seen[2] + "/" + seen[3] + "/" + seen[4] + "/" + seen[5] + ")");
        Profile star = Person(0, TalentCatalogue.Mid, 60, 60, 60);
        star.Rating = 5;
        star.LevelDay = 0;
        Check(TalentRules.CanPromote(star, 200, 120, 60), "tech: a strong mid long enough at the level can be promoted");
        Check(!TalentRules.CanPromote(star, 10, 120, 60), "tech: not before their time at the level");
        Check(!TalentRules.CanPromote(star, 200, 120, 40), "tech: not with a main skill short of the next level");
        TalentRules.Promote(star, 200);
        Check(star.Level == TalentCatalogue.Senior && star.LevelDay == 200, "tech: a promotion is one level up");
        Profile top = Person(TalentCatalogue.QaTester, TalentCatalogue.Staff, 90, 90, 90);
        top.Rating = 5;
        Check(!TalentRules.CanPromote(top, 500, 120, 95), "tech: no promotion past the role's highest level");
        Profile asks = Person(0, 1, 50, 50, 50);
        asks.RaiseAsk = 300;
        int morale = asks.Morale;
        TalentRules.Refused(asks);
        Check(asks.RaiseAsk == 0 && asks.Morale < morale, "tech: a refused rise is settled and costs morale");
    }

    private static void TechNotice()
    {
        Profile p = Person(0, 1, 50, 50, 50);
        p.Joined = 0;
        int year = 120;
        Check(TalentRules.OnProbation(p, 10, year) && TalentRules.NoticeDays(p, 10, year) == TalentCatalogue.ProbationNotice, "tech: a week's notice on probation");
        Check(TalentRules.Severance(p, 10, year, 100000, LeaveKind.LaidOff) == 0, "tech: no severance on probation");
        Check(TalentRules.NoticeDays(p, 3 * year + 5, year) == 3 * TalentCatalogue.NoticePerYear, "tech: a week of notice a year of service");
        Check(TalentRules.NoticeDays(p, 40 * year, year) == TalentCatalogue.NoticeMost, "tech: notice stops at twelve weeks");
        Check(TalentRules.Severance(p, 3 * year + 5, year, 100000, LeaveKind.LaidOff) == 3 * 100000, "tech: a lay-off after three years pays three weeks");
        Check(TalentRules.Severance(p, year + 5, year, 100000, LeaveKind.LaidOff) == TalentCatalogue.SeveranceLeastWeeks * 100000, "tech: two weeks at the least");
        Check(TalentRules.Severance(p, 3 * year, year, 100000, LeaveKind.Resigned) == 0, "tech: nothing for whoever resigns");
        Check(!TalentRules.FairDismissal(p, 3 * year, year), "tech: a dismissal after probation is not fair without poor ratings");
        p.LowRuns = TalentCatalogue.LowRatingsToGo;
        Check(TalentRules.FairDismissal(p, 3 * year, year), "tech: two ratings of 1 in a row make it fair");
        TalentRules.Notice(p, 3 * year, year, LeaveKind.LaidOff);
        Check(p.NoticeEnd == 3 * year + TalentRules.NoticeDays(p, 3 * year, year) && p.Leaving == (int)LeaveKind.LaidOff, "tech: notice served runs its days");
        Profile d = Person(0, 1, 50, 50, 50);
        TalentRules.Notice(d, 50, year, LeaveKind.Dismissed);
        Check(d.NoticeEnd == 50, "tech: a dismissal is at once");
    }

    private static void TechCourses()
    {
        Check(TalentRules.CourseGain(0) == TalentCatalogue.CourseGain && TalentRules.CourseGain(95) == 2, "tech: a course adds eight points from nothing and two near the top");
        Check(TalentRules.CourseFee(80) / TalentRules.CourseGain(80) > TalentRules.CourseFee(20) / TalentRules.CourseGain(20), "tech: courses for the able cost more for each point");
        Profile p = Person(TalentCatalogue.DevOps, 1, 50, 50, 50);
        int a = p.A;
        TalentRules.StartCourse(p, 0, 10, -1);
        Check(p.OnCourse(11) && TalentRules.EndCourse(p, 1, 11) == 0, "tech: a course runs its days");
        Check(TalentRules.EndCourse(p, 1, 10 + TalentCatalogue.CourseDays) == 1 && p.A == a + TalentRules.CourseGain(50) * 1000 && p.CourseSkill == -1, "tech: a course ended adds its points");
        int cloud = 0;   // cloud associate: automation 40
        Check(TalentRules.ExamChance(p, cloud) == TalentCatalogue.ExamPassBase + 2 * (p.A / 1000 - 40), "tech: the pass chance rises two a point over the score needed");
        Profile weak = Person(TalentCatalogue.DevOps, 0, 20, 20, 20);
        Check(TalentRules.ExamChance(weak, cloud) == 0, "tech: no exam under the score it needs");
        Check(TalentRules.ExamChance(p, 6) == 0, "tech: no exam in a skill the role does not have");
        int passed = 0;
        for (int n = 0; n < 200; n++)
        {
            Profile q = Person(TalentCatalogue.DevOps, 1, 50, 50, 50);
            q.Id = "x" + n;
            TalentRules.StartCourse(q, 0, 0, cloud);
            passed += TalentRules.EndCourse(q, 3, TalentCatalogue.ExamDays) == 2 && q.Has(cloud) ? 1 : 0;
        }
        Check(passed > 120 && passed < 180, "tech: about three in four pass at a 75% chance (" + passed + " of 200)");
        p.Certs = (1 << 1) | (1 << 2);
        Check(TalentRules.CertPayBp(p) == TalentCatalogue.Certs[1].PayBp + TalentCatalogue.Certs[2].PayBp, "tech: certificates add to the going rate");
    }

    private static void TechGoing()
    {
        RoleDef engineer = TalentCatalogue.Role(0), cto = TalentCatalogue.Role(TalentCatalogue.Cto), qa = TalentCatalogue.Role(TalentCatalogue.QaTester);
        Check(TalentRules.Going(3000, engineer, 1000, 0) == 3000, "tech: an engineer's going rate is the game's wage for the skill");
        Check(TalentRules.Going(3000, cto, 1000, 0) > TalentRules.Going(3000, engineer, 1000, 0) && TalentRules.Going(3000, qa, 1000, 0) < 3000, "tech: a CTO is dearer than an engineer, a tester cheaper");
        Check(TalentRules.Going(3000, engineer, 1300, 0) == 3900, "tech: a shortage puts the rate up");
        Check(TalentRules.Going(100, engineer, 1000, 0) == TalentCatalogue.RateLeast, "tech: never under the floor");
    }

    private static void TechRivals()
    {
        int juniors = 0, happy = 0, unhappy = 0, clause = 0;
        for (int day = 0; day < 4000; day++)
        {
            Profile j = Person(0, TalentCatalogue.Junior, 30, 30, 30);
            Profile h = Person(0, TalentCatalogue.Senior, 70, 70, 70);
            Profile u = Person(0, TalentCatalogue.Senior, 70, 70, 70);
            Profile c = Person(0, TalentCatalogue.Senior, 70, 70, 70);
            h.Morale = 900;
            u.Morale = 200;
            c.Morale = 200;
            c.NonCompete = true;
            juniors += TalentRules.Approached(5, j, day, 1000, 0, 3000, 3000) ? 1 : 0;
            happy += TalentRules.Approached(5, h, day, 1000, 0, 3300, 3000) ? 1 : 0;
            unhappy += TalentRules.Approached(5, u, day, 1000, 0, 2400, 3000) ? 1 : 0;
            clause += TalentRules.Approached(5, c, day, 1000, 0, 2400, 3000) ? 1 : 0;
        }
        Check(juniors == 0, "tech: rivals do not come for juniors");
        Check(unhappy > happy * 2, "tech: rivals come more often for the unhappy and underpaid (" + happy + " against " + unhappy + ")");
        Check(clause < unhappy && clause > 0, "tech: a restraint clause cuts the approaches (" + clause + " against " + unhappy + ")");
        Profile p = Person(0, TalentCatalogue.Senior, 70, 70, 70);
        TalentRules.Offer(p, "A Rival", 10);
        Check(TalentRules.OfferOpen(p, 12) && !TalentRules.Approached(5, p, 11, 5000, -10, 1, 9999), "tech: an open offer waits and brings no second one");
        TalentRules.Answer(p, true, 12, 120);
        Check(!TalentRules.OfferOpen(p, 12) && p.NoticeEnd == 0 && p.PoachDay > 12, "tech: a matched offer keeps them, and rivals rest");
        Profile q = Person(0, TalentCatalogue.Senior, 70, 70, 70);
        TalentRules.Offer(q, "A Rival", 10);
        TalentRules.Answer(q, false, 11, 120);
        Check(q.Leaving == (int)LeaveKind.Poached && q.NoticeEnd > 11, "tech: an offer not matched sends them to the rival after their notice");
    }

    private static void TechPeopleSave()
    {
        var f = new TalentFile { Seed = 77, FirstDay = 3, LastDay = 40, NextPro = 9 };
        Profile p = TalentRules.Make(77, 2, TalentCatalogue.Accountant, TalentCatalogue.Senior, 5);
        p.Id = "pro:4";
        p.Name = "Pat | Quill;~";
        p.Place = "Main%20Street~12";
        p.Wage = 4567;
        p.NonCompete = true;
        p.Options = 1200;
        p.AskEnd = 44;
        f.People.Add(p);
        Profile e = TalentRules.Make(77, 3, 0, 1, 5);
        e.Id = "AbC123==";
        f.People.Add(e);
        string line = TalentStore.Write(f);
        int version;
        TalentFile back = TalentStore.Read(line, out version);
        Profile p2 = back.Of("pro:4");
        Check(version == TalentStore.Version && back.Seed == 77 && back.LastDay == 40 && back.NextPro == 9 && back.People.Count == 2, "tech: the people's line keeps its header and everyone");
        Check(p2 != null && p2.Payroll && p2.Role == TalentCatalogue.Accountant && p2.A == p.A && p2.Wage == 4567 && p2.NonCompete && p2.Options == 1200 && p2.AskEnd == 44,
            "tech: a professional of the payroll comes back as written");
        Check(p2 != null && p2.Place == "Main%20Street~12", "tech: a place key keeps its '~'");
        Check(p2 != null && p2.Name.IndexOf('|') < 0 && p2.Name.IndexOf(';') < 0, "tech: a name loses the dividers");
        Check(back.Of("AbC123==") != null, "tech: a game id with '=' comes back");
        var data = new Dictionary<string, string> { { TalentStore.Key, "nonsense" } };
        Check(TalentStore.Load(data, out version) == null, "tech: a line that cannot be read gives no file");
    }

    private static void TechStories()
    {
        var news = new TechNews();
        Check(news.Add(5, "k1", "business", "A") && !news.Add(5, "k1", "business", "A"), "tech: the same story is printed once");
        news.Add(5, "k2", "business", "B");
        news.Add(5, "k3", "business", "C");
        Check(!news.Add(5, "k4", "business", "D"), "tech: three stories a day at most");
        var into = new List<TechStory>();
        news.For(5, into);
        Check(into.Count == 3, "tech: a day's paper gets its stories");
        TechNews back = TechNews.Read(news.Write());
        var again = new List<TechStory>();
        back.For(5, again);
        Check(again.Count == 3 && again[0].A == "A", "tech: the stories survive a save");
        for (int i = 0; i < 60; i++)
        {
            news.Add(10 + i, "k", "n", i.ToString());
        }
        Check(news.Stories.Count == TechNews.Most, "tech: the oldest stories go when the list is full");
    }
}
