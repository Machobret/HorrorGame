using System;
using UnityEngine;

namespace HorrorGame.Environment
{
    /// <summary>Owns the renderer's property block; never creates a material instance.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class TruckCargoAtlas : MonoBehaviour
    {
        static readonly int TileProperty = Shader.PropertyToID("_CargoDesign");
        [SerializeField, Range(0, 5), InspectorName("Cargo Design")]
        [Tooltip("Selects one of six horizontal designs in the truck atlas.")]
        int tileIndex;
        [SerializeField, HideInInspector] string placementIdentity;
        MeshRenderer truckRenderer;
        MaterialPropertyBlock properties;
        bool runtimeInitialized;

        public int TileIndex => tileIndex;

        void OnEnable()
        {
            if (Application.IsPlaying(gameObject))
            {
                // Once per spawned lifetime, including scene instances. Do not disturb Unity's global RNG.
                if (!runtimeInitialized)
                {
                    tileIndex = NewTile();
                    runtimeInitialized = true;
                }
                Apply();
            }
#if UNITY_EDITOR
            else QueueEditorRefresh();
#endif
        }

        void OnValidate()
        {
            tileIndex = Mathf.Clamp(tileIndex, 0, 5);
#if UNITY_EDITOR
            // OnValidate can run on the loading thread. Renderer and identity work must be deferred.
            QueueEditorRefresh();
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= RefreshEditorPlacement;
#endif
            if (truckRenderer && truckRenderer.sharedMaterials.Length > 1) truckRenderer.SetPropertyBlock(null, 1);
        }

        [ContextMenu("Randomize Cargo Design")]
        public void RandomizeTile()
        {
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject)) UnityEditor.Undo.RecordObject(this, "Randomize truck cargo design");
#endif
            tileIndex = NewTile();
            Apply();
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject)) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        static int NewTile() => (int)((uint)Guid.NewGuid().GetHashCode() % 6u);

        void Apply()
        {
            if (!truckRenderer) truckRenderer = GetComponent<MeshRenderer>();
            if (truckRenderer.sharedMaterials.Length < 2) return;
            properties ??= new MaterialPropertyBlock();
            truckRenderer.GetPropertyBlock(properties, 1);
            properties.SetFloat(TileProperty, tileIndex);
            truckRenderer.SetPropertyBlock(properties, 1);
        }

#if UNITY_EDITOR
        void QueueEditorRefresh()
        {
            UnityEditor.EditorApplication.delayCall -= RefreshEditorPlacement;
            UnityEditor.EditorApplication.delayCall += RefreshEditorPlacement;
        }

        void RefreshEditorPlacement()
        {
            if (!this || !isActiveAndEnabled) return;
            if (Application.IsPlaying(gameObject)) { Apply(); return; }
            if (!UnityEditor.EditorUtility.IsPersistent(this)
                && UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) == null)
            {
                var id = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this);
                // Unsaved scenes have no asset GUID. The session ID still distinguishes duplicates.
                string identity = id.assetGUID == default || id.targetObjectId == 0
                    ? "Unsaved:" + GetInstanceID()
                    : id.ToString();
                if (placementIdentity != identity)
                {
                    placementIdentity = identity;
                    tileIndex = NewTile();
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                }
            }
            Apply();
        }
#endif
    }
}

