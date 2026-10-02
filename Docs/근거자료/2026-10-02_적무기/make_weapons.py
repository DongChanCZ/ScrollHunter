"""Scroll Hunter enemy weapons (2026-10-02): raider dagger, leader greatsword, mage staff. Procedural, no external assets.

Run: blender -b --factory-startup --python make_weapons.py -- <evidence_dir> <unity_dir>
Units: models are 1.0 tall (about 1.65 m). Blender: long axis +Y (tip/top), blade width X, thickness Z.
Origin = grip point of the main hand. Imports into Unity with the long axis on +Z (same as the arrow).
"""
import bpy, bmesh, math, sys, json
from mathutils import Vector
from pathlib import Path

argv = sys.argv[sys.argv.index('--') + 1:]
EVIDENCE, UNITY = Path(argv[0]), Path(argv[1])
EVIDENCE.mkdir(parents=True, exist_ok=True); UNITY.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def sweep(bm, ys, profile_of, mat, caps=True, offset=lambda y: Vector()):
    rings = [[bm.verts.new(offset(y) + Vector((px, y, pz))) for px, pz in profile_of(y)] for y in ys]
    n = len(rings[0])
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(n):
            bm.faces.new((r0[i], r0[(i + 1) % n], r1[(i + 1) % n], r1[i])).material_index = mat
    if caps:
        bm.faces.new(list(reversed(rings[0]))).material_index = mat
        bm.faces.new(rings[-1]).material_index = mat


def ellipse(a, b, n=10):
    return [(a * math.cos(2 * math.pi * i / n), b * math.sin(2 * math.pi * i / n)) for i in range(n)]


def material(name, build, rough=0.75):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
    nt.links.new(build(nt), bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = rough
    return m


def tex(kind, dark, light, scale, distortion=0.0, direction='X'):
    def build(nt):
        coord = nt.nodes.new('ShaderNodeTexCoord')
        ramp = nt.nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color = (*dark, 1); ramp.color_ramp.elements[1].color = (*light, 1)
        if kind == 'grain':
            src = nt.nodes.new('ShaderNodeTexWave'); src.bands_direction = direction
            src.inputs['Scale'].default_value = scale; src.inputs['Distortion'].default_value = distortion
            src.inputs['Detail'].default_value = 3
        else:
            src = nt.nodes.new('ShaderNodeTexNoise'); src.inputs['Scale'].default_value = scale
            src.inputs['Detail'].default_value = 6
            ramp.color_ramp.elements[0].position = 0.35; ramp.color_ramp.elements[1].position = 0.7
        nt.links.new(coord.outputs['Object'], src.inputs['Vector'])
        nt.links.new(src.outputs['Fac'], ramp.inputs['Fac'])
        return ramp.outputs['Color']
    return build


STEEL = material('Steel', tex('noise', (0.10, 0.10, 0.105), (0.28, 0.28, 0.29), 500), 0.45)
DARKSTEEL = material('DarkSteel', tex('noise', (0.035, 0.035, 0.04), (0.10, 0.10, 0.11), 400), 0.5)
LEATHER = material('Leather', tex('noise', (0.025, 0.014, 0.008), (0.065, 0.038, 0.020), 900))
BRONZE = material('Bronze', tex('noise', (0.12, 0.07, 0.025), (0.30, 0.18, 0.07), 300), 0.5)
WOOD = material('Wood', tex('grain', (0.035, 0.020, 0.010), (0.10, 0.058, 0.030), 45, 9, 'X'))
CRYSTAL = material('Crystal', tex('noise', (0.03, 0.008, 0.05), (0.10, 0.03, 0.16), 40), 0.25)


def obj(bm, name, mats):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    return ob


def blade(bm, y0, length, width, thick, mat, tip=0.18):
    """Diamond-section blade from y0 along +Y; last `tip` fraction narrows to a point."""
    ys = [y0 + length * i / 16 for i in range(17)]
    def prof(y):
        t = (y - y0) / length
        w = width * (1 - 0.15 * t) * (1 if t < 1 - tip else max(0.02, (1 - t) / tip))
        th = thick * (1 - 0.3 * t)
        return [(w / 2, 0), (w * 0.38, th / 2), (-w * 0.38, th / 2), (-w / 2, 0), (-w * 0.38, -th / 2), (w * 0.38, -th / 2)]
    sweep(bm, ys, prof, mat)


def tube(bm, y0, y1, r, mat, n=10, steps=1, rz=None):
    ys = [y0 + (y1 - y0) * i / steps for i in range(steps + 1)]
    sweep(bm, ys, lambda y: ellipse(r(y) if callable(r) else r, (rz(y) if callable(rz) else rz) if rz else (r(y) if callable(r) else r), n), mat)


def box(bm, y0, y1, w, th, mat):
    sweep(bm, [y0, y1], lambda y: [(w / 2, th / 2), (-w / 2, th / 2), (-w / 2, -th / 2), (w / 2, -th / 2)], mat)


# ---------------- Dagger (about 36 cm). Origin at grip centre. ----------------
bm = bmesh.new()
blade(bm, 0.038, 0.14, 0.022, 0.005, 0)                                  # steel blade
box(bm, 0.030, 0.038, 0.055, 0.010, 1)                                   # dark crossguard
tube(bm, -0.032, 0.030, lambda y: 0.0085 * (1 + 0.06 * math.sin(y * 2 * math.pi / 0.008)), 2, 10, 16)  # leather wrap
tube(bm, -0.045, -0.032, lambda y: 0.0105, 1, 10)                       # pommel
dagger = obj(bm, 'WPN_Dagger_v01', [STEEL, DARKSTEEL, LEATHER])

# ---------------- Greatsword (about 1.4 m). Origin at the right-hand (upper) grip. ----------------
bm = bmesh.new()
blade(bm, 0.045, 0.62, 0.042, 0.009, 0, tip=0.08)
box(bm, 0.030, 0.045, 0.15, 0.016, 1)                                    # wide crossguard
tube(bm, 0.022, 0.030, lambda y: 0.016, 1, 10, rz=lambda y: 0.010)      # ricasso collar
tube(bm, -0.16, 0.022, lambda y: 0.0105 * (1 + 0.05 * math.sin(y * 2 * math.pi / 0.009)), 2, 10, 40)  # long two-hand grip
tube(bm, -0.19, -0.16, lambda y: 0.017 * math.sin(max(0.15, (y + 0.19) / 0.03) * math.pi * 0.5), 1, 10, 6)  # pommel
greatsword = obj(bm, 'WPN_Greatsword_v01', [STEEL, DARKSTEEL, LEATHER])

# ---------------- Staff (about 1.6 m). Origin at the hand grip, 0.55 above the foot. ----------------
bm = bmesh.new()
wobble = lambda y: Vector((0.004 * math.sin(y * 9.0), 0, 0.003 * math.sin(y * 6.0 + 1.0)))
ys = [-0.55 + 0.97 * i / 48 for i in range(49)]
sweep(bm, ys, lambda y: ellipse(0.0115 + 0.002 * math.sin(y * 23.0) + (0.004 if 0.30 < y < 0.36 else 0), 0.011 + 0.002 * math.cos(y * 19.0), 10), 0, offset=wobble)
tube(bm, -0.55, -0.52, lambda y: 0.0125, 2, 10)                           # iron foot cap (dark)
tube(bm, -0.03, 0.04, lambda y: 0.0125 * (1 + 0.05 * math.sin(y * 2 * math.pi / 0.008)), 3, 10, 14)  # leather wrap
top = wobble(0.42)
for k in range(4):                                                        # bronze prongs around the crystal
    a = k * math.pi / 2
    pts = [top + Vector((math.cos(a) * r, 0.42 + h, math.sin(a) * r)) for r, h in ((0.012, 0.0), (0.03, 0.04), (0.026, 0.09), (0.008, 0.12))]
    for p0, p1 in zip(pts, pts[1:]):
        d = (p1 - p0); L = d.length; mid = (p0 + p1) / 2
        ring = []
        sub = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.0035, radius2=0.003, depth=L)
        rot = d.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=rot, verts=sub['verts'])
        bmesh.ops.translate(bm, vec=mid, verts=sub['verts'])
        for f in {f for v in sub['verts'] for f in v.link_faces}: f.material_index = 1
