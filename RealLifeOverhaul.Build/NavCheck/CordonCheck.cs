using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// Checks for who is inside a cordon's tape and where they are put outside it (Traffic/CordonRules.cs, compiled as it
// ships), and, over the sources, that every cordon the mod puts up runs that rule on the frame its tape goes up.
// The owner's report of 15 Oct 2026: at "Pedestrian down" on 4th Avenue three pedestrians stood inside the tape, on top
// of the casualty. The scene lay on a crossing, so its tape was left without walls ("not walled (a crossing runs
// through it)" in Player.log), people crossed the road straight through it, and nobody already inside when the tape
// went up was moved. Now the tape is walled at a crossing too, a bar shuts any opening a walkway runs through, and
// whoever is inside (but the scene's own people) is put at the nearest place outside, a body's width apart.
//
// What is not here: the street. Whether a place outside has ground a person can stand on is asked of the engine
// (People/CordonClear); the "Cordon check (cleared)" line of Player.log says who was moved and how far.
internal static partial class Program
{
    private static void Cordons()
    {
        CordonInside();
        CordonExits();
        CordonExitsAtRandom();
        CordonSpread();
        CordonGaps();
        CordonCost();
        CordonSources();
    }

    private static readonly CordonBox Taped = new CordonBox(-6f, 7f, -3f, 3.5f);   // 13 m along the road, 6.5 m across it

    private static void CordonInside()
    {
        Check(CordonRules.Inside(Taped, 0f, 0f) && CordonRules.Inside(Taped, -5.9f, 3.4f), "cordon: a point within the tape is inside");
        Check(CordonRules.Inside(Taped, 0f, 3.5f + CordonRules.SideReach - 0.01f) && !CordonRules.Inside(Taped, 0f, 3.5f + CordonRules.SideReach + 0.01f),
            "cordon: somebody against a side's tape (within its wall's reach) is inside; a step beyond it is not");
        Check(CordonRules.Inside(Taped, 7f + CordonRules.EndReach - 0.01f, 0f) && !CordonRules.Inside(Taped, 7f + CordonRules.EndReach + 0.01f, 0f)
              && CordonRules.Inside(Taped, -6f - CordonRules.EndReach + 0.01f, 0f) && !CordonRules.Inside(Taped, -6f - CordonRules.EndReach - 0.01f, 0f),
            "cordon: somebody in front of an end's barriers (within its deeper wall's reach) is inside; beyond it is not");
        Check(CordonRules.EndReach > 1.22f + 0.3f && CordonRules.SideReach > 0.15f + 0.3f, "cordon: the reach of a wall covers the wall itself and a walker's radius (an end's wall is 1.22 m deep)");
        Check(CordonRules.SideClear > CordonRules.SideReach + 0.3f && CordonRules.EndClear > CordonRules.EndReach + 0.2f, "cordon: a place outside is a step beyond the walls' reach");
        var point = new CordonBox(0f, 0f, 0f, 0f);
        Check(CordonRules.Inside(point, 0f, 0f) && !CordonRules.Inside(point, 0f, 1f), "cordon: a tape round nothing still has its walls' reach");
    }

    private static readonly float[] ExitAlong = new float[CordonRules.Exits];
    private static readonly float[] ExitAcross = new float[CordonRules.Exits];
    private static readonly bool[] ExitSide = new bool[CordonRules.Exits];

