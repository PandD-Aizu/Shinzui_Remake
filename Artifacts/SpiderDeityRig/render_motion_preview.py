import bpy, math, pathlib
from mathutils import Vector
out=pathlib.Path(r'D:/Pandd/ShinShinzui/Artifacts/SpiderDeityRig')
bpy.ops.wm.open_mainfile(filepath=str(out/'SpiderDeity_Rigged.blend'));s=bpy.context.scene;arm=bpy.data.objects['SpiderDeity_Rig'];s.render.resolution_x=700;s.render.resolution_y=700;s.render.resolution_percentage=100
legs=['Leg_L01','Leg_L02','Leg_L03','Leg_L04','Leg_R01','Leg_R02','Leg_R03','Leg_R04'];original={n:bpy.data.objects['IK_'+n].location.copy() for n in legs}
for f in range(32):
 for i,name in enumerate(legs):
  a=max(0,math.sin(f/32*math.tau + (i%2)*math.pi));bpy.data.objects['IK_'+name].location=original[name]+Vector((0,-.018*a,.065*a));arm.pose.bones[name+'_Foot'].constraints[0].influence=1
 bpy.context.view_layer.update();s.render.filepath=str(out/f'preview_frame_{f:02d}.png');bpy.ops.render.render(write_still=True)
