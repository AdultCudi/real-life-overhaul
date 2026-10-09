"""Lists what the game's characters can already do: every AnimatorController in the game data with its
parameters, layers and clips (name, length, loop, humanoid or not), and every Avatar (human or not).

LOCAL REFERENCE ONLY: reads the installed game, writes one JSON; nothing from it is shipped.

    python -I inventory_game_animations.py <Big Ambitions_Data folder> <out.json> <pylib folder with UnityPy>
"""
import json
import os
import sys

data_dir, out_path, pylib = sys.argv[1], sys.argv[2], sys.argv[3]
sys.path.insert(0, pylib)
import UnityPy  # noqa: E402

files = sys.argv[4:] or ["resources.assets"] + sorted(f for f in os.listdir(data_dir) if f.startswith("sharedassets") and f.endswith(".assets"))
out = {"controllers": [], "avatars": [], "clips": {}}
for name in files:
    path = os.path.join(data_dir, name)
    try:
        env = UnityPy.load(path)
    except Exception as e:
        print("skip", name, e)
        continue
    for obj in env.objects:
        kind = obj.type.name
        try:
            if kind == "Avatar":
                tree = obj.read_typetree()
                human = tree["m_Avatar"]["m_Human"]["data"] if "data" in tree["m_Avatar"]["m_Human"] else tree["m_Avatar"]["m_Human"]
                bones = human.get("m_HumanBoneIndex", [])
                out["avatars"].append({"file": name, "name": tree["m_Name"], "isHuman": any(b >= 0 for b in bones), "humanBones": sum(1 for b in bones if b >= 0),
                                       "tos": len(tree.get("m_TOS", []))})
            elif kind == "AnimationClip":
                tree = obj.read_typetree()
                settings = tree.get("m_MuscleClip", {})
                out["clips"][str(obj.path_id) + "@" + name] = {
                    "name": tree["m_Name"], "file": name, "legacy": tree.get("m_Legacy"), "rate": tree.get("m_SampleRate"),
                    "length": round(settings.get("m_StopTime", 0) - settings.get("m_StartTime", 0), 3),
                    "loop": bool(settings.get("m_LoopTime")), "humanoid": tree.get("m_AnimationType") == 3, "type": tree.get("m_AnimationType"),
                    "events": [e.get("functionName") for e in tree.get("m_Events", [])]}
            elif kind == "AnimatorController":
                tree = obj.read_typetree()
                tos = dict((k, v) for k, v in tree.get("m_TOS", []))
                ctrl = tree["m_Controller"]
                values = ctrl["m_Values"]["data"]["m_ValueArray"] if "data" in ctrl["m_Values"] else ctrl["m_Values"]["m_ValueArray"]
                params = [{"name": tos.get(v["m_ID"], "?"), "type": {1: "float", 3: "int", 4: "bool", 9: "trigger"}.get(v["m_Type"], v["m_Type"])} for v in values]
                layers = []
                for layer in ctrl["m_LayerArray"]:
                    l = layer.get("data", layer)
                    layers.append({"name": tos.get(l["m_Binding"], "?"), "blending": l.get("m_LayerBlendingMode"), "ikPass": l.get("m_IKPass"), "weight": l.get("m_DefaultWeight"),
                                   "stateMachine": l.get("m_StateMachineIndex"), "bodyMask": l.get("m_BodyMask") is not None})
                machines = []
                for machine in ctrl["m_StateMachineArray"]:
                    m = machine.get("data", machine)
                    states = []
                    for state in m["m_StateConstantArray"]:
                        s = state.get("data", state)
                        states.append({"name": tos.get(s["m_NameID"], "?"), "speed": s.get("m_Speed"), "loop": s.get("m_Loop"), "ik": s.get("m_IKOnFeet"),
                                       "tree": s.get("m_BlendTreeConstantIndexArray"), "leaf": s.get("m_LeafInfoArray")})
                    machines.append(states)
                clips = [str(c.get("m_PathID")) for c in tree["m_AnimationClips"]]
                out["controllers"].append({"file": name, "name": tree["m_Name"], "params": params, "layers": layers, "machines": machines, "clipRefs": clips,
                                           "externals": [e.path for e in obj.assets_file.externals]})
        except Exception as e:
            print("failed", name, kind, obj.path_id, repr(e)[:200])
    print(name, "done", len(out["controllers"]), "controllers", len(out["clips"]), "clips", len(out["avatars"]), "avatars", flush=True)
with open(out_path, "w") as f:
    json.dump(out, f, indent=1, default=str)
