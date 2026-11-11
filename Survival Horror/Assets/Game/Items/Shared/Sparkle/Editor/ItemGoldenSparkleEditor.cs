using UnityEditor;
using UnityEngine;

namespace HorrorGame.Items.Editor
{
    [CustomEditor(typeof(ItemGoldenSparkle))]
    public sealed class ItemGoldenSparkleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var effect = (ItemGoldenSparkle)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(Application.isPlaying ? effect.VisibilityStatus :
                "The sparkle is controlled by gameplay, so the Particle System Play button does not emit it. Enter Play mode and use Preview below to check visibility.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
                if (GUILayout.Button("Preview Sparkle for 5 Seconds")) effect.Preview();
            if (Application.isPlaying) Repaint();
        }
    }
}
