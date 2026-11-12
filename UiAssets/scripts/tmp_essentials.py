"""TextMeshPro's essential resources (its shaders and settings) unpacked into the Unity project, with their own GUIDs.

    python -I UiAssets/scripts/tmp_essentials.py <UnityProject>

Unity's AssetDatabase.ImportPackage returns before the import is done in batch mode, so a build that quits after it
finds nothing. A .unitypackage is a gzip tar of folders named by GUID, each holding the asset's path, the asset and its
.meta; written straight into Assets, Unity imports them as its own on the next start. The package comes from the
project's package cache (Library/PackageCache), which Unity fills on its first start. Nothing here touches the game.
"""
import os
import sys
import tarfile

PACKAGE = os.path.join("Library", "PackageCache", "com.unity.textmeshpro@3.2.0-pre.10", "Package Resources", "TMP Essential Resources.unitypackage")


def main():
    project = os.path.abspath(sys.argv[1])
    source = os.path.join(project, PACKAGE)
    if not os.path.exists(source):
        raise SystemExit("TMP: no essential resources package at " + source + " (start Unity on the project once)")
    entries = {}
    with tarfile.open(source, "r:gz") as tar:
        for member in tar.getmembers():
            parts = member.name.replace("\\", "/").strip("./").split("/")
            if len(parts) != 2 or not member.isfile():
                continue
            guid, kind = parts
            entries.setdefault(guid, {})[kind] = tar.extractfile(member).read()
    written = 0
    for guid, files in entries.items():
        if "pathname" not in files:
            continue
        rel = files["pathname"].decode("utf-8").splitlines()[0].strip()
        if not rel.startswith("Assets/") or ".." in rel.split("/"):
            raise SystemExit("TMP: an entry would land outside Assets: " + rel)
        target = os.path.join(project, *rel.split("/"))
        if "asset" in files:
            os.makedirs(os.path.dirname(target), exist_ok=True)
            with open(target, "wb") as f:
                f.write(files["asset"])
            written += 1
        else:
            os.makedirs(target, exist_ok=True)
        if "asset.meta" in files:
            with open(target + ".meta", "wb") as f:
                f.write(files["asset.meta"])
    print("TMP: %d essential files unpacked into %s" % (written, os.path.join(project, "Assets", "TextMesh Pro")))


if __name__ == "__main__":
    main()
