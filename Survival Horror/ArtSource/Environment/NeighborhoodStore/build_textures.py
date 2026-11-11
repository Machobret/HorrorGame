"""Original checkout surface art; Python + Pillow. Other textures reuse the imported shelf pack."""
from pathlib import Path
import random,sys
if '--deps' in sys.argv: sys.path.insert(0,sys.argv[sys.argv.index('--deps')+1])
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'Assets/Game/Environment/NeighborhoodStore/Textures'
OUT.mkdir(parents=True,exist_ok=True)
rng=random.Random(1982)
im=Image.new('RGB',(128,64),(78,101,89));d=ImageDraw.Draw(im)
for n in range(700):
    x,y=rng.randrange(128),rng.randrange(64)
    d.point((x,y),fill=rng.choice([(90,110,97),(63,84,73),(112,129,107),(80,102,88)]))
for n in range(45):
    x,y=rng.randrange(128),rng.randrange(50,64)
    d.line((x,y,min(127,x+rng.randrange(1,5)),y),fill=(137,143,111))
d.ellipse((89,14,108,28),outline=(57,76,61),width=2)
d.ellipse((90,15,107,27),outline=(109,121,91))
for x,y in [(19,39),(25,39),(74,45)]:d.line((x,y,x+10,y+1),fill=(49,69,59))
im.save(OUT/'CheckoutCounter_Laminate.png')
im=Image.new('RGB',(64,64),(183,174,139));d=ImageDraw.Draw(im)
d.rectangle((2,2,61,61),outline=(117,44,34),width=3)
font=ROOT/'Assets/Game/UI/Fonts/ExaltFallUI-Bold.ttf'
for text,y in [('PAY',10),('HERE',32)]:d.text((32,y),text,font=ImageFont.truetype(str(font),17),fill=(112,38,29),anchor='mt')
for n in range(35):
    x=rng.choice([rng.randrange(5),rng.randrange(59,64)]);y=rng.randrange(64)
    d.point((x,y),fill=(119,106,76))
im.save(OUT/'CheckoutCounter_Sign.png')
print('CHECKOUT_TEXTURES_OK: two original pixel textures')
