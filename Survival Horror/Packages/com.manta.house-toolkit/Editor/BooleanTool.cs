using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static class BooleanTool
    {
        static string selected;
        static BooleanCut hovered;
        static BooleanCut draft;
        static CutShape nextShape;
        static bool placeAnother;
        static bool selectBooleans = EditorPrefs.GetBool("HouseToolkit.SelectBooleans", false);
        internal static bool SelectBooleans
        {
            get => selectBooleans;
            set
            {
                if (selectBooleans == value) return;
                selectBooleans = value;
                EditorPrefs.SetBool("HouseToolkit.SelectBooleans", value);
                if (HouseScene.Mode == HouseTool.Select) Reset();
                HouseScene.Repaint();
            }
        }
        internal static bool IsPlacing => HouseScene.Mode == HouseTool.Boolean && HouseScene.House &&
            (placeAnother || HouseScene.House.booleanCuts.Count == 0);
        static readonly BooleanCut preview = new BooleanCut();
        static bool showHelpers = true;
        internal static BooleanCut Current => HouseScene.House?.booleanCuts.Find(c => c.id == selected);
        internal static BooleanCut DisplayedSelection => draft ?? Current;
        static SceneView dragView;
        static int bodyControl;
        static Plane dragPlane;
        static Vector3 pressPoint;
        public static void Reset()
        {
            CancelDrag();
            selected = null;
            hovered = null;
            placeAnother = false;
        }
        internal static void SetActive(string id)
        {
            CancelDrag();
            selected=id;placeAnother=false;hovered=null;
        }
        public static void CancelDrag()
        {
            if (bodyControl != 0 && GUIUtility.hotControl == bodyControl) GUIUtility.hotControl = 0;
            bodyControl = 0; dragView = null; draft = null;
        }
        static void Commit(BooleanCut cut, string remove = null)
        {
            if (BooleanOutput.Change(HouseScene.House, cut, remove, out HouseScene.Message))
            {
                selected = cut?.id;
                if (cut==null && HouseScene.Picks.Active.cutter==remove) HouseScene.Select(default);
                placeAnother = false;
            }
            CancelDrag();
            HouseScene.Repaint();
        }

        internal static void CutMaterialField(House house)
        {
            EditorGUI.BeginChangeCheck();
            var material = (Material)EditorGUILayout.ObjectField("Cut face material", house.cutMaterial, typeof(Material), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(house, "Change cut face material");
                house.cutMaterial = material;
                foreach (var state in house.GetComponentsInChildren<BooleanCutResult>(true))
                {
                    if (!state.TryGetComponent<MeshRenderer>(out var display)) continue;
                    Undo.RecordObject(display, "Change cut face material");
                    BooleanOutput.RefreshMaterials(house, state);
                }
                EditorUtility.SetDirty(house);
            }
        }

        internal static void ToolProperties()
        {
            var house = HouseScene.House;
            bool placementTool = HouseScene.Mode == HouseTool.Boolean;
            if (placementTool) showHelpers = EditorGUILayout.Toggle("Show cutters", showHelpers);
            else
            {
                SelectBooleans = EditorGUILayout.ToggleLeft("Select booleans", SelectBooleans);
                if (!SelectBooleans) return;
            }

            if (house.booleanCuts.Count > 0)
            {
                var labels = new[]{"Choose a cutter"}.Concat(house.booleanCuts.Select((c,i) => $"{i+1}. {c.shape}")).ToArray();
                int index = house.booleanCuts.IndexOf(Current) + 1;
                int choice = EditorGUILayout.Popup("Existing cutter", index, labels);
                if (choice != index)
                {
                    if (choice == 0) Reset();
                    else SelectCut(house.booleanCuts[choice-1]);
                    HouseScene.Repaint();
                }
            }
            if (!placementTool) return;
            if (!IsPlacing && GUILayout.Button("Place new cut"))
            {
                CancelDrag(); selected = null; placeAnother = true; HouseScene.Repaint();
            }
            if (IsPlacing)
            {
                nextShape = (CutShape)EditorGUILayout.EnumPopup("Shape", nextShape);
                if (house.booleanCuts.Count > 0 && GUILayout.Button("Cancel placement")) { Reset(); HouseScene.Repaint(); }
            }
        }

        internal static void SelectCut(BooleanCut cut)
        {
            CancelDrag();
            HouseScene.ClearArchitectureHover();
            HouseScene.Pick(new HousePick{cutter=cut.id},false);
            selected = cut.id;
            placeAnother = false;
            HouseProperties.ShowSelection();
        }

        internal static void SelectionProperties()
        {
            var cut = Current;
            if (cut == null) return;
            EditorGUILayout.LabelField("Boolean cut", EditorStyles.boldLabel);
            var change = cut.Copy();
            EditorGUI.BeginChangeCheck();
            change.shape = (CutShape)EditorGUILayout.EnumPopup("Shape", change.shape);
            change.center = VectorField("Location (m)", change.center);
            var angles = VectorField("Rotation", change.rotation.eulerAngles);
            change.size = VectorField("Size (m)", change.size);
            if (EditorGUI.EndChangeCheck()) { change.rotation = Quaternion.Euler(angles); Commit(change); }
            float factor = EditorGUILayout.DelayedFloatField(new GUIContent("Uniform scale",
                "Multiply all three dimensions by this factor."), 1f);
            if (factor != 1f)
            {
                var scaled = cut.Copy();
                scaled.size *= factor;
                Commit(scaled);
            }
            if (GUILayout.Button("Delete cut")) Commit(null, cut.id);
            EditorGUILayout.HelpBox("Drag the cutter to move it. W, E and R switch the transform handle. Drag the centre of the scale handle for uniform scaling.", MessageType.None);
        }

        static Vector3 VectorField(string label, Vector3 value)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
            float width = (rect.width - 6) / 3;
            for (int i = 0; i < 3; i++)
                value[i] = EditorGUI.DelayedFloatField(new Rect(rect.x + i * (width + 3), rect.y, width, rect.height), value[i]);
            return value;
        }

        internal static void Scene(SceneView view, Event e, Ray ray, int control)
        {
            var house = HouseScene.House;
            var cut = Current;
            bool deleteCommand = e.commandName == "Delete" || e.commandName == "SoftDelete";
            if (cut != null && deleteCommand && e.type == EventType.ValidateCommand) { e.Use(); return; }
            if ((e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)) ||
                (deleteCommand && e.type == EventType.ExecuteCommand))
            {
                if (HouseScene.Picks.Items.Count>1) return;
                if (cut != null) { Commit(null, cut.id); e.Use(); }
                return;
            }
            if (dragView && dragView != view) return;
            if (e.type == EventType.Ignore) CancelDrag();
            if (bodyControl != 0 && cut != null)
            {
                if (e.rawType == EventType.MouseDrag && e.button == 0 && dragPlane.Raycast(ray, out var depthOnPlane))
                {
                    draft ??= cut.Copy();
                    draft.center = cut.center + house.transform.InverseTransformVector(ray.GetPoint(depthOnPlane) - pressPoint);
                    e.Use(); view.Repaint();
                }
                if (e.rawType == EventType.MouseUp && e.button == 0)
                {
                    if (draft != null) Commit(draft); else CancelDrag();
                    e.Use(); return;
                }
            }
            foreach (var item in house.booleanCuts)
                if (e.type == EventType.Repaint && (HouseScene.Mode == HouseTool.Select || showHelpers || item == cut))
                    Wire(house, item == cut && draft != null ? draft : item,
                        (item == cut || HouseScene.Picks.Has(new HousePick{cutter=item.id})) ? Color.yellow : item == hovered ? Color.white : Color.cyan);
            if (cut != null && HouseScene.Picks.Items.Count<=1 && !e.shift && !e.alt && bodyControl == 0)
            {
                bool release = e.rawType == EventType.MouseUp && e.button == 0;
                var current = draft ?? cut;
                var position = house.transform.TransformPoint(current.center);
                var rotation = house.transform.rotation * current.rotation;
                var size = current.size;
                EditorGUI.BeginChangeCheck();
                if (Tools.current == Tool.Rotate)
                    rotation = Handles.RotationHandle(rotation, position);
                else if (Tools.current == Tool.Scale)
                    size = Handles.ScaleHandle(size, position, rotation, HandleUtility.GetHandleSize(position));
                else
                    position = Handles.PositionHandle(position, Tools.pivotRotation == PivotRotation.Global ? Quaternion.identity : rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    dragView = view;
                    draft ??= cut.Copy();
                    draft.center = house.transform.InverseTransformPoint(position);
                    draft.rotation = Quaternion.Inverse(house.transform.rotation) * rotation;
                    draft.size = size;
                    view.Repaint();
                }
                if (draft != null && release) { Commit(draft); return; }
            }
            if (e.alt || Tools.viewToolActive || e.type == EventType.Used) return;
            if (e.type == EventType.MouseLeaveWindow) hovered = null;
            if (e.type == EventType.MouseMove)
            {
                hovered = IsPlacing ? null : Pick(house, ray);
                if (hovered != null && HouseScene.Mode == HouseTool.Select)
                {
                    HouseScene.ClearArchitectureHover();
                    e.Use();
                }
                view.Repaint();
                return;
            }
            bool press = e.type == EventType.MouseDown && e.button == 0 &&
                GUIUtility.hotControl == 0 && HandleUtility.nearestControl == control;
            if (IsPlacing)
            {
                if (!press && e.type != EventType.Repaint) return;
                var plane = new Plane(HouseScene.Floor.transform.up, HouseScene.Floor.transform.position);
                bool onPlane = plane.Raycast(ray, out var depth);
                var surface = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.GetComponentInParent<House>() == house &&
                        !SceneVisibilityManager.instance.IsHidden(h.collider.gameObject)).OrderBy(h => h.distance).FirstOrDefault();
                if (!surface.collider && !onPlane) return;
                preview.shape = nextShape;
                preview.center = house.transform.InverseTransformPoint(surface.collider ? surface.point : ray.GetPoint(depth));
                if (!press) { Wire(house, preview, Color.green); return; }
                var placed = preview.Copy();
                placed.id = System.Guid.NewGuid().ToString("N");
                Commit(placed);
                HouseProperties.ShowSelection();
            }
            else if (press)
            {
                var picked = Pick(house, ray);
                if (picked == null)
                {
                    if (cut != null && !e.shift) { Reset(); HouseScene.Repaint(); }
                    return;
                }
                if(e.shift)
                {
                    HouseScene.Pick(new HousePick{cutter=picked.id},true);
                    HouseProperties.ShowSelection();
                    e.Use();return;
                }
                SelectCut(picked);
                dragPlane = new Plane(view.camera.transform.forward, house.transform.TransformPoint(picked.center));
                if (dragPlane.Raycast(ray, out float entry))
                {
                    pressPoint = ray.GetPoint(entry);
                    dragView = view; bodyControl = control; GUIUtility.hotControl = control;
                }
            }
            else return;
            e.Use(); HouseScene.Repaint();
        }

        static BooleanCut Pick(House house, Ray ray)
        {
            BooleanCut picked = null;
            float closest = float.PositiveInfinity;
            var selectedCut = Current;
            foreach (var item in house.booleanCuts)
            {
                if (HouseScene.Mode == HouseTool.Boolean && !showHelpers && item != selectedCut) continue;
                var matrix = house.transform.localToWorldMatrix * item.Matrix;
                var inverse = matrix.inverse;
                var localRay = new Ray(inverse.MultiplyPoint3x4(ray.origin), inverse.MultiplyVector(ray.direction));
                bool hit = item.shape == CutShape.Box
                    ? new Bounds(Vector3.zero, Vector3.one).IntersectRay(localRay, out float d)
                    : HouseBooleanMesh.RaycastIcosphere(localRay, out d);
                if (!hit) continue;
                float distance = (matrix.MultiplyPoint3x4(localRay.GetPoint(d)) - ray.origin).sqrMagnitude;
                if (distance < closest) { closest = distance; picked = item; }
            }
            return picked;
        }

        static void Wire(House house, BooleanCut cut, Color color)
        {
            using (new Handles.DrawingScope(color, house.transform.localToWorldMatrix * cut.Matrix))
                if (cut.shape == CutShape.Box) Handles.DrawWireCube(Vector3.zero, Vector3.one);
                else Handles.DrawLines(HouseBooleanMesh.IcosphereEdges);
        }
    }
}
