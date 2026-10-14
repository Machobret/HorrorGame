# Hunting rifle and flare gun prototype

## Try them

Drag `Prefabs/Weapon_TestPickups.prefab` into a playable scene near the player.
It contains a loaded pistol (15 rounds), shotgun (3 shells), hunting rifle
(5 rounds), and flare gun (1 flare). Behind the rifle and flare gun are a
five-round rifle ammo box and three spare flares. The rifle box uses the
downloaded `PSXAmmoBoxes/PSXAmmoBoxes/Rifle Ammo` mesh and texture.
Use the normal pickup interaction, equip through the inventory, and use the
existing aim, fire and reload controls. Individual pickups are in `Prefabs`.

The rifle ammo FBX needs its imported root scale of 100 for centimetre-to-metre
conversion. Its pickup and examination prefabs retain that scale on the model
child, while their own roots stay at scale 1. Resetting that model child to scale
1 makes it about 100 times too small. The builder retains imported scale and
validates pickup size and floor placement.

Flare ammo is a bundle of three solid low-poly signal cartridges with worn red
cases, upright labels, brass rims/primers and a dark retaining band. This rebuilt
mesh uses metres directly and has a root scale of 1. It has its own
`FlareAmmo_Albedo.png` atlas, matching the flare gun's palette. The pickup still
adds three flare rounds. `ArtSource/Weapons/build_flare_ammo.py` rebuilds the
mesh and `render_theme.py --only-flare-ammo` updates its inventory icon.

The four item assets are registered in the GameManager's existing ItemDB.
The player's starting inventory and the user's level are not changed.

## Settings

| Weapon | Magazine | Body / weakpoint impact | Fire cycle | Reload |
| --- | --- | --- | --- | --- |
| Hunting rifle | 5 | 2 / 3 | 1.4 s | 2.2 s |
| Flare gun | 1 | 0.25 / 0.375 | 0.8 s | 1.8 s |

The rifle is accurate to 40 m and cycles its separate bolt mesh. Its scope is
part of the model; this prototype uses the existing aim system without a
dedicated scope view. The shotgun's player animations and audio are reused.
The flare gun reuses the pistol's player animations and audio.

`Data/HuntingRifle.asset` and `Data/FlareGun.asset` control magazine size, ammunition,
reload duration, sounds, animation overrides and hand attachment.
Attack assets in `Data` control impact damage. The weapon prefab's
`AttackMontage.Duration` controls the firing cycle.

`Effects/SignalFlare.prefab` controls the flare:

- Launch speed: `FlareGunAttack.LaunchSpeed` on the gun, default 18 m/s.
- Light: bright red, intensity 12, 12 m range, lasts 12 seconds with sparks,
  a red trail and a soft additive glow. `LightIntensity` controls brightness.
- Burn: 0.25 damage per second for 6 seconds on the enemy it hits.
- `EnableDamage`: disables both impact damage and burning; light remains active.
- Gravity, collision layers, radius, lifetime and burn duration are adjustable.

Flares stick to the first surface or enemy, stop at walls, ignore the shooter,
and pause with scaled game time. Burning preserves health/death events without
repeated hit animations. Flares are temporary scene effects and are not saved.
Enemy attraction is not part of this prototype.

## Source models and maintenance

The downloaded low-poly meshes and textures were converted from the Guns folder.
Separate bolt, barrel and scope meshes are retained. Loose cartridges were
removed from held models and a flare cartridge was imported as an ammo model.
Meshes are in metres, face +Z, and have a grip-centred origin.
Point-filtered textures preserve their PSX appearance.

The hunting rifle and flare gun use the template weapons' `HorrorEngine/UnlitAlphaCutout`
shader and shading baked into their albedo textures. The rifle has worn walnut and
blued steel; the flare gun has muted red enamel, chipped edges and a dark rubber
grip. Rifle textures import at up to 512 pixels because their atlas has narrow
islands; flare and scope textures import at up to 256 pixels. Inventory icons
remain 512-pixel sprites with bilinear filtering.

The texture comparison, generated masters, prompts and original backups are in
`ArtSource/Weapons/TextureTheme`. `render_theme.py` rerenders the existing gun
meshes and icons without rebuilding gameplay prefabs. Pass `--apply` to install
the approved masters again. `build_models.py` preserves these approved textures
when rebuilding geometry.

`ArtSource/Weapons/build_models.py` rebuilds models and renders inventory icons
using Blender. Its source paths refer to the local downloaded assets.
**Horror Game > Weapons > Build Rifle and Flare Prototype** recreates generated
prefabs and resets their prototype settings, while retaining existing asset
GUIDs and item IDs. Use it only when rebuilding, not after manual tuning.

Validation:

- `WeaponAssetBuilder.Validate`: item references, database IDs, attack/ammo
  wiring and mesh scale.
- `WeaponPlayValidation.RunBatch`: actual rifle/flare firing, projectile
  collision, wall blocking, shooter exclusion, burn amount/expiry, damage
  toggle, pause, lifetime, reload counts and inventory save fields.
