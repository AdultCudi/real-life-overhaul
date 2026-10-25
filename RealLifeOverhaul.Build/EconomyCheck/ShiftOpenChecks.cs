using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// A shift is never set while its workplace is shut (Scripts/Careers: Work/OpenHoursRules, CareersDirector.Hours; Scripts/Jobs:
// CareerDirector, CareerState). A profession's catalogue hour was worked wherever it was: the warehouse's 06:00
// at a wholesaler that opens at 08:00 could not be got into (the game's BuildingHelper.CanEnterBuilding reads its schedule),
// the start window closed as the door opened, and three missed days were the sack. First every profession's fit against a
// spread of the city's opening hours, then the save, then the source that applies it on every path, then the texts.
internal static partial class Program
{
    private static void ShiftOpen()
    {
        ShiftOpenOwnersCase();
        ShiftOpenEveryJob();
        ShiftOpenEdges();
        ShiftOpenSave();
        ShiftOpenStreet();
        ShiftOpenSources();
        ShiftOpenTexts();
    }

    private const int Weekdays = 31, Everyday = 127;

    // Seven days' masks, Monday first: open from `from` to `to` (to 24 for midnight) on the days given, shut on the rest.
    private static int[] Open(int days, int from, int to)
    {
        var week = new int[7];
        for (int d = 0; d < 7; d++)
        {
            for (int h = from; (days >> d & 1) != 0 && h < to; h++)
            {
                week[d] |= 1 << h;
            }
        }
        return week;
    }

    private static int[] Join(int[] a, int[] b)
    {
        var week = new int[7];
        for (int d = 0; d < 7; d++)
        {
            week[d] = a[d] | b[d];
        }
        return week;
    }

    // ---------------------------------------------------------------- the owner's case

    private static void ShiftOpenOwnersCase()
    {
        Profession p = ProCatalogue.Find(ProCatalogue.Warehouse);
        Check(p != null && p.Hour == 6 && p.Days == Weekdays, "shift open: the warehouse job's catalogue week starts at 06:00 on weekdays (the owner's case)");
        int[] wholesale = Open(Weekdays, 8, 20);
        int own = OpenHoursRules.Pack(p.Days, p.Hour, p.Hours);
        Check(OpenHoursRules.AnyShut(own, wholesale) && OpenHoursRules.StartsShut(own, wholesale, 0),
            "shift open: at a place open 08:00 to 20:00 the old 06:00 start is shut, the case that got the player sacked");
        int fit = OpenHoursRules.Fit(p.Days, p.Hour, p.Hours, wholesale);
        Check(fit == OpenHoursRules.Pack(Weekdays, 8, 8) && !OpenHoursRules.AnyShut(fit, wholesale),
            "shift open: the warehouse job there is fitted to 08:00 to 16:00 on its five weekdays, the full eight hours, inside the opening hours");
        var post = new Opening { Prof = ProCatalogue.Warehouse, Shift = fit };
        Profession def = post.Def;
        Check(def.Hour == 8 && def.Hours == 8 && def.Days == Weekdays && def.Id == p.Id && def.Key == p.Key,
            "shift open: the post's profession reads the fitted week, so the start window, the day's end, the reminder and the desk read it");
        Check(ProRules.CanStart(def, 1, 8 * 60, out int minutes, out bool late) && minutes == 480 && !late && !ProRules.CanStart(def, 1, 6 * 60, out _, out _),
            "shift open: the working day is started at the door's opening, on time, and not at the shut 06:00");
        Check(ReferenceEquals(post.Def, def), "shift open: the fitted profession is made once and kept, never per read");
    }

    // ---------------------------------------------------------------- every job against the city's hours

