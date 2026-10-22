using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts a hand on a point after the animation has posed the arm: the elbow opens or closes to the right length, then the
    /// arm swings onto the target; the hand keeps its own turn. A dozen vector operations, no allocation.
    /// </summary>
    public static class TwoBoneReach
    {
        private const float Epsilon = 0.0005f;   // metres

        /// <summary>Turns upper and lower so the end is on the target, by weight: 0 leaves the animation alone, 1 puts it there.</summary>
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
