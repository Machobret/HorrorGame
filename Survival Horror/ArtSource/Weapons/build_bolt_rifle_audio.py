"""Extract hunting rifle shot and reload from the supplied bolt-action recording."""
from pathlib import Path
import av,json,shutil,uuid,wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2];RATE=44100
source=Path('C:/Users/Mangis/Downloads/bolt action rifle sound effects.mp3')
r=av.AudioResampler(format='fltp',layout='mono',rate=RATE);chunks=[]
for frame in av.open(str(source)).decode(audio=0):chunks.extend(f.to_ndarray().reshape(-1) for f in r.resample(frame))
chunks.extend(f.to_ndarray().reshape(-1) for f in r.resample(None));original=np.concatenate(chunks)
folder=ROOT/'Assets/Game/Weapons/Audio';archive=ROOT/'ArtSource/Weapons/Audio';report=[]
for name,start,end,peak_db in [('HuntingRifle_Shot',1.84,3.63,-.8),('HuntingRifle_Reload',4.015,5.22,-4)]:
    target=folder/(name+'.wav');backup=archive/(name+'_BeforeBoltRecording.wav')
    if target.exists() and not backup.exists():shutil.copy2(target,backup)
    signal=original[int(start*RATE):int(end*RATE)].astype(float);signal-=signal.mean()
    signal*=10**(peak_db/20)/max(float(np.max(np.abs(signal))),1e-9)
    fadein=int(RATE*.001);fadeout=int(RATE*.025)
    signal[:fadein]*=np.linspace(0,1,fadein);signal[-fadeout:]*=np.linspace(1,0,fadeout)
    with wave.open(str(target),'wb') as output:
        output.setnchannels(1);output.setsampwidth(2);output.setframerate(RATE)
        output.writeframes((signal*32767).astype('<i2').tobytes())
    meta=Path(str(target)+'.meta')
    if not meta.exists():
        text=(folder/'Revolver_Reload.wav.meta').read_text()
        import re
        meta.write_text(re.sub(r'guid: \w+', 'guid: '+uuid.uuid4().hex,text,count=1))
    assert np.max(np.abs(signal))<1
    report.append({'clip':name,'source':str(source),'start':start,'end':end,'duration':len(signal)/RATE,'peak_db':peak_db})
# Wire the new reload clip; shot keeps its existing GUID.
import re
reload_guid=re.search(r'guid: (\w+)',(folder/'HuntingRifle_Reload.wav.meta').read_text())[1]
p=ROOT/'Assets/Game/Weapons/Data/HuntingRifle.asset';text=p.read_text()
text=re.sub(r'ReloadSound: \{fileID: 8300000, guid: \w+, type: 3\}',f'ReloadSound: {{fileID: 8300000, guid: {reload_guid}, type: 3}}',text)
p.write_text(text)
(archive/'bolt_rifle_edits.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
