# Railing source

`Railing_Source.blend` is an unchanged copy of the supplied
`C:/Users/manta/Downloads/Procedural Toolkit Demos/Fence_Family_Reference.blend`.
Only its `Railing` object is exported. Original pack content and its other objects remain untouched.

`export_railing.py` evaluates the Wireframe modifier, bakes object scale, removes reference-sheet
placement, centres thickness, and raises the base to zero. Native 2 m width and 100 mm thickness
are retained. The saved height is rounded to the nearest millimetre for the importer filename.
The 1 m and 0.5 m variants are plane-cropped and capped. All exported components are closed/manifold.
Two nonempty material slots and planar UVs satisfy the wall module contract. Unity remaps both slots
to the recently imported `M_Metal_Rusty_04` material without duplicating it.

Run from the project root:

```powershell
& 'C:/Portable Applications/Blender Portable/Blender 5.0.1/blender.exe' --background --factory-startup --disable-autoexec --python ArtSource/Environment/Railings/export_railing.py -- --project .
```

Exports stage under ignored `Exports/Reviews/RailingFamily/Models`. Then run
**Tools > Environment > Railings > Import railing wall family** in Unity. The importer moves
previously exported FBXs through AssetDatabase if their height token changes, preserving their GUIDs.
For an already open editor, write `import` to `Exports/Reviews/RailingFamily/import.request`
before a script refresh. The explicit request is consumed once after reload; the result is written
beside it. Source hashes and export geometry counts are recorded in `provenance.json`.
Review renders/logs belong in ignored `Exports/Reviews/RailingFamily`.

For a model-only update, add `--quick` to the exporter and use `import-only` as the request.
This updates the existing modules and material bindings without validation, tests or a review render.
