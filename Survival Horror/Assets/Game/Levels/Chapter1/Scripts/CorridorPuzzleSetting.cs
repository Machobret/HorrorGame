using HorrorEngine;

namespace HorrorGame.Chapter1
{
    public sealed class CorridorPuzzleSetting : SettingsElementComboContent
    {
        private const string Key = "Game.CorridorPuzzle";
        private const string PuzzleA = "Puzzle A";
        private const string PuzzleB = "Puzzle B";

        internal static bool UseTenantClue => ReadPlayerPrefsValue(Key) == PuzzleB;
        public override string GetDefaultValue() => PuzzleA;
        public override int GetItemCount() => 2;
        public override string GetItemName(int index) => index == 0 ? PuzzleA : PuzzleB;
        public override void Apply() { }
    }
}
