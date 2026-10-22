from pathlib import Path
from fractions import Fraction
from PIL import Image
import av,csv,wave,numpy as np
root=Path(__file__).resolve().parents[3]
review=Path(__file__).resolve().parent
src=Path(r'C:/Users/Mangis/AppData/Local/Temp/FlareSceneFinalValidation/ReloadTimingSeamReview')
out=review/'Timing';out.mkdir(exist_ok=True)
rows=list(csv.DictReader((src/'PoseMetrics.csv').open()))
for gun in ('FlareGun','Revolver'):
 case=gun+'_AimedHeld';rr=[r for r in rows if r['case']==case]
 if not rr:continue
 times=np.array([float(r['gameTime']) for r in rr]);t0=times[0]
 start=next(float(r['gameTime'])-float(r['reloadTime']) for r in rr if float(r['reloadTime'])>=0)
 count=round((times[-1]-t0+.5)*60)+1
 dest=out/(gun+'_Reload_With_Sound.mp4')
 container=av.open(str(dest),'w');video=container.add_stream('libx264',rate=60)
 video.width=960;video.height=720;video.pix_fmt='yuv420p';video.options={'crf':'20','preset':'fast'}
 audio=container.add_stream('aac',rate=44100);audio.layout='mono';audio.bit_rate=128000
 for n in range(count):
  index=max(0,min(len(rr)-1,int(np.searchsorted(times,t0+n/60,side='right')-1)))
  image=Image.open(src/case/(int(rr[index]['frame']).__format__('04d')+'.png')).convert('RGB')
  frame=av.VideoFrame.from_ndarray(np.asarray(image),format='rgb24');frame.pts=n;frame.time_base=Fraction(1,60)
  for packet in video.encode(frame):container.mux(packet)
 for packet in video.encode():container.mux(packet)
 with wave.open(str(root/'Assets/Game/Weapons/Audio'/f'{gun}_Reload.wav'),'rb') as wav:
  assert wav.getsampwidth()==2 and wav.getnchannels()==1 and wav.getframerate()==44100
  signal=np.frombuffer(wav.readframes(wav.getnframes()),dtype='<i2').astype('float32')/32768
 samples=np.zeros(round(count/60*44100),dtype='float32');offset=max(0,round((start-t0)*44100));length=min(len(signal),len(samples)-offset);samples[offset:offset+length]=signal[:length]
 frame=av.AudioFrame.from_ndarray(samples.reshape(1,-1),format='fltp',layout='mono');frame.sample_rate=44100;frame.pts=0;frame.time_base=Fraction(1,44100)
 for packet in audio.encode(frame):container.mux(packet)
 for packet in audio.encode():container.mux(packet)
 container.close();print(dest.name,'duration',round(count/60,3),'audio offset',round(start-t0,4))
