"""Build the persistent quality catalog and offline visual review from rendered evidence."""
from pathlib import Path
import collections
import csv
import hashlib
import json
import re
import shutil
import sys

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
REVIEW = PROJECT / "Exports/Reviews/DownloadedEnvironmentPacks/QualityReview"
STAGE = REVIEW.parent / "ImportProject"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def classify(entry, metric, rules, maintained=None, removed=False):
    a = dict(entry)
    a["metrics"] = metric
    flags, reasons = [], []
    name, pack = a["source_object"].lower(), a["pack"]
    ident = a["id"]
    category = "Finished props"
    use_tier = "Placement candidate"

    def flag(key, reason):
        if key not in flags:
            flags.append(key)
        if reason not in reasons:
            reasons.append(reason)

    if a["missing_images"]:
        flag("MissingTextures", "Absent from the supplied archive: " + ", ".join(a["missing_images"]))
    if a["fallback"]:
        flag("FallbackMaterial", "An unassigned source material slot uses the importer's neutral URP fallback.")
    if pack == "ModelsPackPSX":
        flag("ScaleReview", rules["notes"]["scale"])
        if name.startswith(("ar", "plane", "r1", "r2")) and (name.startswith("ar") or ident in {"P0528", "P0532", "P0533"}):
            category = "Vegetation"
            flag("VegetationReview", "Review card/billboard foliage and texture wrapping against the current first-person vegetation style.")
        elif name.startswith("librero"):
            category = "Shop displays"
            if a["triangles"] <= 32:
                flag("ProxyGeometry", rules["notes"]["background"])
                use_tier = "Background only"
        elif name.startswith("auto"):
            category = "Vehicles"
        elif name.startswith(("mesa", "base_c", "colchon", "litera", "almohada")):
            category = "Furniture"
        elif name.startswith(("micro", "olla", "sarten", "licuadora", "cylinder")):
            category = "Household props"
        elif name.startswith("casa") or ident in {"P0220", "P0257", "P0311", "P0393"}:
            category = "Exterior buildings"
    elif pack == "IndustrialExterior":
        category = "Industrial infrastructure"
        if name.startswith("building"):
            category = "Exterior buildings"
        if name.startswith(("wall", "fence", "concrete_miniwall", "road_", "alphalt")):
            flag("ArchitectureReview", "Generic architecture/road surface; hold against the existing House Toolkit and road/fence families.")
            category = "Generic architecture"
    elif pack == "MetroPSX":
        category = "Metro modules"
        if name.startswith(("door", "glass", "frame")):
            flag("ArchitectureReview", "Loose door, window or frame: review as a component rather than a finished placement prop.")
        if name.startswith("poster"):
            flag("SettingReview", "Source poster graphics require a setting/art-direction match; a black back face is not a missing texture.")
            category = "Signs and posters"
    elif pack == "Office":
        category = "Office props"
        if name.startswith("floor"):
            category = "Generic architecture"
            flag("ArchitectureReview", "Generic floor module; compare with the project's authored floor system before use.")
        if name.endswith(("_shelf", "_shelf_fancy")):
            flag("Components", "Detached cabinet drawer. Keep for a cabinet assembly or interaction setup, not as a complete cabinet.")
        if name.startswith("divider"):
            category = "Office furniture"  # Freestanding partitions/cubicles are finished furniture, not house walls.
    elif pack == "Pizzeria":
        category = "Restaurant props"
        if name.startswith("building") or ident in {"P0762", "P0912", "P0913", "P0914"}:
            category = "Exterior buildings"
            reasons.append("Exterior/background building; no usable interior is established by this import.")
        if name.startswith(("road", "door", "glass", "fence", "lines", "metal_tread")):
            category = "Generic architecture"
            flag("ArchitectureReview", "Generic architecture or source road layout; hold against the House Toolkit/current level plan.")
        if name.startswith("tree"):
            category = "Vegetation"
            flag("VegetationReview", "Source billboard/card foliage needs a first-person style and scale check.")
        if name.startswith("decorative_sign"):
            category = "Signs and posters"
            flag("SettingReview", "Retro signs/licence plates have specific locale and period imagery; review against the game's setting.")
    elif pack == "Sewer":
        category = "Sewer modules" if name.startswith(("serwer", "pipe")) else "Sewer clutter"
        if name.startswith("door_metal"):
            flag("ArchitectureReview", "Generic metal door leaf; prefer or adapt the existing door family after review.")

    if ident in rules["architecture_review_ids"]:
        category = "Generic architecture"
        flag("ArchitectureReview", "Visual review: a generic wall/door/window piece, landscape strip, or open-roof building shell; outside the finished-building shortlist.")
    if ident in rules["component_review_ids"]:
        category = "Assembly components"
        flag("Components", "Visual review: loose fixture/furniture component or an assembly whose intended complete form is unclear.")
    if ident in rules["geometry_cleanup_ids"]:
        flag("GeometryCleanup", f"{metric['degenerate_triangles']} of {metric['triangles']} triangles are degenerate. Clean up and recheck collision before promotion.")
    if ident in rules["quality_review_ids"]:
        flag("VisualQualityReview", "Visual review: coarse foliage/debris geometry and texture stretching; judge at the intended viewing distance.")
    if ident in rules["background_proxy_ids"]:
        flag("ProxyGeometry", rules["notes"]["background"])
        category = "Shop displays"
        use_tier = "Background only"
    if ident in rules["exclude_source_layout_ids"]:
        flag("SampleLayout", "Visual review: separated sample instances or layout fragments in one source mesh; split/rebuild before placement.")
        use_tier = "Excluded from placement"
    if metric["invalid_vertices"]:
        flag("InvalidGeometry", "Non-finite mesh coordinates detected.")

    if "SampleLayout" in flags or "InvalidGeometry" in flags:
        status = "Exclude"
    elif any(f in flags for f in ("MissingTextures", "FallbackMaterial", "GeometryCleanup")):
        status = "Repair"
    elif "ProxyGeometry" in flags:
        status = "BackgroundOnly"
    elif flags:
        status = "Review"
    else:
        status = "Keep"
        reasons.append("Visually screened finished prop/building/module within the selected content scope; placement candidate, not gameplay-certified.")
    if maintained is not None or removed:
        a['source_missing_images'] = list(a['missing_images'])
        a['source_had_unassigned_material'] = a['fallback']
        a['source_material_paths'] = list(a['material_paths'])
        flags = [f for f in flags if f not in {'SettingReview', 'VegetationReview'}]
        if removed:
            status, use_tier = 'Removed', 'Removed from prefab library'
            reasons = ['Approved removal of generic building parts, incomplete assemblies or source layout fragments. Original downloads retained in ArtSource.']
            flags = []
            a['missing_images'], a['fallback'], a['material_paths'] = [], False, []
        else:
            a['material_paths'] = maintained['material_paths']
            a['triangles'] = maintained['triangles']
            a['missing_images'], a['fallback'] = [], False
            a['repaired'] = maintained['repaired']
            flags = [f for f in flags if f not in {'MissingTextures', 'FallbackMaterial', 'GeometryCleanup'}]
            reasons = [r for r in reasons if not r.startswith(('Absent from', 'An unassigned', 'Source poster', 'Retro signs', 'Source billboard', 'Review card/', '19 of 84'))]
            reasons = [r for r in reasons if 'triangles are degenerate.' not in r]
            if maintained['repaired']:
                reasons.append('Repaired with intentional replacement surfaces; working source material slots retained. Replacement tiling textures use projected UVs.')
                a['image'] = '../MaintenanceReview/thumbnails/' + ident + '.png'
            status = 'BackgroundOnly' if 'ProxyGeometry' in flags else 'Review' if flags else 'Keep'
    a.update(status=status, category=category, use_tier=use_tier, flags=flags, reasons=reasons,
             display_name=rules["display_names"].get(ident, a["source_object"]),
             labels=["DownloadedPack", "Downloaded" + status] + ["Downloaded" + f for f in flags],
             back_image=(f'../MaintenanceReview/thumbnails/{ident}_back.png' if maintained and maintained['repaired'] else f'thumbnails/{ident}_back.png'))
    return a


