import bpy,sys
sys.path.insert(0,'D:/Pandd/ShinShinzui/Artifacts/Protagonist')
from repair_skin_weights import repair_lower_body_weights
root='D:/Pandd/ShinShinzui/'
bpy.ops.wm.open_mainfile(filepath=root+'Artifacts/Protagonist/ProtagonistRig.blend')
body=bpy.data.objects['BodyMesh']
print('REPAIRED',repair_lower_body_weights(body))
bpy.ops.object.select_all(action='DESELECT')
for name in ['BodyMesh','HeadMesh','ProtagonistRig']:
    bpy.data.objects[name].select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['ProtagonistRig']
bpy.ops.wm.save_as_mainfile(filepath=root+'Artifacts/Protagonist/ProtagonistRig.blend')
bpy.ops.export_scene.fbx(filepath=root+'Assets/Shinzui/Art/Protagonist/Protagonist.fbx',use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL')
bpy.ops.export_scene.gltf(filepath=root+'Artifacts/Protagonist/ProtagonistRig.glb',use_selection=True,export_format='GLB',export_animations=False)
