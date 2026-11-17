"""Archive and inventory Classic64 without importing unapproved content into Unity.

Run with Python: prepare_library.py --source <extracted pack directory>.
"""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[2]
FAMILIES = {
    'Art': 'HouseholdDecor', 'Books': 'HouseholdDecor', 'Boxes': 'StorageContainers',
    'Bricks': 'MasonrySurfaces', 'Buildings': 'InteriorFixtures', 'Concrete': 'MasonrySurfaces',
    'Doors': 'InteriorFixtures', 'Electrical': 'LightingElectrical', 'Floor': 'InteriorFixtures',
    'Food': 'FoodKitchenware', 'Gems': 'HouseholdDecor', 'Ground': 'Nature',
    'Industrial': 'ToolsEquipment', 'Metal': 'ToolsEquipment', 'Misc': 'ToolsEquipment',
    'Nature': 'Vegetation', 'Panels': 'AppliancesElectronics', 'Papers': 'HouseholdDecor',
    'Products': 'FoodKitchenware', 'Road': 'RoadSurfaces', 'Rocks': 'Nature',
    'Roof': 'MasonrySurfaces', 'Shelves': 'Furniture', 'Signs': 'RoadFurniture',
    'Tiles': 'MasonrySurfaces', 'Tools': 'ToolsEquipment', 'Units': 'AppliancesElectronics',
    'Vehicles': 'Vehicles', 'Walls': 'MasonrySurfaces', 'Water': 'Nature',
    'Windows': 'Windows', 'Wood': 'InteriorFixtures',
}
PILOT = [
    ('Books/book_black_01.blend', 'HouseholdDecor', 'BookBlack01', .24),
    ('Boxes/box_01.blend', 'StorageContainers', 'CardboardBox01', .45),
    ('Shelves/commerical_shelf_one_side.blend', 'Furniture', 'CommercialShelf', 1.8),
    ('Food/food_canned_soup_1.blend', 'FoodKitchenware', 'SoupCan01', .12),
    ('Food/drink_can_soda.blend', 'FoodKitchenware', 'SodaCan01', .122),
    ('Electrical/electrical_junctionbox_small.blend', 'LightingElectrical', 'JunctionBox', .3),
    ('Industrial/pump_station.blend', 'ToolsEquipment', 'PumpStation', 1.5),
    ('Tools/shovel.blend', 'ToolsEquipment', 'Shovel', 1.25),
    ('Nature/bush_01.blend', 'Vegetation', 'Bush01', .8),
    ('Units/generator.blend', 'AppliancesElectronics', 'Generator', 1.2),
    ('Industrial/fire_extenguisher_case.blend', 'InteriorFixtures', 'ExtinguisherCase', .7),
]
SURFACES = [
    ('Concrete/Materials/concrete_01.png', 'MasonrySurfaces', 'Concrete01'),
    ('Bricks/Materials/brick_medium_mossy.png', 'MasonrySurfaces', 'MossyBrick'),
    ('Metal/Materials/metal_rusty_06.jpg', 'ToolsEquipment', 'RustyMetal06'),
    ('Wood/Materials/wood_bare.png', 'InteriorFixtures', 'BareWood'),
    ('Floor/Materials/carpet_darkgray_01.png', 'InteriorFixtures', 'DarkCarpet'),
    ('Tiles/Materials/tiles_ceiling_02.jpg', 'MasonrySurfaces', 'CeilingTiles02'),
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    args = parser.parse_args()
    original = HERE / 'Original'
    if args.source.resolve() == original.resolve():
        raise ValueError('Source must be the external extracted pack, not the archive')
    original.mkdir(exist_ok=True)
    review = PROJECT / 'Exports/Reviews/Classic64'
    review.mkdir(parents=True, exist_ok=True)
    baseline_path = review / 'ExistingFilesBefore.json'
    if not baseline_path.exists():
        baseline = {}
        for folder in ('Assets', 'Packages', 'ProjectSettings'):
            for p in (PROJECT / folder).rglob('*'):
                if p.is_file():
                    stat = p.stat()
                    baseline[p.relative_to(PROJECT).as_posix()] = [stat.st_size, stat.st_mtime_ns]
        baseline_path.write_text(json.dumps(baseline, indent=2), encoding='utf-8')
    existing_images = {}
    for p in (PROJECT / 'Assets/Game/Environment').rglob('*'):
        if p.suffix.lower() in ('.png', '.jpg', '.jpeg'):
            existing_images.setdefault(digest(p), []).append(p.relative_to(PROJECT).as_posix())
    pilot_paths = {p[0] for p in PILOT} | {p[0] for p in SURFACES}
    # Recover local surface filenames when re-importing the original download.
    catalog_path = HERE / 'Catalog.json'
    catalog = json.loads(catalog_path.read_text(encoding='utf-8')) if catalog_path.exists() else {}
    pebble_paths = {entry['sha256']: Path(entry['source']) for entry in catalog.get('files', [])
                    if 'pebble' in Path(entry['source']).name.lower()}
    aliases = {}
    for folder in ('Ground/Materials', 'Rocks/Materials'):
        for image in (args.source / folder).glob('*'):
            if image.is_file():
                canonical = pebble_paths.get(digest(image))
                if canonical and image.name != canonical.name:
                    aliases[image.name] = canonical.name

    entries = []
    for source in sorted(args.source.rglob('*')):
        if not source.is_file():
            continue
        relative = source.relative_to(args.source)
        if relative.parent.as_posix() in ('Ground/Materials', 'Rocks/Materials'):
            relative = relative.with_name(aliases.get(relative.name, relative.name))
        dest = original / relative
        data = source.read_bytes()
        if relative.as_posix() == 'Changelog v0.6.txt':
            for external, local in aliases.items():
                data = data.replace(external.encode(), local.encode())
        sha = hashlib.sha256(data).hexdigest()
        if dest.exists() and digest(dest) != sha:
            raise ValueError(f'Archived original differs; refusing overwrite: {dest}')
        dest.parent.mkdir(parents=True, exist_ok=True)
        if not dest.exists():
            dest.write_bytes(data)
        entries.append(dict(source=relative.as_posix(), sha256=sha, bytes=len(data),
                            proposed_family=FAMILIES.get(relative.parts[0], ''),
                            status='PilotSelected' if relative.as_posix() in pilot_paths else 'SourceOnly',
                            exact_existing_matches=existing_images.get(sha, [])))
    for path in pilot_paths:
        if not (original / path).is_file():
            raise FileNotFoundError(path)
    catalog = dict(pack='Classic64 0.6', author='Craig Snedeker',
                   license_declaration='CC0 per Original/Readme.txt; readme internally labels version 0.2',
                   source_download=str(args.source), imported_scope='Pilot only; remaining library awaits visual review',
                   category_mapping_is_provisional=True,
                   counts=dict(Counter(Path(e['source']).suffix.lower() for e in entries)), files=entries)
    (HERE / 'Catalog.json').write_text(json.dumps(catalog, indent=2), encoding='utf-8')
    selection = dict(models=[dict(source=s, family=f, subject=n,
                                  target_height_m=h if n == 'Shovel' else None) for s, f, n, h in PILOT],
                     surfaces=[dict(source=s, family=f, subject=n) for s, f, n in SURFACES])
    (HERE / 'PilotSelection.json').write_text(json.dumps(selection, indent=2), encoding='utf-8')
    print(json.dumps(dict(archived=len(entries), counts=catalog['counts'],
                          exact_existing_images=sum(bool(e['exact_existing_matches']) for e in entries),
                          selected_models=len(PILOT), selected_surfaces=len(SURFACES))))


if __name__ == '__main__':
    main()
