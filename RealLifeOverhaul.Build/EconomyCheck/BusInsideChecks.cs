using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;

// The bus ride seen (Scripts/Transport/BusInside): the places are the clips' measurements laid on the bus (board_step's
// step, pay_tap's reader, sit_bench's seat, strap_hang's strap), every path a rider walks keeps clear of the seats, the
// housings, the cab and the posts and stays inside the walls, the way off is the nearest door from anywhere on the floor,
// the walk along a path ends where it should, the pull-away is shown for a while and never for long, and a ride ended
// early or saved part of the way gives the fare back exactly while it has not happened. The mesh is measured against the
// same numbers by PropAssets/scripts/checks_service.py.
internal static partial class Program
{
    private const float BodyClear = 0.10f;   // a path keeps this far from anything standing on the floor
    private const float SeatClear = 0.05f;   // ...and this far from the seat being sat on, the last steps to it

    private static void BusRide()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        BusPlaces();
        BusPaths();
        BusWayOut();
        BusAlong();
        BusPullAway();
        BusFare();
        BusJourney();
        BusRound();
        BusBoarding();
        BusMiddleDoor();
        BusNetwork();
        BusStopStreet();
        Console.WriteLine("  Bus ride: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static bool Near(float a, float b, float within)
    {
        return Math.Abs(a - b) <= within;
    }

    // Boarding by the middle door: its own validator, the paths from it, the way off again, and which door is taken.
    private static void BusMiddleDoor()
    {
        var lens = new BusInside.Spot(BusInside.MiddleReaderX, BusInside.MiddleReaderZ);
        Check(Near(BusInside.MiddleReader.To(lens), BusInside.ReaderAhead, 0.02f), "bus middle door: its validator is pay_tap's 0.50 m from where the rider stands (" + BusInside.MiddleReader.To(lens).ToString("F3") + ")");
        Check(Near(BusInside.ReaderY - BusInside.Floor, BusInside.ReaderUp, 0.01f), "bus middle door: its lens is at the front one's height, pay_tap's 0.975 m over the floor");
        float facing = BusInside.MiddleReaderFacing;
        Check(Math.Abs(Math.Abs(facing) - 180f) < 20f, "bus middle door: the rider turns back to it, inside the doorway (" + facing.ToString("F0") + " degrees)");
        Check(lens.Z > BusInside.MiddleDoor - 0.6f && lens.Z < BusInside.MiddleDoor && lens.X < BusInside.Wall && lens.X > 0.6f, "bus middle door: the validator stands inside the middle door, behind its middle, at the side");
        Check(BusInside.Clearance(BusInside.MiddleReader) >= BodyClear, "bus middle door: before it the rider stands clear of the poles and the validator (" + BusInside.Clearance(BusInside.MiddleReader).ToString("F2") + ")");
        Check(BusInside.Clearance(BusInside.Exit) >= BodyClear, "bus middle door: those getting off by it still pass clear of its validator (" + BusInside.Clearance(BusInside.Exit).ToString("F2") + ")");
        var path = new List<BusInside.Spot>(12);
        var walks = new List<KeyValuePair<string, BusInside.Place[]>>
        {
            new KeyValuePair<string, BusInside.Place[]>("the middle door's step to its validator", new[] { BusInside.Place.MiddleAboard, BusInside.Place.MiddleReader }),
            new KeyValuePair<string, BusInside.Place[]>("the middle validator to the aisle seat", new[] { BusInside.Place.MiddleReader, BusInside.Place.Seat }),
            new KeyValuePair<string, BusInside.Place[]>("the middle validator to the window seat", new[] { BusInside.Place.MiddleReader, BusInside.Place.WindowSeat }),
            new KeyValuePair<string, BusInside.Place[]>("the middle validator to the strap", new[] { BusInside.Place.MiddleReader, BusInside.Place.Strap })
        };
        foreach (KeyValuePair<string, BusInside.Place[]> walk in walks)
        {
            BusInside.Path(walk.Value[0], walk.Value[1], path);
            BusInside.Spot end = path[path.Count - 1];
            BusInside.Spot want = BusInside.At(walk.Value[1]);
            Check(path.Count >= 2 && end.X == want.X && end.Z == want.Z, "bus middle door: " + walk.Key + ": a path from the one place to the other");
            int seat = walk.Value[1] == BusInside.Place.Seat ? 0 : walk.Value[1] == BusInside.Place.WindowSeat ? 1 : -1;
            Clear("bus middle door: " + walk.Key, path, seat);
            Check(BusInside.Length(path) < 3f, "bus middle door: " + walk.Key + ": a few steps (" + BusInside.Length(path).ToString("F1") + " m), not up the aisle to the front");
        }
        bool front = BusInside.WayOut(BusInside.MiddleReader, path);
        BusInside.Spot last = path[path.Count - 1];
        Check(!front && last.X == BusInside.Exit.X && last.Z == BusInside.Exit.Z, "bus middle door: off again from its validator by the middle door");
        Clear("bus middle door: off again from its validator", path, -1);

        // Which door.
        Check(BusInside.ByMiddleDoor(6f, 2f) && !BusInside.ByMiddleDoor(2f, 6f) && !BusInside.ByMiddleDoor(3f, 2.6f), "bus middle door: the nearer door is taken, the front one when they are about as near");
        Check(BusInside.BoardTriesMiddle[0].Middle && Math.Abs(BusInside.BoardTriesMiddle[0].Along) < 1e-4f, "bus middle door: by the middle door its own place is tried first");
        bool fallback = false;
        foreach (BusInside.Approach a in BusInside.BoardTriesMiddle)
        {
            fallback |= !a.Middle;
        }
        Check(fallback && BusInside.BoardTries[0].Middle == false, "bus middle door: the other door is still tried when the nearer one's place cannot be walked to");
    }

    private static void BusPlaces()
    {
        Check(Near(BusInside.Outside, BusBay.BusSideHalf + 0.30f, 1e-4f), "bus ride: the place to step in from is 0.30 m out from the side (BusVehicle.DoorOut)");
        Check(Near(BusInside.BoardFrom.X, BusInside.Outside, 1e-4f) && Near(BusInside.BoardFrom.Z, BusInside.FrontDoor, 1e-4f), "bus ride: board_step begins on the road at the front door's middle");
        Check(Near(BusInside.BoardFrom.X - BusInside.Aboard.X, BusInside.StepIn, 1e-4f) && Near(BusInside.Aboard.Z, BusInside.FrontDoor, 1e-4f),
              "bus ride: board_step leaves the rider StepIn in, on the floor");
        Check(BusInside.Aboard.X < BusInside.Wall && BusInside.Aboard.X > BusInside.Wall - 0.2f, "bus ride: board_step's end is just inside the door, on the floor");
        Check(Near(BusInside.Floor - BusInside.StepUp, 0.03f, 0.01f), "bus ride: the floor is within 3 cm of where board_step's pose ends (" + BusInside.StepUp + " up)");
        var lens = new BusInside.Spot(BusInside.ReaderX, BusInside.ReaderZ);
        Check(Near(BusInside.Reader.To(lens), BusInside.ReaderAhead, 0.02f), "bus ride: the validator is pay_tap's 0.50 m from where the rider stands (" + BusInside.Reader.To(lens).ToString("F3") + ")");
        Check(Near(BusInside.ReaderY - BusInside.Floor, BusInside.ReaderUp, 0.01f), "bus ride: the validator's lens is pay_tap's 0.975 m over the floor");
        float facing = BusInside.ReaderFacing;
        Check(facing < -30f && facing > -90f, "bus ride: the rider turns to the validator, towards the driver and the front (" + facing.ToString("F0") + " degrees)");
        Check(BusInside.Clearance(BusInside.Reader) >= BodyClear, "bus ride: before the validator the rider stands clear of both door posts and the validator (" + BusInside.Clearance(BusInside.Reader).ToString("F2") + ")");
        var strapFeet = new BusInside.Spot(BusInside.StrapX - BusInside.StrapRight, BusInside.RiderStrap - BusInside.StrapAhead);
        Check(Near(strapFeet.X, BusInside.Strap.X, 1e-4f) && Near(strapFeet.Z, BusInside.Strap.Z, 1e-4f), "bus ride: the standing rider is where strap_hang wants the feet under the strap");
        Check(BusInside.Clearance(BusInside.Strap) >= BodyClear, "bus ride: the standing place is clear of everything (" + BusInside.Clearance(BusInside.Strap).ToString("F2") + ")");
        for (int i = 0; i < BusInside.Seats.Length; i++)
        {
            BusInside.Spot feet = BusInside.SeatFeetAt(i);
            Check(Near(feet.Z - BusInside.Seats[i].Z, BusInside.SeatFeet, 1e-4f) && Near(feet.X, BusInside.Seats[i].X, 1e-4f), "bus ride: seat " + i + ": the feet are sit_bench's 0.30 m ahead of its middle");
            Check(BusInside.Clearance(feet, i) >= SeatClear, "bus ride: seat " + i + ": the feet stand clear of every other seat (" + BusInside.Clearance(feet, i).ToString("F2") + ")");
            Check(BusInside.Furniture[i].Clearance(feet) > 0.05f && BusInside.Furniture[i].Clearance(feet) < 0.15f, "bus ride: seat " + i + ": the feet are just in front of its cushion");
        }
        Check(BusInside.Seats.Length == 2 && BusInside.Seats[0].Z == BusInside.Seats[1].Z && BusInside.Seats[0].X > BusInside.Seats[1].X, "bus ride: the two seats are a pair, the aisle one first");
        Check(BusInside.Exit.Z == BusInside.MiddleDoor && BusInside.ExitTo.Z == BusInside.MiddleDoor && Near(BusInside.ExitTo.X, BusInside.Outside, 1e-4f), "bus ride: off by the middle door, onto the road where one steps in");
        Check(BusInside.FrontExitTo.Z == BusInside.FrontDoor && BusInside.FrontExit.Z == BusInside.FrontDoor, "bus ride: off again by the front door from its platform");
    }

    // Every way a rider is walked keeps a body's width clear of the furniture, and inside the walls.
    private static void BusPaths()
    {
        var path = new List<BusInside.Spot>(12);
        var walks = new List<KeyValuePair<string, BusInside.Place[]>>
        {
            new KeyValuePair<string, BusInside.Place[]>("aboard to the validator", new[] { BusInside.Place.Aboard, BusInside.Place.Reader }),
            new KeyValuePair<string, BusInside.Place[]>("the validator to the aisle seat", new[] { BusInside.Place.Reader, BusInside.Place.Seat }),
            new KeyValuePair<string, BusInside.Place[]>("the validator to the window seat", new[] { BusInside.Place.Reader, BusInside.Place.WindowSeat }),
            new KeyValuePair<string, BusInside.Place[]>("the validator to the strap", new[] { BusInside.Place.Reader, BusInside.Place.Strap })
        };
        foreach (KeyValuePair<string, BusInside.Place[]> walk in walks)
        {
            BusInside.Path(walk.Value[0], walk.Value[1], path);
            Check(path.Count >= 2, "bus ride: " + walk.Key + ": a path");
            BusInside.Spot end = path[path.Count - 1];
            BusInside.Spot want = BusInside.At(walk.Value[1]);
            Check(path[0].X == BusInside.At(walk.Value[0]).X && path[0].Z == BusInside.At(walk.Value[0]).Z && end.X == want.X && end.Z == want.Z, "bus ride: " + walk.Key + ": from the one place to the other");
            int seat = walk.Value[1] == BusInside.Place.Seat ? 0 : walk.Value[1] == BusInside.Place.WindowSeat ? 1 : -1;
            Clear("bus ride: " + walk.Key, path, seat);
            Check(BusInside.Length(path) < 8f, "bus ride: " + walk.Key + ": a few metres (" + BusInside.Length(path).ToString("F1") + ")");
        }
        BusInside.Path(BusInside.Place.Seat, BusInside.Place.Reader, path);
        Check(path.Count == 1, "bus ride: a path that is not one of the ways in is the first place alone");
    }

    // Walking a path: every 2 cm at least BodyClear from the furniture and the walls; within 0.6 m of the seat sat on (the
    // last steps to it, the first off it), SeatClear from that seat.
    private static void Clear(string what, List<BusInside.Spot> path, int seat)
    {
        float length = BusInside.Length(path);
        BusInside.Spot feet = seat >= 0 ? BusInside.SeatFeetAt(seat) : default(BusInside.Spot);
        for (float m = 0f; m <= length + 0.001f; m += 0.02f)
        {
            BusInside.Spot at = BusInside.Along(path, m, out _);
            bool nearSeat = seat >= 0 && at.To(feet) < 0.6f;
            float clear = nearSeat ? BusInside.Clearance(at, seat) : BusInside.Clearance(at);
            float own = nearSeat ? BusInside.Furniture[seat].Clearance(at) : float.MaxValue;
            if (clear < BodyClear || own < SeatClear)
            {
                Check(false, what + ": clear of the furniture all the way (" + Math.Min(clear, own).ToString("F2") + " at " + at.X.ToString("F2") + ", " + at.Z.ToString("F2") + ")");
                return;
            }
        }
        Check(true, what + ": clear of the furniture all the way");
    }

    // The way off from anywhere a rider can be: the nearest door, clear of the furniture, ending at its threshold.
    private static void BusWayOut()
    {
        var path = new List<BusInside.Spot>(12);
        var ways = new List<BusInside.Spot>(8);
        var from = new List<KeyValuePair<string, BusInside.Spot>>
        {
            new KeyValuePair<string, BusInside.Spot>("the aisle seat", BusInside.SeatFeetAt(0)),
            new KeyValuePair<string, BusInside.Spot>("the window seat", BusInside.SeatFeetAt(1)),
            new KeyValuePair<string, BusInside.Spot>("the strap", BusInside.Strap),
            new KeyValuePair<string, BusInside.Spot>("the validator", BusInside.Reader),
            new KeyValuePair<string, BusInside.Spot>("the step", BusInside.Aboard)
        };
        foreach (KeyValuePair<string, BusInside.Spot> start in from)
        {
            bool front = BusInside.WayOut(start.Value, path);
            BusInside.Spot end = path[path.Count - 1];
            bool platform = start.Value.Z > 3.7f;
            Check(front == platform && (front ? end.X == BusInside.FrontExit.X && end.Z == BusInside.FrontExit.Z : end.X == BusInside.Exit.X && end.Z == BusInside.Exit.Z),
                  "bus ride: off from " + start.Key + " by the " + (platform ? "front" : "middle") + " door");
            int seat = start.Key == "the aisle seat" ? 0 : start.Key == "the window seat" ? 1 : -1;
            Clear("bus ride: off from " + start.Key, path, seat);
        }
        // Part of the way down the aisle (a move of the player's own as they walk in): off from every place of the ways in.
        foreach (BusInside.Place to in new[] { BusInside.Place.Seat, BusInside.Place.WindowSeat, BusInside.Place.Strap })
        {
            BusInside.Path(BusInside.Place.Reader, to, ways);
            float length = BusInside.Length(ways);
            int bad = 0;
            for (float m = 0f; m <= length; m += 0.25f)
            {
                BusInside.Spot at = BusInside.Along(ways, m, out _);
                BusInside.WayOut(at, path);
                for (float k = 0.3f; k <= BusInside.Length(path) - 0.3f; k += 0.05f)
                {
                    if (BusInside.Clearance(BusInside.Along(path, k, out _)) < SeatClear)
                    {
                        bad++;
                        break;
                    }
                }
            }
            Check(bad == 0, "bus ride: off from anywhere on the way to the " + to + ", clear of the furniture (" + bad + " places not)");
        }
    }

    private static void BusAlong()
    {
        var path = new List<BusInside.Spot> { new BusInside.Spot(0f, 0f), new BusInside.Spot(0f, 2f), new BusInside.Spot(1f, 2f) };
        BusInside.Spot a = BusInside.Along(path, 0f, out float f0);
        BusInside.Spot b = BusInside.Along(path, 1f, out float f1);
        BusInside.Spot c = BusInside.Along(path, 2.5f, out float f2);
        BusInside.Spot d = BusInside.Along(path, 9f, out _);
        Check(a.X == 0f && a.Z == 0f && Near(f0, 0f, 0.01f), "bus ride: a path begins at its first place, heading along its first leg");
        Check(Near(b.Z, 1f, 1e-4f) && Near(f1, 0f, 0.01f), "bus ride: a metre along the first leg");
        Check(Near(c.X, 0.5f, 1e-4f) && Near(c.Z, 2f, 1e-4f) && Near(f2, 90f, 0.01f), "bus ride: half way along the second leg, heading towards the doors");
        Check(d.X == 1f && d.Z == 2f, "bus ride: past its end, at its end");
        Check(Near(BusInside.Length(path), 3f, 1e-4f), "bus ride: a path's length");
        Check(Near(new BusInside.Spot(0f, 0f).Facing(new BusInside.Spot(-1f, 0f)), -90f, 0.01f), "bus ride: facing away from the doors is -90");
        var box = new BusInside.Box(0f, 1f, 0f, 1f);
        Check(Near(box.Clearance(new BusInside.Spot(2f, 0.5f)), 1f, 1e-4f) && box.Clearance(new BusInside.Spot(0.5f, 0.5f)) < 0f, "bus ride: a box's clearance, outside and in");
    }

    private static void BusPullAway()
    {
        Check(!BusInside.FadeNow(0f, 0f, 0f), "bus ride: nothing fades as the doors close");
        Check(!BusInside.FadeNow(3f, 2f, 10f), "bus ride: the bus is seen pulling away for a while");
        Check(BusInside.FadeNow(5f, 4f, BusInside.ShowMetres), "bus ride: once it has gone 30 m the time of the ride passes");
        Check(BusInside.FadeNow(9f, BusInside.ShowMoving, 20f), "bus ride: or after 7 s on the move");
        Check(BusInside.FadeNow(BusInside.ShowStill, 0f, 0f), "bus ride: held in its bay with the doors shut, 8 s is enough");
        Check(!BusInside.FadeNow(BusInside.ShowStill - 0.5f, 0f, 0f), "bus ride: ...and not before");
        Check(BusInside.FadeNow(BusInside.ShowMost, 0.5f, 1f), "bus ride: never longer than 16 s after the doors shut, whatever holds it");
        float most = 0f;
        for (float shut = 0f; shut < 30f; shut += 0.25f)
        {
            if (!BusInside.FadeNow(shut, Math.Max(0f, shut - 3f), Math.Max(0f, shut - 3f) * 0.4f))
            {
                most = shut;
            }
        }
        Check(most < BusInside.ShowMost, "bus ride: a bus crawling at walking pace is shown " + most.ToString("F1") + " s at most");
    }

    private static void BusFare()
    {
        var back = new Dictionary<BusInside.Stage, bool>
        {
            { BusInside.Stage.None, false }, { BusInside.Stage.Walking, false }, { BusInside.Stage.Boarding, true }, { BusInside.Stage.Aboard, true },
            { BusInside.Stage.Leaving, false }, { BusInside.Stage.Away, false }, { BusInside.Stage.Arriving, false }
        };
        foreach (KeyValuePair<BusInside.Stage, bool> stage in back)
        {
            Check(BusInside.FareBackOnCancel(stage.Key) == stage.Value, "bus ride: ended early at " + stage.Key + ", the fare " + (stage.Value ? "is" : "is not") + " given back");
        }
        var start = new Dictionary<BusInside.Stage, bool>
        {
            { BusInside.Stage.Boarding, true }, { BusInside.Stage.Aboard, true }, { BusInside.Stage.Leaving, true }, { BusInside.Stage.Away, true }, { BusInside.Stage.Arriving, false }
        };
        foreach (KeyValuePair<BusInside.Stage, bool> stage in start)
        {
            Check(BusInside.SaveAtStart(stage.Key) == stage.Value, "bus ride: saved at " + stage.Key + ", the save has the player at the stop " + (stage.Value ? "they boarded at" : "they get off at"));
        }
    }
}
