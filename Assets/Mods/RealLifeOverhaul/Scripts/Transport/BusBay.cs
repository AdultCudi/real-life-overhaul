using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The geometry of a bus bay, and nothing else: numbers in, numbers out, no engine types, so
    /// that the build's NavCheck drives a model of the bus through it outside the game.
    ///
    /// A BAY is a length of the strip between a driving lane and the kerb (where the game parks
    /// cars) that a bus pulls into, so that it stands beside the lane and not in it. Everything is
    /// said in the stop's own frame: metres ALONG the lane from the pole (ahead is positive) and
    /// metres ASIDE from the lane's line towards the kerb.
    ///
    /// WHY THE CURVES ARE WHAT THEY ARE. The bus is turned about one point of its length (the
    /// origin of the vehicle that carries it: under the middle of a van, under the rear axle of a
    /// truck), so whatever of it lies ahead of that point swings towards the kerb as it comes in at
    /// an angle, and whatever lies behind swings towards the kerb as it turns out. A body that
    /// stands 0.2 m from the kerb has no room for either, unless the angle falls away as fast as
    /// the gap closes: the last metres in, and the first metres out, are an exponential whose
    /// length constant is about the lever that swings (<see cref="In"/>, <see cref="Out"/>).
    /// That is what made a bay some thirty metres long for a 6.7 m bus on a van.
    ///
    /// THE BUS NOW (BusVehicle, BusDriver). The mod's own bus is driven by its front axle, its rear
    /// axle trailing (BusPath.Trail), so the body turns no faster than its rear can follow: coming
    /// in on a curve as short as its nose, it would still face the kerb when its front axle reached
    /// the bay's line, and its nose would swing over the kerb. Its curve in is as long as its
    /// wheelbase (<see cref="Approach"/>), and its front axle then runs <see cref="Settle"/> metres
    /// along the bay's line before it stands, while its rear comes in behind it: it stands square to
    /// within a degree or so. <see cref="Lay"/> is the bay for it, for the stop and the bus alike.
    /// </summary>
    public static class BusBay
    {
        /// <summary>Half the width of a driving lane: the lane's edge is this far from its line (a lane is 3.3 m: EmergencyDriver.PassOffset).</summary>
        public const float LaneHalf = 1.65f;

        /// <summary>
        /// Metres between the side of a bus standing true in its bay and the lane's edge, at the least: what a bay is laid out
        /// for. None: a 2.40 m bus in a 2.55 m strip (most of the city's) stands with its side on the lane's edge line, as a
        /// bus does at a kerbside stop where the parking lane is narrow.
        /// </summary>
        public const float Clear = 0f;

        /// <summary>Metres between the door side of a bus standing in its bay and the kerb: a low floor's level step.</summary>
        public const float KerbGap = 0.15f;

        /// <summary>The widest thing the game's traffic drives (a lorry), halved: what passes on the lane's line reaches this far.</summary>
        public const float WidestHalf = 1.30f;

        /// <summary>The driver takes a point of a bay's path as reached this near it (4.5 m on the open lane): it keeps to the curve.</summary>
        public const float Reach = 2f;

        /// <summary>Metres between the points of a bay's path.</summary>
        public const float Step = 1f;

        /// <summary>m/s in and out of a bay.</summary>
        public const float Speed = 4.5f;

        /// <summary>Metres of lane a bay's curve may take up: a stop has plain lane for 26 m either side (TransitMap.JunctionClear).</summary>
        public const float RunMost = 25f;

        // The bus a bay is laid out for: the mod's own (BusVehicle is built to these).
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
        /// The lever its curve out is laid with (Out). Its rear axle trails, so what swings towards the kerb as its body turns
        /// out is only the overhang behind that axle, not the whole of it behind the front: a little more than that overhang
        /// keeps the swing inside its gap to the kerb (NavCheck drives it).
        /// </summary>
        public const float BusLeave = 3.5f;

        // A van's levers (the bus body on the van it is modelled on: 3.33 m of it either side of the point it turns about).
        private const float NoseSlack = 0.45f;   // the path's length constant in is the lever ahead less this: the driver lags a path by about as much
        private const float TailFactor = 1.2f;   // ...and out, the lever behind times this
        private const float InShort = 0.03f;     // metres from the bay's line at which the curve in is taken to have arrived
        private const float OutShortPer = 0.03f; // the same out, per metre of lever
        private const float Blend = 3f;          // metres over which the path leaves (or rejoins) the lane's line
        private const float SlopeLeast = 0.28f;  // the steepest the path runs across the strip (16 degrees) at the narrowest bay
        private const float SlopeMore = 0.08f;   // ...and this much steeper per metre the bay lies further over
        private const float SlopeMost = 0.40f;
        private const float StripIn = 1.8f;      // metres of a curve in, and out, that are over before anything of the bus is beyond the lane's edge
        private const float StripOut = 2.1f;

        /// <summary>
        /// One curve of a bay's path: how far aside of the bay's line the path still is, by the metres to go to the
        /// standing place (in), or how far it has come away from the bay's line, by the metres gone (out).
        /// </summary>
        public struct Curve
        {
            public float Offset;    // from the lane's line to the bay's
            public float Lambda;    // the exponential's length constant
            public float Short;     // its amplitude
            public float Slope;     // of the straight middle part
            public float EndTail;   // metres at which the exponential ends
            public float EndRamp;   // ...and the straight part
            public float Length;    // ...and the whole curve: beyond, the path is on the lane's line
            public bool Cut;        // there was not the room for it: its exponential is shorter than the lever asks, and the bus would swing wide

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
            // No room for it: steeper across the strip first, then a shorter exponential (the lever then swings further).
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
        /// The length constant of the bus's curve in, near its wheelbase: its body then turns no faster than its rear can
        /// follow, and its nose does not swing over the kerb (NavCheck drives it). Longer is gentler and takes more of the strip.
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
        /// The bay laid out for the bus beside a kerb so far from the lane's line: where its middle stands (metres aside of
        /// the line), its curves in and out for its front axle, and the strip they take behind and ahead of the pole (from
        /// where its body first reaches beyond the lane's edge). False if there is no room: the kerb too near, or a curve cut
        /// short.
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

        // The bus driven along a curve by its front axle, its rear drawn after it (BusPath.Trail), a pose every SweepStep: in,
        // the furthest back from the pole any of its kerb side is beyond the lane's edge (negative); out, the furthest on.
        // Metres along the lane from the pole. Along the curve's points as TransitBus lays them, a straight line between each.
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
                end = rest + curve.Length + 3f * BusWheelbase;   // on along the lane's line until its trailing rear is back in the lane too
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

        /// <summary>How far from the lane's line the kerb must be for a bay: the lane's half, the clearance, the body and the gap to the kerb.</summary>
        public static float KerbNeeded(float sideHalf)
        {
            return LaneHalf + Clear + sideHalf * 2f + KerbGap;
        }

        /// <summary>
        /// Where the middle of a bus that wide stands in a bay, in metres aside of the lane's line: its door side
        /// <see cref="KerbGap"/> from the kerb. 0 if the kerb is too near the lane for a bay.
        /// </summary>
        public static float Offset(float kerbAside, float sideHalf)
        {
            return kerbAside >= KerbNeeded(sideHalf) - 0.001f ? kerbAside - KerbGap - sideHalf : 0f;
        }

        /// <summary>
        /// Metres of the strip that must be free behind the pole for a bus to come in along a curve: from where its nose
        /// first reaches beyond the lane's edge. <paramref name="middleAhead"/> is how far the middle of the bus is ahead of
        /// the point it is turned about (the curve is that point's path, and the middle comes to rest at the pole).
        /// </summary>
        public static float Behind(Curve curveIn, float middleAhead)
        {
            return curveIn.Length + middleAhead - StripIn;
        }

        /// <summary>...and ahead of the pole for it to pull out: to where its nose is back inside the lane's edge.</summary>
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

        // ---------------------------------------------------------------- a bay's zone (BusZones, BusStopDressing)

        /// <summary>
        /// What is left of a run of the game's kerbside parking beside a bus zone, in metres along the lane: the part
        /// before the zone or the part after it, whichever the zone leaves whole. Where the zone lies in the middle of
        /// the run the longer part is kept, and the other is <paramref name="lost"/> (parking given up beyond the zone).
        /// A run wholly in the zone keeps nothing: both ends are then the zone's end. False if the run does not reach
        /// into the zone at all, and nothing is to be done to it.
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
        /// Whether a parked car stands in a bus zone, or too near it: its body runs from <paramref name="carStart"/> to
        /// <paramref name="carEnd"/> metres along the stop's lane and the zone from <paramref name="from"/> to
        /// <paramref name="to"/>, and the body must keep <paramref name="margin"/> clear of both ends. A car that only
        /// touches the margin is in it.
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
        /// The game's parking spots given up when a run of kerbside parking <paramref name="was"/> metres long is cut to
        /// <paramref name="now"/> metres, at one spot each <paramref name="spot"/> metres (ParkingLaneGenerator lays its cars
        /// a spot at a time from one end, and a spot that no longer fits whole is never laid).
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
        /// How many times a broken line's pattern (a dash and its gap, of which <paramref name="paint"/> is the dash's
        /// share) is laid along a line <paramref name="length"/> metres long, for a pattern <paramref name="repeat"/>
        /// metres long, so that the line begins and ends with paint: a whole number of patterns and one dash more,
        /// stretched or squeezed a little to fit. A line shorter than a dash is one dash.
        /// </summary>
        public static float Dashes(float length, float repeat, float paint)
        {
            return Math.Max(0f, (float)Math.Round(length / repeat - paint)) + paint;
        }
    }
}
