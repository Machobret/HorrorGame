using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    internal static partial class FixtureTool
    {
        internal static FillerBuilding facadeBrush;

        internal static void BeginFacadeBrush(FillerBuilding building)
        {
            if (!building) return;
            if (RoadScene.Active != building) RoadScene.Enter(building);
            RoadScene.SetDrawing(false);
            moving = null;
            var choice = Brush;
            if (choice == null || !choice.Supports(FixtureMount.Wall))
            {
                choice = Choices.FirstOrDefault(c => c.Supports(FixtureMount.Wall));
                if (choice == null) return;
                SelectBrush(choice);
            }
            facadeBrush = building;
            RoadInspector.ShowRoad();
            if (!Application.isBatchMode) HouseProperties.Open();
            RoadScene.Repaint();
        }

        static HouseFloor HitFloor(RaycastHit hit)
        {
            var floor=hit.collider.GetComponentInParent<HouseFloor>();
            var slab=hit.collider.GetComponentInParent<HouseSlab>();
            if(floor && slab && !slab.overhead && Vector3.Dot(hit.normal,floor.transform.up)<-.9f)
            {
                var floors=HouseEditing.Floors(floor.House);
                int index=System.Array.IndexOf(floors,floor);
                if(index>0)return floors[index-1];
            }
            return floor;
        }

        static void ReattachScene(SceneView view)
        {
            if (!moving || HouseScene.Mode==HouseTool.Fixture && HouseScene.Floor) return;
            var e=Event.current;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape) { moving=null; e.Use(); return; }
            if(e.alt || Tools.viewToolActive)return;
            int control=GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var hit=Physics.RaycastAll(ray,Mathf.Infinity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider.GetComponentInParent<FixtureAttachment>()!=moving).OrderBy(h=>h.distance).FirstOrDefault();
            if(!hit.collider)return;
            var preset=moving.LightPreset;
            if(preset==null)return;
            var floor=HitFloor(hit);
            Transform host=null; Vector3 point=hit.point; int side=1;
            bool house=floor && TargetSurface(floor,preset,preset.mount,ray,out host,out point,out side);
            if(!TryFacade(preset,hit,out var building,out var edge) && !house)return;
            if(e.type==EventType.Repaint)Handles.DrawWireDisc(point,hit.normal,HandleUtility.GetHandleSize(point)*.09f);
            if(e.type==EventType.MouseMove)view.Repaint();
            if(e.type!=EventType.MouseDown || e.button!=0 || HandleUtility.nearestControl!=control)return;
            var fixture=moving;
            if(house) Mount(fixture,host,point,side);
            else
            {
                Undo.RecordObject(fixture,"Reattach fixture");
                Undo.SetTransformParent(fixture.transform,building.transform,"Reattach fixture");
                Undo.RecordObject(fixture.transform,"Reattach fixture");
                fixture.freePlacement=false; fixture.mount=FixtureMount.Wall;
                SetFacadeAnchor(fixture,building,edge,hit.point,hit.normal);
                Refit(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fixture.transform);
            }
            moving=null; e.Use(); view.Repaint();
        }

        static FixtureChoice DraggedFixture()
        {
            var objects = DragAndDrop.objectReferences;
            if (objects.Length != 1 || !(objects[0] is GameObject prefab) || !EditorUtility.IsPersistent(prefab) ||
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()!=null)
                return null;
            return Choices.FirstOrDefault(c => c.prefab == prefab && c.lightRange > 0);
        }

        static DragAndDropVisualMode DropHierarchyFixture(EntityId target, HierarchyDropFlags flags, Transform defaultParent,bool perform)
        {
            var choice=DraggedFixture();
            if(choice==null)return DragAndDropVisualMode.None;
            if(!perform)return DragAndDropVisualMode.Copy;
            var upon=(EditorUtility.EntityIdToObject(target) as GameObject)?.transform;
            bool between=(flags & HierarchyDropFlags.DropBetween)!=0;
            var parent=upon ? (between ? upon.parent : upon) : defaultParent;
            var fixture=Place(choice,null,parent ? parent.position : Vector3.zero,1);
            if(parent) Undo.SetTransformParent(fixture.transform,parent,"Place fixture");
            if(between && upon)fixture.transform.SetSiblingIndex(upon.GetSiblingIndex()+((flags & HierarchyDropFlags.DropAbove)!=0 ? 0 : 1));
            Selection.activeGameObject=fixture.gameObject;
            return DragAndDropVisualMode.Copy;
        }

        static DragAndDropVisualMode DropFixture(Object target, Vector3 position, Vector2 viewport, Transform parent, bool perform)
        {
            var choice=DraggedFixture();
            if (choice == null) return DragAndDropVisualMode.None;
            if (perform)
            {
                var view=SceneView.currentDrawingSceneView ? SceneView.currentDrawingSceneView : SceneView.lastActiveSceneView;
                var ray = Event.current!=null ? HandleUtility.GUIPointToWorldRay(Event.current.mousePosition) :
                    view && view.camera ? view.camera.ViewportPointToRay(viewport) : new Ray(position+Vector3.up,Vector3.down);
                var fixture = PlaceFromRay(choice, ray, position, parent);
                Selection.activeGameObject = fixture.gameObject;
            }
            return DragAndDropVisualMode.Copy;
        }

        internal static FixtureAttachment PlaceFromRay(FixtureChoice choice, Ray ray, Vector3 fallback, Transform freeParent)
        {
            if (Physics.Raycast(ray,out var hit,Mathf.Infinity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                var floor = HitFloor(hit);
                if (floor && TargetSurface(floor,choice,choice.mount,ray,out var host,out var point,out int side) &&
                    (hit.point-point).sqrMagnitude<.0004f)
                    return Place(choice,host,point,side);
                if (TryFacade(choice,hit,out var building,out var edge))
                    return PlaceFacade(choice,building,edge,hit.point,hit.normal);
                fallback = hit.point;
            }
            var free = Place(choice,null,fallback,1);
            if (freeParent) Undo.SetTransformParent(free.transform,freeParent,"Place fixture");
            return free;
        }

        static bool TryFacade(FixtureChoice choice,RaycastHit hit,out FillerBuilding building,out GroundEdge edge)
        {
            if (TryFacade(hit, out building, out edge)) return choice.Supports(FixtureMount.Wall);
            return false;
        }

        internal static bool TryFacade(RaycastHit hit,out FillerBuilding building,out GroundEdge edge)
        {
            building = hit.collider.GetComponent<FillerBuilding>();
            edge = null;
            if (!building || hit.triangleIndex < 0 ||
                hit.triangleIndex >= building.faces.Length) return false;
            var face = building.faces[hit.triangleIndex];
            if (face.part != RoadPart.Boundary) return false;
            edge = building.footprint.edges.Find(e=>e.id==face.id);
            return edge!=null;
        }

        internal static FixtureAttachment PlaceFacade(FixtureChoice choice,FillerBuilding building,GroundEdge edge,Vector3 point,Vector3 normal)
        {
            var fixture = Place(choice,building.transform,point,1);
            SetFacadeAnchor(fixture,building,edge,point,normal);
            return fixture;
        }

        static void SetFacadeAnchor(FixtureAttachment fixture,FillerBuilding building,GroundEdge edge,Vector3 point,Vector3 normal)
        {
            var p = building.transform.InverseTransformPoint(point);
            var tangent = (edge.b-edge.a).normalized;
            fixture.wallSide = Vector3.Dot(building.transform.TransformDirection(new Vector3(tangent.y,0,-tangent.x)),normal)>=0 ? 1 : -1;
            fixture.facadeEdge = edge.id;
            fixture.edgeDistance = Vector2.Dot(new Vector2(p.x,p.z)-edge.a,tangent);
            fixture.storey=0;
            while (fixture.storey+1<building.floors && p.y>=building.Elevation(fixture.storey+1)) fixture.storey++;
            fixture.storeyHeight = p.y-building.Elevation(fixture.storey);
            fixture.FitFacade(building,edge);
        }

        internal static bool DrawFacadeBrush(FillerBuilding building,Event e)
        {
            if (!building || facadeBrush!=building) return false;
            if (e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape) { facadeBrush=null; e.Use(); return true; }
            if (e.alt || Tools.viewToolActive) return true;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(control);
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var collider = building.GetComponent<MeshCollider>();
            if (Brush!=null && collider && collider.Raycast(ray,out var hit,Mathf.Infinity) &&
                TryFacade(Brush,hit,out _,out var edge))
            {
                if (e.type==EventType.Repaint) Handles.DrawWireDisc(hit.point,hit.normal,HandleUtility.GetHandleSize(hit.point)*.09f);
                if (e.type==EventType.MouseDown && e.button==0 && HandleUtility.nearestControl==control)
                {
                    PlaceFacade(Brush,building,edge,hit.point,hit.normal);
                    SceneView.RepaintAll();
                    e.Use();
                }
            }
            if(e.type==EventType.MouseMove) SceneView.RepaintAll();
            return true;
        }
    }
}
