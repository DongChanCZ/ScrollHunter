"""Local hand corrections and a deform rig; preserve the approved VARCO source."""
import bpy, bmesh, math, json, hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector, Quaternion
from mathutils.kdtree import KDTree

F=Path(__file__).parent
SRC=F/'02_궁병여성_VARCO_손수정.glb'
OUT='03_궁병_손보정_리깅'
source_hash=hashlib.sha256(SRC.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SRC))
body=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=body;body.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
body.name='Archer';body.data.name='ArcherMesh'
source_normals={tuple(body.data.vertices[loop.vertex_index].co):normal.vector.copy() for loop,normal in zip(body.data.loops,body.data.corner_normals)}
bm=bmesh.new();bm.from_mesh(body.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
weight_proxy=bpy.data.meshes.new('WeightCalculationProxy');bm.to_mesh(weight_proxy)
original_outside={tuple(v.co) for v in bm.verts if abs(v.co.x)<.408}

# Add geometry only to the hands, retaining the generated body and per-corner UVs.
edges=[e for e in bm.edges if all(abs(v.co.x)>.414 for v in e.verts)]
bmesh.ops.subdivide_edges(bm,edges=edges,cuts=2,use_grid_fill=True)
bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3])
bm.verts.ensure_lookup_table();bm.normal_update()
finger_specs={}

def smooth(a,b,x):
    t=max(0.,min(1.,(x-a)/(b-a)));return t*t*(3-2*t)

landmarks={
    'Thumb':[(.421,-.020,.267),(.435,-.030,.258),(.447,-.039,.253),(.455,-.044,.250)],
    'Index':[(.448,-.013,.281),(.463,-.024,.280),(.478,-.030,.279),(.490,-.032,.274)],
    'Middle':[(.449,-.002,.282),(.469,-.001,.279),(.485,-.004,.270),(.499,-.005,.260)],
    'Ring':[(.445,.014,.279),(.463,.021,.273),(.481,.020,.262),(.496,.020,.253)],
    'Little':[(.433,.027,.270),(.447,.036,.264),(.462,.040,.254),(.474,.040,.246)]}
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

# Mild local relaxation rounds coarse end caps; no remeshing of body/clothes.
for sign in (-1,1):
    side='Left' if sign>0 else 'Right'
    for name,base in [('Thumb',.425),('Index',.447),('Middle',.453),('Ring',.449),('Little',.441)]:
        verts=[v for v in bm.verts if sign*v.co.x>base and finger_name(v.co)==name]
        assert verts,(side,name)
        maxx=max(sign*v.co.x for v in verts)
        tip_zone=maxx-(.010 if name=='Thumb' else .009)
        for repeat in range(5):
            moves={}
            for v in verts:
                influence=smooth(tip_zone-.008,tip_zone+.003,sign*v.co.x)*.35
                if influence:
                    avg=sum((e.other_vert(v).co for e in v.link_edges),Vector())/len(v.link_edges)
                    moves[v]=v.co.lerp(avg,influence)
            for v,co in moves.items():v.co=co
        # The generated needle tip is shortened and its terminal cross-section widened.
        distal=[v for v in verts if sign*v.co.x>tip_zone]
        if distal:
            mid=sum((v.co for v in distal),Vector())/len(distal)
            for v in distal:
                t=smooth(tip_zone,maxx,sign*v.co.x)
                v.co.x-=sign*.0015*t
                v.co.y=mid.y+(v.co.y-mid.y)*(1+.22*t)
                v.co.z=mid.z+(v.co.z-mid.z)*(1+.22*t)
        # Trim only the needle end and close it with a beveled, watertight cap.
        pts=finger_specs[(side,name)];axis=(pts[-1]-pts[-2]).normalized()
        end_projection=max(v.co.dot(axis) for v in verts)
        plane=pts[-1]+axis*(end_projection-.0025-pts[-1].dot(axis))
        uv_layer=bm.loops.layers.uv.active
        nearest_vertex=min(verts,key=lambda v:(v.co-(plane-axis*.002)).length)
        sample_uv=next(iter(nearest_vertex.link_loops))[uv_layer].uv.copy()
        geom=set(verts)
        for v in verts:geom.update(v.link_edges);geom.update(v.link_faces)
        result=bmesh.ops.bisect_plane(bm,geom=list(geom),dist=1e-7,plane_co=plane,plane_no=axis,clear_outer=True,clear_inner=False)
        rim=[g for g in result['geom_cut'] if isinstance(g,bmesh.types.BMEdge) and g.is_valid and g.is_boundary]
        if rim:
            filled=bmesh.ops.holes_fill(bm,edges=rim,sides=0)['faces']
            for f in filled:
                for loop in f.loops:loop[uv_layer].uv=sample_uv
            bevel=bmesh.ops.bevel(bm,geom=rim,offset=.0008,segments=3,affect='EDGES',clamp_overlap=True)
            for f in bevel['faces']:
                for loop in f.loops:loop[uv_layer].uv=sample_uv

