using System.Globalization;
using BigAmbitions.Characters;
using Characters.EmojiSystem;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Ride-hail: the passenger getting in, getting out, and walking off.</summary>
    public sealed partial class TaxiJob
    {
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
                    Seated(now);
                    return;
                }
                ActivityAnimators.Forget(_passenger.Character.animator);   // the door is gone from under their hand: nobody walks off half way into a car
            }
            bool there = now - _stepFrom > 0.6f && _passenger.HasArrived && gap < 1.3f;
            if (!moved && (there || (now > _stepEnd && gap < 3f)))
            {
                JobCast.DoorSound(_boardSpot);
                // With the clip, and only from the place it is made for (nobody is slid the last step): they turn to the door, open it
                // and get in, and only then are they in the car.
                bool atDoor = _doorClip && gap < DoorExact;
                Vector3 here = _passenger.Position;
                _passenger.StandAt(here, atDoor ? here + _doorInwards * 3f : _car.transform.position);
                float getIn = atDoor ? Ctx.Animations.Play(_passenger.Character, _doorRight ? JobClip.CarEnter : JobClip.CarEnterLeft) : 0f;
                Ctx.Logger?.Info(getIn > 0f
                    ? "Jobs: the passenger gets in at the " + (_doorRight ? "right" : "left") + " rear door, " + gap.ToString("F2", CultureInfo.InvariantCulture)
                        + " m from where the clip is made for."
                    : "Jobs: the passenger is in the car (no getting-in clip"
                        + (_doorClip ? ": they stopped " + gap.ToString("F2", CultureInfo.InvariantCulture) + " m from the door)." : " to be had)."));
                if (getIn > 0f)
                {
                    _enterUntil = now + getIn;
                    return;
                }
                Seated(now);
                return;
            }
            if (moved || now > _stepEnd)
            {
                // The car pulled away, or they could not get to it: they walk back to the pavement and wait again.
                if (!_passenger.MoveTo(_pickup.Front, Pace.Walk))
                {
                    _passenger.Halt();
                }
                if (moved)
                {
                    Ctx.Say(JobText.Pick(Hold, ref _line), 5f);
                }
                _stoppedSince = -1f;
                // Not asked again where the car stands now; the wait is not started afresh, only never cut below a fair chance.
                _tried = !moved;
                _triedAt = _boardCarAt;
                _pickupBy = Mathf.Max(_pickupBy, now + 20f);
                Go(Step.ToPickup, now, _pickupBy - now);
                Mark(_pickup.Front, _pickupLabel);
                Show(JobText.T("reallife_job_taxi_obj_stop"));
            }
        }

        // The passenger is in the car: off the street, and the ride begins.
        private void Seated(float now)
        {
            Ctx.Cast.Seat(_passenger);
            _rider = _passenger;
            _passenger = null;
            StartRide(now);
        }

        // The passenger gets out on the side nearer where they are going. False with no room on either side.
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
            // With a clip for getting out they come out where it ends: square on to the door, facing the car, 0.75 m out from the
            // middle of the seat. Without one, or with nowhere to stand there, beside the door.
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

        // The arm comes down: the hailing clip goes on until told, the game's gesture ends by itself.
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
    }
}
