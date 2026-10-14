"""Repair generated ammo visuals without rebuilding or changing their save IDs.

The imported ammo FBXs have a scale of 100 on the root. The original builder
overrode it with 1. Restore that correction and scale the baked offsets with it.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2] / 'Assets/Game/Weapons/Prefabs'
for name in ('HuntingRifleAmmo', 'FlareAmmo'):
    if name == 'FlareAmmo' and (root.parent / 'Models/FlareAmmo/FlareAmmo_Albedo.png').exists():
        print('NATIVE_METRE_MODEL', name)
        continue
    for kind in ('Pickup', 'Examination'):
        path = root / f'{name}_{kind}.prefab'
        text = path.read_text()
        scales = re.findall(r'propertyPath: m_LocalScale\.[xyz]\n\s+value: ([^\n]+)', text)
        if scales == ['100'] * 3:
            print('ALREADY_FIXED', path.name)
            continue
        assert scales == ['1'] * 3, (path, scales)
        text = re.sub(r'(propertyPath: m_LocalScale\.[xyz]\n\s+value:) 1\n', r'\1 100\n', text)
        text = re.sub(
            r'(propertyPath: m_LocalPosition\.[xyz]\n\s+value:) ([^\n]+)',
            lambda match: match[1] + ' ' + format(float(match[2]) * 100, '.10g'), text)
        if kind == 'Pickup':
            for field in ('m_Center', 'Offset'):
                text = re.sub(
                    r'(  ' + field + r':) \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}',
                    lambda match: match[1] + ' {' + ', '.join(
                        axis + ': ' + format(float(match[index]) * 100, '.10g')
                        for axis, index in (('x', 2), ('y', 3), ('z', 4))) + '}', text)
            center_y = float(re.search(r'm_Center: \{x: [^,]+, y: ([^,]+)', text)[1])
            text = re.sub(r'm_Size: \{x: 0.2, y: 0.2, z: 0.2\}',
                          'm_Size: {x: 0.2, y: ' + format(max(.2, center_y * 2), '.10g') + ', z: 0.2}', text)
        path.write_text(text)
        print('FIXED', path.name)
