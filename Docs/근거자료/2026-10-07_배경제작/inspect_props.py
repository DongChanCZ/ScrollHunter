"""Blender background inspection of downloaded VARCO props; never touches Unity files."""
import bpy, bmesh, json, math, struct
from pathlib import Path
from mathutils import Vector

root = Path(__file__).parent
reports = []
for path in sorted(root.glob('P*.glb')):
    raw = path.read_bytes()
    json_size = struct.unpack_from('<I', raw, 12)[0]
    gltf = json.loads(raw[20:20+json_size])
    binary_start = 28 + json_size
    material = gltf['materials'][0]
    for label, texture in [('BaseColor', material['pbrMetallicRoughness']['baseColorTexture']),
                           ('Normal', material['normalTexture'])]:
        image = gltf['images'][gltf['textures'][texture['index']]['source']]
        assert image['mimeType'] == 'image/png'
        view = gltf['bufferViews'][image['bufferView']]
        start = binary_start + view.get('byteOffset', 0)
        (root/(path.stem+'_'+label+'.png')).write_bytes(raw[start:start+view['byteLength']])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    points = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    assert points and all(math.isfinite(x) for p in points for x in p), path.name
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    center, size = (low + high) / 2, max(high - low)
    report = dict(file=path.name, bytes=path.stat().st_size, meshes=len(meshes),
                  vertices=sum(len(o.data.vertices) for o in meshes),
                  triangles=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),
                  bounds_min=list(low), bounds_max=list(high),
                  all_meshes_have_uv=all(bool(o.data.uv_layers) for o in meshes),
                  textures=[dict(name=i.name, size=list(i.size), packed=bool(i.packed_file))
                            for i in bpy.data.images if i.type == 'IMAGE' and i.size[0]],
                  boundary_edges=0, nonmanifold_edges=0, zero_area_faces=0)
    for o in meshes:
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=size*1e-7)
        report['boundary_edges'] += sum(e.is_boundary for e in bm.edges)
        report['nonmanifold_edges'] += sum(not e.is_manifold for e in bm.edges)
        report['zero_area_faces'] += sum(f.calc_area() < size*size*1e-14 for f in bm.faces)
        bm.free()
    assert report['all_meshes_have_uv'] and report['textures'], report
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.show_shadows = False
    scene.display.shading.show_cavity = False
    scene.display.shading.background_type = 'WORLD'
    scene.world = bpy.data.worlds.new('InspectionWorld')
    scene.world.color = (.12, .12, .12)
    data = bpy.data.cameras.new('InspectionCamera')
    data.type = 'ORTHO'; data.ortho_scale = size * 1.35
    data.clip_start = size*.001; data.clip_end = size*100
    camera = bpy.data.objects.new('InspectionCamera', data)
    scene.collection.objects.link(camera); scene.camera = camera
    for label, direction in [('front', (.35, -2, .3)), ('rear', (-.5, 2, .4))]:
        camera.location = center + Vector(direction) * size
        camera.rotation_euler = (center-camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(root / (path.stem + '_' + label + '.png'))
        bpy.ops.render.render(write_still=True)
    reports.append(report)
    (root/'mesh_inspection.json').write_text(json.dumps(reports, ensure_ascii=False, indent=2), encoding='utf-8')
    print('INSPECTED', json.dumps(report, ensure_ascii=True), flush=True)
assert len(reports) == 7, ('expected seven props', len(reports))

fbx_reports = []
for path in sorted(root.glob('P*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=False)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    expected = next(r for r in reports if Path(r['file']).stem == path.stem)
    actual = sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
    images = [i for i in bpy.data.images if i.type == 'IMAGE' and i.size[0]]
    report = dict(file=path.name, triangles=actual, same_triangle_count=actual==expected['triangles'],
                  all_meshes_have_uv=all(bool(o.data.uv_layers) for o in meshes),
                  textures=[dict(name=i.name, size=list(i.size), packed=bool(i.packed_file)) for i in images])
    assert report['same_triangle_count'] and report['all_meshes_have_uv'] and len(images)>=2, report
    fbx_reports.append(report)
(root/'fbx_inspection.json').write_text(json.dumps(fbx_reports, ensure_ascii=False, indent=2), encoding='utf-8')
assert len(fbx_reports) == 7
print('FBX_CHECKED', len(fbx_reports), flush=True)
