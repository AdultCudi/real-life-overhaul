using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Schools;
using RealLifeOverhaul.Uptown;

// Birchwood School after the owner's test of 26 Oct 2026 (Player_campus_26oct.log and the screenshots beside it): the game's building card read as an office
// that was closed on a Friday afternoon; the court's plane lay under the lawn; the block was bare lawn. The checks: the card's rule (open or closed by the
// school's own hours, its name, its kind, who runs it), the court (two half courts, their hoops and nets and fence, above the grass), the layout (paths that
// reach the main door, the court and the field from the gates, clearances from the fence, the buildings, the paths and the field's margin), the people (who
// is out when, and where), the sources that say each is wired, and the texts. Each was made to fail once against the old code (the report says how).
internal static partial class Program
{
    private static void CampusDressingChecks()
    {
        CampusCardChecks();
        CampusCourtChecks();
        CampusLayoutChecks();
        CampusPeopleChecks();
        CampusPageChecks();
        CampusGroundChecks();
        CampusWiringChecks();
    }

    // ---------------------------------------------------------------- the card

    private static void CampusCardChecks()
    {
        // Friday day 47 at 14:56 (the owner's screenshot): the school is open, closing in 4 hours at 18:00.
        Check(SchoolRules.SchoolDay(47) && SchoolRules.Open(47, 14) && !SchoolRules.Open(47, 18) && !SchoolRules.Open(47, 6) && SchoolRules.Open(47, 7) && SchoolRules.Open(47, 17),
            "campus card: the school is open on a Friday from 07:00 to 18:00, and on Friday at 14:56 it is open");
        Check(!SchoolRules.Open(48, 12) && !SchoolRules.Open(49, 12) && SchoolRules.Open(50, 12) && SchoolRules.Open(1, 9) && SchoolRules.Open(54, 9) && !SchoolRules.Open(54, 23),
            "campus card: closed on a Saturday and a Sunday, open on a Monday, closed at night");
        Check(SchoolRules.OpensAt == 7 && SchoolRules.ClosesAt == 18 && ModSource(@"Schools\SchoolFloors.cs").Contains("OpenFrom = SchoolRules.OpensAt, OpenTo = SchoolRules.ClosesAt"),
            "campus card: the door's hours (the floors core's) and the card's are the one rule");
        int h = SchoolRules.HoursToChange(47, 14, out int at);
        Check(h == 4 && at == 18, "campus card: open at 14:56 on a Friday, it closes in 4 hours at 18:00: " + h + " h to " + at);
        h = SchoolRules.HoursToChange(47, 17, out at);
        Check(h == 1 && at == 18, "campus card: open at 17:00 it closes in 1 hour");
        h = SchoolRules.HoursToChange(47, 18, out at);
        Check(h == 61 && at == 7, "campus card: shut on Friday at 18:00 it opens again at 07:00 on Monday, 61 hours on: " + h);
        h = SchoolRules.HoursToChange(49, 22, out at);
        Check(h == 9 && at == 7, "campus card: shut on a Sunday night it opens in 9 hours at 07:00");
        h = SchoolRules.HoursToChange(50, 6, out at);
        Check(h == 1 && at == 7, "campus card: shut at 06:00 on a school day it opens in 1 hour");
        bool consistent = true;
        for (int day = 1; day <= 14; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                bool open = SchoolRules.Open(day, hour);
                int hours = SchoolRules.HoursToChange(day, hour, out int when);
                // The state flips exactly after that many hours, never sooner.
                int d2 = day, h2 = hour;
                for (int k = 0; k < hours; k++)
                {
                    consistent &= SchoolRules.Open(d2, h2) == open;
                    h2++;
                    if (h2 == 24)
                    {
                        h2 = 0;
                        d2++;
                    }
                }
                consistent &= SchoolRules.Open(d2, h2) != open && h2 == when;
            }
        }
        Check(consistent, "campus card: for every hour of two weeks the state flips exactly when the card says, at the hour it says");
        Check(SpecialFacts.StateKey(CardState.Open, 4) == "buildingresume_open_closing_in_hours" && SpecialFacts.StateKey(CardState.Open, 1) == "buildingresume_open_closing_in_hour"
              && SpecialFacts.StateKey(CardState.Closed, 61) == "buildingresume_closed_opening_in_hours" && SpecialFacts.StateKey(CardState.Closed, 1) == "buildingresume_closed_opening_in_hour"
              && SpecialFacts.StateKey(CardState.None, 4) == null, "campus card: the state lines are the game's own for a business open or closed (BuildingResume.UpdateOpenStateLabel)");
        // Who runs it: the council, a trust or the player, from the school's file.
        string text = ModSource(@"Schools\SchoolsText.cs"), campus = Regex.Replace(ModSource(@"Schools\SchoolsDirector.Campus.cs"), @"//[^
]*", " "), names = ModSource(@"Core\SpecialNames.cs"), floors = ModSource(@"Schools\SchoolFloors.cs");
        Check(text.Contains("CardRunPlayer = \"reallife_schools_card_run_player\"") && text.Contains("CardRunCouncil = \"reallife_schools_card_run_council\"") && text.Contains("CardRunTrust = \"reallife_schools_card_run_trust\"")
              && text.Contains("CardType = \"reallife_schools_card_type\""), "campus card: the kind and the three owner lines are texts of the schools");
        Check(Regex.IsMatch(text, @"CardRun\(int owner\) => owner == SchoolOwner\.Player \? T\(CardRunPlayer\) : owner == SchoolOwner\.Council \? T\(CardRunCouncil\) : Fill\(CardRunTrust"),
            "campus card: who runs it reads the school's owner: the player, the council, or a trust by name");
        Check(Regex.IsMatch(campus, @"SpecialBuildings\.Set\(SchoolsGame\.MainId, main\.Controller, SpecialKind\.Campus, SchoolsText\.Campus, SchoolsText\.CardType, \(\) => Settings\.Schools\)")
              && campus.Contains("SpecialBuildings.Facts(SchoolsGame.MainId, SayCard);") && campus.Contains("FileCard(main);") && campus.Contains("SpecialBuildings.Remove(SchoolsGame.MainId);"),
            "campus card: the main school is on the game's card by the mod's one card path (name, kind, facts) at a load, and off it when the campus goes");
        Check(Regex.IsMatch(campus, @"SchoolRules\.Open\(day, hour\)[\s\S]*facts\.State = open \? CardState\.Open : CardState\.Closed;[\s\S]*SchoolRules\.HoursToChange\(day, hour, out int at\)[\s\S]*facts\.RunLine = SchoolsText\.CardRun\(_state\.Campus\.Owner\);"),
            "campus card: the card's state is the school's own hours and its owner line the school's owner");
        Check(floors.Contains("SchoolRules.Open(SchoolsGame.Day, SchoolsGame.Hour)"), "campus card: the school's door opens by the same rule the card says");
        Check(names.Contains("SpecialFacts.StateKey(Said.State, Said.Hours)") && names.Contains("openStateLabel.SetData(LanguageChangeEventDataHolder.Create(key,") && names.Contains("WorldPanel.Green()") && names.Contains("rivalBusinessOwner")
              && names.Contains("clickToEnterLabel") && names.Contains("entry.Facts != null"),
            "campus card: the game's card gets the state line (green open, red closed), the owner line and the click to enter from the entry's facts");
        // The hall and the junior block are looks with no door and no registration of their own: the game's card is for a building with a door, so they have none.
        Check(!ModSource(@"Schools\SchoolsGame.cs").Contains("CityBuildingController") || ModSource(@"Schools\SchoolsGame.cs").Contains("made.Controller"),
            "campus card: the hall and the junior block carry no controller, so the game shows no card for them");
    }

    // ---------------------------------------------------------------- the court

    private static void CampusCourtChecks()
    {
        var pieces = new List<CampusPiece>();
        var runs = new List<CampusFence>();
        SchoolGrounds.Pieces(pieces, runs);
        SchoolGrounds.Ground(out float x0, out float z0, out _, out _);
        List<CampusPiece> courts = pieces.Where(p => p.Kind == CampusPieceKind.Court).ToList(), hoops = pieces.Where(p => p.Kind == CampusPieceKind.Hoop).ToList();
        Check(courts.Count == 2 && hoops.Count == 2, "campus court: the court is there, two half courts with a hoop each: " + courts.Count + " and " + hoops.Count);
        if (courts.Count != 2 || hoops.Count != 2)
        {
            return;
        }
        // The hoops face each other, each court plane turned the half turn from its hoop and its middle the game's 1.96 m out along the way the hoop faces.
        bool faces = true, centred = true;
        for (int i = 0; i < 2; i++)
        {
            CampusPiece hoop = hoops[i], court = courts[i];
            double a = hoop.Yaw * Math.PI / 180.0;
            float fx = (float)Math.Sin(a), fz = (float)Math.Cos(a);
            float along = (court.X - hoop.X) * fx + (court.Z - hoop.Z) * fz, across = (court.X - hoop.X) * fz - (court.Z - hoop.Z) * fx;
            centred &= Math.Abs(along - SchoolGrounds.HoopToCentre * SchoolGrounds.CourtScale) < 0.01f && Math.Abs(Math.Abs(across) - SchoolGrounds.HoopSide * SchoolGrounds.CourtScale) < 0.01f;
            faces &= Math.Abs(CampusLookRules.Turn(court.Yaw - hoop.Yaw) - 180f) < 0.01f;
        }
        Check(centred && faces, "campus court: each court plane is the game's half court at its hoop (1.96 m out, the half turn from it, as the game places its own)");
        Check(Math.Abs(CampusLookRules.Turn(hoops[0].Yaw - hoops[1].Yaw) - 180f) < 0.01f && hoops[1].Z > hoops[0].Z + 10f && Math.Abs(hoops[0].X - hoops[1].X) < 0.01f,
            "campus court: the two hoops stand at its two ends and face each other, their backboards to the baselines");
        float southEdge = z0 + SchoolGrounds.CourtFrom, northEdge = southEdge + SchoolGrounds.CourtLength;
        Check(hoops[0].Z > southEdge && hoops[0].Z - southEdge < 1.2f && hoops[1].Z < northEdge && northEdge - hoops[1].Z < 1.2f, "campus court: each hoop stands inside its baseline, about a metre from it");
        // Above the lawn: the block ground's grass top is 0.0552 m (the game's mesh, Build 3682), the court's plane and the paths lie higher.
        string game = ModSource(@"Schools\SchoolsGame.cs"), parks = ModSource(@"Uptown\ParksGame.cs");
        Match rise = Regex.Match(parks, @"PathRise = ([0-9.]+)f");
        float surface = rise.Success ? float.Parse(rise.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
        Check(surface >= 0.0552f + 0.02f && Regex.IsMatch(game, @"private const float Surface = ParksGame\.PavingRise;") && Regex.IsMatch(game, @"Name = ""SM_BasketballCourt"", X = -452\.71f, Z = -326\.32f, Y = Surface")
              && game.Contains("UptownLots.GroundY + Surface"), "campus court: the court's plane and the pitch lie " + surface + " m above the ground, over the grass top (0.0552), not at 0.03 under it");
        Check(game.Contains("copy.transform.localScale = copy.transform.localScale * p.StretchX;"), "campus court: the court keeps the game's own scale, stretched evenly (not replaced by a scale of one)");
        // The nets: inside the hoop's own mesh (rim ring 1.30 high, 0.54 out, 0.2 across; mesh z up to 0.765), under the rim.
        Check(SchoolGrounds.NetSides >= 8 && SchoolGrounds.NetTop > 1.2f && SchoolGrounds.NetTop < 1.4f && SchoolGrounds.NetOut + SchoolGrounds.NetTopRadius <= 0.77f && SchoolGrounds.NetOut - SchoolGrounds.NetTopRadius >= 0.3f
              && SchoolGrounds.NetFootRadius < SchoolGrounds.NetTopRadius && SchoolGrounds.NetLength > 0.2f && SchoolGrounds.NetLength < 0.6f && SchoolGrounds.NetTop - SchoolGrounds.NetLength > 0.8f,
            "campus court: a net hangs from each hoop's rim, inside its mesh and narrowing to the foot");
        Check(game.Contains("Material material = CampusLook.NetMaterial;") && game.Contains("if (p.Kind == CampusPieceKind.Hoop && Net(made))") && ModSource(@"Schools\CampusLook.cs").Contains("NetMaterial => Found.TryGetValue(\"fence\""),
            "campus court: each hoop gets its net, drawn with the game's chain-link material");
        // The fence round it: the south baseline and the two long sides whole, the north baseline open in the middle to the plaza.
        float courtX = courts[0].X;
        float half = SchoolGrounds.CourtWidth * 0.5f + 0.2f;
        int round = runs.Count(r => Math.Abs(Math.Abs(r.X0 - courtX) - half) < 0.35f || Math.Abs(Math.Abs(r.X1 - courtX) - half) < 0.35f);
        Check(round == 5, "campus court: five runs of the game's playground fence round the court: " + round);
        float northFence = northEdge + 0.3f;
        int openMetres = 0;
        for (float x = courtX - half + 0.25f; x < courtX + half; x += 0.5f)
        {
            openMetres += Fenced(runs, x, northFence) ? 0 : 1;
        }
        Check(openMetres > 0 && openMetres <= (int)(SchoolGrounds.CourtGap * 2f) + 1 && !Fenced(runs, courtX, northFence) && Fenced(runs, courtX - half, (southEdge + northEdge) * 0.5f) && Fenced(runs, courtX, southEdge - 0.3f),
            "campus court: the court's north baseline is open " + openMetres * 0.5f + " m in the middle, to the plaza; its south baseline and its sides are whole");
        // The strips between the court's fence and the buildings stay wide enough to walk.
        SchoolGrounds.Courtyard(out _, out float courtyardHalf);
        Check(courtyardHalf - half >= 1.4f, "campus court: the strips beside the court's fence are " + (courtyardHalf - half) + " m wide, room to walk");
    }

    // ---------------------------------------------------------------- the layout

    private static void CampusLayoutChecks()
    {
        var pieces = new List<CampusPiece>();
        var runs = new List<CampusFence>();
        var paths = new List<CampusBox>();
        var bodies = new List<CampusBox>();
        SchoolGrounds.Pieces(pieces, runs);
        SchoolGrounds.Paths(paths);
        SchoolGrounds.Buildings(bodies);
        SchoolGrounds.Ground(out float x0, out float z0, out float x1, out float z1);
        Check(paths.Count >= 8 && paths.All(p => p.X - p.HalfX >= x0 && p.X + p.HalfX <= x1 && p.Z - p.HalfZ >= z0 && p.Z + p.HalfZ <= z1), "campus layout: paths are laid, all of them on the campus: " + paths.Count);
        Check(paths.All(p => bodies.All(b => !Overlap(p, b, 0.2f))), "campus layout: no path runs through a building's body");
        CampusBox courtGround = SchoolGrounds.CourtGround();
        Check(paths.All(p => !Overlap(p, courtGround, 0f)), "campus layout: no path runs over the court");
        // Reaches: the rectangles that touch are one path; every route must start and end on one, and the gates, the main door, the court and the field must each be on the right one.
        var routes = new List<SchoolGrounds.CampusRoute>();
        SchoolGrounds.Routes(routes, float.NaN, float.NaN);
        CampusPiece field = pieces.First(p => p.Kind == CampusPieceKind.Pitch);
        CampusBox pitch = new CampusBox(field.X - SchoolGrounds.PitchWidth * 0.5f, field.Z - SchoolGrounds.PitchDepth * 0.5f, field.X + SchoolGrounds.PitchWidth * 0.5f, field.Z + SchoolGrounds.PitchDepth * 0.5f, 0f);
        // What is walked on: the paths, the main school's forecourt slab (the walks and the spine meet on it) and the field's turf (the field's gate and the plaza's path meet on it).
        var ground = new List<CampusBox>(paths) { SchoolGrounds.Forecourt(), pitch };
        int[] group = Components(ground);
        int reached = 0;
        var failed = new List<string>();
        float courtX = pieces.First(p => p.Kind == CampusPieceKind.Court).X;
        float opening = z0 + SchoolGrounds.CourtFrom + SchoolGrounds.CourtLength + 0.3f;
        foreach (SchoolGrounds.CampusRoute r in routes)
        {
            int a = OnPath(ground, r.FromX, r.FromZ, 2.2f), b = OnPath(ground, r.ToX, r.ToZ, 2.2f);
            bool toCourt = r.Name.Contains("court");
            // The court is entered by the opening of its north fence, which a path of the plaza's group must meet; a place on a path must be on the gate's group.
            bool ok = a >= 0 && (toCourt ? OnPathOrNear(ground, group, group[a], courtX, opening, 1.0f) : b >= 0 && group[a] == group[b]);
            reached += ok ? 1 : 0;
            if (!ok)
            {
                failed.Add(r.Name);
            }
        }
        Check(routes.Count == 9 && reached == routes.Count, "campus layout: the paths reach the main door, both yards, the court, the plaza, the hall's and the junior block's ends and the field from the gates: " + reached + " of " + routes.Count
              + (failed.Count > 0 ? " (not: " + string.Join("; ", failed) + ")" : string.Empty));
        // The door itself: a path ends within 0.5 m of the main school's door, on the gate's line.
        SchoolGrounds.MainLot(out float mx, out _, out _, out _, out _, out _);
        float doorZ = SchoolGrounds.DoorOf(z0, float.NaN);
        CampusBox spine = paths.First(p => Math.Abs(p.X - mx) < 0.01f && p.Z - p.HalfZ < z0 + 1f);
        Check(spine.Z - spine.HalfZ <= z0 + 0.5f && spine.Z + spine.HalfZ >= doorZ - 0.5f && spine.Z + spine.HalfZ <= doorZ + 0.5f, "campus layout: the main gate's path runs from the gate to the main school's door");
        foreach (float door in new[] { z0 + SchoolGrounds.MainInset - SchoolGrounds.DoorBay, z0 + 4.5f, z0 + 6f })
        {
            var again = new List<CampusBox>();
            SchoolGrounds.Paths(again, door);
            CampusBox s2 = again.First(p => Math.Abs(p.X - mx) < 0.01f && p.Z - p.HalfZ < z0 + 1f);
            Check(Math.Abs(s2.Z + s2.HalfZ - (door - 0.2f)) < 0.01f || door - 0.2f < 1.2f, "campus layout: the spine follows the door wherever the copy engine puts it (" + (door - z0) + " m)");
        }
        // The gates are the ways in and out: nothing stands in a gate's way.
        float mid = (x0 + x1) * 0.5f, sideGate = z0 + SchoolGrounds.SideGateV;
        var passages = new List<CampusBox>
        {
            new CampusBox(mid - 3f, z0, mid + 3f, z0 + 3f, 0f),
            new CampusBox(x1 - 4f, sideGate - 1.8f, x1, sideGate + 1.8f, 0f),
            new CampusBox(mid - 2.5f, z1 - 4f, mid + 2.5f, z1, 0f)
        };
        var dressing = pieces.Where(p => p.Kind != CampusPieceKind.Fence && p.Kind != CampusPieceKind.Post && p.Kind != CampusPieceKind.Pitch && p.Kind != CampusPieceKind.Court).ToList();
        Check(dressing.All(p => passages.All(g => !Inside(p.X, p.Z, g, 0.5f))), "campus layout: nothing stands in the main gate's, the side gate's or the field's gate's way");
        // Clear of the fence, the buildings, the court and the paths.
        Check(dressing.All(p => p.X >= x0 + 0.9f && p.X <= x1 - 0.9f && p.Z >= z0 + 0.9f && p.Z <= z1 - 0.9f), "campus layout: the dressing keeps 0.9 m from the fence line");
        Check(dressing.All(p => bodies.All(b => !Inside(p.X, p.Z, b, 0.3f))), "campus layout: nothing stands in a building's body");
        Check(dressing.Where(p => p.Kind != CampusPieceKind.Hoop).All(p => !Inside(p.X, p.Z, courtGround, 0f)), "campus layout: nothing but the hoops stands on the court");
        CampusBox plaza = paths.First(p => Math.Abs(p.Z - (z0 + 60.8f)) < 0.5f && p.HalfX > 6f);
        Check(dressing.Where(p => p.Kind != CampusPieceKind.Hoop).All(p => paths.Where(r => r.X != plaza.X || r.Z != plaza.Z).All(r => !Inside(p.X, p.Z, r, 0.1f))),
            "campus layout: no tree, bench, table or bin stands on a path, but in the plaza's paved square");
        // The playing field keeps its margin from the fence on every side, and clear of the green's furniture.
        float fenceW = x0 + SchoolGrounds.FenceIn, fenceE = x1 - SchoolGrounds.FenceIn, fenceN = z1 - SchoolGrounds.FenceIn;
        float margin = Math.Min(Math.Min(pitch.X - pitch.HalfX - fenceW, fenceE - (pitch.X + pitch.HalfX)), fenceN - (pitch.Z + pitch.HalfZ));
        Check(margin >= SchoolGrounds.FieldMargin, "campus layout: the playing field keeps " + margin.ToString("F1") + " m from the fence on its north, east and west (at least " + SchoolGrounds.FieldMargin + ")");
        Check(Math.Abs(SchoolGrounds.PitchWidth - SchoolGrounds.PitchBaseWidth * SchoolGrounds.PitchScale) < 0.01f && Math.Abs(field.StretchX - field.StretchZ) < 0.001f, "campus layout: the pitch is stretched evenly, so its lines keep their shape");
        Check(dressing.All(p => !Inside(p.X, p.Z, pitch, 0.3f)), "campus layout: no piece of the dressing stands on the field");
        // What the dressing is, from the game's own pieces, by kind.
        Check(dressing.Count(p => p.Kind == CampusPieceKind.Oak || p.Kind == CampusPieceKind.Birch || p.Kind == CampusPieceKind.Beech || p.Kind == CampusPieceKind.Maple) >= 20, "campus layout: trees, twenty and more, of four kinds");
        Check(dressing.Count(p => p.Kind == CampusPieceKind.Bench) >= 10 && dressing.Count(p => p.Kind == CampusPieceKind.Table) >= 5 && dressing.Count(p => p.Kind == CampusPieceKind.Bin) >= 8
              && dressing.Count(p => p.Kind == CampusPieceKind.Climb) >= 2,
            "campus layout: benches, table groups, bins and climbing frames: " + SchoolGrounds.Words(pieces));
        // The planters: four on the main school's forecourt 1.2 m in front of its front wall at 13.5 and 17.5 m either side of its middle, two by the junior block's door.
        var bushes = dressing.Where(p => p.Kind == CampusPieceKind.Bush).ToList();
        float wall = SchoolGrounds.DoorOf(z0, float.NaN) + SchoolGrounds.DoorBay;
        Check(bushes.Count == 6 && new[] { -17.5f, -13.5f, 13.5f, 17.5f }.All(d => bushes.Any(b => Math.Abs(b.X - (mid + d)) < 0.01f && Math.Abs(wall - b.Z - 1.2f) < 0.01f)),
            "campus layout: four planters on the main school's forecourt, one of the game's bushes each, 1.2 m in front of its front wall");
        LookModel junior = CampusLookRules.Models[2];
        float jx = x1 - CampusLookRules.WallFromEdge(junior) + 1.0f, jz = z0 + 34f + 2f + 15f;
        Check(bushes.Count(b => Math.Abs(b.X - jx) < 0.01f && (Math.Abs(b.Z - (jz - 5.5f)) < 0.01f || Math.Abs(b.Z - (jz + 5.5f)) < 0.01f)) == 2, "campus layout: two planters 1.0 m in front of the junior block's door, 5.5 m either side");
        // Deterministic: the same on every call.
        var again2 = new List<CampusPiece>();
        var runs2 = new List<CampusFence>();
        SchoolGrounds.Pieces(again2, runs2);
        Check(again2.Count == pieces.Count && again2.Zip(pieces, (a, b) => a.Kind == b.Kind && a.X == b.X && a.Z == b.Z && a.Yaw == b.Yaw).All(x => x), "campus layout: the same pieces in the same places every time");
        Check(pieces.Count < 480 && pieces.Count(p => p.Kind != CampusPieceKind.Fence && p.Kind != CampusPieceKind.Post) < 120, "campus layout: the dressing is a hundred pieces or so, laid once behind the loading screen");
    }

    private static bool Inside(float x, float z, CampusBox b, float margin) => Math.Abs(x - b.X) <= b.HalfX + margin && Math.Abs(z - b.Z) <= b.HalfZ + margin;

    private static bool Overlap(CampusBox a, CampusBox b, float margin) => Math.Abs(a.X - b.X) < a.HalfX + b.HalfX + margin && Math.Abs(a.Z - b.Z) < a.HalfZ + b.HalfZ + margin;

    // The paths that touch or overlap are one path: a group number for each rectangle.
    private static int[] Components(List<CampusBox> rects)
    {
        int[] g = Enumerable.Range(0, rects.Count).ToArray();
        int Find(int i) => g[i] == i ? i : g[i] = Find(g[i]);
        for (int i = 0; i < rects.Count; i++)
        {
            for (int j = i + 1; j < rects.Count; j++)
            {
                if (Overlap(rects[i], rects[j], 0.01f))
                {
                    g[Find(i)] = Find(j);
                }
            }
        }
        for (int i = 0; i < rects.Count; i++)
        {
            g[i] = Find(i);
        }
        return g;
    }

    // The index of a path a spot is on or within a margin of, nearest first; -1 for none.
    private static int OnPath(List<CampusBox> rects, float x, float z, float margin)
    {
        int best = -1;
        float far = float.MaxValue;
        for (int i = 0; i < rects.Count; i++)
        {
            float dx = Math.Max(0f, Math.Abs(x - rects[i].X) - rects[i].HalfX), dz = Math.Max(0f, Math.Abs(z - rects[i].Z) - rects[i].HalfZ);
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d <= margin && d < far)
            {
                far = d;
                best = i;
            }
        }
        return best;
    }

    // True when some path of the group is within the reach of the spot (a place on the open ground, the field or the court, that a path leads to).
    private static bool OnPathOrNear(List<CampusBox> rects, int[] group, int want, float x, float z, float reach)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            float dx = Math.Max(0f, Math.Abs(x - rects[i].X) - rects[i].HalfX), dz = Math.Max(0f, Math.Abs(z - rects[i].Z) - rects[i].HalfZ);
            if (group[i] == want && Math.Sqrt(dx * dx + dz * dz) <= reach)
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- the people

    private static void CampusPeopleChecks()
    {
        const int year = 60;
        // The school day: nobody at night or at the weekend, the run, the break, lunch and the clubs by the clock.
        Check(SchoolRules.CrowdMoments(48, 12 * 60, year) == CrowdWhen.None && SchoolRules.CrowdMoments(49, 8 * 60, year) == CrowdWhen.None && SchoolRules.CrowdMoments(47, 3 * 60, year) == CrowdWhen.None
              && SchoolRules.CrowdMoments(47, 22 * 60, year) == CrowdWhen.None, "campus people: quiet at night and at the weekend");
        Check((SchoolRules.CrowdMoments(47, 8 * 60, year) & CrowdWhen.Run) != 0 && (SchoolRules.CrowdMoments(47, 15 * 60, year) & CrowdWhen.Run) != 0 && (SchoolRules.CrowdMoments(47, 11 * 60, year) & CrowdWhen.Run) == 0,
            "campus people: the school run's hours bring the families and pupils to the gates and the door");
        Check(SchoolRules.CrowdMoments(47, 10 * 60 + 40, year) == CrowdWhen.Break && SchoolRules.CrowdMoments(47, 13 * 60, year) == CrowdWhen.Lunch && (SchoolRules.CrowdMoments(47, 16 * 60, year) & CrowdWhen.After) != 0
              && SchoolRules.CrowdMoments(47, 11 * 60, year) == CrowdWhen.None && SchoolRules.CrowdMoments(47, 18 * 60, year) == CrowdWhen.None, "campus people: a break, a lunch hour and the afternoon's clubs, and nobody between");
        // Open day is the term's third day.
        int openDay = Enumerable.Range(1, 120).First(d => SchoolRules.OpenDay(d, year) && SchoolRules.SchoolDay(d));
        Check((SchoolRules.CrowdMoments(openDay, 11 * 60, year) & CrowdWhen.OpenDay) != 0, "campus people: an open day brings people to the gates, the door and the forecourt in its hours");
        var squares = new List<CampusCrowd>();
        SchoolGrounds.Crowds(squares);
        SchoolGrounds.Pieces(new List<CampusPiece>(), new List<CampusFence>());
        var bodies = new List<CampusBox>();
        SchoolGrounds.Buildings(bodies);
        Check(squares.Count == SchoolGrounds.CrowdCount && squares.All(s => s.When != CrowdWhen.None), "campus people: nine squares, each with its moments");
        foreach (CrowdWhen moment in new[] { CrowdWhen.Run, CrowdWhen.Break, CrowdWhen.Lunch, CrowdWhen.After, CrowdWhen.OpenDay })
        {
            Check(squares.Any(s => (s.When & moment) != 0 && s.Inside), "campus people: at " + moment + " some square on the campus's own ground has people");
        }
        Check(squares.Where(s => s.Inside).All(s => bodies.All(b => !Inside(s.X, s.Z, b, 0f))), "campus people: no square's middle is inside a building");
        var plan = new List<CampusPiece>();
        SchoolGrounds.Pieces(plan, new List<CampusFence>());
        CampusCrowd court = squares.First(s => s.Name == "the court");
        CampusBox courtGround = SchoolGrounds.CourtGround();
        Check(Inside(court.X, court.Z, courtGround, 0f) && (court.When & CrowdWhen.After) != 0 && (court.When & CrowdWhen.Lunch) != 0, "campus people: some play on the court, at lunch and after classes");
        CampusCrowd field = squares.First(s => s.Name == "the field");
        Check((field.When & CrowdWhen.After) != 0 && (field.When & CrowdWhen.Run) == 0, "campus people: a few on the field after classes");
        Check(squares.Count(s => s.Name.Contains("yard") || s.Name == "the green" || s.Name == "the plaza") == 4 && squares.Where(s => s.Name.Contains("yard") || s.Name == "the green").All(s => (s.When & CrowdWhen.Lunch) != 0),
            "campus people: groups in the plaza, on the green and in the yards at break and lunch, by the benches and tables");
        // The game has no child models: every square is the game's own pedestrians through the zones the families at the gates already use.
        string game = ModSource(@"Schools\SchoolsGame.cs"), campus = ModSource(@"Schools\SchoolsDirector.Campus.cs");
        Check(game.Contains("PeopleGame.Zone(") && !game.Contains("new CityPerson") && campus.Contains("SchoolRules.CrowdMoments(SchoolsGame.Day, SchoolsGame.Minute, SchoolsGame.Year)") && campus.Contains("Near(CrowdNear) ? want : CrowdWhen.None")
              && campus.Contains("if (_crowded && want == _crowdMoments)"),
            "campus people: the game's own pedestrians in zones by the school's clock, only while the player is near, a few comparisons a second when nothing changes");
        // The routes walked at a load: one line.
        Check(campus.Contains("Schools check (campus reach)") && campus.Contains("SchoolsGame.Reaches(from, to, SchoolsGame.NpcAgent") && campus.Contains("SchoolsGame.Reaches(from, to, SchoolsGame.PlayerAgent"),
            "campus people: a load asks the walking meshes whether people and the player can walk every route, and says so in one line");
    }

    // ---------------------------------------------------------------- the sources and the texts

    private static void CampusWiringChecks()
    {
        string game = ModSource(@"Schools\SchoolsGame.cs"), look = ModSource(@"Schools\CampusLookRules.cs"), pieces = ModSource(@"Uptown\GamePieces.cs"), grounds = ModSource(@"Schools\SchoolGrounds.Dressing.cs");
        foreach (string piece in new[] { "Tree Broadleaf L1", "Tree Birch", "Tree Broadleaf M1", "Tree Maple", "Broadleaf Bush L1", "SM_Trash Can", "Prefab_TableGroup", "Playground_Climbing", "Playground_Fence_Modular" })
        {
            Check(game.Contains("Name = \"" + piece + "\""), "campus dressing: the game's '" + piece + "' is a template found by the shared finder");
        }
        Check(game.Contains("ParksGame.PavingTemplate((int)CampusPieceKind.Pavement)") && game.Contains("ParksGame.PavingFrom(Found[t])") && game.Contains("ParksGame.PavingMesh(Rects,"), "campus dressing: the paths are the game's own pavement material, the parks' way");
        Check(pieces.Contains("StandAligned(") && game.Contains("GamePieces.StandAligned(template, parent"), "campus dressing: a table group keeps its own turn, the campus's yaw put on it");
        Check(game.Contains("Obstacle(parent, new Vector3(p.X, UptownLots.GroundY, p.Z), p.Yaw, new Vector3(across, high, deep));"), "campus dressing: the walking meshes are cut round each piece (walkability)");
        Check(!Regex.IsMatch(grounds, @"using System\.Linq|\bRandom\b|\bTime\.|UnityEngine|GetHashCode"), "campus dressing: the layout is game-free and leaves nothing to chance");
        Check(CampusLookRules.GameNames("glass")[0] == "M_Glass" && CampusLookRules.GameNames("glass").Contains("M_GlassBlueHue") && look.Contains("opaque, metallic, grey-blue"),
            "campus look: the windows are the game's M_Glass (opaque, metallic, grey-blue, smoothness 0.87: it reflects the street and reads dark), the bright teal M_GlassBlueHue only a fallback");
        Check(ModSource(@"Schools\CampusLook.cs").Contains("said.Append(k > 0 ? \", \" : string.Empty).Append(kind).Append(pick != null ? \" '\" + pick.name + \"'\" : \" not found\");"),
            "campus look: the glass material's name is logged once a load with the others");
        // Texts: the kind and the owner lines in en, pt and zh-cn; What's new in all nine, this fix once each.
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("campus dressing texts", words, codes))
        {
            return;
        }
        const string cardType = "reallife_schools_card_type", runPlayer = "reallife_schools_card_run_player", runCouncil = "reallife_schools_card_run_council", runTrust = "reallife_schools_card_run_trust";
        foreach (string key in new[] { cardType, runPlayer, runCouncil, runTrust })
        {
            for (int l = 0; l < 3; l++)
            {
                Check(words[l].TryGetValue(key, out string value) && value.Length > 0 && !value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013), "campus dressing texts: " + key + " is in " + codes[l] + ".json, in the style");
            }
        }
        Check(words[0][cardType] == "School" && words[0][runCouncil] == "Run by the council" && words[0][runPlayer] == "Run by you" && words[0][runTrust] == "Run by {who}",
            "campus dressing texts: the card reads School, and Run by the council, you or a trust");
        var lines = new Dictionary<string, string>
        {
            { "en", "Fixed: Birchwood School's building card" }, { "pt", "Corrigido: o cartão do edifício da Birchwood School" }, { "zh-cn", "已修复：伯奇伍德学校的建筑卡片" },
            { "de", "Behoben: Die Gebäudekarte der Birchwood School" }, { "fr", "Corrigé : la fiche du bâtiment de Birchwood School" }, { "es", "Corregido: la ficha del edificio de Birchwood School" },
            { "ru", "Исправлено: карточка здания школы Birchwood" }, { "nl", "Opgelost: de gebouwkaart van Birchwood School" }, { "tr", "Düzeltildi: Birchwood School'un bina kartı" }
        };
        for (int l = 0; l < codes.Length; l++)
        {
            Check(lines.TryGetValue(codes[l], out string wanted) && WhatsNewHas(words[l], wanted), "campus dressing texts: What's new has the card's fix in " + codes[l]);
        }
    }
}
