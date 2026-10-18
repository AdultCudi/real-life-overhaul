using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The tow truck's rules as numbers, game-free so that NavCheck's TowCheck runs the very code the truck is run by: the
    /// bed's motion, where a wreck is on it and on the way up, the stretch of road to stand ahead of a wreck, how long it is
    /// said to take, and when it may come and go. Space is the truck's own: ahead from the middle between its axles, up from
    /// the road, in the model's figures (props_tow.py). The bed (<see cref="DeckPose"/>) turns nose up about its pivot and
    /// then slides back, each eased and overlapping a little, so nothing jumps. A car is drawn up by its axles along the road
    /// and the bed's top, the step at the tail rounded (<see cref="CarOnBed"/>). A few multiplications, a few times a frame.
    /// </summary>
    public static class TowRules
    {
        // The truck (TowTruck, props_tow.py).
        public const float AxleFront = 2.30f;          // metres ahead of its origin
        public const float AxleRear = -2.30f;          // metres
        public const float Nose = 3.42f;               // the front of its bumper
        public const float HalfWidth = 1.20f;          // the bed's half width
        public const float TiltMost = 13f;             // degrees the subframe turns nose up
        public const float PivotAhead = -4.45f;        // the subframe's pivot, ahead of the origin...
        public const float PivotUp = 0.89f;            // ...and over the road
        public const float BedTop = 1.06f;             // the bed's top at rest
        public const float BedTail = -4.85f;           // its tail at rest, ahead of the origin
        public const float HeadBoard = 1.17f;          // the face of its headboard at rest
        public const float TailOverRoad = 0.04f;       // the top of the tail over the road with the bed down

        /// <summary>Metres the bed slides back along the subframe at <see cref="TiltMost"/> to bring its tail down to <see cref="TailOverRoad"/>.</summary>
        public static readonly float SlideMost = SlideFor(TiltMost);

        private const float TiltShare = 0.40f;         // of t over which the subframe turns...
        private const float SlideFrom = 0.30f;         // ...and from which the bed slides, to 1
        public const float RoundOver = 0.35f;          // metres either side of the tail over which the road and the bed's top are rounded into one
        public const float HeadGap = 0.30f;            // metres a car stands short of the headboard
        public const float FootGap = 0.25f;            // ...and its near end behind the tail at the foot of the bed
        public const float BaseShare = 0.62f;          // of a car's length between its axles

        // What it takes.
        public const float LongestCar = 5.6f;          // metres: the bed's top is 6.02 long less HeadGap
        public const float WidestCar = 2.3f;           // metres: inside the edge rails

        /// <summary>True for a car of this length and width (metres) that goes on the bed.</summary>
        public static bool Fits(float length, float width)
        {
            return length > 0.5f && length <= LongestCar && width > 0.3f && width <= WidestCar;
        }

        /// <summary>How far the subframe is turned (degrees, nose up) and the bed slid back (metres) at t, 0 at rest and 1 down.</summary>
        public static void DeckPose(float t, out float tilt, out float slide)
        {
            tilt = TiltMost * Smooth(t / TiltShare);
            slide = SlideMost * Smooth((t - SlideFrom) / (1f - SlideFrom));
        }

        /// <summary>Where a point of the bed's top this far ahead of its tail is (up, ahead), with the bed turned and slid so.</summary>
        public static void BedPoint(float along, float tilt, float slide, out float up, out float ahead)
        {
            // At rest the point is at (BedTop, BedTail + along); slid back along the bed, then turned about the pivot.
            float a = Rad(tilt);
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            float ry = BedTop - PivotUp, rz = BedTail + along - slide - PivotAhead;
            up = PivotUp + rz * sin + ry * cos;
            ahead = PivotAhead + rz * cos - ry * sin;
        }

        /// <summary>The slide that brings the tail's top to <see cref="TailOverRoad"/> at this tilt (degrees, more than 0).</summary>
        public static float SlideFor(float tilt)
        {
            float a = Rad(tilt);
            float sin = (float)Math.Sin(a), cos = (float)Math.Cos(a);
            float ry = BedTop - PivotUp, rz = BedTail - PivotAhead;   // rz < 0: the tail is behind the pivot
            // up = PivotUp + (rz - slide) * sin + ry * cos = TailOverRoad
            return (PivotUp + rz * sin + ry * cos - TailOverRoad) / sin;
        }

        /// <summary>Metres from the tail along the bed's top to where the middle of a car of this half length stands on it.</summary>
        public static float RestAlong(float halfLength)
        {
            return HeadBoard - BedTail - HeadGap - halfLength;
        }

        /// <summary>Where the tail lies with the bed down (up, ahead).</summary>
        public static void TailDown(out float up, out float ahead)
        {
            BedPoint(0f, TiltMost, SlideMost, out up, out ahead);
        }

        /// <summary>Metres ahead of the truck's origin of the middle of a car of this half length at the foot of the bed down.</summary>
        public static float FootAhead(float halfLength)
        {
            TailDown(out _, out float tail);
            return tail - FootGap - halfLength;
        }

        /// <summary>Metres along the line up the bed (0 at the tail) of the middle of a car at the foot: both its axles on the road.</summary>
        public static float FootAlong(float halfLength)
        {
            return -FootGap - halfLength;
        }

        /// <summary>
        /// A point of the line a car's wheels run on with the bed down, this far along it from the tail (up the bed positive):
        /// the road and the bed's top blended over <see cref="RoundOver"/> either side of the tail.
        /// </summary>
        public static void Track(float along, out float up, out float ahead)
        {
            TailDown(out _, out float tailAhead);
            BedPoint(along, TiltMost, SlideMost, out float bedUp, out float bedAhead);
            float roadAhead = tailAhead + along;   // the road, level with the truck's own ground
            float w = Smooth((along + RoundOver) / (2f * RoundOver));
            up = bedUp * w;
            ahead = roadAhead + (bedAhead - roadAhead) * w;
        }

        /// <summary>
        /// A car of this half length going up the bed, its middle this far along the line (<see cref="Track"/>): the point
        /// midway under its axles (up, ahead) and its pitch, degrees nose up.
        /// </summary>
        public static void CarOnBed(float along, float halfLength, out float up, out float ahead, out float pitch)
        {
            float half = halfLength * BaseShare;
            Track(along + half, out float fu, out float fa);
            Track(along - half, out float ru, out float ra);
            up = (fu + ru) * 0.5f;
            ahead = (fa + ra) * 0.5f;
            pitch = (float)(Math.Atan2(fu - ru, fa - ra) * 180.0 / Math.PI);
        }

        /// <summary>
        /// The car drawn along the road to the foot of the bed: its middle (across, ahead) and heading (degrees from the truck's
        /// forward) at u from 0 to 1, on a curve that leaves towards the foot and arrives along the truck.
        /// </summary>
        public static void Drag(float u, float x0, float z0, float yaw0, float x1, float z1, float yaw1, out float x, out float z, out float yaw)
        {
            u = Clamp01(u);
            float dx = x1 - x0, dz = z1 - z0;
            float reach = (float)Math.Sqrt(dx * dx + dz * dz);
            float u2 = u * u, u3 = u2 * u;
            float h00 = 2f * u3 - 3f * u2 + 1f, h10 = u3 - 2f * u2 + u, h01 = -2f * u3 + 3f * u2, h11 = u3 - u2;
            // Leaving: towards the foot. Arriving: straight along the truck, as far again.
            x = h00 * x0 + h10 * dx + h01 * x1 + h11 * 0f;
            z = h00 * z0 + h10 * dz + h01 * z1 + h11 * reach;
            yaw = yaw0 + DeltaAngle(yaw0, yaw1) * Smooth(u);
        }

        /// <summary>The shortest turn from a to b, degrees, -180 to 180.</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f)
            {
                d -= 360f;
            }
            else if (d < -180f)
            {
                d += 360f;
            }
            return d;
        }

        /// <summary>Which way round a car goes on the bed: 0 nose to the cab, 180 tail to it, whichever is the smaller turn from how it stands.</summary>
        public static float LoadHeading(float standing)
        {
            float d = DeltaAngle(0f, standing);
            return d > 90f || d < -90f ? 180f : 0f;
        }

        // Where it stands.
        public const float MergeLeast = 12f;           // metres over which it moves from the lane beside into the wreck's
        public const float MergePerMetre = 3.6f;       // ...and at least this many for every metre across
        public const float PassClear = 1.5f;           // metres its rear axle is past the wreck's far end before it moves over
        public const float StandAfter = 2f;            // metres in the wreck's lane before it stands
        public const float DragLeast = 0.5f;           // metres at least that the wreck is drawn forward to the foot

        /// <summary>
        /// The stretch of road it takes, in metres along the wreck's lane: from the lane beside (this far across) it moves over
        /// once past the wreck's far end, and stands with its front axle at standAt, the foot of its bed ahead of the wreck.
        /// </summary>
        public static void Stretch(float wreckMiddle, float wreckFront, float across, float halfLength,
            out float mergeFrom, out float mergeTo, out float standAt)
        {
            float merge = Math.Max(MergeLeast, Math.Abs(across) * MergePerMetre);
            mergeFrom = wreckFront + (AxleFront - AxleRear) + PassClear;
            mergeTo = mergeFrom + merge;
            standAt = mergeTo + StandAfter;
            // The foot must be ahead of the wreck: the car is drawn forward to it, never back.
            float foot = standAt - AxleFront + FootAhead(halfLength);
            if (foot < wreckMiddle + DragLeast)
            {
                standAt += wreckMiddle + DragLeast - foot;
            }
        }

        /// <summary>The share (0 at the start of the move, 1 at its end) of the way across from the lane beside to the wreck's.</summary>
        public static float Across(float along, float mergeFrom, float mergeTo)
        {
            return Smooth((along - mergeFrom) / Math.Max(0.01f, mergeTo - mergeFrom));
        }

        // How long it takes.
        public const float ApproachPace = 7.5f;        // m/s it makes good over a route, bends and junctions taken
        public const float StandSeconds = 6f;          // seconds to slow, move over and stand
        public const float HoldLeast = 12f;            // seconds it is held out of sight before it sets off
        public const float HoldMost = 28f;             // seconds

        /// <summary>Real seconds from now until it stands at the wreck: held this long, then over so much road.</summary>
        public static float ArrivalSeconds(float hold, float metres)
        {
            return Math.Max(0f, hold) + Math.Max(0f, metres) / ApproachPace + StandSeconds;
        }

        /// <summary>The wait the notice says: game minutes at the clock's own rate (TaxiRules.Minutes), rounded up, at least one.</summary>
        public static int Minutes(float seconds, float gameMinutesPerSecond)
        {
            float rate = gameMinutesPerSecond > 0f ? gameMinutesPerSecond : 1f;
            int minutes = (int)Math.Ceiling(Math.Max(0f, seconds) * rate);
            return minutes < 1 ? 1 : minutes;
        }

        // The work, in seconds.
        public const float DownSeconds = 9f;           // the bed goes down...
        public const float UpSeconds = 9f;             // ...and comes back up
        public const float HookSeconds = 6f;           // the operator at the wreck, hooking the cable on
        public const float RightSeconds = 3.5f;        // a car on its side or its roof winched back on to its wheels
        public const float DragPace = 1.1f;            // m/s the winch draws a car along the road...
        public const float ClimbPace = 0.7f;           // ...and up the bed
        public const float StrapSeconds = 5f;          // the operator at the wheels, strapping the car down
        public const float WinchLeast = 2f;            // seconds a pull takes at least

        /// <summary>Seconds the winch takes over this much of the road or of the bed.</summary>
        public static float PullSeconds(float metres, float pace)
        {
            return Math.Max(WinchLeast, Math.Abs(metres) / pace);
        }

        /// <summary>Whether the truck or its wreck may be put on or taken off the road here: never in sight, nor within nearKeep metres of the player.</summary>
        public static bool OutOfSight(bool seen, float flatDistance, float nearKeep)
        {
            return !seen && flatDistance >= nearKeep;
        }

        /// <summary>Whether a wreck this far from the player (flat metres) is sent a truck at all: further than the street is drawn, it is simply gone.</summary>
        public static bool WorthSending(float flatDistance, float far)
        {
            return flatDistance <= far;
        }

        /// <summary>0 to 1, eased in and out (zero rate at both ends).</summary>
        public static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float Clamp01(float t)
        {
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        private static float Rad(float degrees)
        {
            return (float)(degrees * Math.PI / 180.0);
        }
    }
}
