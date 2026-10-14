"""Author the CC-BY Winged Sword as a beveled mesh. Run with Blender --background."""
import bpy, json, os, tempfile
from pathlib import Path
from mathutils import Vector

work = os.path.join(tempfile.gettempdir(), 'ScrollHunter_20261014_winged_marker')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
source = Path(__file__).resolve().parents[3] / 'Assets/_Project/UI/WingedSwordMarker/winged-sword.svg.txt'
os.makedirs(work, exist_ok=True)
svg = Path(work) / 'winged-sword.svg'
svg.write_bytes(source.read_bytes())
bpy.ops.import_curve.svg(filepath=str(svg))
objects = [o for o in bpy.context.scene.objects if o.type == 'CURVE']
assert objects, 'SVG import produced no curves'
for obj in objects:
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.data.resolution_u = 5
    obj.data.dimensions = '2D'
    obj.data.fill_mode = 'BOTH'

for obj in objects:
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
if len(objects) > 1:
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
obj = bpy.context.object
obj.name = 'WingedSword'
points = [v.co.copy() for v in obj.data.vertices]
lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
scale = 1.8 / max(hi.x-lo.x, hi.y-lo.y)
for v in obj.data.vertices:
    v.co = (v.co-(lo+hi)*.5)*scale
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=.0001)
bpy.ops.object.mode_set(mode='OBJECT')
solid = obj.modifiers.new('Depth', 'SOLIDIFY')
solid.thickness = .10
solid.offset = 0
bpy.ops.object.modifier_apply(modifier=solid.name)
bevel = obj.modifiers.new('Metal edges', 'BEVEL')
bevel.width = .012
bevel.segments = 1
bevel.limit_method = 'ANGLE'
bevel.use_clamp_overlap = True
bpy.ops.object.modifier_apply(modifier=bevel.name)
obj.data.calc_loop_triangles()
vertices, normals, colors, triangles = [], [], [], []
for triangle in obj.data.loop_triangles:
    for vi in triangle.vertices:
        p = obj.data.vertices[vi].co
        # Pale blade, warm gold wings and hilt. Original silhouette and split blade remain.
        color = [.95, .92, .74, 1] if abs(p.x) < .18 and p.y < .30 else [.88, .62, .23, 1]
        triangles.append(len(vertices))
        vertices.append(dict(x=p.x, y=p.y, z=p.z))
        normals.append(dict(x=triangle.normal.x, y=triangle.normal.y, z=triangle.normal.z))
        colors.append(dict(r=color[0], g=color[1], b=color[2], a=1))
assert len(vertices) > 100 and max(p['z'] for p in vertices)-min(p['z'] for p in vertices) > .05
assert max(abs(p['x']) for p in vertices) < 1 and max(abs(p['y']) for p in vertices) < 1
with open(os.path.join(work, 'mesh.json'), 'w', encoding='utf-8') as f:
    json.dump(dict(vertices=vertices, normals=normals, colors=colors, triangles=triangles), f)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(work, 'WingedSword.blend'))
print('WINGED_SWORD', len(vertices), 'vertices', len(triangles)//3, 'triangles')
