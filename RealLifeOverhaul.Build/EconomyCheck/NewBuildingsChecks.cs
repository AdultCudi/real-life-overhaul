using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul.NewBuildings;

// The mod's own buildings (Scripts/NewBuildings). The rules are called: a design's name, the building a design is measured
// on, the pick, where a design stands on its lot and whether ground is clear, the lot taken when the one asked for is not,
// what refuses a declaration, the frame cost's sum, the test plot. The game's side is read as text: nothing of a made
// building reaches the save's lists, the registration is in the game's lookup before the record and the claim comes after
// both, no made controller is destroyed in a session, every member of the game's read by name is in Core/GameContract,
// the save guard runs whatever is switched off, nothing is sent, and the texts are in the three languages.
internal static partial class Program
{
    private static void NewBuildingsChecks()
    {
        NewBuildingsNames();
        NewBuildingsPicks();
        NewBuildingsGeometry();
        NewBuildingsLots();
        NewBuildingsRefusals();
        NewBuildingsTestPlotChecks();
        NewBuildingsTiming();
        NewBuildingsSources();
        NewBuildingsTexts();
    }

    // ---------------------------------------------------------------- names, hashes, the building a design is measured on

    private static void NewBuildingsNames()
    {
        Check(NewBuildingsRules.DesignName("SM_Hell's KitchenBuilding11 (1)") == "SM_Hell's KitchenBuilding11" && NewBuildingsRules.DesignName("SM_Building (2) (14)") == "SM_Building",
            "new buildings: a design's name is its object's name with Unity's copy numbers taken off, however many");
        Check(NewBuildingsRules.DesignName("Office (Tall)") == "Office (Tall)" && NewBuildingsRules.DesignName("(3)") == "(3)" && NewBuildingsRules.DesignName("  SM_Plain  ") == "SM_Plain"
              && NewBuildingsRules.DesignName(null) == string.Empty && NewBuildingsRules.DesignName(string.Empty) == string.Empty,
            "new buildings: brackets that are not a copy number stay, a name that is only a number stays, and nothing is nothing");
        Check(NewBuildingsRules.Hash(string.Empty) == 2166136261u && NewBuildingsRules.Hash(null) == 2166136261u && NewBuildingsRules.Hash("a") == 0xe40c292cu && NewBuildingsRules.Hash("foobar") == 0xbf9cf968u,
            "new buildings: the pick's hash is FNV-1a (its published values), the same on every machine and every run");

        Check(NewBuildingsRules.MeasuredFrom(false, "ba:street_z", 99, true, "ba:street_a", 1) && !NewBuildingsRules.MeasuredFrom(true, "ba:street_a", 1, false, "ba:street_z", 99),
            "new buildings: a design is measured on a building of it not drawn in a static batch before one that is");
        Check(NewBuildingsRules.MeasuredFrom(false, "ba:street_a", 5, false, "ba:street_b", 1) && !NewBuildingsRules.MeasuredFrom(false, "ba:street_b", 1, false, "ba:street_a", 5)
              && NewBuildingsRules.MeasuredFrom(true, "s", 3, true, "s", 7) && !NewBuildingsRules.MeasuredFrom(true, "s", 7, true, "s", 3) && !NewBuildingsRules.MeasuredFrom(false, "s", 3, false, "s", 3),
            "new buildings: then on the one at the lower address, street first, then number; the same building is not taken again");
        // The scene lists its buildings in no fixed order: every order of the same five ends on the same one.
        var streets = new[] { "ba:street_c", "ba:street_a", "ba:street_b", "ba:street_a", "ba:street_d" };
        var numbers = new[] { 1, 9, 4, 2, 3 };
        var batched = new[] { false, true, false, false, true };
        int[] order = { 0, 1, 2, 3, 4 };
        string first = null;
        bool same = true;
        for (int turn = 0; turn < 120; turn++)
        {
            Shuffle(order, turn);
            int kept = order[0];
            for (int i = 1; i < order.Length; i++)
            {
                int k = order[i];
                if (NewBuildingsRules.MeasuredFrom(batched[k], streets[k], numbers[k], batched[kept], streets[kept], numbers[kept]))
                {
                    kept = k;
                }
            }
            string won = streets[kept] + numbers[kept];
            same &= first == null || won == first;
            first = first ?? won;
        }
        Check(same && first == "ba:street_a2", "new buildings: whatever order the scene lists five buildings of a design in, it is measured on the same one (" + first + ")");

        var list = new List<DesignEntry> { Entry("SM_B", "ba:buildingsize_c"), Entry("SM_A", "ba:buildingsize_k"), Entry("SM_A", "ba:buildingsize_c"), Entry("SM_a", "ba:buildingsize_c") };
        list.Sort(NewBuildingsRules.CompareDesigns);
        Check(list[0].Name == "SM_A" && list[0].Size == "ba:buildingsize_c" && list[1].Name == "SM_A" && list[1].Size == "ba:buildingsize_k" && list[2].Name == "SM_B" && list[3].Name == "SM_a",
            "new buildings: the catalogue is in one order everywhere: by name, then size, ordinal");
        var specs = new List<MadeSpec> { new MadeSpec { Id = "zoo" }, new MadeSpec { Id = "Abc" }, new MadeSpec { Id = "abc" } };
        specs.Sort(NewBuildingsRules.CompareSpecs);
        Check(specs[0].Id == "Abc" && specs[1].Id == "abc" && specs[2].Id == "zoo", "new buildings: the declarations are made in the order of their ids, ordinal, whatever order the features declared them in");

        Check(NewBuildingsRules.StructureName("ba:buildingsize_c", 1) == "C1" && NewBuildingsRules.StructureName("ba:buildingsize_k", 3) == "K3" && NewBuildingsRules.StructureName(null, 2) == "?2",
            "new buildings: a size and version are named as the game names its structures (C1)");
        Check(NewBuildingsRules.Turn(-90f) == 270f && NewBuildingsRules.Turn(450f) == 90f && NewBuildingsRules.Turn(360f) == 0f && NewBuildingsRules.Turn(-720f) == 0f && NewBuildingsRules.Turn(12.5f) == 12.5f,
            "new buildings: an angle is brought into 0 to 360 degrees");
        Check(NewBuildingsRules.Upright(1f) && NewBuildingsRules.Upright((float)Math.Cos(0.5 * Math.PI / 180.0)) && !NewBuildingsRules.Upright((float)Math.Cos(2.0 * Math.PI / 180.0)),
            "new buildings: a building leaning up to a degree is stood upright; one leaning more is never a design");
    }

