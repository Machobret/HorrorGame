using TMPro;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class MenuReadability : MonoBehaviour
    {
        public TMP_Text ToggleLabel;
        public static bool LargeText=>PlayerPrefs.GetInt("HorrorGame.LargeMenuText",0)!=0;
        readonly List<TMP_Text> texts=new List<TMP_Text>();
        float nextScan;
        void OnEnable()=>Refresh();
        void LateUpdate(){if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+.5f;DiscoverText();}}
        void DiscoverText(){GetComponentsInChildren(true,texts);foreach(var text in texts)if(!text.GetComponent<MenuTextScale>()){float size=text.fontSize,min=text.fontSizeMin,max=text.fontSizeMax;var marker=text.gameObject.AddComponent<MenuTextScale>();marker.BaseSize=size;marker.BaseMinimum=min;marker.BaseMaximum=max;marker.Apply();}}
        public void Toggle(){PlayerPrefs.SetInt("HorrorGame.LargeMenuText",LargeText?0:1);PlayerPrefs.Save();Refresh();}
        public void Refresh(){DiscoverText();foreach(var text in GetComponentsInChildren<MenuTextScale>(true))text.Apply();if(ToggleLabel)ToggleLabel.text=LargeText?"TEXT: LARGE":"TEXT: NORMAL";}
    }
}
