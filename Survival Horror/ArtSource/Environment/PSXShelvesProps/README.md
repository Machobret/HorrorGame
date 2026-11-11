# PSX Bookshelves & Food Shelves conversion

Source: the user's downloaded `PSX_ShelvesProps_v2/PSX_ShelvesProps` pack, Normal and Horror Blender files. Original meshes, UVs and texture art are preserved. See `Original_README.txt` and `Original_LICENSE.txt` for author documentation/licence. The browser download identifies the pack as **PSX Bookshelves & Food Shelves** by **Munchkin Gaming**.

The prepared runtime family is `Assets/Game/Environment/PSXShelvesProps`. `PackManifest.json` records source paths and SHA-256 hashes, per-object material order, source placement anchors and expected metre dimensions. It is consumed by the Unity builder; do not rename its generated paths independently.

Rebuild from a project-root shell:

```text
blender --background --python ArtSource/Environment/PSXShelvesProps/prepare_pack.py -- --source <downloaded PSX_ShelvesProps directory>
```

Then run **Horror Game > Environment > Build PSX Shelves Props** in Unity outside Play Mode. Blender 5.2 was used for conversion, Unity 6000.3.11f1/URP 17 for import.

Conversion exports each of the 48 mesh objects in both variants to an individual FBX. Source world transforms are baked into geometry and offset to the Normal version's canonical placement pivot; Horror counterparts reuse that pivot rather than recentering on spilled contents. Meshes/UVs/material-slot assignments remain intact. Original loose Light objects become configured child spotlights on their corresponding fixture prefabs. Godot project/scripts and complete combined-scene exports are not runtime dependencies.

Unity validation checks all 96 prefabs for metre dimensions, material texture references, point filtering and static collision. Reports are written to ignored `Exports/Reviews/PSXShelvesProps`. Existing scenes are not edited by the builder.
