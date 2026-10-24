# Writes shop-goods-<build>.json beside this file: what EconomyCheck's ShopChecks need of the game's own data that is not
# in its StreamingAssets folder as plain files. Read from the game's Addressables bundles: every item that is sold or
# shows goods (its type, prices, demand and the goods it shows) and the business types the new kinds of shop trade as
# (Scripts/Shops). The wholesale stores' goods are read by the checks from the game's own layout files.
# Reads the game's files only; writes only this one file.
# Usage, after a game update: python -I shop_goods.py <build> ["<Big Ambitions_Data folder>"]  (needs UnityPy)
import json
import os
import sys

import UnityPy

BUILD = sys.argv[1] if len(sys.argv) > 1 else "3682"
GAME = sys.argv[2] if len(sys.argv) > 2 else r"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data"
AA = os.path.join(GAME, "StreamingAssets", "aa", "StandaloneWindows64")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "shop-goods-" + BUILD + ".json")
DONORS = ("ba:businesstype_coffeeshop", "ba:businesstype_fastfoodrestaurant", "ba:businesstype_liquorstore")


def bundle(prefix):
    for name in os.listdir(AA):
        if name.startswith(prefix) and name.endswith(".bundle"):
            return UnityPy.load(os.path.join(AA, name))
    raise SystemExit("no bundle " + prefix)


def behaviours(env):
    for o in env.objects:
        if o.type.name == "MonoBehaviour":
            tree = o.read_typetree()
            yield o.read(check_read=False).m_Script.read().m_ClassName, o.path_id, tree


items = {}
for cls, _, t in behaviours(bundle("defaultlocalgroup_assets_items_")):
    if cls != "Item":
        continue
    sold = t["type"] & 6 != 0
    shows = len(t["itemsThatCanShowcase"]) > 0 or t["type"] & 0x48 != 0
    if sold or shows:
        items[t["itemName"]] = {
            "type": t["type"], "wholesale": round(t["wholesalePrice"], 4), "market": round(t["defaultMarketPrice"], 4),
            "ratio": round(t["productSalesRatio"], 4), "demanded": bool(t["isADemandedProduct"]), "rivalOnly": bool(t["isSpecialRivalOnly"]),
            "shows": sorted(t["itemsThatCanShowcase"]),
        }

sims, reqs, types = {}, {}, {}
env = bundle("defaultlocalgroup_assets_businesstypes_")
for cls, pid, t in behaviours(env):
    if cls.endswith("Simulator"):
        sims[pid] = cls
for cls, pid, t in behaviours(bundle("defaultlocalgroup_assets_businessrequirements_")):
    reqs[pid] = t["m_Name"]
for cls, pid, t in behaviours(env):
    if cls == "BusinessType" and t["businessTypeName"] in DONORS:
        types[t["businessTypeName"]] = {
            "simulator": sims.get(t["simulator"]["m_PathID"], ""), "customerType": t["customerType"], "course": t["courseRequired"],
            "building": t["suitableBuildingType"], "sources": list(t["productSources"]), "requirements": [reqs.get(r["m_PathID"], "?") for r in t["businessRequirements"]],
            "tags": sorted(t["tags"]), "products": {p["itemName"]: round(p["impact"], 4) for p in t["businessProducts"]},
        }

json.dump({"build": BUILD, "items": dict(sorted(items.items())), "types": types},
          open(OUT, "w", encoding="utf-8", newline="\n"), indent=1, sort_keys=False)
print("wrote", OUT, len(items), "items,", len(types), "types")
