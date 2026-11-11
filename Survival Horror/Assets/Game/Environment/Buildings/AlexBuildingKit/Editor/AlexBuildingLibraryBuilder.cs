using UnityEditor;

namespace HorrorGame.Environment.Editor
{
    public static class AlexBuildingLibraryBuilder
    {
        const string Plan = "ArtSource/Environment/AlexBuildingKit/Selection.json";
        const string Receipt = "ArtSource/Environment/AlexBuildingKit/ImportReceipt.json";
        const string Output = "Exports/Reviews/AlexBuildingKit";
        const string Title = "ALEX / SIX ASSEMBLED BUILDING EXTERIORS";
        [MenuItem("Tools/Horror Game/Alex Building Kit/Rebuild Building Prefabs")]
        public static void Build() => ExteriorBuildingLibraryBuilder.BuildLibrary(Plan, Receipt, Output, Title, "Alex_");
        public static void RunBatch() => ExteriorBuildingLibraryBuilder.RunLibraryBatch(Plan, Receipt, Output, Title, "Alex_");
        public static void VerifyRebuilds() { Build(); UrbanApartmentLibraryBuilder.Build(); }
    }
}
