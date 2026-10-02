"""Local hand corrections and a deform rig; preserve the approved VARCO source."""
import bpy, bmesh, math, json, hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector, Quaternion
from mathutils.kdtree import KDTree

F=Path(__file__).parent
SRC=F/'08_마법사_시안.glb'
OUT='09_마법사_보정_리깅'
source_hash=hashlib.sha256(SRC.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SRC))
body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=body;body.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
body.name='Mage';body.data.name='MageMesh'
source_normals={tuple(body.data.vertices[loop.vertex_index].co):normal.vector.copy() for loop,normal in zip(body.data.loops,body.data.corner_normals)}
bm=bmesh.new();bm.from_mesh(body.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
weight_proxy=bpy.data.meshes.new('WeightCalculationProxy');bm.to_mesh(weight_proxy)
original_outside={tuple(v.co) for v in bm.verts if abs(v.co.x)<.400}

# Add geometry only to the hands, retaining the generated body and per-corner UVs.
edges=[e for e in bm.edges if all(abs(v.co.x)>.405 for v in e.verts)]
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=2,use_grid_fill=True)
bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3])
bm.verts.ensure_lookup_table();bm.normal_update()
finger_specs={}

def smooth(a,b,x):
    t=max(0.,min(1.,(x-a)/(b-a)));return t*t*(3-2*t)

landmarks={
    'Thumb':[(.414,-.011,.759),(.429,-.017,.777),(.442,-.018,.786),(.451,-.019,.791)],
    'Index':[(.438,-.001,.760),(.462,-.001,.762),(.482,-.001,.762),(.495,-.001,.762)],
    'Middle':[(.440,.004,.747),(.463,.004,.746),(.483,.004,.746),(.501,.003,.746)],
    'Ring':[(.437,.005,.734),(.461,.005,.731),(.482,.005,.729),(.494,.005,.728)],
    'Little':[(.431,.000,.720),(.451,-.001,.718),(.466,-.002,.714),(.479,-.002,.711)]}
for side,s in [('Left',1),('Right',-1)]:
    for name,pts in landmarks.items():finger_specs[(side,name)]=[Vector((s*x,y,z)) for x,y,z in pts]

def nearest_segment(p,pts):
    results=[];lengths=[(b-a).length for a,b in zip(pts,pts[1:])];total=sum(lengths)
    for i,(a,b) in enumerate(zip(pts,pts[1:])):
        axis=b-a;t=max(0.,min(1.,(p-a).dot(axis)/axis.length_squared));q=a+t*axis
        results.append(((p-q).length,(sum(lengths[:i])+t*lengths[i])/total,i,t,q))
    return min(results,key=lambda r:r[0])

def finger_name(p):
    side='Left' if p.x>0 else 'Right'
    return min(landmarks,key=lambda n:nearest_segment(p,finger_specs[(side,n)])[0])

# Local subdivision and relaxation round the existing blunt fingertip caps.
# Body, palm volume and original finger lengths are preserved outside distal zones.
for sign in (-1,1):
    side='Left' if sign>0 else 'Right'
    for name,base in [('Thumb',.423),('Index',.452),('Middle',.456),('Ring',.454),('Little',.444)]:
        verts=[v for v in bm.verts if sign*v.co.x>base and finger_name(v.co)==name]
        assert verts,(side,name)
        pts=finger_specs[(side,name)];axis=(pts[-1]-pts[-2]).normalized()
        end=max(v.co.dot(axis) for v in verts)
        for repeat in range(10):
            moves={}
            for v in verts:
                influence=smooth(end-.013,end-.004,v.co.dot(axis))*.38
                if influence:
                    avg=sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges)
                    moves[v]=v.co.lerp(avg,influence)
            for v,co in moves.items():v.co=co

