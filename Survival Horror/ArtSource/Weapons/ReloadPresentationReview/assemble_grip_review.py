from pathlib import Path
from PIL import Image,ImageDraw
import csv,shutil,json,numpy as np
here=Path(r'C:/Users/Mangis/Documents/GitHub/HorrorGame/Survival Horror/ArtSource/Weapons/ReloadPresentationReview');out=here/'GripCorrections';out.mkdir(exist_ok=True)
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/LongGunGripReview');before=Path(r'C:/Users/Mangis/AppData/Local/Temp/LongGunGripBeforeFix')
sheet=Image.new('RGB',(960,4*385),(12,15,19));d=ImageDraw.Draw(sheet)
for row,(gun,side) in enumerate([('HuntingRifle','Left'),('HuntingRifle','Right'),('Shotgun','Left'),('Shotgun','Right')]):
 for col,(folder,label) in enumerate([(before,'Before'),(src,'After')]):
  p=folder/f'{gun}_AimedHeld_Baseline_{side}.png';im=Image.open(p).resize((480,360),Image.Resampling.LANCZOS);sheet.paste(im,(col*480,row*385+25));d.text((col*480+8,row*385+7),f'{gun} {side.lower()} / {label}',fill='white')
sheet.save(out/'Long_Gun_Grips_Before_After.jpg',quality=93)
sources=[sorted((src/(g+'_Lowered')).glob('*.png')) for g in ['Gun','Shotgun','HuntingRifle']];frames=[]
for n in range(max(map(len,sources))):
 f=Image.new('RGB',(960,265),(12,15,19));draw=ImageDraw.Draw(f)
 for col,(files,label) in enumerate(zip(sources,['1911','Shotgun','Hunting rifle'])):
  im=Image.open(files[min(n,len(files)-1)]).convert('RGB').resize((320,240),Image.Resampling.LANCZOS);f.paste(im,(col*320,25));draw.text((col*320+8,8),label+' / lowered reload',fill='white')
 frames.append(f)
palette=Image.new('RGB',(960,265*6))
for j in range(6):palette.paste(frames[j*(len(frames)-1)//5],(0,j*265))
palette=palette.quantize(colors=192);frames=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames];durations=[40 if j%6<4 else 50 for j in range(len(frames))];durations[0]=300;durations[-1]=500
frames[0].save(out/'Lowered_Reload_Pocket_Reach.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
shutil.copy2(src/'PoseMetrics.csv',out/'PoseMetrics.csv')
for file in src.glob('*_GripProbe.json'):shutil.copy2(file,out/file.name)
print('Saved grip comparison, reload GIF and pose probes')
