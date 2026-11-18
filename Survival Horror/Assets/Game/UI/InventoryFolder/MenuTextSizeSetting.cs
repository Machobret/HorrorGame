using HorrorEngine;
using UnityEngine;

namespace HorrorGame.UI
{
    public sealed class MenuTextSizeSetting : SettingsElementComboContent
    {
        private const string Key = "Game.InventoryTextSize";
        private const string LegacyKey = "HorrorGame.LargeMenuText";
        private const string Normal = "Normal";
        private const string Large = "Large";

        internal static bool UseLargeText => ReadSavedSize() != Normal;

        public override string GetDefaultValue() => Large;
        public override int GetItemCount() => 2;
        public override string GetItemName(int index) => index == 0 ? Normal : Large;

        // The menus read the saved setting when opened, so an unapplied change stays in Settings.
        public override void Apply() { }

        public override string GetPlayerPrefsValue() => ReadSavedSize();

        private static string ReadSavedSize()
        {
            string saved = ReadPlayerPrefsValue(Key);
            if (saved == Normal || saved == Large)
                return saved;

            if (PlayerPrefs.HasKey(LegacyKey))
                return PlayerPrefs.GetInt(LegacyKey) == 0 ? Normal : Large;

            return string.Empty;
        }
    }
}
