using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    internal static partial class FixtureTool
    {
        static FixturePalette[] palettes;
        static FixturePalette brushPalette;
        static GameObject brushPrefab;
        static FixtureMount brushMount;
        // Unity can deserialize a palette between drawing its swatch and clicking the scene.
        internal static FixtureChoice Brush => brushPalette ? brushPalette.Find(brushPrefab, brushMount) : null;
        static FixtureAttachment moving;
        static FixtureAttachment editingDefaults;
        internal const string ChangePreset = "Change fixture lighting preset";
        internal static void Cancel() => moving = null;
        static FixtureTool()
        {
            DragAndDrop.AddDropHandlerV2(DropFixture);
            DragAndDrop.AddDropHandlerV2(DropHierarchyFixture);
            SceneView.duringSceneGui += ReattachScene;
            EditorApplication.projectChanged += () => palettes = null;
            Undo.undoRedoEvent += (in UndoRedoInfo info) =>
            {
                if (info.undoName == ChangePreset || info.undoName == LocalLighting) RefreshLighting(null);
            };
        }

        static FixturePalette[] Palettes => palettes ??= AssetDatabase.FindAssets("t:FixturePalette")
            .Select(id => AssetDatabase.LoadAssetAtPath<FixturePalette>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(p => p).ToArray();

        static FixtureChoice[] Choices => Palettes
            .Where(p => p && p.fixtures != null).SelectMany(p => p.fixtures)
            .Where(c => c != null && c.prefab).ToArray();

        internal static void Palette(bool facadeOnly = false)
        {
            if (moving)
            {
                EditorGUILayout.HelpBox("Click a mounting surface to move " + moving.name + ".", MessageType.Info);
                if (GUILayout.Button("Cancel move")) Finish(moving);
                return;
            }
            var choices = facadeOnly ? Choices.Where(c=>c.Supports(FixtureMount.Wall)).ToArray() : Choices;
            var brush = Brush ?? choices.FirstOrDefault();
            if (facadeOnly && !choices.Contains(brush)) { brush=choices.FirstOrDefault(); if(brush!=null) SelectBrush(brush); }
            if (brush == null) { EditorGUILayout.HelpBox("Add prefabs to a Fixture Palette asset.", MessageType.Info); return; }
            if (Brush == null) SelectBrush(brush);
            for (int i = 0; i < choices.Length; i += 3)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 3, choices.Length); j++)
                {
                    var choice = choices[j];
                    EditorGUILayout.BeginVertical(GUILayout.MinWidth(55));
                    var image = AssetPreview.GetAssetPreview(choice.prefab);
                    if (!image && AssetPreview.IsLoadingAssetPreview(choice.prefab.GetInstanceID())) HouseProperties.RepaintOpen();
                    var tile = GUILayoutUtility.GetRect(55, 52, GUILayout.ExpandWidth(true));
                    var content = new GUIContent(image ? image : AssetPreview.GetMiniThumbnail(choice.prefab),
                        choice.label + " (" + MountLabel(choice) + "). Double-click to reveal prefab.");
                    ProjectAssetNavigation.HandleDoubleClick(tile, choice.prefab);
                    if (GUI.Toggle(tile, brush == choice, content, GUI.skin.button) && brush != choice)
                    {
                        brush = choice;
                        SelectBrush(brush);
                    }
                    var label = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    ProjectAssetNavigation.HandleDoubleClick(label, choice.prefab);
                    GUI.Label(label, choice.label, EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Label("Mount on " + MountLabel(brush), EditorStyles.miniLabel);
        }

        static string MountLabel(FixtureChoice choice) => choice.wallAndCeiling ? "wall or ceiling" :
            choice.recessedWallMount && choice.mount == FixtureMount.Floor ? "wall or floor" : choice.mount.ToString().ToLowerInvariant();

        internal static void SelectBrush(FixtureChoice choice)
        {
            brushPalette = OwnerOf(choice);
            brushPrefab = choice.prefab;
            brushMount = choice.mount;
        }

        internal static void BeginMove(FixtureAttachment fixture)
        {
            var floor = fixture.GetComponentInParent<HouseFloor>();
            if (!floor || fixture.freePlacement) { moving=fixture; SceneView.RepaintAll(); return; }
            if (HouseScene.House != floor.House) HouseScene.Enter(floor);
            if (HouseScene.Floor != floor) HouseScene.SwitchFloor(floor);
            HouseScene.SetMode(HouseTool.Fixture);
            moving = fixture;
        }

        static void Finish(FixtureAttachment fixture)
        {
            moving = null;
            HouseScene.SetMode(HouseTool.Select);
            Selection.activeGameObject = fixture.gameObject;
            HouseProperties.ShowSelection();
        }

        internal static bool Target(HouseFloor floor, FixtureMount mount, Ray ray, out Transform host, out Vector3 point,
            out int side, bool allowOpeningSurface = false)
        {
            host = null; point = default; side = 1;
            if (mount != FixtureMount.Wall)
            {
                if (floor.WallsOnly) return false;
                bool ceiling = mount == FixtureMount.Ceiling;
                var owner = SlabOpeningTool.Owner(floor, ceiling, out bool roof);
                var slab = owner.GetComponentsInChildren<HouseSlab>().FirstOrDefault(s => s.overhead == roof);
                var up = floor.transform.up;
                var plane = new Plane(up, floor.transform.TransformPoint(Vector3.up * (ceiling ? floor.height : 0)));
                if (!slab || !plane.Raycast(ray, out float depth)) return false;
                point = ray.GetPoint(depth);
                // Probe the actual face, including slab and volumetric cuts, even in cutaway.
                var normal = ceiling ? -up : up;
                var probe = new Ray(point + normal * .03f, -normal);
                var collider = slab.GetComponent<Collider>();
                if (!collider.enabled) collider = slab.GetComponentInChildren<BooleanCutResult>()?.GetComponent<Collider>();
                if (!collider || !collider.Raycast(probe, out var face, .06f) || Vector3.Dot(face.normal, normal) < .9f)
                    return false;
                if (ceiling && !floor.GetComponentsInChildren<HouseRoom>().Any(room =>
                    room.HasCeiling(floor) && RoomGeometry.Inside(floor.Local(probe.origin), room.polygon))) return false;
                host = floor.transform;
                return true;
            }
            float best = float.PositiveInfinity;
            float blocker = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.scene != floor.gameObject.scene ||
                    SceneVisibilityManager.instance.IsHidden(hit.collider.gameObject) ||
                    hit.collider.GetComponentInParent<FixtureAttachment>()) continue;
                var wall = hit.collider.GetComponentInParent<HouseWall>();
                var insert = hit.collider.GetComponentInParent<InsertAttachment>();
                if (!wall && allowOpeningSurface && insert && !string.IsNullOrEmpty(insert.openingId))
                    wall = floor.GetComponentsInChildren<HouseWall>().FirstOrDefault(w => w.openings.Any(o => o.id == insert.openingId));
                if (!wall || wall.Floor != floor || wall.boundaryOnly || (insert && !allowOpeningSurface))
                {
                    blocker = Mathf.Min(blocker, hit.distance);
                    continue;
                }
                if (hit.distance >= best) continue;
                float facing = Vector3.Dot(hit.normal, wall.transform.forward);
                if (Mathf.Abs(facing) < .9f) continue;
                best = hit.distance; host = wall.transform; point = hit.point; side = facing > 0 ? 1 : -1;
            }
            return host && best <= blocker + .0001f;
        }

        internal static void Scene(SceneView view, Event e, Ray ray, int control)
        {
            var brush = Brush;
            if (e.alt || Tools.viewToolActive || (!moving && brush == null)) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseMove && e.type != EventType.Repaint) return;
            var choice = moving ? moving.LightPreset : brush;
            var mount = moving ? moving.mount : brush.mount;
            if (!TargetSurface(HouseScene.Floor, choice, mount, ray, out var host, out var point, out int side,
                moving && moving.allowOpeningSurface)) return;
            if (choice != null) mount = host == HouseScene.Floor.transform ? choice.HorizontalMount : FixtureMount.Wall;
            if (e.type == EventType.MouseMove) view.Repaint();
            if (e.type == EventType.Repaint)
            {
                var normal = mount == FixtureMount.Wall ? host.forward * side : mount == FixtureMount.Ceiling ? -host.up : host.up;
                using (new Handles.DrawingScope(Color.green))
                    Handles.DrawWireDisc(point, normal, HandleUtility.GetHandleSize(point) * .09f);
            }
            if (e.type != EventType.MouseDown || e.button != 0 || HandleUtility.nearestControl != control) return;
            if (moving) { Mount(moving, host, point, side); Finish(moving); }
            else Place(brush, host, point, side);
            e.Use(); view.Repaint();
        }

        internal static bool TargetSurface(HouseFloor floor, FixtureChoice choice, FixtureMount mount, Ray ray,
            out Transform host, out Vector3 point, out int side, bool allowOpeningSurface = false)
        {
            bool allowOpenings = allowOpeningSurface || choice?.allowOpeningSurface == true;
            if (choice == null || (!choice.wallAndCeiling && !choice.recessedWallMount))
                return Target(floor, mount, ray, out host, out point, out side, allowOpenings);
            bool wall = Target(floor, FixtureMount.Wall, ray, out host, out point, out side, allowOpenings);
            var horizontal = choice.HorizontalMount;
            var toward = horizontal == FixtureMount.Floor ? -floor.transform.up : floor.transform.up;
            if (Target(floor, horizontal, ray, out var slab, out var hit, out _) &&
                (!wall || (Vector3.Dot(ray.direction, toward) > 0 &&
                (hit - ray.origin).sqrMagnitude < (point - ray.origin).sqrMagnitude)))
            {
                host = slab; point = hit; side = 1;
                return true;
            }
            return wall;
        }

        internal static FixtureAttachment Place(FixtureChoice choice, Transform host, Vector3 point, int side)
        {
            var root = new GameObject(choice.label);
            var fixture = root.AddComponent<FixtureAttachment>();
            fixture.mount = !host ? choice.mount : host.TryGetComponent<HouseFloor>(out _) ? choice.HorizontalMount : FixtureMount.Wall;
            Undo.RegisterCreatedObjectUndo(root, "Place fixture");
            if (host && !host.GetComponent<FillerBuilding>()) Mount(fixture, host, point, side);
            else
            {
                fixture.freePlacement = !host;
                root.transform.SetParent(host,false);
                root.transform.position = point;
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(choice.prefab, root.transform);
            Undo.RegisterCreatedObjectUndo(model, "Place fixture");
            ApplyPreset(choice, fixture, model);
            return fixture;
        }

        internal static bool Refit(FixtureAttachment fixture)
        {
            var choice = FindChoice(fixture, out var model);
            if (choice == null) return false;
            ApplyPreset(choice, fixture, model);
            return true;
        }

        static FixtureChoice FindChoice(FixtureAttachment fixture, out GameObject model)
        {
            var bound = fixture.LightPreset;
            foreach (Transform child in fixture.transform)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
                var direct = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (!direct && !source) continue;
                model = child.gameObject;
                if (bound != null && (bound.prefab == direct || bound.prefab == source)) return bound;
                var matches = Choices.Where(c => c.Supports(fixture.mount) && (c.prefab == direct || c.prefab == source)).ToArray();
                var choice = matches.FirstOrDefault(c => c.label == fixture.name);
                if (choice == null && matches.Length == 1) choice = matches[0];
                if (choice != null) return choice;
            }
            model = null;
            return null;
        }

        static Bounds ModelBounds(Transform root, GameObject model)
        {
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                // Mount the housing at the surface regardless of the size of its beam.
                if (r.sharedMaterial && r.sharedMaterial.GetTag("FixtureEffect", false) != "") continue;
                var b = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = r.transform.TransformPoint(new Vector3((i&1)==0?b.min.x:b.max.x,
                        (i&2)==0?b.min.y:b.max.y, (i&4)==0?b.min.z:b.max.z));
                    p = root.InverseTransformPoint(p);
                    if (initialized) bounds.Encapsulate(p); else { bounds = new Bounds(p, Vector3.zero); initialized = true; }
                }
            }
            return bounds;
        }

        static float ModelDepth(FixtureMount mount, Bounds bounds) =>
            mount == FixtureMount.Wall ? bounds.size.z : bounds.size.y;

        static void FitEmitter(FixtureAttachment fixture, Light light, Bounds bounds)
        {
            Undo.RecordObjects(new Object[] { fixture, light, light.transform }, "Aim fixture light");
            var mount = fixture.mount;
            var outward = fixture.LightOffsetDirection;
            bool omni = mount == FixtureMount.Floor;
            // Wall and ceiling fittings have opaque backs. A wide, feathered beam
            // lights the room without sending its brightest rays back into the mount.
            light.transform.localPosition = outward * (ModelDepth(mount, bounds) + (omni ? .65f : mount == FixtureMount.Wall ? .34f : .12f) + fixture.lightOffset);
            light.type = omni ? LightType.Point : LightType.Spot;
            if (!omni)
            {
                light.transform.localRotation = mount == FixtureMount.Wall ? Quaternion.Euler(60,0,0) : Quaternion.Euler(90,0,0);
            }
            fixture.emitterFitted = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }

        internal static bool UpgradeEmitter(FixtureAttachment fixture)
        {
            if (fixture.emitterFitted) return false;
            var emitter = fixture.transform.Find("Light");
            if (!emitter || !emitter.TryGetComponent<Light>(out var light) || light.type != LightType.Point ||
                PrefabUtility.IsAnyPrefabInstanceRoot(emitter.gameObject)) return false;
            var model = fixture.transform.GetChild(0).gameObject;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(model);
            var preset = source ? Choices.FirstOrDefault(c => c.prefab == source && c.mount == fixture.mount && c.lightRange > 0) : null;
            if (preset == null) return false;
            var bounds = ModelBounds(fixture.transform, model);
            var original = fixture.LightOffsetDirection * (ModelDepth(fixture.mount, bounds) + .04f);
            if (Vector3.Distance(emitter.localPosition, original) > .002f) return false;
            FitEmitter(fixture, light, bounds);
            BindEmitter(fixture, preset, light);
            if (preset.previousLightIntensity > 0 && Mathf.Approximately(light.intensity, preset.previousLightIntensity))
            {
                light.intensity = preset.originalRange > 0 ? preset.originalIntensity : preset.lightIntensity;
            }
            fixture.lightingInitialized = false;
            SyncLighting(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            return true;
        }

        static void ApplyPreset(FixtureChoice choice, FixtureAttachment fixture, GameObject model)
        {
            Undo.RecordObject(fixture, "Fit fixture model");
            fixture.allowOpeningSurface = choice.allowOpeningSurface;
            var root = fixture.transform;
            var t = model.transform;
            Undo.RecordObject(t, "Fit fixture model");
            t.localRotation = fixture.freePlacement ? Quaternion.identity : Quaternion.Euler(choice.modelRotation);
            if (choice.wallAndCeiling && fixture.mount == FixtureMount.Ceiling)
                t.localRotation = Quaternion.Euler(90, 0, 0) * t.localRotation;
            t.localPosition = Vector3.zero;
            var bounds = ModelBounds(root, model);
            var center = bounds.center;
            if (fixture.mount == FixtureMount.Wall) center.z = choice.recessedWallMount ? bounds.center.z : bounds.min.z;
            else center.y = fixture.mount == FixtureMount.Floor ? bounds.min.y : bounds.max.y;
            Vector3 clearance = fixture.mount == FixtureMount.Wall ? Vector3.forward * choice.surfaceClearance : Vector3.zero;
            t.localPosition = fixture.freePlacement ? Vector3.zero : -center + clearance;
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            var diffuser = model.GetComponentInChildren<Light>(true);
            if (choice.lightRange<=0)
            {
                BindEmitter(fixture,choice,null);
                return;
            }
            if (diffuser)
            {
                var old = root.Find("Light");
                var light = old && old != t && old.TryGetComponent<Light>(out var legacy) ? legacy : diffuser;
                Undo.RecordObjects(new Object[] { fixture, light, light.transform, diffuser }, "Fit fixture light");
                if (light == diffuser && (fixture.lightOffset > 0 || choice.recessedWallMount))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(light.transform);
                    var origin = root.InverseTransformPoint(light.transform.parent.TransformPoint(source.localPosition));
                    if (choice.recessedWallMount && fixture.mount == FixtureMount.Wall && !fixture.freePlacement)
                        origin.z = Mathf.Max(origin.z, bounds.extents.z * .5f);
                    light.transform.position = root.TransformPoint(origin) + root.TransformDirection(fixture.LightOffsetDirection) * fixture.lightOffset;
                    fixture.appliedOffsetAxis=fixture.LightOffsetDirection;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
                }
                fixture.emitterFitted = true;
                fixture.lightingInitialized = true;
                BindEmitter(fixture, choice, light);
                SyncLighting(fixture);
            }
            else
            {
                var child = root.Find("Light");
                var light = child && child != t ? child.GetComponent<Light>() : null;
                if (!light)
                {
                    var emitter = new GameObject("Light");
                    emitter.transform.SetParent(root, false);
                    light = emitter.AddComponent<Light>();
                    light.shadows = LightShadows.None;
                    Undo.RegisterCreatedObjectUndo(emitter, "Add fixture light");
                }
                FitEmitter(fixture, light, bounds);
                if (choice.wallAndCeiling && fixture.mount == FixtureMount.Wall) light.transform.localRotation = Quaternion.identity;
                if (fixture.mount == FixtureMount.Floor) light.intensity = choice.lightIntensity;
                fixture.lightingInitialized = true;
                BindEmitter(fixture, choice, light);
                fixture.ApplyLighting(choice);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
        }

        static void BindEmitter(FixtureAttachment fixture, FixtureChoice choice, Light light)
        {
            fixture.emitter = light;
            fixture.lightPrefab = choice.prefab;
            if (fixture.LightPreset != choice)
                fixture.lightPalette = OwnerOf(choice);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
        }

        static FixturePalette OwnerOf(FixtureChoice choice)
        {
            bool Owns(FixturePalette p) => p && p.fixtures != null && System.Array.IndexOf(p.fixtures, choice) >= 0;
            var owner = Palettes.FirstOrDefault(Owns);
            if (owner) return owner;
            palettes = null; // A synchronous import can precede projectChanged.
            return Palettes.FirstOrDefault(Owns);
        }

        internal static bool SyncLighting(FixtureAttachment fixture)
        {
            var preset = fixture.LightPreset;
            bool bound = false;
            if (!fixture.emitter || preset == null)
            {
                if (!fixture.emitterFitted) return false;
                var child = fixture.transform.Find("Light");
                if (!child || !child.TryGetComponent<Light>(out var light) || PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) return false;
                preset = FindChoice(fixture, out _);
                if (preset == null || preset.lightRange <= 0) return false;
                if (!fixture.lightPalette && light.type == LightType.Spot && !Mathf.Approximately(light.spotAngle, 155f))
                {
                    fixture.overrideSpread = true;
                    fixture.localSpread = light.spotAngle;
                }
                BindEmitter(fixture, preset, light);
                bound = true;
            }
            bool fitted = fixture.FitLegacyPoint(out var diffuser);
            if (fitted)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(diffuser);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.emitter.transform);
            }
            if (fixture.ApplyLighting(preset) || fitted)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.emitter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.emitter.transform);
                return true;
            }
            return bound;
        }

        internal static void RefreshLighting(FixturePalette palette)
        {
            foreach (var fixture in Object.FindObjectsByType<FixtureAttachment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (palette && fixture.lightPalette && fixture.lightPalette != palette) continue;
                var scene = fixture.gameObject.scene;
                if (scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene) && SyncLighting(fixture) && !scene.isDirty)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
            SceneView.RepaintAll();
        }

        internal enum LightField { Spread, Intensity, Range }

        internal static void SetSpread(FixtureAttachment fixture, bool local, float value) =>
            SetLightField(fixture, LightField.Spread, local, value);

        internal static void SetLightField(FixtureAttachment fixture, LightField field, bool local, float value)
        {
            var preset = fixture.LightPreset;
            if (!fixture.emitter || preset == null) return;
            bool angle = field == LightField.Spread && fixture.emitter.type == LightType.Spot;
            value = angle ? Mathf.Clamp(value, 1, 179) : Mathf.Max(field == LightField.Intensity ? 0 : .01f, value);
            bool wasLocal = field == LightField.Intensity ? fixture.intensity.enabled :
                field == LightField.Range ? fixture.range.enabled : fixture.overrideSpread;
            if (local || wasLocal)
            {
                Undo.RecordObjects(new Object[] { fixture, fixture.emitter }, "Change fixture lighting override");
                var setting = new FixtureAttachment.LocalValue { enabled = local, value = value };
                if (field == LightField.Intensity) fixture.intensity = setting;
                else if (field == LightField.Range) fixture.range = setting;
                else { fixture.overrideSpread = local; fixture.localSpread = value; }
                SyncLighting(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            }
            else
            {
                Undo.RecordObject(fixture.lightPalette, ChangePreset);
                Undo.SetCurrentGroupName(ChangePreset);
                if (preset.originalRange <= 0)
                {
                    preset.originalRange = preset.lightRange;
                    preset.originalIntensity = preset.lightIntensity;
                }
                if (field == LightField.Intensity) preset.lightIntensity = value;
                else if (angle) preset.beamAngle = value;
                else preset.lightRange = value;
                EditorUtility.SetDirty(fixture.lightPalette);
                RefreshLighting(fixture.lightPalette);
                if (SyncLighting(fixture)) EditorSceneManager.MarkSceneDirty(fixture.gameObject.scene);
            }
        }

        internal static void SetLightOffset(FixtureAttachment fixture, float value)
        {
            var emitter = fixture.emitter.transform;
            value = Mathf.Clamp01(value);
            Undo.RecordObjects(new Object[] { fixture, emitter }, "Move fixture light source");
            fixture.overrideOffset = true;
            fixture.MoveEmitter(value);
            PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            SceneView.RepaintAll();
        }

        internal static void DrawLighting(FixtureAttachment fixture)
        {
            var preset = fixture.LightPreset;
            if (!fixture.emitter || preset == null) return;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                bool shared = editingDefaults==fixture;
                if (GUILayout.Toggle(shared,"Edit shared defaults · "+preset.label,GUI.skin.button)!=shared)
                    editingDefaults=shared ? null : fixture;
                if (editingDefaults==fixture)
                {
                    EditorGUI.BeginChangeCheck();
                    bool power=EditorGUILayout.Toggle("On",!preset.initiallyOff);
                    var color=EditorGUILayout.ColorField("Color",preset.lightColor);
                    float strength=Mathf.Max(0,EditorGUILayout.DelayedFloatField("Intensity",preset.lightIntensity));
                    float reach=Mathf.Max(.01f,EditorGUILayout.DelayedFloatField("Range (m)",preset.lightRange));
                    float offset=EditorGUILayout.Slider("Light offset (m)",preset.emitterOffset,0,1);
                    var axis=fixture.freePlacement ? EditorGUILayout.Vector3Field("Free offset axis",preset.freeOffsetAxis) : preset.freeOffsetAxis;
                    float angle=fixture.emitter.type==LightType.Spot ? EditorGUILayout.Slider("Beam angle (°)",preset.beamAngle,1,179) : preset.beamAngle;
                    if(EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(fixture.lightPalette,ChangePreset);
                        Undo.SetCurrentGroupName(ChangePreset);
                        preset.initiallyOff=!power; preset.lightColor=color; preset.lightIntensity=strength;
                        preset.lightRange=reach; preset.emitterOffset=offset; preset.beamAngle=angle;
                        preset.freeOffsetAxis=axis.sqrMagnitude>.0001f ? axis.normalized : Vector3.up;
                        EditorUtility.SetDirty(fixture.lightPalette);
                        RefreshLighting(fixture.lightPalette);
                    }
                    return;
                }
                EditorGUILayout.BeginHorizontal();
                var on = EditorGUILayout.Toggle("On", fixture.emitter.enabled);
                if (on != fixture.emitter.enabled) SetAppearance(fixture, () => { fixture.overridePower = true; fixture.localOn = on; });
                Reset(fixture.overridePower, () => fixture.overridePower = false);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                var tint = EditorGUILayout.ColorField("Color", fixture.emitter.color);
                if (tint != fixture.emitter.color) SetAppearance(fixture, () => { fixture.overrideColor = true; fixture.localColor = tint; });
                Reset(fixture.overrideColor, () => fixture.overrideColor = false);
                EditorGUILayout.EndHorizontal();
                DrawField(LightField.Intensity, "Intensity", fixture.intensity.enabled, fixture.intensity.value, preset.lightIntensity);
                if (fixture.mount != FixtureMount.Floor || fixture.emitter.type != LightType.Point)
                    DrawField(LightField.Range, "Range (m)", fixture.range.enabled, fixture.range.value, preset.lightRange);
                if (fixture.mount == FixtureMount.Floor && fixture.emitter.type == LightType.Point || fixture.emitter.type == LightType.Spot)
                    DrawField(LightField.Spread, fixture.emitter.type == LightType.Point ? "Light radius (m)" : "Beam angle (°)",
                        fixture.overrideSpread, fixture.localSpread, fixture.emitter.type == LightType.Point ? preset.lightRange : preset.beamAngle);
                if (!fixture.emitter.GetComponentInChildren<Renderer>(true))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    float offset = EditorGUILayout.Slider(new GUIContent("Light offset (m)",
                        "Move this light source outward from its mounted position."), fixture.lightOffset, 0, 1);
                    if (EditorGUI.EndChangeCheck()) SetLightOffset(fixture, offset);
                    Reset(fixture.overrideOffset, () => fixture.overrideOffset = false);
                    EditorGUILayout.EndHorizontal();
                }
                if (preset.wallAndCeiling && !fixture.freePlacement)
                {
                    EditorGUI.BeginChangeCheck();
                    float roll = EditorGUILayout.FloatField("Surface rotation (°)", fixture.surfaceRotation);
                    if (EditorGUI.EndChangeCheck()) SetRotation(fixture, roll);
                }
            }

            void DrawField(LightField field, string label, bool local, float localValue, float sharedValue)
            {
                EditorGUILayout.BeginHorizontal();
                float value = local ? localValue : sharedValue;
                EditorGUI.BeginChangeCheck();
                float next = field == LightField.Spread && fixture.emitter.type == LightType.Spot
                    ? EditorGUILayout.Slider(label, value, 1, 179)
                    : EditorGUILayout.DelayedFloatField(label, value);
                if (EditorGUI.EndChangeCheck()) SetLightField(fixture, field, true, next);
                using (new EditorGUI.DisabledScope(!local))
                    if (GUILayout.Button("Inherit",GUILayout.Width(52))) SetLightField(fixture,field,false,value);
                EditorGUILayout.EndHorizontal();
            }
            void Reset(bool active, System.Action reset)
            {
                using (new EditorGUI.DisabledScope(!active))
                    if (GUILayout.Button("Inherit", GUILayout.Width(52))) SetAppearance(fixture,reset);
            }
        }

        const string LocalLighting = "Change fixture appearance";
        internal static void SetAppearance(FixtureAttachment fixture, System.Action edit)
        {
            Undo.RecordObjects(new Object[]{fixture,fixture.emitter,fixture.emitter.transform},LocalLighting);
            edit();
            SyncLighting(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.emitter.transform);
            SceneView.RepaintAll();
        }

        static Quaternion MountedRotation(FixtureAttachment fixture)
        {
            var facing = fixture.mount == FixtureMount.Wall && fixture.wallSide < 0 ? Quaternion.Euler(0,180,0) : Quaternion.identity;
            return facing * Quaternion.AngleAxis(fixture.surfaceRotation, fixture.LightOffsetDirection);
        }

        internal static void SetRotation(FixtureAttachment fixture, float degrees)
        {
            Undo.RecordObjects(new Object[] { fixture, fixture.transform }, "Rotate mounted fixture");
            fixture.surfaceRotation = Mathf.DeltaAngle(0, degrees);
            if (fixture.facadeEdge!=0 && fixture.transform.parent.TryGetComponent<FillerBuilding>(out var building) &&
                building.footprint.edges.Find(e=>e.id==fixture.facadeEdge) is GroundEdge edge)
                fixture.FitFacade(building,edge);
            else fixture.transform.localRotation = MountedRotation(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.transform);
        }

        internal static void Mount(FixtureAttachment fixture, Transform host, Vector3 point, int side)
        {
            var t = fixture.transform;
            if (t.parent != host) Undo.SetTransformParent(t, host, "Attach fixture");
            Undo.RecordObjects(new Object[]{fixture,t}, "Attach fixture");
            var oldMount = fixture.mount;
            bool wasFree=fixture.freePlacement;
            fixture.freePlacement=false;
            fixture.facadeEdge=0;
            if (fixture.LightPreset is FixtureChoice preset)
                fixture.mount = host.TryGetComponent<HouseWall>(out _) ? FixtureMount.Wall : preset.HorizontalMount;
            fixture.wallSide = side;
            var local = host.InverseTransformPoint(point);
            fixture.surfaceOffset = fixture.mount == FixtureMount.Wall && host.TryGetComponent<HouseWall>(out var wall)
                ? local.z - side * wall.Thickness * .5f : 0;
            t.SetLocalPositionAndRotation(fixture.MountedPosition(local), MountedRotation(fixture));
            if (wasFree || oldMount != fixture.mount) Refit(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        internal static void Refresh(Transform host)
        {
            foreach (Transform child in host)
                if (child.TryGetComponent<FixtureAttachment>(out var fixture)) Constrain(fixture);
        }

        internal static void Constrain(FixtureAttachment fixture)
        {
            var t = fixture.transform;
            if (!t.parent) return;
            var pos = fixture.MountedPosition(t.localPosition);
            if ((pos - t.localPosition).sqrMagnitude < 1e-12f) return;
            Undo.RecordObject(t,"Keep fixture on surface");
            t.localPosition = pos;
        }

        internal static void Slide(FixtureAttachment fixture, Vector3 worldPoint)
        {
            var t = fixture.transform;
            if(fixture.facadeEdge!=0 && t.parent.TryGetComponent<FillerBuilding>(out var building))
            {
                var edge=building.footprint.edges.Find(e=>e.id==fixture.facadeEdge);
                if(edge==null)return;
                Undo.RecordObjects(new Object[]{fixture,t},"Slide façade fixture");
                SetFacadeAnchor(fixture,building,edge,worldPoint,t.forward);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                return;
            }
            var local = fixture.MountedPosition(t.parent.InverseTransformPoint(worldPoint));
            if ((t.localPosition - local).sqrMagnitude < 1e-12f) return;
            Undo.RecordObject(t, "Slide fixture on surface");
            t.localPosition = local;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        internal static void SelectionFields(FixtureAttachment fixture)
        {
            if (!fixture.allowOpeningSurface) return;
            var available = Choices.Where(c => c.mount == fixture.mount && c.allowOpeningSurface).ToArray();
            var model = fixture.transform.Cast<Transform>().FirstOrDefault(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject));
            var source = model ? PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject) : null;
            int current = System.Array.FindIndex(available, c => c.prefab == source);
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUILayout.Popup("Advertisement", current, available.Select(c => c.label).ToArray());
            if (EditorGUI.EndChangeCheck() && chosen >= 0 && chosen < available.Length)
                ReplacePreset(fixture, available[chosen]);
        }

        internal static void ReplacePreset(FixtureAttachment fixture, FixtureChoice choice)
        {
            if (choice.mount != fixture.mount || !choice.allowOpeningSurface)
                throw new System.ArgumentException("Choose a compatible surface decoration.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Change advertisement");
            foreach (Transform child in fixture.transform.Cast<Transform>().ToArray())
                Undo.DestroyObjectImmediate(child.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(choice.prefab, fixture.transform);
            Undo.RegisterCreatedObjectUndo(model, "Change advertisement");
            Undo.RecordObject(fixture.gameObject, "Change advertisement");
            fixture.name = choice.label;
            ApplyPreset(choice, fixture, model);
            Undo.CollapseUndoOperations(group);
        }
    }

    [CustomEditor(typeof(FixturePalette))]
    internal sealed class FixturePaletteInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector())
            {
                Undo.SetCurrentGroupName(FixtureTool.ChangePreset);
                FixtureTool.RefreshLighting((FixturePalette)target);
            }
        }
    }

    [CustomEditor(typeof(FixtureAttachment))]
    internal sealed class FixtureInspector : UnityEditor.Editor
    {
        bool ownsHandle, previousHidden;

        void OnDisable() => UseMountHandle(false);

        void UseMountHandle(bool active)
        {
            if (active && !ownsHandle) previousHidden = Tools.hidden;
            if (active || ownsHandle) Tools.hidden = active || previousHidden;
            ownsHandle = active;
        }

        public override void OnInspectorGUI()
        {
            var fixture = (FixtureAttachment)target;
            EditorGUILayout.LabelField("Mount",fixture.freePlacement ? "Free" : fixture.mount.ToString());
            FixtureTool.SelectionFields(fixture);
            FixtureTool.DrawLighting(fixture);
            if (GUILayout.Button("Reattach to surface")) FixtureTool.BeginMove(fixture);
            if (GUILayout.Button(new GUIContent("Reapply fixture preset", "Refit the model and restore the generated light's position, range and intensity.")) && !FixtureTool.Refit(fixture))
                EditorWindow.focusedWindow?.ShowNotification(new GUIContent("Matching fixture preset not found."));
        }

        void OnSceneGUI()
        {
            var fixture = (FixtureAttachment)target;
            if (Event.current.type == EventType.Repaint && fixture.emitter &&
                Selection.activeGameObject == fixture.gameObject && SceneView.currentDrawingSceneView is SceneView view && view.camera)
            {
                var source = fixture.emitter.transform.position;
                var facing = view.camera.transform.forward;
                float radius = HandleUtility.GetHandleSize(source) * .065f;
                using (new Handles.DrawingScope(new Color(1f, .8f, .23f)))
                {
                    Handles.DrawDottedLine(fixture.transform.position, source, 4f);
                    var direction = fixture.transform.TransformDirection(fixture.LightOffsetDirection);
                    Handles.ArrowHandleCap(0,source,Quaternion.LookRotation(direction),radius*3,EventType.Repaint);
                    using (new Handles.DrawingScope(Color.black))
                        Handles.DrawWireDisc(source, facing, radius, 4f);
                    Handles.DrawWireDisc(source, facing, radius, 2f);
                    Handles.DrawSolidDisc(source, facing, radius * .22f);
                }
            }
            var floor = fixture.GetComponentInParent<HouseFloor>();
            bool rotate = Tools.current == Tool.Rotate && fixture.LightPreset?.wallAndCeiling == true;
            bool move = Tools.current == Tool.Move || Tools.current == Tool.Transform;
            UseMountHandle(!fixture.freePlacement && (floor || fixture.facadeEdge!=0) && Selection.activeGameObject == fixture.gameObject &&
                (HouseScene.Mode != HouseTool.Fixture || !HouseScene.Floor) && (move || rotate));
            if (!ownsHandle || Tools.viewToolActive || Event.current.alt) return;
            if (rotate)
            {
                var t = fixture.transform;
                var normal = fixture.mount == FixtureMount.Wall ? t.forward : -t.up;
                EditorGUI.BeginChangeCheck();
                var rotation = Handles.Disc(t.rotation, t.position, normal, HandleUtility.GetHandleSize(t.position) * .8f,
                    false, EditorSnapSettings.rotate);
                if (EditorGUI.EndChangeCheck())
                    FixtureTool.SetRotation(fixture, fixture.surfaceRotation + Vector3.SignedAngle(t.right, rotation * Vector3.right, normal));
                return;
            }
            var host = fixture.transform.parent;
            bool wall = fixture.mount == FixtureMount.Wall;
            var frame = fixture.facadeEdge!=0 ? Quaternion.LookRotation(host.up,-fixture.transform.forward) :
                wall ? Quaternion.LookRotation(host.up, -fixture.wallSide * host.forward) : host.rotation;
            EditorGUI.BeginChangeCheck();
            var moved = PlanarMoveHandle.Draw(fixture.transform.position, frame, wall ? Handles.yAxisColor : Handles.zAxisColor);
            if (EditorGUI.EndChangeCheck()) FixtureTool.Slide(fixture, moved);
        }
    }
}
