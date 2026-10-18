using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Someone laid flat on their back along a bed, posed by hand from where the joints are (<see cref="BonePose"/>), on an
    /// animated character or a ragdoll at rest. Called by the stretcher only while it is wheeled.
    /// </summary>
    internal sealed class SupinePose
    {
        private const int Hips = 0, Spine = 1, Chest = 2, UpperChest = 3, Neck = 4, Head = 5;
        private const int ThighL = 6, ShinL = 7, FootL = 8, ThighR = 9, ShinR = 10, FootR = 11;
        private const int ArmL = 12, ForearmL = 13, HandL = 14, ArmR = 15, ForearmR = 16, HandR = 17;
        private const int ToesL = 18, ToesR = 19;
        private const int Count = 20;

        private static readonly HumanBodyBones[] Names =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftToes, HumanBodyBones.RightToes
        };

        // The trunk, hips to head; a rig need not have every joint of it.
        private static readonly int[] Trunk = { Hips, Spine, Chest, UpperChest, Neck, Head };

        private readonly Transform[] _bones = new Transform[Count];
        private readonly Quaternion[] _from = new Quaternion[Count];
        private readonly Quaternion[] _to = new Quaternion[Count];
        private Transform _root;
        private Vector3 _rootFrom;
        private Quaternion _rootFromRotation;
        private Vector3 _rootOnBed;            // the root in the bed's own frame, once laid
        private Quaternion _rootOnBedRotation;
        private Animator _animator;
        private ThirdPersonCharacter _character;   // whose animator it is, if it is one of the game's characters
        private bool _ownsAnimator;            // the body is animated, not a ragdoll: its animator is kept off while the pose is held

        public bool Active { get; private set; }

        /// <summary>
        /// Works out the pose for a body to lie on a bed, head to the bed's forward, hips at <paramref name="hipsOnBed"/> in
        /// the bed's frame; the body is left as it was. False, with nothing changed, if the skeleton cannot be posed.
        /// <paramref name="animated"/>: not a ragdoll, so its animator is kept off until <see cref="Clear"/>.
        /// </summary>
        public bool Begin(Transform root, Animator animator, Transform bed, Vector3 hipsOnBed, bool animated)
        {
            Clear();
            if (root == null || bed == null || animator == null || !animator.isHuman || animator.avatar == null)
            {
                return false;
            }
            for (int i = 0; i < Count; i++)
            {
                _bones[i] = animator.GetBoneTransform(Names[i]);
                bool optional = i == Chest || i == UpperChest || i == Neck || i == ToesL || i == ToesR;
                if (_bones[i] == null && !optional)
                {
                    Forget();
                    return false;
                }
            }
            _root = root;
            _animator = animator;
            _character = root.GetComponent<ThirdPersonCharacter>();
            _ownsAnimator = animated;
            if (animated && animator.enabled)
            {
                animator.enabled = false;   // the bones now hold the pose it last gave them
            }
            _rootFrom = root.position;
            _rootFromRotation = root.rotation;
            for (int i = 0; i < Count; i++)
            {
                _from[i] = _bones[i] != null ? _bones[i].localRotation : Quaternion.identity;
            }

            Lay(bed.forward, bed.up);
            root.position += bed.TransformPoint(hipsOnBed) - _bones[Hips].position;
            _rootOnBed = bed.InverseTransformPoint(root.position);
            _rootOnBedRotation = Quaternion.Inverse(bed.rotation) * root.rotation;
            for (int i = 0; i < Count; i++)
            {
                if (_bones[i] != null)
                {
                    _to[i] = _bones[i].localRotation;
                    _bones[i].localRotation = _from[i];
                }
            }
            root.SetPositionAndRotation(_rootFrom, _rootFromRotation);
            Active = true;
            return true;
        }

        /// <summary>Puts the body part of the way (0 to 1) from how it lay to lying on the bed, wherever the bed now is.</summary>
        public void Set(Transform bed, float progress)
        {
            if (!Active || _root == null)
            {
                return;
            }
            Quiet();
            float t = Mathf.SmoothStep(0f, 1f, progress);
            for (int i = 0; i < Count; i++)
            {
                if (_bones[i] != null)
                {
                    _bones[i].localRotation = t >= 1f ? _to[i] : Quaternion.Slerp(_from[i], _to[i], t);
                }
            }
            Vector3 position = bed.TransformPoint(_rootOnBed);
            Quaternion rotation = bed.rotation * _rootOnBedRotation;
            if (t < 1f)
            {
                position = Vector3.Lerp(_rootFrom, position, t);
                rotation = Quaternion.Slerp(_rootFromRotation, rotation, t);
            }
            _root.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Keeps the laid body on the bed as it moves, by its root alone unless something woke the animator.</summary>
        public void Hold(Transform bed)
        {
            if (!Active || _root == null)
            {
                return;
            }
            if (Quiet())
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_bones[i] != null)
                    {
                        _bones[i].localRotation = _to[i];
                    }
                }
            }
            _root.SetPositionAndRotation(bed.TransformPoint(_rootOnBed), bed.rotation * _rootOnBedRotation);
        }

        /// <summary>Lets the body go: an animated character gets its animator back; a ragdoll is left as it is. Safe at any time.</summary>
        public void Clear()
        {
            if (!Active)
            {
                return;
            }
            Active = false;
            if (_ownsAnimator && _animator != null)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (_bones[i] != null)
                    {
                        _bones[i].localRotation = _from[i];
                    }
                }
                _animator.enabled = _character == null || _character.visible;   // someone out of view stays switched off, as the pool left them
            }
            Forget();
        }

        // The pool switches the animator of someone coming into view back on: off again. True if it was on.
        private bool Quiet()
        {
            if (!_ownsAnimator || _animator == null || !_animator.enabled)
            {
                return false;
            }
            _animator.enabled = false;
            return true;
        }

        private void Forget()
        {
            _root = null;
            _animator = null;
            _character = null;
            _ownsAnimator = false;
            for (int i = 0; i < Count; i++)
            {
                _bones[i] = null;
            }
        }

        // The pose itself, set on the bones as they are now: the whole body turned head to the bed's head and
        // face up, then each joint aimed along the bed, parents before children.
        private void Lay(Vector3 along, Vector3 up)
        {
            Transform hips = _bones[Hips];
            Vector3 pivot = hips.position;
            Turn(_root, pivot, Quaternion.FromToRotation(_bones[Head].position - pivot, along));
            Turn(_root, pivot, FaceUp(_bones[ThighL], _bones[ThighR], along, up));

            int last = Hips;
            for (int i = 1; i < Trunk.Length - 1; i++)
            {
                int joint = Trunk[i];
                if (_bones[joint] == null)
                {
                    continue;
                }
                BonePose.Aim(_bones[last], _bones[joint], along);
                last = joint;
            }
            // Aiming a joint leaves it rolled as it was: hips and shoulders are each turned level again.
            hips.rotation = FaceUp(_bones[ThighL], _bones[ThighR], along, up) * hips.rotation;
            _bones[Spine].rotation = FaceUp(_bones[ArmL], _bones[ArmR], along, up) * _bones[Spine].rotation;
            // Then the head, a little raised on the pillow: after the turns above, so that the rise is straight up.
            BonePose.Aim(_bones[last], _bones[Head], along + up * 0.16f);

            Vector3 right = _bones[ThighR].position - _bones[ThighL].position;
            right -= along * Vector3.Dot(right, along);
            right = right.sqrMagnitude < 0.0001f ? Vector3.Cross(up, along) : right.normalized;

            Leg(ThighL, ShinL, FootL, ToesL, along, up, -right);
            Leg(ThighR, ShinR, FootR, ToesR, along, up, right);
            Arm(ArmL, ForearmL, HandL, along, up, -right);
            Arm(ArmR, ForearmR, HandR, along, up, right);
        }

        private void Leg(int thigh, int shin, int foot, int toes, Vector3 along, Vector3 up, Vector3 side)
        {
            BonePose.Aim(_bones[thigh], _bones[shin], -along + side * 0.05f);
            BonePose.Aim(_bones[shin], _bones[foot], -along);
            if (_bones[toes] != null)
            {
                BonePose.Aim(_bones[foot], _bones[toes], up - along * 0.35f + side * 0.15f);
            }
        }

        // Down the side, the hand resting on the hip: nothing hangs over the edge of the bed.
        private void Arm(int upper, int forearm, int hand, Vector3 along, Vector3 up, Vector3 side)
        {
            BonePose.Aim(_bones[upper], _bones[forearm], -along + side * 0.12f - up * 0.06f);
            BonePose.Aim(_bones[forearm], _bones[hand], -along - side * 0.06f + up * 0.07f);
        }

        // The turn about the bed's long axis that brings the body's front (from a left and a right joint) face up.
        private static Quaternion FaceUp(Transform left, Transform right, Vector3 along, Vector3 up)
        {
            Vector3 front = Vector3.Cross(right.position - left.position, along);
            front -= along * Vector3.Dot(front, along);
            if (front.sqrMagnitude < 0.000001f)
            {
                return Quaternion.identity;
            }
            return Quaternion.AngleAxis(Vector3.SignedAngle(front, up, along), along);
        }

        private static void Turn(Transform root, Vector3 pivot, Quaternion turn)
        {
            root.SetPositionAndRotation(pivot + turn * (root.position - pivot), turn * root.rotation);
        }
    }
}
