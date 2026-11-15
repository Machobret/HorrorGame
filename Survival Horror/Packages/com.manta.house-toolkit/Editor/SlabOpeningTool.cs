using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal sealed class SlabOpeningTool
    {
        readonly List<Vector2> draft=new List<Vector2>();
        readonly PointPlacement pointPress=new PointPlacement();
        HouseFloor owner;
        bool roof,rectangle,armed,altering;
        bool ceiling;
        float Elevation => roof ? owner.height : ceiling ? -owner.House.slabThickness : 0;
        Vector2 down,startCorner,downGui,rightDown;
        bool rightHeld,rightNavigated;
        int selected=-1,vertex=-1,control;
        int editingIndex=-1;
        string editingId;
        SceneView capture;
        Vector2[] proposed;
        public bool Active=>owner;
        internal bool EditingOutline => editingIndex >= 0;
        internal bool CanConfigure => Active && !EditingOutline && !armed && draft.Count == 0;
        internal bool Ceiling => ceiling;
        internal bool Rectangle => rectangle;

        internal bool TryFrameOutline(out Bounds bounds)
        {
            bounds = default;
            if (!owner || editingId == null) return false;
            var opening = Openings.Find(cut => cut.id == editingId);
            return opening != null && HouseSelection.TryFrameCut(owner, roof, proposed ?? opening.points, out bounds);
        }
        List<SlabOpening> Openings=>roof?owner.roofOpenings:owner.floorOpenings;

        internal static HouseFloor Owner(HouseFloor active,bool ceiling,out bool roof)
        {
            roof=false;
            if(!ceiling)return active;
            var floors=HouseEditing.Floors(active.House);
            int i=Array.IndexOf(floors,active);
            if(i+1<floors.Length)return floors[i+1];
            roof=true;return active;
        }

        internal void Begin(HouseFloor active,bool ceiling,bool rect,int index)
        {
            Cancel();owner=Owner(active,ceiling,out roof);this.ceiling=ceiling;rectangle=rect;
            if (index >= Openings.Count) { Cancel(); return; }
            selected=editingIndex=index;
            editingId=index>=0 ? Openings[index].id : null;
            HouseScene.Message=index>=0?"Drag a vertex to reshape or an edge to move. Double-click an edge to add a vertex."
                :rect?"Drag between opposite corners.":"Click each corner. Click the first point or press Enter to finish.";
        }
        internal void Cancel()
        {
            EndGesture();
            owner=null;draft.Clear();editingId=null;editingIndex=selected=vertex=-1;rightHeld=false;
        }
        void EndGesture()
        {
            pointPress.Cancel();
            if(control!=0 && GUIUtility.hotControl==control)GUIUtility.hotControl=0;
            capture=null;control=0;armed=false;altering=false;proposed=null;
        }
        internal void OnUndo()
        {
            EndGesture();
            vertex=-1;
            if (!RefreshTarget()) return;
            HouseScene.Message=null;
        }

        internal bool RefreshTarget()
        {
            if (editingId == null) return true;
            if (editingIndex >= 0 && editingIndex < Openings.Count && Openings[editingIndex].id == editingId) return true;
            EndGesture();
            vertex=-1;
            selected=editingIndex=Openings.FindIndex(cut => cut.id == editingId);
            if (editingIndex >= 0) return true;
            HouseScene.EndOpening();
            return false;
        }
        Vector3 World(Vector2 p)=>owner.World(p)+owner.transform.TransformVector(Vector3.up*Elevation);
        Vector2 Pixel(Vector2 p)=>HandleUtility.WorldToGUIPoint(World(p));
        void Commit(int index,Vector2[] points)
        {
            bool reshape=points!=null && index>=0 && index<Openings.Count;
            EndGesture();
            if(SlabBuilder.SetOpening(owner,roof,index,points,out HouseScene.Message))
            {
                if(reshape)
                {
                    selected=editingIndex=index;
                    editingId=Openings[index].id;
                    vertex=Mathf.Min(vertex,points.Length-1);
                    HouseScene.Message="Drag another vertex or edge. Press Escape to finish.";
                }
                else Cancel();
            }
            HouseScene.Repaint();
        }
        void Pick(Vector2 mouse,out int edge)
        {
            selected=editingIndex;vertex=-1;edge=-1;
            float best=10;
            for(int i=0;i<Openings.Count;i++)
            {
                if(editingIndex>=0 && i!=editingIndex)continue;
                var loop=Openings[i].points;
                for(int j=0;j<loop.Length;j++)
                {
                    float d=Vector2.Distance(Pixel(loop[j]),mouse);
                    if(d<best){best=d;selected=i;vertex=j;}
                }
            }
            if(vertex>=0)return;
            for(int i=0;i<Openings.Count;i++)
            {
                if(editingIndex>=0 && i!=editingIndex)continue;
                var loop=Openings[i].points;
                for(int j=0;j<loop.Length;j++)
                {
                    float d=HandleUtility.DistancePointToLineSegment(mouse,Pixel(loop[j]),Pixel(loop[(j+1)%loop.Length]));
                    if(d<best){best=d;selected=i;edge=j;}
                }
            }
        }
        void PlacePoint(Vector2 point,bool closes)
        {
            if(closes)Commit(Openings.Count,draft.ToArray());
            else if(draft.Count==0 || draft[draft.Count-1]!=point)draft.Add(point);
            SceneView.RepaintAll();
        }

        internal void Handle(SceneView view,Event e)
        {
            if (!RefreshTarget()) return;
            int id=GUIUtility.GetControlID(FocusType.Passive);
            if(pointPress.Track(view,e,PlacePoint))return;
            if(armed && capture!=view)return;
            if(e.type==EventType.KeyDown)
            {
                if(e.keyCode==KeyCode.Escape){HouseScene.EndOpening();e.Use();return;}
                if(e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter)
                {
                    if(draft.Count>0)Commit(Openings.Count,draft.ToArray());e.Use();return;
                }
                if(e.keyCode==KeyCode.Backspace || e.keyCode==KeyCode.Delete)
                {
                    if(draft.Count>0)draft.RemoveAt(draft.Count-1);
                    else if(selected>=0 && selected<Openings.Count)
                    {
                        if(vertex>=0 && Openings[selected].points.Length>3)Commit(selected,Openings[selected].points.Where((_,i)=>i!=vertex).ToArray());
                        else if(vertex<0)Commit(selected,null);
                    }
                    e.Use();view.Repaint();return;
                }
            }
            if(e.rawType==EventType.MouseDown && e.button==1){rightDown=e.mousePosition;rightHeld=true;rightNavigated=false;}
            if(rightHeld && (e.rawType==EventType.KeyDown || (e.mousePosition-rightDown).sqrMagnitude>=9))rightNavigated=true;
            if(e.rawType==EventType.MouseUp && e.button==1)
            {
                if(rightHeld && !rightNavigated && draft.Count>0){draft.RemoveAt(draft.Count-1);view.Repaint();}
                rightHeld=false;
            }
            if((Tools.viewToolActive && !e.alt) || Tools.current==Tool.View || e.button==1 && e.isMouse)return;
            if(e.type==EventType.Layout && !e.alt)HandleUtility.AddDefaultControl(id);
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if(!new Plane(owner.transform.up,World(Vector2.zero)).Raycast(ray,out var depth))return;
            var raw=owner.Local(ray.GetPoint(depth));
            Vector2 Snap(Vector2 p)=>PlanSnapping.FloorPoint(p,null,false,e.alt,HouseScene.Grid);
            var cursor=Snap(raw);
            bool closes=draft.Count>=3 && Vector2.Distance(Pixel(draft[0]),e.mousePosition)<10;
            if(!e.alt && (!armed || !altering))
            {
                float best=10;
                foreach(var node in owner.corners)
                {
                    float distance=Vector2.Distance(Pixel(node.point),e.mousePosition);
                    if(distance<best){best=distance;cursor=node.point;}
                }
            }
            if(selected>=Openings.Count)selected=-1;
            if(e.rawType==EventType.MouseDown && e.button==0 &&
                (e.alt || e.type==EventType.MouseDown && HandleUtility.nearestControl==id && GUIUtility.hotControl==0))
            {
                int edge=-1;
                vertex=-1;
                if(draft.Count==0 && !e.alt)Pick(e.mousePosition,out edge);
                altering=vertex>=0 || edge>=0;
                if(!altering && editingIndex>=0)return;
                if(!altering && !rectangle)
                {
                    pointPress.Press(view,e,id,cursor,closes,PlacePoint);
                    return;
                }
                if(e.alt)return;
                down=raw;startCorner=cursor;downGui=e.mousePosition;armed=true;capture=view;control=id;proposed=null;
                GUIUtility.hotControl=id;
                e.Use();
                if(altering && edge>=0 && e.clickCount>=2)
                {
                    var loop=Openings[selected].points.ToList();var a=loop[edge];var v=loop[(edge+1)%loop.Count]-a;
                    vertex=edge+1;
                    loop.Insert(vertex,a+v*Mathf.Clamp01(Vector2.Dot(raw-a,v)/v.sqrMagnitude));
                    Commit(selected,loop.ToArray());return;
                }
            }
            if(armed && altering && selected>=0 && Vector2.Distance(downGui,e.mousePosition)>3)
            {
                var original=Openings[selected].points;var anchor=original[vertex>=0?vertex:0];
                var delta=Snap(anchor+raw-down)-anchor;
                proposed=original.Select((p,i)=>vertex<0||i==vertex?p+delta:p).ToArray();
            }
            Vector2[] box=null;
            if(armed && rectangle && !altering)
            {
                var start=startCorner;var a=Vector2.Min(start,cursor);var b=Vector2.Max(start,cursor);
                box=SlabOpening.Rectangle(Rect.MinMaxRect(a.x,a.y,b.x,b.y));
            }
            if(e.type==EventType.Repaint)
            {
                if (!rightHeld) CursorGrid.Draw(view, owner, proposed != null ? proposed[Mathf.Max(0, vertex)] :
                    closes && !rectangle ? draft[0] : cursor, Elevation);
                void Draw(IEnumerable<Vector2> points,bool closed,Color color)
                {
                    var loop=points.ToArray();if(loop.Length==0)return;
                    Handles.color=color;
                    Handles.DrawAAPolyLine(3,(closed?loop.Append(loop[0]):loop).Select(World).ToArray());
                    foreach(var p in loop)Handles.DotHandleCap(0,World(p),Quaternion.identity,HandleUtility.GetHandleSize(World(p))*.055f,EventType.Repaint);
                }
                for(int i=0;i<Openings.Count;i++)Draw(i==selected&&proposed!=null?proposed:Openings[i].points,true,i==selected?Color.yellow:Color.cyan);
                if(box!=null)Draw(box,true,Color.yellow);
                if(draft.Count>0)Draw(draft.Append(closes?draft[0]:cursor),false,closes?Color.green:Color.yellow);
            }
            if(armed && e.rawType==EventType.MouseUp && e.button==0)
            {
                bool click=Vector2.Distance(downGui,e.mousePosition)<4;
                if(GUIUtility.hotControl==id)GUIUtility.hotControl=0;
                armed=false;capture=null;control=0;
                if(altering){if(proposed!=null)Commit(selected,proposed);proposed=null;}
                else if(box!=null && !click)Commit(Openings.Count,box);
                if(e.type!=EventType.Used)e.Use();view.Repaint();
            }
            if(e.type==EventType.MouseMove || e.type==EventType.MouseDrag)view.Repaint();
        }
    }
}
