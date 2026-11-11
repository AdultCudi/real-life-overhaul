using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// Working at a desk of one's own tech firm (Tech/Firms/OwnDeskRules, TechDirector.OwnDesk, CareersDirector.OwnDesk): a player
// reported they had the programmer's training and a software studio with staff, but no way to work in it themselves. The owner's
// minutes kept by the day, what they give the next morning's crew (a professional's of the same role, level and skills for the same
// hours, no pay, no seat), the profession they work as at each kind of firm, the save, the click and the game's panel, the page, the
// test, the texts and the Guide in every language.
internal static partial class Program
{
    private static void OwnDesk()
    {
        OwnDeskPoints();
        OwnDeskDays();
        OwnDeskCrew();
        OwnDeskProfessions();
        OwnDeskSave();
        OwnDeskHours();
        OwnDeskSources();
        OwnDeskTexts();
    }

    private static OwnerStanding Owner(int role, int level, int main, int a, int b, int burnout = 0)
    {
        return new OwnerStanding { Prof = ProCatalogue.Developer, Rank = 0, Role = role, Level = level, Main = main, A = a * 1000, B = b * 1000, Burnout = burnout };
    }

    private static void OwnDeskPoints()
    {
        Check(TalentRules.Output(new Profile { Level = TalentCatalogue.Mid, A = 50000, B = 50000, Morale = OwnDeskRules.Morale }, 50, 1) == TalentCatalogue.LevelOutput[TalentCatalogue.Mid],
            "own desk: the owner's morale is an output factor of one: a mid-level of skill 50 gives the level's own output");
        Check(OwnDeskRules.StintMinutes == TalentCatalogue.DayHours * 60, "own desk: a stint at the desk is a working day at most");
        bool same = true;
        string first = null;
        int[] roles = { TalentCatalogue.SoftwareEngineer, TalentCatalogue.QaTester, TalentCatalogue.UxDesigner, TalentCatalogue.WebDesigner, TalentCatalogue.DataScientist,
            TalentCatalogue.SecurityAnalyst, TalentCatalogue.SalesExecutive };
        foreach (int role in roles)
        {
            for (int level = 0; level < TalentCatalogue.Levels; level++)
            {
                foreach (int main in new[] { 0, 20, 50, 85, 100 })
                {
                    OwnerStanding s = Owner(role, level, main, main / 2, main, level * 150);
                    Profile p = OwnDeskRules.AsProfile(s);
                    for (int h = 0; h <= TalentCatalogue.DayHours; h++)
                    {
                        // the firm's own formula for a professional's hours (TechDirector.Crews)
                        int staff = TalentRules.Output(p, main, 9) * FirmsCatalogue.PointsPerDay * h / TalentCatalogue.DayHours / 1000;
                        int owner = OwnDeskRules.Points(s, h * 60, 9);
                        if (staff != owner && first == null)
                        {
                            first = role + "/" + level + "/" + main + "/" + h + ": " + owner + " against " + staff;
                        }
                        same &= staff == owner;
                    }
                }
            }
        }
        Check(same, "own desk: the owner's minutes give exactly what a professional of the same role, level and skills gives for the same hours (" + (first ?? "every case") + ")");
        OwnerStanding mid = Owner(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 50, 50, 50);
        Check(OwnDeskRules.Points(mid, OwnDeskRules.StintMinutes, 1) == FirmsCatalogue.PointsPerDay,
            "own desk: a mid-level software engineer of skill 50 gives the Guide's points a day in a full stint");
        Check(OwnDeskRules.Points(mid, 0, 1) == 0 && OwnDeskRules.Points(mid, -30, 1) == 0, "own desk: no minutes, no points");
        Check(OwnDeskRules.Points(Owner(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Senior, 70, 70, 70), 240, 1)
              > OwnDeskRules.Points(Owner(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Junior, 20, 20, 20), 240, 1),
            "own desk: a senior of more skill gives more for the same time than a junior");
        Check(OwnDeskRules.Points(Owner(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 50, 50, 50, 900), 480, 1) < OwnDeskRules.Points(mid, 480, 1),
            "own desk: burnout from a job of the player's slows the owner's work too");
        Check(OwnDeskRules.Points(Owner(TalentCatalogue.Technician, TalentCatalogue.Mid, 50, 50, 50), 480, 1) == 0
              && OwnDeskRules.Points(Owner(TalentCatalogue.Accountant, TalentCatalogue.Mid, 50, 50, 50), 480, 1) == 0
              && OwnDeskRules.Points(Owner(-1, 0, 50, 50, 50), 480, 1) == 0 && OwnDeskRules.Points(Owner(TalentCatalogue.RoleCount, 0, 50, 50, 50), 480, 1) == 0,
            "own desk: a role a firm's crew does not count (a technician, an accountant, none) gives nothing");
        OwnerStanding wild = Owner(TalentCatalogue.SoftwareEngineer, 99, 500, 900, -40, 5000);
        Profile clamped = OwnDeskRules.AsProfile(wild);
        Check(clamped.Level == TalentCatalogue.Levels - 1 && clamped.A == 100000 && clamped.B == 0 && clamped.Burnout == 1000 && clamped.Morale == OwnDeskRules.Morale
              && OwnDeskRules.Points(wild, 480, 1) == OwnDeskRules.Points(wild, 480, 1), "own desk: a standing out of range is held to the professions' bounds, and the same every time");
    }

