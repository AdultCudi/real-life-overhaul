using System.Collections;
using BAModAPI;
using Helpers;
using UI.Overlays;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// GTA-style entry: press the interact key (F unless rebound; or the second one, E, when the game isn't
    /// already offering an interaction) near an entrance door to go inside, or near one of your vehicles to get in. Both use the
    /// game's own entry methods, so opening hours, entrance fees and cargo rules still apply.
    /// </summary>
    public class NearbyInteraction : MonoBehaviour
    {
        private const float DoorRange = 5f;
        private const float VehicleRange = 4.5f;
        private const float BuildingSearchRadius = 14f;

        private static readonly Collider[] Nearby = new Collider[48];
        private static int _buildingMask;

        public static IModLogger Logger;

        private float _nextHint;   // when the "not through a door while on a job" hint may be shown again

        private static readonly int HotUpdate = HotSpots.Slot("World: NearbyInteraction.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            // A job's key prompt is on screen, or the job has the key for the whole shift (flyers on foot): the key is its.
            if (!Settings.EnterWithKey || JobDirector.TakesInteractKey || TransitDirector.TakesInteractKey || CarOwnership.TakesInteractKey || HomeDirector.TakesInteractKey || GarageDirector.TakesInteractKey || Leisure.LeisureDirector.TakesInteractKey || SocialDirector.TakesInteractKey || TravelDirector.TakesInteractKey || Justice.TakesInteractKey || LicenceDirector.TakesInteractKey || CivicDirector.TakesInteractKey || Family.FamilyDirector.TakesInteractKey || Law.LawDirector.TakesInteractKey || Roommates.RoommateDirector.TakesInteractKey || Ledger.LedgerDirector.TakesInteractKey || Careers.CareersDirector.TakesInteractKey || Tech.TechSites.TakesInteractKey)
            {
                return;
            }

            // The second key (E) is one the game uses too: left alone while the game shows an interaction prompt.
            bool pressed = Keys.Down(KeyAction.Interact)
                           || (Keys.Down(KeyAction.InteractAlt) && !OverlayUI.IsVisible);
            if (!pressed)
            {
                return;
            }

            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.playerController == null || gm.selectedVehicle != null || PlayerHelper.IsUsingVehicle
                || gm.playerController.NavigationDisabled || GameManager.IsAnyMiniGameActive() || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }

            Vector3 playerPos = gm.playerController.transform.position;
            VehicleController vehicle = FindNearestVehicle(playerPos, out float vehicleSqr);
            CityBuildingController building = BuildingManager.IsInsideBuilding
                ? null
                : FindNearestBuilding(playerPos, out float buildingSqr, vehicleSqr);

            // On any job the key opens no door: going indoors ends the shift, and a press that found nothing
            // to do with the job must not do that by accident. (Clicking the building still works.) The car
            // is still got into: a courier and a driver need theirs.
            if (building != null && JobDirector.OnShift)
            {
                if (vehicle != null)
                {
                    StartCoroutine(EnterVehicle(vehicle));
                }
                else if (Time.unscaledTime >= _nextHint)
                {
                    _nextHint = Time.unscaledTime + 4f;
                    UI.Notification.Notifications.Show(UI.Notification.NotificationType.Info, "reallife_job_hint_door", null, 4f);
                }
            }
            else if (building != null)
            {
                Logger?.Info($"Entering nearby building '{building.name}'.");
                building.Interact();
            }
            else if (vehicle != null)
            {
                StartCoroutine(EnterVehicle(vehicle));
            }
        }

        // One frame later, so the same key press can't also reach the game's own vehicle keys.
        private static IEnumerator EnterVehicle(VehicleController vehicle)
        {
            yield return null;
            if (vehicle == null || PlayerHelper.IsUsingVehicle)
            {
                yield break;
            }
            Logger?.Info($"Getting into nearby vehicle '{vehicle.name}'.");
            vehicle.UpdateNavMeshTargets();
            vehicle.DriveVehicle();
        }

        private static VehicleController FindNearestVehicle(Vector3 playerPos, out float bestSqr)
        {
            VehicleController best = null;
            bestSqr = VehicleRange * VehicleRange;
            foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
            {
                if (vehicle == null || !vehicle.isActiveAndEnabled || !vehicle.visible || !GoingPublic.MineVehicle(vehicle))
                {
                    continue;
                }
                float sqr = (vehicle.transform.position - playerPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = vehicle;
                }
            }
            return best;
        }

        // Buildings are found through their colliders, so only the handful around the player are looked at.
        private static CityBuildingController FindNearestBuilding(Vector3 playerPos, out float bestSqr, float closerThanSqr)
        {
            if (_buildingMask == 0)
            {
                _buildingMask = LayerMask.GetMask("Buildings", "BuildingsOutlined");
                if (_buildingMask == 0)
                {
                    _buildingMask = Physics.AllLayers;
                }
            }

            CityBuildingController best = null;
            bestSqr = Mathf.Min(DoorRange * DoorRange, closerThanSqr);
            int count = Physics.OverlapSphereNonAlloc(playerPos, BuildingSearchRadius, Nearby, _buildingMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var cbc = Nearby[i].GetComponentInParent<CityBuildingController>();
                if (cbc == null || cbc.building == null || cbc.entranceDoors == null)
                {
                    continue;
                }
                foreach (var door in cbc.entranceDoors)
                {
                    if (door == null || door.doorTransform == null)
                    {
                        continue;
                    }
                    float sqr = (door.doorTransform.position - playerPos).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = cbc;
                    }
                }
            }
            return best;
        }
    }
}