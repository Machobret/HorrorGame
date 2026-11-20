"""Propose browser coverage for the owned Unity library without changing any assets."""

from collections import Counter
import csv
import json
from pathlib import Path
import re

from build_asset_library import build_pilot_catalogue, normalize
from asset_library_variants import assign_families


PROJECT = Path(__file__).resolve().parents[2]
GAME = PROJECT / 'Assets/Game'
ART = PROJECT / 'ArtSource/Environment'
CATALOGUE = GAME / 'Environment/Editor/AssetLibrary/Catalog.json'
OUTPUT = PROJECT / 'Exports/Reviews/AssetLibrary/WholeProject'
MODELS_PACK_SUBJECTS = ART / 'AssetLibrary/ModelsPackPSXSubjects.json'

DECISIONS = {
    'Identity': ('Ambiguous subject or standalone role',
                 'Name and group each from a native render. Keep uncertain fragments in Technical until their placement role is clear.'),
}

REUSABLE_CHAPTER_PROPS = {
    'AbandonedTruck': ('Street & transport', 'Road vehicles', ('Street',)),
    'PharmacyFridge': ('Appliances & electronics', 'Refrigeration', ('Pharmacy',)),
    'PharmacyShelves': ('Storage', 'Shelving', ('Pharmacy',)),
    'StreetLamp': ('Street & transport', 'Street lighting', ('Street',)),
    'WashingMachine': ('Appliances & electronics', 'Laundry appliances', ('Laundry',)),
}

PILOT_GROUPS = {
    'Appliances': 'Appliances & electronics', 'Food': 'Food & kitchenware', 'Kitchenware': 'Food & kitchenware',
    'Fixtures': 'Commercial & built-in fixtures', 'Household': 'Decor & clutter', 'Furniture': 'Furniture',
    'Retail': 'Commercial & built-in fixtures', 'Storage': 'Storage',
}

PROP_GROUPS = {
    'Appliances & electronics': 'Appliances & electronics',
    'Buildings': 'Buildings',
    'Commercial & built-in fixtures': 'Fixtures & utilities',
    'Decor & clutter': 'Decor & signage',
    'Fixtures & utilities': 'Fixtures & utilities',
    'Food & kitchenware': 'Food & kitchenware',
    'Furniture': 'Furniture',
    'Interactables': 'Interactables',
    'Nature & debris': 'Nature & debris',
    'Signs & graphics': 'Decor & signage',
    'Storage': 'Storage',
    'Street & transport': 'Street & vehicles',
    'Tools & equipment': 'Tools & equipment',
    'Waste & recycling': 'Waste & recycling',
}

# Other Buildings inventory subgroups are construction kits, loose parts, or doors.
PLACEABLE_BUILDING_SUBGROUPS = {
    'Background buildings', 'Commercial buildings', 'Complete buildings',
    'Industrial buildings', 'Urban buildings', 'Urban exteriors',
}

# Collections are cross-category scene kits. Smaller contexts remain regular tags.
PROP_COLLECTIONS = ('Kitchen', 'Grocery Store', 'Office', 'Restaurant', 'Street')

# Named against the saved front renders for Pizzeria. IDs are pinned to the quality catalogue.
PIZZERIA_REVIEWED = {
    'P0762': ('Buildings', 'Urban buildings', 'Masonry corner building'),
    'P0912': ('Buildings', 'Urban buildings', 'Masonry corner building'),
    'P0913': ('Buildings', 'Urban buildings', 'Masonry corner building'),
    'P0914': ('Buildings', 'Commercial buildings', 'Low commercial building'),
    'P0772': ('Street & transport', 'Planters & street dressing', 'Low paved or planter base'),
    'P0920': ('Commercial & built-in fixtures', 'Counters & displays', 'Wood service counter'),
    'P0921': ('Commercial & built-in fixtures', 'Counters & displays', 'Glass food display case'),
    'P0922': ('Commercial & built-in fixtures', 'Counters & displays', 'Short wood counter'),
    'P0923': ('Signs & graphics', 'Menus', 'Countertop menu boards'),
    'P0924': ('Commercial & built-in fixtures', 'Counters & displays', 'Unmarked white fixture'),
    'P0927': ('Street & transport', 'Posts & bollards', 'Pair of safety posts'),
    'P0928': ('Food & kitchenware', 'Serving accessories', 'Cup of straws'),
    'P0971': ('Fixtures & utilities', 'Lighting fixtures', 'Square ceiling light panel'),
}

OTHER_RENDER_SUBJECTS = {
    'P0349': 'Woodgrain CRT television · P0349',
    'P0182': 'Single bed with mattress',
    'P0226': 'Single mattress',
    'P0280': 'Metal single bed frame',
    'P0281': 'Striped single mattress',
    'P0345': 'Plain mattress',
    'P0346': 'Low single bed platform',
    'P0436': 'Freestanding coat rack',
    'P0529': 'Checked mattress',
    'P1124': 'Wood pallet',
}

# Standalone subjects confirmed in the prefab renders. The surrounding kit parts
# still follow their original architecture and infrastructure rules.
REVIEWED_PACK_PROPS = {
    'P0069': ('Fixtures & utilities', 'Ventilation fixtures', 'Packaged industrial ventilation unit', ('Industrial Yard',)),
    'P0070': ('Fixtures & utilities', 'Ventilation fixtures', 'Framed industrial exhaust fan', ('Industrial Yard',)),
    'P0071': ('Fixtures & utilities', 'Ventilation fixtures', 'Cross-braced industrial exhaust fan', ('Industrial Yard',)),
    'P1030': ('Fixtures & utilities', 'Access ladders', 'Sewer access ladder', ('Sewer',)),
}

