using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Checks for whether a tech firm's people work (Scripts/Tech): the hours counted on the hour and kept in the save, who does
// which work, what a contract gets of the day's points, why nobody can work or worked, the roles a newcomer at a tech firm
// gets, and the texts that say so (each reason fits inside its notification).
internal static partial class Program
{
    private static void TechWork()
    {
        TechWorkHours();
        TechWorkWho();
        TechWorkGive();
        TechWorkWhy();
        TechWorkSave();
        TechWorkWords();
    }

    private static void TechWorkHours()
    {
        var p = new Profile();
        Check(p.WorkDay == -1 && p.PrevDay == -1 && TalentRules.HoursOn(p, 0) == 0, "tech work: a new file has no hours counted");
        // the count starts mid-day: the game's figure for today so far stands in
        TalentRules.RollHours(p, 10, -1, 3);
        Check(p.WorkDay == 10 && p.WorkHours == 3 && TalentRules.HoursOn(p, 9) == 0, "tech work: a count started mid-day takes the game's hours so far");
        p.WorkHours += 5;
        TalentRules.RollHours(p, 10, -1, 99);
        Check(p.WorkHours == 8, "tech work: the same day is not started again");
        // midnight: the day counted becomes the day before, and its own count wins over the game's
        TalentRules.RollHours(p, 11, 12, -1);
        Check(p.PrevDay == 10 && p.PrevHours == 8 && p.WorkDay == 11 && p.WorkHours == 0, "tech work: at midnight the day's count becomes yesterday's");
        Check(TalentRules.HoursOn(p, 10) == 8 && TalentRules.HoursOn(p, 11) == 0 && TalentRules.HoursOn(p, 9) == 0, "tech work: the hours of a day are found by its number");
        // a midnight the count missed: the game's figure for the day before stands in
        var q = new Profile { WorkDay = 5, WorkHours = 6 };
        TalentRules.RollHours(q, 11, 7, -1);
        Check(q.PrevDay == 10 && q.PrevHours == 7, "tech work: a day not counted takes the game's figure at midnight");
        var r = new Profile { WorkDay = 5, WorkHours = 6 };
        TalentRules.RollHours(r, 11, -1, 2);
        Check(TalentRules.HoursOn(r, 10) == 0 && r.WorkHours == 2, "tech work: with no figure for it, a day not counted has no hours");
        // a whole day of shifts gives a mid-level a day's points
        Profile mid = TalentRules.Make(5, 1, TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 1);
        int points = TalentRules.Output(mid, 50, 2) * FirmsCatalogue.PointsPerDay * TalentCatalogue.DayHours / TalentCatalogue.DayHours / 1000;
        Check(points > 0, "tech work: eight hours on a shift give an engineer points (" + points + ")");
    }

    private static void TechWorkWho()
    {
        RoleDef qa = TalentCatalogue.Role(TalentCatalogue.QaTester), web = TalentCatalogue.Role(TalentCatalogue.WebDesigner), graphic = TalentCatalogue.Role(TalentCatalogue.GraphicDesignerRole);
        RoleDef analyst = TalentCatalogue.Role(TalentCatalogue.SecurityAnalyst), support = TalentCatalogue.Role(TalentCatalogue.ItSupport), engineer = TalentCatalogue.Role(TalentCatalogue.SoftwareEngineer);
        Check(FirmsRules.Builds(engineer) && FirmsRules.Builds(analyst) && !FirmsRules.Builds(qa) && !FirmsRules.Builds(web) && !FirmsRules.Builds(support), "tech work: engineers and analysts build, testers, web designers and support do not");
        Check(FirmsRules.CanDo(web, WorkKind.Website) && !FirmsRules.CanDo(web, WorkKind.Integration), "tech work: a web designer works on a website only");
        Check(FirmsRules.CanDo(graphic, WorkKind.Branding) && !FirmsRules.CanDo(engineer, WorkKind.Branding) && FirmsRules.CanDo(analyst, WorkKind.Audit), "tech work: branding is the designers', an audit the analysts'");
        Check(FirmsRules.WorksFor(web, FirmKind.WebAgency) && FirmsRules.WorksFor(graphic, FirmKind.Design) && !FirmsRules.WorksFor(support, FirmKind.Security), "tech work: who works for which firm");
        // every tech firm's newcomers with the programmer skill get a role that does its work
        var wanted = new int[8];
        for (int k = 0; k <= (int)FirmKind.AiLab; k++)
        {
            int n = FirmsRules.Wanted(k, wanted);
            int role = TalentRules.Pick(TalentCatalogue.Programmer, wanted, n, 3, 7);
            Check(n > 0 && role >= 0 && FirmsRules.WorksFor(TalentCatalogue.Role(role), (FirmKind)k), "tech work: a programmer at a " + (FirmKind)k + " firm gets a role that does its work");
            Check(n <= wanted.Length, "tech work: the roles wanted fit the list");
        }
        Check(FirmsRules.Wanted(-1, wanted) == 0 && FirmsRules.Wanted((int)FirmKind.WebAgency, wanted) == 0, "tech work: a business of no tech type wants nothing of these");
    }

