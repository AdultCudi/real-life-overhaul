# The game's own shaders by name, for the check of what the mod's asset bundles may bring
# (docs/standards/platforms.md, RLO-PLT-004; StandardCheck reads the list it writes). Read only: it opens the game's
# data files and its Addressables bundles with UnityPy and writes nothing into the game's folder.
#
# A shader is compiled for the graphics APIs of the system the game was built for, so each line also says which the
# installed game has: Direct3D 11 and Vulkan in the Windows game. The names are the same in the game's build for
# another system, which is made from the same project; only the APIs differ.
#
#   python -I game_shaders.py                                   the installed game against the list beside StandardCheck
#   python -I game_shaders.py --write                           the same, and the list written again
#   python -I game_shaders.py --game "<Big Ambitions_Data>"
#
# Needs Python 3.10 or later and UnityPy (pip install UnityPy). Exit code 0 when the list is as the game is.
import argparse
import os
import sys

import UnityPy

DEFAULT_GAME = r"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data"
HERE = os.path.dirname(os.path.abspath(__file__))
LIST = os.path.normpath(os.path.join(HERE, "..", "StandardCheck", "game-shaders.txt"))
APIS = {4: "Direct3D11", 9: "OpenGLES3", 14: "Metal", 15: "OpenGLCore", 18: "Vulkan"}   # the engine's shader compiler platforms
LEFT_OUT = (".resS", ".resource", ".json", ".info", ".config")


def files(data):
    """The game's serialized files, then its Addressables bundles."""
    for name in sorted(os.listdir(data)):
        path = os.path.join(data, name)
        if os.path.isfile(path) and not name.endswith(LEFT_OUT):
            yield path
    for root, _, names in os.walk(os.path.join(data, "StreamingAssets", "aa")):
        for name in sorted(names):
            if name.endswith(".bundle"):
                yield os.path.join(root, name)


def read(data):
    """Every shader's name with the graphics APIs it is compiled for, and the game's build."""
    found = {}
    for path in files(data):
        try:
            env = UnityPy.load(path)
        except Exception as e:  # a file UnityPy cannot open holds no shader the mod could ask for
            print("  left out:", os.path.relpath(path, data), type(e).__name__)
            continue
        for obj in env.objects:
            if obj.type.name != "Shader":
                continue
            tree = obj.read_typetree()
            name = tree.get("m_ParsedForm", {}).get("m_Name")
            if not name:
                continue
            apis = found.setdefault(name, set())
            for platform in tree.get("platforms", []):
                apis.add(APIS.get(platform, str(platform)))
    return found


def build_of():
    """The game's build the mod is made for (Core/GameBuild.cs, which ContractCheck holds to the installed game), or '?'."""
    try:
        source = open(os.path.join(HERE, "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Core", "GameBuild.cs"), encoding="utf-8-sig").read()
        return source.split("public const int MadeFor = ")[1].split(";")[0].strip()
    except (OSError, IndexError):
        return "?"


def text(found, build):
    lines = [
        "# The game's own shaders by name, with the graphics APIs the installed game has each compiled for.",
        "# Written by RealLifeOverhaul.Build/Census/game_shaders.py --write from the installed Windows game (Build " + build + "): every Shader object of",
        "# its data files and its Addressables bundles. A bundle of the mod's may bring a shader only if its name is here, since the mod then",
        "# draws with the game's own of that name on every system (RLO-PLT-004). The names are the same in the game's build for another",
        "# system; the APIs are that build's own. One shader a line: its name, a tab, its APIs.",
    ]
    for name in sorted(found):
        lines.append(name + "\t" + ", ".join(sorted(found[name])))
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--game", default=DEFAULT_GAME)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    found = read(args.game)
    now = text(found, build_of())
    if args.write:
        with open(LIST, "w", encoding="utf-8", newline="\n") as f:
            f.write(now)
        print("SHADERS: " + str(len(found)) + " written to " + LIST)
        return 0
    try:
        was = open(LIST, encoding="utf-8").read()
    except OSError:
        was = ""
    names_was = {line.split("\t")[0] for line in was.splitlines() if line and not line.startswith("#")}
    new, gone = sorted(set(found) - names_was), sorted(names_was - set(found))
    for name in new:
        print("  new in the game:", name)
    for name in gone:
        print("  gone from the game:", name)
    print("SHADERS: as the list has them (" + str(len(found)) + ")" if not new and not gone else "SHADERS: the list is not as the game is: run with --write")
    return 0 if not new and not gone else 1


if __name__ == "__main__":
    sys.exit(main())
