using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.Uptown;

// Uptown's landmarks (Scripts/Landmarks): their sites on the real plan of the first block (none built from the first day,
// none another's, the next block when one is claimed, the same in any order), the works' plot and the ground the main
// building stands on, a span of lots in Uptown's own rules and line; the timetable, the pace and the phases against the
// construction game's; the lift of the lots round them and a match day's; the season; the stadium's pieces, the crowds'
// squares and the plaza; the landmarks' line read back whole; the construction game's landmark tenders; then, over the
// sources, the layering, the frame, the wiring, the shared files' lines and the tests' lines; and the texts in every language.
internal static partial class Program
{
    private static void LandmarksChecks()
    {
        List<SiteLot> lots = LandmarkLots();
        LandmarkSiteChecks(lots);
        LandmarkSpanChecks(lots);
        LandmarkTimeChecks();
        LandmarkLiftChecks(lots);
        LandmarkSeasonChecks();
        LandmarkPieceChecks(lots);
        LandmarkStoreChecks();
        LandmarkSourceChecks();
        LandmarkTextChecks();
    }

    // The first block's lots as the director reads them: the plan's, each built from the first day or not by Uptown's own rule.
    private static List<SiteLot> LandmarkLots()
    {
        var plan = new List<BuildingsLot>();
        foreach (Lot l in LayoutRules.Lots)
        {
            plan.Add(new BuildingsLot
            {
                Id = l.Id, Street = l.Street, Number = l.Number, First = l.First, Avenue = BuildingsRules.FrontsAvenue(l.Facing), X = l.X, Z = l.Z, Facing = l.Facing,
                Width = l.Width, Depth = l.Depth, Class = (BuildingsClass)(int)l.Class, Sizes = l.Sizes
            });
        }
        var dayOne = new List<int>();
        BuildingsRules.DayOne(plan, 0f, 1f, dayOne);
        var firstDay = new HashSet<string>();
        foreach (int i in dayOne)
        {
            firstDay.Add(plan[i].Id);
        }
        var lots = new List<SiteLot>();
        foreach (Lot l in LayoutRules.FirstLots)
        {
            lots.Add(new SiteLot
            {
                Id = l.Id, Block = l.Block, First = l.First, FirstDay = firstDay.Contains(l.Id), Avenue = l.Facing < 1f || l.Facing > 359f, X = l.X, Z = l.Z, Yaw = l.Yaw,
                Width = l.Width, Depth = l.Depth, Class = (int)l.Class
            });
        }
        Check(lots.Count == 83 && firstDay.Count == 21, "landmarks: the first block's 83 lots read, 21 of them built from the first day (" + lots.Count + ", " + firstDay.Count + ")");
        return lots;
    }

    private static LandmarkSite SiteOf(LandmarkKind kind, List<SiteLot> lots, Func<string, bool> claimed, List<int> held)
    {
        var site = new LandmarkSite();
        return LandmarkRules.Site(kind, lots, claimed, held, site) ? site : null;
    }

