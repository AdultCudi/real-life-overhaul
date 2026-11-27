using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Health;
using RealLifeOverhaul.Ui;

// The fitness meter (Scripts/Home/FitnessRules.cs, HomeFitness, HomeNeedsBar, the HUD's seventh need, the Health page's fitness block; Scripts/Careers/Work/FitnessJobRules.cs and
// CareersDirector.Fitness.cs; run with --fitness29): what diet is worth and that it stays smaller than exercise, what idle days cost and that the rule does what the docs say,
// the morning's one move with the diet option off giving exactly what it gave before, each job's minimum and the refusal's number, the warning for a worker far below it
// (never the sack), the fourteen days' trend and the "why" lines, a save from before the history loading as it did and the new line round-tripping, the pages, the options and
// the texts. Each check can fail: the sweeps below compare against the old formulas, and the rules are broken once by hand before they are trusted.
internal static partial class Program
{
    private static void Fitness29()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        FitDiet();
        FitIdle();
        FitMorning();
        FitJobs();
        FitJudge();
        FitHistory();
        FitWhyLines();
        FitSave();
        FitPages();
        FitSources();
        FitTexts();
        Console.WriteLine("  Fitness meter: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static bool FitNear(float a, float b) => Math.Abs(a - b) < 0.0001f;

    // ------------------------------------------------------------------------------------------------- diet

    private static void FitDiet()
    {
        Check(FitNear(FitnessRules.DietDay(0, 0, 0, 0, 0, 0f), 0f), "fitness diet: a day with no meals and no drink moves nothing");
        Check(FitNear(FitnessRules.DietDay(1, 0, 0, 5, 0, 0f), 0.2f) && FitNear(FitnessRules.DietDay(0, 1, 0, 5, 0, 0f), 0.05f), "fitness diet: a meal at home is worth 0.2 and one out of the fridge 0.05");
        Check(FitNear(FitnessRules.DietDay(0, 0, 1, 10, 2, 0f), -0.3f), "fitness diet: a meal bought ready to eat costs 0.3 when junk is a small part of the week");
        Check(FitNear(FitnessRules.DietDay(0, 0, 1, 10, 6, 0f), -0.45f) && FitNear(FitnessRules.DietDay(0, 0, 1, 10, 5, 0f), -0.3f),
            "fitness diet: junk is half as much again once it is 60 per cent of the week's meals, and not a meal before");
        Check(FitNear(FitnessRules.DietDay(3, 0, 0, 9, 0, 0f), 0.4f), "fitness diet: the best a day's food can do is 0.4");
        Check(FitNear(FitnessRules.DietDay(0, 0, 6, 6, 6, 0f), -1f), "fitness diet: the worst a day's food can do is 1");
        Check(FitNear(FitnessRules.DietDay(0, 0, 0, 0, 0, 1.9f), 0f) && FitNear(FitnessRules.DietDay(0, 0, 0, 0, 0, 2f), -0.25f) && FitNear(FitnessRules.DietDay(0, 0, 0, 0, 0, 4f), -0.5f),
            "fitness diet: under two units is no night, two is a night of drinks, four a heavy one");

        bool inside = true, parts = true;
        for (int home = 0; home <= 5; home++)
        {
            for (int plain = 0; plain <= 4; plain++)
            {
                for (int junk = 0; junk <= 6; junk++)
                {
                    for (int week = 0; week <= 3; week++)
                    {
                        float drink = week * 1.7f;
                        float d = FitnessRules.DietDay(home, plain, junk, home + plain + junk + week * 3, junk * (week + 1), drink, out float g, out float j, out float k);
                        inside &= d >= FitnessRules.DietDayLeast - 0.0001f && d <= FitnessRules.DietDayMost + 0.0001f;
                        parts &= FitNear(g + j + k, d) && g >= 0f && j <= 0f && k <= 0f;
                    }
                }
            }
        }
        Check(inside, "fitness diet: whatever is eaten and drunk, a day moves fitness by between -1 and +0.4");
        Check(parts, "fitness diet: the parts that say why (home meals, junk, drinks) always add up to the day's move");

        // Exercise stays the main lever.
        float halfHourFloor = HomeBalance.FitExercise * 0.5f;
        Check(FitnessRules.DietDayMost < halfHourFloor && -FitnessRules.DietDayLeast < halfHourFloor,
            "fitness diet: the most or least a whole day of food does is less than half an hour of floor exercises (" + halfHourFloor + ")");
        Check(7f * FitnessRules.DietDayMost < HomeBalance.FitWorkout && 7f * -FitnessRules.DietDayLeast < HomeBalance.FitJog,
            "fitness diet: a week of the best diet is worth less than an hour on a machine, and a week of the worst is undone by an hour's run");
        Check(-FitnessRules.DietDayLeast > 0f && FitnessRules.DietDayMost > 0f && FitnessRules.DietDayMost < -FitnessRules.DietDayLeast,
            "fitness diet: food holds fitness up less than a bad day takes off it");
        Check(FitNear(FitnessRules.EffortShare(0f), 1f) && FitNear(FitnessRules.EffortShare(100f), 0.75f) && FitNear(FitnessRules.EffortShare(-5f), 1f) && FitNear(FitnessRules.EffortShare(300f), 0.75f),
            "fitness benefits: a run costs the whole energy at fitness 0 and three quarters at 100, held at both ends");
        Check(FitnessRules.IllnessShare(10f) == 1100 && FitnessRules.IllnessShare(20f) == 1000 && FitnessRules.IllnessShare(49f) == 1000 && FitnessRules.IllnessShare(50f) == 900
              && FitnessRules.IllnessShare(69f) == 900 && FitnessRules.IllnessShare(70f) == 800 && FitnessRules.IllnessShare(100f) == 800,
            "fitness benefits: a body under 20 meets a tenth more of Home's illness chances, from 50 a tenth less and from 70 a fifth less");
    }

    // ------------------------------------------------------------------------------------------------- doing nothing

    private static void FitIdle()
    {
        Check(FitNear(HomeBalance.FitIdlePerDay, -0.7f), "fitness idle: the figure the docs give, 0.7 a day, is the rule's");
        Check(FitnessRules.IdleDays(10, 10, 1) == 0 && FitnessRules.IdleDays(11, 10, 1) == 0 && FitnessRules.IdleDays(12, 10, 1) == 0,
            "fitness idle: the day of the last exercise and the two after cost nothing");
        Check(FitnessRules.IdleDays(13, 10, 1) == 1, "fitness idle: the third day without exercise is the first that costs");
        Check(FitnessRules.IdleDays(13, 10, 3) == 1 && FitnessRules.IdleDays(13, 10, 5) == 1,
            "fitness idle: several days passing at once charge only the days past the grace (the old rule charged every one of them)");
        Check(FitnessRules.IdleDays(20, 10, 1) == 1 && FitnessRules.IdleDays(20, 10, 30) == 8 && FitnessRules.IdleDays(20, 10, 0) == 0,
            "fitness idle: never more days than went by, and none when none did");
        bool same = true;
        for (int sinceLast = 0; sinceLast <= 40; sinceLast++)
        {
            int oldDays = sinceLast >= 3 ? 1 : 0;   // what the old rule charged for one day passing
            same &= FitnessRules.IdleDays(100, 100 - sinceLast, 1) == oldDays;
        }
        Check(same, "fitness idle: for the ordinary day passing, the rule charges exactly what it always did");
        Check(FitnessRules.IdleDays(5, 0, 1) == 1 && FitnessRules.IdleDays(2, 0, 1) == 0, "fitness idle: a new game with no exercise yet starts slipping on its third day");
    }

    // ------------------------------------------------------------------------------------------------- the morning

    private static void FitMorning()
    {
        bool offSame = true, noMealsSame = true, ledgerOk = true;
        foreach (float easy in new[] { 1f, 0.5f })
        {
            for (int fitness = 0; fitness <= 100; fitness += 7)
            {
                for (int sinceLast = 0; sinceLast <= 8; sinceLast++)
                {
                    float old = sinceLast >= 3 ? Math.Max(0f, Math.Min(100f, fitness + HomeBalance.FitIdlePerDay * 1 * easy)) : fitness;
                    var day = new FitDay();
                    float off = FitnessRules.Morning(fitness, 50, 50 - sinceLast, 1, easy, false, 5, 3, 4, 20, 9, 5f, ref day);
                    offSame &= FitNear(off, old) && day.DietHome == 0f && day.DietJunk == 0f && day.DietDrink == 0f && day.HomeMeals == 0 && day.JunkMeals == 0 && day.DrinkNights == 0;
                    var day2 = new FitDay();
                    float on = FitnessRules.Morning(fitness, 50, 50 - sinceLast, 1, easy, true, 0, 0, 0, 0, 0, 0f, ref day2);
                    noMealsSame &= FitNear(on, old);
                    ledgerOk &= FitNear(day.Idle, old - fitness);
                }
            }
        }
        Check(offSame, "fitness morning: with the diet option off the morning moves fitness exactly as before, whatever was eaten or drunk, and writes no diet into the ledger");
        Check(noMealsSame, "fitness morning: with the option on and nothing eaten or drunk the morning is the old one too");
        Check(ledgerOk, "fitness morning: the idle days' move is what the ledger says it was");

        var today = new FitDay();
        float junky = FitnessRules.Morning(50f, 50, 49, 1, 1f, true, 0, 0, 4, 8, 6, 0f, ref today);
        Check(FitNear(junky, 49f) && FitNear(today.DietJunk, -1f) && today.JunkMeals == 4 && today.DietHome == 0f, "fitness morning: four meals bought out, most of the week, cost the most a day can, and the ledger says junk");
        today = new FitDay();
        float home = FitnessRules.Morning(50f, 50, 49, 1, 1f, true, 1, 1, 0, 6, 0, 0f, ref today);
        Check(FitNear(home, 50.25f) && FitNear(today.DietHome, 0.25f) && today.HomeMeals == 2, "fitness morning: a meal at home and one from the fridge hold fitness up 0.25");
        today = new FitDay();
        float floor = FitnessRules.Morning(0f, 50, 30, 1, 1f, true, 0, 0, 4, 8, 6, 5f, ref today);
        Check(FitNear(floor, 0f) && FitNear(today.DietJunk + today.DietDrink + today.Idle, 0f), "fitness morning: at the bottom nothing is taken off and the ledger claims nothing");
        today = new FitDay();
        float top = FitnessRules.Morning(99.9f, 50, 49, 1, 1f, true, 3, 0, 0, 3, 0, 0f, ref today);
        Check(FitNear(top, 100f) && today.DietHome > 0f && today.DietHome <= 0.1001f, "fitness morning: at the top only the room that is left is claimed");
        today = new FitDay();
        FitnessRules.Morning(60f, 50, 49, 1, 1f, true, 0, 0, 0, 0, 0, 3f, ref today);
        Check(today.DrinkNights == 1 && FitNear(today.DietDrink, -0.25f), "fitness morning: a night of drinks is counted once and costs a quarter of a point");

        // Over a long run the diet alone can never beat a little exercise: 30 days of the best food against 30 of the worst, with and without one hour of running a week.
        float best = 50f, worst = 50f, withRun = 50f;
        for (int d = 0; d < 30; d++)
        {
            var t = new FitDay();
            best = FitnessRules.Morning(best, 100 + d, 100 + d - 1, 1, 1f, true, 3, 0, 0, 3, 0, 0f, ref t);
            t = new FitDay();
            worst = FitnessRules.Morning(worst, 100 + d, 100 + d - 1, 1, 1f, true, 0, 0, 4, 8, 6, 0f, ref t);
            t = new FitDay();
            withRun = FitnessRules.Morning(withRun, 100 + d, 100 + d - 1, 1, 1f, true, 0, 0, 4, 8, 6, 0f, ref t);
            if (d % 7 == 0)
            {
                withRun = Math.Min(100f, withRun + HomeBalance.FitJog);   // an hour of running a week
            }
        }
        Check(best > 55f && best <= 50f + 30f * FitnessRules.DietDayMost + 0.01f, "fitness morning: a month of the best food alone adds a few points and no more than 12 (" + best + ")");
        Check(worst < 30f && worst >= 50f + 30f * FitnessRules.DietDayLeast - 0.01f, "fitness morning: a month of the worst food alone takes off a good deal but never more than 30 (" + worst + ")");
        Check(withRun > worst + 20f, "fitness morning: an hour of running a week more than makes up for a month of the worst food (" + withRun + " against " + worst + ")");
    }

    // ------------------------------------------------------------------------------------------------- the jobs

    private static Dictionary<string, string> FitEn() => LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));

    private static void FitJobs()
    {
        var physical = new HashSet<int> { ProCatalogue.Police, ProCatalogue.Warehouse, ProCatalogue.SecurityGuard, ProCatalogue.Carpenter, ProCatalogue.Plumber, ProCatalogue.Electrician };
        bool onlyThose = true;
        for (int p = 0; p < ProCatalogue.Count; p++)
        {
            onlyThose &= (FitnessJobRules.Minimum(p) > 0) == physical.Contains(p) && FitnessJobRules.Physical(p) == physical.Contains(p);
        }
        Check(onlyThose && ProCatalogue.Count == 26, "fitness jobs: only the police, the warehouse, the security guard and the three building trades ask for fitness; the other twenty professions ask nothing");
        Check(FitnessJobRules.Minimum(ProCatalogue.Police) == 45 && FitnessJobRules.Minimum(ProCatalogue.Warehouse) == 20 && FitnessJobRules.Minimum(ProCatalogue.SecurityGuard) == 20
              && FitnessJobRules.Minimum(ProCatalogue.Carpenter) == 15 && FitnessJobRules.Minimum(ProCatalogue.Plumber) == 15 && FitnessJobRules.Minimum(ProCatalogue.Electrician) == 15,
            "fitness jobs: the police ask 45, the warehouse and the guard 20, the trades 15");
        Check(FitnessJobRules.Minimum(-1) == 0 && FitnessJobRules.Minimum(999) == 0, "fitness jobs: a profession that is not there asks nothing");
        Check(30 >= FitnessJobRules.HeavyMinimum && 30 >= FitnessJobRules.TradeMinimum && 30 < FitnessJobRules.PoliceMinimum,
            "fitness jobs: a new game's fitness (30) opens every physical job but the police");
        float idleFortnight = 30f + HomeBalance.FitIdlePerDay * 14f;
        Check(idleFortnight >= FitnessJobRules.HeavyMinimum, "fitness jobs: a fortnight of doing nothing past the grace still leaves the warehouse and the guard open (" + idleFortnight + ")");
        Check(FitnessJobRules.PoliceMinimum - 30 <= FitnessJobRules.WeeklyGain, "fitness jobs: from a new game's 30 the police are about a week of regular exercise away, as the refusal says");

        int need;
        Check(FitnessJobRules.Short(ProCatalogue.Police, 44, true, out need) && need == 45 && !FitnessJobRules.Short(ProCatalogue.Police, 45, true, out need) && need == 45,
            "fitness jobs: 44 is short of the police's 45 and 45 is enough");
        Check(!FitnessJobRules.Short(ProCatalogue.Police, 10, false, out need) && need == 45, "fitness jobs: with the option off nobody is short of anything");
        Check(!FitnessJobRules.Short(ProCatalogue.Police, -1, true, out _), "fitness jobs: a fitness that is not kept (-1) asks nothing");
        Check(!FitnessJobRules.Short(ProCatalogue.Developer, 0, true, out need) && need == 0 && !FitnessJobRules.Short(ProCatalogue.Nurse, 0, true, out _),
            "fitness jobs: a desk job or a nurse's is never turned away for fitness");
        Check(FitnessJobRules.Weeks(1) == 1 && FitnessJobRules.Weeks(18) == 1 && FitnessJobRules.Weeks(19) == 2 && FitnessJobRules.Weeks(36) == 2 && FitnessJobRules.Weeks(37) == 3,
            "fitness jobs: the refusal's weeks are whole weeks of three sessions");
        Check(FitnessJobRules.Tier(6) == 0 && FitnessJobRules.Tier(7) == 1 && FitnessJobRules.Tier(18) == 1 && FitnessJobRules.Tier(19) == 2, "fitness jobs: the way to get there is told near, middling or far by the gap");
        bool variants = true;
        for (int prof = 0; prof < ProCatalogue.Count; prof++)
        {
            for (int fit = 0; fit < 50; fit += 3)
            {
                for (int day = 1; day < 40; day += 5)
                {
                    int v = FitnessJobRules.Variant(prof, fit, day, 4);
                    variants &= v >= 0 && v < 4 && v == FitnessJobRules.Variant(prof, fit, day, 4);
                }
            }
        }
        Check(variants && FitnessJobRules.Variant(1, 2, 3, 1) == 0 && FitnessJobRules.Variant(1, 2, 3, 0) == 0, "fitness jobs: a wording is chosen from a hash, the same for the same day and always inside the list");
        var seen = new HashSet<int>();
        for (int day = 1; day < 60; day++)
        {
            seen.Add(FitnessJobRules.Variant(ProCatalogue.Police, 31, day, 4));
        }
        Check(seen.Count == 4, "fitness jobs: over two months every one of the four wordings of a refusal comes up");

        // The refusal's words: the figures are in them.
        Dictionary<string, string> en = FitEn();
        Check(en != null, "fitness jobs: en.json was read");
        if (en == null)
        {
            return;
        }
        string police = en.TryGetValue("reallife_fitness_why_police", out string pt) ? pt : string.Empty;
        string job = en.TryGetValue("reallife_fitness_why_job", out string jt) ? jt : string.Empty;
        Check(police.Contains("{need}") && police.Contains("{have}") && police.Contains("{tip}") && job.Contains("{need}") && job.Contains("{have}") && job.Contains("{tip}") && job.Contains("{job}"),
            "fitness jobs: a refusal names the fitness needed, the player's and how to get there");
        string said = police.Replace("{need}", "45").Replace("{have}", "31").Replace("{tip}", en["reallife_fitness_tip_mid_1"]);
        Check(said == "The fitness test: you need fitness 45 to join the police. You are at 31. A week or two of running would do it.", "fitness jobs: the police's refusal reads as the player was told it would (" + said + ")");
        bool tips = true, lengths = true, weeksOnlyFar = true;
        foreach (string[] tier in new[] { FitnessJobRules.NearTips, FitnessJobRules.MidTips, FitnessJobRules.FarTips })
        {
            tips &= tier.Length == 4;
            foreach (string key in tier)
            {
                tips &= en.ContainsKey(key);
                string tip = en.TryGetValue(key, out string text) ? text : string.Empty;
                weeksOnlyFar &= tier != FitnessJobRules.FarTips ? !tip.Contains("{weeks}") : true;
                foreach (string line in new[] { police, job })
                {
                    string filled = line.Replace("{need}", "45").Replace("{have}", "10").Replace("{job}", "building trades").Replace("{tip}", tip.Replace("{weeks}", "10").Replace("{gap}", "35"));
                    lengths &= filled.Length <= 240 && !filled.Contains("{");
                }
            }
        }
        Check(tips && lengths && weeksOnlyFar, "fitness jobs: all twelve tips exist, none leaves a figure unfilled or a notice over 240 characters, and only the far ones count weeks");
        Check(FitnessJobRules.WarnTexts.Length == 3 && FitnessJobRules.StrikeTexts.Length == 3 && FitnessJobRules.WarnTexts.Concat(FitnessJobRules.StrikeTexts).All(k => en.ContainsKey(k))
              && en.ContainsKey("reallife_fitness_note_warning") && en.ContainsKey("reallife_fitness_note_strike") && en.ContainsKey("reallife_fitness_note_clear"),
            "fitness jobs: the boss's warning and strike have three wordings each, and a notification for where a text cannot be sent");
        bool bossFits = true;
        foreach (string k in FitnessJobRules.WarnTexts.Concat(FitnessJobRules.StrikeTexts).Concat(new[] { "reallife_fitness_note_warning", "reallife_fitness_note_strike", "reallife_fitness_note_clear" }))
        {
            string t = en[k].Replace("{employer}", "Hartley Haulage Limited").Replace("{need}", "45").Replace("{have}", "17").Replace("{days}", "14").Replace("{count}", "2").Replace("{most}", "3");
            bossFits &= t.Length <= 240 && !t.Contains("{");
        }
        Check(bossFits, "fitness jobs: every warning fills whole and stays within a notice's 240 characters");
    }

    // ------------------------------------------------------------------------------------------------- the warning

    private static void FitJudge()
    {
        const int toGo = 3;
        int police = ProCatalogue.Police;   // asks 45: far below is under 35
        Check(FitnessJobRules.Judge(police, 50, true, 0, 0, 0, toGo, 100) == FitnessCheck.None, "fitness warning: enough fitness, nothing to say");
        Check(FitnessJobRules.Judge(police, 36, true, 0, 0, 0, toGo, 100) == FitnessCheck.None, "fitness warning: under the minimum but not far under (36 of 45), nothing to say");
        Check(FitnessJobRules.Judge(police, 35, true, 0, 0, 0, toGo, 100) == FitnessCheck.None, "fitness warning: 35 is 10 under 45, which is not yet far under");
        Check(FitnessJobRules.Judge(police, 34, true, 0, 0, 0, toGo, 100) == FitnessCheck.Warn, "fitness warning: 34 of 45 is far under, and the first look warns");
        Check(FitnessJobRules.Judge(police, 20, true, 100, 0, 0, toGo, 105) == FitnessCheck.None, "fitness warning: five days after the warning there is nothing more, the time to put it right is running");
        Check(FitnessJobRules.Judge(police, 20, true, 100, 0, 0, toGo, 113) == FitnessCheck.None && FitnessJobRules.Judge(police, 20, true, 100, 0, 0, toGo, 114) == FitnessCheck.Strike,
            "fitness warning: the strike comes when the fourteen days are up, not a day before");
        Check(FitnessJobRules.Judge(police, 20, true, 100, 114, 1, toGo, 127) == FitnessCheck.None && FitnessJobRules.Judge(police, 20, true, 100, 114, 1, toGo, 128) == FitnessCheck.Strike,
            "fitness warning: the next strike is another fourteen days on");
        Check(FitnessJobRules.Judge(police, 20, true, 100, 128, 2, toGo, 400) == FitnessCheck.None, "fitness warning: a worker on the last warning is never given another for fitness");
        Check(FitnessJobRules.Judge(police, 40, true, 100, 114, 1, toGo, 120) == FitnessCheck.Clear, "fitness warning: back above 'far under' lifts the warning");
        Check(FitnessJobRules.Judge(police, 20, false, 0, 0, 0, toGo, 100) == FitnessCheck.None && FitnessJobRules.Judge(police, 20, false, 100, 0, 0, toGo, 300) == FitnessCheck.None,
            "fitness warning: with the option off nobody is warned or struck");
        Check(FitnessJobRules.Judge(ProCatalogue.Developer, 0, true, 0, 0, 0, toGo, 100) == FitnessCheck.None && FitnessJobRules.Judge(police, -1, true, 0, 0, 0, toGo, 100) == FitnessCheck.None,
            "fitness warning: a desk job asks nothing and an unknown fitness is not far under");
        Check(FitnessJobRules.Judge(ProCatalogue.Carpenter, 4, true, 0, 0, 0, toGo, 100) == FitnessCheck.Warn && FitnessJobRules.Judge(ProCatalogue.Carpenter, 5, true, 0, 0, 0, toGo, 100) == FitnessCheck.None,
            "fitness warning: every job is judged by its own minimum (the trades' 15: far under is 4 or less)");

        // The whole story, day by day, for a worker who never gets fitter: a warning, two strikes, and then nothing more however long it lasts.
        int warnDay = 0, strikeDay = 0, warnings = 0, strikes = 0, warns = 0;
        int lastStrike = 0;
        for (int day = 100; day < 700; day++)
        {
            FitnessCheck c = FitnessJobRules.Judge(police, 5, true, warnDay, strikeDay, warnings, toGo, day);
            if (c == FitnessCheck.Warn)
            {
                warnDay = day;
                strikeDay = 0;
                warns++;
            }
            else if (c == FitnessCheck.Strike)
            {
                strikeDay = day;
                warnings++;
                strikes++;
                lastStrike = day;
            }
        }
        Check(warns == 1 && strikes == 2 && warnings == toGo - 1 && lastStrike == 128, "fitness warning: a worker who never improves is warned once, struck twice and left on the last warning, never sacked by fitness (" + warns + " warning, " + strikes + " strikes, last on day " + lastStrike + ")");

        // A worker who recovers and slips again is warned again.
        warnDay = strikeDay = 0;
        FitnessCheck first = FitnessJobRules.Judge(police, 20, true, warnDay, strikeDay, 0, toGo, 10);
        warnDay = 10;
        FitnessCheck cleared = FitnessJobRules.Judge(police, 44, true, warnDay, strikeDay, 0, toGo, 12);
        warnDay = 0;
        FitnessCheck again = FitnessJobRules.Judge(police, 20, true, warnDay, strikeDay, 0, toGo, 30);
        Check(first == FitnessCheck.Warn && cleared == FitnessCheck.Clear && again == FitnessCheck.Warn, "fitness warning: getting back to form lifts the warning, and slipping again earns a new one");
        Check(FitnessJobRules.Deadline(100) == 114 && FitnessJobRules.WarnDays == 14 && FitnessJobRules.FarBelow == 10, "fitness warning: the days to put it right are fourteen from the warning");
    }

    // ------------------------------------------------------------------------------------------------- the history

    private static string FitLine(string extra) => "h1;hy=80;st=20;td=75;ft=44;sd=0;day=10;fw=40;tr=3;lw=9;mt=1" + extra + ";diet=0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0";

    private static void FitHistory()
    {
        HomeState.Reset();
        HomeState.Load(new Dictionary<string, string> { { HomeState.Key, FitLine(string.Empty) } });
        Check(HomeState.FitKnown == 0 && HomeState.StartOf(0) < 0f && HomeState.LedgerOf(0).Empty, "fitness trend: a save from before the history starts with no days kept");
        float fitness = 44f;
        bool starts = true, known = true;
        for (int day = 11; day <= 30; day++)
        {
            fitness += 1f;
            HomeState.Fitness = fitness;
            int passed = HomeState.NewDay(day);
            HomeState.StartedDay();
            starts &= passed == 1 && FitNear(HomeState.StartOf(0), fitness) && (day < 12 || FitNear(HomeState.StartOf(1), fitness - 1f));
            known &= HomeState.FitKnown == Math.Min(day - 10, FitnessView.Days);
        }
        Check(starts, "fitness trend: each new day writes its first figure and moves yesterday's back one place");
        Check(known && HomeState.FitKnown == 14, "fitness trend: the days kept grow a day at a time and stop at fourteen");
        Check(FitNear(HomeState.StartOf(13), fitness - 13f) && HomeState.StartOf(14) < 0f && HomeState.StartOf(-1) < 0f, "fitness trend: the oldest day kept is thirteen days back and nothing is made up beyond it");

        // days that nobody lived through in play
        HomeState.Fitness = fitness + 6f;
        int skipped = HomeState.NewDay(34);
        HomeState.StartedDay();
        Check(skipped == 4 && FitNear(HomeState.StartOf(0), fitness + 6f) && FitNear(HomeState.StartOf(1), fitness + 6f) && FitNear(HomeState.StartOf(3), fitness + 6f) && FitNear(HomeState.StartOf(4), fitness),
            "fitness trend: days that passed unseen are held at the figure now and the day before them keeps its own");

        // the ledger moves with the days
        HomeState.Today.Exercise = 8f;
        HomeState.Today.Runs = 1;
        HomeState.NewDay(35);
        Check(FitNear(HomeState.LedgerOf(1).Exercise, 8f) && HomeState.LedgerOf(1).Runs == 1 && HomeState.LedgerOf(0).Empty, "fitness trend: the day's ledger goes back with the day and today starts empty");
        HomeState.NewDay(60);
        Check(HomeState.LedgerOf(0).Empty && HomeState.LedgerOf(13).Empty && HomeState.FitKnown == 14, "fitness trend: a long time away leaves a clean ledger and the history at its most");

        Check(FitnessRules.Direction(2f) == FitnessRules.Rising && FitnessRules.Direction(-2f) == FitnessRules.Slipping && FitnessRules.Direction(1.5f) == FitnessRules.Steady && FitnessRules.Direction(-1.5f) == FitnessRules.Steady,
            "fitness trend: more than a point and a half either way is a trend");
        Check(FitnessRules.Band(0f) == 0 && FitnessRules.Band(19.9f) == 0 && FitnessRules.Band(20f) == 1 && FitnessRules.Band(35f) == 2 && FitnessRules.Band(55f) == 3 && FitnessRules.Band(70f) == 4
              && FitnessRules.Band(85f) == 5 && FitnessRules.Band(100f) == 5 && FitnessRules.BandKeys.Length == FitnessRules.Bands && FitnessRules.BandEnglish.Length == FitnessRules.Bands,
            "fitness trend: six bands, out of shape to athletic, from 0, 20, 35, 55, 70 and 85");
        Check(!FitnessRules.Noticeable(50f, 47f, 5) && FitnessRules.Noticeable(50f, 46f, 3) && FitnessRules.Noticeable(40f, 44f, 3) && !FitnessRules.Noticeable(60f, 40f, 2),
            "fitness trend: the first notice needs four points either way over at least three days kept");
        Check(FitnessRules.Whole(41.5f) == 42 && FitnessRules.Whole(42.5f) == 42 && FitnessRules.Whole(42.4f) == 42 && FitnessRules.Whole(0.4f) == 0,
            "fitness trend: a figure is rounded as the needs' bars round it, a half to the even number");
        HomeState.Reset();
    }

    // ------------------------------------------------------------------------------------------------- the why lines

    private static string Said(WhyLine line)
        => FitnessRules.WhyEnglish(line).Replace("{n}", line.N.ToString(CultureInfo.InvariantCulture)).Replace("{amount}", FitnessRules.Signed(line.Amount));

    private static void FitWhyLines()
    {
        var ledger = new FitDay[14];
        for (int i = 0; i < 3; i++)
        {
            ledger[i * 2].Runs = 1;
            ledger[i * 2].RunGain = 6f;
            ledger[i * 2].Exercise = 6f;
        }
        ledger[1].Idle = -0.7f;
        ledger[3].Idle = -0.7f;
        ledger[2].JunkMeals = 5;
        ledger[2].DietJunk = -3f;
        var into = new WhyLine[FitnessView.WhyMost];
        int n = FitnessRules.Why(ledger, 7, into);
        Check(n == 3 && into[0].Kind == FitWhy.Runs && into[0].N == 3 && into[0].Amount == 18, "fitness why: three runs worth 18 points make the first line");
        Check(Said(into[0]) == "3 runs this week: Fitness +18", "fitness why: the line reads as the player asked, '3 runs this week: Fitness +18' (" + Said(into[0]) + ")");
        Check(into[1].Kind == FitWhy.Junk && into[1].N == 5 && into[1].Amount == -3 && Said(into[1]) == "5 fast food meals: Fitness -3", "fitness why: five fast food meals worth -3 read as '5 fast food meals: Fitness -3' (" + Said(into[1]) + ")");
        Check(into[2].Kind == FitWhy.Idle && into[2].N == 2 && into[2].Amount == -1, "fitness why: two idle days cost one point and come last, the biggest movers first");
        Check(FitnessRules.WhyKey(into[0]) == "reallife_fitness_why_runs" && FitnessRules.WhyKey(new WhyLine { Kind = FitWhy.Runs, N = 1, Amount = 6 }) == "reallife_fitness_why_run_one",
            "fitness why: a count of one has a wording of its own");
        Check(FitnessRules.Signed(18) == "+18" && FitnessRules.Signed(-3) == "-3" && FitnessRules.Signed(0) == "0" && !FitnessRules.Signed(-3).Contains((char)0x2013) && !FitnessRules.Signed(-3).Contains((char)0x2014),
            "fitness why: a figure is signed with a plus or an ordinary minus, no dash");
        var old = new FitDay[14];
        old[8].Runs = 4;
        old[8].RunGain = 20f;
        Check(FitnessRules.Why(old, 7, into) == 0, "fitness why: what happened more than a week ago is not in this week's lines");
        var small = new FitDay[14];
        small[0].DietHome = 0.2f;
        small[0].HomeMeals = 1;
        Check(FitnessRules.Why(small, 7, into) == 0, "fitness why: a meal worth a few hundredths is not a line");
        var drinks = new FitDay[14];
        drinks[1].DrinkNights = 2;
        drinks[1].DietDrink = -0.75f;
        drinks[3].DrinkNights = 0;
        n = FitnessRules.Why(drinks, 7, into);
        Check(n == 1 && into[0].Kind == FitWhy.Drinks && into[0].N == 2 && into[0].Amount == -1 && Said(into[0]) == "2 nights of drinks: Fitness -1", "fitness why: nights of drinks have a line of their own");
        var every = new FitDay[14];
        for (int i = 0; i < 7; i++)
        {
            every[i] = new FitDay { Runs = 1, RunGain = 5f, Exercise = 9f, Sessions = 1, Idle = -1f, JunkMeals = 1, DietJunk = -2f, HomeMeals = 3, DietHome = 3f, DrinkNights = 1, DietDrink = -1f };
        }
        n = FitnessRules.Why(every, 7, into);
        bool sorted = true;
        for (int i = 1; i < n; i++)
        {
            sorted &= Math.Abs(into[i - 1].Amount) >= Math.Abs(into[i].Amount);
        }
        Check(n == FitnessView.WhyMost && sorted, "fitness why: six kinds at most, the biggest first");
        var kinds = new HashSet<FitWhy>();
        for (int i = 0; i < n; i++)
        {
            kinds.Add(into[i].Kind);
        }
        Check(kinds.Count == 6, "fitness why: every kind of mover has its line");
    }

    // ------------------------------------------------------------------------------------------------- the save

    private static void FitSave()
    {
        // A save from before the history.
        HomeState.Reset();
        string oldLine = FitLine(string.Empty);
        HomeState.Load(new Dictionary<string, string> { { HomeState.Key, oldLine } });
        float diet0 = HomeState.DietScore;
        Check(FitNear(HomeState.Fitness, 44f) && HomeState.Day == 10 && FitNear(HomeState.FitnessWeekAgo, 40f) && HomeState.FitKnown == 0 && !HomeState.FitTold && HomeState.DrinksToday == 0f,
            "fitness save: a save from before the history loads with its figures as they were and no history");
        var saved = new Dictionary<string, string>();
        HomeState.Save(saved);
        string line = saved[HomeState.Key];
        Check(line.Contains(";fs=") && line.Contains(";led=") && line.Contains(";fk=") && line.Contains(";fnt=") && line.Contains(";dk=") && line.IndexOf(";tkf=", StringComparison.Ordinal) < line.IndexOf(";fk=", StringComparison.Ordinal)
              && line.IndexOf(";fk=", StringComparison.Ordinal) < line.IndexOf(";diet=", StringComparison.Ordinal),
            "fitness save: the new fields are appended after the old ones, under the same version");
        Check(line.StartsWith("h1;", StringComparison.Ordinal), "fitness save: the line keeps its version, so an older build reads it");
        string[] parts = line.Split(';');
        bool oldReaderSafe = true;
        foreach (string part in parts.Skip(1))
        {
            int eq = part.IndexOf('=');
            string name = part.Substring(0, eq), value = part.Substring(eq + 1);
            if (name == "fs" || name == "led")
            {
                oldReaderSafe &= value.Contains(",") && !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);   // an older reader skips a value that is no number
            }
        }
        Check(oldReaderSafe, "fitness save: the history's two lists are not numbers, so a build without them skips them whole");
        HomeState.Reset();
        HomeState.Load(saved);
        Check(FitNear(HomeState.Fitness, 44f) && FitNear(HomeState.DietScore, diet0) && HomeState.Day == 10, "fitness save: written and read again, the old figures are the same");

        // A full history round trip.
        HomeState.Reset();
        HomeState.Load(new Dictionary<string, string> { { HomeState.Key, FitLine(string.Empty) } });
        float f = 44f;
        for (int day = 11; day <= 18; day++)
        {
            f += 1.5f;
            HomeState.Fitness = f;
            HomeState.NewDay(day);
            HomeState.StartedDay();
            HomeState.Today.Exercise = day * 0.5f;
            HomeState.Today.RunGain = day * 0.25f;
            HomeState.Today.Idle = -0.7f;
            HomeState.Today.DietHome = 0.2f;
            HomeState.Today.DietJunk = -0.3f;
            HomeState.Today.DietDrink = -0.25f;
            HomeState.Today.Runs = day % 3;
            HomeState.Today.Sessions = day % 4;
            HomeState.Today.HomeMeals = day % 5;
            HomeState.Today.JunkMeals = day % 2;
            HomeState.Today.DrinkNights = day % 2;
        }
        HomeState.Drank(1.6f);
        var data = new Dictionary<string, string>();
        HomeState.Save(data);
        int known = HomeState.FitKnown;
        var wasStart = new float[14];
        var wasLedger = new FitDay[14];
        for (int i = 0; i < 14; i++)
        {
            wasStart[i] = HomeState.StartOf(i);
            wasLedger[i] = HomeState.LedgerOf(i);
        }
        HomeState.Reset();
        HomeState.Load(data);
        bool same = HomeState.FitKnown == known && FitNear(HomeState.DrinksToday, 1.6f);
        for (int i = 0; i < 14; i++)
        {
            FitDay a = wasLedger[i], b = HomeState.LedgerOf(i);
            same &= FitNear(wasStart[i], HomeState.StartOf(i)) && FitNear(a.Exercise, b.Exercise) && FitNear(a.RunGain, b.RunGain) && FitNear(a.Idle, b.Idle) && FitNear(a.DietHome, b.DietHome)
                    && FitNear(a.DietJunk, b.DietJunk) && FitNear(a.DietDrink, b.DietDrink) && a.Runs == b.Runs && a.Sessions == b.Sessions && a.HomeMeals == b.HomeMeals && a.JunkMeals == b.JunkMeals
                    && a.DrinkNights == b.DrinkNights;
        }
        Check(same && known == 8, "fitness save: the fourteen days' figures and ledger come back as they were saved (" + known + " days kept)");

        // Damaged lines cost the trend and nothing else.
        HomeState.Reset();
        HomeState.Load(new Dictionary<string, string> { { HomeState.Key, FitLine(";fs=a,b;led=1,2,3;fk=999;fnt=1;dk=NaN") } });
        Check(FitNear(HomeState.Fitness, 44f) && HomeState.FitKnown == 14 && HomeState.FitTold && HomeState.LedgerOf(0).Empty && HomeState.DrinksToday == 0f,
            "fitness save: a damaged history loads without a fault, kept in bounds, and the old figures stand");
        HomeState.Reset();
        HomeState.Load(new Dictionary<string, string> { { HomeState.Key, FitLine(";fs=" + string.Join(",", Enumerable.Repeat("500", 14)) + ";led=" + string.Join(",", Enumerable.Repeat("9999", 154))) } });
        bool bounded = true;
        for (int i = 0; i < 14; i++)
        {
            FitDay d = HomeState.LedgerOf(i);
            bounded &= d.Exercise <= 200f && d.Idle <= 0f && d.DietHome <= 10f && d.Runs <= 99 && d.Sessions <= 99 && HomeState.StartOf(i) <= 100f;
        }
        Check(bounded, "fitness save: every figure read is put inside its bounds");
        HomeState.Reset();
        var empty = new Dictionary<string, string>();
        HomeState.Save(empty);
        Check(empty.Count == 0, "fitness save: nothing is written before the save is loaded");
    }

    // ------------------------------------------------------------------------------------------------- the pages

    private static VentModel FitPage(FitnessView fit, Action<HealthFacts> more = null)
    {
        HealthFacts f = UiSamples.HealthFactsOf("you");
        f.Fit.Known = fit.Known;
        f.Fit.Level = fit.Level;
        f.Fit.Band = fit.Band;
        f.Fit.Day = fit.Day;
        f.Fit.TrendCount = fit.TrendCount;
        f.Fit.Change = fit.Change;
        f.Fit.WhyCount = fit.WhyCount;
        f.Fit.DietCounts = fit.DietCounts;
        Array.Copy(fit.Trend, f.Fit.Trend, fit.Trend.Length);
        Array.Copy(fit.Why, f.Fit.Why, fit.Why.Length);
        more?.Invoke(f);
        var m = new VentModel();
        HealthPages.Fill(m, new VentView(), f, new UiSamples.HealthStub(), new VentWords(ModEnglish(), x => "$" + (long)x));
        return m;
    }

    private static void FitPages()
    {
        var none = new FitnessView();
        VentModel without = FitPage(none);
        VentModel before = UiSamples.Health(ModEnglish(), "you", null, new UiSamples.HealthStub());
        Check(without.Left.Length == before.Left.Length && without.Right.Length == before.Right.Length,
            "fitness page: with fitness not kept the You page is exactly what it was (the same blocks on both sides)");

        var fit = new FitnessView { Known = true, Level = 52, Band = 3, Day = 40, TrendCount = 6, Change = 12, WhyCount = 2, DietCounts = true };
        for (int i = 0; i < 6; i++)
        {
            fit.Trend[i] = 40 + i * 2;
        }
        fit.Why[0] = new WhyLine { Kind = FitWhy.Runs, N = 3, Amount = 18 };
        fit.Why[1] = new WhyLine { Kind = FitWhy.Junk, N = 5, Amount = -3 };
        VentModel page = FitPage(fit);
        VentBlock[] left = page.Left;
        Check(left.Any(b => b.Kind == VentBlockKind.Meter && b.Grade == 52 && b.Value == "52 of 100, fit"), "fitness page: the kit's You page shows the figure on a meter with its words");
        VentBlock chart = left.FirstOrDefault(b => b.Kind == VentBlockKind.Chart);
        Check(chart != null && chart.Chart.Series.Length == 1 && chart.Chart.Series[0].Values.Length == 6 && chart.Chart.Points.Length == 6
              && FitNear(chart.Chart.Series[0].Values[0], 40f) && FitNear(chart.Chart.Series[0].Values[5], 52f) && chart.Chart.Points[5] == "40" && chart.Chart.Points[0] == "35",
            "fitness page: the trend is a line over the days kept, ending on today's live figure");
        Check(left.Any(b => b.Kind == VentBlockKind.Row && b.Value == "40 to 52" && b.Tone == UiRole.Ok), "fitness page: the last two weeks read as a row, in the good colour while it rises");
        Check(left.Any(b => b.Kind == VentBlockKind.Note && b.Text == "3 runs this week: Fitness +18") && left.Any(b => b.Kind == VentBlockKind.Note && b.Text == "5 fast food meals: Fitness -3" && b.Tone == UiRole.Warn),
            "fitness page: the why lines are the sentences, a loss in the warning colour");
        Check(left.Any(b => b.Kind == VentBlockKind.Note && b.Text.Contains("meals at home hold it up")), "fitness page: with the diet on the page says what food does");
        fit.DietCounts = false;
        VentModel noDiet = FitPage(fit);
        Check(!noDiet.Left.Any(b => b.Kind == VentBlockKind.Note && b.Text != null && b.Text.Contains("meals at home hold it up")) && noDiet.Left.Any(b => b.Kind == VentBlockKind.Note && b.Text != null && b.Text.Contains("The police")),
            "fitness page: with the diet off the page does not claim food moves it, and says what jobs ask");
        fit.TrendCount = 1;
        fit.WhyCount = 0;
        VentModel young = FitPage(fit);
        Check(!young.Left.Any(b => b.Kind == VentBlockKind.Chart) && young.Left.Any(b => b.Kind == VentBlockKind.Row && b.Value == "Too early to tell") && young.Left.Any(b => b.Kind == VentBlockKind.Note && b.Text == "Nothing moved your fitness this week."),
            "fitness page: a young save says it is too early to tell and that nothing has moved it");
        fit.Known = false;
        Check(FitPage(fit).Left.Length == before.Left.Length, "fitness page: switching fitness off takes the block away again");
    }

    // ------------------------------------------------------------------------------------------------- sources and texts

    private static void FitSources()
    {
        string health = ModSource(@"Home\HomeHealth.cs"), state = ModSource(@"Home\HomeState.cs"), bar = ModSource(@"Home\HomeNeedsBar.cs"), offers = ModSource(@"Home\HomeDirector.Offers.cs"),
            fitness = ModSource(@"Home\HomeFitness.cs"), director = ModSource(@"Careers\CareersDirector.Fitness.cs"), duty = ModSource(@"Careers\Police\CareersDirector.Duty.cs"),
            work = ModSource(@"Careers\CareersDirector.Work.cs"), settings = ModSource(@"Core\Settings.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs"), tree = ModSource(@"Meta\OptionsTree.cs"),
            list = ModSource(@"Meta\FeatureList.Life.cs"), links = ModSource(@"Wiring\Wiring.FitnessLinks.cs"), wiring = ModSource(@"Wiring\Wiring.cs"), hud = ModSource(@"Ui\Hud\HudStatusGame.cs"),
            needs = ModSource(@"Ui\UiDraw.HudNeeds.cs"), hudBar = ModSource(@"Ui\UiDraw.HudBar.cs"), jog = ModSource(@"Home\JogRun.cs"), leisure = ModSource(@"Leisure\LeisureState.cs"),
            facts = ModSource(@"Health\HealthDirector.Facts.cs"), app = ModSource(@"Health\HealthApp.cs"), pages = ModSource(@"Ui\Ventures\HealthPages.cs"), applying = ModSource(@"Careers\CareersDirector.Applying.cs"),
            needsText = ModSource(@"Careers\CareersDirector.Needs.cs"), store = ModSource(@"Careers\Work\WorkStore.cs"), model = ModSource(@"Careers\Work\WorkModel.cs");

        // The morning: one rule, with the option as its switch, and the old inline rule gone.
        string newDay = MethodText(health, "public static void NewDay(int passed)");
        Check(newDay.Contains("FitnessRules.Morning(HomeState.Fitness, day, HomeState.LastWorkoutDay, passed, Easy, Settings.HomeDiet,") && !newDay.Contains("day - HomeState.LastWorkoutDay >= 3")
              && newDay.IndexOf("if (Settings.HomeFitness)", StringComparison.Ordinal) < newDay.IndexOf("FitnessRules.Morning(", StringComparison.Ordinal),
            "fitness sources: the morning's move is the one rule, inside the fitness option, with the diet option as its switch");
        Check(newDay.Contains("HomeState.StartedDay();") && newDay.Contains("FirstNotice();"), "fitness sources: the morning writes the day's first figure and may give the first notice");
        string first = MethodText(health, "private static void FirstNotice()");
        Check(first.Contains("if (!Settings.HomeFitness || HomeState.FitTold)") && first.Contains("HomeState.FitTold = true;") && first.Contains("FitnessRules.Noticeable("),
            "fitness sources: the first notice is given once, only while fitness is kept");
        string trained = MethodText(health, "private static void Trained(float minutes, float perHour, bool builds, bool run)");
        Check(trained.Contains("HomeState.Today.Exercise +=") && trained.Contains("HomeState.Today.RunGain +="), "fitness sources: exercise writes what it really added into the day's ledger");
        Check(health.Contains("effort *= FitnessRules.EffortShare(HomeState.Fitness);") && Occurs(health, "* FitShare") == 5,
            "fitness sources: a run costs less energy for a fit body, and Home's cold, flu and back chances are scaled by the same share in five places");
        Check(jog.Contains("HomeState.Today.Runs++;"), "fitness sources: a run that moved fitness is counted for the why lines");
        Check(leisure.Contains("Drank?.Invoke(units);") && links.Contains("HomeState.Drank(units);") && links.Contains("Settings.HomeFitness") && wiring.Contains("FitnessLinks.Connect") && wiring.Contains("FitnessLinks.Disconnect"),
            "fitness sources: a drink poured at a bar reaches Home's count through the bridge, wired and unwired with the rest");
        Check(links.Contains("HealthBridges.Fitness = HomeFitness.Fill;") && links.Contains("CareersBridges.Fitness = () => HomeFitness.Level;"), "fitness sources: Health and the careers read fitness only through Home's one reader");
        Check(fitness.Contains("public static int Level =>") && fitness.Contains("public static int DietBand") && fitness.Contains("public static int Band =>") && fitness.Contains("public static void Fill(FitnessView v)")
              && fitness.Contains("Settings.HomeLife && Settings.HomeFitness"), "fitness sources: HomeFitness gives Level, Band, DietBand and Fill, each -1 or empty while home life or fitness is off");
        Check(state.Contains("Put(text, \"fk\", FitKnown);") && state.IndexOf("Put(text, \"tkf\", TakeoutFill);", StringComparison.Ordinal) < state.IndexOf("Put(text, \"fk\", FitKnown);", StringComparison.Ordinal)
              && state.Contains("private const string Version = \"h1\";"), "fitness sources: the new save fields come after the old ones and the version is still h1");

        // The bars: the fourth in the old interface, the seventh need in the kit's.
        Check(bar.Contains("private const int Bars = 4;") && bar.Contains("public static float Fitness = -1f;") && bar.Contains("bool fitOn = Fitness >= 0f;") && bar.Contains("Bar(3, Fitness);")
              && bar.Contains("float bars = _fitOn ? Reach[_count] : Reach[_count - 1];") && bar.Contains("Slots[_count]") && bar.Contains("DisableLayout(copy);"),
            "fitness sources: the old interface's tab draws a fourth bar after the bars that stand, in the place of the home bar when there is no home");
        Check(offers.Contains("HomeNeedsBar.Fitness = HomeFitness.On ? HomeState.Fitness : -1f;") && offers.IndexOf("HomeNeedsBar.Fitness =", StringComparison.Ordinal) > offers.IndexOf("HomeNeedsBar.Hide()", StringComparison.Ordinal),
            "fitness sources: the director sets the fourth bar once a second, after the decision to show the bars, and only while fitness is kept");
        Check(needs.Contains("UiIcon.Home, UiIcon.Heartbeat };") && needs.Contains("default: return m.Fitness;") && hudBar.Contains("(m.Fitness >= 0 ? 1 : 0)") && hudBar.Contains("UiIcon.Heartbeat, m.Fitness"),
            "fitness sources: the kit's bar draws fitness as the seventh need in every size and in the small cells");
        Check(hud.Contains("m.Fitness = HomeFitness.On ? Whole(HomeState.Fitness) : -1;") && hud.Contains("UiText.T(\"reallife_home_strip_fitness\")") && hud.Contains("h = h * 31 + Whole(HomeState.Fitness);"),
            "fitness sources: the HUD fills fitness only while it is kept, names it, and redraws when it moves");
        Check(facts.Contains("FitFacts(f.Fit);") && facts.Contains("HealthBridges.Fitness(view);") && facts.Contains("view.Known = false;"), "fitness sources: Health fills the page's fitness through the bridge, and leaves it out on a fault");
        Check(app.Contains("FitnessRows(f.Fit);") && app.Contains("FitnessNotes(box, f.Fit);") && pages.Contains("Fitness(left, f.Fit, w);"), "fitness sources: both interfaces' You pages show the fitness block");

        // The jobs.
        Check(duty.Contains("return FitnessWhy(p);") && duty.IndexOf("PoliceRules.EntryWhy(", StringComparison.Ordinal) < duty.IndexOf("return FitnessWhy(p);", StringComparison.Ordinal),
            "fitness sources: an application is judged by the police's own asks first and then by fitness, in the one place every application path asks");
        Check(Occurs(applying, "DutyEntryWhy(") == 2 && Occurs(needsText, "DutyEntryWhy(p)") == 1, "fitness sources: the list of posts open to the player, an offer's acceptance and the reason the Career app shows all ask the same place");
        string why = MethodText(director, "private string FitnessWhy(Profession p)");
        Check(why.Contains("if (p == null || !Settings.CareersFitness)") && why.Contains("FitnessJobRules.Short(p.Id, have, true, out int need)") && why.Contains("CareersBridges.FitnessNow()")
              && why.Contains("\"reallife_fitness_why_police\"") && why.Contains("\"need\", need.ToString(FitInv), \"have\", have.ToString(FitInv)"),
            "fitness sources: the refusal comes only with the option on, from the bridge's figure, and carries the number needed and the player's");
        string day = MethodText(director, "private void FitnessDay(Job j, int day)");
        Check(day.Contains("!Settings.CareersFitness") && day.Contains("j.Warnings++;") && !day.Contains("EndJob(") && !day.Contains("Dismissed") && Occurs(day, "j.Warnings") >= 3,
            "fitness sources: the warning on the job only counts a warning, and nothing in it ends a job");
        Check(work.Contains("FitnessDay(j, day);") && work.IndexOf("FitnessDay(j, day);", StringComparison.Ordinal) < work.IndexOf("DutyConduct(j, day);", StringComparison.Ordinal),
            "fitness sources: the day's look is made at the working day's turn, with the job's other looks");
        Check(model.Contains("public int FitWarnDay;") && store.Contains(".Add(j.ShutDay).Add(j.FitWarnDay).Add(j.FitStrikeDay)") && store.Contains("j.ShutDay = x.Int(-1);") && store.IndexOf("j.ShutDay = x.Int(-1);", StringComparison.Ordinal) < store.IndexOf("j.FitWarnDay = Math.Max(0, x.Int());", StringComparison.Ordinal),
            "fitness sources: the job remembers its warning in two fields appended at the end of its record, a save without them reading 0");
        Check(work.Contains("public const int WarningsToGo = 3;"), "fitness sources: the careers' warnings are still three, which the warning rule's cap leans on");
        foreach (string file in new[] { "Home\\FitnessRules.cs", "Home\\HomeFitness.cs", "Careers\\Work\\FitnessJobRules.cs", "Careers\\CareersDirector.Fitness.cs", "Wiring\\Wiring.FitnessLinks.cs" })
        {
            string text = ModSource(file);
            Check(text.Length > 0 && !text.Contains("TODO") && !text.Contains("Update()") && !text.Contains("FindObjectsByType") && !text.Contains("UnityEngine.Random") && !text.Contains("appearanceSetter"),
                "fitness sources: " + file + " has no stub, no per-frame code, no scene search, no unseeded number and touches nobody's looks");
        }
        Check(!ModSource(@"Home\FitnessRules.cs").Contains("using UnityEngine") && !ModSource(@"Careers\Work\FitnessJobRules.cs").Contains("using UnityEngine"), "fitness sources: both rule files are game-free");

        // The options.
        Check(settings.Contains("public static bool HomeDiet = true;") && settings.Contains("public static bool CareersFitness = true;") && settings.Contains("HomeDiet = Bool(modId, HomeDietId, HomeDiet);")
              && settings.Contains("CareersFitness = Bool(modId, CareersFitnessId, CareersFitness);"), "fitness options: both new options are on by default and read from the saved settings");
        Check(mod.Contains("AddToggle(Settings.HomeDietId, \"reallife_home_diet\"") && mod.Contains("AddToggle(Settings.CareersFitnessId, \"reallife_work_opt_fitness\"") && mod.Contains("\"reallife_test_fitness_job\""),
            "fitness options: both are on the options page with their labels, and the test button is there");
        Check(tree.Contains(".Opt(\"home_diet\", \"home_fitness\")") && tree.Contains(".Opt(\"careers_fitness\", \"careers_jobs\")") && list.Contains("Settings.CareersFitnessId") && list.Contains("Settings.HomeDietId")
              && list.Contains("\"reallife_test_fitness_job\""), "fitness options: both have a place in the options tree and the feature list, the diet one needing fitness and the job one needing the professional jobs");
        Check(Occurs(settings, "HomeDietId") == 2 && Occurs(settings, "CareersFitnessId") == 2, "fitness options: each id is declared once and read once");
    }

    private static void FitTexts()
    {
        Dictionary<string, string> en = FitEn();
        Check(en != null, "fitness texts: en.json was read");
        if (en == null)
        {
            return;
        }
        string[] keys =
        {
            "reallife_home_diet", "reallife_opt_tip_home_diet", "reallife_work_opt_fitness", "reallife_opt_tip_careers_fitness", "reallife_home_strip_fitness", "reallife_fitness_sec", "reallife_fitness_row",
            "reallife_fitness_value", "reallife_fitness_row_trend", "reallife_fitness_trend_value", "reallife_fitness_trend_none", "reallife_fitness_sec_why", "reallife_fitness_why_none",
            "reallife_fitness_how", "reallife_fitness_how_diet", "reallife_fitness_first_up_1", "reallife_fitness_first_up_2", "reallife_fitness_first_down_1", "reallife_fitness_first_down_2",
            "reallife_fitness_why_police", "reallife_fitness_why_job", "reallife_fitness_note_warning", "reallife_fitness_note_strike", "reallife_fitness_note_clear", "reallife_test_fitness_job",
            "reallife_fitness_test_job", "reallife_fitness_test_no_job"
        };
        var all = new List<string>(keys);
        all.AddRange(FitnessRules.BandKeys);
        all.AddRange(FitnessJobRules.NearTips.Concat(FitnessJobRules.MidTips).Concat(FitnessJobRules.FarTips).Concat(FitnessJobRules.WarnTexts).Concat(FitnessJobRules.StrikeTexts));
        foreach (FitWhy kind in new[] { FitWhy.Runs, FitWhy.Workouts, FitWhy.Idle, FitWhy.Junk, FitWhy.Home, FitWhy.Drinks })
        {
            all.Add(FitnessRules.WhyKey(new WhyLine { Kind = kind, N = 1 }));
            all.Add(FitnessRules.WhyKey(new WhyLine { Kind = kind, N = 2 }));
        }
        bool present = true, style = true;
        foreach (string key in all)
        {
            present &= en.TryGetValue(key, out string t) && t.Length > 0;
            string text = en.TryGetValue(key, out string v) ? v : string.Empty;
            style &= text.IndexOf('!') < 0 && text.IndexOf((char)0x2014) < 0 && text.IndexOf((char)0x2013) < 0 && !text.Contains(" - ") && text.IndexOf(';') < 0 && !text.Contains("  ");
        }
        Check(present, "fitness texts: every key the code names is in en.json");
        Check(style, "fitness texts: no exclamation mark, dash, semicolon or double space in any of them");
        Check(en["reallife_opt_tip_home_diet"].Length <= 190 && en["reallife_opt_tip_home_diet"].Split(' ').Length <= 32 && en["reallife_opt_tip_careers_fitness"].Length <= 190
              && en["reallife_opt_tip_careers_fitness"].Split(' ').Length <= 32, "fitness texts: each option's tip is at most 32 words and 190 characters");
        bool notices = true;
        foreach (string key in new[] { "reallife_fitness_first_up_1", "reallife_fitness_first_up_2", "reallife_fitness_first_down_1", "reallife_fitness_first_down_2" })
        {
            string t = en[key].Replace("{change}", "12").Replace("{days}", "14").Replace("{fitness}", "100");
            notices &= t.Length <= 240 && !t.Contains("{") && en[key].Contains("{change}") && en[key].Contains("{days}") && en[key].Contains("{fitness}");
        }
        Check(notices, "fitness texts: the first notice has four wordings, each within 240 characters and carrying the move, the days and the figure");
        Check(en["reallife_fitness_why_runs"] == "{n} runs this week: Fitness {amount}" && en["reallife_fitness_why_junk"] == "{n} fast food meals: Fitness {amount}",
            "fitness texts: the why lines are the sentences the player gave as examples");
        string guide = en.TryGetValue("reallife_guide_home", out string g) ? g : string.Empty, jobs = en.TryGetValue("reallife_guide_careers_jobs", out string j) ? j : string.Empty;
        Check(guide.Contains("# Fitness and food") && guide.Contains("{fit_idle}") && guide.Contains("{fit_diet_most}") && guide.Contains("{fit_notice}") && guide.Contains("{fit_days}"),
            "fitness texts: the Guide's Home page explains the meter, what food does and the first notice, with its figures from the code");
        Check(jobs.Contains("# Fitness for physical jobs") && jobs.Contains("{fit_police}") && jobs.Contains("{fit_heavy}") && jobs.Contains("{fit_trade}") && jobs.Contains("{fit_far}") && jobs.Contains("{fit_warn_days}")
              && jobs.Contains("Fitness alone never loses you the job"), "fitness texts: the Guide's jobs page says what the police and the trades ask and what the warning is");
        string line = "Fitness is now a fourth bar beside hygiene, stress and home";
        int rows = 0;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Key.EndsWith("_title", StringComparison.Ordinal) && row.Value.Contains(line))
            {
                rows++;
            }
        }
        Check(rows == 1, "fitness texts: What's new has the meter's line in one row (" + rows + ")");
        string docs = File.Exists(ModPath(@"..\..\..\..\docs\features\home.md")) ? File.ReadAllText(ModPath(@"..\..\..\..\docs\features\home.md")) : string.Empty;
        string police = File.Exists(ModPath(@"..\..\..\..\docs\features\careers-police.md")) ? File.ReadAllText(ModPath(@"..\..\..\..\docs\features\careers-police.md")) : string.Empty;
        Check(docs.Contains("FitnessRules.Morning") && docs.Contains("HomeFitness") && docs.Contains("Diet moves fitness") && police.Contains("fitness test") && police.Contains("FitnessJobRules"),
            "fitness texts: the docs of Home and the police career describe the meter, the diet's part and the test");
    }
}
