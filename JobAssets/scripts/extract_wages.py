"""Reads what the vanilla game pays, so the mod's own jobs can be balanced against it: every employer job
the player can take (SpecialService.playerJob), the base hourly wage of every employee skill (SkillData),
and the delivery van / food delivery job settings (DeliveryJobStartLocation, FoodDeliveryJobConfig,
DeliveryJobTipsConfig).

LOCAL REFERENCE ONLY: reads the installed game, writes one JSON; nothing from it is committed or shipped.

Run with Blender's Python (any Python 3 that can import the UnityPy in the pylib folder works):
    python -I extract_wages.py <Big Ambitions_Data folder> <out.json> <pylib folder with UnityPy>

How it reads (each entry says which in "readVia"):
  "embedded"  the Addressables bundles under StreamingAssets/aa carry their own typetrees, so UnityPy's
              read_typetree() reads them as is.
  "borrowed"  the sharedassets*.assets files hold more copies of the same classes without typetrees; those
              are read with the typetree of the same class taken from a bundle (same build, same layout).
  "handmade"  FoodDeliveryJobConfig exists only in sharedassets1.assets, so its layout is written out below
              from the decompiled field order. Address (its DLL is not decompiled) is inferred from the bytes
              as {string, int}; the names given to those two fields are a guess, nothing else is.
Borrowed and handmade reads fail unless the layout consumes the object's bytes exactly; a failure lands in
"unreadable" instead of being guessed. UnityPy's TypeTreeGenerator is not used (its backend is not in pylib).
Identical copies of one asset in several files are merged into one entry listing every file.

Formulas the numbers plug into (decompiled game code, not read by this script):
    employer job pay   = floor(hours worked inside the shift * hourlySalary)      PlayerActivity/WorkActivity.cs
    employee wage      = baseHourlyWage * (skill^1.05 / 100 + 1) * difficulty     Helpers/EmployeeHelper.cs
    delivery van job   = deliveryReward per completed destination + a tip roll    Vehicles.DeliveryDriverJob
    food delivery      = round(baseReward + metres * rewardPerMeter) + a tip roll Player.FoodDeliveryJob
"""
import glob
import json
import os
import sys

data_dir, out_path, pylib = sys.argv[1], sys.argv[2], sys.argv[3]
sys.path.insert(0, pylib)
import UnityPy  # noqa: E402

WANTED = ("SpecialService", "SkillData", "DeliveryJobStartLocation", "FoodDeliveryJobConfig", "DeliveryJobTipsConfig")
DAYS = {1: "Mon", 2: "Tue", 3: "Wed", 4: "Thu", 5: "Fri", 6: "Sat", 7: "Sun"}          # DayOfWeekOrdered
DIPLOMAS = {0: "Undefined", 1: "FoodSafetyCourse", 2: "AutoMechanicEducation", 3: "BasicHr", 4: "MarketDemands",
            5: "Headquarters", 6: "OfficeBusinesses", 7: "ProductManufacturing"}       # DiplomaName

# Bundles first: their typetrees are what the plain files are read with.
bundles = sorted(glob.glob(os.path.join(data_dir, "StreamingAssets", "aa", "*", "*.bundle")))
plain = [os.path.join(data_dir, f) for f in sorted(os.listdir(data_dir))
         if f in ("resources.assets", "globalgamemanagers.assets") or (f.startswith("sharedassets") and f.endswith(".assets"))]

found = {name: [] for name in WANTED}      # class -> [(file, path id, typetree dict, how it was read)]
unreadable = []                            # wanted objects that could not be read
by_id = {}                                 # (file, path id) -> typetree, to follow references inside one file
layouts = {}                               # class -> typetree taken from a bundle, for the files without one


def n(level, kind, name, flag=0):
    return {"m_Level": level, "m_Type": kind, "m_Name": name, "m_MetaFlag": flag}


def text(level, name):
    return [n(level, "string", name, 0x8000), n(level + 1, "Array", "Array", 0x4001), n(level + 2, "int", "size"), n(level + 2, "char", "data")]


def many(level, name, item):
    """item: the nodes of one element, written at level + 2 and named "data"."""
    return [n(level, "vector", name, 0x8000), n(level + 1, "Array", "Array", 0xC000), n(level + 2, "int", "size")] + item


def pointer(level, name):
    return [n(level, "PPtr<Object>", name), n(level + 1, "int", "m_FileID"), n(level + 1, "SInt64", "m_PathID")]


def group(level, kind, name, fields, field_kind):
    return [n(level, kind, name)] + [n(level + 1, field_kind, f) for f in fields]


