"""Prepare the six downloaded packs without changing their original sources.

Run with Python from any directory. Unity's DownloadedEnvironmentPackImporter
consumes ImportManifest.json and creates URP materials and visual prefabs.
"""
from pathlib import Path
import hashlib
import json
import shutil
import struct

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]


def fbx_nodes(path):
    data = path.read_bytes()
    if not data.startswith(b"Kaydara FBX Binary"):
        raise ValueError(f"Expected binary FBX: {path}")
    version = struct.unpack_from("<I", data, 23)[0]
    wide = version >= 7500
    header = "<QQQB" if wide else "<IIIB"
    size = struct.calcsize(header)

    def node(pos):
        end, count, _, length = struct.unpack_from(header, data, pos)
        if not end:
            return None, pos + size
        pos += size
        name = data[pos:pos + length].decode("utf8")
        pos += length
        props = []
        for _ in range(count):
            kind = chr(data[pos]); pos += 1
            if kind in "YCFDIL":
                fmt = {"Y": "h", "C": "?", "F": "f", "D": "d", "I": "i", "L": "q"}[kind]
                value = struct.unpack_from("<" + fmt, data, pos)[0]
                pos += struct.calcsize(fmt)
            elif kind in "SR":
                n = struct.unpack_from("<I", data, pos)[0]; pos += 4
                value = data[pos:pos+n]; pos += n
                if kind == "S":
                    value = value.decode("utf8", errors="replace")
            elif kind in "fdlibc":
                _, _, n = struct.unpack_from("<III", data, pos)
                pos += 12 + n
                value = None  # Geometry arrays are not needed for material links.
            else:
                raise ValueError((path, kind, pos))
            props.append(value)
        children = []
        while pos + size <= end:
            child, pos = node(pos)
            if child is None:
                break
            children.append(child)
        return (name, props, children), end

    result = []
    pos = 27
    while pos + size < len(data):
        item, pos = node(pos)
        if item is None:
            break
        result.append(item)
    return result


def text_name(value):
    return value.split("\0", 1)[0].split("::")[-1]


def material_links(model, textures):
    nodes = {n[0]: n for n in fbx_nodes(model)}
    objects = {n[1][0]: n for n in nodes["Objects"][2] if n[1]}
    links = [n[1] for n in nodes.get("Connections", (None, None, []))[2]]
    records = []
    for mid, mat in objects.items():
        if mat[0] != "Material":
            continue
        record = {"name": text_name(mat[1][1]), "base_color": "", "normal": "", "emission": "", "color": [1, 1, 1, 1], "opacity": 1,
                  "has_pbr": False, "metallic": 0, "roughness": .85, "transmission": 0, "cutoff": .5,
                  "emission_strength": 1, "alpha_mode": "", "missing_source_textures": []}
        for child in mat[2]:
            if child[0] == "Properties70":
                for p in child[2]:
                    if p[1][0] == "DiffuseColor":
                        record["color"] = p[1][-3:] + [1]
                    elif p[1][0] == "Opacity":
                        record["opacity"] = p[1][-1]
        for link in links:
            if len(link) < 4 or link[2] != mid or link[1] not in objects:
                continue
            texture = objects[link[1]]
            if texture[0] != "Texture":
                continue
            filenames = [n[1][0] for n in texture[2] if n[0] in ("RelativeFilename", "FileName")]
            resolved = []
            for filename in filenames:
                name = filename.replace("\\", "/").rsplit("/", 1)[-1].lower()
                resolved.extend(textures.get(name, []))
            resolved = sorted(set(resolved))
            channel = link[3].lower()
            role = "normal" if "normal" in channel or "bump" in channel else "emission" if "emiss" in channel else "base_color" if "diffuse" in channel or "basecolor" in channel else None
            if role:
                if len(resolved) > 1:
                    raise ValueError(f"Unresolved/ambiguous {role}: {model} / {record['name']} / {filenames}: {resolved}")
                if resolved:
                    record[role] = resolved[0]
                else:
                    record["missing_source_textures"].append({"role": role, "filenames": list(dict.fromkeys(filenames))})
        if record["base_color"] and not record["normal"]:
            normal_name = Path(record["base_color"]).stem.lower() + "_n.png"
            candidates = textures.get(normal_name, [])
            if len(candidates) == 1:
                record["normal"] = candidates[0]
        record["fbx_diffuse_fallback"] = record["color"][:]
        if record["base_color"]:
            # Blender's FBX diffuse constant is the disconnected socket fallback,
            # not a multiplier for an image connected directly to Base Color.
            record["color"] = [1, 1, 1, 1]
        records.append(record)
    alternate = model.with_suffix(".glb")
    if alternate.exists():
        data = alternate.read_bytes()
        length, kind = struct.unpack_from("<II", data, 12)
        if kind != 0x4E4F534A:
            raise ValueError(f"Missing glTF JSON chunk: {alternate}")
        gltf = json.loads(data[20:20 + length])
        definitions = {m["name"]: m for m in gltf.get("materials", [])}
        for record in records:
            if record["name"] not in definitions:
                continue
            material = definitions[record["name"]]
            pbr = material.get("pbrMetallicRoughness", {})
            extensions = material.get("extensions", {})
            record.update(has_pbr=True, color=pbr.get("baseColorFactor", [1, 1, 1, 1]),
                          opacity=pbr.get("baseColorFactor", [1, 1, 1, 1])[3],
                          metallic=pbr.get("metallicFactor", 1), roughness=pbr.get("roughnessFactor", 1),
                          alpha_mode=material.get("alphaMode", "OPAQUE"), cutoff=material.get("alphaCutoff", .5),
                          transmission=extensions.get("KHR_materials_transmission", {}).get("transmissionFactor", 0),
                          emission_strength=extensions.get("KHR_materials_emissive_strength", {}).get("emissiveStrength", 1))
    return records


def main():
    packs = []
    for info in sorted(SOURCE.glob("*/Source.json")):
        provenance = json.loads(info.read_text())
        original = info.parent / "Original" / provenance["original_root"]
        runtime = PROJECT / provenance["runtime_root"]
        models, texture_records, textures = [], [], {}
        for source in sorted(original.rglob("*")):
            if source.suffix.lower() not in (".fbx", ".png", ".jpg", ".jpeg"):
                continue
            target = runtime / source.relative_to(original)
            target.parent.mkdir(parents=True, exist_ok=True)
            if not target.exists():
                shutil.copy2(source, target)
            elif source.read_bytes() != target.read_bytes():
                raise RuntimeError(f"Refusing to replace changed runtime file: {target}")
            path = target.relative_to(PROJECT).as_posix()
            if source.suffix.lower() == ".fbx":
                models.append((source, path))
            else:
                textures.setdefault(source.name.lower(), []).append(path)
                texture_records.append({"path": path, "normal": source.stem.lower().endswith("_n"), "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
        entries = [{"path": path, "sha256": hashlib.sha256(source.read_bytes()).hexdigest(), "materials": material_links(source, textures)} for source, path in models]
        packs.append({"name": info.parent.name, "root": provenance["runtime_root"], "textures": texture_records, "models": entries})
    manifest = {"packs": packs}
    (SOURCE / "ImportManifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    for pack in packs:
        print(pack["name"], len(pack["models"]), "models", len(pack["textures"]), "textures", sum(len(m["materials"]) for m in pack["models"]), "material slots")


if __name__ == "__main__":
    main()
