using System.Collections.Generic;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// Parking along new kerbs, the game's own way: the game's ParkingLaneGenerator is a box on the ParkingArea layer that
    /// parks the game's cars along itself and makes the spots the player's car parks itself in; it uses neither the traffic
    /// nor the walking mesh and starts itself once the game is loaded (its Start, RegisterOnGameLoadedCallback). A lane of
    /// the mod's own is a copy of one of the game's, made asleep under a switched-off holder so that it wakes with no
    /// building of its own (a lane of the street, not of a shop) and without the template's parked cars, then stood where
    /// it is wanted. Taken away, it gives its cars back to the game's pool first. Called on a build or a test, never per frame.
    /// </summary>
    public static class ParkingGame
    {
        public const string CopyName = "RealLifeOverhaul new ground parking";
        public const string HolderName = "RealLifeOverhaul new ground parking (asleep)";
        private const string CurvedSegment = "Lane Segment 0";   // the first piece a curved lane of the game's makes under itself
        private const string ReservedName = "Reserved Parking Spot";   // a spot the game holds for a car on its way (ParkingLaneGenerator.TryReserveSpot)

        private static readonly Collider[] Near = new Collider[48];
        private static readonly List<Transform> Children = new List<Transform>(16);
        private static GameObject _holder;
        private static int _solidMask;
        private static int _groundMask;

        /// <summary>The nearest straight lane of the game's within <paramref name="reach"/>, not a delivery van's spot; null where there is none.</summary>
        public static ParkingLaneGenerator Nearest(Vector3 at, float reach)
        {
            int count = Physics.OverlapSphereNonAlloc(at, reach, Near, LayerHelper.parkingAreaLayerMask, QueryTriggerInteraction.Collide);
            ParkingLaneGenerator best = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                ParkingLaneGenerator lane = Near[i] != null ? Near[i].GetComponentInParent<ParkingLaneGenerator>() : null;
                if (lane == null || lane.designatedDeliveryVanSpot || lane.name == CopyName || lane.transform.Find(CurvedSegment) != null)
                {
                    continue;
                }
                var box = lane.GetComponent<BoxCollider>();
                if (box == null)
                {
                    continue;
                }
                float distance = (box.ClosestPoint(at) - at).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    best = lane;
                }
            }
            return best;
        }

        /// <summary>
        /// The way along a lane its spots run (its box's longer side on the ground) and its length: a lane of cars one behind
        /// the other runs along its z, one of cars side by side along its x.
        /// </summary>
        public static Vector3 Along(ParkingLaneGenerator lane, out float length)
        {
            var box = lane.GetComponent<BoxCollider>();
            Vector3 scale = lane.transform.lossyScale;
            float alongZ = box.size.z * Mathf.Abs(scale.z), alongX = box.size.x * Mathf.Abs(scale.x);
            length = Mathf.Max(alongZ, alongX);
            Vector3 way = alongZ >= alongX ? lane.transform.forward : lane.transform.right;
            way.y = 0f;
            return way.sqrMagnitude > 0.0001f ? way.normalized : Vector3.forward;
        }

        /// <summary>
        /// True when the ground a copy of <paramref name="lane"/> would stand on, its origin at <paramref name="at"/> and
        /// turned <paramref name="turn"/>, is free of buildings, props, vehicles and other lanes, and is ground.
        /// </summary>
        public static bool Room(ParkingLaneGenerator lane, Vector3 at, Quaternion turn, out string why)
        {
            return Room(lane, at, turn, float.NaN, float.NaN, out why);
        }

        /// <summary>
        /// As above for the piece of the lane from <paramref name="from"/> to <paramref name="to"/> metres along its length,
        /// counted from the middle of its box (NaN for the whole lane): what is left of a lane that a garage door's mouth was cut out of.
        /// </summary>
        public static bool Room(ParkingLaneGenerator lane, Vector3 at, Quaternion turn, float from, float to, out string why)
        {
            why = null;
            var box = lane.GetComponent<BoxCollider>();
            Vector3 scale = lane.transform.lossyScale;
            Span(box, scale, from, to, out Vector3 middle, out float length);
            Vector3 centre = at + turn * Vector3.Scale(middle, scale);
            var half = new Vector3(box.size.x * Mathf.Abs(scale.x) * 0.5f, 1f, length * 0.5f);
            if (_solidMask == 0)
            {
                _solidMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps", "InteractiveItems", "Vehicles", "AiVehicles",
                    "ParkedVehicles", "PlayerVehicles", "Human");
                _groundMask = LayerMask.GetMask("Ground", "GroundUnplacable", "Roads");
            }
            int solid = Physics.OverlapBoxNonAlloc(centre + Vector3.up * 1.2f, half, Near, turn, _solidMask, QueryTriggerInteraction.Ignore);
            if (solid > 0)
            {
                why = "something stands there: " + Near[0].name;
                return false;
            }
            int lanes = Physics.OverlapBoxNonAlloc(centre, half, Near, turn, LayerHelper.parkingAreaLayerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < lanes; i++)
            {
                if (Near[i] != null && Near[i].GetComponentInParent<ParkingLaneGenerator>() != lane)
                {
                    why = "another parking lane is there: " + Near[i].name;
                    return false;
                }
            }
            if (!Physics.Raycast(centre + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 6f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                why = "there is no road or ground under it";
                return false;
            }
            return true;
        }

        /// <summary>
        /// A copy of a lane of the game's, its origin at <paramref name="at"/> and turned <paramref name="turn"/> (along a
        /// kerb of new ground, or the test's stretch further along the street), the template's size in the world: no
        /// building of its own, no delivery van's spot, none of the template's cars or spots; it wakes where it stands and
        /// parks its own. Null with why where it cannot be made.
        /// </summary>
        public static ParkingLaneGenerator Copy(ParkingLaneGenerator template, Vector3 at, Quaternion turn, out string why)
        {
            return Copy(template, at, turn, float.NaN, float.NaN, out why);
        }

        /// <summary>
        /// As above, only the piece of the lane from <paramref name="from"/> to <paramref name="to"/> metres along its length, counted
        /// from the middle of the template's box (NaN for the whole lane): its box cut to the piece before it wakes, so it parks cars
        /// along that stretch alone (a straight lane of the game's with no spot count of its own parks along its box).
        /// </summary>
        public static ParkingLaneGenerator Copy(ParkingLaneGenerator template, Vector3 at, Quaternion turn, float from, float to, out string why)
        {
            why = null;
            if (template == null)
            {
                why = "no lane to copy";
                return null;
            }
            if (_holder == null)
            {
                _holder = new GameObject(HolderName);
                _holder.SetActive(false);
            }
            GameObject copy = Object.Instantiate(template.gameObject, _holder.transform);
            copy.name = CopyName;
            var lane = copy.GetComponent<ParkingLaneGenerator>();
            lane.building = null;
            lane.designatedDeliveryVanSpot = false;
            lane.autoParkSpots.Clear();
            // The template's cars and spots came along as children: copies the game's pool never handed out, so they go
            // before the lane wakes and gives its children back to that pool.
            Children.Clear();
            foreach (Transform child in copy.transform)
            {
                Children.Add(child);
            }
            for (int i = 0; i < Children.Count; i++)
            {
                int layer = Children[i].gameObject.layer;
                if (layer == LayerHelper.ParkedVehiclesLayerIndex || layer == LayerHelper.AutoParkSpotsLayerIndex || Children[i].name == ReservedName)
                {
                    Object.DestroyImmediate(Children[i].gameObject);
                }
            }
            Children.Clear();
            if (!float.IsNaN(from) && !float.IsNaN(to))
            {
                var box = copy.GetComponent<BoxCollider>();
                Span(box, template.transform.lossyScale, from, to, out Vector3 middle, out float length);
                box.center = middle;
                box.size = new Vector3(box.size.x, box.size.y, length / Mathf.Max(0.0001f, Mathf.Abs(template.transform.lossyScale.z)));
            }
            // Placed and sized while asleep (the holder stands at the origin unturned), so it first looks at the street
            // from where it stands, as large as the lane Room measured.
            copy.transform.SetPositionAndRotation(at, turn);
            copy.transform.localScale = template.transform.lossyScale;
            copy.transform.SetParent(null, true);
            return lane;
        }

        // A lane's box whole (NaN), or the piece of it from..to metres along its length from the middle of the box: the piece's
        // middle in the box's own frame and its length in metres.
        private static void Span(BoxCollider box, Vector3 scale, float from, float to, out Vector3 middle, out float length)
        {
            float along = Mathf.Max(0.0001f, Mathf.Abs(scale.z));
            middle = box.center;
            length = box.size.z * along;
            if (!float.IsNaN(from) && !float.IsNaN(to))
            {
                middle.z += (from + to) * 0.5f / along;
                length = to - from;
            }
        }

        /// <summary>The game's cars parked on a lane now.</summary>
        public static int Parked(ParkingLaneGenerator lane)
        {
            if (lane == null)
            {
                return 0;
            }
            int parked = 0;
            foreach (Transform child in lane.transform)
            {
                parked += child.gameObject.layer == LayerHelper.ParkedVehiclesLayerIndex ? 1 : 0;
            }
            return parked;
        }

        /// <summary>The spots the player's car can park itself in on a lane.</summary>
        public static int Spots(ParkingLaneGenerator lane)
        {
            return lane != null && lane.autoParkSpots != null ? lane.autoParkSpots.Count : 0;
        }

        /// <summary>Takes a lane of the mod's away: its cars go back to the game's pool, then it goes. Safe on null.</summary>
        public static void Remove(ref ParkingLaneGenerator lane)
        {
            if (lane == null)
            {
                return;
            }
            lane.CleanupParkedVehicles(force: true);
            var box = lane.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.enabled = false;   // the engine destroys it at the frame's end: a lane laid in its place now must not find it there
            }
            Object.Destroy(lane.gameObject);
            lane = null;
        }

        /// <summary>The city is going: the holder goes with it.</summary>
        public static void Forget()
        {
            if (_holder != null)
            {
                Object.Destroy(_holder);
            }
            _holder = null;
            _solidMask = 0;
            _groundMask = 0;
        }
    }
}
