# Ammo packaging revision — research and artwork

Five original diffuse atlases were generated with the built-in imagegen tool. Exact prompts are in GenerationPrompts.json; untouched source images are the *_Atlas_Source.png files. Runtime textures and six-face meshes/materials are under Assets/Game/Weapons/Models/AmmoPackaging. The import limit is 512 pixels with point filtering for deliberately coarse PSX texels.

## References

- Pistol remains the existing 9mm Luger ammo type. Federal American Eagle AE9DP shows short brass, round-nose copper FMJ cartridges and commercial carton packaging: https://www.federalpremium.com/handgun/american-eagle/american-eagle-handgun/11-AE9DP.html
- 12-gauge buckshot: Winchester Super-X XB1200, red plastic shotshell/brass head, folded closure, compact five-round carton: https://winchester.com/Products/Ammunition/Shotshell/Super-X/XB1200
- .357 Magnum: Federal AE357A, long straight rimmed brass case and jacketed soft-point load: https://www.federalpremium.com/handgun/american-eagle/american-eagle-handgun/11-AE357A.html
- .300 Winchester Magnum: Federal Power-Shok comparison, long bottleneck/belted case and pointed hunting projectile, commercial twenty-round carton: https://www.federalpremium.com/compare?cgid=Rifle&pid0=11-300WBS&pid1=11-P300WTC1
- Red aerial signal cartridge: Orion marine signal packaging, red signal cartridge with metal base and flat closure: https://www.orionsignals.com/project/12-gauge-high-performance-aerial-signal/

The labels are original generic designs, not copies of manufacturer trade dress. Package colours are an art-direction choice for quick recognition. The flare carton is a fictional emergency-supply presentation inspired by marine packaging; the Orion reference itself is sold in a sealed printed bag. Existing pickup quantities and ammo compatibility remain unchanged; no artwork states a cartridge count.

## Layout and rebuilding

Top row FRONT / BACK / LEFT END; bottom row RIGHT END / TOP LID / BOTTOM. Generation produced unequal panel widths, so AmmoPackagingBuilder measures each panel independently instead of assuming exact thirds. It pads UVs inside every panel, textures all six box faces and validates nondegenerate triangles. The five model prefabs are reused by pickup/examination assets; model renders supply inventory and pickup icons.

Run HorrorGame.Weapons.Editor.AmmoPackagingBuilder.RunBatch in an isolated Unity project to rebuild geometry, update models and render both sides and the five icons. The menu Apply Researched Ammo Packaging builds assets without replacing the current scene. Source asset GUIDs, root gameplay components, save IDs, pickup counts and colliders are preserved. WeaponAssetBuilder prefers the revised models and icons when rebuilding weapons.
## Completed integration and checks

The main project now uses the new six-face carton models for all five placed pickups and examination prefabs, plus matching rendered icons at the existing sprite GUIDs. English names identify 9mm Luger, 12-gauge shells, .357 Magnum, .300 Win Mag and red signal flares. Original item IDs, pickup amounts, ammo compatibility, stack limits and existing colliders are preserved.

Five dedicated *_Drop.prefab assets are registered in SpawnableSavableDB and assigned to AmmoData.DropPrefab, so inventory drops display the same packages instead of the generic case. Existing database registrations remain. Future weapon rebuilds prefer the revised models, icons and drop prefabs; the legacy side-texture menu delegates to the complete packaging builder once these models exist.

The original flare FBX bundle rendered stretched polygons, so the revised model uses three clean low-poly signal cartridges with brass rims, red casings, ivory identification bands and flat dark closures. No physics behavior was added to dropped ammo.

Unity 6000.3.11f1 validation: all five pickups/examinations and ten front/back views passed mesh/material/UV checks; all five drop prefabs resolved through the save database with distinct stable prefab IDs. Live Play Mode tested actual PickupDropper.Drop and PickupItem.Take for every ammo, counts, unique runtime IDs, spawn tracking, shared icons/examination models and in-memory pickup save-data restoration: 108 assertions passed. No on-disk player save was written. Build/runtime reports and logs are under Preview/. Original icons and serialized assets are retained under Before/.

Ammo_Packaging_Overview.jpg is assembled from real Unity model renders and the assigned inventory sprites. GenerationPrompts.json records the exact prompt set used with the built-in imagegen tool; runtime atlas PNGs are in Assets/Game/Weapons/Models/AmmoPackaging/.
