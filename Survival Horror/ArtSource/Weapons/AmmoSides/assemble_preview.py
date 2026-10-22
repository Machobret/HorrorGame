"""Assemble front and rear Unity renders of the corrected ammunition cartons."""
from pathlib import Path
import argparse
from PIL import Image,ImageDraw,ImageFont
parser=argparse.ArgumentParser();parser.add_argument('--captures',type=Path,required=True);args=parser.parse_args()
root=Path(__file__).resolve().parents[3];out=root/'ArtSource/Weapons/Progress/Ammo_Box_Sides_Preview.png'
sheet=Image.new('RGB',(2000,1100),'#23262a');draw=ImageDraw.Draw(sheet);font=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',24)
for col,(name,title) in enumerate([('PistolAmmo','PISTOL'),('ShotgunAmmo','SHOTGUN'),('RevolverAmmo','MAGNUM'),('HuntingRifleAmmo','HUNTING RIFLE')]):
 for row,view in enumerate(['Front','Back']):
  panel=Image.open(args.captures/f'{name}_{view}.png').convert('RGBA').resize((500,500),Image.Resampling.NEAREST);sheet.paste(panel,(col*500,row*550),panel);draw.text((col*500+18,row*550+514),title+' — '+view.upper(),font=font,fill='white')
sheet.save(out);print(out)
