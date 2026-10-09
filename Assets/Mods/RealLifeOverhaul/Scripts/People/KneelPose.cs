using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The fallback for the mod's kneeling clips (kneel_work, cpr: StreetActor.Kneel plays those
    /// whenever they can be had). Someone down on one knee, leaning a little over whoever is lying
    /// in front of them, both hands low on that person's chest. The game has no kneeling or crouching animation (its
    /// nearest, the gym squat, stands holding a bar), so this is a pose set by hand: the
    /// animator is switched off and a dozen bones are turned once (BonePose). Each bone is aimed
    /// along a direction given in the character's own frame, and each hand is put at a place
    /// worked out from the measured length of that arm, so the hands end where they are meant
    /// to on any build and never above the elbows.
    /// Holding the pose costs nothing: no component, nothing per frame.
    /// Clear puts every bone back as it was and hands the body back to the animator.
    /// One of these belongs to each StreetActor that has ever knelt.
    /// </summary>
    internal sealed class KneelPose
    {
        private const float KneeHeight = 0.07f;   // metres from the ground to the middle of the knee that is down
        private const float Lean = 38f;           // degrees the back is bent forward
        private const float HandForward = 0.50f;  // metres in front of the kneeler that the hands rest: the near side of a chest 0.75 m away
        private const float HandHeight = 0.30f;   // ...and above the ground: just over the chest of someone lying on their back
        private const float HandApart = 0.13f;    // ...and each side of the middle

        // Bones turned, parents before children; then the far ends that give the last ones a direction.
        private const int Hips = 0;
        private const int Spine = 1;
        private const int ThighFront = 2;
        private const int ShinFront = 3;
        private const int ThighDown = 4;
        private const int ShinDown = 5;
        private const int FootDown = 6;
        private const int UpperArmL = 7;
        private const int ForearmL = 8;
        private const int UpperArmR = 9;
        private const int ForearmR = 10;
        private const int FootFront = 11;
        private const int Turned = 12;
        private const int ToesDown = 12;
        private const int HandL = 13;
        private const int HandR = 14;
        private const int Head = 15;
        private const int Count = 16;

        private static readonly HumanBodyBones[] Names =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightToes, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Head
        };

        private readonly Transform[] _bones = new Transform[Count];
        private readonly Quaternion[] _rotations = new Quaternion[Turned];
        private Vector3 _hipsPosition;
        private Animator _animator;

        /// <summary>True from Apply to Clear.</summary>
        public bool Active { get; private set; }

        /// <summary>Kneels the character where it stands, facing the way it faces. False, with nothing changed, if its skeleton is not one this can pose.</summary>
        public bool Apply(ThirdPersonCharacter character)
        {
            if (Active)
            {
                return true;
            }
            Animator animator = character.animator;
            if (animator == null || !animator.isHuman || animator.avatar == null)
            {
                return false;
            }
            for (int i = 0; i < Count; i++)
            {
                _bones[i] = animator.GetBoneTransform(Names[i]);
                // The toes only lay the trailing foot flat; everything else is needed.
                if (_bones[i] == null && i != ToesDown)
                {
                    return false;
                }
            }
            for (int i = 0; i < Turned; i++)
            {
                _rotations[i] = _bones[i].localRotation;
            }
            _hipsPosition = _bones[Hips].localPosition;
            _animator = animator;
            Active = true;
            Set(character);
            return true;
        }

        /// <summary>
        /// Puts the pose on again. For after something switched the animator back on (the pool
        /// showing someone who had been out of view): aiming a bone where it already points
        /// changes nothing, so this is safe to repeat.
        /// </summary>
        public void Set(ThirdPersonCharacter character)
        {
            if (!Active || _animator == null)
            {
                return;
            }
            _animator.enabled = false;

            Transform root = character.transform;
            Vector3 forward = root.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);

            // Down until the right knee is on the ground: that thigh hangs straight from the hip.
            Transform hips = _bones[Hips];
            float thigh = Vector3.Distance(_bones[ThighDown].position, _bones[ShinDown].position);
            float drop = _bones[ThighDown].position.y - (root.position.y + KneeHeight + thigh);
            hips.position -= up * drop;

            Aim(Spine, Head, Quaternion.AngleAxis(Lean, right) * up);

            // Left leg forward with the foot planted; right knee down, shin and foot laid back along the ground.
            Quaternion ankle = _bones[FootFront].rotation;
            Aim(ThighFront, ShinFront, forward - up * 0.06f - right * 0.18f);
            Aim(ShinFront, FootFront, -up + forward * 0.08f);
            _bones[FootFront].rotation = ankle;
            Aim(ThighDown, ShinDown, -up + forward * 0.12f + right * 0.10f);
            Aim(ShinDown, FootDown, -forward - up * 0.08f);
            if (_bones[ToesDown] != null)
            {
                Aim(FootDown, ToesDown, -forward - up * 0.25f);
            }

            // Both hands low and forward, on the chest of the person on the ground: the arms reach down to a
            // place, elbows out, instead of being held at an angle (which left the hands in the air).
            Vector3 chest = root.position + forward * HandForward + up * HandHeight;
            BonePose.Reach(_bones[UpperArmL], _bones[ForearmL], _bones[HandL], chest - right * HandApart, -right - forward * 0.25f);
            BonePose.Reach(_bones[UpperArmR], _bones[ForearmR], _bones[HandR], chest + right * HandApart, right - forward * 0.25f);
        }

        /// <summary>Back on their feet: every bone as it was, and the animator running if the character is being drawn.</summary>
        public void Clear(ThirdPersonCharacter character)
        {
            if (!Active)
            {
                return;
            }
            Active = false;
            for (int i = 0; i < Turned; i++)
            {
                if (_bones[i] != null)
                {
                    _bones[i].localRotation = _rotations[i];
                }
            }
            if (_bones[Hips] != null)
            {
                _bones[Hips].localPosition = _hipsPosition;
            }
            if (_animator != null && character != null)
            {
                _animator.enabled = character.visible;   // someone out of view stays switched off, as the pool left them
            }
            _animator = null;
            for (int i = 0; i < Count; i++)
            {
                _bones[i] = null;
            }
        }

        private void Aim(int bone, int towards, Vector3 direction)
        {
            BonePose.Aim(_bones[bone], _bones[towards], direction);
        }
    }
}
