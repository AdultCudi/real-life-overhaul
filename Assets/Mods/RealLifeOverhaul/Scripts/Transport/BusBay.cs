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
    /// That is what makes a bay some thirty metres long for a 6.7 m bus, as it is on a real street.
    /// </summary>
    public static class BusBay
    {
        /// <summary>Half the width of a driving lane: the lane's edge is this far from its line (a lane is 3.3 m: EmergencyDriver.PassOffset).</summary>
        public const float LaneHalf = 1.65f;

        /// <summary>Metres between the side of a bus standing true in its bay and the lane's edge, at the least: what a bay is laid out for.</summary>
        public const float Clear = 0.15f;

        /// <summary>Metres between the door side of a bus standing in its bay and the kerb.</summary>
        public const float KerbGap = 0.20f;

        /// <summary>The widest thing the game's traffic drives (a lorry), halved: what passes on the lane's line reaches this far.</summary>
        public const float WidestHalf = 1.30f;

        /// <summary>The driver takes a point of a bay's path as reached this near it (4.5 m on the open lane): it keeps to the curve.</summary>
        public const float Reach = 2f;

        /// <summary>Metres between the points of a bay's path.</summary>
        public const float Step = 1f;

        /// <summary>m/s in and out of a bay.</summary>
        public const float Speed = 4.5f;

        /// <summary>Metres of lane a bay's path may take up before and after the pole: a stop has plain lane for 26 m either side (TransitMap.JunctionClear).</summary>
        public const float RunMost = 25f;

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
