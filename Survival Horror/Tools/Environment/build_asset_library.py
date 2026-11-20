"""Build the project placement catalogue using the original pilot classification rules."""
from pathlib import Path
import json
import re
import unicodedata

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Environment/AssetLibrary'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def normalize(text):
    text = re.sub(r'([a-z])([A-Z])', r'\1 \2', text)
    text = unicodedata.normalize('NFKD', text).encode('ascii', 'ignore').decode().lower()
    return re.sub(r'[^a-z0-9]+', ' ', text).strip()


def classify(name, role):
    """Ordered semantic rules; reviewed scale roles take precedence over opaque FBX names."""
    t = normalize(role or name)
    n = normalize(name)
    def has(pattern):
        return re.search(pattern, t) is not None
    def result(category, tags, title=''):
        return category, sorted(set(tags.split('|'))), title
    if has(r'crate or compressed bale'):
        return result('Storage/Crates', 'Storage|Warehouse|Stockroom', 'Crate or compressed bale')
    if has(r'wrapped pallet'):
        return result('Storage/PalletLoads', 'Storage|Stockroom|Warehouse', role)
    if has(r'\bcrate\b|\bbox\d*\b|carton|cardboard|\bcargo\b|site container|shipping container') and not has(r'pizza|strawberr|chunks|choco|milk'):
        return result('Storage/Containers', 'Storage|Stockroom|Warehouse', role)
    if has(r'\bpalet\b|\bpallet\b|pipe pallet'):
        return result('Storage/Pallets', 'Storage|Stockroom|Warehouse')
    if has(r'barrel|liquid reservoir|propane storage|petrol can|gas cylinder|water tank'):
        return result('Storage/TanksAndBarrels', 'Storage|Warehouse', role)
    if has(r'refrigerated meat display|drinks fridge'):
        return result('Appliances/Refrigeration', 'Grocery Store|Food Display', role)
    if has(r'retail|commercial shelf|commerical shelf|store shelf|snack display|produce display|bread rack'):
        return result('Storage/RetailDisplays', 'Storage|Grocery Store|Food Display', role)
    if has(r'dish drain|plate rack'):
        return result('Kitchenware/DishRacks', 'Kitchen|Storage', role)
    if has(r'pantry shelf|spice rack'):
        return result('Storage/Shelving', 'Storage|Kitchen|Pantry', role)
    if has(r'bookcase|book case|bookshelf|library bay'):
        return result('Storage/Bookcases', 'Storage|Office', role)
    if has(r'rack|shelf|shelves|shelving'):
        return result('Storage/Shelving', 'Storage|Stockroom|Warehouse', role)
    if has(r'file cabinet'):
        return result('Storage/Cabinets', 'Storage|Office')
    if has(r'bathroom cabinet|side bedside cabinet'):
        return result('Storage/Cabinets', 'Storage|' + ('Bathroom' if 'bathroom' in t else 'Bedroom'), role)
    if has(r'wall cabinet|wall cupboard|cabinet high corner|cabinet low corner|cupboard') and not has(r'wardrobe'):
        return result('Storage/Cabinets', 'Storage|Kitchen', role)
    if has(r'cabinet|wardrobe|locker|drawer|storage bin'):
        return result('Storage/Cabinets', 'Storage|' + ('Bedroom' if 'wardrobe' in t else 'Stockroom'), role)
    if has(r'point of sale|cash register|checkout counter'):
        return result('Retail/Checkout', 'Grocery Store|Checkout', role)
    if has(r'shopping cart|shopping basket'):
        return result('Retail/ShoppingEquipment', 'Grocery Store', role)
    if has(r'kitchen work|sink counter|preparation table|shelved kitchen|village kitchen'):
        return result('Fixtures/KitchenCounters', 'Kitchen|Food Preparation', role)
    if has(r'refrigerator|fridge'):
        return result('Appliances/Refrigeration', 'Kitchen|Food Storage', role)
    if has(r'microwave|microondas|\bmicro\b|toaster|blender|licuadora|coffee machine|dough mixer|dough press'):
        return result('Appliances/Countertop', 'Kitchen|Food Preparation', role)
    if has(r'stove|oven|freestanding range|freestanding cooker|range hood'):
        return result('Appliances/Cooking', 'Kitchen|Food Preparation', role)
    if has(r'soda machine|water cooler'):
        return result('Appliances/DrinkDispensers', 'Kitchen|Grocery Store|Drinks', role)
    if has(r'kitchen table|dining table|dining kitchen table') or re.search(r'^kitchen (chair|table)', n):
        return result('Furniture/Dining', 'Kitchen|Dining', role)
    if has(r'cutting board|cheese grater|dough cutter|roller|spatula|tongs|ladle|cooking spoon|knife block'):
        return result('Kitchenware/Utensils', 'Kitchen|Food Preparation', role)
    if has(r'fork|spoon|knife|cutlery|butter knife'):
        return result('Kitchenware/Cutlery', 'Kitchen|Dining', role)
    if has(r'stockpot|saucepan|frying pan|\bpan\b|\bpot\b|\bwok\b|\bolla\b|\bsarten\b'):
        return result('Kitchenware/Cookware', 'Kitchen|Food Preparation', role)
    if has(r'tableware|basins|mugs and metal cookware|serving|\btray\b|\bplate\d*\b|\bdish\b|\bbowl\b|\bcup\d*\b|\bmug\b|\bglass\b|tumbler|napkinring|\bstraw\b|vessel soda') and not has(r'aquarium|bathroom|pedestal|washing tub'):
        return result('Kitchenware/Tableware', 'Kitchen|Dining', role)
    if has(r'detergent|sponge|\bsoap\b|paper ?towels'):
        return result('Household/Cleaning', 'Kitchen|Grocery Store|Cleaning', role)
    if has(r'canned|soup can|food cereal|baking|food snack|food pet|milk carton|\bflour\b|\bsugar\b|\bproduct\d*\b|choco|chunks|\bcan\b') and not has(r'trash|petrol|garbage|can soda|can beer|can cola|can fantac|drink can|soda can'):
        return result('Food/Packaged', 'Kitchen|Grocery Store|Pantry', role)
    if has(r'ketchup|mayonnaise|mustard|olive oil|sauce|ketchu'):
        return result('Food/Condiments', 'Kitchen|Grocery Store|Pantry', role)
    if has(r'beer|soda|soft drinks|milk|bottle|botella|scotch|can cola|can fantac') and not has(r'water cooler bottle attached'):
        return result('Food/Drinks', 'Kitchen|Grocery Store|Drinks', role)
    if has(r'apple|\begg\b|strawberr|\bmeat\b|\bchicken\b|\bham\b|mozzarella|\bcheese\b|mushroom|pineapple|\bpepper\b|\bolives\b|\bbasil\b|pepperoni'):
        return result('Food/Ingredients', 'Kitchen|Grocery Store|Food Preparation', role)
    if has(r'pizza box|box pizza|paper bag'):
        return result('Food/Packaging', 'Kitchen|Food Packaging', role)
    if has(r'pizza|burger|toast|cake|flan|fried foods|ice cream|\bmass\b'):
        return result('Food/Prepared', 'Kitchen|Dining', role)
    # No entire pack is classified as Kitchen/Store just because of its pack name.
    return None


