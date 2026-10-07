"""Keep the approved source; correct only lower-face weights in a new FBX."""
import bpy, json, hashlib
from pathlib import Path

folder = Path(__file__).parent
source = folder.parent / '2026-10-02_적모델링/05_우두머리_손보정_리깅.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
body = bpy.data.objects['BanditLeader']
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
for bone in rig.pose.bones:
    bone.matrix_basis.identity()
before = [v.co.copy() for v in body.data.vertices]
def smooth(a, b, x):
    t = max(0., min(1., (x-a)/(b-a)))
    return t*t*(3-2*t)
changed = []
for v in body.data.vertices:
    x, y, z = v.co
    # Front beard/chin follows the head; fade through the neck below it.
    amount = max(smooth(.812, .834, z),
                 smooth(.794, .804, z) * smooth(-.015, -.035, y))
    amount *= 1 - smooth(.057, .075, abs(x))
    if amount <= .00001:
        continue
    old = {body.vertex_groups[g.group].name: g.weight for g in v.groups}
    if old.get('Head', 0) > .99999:
        continue
    weights = {name: w*(1-amount) for name, w in old.items()}
    weights['Head'] = weights.get('Head', 0) + amount
    weights = dict(sorted(weights.items(), key=lambda item: item[1], reverse=True)[:4])
    total = sum(weights.values())
    for g in list(v.groups):
        body.vertex_groups[g.group].remove([v.index])
    for name, w in weights.items():
        if w > .000001:
            body.vertex_groups[name].add([v.index], w/total, 'REPLACE')
    changed.append(v.index)
assert all((v.co-p).length == 0 for v, p in zip(body.data.vertices, before))
assert changed
assert all(abs(sum(g.weight for g in v.groups)-1) < .0001 for v in body.data.vertices)
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active=rig
out=folder/'Leader_face_weights.fbx'
bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, object_types={'MESH','ARMATURE'},
    path_mode='COPY', embed_textures=True, bake_anim=False, add_leaf_bones=False, axis_forward='-Z', axis_up='Y')
report={'changed_vertices':len(changed), 'total_vertices':len(before), 'geometry_unchanged':True,
        'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(), 'output_sha256':hashlib.sha256(out.read_bytes()).hexdigest()}
(folder/'face_weights_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(report)
