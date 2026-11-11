"""Original fictional HARTON register faceplates and pixel wear. Python + Pillow."""
from pathlib import Path
import random,sys
if '--deps' in sys.argv:sys.path.insert(0,sys.argv[sys.argv.index('--deps')+1])
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[3];OUT=ROOT/'Assets/Game/Environment/NeighborhoodStore/Textures'
font=lambda s:ImageFont.truetype(str(ROOT/'Assets/Game/UI/Fonts/ExaltFallUI-Bold.ttf'),s)
rng=random.Random(82);atlas=Image.new('RGB',(256,128))
for i in range(8):
    im=Image.new('RGB',(64,64),[(157,151,119),(39,45,38),(171,166,137),(99,105,72),(13,26,19),(155,149,118),(207,195,161),(124,119,94)][i]);d=ImageDraw.Draw(im)
    if i==0:
        for n in range(250):d.point((rng.randrange(64),rng.randrange(64)),fill=rng.choice([(137,132,102),(173,166,132),(153,147,115)]))
        for x in (3,59):d.line((x,0,x,63),fill=(110,107,82))
    if i==2:
        labels=['7','8','9','QTY','4','5','6','SUB','1','2','3','TAX','0','00','.','CLR']
        for k,t in enumerate(labels):
            x,y=k%4*16,k//4*16;d.rectangle((x,y,x+15,y+15),outline=(97,96,77));d.text((x+8,y+4),t,font=font(6 if len(t)>1 else 9),fill=(33,40,33),anchor='mt')
    if i==3:
        d.rectangle((3,3,60,60),outline=(40,52,36),width=3);d.text((32,26),'TOTAL',font=font(10),fill=(222,211,166),anchor='mt')
    if i==4:
        d.text((32,8),'TOTAL',font=font(8),fill=(65,99,60),anchor='mt');d.text((32,26),'12.40',font=font(17),fill=(139,181,95),anchor='mt')
    if i==5:
        for y in range(7,57,5):d.rectangle((7,y,56,y+1),fill=(45,52,41))
    if i==6:
        d.text((32,3),'CORNER SHOP',font=font(7),fill=(52,57,41),anchor='mt')
        for y in range(18,58,6):d.line((8,y,rng.randrange(39,57),y),fill=(82,84,65))
    if i==7:
        d.rectangle((2,2,61,61),outline=(61,65,48),width=2);d.text((32,17),'HARTON',font=font(11),fill=(38,45,35),anchor='mt');d.text((32,36),'MODEL 82',font=font(7),fill=(45,50,38),anchor='mt')
    for n in range(12):
        x=rng.choice([rng.randrange(4),rng.randrange(60,64)]);y=rng.randrange(64);d.point((x,y),fill=(102,96,68))
    atlas.paste(im,((i%4)*64,(i//4)*64))
atlas.save(OUT/'CashRegister_Atlas.png')
