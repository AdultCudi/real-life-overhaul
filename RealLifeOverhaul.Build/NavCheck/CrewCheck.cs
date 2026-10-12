using System;
using System.Diagnostics;
using RealLifeOverhaul;

// Checks for the ambulance crew's way as plain geometry (People/CrewPath.cs), compiled as it ships: how near a point comes
// to the crew on its way (Clearance), and the part of a straight line, a length of tape, that the crew comes near
// (Crossing), each against the plain way of asking (every pose for a point; a point every 5 mm along a line). Then what a
// planning's worth of ways costs: the crew's planning (People/CrewWay.Plan) tries some hundreds of ways in the one frame
// the crew steps out, each made, tested against the casualty's body at four points and against the four sides of the
// tape; the tape was asked about every 10 cm against every pose, and one planning took 2.2 s in play.
//
// What is not here: the street. Whether a way is clear of vehicles, fixtures and kerbs is asked of the engine
// (CrewWay.Survey); the "People: paramedics out" line of Player.log says what a planning cost in play.
internal static partial class Program
{
    private static void Crew(bool print)
    {
        var random = new Random(20261012);
        var path = new CrewPath();
        int ways = 0, points = 0, lines = 0, crossed = 0, apart = 0, wrong = 0;
        string first = null;
        for (int n = 0; n < 400; n++)
        {
            if (!CrewWay(random, path))
            {
                continue;
            }
            ways++;
            CrewBox(path, out float minX, out float maxX, out float minZ, out float maxZ);
            for (int k = 0; k < 12; k++)
            {
                float x = minX - 6f + (float)random.NextDouble() * (maxX - minX + 12f);
                float z = minZ - 6f + (float)random.NextDouble() * (maxZ - minZ + 12f);
                float quick = path.Clearance(x, z), plain = CrewPlain(path, x, z);
                points++;
                if (quick != plain)
                {
                    wrong++;
                    first = first ?? "a clearance of " + quick + " where every pose says " + plain;
                }
            }
            for (int k = 0; k < 6; k++)
            {
                float ax = minX - 3f + (float)random.NextDouble() * (maxX - minX + 6f), az = minZ - 3f + (float)random.NextDouble() * (maxZ - minZ + 6f);
                double turn = random.NextDouble() * Math.PI * 2.0;
                float length = k == 5 ? 0f : 2f + (float)random.NextDouble() * 10f;
                float bx = ax + (float)Math.Cos(turn) * length, bz = az + (float)Math.Sin(turn) * length;
                float within = k % 2 == 0 ? 0.3f : 0.6f;
                bool exact = path.Crossing(ax, az, bx, bz, within, out float low, out float high);
                bool fine = CrewFine(path, ax, az, bx, bz, within, 0.005f, out float fineLow, out float fineHigh);
                float slack = (length > 0f ? 0.006f / length : 0f) + 1e-4f;   // one step of the fine look, and the rounding
                lines++;
                if (exact != fine || (exact && (low > fineLow + 1e-4f || high < fineHigh - 1e-4f || low < fineLow - slack || high > fineHigh + slack)))
                {
                    wrong++;
                    first = first ?? "a line " + length.ToString("F2") + " m long: crossed " + exact + " from " + low.ToString("F4") + " to " + high.ToString("F4")
                                     + "; looked at every 5 mm: " + fine + " from " + fineLow.ToString("F4") + " to " + fineHigh.ToString("F4");
                }
                if (exact)
                {
                    crossed++;
                }
                else
                {
                    apart++;
                }
            }
        }
        Check(ways > 300, "crew: the ways made up to check against are made (" + ways + " of 400)");
        Check(wrong == 0, "crew: how near a point comes, and the part of a line the crew comes near, are what the plain way of asking finds ("
                          + points + " points, " + lines + " lines" + (first != null ? "; the first that is not: " + first : "") + ")");
        Check(crossed > 200 && apart > 200, "crew: and the lines checked both cross the way and keep away from it (" + crossed + " and " + apart + ")");
        CrewEdges();
        CrewCost(print);
    }

