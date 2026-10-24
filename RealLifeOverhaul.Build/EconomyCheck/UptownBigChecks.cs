using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Uptown's big special places (the owner, 24 Oct 2026: a wholesale store and a gas station before the big release), on
// the real plan of the band's lots: the two places and the lots they hold out of the plan's ordinary buildings, the ground
// each stands on, where on it each of the game's own buildings stands (their measures Build 3682's, not-in-repo
// filllab/facts/specials_scan.py and specials_parts.py), the copy engine's whole designs (only ever named, never picked by
// size, stood by their own front), the mouths kept clear of parking and litter and the avenue lanes they take, the same in
// any order of the plan; the sources (the catalogue lets a whole design in, its workings kept and rebound before it wakes,
// its walls alone carved, the load deciding a place before the plan's lots and never on someone's unit, the street art
// keeping the mouths, the tests and their lines, no frame of its own, nothing to chance); and the texts in all nine.
internal static partial class Program
{
    private static void UptownBigChecks()
    {
        List<BuildingsLot> plan = UptownRealPlan();
        UptownBigLots(plan);
        UptownBigPlacing(plan);
        UptownBigDesigns();
        UptownBigMouths(plan);
        UptownBigSources();
        UptownBigTexts();
    }

    // Total Produce Trading's drive-in and the game's filling station as the copy engine measures them round their roots.
    private static DesignEntry UptownStoreDesign() => new DesignEntry
    {
        Name = "DriveInMurrayHill", Size = "ba:buildingsize_e", Version = 1, Type = BuildingsRules.SpecialType, Uses = 1, MinX = -16.66f, MaxX = 17.01f, MinZ = -12.12f, MaxZ = 17.45f,
        Height = 13.65f, DoorX = 14.8f, DoorZ = 13.03f, DoorFacing = 0f, Whole = true, Front = -1f
    };

    private static DesignEntry UptownStationDesign() => new DesignEntry
    {
        Name = "GasStationBuilding", Size = "ba:buildingsize_a", Version = 2, Type = BuildingsRules.SpecialType, Uses = 4, MinX = -4.01f, MaxX = 10.93f, MinZ = -12.33f, MaxZ = 4.41f,
        Height = 6.78f, DoorX = 4.21f, DoorZ = -1.68f, DoorFacing = 90f, Whole = true, Front = 0f
    };

    private static BuildingsLot UptownLotOf(List<BuildingsLot> plan, string id)
    {
        return plan.Find(l => l.Id == id);
    }

    // ---------------------------------------------------------------- the places and their lots