assert {tuple(v.co) for v in bm.verts if abs(v.co.x)<.400}==original_outside
bmesh.ops.remove_doubles(bm,verts=[v for v in bm.verts if abs(v.co.x)>.405],dist=1e-6)
bmesh.ops.dissolve_degenerate(bm,edges=[e for e in bm.edges if all(abs(v.co.x)>.405 for v in e.verts)],dist=1e-6)
bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3])
print('REPAIRED_MESH',len(bm.verts),'boundary',sum(e.is_boundary for e in bm.edges),'nonmanifold',sum(not e.is_manifold for e in bm.edges),'zero_area',sum(f.calc_area()<1e-12 for f in bm.faces),flush=True)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(body.data);bm.free();body.data.update()
for p in body.data.polygons:p.use_smooth=True
bpy.ops.mesh.customdata_custom_splitnormals_clear()
body.data.update()
body.data.normals_split_custom_set([source_normals.get(tuple(body.data.vertices[l.vertex_index].co),n.vector) if abs(body.data.vertices[l.vertex_index].co.x)<.400 else n.vector for l,n in zip(body.data.loops,body.data.corner_normals)])

# Retouch the UV texels belonging to the underside of the hands. Dorsal skin,
# clothing and face remain from the chosen model. This is a surface-local mask.
mesh=body.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.active.data
mat=mesh.materials[0]
texnodes=[n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
base_node=next(n for n in texnodes if n.image.colorspace_settings.name!='Non-Color')
normal_nodes=[n for n in texnodes if n!=base_node]
base_img=base_node.image;W,H=base_img.size
def surface_mask(weight_at):
    result=np.zeros((H,W),dtype=np.float32)
    for tri in mesh.loop_triangles:
        ps=[mesh.vertices[i].co for i in tri.vertices]
        weights=[weight_at(p,mesh.vertices[i].normal) for p,i in zip(ps,tri.vertices)]
        if max(weights)<1e-5:continue
        uvs=np.array([[uv[l].uv.x*(W-1),uv[l].uv.y*(H-1)] for l in tri.loops])
        lo=np.maximum(np.floor(uvs.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(uvs.max(axis=0)).astype(int),[W-1,H-1])
        if np.any(hi<lo):continue
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1))
        a,b,c=uvs;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:continue
        wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;wc=1-wa-wb
        inside=(wa>=-.001)&(wb>=-.001)&(wc>=-.001)
        val=(wa*weights[0]+wb*weights[1]+wc*weights[2])*inside
        region=result[lo[1]:hi[1]+1,lo[0]:hi[0]+1];np.maximum(region,val,out=region)
    for _ in range(2):
        result=np.maximum.reduce([result,np.roll(result,1,0),np.roll(result,-1,0),np.roll(result,1,1),np.roll(result,-1,1)])
    return result

# Palms and all finger surfaces: remove leather-like bands without touching cuffs.
mask=surface_mask(lambda p,n:smooth(.404,.420,abs(p.x))*max(1-smooth(-.2,.5,n.y),smooth(.431,.451,abs(p.x))))
pixels=np.empty(W*H*4,dtype=np.float32);base_img.pixels.foreach_get(pixels);pixels=pixels.reshape(H,W,4)
samples=[]
for tri in mesh.loop_triangles:
    center=sum((mesh.vertices[i].co for i in tri.vertices),Vector())/3
    uvcenter=sum((uv[l].uv for l in tri.loops),Vector((0,0)))/3
    if .415<abs(center.x)<.438 and tri.normal.y<-.5 and .728<center.z<.760:
        samples.append(pixels[int(uvcenter.y*(H-1)),int(uvcenter.x*(W-1)),:3])
assert samples
skin=np.median(samples,axis=0)
y,x=np.indices((H,W));variation=1+.010*np.sin(x*.075)*np.sin(y*.071)
target=skin[None,None,:]*variation[:,:,None]
pixels[:,:,:3]=pixels[:,:,:3]*(1-mask[:,:,None])+target*mask[:,:,None]
base_img.pixels.foreach_set(pixels.ravel());base_img.update()
base_img.filepath_raw=str(F/(OUT+'_BaseColor.png'));base_img.file_format='PNG';base_img.save()
# Repack a fresh image: packing an already packed imported image retains old bytes.
base_node.image=bpy.data.images.load(base_img.filepath_raw,check_existing=False)
base_node.image.colorspace_settings.name=base_img.colorspace_settings.name
base_node.image.pack()
for node in normal_nodes:
    img=node.image
    if list(img.size)!=[W,H]:continue
    pix=np.empty(W*H*4,dtype=np.float32);img.pixels.foreach_get(pix);pix=pix.reshape(H,W,4)
    pix[:,:,:3]=pix[:,:,:3]*(1-mask[:,:,None])+np.array([.5,.5,1])[None,None,:]*mask[:,:,None]
    img.pixels.foreach_set(pix.ravel());img.update();img.filepath_raw=str(F/(OUT+'_Normal.png'));img.file_format='PNG';img.save()
    node.image=bpy.data.images.load(img.filepath_raw,check_existing=False)
    node.image.colorspace_settings.name=img.colorspace_settings.name
    node.image.pack()

