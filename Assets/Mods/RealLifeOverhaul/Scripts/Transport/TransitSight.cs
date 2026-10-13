using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Whether the player can see a bus: what decides where a bus may be put on the road and when one may be taken off it.
    /// The street's own test (CitySpots.InView) is a cone 110 m deep with no walls: a bus 170 m up a straight avenue is
    /// "out of view" by it, in plain sight, and one just round the corner of a building is "in view". Here a bus is seen
    /// when any of its nose, middle and tail, at window height and at its roof, is on the screen (with a margin, so that
    /// a turn of the camera does not show it at once), no further than the game draws a vehicle, and nothing of a building
    /// stands between the camera and it (one ray each, against the buildings' layers only: the street's furniture and
    /// the traffic do not hide a bus). Asked when a bus is put on the road or is to be taken off it, and four times a
    /// second for a bus that is driving away to go: a handful of rays, and nothing at all while no bus is out.
    ///
    /// NEAR is the other rule: within <see cref="NearKeep"/> of the player a bus is never taken off the street, seen or
    /// not (the camera can be swung round in a moment), and none is put on the road.
    /// </summary>
    public static class TransitSight
    {
        public const float Far = 380f;          // metres: further off than this nothing of the street is drawn clearly enough to be missed
        public const float NearKeep = 60f;      // metres from the player within which a bus is never taken away and none appears
        private const float Margin = 0.12f;     // of the screen, beyond its edges, that counts as on it

        private static int _wallMask;
        private static readonly Vector3[] Points = new Vector3[6];

        private static int WallMask
        {
            get
            {
                if (_wallMask == 0)
                {
                    _wallMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls");
                }
                return _wallMask;
            }
        }

        /// <summary>True when the player can see a bus of this length standing here, pointing this way.</summary>
        public static bool Seen(Vector3 middle, Vector3 forward, float halfLength)
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 up = Vector3.up;
            Points[0] = middle + up * 1.6f;
            Points[1] = middle + forward * halfLength + up * 1.6f;
            Points[2] = middle - forward * halfLength + up * 1.6f;
            Points[3] = middle + up * 3.0f;
            Points[4] = middle + forward * halfLength + up * 3.0f;
            Points[5] = middle - forward * halfLength + up * 3.0f;
            Vector3 eye = camera.transform.position;
            for (int i = 0; i < Points.Length; i++)
            {
                if (SeenPoint(camera, eye, Points[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the player can see a bus whose middle this is (any way it points).</summary>
        public static bool Seen(Vector3 middle)
        {
            return Seen(middle, Vector3.forward, 0f);
        }

        private static bool SeenPoint(Camera camera, Vector3 eye, Vector3 point)
        {
            Vector3 view = camera.WorldToViewportPoint(point);
            if (view.z <= 0.1f || view.z > Far || view.x < -Margin || view.x > 1f + Margin || view.y < -Margin || view.y > 1f + Margin)
            {
                return false;
            }
            Vector3 way = point - eye;
            float distance = way.magnitude;
            if (distance < 0.5f)
            {
                return true;
            }
            // A building in the way hides it: a ray from the camera that stops short of it.
            return !Physics.Raycast(eye, way / distance, distance - 0.5f, WallMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>True within this many metres of the player (flat).</summary>
        public static bool Near(Vector3 place, float within)
        {
            return CitySpots.Flat(place - PlayerHelper.GetPosition()) < within * within;
        }
    }
}
