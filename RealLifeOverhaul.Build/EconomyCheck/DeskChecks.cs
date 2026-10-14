using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Careers;

// Checks for the desk a professional job is worked at (Scripts/Careers/Work/DeskRules.cs): the kind of the game's station
// each profession and workplace works at, the preset fitted out where there is none, the station picked from the save's seed
// and the job (stable, in range, spread), the work panel's state and words by the time and the day, the boss's text when
// the player is away, and that the pay and the counted share of a day are as they were.
internal static partial class Program
{
    private static void Desks()
    {
        DeskKinds();
        DeskPicks();
        DeskStates();
        DeskAway();
        DeskPay();
        DeskWords();
    }

    private static void DeskKinds()
    {
        bool every = true, presets = true;
        string which = null;
        foreach (Profession p in ProCatalogue.All)
        {
            StationKind[] kinds = DeskRules.Kinds(p);
            DeskPreset preset = DeskRules.Preset(p);
            every &= preset != DeskPreset.None;
            DeskPreset wanted = p.Place == Workplace.Office ? DeskPreset.OfficeRow : p.Place == Workplace.Hospital ? DeskPreset.Ward : p.Place == Workplace.School ? DeskPreset.Classroom
                : p.Place == Workplace.CityHall ? DeskPreset.ServiceDesk : DeskPreset.BackOffice;
            if (preset != wanted)
            {
                presets = false;
                which = p.Key;
            }
            foreach (StationKind k in kinds)
            {
                every &= k != StationKind.None;
            }
        }
        Check(every, "desk: every profession has a desk: a station of the game's of a real kind, or a preset fitted out");
        Check(presets, "desk: the preset follows the workplace: an office's row of desks, a nurses' station, a teacher's desk, the council's service desks, a back office desk (" + which + ")");
        bool office = true;
        foreach (int prof in new[] { ProCatalogue.Developer, ProCatalogue.DataPro, ProCatalogue.SecurityPro, ProCatalogue.Designer, ProCatalogue.Accountant, ProCatalogue.Lawyer,
                     ProCatalogue.Architect, ProCatalogue.CivilEngineer, ProCatalogue.Marketer, ProCatalogue.Banker, ProCatalogue.ItSupport, ProCatalogue.HumanResources, ProCatalogue.Manager })
        {
            StationKind[] k = DeskRules.Kinds(ProCatalogue.Find(prof));
            office &= k.Length == 1 && k[0] == StationKind.Desk;
        }
        Check(office, "desk: office work, management and banking are at a desk computer of the game's");
        Check(First(ProCatalogue.Nurse) == StationKind.Desk && DeskRules.Preset(ProCatalogue.Find(ProCatalogue.Nurse)) == DeskPreset.Ward,
            "desk: a nurse works at one of the hospital's desk computers, or at a nurses' station fitted out");
        Check(DeskRules.Kinds(ProCatalogue.Find(ProCatalogue.Teacher)).Length == 0 && DeskRules.Preset(ProCatalogue.Find(ProCatalogue.Teacher)) == DeskPreset.Classroom,
            "desk: a teacher never takes a student's study desk at the school: a teacher's desk is fitted out");
        Check(First(ProCatalogue.CouncilOfficer) == StationKind.Counter && DeskRules.Kinds(ProCatalogue.Find(ProCatalogue.CouncilOfficer))[1] == StationKind.Desk,
            "desk: a council officer works at one of the tax office's counters, else a desk computer");
        Check(First(ProCatalogue.Chef) == StationKind.Register && First(ProCatalogue.BarStaff) == StationKind.Register && First(ProCatalogue.Stylist) == StationKind.Chair
              && First(ProCatalogue.SecurityGuard) == StationKind.Locker, "desk: kitchen and bar staff at a till, a stylist at a salon chair, a guard at the security post");
        Check(First(ProCatalogue.Warehouse) == StationKind.Desk && Array.IndexOf(DeskRules.Kinds(ProCatalogue.Find(ProCatalogue.Warehouse)), StationKind.Machine) > 0
              && First(ProCatalogue.Electrician) == StationKind.Machine && First(ProCatalogue.Sales) == StationKind.Desk
              && Array.IndexOf(DeskRules.Kinds(ProCatalogue.Find(ProCatalogue.Sales)), StationKind.Register) > 0,
            "desk: warehouse work at the warehouse's computer or a machine, the trades at a factory's machine, sales at a desk or a till");
        Check(DeskRules.Kinds(null).Length == 0 && DeskRules.Preset(null) == DeskPreset.None, "desk: no profession, no desk");
        Check(DeskRules.What(StationKind.Register, DeskPreset.BackOffice) == "reallife_desk_what_till" && DeskRules.What(StationKind.None, DeskPreset.Ward) == "reallife_desk_what_ward"
              && DeskRules.Expected(ProCatalogue.Find(ProCatalogue.Teacher)) == "reallife_desk_what_teacher" && DeskRules.Expected(ProCatalogue.Find(ProCatalogue.Developer)) == "reallife_desk_what_desk",
            "desk: what the desk is called: the station's kind, or the preset where there is none");
    }

