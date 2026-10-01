"""Static garment pose checks only. Does not alter delivery GLB/FBX or author animation."""
import bpy
import bmesh
import json
import math
import hashlib
from pathlib import Path
from mathutils import Vector, Quaternion

folder = Path(__file__).parent
sources = [folder / f'04_궁병_손수정.{ext}' for ext in ('glb','fbx','blend')]
hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(sources[0]))
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bm = bmesh.new(); bm.from_mesh(body.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
static = {
    'welded_vertices':len(bm.verts),'triangles':sum(len(f.verts)-2 for f in bm.faces),
    'boundary_edges':sum(e.is_boundary for e in bm.edges),
    'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),
    'zero_area_faces':sum(f.calc_area()<1e-12 for f in bm.faces),
    'signed_volume':bm.calc_volume(signed=True),
}
unseen=set(bm.verts);components=[]
while unseen:
    todo=[unseen.pop()];count=0
    while todo:
        v=todo.pop();count+=1
        for edge in v.link_edges:
            other=edge.other_vert(v)
            if other in unseen:unseen.remove(other);todo.append(other)
    components.append(count)
static['connected_component_vertex_counts']=sorted(components,reverse=True)
bm.to_mesh(body.data);bm.free()
assert static['boundary_edges']==0 and static['nonmanifold_edges']==0
assert static['zero_area_faces']==0
rest=[v.co.copy() for v in body.data.vertices]
report={'source':sources[0].name,'static':static,'source_hashes':hashes,
        'note':'Temporary 19-bone diagnostic armature with automatic weights; no final rig, bow grip, animation, or Unity verification.'}

armature=bpy.data.armatures.new('DiagnosticOnly')
rig=bpy.data.objects.new('DiagnosticOnly_NotFinalRig',armature)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True);body.select_set(False)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
    b=armature.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=armature.edit_bones[parent]
    return b
bone('Hips',(0,0,.48),(0,0,.55))
bone('Spine',(0,0,.55),(0,0,.64),'Hips')
bone('Chest',(0,0,.64),(0,0,.775),'Spine')
bone('Neck',(0,0,.775),(0,0,.825),'Chest')
bone('Head',(0,0,.825),(0,0,.935),'Neck')
for side,s in [('L',1),('R',-1)]:
    bone(side+'_Shoulder',(s*.025,0,.775),(s*.132,.018,.743),'Chest')
    bone(side+'_Arm',(s*.132,.018,.743),(s*.267,.018,.739),side+'_Shoulder')
    bone(side+'_Forearm',(s*.267,.018,.739),(s*.402,.018,.735),side+'_Arm')
    bone(side+'_Hand',(s*.402,.018,.735),(s*.480,.024,.735),side+'_Forearm')
    bone(side+'_Thigh',(s*.065,0,.48),(s*.083,.008,.285),'Hips')
    bone(side+'_Shin',(s*.083,.008,.285),(s*.092,.015,.085),side+'_Thigh')
    bone(side+'_Foot',(s*.092,.015,.085),(s*.1,-.075,.02),side+'_Shin')
bpy.ops.object.mode_set(mode='OBJECT')
body.select_set(True)
report['auto_weight_attempts']=[]
for scale in (1,10,100):
    body.parent=None
    body.matrix_parent_inverse.identity()
    body.vertex_groups.clear()
    for modifier in list(body.modifiers):
        if modifier.type=='ARMATURE':body.modifiers.remove(modifier)
    for v in body.data.vertices:v.co*=scale
    bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='EDIT')
    for b in armature.edit_bones:b.head*=scale;b.tail*=scale
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    weights=[sum(g.weight for g in v.groups) for v in body.data.vertices]
    attempt={'calculation_scale':scale,'bones':len(armature.bones),
        'unweighted_vertices':sum(w<.001 for w in weights),
        'raw_weight_sum_min':min(weights),'raw_weight_sum_max':max(weights),
        'raw_weight_sum_errors_over_0_01':sum(abs(w-1)>.01 for w in weights)}
    if not attempt['unweighted_vertices']:
        for v,total in zip(body.data.vertices,weights):
            for group_index,weight in [(g.group,g.weight) for g in v.groups]:
                body.vertex_groups[group_index].add([v.index],weight/total,'REPLACE')
    weights=[sum(g.weight for g in v.groups) for v in body.data.vertices]
    attempt['weight_sum_errors_over_0_01']=sum(abs(w-1)>.01 for w in weights)
    report['auto_weight_attempts'].append(attempt)
    for v in body.data.vertices:v.co/=scale
    bpy.ops.object.mode_set(mode='EDIT')
    for b in armature.edit_bones:b.head/=scale;b.tail/=scale
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    print('AUTO_WEIGHT_ATTEMPT',json.dumps(attempt),flush=True)
    if not attempt['unweighted_vertices'] and not attempt['weight_sum_errors_over_0_01']:break
