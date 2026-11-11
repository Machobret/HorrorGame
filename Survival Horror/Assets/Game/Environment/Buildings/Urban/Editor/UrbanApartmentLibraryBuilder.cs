using UnityEditor;

namespace HorrorGame.Environment.Editor
{
    public static class UrbanApartmentLibraryBuilder
    {
        const string Plan = "ArtSource/Environment/Urban/Selection.json";
        const string Receipt = "ArtSource/Environment/Urban/ImportReceipt.json";
        const string Output = "Exports/Reviews/Urban";
        const string Title = "URBAN / SIX APARTMENT EXTERIORS";
        [MenuItem("Tools/Horror Game/Urban/Rebuild Apartment Prefabs")]
        public static void Build() => ExteriorBuildingLibraryBuilder.BuildLibrary(Plan, Receipt, Output, Title, "Urban_Apartment");
        public static void RunBatch() => ExteriorBuildingLibraryBuilder.RunLibraryBatch(Plan, Receipt, Output, Title, "Urban_Apartment");
    }
}