    private static void CordonExits()
    {
        float first = CordonRules.Out(Taped, 0f, 2.5f, ExitAlong, ExitAcross, ExitSide);
        Check(Near(ExitAlong[0], 0f) && Near(ExitAcross[0], 3.5f + CordonRules.SideClear) && ExitSide[0] && Near(first, 1f + CordonRules.SideClear),
            "cordon: somebody near the right side is put straight out through it, a step beyond its reach");
        CordonRules.Out(Taped, -5.5f, 0.5f, ExitAlong, ExitAcross, ExitSide);
        Check(Near(ExitAlong[0], -6f - CordonRules.EndClear) && Near(ExitAcross[0], 0.5f) && !ExitSide[0], "cordon: somebody near the near end is put straight out through it, beyond its barriers");
        bool apart = true;
        for (int i = 0; i < CordonRules.Exits; i++)
        {
            apart &= !CordonRules.Inside(Taped, ExitAlong[i], ExitAcross[i]);
        }
        Check(apart, "cordon: every one of the four places is outside the tape and its walls");
        var square = new CordonBox(-3f, 3f, -3f, 3f);
        CordonRules.Out(square, 0f, 0f, ExitAlong, ExitAcross, ExitSide);
        Check(ExitAcross[0] < 0f && ExitAcross[1] > 0f && ExitAlong[2] < -3f && ExitAlong[3] > 3f, "cordon: a tie between sides is settled in a fixed order (left, right, then the ends): the same on every machine");
        // On top of the casualty, in the middle of a crossing that runs across the road: out to the nearer side.
        CordonRules.Out(Taped, 0.5f, 0.4f, ExitAlong, ExitAcross, ExitSide);
        Check(ExitSide[0] && ExitAcross[0] > 3.5f, "cordon: three people on the casualty in the middle of the road go out through the nearer side");
        CordonRules.Outside(Taped, 0f, 2.5f, 1.2f, out float outAlong, out float outAcross);
        CordonRules.Outside(Taped, 0f, 9f, 1.2f, out float stayAlong, out float stayAcross);
        Check(Near(outAlong, 0f) && Near(outAcross, 3.5f + 1.2f) && Near(stayAlong, 0f) && Near(stayAcross, 9f), "cordon: a place to wait at is put out over the nearest side by its margin; one outside stays where it is");
    }

    // Many people at random inside many tapes: the first place is always the nearest, straight out, and none is inside.
    private static void CordonExitsAtRandom()
    {
        var random = new Random(20261015);
        int wrong = 0;
        string first = null;
        long sum = 0;
        for (int n = 0; n < 20000; n++)
        {
            float back = -2f - (float)random.NextDouble() * 15f, front = 2f + (float)random.NextDouble() * 15f;
            float left = -1f - (float)random.NextDouble() * 6f, right = 1f + (float)random.NextDouble() * 6f;
            var box = new CordonBox(back, front, left, right);
            float along = back - 1.5f + (float)random.NextDouble() * (front - back + 3f);
            float across = left - 0.5f + (float)random.NextDouble() * (right - left + 1f);
            float near = CordonRules.Out(box, along, across, ExitAlong, ExitAcross, ExitSide);
            float least = Math.Min(Math.Min(across - (left - CordonRules.SideClear), right + CordonRules.SideClear - across),
                Math.Min(along - (back - CordonRules.EndClear), front + CordonRules.EndClear - along));
            bool straight = ExitSide[0] ? Near(ExitAlong[0], along, 1e-4f) : Near(ExitAcross[0], across, 1e-4f);
            bool outside = true;
            float last = -1f;
            bool rising = true;
            for (int i = 0; i < CordonRules.Exits; i++)
            {
                outside &= !CordonRules.Inside(box, ExitAlong[i], ExitAcross[i]);
                float far = Math.Abs(ExitAlong[i] - along) + Math.Abs(ExitAcross[i] - across);
                rising &= far >= last - 1e-4f;
                last = far;
                sum += (long)(ExitAlong[i] * 1000f) + (long)(ExitAcross[i] * 1000f);
            }
            if (!Near(near, least, 1e-4f) || !straight || !outside || !rising)
            {
                wrong++;
                first = first ?? $"at ({along:F2}, {across:F2}) in [{back:F1}..{front:F1}] x [{left:F1}..{right:F1}]: nearest {near:F2} of {least:F2}, straight {straight}, outside {outside}, nearest first {rising}";
            }
        }
        Check(wrong == 0, "cordon: over 20,000 people inside tapes of every size, the first place is the nearest, straight out, every place outside and nearest first" + (first != null ? " (" + first + ")" : ""));
        var again = new Random(20261015);
        long sum2 = 0;
        for (int n = 0; n < 20000; n++)
        {
            float back = -2f - (float)again.NextDouble() * 15f, front = 2f + (float)again.NextDouble() * 15f;
            float left = -1f - (float)again.NextDouble() * 6f, right = 1f + (float)again.NextDouble() * 6f;
            var box = new CordonBox(back, front, left, right);
            float along = back - 1.5f + (float)again.NextDouble() * (front - back + 3f);
            float across = left - 0.5f + (float)again.NextDouble() * (right - left + 1f);
            CordonRules.Out(box, along, across, ExitAlong, ExitAcross, ExitSide);
            for (int i = 0; i < CordonRules.Exits; i++)
            {
                sum2 += (long)(ExitAlong[i] * 1000f) + (long)(ExitAcross[i] * 1000f);
            }
        }
        Check(sum == sum2, "cordon: the same people in the same tape are put in the same places every time");
    }

