# House Toolkit

Open `Assets/Game/Levels/Chapter0/Scenes/StartingLevel.unity` to draw the new starting level. Select **House → Edit House**. The ground floor starts empty, with cream plaster inside, red brick outside, a wood floor and a white plaster ceiling. This is an authoring scene. The main menu still starts the existing game.

| Asset | Where to edit it |
| --- | --- |
| Plain wall thickness and default inserts | `Assets/Game/Environment/WallFamilies/Plain` |
| Storefront panels, meshes, door and window surrounds | `Assets/Game/Environment/WallFamilies/Storefront` |
| House-projected materials for walls and trims | `Assets/Game/Environment/ProjectedFinishes` |
| Baseboard profile and material | `Assets/Game/Environment/Trims` |
| Gameplay door prefabs and palette inserts | `Assets/Game/Doors` |
| Glazing and window inserts | `Assets/Game/Environment/Windows` |
| Road families, pavement and junction markings | `Assets/Game/Environment/Roads/TownStreet` |

Choose **GameObject → House Toolkit → Create House** or **Create Road** for another network. Assign **Town Street** in the road inspector. The embedded package under `Packages/com.manta.house-toolkit` contains the tools and runs independently of the separate toolkit project.

The plain family generates rectangular openings. Storefront uses 2 m, 1 m and 0.5 m panels, a 0.9 × 2.1 m door surround and a 1 × 1 m window surround. Even Spacing is its default. Assign the glass door or another compatible insert in either family. In the house properties, choose interior and exterior finishes independently. Add a material using **House Toolkit/Projected Finish** to **WallFinishes** to offer it in the picker.

Chapter 1’s APT and electricity puzzle scenes, and the recovery copy, retain their generated meshes, colliders and gameplay objects as static scene content. The material shader in `Assets/Game/Rendering/HorizontalWall.shader` still supplies authored UVs and projection for existing art. The baked wall component restores its saved vertical projection when a scene opens.
