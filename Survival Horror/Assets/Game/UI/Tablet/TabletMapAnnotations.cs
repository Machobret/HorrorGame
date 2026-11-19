using System.Collections.Generic;
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
        private Mesh connectionMesh;
        private MeshRenderer connectionRenderer;
        private Material connectionMaterial;
        private readonly List<Vector3> dashVertices = new List<Vector3>();
        private readonly List<int> dashTriangles = new List<int>();

        private void LateUpdate()
        {
            raycastTarget = false;
            SetVerticesDirty();
            UpdateCircuitConnection();
        }

        protected override void OnDisable()
        {
            if (connectionRenderer) connectionRenderer.enabled = false;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (connectionRenderer) Destroy(connectionRenderer.gameObject);
            if (connectionMesh) Destroy(connectionMesh);
            if (connectionMaterial) Destroy(connectionMaterial);
            base.OnDestroy();
        }
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
        }

        private void UpdateCircuitConnection()
        {
            if (connectionRenderer) connectionRenderer.enabled = false;
            var network = Workspace ? Workspace.Network : null;
            if (!network || !network.HoveredInRange || !network.HoveredPoint ||
                !MapRenderer.Exists || Workspace.Map.IsMapDirectoryOpen) return;

            var renderer = MapRenderer.Instance;
            var camera = renderer.RenderCamera;
            var player = renderer.VisiblePlayerMarker;
            if (!camera || !player) return;
            Vector3 start = camera.WorldToViewportPoint(player.position);
            Vector3 end = camera.WorldToViewportPoint(network.HoveredPoint.transform.position);
            if (start.z <= 0 || end.z <= 0 ||
                !Workspace.Map.TryProjectMapViewport(start, true, out Vector2 startScreen) ||
                !Workspace.Map.TryProjectMapViewport(end, true, out Vector2 endScreen) ||
                !Workspace.Map.TryProjectMapViewport(Vector2.zero, true, out Vector2 origin, true) ||
                !Workspace.Map.TryProjectMapViewport(Vector2.one, true, out Vector2 corner, true)) return;

            float canvasScale = Workspace.MapImage.canvas.scaleFactor;
            Vector2 extent = (corner - origin) / canvasScale;
            Vector2 delta = (endScreen - startScreen) / canvasScale;
            float length = delta.magnitude;
            if (length < 18f || extent.x <= 0f || extent.y <= 0f || !EnsureConnectionMesh(renderer)) return;

            Vector2 normal = new Vector2(-delta.y, delta.x) / length;
            float scale = renderer.MapEffectScale;
            Vector2 halfWidth = Vector2.Scale(normal * (1.5f * scale), new Vector2(1f / extent.x, 1f / extent.y));
            float spacing = 16f * scale;
            float phase = Mathf.Repeat(Time.unscaledTime * 38f * scale, spacing);
            dashVertices.Clear();
            dashTriangles.Clear();

            // Draw in the map texture before its transparent icons.
            for (float dash = phase - spacing; dash < length - 8f; dash += spacing)
            {
                float from = Mathf.Max(0f, dash);
                float to = Mathf.Min(length - 8f, dash + 10f * scale);
                if (to <= from) continue;
                Vector2 head = Vector2.Lerp(start, end, from / length);
                Vector2 tail = Vector2.Lerp(start, end, to / length);
                int index = dashVertices.Count;
                AddVertex(head - halfWidth);
                AddVertex(head + halfWidth);
                AddVertex(tail + halfWidth);
                AddVertex(tail - halfWidth);
                dashTriangles.Add(index);
                dashTriangles.Add(index + 1);
                dashTriangles.Add(index + 2);
                dashTriangles.Add(index);
                dashTriangles.Add(index + 2);
                dashTriangles.Add(index + 3);
            }

            connectionMesh.Clear();
            connectionMesh.SetVertices(dashVertices);
            connectionMesh.SetTriangles(dashTriangles, 0);
            bool powered = network.SourceGrid && network.SourceGrid.IsCircuitPowered(network.HoveredPoint.CircuitId);
            connectionMaterial.SetColor("_BaseColor", powered ? new Color(1f, .87f, .55f, .75f) : new Color(.42f, .77f, 1f, .75f));
            connectionRenderer.enabled = dashVertices.Count > 0;

            void AddVertex(Vector2 viewport)
            {
                Vector3 world = camera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, start.z + .1f));
                dashVertices.Add(connectionRenderer.transform.InverseTransformPoint(world));
            }
        }

        private bool EnsureConnectionMesh(MapRenderer renderer)
        {
            if (connectionRenderer) return true;
            var template = Resources.Load<Material>("MapTransparentIcon");
            if (!template) return false;

            var connection = new GameObject("Map circuit connection");
            connection.layer = LayerMask.NameToLayer("MapSystem");
            connection.transform.SetParent(renderer.transform, false);
            connectionMesh = new Mesh { name = "Map circuit dashes" };
            connectionMesh.MarkDynamic();
            connection.AddComponent<MeshFilter>().sharedMesh = connectionMesh;
            connectionRenderer = connection.AddComponent<MeshRenderer>();
            connectionMaterial = new Material(template) { renderQueue = 2999 };
            connectionMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            connectionRenderer.sharedMaterial = connectionMaterial;
            return true;
        }
    }
}