ADDRESS = [n(3, "Address", "data"), *text(4, "streetName"), n(4, "int", "number")]      # inferred, see the docstring
HANDMADE = {"FoodDeliveryJobConfig": [
    n(0, "MonoBehaviour", "Base"), *pointer(1, "m_GameObject"), n(1, "UInt8", "m_Enabled", 0x4000), *pointer(1, "m_Script"), *text(1, "m_Name"),
    *many(1, "sourceBusinessTypes", text(3, "data")), *many(1, "destinationBuildingTypes", text(3, "data")),
    n(1, "float", "destinationRadius"), *many(1, "excludedPickupAddresses", ADDRESS), *many(1, "excludedDeliveryAddresses", ADDRESS),
    n(1, "int", "newOffersPerHour"), n(1, "int", "maxActiveOffers"),
    *group(1, "Vector2Int", "offerActiveMinutes", "xy", "int"), *group(1, "Vector2Int", "distinctItemsPerOrder", "xy", "int"),
    n(1, "int", "maxAmountPerItem"), n(1, "float", "baseReward"), n(1, "float", "rewardPerMeter"), *pointer(1, "tipsConfig"),
    n(1, "int", "baseTimeMinutes"), n(1, "float", "minutesPerMeter"), *group(1, "Vector2Int", "timeLimitMinutes", "xy", "int"),
    *group(1, "Vector3f", "backpackLocalPosition", "xyz", "float"), *group(1, "Vector3f", "backpackLocalRotation", "xyz", "float"),
    *pointer(1, "mapIcon"), *group(1, "ColorRGBA", "poiColor", "rgba", "float")]}


def script_name(obj):
    try:
        return obj.read(check_read=False).m_Script.read().m_ClassName
    except Exception:
        return None


for path in bundles + plain:
    name = os.path.basename(path)
    try:
        env = UnityPy.load(path)
    except Exception as e:
        print("skip", name, e)
        continue
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        kind = script_name(obj)
        if kind not in found:
            continue
        try:
            if obj.serialized_type and obj.serialized_type.node:
                tree, how = obj.read_typetree(), "embedded"
                layouts.setdefault(kind, obj.serialized_type.node)
            elif kind in layouts:
                tree, how = obj.read_typetree(nodes=layouts[kind]), "borrowed"
            elif kind in HANDMADE:
                tree, how = obj.read_typetree(nodes=HANDMADE[kind]), "handmade"
            else:
                unreadable.append({"class": kind, "file": name, "pathId": obj.path_id, "why": "no typetree in the file and none to borrow"})
                continue
        except Exception as e:
            unreadable.append({"class": kind, "file": name, "pathId": obj.path_id, "why": repr(e)})
            continue
        found[kind].append((name, obj.path_id, tree, how))
        by_id[(name, obj.path_id)] = tree


def merged(entries, link=None):
    """Identical copies (same values, different file) become one entry with "files" and "readVia" lists.
    link: a field holding a reference that only resolves inside its own file; it is left out of the
    comparison and the resolved value (a dict) is kept over the unresolved one (a string)."""
    out = {}
    for entry in entries:
        file, how = entry.pop("file"), entry.pop("readVia")
        kept = out.setdefault(json.dumps({k: v for k, v in entry.items() if k != link}, sort_keys=True), dict(entry, files=[], readVia=[]))
        if link and isinstance(entry.get(link), dict):
            kept[link] = entry[link]
        kept["files"].append(file)
        if how not in kept["readVia"]:
            kept["readVia"].append(how)
    return list(out.values())


def schedule(days, slots_key):
    """{'Mon': ['9-17'], ...} from a List<ScheduleDay>, using workShifts or openingHourSlots. Days with
    isOpen false are left out: for a job that is a day off (WorkActivity.HasDayOff, JobOffer)."""
    out = {}
    for day in days or []:
        if not day.get("isOpen"):
            continue
        slots = ["%d-%d" % (s["startingHour"], s["endingHour"]) for s in day.get(slots_key, [])]
        if slots:
            out[DAYS.get(day["day"], str(day["day"]))] = slots
    return out


def hours(days):
    return {day: sum(int(b) - int(a) for a, b in (s.split("-") for s in slots)) for day, slots in days.items()}


def tips(tree):
    return {"name": tree["m_Name"], "fastDeliveryTimeRatio": tree["fastDeliveryTimeRatio"],
            "fastDeliveryChanceUp": tree["fastDeliveryChanceUp"],
            "tipChances": [{"chance": t["chance"], "tip": t["tip"]} for t in tree["tipChances"]]}


def tips_of(file, ref):
    if not ref or not ref.get("m_PathID"):
        return None
    tree = None if ref.get("m_FileID") else by_id.get((file, ref["m_PathID"]))
    return tips(tree) if tree else "in another file; one of tipsConfigs"


