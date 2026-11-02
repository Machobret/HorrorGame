"""Project-relative output paths for maintained art generators."""
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[2]

def owner(name):
    name = Path(name).name.removeprefix('Attack_')
    for ammo in ('HuntingRifleAmmo','RevolverAmmo','ShotgunAmmo','PistolAmmo','FlareAmmo','SMGAmmo'):
        if name.startswith(ammo): return 'Ammunition/' + ammo
    if name.startswith('RifleAmmo'): return 'Ammunition/HuntingRifleAmmo'
    if name.startswith(('Cartridge_','ShotShell_','FlareCartridge_')): return 'Ammunition/Shared'
    if name.startswith(('Flare','SignalFlare','StylizedFlare')): return 'FlareGun'
    for key in ('Revolver','HuntingRifle','MP5','Pistol','Shotgun'):
        if name.startswith(key): return key
    if name.startswith('Rifle'): return 'HuntingRifle'
    return 'Shared'

def weapon(category, name):
    if category == 'Models':
        parts = name.split('/')
        if parts[0] in ('AmmoSides','AmmoPackaging'):
            return 'Assets/Game/Weapons/Ammunition/Shared/Models/' + name
        return 'Assets/Game/Weapons/' + owner(parts[0]) + '/Models' + ('/' + '/'.join(parts[1:]) if len(parts)>1 else '')
    if name.startswith('Weapon_TestPickups'):
        return 'Assets/Sandbox/WeaponTestRoom/Prefabs/' + name
    return 'Assets/Game/Weapons/' + owner(name) + '/' + category + '/' + name

def weapon_asset(category, name):
    return PROJECT_ROOT / weapon(category, name)
