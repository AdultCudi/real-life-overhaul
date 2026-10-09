using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How tall a character stands, and the scale that gives an officer or a paramedic the height their
    /// service wants. The game has no height: a character has a build and a strength (CharacterData) on one
    /// skeleton per sex, so an officer came out whatever height that skeleton is, often shorter than the
    /// player. The height is set by scaling the whole character about its feet (StreetActor.Resize): the
    /// skeleton, the drawn body, the equipment on the bones, the capsule and the navigation agent all follow,
    /// and poses, clips and a ragdoll are in the bones' own space, so they do too.
    /// The height is measured, never assumed: the lengths of the leg and of the spine joint by joint (which
    /// do not depend on how the character is standing), the ankle above the sole, and the crown above the
    /// head joint as ServiceKit measured it on the drawn head. An officer is scaled to the service's height,
    /// or to the player's if the player is taller, within limits; a paramedic only ever up, and only if they
    /// would be short. Someone who cannot be measured keeps the size the game gave them.
    /// </summary>
    internal static class Stature
    {
        public const float OfficerMan = 1.86f;     // metres an officer stands, in boots
        public const float OfficerWoman = 1.76f;
        public const float MedicMan = 1.72f;       // a paramedic shorter than this is brought up to it
        public const float MedicWoman = 1.66f;
        public const float Least = 0.97f;          // an officer is never scaled outside these
        public const float Most = 1.10f;

        private const float Ankle = 0.09f;         // metres from the sole of a boot to the ankle joint

        private static readonly HumanBodyBones[] Spine =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head
        };

        private static float _player;   // the player's height, measured once per session (the player has one skeleton too)

        /// <summary>
        /// The scale for a uniformed character that has just been dressed and stands at the size the game
        /// built it. 1 if it cannot be measured.
        /// </summary>
        /// <param name="crown">Metres from the head joint to the top of the head (ServiceKit.Crown); 0 if not measured.</param>
        /// <param name="height">How tall the character is before scaling, in metres.</param>
        /// <param name="player">How tall the player is by the same measure; 0 if that could not be had.</param>
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

        // The player's height with the same crown (the game's heads are one size). Kept once it has been had:
        // while the player is in a car the game shrinks their character to nothing, and nothing can be measured.
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

        /// <summary>
        /// A humanoid's standing height in metres at the scale it has now, from its joints: sole to ankle,
        /// shin, thigh, the hip joints up to the base of the spine, the spine joint by joint to the head,
        /// and the crown. The same whatever pose it is in. 0 if the skeleton lacks a leg or a head.
        /// </summary>
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
