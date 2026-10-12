using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The line an ambulance crew wheels its stretcher along, as plain geometry on the ground plane (x and z in
    /// metres; no engine types, so that it can be checked outside the game). It is given the places whoever
    /// pushes walks through, the last of them where the middle of the bed ends up; the corners are rounded to
    /// a radius the stretcher can be turned at, and the whole is laid out by distance walked.
    /// At any distance along it the crew's pose is fixed: whoever pushes on the line, the middle of the bed
    /// on the same line an arm and half a bed further on (so the bed always points from the one to the other
    /// and follows a corner as a rigid thing does), the second of the crew beside its leading end. Because the
    /// last stretch is kept straight for at least that far, the bed ends exactly on the last place, pointing
    /// exactly along the last stretch. Clearance says how near a point comes to everything the crew sweeps
    /// over on the way: the bed, whoever pushes, whoever walks beside it.
    /// Nothing here runs by itself and nothing is allocated after it has been made.
    /// </summary>
    public sealed class CrewPath
    {
        public const float Lead = 1.55f;            // from whoever pushes to the middle of the bed
        public const float BedHalfLength = 1.16f;   // the bed with its push bars
        public const float BedHalfWidth = 0.33f;
        public const float SecondAhead = 1.05f;     // from the middle of the bed to the second of the crew, the way it goes...
        public const float SecondOut = 0.55f;       // ...and out from its middle line
        public const float Body = 0.3f;             // half the width of somebody walking

        private const float Fine = 0.05f;           // metres between the points a corner is drawn with
        private const float Step = 0.1f;            // metres between the points the line is kept as
        private const int Most = 640;
        private const int MostFine = 4096;
        private const float PoseStep = 0.2f;        // metres between the poses a clearance is measured against
        private const int MostPoses = 400;
        private const float FarOff = 5f;            // metres from the whole way beyond which a clearance is only said to be "at least"

        // Metres from the middle of the bed to the furthest of the crew in any pose: whoever pushes is an arm and half a
        // bed behind it along the way (no further in a straight line, however the way bends) and as wide as somebody
        // walking; the bed's own corners and whoever walks beside its leading end are nearer.
        private static readonly float Extent = Math.Max(Lead + Body, Math.Max(Hypot(BedHalfLength, BedHalfWidth), Hypot(SecondAhead, SecondOut) + Body));

        private readonly float[] _x = new float[Most];
        private readonly float[] _z = new float[Most];
        private readonly float[] _y = new float[Most];
        private int _count;
        private float _step = Step;

        private readonly float[] _fineX = new float[MostFine];
        private readonly float[] _fineZ = new float[MostFine];
        private int _fine;

        // The poses along it: whoever pushes, the middle of the bed, the way it points.
        private readonly float[] _poseP = new float[(MostPoses + Extra) * 2];
        private readonly float[] _poseC = new float[(MostPoses + Extra) * 2];
        private readonly float[] _poseH = new float[(MostPoses + Extra) * 2];
        private int _poses;
        private float _minX, _maxX, _minZ, _maxZ;

        /// <summary>True once a line has been made (Build).</summary>
        public bool Built => _count >= 2;

        /// <summary>Metres from the first place to the last, round the corners.</summary>
        public float Length { get; private set; }

        /// <summary>Metres whoever pushes walks: the bed's middle is at the end of the line when they have.</summary>
        public float Travel => Math.Max(0f, Length - Lead);

        /// <summary>1 or -1: the side of the way it goes that the second of the crew walks on (right or left).</summary>
        public float Side { get; private set; } = 1f;

        /// <summary>The corners it was given, rounded or not, for a log line.</summary>
        public int Corners { get; private set; }

        /// <summary>How many points the line is kept as, for whoever gives each its ground height.</summary>
        public int Samples => _count;

        /// <summary>How many poses a clearance is measured against, the swings included (for the checks).</summary>
        internal int PoseCount => _poses;

        /// <summary>One of those poses: whoever pushes, the middle of the bed, the way it points (for the checks).</summary>
        internal void PoseOf(int index, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out float headingX, out float headingZ)
        {
            int at = Math.Max(0, Math.Min(_poses - 1, index)) * 2;
            pusherX = _poseP[at];
            pusherZ = _poseP[at + 1];
            bedX = _poseC[at];
            bedZ = _poseC[at + 1];
            headingX = _poseH[at];
            headingZ = _poseH[at + 1];
        }

        public void Clear()
        {
            _count = 0;
            _poses = 0;
            Length = 0f;
            Corners = 0;
        }

        /// <summary>
        /// Makes the line through the places given (at least two, further apart than a hand's breadth), each
        /// corner rounded to the radius, or to less where a stretch is too short for it. The last stretch keeps
        /// 'keep' metres straight before its end whatever the corner before it would take. False (and no line)
        /// if the places do not make one.
        /// </summary>
        public bool Build(float[] px, float[] pz, int count, float radius, float keep, float side)
        {
            Clear();
            Side = side < 0f ? -1f : 1f;
            if (px == null || pz == null || count < 2)
            {
                return false;
            }
            _fine = 0;
            float lastX = px[0], lastZ = pz[0];
            Emit(lastX, lastZ);
            // The places that are really apart, in order.
            int n = 0;
            for (int i = 0; i < count && n < Scratch; i++)
            {
                if (n > 0 && Hypot(px[i] - _sx[n - 1], pz[i] - _sz[n - 1]) < 0.08f)
                {
                    continue;
                }
                _sx[n] = px[i];
                _sz[n] = pz[i];
                n++;
            }
            if (n < 2)
            {
                return false;
            }
            for (int i = 1; i < n - 1; i++)
            {
                float inX = _sx[i] - _sx[i - 1], inZ = _sz[i] - _sz[i - 1];
                float outX = _sx[i + 1] - _sx[i], outZ = _sz[i + 1] - _sz[i];
                float inLength = Hypot(inX, inZ), outLength = Hypot(outX, outZ);
                inX /= inLength;
                inZ /= inLength;
                outX /= outLength;
                outZ /= outLength;
                float dot = Math.Max(-1f, Math.Min(1f, inX * outX + inZ * outZ));
                float angle = (float)Math.Acos(dot);
                if (angle < 0.02f)
                {
                    continue;   // straight on: no corner here
                }
                Corners++;
                // What each stretch can spare: half of one shared with another corner, all of the first; of the last, what is not kept straight.
                float before = i == 1 ? inLength - 0.02f : inLength * 0.5f;
                float after = i == n - 2 ? Math.Max(0f, outLength - keep) : outLength * 0.5f;
                float tangent = Math.Min((float)(radius * Math.Tan(Math.Min(angle, 3f) * 0.5f)), Math.Min(before, after));
                if (tangent < 0.03f)
                {
                    Emit(_sx[i], _sz[i]);   // no room to round it: a plain corner
                    continue;
                }
                float round = tangent / (float)Math.Tan(angle * 0.5f);
                float cross = inX * outZ - inZ * outX;
                // The side the corner turns to, as a unit vector across the stretch coming in.
                float towardsX = cross > 0f ? -inZ : inZ;
                float towardsZ = cross > 0f ? inX : -inX;
                float fromX = _sx[i] - inX * tangent, fromZ = _sz[i] - inZ * tangent;
                Emit(fromX, fromZ);
                int pieces = Math.Max(2, (int)Math.Ceiling(round * angle / Fine));
                // Turned a piece at a time (the sine and cosine of the angle so far, from those of one piece), and the last
                // exactly: the arc ends where the next stretch begins.
                double pieceSin = Math.Sin(angle / pieces), pieceCos = Math.Cos(angle / pieces);
                double turnSin = 0.0, turnCos = 1.0;
                for (int j = 1; j <= pieces; j++)
                {
                    double nextSin = turnSin * pieceCos + turnCos * pieceSin;
                    turnCos = turnCos * pieceCos - turnSin * pieceSin;
                    turnSin = nextSin;
                    float sin = j == pieces ? (float)Math.Sin(angle) : (float)turnSin, cos = j == pieces ? (float)Math.Cos(angle) : (float)turnCos;
                    Emit(fromX + round * (sin * inX + (1f - cos) * towardsX), fromZ + round * (sin * inZ + (1f - cos) * towardsZ));
                }
            }
            Emit(_sx[n - 1], _sz[n - 1]);

            // Laid out by distance: a point every step, and the last one exactly at the end.
            float total = 0f;
            for (int i = 1; i < _fine; i++)
            {
                total += Hypot(_fineX[i] - _fineX[i - 1], _fineZ[i] - _fineZ[i - 1]);
            }
            if (total < 0.1f)
            {
                return false;
            }
            Length = total;
            _step = Math.Max(Step, total / (Most - 2));
            _count = 0;
            float walked = 0f;
            float next = 0f;
            for (int i = 1; i < _fine; i++)
            {
                float legX = _fineX[i] - _fineX[i - 1], legZ = _fineZ[i] - _fineZ[i - 1];
                float leg = Hypot(legX, legZ);
                if (leg < 1e-6f)
                {
                    continue;
                }
                while (next <= walked + leg && _count < Most - 1 && next < total - 1e-4f)
                {
                    float t = (next - walked) / leg;
                    _x[_count] = _fineX[i - 1] + legX * t;
                    _z[_count] = _fineZ[i - 1] + legZ * t;
                    _y[_count] = 0f;
                    _count++;
                    next += _step;
                }
                walked += leg;
            }
            _x[_count] = _fineX[_fine - 1];
            _z[_count] = _fineZ[_fine - 1];
            _y[_count] = 0f;
            _count++;

            // The poses a clearance is measured against, and the box they all lie in.
            _poses = 0;
            _minX = _minZ = float.MaxValue;
            _maxX = _maxZ = float.MinValue;
            float travel = Travel;
            float poseStep = Math.Max(PoseStep, travel / (MostPoses - 2));
            for (float s = 0f; _poses < MostPoses; s += poseStep)
            {
                bool last = s >= travel;
                Pose(last ? travel : s, out float pX, out float pZ, out float cX, out float cZ, out float hX, out float hZ);
                int at = _poses * 2;
                _poseP[at] = pX;
                _poseP[at + 1] = pZ;
                _poseC[at] = cX;
                _poseC[at + 1] = cZ;
                _poseH[at] = hX;
                _poseH[at + 1] = hZ;
                _poses++;
                _minX = Math.Min(_minX, Math.Min(pX, cX));
                _maxX = Math.Max(_maxX, Math.Max(pX, cX));
                _minZ = Math.Min(_minZ, Math.Min(pZ, cZ));
                _maxZ = Math.Max(_maxZ, Math.Max(pZ, cZ));
                if (last)
                {
                    break;
                }
            }
            return true;
        }

        private const int Scratch = 16;
        private readonly float[] _sx = new float[Scratch];
        private readonly float[] _sz = new float[Scratch];

        private void Emit(float x, float z)
        {
            if (_fine > 0 && Hypot(x - _fineX[_fine - 1], z - _fineZ[_fine - 1]) < 1e-5f)
            {
                return;
            }
            if (_fine < MostFine)
            {
                _fineX[_fine] = x;
                _fineZ[_fine] = z;
                _fine++;
            }
        }

        /// <summary>One of the points the line is kept as.</summary>
        public void Sample(int index, out float x, out float z)
        {
            index = Math.Max(0, Math.Min(_count - 1, index));
            x = _x[index];
            z = _z[index];
        }

        /// <summary>The ground height at one of those points, given by whoever knows the ground.</summary>
        public void SetHeight(int index, float height)
        {
            if (index >= 0 && index < _count)
            {
                _y[index] = height;
            }
        }

        /// <summary>The point of the line so far along it.</summary>
        public void At(float along, out float x, out float z)
        {
            Locate(along, out int index, out float t);
            x = _x[index] + (_x[index + 1] - _x[index]) * t;
            z = _z[index] + (_z[index + 1] - _z[index]) * t;
        }

        /// <summary>The ground height there.</summary>
        public float Height(float along)
        {
            Locate(along, out int index, out float t);
            return _y[index] + (_y[index + 1] - _y[index]) * t;
        }

        // Between which two kept points a distance lies, and how far from the first to the second. (The last stretch is shorter than a step.)
        private void Locate(float along, out int index, out float t)
        {
            if (_count < 2)
            {
                index = 0;
                t = 0f;
                return;
            }
            along = Math.Max(0f, Math.Min(Length, along));
            index = Math.Min(_count - 2, (int)(along / _step));
            float from = index * _step;
            float span = index == _count - 2 ? Length - from : _step;
            t = span > 1e-6f ? Math.Max(0f, Math.Min(1f, (along - from) / span)) : 0f;
        }

        /// <summary>
        /// The crew when whoever pushes has walked so far (0 to Travel): where they are, where the middle of the
        /// bed is, and the way it points (a unit vector from the one to the other).
        /// </summary>
        public void Pose(float along, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out float headingX, out float headingZ)
        {
            along = Math.Max(0f, Math.Min(Travel, along));
            At(along, out pusherX, out pusherZ);
            At(along + Lead, out bedX, out bedZ);
            headingX = bedX - pusherX;
            headingZ = bedZ - pusherZ;
            float length = Hypot(headingX, headingZ);
            if (length < 0.05f)
            {
                // A line shorter than an arm and half a bed: the way its end runs.
                headingX = _x[_count - 1] - _x[_count - 2];
                headingZ = _z[_count - 1] - _z[_count - 2];
                length = Math.Max(1e-5f, Hypot(headingX, headingZ));
            }
            headingX /= length;
            headingZ /= length;
        }

        /// <summary>Where the second of the crew walks when whoever pushes has walked so far.</summary>
        public void Second(float along, out float x, out float z)
        {
            Pose(along, out _, out _, out float bedX, out float bedZ, out float headingX, out float headingZ);
            // (Right of the way it goes: z, -x.)
            x = bedX + headingX * SecondAhead + headingZ * (Side * SecondOut);
            z = bedZ + headingZ * SecondAhead - headingX * (Side * SecondOut);
        }

        /// <summary>
        /// Metres from a point to the nearest thing the crew is at any moment of the way: the bed, whoever pushes
        /// it, whoever walks beside its leading end. 0 if the point is in one of them at some moment.
        /// </summary>
        public float Clearance(float x, float z)
        {
            if (_poses == 0)
            {
                return float.MaxValue;
            }
            // Far from the whole of it (nothing of the crew is further than 1.5 m from where whoever pushes or the bed's
            // middle is): at least this far, which is all anybody asking about something five metres off needs.
            float outside = Math.Max(Math.Max(_minX - x, x - _maxX), Math.Max(_minZ - z, z - _maxZ));
            if (outside > FarOff)
            {
                return outside - 1.5f;
            }
            // The nearest middle of the bed first. The crew in a pose comes no nearer than its bed's middle less Extent, and
            // the bed itself no further than its middle: only the poses with the middle within Extent of the nearest one
            // can be the nearest, and only those are measured. (A millimetre more, for the rounding of the two.)
            float nearest = float.MaxValue;
            for (int i = 0; i < _poses; i++)
            {
                int at = i * 2;
                float offX = x - _poseC[at], offZ = z - _poseC[at + 1];
                nearest = Math.Min(nearest, offX * offX + offZ * offZ);
            }
            float bound = (float)Math.Sqrt(nearest) + Extent + 0.001f;
            float boundSqr = bound * bound;
            float least = float.MaxValue;
            for (int i = 0; i < _poses; i++)
            {
                int at = i * 2;
                float offX = x - _poseC[at], offZ = z - _poseC[at + 1];
                if (offX * offX + offZ * offZ > boundSqr)
                {
                    continue;
                }
                least = Math.Min(least, PoseClearance(_poseP[at], _poseP[at + 1], _poseC[at], _poseC[at + 1], _poseH[at], _poseH[at + 1], Side, x, z));
                if (least <= 0f)
                {
                    return 0f;
                }
            }
            return least;
        }

        /// <summary>The same for the crew in one pose: whoever pushes, the middle of the bed, the way it points, the side the second walks on.</summary>
        public static float PoseClearance(float pusherX, float pusherZ, float bedX, float bedZ, float headingX, float headingZ, float side, float x, float z)
        {
            float dX = x - bedX, dZ = z - bedZ;
            float alongBed = Math.Abs(dX * headingX + dZ * headingZ) - BedHalfLength;
            float acrossBed = Math.Abs(dX * headingZ - dZ * headingX) - BedHalfWidth;
            float bed = alongBed <= 0f && acrossBed <= 0f ? 0f : Hypot(Math.Max(alongBed, 0f), Math.Max(acrossBed, 0f));
            float pusher = Hypot(x - pusherX, z - pusherZ) - Body;
            float secondX = bedX + headingX * SecondAhead + headingZ * (side * SecondOut);
            float secondZ = bedZ + headingZ * SecondAhead - headingX * (side * SecondOut);
            float second = Hypot(x - secondX, z - secondZ) - Body;
            return Math.Max(0f, Math.Min(bed, Math.Min(pusher, second)));
        }

        /// <summary>
        /// Adds a swing to what the crew sweeps over: the bed turned and moved from one pose to another with the
        /// crew at its ends (the move that lays it alongside someone, or brings it round square to the rear doors),
        /// so that Clearance and Crossing count it. A pose is the bed's middle and the way it points.
        /// </summary>
        public void AddSwing(float fromX, float fromZ, float fromHeadingX, float fromHeadingZ, float toX, float toZ, float toHeadingX, float toHeadingZ)
        {
            for (int i = 0; i <= SwingPoses && _poses < MostPoses + Extra; i++)
            {
                Blend(fromX, fromZ, fromHeadingX, fromHeadingZ, toX, toZ, toHeadingX, toHeadingZ, (float)i / SwingPoses,
                    out float x, out float z, out float headingX, out float headingZ);
                int at = _poses * 2;
                _poseC[at] = x;
                _poseC[at + 1] = z;
                _poseH[at] = headingX;
                _poseH[at + 1] = headingZ;
                _poseP[at] = x - headingX * Lead;
                _poseP[at + 1] = z - headingZ * Lead;
                _minX = Math.Min(_minX, Math.Min(x, _poseP[at]));
                _maxX = Math.Max(_maxX, Math.Max(x, _poseP[at]));
                _minZ = Math.Min(_minZ, Math.Min(z, _poseP[at + 1]));
                _maxZ = Math.Max(_maxZ, Math.Max(z, _poseP[at + 1]));
                _poses++;
            }
        }

        /// <summary>How many poses a swing is measured as (the first and the last included).</summary>
        public const int SwingPoses = 12;
        private const int Extra = (SwingPoses + 1) * 2;

        /// <summary>
        /// The part of a straight line (a length of tape, say) that the crew comes within a distance of, as
        /// fractions of it from its first end to its second: from the first such place to the last. False if no part
        /// of it is that near. Worked out, not looked for: in each pose the crew is the bed and two people, and the part
        /// of a line within a distance of a box or of a person is found exactly, so a pose costs a few sums and a pose
        /// too far from the line costs one test. (It was asked point by point, every 10 cm of the line against every pose
        /// of the way: some 60,000 askings a way, and the crew's planning tries hundreds of ways in one frame.)
        /// </summary>
        public bool Crossing(float fromX, float fromZ, float toX, float toZ, float within, out float low, out float high)
        {
            low = 1f;
            high = 0f;
            if (_poses == 0 || within <= 0f)
            {
                return false;   // (nothing of the crew is nearer than no distance at all)
            }
            float runX = toX - fromX, runZ = toZ - fromZ;
            float lengthSqr = runX * runX + runZ * runZ;
            if (lengthSqr < 1e-8f)
            {
                if (Clearance(fromX, fromZ) < within)
                {
                    low = 0f;
                    high = 1f;
                }
                return high >= low;
            }
            // Nowhere near the whole way: no part of it.
            float reach = Extent + within;
            if (Math.Max(fromX, toX) < _minX - reach || Math.Min(fromX, toX) > _maxX + reach
                || Math.Max(fromZ, toZ) < _minZ - reach || Math.Min(fromZ, toZ) > _maxZ + reach)
            {
                return false;
            }
            float reachSqr = reach * reach;
            float person = Body + within;
            for (int i = 0; i < _poses; i++)
            {
                int at = i * 2;
                float bedX = _poseC[at], bedZ = _poseC[at + 1];
                // The point of the line nearest the bed's middle: nothing of the crew in this pose is further than Extent from that middle.
                float t = ((bedX - fromX) * runX + (bedZ - fromZ) * runZ) / lengthSqr;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                float offX = fromX + runX * t - bedX, offZ = fromZ + runZ * t - bedZ;
                if (offX * offX + offZ * offZ >= reachSqr)
                {
                    continue;
                }
                float headingX = _poseH[at], headingZ = _poseH[at + 1];
                // The bed, grown by the distance: lengthened, widened, and a disc at each of its corners.
                Box(fromX, fromZ, runX, runZ, bedX, bedZ, headingX, headingZ, BedHalfLength + within, BedHalfWidth, ref low, ref high);
                Box(fromX, fromZ, runX, runZ, bedX, bedZ, headingX, headingZ, BedHalfLength, BedHalfWidth + within, ref low, ref high);
                for (int c = 0; c < 4; c++)
                {
                    float along = (c & 1) == 0 ? BedHalfLength : -BedHalfLength;
                    float across = (c & 2) == 0 ? BedHalfWidth : -BedHalfWidth;
                    Disc(fromX, fromZ, runX, runZ, lengthSqr, bedX + headingX * along + headingZ * across, bedZ + headingZ * along - headingX * across, within, ref low, ref high);
                }
                // Whoever pushes, and whoever walks beside the leading end.
                Disc(fromX, fromZ, runX, runZ, lengthSqr, _poseP[at], _poseP[at + 1], person, ref low, ref high);
                Disc(fromX, fromZ, runX, runZ, lengthSqr, bedX + headingX * SecondAhead + headingZ * (Side * SecondOut), bedZ + headingZ * SecondAhead - headingX * (Side * SecondOut),
                    person, ref low, ref high);
            }
            return high >= low;
        }

        // The part of the line from (fromX, fromZ) by (runX, runZ) that is inside a box on the ground (its middle, the way
        // its length runs, half its length and half its width), as fractions of the line, taken into low and high.
        private static void Box(float fromX, float fromZ, float runX, float runZ, float midX, float midZ, float alongX, float alongZ,
            float halfLength, float halfWidth, ref float low, ref float high)
        {
            float offX = fromX - midX, offZ = fromZ - midZ;
            float first = 0f, last = 1f;
            // (Across the way it points is (alongZ, -alongX), as PoseClearance measures it.)
            if (Slab(offX * alongX + offZ * alongZ, runX * alongX + runZ * alongZ, halfLength, ref first, ref last)
                && Slab(offX * alongZ - offZ * alongX, runX * alongZ - runZ * alongX, halfWidth, ref first, ref last))
            {
                low = Math.Min(low, first);
                high = Math.Max(high, last);
            }
        }

        // Narrows first..last to where start + t * run lies within half of nothing. False if that leaves nothing.
        private static bool Slab(float start, float run, float half, ref float first, ref float last)
        {
            if (Math.Abs(run) < 1e-9f)
            {
                return Math.Abs(start) < half && first < last;
            }
            float a = (-half - start) / run, b = (half - start) / run;
            if (a > b)
            {
                float swap = a;
                a = b;
                b = swap;
            }
            first = Math.Max(first, a);
            last = Math.Min(last, b);
            return first < last;
        }

        // The part of the line that is nearer a point than the radius, as fractions of the line, taken into low and high.
        private static void Disc(float fromX, float fromZ, float runX, float runZ, float lengthSqr, float x, float z, float radius, ref float low, ref float high)
        {
            float offX = fromX - x, offZ = fromZ - z;
            float half = offX * runX + offZ * runZ;   // half the middle term of the square to be solved
            float rest = offX * offX + offZ * offZ - radius * radius;
            float spread = half * half - lengthSqr * rest;
            if (spread <= 0f)
            {
                return;
            }
            float root = (float)Math.Sqrt(spread);
            float first = Math.Max(0f, (-half - root) / lengthSqr), last = Math.Min(1f, (-half + root) / lengthSqr);
            if (first < last)
            {
                low = Math.Min(low, first);
                high = Math.Max(high, last);
            }
        }

        /// <summary>
        /// Metres from the crew's swept way to a box on the ground (a vehicle's footprint): its middle, the way it
        /// points and its half length and half width. 0 if they overlap at some moment.
        /// </summary>
        public float ClearanceToBox(float centreX, float centreZ, float forwardX, float forwardZ, float halfLength, float halfWidth)
        {
            float norm = Math.Max(1e-5f, Hypot(forwardX, forwardZ));
            forwardX /= norm;
            forwardZ /= norm;
            float least = float.MaxValue;
            // Round its edge a hand's breadth at a time, and its middle (for a box wholly inside what is swept).
            int along = Math.Max(2, (int)Math.Ceiling(halfLength * 2f / 0.15f));
            int across = Math.Max(2, (int)Math.Ceiling(halfWidth * 2f / 0.15f));
            for (int i = 0; i <= along; i++)
            {
                float a = -halfLength + halfLength * 2f * i / along;
                for (int j = 0; j <= across; j++)
                {
                    if (i != 0 && i != along && j != 0 && j != across && (i != along / 2 || j != across / 2))
                    {
                        continue;
                    }
                    float b = -halfWidth + halfWidth * 2f * j / across;
                    least = Math.Min(least, Clearance(centreX + forwardX * a + forwardZ * b, centreZ + forwardZ * a - forwardX * b));
                }
            }
            return least;
        }

        /// <summary>How far whoever pushes has walked when the middle of the bed is nearest a point: for saying when it passes somewhere.</summary>
        public float Nearest(float x, float z)
        {
            float best = float.MaxValue;
            int found = 0;
            for (int i = 0; i < _count; i++)
            {
                float distance = Hypot(_x[i] - x, _z[i] - z);
                if (distance < best)
                {
                    best = distance;
                    found = i;
                }
            }
            float along = found == _count - 1 ? Length : found * _step;
            return Math.Max(0f, Math.Min(Travel, along - Lead));
        }

        /// <summary>
        /// A pose part of the way from one to another, for the last small move that puts the bed exactly where
        /// it belongs: the middle straight across, the heading turned the short way round. t from 0 to 1.
        /// </summary>
        public static void Blend(float fromX, float fromZ, float fromHeadingX, float fromHeadingZ, float toX, float toZ, float toHeadingX, float toHeadingZ,
            float t, out float x, out float z, out float headingX, out float headingZ)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            x = fromX + (toX - fromX) * t;
            z = fromZ + (toZ - fromZ) * t;
            float turn = Turn(fromHeadingX, fromHeadingZ, toHeadingX, toHeadingZ) * t;
            float sin = (float)Math.Sin(turn), cos = (float)Math.Cos(turn);
            headingX = fromHeadingX * cos - fromHeadingZ * sin;
            headingZ = fromHeadingX * sin + fromHeadingZ * cos;
            float length = Math.Max(1e-5f, Hypot(headingX, headingZ));
            headingX /= length;
            headingZ /= length;
        }

        /// <summary>The angle from one direction on the ground to another, in radians, the short way round (-pi to pi).</summary>
        public static float Turn(float fromX, float fromZ, float toX, float toZ)
        {
            return (float)Math.Atan2(fromX * toZ - fromZ * toX, fromX * toX + fromZ * toZ);
        }

        private static float Hypot(float x, float z)
        {
            return (float)Math.Sqrt(x * x + z * z);
        }
    }
}
