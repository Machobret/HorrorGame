"""Assemble Unity captures with weapon labels and a flare lighting panel."""
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[2]
out=ROOT/'ArtSource/Weapons/Progress'
pw,ph,gap,label=768,512,12,62
sheet=Image.new('RGB',(pw*3+gap*2,(ph+label)*2+gap),'#101318')
draw=ImageDraw.Draw(sheet);font=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',26)
items=[('Gun_InGame.png','1911 PISTOL'),('Shotgun_InGame.png','DOUBLE-BARREL SHOTGUN'),('Revolver_InGame.png','MAGNUM REVOLVER'),('HuntingRifle_InGame.png','HUNTING RIFLE'),('FlareGun_InGame.png','FLARE GUN'),('Flare_Illumination_InGame.png','FLARE ILLUMINATION')]
for i,(name,title) in enumerate(items):
 image=Image.open(out/name).convert('RGB')
 if i<5:image=image.crop((220,130,1190,780))
 image=ImageOps.fit(image,(pw,ph),method=Image.Resampling.NEAREST)
 x=(i%3)*(pw+gap);y=(i//3)*(ph+label+gap)
 sheet.paste(image,(x,y))
 draw.rectangle((x,y+ph,x+pw-1,y+ph+label-1),fill='#20252e')
 draw.rectangle((x,y+ph,x+6,y+ph+label-1),fill='#a84642')
 draw.text((x+21,y+ph+18),title,font=font,fill='#f3ede5')
sheet.save(out/'Weapons_And_Flare_Labeled.png')
sheet.save(out/'Weapons_InGame_Progress.png')
print(out/'Weapons_And_Flare_Labeled.png')
