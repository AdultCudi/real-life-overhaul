# Writes cold-furniture-<build>.json beside this file: what EconomyCheck's food spoilage checks (LogisticsChecks.cs, SpoilCold) need
# of the game's own data. Every fridge and freezer of the game (its type, the goods its shelf shows, its slots, its capacity
# multiplier and whitelist) and the box size and saturation of every product the mod lets spoil, read from the game's Addressables
# bundles of items, and the scripts the fridges' prefabs carry (which controller takes what).
# Reads the game's files only; writes only this one file.
# Usage, after a game update: python -I cold_furniture.py <build> ["<Big Ambitions_Data folder>"]  (needs UnityPy)
import json
import os
import sys

import UnityPy

BUILD = sys.argv[1] if len(sys.argv) > 1 else "3682"
GAME = sys.argv[2] if len(sys.argv) > 2 else r"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data"
AA = os.path.join(GAME, "StreamingAssets", "aa", "StandaloneWindows64")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cold-furniture-" + BUILD + ".json")
GOODS = ("apple", "banana", "grapes", "pear", "carrot", "lettuce", "tomato", "onion", "slicedonions", "russetpotatoes", "cheapflower",
         "expensiveflower", "milk", "butter", "cheese", "cheeseplatter", "croissant", "cupcake", "donut", "dough", "groundbeef",
         "chickenbreast", "rawsausage", "salad", "burger", "hotdog", "pizza", "kabob", "frenchfries", "freshfood", "frozenfood", "icecream")


def bundle(prefix):
    for name in os.listdir(AA):
        if name.startswith(prefix) and name.endswith(".bundle"):
            return UnityPy.load(os.path.join(AA, name))
    raise SystemExit("no bundle " + prefix)


def script_of(component):
    try:
        read = component.read() if hasattr(component, "read") else component.component.read()
        if read.object_reader.type.name == "MonoBehaviour":
            return read.m_Script.read().m_ClassName
    except Exception:
        pass
    return None


furniture, goods = {}, {}
for o in bundle("defaultlocalgroup_assets_items_").objects:
    if o.type.name != "MonoBehaviour":
        continue
    t = o.read_typetree()
    if o.read(check_read=False).m_Script.read().m_ClassName != "Item":
        continue
    name = t["itemName"]
    short = name.split("_", 1)[-1]
    if t["isFurniture"] and ("fridge" in short or "freezer" in short):
        furniture[name] = {"type": t["type"], "slots": t["cargoCapacity"], "mult": round(t["cargoCapacityMultiplier"], 6),
                           "whitelist": sorted(t["itemsWhitelist"]), "shows": sorted(t["itemsThatCanShowcase"]), "controller": ""}
    elif short in GOODS:
        goods[name] = {"box": t["boxSize"], "saturation": t["saturation"]}

for o in bundle("defaultlocalgroup_assets_prefabs_").objects:
    if o.type.name != "GameObject":
        continue
    g = o.read()
    key = "ba:itemname_" + g.m_Name.lower()
    if key in furniture:
        scripts = [s for s in (script_of(c) for c in g.m_Components) if s]
        furniture[key]["controller"] = next((s for s in scripts if s.endswith("Controller")), "")

json.dump({"build": BUILD, "furniture": dict(sorted(furniture.items())), "goods": dict(sorted(goods.items()))},
          open(OUT, "w", encoding="utf-8", newline="\n"), indent=1)
print("wrote", OUT, len(furniture), "cold furniture,", len(goods), "goods")
