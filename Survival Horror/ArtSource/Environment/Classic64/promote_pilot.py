"""Copy a verified isolated pilot into the live project with GUID checks.

Only this pilot's named files and its sandbox scene are promoted. Run after Unity
reports CLASSIC64_PILOT_VERIFIED. Existing user content is never mirrored/deleted.
"""
from pathlib import Path
import hashlib
import json
import re
import shutil

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
STAGE = PROJECT / 'Exports/Reviews/Classic64/UnityValidation'


def guid(path):
    match = re.search(r'^guid: ([a-f0-9]{32})', path.read_text(encoding='utf-8'), re.M)
    if not match:
        raise ValueError(f'Missing GUID in {path}')
    return match[1]


def main():
    receipt_path = Path('ArtSource/Environment/Classic64/PilotImportReceipt.json')
    receipt = json.loads((STAGE / receipt_path).read_text(encoding='utf-8'))
    expected = json.loads((HERE / 'PilotManifest.json').read_text(encoding='utf-8'))
    if not receipt['verified'] or receipt['prefabs'] != len(expected['assets']) or receipt['materials'] != len(expected['materials']):
        raise ValueError('Isolated pilot has not passed verification')
    pending = []
    for item in receipt['files']:
        relative = Path(item['path'])
        allowed = (item['path'].startswith('Assets/Game/Environment/') and relative.name.startswith('Classic64_')) or item['path'].startswith('Assets/Sandbox/Classic64/')
        if not allowed:
            raise ValueError(f'Unexpected promotion path: {relative}')
        source = STAGE / relative
        target = PROJECT / relative
        source_meta = Path(str(source) + '.meta')
        target_meta = Path(str(target) + '.meta')
        if guid(source_meta) != item['guid']:
            raise ValueError(f'Receipt GUID mismatch: {source}')
        if target_meta.exists() and guid(target_meta) != item['guid']:
            raise ValueError(f'Live asset already has a different GUID: {target}')
        pending.append((source, target, source_meta, target_meta))
    for source, target, source_meta, target_meta in pending:
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_meta, target_meta)
        shutil.copy2(source, target)
    folder_meta = 'Assets/Sandbox/Classic64.meta'
    if not (PROJECT / folder_meta).exists():
        shutil.copy2(STAGE / folder_meta, PROJECT / folder_meta)
    for capture in receipt['captures']:
        shutil.copy2(STAGE / capture, PROJECT / capture)
    shutil.copy2(STAGE / receipt_path, PROJECT / receipt_path)
    catalog_path = HERE / 'Catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8'))
    manifest = json.loads((HERE / 'PilotManifest.json').read_text(encoding='utf-8'))
    imported = {}
    for asset in manifest['assets']:
        imported[asset['source'].split('/Original/', 1)[1]] = [asset['prefab_path'], asset['model_path']]
    for material in manifest['materials']:
        source = material['texture']['source'].split('/Original/', 1)[1]
        if '#' not in source:
            imported.setdefault(source, []).extend([material['path'], material['texture']['path']])
    for entry in catalog['files']:
        if entry['source'] in imported:
            entry['status'] = 'PilotImportedAndVerified'
            entry['runtime_paths'] = sorted(set(imported[entry['source']]))
    if not (HERE/'LibraryImportReceipt.json').exists():
        catalog['imported_scope'] = f"{receipt['prefabs']} pilot props and six standalone surfaces verified"
    catalog_path.write_text(json.dumps(catalog, indent=2), encoding='utf-8')
    report = dict(promoted_files=len(pending), preserved_guids=True,
                  source_archive_hashes_verified=all(hashlib.sha256((HERE / 'Original' / e['source']).read_bytes()).hexdigest() == e['sha256'] for e in catalog['files']),
                  captures=receipt['captures'])
    (PROJECT / 'Exports/Reviews/Classic64/PromotionReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
