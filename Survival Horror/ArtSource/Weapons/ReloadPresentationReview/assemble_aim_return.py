from pathlib import Path
from PIL import Image, ImageDraw
import shutil
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/ReloadAimReturnReview')
dst=Path(__file__).resolve().parent/'AimReturn';dst.mkdir(exist_ok=True)
names={'Gun':'1911','Shotgun':'Shotgun','HuntingRifle':'Hunting rifle','Revolver':'Revolver','FlareGun':'Flare gun'}
variants=['AimedHeld','AimedReleased','Lowered'];labels=['Aim held: returns to aim','Aim released: stays lowered','Started lowered: stays lowered']
sheet=Image.new('RGB',(1200,5*325),(12,15,19));draw=ImageDraw.Draw(sheet)
for row,(gun,title) in enumerate(names.items()):
    sources=[sorted((src/(gun+'_'+v)).glob('*.png')) for v in variants]
    if not all(sources):continue
    for col,files in enumerate(sources):
        im=Image.open(files[-1]).convert('RGB').resize((400,300),Image.Resampling.LANCZOS)
        sheet.paste(im,(col*400,row*325+25));draw.text((col*400+8,row*325+7),title+' / '+labels[col],fill='white')
    if gun not in ('FlareGun','Revolver'):continue
    frames=[]
    for n in range(max(map(len,sources))):
        frame=Image.new('RGB',(1080,295),(12,15,19));d=ImageDraw.Draw(frame)
        for col,files in enumerate(sources):
            im=Image.open(files[min(n,len(files)-1)]).convert('RGB').resize((360,270),Image.Resampling.LANCZOS)
            frame.paste(im,(col*360,25));d.text((col*360+6,7),labels[col],fill='white')
        frames.append(frame)
    palette=Image.new('RGB',(1080,295*6))
    for i in range(6):palette.paste(frames[i*(len(frames)-1)//5],(0,i*295))
    palette=palette.quantize(colors=192)
    frames=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in frames]
    durations=[40 if i%6<4 else 50 for i in range(len(frames))];durations[0]=250;durations[-1]=600
    frames[0].save(dst/(gun+'_Aim_Return.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
sheet.save(dst/'All_Weapons_Return_Poses.jpg',quality=93)
shutil.copy2(src/'PoseMetrics.csv',dst/'PoseMetrics.csv')
