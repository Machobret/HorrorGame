using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseToolkit.Editor
{
    [InitializeOnLoad]
    public static class WallDecalTool
    {
        const string DragKey = "HouseToolkit.WallDecal";
        static WallDecalAtlas[] atlases;
        static readonly HashSet<WallDecalAtlas> collapsed = new();
        static WallDecalAtlas pressedAtlas;
        static int pressedIndex;
        static Vector2 pressedAt;

        static WallDecalTool()
        {
            EditorApplication.projectChanged += () => { atlases = null; };
            DragAndDrop.AddDropHandlerV2(Drop);
        }

        static void LoadAtlases()
        {
            if (atlases != null) return;
            atlases = AssetDatabase.FindAssets("t:WallDecalAtlas").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WallDecalAtlas>).Where(a => a && a.material)
                .OrderBy(a => a.name, StringComparer.Ordinal).ToArray();
        }

        public static void DrawLibrary()
        {
            LoadAtlases();
            EditorGUILayout.LabelField("Drag a decal into the Scene.", EditorStyles.miniLabel);
            foreach (var atlas in atlases)
            {
                bool open = EditorGUILayout.Foldout(!collapsed.Contains(atlas), ObjectNames.NicifyVariableName(atlas.name), true);
                if (!open) { collapsed.Add(atlas); continue; }
                collapsed.Remove(atlas);
                DrawSwatches(WallDecalLayout.Choices(new[] { atlas }, null), -1, true);
            }
        }

        internal static int AtlasIndex(WallDecalAtlas atlas)
        {
            LoadAtlases();
            return Mathf.Max(0, Array.IndexOf(atlases, atlas));
        }

        internal static bool DrawAtlas(ref int index)
        {
            LoadAtlases();
            if (atlases.Length == 0) return false;
            index = Mathf.Clamp(index, 0, atlases.Length - 1);
            int next = EditorGUILayout.Popup("Atlas", index, atlases.Select(a => a.name).ToArray());
            bool changed = next != index;
            index = next;
            return changed;
        }

        internal static List<WallDecalLayout.Choice> ChoicesForAtlas(int index)
        {
            LoadAtlases();
            return WallDecalLayout.Choices(atlases.Length == 0 ? null : new[] { atlases[Mathf.Clamp(index, 0, atlases.Length - 1)] }, null);
        }

        internal static int DrawSwatches(IReadOnlyList<WallDecalLayout.Choice> choices, int selected, bool draggable = false)
        {
            if (choices.Count == 0) return -1;
            const float gap = 3, height = 52;
            // Leave room for the surrounding inspector and its scroll bar.
            int columns = Mathf.Clamp(Mathf.FloorToInt((EditorGUIUtility.currentViewWidth - 30) / 68), 1, Mathf.Min(8, choices.Count));
            int rows = (choices.Count + columns - 1) / columns;
            var grid = EditorGUILayout.GetControlRect(false, rows * (height + gap) - gap);
            float width = Mathf.Min(108, (grid.width - gap * (columns - 1)) / columns);
            int picked = -1;
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var tile = new Rect(grid.x + i % columns * (width + gap), grid.y + i / columns * (height + gap), width, height);
                if (draggable)
                {
                    var evt = Event.current;
                    int control = GUIUtility.GetControlID(FocusType.Passive, tile);
                    if (tile.Contains(evt.mousePosition) && evt.type == EventType.MouseDown && evt.button == 0)
                    {
                        pressedAtlas = choice.atlas;
                        pressedIndex = i;
                        pressedAt = evt.mousePosition;
                        GUIUtility.hotControl = control;
                        if (evt.clickCount == 2) EditorGUIUtility.PingObject(choice.atlas);
                        evt.Use();
                    }
                    if (pressedAtlas == choice.atlas && pressedIndex == i)
                    {
                        if (evt.rawType == EventType.MouseUp)
                        {
                            if (GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
                            pressedAtlas = null;
                        }
                        else if (evt.type == EventType.MouseDrag && (evt.mousePosition - pressedAt).sqrMagnitude >= 16)
                        {
                            GUIUtility.hotControl = 0;
                            DragAndDrop.PrepareStartDrag();
                            DragAndDrop.SetGenericData(DragKey, choice);
                            DragAndDrop.StartDrag(choice.poster.label);
                            pressedAtlas = null;
                            evt.Use();
                        }
                    }
                    GUI.Box(tile, new GUIContent(string.Empty, choice.poster.label), EditorStyles.helpBox);
                }
                else if (GUI.Button(tile, new GUIContent(string.Empty, choice.poster.label), EditorStyles.miniButton)) picked = i;
                if (Event.current.type != EventType.Repaint) continue;
                if (i == selected)
                {
                    EditorGUI.DrawRect(tile, new Color(.32f, .65f, 1f));
                    EditorGUI.DrawRect(new Rect(tile.x + 2, tile.y + 2, tile.width - 4, tile.height - 4), new Color(.13f, .19f, .27f));
                }
                var texture = choice.atlas.material.mainTexture;
                if (texture)
                {
                    float ratio = choice.poster.size.x / choice.poster.size.y;
                    float imageWidth = Mathf.Min(Mathf.Max(1, width - 8), (height - 8) * ratio);
                    float imageHeight = imageWidth / ratio;
                    GUI.DrawTextureWithTexCoords(new Rect(tile.center.x - imageWidth * .5f, tile.center.y - imageHeight * .5f,
                        imageWidth, imageHeight), texture, choice.poster.uv, false);
                }
                else GUI.Label(tile, choice.poster.label, EditorStyles.centeredGreyMiniLabel);
            }
            return picked;
        }

        static DragAndDropVisualMode Drop(Object target, Vector3 position, Vector2 viewport, Transform parent, bool perform)
        {
            if (!(DragAndDrop.GetGenericData(DragKey) is WallDecalLayout.Choice choice)) return DragAndDropVisualMode.None;
            var view = SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
            if (!view || !view.camera) return DragAndDropVisualMode.Rejected;
            if (perform)
            {
                var normal = -view.camera.transform.forward;
                if (Event.current != null && HandleUtility.PlaceObject(Event.current.mousePosition, out var hit, out var face) &&
                    face.sqrMagnitude > .001f)
                {
                    normal = face.normalized;
                    position = hit + normal * .004f;
                }
                var scene = target is GameObject go && go.scene.IsValid() ? go.scene :
                    parent ? parent.gameObject.scene : UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (RoadScene.Active) RoadScene.Leave();
                if (HouseScene.Floor) HouseScene.Leave();
                var up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < .99f ? Vector3.up : Vector3.forward;
                var decal = MeshDecal.Create(scene, choice.atlas, choice.poster, position, normal, up, Vector2.one);
                Undo.RegisterCreatedObjectUndo(decal.gameObject, "Place decal");
                EnvironmentHierarchy.Group(decal.gameObject);
                Selection.activeGameObject = decal.gameObject;
                SceneView.RepaintAll();
            }
            return DragAndDropVisualMode.Copy;
        }
    }
}