assert {tuple(v.co) for v in bm.verts if abs(v.co.x)<.408}==original_outside
bmesh.ops.remove_doubles(bm,verts=[v for v in bm.verts if abs(v.co.x)>.414],dist=1e-6)
bmesh.ops.dissolve_degenerate(bm,edges=[e for e in bm.edges if all(abs(v.co.x)>.414 for v in e.verts)],dist=1e-6)
bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if len(f.verts)>3])
print('REPAIRED_MESH',len(bm.verts),'boundary',sum(e.is_boundary for e in bm.edges),'nonmanifold',sum(not e.is_manifold for e in bm.edges),'zero_area',sum(f.calc_area()<1e-12 for f in bm.faces),flush=True)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(body.data);bm.free();body.data.update()
for p in body.data.polygons:p.use_smooth=True
bpy.ops.mesh.customdata_custom_splitnormals_clear()
body.data.update()
body.data.normals_split_custom_set([source_normals.get(tuple(body.data.vertices[l.vertex_index].co),n.vector) if abs(body.data.vertices[l.vertex_index].co.x)<.408 else n.vector for l,n in zip(body.data.loops,body.data.corner_normals)])

# Retouch the UV texels belonging to the underside of the hands. Dorsal skin,
# clothing and face remain from the chosen model. This is a surface-local mask.
mesh=body.data;mesh.calc_loop_triangles();uv=mesh.uv_layers.active.data
mat=mesh.materials[0]
texnodes=[n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
base_node=next(n for n in texnodes if n.image.colorspace_settings.name!='Non-Color')
normal_nodes=[n for n in texnodes if n!=base_node]
base_img=base_node.image;W,H=base_img.size
mask=np.zeros((H,W),dtype=np.float32)
world_z=np.zeros((H,W),dtype=np.float32)
samples=[]
for tri in mesh.loop_triangles:
    ps=[mesh.vertices[i].co for i in tri.vertices]
    if not all(abs(p.x)>.401 for p in ps):continue
    # Vertex normal handles the curved palm/finger sides; the dorsal center stays intact.
    weights=[]
    for i,p in zip(tri.vertices,ps):
        n=mesh.vertices[i].normal
        weights.append(smooth(.405,.422,abs(p.x))*(1-smooth(-.35,.50,n.z)))
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
    region=mask[lo[1]:hi[1]+1,lo[0]:hi[0]+1];np.maximum(region,val,out=region)

# Two-pixel padding prevents old seam colors leaking across UV borders.
for _ in range(2):
    mask=np.maximum.reduce([mask,np.roll(mask,1,0),np.roll(mask,-1,0),np.roll(mask,1,1),np.roll(mask,-1,1)])
pixels=np.empty(W*H*4,dtype=np.float32);base_img.pixels.foreach_get(pixels);pixels=pixels.reshape(H,W,4)
for tri in mesh.loop_triangles:
    ps=[mesh.vertices[i].co for i in tri.vertices]
    center=sum(ps,Vector())/3
    if .416<abs(center.x)<.443 and tri.normal.z>.65:
        uvcenter=sum((uv[l].uv for l in tri.loops),Vector((0,0)))/3
        samples.append(pixels[int(uvcenter.y*(H-1)),int(uvcenter.x*(W-1)),:3])
assert samples
skin=np.median(samples,axis=0)
# Subtle deterministic variation avoids painting the palm a featureless solid patch.
y,x=np.indices((H,W));variation=1+.018*np.sin(x*.075)*np.sin(y*.071)
target=skin[None,None,:]*variation[:,:,None]*1.02
pixels[:,:,:3]=pixels[:,:,:3]*(1-mask[:,:,None])+target*mask[:,:,None]
base_img.pixels.foreach_set(pixels.ravel());base_img.update()
base_img.filepath_raw=str(F/(OUT+'_BaseColor.png'));base_img.file_format='PNG';base_img.save()
# Reload the saved texture before packing; imported packed bytes otherwise stay stale.
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
arm=bpy.data.armatures.new('ArcherSkeleton');rig=bpy.data.objects.new('ArcherRig',arm)
bpy.context.collection.objects.link(rig);body.select_set(False);rig.select_set(True)
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None,deform=True):
    b=arm.edit_bones.new(name);b.head=head;b.tail=tail;b.use_deform=deform
    if parent:b.parent=arm.edit_bones[parent]
    return b
