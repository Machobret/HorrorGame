using UnityEditor;
using UnityEngine;

namespace HorrorGame.General.Editor
{
    /// <summary>
    /// Editor-only preview switch. It never changes the material or renderer asset,
    /// so Play mode and player builds always use the authored retro presentation.
    /// </summary>
    [InitializeOnLoad]
    internal static class RetroCompositeEditorPreview
    {
        private const string PreferenceKey = "HorrorGame.RetroComposite.EditorPreviewEnabled";
        private const string MenuPath = "Tools/Horror Game/PSX Preview Enabled";

        static RetroCompositeEditorPreview()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += ApplyPreviewState;
            ApplyPreviewState();
        }

        [MenuItem(MenuPath)]
        private static void TogglePreview()
        {
            EditorPrefs.SetBool(PreferenceKey, !PreviewEnabled);
            ApplyPreviewState();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateTogglePreview()
        {
            Menu.SetChecked(MenuPath, PreviewEnabled);
            return true;
        }

        private static bool PreviewEnabled => EditorPrefs.GetBool(PreferenceKey, true);

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ApplyPreviewState();
        }

        private static void ApplyPreviewState()
        {
            // Testing must look like the shipped game, even when the editor preview is off.
            float enabled = EditorApplication.isPlayingOrWillChangePlaymode || PreviewEnabled ? 1f : 0f;
            Shader.SetGlobalFloat("_RetroCompositeEditorEnabled", enabled);
        }
    }
}
