using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One bone of a ragdoll: what was added to it, and what it looked like before.</summary>
    internal struct RagdollPart
    {
        public Transform Bone;
        public Rigidbody Body;
        public Collider Collider;
        public CharacterJoint Joint;
        public Vector3 LocalPosition;      // the animated pose, put back when the ragdoll ends
        public Quaternion LocalRotation;
        public int Layer;
    }

    /// <summary>
    /// Builds a ragdoll's body on a humanoid skeleton and takes it off again: eleven bones get a rigidbody, a collider
    /// sized from the distances between the bones, and a joint with roughly human limits. The game has no ragdolls, so
    /// nothing is assumed about how its bones are rolled: every axis comes from where the neighbouring bones are.
    /// </summary>
    internal static class RagdollRig
    {
        public const int Hips = 0;
        public const int Torso = 1;
        public const int Head = 2;
        public const int UpperArmL = 3;
        public const int LowerArmL = 4;
        public const int UpperArmR = 5;
        public const int LowerArmR = 6;
        public const int UpperLegL = 7;
        public const int LowerLegL = 8;
        public const int UpperLegR = 9;
        public const int LowerLegR = 10;
        public const int PartCount = 11;

        // The far ends of the forearms and shins, for their lengths. Any of them may be missing.
        public const int HandL = 0;
        public const int HandR = 1;
        public const int FootL = 2;
        public const int FootR = 3;
        public const int EndCount = 4;

        // Kilograms; 75 in all. Hands and feet are counted with the forearms and shins.
        private static readonly float[] Mass = { 12f, 24f, 5f, 2.2f, 1.6f, 2.2f, 1.6f, 8f, 5.2f, 8f, 5.2f };

        private const float ElbowReach = 140f;   // degrees a joint can fold from straight
        private const float KneeReach = 130f;    // degrees

        /// <summary>Finds the bones. False if this skeleton lacks any of the eleven, or is not near natural size and evenly scaled.</summary>
        public static bool Resolve(Animator animator, RagdollPart[] parts, Transform[] ends)
        {
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine == null)
            {
                spine = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
            parts[Hips].Bone = animator.GetBoneTransform(HumanBodyBones.Hips);
            parts[Torso].Bone = spine;
            parts[Head].Bone = animator.GetBoneTransform(HumanBodyBones.Head);
            parts[UpperArmL].Bone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            parts[LowerArmL].Bone = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            parts[UpperArmR].Bone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            parts[LowerArmR].Bone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            parts[UpperLegL].Bone = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            parts[LowerLegL].Bone = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            parts[UpperLegR].Bone = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            parts[LowerLegR].Bone = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            ends[HandL] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            ends[HandR] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            ends[FootL] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            ends[FootR] = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            for (int i = 0; i < PartCount; i++)
            {
                Transform bone = parts[i].Bone;
                // A bone that already carries physics is somebody else's: leave the whole character alone.
                if (bone == null || bone.GetComponent<Rigidbody>() != null || bone.GetComponent<Collider>() != null)
                {
                    return false;
                }
            }
            // The sizes below are metres in the bones' own space, so a character scaled as a whole (an officer made
            // taller: Stature) gets a body that much bigger by itself. Anything else is not a skeleton this is for.
            Vector3 scale = parts[Hips].Bone.lossyScale;
            return scale.y > 0.9f && scale.y < 1.16f && Mathf.Abs(scale.x - scale.y) < 0.02f && Mathf.Abs(scale.z - scale.y) < 0.02f;
        }

        /// <summary>Adds the bodies, colliders and joints, each recorded in the parts as made: on false or a throw the caller strips them.</summary>
        public static bool Build(RagdollPart[] parts, Transform[] ends, int layer)
        {
            // The body's own directions, from the hip joints: they are right however the root is turned.
            Vector3 right = parts[UpperLegR].Bone.position - parts[UpperLegL].Bone.position;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            right.Normalize();
            Vector3 forward = Vector3.Cross(right, Vector3.up);

            for (int i = 0; i < PartCount; i++)
            {
                var body = parts[i].Bone.gameObject.AddComponent<Rigidbody>();
                if (body == null)
                {
                    return false;
                }
                parts[i].Body = body;
                parts[i].Bone.gameObject.layer = layer;
                body.mass = Mass[i];
                body.drag = 0.05f;
                body.angularDrag = 0.8f;
                body.maxAngularVelocity = 18f;
                body.maxDepenetrationVelocity = 3f;    // starts touching the ground and perhaps the car: ease out, never fling
                body.solverIterations = 10;
                // Not interpolated: the bodies hang from one another, and interpolating each on its own pulls a chain apart.
                body.interpolation = RigidbodyInterpolation.None;
                // The only continuous mode a body can keep once it is frozen (kinematic).
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            Transform hips = parts[Hips].Bone;
            Transform spine = parts[Torso].Bone;
            Transform head = parts[Head].Bone;
            float metres = hips.lossyScale.y;   // widths are measured in the world and set in the bones' own space
            float hipWidth = Vector3.Distance(parts[UpperLegL].Bone.position, parts[UpperLegR].Bone.position) / metres;
            float shoulderWidth = Vector3.Distance(parts[UpperArmL].Bone.position, parts[UpperArmR].Bone.position) / metres;

            // Trunk: two boxes. The torso one runs from the base of the spine to the base of the skull.
            Vector3 toSpine = hips.InverseTransformPoint(spine.position);
            AddBox(ref parts[Hips], toSpine, toSpine.normalized * 0.02f, right,
                0.2f, Mathf.Clamp(hipWidth + 0.18f, 0.28f, 0.4f), 0.2f);
            Vector3 toHead = spine.InverseTransformPoint(head.position);
            float torsoHeight = Mathf.Clamp(toHead.magnitude, 0.25f, 0.7f);
            AddBox(ref parts[Torso], toHead, toHead * 0.5f, right,
                torsoHeight, Mathf.Clamp(shoulderWidth * 0.9f, 0.26f, 0.42f), 0.2f);

            Vector3 neck = head.position - spine.position;
            var skull = head.gameObject.AddComponent<SphereCollider>();
            parts[Head].Collider = skull;
            skull.radius = 0.1f;
            skull.center = head.InverseTransformDirection(neck.sqrMagnitude > 0.0001f ? neck.normalized : Vector3.up) * 0.09f;

            AddLimb(ref parts[UpperArmL], parts[LowerArmL].Bone, 0.28f, 0.05f);
            AddLimb(ref parts[UpperArmR], parts[LowerArmR].Bone, 0.28f, 0.05f);
            AddLimb(ref parts[LowerArmL], ends[HandL], 0.26f, 0.042f);
            AddLimb(ref parts[LowerArmR], ends[HandR], 0.26f, 0.042f);
            AddLimb(ref parts[UpperLegL], parts[LowerLegL].Bone, 0.42f, 0.075f);
            AddLimb(ref parts[UpperLegR], parts[LowerLegR].Bone, 0.42f, 0.075f);
            AddLimb(ref parts[LowerLegL], ends[FootL], 0.38f, 0.055f);
            AddLimb(ref parts[LowerLegR], ends[FootR], 0.38f, 0.055f);

            // No part collides with another of the same body: an arm may pass through the chest, but parts pushing on
            // each other are what make a ragdoll blow itself apart.
            for (int i = 0; i < PartCount; i++)
            {
                for (int j = i + 1; j < PartCount; j++)
                {
                    Physics.IgnoreCollision(parts[i].Collider, parts[j].Collider, true);
                }
            }

            // Limits are measured from the pose the joint is made in, and turning the positive way about the axis counts
            // towards the low limit: about the body's right, low is bowing, nodding and a leg swinging back.
            Join(ref parts[Torso], parts[Hips].Body, right, forward, -45f, 25f, 20f, 20f);
            Join(ref parts[Head], parts[Torso].Body, right, forward, -45f, 35f, 30f, 40f);
            AddShoulder(ref parts[UpperArmL], parts[Torso].Body, parts[LowerArmL].Bone, forward, right);
            AddShoulder(ref parts[UpperArmR], parts[Torso].Body, parts[LowerArmR].Bone, forward, right);
            Join(ref parts[UpperLegL], parts[Hips].Body, right, forward, -25f, 95f, 35f, 20f);
            Join(ref parts[UpperLegR], parts[Hips].Body, right, forward, -25f, 95f, 35f, 20f);
            AddHinge(ref parts[LowerArmL], parts[UpperArmL], ends[HandL], ElbowReach, forward, right, elbow: true);
            AddHinge(ref parts[LowerArmR], parts[UpperArmR], ends[HandR], ElbowReach, forward, right, elbow: true);
            AddHinge(ref parts[LowerLegL], parts[UpperLegL], ends[FootL], KneeReach, forward, right, elbow: false);
            AddHinge(ref parts[LowerLegR], parts[UpperLegR], ends[FootR], KneeReach, forward, right, elbow: false);
            return true;
        }

        /// <summary>Removes everything Build added. Joints go first: a body cannot be removed while a joint needs it.</summary>
        public static void Strip(RagdollPart[] parts)
        {
            for (int i = 0; i < PartCount; i++)
            {
                if (parts[i].Joint != null)
                {
                    Object.DestroyImmediate(parts[i].Joint);
                }
                parts[i].Joint = null;
            }
            for (int i = 0; i < PartCount; i++)
            {
                if (parts[i].Collider != null)
                {
                    Object.DestroyImmediate(parts[i].Collider);
                }
                parts[i].Collider = null;
                if (parts[i].Body != null)
                {
                    Object.DestroyImmediate(parts[i].Body);
                }
                parts[i].Body = null;
            }
        }

        // A box on a trunk bone: its tall side along the bone and its wide side along the body's right, whichever of the
        // bone's own axes those lie along.
        private static void AddBox(ref RagdollPart part, Vector3 localUp, Vector3 centre, Vector3 right, float height, float width, float depth)
        {
            Transform bone = part.Bone;
            int up = MainAxis(localUp);
            Vector3 across = bone.InverseTransformDirection(right);
            across[up] = 0f;
            int side = across.sqrMagnitude > 0.0001f ? MainAxis(across) : (up + 1) % 3;
            Vector3 size = Vector3.zero;
            size[up] = height;
            size[side] = width;
            size[3 - up - side] = depth;

            var box = bone.gameObject.AddComponent<BoxCollider>();
            part.Collider = box;
            box.center = centre;
            box.size = size;
        }

        // A capsule from a limb bone to the next joint down, or of a usual length if that bone is unknown.
        private static void AddLimb(ref RagdollPart part, Transform end, float usualLength, float radius)
        {
            Transform bone = part.Bone;
            Vector3 toEnd = end != null ? bone.InverseTransformPoint(end.position) : new Vector3(0f, usualLength, 0f);
            float length = toEnd.magnitude;
            if (length < 0.08f || length > 0.8f)
            {
                toEnd = new Vector3(0f, usualLength, 0f);
                length = usualLength;
            }
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            part.Collider = capsule;
            capsule.direction = MainAxis(toEnd);
            capsule.radius = radius;
            capsule.height = length + radius;   // rounded ends that reach a little past each joint
            capsule.center = toEnd * 0.5f;
        }

        // Shoulders are the loosest joints: free to twist about the arm and to swing most ways.
        private static void AddShoulder(ref RagdollPart arm, Rigidbody torso, Transform elbow, Vector3 forward, Vector3 right)
        {
            Vector3 along = elbow.position - arm.Bone.position;
            along = along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.down;
            Join(ref arm, torso, along, Mathf.Abs(Vector3.Dot(along, forward)) > 0.9f ? right : forward, -70f, 70f, 80f, 60f);
        }

        // Elbows and knees fold one way, about the axis the limb is already bent about; a straight limb takes it from the
        // body (knees fold back, elbows forward).
        private static void AddHinge(ref RagdollPart lower, RagdollPart upper, Transform end, float reach, Vector3 forward, Vector3 right, bool elbow)
        {
            Transform bone = lower.Bone;
            Vector3 upperAlong = bone.position - upper.Bone.position;
            Vector3 lowerAlong = end != null ? end.position - bone.position : upperAlong;
            if (lowerAlong.sqrMagnitude < 0.0001f)
            {
                lowerAlong = upperAlong;
            }
            float bend = Vector3.Angle(upperAlong, lowerAlong);
            Vector3 axis = Vector3.Cross(upperAlong, lowerAlong);
            if (bend < 8f || axis.sqrMagnitude < 0.000001f)
            {
                axis = elbow ? Vector3.Cross(lowerAlong, forward) : Vector3.Cross(forward, lowerAlong);
                if (axis.sqrMagnitude < 0.000001f)
                {
                    axis = right;
                }
            }
            // From this pose it may fold the rest of the way, and straighten as far as it is bent now.
            float fold = Mathf.Clamp(reach - bend, 5f, 170f);
            float open = Mathf.Clamp(bend, 0f, 170f) + 3f;
            Join(ref lower, upper.Body, axis.normalized, lowerAlong.normalized, -fold, open, 5f, 5f);
        }

        private static void Join(ref RagdollPart part, Rigidbody parent, Vector3 axis, Vector3 swingAxis, float low, float high, float swing1, float swing2)
        {
            Transform bone = part.Bone;
            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            part.Joint = joint;
            joint.autoConfigureConnectedAnchor = true;
            joint.anchor = Vector3.zero;   // the bone's own origin is the joint
            joint.axis = bone.InverseTransformDirection(axis);
            joint.swingAxis = bone.InverseTransformDirection(swingAxis);
            joint.connectedBody = parent;
            joint.lowTwistLimit = Limit(low);
            joint.highTwistLimit = Limit(high);
            joint.swing1Limit = Limit(swing1);
            joint.swing2Limit = Limit(swing2);
            joint.enableCollision = false;
            joint.enablePreprocessing = false;   // steadier for chains of light bodies
            joint.enableProjection = true;       // a joint pulled apart by a hard hit is put back together
        }

        private static SoftJointLimit Limit(float degrees)
        {
            return new SoftJointLimit { limit = degrees };
        }

        // 0, 1 or 2: the axis a direction mostly lies along.
        private static int MainAxis(Vector3 direction)
        {
            float x = Mathf.Abs(direction.x);
            float y = Mathf.Abs(direction.y);
            float z = Mathf.Abs(direction.z);
            return x > y && x > z ? 0 : z > y ? 2 : 1;
        }
    }
}