# Set the asset origin at the soles without changing its relative proportions.
floor=min(v.co.z for v in mesh.vertices)
for v in mesh.vertices:v.co.z-=floor
for centers in finger_specs.values():
    for p in centers:p.z-=floor
mesh.update()
rest=[v.co.copy() for v in mesh.vertices]
arm=bpy.data.armatures.new('MageSkeleton');rig=bpy.data.objects.new('MageRig',arm)
bpy.context.collection.objects.link(rig);body.select_set(False);rig.select_set(True)
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None,deform=True):
    b=arm.edit_bones.new(name);b.head=head;b.tail=tail;b.use_deform=deform
    if parent:b.parent=arm.edit_bones[parent]
    return b
bone('Root',(0,0,0),(0,0,.08),deform=False)
bone('Hips',(0,.018,.510),(0,.018,.586),'Root')
bone('Spine',(0,.018,.586),(0,.016,.658),'Hips')
bone('Chest',(0,.016,.658),(0,.018,.720),'Spine')
bone('UpperChest',(0,.018,.720),(0,.018,.790),'Chest')
bone('Neck',(0,.018,.790),(0,.014,.837),'UpperChest')
bone('Head',(0,.014,.837),(0,.014,.946),'Neck')
for side,s in [('Left',1),('Right',-1)]:
    bone(side+'Shoulder',(s*.026,.018,.785),(s*.142,.018,.769),'UpperChest')
    bone(side+'UpperArm',(s*.142,.018,.769),(s*.270,.006,.750),side+'Shoulder')
    bone(side+'LowerArm',(s*.270,.006,.750),(s*.401,.003,.746),side+'UpperArm')
    bone(side+'Hand',(s*.401,.003,.746),(s*.441,.003,.745),side+'LowerArm')
    for name in ('Thumb','Index','Middle','Ring','Little'):
        pts=finger_specs[(side,name)];parent=side+'Hand'
        for i,suffix in enumerate(('Proximal','Intermediate','Distal')):
            n=side+name+suffix;bone(n,pts[i],pts[i+1],parent);parent=n
    bone(side+'UpperLeg',(s*.068,.019,.510),(s*.081,.013,.294),'Hips')
    bone(side+'LowerLeg',(s*.081,.013,.294),(s*.091,.018,.075),side+'UpperLeg')
    bone(side+'Foot',(s*.091,.018,.075),(s*.092,-.064,.028),side+'LowerLeg')
    bone(side+'Toes',(s*.092,-.064,.028),(s*.092,-.108,.022),side+'Foot')
bpy.ops.object.mode_set(mode='OBJECT')

# Heat weights are calculated at a larger temporary scale, then units are restored.
finger_bones=[b.name for b in arm.bones if any(n in b.name for n in landmarks)]
for name in finger_bones:arm.bones[name].use_deform=False
body.data=weight_proxy
for v in weight_proxy.vertices:v.co.z-=floor;v.co*=10
bpy.ops.object.mode_set(mode='EDIT')
for b in arm.edit_bones:b.head*=10;b.tail*=10
bpy.ops.object.mode_set(mode='OBJECT');body.select_set(True)
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
for v in weight_proxy.vertices:v.co/=10
bpy.ops.object.mode_set(mode='EDIT')
for b in arm.edit_bones:b.head/=10;b.tail/=10
bpy.ops.object.mode_set(mode='OBJECT');bpy.context.view_layer.update()
for name in finger_bones:arm.bones[name].use_deform=True
proxy_weights=[[(g.group,g.weight) for g in v.groups] for v in weight_proxy.vertices]
proxy_group_names=[g.name for g in body.vertex_groups]
assert all(w for w in proxy_weights),'Body weight calculation failed'
tree=KDTree(len(weight_proxy.vertices))
for v in weight_proxy.vertices:tree.insert(v.co,v.index)
tree.balance();body.data=mesh
for name in proxy_group_names:body.vertex_groups.new(name=name)
for v in mesh.vertices:
    _,index,_=tree.find(v.co)
    for group,weight in proxy_weights[index]:body.vertex_groups[group].add([v.index],weight,'REPLACE')
