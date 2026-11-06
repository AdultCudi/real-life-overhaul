using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Where Uptown's buildings stand (the owner, 24 Oct 2026: "They are literally in the side walk. Too front."), on the real
// plan of its 422 lots and the game's own designs as Build 3682's scenes have them (Census/design-fronts-3682.json: each
// design the copy engine can pick, its box, its door and every drawn or solid part reaching outside the box, read from the
// game's level files by the kit's filllab/facts/design_fronts.py). The copy engine's own rules measure them
// (NewBuildingsRules.Begin, Part, SquareFacing), pick them (Pick, by the lots' structures as LifeDirector declares them) and
// stand them (Place). Checked: every lot's front line is the back of its street's pavement, the first block's included;
// nothing that stands (a solid part, or a drawn one reaching below head height) of any building stands on the pavement or
// outside its lot, whichever way its street and its door face; its door is where the pedestrians reach it; a part of
// another place under a design keeps it from being copied; a narrow design no longer leaves a wide lot half empty; and the
// box alone, as the engine measured before, is shown to put fronts on the pavement, so the check fails on the old placement.
internal static partial class Program
{
    private const string DesignFronts = "design-fronts-3682.json";

    // A part of a design reaching outside its box: round its root, its lowest and highest points above its door.
    private sealed class FrontPart
    {
        public bool Solid;
        public float Bottom, Top, MinX, MaxX, MinZ, MaxZ;
    }

    // One design of the fixture: as the engine measures it now, as it measured it with its box alone, and its raw measures.
    private sealed class FrontDesign
    {
        public DesignEntry Now, Was;
        public float BoxMinX, BoxMaxX, BoxMinZ, BoxMaxZ, RawFacing;
        public readonly List<FrontPart> Parts = new List<FrontPart>();
    }

    // What a design stood on a lot does there: m past the lot's front (the pavement) of what stands and of what hangs above
    // head height, m outside its sides, m its door stands behind the front, and m its furthest stray stands from the lot.
    private struct Stood
    {
        public float Front, Overhang, Side, DoorBack, Stray;
    }

    private static void UptownPlaceChecks()
    {
        List<FrontDesign> fixture = UptownPlaceFixture();
        Check(fixture != null && fixture.Count > 500, "uptown place: the game's designs as Build 3682's scenes have them are read (" + (fixture != null ? fixture.Count : 0) + " from " + DesignFronts + ")");
        if (fixture == null)
        {
            return;
        }
        UptownPlaceRules();
        UptownPlaceCatalogue(fixture);
        UptownPlaceLots(fixture);
        UptownPlaceSources();
    }