VARIANT_NOTES = {
    'Urban roads': 'Group by junction and bridge shape. Switch surface or edge treatment inside that shape.',
    'Urban fences': 'Group by fence material and construction kit. Keep posts, corners and gates identifiable.',
    'Office partitions': 'Group by assembled shape or height. Treat finish and accent colour as variants.',
    'Urban dumpsters': 'Group complete dumpsters by body shape, then lid, fill and paint. Keep loose lids and piles technical or separate.',
    'Urban trash cans': 'Group by body. Open, closed, filled and colour variants belong inside that family.',
    'Car prefabs': 'Group cars by silhouette or base model. Preserve police and taxi as searchable variants.',
    'PSX shelving': 'Pair Normal and Horror versions of the same shelf. Keep different shelving shapes separate.',
    'Door variants': 'Group by door role and opening type. Keep working, decorative and setup assets distinct.',
    'Baked retail displays': 'Group by shelf shape. Keep the Background status visible so these can be used at the intended distance.',
}


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def guid_of(path):
    meta = path.with_name(path.name + '.meta')
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8-sig'), re.MULTILINE)
    if match is None:
        raise ValueError(f'Unity GUID missing in {meta}')
    return match.group(1)


def label(value):
    value = re.sub(r'^(Classic64|HouseProps|Kitchen|Village|Mine|Urban)_', '', value)
    value = re.sub(r'([a-z])([A-Z])', r'\1 \2', value)
    return re.sub(r'\s+', ' ', re.sub(r'_Visual$', '', value).replace('_', ' ')).strip()


def result(view, group, subgroup, role='Scene prop', tags=(), decision='', note=''):
    return dict(view=view, group=group, subgroup=subgroup, role=role, tags=set(tags), decision=decision, decision_note=note)


def env(group, subgroup, role='Scene prop', tags=(), **kw):
    return result('Environment', group, subgroup, role, tags, **kw)


def technical(group, subgroup, role='Dependency', **kw):
    return result('Technical & work in progress', group, subgroup, role, **kw)


def gameplay(parts, stem):
    owner = parts[0]
    if owner == 'Doors':
        if stem in ('Door_Base', 'Door_Placeholder'):
            return technical('Gameplay internals', 'Door templates', 'Authoring template')
        subgroup = 'Keypad doors' if 'Keypad' in stem else 'Gates' if 'Gate' in stem else 'Interactive doors'
        return result('Gameplay', 'Doors & access', subgroup, 'Interactive prefab')
    if owner == 'Enemies':
        if 'Test' in stem:
            return technical('Gameplay internals', 'Test spawners', 'Test helper')
        if stem == 'BruteAcidPool':
            return technical('Gameplay internals', 'Enemy attack effects', 'Runtime-spawned effect')
        return result('Gameplay', 'Enemies', parts[1], 'Enemy instance')
    if owner == 'Items':
        if stem.endswith('_Pickup'):
            return result('Gameplay', 'Pickups & items', 'World pickups', 'World pickup')
        if stem.endswith('_Examination'):
            return technical('Gameplay internals', 'Item examination', 'Examination model')
        return technical('Gameplay internals', 'Pickup effects & triggers', 'Runtime helper')
    if owner == 'Weapons':
        ammo = parts[1] == 'Ammunition'
        if stem.endswith('_Pickup'):
            return result('Gameplay', 'Weapons & ammunition', 'Ammo pickups' if ammo else 'Weapon pickups', 'World pickup')
        if stem.endswith('_Drop'):
            return technical('Gameplay internals', 'Ammo drops', 'Runtime loot drop')
        subgroup = 'Examination models' if '_Examination' in stem else 'Weapon effects' if 'Effects' in parts else 'Held weapons & models'
        return technical('Gameplay internals', subgroup)
    if owner == 'Characters':
        if stem.endswith('_Visual'):
            return technical('Gameplay internals', 'Player visuals')
        return result('Gameplay', 'Player setup', 'Player controller', 'Open / inspect')
    if owner == 'UI':
        return result('Surfaces & presentation', 'UI', parts[1], 'Open / inspect')
    if owner != 'Levels':
        raise ValueError(f'No proposal function for {owner}')
    chapter = re.sub(r'([a-z])(?=[A-Z0-9])', r'\1 ', parts[1])
    if parts[1:3] == ('Chapter1', 'Assets') and stem in REUSABLE_CHAPTER_PROPS:
        group, subgroup, tags = REUSABLE_CHAPTER_PROPS[stem]
        return env(group, subgroup, tags=(*tags, chapter))
    return result('Outside browser', chapter, 'Chapter-specific prefabs', 'Level-owned only', (chapter,))