    private static void LandmarkSiteChecks(List<SiteLot> lots)
    {
        var held = new List<int>();
        LandmarkSite tower = SiteOf(LandmarkKind.Tower, lots, null, held);
        held.Add(tower != null ? tower.Block : 0);
        LandmarkSite mall = SiteOf(LandmarkKind.Mall, lots, null, held);
        held.Add(mall != null ? mall.Block : 0);
        LandmarkSite stadium = SiteOf(LandmarkKind.Stadium, lots, null, held);
        Check(tower != null && tower.Block == 111 && mall != null && mall.Block == 113 && stadium != null && stadium.Block == 110,
            "landmarks: on a new save the tower stands between 4th and 3rd Street, the mall between 2nd and 1st, the stadium by the buses' terminus between 5th and 4th");
        if (tower == null || mall == null || stadium == null)
        {
            return;
        }
        var byId = new Dictionary<string, SiteLot>();
        foreach (SiteLot l in lots)
        {
            byId[l.Id] = l;
        }
        foreach (LandmarkSite s in new[] { tower, mall, stadium })
        {
            bool fine = s.Lots.Count >= 5 && s.Lots[0] == s.Address && byId[s.Address].Avenue;
            int expected = 0;
            foreach (SiteLot l in lots)
            {
                expected += l.Block == s.Block && !l.FirstDay ? 1 : 0;
            }
            foreach (string id in s.Lots)
            {
                fine &= byId.ContainsKey(id) && byId[id].Block == s.Block && byId[id].First && !byId[id].FirstDay;
                fine &= s.Ground.Holds(byId[id].X, byId[id].Z, 0f);
            }
            Check(fine && s.Lots.Count == expected, "landmarks: " + s.Kind + "'s site is every lot of block " + s.Block + " not built from the first day (" + s.Lots.Count + "), its address on the avenue");
            Check(s.Plot.Width <= LandmarkRules.PlotMost && s.Plot.Depth <= LandmarkRules.PlotMost && s.Plot.Width >= 50 && s.Plot.Depth >= 39
                  && s.Plot.X - s.Plot.Width * 0.5f >= s.Ground.X0 - 0.01f && s.Plot.X + s.Plot.Width * 0.5f <= s.Ground.X1 + 0.01f
                  && Math.Abs(s.Plot.Z + s.Plot.Depth * 0.5f - s.Ground.Z1) < 0.01f && s.Plot.Depth <= s.Ground.Depth + 0.01f && s.Plot.Yaw == 180
                  && PlotRules.Unfit(s.Plot.Width, s.Plot.Depth, 10) == null,
                "landmarks: " + s.Kind + "'s works stand on a plot the city's programme takes (" + s.Plot.Width + " by " + s.Plot.Depth + " m), its front on the avenue, inside the site");
            int anchored = 0;
            foreach (string id in s.Anchor)
            {
                anchored += s.Lots.Contains(id) ? 1 : 0;
            }
            Check(anchored == s.Anchor.Count && s.Anchor[0] == s.Address && s.Anchor.Count + s.Units.Count == s.Lots.Count,
                "landmarks: " + s.Kind + "'s main building stands over lots of its own site, its address first, and every other lot is a shop of its");
        }
        Check(tower.Units.Count == 0 && stadium.Units.Count == 0 && mall.Units.Count >= 4 && mall.Anchor.Count >= 4,
            "landmarks: the mall alone has shops of its own round its main building (" + mall.Units.Count + " shops, its main building over " + mall.Anchor.Count + " lots)");
        Check(stadium.Ground.Depth >= LandmarkRules.StadiumLeastDepth && stadium.Ground.Width >= 60f, "landmarks: the stadium's ground is deep enough for its bowl (" + stadium.Ground.Width + " by " + stadium.Ground.Depth + " m)");
        // The main building's design must fit both the works' frame and its own ground: the game's largest offices (K, about 28 by 38 m) and stores (M, 26 by 33).
        PlotRules.Frame(tower.Plot.Width, tower.Plot.Depth, out float minX, out float maxX, out float minZ, out float maxZ);
        Check(Math.Min(maxX - minX, tower.AnchorGround.Width) >= 28f && Math.Min(maxZ - minZ, tower.AnchorGround.Depth) >= 38f,
            "landmarks: a K office of the city's fits the tower's frame and ground (" + (maxX - minX) + " by " + (maxZ - minZ) + " m frame)");
        PlotRules.Frame(mall.Plot.Width, mall.Plot.Depth, out minX, out maxX, out minZ, out maxZ);
        Check(Math.Min(maxX - minX, mall.AnchorGround.Width) >= 26f && Math.Min(maxZ - minZ, mall.AnchorGround.Depth) >= 33f,
            "landmarks: an M store of the city's fits the mall's frame and its main building's ground (" + mall.AnchorGround.Width + " by " + mall.AnchorGround.Depth + " m)");

        // A lot somebody has moves the site to the next block in its order; every block claimed leaves none; any order of the lots gives the same.
        LandmarkSite moved = SiteOf(LandmarkKind.Tower, lots, id => id == tower.Lots[2], null);
        Check(moved != null && moved.Block == 112, "landmarks: a lot of the tower's block somebody has moves it to the next block in its order (" + (moved != null ? moved.Block : 0) + ")");
        Check(SiteOf(LandmarkKind.Tower, lots, id => true, null) == null, "landmarks: with every lot claimed no landmark has a site, and waits");
        Check(SiteOf(LandmarkKind.Mall, lots, null, new List<int> { 113, 112, 111, 110, 107, 106, 108, 109 }) == null, "landmarks: with every block another's, no site");
        var shuffled = new List<SiteLot>(lots);
        shuffled.Reverse();
        LandmarkSite again = SiteOf(LandmarkKind.Stadium, shuffled, null, new List<int> { 111, 113 });
        Check(again != null && again.Block == stadium.Block && again.Address == stadium.Address && string.Join(",", again.Lots) == string.Join(",", stadium.Lots)
              && again.Plot.X == stadium.Plot.X && again.Plot.Z == stadium.Plot.Z, "landmarks: the same site whatever order the lots come in: the same on every machine");
        Check(LandmarkRules.SiteOn(LandmarkKind.Stadium, 107, lots, null, new LandmarkSite()) == (SiteDepth(lots, 107) >= LandmarkRules.StadiumLeastDepth),
            "landmarks: a block too shallow for the stadium's bowl is never its");
        Check(LandmarkRules.SiteId(LandmarkKind.Tower) == "landmark:tower" && LandmarkRules.KindOf("landmark:stadium") == (int)LandmarkKind.Stadium && LandmarkRules.KindOf("uptown_1_10_01") < 0
              && LandmarkRules.IsSite("landmark:mall") && !LandmarkRules.IsSite("uptown_1_10_01") && UptownLots.Get("landmark:tower") == null,
            "landmarks: a site's id is never one of the plan's lots");
    }

    private static float SiteDepth(List<SiteLot> lots, int block)
    {
        var s = new LandmarkSite();
        return LandmarkRules.SiteOn(LandmarkKind.Tower, block, lots, null, s) ? s.Ground.Depth : 0f;
    }

