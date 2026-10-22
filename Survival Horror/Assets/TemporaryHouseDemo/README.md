# Temporary House Demo

Open `Scenes/TemporaryHouseDemo.unity` directly and press Play. Use the project's normal player controls.

The scene uses existing gameplay prefabs:

- `HECore.prefab` supplies the native game manager, camera system, input, UI and other core services.
- `PlayerSpawnPoint.prefab` selects the existing `CharacterPlayer` data; the native game manager spawns the controllable character.
- Four `FixedCameraWithShape.prefab` instances cover the kitchen, bedroom and two corridor sections. Scene-instance overrides set fixed poses, lenses and room trigger shapes, disable follow to keep cameras inside the rooms, and replace the legacy Cinemachine camera component with the project's Cinemachine 3 camera.
- `Monster_Runner.prefab` is placed at the north end of the corridor on a demo-local baked NavMesh.

There is no demo player controller, first-person camera, mouse-look or custom input implementation. Player movement, camera switching and enemy behavior come from the project.

## Removal

Delete `Assets/TemporaryHouseDemo/` and `Assets/TemporaryHouseDemo.meta`.
The demo references existing gameplay assets in one direction. Existing scenes, prefabs, build profiles, Addressables and project settings do not reference the demo.
No shared prefab or gameplay implementation is modified by this demo.

## Assets

`Models/HouseLayout.fbx` contains the furnished 81-mesh house, with collisions in `Prefabs/HouseLayout.prefab`.
`Models/AssetCatalog.fbx` contains all 267 source models (303 meshes) in their browsing grids; it is not loaded into the level.
Textures and URP materials are local. `Navigation/` holds the baked NavMesh and native camera trigger meshes.
`Source/HouseSource.zip` contains the packed Blender source without invoking Unity's .blend importer.
Doors remain in their authored open pose.

The Editor assembly and tests are isolated with `autoReferenced: false`. Tools > Temporary House Demo > Rebuild isolated scene explicitly rebuilds this demo; save other work first.
The targeted Edit Mode test `HouseWalkthroughTests.NativePlayerEnemyAndRoomCamerasWork` enters Play Mode to check native spawning, input, room cameras and navigation.

Source-pack licensing continues to apply to all imported assets.
