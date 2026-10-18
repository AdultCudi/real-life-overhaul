using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The geometry of a bus bay, numbers only, so NavCheck drives a model bus through it: in the stop's frame, metres along the
    /// lane from the pole (ahead positive) and aside of the lane's line towards the kerb. The last metres in and the first out
    /// are an exponential whose length constant is about the lever that swings towards the kerb (<see cref="In"/>,
    /// <see cref="Out"/>); the mod's bus, its rear axle trailing, comes in over its wheelbase (<see cref="Approach"/>) and then
    /// runs <see cref="Settle"/> metres along the bay's line to stand square. <see cref="Lay"/> is the bay for it.
    /// </summary>
    public static class BusBay
    {
        public const float LaneHalf = 1.65f;      // metres from a lane's line to its edge (a lane is 3.3 m: EmergencyDriver.PassOffset)

        public const float Clear = 0f;            // metres from a bus's side in its bay to the lane's edge, at least: a 2.40 m bus fills the city's 2.55 m strip

        public const float KerbGap = 0.15f;       // metres from the door side of a bus in its bay to the kerb: a low floor's level step

        public const float WidestHalf = 1.30f;    // metres, half the widest thing the traffic drives (a lorry)

        public const float Reach = 2f;            // metres within which the driver takes a point of a bay's path as reached (4.5 on the open lane)

        public const float Step = 1f;             // metres between the points of a bay's path

        public const float Speed = 4.5f;          // m/s in and out of a bay

        public const float RunMost = 25f;         // metres of lane a bay's curve may take up; a stop has 26 m plain either side (TransitMap.JunctionClear)

        // The bus a bay is laid out for, in metres: the mod's own (BusVehicle is built to these).
        public const float BusLength = 10.90f;
        public const float BusSideHalf = 1.20f;
        public const float BusWheelbase = 5.80f;
        public const float BusAxleFront = 2.90f;                              // its front axle, ahead of its middle
        public const float BusNoseAhead = BusLength * 0.5f - BusAxleFront;    // its nose, ahead of the front axle
        public const float BusTailBehind = BusLength * 0.5f + BusAxleFront;   // its tail, behind it
        public const float BusMiddleAhead = -BusAxleFront;                    // its middle, ahead of the point its driver drives

        public const float Settle = 7f;           // metres its front axle runs along the bay's line at the end of its curve in, while its rear comes in

        private const float SweepStep = 0.25f;   // metres between the poses a bay's sweep is measured at
        private const float SweepSlack = 0.2f;   // metres more of the strip than the sweep finds, each end

        public const float BusLeave = 3.5f;       // metres of lever its curve out is laid with: the overhang behind its trailing rear axle and a little more

        // A van's levers (the bus body on the van it is modelled on turns about a point 3.33 m from either end).
        private const float NoseSlack = 0.45f;   // the path's length constant in is the lever ahead less this: the driver lags a path by about as much
        private const float TailFactor = 1.2f;   // and out, the lever behind times this
        private const float InShort = 0.03f;     // metres from the bay's line at which the curve in is taken to have arrived
        private const float OutShortPer = 0.03f; // the same out, per metre of lever
        private const float Blend = 3f;          // metres over which the path leaves (or rejoins) the lane's line
        private const float SlopeLeast = 0.28f;  // the steepest the path runs across the strip (16 degrees) at the narrowest bay
        private const float SlopeMore = 0.08f;   // and this much steeper per metre the bay lies further over
        private const float SlopeMost = 0.40f;   // and never steeper than this
        private const float StripIn = 1.8f;      // metres of a curve in that are over before anything of the bus is beyond the lane's edge
        private const float StripOut = 2.1f;     // the same for a curve out

        /// <summary>One curve of a bay's path: how far aside of the bay's line the path is, by the metres to its place (in) or gone (out).</summary>
        public struct Curve
        {
            public float Offset;    // from the lane's line to the bay's
            public float Lambda;    // metres, the exponential's length constant
            public float Short;     // its amplitude
            public float Slope;     // of the straight middle part
            public float EndTail;   // metres at which the exponential ends
            public float EndRamp;   // and the straight part
            public float Length;    // and the whole curve: beyond, the path is on the lane's line
            public bool Cut;        // no room for it: its exponential is shorter than the lever asks, and the bus would swing wide

            public float Aside(float metres)
            {
                if (metres <= 0f)
                {
                    return 0f;
                }
                if (metres <= EndTail)
                {
                    return Short * ((float)Math.Exp(metres / Lambda) - 1f);
                }
                if (metres <= EndRamp)
                {
                    return Lambda * Slope + (metres - EndTail) * Slope;
                }
                if (metres < Length)
                {
                    float left = Length - metres;
                    return Offset - Slope * left * left / (2f * Blend);
                }
                return Offset;
            }

            /// <summary>How many points, <see cref="Step"/> apart, cover it.</summary>
            public int Points => (int)Math.Ceiling(Length / Step);
        }

        private static Curve Make(float offset, float lambda, float amplitude, float most)
        {
            float slope = Math.Min(SlopeMost, SlopeLeast + SlopeMore * Math.Max(0f, offset - 2.85f));
            Curve curve = Shape(offset, lambda, amplitude, slope);
            bool cut = false;
            // No room: steeper across the strip first, then a shorter exponential (the lever then swings further).
            for (int i = 0; i < 12 && curve.Length > most; i++)
            {
                if (slope < SlopeMost - 0.001f)
                {
                    slope = Math.Min(SlopeMost, slope + 0.04f);
                }
                else
                {
                    lambda *= 0.9f;
                    cut = true;
                }
                curve = Shape(offset, lambda, amplitude, slope);
            }
            curve.Cut = cut || curve.Length > most;
            return curve;
        }

        private static Curve Shape(float offset, float lambda, float amplitude, float slope)
        {
            // A bay so near the lane's line that the exponential and the blend alone are more than the way across: flatter.
            if (offset < lambda * slope + slope * Blend * 0.5f)
            {
                slope = offset / (lambda + Blend * 0.5f);
            }
            float meet = lambda * slope;
            var curve = new Curve { Offset = offset, Lambda = lambda, Short = amplitude, Slope = slope };
            curve.EndTail = lambda * (float)Math.Log(meet / amplitude + 1f);
            curve.EndRamp = curve.EndTail + (offset - meet - slope * Blend * 0.5f) / slope;
            curve.Length = curve.EndRamp + Blend;
            return curve;
        }

        public const float BusApproach = 4.5f;    // metres, the length constant of the bus's curve in: near its wheelbase, so its nose stays off the kerb

        public const float ApproachMost = 28f;    // metres the bus's curve in may take up, more than RunMost for its length

        public static Curve Approach(float offset, float most = ApproachMost)
        {
            return Make(offset, BusApproach, InShort, most);
        }

        /// <summary>The bay for the bus beside a kerb so far out: its middle's offset, its curves in and out, and the strip they take either side of the pole.</summary>
        public static bool Lay(float kerbAside, out float offset, out Curve curveIn, out Curve curveOut, out float behind, out float ahead)
        {
            offset = Offset(kerbAside, BusSideHalf);
            curveIn = default;
            curveOut = default;
            behind = 0f;
            ahead = 0f;
            if (offset <= 0f)
            {
                return false;
            }
            curveIn = Approach(offset);
            curveOut = Out(offset, BusLeave, RunMost + Math.Min(0f, BusMiddleAhead));
            // The strip it takes is where its body really is beyond the lane's edge, its rear trailing: swept, not guessed.
            behind = -Sweep(curveIn, offset, true) + SweepSlack;
            ahead = Sweep(curveOut, offset, false) + SweepSlack;
            return !curveIn.Cut && !curveOut.Cut;
        }

        // The bus driven along its curve, its rear trailing, a pose every SweepStep: in, how far back any of its kerb side passes
        // the lane's edge (negative); out, how far on.
        private static float Sweep(Curve curve, float offset, bool coming)
        {
            float rest = -BusMiddleAhead;   // where its front axle stands, along from the pole
            float fx, fz, start, end;
            if (coming)
            {
                start = rest - Settle - curve.Length;   // its front axle leaves the lane's line here
                end = rest;
                fx = start;
                fz = 0f;
            }
            else
            {
                start = rest;
                end = rest + curve.Length + 3f * BusWheelbase;   // until its trailing rear is back in the lane too
                fx = start;
                fz = offset;
            }
            float rx = fx - BusWheelbase, rz = fz;
            float extent = coming ? float.MaxValue : float.MinValue;
            for (float x = start; x <= end + 0.001f; x += SweepStep)
            {
                fx = x;
                fz = coming ? (x <= rest - Settle ? offset - curve.Aside(rest - Settle - x) : offset) : offset - curve.Aside(x - rest);
                BusPath.Trail(fx, fz, ref rx, ref rz, BusWheelbase);
                // Its kerb side from tail to nose, and where along it that side is beyond the lane's edge.
                float hx = fx - rx, hz = fz - rz;
                float d = (float)Math.Sqrt(hx * hx + hz * hz);
                hx /= d;
                hz /= d;
                float tailX = fx - hx * BusTailBehind - hz * BusSideHalf, tailZ = fz - hz * BusTailBehind + hx * BusSideHalf;
                float noseX = fx + hx * BusNoseAhead - hz * BusSideHalf, noseZ = fz + hz * BusNoseAhead + hx * BusSideHalf;
                if (tailZ <= LaneHalf && noseZ <= LaneHalf)
                {
                    continue;
                }
                float cross = Math.Abs(noseZ - tailZ) > 0.0001f ? tailX + (LaneHalf - tailZ) / (noseZ - tailZ) * (noseX - tailX) : tailX;
                float from = tailZ > LaneHalf ? tailX : cross;
                float to = noseZ > LaneHalf ? noseX : cross;
                extent = coming ? Math.Min(extent, from) : Math.Max(extent, to);
            }
            if (extent == float.MaxValue || extent == float.MinValue)
            {
                extent = coming ? rest - Settle : rest;
            }
            return extent;
        }

        /// <summary>The curve into a bay for a body reaching <paramref name="noseAhead"/> metres ahead of the point it turns about.</summary>
        public static Curve In(float offset, float noseAhead, float most = RunMost)
        {
            return Make(offset, Math.Max(2.2f, noseAhead - NoseSlack), InShort, most);
        }

        /// <summary>The curve out for one reaching <paramref name="tailBehind"/> metres behind it.</summary>
        public static Curve Out(float offset, float tailBehind, float most = RunMost)
        {
            return Make(offset, Math.Max(2f, tailBehind * TailFactor), Math.Max(0.04f, tailBehind * OutShortPer), most);
        }

        /// <summary>Metres from the lane's line the kerb must be for a bay.</summary>
        public static float KerbNeeded(float sideHalf)
        {
            return LaneHalf + Clear + sideHalf * 2f + KerbGap;
        }

        /// <summary>Metres aside of the lane's line the middle of a bus that wide stands in a bay; 0 if the kerb is too near for one.</summary>
        public static float Offset(float kerbAside, float sideHalf)
        {
            return kerbAside >= KerbNeeded(sideHalf) - 0.001f ? kerbAside - KerbGap - sideHalf : 0f;
        }

        /// <summary>Metres of strip behind the pole that must be free for a bus to come in; <paramref name="middleAhead"/> is its middle ahead of its pivot.</summary>
        public static float Behind(Curve curveIn, float middleAhead)
        {
            return curveIn.Length + middleAhead - StripIn;
        }

        /// <summary>The same ahead of the pole, to pull out.</summary>
        public static float Ahead(Curve curveOut, float middleAhead)
        {
            return curveOut.Length - middleAhead - StripOut;
        }

        /// <summary>Metres from the lane's edge to the nearest corner of a body <paramref name="degrees"/> off the lane; negative inside the lane.</summary>
        public static float LaneClear(float aside, float degrees, float halfLength, float sideHalf)
        {
            double angle = Math.Abs(degrees) * Math.PI / 180.0;
            return aside - halfLength * (float)Math.Sin(angle) - sideHalf * (float)Math.Cos(angle) - LaneHalf;
        }

        /// <summary>The same to the kerb; negative over it.</summary>
        public static float KerbClear(float kerbAside, float aside, float degrees, float halfLength, float sideHalf)
        {
            double angle = Math.Abs(degrees) * Math.PI / 180.0;
            return kerbAside - aside - halfLength * (float)Math.Sin(angle) - sideHalf * (float)Math.Cos(angle);
        }

        /// <summary>
        /// The part of a run of kerbside parking a bus zone leaves, in metres along the lane: before or after the zone, the longer
        /// if the zone is in its middle, the rest <paramref name="lost"/>. False if the run does not reach into the zone.
        /// </summary>
        public static bool Keep(float start, float end, float from, float to, out float keepStart, out float keepEnd, out float lost)
        {
            keepStart = start;
            keepEnd = end;
            lost = 0f;
            if (end <= from || start >= to)
            {
                return false;
            }
            bool before = start < from;
            bool after = end > to;
            if (before && after)
            {
                if (from - start >= end - to)
                {
                    keepEnd = from;
                    lost = end - to;
                }
                else
                {
                    keepStart = to;
                    lost = from - start;
                }
            }
            else if (before)
            {
                keepEnd = from;
            }
            else if (after)
            {
                keepStart = to;
            }
            else
            {
                keepStart = to;
                keepEnd = to;
            }
            return true;
        }

        public const float CarMargin = 1f;        // metres a parked car's body is kept clear of each end of a bus zone

        /// <summary>Whether a parked car's body between these metres along the lane is in the zone or its margin.</summary>
        public static bool CarInZone(float carStart, float carEnd, float from, float to, float margin = CarMargin)
        {
            if (carEnd < carStart)
            {
                float swap = carEnd;
                carEnd = carStart;
                carStart = swap;
            }
            return carEnd > from - margin && carStart < to + margin;
        }

        /// <summary>The game's parking spots given up when a run is cut from <paramref name="was"/> to <paramref name="now"/> metres.</summary>
        public static int SpotsLost(float was, float now, float spot)
        {
            if (spot < 0.1f || was <= now)
            {
                return 0;
            }
            return Math.Max(0, (int)Math.Floor(was / spot + 1e-4f) - (int)Math.Floor(Math.Max(0f, now) / spot + 1e-4f));
        }

        /// <summary>
        /// How many times a broken line's pattern is laid along <paramref name="length"/> metres so it begins and ends with
        /// paint: whole patterns and one dash more, stretched a little to fit.
        /// </summary>
        public static float Dashes(float length, float repeat, float paint)
        {
            return Math.Max(0f, (float)Math.Round(length / repeat - paint)) + paint;
        }
    }
}
