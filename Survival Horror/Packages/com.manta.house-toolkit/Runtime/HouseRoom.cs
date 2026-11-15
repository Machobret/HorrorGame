using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways]
    public sealed class HouseRoom : MonoBehaviour
    {
        [HideInInspector] public string roomId;
        [HideInInspector] public int[] corners;
        [HideInInspector] public Vector2[] polygon;
        [HideInInspector] public int[] topTriangles;
        [HideInInspector] public float thickness;
        public Material floorFinish, ceilingFinish, wallFinish;
        Mesh mesh;

        void OnEnable() => Refresh();
        void OnDestroy()
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
        public void Refresh()
        {
            if (!GetComponent<MeshFilter>() || polygon == null || polygon.Length < 3 || topTriangles == null) return;
            if (!mesh) mesh = new Mesh {name = "Room slab", hideFlags = HideFlags.DontSave};
            mesh.Clear();
            int n = polygon.Length;
            var vertices = new Vector3[2*n];
            var uv = new Vector2[2*n];
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(polygon[i].x, 0, polygon[i].y);
                vertices[n+i] = vertices[i] - Vector3.up * thickness;
                uv[i] = uv[n+i] = polygon[i];
                int j = (i+1)%n;
                indices.AddRange(new[]{i,j,n+j,i,n+j,n+i});
            }
            for (int i = 0; i < topTriangles.Length; i += 3)
            {
                int a = topTriangles[i], b = topTriangles[i+1], c = topTriangles[i+2];
                indices.AddRange(new[]{c,b,a,n+a,n+b,n+c});
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(indices,0); mesh.RecalculateNormals();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
