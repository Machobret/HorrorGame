# Animated pixel-art neon signs

The downloaded **Pixel Art Sign Pack - Animated** supplies the artwork. The numbered PNGs under each design's colour or state folder are the animation masters. The importer packs these frames and preserves their order and frame count. The artwork is licensed under CC0. [Source readme](SourceLicense.txt)

## Reimport

1. Set `NEON_SIGN_PACK` to the extracted pack directory. Copy `Tools/Environment/ImportNeonSigns.cs` into `Assets/Editor` of an isolated Unity snapshot. Include the current `Assets/Game/Environment/NeonSigns` folder and its `.meta` file so existing asset GUIDs are retained.
2. Run Unity in batch mode with `-executeMethod ImportNeonSigns.RunBatch`. Copy the resulting `NeonSigns` folder and its `.meta` back to the project. The importer updates the frame atlases and materials and creates only missing prefabs.
3. Run `python -B Tools/Environment/build_asset_library.py` from the project root to refresh the palette.

Each atlas contains one variant's full loop at the original resolution. The loop starts on a bright frame for asset previews and uses 12 frames per second by default. The supplied PNGs have no timing metadata.
