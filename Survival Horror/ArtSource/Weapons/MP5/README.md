# Standard MP5 integration

## Assets and testing
- Equip data: `Assets/Game/Weapons/Data/MP5.asset`
- Equipped model: `Assets/Game/Weapons/Prefabs/MP5.prefab`
- Placeable weapon pickup: `Assets/Game/Weapons/Prefabs/MP5_Pickup.prefab`
- SMG ammo pickup: `Assets/Game/Weapons/Prefabs/SMGAmmo_Pickup.prefab`
- Ammo drop/examination models and inventory icon are included.
- `Weapon_TestPickups.prefab` includes both MP5 and SMG ammunition beside the existing weapons.

The fixed-stock standard model was extracted from the supplied PSX MP5 pack. The compact and suppressed variants were excluded. Original 240x96 UV texture artwork is preserved in a lossless PNG with point filtering; model coordinates are baked to metres, with an approximately 68cm overall length. `MP5_extract.py` records the conversion. OBJ object groups preserve the separate magazine. The charging handle is split from a runtime copy of the original textured body mesh; imported mesh assets are never changed at runtime.

## Gameplay
Initial game tuning: 30-round magazine, 800 rounds/min automatic fire, 1 base damage, 28m range, 3-second reload, 120-round reserve stack. These are tunable on the weapon data, AttackRate, and Attack_MP5 assets. Shot damage/range are game balance values, not real weapon measurements.

The existing manual-reload rules apply: empty triggers never reload automatically. Reloading allows walking and turning, blocks sprinting, and consumes reserve ammunition only at completion. Combining ammunition and weapon in the inventory queues the physical reload after closing inventory. A reload started while aimed returns to aiming only if aim was held continuously; a lowered reload stays lowered.

The MP5 uses separate blue SMG ammunition for game readability, although both it and the pistol use 9mm cartridges. Pistol cartons are red and labelled Standard rounds. Shotgun cartons are green, magnum black with red accents, flares orange, rifle white. All six have full front/back/side/top/bottom panels and matching pickup/inventory icons.

## Animation and audio
Shoulder-supported aiming and lowered holding blend through MP5HandlingPose. MP5ReloadVisual owns both arms during reload: handle lock, visible magazine removal, retrieval, insertion/tug, palm release, and return to the handguard. The handle does not move for each shot. Reload has separate aimed/lowered work positions and blends into the correct ending state. The short directional recoil clips use the beginning of the template's shooting motion and recover before the next shot; no shotgun pumping section is played.

Reference sources: [HK MP5 technical specifications](https://www.heckler-koch.com/en/Products/Military%20and%20Law%20Enforcement/Submachine%20guns/MP5) and [HK instructor material](https://s3.amazonaws.com/hk-manuals/files/Instruction/Training_Division/HK_SUBMACHINE_GUN_INSTRUCTOR.pdf), especially printed pages 36–41 for stance and reload. Timing is authored for the game, not motion capture or a measured real-world reload.

The model pack contains no sounds. MP5 shot/reload audio was authored from the project's existing template sounds; see AudioNotes.md and build_mp5_audio.py. It is an approximation, not an authentic MP5 recording.

## Texture provenance
Ammo palette artwork was edited using the built-in image generation tool, then mapped to six-face carton meshes and rendered in Unity for icons. Exact prompts and source PNGs are in `../AmmoPackaging/MP5PaletteRevision/GenerationPrompts.json`. Unity images are actual game asset renders, not generated previews.

## Rebuild and validation
Use the menu `Horror Game > Weapons > Build Standard MP5` to rebuild just this weapon. Afterward `AmmoPackagingBuilder.RunBatch` rebuilds six cartons and renders icons; its batch entry is intended for an isolated project. `MP5IntegrationValidation.RunIconBatch` renders and assigns the weapon icon.

Validation helper sources are kept here outside Assets. Copy into an isolated project's Editor folder to run their documented batch entry points. They exercise actual input, reload state, and pickups without writing a user save. Validation outputs are under Preview and the ammo packaging Preview folder.
