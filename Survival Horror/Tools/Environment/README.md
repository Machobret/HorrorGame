# Prefab collision maintenance

`prefab_collision.py` snapshots the project into ignored `Exports/Reviews/PrefabCollision/UnityProject`; it never operates the user's open Unity editor. The snapshot uses installed local package versions and preserves prefab metadata. `PrefabCollisionBatch.cs` compiles only in that snapshot.

The batch resolves inherited/nested prefab collision before adding anything. Placeable 3D prefabs under `Assets/Game` and `Assets/Sandbox` are in scope. Existing solid colliders, triggers, character controllers and prefab links remain intact. UI, held weapons, examination presentations, VFX and controller-only/toolkit authoring presets do not receive blocking collision. Vendor prefabs are audited through dependencies but are not edited.

Missing static geometry receives mesh collision; exact cuboids and very small compact imported props use local-bounds boxes. Meshes under nonkinematic rigidbodies receive convex collision. Lower LODs and light/effect emitters do not duplicate collision. Colliders stay on the owning mesh part so multipart assemblies keep their source pivots.

Run from the project root:

1. `python Tools/Environment/prefab_collision.py stage`
2. Run Unity 6000.3.11f1 with `-batchmode -nographics -projectPath <snapshot> -executeMethod PrefabCollisionBatch.RunBatch -logFile <review>/Build.log`.
3. Inspect `BatchReceipt.json`, including physics failures and excluded presentations. `python Tools/Environment/prefab_collision.py prepare-promotion` transplants only new native collider blocks and their GameObject/prefab attachment references into the original serialized prefabs. All existing gameplay component data remains unchanged.
4. Run `PrefabCollisionBatch.VerifyMergedBatch` in the same snapshot. This checks resolved mesh coverage and raycasts collider shapes in a normal scene after the collider-only merge.
5. `python Tools/Environment/prefab_collision.py promote` requires successful verification, rechecks concurrent edits and copies only the listed prefab files. Metadata, scenes and vendor assets are preserved.

The downloaded-prop and Classic64 import builders also generate static collision so those rebuilds retain it. Re-run this maintenance pass after importing other art-only prefab families. Review receipts and logs stay outside runtime content.