bone('Root',(0,0,0),(0,0,.08),deform=False)
bone('Hips',(0,.010,.520),(0,.010,.579),'Root')
bone('Spine',(0,.010,.579),(0,.006,.664),'Hips')
bone('Chest',(0,.006,.664),(0,.010,.727),'Spine')
bone('UpperChest',(0,.010,.727),(0,.014,.792),'Chest')
bone('Neck',(0,.014,.792),(0,.012,.855),'UpperChest')
bone('Head',(0,.012,.855),(0,.012,.969),'Neck')
for side,s in [('Left',1),('Right',-1)]:
    bone(side+'Shoulder',(s*.021,.014,.791),(s*.136,.014,.785),'UpperChest')
    bone(side+'UpperArm',(s*.136,.014,.785),(s*.267,.017,.779),side+'Shoulder')
    bone(side+'LowerArm',(s*.267,.017,.779),(s*.401,.012,.780),side+'UpperArm')
    bone(side+'Hand',(s*.401,.012,.780),(s*.447,.003,.778),side+'LowerArm')
    for name in ('Thumb','Index','Middle','Ring','Little'):
        pts=finger_specs[(side,name)]
        parent=side+'Hand'
        for i,suffix in enumerate(('Proximal','Intermediate','Distal')):
            n=side+name+suffix;bone(n,pts[i],pts[i+1],parent);parent=n
    bone(side+'UpperLeg',(s*.060,.014,.519),(s*.082,.008,.310),'Hips')
    bone(side+'LowerLeg',(s*.082,.008,.310),(s*.092,.022,.080),side+'UpperLeg')
    bone(side+'Foot',(s*.092,.022,.080),(s*.097,-.065,.030),side+'LowerLeg')
    bone(side+'Toes',(s*.097,-.065,.030),(s*.097,-.103,.025),side+'Foot')
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
    if u<.410:continue
    side='Left' if p.x>0 else 'Right';name=finger_name(p)
    pts=finger_specs[(side,name)]
    distance,t,segment,fraction,q=nearest_segment(p,pts)
    blend=smooth(0.,.25,t)
    weights={side+'Hand':1-blend}
    # Smooth transitions over joints, using three segment centers.
    anchors=(.18,.55,.85);vals=[math.exp(-((t-a)/.25)**2) for a in anchors];total=sum(vals)
    for suffix,w in zip(('Proximal','Intermediate','Distal'),vals):weights[side+name+suffix]=blend*w/total
    set_weights(v,weights)

# Keep smoothly calculated shoulder/cloak weights; isolate the compact hair/head.
for v in mesh.vertices:
    p=v.co
    if p.z>.868 or (abs(p.x)<.052 and p.y>.04 and p.z>.807):set_weights(v,{'Head':1})
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
        'origin':'soles','animation_actions':0,'protected_body_geometry_unchanged':True,'scope':'Hand repair and deform skeleton; no Unity import or authored bow animation.'}
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
    pts=finger_specs[(side,name)];direction=(pts[1]-pts[0]).normalized();axis=direction.cross(Vector((0,0,-1))).normalized()
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
reset();render('정면',(0,0,.50),(0,-2,.08),1.13);render('후면',(0,0,.50),(0,2,.08),1.13)
for pose in ('펴기','쥐기'):
    reset()
    if pose=='쥐기':
        for side in ('Left','Right'):
            for name in ('Thumb','Index','Middle','Ring','Little'):curl(side,name,.75)
    bpy.context.view_layer.update()
    for side,s in [('Left',1),('Right',-1)]:
        center=(s*.447,0,.768)
        for view,off in [('등',(0,-.12,.4)),('바닥',(0,-.12,-.4))]:render(side+'_'+pose+'_'+view,center,off,.15)
reset();turn('LeftUpperArm',(0,1,0),65);turn('RightUpperArm',(0,1,0),-65)
bpy.context.view_layer.update();render('팔내림',(0,0,.51),(0,-2,.08),1.13)
reset();turn('LeftUpperArm',(0,0,1),-60);turn('LeftLowerArm',(0,0,1),-15)
turn('RightUpperArm',(0,0,1),-10);turn('RightLowerArm',(0,0,1),135)
bpy.context.view_layer.update();render('팔굽힘',(0,0,.66),(1,-1.7,.5),.86)
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
