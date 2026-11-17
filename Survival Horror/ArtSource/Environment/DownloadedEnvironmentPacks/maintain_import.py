"""Prepare and promote the approved prefab curation through the isolated Unity project."""
from pathlib import Path
import collections
import hashlib
import json
import re
import shutil
import sys
import build_prefabs

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
REVIEW = PROJECT / 'Exports/Reviews/DownloadedEnvironmentPacks'
STAGE = REVIEW / 'ImportProject'

def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def write(p, value):
    p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def prepare():
    catalog = read(SOURCE / 'QualityCatalog.json')
    if (SOURCE / 'MaintenancePlan.json').exists():
        raise RuntimeError('Approved plan already exists; reuse it rather than deriving new decisions from a maintained catalog.')
    materials = {}
    prefix = 'Assets/Game/Environment/ModelsPackPSX/'
    def material(role, texture='', color=(1, 1, 1, 1), metallic=0, smoothness=.3):
        path = prefix + 'Materials/' + role + '.mat'
        materials[role] = dict(role=role, path=path, texture=prefix+'Texture/'+texture if texture else '',
                               color=list(color), metallic=metallic, smoothness=smoothness)
        return role
    material('Repair_Walnut', 'Wood_02.jpg', smoothness=.2)
    material('Repair_Plywood', 'Wood_05.jpg', smoothness=.2)
    material('Repair_PillowFabric', 'Fabric_02.jpg', smoothness=.05)
    material('Repair_BrushedSteel', 'Metal_15.jpg', metallic=.8, smoothness=.5)
    material('Repair_ApplianceEnamel', color=(.79,.79,.73,1), smoothness=.4)
    material('Repair_Porcelain', color=(.86,.85,.78,1), smoothness=.65)
    material('Repair_TablewareCeramic', color=(.72,.77,.73,1), smoothness=.45)
    material('Repair_PlasticJug', color=(.65,.69,.56,1), smoothness=.3)
    material('Repair_PlasticChair', color=(.22,.28,.24,1), smoothness=.3)
    material('Repair_Paper', 'Paper.jpg', smoothness=.05)
    material('Repair_AquariumGlass', color=(.57,.76,.75,.16), smoothness=.85)
    mapping = {'Material.007':'Repair_Plywood', 'Material.012':'Repair_PillowFabric',
               'Material.054':'Repair_Walnut', 'Material.130':'Repair_Walnut',
               'Material.180':'Repair_BrushedSteel', 'Material.132':'Repair_ApplianceEnamel',
               'Material.107':'Repair_PlasticJug', 'Material.045':'Repair_Paper'}
    entries=[]
    for a in catalog['assets']:
        remove=bool(set(a['flags']) & {'ArchitectureReview','Components','SampleLayout'})
        repairs=[]
        if not remove:
            for warning in a['source_texture_warnings']:
                role=mapping[warning]
                if a['id']=='P0225': role='Repair_BrushedSteel'
                for p in a['material_paths']:
                    if Path(p).stem.startswith(warning.replace('.','_')+'_'):
                        repairs.append(dict(original=p, replacement=materials[role]['path'], project_uv=bool(materials[role]['texture'])))
            if a['fallback']:
                role = ('Repair_Porcelain' if a['id'] in {'P0183','P0235'} else
                        'Repair_AquariumGlass' if a['id']=='P0527' else
                        'Repair_PlasticChair' if a['id']=='P0531' else 'Repair_TablewareCeramic')
                p=next(p for p in a['material_paths'] if Path(p).stem.startswith('SourceDefault'))
                repairs.append(dict(original=p,replacement=materials[role]['path'],project_uv=False))
            if a['id']=='P0527':
                p=next(p for p in a['material_paths'] if Path(p).stem.startswith('Material_053_'))
                repairs.append(dict(original=p,replacement=materials['Repair_AquariumGlass']['path'],project_uv=False))
        entries.append(dict(id=a['id'],pack=a['pack'],prefab=a['prefab'],guid=a['guid'],remove=remove,
                            expected_triangles=a['triangles'], bounds_m=a['bounds_m'],
                            repairs=repairs,clean_geometry=a['id']=='P0757'))
    removed_paths={a['prefab'] for a in entries if a['remove']}
    original_import=read(SOURCE/'ImportReceipt.json')
    excluded_sources=[a['source_model']+'\n'+a['source_object'] for a in original_import['assets']
                      if a.get('prefab') in removed_paths or a.get('duplicate_of') in removed_paths]
    plan=dict(scope='Keep finished props, complete buildings and sewer/metro modules. Remove generic building parts and incomplete/source-layout assemblies. Keep signs, posters, billboard foliage and debris.',
              excluded_sources=excluded_sources, materials=list(materials.values()),assets=entries)
    write(SOURCE/'MaintenancePlan.json',plan)
    stage()

def stage():
    build_prefabs.stage()
    shutil.copy2(REVIEW/'PromotionBaseline.json', REVIEW/'MaintenanceBaseline.json')
    for name in ['MaintainPrefabs.cs','MaintenancePlan.json','QualityCatalog.json']:
        target=STAGE/('Assets/Editor/'+name if name.endswith('.cs') else name)
        shutil.copy2(SOURCE/name,target)
    print('Maintenance staged. Run DownloadedPrefabMaintenance.RunBatch in the isolated Unity project.')

