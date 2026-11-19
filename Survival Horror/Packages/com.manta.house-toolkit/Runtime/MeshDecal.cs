using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HouseToolkit
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MeshDecal : MonoBehaviour
    {
        [SerializeField] internal WallDecalAtlas atlas;
        [SerializeField] internal string poster;
        static readonly int atlasTransform = Shader.PropertyToID("_BaseMap_ST");

        void OnEnable() => Refresh();

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall -= RefreshIfLoaded;
            UnityEditor.EditorApplication.delayCall += RefreshIfLoaded;
        }

        void RefreshIfLoaded()
        {
            if (this && isActiveAndEnabled && !UnityEditor.EditorUtility.IsPersistent(this)) Refresh();
        }
#endif

        internal void Refresh()
        {
            var art = atlas ? atlas.Find(poster) : null;
            if (art == null) return;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = atlas.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            var uv = art.uv;
            properties.SetVector(atlasTransform, new Vector4(uv.width, uv.height, uv.x, uv.y));
            renderer.SetPropertyBlock(properties);
        }

        internal static MeshDecal Create(Scene scene, WallDecalAtlas family, WallDecalAtlas.Poster art,
            Vector3 position, Vector3 outward, Vector3 up, Vector2 scale)
        {
            var go = new GameObject(art.label);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-outward, up));
            go.transform.localScale = new Vector3(art.size.x * scale.x, art.size.y * scale.y, 1f);
            var decal = go.AddComponent<MeshDecal>();
            decal.atlas = family;
            decal.poster = art.id;
            decal.Refresh();
            return decal;
        }
    }
}
