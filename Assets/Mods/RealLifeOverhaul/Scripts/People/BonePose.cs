using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What the hand-set poses (KneelPose, Crime/CuffPose) do to a skeleton. Both work from where the joints are now,
    /// never from a bone's roll, so nothing is assumed about the rig beyond the order of its joints.
    /// </summary>
    internal static class BonePose
    {
        /// <summary>Turns a bone so the line from it to the next joint runs along a direction.</summary>
        public static void Aim(Transform bone, Transform next, Vector3 direction)
        {
            Vector3 now = next.position - bone.position;
            if (now.sqrMagnitude > 0.0001f && direction.sqrMagnitude > 0.0001f)
            {
                bone.rotation = Quaternion.FromToRotation(now, direction) * bone.rotation;
            }
        }

        /// <summary>
        /// Puts the wrist on a target by turning the upper arm and forearm, elbow out towards <paramref name="elbowOut"/>.
        /// A target out of reach is reached towards with the arm straight.
        /// </summary>
        public static void Reach(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 elbowOut)
        {
            Vector3 shoulder = upper.position;
            float a = Vector3.Distance(shoulder, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            Vector3 to = target - shoulder;
            float distance = to.magnitude;
            if (a < 0.01f || b < 0.01f || distance < 0.01f)
            {
                return;
            }
            Vector3 along = to / distance;
            distance = Mathf.Clamp(distance, Mathf.Abs(a - b) + 0.01f, (a + b) * 0.995f);

            // The triangle shoulder, elbow, wrist: how far along the line the elbow is, and how far off it.
            float onLine = (a * a + distance * distance - b * b) / (2f * distance);
            float off = Mathf.Sqrt(Mathf.Max(0f, a * a - onLine * onLine));
            Vector3 side = elbowOut - along * Vector3.Dot(elbowOut, along);
            side = side.sqrMagnitude < 0.0001f ? Vector3.zero : side.normalized;
            Vector3 elbow = shoulder + along * onLine + side * off;

            Aim(upper, lower, elbow - shoulder);
            Aim(lower, hand, shoulder + along * distance - lower.position);
        }
    }
}
