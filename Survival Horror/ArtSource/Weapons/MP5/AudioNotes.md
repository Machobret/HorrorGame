# MP5 sound authoring

The supplied model pack has no audio. These are authored cues made from sounds already included in the project's HorrorEngine template, not recordings of an actual MP5. No sounds were downloaded. The project/template asset licence continues to apply to the source material and edits.

## Sources and edits

- `Assets/AssetStore/HorrorEngine/ExampleContent/Weapons/Gun/GunFire.wav`: cropped to the initial report, played 12% faster, gently reduced low-frequency sustain, and shortened with an exponential decay and click-free fades. Output is a 0.245-second mono report. The source itself is already limited; the edit does not claim to recover clipped source information.
- `.../Gun/GunReload.wav`: separate magazine/click transients, cropped without altering playback pitch and placed against the reload's action timings.
- `.../AssaultRifle/AssaultRifleReload.wav`: two mechanical handle transients for locking/releasing the MP5's charging handle. This is an authored approximation.

## Reload synchronization

The reload clip is exactly 3.0 seconds. Times refer to each cue's strongest transient; the quieter lead-in begins slightly earlier.

| Reload action | Peak time |
| --- | ---: |
| Pull and lock charging handle | 0.60 s |
| Magazine release | 0.90 s |
| Magazine removed | 1.15 s |
| Fresh magazine seated | 1.95 s |
| Seating tug | 2.10 s |
| Palm releases charging handle | 2.40 s |
| Hands settle into grip | 2.70 s |

## Format and headroom

Both outputs are uncompressed PCM16, mono, 44.1 kHz. Shot peak is approximately 0.647 (-3.8 dBFS); reload peak is 0.730 (-2.7 dBFS). An eight-shot reference mix at 800 rounds/minute has peak 0.94. Neither authored WAV clips. Final perceived loudness still depends on Unity's source/mixer gain and the environment.

The existing project WAV importer was copied with fresh GUIDs, PCM import, mono, no normalization, preload enabled. `AudioMetrics.json` records the exact source cuts, output measurements and event placements. `AudioSourceWaveforms.png` was inspected to isolate mechanical events; `AudioOutputWaveforms.png` shows the edited cues. This pass used waveform inspection; no listening verification was available.

## Rebuild

Run `build_mp5_audio.py` with Python 3 plus NumPy and Pillow. The script finds the project relative to its own path, overwrites the two WAVs and metrics/waveform, and preserves existing generated importer GUIDs. It creates missing importer metadata from the existing Revolver_Shot WAV importer. No Unity invocation is required.
