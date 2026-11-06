using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Staged incidents, rolled every 20 seconds unless reckless drivers are the source of accidents, and the test buttons.
    /// </summary>
    public partial class PedestrianIncidents
    {
        private static bool CanStage()
        {
            return Settings.Ragdolls && ModState.Loaded && TrafficTools.Ready && ActorPool.Instance != null
                   && TrafficAccidents.Instance != null && RagdollSystem.Instance != null && RagdollSystem.Instance.Working
                   && PlayerHelper.PlayerController != null;
        }

        /// <summary>Options-menu test button: a traffic car hits someone nearby, right away.</summary>
        public void StageNow()
        {
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && !Settings.Ragdolls)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why == TestRefusal.None && !CanStage())
                {
                    why = TestRefusal.PoolBusy;   // no ragdolls or character pool this session
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Pedestrian incident", why);
                    return;
                }
                StreetCrime.EndActive();    // one incident at a time, and this one was asked for
                TrafficAccidents.EndActive();
                VehicleComponent car = TrafficAccidents.Instance.FindMovingCarWide(PlayerHelper.GetPosition(), out int within, out string census);
                if (car == null)
                {
                    TestRefusals.Tell(Logger, "Pedestrian incident", TestRefusal.NoMovingCar, within, census);
                }
                else if (!TryStage(Time.time, car))
                {
                    TestRefusals.Tell(Logger, "Pedestrian incident", TestRefusal.PoolBusy, 0, "no ragdoll or character free, or the accident scene could not start");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
            }
        }

        // A traffic car some way off knocks down someone who stepped out in front of it.
        private bool TryStage(float now, VehicleComponent car)
        {
            TrafficAccidents accidents = TrafficAccidents.Instance;
            // The car first: nobody lying in the road gives up their place for a scene that cannot happen.
            Casualty casualty = car != null ? FreeCasualty() : null;
            if (casualty == null)
            {
                return false;
            }
            Transform body = car.transform;
            Vector3 forward = body.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            forward.Normalize();

            bool dead = Random.value < StagedDeathChance;
            float speed = dead ? Random.Range(11f, 16f) : Random.Range(5.5f, 9f);   // m/s: the severity is rolled, not the car's real speed
            Vector3 ahead = body.position + forward * (Mathf.Max(1.5f, car.length * 0.5f) + 1.2f);
            if (!casualty.BeginNewActor(ahead, Launch(forward, speed), dead ? Casualty.Outcome.Dead : Casualty.Outcome.Injured, now))
            {
                return false;
            }
            if (!accidents.StageCasualty(casualty, body.position, car, dead, byPlayer: false))
            {
                casualty.Remove();
                return false;
            }
            casualty.SceneAsked = true;
            _anyone = true;
            _nextTick = now + TickInterval;
            Logger?.Info($"Pedestrian incident staged: {(dead ? "fatal" : "injury")}, {Vector3.Distance(PlayerHelper.GetPosition(), body.position):F0} m away.");
            TrafficAccidents.Notice(dead ? NotificationType.Error : NotificationType.Warning, StagedStarts[Random.Range(0, StagedStarts.Length)], 8f);
            return true;
        }

        /// <summary>Options-menu test button: the nearest pedestrian is pushed over away from the player and gets up; nothing else follows.</summary>
        public void TestNearest()
        {
            try
            {
                TestRefusal why = TestRefusals.Street();
                if (why == TestRefusal.None && !Settings.Ragdolls)
                {
                    why = TestRefusal.FeatureOff;
                }
                if (why != TestRefusal.None)
                {
                    TestRefusals.Tell(Logger, "Ragdoll", why);
                    return;
                }
                PlayerController player = PlayerHelper.PlayerController;
                var city = InstanceBehavior<CityManager>.Instance;
                Pedestrian nearest = null;
                if (city != null && city.pedestrianSpawner != null)
                {
                    List<Pedestrian> walkers = city.pedestrianSpawner.activePedestrians;
                    float best = 60f * 60f;
                    for (int i = 0; i < walkers.Count; i++)
                    {
                        Pedestrian walker = walkers[i];
                        if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null || walker.tpc.isSittingOn != null)
                        {
                            continue;
                        }
                        float sqr = (walker.transform.position - player.transform.position).sqrMagnitude;
                        if (sqr < best)
                        {
                            best = sqr;
                            nearest = walker;
                        }
                    }
                }
                Vector3 push = nearest != null ? nearest.transform.position - player.transform.position : Vector3.forward;
                Casualty casualty = nearest != null ? FreeCasualty() : null;
                if (casualty != null && casualty.BeginPedestrian(nearest, Launch(push, 6f), Casualty.Outcome.Knocked, false, Time.time))
                {
                    _anyone = true;
                    _nextTick = Time.time + TickInterval;
                    Logger?.Info($"Ragdoll test: {nearest.name} knocked down, {Mathf.Sqrt((nearest.transform.position - player.transform.position).sqrMagnitude):F0} m away.");
                    return;
                }
                TestRefusals.Tell(Logger, "Ragdoll", nearest == null ? TestRefusal.NobodyNear : TestRefusal.PoolBusy, nearest == null ? 60 : 0,
                    nearest == null ? null : "everyone who could make way is still falling, or the ragdoll could not be built");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
            }
        }
    }
}
