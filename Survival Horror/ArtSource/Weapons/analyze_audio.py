from pathlib import Path
import json, wave
import av, numpy as np
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/Weapons/Audio';OUT.mkdir(parents=True,exist_ok=True)
sources={
 'Revolver':'Revolver sound (sound effect) - Reloading, shots, getting.mp3',
 'HuntingRifle':'Real Gun Sounds-Wild West Rifles. Gunshot Sound Effect. ASMR.mp3',
 'FlareGun':'Flare Gun Sound - [ High Quality ].mp3'
}
report={}
for name,file in sources.items():
    container=av.open(str(Path('C:/Users/Mangis/Downloads/Assets/Guns')/file))
    resampler=av.AudioResampler(format='fltp',layout='mono',rate=44100)
    chunks=[]
    for frame in container.decode(audio=0):
        chunks.extend(f.to_ndarray().reshape(-1) for f in resampler.resample(frame))
    chunks.extend(f.to_ndarray().reshape(-1) for f in resampler.resample(None))
    signal=np.concatenate(chunks);np.save(OUT/(name+'_Source.npy'),signal)
    with wave.open(str(OUT/(name+'_Source.wav')),'wb') as wav:
        wav.setnchannels(1);wav.setsampwidth(2);wav.setframerate(44100);wav.writeframes((np.clip(signal,-1,1)*32767).astype('<i2').tobytes())
    step=441; blocks=signal[:len(signal)//step*step].reshape(-1,step)
    rms=np.sqrt(np.mean(blocks**2,axis=1));peak=np.max(np.abs(blocks),axis=1)
    candidates=np.where((rms[1:-1]>=rms[:-2])&(rms[1:-1]>rms[2:])&(rms[1:-1]>.10))[0]+1
    events=[]
    for index in sorted(candidates,key=lambda i:-rms[i]):
        if all(abs(index-e)>45 for e in events):events.append(int(index))
    events.sort()
    report[name]={'source':file,'duration':len(signal)/44100,'peak':float(np.max(np.abs(signal))), 'loud_events':[round(i*.01,2) for i in events]}
    width=1800;height=360
    image=Image.new('RGB',(width,height),'#15191d');draw=ImageDraw.Draw(image)
    for x in range(width):
        i=int(x/width*len(peak));value=float(peak[min(i,len(peak)-1)])
        draw.line((x,180-value*140,x,180+value*140),fill='#69c9b4')
    duration=len(signal)/44100
    for time in np.arange(0,duration,5 if duration<80 else 20):
        x=int(time/duration*width);draw.line((x,25,x,height-25),fill='#444444');draw.text((x+2,12),str(round(time,1)),fill='white')
    draw.text((10,height-20),name+' waveform; seconds',fill='white');image.save(OUT/(name+'_Waveform.png'))
(OUT/'analysis.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