def pack_environment(a, scale_role):
    pack, subject, category = a['pack'], a['source_object'], a['category']
    asset_id = a['id']
    name, scaled = normalize(subject), normalize(scale_role)
    if asset_id == 'P0436':
        return env('Furniture', 'Coat racks', tags=('Entryway',))
    if asset_id == 'P1124':
        return env('Storage', 'Pallets', tags=('Sewer', 'Stockroom'))
    if asset_id == 'P0182':
        return env('Furniture', 'Beds & mattresses', tags=('Bedroom',))
    if category == 'Signs and posters':
        return env('Signs & graphics', 'Posters & signs', tags=('Metro',) if pack == 'MetroPSX' else ('Street',))
    if pack == 'Pizzeria' and a['id'] in PIZZERIA_REVIEWED:
        group, subgroup, title = PIZZERIA_REVIEWED[a['id']]
        if a['id'] in ('P0772', 'P0924'):
            return technical('Prefab components', 'Restaurant fixture review', 'Hold for visual review', decision='Identity',
                             note=f'{title} in the saved render. Confirm its intended use before promoting it to Environment.')
        return env(group, subgroup, tags=('Restaurant', 'Street') if group == 'Buildings' else ('Restaurant',))
    if category == 'Exterior buildings':
        return env('Buildings', 'Industrial buildings' if pack == 'IndustrialExterior' else 'Urban exteriors', tags=('Street',))
    if category == 'Vegetation':
        return env('Nature & debris', 'Vegetation', tags=('Outdoors',))
    if category == 'Vehicles' or subject == 'subway_car':
        return env('Street & transport', 'Rail vehicles' if pack == 'MetroPSX' else 'Road vehicles', tags=('Metro',) if pack == 'MetroPSX' else ('Street',))
    if pack == 'Sewer':
        if category == 'Sewer modules':
            subgroup = 'Pipe modules' if name.startswith('pipe') else 'Tunnel modules'
            return env('Infrastructure kits', subgroup, 'Kit module', ('Sewer',))
        if name.startswith(('trash', 'debris', 'garbage', 'tire', 'brick')):
            return env('Nature & debris', 'Litter & debris', tags=('Sewer',))
        return env('Decor & clutter', 'Utility clutter', tags=('Sewer',))
    if pack == 'MetroPSX':
        if name.startswith(('railing', 'stairs', 'st', 'estruc', 'column')):
            return env('Infrastructure kits', 'Station modules', 'Kit module', ('Metro',))
        if name.startswith(('chair', 'seats')):
            return env('Furniture', 'Public seating', tags=('Metro',))
        if any(word in name for word in ('ticket', 'vending')):
            return env('Commercial & built-in fixtures', 'Station equipment', tags=('Metro',))
        if name == 'metro map':
            return env('Signs & graphics', 'Wayfinding', tags=('Metro',))
        if name == 'trash can':
            return env('Waste & recycling', 'Bins', tags=('Metro',))
        return env('Fixtures & utilities', 'Station lighting', tags=('Metro',))
    if pack == 'IndustrialExterior':
        if name.startswith('bags'):
            return env('Decor & clutter', 'Bagged supplies', tags=('Industrial Yard',))
        if name == 'roadblock':
            return env('Street & transport', 'Barriers', tags=('Industrial Yard',))
        if name == 'generator':
            return env('Tools & equipment', 'Generators', tags=('Industrial Yard',))
        return env('Infrastructure kits', 'Industrial structures & services', 'Kit module', ('Industrial Yard',))
    if pack == 'Office':
        if name.startswith('divider'):
            if re.fullmatch(r'divider full [ch](?: lue| red)? desk(?: blue| red)?', name):
                return env('Furniture', 'Desks', tags=('Office',))
            if name.startswith(('divider veneer', 'divider wood')):
                return env('Furniture', 'Office dividers', tags=('Office',))
            return env('Buildings', 'Modular office partitions', 'Kit module', ('Office',))
        if re.search(r'^(desk\d|conference desk)', name):
            return env('Furniture', 'Desks', tags=('Office',))
        if re.search(r'chair|desk|armchair|couch|beanbag', name) and not re.search(r'lamp|phone|desktop', name):
            return env('Furniture', 'Office furniture', tags=('Office',))
        if re.search(r'computer|desktop|phone|\btv\b', name):
            return env('Appliances & electronics', 'Office electronics', tags=('Office',))
        if 'lamp' in name:
            return env('Fixtures & utilities', 'Lighting fixtures', tags=('Office',))
        if name.startswith('glass'):
            return env('Food & kitchenware', 'Tableware', tags=('Office', 'Kitchen'))
        return env('Decor & clutter', 'Office supplies', tags=('Office',))
    if pack == 'Pizzeria':
        if 'streetlight' in name or 'traffic light' in name:
            return env('Street & transport', 'Street lighting & signals', tags=('Street',))
        if name.startswith('trash'):
            return env('Waste & recycling', 'Bins & litter', tags=('Restaurant',))
        if name.startswith('chair table'):
            return env('Furniture', 'Dining sets', tags=('Restaurant',))
        if name.startswith('phone'):
            return env('Appliances & electronics', 'Phones', tags=('Restaurant',))
        if name.startswith('washbasin'):
            return env('Fixtures & utilities', 'Plumbing fixtures', tags=('Restaurant',))
        if name.startswith('money'):
            return env('Commercial & built-in fixtures', 'Checkout props', tags=('Restaurant',))
    if pack == 'ModelsPackPSX':
        if 'compressed' in scaled or 'debris' in scaled:
            return env('Nature & debris', 'Litter & debris', tags=('Industrial Yard',))
        if re.search(r'skip|recycling|wastebasket|waste bin', scaled):
            return env('Waste & recycling', 'Bins & skips', tags=('Industrial Yard',))
        if re.search(r'\btoilet\b|sink|faucet|vanity|washing|toilet paper', scaled):
            return env('Fixtures & utilities', 'Plumbing fixtures', tags=('Bathroom',))
        if re.search(r'fan|ventilation|meter|vent fitting|\blight\b|utility pole', scaled):
            return env('Fixtures & utilities', 'Lighting, ventilation & utilities')
        if a['id'] == 'P0435':
            return technical('Prefab components', 'Lighting parts', 'Lamp shade')
        if re.search(r'\bbed\b|mattress|chair|sofa|\bdesk\b|workbench|\btable\b', scaled) and not re.search(r'paper|platter|tableware', scaled):
            if re.search(r'bed|mattress', scaled):
                return env('Furniture', 'Beds & mattresses', tags=('Bedroom',))
            if 'writing desk' in scaled:
                return env('Furniture', 'Desks', tags=('Office',))
            return env('Furniture', 'Seating & tables')
        if 'television' in scaled:
            return env('Appliances & electronics', 'Televisions')
        if re.search(r'cushion|pillow|folder|compact disc|aquarium|hook rail', scaled):
            return env('Decor & clutter', 'Household decoration')
        if 'ornamental tree' in scaled:
            return env('Nature & debris', 'Vegetation')
    return env('Needs classification', 'Unresolved imported subject', 'Hold for visual identification',
               decision='Identity', note='Existing pack and scale records do not identify a confident browser subject.')


