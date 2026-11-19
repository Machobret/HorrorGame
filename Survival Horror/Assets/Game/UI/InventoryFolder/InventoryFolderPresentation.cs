using UnityEngine;
using HorrorEngine;
using TMPro;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Inventory-only visual motion; item selection and actions stay in native UIInventory.</summary>
    public sealed class InventoryFolderPresentation : MonoBehaviour
    {
        public RawImage Fog;
        public Material FogMaterial;
        public CanvasGroup Content;
        public RectTransform Notebook;
        public TMP_Text CapacityText,FeedbackText;
        public UIInventory InventoryUI;
        public UnityEngine.UI.ScrollRect DescriptionScroll;
        float feedbackUntil;
        public void ShowFeedback(string message){if(!FeedbackText)return;FeedbackText.text=message;FeedbackText.color=message.StartsWith("Cannot") ? new Color(.48f,.18f,.15f) : new Color(.16f,.155f,.145f);feedbackUntil=Time.unscaledTime+2.5f;}
        [Min(.05f)] public float OpeningDuration=.2f;
        Material runtime;
        double opened,pageSelected;
        void OnEnable()
        {
            opened=Time.realtimeSinceStartupAsDouble;pageSelected=opened;
            runtime=new Material(FogMaterial);Fog.material=runtime;
            Content.alpha=0;
        }
        public void SelectPage(){pageSelected=Time.realtimeSinceStartupAsDouble;if(DescriptionScroll){DescriptionScroll.StopMovement();DescriptionScroll.verticalNormalizedPosition=1;}}
        void LateUpdate()
        {
            if(FeedbackText && InventoryUI && InventoryUI.CombiningEntry!=null){GameText.SetLabel(FeedbackText, "COMBINE: choose a second item");FeedbackText.color=new Color(.16f,.155f,.145f);}
            else if(FeedbackText && Time.unscaledTime>feedbackUntil)FeedbackText.text="";
            if(CapacityText && GameManager.Exists)
            {
                var inventory=GameManager.Instance.Inventory;
                GameText.SetLabel(CapacityText, "SPACE USED  " + inventory.UsedSlots + " / " + (inventory.IsInfinite() ? "UNLIMITED" : inventory.Capacity.ToString()));
            }
            if(runtime)runtime.SetFloat("_FogTime",Time.unscaledTime);
            float t=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-opened)/OpeningDuration);
            Content.alpha=Mathf.SmoothStep(0,1,t);
            float page=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-pageSelected)/.18f);
            if(Notebook)Notebook.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(.12f,-.12f,Mathf.SmoothStep(0,1,page)));
        }
        void OnDisable(){if(Fog)Fog.material=FogMaterial;if(runtime)Destroy(runtime);runtime=null;}
    }
}
