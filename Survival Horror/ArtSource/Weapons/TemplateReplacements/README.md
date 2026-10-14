# Template weapon model replacements

The pistol now uses PSXMiscGuns/1911/1911.fbx (full-size version). The shotgun uses PSXShotgunPack[FIXED]/Files/DoubleBarrelShotgun/Fbx/DoubleBarrelShotgun.fbx (full-length side-by-side version).

The replacement geometry is written to the original Gun.fbx and Shotgun.fbx paths with their existing asset GUIDs. Mesh parts are joined into a single static mesh to retain the original root/transform/renderer subasset identities. Loose ammunition and the spare pistol magazine are excluded. Existing weapon scripts, item IDs, audio, ammo types and capacities remain configured as before. This is a visual replacement; a break-action reload animation has not been added.

Textures use the downloaded source atlases with a 128px import limit, point filtering and no texture compression. The 1024px transparent icons are rendered from the actual meshes with those coarse textures. Original asset copies and configuration snapshots are saved here for reference. Downloaded originals are unchanged.

Build geometry and icons with Blender `--background --python ArtSource/Weapons/build_template_replacements.py`, then run apply_template_replacements.py with Python. The apply script keeps existing identifiers, remaps the materials, moves the muzzle sockets to the new barrel tips, places pickup meshes on the floor and centers examination models. It updates the pistol/shotgun names and descriptions to match their visuals.

The original template prefabs and the pistol/shotgun pickups in Weapon_TestPickups inherit these models. Both inventory and pickup notifications use the existing icon references, whose PNG content is now replaced. Check the character grip pose in the user's level before final art approval: the existing template character animation is reused.

Validation: Unity 6000.3.11f1 in an isolated project passed both model imports and all six held/pickup/examination prefab geometry checks. Confirmed retained model subasset IDs, material connections, 128px point-filtered imports, weapon dimensions, pickup floor placement, centered examination models and muzzle alignment. Character animation grip still needs an in-level visual check.
