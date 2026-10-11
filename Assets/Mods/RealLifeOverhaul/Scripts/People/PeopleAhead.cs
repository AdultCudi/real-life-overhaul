using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The people a vehicle driven by the mod must not run into, whom a physics query does not
    /// find: the mod's own people (anyone standing, kneeling or lying has no collider), bodies
    /// lying in the road, and the player on foot. At most ten positions, compared with the
    /// strip of road ahead of the vehicle; asked a few times a second by EmergencyDriver.
    /// </summary>
    public static class PeopleAhead
    {
        /// <summary>True if anyone is in the strip that runs from <paramref name="from"/> to <paramref name="to"/> metres along a direction.</summary>
        /// <param name="origin">The middle of the vehicle (or of the lane beside it).</param>
        /// <param name="halfWidth">Half the width of the strip.</param>
        public static bool Any(Vector3 origin, Vector3 direction, float from, float to, float halfWidth)
        {
            return Nearest(origin, direction, from, to, halfWidth) < float.MaxValue;
        }

        /// <summary>
        /// How far along the strip the nearest person in it is, in metres from the origin;
        /// float.MaxValue if nobody is in it.
        /// </summary>
        public static float Nearest(Vector3 origin, Vector3 direction, float from, float to, float halfWidth)
        {
            return Nearest(origin, direction, from, to, halfWidth, default, float.MaxValue);
        }

        /// <summary>
        /// The same, for a vehicle beside a kerb: nobody further across the road than the kerb counts (they are on
        /// the pavement). The kerb is given as the way across the road towards it (flat, unit) and how far along
        /// that way it lies from the world's origin.
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

        // How far along the strip a point is, or float.MaxValue if it is outside it (or beyond the kerb, if one is given).
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
