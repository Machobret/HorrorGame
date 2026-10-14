from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageOps
ROOT=Path(__file__).resolve().parents[2];out=ROOT/'ArtSource/Weapons/Progress'
pw,ph,gap,label=768,512,12,62
sheet=Image.new('RGB',(pw*3+gap*2,(ph+label)*2+gap),'#101318');draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',25)
items=[('Gun','1911 PISTOL','+15%'),('Revolver','MAGNUM REVOLVER','GRIP'),('FlareGun','FLARE GUN','-18%')]
for row in range(2):
 for col,(name,title,change) in enumerate(items):
  path=(out/'Handguns_Before' if row==0 else out)/(name+'_InGame.png')
  image=Image.open(path).convert('RGB').crop((220,130,1190,780));image=ImageOps.fit(image,(pw,ph),method=Image.Resampling.NEAREST)
  x=col*(pw+gap);y=row*(ph+label+gap);sheet.paste(image,(x,y))
  draw.rectangle((x,y+ph,x+pw-1,y+ph+label-1),fill='#20252e')
  draw.rectangle((x,y+ph,x+6,y+ph+label-1),fill='#777c85' if row==0 else '#a84642')
  caption=title+' — BEFORE' if row==0 else title+' — AFTER ('+change+')'
  draw.text((x+21,y+ph+18),caption,font=font,fill='#f3ede5')
sheet.save(out/'Handguns_Before_After.png')
print(out/'Handguns_Before_After.png')
