"""Normalize the imported fence library through Unity, preserving source provenance.

prepare writes a GUID-addressed move plan; Unity applies it with AssetDatabase.
finish updates the manifests and reference captions; verify checks the result.
See ArtSource/Environment/Fences/README.md for the complete command sequence.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = "Assets/EnvironmentAuthoring/Content"
FENCES = CONTENT + "/FenceKits/Downloaded"
MATERIALS = CONTENT + "/Materials/ModelSpecific/Fences"
REPORT = ROOT / "Exports/Reviews/FenceNaming"


def read(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8-sig"))


def write(path, data):
    path = ROOT / path
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bindings(path):
    # Names may change; serialized object references must remain byte-for-byte equal.
    return re.findall(r"\{fileID: -?\d+, guid: [0-9a-f]{32}, type: \d+\}",
                      path.read_text(encoding="utf-8-sig"))


def prepare():
    names = read("ArtSource/Environment/Fences/Names.json")
    manifest = read(FENCES + "/FenceSources.json")
    families = {f["source"]: f for f in names["families"]}
    pieces = {(p["source"], p.get("sourceName", "")): p for p in names["pieces"]}
    materials = {m["id"]: m for m in names["materials"]}
    moves, captions = [], {}

    def move(old, new):
        source = ROOT / old
        if not source.exists():
            raise FileNotFoundError(source)
        guid = re.search(r"^guid: ([0-9a-f]{32})$",
                         Path(str(source) + ".meta").read_text(), re.M).group(1)
        entry = dict(guid=guid, source=old, path=new, folder=source.is_dir())
        if source.is_file():
            entry["sha256"] = digest(source)
            if source.suffix in (".asset", ".prefab", ".mat"):
                entry["bindings"] = bindings(source)
            if source.suffix == ".mat":
                entry["materialSettings"] = source.read_text(encoding="utf-8-sig").split("  m_SavedProperties:", 1)[1]
        moves.append(entry)

    for old, family in families.items():
        new = family["name"]
        previous_folder, current_folder = FENCES + "/" + old, FENCES + "/" + new
        move(previous_folder if (ROOT / previous_folder).exists() else current_folder, current_folder)
        previous_catalog = FENCES + "/Families/" + old + ".asset"
        current_catalog = FENCES + "/Families/" + new + ".asset"
        move(previous_catalog if (ROOT / previous_catalog).exists() else current_catalog, current_catalog)
        captions[old] = new
    for piece in manifest["pieces"] + manifest["derivedPosts"]:
        original_style = piece.get("sourceStyle", piece["style"])
        original_name = piece.get("sourceName", piece["name"])
        key = (piece["source"], original_name if piece in manifest["derivedPosts"] else "")
        new = pieces[key]["name"]
        family = families[original_style]["name"]
        old_directory = FENCES + "/" + piece["style"]
        new_directory = FENCES + "/" + family
        move(FENCES + "/" + piece["fbx"], new_directory + "/Sources/" + new + "_Model.fbx")
        move(old_directory + "/Prefabs/" + piece["name"] + ".prefab",
             new_directory + "/Prefabs/" + new + ".prefab")
        if piece["role"] == "Panel":
            move(old_directory + "/" + piece["name"] + ".asset", new_directory + "/" + new + ".asset")
        captions[original_name] = new
        piece.setdefault("sourceName", original_name)
        piece.setdefault("sourceStyle", original_style)
        piece["name"], piece["style"] = new, family
        piece["fbx"] = family + "/Sources/" + new + "_Model.fbx"

    texture_paths = {}
    for material in manifest["materials"]:
        naming = materials[material["id"]]
        old_material = MATERIALS + "/" + material["materialName"] + ".mat"
        new_material = MATERIALS + "/" + naming["name"] + ".mat"
        move(old_material, new_material)
        captions[material["materialName"]] = naming["name"]
        old_texture = FENCES + "/" + material["texture"]
        new_texture = FENCES + "/Textures/" + naming["textureName"] + Path(old_texture).suffix
        move(old_texture, new_texture)
        texture_paths[material["texture"]] = "Textures/" + Path(new_texture).name
        material["materialName"], material["materialPath"] = naming["name"], new_material
        material["texture"] = texture_paths[material["texture"]]
        material["surface"] = naming["surface"]

    paths = [m["path"].lower() for m in moves]
    assert len(paths) == len(set(paths)), "Naming collision"
    assert len(manifest["pieces"]) == manifest["sourceCount"] == 79
    write(REPORT / "plan.json", {"assets": moves})
    write(REPORT / "manifest.json", manifest)
    write(REPORT / "captions.json", captions)
    write(REPORT / "texture-paths.json", texture_paths)
    print(f"Prepared {len(moves)} GUID-preserving moves. No assets moved.")


def finish():
    plan = read(REPORT / "plan.json")["assets"]
    for entry in plan:
        if not (ROOT / entry["path"]).exists():
            raise FileNotFoundError("Unity has not completed the move: " + entry["path"])
    write(FENCES + "/FenceSources.json", read(REPORT / "manifest.json"))

    construction_path = CONTENT + "/ConstructionMaterials/Imported/ConstructionSources.json"
    text = (ROOT / construction_path).read_text(encoding="utf-8-sig")
    # Repair the imported manifest's pre-integration paths as well as renamed textures.
    replacements = {"Assets/FenceKits/Downloaded/": FENCES + "/",
                    "Assets/TownKits/": CONTENT + "/TownKits/",
                    "Assets/ConstructionMaterials/": CONTENT + "/ConstructionMaterials/",
                    "Assets/Materials/": CONTENT + "/Materials/"}
    for old, new in replacements.items():
        text = text.replace(old, new)
    for old, new in read(REPORT / "texture-paths.json").items():
        text = text.replace(FENCES + "/" + old, FENCES + "/" + new)
    (ROOT / construction_path).write_text(text, encoding="utf-8")

    captions = read(REPORT / "captions.json")
    pattern = re.compile("|".join(re.escape(k) for k in sorted(captions, key=len, reverse=True)))
    changed = []
    for scene in (ROOT / "Assets/EnvironmentAuthoring/Reference").rglob("*.unity"):
        text = scene.read_text(encoding="utf-8-sig")
        # Only visible captions and object-name overrides; don't rewrite source signatures.
        lines = text.splitlines(keepends=True)
        for i, line in enumerate(lines):
            if re.match(r"\s*(m_Name|m_Text):", line) or (
                    re.match(r"\s*value:", line) and i > 0 and "propertyPath: m_Name" in lines[i-1]):
                lines[i] = pattern.sub(lambda match: captions[match[0]], line)
        updated = "".join(lines)
        if updated != text:
            assert re.findall(r"\{fileID:.*\}", updated) == re.findall(r"\{fileID:.*\}", text)
            scene.write_text(updated, encoding="utf-8")
            changed.append(str(scene.relative_to(ROOT)))
    print("Updated native manifest paths and reference captions:", changed)
    verify()


def verify():
    plan = read(REPORT / "plan.json")["assets"]
    for entry in plan:
        path = ROOT / entry["path"]
        meta = Path(str(path) + ".meta").read_text()
        assert "guid: " + entry["guid"] in meta, path
        if entry["folder"]:
            continue
        if path.suffix in (".fbx", ".png", ".jpg"):
            assert digest(path) == entry["sha256"], "Source bytes changed: " + str(path)
        if "bindings" in entry:
            assert bindings(path) == entry["bindings"], "References changed: " + str(path)
        if "materialSettings" in entry:
            settings = path.read_text(encoding="utf-8-sig").split("  m_SavedProperties:", 1)[1]
            assert settings == entry["materialSettings"], "Material settings changed: " + str(path)
    manifest = read(FENCES + "/FenceSources.json")
    for p in manifest["pieces"] + manifest["derivedPosts"]:
        assert (ROOT / FENCES / p["fbx"]).exists(), p["name"]
    for m in manifest["materials"]:
        assert (ROOT / m["materialPath"]).exists(), m["materialName"]
        assert (ROOT / FENCES / m["texture"]).exists(), m["texture"]
    construction = json.dumps(read(CONTENT + "/ConstructionMaterials/Imported/ConstructionSources.json"))
    for value in re.findall(r'Assets/[^"\\]+', construction):
        assert (ROOT / value).exists(), "Stale construction path: " + value
    print(f"PASS: {len(plan)} GUIDs, all serialized bindings, 80 FBX files, 13 textures and manifest paths.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("prepare", "finish", "verify"))
    globals()[parser.parse_args().action]()
