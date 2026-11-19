using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace HorrorEngine
{
   
    [CreateAssetMenu(fileName = "GameSettingLanguage", menuName = "Horror Engine/Settings/Language")]
    public class GameSettingsElementLanguage : SettingsElementComboContent
    {
        public Locale[] Languages;

        public override void Apply()
        {
            if (SettingsManager.Instance.Get(this, out string outVal))
            {
                foreach(var locale in Languages)
                {
                    if (locale.LocaleName == outVal)
                    {
                        LocalizationSettings.SelectedLocale = locale;
                        return;
                    }
                }

                // Saved selections from a disabled locale fall back to English.
                LocalizationSettings.SelectedLocale = Languages[0];
            }
        }

        public override string GetDefaultValue()
        {
            return Languages[0].LocaleName;
        }

        public override int GetItemCount()
        {
            return Languages.Length;
        }

        public override string GetItemName(int index)
        {
            return Languages[index].LocaleName;
        }
    }
}