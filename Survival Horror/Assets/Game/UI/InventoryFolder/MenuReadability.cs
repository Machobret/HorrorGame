using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class MenuReadability : MonoBehaviour
    {
        public static bool LargeText => MenuTextSizeSetting.UseLargeText;
        private readonly List<TMP_Text> texts = new List<TMP_Text>();
        private float nextScan;

        private void OnEnable() => Refresh();

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextScan)
                return;

            nextScan = Time.unscaledTime + .5f;
            DiscoverText();
        }

        private void DiscoverText()
        {
            GetComponentsInChildren(true, texts);
            foreach (var text in texts)
            {
                if (text.GetComponent<MenuTextScale>())
                    continue;

                float size = text.fontSize;
                float minimum = text.fontSizeMin;
                float maximum = text.fontSizeMax;
                var marker = text.gameObject.AddComponent<MenuTextScale>();
                marker.BaseSize = size;
                marker.BaseMinimum = minimum;
                marker.BaseMaximum = maximum;
                marker.Apply();
            }
        }

        public void Refresh()
        {
            DiscoverText();
            foreach (var marker in GetComponentsInChildren<MenuTextScale>(true))
                marker.Apply();
        }
    }
}
