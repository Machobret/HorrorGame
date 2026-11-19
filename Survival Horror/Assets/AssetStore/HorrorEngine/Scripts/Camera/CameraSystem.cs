
using UnityEngine;

#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif

namespace HorrorEngine
{
    public class CameraSystem : SingletonBehaviour<CameraSystem>
    {
        public static readonly int CamPreviewOverride = -1;

        private CinemachineBrain m_Brain;
        private Camera m_MainCamera;

        public CinemachineVirtualCameraBase ActiveCamera => m_Brain
            ? m_Brain.ActiveVirtualCamera as CinemachineVirtualCameraBase
            : null;
        public Camera MainCamera => m_MainCamera;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_Brain = GetComponentInChildren<CinemachineBrain>();
            m_Brain.ReleaseCameraOverride(CamPreviewOverride);

            m_MainCamera = m_Brain.GetComponent<Camera>();
        }

        // --------------------------------------------------------------------

        void OnDestroy()
        {
            CameraStack.Instance.ClearAllCameras();
        }
    }
}
