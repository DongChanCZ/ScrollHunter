"""Render the mage's left hand with all fingers curled (Blender pose bones) for before/after weight checks.
Run: blender -b --factory-startup --python render_mage_hand.py -- <file.blend> <out.png> [curl]
"""
import bpy, math, sys
from mathutils import Vector, Quaternion

argv = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=argv[0])
OUT, CURL = argv[1], float(argv[2]) if len(argv) > 2 else 1.0
body = bpy.data.objects['Mage']; rig = bpy.data.objects['MageRig']; arm = rig.data
for b in rig.pose.bones: b.matrix_basis.identity()
for f in ('Thumb', 'Index', 'Middle', 'Ring', 'Little'):
    head = arm.bones['Left' + f + 'Proximal'].head_local; tail = arm.bones['Left' + f + 'Proximal'].tail_local
    axis = (tail - head).normalized().cross(Vector((0, -1, 0))).normalized()
    for s, deg in zip(('Proximal', 'Intermediate', 'Distal'), (35, 45, 25)):
        pb = rig.pose.bones['Left' + f + s]; local = pb.bone.matrix_local.to_3x3().inverted() @ axis
        pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(local, math.radians(deg * CURL * (0.5 if f == 'Thumb' else 1)))
bpy.context.view_layer.update()
scene = bpy.context.scene; scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = scene.render.resolution_y = 600
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'TEXTURE'; scene.display.shading.show_cavity = True
cam_data = bpy.data.cameras.new('C'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = .13; cam_data.clip_start = .001
cam = bpy.data.objects.new('C', cam_data); scene.collection.objects.link(cam); scene.camera = cam
hand = rig.matrix_world @ arm.bones['LeftHand'].head_local
center = hand + Vector((.045, 0, 0))
import os
base, ext = os.path.splitext(OUT)
for view, off in (('바닥', (0, -.4, -.05)), ('옆', (0, 0, .4)), ('끝', (.4, -.05, 0))):
    cam.location = center + Vector(off); cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = base + '_' + view + ext; bpy.ops.render.render(write_still=True)
print('RENDERED', OUT, flush=True)
