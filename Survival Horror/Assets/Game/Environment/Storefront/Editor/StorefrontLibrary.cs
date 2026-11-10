using System;
using System.Linq;
using EnvironmentAuthoring;
using EnvironmentAuthoring.Editor;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.Environment.Editor
{
    public static class StorefrontLibrary
    {
        public const string Folder = "Assets/Game/Environment/Storefront";
        public const string Sources = Folder + "/Sources";
        public const string FamilyPath = Sources + "/Wall_Storefront.asset";
        public const string DoorPath = Sources + "/Insert_Door_01_W1000_H2800_ModelStorefront.asset";
        public const string DoorFamilyPath = Folder + "/Glass_DoorFamily.asset";
        public const string DoorPrefabPath = "Assets/Game/Doors/Prefabs/Door_Glass.prefab";
        public const string GlassPath = GravelGlassLibrary.GlassPath;
        public const string FramePath = "Assets/EnvironmentAuthoring/Content/WallKits/FramedWindows/M_Frame_Painted_01.mat";

        [InitializeOnLoadMethod]
        private static void ScheduleDoorFamilyRename() => EditorApplication.delayCall += RenameDoorFamily;

        public static void RenameDoorFamily()
        {
            const string previousPath = Folder + "/Storefront_DoorFamily.asset";
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(previousPath)))
            {
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(DoorFamilyPath)))
                    throw new InvalidOperationException("Both old and renamed glass door catalogs exist; preserve their references before merging them.");
                string problem = AssetDatabase.MoveAsset(previousPath, DoorFamilyPath);
                if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
            }
            var doors = AssetDatabase.LoadAssetAtPath<OpeningFamily>(DoorFamilyPath);
            if (doors == null) return;
            bool changed = doors.name != "Glass_DoorFamily";
            doors.name = "Glass_DoorFamily";
            foreach (var set in doors.Sets ?? Array.Empty<OpeningFamily.ModelSet>())
                if (set != null && set.Name == "Storefront glass door")
                { set.Name = "Glass door"; changed = true; }
            if (changed)
            {
                EditorUtility.SetDirty(doors);
                AssetDatabase.SaveAssetIfDirty(doors);
            }
        }

        [MenuItem("Tools/Environment/Storefront/Import T150 glass family")]
        public static void Import()
        {
            RenameDoorFamily();
            var frame = Required<Material>(FramePath);
            var glass = Required<Material>(GlassPath);
            var family = WallFamilyImporter.Import(Sources);
            if (family.Id != "Storefront" || family.Definitions().Count() != 5)
                throw new InvalidOperationException("Storefront needs three T150 solids and two doorway surrounds.");
            if (!family.HasMaterialRoles)
            {
                var legacy = family.ReferenceBlank.BodyMaterials;
                family.MaterialRoles = new[]
                {
                    new WallFamily.MaterialRole { Id = "frame", Name = "Frame", Slot = 0,
                        DefaultMaterial = legacy != null && legacy.Length == 2 && legacy[0] != null ? legacy[0] : frame },
                    new WallFamily.MaterialRole { Id = "glass", Name = "Glass", Slot = 1,
                        DefaultMaterial = legacy != null && legacy.Length == 2 && legacy[1] != null ? legacy[1] : glass }
                };
            }
            foreach (var module in family.Definitions())
            {
                if (Mathf.Abs(module.Thickness - .15f) > .0001f)
                    throw new InvalidOperationException("Storefront connection thickness must be 150 mm.");
                module.BodyMaterials = Array.Empty<Material>();
                module.CoverSlabEdge = false;
                EditorUtility.SetDirty(module);
            }
            var door = Required<WallOpeningInsert>(DoorPath);
            if (door.Kind != FloorPlan.OpeningKind.Doorway || door.Mesh.subMeshCount != 2)
                throw new InvalidOperationException("Export the selected storefront door with opaque frame/glass submeshes.");
            door.Material = frame;
            door.GlassMaterial = glass;
            door.UseWallMaterialRoles = true;
            if (door.Prefab == null)
                door.Prefab = Game.Doors.Editor.ProceduralDoorSetup.EnsureVariant(door, DoorPrefabPath);
            EditorUtility.SetDirty(door);
            var doors = AssetDatabase.LoadAssetAtPath<OpeningFamily>(DoorFamilyPath);
            if (doors == null)
            {
                doors = ScriptableObject.CreateInstance<OpeningFamily>();
                AssetDatabase.CreateAsset(doors, DoorFamilyPath);
            }
            doors.Kind = FloorPlan.OpeningKind.Doorway;
            doors.Sets = new[] { new OpeningFamily.ModelSet { Name = "Glass door",
                State = OpeningFamily.Condition.Clean, Models = new[] { door } } };
            EditorUtility.SetDirty(doors);
            family.OpeningFamilies = new[] { doors };
            foreach (float width in new[] { 1f, 2f })
                if (!door.Fits(family, FloorPlan.OpeningKind.Doorway, family.OpeningFor(FloorPlan.OpeningKind.Doorway, width, door.Shape)))
                    throw new InvalidOperationException("Storefront door does not fit the " + width + " m surround.");
            // Standard aperture metadata makes this one shared insert usable by
            // ordinary families too. Append the catalog without replacing their
            // existing door choices or changing any wall geometry/materials.
            foreach (string guid in AssetDatabase.FindAssets("t:WallFamily"))
            {
                var host = Required<WallFamily>(AssetDatabase.GUIDToAssetPath(guid));
                if (host == family || (host.OpeningFamilies ?? Array.Empty<OpeningFamily>()).Contains(doors)) continue;
                // Match actual doorway definitions, not another opening with the
                // same shape/dimensions or a solid module.
                if (!(host.Openings ?? Array.Empty<WallFamily.Opening>()).Any(opening => opening != null
                    && opening.Kind == FloorPlan.OpeningKind.Doorway
                    && door.Fits(host, opening.Kind, opening.Definition))) continue;
                host.OpeningFamilies = (host.OpeningFamilies ?? Array.Empty<OpeningFamily>()).Append(doors).ToArray();
                EditorUtility.SetDirty(host);
            }
            EditorUtility.SetDirty(family);
            if (!family.IsValid(out string reason)) throw new InvalidOperationException(reason);
            AssetDatabase.SaveAssets();
        }

        public static void VerifyDoorPrefab()
        {
            var preset = Required<WallOpeningInsert>(DoorPath);
            var prefab = Required<GameObject>(DoorPrefabPath);
            var basis = Required<GameObject>("Assets/Game/Doors/Prefabs/Door_Base.prefab");
            if (preset.Prefab != prefab || PrefabUtility.GetCorrespondingObjectFromSource(prefab) != basis)
                throw new InvalidOperationException("Glass door must use a native Door_Base variant.");
            var door = prefab.GetComponent<HorrorEngine.TwoSidedDoor>();
            var interaction = prefab.GetComponent<HorrorEngine.Interactive>();
            var visual = prefab.transform.Find("Visual");
            var renderer = visual.GetComponent<MeshRenderer>();
            if (door == null || door.ExitPoint == null || door.OtherExitPoint == null || interaction == null
                || interaction.OnInteract.GetPersistentEventCount() != 1
                || interaction.OnInteract.GetPersistentTarget(0) != door
                || interaction.OnInteract.GetPersistentMethodName(0) != "Use"
                || !prefab.GetComponent<BoxCollider>().isTrigger
                || prefab.transform.Find("BodyCollider").GetComponent<BoxCollider>().isTrigger
                || visual.GetComponent<MeshFilter>().sharedMesh != preset.Mesh
                || renderer.sharedMaterials.Length != 2 || renderer.sharedMaterials[0] != preset.Material
                || renderer.sharedMaterials[1] != preset.GlassMaterial
                || Vector3.Distance(visual.localPosition, new Vector3(-preset.Opening.center.x, -preset.Opening.yMin, 0)) > .00001f)
                throw new InvalidOperationException("Glass door visual, interaction, exits or collider wiring is invalid.");
            Debug.Log("STOREFRONT_DOOR_VERIFIED: native Door_Base variant, two materials, two-sided interaction and colliders.");
        }

        public static void ImportAndVerify() { Import(); VerifyDoorPrefab(); }

        private static T Required<T>(string path) where T : UnityEngine.Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing storefront dependency: " + path);
    }
}
