# Environment asset library

Downloaded props are grouped by functional family. Each family keeps its FBX mesh library, category atlases, materials and individual visual prefabs together. The prefabs contain art only; gameplay behavior and colliders are intentionally assigned by their consumers.

The source review is `Downloads/DownloadedAssets_Review.blend`. Its adjacent `DownloadedAssets_ImportStatus.json` records verified imports, GUIDs and deferred content. The Blender file also embeds the receipt and preserves original meshes, UVs and materials. Imported representations appear in a separate viewport area under `UnityImported_Atlased`.

Current scope uses existing solid furniture variants. Characters, construction, wall/fence families and their materials remain deferred. Foliage contributes textures/materials for custom shrub meshes; its downloaded meshes are excluded. Generic mine props use functional categories, while specialized equipment remains under `MineEquipment`.

The Blender recipe is `ArtSource/Environment/DownloadedProps/build_atlases.py`. It and Unity `EnvironmentAssetImportBuilder.RunBatch` use an export manifest and produce a verified import receipt. Read the handoff record before importing another batch; an object present in the source catalog is not necessarily imported into Unity.
