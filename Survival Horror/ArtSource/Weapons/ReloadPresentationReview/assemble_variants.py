from pathlib import Path
from PIL import Image, ImageDraw
import shutil
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/ReloadPoseVariantsReview')
dst=Path(__file__).resolve().parent/'Variants'
dst.mkdir(exist_ok=True)
names={'Gun':'1911','Shotgun':'Shotgun','HuntingRifle':'Hunting rifle','Revolver':'Revolver','FlareGun':'Flare gun'}
contact=Image.new('RGB',(1440,5*295),(12,15,19)); cd=ImageDraw.Draw(contact)
for row,(gun,title) in enumerate(names.items()):
    sources=[sorted((src/(gun+'_'+v)).glob('*.png')) for v in ('Aimed','Lowered')]
    if not all(sources): continue
    frames=[]
    for n in range(max(map(len,sources))):
        frame=Image.new('RGB',(960,390),(12,15,19));d=ImageDraw.Draw(frame)
        d.text((12,8),title+' - reload from aim',fill='white');d.text((492,8),title+' - reload from lowered',fill='white')
        for j,files in enumerate(sources):
            im=Image.open(files[min(n,len(files)-1)]).convert('RGB').resize((480,360),Image.Resampling.LANCZOS)
            frame.paste(im,(j*480,30))
        frames.append(frame)
    # Shared palette keeps skin/weapon colors stable during motion.
    palette=Image.new('RGB',(960,390*6))
    for j in range(6):palette.paste(frames[j*(len(frames)-1)//5],(0,j*390))
    palette=palette.quantize(colors=192)
    frames=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in frames]
    durations=[40 if i%6<4 else 50 for i in range(len(frames))];durations[0]=350;durations[-1]=500
    frames[0].save(dst/(gun+'_Aimed_vs_Lowered.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
    # Mid action and final pose for each entry variant.
    for j,(label,files) in enumerate(zip(('Aimed','Lowered'),sources)):
        for k,n in enumerate((max(1,(len(files)-7)//2),len(files)-1)):
            x=(j*2+k)*360;y=row*295+25
            im=Image.open(files[n]).resize((360,270),Image.Resampling.LANCZOS);contact.paste(im,(x,y))
            cd.text((x+8,row*295+7),f'{title}: {label} / '+('reload' if k==0 else 'finished'),fill='white')
contact.save(dst/'All_Weapons_Reload_Variants.jpg',quality=93)
shutil.copy2(src/'PoseMetrics.csv',dst/'PoseMetrics.csv')
