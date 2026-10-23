using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// The building trades (Scripts/BuildTrades, Jobs/Kinds/TradeJob, DesignJob, TenderJob): the catalogue of trades and what
// each asks, the courses and the three employers' ladders; the timing bar, the match, the survey, the inspection and its
// bribe, the night watch and their pay; the architecture game's plan, facade and frame and the score the construction
// company reads; the estimator's tender; the trades' line in the save; then, over the sources, the layering, the guard,
// the options, the city table, and the texts in every language.
internal static partial class Program
{
    private static void BuildTrades()
    {
        TradeCatalogueChecks();
        TradeCourseChecks();
        TradeLadderChecks();
        TradeTimingChecks();
        TradeJudgeChecks();
        TradeDesignChecks();
        TradeTenderChecks();
        TradeBookChecks();
        TradePayChecks();
        TradeSourceChecks();
        TradeTextChecks();
    }

    private static void TradeCatalogueChecks()
    {
        var ids = new HashSet<string>();
        foreach (TradeDef t in TradeCatalogue.All)
        {
            Check(ids.Add(t.Id), "trades: " + t.Id + " is listed once");
            JobDef def = JobCatalogue.Find(t.Id);
            Check(def != null && JobCatalogue.IsTrade(def) && JobCatalogue.RecordOf(t.Id) >= JobCatalogue.All.Length, "trades: " + t.Id + " is a job of the trades, recorded after the Gig Line's gigs");
            Check(JobCatalogue.IndexOf(t.Id) < 0, "trades: " + t.Id + " is not offered on the Gig Line (the day's busy and shut gigs stay the seven)");
            Check(t.AtOffice == (def != null && (def.Needs & JobNeed.Office) != 0), "trades: " + t.Id + " is an office trade exactly when its job says so");
            if (t.Needs != null)
            {
                foreach (int course in t.Needs.Courses)
                {
                    Check(StudyCatalogue.Find(course) != null, "trades: " + t.Id + " asks for a course the city teaches (" + course + ")");
                }
                Check(t.Needs.Courses.Length > 0 || t.Needs.Points > 0, "trades: what " + t.Id + " asks can be met");
                Check(!string.IsNullOrEmpty(t.Needs.LacksKey), "trades: " + t.Id + " says what it lacks");
            }
            Check(t.Mode != TradeMode.Timing && t.Mode != TradeMode.Survey || (t.Sweep > 0.5f && t.Zone > 0.1f && t.Zone < 0.5f), "trades: " + t.Id + "'s bar has a sweep and a fair zone");
            Check(t.Mode != TradeMode.Match || t.Seconds >= TradeRules.MatchLeast, "trades: " + t.Id + "'s picks have time");
            Check(t.Learns >= 0 && t.LearnEach > 0, "trades: " + t.Id + " teaches something on the job");
        }
        Check(JobCatalogue.Trades.Length == TradeCatalogue.All.Length && JobCatalogue.Recorded.Length == JobCatalogue.All.Length + JobCatalogue.Trades.Length,
            "trades: every trade a job and every job of the trades a trade; the record keeps the gigs and the trades");
        Check(TradeCatalogue.Find(JobCatalogue.Labourer).Needs == null && TradeCatalogue.Find(JobCatalogue.Mason).Needs == null, "trades: labourer and mason are open to anyone");
        // Every kind of the city's sites has work in some stage, and night security only after hours.
        for (int kind = 0; kind < 6; kind++)
        {
            int any = 0;
            for (int phase = 0; phase < 5; phase++)
            {
                any |= TradeCatalogue.OnAt(kind, phase, true);
            }
            Check(any != 0, "trades: a site of kind " + kind + " has work on in some stage");
        }
        int sec = Array.IndexOf(TradeCatalogue.All, TradeCatalogue.Find(JobCatalogue.Security));
        Check((TradeCatalogue.OnAt(4, 2, true) & (1 << sec)) == 0 && (TradeCatalogue.OnAt(4, 2, false) & (1 << sec)) != 0, "trades: night security is on only after a site's hours");
        int mason = Array.IndexOf(TradeCatalogue.All, TradeCatalogue.Find(JobCatalogue.Mason));
        Check((TradeCatalogue.OnAt(4, 2, false) & (1 << mason)) == 0, "trades: nobody lays bricks after hours");
        Check((TradeCatalogue.OnAt(0, 0, true) & (1 << mason)) == 0 && (TradeCatalogue.OnAt(5, 2, true) & (1 << mason)) != 0, "trades: masons work a rebuild's or a plot's walls, not roadworks");
        Check(TradeCatalogue.AtOffice() != 0 && (TradeCatalogue.AtOffice() & TradeCatalogue.OnAt(5, 2, true)) == 0, "trades: the office's trades are never on at a site");
        Check(TradeCatalogue.OnAt(-1, 0, true) == 0 && TradeCatalogue.OnAt(9, 0, true) == 0 && TradeCatalogue.OnAt(4, 7, true) == 0, "trades: a site kind or stage the catalogue does not know has nothing on");
        // The roles a construction company gives its people: the ten its side uses are all there, each once.
        var roles = new HashSet<string>();
        foreach (TradeRole r in TradeCatalogue.Roles)
        {
            Check(roles.Add(r.Id), "trades: the role " + r.Id + " is listed once");
        }
        foreach (string id in new[] { "labourer", "mason", "foreman", "site_manager", "architect", "engineer", "crane_operator", "electrician", "plumber", "estimator" })
        {
            Check(TradeCatalogue.Role(id) != null, "trades: the construction company's role " + id + " is one of the trades'");
        }
        Check(TradeCatalogue.Role("nobody") == null, "trades: a role the trades do not know is none");
    }

