"""Prepare selected URBAN sources and promote only verified Unity apartment assets."""
from pathlib import Path
from zipfile import ZipFile
import argparse
import hashlib
import json
import re
import shutil
import struct

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[2]
STAGE=PROJECT/'Exports/Reviews/Classic64/UnityValidation'
REVIEW=PROJECT/'Exports/Reviews/Urban'
FOLDER='Assets/Game/Environment/Buildings/Urban'
SELECTION=[
    ('flat_Lshape','Urban_ApartmentLShapeMixed'),
    ('flat_Lshape_brick','Urban_ApartmentLShapeBrickLow'),
    ('flat_Lshape_large_alt','Urban_ApartmentLShapeBrickTall'),
    ('flat_Lshape_vertical_stripes','Urban_ApartmentLShapeStripedTall'),
    ('flat_square_brick','Urban_ApartmentSquareBrick'),
    ('flat_square_concrete','Urban_ApartmentSquareConcrete'),
]

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8',newline='\n')
def copy(path,target):
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
def guid(path):return re.search(r'^guid: ([a-f0-9]{32})',path.read_text(),re.M)[1]

def prepare(archive):
    REVIEW.mkdir(parents=True,exist_ok=True)
    # Preserve pre-existing scenes and user edits; no scene is part of promotion.
    baseline={p.relative_to(PROJECT).as_posix():sha(p) for scene in (PROJECT/'Assets').rglob('*.unity')
              for p in (scene,Path(str(scene)+'.meta')) if p.is_file()}
    write(REVIEW/'SceneBaseline.json',baseline)
    user_paths=['Assets/Game/Environment/Brick/README.md','Assets/Game/Environment/Brick/Brick_Clean.mat','Assets/Game/Environment/Brick/Brick_Clean_Albedo.png']
    write(REVIEW/'ExistingEditBaseline.json',{p:sha(PROJECT/p) for p in user_paths if (PROJECT/p).is_file()})
    records=[];archive=Path(archive)
    with ZipFile(archive) as z:
        atlas=z.read('URBAN/textures/houses.png')
        for source,name in SELECTION:
            for ext in ('.glb','.fbx'):
                member='URBAN/Buildings Prebuild/'+source+ext
                target=HERE/'Original/Buildings'/(source+ext)
                raw=z.read(member);target.parent.mkdir(parents=True,exist_ok=True)
                if target.exists():assert target.read_bytes()==raw,'Existing source changed: '+str(target)
                else:target.write_bytes(raw)
            data=(HERE/'Original/Buildings'/(source+'.glb')).read_bytes()
            length,_=struct.unpack_from('<II',data,12);doc=json.loads(data[20:20+length])
            offset=20+length;binary_length,_=struct.unpack_from('<II',data,offset)
            binary=data[offset+8:offset+8+binary_length]
            for image in doc['images']:
                view=doc['bufferViews'][image['bufferView']];start=view.get('byteOffset',0)
                assert binary[start:start+view['byteLength']]==atlas,'Apartment uses a different atlas'
            records.append(dict(name=name,source='ArtSource/Environment/Urban/Original/Buildings/'+source+'.glb',
                                source_sha256=sha(HERE/'Original/Buildings'/(source+'.glb')),
                                original_fbx_sha256=sha(HERE/'Original/Buildings'/(source+'.fbx')),
                                model_path=FOLDER+'/'+name+'_Model.fbx',prefab_path=FOLDER+'/'+name+'_Exterior.prefab',
                                expected_triangles=sum(doc['accessors'][p['indices']]['count']//3 for m in doc['meshes'] for p in m['primitives'])))
        textures=HERE/'Original/Textures';textures.mkdir(parents=True,exist_ok=True)
        (textures/'houses.png').write_bytes(atlas)
        docs=[n for n in z.namelist() if any(s in n.lower() for s in ('license','licence','readme','credit'))]
    plan=dict(batch='Urban_ApartmentExteriors',stage_root=str(STAGE),folder=FOLDER,models=records,
              source_storey_pitch_m=4.0,target_storey_pitch_m=2.8,uniform_source_scale=0.7,
              scale_reason='Verified source window rows repeat every 4 m; normalize to the accepted 2.8 m ALEX storey pitch',
              texture_path=FOLDER+'/Urban_Apartments_Albedo.png',material_path=FOLDER+'/Urban_Apartments_Surface.mat',
              atlas_sha256=hashlib.sha256(atlas).hexdigest(),scope='Six complete exterior apartment buildings; no modular building pieces, interiors or scene changes')
    write(HERE/'Selection.json',plan)
    write(HERE/'Provenance.json',dict(source_archive=str(archive),archive_sha256=sha(archive),archive_entries=1011,
          selected_apartment_models=6,available_apartment_models=8,license_status='Unverified: no license or readme in supplied archive',
          license_documents=docs,texture_note='Apartment atlas is houses.png; embedded images match the loose atlas byte for byte. Other pack textures include TexturesCom filenames and are not imported.',
          source_scope=plan['scope']))
    alex=PROJECT/'ArtSource/Environment/AlexBuildingKit/ImportReceipt.json'
    if alex.exists():
        write(REVIEW/'AlexBaseline.json',{p:sha(PROJECT/p) for f in json.loads(alex.read_text())['files'] for p in (f['path'],f['path']+'.meta')})
    copy(HERE/'Selection.json',STAGE/'ArtSource/Environment/Urban/Selection.json')
    copy(HERE/'Original/Textures/houses.png',STAGE/plan['texture_path'])
    for relative in (FOLDER+'/Editor/UrbanApartmentLibraryBuilder.cs',FOLDER+'/README.md',
                     'Assets/Game/Environment/Buildings/Editor/ExteriorBuildingLibraryBuilder.cs'):
        copy(PROJECT/relative,STAGE/relative)
        meta=PROJECT/(relative+'.meta')
        if meta.exists():copy(meta,STAGE/(relative+'.meta'))
    # Seed an existing import before regenerating, retaining native prefab/file GUIDs.
    receipt=HERE/'ImportReceipt.json'
    if receipt.exists():
        for f in json.loads(receipt.read_text())['files']:
            for relative in (f['path'],f['path']+'.meta'):copy(PROJECT/relative,STAGE/relative)
    print(json.dumps(dict(selected=6,stage=str(STAGE),source_archive_mib=round(archive.stat().st_size/1048576,1))))

def promote():
    # Other work may continue in the live editor during isolated validation.
    # Protect the current files during this copy, and report changes since preparation.
    scene_baseline=json.loads((REVIEW/'SceneBaseline.json').read_text())
    edit_baseline=json.loads((REVIEW/'ExistingEditBaseline.json').read_text())
    protected={p:sha(PROJECT/p) if (PROJECT/p).is_file() else None for p in (*scene_baseline,*edit_baseline)}
    concurrent_scenes=[p for p,value in scene_baseline.items() if protected[p]!=value]
    concurrent_edits=[p for p,value in edit_baseline.items() if protected[p]!=value]
    alex_path=REVIEW/'AlexBaseline.json'
    alex=json.loads(alex_path.read_text()) if alex_path.exists() else {}
    assert all(sha(PROJECT/p)==h for p,h in alex.items()),'ALEX runtime assets changed'
    receipt_path=STAGE/'ArtSource/Environment/Urban/ImportReceipt.json'
    receipt=json.loads(receipt_path.read_text());plan=json.loads((HERE/'Selection.json').read_text())
    assert receipt['verified'] and len(receipt['models'])==6 and receipt['selection_sha256']==sha(HERE/'Selection.json')
    assert sha(STAGE/'ArtSource/Environment/Urban/Selection.json')==receipt['selection_sha256']
    assert len(receipt['captures'])==7 and all((STAGE/p).is_file() for p in receipt['captures']),'Missing validated captures'
    owned_guids={f['guid'] for f in receipt['files']}
    shader_meta=next((PROJECT/'Library/PackageCache').glob('com.unity.render-pipelines.universal*/Shaders/Lit.shader.meta'))
    version_meta=shader_meta.parent.parent/'Editor/AssetVersion.cs.meta'
    allowed_guids=owned_guids|{guid(shader_meta),guid(version_meta)}
    for f in receipt['files']:
        p=STAGE/f['path'];meta=Path(str(p)+'.meta')
        assert f['path'].startswith(FOLDER+'/') and p.suffix in ('.fbx','.prefab','.mat','.png')
        assert sha(p)==f['sha256'] and sha(meta)==f['meta_sha256'] and guid(meta)==f['guid'],f['path']
        target=PROJECT/f['path'];target_meta=Path(str(target)+'.meta')
        if target_meta.exists():assert guid(target_meta)==f['guid'],'Refusing to replace an existing GUID'
        for candidate in (p,meta):
            if candidate.suffix not in ('.prefab','.mat','.meta'):continue
            references=re.findall(r'guid: ([a-f0-9]{32})',candidate.read_text(encoding='utf-8',errors='replace'))
            assert all(g in allowed_guids or g.startswith('0000000000000000') for g in references),'Unknown dependency in '+str(candidate)
    # Only copy the explicitly checked assets. Source content and all scenes are excluded.
    for f in receipt['files']:
        for relative in (f['path'],f['path']+'.meta'):copy(STAGE/relative,PROJECT/relative)
    for relative in (FOLDER,FOLDER+'/Editor','Assets/Game/Environment/Buildings'):
        source=STAGE/(relative+'.meta');target=PROJECT/(relative+'.meta')
        if source.exists() and not target.exists():copy(source,target)
    for relative in (FOLDER+'/Editor/UrbanApartmentLibraryBuilder.cs.meta',FOLDER+'/README.md.meta'):
        if not (PROJECT/relative).exists():copy(STAGE/relative,PROJECT/relative)
    copy(receipt_path,HERE/'ImportReceipt.json')
    for relative in receipt['captures']:copy(STAGE/relative,PROJECT/relative)
    assert all((sha(PROJECT/p) if (PROJECT/p).is_file() else None)==value for p,value in protected.items()),'A protected file changed during promotion'
    for f in receipt['files']:
        assert sha(PROJECT/f['path'])==f['sha256'] and sha(PROJECT/(f['path']+'.meta'))==f['meta_sha256']
    assert all(sha(PROJECT/p)==h for p,h in alex.items())
    report=dict(prefabs=6,source_triangles=sum(m['triangles'] for m in receipt['models']),verified_files=len(receipt['files']),
                scene_files_and_metadata_preserved_during_promotion=len(scene_baseline),existing_user_edits_preserved_during_promotion=len(edit_baseline),
                concurrent_scene_changes_since_preparation=concurrent_scenes,concurrent_material_changes_since_preparation=concurrent_edits,
                importer_saved_scene_files=False,materials=1,textures=1,all_dependencies_resolve=True,captures=len(receipt['captures']),
                alex_asset_and_metadata_files_preserved=len(alex),storey_pitch_m=plan.get('target_storey_pitch_m'))
    write(REVIEW/'PromotionCheck.json',report);print(json.dumps(report))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--archive',default=r'C:\Users\manta\Downloads\URBAN.zip')
    parser.add_argument('--promote',action='store_true');args=parser.parse_args()
    promote() if args.promote else prepare(args.archive)
