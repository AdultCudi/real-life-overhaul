using System;
using RealLifeOverhaul;

// Checks for the bounce watch's rules (Traffic/TrafficWatchRules.cs), driven as Traffic/TrafficWatch.cs drives them,
// a look every 0.2 s over a made-up pool: a car bouncing in place is still caught, from a stand or after it stopped,
// within the watch's windows plus the two seconds the once-a-second looks add; driving cars, a car crawling and a car
// standing still are never taken for one; a car off the road is forgotten and learnt again; and moving traffic costs a
// fifth of the looks it used to, standing traffic at most a fifth more.
internal static partial class Program
{
    // The pool as the watch reads it: each car's place now, or off the road.
    private sealed class FakePool : IBounceSource
    {
        public readonly float[] X, Y, Z;
        public readonly bool[] On;
        public int Reads;

        public FakePool(int count)
        {
            X = new float[count];
            Y = new float[count];
            Z = new float[count];
            On = new bool[count];
            for (int i = 0; i < count; i++)
            {
                On[i] = true;
            }
        }

        public bool Read(int index, out float x, out float y, out float z)
        {
            Reads++;
            x = X[index];
            y = Y[index];
            z = Z[index];
            return On[index];
        }
    }

    private delegate void CarAt(FakePool pool, int car, float t);

    private static void Bounces()
    {
        const float Hop = 0.05f;   // metres either way: 0.10 m up and down, over the watch's 0.06
        CarAt standing = (p, i, t) => { p.X[i] = i * 10f; p.Z[i] = 0f; p.Y[i] = Hop * (float)Math.Sin(2.0 * Math.PI * 1.5 * t); };
        CarAt driving = (p, i, t) => { p.X[i] = i * 10f; p.Z[i] = 10f * t; p.Y[i] = Hop * (float)Math.Sin(2.0 * Math.PI * 1.5 * t); };
        CarAt crawling = (p, i, t) => { p.X[i] = i * 10f; p.Z[i] = 0.5f * t; p.Y[i] = 0f; };
        CarAt still = (p, i, t) => { p.X[i] = i * 10f; p.Z[i] = 0f; p.Y[i] = 0f; };
        CarAt stopsThenBounces = (p, i, t) =>
        {
            p.X[i] = i * 10f;
            p.Z[i] = t < 4f ? 10f * t : 40f;
            p.Y[i] = t < 4f ? 0f : Hop * (float)Math.Sin(2.0 * Math.PI * 1.5 * (t - 4f));
        };

        float caught = Watch(40, 0, standing, driving, 12f, out _, out int falseAlarms);
        Check(caught > 0f && caught <= TrafficWatchRules.Window * TrafficWatchRules.Windows + 2.2f + TrafficWatchRules.Interval,
            "bounce watch: a car bouncing in place among moving traffic is caught (" + caught.ToString("F1") + " s)");
        Check(falseAlarms == 0, "bounce watch: thirty-nine cars driving past are never taken for a bounce (" + falseAlarms + ")");
        float old = OldWatch(standing, 12f);
        Check(old > 0f && caught <= old + 2.2f,
            "bounce watch: caught at most two seconds after the watch that looked at every car (" + caught.ToString("F1") + " s against " + old.ToString("F1") + " s)");

        float later = Watch(40, 0, stopsThenBounces, driving, 14f, out _, out falseAlarms);
        Check(later > 4f && later <= 4f + TrafficWatchRules.Window * TrafficWatchRules.Windows + 2.2f + TrafficWatchRules.Interval && falseAlarms == 0,
            "bounce watch: a car that drives, stops and bounces is caught after it stops (" + later.ToString("F1") + " s)");
        Check(Watch(40, 0, crawling, driving, 20f, out _, out falseAlarms) < 0f && falseAlarms == 0, "bounce watch: a car crawling without a bounce is never taken for one");
        Check(Watch(40, 0, still, still, 20f, out _, out falseAlarms) < 0f && falseAlarms == 0, "bounce watch: cars standing still are never taken for a bounce");
        Check(Watch(40, 0, driving, driving, 20f, out _, out falseAlarms) < 0f && falseAlarms == 0, "bounce watch: a car bouncing as it drives is driving, not hopping");

        // Off the road for a while, then back and bouncing: forgotten, learnt again, caught.
        var pool = new FakePool(10);
        var tracks = new BounceTrack[10];
        int cursor = 0;
        float found = -1f;
        var index = new int[4];
        var travel = new float[4];
        var middle = new float[4];
        for (int tick = 1; tick <= 100 && found < 0f; tick++)
        {
            float t = tick * TrafficWatchRules.Interval;
            for (int i = 0; i < 10; i++)
            {
                standing(pool, i, t);
            }
            for (int i = 0; i < 10; i++)
            {
                pool.On[i] = i == 3 && (t < 2f || t > 6f);   // only car 3 is on the road, and not from 2 s to 6 s
            }
            int n = TrafficWatchRules.Look(tracks, 10, ref cursor, pool, t, index, travel, middle);
            if (n > 0 && index[0] == 3)
            {
                found = t;
            }
            if (t > 3.3f && t < 6f)   // a full round of the once-a-second looks after it left
            {
                Check(!tracks[3].Watched && tracks[3].CoarseAt == 0f, "bounce watch: a car off the road is forgotten");
            }
        }
        Check(found > 6f && found <= 6f + TrafficWatchRules.Window * TrafficWatchRules.Windows + 2.2f + TrafficWatchRules.Interval,
            "bounce watch: a car back on the road and bouncing is caught (" + found.ToString("F1") + " s)");

        // The cost: positions read a second, against five a car a second before.
        Watch(40, -1, driving, driving, 10f, out int movingReads, out _);
        Watch(40, -1, still, still, 10f, out int standingReads, out _);
        Check(movingReads <= 40 * 5 * 10 / 4, "bounce watch: moving traffic is read a fifth as often (" + movingReads + " reads in 10 s, was " + 40 * 5 * 10 + ")");
        Check(standingReads <= 40 * 5 * 10 * 6 / 5, "bounce watch: standing traffic is read at most a fifth more often (" + standingReads + " reads in 10 s)");
        Check(TrafficWatchRules.CoarseShare(0) == 0 && TrafficWatchRules.CoarseShare(1) == 1 && TrafficWatchRules.CoarseShare(40) == 8 && TrafficWatchRules.CoarseShare(41) == 9,
            "bounce watch: a look goes through a fifth of the pool, rounded up");
    }

