using System.Text;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Snapshots of exactly the source inputs read by geometry gestures.
    internal static class HouseEditInputs
    {
        // Equality proves an unchanged horizontal fit, not permission to reuse vertical
        // checks. Live asset validity also catches mesh edits without new object IDs.
        internal static string WallFitInputs(FloorPlan plan, WallKit kit)
        {
            // Physical runs compare definition references. Finishes and trim settings
            // do not affect that fit; mesh headers and dimensions still participate in validity.
            return HouseInputSignatures.PlanWallGeometryInputs(plan) + ":kit:"
                + HouseInputSignatures.KitValiditySignature(kit)
                + HouseInputSignatures.WallSourceInputs(kit, plan);
        }

        internal static string SourceInputs(HousePlan plan)
        {
            var building = HouseBuilding.For(plan);
            return building == null ? PlanInputs(plan) : SourceInputs(building);
        }

        internal static string SourceInputs(HouseBuilding building)
        {
            var inputs = new StringBuilder();
            inputs.Append(JsonUtility.ToJson(building)).Append(":owner:").Append(building.GetInstanceID())
                .Append(":layer:").Append(building.gameObject.layer).Append(building.transform.position.ToString("R"))
                .Append(building.transform.rotation.ToString("R")).Append(building.transform.lossyScale.ToString("R"));
            bool separator = false;
            foreach (var level in building.Levels)
            {
                if (level == null || level.Plan == null) continue;
                if (separator) inputs.Append('|');
                AppendPlanInputs(inputs, level.Plan);
                separator = true;
            }
            return inputs.ToString();
        }

        internal static string PlanInputs(HousePlan plan)
        {
            var inputs = new StringBuilder();
            AppendPlanInputs(inputs, plan);
            return inputs.ToString();
        }

        private static void AppendPlanInputs(StringBuilder inputs, HousePlan plan)
        {
            var builder = plan.GetComponent<HouseWallBuilder>();
            var parent = plan.transform.parent;
            // One writer keeps the exact guard text without copying each level's
            // complete JSON into another intermediate House-sized string.
            inputs.Append(JsonUtility.ToJson(plan)).Append(":parent:").Append(parent != null ? parent.GetInstanceID() : 0)
                .Append(":scene:").Append(plan.gameObject.scene.handle).Append(":layer:").Append(plan.gameObject.layer)
                .Append(plan.transform.position.ToString("R")).Append(plan.transform.rotation.ToString("R"))
                .Append(plan.transform.lossyScale.ToString("R"));
            if (builder == null) return;
            inputs.Append(JsonUtility.ToJson(builder)).Append(HouseInputSignatures.WallOutputInputs(plan, builder));
            bool separator = false;
            foreach (var definition in builder.Data.AllDefinitions(plan.Data))
            {
                if (separator) inputs.Append('|');
                inputs.Append(definition == null ? "none" : JsonUtility.ToJson(definition));
                separator = true;
            }
        }
    }
}
