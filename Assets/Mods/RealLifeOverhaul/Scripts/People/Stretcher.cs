using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The ambulance crew's wheeled stretcher. There is one, built once (the modelled gurney with its
    /// push bars, a U at each end whose grips are where the pushing clip holds its hands; plain shapes
    /// and no bars where the prop bundle cannot be had)
    /// and kept switched off under the actor pool until a crew takes it out.
    /// It is wheeled along a line worked out beforehand (CrewPath, from CrewWay), and it is the
    /// stretcher that moves its crew, not the other way about: every frame it puts whoever pushes on
    /// that line, itself an arm's length ahead of them pointing from them to its own place on the
    /// line, and the second of the crew beside its leading end, all on the ground height the line was
    /// given. Nobody's navigation agent finds a way or moves anybody (it is only told the speed, for
    /// the walk to play to), so nothing on the navigation mesh can turn them aside or hold them up,
    /// and the stretcher ends exactly where the line ends. Where the line cannot bring it round in the
    /// room there is, it is swung: turned and moved from one pose to another with the crew at its ends
    /// (laid alongside someone from square on to them; brought square to the rear doors).
    /// Whoever pushes has both hands on that end's rail, in the pushing clip of the mod's own at the
    /// pace that clip was made for (the game's walk under the same hands if the clip cannot be had);
    /// the second has the near hand on the leading rail. The medic bag rides on the bed meanwhile:
    /// nobody at the stretcher has anything else in a hand. It also walks its crew the few steps
    /// between the places they work at beside it (Walk), the same way.
    /// Someone lying in the road can be put on it: the real body, straightened out on its back
    /// along the mattress with its head at the head end (SupinePose) and a blanket over it; or
    /// it can carry a covered form when the person is beyond help.
    /// The only per-frame work is placing it (and its passenger, and its crew) while it is
    /// being wheeled or somebody is being walked, and for the moment a lift lasts the passenger's
    /// joints. Parked or put away, nothing runs.
    /// </summary>
    public sealed class Stretcher : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("people", "Stretcher", typeof(Stretcher));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public const float HalfLength = 1f;
        public const float HandleReach = 0.55f;     // metres from an end of the bed to whoever holds it
        private const float BedHeight = 0.7f;       // top of the mattress above the road
        private const float HipsAbove = 0.10f;      // metres from the mattress to the hip joints of someone lying on their back
        private const float HipsAlong = -0.03f;     // ...and from the middle of the bed towards its head: head on the pillow, heels on the mattress
        private const float LiftTime = 0.7f;        // seconds a body takes from the road to lying on the bed, when nobody plays the lift

        private const float TakeWithin = 1f;        // metres from the handle within which someone is at that end of it
        private const float SlipRate = 1.2f;        // metres per second the difference between where someone stands and where the line has them is walked out at
        private const float RailHeight = 0.86f;     // where the hands go: the top rail at each end
        private const float RailApart = 0.22f;      // half the distance between the two hands on it
        private const float BarHeight = 0.95f;      // with the modelled push bars fitted (stretcher_push_bar): the cross bar at each end, which is what push_cart's hands are made to...
        private const float BarApart = 0.225f;      // ...its two rubber grips either side of the middle line...
        private const float BarAlong = 1.11f;       // ...and its distance from the middle of the bed: HalfLength + HandleReach less the 0.44 m the clip holds a rail ahead of the feet
        private const float LiftBack = 0.10f;       // further out than the handle that the crew kneel to lift when the bars are fitted: the clip folds the head 0.5 m forward at bar height

        private const float GuideAhead = CrewPath.SecondAhead - HalfLength;   // the second of a pair walks this far beyond the leading end of the bed...
        private const float GuideOut = CrewPath.SecondOut;                    // ...and this far out from its middle line: beside the corner, clear of the bed
        private const float GuideFastest = 2.4f;    // m/s they come up to their place at, at most
        private const float GuideTurn = 360f;       // degrees per second anybody walked is turned to face the way they go
        private const float GuideInPlace = 0.45f;   // metres from their place within which the hand is on the rail
        private const float GuideFacesAhead = 1.2f; // ...and within which they face the way it is going, whichever way they step

        private const float Quicken = 2.5f;         // m/s2 it is brought up to its pace at
        private const float SwingTurn = 30f;        // degrees per second it is swung round at...
        private const float SwingPace = 0.6f;       // ...and metres per second its middle is moved at meanwhile
        private const float There = 0.03f;          // metres (and 1.5 degrees) from a pose within which it is at it

        private const float BagAlong = -0.76f;      // where the medic bag rides: across the foot of the mattress...
        private const float BagOnLegs = 0.27f;      // ...and this much higher with somebody under the blanket or the sheet there

        private enum Mode { Parked, Wheeled }

        private readonly Transform[] _arms = new Transform[12];   // upper arm, forearm, hand: pusher's left, second's left, pusher's right, second's right

        private static Stretcher _shared;
        private static bool _failed;   // it could not be built, or faulted once: no stretcher this session
        private static bool _modelled; // built when the prop library was ready: if not, it is built again once it is

        private Mode _mode = Mode.Parked;
        private ActorRole _lead;               // at the handle: it goes ahead of them
        private ActorRole _rear;               // the second of a pair, walked at its leading end
        private Transform _leadBody;
        private Transform _rearBody;
        private Vector3 _heading = Vector3.forward;   // the way it is being pushed
        private float _handle = 1f;            // 1: the handle is its foot end and it travels head first; -1: the other way about
        private float _guideSide = 1f;         // 1: the second walks to the right of the way it is going; -1: to the left
        private bool _guideThere;              // ...and is in place, a hand on the rail
        private bool _paced;                   // whoever pushes it walks in the pushing clip, at that clip's pace
        private bool _rearPaced;               // the second walks in a clip of the mod's own (only where the game's walk cannot be had)
        private bool _barred;                  // the modelled push bars are on it: hands go to their grips

        // The line it is being wheeled along, and how far whoever pushes has come along it.
        private CrewPath _path;
        private float _along;
        private float _speed;
        private Vector3 _centre;               // where its middle is...
        private Vector3 _pusher;               // ...and whoever pushes (before what is left of their slip)
        private Vector3 _pusherWas;
        private Vector3 _leadSlip;             // how far whoever took it up still is from where the line has them
        private Vector3 _rearAt;               // where the second is
        private bool _hasFinal;                // after the line: swung to this pose
        private Vector3 _finalCentre;
        private Vector3 _finalHeading;
        private bool _swinging;                // being swung from one pose to another...
        private bool _swingFirst;              // ...before the line (else after it)
        private Vector3 _swingFrom, _swingFromHeading, _swingTo, _swingToHeading;
        private float _swingTime;
        private float _swung;                  // 0 to 1

        private GameObject _sheet;
        private GameObject _blanket;
        private readonly SupinePose _pose = new SupinePose();
        private bool _supine;                  // the passenger was laid out by the pose; else it is carried as it lay
        private float _lift = 1f;              // how far the lift has got, 0 to 1
        private float _liftTime = LiftTime;    // how long this lift takes: the length of the lifting clip when the crew plays it
        private bool _lifting;                 // the crew is lifting the passenger on while it stands
        private ICasualty _patient;
        private Transform _patientBody;
        private Vector3 _patientPosition;      // the body's root, in the stretcher's own frame
        private Quaternion _patientRotation;

        /// <summary>True while a crew has it out.</summary>
        public bool Out => gameObject.activeSelf;

        /// <summary>True while someone is lying on it.</summary>
        public bool Laden => _patient != null;

        /// <summary>True while whoever pushes it walks in the pushing clip, held to that clip's pace (SceneClips.WalkPace).</summary>
        public bool Paced => _paced && _mode == Mode.Wheeled;

        /// <summary>Where the person at the head end stands to take hold of it.</summary>
        public Vector3 HeadEnd => transform.position + transform.forward * (HalfLength + HandleReach);

        /// <summary>Where the person at the foot end stands.</summary>
        public Vector3 FootEnd => transform.position - transform.forward * (HalfLength + HandleReach);

        /// <summary>Where whoever is to push it a given way stands: at the end that trails, so it need not be turned round.</summary>
        public Vector3 HandleFor(Vector3 heading)
        {
            return End(Vector3.Dot(heading, transform.forward) >= 0f ? -1f : 1f);
        }

        /// <summary>...and where the other of a pair stands meanwhile: at the end that leads.</summary>
        public Vector3 LeadingFor(Vector3 heading)
        {
            return End(Vector3.Dot(heading, transform.forward) >= 0f ? 1f : -1f);
        }

        // Where someone stands at an end (1: the head end) before it is taken up: at the handle, or a hand's breadth further out
        // when the push bars are fitted, so that whoever kneels there to lift does not put their head through the bar. The
        // difference is walked out as it is taken up (_leadSlip).
        private Vector3 End(float end)
        {
            return transform.position + transform.forward * (end * (HalfLength + HandleReach + (_barred ? LiftBack : 0f)));
        }

        /// <summary>The stretcher, or null if there is none to be had. Built the first time it is wanted.</summary>
        public static Stretcher Take()
        {
            if (_shared != null && !_modelled && PropLibrary.Ready && !_shared.Out)
            {
                // Built from plain shapes before the modelled props had loaded: once, built again with them.
                Destroy(_shared.gameObject);
                _shared = null;
            }
            if (_shared != null)
            {
                return _shared;
            }
            ActorPool pool = ActorPool.Instance;
            if (_failed || pool == null || !Shapes.Available)
            {
                return null;
            }
            try
            {
                var root = new GameObject("RealLifeOverhaul Stretcher");
                root.transform.SetParent(pool.transform, false);
                root.SetActive(false);
                _shared = root.AddComponent<Stretcher>();
                _shared.Build();
                _shared.enabled = false;
            }
            catch (Exception e)
            {
                Fail(e);
            }
            return _shared;
        }

        private void OnDestroy()
        {
            if (_shared == this)
            {
                _shared = null;
            }
        }

        // Frame, mattress, pillow, rails, legs and wheels; and the sheet, shown only over someone covered.
        private void Build()
        {
            Transform root = transform;
            Material frame = Shapes.Paint(new Color(0.55f, 0.57f, 0.60f));
            Material mattress = Shapes.Paint(new Color(0.10f, 0.13f, 0.22f));
            Material rail = Shapes.Paint(new Color(0.95f, 0.75f, 0.05f), 2f);
            Material tyre = Shapes.Paint(new Color(0.04f, 0.04f, 0.04f));
            Material linen = Shapes.Paint(new Color(0.90f, 0.90f, 0.88f));
            Mesh cube = Shapes.Cube;

            float length = HalfLength * 2f;
            _modelled = PropLibrary.Ready;
            _sheet = new GameObject("Sheet");
            _sheet.transform.SetParent(root, false);
            _sheet.SetActive(false);
            // Over someone who is carried as they are, feet to chest: the modelled blanket, or a plain one.
            _blanket = new GameObject("Blanket");
            _blanket.transform.SetParent(root, false);
            _blanket.SetActive(false);
            if (!Shapes.Prop("stretcher_blanket", _blanket.transform))
            {
                Shapes.Piece("Cover", _blanket.transform, cube, Shapes.Paint(new Color(0.16f, 0.22f, 0.40f)), new Vector3(0f, BedHeight + 0.2f, -0.32f), new Vector3(0.5f, 0.12f, 1.3f));
            }
            if (Shapes.Prop("stretcher", root))
            {
                // The modelled gurney; its draped form too if there is one, else the plain one below.
                // And its push bars (a U at each end, modelled to the pushing clip's hands), if the bundle has them: without, none.
                var bars = new GameObject("PushBars");
                bars.transform.SetParent(root, false);
                _barred = Shapes.Prop("stretcher_push_bar", bars.transform);
                if (!_barred)
                {
                    Destroy(bars);
                }
                if (!Shapes.Prop("stretcher_sheet", _sheet.transform))
                {
                    BuildSheet(cube, linen);
                }
                return;
            }
            Shapes.Piece("Frame", root, cube, frame, new Vector3(0f, BedHeight - 0.11f, 0f), new Vector3(0.56f, 0.04f, length));
            Shapes.Piece("Mattress", root, cube, mattress, new Vector3(0f, BedHeight - 0.045f, 0f), new Vector3(0.52f, 0.09f, length - 0.08f));
            Shapes.Piece("Pillow", root, cube, linen, new Vector3(0f, BedHeight + 0.03f, HalfLength - 0.2f), new Vector3(0.38f, 0.06f, 0.26f));
            for (int side = -1; side <= 1; side += 2)
            {
                Shapes.Piece("Rail", root, cube, rail, new Vector3(side * 0.29f, BedHeight + 0.02f, 0f), new Vector3(0.03f, 0.03f, length * 0.7f));
                for (int end = -1; end <= 1; end += 2)
                {
                    var corner = new Vector3(side * 0.24f, 0f, end * (HalfLength - 0.25f));
                    Shapes.Piece("Leg", root, cube, frame, corner + Vector3.up * (BedHeight * 0.5f - 0.02f), new Vector3(0.035f, BedHeight - 0.2f, 0.035f));
                    Transform wheel = Shapes.Piece("Wheel", root, Shapes.Drum, tyre, corner + new Vector3(-0.025f, 0.07f, 0f), new Vector3(0.14f, 0.05f, 0.14f));
                    wheel.localRotation = Quaternion.Euler(0f, 0f, -90f);
                }
            }

            BuildSheet(cube, linen);
        }

        // Somebody under a sheet: shoulders, trunk and legs, no more than that.
        private void BuildSheet(Mesh cube, Material linen)
        {
            Shapes.Piece("Trunk", _sheet.transform, cube, linen, new Vector3(0f, BedHeight + 0.11f, 0.3f), new Vector3(0.46f, 0.22f, 0.75f));
            Shapes.Piece("Legs", _sheet.transform, cube, linen, new Vector3(0f, BedHeight + 0.08f, -0.42f), new Vector3(0.40f, 0.16f, 0.85f));
            Shapes.Piece("Head", _sheet.transform, cube, linen, new Vector3(0f, BedHeight + 0.12f, 0.8f), new Vector3(0.24f, 0.22f, 0.26f));
        }

        /// <summary>
        /// Wheeled, from now on, along a line: whoever pushes is put on it and walked along it, the stretcher ahead
        /// of them, the second (if there is one) beside its leading end; none of them goes anywhere else until it is
        /// at the end of the line (Arrived) or parked. Coming out of the ambulance it appears at the start of the
        /// line with its crew in place. Already out, it is taken up where it stands by whichever end whoever pushes
        /// is at; if it stands some other way than the line begins, it is swung round to that first.
        /// False (and it is parked) only if there is nobody to push it or no line.
        /// </summary>
        /// <param name="guideSide">1 or -1: the second walks to the right or the left of the way it goes.</param>
        /// <param name="handle">Coming out of the ambulance: 1 to be pushed from its foot end (head first), -1 from its head end.</param>
        /// <param name="hasFinal">After the line it is swung to a last pose: its middle at finalCentre, pointing along finalHeading.</param>
        public bool Wheel(ActorRole by, ActorRole second, CrewPath path, float guideSide, float handle = 1f, bool hasFinal = false,
            Vector3 finalCentre = default, Vector3 finalHeading = default)
        {
            ThirdPersonCharacter first = by != null ? by.Character : null;
            if (_failed || first == null || path == null || !path.Built)
            {
                Park();
                return false;
            }
            LetGo();   // whoever had it before (a lift is not cut short: it goes on as they set off)
            if (!by.Convey())
            {
                Park();
                return false;
            }
            bool wasOut = Out;
            _lead = by;
            _leadBody = first.transform;
            _path = path;
            _along = 0f;
            _speed = 0f;
            _guideSide = guideSide < 0f ? -1f : 1f;
            _hasFinal = hasFinal;
            _finalCentre = finalCentre;
            _finalHeading = Flat(finalHeading, Vector3.forward);
            _mode = Mode.Wheeled;
            _lifting = false;
            _swinging = false;
            Arrived = false;
            PathPose(0f, out Vector3 pusher, out Vector3 centre, out Vector3 heading);
            if (!wasOut)
            {
                // Out of the ambulance: there, pointing the way the line begins, its crew at their places.
                _handle = handle < 0f ? -1f : 1f;
                _centre = centre;
                _heading = heading;
                _pusher = pusher;
                _leadSlip = Vector3.zero;
                gameObject.SetActive(true);
            }
            else
            {
                // Taken up where it stands, from whichever end they are at; what they are off that handle by is walked out.
                Vector3 here = transform.position;
                Vector3 lies = Flat(transform.forward, Vector3.forward);
                _handle = Vector3.Dot(Flat(here - _leadBody.position), lies) >= 0f ? 1f : -1f;
                _centre = here;
                _heading = lies * _handle;
                _pusher = here - _heading * CrewPath.Lead;
                _leadSlip = Flat(_leadBody.position - _pusher);
                if (!At(centre, heading))
                {
                    BeginSwing(here, _heading, centre, heading, first: true);
                }
            }
            _pusherWas = _pusher + _leadSlip;
            Arms(first.animator, 0);
            Arms(null, 3);

            // The second of a pair: walked from here on at the leading end, on the side asked for.
            ThirdPersonCharacter other = second != null && second != by ? second.Character : null;
            if (other != null && second.Convey())
            {
                _rear = second;
                _rearBody = other.transform;
                _rearAt = wasOut ? _rearBody.position : SecondPlace();
                if (!wasOut)
                {
                    _rearBody.rotation = Quaternion.LookRotation(_heading, Vector3.up);
                }
                Arms(other.animator, 3);
                Stow(second);
                // (Their own walk is the game's, played to the speed they are moved at; where it cannot be had, a walking clip of the mod's own.)
                _rearPaced = !second.WalksUnaided && second.Gait(SceneClips.PullCart, SceneClips.WalkPace);
            }
            Stow(by);
            _paced = by.Gait(SceneClips.PushCart, SceneClips.WalkPace);
            enabled = true;
            Step(0f);
            ActorPool.Logger?.Info("People: stretcher pushed from its " + (_handle > 0f ? "foot" : "head") + " end, both hands on that rail"
                                   + (_rear != null ? "; the second of the crew is walked beside its leading end, the near hand on that rail" : "; nobody at its other end")
                                   + (_barred ? "; hands on the push bar's grips" : "; no push bar (hands on the end rail)")
                                   + (_paced ? "; pushing clip, " + SceneClips.WalkPace.ToString("F1") + " m/s" : "; the game's walk (no pushing clip to be had)")
                                   + $"; wheeled along its line, {path.Travel:F1} m" + (_swinging ? ", swung round to it first" : "") + (_hasFinal ? ", swung into place at the end." : "."));
            return true;
        }

        /// <summary>True once it is at the end of the line it was given (and swung into its last pose, if it had one).</summary>
        public bool Arrived { get; private set; }

        /// <summary>Metres whoever pushes has come along the line.</summary>
        public float Progress => _along;

        /// <summary>The middle of the end that leads, the way it was last going.</summary>
        public Vector3 LeadingEnd => transform.position + _heading * HalfLength;

        /// <summary>The way it was last going, along the ground.</summary>
        public Vector3 Heading => _heading;

        /// <summary>True while somebody has it in their hands.</summary>
        public bool Held => _mode != Mode.Parked;

        /// <summary>
        /// The rest of the way at once: at the end of its line, in its last pose, its crew in place. Only for
        /// when nobody can see it (whoever owns the crew decides that).
        /// </summary>
        public void Hurry()
        {
            if (_mode != Mode.Wheeled || _path == null || Arrived)
            {
                return;
            }
            _along = _path.Travel;
            _speed = 0f;
            _swinging = false;
            _leadSlip = Vector3.zero;
            PathPose(_along, out _pusher, out _centre, out _heading);
            if (_hasFinal)
            {
                _centre = new Vector3(_finalCentre.x, _centre.y, _finalCentre.z);
                _heading = _finalHeading;
                _pusher = _centre - _heading * CrewPath.Lead;
                _hasFinal = false;
            }
            _pusherWas = _pusher;
            if (_rear != null)
            {
                _rearAt = SecondPlace();
            }
            Step(0f);
        }

        // Where the line has the crew when whoever pushes has come so far along it, on the ground height it was given.
        private void PathPose(float along, out Vector3 pusher, out Vector3 centre, out Vector3 heading)
        {
            _path.Pose(along, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out float headingX, out float headingZ);
            pusher = new Vector3(pusherX, _path.Height(along), pusherZ);
            centre = new Vector3(bedX, _path.Height(along + CrewPath.Lead), bedZ);
            heading = new Vector3(headingX, 0f, headingZ);
        }

        private Vector3 SecondPlace()
        {
            Vector3 right = Vector3.Cross(Vector3.up, _heading);
            return _centre + _heading * (HalfLength + GuideAhead) + right * (_guideSide * GuideOut);
        }

        // True if it is at a pose: its middle there, pointing that way.
        private bool At(Vector3 centre, Vector3 heading)
        {
            return Flat(centre - _centre).sqrMagnitude <= There * There && Vector3.Angle(_heading, heading) <= 1.5f;
        }

        private void BeginSwing(Vector3 from, Vector3 fromHeading, Vector3 to, Vector3 toHeading, bool first)
        {
            _swinging = true;
            _swingFirst = first;
            _swingFrom = from;
            _swingFromHeading = fromHeading;
            _swingTo = to;
            _swingToHeading = toHeading;
            _swung = 0f;
            _speed = 0f;
            _swingTime = Mathf.Max(0.4f, Mathf.Max(Vector3.Angle(fromHeading, toHeading) / SwingTurn, Flat(to - from).magnitude / SwingPace));
        }

        // One frame of being wheeled: along the line (or round a swing), and everybody put where that has them.
        private void Step(float dt)
        {
            float travel = _path.Travel;
            if (_swinging)
            {
                _swung = Mathf.Min(1f, _swung + dt / _swingTime);
                float eased = _swung * _swung * (3f - 2f * _swung);
                CrewPath.Blend(_swingFrom.x, _swingFrom.z, _swingFromHeading.x, _swingFromHeading.z, _swingTo.x, _swingTo.z, _swingToHeading.x, _swingToHeading.z, eased,
                    out float x, out float z, out float headingX, out float headingZ);
                _centre = new Vector3(x, Mathf.Lerp(_swingFrom.y, _swingTo.y, eased), z);
                _heading = new Vector3(headingX, 0f, headingZ);
                _pusher = _centre - _heading * CrewPath.Lead;
                if (_swung >= 1f)
                {
                    _swinging = false;
                    if (!_swingFirst)
                    {
                        _hasFinal = false;   // in its last pose
                    }
                }
            }
            else if (_along < travel)
            {
                // At the pace of the walking clip, eased onto the end of the line.
                float pace = Mathf.Min(SceneClips.WalkPace, Mathf.Max(0.25f, (travel - _along) * 2.5f));
                _speed = Mathf.MoveTowards(_speed, pace, Quicken * dt);
                _along = Mathf.Min(travel, _along + _speed * dt);
                if (travel - _along < 0.001f)
                {
                    _along = travel;
                    _speed = 0f;
                }
                PathPose(_along, out _pusher, out _centre, out _heading);
            }
            if (!_swinging && _along >= travel && _hasFinal)
            {
                var last = new Vector3(_finalCentre.x, _centre.y, _finalCentre.z);
                if (At(last, _finalHeading))
                {
                    _hasFinal = false;
                }
                else
                {
                    BeginSwing(_centre, _heading, last, _finalHeading, first: false);
                }
            }
            _leadSlip = Vector3.MoveTowards(_leadSlip, Vector3.zero, SlipRate * dt);
            Arrived = !_swinging && !_hasFinal && _along >= travel && _leadSlip.sqrMagnitude < 0.0001f;

            transform.SetPositionAndRotation(_centre, Quaternion.LookRotation(_heading * _handle, Vector3.up));
            Vector3 pusher = _pusher + _leadSlip;
            Vector3 velocity = dt > 0f ? Flat(pusher - _pusherWas) / dt : Vector3.zero;
            _pusherWas = pusher;
            _lead.ConveyTo(pusher, Quaternion.LookRotation(_heading, Vector3.up), velocity);

            // The second of a pair: at their place beside the leading end, or coming up to it no faster than a jog.
            if (_rear != null && !_rear.Present)
            {
                DropSecond();
            }
            if (_rear != null)
            {
                Vector3 place = SecondPlace();
                Vector3 was = _rearAt;
                _rearAt = Vector3.MoveTowards(_rearAt, place, GuideFastest * dt);
                Vector3 off = Flat(place - _rearAt);
                Vector3 going = dt > 0f ? Flat(_rearAt - was) / dt : Vector3.zero;
                _guideThere = off.sqrMagnitude < GuideInPlace * GuideInPlace;
                // (Only someone coming up from well off is turned the way they walk: a last step sideways is taken facing forwards.)
                Vector3 face = off.sqrMagnitude < GuideFacesAhead * GuideFacesAhead || going.sqrMagnitude < 0.04f ? _heading : going.normalized;
                _rear.ConveyTo(_rearAt, Quaternion.RotateTowards(_rearBody.rotation, Quaternion.LookRotation(face, Vector3.up), GuideTurn * dt), going);
            }

            if (_patient == null)
            {
                return;
            }
            if (!_patient.Present || _patientBody == null)
            {
                Unload();   // taken off our hands (the scene ended for them): nothing of theirs is moved again
                return;
            }
            if (!_supine)
            {
                _patientBody.SetPositionAndRotation(transform.TransformPoint(_patientPosition), transform.rotation * _patientRotation);
            }
            else if (_lift >= 1f || !AdvanceLift())
            {
                _pose.Hold(transform);
            }
        }

        private void DropSecond()
        {
            Release(_rear, _rearPaced);
            _rear = null;
            _rearBody = null;
            _rearPaced = false;
            _guideThere = false;
            Arms(null, 3);
        }

        // ---------------------------------------------------------------- the crew's few steps beside it

        private const int Walkers = 2;
        private readonly ActorRole[] _walker = new ActorRole[Walkers];
        private readonly Vector3[] _walkBy = new Vector3[Walkers * 3];   // by way of one place or two (if any), then to
        private readonly int[] _walkPlaces = new int[Walkers];
        private readonly int[] _walkNext = new int[Walkers];
        private readonly Vector3[] _walkFace = new Vector3[Walkers];
        private readonly Vector3[] _walkAt = new Vector3[Walkers];
        private readonly float[] _walkSpeed = new float[Walkers];

        /// <summary>
        /// Walks one of the crew from where they are to a place beside it (by way of another first, if they must go
        /// round someone lying there), at a walk, straight, and stands them there facing a point; whatever pose they
        /// were told to take they take on arrival, as after any order. They are moved the way the stretcher's crew is:
        /// no route is asked of the navigation mesh, so they cannot stop short of the place or be sent round. False
        /// (nothing changed) if it is not out or they cannot be had.
        /// </summary>
        /// <param name="first">A place to go by before any other (round a corner of the bed), if hasFirst.</param>
        /// <param name="way">A place to go by next (round the head or feet of whoever lies there), if byWay.</param>
        public bool Walk(ActorRole who, bool hasFirst, Vector3 first, bool byWay, Vector3 way, Vector3 to, Vector3 thenFace)
        {
            if (_failed || !Out || who == null || !who.Present)
            {
                return false;
            }
            int slot = -1;
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] == who || (slot < 0 && _walker[i] == null))
                {
                    slot = i;
                    if (_walker[i] == who)
                    {
                        break;
                    }
                }
            }
            if (slot < 0 || !who.Convey())
            {
                return false;
            }
            _walker[slot] = who;
            int places = 0;
            if (hasFirst)
            {
                _walkBy[slot * 3 + places++] = OnGround(first);
            }
            if (byWay)
            {
                _walkBy[slot * 3 + places++] = OnGround(way);
            }
            _walkBy[slot * 3 + places++] = OnGround(to);
            _walkPlaces[slot] = places;
            _walkNext[slot] = 0;
            _walkFace[slot] = thenFace;
            _walkAt[slot] = who.Position;
            _walkSpeed[slot] = 0f;
            enabled = true;
            return true;
        }

        /// <summary>True while that one of the crew is still on their way to the place they were walked to.</summary>
        public bool Walking(ActorRole who)
        {
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] != null && _walker[i] == who)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whoever is being walked is put at their place at once. Only for when nobody can see them.</summary>
        public void HurryWalks()
        {
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] != null)
                {
                    _walkAt[i] = _walkBy[i * 3 + _walkPlaces[i] - 1];
                    _walkNext[i] = _walkPlaces[i];
                    Arrive(i);
                }
            }
        }

        private static Vector3 OnGround(Vector3 place)
        {
            if (StreetSpots.Ground(place, out float ground))
            {
                place.y = ground;
            }
            return place;
        }

        private bool AnyWalking()
        {
            for (int i = 0; i < Walkers; i++)
            {
                if (_walker[i] != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void StepWalks(float dt)
        {
            for (int i = 0; i < Walkers; i++)
            {
                ActorRole who = _walker[i];
                if (who == null)
                {
                    continue;
                }
                ThirdPersonCharacter character = who.Conveyed ? who.Character : null;
                if (character == null)
                {
                    _walker[i] = null;   // gone, or given an order by whoever owns them: no longer ours to walk
                    continue;
                }
                bool last = _walkNext[i] == _walkPlaces[i] - 1;
                Vector3 to = _walkBy[i * 3 + _walkNext[i]];
                Vector3 run = Flat(to - _walkAt[i]);
                float left = run.magnitude;
                float pace = last ? Mathf.Min(SceneClips.WalkPace, Mathf.Max(0.3f, left * 3f)) : SceneClips.WalkPace;
                _walkSpeed[i] = Mathf.MoveTowards(_walkSpeed[i], pace, Quicken * dt);
                float step = _walkSpeed[i] * dt;
                if (left <= step + 0.004f)
                {
                    _walkAt[i] = to;
                    _walkNext[i]++;
                    if (last)
                    {
                        Arrive(i);
                        continue;
                    }
                    who.ConveyTo(to, character.transform.rotation, left > 0.001f ? run / left * _walkSpeed[i] : Vector3.zero);   // (round the place they go by without breaking step)
                    continue;
                }
                Vector3 way = run / left;
                Vector3 at = _walkAt[i] + way * step;
                at.y = Mathf.Lerp(_walkAt[i].y, to.y, step / left);
                _walkAt[i] = at;
                who.ConveyTo(at, Quaternion.RotateTowards(character.transform.rotation, Quaternion.LookRotation(way, Vector3.up), GuideTurn * dt), way * _walkSpeed[i]);
            }
        }

        // At their place: put exactly on it and stood there, facing what they were to face (and in whatever pose they were told to take).
        private void Arrive(int slot)
        {
            ActorRole who = _walker[slot];
            _walker[slot] = null;
            if (who == null || !who.Conveyed)
            {
                return;
            }
            ThirdPersonCharacter character = who.Character;
            if (character != null)
            {
                who.ConveyTo(_walkAt[slot], character.transform.rotation, Vector3.zero);
            }
            who.Stand(_walkFace[slot]);
        }

        // Nobody is left being walked by a stretcher that is put away: stood where they are.
        private void EndWalks()
        {
            for (int i = 0; i < Walkers; i++)
            {
                ActorRole who = _walker[i];
                _walker[i] = null;
                if (who != null && who.Conveyed)
                {
                    who.Stand(_walkFace[i]);
                }
            }
        }

        /// <summary>Left standing where it is. Whoever is on it stays on it; whoever had it has their hands (and their bag) back.</summary>
        public void Park()
        {
            LetGo();
            _mode = Mode.Parked;
            _path = null;
            _swinging = false;
            _hasFinal = false;
            _lifting = false;
            enabled = AnyWalking();
            FinishLift();   // nobody is left half way onto the bed
        }

        /// <summary>
        /// The crew lifts whoever was just loaded onto the bed while it stands: each of the two who
        /// is at an end of it plays the lifting clip, and the body goes up over the length of that
        /// clip. Returns how long that takes, or 0 if nobody can play it (the body then goes up in
        /// a moment as the stretcher is taken hold of, as it did before there was such a clip).
        /// </summary>
        public float Lift(ActorRole first, ActorRole second)
        {
            if (_failed || !Out || _mode != Mode.Parked || _patient == null || !_supine || _lift >= 1f
                || !ClipLibrary.TryGet(SceneClips.LiftPatient, out CityClip clip))
            {
                return 0f;
            }
            LetGo();
            _lead = first;
            _rear = second != first ? second : null;
            bool lifted = Lifts(_lead);
            lifted |= Lifts(_rear);
            if (!lifted)
            {
                LetGo();
                return 0f;
            }
            _liftTime = Mathf.Max(LiftTime, clip.Length);
            _lifting = true;
            enabled = true;
            return _liftTime;
        }

        // Both hands are needed: the bag goes onto the bed first.
        private bool Lifts(ActorRole who)
        {
            if (who == null || !AtAnEnd(who))
            {
                return false;
            }
            Stow(who);
            return who.Act(SceneClips.LiftPatient);
        }

        /// <summary>How far someone is from the nearer end of it (where its handles are held from), in metres.</summary>
        public float FromAnEnd(Vector3 at)
        {
            return Mathf.Sqrt(Mathf.Min(Flat(at - FootEnd).sqrMagnitude, Flat(at - HeadEnd).sqrMagnitude));
        }

        private bool AtAnEnd(ActorRole who)
        {
            return who.Present && FromAnEnd(who.Position) <= TakeWithin;
        }

        // The passenger is on the bed, laid out: the blanket goes over them.
        private void FinishLift()
        {
            if (_patient == null || !_supine || _lift >= 1f)
            {
                return;
            }
            _lift = 1f;
            _pose.Set(transform, 1f);
            if (_blanket != null)
            {
                _blanket.SetActive(true);
            }
        }

        // One step of the lift. False once there is nothing (more) to lift.
        private bool AdvanceLift()
        {
            if (_patient == null || !_supine || _lift >= 1f)
            {
                return false;
            }
            float lift = Mathf.Min(1f, _lift + Time.deltaTime / _liftTime);
            if (lift >= 1f)
            {
                FinishLift();
                return false;
            }
            _lift = lift;
            _pose.Set(transform, lift);
            return true;
        }

        /// <summary>
        /// Lays a casualty on the bed: on their back along the mattress, hips at its middle, head at
        /// the head end, whichever way they were lying (a ragdoll at rest is straightened out; an
        /// animated character is taken out of its lying animation). The body is lifted over a
        /// moment, then covered to the chest with the blanket, and from here on goes where the
        /// stretcher goes. A skeleton that cannot be posed is turned and carried as it lies. False
        /// if they cannot be picked up (still falling, or already gone); nothing is changed then.
        /// </summary>
        public bool Load(ICasualty patient)
        {
            if (_failed || patient == null || !Out || !patient.Lift(out Transform body, out Transform hips, out Transform head))
            {
                return false;
            }
            try
            {
                Transform bed = transform;
                _pose.Clear();
                Animator animator = body.GetComponentInChildren<Animator>(true);
                var character = body.GetComponent<ThirdPersonCharacter>();
                bool animated = character == null || !RagdollSystem.Holds(character);
                _supine = _pose.Begin(body, animator, bed, new Vector3(0f, BedHeight + HipsAbove, HipsAlong), animated);
                if (_supine)
                {
                    _lift = 0f;
                }
                else
                {
                    // Turned about the hips until the line from hips to head runs up the bed, then moved so the hips are on it.
                    Vector3 spine = head.position - hips.position;
                    spine.y = 0f;
                    if (spine.sqrMagnitude > 0.01f)
                    {
                        float turn = Vector3.SignedAngle(spine, bed.forward, Vector3.up);
                        body.rotation = Quaternion.AngleAxis(turn, Vector3.up) * body.rotation;
                    }
                    Vector3 middle = bed.position + Vector3.up * (BedHeight + 0.12f) - bed.forward * 0.1f;
                    body.position += middle - hips.position;
                    _lift = 1f;
                }

                _patient = patient;
                _patientBody = body;
                _patientPosition = bed.InverseTransformPoint(body.position);
                _patientRotation = Quaternion.Inverse(bed.rotation) * body.rotation;
                return true;   // the lift itself is played by the crew (Lift) or as they take hold (Step), or ended at once if nobody does (Park)
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }
        }

        /// <summary>
        /// Lets go of whoever is on the bed, before their owner takes them off the street: an
        /// animated character gets its animator back. The stretcher itself stays out.
        /// </summary>
        public void Unload()
        {
            _pose.Clear();
            _supine = false;
            _lift = 1f;
            _liftTime = LiftTime;
            _lifting = false;
            _patient = null;
            _patientBody = null;
            if (_blanket != null)
            {
                _blanket.SetActive(false);
            }
        }

        /// <summary>Shows a covered form on the bed, for someone who is not carried as they are.</summary>
        public void Cover()
        {
            if (_sheet != null && Out)
            {
                _sheet.SetActive(true);
            }
        }

        /// <summary>
        /// Back into the ambulance: hidden, and holding nobody. Whoever was on it is no longer
        /// moved by it; taking them off the street is their owner's business.
        /// </summary>
        public void PutAway()
        {
            EndWalks();
            Park();
            enabled = false;
            Unload();
            if (_sheet != null)
            {
                _sheet.SetActive(false);
            }
            gameObject.SetActive(false);
        }

        /// <summary>Puts the shared stretcher away, if there is one. For a scene that ends however it ends.</summary>
        public static void PutAwayShared()
        {
            if (_shared != null)
            {
                _shared.PutAway();
            }
        }

        // Everybody's hands off it: they walk as the game has them walk again, and the bag is back in the hand it rides out of.
        // (They stay as they were left, conveyed and standing still, until whoever owns them gives them their next order.)
        private void LetGo()
        {
            Release(_lead, _paced);
            Release(_rear, _rearPaced);
            _lead = null;
            _rear = null;
            _leadBody = null;
            _rearBody = null;
            _guideThere = false;
            _paced = false;
            _rearPaced = false;
        }

        private static void Release(ActorRole who, bool paced)
        {
            if (who != null)
            {
                if (paced)
                {
                    who.Gait(null);   // (only the clip it gave them is taken off them: how they carry the bag is theirs)
                }
                who.StowBag(null, default, default);
            }
        }

        // The medic bag of whoever has their hands on the stretcher rides on it: across the foot of the
        // mattress, or on the legs of whoever lies there. Never in a hand that is on a rail.
        private void Stow(ActorRole who)
        {
            bool onLegs = Laden || (_sheet != null && _sheet.activeSelf);
            who.StowBag(transform, new Vector3(0f, BedHeight + (onLegs ? BagOnLegs : 0f), BagAlong), Quaternion.identity);
        }

        // Shoulder, elbow and wrist of each arm, asked once per hold: left arm at 'first', right arm after it.
        private void Arms(Animator animator, int first)
        {
            bool human = animator != null && animator.isHuman && animator.avatar != null;
            _arms[first] = human ? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
            _arms[first + 1] = human ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            _arms[first + 2] = human ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            _arms[first + 6] = human ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            _arms[first + 7] = human ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            _arms[first + 8] = human ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("People: Stretcher.LateUpdate");
        // Runs only while it is being wheeled, while one of its crew is being walked, or while a body is being lifted onto it.
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            try
            {
                float dt = Time.deltaTime;
                StepWalks(dt);
                if (_mode == Mode.Parked)
                {
                    bool gone = _patient != null && (!_patient.Present || _patientBody == null);
                    if (gone)
                    {
                        Unload();   // taken off our hands in the middle of the lift
                    }
                    if (gone || !_lifting || !AdvanceLift())
                    {
                        _lifting = false;
                    }
                    enabled = _lifting || AnyWalking();
                    return;
                }
                if (_lead == null || !_lead.Conveyed)
                {
                    Park();   // whoever had it is gone (or was sent elsewhere by whoever owns them): it stays where it is until the scene puts it away
                    return;
                }
                Step(dt);
                Grip();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // Hands on the rails, after the animation has posed the arms: both of whoever pushes, on the rail at
        // their end; of the second, once in place beside the leading end, the hand nearer the bed on that
        // end's rail at its corner (both would twist them round to face it, and they are walking forwards).
        private void Grip()
        {
            if (_leadSlip.sqrMagnitude > 0.01f)
            {
                return;   // still closing up to the handle
            }
            // With the push bars: their grips, which is where the pushing clip already has the fists (0.44 m ahead of the feet,
            // 0.95 m up, 0.45 m apart), so the solver only takes up what a turn or a slope leaves. Without: the end rail.
            float along = _barred ? BarAlong : HalfLength + 0.04f;
            float apart = _barred ? BarApart : RailApart;
            Vector3 middle = transform.position + Vector3.up * (_barred ? BarHeight : RailHeight);
            Vector3 right = Vector3.Cross(Vector3.up, _heading);
            Vector3 handle = middle - _heading * along;
            Reach(0, handle - right * apart);
            Reach(6, handle + right * apart);
            if (_rear != null && _guideThere)
            {
                // (One hand: they walk forwards beside the corner, 0.55 m out, and the far grip is 0.78 m across the body from them.)
                Vector3 corner = middle + _heading * along + right * (_guideSide * (_barred ? BarApart : RailApart + 0.06f));
                Reach(_guideSide > 0f ? 3 : 9, corner);
            }
        }

        private void Reach(int arm, Vector3 grip)
        {
            if (_arms[arm] != null && _arms[arm + 1] != null && _arms[arm + 2] != null)
            {
                TwoBoneReach.Solve(_arms[arm], _arms[arm + 1], _arms[arm + 2], grip);
            }
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        private static Vector3 Flat(Vector3 direction, Vector3 otherwise)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.04f ? direction.normalized : otherwise;
        }

        // The stretcher is an extra. If it faults it is put away and the crews go back to doing without.
        private static void Fail(Exception e)
        {
            _failed = true;
            ActorPool.Logger?.Error(e);
            ActorPool.Logger?.Warn("People: the stretcher is switched off for this session after the error above.");
            try
            {
                if (_shared != null)
                {
                    _shared.PutAway();
                }
            }
            catch (Exception inner)
            {
                ActorPool.Logger?.Error(inner);
            }
        }
    }
}
