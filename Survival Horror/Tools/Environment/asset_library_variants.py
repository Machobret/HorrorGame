"""Identify prefab versions whose subject, shape and placement role are shared."""

from pathlib import Path
import re


# These shelf pairs were checked against front renders and calibrated dimensions.
# A different width, slope or shelf profile remains a separate placement choice.
DISPLAY_FAMILIES = {
    'Double-bay grocery shelving': ('P0461', 'P0488'),
    'Wide grocery shelving': ('P0462', 'P0463', 'P0489'),
    'Medium boxed-goods display': ('P0493', 'P0497'),
    'Narrow boxed-goods display': ('P0498', 'P0501'),
}

CRT_PAIRS = (
    ('P0320', 'P0328'), ('P0321', 'P0329'), ('P0322', 'P0330'), ('P0323', 'P0331'),
    ('P0325', 'P0332'), ('P0326', 'P0333'), ('P0327', 'P0334'),
)

OFFICE_COLOURS = r'black|blue|green|grey|gray|orange|pink|red|white|yellow|pattern'


def words(name):
    spaced = re.sub(r'(?<=[a-z])(?=[A-Z])|(?<=[a-zA-Z])(?=\d)', ' ', name)
    return re.sub(r' +', ' ', spaced.replace('_', ' ')).strip().capitalize()