    private static void TechWorkGive()
    {
        int engineering = 0, design = 10, web = 6;
        int got = FirmsRules.Give(WorkKind.Website, 8, ref engineering, ref design, ref web);
        Check(got == 6 && design == 4 && web == 0, "tech work: web designers build a website when there are no engineers");
        engineering = 5; design = 6; web = 6;
        got = FirmsRules.Give(WorkKind.Website, 8, ref engineering, ref design, ref web);
        Check(got == 8 && engineering == 0 && web == 3 && design == 3, "tech work: a website takes the engineers first, then the web designers");
        engineering = 10; design = 3; web = 0;
        got = FirmsRules.Give(WorkKind.Integration, 5, ref engineering, ref design, ref web);
        Check(got == 5 && engineering == 5 && design == 3, "tech work: an integration takes engineering only");
        got = FirmsRules.Give(WorkKind.Branding, 5, ref engineering, ref design, ref web);
        Check(got == 3 && design == 0 && engineering == 5, "tech work: branding takes design only");
        got = FirmsRules.Give(WorkKind.Audit, 9, ref engineering, ref design, ref web);
        Check(got == 5 && engineering == 0, "tech work: no more is given than is left");
        got = FirmsRules.Give(WorkKind.Audit, -3, ref engineering, ref design, ref web);
        Check(got == 0, "tech work: nothing needed, nothing given");
    }

    private static void TechWorkWhy()
    {
        var f = new FirmFacts();
        Check(FirmsRules.Why(f, false) == Idle.NoStaff && FirmsRules.Why(f, true) == Idle.NoStaff, "tech work: nobody there comes first");
        f.Staff = 2;
        Check(FirmsRules.Why(f, false) == Idle.NoWorkers, "tech work: then nobody who does the work");
        f.Workers = 1;
        f.Closed = true;
        f.Net = 1;
        Check(FirmsRules.Why(f, false) == Idle.NetNone, "tech work: closed without an internet plan");
        f.Net = 2;
        Check(FirmsRules.Why(f, false) == Idle.NetInstalling, "tech work: closed while the internet is installed");
        f.Net = 3;
        Check(FirmsRules.Why(f, false) == Idle.NetOutage, "tech work: closed in an outage");
        f.Net = 0;
        f.Dark = true;
        Check(FirmsRules.Why(f, false) == Idle.NoPower, "tech work: closed without power");
        f.Dark = false;
        Check(FirmsRules.Why(f, false) == Idle.Shut, "tech work: closed by the game's switch");
        f.Closed = false;
        Check(FirmsRules.Why(f, false) == Idle.NoHours, "tech work: no opening hours");
        f.OpenDays = 6;
        Check(FirmsRules.Why(f, false) == Idle.NoStations, "tech work: no workstation and no shift");
        f.Stations = 2;
        Check(FirmsRules.Why(f, false) == Idle.NoShifts, "tech work: workstations but no shift");
        f.ShiftHours = 40;
        Check(FirmsRules.Why(f, false) == Idle.None, "tech work: shifts in the week, so somebody can work");
        Check(FirmsRules.Why(f, true) == Idle.DayOff, "tech work: no shift yesterday was a day off");
        f.ShiftsYesterday = 8;
        Check(FirmsRules.Why(f, true) == Idle.Away, "tech work: a shift yesterday not worked means they were away");
        f.Worked = 8;
        f.Closed = true;
        Check(FirmsRules.Why(f, true) == Idle.None && FirmsRules.Why(f, false) == Idle.Shut, "tech work: worked yesterday, closed now");
        Check((int)Idle.Count == 13, "tech work: every reason is counted");
    }

