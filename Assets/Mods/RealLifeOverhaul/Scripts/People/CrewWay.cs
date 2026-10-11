using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The ambulance crew's one way from the rear doors to the casualty and back, and the fixed layout at the
    /// casualty it leads to. Worked out once, when the ambulance has stopped, and everything else is made to
    /// agree with it: the stretcher and both of the crew are moved along it (CrewPath, Stretcher.Wheel), the
    /// cordon opens its tape where it crosses and keeps posts, cones and barriers off it (SceneCordon.SetCorridor),
    /// and nobody kneels where the stretcher stands. The navigation mesh has no part in it: the way is a line
    /// with rounded corners that is tested, pose by pose, against every vehicle and fixture, the tape and the
    /// casualty's own body before anyone sets off, so nothing on it can turn the crew aside.
    /// The layout comes from the casualty's own body axis: the stretcher parallel to the body, head to head,
    /// 0.55 m from it (on the side away from the casualty's own car, else the side help comes from; the other
    /// side if only that one can be reached); whoever treats kneels at the chest on the other side; the second
    /// of the crew at the head or the hips.
    /// The way: out sideways behind the doors, along one side of the ambulance, straight to the casualty or
    /// through the end of the tape square to it; then one of two last moves, whichever has room. A run-in:
    /// round a corner onto the body's own line and straight along it to its place. Or a swing-in: straight at
    /// the casualty's side, stopping 2.1 m short of their line, and the bed swung a quarter turn round into
    /// its place with the crew at its ends (where the body lies across a road too narrow for a run-in).
    /// Back the same way with the other of the crew pushing, round behind the ambulance and up to the rear
    /// doors square, its leading end 0.25 m from them; where there is no room behind for that, swung square
    /// on the spot. On the carriageway and out of any lane traffic is let past in if that can be had; over a
    /// pavement or through that lane (held shut meanwhile) only if not. A way that cannot be made at all is
    /// said so, with what was in it. All of it is asked once per outing; nothing here runs.
    /// </summary>
    public sealed partial class CrewWay
    {
        public const float Width = 2.3f;            // metres the crew takes up across its way: the bed, and whoever walks beside its leading end, with a little either side
        public const float BedOut = 0.85f;          // from the body's axis to the stretcher's middle line: 0.55 m to its near rail
        public const float DoorGap = 0.25f;         // from the rear doors to the stretcher's leading end when it is loaded
        private const float BedHalfWidth = 0.3f;
        private const float Gap = 0.3f;             // what every place keeps from anything solid, and from the stretcher
        private const float BedBack = 0.35f;        // the stretcher's middle is this far towards the feet from the chest: hips on its middle, head on the pillow
        private const float Reach = CrewPath.Lead;  // from the stretcher's middle to whoever pushes it
        private const float Sweep = 0.85f;          // from the middle line of the way to the outside of whoever walks beside the bed's leading end
        private const float Corner = 1.2f;          // metres: the radius the corners of the way are rounded to
        private const float Keep = CrewPath.Lead + 0.05f;   // of the last stretch, kept straight: the bed ends pointing exactly along it
        private const float Beside = Sweep + 0.4f;  // from the ambulance's side to the middle line of the way
        private const float DoorBack = 1.3f;        // behind the rear doors, where the crew steps out
        private const float TurnBack = 4.3f;        // behind the rear doors, where the stretcher is brought round to face them
        private const float PivotBack = 1.6f;       // ...or, without room for that, where it is swung square on the spot
        private const float SwingOut = 1.25f;       // from where the bed stands beside the casualty to where it is swung in from: 2.1 m from their line
        private const float BodyClear = 0.2f;       // what the bed and the crew keep from the middle line of whoever lies there, on the way
        private const float TapeClear = 0.3f;       // ...and from the tape (SceneCordon keeps its posts as far from them)
        private const float MostOpening = 3.6f;     // metres of one side of the tape the way may come that near: an opening, not a side taken down
        private const float HeadRound = 1.15f;      // from the chest to where someone walks round the head...
        private const float FeetRound = 1.9f;       // ...and round the feet
        private const int Places = 12;

        private static readonly float[] RunIns = { 1.3f, 2.4f, 3.8f };   // metres from the corner onto the body's line to where whoever pushes stops

        private Rigidbody _ambulance;
        private float _kneelOut, _kneelAlong, _secondOut;
        private Vector3 _preferred;                 // the way along the body that leads away from the ambulance
        private float _halfWidth;

        // The taped rectangle, if the scene has one (SceneCordon.Frame).
        private SceneCordon _cordon;
        private bool _framed;
        private Vector3 _scene, _forward, _right;
        private float _back, _front, _left, _rightEdge;

        // The places of the way being tried, and of the best found so far.
        private readonly float[] _x = new float[Places];
        private readonly float[] _z = new float[Places];
        private int _count;
        private readonly float[] _bestX = new float[Places];
        private readonly float[] _bestZ = new float[Places];
        private int _bestCount;
        private readonly CrewPath _try = new CrewPath();
        private string _blocked;                    // what stood in the last way that was refused, for Why
        private bool _tryOpen;                      // the way being tried runs in a lane traffic is let past in

        /// <summary>The way out, from the rear doors to beside the casualty; and the way back, to the rear doors.</summary>
        public readonly CrewPath Out = new CrewPath();

        /// <summary>See <see cref="Out"/>.</summary>
        public readonly CrewPath Home = new CrewPath();

        /// <summary>True once the layout at the casualty has been worked out (Lay).</summary>
        public bool Laid { get; private set; }

        /// <summary>True if a way was found (Plan).</summary>
        public bool Planned { get; private set; }

        public string Why { get; private set; }       // why no way could be planned; null if one was
        public string Failed { get; private set; }    // the first place of the layout that failed its clear test; null if none did
        public float Straight { get; private set; }   // rear doors to the stretcher's place, as the crow flies

        public Vector3 Chest { get; private set; }
        public Vector3 Axis { get; private set; }     // hips to head
        public Vector3 Side { get; private set; }     // from the body towards the stretcher
        public Vector3 Bed { get; private set; }      // the stretcher's middle when parked; its head end points along Axis
        public Vector3 Heading { get; private set; }  // the way it points when it is parked: along the body, one way or the other
        public float Handle { get; private set; }     // 1: pushed from its foot end (head first); -1: from its head end
        public Vector3 Stop { get; private set; }     // where whoever pushes stands when it is parked
        public Vector3 Kneel { get; private set; }    // whoever treats: at the chest, on the side away from the stretcher
        public Vector3 Second { get; private set; }   // the other of the crew: at the head or the hips, outside the stretcher's footprint

        public Vector3 Door { get; private set; }     // on the road behind the rear doors, on the ambulance's middle line: where whoever pushes steps out
        public Vector3 SecondOut { get; private set; }   // ...and where the other of the crew does: beside the stretcher's leading end
        public Vector3 RearFace { get; private set; } // the rear doors themselves
        public Vector3 Back { get; private set; }     // the way the rear doors face
        public float AwaySide { get; private set; }   // -1 or 1: the side of the ambulance the way runs along (its left or right)
        public float Guide { get; private set; }      // -1 or 1: the side of the way out the second of the crew walks on (the side that ends away from the casualty)

        public bool Swing { get; private set; }       // the last move is a swing-in, not a run-in
        public Vector3 SwingFrom { get; private set; }   // ...from here (the bed's middle), pointing at the casualty's side
        public bool Square { get; private set; }      // there is room behind the ambulance to bring the stretcher round square to the doors
        public Vector3 HomeHeading { get; private set; } // the way the bed points when whoever pushes it back takes hold: they stand at its other end
        public Vector3 HomeBed { get; private set; }  // the bed's middle when it is loaded
        public bool HomeChecked { get; private set; } // the way back passed the same tests as the way out
        public int Tier { get; private set; }         // 0: on the carriageway, out of any open lane; 1: wherever there was room
        public bool OpenLane { get; private set; }    // it runs in a lane traffic is let past in: that lane is held shut while the crew is on it
        public bool BySide { get; private set; }      // the stretcher stands on the other side of the casualty from the one first chosen
        public float OutGate { get; private set; }    // metres whoever pushes has walked when the bed's middle crosses the tape on the way out; -1 if it does not
        public float HomeGate { get; private set; }   // ...and on the way back
        public float DoorsToTape { get; private set; }   // metres from the rear doors to the tape, along the road; -1 with no tape between them and the casualty
        public string Between { get; private set; }   // what stands between the ambulance's nose and the tape; null if nothing does

        public void Clear()
        {
            Laid = false;
            Planned = false;
            Why = null;
            Failed = null;
            _ambulance = null;
            _cordon = null;
            _framed = false;
            Swing = false;
            Square = false;
            OpenLane = false;
            BySide = false;
            Out.Clear();
            Home.Clear();
        }

        /// <summary>
        /// The layout at the casualty, from how they lie. Tried on the preferred side first (away from the
        /// vehicle named, if one stands beside them; else the side help comes from), then on the other; if
        /// neither passes every clear test the preferred side is kept and Failed says what was in the way.
        /// </summary>
        /// <param name="kneelOut">Metres from the chest to whoever treats (the reach of the clip they work in).</param>
        /// <param name="kneelAlong">...and towards the head from the middle of the chest.</param>
        public void Lay(Vector3 chest, Vector3 axis, Vector3 from, float kneelOut, float kneelAlong, float secondOut, bool beside, Vector3 vehicle)
        {
            Clear();
            axis.y = 0f;
            axis = axis.sqrMagnitude < 0.01f ? Vector3.forward : axis.normalized;
            Chest = chest;
            Axis = axis;
            _kneelOut = kneelOut;
            _kneelAlong = kneelAlong;
            _secondOut = secondOut;
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            if (beside ? Vector3.Dot(side, vehicle - chest) > 0f : Vector3.Dot(side, from - chest) < 0f)
            {
                side = -side;
            }
            // Wheeled in along the body, away from the ambulance, if that can be had.
            _preferred = Vector3.Dot(axis, chest - from) >= 0f ? axis : -axis;
            string first = Try(side);
            if (first != null && Try(-side) != null)
            {
                Try(side);
                Failed = first;
            }
            Face(_preferred);
            Laid = true;
        }

        // The layout on one side of the body. Null if the stretcher's place and the treating paramedic's are clear; else which was not, and of what.
        private string Try(Vector3 side)
        {
            Side = side;
            Bed = Chest - Axis * BedBack + side * BedOut;
            Kneel = Chest - side * _kneelOut + Axis * _kneelAlong;
            Collider hit = SceneParking.InStrip(Bed - Axis * (Stretcher.HalfLength + Gap), Bed + Axis * (Stretcher.HalfLength + Gap), BedHalfWidth + Gap);
            if (hit != null)
            {
                return "the stretcher's place beside the casualty (" + Name(hit) + ")";
            }
            if (!OnCarriage(Bed))
            {
                return "the stretcher's place beside the casualty (not on the carriageway)";
            }
            hit = In(Kneel);
            if (hit != null)
            {
                return "the treating paramedic's place at the chest (" + Name(hit) + ")";
            }
            return null;
        }

        // The way the bed points when parked, and what follows from it: which end it is pushed from, where whoever
        // pushes stands, and where they kneel afterwards (from the feet they go round to the hips, from the head
        // they are at the head already; the other of the two if that one is not clear).
        private void Face(Vector3 heading)
        {
            Heading = heading;
            Handle = Vector3.Dot(heading, Axis) > 0f ? 1f : -1f;
            Stop = Bed - heading * Reach;
            Guide = Vector3.Dot(Vector3.Cross(Vector3.up, heading), Side) >= 0f ? 1f : -1f;
            Vector3 head = Chest + Axis * 1.05f - Side * 0.1f;
            Vector3 hips = Chest - Axis * (0.9f - _kneelAlong) - Side * _secondOut;   // 0.9 m from whoever is at the chest
            bool headIn = In(head) != null;
            bool hipsIn = In(hips) != null;
            Second = Handle > 0f ? (!hipsIn || headIn ? hips : head) : (!headIn || hipsIn ? head : hips);
        }

        /// <summary>
        /// The way from the ambulance's rear doors to the stretcher's place and back. Needs Lay first. False, with
        /// Why, if none could be found: the stretcher then stays in the ambulance.
        /// </summary>
        /// <param name="cordon">The measured scene (its tape may or may not be up), or null.</param>
        public bool Plan(VehicleComponent ambulance, SceneCordon cordon)
        {
            Planned = false;
            Why = null;
            Out.Clear();
            Home.Clear();
            if (!Laid || ambulance == null)
            {
                Why = "there is no layout to plan it to";
                return false;
            }
            _ambulance = ambulance.rb;
            _cordon = cordon;
            _framed = cordon != null && cordon.Frame(out _scene, out _forward, out _right, out _back, out _front, out _left, out _rightEdge);
            TrafficTools.Footprint(ambulance, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (StreetSpots.Ground(centre, out float ground))
            {
                centre.y = ground;
            }
            _halfWidth = halfWidth;
            Back = -forward;
            RearFace = centre - forward * halfLength;
            Door = RearFace - forward * DoorBack;
            Straight = Flat(Bed - Door).magnitude;
            Measure(centre, forward, halfLength, halfWidth);
            Look();   // the street round the scene, read once: every way below is tested against that (CrewWay.Survey)

            Vector3 firstSide = Side;
            float best = float.MaxValue;
            Vector3 bestSide = firstSide, bestHeading = _preferred, bestFrom = default;
            float bestAway = 1f;
            bool bestSwing = false, bestOpen = false;
            _blocked = null;
            for (int tier = 0; tier < 2 && best == float.MaxValue; tier++)
            {
                _askedNow = 0;   // (each tier has its own share of what the engine may be asked)
                for (int body = 0; body < 2; body++)
                {
                    // (On the carriageway a side of the body that failed its own clear test is not tried; wherever there is room, both are.)
                    if (Try(body == 0 ? firstSide : -firstSide) != null && tier == 0)
                    {
                        continue;
                    }
                    for (int move = 0; move < RunIns.Length * 2 + 2; move++)
                    {
                        bool swing = move >= RunIns.Length * 2;
                        Vector3 heading = move % 2 == 0 ? _preferred : -_preferred;
                        // The last places: the corner onto the body's line and the bed's place; or where it is swung in from, come at straight.
                        Vector3 from = Bed + Side * SwingOut;
                        Vector3 turn = swing ? from + Side * (Reach + RunIns[0]) : Bed - heading * (Reach + RunIns[move / 2]);
                        Vector3 last = swing ? from : Bed;
                        float guide = Vector3.Dot(Vector3.Cross(Vector3.up, heading), Side) >= 0f ? 1f : -1f;
                        float penalty = (body == 1 ? 3f : 0f) + (swing ? 1f : 0f) + (move % 2 == 1 ? 0.5f : 0f);
                        for (int away = 0; away < 2; away++)
                        {
                            float side = away == 0 ? -1f : 1f;
                            Vector3 beside = Door + right * (side * (halfWidth + Beside));
                            for (int route = 0; route < Routes; route++)
                            {
                                if (!Route(route, beside, turn, last))
                                {
                                    continue;
                                }
                                float length = Length() + penalty;
                                if (length >= best)
                                {
                                    continue;
                                }
                                _ways++;
                                if (Valid(tier, guide, swing, from, -Side, Bed, heading, home: false))
                                {
                                    best = length;
                                    bestSide = Side;
                                    bestHeading = heading;
                                    bestFrom = from;
                                    bestAway = side;
                                    bestSwing = swing;
                                    bestOpen = _tryOpen;
                                    Tier = tier;
                                    _bestCount = _count;
                                    for (int i = 0; i < _count; i++)
                                    {
                                        _bestX[i] = _x[i];
                                        _bestZ[i] = _z[i];
                                    }
                                    break;   // (the straight one first, then by the tape's end, then to one side: the first that is clear is the one for this side and move)
                                }
                            }
                        }
                    }
                }
            }
            if (best == float.MaxValue)
            {
                Forget();
                Try(firstSide);
                Face(_preferred);
                Why = "no way " + Width.ToString("F1") + " m wide is clear from either side of the ambulance to either side of the casualty, by a run-in or a swing-in"
                      + (_blocked != null ? " (the last one tried: " + _blocked + ")" : "");
                return false;
            }

            // The one chosen: the layout on its side, the way out, and the way back.
            Try(bestSide);
            BySide = Vector3.Dot(bestSide, firstSide) < 0f;
            Face(bestHeading);
            AwaySide = bestAway;
            Swing = bestSwing;
            SwingFrom = bestFrom;
            OpenLane = bestOpen;
            _count = _bestCount;
            for (int i = 0; i < _count; i++)
            {
                _x[i] = _bestX[i];
                _z[i] = _bestZ[i];
            }
            Out.Build(_x, _z, _count, Corner, Keep, Guide);
            if (Swing)
            {
                Out.AddSwing(SwingFrom.x, SwingFrom.z, -Side.x, -Side.z, Bed.x, Bed.z, Heading.x, Heading.z);
            }
            Level(Out);
            Out.Second(0f, out float secondX, out float secondZ);
            SecondOut = new Vector3(secondX, Out.Height(0f), secondZ);
            OutGate = Gate(Out, entering: true);
            _askedNow = 0;   // (...and so has the way back)
            PlanHome(right);
            Forget();
            Planned = true;
            return true;
        }

        // The way back: from the other end of the bed (or, after a swing-in, swung out to point back the way it came),
        // by the places of the way out in reverse, then round behind the ambulance to the rear doors.
        private void PlanHome(Vector3 right)
        {
            Vector3 over = right * AwaySide;
            float out1 = _halfWidth + Beside;
            HomeBed = RearFace + Back * (DoorGap + Stretcher.HalfLength);
            // What of the way out is walked again: everything between the place beside the ambulance and the bed's own.
            int inner = _bestCount - 3;   // (less the doors, the place beside the ambulance and the bed's place)
            HomeChecked = false;
            for (int attempt = 0; attempt < 2 && !HomeChecked; attempt++)
            {
                Square = attempt == 0;
                _count = 0;
                bool found = false;
                // After a swing-in whoever pushes back may stand at either end: the one from which the swing out is clear.
                for (int end = 0; end < (Swing ? 2 : 1) && !found; end++)
                {
                    HomeHeading = Swing ? (end == 0 ? Heading : -Heading) : -Heading;
                    _count = 0;
                    Add(Swing ? SwingFrom - Side * Reach : Bed + Heading * Reach);
                    for (int i = 0; i < inner; i++)
                    {
                        _x[_count] = _bestX[_bestCount - 2 - i];
                        _z[_count] = _bestZ[_bestCount - 2 - i];
                        _count++;
                    }
                    if (Square)
                    {
                        Add(RearFace + over * out1 + Back * DoorBack);
                        Add(RearFace + over * out1 + Back * TurnBack);
                        Add(RearFace + Back * TurnBack);
                        Add(HomeBed);
                    }
                    else
                    {
                        Add(RearFace + over * out1 + Back * PivotBack);
                        Add(RearFace + Back * PivotBack);
                    }
                    found = Valid(Tier, -Guide, false, default, default, default, default, home: true);
                }
                HomeChecked = found;
            }
            // (With neither clear, the last one built is used all the same and said so: the crew is never left at the casualty.)
            Home.Build(_x, _z, _count, Corner, Keep, -Guide);
            if (Swing)
            {
                Home.Pose(0f, out _, out _, out float startX, out float startZ, out float startHeadingX, out float startHeadingZ);
                Home.AddSwing(Bed.x, Bed.z, HomeHeading.x, HomeHeading.z, startX, startZ, startHeadingX, startHeadingZ);
            }
            if (!Square)
            {
                Home.Pose(Home.Travel, out _, out _, out float endX, out float endZ, out float endHeadingX, out float endHeadingZ);
                Home.AddSwing(endX, endZ, endHeadingX, endHeadingZ, HomeBed.x, HomeBed.z, -Back.x, -Back.z);
            }
            Level(Home);
            HomeGate = Gate(Home, entering: false);
        }

        private const int GateRoutes = 15;
        private const int Routes = 1 + GateRoutes + 12;

        // The places of one way from beside the ambulance to the last two: straight; or square through an end of the
        // tape, so many metres across it (in line with where it is going, at the opening already there, in line with
        // the ambulance, then further and further to either side); or by a place to one side of the middle. False
        // if there is no such way here (no tape between them, or the same as one already tried).
        private bool Route(int route, Vector3 beside, Vector3 turn, Vector3 last)
        {
            _count = 0;
            Add(Door);
            Add(beside);
            if (route >= 1 && route <= GateRoutes)
            {
                if (!_framed)
                {
                    return false;
                }
                float from = Vector3.Dot(beside - _scene, _forward);
                float to = Vector3.Dot(turn - _scene, _forward);
                bool near = from < _back - 0.5f && to > _back;
                bool far = from > _front + 0.5f && to < _front;
                if (!near && !far)
                {
                    return false;
                }
                float aim = Vector3.Dot(turn - _scene, _right);
                float low = _left + Width * 0.5f + TapeClear;
                float high = _rightEdge - Width * 0.5f - TapeClear;
                if (low > high)
                {
                    low = high = (_left + _rightEdge) * 0.5f;
                }
                int index = route - 1;
                float across = index == 0 ? aim
                    : index == 1 ? (_cordon.Up && near ? _cordon.DoorAcross : float.NaN)
                    : index == 2 ? Vector3.Dot(beside - _scene, _right)
                    : aim + ((index - 3) % 2 == 0 ? -0.75f : 0.75f) * ((index - 3) / 2 + 1);
                if (float.IsNaN(across) || across < low - 0.01f || across > high + 0.01f)
                {
                    if (index != 0 && index != 2)
                    {
                        return false;
                    }
                    across = Mathf.Clamp(across, low, high);
                }
                float line = near ? _back : _front;
                float inward = near ? 1f : -1f;
                Vector3 outside = _scene + _forward * (line - inward * 1.3f) + _right * across;
                Vector3 inside = _scene + _forward * (line + inward * 1.3f) + _right * across;
                Add(outside);
                if ((to - (line + inward * 1.3f)) * inward > 0.3f)
                {
                    Add(inside);
                }
            }
            else if (route > GateRoutes)
            {
                int index = route - GateRoutes - 1;
                Vector3 run = Flat(turn - beside);
                float length = run.magnitude;
                if (length < 3f)
                {
                    return false;
                }
                Vector3 aside = Vector3.Cross(Vector3.up, run / length);
                Add((beside + turn) * 0.5f + aside * ((index % 2 == 0 ? -0.75f : 0.75f) * (index / 2 + 2)));
            }
            Add(turn);
            Add(last);
            return true;
        }

        private void Add(Vector3 place)
        {
            if (_count < Places)
            {
                _x[_count] = place.x;
                _z[_count] = place.z;
                _count++;
            }
        }

        private float Length()
        {
            float length = 0f;
            for (int i = 1; i < _count; i++)
            {
                float x = _x[i] - _x[i - 1], z = _z[i] - _z[i - 1];
                length += Mathf.Sqrt(x * x + z * z);
            }
            return length;
        }

        // The places in _x and _z as a way, tested: against the casualty's own body, the tape, and pose by pose against
        // every vehicle and fixture (the ambulance itself only where the stretcher comes out of it or goes back in), on
        // the carriageway and out of any lane traffic is let past in (tier 0) or wherever there is ground (tier 1).
        private bool Valid(int tier, float guide, bool swing, Vector3 swingFrom, Vector3 swingHeading, Vector3 swingTo, Vector3 swingToHeading, bool home)
        {
            _tryOpen = false;
            if (!_try.Build(_x, _z, _count, Corner, Keep, guide))
            {
                _blocked = "no line through its places";
                return false;
            }
            float travel = _try.Travel;
            if (swing)
            {
                _try.AddSwing(swingFrom.x, swingFrom.z, swingHeading.x, swingHeading.z, swingTo.x, swingTo.z, swingToHeading.x, swingToHeading.z);
            }
            else if (home)
            {
                // The way back's own swings: out from beside the casualty, and (without room to turn) square at the doors.
                _try.Pose(0f, out _, out _, out float startX, out float startZ, out float startHeadingX, out float startHeadingZ);
                if (Swing)
                {
                    _try.AddSwing(Bed.x, Bed.z, HomeHeading.x, HomeHeading.z, startX, startZ, startHeadingX, startHeadingZ);
                }
                if (!Square)
                {
                    _try.Pose(travel, out _, out _, out float endX, out float endZ, out float endHeadingX, out float endHeadingZ);
                    _try.AddSwing(endX, endZ, endHeadingX, endHeadingZ, HomeBed.x, HomeBed.z, -Back.x, -Back.z);
                }
            }

            // Whoever lies there: on the way out nothing passes over their own line (on the way back they are on the bed).
            if (!home)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 part = Chest + Axis * (0.45f - i * 0.55f);
                    if (_try.Clearance(part.x, part.z) < BodyClear)
                    {
                        _blocked = "it would pass over the casualty";
                        return false;
                    }
                }
            }

            // The tape: crossed in one place a side at most, never run along; and the bed parked inside it.
            if (_framed)
            {
                float most = tier == 0 ? MostOpening : MostOpening * 1.8f;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = Edge(i);
                    Vector3 b = Edge(i + 1);
                    if (_try.Crossing(a.x, a.z, b.x, b.z, TapeClear, out float low, out float high) && (high - low) * Flat(b - a).magnitude > most)
                    {
                        _blocked = "it would run along the tape";
                        return false;
                    }
                }
                if (tier == 0 && !home && Inside(Chest, 0f) && !(Inside(Bed + Axis * CrewPath.BedHalfLength, 0.2f) && Inside(Bed - Axis * CrewPath.BedHalfLength, 0.2f)))
                {
                    _blocked = "the stretcher would stand across the tape";
                    return false;
                }
            }

            // Pose by pose: the strip from behind whoever pushes to ahead of whoever walks at the leading end.
            int mask = tier == 0 ? SceneParking.StreetMask : SceneParking.RoadsideMask;
            float half = Sweep + (tier == 0 ? 0.3f : 0.12f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / 0.5f));
            // (From the far end back: what is in the way is most often by the casualty, and a way is given up at the first thing found.)
            for (int i = steps; i >= 0; i--)
            {
                float along = travel * i / steps;
                _try.Pose(along, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out float headingX, out float headingZ);
                bool atDoors = home ? along > travel - 2.5f : along < 2.5f;
                if (!PoseClear(new Vector3(pusherX, Door.y, pusherZ), new Vector3(bedX, Door.y, bedZ), new Vector3(headingX, 0f, headingZ), half, mask, atDoors, tier, i % 2 == 0))
                {
                    return false;
                }
            }
            int swings = (swing ? 1 : 0) + (home && Swing ? 1 : 0) + (home && !Square ? 1 : 0);
            for (int s = 0; s < swings; s++)
            {
                bool first = swing || (home && Swing && s == 0);
                Vector3 from, fromHeading, to, toHeading;
                if (swing)
                {
                    from = swingFrom;
                    fromHeading = swingHeading;
                    to = swingTo;
                    toHeading = swingToHeading;
                }
                else if (first)
                {
                    _try.Pose(0f, out _, out _, out float x, out float z, out float headingX, out float headingZ);
                    from = Bed;
                    fromHeading = HomeHeading;
                    to = new Vector3(x, Bed.y, z);
                    toHeading = new Vector3(headingX, 0f, headingZ);
                }
                else
                {
                    _try.Pose(travel, out _, out _, out float x, out float z, out float headingX, out float headingZ);
                    from = new Vector3(x, Door.y, z);
                    fromHeading = new Vector3(headingX, 0f, headingZ);
                    to = HomeBed;
                    toHeading = -Back;
                }
                for (int i = 0; i <= CrewPath.SwingPoses; i += 2)
                {
                    CrewPath.Blend(from.x, from.z, fromHeading.x, fromHeading.z, to.x, to.z, toHeading.x, toHeading.z, (float)i / CrewPath.SwingPoses,
                        out float bedX, out float bedZ, out float headingX, out float headingZ);
                    var heading = new Vector3(headingX, 0f, headingZ);
                    var bed = new Vector3(bedX, from.y, bedZ);
                    if (!PoseClear(bed - heading * Reach, bed, heading, half, mask, home && !first, tier, false))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// What stands on the way back now, half a minute after it was planned: a vehicle or a fixture in the strip the
        /// crew sweeps over (the ambulance itself only counts away from its rear doors). Null if it is still clear.
        /// Asked once, when the crew is about to set off back.
        /// </summary>
        public Collider HomeBlocked()
        {
            if (!Planned || !Home.Built)
            {
                return null;
            }
            float travel = Home.Travel;
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / 0.6f));
            for (int i = 0; i <= steps; i++)
            {
                float along = travel * i / steps;
                Home.Pose(along, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out float headingX, out float headingZ);
                var heading = new Vector3(headingX, 0f, headingZ);
                var pusher = new Vector3(pusherX, Home.Height(along), pusherZ);
                var bed = new Vector3(bedX, pusher.y, bedZ);
                Collider hit = SceneParking.InStrip(pusher - heading * CrewPath.Body, bed + heading * (CrewPath.SecondAhead + CrewPath.Body), Sweep + 0.05f,
                    along > travel - 2.5f ? _ambulance : null, SceneParking.RoadsideMask);
                if (hit != null)
                {
                    return hit;
                }
            }
            return null;
        }

        // One pose of the crew: nothing solid in its strip, ground under it, and (tier 0) carriageway that is nobody's open lane.
        private bool PoseClear(Vector3 pusher, Vector3 bed, Vector3 heading, float half, int mask, bool atDoors, int tier, bool looked)
        {
            Collider hit = InStrip(pusher - heading * (CrewPath.Body + 0.05f), bed + heading * (CrewPath.SecondAhead + CrewPath.Body + 0.05f), half,
                atDoors ? _ambulance : null, mask);
            if (hit != null)
            {
                _blocked = Name(hit);
                return false;
            }
            if (!looked)
            {
                return true;
            }
            if (!HasGround(pusher) || !HasGround(bed))
            {
                _blocked = "no ground under it";
                return false;
            }
            bool open = _cordon != null && (_cordon.InOpenLane(bed, 0.2f) || _cordon.InOpenLane(pusher, 0.2f));
            if (tier > 0)
            {
                _tryOpen |= open;
                return true;
            }
            if (open)
            {
                _blocked = "the lane traffic is let past in";
                return false;
            }
            if (!OnRoad(pusher) || !OnRoad(bed))
            {
                _blocked = "a pavement or a kerb in the line";
                return false;
            }
            return true;
        }

        // The ground height along a way: asked every half metre, and in between as the road runs.
        private static void Level(CrewPath path)
        {
            int count = path.Samples;
            int last = 0;
            float lastHeight = 0f;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                if (i % 5 != 0 && i != count - 1)
                {
                    continue;
                }
                path.Sample(i, out float x, out float z);
                // (From a little above the last height found: the road may climb.)
                if (!StreetSpots.Ground(new Vector3(x, any ? lastHeight : StartHeight, z), out float height))
                {
                    height = any ? lastHeight : StartHeight;
                }
                if (!any)
                {
                    lastHeight = height;
                    any = true;
                }
                for (int j = last; j <= i; j++)
                {
                    path.SetHeight(j, i == last ? height : Mathf.Lerp(lastHeight, height, (float)(j - last) / (i - last)));
                }
                last = i;
                lastHeight = height;
            }
        }

        private static float StartHeight;   // the height the ground is first looked for from: the rear doors' (set by Measure)

        // How far the rear doors are from the tape, and what (if anything) stands between the ambulance's nose and it.
        private void Measure(Vector3 centre, Vector3 forward, float halfLength, float halfWidth)
        {
            StartHeight = centre.y;
            DoorsToTape = -1f;
            Between = null;
            if (!_framed)
            {
                return;
            }
            float rear = Vector3.Dot(RearFace - _scene, _forward);
            float line = rear < _back ? _back : rear > _front ? _front : float.NaN;
            if (float.IsNaN(line))
            {
                return;
            }
            DoorsToTape = Mathf.Abs(line - rear);
            Vector3 nose = centre + forward * (halfLength + 0.3f);
            float ahead = Mathf.Abs(line - Vector3.Dot(nose - _scene, _forward));
            if (ahead > 0.6f && Vector3.Dot(forward, _forward) * (line - rear) > 0f)
            {
                Collider hit = SceneParking.InStrip(nose, nose + forward * (ahead - 0.3f), halfWidth, _ambulance, SceneParking.RoadsideMask);
                Between = hit != null ? Name(hit) : null;
            }
        }

        // How far whoever pushes has walked when the bed's middle crosses the tape (into the taped area, or out of it); -1 if it does not.
        private float Gate(CrewPath path, bool entering)
        {
            if (!_framed)
            {
                return -1f;
            }
            float travel = path.Travel;
            bool was = false;
            for (float along = 0f; along <= travel; along += 0.25f)
            {
                path.Pose(along, out _, out _, out float x, out float z, out _, out _);
                bool inside = Inside(new Vector3(x, 0f, z), 0f);
                if (along > 0f && inside != was && inside == entering)
                {
                    return along;
                }
                was = inside;
            }
            return -1f;
        }

        // A corner of the taped rectangle, 0 to 3 round it (4 is 0 again).
        private Vector3 Edge(int index)
        {
            index &= 3;
            return _scene + _forward * (index == 0 || index == 3 ? _back : _front) + _right * (index < 2 ? _left : _rightEdge);
        }

        // True if a point is inside the taped rectangle by a margin.
        private bool Inside(Vector3 point, float margin)
        {
            float along = Vector3.Dot(point - _scene, _forward);
            float across = Vector3.Dot(point - _scene, _right);
            return along > _back + margin && along < _front - margin && across > _left + margin && across < _rightEdge - margin;
        }

        /// <summary>
        /// For someone walking from one place beside the casualty to another: true, with the place to go by, if the
        /// straight line would take them over the body. They go round the head or the feet instead, whichever is shorter.
        /// </summary>
        public bool Round(Vector3 from, Vector3 to, out Vector3 via)
        {
            via = default;
            if (!Laid)
            {
                return false;
            }
            float fromSide = Vector3.Dot(from - Chest, Side);
            float toSide = Vector3.Dot(to - Chest, Side);
            if (fromSide * toSide >= 0f)
            {
                return false;   // both on the same side of the body
            }
            float fromAlong = Vector3.Dot(from - Chest, Axis);
            float toAlong = Vector3.Dot(to - Chest, Axis);
            float crosses = Mathf.Lerp(fromAlong, toAlong, fromSide / (fromSide - toSide));
            if (crosses > HeadRound - 0.2f || crosses < -FeetRound + 0.2f)
            {
                return false;   // the line already passes beyond the head or the feet
            }
            Vector3 head = Chest + Axis * HeadRound;
            Vector3 feet = Chest - Axis * FeetRound;
            via = Flat(head - from).magnitude + Flat(to - head).magnitude <= Flat(feet - from).magnitude + Flat(to - feet).magnitude ? head : feet;
            via.y = from.y;
            return true;
        }

        /// <summary>
        /// For someone walking from beside the stretcher (as it stands) to somewhere the straight line to which runs
        /// through it: true, with the place to go by first, a step clear of whichever of its corners makes the
        /// shortest way round.
        /// </summary>
        public static bool RoundBed(Transform bed, Vector3 from, Vector3 to, out Vector3 via)
        {
            via = default;
            Vector3 a = bed.InverseTransformPoint(from);
            Vector3 b = bed.InverseTransformPoint(to);
            float halfWidth = CrewPath.BedHalfWidth + 0.25f;     // the rail and half of whoever walks past it
            float halfLength = CrewPath.BedHalfLength + 0.25f;
            bool through = false;
            for (int i = 1; i < 12 && !through; i++)
            {
                Vector3 at = Vector3.Lerp(a, b, i / 12f);
                through = Mathf.Abs(at.x) < halfWidth && Mathf.Abs(at.z) < halfLength;
            }
            if (!through)
            {
                return false;
            }
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3((i < 2 ? -1f : 1f) * (halfWidth + 0.2f), 0f, (i % 2 == 0 ? -1f : 1f) * (halfLength + 0.2f));
                float length = Flat(corner - a).magnitude + Flat(b - corner).magnitude;
                if (length < best)
                {
                    best = length;
                    via = corner;
                }
            }
            via = bed.TransformPoint(via);
            via.y = from.y;
            return true;
        }

        /// <summary>
        /// Moves a place to kneel out of the stretcher as it actually stands (wherever it was left), with the
        /// clearance every place keeps. True if it had to be moved.
        /// </summary>
        public static bool OffTheBed(Transform bed, ref Vector3 place)
        {
            Vector3 local = bed.InverseTransformPoint(place);
            float halfWidth = BedHalfWidth + Gap + 0.3f;                 // the rail, the clearance, and half of whoever kneels
            float halfLength = Stretcher.HalfLength + 0.11f + Gap + 0.3f;   // (the push bars reach a hand's breadth beyond the bed)
            float inWidth = halfWidth - Mathf.Abs(local.x);
            float inLength = halfLength - Mathf.Abs(local.z);
            if (inWidth <= 0f || inLength <= 0f)
            {
                return false;
            }
            if (inWidth <= inLength)
            {
                local.x = local.x >= 0f ? halfWidth : -halfWidth;
            }
            else
            {
                local.z = local.z >= 0f ? halfLength : -halfLength;
            }
            float height = place.y;
            place = bed.TransformPoint(local);
            place.y = height;
            return true;
        }

        // What stands where someone would kneel: anything solid within their own half width and the clearance.
        private static Collider In(Vector3 place)
        {
            if (StreetSpots.Ground(place, out float ground))
            {
                place.y = ground;
            }
            int count = Physics.OverlapSphereNonAlloc(place + Vector3.up * 0.9f, 0.3f + Gap, Hits, SceneParking.StreetMask, QueryTriggerInteraction.Ignore);
            Collider found = count > 0 ? Hits[0] : null;
            for (int i = 0; i < count; i++)
            {
                Hits[i] = null;
            }
            return found;
        }

        private static readonly Collider[] Hits = new Collider[4];

        // True on the carriageway: street under the point, and not where only the game's pedestrians walk (a
        // pavement). A crossing is carriageway: their mesh runs over it, but so does a lane of the road.
        private static bool OnCarriage(Vector3 point)
        {
            if (!StreetSpots.Ground(point, out float ground))
            {
                return false;
            }
            point.y = ground;
            return !NavMesh.SamplePosition(point, out _, 0.3f, StreetSpots.Pavement) || InLane(point);
        }

        private const float LaneHalf = 1.9f;   // metres from a lane's middle line that are that lane
        private const float LaneReach = 6f;    // ...and from a place that a lane's nearest point is looked for

        // True within a lane's width of the road graph: measured across the lane from its nearest point's own line,
        // so that a pavement beside a kerb lane (2 m and more from that line) is not taken for the road.
        private static bool InLane(Vector3 point)
        {
            return TrafficManager.HasInstance && InLane(point, TrafficManager.Instance.GetClosestWaypoint(point, LaneReach));
        }

        private static bool InLane(Vector3 point, Waypoint nearest)
        {
            if (nearest == null || !TrafficManager.HasInstance)
            {
                return false;
            }
            TrafficManager traffic = TrafficManager.Instance;
            Waypoint other = nearest.neighbors != null && nearest.neighbors.Count > 0 ? traffic.GetWaypoint(nearest.neighbors[0])
                : nearest.prev != null && nearest.prev.Count > 0 ? traffic.GetWaypoint(nearest.prev[0]) : null;
            Vector3 along = other != null ? Flat(other.position - nearest.position) : Vector3.zero;
            Vector3 offset = Flat(point - nearest.position);
            if (along.sqrMagnitude < 0.01f)
            {
                return offset.sqrMagnitude < LaneHalf * LaneHalf;
            }
            return Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, along.normalized))) < LaneHalf;
        }

        private static string Name(Collider hit)
        {
            return "'" + hit.name + "' on layer " + LayerMask.LayerToName(hit.gameObject.layer);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