    private static void CordonSpread()
    {
        // Three people standing on top of one another (and of the casualty): side by side at the tape, a body's width apart.
        var takenAlong = new float[8];
        var takenAcross = new float[8];
        int taken = 0;
        bool ok = true, firstStays = true;
        for (int person = 0; person < 3; person++)
        {
            CordonRules.Out(Taped, 0.5f, 0.4f, ExitAlong, ExitAcross, ExitSide);
            float along = ExitAlong[0], across = ExitAcross[0];
            ok &= CordonRules.Spread(Taped, ExitSide[0], ref along, ref across, takenAlong, takenAcross, taken);
            ok &= !CordonRules.Inside(Taped, along, across) && Near(across, ExitAcross[0]);
            firstStays &= person > 0 || (Near(along, ExitAlong[0]) && Near(across, ExitAcross[0]));
            takenAlong[taken] = along;
            takenAcross[taken] = across;
            taken++;
        }
        float least = float.MaxValue;
        for (int i = 0; i < taken; i++)
        {
            for (int j = i + 1; j < taken; j++)
            {
                float a = takenAlong[i] - takenAlong[j], c = takenAcross[i] - takenAcross[j];
                least = Math.Min(least, (float)Math.Sqrt(a * a + c * c));
            }
        }
        Check(ok && firstStays && least >= CordonRules.Spacing - 0.001f, $"cordon: three people on the casualty end up outside, side by side along the tape, {least:F2} m apart at the least ({CordonRules.Spacing:F2} wanted)");
        Check(Near(takenAlong[1], takenAlong[0] + CordonRules.Spacing) && Near(takenAlong[2], takenAlong[0] - CordonRules.Spacing), "cordon: whoever comes later is slid a step one way, then the other");

        // Beyond an end, people are slid across the road, not along it (into the tape).
        float endAlong = -6f - CordonRules.EndClear, endAcross = 0f;
        takenAlong[0] = endAlong;
        takenAcross[0] = endAcross;
        bool slid = CordonRules.Spread(Taped, false, ref endAlong, ref endAcross, takenAlong, takenAcross, 1);
        Check(slid && Near(endAlong, -6f - CordonRules.EndClear) && Near(Math.Abs(endAcross), CordonRules.Spacing), "cordon: beyond an end, the next person is slid across the road along the barriers");

        float lone = 2f, loneAcross = 3.5f + CordonRules.SideClear;
        Check(CordonRules.Spread(Taped, true, ref lone, ref loneAcross, takenAlong, takenAcross, 0) && lone == 2f && loneAcross == 3.5f + CordonRules.SideClear,
            "cordon: the first person put outside stays where the rule put them");
        float stray = 0f, strayAcross = 3.6f;   // a place handed in that is inside the walls' reach is never given out
        Check(!CordonRules.Spread(Taped, false, ref stray, ref strayAcross, takenAlong, takenAcross, 0) || CordonRules.Inside(Taped, stray, strayAcross) == false,
            "cordon: a place inside the walls' reach is not given out as it is");

        // No room left along the tape: said, and the place left as it was.
        var crowdAlong = new float[400];
        var crowdAcross = new float[400];
        for (int i = 0; i < 400; i++)
        {
            crowdAlong[i] = -20f + i * 0.1f;
            crowdAcross[i] = 3.5f + CordonRules.SideClear;
        }
        float fullAlong = 0f, fullAcross = 3.5f + CordonRules.SideClear;
        Check(!CordonRules.Spread(Taped, true, ref fullAlong, ref fullAcross, crowdAlong, crowdAcross, 400) && fullAlong == 0f, "cordon: with the tape lined all along, no place is made up: the person is said to be left where they stand");

        // A place slid along a side never comes back inside the tape, even from a corner.
        float cornerAlong = -6.2f, cornerAcross = 3.5f + CordonRules.SideClear;
        takenAlong[0] = cornerAlong;
        takenAcross[0] = cornerAcross;
        bool corner = CordonRules.Spread(Taped, true, ref cornerAlong, ref cornerAcross, takenAlong, takenAcross, 1);
        Check(corner && !CordonRules.Inside(Taped, cornerAlong, cornerAcross), "cordon: slid round a corner, a place stays outside the tape");
    }

