using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using UnityEngine;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>
    /// The showroom's floor while the player is in one of their showrooms: the hour's visitors come in, look the cars over, are
    /// seen to and leave, showing what DealerRules.Hour worked out and changing nothing. Off elsewhere; inside, a time comparison
    /// a frame and four times a second a pass over at most four people. Local only in Going Public.
    /// </summary>
    public sealed class ShowroomFloor : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("economy_dealer", "ShowroomFloor", typeof(ShowroomFloor));

        private const float Beat = 0.25f;          // seconds between two looks at the people on the floor
        private const float Side = 0.75f;          // metres from a car's side a visitor stands
        private const float HalfWidth = 1f;        // metres: half a car's width, near enough for where to stand
        private const float HalfLength = 2.4f;     // ...and half its length
        private const float Reach = 1.1f;          // metres from a visitor the salesperson stands to talk

        public static IModLogger Logger;
        public static ShowroomFloor Instance { get; private set; }

        private enum Step { Coming, Looking, Waiting, Talking, Leaving }

        private sealed class Visitor
        {
            public readonly ActorRole Role = new ActorRole();
            public FloorVisit Visit;
            public int Index;            // the visit's place in the hour's plan
            public int Hour;
            public int At;               // which of the visit's cars they are at (0 to Cars - 1)
            public Step Step;
            public float Until;
            public bool Talked;

            public bool Out => Role.Present;
        }

        private readonly Visitor[] _visitors = { new Visitor(), new Visitor(), new Visitor() };
        private readonly ActorRole _sales = new ActorRole();
        private readonly List<FloorVisit> _plan = new List<FloorVisit>(FloorCatalogue.ShownMost);
        private readonly List<int> _offerCars = new List<int>(4);
        private readonly List<bool> _away = new List<bool>(16);   // by the lot's places: in the workshop or out on a test drive, not looked over

        private DealerYard _yard;
        private DealerYard _refused;     // a showroom whose floor could not be shown (no door to stand at), until the player leaves it
        private int _hour = -1;          // the hour the plan is of (day x 24 + hour)
        private int _next;               // the next visit of the plan to come in
        private bool _staffed;           // a salesperson is on shift this hour
        private Vector3 _door, _outside, _home;
        private bool _hasDoor, _hasHome;
        private float _nextBeat;
        private Visitor _talking;        // the visitor the salesperson is with, or going to
        private float _salesUntil;
        private bool _salesGoing;
        private int _shown, _skipped;    // visitors put on the floor and left out this hour, for the self-check

        /// <summary>The showroom the player stands in, while the floor is shown; null otherwise.</summary>
        public DealerYard Yard => _yard;

        private void Awake()
        {
            Instance = this;
            enabled = false;   // nobody to look after until the player walks into one of their showrooms
        }

        private void OnDestroy()
        {
            Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>The showroom the player stands in (null: none of theirs), told once a second and on entering; nothing unless it changed.</summary>
        public void Stand(DealerYard y)
        {
            if (y == _yard || (y != null && y == _refused))
            {
                return;
            }
            Clear();
            if (y == null)
            {
                _refused = null;
                return;
            }
            if (!Settings.EconomyDealerFloor)
            {
                return;
            }
            try
            {
                Begin(y);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Clear();
            }
        }

        private void Begin(DealerYard y)
        {
            DealerDirector director = DealerDirector.Instance;
            if (director == null || director.State == null || !y.Laid)
            {
                return;
            }
            _yard = y;
            ShowroomGame.FloorMiddle(y, out Vector3 middle);
            _hasDoor = ShopFloor.Door(middle, out _door, out _outside);
            _hasHome = ShowroomGame.Desk(middle, out _home) || ShopFloor.RandomSpot(out _home);
            if (!_hasDoor)
            {
                Logger?.Warn("Car Showroom: no door of '" + y.Name + "' could be stood at, so no visitors are shown on its floor.");
                _yard = null;
                _refused = y;
                return;
            }
            Replan(true);
            enabled = true;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Car Showroom check (floor, '" + y.Name + "'): " + _plan.Count + " visitor(s) of this hour to be seen, " + (_staffed ? "a salesperson on the floor" : "nobody on shift")
                             + ", the door " + (_hasDoor ? "found" : "not found") + ", the desk " + (_hasHome ? "found" : "not found") + ": PASS.");
            }
        }

        /// <summary>A new hour of the showroom the player stands in has been worked: what it brought is shown.</summary>
        public void NewHour(DealerYard y)
        {
            if (y != _yard || _yard == null)
            {
                return;
            }
            try
            {
                Replan(false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>A car of the floor has moved to another bay: whoever is looking at one goes to it again.</summary>
        public void CarsMoved()
        {
            for (int i = 0; i < _visitors.Length; i++)
            {
                Visitor v = _visitors[i];
                if (v.Out && (v.Step == Step.Looking || v.Step == Step.Coming))
                {
                    GoTo(v, Time.time);
                }
            }
        }

        // Everyone off the floor, and the component off.
        private void Clear()
        {
            for (int i = 0; i < _visitors.Length; i++)
            {
                _visitors[i].Role.Exit();
                _visitors[i].Step = Step.Leaving;
            }
            _sales.Exit();
            _talking = null;
            _salesGoing = false;
            _yard = null;
            _plan.Clear();
            _hour = -1;
            _next = 0;
            enabled = false;
        }

        // The lot's cars not on show at an hour (in the workshop or out on a test drive), by their places, into the kept list.
        private List<bool> Away(int hour)
        {
            _away.Clear();
            for (int i = 0; _yard != null && i < _yard.Cars.Count; i++)
            {
                _away.Add(_yard.Cars[i].ShopUntil > hour || _yard.Cars[i].OutUntil > hour);
            }
            return _away;
        }

        // The hour's plan: on walking in, the visits under way are put straight onto the floor; on a new hour, the visitors
        // still in finish what they are doing.
        private void Replan(bool walkingIn)
        {
            DealerDirector director = DealerDirector.Instance;
            if (director == null || director.State == null || _yard == null)
            {
                return;
            }
            int hour = ShowroomGame.Hour;
            director.FloorHour(_yard, hour, out int visitors, out int served, out bool staffed, _offerCars);
            if (_hour >= 0 && Meta.Dev.Verbose)
            {
                Logger?.Info("Car Showroom check (floor, hour " + _hour % 24 + "): " + _shown + " of " + _plan.Count + " visitor(s) planned were seen, " + _skipped
                             + " left out for want of room or of people: " + (_shown + _skipped <= _plan.Count ? "PASS" : "FAIL") + ".");
            }
            _hour = hour;
            _shown = _skipped = 0;
            ShowroomFloorRules.Plan(director.State.Seed, hour, _yard.Place, visitors, served, _offerCars, _yard.Cars.Count, _plan, Away(hour));
            _next = 0;
            if (walkingIn)
            {
                // What came before the player did is past; a visit under way is on the floor already, at its first car.
                int minute = ShowroomGame.Minute;
                float now = Time.time;
                for (int i = 0; i < _plan.Count; i++)
                {
                    int phase = ShowroomFloorRules.Phase(_plan[i].Minute, minute);
                    if (phase == 0)
                    {
                        break;
                    }
                    _next = i + 1;
                    if (phase == 1 && !Arrive(i, now, true))
                    {
                        _skipped++;
                    }
                }
            }
            Staff(staffed);
        }

        // The salesperson on the floor while somebody is on shift, by the desk.
        private void Staff(bool staffed)
        {
            _staffed = staffed;
            if (!staffed)
            {
                _sales.Exit();
                _talking = null;
                _salesGoing = false;
                return;
            }
            if (!_sales.Present && _hasHome && _sales.EnterPresence(_home, ActorLook.Civilian, 1.5f, false))
            {
                _sales.Stand(ShowroomGame.FloorMiddleOf(_yard));
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("Economy: ShowroomFloor.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
            if (now < _nextBeat)
            {
                return;
            }
            _nextBeat = now + Beat;
            if (_yard == null || !Settings.EconomyDealerFloor)
            {
                Clear();
                return;
            }
            Admit(now);
            for (int i = 0; i < _visitors.Length; i++)
            {
                Tend(_visitors[i], now);
            }
            Salesperson(now);
        }

        // Visitors whose minute has come, while there is room on the floor; one who waited too long for room is left out.
        private void Admit(float now)
        {
            if (_next >= _plan.Count || ShowroomGame.Hour != _hour)
            {
                return;   // the next hour's plan comes with NewHour, once the showroom's hour has been worked
            }
            int minute = ShowroomGame.Minute;
            FloorVisit v = _plan[_next];
            if (minute < v.Minute)
            {
                return;
            }
            if (minute - v.Minute > FloorCatalogue.Late)
            {
                _skipped++;
                _next++;
                return;
            }
            if (Free() == null)
            {
                return;   // waits for room
            }
            if (!Arrive(_next, now, false))
            {
                _skipped++;   // room, but nobody to be had from the people pool
            }
            _next++;
        }

        private Visitor Free()
        {
            for (int i = 0; i < _visitors.Length; i++)
            {
                if (!_visitors[i].Out)
                {
                    return _visitors[i];
                }
            }
            return null;
        }

        // A visit of the plan onto the floor: in at the door, or (walking in on one under way) at their car already.
        private bool Arrive(int index, float now, bool already)
        {
            Visitor v = Free();
            if (v == null)
            {
                return false;
            }
            FloorVisit visit = _plan[index];
            Vector3 at = _door;
            if (already && ShowroomGame.BesideCar(_yard, visit.First, index, Side, HalfWidth, HalfLength, out Vector3 beside, out _))
            {
                at = beside;
            }
            if (!v.Role.EnterPresence(at, ActorLook.Civilian, 1.5f, false))
            {
                return false;
            }
            v.Visit = visit;
            v.Index = index;
            v.Hour = _hour;
            v.At = 0;
            v.Talked = false;
            _shown++;
            GoTo(v, now);
            return true;
        }

        // To the car they are at in their visit: beside it, facing it.
        private void GoTo(Visitor v, float now)
        {
            int car = v.Visit.Car(v.At);
            if (!ShowroomGame.BesideCar(_yard, car, v.Index + v.At, Side, HalfWidth, HalfLength, out Vector3 spot, out Vector3 middle))
            {
                Leave(v);
                return;
            }
            v.Step = Step.Coming;
            v.Role.Quiet();
            v.Role.Go(spot, Pace.Walk, middle, 25f);
            v.Until = now + 30f;
        }

        private void Tend(Visitor v, float now)
        {
            v.Role.Tick(now);
            if (!v.Out)
            {
                if (_talking == v)
                {
                    _talking = null;
                }
                return;
            }
            switch (v.Step)
            {
                case Step.Coming:
                    if (v.Role.Settled || now > v.Until)
                    {
                        v.Step = Step.Looking;
                        v.Until = now + ShowroomFloorRules.LookSeconds(v.Hour, v.Index, v.At);
                        v.Role.Repeat(AnimationType.PressAction, AnimationType.IDontThinkSo, 5f, 9f);
                        if (v.Visit.Served && !v.Talked && _talking == null && _sales.Present)
                        {
                            Call(v, now);
                        }
                    }
                    break;

                case Step.Looking:
                    if (v.Visit.Served && !v.Talked && _talking == null && _sales.Present)
                    {
                        Call(v, now);
                    }
                    if (now >= v.Until && _talking != v)
                    {
                        Next(v, now);
                    }
                    break;

                case Step.Waiting:
                case Step.Talking:
                    if (_talking != v)
                    {
                        Next(v, now);   // the salesperson was taken away: they carry on
                    }
                    break;
            }
        }

        // On to the next car of their visit, or out.
        private void Next(Visitor v, float now)
        {
            v.Role.Quiet();
            if (v.At + 1 < v.Visit.Cars)
            {
                v.At++;
                GoTo(v, now);
                return;
            }
            if (!v.Visit.Served)
            {
                v.Role.Emote(CharacterEmojiName.CustomerCantFindEmployeeOrRegister, 3f);   // the game's own face for a customer nobody saw to
            }
            Leave(v);
        }

        private void Leave(Visitor v)
        {
            v.Step = Step.Leaving;
            v.Role.Quiet();
            v.Role.WalkOff(_outside, Pace.Walk, 40f);
            if (_talking == v)
            {
                _talking = null;
            }
        }

        private void Call(Visitor v, float now)
        {
            if (!ShowroomGame.Between(v.Role.Position, ShowroomGame.CarMiddle(_yard, v.Visit.Car(v.At)), Reach, out Vector3 spot))
            {
                return;
            }
            _talking = v;
            _salesGoing = true;
            _salesUntil = now + 25f;
            v.Step = Step.Waiting;
            _sales.Drop();
            _sales.Go(spot, Pace.Walk, v.Role.Position, 25f);
        }

        private void Salesperson(float now)
        {
            _sales.Tick(now);
            if (!_sales.Present)
            {
                if (_talking != null)
                {
                    _talking.Talked = true;
                    _talking = null;
                }
                return;
            }
            Visitor v = _talking;
            if (v == null)
            {
                return;
            }
            if (!v.Out)
            {
                Home();
                return;
            }
            if (_salesGoing && (_sales.Settled || now > _salesUntil))
            {
                _salesGoing = false;
                if (!_sales.Settled)
                {
                    _sales.Stand(v.Role.Position);   // as far as they got
                }
                _salesUntil = now + FloorCatalogue.TalkSeconds;
                _sales.Face(v.Role.Position);
                _sales.Work(SceneClips.TakeNotes);
                v.Step = Step.Talking;
                v.Role.Face(_sales.Position);
                return;
            }
            if (!_salesGoing && now >= _salesUntil)
            {
                v.Talked = true;
                v.Role.Emote(v.Visit.Offers ? CharacterEmojiName.CommonGreenHappy : CharacterEmojiName.CustomerGoodService, 3f);
                v.Step = Step.Looking;
                v.Until = now + 4f;
                Home();
            }
        }

        private void Home()
        {
            _talking = null;
            _salesGoing = false;
            _sales.Drop();
            if (_hasHome)
            {
                _sales.Go(_home, Pace.Walk, ShowroomGame.FloorMiddleOf(_yard), 25f);
            }
        }

        /// <summary>For Options > Tests: a busy hour's visitors come in now; only the floor is shown, no figure changes.</summary>
        public void TestFloor()
        {
            const string test = "Car Showroom floor";
            try
            {
                string why = TestStart();
                if (why != null)
                {
                    TestRefusals.TellKey(Logger, test, why);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private string TestStart()
        {
            DealerDirector director = DealerDirector.Instance;
            DealerYard y = director != null ? director.YardInside() : null;
            if (y == null)
            {
                return "reallife_showroom_test_not_inside";
            }
            if (!Settings.EconomyDealerFloor)
            {
                return "reallife_showroom_test_off";
            }
            if (y.Cars.Count == 0)
            {
                return "reallife_dealer_test_no_cars";
            }
            if (_yard != y)
            {
                Stand(y);
            }
            if (_yard == null)
            {
                return y == _refused ? "reallife_showroom_test_no_door" : "reallife_showroom_test_not_inside";
            }
            director.FloorOffers(_yard, _hour, _offerCars);
            ShowroomFloorRules.Plan(director.State.Seed, _hour + 7919, _yard.Place, FloorCatalogue.ShownMost, FloorCatalogue.ShownMost / 2, _offerCars, _yard.Cars.Count,
                _plan, Away(ShowroomGame.Hour));
            for (int i = 0; i < _plan.Count; i++)
            {
                FloorVisit v = _plan[i];
                v.Minute = ShowroomGame.Minute;   // every one of them due now: they come in as room is made
                _plan[i] = v;
            }
            _next = 0;
            _shown = _skipped = 0;
            Staff(true);
            enabled = true;
            Logger?.Info("Car Showroom: a test hour of " + _plan.Count + " visitor(s) on the floor of '" + _yard.Name + "' (" + _yard.Cars.Count
                         + " car(s)); nothing of the showroom's figures is changed.");
            return null;
        }
    }
}
