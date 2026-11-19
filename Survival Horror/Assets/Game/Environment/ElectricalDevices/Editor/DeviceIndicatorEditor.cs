using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Environment.Editor
{
    [CustomEditor(typeof(DeviceIndicator)), CanEditMultipleObjects]
    sealed class DeviceIndicatorEditor : UnityEditor.Editor
    {
        static readonly GUIContent PostMountLabel = new GUIContent("Post Mount", "Enable the freestanding post and its collider.");
        SerializedObject mountObjects;
        SerializedProperty mountActive;

        void OnEnable()
        {
            var mounts = targets.Cast<DeviceIndicator>().Select(device => device.PostMount).ToArray();
            if (mounts.Length == 0 || mounts.Any(mount => !mount))
                return;

            mountObjects = new SerializedObject(mounts);
            mountActive = mountObjects.FindProperty("m_IsActive");
        }

        void OnDisable()
        {
            mountObjects?.Dispose();
            mountObjects = null;
            mountActive = null;
        }

        public override void OnInspectorGUI()
        {
            if (mountActive != null)
            {
                mountObjects.Update();
                EditorGUILayout.PropertyField(mountActive, PostMountLabel);
                mountObjects.ApplyModifiedProperties();
                EditorGUILayout.Space();
            }

            DrawDefaultInspector();
        }
    }
}