    private static void CordonGaps()
    {
        var open = new bool[64];
        var from = new float[4];
        var to = new float[4];
        int count = CordonRules.Points(10f);
        Check(count == 21 && Near(CordonRules.At(20, count, 10f), 10f) && Near(CordonRules.At(3, count, 10f), 1.5f), "cordon gaps: a 10 m line is looked at every half metre, both ends included");
        int odd = CordonRules.Points(10.2f);
        Check(odd == 22 && Near(CordonRules.At(21, odd, 10.2f), 10.2f) && CordonRules.Points(0f) == 1 && Near(CordonRules.At(0, 1, 0f), 0f), "cordon gaps: the last point is the line's end, whatever its length; a line of no length is one point");
        Check(CordonRules.Gaps(open, count, 10f, from, to, 4) == 0, "cordon gaps: no walkway through the tape, no bar");
        for (int i = 0; i < count; i++)
        {
            open[i] = true;
        }
        int runs = CordonRules.Gaps(open, count, 10f, from, to, 4);
        Check(runs == 1 && Near(from[0], 0f) && Near(to[0], 10f), "cordon gaps: a walkway along the whole line is one bar, kept within the line");
        Array.Clear(open, 0, open.Length);
        open[4] = open[5] = open[6] = true;   // 2 to 3 m: an opening on a crossing
        runs = CordonRules.Gaps(open, count, 10f, from, to, 4);
        Check(runs == 1 && Near(from[0], 2f - CordonRules.GatePad) && Near(to[0], 3f + CordonRules.GatePad), "cordon gaps: an opening a walkway runs through gets a bar reaching half a metre past it either side");
        open[16] = true;   // 8 m: a second, apart from the first
        runs = CordonRules.Gaps(open, count, 10f, from, to, 4);
        Check(runs == 2 && Near(from[1], 8f - CordonRules.GatePad) && Near(to[1], 8f + CordonRules.GatePad), "cordon gaps: two openings apart are two bars");
        open[8] = true;    // 4 m: its pad touches the first run's
        runs = CordonRules.Gaps(open, count, 10f, from, to, 4);
        Check(runs == 2 && Near(to[0], 4f + CordonRules.GatePad), "cordon gaps: runs whose bars would touch are one bar");
        runs = CordonRules.Gaps(open, count, 10f, from, to, 1);
        Check(runs == 1 && Near(from[0], 1.5f) && Near(to[0], 8f + CordonRules.GatePad), "cordon gaps: with one bar left, it reaches over every opening, so none is left open");
        Check(CordonRules.Gaps(open, count, 10f, from, to, 0) == 0, "cordon gaps: with no bar left, none is made");
        Array.Clear(open, 0, open.Length);
        open[0] = true;
        runs = CordonRules.Gaps(open, 1, 0f, from, to, 4);
        Check(runs == 1 && Near(from[0], 0f) && Near(to[0], 0f), "cordon gaps: a line of one point on a walkway is one bar of no length (made half a metre long where it stands)");
    }

