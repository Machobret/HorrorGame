# Weapon media captures

Weapons_Side_By_Side.png shows fresh Unity Play-mode captures of the 1911, double-barrel shotgun, magnum revolver, hunting rifle and flare gun. The flare gun uses equipped scale .0074 and the revised grip. Weapons_And_Flare_Labeled.png preserves the previous six-panel layout with a flare illumination panel.

FlareGun_Firing.gif is a real character shot triggered through aim/fire input in FlareGun_TestRoom. It contains 121 frames at 24 fps, including the recoil, projectile flight, gravity arc, impact and red surface lighting. The recorded flare travelled 5.27 metres before landing. GIF colours use one shared palette, and the game's PSX rendering remains enabled. No demonstration projectile or generated imagery is used.

The editor-only capture source is saved under ../GripAudit/WeaponProgressCapture.cs. Run it inside an isolated Unity copy from an Editor folder; the output path is declared in the source. assemble_weapon_media.py assembles its frames and the five stills. Raw PNG frames remain in the temporary validation project.

Weapons_And_Ammo_Ground.png combines five individually framed Unity renders of the actual weapon/ammo pickup prefabs on a concrete floor. Order: 1911, shotgun, revolver, hunting rifle, flare gun. It contains no player or UI. The preview uses a higher virtual rendering resolution and reduced dithering to expose the existing point-filtered textures. These presentation settings and object placements exist only in the isolated capture session; gameplay assets are unchanged. WeaponGroundCapture.cs and assemble_ground_models.py preserve the reproduction sources.