    private static List<FrontDesign> UptownPlaceFixture()
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "..", "..", "..", "RealLifeOverhaul.Build", "Census", DesignFronts));
        if (!File.Exists(path))
        {
            return null;
        }
        var list = new List<FrontDesign>(600);
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            foreach (JsonElement row in doc.RootElement.GetProperty("designs").EnumerateArray())
            {
                string key = row[0].GetString();
                int bar = key.LastIndexOf('|');
                JsonElement box = row[3], door = row[4];
                var f = new FrontDesign
                {
                    BoxMinX = box[0].GetSingle(), BoxMaxX = box[1].GetSingle(), BoxMinZ = box[2].GetSingle(), BoxMaxZ = box[3].GetSingle(), RawFacing = door[3].GetSingle()
                };
                foreach (JsonElement p in row[5].EnumerateArray())
                {
                    f.Parts.Add(new FrontPart
                    {
                        Solid = p[0].GetInt32() == 1, Bottom = p[1].GetSingle(), Top = p[2].GetSingle(), MinX = p[3].GetSingle(), MaxX = p[4].GetSingle(),
                        MinZ = p[5].GetSingle(), MaxZ = p[6].GetSingle()
                    });
                }
                DesignEntry Entry() => new DesignEntry
                {
                    Name = key.Substring(0, bar), Size = key.Substring(bar + 1), Type = row[1].GetString(), Version = row[2].GetInt32(), Uses = 1,
                    DoorX = door[0].GetSingle(), DoorZ = door[1].GetSingle(), DoorY = door[2].GetSingle()
                };
                // Now: the engine's own measure, as CopyGame.Describe makes it in the game (heights round the root there).
                f.Now = Entry();
                NewBuildingsRules.Begin(f.Now, f.BoxMinX, f.BoxMaxX, f.BoxMinZ, f.BoxMaxZ, Math.Max(1f, box[4].GetSingle()));
                foreach (FrontPart p in f.Parts)
                {
                    NewBuildingsRules.Part(f.Now, p.Solid, p.MinX, p.MaxX, p.MinZ, p.MaxZ, p.Bottom + f.Now.DoorY, p.Top + f.Now.DoorY);
                }
                f.Now.DoorFacing = NewBuildingsRules.SquareFacing(f.RawFacing, f.Now.DoorX, f.Now.DoorZ, f.BoxMinX, f.BoxMaxX, f.BoxMinZ, f.BoxMaxZ);
                // Was: the box alone and the door's own facing, as the engine measured before 24 Oct 2026.
                f.Was = Entry();
                f.Was.MinX = f.Was.BoxMinX = f.BoxMinX;
                f.Was.MaxX = f.Was.BoxMaxX = f.BoxMaxX;
                f.Was.MinZ = f.Was.BoxMinZ = f.BoxMinZ;
                f.Was.MaxZ = f.Was.BoxMaxZ = f.BoxMaxZ;
                f.Was.Height = Math.Max(1f, box[4].GetSingle());
                f.Was.DoorFacing = f.RawFacing;
                list.Add(f);
            }
        }
        list.Sort((a, b) => NewBuildingsRules.CompareDesigns(a.Now, b.Now));
        GateFixture(list);   // the game's garage doors and car parks on the designs that have them (Census/design-gates-3682.json)
        return list;
    }

    // ---------------------------------------------------------------- the rules, on made-up designs

    private static void UptownPlaceRules()
    {
        // A box 10 across and 20 deep round its root, its door the middle of its front (z 10), 0.3 m up.
        DesignEntry d = new DesignEntry { DoorX = 0f, DoorZ = 10f, DoorY = 0.3f };
        NewBuildingsRules.Begin(d, -5f, 5f, -10f, 10f, 12f);
        NewBuildingsRules.Part(d, false, -2f, 2f, 9f, 11.5f, 0.3f, 1.2f);     // steps before the door, on the ground
        NewBuildingsRules.Part(d, false, -1f, 1f, 9f, 12.4f, 4.0f, 5.0f);     // a sign over the pavement, above head height
        NewBuildingsRules.Part(d, true, -6f, 5f, -10f, 10f, 0f, 3f);          // a solid wall a metre past its side
        NewBuildingsRules.Part(d, false, 0f, 1f, 0f, 1f, 0f, 14f);            // a chimney inside the box, above it
        Check(Near(d.MaxZ, 11.5f, 0.001f) && Near(d.MinX, -6f, 0.001f) && Near(d.MaxX, 5f, 0.001f) && Near(d.MinZ, -10f, 0.001f) && Near(d.Height, 14f, 0.001f) && !d.Strays
              && Near(d.BoxMaxZ, 10f, 0.001f),
            "uptown place: a design's footprint is its box and every solid part and drawn part reaching below head height round it, its height its highest part; a sign above head height takes no ground");
        NewBuildingsRules.Part(d, true, -1f, 1f, 30f, 31f, 0f, 3f);           // a collider 20 m before the box: of another place
        Check(d.Strays && Near(d.MaxZ, 11.5f, 0.001f), "uptown place: a part more than " + NewBuildingsRules.StrayMost + " m outside the box marks the design never copied and takes no ground");
        DesignEntry ok = new DesignEntry { DoorX = 0f, DoorZ = 10f, DoorY = 0.3f };
        NewBuildingsRules.Begin(ok, -5f, 5f, -10f, 10f, 12f);
        NewBuildingsRules.Part(ok, true, -1f, 1f, 10f, 10f + NewBuildingsRules.StrayMost, 0f, 3f);
        Check(!ok.Strays && Near(ok.MaxZ, 10f + NewBuildingsRules.StrayMost, 0.001f), "uptown place: a part just within the stray limit is the building's and takes its ground");

        Check(NewBuildingsRules.SquareFacing(0f, 0f, 10f, -5f, 5f, -10f, 10f) == 0f && NewBuildingsRules.SquareFacing(358f, 0f, 10f, -5f, 5f, -10f, 10f) == 0f
              && NewBuildingsRules.SquareFacing(92f, 5f, 0f, -5f, 5f, -10f, 10f) == 90f && NewBuildingsRules.SquareFacing(-90f, -5f, 0f, -5f, 5f, -10f, 10f) == 270f
              && NewBuildingsRules.SquareFacing(30f, 0f, 10f, -5f, 5f, -10f, 10f) == 0f && NewBuildingsRules.SquareFacing(60f, 5f, 0f, -5f, 5f, -10f, 10f) == 90f,
            "uptown place: a door's facing is squared to its root's nearest right angle");
        Check(NewBuildingsRules.SquareFacing(45f, 4.2f, 10f, -5f, 5f, -10f, 10f) == 0f && NewBuildingsRules.SquareFacing(45f, 5f, 8f, -5f, 5f, -10f, 10f) == 90f
              && NewBuildingsRules.SquareFacing(135f, 5f, -9f, -5f, 5f, -10f, 10f) == 90f && NewBuildingsRules.SquareFacing(225f, -4f, -10f, -5f, 5f, -10f, 10f) == 180f,
            "uptown place: a door across a corner (45 degrees) faces the way of the box face nearer it");

        var front = new DesignEntry { MinX = -5f, MaxX = 5f, MinZ = -10f, MaxZ = 10f, DoorX = 0f, DoorZ = 10.3f, DoorFacing = 0f };
        var back = new DesignEntry { MinX = -5f, MaxX = 5f, MinZ = -10f, MaxZ = 10f, DoorX = 0f, DoorZ = 7.9f, DoorFacing = 0f };
        var deep = new DesignEntry { MinX = -5f, MaxX = 5f, MinZ = -10f, MaxZ = 10f, DoorX = 0f, DoorZ = 7.9f, DoorFacing = 0f, Batched = false, Strays = true };
        Check(Near(NewBuildingsRules.DoorBehind(front), -0.3f, 0.001f) && Near(NewBuildingsRules.DoorBehind(back), 2.1f, 0.001f) && NewBuildingsRules.Copyable(front)
              && !NewBuildingsRules.Copyable(back) && !NewBuildingsRules.Copyable(deep) && !NewBuildingsRules.Copyable(new DesignEntry { MinX = -1f, MaxX = 1f, MinZ = -1f, MaxZ = 1f, DoorZ = 1f, Batched = true })
              && !NewBuildingsRules.Copyable(null),
            "uptown place: a design is copied only when its door stands within " + NewBuildingsRules.DoorBackMost + " m of its front, nothing of another place is under it and it is in no static batch");
        // The game sends a pedestrian to a building's door: 0.5 m out of it, a point of their walking mesh within 2 m
        // (Streets.Pedestrians.PedestrianBuildingPositionProvider.TryGetSampleTarget); theirs ends at the lot's front, kept
        // 0.3 m in by their radius. A door at the limit is reached.
        const float Out = 0.5f, Within = 2f, Radius = 0.3f;
        Check(NewBuildingsRules.DoorBackMost - Out + Radius <= Within, "uptown place: a door " + NewBuildingsRules.DoorBackMost + " m behind its front is within the game's 2 m of the pedestrians' ground");

        // A wide lot is given its wider designs: of three that fit 23 m, the 7 m one (under 70% of the widest) is never picked.
        var pick = new List<DesignEntry> { Wide("SM_A", 7f), Wide("SM_B", 20f), Wide("SM_C", 15f) };
        bool narrowNever = true;
        var seen = new HashSet<string>();
        for (int i = 0; i < 60; i++)
        {
            int at = NewBuildingsRules.Pick(pick, "ba:buildingsize_c", "ba:buildingtype_office", null, 23f, 32f, "lot_" + i);
            narrowNever &= at >= 0 && pick[at].Name != "SM_A";
            seen.Add(at >= 0 ? pick[at].Name : "none");
        }
        Check(narrowNever && seen.Contains("SM_B") && seen.Contains("SM_C"), "uptown place: a lot is given the designs at least " + NewBuildingsRules.WidthShare + " as wide as the widest that fits, by its id (" + string.Join(", ", seen) + ")");
        Check(NewBuildingsRules.Pick(pick, "ba:buildingsize_c", "ba:buildingtype_office", null, 10f, 32f, "lot_1") == 0, "uptown place: where only a narrow design fits, it is the one given");
    }

    private static DesignEntry Wide(string name, float width)
    {
        return new DesignEntry { Name = name, Size = "ba:buildingsize_c", Type = "ba:buildingtype_office", Version = 1, MinX = -width * 0.5f, MaxX = width * 0.5f, MinZ = -20f, MaxZ = 0f, DoorX = 0f, DoorZ = 0f };
    }

    // ---------------------------------------------------------------- the game's designs

    private static void UptownPlaceCatalogue(List<FrontDesign> fixture)
    {
        int grown = 0, strays = 0, squared = 0, backs = 0, copyable = 0, square = 0;
        bool mb06 = false, mh02 = false;
        string strayNames = string.Empty;
        foreach (FrontDesign f in fixture)
        {
            DesignEntry d = f.Now;
            grown += d.MaxX - d.MinX > f.BoxMaxX - f.BoxMinX + 0.05f || d.MaxZ - d.MinZ > f.BoxMaxZ - f.BoxMinZ + 0.05f ? 1 : 0;
            strays += d.Strays ? 1 : 0;
            mb06 |= d.Strays && d.Name == "SM_MB Building 06";
            mh02 |= d.Strays && d.Name == "SM_MurrayHill Section H_Building 02";
            strayNames += d.Strays && strayNames.Length < 120 ? (strayNames.Length > 0 ? ", " : string.Empty) + d.Name : string.Empty;
            squared += Math.Abs(NewBuildingsRules.Turn(d.DoorFacing - f.RawFacing + 180f) - 180f) > 1f ? 1 : 0;
            backs += !d.Strays && NewBuildingsRules.DoorBehind(d) > NewBuildingsRules.DoorBackMost ? 1 : 0;
            copyable += NewBuildingsRules.Copyable(d) ? 1 : 0;
            square += Near(d.DoorFacing % 90f, 0f, 0.001f) ? 1 : 0;
        }
        Check(grown > 150 && mb06 && mh02,
            "uptown place: of " + fixture.Count + " designs, " + grown + " take ground outside their box (steps, porches, bays, scaffolding); " + strays + " carry a part of another place and are never copied ("
            + strayNames + ", among them SM_MB Building 06 and SM_MurrayHill Section H_Building 02, whose colliders stand some 220 m off)");
        Check(square == fixture.Count && squared >= 20, "uptown place: every design's door faces a right angle of its root, " + squared + " of them squared from across a corner");
        Check(backs > 20 && copyable > 400, "uptown place: " + backs + " designs whose door stands more than " + NewBuildingsRules.DoorBackMost + " m behind their front (a yard, a recess) are never picked; "
              + copyable + " can be");

        // Every design that can be copied, on a lot of each class facing each way: its footprint on the lot's front line and
        // inside its lot, its door within reach, nothing that stands before the front.
        float worstFront = 0f, worstSide = 0f, worstDoor = float.MinValue, worstHang = 0f;
        int stood = 0;
        string where = null;
        foreach (FrontDesign f in fixture)
        {
            if (!NewBuildingsRules.Copyable(f.Now))
            {
                continue;
            }
            foreach (int kind in new[] { 0, 1, 2, 3 })
            {
                LayoutRules.Footprint((LotClass)kind, out int width, out int depth);
                if (!NewBuildingsRules.Fits(f.Now, width, depth))
                {
                    continue;
                }
                foreach (float facing in new[] { 0f, 90f, 180f, 270f })
                {
                    Stood s = StandOn(f, f.Now, new MadeLot(100f, 200f, facing, width, depth));
                    stood++;
                    if (s.Front > worstFront || s.Side > worstSide)
                    {
                        where = f.Now.Name + " facing " + facing;
                    }
                    worstFront = Math.Max(worstFront, s.Front);
                    worstSide = Math.Max(worstSide, s.Side);
                    worstDoor = Math.Max(worstDoor, s.DoorBack);
                    worstHang = Math.Max(worstHang, s.Overhang);
                }
            }
        }
        Check(stood > 2000 && worstFront <= 0.01f && worstSide <= 0.01f && worstDoor <= NewBuildingsRules.DoorBackMost + 0.01f,
            "uptown place: every design that can be copied, on every lot class it fits, facing every way (" + stood + "), stands nothing before its lot's front (worst "
            + worstFront.ToString("F2") + " m) or past its sides (" + worstSide.ToString("F2") + " m), its door at most " + worstDoor.ToString("F2") + " m behind the front"
            + (where != null ? " (" + where + ")" : string.Empty));
        Check(worstHang <= 2f, "uptown place: what hangs above head height over the pavement (a sign, a flag) reaches at most " + worstHang.ToString("F2") + " m out, as the game's own do (up to about 2 m in Manhattan)");
    }

    // A design stood on a lot by the engine's placement (NewBuildingsRules.Place), its box and parts taken to the world.
    private static Stood StandOn(FrontDesign f, DesignEntry as_, MadeLot lot)
    {
        NewBuildingsRules.Place(as_, lot, out float x, out float z, out float yaw);
        double a = yaw * Math.PI / 180.0, l = lot.Facing * Math.PI / 180.0;
        float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
        float fx = (float)Math.Sin(l), fz = (float)Math.Cos(l), rx = fz, rz = -fx;
        var stood = new Stood { Front = float.MinValue, Overhang = float.MinValue, Side = float.MinValue };
        void Corners(float x0, float x1, float z0, float z1, ref float front, ref float side)
        {
            for (int i = 0; i < 4; i++)
            {
                float px = (i & 1) == 0 ? x0 : x1, pz = (i & 2) == 0 ? z0 : z1;
                float wx = x + px * c + pz * s - lot.X, wz = z - px * s + pz * c - lot.Z;
                front = Math.Max(front, wx * fx + wz * fz - lot.Depth * 0.5f);
                side = Math.Max(side, Math.Abs(wx * rx + wz * rz) - lot.Width * 0.5f);
            }
        }
        float none = float.MinValue;
        Corners(f.BoxMinX, f.BoxMaxX, f.BoxMinZ, f.BoxMaxZ, ref stood.Front, ref stood.Side);
        foreach (FrontPart p in f.Parts)
        {
            float outside = Math.Max(Math.Max(f.BoxMinX - p.MinX, p.MaxX - f.BoxMaxX), Math.Max(f.BoxMinZ - p.MinZ, p.MaxZ - f.BoxMaxZ));
            if (outside > NewBuildingsRules.StrayMost)
            {
                stood.Stray = Math.Max(stood.Stray, outside);
                continue;
            }
            if (p.Solid || p.Bottom < NewBuildingsRules.HeadClear)
            {
                Corners(p.MinX, p.MaxX, p.MinZ, p.MaxZ, ref stood.Front, ref stood.Side);
            }
            else
            {
                Corners(p.MinX, p.MaxX, p.MinZ, p.MaxZ, ref stood.Overhang, ref none);
            }
        }
        NewBuildingsRules.DoorAt(as_, x, z, yaw, out float doorX, out float doorZ);
        stood.DoorBack = lot.Depth * 0.5f - ((doorX - lot.X) * fx + (doorZ - lot.Z) * fz);
        stood.Overhang = Math.Max(0f, stood.Overhang);
        return stood;
    }

    // ---------------------------------------------------------------- the real plan

    private static void UptownPlaceLots(List<FrontDesign> fixture)
    {
        List<BuildingsLot> plan = UptownRealPlan();
        // Every lot's front line is the back of its street's pavement (the game's 8.65 m from a street's middle, 13.65 m from
        // an avenue's), the first block's included, so a footprint on it stands on no pavement.
        int onEdge = 0;
        string off = null;
        foreach (BuildingsLot lot in plan)
        {
            float edge = PavementEdge(lot);
            onEdge += Math.Abs(edge) <= 0.02f ? 1 : 0;
            off = Math.Abs(edge) <= 0.02f ? off : off ?? lot.Id + " (" + edge.ToString("F2") + " m)";
        }
        Check(onEdge == plan.Count, "uptown place: all " + plan.Count + " lots' fronts stand on the back of their street's pavement" + (off != null ? "; not " + off : string.Empty));

        var now = new List<DesignEntry>(fixture.Count);
        var was = new List<DesignEntry>(fixture.Count);
        var byEntry = new Dictionary<DesignEntry, FrontDesign>();
        foreach (FrontDesign f in fixture)
        {
            now.Add(f.Now);
            was.Add(f.Was);
            byEntry[f.Now] = f;
            byEntry[f.Was] = f;
        }
        List<int> built = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        float nowFront = 0f, wasFront = 0f, nowDoor = float.MinValue, wasStray = 0f, nowFill = 0f, wasFill = 0f;
        int nowMade = 0, wasMade = 0, nowOnPavement = 0, wasOnPavement = 0, nowGaps = 0, wasGaps = 0, avenues = 0, streets = 0, first = 0;
        string wasWorst = null, nowWorst = null, wasStrayAt = null;
        var into = new List<BuildingsStructure>();
        var fronts = new Dictionary<int, List<float[]>>();   // each block's buildings' footprints, along the world's axes
        foreach (int i in built)
        {
            BuildingsLot lot = plan[i];
            List<BuildingsStructure> options = UptownOptions(lot);
            bool lease = leased.Contains(lot.Id);
            int structure = lease ? BuildingsRules.PickOffice(lot.Id, options) : BuildingsRules.PickStructure(lot.Id, options);
            if (structure < 0)
            {
                continue;
            }
            BuildingsRules.Others(options, structure, lease, into);
            var made = new MadeLot(lot.X, lot.Z, lot.Facing, lot.Width, lot.Depth);
            int n = Choose(now, options[structure], into, made, lot.Id, false, BuildingsRules.LookOf(lot.Class));
            int w = Choose(was, options[structure], into, made, lot.Id, true, BuildingsRules.LookOf(lot.Class));
            if (n >= 0)
            {
                Stood s = StandOn(byEntry[now[n]], now[n], made);
                nowMade++;
                nowOnPavement += s.Front > 0.01f ? 1 : 0;
                if (s.Front > nowFront)
                {
                    nowFront = s.Front;
                    nowWorst = lot.Id + " (" + now[n].Name + ")";
                }
                nowDoor = Math.Max(nowDoor, s.DoorBack);
                float across = Across(now[n]);
                nowFill += across / lot.Width;
                nowGaps += lot.Width - across > 6f ? 1 : 0;
                NewBuildingsRules.Footprint(now[n], made, out float fcx, out float fcz, out float fhw, out float fhd);
                bool alongX = lot.Facing == 0f || lot.Facing == 180f;
                if (!fronts.TryGetValue(lot.Block, out List<float[]> boxes))
                {
                    boxes = new List<float[]>();
                    fronts.Add(lot.Block, boxes);
                }
                boxes.Add(alongX ? new[] { fcx - fhw, fcz - fhd, fcx + fhw, fcz + fhd } : new[] { fcx - fhd, fcz - fhw, fcx + fhd, fcz + fhw });
                avenues += lot.Avenue ? 1 : 0;
                streets += lot.Avenue ? 0 : 1;
                first += lot.First ? 1 : 0;
            }
            if (w >= 0)
            {
                Stood s = StandOn(byEntry[was[w]], was[w], made);
                wasMade++;
                wasOnPavement += s.Front > 0.3f ? 1 : 0;
                if (s.Front > wasFront)
                {
                    wasFront = s.Front;
                    wasWorst = lot.Id + " (" + was[w].Name + ")";
                }
                if (s.Stray > wasStray)
                {
                    wasStray = s.Stray;
                    wasStrayAt = lot.Id + " (" + was[w].Name + ")";
                }
                float across = Across(was[w]);
                wasFill += across / lot.Width;
                wasGaps += lot.Width - across > 6f ? 1 : 0;
            }
        }
        // The engine as it measured before: the box alone puts fronts on the pavement, and a collider of another place in the band.
        Check(wasMade > 300 && wasFront > 2f && wasOnPavement > 50 && wasStray > 100f,
            "uptown place: with the box alone, as before, " + wasOnPavement + " of " + wasMade + " buildings stood more than 0.3 m on the pavement, the worst " + wasFront.ToString("F2") + " m at "
            + wasWorst + ", and " + wasStrayAt + " carried a collider " + wasStray.ToString("F0") + " m from its building");
        Check(nowMade == built.Count && nowOnPavement == 0 && nowFront <= 0.01f && avenues > 100 && streets > 150 && first >= 30,
            "uptown place: on the real plan all " + nowMade + " of " + built.Count + " buildings (" + avenues + " on avenues, " + streets + " on streets, " + first
            + " in the first block) stand nothing on the pavement (the most forward " + Math.Max(0f, nowFront).ToString("F2") + " m, at " + nowWorst + ")");
        Check(nowDoor <= NewBuildingsRules.DoorBackMost + 0.01f, "uptown place: every door stands at most " + nowDoor.ToString("F2") + " m behind its building's front, where the pedestrians reach it");
        Check(nowMade > 0 && wasMade > 0 && nowFill / nowMade >= 0.8f && nowFill / nowMade > wasFill / wasMade + 0.05f && nowGaps < wasGaps,
            "uptown place: a building takes " + (100f * nowFill / Math.Max(1, nowMade)).ToString("F0") + "% of its lot's frontage on average (before " + (100f * wasFill / Math.Max(1, wasMade)).ToString("F0")
            + "%), " + nowGaps + " lots more than 6 m short across (before " + wasGaps + ")");

        // Built up as Manhattan is (the owner, 24 Oct 2026: "Like Manhattan"): the share of each block's edge with a building's
        // face within 3 m of it, measured as Manhattan's own were (median 71% over 16 blocks south of 1st Avenue, the game's
        // buildings' boxes, Build 3682); the first plan's band, as the owner's load of 24 Oct made it, was at 34%.
        var shares = new List<float>();
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int column = 1; column <= LayoutRules.Columns; column++)
            {
                if (LayoutRules.IsSquare(row, column) || LayoutRules.IsFirst(row, column))
                {
                    continue;
                }
                LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                List<float[]> b = fronts.TryGetValue(row * 100 + column, out List<float[]> got) ? got : new List<float[]>();
                float covered = Covered(b, x0, x1, f => f[3] > z1 - 3f, 0, 2) + Covered(b, x0, x1, f => f[1] < z0 + 3f, 0, 2)
                                + Covered(b, z0, z1, f => f[0] < x0 + 3f, 1, 3) + Covered(b, z0, z1, f => f[2] > x1 - 3f, 1, 3);
                shares.Add(covered / (2f * (x1 - x0) + 2f * (z1 - z0)));
            }
        }
        shares.Sort();
        float median = shares.Count > 0 ? shares[shares.Count / 2] : 0f;
        Check(shares.Count >= 30 && median >= 0.6f, "uptown place: the band's blocks are built along " + (100f * median).ToString("F0") + "% of their edges (the median of " + shares.Count
              + " blocks beyond the first, the open lots among them), Manhattan's own 71%, the first plan's 34%");
    }

    // m of the run from a to b under the boxes that pass the test, each taken from its axis's two ends (0 and 2 for x, 1 and 3 for z).
    private static float Covered(List<float[]> boxes, float a, float b, Func<float[], bool> near, int lo, int hi)
    {
        var runs = new List<(float, float)>();
        foreach (float[] f in boxes)
        {
            if (near(f) && f[hi] > a && f[lo] < b)
            {
                runs.Add((Math.Max(a, f[lo]), Math.Min(b, f[hi])));
            }
        }
        runs.Sort((p, q) => p.Item1.CompareTo(q.Item1));
        float total = 0f, at = a;
        foreach ((float s, float e) in runs)
        {
            if (e > at)
            {
                total += e - Math.Max(s, at);
                at = e;
            }
        }
        return total;
    }

    // m from a lot's front line to the back of its street's pavement (0 when on it; above 0 when the front is on the pavement).
    private static float PavementEdge(BuildingsLot lot)
    {
        float front;
        if (lot.Facing == 0f || lot.Facing == 180f)
        {
            front = lot.Z + (lot.Facing == 0f ? 1f : -1f) * lot.Depth * 0.5f;
            int row = (int)Math.Round((front - LayoutRules.FirstAvenueZ) / LayoutRules.AvenuePitch);
            return LayoutRules.AvenueHalf - Math.Abs(front - LayoutRules.AvenueZ(row));
        }
        front = lot.X + (lot.Facing == 90f ? 1f : -1f) * lot.Depth * 0.5f;
        int k = (int)Math.Round((front - LayoutRules.StreetFirstX) / LayoutRules.StreetPitch);
        return LayoutRules.StreetHalf - Math.Abs(front - LayoutRules.StreetX(k));
    }

    // The director's choice for a lot (NewBuildingsDirector.Make): its own structure's design, else the first of its others
    // with one. The old way picked among every design of the size that fits, by Batched alone.
    private static int Choose(List<DesignEntry> designs, BuildingsStructure own, List<BuildingsStructure> others, MadeLot lot, string id, bool old, string named)
    {
        for (int s = -1; s < others.Count; s++)
        {
            BuildingsStructure st = s < 0 ? own : others[s];
            int pick = old && named == null ? OldPick(designs, st.Size, st.Type, lot.Width, lot.Depth, id) : NewBuildingsRules.Pick(designs, st.Size, st.Type, named, lot.Width, lot.Depth, id);
            if (pick >= 0)
            {
                return pick;
            }
        }
        return -1;
    }

    // NewBuildingsRules.Pick as it was before 24 Oct 2026: every design of the size that fits, of its type if any, by the id.
    private static int OldPick(List<DesignEntry> designs, string size, string type, float width, float depth, string id)
    {
        int sized = 0, typed = 0;
        foreach (DesignEntry d in designs)
        {
            if (d.Size == size && NewBuildingsRules.Fits(d, width, depth))
            {
                sized++;
                typed += d.Type == type ? 1 : 0;
            }
        }
        if (sized == 0)
        {
            return -1;
        }
        bool byType = typed > 0;
        int wanted = (int)(NewBuildingsRules.Hash(id) % (uint)(byType ? typed : sized));
        for (int i = 0; i < designs.Count; i++)
        {
            if (designs[i].Size == size && NewBuildingsRules.Fits(designs[i], width, depth) && (!byType || designs[i].Type == type) && wanted-- == 0)
            {
                return i;
            }
        }
        return -1;
    }

    private static float Across(DesignEntry d)
    {
        NewBuildingsRules.DoorFrame(d, out float uMin, out float uMax, out _, out _);
        return uMax - uMin;
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownPlaceSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string copy = Read("NewBuildings/CopyGame.cs"), copies = Read("NewBuildings/BuildingCopies.cs"), handles = Read("NewBuildings/NewBuildingsDirector.Copies.cs");
        string tests = Read("NewBuildings/NewBuildingsDirector.Tests.cs"), art = Read("Uptown/GroundGame.Art.cs"), paving = Read("Uptown/GroundGame.Paving.cs");
        string describe = MemberText(copy, "public static bool Describe(CityBuildingController c, DesignEntry into)");
        string parts = MemberText(copy, "private static void MeasureParts(CityBuildingController c, MeshRenderer box, Quaternion unturn, Vector3 origin, DesignEntry into)");
        Check(describe.Contains("NewBuildingsRules.Begin(") && describe.Contains("MeasureParts(c, box, unturn, origin, into)") && describe.Contains("NewBuildingsRules.SquareFacing(")
              && describe.IndexOf("into.DoorY = at.y", StringComparison.Ordinal) < describe.IndexOf("NewBuildingsRules.Begin(", StringComparison.Ordinal)
              && parts.Contains("ShadowCastingMode.ShadowsOnly") && parts.Contains("isTrigger") && parts.Contains("r == box") && copy.Contains("NewBuildingsRules.Part(into, solid,"),
            "uptown place: the game's side measures every drawn and solid part of a building round its box, by the rules checked here, its door first");
        Check(copy.Contains("if (NewBuildingsRules.Copyable(entry))") && copies.Contains("NewBuildingsRules.Copyable(d)") && handles.Contains("!NewBuildingsRules.Copyable(designs[design])")
              && tests.Contains("if (!NewBuildingsRules.Copyable(d))") && MemberText(copy, "private static void Finish()").Contains("NewBuildingsRules.Copyable(d)"),
            "uptown place: every copy, look and test takes only designs that can be copied (no part of another place, the door within reach), and the catalogue's line counts them");
        Check(paving.Contains("private const string PavementMaterial = \"M_RoadSideCurb\";") && MemberText(art, "public static int CopyArt(RoadBuild build)").Contains("Unpave(copy.GetComponent<Renderer>(), false)")
              && MemberText(art, "public static int CopyArt(RoadBuild build)").Contains("UnpaveKept();") && MemberText(art, "private static void DropArt()").Contains("Repaved();")
              && art.Contains("KeptPlanes.Add(planes[i]);") && MemberText(paving, "private static void UnpaveKept()").Contains("LayoutRules.OnGround(middle.x, middle.z)"),
            "uptown place: the block grounds' ring of pavement slabs is laid as their middle on the band (the copies and the backdrop's own over it), and the backdrop's get theirs back when the art goes");
    }
}