def proposal(entry):
    path = Path(entry['path'])
    stem = path.stem
    folder = path.parts[3] if len(path.parts) > 4 else ''
    original = entry['original']
    if path.parts[2] != 'Environment':
        return None

    if folder == 'NeonSigns':
        design = path.parent.name
        title, _, version = entry['name'].rpartition(' (')
        return f'neon:{design}', title, version.removesuffix(')')

    if folder == 'Pizzeria' and entry['category'].startswith('Buildings/') and len(entry['size']) == 3:
        match = re.fullmatch(r'Pizzeria_building_(\d+)(?:_Cube)?(?:_(\d+))?', stem)
        if match:
            subject, variant = match.groups()
            width, height, depth = entry['size']
            short, long = sorted((width, depth))
            # Treat a rotated footprint as the same shape. Keep different widths and heights separate.
            footprint = f'{short:.1f}:{long:.1f}:{height:.1f}'
            title = 'Masonry corner building' if subject == '10' else f'Urban building {subject}'
            return f'pizzeria:building:{subject}:{footprint}', title, f'Variant {variant}' if variant else 'Standard'

    if folder == 'PSXShelvesProps':
        match = re.fullmatch(r'(.+)_(Normal|Horror)', stem)
        if match and any(part in path.parts for part in ('Shelving', 'Books', 'Lighting')):
            base, version = match.groups()
            return f'showroom:{path.parent.name}:{base}', words(base), version

    if folder == 'Office':
        cubicle = re.fullmatch(r'divider_full_([ch])(?:_(lue|red))?_desk(?:_(blue|red))?', original)
        if cubicle:
            profile, prefix, suffix = cubicle.groups()
            colour = suffix or ('blue' if prefix == 'lue' else prefix) or 'standard'
            return f'office:cubicle_{profile}', f'{profile.upper()}-shaped cubicle desk', words(colour)
        divider = re.fullmatch(r'divider_(veneer|wood)(?:_(blue|red))?(_low)?(_half)?', original)
        if divider:
            finish, colour, low, half = divider.groups()
            profile = ('Low ' if low else '') + ('Narrow ' if half else '')
            title = (profile + 'office divider').capitalize()
            return f'office:divider:{profile}', title, words(finish + (' ' + colour if colour else ''))
        desk = re.fullmatch(r'(desk[12])(?:_(\w+))?', original)
        if desk:
            base, version = desk.groups()
            version = version or 'standard'
            if 'corner' in version:
                # Desk 1's two corner assets have different measured widths and depths.
                profile = original if base == 'desk1' else f'{base}_corner'
                return f'office:{profile}', words(base) + ' corner', words(version)
            # The scale audit measured two different footprints, despite the shared Desk name.
            deep = version.startswith('alt') and original != 'desk1_alt_weathered'
            profile = f'{base}_deep' if deep else base
            return f'office:{profile}', words(base) + (' deep' if deep else ''), words(version)
        if re.fullmatch(r'conference_desk(?:_squared)?(?:_alt)?', original):
            profile = original.removesuffix('_alt')
            return f'office:{profile}', words(profile), 'Alternate finish' if original.endswith('_alt') else 'Standard'
        if re.match(r'^(?:armchair|beanbag|couch|conference_chair|office_chair)(?:_|$)', original):
            profile = re.sub(r'_(?:' + OFFICE_COLOURS + r')(?=_|$)', '', original)
            finish = ' '.join(token for token in original.split('_') if re.fullmatch(OFFICE_COLOURS, token))
            return f'office:{profile}', words(profile), words(finish) or 'Standard'

    if folder == 'Vehicles' and re.fullmatch(r'Cars_Car5(?:_Police|_Taxi)?', stem):
        return 'vehicle:car5', 'Car 5', stem.removeprefix('Cars_Car5_') if stem != 'Cars_Car5' else 'Standard'

    if 'Dumpsters' in path.parts and entry['category'].startswith('Waste & recycling/'):
        if stem.startswith('Urban_Dumpster'):
            diagonal = stem.startswith('Urban_DumpsterDiagonal')
            return ('dumpster:diagonal' if diagonal else 'dumpster:standard',
                    'Diagonal dumpster' if diagonal else 'Standard dumpster',
                    words(stem.removeprefix('Urban_DumpsterDiagonal' if diagonal else 'Urban_Dumpster')))

    if 'MetalTrashCans' in path.parts and entry['category'].startswith('Waste & recycling/'):
        if stem.startswith('Urban_Metaltrashcan') and not stem.endswith('Lid'):
            return 'bin:metal', 'Metal trash can', words(stem.removeprefix('Urban_Metaltrashcan'))

    if 'TrashCans' in path.parts and re.fullmatch(r'Urban_TrashCan(?:(Alt[12]))?(?:Full|Open|Overflowing)?', stem):
        body = re.match(r'Urban_TrashCan(?:Alt[12])?', stem).group()
        return f'bin:{body}', words(body.removeprefix('Urban_')), words(stem.removeprefix(body)) or 'Standard'

    if folder == 'ModelsPackPSX':
        title, _, review_id = entry['name'].rpartition(' · ')
        for standard, green in CRT_PAIRS:
            if review_id in (standard, green):
                return f'crt:{standard}', title.removesuffix(' with green screen'), 'Green screen' if review_id == green else 'Screen image'
        for title, ids in DISPLAY_FAMILIES.items():
            asset_id = next((asset_id for asset_id in ids if entry['name'].endswith(' · ' + asset_id)), None)
            if asset_id:
                return f'display:{ids[0]}', title, asset_id

    if folder == 'FoodKitchenware':
        packaging = re.fullmatch(r'Classic64_(FoodCannedSoup|FoodCereal|FoodSnackChips|DrinkCanSoda)(\d+)_Visual', stem)
        if packaging:
            kind = packaging.group(1)
            title = {'FoodCannedSoup': 'Canned soup', 'FoodCereal': 'Cereal box', 'FoodSnackChips': 'Chip bag', 'DrinkCanSoda': 'Soda can'}[kind]
            return f'food:{kind}', title, 'Label ' + packaging.group(2)
    return None


def assign_families(entries):
    groups = {}
    for entry in entries:
        candidate = proposal(entry)
        if not candidate:
            continue
        key, title, version = candidate
        # Never merge an inspection-only entry with scene placement or mix browse views.
        group_key = (entry['view'], entry['category'], entry['action'], key)
        groups.setdefault(group_key, []).append((entry, title, version))
    for key, members in groups.items():
        if len(members) < 2:
            continue
        for entry, title, version in members:
            entry.update(family='|'.join(key), familyName=title, variantName=version)
    return {key: members for key, members in groups.items() if len(members) > 1}