    private static void UptownBigLots(List<BuildingsLot> plan)
    {
        BigPlace store = BigPlacesRules.Of(BigKind.Wholesale), station = BigPlacesRules.Of(BigKind.GasStation);
        Check(BigPlacesRules.All.Length == 2 && store != null && station != null && store.Lot == "uptown_place_wholesale" && station.Lot == "uptown_place_fuel"
              && store.Service == "MurrayHillWholesaleStore" && station.Service == "UnitedGasoline" && store.Name == "Northgate Wholesale" && station.Name == "Summit Fuel"
              && store.Design == "DriveInMurrayHill" && station.Design == "GasStationBuilding" && store.Version == 1 && station.Version == 2,
            "uptown big: two places, Total Produce Trading's drive-in store on block 108's Uptown Avenue front and United Gasoline's station on the corner of 9th Street and Uptown Avenue, each with a name of its own");
        var held = new List<string>();
        bool known = true, oneBlock = true, sizes = true;
        foreach (BigPlace p in BigPlacesRules.All)
        {
            BuildingsLot own = UptownLotOf(plan, p.Lot);
            known &= own.Id == p.Lot;
            held.Add(p.Lot);
            foreach (string id in p.Over)
            {
                BuildingsLot o = UptownLotOf(plan, id);
                known &= o.Id == id;
                oneBlock &= o.Block == own.Block;
                held.Add(id);
            }
            foreach (string id in p.KeptClear)
            {
                BuildingsLot o = UptownLotOf(plan, id);
                known &= o.Id == id;
                oneBlock &= o.Block == own.Block;
                held.Add(id);
            }
            sizes &= own.Class == (p.Kind == BigKind.Wholesale ? BuildingsClass.Warehouse : BuildingsClass.Office);
        }
        Check(known && oneBlock && held.Count == 2 && new HashSet<string>(held).Count == 2 && sizes && BigPlacesRules.All[0].Over.Length + BigPlacesRules.All[0].KeptClear.Length
              + BigPlacesRules.All[1].Over.Length + BigPlacesRules.All[1].KeptClear.Length == 0 && UptownLots.Places.Count == 2,
            "uptown big: each place stands on the ground the plan keeps for it (UptownLots.Places), in the plan, held once and no lot besides; the store's of the warehouse class, the station's an office's");
        bool clear = true, holds = true;
        foreach (string id in held)
        {
            clear &= !BuildingsRules.Clear(id);
            holds &= BigPlacesRules.Holds(id) && BigPlacesRules.HolderOf(id) != null;
        }
        bool neighbours = plan.TrueForAll(l => held.Contains(l.Id) || l.Id == "uptown_1_14_004" || (BuildingsRules.Clear(l.Id) && !BigPlacesRules.Holds(l.Id)))
                          && !BigPlacesRules.Holds(null) && BigPlacesRules.On("uptown_1_08_001") == null
                          && BigPlacesRules.On("uptown_place_wholesale") == store && BigPlacesRules.On("uptown_place_fuel") == station;
        Check(clear && holds && neighbours, "uptown big: a place's lots are none of the plan's ordinary buildings (BuildingsRules.Clear), its neighbours are");
        var built = UptownIds(plan, BuildingsRules.Built(plan, LandmarkRules.OwnBlock, 0f, 1f, new List<int>()));
        var leased = UptownIds(plan, BuildingsRules.Leased(plan, new List<int>()));
        var legacy = UptownIds(plan, BuildingsRules.DayOne(plan, 0f, 1f, new List<int>()));
        bool apart = true;
        foreach (string id in held)
        {
            apart &= !built.Contains(id) && !leased.Contains(id) && !legacy.Contains(id);
        }
        bool landmarks = true;
        foreach (BigPlace p in BigPlacesRules.All)
        {
            landmarks &= !LandmarkRules.OwnBlock(UptownLotOf(plan, p.Lot).Block);
        }
        Check(apart && landmarks, "uptown big: no place takes a lot built from the first day before the band was filled, a leased office's or a landmark's block");
    }

    // ---------------------------------------------------------------- where each stands

