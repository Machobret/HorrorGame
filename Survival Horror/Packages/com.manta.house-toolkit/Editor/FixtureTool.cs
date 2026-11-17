using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    internal static class FixtureTool
    {
        static FixtureChoice[] choices;
        static FixtureChoice brush;
        static FixtureAttachment moving;
        internal static void Cancel() => moving = null;
        static FixtureTool() => EditorApplication.projectChanged += () => { choices = null; brush = null; };

        static FixtureChoice[] Choices => choices ??= AssetDatabase.FindAssets("t:FixturePalette")
            .Select(id => AssetDatabase.LoadAssetAtPath<FixturePalette>(AssetDatabase.GUIDToAssetPath(id)))
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
            brush ??= Choices.FirstOrDefault();
            if (brush == null) { EditorGUILayout.HelpBox("Add prefabs to a Fixture Palette asset.", MessageType.Info); return; }
            for (int i = 0; i < choices.Length; i += 3)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 3, choices.Length); j++)
                {
                    var choice = choices[j];
                    EditorGUILayout.BeginVertical(GUILayout.MinWidth(55));
                    var image = AssetPreview.GetAssetPreview(choice.prefab);
                    if (!image && AssetPreview.IsLoadingAssetPreview(choice.prefab.GetInstanceID())) HouseProperties.RepaintOpen();
                    if (GUILayout.Toggle(brush == choice, new GUIContent(image ? image : AssetPreview.GetMiniThumbnail(choice.prefab),
                        choice.label + " (" + choice.mount + ")"), GUI.skin.button, GUILayout.Height(52))) brush = choice;
                    GUILayout.Label(choice.label, EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Label("Mount on " + brush.mount.ToString().ToLowerInvariant(), EditorStyles.miniLabel);
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
            if (e.alt || Tools.viewToolActive || (!moving && brush == null)) return;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseMove && e.type != EventType.Repaint) return;
            var mount = moving ? moving.mount : brush.mount;
            if (!Target(HouseScene.Floor, mount, ray, out var host, out var point, out int side)) return;
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

        internal static FixtureAttachment Place(FixtureChoice choice, Transform host, Vector3 point, int side)
        {
            var root = new GameObject(choice.label);
            var fixture = root.AddComponent<FixtureAttachment>();
            fixture.mount = choice.mount;
            Undo.RegisterCreatedObjectUndo(root, "Place fixture");
            Mount(fixture, host, point, side);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(choice.prefab, root.transform);
            Undo.RegisterCreatedObjectUndo(model, "Place fixture");
            ApplyPreset(choice, fixture, model);
            return fixture;
        }

        internal static bool Refit(FixtureAttachment fixture)
        {
            foreach (Transform child in fixture.transform)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
                var direct = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                var matches = Choices.Where(c => c.mount == fixture.mount && (c.prefab == direct || c.prefab == source)).ToArray();
                var choice = matches.FirstOrDefault(c => c.label == fixture.name);
                if (choice == null && matches.Length == 1) choice = matches[0];
                if (choice == null) continue;
                ApplyPreset(choice, fixture, child.gameObject);
                return true;
            }
            return false;
        }

        static void ApplyPreset(FixtureChoice choice, FixtureAttachment fixture, GameObject model)
        {
            var root = fixture.transform;
            var t = model.transform;
            Undo.RecordObject(t, "Fit fixture model");
            t.localRotation = Quaternion.Euler(choice.modelRotation);
            t.localPosition = Vector3.zero;
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
            var center = bounds.center;
            if (choice.mount == FixtureMount.Wall) center.z = bounds.min.z;
            else center.y = choice.mount == FixtureMount.Floor ? bounds.min.y : bounds.max.y;
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
                    light.type = LightType.Point;
                    light.shadows = LightShadows.None;
                    Undo.RegisterCreatedObjectUndo(emitter, "Add fixture light");
                }
                Undo.RecordObjects(new Object[] {light, light.transform}, "Fit fixture light");
                light.transform.localPosition = choice.mount == FixtureMount.Wall ? Vector3.forward * (bounds.size.z + .04f)
                    : Vector3.up * ((choice.mount == FixtureMount.Floor ? 1 : -1) * (bounds.size.y + .04f));
                light.range = choice.lightRange;
                light.intensity = choice.lightIntensity;
                PrefabUtility.RecordPrefabInstancePropertyModifications(light.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
        }

        internal static void Mount(FixtureAttachment fixture, Transform host, Vector3 point, int side)
        {
            var t = fixture.transform;
            if (t.parent != host) Undo.SetTransformParent(t, host, "Attach fixture");
            Undo.RecordObjects(new Object[]{fixture,t}, "Attach fixture");
            fixture.wallSide = side;
            var local = host.InverseTransformPoint(point);
            fixture.surfaceOffset = fixture.mount == FixtureMount.Wall && host.TryGetComponent<HouseWall>(out var wall)
                ? local.z - side * wall.Thickness * .5f : 0;
            t.SetLocalPositionAndRotation(fixture.MountedPosition(local),
                fixture.mount == FixtureMount.Wall && side < 0 ? Quaternion.Euler(0,180,0) : Quaternion.identity);
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

    [CustomEditor(typeof(FixtureAttachment))]
    internal sealed class FixtureInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var fixture = (FixtureAttachment)target;
            EditorGUILayout.LabelField("Mount",fixture.mount.ToString());
            if (GUILayout.Button("Move fixture")) FixtureTool.BeginMove(fixture);
            if (GUILayout.Button(new GUIContent("Reapply fixture preset", "Refit the model and restore the generated light's position, range and intensity.")) && !FixtureTool.Refit(fixture))
                EditorWindow.focusedWindow?.ShowNotification(new GUIContent("Matching fixture preset not found."));
        }

        void OnSceneGUI()
        {
            var fixture = (FixtureAttachment)target;
            var floor = fixture.GetComponentInParent<HouseFloor>();
            if (!floor || Tools.current != Tool.Move || Tools.viewToolActive || Event.current.alt) return;
            if (Event.current.rawType == EventType.MouseUp) FixtureTool.Constrain(fixture);
            using (new Handles.DrawingScope(Color.green))
            {
                var point = fixture.transform.position;
                EditorGUI.BeginChangeCheck();
                Handles.FreeMoveHandle(point, HandleUtility.GetHandleSize(point)*.075f, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck() && FixtureTool.Target(floor, fixture.mount,
                    HandleUtility.GUIPointToWorldRay(Event.current.mousePosition), out var host, out var hit, out int side))
                    FixtureTool.Mount(fixture, host, hit, side);
            }
        }
    }
}