def named_environment(parts, stem):
    folder, name = parts[1], normalize(stem)
    if folder == 'ElectricalBox' and stem == 'ElectricityBox':
        return env('Interactables', 'Electrical cabinets',
                   tags=('Electrical', 'Industrial Yard', 'Interactable'),
                   note='Use the local lever to switch its assigned power feeds. The Antenna option controls the visual attachment.')
    if folder == 'ElectricalDevices' and stem in {'ControlButton', 'IntercomSpeaker', 'KeycardReader', 'SecurityScreen'}:
        return env('Interactables', 'Electrical controls',
                   tags=('Electrical', 'Interactable', 'Antenna', 'Post Mount'),
                   note='Use Antenna and Post Mount on the Device Indicator to show or hide the antenna and enable freestanding placement. The antenna indicator is controlled separately.')
    if folder == 'ElectricalDevices' and stem == 'DevicePost':
        return technical('Prefab components', 'Electrical device mounts', 'Mount dependency')
    if folder == 'Fences' and stem == 'Urban_WoodenFencePile':
        return env('Nature & debris', 'Fence debris', tags=('Street', 'Outdoors'))
    direct = {
        'NeonSigns': ('Signs & graphics', 'Neon signs', 'Scene prop', ('Street', 'Neon')),
        'FillerBuildings': ('Buildings', 'Background buildings', 'Scene prop', ('Street',)),
        'FireEscapes': ('Buildings', 'Fire escape kit', 'Kit module', ('Street',)),
        'Fences': ('Buildings', 'Fences & boundary walls', 'Kit module', ('Street', 'Outdoors')),
        'RoadSurfaces': ('Infrastructure kits', 'Roads, bridges & sidewalks', 'Kit module', ('Street',)),
        'Windows': ('Buildings', 'Window kit', 'Kit module', ()),
        'WallFamilies': ('Buildings', 'Storefront, wall & gate kits', 'Kit module', ('Street',)),
        'Stairs': ('Buildings', 'Stair modules', 'Kit module', ()),
        'Pipes': ('Infrastructure kits', 'Pipes & valves', 'Kit module', ('Industrial Yard',)),
        'StorageBoxes': ('Storage', 'Boxes', 'Scene prop', ('Stockroom',)),
        'MineEquipment': ('Tools & equipment', 'Mining equipment', 'Scene prop', ('Mine',)),
        'Nature': ('Nature & debris', 'Rocks & ground clutter', 'Scene prop', ('Outdoors',)),
        'Vegetation': ('Nature & debris', 'Vegetation', 'Scene prop', ('Outdoors',)),
    }
    if folder in direct:
        if folder == 'StorageBoxes' and stem.endswith('_NoCollision'):
            return technical('Prefab components', 'Non-collision variants', 'Visual prefab')
        return env(*direct[folder])
    if folder == 'Buildings':
        return (env('Buildings', 'Modular urban walls', 'Kit module', ('Street',)) if 'Modular' in parts else
                env('Buildings', 'Complete buildings', tags=('Street',)))
    if folder == 'GarageDoors':
        if 'Operable' in stem:
            return result('Gameplay', 'Doors & access', 'Garage doors', 'Interactive prefab')
        return env('Buildings', 'Decorative garage doors', tags=('Street',))
    if folder == 'PushableCrate':
        role = {'PushableCrate1': 'Push and climb', 'PushableCrate1.5': 'Push only'}.get(stem, 'Interactive prefab')
        return result('Gameplay', 'Interactables', 'Pushable crates', role, ('Stockroom',))
    if folder == 'PSXShelvesProps':
        if 'Lighting' in parts and re.fullmatch(r'CeilingLight_\d+_(Normal|Horror)', stem):
            return env('Fixtures & utilities', 'Lighting fixtures', tags=(stem.rsplit('_', 1)[1],))
        if 'DisplayGeometry' in parts or 'Lighting' in parts:
            subgroup = 'Shelf showroom geometry' if 'DisplayGeometry' in parts else 'Shelf showroom lighting'
            return technical('Showroom & authoring', subgroup, 'Demo component')
        if 'Books' in parts:
            return env('Decor & clutter', 'Books', tags=('Office',))
        return env('Storage', 'Shelving', tags=('Stockroom',))
    if folder == 'RoadFurniture':
        if re.search(r'\bsign|graffiti\b', name):
            return env('Signs & graphics', 'Street signs & graphics', tags=('Street',))
        return env('Street & transport', 'Roadside furniture', tags=('Street',))
    if folder == 'StorageContainers':
        if re.search(r'dumpster lid ?[12]$', name):
            return technical('Prefab components', 'Dumpster lids', 'Loose part')
        if re.search(r'trash\s*(pile|bag)|garbage\s*bag|litter', name):
            return env('Nature & debris', 'Litter & debris', tags=('Street',))
        if re.search(r'dumpster|trash\s*(can|bin)', name):
            return env('Waste & recycling', 'Bins & dumpsters', tags=('Street',))
        return env('Storage', 'Containers & pallets', tags=('Stockroom',))
    if folder == 'Vehicles':
        if stem == 'Cars_Wheel':
            return technical('Prefab components', 'Vehicle parts', 'Loose part')
        if 'Ramp' in stem:
            return env('Tools & equipment', 'Vehicle maintenance', tags=('Garage',))
        return env('Street & transport', 'Road vehicles', tags=('Street',))
    if folder == 'Lighting':
        if stem == 'Floodlight':
            return env('Fixtures & utilities', 'Lighting fixtures', tags=('Industrial', 'Outdoors', 'Spotlight'))
        if stem in ('AtmosphericFog', 'Fire'):
            return result('Surfaces & presentation', 'Effects', 'Atmospheric effects', 'Scene effect')
        if stem in ('EmergencyRed', 'MoonWindow'):
            return result('Surfaces & presentation', 'Lighting', 'Lighting rigs', 'Scene light')
        return env('Fixtures & utilities', 'Lighting fixtures')
    if folder in ('AppliancesElectronics', 'InteriorFixtures', 'LightingElectrical'):
        if stem == 'Classic64_Building01_Visual':
            return env('Buildings', 'Complete buildings', tags=('Street',))
        if re.search(r'panel|generator|ventilat|junction box|power socket|thermostat|radiator|duct|vent cover', name):
            return env('Fixtures & utilities', 'Electrical & mechanical services')
        if re.search(r'light|lamp|farol|fan|focus', name):
            return env('Fixtures & utilities', 'Lighting & fans')
        if re.search(r'sink|bathtub|toilet|extinguisher', name):
            return env('Fixtures & utilities', 'Plumbing & safety fixtures', tags=('Bathroom',) if 'HouseProps' in stem else ())
        if re.search(r'carpet|doormat', name):
            return env('Decor & clutter', 'Rugs & mats')
        return env('Appliances & electronics', 'Household devices')
    if folder == 'Furniture':
        subgroup = 'Beds' if re.search(r'\bbed\d*\b', name) else 'Seating & tables'
        return env('Furniture', subgroup, tags=('Bedroom',) if subgroup == 'Beds' else ())
    if folder == 'HouseholdDecor':
        subgroup = 'Books & papers' if re.search(r'book|paper|catalog', name) else 'Household decoration'
        return env('Decor & clutter', subgroup)
    if folder == 'ToolsEquipment':
        return env('Tools & equipment', 'Hand tools & workshop equipment')
    return env('Needs classification', 'Unresolved environment subject', 'Hold for visual identification',
               decision='Identity', note='No confident subject rule for this path.')


