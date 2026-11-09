using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnvironmentAuthoring.Editor
{
    // A transparent indicator of the active level and its immediate neighbours.
    // They fit using one shared metre scale, without scrolling through the building.
    internal sealed class HouseMetricRuler
    {
        internal readonly VisualElement Root = new VisualElement { name = "house-metric-ruler" };
        internal readonly VisualElement Diagram = new VisualElement { name = "house-ruler-diagram" };
        internal HouseMetricRulerData Data { get; private set; }
        private readonly SceneView view;
        private string stamp;
        private HouseMetricRulerData.Band hovered;
        private static readonly Color Selected = new Color(.28f, .68f, .9f);
        private Color Ink => EditorGUIUtility.isProSkin ? new Color(.88f, .9f, .88f) : new Color(.15f, .18f, .15f);

        internal HouseMetricRuler(SceneView view)
        {
            this.view = view;
            Root.userData = this;
            Root.style.position = Position.Absolute;
            Root.style.right = 12; Root.style.top = 200; Root.style.width = 82;
            Root.style.backgroundColor = Color.clear;
            Diagram.style.width = 82; Diagram.style.flexShrink = 0;
            Diagram.generateVisualContent += Draw;
            Root.Add(Diagram);
            Diagram.RegisterCallback<PointerMoveEvent>(e => Hover(e.localPosition));
            Diagram.RegisterCallback<PointerLeaveEvent>(_ => { hovered = null; Diagram.tooltip = ""; Diagram.MarkDirtyRepaint(); });
            Diagram.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                var band = Data?.HitBand(e.localPosition.y);
                if (band != null && e.localPosition.x >= 48) Select(band);
                e.StopPropagation();
            });
            // No wheel/click in the ruler can reach the Scene's geometry/camera handlers.
            Root.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            Root.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
            Root.RegisterCallback<WheelEvent>(e => e.StopPropagation());
            HouseSceneUI.Watch(Root, Refresh);
        }

        internal void Refresh()
        {
            var session = HouseEditingContext.Active?.Session;
            var plan = session?.SessionPlan;
            var building = HouseBuilding.For(plan);
            bool visible = plan != null && building != null && !EditorApplication.isPlayingOrWillChangePlaymode;
            Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) { stamp = null; return; }
            string current = HousePropertyState.Stamp(plan, building);
            bool layoutChanged = false;
            if (stamp != current)
            {
                stamp = current;
                var selection = session.Selection.Items;
                string room = selection.Count == 1 && selection[0].Type == HouseEditSelection.Kind.Room ? selection[0].Id : null;
                HouseEditSelection.Item surface = null;
                if (selection.Count > 0 && selection.All(item => item.Type == HouseEditSelection.Kind.SurfaceRegion)
                    && selection.Select(item => item.BoundaryId).Distinct().Count() == 1)
                {
                    var ids = selection.Select(item => item.Id).ToArray();
                    bool oneArea = HouseSceneQueries.SurfaceAreas(building, selection[0].BoundaryId)
                        .Any(area => ids.Count() == 1 && ids.Contains(area.RegionId));
                    if (selection.Count == 1 || oneArea) surface = selection[0];
                }
                Data = HouseMetricRulerData.Read(plan, room, surface?.BoundaryId, surface?.Id);
                hovered = null; Diagram.tooltip = "";
                layoutChanged = true;
            }
            float viewHeight = view.rootVisualElement.layout.height;
            // Reserve the SceneView camera gizmo and its projection controls.
            // Compress the ruler below them rather than moving it into their hit area.
            const float top = 200;
            float available = Mathf.Max(32, viewHeight - top - 40);
            if (Data.FitHeight(available)) layoutChanged = true;
            if (layoutChanged) Layout();
            Root.style.height = Data.Height;
            // The active level stays at the viewport's same vertical anchor when
            // switching levels, even when a neighbour is missing or heights differ.
            Root.style.top = top + (available - Data.Height) / 2;
            Root.style.right = 12;
        }

        private void Layout()
        {
            Diagram.Clear();
            Diagram.style.height = Data.Height;
            int first = Mathf.CeilToInt(Data.Minimum * 5), last = Mathf.FloorToInt(Data.Maximum * 5);
            int labelEvery = Mathf.Max(1, Mathf.CeilToInt(18 / Data.Scale));
            for (int tick = first; tick <= last; tick++)
            {
                if (tick % (5 * labelEvery) != 0) continue;
                var label = new Label(HouseSectionData.Metres(tick / 5f)) { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.left = 0; label.style.top = Data.Y(tick / 5f) - 8;
                label.style.width = 36; label.style.height = 16; label.style.fontSize = 11;
                label.style.unityTextAlign = TextAnchor.MiddleRight; label.style.color = Ink;
                Diagram.Add(label);
            }
            // Keyboard users can reach the same bands. Invisible targets retain true
            // proportions; pointer hover uses nearest-band resolution, not overlapping boxes.
            foreach (var band in Data.Bands)
            {
                var button = new Button(() => Select(band)) { name = BandName(band), tooltip = band.Tooltip };
                button.style.position = Position.Absolute; button.style.left = 50; button.style.width = 30;
                float top = Data.Y(band.Top), bottom = Data.Y(band.Bottom);
                float height = Mathf.Max(HouseMetricRulerData.MinimumHoverHeight, bottom - top);
                button.style.top = (top + bottom - height) / 2; button.style.height = height;
                button.style.minHeight = 0; button.style.paddingTop = button.style.paddingBottom = 0;
                button.style.marginTop = button.style.marginBottom = 0;
                button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0;
                button.style.backgroundColor = Color.clear;
                button.pickingMode = PickingMode.Ignore;
                Diagram.Add(button);
            }
            foreach (var row in Data.Section.Rows.Where(row => row.OpenSky))
            {
                float y = Data.Y(row.Ceiling);
                if (y < 0 || y > Data.Height) continue;
                var sky = new Label(row.HasCeiling ? "Partly open sky" : "Open sky") { pickingMode = PickingMode.Ignore };
                sky.style.position = Position.Absolute;
                sky.style.left = -94; sky.style.top = y - 8; sky.style.width = 124;
                sky.style.unityTextAlign = TextAnchor.MiddleRight;
                sky.style.fontSize = 11; sky.style.color = Ink;
                sky.style.whiteSpace = WhiteSpace.NoWrap;
                Diagram.Add(sky);
            }
            AddLevelArrow(-1);
            AddLevelArrow(1);
            Diagram.MarkDirtyRepaint();
        }

        private void AddLevelArrow(int direction)
        {
            var rows = Data.Section.Rows;
            int index = rows.FindIndex(r => r.Level.Plan == Data.Section.Plan) + direction;
            if (index < 0 || index >= rows.Count || rows[index].Level.Plan == null) return;
            var row = rows[index];
            // Fill this storey's clear space using the same scaled surfaces as
            // its slab bands. Insets keep the button away from slabs and ticks.
            const float verticalInset = 6, horizontalInset = 2;
            float top = Data.Y(row.Ceiling) + verticalInset;
            float bottom = Data.Y(row.Floor) - verticalInset;
            if (bottom <= top) return; // A compressed gap may have no usable button area.
            var gap = Data.HitGap((top + bottom) / 2);
            var arrow = new Button(() => HouseEditingContext.CycleLevel(HouseEditingContext.Active?.Session?.SessionPlan, direction))
            {
                name = direction > 0 ? "house-ruler-up" : "house-ruler-down",
                tooltip = "Go to " + HouseSceneUI.LevelName(row.Level.Plan) + (gap == null ? "" : "\n" + gap.Tooltip)
            };
            arrow.style.position = Position.Absolute;
            arrow.style.left = 50 + horizontalInset; arrow.style.width = 30 - horizontalInset * 2;
            arrow.style.top = top; arrow.style.height = bottom - top; arrow.style.minHeight = 0;
            arrow.style.paddingLeft = arrow.style.paddingRight = arrow.style.paddingTop = arrow.style.paddingBottom = 0;
            arrow.style.marginLeft = arrow.style.marginRight = arrow.style.marginTop = arrow.style.marginBottom = 0;
            arrow.style.borderLeftWidth = arrow.style.borderRightWidth = arrow.style.borderTopWidth = arrow.style.borderBottomWidth = 0;
            arrow.style.backgroundColor = Color.clear;
            arrow.RegisterCallback<PointerEnterEvent>(_ => arrow.style.backgroundColor = new Color(1, 1, 1, .12f));
            arrow.RegisterCallback<PointerLeaveEvent>(_ => arrow.style.backgroundColor = Color.clear);
            arrow.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                // Compressed rulers can put a slab beneath the button. Keep that
                // slab's conservative hit target accessible instead of navigating.
                var band = Data?.HitBand(e.position.y - Diagram.worldBound.y);
                if (band != null) { Select(band); e.StopImmediatePropagation(); }
            }, TrickleDown.TrickleDown);
            // Let the button process the press, then prevent the ruler/Scene from
            // interpreting that same press as geometry input.
            arrow.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.flexGrow = 1;
            icon.generateVisualContent += context =>
            {
                var p = context.painter2D;
                float x = icon.contentRect.width / 2, y = icon.contentRect.height / 2;
                p.strokeColor = Color.white; p.lineWidth = 2;
                p.BeginPath(); p.MoveTo(new Vector2(x - 5, y + direction * 3));
                p.LineTo(new Vector2(x, y - direction * 3)); p.LineTo(new Vector2(x + 5, y + direction * 3)); p.Stroke();
            };
            arrow.Add(icon); Diagram.Add(arrow);
        }
        internal static string BandName(HouseMetricRulerData.Band band) => "house-ruler-" + (band.Ceiling ? "ceiling-" : "floor-")
            + band.Row.Level.Id + (band.Reference ? "-default" : "");

        internal void Select(HouseMetricRulerData.Band band)
        {
            var context = HouseEditingContext.Active;
            if (context == null || context.Controller.HasSceneGesture) return;
            if (stamp != HousePropertyState.Stamp(context.Session.SessionPlan, HouseBuilding.For(context.Session.SessionPlan)))
            { Refresh(); return; } // A removed/moved band must not act on yesterday's selection.
            if (!Data.Bands.Contains(band)) return;
            var plan = band.Row.Level.Plan;
            if (HouseBuilding.For(plan) != Data.Section.Building) return;
            if (band.Selected && !band.Reference && Data.Section.RoomId != null && plan == Data.Section.Plan)
            {
                OpenThickness(plan, band);
                return;
            }
            if (context.Session.SessionPlan != plan) HouseEditingContext.SelectLevel(plan);
            if (HouseEditingContext.Active?.Session?.SessionPlan != plan) return;
            var controller = HouseEditingContext.Active.Controller;
            controller.SetMode(HouseEditorSession.Mode.Edit);
            if (band.BoundaryId != null && band.RegionId != null)
            {
                controller.Select(HouseEditSelection.Kind.SurfaceRegion, band.RegionId, boundaryId: band.BoundaryId);
                Refresh();
                return;
            }
            controller.Select(band.Ceiling ? HouseEditSelection.Kind.CeilingSlab : HouseEditSelection.Kind.FloorSlab,
                band.Row.Level.Id);
            HousePropertiesWindow.ShowSelection();
            Refresh();
        }
        private Rect Anchor(HouseMetricRulerData.Band band) => new Rect(Diagram.worldBound.x,
            Mathf.Clamp(Diagram.worldBound.y + Data.Y(band.Top), Root.worldBound.yMin, Root.worldBound.yMax - 8), 82, 8);
        private void OpenThickness(HousePlan plan, HouseMetricRulerData.Band band)
        {
            UnityEditor.PopupWindow.Show(Anchor(band), new HouseSectionValuePopup(plan,
                "Slab thickness (m)", "House-wide: centred supporting slab, including its floor finish."));
        }
        private void Hover(Vector2 point)
        {
            hovered = point.x >= 48 ? Data?.HitBand(point.y) : null;
            Diagram.tooltip = hovered?.Tooltip ?? (point.x >= 48 ? Data?.HitGap(point.y)?.Tooltip : null) ?? "";
            Diagram.MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (Data == null) return;
            var p = context.painter2D;
            void Line(float x, float y, float endX, float endY, Color color)
            {
                p.strokeColor = color; p.lineWidth = 1;
                p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(endX, endY)); p.Stroke();
            }
            Line(38, Data.Y(Data.Maximum), 38, Data.Y(Data.Minimum), Ink);
            int first = Mathf.CeilToInt(Data.Minimum * 5), last = Mathf.FloorToInt(Data.Maximum * 5);
            for (int i = first; i <= last; i++) Line(38, Data.Y(i / 5f), i % 5 == 0 ? 48 : 43, Data.Y(i / 5f), Ink);
            var selection = HouseEditingContext.Active?.Session?.Selection;
            foreach (var band in Data.Bands.OrderByDescending(b => b.Reference))
            {
                bool picked = selection?.Contains(band.Ceiling ? HouseEditSelection.Kind.CeilingSlab : HouseEditSelection.Kind.FloorSlab,
                    band.Row.Level.Id) == true;
                var color = band.Selected || picked ? Selected : Ink;
                if (band.Reference) color.a = .18f;
                else if (band.Row.Level.Plan != Data.Section.Plan && !band.Selected) color.a = .55f;
                float top = Data.Y(band.Top), bottom = Data.Y(band.Bottom);
                p.fillColor = p.strokeColor = color; p.lineWidth = 1;
                p.BeginPath(); p.MoveTo(new Vector2(50, top)); p.LineTo(new Vector2(80, top));
                p.LineTo(new Vector2(80, bottom)); p.LineTo(new Vector2(50, bottom)); p.ClosePath();
                p.Fill();
                if (band == hovered) Line(50, (top + bottom) / 2, 80, (top + bottom) / 2, Selected);
            }
        }
    }

    [InitializeOnLoad]
    internal static class HouseMetricRulerHost
    {
        private static double nextCheck;
        static HouseMetricRulerHost()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Remove;
        }
        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + .25;
            if (HouseEditingContext.Active?.Session?.SessionPlan == null) return;
            foreach (SceneView view in SceneView.sceneViews) Ensure(view);
        }
        internal static VisualElement Ensure(SceneView view)
        {
            var root = view.rootVisualElement.Q<VisualElement>("house-metric-ruler");
            if (root != null) return root;
            var ruler = new HouseMetricRuler(view);
            view.rootVisualElement.Add(ruler.Root);
            return ruler.Root;
        }
        private static void Remove()
        {
            foreach (SceneView view in SceneView.sceneViews)
                view.rootVisualElement.Q<VisualElement>("house-metric-ruler")?.RemoveFromHierarchy();
        }
    }
}