    private static void UptownBigPlacing(List<BuildingsLot> plan)
    {
        BigPlace store = BigPlacesRules.Of(BigKind.Wholesale), station = BigPlacesRules.Of(BigKind.GasStation);
        bool g1 = BigPlacesRules.Ground(store, plan, out BuildingsLot storeGround), g2 = BigPlacesRules.Ground(station, plan, out BuildingsLot stationGround);
        Check(g1 && g2 && storeGround.Id == store.Lot && storeGround.Street == UptownLotOf(plan, store.Lot).Street && Near(storeGround.X, -83.4f, 0.05f) && Near(storeGround.Z, 577.63f, 0.05f)
              && storeGround.Width == 60 && storeGround.Depth == 41 && Near(storeGround.Facing, 0f, 0.01f)
              && stationGround.Width == 23 && stationGround.Depth == 32 && Near(stationGround.Facing, 180f, 0.01f) && Near(stationGround.X, -280.0f, 0.05f),
            "uptown big: the store's ground is the plan's 60 by 41 m along Uptown Avenue at its own address; the station's 23 by 32 m on its corner ("
            + storeGround.X + ", " + storeGround.Z + ", " + storeGround.Width + " by " + storeGround.Depth + ")");
        var shuffled = new List<BuildingsLot>(plan);
        shuffled.Reverse();
        bool same = BigPlacesRules.Ground(store, shuffled, out BuildingsLot again) && Near(again.X, storeGround.X, 0.001f) && again.Width == storeGround.Width && again.Depth == storeGround.Depth;
        Check(same && !BigPlacesRules.Ground(store, new List<BuildingsLot>(), out _) && !BigPlacesRules.Ground(null, plan, out _),
            "uptown big: the same ground in any order of the plan, none without its lots");

        // The store: its front 10.5 m back from Uptown Avenue's pavement, its west side against the ground's west end; its car park's lines stop at the pavements.
        DesignEntry d = UptownStoreDesign();
        MadeLot lot = new MadeLot(storeGround.X, storeGround.Z, storeGround.Facing, storeGround.Width, storeGround.Depth) { Inset = store.Inset, Side = store.Side, SideGap = store.SideGap };
        NewBuildingsRules.Place(d, lot, out float x, out float z, out float yaw);
        float pavement = (float)(LayoutRules.AvenueZ(1) - LayoutRules.AvenueHalf), west7th = LayoutRules.StreetX(2) + LayoutRules.StreetHalf, east6th = LayoutRules.StreetX(3) - LayoutRules.StreetHalf;
        bool front = Near(z + d.MaxZ, pavement - 10.5f, 0.1f) && Near(yaw, 0f, 0.01f);
        bool westSide = Near(x + d.MinX, storeGround.X - 30f + 0.5f, 0.05f);
        bool lines = x - 22.01f >= west7th && z + 27.92f <= pavement + 0.05f;                     // the car park's lines, x -22.01 to 12.27 and to z 27.92 round its root
        bool yard = storeGround.X + 30f - (x + 20.29f) >= 20f && x + 20.29f < east6th;           // the garage doors' triggers reach x 20.29; 20 m or more of yard beyond them
        NewBuildingsRules.Footprint(d, lot, out float cx, out float cz, out float hw, out float hd);
        bool foot = Near(cx, x + (d.MinX + d.MaxX) * 0.5f, 0.05f) && Near(cz, z + (d.MinZ + d.MaxZ) * 0.5f, 0.05f) && Near(hw * 2f, d.MaxX - d.MinX, 0.01f);
        Check(front && westSide && lines && yard && foot,
            "uptown big: the store stands as Total Produce Trading stands to its street, its front 10.5 m back from Uptown Avenue's pavement so its car park's lines end there, "
            + "against the ground's west end, its garage doors opening east onto a yard 20 m wide or more (root " + x.ToString("F2") + ", " + z.ToString("F2") + ")");
        bool storeClear = UptownBigClear(plan, store, cx, cz, lot.Facing, hw, hd, out string storeMeets);
        Check(storeClear, "uptown big: the store's walls stand 1.5 m clear of every lot of the plan it does not hold" + (storeMeets != null ? " (meets " + storeMeets + ")" : string.Empty));

        // The station: the pumps' open end 1.87 m behind Uptown Avenue's north pavement, the forecourt and repair bays 5.11 m from 9th Street's east pavement.
        DesignEntry s = UptownStationDesign();
        MadeLot slot = new MadeLot(stationGround.X, stationGround.Z, stationGround.Facing, stationGround.Width, stationGround.Depth) { Inset = station.Inset, Side = station.Side, SideGap = station.SideGap };
        NewBuildingsRules.Place(s, slot, out float sx, out float sz, out float syaw);
        float north = (float)(LayoutRules.AvenueZ(1) + LayoutRules.AvenueHalf), ninth = LayoutRules.StreetX(0) + LayoutRules.StreetHalf;
        bool turned = Near(syaw, 180f, 0.01f);
        bool pumps = Near(sz - s.MaxZ, north + 1.87f, 0.05f);                                     // turned 180: its +Z faces south
        bool forecourt = Near(sx - s.MaxX, ninth + 5.11f, 0.05f);                                 // and its +X west
        bool bay = sx - 12.54f - 1f >= ninth;                                                      // the outer pump's trigger reaches local x 12.54; a car a metre more
        NewBuildingsRules.Footprint(s, slot, out float scx, out float scz, out float shw, out float shd);
        NewBuildingsRules.DoorAt(s, sx, sz, syaw, out float doorX, out float doorZ);
        bool door = Near(NewBuildingsRules.Turn(syaw + s.DoorFacing), 270f, 0.01f) && doorX < sx && Near(scx, sx - (s.MinX + s.MaxX) * 0.5f, 0.05f);
        Check(turned && pumps && forecourt && bay && door,
            "uptown big: the station stands on its corner as United Gasoline stands on 2nd Street's and 1st Avenue's, the pumps' open end 1.87 m behind the avenue's pavement, "
            + "its forecourt and repair bays 5.11 m from 9th Street's, its shop's door facing the forecourt (root " + sx.ToString("F2") + ", " + sz.ToString("F2") + ", turned " + syaw.ToString("F0") + ")");
        bool stationClear = UptownBigClear(plan, station, scx, scz, slot.Facing, shw, shd, out string stationMeets);
        Check(stationClear, "uptown big: the station's walls stand 1.5 m clear of every lot of the plan it does not hold" + (stationMeets != null ? " (meets " + stationMeets + ")" : string.Empty));

        // An ordinary design stands as it always did: its door's side to the street, its front on the lot's front line, in the middle.
        var plain = new DesignEntry { Name = "x", Size = "ba:buildingsize_c", MinX = -5f, MaxX = 6f, MinZ = -8f, MaxZ = 9f, DoorX = 1f, DoorZ = 9f, DoorFacing = 90f };
        var plainLot = new MadeLot(10f, 20f, 270f, 24f, 30f);
        NewBuildingsRules.Place(plain, plainLot, out float px, out float pz, out float pyaw);
        NewBuildingsRules.DoorFrame(plain, out float u0, out float u1, out _, out float v1);
        float f = 270f * (float)Math.PI / 180f, toFront = 15f - v1, across = (u0 + u1) * 0.5f;
        bool unchanged = Near(pyaw, 180f, 0.01f) && Near(px, 10f + toFront * (float)Math.Sin(f) - across * (float)Math.Cos(f), 0.001f)
                         && Near(pz, 20f + toFront * (float)Math.Cos(f) + across * (float)Math.Sin(f), 0.001f) && NewBuildingsRules.FrontOf(plain) == 90f;
        Check(unchanged, "new buildings: an ordinary design is still stood by its door, its front on the lot's front line, in the middle of the lot");
    }

