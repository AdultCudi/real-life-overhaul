using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// A player found the ride-hail passengers' penalties harsh and asked for an easier version that keeps today's for those who
// want it. "Ride fares: easier passengers" (job_taxi_easy, off by default) picks TaxiComfortRules.Easier over Normal once a
// fare: fewer and smaller deductions for hard stops, swerves, bumps and speeding, the same for crashes, police stops and the
// slip. First the tables (Normal exactly the figures the gig had), then the arithmetic, then a rough ride under each, then
// the gig's code reading only the table, then the option and the texts.
internal static partial class Program
{
    private static void TaxiComfort()
    {
        TaxiComfortTables();
        TaxiComfortSums();
        TaxiComfortRide();
        TaxiComfortSources();
        TaxiComfortWords();
    }

    private static void TaxiComfortTables()
    {
        TaxiComfortTable n = TaxiComfortRules.Normal, e = TaxiComfortRules.Easier;
        Check(n.JoltCost == 8f && n.JoltGap == 3f && n.HarshScale == 1f && n.BumpOver == 5.5f && n.SpeedingAbove == 26f && n.SpeedingCost == 4f
              && n.CrashHard == 40f && n.CrashSoft == 22f && n.PoliceCost == 20f && n.LowComfort == 30 && n.LowCut == 0.3f,
            "taxi comfort: the usual passengers are exactly as the gig always judged a ride");
        Check(e.JoltCost < n.JoltCost && e.JoltGap > n.JoltGap && e.HarshScale > n.HarshScale && e.BumpOver > n.BumpOver
              && e.SpeedingAbove >= n.SpeedingAbove && e.SpeedingCost < n.SpeedingCost,
            "taxi comfort: easier passengers mind hard stops, bumps and speeding later, less and less often");
        Check(e.CrashHard == n.CrashHard && e.CrashSoft == n.CrashSoft && e.PoliceCost == n.PoliceCost && e.LowComfort == n.LowComfort && e.LowCut == n.LowCut,
            "taxi comfort: crashes, police stops and the slip's cut for a bad ride are the same for easier passengers");
        Check(e.JoltCost > 0f && e.SpeedingCost > 0f && e.HarshScale <= 1.5f,
            "taxi comfort: easier passengers still mind: a jolt and speeding still cost something, and braking counts a little later, not never");
        Check(TaxiComfortRules.For(false) == n && TaxiComfortRules.For(true) == e, "taxi comfort: the option off is the usual table, on the easier one");
    }

    private static void TaxiComfortSums()
    {
        const float Most = 9.5f;   // TaxiJob.HarshG: an emergency stop
        bool normal = true;
        foreach (float car in new[] { 3.6f, 4.5f, 6.3f, 9.5f })
        {
            normal &= TaxiComfortRules.Harsh(car, TaxiComfortRules.Normal, Most) == car;
        }
        Check(normal, "taxi comfort: the usual passengers' hard stop is the car's own");
        TaxiComfortTable e = TaxiComfortRules.Easier;
        Check(Math.Abs(TaxiComfortRules.Harsh(6f, e, Most) - 6f * e.HarshScale) < 0.001f && TaxiComfortRules.Harsh(9f, e, Most) == Most
              && TaxiComfortRules.Harsh(10f, e, Most) == 10f && TaxiComfortRules.Harsh(4.5f, e, Most) > 4.5f,
            "taxi comfort: easier passengers mind braking later, but never past an emergency stop unless the car's own already is");
        Check(TaxiComfortRules.Crash(9f, e) == e.CrashHard && TaxiComfortRules.Crash(TaxiComfortRules.CrashFast, e) == e.CrashSoft && TaxiComfortRules.Crash(2f, e) == e.CrashSoft,
            "taxi comfort: a crash above the hard crash's speed costs the most, any other the less");
        Check(TaxiComfortRules.LowShare(29, e) == 0.3f && TaxiComfortRules.LowShare(30, e) == 0f && TaxiComfortRules.LowShare(0, TaxiComfortRules.Normal) == 0.3f
              && TaxiComfortRules.LowShare(100, TaxiComfortRules.Normal) == 0f,
            "taxi comfort: only a ride that ended under the line loses a share of the fare, the same for both");
        string line = TaxiComfortRules.Describe(e);
        Check(line.Contains("jolt 4 comfort") && line.Contains("every 6 s") && line.Contains("x1.35") && line.Contains("1.5 a second") && line.Contains("30%"),
            "taxi comfort: the table's line for the log names each figure (" + line + ")");
    }

    // A rough ride of a minute: a hard stop every 2 seconds and 20 seconds over the speed. The usual passengers end it angry,
    // easier ones well above them, still minding.
    private static void TaxiComfortRide()
    {
        float usual = TaxiComfortMinute(TaxiComfortRules.Normal), easier = TaxiComfortMinute(TaxiComfortRules.Easier);
        Check(usual < 30f && easier >= 30f && easier < 100f,
            "taxi comfort: a minute of hard stops and speeding leaves the usual passenger at " + usual.ToString("F0") + " and an easier one at " + easier.ToString("F0"));
        Check(TaxiComfortMinute(TaxiComfortRules.Easier) == easier, "taxi comfort: the same ride gives the same comfort");
    }

    private static float TaxiComfortMinute(TaxiComfortTable t)
    {
        float comfort = 100f, last = -100f;
        const float Dt = 0.2f;   // the jobs' tick
        for (int tick = 0; tick < 300; tick++)
        {
            float now = tick * Dt;
            bool stop = tick % 10 == 0;
            if (stop && now - last > t.JoltGap)
            {
                comfort = Math.Max(0f, comfort - t.JoltCost);
                last = now;
            }
            float speed = tick < 100 ? 28f : 15f;
            if (speed > t.SpeedingAbove)
            {
                comfort -= t.SpeedingCost * Dt;
            }
        }
        return Math.Max(0f, comfort);
    }

