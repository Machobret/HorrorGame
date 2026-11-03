"""Build a body-fitted cape yoke and a single cloth surface using the source dimensions."""
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def build(body,cape,mask):
    # Triangulate once and reuse the exact triangles for the body and yoke. Two
    # independently triangulated bent quads can intersect despite matching corners.
    bm=bmesh.new();bm.from_mesh(body.data)
    selected=bm.faces.layers.int.new('CapeAttachment')
    for face in bm.faces:
        center=sum((v.co for v in face.verts),Vector())/len(face.verts)
        face[selected]=int(center.z+.55*center.y>=1.56 and center.z<=1.70 and min(v.co.z for v in face.verts)>=1.50)
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='FIXED',ngon_method='EAR_CLIP')
    bm.to_mesh(body.data);bm.free()
    body.data.update();body.data.calc_loop_triangles()
    triangles=[tuple(t.vertices) for t in body.data.loop_triangles]
    bvh=BVHTree.FromPolygons([v.co for v in body.data.vertices],triangles,all_triangles=True)
    authored=[v.co.copy() for v,w in zip(cape.data.vertices,mask) if w>0]
    bottom=min(p.z for p in authored)
    width=max(abs(p.x) for p in authored)
    columns,rows=12,18
    points=[];faces=[]
    for row in range(rows+1):
        t=row/rows
        half_width=.14+(width-.14)*(1-(1-t)**2)
        if row<3:half_width=.12
        for column in range(columns+1):
            p=Vector(((column/columns*2-1)*half_width,.24+.035*t,1.67*(1-t)+bottom*t))
            hit,_,_,_=bvh.ray_cast(Vector((p.x,2,p.z)),Vector((0,-1,0)),4)
            if hit is not None:p.y=hit.y+.08 if row<3 else max(p.y,hit.y+.06)
            points.append(p)
    for row in range(rows):
        for column in range(columns):
            a=row*(columns+1)+column;b=a+columns+1
            faces.append([a,a+1,b+1,b])
    guard_points=points[:3*(columns+1)]
    guard_faces=faces[:2*columns]
    cut=2*(columns+1)
    cloth_points=points[cut:]
    cloth_faces=[[i-cut for i in f] for f in faces[2*columns:]]
    data=bpy.data.meshes.new('Superman_CapeSheet');data.from_pydata(cloth_points,[],cloth_faces);data.update();data.uv_layers.new(name='UVMap')
    material=cape.data.materials[0];data.materials.append(material)
    cape.data=data;cape.vertex_groups.clear()
    chest=cape.vertex_groups.new(name='DEF-spine.003');chest.add(list(range(len(cloth_points))),1,'REPLACE')
    pinned_count=columns+1
    simulated=cape.vertex_groups.new(name='Simulated');simulated.add(list(range(pinned_count,len(cloth_points))),1,'REPLACE')
    result_mask=[0.0]*pinned_count+[1.0]*(len(cloth_points)-pinned_count)

    # Copy the body's surface and skinning for the shoulder attachment. The lower
    # boundary slopes upward toward the front, keeping the chest emblem visible.
    positions=[];weights=[];shell_faces=[]
    selected=body.data.attributes['CapeAttachment']
    for polygon in body.data.polygons:
        triangle=list(polygon.vertices)
        if not selected.data[polygon.index].value:continue
        ids=[]
        for i in triangle:
            vertex=body.data.vertices[i]
            ids.append(len(positions));positions.append(vertex.co+vertex.normal*.022)
            weights.append({g.group:g.weight for g in vertex.groups})
        shell_faces.append(ids)
    guard_start=len(positions)
    positions.extend(guard_points)
    weights.extend([{body.vertex_groups['DEF-spine.003'].index:1.0} for _ in guard_points])
    shell_faces.extend([[guard_start+i for i in f] for f in guard_faces])
    shell_data=bpy.data.meshes.new('Superman_CapeAttachment');shell_data.from_pydata(positions,[],shell_faces);shell_data.update();shell_data.materials.append(material)
    shell_data.uv_layers.new(name='UVMap')
    role=shell_data.uv_layers.new(name='AttachmentRole')
    for loop in shell_data.loops:role.data[loop.index].uv=(1 if loop.vertex_index>=guard_start else 0,0)
    shell=bpy.data.objects.new('Superman_CapeAttachment',shell_data);bpy.context.collection.objects.link(shell)
    shell.parent=cape.parent
    for g in body.vertex_groups:shell.vertex_groups.new(name=g.name)
    for i,values in enumerate(weights):
        selected=sorted(values.items(),key=lambda p:p[1],reverse=True)[:4];total=sum(w for _,w in selected)
        for group,weight in selected:
            if weight>0:shell.vertex_groups[group].add([i],weight/total,'REPLACE')
    modifier=shell.modifiers.new('Player skin','ARMATURE');modifier.object=cape.parent
    shell['attachment']='Body-fitted skinned yoke; CPU cloth is restricted to the author-selected hanging sheet.'
    return shell,result_mask
