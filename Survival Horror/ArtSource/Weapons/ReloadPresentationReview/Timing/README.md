# Reload audio timing

`FlareGun_Reload_Original.wav` preserves the supplied, previously assigned 0.940-second clip. It is copied only when absent; the build script always reads that backup. The Unity WAV retains its existing path and GUID.

Run `retime_flare_reload.py` with Python and NumPy to rebuild `Assets/Game/Weapons/Audio/FlareGun_Reload.wav`. It moves six intact sound events into a 2.300-second timeline. There is no time stretching, resampling, pitch shift, gain normalization, or synthetic sound. Format remains 44,100 Hz, mono, PCM16; peak remains approximately -8.00 dBFS.

## Flare cuts and placement

Cuts minimize centered 4 ms RMS between the six observed peaks, excluding 15 ms around each peak. Each segment has a 4 ms fade at both ends, reaching zero before the inserted silence. The quiet interior cuts range from -49.5 to -41.8 dBFS RMS. All original audio is retained across the six segments, with only those short fade envelopes altered.

| Event | Original peak (s) | Original segment (s) | New peak (s) | New segment (s) |
|---|---:|---:|---:|---:|
| 1 | 0.075 | 0.000000–0.126236 | 0.220 | 0.145011–0.271247 |
| 2 | 0.170 | 0.126236–0.255533 | 0.490 | 0.446236–0.575533 |
| 3 | 0.294 | 0.255533–0.333628 | 0.740 | 0.701542–0.779637 |
| 4 | 0.394 | 0.333628–0.441723 | 1.300 | 1.239637–1.347732 |
| 5 | 0.509 | 0.441723–0.600454 | 1.530 | 1.462721–1.621451 |
| 6 | 0.634 | 0.600454–0.940000 | 1.860 | 1.826463–2.166009 |

The final original tail ends at 2.166 seconds; the output ends at 2.300 seconds. The small sub-sample rounding in the table is below 0.023 ms. `FlareGun_Reload_Timing.json` records full precision and format measurements.

## Measurements of the five assigned clips before this retiming

Files were resolved from each weapon's ReloadSound GUID. Audible intervals below use 5 ms RMS blocks above -30 dB relative to the clip's peak RMS. These measurements describe sound energy; assigning a transient to a particular mechanical action is an animation-design inference.

| Weapon | Assigned source | Length (s) | Audible interval (s) | Prominent peaks (s) | Chosen animation total (s) |
|---|---|---:|---:|---|---:|
| 1911 | Template Weapons/Gun/GunReload.wav | 1.970542 | 0.000–1.400 | 0.075, 0.670, 1.160, 1.300 | 1.50 |
| Shotgun | Template Weapons/Shotgun/ShotgunReload.wav | 2.196813 | 0.495–1.950 | 0.590, 0.810, 1.075, 1.245, 1.615, 1.900 | 2.10 |
| Hunting rifle | Game/Weapons/Audio/HuntingRifle_Reload.wav | 1.205011 | 0.050–1.117 | 0.140, 0.254, 0.678, 0.868, 0.963, 1.068 | 1.30 |
| Revolver | Game/Weapons/Audio/Revolver_Reload.wav | 2.350000 | 0.085–2.010 | 0.130, 1.038, 1.806, 1.961 | 2.35 |
| Flare gun | Game/Weapons/Audio/FlareGun_Reload.wav (original) | 0.940000 | 0.045–0.908 | 0.075, 0.170, 0.294, 0.394, 0.509, 0.634 | 2.30, with retimed sound |

For the revolver's 2.35-second total, an ejection at 44% lands at 1.034 seconds; closing at 75.5–86.5% spans 1.774–2.033 seconds and covers the final two measured clicks. Flare event placement above preserves a deliberate reload pace while aligning separate mechanical sounds with the visible stages.

Only the flare audio and these source notes are changed by this script. Weapon durations, animator speed, and mechanical animation timing are integrated separately by the parent task.