    private static void ShiftOpenEveryJob()
    {
        var patterns = new List<int[]>
        {
            Open(Weekdays, 9, 17), Open(Everyday, 8, 20), Open(Everyday, 10, 22), Open(Everyday, 7, 15), Open(Everyday, 12, 24),
            Join(Open(Everyday, 20, 24), Open(Everyday, 0, 4)), Open(Everyday, 0, 24), Open(Everyday & ~64, 9, 21), Open(Everyday & ~64, 10, 18),
            Join(Open(Weekdays, 8, 12), Open(Weekdays, 13, 17)), Open(32 | 64, 10, 18), Open(Weekdays, 10, 15), Open(4 | 8 | 16 | 32 | 64, 18, 24),
            Open(Weekdays, 9, 13), Open(Everyday, 6, 14)
        };
        int fits = 0, moved = 0, none = 0;
        var wrong = new List<string>();
        foreach (Profession p in ProCatalogue.All)
        {
            int own = OpenHoursRules.Pack(p.Days, p.Hour, p.Hours);
            for (int i = 0; i < patterns.Count; i++)
            {
                int[] week = patterns[i];
                int fit = OpenHoursRules.Fit(p.Days, p.Hour, p.Hours, week);
                fits++;
                if (fit == 0)
                {
                    none++;
                    bool anyRun = false;
                    for (int d = 0; d < 7 && !anyRun; d++)
                    {
                        for (int h = 0; h + OpenHoursRules.LeastHours <= 24 && !anyRun; h++)
                        {
                            anyRun = OpenHoursRules.Covers(week[d], h, OpenHoursRules.LeastHours);
                        }
                    }
                    if (anyRun)
                    {
                        wrong.Add(p.Key + " #" + i + " no week where one fits");
                    }
                    continue;
                }
                int days = OpenHoursRules.DaysOf(fit), hour = OpenHoursRules.HourOf(fit), hours = OpenHoursRules.HoursOf(fit);
                moved += fit != own ? 1 : 0;
                if (OpenHoursRules.AnyShut(fit, week))
                {
                    wrong.Add(p.Key + " #" + i + " starts or runs while shut");
                }
                if (hour + hours > 24 || hours > p.Hours || hours < Math.Min(p.Hours, OpenHoursRules.LeastHours) || OpenHoursRules.Count(days) > OpenHoursRules.Count(p.Days) || days == 0)
                {
                    wrong.Add(p.Key + " #" + i + " a week out of shape");
                }
                if (!OpenHoursRules.AnyShut(own, week) && fit != own)
                {
                    wrong.Add(p.Key + " #" + i + " moved though its own week was open");
                }
                if (OpenHoursRules.Fit(p.Days, p.Hour, p.Hours, week) != fit)
                {
                    wrong.Add(p.Key + " #" + i + " not the same twice");
                }
            }
            Check(OpenHoursRules.Fit(p.Days, p.Hour, p.Hours, new int[7]) == own && OpenHoursRules.Fit(p.Days, p.Hour, p.Hours, null) == own,
                "shift open: " + p.Key + " keeps its own week at a place whose hours the game does not keep (the mod's buildings, no business)");
        }
        Check(wrong.Count == 0 && fits == ProCatalogue.All.Length * patterns.Count && moved > 0,
            "shift open: every one of the " + ProCatalogue.All.Length + " professions against " + patterns.Count + " kinds of opening hours (" + fits + " fits, " + moved + " moved, "
            + none + " with no working day): never starting or running while shut, never longer or on more days than its own, kept where it was open, the same twice (wrong: "
            + string.Join(", ", wrong) + ")");
    }

    // ---------------------------------------------------------------- the edges

