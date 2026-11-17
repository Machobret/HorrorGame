using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HouseToolkit.Editor
{
    public static class WallBuilder
    {
        public static bool Rebuild(HouseWall wall, out string reason)
        {
            var fits = new List<FittedOpening>();
            if (!wall.Floor) { reason = "Parent the wall to a house floor."; return false; }
            if (wall.boundaryOnly)
            {
                reason = "Remove the openings before making a wall-less boundary.";
                if (wall.openings.Count > 0) return false;
                var geometry = wall.GetComponentInChildren<WallOutput>(true);
                if (geometry) Undo.DestroyObjectImmediate(geometry.gameObject);
                reason = null;
                return true;
            }
            if (!wall.TryLayout(wall.Floor.height, fits, out reason)) return false;
            var old = wall.GetComponentInChildren<WallOutput>(true);
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject("Wall geometry");
            root.transform.SetParent(wall.transform, false);
            var output = root.AddComponent<WallOutput>();
            var family = wall.Family;
            float depth=wall.Thickness;
            output.builtPanelLayout = wall.panelLayout;
            bool even = family.panel && output.builtPanelLayout == PanelLayout.EvenSpacing;
            float targetWidth = even ? PanelFit.EvenTarget(wall.length, fits, family.panel.size.x) : 0;
            float height = wall.Floor.height;
            float cursor = 0;
            float startInset = wall.startPostInset, endInset = wall.endPostInset;
            foreach (var fit in fits)
            {
                Fill(cursor, fit.left);
                if (fit.segment)
                {
                    RenderSegment(fit.segment, fit.left, 0, fit.width, height);
                    if (height > fit.segment.size.y)
                    {
                        if (family.panel && !even && family.panels.Length == 1) Stack(family.panel, fit.left, fit.width, fit.segment.size.y);
                        else Fill(fit.left, fit.Right, fit.segment.size.y);
                    }
                }
                else if (family.panel)
                {
                    // A prefab-only custom panel occupies the entire bay from the wall base.
                    Fill(fit.left, fit.Right, fit.placement.Size.y);
                }
                else
                {
                    Plain(fit.left, 0, fit.width, fit.sill);
                    float head = fit.sill + fit.placement.Size.y;
                    Plain(fit.left, head, fit.width, height - head);
                }
                cursor = fit.Right;
            }
            Fill(cursor, wall.length);
            if (wall.Floor.House && wall.slabSpans!=null)
                foreach(var span in wall.slabSpans) Plain(span.x,height,span.y-span.x,wall.Floor.House.slabThickness);
            if (wall.Trim)
            {
                float start = 0;
                foreach (var fit in fits)
                {
                    if (fit.sill >= wall.Trim.height) continue;
                    Trim(start, fit.left);
                    start = fit.Right;
                }
                Trim(start, wall.length);
            }

            void Trim(float left, float right)
            {
                if (right-left <= .0001f) return;
                var trim = wall.Trim;
                foreach (int side in new[]{-1,1})
                {
                    var go = new GameObject(trim.name);
                    go.transform.SetParent(root.transform,false);
                    var mesh=trim.profile ? trim.profile : Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    var b=mesh.bounds;
                    if(b.size.x<=0 || b.size.y<=0 || b.size.z<=0) continue;
                    var scale=new Vector3((right-left)/b.size.x,trim.height/b.size.y,trim.depth/b.size.z);
                    go.transform.localScale=scale;
                    go.transform.localPosition=new Vector3((right+left)/2,trim.height/2,side*(depth+trim.depth)/2)-Vector3.Scale(scale,b.center);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial=trim.material ? trim.material : family.surface;
                }
            }

            Undo.RegisterCreatedObjectUndo(root, "Build wall");
            SyncInserts(wall, fits);
            ApplyMaterials(wall);
            if (wall.Floor.House && wall.Floor.House.booleanCuts.Count > 0)
            {
                BooleanOutput.Apply(wall.Floor.House, output);
                BooleanOutput.ApplyWindows(wall.Floor.House, wall);
            }
            return true;

            void Fill(float start, float end, float bottom = 0)
            {
                float width = end - start;
                if (width <= 0.00001f) return;
                if (!family.panel) { Plain(start, bottom, width, height - bottom); return; }
                if (even)
                {
                    var panel = family.panel;
                    int count = PanelFit.EvenCount(width, panel.size.x, targetWidth);
                    for (int i = 0; i < count; i++)
                    {
                        float leftEdge = start + width * ((float)i / count);
                        float rightEdge = i + 1 == count ? end : start + width * ((i + 1f) / count);
                        Stack(panel, leftEdge, rightEdge - leftEdge, bottom);
                    }
                    return;
                }
                var pieces = PanelFit.Choose(family.panels, width);
                double nominal = 0;
                foreach (var piece in pieces) nominal += piece.size.x;
                double offset = 0;
                float left = start;
                for (int i = 0; i < pieces.Count; i++)
                {
                    offset += pieces[i].size.x;
                    float right = i == pieces.Count - 1 ? end : start + (float)(width * offset / nominal);
                    Stack(pieces[i], left, right - left, bottom);
                    left = right;
                }
            }
            void Stack(WallSegment panel, float left, float width, float bottom)
            {
                for (int row = 0; bottom + row * panel.size.y < height - 0.00001f; row++)
                {
                    float y = bottom + row * panel.size.y;
                    RenderSegment(panel, left, y, width, height - y);
                }
            }
            void RenderSegment(WallSegment segment, float left, float baseY, float width, float available)
            {
                float scale = width / segment.size.x;
                if (segment.mesh)
                {
                    var m=segment.mesh;var b=m.bounds;
                    float lo=Mathf.Clamp((startInset-left)/scale,0,segment.size.x);
                    float hi=Mathf.Clamp((wall.length-endInset-left)/scale,0,segment.size.x);
                    if(hi-lo<.00001f)return;
                    var go=new GameObject(segment.name);
                    go.transform.SetParent(root.transform,false);
                    go.transform.localScale=new Vector3(scale,1,1);
                    go.transform.localPosition=new Vector3(left-b.min.x*scale,baseY-b.min.y,-b.center.z);
                    var part=go.AddComponent<AuthoredPanel>();
                    part.source=m;
                    part.limits=new Vector3(b.min.x+lo,b.min.x+hi,b.min.y+Mathf.Min(segment.size.y,available));
                    part.glazing=new Vector2(b.min.x+Mathf.Clamp((wall.startCornerClearance-left)/scale,lo,hi),
                        b.min.x+Mathf.Clamp((wall.length-wall.endCornerClearance-left)/scale,lo,hi));
                    part.Refresh();
                    output.authored.Add(go.GetComponent<MeshRenderer>());
                    return;
                }
                var opaque = new List<Bounds>();
                var panes = new List<Bounds>();
                foreach (var part in segment.parts)
                {
                    var b = part.bounds;
                    float bottom = Mathf.Max(0, b.min.y);
                    float top = Mathf.Min(b.max.y, available, segment.size.y);
                    if (top <= bottom + 0.00001f) continue;
                    float x0 = startInset > left ? Mathf.Max(b.min.x,(startInset-left)/scale) : b.min.x;
                    float x1 = left+width > wall.length-endInset+.00001f ? Mathf.Min(b.max.x,(wall.length-endInset-left)/scale) : b.max.x;
                    if (x1 <= x0 + .00001f) continue;
                    if (part.glass)
                    {
                        // Continue the opaque jamb to the first clear pane. At tight turns
                        // intersecting wall strips may run alongside each other beyond the post.
                        float paneStart=Mathf.Clamp((wall.startCornerClearance-left)/scale,x0,x1);
                        float paneEnd=Mathf.Clamp((wall.length-wall.endCornerClearance-left)/scale,x0,x1);
                        void AddJamb(float from,float to)
                        {
                            if (to<=from+.00001f) return;
                            opaque.Add(new Bounds(new Vector3((from+to)*.5f,(top+bottom)*.5f,b.center.z),
                                new Vector3(to-from,top-bottom,b.size.z)));
                        }
                        if (paneStart>=paneEnd) { AddJamb(x0,x1); continue; }
                        AddJamb(x0,paneStart); AddJamb(paneEnd,x1);
                        x0=paneStart; x1=paneEnd;
                    }
                    if (x1 <= x0 + .00001f) continue;
                    var clipped = b;
                    clipped.SetMinMax(new Vector3(x0,bottom,b.min.z),new Vector3(x1,top,b.max.z));
                    (part.glass ? panes : opaque).Add(clipped);
                }
                if (opaque.Count > 0)
                {
                    var frame = new GameObject(segment.name + " frame");
                    frame.transform.SetParent(root.transform,false);
                    frame.transform.localPosition = new Vector3(left,baseY,0);
                    frame.transform.localScale = new Vector3(scale,1,1);
                    var component = frame.AddComponent<SegmentFrame>();
                    component.parts = opaque.ToArray();
                    component.Refresh();
                    foreach (var bounds in opaque)
                    {
                        var collider = frame.AddComponent<BoxCollider>();
                        collider.center = bounds.center; collider.size = bounds.size;
                    }
                    output.surfaces.Add(frame.GetComponent<MeshRenderer>());
                }
                foreach (var pane in panes)
                    Box(new Vector3(left+pane.center.x*scale,baseY+pane.center.y,pane.center.z),
                        new Vector3(pane.size.x*scale,pane.size.y,pane.size.z),true);
            }
            void Plain(float x, float y, float width, float h)
            {
                if (h <= 0.00001f || width <= 0.00001f) return;
                float start = Mathf.Max(x,startInset);
                float end = Mathf.Min(x+width,wall.length-endInset);
                if (end > start + .00001f)
                    Box(new Vector3((start+end)/2, y+h/2, 0), new Vector3(end-start,h,depth), false);
            }
            void Box(Vector3 center, Vector3 size, bool glass)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = glass ? "Glass" : family.panel ? "Frame" : "Wall";
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = center;
                box.transform.localScale = size;
                if (glass)
                {
                    // Use a single shared XY quad; the box collider retains its authored depth.
                    box.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                }
                (glass ? output.panes : output.surfaces).Add(box.GetComponent<MeshRenderer>());
            }
        }

        public static void ApplyMaterials(HouseWall wall)
        {
            var output = wall.GetComponentInChildren<WallOutput>(true);
            if (!output || !wall.Family) return;
            var surface = WallFaces.StructuralFinish(wall);
            var glazing = wall.glassOverride ? wall.glassOverride : wall.Floor.GlassFor(wall.interior) ? wall.Floor.GlassFor(wall.interior) : wall.Family.glass;
            if (wall.Family.IsComplex) Apply(output.surfaces,surface);
            else WallFaces.Apply(wall, output);
            Apply(output.panes,glazing);
            foreach(var renderer in output.authored)
            {
                if(!renderer)continue;
                int count=renderer.GetComponent<AuthoredPanel>().source.subMeshCount;
                var previous=renderer.sharedMaterials;
                if(previous.Length==count && previous[0]==surface && (count==1 || previous[1]==glazing))continue;
                renderer.sharedMaterials=count==1 ? new[]{surface} : new[]{surface,glazing};
                EditorUtility.SetDirty(renderer);
            }

            foreach (var binding in wall.GetComponentsInChildren<InsertAttachment>(true))
            {
                var renderer = binding.finishRenderer;
                if (!renderer) continue;
                var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                if (!source) continue;
                var current = renderer.sharedMaterials;
                var defaults = source.sharedMaterials;
                if (current.Length < 2 || defaults.Length < 2) continue;
                var frame = wall.Family.panel ? surface : defaults[0];
                var glass = wall.Family.panel ? wall.glassOverride ? wall.glassOverride : wall.Floor.GlassFor(wall.interior) ? wall.Floor.GlassFor(wall.interior) : wall.Family.glass : defaults[1];
                if (current[0] == binding.lastFrame) current[0] = frame;
                if (current[1] == binding.lastGlass) current[1] = glass;
                var previous = renderer.sharedMaterials;
                if (current[0] != previous[0] || current[1] != previous[1])
                {
                    renderer.sharedMaterials = current;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    EditorUtility.SetDirty(renderer);
                }
                if (binding.lastFrame != frame || binding.lastGlass != glass)
                {
                    binding.lastFrame = frame;
                    binding.lastGlass = glass;
                    EditorUtility.SetDirty(binding);
                }
            }

            if (wall.Floor.House) wall.Floor.House.InvalidateProjection();
            if (wall.Floor.House && wall.Floor.House.booleanCuts.Count > 0) BooleanOutput.RefreshMaterials(wall.Floor.House, wall);

            static void Apply(List<MeshRenderer> renderers, Material material)
            {
                foreach (var renderer in renderers)
                    if (renderer && renderer.sharedMaterial != material)
                    {
                        renderer.sharedMaterial = material;
                        EditorUtility.SetDirty(renderer);
                    }
            }
        }

        static void SyncInserts(HouseWall wall, List<FittedOpening> fits)
        {
            var previous = new List<InsertAttachment>(wall.GetComponentsInChildren<InsertAttachment>(true));
            foreach (var fit in fits)
            {
                var placement = fit.placement;
                if (!placement.HasPrefab) continue;
                var prefab = placement.insert.prefab;
                var attachment = previous.Find(item => item.openingId == placement.id && item.sourcePrefab == prefab);
                if (attachment)
                {
                    previous.Remove(attachment);
                    Undo.RecordObject(attachment.transform, "Move insert");
                }
                else
                {
                    var root = new GameObject(prefab.name + " attachment");
                    root.transform.SetParent(wall.transform, false);
                    attachment = root.AddComponent<InsertAttachment>();
                    attachment.openingId = placement.id;
                    attachment.sourcePrefab = prefab;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    instance.transform.localPosition = Vector3.zero;
                    Undo.RegisterCreatedObjectUndo(root, "Place insert");
                }
                if (placement.insert is GarageDoorInsert garage)
                {
                    var door=attachment.GetComponentInChildren<GarageDoor>(true);
                    if (door && door.Configure(garage.ClearSize(placement.clearSize),placement.finishTile,garage.operable))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                }
                var target = placement.insert.finishRenderer;
                if (target && !attachment.finishRenderer)
                    foreach (var renderer in attachment.GetComponentsInChildren<Renderer>(true))
                        if (PrefabUtility.GetCorrespondingObjectFromSource(renderer) == target)
                        {
                            var slots = target.sharedMaterials;
                            if (slots.Length < 2) break;
                            attachment.finishRenderer = renderer;
                            attachment.lastFrame = slots[0];
                            attachment.lastGlass = slots[1];
                            EditorUtility.SetDirty(attachment);
                            break;
                        }
                var facing = placement.reversed ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                attachment.transform.SetLocalPositionAndRotation(new Vector3(placement.distance, fit.sill, 0), facing);
            }
            foreach (var item in previous) Undo.DestroyObjectImmediate(item.gameObject);
        }

        public static bool SetHeight(HouseFloor floor, float value, out string reason)
        {
            var walls = floor.GetComponentsInChildren<HouseWall>(true);
            var fits = new List<FittedOpening>();
            if (!float.IsFinite(value) || value <= 0) { reason = "Floor height must be positive."; return false; }
            foreach (var wall in walls)
                if (!wall.boundaryOnly && !wall.TryLayout(value, fits, out reason)) return false;
            Undo.RecordObject(floor, "Change floor height");
            float change = value - floor.height;
            if (floor.House && change != 0)
                foreach (var upper in HouseEditing.Floors(floor.House))
                    if (upper.transform.localPosition.y > floor.transform.localPosition.y)
                    {
                        Undo.RecordObject(upper.transform,"Adjust upper floor");
                        upper.transform.localPosition += Vector3.up * change;
                    }
            floor.height = value;
            foreach (var wall in walls) Rebuild(wall, out _);
            if (!WallJunctions.Refresh(floor,out reason)) return false;
            SlabBuilder.Rebuild(floor.House);
            HouseScene.RefocusIfMoved();
            EditorUtility.SetDirty(floor);
            reason = null;
            return true;
        }

        public static bool Place(HouseWall wall, HouseInsert insert, float distance, float sill, out string reason)
            => Place(wall, insert, distance, sill, out reason, false);

        public static bool Place(HouseWall wall, HouseInsert insert, float distance, float sill, out string reason, bool reversed, Vector2 clearSize=default, int finishTile=0, bool empty=false)
        {
            if (!wall || !wall.Floor || wall.boundaryOnly) { reason="Choose a physical wall."; return false; }
            var candidate = new PlacedOpening { id = Guid.NewGuid().ToString("N"), insert = insert, distance = distance, sill = sill, reversed = reversed, clearSize=clearSize, finishTile=finishTile, empty=empty };
            if (!wall.TryLayout(wall.Floor.height, new List<FittedOpening>(), out reason, candidate)) return false;
            var proposed=new List<PlacedOpening>(wall.openings){candidate};
            if (!WallJunctions.CheckOpening(wall,proposed,out reason)) return false;
            Undo.RecordObject(wall, "Place opening");
            wall.openings.Add(candidate);
            EditorUtility.SetDirty(wall);
            if (!Rebuild(wall, out reason)) return false;
            return WallJunctions.Refresh(wall.Floor,out reason);
        }
    }
}
