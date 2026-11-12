# The census of the game's own interface objects, for the steps after a game update. Read only: it opens the game's
# scene and shared asset files with UnityPy and writes nothing into the game's folder.
#
# The RLO interface reads the game's live panels and presses the game's own buttons. Two facts about how the game builds
# those panels cannot be read from its code, only from its files, and each was got wrong once (1.0.0 to 1.0.2):
#   1. Where the rows of a list are. The purchase panel's rows are copies of its templates beside the templates, inside a
#      scroll view; the object the panel switches on and off for the whole list (itemsContainer) is the scroll view, two
#      levels up. A screen that looked among that object's children found no row (CheckoutRules, CheckoutScreen.Read).
#   2. A button on an object that a UI.HoverEffect switches on only while the mouse is over the game's own row. The kit
#      hides the game's rows from the mouse, so that object never comes on: such a button is pressed through
#      UiWidgets.PressUnder, never UiWidgets.Press (an order's add button: DeliveryItemEntry.hoverOutlineButton).
# This script reads both from the installed game and compares them with what the mod knows (KNOWN below). After a game
# update: run it, and where it reports something new, read the kit screen that covers that panel before raising MadeFor.
#
#   python -I game_ui_census.py [--game "<Big Ambitions_Data>"]
#
# Needs Python 3.10 or later and UnityPy (pip install UnityPy). Exit code 0 when nothing the kit relies on changed.
import argparse
import os
import struct
import sys

import UnityPy

DEFAULT_GAME = r"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data"

# Every hover-switched object of the game that holds something to press, as "<host object>/<switched object>", with the
# way the kit presses it.
KNOWN_HOVER_HELD = {
    "DeliveryItemTemplate/HoverOutline": "UiWidgets.PressUnder in Scripts/Ui/World/ItemsListKit.cs",
}
# The purchase panel: the templates' parent, and the list object above it, by name.
PURCHASE_ROWS_PARENT = "Content"
PURCHASE_LIST = "Items"
PRESSABLE = {"Button", "Toggle", "TMP_InputField", "TMP_Dropdown", "Slider", "Dropdown", "InputField"}


def files(game):
    for name in sorted(os.listdir(game)):
        if name.endswith(".assets") or (name.startswith("level") and "." not in name):
            yield name


