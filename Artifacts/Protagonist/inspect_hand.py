import bpy
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='D:/Pandd/ShinShinzui/Artifacts/Protagonist/source-inspect.blend')
scene = bpy.context.scene
scene.camera.location = (.45, 0, 2)
scene.camera.rotation_euler = (Vector((.45, 0, .78)) - scene.camera.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera.data.ortho_scale = .22
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.filepath = 'D:/Pandd/ShinShinzui/Artifacts/Protagonist/hand-top.png'
bpy.ops.render.render(write_still=True)
