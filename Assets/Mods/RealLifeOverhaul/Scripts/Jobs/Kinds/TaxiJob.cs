using BigAmbitions.Characters;
using Characters.EmojiSystem;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Ride-hail in the player's own car or a hired van, with a roof sign: a passenger waves from the pavement, gets in at a
    /// rear door when the car stops beside them and gets out at the other address. The fare is by distance, the tip by how the
    /// ride felt (hard braking, bumps, speeding, police, crashes) and how quick it was; a hard stop is judged by what each car
    /// can do (<see cref="Tolerated"/>). Nothing is done on foot, so no panel and no clock.
    /// </summary>
    public sealed partial class TaxiJob : Job
    {
        private enum Step { ToPickup, Boarding, Riding, Between }

        private const float PickupMin = 70f;            // metres from the player a fare may wait
        private const float PickupMax = 380f;           // metres
        private const float TripMin = 280f;             // metres from the pickup
        private const float TripMax = 950f;             // metres
        private const float LaneWithin = 13f;           // metres: a door only counts if a car can pull up this near it
        private const float StopWithin = 17f;           // metres: the car has arrived when stopped this near the door front
        private const float StoppedSpeed = 0.7f;        // m/s
        private const float StoppedFor = 0.5f;          // seconds
        private const float PersonAppears = 130f;       // metres: the passenger is put on the street when the player is this near
        private const float HailWithin = 34f;           // metres
        private const float BoardAllow = 30f;           // seconds for the passenger to get in
        private const float AwayAllow = 25f;            // seconds the driver may leave a passenger sitting in the car
        private const float HarshG = 9.5f;              // m/s2 of braking or swerving held over two ticks: an emergency stop, and the most it is ever set to
        private const float HarshLeast = 4.5f;          // ...and the least: nobody minds less than this
        private const float BumpOver = 5.5f;            // m/s2 more in a single tick is a jolt: a kerb, a nudge
        private const float Grip = 9f;                  // m/s2 the tyres hold on a dry road
        private const float Tolerated = 0.7f;           // the share of what the car can do that a passenger puts up with
        private const float WheelRadius = 0.33f;        // metres
        private const float LaunchSpeed = 5f;           // m/s at which the engine's pull is taken
        private const string SignProp = "taxi_sign";
        private const float DoorExact = 0.35f;          // metres from the place the getting-in clip is made for within which it is played
        private const float SpeedingAbove = 26f;        // m/s: about 94 km/h, with a passenger aboard
        public const int MissesAllowed = 2;             // missed fares that end the shift

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
        private bool _hailing;              // the passenger's arm is up: a clip that goes on until it is stopped
        private bool _doorClip;             // they board where the getting-in clip is made for, and there is such a clip
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

        private ShiftFigures _figures;
        private int _cardKey = int.MinValue;

        public override JobDef Def => _def;

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
                // The hired van is gone (wrecked and taken away by the game): the shift is over, nobody's bad one.
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
                    // Nobody could be put there (the door front was not standable): another call instead.
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
                    _hailing = Ctx.Animations.Play(_passenger.Character, JobClip.Hail) > 0f;   // the mod's clip goes on by itself; the game's gesture is made again
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
                // With a getting-in clip they walk to where it is made for, square to the rear door; without one, beside the door.
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
                    // Left sitting in the car: they are gone by the time the driver is back, and the fare is lost.
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

            // How hard the car is thrown about, whichever way: braking, swerving, a bump.
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
                // Boxed in for a good while: paid anyway, having squeezed out unseen.
                Ctx.Logger?.Info("Jobs: no room beside the car at " + _drop.Name + " after 12 s; the fare is settled without the walk to the door.");
                Settle(now);
            }
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

        // The passenger was not reached in time and gives up; twice in a shift and the dispatcher stops calling.
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

        // In steps of five, so the card's text is rebuilt a few times a ride and not every tick.
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
