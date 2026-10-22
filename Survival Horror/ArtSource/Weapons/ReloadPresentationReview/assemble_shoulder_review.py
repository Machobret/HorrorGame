from pathlib import Path
from PIL import Image, ImageDraw
import csv,json,math,shutil
import numpy as np
root=Path(r'C:/Users/Mangis/Documents/GitHub/HorrorGame/Survival Horror/ArtSource/Weapons/ReloadPresentationReview')
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/ReloadShoulderReview')
out=root/'ShoulderCorrection';out.mkdir(exist_ok=True)
rows=list(csv.DictReader((src/'PoseMetrics.csv').open()))
guns=['Gun','Shotgun','HuntingRifle'];labels=['1911','Shotgun','Hunting rifle'];report={}
sheet=Image.new('RGB',(1440,1080),(12,15,19));draw=ImageDraw.Draw(sheet)
for j,(gun,label) in enumerate(zip(guns,labels)):
 r=[r for r in rows if r['case']==gun+'_Lowered'];active=[x for x in r if .15<float(x['reloadTime'])<.65]
 pocket=min(active,key=lambda x:float(x['leftHandY']));idx=int(pocket['frame'])
 for k,ix in enumerate([max(0,idx-5),idx,idx+8]):
  im=Image.open(src/(gun+'_Lowered')/f'{ix:04}.png').convert('RGB').resize((480,330),Image.Resampling.LANCZOS)
  sheet.paste(im,(k*480,j*360+30));draw.text((k*480+10,j*360+10),f'{label} / {ix/60:.2f}s',fill='white')
 def vec(row,p):return np.array([float(row[p+a]) for a in 'XYZ'])
 def angle(row):
  a=vec(row,'leftUpper')-vec(row,'leftElbow');b=vec(row,'leftHand')-vec(row,'leftElbow')
  return float(np.degrees(np.arccos(np.clip(np.dot(a,b)/(np.linalg.norm(a)*np.linalg.norm(b)),-1,1))))
 q0=np.array([float(r[0]['leftShoulderQ'+a]) for a in 'XYZW']);q1=np.array([float(pocket['leftShoulderQ'+a]) for a in 'XYZW'])
 report[gun]={'pocketTime':float(pocket['reloadTime']),'pocketElbowAngleDegrees':angle(pocket),'shoulderLocalChangeFromIdleDegrees':float(2*np.degrees(np.arccos(np.clip(abs(np.dot(q0,q1))/(np.linalg.norm(q0)*np.linalg.norm(q1)),-1,1)))),'maxElbowAngleDuringPocketWindow':max(map(angle,active))}
sheet.save(out/'Shoulder_Pocket_Sequence.jpg',quality=94)
sources=[sorted((src/(g+'_Lowered')).glob('*.png')) for g in guns];frames=[]
for n in range(0,max(map(len,sources)),2):
 frame=Image.new('RGB',(1200,325),(12,15,19));d=ImageDraw.Draw(frame)
 for j,(files,label) in enumerate(zip(sources,labels)):
  im=Image.open(files[min(n,len(files)-1)]).convert('RGB').resize((400,300),Image.Resampling.LANCZOS);frame.paste(im,(400*j,25));d.text((400*j+10,8),label+' / shoulder reach',fill='white')
 frames.append(frame)
pal=Image.new('RGB',(1200,325*6))
for j in range(6):pal.paste(frames[j*(len(frames)-1)//5],(0,325*j))
pal=pal.quantize(colors=224);frames=[f.quantize(palette=pal,dither=Image.Dither.NONE) for f in frames]
durations=[30 if i%3<2 else 40 for i in range(len(frames))];durations[0]=300;durations[-1]=500
frames[0].save(out/'Shoulder_Reloads.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
(out/'ShoulderMetrics.json').write_text(json.dumps(report,indent=2))
shutil.copy2(src/'PoseMetrics.csv',out/'PoseMetrics_60fps.csv')
print(json.dumps(report,indent=2))