class Asset:
    def __init__(self, game, name, scripts):
        self.name = name
        self.asset = UnityPy.load(os.path.join(game, name)).assets[0]
        self.scripts = scripts
        self.externals = [e.name if hasattr(e, "name") else str(e) for e in getattr(self.asset, "externals", [])]

    def get(self, pid):
        return self.asset.objects.get(pid)

    def transform(self, go):
        for c in go.m_Components:
            comp = c.component if hasattr(c, "component") else c
            o = self.get(comp.path_id)
            if o is not None and o.type.name in ("Transform", "RectTransform"):
                return o.read()
        return None

    def parent(self, go_id):
        o = self.get(go_id)
        if o is None or o.type.name != "GameObject":
            return None
        t = self.transform(o.read())
        if t is None or t.m_Father.path_id == 0:
            return None
        father = self.get(t.m_Father.path_id)
        return father.read().m_GameObject.path_id if father is not None else None

    def name_of(self, go_id):
        o = self.get(go_id)
        return o.read().m_Name if o is not None and o.type.name == "GameObject" else "?"

    def script(self, obj):
        """A script's class name (the game's and the engine's interface scripts are both named in globalgamemanagers)."""
        raw = obj.get_raw_data()
        if len(raw) < 28:
            return None, raw
        file_id, sid = struct.unpack_from("<iq", raw, 16)
        where = self.externals[file_id - 1] if 0 < file_id <= len(self.externals) else ""
        return (self.scripts.get(sid) if "globalgamemanagers" in where else None), raw

    def scripts_under(self, go_id, depth=0):
        found = []
        o = self.get(go_id)
        if o is None or o.type.name != "GameObject" or depth > 4:
            return found
        go = o.read()
        for c in go.m_Components:
            comp = c.component if hasattr(c, "component") else c
            m = self.get(comp.path_id)
            if m is not None and m.type.name == "MonoBehaviour":
                cls, _ = self.script(m)
                if cls:
                    found.append(cls)
        t = self.transform(go)
        for child in (t.m_Children if t is not None else []):
            ct = self.get(child.path_id)
            if ct is not None:
                found += self.scripts_under(ct.read().m_GameObject.path_id, depth + 1)
        return found

    def references(self, raw):
        """The objects of this file a script refers to, in its fields' order (a reference by its shape: file 0, an id of the file)."""
        at = 28
        (n,) = struct.unpack_from("<i", raw, at)
        at += 4 + ((n + 3) & ~3)
        while at + 12 <= len(raw):
            file_id, pid = struct.unpack_from("<iq", raw, at)
            if file_id == 0 and pid != 0 and pid in self.asset.objects:
                yield pid
                at += 12
            else:
                at += 4


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--game", default=DEFAULT_GAME)
    args = ap.parse_args()
    if not os.path.isdir(args.game):
        sys.exit("The game's data folder is not at " + args.game + " (give --game)")
    genv = UnityPy.load(os.path.join(args.game, "globalgamemanagers.assets"))
    scripts = {o.path_id: o.read().m_ClassName for o in genv.objects if o.type.name == "MonoScript"}
    news, hover, purchase = [], {}, None
    for name in files(args.game):
        a = Asset(args.game, name, scripts)
        for obj in list(a.asset.objects.values()):
            if obj.type.name != "MonoBehaviour":
                continue
            cls, raw = a.script(obj)
            if cls == "HoverEffect":
                host = struct.unpack_from("<q", raw, 4)[0]
                refs = [p for p in a.references(raw) if a.get(p).type.name == "GameObject"]
                held = set(a.scripts_under(refs[0])) & PRESSABLE if refs else set()
                if held:
                    hover[a.name_of(host) + "/" + a.name_of(refs[0])] = (name, sorted(held))
            elif cls == "PurchaseUI":
                by_name = {}
                for p in a.references(raw):
                    o = a.get(p)
                    if o.type.name in ("Transform", "RectTransform"):
                        g = o.read().m_GameObject.path_id
                        by_name[a.name_of(g)] = g
                item, tax, shown = by_name.get("ItemTemplate"), by_name.get("TaxTemplate"), by_name.get(PURCHASE_LIST)
                purchase = (name, a.name_of(a.parent(item)) if item else None, a.name_of(a.parent(tax)) if tax else None, shown is not None,
                            item is not None and shown is not None and a.parent(item) == shown)
    for key, (where, held) in sorted(hover.items()):
        if key not in KNOWN_HOVER_HELD:
            news.append("NEW: %s in %s holds %s on an object only the mouse switches on: a kit screen must press it through UiWidgets.PressUnder" % (key, where, ", ".join(held)))
    for key in KNOWN_HOVER_HELD:
        if key not in hover:
            news.append("GONE: %s no longer holds a button under a hover effect (%s may go back to UiWidgets.Press)" % (key, KNOWN_HOVER_HELD[key]))
    if purchase is None:
        news.append("GONE: no PurchaseUI found in the game's files")
    else:
        where, item_parent, tax_parent, has_list, direct = purchase
        if item_parent != PURCHASE_ROWS_PARENT or tax_parent != PURCHASE_ROWS_PARENT or not has_list or direct:
            news.append("CHANGED: the purchase panel in %s: goods rows beside their template under '%s', tax rows under '%s', the list object '%s' %s%s"
                        % (where, item_parent, tax_parent, PURCHASE_LIST, "found" if has_list else "not found", ", which is now the rows' own parent" if direct else ""))
    print("hover-held buttons: %d (%s)" % (len(hover), ", ".join(sorted(hover)) or "none"))
    if purchase is not None:
        print("purchase panel (%s): goods rows under '%s', tax rows under '%s'" % (purchase[0], purchase[1], purchase[2]))
    for line in news:
        print(line)
    print("UI CENSUS: as the mod knows it." if not news else "UI CENSUS: %d difference(s): read the kit screens named above." % len(news))
    sys.exit(1 if news else 0)


main()