    private static void ShiftOpenEdges()
    {
        Profession bar = ProCatalogue.Find(ProCatalogue.BarStaff);
        int bars = OpenHoursRules.Fit(bar.Days, bar.Hour, bar.Hours, Open(Everyday, 8, 20));
        Check(bars == OpenHoursRules.Pack(bar.Days, 12, 8),
            "shift open: bar staff (14:00 for eight hours) at a place shut at 20:00 start at 12:00 on their own days, the nearest start that ends at close");
        int club = OpenHoursRules.Fit(bar.Days, bar.Hour, bar.Hours, Join(Open(Everyday, 20, 24), Open(Everyday, 0, 4)));
        Check(club == OpenHoursRules.Pack(bar.Days, 20, 4), "shift open: at a nightclub open 20:00 to 04:00 the shift is its evening, 20:00 to midnight, never across the day's end");
        Profession wh = ProCatalogue.Find(ProCatalogue.Warehouse);
        int midweek = OpenHoursRules.Fit(wh.Days, wh.Hour, wh.Hours, Open(4 | 8 | 16 | 32 | 64, 6, 18));
        Check(midweek == OpenHoursRules.Pack(4 | 8 | 16, 6, 8), "shift open: a place shut on Monday and Tuesday keeps the job's own hour on its open weekdays, never more days than its own");
        int weekend = OpenHoursRules.Fit(wh.Days, wh.Hour, wh.Hours, Open(32 | 64, 10, 18));
        Check(weekend == OpenHoursRules.Pack(32 | 64, 10, 8), "shift open: a place open only at the weekend has the weekday job's days there, as it has no other");
        int shortDay = OpenHoursRules.Fit(wh.Days, wh.Hour, wh.Hours, Open(Weekdays, 10, 15));
        Check(shortDay == OpenHoursRules.Pack(Weekdays, 10, 5), "shift open: a place open five hours a day has a five-hour day, its whole opening");
        Check(OpenHoursRules.Fit(wh.Days, wh.Hour, wh.Hours, Open(Everyday, 10, 13)) == 0, "shift open: a place open under four hours in a row has no working day: no vacancy is made there");
        Check(OpenHoursRules.Fit(wh.Days, wh.Hour, wh.Hours, Join(Open(Weekdays, 8, 12), Open(Weekdays, 13, 17))) == OpenHoursRules.Pack(Weekdays, 8, 4),
            "shift open: a place closed for an hour at noon has the day before it, never one across the closed hour");
        Check(OpenHoursRules.Covers(0xF00000, 20, 4) && !OpenHoursRules.Covers(0xF00000, 20, 5) && !OpenHoursRules.Covers(-1, 22, 4) && !OpenHoursRules.Covers(-1, -1, 2),
            "shift open: a span is covered only inside the day and only where every hour is open");
        Check(OpenHoursRules.Weekday(1) == 0 && OpenHoursRules.Weekday(7) == 6 && OpenHoursRules.Weekday(8) == 0 && OpenHoursRules.Weekday(61) == 4,
            "shift open: day 1 of a game is a Monday, as the game counts (TimeHelper.GetDayOfWeek)");
        int packed = OpenHoursRules.Pack(Weekdays, 23, 1);
        Check(OpenHoursRules.DaysOf(packed) == Weekdays && OpenHoursRules.HourOf(packed) == 23 && OpenHoursRules.HoursOf(packed) == 1 && packed != 0, "shift open: a week packs and unpacks whole");

        int[] wholesale = Open(Weekdays, 8, 20);
        var timer = Stopwatch.StartNew();
        int sum = 0;
        for (int n = 0; n < 64; n++)
        {
            Profession p = ProCatalogue.All[n % ProCatalogue.All.Length];
            sum += OpenHoursRules.Fit(p.Days, p.Hour, p.Hours, wholesale) & 1;
        }
        double ms = timer.Elapsed.TotalMilliseconds;
        Check(ms < 20 && sum >= 0, "shift open: a morning's fit of 64 posts takes " + ms.ToString("F3") + " ms (under 20 ms, once a game day)");
    }

    // ---------------------------------------------------------------- the save

