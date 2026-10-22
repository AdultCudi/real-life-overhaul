using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A leg put on a point after the animation: the thigh and shin are turned so that the ankle is on
    /// the target with the knee towards a given side. Unlike CityLife's TwoBoneReach (made for arms that
    /// are already bent the right way) this decides the bend itself: a standing leg is straight.
    /// </summary>
    public static class LegIk
    {
        /// <summary>weight: 1 puts the ankle on the target; less leaves that much of the pose the animation gave the leg.</summary>
        public static void Solve(Transform thigh, Transform shin, Transform foot, Vector3 target, Vector3 kneeTowards, float weight = 1f)
        {
            if (thigh == null || shin == null || foot == null)
            {
                return;
            }
            Vector3 hip = thigh.position;
            float upper = (shin.position - hip).magnitude;
            float lower = (foot.position - shin.position).magnitude;
            if (upper < 0.01f || lower < 0.01f)
            {
                return;
            }
            Quaternion footTurn = foot.rotation;
            Quaternion thighWas = thigh.localRotation, shinWas = shin.localRotation;
            Vector3 to = target - hip;
            float reach = Mathf.Clamp(to.magnitude, Mathf.Abs(upper - lower) + 0.01f, upper + lower - 0.005f);
            Vector3 along = to.sqrMagnitude > 1e-6f ? to.normalized : Vector3.down;
            Vector3 side = kneeTowards - along * Vector3.Dot(kneeTowards, along);
            if (side.sqrMagnitude < 1e-6f)
            {
                return;
            }
            side.Normalize();
            float cos = Mathf.Clamp((upper * upper + reach * reach - lower * lower) / (2f * upper * reach), -1f, 1f);
            Vector3 knee = hip + along * (upper * cos) + side * (upper * Mathf.Sqrt(1f - cos * cos));
            thigh.rotation = Quaternion.FromToRotation(shin.position - hip, knee - hip) * thigh.rotation;
            Vector3 ankle = hip + along * reach;
            shin.rotation = Quaternion.FromToRotation(foot.position - shin.position, ankle - shin.position) * shin.rotation;
            if (weight < 1f)
            {
                thigh.localRotation = Quaternion.Slerp(thighWas, thigh.localRotation, weight);
                shin.localRotation = Quaternion.Slerp(shinWas, shin.localRotation, weight);
            }
            foot.rotation = footTurn;   // the foot stays as flat as the animation had it
        }
    }
}
