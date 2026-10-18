using System;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The ambulance crew's wheeled stretcher, built once and kept under the actor pool. It moves its crew along a line worked
    /// out beforehand (CrewPath, from CrewWay), so nothing can turn them aside, and carries a casualty laid on their back
    /// (SupinePose). Cost: per frame only while wheeled, walking someone or lifting; nothing otherwise.
    /// </summary>
    public sealed partial class Stretcher : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("people", "Stretcher", typeof(Stretcher));

        public const float HalfLength = 1f;         // metres
        public const float HandleReach = 0.55f;     // metres from an end of the bed to whoever holds it
        private const float BedHeight = 0.7f;       // metres from the road to the top of the mattress
        private const float HipsAbove = 0.10f;      // metres from the mattress to the hip joints of someone lying on their back
        private const float HipsAlong = -0.03f;     // ...and from the middle of the bed towards its head: head on the pillow, heels on the mattress
        private const float LiftTime = 0.7f;        // seconds a body takes from the road to lying on the bed, when nobody plays the lift

        private const float TakeWithin = 1f;        // metres from the handle within which someone is at that end of it
        private const float SlipRate = 1.2f;        // metres per second at which the gap between where someone stands and where the line has them closes
        private const float RailHeight = 0.86f;     // metres up: where the hands go, the top rail at each end
        private const float RailApart = 0.22f;      // metres: half the distance between the two hands on it
        private const float BarHeight = 0.95f;      // metres up with the modelled push bars (stretcher_push_bar): the cross bar push_cart's hands are made to...
        private const float BarApart = 0.225f;      // ...its two rubber grips either side of the middle line...
        private const float BarAlong = 1.11f;       // ...and its distance from the middle of the bed: HalfLength + HandleReach less the 0.44 m the clip holds a rail ahead of the feet
        private const float LiftBack = 0.10f;       // metres beyond the handle the crew kneel to lift with the bars fitted: the clip folds the head 0.5 m forward at bar height

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

        private const float BagAlong = -0.76f;      // metres along the bed where the medic bag rides: across the foot of the mattress...
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

        public bool Out => gameObject.activeSelf;

        public bool Laden => _patient != null;

        // Where somebody stands at the head end, or the foot end, to take hold of it.
        public Vector3 HeadEnd => transform.position + transform.forward * (HalfLength + HandleReach);
        public Vector3 FootEnd => transform.position - transform.forward * (HalfLength + HandleReach);

        /// <summary>Where whoever is to push it a given way stands: the end that trails, so it need not be turned round.</summary>
        public Vector3 HandleFor(Vector3 heading)
        {
            return End(Vector3.Dot(heading, transform.forward) >= 0f ? -1f : 1f);
        }

        /// <summary>...and where the other of a pair stands: the end that leads.</summary>
        public Vector3 LeadingFor(Vector3 heading)
        {
            return End(Vector3.Dot(heading, transform.forward) >= 0f ? 1f : -1f);
        }

        // Where someone stands at an end (1: the head end) before taking it up: at the handle, or a hand's breadth further out
        // with the push bars so whoever kneels to lift clears the bar; the difference is walked out (_leadSlip).
        private Vector3 End(float end)
        {
            return transform.position + transform.forward * (end * (HalfLength + HandleReach + (_barred ? LiftBack : 0f)));
        }

        /// <summary>The stretcher, built the first time it is wanted; null if there is none to be had.</summary>
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
                // The modelled gurney, its draped form if there is one, and its push bars if the bundle has them.
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

        private void BuildSheet(Mesh cube, Material linen)
        {
            Shapes.Piece("Trunk", _sheet.transform, cube, linen, new Vector3(0f, BedHeight + 0.11f, 0.3f), new Vector3(0.46f, 0.22f, 0.75f));
            Shapes.Piece("Legs", _sheet.transform, cube, linen, new Vector3(0f, BedHeight + 0.08f, -0.42f), new Vector3(0.40f, 0.16f, 0.85f));
            Shapes.Piece("Head", _sheet.transform, cube, linen, new Vector3(0f, BedHeight + 0.12f, 0.8f), new Vector3(0.24f, 0.22f, 0.26f));
        }

        /// <summary>
        /// Wheeled from now on along a line, its pusher and second moved with it, until <see cref="Arrived"/> or parked; out of
        /// the ambulance it appears at the start, otherwise it is taken up where it stands. False, and parked, without a pusher.
        /// </summary>
        public bool Wheel(ActorRole by, ActorRole second, CrewPath path, float guideSide, float handle = 1f, bool hasFinal = false,
            Vector3 finalCentre = default, Vector3 finalHeading = default)
        {
            ThirdPersonCharacter first = by != null ? by.Character : null;
            if (_failed || first == null || path == null || !path.Built)
            {
                Park();
                return false;
            }
            LetGo();   // whoever had it before; a lift is not cut short, it goes on as they set off
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
                _handle = handle < 0f ? -1f : 1f;
                _centre = centre;
                _heading = heading;
                _pusher = pusher;
                _leadSlip = Vector3.zero;
                gameObject.SetActive(true);
            }
            else
            {
                // Taken up where it stands, from whichever end they are at; how far they are off that handle is walked out.
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
                // Their walk is the game's, played to the speed they are moved at; a clip of the mod's only where that cannot be had.
                _rearPaced = !second.WalksUnaided && second.Gait(SceneClips.PullCart, SceneClips.WalkPace);
            }
            Stow(by);
            _paced = by.Gait(SceneClips.PushCart, SceneClips.WalkPace);
            enabled = true;
            Step(0f);
            ActorPool.Logger?.Info("People: stretcher pushed from its " + (_handle > 0f ? "foot" : "head") + " end, both hands on that rail"
                                   + (_rear != null ? "; the second of the crew is walked beside its leading end, the near hand on that rail" : "; nobody at its other end")
                                   + (_barred ? "; hands on the push bar's grips" : "; no push bar (hands on the end rail)")
                                   + (_paced ? "; pushing clip, " + SceneClips.WalkPace.ToString("F1", CultureInfo.InvariantCulture) + " m/s" : "; the game's walk (no pushing clip to be had)")
                                   + FormattableString.Invariant($"; wheeled along its line, {path.Travel:F1} m") + (_swinging ? ", swung round to it first" : "")
                                   + (_hasFinal ? ", swung into place at the end." : "."));
            return true;
        }

        /// <summary>True once it is at the end of its line, and in its last pose if it had one.</summary>
        public bool Arrived { get; private set; }

        public float Progress => _along;

        public Vector3 LeadingEnd => transform.position + _heading * HalfLength;

        public Vector3 Heading => _heading;

        public bool Held => _mode != Mode.Parked;

        /// <summary>The rest of the way at once, its crew in place: only for when nobody can see it.</summary>
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

        // One frame of being wheeled: along the line or round a swing, and everybody put where that has them.
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

            // The second of a pair: at their place by the leading end, or coming up to it no faster than a jog.
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
                // Only someone coming up from well off turns the way they walk: a last step sideways is taken facing forwards.
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

        /// <summary>Left standing where it is; whoever is on it stays on it, and whoever had it has their hands and bag back.</summary>
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

        /// <summary>Back into the ambulance, hidden and holding nobody; taking the passenger off the street is their owner's business.</summary>
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

        public static void PutAwayShared()
        {
            if (_shared != null)
            {
                _shared.PutAway();
            }
        }

        // Everybody's hands off it: the game's walk again and the bag back in the hand. They stay conveyed and still until
        // their owner gives the next order.
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
                    who.Gait(null);   // only the clip it gave them: how they carry the bag is theirs
                }
                who.StowBag(null, default, default);
            }
        }

        // The medic bag of whoever has their hands on it rides on the bed: across the foot of the mattress, or on the legs.
        private void Stow(ActorRole who)
        {
            bool onLegs = Laden || (_sheet != null && _sheet.activeSelf);
            who.StowBag(transform, new Vector3(0f, BedHeight + (onLegs ? BagOnLegs : 0f), BagAlong), Quaternion.identity);
        }

        // Shoulder, elbow and wrist of each arm, asked once per hold: the left arm at 'first', the right after it.
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
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
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
                    Park();   // whoever had it is gone or was sent elsewhere: it stays until the scene puts it away
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

        // Hands on the rails, after the animation has posed the arms: both of the pusher's on their end; the second's near
        // hand at the leading corner once in place (both would twist them round, and they walk forwards).
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
                // One hand: they walk forwards beside the corner, and the far grip is 0.78 m across the body from them.
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

        // The stretcher is an extra: if it faults it is put away and the crews do without.
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
