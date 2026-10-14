"""Update the test rack without resetting tuned weapons or requiring the open editor to close."""
from pathlib import Path
import re, json, uuid
root=Path(__file__).resolve().parents[2]
prefabs=root/'Assets/Game/Weapons/Prefabs'
measurements=Path('C:/Users/Mangis/AppData/Local/Temp/WeaponVisualValidation')
def guid(path): return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]
def meta(path):
    p=Path(str(path)+'.meta')
    if not p.exists(): p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nPrefabImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
template=(prefabs/'HuntingRifle_Pickup.prefab').read_text()
rifle_guid=guid(root/'Assets/Game/Weapons/Models/HuntingRifle/HuntingRifle.fbx')
for source,name,scale,material in [('Gun','Pistol',.01,'7916ee5fdc758f144b96ea3224defc55'),('Shotgun','Shotgun',.19875449,'91812d3d947bb724d90e4ceb46ca399c')]:
    data=root/('Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/'+source+'/'+source+'.asset')
    model=data.with_suffix('.fbx')
    measured=json.loads((measurements/('measure_'+source+'.json')).read_text())
    text=template.replace('m_Name: HuntingRifle_Pickup','m_Name: '+name+'_Pickup')
    text=re.sub(r'(    Item: )\{[^}]+\}',r'\1{fileID: 11400000, guid: '+guid(data)+', type: 2}',text)
    magazine=re.search(r'  MaxAmmo: (\d+)',data.read_text())[1]
    text=re.sub(r'    SecondaryCount: \d+', '    SecondaryCount: '+magazine,text)
    text=re.sub(r'  m_Id: [^\n]*','  m_Id: '+str(uuid.uuid4()),text)
    prefix,visual=text.split('--- !u!1001',1)
    visual=visual.replace(rifle_guid,guid(model)).replace('value: HuntingRifle','value: '+name)
    for axis in 'xyz':
        for prop,value in [('m_LocalPosition',measured['position'][axis]),('m_LocalScale',scale)]:
            visual,count=re.subn(r'(propertyPath: '+prop+r'\.'+axis+r'\n      value:) [^\n]*',r'\g<1> '+str(value),visual)
            if count==0:
                assert prop=='m_LocalScale'
                visual=visual.replace('    m_RemovedComponents: []','    - target: {fileID: -8679921383154817045, guid: '+guid(model)+', type: 3}\n      propertyPath: '+prop+'.'+axis+'\n      value: '+str(value)+'\n      objectReference: {fileID: 0}\n    m_RemovedComponents: []',1)
    visual=visual.replace('    m_RemovedComponents: []',
        '    - target: {fileID: -7511558181221131132, guid: '+guid(model)+', type: 3}\n'
        "      propertyPath: 'm_Materials.Array.data[0]'\n      value: \n"
        '      objectReference: {fileID: 2100000, guid: '+material+', type: 2}\n    m_RemovedComponents: []',1)
    size=measured['size']
    prefix=re.sub(r'  m_Size: \{[^}]+\}', '  m_Size: {x: '+str(max(size['x'],.2))+', y: '+str(max(size['y'],.2))+', z: '+str(max(size['z'],.2))+'}',prefix)
    prefix=re.sub(r'  m_Center: \{[^}]+\}', '  m_Center: {x: 0, y: '+str(size['y']*.5)+', z: 0}',prefix)
    prefix=re.sub(r'  Offset: \{[^}]+\}', '  Offset: {x: 0, y: '+str(size['y']*.5)+', z: 0}',prefix)
    out=prefabs/(name+'_Pickup.prefab');out.write_text(prefix+'--- !u!1001'+visual);meta(out)
    print('ADDED',name,'loaded rounds',magazine,'size',size)

rackpath=prefabs/'Weapon_TestPickups.prefab'
rack=rackpath.read_text()
# Keep the existing nested IDs so scene instances retain their links.
positions={'Pistol_Pickup':(0,0),'Shotgun_Pickup':(1.25,0),'HuntingRifle_Pickup':(2.5,0),'FlareGun_Pickup':(3.75,0),'HuntingRifleAmmo_Pickup':(2.5,1.25),'FlareAmmo_Pickup':(3.75,1.25)}
chunks=rack.split('--- !u!1001')
for index in range(1,len(chunks)):
    name=re.search(r'propertyPath: m_Name\n      value: (\w+)',chunks[index])[1]
    x,z=positions[name]
    for axis,value in [('x',x),('z',z)]:
        chunks[index]=re.sub(r'(propertyPath: m_LocalPosition\.'+axis+r'\n      value:) [^\n]+',r'\g<1> '+str(value),chunks[index])
rack='--- !u!1001'.join(chunks)
parent=5116455486725574071
for offset,(name,x) in enumerate([('Pistol',0),('Shotgun',1.25)]):
    path=prefabs/(name+'_Pickup.prefab');g=guid(path)
    instance=740002000000000001+offset*10; stripped=instance+1
    target=7569754894736958610;go=5386159470166282903
    block='--- !u!1001 &'+str(instance)+'\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: '+str(parent)+'}\n    m_Modifications:\n'
    for fileid,prop,value in [(go,'m_Name',name+'_Pickup'),(target,'m_LocalPosition.x',x),(target,'m_LocalPosition.y',0),(target,'m_LocalPosition.z',0),(target,'m_LocalRotation.w',1),(target,'m_LocalRotation.x',0),(target,'m_LocalRotation.y',0),(target,'m_LocalRotation.z',0),(target,'m_LocalScale.x',1),(target,'m_LocalScale.y',1),(target,'m_LocalScale.z',1)]:
        block+='    - target: {fileID: '+str(fileid)+', guid: '+g+', type: 3}\n      propertyPath: '+prop+'\n      value: '+str(value)+'\n      objectReference: {fileID: 0}\n'
    block+='    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {fileID: 100100000, guid: '+g+', type: 3}\n'
    block+='--- !u!4 &'+str(stripped)+' stripped\nTransform:\n  m_CorrespondingSourceObject: {fileID: '+str(target)+', guid: '+g+', type: 3}\n  m_PrefabInstance: {fileID: '+str(instance)+'}\n  m_PrefabAsset: {fileID: 0}\n'
    if g not in rack:
        rack=rack.replace('  m_Children:\n','  m_Children:\n  - {fileID: '+str(stripped)+'}\n',1)
        rack+='\n'+block
rackpath.write_text(rack)
ammo_guid=guid(root/'Assets/Game/Weapons/Data/HuntingRifleAmmo.asset')
assert ammo_guid in (root/'Assets/Game/Weapons/Data/HuntingRifle.asset').read_text()
assert ammo_guid in (prefabs/'HuntingRifleAmmo_Pickup.prefab').read_text()
assert guid(root/'Assets/Game/Weapons/Models/RifleAmmo/RifleAmmo.fbx') in (prefabs/'HuntingRifleAmmo_Pickup.prefab').read_text()
print('VERIFIED hunting rifle uses the downloaded rifle ammo box and matching ammo item')
