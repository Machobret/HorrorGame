# Custom weapon audio and pickup icons

Sources: the three MP3 recordings supplied in Downloads/Assets/Guns. Analysis used waveform levels and time/frequency spectra to identify isolated firing transients and mechanical sequences. The source files are unchanged. Source durations are listed in analysis.json; exact edit boundaries, noise profile and output levels are recorded in edits.json.

Delivered PCM WAV clips live in Assets/Game/Weapons/Audio (mono, 44.1 kHz, 16-bit, short fades, no clipped output):

| Clip | Source interval | Purpose |
| --- | --- | --- |
| Revolver_Shot | 4.846–6.650 s | Single shot with its decay |
| Revolver_Reload | 2.090–4.440 s | Mechanical reload sequence |
| HuntingRifle_Shot | 20.728–21.155 s | Short firing transient, trimmed before later mechanical events |
| FlareGun_Shot | 5.452–7.100 s | Flare launch and decay |
| FlareGun_Reload | 2.220–3.160 s | Short handling/loading sound |

The rifle now uses the user-supplied Downloads/bolt action rifle sound effects.mp3. Rebuild its shot and reload with build_bolt_rifle_audio.py after any earlier audio-generation scripts. Exact cuts are in bolt_rifle_edits.json. Leading and trailing silence are removed, short fades prevent clicks, and peak normalization leaves headroom without changing playback speed. The previous shot is backed up as HuntingRifle_Shot_BeforeBoltRecording.wav. Rifle firing remains 1.4 seconds and reloading remains 2.2 seconds; movement remains enabled for both.

The existing ShotAttack/FlareGunAttack reads WeaponData.ShotSound. PlayerStateReload reads ReloadSound. Existing attack montage audio is empty, so no second shot layer was added. Apply updates only the audio references and icon import settings; it does not rebuild weapon prefabs or alter pickup IDs. Unity menu: Horror Game / Weapons / Apply Custom Audio and Icons.

Pickup icons were rendered from the actual meshes using render_pickup_icons.py: transparent 1024px images, directional studio lights, a closer three-quarter view and angled rifles. Gameplay textures are unchanged. Image asset paths and GUIDs are preserved, so both inventory and pickup messages use the new icons.
