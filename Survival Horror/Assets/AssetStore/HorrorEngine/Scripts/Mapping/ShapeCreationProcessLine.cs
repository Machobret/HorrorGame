using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "ShapeCreationProcessLine", menuName = "Horror Engine/Mapping/ShapeCreationLine")]
    public class ShapeCreationProcessLine : ShapeCreationProcess
    {
        public Material Material;
        public float Thickness;
        public bool Loop;

        public override GameObject Create(string name, Transform parent, int layer, ShapeData[] shapes, Matrix4x4 trs)
        {
            GameObject go = new GameObject(name + "_Line");
            go.transform.parent = parent;
            go.transform.position = Vector3.zero;
            go.layer = layer;

            foreach (var shape in shapes)
            {
                Vector3[] vertices = new Vector3[shape.Points.Length];
                for (int i = 0; i < vertices.Length; ++i)
                {
                    Vector3 pos = trs.MultiplyPoint(shape.Points[i]);
                    vertices[i] = go.transform.InverseTransformPoint(pos);
                }

                if (shape.Connections == null || shape.Connections.Length == 0)
                {
                    LineRenderer lineRenderer = CreateLine(go);
                    lineRenderer.positionCount = vertices.Length;
                    lineRenderer.SetPositions(vertices);
                    lineRenderer.loop = Loop || (shape.Closed && vertices.Length >= 3);
                }
                else
                {
                    // Keep the normal ordered path, then layer any authored
                    // point-to-point links over it. One Shape can therefore
                    // render loops and branches instead of being one polyline.
                    for (int i = 1; i < vertices.Length; ++i) CreateEdge(go, vertices[i - 1], vertices[i]);
                    if ((Loop || shape.Closed) && vertices.Length >= 3) CreateEdge(go, vertices[vertices.Length - 1], vertices[0]);
                    foreach (ShapeEdge edge in shape.Connections)
                    {
                        if (edge.From < 0 || edge.To < 0 || edge.From >= vertices.Length || edge.To >= vertices.Length || edge.From == edge.To) continue;
                        CreateEdge(go, vertices[edge.From], vertices[edge.To]);
                    }
                }
            }

            return go;
        }

        private LineRenderer CreateLine(GameObject go)
        {
            LineRenderer lineRenderer = go.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.sharedMaterial = Material;
            lineRenderer.widthMultiplier = Thickness;
            return lineRenderer;
        }

        private void CreateEdge(GameObject go, Vector3 from, Vector3 to)
        {
            LineRenderer lineRenderer = CreateLine(go);
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, from);
            lineRenderer.SetPosition(1, to);
        }
    }
}
