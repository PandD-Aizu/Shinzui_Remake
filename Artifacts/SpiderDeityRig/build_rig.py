import bpy,json,numpy as np, math,pathlib,sys
from mathutils import Vector
from mathutils.kdtree import KDTree
OUT=pathlib.Path(r'D:/Pandd/ShinShinzui/Artifacts/SpiderDeityRig'); UNITY=pathlib.Path(r'D:/Pandd/ShinShinzui/Assets/Shinzui/3DModels/SpiderDeity');UNITY.mkdir(exist_ok=True)
def pos(p):return Vector((p[0],-p[2],p[1]))
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=r'C:/Users/koton/Downloads/spider deity 3d model.glb')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH');mesh.name='SpiderDeity_Mesh';mesh.data.name='SpiderDeity_Geometry';mesh.parent=None
for o in list(bpy.data.objects):
 if o!=mesh:bpy.data.objects.remove(o,do_unlink=True)
definition=json.loads((OUT/'rig_definition.json').read_text());w=np.load(OUT/'weights.npz')['weights'];source=np.load(OUT/'mesh.npz')['v'];tree=KDTree(len(source))
for i,p in enumerate(source):tree.insert(pos(p),i)
tree.balance();mapping=[tree.find(v.co)[1] for v in mesh.data.vertices]
armdata=bpy.data.armatures.new('SpiderDeity_Skeleton');arm=bpy.data.objects.new('SpiderDeity_Rig',armdata);bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True);mesh.select_set(False);arm.show_in_front=True;armdata.display_type='OCTAHEDRAL'
bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None,deform=True,connect=False):
 b=armdata.edit_bones.new(name);b.head=head;b.tail=tail;b.use_deform=deform
 if parent:b.parent=armdata.edit_bones[parent];b.use_connect=connect
 return b
bone('Root',(0,0,0),(0,0,.12),deform=False);bone('Body',pos((0,.445,.02)),pos((0,.64,.02)),'Root')
for leg in definition['legs']:
 name=leg['name'];p=list(map(pos,leg['points']));par='Body'
 for i,suffix in enumerate(['Upper','Lower','Foot']):
  bn=name+'_'+suffix;bone(bn,p[i],p[i+1],par,connect=i>0);par=bn
 bone(name+'_Tip',p[3],p[3]+Vector((0,0,.025)),par,deform=False,connect=True)
bpy.ops.object.mode_set(mode='OBJECT')
for j,name in enumerate(definition['weight_names']):
 group=mesh.vertex_groups.new(name=name)
 for i,src in enumerate(mapping):
  value=float(w[src,j])
  if value>1e-7:group.add([i],value,'REPLACE')
mesh.parent=arm;mod=mesh.modifiers.new('SpiderDeity Skin','ARMATURE');mod.object=arm;mod.use_deform_preserve_volume=False
# Blender-only manipulators: constraints are disabled in the neutral export pose.
controls=bpy.data.collections.new('IK_Controls_Blender_Only');bpy.context.scene.collection.children.link(controls)
for leg in definition['legs']:
 name=leg['name'];p=list(map(pos,leg['points']));target=bpy.data.objects.new('IK_'+name,None);controls.objects.link(target);target.location=p[-1];target.empty_display_type='SPHERE';target.empty_display_size=.018
 target['purpose']='Move this target in Blender after enabling the corresponding IK constraint influence.'
 ik=arm.pose.bones[name+'_Foot'].constraints.new('IK');ik.name='Preview IK (enable influence)';ik.target=target;ik.chain_count=3;ik.iterations=128;ik.use_stretch=False;ik.influence=0
 for suffix in ['Upper','Lower','Foot']:arm.pose.bones[name+'_'+suffix].ik_stretch=0