    // A pool of 'count' cars, 'watched' placed by 'it', the others by 'others', looked at every 0.2 s for 'seconds' as the
    // watch does: when 'watched' was found bouncing (-1 if never), the reads made, and how many others were taken for one.
    private static float Watch(int count, int watched, CarAt it, CarAt others, float seconds, out int reads, out int falseAlarms)
    {
        var pool = new FakePool(count);
        var tracks = new BounceTrack[count];
        var index = new int[4];
        var travel = new float[4];
        var middle = new float[4];
        int cursor = 0;
        float found = -1f;
        falseAlarms = 0;
        int ticks = (int)Math.Round(seconds / TrafficWatchRules.Interval);
        for (int tick = 1; tick <= ticks; tick++)
        {
            float t = tick * TrafficWatchRules.Interval;
            for (int i = 0; i < count; i++)
            {
                (i == watched ? it : others)(pool, i, t);
            }
            int n = TrafficWatchRules.Look(tracks, count, ref cursor, pool, t, index, travel, middle);
            for (int k = 0; k < n; k++)
            {
                if (index[k] == watched)
                {
                    found = found < 0f ? t : found;
                    Check(travel[k] >= TrafficWatchRules.MinTravel && Math.Abs(middle[k]) < 0.02f, "bounce watch: the bounce's travel and middle are reported");
                }
                else
                {
                    falseAlarms++;
                }
            }
        }
        reads = pool.Reads;
        return found;
    }

    // The watch as it was: every car looked at closely at every look.
    private static float OldWatch(CarAt it, float seconds)
    {
        var track = default(BounceTrack);
        var pool = new FakePool(1);
        int ticks = (int)Math.Round(seconds / TrafficWatchRules.Interval);
        for (int tick = 1; tick <= ticks; tick++)
        {
            float t = tick * TrafficWatchRules.Interval;
            it(pool, 0, t);
            if (TrafficWatchRules.Close(ref track, pool.X[0], pool.Y[0], pool.Z[0], t, out _, out _))
            {
                return t;
            }
        }
        return -1f;
    }
}