    // True when the turned footprint grown by the made buildings' gap meets no lot of the plan but the place's own.
    private static bool UptownBigClear(List<BuildingsLot> plan, BigPlace place, float cx, float cz, float facing, float hw, float hd, out string meets)
    {
        meets = null;
        foreach (BuildingsLot l in plan)
        {
            if (BigPlacesRules.HolderOf(l.Id) == place)
            {
                continue;
            }
            bool avenue = BuildingsRules.FrontsAvenue(l.Facing);
            float halfX = (avenue ? l.Width : l.Depth) * 0.5f, halfZ = (avenue ? l.Depth : l.Width) * 0.5f;
            if (NewBuildingsRules.Overlaps(l.X - halfX, l.Z - halfZ, l.X + halfX, l.Z + halfZ, cx, cz, facing, hw, hd, NewBuildingsRules.Gap))
            {
                meets = l.Id;
                return false;
            }
        }
        return true;
    }

    // ---------------------------------------------------------------- the copy engine's whole designs

    private static void UptownBigDesigns()
    {
        Check(NewBuildingsRules.Whole.Length == 2 && NewBuildingsRules.WholeOf("DriveInMurrayHill", "ba:buildingsize_e") == 0 && NewBuildingsRules.WholeOf("GasStationBuilding", "ba:buildingsize_a") == 1
              && NewBuildingsRules.WholeOf("GasStationBuilding", "ba:buildingsize_b") < 0 && NewBuildingsRules.WholeOf(null, null) < 0
              && NewBuildingsRules.WholeOf("WholeSaleBuildingMidtown", "ba:buildingsize_e") < 0 && NewBuildingsRules.WholeOf("GasStationBuildingTrucks", "ba:buildingsize_a") < 0,
            "new buildings: the game's buildings copied whole are the drive-in of Total Produce Trading and the filling station, by name and size; Stockco's and Metro's are drawn in a static batch, the trucks' station is the trucks'");
        var plain = new DesignEntry { Name = "SM_Plain", Size = "ba:buildingsize_a", Type = "ba:buildingtype_retail", MinX = -5f, MaxX = 5f, MinZ = -5f, MaxZ = 5f, DoorX = 0f, DoorZ = 5f, DoorFacing = 0f };
        var station = UptownStationDesign();
        var designs = new List<DesignEntry> { station, plain };
        designs.Sort(NewBuildingsRules.CompareDesigns);
        int byName = NewBuildingsRules.Pick(designs, "ba:buildingsize_a", BuildingsRules.SpecialType, "GasStationBuilding", 23f, 32f, "uptown_place_fuel", true);
        int notWhole = NewBuildingsRules.Pick(designs, "ba:buildingsize_a", "ba:buildingtype_retail", "GasStationBuilding", 23f, 32f, "x", false);
        bool neverBySize = true;
        for (int i = 0; i < 64; i++)
        {
            int any = NewBuildingsRules.Pick(designs, "ba:buildingsize_a", "ba:buildingtype_retail", null, 23f, 32f, "lot " + i);
            neverBySize &= any >= 0 && designs[any] == plain;
        }
        int oldPick = NewBuildingsRules.Pick(designs, "ba:buildingsize_a", "ba:buildingtype_retail", "GasStationBuilding", 23f, 32f, "x");
        bool forPlots = designs[NewBuildingsRules.PickFor(designs, 30f, 30f, null, "plot")] == plain;
        Check(byName >= 0 && designs[byName] == station && notWhole < 0 && oldPick < 0 && neverBySize && forPlots
              && NewBuildingsRules.Pick(designs, "ba:buildingsize_a", BuildingsRules.SpecialType, null, 23f, 32f, "x", true) < 0,
            "new buildings: a whole design is only ever named by a special place: never picked by size, for a plot's look or under a plain declaration's name");
        Check(NewBuildingsRules.FrontOf(station) == 0f && NewBuildingsRules.FrontOf(UptownStoreDesign()) == 0f && NewBuildingsRules.Fits(station, 15f, 17f) && !NewBuildingsRules.Fits(station, 17f, 16f),
            "new buildings: a whole design is stood by its own front (the station's pumps' end), and fits ground by that front (14.94 m across, 16.74 m deep for the station)");
        var noDesign = new MadeSpec { Id = "x", Street = MadeStreets.Prefix + "provinglane", Number = 1, Whole = true, Design = "SM_Plain" };
        string refused = NewBuildingsRules.Refusal(noDesign, (a, b, c) => true, a => true, true);
        Check(refused != null && refused.Contains("copied whole"), "new buildings: a declaration copied whole that names none of the game's whole buildings is refused (" + refused + ")");
    }

