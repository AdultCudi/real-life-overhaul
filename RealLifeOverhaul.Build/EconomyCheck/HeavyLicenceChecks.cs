using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul;

// The heavy goods licence (Scripts/Licences/HeavyRules.cs, a player's suggestion): which trucks need it, the school's steps
// and their waits, its part of the licence line in the save, the heavy loads it opens on the courier round, and over the
// sources the gate, the enforcement, the school's truck, the hired truck kept out of saves, and the texts.
internal static partial class Program
{
    private static string ScriptSource(string file)
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string path = System.IO.Path.Combine(folder, file);
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }

    private static void HeavyLicence()
    {
        HeavyClasses();
        HeavySteps();
        HeavyRecord();
        HeavyLoads();
        HeavySources();
        HeavyTexts();
    }

    private static void HeavyClasses()
    {
        Check(HeavyRules.IsHeavy("ba:vehicletype_freighttruckt1", true, 50) && HeavyRules.IsHeavy("freighttruckt1", false, 0),
            "heavy licence: the game's Freight Truck needs it, by name whatever its figures");
        Check(!HeavyRules.IsHeavy("ba:vehicletype_deliverytruck", true, 55) && !HeavyRules.IsHeavy("ba:vehicletype_deliverytruck", true, 40),
            "heavy licence: the Vord Courier box truck stays on the commercial licence");
        Check(!HeavyRules.IsHeavy("ba:vehicletype_mersaididash", false, 65) && !HeavyRules.IsHeavy("ba:vehicletype_honzamimic", false, 60) && !HeavyRules.IsHeavy(null, false, 0),
            "heavy licence: vans, cars and an unknown name are not heavy");
        Check(HeavyRules.IsHeavy("othermod:vehicletype_bigrig", true, 50) && !HeavyRules.IsHeavy("othermod:vehicletype_bigrig", true, 51)
              && !HeavyRules.IsHeavy("othermod:vehicletype_bigrig", true, 0) && !HeavyRules.IsHeavy("othermod:vehicletype_suv", false, 45),
            "heavy licence: another mod's truck in the game's heaviest speed tier is heavy, a faster one or a non-truck is not");
    }

    private static void HeavySteps()
    {
        const int day = 40;
        var none = new HeavyRules.Record();
        var theory = new HeavyRules.Record { TheoryDay = day - 3 };
        var lesson = new HeavyRules.Record { TheoryDay = day - 3, Lessons = 1 };
        Check(HeavyRules.Refusal(HeavyRules.Step.Theory, false, false, false, none, day) == "reallife_school_no_need_commercial"
              && HeavyRules.Refusal(HeavyRules.Step.Lesson, false, false, false, theory, day) == "reallife_school_no_need_commercial"
              && HeavyRules.Refusal(HeavyRules.Step.Test, false, false, false, lesson, day) == "reallife_school_no_need_commercial",
            "heavy licence: every step needs a valid commercial licence first");
        Check(HeavyRules.Refusal(HeavyRules.Step.Theory, true, false, false, none, day) == null, "heavy licence: the paper is open to a commercial driver");
        Check(HeavyRules.Refusal(HeavyRules.Step.Theory, true, false, false, theory, day) == "reallife_school_no_theory_passed",
            "heavy licence: a paper still in date is not sat again");
        Check(HeavyRules.Refusal(HeavyRules.Step.Theory, true, false, false, new HeavyRules.Record { PaperRetryDay = day + 1 }, day) == "reallife_school_no_wait"
              && HeavyRules.Refusal(HeavyRules.Step.Theory, true, false, false, new HeavyRules.Record { PaperRetryDay = day }, day) == null,
            "heavy licence: a failed paper waits for its retry day, and not a day more");
        Check(HeavyRules.Refusal(HeavyRules.Step.Theory, true, true, false, none, day) == "reallife_school_no_have"
              && HeavyRules.Refusal(HeavyRules.Step.Test, true, true, false, lesson, day) == "reallife_school_no_have",
            "heavy licence: nothing to sit while the licence is held or still renewable");
        Check(HeavyRules.Refusal(HeavyRules.Step.Lesson, true, false, false, none, day) == "reallife_school_no_need_heavy_theory"
              && HeavyRules.Refusal(HeavyRules.Step.Lesson, true, false, false, theory, day) == null
              && HeavyRules.Refusal(HeavyRules.Step.Lesson, true, true, true, none, day) == null,
            "heavy licence: a lesson after the paper, or at any time once licensed");
        Check(HeavyRules.Refusal(HeavyRules.Step.Test, true, false, false, theory, day) == "reallife_school_no_need_lesson",
            "heavy licence: the driven test needs a lesson");
        Check(HeavyRules.Refusal(HeavyRules.Step.Test, true, false, false, new HeavyRules.Record { TheoryDay = day - HeavyRules.TheoryValidDays - 1, Lessons = 1 }, day)
              == "reallife_school_no_need_heavy_theory",
            "heavy licence: a paper out of date must be sat again before the drive");
        Check(HeavyRules.WaitDays == 2
              && HeavyRules.Refusal(HeavyRules.Step.Test, true, false, false, new HeavyRules.Record { TheoryDay = day - 1, Lessons = 1 }, day) == "reallife_school_no_heavy_wait"
              && HeavyRules.Refusal(HeavyRules.Step.Test, true, false, false, new HeavyRules.Record { TheoryDay = day - 2, Lessons = 1 }, day) == null,
            "heavy licence: the drive can be booked two days after the paper's pass, not before");
        var failed = new HeavyRules.Record { TheoryDay = day - 5, Lessons = 1 };
        failed.Failed(day);
        Check(failed.RetestDue && failed.RetestDay == day + HeavyRules.WaitDays
              && HeavyRules.Refusal(HeavyRules.Step.Test, true, false, false, failed, day + 1) == "reallife_school_no_heavy_retest"
              && HeavyRules.Refusal(HeavyRules.Step.Test, true, false, false, failed, day + 2) == null,
            "heavy licence: a failed drive waits two days, then can be sat again");
        Check(HeavyRules.Fee(HeavyRules.Step.Test, failed) == HeavyRules.RetestFee && HeavyRules.Fee(HeavyRules.Step.Test, lesson) == HeavyRules.TestFee
              && HeavyRules.Fee(HeavyRules.Step.Theory, none) == HeavyRules.TheoryFee && HeavyRules.Fee(HeavyRules.Step.Lesson, none) == HeavyRules.LessonFee,
            "heavy licence: each step's fee, the retest's after a failed drive");
        failed.Passed();
        Check(!failed.RetestDue && failed.RetestDay == 0, "heavy licence: a pass clears the retest");
        Check(HeavyRules.TestFee > Prices.At(380) && HeavyRules.TheoryFee > Prices.At(60) && HeavyRules.RetestFee < HeavyRules.TestFee,
            "heavy licence: dearer than the commercial test and the car's paper, a retest less than the test");
        var paper = new HeavyRules.Record();
        paper.PaperMarked(false, day);
        Check(paper.TheoryDay == 0 && paper.PaperRetryDay == day + 1, "heavy licence: a failed paper may be sat again the next day");
        paper.PaperMarked(true, day + 1);
        Check(paper.TheoryDay == day + 1 && paper.PaperRetryDay == 0, "heavy licence: a passed paper counts from its day");
    }

    private static void HeavyRecord()
    {
        var record = new HeavyRules.Record { TheoryDay = 12, PaperRetryDay = 3, Lessons = 2, RetestDay = 19, RetestDue = true, Decided = true };
        var text = new StringBuilder();
        HeavyRules.Write(text, record);
        string line = text.ToString();
        Check(line == ";hgv=12,3,2,19,1,1", "heavy licence: the record's part of the save line (" + line + ")");
        HeavyRules.Record back = HeavyRules.Read(line.Substring(5).Split(','));
        Check(back.TheoryDay == 12 && back.PaperRetryDay == 3 && back.Lessons == 2 && back.RetestDay == 19 && back.RetestDue && back.Decided,
            "heavy licence: the record reads back as written");
        HeavyRules.Record shorter = HeavyRules.Read(new[] { "7", "x" });
        Check(shorter.TheoryDay == 7 && shorter.PaperRetryDay == 0 && shorter.Lessons == 0 && !shorter.RetestDue && !shorter.Decided,
            "heavy licence: a short or broken record reads as zeros where it is missing");
        HeavyRules.Record nothing = HeavyRules.Read(null);
        Check(nothing.TheoryDay == 0 && !nothing.Decided, "heavy licence: a save without the part starts the class from nothing");

        // The licence line: the two new parts after the points, so an older RLO reads everything before them and skips them.
        string state = ScriptSource(@"Licences\LicenceState.cs");
        string save = CompanyMethod(state, "public static void Save(Dictionary<string, string> data)");
        Check(InOrder(save, "Pair(text, \"tests\"", "text.Append(\";pts=\");", "Pair(text, \"heavy\", Cards[4].Issued, Cards[4].Expires);", "HeavyRules.Write(text, Heavy);", "data[Key] = text.ToString();"),
            "heavy licence: the heavy card and record are written after everything an older RLO reads");
        string load = CompanyMethod(state, "public static void Load(Dictionary<string, string> data)");
        Check(load.Contains("case \"heavy\": Cards[4]") && load.Contains("case \"hgv\": Heavy = HeavyRules.Read(v);") && state.Contains("new Card[5]")
              && CompanyMethod(state, "public static void Reset()").Contains("Heavy = default;"),
            "heavy licence: the card and record are read back, and cleared between saves");
        Check(state.Contains("Heavy = 4") && state.Contains("Motorcycle = 3,"), "heavy licence: the new class keeps every saved class's number");
    }

    private static void HeavyLoads()
    {
        Check(HeavyRules.IsLoad(true, true) && !HeavyRules.IsLoad(false, true) && !HeavyRules.IsLoad(true, false),
            "heavy licence: a heavy load is a heavy truck and a driver the rules let drive it");
        Check(HeavyRules.LoadPayFor(50) == 90 && HeavyRules.LoadPayFor(0) == 0 && HeavyRules.LoadPayFor(-4) == 0 && HeavyRules.LoadPayFor(1) == 2,
            "heavy licence: a heavy load's drop pays 1.8 times a parcel's");
        Check(HeavyRules.LoadPay > 1.5f && HeavyRules.LoadPay < 2.5f && HeavyRules.LoadTime > 1f && HeavyRules.LoadTime < 2f,
            "heavy licence: better paid, a little more time, never a different job");
        // A round of five at the table's figures: what it pays over a parcel round has to pay for the truck in about two rounds.
        int extra = (HeavyRules.LoadPayFor(50) - 50) * 5;
        Check(extra * 2 >= HeavyRules.TruckHireFee - 125 && extra < HeavyRules.TruckHireFee * 2,
            "heavy licence: two heavy rounds earn back the truck's hire over the van's, and the hire still counts (" + extra + " a round)");
        Check(HeavyRules.TruckFeeDue(0, 260) == 260 && HeavyRules.TruckFeeDue(125, 260) == 135 && HeavyRules.TruckFeeDue(260, 260) == 0 && HeavyRules.TruckFeeDue(400, 260) == 0
              && HeavyRules.TruckFeeDue(-5, 260) == 260,
            "heavy licence: a van's fee paid the same day counts towards the truck's, never below nothing");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int sum = 0;
        for (int i = 0; i < 20000; i++)
        {
            sum += HeavyRules.IsHeavy(i % 2 == 0 ? "ba:vehicletype_freighttruckt1" : "ba:vehicletype_deliverytruck", true, 50) ? 1 : 0;
        }
        Check(sum == 10000 && watch.Elapsed.TotalMilliseconds < 50, "heavy licence: 20,000 vehicle questions well under 50 ms");
    }

    private static void HeavySources()
    {
        string licences = ScriptSource(@"Licences\Licences.cs");
        string classOf = CompanyMethod(licences, "public static bool ClassOf(VehicleController vehicle, out LicenceClass kind)");
        Check(InOrder(classOf, "kind = car == CarKind.Van || car == CarKind.Truck ? LicenceClass.Commercial : LicenceClass.Car;",
                "if (car == CarKind.Truck && HeavyClass && HeavyRules.IsHeavy(type.vehicleTypeName, true, type.maxSpeed))", "kind = LicenceClass.Heavy;"),
            "heavy licence: the gate asks for the heavy class only while its option is on, else the commercial one");
        Check(CompanyMethod(licences, "public static bool Has(LicenceClass kind)").Contains("!Required || !LicenceState.Loaded || LicenceState.Covered(kind"),
            "heavy licence: with \"Driving needs a licence\" off everyone may drive anything, as before");

        string director = ScriptSource(@"Licences\LicenceDirector.cs");
        Check(director.Contains("!LicenceState.Covered(needs, day)") && director.Contains("LicenceStanding standing = LicenceState.Entitled(needs, day);"),
            "heavy licence: getting into a heavy truck without it gives the same warning as the other classes");
        Check(InOrder(director, "if (!LicenceState.Heavy.Decided && Settings.HeavyLicence)", "GrandfatherHeavy();")
              && InOrder(CompanyMethod(director, "private void GrandfatherHeavy()"), "LicenceState.Heavy.Decided = true;", "Dealerships.Stock(instance)", "HeavyRules.IsHeavy(", "LicenceState.Grant(LicenceClass.Heavy, day);"),
            "heavy licence: a save already owning a heavy truck is given the class once, never for a dealer's stock or a job van");
        string enforcement = ScriptSource(@"Licences\Driving\DrivingEnforcement.cs");
        Check(enforcement.Contains("LicenceStanding standing = LicenceState.Entitled(kind, day);") && enforcement.Contains("(kind != LicenceClass.Heavy && LicenceState.Valid(LicenceClass.Heavy, day))"),
            "heavy licence: a stop in a heavy truck finds no licence, the wrong class or a valid one, as for any class; a heavy licence covers a van");

        string car = ScriptSource(@"Licences\Driving\TestCar.cs");
        Check(InOrder(car, "string type = truck ? TruckType : van ? VanType : CarType;") && InOrder(car, "refusal = NoTruckKey;", "return null;", "refusal = FindSpot(near, truck,"),
            "heavy licence: the school lends the Freight Truck for the heavy class, and a missing truck refuses only that class");
        string test = ScriptSource(@"Licences\Driving\PracticalTest.cs");
        Check(test.Contains("kind == LicenceClass.Commercial || kind == LicenceClass.Heavy") && ScriptSource(@"Licences\Driving\PracticalTest.Marking.cs").Contains("if (Reverses(_class) && _reversed < 2.5f"),
            "heavy licence: the truck's bay is reversed into, as the van's");
        string result = ScriptSource(@"Licences\Driving\PracticalTest.Result.cs");
        Check(result.Contains("LicenceState.DriveMarked(_class, _passed, day);") && result.Contains("LicenceState.LessonDone(_class);"),
            "heavy licence: the drive's result goes on the record of the class driven");

        string rental = ScriptSource(@"Jobs\JobRental.cs");
        Check(InOrder(CompanyMethod(rental, "public void Save(Dictionary<string, string> data)"), "BeforeSerialise();", "data[Key] = ")
              && InOrder(CompanyMethod(rental, "private void BeforeSerialise()"), "all.Remove(_truck)", "PutBack(_truck)"),
            "heavy licence: a hired truck comes off the save's vehicle list for every save, so a save loaded without the mod has none");
        Check(InOrder(CompanyMethod(rental, "private void TakeBack()"), "if (car != null && truck && car.vehicleInstance != null)", "car.vehicleInstance.Delete(car);"),
            "heavy licence: the hired truck goes back by the game's own delete");
        string courier = ScriptSource(@"Jobs\Kinds\CourierJob.cs");
        string owed = CompanyMethod(courier, "private bool Owed(bool beatTheClock, out PaySlip slip)");
        Check(owed.Contains("int each = DropPay();") && owed.Contains("Distance = RoundPay(") && !owed.Contains("UnitPay()"),
            "heavy licence: the slip pays a heavy load's rate for every drop and the distance");
        Check(InOrder(courier, "_heavy = HeavyRules.IsLoad(JobDirector.IsHeavyTruck(wheels), Ctx.Test || Licences.Driving.Has(LicenceClass.Heavy));", "_window *= HeavyRules.LoadTime;"),
            "heavy licence: a round in a heavy truck with the licence is a heavy load, with the truck's time");
        Check(ScriptSource(@"Jobs\JobPhone.cs").Contains("if (def.Id == JobCatalogue.Courier && JobDirector.HeavyOffered)")
              && ScriptSource(@"Jobs\JobDirector.cs").Contains("public static bool HeavyOffered => Settings.HeavyLicence && Licences.Driving.Has(LicenceClass.Heavy);"),
            "heavy licence: the Gig Line offers heavy loads only with the option on and the licence");
        Check(!(rental + ScriptSource(@"Licences\HeavyRules.cs")).Contains("Random.") && !CompanyMethod(courier, "protected override bool OnBegin(float now)").Contains("Random."),
            "heavy licence: nothing of it is drawn: the same on every machine");
        string mod = ScriptSource(@"Core\RealLifeOverhaulMod.cs");
        Check(mod.Contains("\"reallife_test_lic_theory_heavy\"") && mod.Contains("TestPractical(LicenceClass.Heavy)") && mod.Contains("\"reallife_test_job_heavy_load\"")
              && mod.Contains("Settings.HeavyLicenceId, \"reallife_lic_opt_heavy\""),
            "heavy licence: its option and its test buttons on the options page");
    }

    private static void HeavyTexts()
    {
        string[] keys =
        {
            "reallife_lic_class_heavy", "reallife_lic_opt_heavy", "reallife_opt_tip_heavy_licence", "reallife_test_lic_heavy", "reallife_test_lic_theory_heavy",
            "reallife_test_lic_practical_heavy", "reallife_test_job_heavy_load", "reallife_school_name_theory_heavy", "reallife_school_name_lesson_heavy",
            "reallife_school_name_test_heavy", "reallife_school_pitch_theory_heavy", "reallife_school_pitch_lesson_heavy", "reallife_school_pitch_test_heavy",
            "reallife_school_no_need_commercial", "reallife_school_no_need_heavy_theory", "reallife_school_no_heavy_wait", "reallife_school_no_heavy_retest",
            "reallife_school_go_truck", "reallife_lic_theory_heavy_headline", "reallife_lic_test_meet_truck", "reallife_lic_test_get_in_truck", "reallife_lic_marker_truck",
            "reallife_lic_sheet_fail_heavy", "reallife_lic_test_bye_fail_heavy", "reallife_lic_no_truck", "reallife_lic_grandfather_heavy",
            "reallife_jobline_heavy", "reallife_jobline_heavy_pitch", "reallife_jobline_heavy_own", "reallife_jobline_heavy_hire", "reallife_jobline_heavy_take",
            "reallife_jobline_heavy_go", "reallife_job_heavy_no_licence", "reallife_job_heavy_no_money", "reallife_job_heavy_no_room", "reallife_job_heavy_hired",
            "reallife_job_heavy_start", "reallife_job_rental_swap"
        };
        string[] notices =
        {
            "reallife_lic_grandfather_heavy", "reallife_job_heavy_hired", "reallife_job_heavy_start", "reallife_job_rental_swap", "reallife_lic_no_truck",
            "reallife_school_no_heavy_wait", "reallife_school_no_heavy_retest", "reallife_job_heavy_no_room"
        };
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            bool all = words != null;
            for (int i = 0; all && i < keys.Length; i++)
            {
                all = words.TryGetValue(keys[i], out string text) && text.Length > 0 && !text.Contains('!') && !text.Contains((char)0x2014) && !text.Contains((char)0x2013);
            }
            Check(all, "heavy licence: every text in " + code);
            bool shortNotices = words != null;
            foreach (string key in notices)
            {
                shortNotices &= words != null && words.TryGetValue(key, out string text) && text.Length <= 240;
            }
            Check(shortNotices, "heavy licence: its notifications at most 240 characters in " + code);
            bool questions = words != null && HeavyRules.QuestionKeys.Length == 24;
            foreach (string key in HeavyRules.QuestionKeys)
            {
                questions &= words != null && words.TryGetValue(key, out string text) && text.Split('|').Length == 4 && !text.Contains('!');
            }
            Check(questions, "heavy licence: 24 questions, each with three answers, in " + code);
            Check(words != null && words.TryGetValue("reallife_jobline_heavy_pitch", out string pitch) && pitch.Contains("{pay}") && pitch.Contains("{shift}")
                  && pitch.Contains("{count}") && pitch.Contains("{truck}") && pitch.Contains("{status}")
                  && words.TryGetValue("reallife_jobline_heavy_hire", out string hire) && hire.Contains("{fee}")
                  && words.TryGetValue("reallife_lic_sheet_fail_heavy", out string sheet) && sheet.Contains("{days}") && sheet.Contains("{amount}"),
                "heavy licence: the Gig Line's page and the sheet keep their figures in " + code);
        }
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null && en["reallife_school_no_heavy_wait"].Contains(HeavyRules.WaitDays + " days") && en["reallife_school_no_heavy_retest"].Contains(HeavyRules.WaitDays + " days")
              && en["reallife_school_pitch_test_heavy"].Contains(HeavyRules.WaitDays + " days") && en["reallife_school_no_need_heavy_theory"].Contains(HeavyRules.TheoryValidDays + " days")
              && en["reallife_school_pitch_theory_heavy"].Contains(HeavyRules.TheoryQuestions + " questions") && en["reallife_school_pitch_theory_heavy"].Contains(HeavyRules.TheoryPass + " to pass"),
            "heavy licence: the figures the texts name are the rules' own");
        Check(HeavyRules.QuestionGroups.Length == 4 && HeavyRules.QuestionGroups[0] == 0 && HeavyRules.QuestionGroups[3] < HeavyRules.QuestionKeys.Length
              && HeavyRules.QuestionKeys.Length >= HeavyRules.TheoryQuestions * 2,
            "heavy licence: four subjects, and twice the paper's questions to draw from");
        foreach (string code in new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null && words.TryGetValue("reallife_guide_licences", out string guide) && guide.Contains("{lic_heavy_test_fee}") && guide.Contains("{lic_heavy_wait_days}")
                  && guide.Contains("{lic_heavy_load_pay}") && guide.Contains("Freight Truck"),
                "heavy licence: the Guide's licences page says how to get it, in " + code);
            Check(AnyChangesRow(words, "Freight Truck"), "heavy licence: What's new says so in " + code);
        }
    }
}