bpy.data.meshes.remove(weight_proxy)

def set_weights(v,weights):
    for g in list(v.groups):body.vertex_groups[g.group].remove([v.index])
    weights={n:w for n,w in weights.items() if w>.00001};total=sum(weights.values())
    for n,w in weights.items():
        group=body.vertex_groups.get(n) or body.vertex_groups.new(name=n)
        group.add([v.index],w/total,'REPLACE')

# Explicit finger weights avoid adjacent digits pulling each other with heat weights.
for v in mesh.vertices:
    p=v.co;u=abs(p.x)
    if u<.407:continue
    side='Left' if p.x>0 else 'Right';name=finger_name(p)
    pts=finger_specs[(side,name)]
    distance,t,segment,fraction,q=nearest_segment(p,pts)
    blend=smooth(0.,.25,t)
    weights={side+'Hand':1-blend}
    # Smooth transitions over joints, using three segment centers.
    anchors=(.18,.55,.85);vals=[math.exp(-((t-a)/.25)**2) for a in anchors];total=sum(vals)
    for suffix,w in zip(('Proximal','Intermediate','Distal'),vals):weights[side+name+suffix]=blend*w/total
    set_weights(v,weights)

# Keep shoulder/cowl weights from the proxy; attach the upper hood to the head.
for v in mesh.vertices:
    p=v.co
    if p.z>.837:set_weights(v,{'Head':1})
    if abs(p.x)>.16:
        expected='Left' if p.x>0 else 'Right'
        pairs={body.vertex_groups[g.group].name:g.weight for g in v.groups if not body.vertex_groups[g.group].name.startswith('Right' if p.x>0 else 'Left')}
        if pairs:set_weights(v,pairs)
    pairs=sorted([(body.vertex_groups[g.group].name,g.weight) for g in v.groups],key=lambda x:x[1],reverse=True)[:4]
    if not pairs:raise RuntimeError(f'Unweighted vertex {v.index}')
    set_weights(v,dict(pairs))
for mod in body.modifiers:
    if mod.type=='ARMATURE':mod.use_deform_preserve_volume=False
bpy.context.view_layer.update()

report={'source':SRC.name,'source_sha256':source_hash,'bones':len(arm.bones),'finger_bones':30,
        'vertices':len(mesh.vertices),'triangles':sum(len(p.vertices)-2 for p in mesh.polygons),
        'skin_color_linear':skin.tolist(),'retouched_texture_pixels':int((mask>.01).sum()),
        'origin':'soles','animation_actions':0,'protected_body_geometry_unchanged':True,'scope':'Hand repair and deform skeleton; no Unity import or authored combat animation.'}
bm=bmesh.new();bm.from_mesh(mesh)
report['topology']={'boundary':sum(e.is_boundary for e in bm.edges),'nonmanifold':sum(not e.is_manifold for e in bm.edges),'zero_area':sum(f.calc_area()<1e-12 for f in bm.faces)}
bm.free();assert report['topology']=={'boundary':0,'nonmanifold':0,'zero_area':0}
report['unweighted_vertices']=sum(not v.groups for v in mesh.vertices)
report['weight_sum_errors']=sum(abs(sum(g.weight for g in v.groups)-1)>.0001 for v in mesh.vertices)
assert report['unweighted_vertices']==report['weight_sum_errors']==0
assert all(len(v.groups)<=4 for v in mesh.vertices)

scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_cavity=True;scene.display.shading.background_type='WORLD'
scene.world=bpy.data.worlds.new('PreviewWorld');scene.world.color=(.045,.045,.045)
camdata=bpy.data.cameras.new('Preview');cam=bpy.data.objects.new('Preview',camdata);scene.collection.objects.link(cam);scene.camera=cam
camdata.type='ORTHO';camdata.clip_start=.001
def render(name,center,offset,scale):
    center=Vector(center);cam.location=center+Vector(offset);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();camdata.ortho_scale=scale
    scene.render.filepath=str(F/(OUT+'_'+name+'.png'));bpy.ops.render.render(write_still=True)
