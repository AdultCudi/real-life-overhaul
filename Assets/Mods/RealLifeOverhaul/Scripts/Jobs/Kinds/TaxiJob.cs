using BigAmbitions.Characters;
using Characters.EmojiSystem;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Ride-hail driver, in the player's own car or a van hired for the day (JobRental), with
    /// a roof sign on it for the length of the shift. A fare is called in: a passenger waits on
    /// the pavement outside a building, waves when the car comes into the street, walks to a
    /// rear door once it has stopped beside them and gets in. They are driven to another
    /// address, get out on the kerb side, say what they thought of the ride and walk to the
    /// door. The fare is by distance; the tip is by how the ride felt (hard braking, bumps,
    /// speeding, being pulled over, a crash) and whether it was quick. Then the next call comes.
    ///
    /// Uses: the game's walk for the passenger, JobClip.Hail, Thank and Annoyed, and for getting
    /// in and out JobClip.CarEnter and CarExit at the door: with the clips, the passenger opens
    /// the door and gets in before they are taken off the street; without them it is the sound
    /// of the door alone (the game has no such animation, and none is faked). Either way they
    /// are only ever on their feet at a place beside the car that was found clear, never in it.
    ///
    /// What counts as a hard stop is worked out from the car being driven, each ride: the most
    /// its brakes and its engine can do (never more than the tyres can hold), of which a
    /// passenger puts up with seven tenths. A gentle car is therefore judged gently.
    ///
    /// IN THE GAME'S OWN INTERFACE. Nothing in this job is done on foot with a key, so there is no panel over a place:
    /// the shift's figures are on the HUD in a copy of the game's job panel (<see cref="ShiftFigures"/>), the fare to
    /// reach or the ride's comfort and the missed fares of the two allowed on its second line. There is no "Let time
    /// pass": the car would have to be moved by the clock. The driver works in their own clothes.
    /// </summary>
    public sealed class TaxiJob : Job
    {
        private enum Step { ToPickup, Boarding, Riding, Between }

        private const float PickupMin = 70f;            // metres from the player a fare may wait
        private const float PickupMax = 380f;
        private const float TripMin = 280f;
        private const float TripMax = 950f;
        private const float LaneWithin = 13f;           // a door only counts if a car can pull up this near it
        private const float StopWithin = 17f;           // the car has "arrived" when stopped this near the door front
        private const float StoppedSpeed = 0.7f;        // m/s
        private const float StoppedFor = 0.5f;
        private const float PersonAppears = 130f;       // the passenger is put on the street when the player is this near
        private const float HailWithin = 34f;
        private const float BoardAllow = 30f;
        private const float AwayAllow = 25f;            // seconds the driver may leave a passenger sitting in the car
        private const float HarshG = 9.5f;              // m/s2 of braking or swerving, held over two ticks: an emergency stop. The most it is ever set to.
        private const float HarshLeast = 4.5f;          // ...and the least: nobody minds less than this
        private const float BumpOver = 5.5f;            // this much more in a single tick is a jolt: a kerb, a nudge
        private const float Grip = 9f;                  // m/s2 the tyres hold on a dry road
        private const float Tolerated = 0.7f;           // the share of what the car can do that a passenger puts up with
        private const float WheelRadius = 0.33f;
        private const float LaunchSpeed = 5f;           // m/s at which the engine's pull is taken
        private const string SignProp = "taxi_sign";
        private const float DoorExact = 0.35f;          // metres from the place the getting-in clip is made for within which it is played
        private const float SpeedingAbove = 26f;        // m/s: about 94 km/h, with a passenger aboard
        /// <summary>Missed fares that end the shift.</summary>
        public const int MissesAllowed = 2;

        private static readonly string[] Greetings =
        {
            "reallife_job_taxi_greet_1", "reallife_job_taxi_greet_2", "reallife_job_taxi_greet_3", "reallife_job_taxi_greet_4"
        };
        private static readonly string[] Good =
        {
            "reallife_job_taxi_good_1", "reallife_job_taxi_good_2", "reallife_job_taxi_good_3", "reallife_job_taxi_good_4"
        };
        private static readonly string[] Fair = { "reallife_job_taxi_fair_1", "reallife_job_taxi_fair_2", "reallife_job_taxi_fair_3" };
        private static readonly string[] Bad = { "reallife_job_taxi_bad_1", "reallife_job_taxi_bad_2", "reallife_job_taxi_bad_3" };
        private static readonly string[] Braking = { "reallife_job_taxi_brake_1", "reallife_job_taxi_brake_2", "reallife_job_taxi_brake_3" };
        private static readonly string[] Crashes = { "reallife_job_taxi_crash_1", "reallife_job_taxi_crash_2", "reallife_job_taxi_crash_3" };
        private static readonly string[] Police = { "reallife_job_taxi_police_1", "reallife_job_taxi_police_2" };
        private static readonly string[] Fast = { "reallife_job_taxi_fast_1", "reallife_job_taxi_fast_2" };
        private static readonly string[] GaveUp = { "reallife_job_taxi_gaveup_1", "reallife_job_taxi_gaveup_2" };
        private static readonly string[] Hold = { "reallife_job_taxi_hold_1", "reallife_job_taxi_hold_2" };

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Taxi);

        private Step _step;
        private float _stepFrom;
        private float _stepEnd;
        private DoorSpot _pickup;
        private DoorSpot _drop;
        private float _metres;
        private StreetActor _passenger;     // on the street: waiting, walking to the car, or just out of it
        private StreetActor _rider;         // in the car: the same person who got in gets out
        private StreetActor _leaving;       // the last passenger, walking to their door
        private float _leaveAt;
        private float _leavingUntil;
        private bool _leavingWalks;
        private Vector3 _leaveTo;
        private float _lookingSince;    // since when a way out of the car has been looked for at the drop-off
        private CarController _car;         // the car the passenger is in
        private float _stoppedSince = -1f;
        private Vector3 _triedAt;           // where the car stood when no door could be reached
        private bool _tried;
        private Vector3 _boardSpot;
        private Vector3 _boardCarAt;
        private float _nextHail;
        private bool _hailed;
        private bool _hailing;              // the passenger's arm is up (a clip that goes on until it is stopped)
        private bool _doorClip;             // they board at the place the getting-in clip is made for, and there is such a clip
        private bool _doorRight;            // ...at a door on the car's right side
        private Vector3 _doorInwards;       // ...facing this way: square to the car
        private bool _onFootShown;
        private float _pickupBy;         // when the waiting passenger gives up, whatever happens at the door

        private float _harsh = HarshG;      // for the car of this ride
        private float _bump = HarshG + BumpOver;
        private bool _hired;                // the shift is driven in a hired van
        private Transform _sign;            // on the roof of the car being worked with
        private CarController _signOn;
        private float _nextSign;
        private float _enterUntil = -1f;    // the passenger is getting in: off the street when this passes
        private JobClip _farewell;
        private float _farewellAt = -1f;    // when the passenger, out of the car, says goodbye

        private float _comfort;
        private int _comfortShown = -1;
        private float _rideFrom;
        private Vector3 _lastVelocity;
        private bool _wasHarsh;
        private float _lastTick;
        private float _lastJolt = -100f;
        private float _awaySince = -1f;
        private bool _speedingSaid;
        private int _crashes;
        private int _stops;

        private int _fares;
        private int _misses;
        private int _noCalls;
        private int _line = -1;
        private string _pickupLabel;
        private string _dropLabel;

        // The game's own interface.
        private ShiftFigures _figures;
        private int _cardKey = int.MinValue;

        public override JobDef Def => _def;

        /// <summary>Giving up with someone waving at the kerb, or sitting in the back, counts against the driver.</summary>
        public override bool QuitIsFailure => _step == Step.Boarding || _step == Step.Riding;

        protected override bool OnBegin(float now)
        {
            _pickupLabel = JobText.T("reallife_job_taxi_marker_pickup");
            _dropLabel = JobText.T("reallife_job_taxi_marker_drop");
            if (!NextFare(now))
            {
                Refusal = "reallife_job_no_here";
                return false;
            }
            // Last of all, once nothing else can refuse: the player's own car, or a van hired for the day.
            CarController car = Ctx.Director.CarFor(Ctx.Test, out string noCar);
            if (car == null)
            {
                Refusal = noCar ?? "reallife_job_no_car";
                return false;
            }
            if (Ctx.Perk(CareerPerk.Premium) && !CareerDirector.IsLuxury(car))
            {
                Refusal = "reallife_career_no_luxury";   // premium work is done in a Luxury-kind car (Driving/CarCatalogue), and a hired van is not one
                return false;
            }
            _hired = ReferenceEquals(Ctx.Rental.Car, car);
            Sign(car);
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_taxi_card_done");
            Figures(now);
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: taxi: shown in the game's own interface: job panel " + (_figures.Shown ? "copied" : "NOT copied (the shift's figures are not shown)")
                                 + ", " + MissesAllowed + " missed fares end the shift, no clock offered (the car would have to be moved).");
            }
            return true;
        }

        // The shift's figures on the card: each line is written only when what it says changes.
        private void Figures(float now)
        {
            _figures.Objective(Objective);
            _figures.Units(_fares, 0);
            int value = _step == Step.ToPickup ? Mathf.Max(0, Mathf.CeilToInt(_stepEnd - now)) : _step == Step.Riding ? Mathf.Max(0, _comfortShown) : -1;
            int key = (int)_step | ((value + 1) << 3) | (_misses << 20);
            if (key != _cardKey)
            {
                _cardKey = key;
                string missed = JobText.T("reallife_job_taxi_card_missed", "misses", _misses.ToString(), "most", MissesAllowed.ToString());
                _figures.Limit(_step == Step.ToPickup ? JobText.T("reallife_job_taxi_card_reach", "time", ShiftFigures.Clock(value)) + ", " + missed
                    : _step == Step.Riding ? JobText.T("reallife_job_taxi_card_comfort", "percent", value.ToString()) + ", " + missed : missed,
                    _misses >= MissesAllowed - 1 ? WorldPanel.Yellow() : WorldPanel.White());
            }
            _figures.Money(Shift.Total, Shift.Tips);
            _figures.Time(-1);
        }

        // ---------------------------------------------------------------- the roof sign

        private static readonly RaycastHit[] RoofHits = new RaycastHit[12];

        // The sign stands on the roof of the car the shift is driven in: on the car's own body, found with one ray
        // from above. Moved only when the player changes cars; taken off at the end of the shift.
        private void Sign(CarController car)
        {
            if (car == null || ReferenceEquals(car, _signOn))
            {
                return;
            }
            Unsign();
            int mask = TrafficTools.VehicleMask;
            Transform body = car.transform;
            JobSpots.CarFrame(car, out Vector3 centre, out _, out _, out _);
            int hits = mask != 0 ? Physics.RaycastNonAlloc(centre + body.up * 4f, -body.up, RoofHits, 5f, mask, QueryTriggerInteraction.Ignore) : 0;
            float nearest = float.MaxValue;
            Vector3 roof = default;
            for (int i = 0; i < hits; i++)
            {
                if (RoofHits[i].collider != null && RoofHits[i].collider.transform.IsChildOf(body) && RoofHits[i].distance < nearest)
                {
                    nearest = RoofHits[i].distance;
                    roof = RoofHits[i].point;
                }
                RoofHits[i] = default;
            }
            if (nearest > 4.5f)
            {
                return;   // nothing of the car's under the ray: no sign rather than one in the air
            }
            _sign = Ctx.Props.Take(SignProp);
            if (_sign == null)
            {
                return;
            }
            _sign.SetParent(body, false);
            _sign.localPosition = body.InverseTransformPoint(roof);
            _sign.localRotation = Quaternion.identity;
            Vector3 scale = body.lossyScale;
            _sign.localScale = new Vector3(1f / Mathf.Max(0.01f, scale.x), 1f / Mathf.Max(0.01f, scale.y), 1f / Mathf.Max(0.01f, scale.z));
            _signOn = car;
        }

        private void Unsign()
        {
            if (_sign != null)
            {
                Ctx.Props.Return(_sign);   // back under the jobs' own root, at scale one, switched off
            }
            _sign = null;
            _signOn = null;
        }

        // What this car can do, and so what a passenger in it calls a hard stop.
        private void Tune(CarController car)
        {
            _harsh = HarshG;
            try
            {
                Rigidbody body = Ctx.Body;
                var vehicle = car != null ? car.vehicleController : null;
                if (vehicle != null && body != null && body.mass > 100f)
                {
                    float brakes = vehicle.brakes != null ? 4f * vehicle.brakes.maxTorque / (WheelRadius * body.mass) : Grip;
                    float engine = vehicle.powertrain != null && vehicle.powertrain.engine != null
                        ? vehicle.powertrain.engine.maxPower * 1000f / (body.mass * LaunchSpeed) : Grip;
                    float most = Mathf.Min(Grip, Mathf.Max(brakes, engine));
                    _harsh = Mathf.Clamp(most * Tolerated, HarshLeast, HarshG);
                    Ctx.Logger?.Info("Jobs: taxi comfort for this car: brakes " + brakes.ToString("F1") + " m/s2, engine " + engine.ToString("F1")
                                     + " m/s2 at " + LaunchSpeed.ToString("F0") + " m/s, " + body.mass.ToString("F0") + " kg; a hard stop is " + _harsh.ToString("F1") + " m/s2.");
                }
            }
            catch (System.Exception e)
            {
                Ctx.Logger?.Error(e);
            }
            if (Ctx.Perk(CareerPerk.Premium))
            {
                _harsh = Mathf.Max(HarshLeast * 0.8f, _harsh * CareerCatalogue.PremiumHarsh);   // a premium passenger minds sooner
            }
            _bump = _harsh + BumpOver;
        }

        // ---------------------------------------------------------------- a new fare

        private bool NextFare(float now)
        {
            Vector3 from = Ctx.PlayerPosition;
            if (!JobSpots.RandomDoor(from, PickupMin, PickupMax, null, LaneWithin, out _pickup)
                || !JobSpots.RandomDoor(_pickup.Front, TripMin, TripMax, _pickup.Building, LaneWithin, out _drop))
            {
                return false;
            }
            _metres = JobSpots.Flat(_pickup.Front, _drop.Front);
            _crashes = 0;
            _stops = 0;
            _hailed = false;
            _tried = false;
            _onFootShown = false;
            _stoppedSince = -1f;
            Go(Step.ToPickup, now, 110f + JobSpots.Flat(from, _pickup.Front) / 4.5f);
            _pickupBy = _stepEnd;
            Show(JobText.T("reallife_job_taxi_obj_pickup", "place", _pickup.Name));
            Mark(_pickup.Front, _pickupLabel);
            Ctx.Logger?.Info("Jobs: taxi fare from " + _pickup.Name + " to " + _drop.Name + ", " + _metres.ToString("F0") + " m.");
            return true;
        }

        private void Go(Step step, float now, float seconds)
        {
            _step = step;
            _stepFrom = now;
            _stepEnd = now + seconds;
        }

        // ---------------------------------------------------------------- the tick

        public override void Tick(float now)
        {
            float dt = Mathf.Max(0.05f, now - _lastTick);
            _lastTick = now;
            if (Ctx.Driving && !ReferenceEquals(Ctx.Car, _signOn) && now >= _nextSign)
            {
                _nextSign = now + 2f;
                Sign(Ctx.Car);
            }
            if (_hired && Ctx.Rental.Car == null && JobDirector.NearestCar(60f) == null)
            {
                // The hired van is gone (wrecked, and taken away by the game): the shift is over, nobody's bad one.
                Ctx.Say("reallife_job_rental_lost", 9f);
                Finish(JobEnd.Interrupted);
                return;
            }
            SeeOff(now);
            switch (_step)
            {
                case Step.ToPickup: ToPickup(now); break;
                case Step.Boarding: Boarding(now); break;
                case Step.Riding: Riding(now, dt); break;
                case Step.Between: Between(now); break;
            }
            _lastVelocity = Ctx.Velocity;
            if (!Over)
            {
                Figures(now);
            }
        }

        private void ToPickup(float now)
        {
            int left = Mathf.CeilToInt(_stepEnd - now);
            // (the countdown is on the card: see Figures)
            if (left <= 0)
            {
                Missed(now);
                return;
            }
            float away = JobSpots.Flat(Ctx.PlayerPosition, _pickup.Front);
            if (_passenger == null && away < PersonAppears)
            {
                _passenger = Ctx.Cast.Put(_pickup.Front, true);
                if (_passenger == null)
                {
                    // Nobody could be put there (the door front turned out not to be standable): another call instead.
                    Ctx.Logger?.Info("Jobs: nobody could be put at " + _pickup.Name + "; the fare is replaced.");
                    Replace(now);
                    return;
                }
                _passenger.StandAt(_pickup.Front, Ctx.PlayerPosition);
            }
            if (_passenger == null)
            {
                return;
            }
            if (away < HailWithin)
            {
                _passenger.Face(Ctx.PlayerPosition);
                if (Ctx.Driving && now >= _nextHail && _passenger.HasArrived)   // not while walking back to the kerb
                {
                    _nextHail = now + 3.5f;
                    _hailing = Ctx.Animations.Play(_passenger.Character, JobClip.Hail) > 0f;   // (the mod's clip goes on by itself; the game's gesture is made again)
                    if (!_hailed)
                    {
                        _hailed = true;
                        Show(JobText.T("reallife_job_taxi_obj_stop"));
                    }
                }
            }
            else
            {
                Unhail();   // the car has gone on: nobody waves at an empty road
            }
            if (!Ctx.Driving)
            {
                Unhail();
                if (!_onFootShown)
                {
                    _onFootShown = true;
                    Show(JobText.T("reallife_job_taxi_obj_car"));
                }
                _stoppedSince = -1f;
                return;
            }
            if (_onFootShown)
            {
                _onFootShown = false;
                Show(JobText.T(_hailed ? "reallife_job_taxi_obj_stop" : "reallife_job_taxi_obj_pickup", "place", _pickup.Name));
            }
            if (!Stopped(now, _pickup.Front))
            {
                return;
            }
            // Stopped beside them: is there a rear door they can walk to? Asked again only once the car has moved.
            Vector3 carAt = Ctx.Car.transform.position;
            if (_tried && JobSpots.Flat(carAt, _triedAt) < 1f)
            {
                return;
            }
            bool room = JobSpots.BesideCar(Ctx.Car, _passenger.Position, out _boardSpot);
            _doorClip = false;
            if (room)
            {
                // With a clip for getting in they walk to where that clip is made for: square on to the rear door, the
                // feet 0.75 m out from the middle of the seat. Without one, to the ground beside the door as before.
                JobSpots.AtDoor(Ctx.Car, _boardSpot, out Vector3 door, out _doorInwards, out _doorRight);
                if (ClipJobAnimations.Has(_doorRight ? JobClip.CarEnter : JobClip.CarEnterLeft))
                {
                    _doorClip = true;
                    _boardSpot = door;
                }
            }
            if (!room || !_passenger.MoveTo(_boardSpot, Pace.Walk))
            {
                _tried = true;
                _triedAt = carAt;
                Show(JobText.T("reallife_job_taxi_obj_room"));
                return;
            }
            Unhail();
            _tried = false;
            _car = Ctx.Car;
            _boardCarAt = carAt;
            Unmark();
            Show(JobText.T("reallife_job_taxi_obj_boarding"));
            Go(Step.Boarding, now, BoardAllow);
        }

        private void Boarding(float now)
        {
            if (_passenger == null || !_passenger.IsValid)
            {
                _passenger = null;
                Replace(now);
                return;
            }
            bool moved = !Ctx.Driving || Ctx.Car != _car || Ctx.Speed > 1.5f || JobSpots.Flat(Ctx.Car.transform.position, _boardCarAt) > 1.2f;
            float gap = JobSpots.Flat(_passenger.Position, _boardSpot);
            if (_enterUntil >= 0f)
            {
                // At the door, getting in. If the car pulls away now they are left standing, and say so below.
                if (!moved && now < _enterUntil)
                {
                    return;
                }
                _enterUntil = -1f;
                if (!moved)
                {
                    Ctx.Cast.Seat(_passenger);
                    _rider = _passenger;
                    _passenger = null;
                    StartRide(now);
                    return;
                }
                ActivityAnimators.Forget(_passenger.Character.animator);   // the door is gone from under their hand: nobody walks off half way into a car
            }
            bool there = now - _stepFrom > 0.6f && _passenger.HasArrived && gap < 1.3f;
            if (!moved && (there || (now > _stepEnd && gap < 3f)))
            {
                JobCast.DoorSound(_boardSpot);
                // With the clip, and only from the place it is made for (they stop where the walk left them: nobody is
                // slid the last step): they turn square to the door, open it and get in, and only then are they in the car.
                bool atDoor = _doorClip && gap < DoorExact;
                Vector3 here = _passenger.Position;
                _passenger.StandAt(here, atDoor ? here + _doorInwards * 3f : _car.transform.position);
                float getIn = atDoor ? Ctx.Animations.Play(_passenger.Character, _doorRight ? JobClip.CarEnter : JobClip.CarEnterLeft) : 0f;
                Ctx.Logger?.Info(getIn > 0f
                    ? "Jobs: the passenger gets in at the " + (_doorRight ? "right" : "left") + " rear door, " + gap.ToString("F2") + " m from where the clip is made for."
                    : "Jobs: the passenger is in the car (no getting-in clip" + (_doorClip ? ": they stopped " + gap.ToString("F2") + " m from the door)." : " to be had)."));
                if (getIn > 0f)
                {
                    _enterUntil = now + getIn;
                    return;
                }
                Ctx.Cast.Seat(_passenger);
                _rider = _passenger;
                _passenger = null;
                StartRide(now);
                return;
            }
            if (moved || now > _stepEnd)
            {
                // The car pulled away (or they could not get to it): they walk back to the pavement and wait there again.
                if (!_passenger.MoveTo(_pickup.Front, Pace.Walk))
                {
                    _passenger.Halt();
                }
                if (moved)
                {
                    Ctx.Say(JobText.Pick(Hold, ref _line), 5f);
                }
                _stoppedSince = -1f;
                // Not asked again where the car stands now; and the wait is not started afresh, only never cut below a fair chance.
                _tried = !moved;
                _triedAt = _boardCarAt;
                _pickupBy = Mathf.Max(_pickupBy, now + 20f);
                Go(Step.ToPickup, now, _pickupBy - now);
                Mark(_pickup.Front, _pickupLabel);
                Show(JobText.T("reallife_job_taxi_obj_stop"));
            }
        }

        private void StartRide(float now)
        {
            _comfort = 100f;
            _comfortShown = -1;
            _rideFrom = now;
            _awaySince = -1f;
            _speedingSaid = false;
            _wasHarsh = false;
            _lastVelocity = Ctx.Velocity;
            _stoppedSince = -1f;
            _tried = false;
            Tune(_car);
            Go(Step.Riding, now, 0f);
            Show(JobText.T("reallife_job_taxi_obj_drive", "place", _drop.Name));
            Mark(_drop.Front, _dropLabel);
            JobText.Toast(NotificationType.Info, JobText.Pick(Greetings, ref _line), "place", _drop.Name, 7f);
            ShowComfort();
        }

        private void Riding(float now, float dt)
        {
            if (_car == null)
            {
                Finish(JobEnd.Failed);   // the car is gone, and the passenger with it
                return;
            }
            if (!Ctx.Driving || Ctx.Car != _car)
            {
                if (_awaySince < 0f)
                {
                    _awaySince = now;
                    Show(JobText.T("reallife_job_taxi_obj_back"));
                }
                else if (now - _awaySince > AwayAllow)
                {
                    // Left sitting in the car: they have got out and gone by the time the driver is back, and the fare is lost.
                    Ctx.Say("reallife_job_taxi_abandoned", 8f);
                    Finish(JobEnd.Failed);
                }
                _stoppedSince = -1f;
                return;
            }
            if (_awaySince >= 0f)
            {
                _awaySince = -1f;
                Show(JobText.T("reallife_job_taxi_obj_drive", "place", _drop.Name));
            }

            // How hard the car is being thrown about, whichever way: braking, swerving, a bump.
            float g = (Ctx.Velocity - _lastVelocity).magnitude / dt;
            bool harsh = g > _harsh;
            if (((harsh && _wasHarsh) || g > _bump) && now - _lastJolt > 3f)
            {
                Jolt(8f, Braking, now);
            }
            _wasHarsh = harsh;
            if (Ctx.Speed > SpeedingAbove)
            {
                _comfort -= 4f * dt;
                if (!_speedingSaid)
                {
                    _speedingSaid = true;
                    Ctx.Say(JobText.Pick(Fast, ref _line), 5f);
                }
            }
            ShowComfort();

            if (!Stopped(now, _drop.Front))
            {
                return;
            }
            Vector3 carAt = _car.transform.position;
            if (_tried && JobSpots.Flat(carAt, _triedAt) < 1f && now - _lookingSince < 12f)
            {
                return;
            }
            if (!_tried)
            {
                _lookingSince = now;
            }
            if (StepOut(_drop.Front, now))
            {
                Settle(now);
                return;
            }
            _tried = true;
            _triedAt = carAt;
            Show(JobText.T("reallife_job_taxi_obj_room_out"));
            if (now - _lookingSince >= 12f)
            {
                // Boxed in for a good while: they are paid for anyway, having squeezed out unseen.
                Ctx.Logger?.Info("Jobs: no room beside the car at " + _drop.Name + " after 12 s; the fare is settled without the walk to the door.");
                Settle(now);
            }
        }

        // The passenger gets out on the side nearer where they are going. False if there is no room on either side.
        private bool StepOut(Vector3 towards, float now)
        {
            if (_car == null || !JobSpots.BesideCar(_car, towards, out Vector3 spot))
            {
                return false;
            }
            StreetActor person = _rider;
            if (person == null)
            {
                return false;
            }
            // With a clip for getting out they come out where that clip ends: square on to the door, facing the car, the
            // feet 0.75 m out from the middle of the seat. Without one (or if nobody can stand there), beside the door.
            JobSpots.AtDoor(_car, spot, out Vector3 door, out Vector3 inwards, out bool right);
            JobClip exit = right ? JobClip.CarExit : JobClip.CarExitLeft;
            bool atDoor = ClipJobAnimations.Has(exit) && Ctx.Cast.Unseat(person, door, true);
            if (!atDoor && !Ctx.Cast.Unseat(person, spot, true))
            {
                return false;
            }
            _rider = null;
            JobCast.DoorSound(spot);
            float getOut = 0f;
            if (atDoor)
            {
                person.StandAt(door, door + inwards * 3f);
                getOut = Ctx.Animations.Play(person.Character, exit);
            }
            else
            {
                // Out of the door with their back to the car.
                Vector3 away = spot + (spot - _car.transform.position);
                away.y = spot.y;
                person.StandAt(spot, away);
            }
            Ctx.Logger?.Info(getOut > 0f ? "Jobs: the passenger gets out at the " + (right ? "right" : "left") + " rear door (clip)."
                : "Jobs: the passenger is out beside the car (no getting-out clip to be had there).");
            if (_leaving != null)
            {
                Ctx.Cast.Release(_leaving);
            }
            _leaving = person;
            _leaveTo = towards;
            _leavingWalks = false;
            _farewellAt = now + getOut;          // what they thought of the ride is said once they are out
            _leaveAt = now + getOut + 1.8f;
            _leavingUntil = now + 40f;
            return true;
        }

        private void Settle(float now)
        {
            float seconds = now - _rideFrom;
            float par = _metres / 7.5f + 20f;
            int comfort = Mathf.Clamp(Mathf.RoundToInt(_comfort), 0, 100);
            var slip = new PaySlip { Units = 1, Base = UnitPay(), Distance = DistancePay(_metres) };
            int fare = slip.Base + slip.Distance;

            float tip = comfort >= 90 ? Random.Range(0.22f, 0.30f)
                : comfort >= 75 ? Random.Range(0.12f, 0.18f)
                : comfort >= 55 ? Random.Range(0.04f, 0.08f)
                : 0f;
            if (tip > 0f && seconds <= par && comfort >= 75)
            {
                slip.Bonus = Mathf.RoundToInt(fare * 0.05f);   // quick as well as smooth
            }
            slip.Tips = Mathf.RoundToInt(fare * tip * Ctx.TipFactor);
            slip.Penalty = Mathf.RoundToInt(fare * (_crashes * JobCatalogue.CrashCut + _stops * JobCatalogue.PoliceStopCut + (comfort < 30 ? 0.3f : 0f)));
            UnitDone(_fares + 1, fare);   // the shift's big tipper, if the offer drew one: a share of the fare
            Ctx.Pay(this, slip, "reallife_job_taxi_slip");

            string[] lines = comfort >= 75 ? Good : comfort >= 45 ? Fair : Bad;
            Ctx.Say(JobText.Pick(lines, ref _line), 7f);
            if (_leaving != null)
            {
                _farewell = comfort >= 45 ? JobClip.Thank : JobClip.Annoyed;   // played by SeeOff once they are out of the door
                Ctx.Cast.Emoji(_leaving, comfort >= 75 ? CharacterEmojiName.CommonGreenHappy
                    : comfort >= 45 ? CharacterEmojiName.CommonYellowNeutral : CharacterEmojiName.CommonRedAngry, 3f);
            }

            _fares++;
            _car = null;
            Ctx.Cast.Release(_rider);   // only still aboard if there was no room to get out: gone unseen
            _rider = null;
            Unmark();
            Show(JobText.T("reallife_job_taxi_obj_between"));
            Go(Step.Between, now, Random.Range(6f, 11f));
        }

        private void Between(float now)
        {
            if (now < _stepEnd)
            {
                return;
            }
            if (Ctx.Perk(CareerPerk.Shorter) && _fares >= CareerCatalogue.CabShiftFares)
            {
                Ctx.Say("reallife_career_cab_shift_done", 9f);
                Finish(JobEnd.Done);
                return;
            }
            if (!Ctx.FullShift && JobState.LeftToday(Def) <= 0)
            {
                Ctx.Say("reallife_job_taxi_done_day", 9f);
                Finish(JobEnd.Done);
                return;
            }
            if (SaveGameManager.Current.Energy < JobCatalogue.MinEnergy)
            {
                Ctx.Say("reallife_job_done_tired", 9f);
                Finish(JobEnd.Done);
                return;
            }
            if (NextFare(now))
            {
                _noCalls = 0;
                return;
            }
            if (++_noCalls >= 4)
            {
                Ctx.Say("reallife_job_taxi_done_quiet", 9f);
                Finish(JobEnd.Done);
                return;
            }
            Go(Step.Between, now, 8f);   // nothing nearby this time: listen a little longer
        }

        // ---------------------------------------------------------------- when it goes wrong

        // The passenger was not reached in time: they give up. Twice in a shift and the dispatcher stops calling.
        private void Missed(float now)
        {
            Ctx.Say(JobText.Pick(GaveUp, ref _line), 7f);
            LetGo(now);
            if (++_misses >= MissesAllowed)
            {
                Finish(JobEnd.Failed);
                return;
            }
            Unmark();
            Show(JobText.T("reallife_job_taxi_obj_between"));
            Go(Step.Between, now, 6f);
        }

        // The fare could not be staged: no blame, another one is called in.
        private void Replace(float now)
        {
            LetGo(now);
            Unmark();
            Show(JobText.T("reallife_job_taxi_obj_between"));
            Go(Step.Between, now, 3f);
        }

        // Whoever is waiting at the kerb walks off instead.
        private void LetGo(float now)
        {
            if (_passenger == null)
            {
                return;
            }
            if (_leaving != null)
            {
                Ctx.Cast.Release(_leaving);
            }
            Unhail();
            _leaving = _passenger;
            _passenger = null;
            _enterUntil = -1f;
            _farewellAt = -1f;
            if (!StreetSpots.AwayFrom(_leaving.Position, Ctx.PlayerPosition, 30f, false, out _leaveTo))
            {
                _leaveTo = _leaving.Position;
            }
            _leavingWalks = false;
            _leaveAt = now + 0.5f;
            _leavingUntil = now + 25f;
        }

        // The arm comes down (the hailing clip goes on until it is told; the game's gesture ends by itself).
        private void Unhail()
        {
            if (_hailing)
            {
                _hailing = false;
                if (_passenger != null && _passenger.IsValid)
                {
                    Ctx.Animations.Stop(_passenger.Character);
                }
            }
        }

        // The last passenger: a moment by the car, then to their door, then gone (through it, or once far behind).
        private void SeeOff(float now)
        {
            if (_leaving == null)
            {
                return;
            }
            if (!_leaving.IsValid)
            {
                _leaving = null;
                _farewellAt = -1f;
                return;
            }
            if (_farewellAt >= 0f && now >= _farewellAt)
            {
                _farewellAt = -1f;
                _leaving.Face(Ctx.PlayerPosition);
                Ctx.Animations.Play(_leaving.Character, _farewell);
            }
            if (!_leavingWalks)
            {
                if (now < _leaveAt)
                {
                    return;
                }
                _leavingWalks = true;
                if (JobSpots.Flat(_leaving.Position, _leaveTo) < 1.5f || !_leaving.MoveTo(_leaveTo, Pace.Walk))
                {
                    Ctx.Cast.Release(_leaving);
                    _leaving = null;
                }
                return;
            }
            bool arrived = now - _leaveAt > 1.5f && _leaving.HasArrived;
            if (arrived || now > _leavingUntil || JobSpots.Flat(_leaving.Position, Ctx.PlayerPosition) > 90f)
            {
                Ctx.Cast.Release(_leaving);
                _leaving = null;
            }
        }

        public override void OnCrash(float speed)
        {
            if (_step == Step.Riding)
            {
                _crashes++;
                Jolt(speed > 8f ? 40f : 22f, Crashes, Time.time);
            }
        }

        public override void OnPoliceStop()
        {
            if (_step == Step.Riding)
            {
                _stops++;
                Jolt(20f, Police, Time.time);
            }
        }

        private void Jolt(float cost, string[] lines, float now)
        {
            _comfort = Mathf.Max(0f, _comfort - cost);
            _lastJolt = now;
            Ctx.Say(JobText.Pick(lines, ref _line), 5f);
            ShowComfort();
        }

        // Shown in steps of five, so the text is rebuilt a few times a ride and not every tick.
        private void ShowComfort()
        {
            int shown = Mathf.Clamp(Mathf.CeilToInt(_comfort / 5f) * 5, 0, 100);
            if (shown != _comfortShown)
            {
                _comfortShown = shown;
            }
        }

        // True once the car being driven has stood still near a place for a moment.
        private bool Stopped(float now, Vector3 place)
        {
            if (!Ctx.Driving || Ctx.Speed > StoppedSpeed || JobSpots.Flat(Ctx.Car.transform.position, place) > StopWithin)
            {
                _stoppedSince = -1f;
                return false;
            }
            if (_stoppedSince < 0f)
            {
                _stoppedSince = now;
            }
            return now - _stoppedSince >= StoppedFor;
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            Ctx.Cast.Release(_passenger);
            Ctx.Cast.Release(_rider);
            Ctx.Cast.Release(_leaving);
            _passenger = null;
            _rider = null;
            _leaving = null;
            _car = null;
            Unsign();
        }
    }
}
