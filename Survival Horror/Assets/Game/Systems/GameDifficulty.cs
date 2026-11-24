using UnityEngine;

namespace HorrorGame.General
{
    /// <summary>Persistent difficulty selection shared by menu and gameplay systems.</summary>
    public enum GameDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    public static class DifficultySettings
    {
        public const string PlayerPrefsKey = "HorrorGame.Difficulty";

        public static GameDifficulty Current
        {
            get
            {
                int value = PlayerPrefs.GetInt(PlayerPrefsKey, (int)GameDifficulty.Normal);
                return (GameDifficulty)Mathf.Clamp(value, (int)GameDifficulty.Easy, (int)GameDifficulty.Hard);
            }
        }

        public static void Save(GameDifficulty difficulty)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, (int)difficulty);
            PlayerPrefs.Save();
        }
    }
}
