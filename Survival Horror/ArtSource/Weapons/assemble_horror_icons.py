from pathlib import Path
import shutil, sys
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'ArtSource/Weapons/HorrorIcons'
names=[('1911','1911 pistol'),('DoubleBarrel','Double-barrel shotgun'),('Revolver','Magnum revolver'),('HuntingRifle','Hunting rifle'),('FlareGun','Flare gun')]
def font(size,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'),size)
board=Image.new('RGB',(1800,600),(18,21,25));draw=ImageDraw.Draw(board)
draw.text((28,18),'HORROR LIGHTING — FULL WEAPON SET',font=font(32,True),fill=(237,232,221))
draw.text((28,65),'Amber key light / cool rim light / pixelated textures. Small previews show 64px inventory size.',font=font(21),fill=(167,175,184))
for i,(name,title) in enumerate(names):
 x=i*360;y=125
 draw.rounded_rectangle((x+10,y,x+350,y+450),radius=14,fill=(31,36,42),outline=(56,63,70))
 image=Image.open(OUT/(name+'.png')).convert('RGBA')
 assert image.size==(1024,1024) and image.getbbox(),name
 preview=image.resize((330,330),Image.Resampling.LANCZOS);board.paste(preview,(x+15,y+15),preview)
 draw.text((x+22,y+355),title,font=font(21,True),fill=(224,228,232))
 draw.rounded_rectangle((x+264,y+378,x+332,y+446),radius=4,fill=(14,17,20),outline=(71,78,84))
 small=image.resize((64,64),Image.Resampling.LANCZOS);board.paste(small,(x+266,y+380),small)
 if '--apply' in sys.argv:
  target=ROOT/'Assets/AssetStore/HorrorEngine/ExampleContent/Weapons'/('Gun' if name=='1911' else 'Shotgun')/('Gun.png' if name=='1911' else 'Shotgun.png') if name in ('1911','DoubleBarrel') else ROOT/'Assets/Game/Weapons/Models'/name/(name+'_Icon.png')
  backup=OUT/(name+'_Before.png')
  if not backup.exists():shutil.copy2(target,backup)
  shutil.copy2(OUT/(name+'.png'),target)
  print('APPLIED',target)
board.save(OUT/'AllGuns_HorrorLighting.png')
print(OUT/'AllGuns_HorrorLighting.png')
