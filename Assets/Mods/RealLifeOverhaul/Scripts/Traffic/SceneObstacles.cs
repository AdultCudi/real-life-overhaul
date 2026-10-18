using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Makes the vehicles standing at a scene solid for people on foot: the game moves everyone on its navigation mesh, which
    /// traffic cars are not part of, so a box the vehicle's size is cut out of the mesh while it stands. A handful of the
    /// mod's own boxes, built once and switched off when unused; placed once per vehicle, nothing after the cut.
    /// </summary>
    public static class SceneObstacles
    {
        private const int Count = 8;
        private const float Clearance = 0.25f;   // metres the box is larger than the vehicle, each side

        private static readonly NavMeshObstacle[] Boxes = new NavMeshObstacle[Count];
        private static readonly VehicleComponent[] Owners = new VehicleComponent[Count];
        private static GameObject _root;

        /// <summary>Cuts a standing vehicle out of the navigation mesh where it is now. Asking again for the same vehicle moves its box.</summary>
        public static void Add(VehicleComponent car, Transform host)
        {
            if (car == null)
            {
                return;
            }
            if (_root == null)
            {
                Build(host);
            }
            int slot = -1;
            for (int i = 0; i < Count; i++)
            {
                if (Owners[i] == car)
                {
                    slot = i;
                    break;
                }
                if (slot < 0 && (object)Owners[i] == null)
                {
                    slot = i;
                }
            }
            if (slot < 0)
            {
                return;   // more vehicles than boxes: the last ones stay as they were
            }

            // The box is the car's real body (its colliders): its nominal size left an end of a skewed car outside.
            TrafficTools.Footprint(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            if (StreetSpots.Ground(centre, out float ground))
            {
                centre.y = ground;
            }

            NavMeshObstacle box = Boxes[slot];
            box.gameObject.SetActive(false);   // moved while off, so the mesh is cut once, in the right place
            box.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(forward, Vector3.up));
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3((halfWidth + Clearance) * 2f, 2f, (halfLength + Clearance) * 2f);
            box.gameObject.SetActive(true);
            MeshCuts.Note("a vehicle standing at a scene");
            Owners[slot] = car;
        }

        /// <summary>The vehicle is moving off or gone: the mesh under it is whole again.</summary>
        public static void Remove(VehicleComponent car)
        {
            for (int i = 0; i < Count; i++)
            {
                if ((object)Owners[i] != null && Owners[i] == car)
                {
                    Free(i);
                }
            }
        }

        /// <summary>Every box off. Safe at any time, any number of times.</summary>
        public static void Clear()
        {
            if (_root == null)
            {
                return;
            }
            for (int i = 0; i < Count; i++)
            {
                Free(i);
            }
        }

        private static void Free(int slot)
        {
            Owners[slot] = null;
            if (Boxes[slot] != null && Boxes[slot].gameObject.activeSelf)
            {
                Boxes[slot].gameObject.SetActive(false);
                MeshCuts.Note("a vehicle gone from a scene");
            }
        }

        private static void Build(Transform host)
        {
            _root = new GameObject("RealLifeOverhaul Scene Obstacles");
            _root.transform.SetParent(host, false);
            for (int i = 0; i < Count; i++)
            {
                var box = new GameObject("Vehicle");
                box.transform.SetParent(_root.transform, false);
                box.SetActive(false);
                NavMeshObstacle obstacle = box.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
                Boxes[i] = obstacle;
                Owners[i] = null;
            }
        }
    }
}
