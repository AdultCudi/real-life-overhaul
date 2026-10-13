using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Hands held behind the back: handcuffs. Crime/ArrestSequence plays the mod's own clips for
    /// it (cuffed_stand, cuffed_walk); this is what holds the arms whenever no such clip is on the
    /// body (the clip bundle not loaded, the clip player off, a clip ended), and what lays the
    /// handcuffs across the wrists in either case. The game has no such animation, so the arms are
    /// set by hand, the same way People/KneelPose sets a kneel (both through People/BonePose): each bone is aimed along a
    /// direction given in the character's own frame, by turning it from wherever it points now,
    /// so nothing is assumed about how the skeleton is rolled. Unlike a kneel, a cuffed person
    /// walks, so the animator keeps running and writes the arms again every frame; the four arm
    /// bones are therefore aimed again after it, in LateUpdate. That is the only per-frame work
    /// in the crime system besides the marker: two people at most, four bones each, and the
    /// component is switched off whenever nobody is cuffed. With the modelled props loaded, a pair
    /// of handcuffs (one object per slot, kept and reused) is laid across the wrists in the same pass. Nothing is left on the character:
    /// with the cuffs off, the animator's next frame is all there is.
    /// </summary>
    public sealed class CuffPose : MonoBehaviour
    {
        private const int Slots = 2;

        private sealed class Cuffed
        {
            public ThirdPersonCharacter Character;
            public Transform UpperL, LowerL, HandL, UpperR, LowerR, HandR;
            public GameObject Cuffs;   // the modelled handcuffs, if they could be had; kept and reused
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
                // The one prop of this pose: made the first time somebody is cuffed after the props have loaded.
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
                    cuffed.Character = null;   // (also anyone destroyed with the street)
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
                    // Not while they are a ragdoll (physics has the body), and not while nobody is drawing them.
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

        // True while one of the mod's clips has the body (cuffed_stand, cuffed_walk, or the fade between them and
        // the game's animation): the arms are that clip's business, and aiming them as well would fight it.
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
            Aim(cuffed.UpperL, cuffed.LowerL, -up * 0.86f - forward * 0.42f - right * 0.10f);
            Aim(cuffed.LowerL, cuffed.HandL, right * 0.80f - forward * 0.38f - up * 0.30f);
            Aim(cuffed.UpperR, cuffed.LowerR, -up * 0.86f - forward * 0.42f + right * 0.10f);
            Aim(cuffed.LowerR, cuffed.HandR, -right * 0.80f - forward * 0.38f - up * 0.30f);
        }

        // The handcuffs lie across the wrists, flat against the back. Moved with them while they are held; hidden otherwise.
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

        private static void Aim(Transform from, Transform towards, Vector3 direction)
        {
            BonePose.Aim(from, towards, direction);
        }
    }
}
