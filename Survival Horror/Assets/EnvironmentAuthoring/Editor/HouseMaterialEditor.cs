using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    internal static class HouseMaterialEditor
    {
        private static readonly HashSet<int> expandedFamilies = new HashSet<int>();
        internal static HouseEditResult LevelWallDefaults(HousePlan plan)
        {
            var kit = plan.GetComponent<HouseWallBuilder>()?.Data;
            var walls = plan.Data.Walls.Where(wall => wall.IsPhysical).ToArray();
            var families = walls.Length > 0 ? walls.Select(wall => HouseFinishResolution.Family(plan, wall)).Distinct().ToArray()
                : kit?.FamilyDefinitions(plan.Data).ToArray() ?? Array.Empty<WallFamily>();
            foreach (var family in families.Where(family => family != null && family.HasMaterialRoles))
                DrawFamilyDefaults(family, collapsible: false);
            if (families.Length > 0 && families.All(family => family != null && family.HasMaterialRoles))
                return new HouseEditResult(HouseEditStatus.Unchanged);
            var house = HouseBuilding.For(plan)?.Data;
            EditorGUI.BeginChangeCheck();
            var inside = Field("Interior wall finish", plan.Data.InsideMaterial, house?.InsideMaterial);
            var outside = Field("Exterior wall finish", plan.Data.OutsideMaterial, house?.OutsideMaterial);
            return EditorGUI.EndChangeCheck() ? HouseFinishCommands.SetLevelWallFinishes(plan, inside, outside)
                : new HouseEditResult(HouseEditStatus.Unchanged);

            Material Field(string label, Material stored, Material inherited)
                => HousePropertyFields.InheritedMaterial(new GUIContent(label), stored,
                    stored != null ? stored : inherited, stored != null, out _, wallFinish: true);
        }

        internal static void DrawWallOverrides(HousePlan plan, FloorPlan.Wall[] walls)
        {
            walls = walls.Where(wall => wall.IsPhysical).ToArray();
            if (walls.Length == 0)
                return;
            foreach (var group in walls.GroupBy(wall => HouseFinishResolution.Family(plan, wall))
                .Where(group => group.Key != null && group.Key.HasMaterialRoles))
            {
                var family = group.Key;
                EditorGUILayout.LabelField(family.name + " materials", EditorStyles.boldLabel);
                foreach (var role in family.MaterialRoles.OrderBy(role => role.Slot))
                    DrawInherited(new GUIContent(role.Name, "Wall override. Reset restores the " + family.name + " family default."),
                        group.Select(wall => (wall.RoleMaterial(role.Id), family.RoleMaterial(wall, role.Slot))),
                        material => HouseFinishCommands.SetWallRole(plan, group.Select(wall => wall.Id), role.Id, material), plan, wallFinish: true);
                DrawFamilyDefaults(family, collapsible: true);
            }
            walls = walls.Where(wall => HouseFinishResolution.Family(plan, wall)?.HasMaterialRoles != true).ToArray();
            if (walls.Length == 0) return;
            foreach (bool left in new[]{true, false})
            {
                Material Get(FloorPlan.Wall wall) => left ? wall.LeftMaterial : wall.RightMaterial;
                string label = left ? "Left finish" : "Right finish";
                if (walls.Length == 1)
                {
                    var room = plan.Data.RoomOnSide(walls[0], left);
                    label = (left ? "Left" : "Right") + (room == null ? " (outside)" : " (room " + (plan.Data.Cells.ToList().IndexOf(room) + 1) + ")");
                }

                DrawInherited(new GUIContent(label, "Left/right follow the saved wall direction. Reset restores room/kit inheritance."),
                    walls.Select(w => (Get(w), HouseFinishResolution.Wall(plan, w, left))),
                    material => HouseEditCommands.SetWallFinishes(plan, walls.Select(w => w.Id), left, material), plan, wallFinish: true);
            }
        }

        private static void DrawFamilyDefaults(WallFamily family, bool collapsible)
        {
            if (collapsible)
            {
                int key = family.GetInstanceID();
                bool expanded = EditorGUILayout.Foldout(expandedFamilies.Contains(key), "Family defaults (shared)", true);
                if (expanded) expandedFamilies.Add(key); else expandedFamilies.Remove(key);
                if (!expanded) return;
            }
            else EditorGUILayout.LabelField(family.name + " defaults (shared)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Applies to every wall using this family unless overridden.", EditorStyles.wordWrappedMiniLabel);
            foreach (var role in family.MaterialRoles.OrderBy(role => role.Slot))
            {
                EditorGUI.BeginChangeCheck();
                var material = HousePropertyFields.Material(new GUIContent(role.Name, "Shared family default material assignment."), role.DefaultMaterial, wallFinish: true);
                if (EditorGUI.EndChangeCheck())
                {
                    var result = HouseFinishCommands.SetFamilyRole(family, role.Id, material);
                    if (result.Status == HouseEditStatus.Rejected) Debug.LogWarning(result.Message, family);
                }
            }
        }

        internal static void DrawRoomOverrides(HousePlan plan, FloorPlan.Cell[] rooms)
        {
            if (rooms.Length == 0) return;
            DrawInherited(new GUIContent("Wall finish", "Room default for facing walls; explicit wall-side exceptions remain."),
                rooms.SelectMany(room => RoomWallMaterials(plan, room).Select(material => (room.WallMaterial, material))),
                material => HouseEditCommands.SetRoomFinishes(plan, rooms.Select(room => room.Id), material), plan, wallFinish: true);
            foreach (bool ceiling in new[] { false, true })
            {
                Material Get(FloorPlan.Cell room) => ceiling ? room.CeilingMaterial : room.FloorMaterial;
                var building = HouseBuilding.For(plan);
                DrawInherited(new GUIContent(ceiling ? "Ceiling finish" : "Floor finish", "Reset restores the level/House default."),
                    rooms.Select(room => (Get(room), HouseFinishResolution.RenderMaterial(
                        HouseFinishResolution.RoomSurface(building, plan, room, ceiling)))),
                    material => HouseEditCommands.SetRoomSurfaceFinishes(plan, rooms.Select(room => room.Id), ceiling, material), plan);
            }
        }

        internal static Material[] RoomWallMaterials(HousePlan plan, FloorPlan.Cell room)
        {
            if (room.WallMaterial != null) return new[] { room.WallMaterial };
            var values = new List<Material>();
            foreach (var wall in plan.Data.Walls.Where(w => w.IsPhysical))
                foreach (bool left in new[] { true, false })
                    if (plan.Data.RoomOnSide(wall, left) == room)
                        values.Add(HouseFinishResolution.Wall(plan, wall, left, roomOnly: true));
            if (values.Count == 0)
            {
                values.Add(HouseFinishResolution.RoomWall(plan, room));
            }
            return values.Distinct().ToArray();
        }

        private static void DrawInherited(GUIContent label, IEnumerable<(Material Stored, Material Effective)> values,
            Func<Material, HouseEditResult> edit, HousePlan plan, bool wallFinish = false)
        {
            var samples = values.ToArray();
            bool any = samples.Any(v => v.Stored != null);
            EditorGUI.showMixedValue = samples.Any(v => v.Effective != samples[0].Effective);
            EditorGUI.BeginChangeCheck();
            var material = HousePropertyFields.InheritedMaterial(label, samples[0].Stored, samples[0].Effective, any, out _, wallFinish);
            bool changed = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;
            if (changed)
            {
                var result = edit(material);
                if (result.Status == HouseEditStatus.Rejected) Debug.LogWarning(result.Message, plan);
            }
        }

        internal static bool SetRoomFinishes(HousePlan plan, System.Collections.Generic.IEnumerable<string> ids, Material material)
            => HouseEditCommands.SetRoomFinishes(plan, ids, material).Applied;
    }
}
