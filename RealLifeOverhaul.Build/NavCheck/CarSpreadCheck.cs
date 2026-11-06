using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using RealLifeOverhaul;

// The driven car's sums spread over frames (docs/standards/performance.md, RLO-PERF-024; Driving/CarTuning): the handling's
// design a step at a time (HandlingModel.cs, HandlingDesign) and the search for the car's power a step at a time
// (CarModel.cs, CarSolve) give what the whole Design and Solve give, bit for bit, on every one of the game's cars at every
// level and with the sliders moved, with a whole design of another car between every two steps (a step leans on nothing
// the shared scratch held); each step is short; the frames CarTuning spreads them over, a slice of CarSolve.SliceMs each,
// stay well under 2 ms here; and the self-check line's verdict (Core/WorkSlice.cs, SpreadWatch) says FAIL past 2 ms.
// Done whole, the two were a frame of 15.2 ms in the owner's beta 6 log (28 Oct 2026) as the player got into a car.
internal static partial class Program
{
    private const double StepMs = 0.6;     // ms a step may take here at most (the slowest is about a third of that; Mono some three times slower)
    private const double SpreadMs = 1.5;   // ms a frame of the spread may take here at most: the slice and one step past it
    private const int Tries = 5;           // a time is the best of this many tries: the machine may be busy

