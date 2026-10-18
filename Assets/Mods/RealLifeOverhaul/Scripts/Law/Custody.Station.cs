using System;
using System.Collections;
using Helpers;
using Parking.UndergroundParking;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>Under the dark screen to the precinct: onto its pavement, through its door and into the holding cell.</summary>
    public sealed partial class Custody
    {
        private bool _inCell;                // in a station's holding cell, in view
        private bool _moved;                 // put down outside the precinct

        private IEnumerator Transport()
        {
            yield return LeaveVehicle(3f, force: true);
            var buildings = InstanceBehavior<BuildingManager>.Instance;
            if (UndergroundParkingManager.IsInsideParking)
            {
                UndergroundParkingManager.ExitParking(playFadeAnimation: false);   // as the game's own hospital trip leaves a car park
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (BuildingManager.IsInsideBuilding && buildings != null)
            {
                buildings.ExitFromBuilding(0, playFadeAnimation: false);
                float waited = 0f;
                while ((BuildingManager.IsInsideBuilding || buildings.exitingBuilding) && waited < 10f)
                {
                    waited += Time.unscaledDeltaTime;
                    _beat = Time.unscaledTime;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            // Knocked down or on the way to hospital: they are the game's for now, and are not moved.
            PlayerController held = PlayerHelper.PlayerController;
            float patience = 0f;
            while ((!LifeEvents.IsSafeForEvent() || (held != null && held.Character != null && RagdollSystem.Holds(held.Character))) && patience < 8f)
            {
                patience += Time.unscaledDeltaTime;
                _beat = Time.unscaledTime;
                yield return null;
            }
            _beat = Time.unscaledTime;
            if (!LifeEvents.IsSafeForEvent() || held == null || held.Character == null || RagdollSystem.Holds(held.Character)
                || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking
                || !PutOutside(_precincts.At(LawState.Custody.Precinct)))
            {
                _moved = false;
                if (LawState.Custody.Court)
                {
                    // A court's hours are not let off for a door that could not be reached: they pass where the player is.
                    Logger?.Warn("Law: the player could not be taken to the precinct (still in a vehicle or a building, or no way to the door); the court's hours are served where they are.");
                    yield break;
                }
                Logger?.Warn("Law: the player could not be taken to the precinct (still in a vehicle or a building, or no way to the door); let go with the fines.");
                Drop();
                yield break;
            }
            _moved = true;
        }

        private bool PutOutside(Precinct precinct)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (precinct == null || player == null || player.Character == null || player.Character.navmeshAgent == null)
            {
                return false;
            }
            _precincts.DressNow(precinct);
            Vector3 outward;
            if (!Precincts.Front(precinct, out Vector3 spot, out outward))
            {
                // The game's own spot for somebody arriving at the building (its taxi's).
                spot = precinct.Building.GetNavMeshTargetPosition();
                outward = spot - precinct.Door;
                outward.y = 0f;
                if (!UnityEngine.AI.NavMesh.SamplePosition(spot, out _, 3f, -1))
                {
                    return false;
                }
            }
            player.ResetNavigation();
            player.Character.WarpSafely(spot);
            if (outward.sqrMagnitude > 0.01f)
            {
                player.transform.rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);
            }
            player.Character.Reset();
            player.ResetNavigation();
            return JobSpots.Flat(player.transform.position, spot) < 4f;
        }

        // Into a walk-in station by its own door, the game's own way in (which fades and clears by itself), and into the cell
        // before the screen clears. Otherwise the player stays where they are and the cell is the panel.
        private IEnumerator GoInside()
        {
            _inCell = false;
            Precinct precinct = _precincts.At(LawState.Custody.Precinct);
            LawDirector director = LawDirector.Instance;
            BuildingManager buildings = InstanceBehavior<BuildingManager>.Instance;
            if (_interior == null || director == null || buildings == null || !Precincts.CanEnter(precinct) || !director.EnterStation(precinct))
            {
                yield break;
            }
            float waited = 0f;
            bool placed = false;
            while (waited < 12f && (buildings.enteringBuilding || !BuildingManager.IsInsideBuilding))
            {
                waited += Time.unscaledDeltaTime;
                _beat = Time.unscaledTime;
                if (!placed && _interior.Dressed && _interior.Precinct == precinct)
                {
                    placed = true;
                    PutInCell();
                }
                yield return null;
            }
            _beat = Time.unscaledTime;
            if (waited < 12f)
            {
                _faded = false;   // the game's own way in has cleared the screen
            }
            if (!placed && InStation(precinct))
            {
                // The room is furnished a moment after the door: looked for once more.
                for (int i = 0; i < 30 && !_interior.Dressed; i++)
                {
                    _beat = Time.unscaledTime;
                    yield return null;
                }
                PutInCell();
            }
            Logger?.Info("Law: taken inside " + (precinct != null ? precinct.Name : "the precinct")
                         + (_inCell ? ": in the holding cell." : InStation(precinct) ? ": no cell in this room; held at the desk." : ": the way in failed; held outside."));
        }

        private bool InStation(Precinct precinct)
        {
            return precinct != null && precinct.Building != null && BuildingManager.IsInsideBuilding
                   && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.building, precinct.Building.building);
        }

        private bool PutInCell()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (_interior == null || !_interior.Dressed || !_interior.HasCell || player == null || player.Character == null || player.Character.navmeshAgent == null)
            {
                return false;
            }
            _player = player;
            player.ResetNavigation();
            player.Character.navmeshAgent.Warp(_interior.CellSpot);
            Vector3 facing = _interior.CellFace - _interior.CellSpot;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
            {
                player.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }
            player.Character.Reset();
            HoldPlayer();
            _inCell = true;
            return true;
        }

        // Out of the cell to the front desk (the bars are solid). If they cannot be put there the bars go instead, so nobody
        // is ever left shut in.
        private void LeaveCell()
        {
            if (!_inCell)
            {
                return;
            }
            _inCell = false;
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (_interior == null || !_interior.Dressed || player == null || player.Character == null || player.Character.navmeshAgent == null
                    || !BuildingManager.IsInsideBuilding)
                {
                    return;
                }
                player.ResetNavigation();
                if (UnityEngine.AI.NavMesh.SamplePosition(_interior.ServiceSpot, out UnityEngine.AI.NavMeshHit hit, 1.5f, -1))
                {
                    player.Character.navmeshAgent.Warp(hit.position);
                    Vector3 facing = _interior.ServiceFace - hit.position;
                    facing.y = 0f;
                    if (facing.sqrMagnitude > 0.01f)
                    {
                        player.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                    }
                    player.Character.Reset();
                }
                else
                {
                    _interior.OpenCell();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _interior?.OpenCell();
            }
        }

        // Nothing came of it: the fines stand, the cell does not, and the record keeps a citation.
        private void Drop()
        {
            Record.Close(Outcome.Cited, 0);
            LawState.Custody.Clear();
            _phase = Phase.None;
            if (SaveGameManager.Current != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_law_arrest_dropped", null, 9f);
            }
        }
    }
}
