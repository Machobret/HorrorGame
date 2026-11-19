using UnityEngine;
using UnityEditor;
#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
#else
using Cinemachine;
#endif


namespace HorrorEngine
{
    [CustomEditor(typeof(CameraPreview))]
    public class CameraPreviewEditor : Editor
    {
        private Vector2Int m_Aspect = new Vector2Int(16, 9);

        private Transform m_TargetTransform;
#if UNITY_6000_0_OR_NEWER
        private CinemachineCamera m_VirtualCam;
        private CinemachineVirtualCamera m_LegacyVirtualCam;
#else
        private CinemachineVirtualCamera m_VirtualCam;
#endif
        private Camera m_RegularCam;

        private float m_Size = 250;
        
        private Camera m_Camera;
        private RenderTexture m_RenderTexture;
        // --------------------------------------------------------------------

        public void OnEnable()
        {
            if (!Application.isPlaying)
            {
                var preview = target as CameraPreview;
                m_TargetTransform = preview.transform;
                m_RegularCam = preview.GetComponent<Camera>();

#if UNITY_6000_0_OR_NEWER
                m_VirtualCam = preview.GetComponent<CinemachineCamera>();
                m_LegacyVirtualCam = preview.GetComponent<CinemachineVirtualCamera>();
#else
                m_VirtualCam = preview.GetComponent<CinemachineVirtualCamera>();
#endif

                var camGO = EditorUtility.CreateGameObjectWithHideFlags("Preview Scene Camera", HideFlags.HideAndDontSave, typeof(Camera));
                m_Camera = camGO.GetComponent<Camera>();
                m_Camera.cameraType = CameraType.Preview;
                m_Camera.enabled = false;
                m_Camera.clearFlags = CameraClearFlags.Skybox;
                m_Camera.forceIntoRenderTexture = true;
                m_Camera.renderingPath = RenderingPath.Forward;
                m_Camera.useOcclusionCulling = false;
                m_Camera.scene = preview.gameObject.scene;
                
                
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }
        }

        // --------------------------------------------------------------------

        private void Render()
        {
            
            float aspect = (float)Mathf.Max(m_Aspect.x, 1) / Mathf.Max(m_Aspect.y, 1);

            if (m_RenderTexture)
                RenderTexture.ReleaseTemporary(m_RenderTexture);

            m_RenderTexture = RenderTexture.GetTemporary((int)(m_Size * aspect), (int)m_Size, 16);
            
            m_Camera.transform.position = m_TargetTransform.position;
            m_Camera.transform.rotation = m_TargetTransform.rotation;
            m_Camera.targetTexture = m_RenderTexture;
            if (m_VirtualCam)
            {

#if UNITY_6000_0_OR_NEWER
                var lens = m_VirtualCam.Lens;
#else
                var lens = m_VirtualCam.m_Lens;
#endif
                m_Camera.fieldOfView = lens.FieldOfView;
                m_Camera.farClipPlane = lens.FarClipPlane;
                m_Camera.nearClipPlane = lens.NearClipPlane;
            }
#if UNITY_6000_0_OR_NEWER
            else if (m_LegacyVirtualCam)
            {
                var lens = m_LegacyVirtualCam.m_Lens;
                m_Camera.fieldOfView = lens.FieldOfView;
                m_Camera.farClipPlane = lens.FarClipPlane;
                m_Camera.nearClipPlane = lens.NearClipPlane;
            }
#endif
            else if (m_RegularCam)
            {
                m_Camera.fieldOfView = m_RegularCam.fieldOfView;
                m_Camera.farClipPlane = m_RegularCam.farClipPlane;
                m_Camera.nearClipPlane = m_RegularCam.nearClipPlane;
            }

            m_Camera.Render();
            
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            ReleaseCamera();
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        // --------------------------------------------------------------------

        public override void OnInspectorGUI()
        {
            if (!Application.isPlaying)
            {
                serializedObject.Update();
                var letterbox = serializedObject.FindProperty(nameof(CameraPreview.LetterboxHeight));
                EditorGUILayout.PropertyField(letterbox);
                serializedObject.ApplyModifiedProperties();
                m_Aspect = EditorGUILayout.Vector2IntField("Aspect", m_Aspect);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box("", GUILayout.Height(m_Size));
                EditorGUILayout.EndHorizontal();
                Rect scale = GUILayoutUtility.GetLastRect();
                if (m_Camera)
                {
                    Render();
                    var texture = m_Camera.targetTexture;
                    float fit = Mathf.Min(scale.width / texture.width, scale.height / texture.height);
                    Vector2 size = new Vector2(texture.width, texture.height) * fit;
                    var frame = new Rect(scale.center - size * .5f, size);
                    EditorGUI.DrawTextureTransparent(frame, texture, ScaleMode.StretchToFill);
                    float barHeight = frame.height * letterbox.floatValue;
                    if (barHeight > 0f && Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.DrawRect(new Rect(frame.x, frame.y, frame.width, barHeight), Color.black);
                        EditorGUI.DrawRect(new Rect(frame.x, frame.yMax - barHeight, frame.width, barHeight), Color.black);
                    }
                }

                if (GUILayout.Button(new GUIContent("Go to POV", "Moves the editor camera to this POV")))
                {
                    var sceneView = SceneView.lastActiveSceneView;

                    sceneView.camera.transform.position = m_TargetTransform.position;
                    sceneView.camera.transform.rotation = m_TargetTransform.rotation;
                    sceneView.camera.fieldOfView = 10;
                    sceneView.AlignViewToObject(sceneView.camera.transform);
                    
                }

                if (GUILayout.Button(new GUIContent("Move to viewport POV", "Moves the camera to the scene view position/rotation")))
                {
                    var sceneView = SceneView.lastActiveSceneView;

                    m_TargetTransform.position = sceneView.camera.transform.position;
                    m_TargetTransform.rotation = sceneView.camera.transform.rotation;

                    EditorUtility.SetDirty(m_TargetTransform);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Camera preview is not available in play mode", MessageType.Info);
            }
        }

        // --------------------------------------------------------------------

        private void ReleaseCamera()
        {
            if (m_Camera)
            {
                if (m_RenderTexture)
                    RenderTexture.ReleaseTemporary(m_RenderTexture);

                DestroyImmediate(m_Camera.gameObject);
                m_Camera = null;
            }
        }

        // --------------------------------------------------------------------

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ReleaseCamera();
            }
        }
    }
}
