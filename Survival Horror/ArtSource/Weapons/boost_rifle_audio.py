"""Increase the clean rifle shot's body without mixing in other sounds."""

# Shared paths keep generated assets beside their owning weapon.
import sys as _sys
from pathlib import Path as _Path
_sys.path.insert(0, str(next(p for p in _Path(__file__).resolve().parents if (p / "ProjectSettings").is_dir()) / "Tools" / "Art"))
from weapon_paths import weapon_asset
from pathlib import Path
import shutil,wave
import numpy as np
ROOT=Path(__file__).resolve().parents[2]
target=weapon_asset('Audio', str('HuntingRifle_Shot.wav'))
backup=ROOT/'ArtSource/Weapons/Audio/HuntingRifle_Shot_BeforeLoudness.wav'
backup.parent.mkdir(exist_ok=True)
if not backup.exists():shutil.copy2(target,backup)
with wave.open(str(backup)) as w:
 rate=w.getframerate();signal=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float)/32768
before=20*np.log10(np.sqrt(np.mean(signal**2)))
signal=np.tanh(signal/max(np.max(np.abs(signal)),1e-9)*3)
signal*=10**(-.8/20)/max(np.max(np.abs(signal)),1e-9)
fade=int(rate*.01);signal[-fade:]*=np.linspace(1,0,fade)
with wave.open(str(target),'wb') as w:
 w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes((signal*32767).astype('<i2').tobytes())
print('RIFLE_SHOT_LOUDNESS',round(before,1),'->',round(20*np.log10(np.sqrt(np.mean(signal**2))),1),'dB RMS; peak -0.8dB, no clipping')
