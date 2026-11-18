using HorrorEngine;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Shape-based markers projected through the existing map camera; no room discovery is modified.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletMapAnnotations : MaskableGraphic
    {
        public TabletWorkspace Workspace;
        void LateUpdate(){raycastTarget=false;SetVerticesDirty();}
        bool Project(Vector3 local,out Vector2 p)
        {
            var renderer=MapRenderer.Instance;var v=renderer.RenderCamera.WorldToViewportPoint(renderer.MapPositionToWorld(local));
            var uv=Workspace.MapImage.uvRect;v.x=(v.x-uv.x)/uv.width;v.y=(v.y-uv.y)/uv.height;
            p=rectTransform.rect.min+Vector2.Scale(new Vector2(v.x,v.y),rectTransform.rect.size);return v.z>0 && v.x>.01f && v.x<.99f && v.y>.01f && v.y<.99f;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(!Workspace || !Workspace.Map.CurrentMap || !MapRenderer.Exists || !MapRenderer.Instance.RenderCamera || !GameManager.Exists)return;
            var map=Workspace.Map.CurrentMap;
            foreach(var door in map.Doors)
            {
                var state=door.GetState();if(state==MapDoorState.Unknown)continue;
                var p=map.GetTRS(door.Transform.Offset,door.Transform.Rotation,door.Transform.Scale,door.Transform.ZOrder).MultiplyPoint(Vector3.zero);
                if(Project(p,out var screen))TabletMapSymbol.Draw(mesh,state==MapDoorState.Locked?TabletSymbolKind.LockedDoor:TabletSymbolKind.UnlockedDoor,screen,6);
            }
            foreach(var room in map.Rooms)if(room.GetState()==MapRoomState.NotVisited)
            {
                var p=map.GetTRS(room.Transform.Offset,room.Transform.Rotation,room.Transform.Scale,room.Transform.ZOrder).MultiplyPoint(Vector3.zero);
                if(Project(p,out var screen))TabletMapSymbol.Draw(mesh,TabletSymbolKind.Unexplored,screen,6);
            }
            DrawCircuitConnection(mesh);
        }

        private void DrawCircuitConnection(VertexHelper mesh)
        {
            var network = Workspace.Network;
            var renderer = MapRenderer.Instance;
            if (!network || !network.HoveredInRange || !network.HoveredPoint ||
                !renderer.VisiblePlayerMarker || Workspace.Map.IsMapDirectoryOpen) return;

            if (!ProjectForConnection(renderer.VisiblePlayerMarker.position, out Vector2 start) ||
                !ProjectForConnection(network.HoveredPoint.transform.position, out Vector2 end)) return;
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 18f) return;
            Vector2 direction = delta / length;
            bool powered = network.SourceGrid && network.SourceGrid.IsCircuitPowered(network.HoveredPoint.CircuitId);
            Color32 color = powered ? new Color(1f, .87f, .55f, .75f) : new Color(.42f, .77f, 1f, .75f);
            float scale = renderer.MapEffectScale;
            float spacing = 16f * scale;
            float phase = Mathf.Repeat(Time.unscaledTime * 38f * scale, spacing);
            for (float dash = phase - spacing; dash < length - 8f; dash += spacing)
            {
                float from = Mathf.Max(7f, dash);
                float to = Mathf.Min(length - 8f, dash + 10f * scale);
                if (to <= from) continue;
                Vector2 a = start + direction * from;
                Vector2 b = start + direction * to;
                DrawLine(mesh, a, b, 1.5f * scale, color);
            }
        }

        private static void DrawLine(VertexHelper mesh, Vector2 a, Vector2 b, float halfWidth, Color32 color)
        {
            Vector2 delta = b - a;
            Vector2 across = new Vector2(-delta.y, delta.x).normalized * halfWidth;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - across, color, Vector2.zero);
            mesh.AddVert(a + across, color, Vector2.zero);
            mesh.AddVert(b + across, color, Vector2.zero);
            mesh.AddVert(b - across, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }

        private bool ProjectForConnection(Vector3 world, out Vector2 point, bool allowOutside = false)
        {
            point = default;
            Vector3 viewport = MapRenderer.Instance.RenderCamera.WorldToViewportPoint(world);
            if (viewport.z <= 0 || !Workspace.Map.TryProjectMapViewport(viewport, true, out Vector2 screen, allowOutside))
                return false;
            var root = canvas.rootCanvas;
            var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out point);
        }
    }
}
