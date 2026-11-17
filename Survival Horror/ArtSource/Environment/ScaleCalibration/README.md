# Prefab metre-scale calibration

`ScalePlan.json` records explicit uniform corrections and dimensional references for 392 prefabs, following a resolved-hierarchy size audit of 2,153 owned prefabs. It covers all 338 retained ModelsPackPSX prefabs and 54 outliers from other libraries. 389 prefabs change size; three ModelsPackPSX fixtures/trays retain plausible existing dimensions.

ModelsPackPSX has a broad two-to-one scale problem, with a median factor of approximately 0.451; 258 of 338 corrections fall between 0.4 and 0.6. It has no universal correct multiplier. Furniture, appliances, dishes, shelving, vehicles, containers and foliage use separate reviewed targets. Uniform scaling preserves source proportions rather than stretching axes to manufacture standardized dimensions.

| Example | Source size | Calibrated size |
| --- | --- | --- |
| Sedan length | 7.36 m | 4.40 m |
| Van length | 8.20 m | 4.80 m |
| Refrigerator height | 3.65 m | 1.80 m |
| Representative bed length | 4.86 m | 2.05 m |
| Table fork length | 0.58 m | 0.20 m |
| Microwave width | 1.12 m | 0.54 m |
| Rolling tool chest height | 4.85 m | 0.97 m |
| Square pallet footprint | 3.00 m | 1.20 m |

Other corrections include overlarge beds, dining chairs, lounge seating and public litter bins. Horizontal triangle-area measurements distinguish seats/worktops from arms, cushions and backsplashes. Office lounge seats measured approximately 0.62 m before correction and 0.465 m afterward. Pizzeria prep worktops already measure approximately 0.90–0.96 m; their taller backsplashes are retained. Previously calibrated Cars, Classic64 Volvo/shovel/guardrail, modular architecture, plausible building assemblies and pipe lengths are retained.

`ScaleReceipt.json` is the current verified size ledger and supersedes dimensions in historical import receipts. Original downloads, source geometry, UVs, materials, hierarchy, pivots, GUIDs and collider bindings are preserved. All corrected roots remain at unit scale: scaling affects their contents, so explicit root-scale overrides on placed prefab instances do not mask the correction. Existing scene instance scales are retained; historical inspection galleries may use deliberate display scaling.

## Reproduce

1. `Tools/Environment/PrefabScaleAudit.cs` measures resolved prefab dimensions and optionally captures furniture and horizontal-surface evidence in the cached isolated Unity project. Outputs remain under ignored `Exports/Reviews/PrefabCollision/ScaleAudit`.
2. `build_plan.py` reproduces the reviewed targets from that audit and the persistent downloaded-pack catalog. Treat this as initial decision generation; do not regenerate from already corrected dimensions without restoring the original audit inputs.
3. Copy `ScalePlan.json` to the isolated project's `PrefabScalePlan.json`, and compile `Tools/Environment/PrefabScaleCalibration.cs` alongside `PrefabCollisionBatch.cs` under its Assets/Editor folder. Run `PrefabScaleCalibration.RunBatch` with Unity 6000.3.11f1, `-batchmode -nographics`.
4. `VerifyIdempotenceBatch` checks that repeated calibration leaves prefab and metadata bytes unchanged. When available, downloaded-pack maintenance verification also runs. `PrefabScaleAudit.CaptureCalibrationBatch` renders representative results beside a 1.8 m capsule reference; omit `-nographics` for captures.
5. `python Tools/Environment/scale_prefabs.py promote` requires the verified Unity receipt, validates pre-edit hashes, identical dependency GUIDs and unchanged prefab GUIDs, then promotes only planned prefab/metadata files. No scene files or source meshes are rewritten.

Downloaded-pack rebuild staging copies the same plan and shared recipes; maintenance reapplies absolute target dimensions after generating raw prefabs. A calibrated prefab is not multiplied again. Other art libraries can run the same calibration pass after a rebuild. Native `ScaleCalibrated` labels identify reviewed targets; ModelsPackPSX also uses `DownloadedScaleCalibrated` and no longer carries its blanket scale-review label. Background-only and visual-quality restrictions remain.
