"""Local Blender mesh repair; never overwrites the generated source."""
import bpy
import bmesh
import json
import math
import hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

folder = Path(__file__).parent
source_hash = hashlib.sha256((folder / '03_궁병_시안.glb').read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(folder / '03_궁병_시안.glb'))
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
original_normals = {}
for loop, normal in zip(body.data.loops, body.data.corner_normals):
    original_normals[tuple(body.data.vertices[loop.vertex_index].co)] = normal.vector.copy()
hand_vertices = [[v.index, *v.co] for v in body.data.vertices if abs(v.co.x) > .38]
(folder / '04_hand_geometry.json').write_text(json.dumps({
    'vertices': hand_vertices,
    'faces': [list(p.vertices) for p in body.data.polygons if all(abs(body.data.vertices[i].co.x) > .38 for i in p.vertices)]
}), encoding='utf-8')
print('HAND_BOUNDS', [[min(v[i] for v in hand_vertices), max(v[i] for v in hand_vertices)] for i in range(1,4)])

# GLB splits vertices at UV seams. Welding retains the per-corner UV data.
bm = bmesh.new()
bm.from_mesh(body.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
bm.to_mesh(body.data)
bm.free()
source_mesh = body.data.copy()
source_mesh.calc_loop_triangles()
source_points = [v.co.copy() for v in source_mesh.vertices]
source_triangles = [tuple(t.vertices) for t in source_mesh.loop_triangles]
source_uvs = [[source_mesh.uv_layers.active.data[l].uv.copy() for l in t.loops] for t in source_mesh.loop_triangles]
source_bvh = BVHTree.FromPolygons(source_points, source_triangles, all_triangles=True)

def statistics(mesh):
    bm = bmesh.new(); bm.from_mesh(mesh)
    result = dict(vertices=len(bm.verts), triangles=sum(len(f.verts)-2 for f in bm.faces),
                  boundary_edges=sum(e.is_boundary for e in bm.edges),
                  nonmanifold_edges=sum(not e.is_manifold for e in bm.edges),
                  loose_vertices=sum(not v.link_faces for v in bm.verts))
    bm.free()
    return result

report = {'source': '03_궁병_시안.glb', 'before_welded': statistics(body.data)}
print('BEFORE', report['before_welded'])

# A narrow round-ended channel opens the fused middle/ring web on each hand.
# Local coordinates are observed from this specific archer, not a general repair algorithm.
new_material = bpy.data.materials.new('RepairSidewallMarker')
body.data.materials.append(new_material)
for sign in (1, -1):
    start = Vector((.453, .0340))
    end = Vector((.515, .0354))
    axis = (end-start).normalized()
    across = Vector((-axis.y, axis.x))
    radius = .00165
    outline = [start + across*radius, end + across*.0034, end - across*.0034, start - across*radius]
    # Semicircle closes the proximal end, avoiding a sharp slit in the palm.
    for i in range(1, 9):
        angle = -math.pi/2 - math.pi*i/9
        outline.append(start + axis*(math.cos(angle)*radius) + across*(math.sin(angle)*radius))
    n = len(outline)
    verts = [(sign*p.x, p.y, z) for z in (.69, .79) for p in outline]
    faces = [tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh = bpy.data.meshes.new('FingerGapCutter')
    mesh.from_pydata(verts, [], faces)
    cutter = bpy.data.objects.new('FingerGapCutter', mesh)
    bpy.context.collection.objects.link(cutter)
    cutter.data.materials.append(body.data.materials[0]); cutter.data.materials.append(new_material)
    for p in cutter.data.polygons: p.material_index = 1
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    mod = body.modifiers.new('SeparateMiddleRing', 'BOOLEAN')
    mod.operation = 'DIFFERENCE'; mod.solver = 'EXACT'; mod.object = cutter
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)

bm = bmesh.new(); bm.from_mesh(body.data)
rim = [e for e in bm.edges if e.is_manifold and {f.material_index for f in e.link_faces} == {0,1}]
print('RIM_EDGES', len(rim))
bmesh.ops.bevel(bm, geom=rim, offset=.0013, segments=3, affect='EDGES', clamp_overlap=True)
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts)>3])
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(body.data); bm.free()
body.data.update()

