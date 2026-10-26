using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Schools;
using RealLifeOverhaul.Uptown;

// Birchwood School and the city's schools (Scripts/Schools): the campus on the plan's west square (the main school's lot,
// the hall and the junior block, the court, the green, the pitch and the fence with its three gates, all inside the block
// and clear of each other, the families' squares on the pavements); the market's rules (families shared by name, fee and
// scholarships, places kept, teaching, results day, inspections, the pay war both ways with the player's school among the
// rest, one set of levers for every owner, money, worth and the asking price, the opening sale, offers, a Going Public
// session buying and selling nothing, the same days giving the same schools); the district's lift; the line read back; the
// sources (game-free rules, the scene in SchoolsGame alone, one guarded Update, the bridges wired, every test's line); and the
// texts in every language with What's new in all nine.
internal static partial class Program
{
    private static void SchoolChecks()
    {
        SchoolGroundChecks();
        SchoolMarketChecks();
        SchoolParityChecks();
        SchoolDayChecks();
        SchoolLiftChecks();
        SchoolStoreChecks();
        SchoolSourceChecks();
        SchoolTextChecks();
        CampusLookChecks();
        CampusDressingChecks();
    }

    // ---------------------------------------------------------------- the campus on its block

    private static bool Within(float x, float z, float x0, float z0, float x1, float z1, float margin = 0f) =>
        x >= x0 - margin && x <= x1 + margin && z >= z0 - margin && z <= z1 + margin;

