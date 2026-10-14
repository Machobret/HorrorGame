"""Assemble actual Unity stills and a 24 fps flare recording.
Usage: python assemble_weapon_media.py --frames <Unity frame directory>
"""
from pathlib import Path
import argparse
from PIL import Image, ImageDraw, ImageFont, ImageOps
root = Path(__file__).resolve().parents[2]
out = root / 'ArtSource/Weapons/Progress'
items = [('Gun', '1911 PISTOL'), ('Shotgun', 'DOUBLE-BARREL SHOTGUN'),
         ('Revolver', 'MAGNUM REVOLVER'), ('HuntingRifle', 'HUNTING RIFLE'),
         ('FlareGun', 'FLARE GUN')]
width, height, label, gap = 480, 420, 56, 10
sheet = Image.new('RGB', (width * 5 + gap * 4, height + label), '#101318')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf', 22)
for i, (name, title) in enumerate(items):
    source = Image.open(out / (name + '_InGame.png')).convert('RGB')
    source = source.crop((220, 130, 1190, 780))
    panel = ImageOps.fit(source, (width, height), method=Image.Resampling.NEAREST)
    x = i * (width + gap)
    sheet.paste(panel, (x, 0))
    draw.rectangle((x, height, x + width - 1, height + label - 1), fill='#20252e')
    draw.rectangle((x, height, x + 5, height + label - 1), fill='#a84642')
    draw.text((x + 16, height + 16), title, font=font, fill='#f3ede5')
sheet.save(out / 'Weapons_Side_By_Side.png')
parser = argparse.ArgumentParser()
parser.add_argument('--frames', type=Path, required=True)
args = parser.parse_args()
paths = sorted(args.frames.glob('Flare_*.png'))
if not paths:
    raise SystemExit('No Unity frames found')
# One shared palette preserves the same colours throughout the animation.
samples = [Image.open(paths[i]).convert('RGB').resize((240, 135))
           for i in range(0, len(paths), max(1, len(paths) // 12))]
atlas = Image.new('RGB', (240, 135 * len(samples)))
for i, sample in enumerate(samples):
    atlas.paste(sample, (0, i * 135))
palette = atlas.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
frames = [Image.open(p).convert('RGB').resize((800, 450), Image.Resampling.NEAREST)
          .quantize(palette=palette, dither=Image.Dither.NONE) for p in paths]
durations = [(round((i + 1) * 100 / 24) - round(i * 100 / 24)) * 10
             for i in range(len(frames))]
frames[0].save(out / 'FlareGun_Firing.gif', save_all=True, append_images=frames[1:],
               duration=durations, loop=0, disposal=1, optimize=False)
print(f'Saved lineup and {len(frames)} frame GIF to {out}')