    private static void CordonCost()
    {
        long bytes = CordonAsks(out long ms);
        Check(bytes == 0, $"cordon: sorting 100,000 people and placing them allocates nothing ({bytes} bytes)");
        Check(ms < 200, $"cordon: sorting 100,000 people and placing them takes {ms} ms (200 at the most; a raising has a few dozen)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long CordonAsks(out long ms)
    {
        var takenAlong = new float[48];
        var takenAcross = new float[48];
        Stopwatch watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int taken = 0;
        for (int n = 0; n < 100000; n++)
        {
            float along = -6f + (n % 130) * 0.1f, across = -3f + (n % 65) * 0.1f;
            if (!CordonRules.Inside(Taped, along, across))
            {
                continue;
            }
            CordonRules.Out(Taped, along, across, ExitAlong, ExitAcross, ExitSide);
            float a = ExitAlong[0], c = ExitAcross[0];
            if (CordonRules.Spread(Taped, ExitSide[0], ref a, ref c, takenAlong, takenAcross, taken))
            {
                takenAlong[taken] = a;
                takenAcross[taken] = c;
                taken = (taken + 1) % 48;
            }
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.ElapsedMilliseconds;
        return allocated;
    }

    // Over the sources as they ship: every cordon's tape goes up in one place, and that place clears it.
    private static void CordonSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string cordon = Source(scripts, @"Traffic\SceneCordon.cs");
        if (cordon.Length == 0)
        {
            Check(false, "cordon: the mod's sources are at " + scripts);
            return;
        }
        string dress = Body(cordon, "private void Dress(float now)");
        int shown = dress.IndexOf("_props.SetActive(true)", StringComparison.Ordinal);
        int cleared = dress.IndexOf("CordonClear.Out(this", StringComparison.Ordinal);
        Check(shown >= 0 && Count(cordon, "_props.SetActive(true)") == 1, "cordon: the tape, its walls and bars are shown in one place only (SceneCordon.Dress)");
        Check(cleared > shown, "cordon: whoever is inside the tape is put outside it on the frame it goes up, after its walls (SceneCordon.Dress calls CordonClear.Out)");
        Check(dress.Contains("bool walled = _wallCount > 0;") && !dress.Contains("_crossing &&") && !dress.Contains("!_crossing"),
            "cordon: the tape is walled where a crossing runs through it as everywhere else");
        Check(dress.Contains("_barCount = walled ? Bar() : 0;") && Body(cordon, "private int Bar()").Contains("CordonRules.Gaps("),
            "cordon: every opening a walkway runs through gets a bar (SceneCordon.Bar, CordonRules.Gaps)");
        Check(Body(cordon, "public void Raise(float now)").Contains("Dress(now)") && Body(cordon, "public void SetCorridor(").Contains("Dress(Time.time)"),
            "cordon: raising the tape and running it again round the crew's way both go through Dress");
        string survey = Body(cordon, "private void Survey()");
        Check(survey.Contains("Kerbside = KeepOutside(") && survey.Contains("_corners[i] = KeepOutside("), "cordon: the places handed to people who only wait (the kerb, the corners) are outside the tape");
        Check(Body(cordon, "public bool Shuts(Vector3 point)").Contains("CordonRules.Inside(") && Body(cordon, "public Vector3 KeepOutside(").Contains("CordonRules.Outside("),
            "cordon: inside and outside are the one rule's (Traffic/CordonRules)");

        // Every scene with a cordon puts its tape up through SceneCordon.Raise, and nothing else puts up tape.
        int raisers = 0;
        string others = null;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            if (name == "SceneCordon.cs")
            {
                continue;
            }
            string code = Code(File.ReadAllText(path));
            if (code.Contains("\"tape_post\"") || code.Contains("\"tape_4m"))
            {
                others = (others == null ? "" : others + ", ") + name;
            }
            if (code.Contains("SceneCordon") || code.Contains("Cordon.Raise(") || code.Contains("cordon.Raise("))
            {
                raisers += code.Contains("ordon.Raise(now)") ? 1 : 0;
            }
        }
        Check(others == null, "cordon: no file but SceneCordon puts up tape" + (others != null ? " (" + others + ")" : ""));
        Check(raisers >= 3, $"cordon: the scenes that put up a cordon (accidents, fires, street events) raise it through SceneCordon.Raise ({raisers} found)");

        string clear = Source(scripts, @"People\CordonClear.cs");
        string run = Body(clear, "public static void Out(");
        Check(run.Contains("Walkers();") && run.Contains("CityLife();") && run.Contains("Presence();"), "cordon: the game's pedestrians, the living city's people and the officers only there to be seen are all looked at");
        Check(Body(clear, "private static void Walkers()").Contains("activePedestrians") && Body(clear, "private static void Walkers()").Contains("!walker.isActiveAndEnabled"),
            "cordon: the game's pedestrians come from its own list, and one the mod has borrowed (a casualty) is never moved");
        Check(Body(clear, "private static void Presence()").Contains("!actor.Yields"), "cordon: the scene's own people (the casualty, the crews, the officers) are never moved");
        Check(!clear.Contains("void Update(") && !clear.Contains("FindObjects") && !clear.Contains("SceneFinds."), "cordon: clearing runs only when the tape goes up: no frame of its own and no search of the scene");
        Check(Body(clear, "private static bool Put(").Contains("CordonRules.Spread(") && Body(clear, "private static bool Put(").Contains("CordonRules.Apart("),
            "cordon: two people are never put on top of one another");

        Check(Body(Source(scripts, @"Events\FireScene.cs"), "private void Watchers(").Contains("Cordon.Shuts("), "cordon: a fire's onlookers watch from outside the tape");
        Check(Body(Source(scripts, @"Events\StreetEventScene.cs"), "private void Onlooker()").Contains("Cordon.Shuts("), "cordon: a street event's onlookers watch from outside the tape");
    }
}
