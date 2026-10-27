using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RealLifeOverhaul;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// The vehicle entrances of the buildings the copy engine copies (the owner, 27 Oct 2026, of a player's question how to get cars and
// trucks in and out of a warehouse, which Uncle Fred had answered with "the copied warehouses in Uptown have their vehicle entrances
// disabled": "this breaks the immersion and the game"): the game's garage doors (DriveInEntrance) and underground car parks
// (UndergroundParkingEntrance) of Build 3682's designs, read from the game's own scenes by the kit's filllab/facts/design_gates.py
// (Census/design-gates-3682.json: 39 designs with garage doors, 62 doors, 3 designs with a car park), against the copy engine's rules
// (GateRules: which doors a made building keeps, which look out on the street a design is stood to, the number a copy's car park is
// given), the district's plan (its warehouses wear only a look whose doors a car reaches from the street; the mouth of each door,
// MouthRules, is clear of every building and of the bus stops; the parking lanes are cut round it), the whole-building check's item,
// the engine's source read as text (the doors are kept on a working copy, not taken off) and the texts.
internal static partial class Program
{
    private const string DesignGates = "design-gates-3682.json";

    // The census read onto the fixture's designs: each design that has them gets its Gates and Parks as the engine measures them
    // from the building (CopyGame.Describe). Returns how many designs were given any, or -1 when the census is not there.
    private static int GateFixture(List<FrontDesign> fixture)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "..", "..", "..", "RealLifeOverhaul.Build", "Census", DesignGates));
        if (!File.Exists(path))
        {
            return -1;
        }
        var byKey = new Dictionary<string, FrontDesign>(StringComparer.Ordinal);
        foreach (FrontDesign f in fixture)
        {
            byKey[f.Now.Name + "|" + f.Now.Size] = f;
        }
        int given = 0;
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
        {
            foreach (JsonElement row in doc.RootElement.GetProperty("gated").EnumerateArray())
            {
                if (!byKey.TryGetValue(row[0].GetString(), out FrontDesign f))
                {
                    continue;
                }
                var gates = new List<Gate>(2);
                foreach (JsonElement g in row[3].EnumerateArray())
                {
                    JsonElement at = g.GetProperty("at"), closed = g.GetProperty("closed");
                    gates.Add(new Gate
                    {
                        Id = g.GetProperty("id").GetInt32(), X = at[0].GetSingle(), Z = at[1].GetSingle(), Facing = at[2].GetSingle(),
                        Width = closed[2].GetSingle(), Depth = closed[3].GetSingle()
                    });
                }
                var parks = new List<Park>(1);
                foreach (JsonElement p in row[4].EnumerateArray())
                {
                    JsonElement at = p.GetProperty("at"), vehicle = p.GetProperty("vehicle"), player = p.GetProperty("player");
                    parks.Add(new Park
                    {
                        Number = p.GetProperty("number").GetInt32(), Version = p.GetProperty("version").GetInt32(), X = at[0].GetSingle(), Z = at[1].GetSingle(), Facing = at[2].GetSingle(),
                        ExitX = vehicle[0].GetSingle(), ExitZ = vehicle[1].GetSingle(), ExitFacing = vehicle[2].GetSingle(), PlayerX = player[0].GetSingle(), PlayerZ = player[1].GetSingle()
                    });
                }
                f.Now.Gates = gates.Count > 0 ? gates.ToArray() : null;
                f.Now.Parks = parks.Count > 0 ? parks.ToArray() : null;
                given++;
            }
        }
        return given;
    }

    // The real plan with the declarations the district makes of its built lots (LifeDirector: each lot's structure picked by its id,
    // its other structures, the look its class wears) laid out along the streets by the district's own rules. The fillers into
    // <fills>; returns the declarations by lot id.
    private static Dictionary<string, MadeSpec> GatePlanned(List<DesignEntry> designs, out List<BuildingsLot> plan, out List<FrontFill> fills)
    {
        plan = UptownRealPlan();
        List<int> built = BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>());
        HashSet<string> leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        var specs = new Dictionary<string, MadeSpec>(StringComparer.Ordinal);
        var into = new List<BuildingsStructure>();
        foreach (int i in built)
        {
            BuildingsLot lot = plan[i];
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
            specs[lot.Id] = new MadeSpec
            {
                Id = lot.Id, Street = lot.Street, Number = lot.Number, Size = options[pick].Size, Version = options[pick].Version, Type = options[pick].Type, Life = true,
                Lots = new[] { new MadeLot(lot.X, lot.Z, lot.Facing, lot.Width, lot.Depth) }, Others = others, Design = BuildingsRules.LookOf(lot.Class)
            };
        }
        fills = new List<FrontFill>();
        List<BuildingsLot> lots = plan;
        FrontageRules.Plan(lots, id => specs.TryGetValue(id, out MadeSpec s) ? s : null, designs, fills);
        return specs;
    }

    private static void GateChecks()
    {
        List<FrontDesign> fixture = UptownPlaceFixture();
        if (fixture == null)
        {
            Check(false, "gates: the game's designs (" + DesignFronts + ") could not be read");
            return;
        }
        var designs = new List<DesignEntry>(fixture.Count);
        foreach (FrontDesign f in fixture)
        {
            designs.Add(f.Now);
        }
        GateCensus(designs);
        GateLive();
        GateServes();
        GatePlan(designs);
        GateLanes();
        GateParks(designs);
        GateWhole();
        GateSources();
        GateTexts();
    }

    // ---------------------------------------------------------------- the census

    // The vehicle slots the game gives a warehouse structure (BuildingSizeData.numberOfVehicleSlots, Build 3682): H one, I, P and Q two.
    private static int GateBays(string size)
    {
        return size == "ba:buildingsize_h" ? 1 : size == "ba:buildingsize_i" || size == "ba:buildingsize_p" || size == "ba:buildingsize_q" ? 2 : 0;
    }

    private static void GateCensus(List<DesignEntry> designs)
    {
        int withEntrance = 0, withDoors = 0, doors = 0, parks = 0, serving = 0, copyable = 0, servingH = 0, servingI = 0, parkServing = 0;
        bool bays = true, ids = true, sized = true;
        foreach (DesignEntry d in designs)
        {
            withEntrance += d.Gates != null || d.Parks != null ? 1 : 0;
            if (d.Gates != null)
            {
                withDoors++;
                doors += d.Gates.Length;
                if (GateRules.Serves(d))
                {
                    bool can = NewBuildingsRules.Copyable(d);
                    serving++;
                    copyable += can ? 1 : 0;
                    servingH += can && d.Size == "ba:buildingsize_h" ? 1 : 0;
                    servingI += can && d.Size == "ba:buildingsize_i" ? 1 : 0;
                }
                bays &= d.Type != GateRules.WarehouseType || d.Gates.Length == GateBays(d.Size);
                var seen = new HashSet<int>();
                foreach (Gate g in d.Gates)
                {
                    ids &= g.Id >= 0 && g.Id < d.Gates.Length && seen.Add(g.Id);
                    sized &= g.Width >= 3f && g.Width <= 5.5f && g.Depth >= 3f && g.Depth <= 4f;
                }
            }
            if (d.Parks != null)
            {
                parks += d.Parks.Length;
                parkServing += GateRules.ParkServes(d) ? 1 : 0;
            }
        }
        Check(designs.Count > 500 && withEntrance == 42 && withDoors == 39 && doors == 62 && parks == 3,
            "gates: the census of the game's vehicle entrances is read onto the designs (" + withEntrance + " of " + designs.Count + " with one: " + withDoors + " with garage doors, "
            + doors + " doors, " + parks + " car parks)");
        Check(bays && ids && sized,
            "gates: a warehouse design has a garage door for each vehicle slot of its structure (H one, I, P and Q two), their ids 0 up, each 3 to 5.5 m wide: the door's id plus one is the slot it enters by");
        Check(serving == 23 && copyable == 21 && servingH == 7 && servingI == 7 && parkServing == 2,
            "gates: " + serving + " of the " + withDoors + " designs with garage doors have every door on the side the design is stood to its street by (" + copyable + " the engine can copy, "
            + servingH + " of size H and " + servingI + " of size I), the rest on a side wall; " + parkServing + " of the 3 car parks have the ramp on that side");
    }

    // ---------------------------------------------------------------- which door a copy keeps

    private static void GateLive()
    {
        string warehouse = GateRules.WarehouseType;
        Check(GateRules.Live(false, warehouse, 1, 0) && !GateRules.Live(false, warehouse, 1, 1) && GateRules.Live(false, warehouse, 2, 0) && GateRules.Live(false, warehouse, 2, 1)
              && !GateRules.Live(false, warehouse, 2, 2) && !GateRules.Live(false, warehouse, 0, 0) && !GateRules.Live(false, warehouse, 2, -1),
            "gates: a warehouse keeps the garage door of each vehicle slot its structure has (an H its first, an I both) and no other");
        Check(!GateRules.Live(false, "ba:buildingtype_office", 2, 0) && !GateRules.Live(false, "ba:buildingtype_retail", 2, 1) && !GateRules.Live(false, NewBuildingsRules.HomeType, 1, 0)
              && !GateRules.Live(false, NewBuildingsRules.SpecialType, 2, 0) && GateRules.Live(true, NewBuildingsRules.SpecialType, 0, 3) && !GateRules.Live(false, null, 2, 0),
            "gates: a shop, an office or a home in a warehouse's look has the doors shut, no car being taken into an interior with no bay; a special place copied whole keeps its own");
    }

    // ---------------------------------------------------------------- which designs serve a street

    // A design 20 m across and 40 deep (its footprint x -10 to 10, z -20 to 20), its door side turned <front> degrees from its root's forward.
    private static DesignEntry GateDesign(float front, params Gate[] gates)
    {
        var d = new DesignEntry { Name = "made-up", Size = "ba:buildingsize_h", Type = GateRules.WarehouseType, DoorFacing = front };
        NewBuildingsRules.Begin(d, -10f, 10f, -20f, 20f, 8f);
        d.Gates = gates.Length > 0 ? gates : null;
        return d;
    }

    private static Gate GateAt(int id, float x, float z, float facing)
    {
        return new Gate { Id = id, X = x, Z = z, Facing = facing, Width = 3.25f, Depth = 3.25f };
    }

    private static void GateServes()
    {
        DesignEntry front = GateDesign(0f, GateAt(0, 3f, 21.65f, 0f));        // 1.65 m before the front, looking out: the game's own stand
        DesignEntry both = GateDesign(0f, GateAt(0, 3f, 21.65f, 0f), GateAt(1, -4f, 21.65f, 0f));
        DesignEntry slight = GateDesign(0f, GateAt(0, 3f, 21f, 15f));
        DesignEntry turned = GateDesign(90f, GateAt(0, 11.65f, 3f, 90f));      // the same, its front on +X
        DesignEntry inFront = GateDesign(0f, GateAt(0, 3f, 17f, 0f));          // 3 m behind the front: still reached from the street
        DesignEntry side = GateDesign(0f, GateAt(0, 10f, 5f, 90f));
        DesignEntry deepIn = GateDesign(0f, GateAt(0, 3f, 14f, 0f));
        DesignEntry farOut = GateDesign(0f, GateAt(0, 3f, 24.5f, 0f));
        DesignEntry across = GateDesign(0f, GateAt(0, 3f, 21f, 25f));
        DesignEntry mixed = GateDesign(0f, GateAt(0, 3f, 21.65f, 0f), GateAt(1, -10f, 5f, 270f));
        Check(GateRules.Serves(front) && GateRules.Serves(both) && GateRules.Serves(slight) && GateRules.Serves(turned) && GateRules.Serves(inFront)
              && Near(GateRules.Behind(front, 3f, 21.65f), -1.65f, 0.001f) && Near(GateRules.Behind(turned, 11.65f, 3f), -1.65f, 0.001f),
            "gates: a design whose garage doors all look out the way its door does, at its front, serves its street, whichever way it is turned (a door 1.65 m before the front, as the game's stand)");
        Check(!GateRules.Serves(side) && !GateRules.Serves(deepIn) && !GateRules.Serves(farOut) && !GateRules.Serves(across) && !GateRules.Serves(mixed) && !GateRules.Serves(GateDesign(0f))
              && !GateRules.Serves(null) && !GateRules.HasPark(front),
            "gates: a door on a side wall, one 6 m behind the front or 4.5 m before it, one turned 25 degrees off, one of two on a side wall, and a design with none, serve no street");
        var park = new DesignEntry { Name = "made-up", Size = "ba:buildingsize_l", Type = "ba:buildingtype_retail", DoorFacing = 0f };
        NewBuildingsRules.Begin(park, -10f, 10f, -20f, 20f, 8f);
        park.Parks = new[] { new Park { Number = 3, Version = 1, X = 0f, Z = 21f, Facing = 0f, ExitX = 0f, ExitZ = 22f, ExitFacing = 0f } };
        DesignEntry sideRamp = new DesignEntry { Name = "made-up", Size = "ba:buildingsize_l", Type = "ba:buildingtype_retail", DoorFacing = 0f };
        NewBuildingsRules.Begin(sideRamp, -10f, 10f, -20f, 20f, 8f);
        sideRamp.Parks = new[] { new Park { Number = 3, Version = 1, X = 10f, Z = 0f, Facing = 90f, ExitX = 11f, ExitZ = 0f, ExitFacing = 90f } };
        Check(GateRules.HasPark(park) && GateRules.ParkServes(park) && GateRules.HasPark(sideRamp) && !GateRules.ParkServes(sideRamp) && !GateRules.ParkServes(front),
            "gates: a car park's ramp serves the street when a vehicle comes out of it at the front, looking out, and not when it comes out on a side");
        GateRules.World(100f, 200f, 90f, 3f, 5f, out float wx, out float wz);
        NewBuildingsRules.DoorAt(new DesignEntry { DoorX = 3f, DoorZ = 5f }, 100f, 200f, 90f, out float dx, out float dz);
        Check(Near(wx, dx, 0.001f) && Near(wz, dz, 0.001f) && GateRules.Quarter(90f, 0f) == 1 && GateRules.Quarter(90f, 90f) == 2 && GateRules.Quarter(270f, 90f) == 0
              && GateRules.Quarter(0f, 358f) == 0 && GateRules.Quarter(180f, 0f) == 2 && GateRules.Quarter(0f, 270f) == 3,
            "gates: a door's place in the world is a point's, as the engine gives a design's door, and its way out a quarter turn of the grid (north 0, east 1, south 2, west 3)");
    }

    // ---------------------------------------------------------------- the real plan

    // A design stood on a ground, and whose it is, for a mouth to be laid against.
    private struct GatePiece
    {
        public DesignEntry Design;
        public MadeLot Ground;
        public string Id;
    }

    private static void GatePlan(List<DesignEntry> designs)
    {
        Dictionary<string, MadeSpec> specs = GatePlanned(designs, out List<BuildingsLot> plan, out List<FrontFill> fills);
        var lotOf = new Dictionary<string, BuildingsLot>(plan.Count * 2, StringComparer.Ordinal);
        foreach (BuildingsLot l in plan)
        {
            lotOf[l.Id] = l;
        }
        var ends = new Dictionary<string, LotEnds>(plan.Count * 2, StringComparer.Ordinal);
        LookRules.Open(plan, id => specs.ContainsKey(id), ends);
        var pieces = new List<GatePiece>(1024);
        foreach (MadeSpec s in specs.Values)
        {
            int d = NewBuildingsRules.Pick(designs, s.Size, s.Type, s.Design, s.Lots[0].Width, s.Lots[0].Depth, s.Id);
            if (d >= 0)
            {
                pieces.Add(new GatePiece { Design = designs[d], Ground = s.Lots[0], Id = s.Id });
            }
        }
        foreach (FrontFill f in fills)
        {
            pieces.Add(new GatePiece { Design = designs[f.Design], Ground = f.Lot, Id = "a filler" });
        }
        var poleX = new List<float>(64);
        var poleZ = new List<float>(64);
        StreetsRules.StopPoles(poleX, poleZ);

        int warehouses = 0, named = 0, serving = 0, bayless = 0, was = 0, doorCount = 0, wrongWay = 0, offFrontage = 0, wrongReach = 0, meets = 0, onPoles = 0, onPolesWas = 0;
        var looks = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var mouth = new List<GroundRect>(1);
        foreach (MadeSpec s in specs.Values)
        {
            if (s.Type != GateRules.WarehouseType)
            {
                continue;
            }
            warehouses++;
            BuildingsLot lot = lotOf[s.Id];
            // The look the pick had before the doors were asked after: how many of the warehouses it left with no door a car could use.
            var asked = new FrontageRules.Want { Ends = ends.TryGetValue(s.Id, out LotEnds open) ? open : default, Facing = lot.Facing, Sides = true, Depth = lot.Depth };
            int old = FrontageRules.Look(designs, s.Size, s.Version, s.Type, lot.Width, lot.Depth, s.Id, asked);
            was += old >= 0 && !GateRules.Serves(designs[old]) ? 1 : 0;
            // And the look it would have had, serving its street but with no regard to the bus stops: how many of those would have a stop's pole in their mouth.
            asked.Gates = true;
            int plain = FrontageRules.Look(designs, s.Size, s.Version, s.Type, lot.Width, lot.Depth, s.Id, asked);
            onPolesWas += plain >= 0 && MouthRules.TakesStop(designs[plain], new MadeLot(lot.X, lot.Z, lot.Facing, lot.Width + 0.01f, lot.Depth), poleX, poleZ) ? 1 : 0;
            MadeLot ground = s.Lots[0];
            int d = NewBuildingsRules.Pick(designs, s.Size, s.Type, s.Design, ground.Width, ground.Depth, s.Id);
            if (d < 0)
            {
                continue;
            }
            named++;
            DesignEntry design = designs[d];
            serving += GateRules.Serves(design) ? 1 : 0;
            bayless += design.Gates == null || design.Gates.Length < GateBays(s.Size) ? 1 : 0;
            string look = design.Name + " (" + design.Size.Substring(design.Size.LastIndexOf('_') + 1).ToUpperInvariant() + ")";
            looks[look] = looks.TryGetValue(look, out int n) ? n + 1 : 1;
            NewBuildingsRules.Place(design, ground, out float x, out float z, out float yaw);
            int street = GateRules.Quarter(0f, ground.Facing);
            bool alongX = street == 0 || street == 2;
            double turn = ground.Facing * Math.PI / 180.0;
            float frontX = ground.X + (float)Math.Sin(turn) * ground.Depth * 0.5f, frontZ = ground.Z + (float)Math.Cos(turn) * ground.Depth * 0.5f;
            float from = (alongX ? ground.X : ground.Z) - ground.Width * 0.5f, to = from + ground.Width;
            foreach (Gate g in design.Gates ?? new Gate[0])
            {
                doorCount++;
                GateRules.World(x, z, yaw, g.X, g.Z, out float wx, out float wz);
                int way = GateRules.Quarter(yaw, g.Facing);
                wrongWay += way == street ? 0 : 1;
                float along = alongX ? wx : wz;
                offFrontage += along >= from && along <= to ? 0 : 1;
                mouth.Clear();
                MouthRules.Gate(wx, wz, way, g.Width, mouth);
                GroundRect m = mouth[0];
                // From the lot's front line (the back of the pavement) to 3 m past the kerb: the pavement (3.6 to 3.7 m) and the 3 m.
                float reach = street == 0 ? m.Z2 - frontZ : street == 2 ? frontZ - m.Z1 : street == 1 ? m.X2 - frontX : frontX - m.X1;
                wrongReach += reach >= 6.5f && reach <= 6.8f ? 0 : 1;
                foreach (GatePiece p in pieces)
                {
                    if (string.Equals(p.Id, s.Id, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    NewBuildingsRules.Footprint(p.Design, p.Ground, out float cx, out float cz, out float hw, out float hd);
                    meets += NewBuildingsRules.Overlaps(m.X1, m.Z1, m.X2, m.Z2, cx, cz, p.Ground.Facing, hw, hd, -NewBuildingsRules.Touch) ? 1 : 0;
                }
                for (int k = 0; k < poleX.Count; k++)
                {
                    onPoles += MouthRules.Holds(mouth, poleX[k], poleZ[k], MouthRules.StopReach) ? 1 : 0;
                }
            }
        }
        var words = new List<string>(looks.Count);
        foreach (KeyValuePair<string, int> k in looks)
        {
            words.Add(k.Value + " " + k.Key);
        }
        Check(warehouses == 35 && named == warehouses && serving == warehouses && bayless <= 2,
            "gates: on the real plan all " + warehouses + " warehouses wear a look the engine takes by name (" + named + ") whose garage doors all look out on their street (" + serving
            + "), a door for each of their structure's bays but " + bayless + " (where the only look clear of a bus stop has fewer doors than bays): " + string.Join(", ", words));
        Check(was >= 10,
            "gates: the pick that does not ask after the doors would leave " + was + " of the " + warehouses + " warehouses in a look with no door a car can use (an office's, a shop's, a home's, or a warehouse's door on a side wall)");
        Check(doorCount >= 40 && wrongWay == 0 && offFrontage == 0,
            "gates: each of the " + doorCount + " garage doors of the warehouses looks out on the street its lot fronts (" + wrongWay + " the other way) and stands on its lot's frontage (" + offFrontage + " off it)");
        Check(wrongReach == 0 && meets == 0,
            "gates: the mouth of each door runs from the door over the pavement to 3 m past the kerb (" + wrongReach + " that do not) and meets no other building or filler (" + meets + ")");
        Check(onPoles == 0 && onPolesWas >= 1 && poleX.Count >= 20,
            "gates: no door's mouth takes the pole or the shelter of a stop the routes want (" + onPoles + " of the " + poleX.Count + " poles within " + MouthRules.StopReach
            + " m of a mouth); a look picked with no regard to the stops would have (" + onPolesWas + " of the warehouses)");

        // The mouth reaches as far past the kerb as the wholesale store's: the same 3 m over the kerb's parking lane.
        BigPlace store = BigPlacesRules.Of(BigKind.Wholesale);
        bool found = BigPlacesRules.Ground(store, plan, out BuildingsLot place);
        var big = new List<GroundRect>(2);
        if (found)
        {
            BigPlacesRules.Mouths(store, place, big);
        }
        float gx = place.X, gz = place.Z + place.Depth * 0.5f;
        var mine = new List<GroundRect>(1);
        MouthRules.Gate(gx, gz, GateRules.Quarter(0f, place.Facing), 3.25f, mine);
        bool same = false;
        foreach (GroundRect b in big)
        {
            same |= mine.Count == 1 && (Near(b.Z2, mine[0].Z2, 0.02f) || Near(b.Z1, mine[0].Z1, 0.02f) || Near(b.X2, mine[0].X2, 0.02f) || Near(b.X1, mine[0].X1, 0.02f));
        }
        Check(found && big.Count > 0 && same, "gates: a garage door's mouth reaches as far past the kerb as the wholesale store's (" + BigPlacesRules.MouthIntoRoad + " m)");
    }

    // ---------------------------------------------------------------- the parking lanes round a mouth

    private static void GateLanes()
    {
        Check(MouthRules.Side == 2f && MouthRules.LaneMargin == 1f && MouthRules.LaneLeast == 6f && MouthRules.StopReach == 6f && BigPlacesRules.MouthIntoRoad == 3f
              && GateRules.FacingNear == 20f && GateRules.FrontBehind == 4f && GateRules.FrontAhead == 3f,
            "gates: a mouth is the door's width and 2 m either side, to 3 m past the kerb; a lane keeps 1 m clear either side of it and no piece under 6 m; a stop's pole and shelter 6 m; "
            + "a door serves its street within 20 degrees of the front, 4 m behind it and 3 m before it");
        float road = LayoutRules.AvenueZ(2);
        float pavement = road - LayoutRules.AvenueHalf;             // the back of the pavement of the avenue's south side
        float doorZ = pavement + 1.65f;                              // a warehouse's door stands 1.65 m before its front, looking north
        float laneFrom = road - LayoutRules.AvenueKerb + 1.3f;       // the game's parking lane: 1.3 m in from the kerb, 2.5 m wide
        float laneTo = laneFrom + 2.5f;
        var mouth = new List<GroundRect>();
        MouthRules.Gate(40f, doorZ, 0, 3.25f, mouth);
        GroundRect m = mouth[0];
        float half = 3.25f * 0.5f + MouthRules.Side;
        Check(mouth.Count == 1 && Near(m.X1, 40f - half, 0.01f) && Near(m.X2, 40f + half, 0.01f) && Near(m.Z1, doorZ, 0.01f)
              && Near(m.Z2, road - LayoutRules.AvenueKerb + BigPlacesRules.MouthIntoRoad, 0.01f),
            "gates: a mouth is the door's width and " + MouthRules.Side + " m either side of it, from the door to " + BigPlacesRules.MouthIntoRoad + " m past the kerb of the avenue it looks out on");
        var south = new List<GroundRect>();
        MouthRules.Gate(40f, road + LayoutRules.AvenueHalf - 1.65f, 2, 3.25f, south);
        float streetX = LayoutRules.StreetX(3);
        var east = new List<GroundRect>();
        MouthRules.Gate(streetX - LayoutRules.StreetHalf + 1.65f, 300f, 1, 3.25f, east);
        var west = new List<GroundRect>();
        MouthRules.Gate(streetX + LayoutRules.StreetHalf - 1.65f, 300f, 3, 3.25f, west);
        Check(south.Count == 1 && Near(south[0].Z1, road + LayoutRules.AvenueKerb - BigPlacesRules.MouthIntoRoad, 0.01f) && Near(south[0].Z2, road + LayoutRules.AvenueHalf - 1.65f, 0.01f)
              && east.Count == 1 && Near(east[0].X2, streetX - LayoutRules.StreetKerbWest + BigPlacesRules.MouthIntoRoad, 0.01f) && Near(east[0].Z1, 300f - half, 0.01f)
              && west.Count == 1 && Near(west[0].X1, streetX + LayoutRules.StreetKerbEast - BigPlacesRules.MouthIntoRoad, 0.01f) && Near(west[0].Z2, 300f + half, 0.01f),
            "gates: a door looking south, east or west has its mouth to the kerb of the avenue or street on that side, as one looking north");

        var from = new List<float>();
        var to = new List<float>();
        int whole = MouthRules.Pieces(0f, 100f, laneFrom, laneTo, true, new List<GroundRect>(), from, to);
        bool wholeOk = whole == 1 && Near(from[0], 0f, 0.001f) && Near(to[0], 100f, 0.001f);
        int two = MouthRules.Pieces(0f, 100f, laneFrom, laneTo, true, mouth, from, to);
        bool twoOk = two == 2 && Near(from[0], 0f, 0.001f) && Near(to[0], 40f - half - MouthRules.LaneMargin, 0.01f) && Near(from[1], 40f + half + MouthRules.LaneMargin, 0.01f) && Near(to[1], 100f, 0.001f);
        int far = MouthRules.Pieces(0f, 100f, road + 6f, road + 8.5f, true, mouth, from, to);   // a lane on the other side of the road: the mouth does not reach it
        bool farOk = far == 1 && Near(from[0], 0f, 0.001f) && Near(to[0], 100f, 0.001f);
        Check(wholeOk && twoOk && farOk && MouthRules.Meets(0f, 100f, laneFrom, laneTo, true, mouth) && !MouthRules.Meets(0f, 100f, road + 6f, road + 8.5f, true, mouth)
              && !MouthRules.Meets(0f, 30f, laneFrom, laneTo, true, mouth) && !MouthRules.Meets(0f, 100f, laneFrom, laneTo, true, null),
            "gates: a parking lane a mouth crosses is laid as the two pieces it leaves, the mouth's stretch and " + MouthRules.LaneMargin + " m either side given up; one it does not reach stays whole");
        var atEnd = new List<GroundRect>();
        MouthRules.Gate(98f, doorZ, 0, 3.25f, atEnd);
        int end = MouthRules.Pieces(0f, 100f, laneFrom, laneTo, true, atEnd, from, to);
        bool endOk = end == 1 && Near(from[0], 0f, 0.001f) && Near(to[0], 98f - half - MouthRules.LaneMargin, 0.01f);
        var atStart = new List<GroundRect>();
        MouthRules.Gate(2f, doorZ, 0, 3.25f, atStart);
        int start = MouthRules.Pieces(0f, 100f, laneFrom, laneTo, true, atStart, from, to);
        bool startOk = start == 1 && Near(from[0], 2f + half + MouthRules.LaneMargin, 0.01f) && Near(to[0], 100f, 0.001f);
        var short12 = new List<GroundRect>();
        MouthRules.Gate(6f, doorZ, 0, 3.25f, short12);
        int none = MouthRules.Pieces(0f, 12f, laneFrom, laneTo, true, short12, from, to);
        var twice = new List<GroundRect>();
        MouthRules.Gate(20f, doorZ, 0, 3.25f, twice);
        MouthRules.Gate(60f, doorZ, 0, 3.25f, twice);
        int three = MouthRules.Pieces(0f, 100f, laneFrom, laneTo, true, twice, from, to);
        Check(endOk && startOk && none == 0 && three == 3 && Near(to[2], 100f, 0.001f),
            "gates: a mouth at a lane's end leaves the one piece; a piece under " + MouthRules.LaneLeast + " m, which parks no car, is dropped, so a lane the mouths take whole is given up; two mouths leave three");
        var along = new List<GroundRect>();
        MouthRules.Gate(streetX - LayoutRules.StreetHalf + 1.65f, 300f, 1, 3.25f, along);
        float bandFrom = streetX - LayoutRules.StreetKerbWest + 1.3f, bandTo = bandFrom + 2.5f;   // a street's parking lane runs along z, 1.3 m in from its west kerb
        int streetLane = MouthRules.Pieces(250f, 350f, bandFrom, bandTo, false, along, from, to);
        Check(streetLane == 2 && Near(to[0], 300f - half - MouthRules.LaneMargin, 0.01f) && Near(from[1], 300f + half + MouthRules.LaneMargin, 0.01f),
            "gates: the same on a street, where a lane runs along z and a door looks east");
        Check(MouthRules.Holds(mouth, 40f, doorZ + 1f, MouthRules.PoleReach) && MouthRules.Holds(mouth, 40f - half - 0.3f, doorZ + 1f, MouthRules.PoleReach)
              && !MouthRules.Holds(mouth, 40f - half - 1f, doorZ + 1f, MouthRules.PoleReach) && !MouthRules.Holds(mouth, 40f, doorZ - 2f, MouthRules.PoleReach),
            "gates: a lamp post stands in a mouth when its reach meets it, and not a metre outside");
    }

    // ---------------------------------------------------------------- car parks

    private static void GateParks(List<DesignEntry> designs)
    {
        List<BuildingsLot> plan = UptownRealPlan();
        var numbers = new HashSet<int>();
        bool above = true, same = true;
        foreach (BuildingsLot l in plan)
        {
            int n = GateRules.ParkNumber(l.Id);
            numbers.Add(n);
            above &= n >= GateRules.ParkFrom && n < GateRules.ParkFrom + GateRules.ParkSpan && GateRules.IsMadePark(n);
            same &= n == GateRules.ParkNumber(new string(l.Id.ToCharArray()));
        }
        Check(above && same && numbers.Count == plan.Count && plan.Count > 500 && !GateRules.IsMadePark(5) && GateRules.ParkNumber("a") != GateRules.ParkNumber("b")
              && GateRules.ParkNumber(null) >= GateRules.ParkFrom,
            "gates: each building's car park is given a number of its own from its id, the same every time and on every machine, above the game's 5 and not shared by two of the plan's "
            + plan.Count + " lots");
        DesignEntry grand = null, murray = null, flatiron = null;
        foreach (DesignEntry d in designs)
        {
            grand = d.Name == "SM_GrandCentralStationArea_Building 09" && d.Size == "ba:buildingsize_l" ? d : grand;
            murray = d.Name == "SM_MurrayHill Section C_Building 05" && d.Size == "ba:buildingsize_n" ? d : murray;
            flatiron = d.Name == "prefab_FlatironAreaBuildingBig_1" && d.Size == "ba:buildingsize_j" ? d : flatiron;
        }
        Check(grand != null && murray != null && flatiron != null && GateRules.ParkServes(grand) && GateRules.ParkServes(murray) && !GateRules.ParkServes(flatiron)
              && NewBuildingsRules.Copyable(grand) && NewBuildingsRules.Copyable(murray) && NewBuildingsRules.Copyable(flatiron),
            "gates: of the three car parks the engine can copy, the ramps of the Grand Central area's building 09 and Murray Hill's building 05 look out on their street and are wired, Flatiron's looks out on a side and is taken off");
    }

    // ---------------------------------------------------------------- the whole-building check

    private static void GateWhole()
    {
        CompleteFacts whole = BuildModeWhole();
        whole.Type = whole.RecordType = GateRules.WarehouseType;
        whole.Warehouse = true;
        whole.WarehouseRegistration = true;
        whole.GatesWanted = 2;
        whole.GatesWhole = 2;
        whole.GatesServe = true;
        int all = CompleteRules.Passed(whole);
        Check(CompleteRules.All(all) && CompleteRules.FirstFailed(all) == null && CompleteRules.All(CompleteRules.Passed(BuildModeWhole())) && CompleteRules.Entrances(default),
            "gates: a warehouse with its garage doors whole passes the whole-building check, and so does a building that is no warehouse");
        var broken = new List<KeyValuePair<string, CompleteFacts>>();
        CompleteFacts f;
        f = whole; f.WarehouseRegistration = false; broken.Add(new KeyValuePair<string, CompleteFacts>("no vehicle slots", f));
        f = whole; f.GatesWanted = 0; f.GatesWhole = 0; broken.Add(new KeyValuePair<string, CompleteFacts>("no door a bay stands behind", f));
        f = whole; f.GatesWhole = 1; broken.Add(new KeyValuePair<string, CompleteFacts>("a door not whole", f));
        f = whole; f.GatesServe = false; broken.Add(new KeyValuePair<string, CompleteFacts>("a door on a wall", f));
        string wrong = null;
        foreach (KeyValuePair<string, CompleteFacts> b in broken)
        {
            int passed = CompleteRules.Passed(b.Value);
            if (CompleteRules.All(passed) || CompleteRules.FirstFailed(passed) != "entrances" || CompleteRules.Entrances(b.Value))
            {
                wrong = wrong ?? b.Key;
            }
        }
        string pass = CompleteRules.Line("the load", "1 Maple Street", whole, all), fail = CompleteRules.Line("the load", "1 Maple Street", broken[0].Value, CompleteRules.Passed(broken[0].Value));
        Check(wrong == null && (int)CompleteItem.Entrances == 12 && (int)CompleteItem.Count == 13 && pass.Contains("entrances PASS") && fail.Contains("entrances FAIL") && fail.EndsWith(": FAIL"),
            "gates: a warehouse with no vehicle slots, no door a bay stands behind, a door that is not whole or one on a wall fails the check's item, named in its line (" + (wrong ?? "each fails alone") + ")");
    }

    // ---------------------------------------------------------------- the game's side, read as text

    private static void GateSources()
    {
        string copy = ModSource(@"NewBuildings\CopyGame.cs"), rules = ModSource(@"NewBuildings\NewBuildingsRules.cs"), copies = ModSource(@"NewBuildings\BuildingCopies.cs");
        string frontage = ModSource(@"Uptown\FrontageRules.cs"), art = ModSource(@"Uptown\GroundGame.Art.cs"), mouths = ModSource(@"Uptown\GroundGame.Mouths.cs");
        string ground = ModSource(@"Uptown\GroundDirector.cs"), stand = ModSource(@"Uptown\LifeDirector.Stand.cs"), parking = ModSource(@"NewGround\ParkingGame.cs");
        string save = ModSource(@"NewBuildings\MadeGame.Save.cs"), life = ModSource(@"Uptown\LifeDirector.cs"), guide = ModSource(@"Uptown\LifeGuide.cs");
        string tests = ModSource(@"Uptown\LifeDirector.Doors.cs"), facts = ModSource(@"Uptown\LifeGame.Complete.cs"), streets = ModSource(@"Uptown\StreetsGame.cs");
        string plan = ModSource(@"TestRun\RunPlan.cs");
        Check(copy.Length > 0 && rules.Length > 0 && copies.Length > 0 && frontage.Length > 0 && art.Length > 0 && mouths.Length > 0 && ground.Length > 0 && stand.Length > 0
              && parking.Length > 0 && save.Length > 0 && life.Length > 0 && guide.Length > 0 && tests.Length > 0 && facts.Length > 0 && streets.Length > 0 && plan.Length > 0,
            "gates sources: the copy engine, the district's ground and stand, the parking and the tests are where the checks look for them");

        string template = MemberText(copy, "private static Transform MakeTemplate("), working = MemberText(copy, "public static CityBuildingController MakeWorking(");
        string wire = MemberText(copy, "private static void Wire("), measure = MemberText(copy, "private static void MeasureEntrances(");
        Check(!copy.Contains("StripWorking") && !template.Contains("DriveInEntrance") && !template.Contains("UndergroundParkingEntrance") && !template.Contains("driveInEntrances")
              && template.Contains("else if (!working)") && !copy.Contains("driveInEntrances = new DriveInEntrance[0]"),
            "gates sources: a working copy's template keeps every garage door and car park entrance of the game's building: nothing takes them off there, and no controller is left with none");
        Check(working.IndexOf("Wire(c, Designs[design], building, madeId);", StringComparison.Ordinal) is int wired && wired > 0
              && working.IndexOf("copy.gameObject.SetActive(true);", StringComparison.Ordinal) > wired
              && wire.Contains("GateRules.Live(false, building.BuildingType, slots, door.doorID)") && wire.Contains("Kept.Add(door);") && wire.Contains("c.driveInEntrances = Kept.ToArray();")
              && wire.Contains("int slots = SlotsOf(building);") && wire.Contains("GateRules.ParkServes(design)") && wire.Contains("park.parkingNumber = GateRules.ParkNumber(madeId);")
              && wire.Contains("c.undergroundParkingEntrance = null;"),
            "gates sources: a copy's doors are settled before it wakes: the doors of a warehouse's bays kept whole and in the controller's list, the others taken off, a car park given its number or taken off");
        Check(measure.Contains("c.driveInEntrances") && measure.Contains("c.undergroundParkingEntrance") && measure.Contains("park.vehicleExitPoint") && measure.Contains("into.Gates = ")
              && measure.Contains("into.Parks = ") && MemberText(copy, "public static bool Describe(").Contains("MeasureEntrances(c, unturn, origin, yaw, into);"),
            "gates sources: every design is measured for its garage doors and its car park from the building itself, once, behind the loading screen");
        Check(frontage.Contains("Gates = spec.Type == GateRules.WarehouseType") && frontage.Contains("want.Takes(designs[i])") && frontage.Contains("return want.Takes(d) && ")
              && Occurs(frontage, "want.Alone()") >= 2 && frontage.Contains("return !Gates || (GateRules.Serves(d) && (Blocked == null || !Blocked.Contains(d)));")
              && frontage.Contains("want.Blocked = StopBlocked(designs, ") && frontage.Contains("MouthRules.TakesStop(d, ground, poleX, poleZ)") && frontage.Contains("alone.Blocked = null;"),
            "gates sources: a warehouse's lot asks for a look whose doors serve its street and keep clear of a bus stop in every pick (the look, the widest that fits), and a filler beside or behind it does not");
        string pick = MemberText(rules, "public static int Pick(IList<DesignEntry> designs, string size, string type, string named, float width, float depth, string id, bool whole)");
        Check(pick.Contains("string.Equals(type, GateRules.WarehouseType, StringComparison.Ordinal) && AnyServing(designs, size, width, depth)") && rules.Contains("(!serving || GateRules.Serves(d))")
              && MemberText(copies, "public static bool Fitting(").Contains("servingFits > 0 && servingFits < Fits.Count") && MemberText(copies, "public static bool Fitting(").Contains("!GateRules.Serves(designs[Fits[i]])"),
            "gates sources: the engine's own pick prefers such a look for a warehouse that names none, and the construction game is offered only such designs for a warehouse where one fits");

        string late = MemberText(ground, "private bool Late()"), one = MemberText(stand, "private static bool StandOne(StandWait w, out string why)");
        Check(late.IndexOf("GroundGame.OpenMouths(standing);", StringComparison.Ordinal) is int opened && opened > 0 && late.IndexOf("GroundGame.Populate(", StringComparison.Ordinal) > opened
              && one.Contains("GroundGame.OpenMouths(StandMouth)") && one.IndexOf("Standing.Add(made);", StringComparison.Ordinal) < one.IndexOf("GroundGame.OpenMouths(StandMouth)", StringComparison.Ordinal),
            "gates sources: the district opens the mouths of its warehouses' doors before it lays the parking, and a warehouse made in play opens its own the moment it stands");
        string open = MemberText(mouths, "public static int OpenMouths("), lay = MemberText(mouths, "private static int LayLane("), mouthsOf = MemberText(mouths, "private static void MouthsOf(");
        string populate = MemberText(art, "public static int Populate(bool people, out string why)");
        Check(open.Contains("PutAwayLamps()") && open.Contains("PutAwayLitter()") && open.Contains("StreetsGame.Open(GateMouths)") && open.Contains("Relay();")
              && mouthsOf.Contains("c.driveInEntrances") && mouthsOf.Contains("c.undergroundParkingEntrance") && mouthsOf.Contains("MouthRules.Gate(")
              && lay.Contains("MouthRules.Pieces(") && lay.Contains("ParkingGame.Copy(template, slot.At, slot.Turn, from, to, out refusal)") && populate.Contains("LayLane(slot, ref why, out int failed);")
              && parking.Contains("public static ParkingLaneGenerator Copy(ParkingLaneGenerator template, Vector3 at, Quaternion turn, float from, float to, out string why)")
              && streets.Contains("public static int Open(IList<GroundRect> mouths)"),
            "gates sources: a mouth is cleared of lamp posts, litter and furniture, a lane that crosses it is laid as the pieces it leaves (or laid again so, where the parking is already there)");
        Check(save.Contains("public static bool ParkOf(MadeBuilding made, out string street, out int number, out Vector3 exit, out float heading)") && save.Contains("park.Address.streetName")
              && save.Contains("set.Door(park, number,") && life.Contains("MadeGame.ParkOf(made, out string parkStreet,") && life.Contains("set.Door(parkStreet, park,"),
            "gates sources: a car park of a copy is named to the district save as a building of the mod's is, by the street the game's own entrance names");
        string census = MemberText(tests, "private static void DoorsCensus(string when)"), drive = MemberText(tests, "private IEnumerator DriveIn(");
        Check(facts.Contains("Entrances(made, ref f);") && facts.Contains("f.GatesWhole += ") && life.Contains("DoorsCensus(\"the load\");") && census.Contains("\"Uptown check (garage doors, \"")
              && drive.Contains("gate.TryToEnterWithCar(car)") && drive.Contains("manager.ExitFromBuilding(gate.doorID)") && drive.Contains("bool pass = why == null && asked && inside && leftAgain && slots == structure;")
              && drive.Contains("\"Uptown check (warehouse): "),
            "gates sources: the whole-building check reads the doors, the load writes its census line, and the test takes the player's car in by the game's own call and out by its own exit");
        Check(guide.Contains(".Test(\"reallife_uptown_test_warehouse\", LifeDirector.TestWarehouse);") && plan.Contains("{ \"reallife_uptown_test_warehouse\", "),
            "gates sources: the test is among Options > Tests and is listed among those the run leaves to the player, with why");
    }

    // ---------------------------------------------------------------- texts

    // The start of the fix line in each language, after its fix mark.
    private static readonly Dictionary<string, string> GateFixStarts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "en", "you could not drive a car or truck into the warehouses of Uptown and Cedar Heights" },
        { "pt", "não era possível entrar de carro ou caminhão nos armazéns de Uptown e Cedar Heights" },
        { "zh-cn", "上城和雪松高地的仓库无法开进汽车或卡车" },
        { "de", "In die Lagerhäuser von Uptown und Cedar Heights konnte man nicht mit einem Auto oder Lastwagen hineinfahren" },
        { "fr", "on ne pouvait pas entrer en voiture ou en camion dans les entrepôts d'Uptown et de Cedar Heights" },
        { "es", "no se podía entrar en coche ni en camión en los almacenes de Uptown y Cedar Heights" },
        { "ru", "в склады Uptown и Cedar Heights нельзя было заехать на машине или грузовике" },
        { "nl", "je kon met een auto of vrachtwagen niet in de magazijnen van Uptown en Cedar Heights rijden" },
        { "tr", "Uptown ve Cedar Heights'taki depolara arabayla veya kamyonla girilemiyordu" },
        { "pl", "do magazynów w Uptown i Cedar Heights nie dało się wjechać samochodem ani ciężarówką" },
        { "ko", "Uptown과 Cedar Heights의 창고에는 차나 트럭으로 들어갈 수 없었습니다" }
    };

    private static void GateTexts()
    {
        string[] keys =
        {
            "reallife_uptown_test_warehouse", "reallife_uptown_test_warehouse_done", "reallife_uptown_test_warehouse_failed", "reallife_uptown_test_warehouse_car",
            "reallife_uptown_test_warehouse_none", "reallife_uptown_test_warehouse_rent"
        };
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("gates texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < ModLanguages.Required; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
                else if (key != keys[0] && value.Length > 240)
                {
                    missing = missing ?? key + " (over 240 characters)";
                }
            }
            Check(missing == null, "gates texts: the test's six texts are in " + codes[l] + ".json, in the style, with the English names, a notice at most 240 characters (" + (missing ?? "all") + ")");
        }
        string notice = words[0]["reallife_uptown_test_warehouse_done"] + words[0]["reallife_uptown_test_warehouse_rent"];
        Check(notice.Contains("{address}") && notice.Contains("{slots}") && notice.Contains("{used}") && words[0]["reallife_uptown_test_warehouse_failed"].Contains("{address}"),
            "gates texts: the notices carry the address, the vehicle slots and the ones used, which the test fills in");

        // What's new: the fix line once in the next row (or the row a release made of it) of each language, after its fix mark, no double space, never repeated.
        var all = new List<KeyValuePair<string, Dictionary<string, string>>>();
        for (int l = 0; l < words.Length; l++)
        {
            all.Add(new KeyValuePair<string, Dictionary<string, string>>(codes[l], words[l]));
        }
        foreach (string later in new[] { "pl", "ko" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), later + ".json"));
            Check(own != null, "gates texts: " + later + ".json is where the checks look for it");
            if (own != null)
            {
                all.Add(new KeyValuePair<string, Dictionary<string, string>>(later, own));
            }
        }
        foreach (KeyValuePair<string, Dictionary<string, string>> language in all)
        {
            string mark = FixMarks[language.Key], line = mark + GateFixStarts[language.Key];
            string row = FixRow(language.Value, line);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool repeated = false;
            string[] parts = row.Split(new[] { mark }, StringSplitOptions.None);
            for (int p = 1; p < parts.Length; p++)
            {
                repeated |= !seen.Add(parts[p].Trim());
            }
            Check(WhatsNewHas(language.Value, line) && HunterCount(row, line) == 1 && !row.Contains("  ") && !repeated && !row.Contains(NothingYet[language.Key]),
                "gates texts: What's new has the fix of the warehouses' garage doors in " + language.Key + ", once, after its mark, no double space, no line repeated");
        }

        string newBuildings = DocText(@"features\new-buildings.md"), uptown = DocText(@"features\uptown.md");
        Check(uptown.Contains("### Garage doors") && uptown.Contains("Uptown: drive into the nearest warehouse and out") && newBuildings.Contains("## Vehicle entrances: garage doors and car parks")
              && !newBuildings.Contains("taken off a working copy") && !newBuildings.Contains("vehicle entrances disabled") && !uptown.Contains("vehicle entrances disabled"),
            "gates texts: the docs say that a warehouse's garage doors work, and no longer that a working copy has them taken off");
    }

    // A document of the docs folder as text.
    private static string DocText(string relative)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "..", "..", "..", "docs", relative));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
