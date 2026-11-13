using System.Collections.Generic;
using RealLifeOverhaul;

// A player's motorbike stand stood on the forecourt of the gas station at 47 5th Avenue, in the lane to its two repair bays (13 Nov 2026):
// the pavement test passes a whole forecourt, so a place also keeps out of every lane a vehicle drives or parks in. Offline: --bike-lanes.
internal static partial class Program
{
    private static void BikeLanes()
    {
        // A station modelled on the picture: two repair bays 3 m wide and 6 m deep against the back wall (z 0 to 6, drive-in along z),
        // a pump island, a garage door, the building's front wall at z 0 and the road at z 30.
        var lanes = new List<BikeLaneRules.Lane>();
        BikeLaneRules.Approach(-2.5f, 3f, 1.5f, 3f, lanes);
        BikeLaneRules.Approach(2.5f, 3f, 1.5f, 3f, lanes);
        BikeLaneRules.Approach(12f, 14f, 1f, 3f, lanes);   // a pump's lane
        lanes.Add(BikeLaneRules.Disc(0f, 0.5f, BikeLaneRules.DiscRadius));   // the garage door
        float stand = BikeLaneRules.Radius(1.35f, 1.35f);

        Check(BikeLaneRules.InTheWay(lanes, 0f, 10f, stand), "bike lanes: a spot in the corridor four metres in front of the bays is refused, as the player's stand was");
        Check(BikeLaneRules.InTheWay(lanes, -2.5f, 8f, stand), "bike lanes: a spot straight out from a bay, 5 m beyond its trigger, is refused");
        Check(BikeLaneRules.InTheWay(lanes, -2.5f, 3f, stand) && BikeLaneRules.InTheWay(lanes, 2.5f, 3f, stand), "bike lanes: a spot on a bay's trigger itself is refused");
        Check(BikeLaneRules.InTheWay(lanes, 0f, 20f, stand), "bike lanes: the corridor runs out 14 m from the trigger's end, so a spot at the road's edge in line with the bays is refused");
        Check(!BikeLaneRules.InTheWay(lanes, 0f, 36f, stand), "bike lanes: well beyond the corridor's end (past the road) a spot is not refused for it");
        Check(!BikeLaneRules.InTheWay(lanes, -12f, 2f, stand) && !BikeLaneRules.InTheWay(lanes, 18f, -3f, stand), "bike lanes: a spot against the wall beside the bays, off to either side, is accepted");
        Check(BikeLaneRules.InTheWay(lanes, 12f, 10f, stand), "bike lanes: the lane to a pump is refused");
        Check(BikeLaneRules.InTheWay(lanes, 0f, -2.5f, stand), "bike lanes: a spot at the garage door (a disc round it) is refused");

        // The margin: a footprint's edge touching a lane's edge is refused, a hand's breadth clear of it is not.
        var one = new List<BikeLaneRules.Lane>();
        BikeLaneRules.Approach(0f, 0f, 1.5f, 3f, one);   // lane half width 2.4 m (1.5 + 0.9) along z
        float edge = 2.4f + stand;
        Check(BikeLaneRules.InTheWay(one, edge - 0.05f, 0f, stand) && !BikeLaneRules.InTheWay(one, edge + 0.05f, 0f, stand),
            "bike lanes: a place is refused when its footprint (with " + BikeLaneRules.Margin.ToString("F1") + " m of room) reaches the lane and kept when it just clears");
        Check(BikeLaneRules.Radius(1.35f, 1.35f) > 1.35f * 1.414f && BikeLaneRules.Radius(1.35f, 1.35f) < 3f, "bike lanes: a stand's radius holds its whole corner plus the margin, about 2.4 m");

        // A trigger's long side is the way in: along x for a wide box, along z for a deep one, both for a square one.
        var wide = new List<BikeLaneRules.Lane>();
        BikeLaneRules.Approach(0f, 0f, 4f, 1.5f, wide);
        var deep = new List<BikeLaneRules.Lane>();
        BikeLaneRules.Approach(0f, 0f, 1.5f, 4f, deep);
        var square = new List<BikeLaneRules.Lane>();
        BikeLaneRules.Approach(0f, 0f, 2f, 2f, square);
        Check(wide.Count == 1 && deep.Count == 1 && square.Count == 2, "bike lanes: a wide or deep trigger has one lane, a square one two");
        Check(BikeLaneRules.InTheWay(wide, 15f, 0f, 1f) && !BikeLaneRules.InTheWay(wide, 0f, 15f, 1f), "bike lanes: a wide trigger's lane runs along x, not z");
        Check(BikeLaneRules.InTheWay(deep, 0f, 15f, 1f) && !BikeLaneRules.InTheWay(deep, 15f, 0f, 1f), "bike lanes: a deep trigger's lane runs along z, not x");
        Check(BikeLaneRules.InTheWay(square, 15f, 0f, 1f) && BikeLaneRules.InTheWay(square, 0f, 15f, 1f), "bike lanes: a square trigger's lane runs both ways");

        // No lanes known (a city without gas stations) refuses nothing; the same answer every time.
        Check(!BikeLaneRules.InTheWay(new List<BikeLaneRules.Lane>(), 1f, 1f, stand), "bike lanes: with no lane known nothing is refused");
        Check(BikeLaneRules.Distance(BikeLaneRules.Disc(3f, 4f, 1f), 0f, 0f) == 5f && BikeLaneRules.Distance(one[0], 0f, 100f) > 80f, "bike lanes: the distance is to the lane's nearest point, a disc's centre for a disc");

        // Over the sources.
        string places = ModSource(@"Garage\BikePlaces.cs");
        int a = places.IndexOf("private static bool Beside(", System.StringComparison.Ordinal);
        int b = places.IndexOf("private static bool Outside(", System.StringComparison.Ordinal);
        string beside = a >= 0 && b > a ? places.Substring(a, b - a) : string.Empty;
        string outside = b >= 0 ? places.Substring(b, places.IndexOf("public static bool InTheWay(", b, System.StringComparison.Ordinal) - b) : string.Empty;
        Check(beside.Contains("!InTheWay(spot, facing, DockHalf)") && outside.Contains("!InTheWay(spot, facing, half)"),
            "bike lanes source: a dock, a rack and a stand each ask whether a vehicle's way runs through the spot before the footprint test");
        Check(places.Contains("repairStationTriggers") && places.Contains("gasStationTriggers") && places.Contains("gasStationRepairGarageDoor") && places.Contains("Displays.Add("),
            "bike lanes source: the lanes come from the repair bays, the pumps, the garage door and the dealerships' display spots");
        Check(places.Contains("\"AutoParkSpots\", \"ParkingArea\"") && places.Contains("QueryTriggerInteraction.Collide"), "bike lanes source: the game's parking zones count as in the way");
        Check(places.Contains("BikeLaneRules.InTheWay(Lanes, spot.x, spot.z, BikeLaneRules.Radius(BikeHalf.x, BikeHalf.z)"), "bike lanes source: the spot a hired or bought bike is handed over at keeps out of the lanes too");
        Check(places.Contains("as in a vehicle's way"), "bike lanes source: the load's log line says how many spots were refused as in a vehicle's way");
        Check(!places.Contains("AddComponent<") && !ModSource(@"Core\Shapes.cs").Contains("AddComponent<BoxCollider") && !ModSource(@"Core\Shapes.cs").Contains("AddComponent<MeshCollider"),
            "bike lanes source: the dressing of a place still has no collider; only a handed-over bike (the game's scooter) is solid");
        string state = ModSource(@"Garage\GarageState.cs");
        Check(!state.Contains("BikePlace") && !state.Contains("Dressing"), "bike lanes source: nothing in the save points at a place, so the places are worked out afresh at each load");
    }
}
