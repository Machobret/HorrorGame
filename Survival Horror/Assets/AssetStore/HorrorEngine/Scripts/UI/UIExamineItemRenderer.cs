using UnityEngine;

namespace HorrorEngine
{
    public class UIExamineItemRenderer : MonoBehaviour
    {
        public Transform PreviewObject;
        public Camera Camera;
        public InteractionRaycastDetector InteractionDetector;

        [SerializeField] private float m_RenderTextureScale = 1f;
        [SerializeField] private FilterMode m_RenderTextureFilterMode = FilterMode.Bilinear;
        [SerializeField] private Vector3 m_Location = new Vector3(0, 0, -1000);

        private RenderTexture m_Texture;

        public RenderTexture Texture => m_Texture;

        /// <summary>Center the visible geometry and fit a rotation-safe sphere to the preview camera.</summary>
        public bool FrameModel(GameObject model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>(false);
            int previewLayer=-1;
            for(int layer=0;layer<32;layer++)if((Camera.cullingMask & (1<<layer))!=0){previewLayer=layer;break;}
            if(previewLayer<0)return false;
            // Change only instantiated preview visuals; world prefabs and gameplay collision layers stay intact.
            foreach(var renderer in renderers)if((Camera.cullingMask & (1<<renderer.gameObject.layer))==0)renderer.gameObject.layer=previewLayer;
            Bounds bounds=default;
            bool found=false;
            foreach(var renderer in renderers)
            {
                if(!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)continue;
                var local=renderer.localBounds;
                for(int k=0;k<8;k++)
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
                    var point=PreviewObject.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                }
            }
            if(!found || bounds.size.sqrMagnitude<1e-12f)return false;
            model.transform.localPosition-=bounds.center;
            var scale=PreviewObject.lossyScale;
            float radius=Mathf.Max(.001f,bounds.extents.magnitude*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            float halfVertical=Camera.fieldOfView*Mathf.Deg2Rad*.5f;
            float halfHorizontal=Mathf.Atan(Mathf.Tan(halfVertical)*Camera.aspect);
            float distance=radius/Mathf.Sin(Mathf.Min(halfVertical,halfHorizontal))*1.1f;
            // The template camera is on this root and owns PreviewObject; move the pivot, not its parent.
            PreviewObject.position=Camera.transform.position+Camera.transform.forward*distance;
            Camera.nearClipPlane=Mathf.Max(.0001f,(distance-radius)*.2f);
            Camera.farClipPlane=Mathf.Max(Camera.nearClipPlane+1,distance+radius*4);
            if(Camera.orthographic)Camera.orthographicSize=radius*1.1f/Mathf.Min(1,Camera.aspect);
            return true;
        }


        // --------------------------------------------------------------------

        private void OnEnable()
        {
            transform.position = m_Location;
            UpdateTexture();
        }

        // --------------------------------------------------------------------

#if UNITY_EDITOR
        private void Update()
        {
            UpdateTexture();
        }
#endif

        // --------------------------------------------------------------------

        private void UpdateTexture()
        {
            int w = (int)(Screen.width * m_RenderTextureScale);
            int h = (int)(Screen.height * m_RenderTextureScale);
            if (!m_Texture || m_Texture.width != w || m_Texture.height != h)
            {
                m_Texture?.Release();
                m_Texture = new RenderTexture(w, h, 16);
                m_Texture.filterMode = m_RenderTextureFilterMode;

                Camera.targetTexture = m_Texture;
            }
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            m_Texture?.Release();
        }
    }
}
