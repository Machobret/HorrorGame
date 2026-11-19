"""Stage the current owned prefab inventory or summarize a completed Unity health scan."""

from collections import Counter
import csv
from hashlib import sha256
import json
from pathlib import Path
import re
import shutil
import sys


ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / 'Exports/Reviews/AssetHealth'
STAGE = ROOT / 'Exports/Reviews/PrefabCollision/UnityProject'
CATALOGUE = ROOT / 'Assets/Game/Environment/Editor/AssetLibrary/Catalog.json'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def stage():
    if not (STAGE / 'ProjectSettings/ProjectVersion.txt').is_file():
        raise SystemExit('Prepare the isolated project with prefab_collision.py stage first.')
    if (STAGE / 'Temp/UnityLockfile').exists():
        raise SystemExit('The isolated Unity project is in use.')
    REVIEW.mkdir(parents=True, exist_ok=True)
    prefabs = []
    for folder in ('Assets/Game', 'Assets/Sandbox', 'Assets/Settings', 'Packages/com.manta.house-toolkit'):
        for source in (ROOT / folder).rglob('*'):
            if not source.is_file() or source.name.endswith(('.unity', '.unity.meta')):
                continue
            relative = source.relative_to(ROOT)
            if source.suffix == '.prefab' and folder in ('Assets/Game', 'Assets/Sandbox'):
                prefabs.append(relative.as_posix())
            target = STAGE / relative
            if target.is_file():
                old, current = target.stat(), source.stat()
                if (old.st_size, old.st_mtime_ns) == (current.st_size, current.st_mtime_ns):
                    continue
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    (STAGE / 'AssetHealthScope.json').write_text(json.dumps(dict(prefabs=sorted(prefabs))), encoding='utf-8')
    shutil.copy2(Path(__file__).with_name('PrefabHealthAudit.cs'), STAGE / 'Assets/Editor/PrefabHealthAudit.cs')
    print(f'Staged {len(prefabs):,} current owned prefabs for PrefabHealthAudit.RunBatch.')


def plausibility_check(name, category, size):
    height = size['y']
    span = max(size.values())
    name = name.lower()
    if not span:
        return ''
    # Conservative screening ranges for recognisable subjects. Assemblies and loosely named packs need context.
    if category.startswith('Furniture/'):
        if re.search(r'\bchair\b', name) and 'set' not in name and height > 1.75:
            return 'Chair height above 1.75 m'
        if re.search(r'\bdesk\b|\btable\b', name) and 'set' not in name and (height > 1.6 or span > 4.3):
            return 'Desk or table exceeds 1.6 m high or 4.3 m span'
        if re.search(r'\bcouch\b|\bsofa\b|\bbed\b', name) and (height > 2.15 or span > 4.3):
            return 'Sofa or bed exceeds 2.15 m high or 4.3 m span'
    if re.search(r'\bflashlight\b|\bdesklamp\b|\btable lamp\b', name) and span > 1.1:
        return 'Portable light exceeds 1.1 m'
    if re.search(r'\bmicrowave\b|\btoaster\b|\bblender\b', name) and span > 1.25:
        return 'Countertop appliance exceeds 1.25 m'
    if re.search(r'\bfridge\b|\brefrigerator\b', name) and 'display' not in name and height > 2.65:
        return 'Fridge exceeds 2.65 m'
    if re.search(r'\btrash can\b|\bwaste bin\b|\bwastebasket\b', name) and height > 1.6:
        return 'Waste receptacle exceeds 1.60 m'
    return ''