# Preserve all original UVs. Only the new inside walls receive nearest-surface UVs.
uv_layer = body.data.uv_layers.active
wall_count = 0
for face in body.data.polygons:
    face_center = sum((body.data.vertices[i].co for i in face.vertices), Vector()) / len(face.vertices)
    distance_to_original = source_bvh.find_nearest(face_center)[3]
    new_surface = abs(face_center.x) > .449 and .021 < face_center.y < .05 and distance_to_original > 5e-7
    if face.material_index != 1 and not new_surface: continue
    wall_count += 1
    # The generated fusion seam is painted dark. Sample intact finger skin
    # beside that seam rather than stretching its dark crease onto the new wall.
    center_y = .0340 + (abs(face_center.x)-.453)*(.0014/.062)
    toward_finger = 1 if face_center.y > center_y else -1
    for loop_index in face.loop_indices:
        co = body.data.vertices[body.data.loops[loop_index].vertex_index].co
        probe = co + Vector((0,toward_finger*.0045,0))
        # Keep the projection on the same dorsal UV island across each wall.
        probe.y += (co.z-.737)*.12
        probe.z = .69 if face_center.z < .737 else .78
        probe.x = math.copysign(min(abs(probe.x), .488), probe.x)
        nearest, normal, tri_index, distance = source_bvh.find_nearest(probe)
        triangle = source_triangles[tri_index]
        abc = [source_points[i] for i in triangle]
        uv = [Vector((v.x,v.y,0)) for v in source_uvs[tri_index]]
        mapped = barycentric_transform(nearest, *abc, *uv)
        uv_layer.data[loop_index].uv = mapped.xy
    face.material_index = 0
    face.use_smooth = True
body.data.materials.pop(index=1)

# Boolean interpolation leaves inappropriate custom normals on the new walls.
# Recompute only the repaired middle/ring area; retain original normals elsewhere.
def repaired_area(co):
    return abs(co.x) > .448 and .019 < co.y < .050
for p in body.data.polygons:
    if any(repaired_area(body.data.vertices[i].co) for i in p.vertices):
        p.use_smooth = True
bpy.context.view_layer.objects.active = body
bpy.ops.mesh.customdata_custom_splitnormals_clear()
body.data.update()
normals = []
for loop, normal in zip(body.data.loops, body.data.corner_normals):
    co = body.data.vertices[loop.vertex_index].co
    normals.append(normal.vector.copy() if repaired_area(co) else original_normals.get(tuple(co),normal.vector).copy())
body.data.normals_split_custom_set(normals)
report['new_wall_triangles'] = wall_count
report['after'] = statistics(body.data)
assert report['after']['boundary_edges'] == 0
assert report['after']['nonmanifold_edges'] == 0
assert report['after']['loose_vertices'] == 0

protected_before = {tuple(v) for v in source_points if not (abs(v.x) > .449 and .020 < v.y < .05)}
protected_after = {tuple(v.co) for v in body.data.vertices if not (abs(v.co.x) > .449 and .020 < v.co.y < .05)}
report['protected_vertices_unchanged'] = protected_before == protected_after
assert report['protected_vertices_unchanged'], 'Unexpected change outside the repair region'

bm = bmesh.new(); bm.from_mesh(body.data)
result_bvh = BVHTree.FromBMesh(bm)
gap_tests = []
for sign in (1,-1):
    for x in (.458,.465,.475,.485,.491):
        y = .0340 + (x-.453)*(.0014/.062)
        hit = result_bvh.ray_cast(Vector((sign*x,y,.8)), Vector((0,0,-1)), .12)[0]
        gap_tests.append({'side':'L' if sign==1 else 'R','x':x,'gap_open':hit is None})
