using UnityEditor;

namespace HorrorEngine
{
    [CustomEditor(typeof(CameraPOV))]
    public class CameraPOVEditor : Editor
    {
        private SerializedProperty m_Priority;
        private SerializedProperty m_PositionFollow;
        private SerializedProperty m_RotationFollow;

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            m_Priority = serializedObject.FindProperty("Priority");
            m_PositionFollow = serializedObject.FindProperty("PositionFollow");
            m_RotationFollow = serializedObject.FindProperty("RotationFollow");
        }

        // --------------------------------------------------------------------

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Priority);
            DrawFollowSettings("Player Follow (Position)", m_PositionFollow);
            DrawFollowSettings("Player Follow (Rotation)", m_RotationFollow);

            serializedObject.ApplyModifiedProperties();
        }

        // --------------------------------------------------------------------

        private static void DrawFollowSettings(string title, SerializedProperty settings)
        {
            SerializedProperty enabled = settings.FindPropertyRelative("Enabled");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            enabled.boolValue = EditorGUILayout.ToggleLeft(title, enabled.boolValue, EditorStyles.boldLabel);

            if (enabled.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("FollowMode"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("BorderOffset"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("CenterFollowDistance"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("FollowHorizontal"));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("FollowVertical"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
