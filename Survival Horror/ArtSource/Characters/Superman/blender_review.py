"""Neutral color-only Blender inspection and geometric clearance checks."""
import bpy
import json
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def configure():
    scene=bpy.context.scene
    world=scene.world or bpy.data.worlds.new('Superman_ReviewWorld')
    scene.world=world;world.use_nodes=True
    background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
    for link in list(world.node_tree.links):
        if link.to_node==background:world.node_tree.links.remove(link)
    background.inputs['Color'].default_value=(.06,.07,.09,1)
    background.inputs['Strength'].default_value=.25
    world.color=(.06,.07,.09)
    for name,position,power in [('Review_Key',(3,-4,5),220),('Review_Fill',(-3,-1,3),100),('Review_Rim',(1,3,4),160)]:
        obj=bpy.data.objects.get(name)
        if not obj:
            data=bpy.data.lights.new(name,'AREA');obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj)
        obj.data.energy=power;obj.data.shape='DISK';obj.data.size=4
        obj.location=position;obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
    cam=bpy.data.objects.get('Review_Camera')
    if not cam:
        data=bpy.data.cameras.new('Review_Camera');cam=bpy.data.objects.new('Review_Camera',data);scene.collection.objects.link(cam)
    scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.25
    cam.location=(2,4,2.5);cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.engine='BLENDER_WORKBENCH'
    scene.display.shading.light='FLAT';scene.display.shading.color_type='TEXTURE'
    scene.display.shading.show_shadows=False;scene.display.shading.show_cavity=False
    scene.display.shading.show_specular_highlight=False
    scene.display.shading.background_type='WORLD'
    scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    scene.view_settings.exposure=0;scene.view_settings.gamma=1
    scene.render.resolution_x=900;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    for obj in scene.objects:
        if obj.type=='MESH':
            for material in obj.data.materials:
                if material and material.use_nodes:
                    bs=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
                    if bs:material.diffuse_color=bs.inputs['Base Color'].default_value
                    texture=next((n for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None)
                    if texture:material.node_tree.nodes.active=texture
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active
                space.shading.type='SOLID';space.shading.light='FLAT';space.shading.color_type='TEXTURE'
                space.shading.show_shadows=False;space.shading.show_cavity=False
                space.overlay.show_overlays=False
                space.region_3d.view_distance=2.7;space.region_3d.view_location=(0,0,1.1)
                space.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
    return cam

def render(output):
    cam=configure()
    scene=bpy.context.scene
    for name,position,focus,scale in [
        ('front',(0,-5,1),(0,0,1),2.2),('back',(0,5,1),(0,0,1),2.2),
        ('shoulders_back',(1.6,4,2.6),(0,0,1.60),.95),
        ('shoulders_front',(1.6,-4,2.2),(0,0,1.6),.95),
        ('shoulders_side',(4,0,2.1),(0,0,1.6),.95)]:
        cam.location=position;cam.rotation_euler=(Vector(focus)-cam.location).to_track_quat('-Z','Y').to_euler()
        cam.data.ortho_scale=scale
        scene.render.filepath=str(output/('color_'+name+'.png'));bpy.ops.render.render(write_still=True)
    cam.location=(1.6,4,2.6);cam.rotation_euler=(Vector((0,0,1.6))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.05

def intersections(output):
    body=bpy.data.objects['Superman_Body']
    body.data.calc_loop_triangles()
    points=[body.matrix_world@v.co for v in body.data.vertices]
    bvh=BVHTree.FromPolygons(points,[tuple(t.vertices) for t in body.data.loop_triangles],all_triangles=True)
    upper=[];crossings=[]
    for name in ['Superman_Cape','Superman_CapeAttachment']:
        cape=bpy.data.objects[name]
        for v in cape.data.vertices:
            p=cape.matrix_world@v.co
            if p.z<1.30:continue
            loc,normal,index,distance=bvh.find_nearest(p)
            upper.append({'object':name,'vertex':v.index,'point':list(p),'signed_clearance':(p-loc).dot(normal),'distance':distance})
        for edge in cape.data.edges:
            a,b=[cape.matrix_world@cape.data.vertices[i].co for i in edge.vertices]
            if max(a.z,b.z)<1.30:continue
            delta=b-a
            if delta.length<2e-5:continue
            loc,normal,index,distance=bvh.ray_cast(a+delta.normalized()*1e-5,delta.normalized(),delta.length-2e-5)
            if loc is not None:crossings.append({'object':name,'edge':list(edge.vertices),'point':list(loc)})
    report={'upper_vertices':upper,'edge_crossings':crossings}
    (output/'cape_clearance.json').write_text(json.dumps(report,indent=2))
    print('UPPER_CAPE',len(upper),'EDGE_CROSSINGS',len(crossings),'MIN_SIGNED_CLEARANCE',min(p['signed_clearance'] for p in upper))
