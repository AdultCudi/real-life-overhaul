using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// The looks of Uptown's buildings (the owner, 27 Oct 2026, after a player walked the new district: "A lot of buildings have
// missing textures, there are gaps between buildings and buildings do not face the road properly"; "A district is a lot of work.
// Still needs to be properly fixed."), on the real plan and the game's own designs as Build 3682's scenes have them
// (Census/design-fronts-3682.json, Census/design-looks-3682.json): the census of every building and filler (LookCensus), the ends of
// each that nothing stands against and whether the design's side there has windows, can be given them or is a wall nothing can be
// done with, the gaps, the yards, the lots left without a building, whether every door faces its lot's street, the window
// overlays' geometry (SideRules), the row of the game's buildings along 1st Avenue's south pavement (SouthRules) against the
// backdrop's boxes the player saw there, and the sources of the game's side. The pick and the layout of 0.58.0 are held against the
// new ones: the same rules fail them.
internal static partial class Program
{
    private const string DesignLooks = "design-looks-3682.json";

    // One world of the plan: its designs, the lots built from the first day with their declarations, and the pieces standing.
    private sealed class LookWorld
    {
        public List<BuildingsLot> Plan;
        public List<DesignEntry> Designs;
        public readonly Dictionary<string, MadeSpec> Specs = new Dictionary<string, MadeSpec>(StringComparer.Ordinal);
        public readonly List<FrontFill> Fills = new List<FrontFill>();
        public readonly List<LookPiece> Pieces = new List<LookPiece>();
        public readonly HashSet<string> Built = new HashSet<string>(StringComparer.Ordinal);
        public int South;   // fillers of the avenue's south row among the Fills
    }

