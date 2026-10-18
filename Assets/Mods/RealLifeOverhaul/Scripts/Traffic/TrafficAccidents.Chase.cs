using System;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Localizor;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Traffic accidents: the hit-and-run, a driver who bolts when the patrol car turns up.</summary>
    public sealed partial class TrafficAccidents : MonoBehaviour
    {
        private const float FleeRouteLength = 650f;     // metres
        private const float FleeRouteMinimum = 250f;    // metres
        private const float FleeTriggerDistance = 45f;  // metres: the suspect bolts when the patrol car gets this close
        private const float SuspectSpeed = 12.5f;       // m/s
        private const float ChaseSpeed = 15.5f;         // m/s: the patrol car is a little faster
        private const float ChaseGap = 9f;              // metres

        private void PlanChase(VehicleComponent car)
        {
            try
            {
                // The lane point ahead of its nose for a car out of the traffic system's hands a while; else the one it was heading for.
                Vector3 nose = car.transform.forward;
                nose.y = 0f;
                Waypoint anchor = _inPlace && nose.sqrMagnitude > 0.01f
                    ? TrafficTools.ClosestWaypointAlong(car.transform.position + nose.normalized * (Mathf.Max(1.5f, car.length * 0.5f) + 3f), nose.normalized * 2f, 12f)
                    : TrafficManager.Instance.GetTargetWaypoint(car.GetIndex());
                if (anchor == null || TrafficTools.WalkForward(anchor, FleeRouteLength, 500, _routeScratch) < FleeRouteMinimum)
                {
                    return;
                }
                _fleePath.Clear();
                for (int i = 0; i < _routeScratch.Count; i++)
                {
                    _fleePath.Add(_routeScratch[i].position);
                }
                _routeScratch.Clear();
                _fleeAnchor = anchor;
                _chase = Chase.Planned;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _chase = Chase.None;
            }
        }

        private void BeginChase(float now)
        {
            if (_leadWreck == null || _leadWreck.Car == null)
            {
                _chase = Chase.None;
                return;
            }
            VehicleComponent car = _leadWreck.Car;
            _people.OnDriverFled();   // back behind the wheel
            Unpin(_leadWreck);
            _wrecks.Remove(_leadWreck);
            _leadWreck = null;
            if (_fleePath.Count == 0 || !_suspect.Take(car))
            {
                TrafficTools.ReturnToTraffic(car, "the accident scene: the hit-and-run could not start");
                _chase = Chase.None;
                return;
            }

            _suspect.Path.AddRange(_fleePath);
            EmergencyDriver flee = _suspect.Driver;
            flee.Begin(car, _suspect.Path, _fleePath[_fleePath.Count - 1], 6f);
            flee.CruiseSpeed = SuspectSpeed;

            ResponseUnit patrol = _police.Unit;
            patrol.Path.AddRange(_fleePath);
            patrol.Driver.Follow(car.rb, ChaseGap);
            patrol.Driver.CruiseSpeed = ChaseSpeed;

            _police.Stage = Stage.Chasing;
            _chase = Chase.Running;
            _fled = true;
            bool caught = Random.value < 0.55f;
            _pursuit.Begin(_suspect, patrol, _arrest, caught, Mathf.Min(now + Random.Range(20f, 45f), _deadline - 22f), arrest: true, "reallife_chase_caught");
            News.Report(NewsKind.HitAndRun, _scene);
            Logger?.Info($"Accident: hit-and-run, {car.prefab.name} is running; will be {(caught ? "caught" : "lost")}.");
            Notice(NotificationType.Warning, "reallife_chase_start", 8f);
        }

        // The chase itself is RoadChase's, as for a reckless driver the police have seen.
        private void TickChase(float now)
        {
            if (_pursuit.Tick(now, out bool caught))
            {
                EndChase(caught, now);
            }
        }

        // Caught: the suspect's car is taken away. Escaped: it melts back into traffic.
        private void EndChase(bool caught, float now)
        {
            if (!caught)
            {
                Notice(NotificationType.Info, "reallife_chase_escaped", 8f);
            }
            _arrest.Clear();
            _suspect.Release(resumeTraffic: true, remove: caught);
            // The patrol car is never handed back standing: it drives off along its lane first, or is taken off the road (SendOff).
            _police.AtStand = false;
            SendOff(_police, resumeTraffic: true, drive: true);
            if (_police.Stage == Stage.Idle)
            {
                _police.Stage = Stage.Done;
            }
            _chase = Chase.None;
            _pursuit.Reset();

            // The scene the suspect left behind stays a little longer, then clears if nobody else is working there.
            _attended = true;
            _lingerUntil = now + Random.Range(6f, 14f);
        }
    }
}