    private static void ShiftOpenSave()
    {
        int week = OpenHoursRules.Pack(Weekdays, 8, 8);   // 526367: a number found nowhere else in the line
        var f = new StudyFile { Seed = 9 };
        WorkFile w = f.Work;
        w.Openings.Add(new Opening { Id = 1, Prof = ProCatalogue.Warehouse, Employer = "Wholesale", Street = "1st Avenue", Number = 4, Pay = 7000, To = 30, Lead = "lead-7", Shift = week });
        var a = new Application { Day = 3, Stage = (int)ApplyStep.Sent };
        w.Openings[0].CopyTo(a.Post);
        w.Applications.Add(a);
        var j = new Job { Hired = 2, Pay = 7000, ShutDay = 777 };
        w.Openings[0].CopyTo(j.Post);
        w.Job = j;
        string line = CareersStore.Write(f);
        StudyFile back = CareersStore.Read(line, out _);
        WorkFile b = back.Work;
        Check(line.Contains("|lead-7|" + week + "~") && b.Openings.Count == 1 && b.Openings[0].Lead == "lead-7" && b.Openings[0].Shift == week
              && b.Applications.Count == 1 && b.Applications[0].Post.Shift == week
              && b.Job != null && b.Job.Post.Shift == week && b.Job.ShutDay == 777 && b.Job.Def.Hour == 8 && CareersStore.Write(back) == line,
            "shift open: a vacancy's lead and then its week, an application's and the job's week, and the job's shut day, are saved and read back the same");
        string old = line.Replace("|lead-7|" + week + "~", "~").Replace("|" + week + "~", "~").Replace("|" + week + "|777", string.Empty);
        StudyFile before = CareersStore.Read(old, out _);
        Check(old.IndexOf(week.ToString(), StringComparison.Ordinal) < 0 && old.IndexOf("lead-7", StringComparison.Ordinal) < 0 && before.Work.Openings.Count == 1
              && before.Work.Openings[0].Shift == 0 && before.Work.Openings[0].Lead.Length == 0 && before.Work.Openings[0].Pay == 7000 && before.Work.Openings[0].Level == -1
              && before.Work.Applications.Count == 1 && before.Work.Applications[0].Post.Shift == 0 && before.Work.Job != null && before.Work.Job.Post.Shift == 0
              && before.Work.Job.ShutDay == -1 && before.Work.Job.Def.Hour == 6 && before.Work.Job.Pay == 7000,
            "shift open: a save from before both the lead and the week loads, its vacancy with no lead, its posts on the profession's own week (fitted again at load), no shut day");
        string leadOnly = line.Replace("|lead-7|" + week + "~", "|lead-7~");
        StudyFile led = CareersStore.Read(leadOnly, out _);
        Check(led.Work.Openings.Count == 1 && led.Work.Openings[0].Lead == "lead-7" && led.Work.Openings[0].Shift == 0 && led.Work.Applications[0].Post.Shift == week,
            "shift open: a vacancy with its lead and no week reads its lead and is fitted again; the application after it parses whole");
        int at = line.IndexOf("|" + week + "~", StringComparison.Ordinal);
        string bad = at < 0 ? line : line.Substring(0, at) + "|" + OpenHoursRules.Pack(Weekdays, 20, 8) + line.Substring(at + 1 + week.ToString().Length);
        Check(CareersStore.Read(bad, out _).Work.Openings[0].Shift == 0, "shift open: a saved week past midnight is not read: the post is fitted again");
        var copy = new Opening();
        w.Openings[0].CopyTo(copy);
        Check(copy.Shift == week && copy.Lead == "lead-7" && copy.Same(w.Openings[0]), "shift open: a post copied to an application or a job carries its week and its lead");
    }

    // ---------------------------------------------------------------- the street careers

    private static void ShiftOpenStreet()
    {
        Check(OpenHoursRules.RotaShutExcuses(12, 12) && !OpenHoursRules.RotaShutExcuses(12, 13) && !OpenHoursRules.RotaShutExcuses(0, 0),
            "shift open: a rota day the player was turned away from because nobody near was open is excused; another day, or none, is not");
        Check(!OpenHoursRules.RotaLateMarks(true, 12, 12) && OpenHoursRules.RotaLateMarks(true, 11, 12) && !OpenHoursRules.RotaLateMarks(false, 0, 12),
            "shift open: a rota shift begun late after such a turning away is on time; a late start on any other day is a mark");
    }

    // ---------------------------------------------------------------- the source

