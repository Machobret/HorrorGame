using System.Linq;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Stable input text, independent of output construction.
    internal static class HouseInputSignatures
    {
        internal static string WallOutputInputs(HousePlan plan, HouseWallBuilder builder, HouseGenerationData generation = null)
        {
            // Asset GUIDs survive save/reopen; Unity instance IDs do not. Output is deliberately
            // excluded, so creating renderers cannot trigger an endless regeneration loop.
            string MeshInput(Mesh mesh) => generation?.MeshSignature(mesh) ?? GeneratedMeshResources.SourceSignature(mesh);
            string finishes = string.Join("|", plan.Data.Cells.Select(room => room == null ? "missing-room"
                : room.Id + ":" + AssetIdentity(room.WallMaterial)))
                + string.Join("|", plan.Data.Walls.Select(wall => wall == null ? "missing-wall"
                    : wall.Id + ":" + AssetIdentity(wall.LeftMaterial) + ":" + AssetIdentity(wall.RightMaterial)));
            var effectiveTrims = plan.Data.Walls.Select(w => (Wall: w, Trim: HouseTrimLayout.Resolve(plan, w))).Where(v => v.Trim != null);
            string trims = string.Join("|", effectiveTrims.Select(v => v.Wall.Id
                + ":" + MeshInput(v.Trim.Mesh) + ":" + AssetIdentity(v.Trim.Material)
                + ":" + v.Trim.Height.ToString("R", CultureInfo.InvariantCulture) + ":" + v.Trim.Depth.ToString("R", CultureInfo.InvariantCulture)
                + ":" + v.Trim.Offset.ToString("R", CultureInfo.InvariantCulture) + ":" + v.Trim.PatternOffset.ToString("R", CultureInfo.InvariantCulture)));
            if (effectiveTrims.Any(v => v.Trim.Mesh != null)) trims += ":trim-centreline-v2";
            foreach (var definition in builder.Data.AllDefinitions(plan.Data))
            {
                if (definition == null) continue;
                trims += ":connection-seams:" + definition.ConnectionDepths.ToString("R");
            }
            // Wall groups now store geometry relative to their placement origin.
            // Existing saved output is revisited by the next explicit rebuild/edit.
            string data = "wall-side-output-v22" + PlanWallGeometryInputs(plan.Data) + finishes + trims
                + AssetIdentity(plan.Data.OutsideMaterial) + AssetIdentity(plan.Data.InsideMaterial)
                + AssetIdentity(HouseBuilding.For(plan)?.Data.InsideMaterial) + AssetIdentity(HouseBuilding.For(plan)?.Data.OutsideMaterial)
                + KitValiditySignature(builder) + builder.Data.ShowWalls + plan.HasSupportedTransform + plan.gameObject.layer;
            data += WallSourceInputs(builder.Data, plan.Data, true, generation);
            return Hash128.Compute(data).ToString();
        }

        // Only inputs interpreted by wall fitting/collision belong here. Floor finishes
        // and native material references must not invalidate an otherwise identical wall.
        internal static string PlanWallGeometryInputs(FloorPlan plan)
        {
            var input = new StringBuilder("plan-wall-geometry-v1");
            void Text(string value) => input.Append(value?.Length ?? -1).Append(':').Append(value).Append('|');
            void Number(float value) => Text(value.ToString("R", CultureInfo.InvariantCulture));
            if (plan == null) return "missing-plan";
            foreach (var corner in plan.Corners)
            {
                if (corner == null) { Text("missing-corner"); continue; }
                Text(corner.Id); Number(corner.Position.x); Number(corner.Position.y);
            }
            Text("walls");
            foreach (var wall in plan.Walls)
            {
                if (wall == null) { Text("missing-wall"); continue; }
                Text(wall.Id); Text(wall.StartId); Text(wall.EndId); Text(wall.Type.ToString()); Text(wall.Presence.ToString());
                if (wall.Family != null) Text(AssetIdentity(wall.Family));
                foreach (var opening in wall.Openings)
                {
                    if (opening == null) { Text("missing-opening"); continue; }
                    Text(opening.Id); Text(opening.Kind.ToString()); Number(opening.Distance); Number(opening.Width);
                    if (!string.IsNullOrEmpty(opening.Shape))
                    { Text(opening.Shape); }
                }
                Text("wall-end");
            }
            Text("rooms");
            foreach (var room in plan.Cells)
            {
                if (room == null) { Text("missing-room"); continue; }
                Text(room.Id); Text(room.StartCornerId); Text(room.IsOutdoor.ToString());
                foreach (var wallId in room.WallIds) Text(wallId);
                Text("room-end");
            }
            return Hash128.Compute(input.ToString()).ToString();
        }

        internal static string KitValiditySignature(HouseWallBuilder builder)
            => KitValiditySignature(builder == null ? null : builder.Data);

        internal static string WallSourceInputs(WallKit kit, FloorPlan plan, bool appearance = false, HouseGenerationData data = null)
        {
            if (kit == null) return "";
            // Source lists grow with the family/opening count. Write the same
            // ordered text once instead of copying the whole prefix per item.
            var inputs = new StringBuilder();
            if (kit.DefaultFamily != null) inputs.Append(":default-family:").Append(AssetIdentity(kit.DefaultFamily));
            if (kit.ExteriorFamily != null) inputs.Append(":exterior-family:").Append(AssetIdentity(kit.ExteriorFamily));
            foreach (var family in kit.FamilyDefinitions(plan))
            {
                foreach (var piece in family.Definitions())
                    inputs.Append(":complete:").Append(data?.KitSignature(piece) ?? KitSignature(piece));
                foreach (var opening in family.Openings ?? System.Array.Empty<WallFamily.Opening>())
                    if (opening != null) inputs.Append(":complete-opening:").Append(opening.Kind.ToString()).Append(':').Append(AssetIdentity(opening.Definition));
                inputs.Append(":family:").Append(AssetIdentity(family)).Append(':').Append(family.Id);
            }
            // Catalog membership affects the selector only. Render inputs track placed models;
            // wall fitting and collision do not interpret model geometry or finishes.
            if (appearance && plan != null)
            {
                // Distinct source dependencies alone cannot detect A,A,B becoming A,B,B.
                foreach (var opening in plan.Walls.SelectMany(w => w.Openings))
                    inputs.Append(":placed-model:").Append(opening.Id).Append(':').Append(AssetIdentity(opening.Insert));
                foreach (var insert in plan.Walls.SelectMany(w => w.Openings).Select(o => o.Insert).Where(i => i != null).Distinct())
                    inputs.Append(":insert:").Append(AssetIdentity(insert)).Append(':').Append(insert.Shape)
                        .Append(':').Append(insert.Kind.ToString()).Append(':').Append(insert.SegmentWidth.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':').Append(JsonUtility.ToJson(insert.Opening)).Append(':').Append(insert.MinimumWallThickness.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':').Append(insert.MaximumWallThickness.ToString("R", CultureInfo.InvariantCulture))
                        .Append(':').Append(data?.MeshSignature(insert.Mesh) ?? GeneratedMeshResources.SourceSignature(insert.Mesh)).Append(':').Append(AssetIdentity(insert.Material)).Append(':').Append(AssetIdentity(insert.GlassMaterial)).Append(':').Append(AssetIdentity(insert.Prefab));
            }
            return inputs.ToString();
        }

        internal static string KitValiditySignature(WallKit kit)
        {
            if (kit == null) return "no-builder";
            // Geometry-group identities stay unchanged. This header only makes invalid
            // in-memory meshes or missing required materials re-enter validation.
            bool valid = HouseWallLayout.ValidateKit(kit, out string problem);
            var signature = new StringBuilder();
            signature.Append(valid.ToString()).Append(':').Append(problem);
            foreach (var definition in kit.AllDefinitions())
            {
                if (definition == null) signature.Append("|none");
                else
                {
                    bool definitionValid = definition.IsValid(out string reason);
                    signature.Append('|').Append(definitionValid.ToString()).Append(':').Append(reason);
                }
            }
            return signature.ToString();
        }

        // Generated geometry depends on dimensions, source meshes and apertures, never materials.
        internal static string KitSignature(WallModuleDefinition definition, HouseGenerationData data = null)
        {
            if (definition == null) return "none";
            return AssetIdentity(definition) + ":"
                + ("complete:" + definition.FamilyId + ":" + definition.OpeningShape + ":")
                + definition.Width.ToString("R", CultureInfo.InvariantCulture) + ":"
                + definition.Height.ToString("R", CultureInfo.InvariantCulture) + ":"
                + definition.Thickness.ToString("R", CultureInfo.InvariantCulture) + ":"
                + definition.AllowLengthStretch + ":"
                + (data?.MeshSignature(definition.Body) ?? GeneratedMeshResources.SourceSignature(definition.Body)) + ":"
                + "no-mesh"
                // Keep the current key layout, including unused source tokens, so
                // removing schema fields cannot replace otherwise unchanged painted bodies.
                + ":" + JsonUtility.ToJson(definition.OpeningOutline) + ":none";
        }

        internal static string AssetIdentity(UnityEngine.Object asset)
        {
            if (asset == null)
                return "none";
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                return guid + ":" + localId;
            return "unsaved:" + asset.GetInstanceID();
        }

        // Walls and collision share heights/topology, not other levels' finishes.
        // Read accepted records directly: computing this stamp must not derive slabs.
        internal static Vector2 LocalShapePoint(Vector2 point)
        {
            // Subtracting translated floats introduces tiny differences. Shape keys
            // use 0.1 mm (below plan tolerance); root placement remains exact.
            return new Vector2(Mathf.Round(point.x * 10000) / 10000, Mathf.Round(point.y * 10000) / 10000);
        }

        internal static string SharedWallHeightInputs(HouseBuilding building, HousePlan plan = null)
        {
            if (building == null) return "";
            building.TryValidateLayout(out string structureProblem);
            var input = new StringBuilder("shared-wall-heights-v4");
            var source = plan != null ? building.Find(plan) : null;
            var above = source != null ? building.Above(source) : null;
            void Text(string value) => input.Append(value?.Length ?? -1).Append(':').Append(value).Append('|');
            void Number(float value) => Text(value.ToString("R", CultureInfo.InvariantCulture));
            void Point(Vector2 value) { Number(value.x); Number(value.y); }
            Text(structureProblem); Text(building.Data.Anchor?.Id); Number(building.SlabThickness);
            foreach (var level in building.Levels)
            {
                if (level == null) { Text("missing-level"); continue; }
                // A wall reads its own storey and the coverage immediately above.
                // Slab output omits this scope and retains the complete stack stamp.
                if (plan != null && level != source && level.Id != above?.UpperId) continue;
                Text(level.Id); Number(level.Span);
                if (level.Plan == null) { Text("missing-plan"); continue; }
                var pose = level.Plan.transform;
                Text(pose.localPosition.ToString("R")); Text(pose.localRotation.ToString("R")); Text(pose.localScale.ToString("R"));
                Text(level.Plan.HasSupportedTransform.ToString());
                var floor = level.Plan.Data;
                Text("corners");
                foreach (var corner in floor.Corners)
                { if (corner == null) { Text("missing-corner"); continue; } Text(corner.Id); Point(corner.Position); }
                Text("walls");
                foreach (var wall in floor.Walls)
                { if (wall == null) { Text("missing-wall"); continue; } Text(wall.Id); Text(wall.StartId); Text(wall.EndId); }
                Text("rooms");
                foreach (var room in floor.Cells)
                {
                    if (room == null) { Text("missing-room"); continue; }
                    Text(room.Id); Text(room.StartCornerId); Text(room.IsOutdoor.ToString());
                    foreach (var wallId in room.WallIds) Text(wallId);
                    Text("room-end");
                }
                Text("level-end");
            }
            Text("boundaries");
            foreach (var boundary in building.Boundaries)
            {
                if (boundary == null) { Text("missing-boundary"); continue; }
                if (plan != null && boundary != above) continue;
                Text(boundary.Id); Text(boundary.LowerId); Text(boundary.UpperId);
                Text("nodes");
                foreach (var node in boundary.DividerNodes)
                { if (node == null) { Text("missing-node"); continue; } Text(node.Id); Point(node.Position); }
                Text("dividers");
                foreach (var edge in boundary.Dividers)
                { if (edge == null) { Text("missing-divider"); continue; } Text(edge.Id); Text(edge.StartId); Text(edge.EndId); }
                Text("segments");
                foreach (var segment in boundary.Regions)
                { if (segment == null) { Text("missing-segment"); continue; } Text(segment.Id); Text(segment.FaceKey); }
                Text("boundary-end");
            }
            return Hash128.Compute(input.ToString()).ToString();
        }

        internal static string SlabOutputInputs(HouseBuilding building, HouseGenerationData data = null)
        {
            if (building == null) return "";
            // Native Plan/Content references are not geometry inputs. Serializing them
            // embeds transient instance IDs and spuriously rebuilds a reopened scene.
            var input = new StringBuilder("slab-inputs-v3");
            void Text(string value) => input.Append(value?.Length ?? -1).Append(':').Append(value).Append('|');
            void Number(float value) => Text(value.ToString("R", CultureInfo.InvariantCulture));
            void Point(Vector2 value) { Number(value.x); Number(value.y); }
            Text(data?.SharedWallHeightInputs ?? SharedWallHeightInputs(building));
            Number(building.FloorThickness); Number(building.CeilingThickness);
            Text(AssetIdentity(building.Data.FloorMaterial)); Text(AssetIdentity(building.CeilingMaterial)); Point(building.CeilingTextureSize);
            Text(building.gameObject.layer.ToString());
            foreach (var boundary in building.Boundaries)
            {
                if (boundary == null) continue; // The shared stamp retains the structural diagnostic.
                Text(boundary.Id);
                foreach (var opening in boundary.Openings)
                {
                    if (opening == null) { Text("missing-opening"); continue; }
                    Text(opening.Id);
                    foreach (var point in opening.Points) Point(point);
                    Text("opening-end");
                }
                foreach (var segment in boundary.Regions)
                {
                    if (segment == null) continue;
                    Text(segment.Id); Text(segment.Ceiling.ToString()); Text(segment.OverrideMaterial.ToString());
                    Text(segment.CutAway.ToString()); Text(AssetIdentity(segment.Material));
                }
                Text("boundary-end");
            }
            foreach (var level in building.Levels)
            {
                if (level?.Plan == null) continue;
                var plan = level.Plan;
                Text(level.Id); Text(plan.HasSupportedTransform.ToString()); Text(plan.gameObject.layer.ToString());
                Text(plan.Data.ShowFloors.ToString()); Text(plan.Data.GenerateCollision.ToString());
                Text(AssetIdentity(plan.Data.CeilingMaterial)); Text(AssetIdentity(plan.Data.FloorMaterial)); Point(plan.Data.FloorTextureSize);
                foreach (var room in plan.Data.Cells)
                {
                    if (room == null) continue;
                    Text(room.Id); Text(AssetIdentity(room.FloorMaterial)); Text(AssetIdentity(room.CeilingMaterial));
                }
                Text("level-end");
            }
            return Hash128.Compute(input.ToString()).ToString();
        }

    }
}
