import bpy
bpy.ops.wm.open_mainfile(filepath=r'D:/Pandd/ShinShinzui/Artifacts/SpiderDeityRig/SpiderDeity_Rigged.blend')
bpy.ops.object.select_all(action='DESELECT')
for name in ['SpiderDeity_Rig','SpiderDeity_Mesh']:bpy.data.objects[name].select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['SpiderDeity_Rig']
bpy.ops.export_scene.fbx(filepath=r'D:/Pandd/ShinShinzui/Assets/Shinzui/3DModels/SpiderDeity/SpiderDeity_Rigged.fbx',use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_armature_deform_only=False)
