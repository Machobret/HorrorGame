using HorrorEngine;
using TMPro;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class InventoryItemAnnotation : MonoBehaviour
    {
        public UIInventoryItem Slot;
        public UIInventory InventoryUI;
        public InventoryIconProfile Profile;
        public TMP_Text Quantity,Equipped,Empty,CombineMark;
        void OnEnable(){if(!InventoryUI)InventoryUI=GetComponentInParent<UIInventory>();}
        void LateUpdate()
        {
            if(!InventoryUI)InventoryUI=GetComponentInParent<UIInventory>();
            if(!GameManager.Exists)return;
            var entry=Slot ? Slot.InventoryEntry : InventoryUI.SelectedEntry;
            var item=entry?.Item;
            if(Empty)Empty.gameObject.SetActive(!item);
            if(CombineMark)CombineMark.gameObject.SetActive(item && InventoryUI && InventoryUI.CombiningEntry==entry);
            if(Equipped)
            {
                bool equipped=false;
                if(item)foreach(var pair in GameManager.Instance.Inventory.Equipped)if(pair.Value==entry)equipped=true;
                Equipped.gameObject.SetActive(equipped);
            }
            if(!Quantity)return;
            Quantity.gameObject.SetActive(item);
            if(!item)return;
            if(item is ReloadableWeaponData weapon)
                Quantity.text="Loaded: "+Mathf.RoundToInt(entry.SecondaryCount)+" / "+Mathf.Max(weapon.MaxAmmo,Mathf.RoundToInt(entry.SecondaryCount));
            else if(item.Flags.HasFlag(ItemFlags.Stackable))
                Quantity.text=Mathf.RoundToInt(entry.Count)+(Profile && Profile.IsAmmunition(item) ? " rounds" : " items");
            else Quantity.text="";
        }
    }
}
