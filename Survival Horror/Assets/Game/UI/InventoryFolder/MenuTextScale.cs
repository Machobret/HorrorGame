using TMPro;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class MenuTextScale : MonoBehaviour
    {
        public float BaseSize=18,BaseMinimum=18,BaseMaximum=18;
        void OnEnable()=>Apply();
        public void Apply()
        {
            var text=GetComponent<TMP_Text>();float scale=MenuReadability.LargeText?1.2f:1;
            text.fontSize=BaseSize*scale;text.fontSizeMin=BaseMinimum*scale;text.fontSizeMax=BaseMaximum*scale;
        }
    }
}
