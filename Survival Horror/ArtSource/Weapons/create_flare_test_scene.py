from pathlib import Path
import re,uuid,math
root=Path.cwd();assets=root/'Assets';folder=assets/'Game/Weapons/TestRoom';folder.mkdir(exist_ok=True)
def guid(p):
 meta=Path(str(p)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
 return re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1]
def write(p,s):p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode())
base=(assets/'AssetStore/HorrorEngine/ExampleContent/Scenes/EmptyScene.unity').read_text()
blocks=re.split(r'(?=^--- !u!)',base,flags=re.M)
floor=''.join(b for b in blocks if re.match(r'--- !u!\d+ &169305367[1-5]\n',b))
floor=floor.replace('  - component: {fileID: 1693053676}\n','')
mat_source=(assets/'Game/Weapons/Materials/HuntingRifle_Material_066.mat').read_text()
materials={}
for name,c in [('Concrete',(.23,.26,.29)),('Backdrop',(.07,.09,.11)),('Marker',(.8,.15,.08))]:
 p=folder/(name+'.mat');s=mat_source
 s=re.sub(r'  m_Name: [^\n]+','  m_Name: FlareTest_'+name,s)
 s=re.sub(r'(m_Texture:) \{[^}]+\}',r'\1 {fileID: 0}',s)
 for prop in ('_BaseColor','_Color'):s=re.sub(r'(- '+prop+r':) \{[^}]+\}',lambda m:m[1]+f' {{r: {c[0]}, g: {c[1]}, b: {c[2]}, a: 1}}',s)
 s=re.sub(r'(- _Smoothness:) [^\n]+',r'\1 0',s);write(p,s);materials[name]=guid(p)
parts=[];children=[]
def cube(name,pos,size,material):
 i=750000000000000100+len(children)*10
 s=floor
 for j in range(1,6):s=s.replace(str(1693053670+j),str(i+j))
 s=s.replace('m_Name: Floor','m_Name: '+name)
 s=re.sub(r'm_LocalPosition: \{[^}]+\}',f'm_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}',s)
 s=re.sub(r'm_LocalScale: \{[^}]+\}',f'm_LocalScale: {{x: {size[0]}, y: {size[1]}, z: {size[2]}}}',s)
 s=s.replace('m_Father: {fileID: 0}','m_Father: {fileID: 750000000000000002}')
 s=re.sub(r'(m_Materials:\n  -) \{[^}]+\}',r'\1 {fileID: 2100000, guid: '+materials[material]+', type: 2}',s)
 parts.append(s);children.append(i+5)
for x in range(-6,6):
 for z in range(-4,14):cube(f'Floor tile {x} {z}',(x+.5,-.125,z+.5),(1,.25,1),'Concrete')
cube('Dark side backdrop',(-5,2.5,5),(.2,5,18),'Backdrop')
cube('End wall',(0,2.5,13),(12,5,.2),'Concrete')
for name,pos,size in [('2m target',(-2.5,1,2),(.8,2,.3)),('4m target',(2.5,1,4),(.8,2,.3)),('6m panel',(0,1,6),(1.5,2,.3))]:cube(name,pos,size,'Concrete')
for distance in range(2,11,2):cube(str(distance)+' metre line',(0,.008,distance),(8,.016,.035),'Marker')
arena='''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &750000000000000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 750000000000000002}
  m_Layer: 0
  m_Name: FlareTestArena
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &750000000000000002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 750000000000000001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children:
'''+''.join(f'  - {{fileID: {i}}}\n' for i in children)+'''  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
'''+''.join(parts)
arena_path=folder/'FlareTestArena.prefab';write(arena_path,arena);arena_guid=guid(arena_path)
# Use the template's complete player/UI/core setup and a separate scene GUID.
for i,b in enumerate(blocks):
 if ('m_Name: Floor\n' in b or 'm_Name: Navigation\n' in b) and b.startswith('--- !u!1'):blocks[i]=b.replace('m_IsActive: 1','m_IsActive: 0')
 if b.startswith('--- !u!108'):blocks[i]=re.sub(r'm_Intensity: [^\n]+','m_Intensity: 0.3',b)
 if b.startswith('--- !u!104'):
  blocks[i]=b.replace('m_AmbientIntensity: 1','m_AmbientIntensity: 0.15').replace('m_SkyboxMaterial: {fileID: 10304, guid: 0000000000000000f000000000000000, type: 0}','m_SkyboxMaterial: {fileID: 0}')
 if b.startswith('--- !u!1001 &1686401510'):
  # Camera trigger covers the entire room; virtual camera has a wide overview.
  b=re.sub(r'(propertyPath: m_Lens.FieldOfView\n      value:) [^\n]+',r'\1 65',b)
  extra=''
  for axis,value in [('x',100),('y',30),('z',100)]:
   extra+=f'    - target: {{fileID: 5130916788458517775, guid: 57be7cbfb83c77b4196f49c1698080c1, type: 3}}\n      propertyPath: m_Size.{axis}\n      value: {value}\n      objectReference: {{fileID: 0}}\n'
  for axis in 'xyz':
   b=re.sub(r'(target: \{fileID: 5789214400614700604,[^\n]+\n      propertyPath: m_LocalPosition\.'+axis+r'\n      value:) [^\n]+',r'\1 0',b)
  b=b.replace('fileID: 1579692686389723225,','fileID: 5270810905108574020,')
  for axis,value in zip('xyz',(7,6,-7)):
   b=re.sub(r'(target: \{fileID: 7687138500670737955,[^\n]+\n      propertyPath: m_LocalPosition\.'+axis+r'\n      value:) [^\n]+',lambda m:m[1]+' '+str(value),b)
  yaw=math.atan2(-7,11);pitch=math.atan2(5,math.sqrt(170))
  sx,cx=math.sin(pitch/2),math.cos(pitch/2);sy,cy=math.sin(yaw/2),math.cos(yaw/2)
  for axis,value in zip('xyzw',(cy*sx,sy*cx,-sy*sx,cy*cx)):
   b=re.sub(r'(target: \{fileID: 7687138500670737955,[^\n]+\n      propertyPath: m_LocalRotation\.'+axis+r'\n      value:) [^\n]+',lambda m:m[1]+' '+str(value),b)
  b=b.replace('    m_RemovedComponents:',extra+'    m_RemovedComponents:');blocks[i]=b
