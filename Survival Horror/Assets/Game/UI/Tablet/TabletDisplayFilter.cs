using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Composites the tablet's Canvas through its screen material, without filtering other UI.</summary>
    public sealed class TabletDisplayFilter : MonoBehaviour
    {
        public Camera DisplayCamera;
        public RawImage Output;
        public Material ScreenMaterial;
        RenderTexture target;
        Material material;
        void OnEnable() => Resize();
        void LateUpdate()
        {
            if(material)material.SetFloat("_ScreenTime",Time.unscaledTime);
            if(target==null||target.width!=Screen.width||target.height!=Screen.height)Resize();
        }
        void Resize()
        {
            if(!DisplayCamera||!Output||!ScreenMaterial)return;
            Release();
            // Keep screen-sized input coordinates for native UI raycasts. The shader reduces the visible resolution.
            target=new RenderTexture(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),24){name="Tablet display",filterMode=FilterMode.Point};target.Create();
            material=new Material(ScreenMaterial);Output.material=material;Output.texture=target;DisplayCamera.targetTexture=target;
        }
        void OnDisable() => Release();
        void Release()
        {
            if(DisplayCamera)DisplayCamera.targetTexture=null;
            if(Output){Output.texture=null;Output.material=null;}
            if(target){target.Release();Destroy(target);target=null;}
            if(material){Destroy(material);material=null;}
        }
    }
}
