using System;
using UnityEngine;

namespace HouseToolkit
{
    [ExecuteAlways, SelectionBase, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public abstract class ProceduralSurface : MonoBehaviour
    {
        // Scene picking reads all three layers. Only an object's supported layers are serialized.
        static readonly RoadGraph emptyGraph = new RoadGraph();
        static readonly GroundPlan emptyGround = new GroundPlan();
        internal virtual RoadGraph Streets => emptyGraph;
        internal virtual RoadFamily StreetFamily => null;
        internal virtual RoadMesh CreateGeometry() => WithPavement(new RoadMesh());
        internal RoadMesh WithPavement(RoadMesh mesh)
        {
            PavementGeometry.Build(mesh,this);
            return mesh;
        }
        internal virtual RoadGraph Paths { get => emptyGraph; set => throw new InvalidOperationException("This object has no sidewalk paths."); }
        internal virtual GroundPlan Areas { get => emptyGround; set => throw new InvalidOperationException("This object has no ground areas."); }
        public virtual float PathWidth => 1.5f;
        public abstract Material Pavement { get; }
        public virtual float PavementElevation => 0;
        public Material PathMaterial(RoadSpan span)
        {
            var style = Paths.RunStyle(span.run);
            return span.pathMaterial ? span.pathMaterial : style != null && style.material ? style.material : Pavement;
        }
        [NonSerialized] public RoadHit[] faces = Array.Empty<RoadHit>();
        Mesh surface, collision;
        protected void OnEnable() => Rebuild();
#if UNITY_EDITOR
        bool dirty;
        protected void OnValidate() => dirty = true;
        protected void Update() { if (dirty) Rebuild(); }
#endif
        public void Rebuild()
        {
#if UNITY_EDITOR
            dirty = false;
#endif
            if (!surface) surface = NewMesh(gameObject.name+" surface");
            if (!collision) collision = NewMesh(gameObject.name+" collision");
            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = null;
            var geometry = CreateGeometry();
            geometry.Apply(surface, collision, out faces, out var materials);
            GetComponent<MeshFilter>().sharedMesh = surface;
            GetComponent<MeshRenderer>().sharedMaterials = materials;
            if (faces.Length > 0) collider.sharedMesh = collision;
        }
        static Mesh NewMesh(string name) => new Mesh { name = name, hideFlags = HideFlags.DontSave,
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        protected void OnDestroy() { Release(surface); Release(collision); }
        static void Release(Mesh mesh)
        {
            if (!mesh) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }
    }
}
