using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseToolkit.Editor
{
    public sealed class ScatterBrush : ScriptableObject
    {
        [SerializeField] ScatterPreset preset;
        [SerializeField] float radius = 1.25f, density = 1.5f;
        [SerializeField] ScatterPreset.Pieces pieces;
        ScatterPreset[] presets;
        string[] names;
        bool painting, erase;
        internal bool Active => painting;
        readonly HashSet<(ScatterPreset, Mesh, Material)> excluded = new();
        ScatterPreset.Entry[] enabledEntries;
        readonly Dictionary<(Mesh, Material), Texture2D> swatches = new();
        PreviewRenderUtility previewRenderer;
        static readonly string[] modes = { "Paint", "Erase" };
        int group = -1, pointer, seed = 4517;
        Vector3 last;
        readonly List<ScatterCell.Piece> preview = new();
        readonly HashSet<ScatterCell> recorded = new();
        readonly Dictionary<Vector3Int, ScatterCell> cells = new();
        Scene strokeScene;
        (Vector3 point, Vector3 normal, int scene, ScatterPreset preset, ScatterPreset.Pieces pieces, float radius, float density, int seed) previewKey;

        [MenuItem("GameObject/Environment/Scatter", false, 18)]
        internal static void Open()
        {
            WireTool.Leave();
            if (HouseScene.Floor) HouseScene.Leave();
            if (RoadScene.Active) RoadScene.Leave();
            if (EnvironmentPlacementTool.Pending) EnvironmentPlacementTool.Cancel();
            HouseProperties.OpenScatter();
        }
        internal void Activate()
        {
            painting = true;
            SceneView.RepaintAll();
        }
        internal void Deactivate()
        {
            Finish();
            painting = false;
            HouseProperties.RepaintOpen();
            SceneView.RepaintAll();
        }

        void OnEnable()
        {
            Refresh();
            SceneView.duringSceneGui += SceneGUI;
            EditorApplication.projectChanged += Refresh;
        }
        void OnDisable()
        {
            Finish();
            ClearSwatches();
            SceneView.duringSceneGui -= SceneGUI;
            EditorApplication.projectChanged -= Refresh;
        }
        void Refresh()
        {
            presets = AssetDatabase.FindAssets("t:ScatterPreset").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScatterPreset>).Where(p => p).OrderBy(p => p.name).ToArray();
            names = presets.Select(p => ObjectNames.NicifyVariableName(p.name)).ToArray();
            if (!preset) preset = presets.FirstOrDefault(p => p.name == "Paper Trail") ?? presets.FirstOrDefault();
            ClearSwatches();
            UpdateChoices();
            HouseProperties.RepaintOpen();
        }
        void ClearSwatches()
        {
            foreach (var image in swatches.Values) if (image) DestroyImmediate(image);
            swatches.Clear();
            previewRenderer?.Cleanup();
            previewRenderer = null;
        }
        void UpdateChoices()
        {
            enabledEntries = preset ? preset.entries.Where(e => e != null && e.Available(pieces)).Select(e =>
                new ScatterPreset.Entry { weight = e.weight, material = e.material, solid = e.solid,
                    variants = e.variants.Where(m => m && m.isReadable && m.subMeshCount == 1 &&
                        !excluded.Contains((preset, m, e.material))).ToArray() }).Where(e => e.variants.Length > 0).ToArray()
                : Array.Empty<ScatterPreset.Entry>();
            previewKey = default;
            preview.Clear();
            SceneView.RepaintAll();
        }
        internal Texture2D Swatch(Mesh mesh, Material material)
        {
            var key = (mesh, material);
            if (swatches.TryGetValue(key, out var image)) return image;
            previewRenderer ??= new PreviewRenderUtility();
            var camera = previewRenderer.camera;
            var bounds = mesh.bounds;
            float extent = Mathf.Max(.001f, bounds.extents.magnitude);
            var direction = new Vector3(.15f, 1f, -.42f).normalized;
            camera.transform.SetPositionAndRotation(bounds.center + direction * extent * 3, Quaternion.LookRotation(-direction));
            camera.orthographic = true;
            camera.orthographicSize = extent * 1.05f;
            camera.nearClipPlane = extent * .1f;
            camera.farClipPlane = extent * 6;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f,.18f,.18f);
            previewRenderer.ambientColor = Color.gray;
            previewRenderer.lights[0].intensity = 1.2f;
            previewRenderer.lights[0].transform.rotation = Quaternion.Euler(55,-35,0);
            previewRenderer.BeginStaticPreview(new Rect(0,0,96,96));
            previewRenderer.DrawMesh(mesh, Matrix4x4.identity, material, 0);
            previewRenderer.Render(true);
            image = previewRenderer.EndStaticPreview();
            image.hideFlags = HideFlags.HideAndDontSave;
            swatches.Add(key, image);
            return image;
        }
        void DrawSwatches()
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 32) / 65));
            int i = 0;
            Rect row = default;
            foreach (var entry in preset.entries)
            {
                if (entry == null || !entry.Available(pieces)) continue;
                foreach (var mesh in entry.variants)
                {
                    if (!mesh || !mesh.isReadable || mesh.subMeshCount != 1) continue;
                    if (i % columns == 0) row = EditorGUILayout.GetControlRect(false, 60);
                    var rect = new Rect(row.x + i++ % columns * 65, row.y, 60, 60);
                    var key = (preset, mesh, entry.material);
                    bool on = !excluded.Contains(key);
                    var evt = Event.current;
                    if (rect.Contains(evt.mousePosition) && evt.type == EventType.MouseDown && evt.button == 0 && evt.clickCount == 2)
                    {
                        EditorGUIUtility.PingObject(mesh);
                        // The first click toggled the tile. Restore it when revealing the asset.
                        if (on) excluded.Add(key); else excluded.Remove(key);
                        UpdateChoices();
                        evt.Use();
                    }
                    if (GUI.Button(rect, new GUIContent(string.Empty, entry.label + " / " + mesh.name)))
                    {
                        if (on) excluded.Add(key); else excluded.Remove(key);
                        UpdateChoices();
                    }
                    if (evt.type != EventType.Repaint) continue;
                    GUI.DrawTexture(new Rect(rect.x+2,rect.y+2,56,56), Swatch(mesh,entry.material));
                    if (!on) EditorGUI.DrawRect(rect, new Color(0,0,0,.62f));
                    EditorStyles.toggle.Draw(new Rect(rect.x+3,rect.y+2,16,16), GUIContent.none, false, false, on, false);
                }
            }
        }
        internal void DrawProperties()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Scatter", EditorStyles.boldLabel);
                if (GUILayout.Button("Done", GUILayout.Width(52))) { Deactivate(); return; }
            }
            if (presets.Length == 0) { EditorGUILayout.HelpBox("Create a Scatter Preset to paint.", MessageType.Info); return; }
            EditorGUI.BeginChangeCheck();
            preset = presets[EditorGUILayout.Popup("Preset", Mathf.Max(0, Array.IndexOf(presets, preset)), names)];
            radius = EditorGUILayout.Slider("Radius (m)", radius, .2f, 4f);
            density = EditorGUILayout.Slider("Density (pieces/m²)", density, .25f, 6f);
            pieces = (ScatterPreset.Pieces)EditorGUILayout.EnumPopup("Pieces", pieces);
            if (EditorGUI.EndChangeCheck()) UpdateChoices();
            erase = GUILayout.Toolbar(erase ? 1 : 0, modes) == 1;
            DrawSwatches();
            EditorGUILayout.LabelField("Shift erases enabled pieces. Esc finishes.", EditorStyles.wordWrappedMiniLabel);
            if (GUI.changed) SceneView.RepaintAll();
        }

        internal static Vector3Int Key(Vector3 p) => Vector3Int.FloorToInt(p / ScatterCell.Width);
        static bool Ground(Scene scene, Ray ray, float distance, out RaycastHit hit)
        {
            var physics = scene.GetPhysicsScene();
            if (!physics.Raycast(ray.origin, ray.direction, out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            bool Visible(GameObject go) => go.scene == scene && !SceneVisibilityManager.instance.IsHidden(go) &&
                !SceneVisibilityManager.instance.IsPickingDisabled(go);
            if (Visible(hit.collider.gameObject)) return hit.normal.y >= .65f;
            hit = default;
            float best = float.PositiveInfinity;
            foreach (var candidate in Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var go = candidate.collider.gameObject;
                if (candidate.distance >= best || !Visible(go)) continue;
                hit = candidate;
                best = candidate.distance;
            }
            return hit.collider && hit.normal.y >= .65f;
        }

        internal static void Sample(Scene scene, Vector3 center, Vector3 normal, ScatterPreset preset,
            ScatterPreset.Pieces filter, float radius, float density, int seed, List<ScatterCell.Piece> result, ScatterPreset.Entry[] choices = null)
        {
            result.Clear();
            if (choices != null && choices.Length == 0) return;
            var rng = new System.Random(seed);
            float Next() => (float)rng.NextDouble();
            float expected = Mathf.PI * radius * radius * density;
            int count = Mathf.FloorToInt(expected + Next());
            var basis = Quaternion.FromToRotation(Vector3.up, normal);
            for (int i = 0; i < count; i++)
            {
                float angle = Next() * Mathf.PI * 2, distance = Mathf.Sqrt(Next()) * radius;
                var probe = center + basis * new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                if (!Ground(scene, new Ray(probe + normal * .16f, -normal), .32f, out var hit)) continue;
                var entry = preset.Pick(rng, filter, choices);
                if (entry == null) break;
                var mesh = entry.variants[rng.Next(entry.variants.Length)];
                if (!mesh || !mesh.isReadable || mesh.subMeshCount != 1) continue;
                var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.AngleAxis(Next() * 360, Vector3.up);
                float scale = Mathf.Lerp(.86f, 1.14f, Next());
                if (!entry.solid && !Supported(scene, hit, rotation, mesh.bounds, scale)) continue;
                result.Add(new ScatterCell.Piece { mesh = mesh, material = entry.material, preset = preset, solid = entry.solid,
                    position = hit.point + hit.normal * (.002f + Next() * .0015f), rotation = rotation, scale = scale });
            }
        }
        static bool Supported(Scene scene, RaycastHit center, Quaternion rotation, Bounds bounds, float scale)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, 0, (i & 2) == 0 ? bounds.min.z : bounds.max.z);
                var point = center.point + rotation * (corner * scale);
                if (!Ground(scene, new Ray(point + center.normal * .025f, -center.normal), .05f, out _)) return false;
            }
            return true;
        }

        void SceneGUI(SceneView view)
        {
            if (!painting || !preset || HouseScene.Floor || RoadScene.Active || EnvironmentPlacementTool.Pending || EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            { Deactivate(); e.Use(); view.Repaint(); return; }
            if (e.rawType == EventType.MouseUp && e.button == 0 && group >= 0)
            { Finish(); e.Use(); return; }
            if (e.alt || Tools.viewToolActive || e.button == 1 && e.isMouse) return;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) { HandleUtility.AddDefaultControl(control); return; }
            if (e.type == EventType.MouseMove) { view.Repaint(); return; }
            bool down = e.type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == control;
            bool drag = e.type == EventType.MouseDrag && group >= 0 && GUIUtility.hotControl == pointer;
            if (!down && !drag && e.type != EventType.Repaint) return;
            if (enabledEntries == null || enabledEntries.Length == 0) return;
            var scene = group >= 0 ? strokeScene : SceneManager.GetActiveScene();
            if (!Ground(scene, HandleUtility.GUIPointToWorldRay(e.mousePosition), Mathf.Infinity, out var hit)) return;
            bool removing = erase || e.shift;
            if (drag && Vector3.Distance(hit.point, last) < radius * 1.1f) return;
            var key = (hit.point, hit.normal, scene.handle, preset, pieces, radius, density, seed);
            if (!removing && (down || drag || !previewKey.Equals(key)))
            {
                Sample(scene, hit.point, hit.normal, preset, pieces, radius, density, seed, preview, enabledEntries);
                previewKey = key;
            }
            if (e.type == EventType.Repaint)
            {
                using (new Handles.DrawingScope(removing ? new Color(1,.45f,.25f) : new Color(.6f,.88f,.52f)))
                {
                    Handles.DrawWireDisc(hit.point, hit.normal, radius);
                    if (!removing)
                        foreach (var p in preview)
                            using (new Handles.DrawingScope(Matrix4x4.TRS(p.position,p.rotation,Vector3.one*p.scale)))
                                Handles.DrawWireCube(p.mesh.bounds.center,p.mesh.bounds.size);
                }
                return;
            }
            if (down)
            {
                Undo.IncrementCurrentGroup();
                group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(removing ? "Erase scatter" : "Paint scatter");
                pointer = GUIUtility.hotControl = control;
                strokeScene = scene;
                cells.Clear();
                foreach (var cell in FindObjectsByType<ScatterCell>(FindObjectsSortMode.None))
                    if (cell.gameObject.scene == scene) cells[Key(cell.transform.position + Vector3.one * .01f)] = cell;
            }
            if (removing) Erase(hit.point); else Paint();
            last = hit.point;
            seed = unchecked(seed * 1664525 + 1013904223);
            e.Use(); view.Repaint();
        }
        void Record(ScatterCell cell)
        {
            if (recorded.Add(cell)) Undo.RegisterCompleteObjectUndo(cell, "Edit scatter");
        }
        void Paint()
        {
            var touched = new HashSet<ScatterCell>();
            foreach (var world in preview)
            {
                var key = Key(world.position);
                if (!cells.TryGetValue(key, out var cell) || !cell)
                {
                    var go = new GameObject($"Scatter {key.x}, {key.y}, {key.z}");
                    SceneManager.MoveGameObjectToScene(go, strokeScene);
                    go.transform.position = (Vector3)key * ScatterCell.Width;
                    cell = go.AddComponent<ScatterCell>();
                    Undo.RegisterCreatedObjectUndo(go, "Paint scatter");
                    EnvironmentHierarchy.Group(go);
                    cells[key] = cell;
                }
                Record(cell);
                var p = world;
                p.position = cell.transform.InverseTransformPoint(world.position);
                p.rotation = Quaternion.Inverse(cell.transform.rotation) * world.rotation;
                cell.pieces.Add(p);
                touched.Add(cell);
            }
            foreach (var cell in touched) cell.Rebuild();
        }
        void Erase(Vector3 center)
        {
            var min = Key(center - Vector3.one * radius);
            var max = Key(center + Vector3.one * radius);
            for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            {
                if (!cells.TryGetValue(new Vector3Int(x, y, z), out var cell) || !cell) continue;
                bool Match(ScatterCell.Piece p) => p.preset == preset && !excluded.Contains((preset, p.mesh, p.material)) &&
                    (pieces == ScatterPreset.Pieces.All || p.solid == (pieces == ScatterPreset.Pieces.Solid)) &&
                    (cell.transform.TransformPoint(p.position) - center).sqrMagnitude <= radius * radius;
                if (!cell.pieces.Exists(Match)) continue;
                Record(cell);
                cell.pieces.RemoveAll(Match);
                if (cell.pieces.Count == 0) Undo.DestroyObjectImmediate(cell.gameObject);
                else cell.Rebuild();
            }
        }
        void Finish()
        {
            if (group >= 0) Undo.CollapseUndoOperations(group);
            if (pointer != 0 && GUIUtility.hotControl == pointer) GUIUtility.hotControl = 0;
            group = -1; pointer = 0; recorded.Clear(); cells.Clear();
        }
    }
}