    private static void LandmarkSpanChecks(List<SiteLot> lots)
    {
        var held = new List<int>();
        LandmarkSite tower = SiteOf(LandmarkKind.Tower, lots, null, held);
        var span = new List<BuildingsLot>();
        foreach (string id in tower.Anchor)
        {
            Lot l = UptownLots.Get(id);
            span.Add(new BuildingsLot { Id = l.Id, X = l.X, Z = l.Z, Facing = l.Facing, Width = l.Width, Depth = l.Depth });
        }
        Check(BuildingsRules.SpanGround(span, out float x, out float z, out float w, out float d) && Math.Abs(w - tower.Ground.Width) < 0.01f && Math.Abs(d - tower.Ground.Depth) < 0.01f
              && Math.Abs(x - tower.Ground.MidX) < 0.01f && Math.Abs(z - tower.Ground.MidZ) < 0.01f,
            "landmarks: Uptown's span over the tower's lots is the landmark's own ground (" + w + " by " + d + " m)");
        Check(!BuildingsRules.SpanGround(new List<BuildingsLot>(), out _, out _, out _, out _), "landmarks: a span of no lots is no ground");
        Check(BuildingsRules.SpanRefusal(0, 0, 0, 0, true) == null && BuildingsRules.SpanRefusal(4, 4, 4, 4, true) == null
              && BuildingsRules.SpanRefusal(4, 3, 4, 4, true) != null && BuildingsRules.SpanRefusal(4, 4, 3, 4, true) != null && BuildingsRules.SpanRefusal(4, 4, 4, 3, true) != null
              && BuildingsRules.SpanRefusal(4, 4, 4, 4, false) != null && BuildingsRules.SpanRefusal(BuildingsRules.SpanMost, 99, 99, 99, true) != null,
            "landmarks: Uptown refuses a span with a lot unknown, taken, in another block or named twice, or too many lots");
        var lot = new BuildingsLot { Id = "a", Sizes = new[] { "ba:buildingsize_h" } };
        Check(BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_k", 1, "ba:buildingtype_office", 0, FinishOwner.City, 0, true, true, false, true) == null
              && BuildingsRules.FinishRefusal(lot, true, false, "ba:buildingsize_k", 1, "ba:buildingtype_office", 0, FinishOwner.City, 0, true, true, false) != null,
            "landmarks: a building over a span may be of a size its own lot's class does not take; alone on its lot it may not");
        Check(BuildingsRules.Lifted(38, 10) == 48 && BuildingsRules.Lifted(70, 30) == BuildingsRules.LiftedMost && BuildingsRules.Lifted(5, 0) == BuildingsRules.TrafficLeast
              && BuildingsRules.Lifted(30, -5) == 30, "landmarks: a lot's traffic lifted, never over Uptown's most nor under the game's least, never lowered");
        // The line keeps the span; an older line without it reads as before.
        var state = new LifeState();
        state.Finished.Add(new FinishedBuilding { Lot = tower.Address, Size = "ba:buildingsize_k", Version = 1, Type = "ba:buildingtype_office", Design = "SM_Office", Over = new[] { "uptown_1_11_01", "uptown_1_11_03" } });
        state.Finished.Add(new FinishedBuilding { Lot = "uptown_1_06_01", Size = "ba:buildingsize_m", Version = 1, Type = "ba:buildingtype_retail", Design = string.Empty });
        LifeState back = LifeStore.Read(LifeStore.Write(state), out _);
        Check(back != null && back.Finished.Count == 2 && back.Finished[0].Over != null && back.Finished[0].Over.Length == 2 && back.Finished[0].Over[1] == "uptown_1_11_03"
              && back.Finished[1].Over == null && back.Covering("uptown_1_11_03") == back.Finished[0] && back.Covering("uptown_1_06_01") == back.Finished[1] && back.Covering("x") == null,
            "landmarks: Uptown's line keeps the lots a building stands over, and knows a lot under one as taken");
        LifeState old = LifeStore.Read("v=1^f:uptown_1_06_01|ba:buildingsize_a|1|ba:buildingtype_retail||0|0|0|3|1", out _);
        Check(old != null && old.Finished.Count == 1 && old.Finished[0].Over == null, "landmarks: Uptown's line written before spans reads as it did");
        LifeState odd = LifeStore.Read("v=1^f:a|s|1|t||0|0|0|3|1|b,b,c d,;b|s|1|t||0|0|0|3|1", out _);
        Check(odd != null && odd.Finished.Count == 1 && odd.Finished[0].Over.Length == 1 && odd.Finished[0].Over[0] == "b",
            "landmarks: a span's lots are ids only, each once, and a record on a lot another stands over is not read");
    }

    private static void LandmarkTimeChecks()
    {
        Check(LandmarkRules.AnnounceOn(LandmarkKind.Tower, 1, true) < LandmarkRules.AnnounceOn(LandmarkKind.Mall, 1, true)
              && LandmarkRules.AnnounceOn(LandmarkKind.Mall, 1, true) < LandmarkRules.AnnounceOn(LandmarkKind.Stadium, 1, true)
              && LandmarkRules.AnnounceOn(LandmarkKind.Tower, 900, true) == LandmarkRules.AnnounceOn(LandmarkKind.Tower, 3, true)
              && LandmarkRules.AnnounceOn(LandmarkKind.Tower, 400, false) > 400 && LandmarkRules.AnnounceOn(LandmarkKind.Stadium, 400, false) > LandmarkRules.AnnounceOn(LandmarkKind.Mall, 400, false),
            "landmarks: announced one after another, on fixed days in a Going Public session, and in turn after the programme first ran in an older save");
        bool weeks = true;
        for (int day = 1; day < 60; day++)
        {
            weeks &= LandmarkRules.Weekday(day) == LandRules.Weekday(day) && LandmarkRules.AfterWorkingDays(day, 7) == PlanningRules.AfterWorkingDays(day, 7);
        }
        Check(weeks, "landmarks: the week and working days are the construction game's own");
        Check(LandmarkRules.CityDue(LandmarkKind.Tower, 20) > 20 + LandmarkRules.CityDays(LandmarkKind.Tower) && LandmarkRules.CityDue(LandmarkKind.Mall, 20) < LandmarkRules.CityDue(LandmarkKind.Tower, 20),
            "landmarks: the city's own works take their working days, weekends on top");
        bool phases = true;
        for (int p = 0; p < 3; p++)
        {
            phases &= LandmarkRules.PhaseEnd(p) == BuildCatalogue.PlotStageEnds[p];
        }
        Check(phases && LandmarkRules.Phase(0.0) == 0 && LandmarkRules.Phase(0.45) == 1 && LandmarkRules.Phase(0.7) == 2 && LandmarkRules.Phase(0.9) == 3 && LandmarkRules.Phase(2.0) == 3,
            "landmarks: the works' phases are the plot's own stages (groundworks, frame, envelope, fit-out)");
        double done = 0;
        bool short_ = true;
        for (int day = 10; day <= 40; day++)
        {
            for (int h = 0; h < 10; h++)
            {
                done += LandmarkRules.Pace(done, 480, 1f, day, 10, 40);
                short_ &= done <= 480 * 0.995 + 1e-6;
            }
        }
        Check(short_ && done > 480 * 0.99, "landmarks: the timetable's pace keeps the site just short of its end until its due day hands it over (" + done.ToString("0.0") + " of 480 hours)");
        Check(LandmarkRules.Pace(0, 0, 3f, 5, 1, 10) == 3f, "landmarks: a site with no hours of its own works at the city's pace");
    }

