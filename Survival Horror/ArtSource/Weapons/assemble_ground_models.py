"""Combine five Unity ground-model renders, without character or UI overlays."""
from pathlib import Path
import argparse
from PIL import Image
parser = argparse.ArgumentParser()
parser.add_argument('--captures', type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
out = root / 'ArtSource/Weapons/Progress'
names = ['Pistol', 'Shotgun', 'Revolver', 'HuntingRifle', 'FlareGun']
sheet = Image.new('RGB', (3600, 640), '#131519')
for i, name in enumerate(names):
    frame = Image.open(args.captures / f'Weapons_And_Ammo_Ground_{name}.png').convert('RGB')
    frame = frame.resize((720, 640), Image.Resampling.NEAREST)
    sheet.paste(frame, (i * 720, 0))
sheet.save(out / 'Weapons_And_Ammo_Ground.png')
print(out / 'Weapons_And_Ammo_Ground.png')
