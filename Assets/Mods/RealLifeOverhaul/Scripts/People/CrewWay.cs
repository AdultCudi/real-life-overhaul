using System.Globalization;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The ambulance crew's one way from the rear doors to the casualty and back, and the layout at the casualty: worked out
    /// once when the ambulance stops, as a line with rounded corners tested pose by pose against every vehicle and fixture,
    /// the tape and the casualty. The stretcher, the crew and the cordon all follow it (CrewPath, Stretcher.Wheel,
    /// SceneCordon.SetCorridor). Nothing runs by itself.
    /// </summary>
    public sealed partial class CrewWay
    {
        public const float Width = 2.3f;            // metres the crew takes up across its way: the bed and whoever walks beside it, and a little more
        public const float BedOut = 0.85f;          // metres from the body's axis to the stretcher's middle line: 0.55 m to its near rail
        public const float DoorGap = 0.25f;         // metres from the rear doors to the loaded stretcher's leading end
        private const float BedHalfWidth = 0.3f;    // metres
        private const float Gap = 0.3f;             // metres every place keeps from anything solid, and from the stretcher
        private const float BedBack = 0.35f;        // the stretcher's middle is this far towards the feet from the chest: hips on its middle, head on the pillow
        private const float Reach = CrewPath.Lead;  // from the stretcher's middle to whoever pushes it
        private const float Sweep = 0.85f;          // from the middle line of the way to the outside of whoever walks beside the bed's leading end
        private const float Corner = 1.2f;          // metres: the radius the corners of the way are rounded to
        private const float Keep = CrewPath.Lead + 0.05f;   // of the last stretch, kept straight: the bed ends pointing exactly along it
        private const float Beside = Sweep + 0.4f;  // from the ambulance's side to the middle line of the way
        private const float DoorBack = 1.3f;        // metres behind the rear doors, where the crew steps out
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

        private readonly float[] _x = new float[Places];
        private readonly float[] _z = new float[Places];
        private int _count;
        private readonly float[] _bestX = new float[Places];
        private readonly float[] _bestZ = new float[Places];
        private int _bestCount;
        private readonly CrewPath _try = new CrewPath();
        private string _blocked;                    // what stood in the last way that was refused, for Why
        private bool _tryOpen;                      // the way being tried runs in a lane traffic is let past in

        public readonly CrewPath Out = new CrewPath();

        public readonly CrewPath Home = new CrewPath();

        public bool Laid { get; private set; }

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
        /// The layout at the casualty from how they lie: tried on the preferred side (away from <paramref name="vehicle"/>
        /// when <paramref name="beside"/>, else the side help comes from), then the other; if neither is clear the preferred
        /// side stays and <see cref="Failed"/> says what was in the way. <paramref name="kneelOut"/> and
        /// <paramref name="kneelAlong"/> place whoever treats: metres out from the chest, and towards the head.
        /// </summary>
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

        // The layout on one side of the body: null if the stretcher's and the treating paramedic's places are clear, else what is in them.
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

        // The way the bed points when parked and what follows: the end it is pushed from, where the pusher stands, and
        // where the second goes (the hips when pushed from the feet, else the head; the other one if that is blocked).
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
        /// The way from the rear doors to the stretcher's place and back, after <see cref="Lay"/>; <paramref name="cordon"/>
        /// may be null. False, with <see cref="Why"/>, if there is none: the stretcher then stays in the ambulance.
        /// </summary>
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
            Look();   // the street round the scene, read once (Survey)

            Vector3 firstSide = Side;
            float best = float.MaxValue;
            Vector3 bestSide = firstSide, bestHeading = _preferred, bestFrom = default;
            float bestAway = 1f;
            bool bestSwing = false, bestOpen = false;
            _blocked = null;
            for (int tier = 0; tier < 2 && best == float.MaxValue; tier++)
            {
                _askedNow = 0;   // each tier has its own share of engine queries
                for (int body = 0; body < 2; body++)
                {
                    // On the carriageway a side of the body that failed its own clear test is not tried.
                    if (Try(body == 0 ? firstSide : -firstSide) != null && tier == 0)
                    {
                        continue;
                    }
                    for (int move = 0; move < RunIns.Length * 2 + 2; move++)
                    {
                        bool swing = move >= RunIns.Length * 2;
                        Vector3 heading = move % 2 == 0 ? _preferred : -_preferred;
                        // The last places: the corner onto the body's line and the bed's place; or, swung in, where it is swung from.
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
                                    break;   // straight, by the tape's end, then aside: the first clear route wins for this side and move
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
                Why = "no way " + Width.ToString("F1", CultureInfo.InvariantCulture) + " m wide is clear from either side of the ambulance to either side of the casualty, by a run-in or a swing-in"
                      + (_blocked != null ? " (the last one tried: " + _blocked + ")" : "");
                return false;
            }

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
            _askedNow = 0;   // ...and so has the way back
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
            int inner = _bestCount - 3;   // less the doors, the place beside the ambulance and the bed's place
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
            // With neither clear the last one built is used and said so: the crew is never left at the casualty.
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

        // The places of one way from beside the ambulance to the last two: straight; square through an end of the tape at
        // some point across it (in line with where it goes, at the opening, in line with the ambulance, then further to
        // either side); or by a place to one side. False if there is no such way (no tape between, or one already tried).
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

        /// <summary>True, with a place to go by round the head or the feet, when walking straight between two places would cross the body.</summary>
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

        /// <summary>True, with a place clear of the nearest corner, when the straight line from beside the stretcher runs through it.</summary>
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

        /// <summary>Moves a place to kneel out of the stretcher as it stands, keeping the usual clearance; true if it moved.</summary>
        public static bool OffTheBed(Transform bed, ref Vector3 place)
        {
            Vector3 local = bed.InverseTransformPoint(place);
            float halfWidth = BedHalfWidth + Gap + 0.3f;                 // the rail, the clearance, and half of whoever kneels
            float halfLength = Stretcher.HalfLength + 0.11f + Gap + 0.3f;   // the push bars reach a hand's breadth beyond the bed
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

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }
    }
}
