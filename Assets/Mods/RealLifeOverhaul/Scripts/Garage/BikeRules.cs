using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How a bicycle or a motorbike rides, as numbers with nothing of the game or the engine in them, so that NavCheck
    /// (BikeCheck.cs) checks the very code <see cref="BikeMount"/> rides by.
    ///
    /// PEDALLING (<see cref="Pedal"/>). A bicycle is pushed by its rider, not by the scooter's engine: the scooter's
    /// drivetrain is let go (<see cref="FreeWheelRatio"/>), because through its gearbox the engine and the gearbox weigh on
    /// the rear wheel like another 275 to 600 kg, and its clutch slips and grabs up to 18 km/h; that was the slow, juddering
    /// start. The push is the rider's: as hard as <see cref="Ride.StartPush"/> from a standstill (a hard push on the pedals),
    /// and no more than the rider's power can give as the speed rises. Air drag is whatever is left of that power at the
    /// cruising speed, so steady pedalling settles there. A tired rider pushes half as hard. Letting go coasts: nothing
    /// holds the wheels but their rolling and the air. Holding the brake at a standstill walks it back, slowly.
    ///
    /// THE POSE (<see cref="Want"/>). Riding (pedalling, on a bicycle), freewheeling, or stopped with a foot down. Pushing
    /// off is at once; any other change has to last <see cref="HoldFor"/> first, so that a dab of the throttle or a crawl
    /// does not flick the pose; between two poses the rider's clips and legs cross over, never jump (<see cref="Smooth"/>).
    ///
    /// THE LEAN (<see cref="LeanMost"/>, <see cref="LeanRate"/>). The game's scooter leans its full 33 degrees from 1 m/s
    /// and swings there at 220 degrees a second: at a walking pace a bicycle flopped over with every touch of the steering
    /// and snapped back. A two-wheeler leans with its speed, as one does (full lean from <see cref="FullLeanAt"/>), and
    /// gently at a crawl.
    ///
    /// THE BODY (<see cref="Frame"/>, <see cref="Bars"/>). What the world meets. The scooter's own box is 0.32 m wide and
    /// 1.06 m long, under its deck: a bicycle's wheels and a motorbike's both stuck out of it by half a metre, and its bars
    /// and the rider's knees out of its sides, into whatever they met. The frame box runs from wheel to wheel, high enough
    /// off the road for kerbs and narrow enough to stay off it at full lean (<see cref="LeanClearance"/>); the bars box
    /// is the bars and the rider's knees, the width of the grips.
    ///
    /// THE MASS (<see cref="KeepMass"/>, <see cref="PutMassBack"/>, <see cref="Turns"/>). The body's centre of mass and
    /// inertia are the game's, kept while the boxes change and written back after. The game holds a scooter nobody rides
    /// frozen, and the engine freezes a body's turning by setting its inertia to nothing, which written onto a ridden bike
    /// is one that steers and goes straight on: they are kept and written back only while the body is free to turn.
    ///
    /// Everything here is a handful of multiplications, asked once a physics step or once a frame while a two-wheeler is
    /// ridden, or once when one is made or got on. Nothing is allocated.
    /// </summary>
    public static class BikeRules
    {
        /// <summary>One bicycle's push: what its rider can give, on the game's 250 kg scooter body.</summary>
        public struct Ride
        {
            public float CruiseKmh;     // what steady pedalling holds on the flat
            public float PowerPerKg;    // W per kg of the body: the rider's power
            public float StartPush;     // m/s2: the hardest push, from a standstill

            public Ride(float cruiseKmh, float powerPerKg, float startPush)
            {
                CruiseKmh = cruiseKmh;
                PowerPerKg = powerPerKg;
                StartPush = startPush;
            }
        }

        public static readonly Ride City = new Ride(38f, 20f, 3.0f);   // to walking pace in half a second, 25 km/h in under 3, 34 in about 5
        public static readonly Ride Road = new Ride(50f, 28f, 3.2f);   // a lighter push on a lighter bike: 25 km/h in under 2.5, 45 in about 6

        public const float ScooterMass = 250f;          // kg: the game's scooter, every two-wheeler's body
        public const float FreeWheelRatio = 0.01f;      // the bicycle's final drive: the engine and gearbox's weight on the wheel, times its square, is gone
        public const float LeastSpeed = 0.1f;           // m/s: the power's push is reckoned from here up (no infinite push at a standstill)
        public const float BackPush = 1.2f;             // m/s2: walking it back
        public const float BackMost = 1.0f;             // m/s backwards, a slow walk
        public const float WalkingPace = 1.4f;          // m/s, 5 km/h

        // ---------------------------------------------------------------- pedalling

        /// <summary>
        /// The air's hold, N per (m/s) squared, that makes steady pedalling settle at the ride's cruising speed: what is left
        /// of the rider's power there once the tyres' rolling (N) is paid.
        /// </summary>
        public static float Drag(Ride ride, float mass, float rolling)
        {
            float cruise = ride.CruiseKmh / 3.6f;
            float push = ride.PowerPerKg * mass / cruise - rolling;
            return push > 0f ? push / (cruise * cruise) : 0f;
        }

        /// <summary>
        /// The rider's push along the bike (N) at this forward speed (m/s), with the throttle (0 to 1) and the effort
        /// (1 fresh, 0.5 worn out). Backwards (<paramref name="back"/>: the brake held in the gearbox's reverse) a slow walk
        /// back instead. Zero with nothing held.
        /// </summary>
        public static float Pedal(Ride ride, float mass, float along, float throttle, float effort, float back)
        {
            if (throttle > 0.1f)
            {
                float push = ride.StartPush * mass;
                float power = ride.PowerPerKg * mass / (along > LeastSpeed ? along : LeastSpeed);
                return Clamp01(throttle) * Clamp01(effort) * (power < push ? power : push);
            }
            if (back > 0.1f && along > -BackMost)
            {
                return -Clamp01(back) * BackPush * mass * (1f - Clamp01(-along / BackMost));
            }
            return 0f;
        }

        /// <summary>The air on the bike at this speed (N, against the motion).</summary>
        public static float Air(float drag, float speed)
        {
            return drag * speed * speed;
        }

        // ---------------------------------------------------------------- the pose

        public enum Pose { Ride, Coast, Stand }

        public const float StopBelow = 0.35f;           // m/s: slower than this with nothing held is standing still (a foot goes down)
        public const float MoveAbove = 0.8f;            // m/s: and faster than this is riding again
        public const float BackingBelow = -0.2f;        // m/s: walking it back keeps both feet down
        public const float PushOffHold = 0.05f;         // s a push off has to last: at once
        public const float StopHold = 0.15f;            // s a stop has to last before the foot goes down
        public const float ChangeHold = 0.3f;           // s pedalling and freewheeling have to last before the pose follows
        public const float PoseFade = 0.35f;            // s one riding clip takes to cross into the next; no other change is made meanwhile

        /// <summary>
        /// The pose the rider should be in: <paramref name="speed"/> signed along the bike (m/s), <paramref name="pushing"/>
        /// with the throttle held. Without a stopped clip the rider never stands; without a freewheeling one never coasts.
        /// </summary>
        public static Pose Want(Pose now, float speed, bool pushing, bool canStand, bool canCoast, bool bicycle)
        {
            float fast = speed < 0f ? -speed : speed;
            if (canStand && speed < BackingBelow && !pushing)
            {
                return Pose.Stand;
            }
            if (now == Pose.Stand && canStand)
            {
                return pushing || fast > MoveAbove ? Moving(pushing, canCoast, bicycle) : Pose.Stand;
            }
            if (canStand && fast < StopBelow && !pushing)
            {
                return Pose.Stand;
            }
            return Moving(pushing, canCoast, bicycle);
        }

        private static Pose Moving(bool pushing, bool canCoast, bool bicycle)
        {
            return bicycle && !pushing && canCoast ? Pose.Coast : Pose.Ride;
        }

        /// <summary>How long (s) a wanted change from <paramref name="now"/> to <paramref name="want"/> must last before it is made.</summary>
        public static float HoldFor(Pose now, Pose want)
        {
            return now == Pose.Stand ? PushOffHold : want == Pose.Stand ? StopHold : ChangeHold;
        }

        /// <summary>Smoothstep: a weight that leaves 0 and reaches 1 with no jolt at either end.</summary>
        public static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>A shown value eased towards its target over about <paramref name="seconds"/>, the same at any frame rate.</summary>
        public static float Ease(float shown, float target, float dt, float seconds)
        {
            if (seconds <= 0f || dt <= 0f)
            {
                return seconds <= 0f ? target : shown;
            }
            return shown + (target - shown) * (1f - (float)Math.Exp(-dt / seconds));
        }

        // ---------------------------------------------------------------- the lean

        public const float LeanMostDeg = 33f;           // the game's scooter's full lean
        public const float FullLeanAt = 10f;            // m/s (36 km/h) from which it may lean fully
        public const float LeanRateSlow = 90f;          // degrees a second at a standstill (the game's scooter: 220)
        public const float LeanRateFast = 180f;         // ...from FullLeanAt up

        /// <summary>The furthest the bike may lean at this speed (m/s), degrees.</summary>
        public static float LeanMost(float speed)
        {
            float fast = speed < 0f ? -speed : speed;
            return fast >= FullLeanAt ? LeanMostDeg : LeanMostDeg * fast / FullLeanAt;
        }

        /// <summary>How fast (degrees a second) the lean may change at this speed (m/s).</summary>
        public static float LeanRate(float speed)
        {
            float fast = speed < 0f ? -speed : speed;
            return LeanRateSlow + (LeanRateFast - LeanRateSlow) * Clamp01(fast / FullLeanAt);
        }

        // ---------------------------------------------------------------- the body the world meets

        /// <summary>A box in the bike's own space (metres; origin on the road, +Z forwards, +Y up): its middle and its size.</summary>
        public struct Box
        {
            public float X, Y, Z;               // the middle
            public float Width, Height, Length;

            public float Bottom => Y - Height * 0.5f;
            public float Top => Y + Height * 0.5f;
            public float Front => Z + Length * 0.5f;
            public float Back => Z - Length * 0.5f;
            public float HalfWidth => Width * 0.5f;
        }

        public const float FrameHalfBicycle = 0.16f;    // m: a bicycle's frame and the rider's legs, as the scooter's own box
        public const float FrameHalfMotorbike = 0.24f;  // m: a motorbike's engine and tank
        public const float KerbClearance = 0.20f;       // m: the frame box's bottom at least this far off the road (a kerb is 0.15)
        public const float LeanClearance = 0.05f;       // m: ...and its lowest corner this far off it at full lean and more
        public const float LeanMargin = 5f;             // degrees past the full lean the clearance must hold for
        public const float WheelTyre = 0.02f;           // m past the wheel's round, the tyre's bulge
        public const float GripReach = 0.06f;           // m past a grip's middle, the hand on it
        public const float BarsBelow = 0.35f;           // m under the grips: the rider's knees
        public const float BarsAbove = 0.08f;           // m over them: the hands
        public const float BarsBehindSeat = 0.10f;      // m behind the saddle's front the bars box starts (the rider's thighs)
        public const float BarsAhead = 0.08f;           // m ahead of the grips

        /// <summary>
        /// The frame box: from the back of the rear wheel to the front of the front one, from just off the road to the
        /// saddle, as narrow as the frame. The wheels' middles at <paramref name="rearZ"/> and <paramref name="frontZ"/>.
        /// </summary>
        public static Box Frame(float rearZ, float frontZ, float wheelRadius, float seatHeight, bool motor)
        {
            float half = motor ? FrameHalfMotorbike : FrameHalfBicycle;
            float back = rearZ - wheelRadius - WheelTyre;
            float front = frontZ + wheelRadius + WheelTyre;
            float bottom = Bottom(half);
            float top = Math.Max(seatHeight, 2f * wheelRadius);
            return new Box
            {
                X = 0f, Y = (bottom + top) * 0.5f, Z = (back + front) * 0.5f,
                Width = 2f * half, Height = top - bottom, Length = front - back
            };
        }

        /// <summary>
        /// The bars box: the width of the grips and the hands on them, from the rider's knees to the hands, from the
        /// saddle's front to just ahead of the grips. <paramref name="gripHalf"/> is a grip's middle off the centre line.
        /// </summary>
        public static Box Bars(float gripHalf, float gripHeight, float gripZ, float seatZ)
        {
            float half = gripHalf + GripReach;
            float bottom = gripHeight - BarsBelow, top = gripHeight + BarsAbove;
            float back = seatZ - BarsBehindSeat, front = gripZ + BarsAhead;
            if (front < back + 0.2f)
            {
                front = back + 0.2f;   // (a grip over the saddle: never a box with no length)
            }
            return new Box
            {
                X = 0f, Y = (bottom + top) * 0.5f, Z = (back + front) * 0.5f,
                Width = 2f * half, Height = top - bottom, Length = front - back
            };
        }

        /// <summary>The lowest a box's bottom may be for its half width to stay <see cref="LeanClearance"/> off the road at full lean and the margin.</summary>
        public static float Bottom(float halfWidth)
        {
            double lean = (LeanMostDeg + LeanMargin) * Math.PI / 180.0;
            float atLean = (float)((LeanClearance + halfWidth * Math.Sin(lean)) / Math.Cos(lean));
            return Math.Max(KerbClearance, atLean);
        }

        /// <summary>How high off the road the box's lowest corner is with the bike leant this far (degrees) about its wheels' line.</summary>
        public static float LowestAt(Box box, float leanDeg)
        {
            double lean = Math.Abs(leanDeg) * Math.PI / 180.0;
            return (float)(box.Bottom * Math.Cos(lean) - box.HalfWidth * Math.Sin(lean));
        }

        /// <summary>
        /// A box's size along the axes of a space turned by the rotation matrix m (rows: the space's axes in the bike's): each
        /// of the space's axes takes the absolute parts of the bike's sizes that lie along it, so a box turned a quarter turn
        /// has its width and length swapped, and no size ever comes out negative.
        /// </summary>
        public static void SizeAlong(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22,
            float x, float y, float z, out float sx, out float sy, out float sz)
        {
            sx = Math.Abs(m00) * x + Math.Abs(m01) * y + Math.Abs(m02) * z;
            sy = Math.Abs(m10) * x + Math.Abs(m11) * y + Math.Abs(m12) * z;
            sz = Math.Abs(m20) * x + Math.Abs(m21) * y + Math.Abs(m22) * z;
        }

        // ---------------------------------------------------------------- the mass

        public const float LeastInertia = 0.001f;   // kg m2: a component at or under this is the engine's lock on a frozen turn, not a body's own

        /// <summary>
        /// True for an inertia a body turns on: every component finite and over <see cref="LeastInertia"/>. The engine takes
        /// a zero component as an infinite inertia about that axis; it is how it freezes a body's turning.
        /// </summary>
        public static bool Turns(float x, float y, float z)
        {
            return Turning(x) && Turning(y) && Turning(z);
        }

        /// <summary>
        /// Whether the body's centre of mass and inertia, read now, are kept as the game's: only while no turn of the body is
        /// frozen (<paramref name="turnFree"/>), and only an inertia it turns on. The game holds every scooter nobody is on
        /// frozen (VehicleController.SetFreeze), so a bike dressed while it stands reads an inertia of nothing.
        /// </summary>
        public static bool KeepMass(bool turnFree, float x, float y, float z)
        {
            return turnFree && Turns(x, y, z);
        }

        /// <summary>
        /// Whether what was kept is written back: only something kept, and only into a body free to turn. A body the game
        /// holds frozen gets the game's own values from the game as it is let go (VariableCenterOfMass).
        /// </summary>
        public static bool PutMassBack(bool kept, bool turnFree)
        {
            return kept && turnFree;
        }

        private static bool Turning(float value)
        {
            return value > LeastInertia && value < float.PositiveInfinity;   // (not a number fails both)
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