    private static void SchoolGroundChecks()
    {
        SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
        Check(Math.Abs(x1 - x0 - 70f) < 0.1f && Math.Abs(z1 - z0 - 119.9f) < 0.2f && Math.Abs(x0 + 642.22f) < 0.05f && Math.Abs(z0 - 625.43f) < 0.05f,
            "schools ground: the campus is the plan's west square, 70 by 120 m from Birch Street and Uptown Avenue (" + x0 + ", " + z0 + " to " + x1 + ", " + z1 + ")");
        Check(LayoutRules.Lots.All(l => !SchoolGrounds.On(l.X, l.Z)) && LayoutRules.Places.All(p => !SchoolGrounds.On(p.X, p.Z)), "schools ground: no lot or big place of the plan stands on the campus");
        SchoolGrounds.MainLot(out float mx, out float mz, out float facing, out float width, out float depth, out float inset);
        float m0 = mx - width * 0.5f, m1 = mx + width * 0.5f, n0 = mz - depth * 0.5f, n1 = mz + depth * 0.5f;
        Check(Within(m0, n0, x0, z0, x1, z1) && Within(m1, n1, x0, z0, x1, z1) && Math.Abs(facing - 180f) < 0.01f && Math.Abs(n0 - z0) < 0.01f,
            "schools ground: the main school's lot is inside the campus, its front on Uptown Avenue");
        Check(inset > 0f && inset < depth - SpacesRules.Grid.First(s => s.Size == SchoolGrounds.MainSize).Deep,
            "schools ground: a K1 office's depth fits the main school's lot behind its forecourt (" + inset + " m in, " + depth + " m deep)");
        var plots = new List<CampusBlockPlot>();
        SchoolGrounds.Blocks(plots);
        Check(plots.Count == 2, "schools ground: the hall and the junior block: " + plots.Count);
        var boxes = new List<(string, float, float, float, float)> { ("the main school", m0, n0, m1, n1) };
        foreach (CampusBlockPlot p in plots)
        {
            bool alongZ = p.Yaw == 90 || p.Yaw == 270;   // a plot facing a street: its width runs north, its depth across
            float hx = (alongZ ? p.Depth : p.Width) * 0.5f, hz = (alongZ ? p.Width : p.Depth) * 0.5f;
            boxes.Add((p.Name, p.X - hx, p.Z - hz, p.X + hx, p.Z + hz));
            Check(Within(p.X - hx, p.Z - hz, x0, z0, x1, z1) && Within(p.X + hx, p.Z + hz, x0, z0, x1, z1), "schools ground: " + p.Name + "'s plot is inside the campus");
        }
        for (int a = 0; a < boxes.Count; a++)
        {
            for (int b = a + 1; b < boxes.Count; b++)
            {
                bool apart = boxes[a].Item4 <= boxes[b].Item2 || boxes[b].Item4 <= boxes[a].Item2 || boxes[a].Item5 <= boxes[b].Item3 || boxes[b].Item5 <= boxes[a].Item3;
                Check(apart, "schools ground: " + boxes[a].Item1 + " and " + boxes[b].Item1 + " stand apart");
            }
        }
        var pieces = new List<CampusPiece>();
        var runs = new List<CampusFence>();
        SchoolGrounds.Pieces(pieces, runs);
        Check(pieces.All(p => SchoolGrounds.On(p.X, p.Z)), "schools ground: every piece stands on the campus");
        Check(pieces.Where(p => p.Kind != CampusPieceKind.Fence && p.Kind != CampusPieceKind.Post && p.Kind != CampusPieceKind.Bush).All(p => boxes.Skip(1).All(b => !Within(p.X, p.Z, b.Item2, b.Item3, b.Item4, b.Item5))),
            "schools ground: the court, hoops, trees, benches and pitch stand clear of the hall's and the junior block's plots (the dressing's own checks are in CampusDressingChecks)");
        Check(pieces.Count(p => p.Kind == CampusPieceKind.Court) == 2 && pieces.Count(p => p.Kind == CampusPieceKind.Hoop) == 2 && pieces.Count(p => p.Kind == CampusPieceKind.Pitch) == 1,
            "schools ground: a court of two half courts with two hoops, and the pitch");
        CampusPiece court = pieces.First(p => p.Kind == CampusPieceKind.Court);
        Check(Math.Abs(court.StretchX - court.StretchZ) < 0.001f && Math.Abs(court.StretchX - SchoolGrounds.CourtScale) < 0.001f
              && Math.Abs(SchoolGrounds.HalfCourtAcross * SchoolGrounds.CourtScale - SchoolGrounds.CourtWidth) < 0.05f && Math.Abs(2f * SchoolGrounds.HalfCourtDeep * SchoolGrounds.CourtScale - SchoolGrounds.CourtLength) < 0.05f,
            "schools ground: the game's half court stretched evenly by " + SchoolGrounds.CourtScale + " to " + SchoolGrounds.CourtWidth + " by " + SchoolGrounds.CourtLength / 2f + " m, two back to back");
        float courtHalfAcross = SchoolGrounds.CourtWidth * 0.5f;
        Check(court.X - courtHalfAcross > plots.Min(p => p.X) + 12f && court.X + courtHalfAcross < plots.Max(p => p.X) - 12f, "schools ground: the court lies in the courtyard between the hall and the junior block");
        CampusPiece pitch = pieces.First(p => p.Kind == CampusPieceKind.Pitch);
        Check(Within(pitch.X - SchoolGrounds.PitchWidth * 0.5f, pitch.Z - SchoolGrounds.PitchDepth * 0.5f, x0, z0, x1, z1) && Within(pitch.X + SchoolGrounds.PitchWidth * 0.5f, pitch.Z + SchoolGrounds.PitchDepth * 0.5f, x0, z0, x1, z1)
              && pitch.Z - SchoolGrounds.PitchDepth * 0.5f > pieces.Where(p => p.Kind == CampusPieceKind.Court).Max(p => p.Z) + SchoolGrounds.CourtLength * 0.25f, "schools ground: the playing field lies whole at the campus's north end, north of the court");
        // The fence: modules end to end along each run, a run's ends inside the block's edge, the three gates open.
        Check(runs.Count == 12, "schools ground: seven runs of fence round the block, broken by three gates, and five round the court (its north baseline open): " + runs.Count);
        float midX = (x0 + x1) * 0.5f;
        Check(!Fenced(runs, midX, z0 + SchoolGrounds.FenceIn) && !Fenced(runs, midX, z1 - SchoolGrounds.FenceIn), "schools ground: the main gate on Uptown Avenue and the field's gate on Summit Avenue are open");
        Check(Fenced(runs, x0 + SchoolGrounds.FenceIn, (z0 + z1) * 0.5f), "schools ground: Birch Street's side is fenced");
        int sideGaps = 0;
        for (float z = z0 + 2f; z < z1 - 2f; z += 0.5f)
        {
            sideGaps += Fenced(runs, x1 - SchoolGrounds.FenceIn, z) ? 0 : 1;
        }
        Check(sideGaps > 0 && sideGaps <= (int)(SchoolGrounds.SideGate * 2f) + 2, "schools ground: Elm Street's side has its one side gate: " + sideGaps * 0.5f + " m open");
        var fences = pieces.Where(p => p.Kind == CampusPieceKind.Fence).ToList();
        bool endToEnd = true;
        for (int i = 1; i < pieces.Count; i++)
        {
            if (pieces[i].Kind != CampusPieceKind.Fence || pieces[i - 1].Kind != CampusPieceKind.Fence)
            {
                continue;   // a run's modules come one after another; its posts close it
            }
            float d = (float)Math.Sqrt((pieces[i].X - pieces[i - 1].X) * (pieces[i].X - pieces[i - 1].X) + (pieces[i].Z - pieces[i - 1].Z) * (pieces[i].Z - pieces[i - 1].Z));
            endToEnd &= Math.Abs(d - SchoolGrounds.FenceModule) < 0.01f && Math.Abs(pieces[i].Yaw - pieces[i - 1].Yaw) < 0.01f;
        }
        Check(endToEnd && fences.Count > 200, "schools ground: the fence's modules stand end to end, a module's length apart: " + fences.Count);
        // A module reaches back along its own -Z from where it stands: turned with +Z along the run, so it covers the run.
        CampusFence south = runs[0];
        CampusPiece firstModule = fences[0];
        float yawRad = firstModule.Yaw * (float)Math.PI / 180f;
        float backX = firstModule.X - (float)Math.Sin(yawRad) * SchoolGrounds.FenceModule, backZ = firstModule.Z - (float)Math.Cos(yawRad) * SchoolGrounds.FenceModule;
        Check(Math.Abs(backZ - south.Z0) < 0.01f && backX >= south.X0 - 0.01f && backX < firstModule.X, "schools ground: the first module reaches back to the run's start, not past it");
        var crowds = new List<CampusCrowd>();
        SchoolGrounds.Crowds(crowds);
        Check(crowds.Count == SchoolGrounds.CrowdCount && crowds.Take(2).All(c => !SchoolGrounds.On(c.X, c.Z)) && crowds.Take(2).All(c => GroundSurvey.At(c.X, c.Z).Kind == GroundKind.Street || GroundSurvey.At(c.X, c.Z).Kind == GroundKind.Avenue)
              && crowds.Skip(2).All(c => c.Inside && SchoolGrounds.On(c.X, c.Z)),
            "schools ground: the families' two squares are on the pavements outside the gates, the seven others on the campus");
        SchoolGrounds.Gate(out float gx, out float gz, out _);
        Check(GroundSurvey.At(gx, gz).Kind == GroundKind.Avenue && gz < z0 && gz > z0 - LayoutRules.AvenueHalf + LayoutRules.AvenueKerb, "schools ground: the gate's spot is on Uptown Avenue's north pavement");
        Check(GroundSurvey.At((x0 + x1) * 0.5f, (z0 + z1) * 0.5f).Kind == GroundKind.Campus, "schools ground: the campus reads as the campus in What is this ground?");
        Check(SchoolGrounds.Street == "reallife:street_birchwoodgreen" && SchoolGrounds.Street.StartsWith("reallife:street_", StringComparison.Ordinal)
              && !SchoolGrounds.Street.StartsWith(LayoutStreets.Prefix, StringComparison.Ordinal), "schools ground: its street is the mod's own, outside Uptown's plan's streets");
        Check(ModSource(@"NewBuildings\NewBuildingsRules.cs").Contains("new MadeStreet(Prefix + \"birchwoodgreen\""), "schools ground: its street is among the mod's own made streets");
    }

