using System.Globalization;
using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The drivers: getting out, arguing, giving their account, and the driver at fault questioned.</summary>
    public sealed partial class AccidentCast
    {
        // Once the officers have had a look, the driver at fault is questioned where they stand by the officer working the
        // scene (the one on traffic only if alone) and written a ticket. Decided once; a driver lying hurt, gone or never out
        // is not questioned, and the log says which.
        private void TickFault(float now)
        {
            if (_booking != Booking.None)
            {
                TickBooking(now);
                return;
            }
            if (!_faultKnown || _faultSettled || _officers != Crew.Working || now < _faultFrom)
            {
                return;
            }
            _faultSettled = true;
            ActorRole driver = _faultA ? _driverA : _driverB;
            if (!driver.Present || (_faultA && _lying))
            {
                Logger?.Info(_faultA && _lying
                    ? "People: the driver at fault is the one lying hurt; they go with the ambulance, and are not questioned at the scene."
                    : "People: the driver at fault is not out on the road (they never got out, or have gone); nobody is questioned at the scene.");
                return;
            }
            SceneOfficer by = _officerA.Role.Present ? _officerA : _officerB.Role.Present ? _officerB : null;
            if (by == null)
            {
                return;
            }
            if (_witness.Is(driver))
            {
                _witness.Stop();
            }
            _arguing = false;
            _argueOn = false;
            by.Stop();
            _booked = driver;
            _booker = by;
            _booking = Booking.Walking;
            _bookingUntil = now + BookWalkAllow;
            driver.Quiet();
            driver.Drop();
            driver.Stand(by.Role.Position);
            by.Role.Go(Towards(driver.Position, by.Role.Position, BookStand), Pace.Walk, driver.Position, BookWalkAllow);
            Logger?.Info("People: the driver at fault (the " + (_patient != null ? "driver who knocked someone down" : _faultA ? "worst-hit car's" : "other car's")
                         + ") stayed and called for help: an officer questions them and writes them a ticket; nobody is arrested.");
        }

        private void TickBooking(float now)
        {
            if (_booked == null || _booker == null || !_booked.Present || !_booker.Role.Present || _officers != Crew.Working)
            {
                EndBooking("the driver or the officer is no longer there");
                return;
            }
            if (_booking == Booking.Walking)
            {
                float apart = (_booker.Role.Position - _booked.Position).sqrMagnitude;
                if (!(_booker.Role.Settled && apart < (BookStand + 1.2f) * (BookStand + 1.2f)) && now < _bookingUntil)
                {
                    return;
                }
                _booker.Role.Face(_booked.Position);
                _booker.Role.Work(SceneClips.Notes, PermanentAnimationType.TextingPhone, PhoneProp);   // the notebook, as for a witness's account
                _booked.Face(_booker.Role.Position);
                _booked.Repeat(AnimationType.IDontThinkSo, 4f, 8f);
                _booking = Booking.Writing;
                _bookingUntil = now + Random.Range(BookLeast, BookMost);
                return;
            }
            if (now >= _bookingUntil)
            {
                EndBooking(null);
                _booker?.Begin(_brief, now);   // back to the round
                _booker = null;
            }
        }

        // The questions are over (or cut short): the driver is left standing by their car, the officer free.
        private void EndBooking(string cut)
        {
            if (_booking == Booking.None)
            {
                return;
            }
            bool written = _booking == Booking.Writing && cut == null;
            _booking = Booking.None;
            if (_booked != null && _booked.Present)
            {
                _booked.Quiet();
                _booked.Stand(_scene);
            }
            _booked = null;
            if (cut != null)
            {
                _booker = null;
            }
            Logger?.Info(written ? "People: the driver at fault has been questioned and written a ticket."
                : "People: the questions of the driver at fault were cut short (" + cut + ").");
        }

        private bool Booked(ActorRole driver)
        {
            return _booking != Booking.None && ReferenceEquals(_booked, driver);
        }

        private void DriversOut(float now)
        {
            Brief();
            DriversStepped = true;
            if (_patient != null)
            {
                if (_hasA && _driverA.Enter(_doorA, ActorLook.Civilian, 2f, onRoad: true))
                {
                    _outA = true;
                    ActorPool.PlayDoorSound(_doorA);
                    _witness.Begin(_driverA, _brief, now);
                }
                return;
            }

            bool gotA = _hasA && _driverA.Enter(_doorA, ActorLook.Civilian, 2f, onRoad: true);
            bool gotB = _hasB && _driverB.Enter(_doorB, ActorLook.Civilian, 2f, onRoad: true);
            _outA = gotA;
            _outB = gotB;
            if (gotA)
            {
                ActorPool.PlayDoorSound(_doorA);
            }
            if (gotB)
            {
                ActorPool.PlayDoorSound(_doorB);
            }
            Vector3 a = gotA ? _driverA.Position : _doorA;
            Vector3 b = gotB ? _driverB.Position : _doorB;

            switch (_severity)
            {
                case TrafficAccidents.Severity.Minor:
                    if (gotA && gotB)
                    {
                        // They have it out face to face (Argue); someone about to bolt stays by their door.
                        if (_bWillFlee)
                        {
                            _driverB.Stand(a);
                            _driverA.Go(Towards(b, a, 1.7f), Pace.Walk, b);
                        }
                        else
                        {
                            _driverA.Stand(b);
                            _driverB.Go(Towards(a, b, 1.7f), Pace.Walk, a);
                        }
                        _arguing = true;
                    }
                    else
                    {
                        _driverA.Stand(_scene);
                        _driverB.Stand(_scene);
                        _driverA.Hold(PhonePose, PhoneProp);
                        _driverB.Hold(PhonePose, PhoneProp);
                    }
                    break;

                case TrafficAccidents.Severity.Moderate:
                    _driverA.Stand(_scene);
                    _driverA.Hold(PhonePose, PhoneProp);
                    WitnessBegins(gotB, a, now);
                    break;

                default:
                    if (gotA && _mayLie && BodyClear(_driverA.Position, _roadForward))
                    {
                        _lying = true;
                        _driverA.Stand(_lieTowards);
                        _driverA.Hold(LyingPose);
                        Brief();   // there is now someone on the ground
                    }
                    else if (gotA)
                    {
                        // Nowhere beside the wreck where a whole body lies clear of every vehicle: nobody is put half under one.
                        _driverA.Stand(_scene);
                        _driverA.Hold(PhonePose, PhoneProp);
                        Logger?.Info("People: no clear road beside the wreck for its driver to lie on (a vehicle or street furniture within a body's length); they stay on their feet.");
                    }
                    WitnessBegins(gotB, a, now);
                    if (!gotB && _lying && !_hasB && !_bWillFlee)
                    {
                        Bystander(now);
                    }
                    break;
            }
        }

        // One car alone with its driver down: a passer-by comes over from the pavement, out of sight if they can, as a witness.
        private void Bystander(float now)
        {
            if (StreetSpots.PavementAround(_scene, 7f, 16f, true, out Vector3 from) && _driverB.Enter(from, ActorLook.Civilian, 2f, onRoad: true))
            {
                _witness.Begin(_driverB, _brief, now);
                Logger?.Info("People: a passer-by comes over to the driver lying in the road.");
            }
        }

        private void WitnessBegins(bool gotB, Vector3 a, float now)
        {
            if (!gotB)
            {
                return;
            }
            if (_bWillFlee)
            {
                _driverB.Stand(_hasA ? a : _scene);
            }
            else
            {
                _witness.Begin(_driverB, _brief, now);
            }
        }

        private bool Listening(out Vector3 officer)
        {
            officer = default;
            SceneOfficer with = _officerA.Interviewing ? _officerA : _officerB.Interviewing ? _officerB : null;
            if (with == null)
            {
                return false;
            }
            officer = with.Role.Position;
            return (officer - _brief.Witness).sqrMagnitude < TalkRange * TalkRange;
        }

        // While an officer stands with them, whoever is on their feet tells it with their hands; when the officer goes, the
        // hands stop.
        private void TakeStatements(bool heard, Vector3 officer)
        {
            _statements = heard;
            bool aTalks = _driverA.Present && !_lying && _patient == null && !Booked(_driverA) && (officer - _driverA.Position).sqrMagnitude < TalkRange * TalkRange * 2f;
            bool bTalks = _driverB.Present && !_witness.Active && !Booked(_driverB) && (officer - _driverB.Position).sqrMagnitude < TalkRange * TalkRange * 2f;
            if (!heard)
            {
                if (!_lying && _patient == null && !Booked(_driverA))
                {
                    _driverA.Quiet();
                }
                if (!_witness.Active && !Booked(_driverB))
                {
                    _driverB.Quiet();
                }
                _argueOn = false;
                return;
            }
            if (aTalks)
            {
                _driverA.Drop();
                _driverA.Face(officer);
                _driverA.Repeat(AnimationType.IDontThinkSo, 5f, 9f);
            }
            if (bTalks)
            {
                _driverB.Drop();
                _driverB.Face(officer);
                _driverB.Repeat(AnimationType.IDontThinkSo, 4f, 8f);
            }
            if (_severity == TrafficAccidents.Severity.Minor && aTalks)
            {
                _driverA.Emote(CharacterEmojiName.CommonYellowNeutral);
            }
        }

        private void Argue()
        {
            bool near = _driverA.Present && _driverB.Present && _driverA.Settled && _driverB.Settled
                        && (_driverA.Position - _driverB.Position).sqrMagnitude < TalkRange * TalkRange;
            if (near == _argueOn)
            {
                return;
            }
            _argueOn = near;
            if (near)
            {
                _driverA.Face(_driverB.Position);
                _driverB.Face(_driverA.Position);
                _driverA.Repeat(AnimationType.IDontThinkSo, 4f, 8f, CharacterEmojiName.CommonRedAngry, 0.5f);
                _driverB.Repeat(AnimationType.IDontThinkSo, 4.5f, 9f, CharacterEmojiName.CommonRedAngry, 0.4f);
            }
            else
            {
                _driverA.Quiet();
                _driverB.Quiet();
            }
        }
    }
}
