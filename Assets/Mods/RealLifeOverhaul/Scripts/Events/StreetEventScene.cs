using System.Collections.Generic;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// A street event on a stretch of road closed with barriers and tape, marshals at its ends: market stalls with vendors
    /// and customers, a march with banners, a protest watched by two officers, a fun run, a film shoot or fireworks. Eight
    /// people at most and two officers, stepped four times a second and mostly once a second. Nobody appears in view: it is
    /// set up out of sight, the crowd walks in and off, and the props go once nobody is looking. Every way out, a fault
    /// included, ends in <see cref="End"/>, which gives everything back and reopens the road.
    /// </summary>
    internal sealed partial class StreetEventScene : IEventScene
    {
        private enum Phase { SetUp, Running, TakeDown, Clearing, Done }
        private enum Job { Marshal, Vendor, Browser, Marcher, Runner, Onlooker, Protester, Organiser, Operator, FilmDirector, Assistant, Actor, Watcher }

        private const int MaxCast = CityPeople.MaxPeople;
        private const int MaxStalls = 3;
        private const float OperatorBehind = 0.75f;      // metres from film_camera's pivot back to camera_operate's feet, on the lens's line
        private const float OperatorTolerance = 0.08f;   // metres off that place at which the clip still meets the viewfinder
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

        public string Label => EventCalendar.Name(_kind);
        public Vector3 Focus => _centre;
        public float Reach => 260f;
        /// <summary>Why the last Stage put nothing on the street, for the log.</summary>
        public string Refusal => _refusal;
        /// <summary>True if the last Stage got as far as closing the road before it gave up (it moved traffic and took people).</summary>
        public bool Costly { get; private set; }

        /// <summary>
        /// Closes a stretch of road near a place and dresses it. False, the street left as it was, with no plain road there,
        /// the place in view outside a test, or too little room for what the event needs.
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
                    // The operator's place is exact (FilmSet): the agent is sent as far past it as it stops short, a few times
                    // (Slot counts them).
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
