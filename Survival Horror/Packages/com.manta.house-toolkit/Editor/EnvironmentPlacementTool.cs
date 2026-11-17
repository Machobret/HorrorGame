using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace HouseToolkit.Editor
{
    [EditorTool("Place environment object")]
    public sealed class EnvironmentPlacementTool : EditorTool
    {
        internal enum Kind { House, Road, Sidewalk, GroundArea, FillerBuilding, Wall, Shelf, Marking }
        internal static EnvironmentPlacementTool Pending => ToolManager.activeToolType == typeof(EnvironmentPlacementTool)
            ? Resources.FindObjectsOfTypeAll<EnvironmentPlacementTool>().FirstOrDefault(ToolManager.IsActiveTool) : null;
        internal Kind choice;
        SceneView owner;
        Vector3? cursor;
        readonly PointPlacement press=new PointPlacement();
        float elevation;

        [MenuItem("GameObject/Environment/House",false,10)] static void House() => Arm(Kind.House);
        [MenuItem("GameObject/Environment/Road",false,11)] static void Road() => Arm(Kind.Road);
        [MenuItem("GameObject/Environment/Sidewalk",false,12)] static void Sidewalk() => Arm(Kind.Sidewalk);
        [MenuItem("GameObject/Environment/Ground Area",false,13)] static void Ground() => Arm(Kind.GroundArea);

        [MenuItem("GameObject/Environment/Filler Building",false,14)] static void Filler() => Arm(Kind.FillerBuilding);

        [MenuItem("GameObject/Environment/Standalone Wall",false,15)] static void Wall() => Arm(Kind.Wall);
        [MenuItem("GameObject/Environment/Shelf",false,16)] static void Shelf() => Arm(Kind.Shelf);

        [MenuItem("GameObject/Environment/Marking",false,17)] static void Marking() => Arm(Kind.Marking);

        internal static void Arm(Kind kind,SceneView view=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(HouseScene.Floor)HouseScene.Leave();
            if(RoadScene.Active)RoadScene.Leave();
            view=view ? view : SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView : EditorWindow.GetWindow<SceneView>();
            ToolManager.SetActiveTool<EnvironmentPlacementTool>();
            var tool=Pending;
            tool.press.Cancel();tool.choice=kind;tool.owner=view;tool.cursor=null;
            view.Focus();view.Repaint();
        }
        internal static void Cancel() => ToolManager.RestorePreviousTool();
        public override void OnWillBeDeactivated(){press.Cancel();owner=null;cursor=null;}

        public override void OnToolGUI(EditorWindow window)
        {
            if(!(window is SceneView view) || view!=owner)return;
            var e=Event.current;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape){e.Use();Cancel();return;}
            if(press.Track(view,e,Place))return;
            if(e.button==1 && e.isMouse || Tools.viewToolActive && !e.alt)return;
            int control=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.Layout && !e.alt)HandleUtility.AddDefaultControl(control);
            if(e.type==EventType.MouseMove || e.rawType==EventType.MouseDown || e.type==EventType.Repaint)
            {
                var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if(!HandleUtility.PlaceObject(e.mousePosition,out var point,out _))
                {
                    if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance)){cursor=null;return;}
                    point=ray.GetPoint(distance);
                }
                if(choice==Kind.Marking && MarkingPlacement.Cursor(ray,Vector3.up,out var groundPoint))point=groundPoint;
                var plan=PlanSnapping.FloorPoint(new Vector2(point.x,point.z),null,false,e.alt,PlanSnapping.Grid);
                cursor=new Vector3(plan.x,point.y,plan.y);
                if(e.type==EventType.Repaint)
                    using(new Handles.DrawingScope(Color.green))
                        Handles.DrawWireDisc(cursor.Value,Vector3.up,HandleUtility.GetHandleSize(cursor.Value)*.1f);
                if(e.rawType==EventType.MouseDown && e.button==0)
                {
                    elevation=point.y;
                    press.Press(view,e,control,plan,false,Place);
                }
                else if(e.type==EventType.MouseMove)view.Repaint();
            }
        }
        void Place(Vector2 point,bool finish)
        {
            var kind=choice;
            var view=owner;
            var position=new Vector3(point.x,elevation,point.y);
            Cancel();
            // A newly created object immediately receives subsequent drawing clicks.
            if(Tools.current==Tool.View)Tools.current=Tool.Move;
            if(kind==Kind.House || kind==Kind.Wall)HouseScene.CreateAt(position,true,kind==Kind.Wall);
            else if(kind==Kind.FillerBuilding)RoadScene.CreateFillerAt(position);
            else if(kind==Kind.Marking)RoadScene.CreateMarkingAt(position);
            else if(kind==Kind.Shelf)RoadScene.CreateShelfAt(position);
            else RoadScene.CreateAt(kind==Kind.Sidewalk ? RoadScene.Brush.Sidewalk :
                kind==Kind.GroundArea ? RoadScene.Brush.Ground : RoadScene.Brush.Road,position,true);
            if(view)view.Focus();
        }
    }
}
