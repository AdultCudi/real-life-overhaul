using System;
using RealLifeOverhaul;

// Where a two-wheeler place stands (Garage/BikeSpotRules.cs): the samples cover its whole footprint and a margin round it,
// the same every time; a place on a crossing, with a point off the pavement or over a kerb is refused, a level place wholly
// on pavement is kept. Then, over the sources, that every place the city gets (docks, racks, stands) is put down through the
// footprint test, stood on the ground it found, and that a bike handed over at a place is never stood on a crossing.
internal static partial class Program
{
    private static void BikeSpots(string scripts)
    {
        // ---- the samples: the middle first, every corner of the footprint plus the margin, all different, the same every time.
        float halfAcross = 1.05f, halfAlong = 0.95f;   // the bicycle rack's (BikePlaces.RackHalf)
        bool same = true, distinct = true, inside = true;
        int corners = 0;
        for (int i = 0; i < BikeSpotRules.Samples; i++)
        {
            BikeSpotRules.Sample(i, halfAcross, halfAlong, out float a, out float b);
            BikeSpotRules.Sample(i, halfAcross, halfAlong, out float a2, out float b2);
            same &= a == a2 && b == b2;
            inside &= Math.Abs(a) <= halfAcross + BikeSpotRules.Margin + 1e-4f && Math.Abs(b) <= halfAlong + BikeSpotRules.Margin + 1e-4f;
            corners += Near(Math.Abs(a), halfAcross + BikeSpotRules.Margin) && Near(Math.Abs(b), halfAlong + BikeSpotRules.Margin) ? 1 : 0;
            for (int j = 0; j < i; j++)
            {
                BikeSpotRules.Sample(j, halfAcross, halfAlong, out float c, out float d);
                distinct &= Math.Abs(a - c) > 0.01f || Math.Abs(b - d) > 0.01f;
            }
        }
        BikeSpotRules.Sample(0, halfAcross, halfAlong, out float midA, out float midB);
        Check(same && distinct && inside && corners == 4 && midA == 0f && midB == 0f,
            "bike places: " + BikeSpotRules.Samples + " points sampled, the middle first and all four corners of the footprint with " + BikeSpotRules.Margin.ToString("F2")
            + " m round it, all different and the same every time");
        bool refused = false;
        try
        {
            BikeSpotRules.Sample(BikeSpotRules.Samples, halfAcross, halfAlong, out _, out _);
        }
        catch (ArgumentOutOfRangeException)
        {
            refused = true;
        }
        Check(refused, "bike places: a sample past the last is refused, never made up");
        Check(BikeSpotRules.Margin >= 0.3f && BikeSpotRules.CrossingMargin >= 0.5f && BikeSpotRules.LevelMost < 0.1f,
            "bike places: the margin keeps a place " + BikeSpotRules.Margin.ToString("F2") + " m from the kerb's edge, " + BikeSpotRules.CrossingMargin.ToString("F1")
            + " m from a crossing, and a rise of " + BikeSpotRules.LevelMost.ToString("F2") + " m (under a kerb's 0.12) is the most under it");

        // ---- the verdicts: the player's screenshot (a rack on a crossing, half over the road) and its kin are refused.
        Check(BikeSpotRules.Judge(0, 10.00f, 10.02f, false) == BikeSpotRules.Refusal.None, "bike places: a level place wholly on pavement is kept");
        Check(BikeSpotRules.Judge(0, 10.00f, 10.00f, true) == BikeSpotRules.Refusal.Crossing, "bike places: a place on a marked crossing is refused, though the crossing is walkable mesh");
        Check(BikeSpotRules.Judge(3, 10.00f, 10.00f, false) == BikeSpotRules.Refusal.OffPavement, "bike places: a place with points in the road, off the pavement, is refused");
        Check(BikeSpotRules.Judge(0, 9.85f, 10.00f, false) == BikeSpotRules.Refusal.OverKerb, "bike places: a place over a kerb (0.15 m between its lowest and highest ground) is refused");
        Check(BikeSpotRules.Judge(9, float.MaxValue, float.MinValue, true) == BikeSpotRules.Refusal.Crossing && BikeSpotRules.Judge(9, float.MaxValue, float.MinValue, false) == BikeSpotRules.Refusal.OffPavement,
            "bike places: with no point found at all it is refused, never taken for level");
        Check(BikeSpotRules.OnPavement(true, 0.1f * 0.1f) && !BikeSpotRules.OnPavement(true, 0.3f * 0.3f) && !BikeSpotRules.OnPavement(false, 0f),
            "bike places: a point is pavement only with the walkable mesh right there, not 0.3 m off");

        // ---- over the sources: every place through the footprint test, stood on its ground.
        string places = Source(scripts, @"Garage\BikePlaces.cs");
        string beside = Body(places, "private static bool Beside(");
        string outside = Body(places, "private static bool Outside(");
        Check(beside.Contains("Grounded(ref spot, facing, DockHalf)") && outside.Contains("Grounded(ref spot, facing, half)"),
            "bike places source: the docks, the racks and the motorbike stands are each put down through the footprint test");
        string grounded = Body(places, "public static bool Grounded(");
        Check(grounded.Contains("BikeSpotRules.Sample(i,") && grounded.Contains("BikeSpotRules.OnPavement(") && grounded.Contains("CitySpots.ClearOfCrossings(")
              && grounded.Contains("BikeSpotRules.Judge(") && grounded.Contains("spot.y = middle"),
            "bike places source: the footprint test samples by the rules, asks for crossings, judges by the rules and stands the place on the ground at its middle");
        Check(Count(places, "Add(BikePlaceKind.") == 4, "bike places source: places are added only where the test was asked (a dock, a rack, a stand at a dealership or a garage)");
        string hand = Body(places, "public static bool HandOverSpot(");
        Check(hand.Contains("CitySpots.ClearOfCrossings("), "bike places source: a bike handed over at a place is never stood on a crossing");
    }
}