    private static void LandmarkLiftChecks(List<SiteLot> lots)
    {
        LandmarkSite stadium = SiteOf(LandmarkKind.Stadium, lots, null, new List<int> { 111, 113 });
        var kinds = new List<LandmarkKind> { LandmarkKind.Stadium };
        var grounds = new List<SiteRect> { stadium.Ground };
        int inside = LandmarkRules.Lift(stadium.Ground.MidX, stadium.Ground.MidZ, kinds, grounds, 1);
        int near = LandmarkRules.Lift(stadium.Ground.X1 + 20f, stadium.Ground.MidZ, kinds, grounds, 1);
        int far = LandmarkRules.Lift(stadium.Ground.X1 + 400f, stadium.Ground.MidZ, kinds, grounds, 1);
        Check(inside == 6 && near > 0 && near < inside && far == 0 && LandmarkRules.Lift(0, 0, kinds, grounds, 0) == 0, "landmarks: the lift is the most beside a landmark and none beyond its reach (" + inside + ", " + near + ", " + far + ")");
        var three = new List<LandmarkKind> { LandmarkKind.Tower, LandmarkKind.Mall, LandmarkKind.Stadium };
        var same = new List<SiteRect> { stadium.Ground, stadium.Ground, stadium.Ground };
        Check(LandmarkRules.Lift(stadium.Ground.MidX, stadium.Ground.MidZ, three, same, 3) == LandmarkRules.LiftCap, "landmarks: three landmarks together lift a lot to the cap, no further");
        Check(LandmarkRules.MatchDayLift(stadium.Ground.MidX, stadium.Ground.Z1 + 30f, stadium.Ground) == LandmarkRules.MatchLift
              && LandmarkRules.MatchDayLift(stadium.Ground.MidX + 400f, stadium.Ground.Z1, stadium.Ground) == 0 && LandmarkRules.MatchDayLift(0, 0, default(SiteRect)) == 0,
            "landmarks: a match day lifts the traffic within its reach of the stadium only");
    }

    private static void LandmarkSeasonChecks()
    {
        bool saturdays = true, before = true;
        for (int day = 1; day < 200; day++)
        {
            saturdays &= LandmarkRules.MatchDay(day, 30) == (day > 30 && LandmarkRules.Weekday(day) == 5);
            before &= !LandmarkRules.MatchDay(day, 0);
        }
        Check(saturdays && before, "landmarks: Uptown FC play at home every Saturday after the stadium opened, and never before it has");
        int next = LandmarkRules.NextMatch(31, 30);
        Check(LandmarkRules.MatchDay(next, 30) && next - 31 < 7 && LandmarkRules.NextMatch(next, 30) == next, "landmarks: the next home match is the first Saturday from a day on");
        var seen = new HashSet<int>();
        bool scores = true, crowds = true, same = true;
        for (int day = 6; day < 6 + 7 * 8; day += 7)
        {
            seen.Add(LandmarkRules.Opponent(day));
        }
        for (int day = 1; day < 400; day++)
        {
            LandmarkRules.Score(day, out int h, out int a);
            LandmarkRules.Score(day, out int h2, out int a2);
            scores &= h >= 0 && h <= 4 && a >= 0 && a <= 3;
            same &= h == h2 && a == a2 && LandmarkRules.Opponent(day) == LandmarkRules.Opponent(day) && LandmarkRules.Attendance(day, 30) == LandmarkRules.Attendance(day, 30);
            int n = LandmarkRules.Attendance(day, 30);
            crowds &= n >= LandmarkRules.AttendanceLeast && n <= LandmarkRules.Capacity;
        }
        Check(seen.Count == LandmarkRules.OpponentCount, "landmarks: every club of the league comes in turn over eight weeks (" + seen.Count + ")");
        Check(scores && crowds && same, "landmarks: scores and crowds within bounds, the same on every machine for the same day");
        Check(LandmarkRules.Attendance(300, 30) >= LandmarkRules.Attendance(37, 30) - 4800, "landmarks: the crowds grow as the ground settles in");
        Check(LandmarkRules.JobsOf(LandmarkKind.Tower) > LandmarkRules.JobsOf(LandmarkKind.Mall) && LandmarkRules.JobsOf(LandmarkKind.Mall) > LandmarkRules.JobsOf(LandmarkKind.Stadium)
              && LandmarkRules.MatchJobs > 0, "landmarks: each landmark's jobs, the tower's the most");
        Check(LandmarkRules.CityContractor(LandmarkKind.Tower) >= 0 && LandmarkRules.CityContractor(LandmarkKind.Stadium) < 8, "landmarks: the city's works engage one of the programme's eight contractors");
    }

