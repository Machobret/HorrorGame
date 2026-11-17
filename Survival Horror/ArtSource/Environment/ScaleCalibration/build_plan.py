"""Build explicit per-prefab metre-scale decisions from the visual review and Unity size audit."""
from pathlib import Path
import json

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
PACKS=PROJECT/'ArtSource/Environment/DownloadedEnvironmentPacks'
AUDIT=PROJECT/'Exports/Reviews/PrefabCollision/ScaleAudit'

def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

def build():
    catalog=read(PACKS/'QualityCatalog.json')['assets'];measured={a['path']:a for a in read(AUDIT/'AllPrefabs.json')['assets']}
    models={int(a['id'][1:]):a for a in catalog if a['pack']=='ModelsPackPSX' and a['status']!='Removed'}
    decisions={}
    def set_rule(ids,role,factor=None,axis=None,target=None):
        for token in ids.split():
            start,_,end=token.partition('-')
            for ident in range(int(start),int(end or start)+1):
                if ident not in models:continue
                a=models[ident];size=measured[a['prefab']]['size']
                denominator=max(size[0],size[2]) if axis=='horizontal' else max(size) if axis=='max' else size[int(axis)] if axis is not None else 1
                value=target/denominator if target is not None else factor
                decisions[ident]=dict(role=role,factor=value,reference=(f'Overall {axis} dimension {target:g} m' if target is not None else f'Uniform family calibration {value:g}; source proportions retained'))
    set_rule('126-537','General reviewed prop',factor=.5)
    set_rule('126 283','Bed pillow',axis='max',target=.70)
    set_rule('127 344','Square cushion',axis='max',target=.50)
    set_rule('128-162 528 532-533','Vegetation; preserve relative tree/shrub/vine sizes',factor=.5)
    set_rule('163','Compact van',axis='horizontal',target=4.80)
    set_rule('164','Sedan',axis='horizontal',target=4.40)
    set_rule('165 168','Open industrial skip, filled/empty pair',factor=3.5/10.424)
    set_rule('166-167','Covered recycling container',factor=3.2/9.377)
    set_rule('169-170','Large roll-off skip',factor=6/16.710)
    set_rule('171 174','Large debris pile',axis='horizontal',target=6)
    set_rule('172-173 179-180','Debris cluster',factor=.5)
    set_rule('175','Compressed waste bale',axis=1,target=1.3)
    set_rule('176','Compressed waste bale, low variant',factor=.4)
    set_rule('177','Compressed waste bale',axis=1,target=1.1)
    set_rule('178','Compressed waste bale, tall variant',axis=1,target=1.6)
    set_rule('181','Complete bed',axis='horizontal',target=2.05)
    set_rule('182 226 280-281','Matching single bed frame/mattress family',factor=.46)
    set_rule('183','Toilet',axis=1,target=.80)
    set_rule('184','Lined wastebasket',factor=.5)
    set_rule('185','Tall wastebasket',axis=1,target=.8)
    set_rule('186','Small bottle/jar',axis=1,target=.16)
    set_rule('187','Square ventilation block',factor=.5)
    set_rule('188 209-210','Shipping carton',factor=.35)
    set_rule('189-201 221','Wrapped pallet/carton stacks',factor=.4)
    set_rule('202-206','Wooden crate or compressed bale',factor=.35)
    set_rule('207 310 317','Matching warehouse storage rack family',axis=1,target=2.05)
    set_rule('208','Wok with side handles',axis='max',target=.42)
    set_rule('211','Low metal storage rack',axis=1,target=1.8)
    set_rule('212-214','Matching single/double/triple warehouse racks',axis=1,target=2.5)
    set_rule('220','20-foot site container',axis='horizontal',target=6.06)
    set_rule('222','Shopping cart',axis=1,target=1)
    set_rule('223','Small cottage with pitched roof',factor=.5)
    set_rule('225','Compact disc',axis='horizontal',target=.12)
    set_rule('235 454','Pedestal sink',axis=1,target=.85)
    set_rule('236 265 309 371 374 384','Kitchen worktop/counter',axis=1,target=.90)
    set_rule('239 277','Freestanding range including raised backguard',axis=1,target=1.10)
    set_rule('242-243','Sink counter including backsplash',axis=1,target=.95)
    set_rule('245','Bridge faucet',axis='max',target=.30)
    set_rule('248 293','Wall cabinet',axis=1,target=.72)
    set_rule('251 378 382','Wooden cooking spatula',axis='max',target=.35)
    set_rule('252','Kitchen tongs',axis='max',target=.30)
    set_rule('253-254','Table fork/spoon',axis='max',target=.20)
    set_rule('255','Kitchen knife',axis='max',target=.28)
    set_rule('256 282 386','Wall-mounted shelf with brackets',factor=.5)
    set_rule('257','Single-storey commercial building',axis=1,target=4.2)
    set_rule('284 506','Matching three-tier table/workbench',axis=1,target=.78)
    set_rule('285','Folded table',axis='horizontal',target=1.2)
    set_rule('286','Wooden serving tray',axis='horizontal',target=.45)
    set_rule('287 316','Bathroom vanity and basin',axis=1,target=.90)
    set_rule('290-291','Closed/open paper folder or book',axis='horizontal',target=.28)
    set_rule('292','Bathroom cabinet',axis=1,target=.85)
    set_rule('296','Range hood and chimney',axis=0,target=.80)
    set_rule('297-300','Point-of-sale terminal family',factor=.4)
    set_rule('301 305 352','Tall cupboard/wardrobe',axis=1,target=1.90)
    set_rule('304','Storage drawer/bin',factor=.5)
    set_rule('311 393','Single-storey exterior building',factor=.5)
    set_rule('313','Wooden hook rail',factor=.5)
    set_rule('314','Plastic washing basin',factor=.45)
    set_rule('320-323 325-334','Matching CRT television family',factor=.4)
    set_rule('324','Marble pedestal sink and faucet',axis=1,target=.90)
    set_rule('335','Bent reading light',factor=.5)
    set_rule('336-337','Dish drainer/rack',factor=.5)
    set_rule('338','Kitchen sponge',axis='max',target=.12)
    set_rule('339 342 380 381','Dining/visitor chair',axis=1,target=1.0)
    set_rule('340','Wooden cutting board',axis='horizontal',target=.40)
    set_rule('341','Shelved kitchen workbench',axis=1,target=.90)
    set_rule('343','Domestic satellite dish and stand',axis=0,target=.80)
    set_rule('345-346','Matching mattress and wooden frame',factor=.55)
    set_rule('347-348','Writing desk',axis=1,target=.75)
    set_rule('349 353','Side/bedside cabinet',axis=1,target=.60)
    set_rule('354','Dining chair',axis=1,target=.95)
    set_rule('355-356','Dining table',axis=1,target=.75)
    set_rule('357','Low coffee table',axis=1,target=.43)
    set_rule('358-360','Seat cushion',factor=.5)
    set_rule('361-362','Matching upholstered armchair/sofa',axis=1,target=.90)
    set_rule('365 368 377','Wall cupboard/hanging storage',factor=.5)
    set_rule('375','High-back dining chair',axis=1,target=1.05)
    set_rule('376','Refrigerator',axis=1,target=1.80)
    set_rule('379','Tall gas cylinder',axis=1,target=1.20)
    set_rule('385','Freestanding cooker',axis=1,target=.90)
    set_rule('387-388 390-391','Ceiling-fan family, drop lengths preserved',factor=.36)
    set_rule('389 392','Small standing light/bollard, plausible existing height',factor=1)
    set_rule('394','Toilet-paper roll',axis='max',target=.12)
    set_rule('395 398','Large stockpot/lid pair',factor=.4)
    set_rule('396 399-408 413-416 435 442-444','Ceramic/plastic tableware and basins',factor=.45)
    set_rule('397','Table spoon',axis='max',target=.22)
    set_rule('409','Cooking spoon',axis='max',target=.38)
    set_rule('410','Ladle',axis=1,target=.38)
    set_rule('411-412 432-433','Wooden serving platter/bowl family',factor=.21)
    set_rule('417','Tall plastic waste bin',axis=1,target=.65)
    set_rule('418','Wide plastic washing tub',factor=.5)
    set_rule('419-420','Rotating snack display',axis=1,target=1.60)
    set_rule('422 424 431','Matching water tank and platform assemblies',factor=.55)
    set_rule('425','Utility pole',axis=1,target=9)
    set_rule('430','Office chair',axis=1,target=1.05)
    set_rule('434','Ornamental tree',factor=.5)
    set_rule('436','Standing light/coat stand',axis=1,target=1.65)
    set_rule('437','Small hood/vent fitting',factor=.5)
    set_rule('438-441 447-453','Mugs and metal cookware',factor=.4)
    set_rule('445-446','Plastic detergent jug',axis=1,target=.30)
    set_rule('455-502','Retail display/shelf family, shelf-bank lengths retained',factor=.6)
    set_rule('455 460','Tall wooden bookcase',axis=1,target=2)
    set_rule('478-479','Low produce display island',factor=.5)
    set_rule('490-491','Tall retail shelving bank',axis=1,target=2.05)
    set_rule('492','Refrigerated meat display',axis=1,target=1.60)
    set_rule('503','Countertop blender',axis=1,target=.42)
    set_rule('504','Bunk bed',factor=.46)
    set_rule('505','Utility-meter fitting',axis='max',target=.18)
    set_rule('507-512','Dining/kitchen table',axis=1,target=.75)
    set_rule('513','Round coffee table',axis=1,target=.43)
    set_rule('514-517','Countertop microwave',axis=0,target=.54)
    set_rule('518-525 534-537','Matching saucepan/frying-pan family',factor=.35)
    set_rule('527','Glass aquarium',axis=0,target=1.0)
    set_rule('529','Standalone mattress',axis='horizontal',target=2)
    set_rule('530','Small plastic tray, plausible existing size',factor=1)
    set_rule('531','Plastic armchair',axis=1,target=.95)
    assets=[]
    for ident,a in models.items():
        rule=decisions[ident];old=measured[a['prefab']]['size'];factor=rule['factor']
        assets.append(dict(id=a['id'],path=a['prefab'],guid=a['guid'],role=rule['role'],reference=rule['reference'],factor=factor,source_size_m=old,target_size_m=[v*factor for v in old]))
    # Other libraries: targeted corrections after silhouette and horizontal-surface review.
    def other(path,role,factor=None,axis=None,target=None,reference=None):
        a=measured[path];old=a['size'];denominator=max(old[0],old[2]) if axis=='horizontal' else max(old) if axis=='max' else old[int(axis)] if axis is not None else 1
        value=target/denominator if target is not None else factor
        assets.append(dict(id='',path=path,guid=a['guid'],role=role,reference=reference or (f'Overall {axis} dimension {target:g} m' if target is not None else f'Visually checked family calibration {value:g}'),factor=value,source_size_m=old,target_size_m=[v*value for v in old]))
    other('Assets/Game/Environment/ToolsEquipment/Classic64_RollingToolChest_Visual.prefab','Rolling workshop tool chest',factor=.2,reference='0.97 m high, 0.75 m wide; four-drawer wheeled chest')
    other('Assets/Game/Environment/StorageContainers/Classic64_GarbageBag_Visual.prefab','Pair of filled garbage bags',factor=.23,reference='Each bag approximately 0.75 m tall; preserve two-bag spacing')
    other('Assets/Game/Environment/StorageContainers/Classic64_WoodPallet_Visual.prefab','Square wooden pallet',factor=.4,reference='1.20 m square pallet; authored square proportions retained')
    for path,a in measured.items():
        if not path.startswith('Assets/Game/Environment/'):continue
        name=Path(path).stem
        if a['family']=='Furniture':
            if name.startswith(('HouseProps_bed','Village_bed')):
                other(path,'Bed/frame including headboard',axis='horizontal',target=2.2 if name=='HouseProps_bed2' else 2.15,reference='Overall bed-frame length 2.15 m; low bunk 2.20 m. Silhouettes checked for attached furniture.')
            if name in {'HouseProps_chair','HouseProps_chair2','HouseProps_chair3','HouseProps_chair4','Kitchen_chair','Village_Chair','Village_Chair_01','Village_Chair_02','Village_Chair_03'}:
                seat=.56 if name in {'HouseProps_chair2','Village_Chair_02'} else .54 if name in {'Kitchen_chair','Village_Chair_03'} else .60
                other(path,'Dining chair',factor=.46/seat,reference=f'Measured seat surface {seat:.2f} m; calibrated to approximately 0.46 m')
            if name.startswith('HouseProps_couch'):
                other(path,'Upholstered lounge seating',factor=.75,reference='Measured cushion tops 0.62–0.64 m; corrected to 0.47–0.48 m')
            if name=='Village_Armchair_04':
                other(path,'Low-back sofa',factor=.46/.66,reference='Measured seat 0.66 m; corrected to 0.46 m')
            if name in {'Kitchen_table_01','Kitchen_table_02','Village_Table','Village_Table_01'}:
                other(path,'Dining table',axis=1,target=.76,reference='Conventional 0.76 m dining-table top')
        if a['family']=='Office' and name.startswith(('Office_armchair_','Office_couch_')):
            other(path,'Matching office lounge seating',factor=.75,reference='Measured seat top 0.62 m; calibrated to 0.465 m, preserving all colour variants')
        if name=='Office_office_chair_fancy':
            other(path,'Adjustable high-back office chair',factor=.50/.58,reference='Measured seat 0.58 m; calibrated to 0.50 m')
        if a['family']=='StorageContainers' and name.startswith('Urban_TrashCan'):
            other(path,'Matching public litter-bin family',factor=.5,reference='2.105 m source bin corrected to 1.053 m; preserve all colour/lid variants')
    plan=dict(units='Unity metres',method='Uniform scaling per visually identified object/family; no axis stretching. Original proportions, pivots, hierarchy, geometry, materials and collider bindings retained.',assets=assets,
        wider_audit=dict(measured_prefabs=len(measured),input='Exports/Reviews/PrefabCollision/ScaleAudit/AllPrefabs.json',surface_evidence='Exports/Reviews/PrefabCollision/ScaleAudit/FurnitureSurfaces.json',retained_notes=['Previously calibrated Cars_* and Classic64 Volvo/shovel/guardrail sizes retained.','Pizzeria prep tables retain approximately 0.90–0.96 m worktops; overall height includes backsplashes.','Large buildings, sewer/metro assemblies, pipe lengths and modular architecture are not normalized to prop size.','Plausible smaller fixtures and trays retained; reviewed ModelsPackPSX entries include three unchanged prefabs.']))
    write(SOURCE/'ScalePlan.json',plan)
    print('ModelsPackPSX reviewed:',len(models),'resized:',sum(abs(decisions[i]['factor']-1)>1e-5 for i in models))
    print('Unclassified:',[(a['id'],a['path']) for a in assets if a['role']=='General reviewed prop'])
    print('Additional corrections:',len(assets)-len(models),'Total reviewed targets:',len(assets))

if __name__=='__main__':build()
