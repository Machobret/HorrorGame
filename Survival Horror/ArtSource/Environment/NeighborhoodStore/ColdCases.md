# Corner Market refrigeration

The four models share a 256 × 256 pixel atlas with original product labels and cabinet finishes. Each model has a single solid mesh and a second mesh for clear glass. Upright cases hold soda cans, juice bottles, or cartons and yogurt cups. The chest freezer holds wrapped desserts and tubs below sliding glass lids. The lids, doors, and stock are static scenery.

The references show a dense cooler wall, different case types, and a small coffee station near checkout. The narrow aisle and the artwork were designed for Building01. [New York storefront and interior photos](https://cbmanhattan.cityofnewyork.us/cb4/wp-content/uploads/sites/10/2023/10/10-BLP-Letter-to-SLA-re-455-W-37th-Street-%E2%80%93-G.F.-37-Rest-Group-LLC.pdf) · [Brooklyn shop and coffee counter, photo 7](https://extapps.dec.ny.gov/data/DecDocs/C224375/Application.BCP.C224375.2021-09-01.Phase%201%20ESA.pdf) · [True product portfolio](https://www.truemfg.com/wp-content/uploads/2025/05/True-Product-Portfolio-2024.pdf)

`CornerMarket_ColdCases.blend` groups the editable meshes into separate collections with the atlas packed. The Blender script exports each case at its bottom-center origin before arranging the four models side by side in the source file.

```text
python ArtSource/Environment/NeighborhoodStore/build_cold_cases.py --atlas
blender --background --python ArtSource/Environment/NeighborhoodStore/build_cold_cases.py -- --models
```

Use Pillow for the atlas step and Blender 5.2 for the model export. In Unity, **Horror Game > Environment > Create Missing Corner Market Cold Cases** creates materials and collider-equipped prefabs for missing cases. Keep the existing FBX and prefab metadata when updating assets.