    private static void LandmarkPieceChecks(List<SiteLot> lots)
    {
        LandmarkSite stadium = SiteOf(LandmarkKind.Stadium, lots, null, new List<int> { 111, 113 });
        var pieces = new List<LandmarkPiece>();
        LandmarkRules.Stadium(stadium.Ground, pieces);
        int stands = 0, ends = 0, corners = 0, lights = 0, lamps = 0, pitch = 0, gates = 0;
        bool inside = true, clear = true;
        LandmarkPiece field = default(LandmarkPiece);
        foreach (LandmarkPiece p in pieces)
        {
            stands += p.Id == LandmarkRules.Stand ? 1 : 0;
            ends += p.Id == LandmarkRules.End ? 1 : 0;
            corners += p.Id == LandmarkRules.Corner ? 1 : 0;
            lights += p.Id == LandmarkRules.Floodlight ? 1 : 0;
            lamps += p.Id == LandmarkRules.Lamps ? 1 : 0;
            gates += p.Id == LandmarkRules.Turnstile ? 1 : 0;
            if (p.Id == LandmarkRules.Pitch)
            {
                pitch++;
                field = p;
            }
            inside &= stadium.Ground.Holds(p.X, p.Z, 0.01f);
        }
        foreach (LandmarkPiece p in pieces)
        {
            if (p.Solid && p.Id != LandmarkRules.Floodlight)
            {
                float half = p.Id == LandmarkRules.Stand ? LandmarkRules.StandDepth * 0.5f : LandmarkRules.EndDepth * 0.5f;
                clear &= Math.Abs(p.X - field.X) >= field.StretchX * LandmarkRules.PitchWidth * 0.5f + half - 0.01f
                         || Math.Abs(p.Z - field.Z) >= field.StretchZ * LandmarkRules.PitchDepth * 0.5f + (p.Id == LandmarkRules.End ? 0f : half) - 0.01f;
            }
        }
        Check(stands >= 8 && stands % 2 == 0 && ends >= 6 && ends % 2 == 0 && corners == 4 && lights == 4 && lamps == 4 && pitch == 1 && gates == LandmarkRules.Gates,
            "landmarks: Crest Field stands " + stands + " main stand sections, " + ends + " end sections, four corners, four floodlights with their lamps, its pitch and its turnstiles");
        Check(inside && clear && field.StretchX > 0.8f && field.StretchX < 1.2f && field.StretchZ > 0.75f && field.StretchZ < 1.2f,
            "landmarks: every piece of the stadium inside its ground, the stands round the pitch and none on it, the pitch stretched little (" + field.StretchX.ToString("0.00") + ", " + field.StretchZ.ToString("0.00") + ")");
        var none = new List<LandmarkPiece> { new LandmarkPiece() };
        LandmarkRules.Stadium(new SiteRect(0, 0, 20, 20), none);
        Check(none.Count == 0, "landmarks: ground too small for a bowl stands nothing");
        var squares = new List<CrowdSquare>();
        LandmarkRules.Crowds(stadium.Ground, true, squares);
        bool pavement = squares.Count == 4;
        for (int i = 0; i < squares.Count; i++)
        {
            CrowdSquare s = squares[i];
            bool front = s.Z > stadium.Ground.Z1 && s.Z + s.Depth * 0.5f <= stadium.Ground.Z1 + 3.7f;
            bool side = (s.X < stadium.Ground.X0 && s.X - s.Width * 0.5f >= stadium.Ground.X0 - 3.7f) || (s.X > stadium.Ground.X1 && s.X + s.Width * 0.5f <= stadium.Ground.X1 + 3.7f);
            pavement &= front || side;
        }
        LandmarkRules.Crowds(stadium.Ground, false, squares);
        Check(pavement && squares.Count == 1 && squares[0].Z > stadium.Ground.Z1, "landmarks: the crowds stand on the pavements round a landmark, never on its ground or the road");
        LandmarkSite tower = SiteOf(LandmarkKind.Tower, lots, null, null);
        var plaza = new List<LandmarkPiece>();
        LandmarkRules.Plaza(tower.AnchorGround, 28f, 38f, true, plaza);
        bool off = plaza.Count >= 6;
        foreach (LandmarkPiece p in plaza)
        {
            bool under = Math.Abs(p.X - tower.AnchorGround.MidX) < 14f + 1.4f && p.Z > tower.AnchorGround.Z1 - 38f - 1.4f;
            off &= !under && tower.AnchorGround.Holds(p.X, p.Z, 0.01f) && !p.Solid;
        }
        Check(off, "landmarks: the plaza's flags, bollards, benches and bins stand round the tower, never in it, inside its ground (" + plaza.Count + ")");
        LandmarkRules.Front(stadium.Ground, out float fx, out float fz);
        Check(fz > stadium.Ground.Z1 && fz < stadium.Ground.Z1 + 3.7f && Math.Abs(fx - stadium.Ground.MidX) < 0.01f, "landmarks: a landmark's front is on the avenue's pavement in the middle of it");
        Check(LandmarkRules.Tallest(new List<float> { 20f, 41f, 41f, 12f }) == 1 && LandmarkRules.Tallest(new List<float>()) == -1, "landmarks: the tallest design that fits, the first of equals");
    }

