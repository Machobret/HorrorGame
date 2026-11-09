using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace EnvironmentAuthoring.Editor
{
    [CustomEditor(typeof(HousePlan))]
    public sealed class HousePlanEditor : UnityEditor.Editor
    {
        // Unity's native F command asks active editors for semantic selection bounds.
        private bool HasFrameBounds() => HouseEditingContext.SelectedWallFrameBounds((HousePlan)target, out _);
        private Bounds OnGetFrameBounds() { HouseEditingContext.SelectedWallFrameBounds((HousePlan)target, out var bounds); return bounds; }

        [MenuItem("GameObject/Environment/House", false, 10)]
        private static void CreateHouse() => EnvironmentCreateTool.Arm(EnvironmentCreateCommands.Kind.House, SceneView.lastActiveSceneView);

        public override void OnInspectorGUI()
        {
            var plan = (HousePlan)target;
            EditorGUILayout.LabelField("House outline", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{plan.Data.Corners.Count} corners / {plan.Data.Walls.Count} walls");
            if (plan.Data.IsClosed)
            {
                EditorGUILayout.LabelField($"{plan.Data.Cells.Count} cell(s)");
                for (int i = 0; i < plan.Data.Cells.Count; i++)
                    EditorGUILayout.LabelField($"Cell {i + 1} / {Math.Abs(OutlineGeometry.SignedArea(plan.Data.GetCellBoundary(plan.Data.Cells[i]))):0.##} m\u00B2");
            }
            else
                EditorGUILayout.LabelField("Open walls / no room");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Floors", EditorStyles.boldLabel);
            bool needsSceneInstance = HouseRebuildCoordinator.NeedsSceneInstance(plan);
            using (new EditorGUI.DisabledScope(needsSceneInstance || EditorApplication.isPlayingOrWillChangePlaymode || !HouseEditorSession.instance.CanEditSource))
            {
                EditorGUI.BeginChangeCheck();
                bool show = EditorGUILayout.Toggle("Show floors", plan.Data.ShowFloors);
                var material = (Material)EditorGUILayout.ObjectField("Floor material", plan.Data.FloorMaterial, typeof(Material), false);
                var repeat = new Vector2(EditorGUILayout.DelayedFloatField("Repeat width (m)", plan.Data.FloorTextureSize.x),
                    EditorGUILayout.DelayedFloatField("Repeat depth (m)", plan.Data.FloorTextureSize.y));
                bool collisionEnabled = EditorGUILayout.Toggle("Generate collision", plan.Data.GenerateCollision);
                if (EditorGUI.EndChangeCheck())
                    HouseEditCommands.SetFloorSettings(plan, show, material, repeat, collisionEnabled);
            }
            if (needsSceneInstance)
                EditorGUILayout.HelpBox("Generated floors currently save in scenes only. Unpack this House instance to generate its floors; prefab mesh baking is not supported yet.", MessageType.Info);
            var collision = HouseCollisionBuilder.Output(plan);
            if (collision != null && !string.IsNullOrEmpty(collision.Problem))
                EditorGUILayout.HelpBox(collision.Problem, MessageType.Warning);
            var slabs = HouseBuilding.For(plan) is HouseBuilding house ? HouseSlabBuilder.Output(house) : null;
            if (slabs != null && !string.IsNullOrEmpty(slabs.Problem))
                EditorGUILayout.HelpBox(slabs.Problem, MessageType.Warning);

            EditorGUILayout.HelpBox("Draw walls; closing a loop creates a room. Extend outside existing rooms to add more. "
                + "Wall chains segments and creates rooms automatically; Delete removes the targeted authored element. "
                + "Door and Window preview openings before placement. Select moves nodes, walls and openings; drag empty space to box-select nodes. "
                + "Save the scene to keep your work.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode
                || Selection.gameObjects.Length != 1))
            {
                if (GUILayout.Button("Edit House"))
                {
                    HouseEditingContext.Enter(plan);
                }
                if (GUILayout.Button("Top View"))
                    ShowTopView(plan);
                if (plan.GetComponent<HouseWallBuilder>() == null && GUILayout.Button("Enable 3D Walls"))
                    HouseWallBuilderEditor.Enable(plan);
            }
            if (HouseEditingContext.Active?.Session != null)
                EditorGUILayout.HelpBox("Selection properties are in the House Properties window's Selection tab.", MessageType.None);
            if (!plan.HasSupportedTransform)
                EditorGUILayout.HelpBox("Keep the House upright with world scale (1, 1, 1). "
                    + "Moving it and rotating around Y are supported.", MessageType.Warning);
        }

        private void OnSceneGUI()
        {
            if (HouseEditingContext.Active?.Session == null)
                HousePlanSceneDrawing.DrawPlan((HousePlan)target);
        }

        internal static void ShowTopView(HousePlan plan, bool frame = false)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null)
                view = EditorWindow.GetWindow<SceneView>();
            if (HouseEditingContext.Active != null && !frame)
            {
                HouseEditingContext.Active.Camera.Toggle(view);
                return;
            }
            var bounds = new Bounds(plan.transform.position, Vector3.zero);
            foreach (var corner in plan.Data.Corners)
                bounds.Encapsulate(HouseSceneQueries.NodeWorld(plan, corner.Position));
            view.LookAtDirect(bounds.center, view.rotation, Mathf.Max(5f, bounds.extents.magnitude * 1.5f));
            if (HouseEditingContext.Active != null) HouseEditingContext.Active.Camera.Toggle(view);
            else
            {
                view.orthographic = true;
                view.LookAtDirect(bounds.center, Quaternion.Euler(90f, 0f, 0f), view.size);
            }
            view.Focus();
        }

        [DrawGizmo(GizmoType.NonSelected)]
        private static void DrawUnselected(HousePlan plan, GizmoType gizmoType)
        {
            if (HouseLevelView.HiddenLevel(plan) || (HouseEditingContext.Active != null && !HouseEditingContext.Active.Contains(plan.gameObject))) return;
            Color previous = Gizmos.color;
            Gizmos.color = new Color(0.25f, 0.8f, 0.85f);
            foreach (var wall in plan.Data.Walls)
                Gizmos.DrawLine(HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.StartId)), HouseSceneQueries.NodeWorld(plan, plan.Data.GetPosition(wall.EndId)));
            Gizmos.color = previous;
        }
    }

}
