# Temporary House Demo

Open `Scenes/TemporaryHouseDemo.unity` directly and press Play.
WASD moves, mouse looks, Escape releases the pointer, and click captures it again.
The scene contains the furnished kitchen, bedroom and connecting corridor, static mesh collisions, lighting, and a standalone player.

## Scope and removal

Everything authored for this demo is under `Assets/TemporaryHouseDemo/`.
Delete that folder and `Assets/TemporaryHouseDemo.meta` to remove it.
No demo scene is added to Build Profiles, Addressables, bootstraps, Resources, tags, layers, or project settings.
The demo scripts are separate assemblies with `autoReferenced: false`; there are no automatic initialization or import hooks.
The scene uses the project's existing URP and Input System packages without adding dependencies.
Doors are fixed in their open pose. There is no production gameplay integration, baked lighting, or NavMesh.

## Contents

- `Models/HouseLayout.fbx`: assembled furnished house, 81 mesh objects.
- `Models/AssetCatalog.fbx`: both original asset grids, 303 mesh objects representing 267 models, for browsing/reuse; not loaded into the demo scene.
- `Textures/` and `Materials/`: local textures and URP materials, with point filtering.
- `Prefabs/HouseLayout.prefab`: house geometry with static collision.
- `Scripts/`: demo-only walk-through controller.
- `Editor/`: explicit scene rebuild and validation tool, under Tools > Temporary House Demo.
- `Data/`: source import mapping and generated validation report.
- `Source/HouseSource.zip`: packed Blender source for editing, archived to prevent Unity's automatic .blend import.

Asset licensing remains that of the supplied source packs. The demo does not grant additional redistribution rights.

The project keeps its existing global render/input settings; the demo scene and its player do not reference shared gameplay prefabs or scripts.
To rebuild, use Tools > Temporary House Demo > Rebuild isolated scene. This replaces this demo scene and prefab; save other work first.
