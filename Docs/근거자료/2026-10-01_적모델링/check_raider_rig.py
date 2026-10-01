"""Blender background inspection. Source GLB/FBX is never overwritten."""
import bpy
import json
import sys
import math
from pathlib import Path
from collections import Counter
from mathutils import Vector, Quaternion

folder = Path(__file__).parent
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
source = folder / (args[0] if args else '02_약탈자_손가락리깅.glb')
bpy.ops.wm.read_factory_settings(use_empty=True)
if source.suffix.lower() == '.fbx':
    bpy.ops.import_scene.fbx(filepath=str(source))
else:
    bpy.ops.import_scene.gltf(filepath=str(source))
all_meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
meshes = [o for o in all_meshes if any(m.type == 'ARMATURE' for m in o.modifiers)]
for helper in all_meshes:
    if helper not in meshes:
        helper.hide_render = True
rigs = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
assert len(rigs) == 1, f'Expected one armature, got {len(rigs)}'
rig = rigs[0]
weights = Counter()
unweighted = 0
bad_sums = 0
for mesh in meshes:
    for vertex in mesh.data.vertices:
        total = sum(g.weight for g in vertex.groups)
        unweighted += total < 0.001
        bad_sums += abs(total - 1) > 0.01
        for g in vertex.groups:
            if g.weight > 0.01:
                weights[mesh.vertex_groups[g.group].name] += 1
report = {
    'source': source.name,
    'meshes': len(meshes),
    'vertices': sum(len(o.data.vertices) for o in meshes),
    'triangles': sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),
    'unweighted_vertices': unweighted,
    'weight_sum_errors_over_0_01': bad_sums,
    'armature': rig.name,
    'excluded_import_helpers': [o.name for o in all_meshes if o not in meshes],
    'bones': [{'name': b.name, 'parent': b.parent.name if b.parent else None,
               'head': list(rig.matrix_world @ b.head_local),
               'tail': list(rig.matrix_world @ b.tail_local),
               'weighted_vertices_over_0_01': weights[b.name]}
              for b in rig.data.bones],
    'actions': [a.name for a in bpy.data.actions],
}
output = folder / ('02_리깅검사_' + source.suffix[1:] + '.json')
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('RIG_REPORT', json.dumps({k:v for k,v in report.items() if k != 'bones'}, ensure_ascii=True))
print('BONES', len(rig.data.bones), 'FINGER_BONES', sum('Finger' in b.name for b in rig.data.bones))

if '--render' in args:
    assert source.suffix == '.glb', 'Diagnostic posing uses the inspected GLB skeleton'
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (0.65, 0.68, 0.73)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.background_type = 'WORLD'
    scene.world = bpy.data.worlds.new('InspectionWorld')
    scene.world.color = (0.055, 0.055, 0.055)
    camera_data = bpy.data.cameras.new('InspectionCamera')
    camera = bpy.data.objects.new('InspectionCamera', camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 0.19
    camera_data.clip_start = 0.001
    camera_data.clip_end = 10
    def reset():
        for b in rig.pose.bones:
            b.matrix_basis.identity()
        bpy.context.view_layer.update()
    def curl(side, finger, factor=1.0):
        prefix = f'HandLoRA_Bip01_{side}_Finger{finger}'
        first = rig.data.bones[prefix]
        direction = (first.tail_local-first.head_local).normalized()
        # Diagnostic flexion toward the palm. This is not an authored weapon grip.
        toward = Vector((0, 0, -1)) if finger else Vector((0, 1, -0.5))
        axis = direction.cross(toward).normalized()
        angles = (45,60,35) if finger else (20,30,15)
        for suffix, degrees in zip(('', '1', '2'), angles):
            b = rig.pose.bones[prefix+suffix]
            local_axis = b.bone.matrix_local.to_3x3().inverted() @ axis
            b.rotation_mode = 'QUATERNION'
            b.rotation_quaternion = Quaternion(local_axis, math.radians(degrees)*factor)
        bpy.context.view_layer.update()
    def positions():
        dg = bpy.context.evaluated_depsgraph_get()
        return [o.matrix_world @ v.co for o in meshes for v in o.evaluated_get(dg).data.vertices]
    reset()
    rest = positions()
    isolated = []
    for side in ('L','R'):
        for finger in range(5):
            reset()
            curl(side,finger)
            posed = positions()
            delta = [(a-b).length for a,b in zip(posed,rest)]
            opposite = [d for d,p in zip(delta,rest) if (p.x < 0 if side=='L' else p.x > 0)]
            isolated.append({'side':side,'finger':finger,'moved_vertices':sum(d>1e-5 for d in delta),
                             'max_displacement':max(delta),'opposite_side_max':max(opposite)})
    report['isolated_finger_tests'] = isolated
    assert all(t['moved_vertices'] > 0 for t in isolated)
    assert all(t['opposite_side_max'] < 1e-5 for t in isolated)
    report['pose_note'] = 'Synthetic diagnostic flexion; not a final dagger grip or game animation.'
    for pose in ('open','half','curl','index'):
        reset()
        if pose in ('half','curl','index'):
            for side in ('L','R'):
                for finger in ([1] if pose == 'index' else range(5)):
                    curl(side,finger,0.65 if pose=='half' else 1.0)
        for side, sign in (('L',1),('R',-1)):
            center = Vector((sign*0.437,0.022,0.705))
            for view,offset in [('back',(sign*0.10,-0.15,0.35)),('palm',(sign*0.13,-0.16,-0.28))]:
                camera.location = center + Vector(offset)
                camera.rotation_euler = (center-camera.location).to_track_quat('-Z','Y').to_euler()
                scene.render.filepath = str(folder / f'02_hand_{side}_{pose}_{view}.png')
                bpy.ops.render.render(write_still=True)
    reset()
    output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('FINGER_TESTS',json.dumps(isolated))
