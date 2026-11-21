using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsQualityPreset", menuName = "Horror Engine/Settings/Quality Preset")]
    public class GraphicSettingsElementQualityPreset : SettingsElementComboContent
    {
        public override string GetDefaultValue()
        {
            return GetItemName(QualitySettings.GetQualityLevel());
        }

        public override int GetItemIndex(string name)
        {
            // Older builds saved the index when settings were reset to defaults.
            if (int.TryParse(name, out int index) && index >= 0 && index < GetItemCount())
                return index;
            return base.GetItemIndex(name);
        }

        public override void Apply()
        {
            if (SettingsManager.Instance.Get(this, out string outVal))
            {
                int index = GetItemIndex(outVal);
                if (index >= 0) 
                    QualitySettings.SetQualityLevel(index);
            }
        }

        public override string GetItemName(int index)
        {
            return QualitySettings.names[index];
        }

        public override int GetItemCount()
        {
            return QualitySettings.names.Length;
        }
    }
}