    // ---------------------------------------------------------------- the mouths

    private static void UptownBigMouths(List<BuildingsLot> plan)
    {
        var mouths = new List<GroundRect>();
        BigPlace store = BigPlacesRules.Of(BigKind.Wholesale), station = BigPlacesRules.Of(BigKind.GasStation);
        BigPlacesRules.Ground(store, plan, out BuildingsLot sg);
        BigPlacesRules.Mouths(store, sg, mouths);
        int storeMouths = mouths.Count;
        BigPlacesRules.Ground(station, plan, out BuildingsLot tg);
        BigPlacesRules.Mouths(station, tg, mouths);
        while (mouths.Count < 3)
        {
            mouths.Add(default);   // a mouth short: the checks below fail on its place, never on its index
        }
        GroundRect a = mouths[0];
        float avenue = (float)LayoutRules.AvenueZ(1);
        bool storeMouth = storeMouths == 1 && Near(a.X1, -113.42f, 0.05f) && Near(a.X2, -53.42f, 0.05f) && Near(a.Z1, avenue - LayoutRules.AvenueHalf, 0.05f)
                          && Near(a.Z2, avenue - LayoutRules.AvenueKerb + BigPlacesRules.MouthIntoRoad, 0.05f);
        GroundRect b = mouths[1], c = mouths[2];
        bool stationMouths = storeMouths == 1 && mouths.Count == 3 && Near(b.X1, -291.52f, 0.05f) && Near(b.X2, -268.52f, 0.05f) && Near(b.Z1, avenue + LayoutRules.AvenueKerb - BigPlacesRules.MouthIntoRoad, 0.05f)
                             && Near(b.Z2, avenue + LayoutRules.AvenueHalf, 0.05f) && Near(c.X1, LayoutRules.StreetX(0) + LayoutRules.StreetKerbEast - BigPlacesRules.MouthIntoRoad, 0.05f)
                             && Near(c.X2, -291.52f, 0.05f) && Near(c.Z1, avenue + LayoutRules.AvenueHalf, 0.05f) && Near(c.Z2, avenue + LayoutRules.AvenueHalf + 32f, 0.05f);
        Check(storeMouth && stationMouths, "uptown big: the store's mouth runs along its ground's whole front over Uptown Avenue's south pavement past the kerb; the station's over the avenue's north pavement and 9th Street's east one");

        // The avenues' parking lanes as the band lays them, two a length (the game's avenue module's, 62.1 by 2.5 m, 8.65 m either side of the middle).
        int lanes = 0, kept = 0;
        string which = null;
        for (int row = 1; row <= LayoutRules.Rows; row++)
        {
            for (int k = -5; k < 13; k++)
            {
                float mid = LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, z0 = (float)LayoutRules.AvenueZ(row);
                for (int side = -1; side <= 1; side += 2)
                {
                    lanes++;
                    float lz = z0 + side * 8.65f;
                    if (BigPlacesRules.Meets(mouths, mid - 31.05f, lz - 1.25f, mid + 31.05f, lz + 1.25f))
                    {
                        kept++;
                        which = (which != null ? which + ", " : string.Empty) + "row " + row + " x " + mid.ToString("F1") + (side < 0 ? " south" : " north");
                    }
                }
            }
        }
        Check(lanes == 108 && kept == 2, "uptown big: the mouths keep 2 of the avenues' 108 parking lanes off, the one along each place's front (" + which + ")");
        // The game's litter where the band's copied modules lay it (Build 3682): 9th Street's crossing's north-east bags, 9th Street's bags by the forecourt, 6th Street's crossing's far bags.
        bool litter = BigPlacesRules.In(mouths, -293.27f, 626.2f) && BigPlacesRules.In(mouths, -294.27f, 642.5f) && BigPlacesRules.In(mouths, -57.6f, 598.7f)
                      && !BigPlacesRules.In(mouths, -48.07f, 597.98f) && !BigPlacesRules.In(mouths, -117.77f, 598.38f);
        bool lights = !BigPlacesRules.In(mouths, LayoutRules.StreetX(0) + 9.3f, avenue + 10.7f) && !BigPlacesRules.In(mouths, LayoutRules.StreetX(3) - 9.0f, avenue - 10.9f)
                      && !BigPlacesRules.In(mouths, LayoutRules.StreetX(2) + 6.1f, avenue - 13.9f);
        Check(litter && lights, "uptown big: the bags of litter in a mouth are put away and the crossings' light poles stand clear of every mouth");
    }

