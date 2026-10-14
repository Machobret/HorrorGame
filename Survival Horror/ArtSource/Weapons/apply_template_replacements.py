"""Keep every existing item/prefab identity; update its rendering and socket placement."""
from pathlib import Path
import json,re,uuid,shutil
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'ArtSource/Weapons/TemplateReplacements'
WEAPONS=ROOT/'Assets/AssetStore/HorrorEngine/ExampleContent/Weapons'
report=json.loads((ART/'geometry.json').read_text())
def backup(path):
 dest=ART/(path.parent.name+'_'+path.name+'.before')
 if not dest.exists():shutil.copy2(path,dest)
def override(text,guid,key,value):
 pattern=r'(target: \{fileID: -8679921383154817045, guid: '+guid+r', type: 3\}\n      propertyPath: '+re.escape(key)+r'\n      value:) [^\n]*'
 return re.sub(pattern,lambda m:m[1]+' '+str(value),text)
for name,entry in report.items():
 folder=WEAPONS/name
 model_guid=re.search(r'guid: ([0-9a-f]+)',(folder/(name+'.fbx.meta')).read_text())[1]
 material_guid=re.search(r'guid: ([0-9a-f]+)',(folder/(name+'.mat.meta')).read_text())[1]
 texmeta=folder/(name+'_PSX_Albedo.png.meta')
 if not texmeta.exists():
  text=(ROOT/'Assets/Game/Weapons/Models/Revolver/ColtPython.png.meta').read_text()
  text=re.sub(r'^guid: [0-9a-f]+','guid: '+uuid.uuid4().hex,text,flags=re.M)
 else:text=texmeta.read_text()
 text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 128',text)
 text=re.sub(r'filterMode: \d+','filterMode: 0',text)
 text=text.split('AssetOrigin:')[0];texmeta.write_text(text)
 tex_guid=re.search(r'guid: ([0-9a-f]+)',text)[1]
 modelmeta=folder/(name+'.fbx.meta');backup(modelmeta);text=modelmeta.read_text()
 text=text.replace('externalObjects: {}','externalObjects:\n  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: TemplateWeapon\n    second: {fileID: 2100000, guid: '+material_guid+', type: 2}')
 text=re.sub(r'materialImportMode: \d+','materialImportMode: 2',text);text=re.sub(r'importAnimation: \d+','importAnimation: 0',text)
 text=re.sub(r'isReadable: \d+','isReadable: 1',text);modelmeta.write_text(text)
 material=folder/(name+'.mat');backup(material);text=material.read_text()
 text=re.sub(r'(- _MainTex:\n        m_Texture:) \{[^}]+\}',r'\1 {fileID: 2800000, guid: '+tex_guid+', type: 3}',text)
 material.write_text(text)
 iconmeta=folder/(name+'.png.meta');backup(iconmeta);text=iconmeta.read_text()
 text=re.sub(r'maxTextureSize: \d+','maxTextureSize: 1024',text);iconmeta.write_text(text)
 # Existing muzzle transform IDs are deliberately retained.
 held=folder/(name+'.prefab');backup(held);text=held.read_text()
 blocks=re.split(r'(?=--- !u!)',text)
 for i,block in enumerate(blocks):
  if block.startswith('--- !u!4 &3683940282338885898\n'):
   x,y,z=entry['muzzle_local'];blocks[i]=re.sub(r'm_LocalPosition: \{[^}]+\}',f'm_LocalPosition: {{x: {x}, y: {y}, z: {z}}}',block)
 held.write_text(''.join(blocks))
 width,length,height=entry['size'];wx,forward_negative,up=entry['center']
 forward=-forward_negative;sign=-1 if name=='Gun' else 1
 center=[forward*sign,up,wx];minimum_y=up-height*.5
 pickup=ROOT/'Assets/Game/Weapons/Prefabs'/('Pistol_Pickup.prefab' if name=='Gun' else 'Shotgun_Pickup.prefab')
 backup(pickup);text=pickup.read_text()
 for axis,value in zip('xyz',[-center[0],-minimum_y,-center[2]]):text=override(text,model_guid,'m_LocalPosition.'+axis,value)
 text=re.sub(r'm_Size: \{[^}]+\}',f'm_Size: {{x: {max(.2,length)}, y: {max(.2,height)}, z: {max(.2,width)}}}',text)
 text=re.sub(r'm_Center: \{[^}]+\}',f'm_Center: {{x: 0, y: {height*.5}, z: 0}}',text)
 text=re.sub(r'Offset: \{[^}]+\}',f'Offset: {{x: 0, y: {height*.5}, z: 0}}',text);pickup.write_text(text)
 exam=folder/(name+'Examination.prefab');backup(exam);text=exam.read_text()
 for axis in 'xyz':text=override(text,model_guid,'m_LocalScale.'+axis,entry['held_scale'])
 # The shotgun examination rotates 180 degrees around its vertical axis.
 position=[-center[0],-center[1],-center[2]] if name=='Gun' else [center[0],-center[1],center[2]]
 for axis,value in zip('xyz',position):text=override(text,model_guid,'m_LocalPosition.'+axis,value)
 exam.write_text(text)
 data=folder/(name+'.asset');backup(data);text=data.read_text()
 display='1911 pistol' if name=='Gun' else 'Double-barrel shotgun'
 description='A 1911-style pistol. Uses pistol ammunition.' if name=='Gun' else 'A side-by-side double-barrel shotgun. Uses shotgun shells.'
 for field,value in [('Name',display),('Description',description)]:
  text=re.sub(r'(  '+field+r':\n    IsLocalized:) \d+',r'\1 0',text)
  text=re.sub(r'(  '+field+r':\n    IsLocalized: 0\n    Unlocalized:) [^\n]*',lambda m:m[1]+' '+value,text)
 data.write_text(text)
 print('TEMPLATE_REFERENCES_UPDATED',name)