    private static void OwnDeskDays()
    {
        var f = new Firm();
        Check(OwnDeskRules.MinutesOn(f, 10) == 0 && OwnDeskRules.MinutesOn(f, -1) == 0, "own desk days: a new firm has no minutes of the owner's");
        OwnDeskRules.Credit(f, 10, 60);
        OwnDeskRules.Credit(f, 10, 30);
        Check(OwnDeskRules.MinutesOn(f, 10) == 90, "own desk days: the minutes of a day add up");
        OwnDeskRules.Credit(f, 11, 20);
        Check(OwnDeskRules.MinutesOn(f, 10) == 90 && OwnDeskRules.MinutesOn(f, 11) == 20, "own desk days: a new day starts its own count and keeps the day before for the morning");
        OwnDeskRules.Credit(f, 10, 15);
        Check(OwnDeskRules.MinutesOn(f, 10) == 105 && OwnDeskRules.MinutesOn(f, 11) == 20, "own desk days: minutes of the day before still reach it (a stint over midnight)");
        OwnDeskRules.Credit(f, 13, 5);
        Check(OwnDeskRules.MinutesOn(f, 10) == 0 && OwnDeskRules.MinutesOn(f, 11) == 20 && OwnDeskRules.MinutesOn(f, 12) == 0 && OwnDeskRules.MinutesOn(f, 13) == 5,
            "own desk days: two days on, the oldest count goes and a day not worked reads none");
        OwnDeskRules.Credit(f, 9, 40);
        OwnDeskRules.Credit(f, 13, 0);
        OwnDeskRules.Credit(f, 13, -50);
        OwnDeskRules.Credit(null, 13, 50);
        Check(OwnDeskRules.MinutesOn(f, 9) == 0 && OwnDeskRules.MinutesOn(f, 13) == 5 && OwnDeskRules.MinutesOn(f, 11) == 20,
            "own desk days: an older day, no minutes or a lost firm change nothing");
        OwnDeskRules.Credit(f, 13, 5000);
        Check(OwnDeskRules.MinutesOn(f, 13) == OwnDeskRules.DayMost, "own desk days: a day holds a day's minutes at most, whatever a fast forward credits");
    }

