using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How a tool sits in a hand, as the clip it belongs to was made: on which hand bone, where
    /// and how it is turned on that bone, and (for a tool held in both hands) where on the tool
    /// the other wrist belongs. Written by the animation pipeline into the clip catalogue; never
    /// made up in code.
    /// </summary>
    public struct ToolGrip
    {
        public HumanBodyBones Hand;
        public Vector3 Position;       // of the tool's origin on the hand bone
        public Quaternion Rotation;
        public bool TwoHanded;
        public HumanBodyBones OtherHand;
        public Vector3 WristOnTool;    // where the other wrist is, in the tool's own space
    }

    /// <summary>
    /// A tool in somebody's hands. The tool object is parented to the hand bone at the clip's
    /// grip, so it moves with the hand for nothing. If the clip holds it with both hands the
    /// other hand is kept on it by <see cref="TwoBoneReach"/> after the animation each frame
    /// (ActivityAnimators does that, and only for people who are being animated), because a
    /// retargeted arm ends up a few centimetres from where the clip's author put it.
    /// </summary>
    public sealed class HeldTool
    {
        private Animator _animator;
        private Transform _tool;
        private Transform _upper;
        private Transform _lower;
        private Transform _wrist;
        private Vector3 _wristOnTool;
        private float _weight;
        private float _weightGoal;

        public bool Held => _tool != null;
        public Transform Tool => _tool;

        /// <summary>
        /// Puts a tool object in the character's hand. False (and nothing changed) if this
        /// character has no such bone. The tool keeps its life size whatever scale the bones carry.
        /// </summary>
        public bool Attach(Animator animator, Transform tool, ToolGrip grip)
        {
            Detach(null);
            if (animator == null || tool == null || !animator.isHuman)
            {
                return false;
            }
            Transform hand = animator.GetBoneTransform(grip.Hand);
            if (hand == null)
            {
                return false;
            }
            Vector3 scale = hand.lossyScale;
            if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f || Mathf.Abs(scale.z) < 0.001f)
            {
                return false;
            }
            tool.SetParent(hand, false);
            tool.localPosition = new Vector3(grip.Position.x / scale.x, grip.Position.y / scale.y, grip.Position.z / scale.z);
            tool.localRotation = grip.Rotation;
            tool.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            _animator = animator;
            _tool = tool;
            _upper = null;
            if (grip.TwoHanded)
            {
                bool left = grip.OtherHand == HumanBodyBones.LeftHand;
                _upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                _lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                _wrist = animator.GetBoneTransform(grip.OtherHand);
                _wristOnTool = grip.WristOnTool;
                if (_upper == null || _lower == null || _wrist == null)
                {
                    _upper = null;
                }
            }
            _weight = 0f;
            _weightGoal = 1f;
            if (_upper != null)
            {
                ActivityAnimators.Reach(this);
            }
            return true;
        }

        /// <summary>
        /// Takes the tool out of the hand and gives it to another parent (or none), where it is
        /// in the world now. The other hand lets go at once.
        /// </summary>
        public void Detach(Transform parent)
        {
            if (_tool != null)
            {
                _tool.SetParent(parent, true);
                _tool.localScale = Vector3.one;
            }
            _tool = null;
            _animator = null;
            _upper = null;
            _weight = 0f;
        }

        /// <summary>The other hand comes off the tool over a moment: nothing that needs both hands is being played.</summary>
        public void LetGo()
        {
            _weightGoal = 0f;
        }

        /// <summary>
        /// A different clip is about to hold the same tool in the same hand. Its grip says whether
        /// the other hand is on the tool as well: that hand comes off or goes back on over a moment.
        /// </summary>
        public void Regrip(ToolGrip grip)
        {
            if (_tool == null || _animator == null)
            {
                return;
            }
            if (!grip.TwoHanded)
            {
                _weightGoal = 0f;
                return;
            }
            bool left = grip.OtherHand == HumanBodyBones.LeftHand;
            Transform upper = _animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = _animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform wrist = _animator.GetBoneTransform(grip.OtherHand);
            if (upper == null || lower == null || wrist == null)
            {
                return;
            }
            if (_upper != upper)
            {
                _weight = 0f;
            }
            _upper = upper;
            _lower = lower;
            _wrist = wrist;
            _wristOnTool = grip.WristOnTool;
            _weightGoal = 1f;
            ActivityAnimators.Reach(this);
        }

        /// <summary>Called after the animation, once per frame per tool held in both hands. False when it no longer needs calling.</summary>
        internal bool Reach(float delta)
        {
            if (_tool == null || _upper == null || _animator == null)
            {
                return false;
            }
            if (!_animator.enabled || !_animator.gameObject.activeInHierarchy)
            {
                return true;   // not being animated (out of view): nothing to correct, and nothing spent
            }
            _weight = Mathf.MoveTowards(_weight, _weightGoal, delta * 4f);
            TwoBoneReach.Solve(_upper, _lower, _wrist, _tool.TransformPoint(_wristOnTool), _weight);
            return true;
        }
    }
}