    // A fixed reshuffle of the numbers (the same every run).
    private static void Shuffle(int[] order, int turn)
    {
        uint seed = 2166136261u ^ (uint)turn;
        for (int i = order.Length - 1; i > 0; i--)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            int j = (int)(seed % (uint)(i + 1));
            int t = order[i];
            order[i] = order[j];
            order[j] = t;
        }
    }

    // A design whose door is the middle of its front, the front facing its root's forward turned by doorFacing degrees (a
    // multiple of 90): width across the door, depth back from it; its root front from the door's front.
    private static DesignEntry Entry(string name, string size, string type = "ba:buildingtype_office", float width = 16f, float depth = 20f, float doorFacing = 0f, bool batched = false, float front = 5f)
    {
        double a = doorFacing * Math.PI / 180.0;
        float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
        var d = new DesignEntry { Name = name, Size = size, Version = 1, Type = type, Uses = 1, Batched = batched, DoorFacing = doorFacing, Height = 12f, DoorY = 0.3f };
        d.MinX = d.MinZ = float.MaxValue;
        d.MaxX = d.MaxZ = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            float u = (i & 1) == 0 ? -width * 0.5f : width * 0.5f;
            float v = (i & 2) == 0 ? front - depth : front;
            float x = u * c + v * s, z = -u * s + v * c;   // from the door's frame to the root's
            d.MinX = Math.Min(d.MinX, x);
            d.MaxX = Math.Max(d.MaxX, x);
            d.MinZ = Math.Min(d.MinZ, z);
            d.MaxZ = Math.Max(d.MaxZ, z);
        }
        d.DoorX = front * s;
        d.DoorZ = front * c;
        return d;
    }

    // ---------------------------------------------------------------- the pick

    private static void NewBuildingsPicks()
    {
        const string C = "ba:buildingsize_c", K = "ba:buildingsize_k", Office = "ba:buildingtype_office", Retail = "ba:buildingtype_retail";
        var designs = new List<DesignEntry>
        {
            Entry("A_House", C, Office),
            Entry("B_Shop", C, Retail),
            Entry("C_Batched", C, Office, batched: true),
            Entry("D_Big", C, Office, 40f, 40f),
            Entry("E_Office", C, Office, 16f, 20f, 90f),
            Entry("F_Other", K, Office),
        };
        designs.Sort(NewBuildingsRules.CompareDesigns);
        bool typed = true, spread0 = false, spread4 = false, steady = true;
        for (int i = 0; i < 40; i++)
        {
            string id = "feature_" + i;
            int pick = NewBuildingsRules.Pick(designs, C, Office, null, 30f, 34f, id);
            typed &= pick == 0 || pick == 4;
            spread0 |= pick == 0;
            spread4 |= pick == 4;
            steady &= NewBuildingsRules.Pick(designs, C, Office, null, 30f, 34f, id) == pick;
            int expect = NewBuildingsRules.Hash(id) % 2u == 0 ? 0 : 4;
            typed &= pick == expect;
        }
        Check(typed && spread0 && spread4, "new buildings: a building is given a design of its size and type that can be copied and fits its lot, picked by its id (never one in a static batch or too big)");
        Check(steady, "new buildings: the same id gets the same design every time");
        bool anyType = true;
        for (int i = 0; i < 30; i++)
        {
            int pick = NewBuildingsRules.Pick(designs, C, "ba:buildingtype_warehouse", null, 30f, 34f, "w" + i);
            anyType &= pick == 0 || pick == 1 || pick == 4;
        }
        Check(anyType, "new buildings: with no design of its type, one of any type of its size is picked");
        Check(NewBuildingsRules.Pick(designs, C, Office, "B_Shop", 30f, 34f, "x") == 1 && NewBuildingsRules.Pick(designs, C, Office, "F_Other", 30f, 34f, "x") == 5,
            "new buildings: a design asked for by name is the one given, whatever its size and type");
        Check(NewBuildingsRules.Pick(designs, C, Office, "C_Batched", 30f, 34f, "x") == -1 && NewBuildingsRules.Pick(designs, C, Office, "D_Big", 30f, 34f, "x") == -1
              && NewBuildingsRules.Pick(designs, C, Office, "Nope", 30f, 34f, "x") == -1,
            "new buildings: a design asked for by name is refused when it is in a static batch, does not fit or is unknown; nothing else is given in its place");
        Check(NewBuildingsRules.Pick(designs, "ba:buildingsize_z", Office, null, 30f, 34f, "x") == -1 && NewBuildingsRules.Pick(null, C, Office, null, 30f, 34f, "x") == -1
              && NewBuildingsRules.Pick(designs, C, Office, null, 10f, 10f, "x") == -1,
            "new buildings: no design of its size, no catalogue or a lot too small for any: no design, and the building stands down");

        // A plot's building, or a building's new look, when no design is named: one that fills the ground, picked by a key.
        var ground = new List<DesignEntry>
        {
            Entry("G_Big", C, Office, 20f, 18f),          // 360 m2: the best fit of 22 by 20
            Entry("G_Batched", C, Office, 21f, 19f, batched: true),
            Entry("G_Close", C, Retail, 19f, 18f),        // 342 m2: within the share of the best
            Entry("G_Small", K, Office, 10f, 10f),        // 100 m2: too small beside them
            Entry("G_Wide", C, Office, 30f, 10f),         // too wide for the ground
            Entry("G_Turned", C, Office, 18f, 19f, 90f),  // 342 m2, its door on its side
        };
        ground.Sort(NewBuildingsRules.CompareDesigns);
        int big = ground.FindIndex(e => e.Name == "G_Big"), close = ground.FindIndex(e => e.Name == "G_Close"), turned = ground.FindIndex(e => e.Name == "G_Turned");
        bool fills = true, spreadBig = false, spreadClose = false, spreadTurned = false, same = true;
        for (int i = 0; i < 60; i++)
        {
            string key = "plot:" + i + ":-40:090";
            int pick = NewBuildingsRules.PickFor(ground, 22f, 20f, null, key);
            fills &= pick == big || pick == close || pick == turned;
            spreadBig |= pick == big;
            spreadClose |= pick == close;
            spreadTurned |= pick == turned;
            same &= NewBuildingsRules.PickFor(ground, 22f, 20f, null, key) == pick;
        }
        Check(fills && spreadBig && spreadClose && spreadTurned && same,
            "new buildings: a plot is given a design that fills its ground (never one in a static batch, too big or far smaller than the best), the same one for the same key");
        bool notOwn = true;
        for (int i = 0; i < 30; i++)
        {
            int pick = NewBuildingsRules.PickFor(ground, 22f, 20f, "G_Big", "ba:street_fifthavenue#" + i);
            notOwn &= pick >= 0 && pick != big;
        }
        Check(notOwn, "new buildings: a building's new look is never the design it wears already");
        Check(NewBuildingsRules.PickFor(ground, 8f, 8f, null, "k") == -1 && NewBuildingsRules.PickFor(null, 22f, 20f, null, "k") == -1
              && NewBuildingsRules.PickFor(ground, 11f, 11f, null, "k") == ground.FindIndex(e => e.Name == "G_Small"),
            "new buildings: ground too small for any design gets none; ground only a small design fits gets that one");
    }

    // ---------------------------------------------------------------- where a design stands

    private static void NewBuildingsGeometry()
    {
        DesignEntry d = Entry("SM_Front", "ba:buildingsize_c");   // 16 across, 20 deep, its door the middle of its front, 5 m before its root
        Check(NewBuildingsRules.Fits(d, 16f, 20f) && !NewBuildingsRules.Fits(d, 15.9f, 20f) && !NewBuildingsRules.Fits(d, 16f, 19.9f),
            "new buildings: a design fits a lot as wide and as deep as it is, and not one a little smaller");
        DesignEntry side = Entry("SM_Side", "ba:buildingsize_c", width: 16f, depth: 20f, doorFacing: 90f);
        Check(NewBuildingsRules.Fits(side, 16.01f, 20.01f) && !NewBuildingsRules.Fits(side, 20f, 16f) && Near(side.MaxX - side.MinX, 20f) && Near(side.MaxZ - side.MinZ, 16f),
            "new buildings: a design is measured from its door: a door on its side makes its depth what runs across its root");
        var flat = new DesignEntry { MinX = 2f, MaxX = 2f, MinZ = -5f, MaxZ = 5f };
        Check(!NewBuildingsRules.Fits(flat, 100f, 100f), "new buildings: a design with no width never fits");

        var lot = new MadeLot(100f, 200f, 0f, 30f, 34f);
        NewBuildingsRules.Place(d, lot, out float x, out float z, out float yaw);
        NewBuildingsRules.DoorAt(d, x, z, yaw, out float doorX, out float doorZ);
        NewBuildingsRules.Footprint(d, lot, out float cx, out float cz, out float hw, out float hd);
        Check(Near(x, 100f) && Near(z, 212f) && Near(yaw, 0f) && Near(doorX, 100f) && Near(doorZ, 217f),
            "new buildings: on a lot facing north the design's door is the middle of the lot's front (" + doorX + ", " + doorZ + ")");
        Check(Near(cx, 100f) && Near(cz, 207f) && Near(hw, 8f) && Near(hd, 10f), "new buildings: its footprint is its own size, its front on the lot's front");

        lot = new MadeLot(100f, 200f, 90f, 30f, 34f);
        NewBuildingsRules.Place(d, lot, out x, out z, out yaw);
        NewBuildingsRules.DoorAt(d, x, z, yaw, out doorX, out doorZ);
        Check(Near(x, 112f) && Near(z, 200f) && Near(yaw, 90f) && Near(doorX, 117f) && Near(doorZ, 200f),
            "new buildings: on a lot facing east it is turned as the game turns things (90 degrees: forward is east), its door the middle of the front");

        var unit = new DesignEntry { DoorX = 0f, DoorZ = 1f };
        NewBuildingsRules.DoorAt(unit, 0f, 0f, 90f, out float fx, out float fz);
        var right = new DesignEntry { DoorX = 1f, DoorZ = 0f };
        NewBuildingsRules.DoorAt(right, 0f, 0f, 90f, out float rx, out float rz);
        Check(Near(fx, 1f) && Near(fz, 0f) && Near(rx, 0f) && Near(rz, -1f), "new buildings: a turn of 90 degrees takes forward to east and right to south, as Unity's does");

        // Off-centre: the footprint, not the root, is in the middle of the lot.
        var offset = new DesignEntry { MinX = -4f, MaxX = 12f, MinZ = -15f, MaxZ = 5f, DoorX = 0f, DoorZ = 5f, DoorFacing = 0f };
        lot = new MadeLot(100f, 200f, 0f, 30f, 34f);
        NewBuildingsRules.Place(offset, lot, out x, out z, out yaw);
        NewBuildingsRules.Footprint(offset, lot, out cx, out cz, out hw, out hd);
        Check(Near(x, 96f) && Near(z, 212f) && Near(cx, 100f) && Near(cz, 207f), "new buildings: a design whose root is not its middle is stood with its middle in the lot's middle");

        // A copy over one of the game's buildings: its door where the building's own is, as far as its footprint may move.
        DesignEntry align = Entry("SM_Align", "ba:buildingsize_c", width: 12f, depth: 20f);
        var place = new MadeLot(100f, 200f, 0f, 20f, 24f);
        float missed = NewBuildingsRules.AlignDoor(align, place, 3f, NewBuildingsRules.Gap, out x, out z, out yaw);
        NewBuildingsRules.DoorAt(align, x, z, yaw, out doorX, out doorZ);
        Check(Near(missed, 0f) && Near(doorX, 103f) && Near(doorZ, 212f) && Near(yaw, 0f),
            "new buildings: a copy over a building has its door where the building's door is, on the building's front line (" + doorX + ", " + doorZ + ")");
        missed = NewBuildingsRules.AlignDoor(align, place, 9f, NewBuildingsRules.Gap, out x, out z, out yaw);
        NewBuildingsRules.DoorAt(align, x, z, yaw, out doorX, out doorZ);
        Check(Near(missed, 3.5f) && Near(doorX, 105.5f), "new buildings: its footprint never moves past the building's place and the gap kept; the door is then short by what is left (" + missed + " m)");
        place = new MadeLot(100f, 200f, 90f, 20f, 24f);
        missed = NewBuildingsRules.AlignDoor(align, place, 3f, NewBuildingsRules.Gap, out x, out z, out yaw);
        NewBuildingsRules.DoorAt(align, x, z, yaw, out doorX, out doorZ);
        Check(Near(missed, 0f) && Near(doorX, 112f, 0.01f) && Near(doorZ, 197f, 0.01f), "new buildings: on a place facing east, right of the middle is south");

        // Every turn of the lot and of the door: the door on the lot's front, the footprint inside the lot.
        bool doorOnFront = true, inside = true;
        string where = null;
        foreach (float facing in new[] { 0f, 37f, 90f, 180f, 251f, 270f, 333f })
        {
            foreach (float doorFacing in new[] { 0f, 90f, 180f, 270f })
            {
                DesignEntry e = Entry("SM_Turn", "ba:buildingsize_c", width: 14f, depth: 22f, doorFacing: doorFacing, front: 3f);
                var each = new MadeLot(-500f, 40f, facing, 20f, 30f);
                NewBuildingsRules.Place(e, each, out x, out z, out yaw);
                NewBuildingsRules.DoorAt(e, x, z, yaw, out doorX, out doorZ);
                double f = facing * Math.PI / 180.0;
                float wantX = each.X + 15f * (float)Math.Sin(f), wantZ = each.Z + 15f * (float)Math.Cos(f);
                if (!Near(doorX, wantX, 0.01f) || !Near(doorZ, wantZ, 0.01f))
                {
                    doorOnFront = false;
                    where = where ?? "lot " + facing + ", door " + doorFacing;
                }
                NewBuildingsRules.Footprint(e, each, out cx, out cz, out hw, out hd);
                for (int i = 0; i < 4; i++)
                {
                    float a = (i & 1) == 0 ? -hw : hw, b = (i & 2) == 0 ? -hd : hd;
                    float px = cx + a * (float)Math.Cos(f) + b * (float)Math.Sin(f), pz = cz - a * (float)Math.Sin(f) + b * (float)Math.Cos(f);
                    inside &= NewBuildingsRules.Inside(px, pz, each.X, each.Z, facing, each.Width * 0.5f, each.Depth * 0.5f, 0.01f);
                }
            }
        }
        Check(doorOnFront, "new buildings: whichever way the lot and the door face, the door ends in the middle of the lot's front (" + (where ?? "all") + ")");
        Check(inside, "new buildings: whichever way they face, the footprint stays inside its lot");

        Check(NewBuildingsRules.Inside(5f, 0f, 0f, 0f, 0f, 5f, 5f, 0f) && !NewBuildingsRules.Inside(5.1f, 0f, 0f, 0f, 0f, 5f, 5f, 0f) && NewBuildingsRules.Inside(5.1f, 0f, 0f, 0f, 0f, 5f, 5f, 0.2f),
            "new buildings: a point on the edge is inside, one just past it is not, unless within the margin");
        Check(NewBuildingsRules.Inside(0f, 9f, 0f, 0f, 90f, 10f, 2f, 0f) && !NewBuildingsRules.Inside(9f, 0f, 0f, 0f, 90f, 10f, 2f, 0f),
            "new buildings: a rectangle facing east is wide north to south");
        Check(NewBuildingsRules.Overlaps(-1f, -1f, 1f, 1f, 0f, 0f, 0f, 2f, 2f, 0f) && !NewBuildingsRules.Overlaps(10f, 10f, 12f, 12f, 0f, 0f, 0f, 2f, 2f, 0f),
            "new buildings: a box inside the footprint meets it; one far off does not");
        Check(NewBuildingsRules.Overlaps(2f, -1f, 3f, 1f, 0f, 0f, 0f, 2f, 2f, 0f) && !NewBuildingsRules.Overlaps(2.1f, -1f, 3f, 1f, 0f, 0f, 0f, 2f, 2f, 0f)
              && NewBuildingsRules.Overlaps(2.1f, -1f, 3f, 1f, 0f, 0f, 0f, 2f, 2f, 0.2f),
            "new buildings: a box touching the footprint meets it, one just apart does not, unless within the gap kept");
        Check(!NewBuildingsRules.Overlaps(5f, 5f, 6f, 6f, 0f, 0f, 45f, 5f, 5f, 0f) && NewBuildingsRules.Overlaps(4f, 0f, 6f, 1f, 0f, 0f, 45f, 5f, 5f, 0f),
            "new buildings: a box in the corner of a turned footprint's reach but clear of the footprint itself does not meet it");
        Check(!NewBuildingsRules.Overlaps(1f, 1f, -1f, -1f, 0f, 0f, 0f, 50f, 50f, 0f), "new buildings: a box with nothing in it meets nothing");
        Check(NewBuildingsRules.Within(0f, 0f, 0f, 5f, 5f, -10f, -10f, 10f, 10f, 4f) && !NewBuildingsRules.Within(0f, 0f, 0f, 5f, 5f, -10f, -10f, 10f, 10f, 5.5f)
              && NewBuildingsRules.Within(0f, 0f, 45f, 5f, 5f, -10f, -10f, 10f, 10f, 2.5f) && !NewBuildingsRules.Within(0f, 0f, 45f, 5f, 5f, -10f, -10f, 10f, 10f, 3f),
            "new buildings: a footprint is inside the city's limits only with every corner inside them, turned or not");
    }

    // ---------------------------------------------------------------- the lot taken

    private static void NewBuildingsLots()
    {
        var asked = new List<int>();
        Check(NewBuildingsRules.FirstFree(6, 0, k => true) == 0 && NewBuildingsRules.FirstFree(6, 2, k => k != 2) == 3 && NewBuildingsRules.FirstFree(6, 5, k => k == 1) == 1,
            "new buildings: the lot asked for when it is free, else the next free one, round to the start");
        Check(NewBuildingsRules.FirstFree(6, 0, k => false) == -1 && NewBuildingsRules.FirstFree(0, 0, k => true) == -1 && NewBuildingsRules.FirstFree(6, 0, null) == -1,
            "new buildings: no free lot, no lots, or nothing to ask: none, and the building stands down");
        Check(NewBuildingsRules.FirstFree(6, 9, k => true) == 0 && NewBuildingsRules.FirstFree(6, -1, k => k >= 4) == 4, "new buildings: a lot asked for that is not there starts at the first");
        NewBuildingsRules.FirstFree(6, 4, k =>
        {
            asked.Add(k);
            return k == 2;
        });
        Check(asked.Count == 5 && asked[0] == 4 && asked[1] == 5 && asked[2] == 0 && asked[3] == 1 && asked[4] == 2,
            "new buildings: the lots are tried in one fixed order (4, 5, 0, 1, 2), so every machine with the same ground takes the same one");

        Check(NewBuildingsRules.CostPasses(10.4f, 10f) && !NewBuildingsRules.CostPasses(10.6f, 10f) && !NewBuildingsRules.CostPasses(10.5f, 10f) && NewBuildingsRules.CostPasses(9f, 10f),
            "new buildings: ten copies pass the frame cost test when they add under 0.5 ms");
        bool ahead = true, apart = true;
        var spotX = new float[NewBuildingsRules.CostCopies];
        var spotZ = new float[NewBuildingsRules.CostCopies];
        foreach (float facing in new[] { 0f, 90f, 213f })
        {
            double f = facing * Math.PI / 180.0;
            for (int i = 0; i < NewBuildingsRules.CostCopies; i++)
            {
                NewBuildingsRules.CostSpot(i, 1000f, -300f, facing, out spotX[i], out spotZ[i]);
                float along = (spotX[i] - 1000f) * (float)Math.Sin(f) + (spotZ[i] + 300f) * (float)Math.Cos(f);
                float across = (spotX[i] - 1000f) * (float)Math.Cos(f) - (spotZ[i] + 300f) * (float)Math.Sin(f);
                ahead &= along >= NewBuildingsRules.CopyAhead - 0.01f && Math.Abs(across) <= 60.01f;
                for (int j = 0; j < i; j++)
                {
                    float dx = spotX[i] - spotX[j], dz = spotZ[i] - spotZ[j];
                    apart &= dx * dx + dz * dz >= 29.9f * 29.9f;
                }
            }
        }
        NewBuildingsRules.CostSpot(2, 0f, 0f, 0f, out float midX, out float midZ);
        Check(ahead && apart && Near(midX, 0f) && Near(midZ, NewBuildingsRules.CopyAhead),
            "new buildings: the ten copies of the frame cost test stand in view, 40 m ahead and further, 30 m apart");
    }

    // ---------------------------------------------------------------- what refuses a declaration

    private static void NewBuildingsRefusals()
    {
        Func<string, int, string, bool> takes = (size, version, type) => true;
        Func<string, bool> known = district => true;
        MadeSpec good = NewBuildingsTestPlot.Spec();
        Check(NewBuildingsRules.Refusal(good, takes, known, true) == null, "new buildings: the test building's declaration is one that can be made");

        MadeSpec s = NewBuildingsTestPlot.Spec();
        s.Id = null;
        bool noId = NewBuildingsRules.Refusal(s, takes, known, true) != null && NewBuildingsRules.Refusal(null, takes, known, true) != null;
        s = NewBuildingsTestPlot.Spec();
        s.Street = "ba:street_fifthavenue";
        string theirs = NewBuildingsRules.Refusal(s, takes, known, true);
        s.Street = MadeStreets.Prefix + "nowhere";
        string unknown = NewBuildingsRules.Refusal(s, takes, known, true);
        s.Street = null;
        string none = NewBuildingsRules.Refusal(s, takes, known, true);
        Check(noId && theirs != null && theirs.Contains("not one of the mod's own streets") && unknown != null && none != null,
            "new buildings: a declaration with no id, on one of the game's streets (a free update may fill it) or on a street the mod does not have is refused");
        s = NewBuildingsTestPlot.Spec();
        s.Number = 0;
        Check(NewBuildingsRules.Refusal(s, takes, known, true) != null, "new buildings: a house number under 1 is refused");
        s = NewBuildingsTestPlot.Spec();
        s.Type = NewBuildingsRules.SpecialType;
        string special = NewBuildingsRules.Refusal(s, takes, known, true);
        s.Type = null;
        Check(special != null && special.Contains("special service") && NewBuildingsRules.Refusal(s, takes, known, true) != null,
            "new buildings: the game's special building type (its towing reads a special service) and no type are refused");
        s = NewBuildingsTestPlot.Spec();
        string noStructure = NewBuildingsRules.Refusal(s, (size, version, type) => false, known, true);
        string noAsk = NewBuildingsRules.Refusal(s, null, known, true);
        string noDistrict = NewBuildingsRules.Refusal(s, takes, district => false, true);
        Check(noStructure != null && noStructure.Contains("no structure") && noAsk != null && noDistrict != null && noDistrict.Contains("no district"),
            "new buildings: a size, version and type the game has no structure for, or a district it does not have, is refused: the game's limits are asked, never assumed");
        s.Type = NewBuildingsRules.HomeType;
        Check(NewBuildingsRules.Refusal(s, takes, known, false) != null && NewBuildingsRules.Refusal(s, takes, known, true) == null,
            "new buildings: a home is refused unless the game's food deliveries can be told to leave it alone");
        s = NewBuildingsTestPlot.Spec();
        s.Lots = new MadeLot[0];
        bool noLots = NewBuildingsRules.Refusal(s, takes, known, true) != null;
        s.Lots = null;
        noLots &= NewBuildingsRules.Refusal(s, takes, known, true) != null;
        s = NewBuildingsTestPlot.Spec();
        s.Traffic = 101;
        bool traffic = NewBuildingsRules.Refusal(s, takes, known, true) != null;
        s.Traffic = -1;
        traffic &= NewBuildingsRules.Refusal(s, takes, known, true) != null;
        Check(noLots && traffic, "new buildings: a declaration with nowhere to stand, or a traffic index outside 0 to 100, is refused");

        Check(MadeStreets.IsMine(MadeStreets.Prefix + "provinglane") && !MadeStreets.IsMine("ba:street_fifthavenue") && !MadeStreets.IsMine(string.Empty) && !MadeStreets.IsMine(null)
              && !MadeStreets.IsMine("REALLIFE:street_provinglane") && !MadeStreets.Prefix.StartsWith("ba:", StringComparison.Ordinal),
            "new buildings: a street of the mod's own is told by its prefix alone, which none of the game's streets has");
        bool streets = MadeStreets.All.Length > 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var shorts = new HashSet<string>(StringComparer.Ordinal);
        foreach (MadeStreet street in MadeStreets.All)
        {
            streets &= street.Id.StartsWith(MadeStreets.Prefix, StringComparison.Ordinal) && ids.Add(street.Id) && shorts.Add(street.Abbreviation) && street.Abbreviation.Length == 3
                       && MadeStreets.Find(street.Id) == street && street.NameKey.StartsWith("reallife_newbuildings_street_", StringComparison.Ordinal);
        }
        Check(streets && MadeStreets.Find("ba:street_fifthavenue") == null && MadeStreets.Find(null) == null,
            "new buildings: every street of the mod's own has the prefix, an id and a short name of its own, and a name in the texts");
    }

    // ---------------------------------------------------------------- the test plot

    private static void NewBuildingsTestPlotChecks()
    {
        MadeLot[] lots = NewBuildingsTestPlot.Lots;
        bool apart = lots.Length == 6, inLine = true;
        for (int i = 0; i < lots.Length; i++)
        {
            inLine &= lots[i].Facing == 0f && lots[i].Width == 30f && lots[i].Depth == 34f && Near(lots[i].Z + lots[i].Depth * 0.5f, -909f);
            for (int j = 0; j < i; j++)
            {
                apart &= Math.Abs(lots[i].X - lots[j].X) - lots[i].Width >= 2f * NewBuildingsRules.Gap;
            }
            if (i > 0)
            {
                apart &= lots[i].X > lots[i - 1].X;   // west to east: the order they are tried in
            }
        }
        Check(apart && inLine, "new buildings: the test plot is six lots west to east along one front line, 30 by 34 m, never closer than the gap kept between two buildings");
        // The road's southern lane runs at z -903 from x -2313 to -2087 (the traffic's Road_237).
        float laneZ = -903f, front = lots[0].Z + lots[0].Depth * 0.5f;
        Check(laneZ - front >= NewBuildingsRules.RoadClear && laneZ - front <= NewBuildingsRules.StreetReach && lots[0].X - 15f >= -2313f && lots[5].X + 15f <= -2087f,
            "new buildings: the test plot fronts the road north of the airport: clear of its lane, near enough to front it, along its straight stretch");
        MadeSpec a = NewBuildingsTestPlot.Spec(), b = NewBuildingsTestPlot.Spec();
        a.Lots[0].X = 0f;
        Check(b.Lots[0].X == lots[0].X && NewBuildingsTestPlot.Lots[0].X == -2290f && a != b, "new buildings: each declaration of the test building is its own, its lots too");
        Check(MadeStreets.Find(NewBuildingsTestPlot.Street) != null && NewBuildingsTestPlot.Number == 1 && NewBuildingsTestPlot.Type != NewBuildingsRules.SpecialType && NewBuildingsTestPlot.Type != NewBuildingsRules.HomeType,
            "new buildings: the test building is 1 Proving Lane, a street of the mod's own, an office (no special service, no food deliveries)");
    }

    // ---------------------------------------------------------------- the load's cost

    private static void NewBuildingsTiming()
    {
        var designs = new List<DesignEntry>(600);
        string[] sizes = { "ba:buildingsize_c", "ba:buildingsize_d", "ba:buildingsize_e", "ba:buildingsize_f", "ba:buildingsize_k" };
        string[] types = { "ba:buildingtype_office", "ba:buildingtype_retail", "ba:buildingtype_residential", "ba:buildingtype_warehouse" };
        for (int i = 0; i < 600; i++)
        {
            designs.Add(Entry("SM_Design" + i.ToString("000"), sizes[i % sizes.Length], types[i % types.Length], 10f + i % 25, 12f + i % 27, (i % 4) * 90f, i % 9 == 0));
        }
        designs.Sort(NewBuildingsRules.CompareDesigns);
        double best = double.MaxValue;
        for (int run = 0; run < 5; run++)
        {
            best = Math.Min(best, PicksTime(designs));
        }
        Check(best < 50.0, "new buildings: 120 picks (20 declarations, each over 6 lots) over 600 designs take under 50 ms behind the loading screen: " + best.ToString("0.0") + " ms");
        best = double.MaxValue;
        for (int run = 0; run < 5; run++)
        {
            best = Math.Min(best, GroundTime());
        }
        Check(best < 100.0, "new buildings: the ground's box checks of 120 lots against 3,000 of the game's buildings take under 100 ms: " + best.ToString("0.0") + " ms");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PicksTime(List<DesignEntry> designs)
    {
        var watch = Stopwatch.StartNew();
        int found = 0;
        for (int spec = 0; spec < 20; spec++)
        {
            for (int lot = 0; lot < 6; lot++)
            {
                found += NewBuildingsRules.Pick(designs, "ba:buildingsize_c", "ba:buildingtype_office", null, 30f + lot, 34f, "spec_" + spec) >= 0 ? 1 : 0;
            }
        }
        watch.Stop();
        return found == 120 ? watch.Elapsed.TotalMilliseconds : double.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double GroundTime()
    {
        var watch = Stopwatch.StartNew();
        int met = 0;
        for (int lot = 0; lot < 120; lot++)
        {
            float cx = lot * 40f, cz = 0f;
            for (int b = 0; b < 3000; b++)
            {
                float x0 = (b % 60) * 90f, z0 = (b / 60) * 90f + 50f;
                met += NewBuildingsRules.Overlaps(x0, z0, x0 + 30f, z0 + 30f, cx, cz, lot * 7f, 15f, 17f, NewBuildingsRules.Gap) ? 1 : 0;
            }
        }
        watch.Stop();
        return met >= 0 ? watch.Elapsed.TotalMilliseconds : double.MaxValue;
    }

    // ---------------------------------------------------------------- the game's side, read as text

    private static void NewBuildingsSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string folder = System.IO.Path.Combine(scripts, "NewBuildings");
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        string director = Read(@"NewBuildings\NewBuildingsDirector.cs"), tests = Read(@"NewBuildings\NewBuildingsDirector.Tests.cs"), made = Read(@"NewBuildings\MadeGame.cs");
        string save = Read(@"NewBuildings\MadeGame.Save.cs"), copy = Read(@"NewBuildings\CopyGame.cs"), ground = Read(@"NewBuildings\CopyGame.Ground.cs"), look = Read(@"NewBuildings\CopyLook.cs");
        string contract = Read(@"Core\GameContract.cs"), settings = Read(@"Core\Settings.cs"), tree = Read(@"Meta\OptionsTree.cs"), mod = Read(@"Core\RealLifeOverhaulMod.cs");
        string owners = Read(@"Core\FeatureOwners.cs"), wiring = Read(@"Wiring\Wiring.cs"), guide = Read(@"NewBuildings\NewBuildingsGuide.cs");
        Check(director.Length > 0 && tests.Length > 0 && made.Length > 0 && save.Length > 0 && copy.Length > 0 && ground.Length > 0 && look.Length > 0 && Directory.Exists(folder),
            "new buildings: the director, its tests, the made buildings' and the copy engine's Game files and the copy's look are there");

        // Every file of the folder, its code alone (comments and the log's words name the game's lists freely).
        var code = new StringBuilder(1 << 18);
        var files = new List<string>(Directory.Exists(folder) ? Directory.GetFiles(folder, "*.cs") : new string[0]);
        files.Sort(StringComparer.Ordinal);
        foreach (string path in files)
        {
            code.Append(CodeAlone(File.ReadAllText(path))).Append('\n');
        }
        string all = code.ToString();

        // Nothing of a made building reaches the save's lists: no list of the save is added to, and only the save guard
        // takes out of them (and puts back only what it took).
        Check(!Regex.IsMatch(all, @"BuildingRegistrations\.(Add|Insert|AddRange)\(") && !Regex.IsMatch(all, @"\ballBuildings\b|\bAllBuildings\b") && !all.Contains(".modData")
              && !all.Contains("SaveHook.Keep(") && !all.Contains("SaveLines"),
            "new buildings: nothing of a made building is added to the save's registrations, the city's list of all buildings or the mod's lines in the save");
        bool onlyGuard = true;
        foreach (string path in files)
        {
            string name = System.IO.Path.GetFileName(path);
            string text = CodeAlone(File.ReadAllText(path));
            if (name != "MadeGame.Save.cs" && Regex.IsMatch(text, @"\.(deliveryJobLocationsDoneToday|foodDeliveryOffers|customDestination|currentPlayerMission|LastPlayerPosition|CurrentStreetName)\b"))
            {
                onlyGuard = false;
            }
        }
        Check(onlyGuard && MemberText(made, "public static int InSaveList(").Contains("found +=") && !Regex.IsMatch(MemberText(made, "public static int InSaveList("), @"\.(Add|Remove|Insert)\("),
            "new buildings: only the save guard (MadeGame.Save.cs) touches what the save names, and the made buildings' check only counts the save's registrations");
        string enter = MemberText(made, "public static bool Enter(");
        int reg = enter.IndexOf("registrations[address] = registration;", StringComparison.Ordinal), rec = enter.IndexOf("buildings[address] = record;", StringComparison.Ordinal);
        Check(reg > 0 && rec > reg && enter.Contains("GameContract.RegistrationLookup.Field") && enter.Contains("GameContract.BuildingLookup.Field"),
            "new buildings: a made building's registration is in the game's private lookup before its record (the game's own lookup would otherwise add one to the save)");
        string make = MemberText(director, "private bool Make(MadeSpec spec)");
        int entered = make.IndexOf("MadeGame.Enter(record, registration, out why)", StringComparison.Ordinal), working = make.IndexOf("CopyGame.MakeWorking(", StringComparison.Ordinal);
        int claim = make.IndexOf("MadeGame.Claim(spec", StringComparison.Ordinal);
        Check(entered > 0 && working > entered && claim > working && make.Contains("MadeGame.Leave(record);") && Occurs(all, "MadeGame.Claim(") == 1,
            "new buildings: the address is claimed in the special buildings' book only once the record, the registration and the copy are all there (held units asks the game's lookup at once)");
        string wake = MemberText(copy, "public static CityBuildingController MakeWorking(");
        int active = wake.IndexOf("copy.gameObject.SetActive(true);", StringComparison.Ordinal);
        Check(active > 0 && wake.IndexOf("c.building = building;", StringComparison.Ordinal) is int b && b > 0 && b < active
              && wake.IndexOf("c.buildingRegistration = registration;", StringComparison.Ordinal) is int r && r > 0 && r < active,
            "new buildings: a working copy is given its record and registration before it wakes (the city's list can reach it before its Start)");
        Check(MemberText(made, "public static BuildingRegistration MakeRegistration(").Contains("reg.AvailableForRent = false;") && !Regex.IsMatch(all, @"AvailableForRent\s*=\s*true|ForSale\s*=\s*true|RentedByPlayer\s*=\s*true")
              && MemberText(made, "public static Building MakeRecord(").Contains("b.SpecialService = null;") && MemberText(made, "public static Building MakeRecord(").Contains("ScriptableObject.CreateInstance<Building>()"),
            "new buildings: a made building is never to let or for sale and has no special service; its record is new, so its DLC is the default, none");
        Check(MemberText(made, "public static bool Claim(").Contains("SpecialBuildings.Reserve(spec.Id, spec.Street, spec.Number, on, SpecialKind.Other)") && !all.Contains("IgnoredAddresses"),
            "new buildings: a made building is claimed through the special buildings' book, and never put on the game's ignore list here");

        // No made controller is destroyed in a session: only a decorative copy (no controller), a sleeping template, and the
        // records and streets the mod made, once the city goes.
        var destroyed = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(all, @"\bDestroy\(([^;]*?)\);"))
        {
            destroyed.Add(m.Groups[1].Value.Trim());
        }
        Check(string.Join(" ", destroyed) == "b data look.gameObject template.gameObject" && MemberText(copy, "public static void Remove(CopyLook look)").Contains("UnityEngine.Object.Destroy(look.gameObject)")
              && MemberText(copy, "public static void Drop(Transform template)").Contains("UnityEngine.Object.Destroy(template.gameObject)"),
            "new buildings: the folder destroys only a decorative copy, a sleeping template, and its own records and streets (" + string.Join(", ", destroyed) + ")");
        string unload = MemberText(made, "public static void Unload(bool cityGoes)"), drop = MemberText(made, "private static void Drop()");
        int left = drop.IndexOf("Leave(b);", StringComparison.Ordinal), gone = drop.IndexOf("UnityEngine.Object.Destroy(b);", StringComparison.Ordinal);
        Check(unload.Contains("if (cityGoes)") && unload.Contains("Drop();") && left > 0 && gone > left && drop.Contains("UnityEngine.Object.Destroy(data);")
              && drop.Contains("lookup.Remove(pair.Key);") && MemberText(made, "public static Building MakeRecord(").Contains("Records.Add(b);"),
            "new buildings: what the mod makes by code (each record, each street) is taken out of the game's lookups and destroyed when the city goes, and only then");
        string standing = MemberText(copy, "public static CityBuildingController MakeWorking(");
        Check(standing.Contains("SceneManager.MoveGameObjectToScene(copy.gameObject, source.gameObject.scene)") && !standing.Contains("_standing") && !standing.Contains("_asleep"),
            "new buildings: a made building's copy stands in the scene of the building it copies, never under the mod's own objects (the save tools' removal destroys those)");
        string down = MemberText(director, "private void OnDestroy()"), outOf = MemberText(director, "private void KeepOutOfSaves()");
        Check(down.Contains("bool stays = Meta.SaveTools.Dormant;") && down.Contains("KeepOutOfSaves();") && down.Contains("MadeGame.Unload(!stays);")
              && outOf.IndexOf("MadeGame.Strip(_taken);", StringComparison.Ordinal) is int stripped && stripped > 0 && outOf.IndexOf("_taken.Clear();", StringComparison.Ordinal) > stripped
              && outOf.Contains("CopyGame.Show(made.Controller, false);"),
            "new buildings: when the save tools stop the mod in a city that stays, the made buildings stay hidden and the save in memory names none of their addresses for good");
        int immediate = Occurs(all, "DestroyImmediate(");
        Check(immediate == 3 && Occurs(MemberText(copy, "private static void StripWorking("), "DestroyImmediate(") == 1 && Occurs(MemberText(copy, "private static void StripLook("), "DestroyImmediate(") == 2,
            "new buildings: what is taken off at once is taken off a template asleep under a switched-off holder (its Awake never ran)");
        Check(MemberText(copy, "public static void Show(CityBuildingController c, bool shown)").Contains("c.gameObject.SetActive(shown)") && MemberText(director, "private void Apply(string why)").Contains("CopyGame.Show(made.Controller, want)")
              && MemberText(director, "public void StopAll(string why)").Contains("Apply(why)") && !MemberText(director, "private void OptionChanged(bool on)").Contains("Remove("),
            "new buildings: a made building switched off, refused by a session or cleaned up is hidden, never destroyed");
        bool destroysMade = false;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = CodeAlone(File.ReadAllText(path));
            destroysMade |= Regex.IsMatch(text, @"Destroy\w*\([^;]*\.Controller\b") && text.Contains("MadeBuilding");
        }
        Check(!destroysMade, "new buildings: no file of the mod destroys a made building's controller");

        // Every member of the game's read by name is in Core/GameContract.
        Check(!Regex.IsMatch(all, @"GetField\(|GetProperty\(|GetMethod\(|BindingFlags|AccessTools|Traverse|InvokeMember\(")
              && !Regex.IsMatch(all, "\"(AllBuildingDictionary|AllBuildingRegistrationDictionary|StreetDataDictionary|ClosestCbcPositionJob|excludedDeliveryAddresses)\""),
            "new buildings: no member of the game's is looked up by name outside Core/GameContract");
        Check(contract.Contains("ContractBook.Part(\"own_buildings\", \"newbuildings\"") && contract.Contains("\"Helpers.BuildingHelper\", \"AllBuildingDictionary\"")
              && contract.Contains("\"Helpers.BuildingHelper\", \"AllBuildingRegistrationDictionary\"") && contract.Contains("\"Streets.AddressHelper\", \"StreetDataDictionary\"")
              && contract.Contains("Optional(StaticField(OwnBuildings, Game, \"Buildings.ClosestBuildingFromPlayer\", \"ClosestCbcPositionJob\"")
              && contract.Contains("Optional(Field(OwnBuildings, Game, \"Player.FoodDeliveryJob.FoodDeliveryJobConfig\", \"excludedDeliveryAddresses\""),
            "new buildings: the game's lookups of buildings, registrations and streets are contract lines of their own part; the closest-building job and the deliveries' list are optional ones");

        // The load, the save guard, the option, Going Public, DLCs, streets and the ground.
        string awake = MemberText(director, "private void Awake()"), slow = MemberText(director, "private void Slow(float now)"), destroy = MemberText(director, "private void OnDestroy()");
        Check(Regex.IsMatch(awake, @"ScenePrewarm\.Add\(""[^""]+"", MakeAll\);") && Regex.IsMatch(awake, @"ScenePrewarm\.AddLate\(""[^""]+"", Listed\);")
              && mod.Contains("Guard.Add<NewBuildings.NewBuildingsDirector>("),
            "new buildings: the buildings are made behind the loading screen, before the game's own start of the city lists its buildings, and looked for in the list after it");
        string onSave = MemberText(director, "private void OnSave()");
        Check(slow.Contains("SaveHook.KeepEssential(_onSave);") && slow.IndexOf("SaveHook.KeepEssential(_onSave);", StringComparison.Ordinal) < slow.IndexOf("if (MadeGame.Loading)", StringComparison.Ordinal)
              && destroy.Contains("SaveHook.Drop(_onSave);") && destroy.Contains("PutBackNow(") && MemberText(director, "private void OnDisable()").Contains("PutBackNow(")
              && onSave.Contains("finally") && onSave.Contains("StartCoroutine(Guard.Routine(PutBackAfter()))"),
            "new buildings: the save guard runs whatever is switched off, puts back a frame later (after a fault too), and at once if the city goes first");
        string strip = MemberText(save, "public static int Strip(SaveTaken taken)"), back = MemberText(save, "public static bool PutBack(SaveTaken taken)");
        Check(strip.Contains("MadeStreets.IsMine(save.CurrentStreetName)") && strip.Contains("save.CurrentStreetName = string.Empty;") && strip.Contains("save.LastPlayerPosition = ")
              && strip.Contains("MadeStreets.IsMine(regs[i].StreetName)") && back.Contains("save.CurrentStreetName = taken.Street;") && back.Contains("save.LastPlayerPosition = taken.Position;"),
            "new buildings: a save made inside a made building is written with no address and the player at its door outside, and the save in memory is put back as it was");
        Check(MemberText(director, "private bool MakeAll()").IndexOf("if (!Settings.NewBuildings)", StringComparison.Ordinal) is int off && off > 0
              && off < MemberText(director, "private bool MakeAll()").IndexOf("MadeBuildings.Wanted(", StringComparison.Ordinal)
              && settings.Contains("public const string NewBuildingsId = \"new_buildings\";") && settings.Contains("public static bool NewBuildings = true;")
              && settings.Contains("NewBuildings = Bool(modId, NewBuildingsId, NewBuildings);") && guide.Contains(".Owns(Settings.NewBuildingsId)")
              && tree.Contains("S(\"newbuildings\").For(\"newbuildings\").Main(\"new_buildings\")") && owners.Contains("{ \"RealLifeOverhaul.NewBuildings\", \"newbuildings\" }"),
            "new buildings: an option of its own, on by default, in the options tree; off, nothing is made at the next load");
        Check(MemberText(director, "private void Apply(string why)").Contains("MadeGame.SessionAgrees()") && MemberText(made, "public static bool SessionAgrees()").Contains("SharedCity.Following")
              && MemberText(made, "public static bool SessionAgrees()").Contains("SharedCity.Leading") && !Regex.IsMatch(all, @"\bSend\w*\(|Network|Rpc"),
            "new buildings: in a Going Public session they stand only while every machine is known to run the mod, and nothing is sent");
        string ofDlc = MemberText(copy, "private static bool OfDlc(CityBuildingController c)");
        Check(ofDlc.Contains("GameDlc.Gated(c)") && ofDlc.Contains("GetComponentInChildren<ToggleObjectDLC>(true)") && ofDlc.Contains("GetComponentInParent<ToggleObjectDLC>(true)")
              && Occurs(copy, "if (OfDlc(c))") == 2 && !Regex.IsMatch(all, @"requiredDLC|DlcIsOwned|IsDlcInstalled|SteamAPI\.|SteamApps\."),
            "new buildings: no design the game keeps behind a DLC, or with a DLC's content in it or round it, is ever picked (Core/GameDlc), and whether a DLC is owned is never read");
        Check(!all.Contains(".cityBuildingControllers") && Occurs(all, "GameDlc.Open(") >= 3 && MemberText(made, "public static bool InCityList(").Contains("GameDlc.GatedInCity"),
            "new buildings: the city's list of buildings is read only through Core/GameDlc, and the made buildings' check counts it as the game does");
        Check(Read(@"Meta\FeatureCities.cs").Contains("new Line(\"newbuildings\", CityScope.NewYork,") && Read(@"..\..\..\..\RealLifeOverhaul.Build\ContractCheck\CityChecks.cs").Contains("\"NewBuildings/CopyGame.cs\""),
            "new buildings: the feature stands on New York's map (the city table: another city has none of it), and its picks are in ContractCheck's census of picks");
        Check(!Regex.IsMatch(all, "\"ba:street_") && MemberText(made, "public static int AddStreets()").Contains("GameContract.StreetLookup.Field") && MemberText(made, "public static int AddStreets()").Contains("CreateInstance<StreetData>()"),
            "new buildings: no street of the game's is ever used; the mod's own are put among the game's at every load");
        Check(make.Contains("CopyGame.GroundWhy(designs[pick], each, out root, out yaw, out door)") && make.Contains("NewBuildingsRules.FirstFree(spec.Lots.Length, spec.Preferred,")
              && MemberText(director, "private bool MakeAll()").Contains("CopyGame.MeasureAll(") && MemberText(director, "private bool MakeAll()").Contains("CopyGame.MeasureOthers("),
            "new buildings: every lot's ground is checked at every load before a building is stood on it, and the next free lot is taken by the fixed rule");
        Check(make.Contains("NewBuildingsRules.Refusal(spec, MadeGame.StructureTakes, MadeGame.DistrictKnown, MadeGame.HomesLeftOut())") && make.Contains("MadeGame.GameHas(spec.Street, spec.Number)")
              && MemberText(made, "public static bool StructureTakes(").Contains("BuildingSizeHelper.GetAllBuildingSizes()") && MemberText(made, "public static bool StructureTakes(").Contains("!each.specialBuildingOnly")
              && MemberText(made, "public static bool DistrictKnown(").Contains("NeighborhoodHelper.Neighborhoods") && MemberText(made, "public static bool DistrictKnown(").Contains("CityCheck.Here(district)"),
            "new buildings: the game's sizes, versions, types and districts (of the loaded city) are read from the game at the load, and an address the game has already is left to it");
        Check(MemberText(copy, "private static void Measure(CityBuildingController c)").Contains("NewBuildingsRules.MeasuredFrom(") && MemberText(copy, "private static void Finish()").Contains("Designs.Sort(NewBuildingsRules.CompareDesigns)"),
            "new buildings: the catalogue is the same on every machine: each design measured on the same building, in one order");
        Check(look.Contains("public sealed class CopyLook : ViewBlockingEntity") && MemberText(look, "public override void OnDestroy()").Contains("Delegate.Remove(GlobalEvents.onCityMapToggle, _onMap)")
              && MemberText(look, "public override void Awake()").Contains("LayerHelper.BuildingLayerIndex"),
            "new buildings: a decorative copy fades as the game's buildings do, follows the map, stands on the buildings' layer and takes its one handler off when it goes");
        Check(wiring.Contains("Step(\"the mod's own buildings on the minimap\", NewBuildingsLinks.Connect);") && wiring.Contains("Step(\"the mod's own buildings on the minimap\", NewBuildingsLinks.Disconnect);"),
            "new buildings: the minimap is told about the mod's own buildings through the wiring, and let go of them");

        // The decorative copies for the construction sites: every change out of view, the swap in one frame, undone the same way.
        string copies = Read(@"NewBuildings\NewBuildingsDirector.Copies.cs"), api = Read(@"NewBuildings\BuildingCopies.cs");
        string change = MemberText(copies, "private bool Change(bool outOfViewOnly)"), stand = MemberText(copies, "private void Stand(CopyHandle h, string when)");
        Check(Occurs(change, "!outOfViewOnly || ") == 4 && Occurs(change, "CopyGame.InView(") == 4 && MemberText(copies, "private bool Copies()").Contains("Change(true);")
              && MemberText(copies, "private void CopiesNow()").Contains("while (Change(false))") && MemberText(director, "private bool Listed()").Contains("CopiesNow();"),
            "new buildings: in play a copy stands or goes and a look is hidden or shown only out of the camera's view; behind the loading screen, all at once");
        int stood = stand.IndexOf("CopyGame.MakeLook(", StringComparison.Ordinal), hid = stand.IndexOf("CopyGame.HideLook(h.Over)", StringComparison.Ordinal);
        Check(stood > 0 && hid > stood && stand.Contains("CopyGame.Lend(look, h.Over);") && MemberText(api, "public static void Remove(CopyHandle handle)").Contains("CopyGame.ShowLook(handle.Over);")
              && MemberText(api, "internal static int RemoveAll(string why)").Contains("CopyGame.ShowAllLooks();"),
            "new buildings: a building's new look stands and its own look is hidden in the same frame, its hovers and clicks the building's; taken away, the own look comes back with it");
        string hideLook = MemberText(copy, "public static bool HideLook(CityBuildingController c)"), showLook = MemberText(copy, "public static bool ShowLook(CityBuildingController c)");
        Check(hideLook.Contains("list.Add(c.lodVersion);") && hideLook.Contains("forceRenderingOff = true;") && hideLook.Contains("lights[i].enabled = false;")
              && showLook.Contains("lights[i].enabled = true;") && MemberText(look, "internal void Lend(ViewBlockingEntity owner)").Contains("cityBuildingController = owner != null ? owner : this;"),
            "new buildings: a building's own look hidden is every renderer under it, its box (its shadow and its city map look) and its signs' lights, all given back as they were");
        Check(MemberText(api, "public static CopyHandle PlaceOnPlot(").Contains("NewBuildingsRules.Turn(yaw + 180f)") && Read(@"Civic\PlotRules.cs").Contains("its front is -Z")
              && Read(@"Civic\CivicWorks.cs").Contains("public SiteState StartPlot(Vector3 middle, int yaw, int width, int depth,"),
            "new buildings: a plot's building takes the plot as the construction sites give it (CivicWorks.StartPlot), its front where theirs is (PlotRules: -Z)");

        // The tests: each needs Developer diagnostics, and each writes its check line.
        Check(MemberText(tests, "public static void TestMake()").Contains("Meta.Dev.Verbose") && director.Contains("if (TestAsked && Meta.Dev.Verbose")
              && guide.Contains(".Test(\"reallife_newbuildings_test_make\", \"reallife_newbuildings_test_walk\", \"reallife_newbuildings_test_save\", \"reallife_newbuildings_test_copy\",")
              && guide.Contains("\"reallife_newbuildings_test_cost\", \"reallife_newbuildings_test_off\")"),
            "new buildings: the six test entries are on the options page, and the test building is made only with Developer diagnostics on");
        string lines = director + tests;
        bool written = true;
        foreach (string line in new[] { "\"Made buildings check (\" + name + \", \"", "in the game's building lookup ", "the closest-building job sized to the list", "Made buildings check (way in): entered on structure ",
                     "Made buildings check (way out): the player is at its door", "Save check: the written save names no made address (registrations ", "Save check (put back): ",
                     "Construction check (copy): \" + d.Name + \", 0 in a static batch, fade linked", "Copy check (frame cost): ", "Made buildings check (switched off): ",
                     "the hidden building's hourly handlers ran without an error" })
        {
            written &= lines.Contains(line);
        }
        Check(written, "new buildings: every test writes its check line, as the test list reads them");
    }

    // A file's code with its comments taken off (a "//" inside a string stays).
    private static string CodeAlone(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (string raw in text.Split('\n'))
        {
            bool quoted = false, verbatim = false;
            int cut = raw.Length;
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (quoted)
                {
                    if (ch == '\\' && !verbatim)
                    {
                        i++;
                    }
                    else if (ch == '"')
                    {
                        quoted = false;
                    }
                    continue;
                }
                if (ch == '"')
                {
                    quoted = true;
                    verbatim = i > 0 && raw[i - 1] == '@';
                }
                else if (ch == '\'' && i + 2 < raw.Length)
                {
                    i += raw[i + 1] == '\\' ? 3 : 2;   // a character: '"' or '\''
                }
                else if (ch == '/' && i + 1 < raw.Length && raw[i + 1] == '/')
                {
                    cut = i;
                    break;
                }
            }
            sb.Append(raw, 0, cut).Append('\n');
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- the texts

    private static void NewBuildingsTexts()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string keysFile = System.IO.Path.Combine(scripts, "NewBuildings", "NewBuildingsText.cs");
        string keysText = File.Exists(keysFile) ? File.ReadAllText(keysFile) : string.Empty;
        var others = new StringBuilder(1 << 18);
        foreach (string path in Directory.GetFiles(System.IO.Path.Combine(scripts, "NewBuildings"), "*.cs"))
        {
            if (System.IO.Path.GetFileName(path) != "NewBuildingsText.cs")
            {
                others.Append(File.ReadAllText(path)).Append('\n');
            }
        }
        string sources = others.ToString();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json")), pt = LoadWords(System.IO.Path.Combine(LocalesFolder(), "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(LocalesFolder(), "zh-cn.json"));
        var keys = new List<string>();
        var names = new List<string>();
        foreach (Match m in Regex.Matches(keysText, @"public const string (\w+) = ""(reallife_[a-z_]+)"";"))
        {
            names.Add(m.Groups[1].Value);
            keys.Add(m.Groups[2].Value);
        }
        keys.Add("reallife_opt_tip_new_buildings");
        keys.Add("reallife_opt_sec_newbuildings");
        keys.Add("reallife_contract_part_own_buildings");
        bool there = en != null && pt != null && zh != null && names.Count == 28;
        string missing = null;
        foreach (string key in keys)
        {
            bool all3 = there && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0;
            if (all3 && !SameHoles(en[key], pt[key], zh[key]))
            {
                all3 = false;
            }
            if (!all3)
            {
                there = false;
                missing = missing ?? key;
            }
        }
        Check(there, "new buildings: every text is in en, pt and zh-cn with the same {holes} (" + (missing ?? keys.Count + " texts") + ")");
        bool used = true;
        string unused = null;
        for (int i = 0; i < names.Count; i++)
        {
            if (!sources.Contains("NewBuildingsText." + names[i]) && !sources.Contains("\"" + keys[i] + "\""))
            {
                used = false;
                unused = unused ?? keys[i];
            }
        }
        Check(used, "new buildings: every text of NewBuildingsText is used (" + (unused ?? "all") + ")");
        bool short240 = there;
        foreach (string key in keys)
        {
            if (key.StartsWith("reallife_newbuildings_test_", StringComparison.Ordinal) && en != null && en.TryGetValue(key, out string text))
            {
                short240 &= text.Length <= 240 && pt[key].Length <= 240 && zh[key].Length <= 240;
            }
        }
        Check(short240, "new buildings: every notice of the tests is at most 240 characters in each language");
        bool news = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            news &= words != null && words.TryGetValue("reallife_changes_next", out string next) && !next.Contains("Proving Lane");
        }
        Check(news, "new buildings: What's new says nothing of them yet (a player sees nothing of them until a feature puts a place in one)");
    }

    private static readonly Regex NewBuildingsHoles = new Regex(@"\{[a-z]+\}");

    // True when the three texts have the same {holes}.
    private static bool SameHoles(string en, string pt, string zh)
    {
        string a = Holes(NewBuildingsHoles, en);
        return a == Holes(NewBuildingsHoles, pt) && a == Holes(NewBuildingsHoles, zh);
    }
}