bm.free()
assert all(t['gap_open'] for t in gap_tests)
report['gap_tests'] = gap_tests
report['source_sha256'] = source_hash
assert hashlib.sha256((folder / '03_궁병_시안.glb').read_bytes()).hexdigest() == source_hash
print('AFTER', report['after'])
(folder / '04_궁병_손보정검사.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 900; scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'SINGLE'
scene.display.shading.single_color = (.65,.68,.73)
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = 'BOTH'
scene.display.shading.background_type = 'WORLD'
scene.world = bpy.data.worlds.new('InspectionWorld'); scene.world.color = (.04,.04,.04)
camera_data = bpy.data.cameras.new('InspectionCamera')
camera = bpy.data.objects.new('InspectionCamera', camera_data)
scene.collection.objects.link(camera); scene.camera = camera
camera_data.type='ORTHO'; camera_data.ortho_scale=.145; camera_data.clip_start=.001
for shading in ('SINGLE','TEXTURE'):
    scene.display.shading.color_type = shading
    for side,sign in (('L',1),('R',-1)):
        center=Vector((sign*.448,.018,.737))
        for view,offset in [('top',(sign*.1,-.1,.4)),('palm',(sign*.1,-.1,-.4))]:
            camera.location=center+Vector(offset)
            camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(folder / f'04_궁병_손_{side}_{view}{"_색상" if shading=="TEXTURE" else ""}.png')
            bpy.ops.render.render(write_still=True)

# Original and repaired meshes are rendered with identical camera/light settings.
repaired_mesh = body.data
body.data = source_mesh
scene.display.shading.color_type = 'SINGLE'
for side,sign in (('L',1),('R',-1)):
    center=Vector((sign*.448,.018,.737))
    camera.location=center+Vector((sign*.1,-.1,.4))
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(folder / f'04_궁병_손_{side}_수정전.png')
    bpy.ops.render.render(write_still=True)
body.data = repaired_mesh

# Diagnostic flexion checks geometry separation only. This is not a production rig.
rest = [v.co.copy() for v in body.data.vertices]
pose_tests=[]
for side,sign in (('L',1),('R',-1)):
    for finger,lower,upper in [('middle',.018,.034),('ring',.034,.050)]:
        moved=[]
        for i,p in enumerate(rest):
            x=sign*p.x
            boundary=.0340 + (x-.453)*(.0014/.062)
            in_finger = lower < p.y < upper and (p.y < boundary if finger=='middle' else p.y > boundary)
            if x <= .454 or not in_finger: continue
            t=x-.454; k=24; angle=k*t; dz=p.z-.737
            body.data.vertices[i].co=Vector((sign*(.454+math.sin(angle)/k+dz*math.sin(angle)),p.y,.737-(1-math.cos(angle))/k+dz*math.cos(angle)))
            moved.append(i)
        body.data.update()
        assert moved
        assert all(sign*rest[i].x > .454 for i in moved)
        opposite_max=max((v.co-p).length for v,p in zip(body.data.vertices,rest) if sign*p.x<0)
        assert opposite_max == 0
        pose_tests.append({'side':side,'finger':finger,'moved_vertices':len(moved),'opposite_side_displacement':opposite_max})
        center=Vector((sign*.45,.018,.73))
        camera.location=center+Vector((sign*.1,-.22,.20))
        camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(folder/f'04_궁병_굽힘_{side}_{finger}.png')
        bpy.ops.render.render(write_still=True)
        for v,p in zip(body.data.vertices,rest):v.co=p
        body.data.update()
report['diagnostic_flexion'] = pose_tests
report['diagnostic_note'] = 'Procedural mesh flexion only; VARCO rigging, skin weights and bow grip are not validated.'

# Save/export the rest pose, body only. Cameras and diagnostic poses are excluded.
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); bpy.context.view_layer.objects.active=body
body.name='Archer_HandsRepaired'
bpy.ops.export_scene.gltf(filepath=str(folder/'04_궁병_손수정.glb'),export_format='GLB',use_selection=True,export_animations=False)
bpy.ops.export_scene.fbx(filepath=str(folder/'04_궁병_손수정.fbx'),use_selection=True,object_types={'MESH'},path_mode='COPY',embed_textures=True,bake_anim=False,add_leaf_bones=False)
report['exported']=['04_궁병_손수정.glb','04_궁병_손수정.fbx','04_궁병_손수정.blend']
(folder / '04_궁병_손보정검사.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
scene.display.shading.color_type='TEXTURE'
camera_data.ortho_scale=1.14
center=Vector((0,0,.49));camera.location=Vector((0,-2,.53))
camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1000;scene.render.resolution_y=1000
scene.render.filepath=str(folder/'04_궁병_전신.png')
bpy.ops.render.render(write_still=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(folder / '04_궁병_손수정.blend'))
