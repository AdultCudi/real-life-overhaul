"""Reads the street characters' Avatar (ArmatureLODAvatar) from the game: skeleton, T-pose, human bone map.

LOCAL REFERENCE ONLY: written to CityAssets/_reference (git-ignored), never shipped. The Unity check uses it,
when it is there, to play the clips on the game's own skeleton and to measure where its hands are.

    python -I extract_game_skeleton.py <StandaloneWindows64 folder> <out.json> <pylib folder with UnityPy>
"""
import glob
import json
import os
import sys

data_dir, out_path, pylib = sys.argv[1], sys.argv[2], sys.argv[3]
sys.path.insert(0, pylib)
import UnityPy  # noqa: E402

HUMAN = ["Hips", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot", "Spine", "Chest", "Neck", "Head",
         "LeftShoulder", "RightShoulder", "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand",
         "LeftToes", "RightToes", "LeftEye", "RightEye", "Jaw", "UpperChest"]   # the order of mecanim's body bone indices

bundle = glob.glob(os.path.join(data_dir, "defaultlocalgroup_assets_prefabs_*.bundle"))[0]
env = UnityPy.load(bundle)
for obj in env.objects:
    if obj.type.name != "Avatar":
        continue
    tree = obj.read_typetree()
    if tree["m_Name"] != "ArmatureLODAvatar":
        continue
    tos = {k: v for k, v in tree["m_TOS"]}
    avatar = tree["m_Avatar"]

    def un(x):
        return x.get("data", x) if isinstance(x, dict) else x

    def skeleton(sk, pose):
        sk, pose = un(sk), un(pose)
        nodes = []
        for node, ident, x in zip(sk["m_Node"], sk["m_ID"], pose["m_X"]):
            t, q, s = x["t"], x["q"], x["s"]
            path = tos.get(ident, "")
            nodes.append({"path": path, "name": path.split("/")[-1], "parent": node["m_ParentId"], "axes": node["m_AxesId"],
                          "t": [t["x"], t["y"], t["z"]], "q": [q["x"], q["y"], q["z"], q["w"]], "s": [s["x"], s["y"], s["z"]]})
        return nodes
    human = un(avatar["m_Human"])
    out = {
        "keys": sorted(tree.keys()), "avatarKeys": sorted(avatar.keys()), "humanKeys": sorted(human.keys()),
        "skeleton": skeleton(avatar["m_AvatarSkeleton"], avatar["m_DefaultPose"]),
        "humanSkeleton": skeleton(human["m_Skeleton"], human["m_SkeletonPose"]),
        "humanBoneIndex": dict(zip(HUMAN, human["m_HumanBoneIndex"])),
        "scale": human.get("m_Scale"), "hasLeftHand": human.get("m_HasLeftHand"), "hasRightHand": human.get("m_HasRightHand"),
        "rootX": human.get("m_RootX"), "humanSkeletonIndex": avatar.get("m_HumanSkeletonIndexArray"),
        "humanDescription": tree.get("m_HumanDescription"),
    }
    with open(out_path, "w") as f:
        json.dump(out, f, indent=1)
    print("bones", len(out["skeleton"]), "human skeleton", len(out["humanSkeleton"]))
    break