    // True when a run of fence passes within half a metre of the spot.
    private static bool Fenced(List<CampusFence> runs, float x, float z)
    {
        foreach (CampusFence r in runs)
        {
            float dx = r.X1 - r.X0, dz = r.Z1 - r.Z0, len = dx * dx + dz * dz;
            float t = len <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((x - r.X0) * dx + (z - r.Z0) * dz) / len));
            float px = r.X0 + dx * t - x, pz = r.Z0 + dz * t - z;
            if (px * px + pz * pz < 0.25f)
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- the market's rules

    private static void SchoolMarketChecks()
    {
        var s = new SchoolState();
        Check(SchoolRules.Count == 12 && s.Files.All(f => f != null) && s.Campus.Owner == SchoolOwner.Council && SchoolRules.Def(0).Kind == SchoolKind.Campus
              && Enumerable.Range(1, 4).All(i => SchoolOwner.IsTrust(s.Files[i].Owner)) && Enumerable.Range(5, 7).All(i => !SchoolRules.Private(i)),
            "schools: twelve schools, Birchwood the council's at first, four trusts' and seven of the council's");
        var apps = new int[SchoolRules.Count];
        SchoolRules.Applications(s, apps);
        Check(apps.Sum() == SchoolRules.PrivatePool && Enumerable.Range(5, 7).All(i => apps[i] == 0), "schools: the city's paying families are shared among the private schools alone: " + apps.Sum());
        SchoolRules.Admit(s);
        Check(s.Files.All(f => f.Pupils <= f.Places && f.Pupils >= 0) && Enumerable.Range(0, 5).All(i => s.Files[i].Pupils + s.Files[i].Waiting == apps[i]),
            "schools: a term's start takes what fits each school's places, the rest wait");
        var dear = SchoolRules.Start(1);
        var cheap = SchoolRules.Start(1);
        cheap.Fee = dear.Fee - 20;
        var famous = SchoolRules.Start(1);
        famous.Reputation += 100;
        var giving = SchoolRules.Start(1);
        giving.Scholar += 10;
        Check(SchoolRules.Pull(cheap) > SchoolRules.Pull(dear) && SchoolRules.Pull(famous) > SchoolRules.Pull(dear) && SchoolRules.Pull(giving) > SchoolRules.Pull(dear)
              && SchoolRules.Pull(SchoolRules.Start(6)) == 0.0, "schools: a lower fee, a better name and more scholarships each pull more families; a council school pulls none");
        var lean = SchoolRules.Start(1);
        var staffed = SchoolRules.Start(1);
        lean.Teachers = 5;
        Check(SchoolRules.Teaching(staffed) > SchoolRules.Teaching(lean) && SchoolRules.Teaching(staffed) <= 100 && SchoolRules.Teaching(lean) >= 0, "schools: more teachers for the pupils teach better, within 0 to 100");
        Check(SchoolRules.ResultsOf(staffed, 3) == SchoolRules.ResultsOf(SchoolRules.Start(1), 3) && Enumerable.Range(1, 30).All(y => SchoolRules.ResultsOf(staffed, y) >= 0 && SchoolRules.ResultsOf(staffed, y) <= 100),
            "schools: results day's scores are the same for the same school and year, within 0 to 100");
        var grades = new HashSet<SchoolGrade>();
        for (int skill = 0; skill <= 100; skill += 5)
        {
            var f = SchoolRules.Start(1);
            f.Skill = skill;
            f.HeadSkill = skill;
            f.Results = skill;
            grades.Add(SchoolRules.GradeOf(f));
        }
        Check(grades.Count == 4, "schools: the inspectors give each of their four verdicts somewhere on the scale: " + grades.Count);
        var drift = SchoolRules.Start(1);
        drift.Reputation = 100;
        int target = SchoolRules.ReputationTarget(drift);
        SchoolRules.Drift(drift);
        Check(drift.Reputation > 100 && drift.Reputation <= target, "schools: a school's name drifts toward what its figures earn");
        var hiring = SchoolRules.Start(1);
        hiring.Teachers = 2;
        int hired = SchoolRules.Staff(hiring);
        Check(hired == SchoolRules.HireMost && hiring.Teachers == 2 + SchoolRules.HireMost, "schools: a school short of teachers takes on a week's most from the city's pool");
        var overStaffed = SchoolRules.Start(1);
        overStaffed.Teachers = SchoolRules.Need(overStaffed) + 10;
        SchoolRules.Staff(overStaffed);
        Check(overStaffed.Teachers == SchoolRules.Need(overStaffed) + 1, "schools: a school well over its need lets the spare go but one");
        var money = SchoolRules.Start(1);
        money.Scholar = 10;
        long paying = money.Pupils - money.Pupils * 10 / 100;
        Check(SchoolRules.Income(money) == paying * money.Fee && SchoolRules.Income(SchoolRules.Start(6)) == (long)SchoolRules.Start(6).Pupils * SchoolRules.CouncilPerPupil,
            "schools: scholars pay nothing; a council school lives on the council's grant");
        var worth = SchoolRules.Start(0);
        Check(SchoolRules.Valuation(worth) >= SchoolRules.ValueFloor && SchoolRules.Asking(worth) == SchoolRules.Valuation(worth) * 115 / 100 && SchoolRules.SalePrice(worth) == SchoolRules.Valuation(worth) * 90 / 100,
            "schools: a school is worth its profit's days, never under the floor; its owner asks 115%, the trusts pay the player 90%");
        var fees = SchoolRules.Start(1);
        fees.Fee = SchoolRules.FeeMost;
        Check(!SchoolRules.StepFee(fees, +1) && fees.Fee == SchoolRules.FeeMost && SchoolRules.StepFee(fees, -1) && fees.Fee == SchoolRules.FeeMost - SchoolRules.FeeStep,
            "schools: the fee moves a step within its bounds");
        var classes = SchoolRules.Start(0);
        classes.Places = SchoolRules.Def(0).Capacity;
        Check(!SchoolRules.StepClass(classes, +1) && SchoolRules.StepClass(classes, -1) && classes.Places == SchoolRules.Def(0).Capacity - SchoolRules.ClassSize && classes.Pupils <= classes.Places,
            "schools: a class opens up to the buildings' places and closes with its pupils kept within them");
        SchoolRules.Candidate(0, 5, 0, out int seedA, out int skillA);
        SchoolRules.Candidate(0, 5, 0, out int seedB, out int skillB);
        SchoolRules.Candidate(0, 6, 0, out int seedC, out _);
        Check(seedA == seedB && skillA == skillB && seedC != seedA && skillA >= 40 && skillA <= 90, "schools: a week's candidates are the same all week, other ones the next, skill 40 to 90");
        var timing = new SchoolState();
        var watch = Stopwatch.StartNew();
        for (int day = 1; day <= 365; day++)
        {
            SchoolRules.Day(timing, day, 60, false, 0);
        }
        Check(watch.Elapsed.TotalMilliseconds < 250.0, "schools: a year of days' passes in under 250 ms: " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
    }

    // ---------------------------------------------------------------- the owner's rule: one set of rules for all

    private static void SchoolParityChecks()
    {
        // The pay war both ways: the player's school takes teachers when it pays a tenth more, and loses them when it pays less.
        var s = new SchoolState();
        SchoolRules.Admit(s);
        s.Campus.Owner = SchoolOwner.Player;
        s.Campus.PayBp = SchoolRules.PayMost;
        s.Campus.Teachers = 2;
        int otherBefore = s.Files.Where(f => f.Id != 0).Sum(f => f.Teachers);
        SchoolRules.Poach(s, 10);
        Check(s.Campus.Teachers > 2 && s.Files.Where(f => f.Id != 0).Sum(f => f.Teachers) < otherBefore && s.News.Any(n => n.Kind == SchoolRules.NewsPoached && n.School == 0),
            "schools parity: the player's school paying more takes teachers from the lowest payer, as a trust would");
        var t = new SchoolState();
        SchoolRules.Admit(t);
        t.Campus.Owner = SchoolOwner.Player;
        t.Campus.PayBp = SchoolRules.PayLeast;
        t.Files[1].PayBp = SchoolRules.PayMost;
        t.Files[1].Teachers = 1;
        int campusBefore = t.Campus.Teachers;
        SchoolRules.Poach(t, 10);
        Check(t.Campus.Teachers < campusBefore && t.News.Any(n => n.Kind == SchoolRules.NewsPoached && n.Other == 0), "schools parity: a trust paying more takes the player's teachers, as the player could take theirs");
        // The trusts' week pulls the player's levers: the same steps and bounds.
        var trust = SchoolRules.Start(1);
        trust.Waiting = trust.Places;
        int fee = trust.Fee;
        SchoolRules.Policy(trust, 1);
        var byHand = SchoolRules.Start(1);
        SchoolRules.StepFee(byHand, +1);
        Check(trust.Fee == byHand.Fee && trust.Fee == fee + SchoolRules.FeeStep, "schools parity: a trust raises its fee with a waiting list by the player's own step");
        var player = SchoolRules.Start(0);
        player.Owner = SchoolOwner.Player;
        player.Waiting = player.Places;
        int playerFee = player.Fee, playerPlaces = player.Places;
        SchoolRules.Policy(player, 1);
        Check(player.Fee == playerFee && player.Places == playerPlaces, "schools parity: the week's running never moves the player's own levers");
        Check(ModSource(@"Schools\SchoolsDirector.Market.cs").Contains("SchoolRules.StepFee(f, way)") && ModSource(@"Schools\SchoolsDirector.Market.cs").Contains("SchoolRules.StepClass(f, way)")
              && ModSource(@"Schools\SchoolsDirector.Market.cs").Contains("SchoolRules.StepPay(f, way)") && ModSource(@"Schools\SchoolsDirector.Market.cs").Contains("SchoolRules.StepScholar(f, way)"),
            "schools parity: the player's levers are the rules' own steps, the ones the trusts' week pulls");
        // Buying: the player pays a trust's asking price; a trust buying from another pays the same.
        var market = new SchoolState();
        long asking = SchoolRules.Asking(market.Files[3]);
        long buyerCash = market.Files[1].Cash;
        SchoolRules.Sell(market, 3, 0, asking, 5);
        Check(market.Files[3].Owner == 0 && market.Files[1].Cash == buyerCash - asking, "schools parity: a trust buys another's school at the owner's asking price, paid from its own money");
    }

    // ---------------------------------------------------------------- the days

    private static void SchoolDayChecks()
    {
        var s = new SchoolState();
        SchoolDayFlags first = SchoolRules.Day(s, 1, 60, false, 0);
        Check(s.Opened == 1 && s.News.Any(n => n.Kind == SchoolRules.NewsOpened) && (first & SchoolDayFlags.TermStarted) != 0 && s.Term == 0,
            "schools days: the campus opens on the first day's pass, in the City Ledger, and the term starts");
        for (int day = 2; day < 1 + SchoolRules.SaleDays; day++)
        {
            SchoolRules.Day(s, day, 60, false, 0);
        }
        Check(s.Campus.Owner == SchoolOwner.Council, "schools days: the council keeps Birchwood while it is offered");
        int saleDay = 1 + SchoolRules.SaleDays;
        while (!SchoolRules.WeekDay(saleDay))
        {
            saleDay++;
        }
        for (int day = 1 + SchoolRules.SaleDays; day <= saleDay; day++)
        {
            SchoolRules.Day(s, day, 60, false, 0);
        }
        Check(SchoolOwner.IsTrust(s.Campus.Owner) && s.News.Any(n => n.Kind == SchoolRules.NewsSold && n.School == 0), "schools days: after the sale's days a trust buys Birchwood at the week's end: " + s.Campus.Owner);
        var session = new SchoolState();
        for (int day = 1; day <= 70; day++)
        {
            SchoolRules.Day(session, day, 60, true, 0);
        }
        Check(session.Campus.Owner == SchoolOwner.Council && !session.News.Any(n => n.Kind == SchoolRules.NewsSold), "schools days: in a Going Public session nothing is bought or sold");
        // The player's school: never sold without them; a trust's offer after its days; the week's book.
        var mine = new SchoolState();
        SchoolRules.Day(mine, 1, 60, false, 0);
        SchoolRules.Sell(mine, 0, SchoolOwner.Player, 0, 1);
        bool offered = false, booked = false;
        for (int day = 2; day <= 120; day++)
        {
            SchoolDayFlags f = SchoolRules.Day(mine, day, 60, false, 0);
            offered |= (f & SchoolDayFlags.Offer) != 0;
            booked |= (f & SchoolDayFlags.WeekBooked) != 0;
            mine.Campus.Week = 0;
        }
        Check(mine.Campus.Owner == SchoolOwner.Player && offered && booked && mine.OwnedSince == 1, "schools days: the player's school stays theirs, gets a trust's offer in time and is booked each week");
        // Results day once a year at the end of the summer term; each school inspected on its own day.
        var year = new SchoolState();
        int results = 0, inspected = 0;
        for (int day = 1; day <= 120; day++)
        {
            SchoolDayFlags f = SchoolRules.Day(year, day, 60, false, 0);
            results += (f & SchoolDayFlags.Results) != 0 ? 1 : 0;
            inspected += (f & SchoolDayFlags.Inspected) != 0 ? 1 : 0;
        }
        Check(results == 2 && year.Files.All(f => f.Results >= 0) && year.News.Count(n => n.Kind == SchoolRules.NewsResults) >= 1, "schools days: results day comes once a year: " + results + " in two years");
        Check(inspected >= 2 && year.Campus.Grade != 0, "schools days: Birchwood is inspected on its days: " + inspected);
        // The same days give the same schools on every machine, whatever came before in another run.
        var a = new SchoolState();
        var b = new SchoolState();
        for (int day = 1; day <= 200; day++)
        {
            SchoolRules.Day(a, day, 60, true, 1);
            SchoolRules.Day(b, day, 60, true, 1);
        }
        Check(SchoolStore.Write(a) == SchoolStore.Write(b), "schools days: two machines with the same line and days have the same schools");
        // The council's money for schools: its schools' teachers follow it.
        var council = new SchoolState();
        for (int day = 1; day <= 120; day++)
        {
            SchoolRules.Day(council, day, 60, false, 1);
        }
        Check(council.Files[6].Skill > SchoolRules.Def(6).Skill, "schools days: the council's grants for schools lift its schools' teachers");
    }

    private static void SchoolLiftChecks()
    {
        SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
        Check(SchoolRules.Lift(x0 - 1f, z0, 900, false, x0, z0, x1, z1) == 0, "schools lift: nothing before the campus opens");
        Check(SchoolRules.Lift(x0 - 1f, z0, 590, true, x0, z0, x1, z1) == 0 && SchoolRules.Lift(x0 - 1f, z0, 650, true, x0, z0, x1, z1) == 2
              && SchoolRules.Lift(x0 - 1f, z0, 750, true, x0, z0, x1, z1) == 4 && SchoolRules.Lift(x0 - 1f, z0, 850, true, x0, z0, x1, z1) == 6,
            "schools lift: 2, 4 or 6 points next to the campus with a name of 600, 700 or 800");
        bool never = true;
        for (int metres = 0; metres <= 3 * SchoolRules.LiftReach; metres += 5)
        {
            int lift = SchoolRules.Lift(x0 - metres, z0, 850, true, x0, z0, x1, z1);
            never &= lift >= 0 && lift <= 6 && (metres < SchoolRules.LiftReach || lift == 0);
        }
        Check(never && SchoolRules.Lift(x0 - SchoolRules.LiftReach * 0.5f, z0, 850, true, x0, z0, x1, z1) == 3,
            "schools lift: falling to none at " + SchoolRules.LiftReach + " m, and none further out");
        string director = ModSource(@"Schools\SchoolsDirector.cs");
        Check(director.Contains("if (!Settings.Schools || SchoolsGame.InSession)") && director.Contains("DistrictBuildings.TrafficLift = lot => (before != null ? before(lot) : 0) + LiftOf(lot);"),
            "schools lift: chained after the landmarks', none with the option off or in a Going Public session");
    }

    // ---------------------------------------------------------------- the line

    private static void SchoolStoreChecks()
    {
        var s = new SchoolState();
        for (int day = 1; day <= 90; day++)
        {
            SchoolRules.Day(s, day, 60, false, 0);
        }
        s.Children.Add("Ann;a|b^c");
        s.Children[0] = SchoolStore.Clean(s.Children[0]);
        string line = SchoolStore.Write(s);
        SchoolState back = SchoolStore.Read(line, out int version);
        Check(back != null && version == SchoolStore.Version && SchoolStore.Write(back) == line, "schools store: the line reads back whole");
        Check(back != null && back.Children.Count == 1 && back.Children[0] == "Annabc", "schools store: a child's name keeps none of the line's own separators");
        Check(SchoolStore.Read("v=1", out _) != null && SchoolStore.Read("garbage", out _) == null && SchoolStore.Read(null, out _) == null, "schools store: an empty line is a fresh one, a strange one unread");
        var data = new Dictionary<string, string>();
        Check(SchoolStore.Load(data, SchoolStore.Key, out int none) != null && none == 0, "schools store: a save without the line is a new one: the campus comes at the next load");
        string later = line.Replace("^s:", "^z:something new^s:");
        string[] parts = later.Split('^');
        string schools = parts.First(p => p.StartsWith("s:"));
        string longer = schools.Replace(";", "|99;") + "|99";
        SchoolState newer = SchoolStore.Read(later.Replace(schools, longer).Replace("v=1", "v=2"), out int v2);
        Check(newer != null && v2 == 2 && SchoolStore.Write(newer).Replace("v=1", "v=2") == line.Replace("v=1", "v=2"),
            "schools store: a later version's line (a new part, a field more on each record) is read as far as this one knows it");
        string shorter = line.Replace(schools, "s:0|-1|600");
        SchoolState partial = SchoolStore.Read(shorter, out _);
        Check(partial != null && partial.Campus.Places == SchoolRules.Def(0).Places, "schools store: a record too short to read leaves the school as it starts");
    }

    // ---------------------------------------------------------------- the sources

    private static void SchoolSourceChecks()
    {
        string[] files =
        {
            "SchoolRules.cs", "SchoolState.cs", "SchoolStore.cs", "SchoolGrounds.cs", "SchoolGrounds.Dressing.cs", "SchoolsBridges.cs", "SchoolsGame.cs", "SchoolFloors.cs", "SchoolsDirector.cs",
            "SchoolsDirector.Campus.cs", "SchoolsDirector.Market.cs", "SchoolsDirector.Paper.cs", "SchoolsDirector.Tests.cs", "SchoolsPage.cs", "SchoolsText.cs", "SchoolsGuide.cs"
        };
        var code = new Dictionary<string, string>();
        foreach (string name in files)
        {
            code[name] = ModSource(@"Schools\" + name);
            Check(code[name].Length > 0, "schools sources: " + name + " is there");
        }
        string all = string.Join("\n", code.Values);
        Check(!Regex.IsMatch(all, @"using System\.Linq|\.Where\(|\.Select\(|\.OrderBy\(|\.ToList\(\)|\.ToArray\(\)"), "schools sources: no LINQ");
        Check(!Regex.IsMatch(all, @"FindObjectsOfType|FindObjectsByType|GameObject\.Find\b|OverlapSphere\(|RaycastAll\(|\bHarmony\b|\bMPClient\b|\bMPServer\b|DlcIsOwned|requiredDLC|SteamApps"),
            "schools sources: no whole-scene search, no patch, nothing sent over the network, no DLC read");
        Check(!Regex.IsMatch(all, @"\bTODO\b|\bFIXME\b|NotImplementedException"), "schools sources: no stub");
        foreach (string name in new[] { "SchoolRules.cs", "SchoolState.cs", "SchoolStore.cs", "SchoolGrounds.cs", "SchoolGrounds.Dressing.cs" })
        {
            Check(!Regex.IsMatch(code[name], @"\bUnityEngine\b|\bRandom\.|\bnew Random\b|DateTime|TickCount|\bTime\.|GetHashCode"), "schools sources: " + name + " is game-free and leaves nothing to chance");
        }
        foreach (string name in files)
        {
            if (name == "SchoolsGame.cs")
            {
                continue;
            }
            Check(!Regex.IsMatch(code[name], @"new GameObject\(|AddComponent<|PeopleGame\.|WalkGame\.|PlayerHelper\.|BuildingManager\.|\.trafficIndex|TimeHelper\.|\bGameManager\.|Notifications\.Show"),
                "schools sources: " + name + " leaves the game's scene, the player, the clock and money to SchoolsGame");
            Check(!Regex.IsMatch(code[name], @"\b(FamilyState|FamilyDirector|CareersDirector|CivicDirector|LandmarksDirector)\b"), "schools sources: " + name + " reaches family life, careers and the council only through its bridges");
        }
        string director = code["SchoolsDirector.cs"];
        Check(Regex.Matches(director, @"private void Update\(\)").Count == 1 && director.Contains("using var hotSpot = HotSpots.Time(HotUpdate);") && director.Contains("Guard.Fail(FrameGuard, e, this);")
              && director.Contains("if (now < _next)"), "schools sources: one guarded Update, a time comparison a frame");
        Check(!Regex.IsMatch(all, @"void (LateUpdate|FixedUpdate|OnGUI)\("), "schools sources: no other frame method");
        string wiring = ModSource(@"Wiring\Wiring.SchoolLinks.cs");
        Check(wiring.Contains("SchoolsBridges.Children = ") && wiring.Contains("SchoolsBridges.SetSchool = ") && wiring.Contains("SchoolsBridges.Council = ") && wiring.Contains("FamilyBridges.School = ")
              && wiring.Contains("CareersBridges.SchoolsTab = ") && wiring.Contains("News.Extra = Paper;") && wiring.Contains("SchoolsBridges.Reset();") && wiring.Contains("News.Extra = _paperBefore;"),
            "schools sources: every bridge wired, and put back");
        Check(ModSource(@"Wiring\Wiring.cs").Contains("Step(\"the schools\", SchoolLinks.Connect);") && ModSource(@"Wiring\Wiring.cs").Contains("Step(\"the schools\", SchoolLinks.Disconnect);"),
            "schools sources: the wiring connects and disconnects the schools");
        string family = ModSource(@"Family\Children.cs");
        Check(family.Contains("int fee = CampusFee(c);") && family.Contains("AtSchool(Errands.Add(Errands.SchoolRun") && ModSource(@"Family\Children.Growing.cs").Contains("SchoolStanding(c)")
              && ModSource(@"Family\FamilyBridges.cs").Contains("public interface ISchoolBridge"), "schools sources: family life charges the campus's fee, sends the school run there and reads its reports through its bridge");
        string tests = code["SchoolsDirector.Tests.cs"], guide = code["SchoolsGuide.cs"];
        foreach (string line in new[] { "census", "take me", "school run", "results day", "inspection", "term", "make it mine", "enrol", "back" })   // the look's two are in CampusLookChecks
        {
            Check(tests.Contains("\"Schools check (" + line) && guide.Contains("\"Schools check (" + line + ")\""), "schools sources: the test '" + line + "' writes its line and is in Options > Tests");
        }
        Check(ModSource(@"Core\Settings.cs").Contains("SchoolsId = \"uptown_campus\"") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"schools\").For(\"schools\").Main(\"uptown_campus\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"schools\", CityScope.NewYork"), "schools sources: its option under the district's options, its city line");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").IndexOf("Guard.Add<Schools.SchoolsDirector>", StringComparison.Ordinal) < ModSource(@"Core\RealLifeOverhaulMod.cs").IndexOf("Guard.Add<NewBuildings.NewBuildingsDirector>", StringComparison.Ordinal),
            "schools sources: the director is there before the made buildings are made, so its main school is declared in time");
        // The hall and the junior block (Player.log, 26 Oct 2026: "asked of the copy engine (0)"): the director is made before the copy
        // engine's, which takes no request until it is made, so a request in Awake was answered with none. They are asked behind the
        // loading screen, once the main school stands, a slot a plot kept even when refused, asked again when the engine takes requests,
        // and each outcome written once with the design or the reason.
        string Bare(string text) => Regex.Replace(text, @"//[^\n]*", " ");
        string campusDirector = Bare(ModSource(@"Schools\SchoolsDirector.cs")), campusPart = Bare(ModSource(@"Schools\SchoolsDirector.Campus.cs"));
        Match awake = Regex.Match(campusDirector, @"private void Awake\(\)\s*\{[\s\S]*?\n        \}");
        Check(awake.Success && !awake.Value.Contains("Ask()") && awake.Value.Contains("SchoolsGame.Declare(Wanted);"),
            "schools sources: the hall and the junior block are not asked of the copy engine in Awake, when it does not take requests yet");
        Match atLoad = Regex.Match(campusPart, @"private bool AtLoad\(\)\s*\{[\s\S]*?\n        \}");
        Check(atLoad.Success && Regex.IsMatch(atLoad.Value, @"main == null \|\| main\.Controller == null[\s\S]*cut = StandLooks\(main\) \|\| SchoolsGame\.Cut\(main\);\s*Ask\(\);"),
            "schools sources: they are asked behind the loading screen, once the main school stands");
        Check(campusPart.Contains("_copies.Add(own ? null : SchoolsGame.Plot(_plots[i]));") && !Regex.IsMatch(campusPart, @"handle != null\)\s*\{\s*_copies\.Add"),
            "schools sources: a request the copy engine refused keeps its slot, so the plot is asked again and its outcome told");
        Check(campusPart.Contains("handle == null && SchoolsGame.CopiesAvailable") && campusPart.Contains("SchoolGrounds.BlockLine(") && Regex.IsMatch(campusPart, @"private void Campus\(\)[\s\S]*?Blocks\(\);"),
            "schools sources: each is asked again once the engine takes requests, and its outcome is written once, from the second's look");
        Check(campusPart.Contains("Asked() + \" of \" + _plots.Count") && !campusPart.Contains("asked of the copy engine (\" + _copies.Count"),
            "schools sources: the load's line counts the requests the engine took, not the slots");
        Check(SchoolGrounds.BlockLine("the hall", true, true, "SM_Office_7", null) == "the hall stands as the city's design SM_Office_7"
              && SchoolGrounds.BlockLine("the hall", true, false, null, "no design that can be copied fits 30 by 24 m") == "the hall was not made: no design that can be copied fits 30 by 24 m"
              && SchoolGrounds.BlockLine("the junior block", false, false, null, "its option is off") == "the junior block was not asked of the copy engine: its option is off"
              && SchoolGrounds.BlockLine("the hall", true, false, null, null) == "the hall was not made: no reason given",
            "schools ground: a building of the campus is told in one line: the design it wears, or why it was not made or asked");
        // The bench (Player.log, 26 Oct 2026: "not found: SleepingBench"): one place for the game's pavement bench; the campus's, the streets' and the parks' the same.
        string pieceCode = Bare(ModSource(@"Uptown\GamePieces.cs")), gameCode = Bare(ModSource(@"Schools\SchoolsGame.cs"));
        string streetCode = Bare(ModSource(@"Uptown\StreetsGame.cs")), parkCode = Bare(ModSource(@"Uptown\ParksGame.cs"));
        Check(Regex.IsMatch(gameCode, @"Kind = \(int\)CampusPieceKind\.Bench, Root = GamePieces\.BenchRoot, Name = GamePieces\.BenchName, X = GamePieces\.BenchX, Z = GamePieces\.BenchZ,[^}]*Within = GamePieces\.BenchReach")
              && !gameCode.Contains("-397.79f") && !gameCode.Contains("Name = \"SleepingBench\""),
            "schools sources: the campus's bench is the game's pavement bench, asked for where the street furniture asks for it, not under BuildingBlocks");
        Check(pieceCode.Contains("BenchRoot = \"SleepingBench\"") && pieceCode.Contains("BenchX = 214.40f") && pieceCode.Contains("BenchZ = 99.39f") && pieceCode.Contains("BenchReach = 40f")
              && streetCode.Contains("Root = GamePieces.BenchRoot, Name = GamePieces.BenchName, X = GamePieces.BenchX, Z = GamePieces.BenchZ") && parkCode.Contains("Root = GamePieces.BenchRoot, Name = GamePieces.BenchName, X = GamePieces.BenchX, Z = GamePieces.BenchZ"),
            "schools sources: the bench's place is the same for the campus, the street furniture and the parks");
        // Player.log, 26 Oct 2026: the bench's own scene root was not found at the load while Vegetation, Trash Cans and Street Lamp beside it were: all three look in Madison Square Park's too.
        Check(pieceCode.Contains("BenchAltRoot = \"Madison Square Park\"") && pieceCode.Contains("BenchAltX = 192.79f") && pieceCode.Contains("BenchAltZ = 269.00f") && pieceCode.Contains("second[t]")
              && streetCode.Contains("AltRoot = GamePieces.BenchAltRoot") && parkCode.Contains("AltRoot = GamePieces.BenchAltRoot") && gameCode.Contains("AltRoot = GamePieces.BenchAltRoot"),
            "schools sources: the bench is looked for in Madison Square Park's group too when its own root is not there (the campus's, the streets' and the parks')");
    }

