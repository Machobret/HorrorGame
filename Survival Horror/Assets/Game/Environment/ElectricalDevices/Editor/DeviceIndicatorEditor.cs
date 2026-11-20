using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Environment.Editor
{
    [CustomEditor(typeof(DeviceIndicator)), CanEditMultipleObjects]
    sealed class DeviceIndicatorEditor : UnityEditor.Editor
    {
        static readonly GUIContent PostMountLabel = new GUIContent("Post Mount", "Enable the freestanding post and its collider.");
        SerializedProperty antennaActive, mountActive;

        void OnEnable()
        {
            var devices = targets.Cast<DeviceIndicator>().ToArray();
            antennaActive = AttachmentToggle.Bind(devices.Select(device => device.Antenna).ToArray());
            mountActive = AttachmentToggle.Bind(devices.Select(device => device.PostMount).ToArray());
        }

        void OnDisable()
        {
            antennaActive?.serializedObject.Dispose();
            mountActive?.serializedObject.Dispose();
        }

        public override void OnInspectorGUI()
        {
            AttachmentToggle.Draw(antennaActive, AttachmentToggle.AntennaLabel);
            AttachmentToggle.Draw(mountActive, PostMountLabel);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(ElectricalBoxIndicator)), CanEditMultipleObjects]
    sealed class ElectricalBoxIndicatorEditor : UnityEditor.Editor
    {
        SerializedProperty antennaActive;

        void OnEnable() => antennaActive =
            AttachmentToggle.Bind(targets.Cast<ElectricalBoxIndicator>().Select(box => box.Antenna).ToArray());

        void OnDisable() => antennaActive?.serializedObject.Dispose();

        public override void OnInspectorGUI()
        {
            AttachmentToggle.Draw(antennaActive, AttachmentToggle.AntennaLabel);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }

    static class AttachmentToggle
    {
        internal static readonly GUIContent AntennaLabel = new GUIContent("Antenna", "Enable the antenna and its tip light.");

        internal static SerializedProperty Bind(GameObject[] children)
        {
            if (children.Length == 0 || children.Any(child => !child)) return null;
            return new SerializedObject(children).FindProperty("m_IsActive");
        }

        internal static void Draw(SerializedProperty active, GUIContent label)
        {
            if (active == null) return;
            var children = active.serializedObject;
            children.Update();
            EditorGUILayout.PropertyField(active, label);
            children.ApplyModifiedProperties();
        }
    }
}
