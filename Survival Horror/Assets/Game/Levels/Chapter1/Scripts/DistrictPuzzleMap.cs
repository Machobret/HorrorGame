using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Chapter1
{
    /// <summary>Editable 2D map-shape marker, intentionally similar to map rooms but independent of the final art mesh.</summary>
    public sealed class DistrictPuzzleMap : MonoBehaviour
    {
        public List<DistrictMapShape> Shapes = new List<DistrictMapShape>();
        private void OnDrawGizmos()
        {
            foreach (DistrictMapShape shape in Shapes) shape.Draw(transform);
        }
    }
    [System.Serializable]
    public class DistrictMapShape
    {
        public string Label;
        public Color Color = Color.cyan;
        public List<Vector3> Points = new List<Vector3>();
        public void Draw(Transform root)
        {
            Gizmos.color = Color;
            for (int i = 0; i < Points.Count; i++) Gizmos.DrawLine(root.TransformPoint(Points[i]), root.TransformPoint(Points[(i + 1) % Points.Count]));
        }
    }
}
