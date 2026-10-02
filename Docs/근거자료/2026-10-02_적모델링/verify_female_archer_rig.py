import numpy as np
"""Reimport both delivery formats and exercise the actual exported skin bindings."""
import bpy,bmesh,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Quaternion
from mathutils.kdtree import KDTree

F=Path(__file__).parent;stem='03_궁병_손보정_리깅'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(F/'02_궁병여성_VARCO_손수정.glb'))
source=next(o for o in bpy.data.objects if o.type=='MESH')
original=[source.matrix_world@v.co for v in source.data.vertices]
floor=min(p.z for p in original)
protected=[p-Vector((0,0,floor)) for p in original if abs(p.x)<.408]
all_reports=[]
for ext in ('glb','fbx'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=F/(stem+'.'+ext)
    if ext=='glb':bpy.ops.import_scene.gltf(filepath=str(path))
    else:bpy.ops.import_scene.fbx(filepath=str(path))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    body=next(o for o in bpy.data.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers))
    mesh=body.data
    def reset():
        for b in rig.pose.bones:b.matrix_basis.identity()
        bpy.context.view_layer.update()
    def positions():return [body.matrix_world@v.co for v in body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices]
    def turn(name,axis,degrees):
        p=rig.pose.bones[name];local=(rig.matrix_world@p.bone.matrix_local).to_3x3().inverted()@Vector(axis)
        p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion(local,math.radians(degrees))
    reset();rest=positions()
    tree=KDTree(len(rest))
    for i,p in enumerate(rest):tree.insert(p,i)
    tree.balance()
    protected_error=max(tree.find(p)[2] for p in protected)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
    report={'format':ext,'bytes':path.stat().st_size,'bones':len(rig.data.bones),
      'finger_bones':sum(any(s in b.name for s in ('Proximal','Intermediate','Distal')) for b in rig.data.bones),
      'triangles':sum(len(p.vertices)-2 for p in mesh.polygons),'boundary_edges':sum(e.is_boundary for e in bm.edges),
      'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'zero_area_faces':sum(f.calc_area()<1e-12 for f in bm.faces),
      'unweighted_vertices':sum(sum(g.weight for g in v.groups)<.001 for v in mesh.vertices),
      'weight_sum_errors':sum(abs(sum(g.weight for g in v.groups)-1)>.0001 for v in mesh.vertices),
      'maximum_influences':max(len(v.groups) for v in mesh.vertices),
      'protected_body_position_error':protected_error,'minimum_z':min(p.z for p in rest),
      'actions':len(bpy.data.actions),'textures':[{'size':list(i.size),'packed':bool(i.packed_file)} for i in bpy.data.images if i.type=='IMAGE' and i.size[0]],'finger_tests':[]}
    bm.free()
    assert report['bones']==53 and report['finger_bones']==30
    assert report['boundary_edges']==report['nonmanifold_edges']==report['zero_area_faces']==0
    assert report['unweighted_vertices']==report['weight_sum_errors']==0
    assert report['maximum_influences']<=4 and report['actions']==0
    report['texture_pixel_errors']={}
    for node in body.data.materials[0].node_tree.nodes:
        if node.type!='TEX_IMAGE' or not node.image:continue
        img=node.image;label='Normal' if img.colorspace_settings.name=='Non-Color' else 'BaseColor'
        expected=bpy.data.images.load(str(F/(stem+'_'+label+'.png')),check_existing=False)
        expected.colorspace_settings.name=img.colorspace_settings.name
        a=np.array(img.pixels[:]);b=np.array(expected.pixels[:]);error=float(np.max(abs(a-b)))
        report['texture_pixel_errors'][label]=error
        assert error<.005,(ext,label,error)
        bpy.data.images.remove(expected)
    assert set(report['texture_pixel_errors'])=={'BaseColor','Normal'}
    print('BASE_CHECK',json.dumps({k:v for k,v in report.items() if k!='finger_tests'}),flush=True)
    # Exported inverse-bind matrices are float32: tolerate 1/100,000 model unit.
    assert protected_error<1e-5 and abs(report['minimum_z'])<1e-5,(protected_error,report['minimum_z'])
    for side in ('Left','Right'):
        for name in ('Thumb','Index','Middle','Ring','Little'):
            reset();first=rig.data.bones[side+name+'Proximal']
            direction=(rig.matrix_world.to_3x3()@(first.tail_local-first.head_local)).normalized()
            axis=direction.cross(Vector((0,0,-1))).normalized()
            for suffix,deg in zip(('Proximal','Intermediate','Distal'),(35,45,25)):turn(side+name+suffix,axis,deg)
            bpy.context.view_layer.update();pts=positions();delta=[(p-q).length for p,q in zip(pts,rest)]
            opposite=max(d for d,p in zip(delta,rest) if (p.x<0 if side=='Left' else p.x>0))
            moved=sum(d>1e-5 for d in delta)
            assert moved>0 and opposite<1e-5
            report['finger_tests'].append({'finger':side+name,'moved_vertices':moved,'opposite_side_error':opposite})
    reset()
    for name,axis,degrees in [('LeftUpperArm',(0,1,0),65),('RightUpperArm',(0,1,0),-65),('LeftLowerArm',(0,0,1),90),('RightLowerArm',(0,0,1),-90),('LeftUpperLeg',(1,0,0),25),('LeftLowerLeg',(1,0,0),-55)]:turn(name,axis,degrees)
    bpy.context.view_layer.update();pts=positions()
    assert all(math.isfinite(c) for p in pts for c in p)
    report['arm_leg_pose_finite']=True
    if ext=='fbx':
        scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
        scene.display.shading.color_type='TEXTURE';scene.display.shading.light='STUDIO';scene.display.shading.show_cavity=True
        scene.display.shading.background_type='WORLD';scene.world=bpy.data.worlds.new('CheckWorld');scene.world.color=(.04,.04,.04)
        data=bpy.data.cameras.new('Check');cam=bpy.data.objects.new('Check',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO';data.ortho_scale=1.13
        center=Vector((0,0,.52));cam.location=center+Vector((.5,-2,.2));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(F/(stem+'_FBX_재검사.png'));bpy.ops.render.render(write_still=True)
    all_reports.append(report)
(F/(stem+'_내보내기검사.json')).write_text(json.dumps(all_reports,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXPORT_CHECK',json.dumps(all_reports,ensure_ascii=True),flush=True)
