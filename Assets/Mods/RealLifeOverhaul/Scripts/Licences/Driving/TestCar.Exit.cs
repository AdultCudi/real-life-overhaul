using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Helpers;
using UI;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Getting the player out of the school's car. The game's exit puts them back at the wheel when its one door point is
    /// not walkable, so the school names a walkable point, and finishes any exit left half done.
    /// </summary>
    public sealed partial class TestCar
    {
        private static readonly FieldInfo TargetsField = GameContract.NavTargets.Field;
        private static readonly FieldInfo BlockersField = GameContract.NavBlockers.Field;

        private bool _wasDriven;        // at its wheel at the last look
        private float _leftAt = -100f;  // when the player last got out of it (Time.time)
        private Vector3 _exitSpot;      // where the school last asked the game to put them
        private bool _haveSpot;
        private int _freed;             // exits the school has had to finish since the player last got out

        // Out with the game's own exit once the car has stopped, or after twelve seconds if they drive on.
        private IEnumerator SeeOut(CarController car)
        {
            float waited = 0f;
            while (Held(car) && Mathf.Abs(car.CurrentSpeed) > 1f && waited < 12f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Held(car) && PutOut(car))
            {
                yield return new WaitForSecondsRealtime(0.5f);
                ClosePanel();
            }
            _seeingOut = false;
        }

        private bool Held(CarController car)
        {
            return car != null && ReferenceEquals(car, _car) && _returning && Driven;
        }

        // The game's exit, with a walkable place written over its door point after the exit has checked that point.
        private bool PutOut(CarController car)
        {
            bool found = false;
            Vector3 spot = default;
            try
            {
                found = ExitSpot(car, out spot);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _exitSpot = spot;
            _haveSpot = found;
            _leftAt = Time.time;
            _freed = 0;
            _wasDriven = false;
            try
            {
                car.ExitVehicle();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _logger?.Warn("Licences: the game's exit from the school's car stopped half way (the error above); the school finishes it.");
                if (!Driven)
                {
                    Free(car, "the game's exit stopped half way");
                }
                return false;
            }
            if (found)
            {
                Aim(car, spot);
            }
            return true;
        }

        // The game's exit warps the player to its first door point one frame after it was called.
        private void Aim(CarController car, Vector3 spot)
        {
            try
            {
                if (TargetsField != null && TargetsField.GetValue(car) is Transform[] targets && targets.Length > 0 && targets[0] != null)
                {
                    targets[0].position = spot;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // A walkable place beside the car at street height, clear of it and of other vehicles; failing all, the pavement.
        private static bool ExitSpot(CarController car, out Vector3 spot)
        {
            spot = default;
            if (car == null)
            {
                return false;
            }
            JobSpots.CarFrame(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            NavMeshQueryFilter filter = PlayerController.navMeshQueryFilter;
            for (int i = 0; i < CarExitRules.Places; i++)
            {
                CarExitRules.Place(i, halfWidth, halfLength, out float across, out float along);
                Vector3 at = centre + right * across + forward * along;
                bool grounded = StreetSpots.Ground(at, out float ground);
                at.y = ground;
                if (!NavMesh.SamplePosition(at, out NavMeshHit hit, CarExitRules.SampleRadius, filter)
                    || (grounded && Mathf.Abs(hit.position.y - ground) > 0.45f))
                {
                    continue;
                }
                Vector3 local = hit.position - centre;
                if (CarExitRules.InBody(Vector3.Dot(local, right), Vector3.Dot(local, forward), halfWidth, halfLength))
                {
                    continue;
                }
                Vector3 clear = hit.position;
                if (!StreetSpots.OutOfVehicles(ref clear) || !NavMesh.SamplePosition(clear, out hit, 0.5f, filter))
                {
                    continue;
                }
                local = hit.position - centre;
                if (CarExitRules.InBody(Vector3.Dot(local, right), Vector3.Dot(local, forward), halfWidth, halfLength))
                {
                    continue;
                }
                spot = hit.position;
                return true;
            }
            if (StreetSpots.OnPavement(centre, 12f, out Vector3 pavement) && NavMesh.SamplePosition(pavement, out NavMeshHit onFoot, 1f, filter))
            {
                spot = onFoot.position;
                return true;
            }
            return false;
        }

        // Once a second for a while after the player got out: the exit is finished only if something of it was left undone.
        private void WatchExit(Vector3 player)
        {
            float since = Time.time - _leftAt;
            if (since < CarExitRules.SettleSeconds || since > CarExitRules.WatchFor || _car == null)
            {
                return;
            }
            PlayerController pc = PlayerHelper.PlayerController;
            if (pc == null || pc.Character == null || PlayerHelper.IsUsingVehicle || HeldByOther(pc))
            {
                return;
            }
            JobSpots.CarFrame(_car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 local = player - centre;
            local.y = 0f;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            NavMeshAgent agent = pc.Character.navmeshAgent;
            bool underCar = pc.transform.IsChildOf(_car.transform);
            bool marked = HasBlocker(pc, NavigationBlocker.Vehicle);
            bool hidden = !pc.gameObject.activeInHierarchy || pc.Character.transform.localScale.sqrMagnitude < 0.01f;
            bool onMesh = agent != null && agent.enabled && agent.isOnNavMesh;
            bool inBody = CarExitRules.InBody(Vector3.Dot(local, right), Vector3.Dot(local, forward), halfWidth, halfLength);
            if (!CarExitRules.Stuck(false, since, local.magnitude, underCar, marked, hidden, onMesh, inBody))
            {
                return;
            }
            string what = (underCar ? "still under the car, " : string.Empty) + (marked ? "still held as in a vehicle, " : string.Empty)
                          + (hidden ? "not shown, " : string.Empty) + (!onMesh ? "off the navigation mesh, " : string.Empty)
                          + (inBody ? "inside the car's body, " : string.Empty);
            Free(_car, what.TrimEnd(' ', ','));
        }

        // The rest of the game's exit (CarController.ExitVehicle, PlayerController.Unstuck), three times at most per exit.
        private void Free(CarController car, string why)
        {
            if (_freed >= 3)
            {
                return;
            }
            _freed++;
            try
            {
                var gm = InstanceBehavior<GameManager>.Instance;
                PlayerController pc = gm != null ? gm.playerController : null;
                if (pc == null || pc.Character == null || Driven)
                {
                    return;
                }
                bool underCar = car != null && pc.transform.IsChildOf(car.transform);
                bool hidden = !pc.gameObject.activeInHierarchy || pc.Character.transform.localScale.sqrMagnitude < 0.01f;
                if (underCar || pc.transform.parent != gm.transform)
                {
                    pc.transform.SetParent(gm.transform);
                }
                if (hidden)
                {
                    pc.Character.ToggleVisibility(show: true);
                }
                if (HasBlocker(pc, NavigationBlocker.Vehicle))
                {
                    pc.UnsetNavigationBlocker(NavigationBlocker.Vehicle);
                }
                if (underCar || hidden)
                {
                    CameraHelper.SetCamera(BuildingManager.IsInsideBuilding || Parking.UndergroundParking.UndergroundParkingManager.IsInsideParking ? gm.indoorCamera : gm.pedestrianCamera);
                }
                Vector3 spot = _exitSpot;
                bool found = _haveSpot && car != null && (spot - car.transform.position).sqrMagnitude < CarExitRules.StuckNear * CarExitRules.StuckNear;
                if (!found)
                {
                    found = ExitSpot(car, out spot);
                }
                NavMeshAgent agent = pc.Character.navmeshAgent;
                bool warped = false;
                if (agent != null && found)
                {
                    if (!agent.enabled)
                    {
                        agent.enabled = true;
                    }
                    warped = agent.Warp(spot);
                }
                pc.ResetNavigation();
                HideCard(car);
                _logger?.Info("Licences: out of the school's car, the player was " + why + "; the school finished the exit"
                              + (warped ? " and put them on their feet " + Vector3.Distance(spot, car != null ? car.transform.position : spot).ToString("F1") + " m from the car's middle."
                                  : ", but found no walkable place to put them (" + (found ? "the warp was refused" : "none within reach") + ")."));
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // Something else holds the player still (a sequence of the game's, custody, a fall): left to it.
        private static bool HeldByOther(PlayerController pc)
        {
            if (BlockersField == null || !(BlockersField.GetValue(pc) is HashSet<NavigationBlocker> set))
            {
                return false;
            }
            foreach (NavigationBlocker blocker in set)
            {
                if (blocker != NavigationBlocker.Vehicle)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasBlocker(PlayerController pc, NavigationBlocker blocker)
        {
            return BlockersField != null && BlockersField.GetValue(pc) is HashSet<NavigationBlocker> set && set.Contains(blocker);
        }

        private bool UnderCar()
        {
            PlayerController pc = PlayerHelper.PlayerController;
            return pc != null && _car != null && pc.transform.IsChildOf(_car.transform);
        }

        // A card left on a deleted car throws every frame (OverlayManager.LateUpdate), and the game hides it only on hover out.
        private static void HideCard(CarController car)
        {
            try
            {
                OverlayGuard.Release(car);
                var gm = InstanceBehavior<GameManager>.Instance;
                UIs hud = InstanceBehavior<UIs>.Instance;
                if (car != null && gm != null && ReferenceEquals(gm.selectedVehicle, car) && !PlayerHelper.IsUsingVehicle
                    && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
                {
                    hud.playerHUD.itemPanelUI.Toggle(false);
                }
            }
            catch (Exception e)
            {
                Licences.Logger?.Error(e);
            }
        }

        // The game clears the selected vehicle only when its panel closes.
        private static void ClosePanel()
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            UIs hud = InstanceBehavior<UIs>.Instance;
            if (gm != null && gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
            {
                hud.playerHUD.itemPanelUI.Toggle(false);
            }
        }
    }
}
