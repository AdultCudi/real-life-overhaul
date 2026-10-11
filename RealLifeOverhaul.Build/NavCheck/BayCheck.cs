using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// Checks for the bus bays (Transport/BusBay.cs, compiled as it ships): the arithmetic of where a bus stands, the two
// curves, and a bus driven in and out along them by a model of the mod's driver.
//
// THE MODEL is the part of Traffic/EmergencyDriver.FixedUpdate that steers and paces a vehicle along a route, written
// out again here in plain numbers (that file needs the game's engine): the next point of the route is the first one
// further off than the reach (the last one is driven up to 1.2 m); the vehicle is turned towards it at no more than
// 120 degrees a second about its own origin; it gathers speed at 6 m/s2 and loses it at 24; within its last two points
// it goes no faster than it can stop from at 4 m/s2, never under 1.5 m/s, and stops dead when the last is reached.
// TransitBus adds what is modelled as it is there: the pace down to the bay's (2.5 m/s2 before the path), the tighter
// reach from 6 m before it, and 1.5 m/s rising by 1.5 a second on the way out. What the model is not: the game's
// physics (springs, a body pushed by another). Whether the real vehicle keeps to these numbers is for the game to
// show, in the arrival line of Player.log.
internal static partial class Program
{
    private const float BodyHalf = 3.33f;      // TransitBus.BodyLength / 2
    private const float BodySide = 1.05f;      // TransitBus.BodyHalfWidth
    private const float RouteReach = 4.5f;     // EmergencyDriver.PointReach
    private const float FinalReach = 1.2f;     // EmergencyDriver.FinalReach, and TransitBus.FinalShort
    private const float Cruise = 11f;          // TransitBus.Cruise
    private const float BayBraking = 2.5f;     // TransitBus.BayBraking
    private const float BayLead = 6f;          // TransitBus.BayLead

    private struct Pose
    {
        public float Along, Aside, Heading, Speed;   // of the point it is turned about; heading in radians, towards the kerb positive
    }