    // ---------------------------------------------------------------- the sources

    private static void UptownBigSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string copy = Read("NewBuildings/CopyGame.cs"), newDirector = Read("NewBuildings/NewBuildingsDirector.cs"), buildings = Read("Uptown/LifeGame.Buildings.cs");
        string art = Read("Uptown/GroundGame.Art.cs"), director = Read("Uptown/LifeDirector.cs"), big = Read("Uptown/LifeDirector.BigPlaces.cs"), tests = Read("Uptown/LifeDirector.BigTests.cs");
        string game = Read("Uptown/LifeGame.BigPlaces.cs"), guide = Read("Uptown/LifeGuide.cs"), rules = Read("Uptown/BigPlacesRules.cs"), contract = Read("Core/GameContract.cs");

        string measure = MemberText(copy, "private static void Measure(CityBuildingController c)");
        int whole = measure.IndexOf("int whole = NewBuildingsRules.WholeOf(name, b.BuildingSize);", StringComparison.Ordinal);
        Check(whole > 0 && whole < measure.IndexOf("b.BuildingType == SpecialType", StringComparison.Ordinal) && measure.Contains("OfDlc(c)") && measure.Contains("Whole = whole >= 0"),
            "uptown big: the copy engine's catalogue lets a whole design in before it leaves out the special type, a gas station and a taxi stand, never a DLC's");
        string strip = MemberText(copy, "private static void StripWhole(CityBuildingController c)");
        string making = MemberText(copy, "public static CityBuildingController MakeWorking(");
        Check(strip.Length > 0 && !strip.Contains("DriveInEntrance") && strip.Contains("UndergroundParkingEntrance") && strip.Contains("designatedDeliveryVanSpot = false")
              && making.IndexOf("Rebind(copy, source.building, building);", StringComparison.Ordinal) > 0
              && making.IndexOf("Rebind(copy, source.building, building);", StringComparison.Ordinal) < making.IndexOf("copy.gameObject.SetActive(true);", StringComparison.Ordinal),
            "uptown big: a whole copy keeps its garage doors, pumps and triggers, loses only what the game holds once in the city, and its parking lanes name its own record before it wakes");
        Check(MemberText(newDirector, "private bool Make(MadeSpec spec)").Contains("each.Width, each.Depth, spec.Id, spec.Whole)")
              && newDirector.Contains("DoorFacing = NewBuildingsRules.Turn(yaw + chosen.DoorFacing)"),
            "uptown big: the made buildings' engine names a whole design as one, and keeps the way its door truly faces");
        string cut = MemberText(buildings, "public static bool Cut(MadeBuilding made)"), walls = MemberText(buildings, "private static bool CutWalls(Transform root)");
        Check(cut.Contains("if (d.Whole)") && walls.Contains("wall.isTrigger") && walls.Contains("local.size.y < WallLeast") && walls.Contains("carveOnlyStationary = true"),
            "uptown big: a whole copy carves its walls alone, its forecourt, yard and car park left to walk on");
        string someones = MemberText(buildings, "public static bool Someones(BuildingsLot lot)");
        Check(someones.Contains("reg.RentedByPlayer") && someones.Contains("reg.BuildingOwnedByPlayer") && someones.Contains("GoingPublic.Foreign(reg)"),
            "uptown big: a lot the save holds someone's unit on is the player's, rented or owned, or another player's in a session");
        string declare = MemberText(director, "private static void Declare()");
        Check(declare.IndexOf("DecideBig();", StringComparison.Ordinal) >= 0 && declare.IndexOf("DecideBig();", StringComparison.Ordinal) < declare.IndexOf("BuildingsRules.Built(", StringComparison.Ordinal)
              && declare.Contains("int big = DeclareBig();") && big.Contains("LifeGame.Someones(lot)") && big.Contains("BigDown.Add(id)") && big.Contains("LifeGame.BigSpec(place, ground,"),
            "uptown big: the load decides each place before the plan's lots, and a place on someone's unit stands down with its lots the plan's ordinary ones");
        string copyArt = MemberText(art, "public static int CopyArt(RoadBuild build)"), populate = MemberText(art, "public static int Populate(bool people, out string why)");
        Check(copyArt.IndexOf("FindMouths();", StringComparison.Ordinal) > 0 && copyArt.IndexOf("FindMouths();", StringComparison.Ordinal) < copyArt.IndexOf("LayoutPlans.Modules(Places);", StringComparison.Ordinal)
              && copyArt.IndexOf("ClearMouths(copy);", StringComparison.Ordinal) > copyArt.IndexOf("Strip(copy);", StringComparison.Ordinal)
              && populate.IndexOf("if (InMouth(lanes[l], at, turn))", StringComparison.Ordinal) > 0
              && populate.IndexOf("if (InMouth(lanes[l], at, turn))", StringComparison.Ordinal) < populate.IndexOf("ParkingGame.Room(", StringComparison.Ordinal),
            "uptown big: the street art keeps the mouths clear, the litter put away before a copy wakes and no parking lane laid there");
        Check(guide.Contains(".Check(\"reallife_uptown_test_store\", LifeDirector.TestStore, \"Uptown check (wholesale store)\", RunPlace.Outdoors)")
              && guide.Contains(".Test(\"reallife_uptown_test_store_order\", LifeDirector.TestOrder)")
              && guide.Contains(".Check(\"reallife_uptown_test_station\", LifeDirector.TestStation, \"Uptown check (gas station)\", RunPlace.Outdoors)")
              && guide.Contains(".Test(\"reallife_uptown_test_station_fill\", LifeDirector.TestFill)")
              && tests.Contains("\"Uptown check (\" + what + \"): \"") && tests.Contains("TakeMe(BigKind.Wholesale, \"wholesale store\")") && tests.Contains("TakeMe(BigKind.GasStation, \"gas station\")")
              && tests.Contains("\"Uptown check (wholesale delivery): ") && tests.Contains("\"Uptown check (fill-up): ") && tests.Split("(pass ? \"PASS\" : \"FAIL\")").Length >= 4,
            "uptown big: the four tests are buttons, the two that take the player there in the test run, each writing its PASS or FAIL line");
        Check(game.Contains("GameContract.WholesaleDeliver.Method") && game.Contains("GameContract.StationRefuel.Method") && game.Contains("GameContract.StationTrigger.Field")
              && contract.Contains("Optional(StaticMethod(UptownBigTests, Game, \"Helpers.BusinessHelper\", \"DeliverWholesaleContract\"")
              && contract.Contains("Optional(Method(UptownBigTests, Game, \"UI.Overlays.GasStationOverlay\", \"Refuel\""),
            "uptown big: the tests reach the game's own delivery and fill-up through optional lines of the contract");
        bool calm = true;
        foreach (string text in new[] { rules, big, tests, game })
        {
            calm &= !text.Contains("void Update(") && !text.Contains("Random") && !text.Contains("using System.Linq") && !text.Contains("Harmony");
        }
        Check(calm && !rules.Contains("UnityEngine"), "uptown big: no frame of their own, nothing to chance, no LINQ or Harmony, the rules game-free");
    }

    // ---------------------------------------------------------------- the texts

    private static void UptownBigTexts()
    {
        string folder = LocalesFolder();
        var langs = new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        bool all = true;
        string missing = null;
        foreach (string lang in langs)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, lang + ".json"));
            bool found = words != null && words.TryGetValue("reallife_changes_next", out string next) && next.Contains("Northgate Wholesale") && next.Contains("Summit Fuel");
            all &= found;
            missing = found ? missing : missing ?? lang;
        }
        Check(all, "uptown big: What's new names the wholesale store and the petrol station in all nine languages" + (missing != null ? " (not in " + missing + ")" : string.Empty));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json"));
        bool labels = en != null && en.TryGetValue(LifeText.TestStore, out string s) && s == "Uptown: take me to the wholesale store"
                      && en.TryGetValue(LifeText.TestOrder, out string o) && o == "Uptown: an order from the Uptown wholesale store delivered now"
                      && en.TryGetValue(LifeText.TestStation, out string t) && t == "Uptown: take me to the petrol station"
                      && en.TryGetValue(LifeText.TestFill, out string f) && f == "Uptown: fill up at the Uptown petrol station now";
        Check(labels, "uptown big: the four test buttons say what they do, as the owner named them (in British English, a petrol station)");
    }
}
