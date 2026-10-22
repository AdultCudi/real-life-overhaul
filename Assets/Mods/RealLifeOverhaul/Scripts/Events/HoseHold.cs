using System;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The firefighter's hands on the nozzle, which stands in the world aimed at the fire: after each frame's animation both
    /// wrists are eased onto it with TwoBoneReach, where hose_hold's own grip has them, or at its two grips without that
    /// clip. Enabled only while somebody holds a nozzle and is animated: about thirty vector operations a frame, no
    /// allocation.
    /// </summary>
    internal sealed class HoseHold : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("events", "HoseHold", typeof(HoseHold));

        private static readonly Vector3 RightOnNozzle = new Vector3(0f, 0f, 0.06f);
        private static readonly Vector3 LeftOnNozzle = new Vector3(0f, -0.02f, 0.30f);

        private Animator _animator;
        private Transform _nozzle;
        private Transform _rightUpper, _rightLower, _rightHand, _leftUpper, _leftLower, _leftHand;
        private Vector3 _right = RightOnNozzle;   // where each wrist belongs, in the nozzle's own space
        private Vector3 _left = LeftOnNozzle;
        private float _weight;

        private void Awake()
        {
            enabled = false;
        }

        /// <summary>The hands as a clip's grip holds the nozzle: the nozzle on the right hand, the left wrist on the nozzle.</summary>
        public void Begin(Animator animator, Transform nozzle, ToolGrip grip)
        {
            Begin(animator, nozzle);
            if (enabled && grip.Hand == HumanBodyBones.RightHand && grip.TwoHanded && grip.OtherHand == HumanBodyBones.LeftHand)
            {
                _right = Quaternion.Inverse(grip.Rotation) * -grip.Position;
                _left = grip.WristOnTool;
            }
        }

        public void Begin(Animator animator, Transform nozzle)
        {
            End();
            _right = RightOnNozzle;
            _left = LeftOnNozzle;
            if (animator == null || nozzle == null || !animator.isHuman)
            {
                return;
            }
            _rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (_rightUpper == null || _rightLower == null || _rightHand == null || _leftUpper == null || _leftLower == null || _leftHand == null)
            {
                return;
            }
            _animator = animator;
            _nozzle = nozzle;
            _weight = 0f;
            enabled = true;
        }

        public void End()
        {
            _animator = null;
            _nozzle = null;
            enabled = false;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Events: HoseHold.LateUpdate");
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
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            try
            {
                if (_animator == null || _nozzle == null)
                {
                    End();
                    return;
                }
                if (!_animator.enabled || !_animator.gameObject.activeInHierarchy)
                {
                    return;   // out of view: nothing to correct
                }
                _weight = Mathf.MoveTowards(_weight, 1f, Time.deltaTime * 3f);
                // The right hand at the back of the nozzle by the hip, the left further along it.
                TwoBoneReach.Solve(_rightUpper, _rightLower, _rightHand, _nozzle.TransformPoint(_right), _weight);
                TwoBoneReach.Solve(_leftUpper, _leftLower, _leftHand, _nozzle.TransformPoint(_left), _weight);
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
                End();
            }
        }
    }
}
