using UnityEngine;
using UnityEditor;
using HorrorGame.Chapter1;
using System.IO;

namespace HorrorEngine
{
    [CustomEditor(typeof(MapController))]
    public class MapControllerEditor : Editor
    {
        [SerializeField] private Texture2D m_MapIcon;

        internal static PowerPanelController FindCircuitController(MapController map)
        {
            if (!map || !map.Data || !map.gameObject.scene.IsValid()) return null;
            PowerPanelController unassigned = null;
            foreach (var root in map.gameObject.scene.GetRootGameObjects())
            foreach (var panel in root.GetComponentsInChildren<PowerPanelController>(true))
            {
                if (panel.ResolveSourceMap() == map) return panel;
                foreach (var dot in panel.GetComponentsInChildren<PowerCircuitPanelElement>(true))
                    if (dot.GetMapData() == map.Data) return panel;
                if (!panel.ResolveSourceMap()) unassigned = panel;
            }
            return unassigned;
        }

        internal static PowerPanelController EnsureCircuitController(MapController map)
        {
            var panel = FindCircuitController(map);
            if (panel && panel.Data) return panel;
            string mapPath = AssetDatabase.GetAssetPath(map.Data);
            if (string.IsNullOrEmpty(mapPath)) return null;
            if (!panel)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Levels/Chapter1/Prefabs/PowerPanelController.prefab");
                if (!prefab) return null;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, map.transform);
                Undo.RegisterCreatedObjectUndo(instance, "Create Power Panel Controller");
                panel = instance.GetComponent<PowerPanelController>();
            }
            Undo.RecordObject(panel, "Configure Power Panel Controller");
            var layout = ScriptableObject.CreateInstance<PowerPanelLayout>();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.ChangeExtension(mapPath, null) + "_Circuits.asset");
            AssetDatabase.CreateAsset(layout, path);
            panel.Data = layout;
            panel.SourceMap = map;
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            return panel;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Separator();
            if (GUILayout.Button(new GUIContent("Open Editor", m_MapIcon), GUILayout.Height(30)))
            {
                MappingEditorWindow.Init(target as MapController);
            }
            var map = (MapController)target;
            using (new EditorGUI.DisabledScope(!map.Data || EditorApplication.isPlayingOrWillChangePlaymode ||
                       EditorUtility.IsPersistent(map) || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(map.gameObject) != null))
            {
                if (GUILayout.Button("Edit Circuits"))
                {
                    if (EnsureCircuitController(map)) MappingEditorWindow.Init(map);
                }
                if (GUILayout.Button("Add Circuit"))
                    MappingEditorWindow.AddCircuitFromInspector(map);
            }
        }
    }
}
