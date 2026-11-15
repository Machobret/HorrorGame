using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HouseToolkit
{
    // Render batches can contain paint. Collision triangles keep their authoring target
    // independently of material batching, so replacing a material cannot change picking.
    internal sealed class RoadMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        // UV0.xy is the material UV. UV0.z distinguishes strips, T/X junctions and markings.
        readonly List<Vector4> uv = new List<Vector4>();
        readonly List<Vector4> traffic = new List<Vector4>();
        readonly List<Vector4> acrossRoads = new List<Vector4>();
        readonly List<Vector4> intoJunction = new List<Vector4>();
        List<Vector4> facadeData;
        readonly List<Color32> styles = new List<Color32>();
        readonly List<Material> materials = new List<Material>();
        readonly List<List<int>> batches = new List<List<int>>();
        readonly List<int> solid = new List<int>();
        readonly List<RoadHit> targets = new List<RoadHit>();

        public void Face(Material material, RoadHit hit, Vector3 normal, Vector3[] points, Vector2[] tex = null, bool paint = false,
            int junctionArms = 0, RoadFamily style = null, RoadFamily styleB = null, RoadFamily styleC = null, bool atlasPaint = false, RoadGeometry.JunctionWear wear = null, Vector4? facade = null, Color32? facadeStyle = null)
        {
            int batch = materials.IndexOf(material);
            if (batch < 0) { batch = materials.Count; materials.Add(material); batches.Add(new List<int>()); }
            int first = vertices.Count;
            Color32 primary = facadeStyle ?? (style ? style.SurfaceStyle : (Color32)Color.white);
            if (atlasPaint)
            {
                var materialColor = style.CrossingPaint.color;
                var tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? materialColor.linear : materialColor;
                tint.a = style.paintWear;
                primary = tint;
            }
            var counts=wear?.lanes ?? new Vector4(style?style.LaneCount:0,0,0,0);
            if(wear!=null && acrossRoads.Count==0)
                for(int i=0;i<first;i++){acrossRoads.Add(Vector4.zero);intoJunction.Add(Vector4.zero);}
            if(facade.HasValue && facadeData==null)
                facadeData=new List<Vector4>(new Vector4[first]);
            for (int i = 0; i < points.Length; i++)
            {
                vertices.Add(points[i]);
                facadeData?.Add(facade.GetValueOrDefault());
                traffic.Add(counts);
                if(wear!=null || acrossRoads.Count>0)
                {
                    var across=Vector4.zero;var inward=Vector4.zero;
                    if(wear!=null)wear.Project(points[i],out across,out inward);
                    acrossRoads.Add(across);intoJunction.Add(inward);
                }
                Vector2 coords = tex != null ? tex[i] : Mathf.Abs(normal.y) > .5f
                    ? new Vector2(points[i].x, points[i].z)
                    : new Vector2(Vector3.Dot(points[i], Vector3.Cross(Vector3.up,normal).normalized), points[i].y);
                uv.Add(new Vector4(coords.x, coords.y, atlasPaint ? 2 : paint ? 1 : junctionArms, wear?.turns ?? 0));
                styles.Add(i == 1 && styleB ? styleB.SurfaceStyle : i == 2 && styleC ? styleC.SurfaceStyle : primary);
            }
            for (int i = 2; i < points.Length; i++)
            {
                int a = first, b = first + i - 1, c = first + i;
                var cross = Vector3.Cross(vertices[b]-vertices[a], vertices[c]-vertices[a]);
                if (cross.sqrMagnitude < 1e-14f) continue;
                if (Vector3.Dot(cross, normal) < 0) (b,c) = (c,b);
                var indices = batches[batch];
                indices.Add(a); indices.Add(b); indices.Add(c);
                if (paint) continue;
                solid.Add(a); solid.Add(b); solid.Add(c);
                targets.Add(hit);
            }
        }

#if !UNITY_EDITOR
        static Material defaultSurface;
#endif
        static Material DefaultSurface()
        {
            if(GraphicsSettings.currentRenderPipeline) return GraphicsSettings.currentRenderPipeline.defaultMaterial;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
#else
            if(!defaultSurface) defaultSurface=new Material(Shader.Find("Standard")) {name="Default pavement",hideFlags=HideFlags.HideAndDontSave};
            return defaultSurface;
#endif
        }

        public void Apply(Mesh surface, Mesh collision, out RoadHit[] hits, out Material[] slots)
        {
            surface.Clear(); collision.Clear();
            surface.SetVertices(vertices); surface.SetUVs(0, uv); surface.SetColors(styles); surface.SetUVs(2, traffic);
            if(acrossRoads.Count>0){surface.SetUVs(3,acrossRoads);surface.SetUVs(4,intoJunction);}
            if(facadeData!=null)surface.SetUVs(5,facadeData);
            surface.subMeshCount = batches.Count;
            for (int i = 0; i < batches.Count; i++) surface.SetTriangles(batches[i], i, false);
            surface.RecalculateNormals(); surface.RecalculateTangents(); surface.RecalculateBounds();
            collision.SetVertices(vertices); collision.SetTriangles(solid, 0);
            hits = targets.ToArray();
            slots = materials.ToArray();
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i]) slots[i] = DefaultSurface();
        }
    }
}