def summarize():
    report = read(REVIEW / 'UnityAudit.json')
    if not report['verified']:
        raise SystemExit('Unity audit did not complete.')
    entries = {e['guid']: e for e in read(CATALOGUE)['entries']}
    promotion_path = REVIEW / 'FlashlightPromotion.json'
    promotion = read(promotion_path) if promotion_path.exists() else {}
    resolved_prefab = ROOT / promotion.get('prefab', '')
    fixed_guid = (promotion['guid'] if promotion.get('verified') and resolved_prefab.is_file()
                  and sha256(resolved_prefab.read_bytes()).hexdigest() == promotion.get('prefab_sha256') else '')
    exact = Counter()
    inventory = []
    findings = []
    for prefab in report['prefabs']:
        path = prefab['path']
        entry = entries.get(prefab['guid'], {})
        name = entry.get('name', Path(path).stem)
        category = entry.get('category', '')
        size = prefab['size']
        kinds = set()

        def flag(kind, part='', evidence=''):
            status = 'Review'
            if kind == 'Procedural mesh expected':
                status = 'Expected'
            elif kind == 'Mostly opaque surface in transparent pass' and prefab['guid'] == fixed_guid:
                status = 'Fixed'
            else:
                kinds.add(kind)
            findings.append(dict(finding=kind, status=status, prefab=path, name=name, category=category, part=part, evidence=evidence))

        for part in prefab['parts']:
            label = part['name']
            if not part['mesh']:
                kind = 'Procedural mesh expected' if '/FillerBuildings/' in path else 'Missing mesh'
                flag(kind, label, 'MeshFilter has no imported mesh')
                continue
            if part['opposed'] >= 2 and part['opposedAreaFraction'] >= .05:
                flag('Normals oppose face winding', label,
                     f"{part['opposed']} triangles covering {part['opposedAreaFraction']:.1%} of the mesh area")
            if part['inwardClosedComponents']:
                flag('Inward-wound closed component', label,
                     f"{part['inwardClosedComponents']} of {part['closedComponents']} closed components; verify the component is exterior geometry")
            if part['mirrored']:
                flag('Mirrored transform', label, 'Negative world determinant; verify the mirror is intentional')
            if part['missingNormals'] and part['triangles']:
                flag('Missing or zero normals', label, f"{part['missingNormals']} vertices; check shader and geometry role")
            for material in part['materials']:
                material_name = material.get('name', '')
                source = material.get('path', '')
                if material['missing'] or (source.startswith('Assets/') and not (ROOT / source).is_file()):
                    flag('Missing material or shader', label, source or material_name)
                if 'fallback' in material_name.lower() or material_name.startswith(('Default-Material', 'SourceDefault')):
                    flag('Fallback material candidate', label, material_name)
                if not material['transparent'] or material['depthWrite'] or material['alphaSamples'] < 24:
                    continue
                fraction = material['opaqueFraction']
                if fraction >= .95:
                    kind = 'Mostly opaque surface in transparent pass'
                elif fraction >= .68:
                    kind = 'Mixed opacity in transparent pass'
                else:
                    continue
                flag(kind, label, f"{material_name}: {fraction:.1%} near-opaque atlas samples from {material['alphaSamples']} UV probes")
        if entry:
            size_issue = plausibility_check(name, category, size)
            if size_issue:
                flag('Scale review', evidence=size_issue)
        exact.update(kinds)
        inventory.append(dict(prefab=path, name=name, category=category, guid=prefab['guid'],
                              width_m=round(size['x'], 3), height_m=round(size['y'], 3), depth_m=round(size['z'], 3),
                              mesh_parts=len(prefab['parts']), solid_colliders=prefab['solidColliders'],
                              findings='; '.join(sorted(kinds))))

    order = {'Missing mesh': 0, 'Missing material or shader': 1, 'Normals oppose face winding': 2,
             'Mostly opaque surface in transparent pass': 3, 'Mixed opacity in transparent pass': 4,
             'Inward-wound closed component': 5}
    findings.sort(key=lambda row: (row['status'] != 'Review', order.get(row['finding'], 6), row['prefab']))
    for name, rows in [('PrefabHealth.csv', inventory), ('ReviewQueue.csv', findings)]:
        with (REVIEW / name).open('w', encoding='utf-8-sig', newline='') as output:
            fields = list(rows[0]) if rows else ['finding', 'status', 'prefab', 'name', 'category', 'part', 'evidence']
            writer = csv.DictWriter(output, fieldnames=fields)
            writer.writeheader()
            writer.writerows(rows)
    print(json.dumps(dict(prefabs=len(inventory), unique_meshes=report['uniqueMeshes'],
                          unique_triangles=report['uniqueTriangles'], findings=dict(exact)), indent=2))


if __name__ == '__main__':
    {'stage': stage, 'summarize': summarize}[sys.argv[1]]()
