from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parent/'IconStudies'
styles=[('01_Profile','1. Clean profile','Flat angle / soft neutral light'),('02_Classic','2. Classic inventory','Three-quarter / warm studio light'),('03_Forensic','3. Cold forensic','Raised angle / blue-white highlights'),('04_Horror','4. Horror lighting','Low angle / amber key, blue rim'),('05_Retro','5. Chunky retro','128px render / sharp pixel edges')]
def font(size,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('segoeuib.ttf' if bold else 'segoeui.ttf'),size)
board=Image.new('RGB',(1800,1040),(18,21,25));draw=ImageDraw.Draw(board)
draw.text((30,20),'WEAPON ICON STYLE STUDIES',font=font(32,True),fill=(237,232,221))
draw.text((30,64),'Same game meshes and pixelated textures. Large previews + 64px inventory-size previews.',font=font(20),fill=(167,175,184))
for col,(key,title,description) in enumerate(styles):
 x=col*360
 draw.text((x+18,110),title,font=font(23,True),fill=(234,231,220))
 draw.text((x+18,147),description,font=font(16),fill=(174,183,194))
 for row,name in enumerate(('1911','DoubleBarrel')):
  y=190+row*410
  draw.rounded_rectangle((x+10,y,x+350,y+390),radius=14,fill=(31,36,42),outline=(56,63,70))
  image=Image.open(ROOT/(key+'_'+name+'.png')).convert('RGBA')
  preview=image.resize((320,320),Image.Resampling.NEAREST if key=='05_Retro' else Image.Resampling.LANCZOS)
  board.paste(preview,(x+20,y+2),preview)
  draw.text((x+25,y+336),'1911' if row==0 else 'Double barrel',font=font(21,True),fill=(210,216,221))
  draw.rounded_rectangle((x+263,y+321,x+331,y+389),radius=4,fill=(14,17,20),outline=(71,78,84))
  small=image.resize((64,64),Image.Resampling.NEAREST if key=='05_Retro' else Image.Resampling.LANCZOS)
  board.paste(small,(x+265,y+323),small)
board.save(ROOT/'WeaponIconStyles.png')
print(ROOT/'WeaponIconStyles.png')
