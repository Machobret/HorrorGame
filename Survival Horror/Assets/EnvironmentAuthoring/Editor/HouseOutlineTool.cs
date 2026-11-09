using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // A Unity palette/shortcut adapter. The context dispatches all Scene input to its session.
    [EditorTool("House tools", null, typeof(HouseEditingContext))]
    public sealed class HouseOutlineTool : EditorTool
    {
        public override GUIContent toolbarIcon => new GUIContent(EditorGUIUtility.IconContent("Prefab Icon").image, "House palette tools");
        public override bool IsAvailable() => HouseEditingContext.Active?.Session?.SessionPlan != null
            && !EditorApplication.isPlayingOrWillChangePlaymode;
        public override void OnWillBeDeactivated() => HouseEditingContext.Active?.Controller.CancelSceneGesture();
    }
}