crystal = bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.024)
bmesh.ops.scale(bm, vec=(0.8, 1.6, 0.8), verts=crystal['verts'])
bmesh.ops.translate(bm, vec=top + Vector((0, 0.48, 0)), verts=crystal['verts'])
for f in {f for v in crystal['verts'] for f in v.link_faces}: f.material_index = 4
staff = obj(bm, 'WPN_Staff_v01', [WOOD, BRONZE, DARKSTEEL, LEATHER, CRYSTAL])


def bake_and_export(ob, stem, size):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
    bpy.ops.object.mode_set(mode='OBJECT')
    img = bpy.data.images.new(stem + '_BaseColor', size, size, alpha=False)
    for m in ob.data.materials:
        node = m.node_tree.nodes.get('BakeTarget') or m.node_tree.nodes.new('ShaderNodeTexImage')
        node.name = 'BakeTarget'; node.image = img; m.node_tree.nodes.active = node
    scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 16
    scene.render.bake.margin = 8
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=True)
    img.filepath_raw = str(UNITY / (stem + '_BaseColor.png')); img.file_format = 'PNG'; img.save()
    bpy.ops.export_scene.fbx(filepath=str(UNITY / (stem + '.fbx')), use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                             embed_textures=False, path_mode='STRIP', bake_anim=False, add_leaf_bones=False)
    return {'file': stem + '.fbx', 'triangles': sum(len(p.vertices) - 2 for p in ob.data.polygons), 'texture': size,
            'length': round(max(v.co.y for v in ob.data.vertices) - min(v.co.y for v in ob.data.vertices), 3)}


report = [bake_and_export(dagger, 'WPN_Dagger_v01', 512), bake_and_export(greatsword, 'WPN_Greatsword_v01', 1024),
          bake_and_export(staff, 'WPN_Staff_v01', 1024)]
bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE / 'WPN_Enemy_v01.blend'))
(EVIDENCE / '무기_제작검사.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('DONE', json.dumps(report), flush=True)
