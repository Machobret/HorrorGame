# Classic64 reviews

The complete library is in `Classic64_LibraryReview.unity`, available through
`Tools > Horror Game > Classic64 > Open Complete Library Review`. Its hierarchy
groups model families and material swatches. Disabled cameras provide family
views. Gallery copies use display scaling; labels show runtime dimensions.
Use the environment prefabs for actual level placement.

`Examples` contains the source food-shelf assembly and curved guardrail example.
These and both review scenes remain outside enabled Build Settings.

## Original pilot

Open `Classic64_PilotReview.unity`, or use
`Tools > Horror Game > Classic64 > Open Pilot Review`.

The pilot scene contains two copies of the retained imported art: `NeutralLighting` near
the origin and `HorrorLighting` 24 m to the right. Both use the game's render
pipeline. The neutral overview is the enabled Game camera. Disable it and enable
`Horror_Overview` to switch Game view to the horror stage; alternatively inspect
either stage in Scene view.

Each stage also has disabled `SmallProps` and `SurfaceSamples` cameras for closer
inspection. Select a camera for its preview, or enable only that camera for Game
view. The small-prop table holds the book, soup can, soda can and junction box at
their imported dimensions. All imported prefab instance scales are one.

Review texture clarity, source colors under both lighting setups, foliage cutout
edges, metre scale, thin surfaces, shelf assembly and shovel proportions.
Source texture pixels and UVs are preserved. The shovel has a documented scale
correction. The pilot door was removed under the earlier geometry scope.

These are visual prefabs. No pickups, door behavior, collision or navigation
obstacles are assigned. The sandbox scene is not added to Build Settings.

The full source catalog, provenance and rebuild instructions are under
`ArtSource/Environment/Classic64`. Six reproducible screenshots, Unity logs and
verification reports are under ignored `Exports/Reviews/Classic64`. The full
library is now imported; its additional captures and reports are in `Library`
there. The current runtime selection excludes unfinished and unpainted models;
the catalog and source README identify the exclusions.

Neutral screenshots bypass full-screen retro effects using a temporary plain URP
renderer for clear asset inspection. Horror screenshots retain the game's retro
renderer. The saved scene itself uses the project pipeline for both stages.

## Geometry scope

Standalone Classic64 wall, door and window models are excluded, including the pilot door. Both review scenes have had these instances removed. Complete buildings and props remain. Surface textures are retained; original pre-exclusion captures are historical.

## Finished-assets cleanup

The runtime library now contains 109 finished environment prefabs and two examples.
Seven unfinished/unpainted models and 21 untextured materials were removed. Complete
textured `Building01` remains; unpainted `Shed01` is excluded. At the user's request,
every scene file was left unchanged during this cleanup. Consequently the historical
library review contains missing references to removed models/materials. The pilot
scene has no new missing references. Use runtime prefabs and the catalog for the
current selection; rebuilding or saving review scenes requires separate authorization.