    // A way as the planning lays one: from behind the rear doors, out beside the ambulance, sometimes by a place on
    // the way, round onto the casualty's line and along it; the second of the crew on either side; sometimes a swing.
    private static bool CrewWay(Random random, CrewPath path)
    {
        float[] x = new float[6], z = new float[6];
        int count = 0;
        float side = random.Next(2) == 0 ? -1f : 1f;
        x[count] = 0f;
        z[count++] = 0f;
        x[count] = side * (1.2f + 1.25f);
        z[count++] = -1f;
        if (random.Next(3) > 0)
        {
            x[count] = -6f + (float)random.NextDouble() * 12f;
            z[count++] = -4f - (float)random.NextDouble() * 10f;
        }
        float turnX = -9f + (float)random.NextDouble() * 18f, turnZ = -14f - (float)random.NextDouble() * 14f;
        x[count] = turnX;
        z[count++] = turnZ;
        double heading = random.NextDouble() * Math.PI * 2.0;
        float run = CrewPath.Lead + 1.3f + (float)random.NextDouble() * 2.5f;
        x[count] = turnX + (float)Math.Cos(heading) * run;
        z[count++] = turnZ + (float)Math.Sin(heading) * run;
        if (!path.Build(x, z, count, 1.2f, CrewPath.Lead + 0.05f, side))
        {
            return false;
        }
        if (random.Next(2) == 0)
        {
            float endX = x[count - 1], endZ = z[count - 1];
            path.AddSwing(endX, endZ, (float)Math.Cos(heading), (float)Math.Sin(heading), endX + 1.25f, endZ + 0.4f, (float)Math.Sin(heading), -(float)Math.Cos(heading));
        }
        return true;
    }

    // The box every pose's pusher and bed lie in, as the path keeps it.
    private static void CrewBox(CrewPath path, out float minX, out float maxX, out float minZ, out float maxZ)
    {
        minX = minZ = float.MaxValue;
        maxX = maxZ = float.MinValue;
        for (int i = 0; i < path.PoseCount; i++)
        {
            path.PoseOf(i, out float px, out float pz, out float bx, out float bz, out _, out _);
            minX = Math.Min(minX, Math.Min(px, bx));
            maxX = Math.Max(maxX, Math.Max(px, bx));
            minZ = Math.Min(minZ, Math.Min(pz, bz));
            maxZ = Math.Max(maxZ, Math.Max(pz, bz));
        }
    }

    // How near a point comes, the plain way: every pose (with the path's own answer for a point far outside the whole way).
    private static float CrewPlain(CrewPath path, float x, float z)
    {
        if (path.PoseCount == 0)
        {
            return float.MaxValue;
        }
        CrewBox(path, out float minX, out float maxX, out float minZ, out float maxZ);
        float outside = Math.Max(Math.Max(minX - x, x - maxX), Math.Max(minZ - z, z - maxZ));
        if (outside > 5f)
        {
            return outside - 1.5f;
        }
        float least = float.MaxValue;
        for (int i = 0; i < path.PoseCount; i++)
        {
            path.PoseOf(i, out float px, out float pz, out float bx, out float bz, out float hx, out float hz);
            least = Math.Min(least, CrewPath.PoseClearance(px, pz, bx, bz, hx, hz, path.Side, x, z));
            if (least <= 0f)
            {
                return 0f;
            }
        }
        return least;
    }

