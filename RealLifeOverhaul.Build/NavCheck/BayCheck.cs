using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// Checks for the bus bays (Transport/BusBay.cs and Transport/BusPath.cs, compiled as they ship): the arithmetic of where
// the bus stands, the curves, the zone round a bay, and the mod's own bus driven in and out along them.
//
// THE MODEL is the bus as the game drives it, by the code that drives it there (BusPath): its front axle along a spline
// through the route's points, its pace (each bend's limit planned back from it, every stop planned at the braking
// passengers keep their feet at, a pull that eases in), and its rear axle drawn after the front at the wheelbase. What
// BusDriver adds in the game (the physics step, the road's height, what is in its way) is not modelled, and nothing is in
// the way here. TransitBus's part is written out again (that file needs the game's engine): the route it lays into a bay
// (the curve in for the front axle, the run along the bay's line while the rear comes in, the standing place and the
// point beyond it) and out of one (the curve out from where the bus really stands, then the lane), and its pace: down to
// the bay's pace at 1.5 m/s2 before the path begins, out of the bay gently from rest.
// Places are metres along the lane from the pole (ahead positive) and aside of the lane's line (towards the kerb positive).
internal static partial class Program
{
    private const float Cruise = 11f;          // TransitBus.Cruise
    private const float BayBraking = 1.5f;     // TransitBus.BayBraking
    private const float BodySide = BusBay.BusSideHalf;
    private const float BodyHalf = BusBay.BusLength * 0.5f;

    private struct Bus
    {
        public float Fx, Fz;    // its front axle (along, aside)
        public float Rx, Rz;    // its rear axle
        public float Speed;
        public float Heading => (float)Math.Atan2(Fz - Rz, Fx - Rx);   // radians, towards the kerb positive
        public float MiddleX => (Fx + Rx) * 0.5f;
        public float MiddleZ => (Fz + Rz) * 0.5f;
    }

    private static Bus Standing(float along, float aside, float degrees)
    {
        float h = degrees * (float)Math.PI / 180f;
        float c = (float)Math.Cos(h), s = (float)Math.Sin(h);
        // (along and aside are its middle's; the axles are BusAxleFront either side of it)
        return new Bus { Fx = along + c * BusBay.BusAxleFront, Fz = aside + s * BusBay.BusAxleFront, Rx = along - c * BusBay.BusAxleFront, Rz = aside - s * BusBay.BusAxleFront };
    }

