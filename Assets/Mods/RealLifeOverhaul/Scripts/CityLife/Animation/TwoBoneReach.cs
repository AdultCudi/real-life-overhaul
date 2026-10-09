using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts the end of a two-bone limb (a hand) on a point by turning the two bones above it,
    /// after the animation has posed them: the bend at the elbow is opened or closed to the
    /// right length, then the whole arm is swung onto the target. The hand keeps the turn the
    /// animation gave it. A dozen vector operations and no allocation.
    /// </summary>
    public static class TwoBoneReach
    {
        private const float Epsilon = 0.0005f;

        /// <param name="weight">0 leaves the animation alone, 1 puts the wrist on the target.</param>
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, float weight = 1f)
        {
            if (weight <= 0f)
            {
                return;
            }
            Vector3 a = upper.position;
            Vector3 b = lower.position;
            Vector3 c = end.position;
            target = Vector3.Lerp(c, target, weight);
            Quaternion endTurn = end.rotation;

            float upperLength = (b - a).magnitude;
            float lowerLength = (c - b).magnitude;
            if (upperLength < Epsilon || lowerLength < Epsilon)
            {
                return;
            }
            float reach = Mathf.Clamp((target - a).magnitude, Mathf.Abs(upperLength - lowerLength) + Epsilon, upperLength + lowerLength - Epsilon);

            // The angles of the triangle now, and the angles it must have to span the distance.
            float shoulderNow = Angle(c - a, b - a);
            float elbowNow = Angle(a - b, c - b);
            float shoulderThen = Mathf.Acos(Mathf.Clamp((lowerLength * lowerLength - upperLength * upperLength - reach * reach) / (-2f * upperLength * reach), -1f, 1f));
            float elbowThen = Mathf.Acos(Mathf.Clamp((reach * reach - upperLength * upperLength - lowerLength * lowerLength) / (-2f * upperLength * lowerLength), -1f, 1f));

            Vector3 axis = Vector3.Cross(c - a, b - a);
            if (axis.sqrMagnitude < Epsilon * Epsilon)
            {
                axis = Vector3.Cross(c - a, upper.up);   // a straight arm: bend it the way the bone itself faces
                if (axis.sqrMagnitude < Epsilon * Epsilon)
                {
                    axis = Vector3.Cross(c - a, Vector3.up);
                }
            }
            axis.Normalize();
            upper.rotation = Quaternion.AngleAxis((shoulderThen - shoulderNow) * Mathf.Rad2Deg, axis) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis((elbowThen - elbowNow) * Mathf.Rad2Deg, axis) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(end.position - a, target - a) * upper.rotation;
            end.rotation = endTurn;
        }

        private static float Angle(Vector3 from, Vector3 to)
        {
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(from.normalized, to.normalized), -1f, 1f));
        }
    }
}
