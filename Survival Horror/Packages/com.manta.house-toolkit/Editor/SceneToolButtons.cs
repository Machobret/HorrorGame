using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseToolkit.Editor
{
    internal static class SceneToolButtons
    {
        internal static Button Add(VisualElement target, string title, string tip, System.Action action)
        {
            var button = new Button(action) {name = title, tooltip = tip};
            button.style.height = 26;
            button.style.flexDirection = FlexDirection.Row;
            button.style.justifyContent = Justify.FlexStart;
            button.style.alignItems = Align.Center;
            var icon = new VisualElement {pickingMode = PickingMode.Ignore};
            icon.style.width = icon.style.height = 16;
            icon.generateVisualContent += ctx => DrawIcon(ctx.painter2D, title);
            button.Add(icon);
            var label = new Label(title) {pickingMode = PickingMode.Ignore};
            label.style.marginLeft = 7;
            button.Add(label);
            target.Add(button);
            return button;
        }

        internal static void Highlight(Button button, bool active) =>
            button.style.backgroundColor = active ? new StyleColor(new Color(.22f,.43f,.61f)) : new StyleColor(StyleKeyword.Null);

        static void DrawIcon(Painter2D painter, string tool)
        {
            painter.strokeColor = EditorGUIUtility.isProSkin ? new Color(.85f,.85f,.85f) : new Color(.22f,.22f,.22f);
            painter.lineWidth = 1.3f;
            void Line(params float[] coords)
            {
                painter.BeginPath(); painter.MoveTo(new Vector2(coords[0],coords[1]));
                for (int i = 2; i < coords.Length; i += 2) painter.LineTo(new Vector2(coords[i],coords[i+1]));
                painter.Stroke();
            }
            switch (tool)
            {
                case "Draw Shelf": Line(2,15,2,1); Line(14,15,14,1); Line(2,3,14,3); Line(2,8,14,8); Line(2,13,14,13); break;
                case "Boolean cut": Line(1,4,8,1,15,4,8,7,1,4,1,12,8,15,8,7); Line(15,4,15,8,12,9,12,12,15,11,15,12,8,15); break;
                case "Slab opening": Line(1,9,7,3,15,6,9,13,1,9); Line(5,8,8,5.5f,11,7,8,10,5,8); break;
                case "Draw Road": Line(2,14,6,2); Line(10,14,14,2); Line(6,13,7,10); Line(8,7,9,4); break;
                case "Draw Sidewalk": Line(1,12,6,3,14,3); Line(4,14,8,6,15,6); Line(4,7,7,9); Line(10,3,10,6); break;
                case "Ground Area": Line(1,10,5,3,14,5,12,13,5,14,1,10); Line(5,10,7,6,10,7); break;
                case "Cutaway": Line(1,5,8,2,15,5,8,8,1,5); Line(1,8,8,11,15,8); Line(1,11,8,14,15,11); break;
                case nameof(HouseTool.Select): Line(3,2,13,8,8,9,6,14,3,2); break;
                case nameof(HouseTool.Wall): Line(2,13,2,3,14,3,14,13,2,13); Line(2,8,14,8); Line(6,3,6,8); Line(10,8,10,13); break;
                case nameof(HouseTool.Panel): Line(1,3,15,3,15,13,1,13,1,3); Line(4,3,4,13); Line(12,3,12,13); Line(4,11,12,5); break;
                case nameof(HouseTool.Door): Line(3,14,3,2,12,2,12,14); Line(6,14,6,4,12,2); Line(8,9,9,9); break;
                case nameof(HouseTool.Window): Line(2,3,14,3,14,13,2,13,2,3); Line(8,3,8,13); Line(2,8,14,8); break;
            }
        }
    }
}
