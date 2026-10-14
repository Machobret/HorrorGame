from pathlib import Path
import re,uuid
root=Path.cwd();assets=root/'Assets'
controller=assets/'AssetStore/HorrorEngine/ExampleContent/Character/Character.controller'
s=controller.read_text()
mask=assets/'AssetStore/HorrorEngine/ExampleContent/Character/Masks/UpperBody.mask.meta'
mask_guid=re.search(r'guid: (\w+)',mask.read_text())[1]
if 'm_Name: Mobile Reload' not in s:
 layer=f'''  - serializedVersion: 5
    m_Name: Mobile Reload
    m_StateMachine: {{fileID: 750010000000000001}}
    m_Mask: {{fileID: 31900000, guid: {mask_guid}, type: 2}}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {{fileID: 9100000}}
'''
 start=s.index('  m_AnimatorLayers:');end=s.index('--- !u!',start)
 s=s[:end]+layer+s[end:]
 blocks=re.split(r'(?=^--- !u!)',s,flags=re.M)
 reload=next(b for b in blocks if b.startswith('--- !u!1102') and '  m_Name: WeaponReload\n' in b)
 reload=reload.replace('&-5888651838510493511','&750010000000000002').replace('m_IKOnFeet: 1','m_IKOnFeet: 0').replace('m_WriteDefaultValues: 1','m_WriteDefaultValues: 0')
 empty=reload.replace('&750010000000000002','&750010000000000003').replace('m_Name: WeaponReload','m_Name: Empty')
 empty=re.sub(r'm_Motion: \{[^}]+\}','m_Motion: {fileID: 0}',empty)
 machine='''--- !u!1107 &750010000000000001
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Mobile Reload
  m_ChildStates:
  - serializedVersion: 1
    m_State: {fileID: 750010000000000003}
    m_Position: {x: 200, y: 0, z: 0}
  - serializedVersion: 1
    m_State: {fileID: 750010000000000002}
    m_Position: {x: 400, y: 0, z: 0}
  m_ChildStateMachines: []
  m_AnyStateTransitions: []
  m_EntryTransitions: []
  m_StateMachineTransitions: {}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {x: 0, y: 50, z: 0}
  m_EntryPosition: {x: 0, y: 0, z: 0}
  m_ExitPosition: {x: 600, y: 0, z: 0}
  m_ParentStateMachinePosition: {x: 600, y: 50, z: 0}
  m_DefaultState: {fileID: 750010000000000003}
'''
 s+=machine+reload+empty
 controller.write_bytes(s.replace('\n','\r\n').encode())
folder=assets/'Game/Weapons/Animation';folder.mkdir(exist_ok=True)
p=folder/'AnimState_MobileReload.asset'
s=(assets/'AssetStore/HorrorEngine/ExampleContent/AnimStates/AnimState_Player_WeaponReload.asset').read_text().replace('m_Name: AnimState_Player_WeaponReload','m_Name: AnimState_MobileReload').replace('Layer: 2','Layer: 4')
p.write_bytes(s.replace('\n','\r\n').encode())
meta=Path(str(p)+'.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n')
handle_guid=re.search(r'guid: (\w+)',meta.read_text())[1]
character=assets/'AssetStore/HorrorEngine/Prefabs/Character.prefab'
s=character.read_text();parts=re.split(r'(?=^--- !u!)',s,flags=re.M)
for i,b in enumerate(parts):
 if b.startswith('--- !u!114 &6474223013066312300\n') and 'MobileReloadAnimation:' not in b:
  parts[i]=b+f'  MobileReloadAnimation: {{fileID: 11400000, guid: {handle_guid}, type: 2}}\n'
character.write_bytes(''.join(parts).replace('\n','\r\n').encode())
print('UPPER_BODY_MOBILE_RELOAD_LAYER_CONFIGURED')