    private static void ShiftOpenSources()
    {
        string hours = ModSource(@"Careers\CareersDirector.Hours.cs"), work = ModSource(@"Careers\CareersDirector.Work.cs"), vac = ModSource(@"Careers\CareersDirector.Vacancies.cs");
        string game = ModSource(@"Careers\Work\WorkGame.cs"), tests = ModSource(@"Careers\CareersDirector.Tests.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string street = ModSource(@"Jobs\CareerDirector.cs"), state = ModSource(@"Jobs\CareerState.cs"), model = ModSource(@"Careers\Work\WorkModel.cs");
        Check(hours.Length > 0 && work.Length > 0 && vac.Length > 0 && game.Length > 0 && tests.Length > 0 && mod.Length > 0 && street.Length > 0 && state.Length > 0 && model.Length > 0,
            "shift open: the sources are read");
        Check(MethodText(vac, "private Opening NewVacancy(").Contains("return FitShift(o) ? o : null;")
              && MethodText(vac, "private void BoardVacancies(").Contains("if (!FitShift(o2))"),
            "shift open: every vacancy, the city's and the jobs board's, is fitted to its place's hours as it is made, or not made");
        string start = MethodText(work, "private void StartWork()");
        string dayRun = MethodText(work, "private void WorkDayRun(int day)");
        int settled = dayRun.IndexOf("SettleDay(j, d);", StringComparison.Ordinal), keep = dayRun.IndexOf("KeepShifts(day, false);", StringComparison.Ordinal);
        Check(start.Contains("KeepShifts(Save.Day, true);") && settled > 0 && keep > settled,
            "shift open: at load and each morning, after the days gone by are settled on their own week, every post is fitted again");
        string settle = MethodText(work, "private void SettleDay(Job j, int day, int from, int to)");
        int excuse = settle.IndexOf("if (ShutExcused(j, day))", StringComparison.Ordinal), warn = settle.IndexOf("j.Warnings++;", StringComparison.Ordinal);
        Check(excuse > 0 && warn > excuse && settle.Contains("ProRules.Excuse(j);"), "shift open: a day not worked whose place was shut at its start is excused before any warning");
        Check(MethodText(work, "private void WorkHour(int day, int hour)").Contains("ShutAtStart(j, day, hour);"), "shift open: the start hour of each working day looks at the door");
        string keepJob = MethodText(hours, "private void KeepJobShift(Job j, int day, bool load)");
        Check(keepJob.Contains("bool righted = load && shutBefore") && keepJob.Contains("j.Warnings = 0;") && keepJob.Contains("j.Absent = 0;")
              && keepJob.Contains("\"reallife_work_note_hours_righted\"") && keepJob.Contains("\"reallife_work_note_hours_moved\""),
            "shift open: a save's job whose old week began while shut is put right at load, its warnings and missed days taken back, and the player told");
        string fitted = MethodText(hours, "private int Fitted(Profession p, Opening o, int prefer = 0)");
        Check(fitted.Contains("o.Work == (int)WorkMode.Remote") && fitted.Contains("WorkGame.OpenWeek(o.Street, o.Number, _week)") && fitted.Contains("OpenHoursRules.Fit("),
            "shift open: the fit reads the game's schedule of the post's own place, and leaves a post worked only from home on its own week");
        string week = MethodText(game, "public static bool OpenWeek(string street, int number, int[] into)");
        string reg = MethodText(game, "private static BuildingRegistration Registration(string street, int number)");
        Check(week.Contains("TimeHelper.GetDayOfWeek(w + 1)") && week.Contains("s.IsOpenAtHour(h)") && week.Contains("!s.isOpen") && reg.Contains("SpecialBuildings.Filed(street, number)")
              && reg.Contains("BuildingHelper.GetBuildingRegistration("), "shift open: the week is the game's own schedule day by day; a building the mod holds keeps no hours");
        Check(MethodText(game, "public static bool ShutNow(string street, int number)").Contains("BusinessHelper.IsBusinessOpen(reg)"),
            "shift open: shut now is the game's own reading, a temporary closing included");
        Check(model.Contains("o.Shift = Shift;") && model.Contains("_fitted = p.WithShift("), "shift open: a post's week is copied with it and its profession made once a week");
        string clock = MethodText(street, "public bool ClockOn(out string reasonKey)");
        string miss = MethodText(street, "private void Miss(int day, Employer employer)");
        Check(clock.Contains("OpenHoursRules.RotaLateMarks(") && clock.Contains("reasonKey == \"reallife_job_no_client\"") && clock.Contains("CareerState.ShutDay = day;")
              && miss.Contains("OpenHoursRules.RotaShutExcuses(CareerState.ShutDay, day)") && miss.Contains("\"reallife_career_shut_excused\"")
              && miss.IndexOf("RotaShutExcuses", StringComparison.Ordinal) < miss.IndexOf("CareerState.Missed(day)", StringComparison.Ordinal),
            "shift open: a rota shift turned away because no client near was open is not a late mark nor a no-show");
        Check(state.Contains("Join(text, TotalShifts, TotalEarned, CartPaidDay, GigDay, CashierHours, ShutDay);") && state.Contains("ShutDay = shut != null ? shut[5] : 0;"),
            "shift open: the street career's shut day is saved last in its group, and a save without it loads");
        string listTest = MethodText(tests, "public void TestShiftHours()"), caseTest = MethodText(tests, "public void TestForklift()");
        Check(mod.Contains(".AddButton(\"reallife_work_test_shift_hours\", () => Careers.CareersDirector.Instance?.TestShiftHours())")
              && mod.Contains(".AddButton(\"reallife_work_test_forklift\", () => Careers.CareersDirector.Instance?.TestForklift())")
              && listTest.Contains("\"Careers check (shift hours): ") && listTest.Contains("\"reallife_work_test_shift_hours_pass\"") && listTest.Contains("\"reallife_work_test_shift_hours_fail\"")
              && caseTest.Contains("KeepJobShift(j, day, true);") && caseTest.Contains("j.Post.Shift = 0;") && caseTest.Contains("\"Careers check (forklift case): ")
              && caseTest.Contains("\"reallife_work_test_forklift_pass\"") && caseTest.Contains("ShutExcused(old, shutMorning)"),
            "shift open: Options > Tests lists the next working days against their places' hours, and makes the warehouse case again through the load's own pass, each with a PASS or FAIL line");
    }

    // ---------------------------------------------------------------- the texts

    private static void ShiftOpenTexts()
    {
        var keys = new[]
        {
            "reallife_work_note_hours_moved", "reallife_work_note_hours_righted", "reallife_work_note_shut", "reallife_career_shut_excused", "reallife_work_test_shift_hours",
            "reallife_work_test_forklift", "reallife_work_test_shift_hours_pass", "reallife_work_test_shift_hours_fail", "reallife_work_test_shift_hours_none",
            "reallife_work_test_forklift_pass", "reallife_work_test_forklift_fail", "reallife_work_test_forklift_none"
        };
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        var lacking = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            foreach (string key in keys)
            {
                if (own == null || en == null || !own.TryGetValue(key, out string words) || words.Length == 0 || !en.TryGetValue(key, out string english)
                    || !SamePlaceholders(words, english) || (code == "en" && words.Length > 240))
                {
                    lacking.Add(code + " " + key);
                }
            }
        }
        Check(lacking.Count == 0, "shift open: the notices and the tests' buttons are in en, pt and zh-cn with the same placeholders, each notice 240 characters at most (lacking: "
                                  + string.Join(", ", lacking) + ")");
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("shift open", langs, codes))
        {
            return;
        }
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: a job's working hours now always fall inside its workplace's opening hours",
            ["pt"] = "Corrigido: o horário de um emprego agora sempre fica dentro do horário de funcionamento do local de trabalho",
            ["zh-cn"] = "已修复：工作的上班时间现在总在工作地点的营业时间之内",
            ["de"] = "Behoben: Die Arbeitszeiten eines Jobs liegen jetzt immer innerhalb der Öffnungszeiten des Arbeitsplatzes",
            ["fr"] = "Corrigé : les horaires d'un emploi tombent désormais toujours dans les heures d'ouverture du lieu de travail",
            ["es"] = "Corregido: el horario de un trabajo ahora siempre cae dentro del horario de apertura del lugar de trabajo",
            ["ru"] = "Исправлено: часы на работе теперь всегда укладываются в часы работы заведения",
            ["nl"] = "Opgelost: de werktijden van een baan vallen nu altijd binnen de openingstijden van de werkplek",
            ["tr"] = "Düzeltildi: bir işin çalışma saatleri artık her zaman iş yerinin açılış saatleri içinde kalıyor"
        };
        var missing = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!lines.TryGetValue(codes[l], out string wanted) || !WhatsNewHas(langs[l], wanted))
            {
                missing.Add(codes[l]);
            }
        }
        Check(missing.Count == 0 && codes.Length == lines.Count,
            "shift open: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", missing) + ")");
    }

    // True if two texts hold the same {placeholders}, in any order.
    private static bool SamePlaceholders(string a, string b)
    {
        var x = new List<string>();
        var y = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(a, "\\{[a-z]+\\}"))
        {
            x.Add(m.Value);
        }
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(b, "\\{[a-z]+\\}"))
        {
            y.Add(m.Value);
        }
        x.Sort(StringComparer.Ordinal);
        y.Sort(StringComparer.Ordinal);
        return string.Join(",", x) == string.Join(",", y);
    }
}