    private static void CarSpread()
    {
        List<(string type, CarChassis car)> cars = GameCars();
        HandlingTuning[] tunings = { HandlingTuning.Neutral, new HandlingTuning(130, 70, 140, 80, 120, 130), new HandlingTuning(70, 150, 60, 120, 80, 70) };
        var whole = new HandlingSetup();
        var staged = new HandlingSetup();
        var other = new HandlingSetup();
        var design = new HandlingDesign();
        int compared = 0, same = 0, mostSteps = 0;
        string firstDiffer = null;
        foreach ((string type, CarChassis car) in cars)
        {
            CarKind kind = KindOf(type);
            foreach (HandlingTuning tuning in tunings)
            {
                foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
                {
                    HandlingTarget target = HandlingRules.Target(kind, level, tuning);
                    string why = HandlingModel.Design(car, target, whole);
                    design.Begin(car, target, staged);
                    int n = 0;
                    bool more = true;
                    while (more)
                    {
                        // A whole design of another car between two steps: whatever the model's shared scratch held is gone.
                        (string _, CarChassis between) = cars[(n * 7 + 3) % cars.Count];
                        HandlingModel.Design(between, HandlingRules.Target(CarKind.Saloon, HandlingLevel.Heavy, HandlingTuning.Neutral), other);
                        more = design.Step();
                        n++;
                    }
                    compared++;
                    mostSteps = Math.Max(mostSteps, n);
                    if (why == design.Why && SetupBits(whole) == SetupBits(staged) && n == design.Steps)
                    {
                        same++;
                    }
                    else
                    {
                        firstDiffer ??= type + " at " + level + " (sliders " + tuning.Stamp + ")";
                    }
                }
            }
        }
        Check(cars.Count >= 18 && compared == cars.Count * 9 && same == compared, "car spread: the handling's design a step at a time is the whole design bit for bit, a whole design of another car between every two steps ("
                                                  + same + " of " + compared + (firstDiffer != null ? "; not " + firstDiffer : "") + ")");
        Check(mostSteps <= 8 + 17, "car spread: a design takes at most " + mostSteps + " steps (8, and one more for each 5 km/h the game's car is slowed by in its hardest turn)");

        // A car the handling refuses is refused the same way a step at a time, in its first step.
        CarChassis odd = cars[0].car;
        float mass = odd.Mass;
        odd.Mass = 50f;
        HandlingTarget full = HandlingRules.Target(CarKind.Saloon, HandlingLevel.Full, HandlingTuning.Neutral);
        string refused = HandlingModel.Design(odd, full, whole);
        design.Begin(odd, full, staged);
        bool left = design.Step();
        Check(refused != null && refused == design.Why && !left && design.Steps == 1 && !design.Busy, "car spread: a car the handling refuses is refused in the first step (" + refused + ")");
        odd.Mass = mass;

        // Each step's time, the best of a few tries.
        double slowest = 0;
        string slowestAt = null;
        var watch = new Stopwatch();
        foreach ((string type, CarChassis car) in cars)
        {
            CarKind kind = KindOf(type);
            foreach (HandlingLevel level in new[] { HandlingLevel.Light, HandlingLevel.Full, HandlingLevel.Heavy })
            {
                HandlingTarget target = HandlingRules.Target(kind, level, tunings[2]);
                var best = new double[32];
                for (int t = 0; t < Tries; t++)
                {
                    design.Begin(car, target, staged);
                    for (int n = 0; n < best.Length; n++)
                    {
                        watch.Restart();
                        bool more = design.Step();
                        watch.Stop();
                        double ms = watch.Elapsed.TotalMilliseconds;
                        best[n] = t == 0 ? ms : Math.Min(best[n], ms);
                        if (!more)
                        {
                            break;
                        }
                    }
                }
                for (int n = 0; n < best.Length; n++)
                {
                    if (best[n] > slowest)
                    {
                        slowest = best[n];
                        slowestAt = type + " at " + level + ", step " + (n + 1);
                    }
                }
            }
        }
        Check(slowest <= StepMs, "car spread: the slowest step of a design takes " + slowest.ToString("F3") + " ms here (" + slowestAt + "; " + StepMs + " at most)");

        // The search for the power: the same as the whole search, in 24 steps at most, each short.
        CarPhysics physics = SpreadPhysics();
        var wholeSetup = new CarSetup();
        var stagedSetup = new CarSetup();
        var solve = new CarSolve();
        int searches = 0, alike = 0, mostSolveSteps = 0;
        double slowestSolve = 0;
        foreach (float kg in new[] { 900f, 1400f, 2400f, 9000f })
        {
            foreach (float seconds in new[] { 3f, 6f, 12f, 30f })
            {
                foreach (float top in new[] { 80f, 160f, 250f, 330f })
                {
                    foreach (bool eased in new[] { false, true })
                    {
                        physics.Mass = kg;
                        physics.TractionForce = kg * 9.81f * 1.2f;
                        bool solved = CarModel.Solve(physics, CarKind.Sports, seconds, top, eased, wholeSetup, out CarRun run);
                        var best = new double[32];
                        int n = 0;
                        for (int t = 0; t < Tries; t++)
                        {
                            solve.Begin(physics, CarKind.Sports, seconds, top, eased, stagedSetup);
                            n = 0;
                            bool more = true;
                            while (more && n < best.Length)
                            {
                                watch.Restart();
                                more = solve.Step();
                                watch.Stop();
                                best[n] = t == 0 ? watch.Elapsed.TotalMilliseconds : Math.Min(best[n], watch.Elapsed.TotalMilliseconds);
                                n++;
                            }
                        }
                        for (int i = 0; i < n; i++)
                        {
                            slowestSolve = Math.Max(slowestSolve, best[i]);
                        }
                        searches++;
                        mostSolveSteps = Math.Max(mostSolveSteps, n);
                        CarRun r = solve.Result;
                        if (solved == solve.Solved && run.To50 == r.To50 && run.To100 == r.To100 && run.KmhAfter20 == r.KmhAfter20 && run.TopKmh == r.TopKmh && n == solve.Steps
                            && wholeSetup.PowerKw == stagedSetup.PowerKw && wholeSetup.Drag == stagedSetup.Drag && wholeSetup.TyreLimited == stagedSetup.TyreLimited
                            && wholeSetup.UpshiftRpm == stagedSetup.UpshiftRpm && wholeSetup.DownshiftRpm == stagedSetup.DownshiftRpm && SameBits(wholeSetup.Ratios, stagedSetup.Ratios))
                        {
                            alike++;
                        }
                    }
                }
            }
        }
        Check(searches == 128 && alike == searches, "car spread: the search for a car's power a step at a time is the whole search bit for bit (" + alike + " of " + searches + ")");
        Check(mostSolveSteps == CarModel.Halvings + 2, "car spread: the search takes " + mostSolveSteps + " steps at most (the top of the range, 22 halvings, the setup found)");
        Check(slowestSolve <= StepMs, "car spread: the slowest step of the search takes " + slowestSolve.ToString("F3") + " ms here (" + StepMs + " at most)");

        // The frames CarTuning spreads both over: a slice of CarSolve.SliceMs a frame, a step more at most.
        double worstFrame = double.MaxValue;
        int frames = 0;
        string worstSaid = null;
        var spread = new SpreadWatch();
        for (int t = 0; t < Tries; t++)
        {
            spread.Begin();
            foreach ((string type, CarChassis car) in cars)
            {
                design.Begin(car, HandlingRules.Target(KindOf(type), HandlingLevel.Heavy, tunings[1]), staged);
                solve.Begin(physics, KindOf(type), 8f, 200f, true, stagedSetup);
                while (design.Busy || solve.Busy)
                {
                    long began = Stopwatch.GetTimestamp();
                    WorkSlice slice = WorkSlice.Of(CarSolve.SliceMs, 1);
                    while ((design.Busy || solve.Busy) && slice.More())
                    {
                        long item = Stopwatch.GetTimestamp();
                        if (design.Busy)
                        {
                            design.Step();
                        }
                        else
                        {
                            solve.Step();
                        }
                        spread.Item(0, item);
                    }
                    spread.Frame(began);
                }
            }
            if (spread.WorstMs < worstFrame)
            {
                worstFrame = spread.WorstMs;
                frames = spread.Frames;
                worstSaid = spread.Said(new[] { "a step" });
            }
        }
        Check(worstFrame <= SpreadMs, "car spread: every car's design and search spread a slice of " + CarSolve.SliceMs + " ms a frame: no frame over " + worstFrame.ToString("F2")
                                      + " ms here (" + SpreadMs + " at most; " + frames + " frames for " + cars.Count + " cars: " + worstSaid + ")");

        // The self-check line's verdict: PASS to 2 ms, FAIL past it; a frame of one item alone not held against work whose
        // items cannot be cut, a frame of two items held against any.
        Check(SpreadWatch.Verdict(1.99) == "PASS" && SpreadWatch.Verdict(2.01).StartsWith("FAIL, a frame of 2.0 ms", StringComparison.Ordinal),
            "car spread: the self-check line says PASS for a slowest frame of 1.99 ms and FAIL for one of 2.01 ms");
        var lone = new SpreadWatch();
        long frameBegan = Stopwatch.GetTimestamp();
        long one = Stopwatch.GetTimestamp();
        Spin(2.3);
        lone.Item(1, one);
        lone.Frame(frameBegan);
        bool loneOk = lone.Verdict(true) == "PASS" && lone.Verdict(false).StartsWith("FAIL", StringComparison.Ordinal);
        frameBegan = Stopwatch.GetTimestamp();
        one = Stopwatch.GetTimestamp();
        Spin(1.2);
        lone.Item(2, one);
        one = Stopwatch.GetTimestamp();
        Spin(1.2);
        lone.Item(3, one);
        lone.Frame(frameBegan);
        Check(loneOk && lone.Verdict(true).StartsWith("FAIL", StringComparison.Ordinal) && lone.Frames == 2 && lone.Items == 3 && lone.Said(new[] { "a", "b", "c", "d" }).Contains("(b)"),
            "car spread: a frame of one item over 2 ms passes only where items cannot be cut; a frame of two items over 2 ms fails everywhere; the line names the slowest item");
    }

