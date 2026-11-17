using System.Collections.Generic;
using HorrorEngine;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Lay out native inventory entries in one or two cells without adding fake items to save data.</summary>
    public sealed class InventoryFootprintLayout : LayoutGroup
    {
        public int Columns=4;
        public float Gap=10;
        readonly List<UIInventoryItem> slots=new List<UIInventoryItem>();
        public override void CalculateLayoutInputHorizontal(){base.CalculateLayoutInputHorizontal();}
        public override void CalculateLayoutInputVertical(){SetLayoutInputForAxis(0,0,-1,1);}
        public override void SetLayoutHorizontal()=>Arrange();
        public override void SetLayoutVertical()=>Arrange();
        void LateUpdate()=>LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        void Arrange()
        {
            if(!GameManager.Exists)return;
            slots.Clear();GetComponentsInChildren(false,slots);
            var inventory=GameManager.Instance.Inventory;
            int capacity=inventory.IsInfinite()?Mathf.Max(8,inventory.UsedSlots+4):inventory.Capacity;
            int rows=Mathf.Max(1,Mathf.CeilToInt(capacity/(float)Columns));
            var rect=((RectTransform)transform).rect;
            float width=(rect.width-Gap*(Columns-1))/Columns,height=Mathf.Min(100,(rect.height-Gap*(rows-1))/rows);
            var occupied=new bool[rows*Columns];
            // Place wide items before single items so a free cell at a row end is not wasted.
            for(int pass=0;pass<3;pass++)foreach(var slot in slots)
            {
                var item=slot.InventoryEntry?.Item;int span=item?Mathf.Clamp(item.InventorySlots,1,Columns):1;
                if(pass==0 && (!item || span==1) || pass==1 && (!item || span!=1) || pass==2 && item)continue;
                int cell=-1;
                for(int i=0;i<capacity;i++){if(i%Columns+span>Columns || i+span>capacity)continue;bool free=true;for(int j=0;j<span;j++)if(occupied[i+j])free=false;if(free){cell=i;break;}}
                var group=slot.GetComponent<CanvasGroup>();if(!group)group=slot.gameObject.AddComponent<CanvasGroup>();
                group.alpha=cell<0?0:1;group.interactable=group.blocksRaycasts=cell>=0;
                var button=slot.GetComponent<Button>();if(button)button.interactable=cell>=0;
                if(cell<0)continue;
                for(int j=0;j<span;j++)occupied[cell+j]=true;
                var r=(RectTransform)slot.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(cell%Columns*(width+Gap),-cell/Columns*(height+Gap));r.sizeDelta=new Vector2(width*span+Gap*(span-1),height);
            }
        }
    }
}
