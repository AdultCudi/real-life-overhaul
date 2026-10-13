using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The bus ride through the city in real time (Scripts/Transport/RideRules, TransitDirector.Journey): the bar's figure, the
// stop a rider who asks to get off is let off at, the time limit after which a bus that gets nowhere is given up, what a
// save on the way gives back, and how long a ride takes as the bus drives it. And the way onto the bus and off it when
// something stands by a door (BusInside: the places tried in turn, the middle door last, the ways inside to and from it),
// and the boarding strip cleared of street furniture at every stop.
internal static partial class Program
{
    private static void BusJourney()
    {
        // The bar.
        Check(Near(RideRules.Progress(100f, 1100f, 100f), 0f, 1e-4f) && Near(RideRules.Progress(100f, 1100f, 600f), 0.5f, 1e-4f) && Near(RideRules.Progress(100f, 1100f, 1100f), 1f, 1e-4f),
              "bus journey: the bar is the part of the line ridden, from the stop boarded at to the one got off at");
        Check(RideRules.Progress(100f, 1100f, 50f) == 0f && RideRules.Progress(100f, 1100f, 1500f) == 1f && RideRules.Progress(500f, 500f, 0f) == 1f, "bus journey: the bar stays between empty and full");
        Check(Near(RideRules.AtMetres(800f, 120f, true, false, 500f), 680f, 1e-3f), "bus journey: driving to a stop, it is short of it by what is still to drive");
        Check(Near(RideRules.AtMetres(800f, 900f, true, false, 500f), 500f, 1e-3f), "bus journey: a route longer than the line (a bay's curve) never puts it behind the stop it left");
        Check(Near(RideRules.AtMetres(800f, 0f, false, true, 500f), 800f, 1e-3f) && Near(RideRules.AtMetres(800f, 50f, false, false, 500f), 500f, 1e-3f),
              "bus journey: standing at a stop it is there; leaving one it is at the one it left");
        Check(RideRules.Advance(0.4f, 0.3f) == 0.4f && RideRules.Advance(0.4f, 0.6f) == 0.6f && RideRules.Advance(0.4f, 2f) == 1f, "bus journey: the bar never goes back");

        // Getting off early: the next stop it calls at, never past the ticket's.
        Check(RideRules.GetOffAt(2, 3, false, false, 6) == 3, "bus journey: driving to a stop, off at that stop");
        Check(RideRules.GetOffAt(3, 3, false, false, 6) == 4, "bus journey: just left a stop, off at the one after");
        Check(RideRules.GetOffAt(2, 3, true, true, 6) == 3, "bus journey: standing at a stop with its doors open, off there");
        Check(RideRules.GetOffAt(2, 3, true, false, 6) == 4, "bus journey: standing at a stop with its doors shut, off at the next");
        Check(RideRules.GetOffAt(5, 5, false, false, 6) == 6 && RideRules.GetOffAt(5, 6, false, false, 6) == 6, "bus journey: never past the stop the ticket was for");
        Check(RideRules.StopsLeft(3, 6) == 4 && RideRules.StopsLeft(6, 6) == 1 && RideRules.StopsLeft(7, 6) == 0, "bus journey: stops to come, the one got off at included");

        // The time limit.
        Check(!RideRules.Stuck(RideRules.StuckDriving - 1f, false, 100f) && RideRules.Stuck(RideRules.StuckDriving, false, 100f), "bus journey: on the road, given up after a minute without a metre more");
        Check(!RideRules.Stuck(RideRules.StuckDriving + 10f, true, 100f) && RideRules.Stuck(RideRules.StuckAny, true, 100f), "bus journey: at a stop it may stand longer, two minutes at the most");
        Check(RideRules.StuckAny > 34f + 30f && RideRules.StuckAny > 45f, "bus journey: longer than the longest stand and the longest wait to pull out of a bay");
        Check(RideRules.Stuck(0f, false, RideRules.LongestRide), "bus journey: a whole ride is given up after 15 minutes, however it is getting on");
        Check(RideRules.StuckDriving > 45f, "bus journey: longer than a red light nobody is looking at is waited at (45 s)");
        Check(Near(RideRules.MinutesLeft(40f, 0.25f), 30f, 1e-3f) && RideRules.MinutesLeft(40f, 1f) == 1f && RideRules.MinutesLeft(40f, -1f) == 40f,
              "bus journey: skipped part of the way, the part not yet ridden of the ride's time, a minute at the least");

        // A save on the way.
        Check(BusInside.SaveAtStart(BusInside.Stage.Riding), "bus journey: saved while riding, at a stop the bus has been at, never on the bus");
        Check(BusInside.SaveFareBack(BusInside.Stage.Riding, 0) && !BusInside.SaveFareBack(BusInside.Stage.Riding, 1) && !BusInside.SaveFareBack(BusInside.Stage.Away, 2),
              "bus journey: the fare comes back only while the bus has called at no stop");
        Check(BusInside.SaveFareBack(BusInside.Stage.Leaving, 0) && !BusInside.SaveFareBack(BusInside.Stage.Arriving, 0), "bus journey: a save as the bus pulls away gives the fare back, as before");
        Check(!BusInside.FareBackOnCancel(BusInside.Stage.Riding), "bus journey: ended early on the way, nothing is given back");

        // How long a ride takes, as the bus drives it.
        float hop = RideRules.DriveSeconds(RideRules.StopApart);
        Check(hop > RideRules.StopApart / RideRules.Cruise && hop < RideRules.StopApart / RideRules.Cruise + 25f, "bus journey: one stop to the next, " + RideRules.StopApart.ToString("F0") + " m, in " + hop.ToString("F1") + " s");
        float one = RideRules.DriveSeconds(250f), three = RideRules.RideSeconds(3), five = RideRules.RideSeconds(5);
        Check(three > 3f * hop && three < 3f * hop + 3f * RideRules.StandUsual, "bus journey: three stops with two stands on the way");
        Console.WriteLine("  Bus journey, as the bus drives it (no lights, no traffic): " + RideRules.StopApart.ToString("F0") + " m to the next stop " + hop.ToString("F0") + " s, 250 m " + one.ToString("F0")
                          + " s; three stops " + three.ToString("F0") + " s (" + (three / RideRules.FastForward).ToString("F0") + " s fast-forwarded); five stops " + five.ToString("F0") + " s ("
                          + (five / RideRules.FastForward).ToString("F0") + " s fast-forwarded)");
    }