    private static void TaxiComfortSources()
    {
        string job = ModSource(@"Jobs\Kinds\TaxiJob.cs");
        string car = ModSource(@"Jobs\Kinds\TaxiJob.Car.cs");
        string riding = MemberText(job, "private void Riding(float now, float dt)");
        string start = MemberText(job, "private void StartRide(float now)");
        Check(riding.Contains("now - _lastJolt > _table.JoltGap") && riding.Contains("Jolt(_table.JoltCost, Braking, now)")
              && riding.Contains("Ctx.Speed > _table.SpeedingAbove") && riding.Contains("_table.SpeedingCost * dt"),
            "taxi comfort: a ride's jolts and speeding are judged by the fare's table");
        Check(MemberText(job, "public override void OnCrash(float speed)").Contains("TaxiComfortRules.Crash(speed, _table)")
              && MemberText(job, "public override void OnPoliceStop()").Contains("_table.PoliceCost")
              && MemberText(job, "private void Settle(float now)").Contains("TaxiComfortRules.LowShare(comfort, _table)"),
            "taxi comfort: crashes, police stops and the slip read the table too");
        int pick = start.IndexOf("_table = TaxiComfortRules.For(Settings.JobTaxiEasy);", StringComparison.Ordinal);
        Check(pick > 0 && start.IndexOf("Tune(_car);", StringComparison.Ordinal) > pick
              && MemberText(car, "private void Tune(CarController car)").Contains("_harsh = TaxiComfortRules.Harsh(_harsh, _table, HarshG);")
              && MemberText(car, "private void Tune(CarController car)").Contains("_bump = _harsh + _table.BumpOver;"),
            "taxi comfort: the table is picked once as a ride begins, before the car's hard stop is set by it");
        bool bare = job.Contains("Jolt(8f") || job.Contains("4f * dt") || job.Contains("> 3f)") || job.Contains("comfort < 30 ?") || job.Contains("40f : 22f")
                    || job.Contains("Jolt(20f") || job.Contains("SpeedingAbove = ") || car.Contains("+ BumpOver");
        Check(!bare, "taxi comfort: no figure of the old rule is left in the gig's own code");
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string list = ModSource(@"Meta\FeatureList.cs");
        string tests = ModSource(@"Jobs\JobDirector.Tests.cs");
        Check(settings.Contains("JobTaxiEasyId = \"job_taxi_easy\"") && settings.Contains("public static bool JobTaxiEasy = false;")
              && settings.Contains("JobTaxiEasy = Bool(modId, JobTaxiEasyId, JobTaxiEasy);")
              && mod.Contains("AddToggle(Settings.JobTaxiEasyId, \"reallife_jobs_taxi_easy\", Settings.JobTaxiEasy,")
              && mod.Contains("AddButton(\"reallife_test_job_taxi_table\"") && tree.Contains("Opts(\"job_pay\", \"job_taxi_easy\")")
              && list.Contains(".Owns(Settings.JobsId, Settings.JobTaxiEasyId)") && tests.Contains("public void TestTaxiTable()"),
            "taxi comfort: the option is saved, off by default, on the jobs' section of the page with a test that prints the table");
    }

    private static void TaxiComfortWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("taxi comfort", langs, codes))
        {
            return;
        }
        string lacking = string.Empty;
        foreach (string key in new[] { "reallife_jobs_taxi_easy", "reallife_opt_tip_job_taxi_easy", "reallife_test_job_taxi_table",
                                       "reallife_job_taxi_table_easier", "reallife_job_taxi_table_usual" })
        {
            string l = LanguagesLacking(langs, codes, key);
            lacking += l.Length > 0 ? " " + key + " (" + l + ")" : string.Empty;
        }
        Check(lacking.Length == 0, "taxi comfort: every text is in every language with the English names in curly brackets:" + lacking);
        bool notes = true;
        foreach (string key in new[] { "reallife_job_taxi_table_easier", "reallife_job_taxi_table_usual" })
        {
            notes &= langs[0].TryGetValue(key, out string t) && t.Length <= 240 && t.Contains("{jolt}") && t.Contains("{gap}") && t.Contains("{speed}");
        }
        Check(notes, "taxi comfort: the test's notices name the jolt, its gap and speeding, at most 240 characters");
        Check(langs[0].TryGetValue("reallife_jobs_taxi_easy", out string label) && label == "Ride fares: easier passengers"
              && langs[0].TryGetValue("reallife_opt_tip_job_taxi_easy", out string tip) && tip.Contains("half") && tip.Contains("6 seconds") && tip.Contains("hard stops, bumps and speeding")
              && tip.Contains("Crashes, police stops, tips and the fare are as before"),
            "taxi comfort: the option is named as asked and its sentence says what it changes and what it keeps");
        Check(LineEverywhere(new[]
              {
                  "Ride fares: easier passengers", "Corridas: passageiros mais tranquilos", "网约车：更宽容的乘客", "Fahrten: entspanntere Fahrgäste",
                  "Courses : passagers plus indulgents", "Carreras: pasajeros más tolerantes", "Поездки: терпеливые пассажиры",
                  "Ritten: geduldigere passagiers", "Yolculuklar: daha hoşgörülü yolcular"
              }),
            "taxi comfort: What's new has the option's line in some row of every language");
    }
}
