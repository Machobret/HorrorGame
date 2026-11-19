using HorrorEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Inventory presentation of the player's existing health; owns no gameplay state.</summary>
    public sealed class InventoryConditionFluid : MonoBehaviour
    {
        public Image Window;
        public TMP_Text Label;
        public Material FluidMaterial;
        public bool ShowFluid;
        [Range(0,1)] public float InjuredBelow=.66f;
        [Range(0,1)] public float CriticalBelow=.33f;
        [Min(.01f)] public float TransitionSpeed=2f;
        Material runtime;
        Health health;
        float displayedDamage;

        void OnEnable()
        {
            Window.enabled=ShowFluid;
            if(ShowFluid){runtime=new Material(FluidMaterial);Window.material=runtime;}
            BindHealth();
            displayedDamage=health ? 1-Mathf.Clamp01(health.Normalized) : 0;
            UpdateDisplay();
        }

        void BindHealth()
        {
            var player=GameManager.Exists ? GameManager.Instance.Player : null;
            health=player ? player.GetComponent<Health>() : null;
        }

        void LateUpdate()
        {
            BindHealth();
            float damage=health ? 1-Mathf.Clamp01(health.Normalized) : 0;
            displayedDamage=Mathf.MoveTowards(displayedDamage,damage,Time.unscaledDeltaTime*TransitionSpeed);
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            if(runtime){runtime.SetFloat("_Damage",displayedDamage);runtime.SetFloat("_FluidTime",Time.unscaledTime);}
            float ratio=health ? Mathf.Clamp01(health.Normalized) : 1;
            GameText.SetLabel(Label, ratio <= CriticalBelow ? "CRITICAL" : ratio < InjuredBelow ? "INJURED" : "FINE");
            Label.color=ratio<InjuredBelow ? new Color(.48f,.18f,.15f) : new Color(.18f,.17f,.15f);
        }

        void OnDisable()
        {
            if(Window)Window.material=FluidMaterial;
            if(runtime)Destroy(runtime);
            runtime=null;
            health=null;
        }
    }
}