    // ---------------------------------------------------------------- the texts

    private static void SchoolTextChecks()
    {
        var keys = new SortedSet<string>();
        foreach (string file in new[] { @"Schools\SchoolsText.cs", @"Schools\SchoolsGuide.cs", @"Schools\SchoolRules.cs", @"Careers\CampusApp.cs", @"Careers\CampusApp.Schools.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(file), "\"(reallife_(?:schools_|meta_title_schools|guide_schools|transaction_school_|newbuildings_street_birchwoodgreen)[a-z0-9_]*)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_opt_sec_schools");
        keys.Add("reallife_opt_tip_uptown_campus");
        Check(keys.Count > 120, "schools texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("schools texts", words, codes))
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
                    Check(false, "schools texts: " + key + " is in " + codes[l] + ".json");
                    continue;
                }
                if (value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Replace("\n- ", "\n").StartsWith("- ") || value.Replace("\n- ", "\n").Contains(" - "))
                {
                    styled++;
                    Check(false, "schools texts: " + key + " has no exclamation mark or dash in " + codes[l]);
                }
                var mine = new SortedSet<string>(Regex.Matches(value, @"\{[a-z_]+\}").Select(m => m.Value));
                var english = new SortedSet<string>(Regex.Matches(words[0][key], @"\{[a-z_]+\}").Select(m => m.Value));
                if (!mine.SetEquals(english))
                {
                    places++;
                    Check(false, "schools texts: " + key + " has the English text's placeholders in " + codes[l]);
                }
            }
            Check(missing == 0 && styled == 0 && places == 0, "schools texts: every key of the schools is in " + codes[l] + ".json, in the style, with its placeholders");
        }
        foreach (string notice in keys.Where(k => k.Contains("_notice_") || k.Contains("_test_")))
        {
            Check(words[0].TryGetValue(notice, out string text) && text.Length <= 240, "schools texts: the notice " + notice + " is 240 characters at most in en");
        }
        var lines = new Dictionary<string, string>
        {
            { "en", "Birchwood School: a school for ages 5 to 17 on Uptown's west square" }, { "pt", "Escola Birchwood: uma escola para idades de 5 a 17 anos" },
            { "zh-cn", "伯奇伍德学校：Uptown 西侧广场上一所招收 5 到 17 岁学生的学校" }, { "de", "Birchwood School: eine Schule für 5 bis 17 Jahre" },
            { "fr", "Birchwood School : une école de 5 à 17 ans" }, { "es", "Birchwood School: una escuela de 5 a 17 años" }, { "ru", "Школа Birchwood: школа для детей от 5 до 17 лет" },
            { "nl", "Birchwood School: een school voor 5 tot 17 jaar" }, { "tr", "Birchwood School: Uptown'un batı meydanında" }
        };
        for (int l = 0; l < codes.Length; l++)
        {
            Check(lines.TryGetValue(codes[l], out string wanted) && WhatsNewHas(words[l], wanted), "schools texts: What's new has the campus in " + codes[l]);
        }
        Check(codes.Length == 9, "schools texts: What's new is read in all nine languages: " + codes.Length);
        Check(SchoolRules.Catalogue.All(d => keys.Contains(d.Key)) && SchoolRules.TrustKeys.All(keys.Contains), "schools texts: every school's and trust's name is a text");
    }
}
