using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The people a vehicle driven by the mod must not hit whom a physics query does not find: the mod's own people
    /// (no collider while standing, kneeling or lying), bodies in the road and the player on foot. Asked a few times a
    /// second by EmergencyDriver.
    /// </summary>
    public static class PeopleAhead
    {

        /// <summary>
        /// How far along the strip from <paramref name="from"/> to <paramref name="to"/> metres the nearest person in it
        /// is, in metres from <paramref name="origin"/>; float.MaxValue if nobody is.
        /// </summary>
        public static float Nearest(Vector3 origin, Vector3 direction, float from, float to, float halfWidth)
        {
            return Nearest(origin, direction, from, to, halfWidth, default, float.MaxValue);
        }

        /// <summary>
        /// The same beside a kerb: nobody beyond it counts (they are on the pavement). <paramref name="kerbTowards"/> is
        /// the flat unit way across the road towards it, <paramref name="kerbAt"/> how far along that way it lies.
        /// </summary>
        public static float Nearest(Vector3 origin, Vector3 direction, float from, float to, float halfWidth, Vector3 kerbTowards, float kerbAt)
        {
            float nearest = float.MaxValue;
            ActorPool pool = ActorPool.Instance;
            if (pool != null)
            {
                for (int i = pool.ActorCount - 1; i >= 0; i--)
                {
                    StreetActor actor = pool.ActorAt(i);
                    if (actor.InUse && actor.IsValid)
                    {
                        nearest = Mathf.Min(nearest, Along(actor.Position, origin, direction, from, to, halfWidth, kerbTowards, kerbAt));
                    }
                }
            }

            RagdollSystem ragdolls = RagdollSystem.Instance;
            if (ragdolls != null && ragdolls.enabled)
            {
                for (int i = 0; i < RagdollSystem.MaxBodies; i++)
                {
                    if (ragdolls.TryGetBody(i, out Vector3 body))
                    {
                        nearest = Mathf.Min(nearest, Along(body, origin, direction, from, to, halfWidth + 0.5f, kerbTowards, kerbAt));
                    }
                }
            }

            if (!PlayerHelper.IsUsingVehicle)
            {
                nearest = Mathf.Min(nearest, Along(PlayerHelper.GetPosition(), origin, direction, from, to, halfWidth, kerbTowards, kerbAt));
            }
            return nearest;
        }

        // How far along the strip a point is; float.MaxValue outside it or beyond the kerb.
        private static float Along(Vector3 point, Vector3 origin, Vector3 direction, float from, float to, float halfWidth, Vector3 kerbTowards, float kerbAt)
        {
            if (kerbAt < float.MaxValue && point.x * kerbTowards.x + point.z * kerbTowards.z > kerbAt)
            {
                return float.MaxValue;
            }
            Vector3 offset = point - origin;
            offset.y = 0f;
            float along = Vector3.Dot(offset, direction);
            bool inside = along > from && along < to && offset.sqrMagnitude - along * along < halfWidth * halfWidth;
            return inside ? along : float.MaxValue;
        }
    }
}
