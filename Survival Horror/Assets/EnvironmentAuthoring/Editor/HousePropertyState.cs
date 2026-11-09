using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    // Views compare displayed state without owning a copy of the authoring model.
    // The window and ruler share this stamp; neither depends on the other's lifetime.
    internal static class HousePropertyState
    {
        private static string SessionStamp(HouseEditorSession session) => session == null ? "" :
            session.ActiveMode + ":" + session.SnapToGrid + ":" + session.GridSize + ":"
            + JsonUtility.ToJson(session.Selection) + ":" + session.SlabCeiling + ":" + session.SlabPolygon;

        internal static string Stamp(HousePlan plan, HouseBuilding building)
        {
            if (plan == null) return "";
            var context = HouseEditingContext.Active;
            var builder = plan.GetComponent<HouseWallBuilder>();
            return plan.GetInstanceID() + ":" + plan.name + ":" + JsonUtility.ToJson(plan)
                + ":" + JsonUtility.ToJson(building) + ":" + SessionStamp(context?.Session)
                + ":" + context?.Session?.ContextRevision + ":" + context?.Controller.HasSelectionGesture
                + ":" + plan.HasSupportedTransform + ":" + HouseRebuildCoordinator.NeedsSceneInstance(plan)
                + ":" + JsonUtility.ToJson(builder) + ":" + builder?.Data.ReferenceBlank?.Height
                + ":" + builder?.Output?.InputSignature + ":" + (building != null ? HouseSlabBuilder.Output(building)?.Signature : "")
                + ":" + HouseEditorSession.instance.Pending.DisplayRevision;
        }
    }
}
