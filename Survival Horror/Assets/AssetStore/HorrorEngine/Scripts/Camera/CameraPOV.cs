using System;
using System.Collections.Generic;
using UnityEngine;

using Unity.Cinemachine;

namespace HorrorEngine 
{

    public class CameraPOV : MonoBehaviour
    {
        public int Priority;

        private CinemachineVirtualCameraBase m_VirtualCam;
        private int m_TriggerEnterCount;
        private Dictionary<OnDisableNotifier, Collider> m_TargetTracked = new Dictionary<OnDisableNotifier, Collider>();
        private Action<OnDisableNotifier> m_TargetDisabledCallback;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_TargetDisabledCallback = OnTargetDisabled;

            m_VirtualCam = GetComponentInChildren<CinemachineVirtualCameraBase>(true);

            if (!m_VirtualCam)
            {
                Debug.LogError($"CameraPOV '{name}' requires a Cinemachine virtual camera in its children.", this);
                enabled = false;
                return;
            }

            var disableNotifier = GetComponentInChildren<OnDisableNotifier>();
            if (disableNotifier) 
            {
                disableNotifier.AddCallback((notif) => 
                {
                    m_TriggerEnterCount = 0;
                    CameraStack.Instance.RemoveCamera(this);
                });
            }

            Deactivate();
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            foreach(var entry in m_TargetTracked)
            {
                var disableNotifier = entry.Key;
                disableNotifier?.RemoveCallback(m_TargetDisabledCallback);
            }
            m_TargetTracked.Clear();
        }

        // --------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponent<CameraPOVTarget>())
                return;

            if (!m_TargetTracked.ContainsValue(other)) 
            {
                var notifier = other.GetComponentInParent<OnDisableNotifier>();
                notifier.AddCallback(m_TargetDisabledCallback);
                m_TargetTracked.Add(notifier, other);
            }

            ++m_TriggerEnterCount;
            CameraStack.Instance.AddCamera(this, Priority);
        }

        // --------------------------------------------------------------------

        private void OnTriggerExit(Collider other)
        {
            if (!other.GetComponent<CameraPOVTarget>())
                return;

            if (m_TargetTracked.ContainsValue(other))
            {
                var notifier = other.GetComponentInParent<OnDisableNotifier>();
                RemoveCallback(notifier, other);
            }

            --m_TriggerEnterCount;
            Debug.Assert(m_TriggerEnterCount >= 0, $"Trigger count went negative in CameraPOV {name}");
            if (m_TriggerEnterCount == 0)
                CameraStack.Instance.RemoveCamera(this);
        }

        // --------------------------------------------------------------------

        private void OnTargetDisabled(OnDisableNotifier notifier)
        {
            var collider = m_TargetTracked[notifier];
            RemoveCallback(notifier, collider);
            OnTriggerExit(collider);
        }

        // --------------------------------------------------------------------

        private void RemoveCallback(OnDisableNotifier notifier, Collider collider)
        {
            notifier.RemoveCallback(m_TargetDisabledCallback);
            m_TargetTracked.Remove(notifier);
        }

        // --------------------------------------------------------------------

        public void Activate()
        {
            if (m_VirtualCam)
                m_VirtualCam.gameObject.SetActive(true);
        }

        // --------------------------------------------------------------------

        public void Deactivate()
        {
            if (m_VirtualCam)
                m_VirtualCam.gameObject.SetActive(false);
        }
    }
}
