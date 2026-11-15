using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    static class FamilyAssetMenus
    {
        [MenuItem("Assets/Create/House Toolkit/Plain Wall Family")]
        static void Plain()=>NewFamily(false);
        [MenuItem("Assets/Create/House Toolkit/Complex Wall Family")]
        static void Complex()=>NewFamily(true);
        static void NewFamily(bool complex)
        {
            var f=ScriptableObject.CreateInstance<WallFamily>();f.complex=complex;
            ProjectWindowUtil.CreateAsset(f,complex?"Complex.asset":"Plain.asset");
        }
        [MenuItem("Assets/Create/House Toolkit/Wall Segment")]
        static void Segment()
        {
            var selection=Selection.activeObject;
            string basePath=AssetDatabase.GetAssetPath(selection);
            string folder=AssetDatabase.IsValidFolder(basePath)?basePath:System.IO.Path.GetDirectoryName(basePath);
            if(string.IsNullOrEmpty(folder)||!folder.StartsWith("Assets"))folder="Assets";
            Mesh mesh=selection as Mesh;
            if(!mesh && selection is GameObject model && AssetImporter.GetAtPath(basePath) is ModelImporter)
            {
                var filters=model.GetComponentsInChildren<MeshFilter>();
                if(filters.Length==1)mesh=filters[0].sharedMesh;
            }
            string name=mesh?mesh.name+" Segment":"Wall Segment";
            var go=new GameObject(name);
            try
            {
                go.AddComponent<WallSegment>().mesh=mesh;
                string path=AssetDatabase.GenerateUniqueAssetPath(folder.Replace('\\','/')+"/"+name+".prefab");
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,path);
                ProjectWindowUtil.ShowCreatedAsset(prefab);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
    [CustomEditor(typeof(WallSegment))]
    class WallSegmentInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var mesh=serializedObject.FindProperty("mesh");EditorGUILayout.PropertyField(mesh);
            if(mesh.objectReferenceValue is Mesh m)
            {
                EditorGUILayout.LabelField("Width × height × depth (m)",m.bounds.size.ToString("F3"));
                if(!m.isReadable || m.subMeshCount>2)EditorGUILayout.HelpBox("Enable Read/Write in the model import settings. Use one frame slot and optionally a second glass slot.",MessageType.Warning);
            }
            else if(((WallSegment)target).parts.Length>0)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("legacySize"),new GUIContent("Size"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("parts"),true);
            }
            else EditorGUILayout.HelpBox("Assign the mesh from an imported model.",MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("aperture"),new GUIContent("Opening rectangle (m)"));
            if(serializedObject.FindProperty("aperture").rectValue.size!=Vector2.zero)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("kind"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
