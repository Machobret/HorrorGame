# Prefab metre-scale calibration

`ScalePlan.json` records dimensional targets for 635 prefabs, including all 338 retained ModelsPackPSX prefabs. Of these targets, 632 change the original size. Three ModelsPackPSX fixtures or trays retain their original dimensions.

The project-wide follow-up covered 2,150 game prefabs and five sandbox prefabs. It measured world mesh bounds for 2,110 and recorded 45 entries without measurable world geometry under the audit filter. It verified the previous 418 targets and corrected 217 additional props. `PropScaleReferences.tsv` records the placement dimension or shared scale for each newly reviewed prop family. Boxes and slices use the same factor as their pizzas, and detachable lids and refuse use the same factor as the corresponding dumpster body. Twelve representative prefabs were rendered beside a 1.8 m height reference. The full dimensions and reference sheet are in `Exports/Reviews/PrefabCollision/SizeReview`.

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

The street-furniture pass calibrates 12 shelters and 14 light or signal prefabs. Flat shelters are about 2.4–2.5 m high and rounded shelters about 3.1 m. Both sit alongside the 2.8 m Urban storey pitch. The 4 m Chapter 1 lamp anchors the neighborhood reference, and Urban and Pizzeria streetlights now match at about 5 m. Urban signals stand about 5.1 m high and Pizzeria signals about 5.5 m. The Pizzeria arms sit lower on their poles, so the larger target keeps clearance for the 2.61 m Box Truck. Urban signal poles and their matching detachable heads use the same multiplier.

## Reproduce

1. `Tools/Environment/PrefabScaleAudit.cs` measures resolved prefab dimensions and optionally captures furniture and horizontal-surface evidence in the cached isolated Unity project. For a fresh measurement, pass `-sizeAuditOutput ../SizeReview/PostCalibration` to `PrefabScaleAudit.RunBatch`. Reserve its default output directory for the original measurements used by the plan.
2. `build_plan.py` reproduces the reviewed targets from that audit and the persistent downloaded-pack catalog. Treat this as initial decision generation; do not regenerate from already corrected dimensions without restoring the original audit inputs.
3. Copy `ScalePlan.json` to the isolated project's `PrefabScalePlan.json`, and compile `Tools/Environment/PrefabScaleCalibration.cs` alongside `PrefabCollisionBatch.cs` under its Assets/Editor folder. Run `PrefabScaleCalibration.RunBatch` with Unity 6000.3.11f1, `-batchmode -nographics`.
4. `VerifyIdempotenceBatch` checks that repeated calibration leaves prefab and metadata bytes unchanged. When available, downloaded-pack maintenance verification also runs. `PrefabScaleAudit.CaptureCalibrationBatch` renders representative results beside a 1.8 m capsule reference; omit `-nographics` for captures.
5. `python Tools/Environment/scale_prefabs.py promote` requires the verified Unity receipt, validates pre-edit hashes, identical dependency GUIDs and unchanged prefab GUIDs, then promotes only planned prefab/metadata files. No scene files or source meshes are rewritten.

Downloaded-pack rebuild staging copies the same plan and shared recipes; maintenance reapplies absolute target dimensions after generating raw prefabs. A calibrated prefab is not multiplied again. Other art libraries can run the same calibration pass after a rebuild. Native `ScaleCalibrated` labels identify reviewed targets; ModelsPackPSX also uses `DownloadedScaleCalibrated` and no longer carries its blanket scale-review label. Background-only and visual-quality restrictions remain.
