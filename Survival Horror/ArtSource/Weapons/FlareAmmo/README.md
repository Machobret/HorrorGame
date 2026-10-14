# Flare ammunition rework

Replaced the flat cartridge with three solid, 16-sided signal rounds in a
compact triangular bundle. Worn red cases have upright printed labels. Brass
bases have rims, concentric stamped rings and inset primers. A narrow dark band
holds the three rounds. The complete mesh has 303 polygon faces and occupies
about 5.3 x 4.6 x 9.6 cm in Unity.

The bundle uses native metre units and scale 1, with a dedicated
`Assets/Game/Weapons/Models/FlareAmmo/FlareAmmo_Albedo.png` atlas and the template's
unlit shader. The pickup/examination models and inventory icon use this new
mesh. Pickup count remains three; original prefab, model and save IDs are
retained. The downloaded assets were not modified.

Verified in Unity 6000.3.11f1: the test rack still contains all six visible
pickups, the bundle rests on the floor and fits the interaction trigger, and
its examination model is centered. The unlit material resolves the new atlas,
the shader compiles, and an actual Unity camera render was inspected. The
inventory icon remains a 512-pixel sprite.

Original mesh and icon backups are stored here. `build_flare_ammo.py` recreates
the mesh and prepares the generated texture master at 512 pixels. Unity imports
the albedo at 256 pixels with point filtering. `render_theme.py --only-flare-ammo`
renders the 512-pixel inventory icon. The general weapon rebuild invokes this
new mesh generator, preserving the improved ammunition.

The master `FlareAmmo_Atlas_Source.png` was made with the built-in imagegen tool.
The UV layout reserves the top three quarters for red casing, bottom left half
for brass side walls, the next bottom quarter for a stamped base and the final
bottom quarter for the dark caps/band. Geometry and UVs were made in Blender.

## Imagegen prompts

Initial generation, with the flare gun atlas as a palette reference:

Create a NEW production square 2D albedo texture atlas for low-poly flare
ammunition in a PSX survival horror game. Provided image is ONLY the muted
weathered red paint and dark steel style reference; do NOT copy its UV shapes.
Canvas 512x512. EXACT layout: upper 75 percent (y=0 through 383) is one continuous
rectangular texture of worn dark red cardboard/plastic cartridge casing, light
baked edge shading, restrained gray scuffs and dirt, muted burgundy red matching
reference. This rectangle fills full width edge to edge, no objects, no
silhouette, no empty background. Center a small aged ivory rectangular printed
label in the upper rectangle, with clearly readable dark uppercase text RED
FLARE on one line and SIGNAL underneath. Label occupies the middle half of
canvas width, with ample red around it. No extra wording, no logos, no numbers.
Bottom 25 percent (y=384 through 511): left 75 percent (x=0 through383) a seamless
flat worn brass material swatch with soft mottling and subtle darker edges, no
shapes or text; right25percent(x=384through511) a flat dark charcoal cap material
swatch with light scratches. Sharp straight rectangular boundaries between
material swatches. Orthographic FLAT texture atlas, not a photograph or render
of ammunition, no 3D cylinders, no perspective, no shadows cast by objects, no
borders. Low-resolution game-readable surface detail. Understated wear
consistent with the reference weapon, avoid rusty orange or bright red.

Final refinement:

Precise texture atlas edit. Preserve the TOP 75 percent of this texture exactly,
including red casing and ivory RED FLARE SIGNAL label. Preserve BOTTOM RIGHT 25
percent dark charcoal square. Change only the brass area in the bottom quarter
of the canvas, x=0% to75%,y=75%to100%. Split this brass area into two patches:
x=0%to50% is a smooth brushed dark gold brass swatch, unmistakably METAL with
narrow directional brushed highlights, subtle black patina and small silver
edge scratches. No rough paper, wood grain, cloth or cork appearance; no large
random mottling. x=50%to75% is one square patch containing a round brass cartridge
base seen exactly front-on, centered at x=62.5%,y=87.5%; circular
diameter=22%ofcanvaswidth. Base should have two clearly visible concentric stamped
metal rings with pale gold edge highlights, dark recessed ring grooves, and a
small brass primer circle in the center. Outside the round base within this
square is plain brass. Keep all material rectangle boundaries exactly
at75%height,50%width,and75%width. This is flat baked albedo, low-poly PSX horror
game texture. Preserve canvas proportions, no render or scene. Only bottom-left
brass material is being edited. Top three quarters and bottom-right square must
stay unchanged.