def write_csv(path, assets):
    with path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.writer(stream)
        writer.writerow(["ID", "Pack", "Prefab", "Decision", "Use tier", "Category", "Missing source images", "Neutral fallback", "Bounds X/Y/Z metres", "Flags", "Reasons", "GUID"])
        for a in assets:
            writer.writerow([a["id"], a["pack"], a["prefab"], a["status"], a["use_tier"], a["category"], "; ".join(a["missing_images"]), a["fallback"],
                             " / ".join(f"{v:.3f}" for v in a["bounds_m"]), "; ".join(a["flags"]), " | ".join(a["reasons"]), a["guid"]])


def main():
    original = read(REVIEW / "ReviewInput.json")
    rules = read(SOURCE / "quality_rules.json")
    metrics = {a["id"]: a for a in read(REVIEW / "ReviewMetrics.json")["assets"]}
    assert len(metrics) == len(original["assets"]) == 1141
    repaired_metrics = REVIEW.parent / 'MaintenanceReview/ReviewMetrics.json'
    if repaired_metrics.exists():
        metrics.update({a['id']: a for a in read(repaired_metrics)['assets']})
    maintenance_path = SOURCE / 'MaintenanceReceipt.json'
    maintenance = read(maintenance_path) if maintenance_path.exists() else None
    maintained = {a['id']: a for a in maintenance['assets']} if maintenance else {}
    removed = set(maintenance['removed_prefabs']) if maintenance else set()
    assets = [classify(a, metrics[a['id']], rules, maintained.get(a['id']), a['prefab'] in removed) for a in original['assets']]
    counts = dict(collections.Counter(a["status"] for a in assets))
    catalog = {"scope": rules["scope"], "review_method": "All 1,141 prefabs rendered from two opposing angles, all front contact sheets visually screened, ambiguous proxies/parts checked from the reverse angle; source materials, dimensions and mesh metrics audited.",
               "keep_meaning": "A visual placement candidate in the chosen scope; not a full in-game, collision traversal or interaction certification.",
               "counts": counts, "assets": assets}
    (SOURCE / "QualityCatalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (REVIEW / "QualityCatalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_csv(REVIEW / "AllPrefabs.csv", assets)
    affected = [a for a in assets if a["missing_images"] or a["fallback"]]
    write_csv(REVIEW / "MissingTexturesAndFallbacks.csv", affected)
    if '--catalog-only' not in sys.argv:
        baseline = {}
        for a in assets:
            if a['status'] == 'Removed':
                continue
            meta = PROJECT / (a["prefab"] + ".meta")
            baseline[a["prefab"]] = {"sha256": hashlib.sha256(meta.read_bytes()).hexdigest(), "guid": a["guid"]}
            shutil.copy2(meta, STAGE / (a["prefab"] + ".meta"))
        (REVIEW / "LabelBaseline.json").write_text(json.dumps(baseline, indent=2), encoding="utf-8")
    write_catalog = dict(catalog, assets=[a for a in assets if a['status'] != 'Removed'])
    (STAGE / 'QualityCatalog.json').write_text(json.dumps(write_catalog, ensure_ascii=False, indent=2), encoding='utf-8')
    print("Decisions:", counts)
    print("Affected:", len(affected), "texture", sum(bool(a["missing_images"]) for a in assets), "fallback", sum(a["fallback"] for a in assets))
    print("Photo-proxy meshes:", sum("ProxyGeometry" in a["flags"] for a in assets))


if __name__ == "__main__":
    main()