def variant_review(parts, stem, quality):
    folder = parts[1]
    if parts[0] == 'Doors' and stem.startswith(('Door_Exterior', 'Door_Interior')):
        return 'Door variants'
    if parts[0] != 'Environment':
        return ''
    if quality.get('status') == 'BackgroundOnly' and quality.get('category') == 'Shop displays':
        return 'Baked retail displays'
    if folder == 'Fences':
        return 'Urban fences'
    if folder == 'RoadSurfaces':
        return 'Urban roads'
    if folder == 'PSXShelvesProps' and 'Shelving' in parts:
        return 'PSX shelving'
    if folder == 'Office' and quality.get('source_object', '').startswith('divider'):
        return 'Office partitions'
    if folder == 'Vehicles' and stem.startswith('Cars_Car'):
        return 'Car prefabs'
    if folder == 'StorageContainers':
        if 'Dumpsters' in parts:
            return 'Urban dumpsters'
        if 'MetalTrashCans' in parts or 'TrashCans' in parts:
            return 'Urban trash cans'
    return ''


def prefab_inventory(pilot=None, apply_overrides=True):
    if pilot is None:
        pilot, _ = build_pilot_catalogue()
    live = {a['guid'] for a in read_json(CATALOGUE)['entries']} if CATALOGUE.exists() else set()
    entries = {a['guid']: a for a in pilot['entries']}
    overrides_path = CATALOGUE.with_name('Overrides.json')
    if apply_overrides and overrides_path.exists():
        entries = {guid: dict(entry) for guid, entry in entries.items()}
        for override in read_json(overrides_path)['entries']:
            if override['guid'] in entries:
                entries[override['guid']].update(override)
    quality = {a['guid']: a for a in read_json(ART / 'DownloadedEnvironmentPacks/QualityCatalog.json')['assets'] if a['status'] != 'Removed'}
    subjects = read_json(MODELS_PACK_SUBJECTS)['subjects']
    calibrated = {a['guid']: a['role'] for a in read_json(ART / 'ScaleCalibration/ScalePlan.json')['assets'] if not a.get('dimension_only')}
    rows = []
    for path in sorted(GAME.rglob('*.prefab')):
        parts = path.relative_to(GAME).parts
        guid = guid_of(path)
        a, known = quality.get(guid, {}), entries.get(guid)
        scale_role = calibrated.get(guid, '')
        if known:
            parent, _, sub = known['category'].partition('/')
            row = env(PILOT_GROUPS.get(parent, label(parent)), label(sub or parent), tags=known['tags'])
            display = known['name']
            evidence = 'Existing Asset Library classification'
        elif parts[0] != 'Environment':
            row = gameplay(parts, path.stem)
            display = label(path.stem)
            evidence = 'Owned feature folder and prefab role'
            if row['view'] == 'Environment':
                evidence = 'Identified reusable chapter prop with no script or nested prefab references'
        elif a:
            if a['id'] in REVIEWED_PACK_PROPS:
                group, subgroup, display, tags = REVIEWED_PACK_PROPS[a['id']]
                row = env(group, subgroup, tags=tags)
                evidence = 'Saved prefab render confirms standalone fixture'
            else:
                row = pack_environment(a, scale_role)
                reviewed = PIZZERIA_REVIEWED.get(a['id'])
                rendered_name = reviewed[2] if reviewed else OTHER_RENDER_SUBJECTS.get(a['id'])
                display = rendered_name or scale_role or label(a['source_object'])
                if a['pack'] == 'Pizzeria' and scale_role in ('Pizzeria streetlight', 'Pizzeria traffic signal'):
                    display = label(a['source_object'])
                evidence = 'Saved prefab front render' if rendered_name else 'Scale audit role and download quality review' if scale_role else 'Source name and download quality review'
                if a['pack'] == 'Office' and a['source_object'].startswith('divider') and row['group'] == 'Furniture':
                    kind = 'Cubicle desk' if row['subgroup'] == 'Desks' else 'Freestanding office divider'
                    subject = label(a['source_object']).replace('lue desk', 'blue desk')
                    profile = re.sub(r'^divider (?:full )?|\bdesk\b', '', subject)
                    display = f"{kind} ({' '.join(profile.split()).title()})"
                    evidence = 'Saved prefab render shows a work surface or freestanding feet'
        else:
            row = named_environment(parts, path.stem)
            display = label(path.stem)
            evidence = 'Owned family folder and prefab name'
            if path.stem == 'Classic64_Building01_Visual':
                evidence = 'Classic64 finished geometry review documents this complete textured building'
            elif parts[1] == 'PushableCrate':
                evidence = 'Crate documentation and both prefab GUIDs referenced in Electricity Puzzle Dom'
            elif parts[1] == 'NeonSigns':
                design, version = path.stem.split('_', 1)
                display = f'{label(design)} ({label(version)})'
                evidence = 'Animated pixel art from the Pixel Art Sign Pack'
            elif path.stem == 'Urban_WoodenFencePile':
                display = 'Wooden fence debris pile'
                evidence = 'Source mesh and scale audit identify a low pile of discarded fence boards'
            elif parts[1] == 'PSXShelvesProps' and 'Lighting' in parts and row['view'] == 'Environment':
                evidence = 'Fixture prefab includes an imported fixture mesh and child spotlight'
        status = known['status'] if known else {'Keep': 'Candidate', 'BackgroundOnly': 'Background', 'Review': 'Review'}.get(a.get('status'), 'Unreviewed')
        subject = subjects.get(a.get('id'))
        if subject:
            if 'category' in subject:
                group, subgroup = subject['category'].split('/', 1)
                row = env(group, subgroup, tags=subject.get('tags', row['tags']))
            display = f"{subject['name']} · {a['id']}"
            evidence = 'Opposing Unity renders of the current prefab'
            if subject.get('review'):
                status = 'Review'
            row['decision_note'] = subject.get('note', row['decision_note'])
        variants = variant_review(parts, path.stem, a)
        row.update(guid=guid, prefab=path.relative_to(PROJECT).as_posix(), name=display, status=status,
                   review_note=' '.join(a.get('reasons', [])[:1]) if status in ('Review', 'Background') else '',
                   tags='; '.join(sorted(row['tags'])), source_pack=a.get('pack', parts[1] if len(parts) > 2 else parts[0]),
                   review_id=a.get('id', ''), scale_subject=scale_role, evidence=evidence, current_browser='Yes' if guid in live else 'No',
                   variant_review=variants, variant_note=VARIANT_NOTES.get(variants, ''))
        rows.append(row)
    if len({r['guid'] for r in rows}) != len(rows):
        raise ValueError('Duplicate prefab GUIDs in owned inventory.')
    if set(entries).difference(r['guid'] for r in rows):
        raise ValueError('The current Asset Library has entries outside the owned prefab inventory.')
    if {r['decision'] for r in rows if r['decision']}.difference(DECISIONS):
        raise ValueError('Decision row without an explanation.')
    return rows


