"""Add the omitted pistol/shotgun boxes without rebuilding other weapons."""
from pathlib import Path
import json,re,uuid
ROOT=Path(__file__).resolve().parents[2]
FOLDER=ROOT/'Assets/Game/Weapons/Prefabs'
ITEMS=ROOT/'Assets/AssetStore/HorrorEngine/ExampleContent/Items'
REPORT=json.loads((Path(__file__).parent/'TemplateAmmoGeometry.json').read_text())
def write(path,text):path.write_bytes(text.replace('\r\n','\n').replace('\n','\r\n').encode())
def guid(path):return re.search(r'^guid: (\w+)',Path(str(path)+'.meta').read_text(),re.M)[1]
rack=FOLDER/'Weapon_TestPickups.prefab';rack_text=rack.read_text()
prototype=rack_text[rack_text.index('--- !u!1001 &9148733122171922768'):]
for index,(name,model,count,model_transform,model_object) in enumerate([
 ('PistolAmmo','GunAmmo',15,3633606685666757362,4118254525684813896),
 ('ShotgunAmmo','ShotgunAmmo',4,5196804772522163944,4851804809731023954)]):
 entry=next(e for e in REPORT['entries'] if e['name']==model)
 target=FOLDER/(name+'_Pickup.prefab');text=(FOLDER/'HuntingRifleAmmo_Pickup.prefab').read_text()
 identity=re.search(r'm_Id: ([^\n]+)',target.read_text())[1] if target.exists() else str(uuid.uuid4())
 text=text.replace('HuntingRifleAmmo_Pickup',name+'_Pickup')
 text=re.sub(r'm_Id: [^\n]+','m_Id: '+identity,text)
 item_guid=guid(ITEMS/model/(model+'.asset'))
 text=re.sub(r'(  Entry:\n    Item:) \{[^}]+\}\n    Count: [^\n]+',r'\1 {fileID: 11400000, guid: '+item_guid+', type: 2}\n    Count: '+str(count),text)
 visual_guid=guid(ITEMS/model/(model+'.prefab'))
 text=text.replace('4b6e6704683f0d640961b7f2cdc65489',visual_guid).replace('-8679921383154817045',str(model_transform)).replace('919132149155446097',str(model_object))
 for axis in 'xyz':
  for field,value in [('Scale',entry['scale']),('Position',entry['position'][axis])]:
   pattern=r'(propertyPath: m_Local'+field+r'\.'+axis+r'\n      value:) [^\n]+'
   text=re.sub(pattern,lambda m:m[1]+' '+str(value),text)
 text=text.replace('value: RifleAmmo','value: '+model);write(target,text)
 meta=Path(str(target)+'.meta')
 if not meta.exists():write(meta,'fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
 prefab_guid=guid(target)
 if prefab_guid not in rack_text:
  instance_id=740003000000000001+index;transform_id=740003100000000001+index
  block=prototype.replace('688c22e50ab3c2746b194f16406e42a0',prefab_guid).replace('1046528341713748907','3959426251832750091').replace('5588204491852839664','7372703362884447932').replace('9148733122171922768',str(instance_id)).replace('3709732637515518880',str(transform_id)).replace('RevolverAmmo_Pickup',name+'_Pickup')
  block=re.sub(r'(propertyPath: m_LocalPosition.x\n      value:) [^\n]+',lambda m:m[1]+' '+str(index*1.25),block)
  rack_text=rack_text.replace('  m_Father: {fileID: 0}',f'  - {{fileID: {transform_id}}}\n  m_Father: {{fileID: 0}}',1)
  rack_text+=block
 print('TEMPLATE_AMMO_ADDED',name,count,prefab_guid)
write(rack,rack_text)
