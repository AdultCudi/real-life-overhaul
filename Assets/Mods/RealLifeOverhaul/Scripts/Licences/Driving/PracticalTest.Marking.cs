using System;
using System.Globalization;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the examiner marks, and only what the player could have helped: the pedals are all-or-nothing keys and the
    /// car starts wherever there was room, so position faults wait until it has settled in a lane.
    /// </summary>
    internal sealed partial class PracticalTest
    {
        private const float SampleInterval = 0.1f;     // seconds
        private const float SettleAllow = 25f;         // seconds from setting off after which the car counts as on the road, wherever it is
        private const float LineApproach = 3f;         // metres short of a stop line the car must have been seen for the line to count
        private const float FirmStop = 6f;             // m/s2: a firm stop, not a harsh one
        private const float ReactTime = 0.7f;          // seconds to see a light change
        private const float RoomMost = 26f;            // metres: the most room asked for; a light red from that far is the driver's at any speed
        private const float ShuntAllow = 2.5f;         // seconds after being run into in which a line crossed is not the driver's doing
        private const float Abreast = 2f;              // metres along the road within which two lanes' stop lines are one line
        private const float HarshFrom = 11f;           // m/s (40 km/h): braking is only harsh from above this...
        private const float HarshHeld = 0.9f;          // ...with the brake held this many seconds: all but a full stop
        private const float OverHeld = 4f;             // seconds over the limit before it is a fault
        private const float FarOverHeld = 2.5f;        // ...and far over it
        private const float ContactLineGap = 1f;       // seconds between log lines of contacts that did not count (a bottomed spring meets the road every step)
        private const float StopHalfWidth = 1.7f;      // metres: the stop box in the lane, half its width...
        private const float StopHalfLength = 4f;       // ...and half its length: a car and a good metre each end
        private const float BoxAllow = 0.6f;           // metres a car may stand over the box's edge and still be in it
        private const float BoxNear = 2.5f;            // ...and still be marked only as parked badly

        // The box the drive ends in, painted on the road: the bay at the kerb, or with no kerb a box in the lane.
        private Vector3 _boxCentre;
        private Quaternion _boxFacing = Quaternion.identity;
        private float _boxHalfWidth, _boxHalfLength;
        private bool _inBox;

        private CarController _hooked;
        private UnityAction<Collision> _onCollision;
        private float _nextSample;
        private int _line = -1;
        private float _along;
        private float _redSince = -1f;
        private bool _pastBooked;
        private float _lineFrom;            // how far from the line the car was when it was first seen (negative: short of it)
        private bool _couldStop;            // the light was red while there was still room to stop firmly
        private float _lastShunt = -100f;   // when the car was last run into while standing or crawling
        private bool _settled;              // the car has been properly in a lane since setting off
        private float _driveSince;
        private float _overSince = -1f, _farOverSince = -1f;
        private bool _overBooked, _farOverBooked;
        private float _laneSince = -1f;
        private bool _laneBooked;
        private float _pavementSince = -1f;
        private bool _pavementBooked;
        private float _wrongSince = -1f;
        private bool _wrongBooked;
        private float _lastSpeed;
        private float _brakeSince = -1f;
        private float _lastBrake = -100f;
        private float _lastTouch = -100f;
        private float _contactLineAt = -100f;   // when a contact that did not count was last written to the log
        private int _contactsQuiet;             // ...and how many like it have not been written since
        private float _reversed;            // metres backed near the bay
        private Vector3 _lastPosition;

        private int Faults(bool serious)
        {
            int total = 0;
            for (int i = 0; i < _faults.Length; i++)
            {
                total += Serious[i] == serious ? _faults[i] : 0;
            }
            return total;
        }

        private void ResetSampling()
        {
            _line = -1;
            _redSince = -1f;
            _pastBooked = false;
            _couldStop = false;
            _lastShunt = -100f;
            _settled = false;
            _overSince = -1f;
            _farOverSince = -1f;
            _overBooked = false;
            _farOverBooked = false;
            _laneSince = -1f;
            _laneBooked = false;
            _pavementSince = -1f;
            _pavementBooked = false;
            _wrongSince = -1f;
            _wrongBooked = false;
            _brakeSince = -1f;
            _lastSpeed = 0f;
            _nextSample = 0f;
        }

        private void Sample(float now, float delta)
        {
            Vector3 position = _body.position;
            Vector3 velocity = _body.velocity;
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            Vector3 forward = _body.rotation * Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            Vector3 heading = speed > 1.5f ? new Vector3(velocity.x, 0f, velocity.z) / speed : forward;

            // Backing up near the bay: the commercial class has to reverse in.
            if (_step == Step.Park)
            {
                BoxSign();
                Vector3 moved = position - _lastPosition;
                float along = moved.x * forward.x + moved.z * forward.z;
                if (along < 0f && along > -3f)
                {
                    _reversed -= along;
                }
            }
            _lastPosition = position;

            // A stop line counts only if the car came up to it from behind, and its red only if there was room to stop.
            if (RoadMap.LineAt(position, heading, out int line, out float past))
            {
                bool red = RoadMap.IsRed(RoadMap.Line(line));
                if (line != _line)
                {
                    // A line abreast of the one watched (changing lane, or straddling two) is the same line.
                    bool same = _line >= 0 && Mathf.Abs(past - _along) < Abreast;
                    _line = line;
                    if (!same)
                    {
                        _along = past;
                        _lineFrom = past;
                        _redSince = -1f;
                        _couldStop = false;
                        _pastBooked = false;
                    }
                    else if (past < _lineFrom)
                    {
                        _lineFrom = past;
                    }
                    _along = Mathf.Min(_along, past);
                }
                if (!red)
                {
                    _redSince = -1f;
                    _couldStop = false;
                }
                else
                {
                    if (_redSince < 0f)
                    {
                        _redSince = now;
                    }
                    _couldStop |= -past >= Mathf.Min(speed * ReactTime + speed * speed / (2f * FirmStop), RoomMost);
                }
                bool theirs = _settled && _lineFrom <= -LineApproach && red && _couldStop && now - _redSince >= 1f && now - _lastShunt >= ShuntAllow;
                if (theirs && _along < 1f && past >= 1f && speed > 1.5f)
                {
                    Fault(TestFault.RedLight);
                    _pastBooked = true;
                }
                else if (theirs && !_pastBooked && speed < 0.4f && past > 1.2f && past < 4f)
                {
                    Fault(TestFault.PastLine);
                    _pastBooked = true;
                }
                _along = past;
            }
            else
            {
                _line = -1;
            }

            // The brake is a key, as hard on a tap as when held, so harsh braking is a full stop held from speed.
            float decel = (_lastSpeed - speed) / delta;
            if (decel > 9f && (_brakeSince >= 0f || _lastSpeed > HarshFrom))
            {
                if (_brakeSince < 0f)
                {
                    _brakeSince = now;
                }
                else if (now - _brakeSince >= HarshHeld && now - _lastBrake > 6f)
                {
                    _lastBrake = now;
                    Fault(TestFault.HarshBrake);
                }
            }
            else
            {
                _brakeSince = -1f;
            }
            _lastSpeed = speed;

            if (!RoadMap.NearestLane(position, out float distance, out Vector3 runs, out float limit))
            {
                _overSince = -1f;
                _farOverSince = -1f;
                _laneSince = -1f;
                _pavementSince = -1f;
                _wrongSince = -1f;
                return;
            }

            float over = speed * 3.6f - limit;
            if (over > 10f && distance < 8f)
            {
                if (_overSince < 0f)
                {
                    _overSince = now;
                }
                else if (!_overBooked && now - _overSince >= OverHeld)
                {
                    _overBooked = true;
                    Fault(TestFault.Speeding);
                }
            }
            else
            {
                _overSince = -1f;
                _overBooked = false;
            }
            if (over > 30f && distance < 8f)
            {
                if (_farOverSince < 0f)
                {
                    _farOverSince = now;
                }
                else if (!_farOverBooked && now - _farOverSince >= FarOverHeld)
                {
                    _farOverBooked = true;
                    Fault(TestFault.SpeedingFar);
                }
            }
            else
            {
                _farOverSince = -1f;
                _farOverBooked = false;
            }

            // Measured from the route's own lane, as the road data's points may lie metres apart.
            float lateral = _route.Lateral(position, out bool junction);
            float withLane = runs.x * velocity.x + runs.z * velocity.z;
            if (!_settled)
            {
                _settled = now - _driveSince >= SettleAllow || (speed > 3f && lateral < 1.8f && distance < 2.2f && withLane > 0.5f * speed);
                _laneSince = -1f;
                _pavementSince = -1f;
                _wrongSince = -1f;
                return;
            }
            if (_step == Step.Drive && speed > 3f && !junction && lateral > 2.6f && lateral < 7f)
            {
                if (_laneSince < 0f)
                {
                    _laneSince = now;
                }
                else if (!_laneBooked && now - _laneSince >= 3f)
                {
                    _laneBooked = true;
                    Fault(TestFault.LeftLane);
                }
            }
            else if (lateral < 1.8f || speed < 1f || junction)
            {
                _laneSince = -1f;
                _laneBooked = false;
            }

            if (speed > 2f && lateral > 3.2f && NavMesh.SamplePosition(position, out NavMeshHit hit, 0.6f, StreetSpots.Pavement)
                && (hit.position.x - position.x) * (hit.position.x - position.x) + (hit.position.z - position.z) * (hit.position.z - position.z) < 0.25f)
            {
                if (_pavementSince < 0f)
                {
                    _pavementSince = now;
                }
                else if (!_pavementBooked && now - _pavementSince >= 1.5f)
                {
                    _pavementBooked = true;
                    Fault(TestFault.Pavement);
                }
            }
            else
            {
                _pavementSince = -1f;
                _pavementBooked = false;
            }

            // Against the traffic. Between two lane points nothing is known, so only going with a lane or slowing clears it.
            if (speed > 5f && distance < 2.2f && withLane < -0.85f * speed)
            {
                if (_wrongSince < 0f)
                {
                    _wrongSince = now;
                }
                else if (!_wrongBooked && now - _wrongSince >= 2.5f)
                {
                    _wrongBooked = true;
                    Fault(TestFault.WrongWay);
                }
            }
            else if (speed < 3f || (distance < 2.2f && withLane > 0.5f * speed))
            {
                _wrongSince = -1f;
                _wrongBooked = false;
            }
        }

        private void Hook(CarController car)
        {
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                _onCollision = _onCollision ?? Guard.Listener<Collision>(OnCollision);
                car.vehicleController.onCollision.AddListener(_onCollision);
                _hooked = car;
            }
        }

        private void Unhook()
        {
            if (_hooked != null && _hooked.vehicleController != null)
            {
                _hooked.vehicleController.onCollision.RemoveListener(_onCollision);
            }
            _hooked = null;
        }

        // Only the car hitting something is marked (CarContactRules): never the road under it as its nose dips, a kerb
        // under the wheels, its own load or a trigger. Standing or crawling, the car was run into: not marked, nor a
        // line crossed just after.
        private void OnCollision(Collision collision)
        {
            try
            {
                float now = Time.time;
                if ((_step != Step.Drive && _step != Step.Park) || collision == null || _hooked == null || now - _lender.SpawnedAt < 3f)
                {
                    return;
                }
                CarContact contact = CarContacts.Judge(collision, _hooked.transform);
                ContactMark mark = TestContactRules.Mark(contact, _lastSpeed, now - _lastTouch);
                if (mark == ContactMark.RunInto)
                {
                    _lastShunt = now;
                }
                else if (mark == ContactMark.Touched || mark == ContactMark.Collision)
                {
                    _lastTouch = now;
                    Fault(mark == ContactMark.Collision ? TestFault.Collision : TestFault.Touched);
                }
                if (Meta.Dev.Verbose)
                {
                    NoteContact(collision, contact, mark, now);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // One line a contact considered; those that did not count at most one a second, with how many were passed over.
        private void NoteContact(Collision collision, in CarContact contact, ContactMark mark, float now)
        {
            bool marked = mark == ContactMark.Touched || mark == ContactMark.Collision;
            if (!marked && now - _contactLineAt < ContactLineGap)
            {
                _contactsQuiet++;
                return;
            }
            string outcome = marked ? "counted, marked as " + (mark == ContactMark.Collision ? "a collision (serious)" : "a touch (minor)") + ": PASS"
                : mark == ContactMark.RunInto ? "counted, but the car was standing or crawling: run into, not marked"
                : mark == ContactMark.Same ? "counted, but the same knock as the fault just marked"
                : contact.IsHit ? "not counted: under " + TestContactRules.TouchFrom.ToString("0.0", CultureInfo.InvariantCulture) + " m/s along the normal, too soft for a touch"
                : "not counted: " + CarContactRules.Why(contact.Verdict);
            string quiet = _contactsQuiet > 0 ? " (" + _contactsQuiet + " more not counted since the last line)" : string.Empty;
            if (!marked)
            {
                _contactLineAt = now;
                _contactsQuiet = 0;
            }
            _logger?.Info("Licences check (contact): " + CarContacts.Describe(collision, contact) + ", the car at "
                          + _lastSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " m/s: " + outcome + quiet + ".");
        }

        private void Fault(TestFault fault)
        {
            _faults[(int)fault]++;
            _logger?.Info("Licences: fault marked: " + fault + " (" + (Serious[(int)fault] ? "serious" : "minor") + ").");
            if (_lesson)
            {
                Licences.Toast(NotificationType.Warning, FaultKeys[(int)fault], 6f);
            }
        }

        // Metres over the edge of the painted box; 0 or less is inside it.
        private float OverBox(Vector3 centre, float halfLength, float halfWidth)
        {
            Vector3 local = Quaternion.Inverse(_boxFacing) * (centre - _boxCentre);
            return Mathf.Max(Mathf.Abs(local.x) + halfWidth - _boxHalfWidth, Mathf.Abs(local.z) + halfLength - _boxHalfLength);
        }

        // The marker says whether the car is in the box, before "I have parked" is pressed.
        private void BoxSign()
        {
            JobSpots.CarFrame(_car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
            bool inside = OverBox(centre, halfLength, halfWidth) <= BoxAllow;
            if (inside != _inBox)
            {
                _inBox = inside;
                _markerLabel = Licences.T(inside ? "reallife_lic_marker_in" : _route.HasBay ? "reallife_lic_marker_bay" : "reallife_lic_marker_stop");
            }
        }

        private void JudgeParking()
        {
            JobSpots.CarFrame(_car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            if (!_route.HasBay)
            {
                // A stop in the lane is judged by where the car stands, not how straight.
                float out0 = OverBox(centre, halfLength, halfWidth);
                _logger?.Info("Licences: stopped " + Mathf.Max(0f, out0).ToString("F2", CultureInfo.InvariantCulture) + " m over the edge of the stop box painted in the lane ("
                              + (_boxHalfWidth * 2f).ToString("F1", CultureInfo.InvariantCulture) + " by " + (_boxHalfLength * 2f).ToString("F1", CultureInfo.InvariantCulture) + " m).");
                if (out0 > BoxNear)
                {
                    _faults[(int)TestFault.NotParked]++;
                }
                else if (out0 > BoxAllow)
                {
                    _faults[(int)TestFault.ParkedBadly]++;
                }
                return;
            }
            Vector3 local = Quaternion.Inverse(_route.BayFacing) * (centre - _route.BayCentre);
            Vector3 bayForward = _route.BayFacing * Vector3.forward;
            float angle = Vector3.Angle(forward, bayForward);
            float overSide = Mathf.Abs(local.x) + halfWidth - _route.BayHalfWidth;
            float overEnd = Mathf.Abs(local.z) + halfLength - _route.BayHalfLength;
            float over = Mathf.Max(overSide, overEnd);
            _logger?.Info("Licences: parked " + local.x.ToString("F2", CultureInfo.InvariantCulture) + " m across and " + local.z.ToString("F2", CultureInfo.InvariantCulture)
                          + " m along from the middle of the bay, "
                          + angle.ToString("F0") + " degrees off, " + Mathf.Max(0f, over).ToString("F2", CultureInfo.InvariantCulture) + " m over its edge; reversed "
                          + _reversed.ToString("F1", CultureInfo.InvariantCulture) + " m.");
            if (over > 0.6f || angle > 25f)
            {
                _faults[(int)TestFault.NotParked]++;
            }
            else if (over > 0.1f || angle > 12f)
            {
                _faults[(int)TestFault.ParkedBadly]++;
            }
            if (_class == LicenceClass.Commercial && _reversed < 2.5f && over <= 0.6f)
            {
                _faults[(int)TestFault.NoReverse]++;
            }
        }
    }
}
