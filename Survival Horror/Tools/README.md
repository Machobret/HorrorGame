# Project tools

`Art/weapon_paths.py` locates the project from its own file and routes weapon-generation output to the owning asset family. Unity editor builders use the matching `WeaponAssetPaths` helper under `Assets/Game/Weapons/Shared/Editor`.

Asset-specific source recipes remain in ArtSource. Put new shared automation here and send reproducible reports to ignored `Exports/Reviews`.

`Environment/fence_names.py` prepares and verifies the native fence naming plan from `ArtSource/Environment/Fences/Names.json`. Unity's `FenceLibraryNaming` editor utility applies moves through AssetDatabase. See the fence source README for the sequence and provenance rules.