    private static void TradeCourseChecks()
    {
        int[] mine = { StudyCatalogue.Bricklaying, StudyCatalogue.CraneTicket, StudyCatalogue.ConstructionManagement, StudyCatalogue.ArchitectLicence };
        var seen = new HashSet<int>();
        foreach (CourseDef c in StudyCatalogue.All)
        {
            Check(seen.Add(c.Id) && c.Id >= 0 && c.Id < StudyCatalogue.IdsMost, "trades courses: course " + c.Id + " has an id of its own under " + StudyCatalogue.IdsMost);
        }
        foreach (int id in mine)
        {
            Check(StudyCatalogue.Find(id) != null, "trades courses: course " + id + " is taught");
        }
        CourseDef brick = StudyCatalogue.Find(StudyCatalogue.Bricklaying);
        Check(brick.SkillA == Skills.Masonry && brick.Kind == CourseKind.Vocational && brick.Campus == Campus.School, "trades courses: bricklaying is a diploma at the school that teaches masonry");
        CourseDef licence = StudyCatalogue.Find(StudyCatalogue.ArchitectLicence);
        Check(licence.Kind == CourseKind.Certificate && Array.IndexOf(licence.Needs, StudyCatalogue.Architecture) >= 0 && !licence.Timetabled,
            "trades courses: the architect's licence is an exam after the architecture degree, studied at home");
        CourseDef cm = StudyCatalogue.Find(StudyCatalogue.ConstructionManagement);
        Check(cm.Kind == CourseKind.Degree && cm.Campus == Campus.University, "trades courses: construction management is a degree at the university");
        Check(Skills.Masonry == TalentCatalogue.SkillCount + Skills.OwnKeys.Length - 1 && Skills.Count == Skills.Masonry + 1 && StudyCatalogue.SkillKey(Skills.Masonry) == "reallife_study_skill_masonry",
            "trades courses: masonry is the last skill, with its name");
        // The four come after every course before them, so a course's room at the university and a list's order stay as they were.
        int at = -1;
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            if (StudyCatalogue.All[i].Id == StudyCatalogue.Bricklaying)
            {
                at = i;
            }
        }
        Check(at == StudyCatalogue.All.Length - 4, "trades courses: the trades' courses are the catalogue's last four");
    }

    private static void TradeLadderChecks()
    {
        foreach (string id in new[] { CareerCatalogue.Builders, CareerCatalogue.Inspection, CareerCatalogue.Architects })
        {
            Employer e = CareerCatalogue.Find(id);
            Check(e != null && e.Premises.Length == 0, "trades ladders: " + id + " is an employer with no help-wanted board");
            if (e == null)
            {
                continue;
            }
            Check(JobCatalogue.Find(e.Job) != null && JobCatalogue.IsTrade(JobCatalogue.Find(e.Job)), "trades ladders: " + id + "'s own work is a trade");
            foreach (CareerRank r in e.Ranks)
            {
                Check(r.JobId == null || JobCatalogue.Find(r.JobId) != null, "trades ladders: " + r.TitleKey + "'s work is a job");
                Check(r.Needs == null || TradeCatalogue.Role(r.Needs) != null, "trades ladders: what " + r.TitleKey + " asks is a role of the trades'");
            }
        }
        Employer builders = CareerCatalogue.Find(CareerCatalogue.Builders);
        Check(builders.Ranks.Length == 4 && builders.Ranks[0].Needs == null && builders.Ranks[1].JobId == JobCatalogue.Mason && builders.Ranks[1].Needs == "mason_rank"
              && builders.Ranks[2].Work == CareerWork.Lead && builders.Ranks[3].Needs == "site_manager",
            "trades ladders: labourer (open to anyone), mason by the trade learnt, foreman in charge, site manager with a degree");
        Check(TradeCatalogue.MasonRank.Skill == Skills.Masonry && TradeCatalogue.MasonRank.Points > 0
              && TradeCatalogue.MasonRank.Points * 1000 <= 40 * JobCatalogue.Find(JobCatalogue.Labourer).ShiftUnits * TradeCatalogue.Find(JobCatalogue.Labourer).LearnEach,
            "trades ladders: the apprenticeship: forty labourer's shifts at most teach the mason's rank");
        Employer practice = CareerCatalogue.Find(CareerCatalogue.Architects);
        Check(practice.Ranks[0].Needs == null && practice.Ranks[1].Needs == "architect" && practice.Ranks[2].Needs == "licensed_architect" && practice.Ranks[3].Needs == "licensed_architect",
            "trades ladders: draughtsperson for anyone, junior architect by the degree, architect and partner by the licence");
        Employer control = CareerCatalogue.Find(CareerCatalogue.Inspection);
        Check(control.Ranks[0].Needs == "inspector", "trades ladders: the city takes on inspectors with a degree in building only");
        for (int i = 1; i < builders.Ranks.Length; i++)
        {
            Check(builders.Ranks[i].Hourly > builders.Ranks[i - 1].Hourly && practice.Ranks[i].Hourly > practice.Ranks[i - 1].Hourly, "trades ladders: each rank pays more basic than the one below (" + i + ")");
        }
        // The professions' architect signs off plans with the licence only.
        Profession arch = ProCatalogue.Find(ProCatalogue.Architect);
        for (int r = 1; r < arch.Ranks.Length; r++)
        {
            Check(Array.IndexOf(arch.Ranks[r].Quals, StudyCatalogue.ArchitectLicence) >= 0, "trades ladders: the profession's architect rank " + r + " asks for the licence");
        }
        Check(arch.Ranks[0].Quals.Length == 1 && arch.Ranks[0].Quals[0] == StudyCatalogue.Architecture, "trades ladders: the first rung is the degree alone");
        // What a need holds against.
        var passed = new HashSet<int>();
        int points = 0;
        Func<int, bool> has = c => passed.Contains(c);
        Func<int, int> pts = s => s == Skills.Masonry ? points : 0;
        Check(!TradeRules.Holds(TradeCatalogue.MasonRank, has, pts), "trades ladders: no course and no masonry: not a mason");
        points = TradeCatalogue.MasonRank.Points;
        Check(TradeRules.Holds(TradeCatalogue.MasonRank, has, pts), "trades ladders: the masonry learnt as a labourer makes a mason");
        points = 0;
        passed.Add(StudyCatalogue.Bricklaying);
        Check(TradeRules.Holds(TradeCatalogue.MasonRank, has, pts), "trades ladders: the bricklaying diploma makes a mason");
        Check(!TradeRules.Holds(TradeCatalogue.ManagerRank, has, pts), "trades ladders: a diploma is no site manager");
        passed.Add(StudyCatalogue.ConstructionManagement);
        Check(TradeRules.Holds(TradeCatalogue.ManagerRank, has, pts) && TradeRules.Holds(TradeCatalogue.Building, has, pts), "trades ladders: the degree makes a site manager and an inspector");
        Check(TradeRules.Holds(null, null, null), "trades ladders: what asks nothing is held without a file");
        Check(!TradeRules.Holds(TradeCatalogue.Ticket, null, null), "trades ladders: without the player's file nothing that asks something is held");
    }

    private static void TradeTimingChecks()
    {
        TradeDef mason = TradeCatalogue.Find(JobCatalogue.Mason);
        bool inRange = true, swingRange = true;
        for (int i = 0; i <= 4000; i++)
        {
            float t = i * 0.0137f;
            float m = TradeRules.Marker(t, mason.Sweep), s = TradeRules.Swing(t, 2.6f);
            inRange &= m >= 0f && m <= 1f;
            swingRange &= s >= 0f && s <= 1f;
        }
        Check(inRange && swingRange, "trades timing: the marker and the swing stay on the bar");
        Check(Math.Abs(TradeRules.Marker(0f, 1.5f)) < 0.001f && Math.Abs(TradeRules.Marker(0.75f, 1.5f) - 1f) < 0.001f && Math.Abs(TradeRules.Marker(1.5f, 1.5f)) < 0.001f,
            "trades timing: the marker crosses the bar and comes back once a sweep");
        Check(Math.Abs(TradeRules.Marker(3.3f, 1.5f) - TradeRules.Marker(0.3f, 1.5f)) < 0.001f, "trades timing: the sweep repeats");
        Check(TradeRules.Swing(0f, 2f) < 0.001f && Math.Abs(TradeRules.Swing(1f, 2f) - 1f) < 0.001f, "trades timing: the crane's load swings from one end to the other");
        Check(TradeRules.Marker(1f, 0f) == 0.5f, "trades timing: a bar with no sweep stands in the middle");
        float w0 = TradeRules.ZoneWidth(mason, 0), w50 = TradeRules.ZoneWidth(mason, 50), w100 = TradeRules.ZoneWidth(mason, 100), wOver = TradeRules.ZoneWidth(mason, 500);
        Check(w0 == mason.Zone && w50 > w0 && w100 >= w50 && w100 <= TradeRules.WidestZone && wOver == w100, "trades timing: the skill widens the zone, to a limit");
        var dice = new TradeDice(77);
        bool onBar = true;
        for (int i = 0; i < 10000; i++)
        {
            float w = TradeRules.ZoneWidth(mason, i % 101);
            float c = TradeRules.ZoneCentre(ref dice, w);
            onBar &= c - w * 0.5f >= 0f && c + w * 0.5f <= 1f;
        }
        Check(onBar, "trades timing: every zone drawn stands whole on the bar");
        Check(TradeRules.Press(0.5f, 0.5f, 0.2f) == StrokeGrade.Best && TradeRules.Press(0.59f, 0.5f, 0.2f) == StrokeGrade.Fair && TradeRules.Press(0.61f, 0.5f, 0.2f) == StrokeGrade.Miss,
            "trades timing: the middle of the zone is best, its edge fair, outside a miss");
        Check(TradeRules.Worth(StrokeGrade.Best, 0) == 1f && TradeRules.Worth(StrokeGrade.Fair, 0) == TradeRules.FairWorth && TradeRules.Worth(StrokeGrade.Best, 1) == TradeRules.RedoWorth
              && TradeRules.Worth(StrokeGrade.Miss, 2) == 0f, "trades timing: a stroke done again is worth less, one laid as it lies nothing");
        Check(TradeRules.Quality(8f, 8) == 1f && TradeRules.Quality(0f, 8) == 0f && TradeRules.Quality(4f, 0) == 0f, "trades timing: a unit's quality from its strokes");
        Check(TradeRules.UnitPay(100f, 0f) == 40 && TradeRules.UnitPay(100f, 1f) == 100 && TradeRules.UnitPay(100f, 2f) == 100 && TradeRules.UnitPay(100f, 0.5f) == 70,
            "trades timing: a unit pays from two fifths done badly to all of it done perfectly");
        Check(TradeRules.TrueBonus(200, 6, 6, 6) == 20 && TradeRules.TrueBonus(200, 6, 6, 5) == 0 && TradeRules.TrueBonus(200, 5, 6, 5) == 0, "trades timing: the whole shift perfect earns a tenth more");
        // The match
        TradeDef sparks = TradeCatalogue.Find(JobCatalogue.Electrician);
        Check(TradeRules.PickSeconds(sparks, 0, 0) == sparks.Seconds && TradeRules.PickSeconds(sparks, 100, 0) == TradeRules.MatchLeast && TradeRules.PickSeconds(sparks, 0, 100) > sparks.Seconds,
            "trades match: a pick's time shortens through the shift, never under its least, and the skill gives more");
        Check(TradeRules.Pick(1, 1, 0.5f, 4f) == StrokeGrade.Best && TradeRules.Pick(1, 1, 3f, 4f) == StrokeGrade.Fair && TradeRules.Pick(0, 1, 0.5f, 4f) == StrokeGrade.Miss
              && TradeRules.Pick(1, 1, 4.1f, 4f) == StrokeGrade.Miss && TradeRules.Pick(-1, 1, 5f, 4f) == StrokeGrade.Miss, "trades match: right and quick is best, right fair, wrong or late a miss");
        var counts = new int[3];
        var pd = new TradeDice(5);
        for (int i = 0; i < 9000; i++)
        {
            counts[TradeRules.PickRight(ref pd)]++;
        }
        Check(counts[0] > 2700 && counts[1] > 2700 && counts[2] > 2700, "trades match: each answer is asked for about as often: " + counts[0] + ", " + counts[1] + ", " + counts[2]);
        // The survey and the watch
        TradeRules.Corner(0, 20f, 10f, out float x0, out float z0);
        TradeRules.Corner(2, 20f, 10f, out float x2, out float z2);
        TradeRules.Corner(5, 20f, 10f, out float x5, out float z5);
        Check(x0 == -10f && z0 == -5f && x2 == 10f && z2 == 5f && x5 == 10f && z5 == -5f, "trades survey: the four corners of the land, front first, and round again");
        int noises = 0;
        bool same = true;
        for (int r = 0; r < 4000; r++)
        {
            bool n = TradeRules.Noise(99u, r, out int post);
            same &= n == TradeRules.Noise(99u, r, out int again) && post == again && post >= 0 && post < TradeRules.Posts;
            noises += n ? 1 : 0;
        }
        Check(same && Math.Abs(noises / 4000f - TradeRules.NoiseChance) < 0.03f, "trades watch: a round's noise and its post are the same on every look, about " + TradeRules.NoiseChance + " of rounds: " + noises);
        Check(TradeRules.RoundWorth(false, false) == 1f && TradeRules.RoundWorth(true, true) == 2f && TradeRules.RoundWorth(true, false) == 0.5f, "trades watch: a thief caught counts double, one got away half");
        TradeRules.Post(1, out float px, out float pz);
        Check(px == TradeRules.PostOut && pz == 0f, "trades watch: the posts stand either side of the site and behind it");
    }

    private static void TradeJudgeChecks()
    {
        var kinds = new int[TradeRules.Items];
        var faults = new bool[TradeRules.Items];
        var kinds2 = new int[TradeRules.Items];
        var faults2 = new bool[TradeRules.Items];
        bool distinct = true, repeat = true, readings = true, unsafeMost = true;
        int faultTotal = 0;
        for (int site = 1; site < 300; site++)
        {
            bool unsafeSite = site % 5 == 0;
            TradeRules.Checklist(site, 40 + site % 7, unsafeSite, kinds, faults);
            TradeRules.Checklist(site, 40 + site % 7, unsafeSite, kinds2, faults2);
            int count = 0, used = 0;
            for (int i = 0; i < TradeRules.Items; i++)
            {
                repeat &= kinds[i] == kinds2[i] && faults[i] == faults2[i];
                distinct &= (used & (1 << kinds[i])) == 0 && kinds[i] >= 0 && kinds[i] < TradeRules.ItemKinds;
                used |= 1 << kinds[i];
                count += faults[i] ? 1 : 0;
                TradeRules.Reading(site, 40, i, kinds[i], faults[i], out int measured, out int limit, out bool atLeast);
                readings &= TradeRules.Sound(measured, limit, atLeast) == !faults[i] && measured >= 0 && limit > 0;
            }
            unsafeMost &= !unsafeSite || count >= TradeRules.UnsafeFaults;
            faultTotal += unsafeSite ? 0 : count;
        }
        Check(repeat, "trades inspection: a site's checklist is the same for everyone who looks on that day");
        Check(distinct, "trades inspection: no kind of item twice on a checklist");
        Check(readings, "trades inspection: every reading says what it is: under or over its limit exactly when it is at fault");
        Check(unsafeMost, "trades inspection: an unsafe site has at least " + TradeRules.UnsafeFaults + " faults to find");
        float share = faultTotal / (240f * TradeRules.Items);
        Check(Math.Abs(share - TradeRules.FaultShare) < 0.05f, "trades inspection: about " + TradeRules.FaultShare + " of a safe site's items are at fault: " + share.ToString("0.00"));
        int offered = 0, caught = 0;
        for (int i = 0; i < 4000; i++)
        {
            offered += TradeRules.BribeOffered(i, 10, 2) ? 1 : 0;
            caught += TradeRules.BribeCaught(i, 10) ? 1 : 0;
        }
        Check(Math.Abs(offered / 4000f - TradeRules.BribeShare) < 0.03f && Math.Abs(caught / 4000f - TradeRules.CaughtShare) < 0.03f,
            "trades inspection: bribes are offered and come out about as often as the rules say: " + offered + ", " + caught);
        Check(!TradeRules.BribeOffered(3, 10, 0), "trades inspection: no bribe for a site with no faults found");
        Check(TradeRules.BribeOffered(17, 3, 2) == TradeRules.BribeOffered(17, 3, 2) && TradeRules.BribeCaught(17, 3) == TradeRules.BribeCaught(17, 3), "trades inspection: the same answer on every machine");
        Check(TradeRules.Bribe(55f, 8) == (int)Math.Round(55f * 8 * TradeRules.BribeShifts), "trades inspection: a bribe is worth two and a half shifts");
    }

    private static DesignRoom[] PlanCells(DesignBrief b, DesignRoom[] order, int count, int[] starts, ref int lobby, params int[] at)
    {
        var cells = new DesignRoom[DesignRules.Cells];
        var places = new int[DesignRules.Cells];
        if (b.Simple)
        {
            cells[DesignRules.SimpleStairs] = DesignRoom.Stairs;
        }
        for (int i = 0; i < count; i++)
        {
            starts[i] = -1;
            if (i < at.Length && at[i] >= 0)
            {
                int n = DesignRules.Places(order[i], cells, places);
                bool ok = Array.IndexOf(places, at[i], 0, n) >= 0;
                if (ok)
                {
                    DesignRules.Put(order[i], at[i], cells, ref lobby);
                    starts[i] = at[i];
                }
            }
        }
        return cells;
    }

    private static void TradeDesignChecks()
    {
        // The brief
        bool same = true, fits = true;
        for (uint seed = 1; seed < 400; seed++)
        {
            DesignBrief a = DesignRules.Draw(seed, seed % 3 == 0), b = DesignRules.Draw(seed, seed % 3 == 0);
            same &= a.Client == b.Client && a.Storeys == b.Storeys && a.Budget == b.Budget && a.Loves == b.Loves && a.Hates == b.Hates;
            fits &= a.FlatCells <= 8 && a.Budget > 0 && a.FacadeBudget > 0 && a.Loves / DesignRules.Options != a.Hates / DesignRules.Options;
        }
        Check(same, "trades design: a brief drawn from the same seed is the same brief");
        Check(fits, "trades design: every brief's flats fit the two lit rows, it has budgets, and the client loves and hates different things");
        DesignBrief p1 = DesignRules.ForProject("build:12", 5, 18, 4000000), p2 = DesignRules.ForProject("build:12", 5, 18, 4000000);
        Check(p1.Client == p2.Client && p1.Budget == p2.Budget && p1.Storeys == 5 && p1.Commission == "build:12", "trades design: a builder's project gives the same brief every time it is asked");
        Check(DesignRules.Hash("build:12") == DesignRules.Hash("build:12") && DesignRules.Hash("build:12") != DesignRules.Hash("build:13") && DesignRules.Hash("") == 2166136261u,
            "trades design: the project's hash is the same in every build");
        Check(DesignRules.ForProject("build:1", 1, 0, 0).Storeys >= 2 && DesignRules.ForProject("build:1", 40, 900, 1).Storeys <= 10, "trades design: a project's odd figures are kept to a building");

        // A good plan: the cores in the middle row, every flat on a lit row beside them, the lobby under a core.
        var brief = new DesignBrief { Client = 1, Storeys = 4, Studios = 2, TwoBeds = 0, Families = 2, Lift = true };
        brief.Budget = DesignRules.Reference(brief) * 110 / 100;   // as a drawn brief's: a tenth over the plan with no corridor at all
        brief.FacadeBudget = DesignRules.FacadeReference(brief);
        var order = new DesignRoom[12];
        int count = DesignRules.Order(brief, order);
        Check(count == 7 && order[0] == DesignRoom.Stairs && order[1] == DesignRoom.Lift && order[2] == DesignRoom.Lobby && order[3] == DesignRoom.Family && order[6] == DesignRoom.Studio,
            "trades design: the stairs, the lift and the lobby first, then the flats from the largest");
        var starts = new int[12];
        int lobby = -1;
        // stairs at middle bay 2 (cell 5), lift at middle bay 3 (cell 6), lobby under bay 2 (cell 1), families at front 0..2 and back 9..11, studios at front 3 and back 8
        DesignRoom[] cells = PlanCells(brief, order, count, starts, ref lobby, 5, 6, 1, 0, 9, 3, 8);
        DesignScore good = DesignRules.Score(brief, order, count, starts, cells, lobby);
        Check(good.Fit == 100 && good.Light == 100 && good.Cost == 100, "trades design: the good plan fits every room in daylight within the budget: " + good.Write());
        Check(good.Flow >= 80 && good.Plan >= 90, "trades design: and its flats are near the stairs: flow " + good.Flow + ", plan " + good.Plan);
        Check(good.Units == 4 * 3 + 3 && good.Storeys == 4, "trades design: four flats on each upper floor and three on the ground: " + good.Units);
        // A plan with the flats in the middle row is dark.
        DesignRoom[] darkCells = PlanCells(brief, order, count, starts, ref lobby, 0, 3, 0, 4, 9, 7, 8);
        DesignScore dark = DesignRules.Score(brief, order, count, starts, darkCells, lobby);
        Check(dark.Light < good.Light, "trades design: flats in the middle row lose their daylight: " + dark.Light);
        // A flat left out: less fit, less cost.
        int lobby2 = -1;
        DesignRoom[] partCells = PlanCells(brief, order, count, starts, ref lobby2, 5, 6, 1, 0, -1, 3, 8);
        DesignScore part = DesignRules.Score(brief, order, count, starts, partCells, lobby2);
        Check(part.Fit < 100 && part.Fit > 50 && part.CostPermille < good.CostPermille, "trades design: a family flat left out costs fit and saves money: " + part.Write());
        Check(part.Units < good.Units, "trades design: and gives fewer flats");
        // No stairs: not a building.
        int lobby3 = -1;
        DesignRoom[] noStairs = PlanCells(brief, order, count, starts, ref lobby3, -1, 6, 1, 0, 9, 3, 8);
        Check(DesignRules.Score(brief, order, count, starts, noStairs, lobby3).Fit <= 30, "trades design: a plan with no stairs fits nothing");
        Check(DesignRules.CostScore(1000) == 100 && DesignRules.CostScore(1100) == 60 && DesignRules.CostScore(1300) == 0 && DesignRules.CostScore(800) == 100,
            "trades design: the cost scores 100 within the budget, four points a per cent over");
        var places = new int[DesignRules.Cells];
        Check(DesignRules.Places(DesignRoom.Family, new DesignRoom[DesignRules.Cells], places) == 6 && DesignRules.Places(DesignRoom.Studio, new DesignRoom[DesignRules.Cells], places) == 12
              && DesignRules.Places(DesignRoom.Lobby, cells, places) == 4, "trades design: a family flat has six places on an empty floor, a studio twelve, the lobby the four front bays");
        Check(DesignRules.Places(DesignRoom.Family, cells, places) == 0, "trades design: a full floor has no place for another flat");
        int l = -1;
        var take = new DesignRoom[DesignRules.Cells];
        DesignRules.Put(DesignRoom.TwoBed, 4, take, ref l);
        DesignRules.Take(DesignRoom.TwoBed, 4, take, ref l);
        Check(take[4] == DesignRoom.Empty && take[5] == DesignRoom.Empty, "trades design: a room taken back leaves its cells free");
        // The draughtsperson's sheet: the stairs drawn, three flats.
        DesignBrief sheet = DesignRules.Draw(9u, true);
        var sheetOrder = new DesignRoom[12];
        int sheetCount = DesignRules.Order(sheet, sheetOrder);
        Check(sheet.Simple && sheet.Flats == 3 && sheetCount == 3 && DesignRules.Flat(sheetOrder[0]), "trades design: a sheet is three flats with the stairs drawn already");

        // The facade
        int[] choice = new int[DesignRules.Elements];
        int best = DesignRules.BestSatisfaction(brief);
        bool inRange = true, never = true;
        for (int code = 0; code < 256; code++)
        {
            choice[0] = code & 3;
            choice[1] = (code >> 2) & 3;
            choice[2] = (code >> 4) & 3;
            choice[3] = (code >> 6) & 3;
            int s = DesignRules.Satisfaction(brief, choice, out int cost);
            inRange &= s >= 0 && s <= 100 && cost > 0;
            never &= s <= best;
        }
        Check(inRange && never && best > 50, "trades design: satisfaction runs 0 to 100 and no facade beats the best: " + best);
        var cheap = new DesignBrief { Client = 3, Storeys = 6, FacadeBudget = 1 };
        int[] stone = { 2, 2, 2, 2 };
        cheap.Loves = -1;
        cheap.Hates = -1;
        int over = DesignRules.Satisfaction(cheap, stone, out _);
        cheap.FacadeBudget = 10000;
        Check(DesignRules.Satisfaction(cheap, stone, out _) > over, "trades design: a facade over its budget pleases less");
        Check(DesignRules.FacadeCost(0, 0, 4) == 4 * DesignRules.FacadeCost(0, 0, 1) && DesignRules.FacadeCost(1, 0, 4) == DesignRules.FacadeCost(1, 0, 1), "trades design: the walls cost by the storey, the rest once");
        var loving = new DesignBrief { Client = 0, Loves = 2, Hates = 7 };
        var plain = new DesignBrief { Client = 0, Loves = -1, Hates = -1 };
        Check(DesignRules.TasteOf(loving, 0, 2) == DesignRules.TasteOf(plain, 0, 2) + 2 && DesignRules.TasteOf(loving, 1, 3) == DesignRules.TasteOf(plain, 1, 3) - 3,
            "trades design: what the client said they love and hate moves their taste");

        // The frame
        var columns = new bool[DesignRules.Lines];
        Check(DesignRules.LongestSpan(columns) == DesignRules.Lines - 1 && !DesignRules.Holds(columns, 2), "trades design: no columns between the ends: an 18 m span holds on no slab");
        for (int i = 1; i < DesignRules.Lines - 1; i++)
        {
            columns[i] = DesignRules.OnWall(i);
        }
        Check(DesignRules.LongestSpan(columns) == 3 && DesignRules.Holds(columns, 0), "trades design: a column on every wall: 4.5 m spans hold on a thin slab");
        int cheapest = DesignRules.CheapestFrame(5);
        Check(cheapest > 0 && cheapest <= DesignRules.FrameCost(columns, 0, 5), "trades design: the cheapest frame costs no more than a column on every wall");
        Check(DesignRules.FrameScore(columns, 0, 5, cheapest) == (int)Math.Round(100.0 * cheapest / DesignRules.FrameCost(columns, 0, 5)), "trades design: a frame scores by its cost against the cheapest");
        var mid = (bool[])columns.Clone();
        mid[4] = true;
        Check(DesignRules.FrameScore(mid, 0, 5, cheapest) < DesignRules.FrameScore(columns, 0, 5, cheapest), "trades design: a column in the middle of a room costs points");
        var none = new bool[DesignRules.Lines];
        Check(DesignRules.FrameScore(none, 0, 5, cheapest) == 0, "trades design: a frame that does not hold scores nothing");
        // Find the cheapest by hand: it must score 100.
        bool found = false;
        var trial = new bool[DesignRules.Lines];
        for (int code = 0; code < 1 << (DesignRules.Lines - 2) && !found; code++)
        {
            bool clear = true;
            for (int i = 1; i < DesignRules.Lines - 1; i++)
            {
                trial[i] = (code & (1 << (i - 1))) != 0;
                clear &= !trial[i] || DesignRules.OnWall(i);
            }
            for (int slab = 0; slab < DesignRules.Slabs && clear && !found; slab++)
            {
                found = DesignRules.Holds(trial, slab) && DesignRules.FrameCost(trial, slab, 5) == cheapest && DesignRules.FrameScore(trial, slab, 5, cheapest) == 100;
            }
        }
        Check(found, "trades design: the cheapest frame that holds scores 100");

        // The whole: the parts done, renormalised; the score's line read back.
        var score = new DesignScore { Fit = 100, Light = 100, Flow = 100, Cost = 100, Facade = -1, Structure = -1, Storeys = 5, Units = 18, CostPermille = 980 };
        Check(DesignRules.Total(score) == 100, "trades design: a perfect plan alone totals 100");
        score.Facade = 0;
        Check(DesignRules.Total(score) == (int)Math.Round(100 * 0.6 / 0.85), "trades design: a facade at nought pulls the total down by its share");
        score.Structure = 100;
        score.Total = DesignRules.Total(score);
        Check(DesignScore.Read(score.Write(), out DesignScore back) && back.Total == score.Total && back.Facade == 0 && back.Structure == 100 && back.CostPermille == 980 && back.Units == 18,
            "trades design: the score's line reads back as written: " + score.Write());
        Check(!DesignScore.Read("d2|1|2", out _) && !DesignScore.Read(null, out _) && !DesignScore.Read("d1|x|1|1|1|1|1|1|1|1|1", out _) && !DesignScore.Read("d1|900|1|1|1|1|1|1|1|1|1", out _),
            "trades design: a line that is not a score is refused");
        Check(DesignRules.CostWith(brief, 1000, brief.FacadeBudget) == 1000, "trades design: the plan and the facade at their budgets come to the whole budget");
        Check(DesignRules.Quality(50, 100) == 0.5f && DesignRules.Quality(120, 100) == 1f && DesignRules.Quality(10, 0) == 1f, "trades design: a stage's quality against its best");
        // Timing: a frame's cheapest is found when the stage begins; a facade's best likewise.
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 40; i++)
        {
            DesignRules.CheapestFrame(3 + i % 5);
            DesignRules.BestSatisfaction(brief);
        }
        watch.Stop();
        Check(watch.Elapsed.TotalMilliseconds < 2000.0, "trades design: forty stages begun in under 2 s: " + watch.Elapsed.TotalMilliseconds.ToString("0") + " ms");
    }

    private static void TradeTenderChecks()
    {
        var items = new TenderItem[TenderRules.Items];
        var again = new TenderItem[TenderRules.Items];
        bool same = true, trueThere = true, distinct = true, moved = true;
        for (uint seed = 1; seed < 500; seed++)
        {
            TenderRules.Draw(seed, items);
            TenderRules.Draw(seed, again);
            int used = 0;
            for (int i = 0; i < TenderRules.Items; i++)
            {
                same &= items[i].TrueCents == again[i].TrueCents && items[i].Right == again[i].Right && items[i].Rate0 == again[i].Rate0;
                trueThere &= items[i].Rate(items[i].Right) == items[i].TrueCents && items[i].Rate((items[i].Right + 1) % 3) != items[i].TrueCents
                             && items[i].Rate((items[i].Right + 2) % 3) != items[i].TrueCents;
                distinct &= (used & (1 << items[i].Kind)) == 0;
                used |= 1 << items[i].Kind;
                int expected = Math.Max(50, ((int)Math.Round(items[i].LastCents * (100 + items[i].MovePercent) / 100.0) + 25) / 50 * 50);
                moved &= items[i].TrueCents == expected;
            }
        }
        Check(same, "trades tender: a tender drawn from the same seed is the same");
        Check(trueThere, "trades tender: the true rate is one of the three offered, at its place, and the other two differ from it");
        Check(distinct, "trades tender: no item twice on a tender");
        Check(moved, "trades tender: the true rate is the last one paid moved by the market, to the half unit");
        TenderRules.Draw(42u, items);
        var picks = new int[TenderRules.Items];
        for (int i = 0; i < picks.Length; i++)
        {
            picks[i] = items[i].Right;
        }
        long cost = TenderRules.TrueTotal(items);
        Check(TenderRules.Bid(items, picks, 0) == cost && TenderRules.Bid(items, picks, 8) == cost + cost * 8 / 100, "trades tender: the true rates at no margin come to the true cost; a margin goes on top");
        long rival = TenderRules.Rival(42u, cost);
        Check(rival >= (long)(cost * TenderRules.RivalLow) - 1 && rival <= (long)(cost * TenderRules.RivalHigh) + 1, "trades tender: the rival bids a little over the true cost");
        Check(TenderRules.Quality(items, picks, true) == 1f && TenderRules.Quality(items, picks, false) == 0.8f, "trades tender: every rate true is a perfect tender, four fifths of it lost");
        Check(TenderRules.Earns(cost + 1, cost + 2, cost) && !TenderRules.Earns(cost - 1, cost + 2, cost) && !TenderRules.Earns(cost + 3, cost + 2, cost),
            "trades tender: won at a profit earns the bonus; won at a loss or lost does not");
        var unpriced = new int[TenderRules.Items];
        for (int i = 0; i < unpriced.Length; i++)
        {
            unpriced[i] = -1;
        }
        Check(TenderRules.Bid(items, unpriced, 0) > cost && TenderRules.Quality(items, unpriced, false) == 0f, "trades tender: an item left unpriced goes in at its dearest and is worth nothing");
    }

    private static void TradeBookChecks()
    {
        DesignBook.Read(null);
        Check(DesignBook.Loaded && DesignBook.Count == 0 && !DesignBook.Latest(out _), "trades book: an empty save has an empty book");
        DesignBook.Commission("build:3", 5, 18, 400000);
        DesignBook.Commission("build:3", 6, 20, 500000);
        Check(DesignBook.Waiting == 1 && DesignBook.Next.Storeys == 6, "trades book: a project's brief asked for again replaces the first");
        Check(!DesignBook.For("build:3", out _), "trades book: a project has no design until the player finishes one");
        var score = new DesignScore { Fit = 90, Light = 80, Cost = 100, Flow = 70, Facade = 60, Structure = -1, Storeys = 6, Units = 20, CostPermille = 990 };
        score.Total = DesignRules.Total(score);
        DesignBook.Finished("build:3", 12, score);
        Check(DesignBook.Waiting == 0 && DesignBook.For("build:3", out DesignScore got) && got.Total == score.Total, "trades book: a finished design is the project's, and its brief is taken off");
        for (int i = 0; i < 12; i++)
        {
            DesignBook.Finished("own:" + i, 13 + i, score);
        }
        Check(DesignBook.Count == DesignBook.DesignsMost && !DesignBook.For("build:3", out _) && DesignBook.For("own:11", out _), "trades book: the last eight designs are kept");
        DesignBook.Commission("build,~|odd", 3, 6, 100);
        DesignBook.Bribed(20, 7, 300);
        DesignBook.TakeOwn();
        string line = DesignBook.Write();
        int own = DesignBook.OwnCount;
        Check(DesignBook.Read(line) && DesignBook.Count == DesignBook.DesignsMost && DesignBook.Waiting == 1 && DesignBook.BribeDay == 20 && DesignBook.BribeAmount == 300
              && DesignBook.OwnCount == own && DesignBook.Next.Id == "build___odd", "trades book: the line reads back as written, an odd project id kept to its own field");
        Check(!DesignBook.BribeDue(22) && DesignBook.BribeDue(20 + DesignBook.BribeComesOutDays), "trades book: a bribe is settled " + DesignBook.BribeComesOutDays + " days after it is taken");
        DesignBook.BribeDone();
        Check(DesignBook.BribeDay == -1 && !DesignBook.BribeDue(1000), "trades book: a settled bribe is gone");
        Check(!DesignBook.Read("2~h,1,2,3,4") && DesignBook.Count == 0 && DesignBook.Loaded, "trades book: a newer line is not read (it is kept as it was by the director)");
        Check(!DesignBook.Read("1~zz,1"), "trades book: a damaged line is not read");
        DesignBook.Reset();
    }

    private static void TradePayChecks()
    {
        // A game hour of each trade at its pay, its shift of four game hours, at full quality.
        foreach (JobDef def in JobCatalogue.Trades)
        {
            float hour = def.UnitPay * def.ShiftUnits / 4f;
            TradeDef t = TradeCatalogue.Find(def.Id);
            float least = t.Needs == null ? 40f : 60f;
            Check(hour >= least && hour <= 130f, "trades pay: " + def.Id + " pays " + hour.ToString("0") + " a game hour at its best, between " + least + " and 130");
            Check(def.DailyUnits >= def.ShiftUnits && def.UnitEnergy > 0f && def.UnitsPerLevel > 0, "trades pay: " + def.Id + " has a day's work, an energy cost and levels");
        }
        float labourer = JobCatalogue.Find(JobCatalogue.Labourer).UnitPay * JobCatalogue.Find(JobCatalogue.Labourer).ShiftUnits;
        float mason = JobCatalogue.Find(JobCatalogue.Mason).UnitPay * JobCatalogue.Find(JobCatalogue.Mason).ShiftUnits;
        float architect = JobCatalogue.Find(JobCatalogue.Architect).UnitPay * 2f;
        Check(mason > labourer && architect > mason, "trades pay: a mason's shift pays more than a labourer's, an architect's commission more than both");
    }

    private static string TradeCode(string source) => Regex.Replace(source, @"//[^\n]*", string.Empty);

    private static void TradeSourceChecks()
    {
        string[] trades = { "TradeCatalogue.cs", "TradeRules.cs", "DesignRules.cs", "TenderRules.cs", "DesignBook.cs", "TradesBridges.cs", "TradesDirector.cs", "TradesDirector.Tests.cs",
            "TradeOffice.cs", "SiteWork.cs", "TradesGame.cs", "TradesGuide.cs" };
        string[] kinds = { "TradeJob.cs", "TradeJob.Timing.cs", "TradeJob.Match.cs", "TradeJob.Judge.cs", "TradeJob.Walk.cs", "TradeJob.Modes.cs", "DesignJob.cs", "DesignJob.List.cs", "TenderJob.cs" };
        var code = new Dictionary<string, string>();
        foreach (string name in trades)
        {
            code[name] = ModSource(@"BuildTrades\" + name);
            Check(code[name].Length > 0, "trades sources: " + name + " is there");
        }
        foreach (string name in kinds)
        {
            code[name] = ModSource(@"Jobs\Kinds\" + name);
            Check(code[name].Length > 0, "trades sources: " + name + " is there");
        }
        string all = TradeCode(string.Join("\n", code.Values));
        Check(!Regex.IsMatch(all.Replace("FloorsDirector.Where(", "FloorsDirector.Place("), @"using System\.Linq|\.Where\(|\.Select\(|\.OrderBy\(|\.ToList\(\)|\.ToArray\(\)"),
            "trades sources: no LINQ (FloorsDirector.Where is the floors' own)");
        Check(!Regex.IsMatch(all, @"FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|OverlapSphere\(|RaycastAll\("), "trades sources: no whole-scene search and no physics query that allocates");
        Check(!Regex.IsMatch(all, @"void Update\(\)|void LateUpdate\(\)|void FixedUpdate\(\)"), "trades sources: no Update: a one-second timer and the jobs' tick and frame");
        Check(!Regex.IsMatch(all, @"\bHarmony\b|\bMPClient\b|\bMPServer\b|\bSend\(|DlcIsOwned|requiredDLC|SteamApps"), "trades sources: no patch, nothing sent over the network, no DLC read");
        foreach (string name in new[] { "TradeCatalogue.cs", "TradeRules.cs", "DesignRules.cs", "TenderRules.cs", "DesignBook.cs" })
        {
            Check(!Regex.IsMatch(TradeCode(code[name]), @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\."), "trades sources: " + name + " is game-free and draws only from its seed");
        }
        string director = code["TradesDirector.cs"];
        Check(director.Contains("StartCoroutine(Guard.Routine(Look()))") && director.Contains("new WaitForSecondsRealtime(1f)"), "trades sources: the director's timer is the guard's coroutine, once a second");
        Check(director.Contains("FloorsDirector.Ask(_office)") && director.Contains("FloorsDirector.Release(_office, \"its option is off\")") && code["TradeOffice.cs"].Contains(": FloorPlace"),
            "trades sources: the practice is a place on a floor of the floors core, asked and let go with its option, never a building of its own");
        Check(director.Contains("SaveHook.Keep(_onSave)") && director.Contains("SaveLines.Unreadable(save.modData, DesignBook.Key"), "trades sources: the trades' line is written at the save and kept as read when it cannot be read");
        Check(MemberText(director, "private void Bribe(int day)").Contains("TradeRules.BribeCaught(DesignBook.BribeSite, DesignBook.BribeDay)"), "trades sources: a bribe comes out by the rule, the same on every look");
        string judge = code["TradeJob.Judge.cs"];
        Check(judge.Contains("TradesBridges.Report(_site.Id)") && MemberText(judge, "private void Verdict()").Contains("_site.Unsafe"),
            "trades sources: only an unsafe site is reported, through the city's own report (Going Public: a guest's report is the host's to decide)");
        string trade = code["TradeJob.cs"];
        Check(trade.Contains("public override void Frame(float now)") && MemberText(trade, "public override void Frame(float now)").Contains("_bar.Mark(MarkerNow(now))"),
            "trades sources: the marker is written in the frame, one number on a visible step");
        Check(trade.Contains("Ctx.Uniform.Wear(Ctx.Player, _trade.Painter ? CityLook.Painter : CityLook.RoadWorker)"), "trades sources: the trades wear the game's own clothes in the city's hi-vis or painter's colours");
        Check(MemberText(trade, "public override void End(JobEnd why)").Contains("Ctx.Stand.Clear()") && MemberText(trade, "public override void End(JobEnd why)").Contains("_clock?.Close()"),
            "trades sources: every way out of a trade's shift lets the player go and closes the game's panel");
        foreach (string name in new[] { "DesignJob.cs", "TenderJob.cs" })
        {
            Check(code[name].Contains("public override bool WorksInside(Buildings.Building building) => TradeOffice.Here;"), "trades sources: " + name + " is worked on the practice's floor only");
            Check(MemberText(code[name], "public override void End(JobEnd why)").Contains("Ctx.Stand.Clear()"), "trades sources: " + name + " lets the player go on every way out");
        }
        string jobs = ModSource(@"Jobs\JobDirector.cs");
        Check(jobs.Contains("case JobCatalogue.Architect: return new DesignJob(DesignWork.Architect);") && jobs.Contains("new TradeJob(id)") && jobs.Contains("case JobCatalogue.Estimator: return new TenderJob();"),
            "trades sources: the jobs' director makes the trades' jobs");
        Check(jobs.Contains("if (!InsideAllowed(def))") && jobs.Contains("public static Func<bool> AtOffice;"), "trades sources: an office trade alone is taken indoors, on the practice's floor");
        string civic = ModSource(@"Civic\CityHallPhone.cs");
        Check(civic.Contains("Func<int, DialogEntry> work = CivicBridges.SiteWork;") && civic.Contains("\"reallife_trades_site_button\""), "trades sources: Work here is in the site notice's own conversation");
        string wiring = ModSource(@"Wiring\Wiring.TradesLinks.cs") + ModSource(@"Wiring\Wiring.cs");
        Check(wiring.Contains("Step(\"the building trades\", TradesLinks.Connect);") && wiring.Contains("Step(\"the building trades\", TradesLinks.Disconnect);")
              && wiring.Contains("CareerLinks.Workplace = TradesDirector.Workplace;") && wiring.Contains("CivicBridges.SiteWork = SiteWork.Page;"), "trades sources: the trades are wired in and out");
        string settings = ModSource(@"Core\Settings.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string cities = ModSource(@"Meta\FeatureCities.cs");
        string owners = ModSource(@"Core\FeatureOwners.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(settings.Contains("public const string TradesId = \"build_trades\";") && settings.Contains("public const string TradesOfficeId = \"build_trades_office\";")
              && settings.Contains("public static bool Trades = true, TradesOffice = true;")
              && settings.Contains("Trades = Bool(modId, TradesId, Trades);") && settings.Contains("TradesOffice = Bool(modId, TradesOfficeId, TradesOffice);"),
            "trades sources: the two options, on by default, read from the options page");
        Check(tree.Contains("S(\"buildtrades\").For(\"buildtrades\").Opt(\"build_trades\", \"jobs\").Opt(\"build_trades_office\", \"build_trades\")"), "trades sources: the options' section, each needing the one before");
        Check(cities.Contains("new Line(\"buildtrades\", CityScope.NewYork,"), "trades sources: the trades stand on New York's sites (the city table)");
        Check(owners.Contains("{ \"RealLifeOverhaul.BuildTrades\", \"buildtrades\" }") && mod.Contains("Guard.Add<BuildTrades.TradesDirector>(_root)") && mod.Contains("BuildTrades.TradesOptions.AddTrades(o)"),
            "trades sources: the feature's guard, its director and its options are registered");
        Check(code["TradesGuide.cs"].Contains(".Owns(Settings.TradesId, Settings.TradesOfficeId)") && code["TradesGuide.cs"].Contains(".Data(DesignBook.Key)"), "trades sources: the feature list owns its options and its line");
        // Every test of the options page has its check line.
        string tests = code["TradesDirector.Tests.cs"];
        foreach (string line in new[] { "Trades check (test)", "Trades check (site work test)", "Trades check (practice floor)", "Trades check (bribe test)", "Trades check (apprentice)" })
        {
            Check(tests.Contains(line), "trades sources: a test writes '" + line + "'");
        }
        foreach (string line in new[] { "Trades check (bar)", "Trades check (stroke)", "Trades check (pick)", "Trades check (learnt on the job)" })
        {
            Check(all.Contains(line), "trades sources: the shift writes '" + line + "'");
        }
        foreach (string line in new[] { "Trades check (design score)", "Trades check (design book)", "Trades check (design list)", "Trades check (satisfaction meter)", "Trades check (tender)",
                     "Trades check (practice desk)", "Trades check (practice panel)", "Trades check (site work)", "Trades check (commission)", "Trades check (bribe)" })
        {
            Check(all.Contains(line), "trades sources: '" + line + "' is written");
        }
    }

    private static void TradeTextChecks()
    {
        var keys = new SortedSet<string>();
        foreach (string name in new[] { @"BuildTrades\TradesDirector.cs", @"BuildTrades\TradesDirector.Tests.cs", @"BuildTrades\SiteWork.cs", @"BuildTrades\TradesGuide.cs", @"BuildTrades\TradeCatalogue.cs",
                     @"Jobs\Kinds\TradeJob.cs", @"Jobs\Kinds\TradeJob.Timing.cs", @"Jobs\Kinds\TradeJob.Match.cs", @"Jobs\Kinds\TradeJob.Judge.cs", @"Jobs\Kinds\TradeJob.Walk.cs",
                     @"Jobs\Kinds\DesignJob.cs", @"Jobs\Kinds\DesignJob.List.cs", @"Jobs\Kinds\TenderJob.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                if (!m.Groups[1].Value.Contains("calldialogtype"))
                {
                    keys.Add(m.Groups[1].Value);
                }
            }
        }
        foreach (TradeDef t in TradeCatalogue.All)
        {
            JobDef def = JobCatalogue.Find(t.Id);
            keys.Add(def.NameKey);
            keys.Add(def.PitchKey);
            keys.Add(def.TransactionKey);
            keys.Add("reallife_trades_" + t.Id + "_card_done");
            if (t.Mode == TradeMode.Timing || t.Mode == TradeMode.Match)
            {
                foreach (string part in new[] { "_obj", "_panel", "_miss", "_unit_done", "_unit", "_units" })
                {
                    keys.Add("reallife_trades_" + t.Id + part);
                }
            }
            if (t.Mode == TradeMode.Match)
            {
                for (int i = 0; i < 3; i++)
                {
                    keys.Add("reallife_trades_" + t.Id + "_ask_" + i);
                    keys.Add("reallife_trades_" + t.Id + "_pick_" + i);
                }
            }
            else if (t.Mode == TradeMode.Timing)
            {
                keys.Add("reallife_trades_" + t.Id + "_button");
            }
            if (t.Needs != null)
            {
                keys.Add(t.Needs.LacksKey);
            }
        }
        for (int i = 0; i < TradeRules.ItemKinds; i++)
        {
            keys.Add("reallife_trades_check_" + i);
            keys.Add("reallife_trades_tender_item_" + i);
        }
        for (int e = 0; e < DesignRules.Elements; e++)
        {
            keys.Add("reallife_trades_facade_" + e);
            for (int o = 0; o < DesignRules.Options; o++)
            {
                keys.Add("reallife_trades_facade_" + e + "_" + o);
            }
        }
        for (int i = 1; i <= 6; i++)
        {
            keys.Add("reallife_trades_room_" + i);
        }
        for (int i = 0; i < 4; i++)
        {
            keys.Add("reallife_trades_client_" + i);
            keys.Add("reallife_trades_security_post_" + i);
        }
        for (int i = 0; i < 3; i++)
        {
            keys.Add("reallife_trades_row_" + i);
            keys.Add("reallife_trades_slab_" + i);
        }
        foreach (string id in new[] { CareerCatalogue.Builders, CareerCatalogue.Inspection, CareerCatalogue.Architects })
        {
            Employer e = CareerCatalogue.Find(id);
            keys.Add(e.NameKey);
            keys.Add(e.BossKey);
            keys.Add(e.Pitch);
            keys.Add(e.Welcome);
            keys.Add(e.Review);
            keys.Add(e.Notice);
            keys.Add(e.Walkout);
            keys.Add(e.Colleagues[0]);
            keys.Add(e.Colleagues[1]);
            foreach (CareerRank r in e.Ranks)
            {
                keys.Add(r.TitleKey);
            }
        }
        foreach (int c in new[] { StudyCatalogue.Bricklaying, StudyCatalogue.CraneTicket, StudyCatalogue.ConstructionManagement, StudyCatalogue.ArchitectLicence })
        {
            keys.Add("reallife_study_course_" + StudyCatalogue.Find(c).Key);
            keys.Add("reallife_study_about_" + StudyCatalogue.Find(c).Key);
        }
        keys.Add(StudyCatalogue.SkillKey(Skills.Masonry));
        keys.Add("reallife_opt_tip_build_trades");
        keys.Add("reallife_opt_tip_build_trades_office");
        keys.Add("reallife_career_review_held");
        keys.Add("reallife_career_needs_trades_off");
        Check(keys.Count > 300, "trades texts: their keys are found: " + keys.Count);
        string[] notices =
        {
            "reallife_trades_commission", "reallife_trades_bribe_out", "reallife_trades_bribe_out_dismissed", "reallife_trades_inspector_took", "reallife_trades_inspector_failed",
            "reallife_trades_inspector_stopped", "reallife_trades_security_caught", "reallife_trades_security_away", "reallife_trades_end_off", "reallife_career_review_held",
            "reallife_trades_design_total", "reallife_trades_test_apprentice_done", "reallife_trades_test_no_floor"
        };
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            Check(words != null, "trades texts: " + code + ".json is read");
            if (words == null)
            {
                continue;
            }
            int missing = 0, styled = 0;
            foreach (string key in keys)
            {
                if (!words.TryGetValue(key, out string value) || value.Length == 0)
                {
                    missing++;
                    Check(false, "trades texts: " + key + " is in " + code + ".json");
                }
                else if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Replace("\n- ", "\n").StartsWith("- ") || value.Replace("\n- ", "\n").Contains(" - "))
                {
                    styled++;
                    Check(false, "trades texts: " + key + " has no exclamation mark or dash in " + code);
                }
            }
            Check(missing == 0 && styled == 0, "trades texts: every key of the trades is in " + code + ".json, in the style");
            foreach (string key in notices)
            {
                Check(words.TryGetValue(key, out string value) && value.Length + 40 <= 240, "trades texts: the notice " + key + " stays under 240 characters with its figures in " + code);
            }
            Check(words.TryGetValue("reallife_guide_trades", out string page) && page.Contains("{trades_mason_pay}") && page.Contains("{trades_bribe_out}") && page.Contains("# Going Public"),
                "trades texts: the Guide's page in " + code + " has its figures and its Going Public part");
        }
        string guide = ModSource(@"BuildTrades\TradesGuide.cs");
        foreach (string fact in new[] { "trades_mason_pay", "trades_labourer_pay", "trades_architect_pay", "trades_floor", "trades_mason_points", "trades_foreman_points", "trades_learn", "trades_bribe_out",
                     "trades_bribe_days", "trades_items" })
        {
            Check(guide.Contains(".Fact(\"" + fact + "\""), "trades texts: the Guide's {" + fact + "} is filled from the rules");
        }
    }
}
