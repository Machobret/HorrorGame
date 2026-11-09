using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Initial choices only. Created scene objects own their settings thereafter;
    // changing project defaults never rewrites an existing level.
    [FilePath("ProjectSettings/EnvironmentAuthoring.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class EnvironmentProjectSettings : ScriptableSingleton<EnvironmentProjectSettings>
    {
        internal const string SettingsPath = "Project/Environment Authoring";
        [SerializeField] internal WallFamily InteriorFamily, ExteriorFamily;
        [SerializeField] internal WallOpeningInsert[] CustomInserts = Array.Empty<WallOpeningInsert>();
        [SerializeField] internal Material InsideWalls, OutsideWalls, Floors, Ceilings, CutSurface;
        [SerializeField] internal TownModule WallModel;
        [SerializeField] internal Material RoadSurface, SidewalkSurface;

        internal void Apply(HouseBuilding house)
        {
            house.Data.ConfigureDefaultMaterials(InsideWalls, OutsideWalls, Floors);
            house.Data.ConfigureCeilingFinish(Ceilings, house.Data.CeilingTextureSize);
            house.Data.SetBooleanCutMaterial(CutSurface);
        }

        internal void Apply(HouseWallBuilder builder)
        {
            builder.Data.DefaultFamily = InteriorFamily;
            builder.Data.ExteriorFamily = ExteriorFamily;
            builder.Data.Inserts = (WallOpeningInsert[])CustomInserts?.Clone() ?? Array.Empty<WallOpeningInsert>();
        }

        internal void Apply(TownAuthoring target)
        {
            if (target is TownWallRun wall)
            {
                wall.Model = WallModel;
                if (WallModel != null) wall.Height = WallModel.Height;
            }
            if (target is TownSidewalk walk) walk.Material = SidewalkSurface;
            if (target is TownRoad road)
            { road.Material = RoadSurface; road.SidewalkMaterial = SidewalkSurface; }
        }

        internal string MissingSetup(EnvironmentCreateCommands.Kind kind)
        {
            bool missing = kind == EnvironmentCreateCommands.Kind.House ? InteriorFamily == null
                : kind == EnvironmentCreateCommands.Kind.Wall ? WallModel == null
                : kind == EnvironmentCreateCommands.Kind.Road ? RoadSurface == null
                : kind == EnvironmentCreateCommands.Kind.Sidewalk && SidewalkSurface == null;
            return missing ? "Choose default assets in Project Settings > Environment Authoring, or assign them in this object's Defaults." : "";
        }

        internal void SaveDefaults() => Save(true);
        internal static void Open() => SettingsService.OpenProjectSettings(SettingsPath);

        [SettingsProvider]
        private static SettingsProvider Provider() => new SettingsProvider(SettingsPath, SettingsScope.Project)
        {
            label = "Environment Authoring",
            keywords = new HashSet<string> { "House", "Wall", "Family", "Road", "Sidewalk", "Material" },
            guiHandler = _ =>
            {
                EditorGUILayout.HelpBox("Initial assets for new objects. Existing Houses and paths keep their own settings. Assign your content explicitly; sample scenes are optional.", MessageType.None);
                using var data = new SerializedObject(instance);
                data.Update();
                EditorGUILayout.LabelField("House geometry", EditorStyles.boldLabel);
                Field("InteriorFamily", "Interior family"); Field("ExteriorFamily", "Exterior family");
                Field("CustomInserts", "Custom insert models");
                EditorGUILayout.Space(); EditorGUILayout.LabelField("House finishes", EditorStyles.boldLabel);
                Field("InsideWalls", "Interior walls"); Field("OutsideWalls", "Exterior walls");
                Field("Floors", "Floors"); Field("Ceilings", "Ceilings"); Field("CutSurface", "Boolean cut surfaces");
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
                Field("WallModel", "Wall / fence model"); Field("RoadSurface", "Road finish"); Field("SidewalkSurface", "Sidewalk finish");
                if (data.ApplyModifiedProperties()) instance.SaveDefaults();
                void Field(string name, string label) => EditorGUILayout.PropertyField(data.FindProperty(name), new GUIContent(label), true);
            }
        };
    }
}
