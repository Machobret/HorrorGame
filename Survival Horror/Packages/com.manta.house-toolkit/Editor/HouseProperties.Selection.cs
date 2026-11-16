using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseToolkit.Editor
{
    public sealed partial class HouseProperties
    {
        static bool Batch(string label, IEnumerable<Object> targets,Action edit)
        {
            Undo.IncrementCurrentGroup();
            int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            var owners=targets.Where(t=>t).Distinct().ToArray();
            try
            {
                Undo.RegisterCompleteObjectUndo(owners,label);
                edit();
                foreach(var owner in owners)
                {
                    EditorUtility.SetDirty(owner);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
                }
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
                HouseScene.Message=null;
                return true;
            }
            catch(InvalidOperationException ex)
            {
                Undo.RevertAllDownToGroup(group);HouseScene.Message=ex.Message;return false;
            }
            finally { HouseScene.Repaint(); }
        }

        static void DrawCommonWalls(HouseWall[] walls)
        {
            void Draw<V>(IEnumerable<HouseWall> subset,Func<HouseWall,V> read,Func<V,V> field,Action<HouseWall,V> write)
            {
                var targets=subset.ToArray();
                MultiField.Draw(targets,read,field,value=>Batch("Edit walls",targets,()=>{
                    foreach(var w in targets)write(w,value);
                    RebuildWalls(targets);
                }));
            }
            Draw(walls.Where(w=>!w.Floor.WallsOnly),w=>w.boundaryOnly,v=>EditorGUILayout.Toggle("Boundary only",v),(w,v)=>w.boundaryOnly=v);
            Draw(walls,w=>w.familyOverride,v=>Field("Family override",v),(w,v)=>w.familyOverride=v);
            Draw(walls.Where(w=>!w.boundaryOnly && w.Family && !w.Family.IsComplex),w=>w.thicknessOverride,v=>ThicknessField("Thickness override",v),(w,v)=>w.thicknessOverride=v);
            var complex=walls.Where(w=>w.Family && w.Family.IsComplex).ToArray();
            Draw(complex,w=>w.surfaceOverride,v=>FinishField("Frame override",complex[0].Family,v),(w,v)=>w.surfaceOverride=v);
            var glazed=complex.Where(w=>w.Family.HasGlass).ToArray();
            Draw(glazed,w=>w.glassOverride,v=>FinishField("Glass override",glazed[0].Family,v,true),(w,v)=>w.glassOverride=v);
            Draw(walls,w=>w.suppressTrim,v=>EditorGUILayout.Toggle("Disable trim",v),(w,v)=>w.suppressTrim=v);
            Draw(walls.Where(w=>!w.suppressTrim),w=>w.trimOverride,v=>Field("Trim override",v),(w,v)=>w.trimOverride=v);
        }

        static void RebuildWalls(HouseWall[] walls)
        {
            foreach(var w in walls) if(!WallBuilder.Rebuild(w,out var error)) throw new InvalidOperationException(error);
            foreach(var f in walls.Select(w=>w.Floor).Distinct())
                if(!WallJunctions.Refresh(f,out var error)) throw new InvalidOperationException(error);
        }

        static void DrawHouseBatch()
        {
            var selection=HouseScene.Picks;
            if(selection.DrawFilter(HouseScene.SyncPicks))return;
            var items=selection.Items.ToArray();
            var first=items[0];
            if(first.Kind=="Wall"){DrawWallSelection();return;}
            EditorGUILayout.LabelField(first.Kind+" ("+items.Length+")",EditorStyles.boldLabel);
            if(first.OpeningId!=null) DrawOpenings(items);
            else if(first.room)
            {
                var rooms=items.Select(p=>p.room).Distinct().ToArray();
                void Draw<V>(Func<HouseRoom,V> get,Func<V,V> field,Action<HouseRoom,V> set)
                {
                    MultiField.Draw(rooms,get,field,v=>Batch("Edit rooms",rooms,()=>{
                        foreach(var r in rooms)set(r,v);
                        SlabBuilder.Rebuild(HouseScene.House);
                        foreach(var w in HouseScene.Floor.GetComponentsInChildren<HouseWall>(true))WallBuilder.ApplyMaterials(w);
                    }));
                }
                Draw(r=>r.ceilingOverride,v=>(RoomCeiling)EditorGUILayout.EnumPopup("Ceiling",v),(r,v)=>r.ceilingOverride=v);
                Draw(r=>first.ceiling?r.ceilingFinish:r.floorFinish,v=>FinishPicker.Draw("Material override",v),(r,v)=>{
                    if(first.ceiling)r.ceilingFinish=v;else r.floorFinish=v;
                });
                Draw(r=>r.wallFinish,v=>FinishPicker.Draw("Room wall finish",v),(r,v)=>r.wallFinish=v);
            }
            else if(first.cutter!=null)
            {
                var h=HouseScene.House;
                var cuts=items.Select(p=>h.booleanCuts.Find(c=>c.id==p.cutter)).Where(c=>c!=null).ToArray();
                void Draw<V>(Func<BooleanCut,V> get,Func<V,V> field,Action<BooleanCut,V> set)
                {
                    MultiField.Draw(cuts,get,field,v=>Batch("Edit boolean cuts",new[]{h},()=>{
                        foreach(var c in cuts){set(c,v);if(!c.IsValid(out var error))throw new InvalidOperationException(error);}
                        BooleanOutput.Rebuild(h);
                    }));
                }
                Draw(c=>c.shape,v=>(CutShape)EditorGUILayout.EnumPopup("Shape",v),(c,v)=>c.shape=v);
                Draw(c=>c.center,v=>EditorGUILayout.Vector3Field("Location (m)",v),(c,v)=>c.center=v);
                Draw(c=>c.rotation.eulerAngles,v=>EditorGUILayout.Vector3Field("Rotation",v),(c,v)=>c.rotation=Quaternion.Euler(v));
                Draw(c=>c.size,v=>EditorGUILayout.Vector3Field("Size (m)",v),(c,v)=>c.size=v);
                Draw(c=>1f,v=>EditorGUILayout.DelayedFloatField("Uniform scale",v),(c,v)=>c.size*=v);
                if(GUILayout.Button("Delete cuts")) DeleteHouseBatch();
            }
            else if(first.cut!=null)
            {
                var rect=EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(),new GUIContent("Move outlines X / Z (m)"));
                rect.width=(rect.width-4)*.5f;
                float x=EditorGUI.DelayedFloatField(rect,0);
                rect.x+=rect.width+4;
                var offset=new Vector2(x,EditorGUI.DelayedFloatField(rect,0));
                if(offset!=Vector2.zero) Batch("Move slab openings",items.Select(p=>p.cutOwner),()=>{
                    foreach(var p in items)
                    {
                        var cuts=p.roofCut?p.cutOwner.roofOpenings:p.cutOwner.floorOpenings;
                        var cut=cuts.Find(c=>c.id==p.cut.id);
                        cut.points=cut.points.Select(v=>PlanarNoding.AtPrecision(v+offset)).ToArray();
                    }
                    SlabBuilder.Rebuild(HouseScene.House);
                });
                // Each outline has its own vertices. Choose which outline to draw without losing access to any of them.
                foreach(var p in items)
                    if(GUILayout.Button("Edit outline "+(Array.IndexOf(items,p)+1))){HouseScene.Select(p);HouseScene.EditSelectedCut();return;}
                if(GUILayout.Button("Remove openings"))DeleteHouseBatch();
            }
            else if(first.corner!=0 && GUILayout.Button("Dissolve endpoints"))DeleteHouseBatch();
            else if(first.prop && GUILayout.Button("Edit selected props in Inspector"))
                Selection.objects=items.Select(p=>(Object)p.prop).ToArray();
        }

        static void ChooseRelated(IEnumerable<HousePick> picks)
        {
            var related=picks.ToArray();
            HouseScene.Select(default);
            foreach(var p in related)
                if(!HouseScene.Picks.Has(p)) HouseScene.Pick(p,true);
        }

        static void DrawOpenings(HousePick[] items)
        {
            var entries=items.Select(p=>(pick:p,opening:p.wall.openings.Find(o=>o.id==p.OpeningId))).Where(e=>e.opening!=null).ToArray();
            if(entries.Length==0)return;
            bool segment=items[0].segmentId!=null;
            if(!segment)
            {
                var choices=entries.Select(e=>OpeningPalette.Choices(e.pick.wall,e.opening.insert.kind).Append(e.opening.insert))
                    .Aggregate((common,next)=>common.Intersect(next)).Distinct().ToList();
                MultiField.Draw(entries,e=>e.opening.insert,v=>{
                    int index=EditorGUILayout.Popup("Insert",choices.IndexOf(v),choices.Select(c=>c.name).ToArray());
                    return index>=0 ? choices[index] : v;
                },v=>EditOpenings(items,o=>o.insert=v));
            }
            if(!segment && entries.All(e=>e.opening.insert is GarageDoorInsert))
            {
                Vector2 Clear(PlacedOpening o)=>((GarageDoorInsert)o.insert).ClearSize(o.clearSize);
                MultiField.Draw(entries,e=>Clear(e.opening).x,v=>EditorGUILayout.DelayedFloatField("Clear width (m)",v),
                    v=>EditOpenings(items,o=>o.clearSize=new Vector2(v,Clear(o).y)));
                MultiField.Draw(entries,e=>Clear(e.opening).y,v=>EditorGUILayout.DelayedFloatField("Clear height (m)",v),
                    v=>EditOpenings(items,o=>o.clearSize=new Vector2(Clear(o).x,v)));
                var first=entries[0].opening;
                int index=GarageDoorFields.Swatches((GarageDoorInsert)first.insert,entries.All(e=>e.opening.finishTile==first.finishTile)?first.finishTile:-1);
                if(index>=0)EditOpenings(items,o=>o.finishTile=index);
                if(GUILayout.Button("Randomize finishes"))EditOpenings(items,o=>o.finishTile=GarageDoorFields.RandomTile());
            }
            MultiField.Draw(entries,e=>e.opening.distance,v=>EditorGUILayout.DelayedFloatField("Distance from start (m)",v),
                v=>EditOpenings(items,o=>o.distance=v));
            float offset=EditorGUILayout.DelayedFloatField("Move along wall (m)",0);
            if(offset!=0)EditOpenings(items,o=>o.distance+=offset);
            var plain=entries.Where(e=>e.opening.insert.kind==OpeningKind.Window && !e.pick.wall.Family.panel).ToArray();
            if(!segment)MultiField.Draw(plain,e=>e.opening.sill,v=>EditorGUILayout.DelayedFloatField("Sill (m)",v),
                v=>EditOpenings(plain.Select(e=>e.pick).ToArray(),o=>o.sill=v));
            if(segment && GUILayout.Button("Select inserts"))
                ChooseRelated(items.Select(p=>new HousePick{wall=p.wall,insertId=p.OpeningId,
                    insert=p.wall.GetComponentsInChildren<InsertAttachment>(true).FirstOrDefault(a=>a.openingId==p.OpeningId)}));
            if(!segment && entries.Any(e=>e.pick.wall.Family.FindSurround(e.opening.insert)) && GUILayout.Button("Select opening segments"))
                ChooseRelated(items.Select(p=>new HousePick{wall=p.wall,segmentId=p.OpeningId}));
            if(GUILayout.Button("Select whole walls"))ChooseRelated(items.Select(p=>new HousePick{wall=p.wall}));
            if(GUILayout.Button("Remove openings"))DeleteHouseBatch();
        }

        internal static bool EditOpenings(HousePick[] items,Action<PlacedOpening> change)
        {
            var walls=items.Select(p=>p.wall).Distinct().ToArray();
            return Batch("Edit openings",walls,()=>{
                foreach(var group in items.GroupBy(p=>p.wall))
                {
                    var wall=group.Key;
                    var ids=new HashSet<string>(group.Select(p=>p.OpeningId));
                    if(change==null)wall.openings.RemoveAll(o=>ids.Contains(o.id));
                    else foreach(var o in wall.openings.Where(o=>ids.Contains(o.id)))change(o);
                    if(!HouseWall.Validate(wall.Family,wall.length,wall.Floor.height,wall.openings,new List<FittedOpening>(),out var error,resolvedThickness:wall.Thickness) ||
                        !WallJunctions.CheckOpening(wall,wall.openings,out error))throw new InvalidOperationException(error);
                }
                RebuildWalls(walls);
            });
        }

        internal static void DeleteHouseBatch()
        {
            var selection=HouseScene.Picks;
            if(selection.Mixed)return;
            var items=selection.Items.ToArray();
            if(items.Length==0)return;
            var first=items[0];bool ok=false;
            if(first.OpeningId!=null)ok=EditOpenings(items,null);
            else if(first.corner!=0)
            {
                var edit=new FloorEdit(HouseScene.Floor);
                foreach(var p in items)if(!edit.Dissolve(p.corner,out HouseScene.Message))return;
                ok=edit.Commit("Dissolve endpoints",out HouseScene.Message);
            }
            else if(first.cutter!=null)
            {
                var house=HouseScene.House;
                ok=Batch("Delete cuts",new[]{house},()=>{
                    house.booleanCuts.RemoveAll(c=>items.Any(p=>p.cutter==c.id));BooleanOutput.Rebuild(house);
                });
            }
            else if(first.cut!=null)
                ok=Batch("Remove slab openings",items.Select(p=>p.cutOwner),()=>{
                    foreach(var p in items)(p.roofCut?p.cutOwner.roofOpenings:p.cutOwner.floorOpenings).RemoveAll(o=>o.id==p.cut.id);
                    SlabBuilder.Rebuild(HouseScene.House);
                });
            if(ok)HouseScene.Select(default);
        }
    }
}
