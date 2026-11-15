using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class CursorGrid
    {
        internal static Vector2 Center(Vector2 point, float step) =>
            new Vector2(Mathf.Round(point.x / step), Mathf.Round(point.y / step)) * step;

        internal static void Draw(SceneView view, HouseFloor floor, Vector2 snapped, float elevation = 0)
        {
            Draw(view, floor.transform, snapped, elevation);
        }

        internal static void Draw(SceneView view, Transform space, Vector2 snapped, float elevation = 0)
        {
            var e = Event.current;
            float step = PlanSnapping.Grid;
            if (e.type != EventType.Repaint || e.alt || step <= 0 || Tools.viewToolActive ||
                EditorWindow.mouseOverWindow != view) return;

            var center = Center(snapped, step);
            var origin = space.TransformPoint(new Vector3(center.x, elevation, center.y));
            using (new Handles.DrawingScope(Matrix4x4.TRS(origin, space.rotation, space.lossyScale)))
            {
                const int radius = 4;
                for (int i = -radius; i <= radius; i++)
                {
                    float fade = 1 - Mathf.Abs(i) / (radius + 1f);
                    float span = (radius + .5f) * Mathf.Sqrt(fade) * step;
                    Handles.color = new Color(.64f, .83f, 1f, .14f + .32f * fade);
                    Handles.DrawLine(new Vector3(i * step, 0, -span), new Vector3(i * step, 0, span));
                    Handles.DrawLine(new Vector3(-span, 0, i * step), new Vector3(span, 0, i * step));
                }
                var offset = snapped - center;
                Handles.color = Color.white;
                Handles.DrawWireDisc(new Vector3(offset.x, 0, offset.y), Vector3.up, step * .11f);
            }
        }
    }
}
