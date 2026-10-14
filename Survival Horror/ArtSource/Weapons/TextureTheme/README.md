# Gun texture theme

## Comparison and direction

The template pistol and shotgun have 1024-pixel authored albedos with baked
highlights, shadows and surface detail. Their Unity default texture limits are
128 (pistol) and 256 (shotgun), with point filtering and mipmaps. Both use
`HorrorEngine/UnlitAlphaCutout`, so their appearance comes from the texture rather
than real-time surface lighting. The pistol emphasizes charcoal metal and
stippled black polymer; the shotgun combines dark walnut, dirty steel and light
handling wear. Neither needs a separate normal or metallic map for this look.

The downloaded hunting rifle used a 400x223 photographic atlas with relatively
clean wood and steel. The flare gun used a 240x240 atlas with bright orange/red
paint and a flatter grip. Their URP Lit materials added a different lighting
response from the template guns.

The new finish keeps the existing models and UVs:

- **Hunting rifle:** darker aged walnut, grain and handling scuffs, blued steel,
  restrained silver edge wear. The original charcoal scope texture already fits
  this palette and is retained.
- **Flare gun:** muted oxblood red enamel, subtle chips exposing gray steel,
  grime at joints, textured black grip. Flare ammunition now has its own matching
  atlas and three solid cartridges; see `../FlareAmmo/README.md`.
- **Materials:** same unlit shader and double-sided rendering as the template.
- **Imports:** point filtering and mipmaps. Rifle body maximum 512 pixels to
  retain detail on the thin side-view UV strip; flare and scope maximum 256.
- **Icons:** rerendered from existing meshes at 512x512 with transparent
  backgrounds and bilinear sprite filtering. Blender uses baked/unlit surfaces
  to match the actual game material. Albedo RGB is not treated as transparency.

Texture paths and Unity GUIDs were retained, so held guns, pickups and examination
models all pick up the change. No gameplay prefab or scene was rebuilt.

## Files and reproduction

`Original/` contains pre-edit gun textures and icons. Generated masters are
`HuntingRifle_Atlas_Source.png` and `FlareGun_Atlas_Source.png`. The resized delivery
textures live in `Applied/` and at the existing paths in
`Assets/Game/Weapons/Models`. Masters were produced with the built-in imagegen
tool. Resizing only prepares those outputs for Unity.

Validated in an isolated Unity 6000.3.11f1 project: every gun material resolves
its albedo and uses the template shader; shader passes compile; texture imports
use point filtering at 512x256 for the rifle body, 256x128 for the scope and
256x256 for the flare atlas; inventory sprites remain 512x512. Actual camera
renders are saved as `HuntingRifle_Unity.png`, `FlareGun_Unity.png` and
`FlareAmmo_Unity.png` and were inspected for visible surfaces and atlas seams.

Run Blender with `--background --python ArtSource/Weapons/render_theme.py -- --apply`
to install and rerender, or omit `--apply` to rerender only. The model build script
prefers `Applied/` over downloaded textures and rerenders the approved gun icons.

## Imagegen prompts

### Hunting rifle

Edit target image 1 only: a production UV color texture atlas for an existing
low-poly hunting rifle model. Images 2 and 3 are material/style references only
from the game's existing shotgun and pistol. Preserve the EXACT rectangular
canvas aspect ratio, UV island positions, silhouettes, boundaries and relative
sizes of image 1. The long side-view rifle occupies the same thin upper middle
strip; front-facing circular muzzle/bolt object and scope side view remain in
exact lower positions. Do not render a new gun, do not rearrange, crop, center or
enlarge any part. Keep white unused background white. Retexture surfaces within
their existing silhouettes to match reference weapons: aged dark walnut stock,
visible restrained wood grain and small handling scuffs, charcoal blued steel
barrel and action with subtle silver wear along edges, dirt in crevices, baked
shadows. Match muted warm brown wood and dark gray metal of reference 2,
understated realism and wear of reference 3. No bright rust, no excessive
destruction, no neon, no dramatic light. Preserve all original hardware features.
Crisp hand-painted game albedo texture, with low resolution readable surface
detail, no normal-map colors, no graphics or added text. This is a flat UV atlas
editing job, not a presentation image. Deliver only the edited texture.

### Flare gun

Edit ONLY image 1, an existing square UV atlas for a low-poly flare pistol. Images
2 and 3 are material and theme references from the template weapons. CRITICAL
production constraints: preserve every UV island silhouette, location, exact
footprint and boundary in image 1. No rearrangement, no added geometry, no
separate gun render. Background gaps remain pure black. Top left cartridge label
red/brass rectangle stays exactly where it is, top right trigger/hammer metal
patches stay in place. Curved dark trigger strip across top remains dark. Main
side grip shape stays at left occupying middle and bottom. Horizontal barrel/body
patches stay at right with their exact screw locations and engraving positions.
Retexture existing orange/red painted surfaces into muted dark oxblood red
enamel, slightly weathered and greasy, small believable chips at edges exposing
charcoal steel with restrained silver scuffs, darker grime in joints. Black
rubber grip needs fine worn stipple with baked shading, similar to reference 2.
Match reference weapons' muted desaturated realism, localized handling wear,
baked occlusion and legible highlights. Keep flare identity and existing cartridge
lettering recognizable. Avoid bright orange, heavy orange rust, cartoon look, a
uniform flat dark coat, excessive cracks, or overly busy texture. No added text,
no borders. Flat production albedo UV texture atlas only, not a gun photograph.
All shapes must stay pixel-aligned to the input layout.
