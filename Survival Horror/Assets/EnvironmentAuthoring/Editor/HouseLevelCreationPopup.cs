using System;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal sealed class HouseLevelCreationPopup : PopupWindowContent
    {
        private static readonly string[] Choices = { "Nothing", "Outside contours", "Rooms and walls", "Complete layout" };
        private static readonly string[] Descriptions =
        {
            "Create an empty level.",
            "Copy the exterior footprint without interior partitions.",
            "Copy rooms, walls, openings and their materials.",
            "Also copy the floor's dividers, slab cuts and per-area settings. Existing roof edits are preserved; conflicting copies are rejected."
        };
        private readonly HouseBuilding building;
        private readonly bool below;
        private HouseLevelInheritance inheritance = HouseLevelInheritance.OutsideContours;
        private string error = "";

        internal HouseLevelCreationPopup(HouseBuilding building, bool below)
        { this.building = building; this.below = below; }

        public override Vector2 GetWindowSize() => new Vector2(340, 240);

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField(below ? "Add level below" : "Add level above", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            inheritance = (HouseLevelInheritance)EditorGUILayout.Popup("Inherit", (int)inheritance, Choices);
            if (EditorGUI.EndChangeCheck()) error = "";
            EditorGUILayout.LabelField(Descriptions[(int)inheritance], EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            if (error.Length > 0) EditorGUILayout.HelpBox(error, MessageType.Warning);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(building == null))
                    if (GUILayout.Button("Create"))
                    {
                        try
                        {
                            HouseEditingContext.AddLevelAndSelect(building, below, inheritance);
                            editorWindow.Close();
                        }
                        catch (InvalidOperationException exception) { error = exception.Message; }
                    }
                if (GUILayout.Button("Cancel")) editorWindow.Close();
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { Event.current.Use(); editorWindow.Close(); }
        }
    }
}
