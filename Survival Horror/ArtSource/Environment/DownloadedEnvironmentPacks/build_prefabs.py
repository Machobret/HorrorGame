"""Stage or promote an isolated Unity import without touching the open editor.

python build_prefabs.py stage
Unity.exe -batchmode -nographics -projectPath <printed path> \
  -executeMethod DownloadedEnvironmentPackImporter.RunBatch -logFile <review log>
python build_prefabs.py promote
"""
from pathlib import Path
import hashlib
import json
import re
import shutil
import sys

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
REVIEW = PROJECT / "Exports/Reviews/DownloadedEnvironmentPacks"
STAGE = REVIEW / "ImportProject"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    if path.suffix != ".meta":
        return None
    match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8-sig"), re.MULTILINE)
    return match.group(1) if match else None


def copy(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)


def package_manifest():
    """Resolve an offline dependency closure from this project's package cache."""
    cache = PROJECT / "Library/PackageCache"
    dependencies = {}
    pending = ["com.unity.render-pipelines.universal", "com.unity.modules.physics",
               "com.unity.modules.imageconversion", "com.unity.modules.jsonserialize"]
    while pending:
        name = pending.pop()
        if name in dependencies:
            continue
        if name.startswith("com.unity.modules."):
            dependencies[name] = "1.0.0"
            continue
        matches = list(cache.glob(name + "@*/package.json"))
        if len(matches) != 1:
            raise RuntimeError(f"Expected one installed package for {name}: {matches}")
        path = matches[0]
        package = json.loads(path.read_text(encoding="utf-8-sig"))
        dependencies[name] = "file:" + path.parent.as_posix()
        pending.extend(package.get("dependencies", {}))
    return {"dependencies": dependencies}


def stage():
    manifest = json.loads((SOURCE / "ImportManifest.json").read_text(encoding="utf-8-sig"))
    baseline = {}
    for pack in manifest["packs"]:
        folder = PROJECT / pack["root"]
        files = [folder.with_suffix(".meta"), *sorted(folder.rglob("*"))]
        for path in files:
            if path.is_file():
                relative = path.relative_to(PROJECT)
                baseline[relative.as_posix()] = {"sha256": sha(path), "guid": guid(path)}
                copy(path, STAGE / relative)
    copy(SOURCE / "ImportManifest.json", STAGE / "ImportManifest.json")
    copy(SOURCE / "BuildPrefabs.cs", STAGE / "Assets/Editor/BuildPrefabs.cs")
    if (SOURCE / 'MaintenancePlan.json').exists():
        copy(SOURCE / 'MaintenancePlan.json', STAGE / 'MaintenancePlan.json')
        copy(SOURCE / 'MaintainPrefabs.cs', STAGE / 'Assets/Editor/MaintainPrefabs.cs')
    copy(PROJECT / "ProjectSettings/ProjectVersion.txt", STAGE / "ProjectSettings/ProjectVersion.txt")
    # Resolve URP and its dependencies from the existing local package cache.
    packages = STAGE / "Packages/manifest.json"
    packages.parent.mkdir(parents=True, exist_ok=True)
    packages.write_text(json.dumps(package_manifest(), indent=2) + "\n", encoding="utf-8")
    (REVIEW / "PromotionBaseline.json").write_text(json.dumps(baseline, indent=2) + "\n", encoding="utf-8")
    print(STAGE)
    print("Staged", len(baseline), "existing files, retaining their metadata.")


def promote():
    if (SOURCE / 'MaintenancePlan.json').exists():
        import maintain_import
        return maintain_import.promote()
    receipt_path = STAGE / "ImportReceipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    if not receipt.get("verified") or not receipt.get("prefab_count"):
        raise RuntimeError("Unity import has not passed verification")
    manifest = json.loads((SOURCE / "ImportManifest.json").read_text(encoding="utf-8-sig"))
    baseline = json.loads((REVIEW / "PromotionBaseline.json").read_text(encoding="utf-8-sig"))
    prefabs = {a["prefab"] for a in receipt["assets"] if not a.get("duplicate_of")}
    referenced_guids = set()
    for relative in prefabs:
        referenced_guids.update(re.findall(r"guid: ([0-9a-f]{32})", (STAGE / relative).read_text(encoding="utf-8-sig")))
    for pack in manifest["packs"]:
        for model in pack["models"]:
            referenced_guids.update(re.findall(r"guid: ([0-9a-f]{32})", (STAGE / (model["path"] + ".meta")).read_text(encoding="utf-8-sig")))
    pending = []
    for pack in manifest["packs"]:
        folder = STAGE / pack["root"]
        for path in [folder.with_suffix(".meta"), *sorted(folder.rglob("*"))]:
            if not path.is_file():
                continue
            relative = path.relative_to(STAGE)
            asset = path.with_suffix("") if path.suffix == ".meta" else path
            if asset.suffix == ".prefab" and asset.relative_to(STAGE).as_posix() not in prefabs:
                continue
            if asset.parent.name == "Geometry" and asset.suffix == ".asset" and guid(Path(str(asset) + ".meta")) not in referenced_guids:
                continue  # An unused intermediate from a failed isolated build is not a runtime asset.
            if asset.parent.name == "Materials" and asset.suffix == ".mat" and guid(Path(str(asset) + ".meta")) not in referenced_guids:
                continue
            target = PROJECT / relative
            previous = baseline.get(relative.as_posix())
            if previous:
                if not target.is_file() or sha(target) != previous["sha256"]:
                    raise RuntimeError("Project asset changed during import: " + str(relative))
                if previous["guid"] and guid(path) != previous["guid"]:
                    raise RuntimeError("GUID changed in isolated import: " + str(relative))
            elif target.exists() and sha(target) != sha(path):
                raise RuntimeError("Unexpected existing target: " + str(relative))
            if not target.exists() or sha(target) != sha(path):
                pending.append((path, target))
    # Check all files before making any changes, then promote only the six packs.
    for source, target in pending:
        copy(source, target)
    for relative, previous in baseline.items():
        path = PROJECT / relative
        if previous["guid"] and guid(path) != previous["guid"]:
            raise RuntimeError("GUID verification failed: " + relative)
    copy(receipt_path, SOURCE / "ImportReceipt.json")
    print("Promoted", len(pending), "files; retained", sum(bool(p["guid"]) for p in baseline.values()), "existing GUIDs.")
    print("Prefabs:", receipt["prefab_count"], "duplicates:", receipt["duplicate_count"])


if __name__ == "__main__":
    {"stage": stage, "promote": promote}[sys.argv[1]]()
