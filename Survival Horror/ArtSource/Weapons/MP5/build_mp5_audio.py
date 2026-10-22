"""Author MP5 sound cues from existing licensed project sounds. Requires NumPy/Pillow.
Run from any directory. Does not download sounds or alter Unity import settings.
"""
from pathlib import Path
import json, wave, re, uuid
import numpy as np
from PIL import Image, ImageDraw
ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Assets/Game/Weapons/Audio"
NOTES = Path(__file__).resolve().parent
SOURCE = ROOT / "Assets/AssetStore/HorrorEngine/ExampleContent/Weapons"
RATE = 44100

def load(relative):
    with wave.open(str(SOURCE / relative), "rb") as w:
        if w.getsampwidth() != 2:
            raise ValueError("Only PCM16 source audio is supported")
        a = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").reshape(-1, w.getnchannels()).mean(axis=1) / 32768.
        sr = w.getframerate()
    if sr != RATE:
        a = np.interp(np.arange(round(len(a) * RATE / sr)) * sr / RATE, np.arange(len(a)), a)
    return a

def fade(a, attack=.003, release=.015):
    a = a.copy()
    n = min(len(a), round(attack * RATE)); a[:n] *= np.linspace(0, 1, n)
    n = min(len(a), round(release * RATE)); a[-n:] *= np.linspace(1, 0, n)
    return a

def write(name, a):
    if np.max(np.abs(a)) >= 1: raise ValueError("Clipping in " + name)
    path = OUT / (name + ".wav")
    with wave.open(str(path), "wb") as w:
        w.setparams((1, 2, RATE, len(a), "NONE", "not compressed"))
        w.writeframes(np.round(a * 32767).astype("<i2").tobytes())
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        template = (OUT / "Revolver_Shot.wav.meta").read_text()
        meta.write_text(re.sub(r"guid: [0-9a-f]+", "guid: " + uuid.uuid4().hex, template))
    return {"file": str(path.relative_to(ROOT)).replace("\\", "/"), "duration": len(a) / RATE,
            "peak": float(np.max(np.abs(a))), "rms": float(np.sqrt(np.mean(a*a))), "pcm": "mono signed 16-bit / 44100 Hz"}

# Tighten the template pistol report into a short, brighter automatic-weapon cue.
# The source is already limited; gain changes prevent new output clipping.
source = load("Gun/GunFire.wav")
t = np.arange(round(.245 * RATE)) / RATE
shot = np.interp((.006 + t * 1.12) * RATE, np.arange(len(source)), source)
shot -= np.convolve(shot, np.ones(81)/81, "same") * .40
shot *= np.exp(-np.maximum(t-.045, 0) * 13)
shot = fade(shot, .0015, .035)
shot *= .80 / np.max(np.abs(shot))
# At 800rpm successive short reports overlap; preserve headroom in this reference mix.
burst = np.zeros(round(8 * .075 * RATE) + len(shot))
for i in range(8):
    j = round(i * .075 * RATE); burst[j:j+len(shot)] += shot
scale = min(1., .94 / np.max(np.abs(burst))); shot *= scale; burst *= scale

reload = np.zeros(3 * RATE)
# Time values are the prominent transient/peak, not the beginning of a silent clip.
events = [
    ("charging handle locked rearward", .60, "AssaultRifle/AssaultRifleReload.wav", .19, .365, .50),
    ("magazine release", .90, "Gun/GunReload.wav", 1.08, 1.19, .24),
    ("magazine withdrawn", 1.15, "Gun/GunReload.wav", .00, .17, .31),
    ("new magazine seated", 1.95, "Gun/GunReload.wav", .60, .77, .59),
    ("magazine seating tug", 2.10, "Gun/GunReload.wav", .00, .17, .24),
    ("palm releases charging handle", 2.40, "AssaultRifle/AssaultRifleReload.wav", 1.075, 1.225, .73),
    ("settle into grip", 2.70, "Gun/GunReload.wav", 1.26, 1.385, .20),
]
report = {"authoring": "Edited project template recordings; not an authentic MP5 field recording.", "events": []}
for name, time, source_name, start, end, gain in events:
    a = load(source_name)[round(start*RATE):round(end*RATE)]
    a -= np.mean(a)
    a = fade(a, .003, .012)
    peak = int(np.argmax(np.abs(a)))
    a *= gain / np.max(np.abs(a))
    at = round(time * RATE) - peak
    if at < 0 or at + len(a) > len(reload): raise ValueError("Event outside reload")
    reload[at:at+len(a)] += a
    report["events"].append({"action": name, "transient_seconds": time, "source": source_name,
                              "source_crop_seconds": [start,end], "start_seconds": at/RATE, "gain_peak": gain})
report["outputs"] = [write("MP5_Shot", shot), write("MP5_Reload", reload)]
report["800rpm_eight_round_reference_mix_peak"] = float(np.max(np.abs(burst)))
(NOTES / "AudioMetrics.json").write_text(json.dumps(report, indent=2) + "\n")
# Actual output waveforms make cuts, event timing and headroom reviewable.
im = Image.new("RGB", (1500, 620), "#171c20"); d = ImageDraw.Draw(im)
for row, (name, a) in enumerate([("MP5 shot / 800rpm burst headroom", shot), ("MP5 reload / 3 seconds", reload)]):
    top = 35 + row*295; d.text((25, top), name, fill="white")
    for x in range(1420):
        block=a[int(x*len(a)/1420):int((x+1)*len(a)/1420)]
        peak=float(np.max(np.abs(block))) if len(block) else 0
        d.line((40+x, top+115-90*peak, 40+x, top+115+90*peak), fill="#70bcc6")
    duration=len(a)/RATE
    for sec in np.arange(0,duration+.001,.05 if row==0 else .2):
        x=40+1420*sec/duration; d.text((x,top+220), f"{sec:.2f}", fill="#c3ccce")
    if row==1:
        for e in report["events"]:
            x=40+1420*e["transient_seconds"]/duration
            d.line((x,top+26,x,top+205),fill="#c5995b")
im.save(NOTES / "AudioOutputWaveforms.png")
print(json.dumps(report, indent=2))
