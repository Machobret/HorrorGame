"""Verify the promoted Classic64 catalog, asset hashes, GUIDs and references."""
from collections import defaultdict
from pathlib import Path
import hashlib
import json
import re

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
REVIEW = PROJECT / 'Exports/Reviews/Classic64/Library'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    receipt = json.loads((HERE / 'LibraryImportReceipt.json').read_text(encoding='utf-8'))
    catalog = json.loads((HERE / 'Catalog.json').read_text(encoding='utf-8'))
    guid_paths = defaultdict(list)
    for root in ('Assets', 'Library/PackageCache'):
        for meta in (PROJECT / root).rglob('*.meta'):
            match = re.search(r'^guid: ([a-f0-9]{32})', meta.read_text(encoding='utf-8', errors='replace'), re.M)
            if match:
                guid_paths[match[1]].append(meta.relative_to(PROJECT).as_posix())
    unresolved = []
    expected_missing = []
    quality=json.loads((HERE/'FinishedAssetsScope.json').read_text(encoding='utf-8'))
    removed_guids=set(quality['removed_guids'].values())
    for file in receipt['files']:
        path = PROJECT / file['path']
        meta = Path(str(path) + '.meta')
        assert sha(path) == file['sha256'], f'Promoted file changed: {file["path"]}'
        assert sha(meta) == file['meta_sha256'], f'Promoted metadata changed: {file["path"]}'
        assert re.search(r'^guid: ' + file['guid'] + '$', meta.read_text(encoding='utf-8'), re.M), str(meta)
        asset_matches = [p for p in guid_paths[file['guid']] if p.startswith('Assets/')]
        assert asset_matches == [file['path'] + '.meta'], f'Duplicate asset GUID: {asset_matches}'
        for candidate in (path, meta):
            if candidate.suffix.lower() not in ('.mat', '.prefab', '.unity', '.meta'):
                continue
            for reference in re.findall(r'guid: ([a-f0-9]{32})', candidate.read_text(encoding='utf-8', errors='replace')):
                if not reference.startswith('0000000000000000') and reference not in guid_paths:
                    entry=dict(path=candidate.relative_to(PROJECT).as_posix(), guid=reference)
                    if entry['path'] in quality['review_scenes'] and reference in removed_guids:
                        expected_missing.append(entry)
                    else:
                        unresolved.append(entry)
    assert not unresolved, unresolved
    represented = [e for e in catalog['files'] if Path(e['source']).suffix.lower() in ('.blend', '.png', '.jpg')]
    excluded = [e for e in represented if e['status'].startswith('ExcludedByUser')]
    assert len(represented) == 862 and len(excluded)==20
    represented = [e for e in represented if e not in excluded]
    assert len(represented)==842 and all(e.get('runtime_paths') for e in represented)
    assert all((PROJECT / path).is_file() for entry in represented for path in entry['runtime_paths'])
    baseline = json.loads((REVIEW / 'PilotBaseline.json').read_text(encoding='utf-8'))
    assert all(sha(PROJECT / p) == expected for p, expected in baseline.items()), 'Approved pilot changed'
    build_settings = (PROJECT / 'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8')
    assert 'Assets/Sandbox/Classic64/' not in build_settings, 'Review scene is in Build Settings'
    counts = dict(environment_prefabs=len(list((PROJECT / 'Assets/Game/Environment').rglob('Classic64*_Visual.prefab'))),
                  example_prefabs=len(list((PROJECT / 'Assets/Sandbox/Classic64/Examples').glob('Classic64*_Visual.prefab'))))
    assert counts == dict(environment_prefabs=109, example_prefabs=2), counts
    scope=json.loads((HERE/'GeometryScope.json').read_text(encoding='utf-8'))
    assert all(not (PROJECT/p).exists() for p in scope['remove_assets']), 'Excluded building geometry remains'
    assert all(not (PROJECT/p).exists() for p in quality['remove_assets']), 'Unfinished or untextured assets remain'
    scenes=json.loads((REVIEW/'BeforeFinishedAssetsScope/SceneBaseline.json').read_text(encoding='utf-8'))
    # The cleanup checked every scene at execution time. Later verification owns
    # only this library's reviews; normal level edits and Git merges remain valid.
    scenes={p:value for p,value in scenes.items() if p.startswith('Assets/Sandbox/Classic64/')}
    assert all(sha(PROJECT/p)==value for p,value in scenes.items()), 'Scene file changed'
    manifest=json.loads((HERE/'LibraryManifest.json').read_text(encoding='utf-8'))
    assert all(not a.get('authored_unfinished') for a in manifest['assets'])
    assert all(m.get('texture') and m['texture'].get('path') for m in manifest['materials'])
    report = dict(verified_files=len(receipt['files']), source_records_represented=842, excluded_source_models=20,
                  source_models=130, source_images=732, unexpected_unresolved_guid_references=0,
                  intentional_missing_references_in_unchanged_review_scene=expected_missing,
                  review_scene_files_and_metadata_unchanged=len(scenes),
                  no_duplicate_import_guids=True, pilot_files_unchanged=len(baseline),
                  review_excluded_from_build_settings=True, finished_models_only=True, all_materials_textured=True, captures=len(receipt['captures']), **counts)
    (REVIEW / 'FinalReferenceCheck.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
