using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnvironmentAuthoring.Editor
{
    // Both UI systems share this row contract. Property views own values and
    // commands; this helper owns only the label column and field spacing.
    internal static class HousePropertyFields
    {
        private const float LabelWidth = 120;
        private const float RowHeight = 20;
        private const float RowGap = 2;

        internal static void Message(Label label, string text)
        {
            label.text = text;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        internal static void Align<T>(BaseField<T> field)
        {
            field.style.marginLeft = field.style.marginRight = field.style.marginTop = 0;
            field.style.marginBottom = RowGap;
            field.style.paddingLeft = field.style.paddingRight = 0;
            field.style.height = RowHeight;
            field.style.flexShrink = 0;
            var label = field.labelElement;
            label.style.width = label.style.minWidth = label.style.maxWidth = LabelWidth;
            label.style.flexShrink = 0;
            label.style.marginLeft = label.style.marginRight = 0;
            label.style.paddingLeft = label.style.paddingRight = 0;
            // Keep the native label: thickness controls use it for pointer drags.
            var input = field.Q<VisualElement>(className: BaseField<T>.inputUssClassName);
            input.style.minWidth = 0;
            input.style.flexGrow = input.style.flexShrink = 1;
            input.style.marginLeft = input.style.marginRight = 0;
        }

        internal static Rect InputRect(GUIContent label)
        {
            var row = GUILayoutUtility.GetRect(0, 10000, RowHeight, RowHeight,
                GUIStyle.none, GUILayout.ExpandWidth(true));
            GUILayout.Space(RowGap);
            int previousIndent = EditorGUI.indentLevel;
            try
            {
                EditorGUI.indentLevel = 0;
                var labelRect = row;
                labelRect.width = LabelWidth;
                EditorGUI.LabelField(labelRect, label);
                // PrefixLabel adds its own control gap. Split the row explicitly
                // so IMGUI has exactly the same input origin as retained fields.
                row.xMin += LabelWidth;
                return row;
            }
            finally
            {
                EditorGUI.indentLevel = previousIndent;
            }
        }

        internal static float DelayedFloat(string label, float value) => DelayedFloat(new GUIContent(label), value);
        internal static float DelayedFloat(GUIContent label, float value)
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                return EditorGUI.DelayedFloatField(InputRect(label), value);
        }
        internal static string DelayedText(string label, string value)
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                return EditorGUI.DelayedTextField(InputRect(new GUIContent(label)), value);
        }
        internal static Material Material(string label, Material value, bool wallFinish = false) => Material(new GUIContent(label), value, wallFinish);
        internal static Material Material(GUIContent label, Material value, bool wallFinish = false)
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                return MaterialInput(InputRect(label), value, wallFinish);
        }
        private static Material MaterialInput(Rect row, Material value, bool wallFinish = false)
        {
            int picker = GUIUtility.GetControlID("construction-material-picker".GetHashCode(), FocusType.Passive);
            if (Event.current.type == EventType.ExecuteCommand && Event.current.commandName == "ObjectSelectorUpdated"
                && EditorGUIUtility.GetObjectPickerControlID() == picker)
            { value = EditorGUIUtility.GetObjectPickerObject() as Material; GUI.changed = true; }
            var browse = row; browse.xMin = row.xMax - 23; row.xMax = browse.xMin - 2;
            value = (Material)EditorGUI.ObjectField(row, value, typeof(Material), false);
            bool changed = GUI.changed;
            if (GUI.Button(browse, new GUIContent("…", wallFinish
                ? "Browse wall finishes by surface. Custom materials can still be dragged into the object field."
                : "Browse curated construction finishes with native mesh previews. Search by surface, e.g. M_Brick. The object field also accepts custom/baked materials.")))
            {
                if (wallFinish)
                {
                    var menu = new GenericMenu();
                    var current = value;
                    menu.AddItem(new GUIContent("All wall finishes"), false,
                        () => EditorGUIUtility.ShowObjectPicker<Material>(current, false, ConstructionMaterialCatalog.WallQuery(), picker));
                    menu.AddItem(new GUIContent("All construction materials"), false,
                        () => EditorGUIUtility.ShowObjectPicker<Material>(current, false, "l:Construction", picker));
                    menu.AddSeparator("");
                    foreach (string surface in ConstructionMaterialCatalog.WallSurfaces)
                    {
                        string category = surface;
                        menu.AddItem(new GUIContent(category), false,
                            () => EditorGUIUtility.ShowObjectPicker<Material>(current, false, ConstructionMaterialCatalog.WallQuery(category), picker));
                    }
                    menu.DropDown(browse);
                }
                else EditorGUIUtility.ShowObjectPicker<Material>(value, false, "l:Construction", picker);
            }
            GUI.changed = changed; // Opening the browser alone must never create an inherited override.
            return value;
        }
        internal static Material InheritedMaterial(GUIContent label, Material stored, Material effective,
            bool anyOverrides, out bool reset, bool wallFinish = false)
        {
            var row = InputRect(label);
            var resetRect = row; resetRect.xMin = row.xMax - 40;
            row.xMax = resetRect.xMin - 3;
            EditorGUI.BeginChangeCheck();
            var next = MaterialInput(row, effective, wallFinish);
            bool edited = EditorGUI.EndChangeCheck();
            using (new EditorGUI.DisabledScope(!anyOverrides))
                reset = GUI.Button(resetRect, new GUIContent("Reset", "Restore inheritance for every selected target."));
            if (reset) GUI.changed = true;
            // Merely displaying or pinging an inherited asset must not save an override.
            return reset ? null : edited ? next : stored;
        }

        internal static T ObjectOverride<T>(GUIContent label, T stored, T effective, bool anyOverrides) where T : UnityEngine.Object
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
            {
                var row = InputRect(label);
                var reset = row;
                reset.xMin = row.xMax - 40;
                row.xMax = reset.xMin - 3;
                EditorGUI.BeginChangeCheck();
                var next = (T)EditorGUI.ObjectField(row, effective, typeof(T), false);
                bool edited = EditorGUI.EndChangeCheck();
                using (new EditorGUI.DisabledScope(!anyOverrides))
                    if (GUI.Button(reset, new GUIContent("Reset", "Remove this override and inherit its default.")))
                    { GUI.changed = true; return null; }
                return edited ? next : stored;
            }
        }
        internal static T Enum<T>(string label, T value) where T : Enum
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                return (T)EditorGUI.EnumPopup(InputRect(new GUIContent(label)), value);
        }
        internal static bool Toggle(string label, bool value)
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                return EditorGUI.Toggle(InputRect(new GUIContent(label)), value);
        }
        internal static int Popup(string label, int value, string[] choices)
        {
            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                return EditorGUI.Popup(InputRect(new GUIContent(label)), value, choices);
        }
    }
}
