using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Traffic accidents: the way out, planned before the units arrive and checked as they park and set off.</summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private const float AmbulanceHalf = 1.25f;      // metres: half an ambulance's width, before one is on the road
        private const float PatrolHalf = 1.05f;         // ...and a patrol car's
        private const float PassingRoom = 0.4f;         // metres left between the two when the ambulance drives out past the patrol car

        private static void DepartureCheck(string step, bool pass, string measured)
        {
            string line = "Accident: departure check (" + step + "): " + measured + ": " + (pass ? "PASS." : "FAIL.");
            if (pass)
            {
                Logger?.Info(line);
            }
            else
            {
                Logger?.Warn(line);
            }
        }

        // Decided before either unit is on the road: the loaded ambulance leaves forwards along its own lane through the scene.
        // A patrol car at the far end must stand clear of that line by both half widths and room to pass; where the road does
        // not allow it, the patrol car comes up the ambulance's lane behind it instead.
        private void PlanDeparture(bool ambulance, bool police)
        {
            if (!ambulance || !police)
            {
                DepartureCheck("planned", true, ambulance ? "only the ambulance attends: it leaves forwards along its own lane, nothing of the mod's ahead of it"
                    : police ? "only the patrol car attends: it leaves forwards along its own lane when the scene clears" : "no unit attends");
                return;
            }
            if (!_police.FarEnd)
            {
                DepartureCheck("planned", true, "both units come up the same lane, the ambulance first and nearest the scene, the patrol car behind it: "
                    + "the ambulance leaves forwards through the scene, the patrol car follows when the scene clears");
                return;
            }
            float need = AmbulanceHalf + PatrolHalf + PassingRoom;
            float clear = ExitClearance(out float along);
            if (clear >= need)
            {
                DepartureCheck("planned", true, $"the ambulance leaves forwards along its own lane through the scene; the patrol car stands at the far end in the lane running the other way, "
                    + FormattableString.Invariant($"{clear:F1} m from the middle of the ambulance's way out, {along:F0} m along it ({need:F1} m needed: both half widths and {PassingRoom:F1} m); ")
                    + "order: the ambulance first, the patrol car when it is past");
                return;
            }
            _police.FarEnd = false;
            if (_chase != Chase.Planned)
            {
                _police.At = Mathf.Max(_police.At, _ambulance.At + 5f);
            }
            DepartureCheck("planned", true, (clear < 0f ? "no place for the patrol car at the far end could be measured against the ambulance's way out"
                    : FormattableString.Invariant($"a patrol car at the far end would stand {clear:F1} m from the middle of the ambulance's way out ({need:F1} m needed)"))
                + "; it is sent up the ambulance's own lane behind it instead, after it: nothing of the mod's stands ahead of the ambulance");
        }

        // Metres from where a patrol car would stand at the far end to the middle line of the ambulance's way out (its own
        // lane on from where it stands, as the road graph has it), and how far along that way; -1 if either cannot be had.
        private float ExitClearance(out float along)
        {
            along = 0f;
            if (!_cordon.Stand(false, AmbulanceBack, out Waypoint anchor, out _) || !_cordon.Stand(true, PatrolBackAlone, out _, out Vector3 patrol))
            {
                return -1f;
            }
            float least = float.MaxValue;
            // Every way the lane goes on is measured: at a junction the way out may take any of them.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                TrafficTools.WalkForward(anchor, 110f, 70, _routeScratch);
                float walked = 0f;
                for (int i = 1; i < _routeScratch.Count; i++)
                {
                    Vector3 from = _routeScratch[i - 1].position;
                    Vector3 leg = _routeScratch[i].position - from;
                    leg.y = 0f;
                    float length = leg.magnitude;
                    if (length < 0.05f)
                    {
                        continue;
                    }
                    Vector3 offset = patrol - from;
                    offset.y = 0f;
                    float t = Mathf.Clamp(Vector3.Dot(offset, leg) / length, 0f, length);
                    float distance = (offset - leg * (t / length)).magnitude;
                    if (distance < least)
                    {
                        least = distance;
                        along = walked + t;
                    }
                    walked += length;
                }
            }
            _routeScratch.Clear();
            return least == float.MaxValue ? -1f : least;
        }

        // Both units are standing at the scene: where the patrol car really is against the lane the ambulance will leave by,
        // measured body to body. (If it does stand in that lane after all, it is moved when the ambulance goes: GiveWay.)
        private void ParkedCheck()
        {
            if (_ambulance.Stage != Stage.OnScene || _police.Stage != Stage.OnScene || _ambulance.Unit.Car == null || _police.Unit.Car == null)
            {
                return;
            }
            TrafficTools.Footprint(_ambulance.Unit.Car, out Vector3 centre, out Vector3 forward, out _, out float halfWidth);
            TrafficTools.Footprint(_police.Unit.Car, out Vector3 theirs, out Vector3 theirForward, out _, out float theirHalf);
            Vector3 offset = theirs - centre;
            offset.y = 0f;
            float along = Vector3.Dot(offset, forward);
            float aside = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward))) - halfWidth - theirHalf;
            bool ahead = along > 0f;
            bool facing = Vector3.Dot(theirForward, forward) < 0f;
            DepartureCheck("units parked", !ahead || aside >= ExitMargin,
                ahead ? FormattableString.Invariant($"the patrol car stands {along:F0} m ahead of the ambulance, {(facing ? "facing it" : "facing the same way")}, ")
                      + FormattableString.Invariant($"{aside:F1} m clear of the side of the lane it will leave by ({ExitMargin:F2} m needed)")
                      + (aside >= ExitMargin ? "" : ": it is moved out of that lane when the ambulance goes")
                    : $"the patrol car stands {-along:F0} m behind the ambulance: nothing of the mod's is ahead of it");
        }

        // The departure check as a unit sets off: what stands in the lane it leaves by (as far as the far side of the scene),
        // where the other unit is against that lane, and whether the order is being kept.
        private void SetOffCheck(Attendance attendance, Vector3 at, Vector3 facing, float through)
        {
            if (_phase == Phase.None && !attendance.Through)
            {
                return;   // driving off an open road after the scene: nothing to check
            }
            VehicleComponent car = attendance.Unit.Car;
            Attendance other = attendance.Police ? _ambulance : _police;
            TrafficTools.Footprint(car, out Vector3 centre, out _, out float halfLength, out float halfWidth);
            float reach = Mathf.Clamp(through + 6f, halfLength + 8f, 70f);
            Collider ahead = SceneParking.InStrip(centre + facing * (halfLength + 0.3f), centre + facing * reach, halfWidth + ExitMargin, car.rb, SceneParking.RoadsideMask);
            bool otherThere = other.Unit.InUse && other.Unit.Car != null && !other.Past;
            string where = "the other unit is not at the scene";
            bool otherAhead = false;
            if (otherThere)
            {
                TrafficTools.Footprint(other.Unit.Car, out Vector3 theirs, out _, out _, out float theirHalf);
                Vector3 offset = theirs - centre;
                offset.y = 0f;
                float along = Vector3.Dot(offset, facing);
                float aside = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, facing))) - halfWidth - theirHalf;
                otherAhead = along > 0f && along < reach + 6f && aside < ExitMargin;
                where = FormattableString.Invariant($"the {Name(other)} is {along:F0} m {(along >= 0f ? "ahead" : "behind")}, {aside:F1} m clear of its side ({ExitMargin:F2} m needed)");
            }
            bool order = !attendance.Police || _ambulance.Past;
            string step = Name(attendance) + " set off";
            DepartureCheck(step, ahead == null && !otherAhead && order,
                (attendance.OwnLane || attendance.Police ? "by its own lane, which it keeps to" : "round the wrecks by the lane beside them")
                + (ahead == null ? $"; nothing stands in it for the {reach:F0} m to beyond the scene"
                    : $"; '{ahead.name}' (layer {LayerMask.LayerToName(ahead.gameObject.layer)}) stands in it: it closes up, and what is in the way is moved")
                + "; " + where
                + (attendance.Police ? (order ? "; the ambulance is past the scene" : "; the ambulance is NOT yet past the scene") : "; the ambulance goes first")
                + (_cordon.Closed ? "; the road is still closed to traffic" : "; the road is open"));
        }
    }
}