    private static void LandmarkStoreChecks()
    {
        var state = new LandmarkState { Anchor = 14, LastDay = 90 };
        LandmarkFile t = state.Of(LandmarkKind.Tower);
        t.Stage = LandmarkStage.Building;
        t.Block = 111;
        t.Announced = 15;
        t.Tender = 42;
        t.Builder = 5;
        t.Price = 61234000L;
        t.Started = 19;
        t.Due = 80;
        t.SiteId = 7;
        t.Phase = 2;
        t.PhaseDay = 50;
        t.Design = "SM_Office|Tall;One";
        t.Size = "ba:buildingsize_k";
        t.Version = 2;
        t.Type = "ba:buildingtype_office";
        t.Width = 27.5f;
        t.Depth = 37.25f;
        t.Height = 48.5f;
        t.Area = 12800;
        LandmarkFile s = state.Of(LandmarkKind.Stadium);
        s.Stage = LandmarkStage.Opened;
        s.Builder = LandmarkBuilder.City;
        s.Finished = 70;
        s.Opening = 71;
        s.Joined = true;
        s.InDistrict = true;
        LandmarkState back = LandmarkStore.Read(LandmarkStore.Write(state), out int version);
        LandmarkFile b = back != null ? back.Of(LandmarkKind.Tower) : null, c = back != null ? back.Of(LandmarkKind.Stadium) : null;
        Check(back != null && version == LandmarkStore.Version && back.Anchor == 14 && back.LastDay == 90 && b.Stage == LandmarkStage.Building && b.Block == 111 && b.Announced == 15
              && b.Tender == 42 && b.Builder == 5 && b.Price == 61234000L && b.Started == 19 && b.Due == 80 && b.SiteId == 7 && b.Phase == 2 && b.PhaseDay == 50
              && b.Design == "SM_Office Tall One" && b.Size == "ba:buildingsize_k" && b.Version == 2 && b.Width == 27.5f && b.Depth == 37.25f && b.Height == 48.5f && b.Area == 12800
              && c.Stage == LandmarkStage.Opened && c.Builder == LandmarkBuilder.City && c.Opening == 71 && c.Joined && c.InDistrict && back.Of(LandmarkKind.Mall).Stage == LandmarkStage.Planned
              && LandmarkStore.Write(back) == LandmarkStore.Write(LandmarkStore.Read(LandmarkStore.Write(back), out _)),
            "landmarks: the line read back whole, the same line again, a design's name never breaking it");
        LandmarkState later = LandmarkStore.Read("v=2^z:new part^a:3|9^l:0|5|111|4|0|-2|0|7|60|0|61|62|3|40|0|1|d|s|1|t|1|2|3|99|more;7|1|1|1|1|1|1|1|1|1|1|1|1|1|1|1|d|s|1|t|1|1|1", out int laterVersion);
        Check(later != null && laterVersion == 2 && later.Anchor == 3 && later.Of(LandmarkKind.Tower).Stage == LandmarkStage.Opened && later.Of(LandmarkKind.Tower).Area == 99,
            "landmarks: a later version's line is read as far as this one knows it, a landmark it does not know passed over");
        LandmarkState old = LandmarkStore.Read("v=1^a:3|9^l:2|4|110|4|0|-2|0|7|60|0|61|62|3|40|0|1|||0||0|0|0", out _);
        Check(old != null && old.Of(LandmarkKind.Stadium).Stage == LandmarkStage.Finished && old.Of(LandmarkKind.Stadium).Area == 0, "landmarks: a record before the design's area reads as it did");
        LandmarkState bounded = LandmarkStore.Read("v=1^l:1|99|-5|-1|-1|-99|-7|0|0|-3|0|0|9|0|0|0|||999||-1|-1|5000", out _);
        LandmarkFile m = bounded.Of(LandmarkKind.Mall);
        Check(m.Stage == LandmarkStage.Opened && m.Block == 0 && m.Builder == LandmarkBuilder.Nobody && m.Price == 0 && m.Phase == 3 && m.Version == 99 && m.Width == 0f && m.Height == 0f,
            "landmarks: every figure of the line is bounded");
        Check(LandmarkStore.Read("garbage", out _) == null && LandmarkStore.Read(null, out _) == null && LandmarkStore.Load(null, LandmarkStore.Key, out _).Files.Length == 3
              && LandmarkStore.Key == "reallife:landmarks" && LandmarkStore.TestKey == "reallife:landmarks_before_tests",
            "landmarks: no line is a new save's, a line of something else is no line, under keys of the mod's own");
        // The construction game reads a landmark's tender back, and it suits the builders of offices.
        Check(TenderRules.NeedOf(TenderSource.Landmark, 5, BuildUse.Offices) == Speciality.Offices && TenderRules.NeedOf(TenderSource.Landmark, 5, BuildUse.Homes) == Speciality.Homes
              && BuildCatalogue.LandmarkPremiumPermille > 0 && BuildCatalogue.LandmarkTenderHours > BuildCatalogue.TenderHours,
            "landmarks: a landmark's tender is priced above a public building's, open longer, and suits the builders of its kind");
        var bs = new BuildState { Seed = 3 };
        bs.Tenders.Add(new TenderFile { Id = 9, Source = TenderSource.Landmark, CivicKind = 5, Lot = "landmark:tower", Hours = 480, Value = 70000000L, Opened = 100, Closes = 196 });
        bs.Projects.Add(new ProjectFile { Id = 11, Kind = ProjectKind.Contract, Source = TenderSource.Landmark, CivicKind = 5, Lot = "landmark:tower" });
        BuildState read = BuildStore.Read(BuildStore.Write(bs), out _);
        Check(read != null && read.Tenders.Count == 1 && read.Tenders[0].Source == TenderSource.Landmark && read.Tenders[0].Lot == "landmark:tower" && read.Projects.Count == 1
              && read.Projects[0].Source == TenderSource.Landmark && read.Projects[0].Model == ProjectKind.Plot,
            "landmarks: the construction game's line keeps a landmark's tender and the player's project for it, built as a plot");
        Check(LandmarkRules.UseOf(LandmarkKind.Tower) == (int)BuildUse.Offices && LandmarkRules.UseOf(LandmarkKind.Mall) == (int)BuildUse.Shops
              && LandmarkRules.UseOf(LandmarkKind.Stadium) == (int)BuildUse.Warehouses, "landmarks: each landmark's works priced as the construction game's use it is most like");
    }

