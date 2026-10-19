using System.Collections.Generic;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>What the director runs: a fire, or a street event. One at a time.</summary>
    internal interface IEventScene
    {
        /// <summary>The locale key of what the marker says.</summary>
        string Label { get; }
        Vector3 Focus { get; }
        /// <summary>Metres from the focus beyond which the player has left it behind and it is cleared.</summary>
        float Reach { get; }
        /// <summary>Four times a second. False when it has nothing more to do (the director then calls End).</summary>
        bool Tick(float now, Vector3 player);
        /// <summary>Everything given back and the road reopened. Safe in any state, any number of times.</summary>
        void End(bool trafficRunning);
        /// <summary>The locale key of what the interaction key would do where the player stands, or null.</summary>
        string Prompt(Vector3 player);
        void Interact(float now, Vector3 player);
        /// <summary>The player rang 911 about it. True if the call did something.</summary>
        bool Report(float now);
    }

    /// <summary>
    /// A street event: a stretch of road closed with barriers and tape (the cordon), marshals at
    /// its ends, and on the closed carriageway whatever the kind calls for: market stalls with
    /// vendors and browsing customers, a column marching with banners, a crowd with placards
    /// under the eye of two officers, runners with numbers, a film crew, a crowd watching
    /// fireworks. Eight people at most (the event pool) and two officers (the accident pool's);
    /// stepped four times a second, most of it once a second.
    ///
    /// Nobody appears in view: the place is chosen where the camera is not looking, the staff
    /// walk in while it is set up, the crowd walks in and walks off. When it is over the crowd
    /// leaves, the staff pack up and walk away, the tape comes down, and the props go once
    /// nobody is looking (or after a minute and a half). Every way out, a fault included, ends
    /// in <see cref="End"/>, which gives everybody and everything back and reopens the road.
    /// </summary>
    internal sealed class StreetEventScene : IEventScene
    {
        private enum Phase { SetUp, Running, TakeDown, Clearing, Done }
        private enum Job { Marshal, Vendor, Browser, Marcher, Runner, Onlooker, Protester, Organiser, Operator, FilmDirector, Assistant, Actor, Watcher }

        private const int MaxCast = CityPeople.MaxPeople;
        private const int MaxStalls = 3;
        private const float OperatorBehind = 0.75f;      // camera_operate: the operator's feet behind film_camera's pivot, on the lens's line
        private const float OperatorTolerance = 0.08f;   // and how far off that place the clip still meets the viewfinder
        private const int OperatorTries = 4;
        internal static int SnackPrice => Prices.At(6);
        private const float SnackEnergy = 6f;

        private struct Member
        {
            public CityPerson Who;
            public Job Job;
            public int Step;
            public float Until;
            public Vector3 Post;
            public Vector3 Face;
            public int Slot;
        }

        private struct Stall
        {
            public Vector3 Customer;   // where a customer stands
            public Vector3 Counter;    // what they face
            public Vector3 Behind;     // where the vendor stands
            public bool Taken;         // a browser is at it or on the way
            public float ServeAt;
        }

        private readonly EventDirector _director;
        private readonly Member[] _cast = new Member[MaxCast];
        private readonly Stall[] _stalls = new Stall[MaxStalls];
        private readonly List<Transform> _dressing = new List<Transform>(12);
        private readonly List<Footprint> _footprints = new List<Footprint>(1);
        private int _count;
        private int _stallCount;
        private Vector3 _operatorPost;       // a film shoot: where camera_operate was made to stand, from where the camera really is
        private bool _operatorAtCamera;      // the operator is there and at the viewfinder
        private EventKind _kind;
        private Phase _phase = Phase.Done;
        private Vector3 _centre, _along, _right;
        private float _half, _width;
        private bool _test;
        private int _day, _closesAt;
        private float _phaseUntil, _nextSecond, _nextArrival, _nextCue, _testEnds;
        private bool _taking;          // film: a take is being shot
        private bool _towardsB;        // march: which end the column is heading for
        private bool _thanked;         // the player has had this event's mood
        private float _nearSince;      // fireworks: since when the player has been watching
        private float _nextCheer;
        private Vector3 _sky;          // fireworks: where they burst
        private int _officers;
        private string _refusal;

        public StreetEventScene(EventDirector director)
        {
            _director = director;
        }

        public EventKind Kind => _kind;
        public string Label => EventCalendar.Name(_kind);
        public Vector3 Focus => _centre;
        public float Reach => 260f;
        /// <summary>Why the last Stage put nothing on the street, for the log.</summary>
        public string Refusal => _refusal;
        /// <summary>True if the last Stage got as far as closing the road before it gave up (it moved traffic and took people).</summary>
        public bool Costly { get; private set; }

        // ---------------------------------------------------------------- staging

        /// <summary>
        /// Closes a stretch of road near a place and dresses it. False (with the street left as it
        /// was) if there is no plain stretch of road there, it is in view and this is not a test, or
        /// too little of what the event needs has room.
        /// </summary>
        public bool Stage(EventKind kind, Vector3 near, bool test, int closesAt, float now)
        {
            _refusal = null;
            Costly = false;
            _kind = kind;
            _test = test;
            _day = TimeHelper.CurrentDay;
            _closesAt = closesAt;
            _count = 0;
            _stallCount = 0;
            _officers = 0;
            _thanked = false;
            _taking = false;
            _towardsB = true;
            _nearSince = 0f;
            _half = kind == EventKind.Protest || kind == EventKind.FilmShoot ? 10f : kind == EventKind.Fireworks ? 8f : kind == EventKind.Parade || kind == EventKind.FunRun ? 16f : 13f;
            if (!EventRoad.Lane(near, test ? 30f : 22f, out Waypoint lane, out _along))
            {
                _refusal = "no plain stretch of road within reach";
                return false;
            }
            _right = Vector3.Cross(Vector3.up, _along);
            _centre = lane.position;
            _width = 1.7f;
            if (EventRoad.Oncoming(lane, _along, out Vector3 other))
            {
                _width = Vector3.Distance(other, lane.position) * 0.5f + 1.7f;
                _centre = (other + lane.position) * 0.5f;
            }
            if (StreetSpots.Ground(_centre, out float ground))
            {
                _centre.y = ground;
            }
            // Both ends must be plain road too: a junction inside the stretch would leave a way in for traffic.
            if (!EventRoad.Lane(lane.position + _along * _half, 5f, out _, out Vector3 a) || !EventRoad.Lane(lane.position - _along * _half, 5f, out _, out Vector3 b)
                || Vector3.Dot(a, _along) < 0.85f || Vector3.Dot(b, _along) < 0.85f)
            {
                _refusal = "a junction or a bend lies inside the stretch";
                return false;
            }
            if (!test && CitySpots.InView(_centre))
            {
                _refusal = "the place is in view";
                return false;
            }
            _footprints.Clear();
            _footprints.Add(EventRoad.Stretch(_centre, _along, _half - 3f, Mathf.Max(0.6f, _width - 0.6f)));   // the cordon adds its own margins
            Costly = true;
            _director.Cordon.Close(_centre, _along, _footprints, true);
            if (!_director.Cordon.Closed)
            {
                _refusal = "the road could not be closed";
                _director.Cordon.Open();
                return false;
            }
            _phase = Phase.SetUp;
            _phaseUntil = now + 45f;
            _nextSecond = now;
            _nextArrival = now + 4f;
            _testEnds = now + 200f;
            bool dressed = Dress(now);
            if (!dressed)
            {
                _refusal = _refusal ?? "too little of it had room on this street";
                End(true);
                return false;
            }
            _director.Cordon.Raise(now);
            EventDirector.Logger?.Info(System.FormattableString.Invariant($"Street event: {kind} staged{(test ? " (test)" : "")}: {_half * 2f:F0} m of road closed, {_width * 2f:F1} m across, {_stallCount} stall(s), {_dressing.Count} prop(s), ")
                                       + $"{_count} staff walking in, {_officers} officer(s).");
            return true;
        }

        // What stands on the closed road from the start, and the staff, who walk in.
        private bool Dress(float now)
        {
            switch (_kind)
            {
                case EventKind.Market:
                    AddStall("vendor_cart", -7.5f, CityLook.Vendor, true);
                    AddStall("fruit_stand", 0f, CityLook.Vendor, false);
                    AddStall("coffee_cart", 7.5f, CityLook.Vendor, false);   // (the newspaper kiosk shows nobody inside: it has no vendor to serve from it)
                    AddMarshal(false);
                    return _stallCount > 0;
                case EventKind.FarmersMarket:
                    AddStall("fruit_stand", -7.5f, CityLook.Gardener, false);
                    AddStall("coffee_cart", 0f, CityLook.Vendor, false);
                    AddStall("fruit_stand", 7.5f, CityLook.Gardener, false);
                    AddMarshal(false);
                    return _stallCount > 0;
                case EventKind.Parade:
                    AddMarshal(false);
                    AddMarshal(true);
                    for (int i = 0; i < 4; i++)
                    {
                        if (Spot(-_half + 4f + (i / 2) * 1.6f, (i % 2 == 0 ? -0.8f : 0.8f), 0.4f, out Vector3 post))
                        {
                            Walk(Job.Marcher, CityLook.Civilian, post, post + _along * 5f, i, true);
                        }
                    }
                    return Has(Job.Marcher) >= 2;
                case EventKind.FunRun:
                    AddMarshal(false);
                    AddMarshal(true);
                    return true;
                case EventKind.Protest:
                    for (int i = 0; i < 5; i++)
                    {
                        if (Spot(-1.2f + (i / 3) * 1.3f, -1.3f + (i % 3) * 1.3f, 0.4f, out Vector3 post))
                        {
                            Walk(Job.Protester, CityLook.Civilian, post, post + _along * 6f, i, true);
                        }
                    }
                    if (Spot(-4f, -Mathf.Max(0.5f, _width - 1.1f), 0.45f, out Vector3 desk))
                    {
                        Walk(Job.Organiser, CityLook.Canvasser, desk, desk + _right * 4f, 0, true);
                    }
                    Officers(now);
                    return Has(Job.Protester) >= 3;
                case EventKind.FilmShoot:
                    return FilmSet();
                default:
                    _sky = _centre + _right * (Random.value < 0.5f ? 85f : -85f) + _along * Random.Range(-20f, 20f) + Vector3.up * 58f;
                    if (PlaceProp(EventProps.MortarRack, _half - 2.5f, 0f, 0.7f, 0.4f, 0f) == null && !_director.Sky.Ready())
                    {
                        return false;
                    }
                    AddMarshal(false);
                    return _director.Sky.Ready();
            }
        }

        private bool FilmSet()
        {
            Transform camera = PlaceProp(EventProps.FilmCamera, -3.5f, 0f, 0.5f, 0.5f, 0f);
            if (camera == null)
            {
                return false;
            }
            PlaceProp(EventProps.LightStand, -1.5f, -Mathf.Min(2.2f, _width - 0.8f), 0.5f, 0.5f, 25f);
            PlaceProp(EventProps.LightStand, -1.5f, Mathf.Min(2.2f, _width - 0.8f), 0.5f, 0.5f, -25f);
            PlaceProp(EventProps.Clapper, -5.6f, Mathf.Min(1.4f, _width - 0.8f), 0.4f, 0.4f, 0f);
            Vector3 lens = _centre - _along * 3.5f;
            // The operator: camera_operate is made for somebody standing 0.75 m behind the camera's pivot on the lens's line,
            // facing the way the lens looks, the right eye at the viewfinder (film_camera's eyecup is 0.59 m behind its pivot,
            // 1.50 m up). A hand's breadth off and the eye misses it, so the place is taken from where the camera really
            // stands, the walk is finished to it (Arrivals) and the clip is played only there.
            _operatorPost = camera.position - camera.forward * OperatorBehind;
            _operatorAtCamera = false;
            if (Spot(-3.5f - OperatorBehind, 0f, 0.35f, out Vector3 behind))
            {
                if (CitySpots.Flat(behind - _operatorPost) < OperatorTolerance * OperatorTolerance)
                {
                    behind = new Vector3(_operatorPost.x, behind.y, _operatorPost.z);
                }
                Walk(Job.Operator, CityLook.Civilian, behind, camera.position + camera.forward * 5f, 0, true);
            }
            if (Spot(-5.6f, Mathf.Min(1.4f, _width - 0.8f) - 0.9f, 0.4f, out Vector3 chair))
            {
                Walk(Job.FilmDirector, CityLook.Civilian, chair, lens + _along * 5f, 0, true);
            }
            if (Spot(-6.5f, -Mathf.Min(1.6f, _width - 0.8f), 0.4f, out Vector3 radio))
            {
                Walk(Job.Assistant, CityLook.Courier, radio, _centre, 0, true);
            }
            if (Spot(1.6f, -0.55f, 0.35f, out Vector3 first) && Spot(1.6f, 0.55f, 0.35f, out Vector3 second))
            {
                Walk(Job.Actor, CityLook.Civilian, first, second, 0, true);
                Walk(Job.Actor, CityLook.Civilian, second, first, 1, true);
            }
            AddMarshal(false);
            return Has(Job.Actor) == 2 && Has(Job.Operator) == 1;
        }

        // A stall against one side of the road, its counter to the middle; its vendor walks in.
        private void AddStall(string prop, float along, CityLook look, bool parasol)
        {
            if (_stallCount >= MaxStalls)
            {
                return;
            }
            Vector3 size = PropLibrary.SizeOf(prop);
            if (size == Vector3.zero)
            {
                return;   // no such stall in the bundle: there is no stand-in for one
            }
            float depth = size.z * 0.5f;
            float standsBack = depth + (depth > 0.5f ? 0.3f : 0.45f);   // the vendor, behind the stall: nearer a deep one, whose counter is at its back
            float across = -(_width - standsBack - depth - 0.55f);      // so that the vendor has half a metre of road behind them
            if (across > -0.2f)
            {
                return;   // the road is too narrow for a stall, its vendor and its customers
            }
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float at = along + (attempt == 0 ? 0f : attempt == 1 ? 2f : attempt == 2 ? -2f : 3.5f);
                Vector3 place = Point(at, across);
                Quaternion facing = Quaternion.LookRotation(_right);   // customers at +Z: towards the middle of the road
                if (!SceneParking.IsFree(place, facing, size.x * 0.5f, depth, 0.25f) || !StreetSpots.OnRoad(place, 0.6f, out _))
                {
                    continue;
                }
                Vector3 customer = place + _right * (depth + 0.65f);
                Vector3 behind = place - _right * standsBack;
                if (!StreetSpots.OnRoad(customer, 0.5f, out customer) || !StreetSpots.OnRoad(behind, 0.4f, out behind) || !CitySpots.ClearQuietly(behind, 0.3f))
                {
                    continue;
                }
                Transform stall = _director.Props.Stand(prop, place, facing.eulerAngles.y);
                if (stall == null)
                {
                    return;
                }
                _dressing.Add(stall);
                if (parasol)
                {
                    Vector3 pole = place - _along * (size.x * 0.5f + 0.7f) - _right * 0.2f;
                    if (SceneParking.IsFree(pole, facing, 0.25f, 0.25f, 0.1f))
                    {
                        Transform shade = _director.Props.Stand("vendor_umbrella", pole, 0f);
                        if (shade != null)
                        {
                            _dressing.Add(shade);
                        }
                    }
                }
                int slot = _stallCount++;
                _stalls[slot] = new Stall { Customer = customer, Counter = place, Behind = behind };
                Walk(Job.Vendor, look, behind, customer, slot, true);
                return;
            }
        }

        private void AddMarshal(bool farEnd)
        {
            Vector3 post = Point((farEnd ? 1f : -1f) * (_half + 2.2f), -Mathf.Max(0f, _width - 1.2f));
            if (StreetSpots.OnRoad(post, 1.5f, out post) && CitySpots.ClearQuietly(post, 0.4f))
            {
                Walk(Job.Marshal, CityLook.RoadWorker, post, post + _along * (farEnd ? 12f : -12f), farEnd ? 1 : 0, true);
            }
        }

        // Two officers at the far end, facing the crowd: only there to watch.
        private void Officers(float now)
        {
            if (!Settings.Police || !Settings.People)
            {
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 post = Point(_half - 2.5f, i == 0 ? -1.2f : 1.2f);
                ActorRole officer = _director.Officers[i];
                if (StreetSpots.OnRoad(post, 1f, out post) && officer.Enter(post, ActorLook.Officer, 1.5f, true))
                {
                    officer.Go(post, Pace.Walk, _centre, 10f);
                    officer.Work("guard_stand");
                    _officers++;
                }
            }
        }

        // ---------------------------------------------------------------- places

        private Vector3 Point(float along, float across)
        {
            Vector3 point = _centre + _along * along + _right * across;
            if (StreetSpots.Ground(point, out float ground))
            {
                point.y = ground;
            }
            return point;
        }

        // A place on the closed carriageway with room for a person.
        private bool Spot(float along, float across, float room, out Vector3 spot)
        {
            across = Mathf.Clamp(across, -(_width - 0.6f), _width - 0.6f);
            return StreetSpots.OnRoad(Point(along, across), 0.5f, out spot) && CitySpots.ClearQuietly(spot, room);
        }

        private Transform PlaceProp(string id, float along, float across, float halfWidth, float halfLength, float turn)
        {
            Vector3 place = Point(along, Mathf.Clamp(across, -(_width - halfWidth), _width - halfWidth));
            Quaternion facing = Quaternion.LookRotation(_along) * Quaternion.Euler(0f, turn, 0f);
            if (!SceneParking.IsFree(place, facing, halfWidth, halfLength, 0.2f))
            {
                return null;
            }
            Transform prop = _director.Props.Stand(id, place, facing.eulerAngles.y);
            if (prop != null)
            {
                _dressing.Add(prop);
            }
            return prop;
        }

        // ---------------------------------------------------------------- the cast

        private int Has(Job job)
        {
            int has = 0;
            for (int i = 0; i < _count; i++)
            {
                has += _cast[i].Job == job ? 1 : 0;
            }
            return has;
        }

        // One more person, walking in from out of view (a test may not wait for that: they are put at their place).
        private bool Walk(Job job, CityLook look, Vector3 post, Vector3 face, int slot, bool road, bool jog = false)
        {
            if (_count >= MaxCast)
            {
                return false;
            }
            EventPeople people = _director.People;
            CityPerson who = people.WalkIn(look, post, road, jog);
            bool walking = who != null;
            if (who == null && _test)
            {
                who = people.Put(look, post, face, road);
            }
            if (who == null)
            {
                return false;
            }
            _cast[_count++] = new Member { Who = who, Job = job, Step = walking ? 0 : 1, Post = post, Face = face, Slot = slot, Until = Time.time + 70f };
            if (job == Job.Runner)
            {
                people.Wear(who, EventProps.Bib, HumanBodyBones.UpperChest, 0f, 0f);
            }
            else if (job == Job.Marcher || job == Job.Protester)
            {
                if (people.CarryStaff(who, job == Job.Marcher && slot < 2 ? EventProps.Banner : EventProps.Placard))
                {
                    who.WorkOver(EventPeople.StaffClip, people.Props.Shelf, 0.2f, ClipEnd.Return, BodyMask.RightArm, false);
                }
            }
            if (!walking)
            {
                Arrive(ref _cast[_count - 1], Time.time);
            }
            return true;
        }

        private void Drop(int index, bool sendOff)
        {
            Member member = _cast[index];
            if (member.Job == Job.Browser && member.Slot >= 0)
            {
                _stalls[member.Slot].Taken = false;
            }
            if (sendOff && !member.Who.Lost)
            {
                _director.People.SendOff(member.Who, member.Job == Job.Runner);
            }
            else if (!member.Who.Lost)
            {
                _director.People.Release(member.Who);
            }
            _cast[index] = _cast[--_count];
            _cast[_count] = default;
        }

        // Somebody has reached their place: they stand, turn to what they are there for and take up what they do.
        private void Arrive(ref Member member, float now)
        {
            CityPerson who = member.Who;
            CityProps shelf = _director.People.Props.Shelf;
            member.Step = 1;
            if (member.Job == Job.Runner || (member.Job == Job.Marcher && _phase == Phase.Running))
            {
                return;   // they keep moving: see Second
            }
            who.Stand();
            who.Face(member.Face);
            switch (member.Job)
            {
                case Job.Marshal:
                    if (!who.Work("paddle_hold", shelf, 0.3f))
                    {
                        who.Work("guard_stand", shelf);
                    }
                    break;
                case Job.Vendor:
                    who.Work("counter_wipe", shelf, 0.3f, ClipEnd.Return, true, Random.value * 2f);
                    break;
                case Job.Browser:
                    who.Work("queue_wait", shelf, 0.3f, ClipEnd.Return, true, Random.value * 4f);
                    member.Until = now + Random.Range(4f, 8f);
                    break;
                case Job.Onlooker:
                case Job.Watcher:
                    who.Work("watch_nod", shelf, 0.3f, ClipEnd.Return, true, Random.value * 3f);
                    member.Until = now + Random.Range(3f, 9f);
                    break;
                case Job.Organiser:
                    who.Work("clipboard_write", shelf);
                    break;
                case Job.Operator:
                    // At the viewfinder only if they really stand where the clip was made for; anywhere else they stand by it.
                    _operatorAtCamera = who.Near(_operatorPost, OperatorTolerance) && ClipLibrary.Has("camera_operate");
                    who.Work(_operatorAtCamera ? "camera_operate" : "guard_stand", shelf);
                    member.Until = now + 4f;
                    break;
                case Job.FilmDirector:
                    who.Work("clipboard_write", shelf);
                    break;
                case Job.Assistant:
                    who.Work("radio_talk", shelf);
                    break;
                case Job.Actor:
                    who.Work("arms_crossed", shelf, 0.3f, ClipEnd.Return, true, member.Slot * 3f);
                    break;
                case Job.Protester:
                    who.Work(EventPeople.StaffClip, shelf, 0.3f, ClipEnd.Return, false, member.Slot * 0.7f);   // the staff is already in the hand; the free hand punches the air
                    break;
            }
        }

        // ---------------------------------------------------------------- running

        public bool Tick(float now, Vector3 player)
        {
            if (_phase == Phase.Done)
            {
                return false;
            }
            _director.Cordon.Tick(now);
            for (int i = 0; i < _director.Officers.Length; i++)
            {
                _director.Officers[i].Tick(now);   // a role with nobody behind it does nothing
            }
            if (_kind == EventKind.Fireworks && _phase == Phase.Running && now >= _nextCue)
            {
                _nextCue = now + Random.Range(0.9f, 2.6f);
                _director.Sky.Burst(_sky + Random.insideUnitSphere * 16f);
            }
            if (now < _nextSecond)
            {
                return true;
            }
            _nextSecond = now + 1f;
            _director.People.Tick(_centre, false);
            for (int i = _count - 1; i >= 0; i--)
            {
                if (_cast[i].Who.Lost)
                {
                    Drop(i, false);   // knocked down by a car: the casualty system has them
                }
            }
            switch (_phase)
            {
                case Phase.SetUp:
                    Arrivals(now);
                    if (AllIn() || now >= _phaseUntil)
                    {
                        _phase = Phase.Running;
                        _nextArrival = now + 2f;
                        _nextCue = now + 6f;
                        if (!EventCalendar.Quiet(_kind))
                        {
                            Notifications.Show(NotificationType.Info, Open(), null, 8f);
                        }
                    }
                    break;
                case Phase.Running:
                    Arrivals(now);
                    Second(now, player);
                    bool over = _test ? now >= _testEnds : TimeHelper.CurrentDay != _day || TimeHelper.CurrentHour >= _closesAt;
                    bool storm = !_test && Weather.TakeCover;
                    if (over || storm || Starved())
                    {
                        PackUp(now, storm);
                    }
                    break;
                case Phase.TakeDown:
                    if (now >= _phaseUntil)
                    {
                        // The staff have packed: they walk off, and the tape comes down behind them.
                        for (int i = _count - 1; i >= 0; i--)
                        {
                            Drop(i, true);
                        }
                        for (int i = 0; i < _director.Officers.Length; i++)
                        {
                            _director.Officers[i].WalkAwayFrom(_centre, Pace.Walk, 40f, 40f);
                        }
                        _director.Cordon.Lower();
                        _phase = Phase.Clearing;
                        _phaseUntil = now + 90f;
                    }
                    break;
                case Phase.Clearing:
                    if (now >= _phaseUntil || (_director.People.LeavingCount == 0 && !CitySpots.InView(_centre)))
                    {
                        _phase = Phase.Done;
                        return false;
                    }
                    break;
            }
            return true;
        }

        private bool AllIn()
        {
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Step == 0)
                {
                    return false;
                }
            }
            return true;
        }

        // The event has lost what it is about (every vendor, most of the march): it packs up.
        private bool Starved()
        {
            switch (_kind)
            {
                case EventKind.Market:
                case EventKind.FarmersMarket: return Has(Job.Vendor) == 0;
                case EventKind.Parade: return Has(Job.Marcher) < 2;
                case EventKind.Protest: return Has(Job.Protester) < 2;
                case EventKind.FilmShoot: return Has(Job.Actor) < 2;
                default: return false;
            }
        }

        private void Arrivals(float now)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Step != 0)
                {
                    continue;
                }
                CityPerson who = _cast[i].Who;
                if (who.Arrived && who.Near(_cast[i].Post, 0.7f))
                {
                    // The camera operator's place is exact (see FilmSet): the last steps are walked to it, the agent sent as
                    // far past it as it stops short. Tried a few times (Slot counts them; an operator has no other use for it).
                    if (_cast[i].Job == Job.Operator && _cast[i].Slot < OperatorTries && !who.Near(_cast[i].Post, OperatorTolerance))
                    {
                        _cast[i].Slot++;
                        Vector3 to = _cast[i].Post - who.Position;
                        to.y = 0f;
                        float stop = Mathf.Max(0f, who.Character.navmeshAgent.stoppingDistance);
                        who.WalkTo(to.sqrMagnitude > 0.0001f ? _cast[i].Post + to.normalized * stop : _cast[i].Post);
                        continue;
                    }
                    Arrive(ref _cast[i], now);
                }
                else if (who.Arrived || now >= _cast[i].Until)
                {
                    if (now >= _cast[i].Until)
                    {
                        Drop(i, true);   // never got there: they walk off again
                        return;
                    }
                    who.WalkTo(_cast[i].Post, _cast[i].Job == Job.Runner);
                }
            }
        }

        // Once a second while it is on: who comes, who goes, who does what next.
        private void Second(float now, Vector3 player)
        {
            EventPeople people = _director.People;
            CityProps shelf = people.Props.Shelf;
            bool busy = false;   // something is passing that onlookers clap for

            for (int i = _count - 1; i >= 0; i--)
            {
                CityPerson who = _cast[i].Who;
                if (_cast[i].Step == 0)
                {
                    continue;
                }
                switch (_cast[i].Job)
                {
                    case Job.Browser:
                        if (_cast[i].Step == 1 && now >= _cast[i].Until)
                        {
                            // Paid: the vendor serves, and they are off with what they bought.
                            _cast[i].Step = 2;
                            _cast[i].Until = now + 2.6f;
                            who.WorkOver("pay_tap", shelf, 0.2f, ClipEnd.Return, BodyMask.Upper);
                            _stalls[_cast[i].Slot].ServeAt = now + 0.6f;
                        }
                        else if (_cast[i].Step == 2 && now >= _cast[i].Until)
                        {
                            who.WorkOver("cup_hold", shelf, 0.3f, ClipEnd.Return, BodyMask.RightArm);
                            Drop(i, true);
                        }
                        break;
                    case Job.Vendor:
                        if (_stalls[_cast[i].Slot].ServeAt > 0f && now >= _stalls[_cast[i].Slot].ServeAt)
                        {
                            _stalls[_cast[i].Slot].ServeAt = 0f;
                            who.Work("serve", shelf, 0.25f);
                            _cast[i].Until = now + 2.4f;
                            _cast[i].Step = 2;
                        }
                        else if (_cast[i].Step == 2 && now >= _cast[i].Until)
                        {
                            _cast[i].Step = 1;
                            who.Work("counter_wipe", shelf, 0.3f);
                        }
                        break;
                    case Job.Runner:
                        busy = true;
                        if (_cast[i].Step == 1)
                        {
                            _cast[i].Step = 2;
                            _cast[i].Until = now + 45f;
                            who.WalkTo(Point(_half - 1.5f, _cast[i].Slot % 2 == 0 ? -0.7f : 0.7f), true);
                        }
                        else if (who.Arrived || now >= _cast[i].Until)
                        {
                            Drop(i, true);   // through the finish: off they jog
                        }
                        break;
                    case Job.Operator:
                        if (now >= _cast[i].Until && !_operatorAtCamera)
                        {
                            _cast[i].Until = now + Random.Range(5f, 9f);
                            who.WorkOver("till_tap", shelf, 0.2f, ClipEnd.Return, BodyMask.Upper);   // a hand on the camera's controls
                        }
                        break;
                    case Job.Onlooker:
                    case Job.Watcher:
                        if (now >= _cast[i].Until)
                        {
                            _cast[i].Until = now + Random.Range(5f, 9f);
                            bool film = _cast[i].Job == Job.Watcher || Random.value < 0.4f;
                            if (film && Random.value < 0.6f)
                            {
                                who.Work("take_photo", shelf, 0.3f);
                            }
                            else
                            {
                                who.Work(_cast[i].Job == Job.Watcher && ClipLibrary.Has("crowd_look_up") ? "crowd_look_up" : _cast[i].Step == 2 ? "clap" : "watch_nod", shelf, 0.3f);
                            }
                        }
                        break;
                }
            }

            if (_kind == EventKind.Parade)
            {
                busy = March(now);
            }
            else if (_kind == EventKind.FilmShoot)
            {
                Takes(now, player);
            }
            else if (_kind == EventKind.Fireworks)
            {
                busy = true;
                if (!_thanked && CitySpots.Flat(player - _centre) < 70f * 70f)
                {
                    _nearSince = _nearSince <= 0f ? now : _nearSince;
                    if (now - _nearSince > 20f)
                    {
                        Thank("reallife_event_fireworks_watched", ModState.MoodUpBig);
                    }
                }
            }
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Job == Job.Onlooker && _cast[i].Step != 0)
                {
                    _cast[i].Step = busy ? 2 : 1;   // claps the next time they change what they do
                }
            }

            // Who walks in next.
            if (now < _nextArrival || people.Free == 0 || _count >= MaxCast)
            {
                return;
            }
            _nextArrival = now + Random.Range(5f, 10f);
            switch (_kind)
            {
                case EventKind.Market:
                case EventKind.FarmersMarket:
                    if (Has(Job.Browser) < 4)
                    {
                        int start = Random.Range(0, _stallCount);
                        for (int k = 0; k < _stallCount; k++)
                        {
                            int slot = (start + k) % _stallCount;
                            if (!_stalls[slot].Taken && VendorAt(slot) && Walk(Job.Browser, CityLook.Civilian, _stalls[slot].Customer, _stalls[slot].Counter, slot, true))
                            {
                                _stalls[slot].Taken = true;
                                break;
                            }
                        }
                    }
                    break;
                case EventKind.FunRun:
                    if (Has(Job.Runner) < 4)
                    {
                        int lane = Random.Range(0, 2);
                        if (Spot(-_half + 1.5f, lane == 0 ? -0.7f : 0.7f, 0.35f, out Vector3 start))
                        {
                            Walk(Job.Runner, CityLook.Civilian, start, start + _along * 10f, lane, true, true);
                        }
                    }
                    else if (Has(Job.Onlooker) < 2)
                    {
                        Onlooker();
                    }
                    break;
                case EventKind.Parade:
                    if (Has(Job.Onlooker) < 2)
                    {
                        Onlooker();
                    }
                    break;
                case EventKind.Fireworks:
                    if (Has(Job.Watcher) < 6 && Spot(Random.Range(-_half + 2f, _half - 4.5f), Random.Range(-_width + 0.8f, _width - 0.8f), 0.45f, out Vector3 place))
                    {
                        Walk(Job.Watcher, CityLook.Civilian, place, _sky, 0, true);
                    }
                    break;
            }
        }

        private bool VendorAt(int slot)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Job == Job.Vendor && _cast[i].Slot == slot && _cast[i].Step != 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Somebody comes to watch from the pavement beside the closed road.
        private void Onlooker()
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 want = Point(Random.Range(-_half + 3f, _half - 3f), side * (_width + 2.4f));
            // At the tape at the nearest, facing in: never a place inside it (a crossing inside the tape is pavement too).
            if (CitySpots.Pavement(want, 3.5f, out Vector3 kerb) && !_director.Cordon.Shuts(kerb) && CitySpots.ClearQuietly(kerb, 0.45f) && CitySpots.Flat(kerb - _centre) > _width * _width)
            {
                Walk(Job.Onlooker, CityLook.Civilian, kerb, _centre + _along * Vector3.Dot(kerb - _centre, _along), 0, false);
            }
        }

        // The column: once they have formed up they march to one end, turn, and march back.
        private bool March(float now)
        {
            bool formed = true;
            bool arrived = true;
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Job == Job.Marcher)
                {
                    formed &= _cast[i].Step != 0;
                    arrived &= _cast[i].Who.Arrived;
                }
            }
            if (!formed || !arrived || now < _nextCue)
            {
                return formed && !arrived;
            }
            _nextCue = now + 6f;   // a breath at each end before they turn
            float end = _towardsB ? _half - 4f : -_half + 4f;
            float back = _towardsB ? -1.6f : 1.6f;
            _towardsB = !_towardsB;
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Job == Job.Marcher)
                {
                    int slot = _cast[i].Slot;
                    Vector3 goal = Point(end + (slot / 2) * back, slot % 2 == 0 ? -0.8f : 0.8f);
                    if (StreetSpots.OnRoad(goal, 0.6f, out goal))
                    {
                        _cast[i].Who.WalkTo(goal);
                        // The walk ends any whole-body clip; the banner arm is the masked layer and stays up.
                        if (_cast[i].Who.PlayingOver == null)
                        {
                            _cast[i].Who.WorkOver(EventPeople.StaffClip, _director.People.Props.Shelf, 0.2f, ClipEnd.Return, BodyMask.RightArm, false);
                        }
                    }
                }
            }
            return true;
        }

        // The film set: a take of twenty seconds or so, a pause, and again.
        private void Takes(float now, Vector3 player)
        {
            if (now < _nextCue)
            {
                return;
            }
            _taking = !_taking;
            _nextCue = now + (_taking ? Random.Range(18f, 26f) : Random.Range(10f, 16f));
            CityProps shelf = _director.People.Props.Shelf;
            bool near = CitySpots.Flat(player - _centre) < 32f * 32f;
            for (int i = 0; i < _count; i++)
            {
                CityPerson who = _cast[i].Who;
                if (_cast[i].Step == 0)
                {
                    continue;
                }
                if (_cast[i].Job == Job.Actor)
                {
                    who.Work(_taking ? (_cast[i].Slot == 0 ? "talk_a" : "talk_b") : "arms_crossed", shelf, 0.4f);
                }
                else if (_cast[i].Job == Job.FilmDirector)
                {
                    if (_taking)
                    {
                        who.Work("arms_crossed", shelf, 0.4f);
                    }
                    else
                    {
                        who.Work("clipboard_write", shelf, 0.4f);
                    }
                }
            }
            if (near)
            {
                Notifications.Show(NotificationType.Info, _taking ? (Random.value < 0.5f ? "reallife_event_film_quiet_a" : "reallife_event_film_quiet_b") : "reallife_event_film_cut", null, 5f);
            }
        }

        // ---------------------------------------------------------------- the player

        public string Prompt(Vector3 player)
        {
            if (_phase != Phase.Running || PlayerHelper.IsUsingVehicle)
            {
                return null;
            }
            switch (_kind)
            {
                case EventKind.Market:
                case EventKind.FarmersMarket:
                    return StallNear(player) >= 0 ? "reallife_event_prompt_snack" : null;
                case EventKind.Protest:
                    return !_thanked && NearJob(player, Job.Organiser, 2.6f) ? "reallife_event_prompt_petition" : null;
                case EventKind.Parade:
                case EventKind.FunRun:
                    return Time.time >= _nextCheer && Beside(player, 7f) && (Has(Job.Runner) > 0 || Has(Job.Marcher) > 0) ? "reallife_event_prompt_cheer" : null;
                case EventKind.FilmShoot:
                    return !_thanked && _taking && Beside(player, 9f) ? "reallife_event_prompt_watch" : null;
                default:
                    return null;
            }
        }

        public void Interact(float now, Vector3 player)
        {
            string prompt = Prompt(player);
            if (prompt == null)
            {
                return;
            }
            switch (_kind)
            {
                case EventKind.Market:
                case EventKind.FarmersMarket:
                    Buy(now, StallNear(player));
                    break;
                case EventKind.Protest:
                    Thank(Random.value < 0.5f ? "reallife_event_petition_signed_a" : "reallife_event_petition_signed_b", ModState.Helped);
                    break;
                case EventKind.Parade:
                case EventKind.FunRun:
                    _nextCheer = now + 25f;
                    Wave(player);
                    if (!_thanked)
                    {
                        Thank(_kind == EventKind.FunRun ? "reallife_event_cheer_run" : "reallife_event_cheer_parade", ModState.MoodUp);
                    }
                    else
                    {
                        Notifications.Show(NotificationType.Info, "reallife_event_cheer_again", null, 4f);
                    }
                    break;
                case EventKind.FilmShoot:
                    Thank("reallife_event_film_watched", ModState.MoodUp);
                    break;
            }
        }

        public bool Report(float now)
        {
            return false;   // nothing here is an emergency
        }

        private void Thank(string key, string mood)
        {
            _thanked = true;
            ModState.AddModifier(mood);
            Notifications.Show(NotificationType.Success, key, null, 7f);
        }

        private void Buy(float now, int slot)
        {
            if (slot < 0)
            {
                return;
            }
            var save = SaveGameManager.Current;
            if (save == null || save.Money < SnackPrice)
            {
                Notifications.Show(NotificationType.Warning, "reallife_event_snack_broke",
                    new System.Collections.Generic.Dictionary<string, string>(1) { { "price", ((float)SnackPrice).ToCurrencyFormat() } }, 5f);
                return;
            }
            try
            {
                GameManager.ChangeMoneySafe(-SnackPrice, new TransactionInfo("reallife_transaction_snack"), null, null, force: true);
                EnergyHelper.GenerateEnergy(SnackEnergy);
            }
            catch (System.Exception e)
            {
                EventDirector.Logger?.Error(e);
                return;
            }
            _stalls[slot].ServeAt = now;
            Crime.ShowAmount(NotificationType.Success, Random.value < 0.5f ? "reallife_event_snack_bought_a" : "reallife_event_snack_bought_b", SnackPrice, 5f);
            if (!_thanked)
            {
                _thanked = true;
                ModState.AddModifier(ModState.MoodUp);
            }
        }

        // The stall whose customer side the player stands at, with its vendor in and not serving; -1 if none.
        private int StallNear(Vector3 player)
        {
            for (int slot = 0; slot < _stallCount; slot++)
            {
                if (CitySpots.Flat(player - _stalls[slot].Customer) < 1.9f * 1.9f && VendorAt(slot) && _stalls[slot].ServeAt <= 0f)
                {
                    return slot;
                }
            }
            return -1;
        }

        private bool NearJob(Vector3 player, Job job, float within)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Job == job && _cast[i].Step != 0 && CitySpots.Flat(player - _cast[i].Who.Position) < within * within)
                {
                    return true;
                }
            }
            return false;
        }

        // Within so many metres of the closed stretch's sides.
        private bool Beside(Vector3 player, float within)
        {
            Vector3 offset = player - _centre;
            return Mathf.Abs(Vector3.Dot(offset, _along)) < _half + 2f && Mathf.Abs(Vector3.Dot(offset, _right)) < _width + within;
        }

        // The nearest runner or marcher lifts a hand to whoever cheered.
        private void Wave(Vector3 player)
        {
            int best = -1;
            float nearest = 14f * 14f;
            for (int i = 0; i < _count; i++)
            {
                float sqr = CitySpots.Flat(_cast[i].Who.Position - player);
                if ((_cast[i].Job == Job.Runner || _cast[i].Job == Job.Marcher) && _cast[i].Step != 0 && sqr < nearest)
                {
                    nearest = sqr;
                    best = i;
                }
            }
            if (best >= 0)
            {
                // A marcher's right arm has the banner: they wave with the left; a runner with whatever the clip uses.
                _cast[best].Who.WorkOver("wave_hello", _director.People.Props.Shelf, 0.2f, ClipEnd.Return, _cast[best].Job == Job.Marcher ? BodyMask.LeftArm : BodyMask.Upper);
            }
        }

        // ---------------------------------------------------------------- packing up

        private void PackUp(float now, bool storm)
        {
            _phase = Phase.TakeDown;
            _phaseUntil = now + 9f;
            if (!EventCalendar.Quiet(_kind))
            {
                Notifications.Show(NotificationType.Info, storm ? "reallife_event_called_off" : Closing(), null, 8f);
            }
            CityProps shelf = _director.People.Props.Shelf;
            _director.Sky.StopAll();
            for (int i = _count - 1; i >= 0; i--)
            {
                Job job = _cast[i].Job;
                bool staff = job == Job.Marshal || job == Job.Vendor || job == Job.Operator || job == Job.FilmDirector || job == Job.Assistant || job == Job.Organiser;
                if (!staff || _cast[i].Step == 0)
                {
                    Drop(i, true);   // the crowd simply goes home
                }
                else if (job != Job.Marshal)
                {
                    _cast[i].Who.Work("kneel_work", shelf, 0.4f);   // down at their boxes, packing
                }
            }
            EventDirector.Logger?.Info($"Street event: {_kind} packing up ({(storm ? "a thunderstorm" : _test ? "the test ran its time" : "closing time or nothing left of it")}).");
        }

        public void End(bool trafficRunning)
        {
            _phase = Phase.Done;
            EventPeople people = _director.People;
            for (int i = 0; i < _count; i++)
            {
                _cast[i] = default;
            }
            _count = 0;
            people.ReleaseAll();
            for (int i = 0; i < _director.Officers.Length; i++)
            {
                _director.Officers[i].Exit();
            }
            _officers = 0;
            for (int i = 0; i < _dressing.Count; i++)
            {
                _director.Props.Return(_dressing[i]);
            }
            _dressing.Clear();
            _stallCount = 0;
            _director.Sky.StopAll();
            _director.Cordon.Open();
        }

        private string Open()
        {
            bool second = Random.value < 0.5f;
            switch (_kind)
            {
                case EventKind.Market: return second ? "reallife_event_open_market_b" : "reallife_event_open_market_a";
                case EventKind.FarmersMarket: return second ? "reallife_event_open_farmers_b" : "reallife_event_open_farmers_a";
                case EventKind.Parade: return second ? "reallife_event_open_parade_b" : "reallife_event_open_parade_a";
                case EventKind.Protest: return second ? "reallife_event_open_protest_b" : "reallife_event_open_protest_a";
                case EventKind.FunRun: return second ? "reallife_event_open_run_b" : "reallife_event_open_run_a";
                case EventKind.FilmShoot: return second ? "reallife_event_open_film_b" : "reallife_event_open_film_a";
                default: return second ? "reallife_event_open_fireworks_b" : "reallife_event_open_fireworks_a";
            }
        }

        private string Closing()
        {
            switch (_kind)
            {
                case EventKind.Market: return "reallife_event_close_market";
                case EventKind.FarmersMarket: return "reallife_event_close_farmers";
                case EventKind.Parade: return "reallife_event_close_parade";
                case EventKind.Protest: return "reallife_event_close_protest";
                case EventKind.FunRun: return "reallife_event_close_run";
                case EventKind.FilmShoot: return "reallife_event_close_film";
                default: return "reallife_event_close_fireworks";
            }
        }
    }
}
