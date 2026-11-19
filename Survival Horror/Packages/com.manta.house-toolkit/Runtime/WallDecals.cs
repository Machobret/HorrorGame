using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseToolkit
{
    [Serializable]
    internal struct WallDecalAnchor
    {
        public int edge, storey, side;
        public Vector2 position;
    }

    [Serializable]
    internal sealed class WallDecalPlacement
    {
        public WallDecalAtlas atlas;
        public string poster;
        public WallDecalAnchor anchor;
        public float scale = 1;
        internal float Scale => scale > 0 && float.IsFinite(scale) ? scale : 1;

    }

    // Retains the serialized fields and script GUID of scenes authored with wall-owned posters.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class WallDecals : MonoBehaviour
    {
        [SerializeField] internal List<WallDecalPlacement> placements = new();

        void Start()
        {
            if (Application.isPlaying && Detach()) Destroy(this);
        }

        internal bool Detach()
        {
            var wall = GetComponent<HouseWall>();
            var building = GetComponent<FillerBuilding>();
            for (int i = placements.Count - 1; i >= 0; --i)
            {
                var placement = placements[i];
                var art = placement?.atlas ? placement.atlas.Find(placement.poster) : null;
                if (art == null) continue;
                var anchor = placement.anchor;
                Vector3 center, tangent, normal;
                if (wall)
                {
                    tangent = Vector3.right;
                    normal = Vector3.forward * anchor.side;
                    center = normal * (wall.Thickness * .5f);
                }
                else if (building)
                {
                    var edge = building.footprint.edges.Find(e => e.id == anchor.edge);
                    if (edge == null || !building.TryFacadeNormal(edge, out normal)) continue;
                    var delta = (edge.b - edge.a).normalized;
                    tangent = new Vector3(delta.x, 0, delta.y);
                    center = new Vector3(edge.a.x, building.Elevation(anchor.storey), edge.a.y);
                }
                else continue;
                center += tangent * anchor.position.x + Vector3.up * anchor.position.y + normal * .004f;
                var scale = new Vector2(transform.TransformVector(tangent).magnitude,
                    transform.TransformVector(Vector3.up).magnitude) * placement.Scale;
                var decal = MeshDecal.Create(gameObject.scene, placement.atlas, art, transform.TransformPoint(center),
                    transform.worldToLocalMatrix.transpose.MultiplyVector(normal).normalized, transform.up, scale);
                decal.gameObject.SetActive(enabled && gameObject.activeInHierarchy);
                placements.RemoveAt(i);
            }
            return placements.Count == 0;
        }
    }
}
