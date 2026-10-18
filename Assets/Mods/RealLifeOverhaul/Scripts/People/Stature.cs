using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How tall a character stands, and the scale that gives an officer or a paramedic their service's height. The game
    /// has no height (one skeleton per sex), so the whole character is scaled about its feet (StreetActor.Resize). The
    /// height is measured from the joints, which do not depend on the pose, plus the crown ServiceKit measured.
    /// </summary>
    internal static class Stature
    {
        public const float OfficerMan = 1.86f;     // metres an officer stands, in boots
        public const float OfficerWoman = 1.76f;
        public const float MedicMan = 1.72f;       // a paramedic shorter than this is brought up to it
        public const float MedicWoman = 1.66f;
        public const float Least = 0.97f;          // scale: an officer is never scaled outside these
        public const float Most = 1.10f;

        private const float Ankle = 0.09f;         // metres from the sole of a boot to the ankle joint

        private static readonly HumanBodyBones[] Spine =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head
        };

        private static float _player;   // the player's height, measured once a session

        /// <summary>
        /// The scale for a uniformed character just dressed at the game's size: an officer to the service's height or the
        /// player's if taller, a paramedic only ever up; 1 if it cannot be measured. <paramref name="crown"/> is metres
        /// above the head joint (0: not measured); the outs are heights in metres, <paramref name="player"/> 0 if unknown.
        /// </summary>
        public static float For(Animator animator, ActorLook look, bool man, float crown, out float height, out float player)
        {
            player = 0f;
            height = crown > 0f ? Of(animator, crown) : 0f;
            if (height < 1.3f || height > 2.3f)
            {
                return 1f;   // not measured, or not a skeleton this was written for: left alone
            }
            if (look == ActorLook.Paramedic)
            {
                return Mathf.Clamp((man ? MedicMan : MedicWoman) / height, 1f, Most);
            }
            player = Player(crown);
            float wanted = man ? Mathf.Max(OfficerMan, player) : OfficerWoman;
            return Mathf.Clamp(wanted / height, Least, Most);
        }

        // Kept once had: while the player is in a car the game shrinks their character to nothing.
        private static float Player(float crown)
        {
            if (_player > 0f)
            {
                return _player;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            ThirdPersonCharacter character = controller != null ? controller.Character : null;
            Animator animator = character != null ? character.animator : null;
            if (animator == null || !animator.isHuman || Mathf.Abs(character.transform.lossyScale.y - 1f) > 0.02f)
            {
                return 0f;
            }
            float height = Of(animator, crown);
            if (height > 1.3f && height < 2.3f)
            {
                _player = height;
            }
            return _player;
        }

        /// <summary>A humanoid's standing height in metres at its present scale, whatever its pose; 0 without a leg or a head.</summary>
        public static float Of(Animator animator, float crown)
        {
            if (animator == null || !animator.isHuman)
            {
                return 0f;
            }
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform thighLeft = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform thighRight = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            float leg = (Leg(animator, thighLeft, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot)
                         + Leg(animator, thighRight, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot)) * 0.5f;
            if (hips == null || leg <= 0f || animator.GetBoneTransform(HumanBodyBones.Head) == null)
            {
                return 0f;
            }

            // Up the spine, joint by joint; the first step also says which way is up for the pelvis.
            float trunk = 0f;
            Vector3 along = Vector3.zero;
            Transform below = hips;
            for (int i = 1; i < Spine.Length; i++)
            {
                Transform joint = animator.GetBoneTransform(Spine[i]);
                if (joint == null)
                {
                    continue;
                }
                Vector3 step = joint.position - below.position;
                if (along == Vector3.zero)
                {
                    along = step.normalized;
                }
                trunk += step.magnitude;
                below = joint;
            }
            float pelvis = Mathf.Abs(Vector3.Dot(hips.position - (thighLeft.position + thighRight.position) * 0.5f, along));
            return Ankle * hips.lossyScale.y + leg + pelvis + trunk + crown;
        }

        private static float Leg(Animator animator, Transform thigh, HumanBodyBones shinBone, HumanBodyBones footBone)
        {
            Transform shin = animator.GetBoneTransform(shinBone);
            Transform foot = animator.GetBoneTransform(footBone);
            if (thigh == null || shin == null || foot == null)
            {
                return float.MinValue;
            }
            return Vector3.Distance(thigh.position, shin.position) + Vector3.Distance(shin.position, foot.position);
        }
    }
}
