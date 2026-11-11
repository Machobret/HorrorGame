"""Original pixel surfaces and fictional packaging for the single drinks fridge."""
from pathlib import Path
import random,sys
if '--deps' in sys.argv:sys.path.insert(0,sys.argv[sys.argv.index('--deps')+1])
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/Game/Environment/NeighborhoodStore/Textures'
rng=random.Random(1984);atlas=Image.new('RGB',(256,128))
font=lambda s:ImageFont.truetype(str(ROOT/'Assets/Game/UI/Fonts/ExaltFallUI-Bold.ttf'),s)
colors=[(159,160,135),(65,85,73),(118,127,116),(33,42,35),(126,46,35),(61,90,111),(174,164,129),(83,116,105)]
for i in range(8):
    im=Image.new('RGB',(64,64),colors[i]);d=ImageDraw.Draw(im)
    if i in (0,1,2):
        for n in range(230):d.point((rng.randrange(64),rng.randrange(64)),fill=tuple(max(0,c+rng.choice([-12,-6,0,6,12])) for c in colors[i]))
        for n in range(12):
            x=rng.randrange(64);y=rng.choice([rng.randrange(8),rng.randrange(57,64)]);d.line((x,y,min(63,x+3),min(63,y+3)),fill=(98,77,49))
    if i==2:
        for y in range(8,58,6):d.line((5,y,58,y),fill=(50,67,55),width=2)
    if i==3:
        for x in range(0,64,6):d.line((x,0,x,63),fill=(53,64,51))
    if i in (4,5):
        d.rectangle((0,14,63,48),fill=(195,185,145));d.text((32,23),'COLA' if i==4 else 'WATER',font=font(11),fill=(63,58,39),anchor='mt');d.text((32,39),'CORNER BRAND',font=font(5),fill=(83,78,54),anchor='mt')
        d.line((0,6,63,6),fill=(156,146,109));d.line((0,58,63,58),fill=(156,146,109))
    if i==6:
        # Compensate for the wide, shallow header face so lettering reads at its natural proportions.
        strip=Image.new('RGB',(64,12),colors[i]);sd=ImageDraw.Draw(strip)
        sd.rectangle((0,0,63,11),outline=(100,37,30));sd.text((32,2),'COLD DRINKS',font=font(8),fill=(104,38,30),anchor='mt')
        im=strip.resize((64,64),Image.Resampling.NEAREST);d=ImageDraw.Draw(im)
    if i==7:
        d.line((5,62,55,0),fill=(121,147,130),width=2);d.line((35,63,63,31),fill=(107,137,122))
    atlas.paste(im,((i%4)*64,(i//4)*64))
atlas.save(OUT/'DrinksFridge_Atlas.png')