def build_pilot_catalogue():
    quality = read(ROOT / 'ArtSource/Environment/DownloadedEnvironmentPacks/QualityCatalog.json')
    qa = {a['guid']: a for a in quality['assets'] if a['status'] != 'Removed'}
    plan = read(ROOT / 'ArtSource/Environment/ScaleCalibration/ScalePlan.json')
    roles = {a['guid']: a['role'] for a in plan['assets'] if not a.get('dimension_only')}
    sizes = {a['guid']: a['target_size_m'] for a in plan['assets']}
    reviewed = read(SOURCE / 'ReviewedSubjects.json')['subjects']
    entries = []
    excluded = []
    for path in sorted((ROOT / 'Assets/Game/Environment').rglob('*.prefab')):
        rel = path.relative_to(ROOT).as_posix()
        guid = re.search(r'^guid: ([0-9a-f]{32})$', Path(str(path)+'.meta').read_text(), re.M).group(1)
        # These are authoring fragments, not complete placeable subjects.
        if any(x in rel for x in ['/DisplayGeometry/', '/WallFamilies/', '/Windows/', '/Editor/']):
            continue
        if rel.split('/')[3] in {'ElectricalBox', 'ElectricalDevices', 'GarageDoors', 'LightingElectrical', 'NeonSigns', 'Sewer', 'Vehicles'}:
            continue
        if path.stem in {'HouseProps_pencilCup', 'Mine_Tray', 'StorageBox_NoCollision'}:
            continue
        if re.search(r'Dumpster(Lid|Trash)|Trashpile|trash[Bb]ag|GarbageBag|Garbage_bags', path.stem):
            continue
        a = qa.get(guid, {})
        manual = reviewed.get(a.get('id'), {})
        if manual.get('exclude'):
            continue
        role = roles.get(guid, '')
        original = a.get('source_object', path.stem)
        # Strip known provenance prefixes for semantic matching and display.
        subject = re.sub(r'^(Classic64|HouseProps|Kitchen|Village|Mine|Urban)_', '', original)
        subject = re.sub(r'_Visual$', '', subject)
        match_name = original if original.startswith(('Kitchen_chair', 'Kitchen_table', 'Village_Kitchen')) else subject
        result = classify(match_name, role)
        if result is None:
            if any(k in rel for k in ['/FoodKitchenware/', '/NeighborhoodStore/']):
                excluded.append({'path':rel, 'reason':'No specific semantic rule; needs classification'})
            continue
        category, tags, title = result
        category = manual.get('category', category)
        tags = manual.get('tags', tags)
        title = manual.get('name', title)
        # Kitchen-prefixed chair/table lost prefix above; explicit context restores it.
        if original.startswith('Kitchen_') and category.startswith('Storage/'):
            tags = sorted(set(tags + ['Kitchen']))
        if a.get('pack') == 'Pizzeria' and 'Kitchen' in tags:
            tags = sorted(set(tags + ['Restaurant']))
        if category == 'Storage/PalletLoads' or (category == 'Storage/Containers' and re.search(r'carton|cardboard', normalize(role or subject))):
            tags = sorted(set(tags + ['Grocery Store']))
        if '/PSXShelvesProps/' in rel:
            tags = sorted(set(tags + [('Horror' if '/Horror/' in rel else 'Normal')]))
        status = {'Keep':'Candidate', 'Review':'Review', 'BackgroundOnly':'Background'}.get(a.get('status'), 'Unreviewed')
        note = ' '.join(a.get('reasons', [])) if status in ('Review', 'Background') else ''
        if not manual and 'crate or compressed bale' in role.lower():
            status = 'Review'; note = 'Previous visual identification was ambiguous: crate versus compressed waste bale.'
        if not manual and ('basins' in role.lower() or 'mugs and metal cookware' in role.lower()):
            status = 'Review'; note = 'Family identified previously; individual tableware / utility-basin or cookware subtype needs confirmation.'
        display = re.sub(r',.*$', '', title or role or subject)
        display = re.sub(r'^(Matching |Overall )', '', display, flags=re.I)
        display = re.sub(r'([a-z])([A-Z])', r'\1 \2', display).replace('_', ' ').strip()
        display = re.sub(r'\s+', ' ', display)
        if a:
            display += ' · ' + a['id']
        entries.append(dict(guid=guid, name=display[:1].upper()+display[1:], category=category,
                            tags=tags, status=status, note=note, path=rel,
                            source=a.get('pack', original.split('_')[0]),
                            original=original, size=sizes.get(guid, a.get('bounds_m', [])),
                            classification='Individually reviewed subject render' if manual else 'Reviewed subject from scale audit' if role else 'Named subject rule'))
    aliases = [
        ('grocery store',['shop','store','retail','supermarket','grocery','convenience store']),
        ('refrigeration',['fridge','refrigerator','freezer','cold storage']),
        ('shelving',['shelf','shelves','rack','racks']),
        ('cookware',['pan','pans','frying pan','saucepan','pot','pots','wok','olla','sarten']),
        ('cutlery',['silverware','flatware','fork','spoon','knife']),
        ('tableware',['crockery','dishes','dish','plate','bowl','cup','mug']),
        ('packaged',['canned food','tinned food','tins','cans','canned goods','groceries']),
        ('containers',['box','boxes','crate','crates','carton','cartons']),
        ('electricity box',['electricitybox','electrical box','electrical cabinet','breaker box','fuse box']),
        ('interactables',['interactable','interactible','interactibles']),
        ('intercom speaker',['speaker box','buzzer']),
        ('keycard reader',['key card reader','card reader']),
        ('control button',['push button']),
        ('security screen',['mini terminal','security terminal']),
        ('countertop',['blender','toaster','microwave']),
        ('television',['tv','tvs','televisions']),
        ('floodlight',['flood light','work light']),
        ('stockroom',['backroom','back room']),
    ]
    catalog = dict(version=1, scope=['Kitchen','Grocery Store','Storage'],
                   statuses=['Candidate','Unreviewed','Review','Background'],
                   synonyms=[dict(canonical=c,aliases=v) for c,v in aliases],
                   entries=entries)
    return catalog, excluded


if __name__ == '__main__':
    from build_project_asset_inventory import publish_catalogue

    pilot, unmatched = build_pilot_catalogue()
    publish_catalogue(pilot, unmatched)
