using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HorrorEngine
{
    [InitializeOnLoad]
    public static class MapPathConnectionTool
    {
        private static MapRoom s_Room;
        private static Shape s_FirstShape;
        private static int s_FirstPoint = -1;

        public static bool IsActive => s_Room != null;

        static MapPathConnectionTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static void Begin(Shape source)
        {
            s_Room = source ? source.GetComponentInParent<MapRoom>() : null;
            s_FirstShape = null;
            s_FirstPoint = -1;
            if (!s_Room) Debug.LogWarning("Map point connections must be started from a Shape inside a MapRoom.");
            SceneView.RepaintAll();
        }

        public static void Cancel()
        {
            s_Room = null;
            s_FirstShape = null;
            s_FirstPoint = -1;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!s_Room) return;
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                Cancel();
                evt.Use();
                return;
            }

            Shape closestShape = null;
            int closestPoint = -1;
            float closestDistance = 14f * 14f;
            foreach (Shape shape in s_Room.GetComponentsInChildren<Shape>(true))
            {
                if (!shape) continue;
                for (int i = 0; i < shape.Points.Count; ++i)
                {
                    Vector3 worldPoint = shape.transform.TransformPoint(shape.Points[i]);
                    Handles.color = shape == s_FirstShape && i == s_FirstPoint ? Color.green : new Color(.1f, .65f, 1f, 1f);
                    Handles.DotHandleCap(0, worldPoint, Quaternion.identity, HandleUtility.GetHandleSize(worldPoint) * .08f, EventType.Repaint);
                    float distance = (HandleUtility.WorldToGUIPoint(worldPoint) - evt.mousePosition).sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestShape = shape;
                        closestPoint = i;
                    }
                }
            }

            Handles.BeginGUI();
            GUI.Label(new Rect(12, 12, 370, 24), s_FirstShape ? "Map connection: click the second blue point" : "Map connection: click the first blue point", EditorStyles.whiteBoldLabel);
            Handles.EndGUI();

            if (evt.type != EventType.MouseDown || evt.button != 0 || evt.alt || !closestShape) return;
            if (!s_FirstShape)
            {
                s_FirstShape = closestShape;
                s_FirstPoint = closestPoint;
                evt.Use();
                SceneView.RepaintAll();
                return;
            }

            Connect(s_FirstShape, s_FirstPoint, closestShape, closestPoint);
            Cancel();
            evt.Use();
        }

        private static void Connect(Shape firstShape, int firstPoint, Shape secondShape, int secondPoint)
        {
            bool samePathEnds = firstShape == secondShape && ((firstPoint == 0 && secondPoint == firstShape.Points.Count - 1) || (secondPoint == 0 && firstPoint == firstShape.Points.Count - 1));
            if (samePathEnds)
            {
                Undo.RecordObject(firstShape, "Close Map Path Loop");
                firstShape.CloseShape = true;
                EditorUtility.SetDirty(firstShape);
                EditorSceneManager.MarkSceneDirty(firstShape.gameObject.scene);
                return;
            }

            MapDetailingShape template = firstShape.GetComponent<MapDetailingShape>();
            if (!template) template = s_Room.GetComponentInChildren<MapDetailingShape>(true);
            if (!template || !template.CreationProcess)
            {
                Debug.LogWarning("Add one Map Path to this room before connecting points so the connection has a line style.");
                return;
            }

            Vector3 firstWorldPoint = firstShape.transform.TransformPoint(firstShape.Points[firstPoint]);
            Vector3 secondWorldPoint = secondShape.transform.TransformPoint(secondShape.Points[secondPoint]);
            GameObject edge = new GameObject("Map Path Connection");
            Undo.RegisterCreatedObjectUndo(edge, "Connect Map Path Points");
            edge.transform.SetParent(s_Room.transform);
            edge.transform.localPosition = Vector3.zero;
            edge.transform.localRotation = Quaternion.identity;
            edge.transform.localScale = Vector3.one;

            MapDetailingShape edgePath = edge.AddComponent<MapDetailingShape>();
            edgePath.CreationProcess = template.CreationProcess;
            Shape edgeShape = edge.AddComponent<Shape>();
            edgeShape.CloseShape = false;
            edgeShape.Points = new List<Vector3>
            {
                s_Room.transform.InverseTransformPoint(firstWorldPoint),
                s_Room.transform.InverseTransformPoint(secondWorldPoint)
            };
            EditorUtility.SetDirty(edgePath);
            EditorUtility.SetDirty(edgeShape);
            EditorSceneManager.MarkSceneDirty(s_Room.gameObject.scene);
        }
    }
}
