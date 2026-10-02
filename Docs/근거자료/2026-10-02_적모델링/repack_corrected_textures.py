"""Repair embedded textures in previously delivered models without rebuilding rigs."""
import bpy, json, hashlib
import numpy as np
from pathlib import Path

F=Path(__file__).parent
reports=[]
for stem in ('03_궁병_손보정_리깅','05_우두머리_손보정_리깅','07_깡패_보정_리깅'):
    bpy.ops.wm.open_mainfile(filepath=str(F/(stem+'.blend')))
    body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    def shape_signature():
        data={'vertices':[list(v.co) for v in body.data.vertices],
              'faces':[list(p.vertices) for p in body.data.polygons],
              'groups':[g.name for g in body.vertex_groups],
              'weights':[[(g.group,g.weight) for g in v.groups] for v in body.data.vertices],
              'bones':[(b.name,list(b.head_local),list(b.tail_local),b.parent.name if b.parent else None) for b in rig.data.bones]}
        return hashlib.sha256(json.dumps(data).encode()).hexdigest()
    before=shape_signature();report={'model':stem,'before_pixel_errors':{},'after_pixel_errors':{}}
    for node in body.data.materials[0].node_tree.nodes:
        if node.type!='TEX_IMAGE' or not node.image:continue
        old=node.image;label='Normal' if old.colorspace_settings.name=='Non-Color' else 'BaseColor'
        fresh=bpy.data.images.load(str(F/(stem+'_'+label+'.png')),check_existing=False)
        fresh.colorspace_settings.name=old.colorspace_settings.name
        report['before_pixel_errors'][label]=float(np.max(abs(np.array(old.pixels[:])-np.array(fresh.pixels[:]))))
        fresh.pack();node.image=fresh
    assert shape_signature()==before
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.gltf(filepath=str(F/(stem+'.glb')),export_format='GLB',use_selection=True,export_animations=False)
    bpy.ops.export_scene.fbx(filepath=str(F/(stem+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},path_mode='COPY',embed_textures=True,bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(F/(stem+'.blend')))
    bpy.ops.wm.open_mainfile(filepath=str(F/(stem+'.blend')))
    body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    assert shape_signature()==before
    report['geometry_and_rig_unchanged']=True
    for node in body.data.materials[0].node_tree.nodes:
        if node.type!='TEX_IMAGE' or not node.image:continue
        img=node.image;label='Normal' if img.colorspace_settings.name=='Non-Color' else 'BaseColor'
        expected=bpy.data.images.load(str(F/(stem+'_'+label+'.png')),check_existing=False)
        expected.colorspace_settings.name=img.colorspace_settings.name
        error=float(np.max(abs(np.array(img.pixels[:])-np.array(expected.pixels[:]))))
        report['after_pixel_errors'][label]=error
        assert error<.005,(stem,label,error)
    reports.append(report);print('REPACKED',json.dumps(report,ensure_ascii=True),flush=True)
(F/'텍스처_내보내기_정정검사.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf-8')
