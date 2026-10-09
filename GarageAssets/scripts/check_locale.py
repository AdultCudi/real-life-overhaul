"""The by-hand locale check for when the CatalogueCheck executable is blocked: every complete "reallife_..."
literal in Scripts is a key of Locales/en.json, the JSON parses, and no key is written twice.

    python -I check_locale.py <repository root>
"""
import glob
import json
import os
import re
import sys

root = sys.argv[1]
mod = os.path.join(root, "Assets", "Mods", "RealLifeOverhaul")
path = os.path.join(mod, "Locales", "en.json")
text = open(path, encoding="utf-8-sig").read()

seen = {}
duplicates = []


def pairs(items):
    for key, _ in items:
        if key in seen:
            duplicates.append(key)
        seen[key] = True
    return dict(items)


data = json.loads(text, object_pairs_hook=pairs)
literal = re.compile(r'"(reallife_[a-z0-9_]+)"')
missing = {}
for file in glob.glob(os.path.join(mod, "Scripts", "**", "*.cs"), recursive=True):
    for key in literal.findall(open(file, encoding="utf-8-sig").read()):
        # a literal that ends in "_" is a prefix something is appended to, not a key
        if not key.endswith("_") and key not in data:
            missing.setdefault(key, os.path.relpath(file, mod))
only = sys.argv[2] if len(sys.argv) > 2 else None
for key, file in sorted(missing.items()):
    if only is None or only in file:
        print("MISSING", key, "(" + file + ")")
for key in duplicates:
    print("DUPLICATE", key)
print(len(data), "keys;", len(missing), "missing;", len(duplicates), "duplicated")