rig_ok=not attempt['unweighted_vertices'] and not attempt['weight_sum_errors_over_0_01']
report['diagnostic_rig']=attempt
report['pose_check_status']='performed_with_diagnostic_rig' if rig_ok else 'not_performed_auto_weights_failed'

scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH'
scene.display.shading.background_type='WORLD'
scene.world=bpy.data.worlds.new('InspectionWorld');scene.world.color=(.04,.04,.04)
camera_data=bpy.data.cameras.new('InspectionCamera');camera=bpy.data.objects.new('InspectionCamera',camera_data)
scene.collection.objects.link(camera);scene.camera=camera
camera_data.type='ORTHO';camera_data.ortho_scale=.68;camera_data.clip_start=.001
rig.hide_render=True

def reset():
    for p in rig.pose.bones:p.matrix_basis.identity()
    bpy.context.view_layer.update()
def turn(name,axis,degrees):
    p=rig.pose.bones[name];local=p.bone.matrix_local.to_3x3().inverted()@Vector(axis)
    p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion(local,math.radians(degrees))@p.rotation_quaternion

# Conservative garment band includes the shoulder wrap and upper tunic.
cape_ids={i for i,p in enumerate(rest) if .68<p.z<.82 and abs(p.x)<.19}
cape_edges=[tuple(e.vertices) for e in body.data.edges if all(i in cape_ids for i in e.vertices)]
report['garment_measurement_region']='Upper torso and shoulder wrap band: z .68-.82, abs(x)<.19; not a separately segmented cloth mesh.'
report['poses']=[]
for pose in (('Tpose','ArmsDown','DrawApprox','ArmsRaised') if rig_ok else ('Tpose',)):
    reset()
    if pose=='ArmsDown':
        turn('L_Arm',(0,1,0),65);turn('R_Arm',(0,1,0),-65)
    elif pose=='DrawApprox':
        turn('L_Arm',(0,0,1),-75);turn('L_Forearm',(0,0,1),15)
        turn('R_Arm',(0,0,1),-15)
        turn('R_Forearm',(0,1,0),20);turn('R_Forearm',(0,0,1),155)
    elif pose=='ArmsRaised':
        turn('L_Arm',(0,1,0),-35);turn('R_Arm',(0,1,0),35)
    bpy.context.view_layer.update()
    evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    positions=[evaluated.matrix_world@v.co for v in evaluated.data.vertices]
    finite=all(math.isfinite(n) for p in positions for n in p)
    assert finite
    ratios=[((positions[a]-positions[b]).length/(rest[a]-rest[b]).length,a,b) for a,b in cape_edges if (rest[a]-rest[b]).length>1e-6]
    ratios.sort(reverse=True)
    report['poses'].append({'name':pose,'finite_positions':finite,
        'garment_edge_stretch_max':ratios[0][0],
        'garment_edges_stretched_over_1_5':sum(x[0]>1.5 for x in ratios),
        'garment_edge_count':len(ratios),
        'top_stretched_edges':[{'ratio':x[0],'rest_midpoint':list((rest[x[1]]+rest[x[2]])*.5)} for x in ratios[:5]]})
    for view,offset in [('front',(0,-1.5,.1)),('back',(0,1.5,.1)),('side',(1.2,-.5,.05))]:
        center=Vector((0,0,.73));camera.location=center+Vector(offset)
        camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(folder/f'05_궁병_옷검사_{pose}_{view}.png')
        bpy.ops.render.render(write_still=True)
reset()
assert not bpy.data.actions
report['animation_actions']=len(bpy.data.actions)
assert all(hashlib.sha256(p.read_bytes()).hexdigest()==hashes[p.name] for p in sources)
report['delivered_sources_unchanged']=True
(folder/'05_궁병_옷검사.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(folder/'05_궁병_옷검사_임시리그.blend'))
print('CLOAK_REPORT',json.dumps(report,ensure_ascii=True))
