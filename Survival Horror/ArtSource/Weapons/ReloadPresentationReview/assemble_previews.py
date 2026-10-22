from pathlib import Path
from PIL import Image, ImageDraw
import csv, shutil
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/ReloadPresentationReview')
dst=Path(__file__).resolve().parent
for gun in ('FlareGun','Revolver'):
    files=sorted((src/(gun+'_Frames')).glob('*.png'))
    if not files: continue
    frames=[Image.open(p).convert('RGB').resize((720,540),Image.Resampling.LANCZOS) for p in files]
    palette=Image.new('RGB',(720,540*min(8,len(frames))))
    for i in range(min(8,len(frames))): palette.paste(frames[int(i*(len(frames)-1)/7)],(0,i*540))
    palette=palette.quantize(colors=192)
    frames=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in frames]
    durations=[40 if i%6<4 else 50 for i in range(len(frames))]
    durations[-1]=350
    frames[0].save(dst/(gun+'_Reload.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
    sheet=Image.new('RGB',(1440,560),(15,17,20));draw=ImageDraw.Draw(sheet)
    for i,n in enumerate([1,3,5,7]):
        p=src/(gun+'_Phase_'+str(n)+'.png')
        im=Image.open(p).resize((360,270),Image.Resampling.LANCZOS)
        sheet.paste(im,(i*360,40)); draw.text((i*360+10,15),['Open','Extract','Insert','Close'][i],fill=(230,230,230))
    sheet=sheet.crop((0,0,1440,310));sheet.save(dst/(gun+'_Reload_Phases.jpg'),quality=92)
shutil.copy2(src/'CaptureTimes.csv',dst/'CaptureTimes.csv')