    // What the body reaches, for a pose: the furthest any corner is towards the kerb and towards the far side, and from
    // where to where along the lane its kerb side lies beyond the lane's edge (stripBack above stripFront when none does).
    private static void Corners(Bus bus, out float kerbMost, out float laneLeast, out float stripBack, out float stripFront)
    {
        float hx = bus.Fx - bus.Rx, hz = bus.Fz - bus.Rz;
        float d = (float)Math.Sqrt(hx * hx + hz * hz);
        hx /= d;
        hz /= d;
        float rx = -hz, rz = hx;   // to its right, towards the kerb
        kerbMost = float.MinValue;
        laneLeast = float.MaxValue;
        for (int end = 0; end < 2; end++)
        {
            float ahead = end == 0 ? BusBay.BusNoseAhead : -BusBay.BusTailBehind;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = bus.Fz + hz * ahead + rz * side * BodySide;
                kerbMost = Math.Max(kerbMost, z);
                laneLeast = Math.Min(laneLeast, z);
            }
        }
        float backX = bus.Fx - hx * BusBay.BusTailBehind + rx * BodySide, backZ = bus.Fz - hz * BusBay.BusTailBehind + rz * BodySide;
        float frontX = bus.Fx + hx * BusBay.BusNoseAhead + rx * BodySide, frontZ = bus.Fz + hz * BusBay.BusNoseAhead + rz * BodySide;
        float edge = BusBay.LaneHalf;
        stripBack = float.MaxValue;
        stripFront = float.MinValue;
        if (backZ <= edge && frontZ <= edge)
        {
            return;
        }
        float crossX = Math.Abs(frontZ - backZ) > 0.0001f ? backX + (edge - backZ) / (frontZ - backZ) * (frontX - backX) : backX;
        stripBack = backZ > edge ? backX : crossX;
        stripFront = frontZ > edge ? frontX : crossX;
    }

    private sealed class Drive
    {
        public readonly List<float> X = new List<float>(), Z = new List<float>();
        public int FirstOfBay;          // in: the first point of the bay's path; out: the first of the lane again
        public float BayRun;            // in: metres of route from the first point of the bay's path to its end
        public bool Leaving;
        public float Step = 0.02f;
        public float BaySpeed = BusBay.Speed;

        // What the bus reckons the route will take from where it is (TransitBus.SecondsLeft: BusPath.SecondsLeft at its
        // cruise, the bay's pace over the bay's path): what the stop's panel counts down.
        public float Reckoned(Bus bus)
        {
            var path = new BusPath();
            var heights = new List<float>(X.Count);
            for (int i = 0; i < X.Count; i++)
            {
                heights.Add(0f);
            }
            path.Build(X, heights, Z, X.Count, bus.Fx, 0f, bus.Fz, bus.Fx - bus.Rx, bus.Fz - bus.Rz);
            return path.SecondsLeft(0f, bus.Speed, Cruise, Leaving || BayRun <= 0f ? float.MaxValue : path.Total - BayRun, BaySpeed);
        }

        // Runs the model to the end of the route (or for so many seconds). Calls back with every pose.
        public Bus Run(Bus bus, float seconds, Action<Bus> each)
        {
            var path = new BusPath();
            var heights = new List<float>(X.Count);
            for (int i = 0; i < X.Count; i++)
            {
                heights.Add(0f);
            }
            path.Build(X, heights, Z, X.Count, bus.Fx, 0f, bus.Fz, bus.Fx - bus.Rx, bus.Fz - bus.Rz);
            float s = 0f, pull = 0f, time = 0f, slowTick = 0f;
            float cruise = Leaving ? 1.5f : Cruise;
            while (time < seconds)
            {
                // TransitBus.Drive, four times a second: the pace.
                if (time >= slowTick)
                {
                    slowTick = time + 0.25f;
                    if (!Leaving)
                    {
                        float lead = path.End - s - BayRun;
                        cruise = Math.Min(Cruise, (float)Math.Sqrt(BaySpeed * BaySpeed + 2f * BayBraking * Math.Max(0f, lead)));
                    }
                    else
                    {
                        cruise = path.NextPoint(s) >= FirstOfBay ? Cruise : Math.Min(BaySpeed, 1.5f + 1.5f * time);
                    }
                }
                float room = path.End - s;
                path.At(s, out _, out _, out _, out _, out _);
                float wanted = Math.Min(cruise, Math.Min(path.LimitHere, BusPath.StopIn(room)));
                bus.Speed = BusPath.Pace(bus.Speed, ref pull, wanted, Step);
                float moved = Math.Min(bus.Speed * Step, Math.Max(0f, room));
                s += moved;
                path.At(s, out float fx, out _, out float fz, out _, out _);
                bus.Fx = fx;
                bus.Fz = fz;
                BusPath.Trail(fx, fz, ref bus.Rx, ref bus.Rz, BusBay.BusWheelbase);
                time += Step;
                each?.Invoke(bus);
                if (path.End - s <= 0.02f && bus.Speed < 0.25f)
                {
                    bus.Speed = 0f;
                    break;
                }
            }
            return bus;
        }
    }

    // The route TransitBus.RouteToBay lays for the front axle: lane points 5 m apart, the curve in, the run along the bay's
    // line, the standing place and the point beyond it.
    private static Drive RouteIn(float offset, out BusBay.Curve curve)
    {
        curve = BusBay.Approach(offset);
        int points = curve.Points;
        var drive = new Drive();
        float rest = -BusBay.BusMiddleAhead;
        float leaves = points * BusBay.Step + BusBay.Settle + BusBay.BusMiddleAhead + 0.5f;
        for (float before = 80f; before > leaves; before -= 5f)
        {
            drive.X.Add(-before);
            drive.Z.Add(0f);
        }
        drive.FirstOfBay = drive.X.Count;
        for (int k = points; k >= 1; k--)
        {
            float d = k * BusBay.Step;
            drive.X.Add(rest - d - BusBay.Settle);
            drive.Z.Add(offset - curve.Aside(d));
        }
        for (float along = BusBay.Settle; along > 0.01f; along -= BusBay.Step)
        {
            drive.X.Add(rest - along);
            drive.Z.Add(offset);
        }
        drive.X.Add(rest);
        drive.Z.Add(offset);
        drive.X.Add(rest + BusPath.FinalShort);
        drive.Z.Add(offset);
        drive.BayRun = points * BusBay.Step + BusBay.Settle + BusPath.FinalShort;
        return drive;
    }

    // The route TransitBus.PullOut lays from where the front axle stands: the curve out, then lane points 5 m apart.
    private static Drive RouteOut(Bus stands, out BusBay.Curve curve)
    {
        curve = BusBay.Out(stands.Fz, BusBay.BusLeave, BusBay.RunMost - Math.Max(0f, stands.Fx));
        int points = curve.Points;
        var drive = new Drive { Leaving = true };
        for (int k = 1; k <= points; k++)
        {
            float gone = k * BusBay.Step;
            drive.X.Add(stands.Fx + gone);
            drive.Z.Add(stands.Fz - curve.Aside(gone));
        }
        drive.FirstOfBay = drive.X.Count;
        float rejoins = stands.Fx + points * BusBay.Step + 0.5f;
        for (float on = (float)Math.Ceiling(rejoins / 5f) * 5f + 0.01f; on < 90f; on += 5f)
        {
            drive.X.Add(on);
            drive.Z.Add(0f);
        }
        return drive;
    }

    // A bay's zone: what is left of the game's parking beside it, and the broken line painted round it.
    private static void Zones()
    {
        // A zone from 10 m to 47 m along the lane, and runs of parking that reach into it in every way there is.
        Check(!BusBay.Keep(-30f, 10f, 10f, 47f, out _, out _, out _) && !BusBay.Keep(47f, 90f, 10f, 47f, out _, out _, out _),
            "a run of parking that only touches the zone is cut");
        Check(BusBay.Keep(30f, 120f, 10f, 47f, out float k0, out float k1, out float lost) && Near(k0, 47f) && Near(k1, 120f) && lost == 0f,
            "a run that begins in the zone does not keep its part after it (" + k0 + " to " + k1 + ", " + lost + " lost)");
        Check(BusBay.Keep(-40f, 25f, 10f, 47f, out k0, out k1, out lost) && Near(k0, -40f) && Near(k1, 10f) && lost == 0f,
            "a run that ends in the zone does not keep its part before it (" + k0 + " to " + k1 + ", " + lost + " lost)");
        Check(BusBay.Keep(12f, 40f, 10f, 47f, out k0, out k1, out lost) && Near(k0, 47f) && Near(k1, 47f) && lost == 0f,
            "a run wholly in the zone keeps some of it (" + k0 + " to " + k1 + ")");
        Check(BusBay.Keep(0f, 150f, 10f, 47f, out k0, out k1, out lost) && Near(k0, 47f) && Near(k1, 150f) && Near(lost, 10f),
            "a zone in the middle of a run does not keep the longer part and give up the shorter (" + k0 + " to " + k1 + ", " + lost + " lost)");
        Check(BusBay.Keep(-100f, 55f, 10f, 47f, out k0, out k1, out lost) && Near(k0, -100f) && Near(k1, 10f) && Near(lost, 8f),
            "a zone near the end of a run does not give up the short part beyond it (" + k0 + " to " + k1 + ", " + lost + " lost)");

        // The broken line: a dash of 1 m and a gap of 0.5 m, beginning and ending with paint, for any length.
        const float repeat = 1.5f;
        const float paint = 2f / 3f;
        foreach (float length in new[] { 0.4f, 1f, 6f, 6.5f, 20.3f, 37f, 51.2f })
        {
            float dashes = BusBay.Dashes(length, repeat, paint);
            float whole = dashes - paint;
            Check(Math.Abs(whole - Math.Round(whole)) < 0.001f && whole >= 0f, "a broken line " + length + " m long does not begin and end with paint (" + dashes + " patterns)");
            Check(length < repeat || Math.Abs(length / dashes - repeat) <= repeat * 0.25f, "a broken line " + length + " m long has its pattern stretched to " + (length / dashes) + " m");
        }
    }

    private static void Bays(bool print)
    {
        // Where the bus stands: its door side KerbGap from the kerb, its side on the lane's edge at the narrowest bay.
        float needed = BusBay.KerbNeeded(BodySide);
        Check(Near(needed, 4.20f), "a bay wants the kerb 4.20 m from the lane's line for the 2.40 m bus, not " + needed);
        Check(BusBay.Offset(needed - 0.02f, BodySide) == 0f, "a kerb 2 cm too near still gives a bay");
        Check(Near(BusBay.Offset(needed, BodySide), 2.85f), "at the least kerb the bus stands 2.85 m off the lane's line");
        Check(Near(BusBay.Offset(4.60f, BodySide), 3.25f), "the bus keeps its 0.15 m from the kerb where the strip is wider");
        Check(BusBay.Offset(needed, 1.25f) == 0f, "a body 2.5 m wide is given the 2.40 m bus's bay");
        Check(Near(BusBay.LaneClear(2.85f, 0f, BodyHalf, BodySide), BusBay.Clear), "standing true at the least kerb its side is not on the lane's edge");
        Check(Near(BusBay.KerbClear(4.20f, 2.85f, 0f, BodyHalf, BodySide), BusBay.KerbGap), "standing true the bus is not 0.15 m from the kerb");
        Check(BusBay.LaneClear(0f, 0f, BodyHalf, BodySide) < -2.8f, "a bus on the lane's line is not measured as in the lane");
        Check(BusBay.LaneClear(2.85f, 3f, BodyHalf, BodySide) < 0f, "three degrees askew at the least kerb is not measured as a corner in the lane");
        // (A lorry on the lane's line passes a bus in the narrowest bay with a third of a metre, as on a street whose parking
        // lane is narrow: the traffic slows beside it. Nothing of the bus itself is in the lane.)
        Check(BusBay.LaneHalf + BusBay.Clear - BusBay.WidestHalf >= 0.33f, "a lorry on the lane's line passes a bus in the narrowest bay with less than a third of a metre");
        Zones();

        // The curves themselves.
        foreach (float offset in new[] { 0.8f, 1.6f, 2.85f, 3.25f, 4.35f })
        {
            foreach (float lever in new[] { 1.7f, 3.33f, 4.95f, BusBay.BusLeave })
            {
                for (int which = 0; which < 3; which++)
                {
                    BusBay.Curve curve = which == 0 ? BusBay.In(offset, lever) : which == 1 ? BusBay.Out(offset, lever) : BusBay.Approach(offset);
                    string name = (which == 0 ? "in" : which == 1 ? "out" : "the bus's approach") + " to " + offset + (which < 2 ? " with a lever of " + lever : "");
                    Check(curve.Aside(0f) == 0f && Near(curve.Aside(curve.Length), offset, 0.001f) && Near(curve.Aside(curve.Length + 5f), offset, 0.001f), "the curve " + name + " does not run from the bay's line to the lane's");
                    Check(curve.Length > 3f && curve.Length <= (which == 2 ? BusBay.ApproachMost : BusBay.RunMost) + 0.01f, "the curve " + name + " is " + curve.Length + " m long");
                    bool rises = true, smooth = true;
                    float before = 0f;
                    for (float d = 0.05f; d <= curve.Length + 0.2f; d += 0.05f)
                    {
                        float at = curve.Aside(d);
                        rises &= at >= before - 0.0001f;
                        smooth &= at - before <= 0.05f * 0.42f + 0.0005f;   // never steeper than 0.40 (22 degrees), so no jump at a joint either
                        before = at;
                    }
                    Check(rises, "the curve " + name + " goes back on itself");
                    Check(smooth, "the curve " + name + " jumps or runs steeper than 22 degrees");
                }
            }
        }

        // The bus driven in, stood and driven out again, at the kerbs the city has (4.18 to 5.48 m from the lane's line).
        foreach (float kerb in new[] { 4.20f, 4.30f, 4.60f, 5.00f, 5.60f })
        {
            BayRun(kerb, 0.02f, 0f, 0f, BusBay.Speed, print);
        }
        // The same when things are not as planned: a step of the physics clock twice and half as long, a bus that comes up
        // 0.3 m off the lane's line and 3 degrees askew, a slower and a faster pace through the bay.
        BayRun(4.30f, 0.04f, 0f, 0f, BusBay.Speed, print);
        BayRun(4.30f, 0.01f, 0f, 0f, BusBay.Speed, print);
        BayRun(4.30f, 0.02f, 0.3f, 3f, BusBay.Speed, print);
        BayRun(4.30f, 0.02f, -0.3f, -3f, BusBay.Speed, print);
        BayRun(4.30f, 0.02f, 0f, 0f, 3f, print);
        BayRun(4.30f, 0.02f, 0f, 0f, 6f, print);

        // Held up on its way in, it stands askew part of the way across and must still get out: onto the lane's line, never
        // much further towards the kerb than it stood.
        foreach (float aside in new[] { 0.9f, 1.6f, 2.4f })
        {
            Bus stands = Standing(-6f, aside, 9f);
            Corners(stands, out float stoodKerb, out _, out _, out _);
            float kerbMost = float.MinValue;
            Drive leave = RouteOut(stands, out _);
            Bus end = leave.Run(stands, 40f, bus =>
            {
                Corners(bus, out float kerb, out _, out _, out _);
                kerbMost = Math.Max(kerbMost, kerb);
            });
            Check(Math.Abs(end.MiddleZ) < 0.15f && Math.Abs(end.Heading) < 0.03f, "a bus that stood askew " + aside + " m across does not get back onto the lane's line (" + end.MiddleZ + " m off it)");
            Check(kerbMost <= stoodKerb + 0.15f, "a bus that stood askew " + aside + " m across swings " + (kerbMost - stoodKerb) + " m further towards the kerb as it leaves");
        }
    }

    private static void BayRun(float kerb, float step, float startAside, float startDegrees, float pace, bool print)
    {
        string name = "kerb " + kerb.ToString("F2") + (step != 0.02f ? ", steps of " + step : "") + (startAside != 0f ? ", coming up " + startAside + " m off the line" : "")
                      + (pace != BusBay.Speed ? ", at " + pace + " m/s" : "");
        if (!BusBay.Lay(kerb, out float offset, out BusBay.Curve layIn, out BusBay.Curve layOut, out float behind, out float ahead))
        {
            Check(false, name + ": no bay is laid out at a kerb the city has (" + (offset > 0f ? "a curve is cut short" : "too narrow") + ")");
            return;
        }
        // What TransitMap lays out for a stop is what the bus needs, within the plain lane a stop has.
        Check(layIn.Length + BusBay.Settle + BusBay.BusMiddleAhead + 1f <= 33f && layOut.Length - BusBay.BusMiddleAhead + 1f <= 30f,
            name + ": the bay's path leaves the lane " + (layIn.Length + BusBay.Settle + BusBay.BusMiddleAhead) + " m before the pole and rejoins it " + (layOut.Length - BusBay.BusMiddleAhead) + " m after");
        Check(behind <= 32f && ahead <= 25f, name + ": the bay is " + behind + " m behind and " + ahead + " m ahead of the pole");

        Drive enter = RouteIn(offset, out BusBay.Curve curveIn);
        enter.Step = step;
        enter.BaySpeed = pace;
        float kerbMost = float.MinValue, stripFrom = float.MaxValue, fastest = 0f, steepest = 0f;
        Bus start = Standing(-85f, startAside, startDegrees);
        start.Speed = Cruise;
        float pathFrom = -(curveIn.Points * BusBay.Step + BusBay.Settle) - BusBay.BusMiddleAhead;
        float reckoned = enter.Reckoned(start);
        float took = 0f;
        Bus rest = enter.Run(start, 90f, bus =>
        {
            took += step;
            Corners(bus, out float kerbSide, out _, out float back, out _);
            kerbMost = Math.Max(kerbMost, kerbSide);
            if (bus.Fx > -70f)
            {
                stripFrom = Math.Min(stripFrom, back);   // (a bus that comes up askew towards the kerb is in the strip at once: from where it is under way)
            }
            if (bus.Fx > pathFrom)
            {
                fastest = Math.Max(fastest, bus.Speed);
                steepest = Math.Max(steepest, Math.Abs(bus.Heading));
            }
        });
        float degrees = rest.Heading * 180f / (float)Math.PI;
        float middle = rest.MiddleX;
        float aside = rest.MiddleZ;
        Corners(rest, out float standKerb, out float standLane, out _, out _);
        float toLane = standLane - BusBay.LaneHalf;
        float toKerb = kerb - standKerb;
        Check(Math.Abs(middle) <= 0.25f, name + ": it stands " + middle + " m from the pole");
        Check(Math.Abs(aside - offset) <= 0.08f, name + ": its middle stands " + (aside - offset) + " m off the bay's line");
        Check(Math.Abs(degrees) <= 1.5f, name + ": it stands " + degrees + " degrees askew");
        // (Square to within a degree, its rear corner on the lane's side may stand a hand's breadth in the lane at the narrowest bay.)
        Check(toLane >= -0.12f, name + ": a corner stands " + -toLane + " m in the lane");
        Check(toKerb >= 0.05f, name + ": a corner stands " + toKerb + " m from the kerb");
        // Coming in, its nose swings up to the kerb's edge and, in the widest strips, a few centimetres over it: over the
        // kerb, not into it (the nose is a third of a metre up, the kerb a sixth).
        Check(kerbMost <= kerb + 0.05f, name + ": coming in, a corner swings to " + (kerb - kerbMost) + " m from the kerb");
        Check(stripFrom >= -behind - 0.05f, name + ": coming in, it is beyond the lane's edge " + -stripFrom + " m before the pole, and the bay is kept free for " + behind);
        Check(fastest <= pace + 1f, name + ": it is doing " + fastest + " m/s on the bay's path");
        // What the stop's panel says is what the drive takes (nothing in the way here): within a second, or 5 per cent.
        Check(Math.Abs(reckoned - took) <= Math.Max(1f, took * 0.05f), name + ": the bus reckoned " + reckoned + " s to its stop and took " + took);

        Drive leave = RouteOut(rest, out BusBay.Curve curveOut);
        leave.Step = step;
        leave.BaySpeed = pace;
        float leftKerbMost = float.MinValue, stripTo = float.MinValue, leastAside = float.MaxValue, onLaneAfter = -1f, time = 0f;
        Bus gone = leave.Run(rest, 30f, bus =>
        {
            time += step;
            Corners(bus, out float kerbSide, out _, out _, out float front);
            leftKerbMost = Math.Max(leftKerbMost, kerbSide);
            stripTo = Math.Max(stripTo, front);
            leastAside = Math.Min(leastAside, bus.Fz);
            if (onLaneAfter < 0f && Math.Abs(bus.MiddleZ) < 0.15f)
            {
                onLaneAfter = time;
            }
            steepest = Math.Max(steepest, Math.Abs(bus.Heading));
        });
        // Pulling out its tail swings towards the kerb as its body turns: no further than the kerb's edge.
        Check(leftKerbMost <= kerb + 0.02f, name + ": pulling out, a corner swings to " + (kerb - leftKerbMost) + " m from the kerb");
        Check(stripTo <= ahead + 0.05f, name + ": pulling out, it is still beyond the lane's edge " + stripTo + " m past the pole, and the bay is kept free for " + ahead);
        Check(onLaneAfter > 0f && Math.Abs(gone.MiddleZ) < 0.1f && Math.Abs(gone.Heading) < 0.03f, name + ": it does not end on the lane's line (" + gone.MiddleZ + " m off it)");
        Check(leastAside > -0.15f, name + ": pulling out, its front axle swings " + -leastAside + " m over the lane's line to the far side");
        Check(steepest <= 24f * Math.PI / 180f, name + ": it turns " + steepest * 180f / Math.PI + " degrees off the lane's direction");
        if (print)
        {
            Console.WriteLine("bay  " + name + ": reckoned " + reckoned.ToString("F1") + " s, took " + took.ToString("F1") + " s; the bus " + offset.ToString("F2") + " m off the line; in over " + curveIn.Length.ToString("F1") + " m and " + BusBay.Settle.ToString("F0")
                              + " along the bay, stands " + middle.ToString("+0.00;-0.00") + " m along, " + (aside - offset).ToString("+0.000;-0.000") + " m off the bay's line, "
                              + degrees.ToString("F2") + " degrees, " + toLane.ToString("F2") + " m clear of the lane's edge, " + toKerb.ToString("F2") + " m from the kerb (nearest it came: "
                              + (kerb - kerbMost).ToString("F2") + "), beyond the lane's edge from " + stripFrom.ToString("F1") + " (kept free from " + (-behind).ToString("F1") + "); out over "
                              + curveOut.Length.ToString("F1") + " m, nearest the kerb " + (kerb - leftKerbMost).ToString("F2") + ", beyond the lane's edge to " + stripTo.ToString("F1")
                              + " (kept free to " + ahead.ToString("F1") + "), on the lane's line after " + onLaneAfter.ToString("F1") + " s; steepest " + (steepest * 180f / Math.PI).ToString("F0") + " degrees.");
        }
    }
}
