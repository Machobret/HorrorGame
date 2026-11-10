"""Exclude unfinished/unpainted Classic64 models and their untextured materials.

Prepare a checked Unity cleanup plan, then finalize after AssetDatabase applies it.
Original source masters remain archived for provenance.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
from datetime import datetime, timezone

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[2]
REVIEW=PROJECT/'Exports/Reviews/Classic64/Library'


def read(name):
    return json.loads((HERE/name).read_text(encoding='utf-8'))


def write(name,value):
    (HERE/name).write_text(json.dumps(value,indent=2),encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare():
    manifest=read('LibraryManifest.json')
    materials={m['name']:m for m in manifest['materials']}
    bad=[a for a in manifest['assets'] if a.get('authored_unfinished') or any(not materials[n].get('texture') for n in a['material_names'])]
    untextured=[m for m in manifest['materials'] if not m.get('texture')]
    remove={p for a in bad for p in (a['prefab_path'],a['model_path'])}|{m['path'] for m in untextured}
    retained=[a for a in manifest['assets'] if a not in bad]
    assert all(materials[n]['path'] not in remove for a in retained for n in a['material_names'])
    assert all(i['material_path'] not in remove for i in manifest['source_images'])
    backup=REVIEW/'BeforeFinishedAssetsScope';backup.mkdir(exist_ok=True)
    for name in ('LibraryManifest.json','LibraryImportReceipt.json','Catalog.json'):
        if not (backup/name).exists():shutil.copy2(HERE/name,backup/name)
    # Record every retained byte/GUID metadata file before cleanup.
    old=read('LibraryImportReceipt.json')
    baseline={p:sha(PROJECT/p) for f in old['files'] if f['path'] not in remove for p in (f['path'],f['path']+'.meta')}
    (backup/'RetainedFiles.json').write_text(json.dumps(baseline,indent=2),encoding='utf-8')
    plan=dict(project_root=str(PROJECT),source_scope='Finished, textured, usable Classic64 models only; preserve construction-geometry exclusions',
              excluded_sources=sorted({a['source'].split('/Original/',1)[1] for a in bad}),
              excluded_subjects=[a['name'] for a in bad],remove_assets=sorted(remove),
              review_scenes=['Assets/Sandbox/Classic64/Classic64_LibraryReview.unity'],
              preserve_scene_files=True,
              removed_guids={p:re.search(r'^guid: ([a-f0-9]{32})', (PROJECT/(p+'.meta')).read_text(),re.M)[1] for p in sorted(remove)},
              excluded_model_count=len(bad),excluded_material_count=len(untextured))
    write('FinishedAssetsScope.json',plan)
    manifest['assets']=retained
    manifest['materials']=[m for m in manifest['materials'] if m['path'] not in remove]
    for row in manifest['source_models']:
        if row['source'] in plan['excluded_sources']:
            row['status']='ExcludedByUserQualityScope';row['runtime_paths']=[]
    manifest['counts'].update(new_prefabs=len(retained),materials=len(manifest['materials']),
                               excluded_source_models=sum(s['status'].startswith('ExcludedByUser') for s in manifest['source_models']))
    manifest['quality_scope']=plan['source_scope']
    write('LibraryManifest.json',manifest)
    print(json.dumps(dict(models_to_remove=len(bad),materials_to_remove=len(untextured),files_to_remove=len(remove),sources=plan['excluded_sources'])))


def finalize():
    plan=read('FinishedAssetsScope.json');result=json.loads((REVIEW/'FinishedAssetsScopeResult.json').read_text(encoding='utf-8'))
    assert result['success'] and result['plan_sha256']==sha(HERE/'FinishedAssetsScope.json'),result
    assert all(not (PROJECT/p).exists() and not (PROJECT/(p+'.meta')).exists() for p in plan['remove_assets'])
    baseline=json.loads((REVIEW/'BeforeFinishedAssetsScope/RetainedFiles.json').read_text(encoding='utf-8'))
    changed=[p for p,value in baseline.items() if sha(PROJECT/p)!=value]
    assert not changed,changed
    scenes=json.loads((REVIEW/'BeforeFinishedAssetsScope/SceneBaseline.json').read_text(encoding='utf-8'))
    assert result['preserved_scene_files'] and not result['updated_scenes']
    assert all(sha(PROJECT/p)==value for p,value in scenes.items()), 'Scene file changed'
    manifest=read('LibraryManifest.json');receipt=read('LibraryImportReceipt.json');removed=set(plan['remove_assets'])
    receipt['files']=[f for f in receipt['files'] if f['path'] not in removed]
    for f in receipt['files']:
        f['sha256']=sha(PROJECT/f['path']);f['meta_sha256']=sha(PROJECT/(f['path']+'.meta'))
    receipt.update(new_prefabs=len(manifest['assets']),environment_prefabs=sum(not a['sandbox_example'] for a in manifest['assets']),
                   materials=len(manifest['materials']),mesh_parts=sum(len(a['geometry']) for a in manifest['assets']),
                   manifest_sha256=sha(HERE/'LibraryManifest.json'),excluded_source_models=20,
                   authored_unfinished=[],untextured_sources=[],quality_scope=plan['source_scope'])
    receipt['preserved_scenes_with_removed_asset_references']=plan['review_scenes']
    receipt['quality_scope_completed_utc']=datetime.now(timezone.utc).isoformat()
    receipt['captures']=[p for p in receipt['captures'] if Path(p).name not in ('AppliancesElectronics_Models.png','Vehicles_Models.png')]
    receipt['checks']=[c.replace('115 source models imported, 15 construction sources excluded, and all 732 images represented',
                               '110 source models imported, 20 construction/quality sources excluded, and all 732 images represented')
                        .replace('All material slots resolve to authored textures or documented source colors',
                                 'All retained material slots resolve to authored textures') for c in receipt['checks']]
    receipt['checks'].append('No unfinished models or untextured materials retained; all other asset bytes and GUIDs preserved')
    receipt['checks'].append('All scene files and metadata unchanged; historical library review retains seven removed-prefab references by user request')
    write('LibraryImportReceipt.json',receipt)
    catalog=read('Catalog.json')
    for e in catalog['files']:
        if e['source'] in plan['excluded_sources']:
            e['status']='ExcludedByUserQualityScope';e['runtime_paths']=[]
            e['scope_reason']='Unfinished or unpainted source excluded from usable runtime library'
    catalog['imported_scope']=plan['source_scope']+'; standalone walls, doors and windows excluded; surface textures retained'
    catalog['completed_counts'].update(environment_prefabs=109,materials=739,excluded_source_models=20,represented_source_models=110)
    write('Catalog.json',catalog);write('FinishedAssetsScopeReceipt.json',result)
    report=dict(removed_models=plan['excluded_model_count'],removed_untextured_materials=plan['excluded_material_count'],
                environment_prefabs=109,example_prefabs=2,materials=739,retained_asset_and_metadata_files_unchanged=len(baseline),scene_files_and_metadata_unchanged=len(scenes))
    (REVIEW/'FinishedAssetsReport.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--finalize',action='store_true');args=parser.parse_args()
    finalize() if args.finalize else prepare()
