using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The few physics questions civic life asks that CitySpots does not answer, with the same layer names.</summary>
    internal static class CivicSpots
    {
        private static bool _ready;
        private static int _walls, _solid, _doors, _standing;

        public static int Walls { get { Setup(); return _walls; } }
        public static int Solid { get { Setup(); return _solid; } }

        /// <summary>Vehicles that are standing where they are: parked ones and the player's own.</summary>
        public static int StandingVehicles { get { Setup(); return _standing; } }

        private static void Setup()
        {
            if (_ready)
            {
                return;
            }
            _ready = true;
            int ground = LayerMask.GetMask("Roads", "Ground", "GroundUnplacable");
            _doors = LayerMask.GetMask("InteractiveItems", "InteractiveItemsOutlined");
            _walls = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
            _solid = ~(ground | LayerMask.GetMask("UI", "Ignore Raycast", "AutoParkSpots", "ParkingArea", "TransparentFX", "Water"));
            _standing = LayerMask.GetMask("ParkedVehicles", "PlayerVehicles", "Vehicles");
        }

        public static bool BoxFree(Vector3 centre, Vector3 halfExtents, Quaternion rotation, int mask)
        {
            Setup();
            return !Physics.CheckBox(centre, halfExtents, rotation, mask, QueryTriggerInteraction.Ignore);
        }

        public static bool Wall(Vector3 from, Vector3 direction, float height, float reach, out RaycastHit hit)
        {
            Setup();
            return Physics.Raycast(from + Vector3.up * height, direction, out hit, reach, _walls, QueryTriggerInteraction.Ignore);
        }

        public static bool DoorNear(Vector3 point, float radius)
        {
            Setup();
            return Physics.CheckSphere(point + Vector3.up, radius, _doors, QueryTriggerInteraction.Collide);
        }
    }
}