arm['README']='8 legs, 3 deform segments and Tip per leg. IK constraints start at influence 0 for the unchanged bind pose. Enable per leg to test targets. Runtime Unity IK is not included.'
# Exact neutral pose verification.
bpy.context.view_layer.update();ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());err=max((ev.data.vertices[i].co-v.co).length for i,v in enumerate(mesh.data.vertices));assert err<1e-5,err
report={'source_vertices':len(source),'blender_vertices':len(mesh.data.vertices),'bones':len(armdata.bones),'deform_bones':sum(b.use_deform for b in armdata.bones),'unweighted_vertices':sum(not v.groups for v in mesh.data.vertices),'neutral_max_error':err,'per_leg_tests':[]}
# Check each IK chain independently, including actual evaluated mesh response.
base=np.array([v.co[:] for v in mesh.data.vertices]);
for leg in definition['legs']:
 name=leg['name'];target=bpy.data.objects['IK_'+name];orig=target.location.copy();target.location+=Vector((.006 if name.startswith('Leg_L') else -.006,-.01,.035));ik=arm.pose.bones[name+'_Foot'].constraints[0];ik.influence=1;bpy.context.view_layer.update();tip=arm.matrix_world@arm.pose.bones[name+'_Foot'].tail;error=(tip-target.location).length
 ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());pv=np.array([v.co[:] for v in ev.data.vertices]);delta=np.linalg.norm(pv-base,axis=1);assert np.isfinite(pv).all();assert error<.005,(name,error)
 bodyonly=np.array([w[src,0]>.99999 for src in mapping]);opposite=np.array([sum(w[src,j] for j,n in enumerate(definition['weight_names']) if n.startswith('Leg_R' if name.startswith('Leg_L') else 'Leg_L'))>.99999 for src in mapping]);bodyerr=float(delta[bodyonly].max(initial=0));opp=float(delta[opposite].max(initial=0));assert bodyerr<1e-5 and opp<1e-5
 report['per_leg_tests'].append({'leg':name,'target_error':error,'moved_vertices':int(sum(delta>1e-5)),'body_max_displacement':bodyerr,'opposite_legs_max_displacement':opp})
 ik.influence=0;target.location=orig;bpy.context.view_layer.update()
# Export only deformation hierarchy and mesh, no cameras or preview controls.
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);arm.select_set(True);bpy.context.view_layer.objects.active=arm
if '--skip-fbx' not in sys.argv:
 bpy.ops.export_scene.fbx(filepath=str(UNITY/'SpiderDeity_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_armature_deform_only=False)
bpy.ops.export_scene.gltf(filepath=str(OUT/'SpiderDeity_Rigged.glb'),export_format='GLB',use_selection=True,export_animations=False,export_skins=True)
# Studio preview and camera retained in editable blend only.
s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1200;s.render.resolution_y=1200;s.render.resolution_percentage=100;s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.62,.65,.7);s.display.shading.show_shadows=True;s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH';s.display.shading.background_type='WORLD';s.world=bpy.data.worlds.new('World');s.world.color=(.045,.045,.045)
bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='Preview_Camera';s.camera=cam;cam.location=(1.3,-2,1.05);cam.rotation_euler=(Vector((0,0,.48))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.15
s.render.filepath=str(OUT/'rigged_neutral.png');bpy.ops.render.render(write_still=True)
for name in ['Leg_L01','Leg_R03']:
 bpy.data.objects['IK_'+name].location+=Vector((0,-.025,.09));arm.pose.bones[name+'_Foot'].constraints[0].influence=1
bpy.context.view_layer.update();s.render.filepath=str(OUT/'rigged_pose_test.png');bpy.ops.render.render(write_still=True)
for leg in definition['legs']:
 name=leg['name'];arm.pose.bones[name+'_Foot'].constraints[0].influence=0;bpy.data.objects['IK_'+name].location=pos(leg['points'][-1])
bpy.context.view_layer.update();bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_distance=1.65;area.spaces.active.region_3d.view_location=(0,0,.5);area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion();area.spaces.active.clip_end=100
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SpiderDeity_Rigged.blend'))
(OUT/'validation.json').write_text(json.dumps(report,indent=2));print('VALIDATION',json.dumps(report))
