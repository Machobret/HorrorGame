using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    [CustomEditor(typeof(CameraPOV))]
    public class CameraPOVEditor : Editor
    {
        private SerializedProperty m_Priority;
        private SerializedProperty m_PositionFollow;
        private SerializedProperty m_RotationFollow;
        private SerializedProperty m_ThirdPersonFollow;

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_Priority = serializedObject.FindProperty("Priority");
            m_PositionFollow = serializedObject.FindProperty("PositionFollow");
            m_RotationFollow = serializedObject.FindProperty("RotationFollow");
            m_ThirdPersonFollow = serializedObject.FindProperty("ThirdPersonFollow");
        }

        // --------------------------------------------------------------------

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Priority);
            DrawFollowSettings("Player Follow (Position)", m_PositionFollow, true);
            DrawFollowSettings("Player Follow (Rotation)", m_RotationFollow, false);
            DrawThirdPersonFollowSettings(m_ThirdPersonFollow);

            serializedObject.ApplyModifiedProperties();
        }

        // --------------------------------------------------------------------

        private static void DrawFollowSettings(string title, SerializedProperty settings, bool allowMaintainDistance)
        {
            SerializedProperty enabled = settings.FindPropertyRelative("Enabled");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            enabled.boolValue = EditorGUILayout.ToggleLeft(title, enabled.boolValue, EditorStyles.boldLabel);

            if (enabled.boolValue)
            {
                EditorGUI.indentLevel++;
                SerializedProperty followMode = settings.FindPropertyRelative("FollowMode");
                if (allowMaintainDistance)
                    EditorGUILayout.PropertyField(followMode);
                else
                    followMode.enumValueIndex = EditorGUILayout.IntPopup(
                        "Follow Mode",
                        Mathf.Min(followMode.enumValueIndex, 1),
                        new[] { "Keep Player Centered", "Keep Player Inside Border" },
                        new[] { 0, 1 });

                if (followMode.enumValueIndex == (int)CameraPOV.PlayerFollowMode.MaintainDistance)
                {
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("MaintainDistance"));
                    EditorGUILayout.PropertyField(
                        settings.FindPropertyRelative("KeepOriginalPositionMax"),
                        new GUIContent("Original Max"));
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("FollowHorizontal"));
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("FollowVertical"));
                }
                else
                {
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("BorderOffset"));
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("CenterFollowDistance"));
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("FollowHorizontal"));
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative("FollowVertical"));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        // --------------------------------------------------------------------

        private static void DrawThirdPersonFollowSettings(SerializedProperty settings)
        {
            SerializedProperty enabled = settings.FindPropertyRelative("Enabled");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            enabled.boolValue = EditorGUILayout.ToggleLeft("Player Follow (Third Person)", enabled.boolValue, EditorStyles.boldLabel);

            if (enabled.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("Distance"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("Rotation"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("Pitch"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
