"""Re-import delivered files to verify mesh and embedded textures."""
import bpy
import bmesh
import json
from pathlib import Path

folder=Path(__file__).parent
results=[]
for extension in ('glb','fbx'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path=folder/f'04_궁병_손수정.{extension}'
    if extension=='glb':bpy.ops.import_scene.gltf(filepath=str(path))
    else:bpy.ops.import_scene.fbx(filepath=str(path))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(objects)==1
    obj=objects[0]
    world_bounds=[[min((obj.matrix_world@v.co)[i] for v in obj.data.vertices),max((obj.matrix_world@v.co)[i] for v in obj.data.vertices)] for i in range(3)]
    assert abs(world_bounds[2][1]-.9666096)<1e-5
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
    boundary=sum(e.is_boundary for e in bm.edges)
    nonmanifold=sum(not e.is_manifold for e in bm.edges)
    assert boundary==0 and nonmanifold==0
    triangles=sum(len(f.verts)-2 for f in bm.faces)
    assert triangles==10832
    bm.free()
    images=[im for im in bpy.data.images if im.type=='IMAGE' and im.size[0]>0]
    assert len(images)==2, [(im.name,list(im.size)) for im in bpy.data.images]
    assert all(list(im.size)==[2048,2048] for im in images)
    results.append({'file':path.name,'bytes':path.stat().st_size,'meshes':len(objects),
        'triangles':triangles,'boundary_edges':boundary,'nonmanifold_edges':nonmanifold,
        'world_bounds':world_bounds,'textures':[{'name':im.name,'size':list(im.size),'packed':bool(im.packed_file)} for im in images]})
(folder/'04_궁병_내보내기검사.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXPORT_CHECKS',json.dumps(results,ensure_ascii=True))