def reset():
    for b in rig.pose.bones:b.matrix_basis.identity()
    bpy.context.view_layer.update()
def turn(name,axis,deg):
    p=rig.pose.bones[name];local=p.bone.matrix_local.to_3x3().inverted()@Vector(axis)
    p.rotation_mode='QUATERNION';p.rotation_quaternion=Quaternion(local,math.radians(deg))
def positions():
    return [v.co.copy() for v in body.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices]
def curl(side,name,factor):
    pts=finger_specs[(side,name)];direction=(pts[1]-pts[0]).normalized();axis=direction.cross(Vector((0,-1,0))).normalized()
    for suffix,deg in zip(('Proximal','Intermediate','Distal'),(35,45,25)):
        turn(side+name+suffix,axis,deg*factor)
reset();report['finger_tests']=[]
for side in ('Left','Right'):
    for name in ('Thumb','Index','Middle','Ring','Little'):
        reset();curl(side,name,1);bpy.context.view_layer.update();posed=positions()
        moved=[i for i,(p,q) in enumerate(zip(posed,rest)) if (p-q).length>1e-5]
        other=max((p-q).length for p,q in zip(posed,rest) if (q.x<0 if side=='Left' else q.x>0))
        assert moved and other<1e-6,(side,name,len(moved),other,[(b.name,b.use_deform) for b in arm.bones if name in b.name])
        report['finger_tests'].append({'side':side,'finger':name,'moved':len(moved),'opposite_side_max':other})
reset();render('정면',(0,0,.48),(0,-2,.08),1.13);render('후면',(0,0,.48),(0,2,.08),1.13)
for side,off in [('앞',(0,-.7,.03)),('뒤',(0,.7,.03)),('옆',(.6,.15,.03))]:render('머리'+side,(0,.005,.875),off,.23)
for pose in ('펴기','쥐기'):
    reset()
    if pose=='쥐기':
        for side in ('Left','Right'):
            for name in ('Thumb','Index','Middle','Ring','Little'):curl(side,name,.75)
    bpy.context.view_layer.update()
    for side,s in [('Left',1),('Right',-1)]:
        center=(s*.450,.002,.753)
        for view,off in [('등',(0,.4,.03)),('바닥',(0,-.4,.03))]:render(side+'_'+pose+'_'+view,center,off,.15)
reset();turn('LeftUpperArm',(0,1,0),65);turn('RightUpperArm',(0,1,0),-65)
bpy.context.view_layer.update();render('팔내림',(0,0,.51),(0,-2,.08),1.13)
reset();turn('LeftUpperArm',(0,0,1),-60);turn('LeftLowerArm',(0,0,1),-15)
turn('RightUpperArm',(0,0,1),-10);turn('RightLowerArm',(0,0,1),135)
bpy.context.view_layer.update();render('팔굽힘',(0,0,.66),(1,-1.7,.5),.86)
reset();turn('LeftUpperLeg',(1,0,0),25);turn('LeftLowerLeg',(1,0,0),-25)
bpy.context.view_layer.update();render('다리들기',(0,0,.47),(.8,-2,.15),1.12)
reset()
assert hashlib.sha256(SRC.read_bytes()).hexdigest()==source_hash
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=str(F/(OUT+'.glb')),export_format='GLB',use_selection=True,export_animations=False)
bpy.ops.export_scene.fbx(filepath=str(F/(OUT+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},path_mode='COPY',embed_textures=True,bake_anim=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y')
rig.show_in_front=True;arm.display_type='OCTAHEDRAL'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.color_type='TEXTURE'
            area.spaces.active.region_3d.view_location=Vector((0,0,.5))
            area.spaces.active.region_3d.view_distance=1.8
            area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(F/(OUT+'.blend')))
(F/(OUT+'_검사.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('REPAIR_RIG_REPORT',json.dumps(report,ensure_ascii=True),flush=True)
