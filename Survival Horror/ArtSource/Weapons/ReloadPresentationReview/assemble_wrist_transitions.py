from pathlib import Path
from PIL import Image,ImageDraw
root=Path(r'C:/Users/Mangis/Documents/GitHub/HorrorGame/Survival Horror/ArtSource/Weapons/ReloadPresentationReview')
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/WristTransitionsArmFinal')
out=root/'WristTransitions';out.mkdir(exist_ok=True)
guns=['Gun','Shotgun','HuntingRifle','Revolver','FlareGun'];labels=['1911','Double-barrel shotgun','Hunting rifle','Revolver','Flare gun']
phases=[('lower','Aim to lowered'),('reload-lowered','Reload to lowered'),('reload-aimed-held','Reload to held aim')]
frames=[];durations=[]
for phase,title in phases:
 sources=[]
 for gun in guns:
  folder=src if gun!='FlareGun' else src.parent/'WristFlarePreviewFinal'
  files=sorted((folder/(gun+'_'+phase)).glob('*.png'))
  # Reload folders also contain entry captures. Show only the final contiguous
  # capture window, beginning at the actual return-state transition.
  start=0
  for j in range(1,len(files)):
   if int(files[j].stem)>int(files[j-1].stem)+1:start=j
  sources.append(files[start:])
 count=max(map(len,sources))
 for n in range(count):
  frame=Image.new('RGB',(1440,790),(12,15,19));d=ImageDraw.Draw(frame);d.text((12,8),title+' / actual game capture at 60 fps',fill='white')
  for j,(files,label) in enumerate(zip(sources,labels)):
   if not files:continue
   x=(j%3)*480;y=30+(j//3)*380
   im=Image.open(files[min(n,len(files)-1)]).convert('RGB').resize((480,360),Image.Resampling.LANCZOS);frame.paste(im,(x,y+20));d.text((x+10,y+4),label,fill='white')
  frames.append(frame);durations.append(20 if n%3!=1 else 10)
 durations[-1]=500
pal=Image.new('RGB',(1440,790*5))
for j in range(5):pal.paste(frames[j*(len(frames)-1)//4],(0,790*j))
pal=pal.quantize(colors=224);frames=[f.quantize(palette=pal,dither=Image.Dither.NONE) for f in frames]
frames[0].save(out/'Wrist_Transitions.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
print('Saved',len(frames),'captured transition frames')