    private static void OwnDeskCrew()
    {
        // FirmsRules.Join is the professionals' and the owner's one way into a crew.
        var c = new Crew();
        int seniors = 0;
        long faults = 0;
        Check(FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.SoftwareEngineer), 10, 8, true, 100, ref seniors, ref faults) && c.Engineering == 10 && seniors == 10 && faults == 1000,
            "own desk crew: an engineer's points build, count as a senior's and weigh their faults, and their pay qualifies for relief");
        Check(FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.QaTester), 6, 8, true, 100, ref seniors, ref faults) && c.Testing == 6 && c.Engineering == 10 && seniors == 10,
            "own desk crew: a tester's points go to testing");
        Check(!FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.Technician), 9, 8, true, 100, ref seniors, ref faults) && c.Engineering == 10 && c.Testing == 6,
            "own desk crew: a technician adds no points and no relief");
        Check(FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.WebDesigner), 4, 8, false, 100, ref seniors, ref faults) && c.Design == 4 && c.Web == 4
              && FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.UxDesigner), 3, 8, false, 100, ref seniors, ref faults) && c.Design == 7 && c.Web == 4,
            "own desk crew: designers' points go to design, a web designer's to websites too");
        Check(!FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.SalesExecutive), 5, 8, false, 100, ref seniors, ref faults) && c.Selling == 5
              && !FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.ItSupport), 0, 3, false, 100, ref seniors, ref faults) && c.Support == 1
              && !FirmsRules.Join(ref c, TalentCatalogue.Role(TalentCatalogue.ItSupport), 0, 0, false, 100, ref seniors, ref faults) && c.Support == 1,
            "own desk crew: sellers' points sell, a support person who worked answers once, one who did not counts nothing");
        Check(FirmsRules.Counts(TalentCatalogue.Role(TalentCatalogue.SoftwareEngineer)) && FirmsRules.Counts(TalentCatalogue.Role(TalentCatalogue.UxDesigner))
              && !FirmsRules.Counts(TalentCatalogue.Role(TalentCatalogue.Technician)) && !FirmsRules.Counts(TalentCatalogue.Role(TalentCatalogue.Accountant)) && !FirmsRules.Counts(null),
            "own desk crew: the roles that add to a crew are those Join counts");

        // The owner's crew, merged after the firm's own (its spare share), adds points and nothing else.
        OwnerStanding s = Owner(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Senior, 60, 60, 60);
        var own = new Crew();
        int ownSeniors = 0;
        long ownFaults = 0;
        int points = OwnDeskRules.Join(ref own, s, 300, 4, ref ownSeniors, ref ownFaults);
        Check(points > 0 && points == OwnDeskRules.Points(s, 300, 4) && own.Engineering == points && ownSeniors == points && ownFaults > 0,
            "own desk crew: the owner's engineering counts as a senior's, with their faults weighed in");
        var firm = new Crew { Engineering = 100, Testing = 20, Design = 30, Web = 10, Product = 5, Selling = 7, Support = 2, Seats = 9, Wages = 12345, Security = 400, Reliability = 300, Faults = 120 };
        var merged = firm;
        OwnDeskRules.Add(ref merged, in own);
        Check(merged.Engineering == 100 + points && merged.Testing == 20 && merged.Design == 30 && merged.Seats == 9 && merged.Wages == 12345 && merged.Security == 400
              && merged.Reliability == 300, "own desk crew: the owner adds their points, and no seat, wage, security or reliability (no pay, it is their firm)");
        var none = new Crew();
        int noSeniors = 0;
        long noFaults = 0;
        Check(OwnDeskRules.Join(ref none, s, 0, 4, ref noSeniors, ref noFaults) == 0 && none.Engineering == 0 && noSeniors == 0 && noFaults == 0
              && OwnDeskRules.Join(ref none, Owner(TalentCatalogue.Accountant, 2, 60, 60, 60), 300, 4, ref noSeniors, ref noFaults) == 0 && none.Engineering == 0,
            "own desk crew: no minutes, or a role no crew counts, join nothing");
        OwnerStanding designer = Owner(TalentCatalogue.UxDesigner, TalentCatalogue.Mid, 50, 50, 50);
        var d = new Crew();
        int dp = OwnDeskRules.Join(ref d, designer, 480, 4, ref noSeniors, ref noFaults);
        Check(dp > 0 && d.Design == dp && d.Engineering == 0, "own desk crew: an owner who is a designer adds to the design, as a designer on the staff does");

        // Yesterday's hours of the owner's are hours worked, where their role does the work asked.
        Check(OwnDeskRules.HoursFor(61, TalentCatalogue.SoftwareEngineer, -1, FirmKind.Software) == 2 && OwnDeskRules.HoursFor(60, TalentCatalogue.SoftwareEngineer, -1, FirmKind.Software) == 1
              && OwnDeskRules.HoursFor(0, TalentCatalogue.SoftwareEngineer, -1, FirmKind.Software) == 0 && OwnDeskRules.HoursFor(90, -1, -1, FirmKind.Software) == 0,
            "own desk crew: the owner's hours yesterday are begun hours, none without minutes or a role");
        Check(OwnDeskRules.HoursFor(120, TalentCatalogue.UxDesigner, (int)WorkKind.Branding, FirmKind.Design) == 2
              && OwnDeskRules.HoursFor(120, TalentCatalogue.UxDesigner, (int)WorkKind.Integration, FirmKind.Software) == 0,
            "own desk crew: the owner's hours count on the work their role does, as a professional's");
        var facts = new FirmFacts { Staff = 0, Worked = 0 };
        OwnDeskRules.Count(ref facts, 180, TalentCatalogue.SoftwareEngineer, -1, FirmKind.Software);
        Check(facts.Worked == 3 && FirmsRules.Why(facts, true) == Idle.None, "own desk crew: a firm whose owner worked yesterday is not said to have been idle");
    }

    private static void OwnDeskProfessions()
    {
        const string studio = "reallife:businesstype_softwarestudio", web = "ba:businesstype_webdevelopmentagency", graphic = "ba:businesstype_graphicdesigner";
        var list = new List<int>(8);
        OwnRules.FirmProfessions(studio, list);
        string sector = string.Join(",", list);
        OwnRules.FirmProfessions(web, list);
        string agency = string.Join(",", list);
        OwnRules.FirmProfessions(graphic, list);
        string designer = string.Join(",", list);
        Check(sector == string.Join(",", ProCatalogue.Developer, ProCatalogue.DataPro, ProCatalogue.SecurityPro, ProCatalogue.Designer, ProCatalogue.ItSupport, ProCatalogue.Sales),
            "own desk professions: the sector's own firms can be worked in as a developer, a data or security professional, a designer, IT support or sales (" + sector + ")");
        Check(agency == string.Join(",", ProCatalogue.Developer, ProCatalogue.Designer, ProCatalogue.ItSupport), "own desk professions: a web development agency as a developer, a designer or IT support (" + agency + ")");
        Check(designer == ProCatalogue.Designer.ToString(), "own desk professions: a graphic designer as a designer (" + designer + ")");
        bool every = true;
        foreach (string type in ProCatalogue.TechSector)
        {
            OwnRules.FirmProfessions(type, list);
            every &= string.Join(",", list) == sector;
        }
        OwnRules.FirmProfessions("ba:businesstype_supermarket", list);
        Check(every && list.Count == 0, "own desk professions: all five of the sector's kinds alike, and a shop none");

        var nobody = new StudyFile();
        Check(OwnRules.FirmProfession(nobody, studio, 100, 60, out int junior) == ProCatalogue.Sales && junior == 0 && OwnRules.FirmProfession(nobody, web, 100, 60, out int none) == -1
              && none == -1 && OwnRules.FirmProfession(nobody, graphic, 100, 60, out _) == -1 && OwnRules.FirmProfession(null, studio, 100, 60, out _) == -1
              && OwnRules.FirmProfession(nobody, null, 100, 60, out _) == -1,
            "own desk professions: with nothing studied only a junior seller at the sector's firms (sales asks no course), nothing at an agency or a design studio");
        var coder = new StudyFile();
        coder.Results.Add(new Enrolment { Course = StudyCatalogue.CodingBootcamp, Stage = (int)EnrolStage.Passed, Grade = 3 });
        coder.Skill[TalentCatalogue.Coding] = 30000;
        coder.Skill[TalentCatalogue.SystemDesign] = 20000;
        int prof = OwnRules.FirmProfession(coder, studio, 100, 60, out int rank);
        Check(prof == ProCatalogue.Developer && rank == 0, "own desk professions: the coding bootcamp and its skills make a junior developer at a software studio (" + prof + "/" + rank + ")");
        Check(OwnRules.FirmProfession(coder, web, 100, 60, out _) == ProCatalogue.Developer && OwnRules.FirmProfession(coder, graphic, 100, 60, out _) == -1,
            "own desk professions: a developer can work at a web agency, not at a graphic designer's");
        coder.Results.Add(new Enrolment { Course = StudyCatalogue.Design, Stage = (int)EnrolStage.Passed, Grade = 3 });
        coder.Skill[TalentCatalogue.Visual] = 30000;
        coder.Skill[TalentCatalogue.Research] = 30000;
        Check(OwnRules.FirmProfession(coder, studio, 100, 60, out _) == ProCatalogue.Developer && OwnRules.FirmProfession(coder, graphic, 100, 60, out _) == ProCatalogue.Designer,
            "own desk professions: of two at the same level the catalogue's first, and the one a firm employs");
        coder.Work.History.Add(new Spell { Prof = ProCatalogue.Designer, From = 1, To = 400 });
        coder.Skill[TalentCatalogue.Visual] = 60000;
        coder.Skill[TalentCatalogue.Research] = 60000;
        Check(OwnRules.FirmProfession(coder, studio, 500, 60, out int designerRank) == ProCatalogue.Designer && ProCatalogue.All[ProCatalogue.Designer].Ranks[designerRank].Level > TalentCatalogue.Junior,
            "own desk professions: the higher level wins, as a designer with years behind them");
        Check(OwnRules.FirmProfession(coder, studio, 500, 60, out int again) == ProCatalogue.Designer && again == designerRank, "own desk professions: the same answer every time");
    }

    private static void OwnDeskSave()
    {
        var f = new FirmsFile { Seed = 5, FirstDay = 1, LastDay = 20 };
        var m = new Firm { Place = "Dock%20St~4", Kind = (int)FirmKind.Software, Founded = 3 };
        OwnDeskRules.Credit(m, 19, 200);
        OwnDeskRules.Credit(m, 20, 75);
        f.Firms.Add(m);
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(f), out _);
        Firm b = back.Firms.Count == 1 ? back.Firms[0] : null;
        Check(b != null && b.OwnDay == 20 && b.OwnMinutes == 75 && b.OwnPrevDay == 19 && b.OwnPrevMinutes == 200 && b.Place == m.Place && b.Founded == 3,
            "own desk save: the owner's two counts come back with the firm");
        // A save from before: the firm's record as it was written, ending with the takings by day.
        var w = new TechWriter(FirmsStore.Version);
        w.Part('h').Record().Add(5).Add(1).Add(20).Add(1).Add(0);
        w.Part('f');
        w.Record().Place("Dock%20St~4").Add(0).Add(3).Add(0).Add(0).Add(500).Add(0).Add(0).Add(0).Add(0).Add(0).Add(0).Add("1,2,3");
        FirmsFile old = FirmsStore.Read(w.ToString(), out _);
        Firm o = old.Firms.Count == 1 ? old.Firms[0] : null;
        Check(o != null && o.OwnDay == -1 && o.OwnMinutes == 0 && o.OwnPrevDay == -1 && o.OwnPrevMinutes == 0 && o.Days[2] == 3 && OwnDeskRules.MinutesOn(o, 19) == 0,
            "own desk save: a firm saved before has no minutes of the owner's, and its takings as they were");
    }

    // The hours of a stint are the player's to pick on the work panel's slider (ShiftSkip's ISlidingShift): one to twelve, eight to begin
    // with, kept in the save under reallife:own_desk_hours. Every figure of a stint follows the minutes worked, so none assumes a day.
    private static void OwnDeskHours()
    {
        Check(OwnDeskRules.DefaultHours == 8 && OwnDeskRules.DefaultHours == TalentCatalogue.DayHours && OwnDeskRules.MinutesFor(OwnDeskRules.DefaultHours) == OwnDeskRules.StintMinutes
              && OwnDeskRules.StintMinutes == 480 && OwnDeskRules.HoursFrom(OwnDeskRules.HoursLine(OwnDeskRules.DefaultHours)) == 8,
            "own desk hours: eight is the default, a working day, as the stint was before the slider");
        Check(OwnDeskRules.LeastHours == 1 && OwnDeskRules.MostHours == 12 && OwnDeskRules.MostHours * 60 <= OwnDeskRules.DayMost, "own desk hours: the slider runs from 1 to 12 hours, and a day holds them");
        bool follows = true;
        for (int h = OwnDeskRules.LeastHours; h <= OwnDeskRules.MostHours; h++)
        {
            follows &= OwnDeskRules.MinutesFor(h) == h * 60 && OwnDeskRules.ClampHours(h) == h && OwnDeskRules.HoursFrom(OwnDeskRules.HoursLine(h)) == h;
        }
        Check(follows, "own desk hours: the stint's length follows the choice, an hour for an hour, and each choice survives the save's line");
        Check(OwnDeskRules.HoursFrom(null) == 8 && OwnDeskRules.HoursFrom(string.Empty) == 8 && OwnDeskRules.HoursFrom("v=1") == 8 && OwnDeskRules.HoursFrom("6.5") == 8,
            "own desk hours: a save without the line, or with one no one can read, gets 8");
        Check(OwnDeskRules.ClampHours(0) == 1 && OwnDeskRules.ClampHours(-7) == 1 && OwnDeskRules.ClampHours(13) == 12 && OwnDeskRules.ClampHours(int.MaxValue) == 12 && OwnDeskRules.ClampHours(int.MinValue) == 1
              && OwnDeskRules.HoursFrom("0") == 1 && OwnDeskRules.HoursFrom("-3") == 1 && OwnDeskRules.HoursFrom("99") == 12 && OwnDeskRules.HoursLine(40) == "12"
              && OwnDeskRules.MinutesFor(0) == 60 && OwnDeskRules.MinutesFor(99) == 720, "own desk hours: a value outside the range is held to it, in the save's line and in the stint");

        // The work points are a professional's for the same minutes, so they scale with the hours; so do the begun hours a crew counts.
        OwnerStanding mid = Owner(TalentCatalogue.SoftwareEngineer, TalentCatalogue.Mid, 50, 50, 50);
        bool exact = true, rises = true;
        int before = 0;
        for (int h = OwnDeskRules.LeastHours; h <= OwnDeskRules.MostHours; h++)
        {
            int points = OwnDeskRules.Points(mid, OwnDeskRules.MinutesFor(h), 3);
            int staff = TalentRules.Output(OwnDeskRules.AsProfile(mid), 50, 3) * FirmsCatalogue.PointsPerDay * h / TalentCatalogue.DayHours / 1000;
            exact &= points == staff;
            rises &= points > before;
            before = points;
            var crew = new Crew();
            int seniors = 0;
            long faults = 0;
            exact &= OwnDeskRules.Join(ref crew, mid, OwnDeskRules.MinutesFor(h), 3, ref seniors, ref faults) == points && crew.Engineering == points
                     && OwnDeskRules.HoursFor(OwnDeskRules.MinutesFor(h), TalentCatalogue.SoftwareEngineer, -1, FirmKind.Software) == h;
        }
        Check(exact && rises, "own desk hours: the points of a stint are exactly a professional's for those hours, one hour to twelve, and the crew counts the hours chosen");
        int four = OwnDeskRules.Points(mid, 240, 3), eight = OwnDeskRules.Points(mid, 480, 3), twelve = OwnDeskRules.Points(mid, 720, 3);
        Check(eight == FirmsCatalogue.PointsPerDay && four * 2 == eight && twelve * 2 == eight * 3,
            "own desk hours: half the hours give half the points, and twelve give a day and a half (" + four + ", " + eight + ", " + twelve + ")");
        Check(OwnDeskRules.Points(mid, OwnDeskRules.MinutesFor(0), 3) == OwnDeskRules.Points(mid, 60, 3) && OwnDeskRules.Points(mid, OwnDeskRules.MinutesFor(50), 3) == twelve,
            "own desk hours: a choice outside the range gives the points of the nearest end");

        // The sources: the panel's slider, the choice kept and read, and no figure of the stint tied to a day.
        string desk = ModSource(@"Tech\TechDirector.OwnDesk.cs");
        string firms = ModSource(@"Tech\TechDirector.Firms.cs");
        string skip = ModSource(@"Jobs\ShiftSkip.cs");
        string facts = ModSource(@"Ui\Activity\ActivityFacts.cs");
        string careers = ModSource(@"Careers\CareersDirector.OwnDesk.cs");
        Check(desk.Contains("private sealed class OwnStint : ISlidingShift") && desk.Contains("public int SkipMinutes => OwnDeskRules.MinutesFor(Hours);") && desk.Contains("public int SlideLeast => OwnDeskRules.LeastHours;")
              && desk.Contains("public int SlideMost => OwnDeskRules.MostHours;") && desk.Contains("Hours = OwnDeskRules.ClampHours(value);")
              && MethodText(desk, "private void OpenStint(FirmPlace firm, EmployeeStationController desk, OwnerStanding standing)").Contains("standing, _ownHours);"),
            "own desk hours sources: the stint takes its length from the slider, which starts where the player left it");
        Check(desk.Contains("_director._ownHours = Hours;") && firms.Contains("OwnDeskRules.HoursFrom(save.modData != null && save.modData.TryGetValue(OwnDeskRules.HoursKey, out string hours) ? hours : null)")
              && firms.Contains("data[OwnDeskRules.HoursKey] = OwnDeskRules.HoursLine(_ownHours);") && OwnDeskRules.HoursKey == "reallife:own_desk_hours",
            "own desk hours sources: the last choice is kept in the save under reallife:own_desk_hours and read back, a save without it at 8");
        Check(skip.Contains("public interface ISlidingShift : ISkippableShift") && skip.Contains("return _slide != null && _shift != null && _state == PlayerActivityState.NotStarted && _slide.SlideMost > _slide.SlideLeast;")
              && skip.Contains("_total = Mathf.Max(1, _shift.SkipMinutes);") && MethodText(skip, "private IEnumerator Begin()").Contains("_total = Mathf.Max(1, _shift.SkipMinutes);")
              && facts.Contains("if (skip.Slides && m.HasDuration)"),
            "own desk hours sources: the game's work panel shows the slider before the start only, the clock runs the hours chosen, and the card says when they end");
        Check(desk.Split(new[] { "OwnDeskRules.StintMinutes" }, StringSplitOptions.None).Length - 1 == 2
              && MethodText(desk, "public LabelInfo[] SkipLabels()").Contains("OwnDeskRules.StintMinutes") && MethodText(desk, "internal void OwnDeskLine(InfoBox box, FirmPlace firm)").Contains("OwnDeskRules.StintMinutes"),
            "own desk hours sources: the only fixed day left is the rate the page and the panel's line quote for a full day; the stint, its end, its notice and its test use the hours chosen");
        Regex day = new Regex(@"\b(480|DayHours|StintMinutes)\b");
        Check(!day.IsMatch(MethodText(desk, "private void EndStint(OwnStint stint, bool told)")) && !day.IsMatch(MethodText(desk, "private void StintWorked(OwnStint stint, int minutes)"))
              && !day.IsMatch(MethodText(careers, "internal void FirmWorked(int prof, int rank, int minutes)")),
            "own desk hours sources: what a stint ends with (the points, Careers' learning and burnout) is worked out from the minutes, with no day's length in it");
        Check(OwnDeskSliderTexts(), "own desk hours texts: the slider's words are in en, pt and zh-cn with the hours, the time and the points to fill in");
        Check(LineEverywhere(new[] { "has an hours slider on the game's work panel", "tem um controle de horas no painel de trabalho do jogo", "时长滑块", "gibt es im Arbeitspanel des Spiels einen Stundenregler",
                "dispose d'un curseur d'heures dans le panneau de travail du jeu", "tiene un control de horas en el panel de trabajo del juego", "имеет ползунок часов на рабочей панели игры",
                "heeft in het werkpaneel van het spel een urenschuif", "oyunun iş panelinde 1 ile 12 saat arasında" }),
            "own desk hours texts: What's new tells of the hours slider in some row of every language");
    }

    private static bool OwnDeskSliderTexts()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        bool all = true;
        for (int l = 0; l < codes.Length; l++)
        {
            if (langs[l] == null)
            {
                continue;
            }
            bool slider = langs[l].TryGetValue("reallife_tech_own_slider", out string text) && text.Contains("{hours}") && text.Contains("{time}") && text.Contains("{points}");
            bool core = codes[l] == "en" || codes[l] == "pt" || codes[l] == "zh-cn";
            all &= slider || !core;
        }
        return all && AllIn(new[] { "reallife_tech_own_slider" }, out _);
    }

    private static void OwnDeskSources()
    {
        string desk = ModSource(@"Tech\TechDirector.OwnDesk.cs");
        string firms = ModSource(@"Tech\TechDirector.Firms.cs");
        string app = ModSource(@"Tech\TechDirector.FirmsApp.cs");
        string director = ModSource(@"Tech\TechDirector.cs");
        string page = ModSource(@"Tech\Firms\TechApp.Pages.cs");
        string bridges = ModSource(@"Tech\TechBridges.cs");
        string game = ModSource(@"Tech\Firms\FirmsGame.cs");
        string links = ModSource(@"Wiring\CareersLinks.cs");
        string careers = ModSource(@"Careers\CareersDirector.OwnDesk.cs");
        string deskGame = ModSource(@"Careers\DeskGame.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string list = ModSource(@"Meta\FeatureList.Economy.cs");

        string crews = MethodText(firms, "private void Crews()");
        Check(crews.Contains("FirmsRules.Join(ref c, r, points, h, p.Level >= TalentCatalogue.Senior, TalentRules.Faults(p, person.MainPoints), ref seniors, ref faultWeighted)")
              && !crews.Contains("case Family.Engineering:") && crews.Contains("OwnShare(firm, ref own, ref seniors, ref faultWeighted);")
              && crews.IndexOf("OwnShare(", StringComparison.Ordinal) < crews.IndexOf("c.SeniorShare =", StringComparison.Ordinal)
              && crews.IndexOf("firm.Spare = FirmsGame.Spare(firm.Reg, hours);", StringComparison.Ordinal) < crews.IndexOf("OwnDeskRules.Add(ref c, in own);", StringComparison.Ordinal)
              && crews.IndexOf("OwnDeskRules.Add(ref c, in own);", StringComparison.Ordinal) < crews.IndexOf("FirmsRules.Connected(ref c, firm.Net);", StringComparison.Ordinal),
            "own desk sources: the crew joins professionals and the owner by one rule; the owner's points after a game office's sold share and before the connection");
        string share = MethodText(desk, "private void OwnShare(FirmPlace firm, ref Crew own, ref int seniors, ref long faultWeighted)");
        Check(share.Contains("OwnDeskRules.MinutesOn(firm.File, Save.Day - 1)") && share.Contains("TechBridges.OwnerAt(firm.Reg.businessTypeName, out OwnerStanding s, out _, out _)")
              && share.Contains("OwnDeskRules.Join(ref own, s, firm.OwnMinutes, Save.Day, ref seniors, ref faultWeighted)"),
            "own desk sources: the morning reads yesterday's minutes and the player as Careers has them now");
        Check(MethodText(firms, "private void FirmsDay(int day)").Contains("Crews();\r\n            OwnShareChecked(day);") || MethodText(firms, "private void FirmsDay(int day)").Contains("Crews();\n            OwnShareChecked(day);"),
            "own desk sources: one self-check line a morning for each firm the owner worked at");
        Check(MethodText(app, "internal FirmFacts FactsOf(FirmPlace firm, int kind)").Contains("OwnDeskRules.Count(ref facts, firm.OwnMinutes, firm.OwnRole, kind, firm.File.K);"),
            "own desk sources: a firm whose owner worked yesterday is not told of as idle");
        Check(director.Contains("KeepWarehouses();\r\n                DeskSecond();") || director.Contains("KeepWarehouses();\n                DeskSecond();"),
            "own desk sources: the desk is looked at on the one-second timer while the firms run, never per frame");
        Check(MethodText(firms, "private void StopFirms(bool keep, bool unloading)").Contains("DeskDown();"), "own desk sources: the firms standing down call a stint off and take the click back");

        string offer = MethodText(desk, "private void OfferDesk(BuildingRegistration reg)");
        Check(offer.Contains("Settings.CareersOwn") && offer.Contains("ReferenceEquals(DeskGame.OtherClicked, _onDesk)") && offer.Contains("DeskGame.OtherTarget = free;")
              && offer.Contains("DeskGame.OtherClicked = _onDesk;") && MethodText(desk, "private void DeskDown()").Contains("ReferenceEquals(DeskGame.OtherClicked, _onDesk)"),
            "own desk sources: a free desk is clicked as a job's desk is, and the click is cleared only by its owner, never the bank counter's");
        string clicked = MethodText(desk, "private void DeskClicked(EntityController e)");
        Check(clicked.Contains("DeskRefusal(firm, out OwnerStanding standing, out string professions)") && clicked.Contains("Notifications.Show(NotificationType.Warning, why")
              && clicked.Contains("Notifications.ShowInsufficientEnergy()") && clicked.Contains("DeskGame.WalkTo(desk,"),
            "own desk sources: a click says why not in the game's notification, or walks the player to the desk");
        Check(MethodText(desk, "private void OpenStint(FirmPlace firm, EmployeeStationController desk, OwnerStanding standing)").Contains("ShiftSkip.Offer(this, stint)"),
            "own desk sources: the stint is the game's own work panel, with skip and fast forward (Jobs/ShiftSkip)");
        string started = MethodText(desk, "private void StintStarted(OwnStint stint)");
        Check(started.Contains("if (!DeskGame.SitAt(stint.Desk))") && started.Contains("FirmsGame.Tiring(stint.Desk, true);") && started.Contains("!FirmsGame.Free(stint.Desk)"),
            "own desk sources: the player sits at the desk as the game seats its own staff (WorkActivity) and spends the desk's energy");
        Check(MethodText(desk, "private void StintWorked(OwnStint stint, int minutes)").Contains("OwnDeskRules.Credit(_firmsFile.FirmAt(stint.Place), Save.Day, minutes - stint.Credited);"),
            "own desk sources: every minute goes on the firm as the clock moves, so a save in the middle keeps it");
        string end = MethodText(desk, "private void EndStint(OwnStint stint, bool told)");
        Check(end.Contains("FirmsGame.Tiring(null, false);") && end.Contains("DeskGame.StandFrom(") && end.Contains("RestaffSoon(desk)") && end.Contains("TechBridges.OwnerWorked?.Invoke(")
              && end.Contains("\"reallife_tech_own_note_done\"") && !end.Contains("Pay(") && !end.Contains("Wage"),
            "own desk sources: the end stands the player up, gives the desk back to its own employee, tells Careers and the player, and pays nobody");
        Check(MethodText(desk, "private void WatchStint(BuildingRegistration reg, string place)").Contains("!FirmsGame.PlayerAt(stint.Desk)"),
            "own desk sources: a stint ends when the game seats the desk's scheduled employee, as its own work panel ends");
        Check(game.Contains("EnergyHelper.AddEnergySpender(DeskEnergyKey, station.workEnergyConsumption)") && game.Contains("GoingPublic.Mine(reg)")
              && deskGame.Contains("public static void Restaff(EmployeeStationController station)") && deskGame.Contains("station.UpdateEmployee();"),
            "own desk sources: the desk's own energy rate, only the player's own firm (Going Public), and the game's own seating afterwards");
        Check(MethodText(desk, "internal void OwnDeskLine(InfoBox box, FirmPlace firm)").Contains("\"reallife_tech_own_page_can\"") && page.Contains("_director.OwnDeskLine(box, firm);")
              && page.Contains("OwnDeskRules.HoursFor(firm.OwnMinutes, firm.OwnRole, kind, firm.File.K)") && page.Contains("\"reallife_tech_work_you\""),
            "own desk sources: the firm's page says the player can work there, and who worked yesterday counts them");
        Check(bridges.Contains("public static OwnerAsk Owner;") && bridges.Contains("Owner = null;") && bridges.Contains("OwnerWorked = null;")
              && links.Contains("TechBridges.Owner = Owner;") && links.Contains("TechBridges.OwnerWorked = (prof, rank, minutes) => CareersDirector.Instance?.FirmWorked(prof, rank, minutes);")
              && links.Contains("TechBridges.Owner = null;"), "own desk sources: Careers answers through a bridge, wired and taken down in Wiring");
        string standing = MethodText(careers, "internal bool FirmStanding(string type, out OwnerStanding standing, out string why, out string professions)");
        Check(standing.Contains("!_workOn || !OwnOn") && standing.Contains("OwnRules.FirmProfession(_study, type, Save.Day, Year, out int rank)") && standing.Contains("\"reallife_tech_own_why_none\""),
            "own desk sources: Careers picks the profession from what the player has learnt, or says what is missing");
        string worked = MethodText(careers, "internal void FirmWorked(int prof, int rank, int minutes)");
        Check(worked.Contains("OwnWorked(p, rank, Math.Max(1, minutes / 60), day);") && worked.Contains("Burn(w.Job, w.Job.WeekMinutes, minutes);") && worked.Contains("TestRestraint(false);")
              && !worked.Contains("Pay(") && !worked.Contains("SkillsOf("),
            "own desk sources: the hours teach and keep the trade up through the one rule of own work (OwnWorked, as a contract's do), make a job's week long and test a restraint clause, unpaid");
        Check(desk.Contains("public void TestOwnDesk()") && mod.Contains("\"reallife_tech_test_own_desk\", () => Tech.TechDirector.Instance?.TestOwnDesk()")
              && list.Contains("\"reallife_tech_test_net\", \"reallife_tech_test_own_desk\"") && desk.Contains("\"reallife_tech_test_own_none\"") && desk.Contains("\"reallife_tech_test_own_fail\""),
            "own desk sources: the test is in Options, on the tech sector's list, and says why when it cannot run");
    }

    private static void OwnDeskTexts()
    {
        string[] keys =
        {
            "reallife_tech_own_title", "reallife_tech_own_line", "reallife_tech_own_line_unpaid", "reallife_tech_own_note_done", "reallife_tech_own_why_none", "reallife_tech_own_why_off",
            "reallife_tech_own_why_shut", "reallife_tech_own_page_can", "reallife_tech_own_page_none", "reallife_tech_own_page_off", "reallife_tech_work_you", "reallife_tech_test_own_desk",
            "reallife_tech_test_own_none", "reallife_tech_test_own_fail"
        };
        Check(AllIn(keys, out string missing), "own desk texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool fits = true;
        foreach (string key in new[] { "reallife_tech_own_note_done", "reallife_tech_own_why_none", "reallife_tech_own_why_off", "reallife_tech_own_why_shut", "reallife_tech_test_own_none",
                     "reallife_tech_test_own_fail" })
        {
            fits &= Fits(en, key);
        }
        Check(fits, "own desk texts: the notices are 240 characters or fewer");
        Check(en.TryGetValue("reallife_tech_own_why_none", out string none) && none.Contains("{professions}") && en.TryGetValue("reallife_tech_own_page_can", out string can)
              && can.Contains("{role}") && can.Contains("{level}") && can.Contains("{points}") && en.TryGetValue("reallife_tech_own_note_done", out string done) && done.Contains("{time}")
              && done.Contains("{firm}") && done.Contains("{points}"), "own desk texts: the words carry what the code fills in");
        // The Guide's two pages tell of it in every language that has them; the new line sits at the same place in each.
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        var lacking = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            Dictionary<string, string> words = langs[l];
            if (words == null)
            {
                continue;
            }
            if (words.TryGetValue("reallife_guide_tech_sector", out string sector) && sector.Split('\n').Length != en["reallife_guide_tech_sector"].Split('\n').Length)
            {
                lacking.Add(codes[l] + " tech sector");
            }
            if (words.TryGetValue("reallife_guide_careers_own", out string own) && own.Split('\n').Length != en["reallife_guide_careers_own"].Split('\n').Length)
            {
                lacking.Add(codes[l] + " working for yourself");
            }
        }
        Check(lacking.Count == 0 && en["reallife_guide_tech_sector"].Contains("- Working there yourself: with a profession the firm employs, click a free desk")
              && en["reallife_guide_tech_sector"].Contains("design and IT support at a web development agency, and design at a graphic designer")
              && en["reallife_guide_careers_own"].Contains("In a tech firm of your own you can work yourself"),
            "own desk texts: the Guide's tech sector and working for yourself pages tell of the desk, which professions fit which firms, in every language (" + string.Join("; ", lacking) + ")");
        Check(FixLineEverywhere("there was no way to work in a tech firm of your own", "não havia como trabalhar numa empresa de tecnologia sua", "之前无法在你自己的科技公司里工作",
                "In einer eigenen Tech-Firma konntest du nicht selbst arbeiten", "il n'y avait aucun moyen de travailler dans une société tech à vous",
                "no había forma de trabajar en una empresa tecnológica tuya", "в собственной технологической фирме нельзя было работать самому",
                "je kon niet zelf werken in een eigen techbedrijf", "kendi teknoloji firmanızda kendiniz çalışmanın bir yolu yoktu"),
            "own desk texts: What's new tells of the fix in every language, behind its fix mark, in any row");
    }
}
