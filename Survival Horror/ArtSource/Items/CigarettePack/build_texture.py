"""Original fictional packaging. Run with Python + Pillow, then render_prop.py in Blender."""
from pathlib import Path
import random, sys
if '--deps' in sys.argv:
    sys.path.insert(0, sys.argv[sys.argv.index('--deps') + 1])
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'Assets/Game/Items/CigarettePack'
OUT.mkdir(parents=True, exist_ok=True)
rng = random.Random(1941)
cream, ink, red, gold = (207,194,162), (42,32,29), (103,27,32), (162,132,77)
fontroot = ROOT / 'Assets/Game/UI/Fonts'
def font(size):
    return ImageFont.truetype(str(fontroot / 'ExaltFallUI-Bold.ttf'), size)
def label(d, text, y, size, fill, center=64):
    d.text((center,y),text,font=font(size),fill=fill,anchor='mt')

panels=[]
for i in range(6):
    im=Image.new('RGB',(128,128),cream); d=ImageDraw.Draw(im)
    d.rectangle((3,3,124,124),outline=gold,width=2)
    if i in (0,2):
        d.rectangle((4,4,123,31),fill=red)
        label(d,'ASHLINE',9,19,cream)
        d.line((4,35,123,35),fill=ink,width=2)
        if i==0:
            d.polygon([(64,42),(79,56),(64,70),(49,56)],outline=red,width=2)
            d.line((57,56,71,56),fill=red,width=2)
            label(d,'CLASSIC',74,16,ink)
            label(d,'20 CIGARETTES',94,9,ink)
        else:
            label(d,'ORIGINAL BLEND',46,11,ink)
            label(d,'PACKED FOR',63,8,ink)
            label(d,'ASHLINE TOBACCO CO.',75,8,ink)
            for x in range(23,106,3):
                d.rectangle((x,94,x+rng.choice([0,1]),109),fill=ink)
        d.line((4,114,123,114),fill=red,width=2)
        label(d,'TOBACCO',117,6,ink)
    elif i in (1,3):
        d.rectangle((5,5,122,32),fill=red)
        label(d,'ASHLINE',11,16,cream)
        label(d,'CLASSIC',52,14,ink)
        label(d,'20',80,18,red)
        d.line((8,112,120,112),fill=gold,width=2)
    else:
        d.rectangle((5,5,122,122),fill=red if i==4 else cream)
        label(d,'ASHLINE' if i==4 else 'A 041 / 20',50,18,cream if i==4 else ink)
        d.line((5,15,122,15),fill=gold,width=2)
        d.line((5,112,122,112),fill=gold,width=2)
    # Restrained paper grain, folded edges, printing wear. Keep the name readable.
    pix=im.load()
    for y in range(128):
        for x in range(128):
            n=rng.choice([-7,-3,0,0,0,3,5])
            pix[x,y]=tuple(max(0,min(255,c+n)) for c in pix[x,y])
    d=ImageDraw.Draw(im)
    for n in range(35):
        x=rng.choice([rng.randrange(0,6),rng.randrange(122,128)])
        y=rng.randrange(128)
        d.line((x,y,x+rng.randrange(1,4),min(127,y+rng.randrange(1,5))),fill=(150,136,110))
    panels.append(im)
atlas=Image.new('RGB',(384,256))
for i,p in enumerate(panels): atlas.paste(p,((i%3)*128,(i//3)*128))
atlas.save(OUT/'CigarettePack_Albedo.png')
atlas.save(Path(__file__).with_name('CigarettePack_Atlas_Source.png'))
print(OUT/'CigarettePack_Albedo.png')
