"""Mage finger weights (2026-10-06). Source 09_마법사_보정_리깅.blend is read only.

Problem: 09 used one wide Gaussian per phalanx (sigma = 25% of the whole finger) plus a hand blend over the
first 25%, so every finger vertex follows three bones and the hand at once. Bending then collapses the
finger volume (flat, stretched fingers in Unity grip poses).
Fix: each vertex follows the phalanx it lies on; neighbours blend only near a joint (+-JOINT of a segment,
50/50 exactly at the joint). Only vertices that 09 gave explicit finger weights are changed (|x| >= .407).
Bones, bind pose, mesh, UV and textures are unchanged.

Run: blender -b --factory-startup --python fix_mage_finger_weights.py -- <source.blend> <out_dir>
"""
import bpy, math, sys, json
from mathutils import Vector, Quaternion
from pathlib import Path

argv = sys.argv[sys.argv.index('--') + 1:]
SRC, OUT = Path(argv[0]), Path(argv[1])
NAME = '10_마법사_손가락가중치'
JOINT = .18          # blend half-width as a fraction of the segment length
HAND_X = .407        # 09 explicit finger weights started at |x| >= .407

bpy.ops.wm.open_mainfile(filepath=str(SRC))
body = bpy.data.objects['Mage']; rig = bpy.data.objects['MageRig']; arm = rig.data
mesh = body.data
FINGERS = ('Thumb', 'Index', 'Middle', 'Ring', 'Little')
SEGS = ('Proximal', 'Intermediate', 'Distal')


def chain(side, finger):
    """Rest-pose joint points in the mesh's object space: knuckle, two joints, tip."""
    m = body.matrix_world.inverted() @ rig.matrix_world
    bones = [arm.bones[side + finger + s] for s in SEGS]
    return [m @ bones[0].head_local] + [m @ b.tail_local for b in bones]


chains = {(s, f): chain(s, f) for s in ('Left', 'Right') for f in FINGERS}


def nearest(p, pts):
    best = None
    for i, (a, b) in enumerate(zip(pts, pts[1:])):
        ab = b - a; raw = (p - a).dot(ab) / ab.length_squared; t = min(1., max(0., raw))
        d = (p - (a + ab * t)).length
        if best is None or d < best[0]: best = (d, i, raw)
    return best


def weights_for(p):
    side = 'Left' if p.x > 0 else 'Right'
    finger, (d, i, raw) = min(((f, nearest(p, chains[(side, f)])) for f in FINGERS), key=lambda x: x[1][0])
    names = [side + 'Hand'] + [side + finger + s for s in SEGS]
    own, w = i + 1, {}
    if raw < JOINT and own >= 1:                     # near the joint towards the hand
        k = 0.5 * max(0., 1 - raw / JOINT) if raw >= 0 else 0.5 + 0.5 * min(1., -raw / JOINT)
        w[names[own - 1]] = k; w[names[own]] = 1 - k
    elif raw > 1 - JOINT and own < 3:                # near the joint towards the tip
        k = 0.5 * min(1., (raw - (1 - JOINT)) / JOINT)
        w[names[own + 1]] = k; w[names[own]] = 1 - k
    else:
        w[names[own]] = 1.
    return w


def set_weights(v, w):
    for g in list(v.groups): body.vertex_groups[g.group].remove([v.index])
    w = {n: x for n, x in w.items() if x > 1e-5}; total = sum(w.values())
    for n, x in w.items(): body.vertex_groups[n].add([v.index], x / total, 'REPLACE')


def stretch(curl):
    """Mean |edge length ratio - 1| over hand edges with all fingers curled (Blender pose bones)."""
    for b in rig.pose.bones: b.matrix_basis.identity()
    for side in ('Left', 'Right'):
        for f in FINGERS:
            pts = chains[(side, f)]; axis_world = (pts[1] - pts[0]).normalized().cross(Vector((0, -1, 0))).normalized()
            for s, deg in zip(SEGS, (35, 45, 25)):
                pb = rig.pose.bones[side + f + s]; local = pb.bone.matrix_local.to_3x3().inverted() @ axis_world
                pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion(local, math.radians(deg * curl))
    bpy.context.view_layer.update()
    posed = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices
    vals = []
    for e in mesh.edges:
        a, b = e.vertices
        if abs(mesh.vertices[a].co.x) < HAND_X + .02: continue
        r = (mesh.vertices[a].co - mesh.vertices[b].co).length
        if r > 1e-7: vals.append(abs((posed[a].co - posed[b].co).length / r - 1))
    for b in rig.pose.bones: b.matrix_basis.identity()
    bpy.context.view_layer.update()
    vals.sort()
    return {'mean': round(sum(vals) / len(vals), 4), 'p95': round(vals[int(len(vals) * .95)], 4), 'edges': len(vals)}


report = {'source': SRC.name, 'joint_blend': JOINT, 'before': {c: stretch(c) for c in (.75, 1.)}}
changed = 0
for v in mesh.vertices:
    if abs(v.co.x) >= HAND_X:
        set_weights(v, weights_for(v.co)); changed += 1
report['changed_vertices'] = changed
report['after'] = {c: stretch(c) for c in (.75, 1.)}
report['unweighted'] = sum(not v.groups for v in mesh.vertices)
report['weight_sum_errors'] = sum(abs(sum(g.weight for g in v.groups) - 1) > 1e-4 for v in mesh.vertices)
report['max_influences'] = max(len(v.groups) for v in mesh.vertices)
assert report['unweighted'] == report['weight_sum_errors'] == 0 and report['max_influences'] <= 4

OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); rig.select_set(True)
bpy.context.view_layer.objects.active = rig
# Same export options as 09 (repair_rig_mage.py) so Unity keeps the avatar and texture checks.
bpy.ops.export_scene.fbx(filepath=str(OUT / (NAME + '.fbx')), use_selection=True, object_types={'MESH', 'ARMATURE'},
                         path_mode='COPY', embed_textures=True, bake_anim=False, add_leaf_bones=False,
                         axis_forward='-Z', axis_up='Y')
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / (NAME + '.blend')), copy=True)
(OUT / (NAME + '_검사.json')).write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('FINGER_WEIGHTS', json.dumps(report), flush=True)
