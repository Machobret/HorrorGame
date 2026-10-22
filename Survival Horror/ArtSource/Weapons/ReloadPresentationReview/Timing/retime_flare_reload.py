"""Retime the existing flare reload by moving intact events; no resampling/pitch change.
Run with Python + NumPy from any working directory. The backup is written once.
"""
from pathlib import Path
import json
import shutil
import wave
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = Path(__file__).resolve().parents[4]
TARGET = ROOT / 'Assets/Game/Weapons/Audio/FlareGun_Reload.wav'
ORIGINAL = HERE / 'FlareGun_Reload_Original.wav'
SOURCE_PEAKS = np.array([.075, .170, .294, .394, .509, .634])
DESTINATION_PEAKS = np.array([.220, .490, .740, 1.300, 1.530, 1.860])
DURATION = 2.300
FADE_SECONDS = .004

if not ORIGINAL.exists():
    shutil.copy2(TARGET, ORIGINAL)
with wave.open(str(ORIGINAL), 'rb') as wav:
    rate, channels, width = wav.getframerate(), wav.getnchannels(), wav.getsampwidth()
    assert width == 2, 'Expected the original PCM16 reload WAV'
    source = np.frombuffer(wav.readframes(wav.getnframes()), dtype='<i2').astype(np.float64).reshape(-1, channels) / 32768
assert len(source) / rate < 1.1, 'Backup appears to contain a previously retimed reload'

# Quietest 4ms energy window between each pair of mechanical transients.
window = max(1, round(rate * FADE_SECONDS))
power = np.mean(source ** 2, axis=1)
energy = np.convolve(power, np.ones(window) / window, mode='same')
cuts = [0]
for left, right in zip(SOURCE_PEAKS[:-1], SOURCE_PEAKS[1:]):
    lower, upper = round((left + .015) * rate), round((right - .015) * rate)
    cuts.append(lower + int(np.argmin(energy[lower:upper])))
cuts.append(len(source))
output = np.zeros((round(DURATION * rate), channels), np.float64)
report = dict(source=str(ORIGINAL.relative_to(ROOT)), destination=str(TARGET.relative_to(ROOT)),
              sample_rate=rate, channels=channels, sample_width_bits=width * 8,
              original_duration=len(source) / rate, output_duration=len(output) / rate,
              fade_seconds=FADE_SECONDS, cut_seconds=[index/rate for index in cuts], events=[])
for event, (start, end, peak, destination) in enumerate(zip(cuts[:-1], cuts[1:], SOURCE_PEAKS, DESTINATION_PEAKS)):
    segment = source[start:end].copy()
    fade = min(window, len(segment) // 2)
    segment[:fade] *= np.linspace(0, 1, fade)[:, None]
    segment[-fade:] *= np.linspace(1, 0, fade)[:, None]
    destination_start = round((destination - peak) * rate) + start
    destination_end = destination_start + len(segment)
    assert 0 <= destination_start < destination_end <= len(output)
    assert not np.any(output[destination_start:destination_end]), 'Event segments overlap'
    output[destination_start:destination_end] = segment
    report['events'].append(dict(index=event+1, source_peak_seconds=float(peak),
        destination_peak_seconds=float(destination), source_start_seconds=start/rate,
        source_end_seconds=end/rate, destination_start_seconds=destination_start/rate,
        destination_end_seconds=destination_end/rate,
        cut_start_rms_dbfs=float(20*np.log10(max(np.sqrt(energy[start]), 1e-12))),
        cut_end_rms_dbfs=float(20*np.log10(max(np.sqrt(energy[min(end,len(energy)-1)]), 1e-12)))))

# No gain normalization: retain the original -8dBFS peak and event dynamics.
encoded = np.clip(np.rint(output * 32768), -32768, 32767).astype('<i2')
with wave.open(str(TARGET), 'wb') as wav:
    wav.setnchannels(channels); wav.setsampwidth(width); wav.setframerate(rate)
    wav.writeframes(encoded.tobytes())
report['peak_dbfs'] = float(20*np.log10(max(np.max(np.abs(output)), 1e-12)))
report['last_event_tail_end_seconds'] = report['events'][-1]['destination_end_seconds']
(HERE/'FlareGun_Reload_Timing.json').write_text(json.dumps(report, indent=2)+'\n')
print(json.dumps(report, indent=2))