    private static StationKind First(int prof)
    {
        StationKind[] k = DeskRules.Kinds(ProCatalogue.Find(prof));
        return k.Length > 0 ? k[0] : StationKind.None;
    }

    private static void DeskPicks()
    {
        bool inRange = true, stable = true;
        for (int count = 1; count <= 24; count++)
        {
            for (int job = 0; job < 50; job++)
            {
                int a = DeskRules.Pick(count, 12345, job);
                inRange &= a >= 0 && a < count;
                stable &= a == DeskRules.Pick(count, 12345, job);
            }
        }
        Check(inRange, "desk: the station picked is one of those that fit");
        Check(stable, "desk: the same building, seed and job give the same station every visit");
        Check(DeskRules.Pick(0, 1, 1) == -1, "desk: with no station of the kind, none is picked");
        var used = new HashSet<int>();
        for (int job = 0; job < 400; job++)
        {
            used.Add(DeskRules.Pick(8, 777, job));
        }
        Check(used.Count == 8, "desk: different jobs are spread over every station of the kind (" + used.Count + " of 8)");
        int same = 0;
        for (int job = 0; job < 200; job++)
        {
            same += DeskRules.Pick(10, 1, job) == DeskRules.Pick(10, 2, job) ? 1 : 0;
        }
        Check(same < 60, "desk: another save's seed picks another station mostly (" + same + " of 200 alike)");
        var j = new Job { Hired = 40 };
        new Opening { Id = 7, Prof = ProCatalogue.Developer }.CopyTo(j.Post);
        int key = DeskRules.JobKey(j);
        var again = new Job { Hired = 40 };
        new Opening { Id = 7, Prof = ProCatalogue.Developer }.CopyTo(again.Post);
        var other = new Job { Hired = 41 };
        new Opening { Id = 7, Prof = ProCatalogue.Developer }.CopyTo(other.Post);
        Check(key == DeskRules.JobKey(again) && key != DeskRules.JobKey(other) && DeskRules.JobKey(null) == 0, "desk: a job's number is its post and its first day, the same on every load");
    }

    private static Job DeskJob(WorkMode mode, int hired = 1)
    {
        var j = new Job { Hired = hired, SettledDay = 0, Work = (int)mode };
        new Opening { Id = 3, Prof = ProCatalogue.Developer, Rank = 0 }.CopyTo(j.Post);   // weekdays 09:00 for 8 hours; day 1 a Monday
        return j;
    }