def promote():
    receipt=read(STAGE/'MaintenanceReceipt.json')
    if not receipt.get('verified'): raise RuntimeError('Maintenance was not verified')
    if not (STAGE/'MaintenanceGeometryVerified.txt').is_file(): raise RuntimeError('Run retained geometry verification first')
    rendered=read(REVIEW/'MaintenanceReview/ReviewMetrics.json')['assets']
    if len(rendered)!=receipt['repaired_count'] or any(a['invalid_vertices'] or a['degenerate_triangles'] or not a['front_pixels'] or not a['back_pixels'] for a in rendered):
        raise RuntimeError('Repaired geometry/render verification failed')
    baseline=read(REVIEW/'MaintenanceBaseline.json')
    plan=read(SOURCE/'MaintenancePlan.json')
    catalog=read(SOURCE/'QualityCatalog.json')
    for a in catalog['assets']:
        if a['status']=='Removed': continue
        metadata=(STAGE/(a['prefab']+'.meta')).read_text(encoding='utf-8-sig')
        if any(not re.search(r'^- '+re.escape(label)+r'$', metadata, re.MULTILINE) for label in a['labels']):
            raise RuntimeError('Native quality labels missing: '+a['prefab'])
    removed={a['prefab'] for a in plan['assets'] if a['remove']}
    removed_guids={a['guid'] for a in plan['assets'] if a['remove']}
    removed_meta={p+'.meta' for p in removed}
    removed_guid_bytes={g.encode() for g in removed_guids}
    # Recheck serialized references immediately before deletion; the audit is independent of Unity's cache.
    references=[]
    extensions={'.prefab','.unity','.asset','.mat','.meta','.controller','.overridecontroller','.anim','.json','.cs'}
    for root in ['Assets','ProjectSettings','Packages']:
        for p in (PROJECT/root).rglob('*'):
            if not p.is_file() or p.suffix.lower() not in extensions: continue
            rel=p.relative_to(PROJECT).as_posix()
            if rel in removed or rel in removed_meta: continue
            data=p.read_bytes()
            found=set(re.findall(rb'guid: ([0-9a-f]{32})',data)) & removed_guid_bytes
            if found: references.append(dict(path=rel,guids=sorted(g.decode() for g in found)))
    if references: raise RuntimeError('Removal would break references: '+str(references))
    roots={str(Path(a['prefab']).parents[1]).replace('\\','/') for a in plan['assets']}
    referenced_guids=set()
    for a in plan['assets']:
        if not a['remove']:
            referenced_guids.update(re.findall(r'guid: ([0-9a-f]{32})',(STAGE/a['prefab']).read_text(encoding='utf-8-sig')))
    pending=[]; deletions=[]
    for rel,previous in baseline.items():
        p=PROJECT/rel
        if not p.exists() or build_prefabs.sha(p)!=previous['sha256']:
            raise RuntimeError('Concurrent project change: '+rel)
        staged=STAGE/rel
        is_removed=rel in removed or rel.removesuffix('.meta') in removed
        if is_removed:
            if staged.exists(): raise RuntimeError('Unity did not remove: '+rel)
            deletions.append(p)
        elif not staged.exists(): raise RuntimeError('Unexpected staged deletion: '+rel)
    for root in roots:
        for p in (STAGE/root).rglob('*'):
            if not p.is_file(): continue
            rel=p.relative_to(STAGE).as_posix();target=PROJECT/rel
            if rel not in baseline:
                asset=p.with_suffix('') if p.suffix=='.meta' else p
                meta=Path(str(asset)+'.meta')
                if asset.parent.name not in {'Geometry','Materials'} or asset.suffix not in {'.asset','.mat'} or not meta.is_file() or build_prefabs.guid(meta) not in referenced_guids:
                    continue  # Never promote unused intermediates left in the cached import project.
            if rel not in baseline and target.exists(): raise RuntimeError('Unexpected target: '+rel)
            if p.suffix=='.meta' and rel in baseline and baseline[rel]['guid'] != build_prefabs.guid(p):
                raise RuntimeError('GUID changed: '+rel)
            if not target.exists() or build_prefabs.sha(p)!=build_prefabs.sha(target): pending.append((p,target))
    # Mirror only assets authored/deleted by AssetDatabase in the isolated project, with the same metadata.
    for source,target in sorted(pending,key=lambda pair:pair[0].suffix!='.meta'): build_prefabs.copy(source,target)
    for p in deletions:
        resolved=p.resolve()
        if not resolved.is_relative_to((PROJECT/'Assets/Game/Environment').resolve()): raise RuntimeError(str(p))
        p.unlink()
    for a in plan['assets']:
        meta=PROJECT/(a['prefab']+'.meta')
        if not a['remove'] and build_prefabs.guid(meta)!=a['guid']: raise RuntimeError('Retained GUID mismatch')
    write(SOURCE/'MaintenanceReceipt.json',receipt)
    write(SOURCE/'QualityLabelReceipt.json',dict(verified=True,prefabs_labelled=receipt['retained_count'],guid_changes=0,
          prefab_data_changes=receipt['repaired_count'],counts=catalog['counts'],originals_preserved=True,
          unity_queries=['t:Prefab l:DownloadedKeep','t:Prefab l:DownloadedReview','t:Prefab l:DownloadedBackgroundOnly']))
    write(REVIEW/'MaintenancePromotion.json',dict(updated_files=len(pending),deleted_files=len(deletions),external_references=references,retained_guids=receipt['retained_count']))
    print('Promoted',len(pending),'files; removed',len(removed),'prefabs; retained',receipt['retained_count'],'prefab GUIDs.')

if __name__=='__main__':
    {'prepare':prepare,'stage':stage,'promote':promote}[sys.argv[1]]()
