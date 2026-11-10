"""Prepare/finalize the user's geometry-only Classic64 construction exclusion."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
REVIEW = PROJECT / 'Exports/Reviews/Classic64/Library'
PREFIXES = ('Doors/', 'Windows/', 'Walls/')


def read(name):
    return json.loads((HERE / name).read_text(encoding='utf-8'))


def write(name, value):
    (HERE / name).write_text(json.dumps(value, indent=2), encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def excluded(asset):
    return asset['source'].split('/Original/', 1)[1].startswith(PREFIXES)


def prepare():
    if (HERE/'FinishedAssetsScope.json').exists():
        raise RuntimeError('Geometry scope is already applied; use the current finished-asset manifest instead of restoring the earlier selection')
    backup = REVIEW / 'BeforeGeometryExclusion'
    backup.mkdir(exist_ok=True)
    names = ('LibraryManifest.json', 'LibraryImportReceipt.json', 'PilotManifest.json', 'PilotSelection.json', 'PilotImportReceipt.json', 'Catalog.json')
    for name in names:
        if not (backup / name).exists():
            shutil.copy2(HERE / name, backup / name)
    if not (backup / 'PilotBaseline.json').exists():
        shutil.copy2(REVIEW / 'PilotBaseline.json', backup / 'PilotBaseline.json')
    library = json.loads((backup / 'LibraryManifest.json').read_text(encoding='utf-8'))
    pilot = json.loads((backup / 'PilotManifest.json').read_text(encoding='utf-8'))
    removed = [a for a in library['assets'] + pilot['assets'] if excluded(a)]
    sources = sorted({a['source'].split('/Original/', 1)[1] for a in removed})
    plan = dict(project_root=str(PROJECT), source_scope='Complete buildings and props; exclude standalone doors, windows and wall/structural pieces; retain surface textures',
                excluded_sources=sources, excluded_subjects=[a['name'] for a in removed],
                remove_assets=[p for a in removed for p in (a['prefab_path'], a['model_path'])],
                review_scenes=['Assets/Sandbox/Classic64/Classic64_PilotReview.unity', 'Assets/Sandbox/Classic64/Classic64_LibraryReview.unity'])
    write('GeometryScope.json', plan)
    library['assets'] = [a for a in library['assets'] if not excluded(a)]
    for source in library['source_models']:
        if source['source'] in sources:
            source['status'] = 'ExcludedByUserGeometryScope'
            source['runtime_paths'] = []
    library['counts']['new_prefabs'] = len(library['assets'])
    library['counts']['existing_pilot_prefabs'] = 11
    library['counts']['excluded_source_models'] = len(sources)
    library['geometry_scope'] = plan['source_scope']
    write('LibraryManifest.json', library)
    pilot['assets'] = [a for a in pilot['assets'] if not excluded(a)]
    pilot['geometry_scope'] = plan['source_scope']
    write('PilotManifest.json', pilot)
    selection = read('PilotSelection.json')
    selection['models'] = [a for a in selection['models'] if not a['source'].startswith(PREFIXES)]
    write('PilotSelection.json', selection)
    print(json.dumps(dict(remove_prefabs=len(removed), remove_files=len(plan['remove_assets']), excluded_source_models=len(sources))))


def finalize():
    plan = read('GeometryScope.json')
    result = json.loads((REVIEW / 'GeometryScopeResult.json').read_text(encoding='utf-8'))
    assert result['success'], result
    assert result['plan_sha256'] == sha(HERE / 'GeometryScope.json'), 'Cleanup plan changed'
    assert all(not (PROJECT / p).exists() and not Path(str(PROJECT / p) + '.meta').exists() for p in plan['remove_assets'])
    removed = set(plan['remove_assets'])
    backup_baseline=REVIEW / 'BeforeGeometryExclusion/PilotBaseline.json'
    if not backup_baseline.exists():
        shutil.copy2(REVIEW / 'PilotBaseline.json', backup_baseline)
    before = json.loads((REVIEW / 'BeforeGeometryExclusion/PilotBaseline.json').read_text(encoding='utf-8'))
    intentionally_changed = removed | {p+'.meta' for p in removed} | set(plan['review_scenes'])
    assert all(sha(PROJECT/p)==expected for p,expected in before.items() if p.replace('\\','/') not in intentionally_changed), 'Unrelated pilot asset changed'
    pilot = read('PilotImportReceipt.json')
    pilot['files'] = [f for f in pilot['files'] if f['path'] not in removed]
    pilot['prefabs'] = 11
    pilot['mesh_parts'] = sum(len(a['geometry']) for a in read('PilotManifest.json')['assets'])
    pilot['geometry_scope'] = plan['source_scope']
    pilot['captures_note'] = 'Original pilot captures predate geometry exclusion; current review scene removes the door.'
    write('PilotImportReceipt.json', pilot)
    library = read('LibraryManifest.json')
    receipt = read('LibraryImportReceipt.json')
    receipt['files'] = [f for f in receipt['files'] if f['path'] not in removed]
    for f in receipt['files']:
        f['sha256'] = sha(PROJECT / f['path'])
        f['meta_sha256'] = sha(PROJECT / (f['path'] + '.meta'))
    receipt.update(new_prefabs=len(library['assets']), environment_prefabs=sum(not a['sandbox_example'] for a in library['assets']),
                   existing_pilot_prefabs=11, mesh_parts=sum(len(a['geometry']) for a in library['assets']),
                   manifest_sha256=sha(HERE / 'LibraryManifest.json'), excluded_source_models=len(plan['excluded_sources']),
                   geometry_scope=plan['source_scope'])
    receipt['captures'] = [p for p in receipt['captures'] if Path(p).name not in ('Windows_Models.png', 'MasonrySurfaces_Models.png', 'InteriorFixtures_Models.png')]
    receipt['checks'] = [c.replace('All 130 source Blender files and 732 source images represented', '115 source models imported, 15 construction sources excluded, and all 732 images represented') for c in receipt['checks']]
    receipt['checks'].append('Standalone doors, windows and wall geometry excluded; retained source models verified')
    write('LibraryImportReceipt.json', receipt)
    catalog = read('Catalog.json')
    for entry in catalog['files']:
        if entry['source'] in plan['excluded_sources']:
            entry['status'] = 'ExcludedByUserGeometryScope'
            entry['runtime_paths'] = []
            entry['scope_reason'] = 'Standalone construction geometry excluded; original source retained in ArtSource'
    catalog['imported_scope'] = plan['source_scope']
    catalog['completed_counts'].update(environment_prefabs=116, excluded_source_models=15, represented_source_models=115)
    write('Catalog.json', catalog)
    write('GeometryScopeReceipt.json', result)
    baseline = {str(p.relative_to(PROJECT)):sha(p) for f in pilot['files'] for p in (PROJECT/f['path'],PROJECT/(f['path']+'.meta'))}
    (REVIEW / 'PilotBaseline.json').write_text(json.dumps(baseline,indent=2),encoding='utf-8')
    (PROJECT / 'Exports/Reviews/Classic64/PilotGeometryScope.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(dict(environment_prefabs=116, sandbox_examples=2, excluded_source_models=15, removed_geometry_prefabs=18)))


if __name__ == '__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--finalize',action='store_true');args=parser.parse_args()
    finalize() if args.finalize else prepare()