    private static void LandmarkSourceChecks()
    {
        string[] files =
        {
            "LandmarkRules.cs", "LandmarkStore.cs", "LandmarkBridges.cs", "LandmarksGame.cs", "LandmarksDirector.cs", "LandmarksDirector.Works.cs", "LandmarksDirector.Street.cs",
            "LandmarksDirector.Paper.cs", "LandmarksDirector.Tests.cs", "LandmarksText.cs", "LandmarksGuide.cs"
        };
        var code = new Dictionary<string, string>();
        foreach (string name in files)
        {
            code[name] = ModSource(@"Landmarks\" + name);
            Check(code[name].Length > 0, "landmarks sources: " + name + " is there");
        }
        string all = string.Join("\n", code.Values);
        Check(!Regex.IsMatch(all, @"using System\.Linq|\.Where\(|\.Select\(|\.OrderBy\(|\.ToList\(\)|\.ToArray\(\)"), "landmarks sources: no LINQ");
        Check(!Regex.IsMatch(all, @"FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|OverlapSphere\(|RaycastAll\(|\bHarmony\b|\bMPClient\b|\bMPServer\b|DlcIsOwned|requiredDLC|SteamApps"),
            "landmarks sources: no whole-scene search, no patch, nothing sent over the network, no DLC read");
        foreach (string name in new[] { "LandmarkRules.cs", "LandmarkStore.cs" })
        {
            Check(!Regex.IsMatch(code[name], @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode"), "landmarks sources: " + name + " is game-free and leaves nothing to chance");
        }
        foreach (string name in files)
        {
            if (name == "LandmarksGame.cs")
            {
                continue;
            }
            Check(!Regex.IsMatch(code[name], @"new GameObject\(|AddComponent<|PeopleGame\.|WalkGame\.|PlayerHelper\.|BuildingManager\.|\.trafficIndex|TimeHelper\."),
                "landmarks sources: " + name + " leaves the game's scene, the player and the clock to LandmarksGame");
            Check(!Regex.IsMatch(code[name], @"\b(ConstructionDirector|CivicDirector|CivicWorks|SiteState|FameDirector|FlareDirector)\b"),
                "landmarks sources: " + name + " reaches the construction game, the programme and Standing only through its bridges");
        }
        string director = code["LandmarksDirector.cs"];
        Check(Regex.Matches(all, @"void Update\(\)").Count == 1 && MethodText(director, "private void Update()").Contains("using var hotSpot = HotSpots.Time(HotUpdate);")
              && MethodText(director, "private void Update()").Contains("Guard.Fail(FrameGuard, e, this)") && MethodText(director, "private void OnUpdate()").Contains("if (now < _next)")
              && director.Contains("Guard.For(\"landmarks\", \"LandmarksDirector\", typeof(LandmarksDirector))"),
            "landmarks sources: one Update, guarded: its hot spot, then one time comparison a frame");
        Check(director.Contains("SaveHook.Keep(_onSave)") && MethodText(director, "private void OnSave()").Contains("SaveLines.IsKept(save.modData, LandmarkStore.Key)"),
            "landmarks sources: the line is written at the save, and one this version could not read is kept as it was read");
        Check(MethodText(director, "public static void Register(").Contains("DistrictPlaces.Add(PlacesOwner, Fill);") && MethodText(director, "public static void Register(").Contains("DistrictBuildings.TrafficLift = LiftOf;")
              && MethodText(director, "public static void Register(").Contains("DistrictBuildings.HeldFor = Held;"),
            "landmarks sources: their places are the district save's, their lift and their held lots Uptown's, from game start");
        Check(code["LandmarksDirector.Works.cs"].Contains("DistrictBuildings.FinishOver(site.Address, Over,") && code["LandmarksDirector.Works.cs"].Contains("FinishOwner.City")
              && !code["LandmarksDirector.Works.cs"].Contains("FinishOwner.Player"), "landmarks sources: a landmark's buildings go to the district as the city's, never a deed on one machine");
        Check(code["LandmarksDirector.Works.cs"].Contains("GoingPublic.InSession || LandmarkBridges.OpenTender == null") && code["LandmarksDirector.Works.cs"].Contains("!GoingPublic.IsGuest && LandmarkBridges.StartPlot != null"),
            "landmarks sources: in a Going Public session the city builds them on its timetable, and only the host sets out their works");
        Check(code["LandmarksDirector.Street.cs"].Contains("new DoorPanel(Logger, \"Landmarks check (panel)\")") && code["LandmarksDirector.Street.cs"].Contains("MapPlaces.Register(\"landmarks\""),
            "landmarks sources: the game's own panel at a landmark's front, and the game's own map row");
        // The wiring.
        string wiring = ModSource(@"Wiring\Wiring.LandmarkLinks.cs"), construction = ModSource(@"Wiring\Wiring.ConstructionLinks.cs"), wire = ModSource(@"Wiring\Wiring.cs");
        foreach (Match m in Regex.Matches(code["LandmarkBridges.cs"], @"public static (?:[A-Za-z<>,\[\] ]+) ([A-Z][A-Za-z]+);"))
        {
            Check(code["LandmarkBridges.cs"].Contains(m.Groups[1].Value + " = null;") && wiring.Contains("LandmarkBridges." + m.Groups[1].Value + " = "),
                "landmarks sources: the bridge " + m.Groups[1].Value + " is wired in and put back");
        }
        foreach (string name in new[] { "LandmarkSites", "LandmarkHeld", "LandmarkName", "LandmarkWords", "LandmarkDesign", "LandmarkAwarded", "LandmarkHanded", "LandmarkWorks", "LandmarkPace", "LandmarkBuilt" })
        {
            Check(construction.Contains("ConstructionBridges." + name + " = ") && construction.Contains("ConstructionBridges." + name + " = null;"),
                "landmarks sources: the construction game's bridge " + name + " is the landmarks'");
        }
        Check(wire.Contains("Step(\"the landmarks\", LandmarkLinks.Connect);") && wire.IndexOf("Step(\"the landmarks\", LandmarkLinks.Disconnect);", StringComparison.Ordinal)
              < wire.IndexOf("Step(\"the construction game\", ConstructionLinks.Disconnect);", StringComparison.Ordinal)
              && wire.IndexOf("Step(\"the landmarks\", LandmarkLinks.Connect);", StringComparison.Ordinal) > wire.IndexOf("Step(\"the construction game\", ConstructionLinks.Connect);", StringComparison.Ordinal),
            "landmarks sources: wired in after the construction game and out before it (its piece of the paper is on top)");
        Check(MethodText(ModSource(@"Civic\CivicWorks.cs"), "private void AddLook(").Contains("SiteBuilders.IsTaken(look.Tag)"),
            "landmarks sources: the programme keeps no look of its own where a landmark stands in its own right (the stadium)");
        Check(MethodText(ModSource(@"Construction\ConstructionDirector.Asks.cs"), "internal static bool LotTaken(").Contains("LandmarkHeld(lot)")
              && MethodText(ModSource(@"Construction\ConstructionDirector.Projects.cs"), "private void HandToDistrict(").Contains("LandmarkHanded(p.Lot)"),
            "landmarks sources: the construction game sells and develops no lot a landmark holds, and hands a landmark it built to the landmarks");
        // The shared files' lines.
        string settings = ModSource(@"Core\Settings.cs");
        Check(settings.Contains("public static bool Landmarks = true;") && settings.Contains("Landmarks = Bool(modId, LandmarksId, Landmarks);")
              && settings.Contains("public const string LandmarksId = \"uptown_landmarks\";"), "landmarks sources: their option is on by default and read");
        Check(ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"landmarks\", CityScope.NewYork,") && ModSource(@"Core\FeatureOwners.cs").Contains("{ \"RealLifeOverhaul.Landmarks\", \"landmarks\" }")
              && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"landmarks\").For(\"landmarks\").Main(\"uptown_landmarks\")"),
            "landmarks sources: New York's in the city table, their own feature for the guard, their section of the options");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(mod.Contains("Guard.Add<Landmarks.LandmarksDirector>(_root)") && mod.Contains("Landmarks.LandmarksDirector.Register(context.Logger)")
              && mod.IndexOf("Uptown.LifeDirector.Register(context.Logger)", StringComparison.Ordinal) < mod.IndexOf("Landmarks.LandmarksDirector.Register(context.Logger)", StringComparison.Ordinal),
            "landmarks sources: the director is added, and registered at game start after Uptown");
        // Each test is a check of the test run, its line written by its method.
        string guide = code["LandmarksGuide.cs"], tests = code["LandmarksDirector.Tests.cs"];
        int checks = 0;
        foreach (Match m in Regex.Matches(guide, @"\.Check\(""(reallife_lmk_test_[a-z_]+)"", LandmarksDirector\.(Test[A-Za-z]+), ""(Landmarks check \([^""]+\))"""))
        {
            checks++;
            string method = m.Groups[2].Value, line = m.Groups[3].Value;
            Check(tests.Contains("public static void " + method + "()") && tests.Contains("\"" + line), "landmarks sources: " + method + " is a public static test that writes \"" + line + "\"");
        }
        Check(checks == 12 && !guide.Contains(".Test(\""), "landmarks sources: twelve tests, each joined to the test run (" + checks + ")");
        Check(guide.Contains("\"Landmarks check (back)\", RunPlace.Here, RunPhase.End"), "landmarks sources: their put back runs at the run's end");
    }

    private static void LandmarkTextChecks()
    {
        var keys = new SortedSet<string>();
        foreach (string file in new[] { @"Landmarks\LandmarksText.cs", @"Landmarks\LandmarksGuide.cs", @"Construction\ConstructionDirector.Landmarks.cs", @"Construction\ConstructionDirector.Market.cs",
                     @"Construction\ConstructionDirector.Projects.cs", @"Construction\ConstructionApp.Land.cs", @"Construction\ConstructionApp.Tenders.cs", @"Construction\ConstructionTables.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_(?:lmk|meta_title_landmarks|guide_landmarks)[a-z0-9_]*)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_opt_sec_landmarks");
        keys.Add("reallife_opt_tip_uptown_landmarks");
        for (int k = 0; k < LandmarkRules.OpponentCount; k++)
        {
            keys.Add("reallife_lmk_club_" + k);
        }
        Check(keys.Count > 80, "landmarks texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("landmarks texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            int missing = 0, styled = 0, places = 0;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0)
                {
                    missing++;
                    Check(false, "landmarks texts: " + key + " is in " + codes[l] + ".json");
                    continue;
                }
                if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Replace("\n- ", "\n").StartsWith("- ") || value.Replace("\n- ", "\n").Contains(" - "))
                {
                    styled++;
                    Check(false, "landmarks texts: " + key + " has no exclamation mark or dash in " + codes[l]);
                }
                var mine = new SortedSet<string>();
                var english = new SortedSet<string>();
                foreach (Match m in Regex.Matches(value, @"\{[a-z_]+\}"))
                {
                    mine.Add(m.Value);
                }
                foreach (Match m in Regex.Matches(words[0][key], @"\{[a-z_]+\}"))
                {
                    english.Add(m.Value);
                }
                if (!mine.SetEquals(english))
                {
                    places++;
                    Check(false, "landmarks texts: " + key + " has the English text's placeholders in " + codes[l]);
                }
            }
            Check(missing == 0 && styled == 0 && places == 0, "landmarks texts: every key of the landmarks is in " + codes[l] + ".json, in the style, with its placeholders");
        }
        foreach (string notice in new[] { "reallife_lmk_notice_joined", "reallife_lmk_notice_tender", "reallife_lmk_test_finished", "reallife_lmk_test_announced", "reallife_lmk_test_unread",
                     "reallife_lmk_test_no_district", "reallife_lmk_test_no_site", "reallife_lmk_test_back_done" })
        {
            Check(words[0].TryGetValue(notice, out string text) && text.Length <= 240, "landmarks texts: the notice " + notice + " is 240 characters at most in en");
        }
        foreach (string name in new[] { "reallife_lmk_name_tower", "reallife_lmk_name_mall", "reallife_lmk_name_stadium" })
        {
            Check(words[0].TryGetValue(name, out string text) && !text.Contains("Midtown") && !text.EndsWith(" Mall", StringComparison.Ordinal),
                "landmarks texts: " + name + " is Uptown's own, never the game's Midtown Mall");
        }
        Check(words[0].TryGetValue("reallife_guide_landmarks", out string page) && page.Contains("{lmk_capacity}") && page.Contains("{lmk_kick_off}")
              && ModSource(@"Landmarks\LandmarksGuide.cs").Contains(".Fact(\"lmk_capacity\""), "landmarks texts: the Guide's page has its figures, each a fact of the feature list");
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Uptown's landmarks:" }, { "pt", "Marcos de Uptown:" }, { "zh-cn", "上城地标：" }, { "de", "Uptowns Wahrzeichen:" }, { "fr", "Les monuments d'Uptown :" },
            { "es", "Los monumentos de Uptown:" }, { "ru", "Достопримечательности Аптауна:" }, { "nl", "De bezienswaardigheden van Uptown:" }, { "tr", "Uptown'ın simgeleri:" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            string next = words[l].TryGetValue("reallife_changes_next", out string row) ? row : string.Empty;
            int line = starts.TryGetValue(codes[l], out string start) ? next.IndexOf(start, StringComparison.Ordinal) : -1;
            int fix = FixMarks.TryGetValue(codes[l], out string mark) ? next.IndexOf(mark, StringComparison.Ordinal) : -1;
            Check(line >= 0 && (fix < 0 || line < fix) && next.IndexOf(start ?? "\0", line + 1, StringComparison.Ordinal) < 0,
                "landmarks texts: What's new's next row in " + codes[l] + " has the landmarks once, before its fixes");
        }
    }
}
