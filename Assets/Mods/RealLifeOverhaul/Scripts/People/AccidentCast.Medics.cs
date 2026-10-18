using System.Globalization;
using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The paramedics sent out: their way planned, the stretcher taken out, and the crew's self-checks.</summary>
    public sealed partial class AccidentCast
    {
        // Each part of sending the crew out, in milliseconds, for the log: the one frame of an accident that plans a way.
        private static readonly System.Diagnostics.Stopwatch OutWatch = new System.Diagnostics.Stopwatch();

        private float _msPlan, _msCrew, _msWheel, _msCordon, _msLane;

        private static float Lap()
        {
            float ms = (float)OutWatch.Elapsed.TotalMilliseconds;
            OutWatch.Restart();
            return ms;
        }

        private float MedicsOut(VehicleComponent ambulance, float latest, float now)
        {
            using var piece = SessionWatch.Piece("accident: the ambulance crew being sent out (their way planned, the cordon run round it, a lane shut)");
            _msPlan = _msCrew = _msWheel = _msCordon = _msLane = 0f;
            OutWatch.Restart();
            // Out at the rear doors of their own vehicle; a side door or the road beside it if the rear is walled in.
            Vector3 forward = ambulance.transform.forward;
            forward.y = 0f;
            _ambulanceBack = forward.sqrMagnitude < 0.01f ? Vector3.back : -forward.normalized;
            if (!StreetSpots.BehindCar(ambulance, 1.3f, out _ambulanceDoor)
                && !StreetSpots.BesideCar(ambulance, StreetSpots.Right, 1f, out _ambulanceDoor)
                && !StreetSpots.BesideCar(ambulance, StreetSpots.Left, 1f, out _ambulanceDoor))
            {
                Logger?.Warn("People: no clear ground by the ambulance for its crew to step out onto; the paramedics stay aboard.");
                return 0f;
            }
            ICasualty patient = Patient;
            bool down = patient != null && patient.Present;   // someone lying there: the stretcher comes out, and both of the crew if they can be had

            // For someone lying there everything is fixed before anyone steps out: how they lie, the layout beside them
            // (CrewWay.Lay) and the one way to it and back (CrewWay.Plan).
            Vector3 from = _ambulanceDoor;
            Lie(patient, from, out _patientAt, out Vector3 axis, out _);
            _cpr = false;
            _stretcher = null;
            _bearer = null;
            _lead = null;
            _rear = null;
            _medicsWorked = false;
            _wheeled = false;
            _hurried = false;
            _gateSaid = false;
            _blockedSince = 0f;
            ReleaseWay();
            _way.Clear();
            bool planned = false;
            if (down)
            {
                // Compressions for someone who is dying or dead, kneeling work for anyone else; the first
                // from right beside the chest (the clip's hands work 0.40 m in front), the second a little off.
                _cpr = _fatal || _patient == null;
                bool beside = _patient == null && _hasWreck && Flat(_wreckAt - _patientAt).sqrMagnitude < 4f * 4f;
                _way.Lay(_patientAt, axis, from, _cpr ? CprOut : KneelOut, _cpr ? 0f : 0.25f, KneelOut, beside, _wreckAt);
                _stretcher = Stretcher.Take();
                planned = _stretcher != null && _way.Plan(ambulance, _plan);
                _msPlan = Lap();
                _kneelAt = _way.Kneel;   // after the plan: the stretcher may stand on the other side of them from the one first chosen
                _secondKneel = _way.Second;
                if (!planned)
                {
                    Check("set off", false, _stretcher == null ? "there is no stretcher to be had this session"
                        : System.FormattableString.Invariant($"no way for the stretcher could be planned ({_way.Why}); rear doors to its place {_way.Straight:F1} m straight")
                          + (_way.Failed != null ? "; its place failed its clear test: " + _way.Failed : "")
                          + "; the stretcher stays in the ambulance, the crew goes on foot with the bag and treats where the casualty lies");
                    _stretcher = null;
                }
            }

            // With the stretcher: whoever pushes it steps out on the middle line behind the doors (it comes out sideways,
            // towards the side of the ambulance the way runs along); the other at its leading end, where they will walk.
            Vector3 right = ambulance.transform.right;
            Vector3 second = planned ? _way.SecondOut : _ambulanceDoor;
            Vector3 pusher = planned ? _way.Door : _ambulanceDoor + right * 0.9f;
            OutWatch.Restart();
            bool gotA = _medicA.Enter(second, ActorLook.Paramedic, 2f, onRoad: true);
            bool gotB = (down || Random.value < SecondCrewChance) && _medicB.Enter(pusher, ActorLook.Paramedic, 2f, onRoad: true);
            _msCrew = Lap();
            if (!gotA && !gotB)
            {
                _stretcher = null;
                Logger?.Warn("People: nobody could be had to play the paramedics (every character is in use, or there is no ground to stand on).");
                return 0f;
            }
            ActorPool.PlayDoorSound(_ambulanceDoor);
            if (!down)
            {
                _patientAt = _driverA.Present ? _driverA.Position : _hasA ? _doorA : _scene;
            }
            float straight = Vector3.Distance(_patientAt, from);
            float distance = straight * Detour;
            float reachFor = ReachFor(distance, false);

            // Whoever treats has the medic bag: in the hand on foot, on the bed while wheeling, on the ground while kneeling.
            ActorRole treats = gotA ? _medicA : _medicB;
            _treats = treats;
            treats.CarryBag(true);
            bool paced = false;   // somebody walks in a clip made for one pace: the crew keeps to it, and is given the time
            if (down && planned)
            {
                // One pushing, the other (if two) walked at its leading end, the bag on it: the stretcher moves them all along
                // the way, and only the end of its line ends this step.
                _bearer = gotB ? _medicB : _medicA;
                ActorRole other = gotA && gotB ? _medicA : null;
                OutWatch.Restart();
                _wheeled = _stretcher.Wheel(_bearer, other, _way.Out, _way.Guide, _way.Handle, _way.Swing, _way.Bed, _way.Heading);
                _msWheel = Lap();
                if (_wheeled)
                {
                    paced = true;
                    distance = _way.Out.Travel;
                    if (_plan != null)
                    {
                        _plan.SetCorridor(_way.Out, _way.Home);   // the tape opens where it crosses; no post, cone or barrier stands on it
                        _msCordon = Lap();
                        HoldLane();
                        _msLane = Lap();
                    }
                    SetOffCheck(gotA && gotB);
                }
                else
                {
                    Check("set off", false, "the stretcher could not be taken out (whoever was to push it cannot be moved); the crew goes on foot with the bag");
                    planned = false;
                    _stretcher = null;
                }
            }
            if (down && !_wheeled)
            {
                paced = ToTheChest(treats, Pace.Jog, reachFor);
                reachFor = ReachFor(distance, paced);
                if (gotA && gotB)
                {
                    _goB = _secondKneel;
                    Send(_medicB, _secondKneel, paced ? Pace.Walk : Pace.Jog, _patientAt, reachFor);
                    _medicB.Kneel();
                }
            }
            else if (!down)
            {
                Vector3 near = Towards(_patientAt, from, 1.4f);
                Vector3 apart = Vector3.Cross(Vector3.up, Flat(_patientAt - from).normalized) * 0.8f;
                paced = treats.Gait(SceneClips.CarryBag);
                reachFor = ReachFor(distance, paced);
                Pace pace = paced ? Pace.Walk : Pace.Jog;
                _goA = Stand(near - apart, "paramedic seeing to the driver");
                _goB = Stand(near + apart, "second paramedic seeing to the driver");
                _medicA.Go(_goA, pace, _patientAt, reachFor);
                _medicB.Go(_goB, pace, _patientAt, reachFor);
            }

            float home = _wheeled ? _way.Home.Travel + (_way.Swing ? 4f : 0f) + (_way.Square ? 0f : 4f) : distance;
            float back = home / WheelSpeed + (down ? LiftAllowance + 8f : 3f);
            float reach = distance / (paced ? SceneClips.WalkPace : JogSpeed) + (_wheeled && _way.Swing ? 6f : 2f);
            float treat = _fatal ? Random.Range(5f, 8f) : down ? Random.Range(9f, 16f) : Random.Range(8f, 14f);
            _careLatest = Mathf.Max(now + reach + 4f, latest - back);
            _careUntil = now + reachFor;
            _care = Care.Out;
            _treatFor = treat;
            Phase(now, reach);
            Logger?.Info($"People: paramedics out ({(gotA && gotB ? 2 : 1)}){(_stretcher != null ? " with the stretcher" : "")}, {straight:F0} m from "
                         + (down ? "the casualty" : "the driver") + (paced ? System.FormattableString.Invariant($", at the {SceneClips.WalkPace:F1} m/s their walking clips are made for") : "")
                         + System.FormattableString.Invariant(
                             $"; back aboard in about {reach + treat + back:F0} s. Sending them out took: the way {_msPlan:F1} ms ({_way.Cost}), the two characters {_msCrew:F0} ms, ")
                         + $"the stretcher taken {_msWheel:F0} ms, the cordon run round it {_msCordon:F0} ms, the lane shut {_msLane:F0} ms.");
            return now + reach + treat + back;
        }

        // The crew's self-check, one line a step, each with what was measured against what is wanted and PASS or FAIL:
        // set off, through the opening, parked beside, lifted, set off back, loaded. A run can be judged from these alone.
        private static void Check(string step, bool pass, string measured)
        {
            string line = "People: crew check (" + step + "): " + measured + ": " + (pass ? "PASS." : "FAIL.");
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }

        private const float PlaceWithin = 0.1f;    // metres the stretcher may stand from where it belongs...

        private const float AngleWithin = 3f;      // ...and degrees it may be off the way it should point

        private const float HandleWithin = 0.25f;  // metres someone may stand from the end of the stretcher they are to take

        private const float DoorsWithin = 0.15f;   // metres its leading end may be off the 0.25 m it is loaded from

        private const float TapeWithin = 0.18f;    // metres anything of the cordon must keep from the crew at the least (0.30 is laid out)

        // Step 1: the way as planned, and that the stretcher and whoever pushes it start on it.
        private void SetOffCheck(bool two)
        {
            Vector3 pusher = _bearer.Position;
            Vector3 bed = _stretcher.transform.position;
            _way.Out.Pose(0f, out float pusherX, out float pusherZ, out float bedX, out float bedZ, out _, out _);
            float pusherOff = Flat(pusher - new Vector3(pusherX, 0f, pusherZ)).magnitude;
            float bedOff = Flat(bed - new Vector3(bedX, 0f, bedZ)).magnitude;
            bool clearAhead = _way.Between == null;
            bool pass = pusherOff <= PlaceWithin && bedOff <= PlaceWithin && clearAhead && _way.HomeChecked;
            Check("set off", pass,
                System.FormattableString.Invariant($"way out {_way.Out.Travel:F1} m with {_way.Out.Corners} corner(s) rounded to 1.2 m")
                + (_way.Swing ? ", then a swing-in from 2.1 m off the casualty's line" : ", a run-in along the casualty")
                + System.FormattableString.Invariant($", way back {_way.Home.Travel:F1} m with {_way.Home.Corners}")
                + (_way.Square ? ", round behind the ambulance to the doors square" : ", swung square at the doors (no room to turn behind)")
                + (_way.HomeChecked ? "" : " (NOT clear when planned: something stands behind the ambulance)")
                + System.FormattableString.Invariant($"; rear doors to the stretcher's place {_way.Straight:F1} m straight; by the ambulance's {(_way.AwaySide < 0f ? "left" : "right")} side")
                + (_way.Tier == 0 ? ", on the carriageway" : ", wherever there was room (not all of it carriageway)")
                + (_way.OpenLane ? ", through the lane traffic is let past in (held shut meanwhile)" : "")
                + (_way.BySide ? "; the stretcher goes to the other side of the casualty from the one first chosen (the only one with a clear way)" : "")
                + (_way.DoorsToTape >= 0f
                    ? System.FormattableString.Invariant($"; rear doors {_way.DoorsToTape:F1} m from the tape")
                      + (clearAhead ? ", nothing between the ambulance and it" : $", and {_way.Between} stands between the ambulance and it")
                    : "; no tape between the ambulance and the casualty")
                + System.FormattableString.Invariant($"; every pose tested against vehicles, fixtures, the tape and the casualty; {(two ? "two" : "one")} of the crew; ")
                + System.FormattableString.Invariant($"whoever pushes starts {pusherOff:F2} m from the line, the stretcher {bedOff:F2} m ({PlaceWithin:F2} m allowed)")
                + (_way.Failed != null ? "; (the side first chosen failed its clear test: " + _way.Failed + ")" : ""));
        }

        // Step 2 (and 5 b): said once, when the bed's middle crosses the tape.
        private void GateCheck(float gate, string step)
        {
            if (_gateSaid || gate < 0f || _stretcher == null || _stretcher.Progress < gate)
            {
                return;
            }
            _gateSaid = true;
            float clear = _plan != null ? _plan.CorridorClear : -1f;
            bool taped = _plan != null && _plan.Up && !_plan.Lowered;
            Check(step, !taped || clear >= TapeWithin,
                !taped ? "the stretcher crosses where the tape will run; none is up yet"
                    : System.FormattableString.Invariant(
                        $"the stretcher crosses the tape by its opening; the nearest post, cone, barrier or tape is {clear:F2} m from anything of the crew ({TapeWithin:F2} m at least)"));
        }

        // Only for finishing a step at once when it is well overdue and nobody can see.
        private void Phase(float now, float seconds)
        {
            _phaseAt = now;
            _phaseFor = seconds;
        }

        // Out of sight (both of the crew hidden by the pool) and well past its time, a step is finished where it would have
        // ended; never in sight of the player.
        private void Overdue(float now)
        {
            if (_stretcher == null || now < _phaseAt + _phaseFor + 8f || _medicA.Visible || _medicB.Visible)
            {
                return;
            }
            _stretcher.Hurry();
            _stretcher.HurryWalks();
            if (!_hurried)
            {
                _hurried = true;
                Logger?.Info("People: the ambulance crew is out of sight and behind its time; the step it was on is finished where it would have ended. (Said once an outing.)");
            }
        }

        // The lane traffic is let past in is the crew's for as long as the stretcher is in it (only if the way has to use it).
        private void HoldLane()
        {
            if (_way.OpenLane && _plan != null && !_heldLane)
            {
                _heldLane = true;
                _plan.HoldForCrew(true);
            }
        }

        private void GiveLaneBack()
        {
            if (_heldLane && _plan != null)
            {
                _plan.HoldForCrew(false);
            }
            _heldLane = false;
        }

        private void ReleaseWay()
        {
            GiveLaneBack();
            if (_plan != null)
            {
                _plan.SetCorridor(null, null);
            }
        }

        // To a place beside the casualty or the stretcher, round a corner of the bed and the casualty's head or feet where the
        // straight line would cross either: walked by the stretcher when it is out, by their own agent only without one.
        private void Send(ActorRole who, Vector3 to, Pace pace, Vector3 face, float allow)
        {
            Vector3 at = who.Position;
            bool haveBed = _stretcher != null && _stretcher.Out;
            Vector3 corner = default;
            bool roundBed = haveBed && CrewWay.RoundBed(_stretcher.transform, at, to, out corner);
            bool round = _way.Round(roundBed ? corner : at, to, out Vector3 via);
            if (haveBed && _stretcher.Walk(who, roundBed, corner, round, via, to, face))
            {
                return;
            }
            if (round)
            {
                who.GoVia(via, to, pace, face, allow);
            }
            else
            {
                who.Go(to, pace, face, allow);
            }
        }

        // Seconds a crew is given to reach the people over a distance: jogging, or held to the pace of a walking clip.
        private static float ReachFor(float distance, bool paced)
        {
            return Mathf.Max(ReachMinimum, distance / (paced ? SceneClips.WalkPace : JogSpeed) + 6f);
        }

        // To the casualty's chest with the bag, and down beside it. True if they walk there in the bag-carrying
        // clip, which holds them to its pace.
        private bool ToTheChest(ActorRole medic, Pace pace, float allow)
        {
            bool paced = medic == _treats && medic.Gait(SceneClips.CarryBag);   // only whoever has the bag walks as if carrying one
            if (medic == _medicA)
            {
                _goA = _kneelAt;
            }
            else
            {
                _goB = _kneelAt;
            }
            Send(medic, _kneelAt, paced ? Pace.Walk : pace, _patientAt, paced ? Mathf.Max(allow, ReachFor(Flat(_kneelAt - medic.Position).magnitude * Detour, true)) : allow);
            if (_cpr)
            {
                medic.Kneel(SceneClips.Cpr, SceneClips.CprReach);
            }
            else
            {
                medic.Kneel();
            }
            return paced;
        }
    }
}
