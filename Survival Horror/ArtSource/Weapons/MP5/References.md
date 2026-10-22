# Standard MP5 reference and animation notes

## Sources researched, 22 October 2026

- [Heckler & Koch MP5 specifications](https://www.heckler-koch.com/en/Products/Military%20and%20Law%20Enforcement/Submachine%20guns/MP5): standard 9 x 19 mm model, 15/30-round magazines, nominal 800 rounds/minute, right-side case ejection. The game uses a 30-round magazine; its blue SMG ammunition carton is a gameplay distinction from the red pistol carton.
- [HK Training Division, Submachine Gun Instructor manual](https://s3.amazonaws.com/hk-manuals/files/Instruction/Training_Division/HK_SUBMACHINE_GUN_INSTRUCTOR.pdf), printed pages 36-41: shoulder-supported grip with elbows down; ready position pivots at shoulder contact. Reload keeps the firing-hand grip, uses the support hand to lock the cocking lever back, exchanges magazines, checks seating, and releases the lever with the palm.
- [HK MP5 operator manual, manufacturer-authored scan](https://electronicsandbooks.com/edt/manual/Hardware/H/Heckler%20%26%20Koch/HecklerKochMP5SubmachineGunOperatorsManual%20c20020822%20%5B117%5D.pdf), printed pages 18-19: charging handle is nonreciprocating. Standard 9 mm models do not have the automatic last-round bolt catch described for MP5/10 and MP5/40. [Readable mirror](https://manualzz.com/doc/10168098/heckler-and-koch-mp5-submachine-gun-user-manual).
- [Teufelshund Tactical official video library](https://www.teufelshundtactical.com/videos) provides further instructor demonstration references. A [specific reload demonstration](https://www.youtube.com/watch?v=QLzFfM9YdJo&t=916) was located, but video fetching was unavailable; this implementation does not claim a frame-by-frame review of that video.

## Authored game sequence

The following are designed timings within a three-second game reload, not measured real-world performance times.

| Progress | Action |
| --- | --- |
| 0-.10 | Ease the right arm into compact handling position. |
| .025-.245 | Support hand reaches charging handle, pulls rearward and locks it. |
| .245-.40 | Support hand reaches magazine release; magazine withdraws downward. |
| .40-.58 | Magazine passes to pocket; replacement returns from pocket. |
| .58-.715 | Insert magazine, then make a small seating tug. |
| .715-.84 | Support hand reaches the charging handle and releases it with a palm motion. |
| .84-1 | Return support hand to handguard; blend out corrections into destination pose. |

`MP5ReloadVisual` owns the two arm chains only during reload. It rotates shoulder/clavicle and elbow joints through `ReloadArmIK`; it never lengthens bones or translates skeletal joints. Aimed and lowered starts use different compact working heights and muzzle pitch. The existing reload state still decides whether to return to aim and owns all ammo transfers, inventory-combine transactions, movement restrictions and reload completion. Interruption restores the visible magazine/handle without loading ammunition.

`MP5HandlingPose` maintains the support wrist on a configurable handguard anchor while standing, walking, aiming and firing. It yields completely to reload. Its entry/exit weight blends over 0.16 seconds. The shared wrist continuity component runs afterward at execution order 400. The original textured charging-handle triangles are separated from a runtime clone of the imported body mesh and animated independently; if those triangles are unavailable, a small procedural lever is used as fallback. The handle remains still throughout firing.

The imported MP5 model is normalized around the firing grip with forward +Z. Builder-assigned support anchor and wrist quaternion must be visually checked on the player rig. The source magazine can have a shared model origin, so movement uses a renderer-center offset rather than assuming a local magazine pivot. Existing generic `WeaponAmmoVisuals` and `LongGunSupportPose` must be removed from the MP5 prefab to avoid competing attachment/IK ownership.

These runtime scripts were added without launching Unity by the implementing subagent; parent-task Unity validation and captures provide the integration evidence.

## Measured model anchors

Normalized `Extraction.json` / OBJ measurements put the handguard between z .166 and .329, around y .058-.105. The support wrist default is (-.040,.035,.235), behind/below finger contact on that handguard rather than beside the magazine. Builder wrist orientation remains configurable for this character rig. The baked handle occupies x[-.024,-.001], y[.108,.130], z[.216,.241]; its pivot is (-.0127,.1187,.2286), with .085 rearward travel. Runtime mesh splitting preserves original UVs and materials and never modifies the imported mesh asset.

## Isolated animation capture

Copy `MP5AnimationValidation.cs` from this directory into the isolated project's `Assets/Game/Weapons/Editor` folder, after syncing the MP5 runtime scripts/assets. Run Unity with `-batchmode -projectPath <isolated project> -executeMethod HorrorGame.Weapons.Editor.MP5AnimationValidation.RunBatch -logFile <log>` and omit `-quit`; the helper enters Play Mode and exits on completion. Do not use the currently open user project for this batch capture.

Output is `<isolated project>/MP5AnimationReview`: 60 fps frames for aimed/held, aimed/released at 90 percent, and lowered reloads; held baseline/final screenshots from both sides; mesh/hand JSON probes; and CSV with support anchor error, elbow angles, actor-relative wrist quaternions and mechanism positions/flags. Successful completion logs `ALL_MP5_ANIMATION_CASES_OK`. The checks validate input/state/ammo/movement/visible-mechanism restoration. Mesh intersections and hand-to-surface fit still require reviewing the rendered captures; a green gameplay result alone does not establish visual quality.
