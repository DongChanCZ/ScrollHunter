"""Scroll Hunter archer bow and arrow (2026-10-02). Procedural, no external assets.

Run: blender -b --factory-startup --python make_bow_arrow.py -- <evidence_dir> <unity_dir>
Units: the imported archer is 1.0 tall (about 1.65 m), so 1 unit here = archer height.
Blender axes: bow long axis +Z, bow back (target side) +Y, string side -Y. Arrow points +Y, nock at origin.
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


def sweep(bm, centers, frames, profiles, caps=True, mat_of_ring=None):
    rings = [[bm.verts.new(c + u * px + v * py) for px, py in prof] for c, (u, v), prof in zip(centers, frames, profiles)]
    n = len(rings[0])
    for k, (r0, r1) in enumerate(zip(rings, rings[1:])):
        for i in range(n):
            f = bm.faces.new((r0[i], r0[(i + 1) % n], r1[(i + 1) % n], r1[i]))
            if mat_of_ring: f.material_index = mat_of_ring(k)
    if caps:
        for ring, k in ((rings[0], 0), (rings[-1], len(rings) - 2)):
            f = bm.faces.new(ring if ring is rings[-1] else list(reversed(ring)))
            if mat_of_ring: f.material_index = mat_of_ring(k)


def ellipse(a, b, n=10):
    return [(a * math.cos(2 * math.pi * i / n), b * math.sin(2 * math.pi * i / n)) for i in range(n)]


def to_object(bm, name, mats, location=(0, 0, 0), parent=None):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    ob.location = location
    if parent: ob.parent = parent
    return ob


def empty(name, location, parent):
    e = bpy.data.objects.new(name, None); scene.collection.objects.link(e)
    e.empty_display_size = 0.01; e.location = location; e.parent = parent
    return e


def material(name, build):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
    color = build(nt)
    nt.links.new(color, bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.8
    return m


def grain(dark, light, scale, distortion, direction='X'):
    def build(nt):
        coord = nt.nodes.new('ShaderNodeTexCoord')
        wave = nt.nodes.new('ShaderNodeTexWave'); wave.bands_direction = direction
        wave.inputs['Scale'].default_value = scale; wave.inputs['Distortion'].default_value = distortion
        wave.inputs['Detail'].default_value = 3
        noise = nt.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = scale * 0.6
        ramp = nt.nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].color = (*dark, 1); ramp.color_ramp.elements[1].color = (*light, 1)
        mix = nt.nodes.new('ShaderNodeMath'); mix.operation = 'MULTIPLY_ADD'
        mix.inputs[2].default_value = 0.15
        nt.links.new(coord.outputs['Object'], wave.inputs['Vector']); nt.links.new(coord.outputs['Object'], noise.inputs['Vector'])
        nt.links.new(wave.outputs['Fac'], mix.inputs[0]); nt.links.new(noise.outputs['Fac'], mix.inputs[1])
        nt.links.new(mix.outputs[0], ramp.inputs['Fac'])
        return ramp.outputs['Color']
    return build


def speckle(dark, light, scale):
    def build(nt):
        noise = nt.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value = scale
        noise.inputs['Detail'].default_value = 6
        ramp = nt.nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = 0.35; ramp.color_ramp.elements[1].position = 0.7
        ramp.color_ramp.elements[0].color = (*dark, 1); ramp.color_ramp.elements[1].color = (*light, 1)
        nt.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
        return ramp.outputs['Color']
    return build


# Linear colors chosen to sit beside the archer's brown leather and beige cloth.
WOOD = material('Wood', grain((0.045, 0.022, 0.010), (0.12, 0.062, 0.028), 70, 7))
LEATHER = material('Leather', speckle((0.025, 0.014, 0.008), (0.065, 0.038, 0.020), 900))
HORN = material('Horn', speckle((0.012, 0.010, 0.009), (0.05, 0.040, 0.030), 300))
SHAFT = material('Shaft', grain((0.22, 0.14, 0.07), (0.38, 0.26, 0.14), 140, 4))
IRON = material('Iron', speckle((0.05, 0.05, 0.052), (0.13, 0.13, 0.135), 600))
FEATHER = material('Feather', grain((0.42, 0.38, 0.30), (0.62, 0.58, 0.48), 260, 2, 'Z'))
COCK = material('CockFeather', grain((0.16, 0.035, 0.025), (0.30, 0.07, 0.04), 260, 2, 'Z'))
THREAD = material('Thread', speckle((0.02, 0.015, 0.012), (0.05, 0.035, 0.025), 2000))

# ---------------- Bow (about 1.42 m strung recurve) ----------------
bow = bpy.data.objects.new('WPN_Bow_v01', None); scene.collection.objects.link(bow)
bow.empty_display_size = 0.05
ROOT_Z, LIMB_LEN = 0.105, 0.325


def limb_center(s):
    """Braced limb bends toward the string side (-Y), the last 22% recurves back (+Y)."""
    y = -0.115 * s ** 1.4 + 0.045 * max(0.0, (s - 0.78) / 0.22) ** 2
    return Vector((0, y, s * LIMB_LEN))


limb_objects = {}
for side, sign in (('Upper', 1), ('Lower', -1)):
    bm = bmesh.new(); steps = 28
    ss = [i / steps for i in range(steps + 1)]
    centers = [limb_center(s) for s in ss]
    frames, profiles = [], []
    for i, s in enumerate(ss):
        t = (centers[min(i + 1, steps)] - centers[max(i - 1, 0)]).normalized()
        v = t.cross(X).normalized()
        frames.append((X, v))
        w = 0.024 * (1 - s) + 0.009 * s; th = 0.0085 * (1 - s) + 0.0045 * s
        profiles.append(ellipse(w / 2, th / 2, 10))
    if sign < 0:
        centers = [Vector((c.x, c.y, -c.z)) for c in centers]
        frames = [(u, Vector((v.x, v.y, -v.z))) for u, v in frames]
    sweep(bm, centers, frames, profiles, mat_of_ring=lambda k: 1 if k >= steps - 2 else 0)
    # 시위 끝점은 Unity 프리팹에서 둔다. 자식 empty는 bake_space_transform 변환이 틀어진다.
    limb_objects[side] = to_object(bm, 'Bow_Limb' + side, [WOOD, HORN], (0, 0, sign * ROOT_Z), bow)

# Riser: flares from the limb roots into a deeper grip; arrow shelf sits above the grip.
bm = bmesh.new()
zs = [ROOT_Z * (i / 20 * 2 - 1) for i in range(21)]
profiles = []
for z in zs:
    a = abs(z) / ROOT_Z
    half_w = 0.0095 + 0.0025 * a
    half_d = 0.0115 * (1 - a ** 2.2) + 0.00425 * a ** 2.2
    if 0.045 < z < 0.08: half_w -= 0.0035 * math.sin((z - 0.045) / 0.035 * math.pi)  # shelf window
    profiles.append(ellipse(half_w, half_d, 12))
sweep(bm, [Vector((0, 0, z)) for z in zs], [(X, Y)] * len(zs), profiles)
riser = to_object(bm, 'Bow_Riser', [WOOD], parent=bow)
# Leather wrap with shallow ridges.
bm = bmesh.new()
gz = [-0.045 + 0.09 * i / 40 for i in range(41)]
sweep(bm, [Vector((0, 0, z)) for z in gz], [(X, Y)] * len(gz),
      [ellipse(0.0105 * (1 + 0.05 * math.sin(z * 2 * math.pi / 0.008)), 0.0127 * (1 + 0.05 * math.sin(z * 2 * math.pi / 0.008)), 12) for z in gz])
to_object(bm, 'Bow_Grip', [LEATHER], parent=bow)
empty('Bow_ArrowRest', (0.006, 0.0, 0.05), bow)

# ---------------- Arrow (about 76 cm) ----------------
def arrow(name):
    bm = bmesh.new()
    along = lambda y: Vector((0, y, 0))
    def tube(y0, y1, r, mat, n=8, steps=1):
        ys = [y0 + (y1 - y0) * i / steps for i in range(steps + 1)]
        sweep(bm, [along(y) for y in ys], [(X, Z)] * len(ys), [ellipse(r, r, n)] * len(ys), mat_of_ring=lambda k: mat)
    tube(0.0, 0.008, 0.0029, 2)            # nock
    tube(0.008, 0.428, 0.0024, 0, steps=6)  # shaft
    tube(0.012, 0.017, 0.0027, 4)          # thread wraps
    tube(0.086, 0.091, 0.0027, 4)
    tube(0.426, 0.437, 0.0028, 1)          # iron socket
    hs = [0.437 + 0.034 * i / 8 for i in range(9)]   # broadhead
    widths = [0.0028 + (0.0068 - 0.0028) * min(1, i / 3) if i <= 3 else 0.0068 * (8 - i) / 5 + 0.0002 for i in range(9)]
    sweep(bm, [along(y) for y in hs], [(X, Z)] * len(hs),
          [[(w, 0), (0, 0.0009), (-w, 0), (0, -0.0009)] for w in widths], mat_of_ring=lambda k: 1)
    for k, angle in enumerate((90, 210, 330)):               # three shield-cut vanes
        u = Vector((math.cos(math.radians(angle)), 0, math.sin(math.radians(angle))))
        side = Y.cross(u).normalized()
        ys = [0.022 + 0.064 * i / 10 for i in range(11)]
        mat = 3 if k == 0 else 5
        for i in range(10):
            y0, y1 = ys[i], ys[i + 1]
            h = lambda y: 0.0105 * (1 - ((y - 0.022) / 0.064) ** 1.6) + 0.0012
            quad = [along(y0) + u * 0.0022, along(y1) + u * 0.0022, along(y1) + u * (0.0022 + h(y1)), along(y0) + u * (0.0022 + h(y0))]
            front = [bm.verts.new(p + side * 0.0003) for p in quad]; back = [bm.verts.new(p - side * 0.0003) for p in quad]
            for f in (front, list(reversed(back))):
                bm.faces.new(f).material_index = mat
            for a in range(4):
                bm.faces.new((front[a], front[(a + 1) % 4], back[(a + 1) % 4], back[a])).material_index = mat
    return to_object(bm, name, [SHAFT, IRON, HORN, COCK, THREAD, FEATHER])

arrow_ob = arrow('WPN_Arrow_v01')


def bake_and_export(objects, root, stem, size):
    bpy.ops.object.select_all(action='DESELECT')
    meshes = [o for o in objects if o.type == 'MESH']
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.01)
    bpy.ops.object.mode_set(mode='OBJECT')
    img = bpy.data.images.new(stem + '_BaseColor', size, size, alpha=False)
    for o in meshes:
        for m in o.data.materials:
            node = m.node_tree.nodes.get('BakeTarget') or m.node_tree.nodes.new('ShaderNodeTexImage')
            node.name = 'BakeTarget'; node.image = img; m.node_tree.nodes.active = node
    scene.render.engine = 'CYCLES'; scene.cycles.device = 'CPU'; scene.cycles.samples = 16
    scene.render.bake.margin = 8
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=True)
    img.filepath_raw = str(UNITY / (stem + '_BaseColor.png')); img.file_format = 'PNG'; img.save()
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    root.select_set(True); bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(UNITY / (stem + '.fbx')), use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                             embed_textures=False, path_mode='STRIP', bake_anim=False, add_leaf_bones=False, use_custom_props=False)
    tris = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
    return {'file': stem + '.fbx', 'meshes': [o.name for o in meshes], 'triangles': tris, 'texture': size}


report = []
report.append(bake_and_export([o for o in scene.objects if o is not bow and (o.parent is bow or (o.parent and o.parent.parent is bow))], bow, 'WPN_Bow_v01', 1024))
report.append(bake_and_export([arrow_ob], arrow_ob, 'WPN_Arrow_v01', 512))
bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE / 'WPN_Bow_Arrow_v01.blend'))
(EVIDENCE / '활화살_제작검사.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('DONE', json.dumps(report), flush=True)
