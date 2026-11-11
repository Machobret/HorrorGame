"""Preserve ALEX kit sources, stage complete building imports, promote checked assets."""
from pathlib import Path
from zipfile import ZipFile
import argparse,hashlib,json,re,shutil,struct

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parents[2]
STAGE=PROJECT/'Exports/Reviews/Classic64/UnityValidation'
REVIEW=PROJECT/'Exports/Reviews/AlexBuildingKit'
FOLDER='Assets/Game/Environment/Buildings/AlexBuildingKit'
SHARED='Assets/Game/Environment/Buildings/Editor/ExteriorBuildingLibraryBuilder.cs'
ATLASES={'brick_MAT':'brick','brick_white_MAT':'brick_white','plaster_MAT':'plaster',
         'plaster_red_MAT':'plaster_red','plaster_orange_MAT':'plaster_orange','roof_MAT':'roof'}

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,indent=2)+'\n',encoding='utf-8',newline='\n')
def copy(a,b):b.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(a,b)
def guid(p):return re.search(r'^guid: ([a-f0-9]{32})',p.read_text(),re.M)[1]

def prepare(archive):
    archive=Path(archive);sources=[]
    with ZipFile(archive) as z:
        for item in z.infolist():
            if item.is_dir():continue
            path=(HERE/'Original'/item.filename).resolve();assert path.is_relative_to((HERE/'Original').resolve())
            raw=z.read(item);path.parent.mkdir(parents=True,exist_ok=True)
            if path.exists():assert path.read_bytes()==raw
            else:path.write_bytes(raw)
            sources.append(dict(path=path.relative_to(PROJECT).as_posix(),sha256=sha(path)))
    raw=(HERE/'Original/modular_building_kit_by_ALEX.glb').read_bytes();length,_=struct.unpack_from('<II',raw,12)
    doc=json.loads(raw[20:20+length]);binary=raw[28+length:];materials=[]
    for material,stem in ATLASES.items():
        image=next(i for i in doc['images'] if i['name']==stem+'_texture_sheet')
        view=doc['bufferViews'][image['bufferView']];offset=view.get('byteOffset',0)
        atlas=HERE/'Original/textures'/(stem+'_texture_sheet.png')
        assert binary[offset:offset+view['byteLength']]==atlas.read_bytes(),'Embedded atlas differs from original'
        texture_path=FOLDER+'/Alex_'+stem+'_Albedo.png'
        materials.append(dict(source_material=material,texture_path=texture_path,path=FOLDER+'/Alex_'+stem+'_Surface.mat',atlas_sha256=sha(atlas)))
        copy(atlas,STAGE/texture_path)
    plan=dict(batch='Alex_AssembledBuildings',stage_root=str(STAGE),folder=FOLDER,materials=materials,models=[],
              source='ArtSource/Environment/AlexBuildingKit/Original/modular_building_kit_by_ALEX.glb',
              source_sha256=sha(HERE/'Original/modular_building_kit_by_ALEX.glb'),uniform_source_scale=1.4,storey_height_m=2.8)
    write(HERE/'Selection.json',plan)
    write(HERE/'Provenance.json',dict(archive=str(archive),archive_sha256=sha(archive),source_files=sources,
          source_meshes=len(doc['nodes']),credit='ALEX (name supplied in archive filename)',
          license_status='Unverified: no license, readme or attribution document in the supplied archive',
          scope='Six assembled exterior buildings only; loose modular pieces remain source-only'))
    scene_paths=[p for scene in (PROJECT/'Assets').rglob('*.unity') for p in (scene,Path(str(scene)+'.meta')) if p.is_file()]
    write(REVIEW/'SceneBaseline.json',{p.relative_to(PROJECT).as_posix():sha(p) for p in scene_paths})
    urban=json.loads((PROJECT/'ArtSource/Environment/Urban/ImportReceipt.json').read_text())
    write(REVIEW/'UrbanBaseline.json',{p:sha(PROJECT/p) for f in urban['files'] for p in (f['path'],f['path']+'.meta')})
    for rel in (SHARED,'Assets/Game/Environment/Buildings/Urban/Editor/UrbanApartmentLibraryBuilder.cs',
                FOLDER+'/Editor/AlexBuildingLibraryBuilder.cs',FOLDER+'/README.md'):
        copy(PROJECT/rel,STAGE/rel)
        if (PROJECT/(rel+'.meta')).exists():copy(PROJECT/(rel+'.meta'),STAGE/(rel+'.meta'))
    if (HERE/'ImportReceipt.json').exists():
        for f in json.loads((HERE/'ImportReceipt.json').read_text())['files']:
            for p in (f['path'],f['path']+'.meta'):copy(PROJECT/p,STAGE/p)
    print(json.dumps(dict(source_modules=len(doc['nodes']),source_atlases=len(materials),stage=str(STAGE))))

