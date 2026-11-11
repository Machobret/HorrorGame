# Garage doors

Four closed door meshes/prefabs share GarageDoors_Atlas.mat and the original 2 x 2 atlas: GreyRoller, BlueSectional, WhiteSectional and GreenRoller. Each is 2.8 m wide, 2.8 m high and 0.05 m thick, with its origin at bottom centre. Scale the prefab for a different opening size. Both sides have outward-facing geometry and correctly oriented artwork; edge faces sample the frame border. Each mesh has 12 triangles and one material slot.

The standard wall shader uses Authored UV mapping with crisp point sampling. The quadrant UVs have half-texel insets; the atlas is not projected or repeated across the door. There are no additional normal maps, shaders or per-door textures.

Drag a prefab into the scene as an ordinary object and create a matching Box Boolean cut through the wall. For the default size, use a 2.8 x 2.8 m opening and enough cutter depth to pass through the whole wall (0.5 m covers the standard T150/T300 walls). Align the cut's bottom to the floor and its centre to the door centre, 1.4 m above the prefab origin. Keep the door outside generated wall/slab output hierarchies so wall rebuilds do not own or cut the door mesh.

These are static closed-door visuals with BoxColliders. The artwork includes its frame and painted window/detail pixels; frame/panel animation and interactive opening behaviour are not included. Boolean placement does not use the restricted 1 m/2 m opening-module catalog.

Build missing prefabs or regenerate meshes through Tools > Environment > Garage Doors > Build atlas door meshes. Existing mesh GUIDs and existing prefab edits are preserved. The atlas source/provenance is under ArtSource/Environment/GarageDoors. No Boolean benchmarks were run.
