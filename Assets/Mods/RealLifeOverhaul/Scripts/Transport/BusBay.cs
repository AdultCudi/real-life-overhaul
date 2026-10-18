using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The geometry of a bus bay, numbers only, so NavCheck drives a model bus through it. A bay is a length of the strip
    /// between a driving lane and the kerb (where the game parks) that a bus pulls into, said in the stop's frame: metres
    /// along the lane from the pole (ahead positive) and aside from the lane's line towards the kerb. A body turned about
    /// one point swings towards the kerb with whatever lies ahead of it coming in and behind it turning out, so the last
    /// metres in and the first out are an exponential whose length constant is about that lever (<see cref="In"/>,
    /// <see cref="Out"/>). The mod's bus is driven by its front axle with its rear trailing, so its curve in is as long as
    /// its wheelbase (<see cref="Approach"/>) and it then runs <see cref="Settle"/> metres along the bay's line to stand
    /// square. <see cref="Lay"/> is the bay for it.
    /// </summary>
    public static class BusBay
    {
        /// <summary>Metres from a lane's line to its edge (a lane is 3.3 m: EmergencyDriver.PassOffset).</summary>
        public const float LaneHalf = 1.65f;

        /// <summary>
        /// Metres between the side of a bus standing true in its bay and the lane's edge, at the least. None: a 2.40 m bus in
        /// the city's usual 2.55 m strip stands with its side on the lane's edge line.
        /// </summary>
        public const float Clear = 0f;

        /// <summary>Metres between the door side of a bus standing in its bay and the kerb: a low floor's level step.</summary>
        public const float KerbGap = 0.15f;

        /// <summary>Half the widest thing the game's traffic drives (a lorry): what passes on the lane's line reaches this far.</summary>
        public const float WidestHalf = 1.30f;

        /// <summary>Metres within which the driver takes a point of a bay's path as reached (4.5 m on the open lane), so it keeps to the curve.</summary>
        public const float Reach = 2f;

        /// <summary>Metres between the points of a bay's path.</summary>
        public const float Step = 1f;

        /// <summary>m/s in and out of a bay.</summary>
        public const float Speed = 4.5f;

        /// <summary>Metres of lane a bay's curve may take up; a stop has plain lane for 26 m either side (TransitMap.JunctionClear).</summary>
        public const float RunMost = 25f;

        // The bus a bay is laid out for, in metres: the mod's own (BusVehicle is built to these).
        public const float BusLength = 10.90f;
        public const float BusSideHalf = 1.20f;
        public const float BusWheelbase = 5.80f;
        public const float BusAxleFront = 2.90f;                              // its front axle, ahead of its middle
        public const float BusNoseAhead = BusLength * 0.5f - BusAxleFront;    // its nose, ahead of the front axle
        public const float BusTailBehind = BusLength * 0.5f + BusAxleFront;   // its tail, behind it
        public const float BusMiddleAhead = -BusAxleFront;                    // its middle, ahead of the point its driver drives

        /// <summary>Metres its front axle runs along the bay's line at the end of its curve in, while its rear comes in behind it.</summary>
        public const float Settle = 7f;

        private const float SweepStep = 0.25f;   // metres between the poses a bay's sweep is measured at
        private const float SweepSlack = 0.2f;   // metres more of the strip than the sweep finds, each end

        /// <summary>
        /// Metres of lever its curve out is laid with. Its rear axle trails, so only the overhang behind that axle swings
        /// towards the kerb as it turns out; a little more than that keeps the swing inside its gap (NavCheck drives it).
        /// </summary>
        public const float BusLeave = 3.5f;

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

        /// <summary>
        /// One curve of a bay's path: how far aside of the bay's line the path still is by the metres to the standing place
        /// (in), or how far it has come away from it by the metres gone (out).
        /// </summary>
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

        /// <summary>
        /// Metres, the length constant of the bus's curve in, near its wheelbase so its body turns no faster than its rear can
        /// follow and its nose stays off the kerb (NavCheck drives it).
        /// </summary>
        public const float BusApproach = 4.5f;

        /// <summary>Metres the bus's curve in may take up: more than another vehicle's (RunMost), for its length.</summary>
        public const float ApproachMost = 28f;

        /// <summary>The bus's curve in, for its front axle (<see cref="BusApproach"/>).</summary>
        public static Curve Approach(float offset, float most = ApproachMost)
        {
            return Make(offset, BusApproach, InShort, most);
        }

        /// <summary>
        /// The bay laid out for the bus beside a kerb so far from the lane's line: its middle's offset, its front axle's curves
        /// in and out, and the strip they take behind and ahead of the pole. False if there is no room.
        /// </summary>
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

        // The bus driven along the curve's points by its front axle, its rear trailing, a pose every SweepStep: in, the furthest
        // back from the pole any of its kerb side is beyond the lane's edge (negative); out, the furthest on.
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

        /// <summary>The curve into a bay, for a bus that reaches <paramref name="noseAhead"/> metres ahead of the point it is turned about.</summary>
        public static Curve In(float offset, float noseAhead, float most = RunMost)
        {
            return Make(offset, Math.Max(2.2f, noseAhead - NoseSlack), InShort, most);
        }

        /// <summary>The curve out of a bay, for a bus that reaches <paramref name="tailBehind"/> metres behind the point it is turned about.</summary>
        public static Curve Out(float offset, float tailBehind, float most = RunMost)
        {
            return Make(offset, Math.Max(2f, tailBehind * TailFactor), Math.Max(0.04f, tailBehind * OutShortPer), most);
        }

        /// <summary>Metres from the lane's line the kerb must be for a bay: the lane's half, the clearance, the body and the kerb gap.</summary>
        public static float KerbNeeded(float sideHalf)
        {
            return LaneHalf + Clear + sideHalf * 2f + KerbGap;
        }

        /// <summary>
        /// Metres aside of the lane's line where the middle of a bus that wide stands in a bay, its door side
        /// <see cref="KerbGap"/> from the kerb; 0 if the kerb is too near the lane for a bay.
        /// </summary>
        public static float Offset(float kerbAside, float sideHalf)
        {
            return kerbAside >= KerbNeeded(sideHalf) - 0.001f ? kerbAside - KerbGap - sideHalf : 0f;
        }

        /// <summary>
        /// Metres of the strip that must be free behind the pole for a bus to come in, from where its nose first passes the
        /// lane's edge. <paramref name="middleAhead"/> is how far the bus's middle is ahead of the point it turns about.
        /// </summary>
        public static float Behind(Curve curveIn, float middleAhead)
        {
            return curveIn.Length + middleAhead - StripIn;
        }

        /// <summary>The same ahead of the pole to pull out, to where its nose is back inside the lane's edge.</summary>
        public static float Ahead(Curve curveOut, float middleAhead)
        {
            return curveOut.Length - middleAhead - StripOut;
        }

        /// <summary>
        /// Metres between the lane's edge and the nearest corner of a body standing with its middle <paramref name="aside"/>
        /// of the lane's line, <paramref name="degrees"/> off the lane's direction. Negative: that much of it is in the lane.
        /// </summary>
        public static float LaneClear(float aside, float degrees, float halfLength, float sideHalf)
        {
            double angle = Math.Abs(degrees) * Math.PI / 180.0;
            return aside - halfLength * (float)Math.Sin(angle) - sideHalf * (float)Math.Cos(angle) - LaneHalf;
        }

        /// <summary>Metres between the kerb and the nearest corner of the same body. Negative: that much of it is over the kerb.</summary>
        public static float KerbClear(float kerbAside, float aside, float degrees, float halfLength, float sideHalf)
        {
            double angle = Math.Abs(degrees) * Math.PI / 180.0;
            return kerbAside - aside - halfLength * (float)Math.Sin(angle) - sideHalf * (float)Math.Cos(angle);
        }

        /// <summary>
        /// What is left of a run of kerbside parking beside a bus zone, in metres along the lane: the part before or after the
        /// zone, the longer if the zone is in the middle, the other <paramref name="lost"/>. A run wholly in the zone keeps
        /// nothing (both ends at the zone's end). False if the run does not reach into the zone.
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

        /// <summary>Metres a parked car's body is kept clear of each end of a bus zone: no bumper over its line.</summary>
        public const float CarMargin = 1f;

        /// <summary>
        /// Whether a parked car's body, <paramref name="carStart"/> to <paramref name="carEnd"/> metres along the stop's lane,
        /// is in the zone or within <paramref name="margin"/> of either end of it (touching the margin counts).
        /// </summary>
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

        /// <summary>
        /// The game's parking spots given up when a run of kerbside parking is cut from <paramref name="was"/> to
        /// <paramref name="now"/> metres, a spot each <paramref name="spot"/> metres (the game lays them from one end, whole).
        /// </summary>
        public static int SpotsLost(float was, float now, float spot)
        {
            if (spot < 0.1f || was <= now)
            {
                return 0;
            }
            return Math.Max(0, (int)Math.Floor(was / spot + 1e-4f) - (int)Math.Floor(Math.Max(0f, now) / spot + 1e-4f));
        }

        /// <summary>
        /// How many times a broken line's pattern (<paramref name="repeat"/> metres, the dash a <paramref name="paint"/> share
        /// of it) is laid along <paramref name="length"/> metres so that the line begins and ends with paint: whole patterns and
        /// one dash more, stretched a little to fit. A line shorter than a dash is one dash.
        /// </summary>
        public static float Dashes(float length, float repeat, float paint)
        {
            return Math.Max(0f, (float)Math.Round(length / repeat - paint)) + paint;
        }
    }
}