def promote():
    receipt_path=STAGE/'ArtSource/Environment/AlexBuildingKit/ImportReceipt.json'
    receipt=json.loads(receipt_path.read_text())
    assert receipt['verified'] and len(receipt['models'])==6 and len(receipt['captures'])==7
    assert receipt['selection_sha256']==sha(HERE/'Selection.json')==sha(STAGE/'ArtSource/Environment/AlexBuildingKit/Selection.json')
    for f in receipt['files']:
        p=STAGE/f['path'];meta=Path(str(p)+'.meta')
        assert f['path'].startswith(FOLDER+'/') and p.suffix in ('.fbx','.prefab','.png','.mat')
        assert sha(p)==f['sha256'] and sha(meta)==f['meta_sha256'] and guid(meta)==f['guid']
        target_meta=PROJECT/(f['path']+'.meta')
        if target_meta.exists():assert guid(target_meta)==f['guid'],'Existing GUID would change'
    protected=json.loads((REVIEW/'SceneBaseline.json').read_text())
    current={p:sha(PROJECT/p) if (PROJECT/p).is_file() else None for p in protected}
    concurrent=[p for p,h in protected.items() if current[p]!=h]
    urban=json.loads((REVIEW/'UrbanBaseline.json').read_text())
    assert all(sha(PROJECT/p)==h for p,h in urban.items()),'Existing URBAN runtime assets changed'
    shader=next((PROJECT/'Library/PackageCache').glob('com.unity.render-pipelines.universal*/Shaders/Lit.shader.meta'))
    allowed={f['guid'] for f in receipt['files']}|{guid(shader),guid(shader.parent.parent/'Editor/AssetVersion.cs.meta')}
    for f in receipt['files']:
        for rel in (f['path'],f['path']+'.meta'):
            p=STAGE/rel
            if p.suffix in ('.prefab','.mat','.meta'):
                refs=re.findall(r'guid: ([a-f0-9]{32})',p.read_text(encoding='utf-8',errors='replace'))
                assert all(g in allowed or g.startswith('0000000000000000') for g in refs),rel
            copy(p,PROJECT/rel)
    for rel in (FOLDER,FOLDER+'/Editor',SHARED,FOLDER+'/README.md',FOLDER+'/Editor/AlexBuildingLibraryBuilder.cs','Assets/Game/Environment/Buildings/Editor'):
        meta=rel+'.meta'
        if (STAGE/meta).exists() and not (PROJECT/meta).exists():copy(STAGE/meta,PROJECT/meta)
    for rel in receipt['captures']:
        assert (STAGE/rel).is_file();copy(STAGE/rel,PROJECT/rel)
    copy(receipt_path,HERE/'ImportReceipt.json')
    assert all((sha(PROJECT/p) if (PROJECT/p).is_file() else None)==h for p,h in current.items()),'A scene changed during promotion'
    assert all(sha(PROJECT/p)==h for p,h in urban.items())
    for f in receipt['files']:
        assert sha(PROJECT/f['path'])==f['sha256'] and sha(PROJECT/(f['path']+'.meta'))==f['meta_sha256']
    report=dict(prefabs=6,files=len(receipt['files']),source_triangles=sum(m['triangles'] for m in receipt['models']),
                materials=6,textures=6,dependencies_verified=True,urban_asset_and_meta_files_preserved=len(urban),
                scene_files_and_metadata_preserved_during_promotion=len(current),concurrent_scene_changes=concurrent,importer_saved_scenes=False)
    write(REVIEW/'PromotionCheck.json',report);print(json.dumps(report))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--archive',default=r'C:\Users\manta\Downloads\modular_building_kit_by_ALEX.zip');p.add_argument('--promote',action='store_true');a=p.parse_args()
    promote() if a.promote else prepare(a.archive)
