using System.Collections.Generic;
using HorrorEngine;
using HorrorGame.Chapter1;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.UI
{
    /// <summary>Shape-based markers projected through the existing map camera; no room discovery is modified.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TabletMapAnnotations : MaskableGraphic
    {
        public TabletWorkspace Workspace;
        readonly List<PowerCircuitMapMarker> terminals=new List<PowerCircuitMapMarker>();
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
            terminals.Clear();MapRenderer.Instance.GetComponentsInChildren(false,terminals);
            foreach(var terminal in terminals)
            {
                if(Project(MapRenderer.Instance.WorldToMapPosition(terminal.transform.position),out var screen))TabletMapSymbol.Draw(mesh,TabletSymbolKind.PowerPoint,screen,7);
            }
        }
    }
}
