namespace RealLifeOverhaul
{
    /// <summary>One vehicle of the traffic's pool as the bounce watch knows it (<see cref="TrafficWatchRules"/>).</summary>
    public struct BounceTrack
    {
        public float Since;        // when the close window began; 0 while none has
        public float StartX, StartZ;
        public float Height, Low, High;
        public sbyte Way;          // rising (1), falling (-1), not yet known (0)
        public byte Turns;         // changes between rising and falling in the window
        public byte Bouncing;      // windows in a row that said "bouncing"
        public float CoarseAt;     // when it was last looked at once a second; 0 never
        public float CoarseX, CoarseZ;
        public bool Watched;       // standing: looked at closely, five times a second
    }

    /// <summary>Where the watch reads a vehicle of the pool: false if it is off the road or frozen (nothing to watch).</summary>
    public interface IBounceSource
    {
        bool Read(int index, out float x, out float y, out float z);
    }

    /// <summary>
    /// The bounce watch's rules (Traffic/TrafficWatch): a vehicle the mod holds without driving or freezing bounces on the
    /// spot, because its springs have no damper and the traffic system steadies only its own cars. A bounce is a vehicle
    /// that stays within <see cref="MaxDrift"/> of where a window began while its height turns between rising and falling
    /// at least <see cref="MinTurns"/> times over at least <see cref="MinTravel"/>, for <see cref="Windows"/> windows of
    /// <see cref="Window"/> seconds in a row, looked at every <see cref="Interval"/>.
    ///
    /// Only a vehicle that stands can bounce in place, so every vehicle is looked at once a second (a fifth of the pool a
    /// look, <see cref="CoarseShare"/>), and only one that moved less than <see cref="StandingDrift"/> in that second is
    /// watched closely, as before. A car that starts bouncing is watched within two seconds and judged in the three that
    /// follow; one standing already is judged as soon as before. Moving traffic costs a fifth of the looks it did.
    /// No Unity or game types: checked offline (NavCheck, BounceCheck.cs).
    /// </summary>
    public static class TrafficWatchRules
    {
        public const float Interval = 0.2f;       // seconds between two looks
        public const float Window = 1.5f;         // seconds a vehicle is watched before it is judged
        public const int Windows = 2;             // ...and how many in a row must say "bouncing"
        public const float MinTravel = 0.06f;     // metres up and down within a window
        public const float MinStep = 0.008f;      // metres: a smaller change of height between two looks is no movement
        public const float MaxDrift = 1f;         // metres along the ground within a window: further than this it is driving, not hopping
        public const int MinTurns = 2;            // changes between rising and falling within a window
        public const int Slices = 5;              // looks in a second: the pool is gone through once a second, a fifth a look
        public const float StandingDrift = 2f;    // metres along the ground in a second under which a vehicle stands: watched closely

        /// <summary>How many vehicles of a pool of <paramref name="count"/> one look goes through coarsely.</summary>
        public static int CoarseShare(int count) => count <= 0 ? 0 : (count + Slices - 1) / Slices;

        /// <summary>
        /// One look: a share of the pool coarsely from <paramref name="cursor"/> (moved on), then every watched vehicle
        /// closely. The vehicles found bouncing for <see cref="Windows"/> windows are written to the three arrays (their
        /// index, how far they went up and down, the middle of the bounce), as many as fit; the count found.
        /// </summary>
        public static int Look(BounceTrack[] tracks, int count, ref int cursor, IBounceSource source, float now, int[] found, float[] travel, float[] middle)
        {
            int share = CoarseShare(count);
            for (int k = 0; k < share; k++)
            {
                cursor = cursor >= count - 1 ? 0 : cursor + 1;
                if (source.Read(cursor, out float x, out _, out float z))
                {
                    Coarse(ref tracks[cursor], x, z, now);
                }
                else
                {
                    Lost(ref tracks[cursor]);
                }
            }
            int bounced = 0;
            for (int i = 0; i < count; i++)
            {
                if (!tracks[i].Watched)
                {
                    continue;
                }
                if (!source.Read(i, out float x, out float y, out float z))
                {
                    Lost(ref tracks[i]);
                    continue;
                }
                if (Close(ref tracks[i], x, y, z, now, out float went, out float mid) && bounced < found.Length)
                {
                    found[bounced] = i;
                    travel[bounced] = went;
                    middle[bounced] = mid;
                    bounced++;
                }
            }
            return bounced;
        }

        /// <summary>A look once a second: true while the vehicle stands, and is watched closely.</summary>
        public static bool Coarse(ref BounceTrack t, float x, float z, float now)
        {
            bool known = t.CoarseAt > 0f;
            float dx = x - t.CoarseX;
            float dz = z - t.CoarseZ;
            t.CoarseAt = now;
            t.CoarseX = x;
            t.CoarseZ = z;
            bool standing = known && dx * dx + dz * dz < StandingDrift * StandingDrift;
            if (standing != t.Watched)
            {
                t.Watched = standing;
                t.Since = 0f;      // a fresh window at the next close look
                t.Bouncing = 0;
            }
            return t.Watched;
        }

        /// <summary>Off the road, or frozen: nothing to watch, and known afresh when it is back.</summary>
        public static void Lost(ref BounceTrack t)
        {
            t = default;
        }

        /// <summary>
        /// A close look: true when the vehicle has bounced in place for <see cref="Windows"/> windows in a row (then
        /// <paramref name="travel"/> and <paramref name="middle"/> say how far and about which height).
        /// </summary>
        public static bool Close(ref BounceTrack t, float x, float y, float z, float now, out float travel, out float middle)
        {
            travel = middle = 0f;
            if (t.Since <= 0f)
            {
                Begin(ref t, x, y, z, now);
                return false;
            }
            float step = y - t.Height;
            if (step > MinStep || step < -MinStep)
            {
                sbyte way = (sbyte)(step > 0f ? 1 : -1);
                if (t.Way != 0 && way != t.Way && t.Turns < byte.MaxValue)
                {
                    t.Turns++;
                }
                t.Way = way;
            }
            t.Height = y;
            t.Low = y < t.Low ? y : t.Low;
            t.High = y > t.High ? y : t.High;
            if (now - t.Since < Window)
            {
                return false;
            }
            travel = t.High - t.Low;
            float dx = x - t.StartX;
            float dz = z - t.StartZ;
            bool bouncing = t.Turns >= MinTurns && travel >= MinTravel && dx * dx + dz * dz < MaxDrift * MaxDrift;
            int running = bouncing ? t.Bouncing + 1 : 0;
            middle = (t.Low + t.High) * 0.5f;
            Begin(ref t, x, y, z, now);
            t.Bouncing = (byte)(running >= Windows ? 0 : running);
            return running >= Windows;
        }

        private static void Begin(ref BounceTrack t, float x, float y, float z, float now)
        {
            t.Since = now;
            t.StartX = x;
            t.StartZ = z;
            t.Height = t.Low = t.High = y;
            t.Way = 0;
            t.Turns = 0;
        }
    }
}