services = []
for file, path_id, tree, how in found["SpecialService"]:
    job = tree.get("playerJob") or {}
    shifts = schedule(job.get("scheduleDays"), "workShifts")
    services.append({
        "asset": tree["m_Name"], "file": file, "readVia": how, "businessName": tree["businessName"], "businessTypeName": tree["businessTypeName"],
        "openingHours": schedule(tree.get("scheduleDays"), "openingHourSlots"),
        "playerJob": {
            "employerName": job.get("employerName"), "hourlySalary": job.get("hourlySalary"), "localizeKey": job.get("localizeKey"),
            "requiredDiplomas": [DIPLOMAS.get(d, str(d)) for d in job.get("requiredDiplomas", [])],
            "minDaysBeforeRehireAfterFiring": job.get("minDaysBeforeRehireAfterFiring"),
            "shifts": shifts, "daysOff": [DAYS.get(d["day"], str(d["day"])) for d in job.get("scheduleDays") or [] if not d.get("isOpen")],
            "hoursPerDay": hours(shifts), "hoursPerWeek": sum(hours(shifts).values()),
            "payPerWeek": int(sum(int(h * (job.get("hourlySalary") or 0)) for h in hours(shifts).values()))}})
services = merged(services)
services.sort(key=lambda s: (-(s["playerJob"]["hourlySalary"] or 0), s["businessName"]))

skills = sorted(merged({"file": f, "readVia": how, "skillName": t["skillName"], "asset": t["m_Name"], "baseHourlyWage": t["baseHourlyWage"],
                        "trainingCostMultiplier": t["trainingCostMultiplier"], "secondarySkill": t["secondarySkill"]}
                       for f, _, t, how in found["SkillData"]), key=lambda s: s["skillName"])

locations = merged([{"asset": t["m_Name"], "file": f, "readVia": how, "vehicleTypeName": t["vehicleTypeName"], "deliveryReward": t["deliveryReward"],
                    "destinationsCount": [t["destinationsCountMin"], t["destinationsCountMax"]],
                    "boxesPerDestination": [t["minBoxesPerDestination"], t["maxBoxesPerDestination"]],
                    "radius": t["radius"], "minutesPerMeter": t["minutesPerMeter"], "minutes": [t["minutesMin"], t["minutesMax"]],
                    "destinationBuildingTypes": t["destinationBuildingTypes"], "tipsConfig": tips_of(f, t.get("tipsConfig"))}
                   for f, _, t, how in found["DeliveryJobStartLocation"]], link="tipsConfig")

food = merged([{"asset": t["m_Name"], "file": f, "readVia": how, "baseReward": t["baseReward"], "rewardPerMeter": t["rewardPerMeter"],
               "destinationRadius": t["destinationRadius"], "newOffersPerHour": t["newOffersPerHour"], "maxActiveOffers": t["maxActiveOffers"],
               "offerActiveMinutes": [t["offerActiveMinutes"]["x"], t["offerActiveMinutes"]["y"]],
               "distinctItemsPerOrder": [t["distinctItemsPerOrder"]["x"], t["distinctItemsPerOrder"]["y"]], "maxAmountPerItem": t["maxAmountPerItem"],
               "baseTimeMinutes": t["baseTimeMinutes"], "minutesPerMeter": t["minutesPerMeter"],
               "timeLimitMinutes": [t["timeLimitMinutes"]["x"], t["timeLimitMinutes"]["y"]],
               "sourceBusinessTypes": t["sourceBusinessTypes"], "destinationBuildingTypes": t["destinationBuildingTypes"],
               "excludedPickupAddresses": t["excludedPickupAddresses"], "excludedDeliveryAddresses": t["excludedDeliveryAddresses"],
               "tipsConfig": tips_of(f, t.get("tipsConfig"))}
              for f, _, t, how in found["FoodDeliveryJobConfig"]], link="tipsConfig")

out = {
    "source": {"dataDir": data_dir, "bundlesScanned": len(bundles), "plainFilesScanned": len(plain),
               "method": "UnityPy read_typetree; readVia = embedded | borrowed | handmade typetree, see the docstring of extract_wages.py"},
    "objectsRead": {k: len(v) for k, v in found.items()},
    "playerJobs": [s for s in services if (s["playerJob"]["hourlySalary"] or 0) > 0],
    "specialServicesWithoutPaidJob": [{"asset": s["asset"], "businessName": s["businessName"], "businessTypeName": s["businessTypeName"], "files": s["files"]}
                                      for s in services if not (s["playerJob"]["hourlySalary"] or 0) > 0],
    "skills": skills,
    "deliveryJobStartLocations": locations,
    "foodDeliveryJobConfigs": food,
    "tipsConfigs": merged(dict(tips(t), file=f, readVia=how) for f, _, t, how in found["DeliveryJobTipsConfig"]),
    "unreadable": unreadable,
}
os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(out, f, indent=1, ensure_ascii=False)

print("%d player jobs with pay, %d skills, %d delivery start locations, %d food delivery configs, %d unreadable -> %s"
      % (len(out["playerJobs"]), len(skills), len(locations), len(food), len(unreadable), out_path))
for s in out["playerJobs"]:
    j = s["playerJob"]
    print("  %-34s %-32s $%-6g/h %3dh/week  %s" % (s["businessName"], s["businessTypeName"], j["hourlySalary"], j["hoursPerWeek"], ",".join(j["requiredDiplomas"])))
for s in skills:
    print("  %-34s $%g/h" % (s["skillName"], s["baseHourlyWage"]))
