using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
 
    public class AnimatorOverrider : MonoBehaviour, IResetable
    {
        [Tooltip("Optional player-specific idle. Applied after equipment overrides; other clips are unaffected.")]
        [SerializeField] private AnimationClip m_RelaxedIdleClip;
        [SerializeField] private AnimationClip[] m_RelaxedIdleSources;

        private Animator m_Animator;

        private AnimatorOverrideController m_OverrideController;

        private Dictionary<AnimationClip, AnimationClip> m_OverridesDictionary = new Dictionary<AnimationClip, AnimationClip>();
        private List<AnimatorOverrideController> m_AppliedControllers = new List<AnimatorOverrideController>();
        
        private List<KeyValuePair<AnimationClip, AnimationClip>> m_ClearOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        private List<KeyValuePair<AnimationClip, AnimationClip>> m_NewOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();

        private List<float> m_LayerWeights = new List<float>();

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Animator = GetComponentInChildren<Animator>();

            m_OverrideController = new AnimatorOverrideController(m_Animator.runtimeAnimatorController);
            m_Animator.runtimeAnimatorController = m_OverrideController;
            m_OverrideController.GetOverrides(m_NewOverrides);


            foreach (var anim in m_OverrideController.animationClips)
            {
                m_ClearOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(anim, null));
            }

            if (m_RelaxedIdleClip && m_RelaxedIdleSources != null)
            {
                m_NewOverrides.Clear();
                foreach (var pair in m_ClearOverrides)
                    if (IsRelaxedIdleSource(pair.Key))
                        m_NewOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(pair.Key, m_RelaxedIdleClip));
                m_OverrideController.ApplyOverrides(m_NewOverrides);
            }
        }

        // --------------------------------------------------------------------

        private void ReapplyAllOverrides()
        {
            m_OverridesDictionary.Clear();

            MapOverrides(m_ClearOverrides, false);
            m_OverrideController.ApplyOverrides(m_ClearOverrides);

            foreach (var overrideCtrl in m_AppliedControllers)
            {
                m_NewOverrides.Clear();
                overrideCtrl.GetOverrides(m_NewOverrides);
                MapOverrides(m_NewOverrides, true);
            }

            m_NewOverrides.Clear();
            
            foreach (var overrideEntry in m_OverridesDictionary)
            {
                var clip = overrideEntry.Value ? overrideEntry.Value : overrideEntry.Key;
                var replacement = IsRelaxedIdleSource(clip) ? m_RelaxedIdleClip : overrideEntry.Value;
                m_NewOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(overrideEntry.Key, replacement));
            }

            m_OverrideController.ApplyOverrides(m_NewOverrides);

            RebindAnimatorKeepingLayerWeights();
        }

        // --------------------------------------------------------------------

        public void RebindAnimatorKeepingLayerWeights()
        {
            m_LayerWeights.Clear();

            // Cache layer weights
            for (int i = 0; i< m_Animator.layerCount; ++i)
            {
                m_LayerWeights.Add(m_Animator.GetLayerWeight(i));
            }

            m_Animator.Rebind(); // This is needed so the animator doesn't go to default pose if the playing animation get overriden during CrossFadeInFixedTime

            // Restore layer weights
            for (int i = 0; i < m_Animator.layerCount; ++i)
            {
                m_Animator.SetLayerWeight(i, m_LayerWeights[i]);
            }
        }

        // --------------------------------------------------------------------

        private bool IsRelaxedIdleSource(AnimationClip clip)
        {
            if (!m_RelaxedIdleClip || m_RelaxedIdleSources == null) return false;
            foreach (var source in m_RelaxedIdleSources)
                if (source == clip) return true;
            return false;
        }

        private void MapOverrides(List<KeyValuePair<AnimationClip, AnimationClip>> overrideList, bool skipEmpty)
        {
            foreach (var pair in overrideList)
            {
                if (skipEmpty && !pair.Value)
                    continue;

                if (m_OverridesDictionary.ContainsKey(pair.Key))
                {
                    m_OverridesDictionary[pair.Key] = pair.Value;
                }
                else
                {
                    m_OverridesDictionary.Add(pair.Key, pair.Value);
                }
            }
        }

        // --------------------------------------------------------------------

        public void RemoveOverride(AnimatorOverrideController overrideCtrl)
        {
            if (m_AppliedControllers.Remove(overrideCtrl))
            {
                ReapplyAllOverrides();
            }
            else
            {
                Debug.LogError($"AnimationOverrideController {overrideCtrl} was not removed");
            }
        }

        // --------------------------------------------------------------------

        public void AddOverride(AnimatorOverrideController overrideCtrl)
        {
            if (!m_AppliedControllers.Contains(overrideCtrl))
            {
                m_AppliedControllers.Add(overrideCtrl);
                ReapplyAllOverrides();
            }
            else
            {
                Debug.LogError($"AnimationOverrideController {overrideCtrl} was already applied");
            }

        }

        // --------------------------------------------------------------------

        public void OnReset()
        {
            m_AppliedControllers.Clear();
            ReapplyAllOverrides();
        }
    }
}