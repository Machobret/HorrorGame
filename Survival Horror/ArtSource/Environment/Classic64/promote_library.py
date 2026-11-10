"""Promote the verified full Classic64 library into the real Unity project.

Checks receipt hashes/GUIDs, preserves the pilot, and reconciles all source files.
Only explicitly owned Classic64 assets are copied. No mirroring or deletion.
"""
from pathlib import Path
import hashlib
import json
import re
import shutil

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
STAGE = PROJECT / 'Exports/Reviews/Classic64/UnityValidation'
REVIEW = PROJECT / 'Exports/Reviews/Classic64/Library'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid(path):
    match = re.search(r'^guid: ([a-f0-9]{32})', path.read_text(encoding='utf-8'), re.M)
    if not match:
        raise ValueError(f'Missing GUID: {path}')
    return match[1]


def check_pilot():
    baseline = json.loads((REVIEW / 'PilotBaseline.json').read_text(encoding='utf-8'))
    changed = [p for p, expected in baseline.items() if not (PROJECT / p).is_file() or sha(PROJECT / p) != expected]
    if changed:
        raise ValueError(f'Pilot differs from pre-expansion baseline: {changed}')
    return len(baseline)


def main():
    receipt_relative = Path('ArtSource/Environment/Classic64/LibraryImportReceipt.json')
    receipt = json.loads((STAGE / receipt_relative).read_text(encoding='utf-8'))
    manifest_path = HERE / 'LibraryManifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if not receipt['verified'] or receipt['source_models'] != 130 or receipt['source_images'] != 732:
        raise ValueError('Full-library Unity verification has not passed')
    if sha(manifest_path) != receipt['manifest_sha256'] or sha(STAGE / 'ArtSource/Environment/Classic64/LibraryManifest.json') != receipt['manifest_sha256']:
        raise ValueError('Manifest changed after verification')
    preserved = check_pilot()
    pending = []
    families = {p.name for p in (PROJECT / 'Assets/Game/Environment').iterdir() if p.is_dir()}
    for file in receipt['files']:
        path = Path(file['path'])
        source = STAGE / path
        source_meta = Path(str(source) + '.meta')
        if sha(source) != file['sha256'] or sha(source_meta) != file['meta_sha256'] or guid(source_meta) != file['guid']:
            raise ValueError(f'Verified staged file changed: {path}')
        target = PROJECT / path
        target_meta = Path(str(target) + '.meta')
        if file['preserve_existing']:
            if sha(target) != file['sha256'] or guid(target_meta) != file['guid']:
                raise ValueError(f'Preserved pilot reference differs: {path}')
            continue
        runtime = file['path'].startswith('Assets/Game/Environment/') and path.parts[3] in families and path.name.startswith('Classic64_')
        sandbox = file['path'].startswith('Assets/Sandbox/Classic64/') and path.name.startswith('Classic64_')
        if not runtime and not sandbox:
            raise ValueError(f'Unexpected import destination: {path}')
        if target_meta.exists() and guid(target_meta) != file['guid']:
            raise ValueError(f'Existing asset has another GUID; refusing overwrite: {path}')
        pending.append((source, target, source_meta, target_meta))
    # Copy only new folder metadata; retain existing project folder identities.
    for meta in (STAGE / 'Assets/Sandbox/Classic64').rglob('*.meta'):
        asset = Path(str(meta)[:-5])
        if asset.is_dir():
            relative = meta.relative_to(STAGE)
            target = PROJECT / relative
            if not target.exists():
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(meta, target)
    for source, target, source_meta, target_meta in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_meta, target_meta)
        shutil.copy2(source, target)
    for capture in receipt['captures']:
        target = PROJECT / capture
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(STAGE / capture, target)
    shutil.copy2(STAGE / receipt_relative, PROJECT / receipt_relative)
    preserved_after = check_pilot()
    paths_by_source = {}
    for asset in manifest['assets'] + json.loads((HERE/'PilotManifest.json').read_text(encoding='utf-8'))['assets']:
        key = asset['source'].split('/Original/', 1)[1]
        paths_by_source.setdefault(key, []).extend([asset['model_path'], asset['prefab_path']])
    for image in manifest['source_images']:
        paths_by_source.setdefault(image['source'], []).extend([image['texture_path'], image['material_path']])
    notes_by_source = {}
    for note in manifest['notes']:
        key = note['source'].split('/Original/', 1)[1]
        notes_by_source.setdefault(key, []).append(note['note'])
    catalog_path = HERE / 'Catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    for entry in catalog['files']:
        key = entry['source']
        excluded_source=next((s for s in manifest['source_models'] if s['source']==key and s['status'].startswith('ExcludedByUser')),None)
        if excluded_source:
            entry['status']=excluded_source['status']
            entry['runtime_paths']=[]
            continue
        if key in paths_by_source:
            entry['runtime_paths'] = sorted(set(paths_by_source[key]))
            entry['status'] = 'ImportedAndVerified'
            families = sorted({p.split('/')[3] for p in entry['runtime_paths'] if p.startswith('Assets/Game/Environment/')})
            entry['runtime_families'] = families or ['SandboxExamples']
            if len(families) == 1:
                entry['proposed_family'] = families[0]
        if key in notes_by_source:
            entry['source_appearance_notes'] = sorted(set(notes_by_source[key]))
        if 'unfinished' in key.lower():
            entry['authored_unfinished'] = True
    unrepresented = [e['source'] for e in catalog['files'] if Path(e['source']).suffix.lower() in ('.blend', '.png', '.jpg') and not e.get('runtime_paths') and not e['status'].startswith('ExcludedByUser')]
    if unrepresented:
        raise ValueError(f'Catalog sources still unrepresented: {unrepresented}')
    originals_match = all(sha(HERE / 'Original' / e['source']) == e['sha256'] for e in catalog['files'])
    if not originals_match:
        raise ValueError('An archived original changed')
    catalog['imported_scope'] = 'Finished, textured buildings and props; standalone construction and unfinished/unpainted models excluded; all source images represented'
    catalog['category_mapping_is_provisional'] = False
    catalog['completed_counts'] = dict(source_blend_files=130, source_images=732,
                                        environment_prefabs=receipt['environment_prefabs'] + receipt['existing_pilot_prefabs'],
                                        excluded_source_models=sum(s['status'].startswith('ExcludedByUser') for s in manifest['source_models']),
                                        example_prefabs=receipt['example_prefabs'],
                                        materials=receipt['materials'], textures=receipt['textures'])
    catalog_path.write_text(json.dumps(catalog, indent=2), encoding='utf-8')
    report = dict(promoted_files=len(pending), verified_files=len(receipt['files']),
                  pilot_files_unchanged=preserved_after == preserved,
                  source_archive_hashes_verified=originals_match, unrepresented_sources=unrepresented,
                  counts=catalog['completed_counts'], captures=receipt['captures'])
    (REVIEW / 'PromotionReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