    private static void TechWorkSave()
    {
        var f = new TalentFile { Seed = 5 };
        Profile p = TalentRules.Make(5, 1, TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 1);
        p.Id = "Xy9==";
        p.WorkDay = 41;
        p.WorkHours = 5;
        p.PrevDay = 40;
        p.PrevHours = 8;
        f.People.Add(p);
        Profile fresh = TalentRules.Make(5, 2, TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 1);
        fresh.Id = "Ab1";
        f.People.Add(fresh);
        int version;
        TalentFile back = TalentStore.Read(TalentStore.Write(f), out version);
        Profile b = back.Of("Xy9==");
        Check(b != null && b.WorkDay == 41 && b.WorkHours == 5 && b.PrevDay == 40 && b.PrevHours == 8, "tech work: the hours counted are kept in the save");
        Profile c = back.Of("Ab1");
        Check(c != null && c.WorkDay == -1 && c.PrevDay == -1 && TalentRules.HoursOn(c, 40) == 0, "tech work: a file with no hours counted comes back with none");
        var firms = new FirmsFile { Seed = 2 };
        firms.Contracts.Add(new Contract { Id = 4, Place = "a~1", Kind = (int)WorkKind.Website, Points = 60, Stage = (int)WorkStage.Active, Told = Contract.ToldNobody | Contract.ToldBehind });
        firms.Contracts.Add(new Contract { Id = 5, Place = "a~1", Kind = (int)WorkKind.Audit, Points = 60, Stage = (int)WorkStage.Offered });
        FirmsFile fb = FirmsStore.Read(FirmsStore.Write(firms), out version);
        Check(fb.ContractOf(4) != null && fb.ContractOf(4).Told == (Contract.ToldNobody | Contract.ToldBehind) && fb.ContractOf(5).Told == 0, "tech work: what the player was told of a contract is kept");
    }

    private static readonly string[] IdleKeys =
    {
        "reallife_tech_idle_no_staff", "reallife_tech_idle_no_workers", "reallife_tech_idle_net_none", "reallife_tech_idle_net_installing", "reallife_tech_idle_net_outage",
        "reallife_tech_idle_no_power", "reallife_tech_idle_shut", "reallife_tech_idle_no_hours", "reallife_tech_idle_no_stations", "reallife_tech_idle_no_shifts",
        "reallife_tech_idle_day_off", "reallife_tech_idle_away"
    };

    private static void TechWorkWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("tech work", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        Check(IdleKeys.Length == (int)Idle.Count - 1, "tech work: a text for every reason");
        string client = new string('W', 40);
        foreach (string note in new[] { "reallife_tech_note_nobody", "reallife_tech_note_behind" })
        {
            string template;
            Check(en.TryGetValue(note, out template) && template.Contains("{client}") && template.Contains("{why}"), "tech work: " + note + " names the client and why");
            if (template == null)
            {
                continue;
            }
            foreach (string key in IdleKeys)
            {
                string why;
                if (!en.TryGetValue(key, out why) || LanguagesLacking(langs, codes, key).Length > 0)
                {
                    Check(false, "tech work: " + key + " has a text in each language");
                    continue;
                }
                why = why.Replace("{days}", "2").Replace("{day}", "1234").Replace("{time}", "18:00");
                string toast = template.Replace("{client}", client).Replace("{why}", why);
                Check(toast.Length <= 240, "tech work: " + note + " with " + key + " fits a toast (" + toast.Length + " characters)");
                Check(why.IndexOf('{') < 0, "tech work: " + key + " has no placeholder left unfilled");
            }
        }
        string guide;
        Check(en.TryGetValue("reallife_guide_tech_sector", out guide) && guide.Contains("{tech_net_days}") && guide.Contains("Start a product") && guide.Contains("Rota"),
            "tech work: the Guide sets up a firm step by step, with the internet, the rota and starting a product");
        string button;
        Check(en.TryGetValue("reallife_tech_button_new_product", out button) && guide != null && guide.Contains(button), "tech work: the Guide names the button as the app does");
    }
}
