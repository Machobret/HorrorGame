# Lighter source

Current runtime model: Zippo from **Survival PSX by Elbolilloduro**.
Source page: https://elbolilloduro.itch.io/survival-psx
The creator lists the models as CC0. User supplied the separated FBX and texture from Downloads/Models_Separated/Survival. Untouched inputs are retained in SurvivalSource.

Run Blender with --background --python ArtSource/Items/Lighter/import_survival_zippo.py. It preserves the authored open lid, normalizes body width to about 3.85 cm, centres the mesh, exports the runtime FBX, copies its original atlas and renders the 512px icon. Lighter.blend contains packed texture. Run Horror Game > Items > Build Lighter in Unity afterwards; existing item IDs and asset GUIDs are preserved.

The older build_lighter.py is the original procedural red-enamel prototype recipe and is not the current model generator.
