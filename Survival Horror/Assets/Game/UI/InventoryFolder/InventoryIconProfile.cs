using System;
using HorrorEngine;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class InventoryIconProfile : ScriptableObject
    {
        [Serializable] public struct Entry {public Sprite Sprite;public Rect Visible;public bool Wide;public float Rotation;public Rect WideBounds;}
        public Entry[] Entries;
        public ItemData[] AmmoItems;
        public bool IsAmmunition(ItemData item){if(AmmoItems!=null)foreach(var ammo in AmmoItems)if(ammo==item)return true;return false;}
        public Entry GetEntry(Sprite sprite){if(Entries!=null)foreach(var entry in Entries)if(entry.Sprite==sprite)return entry;return new Entry{Visible=new Rect(0,0,1,1)};}
        public Rect GetVisible(Sprite sprite)
        {
            if(Entries!=null)foreach(var entry in Entries)if(entry.Sprite==sprite)return entry.Visible;
            return new Rect(0,0,1,1);
        }
    }
}
