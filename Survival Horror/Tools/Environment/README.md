# Prefab collision maintenance

`prefab_collision.py` snapshots the project into ignored `Exports/Reviews/PrefabCollision/UnityProject`; it never operates the user's open Unity editor. The snapshot uses installed local package versions and preserves prefab metadata. `PrefabCollisionBatch.cs` compiles only in that snapshot.

The batch resolves inherited/nested prefab collision before adding anything. Placeable 3D prefabs under `Assets/Game` and `Assets/Sandbox` are in scope. Existing solid colliders, triggers, character controllers and prefab links remain intact. UI, held weapons, examination presentations, VFX and controller-only/toolkit authoring presets do not receive blocking collision. Vendor prefabs are audited through dependencies but are not edited.

Prefabs labelled `NonBlockingDecoration` are intentionally collider-free for solid physics. The batch respects that label on stand-alone and nested decoration, and the downloaded-pack builder preserves it. Use it for reviewed dressing such as rugs, decals and mounted light fixtures. Grounded poles, shelter structures and gameplay collision follow their normal coverage rules.

Missing static geometry receives mesh collision; exact cuboids and very small compact imported props use local-bounds boxes. Meshes under nonkinematic rigidbodies receive convex collision. Lower LODs and light/effect emitters do not duplicate collision. Colliders stay on the owning mesh part so multipart assemblies keep their source pivots.

Run from the project root:

1. `python Tools/Environment/prefab_collision.py stage`
2. Run Unity 6000.3.11f1 with `-batchmode -nographics -projectPath <snapshot> -executeMethod PrefabCollisionBatch.RunBatch -logFile <review>/Build.log`.
3. Inspect `BatchReceipt.json`, including physics failures and excluded presentations. `python Tools/Environment/prefab_collision.py prepare-promotion` transplants only new native collider blocks and their GameObject/prefab attachment references into the original serialized prefabs. All existing gameplay component data remains unchanged.
4. Run `PrefabCollisionBatch.VerifyMergedBatch` in the same snapshot. This checks resolved mesh coverage and raycasts collider shapes in a normal scene after the collider-only merge.
5. `python Tools/Environment/prefab_collision.py promote` requires successful verification, rechecks concurrent edits and copies only the listed prefab files. Metadata, scenes and vendor assets are preserved.

The downloaded-prop and Classic64 import builders also generate static collision so those rebuilds retain it. Re-run this maintenance pass after importing other art-only prefab families. Review receipts and logs stay outside runtime content.

## Prefab health scan

`python Tools/Environment/prefab_health.py stage` refreshes the isolated snapshot and builds the scope from the current owned prefabs. Run Unity with `-batchmode -nographics -projectPath <snapshot> -executeMethod PrefabHealthAudit.RunBatch`, then run `python Tools/Environment/prefab_health.py summarize`. The reports are written under `Exports/Reviews/AssetHealth`.

The scan compares face winding with imported normals, checks the winding of closed components, and records mirrored transforms, material bindings, approximate prefab dimensions and transparent-material opacity sampled from PNG atlases. The review queue flags candidates that need visual or gameplay context, including interior surfaces, intentional mirrors and authored translucency.
