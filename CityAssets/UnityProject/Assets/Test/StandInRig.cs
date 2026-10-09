using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The stand-in the clips are tested on: the skeleton of the game's street character, rebuilt from the
/// measurements in scripts/game_rig.json (bone names, hierarchy and T-pose) and given a human Avatar with the
/// same bone map the game uses. Bare transforms: nothing of the game's is in it.
/// </summary>
public static class StandInRig
{
    /// <summary>How the two hand bones are turned in the T-pose, in the character's space.</summary>
    public static Quaternion LeftHandTurn, RightHandTurn;

    [Serializable]
    public class GameRig
    {
        public RigBone[] tposeBones;
    }

    [Serializable]
    public class RigBone
    {
        public string name;
        public string parent;
        public float[] t;
        public float[] q;
    }

    private static readonly string[,] HumanNames =
    {
        { "Hips", "Hips" }, { "Spine", "Spine" }, { "Chest", "Chest" }, { "UpperChest", "UpperChest" }, { "Neck", "Neck" }, { "Skull", "Head" },
        { "Shoulder.L", "LeftShoulder" }, { "UpperArm.L", "LeftUpperArm" }, { "LowerArm.L", "LeftLowerArm" }, { "Hand.L", "LeftHand" },
        { "Shoulder.R", "RightShoulder" }, { "UpperArm.R", "RightUpperArm" }, { "LowerArm.R", "RightLowerArm" }, { "Hand.R", "RightHand" },
        { "UpperLeg.L", "LeftUpperLeg" }, { "LowerLeg.L", "LeftLowerLeg" }, { "Foot.L", "LeftFoot" }, { "ToesFoot.L", "LeftToes" },
        { "UpperLeg.R", "RightUpperLeg" }, { "LowerLeg.R", "RightLowerLeg" }, { "Foot.R", "RightFoot" }, { "ToesFoot.R", "RightToes" },
        { "IndexFinger.L.001", "Left Index Proximal" }, { "IndexFinger.L.002", "Left Index Intermediate" }, { "IndexFinger.L.003", "Left Index Distal" },
        { "RingFinger.L.001", "Left Ring Proximal" }, { "RingFinger.L.002", "Left Ring Intermediate" }, { "RingFinger.L.003", "Left Ring Distal" },
        { "ThumbFinger.L.001", "Left Thumb Proximal" }, { "ThumbFinger.L.002", "Left Thumb Intermediate" }, { "ThumbFinger.L.003", "Left Thumb Distal" },
        { "IndexFinger.R.001", "Right Index Proximal" }, { "IndexFinger.R.002", "Right Index Intermediate" }, { "IndexFinger.R.003", "Right Index Distal" },
        { "RingFinger.R.001", "Right Ring Proximal" }, { "RingFinger.R.002", "Right Ring Intermediate" }, { "RingFinger.R.003", "Right Ring Distal" },
        { "ThumbFinger.R.001", "Right Thumb Proximal" }, { "ThumbFinger.R.002", "Right Thumb Intermediate" }, { "ThumbFinger.R.003", "Right Thumb Distal" }
    };

    public static Animator Build(string json)
    {
        GameRig rig = JsonUtility.FromJson<GameRig>(json);
        if (rig == null || rig.tposeBones == null || rig.tposeBones.Length < 20)
        {
            throw new Exception("game_rig.json has no skeleton");
        }
        var root = new GameObject("StandIn");
        var made = new Dictionary<string, Transform>();
        var skeleton = new List<SkeletonBone> { new SkeletonBone { name = root.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one } };
        foreach (RigBone bone in rig.tposeBones)
        {
            var transform = new GameObject(bone.name).transform;
            transform.SetParent(bone.parent != null && made.TryGetValue(bone.parent, out Transform parent) ? parent : root.transform, false);
            transform.localPosition = new Vector3(bone.t[0], bone.t[1], bone.t[2]);
            transform.localRotation = new Quaternion(bone.q[0], bone.q[1], bone.q[2], bone.q[3]);
            made[bone.name] = transform;
            skeleton.Add(new SkeletonBone { name = bone.name, position = transform.localPosition, rotation = transform.localRotation, scale = Vector3.one });
        }
        var human = new List<HumanBone>();
        for (int i = 0; i < HumanNames.GetLength(0); i++)
        {
            if (made.ContainsKey(HumanNames[i, 0]))
            {
                human.Add(new HumanBone { boneName = HumanNames[i, 0], humanName = HumanNames[i, 1], limit = new HumanLimit { useDefaultValues = true } });
            }
        }
        var description = new HumanDescription
        {
            human = human.ToArray(), skeleton = skeleton.ToArray(), upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
            armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
        };
        LeftHandTurn = made["Hand.L"].rotation;
        RightHandTurn = made["Hand.R"].rotation;
        Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
        avatar.name = "StandIn (game skeleton)";
        var animator = root.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return animator;
    }
}