    private static void BusBoarding()
    {
        // The places tried, in order: the front door's own, a little along the kerb, then the middle door.
        BusInside.Approach[] tries = BusInside.BoardTries;
        Check(!tries[0].Middle && tries[0].Along == 0f, "bus boarding: the front door's own place is tried first");
        int firstMiddle = Array.FindIndex(tries, a => a.Middle);
        Check(firstMiddle > 0 && Array.TrueForAll(tries, a => a.Middle == (Array.IndexOf(tries, a) >= firstMiddle)), "bus boarding: every place by the front door before any by the middle door");
        Check(Array.TrueForAll(tries, a => Math.Abs(a.Along) <= BusInside.AlongMost + 1e-4f), "bus boarding: never more than a metre along the side from a door");
        bool nearerFirst = true;
        for (int i = 1; i < firstMiddle; i++)
        {
            nearerFirst &= Math.Abs(tries[i].Along) >= Math.Abs(tries[i - 1].Along);
        }
        Check(nearerFirst, "bus boarding: along the kerb, the nearer places first");
        Check(BusInside.First(tries, a => true) == 0 && BusInside.First(tries, a => false) == -1, "bus boarding: the first that will do, or none");
        Check(BusInside.First(tries, a => a.Middle) == firstMiddle, "bus boarding: with the front door out of reach, the middle door");
        Check(BusInside.First(tries, a => Math.Abs(a.Along) > 0.6f && !a.Middle) == 3, "bus boarding: with the near places blocked, a metre along");
        BusInside.Spot front = tries[0].At, middle = tries[firstMiddle].At;
        Check(front.X == BusInside.BoardFrom.X && front.Z == BusInside.BoardFrom.Z && middle.X == BusInside.MiddleBoardFrom.X && middle.Z == BusInside.MiddleBoardFrom.Z,
              "bus boarding: the doors' own places are where board_step begins");
        Check(Near(BusInside.MiddleBoardFrom.X - BusInside.MiddleAboard.X, BusInside.StepIn, 1e-4f) && BusInside.MiddleAboard.Z == BusInside.MiddleDoor, "bus boarding: board_step at the middle door leaves the rider StepIn in, on the floor");
        Check(BusInside.OffTries[0].Middle && BusInside.OffTries[0].Along == 0f && !BusInside.OffTriesFront[0].Middle, "bus boarding: off by the nearer door first");
        Check(Array.Exists(BusInside.OffTries, a => !a.Middle) && Array.Exists(BusInside.OffTriesFront, a => a.Middle), "bus boarding: off by the other door when the nearer cannot be stepped down from");

        // In by the middle door: up the aisle to the validator, clear of everything.
        var path = new List<BusInside.Spot>(12);
        BusInside.Path(BusInside.Place.MiddleAboard, BusInside.Place.Reader, path);
        BusInside.Spot end = path[path.Count - 1];
        Check(path.Count > 2 && end.X == BusInside.Reader.X && end.Z == BusInside.Reader.Z, "bus boarding: from the middle door to the validator");
        Clear("bus boarding: the middle door to the validator", path, -1);
        Check(BusInside.Length(path) < 8f, "bus boarding: a few metres up the aisle (" + BusInside.Length(path).ToString("F1") + ")");

        // Off by the other door.
        var starts = new List<KeyValuePair<string, BusInside.Spot>>
        {
            new KeyValuePair<string, BusInside.Spot>("the aisle seat", BusInside.SeatFeetAt(0)),
            new KeyValuePair<string, BusInside.Spot>("the window seat", BusInside.SeatFeetAt(1)),
            new KeyValuePair<string, BusInside.Spot>("the strap", BusInside.Strap),
            new KeyValuePair<string, BusInside.Spot>("half way up the aisle", new BusInside.Spot(0.15f, 1.8f)),
            new KeyValuePair<string, BusInside.Spot>("the validator", BusInside.Reader),
            new KeyValuePair<string, BusInside.Spot>("the middle door's step", BusInside.MiddleAboard)
        };
        foreach (KeyValuePair<string, BusInside.Spot> start in starts)
        {
            foreach (bool byFront in new[] { true, false })
            {
                BusInside.WayOutBy(start.Value, byFront, path);
                BusInside.Spot last = path[path.Count - 1];
                BusInside.Spot want = byFront ? BusInside.FrontExit : BusInside.Exit;
                Check(last.X == want.X && last.Z == want.Z && path[0].X == start.Value.X && path[0].Z == start.Value.Z, "bus boarding: off from " + start.Key + " by the " + (byFront ? "front" : "middle") + " door");
                int seat = start.Key == "the aisle seat" ? 0 : start.Key == "the window seat" ? 1 : -1;
                Clear("bus boarding: off from " + start.Key + " by the " + (byFront ? "front" : "middle") + " door", path, seat);
            }
        }

        // The boarding strip: the kerb by both doors and a walkway 1.5 m wide to each from the waiting place.
        const float kerb = 3.4f;
        const float waitAlong = -1.6f, waitAside = kerb + 1.25f;
        BusInside.Strip[] strips = BusInside.BoardingStrips(kerb, waitAlong, waitAside);
        Check(strips.Length == 3 && Near(strips[1].Half * 2f, 1.5f, 1e-4f) && Near(strips[2].Half * 2f, 1.5f, 1e-4f), "bus boarding strip: walkways 1.5 m wide");
        Check(BusInside.InStrips(strips, BusInside.FrontDoor, kerb) && BusInside.InStrips(strips, BusInside.MiddleDoor, kerb), "bus boarding strip: the kerb at both doors");
        Check(BusInside.InStrips(strips, BusInside.FrontDoor + BusInside.KerbBeyond - 0.05f, kerb + 0.3f) && BusInside.InStrips(strips, BusInside.MiddleDoor - BusInside.KerbBeyond + 0.05f, kerb - 0.3f),
              "bus boarding strip: the kerb a metre beyond each door, astride its edge");
        Check(BusInside.InStrips(strips, 1.5f, kerb + 0.9f), "bus boarding strip: the pavement between the doors, on the way to them");
        Check(BusInside.InStrips(strips, waitAlong, waitAside) && BusInside.InStrips(strips, (waitAlong + BusInside.FrontDoor) * 0.5f, (waitAside + kerb) * 0.5f + 0.7f),
              "bus boarding strip: the waiting place, and the way from it to the front door, 0.7 m to the side");
        Check(!BusInside.InStrips(strips, (waitAlong + BusInside.FrontDoor) * 0.5f, (waitAside + kerb) * 0.5f + 1.2f), "bus boarding strip: no wider than that");
        Check(!BusInside.InStrips(strips, BusInside.FrontDoor + 2.5f, kerb) && !BusInside.InStrips(strips, BusInside.MiddleDoor - 2.5f, kerb), "bus boarding strip: the kerb not cleared far beyond the doors");
        Check(!BusInside.InStrips(strips, 1.5f, kerb + 3.5f) && !BusInside.InStrips(strips, 1.5f, kerb - 1.5f), "bus boarding strip: nothing at the back of the pavement or out in the road");
    }
}
