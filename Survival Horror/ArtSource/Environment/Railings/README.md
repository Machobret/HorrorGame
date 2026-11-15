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

Exports stage under ignored `Exports/Reviews/RailingFamily/Models`. The saved chapter scenes use baked railing geometry. Replace their model through Unity while retaining its GUID when refreshing an existing model.
