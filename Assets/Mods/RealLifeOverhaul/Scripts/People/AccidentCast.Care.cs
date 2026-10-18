using System.Globalization;
using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The paramedics' job a step at a time: treatment, the lift, and the stretcher wheeled back aboard.</summary>
    public sealed partial class AccidentCast
    {
        // The paramedics' job, a step at a time. With the stretcher out each step ends when it is done, never on a clock, so
        // nobody works from where they are or walks off without it; only the crew on foot, with no stretcher, is on a clock.
        private void TickCare(float now)
        {
            switch (_care)
            {
                case Care.Out:
                    if (_wheeled)
                    {
                        if (StretcherLost(now))
                        {
                            break;
                        }
                        GateCheck(_way.OutGate, "through the opening");
                        Overdue(now);
                        if (_stretcher.Arrived)
                        {
                            ParkStretcher(now);
                        }
                        break;
                    }
                    OnFoot(now);
                    break;

                case Care.Settling:
                    Overdue(now);
                    if (Placed(_medicA) && Placed(_medicB))
                    {
                        Logger?.Info("People: paramedics with the patient.");
                        _care = Care.Treating;
                        _careUntil = Mathf.Min(now + _treatFor, Mathf.Max(now + 2f, _careLatest));
                    }
                    break;

                case Care.Treating:
                    if (now >= _careUntil)
                    {
                        BeginLift(now);
                    }
                    break;

                case Care.Lifting:
                    if (StretcherLost(now))
                    {
                        break;
                    }
                    Overdue(now);
                    if (Placed(_lead) && (_rear == null || Placed(_rear)))
                    {
                        LiftOn(now);
                    }
                    break;

                case Care.Loading:
                    if (now >= _careUntil && !StretcherLost(now))
                    {
                        WheelBack(now);
                    }
                    break;

                case Care.Clearing:
                    if (!StretcherLost(now))
                    {
                        WheelBack(now);
                    }
                    break;

                case Care.Wheeling:
                    if (StretcherLost(now))
                    {
                        break;
                    }
                    GateCheck(_way.HomeGate, "back through the opening");
                    Overdue(now);
                    if (_stretcher.Arrived)
                    {
                        LoadIn();
                    }
                    break;

                case Care.Returning:
                    if (!_medicA.Present && !_medicB.Present)
                    {
                        _care = Care.Away;
                        _medicsWorked = true;
                        Logger?.Info("People: paramedics back aboard.");
                    }
                    break;
            }
        }

        private bool Placed(ActorRole who)
        {
            return who == null || !who.Present || (who.Settled && (_stretcher == null || !_stretcher.Walking(who)));
        }

        // The crew on foot, with no stretcher: whoever is late is given more time, and out of sight is put at their place;
        // only at the very latest do they work from where they are, which the log says.
        private void OnFoot(float now)
        {
            bool there = _medicA.Settled || _medicB.Settled;
            if (!there && now >= _careUntil && now < _careLatest)
            {
                bool seen = _medicA.Visible || _medicB.Visible;
                if (!seen)
                {
                    there = (_medicA.Present && !_medicA.Settled && _medicA.Rise(_goA)) | (_medicB.Present && !_medicB.Settled && _medicB.Rise(_goB));
                }
                if (!there)
                {
                    _careUntil = Mathf.Min(now + 8f, _careLatest);
                    return;
                }
            }
            if (there || now >= _careUntil)
            {
                if (there)
                {
                    Logger?.Info("People: paramedics with the patient.");
                }
                else
                {
                    Check("on foot", false, "the paramedics did not reach the patient on foot by the latest they could; they work from where they are");
                }
                _care = Care.Treating;
                _careUntil = Mathf.Min(now + _treatFor, Mathf.Max(now + 2f, _careLatest));
            }
        }

        // The stretcher was lost (it faulted, or whoever had it left): said once, and the crew walks back. True if so.
        private bool StretcherLost(float now)
        {
            if (_stretcher != null && _stretcher.Out && (!_wheeled || _stretcher.Held))
            {
                return false;
            }
            Check("stretcher", false, "the stretcher was lost in the middle of the job (it faulted, or whoever had it left the street); the crew walks back to the ambulance without it");
            _wheeled = false;
            ReleaseWay();
            PutStretcherAway();
            _medicA.Drop();
            _medicB.Drop();
            _medicA.Board(_ambulanceDoor, Pace.Walk, 40f);
            _medicB.Board(_ambulanceDoor, Pace.Walk, 40f);
            _care = Care.Returning;
            return true;
        }

        private void ParkStretcher(float now)
        {
            _wheeled = false;
            Transform bed = _stretcher.transform;
            float beside = Mathf.Abs(Vector3.Dot(bed.position - _way.Chest, _way.Side));
            float off = Flat(bed.position - _way.Bed).magnitude;
            float skew = Vector3.Angle(bed.forward, _way.Axis);
            skew = skew > 90f ? 180f - skew : skew;
            Check("parked beside", off <= PlaceWithin && skew <= AngleWithin && Mathf.Abs(beside - CrewWay.BedOut) <= PlaceWithin,
                System.FormattableString.Invariant(
                    $"the stretcher stands {off:F2} m from its place ({PlaceWithin:F2} m allowed), its middle line {beside:F2} m from the casualty's ({CrewWay.BedOut:F2} m wanted), ")
                + System.FormattableString.Invariant($"{skew:F1} deg off parallel ({AngleWithin:F0} allowed)") + (_way.Swing ? ", swung in" : ", wheeled straight in"));
            _stretcher.Park();   // hands off it, and the bag off the bed into the hand of whoever treats
            GiveLaneBack();
            // Nobody kneels where it stands: the places were laid out clear of where it was to stand, which is where it is.
            bool moved = CrewWay.OffTheBed(bed, ref _kneelAt);
            moved |= CrewWay.OffTheBed(bed, ref _secondKneel);
            ActorRole treats = _treats != null && _treats.Present ? _treats : _medicA.Present ? _medicA : _medicB;
            ActorRole other = treats == _medicA ? _medicB : _medicA;
            ToTheChest(treats, Pace.Walk, 10f);
            if (other.Present)
            {
                Send(other, _secondKneel, Pace.Walk, _patientAt, 10f);
                other.Kneel();
            }
            Logger?.Info("People: stretcher brought up beside the casualty" + (moved ? "; a place to kneel was moved clear of where it stands" : "") + "; "
                         + (_cpr ? "compressions at the chest." : "they kneel to work at the chest."));
            _care = Care.Settling;
            Phase(now, 8f);
        }

        private void BeginLift(float now)
        {
            _medicA.Drop();
            _medicB.Drop();
            if (_stretcher == null || !_stretcher.Out)
            {
                _medicA.Board(_ambulanceDoor, Pace.Walk, 40f);
                _medicB.Board(_ambulanceDoor, Pace.Walk, 40f);
                _care = Care.Returning;
                Logger?.Info("People: paramedics done, walking back to the ambulance.");
                return;
            }
            _stretcher.Park();
            _wheeled = false;
            // Each to an end of it: whoever will push to the end that trails on the way back (so that it is not turned round
            // with somebody on it more than the way needs), the other to the end that leads. Of two, whichever pairing is
            // the shorter walk in all; round the bed's corner and the casualty's head or feet, never over them.
            Vector3 heading = _way.HomeHeading;
            Vector3 handle = _stretcher.HandleFor(heading);
            Vector3 leading = _stretcher.LeadingFor(heading);
            ActorRole first = _medicA.Present ? _medicA : _medicB;
            ActorRole other = _medicA.Present && _medicB.Present ? _medicB : null;
            bool swap = other != null && Flat(other.Position - handle).magnitude + Flat(first.Position - leading).magnitude
                        < Flat(first.Position - handle).magnitude + Flat(other.Position - leading).magnitude;
            _lead = swap ? other : first;
            _rear = swap ? first : other;
            Send(_lead, handle, Pace.Walk, _stretcher.transform.position, LiftAllowance);
            if (_rear != null)
            {
                Send(_rear, leading, Pace.Walk, _stretcher.transform.position, LiftAllowance);
            }
            _care = Care.Lifting;
            Phase(now, LiftAllowance);
        }

        private void LiftOn(float now)
        {
            ICasualty patient = Patient;
            bool there = patient != null && patient.Present;
            bool covered = there && (_fatal || !_stretcher.Load(patient));
            if (covered)
            {
                // Dead, or not to be moved as they are: covered, and the body itself is off the street from here.
                _stretcher.Cover();
                patient.TakeAway();
            }
            float leadOff = _lead != null && _lead.Present ? _stretcher.FromAnEnd(_lead.Position) : 0f;
            float rearOff = _rear != null && _rear.Present ? _stretcher.FromAnEnd(_rear.Position) : 0f;
            float lifting = there && !covered ? _stretcher.Lift(_lead, _rear) : 0f;
            Check("lifted", leadOff <= HandleWithin && rearOff <= HandleWithin && (!there || covered || _stretcher.Laden),
                System.FormattableString.Invariant($"whoever pushes back stands {leadOff:F2} m from their end of the stretcher")
                    + (_rear != null ? System.FormattableString.Invariant($", the other {rearOff:F2} m from theirs") : ", alone")
                + System.FormattableString.Invariant($" ({HandleWithin:F2} m allowed); ")
                + (!there ? "nobody left to lift, the stretcher goes back empty"
                    : covered ? "the casualty is covered on the stretcher and off the road"
                    : lifting > 0f ? System.FormattableString.Invariant($"the crew lifts the casualty onto it ({lifting:F1} s)") : "the casualty is lifted onto it"));
            if (lifting > 0f)
            {
                _care = Care.Loading;
                _careUntil = now + lifting;
                return;
            }
            WheelBack(now);
        }

        // Wheeled back along the way planned. Traffic that has since come to stand on it is taken off the road first (out of
        // view at once, in view after five seconds).
        private void WheelBack(float now)
        {
            Collider inWay = _way.HomeBlocked();
            if (inWay != null && StillWaiting(inWay, now))
            {
                _care = Care.Clearing;
                return;
            }
            ActorRole second = _rear != null && _rear.Present ? _rear : null;
            bool final = !_way.Square;
            bool held = _lead != null && _stretcher.Wheel(_lead, second, _way.Home, -_way.Guide, 1f, final, _way.HomeBed, -_way.Back);
            if (!held && second != null && _stretcher.Wheel(second, null, _way.Home, -_way.Guide, 1f, final, _way.HomeBed, -_way.Back))
            {
                _lead = second;
                _rear = null;
                held = true;
            }
            _wheeled = held;
            _gateSaid = false;
            if (!held)
            {
                StretcherLost(now);
                return;
            }
            HoldLane();
            Vector3 start = _stretcher.HandleFor(_way.HomeHeading);
            float pusherOff = Flat(_lead.Position - start).magnitude;
            Check("set off back", pusherOff <= HandleWithin && inWay == null,
                System.FormattableString.Invariant($"whoever pushes takes it up {pusherOff:F2} m from its handle ({HandleWithin:F2} m allowed)")
                + (second != null ? ", the other of the crew walked at its leading end" : ", alone")
                + System.FormattableString.Invariant($"; way back {_way.Home.Travel:F1} m") + (_way.Swing ? ", swung out to it first" : "")
                + (inWay == null ? "; nothing stands on it" : "; '" + inWay.name + "' (layer " + LayerMask.LayerToName(inWay.gameObject.layer) + ") stands on it and could not be moved"));
            _care = Care.Wheeling;
            Phase(now, _way.Home.Travel / SceneClips.WalkPace + (_way.Swing ? 5f : 1f) + (final ? 5f : 1f));
        }

        // A traffic car only standing on the way back (no wreck, unit or player's car) is taken off the road; anything else
        // cannot be moved. True only while there is something to wait for.
        private bool StillWaiting(Collider inWay, float now)
        {
            if (_blockedSince <= 0f)
            {
                _blockedSince = now;
            }
            VehicleComponent car = inWay.GetComponentInParent<VehicleComponent>();
            bool movable = car != null && car.gameObject.activeInHierarchy && car.presetPath == null && !TrafficTools.IsAmbulance(car) && !TrafficTools.IsHeld(car)
                           && car.GetComponent<TaxiController>() == null && ((1 << inWay.gameObject.layer) & TrafficTools.TrafficMask) != 0;
            if (!movable || now - _blockedSince > 12f)
            {
                return false;
            }
            if (now - _blockedSince >= 5f || !StreetSpots.IsInView(car.transform.position))
            {
                Logger?.Info("People: '" + car.name + "' has come to stand on the ambulance crew's way back; it is taken off the road before they set off.");
                TrafficTools.Remove(car, "standing on the ambulance crew's way back to the ambulance");
            }
            return true;
        }

        private void LoadIn()
        {
            // Measured where it matters: from the end of the stretcher that leads to the rear doors themselves, and how square it stands to them.
            float gap = Vector3.Dot(_stretcher.LeadingEnd - _way.RearFace, _way.Back);
            float aside = Flat(_stretcher.LeadingEnd - _way.RearFace - _way.Back * gap).magnitude;
            float skew = Vector3.Angle(_stretcher.Heading, -_way.Back);
            Check("loaded", Mathf.Abs(gap - CrewWay.DoorGap) <= DoorsWithin && aside <= DoorsWithin && skew <= AngleWithin,
                System.FormattableString.Invariant(
                    $"the stretcher's leading end is {gap:F2} m from the rear doors ({CrewWay.DoorGap:F2} m wanted, {DoorsWithin:F2} m either way), {aside:F2} m off their middle, ")
                + System.FormattableString.Invariant($"{skew:F1} deg off square ({AngleWithin:F0} allowed)") + (_way.Square ? ", brought round behind the ambulance" : ", swung square on the spot")
                + "; crew aboard");
            if (_stretcher.Laden)
            {
                ICasualty patient = Patient;
                _stretcher.Unload();   // an animated body gets its animator back before its owner puts it away
                if (patient != null)
                {
                    patient.TakeAway();
                }
            }
            _wheeled = false;
            ReleaseWay();
            PutStretcherAway();
            ActorPool.PlayDoorSound(_ambulanceDoor);
            _medicA.Exit();
            _medicB.Exit();
            _care = Care.Away;
            _medicsWorked = true;
        }

        private void PutStretcherAway()
        {
            if (_stretcher != null)
            {
                _stretcher.PutAway();
            }
            _stretcher = null;
            _bearer = null;
            _lead = null;
            _rear = null;
            _treats = null;
        }
    }
}