    private static CarPhysics SpreadPhysics()
    {
        var physics = new CarPhysics
        {
            Mass = 1400f, WheelRadius = 0.35f, RollingForce = 200f, TractionForce = 1400f * 9.81f * 1.2f, RevLimitRpm = 7000f, IdleRpm = 800f, LaunchRpm = 2000f,
            DriveInertia = 0.3f, WheelInertiaMass = 40f, EngineLoss = 0.1f, Boost = 1f, ForwardGears = 6
        };
        for (int i = 0; i < CarPhysics.CurveSamples; i++)
        {
            float x = i / (float)(CarPhysics.CurveSamples - 1);
            physics.Curve[i] = Math.Min(1f, 0.2f + 1.2f * x - 0.4f * x * x);
        }
        return physics;
    }

    private static bool SameBits(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (BitConverter.SingleToInt32Bits(a[i]) != BitConverter.SingleToInt32Bits(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    // Every public field of a setup, its axles and its curves, to the bit.
    private static string SetupBits(object o, string prefix = "")
    {
        var sb = new StringBuilder();
        foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object v = f.GetValue(o);
            if (v is AxleSetup || v is CurveKeys)
            {
                sb.Append(SetupBits(v, prefix + f.Name + "."));
            }
            else if (v is float fl)
            {
                sb.Append(prefix).Append(f.Name).Append('=').Append(BitConverter.SingleToInt32Bits(fl)).Append(';');
            }
            else
            {
                sb.Append(prefix).Append(f.Name).Append('=').Append(v).Append(';');
            }
        }
        if (o is CurveKeys k)
        {
            for (int i = 0; i < k.Count; i++)
            {
                sb.Append(prefix).Append(i).Append('=').Append(BitConverter.SingleToInt32Bits(k.Time(i))).Append(',').Append(BitConverter.SingleToInt32Bits(k.Value(i))).Append(',')
                    .Append(BitConverter.SingleToInt32Bits(k.InSlope(i))).Append(',').Append(BitConverter.SingleToInt32Bits(k.OutSlope(i))).Append(';');
            }
        }
        return sb.ToString();
    }
}