scene=''.join(blocks)
camera_stripped=750001000000000001
extras=f'''--- !u!4 &{camera_stripped} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 7687138500670737955, guid: 57be7cbfb83c77b4196f49c1698080c1, type: 3}}
  m_PrefabInstance: {{fileID: 1686401510}}
  m_PrefabAsset: {{fileID: 0}}
'''
def instance(g,pid,root_transform,pos,overrides=''):
 return f'''--- !u!1001 &{pid}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
'''+''.join(f'    - target: {{fileID: {root_transform}, guid: {g}, type: 3}}\n      propertyPath: m_LocalPosition.{axis}\n      value: {value}\n      objectReference: {{fileID: 0}}\n' for axis,value in zip('xyz',pos))+overrides+f'''    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {g}, type: 3}}
'''
roots=[]
for name,pos,pid in [('FlareGun',(1,0,0),750001000000000010),('FlareAmmo',(-1,0,0),750001000000000011)]:
 path=assets/'Game/Weapons/Prefabs'/(name+'_Pickup.prefab');s=path.read_text()
 bs=re.split(r'(?=^--- !u!)',s,flags=re.M)
 tr=next(b for b in bs if b.startswith('--- !u!4') and 'm_Father: {fileID: 0}' in b);tid=re.search(r'&([0-9]+)',tr)[1]
 pickup=next(b for b in bs if '  Entry:' in b);component=re.search(r'&([0-9]+)',pickup)[1]
 pg=guid(path);override=''
 if name=='FlareAmmo':override=f'    - target: {{fileID: {component}, guid: {pg}, type: 3}}\n      propertyPath: Entry.Count\n      value: 10\n      objectReference: {{fileID: 0}}\n'
 extras+=instance(pg,pid,tid,pos,override);roots.append(pid)
pid=750001000000000012;extras+=instance(arena_guid,pid,750000000000000002,(0,0,0));roots.append(pid)
room_guid=guid(assets/'Game/Weapons/FlareTestRoom.cs');data_guid=guid(assets/'Game/Weapons/Data/FlareGun.asset');projectile_guid=guid(assets/'Game/Weapons/Effects/SignalFlare.prefab')
projectile=(assets/'Game/Weapons/Effects/SignalFlare.prefab').read_text()
pcid=re.search(r'^--- !u!114 &(\d+)\nMonoBehaviour:[\s\S]*?m_Script: \{fileID: 11500000, guid: '+guid(assets/'Game/Weapons/SignalFlare.cs'),projectile,re.M)
# Locate component by its script GUID, independent of serialization order.
pcblock=next(b for b in re.split(r'(?=^--- !u!)',projectile,flags=re.M) if 'guid: '+guid(assets/'Game/Weapons/SignalFlare.cs') in b);pcid=re.search(r'&(\d+)',pcblock)[1]
extras+=f'''--- !u!1 &750001000000000020
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 750001000000000021}}
  - component: {{fileID: 750001000000000022}}
  m_Layer: 0
  m_Name: Flare test controls
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &750001000000000021
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 750001000000000020}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children: []
  m_Father: {{fileID: 0}}
--- !u!114 &750001000000000022
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 750001000000000020}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {room_guid}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  FlareGun: {{fileID: 11400000, guid: {data_guid}, type: 2}}
  Projectile: {{fileID: {pcid}, guid: {projectile_guid}, type: 3}}
  TestCamera: {{fileID: {camera_stripped}}}
'''
roots.append(750001000000000021)
scene=scene.replace('--- !u!1660057539 &9223372036854775807',extras+'--- !u!1660057539 &9223372036854775807')
scene+=''.join(f'  - {{fileID: {i}}}\n' for i in roots)
path=assets/'Scenes/FlareGun_TestRoom.unity';write(path,scene);guid(path)
print('FLARE_TEST_SCENE_CREATED',path,'arena cubes',len(children))
rsp=(root/'Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.rsp').read_text()
rsp=re.sub(r'^-out:.*','-out:"C:/Users/Mangis/AppData/Local/Temp/FlareTestCompile.dll"',rsp,flags=re.M)
rsp=re.sub(r'^-refout:.*','-refout:"C:/Users/Mangis/AppData/Local/Temp/FlareTestCompile.ref.dll"',rsp,flags=re.M)
if 'Assets/Game/Weapons/FlareTestRoom.cs' not in rsp:rsp+='\n"Assets/Game/Weapons/FlareTestRoom.cs"\n'
Path('C:/Users/Mangis/AppData/Local/Temp/flare-test-compile.rsp').write_text(rsp)
