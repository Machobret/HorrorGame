using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.Enemies
{
    // Keep the template Animator for combat events; mirror its pose timing on the Brute rig.
    [ExecuteAlways, DefaultExecutionOrder(-200)]
    public sealed class BruteVisualModel : MonoBehaviour
    {
        private const string ModelPath = "Brute/Blobber_Model";
        private GameObject m_Model;
        private Animator m_Source;
        private Animator m_Visual;
        private AnimatorOverrideController m_Controller;
        private Renderer[] m_OriginalRenderers;
        private bool[] m_OriginalVisibility;
        private AnimatorCullingMode m_OriginalCulling;

        private void Awake() { Build(); }
        private void OnEnable() { Build(); }

        private void Build()
        {
            if (m_Model) return;
            var actor = GetComponent<Actor>();
            if (!actor || !actor.MainAnimator) return;
            var prefab = Resources.Load<GameObject>(ModelPath);
            var material = Resources.Load<Material>("Brute/Brute_PSX_Skin");
            var pantsMaterial = Resources.Load<Material>("Brute/Brute_Pants");
            if (!prefab || !material) return; // Import may still be in progress in the Editor.
            m_Source = actor.MainAnimator;
            m_OriginalRenderers = m_Source.GetComponentsInChildren<Renderer>(true);
            m_OriginalVisibility = new bool[m_OriginalRenderers.Length];
            m_Model = Instantiate(prefab, transform, false);
            m_Model.name = "Blobber model";
            m_Model.hideFlags = HideFlags.DontSave;
            m_Model.transform.localPosition = Vector3.zero;
            m_Model.transform.localRotation = Quaternion.identity;
            m_Model.transform.localScale = Vector3.one;
            m_Visual = m_Model.GetComponentInChildren<Animator>();
            if (!m_Visual) m_Visual = m_Model.AddComponent<Animator>();
            foreach (var renderer in m_Model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = pantsMaterial && materials[i] && materials[i].name.StartsWith("Brute_Pants")
                        ? pantsMaterial : material;
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            m_Controller = new AnimatorOverrideController(m_Source.runtimeAnimatorController);
            m_Controller.hideFlags = HideFlags.DontSave;
            var clips = Resources.LoadAll<AnimationClip>(ModelPath);
            var byName = new Dictionary<string, AnimationClip>();
            foreach (var clip in clips) byName[clip.name] = clip;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            m_Controller.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                var original = overrides[i].Key;
                if (!byName.TryGetValue(original.name, out var replacement))
                {
                    Debug.LogError("Brute model is missing animation: " + original.name, this);
                    Cleanup();
                    return;
                }
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
            }
            m_Controller.ApplyOverrides(overrides);
            m_Visual.runtimeAnimatorController = m_Controller;
            m_Visual.applyRootMotion = false;
            m_Visual.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Playback is sampled from the gameplay Animator, so its attack timing remains authoritative.
            m_Visual.speed = 0f;
            m_Visual.Rebind();
            m_Visual.Update(0f);
            m_OriginalCulling = m_Source.cullingMode;
            if (Application.IsPlaying(gameObject)) m_Source.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < m_OriginalRenderers.Length; i++)
            {
                m_OriginalVisibility[i] = m_OriginalRenderers[i].forceRenderingOff;
                m_OriginalRenderers[i].forceRenderingOff = true;
            }
        }

        private void LateUpdate()
        {
            if (!m_Model) { Build(); return; }
            if (!Application.IsPlaying(gameObject) || !m_Source || !m_Source.isActiveAndEnabled ||
                !m_Visual || !m_Visual.isActiveAndEnabled) return;
            m_Visual.SetFloat("Speed", m_Source.GetFloat("Speed"));
            var state = m_Source.IsInTransition(0) ? m_Source.GetNextAnimatorStateInfo(0) :
                m_Source.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != 0 && m_Visual.HasState(0, state.fullPathHash))
            {
                m_Visual.Play(state.fullPathHash, 0, state.normalizedTime);
                m_Visual.Update(0f);
            }
        }

        private void Cleanup()
        {
            if (m_OriginalRenderers != null)
                for (int i = 0; i < m_OriginalRenderers.Length; i++)
                    if (m_OriginalRenderers[i]) m_OriginalRenderers[i].forceRenderingOff = m_OriginalVisibility[i];
            if (m_Source && Application.IsPlaying(gameObject)) m_Source.cullingMode = m_OriginalCulling;
            if (Application.IsPlaying(gameObject))
            {
                if (m_Model) Destroy(m_Model);
                if (m_Controller) Destroy(m_Controller);
            }
            else
            {
                if (m_Model) DestroyImmediate(m_Model);
                if (m_Controller) DestroyImmediate(m_Controller);
            }
            m_Model = null;
            m_Controller = null;
        }

        private void OnDisable() { Cleanup(); }
    }
}
