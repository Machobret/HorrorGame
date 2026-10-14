"""Use a different isolated rifle shot from the user-supplied compilation."""
from pathlib import Path
import av, json, shutil, wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path('C:/Users/Mangis/Downloads/Assets/Guns/Real Gun Sounds-Wild West Rifles. Gunshot Sound Effect. ASMR.mp3')
RATE=44100
resampler=av.AudioResampler(format='fltp',layout='mono',rate=RATE);chunks=[]
for frame in av.open(str(SOURCE)).decode(audio=0):
    chunks.extend(f.to_ndarray().reshape(-1) for f in resampler.resample(frame))
chunks.extend(f.to_ndarray().reshape(-1) for f in resampler.resample(None))
original=np.concatenate(chunks)
start,end=53.225,54.05
signal=original[int(start*RATE):int(end*RATE)].astype(float)
background=original[int(52.2*RATE):int(53.1*RATE)]
size,hop=1024,256;window=np.sqrt(np.hanning(size))
noise=np.lib.stride_tricks.sliding_window_view(background,size)[::hop]*window
noise_power=np.mean(np.abs(np.fft.rfft(noise))**2,axis=0)
padded=np.pad(signal,(size,size));result=np.zeros_like(padded);weight=np.zeros_like(padded)
for offset in range(0,len(padded)-size,hop):
    spectrum=np.fft.rfft(padded[offset:offset+size]*window);power=np.abs(spectrum)**2
    gain=np.sqrt(np.maximum(power-1.5*noise_power,0)/np.maximum(power,1e-12));gain=np.maximum(gain,.06);gain[:2]=0
    result[offset:offset+size]+=np.fft.irfft(spectrum*gain)*window;weight[offset:offset+size]+=window**2
signal=(result/np.maximum(weight,1e-8))[size:size+len(signal)]
signal-=signal.mean();signal=np.tanh(signal/max(np.max(np.abs(signal)),1e-9)*1.6)
signal*=10**(-.8/20)/max(np.max(np.abs(signal)),1e-9)
attack=int(RATE*.001);release=int(RATE*.06)
signal[:attack]*=np.linspace(0,1,attack);signal[-release:]*=np.linspace(1,0,release)
target=ROOT/'Assets/Game/Weapons/Audio/HuntingRifle_Shot.wav'
backup=ROOT/'ArtSource/Weapons/Audio/HuntingRifle_Shot_Previous.wav'
if not backup.exists():shutil.copy2(target,backup)
with wave.open(str(target),'wb') as output:
    output.setnchannels(1);output.setsampwidth(2);output.setframerate(RATE)
    output.writeframes((signal*32767).astype('<i2').tobytes())
assert np.max(np.abs(signal))<1
report={'source':str(SOURCE),'start':start,'end':end,'duration':len(signal)/RATE,'noise_profile':[52.2,53.1],'peak_db':-.8,'rms_db':float(20*np.log10(np.sqrt(np.mean(signal**2))))}
(ROOT/'ArtSource/Weapons/Audio/rifle_alternative.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
