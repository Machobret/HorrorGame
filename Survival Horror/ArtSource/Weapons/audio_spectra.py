from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
folder=Path(__file__).resolve().parent/'Audio'
for name in ('Revolver','HuntingRifle','FlareGun'):
    signal=np.load(folder/(name+'_Source.npy'));rate=44100
    for index,start in enumerate(range(0,int(len(signal)/rate),15)):
        clip=signal[start*rate:(start+15)*rate]
        n=1024;hop=256
        frames=np.lib.stride_tricks.sliding_window_view(clip,n)[::hop]
        power=20*np.log10(np.maximum(np.abs(np.fft.rfft(frames*np.hanning(n))),1e-5))
        rows=np.geomspace(2,480,280).astype(int)
        values=np.clip((power[:,rows].T+30)/60,0,1)[::-1]
        rgb=np.stack([values*255,values**2*230,values**3*110],axis=-1).astype('uint8')
        im=Image.fromarray(rgb).resize((1500,280));image=Image.new('RGB',(1500,320),'#111111');image.paste(im,(0,30));draw=ImageDraw.Draw(image)
        for second in range(16):
            x=int(second/15*1500);draw.text((x,4),str(start+second),fill='white')
        image.save(folder/(name+'_Spectrum_'+str(start)+'.png'))
print('SPECTRA_OK')
