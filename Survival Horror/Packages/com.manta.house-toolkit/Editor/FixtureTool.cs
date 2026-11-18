using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    internal static class FixtureTool
    {
        static FixturePalette[] palettes;
        static FixturePalette brushPalette;
        static GameObject brushPrefab;
        static FixtureMount brushMount;
        // Unity can deserialize a palette between drawing its swatch and clicking the scene.
        internal static FixtureChoice Brush => brushPalette ? brushPalette.Find(brushPrefab, brushMount) : null;
        static FixtureAttachment moving;
        internal const string ChangePreset = "Change fixture lighting preset";
        internal static void Cancel() => moving = null;
        static FixtureTool()
        {
            EditorApplication.projectChanged += () => palettes = null;
            Undo.undoRedoEvent += (in UndoRedoInfo info) =>
            {
                if (info.undoName == ChangePreset) RefreshLighting(null);
            };
        }

        static FixturePalette[] Palettes => palettes ??= AssetDatabase.FindAssets("t:FixturePalette")
            .Select(id => AssetDatabase.LoadAssetAtPath<FixturePalette>(AssetDatabase.GUIDToAssetPath(id)))
            .Where(p => p).ToArray();

        static FixtureChoice[] Choices => Palettes
            .Where(p => p && p.fixtures != null).SelectMany(p => p.fixtures)
            .Where(c => c != null && c.prefab).ToArray();

        internal static void Palette()
        {
            if (moving)
            {
                EditorGUILayout.HelpBox("Click a mounting surface to move " + moving.name + ".", MessageType.Info);
                if (GUILayout.Button("Cancel move")) Finish(moving);
                return;
            }
            var choices = Choices;
            var brush = Brush ?? choices.FirstOrDefault();
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

        static string MountLabel(FixtureChoice choice) => choice.wallAndCeiling ? "wall or ceiling" : choice.mount.ToString().ToLowerInvariant();

        internal static void SelectBrush(FixtureChoice choice)
        {
            brushPalette = OwnerOf(choice);
            brushPrefab = choice.prefab;
            brushMount = choice.mount;
        }

        internal static void BeginMove(FixtureAttachment fixture)
        {
            var floor = fixture.GetComponentInParent<HouseFloor>();
            if (!floor) return;
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

        internal static bool Target(HouseFloor floor, FixtureMount mount, Ray ray, out Transform host, out Vector3 point, out int side)
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
                if (!wall || wall.Floor != floor || wall.boundaryOnly || hit.collider.GetComponentInParent<InsertAttachment>())
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
            if (!TargetSurface(HouseScene.Floor, choice, mount, ray, out var host, out var point, out int side)) return;
            if (choice != null && choice.wallAndCeiling) mount = host == HouseScene.Floor.transform ? FixtureMount.Ceiling : FixtureMount.Wall;
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
            out Transform host, out Vector3 point, out int side)
        {
            if (choice == null || !choice.wallAndCeiling) return Target(floor, mount, ray, out host, out point, out side);
            bool wall = Target(floor, FixtureMount.Wall, ray, out host, out point, out side);
            if (Target(floor, FixtureMount.Ceiling, ray, out var ceiling, out var hit, out _) &&
                (!wall || (Vector3.Dot(ray.direction, floor.transform.up) > 0 &&
                (hit - ray.origin).sqrMagnitude < (point - ray.origin).sqrMagnitude)))
            {
                host = ceiling; point = hit; side = 1;
                return true;
            }
            return wall;
        }

        internal static FixtureAttachment Place(FixtureChoice choice, Transform host, Vector3 point, int side)
        {
            var root = new GameObject(choice.label);
            var fixture = root.AddComponent<FixtureAttachment>();
            fixture.mount = choice.wallAndCeiling && host.TryGetComponent<HouseFloor>(out _) ? FixtureMount.Ceiling : choice.mount;
            Undo.RegisterCreatedObjectUndo(root, "Place fixture");
            Mount(fixture, host, point, side);
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

        static Vector3 Outward(FixtureMount mount) => mount == FixtureMount.Wall ? Vector3.forward :
            mount == FixtureMount.Ceiling ? Vector3.down : Vector3.up;

        static float ModelDepth(FixtureMount mount, Bounds bounds) =>
            mount == FixtureMount.Wall ? bounds.size.z : bounds.size.y;

        static void FitEmitter(FixtureAttachment fixture, Light light, Bounds bounds)
        {
            Undo.RecordObjects(new Object[] { fixture, light, light.transform }, "Aim fixture light");
            var mount = fixture.mount;
            var outward = Outward(mount);
            bool omni = mount == FixtureMount.Floor;
            // Wall and ceiling fittings have opaque backs. A wide, feathered beam
            // lights the room without sending its brightest rays back into the mount.
            light.transform.localPosition = outward * (ModelDepth(mount, bounds) + (omni ? .65f : mount == FixtureMount.Wall ? .34f : .12f));
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
            var original = Outward(fixture.mount) * (ModelDepth(fixture.mount, bounds) + .04f);
            if (Vector3.Distance(emitter.localPosition, original) > .002f) return false;
            FitEmitter(fixture, light, bounds);
            BindEmitter(fixture, preset, light);
            if (preset.previousLightIntensity > 0 && Mathf.Approximately(light.intensity, preset.previousLightIntensity))
            {
                light.intensity = preset.originalRange > 0 ? preset.originalIntensity : preset.lightIntensity;
            }
            fixture.lightingInitialized = false;
            fixture.ApplyLighting(preset);
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            return true;
        }

        static void ApplyPreset(FixtureChoice choice, FixtureAttachment fixture, GameObject model)
        {
            var root = fixture.transform;
            var t = model.transform;
            Undo.RecordObject(t, "Fit fixture model");
            t.localRotation = Quaternion.Euler(choice.modelRotation);
            if (choice.wallAndCeiling && fixture.mount == FixtureMount.Ceiling)
                t.localRotation = Quaternion.Euler(90, 0, 0) * t.localRotation;
            t.localPosition = Vector3.zero;
            var bounds = ModelBounds(root, model);
            var center = bounds.center;
            if (fixture.mount == FixtureMount.Wall) center.z = bounds.min.z;
            else center.y = fixture.mount == FixtureMount.Floor ? bounds.min.y : bounds.max.y;
            t.localPosition = -center;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            if (choice.lightRange > 0 && !model.GetComponentInChildren<Light>(true))
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
            if (fixture.ApplyLighting(preset))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.emitter);
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
            bool angle = field == LightField.Spread && fixture.mount != FixtureMount.Floor;
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

        internal static void DrawLighting(FixtureAttachment fixture)
        {
            var preset = fixture.LightPreset;
            if (!fixture.emitter || preset == null) return;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.LabelField("Shared preset", preset.label);
                if (fixture.mount != FixtureMount.Floor)
                {
                    DrawField(LightField.Intensity, "Intensity", fixture.intensity.enabled, fixture.intensity.value, preset.lightIntensity);
                    DrawField(LightField.Range, "Range (m)", fixture.range.enabled, fixture.range.value, preset.lightRange);
                }
                DrawField(LightField.Spread, fixture.mount == FixtureMount.Floor ? "Light radius (m)" : "Beam angle (°)",
                    fixture.overrideSpread, fixture.localSpread, preset.Spread);
                if (preset.wallAndCeiling)
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
                float next = field == LightField.Spread && fixture.mount != FixtureMount.Floor
                    ? EditorGUILayout.Slider(label, value, 1, 179)
                    : EditorGUILayout.DelayedFloatField(label, value);
                if (EditorGUI.EndChangeCheck()) SetLightField(fixture, field, local, next);
                bool nextLocal = GUILayout.Toggle(local, new GUIContent("Local", "Override only this setting on this fixture."), GUILayout.Width(52));
                if (nextLocal != local) SetLightField(fixture, field, nextLocal, value);
                EditorGUILayout.EndHorizontal();
            }
        }

        static Quaternion MountedRotation(FixtureAttachment fixture)
        {
            var facing = fixture.mount == FixtureMount.Wall && fixture.wallSide < 0 ? Quaternion.Euler(0,180,0) : Quaternion.identity;
            return facing * Quaternion.AngleAxis(fixture.surfaceRotation, Outward(fixture.mount));
        }

        internal static void SetRotation(FixtureAttachment fixture, float degrees)
        {
            Undo.RecordObjects(new Object[] { fixture, fixture.transform }, "Rotate mounted fixture");
            fixture.surfaceRotation = Mathf.DeltaAngle(0, degrees);
            fixture.transform.localRotation = MountedRotation(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.transform);
        }

        internal static void Mount(FixtureAttachment fixture, Transform host, Vector3 point, int side)
        {
            var t = fixture.transform;
            if (t.parent != host) Undo.SetTransformParent(t, host, "Attach fixture");
            Undo.RecordObjects(new Object[]{fixture,t}, "Attach fixture");
            var oldMount = fixture.mount;
            if (fixture.LightPreset is FixtureChoice preset && preset.wallAndCeiling)
                fixture.mount = host.TryGetComponent<HouseWall>(out _) ? FixtureMount.Wall : FixtureMount.Ceiling;
            fixture.wallSide = side;
            var local = host.InverseTransformPoint(point);
            fixture.surfaceOffset = fixture.mount == FixtureMount.Wall && host.TryGetComponent<HouseWall>(out var wall)
                ? local.z - side * wall.Thickness * .5f : 0;
            t.SetLocalPositionAndRotation(fixture.MountedPosition(local), MountedRotation(fixture));
            if (oldMount != fixture.mount) Refit(fixture);
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
        bool ownsRotationHandle, previousHidden;

        void OnDisable() => ShowRotationHandle(false);

        void ShowRotationHandle(bool useDisc)
        {
            if (useDisc == ownsRotationHandle) return;
            if (useDisc) previousHidden = Tools.hidden;
            Tools.hidden = useDisc || previousHidden;
            ownsRotationHandle = useDisc;
        }

        public override void OnInspectorGUI()
        {
            var fixture = (FixtureAttachment)target;
            EditorGUILayout.LabelField("Mount",fixture.mount.ToString());
            FixtureTool.DrawLighting(fixture);
            if (GUILayout.Button("Move fixture")) FixtureTool.BeginMove(fixture);
            if (GUILayout.Button(new GUIContent("Reapply fixture preset", "Refit the model and restore the generated light's position, range and intensity.")) && !FixtureTool.Refit(fixture))
                EditorWindow.focusedWindow?.ShowNotification(new GUIContent("Matching fixture preset not found."));
        }

        void OnSceneGUI()
        {
            var fixture = (FixtureAttachment)target;
            var floor = fixture.GetComponentInParent<HouseFloor>();
            ShowRotationHandle(floor && Tools.current == Tool.Rotate && Selection.activeGameObject == fixture.gameObject &&
                fixture.LightPreset?.wallAndCeiling == true);
            if (ownsRotationHandle && !Tools.viewToolActive && !Event.current.alt)
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
            if (!floor || Tools.current != Tool.Move || Tools.viewToolActive || Event.current.alt) return;
            if (Event.current.rawType == EventType.MouseUp) FixtureTool.Constrain(fixture);
            using (new Handles.DrawingScope(Color.green))
            {
                var point = fixture.transform.position;
                EditorGUI.BeginChangeCheck();
                Handles.FreeMoveHandle(point, HandleUtility.GetHandleSize(point)*.075f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck() && FixtureTool.TargetSurface(floor, fixture.LightPreset, fixture.mount,
                    HandleUtility.GUIPointToWorldRay(Event.current.mousePosition), out var host, out var hit, out int side))
                    FixtureTool.Mount(fixture, host, hit, side);
            }
        }
    }
}
