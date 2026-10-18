using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The outing's part for a drive or a lift: in at a rear door with the job passenger's clips, a ride, out on the kerb.</summary>
    public sealed partial class OutingScene
    {
        private const float DriveGoal = 650f;        // metres driven together
        private const float StopWithin = 17f;        // metres: the car has "pulled up" when stopped this near
        private const float StoppedSpeed = 0.7f;     // m/s
        private const float DoorExact = 0.35f;       // metres from where the getting-in clip is made for within which it is played
        private const float HarshG = 9.5f;           // m/s2 held over two ticks: an emergency stop
        private const float AwayAllow = 25f;         // seconds the driver may leave them sitting in the car

        private Vector3 _boardSpot;
        private Vector3 _boardCarAt;
        private Vector3 _inwards;
        private bool _doorClip;
        private bool _doorRight;
        private bool _tried;
        private Vector3 _triedAt;
        private float _lookingSince;
        private bool _wasHarsh;
        private float _lastJolt = -100f;

        private void TryBoard(float now, float gap)
        {
            if (!D.Driving)
            {
                _stoppedSince = -1f;
                if (!_onFootSaid && gap < 30f)
                {
                    _onFootSaid = true;
                    Line = SocialLife.T("reallife_social_obj_bring_car", SocialLife.Data(_who));
                }
                return;
            }
            if (_onFootSaid)
            {
                _onFootSaid = false;
                Line = SocialLife.T("reallife_social_obj_meet_car", SocialLife.Data(_who, "place", _venue.Name));
            }
            if (D.CarSpeed > StoppedSpeed || JobSpots.Flat(D.Car.transform.position, _friend.Position) > StopWithin)
            {
                _stoppedSince = -1f;
                return;
            }
            if (_stoppedSince < 0f)
            {
                _stoppedSince = now;
            }
            if (now - _stoppedSince < 0.5f)
            {
                return;
            }
            // Is there a rear door they can walk to? Asked again only once the car has moved.
            Vector3 carAt = D.Car.transform.position;
            if (_tried && JobSpots.Flat(carAt, _triedAt) < 1f)
            {
                return;
            }
            bool room = JobSpots.BesideCar(D.Car, _friend.Position, out _boardSpot);
            _doorClip = false;
            if (room)
            {
                JobSpots.AtDoor(D.Car, _boardSpot, out Vector3 door, out _inwards, out _doorRight);
                if (ClipJobAnimations.Has(_doorRight ? JobClip.CarEnter : JobClip.CarEnterLeft))
                {
                    _doorClip = true;
                    _boardSpot = door;
                }
            }
            if (room)
            {
                _friend.Idle(0.2f);
                SocialCast.OnRoad(_friend);
                room = _friend.WalkTo(_boardSpot);
            }
            if (!room)
            {
                _tried = true;
                _triedAt = carAt;
                Line = SocialLife.T("reallife_social_obj_no_room", SocialLife.Data(_who));
                return;
            }
            _tried = false;
            _car = D.Car;
            _boardCarAt = carAt;
            Unmark();
            if (!_greeted)
            {
                Arrived();
                Say(_who, GreetingLine(), SocialLife.Data(_who, "place", _venue.Name));
            }
            Line = SocialLife.T("reallife_social_obj_boarding", SocialLife.Data(_who));
            Go(Step.Boarding, now, 30f);
        }

        private void Boarding(float now)
        {
            bool moved = !D.Driving || D.Car != _car || D.CarSpeed > 1.5f || JobSpots.Flat(D.Car.transform.position, _boardCarAt) > 1.2f;
            float gap = JobSpots.Flat(_friend.Position, _boardSpot);
            if (_enterUntil >= 0f)
            {
                if (!moved && now < _enterUntil)
                {
                    return;
                }
                _enterUntil = -1f;
                if (!moved)
                {
                    D.Cast.Seat(_friend);
                    StartRide(now);
                    return;
                }
                ActivityAnimators.Forget(_friend.Character.animator);   // the door is gone from under their hand
            }
            bool there = now - _stepFrom > 0.6f && _friend.Arrived && gap < 1.3f;
            if (!moved && (there || (now > _stepEnd && gap < 3f)))
            {
                SocialCast.DoorSound(_boardSpot);
                // The clip only from the place it is made for: nobody is slid the last step.
                bool atDoor = _doorClip && gap < DoorExact;
                _friend.Stand();
                _friend.Face(atDoor ? _friend.Position + _inwards * 3f : _car.transform.position);
                float getIn = atDoor ? D.Animations.Play(_friend.Character, _doorRight ? JobClip.CarEnter : JobClip.CarEnterLeft) : 0f;
                SocialDirector.Logger?.Info(getIn > 0f
                    ? "Social: " + _who.Name + " gets in at the " + (_doorRight ? "right" : "left") + " rear door."
                    : "Social: " + _who.Name + " is in the car (no getting-in clip"
                        + (_doorClip ? ": they stopped " + gap.ToString("F2", CultureInfo.InvariantCulture) + " m from the door)." : " to be had)."));
                if (getIn > 0f)
                {
                    _enterUntil = now + getIn;
                    return;
                }
                D.Cast.Seat(_friend);
                StartRide(now);
                return;
            }
            if (moved || now > _stepEnd)
            {
                if (CitySpots.Pavement(_friend.Position, 8f, out Vector3 back))
                {
                    _friend.WalkTo(back);
                }
                _stoppedSince = -1f;
                _tried = !moved;
                _triedAt = _boardCarAt;
                _returning = true;   // no boarding until they are back and standing, or twenty seconds have gone
                _walkUntil = now + 20f;
                Line = SocialLife.T("reallife_social_obj_meet_car", SocialLife.Data(_who, "place", _venue.Name));
                Go(Step.Travel, now, 0f);
            }
        }

        private void StartRide(float now)
        {
            _metres = 0f;
            _shownTens = -1;
            _beats = 0;
            _jolts = 0;
            _awaySince = -1f;
            _wasHarsh = false;
            _stoppedSince = -1f;
            _tried = false;
            _lastCar = _car.transform.position;
            _lastVelocity = D.CarVelocity;
            if (_kind == PlanKind.Lift && _to.Valid)
            {
                Mark(_to.Front, _to.Name);
                Line = SocialLife.T("reallife_social_obj_lift", SocialLife.Data(_who, "to", _to.Name));
            }
            else
            {
                ShowDrive();
            }
            Go(Step.Riding, now, 0f);
        }

        private void ShowDrive()
        {
            int fifties = Mathf.FloorToInt(_metres / 50f);
            if (fifties != _shownTens)
            {
                _shownTens = fifties;
                Dictionary<string, string> data = SocialLife.Data(_who, "metres", (fifties * 50).ToString());
                data["goal"] = ((int)DriveGoal).ToString();
                Line = SocialLife.T("reallife_social_obj_drive", data);
            }
        }

        private void Riding(float now, float dt)
        {
            if (_car == null)
            {
                D.Cast.Release(_friend);
                _friend = null;
                Wrap(now, false);
                return;
            }
            if (!D.Driving || D.Car != _car)
            {
                if (_awaySince < 0f)
                {
                    _awaySince = now;
                    _shownTens = -1;
                    Line = SocialLife.T("reallife_social_obj_back_in", SocialLife.Data(_who));
                }
                else if (now - _awaySince > AwayAllow)
                {
                    Say(_who, SocialLines.Pick(SocialCatalogue.LeftBehind), null, NotificationType.Warning);
                    if (!_test)
                    {
                        SocialLife.Change(_who, 0f, -4f, -4f);
                    }
                    _settled = true;
                    D.Cast.Release(_friend);
                    _friend = null;
                    Over = true;
                }
                _stoppedSince = -1f;
                return;
            }
            bool lift = _kind == PlanKind.Lift && _to.Valid;
            if (_awaySince >= 0f)
            {
                _awaySince = -1f;
                if (lift)
                {
                    Line = SocialLife.T("reallife_social_obj_lift", SocialLife.Data(_who, "to", _to.Name));
                }
            }
            Vector3 carAt = _car.transform.position;
            float moved = JobSpots.Flat(carAt, _lastCar);
            _lastCar = carAt;
            if (moved < 30f)
            {
                _metres += moved;
            }

            // How hard the car is thrown about: braking, swerving, a kerb.
            float g = (D.CarVelocity - _lastVelocity).magnitude / dt;
            bool harsh = g > HarshG;
            if (((harsh && _wasHarsh) || g > HarshG + 5.5f) && now - _lastJolt > 3f)
            {
                _lastJolt = now;
                _jolts++;
                Say(_who, SocialLines.Pick(SocialCatalogue.RideJolt));
            }
            _wasHarsh = harsh;

            float goal = lift ? Mathf.Max(200f, JobSpots.Flat(_venue.Front, _to.Front)) : DriveGoal;
            if (_beats < 2 && _metres >= goal * (_beats + 1) / 3f)
            {
                _beats++;
                Say(_who, SocialLines.Pick(SocialCatalogue.RideTalk));
            }
            if (lift)
            {
                if (Stopped(now, _to.Front, 22f) && StepOut(now, _to.Front, carAt))
                {
                    Unmark();
                    Wrap(now, _jolts < 3 && _crashes == 0);
                }
                return;
            }
            ShowDrive();
            if (_metres >= goal)
            {
                Line = SocialLife.T("reallife_social_obj_pull_over", SocialLife.Data(_who));
                _stoppedSince = -1f;
                _tried = false;
                Go(Step.PullOver, now, 0f);
            }
        }

        private void PullOver(float now)
        {
            if (_car == null || !D.Driving || D.Car != _car)
            {
                Go(Step.Riding, now, 0f);   // out of the car: the ride's own rules for that apply
                return;
            }
            Vector3 carAt = _car.transform.position;
            if (!Stopped(now, carAt, 5f))
            {
                return;
            }
            Vector3 towards = CitySpots.Pavement(carAt, 9f, out Vector3 kerb) ? kerb : carAt + _car.transform.right * 3f;
            if (StepOut(now, towards, carAt))
            {
                Wrap(now, _jolts < 3 && _crashes == 0);
            }
        }

        private bool Stopped(float now, Vector3 place, float within)
        {
            if (D.CarSpeed > StoppedSpeed || JobSpots.Flat(_car.transform.position, place) > within)
            {
                _stoppedSince = -1f;
                return false;
            }
            if (_stoppedSince < 0f)
            {
                _stoppedSince = now;
            }
            return now - _stoppedSince >= 0.6f;
        }

        // Out on the side nearer where they are going; with no room for twelve seconds, let go unseen. False while waiting.
        private bool StepOut(float now, Vector3 towards, Vector3 carAt)
        {
            if (_tried && JobSpots.Flat(carAt, _triedAt) < 1f && now - _lookingSince < 12f)
            {
                return false;
            }
            if (!_tried)
            {
                _lookingSince = now;
            }
            if (JobSpots.BesideCar(_car, towards, out Vector3 spot))
            {
                JobSpots.AtDoor(_car, spot, out Vector3 door, out Vector3 inwards, out bool right);
                JobClip exit = right ? JobClip.CarExit : JobClip.CarExitLeft;
                bool atDoor = ClipJobAnimations.Has(exit) && D.Cast.Unseat(_friend, door, door + inwards * 3f);
                if (atDoor || D.Cast.Unseat(_friend, spot, spot + (spot - carAt)))
                {
                    SocialCast.DoorSound(spot);
                    if (atDoor)
                    {
                        _outUntil = now + D.Animations.Play(_friend.Character, exit);
                    }
                    SocialDirector.Logger?.Info("Social: " + _who.Name + " gets out " + (atDoor ? "at the " + (right ? "right" : "left") + " rear door (clip)." : "beside the car."));
                    _car = null;
                    return true;
                }
            }
            _tried = true;
            _triedAt = carAt;
            Line = SocialLife.T("reallife_social_obj_no_room_out", SocialLife.Data(_who));
            if (now - _lookingSince >= 12f)
            {
                SocialDirector.Logger?.Info("Social: no room beside the car for " + _who.Name + " after 12 s; they are let out unseen.");
                D.Cast.Release(_friend);
                _friend = null;
                _car = null;
                return true;
            }
            return false;
        }

        public override void OnCrash(float speed)
        {
            if ((_step == Step.Riding || _step == Step.PullOver) && _friend != null)
            {
                _crashes++;
                Say(_who, SocialLines.Pick(SocialCatalogue.RideCrash), null, NotificationType.Warning);
                if (_crashes == 1 && !_test)
                {
                    SocialLife.Remember(_who, MemoryCode.Crash);
                }
            }
        }
    }
}
