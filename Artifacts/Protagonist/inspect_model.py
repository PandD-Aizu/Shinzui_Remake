import bpy
import json
from mathutils import Vector

# GLBを読み込み実寸と表面形状を確認
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath='C:/Users/koton/Downloads/rugged man 3d model.glb')
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for obj in meshes:
    print('MODEL', obj.name, 'bounds', [list(obj.matrix_world @ Vector(v)) for v in obj.bound_box])
    print('COUNTS', len(obj.data.vertices), len(obj.data.polygons))

# 正面の確認用画像を出力
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.world.color = (0.4, 0.4, 0.4)
bpy.ops.object.camera_add(location=(0, -3, .5))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 0, .5)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 1.15
scene.camera = camera
for pos, power, size in [((0, -3, 3), 200, 3), ((-2, -1, 1), 80, 2)]:
    bpy.ops.object.light_add(type='AREA', location=pos)
    lamp = bpy.context.object
    lamp.data.energy = power
    lamp.data.shape = 'DISK'
    lamp.data.size = size
    lamp.rotation_euler = (Vector((0, 0, .5)) - lamp.location).to_track_quat('-Z', 'Y').to_euler()
scene.render.resolution_x = 1200
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.filepath = 'D:/Pandd/ShinShinzui/Artifacts/Protagonist/source-front.png'
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath='D:/Pandd/ShinShinzui/Artifacts/Protagonist/source-inspect.blend')
