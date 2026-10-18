using System.Globalization;
using BAModAPI;
using BigAmbitions.Characters;
using GleyTrafficSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The officers out, the brief they work from, and where people at the scene may stand and kneel.</summary>
    public sealed partial class AccidentCast
    {
        private float OfficersOut(VehicleComponent patrol, float now)
        {
            bool doorA = StreetSpots.BesideCar(patrol, StreetSpots.Left, 1f, out _patrolDoorA);
            bool doorB = StreetSpots.BesideCar(patrol, StreetSpots.Right, 1f, out _patrolDoorB);
            if (!doorA && !doorB)
            {
                Logger?.Warn("People: no clear ground by the patrol car for its crew to step out onto; the officers stay aboard.");
                return 0f;
            }
            if (!doorA)
            {
                _patrolDoorA = _patrolDoorB;
            }
            if (!doorB)
            {
                _patrolDoorB = _patrolDoorA;
            }
            // Two officers whenever two can be had: one for the scene, one for the traffic.
            bool gotA = _officerA.Role.Enter(_patrolDoorA, ActorLook.Officer, 2f, onRoad: true);
            bool gotB = doorB && _officerB.Role.Enter(_patrolDoorB, ActorLook.Officer, 2f, onRoad: true);
            if (!gotA && !gotB)
            {
                Logger?.Warn("People: nobody could be had to play the officers (every character is in use, or there is no ground to stand on).");
                return 0f;
            }
            ActorPool.PlayDoorSound(_patrolDoorA);

            _statements = false;
            _officers = Crew.Working;
            _faultFrom = now + Random.Range(BookAfterLeast, BookAfterMost);
            Brief();
            _officerB.OnTraffic = gotB;
            _officerB.Alone = !gotA;
            _officerA.OnTraffic = !gotB;   // alone, the one officer divides their time
            _officerA.Alone = !gotB;
            if (gotA)
            {
                _officerA.Begin(_brief, now);
            }
            if (gotB)
            {
                _officerB.Begin(_brief, now);
            }
            Logger?.Info($"People: officers out ({(gotA && gotB ? 2 : 1)}), {Vector3.Distance(_patrolDoorA, _scene):F0} m from the scene.");
            return 0f;   // they work until they are called back
        }

        private void Brief()
        {
            ICasualty patient = Patient;
            _brief.Scene = _scene;
            _brief.HasBody = patient != null && patient.Present && (_stretcher == null || !_stretcher.Laden);
            if (!_brief.HasBody)
            {
                _brief.Body = _scene;
                _brief.BodyKneel = _scene;
                _kneelKnown = false;
            }
            else if (!_kneelKnown || (patient.Position - _kneelFor).sqrMagnitude > 0.25f)
            {
                // Worked out when they come to rest and again only if they are moved. Paramedics work on
                // the side help comes up on; anyone else kneels on the other side of them.
                _kneelKnown = true;
                _kneelFor = patient.Position;
                Lie(patient, _scene - _roadForward * 15f, out Vector3 chest, out Vector3 axis, out Vector3 side);
                _brief.Body = chest;
                // Beside the driver's own car the far side of them is the car: the officer kneels at their head instead.
                // The same where a held vehicle stands within a metre of that side (someone knocked down beside the car that hit them).
                side = CrewSide(chest, axis, side);
                bool carBeside = (_patient == null && _hasWreck && Flat(_wreckAt - chest).sqrMagnitude < 3.5f * 3.5f)
                                 || VehicleWithin(chest - side * KneelOut + axis * 0.2f, chest - side * KneelOut + axis * 0.2f, VehicleClear);
                _brief.BodyKneel = Stand(carBeside ? chest + axis * 1.05f : chest - side * KneelOut + axis * 0.2f, "officer looking the casualty over");
            }
            _brief.BodyAttended = _care == Care.Settling || _care == Care.Treating || _care == Care.Lifting || _care == Care.Loading
                                  || _care == Care.Clearing || _care == Care.Wheeling || (_care == Care.Out && (_medicA.Settled || _medicB.Settled));
            _brief.CrewOut = _care != Care.Away;
            bool witnessB = _driverB.Present;
            bool witnessA = !witnessB && _driverA.Present && !_lying;
            _brief.HasWitness = witnessA || witnessB;
            _brief.Witness = witnessB ? _driverB.Position : witnessA ? _driverA.Position : _scene;
            _brief.CanMark = _plan != null && _plan.HasImpact && _markers < MarkersWanted;
            if (_brief.CanMark)
            {
                _brief.Evidence = _plan.Evidence(_markers);
            }
            _brief.HasCordon = _cordon != null && _cordon.Up;
            if (_brief.HasCordon)
            {
                _brief.TrafficPost = _cordon.DirectorPost;
                _brief.EntryPost = _cordon.EntryPost;
                _brief.EntryLook = _cordon.EntryLook;
                _brief.Oncoming = _cordon.Oncoming;
                _brief.Kerbside = _cordon.Kerbside;
                for (int i = 0; i < 4; i++)
                {
                    _brief.Corners[i] = _cordon.Corner(i);
                }
            }
            else
            {
                _brief.Oncoming = _scene - _roadForward * 20f;
                _brief.Kerbside = _plan != null && _plan.Surveyed ? _plan.Kerbside : _scene;   // measured when the road was closed, tape or no tape
            }
            // Whoever waits outside the tape does not wait on the ambulance crew's way: the corner of the taped area furthest from it instead.
            if (_way.Planned && _plan != null && _plan.Surveyed && OnTheWay(_brief.Kerbside))
            {
                float most = 0f;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = _plan.Corner(i);
                    float clear = Mathf.Min(_way.Out.Clearance(corner.x, corner.z), _way.Home.Clearance(corner.x, corner.z));
                    if (clear > most)
                    {
                        most = clear;
                        _brief.Kerbside = corner;
                    }
                }
            }
        }

        private bool OnTheWay(Vector3 place)
        {
            return Mathf.Min(_way.Out.Clearance(place.x, place.z), _way.Home.Clearance(place.x, place.z)) < 1f;
        }

        // How someone lies: their chest, the way the body runs (hips to head) and the side facing a place. Someone who cannot
        // say is a point, lying across the line to that place.
        private static void Lie(ICasualty patient, Vector3 facing, out Vector3 chest, out Vector3 axis, out Vector3 side)
        {
            chest = patient != null ? patient.Position : facing;
            axis = Vector3.zero;
            if (patient is ILyingBody body && body.Spine(out Vector3 hips, out Vector3 head))
            {
                axis = Flat(head - hips);
                chest = hips + (head - hips) * 0.55f;
                chest.y = Mathf.Min(hips.y, head.y);
            }
            Vector3 towards = Flat(facing - chest);
            towards = towards.sqrMagnitude < 0.01f ? Vector3.forward : towards.normalized;
            axis = axis.sqrMagnitude < 0.01f ? Vector3.Cross(Vector3.up, towards) : axis.normalized;
            side = Vector3.Cross(Vector3.up, axis);
            if (Vector3.Dot(side, towards) < 0f)
            {
                side = -side;
            }
        }

        private const float VehicleClear = 1f;   // metres kept between any vehicle and where a crew kneels, parks the stretcher and walks between the two

        // The side of someone lying down that the crew works on: the side help comes up on, unless a vehicle stands within a
        // metre of where the crew would kneel and the stretcher wait and the other side is clear; with room on neither, the
        // side away from the casualty's own car.
        private Vector3 CrewSide(Vector3 chest, Vector3 axis, Vector3 side)
        {
            if (CrewRoom(chest, axis, side))
            {
                return side;
            }
            if (CrewRoom(chest, axis, -side))
            {
                if (!_sideLogged)
                {
                    _sideLogged = true;
                    Logger?.Info("People: a vehicle stands within " + VehicleClear.ToString("F0") + " m of the casualty on the side help comes up on; the crew works on the other side of them.");
                }
                return -side;
            }
            if (!_sideLogged)
            {
                _sideLogged = true;
                Logger?.Info("People: vehicles stand within " + VehicleClear.ToString("F0") + " m of the casualty on both sides; the crew works where it can.");
            }
            return _patient == null && _hasWreck && Vector3.Dot(side, Flat(_wreckAt - chest)) > 0f ? -side : side;
        }

        // Kneeling at the chest, the stretcher beside the legs, and the way between the two.
        private static bool CrewRoom(Vector3 chest, Vector3 axis, Vector3 side)
        {
            return !VehicleWithin(chest + side * KneelOut + axis * 0.25f, chest + side * StretcherOut - axis * 0.9f, VehicleClear + 0.35f);
        }

        private static bool VehicleWithin(Vector3 from, Vector3 to, float distance)
        {
            int vehicles = TrafficTools.VehicleMask;
            return vehicles != 0 && Physics.CheckCapsule(from + Vector3.up * 0.9f, to + Vector3.up * 0.9f, distance, vehicles, QueryTriggerInteraction.Ignore);
        }

        private bool _sideLogged;   // which side the crew works on has been said for this scene

        // The nearest place someone can stand: inside the tape's rectangle, on the street and out of any vehicle. The first
        // place of a scene that is not clear as planned is logged with what was in it.
        private Vector3 Stand(Vector3 wanted, string who)
        {
            Vector3 planned = wanted;
            if (_plan != null)
            {
                wanted = _plan.KeepInside(wanted, 0.75f);
            }
            bool onStreet = StreetSpots.OnRoad(wanted, 1.5f, out Vector3 spot);
            bool found = onStreet && StreetSpots.OutOfVehicles(ref spot);
            wanted = planned;
            float moved = found ? Flat(spot - wanted).magnitude : 0f;
            if (!_standLogged && (!found || moved > StandSlack))
            {
                _standLogged = true;
                if (_standMask == 0)
                {
                    _standMask = TrafficTools.VehicleMask | LayerMask.GetMask("StreetProps");
                }
                int count = _standMask != 0 ? Physics.OverlapSphereNonAlloc(wanted + Vector3.up * 0.9f, 0.45f, StandHits, _standMask, QueryTriggerInteraction.Ignore) : 0;
                string what = "no walkable road there: a vehicle's cut-out, the tape's wall or the kerb";
                if (count > 0)
                {
                    what = "it is inside '" + StandHits[0].name + "' (layer " + LayerMask.LayerToName(StandHits[0].gameObject.layer) + ")";
                }
                for (int i = 0; i < count; i++)
                {
                    StandHits[i] = null;
                }
                string then = found ? "they stand " + moved.ToString("F1", CultureInfo.InvariantCulture) + " m from it instead." : "nowhere clear within 1.5 m, so they make for it as planned.";
                Logger?.Info("People: the place planned for the " + who + " failed its clear test: " + what + "; " + then + " (First of this scene.)");
            }
            return onStreet ? spot : wanted;
        }

        private const float StandSlack = 0.6f;   // metres a place to stand may be moved before it counts as not clear as planned

        private static readonly Collider[] StandHits = new Collider[4];

        private static int _standMask;

        private bool _standLogged;

        private static Vector3 Towards(Vector3 target, Vector3 from, float shortBy)
        {
            Vector3 back = Flat(from - target);
            return back.sqrMagnitude < 0.01f ? target : target + back.normalized * shortBy;
        }
    }
}