def prop_exclusion(row):
    if row['group'] == 'Needs classification':
        return 'Needs classification'
    if row['view'] != 'Environment':
        return row['view']
    if row['role'] != 'Scene prop' or row['group'] not in PROP_GROUPS:
        return 'Buildings & construction'
    if row['group'] == 'Buildings' and row['subgroup'] not in PLACEABLE_BUILDING_SUBGROUPS:
        return 'Buildings & construction'
    stem = Path(row['prefab']).stem
    if stem.startswith('Urban_Duct') or stem.endswith('_automatic_ticket_gate'):
        return 'Buildings & construction'
    return ''


def publish_catalogue(pilot, unmatched):
    rows = prefab_inventory(pilot, apply_overrides=False)
    existing = {entry['guid']: entry for entry in pilot['entries']}
    quality = {a['guid']: a for a in read_json(ART / 'DownloadedEnvironmentPacks/QualityCatalog.json')['assets'] if a['status'] != 'Removed'}
    sizes = {a['guid']: a['target_size_m'] for a in read_json(ART / 'ScaleCalibration/ScalePlan.json')['assets']}
    aliases, entries, excluded = {}, [], Counter()
    for row in rows:
        reason = prop_exclusion(row)
        if reason:
            excluded[reason] += 1
            continue
        guid = row['guid']
        base, reviewed = existing.get(guid, {}), quality.get(guid, {})
        group = PROP_GROUPS[row['group']]
        category = f"{group}/{row['subgroup']}"
        if group != row['group']:
            aliases[row['group']] = group
            aliases[f"{row['group']}/{row['subgroup']}"] = category
        old_category = base.get('category')
        if old_category and old_category != category:
            aliases[old_category] = category
            aliases[old_category.split('/')[0]] = group
        name = row['name'].strip()
        original = base.get('original', reviewed.get('source_object', Path(row['prefab']).stem))
        tags = row['tags'].split('; ') if row['tags'] else []
        note = base.get('note') or row['review_note']
        if group == 'Buildings':
            tags = sorted(set(tags) | {'Exterior'})
            if row['source_pack'] == 'Pizzeria':
                note = 'Street-facing exterior for set dressing.'
        entries.append(dict(guid=guid, name=name[:1].upper() + name[1:], category=category, view=row['view'],
                            role=row['role'], action='Place',
                            tags=tags, status=row['status'],
                            note=' '.join(filter(None, [note, row['decision_note']])),
                            path=row['prefab'], source=base.get('source', row['source_pack']), original=original,
                            size=sizes.get(guid, base.get('size', reviewed.get('bounds_m', []))), classification=row['evidence']))
    canonical = {value for e in entries for value in (e['category'], e['category'].split('/')[0])}
    families = assign_families(entries)
    browse_categories = sorted({e['category'].split('/')[0] for e in entries})
    catalogue = dict(version=1, browseCategories=browse_categories, scope=PROP_COLLECTIONS,
                     statuses=pilot['statuses'], synonyms=pilot['synonyms'],
                     categoryAliases=[dict(previous=key, current=value) for key, value in sorted(aliases.items()) if key not in canonical],
                     entries=entries)
    CATALOGUE.write_text(json.dumps(catalogue, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    report = dict(total=len(entries), variant_families=len(families), grouped_variants=sum(len(v) for v in families.values()),
                  browse_categories=dict(sorted(Counter(e['category'].split('/')[0] for e in entries).items())),
                  collections={tag: sum(tag in e['tags'] for e in entries) for tag in PROP_COLLECTIONS},
                  project_browser_only=dict(sorted(excluded.items())),
                  categories=dict(sorted(Counter(e['category'] for e in entries).items())), statuses=dict(Counter(e['status'] for e in entries)),
                  excluded_level_prefabs=[r['prefab'] for r in rows if r['view'] == 'Outside browser'], unmatched_pilot_subjects=unmatched)
    (ART / 'AssetLibrary/ClassificationReceipt.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(f'Published {len(entries):,} prefabs in {len(browse_categories)} broad categories. {sum(excluded.values()):,} prefabs remain outside the shelf.')


def write_csv(path, rows):
    fields = ('view', 'group', 'subgroup', 'role', 'name', 'status', 'review_note', 'tags', 'decision', 'decision_note', 'variant_review',
              'variant_note', 'current_browser', 'shelf_status', 'shelf_reason', 'source_pack', 'review_id', 'scale_subject',
              'evidence', 'guid', 'prefab')
    with path.open('w', newline='', encoding='utf-8-sig') as output:
        writer = csv.DictWriter(output, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def shelf_coverage(rows, entries):
    """Compare the actual published GUIDs with the current asset inventory and scope policy."""
    owned = {row['guid']: row for row in rows}
    live = {entry['guid']: entry for entry in entries}
    missing, unexpected, unclassified = [], [], []
    exclusions = Counter()
    for guid, row in owned.items():
        reason = prop_exclusion(row)
        if reason == 'Needs classification':
            unclassified.append(row['prefab'])
        if reason:
            exclusions[reason] += 1
        if reason and guid in live:
            unexpected.append(row['prefab'])
            row.update(shelf_status='Unexpected shelf entry', shelf_reason=reason)
        elif not reason and guid not in live:
            missing.append(row['prefab'])
            row.update(shelf_status='Missing eligible prefab', shelf_reason='Qualifies for the Asset Library')
        elif reason:
            row.update(shelf_status='Project window only', shelf_reason=reason)
        else:
            row.update(shelf_status='In Asset Library', shelf_reason='Placeable environment prefab')

    unknown_guids = sorted(live.keys() - owned.keys())
    duplicate_guids = sorted(guid for guid, count in Counter(entry['guid'] for entry in entries).items() if count > 1)
    stale_paths = [dict(guid=guid, catalogue=live[guid]['path'], actual=row['prefab'])
                   for guid, row in owned.items() if guid in live and live[guid]['path'] != row['prefab']]
    return dict(verified=not any((missing, unexpected, unclassified, unknown_guids, duplicate_guids, stale_paths)),
                owned_prefabs=len(owned), shelf_entries=len(entries), intended_exclusions=dict(sorted(exclusions.items())),
                missing_eligible=missing, unexpected_on_shelf=unexpected, needs_classification=unclassified,
                unknown_guids=unknown_guids, duplicate_catalogue_guids=duplicate_guids, stale_paths=stale_paths)


def supporting_assets():
    types = {
        '.mat': 'Materials', '.wav': 'Audio', '.mp3': 'Audio', '.ogg': 'Audio', '.aiff': 'Audio', '.unity': 'Scenes',
        '.png': 'Textures & images', '.jpg': 'Textures & images', '.exr': 'Textures & images', '.jpeg': 'Textures & images',
        '.fbx': 'Source models', '.obj': 'Source models', '.cs': 'Scripts', '.shader': 'Shaders', '.shadergraph': 'Shaders',
        '.hlsl': 'Shaders', '.anim': 'Animation & controllers', '.controller': 'Animation & controllers',
        '.overridecontroller': 'Animation & controllers', '.ttf': 'Fonts', '.otf': 'Fonts',
    }
    counts = Counter()
    shared_materials = 0
    for path in GAME.rglob('*'):
        if not path.is_file():
            continue
        suffix = path.suffix.lower()
        kind = types.get(suffix)
        if suffix == '.asset':
            with path.open('rb') as asset:
                head = asset.read(1024)
            kind = 'Serialized meshes' if re.search(rb'^Mesh:', head, re.M) else 'Serialized data & other .asset files'
        if kind:
            counts[kind] += 1
        if suffix == '.mat' and path.relative_to(GAME).parts[:2] in {
            ('Environment', name) for name in ('MasonrySurfaces', 'ProjectedFinishes', 'Roads', 'UrbanSurfaces')
        }:
            shared_materials += 1
    return counts, shared_materials


def report(rows):
    views = Counter(r['view'] for r in rows)
    groups = Counter((r['view'], r['group']) for r in rows)
    subgroups = Counter((r['view'], r['group'], r['subgroup']) for r in rows)
    decisions = Counter(r['decision'] for r in rows if r['decision'])
    variants = Counter(r['variant_review'] for r in rows if r['variant_review'])
    supporting, material_shortlist = supporting_assets()
    current = sum(r['current_browser'] == 'Yes' for r in rows)
    outside = views['Outside browser']
    reusable_from_levels = sum(r['view'] == 'Environment' and r['prefab'].startswith('Assets/Game/Levels/') for r in rows)
    nature_review = sum(r['group'] == 'Nature & debris' and r['status'] == 'Review' for r in rows)
    lines = ['# Whole-project Asset Library proposal', '',
             f'**{len(rows):,} owned prefabs** inventoried by GUID. The current browser has {current:,}. '
             f'The Asset Library includes {sum(not prop_exclusion(r) for r in rows):,} placeable environment prefabs. '
             f'The {outside:,} chapter-specific prefabs and other out-of-scope assets remain in this inventory and Unity’s Project browser.', '',
             '`PrefabInventory.csv` has the full item-level proposal. `DecisionQueue.csv` lists the exact prefabs needing a decision. '
             '`VariantReview.csv` lists the separate variant consolidation work.', '',
             '## Whole-project classifications', '', '| Inventory area | Prefabs |', '| --- | ---: |']
    lines.extend(f'| {view} | {count:,} |' for view, count in views.most_common())
    lines.extend(['', f'{reusable_from_levels:,} reusable props from chapter folders are proposed for Environment. '
                  f'Foliage and debris remain accessible for hand placement, including {nature_review:,} with Review status. '
                  'A future procedural placement tool can consume the same reviewed families.', '',
                  'Shelf showroom pieces and runtime ammo drops belong in Technical. The player controller belongs in Gameplay for inspection. '
                  'Both pushable crates are already used in a level. The 1 m climbable crate is the suggested first choice, '
                  'with the 1.5 m push-only crate listed separately because its behaviour differs.'])
    lines.extend(['', '| View | Group | Prefabs |', '| --- | --- | ---: |'])
    lines.extend(f'| {view} | {group} | {count:,} |' for (view, group), count in sorted(groups.items()))
    lines.extend(['', '## Decisions before publishing', '', '| Decision | Prefabs | Proposed disposition |', '| --- | ---: | --- |'])
    lines.extend(f'| {title} | {decisions[key]:,} | {note} |' for key, (title, note) in DECISIONS.items() if decisions[key])
    lines.extend(['', f'{sum(decisions.values()):,} open decision rows. Each records its evidence and the individual question.', '',
                  '## Variant families', '', '| Family review | Prefabs | Proposed grouping |', '| --- | ---: | --- |'])
    lines.extend(f'| {key} | {count:,} | {VARIANT_NOTES[key]} |' for key, count in variants.most_common())
    lines.extend(['', 'Variant rows can overlap the decision queue. Family cards should retain separate GUIDs and preserve search hits on child variants.', '',
                  '## Supporting assets', '', 'These are raw counts under `Assets/Game`. They define follow-on review scope, and are separate from the prefab totals.', '',
                  '| Asset type | Files |', '| --- | ---: |'])
    lines.extend(f'| {key} | {count:,} |' for key, count in supporting.most_common())
    lines.extend(['', f'{material_shortlist:,} materials are in the shared masonry, projected finish, road and urban surface families. '
                  'Start a reusable material shortlist there. Review tiling versus atlases and shared finishes versus private model materials before exposing Apply. '
                  'Review audio as reusable cues versus feature dependencies. Preserve scenes, lighting data, models, textures, scripts and authored settings as distinct types.', '',
                  'Sandbox and vendor prefabs are outside the owned count. They can be inventoried separately as opt-in views. '
                  'Removed pack objects are outside the live prefab inventory. Existing quality statuses are retained, and All remains the current status default.', '',
                  '## Subgroup counts', '', '| View | Group | Subgroup | Prefabs |', '| --- | --- | --- | ---: |'])
    lines.extend(f'| {view} | {group} | {subgroup} | {count:,} |' for (view, group, subgroup), count in sorted(subgroups.items()))
    (OUTPUT / 'InventoryReport.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'{len(rows):,} owned prefabs. {current:,} already in the browser.')
    print('Views:', dict(views.most_common()))
    print('Decisions:', dict(decisions.most_common()))
    print('Variant review:', dict(variants.most_common()))
    print('Related asset counts:', dict(supporting.most_common()))


def main():
    rows = sorted(prefab_inventory(), key=lambda r: (r['view'], r['group'], r['subgroup'], r['name'].casefold(), r['guid']))
    coverage = shelf_coverage(rows, read_json(CATALOGUE)['entries'])
    OUTPUT.mkdir(parents=True, exist_ok=True)
    write_csv(OUTPUT / 'PrefabInventory.csv', rows)
    write_csv(OUTPUT / 'DecisionQueue.csv', sorted((r for r in rows if r['decision']), key=lambda r: (r['decision'], r['prefab'])))
    write_csv(OUTPUT / 'VariantReview.csv', sorted((r for r in rows if r['variant_review']), key=lambda r: (r['variant_review'], r['prefab'])))
    (OUTPUT / 'ShelfCoverage.json').write_text(json.dumps(coverage, indent=2) + '\n', encoding='utf-8')
    report(rows)
    print(f"Shelf coverage: {'PASS' if coverage['verified'] else 'FAIL'}. See {OUTPUT / 'ShelfCoverage.json'}")
    if not coverage['verified']:
        raise SystemExit(1)


if __name__ == '__main__':
    main()
