import bpy, math
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r'C:/Users/koton/Downloads/spider deity 3d model.glb')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
print('OBJECT',mesh.name,'transform',mesh.matrix_world[:]);print('BOUNDS', [tuple(x) for x in mesh.bound_box])
s=bpy.context.scene;s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1300;s.render.resolution_y=1300;s.render.resolution_percentage=100
s.display.shading.light='STUDIO';s.display.shading.studiolight_rotate_z=.4;s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.62,.65,.7);s.display.shading.show_shadows=True;s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH';s.display.shading.background_type='WORLD';s.world=bpy.data.worlds.new('World');s.world.color=(.045,.045,.045)
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.15
for name,loc in [('front',(1.3,-2,1.05)),('right',(2,0,.65)),('top',(0,0,3))]:
 cam.location=loc;cam.rotation_euler=(Vector((0,0,.48))-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=r'D:/Pandd/ShinShinzui/Artifacts/SpiderDeityRig/'+name+'.png';bpy.ops.render.render(write_still=True)