    // The plan laid out as a load lays it (FrontageRules.Plan on the real designs, the south row after it), the pieces made from the
    // declarations and the fillers; with <fixedUp> off, the pick and the layout of before 27 Oct 2026.
    private static LookWorld UptownLookWorld(bool fixedUp)
    {
        List<FrontDesign> fixture = UptownPlaceFixture();
        if (fixture == null)
        {
            return null;
        }
        var w = new LookWorld { Plan = UptownRealPlan(), Designs = new List<DesignEntry>(fixture.Count) };
        foreach (FrontDesign f in fixture)
        {
            w.Designs.Add(f.Now);
        }
        List<int> built = BuildingsRules.Built(w.Plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> leased = UptownIds(w.Plan, BuildingsRules.Leased(w.Plan, new List<int>()));
        var into = new List<BuildingsStructure>();
        foreach (int i in built)
        {
            BuildingsLot lot = w.Plan[i];
            List<BuildingsStructure> options = UptownOptions(lot);
            bool lease = leased.Contains(lot.Id);
            int pick = lease ? BuildingsRules.PickOffice(lot.Id, options) : BuildingsRules.PickStructure(lot.Id, options);
            if (pick < 0)
            {
                continue;
            }
            BuildingsRules.Others(options, pick, lease, into);
            var others = new MadeStructure[into.Count];
            for (int k = 0; k < into.Count; k++)
            {
                others[k] = new MadeStructure { Size = into[k].Size, Version = into[k].Version, Type = into[k].Type };
            }
            w.Specs[lot.Id] = new MadeSpec
            {
                Id = lot.Id, Street = lot.Street, Number = lot.Number, Size = options[pick].Size, Version = options[pick].Version, Type = options[pick].Type, Life = true,
                Lots = new[] { new MadeLot(lot.X, lot.Z, lot.Facing, lot.Width, lot.Depth) }, Others = others, Design = BuildingsRules.LookOf(lot.Class)
            };
        }
        FrontageRules.Plan(w.Plan, id => w.Specs.TryGetValue(id, out MadeSpec s) ? s : null, w.Designs, w.Fills, out _, fixedUp);
        if (fixedUp)
        {
            w.South = SouthRules.Plan(w.Designs, w.Fills);
        }
        foreach (KeyValuePair<string, MadeSpec> s in w.Specs)
        {
            MadeSpec spec = s.Value;
            int d = NewBuildingsRules.Pick(w.Designs, spec.Size, spec.Type, spec.Design, spec.Lots[0].Width, spec.Lots[0].Depth, spec.Id);
            if (d >= 0)
            {
                w.Pieces.Add(new LookPiece { Id = spec.Id, Design = w.Designs[d], Ground = spec.Lots[0] });
                w.Built.Add(spec.Id);
            }
        }
        for (int i = 0; i < w.Fills.Count; i++)
        {
            FrontFill f = w.Fills[i];
            w.Pieces.Add(new LookPiece { Id = "filler " + i, Design = w.Designs[f.Design], Ground = f.Lot, Filler = true, Back = f.Back });
        }
        return w;
    }

    private static void UptownLookChecks()
    {
        LookWorld was = UptownLookWorld(false), now = UptownLookWorld(true);
        if (was == null || now == null)
        {
            Check(false, "uptown look: the game's designs (" + DesignFronts + ") could not be read");
            return;
        }
        UptownLookTable(now.Designs);
        UptownLookCensus(was, now);
        UptownLookSpots(was, now);
        UptownLookSides(now);
        UptownLookSouth(now);
        UptownLookPick(now);
        UptownLookSources();
        UptownLookTexts();
    }

    // ---------------------------------------------------------------- the table of the designs' sides

    // The table in the mod is Build 3682's census made small: every design of the census has its row, each side as the census says it.
    private static void UptownLookTable(List<DesignEntry> designs)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "..", "..", "..", "RealLifeOverhaul.Build", "Census", DesignLooks));
        Check(File.Exists(path), "uptown look: the game's designs' sides as Build 3682's scenes have them are read (" + DesignLooks + ")");
        if (!File.Exists(path))
        {
            return;
        }
        var keys = new HashSet<string>(LookRules.Keys, StringComparer.Ordinal);
        int rows = 0, wrong = 0, missing = 0;
        string first = null;
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            foreach (JsonElement row in doc.RootElement.GetProperty("designs").EnumerateArray())
            {
                rows++;
                string key = row[0].GetString();
                int bar = key.LastIndexOf('|');
                var d = new DesignEntry { Name = key.Substring(0, bar), Size = key.Substring(bar + 1) };
                missing += keys.Contains(d.Name + "|" + d.Size.Substring(d.Size.LastIndexOf('_') + 1)) ? 0 : 1;
                bool same = SideIs(d, false, row[2].GetString()) && SideIs(d, true, row[3].GetString());
                wrong += same ? 0 : 1;
                first = first ?? (same ? null : key);
            }
        }
        Check(rows >= 500 && missing == 0 && wrong == 0 && LookRules.Known == rows,
            "uptown look: the mod's table of the designs' sides (LookSides) is the census's: " + rows + " designs, " + missing + " missing, " + wrong + " differing" + (first != null ? " (" + first + ")" : string.Empty));
        int windowed = 0, dressable = 0, bare = 0, flatColour = 0;
        foreach (DesignEntry d in designs)
        {
            foreach (bool right in new[] { false, true })
            {
                SideKind k = LookRules.Kind(d, right);
                windowed += k == SideKind.Windowed ? 1 : 0;
                dressable += k == SideKind.Dressable ? 1 : 0;
                bare += k == SideKind.Bare ? 1 : 0;
                flatColour += LookRules.Flat(d, right) ? 1 : 0;
            }
        }
        Check(dressable > 2 * windowed && dressable > 5 * bare && flatColour > 50 && flatColour < dressable / 3 && bare > 0
              && LookRules.Kind(new DesignEntry { Name = "A design a later build adds", Size = "ba:buildingsize_a" }, true) == SideKind.Bare,
            "uptown look: of the " + designs.Count + " designs' " + 2 * designs.Count + " sides " + dressable + " are plain walls that can be given windows (" + flatColour + " of them of flat colour), "
            + windowed + " have windows, " + bare + " cannot be dressed; a design the table does not know is one that cannot");
    }

    private static bool SideIs(DesignEntry d, bool right, string state)
    {
        // a plain wall of flat colour is a number and an f: dressable like the rest, and marked
        bool flat = state.EndsWith("f", StringComparison.Ordinal);
        string core = flat ? state.Substring(0, state.Length - 1) : state;
        SideKind kind = LookRules.Kind(d, right);
        if (LookRules.Flat(d, right) != flat)
        {
            return false;
        }
        if (core == "w")
        {
            return kind == SideKind.Windowed;
        }
        if (core == "-")
        {
            return kind == SideKind.Bare;
        }
        return kind == SideKind.Dressable && Math.Abs(LookRules.Top(d, right) - float.Parse(core, CultureInfo.InvariantCulture)) < 0.01f;
    }

    // ---------------------------------------------------------------- the census, before and after

    // The verdict the game's test gives (LifeDirector.LookWhy), on the census alone: a blank wall to a road, a door off its street, a gap a space wide.
    private static string LookVerdict(LookReport r, bool windowsLaid)
    {
        int blankRoad = windowsLaid ? r.LeftBlankRoad : r.BlankRoadEnds;
        return blankRoad > 0 ? blankRoad + " open ends on a road still show a wall with no window" : windowsLaid && r.FlatRoadEnds > 0 ? r.FlatRoadEnds + " open ends on a road show a wall of flat colour"
            : r.Facing > 0 ? r.Facing + " doors face off their street" : r.WidestGap >= SpacesRules.SpaceWidth ? "a gap a whole space wide" : null;
    }

    private static void UptownLookCensus(LookWorld was, LookWorld now)
    {
        LookReport before = LookCensus.Count(was.Pieces, was.Plan, id => was.Built.Contains(id));
        LookReport after = LookCensus.Count(now.Pieces, now.Plan, id => now.Built.Contains(id));
        Console.WriteLine("Uptown look census, as 0.58.0 stood (no windows laid): " + before.Describe());
        Console.WriteLine("Uptown look census, now: " + after.Describe());
        Check(before.BlankEnds >= 300 && before.BlankRoadEnds >= 100 && before.Buildings == after.Buildings,
            "uptown look: as 0.58.0 stood, " + before.BlankEnds + " of its " + before.OpenEnds + " open ends showed a wall with no window of its own (" + before.BlankRoadEnds + " on a road) and nothing dressed them");
        Check(after.LeftBlankRoad == 0 && after.LeftBlank <= 110 && after.BareRoadEnds == 0 && after.Dressed >= 400,
            "uptown look: now " + after.Dressed + " of those ends are given windows (" + after.DressedRoad + " on a road), " + after.LeftBlank + " are left blank (" + after.LeftBlankRoad + " on a road), none a wall nothing can be done with on a road");
        Check(before.FlatRoadEnds >= 15 && after.FlatRoadEnds == 0 && after.FlatEnds * 3 <= before.FlatEnds,
            "uptown look: walls of flat colour (a grey or plain wall with no texture of its own, windows or not) shown to a street: " + before.FlatRoadEnds + " as 0.58.0 stood, " + after.FlatRoadEnds
            + " now; " + before.FlatEnds + " flat walls dressed with windows then, " + after.FlatEnds + " now (the pick takes a textured wall toward a street where any design will do)");
        Check(before.Facing == 0 && after.Facing == 0 && LotsOnRoads(now.Plan),
            "uptown look: every door faces the street its lot is on, as the plan and the engine place it (" + before.Facing + " before, " + after.Facing + " now), and every lot's front line is on the pavement of the road it faces");
        Check(after.WidestGap < SpacesRules.SpaceWidth && after.Gaps <= before.Gaps && after.Yards < before.Yards && after.YardsShown < before.YardsShown,
            "uptown look: gaps inside built lots " + before.Gaps + " before, " + after.Gaps + " now, none as wide as a space (the widest " + after.WidestGap.ToString("0.0", CultureInfo.InvariantCulture)
            + " m); yards behind buildings " + before.Yards + " before (" + before.YardsShown + " in view of a street or an open lot), " + after.Yards + " now (" + after.YardsShown + "): a building turned to face a yard's open end where no second row fits");
        Check(after.Land == before.Land && after.Lots == before.Lots && after.Land <= 92,
            "uptown look: the lots with no building are the land the plan leaves open for the construction game, the landmarks' blocks and the lot something of the game's stands in: " + after.Land + " of " + after.Lots + ", none lost to a fix");
        // The pick and the layout of 0.58.0 made to fail: the verdict of the game's test, on what stood then.
        string wasWhy = LookVerdict(before, false), nowWhy = LookVerdict(after, true);
        Check(wasWhy != null && nowWhy == null, "uptown look: the census fails 0.58.0 (" + wasWhy + ") and passes now (" + (nowWhy ?? "no fault") + ")");
    }

    // Every lot's front line stands on the pavement of the road it faces: a step out of the front is on a road.
    private static bool LotsOnRoads(List<BuildingsLot> plan)
    {
        foreach (BuildingsLot l in plan)
        {
            double f = l.Facing * Math.PI / 180.0;
            float fx = l.X + (float)Math.Sin(f) * (l.Depth * 0.5f + 0.2f), fz = l.Z + (float)Math.Cos(f) * (l.Depth * 0.5f + 0.2f);
            if (!LookRules.OnRoad(fx, fz))
            {
                return false;
            }
        }
        return true;
    }


    // ---------------------------------------------------------------- the spots a player's screenshots show

    // The lots beside the places a player (#general and #bug-reports, 27 Oct 2026) stood at when a wall, a lawn or a box looked wrong: a screenshot's
    // panel gives the street, x and z; each is found in the plan, and what shows there now is told: every end of the building and of its second
    // row with windows, and no lawn left behind.
    private static readonly string[][] Spots =
    {
        new[] { "uptown_2_18_0001", "5 Summit Avenue, a dark glass building on a lawn (park 12, x 756.5 z 728.5)", "second row" },
        new[] { "uptown_3_18_0005", "6 Summit Avenue, a blank brick wall along the pavement, 'wrong facing' (park 5, x 753.0 z 803.0)", "second row" },
        new[] { "uptown_3_16_0004", "20 Summit Avenue, a blank dark red wall by Ridge Street (ridge 13, x 572.9 z 798.4)", null },
        new[] { "uptown_3_16_0001", "21 Crest Avenue, a corner with a blank brick side (crest avenue 21, x 574.6 z 872.3)", "second row" },
        new[] { "uptown_2_12_0001", "41 Summit Avenue, a grey wall with no window (summit avenue 41, x 223.6 z 729.4)", null },
        new[] { "uptown_1_15_0012", "1 Ridge Street, its end on 1st Avenue's continuation, grey boxes beyond (ridge 1, x 563.4 z 457.6)", null }
    };

    private static void UptownLookSpots(LookWorld was, LookWorld now)
    {
        var ends = new List<LookEnd>();
        LookCensus.OpenEnds(now.Pieces, ends);
        var oldEnds = new List<LookEnd>();
        LookCensus.OpenEnds(was.Pieces, oldEnds);
        int fixedSpots = 0;
        foreach (string[] spot in Spots)
        {
            BuildingsLot lot = now.Plan.Find(l => l.Id == spot[0]);
            if (lot.Id == null || now.Pieces.FindIndex(p => p.Id == spot[0] && !p.Filler) < 0 || was.Pieces.FindIndex(p => p.Id == spot[0] && !p.Filler) < 0)
            {
                Check(false, "uptown look spots: " + spot[1] + ": its lot has no building in the plan");
                continue;
            }
            bool before = true, after = true;
            string oldTold = SpotTold(was, oldEnds, lot, false, out _, ref before), told = SpotTold(now, ends, lot, true, out int rowEnds, ref after);
            after &= spot[2] == null || rowEnds > 0;   // a second row that shows an end to a road is counted: the census does not skip the second rows
            fixedSpots += after ? 1 : 0;
            Console.WriteLine("  spot " + spot[0] + " (" + spot[1] + "):" + Environment.NewLine + "      0.58.0: " + oldTold + Environment.NewLine + "      now:    " + told);
        }
        Check(fixedSpots == Spots.Length, "uptown look spots: at each of the " + Spots.Length + " spots of the screenshots every open end of the building and of its second row shows windows, and no lawn is left behind"
                                          + " (" + fixedSpots + " of " + Spots.Length + "; the lines above)");
    }

    // What stands on a lot, the building and the second rows' buildings whose middle is on its ground, and how each open end of each shows; <ok> is
    // cleared by an end left with a bare wall or by a yard of BackLeast or more left with no second row.
    private static string SpotTold(LookWorld w, List<LookEnd> ends, BuildingsLot lot, bool windowsLaid, out int rowEnds, ref bool ok)
    {
        rowEnds = 0;
        var told = new StringBuilder();
        bool alongX = Math.Abs(Math.Cos(lot.Facing * Math.PI / 180.0)) > 0.5;
        float halfX = (alongX ? lot.Width : lot.Depth) * 0.5f, halfZ = (alongX ? lot.Depth : lot.Width) * 0.5f;
        float deep = 0f;
        bool back = false;
        for (int i = 0; i < w.Pieces.Count; i++)
        {
            LookPiece p = w.Pieces[i];
            bool own = p.Id == lot.Id && !p.Filler;
            if (!own && !(p.Filler && p.Back && Math.Abs(p.Ground.X - lot.X) <= halfX && Math.Abs(p.Ground.Z - lot.Z) <= halfZ))
            {
                continue;
            }
            deep = own ? FrontageRules.Deep(p.Design) : deep;
            back |= !own;
            told.Append(told.Length > 0 ? "; " : string.Empty).Append(own ? "building " : "second row ").Append(p.Design.Name).Append(" (")
                .Append(p.Design.Size.Substring(p.Design.Size.LastIndexOf('_') + 1).ToUpperInvariant()).Append(own ? string.Empty : p.Ground.Facing != lot.Facing ? ", turned to face the end" : string.Empty).Append("):");
            bool any = false;
            foreach (LookEnd e in ends)
            {
                if (e.Piece != i)
                {
                    continue;
                }
                any = true;
                rowEnds += !own && e.Road ? 1 : 0;
                bool panel = e.Kind == SideKind.Dressable && SideRules.Panel(p.Design, p.Ground, e.High, p.Id, out _);
                told.Append(' ').Append(e.Right ? "right" : "left").Append(" end ").Append(e.Road ? "on a road, " : string.Empty)
                    .Append(e.Kind == SideKind.Windowed ? "has windows" : windowsLaid && panel ? "given windows" : "a blank wall").Append(',');
                ok &= !windowsLaid || e.Kind == SideKind.Windowed || panel;
            }
            told.Append(any ? string.Empty : " no open end");
        }
        float behind = lot.Depth - deep;
        bool yard = behind >= FrontageRules.BackLeast && !back;
        ok &= !windowsLaid || !yard;
        told.Append("; ").Append(behind.ToString("0.0", CultureInfo.InvariantCulture)).Append(" m behind the building ").Append(behind < FrontageRules.BackLeast ? "(no yard)" : back ? "(a second row)" : "(a bare yard)");
        return told.ToString();
    }

    // ---------------------------------------------------------------- the windows' geometry

    private static void UptownLookSides(LookWorld now)
    {
        var d = new DesignEntry { Name = "Test", Size = "ba:buildingsize_a", DoorX = 0f, DoorZ = 5f, DoorY = 0.1f, DoorFacing = 0f };
        NewBuildingsRules.Begin(d, -10f, 10f, -5f, 5f, 31f);
        Check(LookRules.Kind(d, false) == SideKind.Bare, "uptown look: a design the table does not know has sides nothing is done with");
        DesignEntry plain = null;
        foreach (DesignEntry e in now.Designs)
        {
            if (plain == null && LookRules.Kind(e, false) == SideKind.Dressable && LookRules.Kind(e, true) == SideKind.Dressable && LookRules.Top(e, false) >= 14f && LookRules.Top(e, true) >= 14f && FrontageRules.Across(e) > 10f)
            {
                plain = e;
            }
        }
        Check(plain != null, "uptown look: the catalogue has a design with two plain sides that can be dressed");
        if (plain == null)
        {
            return;
        }
        // Each of the four ways a lot faces: its lower end is the strip's west or south, its higher the east or north; a panel stands on the wall that end shows.
        foreach (float facing in new[] { 0f, 90f, 180f, 270f })
        {
            var ground = new MadeLot(100f, 200f, facing, FrontageRules.Across(plain) + 0.5f, FrontageRules.Deep(plain) + 5f);
            NewBuildingsRules.Place(plain, ground, out float rootX, out float rootZ, out float yaw);
            bool both = SideRules.Panel(plain, ground, false, "a", out SidePanel low) & SideRules.Panel(plain, ground, true, "a", out SidePanel high);
            if (!both)
            {
                Check(false, "uptown look: a panel stands on each end of a lot facing " + facing);
                continue;
            }
            WorldPanel(low, rootX, rootZ, yaw, out float lowNx, out float lowNz, out float lowX, out float lowZ);
            WorldPanel(high, rootX, rootZ, yaw, out float highNx, out float highNz, out float highX, out float highZ);
            bool alongX = facing == 0f || facing == 180f;
            float lowWay = alongX ? -lowNx : -lowNz, highWay = alongX ? highNx : highNz;
            var positions = new float[12];
            var uvs = new float[8];
            var triangles = new int[6];
            SideRules.Quad(high, positions, uvs, triangles);
            bool faces = SideRules.Faces(positions, triangles, 0, high.NX, high.NZ) && SideRules.Faces(positions, triangles, 3, high.NX, high.NZ);
            bool ends = alongX ? lowX < 100f && highX > 100f : lowZ < 200f && highZ > 200f;
            Check(lowWay > 0.99f && highWay > 0.99f && faces && ends && low.Columns >= 1 && low.Rows >= 1 && uvs[2] == high.Columns && uvs[5] == high.Rows && high.Y1 > high.Y0 + 3f,
                "uptown look: a lot facing " + facing + ": its lower end's wall faces " + (alongX ? "west" : "south") + ", its higher " + (alongX ? "east" : "north") + ", the quad faces outward, "
                + high.Columns + " window columns by " + high.Rows + " floors, from " + high.Y0.ToString("0.0", CultureInfo.InvariantCulture) + " m to " + high.Y1.ToString("0.0", CultureInfo.InvariantCulture) + " m");
        }
        Check(!SideRules.Panel(null, default, true, "a", out _) && !SideRules.Panel(d, new MadeLot(0f, 0f, 0f, 20f, 15f), true, "a", out _),
            "uptown look: no panel for no design, nor on a design the table does not know");
        // A quad wound the other way is a back face: the check of the winding fails it.
        var p = new float[12];
        var uv = new float[8];
        var tri = new int[6];
        SideRules.Panel(plain, new MadeLot(100f, 200f, 0f, FrontageRules.Across(plain) + 0.5f, FrontageRules.Deep(plain) + 5f), true, "a", out SidePanel any);
        SideRules.Quad(any, p, uv, tri);
        int swap = tri[1];
        tri[1] = tri[2];
        tri[2] = swap;
        Check(!SideRules.Faces(p, tri, 0, any.NX, any.NZ), "uptown look: the check of a quad's winding fails a quad wound the other way");
        Check(SideRules.Styles.Length == 4 && SideRules.Styles[0].StartsWith("M_BuildBack_V", StringComparison.Ordinal) && SideRules.Lift < 0.1f && SideRules.FromGround > 2.5f,
            "uptown look: the windows are the game's own backside materials (" + string.Join(", ", SideRules.Styles) + "), a few centimetres off the wall, from a floor above the street");
    }

    // A panel in the world: its wall's outward way and its middle, from the root's place and turn.
    private static void WorldPanel(SidePanel p, float rootX, float rootZ, float yaw, out float nx, out float nz, out float x, out float z)
    {
        double a = yaw * Math.PI / 180.0;
        float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
        nx = p.NX * c + p.NZ * s;
        nz = -p.NX * s + p.NZ * c;
        float mx = (p.AX + p.BX) * 0.5f, mz = (p.AZ + p.BZ) * 0.5f;
        x = rootX + mx * c + mz * s;
        z = rootZ - mx * s + mz * c;
    }

    // ---------------------------------------------------------------- the avenue's south row

    // The backdrop's boxes that stood against 1st Avenue's continuations' south pavement, 26 triangles each in a gradient colour (Build 3682's
    // MapBorder/Buildings Blockouts, read from level6: each piece's middle), and pieces of it behind them and beside Manhattan, which stay.
    private static readonly float[][] BackdropFront =
    {
        new[] { 464.5f, 440.0f }, new[] { 494.0f, 435.3f }, new[] { 510.4f, 435.3f }, new[] { 549.7f, 437.4f }, new[] { 535.8f, 439.9f }, new[] { 667.0f, 437.7f }, new[] { 683.4f, 436.1f },
        new[] { 722.7f, 438.2f }, new[] { 708.8f, 440.7f }, new[] { 647.2f, 436.8f }, new[] { 593.8f, 448.5f }, new[] { 621.0f, 439.8f }, new[] { -497.9f, 449.8f }
    };

    private static readonly float[][] BackdropBeyond =
    {
        new[] { 441.0f, 369.7f }, new[] { 443.1f, 330.8f }, new[] { 468.1f, 292.6f }, new[] { 461.2f, 345.2f }, new[] { 548.3f, 333.3f }, new[] { 665.5f, 360.3f }, new[] { -454.1f, 340.3f }
    };

    private static void UptownLookSouth(LookWorld now)
    {
        var rects = new List<GroundRect>(4);
        SouthRules.Hides(rects);
        int stood = 0, kept = 0;
        foreach (float[] at in BackdropFront)
        {
            stood += InAny(rects, at[0], at[1]) ? 1 : 0;
        }
        foreach (float[] at in BackdropBeyond)
        {
            kept += InAny(rects, at[0], at[1]) ? 1 : 0;
        }
        Check(stood == BackdropFront.Length && kept == 0 && rects.Count == 2,
            "uptown look: the " + BackdropFront.Length + " boxes of the game's backdrop that stood against the avenue's south pavement are put away (" + stood + "), the pieces behind them and Manhattan's own stay (" + kept + " of "
            + BackdropBeyond.Length + " put away)");
        // The row: every block's frontage built, side by side, on the pavement's back, facing the avenue, no piece over another or outside its block.
        float front = SouthRules.Front;
        int pieces = 0, bad = 0;
        float covered = 0f, frontage = 0f;
        foreach (int column in SouthRules.Columns)
        {
            SouthRules.Strip(column, out float x0, out float x1);
            frontage += x1 - x0;
            var row = new List<float[]>();
            foreach (FrontFill f in now.Fills)
            {
                if (f.Lot.Facing == 0f && f.Lot.Z < front && f.Lot.Z > front - SouthRules.Depth && f.Lot.X > x0 && f.Lot.X < x1)
                {
                    DesignEntry d = now.Designs[f.Design];
                    NewBuildingsRules.Footprint(d, f.Lot, out float cx, out float cz, out float hw, out float hd);
                    row.Add(new[] { cx - hw, cz - hd, cx + hw, cz + hd });
                    covered += hw * 2f;
                    pieces++;
                    bad += cx - hw < x0 - 0.01f || cx + hw > x1 + 0.01f || cz + hd > front + 0.2f || cz - hd < front - SouthRules.Depth - 0.01f || !NewBuildingsRules.Copyable(d) ? 1 : 0;
                }
            }
            row.Sort((a, b) => a[0].CompareTo(b[0]));
            for (int i = 1; i < row.Count; i++)
            {
                bad += row[i][0] < row[i - 1][2] - 0.05f ? 1 : 0;
            }
        }
        Check(pieces == now.South && pieces >= 25 && bad == 0 && covered / frontage >= 0.9f,
            "uptown look: " + pieces + " of the game's buildings stand along the avenue's south pavement in " + SouthRules.Columns.Length + " blocks, " + (100f * covered / frontage).ToString("0", CultureInfo.InvariantCulture)
            + "% of their frontage, facing it, none over another or past its block (" + bad + ")");
        Check(SouthRules.Columns.Length == 7 && Array.IndexOf(SouthRules.Columns, 3) >= 0 && Array.IndexOf(SouthRules.Columns, 17) >= 0 && Array.IndexOf(SouthRules.Columns, 9) < 0,
            "uptown look: the row is on the blocks that have the backdrop's ground south of the avenue (three west of 9th Street, four east of 1st Street), never in front of Manhattan's own buildings");
    }

    private static bool InAny(List<GroundRect> rects, float x, float z)
    {
        foreach (GroundRect r in rects)
        {
            if (r.Holds(x, z))
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- the pick

    private static void UptownLookPick(LookWorld now)
    {
        // A lot with an open end takes a design with a side there that has windows or can be given them, where the catalogue has one.
        var ends = new Dictionary<string, LotEnds>(StringComparer.Ordinal);
        LookRules.Open(now.Plan, id => now.Built.Contains(id), ends);
        int open = 0, finished = 0;
        foreach (LookPiece p in now.Pieces)
        {
            if (p.Filler || !ends.TryGetValue(p.Id, out LotEnds e) || !e.Any)
            {
                continue;
            }
            open++;
            finished += LookRules.Finished(p.Design, e, p.Ground.Facing) ? 1 : 0;
        }
        Check(open > 150 && finished >= open - 12, "uptown look: of " + open + " buildings with an end open " + finished + " wear a design whose side there can be dressed or has windows");
        DesignEntry bare = null, good = null;
        foreach (DesignEntry d in now.Designs)
        {
            bare = bare ?? (LookRules.Kind(d, false) == SideKind.Bare && LookRules.Kind(d, true) == SideKind.Bare ? d : null);
            good = good ?? (LookRules.Kind(d, false) != SideKind.Bare && LookRules.Kind(d, true) != SideKind.Bare ? d : null);
        }
        DesignEntry flat = null;
        foreach (DesignEntry d in now.Designs)
        {
            flat = flat ?? (LookRules.Flat(d, false) && LookRules.Flat(d, true) && LookRules.Kind(d, false) == SideKind.Dressable && LookRules.Kind(d, true) == SideKind.Dressable ? d : null);
        }
        var toRoad = new LotEnds { Low = true, High = true, LowRoad = true, HighRoad = true };
        Check(flat != null && !LookRules.Finished(flat, toRoad, 180f) && LookRules.Finished(flat, new LotEnds { Low = true, High = true }, 180f) && LookRules.Finished(flat, default, 180f),
            "uptown look: a design with a plain wall of flat colour on both sides is no finish for a lot whose ends are open to a road, and does for one open to a yard or an open lot, or shut");
        var openBoth = new LotEnds { Low = true, High = true };
        Check(bare != null && good != null && !LookRules.Finished(bare, openBoth, 180f) && LookRules.Finished(good, openBoth, 180f) && LookRules.Finished(bare, default, 180f),
            "uptown look: a design with a wall nothing can be done with is no finish for a lot with open ends, and does for one with none");
        Check(LookRules.RightIsHigh(0f) && !LookRules.RightIsHigh(90f) && !LookRules.RightIsHigh(180f) && LookRules.RightIsHigh(270f),
            "uptown look: the strip's higher end (east or north) is on the right of a lot facing north or west, on the left of one facing east or south");
        BuildingsLot corner = now.Plan.Find(l => l.Id == "uptown_2_12_0001");
        Check(ends.TryGetValue(corner.Id, out LotEnds ce) && ce.Low && ce.LowRoad && !ce.High,
            "uptown look: 41 Summit Avenue, the first lot of its strip east of 3rd Street, has its west end open to the street and its east end against its neighbour");
        BuildingsLot row1 = now.Plan.Find(l => l.Id == "uptown_1_15_0012");
        Check(ends.TryGetValue(row1.Id, out LotEnds re) && re.Low && re.LowRoad,
            "uptown look: 1 Ridge Street, the first lot of its strip, shows its south end to 1st Avenue's continuation, which the plan lays no building along");
    }

    // ---------------------------------------------------------------- the game's side, as written

    private static void UptownLookSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Source(string folder, string file) => File.ReadAllText(System.IO.Path.Combine(scripts, folder, file));
        string ground = Source("NewBuildings", "CopyGame.Ground.cs"), gameGround = Source("Uptown", "GroundGame.cs"), made = Source("NewBuildings", "NewBuildingsDirector.cs");
        string director = Source("Uptown", "LifeDirector.cs"), dress = Source("Uptown", "LifeDirector.Dress.cs"), walls = Source("Uptown", "SideWalls.cs"), guide = Source("Uptown", "LifeGuide.cs");
        string frontage = Source("Uptown", "LifeDirector.Frontage.cs"), groundDirector = Source("Uptown", "GroundDirector.cs"), test = Source("Uptown", "LifeDirector.LookTests.cs");
        Check(ground.Contains("!OfEdge(hit)") && ground.Contains("hit.transform.IsChildOf(OwnEdge)") && gameGround.Contains("CopyGame.OwnEdge = _ground.transform") && gameGround.Contains("CopyGame.OwnEdge = null"),
            "uptown look: the district's own walls are never something solid on a lot (the east ring's first lot stood down for the corner wall), and the edge is let go with the city");
        Check(ground.Contains("OfLitter(hit)") && ground.Contains("\"SM_Trash Bag\"") && ground.Contains("Litter.Add(hit.gameObject)") && ground.Contains("public static int PutAwayLitter()") && made.Contains("CopyGame.PutAwayLitter()"),
            "uptown look: the game's bags of litter on a lot are put away once its building stands, no reason to refuse it (a bag kept the first lot of 1 Cedar Street's neighbour empty)");
        Check(director.Contains("int dressed = _lives ? Dress() : 0;") && director.IndexOf("LifeGame.StandFillers(Fills)", StringComparison.Ordinal) < director.IndexOf("Dress()", StringComparison.Ordinal)
              && dress.Contains("catch (Exception e)") && dress.Contains("SideWalls.Clear()"),
            "uptown look: the windows are laid at the load after the buildings and the fillers stand, a fault leaving the buildings as the game has them");
        Check(walls.Contains("Guard.Wrap<bool>(OnMap)") && walls.Contains("SceneFinds.InMemory<Material>") && walls.Contains("go.transform.SetParent(root, false)") && !walls.Contains("Update(") && !walls.Contains("LateUpdate("),
            "uptown look: the overlays are children of their building (they stand, hide and go with it), the materials found once behind the loading screen, the map's toggle through the guard, nothing a frame");
        Check(frontage.Contains("SouthRules.Plan(designs, Fills)") && groundDirector.Contains("SouthRules.Hides(_rects)"),
            "uptown look: the south row is planned with the other fillers and the backdrop's boxes there are put away with the rest of the backdrop's");
        Check(guide.Contains("reallife_uptown_test_look") && guide.Contains("LifeDirector.TestLook") && test.Contains("Uptown check (look census)") && test.Contains("LookWhy") && test.Contains("NearMetres"),
            "uptown look: Options > Tests has the look census, its line is 'Uptown check (look census)' with the faults near the player named first, PASS when none");
    }

    // ---------------------------------------------------------------- the texts

    private static void UptownLookTexts()
    {
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (words == null || words.Length < 3 || words[0] == null)
        {
            Check(false, "uptown look texts: the languages could not be read");
            return;
        }
        string[] keys = { "reallife_uptown_test_look", "reallife_uptown_test_look_done" };
        for (int l = 0; l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (words[l] == null || !words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - ")
                    || (l > 0 && words[0].TryGetValue(key, out string english) && NamesOf(english) != NamesOf(value)))
                {
                    missing = missing ?? key;
                }
            }
            Check(missing == null, "uptown look texts: the test's two texts are in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "both") + ")");
        }
        int noticeLength = words[0].TryGetValue("reallife_uptown_test_look_done", out string notice) ? notice.Length : int.MaxValue;
        Check(noticeLength <= 240, "uptown look texts: the notice is at most 240 characters (" + noticeLength + ")");
        var fixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Fixed: Uptown's plain side walls now have windows, buildings stand where grey boxes lined 1st Avenue's south side, most bare yards behind buildings are built over, and two lots that stood empty are built." },
            { "pt", "Corrigido: as paredes laterais lisas de Uptown agora têm janelas, há prédios onde caixas cinzentas ladeavam o lado sul da 1st Avenue, a maioria dos pátios vazios atrás dos prédios foi ocupada e dois lotes que estavam vazios agora têm prédio." },
            { "zh-cn", "已修复：上城光秃秃的侧墙现在有了窗户，第一大道南侧原来的灰色方块处现在是建筑，楼后空着的院子大多盖上了房子，两块原本空着的地也盖上了楼。" },
            { "de", "Behoben: Die kahlen Seitenwände in Uptown haben jetzt Fenster, an der Südseite der 1st Avenue stehen Gebäude, wo graue Kisten waren, die meisten leeren Höfe hinter den Gebäuden sind bebaut und zwei leer stehende Grundstücke haben jetzt ein Gebäude." },
            { "fr", "Corrigé : les murs latéraux nus d'Uptown ont maintenant des fenêtres, des immeubles se dressent du côté sud de la 1st Avenue là où il y avait des boîtes grises, la plupart des cours vides derrière les immeubles sont construites et deux terrains restés vides ont maintenant un immeuble." },
            { "es", "Corregido: los muros laterales lisos de Uptown ahora tienen ventanas, hay edificios en el lado sur de la 1st Avenue donde había cajas grises, la mayoría de los patios vacíos detrás de los edificios están construidos y dos solares que estaban vacíos ahora tienen edificio." },
            { "ru", "Исправлено: у голых боковых стен Uptown теперь есть окна, на южной стороне 1st Avenue стоят здания вместо серых коробок, большинство пустых дворов за зданиями застроены, а два пустовавших участка получили здания." },
            { "nl", "Opgelost: de kale zijmuren in Uptown hebben nu ramen, aan de zuidkant van de 1st Avenue staan gebouwen waar grijze dozen stonden, de meeste lege achtererven achter de gebouwen zijn bebouwd en twee lege kavels hebben nu een gebouw." },
            { "tr", "Düzeltildi: Uptown'daki çıplak yan duvarlarda artık pencereler var, 1st Avenue'nun güney tarafında gri kutuların yerinde binalar duruyor, binaların arkasındaki boş avluların çoğu inşa edildi ve boş duran iki parsele bina yapıldı." },
            { "pl", "Naprawiono: gołe ściany boczne w Uptown mają teraz okna, po południowej stronie 1st Avenue stoją budynki w miejscu szarych skrzynek, większość pustych podwórzy za budynkami jest zabudowana, a dwie puste działki mają teraz budynki." },
            { "ko", "수정: 업타운의 맨 옆벽에 창문이 생겼고, 1st Avenue 남쪽에는 회색 상자 대신 건물이 서 있으며, 건물 뒤의 빈 안뜰 대부분에 건물이 들어서고, 비어 있던 부지 두 곳에도 건물이 들어섭니다." }
        };
        for (int l = 0; l < words.Length && l < codes.Length; l++)
        {
            Check(fixes.TryGetValue(codes[l], out string fix) && WhatsNewHas(words[l], fix), "uptown look texts: What's new has the fix line in some row in " + codes[l]);
        }
    }
}