    private static void DeskStates()
    {
        int m;
        bool late;
        Job j = DeskJob(WorkMode.OnSite);
        Check(DeskRules.State(j, 6, 10 * 60, false, out m, out late) == DeskState.DayOff && m == 0, "desk panel: a Saturday is a day off");
        Check(DeskRules.State(j, 2, 7 * 60 + 30, false, out m, out late) == DeskState.NotYet && !DeskRules.Startable(DeskState.NotYet), "desk panel: at 07:30 the shift has not started yet, and Start is not offered");
        Check(DeskRules.State(j, 2, 8 * 60 + 30, false, out m, out late) == DeskState.StartsSoon && m == 8 * 60 && !late && DeskRules.Startable(DeskState.StartsSoon),
            "desk panel: at 08:30 it starts soon, Start is offered, and the whole day is there");
        Check(DeskRules.State(j, 2, 9 * 60, false, out m, out late) == DeskState.Open && m == 8 * 60 && !late, "desk panel: at 09:00 the day is open, all of it");
        Check(DeskRules.State(j, 2, 9 * 60 + 20, false, out m, out late) == DeskState.Open && m == 8 * 60 - 20 && late, "desk panel: at 09:20 it is open and late, the rest of the day");
        Check(DeskRules.State(j, 2, 11 * 60 + 30, false, out m, out late) == DeskState.TooLate && m == 0 && !DeskRules.Startable(DeskState.TooLate),
            "desk panel: two and a half hours after the hour, with nothing worked, it is too late");
        Check(DeskRules.State(DeskJob(WorkMode.OnSite, 5), 2, 9 * 60, false, out m, out late) == DeskState.NotHired, "desk panel: before the first day the job has not begun");
        j.SettledDay = 2;
        Check(DeskRules.State(j, 2, 18 * 60, false, out m, out late) == DeskState.Done, "desk panel: a day counted is done today");
        Job part = DeskJob(WorkMode.OnSite);
        ProRules.Worked(part, 3, 300);
        Check(DeskRules.State(part, 3, 14 * 60, false, out m, out late) == DeskState.Open && m == 180, "desk panel: a day begun can be carried on with, the rest of it (" + m + " minutes)");
        Check(DeskRules.State(part, 3, 17 * 60, false, out m, out late) == DeskState.Done && m == 0, "desk panel: at the day's end a day begun is done");
        Check(DeskRules.State(part, 3, 11 * 60 + 30, false, out m, out late) == DeskState.Open, "desk panel: the late allowance is for starting, not for carrying on");
        Job remote = DeskJob(WorkMode.Remote);
        Check(DeskRules.State(remote, 2, 9 * 60, false, out m, out late) == DeskState.HomeDay, "desk panel: a remote job's desk at the employer's says the day is worked from home");
        Check(DeskRules.State(remote, 2, 9 * 60, true, out m, out late) == DeskState.Open && m == 480, "desk panel: ...and the home computer opens it");
        Job hybrid = DeskJob(WorkMode.Hybrid);
        Check(DeskRules.State(hybrid, 2, 9 * 60, true, out m, out late) == DeskState.OfficeDay && DeskRules.State(hybrid, 1, 9 * 60, true, out m, out late) == DeskState.Open
              && DeskRules.State(hybrid, 1, 9 * 60, false, out m, out late) == DeskState.HomeDay, "desk panel: a hybrid job's Mondays at home, Tuesdays at the employer's");
        Job leaving = DeskJob(WorkMode.OnSite);
        leaving.NoticeEnd = 3;
        Check(DeskRules.State(leaving, 4, 9 * 60, false, out m, out late) == DeskState.DayOff && DeskRules.State(leaving, 3, 9 * 60, false, out m, out late) == DeskState.Open,
            "desk panel: after the last day of notice there are no more working days");
        Check(DeskRules.State(null, 2, 9 * 60, false, out m, out late) == DeskState.NoJob, "desk panel: no job, nothing to work");
        Check(DeskRules.Label(DeskState.DayOff) == "playerhud_currentjob_day_off" && DeskRules.Label(DeskState.NotYet) == "workpanel_business_shift_not_yet_started"
              && DeskRules.Label(DeskState.StartsSoon) == "workpanel_business_shift_starts_soon" && DeskRules.Label(DeskState.Open) == null,
            "desk panel: the day off, the shift not yet started and starting soon are the game's own words for its job; an open day says nothing of its own");
        Check(DeskRules.Label(DeskState.Done) == "reallife_work_why_done_today" && DeskRules.Label(DeskState.TooLate) == "reallife_work_why_not_hours"
              && DeskRules.Label(DeskState.HomeDay) == "reallife_desk_label_home" && DeskRules.Label(DeskState.NotHired) == "reallife_desk_label_first",
            "desk panel: done today, too late, a day from home and a job not begun in the mod's words");
        bool states = true;
        foreach (DeskState s in Enum.GetValues(typeof(DeskState)))
        {
            states &= DeskRules.Startable(s) == (s == DeskState.Open || s == DeskState.StartsSoon) && (DeskRules.Startable(s) || DeskRules.Label(s) != null);
        }
        Check(states, "desk panel: Start is offered only on an open day or one starting soon, and every other state says why");
        var watch = Stopwatch.StartNew();
        int n = 0;
        for (int i = 0; i < 100000; i++)
        {
            n += DeskRules.State(j, 2 + (i & 3), (i * 7) % 1440, (i & 1) == 0, out m, out late) == DeskState.Open ? 1 : 0;
        }
        Check(watch.Elapsed.TotalMilliseconds < 200, "desk panel: a state is worked out quickly (" + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms for 100,000)");
    }

    private static void DeskAway()
    {
        Job j = DeskJob(WorkMode.OnSite);
        Profession p = j.Def;
        Check(DeskRules.AwayHour(p) == 10 && DeskRules.Away(j, 2, 10), "desk: an hour into a day not begun, the boss texts");
        Check(!DeskRules.Away(j, 2, 9) && !DeskRules.Away(j, 2, 11), "desk: once, at that hour");
        Check(!DeskRules.Away(j, 6, 10), "desk: not on a day off");
        ProRules.Worked(j, 2, 30);
        Check(!DeskRules.Away(j, 2, 10), "desk: not once the day is begun");
        Job settled = DeskJob(WorkMode.OnSite);
        settled.SettledDay = 2;
        Job off = DeskJob(WorkMode.OnSite);
        off.OffUntil = 2;
        Check(!DeskRules.Away(settled, 2, 10) && !DeskRules.Away(off, 2, 10) && !DeskRules.Away(DeskJob(WorkMode.OnSite, 5), 2, 10) && !DeskRules.Away(null, 2, 10),
            "desk: not on a day counted already, signed off, before the job begins, or with no job");
    }

