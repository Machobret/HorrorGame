using System;
using UnityEngine;

namespace HorrorGame.Environment
{
    /// <summary>Owns the renderer's property block; never creates a material instance.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer))]
    public sealed class StorageBoxAtlas : MonoBehaviour
    {
        static readonly int TileProperty = Shader.PropertyToID("_AtlasTile");
        [SerializeField, Range(0, 15), InspectorName("Box Design")]
        [Tooltip("Selects one of 16 four-tile groups in the 8 x 8 atlas.")]
        int tileIndex;
        [SerializeField, HideInInspector] string placementIdentity;
        MeshRenderer boxRenderer;
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
            tileIndex = Mathf.Clamp(tileIndex, 0, 15);
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
            if (boxRenderer) boxRenderer.SetPropertyBlock(null);
        }

        [ContextMenu("Randomize Atlas Tile")]
        public void RandomizeTile()
        {
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject)) UnityEditor.Undo.RecordObject(this, "Randomize storage box tile");
#endif
            tileIndex = NewTile();
            Apply();
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject)) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        static int NewTile() => (int)((uint)Guid.NewGuid().GetHashCode() % 16u);

        void Apply()
        {
            if (!boxRenderer) boxRenderer = GetComponent<MeshRenderer>();
            properties ??= new MaterialPropertyBlock();
            boxRenderer.GetPropertyBlock(properties);
            properties.SetFloat(TileProperty, tileIndex);
            boxRenderer.SetPropertyBlock(properties);
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
