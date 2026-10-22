using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Hands cuffed behind the back, aimed by hand after the animator (the game has no such animation) wherever no cuffed
    /// clip has the body, and the handcuffs on the wrists. Per frame: two people at most, four bones each; off while
    /// nobody is cuffed.
    /// </summary>
    public sealed class CuffPose : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("crime", "CuffPose", typeof(CuffPose));

        private const int Slots = 2;

        private sealed class Cuffed
        {
            public ThirdPersonCharacter Character;
            public Transform UpperL, LowerL, HandL, UpperR, LowerR, HandR;
            public GameObject Cuffs;   // the handcuffs prop, if it could be had; kept and reused
        }

        public static CuffPose Instance { get; private set; }

        private readonly Cuffed[] _cuffed = { new Cuffed(), new Cuffed() };

        private void Awake()
        {
            Instance = this;
            enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Puts the cuffs on. False if the skeleton is not one this can pose, or two people are cuffed already.</summary>
        public static bool Put(ThirdPersonCharacter character)
        {
            CuffPose pose = Instance;
            if (pose == null || character == null || character.animator == null || !character.animator.isHuman)
            {
                return false;
            }
            Cuffed free = null;
            for (int i = 0; i < Slots; i++)
            {
                if (pose._cuffed[i].Character == character)
                {
                    return true;
                }
                if (pose._cuffed[i].Character == null && free == null)
                {
                    free = pose._cuffed[i];
                }
            }
            if (free == null)
            {
                return false;
            }
            Animator animator = character.animator;
            free.UpperL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            free.LowerL = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            free.HandL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            free.UpperR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            free.LowerR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            free.HandR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (free.UpperL == null || free.LowerL == null || free.HandL == null || free.UpperR == null || free.LowerR == null || free.HandR == null)
            {
                return false;
            }
            free.Character = character;
            if (free.Cuffs == null && PropLibrary.TryGet("handcuffs", out Mesh mesh, out Material material) && mesh != null && material != null)
            {
                free.Cuffs = new GameObject("RealLifeOverhaul Handcuffs");
                free.Cuffs.transform.SetParent(pose.transform, false);
                Shapes.Piece("Cuffs", free.Cuffs.transform, mesh, material, Vector3.zero, Vector3.one);
            }
            pose.enabled = true;
            return true;
        }

        /// <summary>Takes the cuffs off. Safe for someone who never had them on, or who no longer exists.</summary>
        public static void Remove(ThirdPersonCharacter character)
        {
            CuffPose pose = Instance;
            if (pose == null)
            {
                return;
            }
            bool any = false;
            for (int i = 0; i < Slots; i++)
            {
                Cuffed cuffed = pose._cuffed[i];
                if ((object)cuffed.Character != null && (cuffed.Character == character || cuffed.Character == null))
                {
                    cuffed.Character = null;   // also anyone destroyed with the street
                    Show(cuffed, false);
                }
                any |= (object)cuffed.Character != null;
            }
            pose.enabled = any;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Crime: CuffPose.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            try
            {
                bool any = false;
                for (int i = 0; i < Slots; i++)
                {
                    Cuffed cuffed = _cuffed[i];
                    if ((object)cuffed.Character == null)
                    {
                        continue;
                    }
                    if (cuffed.Character == null || !cuffed.Character.gameObject.activeInHierarchy)
                    {
                        cuffed.Character = null;   // back in the pool, or gone with the street
                        Show(cuffed, false);
                        continue;
                    }
                    any = true;
                    // Not while physics has the body as a ragdoll, nor while nobody draws them.
                    bool held = cuffed.Character.visible && !RagdollSystem.Holds(cuffed.Character);
                    if (held && !ByClip(cuffed.Character))
                    {
                        Hold(cuffed);
                    }
                    Show(cuffed, held);
                }
                enabled = any;
            }
            catch (Exception e)
            {
                Crime.Fault(e);
                for (int i = 0; i < Slots; i++)
                {
                    _cuffed[i].Character = null;
                    Show(_cuffed[i], false);
                }
                enabled = false;
            }
        }

        // True while one of the mod's clips has the body, fades included: aiming the arms as well would fight it.
        private static bool ByClip(ThirdPersonCharacter character)
        {
            ActivityAnimator player = ActivityAnimators.For(character.animator);
            return player != null && player.Active;
        }

        // Upper arms down and a little back, forearms across the small of the back, wrists together.
        private static void Hold(Cuffed cuffed)
        {
            Transform root = cuffed.Character.transform;
            Vector3 forward = root.forward;
            Vector3 up = root.up;
            Vector3 right = root.right;
            BonePose.Aim(cuffed.UpperL, cuffed.LowerL, -up * 0.86f - forward * 0.42f - right * 0.10f);
            BonePose.Aim(cuffed.LowerL, cuffed.HandL, right * 0.80f - forward * 0.38f - up * 0.30f);
            BonePose.Aim(cuffed.UpperR, cuffed.LowerR, -up * 0.86f - forward * 0.42f + right * 0.10f);
            BonePose.Aim(cuffed.LowerR, cuffed.HandR, -right * 0.80f - forward * 0.38f - up * 0.30f);
        }

        // The handcuffs across the wrists, flat against the back, while the person is drawn and not a ragdoll; hidden otherwise.
        private static void Show(Cuffed cuffed, bool shown)
        {
            GameObject cuffs = cuffed.Cuffs;
            if (cuffs == null)
            {
                return;
            }
            if (cuffs.activeSelf != shown)
            {
                cuffs.SetActive(shown);
            }
            if (shown)
            {
                Transform root = cuffed.Character.transform;
                cuffs.transform.SetPositionAndRotation((cuffed.HandL.position + cuffed.HandR.position) * 0.5f - root.forward * 0.02f,
                    Quaternion.LookRotation(root.up, -root.forward));
            }
        }
    }
}
