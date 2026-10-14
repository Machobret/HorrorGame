"""Extract short weapon events from the supplied compilations. No synthetic layers."""
from pathlib import Path
import json, wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'ArtSource/Weapons/Audio'
OUT=ROOT/'Assets/Game/Weapons/Audio';OUT.mkdir(parents=True,exist_ok=True)
RATE=44100
def denoise(signal,background):
    # Subtract only stationary sound estimated immediately before the shot.
    # Preserve the transient and avoid a hard gate across its natural decay.
    size=1024;hop=256;window=np.sqrt(np.hanning(size))
    noise=np.lib.stride_tricks.sliding_window_view(background,size)[::hop]*window
    noise_power=np.mean(np.abs(np.fft.rfft(noise))**2,axis=0)
    padded=np.pad(signal,(size,size));result=np.zeros_like(padded);weight=np.zeros_like(padded)
    for start in range(0,len(padded)-size,hop):
        spectrum=np.fft.rfft(padded[start:start+size]*window)
        power=np.abs(spectrum)**2
        gain=np.sqrt(np.maximum(power-1.5*noise_power,0)/np.maximum(power,1e-12))
        gain=np.maximum(gain,.06)
        gain[:2]=0 # remove DC/sub-bass handling rumble
        result[start:start+size]+=np.fft.irfft(spectrum*gain)*window
        weight[start:start+size]+=window**2
    return (result/np.maximum(weight,1e-8))[size:size+len(signal)]
events=[
 ('Revolver_Shot','Revolver',4.846,6.65,-2,None),
 ('Revolver_Reload','Revolver',2.09,4.44,-7,None),
 ('HuntingRifle_Shot','HuntingRifle',20.728,21.155,-.8,(19.6,20.5)),
 ('FlareGun_Shot','FlareGun',5.452,7.1,-4,None),
 ('FlareGun_Reload','FlareGun',2.22,3.16,-8,None)
]
report=[]
for filename,weapon,start,end,peak_db,profile in events:
    original=np.load(SOURCE/(weapon+'_Source.npy'))
    signal=original[int(start*RATE):int(end*RATE)].astype('float64')
    if profile:signal=denoise(signal,original[int(profile[0]*RATE):int(profile[1]*RATE)])
    signal-=signal.mean()
    if filename == 'HuntingRifle_Shot':
        # Reduce the isolated transient's crest factor, retaining its raw decay.
        # Peak normalization alone left most of this clip much quieter than the revolver.
        signal=np.tanh(signal/max(float(np.max(np.abs(signal))),1e-9)*3)
    signal*=10**(peak_db/20)/max(float(np.max(np.abs(signal))),1e-9)
    fade_in=int(RATE*.001);fade_out=int(RATE*.03)
    signal[:fade_in]*=np.linspace(0,1,fade_in);signal[-fade_out:]*=np.linspace(1,0,fade_out)
    with wave.open(str(OUT/(filename+'.wav')),'wb') as output:
        output.setnchannels(1);output.setsampwidth(2);output.setframerate(RATE)
        output.writeframes((np.clip(signal,-1,1)*32767).astype('<i2').tobytes())
    report.append({'clip':filename,'source':weapon,'start':start,'end':end,'duration':len(signal)/RATE,'peak_db':peak_db,'noise_profile':profile,'peak':float(np.max(np.abs(signal)))})
(SOURCE/'edits.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
