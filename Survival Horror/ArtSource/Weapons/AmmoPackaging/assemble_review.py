from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path(r'C:/Users/Mangis/Documents/GitHub/HorrorGame/Survival Horror/ArtSource/Weapons/AmmoPackaging')
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation')
fonts=Path('C:/Windows/Fonts')
def font(n,bold=False):return ImageFont.truetype(str(fonts/('segoeuib.ttf' if bold else 'segoeui.ttf')),n)
items=[('PistolAmmo','Pistol / Standard 9mm'),('ShotgunAmmo','Shotgun / 12 gauge'),('RevolverAmmo','Revolver / .357 Magnum'),('HuntingRifleAmmo','Rifle / .300 Win Mag'),('FlareAmmo','Red signal flares'),('SMGAmmo','SMG / 9mm rounds')]
sheet=Image.new('RGB',(1920,1010),(17,20,22));d=ImageDraw.Draw(sheet)
d.text((28,20),'AMMUNITION PACKAGING',font=font(30,True),fill=(230,224,206));d.text((28,59),'Textured game models  /  front, reverse and inventory icon',font=font(18),fill=(154,161,158))
for col,(key,label) in enumerate(items):
 x=col*320;d.text((x+14,105),label,font=font(18,True),fill=(223,214,190))
 for y,suffix,size in [(140,'Front',312),(480,'Back',266)]:
  im=Image.open(root/'Preview'/f'{key}_{suffix}.png').convert('RGBA');im.thumbnail((size,size),Image.Resampling.LANCZOS);sheet.paste(im,(x+(320-im.width)//2,y),im)
 iconpath=src/('Assets/Game/Weapons/Models/FlareAmmo/FlareAmmo_Icon.png' if key=='FlareAmmo' else f'Assets/Game/Weapons/Models/AmmoSides/{key}_PickupIcon.png')
 icon=Image.open(iconpath).convert('RGBA').resize((128,128),Image.Resampling.LANCZOS)
 d.rounded_rectangle((x+92,803,x+228,939),radius=4,fill=(25,29,31),outline=(65,69,67));sheet.paste(icon,(x+96,807),icon);d.text((x+94,952),'128 px UI icon',font=font(16),fill=(154,161,158))
 d.line((x+12,464,x+308,464),fill=(51,57,58))
sheet.save(root/'Ammo_Packaging_Overview.jpg',quality=94)
print('Created overview from real Unity renders')