using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class CameraStack
    {
        private static CameraStack m_Instance;

        public static CameraStack Instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = new CameraStack();
                return m_Instance;
            }
        }

        private CameraPOV ActiveCamera
        {
            get
            {
                return m_CameraStack.Count > 0 ? m_CameraStack[m_CameraStack.Count - 1] : null;
            }
        }

        private List<CameraPOV> m_CameraStack = new List<CameraPOV>();
        private CameraPOV m_FallbackCamera;

        // --------------------------------------------------------------------

        public List<CameraPOV> GetCameras()
        {
            return m_CameraStack;
        }

        // --------------------------------------------------------------------

        public void AddCamera(CameraPOV cam, int priority = 0)
        {
            CameraPOV prevCam = ActiveCamera ?? m_FallbackCamera;

            if (m_FallbackCamera)
                m_FallbackCamera = null;

            if (!m_CameraStack.Contains(cam))
            {
                if (!ActiveCamera || ActiveCamera.Priority <= priority)
                {
                    m_CameraStack.Add(cam);
                }
                else
                {
                    // Insert into the list based on priority
                    int i;
                    for (i = m_CameraStack.Count-1; i > 0; --i)
                    {
                        if (m_CameraStack[i].Priority <= priority)
                        {
                            m_CameraStack.Insert(i, cam);
                            break;
                        }
                    }
                    if (i == 0)
                        m_CameraStack.Insert(0, cam);
                }
            }

            if (ActiveCamera != prevCam)
            {
                ActiveCamera.ResetPlayerFollowState();
                ActiveCamera.Activate();
                if (prevCam)
                    prevCam.Deactivate();

            }
        }

        // --------------------------------------------------------------------

        public void RemoveCamera(CameraPOV cam)
        {
            CameraPOV prevCam = ActiveCamera ?? m_FallbackCamera;

            m_CameraStack.Remove(cam);

            if (ActiveCamera != null)
            {
                if (ActiveCamera != prevCam)
                {
                    ActiveCamera.Activate();
                    prevCam?.Deactivate();
                }
                else if (cam != ActiveCamera)
                {
                    cam.Deactivate();
                }
            }
            else if (prevCam)
            {
                // No trigger camera remains: keep the most recent POV alive so its
                // player-follow behaviour continues until another trigger is entered.
                m_FallbackCamera = prevCam;
                m_FallbackCamera.Activate();
            }
        }

        // --------------------------------------------------------------------

        public void ClearAllCameras()
        {
            m_CameraStack.Clear();
            m_FallbackCamera?.Deactivate();
            m_FallbackCamera = null;
        }
    }

}
