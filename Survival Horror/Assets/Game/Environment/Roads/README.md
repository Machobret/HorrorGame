# Road surfaces and paint

Assign `TownStreet/Road Asphalt.mat` to generated roads and hand-authored connecting asphalt meshes. The **House Toolkit/Road Surface** shader samples absolute world XZ, ignores texture alpha and mesh UVs for the asphalt, and needs no road component on custom meshes. **World XZ repeat (metres)** controls scale; texture tiling and offset are shared material adjustments. Separate material copies must use the same texture, tint, repeat, tiling, offset and smoothness to match. Top-down projection stretches on steep slopes and is intended for road tops.

Town Street and Side Street use this same asphalt material for strips, bare sections, and T/X junctions. The previous Bare Asphalt, T Junction and X Junction material assets retain their GUIDs and use the same plain asphalt settings for existing references. The old Street Edges, T Mouths and X Mouths textures are retained but no longer drive road rendering.

All markings use **House Toolkit/Road Paint**, in separate render submeshes lifted 3 mm above the road. Paint is excluded from collision and picking triangles. Center lines, lane dividers, crosswalks, stop lines and atlas symbols retain their road-aligned UVs. Scuffs use shared world XZ and the family's **Paint wear**, with matching clipping in color, depth, depth-normal and shadow passes. Both shaders supply depth normals for URP renderers using a normal prepass.

In the road family's **Markings**, enable **Edge lines** and set their material, width and inset in metres. The inset measures from the road boundary to the outside of the line. Edge ribbons follow bends and the closed boundaries of T/X junctions, leaving junction mouths open. These settings are independent of the asphalt texture and of center-line settings.

Wheel paths remain optional shading driven by generated-road data. Ordinary meshes receive plain asphalt, regardless of vertex colors. **Open-end wear fade (m)** fades wheel tint to plain asphalt at terminal road ends for connections to custom sections; zero disables it. On short terminal spans, the fade is shortened to fit. Set **Wheel path tint** to white or disable **Use generated road wheel paths** on the material to remove the effect entirely. Geometry heights, normals and lighting must also agree at a join.

The family's **UV material repeat (m)** remains available for custom UV-mapped shaders; it does not control world-space asphalt. No changes to building projected finishes are required.

Regression checks: `RoadSurfaceTests` in `Assets/Sandbox/HouseToolkitTests/Editor` covers transforms, texture phase/scale/alpha, shader assignment, edge topology, collision and open-end wheel-wear data.