    // What a day at the desk pays and counts is what it paid and counted from the Career app: the same rules.
    private static void DeskPay()
    {
        Check(ProRules.Wage(3800, 60) == 3800 && ProRules.Wage(3800, 30) == 1900 && ProRules.Wage(3800, 0) == 0, "desk: a day at the desk pays by the minute, as before");
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        int day = dev.Hours * 60;
        Job enough = DeskJob(WorkMode.OnSite);
        ProRules.Worked(enough, 2, day * ProRules.CountedShare / 1000);
        Job short1 = DeskJob(WorkMode.OnSite);
        ProRules.Worked(short1, 2, day * ProRules.CountedShare / 1000 - 1);
        Check(ProRules.CountedShare == 750 && ProRules.Settle(enough, 2) && !ProRules.Settle(short1, 2) && short1.Absent == 1,
            "desk: three quarters of the day worked is a day worked, a minute less a missed day, as before");
        Job overtime = DeskJob(WorkMode.OnSite);
        ProRules.Worked(overtime, 2, day);
        Check(DeskRules.State(overtime, 2, 17 * 60, false, out _, out _) == DeskState.Done, "desk: a whole day worked is done, and overtime is the desk's next button");
    }

    private static void DeskWords()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        var langs = new[] { LoadWords(System.IO.Path.Combine(folder, "en.json")), LoadWords(System.IO.Path.Combine(folder, "pt.json")), LoadWords(System.IO.Path.Combine(folder, "zh-cn.json")) };
        Check(langs[0] != null && langs[1] != null && langs[2] != null, "desk: en.json, pt.json and zh-cn.json are where the checks look for them");
        if (langs[0] == null || langs[1] == null || langs[2] == null)
        {
            return;
        }
        var keys = new List<string>
        {
            "reallife_desk_label_taken", "reallife_desk_label_hours", "reallife_desk_label_late", "reallife_desk_cta_header", "reallife_career_desk_title", "reallife_career_desk_at",
            "reallife_career_desk_how", "reallife_career_desk_home", "reallife_career_desk_none", "reallife_career_desk_closed", "reallife_career_desk_no_computer", "reallife_career_why_route", "reallife_work_interview_entrance",
            "reallife_work_text_away", "reallife_work_text_warning", "reallife_work_text_sacked", "reallife_contact_boss_desc", "reallife_work_test_desk",
            "reallife_work_test_desk_none", "reallife_work_test_desk_here", "reallife_work_test_desk_route"
        };
        foreach (DeskState s in Enum.GetValues(typeof(DeskState)))
        {
            string key = DeskRules.Label(s);
            if (key != null && key.StartsWith("reallife_", StringComparison.Ordinal))
            {
                keys.Add(key);
            }
        }
        foreach (StationKind k in Enum.GetValues(typeof(StationKind)))
        {
            keys.Add(DeskRules.What(k, DeskPreset.None));
        }
        foreach (DeskPreset p in Enum.GetValues(typeof(DeskPreset)))
        {
            keys.Add(DeskRules.What(StationKind.None, p));
        }
        var missing = new List<string>();
        foreach (string key in keys)
        {
            foreach (Dictionary<string, string> words in langs)
            {
                if (!words.TryGetValue(key, out string v) || string.IsNullOrWhiteSpace(v))
                {
                    missing.Add(key);
                    break;
                }
            }
        }
        Check(missing.Count == 0, "desk: the desk's words are in en.json, pt.json and zh-cn.json (" + keys.Count + " keys" + (missing.Count > 0 ? "; missing " + string.Join(", ", missing) : string.Empty) + ")");
        bool figures = true;
        foreach (Dictionary<string, string> words in langs)
        {
            figures &= words["reallife_work_text_away"].Contains("{from}") && words["reallife_work_text_away"].Contains("{to}") && words["reallife_work_text_away"].Contains("{until}")
                       && words["reallife_work_text_warning"].Contains("{count}") && words["reallife_work_text_warning"].Contains("{most}") && words["reallife_career_desk_at"].Contains("{what}")
                       && words["reallife_career_desk_at"].Contains("{address}") && words["reallife_desk_label_first"].Contains("{day}");
        }
        Check(figures, "desk: every language's texts have the figures they are written with");
        Check(!langs[0].ContainsKey("reallife_career_place_hospitals") && !langs[1].ContainsKey("reallife_career_place_hospitals") && !langs[2].ContainsKey("reallife_career_place_hospitals"),
            "desk: \"any hospital\" is gone: a nurse's desk is at the one hospital of the post");
        Check(langs[0]["reallife_guide_careers_jobs"].Contains("click your desk") && langs[0]["reallife_guide_careers_life"].Contains("Work late"),
            "desk: the Guide says how a working day is worked now, and where overtime is");
    }
}