    // What the body reaches, for a pose: the furthest any corner is towards the kerb, and from where to where along the
    // lane its kerb side lies beyond the lane's edge (that length of the strip must be free; stripBack is above stripFront
    // when none of it does).
    private static void Corners(Pose pose, float middleAhead, out float kerbMost, out float stripBack, out float stripFront)
    {
        float cos = (float)Math.Cos(pose.Heading), sin = (float)Math.Sin(pose.Heading);
        float mx = pose.Along + cos * middleAhead, my = pose.Aside + sin * middleAhead;
        kerbMost = float.MinValue;
        for (int end = -1; end <= 1; end += 2)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                kerbMost = Math.Max(kerbMost, my + sin * end * BodyHalf + cos * side * BodySide);
            }
        }
        float backX = mx - cos * BodyHalf - sin * BodySide, backY = my - sin * BodyHalf + cos * BodySide;
        float frontX = mx + cos * BodyHalf - sin * BodySide, frontY = my + sin * BodyHalf + cos * BodySide;
        float edge = BusBay.LaneHalf;
        stripBack = float.MaxValue;
        stripFront = float.MinValue;
        if (backY <= edge && frontY <= edge)
        {
            return;
        }
        float crossX = Math.Abs(frontY - backY) > 0.0001f ? backX + (edge - backY) / (frontY - backY) * (frontX - backX) : backX;
        stripBack = backY > edge ? backX : crossX;
        stripFront = frontY > edge ? frontX : crossX;
    }

    private sealed class Drive
    {
        public readonly List<float> X = new List<float>(), Y = new List<float>();
        public int FirstOfBay;          // in: the first point of the bay's path; out: the first of the lane again
        public float BayRun;            // in: metres of route from the first point of the bay's path to its end
        public bool Leaving;
        public float Step = 0.02f;
        public float BaySpeed = BusBay.Speed;

        // Runs the model to the end of the route (or for so many seconds). Calls back with every pose.
        public Pose Run(Pose pose, float seconds, Action<Pose> each)
        {
            int next = 0;
            int last = X.Count - 1;
            float time = 0f;
            float slowTick = 0f;
            float cruise = Leaving ? 1.5f : Cruise;
            float reach = RouteReach;
            while (time < seconds)
            {
                float hx = (float)Math.Cos(pose.Heading), hy = (float)Math.Sin(pose.Heading);
                while (next <= last)
                {
                    float dx = X[next] - pose.Along, dy = Y[next] - pose.Aside;
                    bool final = !Leaving && next == last;
                    float r = final ? FinalReach : reach;
                    if (dx * dx + dy * dy >= r * r && !(final && dx * hx + dy * hy < 0f))
                    {
                        break;
                    }
                    next++;
                }
                if (next > last)
                {
                    break;
                }
                // TransitBus.Drive, four times a second: the pace and the reach.
                if (time >= slowTick)
                {
                    slowTick = time + 0.25f;
                    float left = Left(pose, next);
                    if (!Leaving)
                    {
                        float lead = left - BayRun;
                        cruise = Math.Min(Cruise, (float)Math.Sqrt(BaySpeed * BaySpeed + 2f * BayBraking * Math.Max(0f, lead)));
                        reach = lead <= BayLead ? BusBay.Reach : RouteReach;
                    }
                    else if (next >= FirstOfBay)
                    {
                        cruise = Cruise;
                        reach = RouteReach;
                    }
                    else
                    {
                        cruise = Math.Min(BaySpeed, 1.5f + 1.5f * time);
                        reach = BusBay.Reach;
                    }
                }
                float tx = X[next] - pose.Along, ty = Y[next] - pose.Aside;
                float want = (float)Math.Atan2(ty, tx);
                float turn = want - pose.Heading;
                while (turn > Math.PI) turn -= 2f * (float)Math.PI;
                while (turn < -Math.PI) turn += 2f * (float)Math.PI;
                float most = 120f * (float)Math.PI / 180f * Step;
                float wanted = Math.Abs(turn) > 22f * Math.PI / 180f ? Math.Min(6.5f, cruise) : cruise;
                float toStop = float.MaxValue;
                if (!Leaving)
                {
                    float toPoint = (float)Math.Sqrt(tx * tx + ty * ty);
                    if (next == last)
                    {
                        toStop = toPoint;
                    }
                    else if (next == last - 1)
                    {
                        float lx = X[last] - X[next], ly = Y[last] - Y[next];
                        toStop = toPoint + (float)Math.Sqrt(lx * lx + ly * ly);
                    }
                }
                if (toStop < 30f)
                {
                    wanted = Math.Min(wanted, Math.Max(1.5f, (float)Math.Sqrt(2f * 4f * toStop)));
                }
                // (The engine turns the body at the end of the step: this step still moves along the old heading.)
                float rate = (wanted < pose.Speed ? 24f : 6f) * Step;
                pose.Speed += Math.Max(-rate, Math.Min(rate, wanted - pose.Speed));
                pose.Along += hx * pose.Speed * Step;
                pose.Aside += hy * pose.Speed * Step;
                pose.Heading += Math.Max(-most, Math.Min(most, turn));
                time += Step;
                each?.Invoke(pose);
            }
            return pose;
        }

        private float Left(Pose pose, int next)
        {
            float dx = X[next] - pose.Along, dy = Y[next] - pose.Aside;
            float left = (float)Math.Sqrt(dx * dx + dy * dy);
            for (int i = next + 1; i < X.Count; i++)
            {
                dx = X[i] - X[i - 1];
                dy = Y[i] - Y[i - 1];
                left += (float)Math.Sqrt(dx * dx + dy * dy);
            }
            return left;
        }
    }

    // The route TransitBus.RouteToBay lays: lane points 5 m apart, then the curve in, the standing place and the point beyond it.
    private static Drive RouteIn(float offset, float middleAhead, out BusBay.Curve curve)
    {
        curve = BusBay.In(offset, BodyHalf + middleAhead, BusBay.RunMost - Math.Max(0f, middleAhead));
        int points = curve.Points;
        var drive = new Drive();
        float rest = -middleAhead;
        float leaves = points * BusBay.Step + middleAhead + 0.5f;
        for (float before = 80f; before > leaves; before -= 5f)
        {
            drive.X.Add(-before);
            drive.Y.Add(0f);
        }
        drive.FirstOfBay = drive.X.Count;
        for (int k = points; k >= 1; k--)
        {
            float d = k * BusBay.Step;
            drive.X.Add(rest - d);
            drive.Y.Add(offset - curve.Aside(d));
        }
        drive.X.Add(rest);
        drive.Y.Add(offset);
        drive.X.Add(rest + FinalReach);
        drive.Y.Add(offset);
        drive.BayRun = points * BusBay.Step + FinalReach;
        return drive;
    }

    // The route TransitBus.PullOut lays from where the bus stands: the curve out, then lane points 5 m apart.
    private static Drive RouteOut(Pose stands, float middleAhead, out BusBay.Curve curve)
    {
        curve = BusBay.Out(stands.Aside, BodyHalf - middleAhead, BusBay.RunMost - Math.Max(0f, stands.Along));
        int points = curve.Points;
        var drive = new Drive { Leaving = true };
        for (int k = 1; k <= points; k++)
        {
            float gone = k * BusBay.Step;
            drive.X.Add(stands.Along + gone);
            drive.Y.Add(stands.Aside - curve.Aside(gone));
        }
        drive.FirstOfBay = drive.X.Count;
        float rejoins = stands.Along + points * BusBay.Step + 0.5f;
        for (float on = (float)Math.Ceiling(rejoins / 5f) * 5f + 0.01f; on < 90f; on += 5f)
        {
            drive.X.Add(on);
            drive.Y.Add(0f);
        }
        return drive;
    }

    private static void Bays(bool print)
    {
        float needed = BusBay.KerbNeeded(BodySide);
        Check(Near(needed, 4.10f), "a bay wants the kerb 4.10 m from the lane's line for a 2.10 m body, not " + needed);
        Check(BusBay.Offset(needed - 0.02f, BodySide) == 0f, "a kerb 2 cm too near still gives a bay");
        Check(Near(BusBay.Offset(needed, BodySide), 2.85f), "at the least kerb the bus stands 2.85 m off the lane's line");
        Check(Near(BusBay.Offset(4.60f, BodySide), 3.35f), "the bus keeps its 0.20 m from the kerb where the strip is wider");
        Check(BusBay.Offset(needed, 1.25f) == 0f, "a vehicle 2.5 m wide is given the 2.1 m body's bay");
        Check(Near(BusBay.LaneClear(2.85f, 0f, BodyHalf, BodySide), BusBay.Clear), "standing true at the least kerb the body is not 0.15 m clear of the lane's edge");
        Check(Near(BusBay.KerbClear(4.10f, 2.85f, 0f, BodyHalf, BodySide), BusBay.KerbGap), "standing true the body is not 0.20 m from the kerb");
        Check(BusBay.LaneClear(0f, 0f, BodyHalf, BodySide) < -2.6f, "a bus on the lane's line is not measured as in the lane");
        Check(BusBay.LaneClear(2.85f, 3f, BodyHalf, BodySide) < 0f, "three degrees askew at the least kerb is not measured as a corner in the lane");
        Check(BusBay.LaneHalf + BusBay.Clear - BusBay.WidestHalf >= 0.5f, "a lorry on the lane's line does not pass half a metre from a bus in the narrowest bay");

        // The curves themselves.
        foreach (float offset in new[] { 0.8f, 1.6f, 2.85f, 3.35f, 4.35f })
        {
            foreach (float lever in new[] { 1.7f, 3.33f, 4.95f })
            {
                for (int which = 0; which < 2; which++)
                {
                    BusBay.Curve curve = which == 0 ? BusBay.In(offset, lever) : BusBay.Out(offset, lever);
                    string name = (which == 0 ? "in" : "out") + " to " + offset + " with a lever of " + lever;
                    Check(curve.Aside(0f) == 0f && Near(curve.Aside(curve.Length), offset, 0.001f) && Near(curve.Aside(curve.Length + 5f), offset, 0.001f), "the curve " + name + " does not run from the bay's line to the lane's");
                    Check(curve.Length > 3f && curve.Length <= BusBay.RunMost + 0.01f, "the curve " + name + " is " + curve.Length + " m long");
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

        // A bus driven in, stood and driven out again. The body on its van (the point it is turned about under its middle),
        // on a van whose origin is a little behind that, and on a vehicle whose origin is under its rear axle.
        float[] kerbs = { 4.10f, 4.15f, 4.60f, 5.60f };
        float[] middles = { 0f, 0.4f, 1.62f };
        for (int m = 0; m < middles.Length; m++)
        {
            for (int k = 0; k < kerbs.Length; k++)
            {
                BayRun(kerbs[k], middles[m], 0.02f, 0f, 0f, BusBay.Speed, print);
            }
        }
        // The same when things are not as planned: a step of the physics clock twice and half as long, a bus that comes up
        // 0.3 m off the lane's line and 3 degrees askew, a slower and a faster pace through the bay.
        BayRun(4.15f, 0f, 0.04f, 0f, 0f, BusBay.Speed, print);
        BayRun(4.15f, 0f, 0.01f, 0f, 0f, BusBay.Speed, print);
        BayRun(4.15f, 0f, 0.02f, 0.3f, 3f, BusBay.Speed, print);
        BayRun(4.15f, 0f, 0.02f, -0.3f, -3f, BusBay.Speed, print);
        BayRun(4.15f, 0f, 0.02f, 0f, 0f, 3f, print);
        BayRun(4.15f, 0f, 0.02f, 0f, 0f, 6f, print);

        // Held up on its way in, it stands askew part of the way across and must still get out: onto the lane's line, never
        // further towards the kerb than it stood.
        foreach (float aside in new[] { 0.9f, 1.6f, 2.4f })
        {
            var stands = new Pose { Along = -6f, Aside = aside, Heading = 9f * (float)Math.PI / 180f };
            Corners(stands, 0f, out float stoodKerb, out _, out _);
            float kerbMost = float.MinValue;
            Drive leave = RouteOut(stands, 0f, out _);
            Pose end = leave.Run(stands, 30f, pose =>
            {
                Corners(pose, 0f, out float kerb, out _, out _);
                kerbMost = Math.Max(kerbMost, kerb);
            });
            Check(Math.Abs(end.Aside) < 0.15f && Math.Abs(end.Heading) < 0.05f, "a bus that stood askew " + aside + " m across does not get back onto the lane's line (" + end.Aside + " m off it)");
            Check(kerbMost <= stoodKerb + 0.15f, "a bus that stood askew " + aside + " m across swings " + (kerbMost - stoodKerb) + " m further towards the kerb as it leaves");
        }
    }

    private static void BayRun(float kerb, float middleAhead, float step, float startAside, float startDegrees, float pace, bool print)
    {
        string name = "kerb " + kerb.ToString("F2") + ", turned about a point " + middleAhead.ToString("F2") + " m behind its middle"
                      + (step != 0.02f ? ", steps of " + step : "") + (startAside != 0f ? ", coming up " + startAside + " m off the line" : "") + (pace != BusBay.Speed ? ", at " + pace + " m/s" : "");
        float offset = BusBay.Offset(kerb, BodySide);
        bool van = middleAhead < 0.5f;   // the body on a van: what a bay is laid out for

        Drive enter = RouteIn(offset, middleAhead, out BusBay.Curve curveIn);
        BusBay.Curve fits = BusBay.Out(offset, BodyHalf - middleAhead, BusBay.RunMost + Math.Min(0f, middleAhead));
        float behind = BusBay.Behind(curveIn, middleAhead);
        float ahead = BusBay.Ahead(fits, middleAhead);
        if (curveIn.Cut || fits.Cut)
        {
            // TransitBus.BayOpen gives such a vehicle the lane's line instead: there is nothing to drive.
            Check(!van, name + ": the body on a van is refused its bay (the curve will not fit)");
            if (print)
            {
                Console.WriteLine("bay  " + name + ": no bay for this vehicle here (its curve in would be " + curveIn.Length.ToString("F1") + " m with the exponential cut short): it stops in the lane.");
            }
            return;
        }
        if (van && middleAhead == 0f)
        {
            // What TransitMap lays out for a stop is what the body on its van needs, and no more than a stop has of plain lane.
            Check(behind <= 24f && ahead <= 24f && curveIn.Length + 1f <= 26f && fits.Length + 1f <= 26f, name + ": the bay is " + behind + " m behind and " + ahead + " m ahead of the pole");
        }
        Check(behind <= 23.5f, name + ": this vehicle needs " + behind + " m of bay behind the pole, more than a stop is ever looked at for");

        enter.Step = step;
        enter.BaySpeed = pace;
        float kerbMost = float.MinValue, stripFrom = float.MaxValue, fastest = 0f, steepest = 0f;
        var start = new Pose { Along = -85f, Aside = startAside, Heading = startDegrees * (float)Math.PI / 180f, Speed = Cruise };
        Pose rest = enter.Run(start, 60f, pose =>
        {
            Corners(pose, middleAhead, out float kerbSide, out float back, out _);
            kerbMost = Math.Max(kerbMost, kerbSide);
            stripFrom = Math.Min(stripFrom, back);
            if (pose.Along > -curveIn.Length - middleAhead)
            {
                fastest = Math.Max(fastest, pose.Speed);
                steepest = Math.Max(steepest, Math.Abs(pose.Heading));
            }
        });
        float degrees = rest.Heading * 180f / (float)Math.PI;
        float middle = rest.Along + middleAhead;
        float aside = rest.Aside + (float)Math.Sin(rest.Heading) * middleAhead;
        float toLane = BusBay.LaneClear(aside, degrees, BodyHalf, BodySide);
        float toKerb = BusBay.KerbClear(kerb, aside, degrees, BodyHalf, BodySide);
        Check(Math.Abs(middle) <= 0.25f, name + ": it stands " + middle + " m from the pole");
        Check(Math.Abs(aside - offset) <= 0.06f, name + ": it stands " + (aside - offset) + " m off the bay's line");
        Check(Math.Abs(degrees) <= 2f, name + ": it stands " + degrees + " degrees askew");
        Check(toLane >= 0f, name + ": a corner stands " + -toLane + " m in the lane");
        Check(toKerb >= 0.05f, name + ": a corner stands " + toKerb + " m from the kerb");
        // The nose as it comes in: for the body on a van, never nearer the kerb than a hand's width. A vehicle turned about
        // its rear axle has a longer nose than the bay is laid out for: it may swing up to the kerb's line, no further.
        Check(kerbMost <= kerb - (van ? 0.10f : 0.03f), name + ": coming in, a corner swings to " + (kerb - kerbMost) + " m from the kerb");
        Check(stripFrom >= -behind, name + ": coming in, it is beyond the lane's edge " + -stripFrom + " m before the pole, and the bay is kept free for " + behind);
        Check(fastest <= pace + 1f, name + ": it is doing " + fastest + " m/s on the bay's path");
        Check(curveIn.Points * BusBay.Step + middleAhead <= BusBay.RunMost + 1f, name + ": the path leaves the lane " + (curveIn.Points * BusBay.Step + middleAhead) + " m before the pole");

        Drive leave = RouteOut(rest, middleAhead, out BusBay.Curve curveOut);
        leave.Step = step;
        leave.BaySpeed = pace;
        float leftKerbMost = float.MinValue, stripTo = float.MinValue, leastAside = float.MaxValue, onLaneAfter = -1f, time = 0f;
        rest.Speed = 0f;
        Pose gone = leave.Run(rest, 14f, pose =>
        {
            time += step;
            Corners(pose, middleAhead, out float kerbSide, out _, out float front);
            leftKerbMost = Math.Max(leftKerbMost, kerbSide);
            stripTo = Math.Max(stripTo, front);
            leastAside = Math.Min(leastAside, pose.Aside);
            if (onLaneAfter < 0f && Math.Abs(pose.Aside) < 0.15f)
            {
                onLaneAfter = time;
            }
            steepest = Math.Max(steepest, Math.Abs(pose.Heading));
        });
        Check(leftKerbMost <= kerb - 0.03f, name + ": pulling out, a corner swings to " + (kerb - leftKerbMost) + " m from the kerb");
        Check(stripTo <= ahead, name + ": pulling out, it is still beyond the lane's edge " + stripTo + " m past the pole, and the bay is kept free for " + ahead);
        Check(onLaneAfter > 0f && Math.Abs(gone.Aside) < 0.1f && Math.Abs(gone.Heading) < 0.03f, name + ": it does not end on the lane's line (" + gone.Aside + " m off it)");
        Check(leastAside > -0.15f, name + ": pulling out, it swings " + -leastAside + " m over the lane's line to the far side");
        Check(rest.Along + curveOut.Points * BusBay.Step <= BusBay.RunMost + 1f, name + ": the path rejoins the lane " + (rest.Along + curveOut.Points * BusBay.Step) + " m past the pole");
        Check(steepest <= 24f * Math.PI / 180f, name + ": it turns " + steepest * 180f / Math.PI + " degrees off the lane's direction");
        if (print)
        {
            Console.WriteLine("bay  " + name + ": the bus " + offset.ToString("F2") + " m off the line; in over " + curveIn.Length.ToString("F1") + " m, stands " + middle.ToString("+0.00;-0.00") + " m along, "
                              + (aside - offset).ToString("+0.000;-0.000") + " m off the bay's line, " + degrees.ToString("F2") + " degrees, " + toLane.ToString("F2") + " m clear of the lane's edge, "
                              + toKerb.ToString("F2") + " m from the kerb (nearest it came: " + (kerb - kerbMost).ToString("F2") + "), beyond the lane's edge from " + stripFrom.ToString("F1") + " (kept free from "
                              + (-behind).ToString("F1") + "); out over " + curveOut.Length.ToString("F1") + " m, nearest the kerb " + (kerb - leftKerbMost).ToString("F2") + ", beyond the lane's edge to "
                              + stripTo.ToString("F1") + " (kept free to " + ahead.ToString("F1") + "), on the lane's line after " + onLaneAfter.ToString("F1") + " s; steepest " + (steepest * 180f / Math.PI).ToString("F0") + " degrees.");
        }
    }
}