    // The part of a line the crew comes near, looked for a point every so far along it (step: metres; 0.1 is how it was asked in play).
    private static bool CrewFine(CrewPath path, float ax, float az, float bx, float bz, float within, float step, out float low, out float high)
    {
        low = 1f;
        high = 0f;
        float length = (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
        int pieces = Math.Max(1, (int)Math.Ceiling(length / step));
        for (int i = 0; i <= pieces; i++)
        {
            float t = (float)i / pieces;
            if (CrewPlain(path, ax + (bx - ax) * t, az + (bz - az) * t) < within)
            {
                low = Math.Min(low, t);
                high = Math.Max(high, t);
            }
        }
        return high >= low;
    }

    // Lines that only one part of the crew comes near, and a line through the bed.
    private static void CrewEdges()
    {
        var path = new CrewPath();
        Check(path.Build(new[] { 0f, 0f }, new[] { 0f, 20f }, 2, 1.2f, CrewPath.Lead + 0.05f, 1f), "crew: a straight way north is made");
        // Across the way 10 m along: the crew sweeps the bed's width (wider than whoever pushes it) and whoever walks at its side, out to the right.
        bool across = path.Crossing(-5f, 10f, 5f, 10f, 0.3f, out float low, out float high);
        float left = -5f + 10f * low, right = -5f + 10f * high;
        Check(across && Near(left, -(CrewPath.BedHalfWidth + 0.3f), 0.005f) && Near(right, CrewPath.SecondOut + CrewPath.Body + 0.3f, 0.005f),
            "crew: a line across a straight way is crossed from the bed's left side to whoever walks on the right (" + left.ToString("F3") + " to " + right.ToString("F3") + " m)");
        Check(!path.Crossing(-5f, -3f, 5f, -3f, 0.3f, out _, out _), "crew: a line 3 m behind where the way starts is not");
        Check(path.Crossing(1f, 0f, 1f, 20f, 0.3f, out _, out _) && !path.Crossing(-0.8f, 0f, -0.8f, 20f, 0.3f, out _, out _) && !path.Crossing(3f, 0f, 3f, 20f, 0.3f, out _, out _),
            "crew: a line alongside 1 m off on the right (within reach of whoever walks at the side) is, 0.8 m off on the left (past the bed and whoever pushes) is not, nor 3 m off");
        Check(path.Crossing(0f, 5f, 0f, 6f, 0.3f, out low, out high) && low == 0f && high == 1f, "crew: a line wholly on the way is crossed all along");
        Check(path.Crossing(0.2f, 9f, 0.2f, 9f, 0.3f, out low, out high) && low == 0f && high == 1f && !path.Crossing(4f, 9f, 4f, 9f, 0.3f, out _, out _),
            "crew: a line of no length is a point, on the way or off it");
        Check(!path.Crossing(-5f, 10f, 5f, 10f, 0f, out _, out _), "crew: no distance at all is near nothing");
    }

    // A planning's worth: a thousand ways made, each tested at four points of the casualty's body and against the four
    // sides of a taped square, the way the planning tests them; the plain way of asking (a point every 10 cm against
    // every pose) against how it is asked now.
    private static void CrewCost(bool print)
    {
        const int Ways = 1000;
        var random = new Random(867);
        var paths = new CrewPath[Ways];
        var tape = new float[Ways * 8];
        var body = new float[Ways * 8];
        for (int w = 0; w < Ways; w++)
        {
            paths[w] = new CrewPath();
            while (!CrewWay(random, paths[w]))
            {
            }
            CrewBox(paths[w], out float minX, out float maxX, out float minZ, out float maxZ);
            // The casualty and the tape round them, at the far end of the way: 11 m by 6 m, turned any way.
            paths[w].PoseOf(paths[w].PoseCount - 1, out _, out _, out float endX, out float endZ, out _, out _);
            double turn = random.NextDouble() * Math.PI * 2.0;
            float cos = (float)Math.Cos(turn), sin = (float)Math.Sin(turn);
            for (int c = 0; c < 4; c++)
            {
                float a = (c == 0 || c == 3) ? -5.5f : 5.5f, b = c < 2 ? -3f : 3f;
                tape[w * 8 + c * 2] = endX + 2f + cos * a - sin * b;
                tape[w * 8 + c * 2 + 1] = endZ + sin * a + cos * b;
                body[w * 8 + c * 2] = endX + 0.85f + cos * (0.45f - c * 0.55f);
                body[w * 8 + c * 2 + 1] = endZ + sin * (0.45f - c * 0.55f);
            }
        }
        float[] x = { 0f, 2.45f, 1f, -4f, -6f }, z = { 0f, -1f, -9f, -20f, -23f };
        var scratch = new CrewPath();
        scratch.Build(x, z, x.Length, 1.2f, CrewPath.Lead + 0.05f, 1f);

        int sumNew = 0;
        CrewNow(paths, tape, body, scratch, x, z, 0, ref sumNew);   // (once first: the runtime's own first-time work is not the path's)
        sumNew = 0;
        Stopwatch watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        CrewNow(paths, tape, body, scratch, x, z, 0, ref sumNew);
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double now = watch.Elapsed.TotalMilliseconds;
        if (print)
        {
            int ignored = 0;
            for (int part = 1; part <= 3; part++)
            {
                watch.Restart();
                CrewNow(paths, tape, body, scratch, x, z, part, ref ignored);
                watch.Stop();
                Console.WriteLine("crew: " + (part == 1 ? "making the ways" : part == 2 ? "the body's four points" : "the tape's four sides") + ": " + watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms");
            }
        }

        int sumOld = 0;
        watch.Restart();
        for (int w = 0; w < Ways; w++)
        {
            scratch.Build(x, z, x.Length, 1.2f, CrewPath.Lead + 0.05f, 1f);
            CrewPath path = paths[w];
            for (int c = 0; c < 4; c++)
            {
                if (CrewPlain(path, body[w * 8 + c * 2], body[w * 8 + c * 2 + 1]) < 0.2f)
                {
                    sumOld++;
                }
                int d = (c + 1) & 3;
                if (CrewFine(path, tape[w * 8 + c * 2], tape[w * 8 + c * 2 + 1], tape[w * 8 + d * 2], tape[w * 8 + d * 2 + 1], 0.3f, 0.1f, out float low, out float high) && high > low)
                {
                    sumOld++;
                }
            }
        }
        watch.Stop();
        double then = watch.Elapsed.TotalMilliseconds;
        if (print)
        {
            Console.WriteLine("crew: " + Ways + " ways, each made and tested at 4 points of the body and on 4 sides of the tape: " + now.ToString("F1") + " ms as asked now, "
                              + then.ToString("F1") + " ms the plain way (" + (then / Math.Max(now, 0.001)).ToString("F0") + " times as long)");
        }
        Check(allocated == 0, "crew: a planning's worth of ways allocates nothing (" + allocated + " bytes)");
        Check(now < 40.0, "crew: and costs a few milliseconds, not seconds (" + now.ToString("F1") + " ms for " + Ways + " ways; the plain way " + then.ToString("F0") + " ms)");
        Check(then > now * 20.0, "crew: at least twenty times less than asking the tape point by point (" + (then / Math.Max(now, 0.001)).ToString("F0") + " times)");
        Check(Math.Abs(sumNew - sumOld) <= Ways / 50, "crew: and the ways found in the way are the same (" + sumNew + " tests met now, " + sumOld + " the plain way)");
    }

    // The work of a planning as it is asked now. part: 0 all of it, 1 only making the ways, 2 only the body, 3 only the tape.
    private static void CrewNow(CrewPath[] paths, float[] tape, float[] body, CrewPath scratch, float[] x, float[] z, int part, ref int met)
    {
        for (int w = 0; w < paths.Length; w++)
        {
            if (part == 0 || part == 1)
            {
                scratch.Build(x, z, x.Length, 1.2f, CrewPath.Lead + 0.05f, 1f);   // (making a way is part of what each costs)
            }
            CrewPath path = paths[w];
            for (int c = 0; c < 4; c++)
            {
                if ((part == 0 || part == 2) && path.Clearance(body[w * 8 + c * 2], body[w * 8 + c * 2 + 1]) < 0.2f)
                {
                    met++;
                }
                int d = (c + 1) & 3;
                if ((part == 0 || part == 3) && path.Crossing(tape[w * 8 + c * 2], tape[w * 8 + c * 2 + 1], tape[w * 8 + d * 2], tape[w * 8 + d * 2 + 1], 0.3f, out float low, out float high) && high > low)
                {
                    met++;
                }
            }
        }
    }
}
